using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.SRM
{
    public class ExpensesController : Controller
    {
        private const string LinkedExpenseIdMarker = DocumentMarkers.LinkedExpenseId;
        /// <summary>Ishlab chiqarish → sotuv o'tkazmasi kirimidagi eski marker ("... EXPENS ID: 123").</summary>
        private const string LegacyExpenseIdMarker = "EXPENS ID: ";

        // Olish so'rovi: Expenses
        // status: null/"" = barchasi, "pending" = Kutilmoqda, "sent" = Yuborilgan
        // SRM-P04: qatorlar IndexData orqali sahifalab (SQL'da) yuklanadi; standart sana filtri — oxirgi 30 kun.
        public async Task<ActionResult> Index(int? warehouseId, string status, DateTime? fromDate, DateTime? toDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);

                ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, warehouseId);
                ViewBag.WarehouseId = warehouseId;
                ViewBag.Status = status ?? "";
                ViewBag.FromDate = DataTablesPageRequest.DefaultFromDate(Request, fromDate);
                ViewBag.ToDate = toDate;
                ViewBag.AssignedWarehouseIds = assignedWarehouseIds;
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses");
                return View();
            }
        }

        /// <summary>Chiqimlar ro'yxati qatori (proyeksiya).</summary>
        private sealed class ExpenseIndexRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public int SenderWHID { get; set; }
            public string SenderName { get; set; }
            public string ReceiverName { get; set; }
            public double? TotalPrice { get; set; }
            public bool Sent { get; set; }
            public DateTime IssueDateTime { get; set; }
        }

        private static IQueryable<ExpenseIndexRow> BuildIndexQuery(DBTHSNEntities db, int? warehouseId, string status,
            DateTime? fromDate, DateTime? toDate, string search)
        {
            var query = db.EXPENSES.AsNoTracking().Where(e => e.IsDeleted != true);

            if (warehouseId.HasValue)
                query = query.Where(e => e.SenderWHID == warehouseId.Value);
            if (status == "pending")
                query = query.Where(e => e.SendStatus != true);
            else if (status == "sent")
                query = query.Where(e => e.SendStatus == true);
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(e => e.IssueDateTime >= from);
            }
            if (toDate.HasValue)
            {
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(e => e.IssueDateTime < toExclusive);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(e => e.DocNo.Contains(s) || e.Warehouse.WHName.Contains(s) || e.Warehouse1.WHName.Contains(s));
            }

            return query.Select(e => new ExpenseIndexRow
            {
                ID = e.ID,
                DocNo = e.DocNo,
                SenderWHID = e.SenderWHID,
                SenderName = e.Warehouse.WHName,
                ReceiverName = e.Warehouse1.WHName,
                TotalPrice = (double?)e.TotalPrice,
                Sent = e.SendStatus == true,
                IssueDateTime = e.IssueDateTime
            });
        }

        private static IOrderedQueryable<ExpenseIndexRow> OrderIndexQuery(IQueryable<ExpenseIndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return desc ? query.OrderByDescending(r => r.DocNo) : query.OrderBy(r => r.DocNo);
                case 1: return desc ? query.OrderByDescending(r => r.SenderName) : query.OrderBy(r => r.SenderName);
                case 2: return desc ? query.OrderByDescending(r => r.ReceiverName) : query.OrderBy(r => r.ReceiverName);
                case 3: return desc ? query.OrderByDescending(r => r.TotalPrice) : query.OrderBy(r => r.TotalPrice);
                case 4: return desc ? query.OrderByDescending(r => r.Sent) : query.OrderBy(r => r.Sent);
                default: return desc ? query.OrderByDescending(r => r.IssueDateTime).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.IssueDateTime).ThenBy(r => r.ID);
            }
        }

        // Olish so'rovi: Expenses/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(int? warehouseId, string status, DateTime? fromDate, DateTime? toDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 5);
            using (var db = new DBTHSNEntities())
            {
                var assigned = new HashSet<int>(await GetAssignedWarehouseIdsAsync(db));
                var total = await BuildIndexQuery(db, warehouseId, status, fromDate, toDate, null).CountAsync();
                var filteredQuery = BuildIndexQuery(db, warehouseId, status, fromDate, toDate, page.Search);
                var filtered = page.Search == null ? total : await filteredQuery.CountAsync();

                var rows = await OrderIndexQuery(filteredQuery, page.OrderColumn, page.OrderDescending)
                    .Skip(page.Start)
                    .Take(page.Length)
                    .ToListAsync();

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = total,
                    recordsFiltered = filtered,
                    data = rows.Select(r => new
                    {
                        id = r.ID,
                        docNo = r.DocNo,
                        sender = r.SenderName,
                        receiver = r.ReceiverName,
                        total = (r.TotalPrice ?? 0).ToString("N2"),
                        sent = r.Sent,
                        date = r.IssueDateTime.ToString("dd.MM.yyyy"),
                        canModify = assigned.Contains(r.SenderWHID)
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: Expenses/ExportExcel — filtr bo'yicha barcha yozuvlar
        [HttpGet]
        public async Task<ActionResult> ExportExcel(int? warehouseId, string status, DateTime? fromDate, DateTime? toDate, string q)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var rows = await OrderIndexQuery(BuildIndexQuery(db, warehouseId, status, fromDate, toDate, q), 5, true)
                    .ToListAsync();

                var bytes = ExcelExport.Create("Ombor chiqimlari", rows,
                    ExcelExport.Col<ExpenseIndexRow>("Hujjat raqami", r => r.DocNo),
                    ExcelExport.Col<ExpenseIndexRow>("Yuboruvchi ombor", r => r.SenderName),
                    ExcelExport.Col<ExpenseIndexRow>("Qabul qiluvchi ombor", r => r.ReceiverName),
                    ExcelExport.Col<ExpenseIndexRow>("Jami summa", r => r.TotalPrice ?? 0, "#,##0.00"),
                    ExcelExport.Col<ExpenseIndexRow>("Status", r => r.Sent ? "Yuborilgan" : "Kutilmoqda"),
                    ExcelExport.Col<ExpenseIndexRow>("Sana", r => r.IssueDateTime, "dd.MM.yyyy"));

                return File(bytes, ExcelExport.ContentType, $"Ombor_chiqimlari_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        // Olish so'rovi: Expenses/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                // Ilovalar: faqat ID (avval havola uchun butun fayl blob'i yuklanardi — SRM-P12)
                ViewBag.DocIds = await db.WRHS_EXPENSE_DOCS.Where(d => d.ExpenseID == expense.ID).Select(d => d.ID).ToListAsync();

                // View barcha uchun ochiq; Edit/Delete faqat MRP uchun
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                ViewBag.CanEdit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses")
                    && assignedWarehouseId.HasValue
                    && expense.SenderWHID == assignedWarehouseId.Value
                    && expense.SendStatus != true;

                return View(expense);
            }
        }

        // Olish so'rovi: Expenses/yaratish
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    ViewBag.NoWarehouseError = "Sizga biriktirilgan ombor mavjud emas";
                    return View();
                }

                PopulateFormLookups(new[] { assignedWarehouseId.Value }, null, null, null);

                return View();
            }
        }

        // POST so'rovi: Expenses/GetAvailableProducts
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> GetAvailableProducts(int warehouseId, int? receiverWarehouseId)
        {
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    return Json(new { success = false, message = "Sizga biriktirilgan ombor mavjud emas" });
                }

                if (warehouseId != assignedWarehouseId.Value)
                {
                    return Json(new { success = false, message = "Siz faqat biriktirilgan ombor bo'yicha chiqim qila olasiz." });
                }

                var productsQuery = db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .Include(s => s.UNIT)
                    .Where(s => s.WHID == warehouseId && s.Amount > 0)
                    .AsQueryable();

                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                var salesWH = WarehouseFlowService.GetSalesWarehouse(db);

                var isProductionToSales = productionWH != null
                    && salesWH != null
                    && warehouseId == productionWH.ID
                    && receiverWarehouseId.HasValue
                    && receiverWarehouseId.Value == salesWH.ID;

                Dictionary<int, decimal> approvedRemaining = null;
                if (isProductionToSales)
                {
                    approvedRemaining = await GetApprovedRemainingForTransferAsync(db, productionWH.ID, salesWH.ID, null);
                }

                var products = await productsQuery
                    .Select(s => new
                    {
                        ProductID = s.ProductID,
                        ProductPNo = s.PRODUCT.PNo,
                        ProductName = s.PRODUCT.Name,
                        AvailableStock = s.Amount,
                        UnitID = s.UnitID,
                        UnitName = s.UNIT.ShortName
                    })
                    .ToListAsync();

                if (isProductionToSales && approvedRemaining != null)
                {
                    products = products
                        .Select(p => new
                        {
                            p.ProductID,
                            p.ProductPNo,
                            p.ProductName,
                            AvailableStock = Math.Max(0, Math.Min((decimal)(approvedRemaining.ContainsKey(p.ProductID) ? approvedRemaining[p.ProductID] : 0), p.AvailableStock)),
                            p.UnitID,
                            p.UnitName
                        })
                        .Where(p => p.AvailableStock > 0)
                        .ToList();
                }

                return Json(new { success = true, data = products });
            }
        }

        // POST so'rovi: Expenses/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ViewModels.ExpenseCreateViewModel model, HttpPostedFileBase expenseFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (!assignedWarehouseId.HasValue)
                            {
                                TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                                return RedirectToAction("Index");
                            }

                            if (model.SenderWHID != assignedWarehouseId.Value)
                            {
                                TempData["Error"] = "Siz faqat biriktirilgan ombordan chiqim qilishingiz mumkin.";
                                return RedirectToAction("Index");
                            }

                            if (model.ExpenseProducts == null)
                            {
                                model.ExpenseProducts = new List<ViewModels.ExpenseProductViewModel>();
                            }

                            // Qoldiq tekshiruvi: bir mahsulot bir necha qatorda bo'lsa — jami miqdor bo'yicha (SRM-B08).
                            // Yakuniy kafolat — StockLedger'dagi atomik yozuv (yetmasa xato, hech narsa yozilmaydi).
                            var stockError = CheckStockAvailability(db, model.SenderWHID, model.ExpenseProducts);
                            if (stockError != null)
                            {
                                ModelState.AddModelError("", stockError);
                                transaction.Rollback();
                                goto RepopulateViewBag;
                            }

                            var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                            var salesWH = WarehouseFlowService.GetSalesWarehouse(db);

                            var isProductionToSales = productionWH != null
                                && salesWH != null
                                && model.SenderWHID == productionWH.ID
                                && model.ReceiverWhID.HasValue
                                && model.ReceiverWhID.Value == salesWH.ID;

                            if (isProductionToSales)
                            {
                                // Faqat so'ralgan mahsulotlar bo'yicha (avval barcha mahsulotlar skanerlanardi — SRM-P15);
                                // bir mahsulot bir necha qatorda bo'lsa — jami miqdor tekshiriladi.
                                var requested = model.ExpenseProducts
                                    .GroupBy(p => p.ProductID)
                                    .Select(g => new { ProductID = g.Key, Qty = (decimal)g.Sum(x => x.Amount), PNo = g.First().ProductPNo })
                                    .ToList();
                                var remainingByProduct = await GetApprovedRemainingForTransferAsync(
                                    db, productionWH.ID, salesWH.ID, requested.Select(r => r.ProductID).ToList());

                                foreach (var r in requested)
                                {
                                    decimal remaining;
                                    remainingByProduct.TryGetValue(r.ProductID, out remaining);
                                    if (r.Qty > remaining)
                                    {
                                        ModelState.AddModelError("",
                                            $"Mahsulot {r.PNo} uchun sifat nazorati tasdiqlangan miqdor yetarli emas. Qoldiq: {remaining}, Talab: {r.Qty}");
                                        transaction.Rollback();
                                        goto RepopulateViewBag;
                                    }
                                }
                            }

                            // Yaratish DocNo agar not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Yaratish expense header
                            var expense = new EXPENS
                            {
                                DocNo = model.DocNo,
                                IssueDateTime = model.IssueDateTime,
                                SenderWHID = model.SenderWHID,
                                ReceiverWhID = model.ReceiverWhID,
                                CurrencyID = model.CurrencyID,
                                Amount = 0,
                                TotalPrice = 0,
                                Description = model.Description,
                                SendStatus = false,
                                IsDeleted = false
                            };

                            db.EXPENSES.Add(expense);
                            await db.SaveChangesAsync();

                            // Qo'shish expense products va decrease stock
                            // Qatorlar: saqlash → sof FIFO chiqim → FIFO tannarx (ExpenseDocumentService, SRM-M01/B13/B19)
                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                await ExpenseDocumentService.PostLinesAsync(db, expense, model.ExpenseProducts);

                            await db.SaveChangesAsync();

                            // Ichki o'tkazma bo'lsa linked income hujjatini majburiy upsert qilamiz.
                            if (expense.ReceiverWhID.HasValue && expense.ReceiverWhID.Value != expense.SenderWHID)
                            {
                                await CreateLinkedIncomeDocumentForExpenseAsync(expense, model.ExpenseProducts, db);
                            }
                            }

                            // Upload expense hujjat agar provided
                            if (expenseFile != null && expenseFile.ContentLength > 0)
                            {
                                var fileData = WarehouseAccess.ReadUpload(expenseFile);

                                var expenseDoc = new WRHS_EXPENSE_DOCS
                                {
                                    ExpenseID = expense.ID,
                                    Doc = fileData
                                };

                                db.WRHS_EXPENSE_DOCS.Add(expenseDoc);
                                await db.SaveChangesAsync();
                            }

                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Create - {expense.DocNo} chiqim hujjati yaratildi");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            var errorMessage = WarehouseAccess.ErrorMessage(ex);
                            ModelState.AddModelError("", errorMessage);
                        }
                    }
                }
            }

            RepopulateViewBag:
            // Xato bilan qaytishda forma to'liq qayta to'ldiriladi (SRM-B24: Units yo'q edi)
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                PopulateFormLookups(assignedWarehouseId.HasValue ? new[] { assignedWarehouseId.Value } : null,
                    model.SenderWHID, model.ReceiverWhID, model.CurrencyID);
            }

            return View(model);
        }

        /// <summary>
        /// Keyingi chiqim raqami: {yyyyMM}_{n}, tranzaksiya ichida qulf bilan (SRM-B07: avval yilsiz
        /// "{oy}_" prefiksi, satr bo'yicha saralash va parallel so'rovlarda takroriy raqam).
        /// </summary>
        private static Task<string> GenerateDocNo(DBTHSNEntities db)
        {
            return DocumentNumberService.NextAsync(db, "EXPENSES", "DocNo", DateTime.Now.ToString("yyyyMM") + "_");
        }

        /// <summary>
        /// Ishlab chiqarish → sotuv o'tkazmasi uchun sifat nazoratidan o'tgan (APPROVED), lekin hali
        /// o'tkazilmagan miqdor: mahsulot bo'yicha QtyAccepted − o'tkazilgan. <paramref name="productIds"/> null
        /// bo'lsa — barcha mahsulotlar. (Avval GetAvailableProducts va Create'da bir xil kodning ikki nusxasi edi.)
        /// </summary>
        private static async Task<Dictionary<int, decimal>> GetApprovedRemainingForTransferAsync(
            DBTHSNEntities db, int productionWhId, int salesWhId, List<int> productIds)
        {
            var approvedQuery = db.QUALITY_INSPECTION.Where(q => q.StatusCode == "APPROVED" && q.IsDeleted == false);
            var transferredQuery = db.EXPENSE_PRODUCTS.Where(ep => ep.EXPENS.SenderWHID == productionWhId
                && ep.EXPENS.ReceiverWhID == salesWhId
                && ep.EXPENS.IsDeleted == false);
            if (productIds != null)
            {
                approvedQuery = approvedQuery.Where(q => productIds.Contains(q.ProductID));
                transferredQuery = transferredQuery.Where(ep => productIds.Contains(ep.ProductID));
            }

            var approvedByProduct = await approvedQuery
                .GroupBy(q => q.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.QtyAccepted) ?? 0 })
                .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

            var transferredByProduct = await transferredQuery
                .GroupBy(ep => ep.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.Amount) ?? 0 })
                .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

            return approvedByProduct.ToDictionary(
                k => k.Key,
                k => k.Value - (transferredByProduct.ContainsKey(k.Key) ? transferredByProduct[k.Key] : 0));
        }

        /// <summary>
        /// Chiqim qatorlari uchun qoldiq yetarliligi (mahsulot bo'yicha jami). null — yetarli.
        /// </summary>
        private static string CheckStockAvailability(DBTHSNEntities db, int warehouseId,
            IEnumerable<ViewModels.ExpenseProductViewModel> lines)
        {
            var requested = (lines ?? Enumerable.Empty<ViewModels.ExpenseProductViewModel>())
                .GroupBy(p => p.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Amount), PNo = g.First().ProductPNo })
                .ToList();
            var onHand = StockLedger.GetOnHand(db, warehouseId, requested.Select(r => r.ProductID));

            foreach (var r in requested)
            {
                double available;
                onHand.TryGetValue(r.ProductID, out available);
                if (available + StockLedger.Epsilon < r.Qty)
                    return $"Mahsulot {r.PNo} uchun yetarli qoldiq yo'q. Mavjud: {available}, Talab: {r.Qty}";
            }
            return null;
        }

        /// <summary>Chiqim formasi spravochniklari (keshdan): yuboruvchi (ruxsat etilgan) va qabul qiluvchi omborlar, valyuta, birlik.</summary>
        private void PopulateFormLookups(IEnumerable<int> allowedSenderIds, int? senderId, int? receiverId, int? currencyId)
        {
            var warehouses = ReferenceDataCache.Get(ReferenceDataCache.Warehouses);
            var senders = warehouses.AsEnumerable();
            if (allowedSenderIds != null)
            {
                var allowed = new HashSet<int>(allowedSenderIds);
                senders = senders.Where(w => allowed.Contains(w.ID));
            }

            ViewBag.SenderWarehouses = new SelectList(senders.ToList(), "ID", "Name", senderId);
            ViewBag.ReceiverWarehouses = new SelectList(warehouses, "ID", "Name", receiverId);
            ViewBag.Currencies = ReferenceDataCache.SelectList(ReferenceDataCache.Currencies, currencyId);
            ViewBag.Units = new SelectList(
                ReferenceDataCache.Get(ReferenceDataCache.Units)
                    .Select(u => new { u.ID, ShortName = u.Extra ?? u.Name })
                    .ToList(),
                "ID", "ShortName");
        }

        /// <summary>
        /// Chiqimdan avtomatik yaratilgan (o'chirilmagan) kirim: "EXPENSE_ID:n" yoki eski "EXPENS ID: n"
        /// markeri bo'yicha ANIQ moslik (SRM-B06: avval LIKE '%EXPENSE_ID:12%' 120, 121... ga ham mos kelardi
        /// va boshqa chiqimning kirimi qayta yozilardi).
        /// </summary>
        private static async Task<INCOME> FindLinkedIncomeAsync(DBTHSNEntities db, int expenseId, bool includeLines)
        {
            var marker = DocumentMarkers.For(LinkedExpenseIdMarker, expenseId);
            var legacyMarker = DocumentMarkers.For(LegacyExpenseIdMarker, expenseId);

            IQueryable<INCOME> query = db.INCOMES;
            if (includeLines)
                query = query.Include(i => i.INCOME_PRODUCTS);

            var candidates = await query
                .Where(i => i.IsDeleted != true && i.Description != null
                    && (i.Description.Contains(marker) || i.Description.Contains(legacyMarker)))
                .ToListAsync();

            return candidates.FirstOrDefault(i =>
                DocumentMarkers.Matches(i.Description, LinkedExpenseIdMarker, expenseId)
                || DocumentMarkers.Matches(i.Description, LegacyExpenseIdMarker, expenseId));
        }

        /// <summary>Foydalanuvchiga biriktirilgan birinchi ombor; biriktirilmagan bo'lsa null (SRM-B16).</summary>
        private async Task<int?> GetAssignedWarehouseIdAsync(DBTHSNEntities db)
        {
            var ids = await GetAssignedWarehouseIdsAsync(db);
            return ids.Count == 0 ? (int?)null : ids[0];
        }

        private Task<List<int>> GetAssignedWarehouseIdsAsync(DBTHSNEntities db)
        {
            return WarehouseAccess.AssignedWarehouseIdsAsync(db, User.Identity.Name);
        }

        // Olish so'rovi: Expenses/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(expense.SenderWHID))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                // Tekshirish agar already sent (SendStatus == true)
                if (expense.SendStatus == true)
                {
                    TempData["Error"] = "Bu chiqim hujjati allaqachon yuborilgan va tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

                var productIds = expense.EXPENSE_PRODUCTS.Select(ep => ep.ProductID).ToList();
                var stockMap = await db.PRODUCT_STOCKS
                    .Where(s => s.WHID == expense.SenderWHID && productIds.Contains(s.ProductID))
                    .ToListAsync();

                var model = new ViewModels.ExpenseEditViewModel
                {
                    ID = expense.ID,
                    DocNo = expense.DocNo,
                    IssueDateTime = expense.IssueDateTime,
                    SenderWHID = expense.SenderWHID,
                    ReceiverWhID = expense.ReceiverWhID,
                    CurrencyID = expense.CurrencyID,
                    Amount = (double?)expense.Amount,
                    TotalPrice = (double?)expense.TotalPrice,
                    Description = expense.Description,
                    ExpenseProducts = expense.EXPENSE_PRODUCTS.Select(ep => new ViewModels.ExpenseProductViewModel
                    {
                        ID = ep.ID,
                        ProductID = ep.ProductID,
                        ProductPNo = ep.PRODUCT.PNo,
                        ProductName = ep.PRODUCT.Name,
                        Amount = (double)ep.Amount,
                        UnitID = ep.UnitID,
                        UnitName = ep.UNIT != null ? ep.UNIT.ShortName : null,
                        PiecePrice = (double)ep.PiecePrice,
                        TotalPrice = (double)ep.TotalPrice,
                        // Eski yozuvlardagi "IncomeID:n" lot markeri foydalanuvchiga izoh sifatida ko'rsatilmaydi
                        Comment = LotTrackingService.ParseIncomeIdFromComment(ep.Comment).HasValue ? "" : (ep.Comment ?? ""),
                        AvailableStock = (double)(stockMap.FirstOrDefault(s => s.ProductID == ep.ProductID)?.Amount ?? 0)
                    }).ToList()
                };

                PopulateFormLookups(new[] { expense.SenderWHID }, model.SenderWHID, model.ReceiverWhID, model.CurrencyID);

                return View(model);
            }
        }

        // POST so'rovi: Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ViewModels.ExpenseEditViewModel model, HttpPostedFileBase expenseFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var expense = await db.EXPENSES.FindAsync(model.ID);
                            if (expense == null || expense.IsDeleted == true)
                            {
                                return HttpNotFound();
                            }

                            var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                            if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(expense.SenderWHID))
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            if (expense.SendStatus == true)
                            {
                                TempData["Error"] = "Bu chiqim hujjati allaqachon yuborilgan va tahrirlash mumkin emas.";
                                return RedirectToAction("Index");
                            }

                            // Bog'langan kirim qabul qilingan bo'lsa, chiqimni o'zgartirib bo'lmaydi (qabul qiluvchi
                            // ombordagi qoldiq eski qatorlar bo'yicha kirgan).
                            var linkedIncome = await FindLinkedIncomeAsync(db, expense.ID, includeLines: false);
                            if (linkedIncome != null && linkedIncome.RecieveStatus == true)
                            {
                                TempData["Error"] = "Bog'langan kirim allaqachon qabul qilingan — chiqimni tahrirlash mumkin emas.";
                                return RedirectToAction("Details", new { id = expense.ID });
                            }

                            // SRM-B15: yuboruvchi ombor o'zgartirilmaydi (avval model.SenderWHID tekshiruvsiz yozilardi)
                            model.SenderWHID = expense.SenderWHID;

                            // SRM-B05: eski qatorlar to'liq bekor qilinadi — PRODUCT_STOCKS bilan birga DLOC lot
                            // ajratmalari (EXPENSE_PRODUCT_ALLOCATIONS, LOT_LOCATIONS, INCOME_LOTS.QtyConsumed) ham.
                            // Avval faqat PRODUCT_STOCKS qaytarilardi va har tahrirda lot miqdori ikki marta "yeyilardi".
                            var oldProducts = await db.EXPENSE_PRODUCTS
                                .Where(ep => ep.ExpenseID == expense.ID)
                                .ToListAsync();

                            foreach (var oldProduct in oldProducts)
                            {
                                StockLedger.ReverseIssueLine(db, expense.SenderWHID, oldProduct.ProductID, (double)oldProduct.Amount,
                                    oldProduct.UnitID > 0 ? oldProduct.UnitID : 1, oldProduct.ID);
                            }

                            // Tekshirish new stock availability
                            if (model.ExpenseProducts == null)
                            {
                                model.ExpenseProducts = new List<ViewModels.ExpenseProductViewModel>();
                            }

                            var stockError = CheckStockAvailability(db, expense.SenderWHID, model.ExpenseProducts);
                            if (stockError != null)
                            {
                                ModelState.AddModelError("", stockError);
                                transaction.Rollback();
                                goto RepopulateViewBag;
                            }

                            // Yangilash expense header
                            expense.DocNo = model.DocNo;
                            expense.IssueDateTime = model.IssueDateTime;
                            expense.ReceiverWhID = model.ReceiverWhID;
                            expense.CurrencyID = model.CurrencyID;
                            expense.Description = model.Description;

                            // O'chirish existing expense products
                            foreach (var existing in oldProducts)
                            {
                                db.EXPENSE_PRODUCTS.Remove(existing);
                            }

                            // Qo'shish updated expense products va yangilash stock
                            // Qatorlar: saqlash → sof FIFO chiqim → FIFO tannarx (ExpenseDocumentService, SRM-M01/B13/B19)
                            expense.Amount = 0;
                            expense.TotalPrice = 0;
                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                await ExpenseDocumentService.PostLinesAsync(db, expense, model.ExpenseProducts);
                            }

                            // Handle fayl upload
                            if (expenseFile != null && expenseFile.ContentLength > 0)
                            {
                                // O'chirish existing docs
                                var existingDocs = db.WRHS_EXPENSE_DOCS.Where(ed => ed.ExpenseID == expense.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_EXPENSE_DOCS.Remove(doc);
                                }

                                // Qo'shish new doc
                                var fileData = WarehouseAccess.ReadUpload(expenseFile);

                                var expenseDoc = new WRHS_EXPENSE_DOCS
                                {
                                    ExpenseID = expense.ID,
                                    Doc = fileData
                                };

                                db.WRHS_EXPENSE_DOCS.Add(expenseDoc);
                            }

                            await db.SaveChangesAsync();

                            // Editda ham linked income hujjatini yangilab/yaratib turamiz.
                            if (expense.ReceiverWhID.HasValue && expense.ReceiverWhID.Value != expense.SenderWHID)
                            {
                                await CreateLinkedIncomeDocumentForExpenseAsync(expense, model.ExpenseProducts, db);
                            }
                            else if (linkedIncome != null)
                            {
                                // Endi ichki o'tkazma emas — kutilayotgan bog'langan kirim bekor qilinadi
                                linkedIncome.IsDeleted = true;
                                await db.SaveChangesAsync();
                            }
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Edit - {expense.DocNo} chiqim hujjati tahrirlandi");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            var errorMessage = WarehouseAccess.ErrorMessage(ex);
                            ModelState.AddModelError("", errorMessage);
                        }
                    }
                }
            }

            RepopulateViewBag:
            // Xato bilan qaytishda forma to'liq qayta to'ldiriladi (SRM-B24: Units yo'qligi sababli Edit yiqilardi)
            PopulateFormLookups(new[] { model.SenderWHID }, model.SenderWHID, model.ReceiverWhID, model.CurrencyID);

            return View(model);
        }

        // Olish so'rovi: Expenses/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (!assignedWarehouseIds.Contains(expense.SenderWHID))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                var blockReason = await GetDeleteBlockReasonAsync(db, expense);
                if (blockReason != null)
                {
                    TempData["Error"] = blockReason;
                    return RedirectToAction("Details", new { id = expense.ID });
                }

                return View(expense);
            }
        }

        /// <summary>
        /// SRM-B04: yuborilgan (qabul qilingan) chiqimni yoki bog'langan kirimi qabul qilingan
        /// o'tkazmani o'chirib bo'lmaydi — aks holda tovar ikki omborda birdan "mavjud" bo'lib qolardi.
        /// </summary>
        private static async Task<string> GetDeleteBlockReasonAsync(DBTHSNEntities db, EXPENS expense)
        {
            if (expense.SendStatus == true)
                return "Bu chiqim hujjati yuborilgan (qabul qilingan) — o'chirib bo'lmaydi. Kerak bo'lsa, qabul qiluvchi ombor kirimini bekor qiling.";

            var linkedIncome = await FindLinkedIncomeAsync(db, expense.ID, includeLines: false);
            if (linkedIncome != null && linkedIncome.RecieveStatus == true)
                return $"Bog'langan kirim «{linkedIncome.DocNo}» allaqachon qabul qilingan — chiqimni o'chirib bo'lmaydi.";

            return null;
        }

        // POST so'rovi: Expenses/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var expense = await db.EXPENSES.FindAsync(id);
                        if (expense != null && expense.IsDeleted != true)
                        {
                            var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                            if (!assignedWarehouseIds.Contains(expense.SenderWHID))
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            // Kirim qabul qilinishi bilan poyga bo'lmasligi uchun bog'langan kirim qatori qulflanadi
                            var blockReason = await GetDeleteBlockReasonAsync(db, expense);
                            if (blockReason != null)
                            {
                                transaction.Rollback();
                                TempData["Error"] = blockReason;
                                return RedirectToAction("Details", new { id = expense.ID });
                            }

                            var linkedIncome = await FindLinkedIncomeAsync(db, expense.ID, includeLines: false);
                            if (linkedIncome != null)
                            {
                                var receivedNow = await db.Database.SqlQuery<int>(
                                    "SELECT CAST(ISNULL(RecieveStatus, 0) AS INT) FROM dbo.INCOMES WITH (UPDLOCK, HOLDLOCK) WHERE ID = @p0",
                                    linkedIncome.ID).FirstOrDefaultAsync();
                                if (receivedNow == 1)
                                {
                                    transaction.Rollback();
                                    TempData["Error"] = $"Bog'langan kirim «{linkedIncome.DocNo}» allaqachon qabul qilingan — chiqimni o'chirib bo'lmaydi.";
                                    return RedirectToAction("Details", new { id = expense.ID });
                                }

                                // Kutilayotgan bog'langan kirim ham bekor qilinadi (aks holda qabul qiluvchi uni
                                // "Qabul qilish" bilan yo'qdan qoldiq yaratardi)
                                linkedIncome.IsDeleted = true;
                            }

                            // Yuboruvchi ombor qoldig'i DLOC lot ajratmalari bilan birga qaytariladi (SRM-B04/B05)
                            var expenseProducts = await db.EXPENSE_PRODUCTS
                                .Where(ep => ep.ExpenseID == expense.ID)
                                .ToListAsync();

                            foreach (var product in expenseProducts)
                            {
                                StockLedger.ReverseIssueLine(db, expense.SenderWHID, product.ProductID, (double)product.Amount,
                                    product.UnitID > 0 ? product.UnitID : 1, product.ID);
                            }

                            expense.IsDeleted = true;
                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Delete - {expense.DocNo} chiqim hujjati o'chirildi");
                        }

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Index");
                    }
                }
            }
        }

        // Olish so'rovi: Expenses/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            // SRM-S04: avval istalgan foydalanuvchi ketma-ket ID bilan istalgan hujjatni yuklab olardi
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.WRHS_EXPENSE_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                string fileName;
                var contentType = WarehouseAttachments.ContentType(doc.Doc, $"Expense_{doc.ExpenseID}", out fileName);
                return File(doc.Doc, contentType, fileName);
            }
        }

        /// <summary>
        /// Kutilmoqdagi chiqimlar uchun yetishmayotgan kirim hujjatlarini yaratadi (mavjud ma'lumotlarni tuzatish).
        /// POST + antiforgery (SRM-B18); qabul qilingan kirimlarga tegilmaydi; har chiqim o'z tranzaksiyasida.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RepairMissingLinkedIncomes()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            int created = 0;
            int updated = 0;
            int skipped = 0;
            var errors = new List<string>();
            using (var db = new DBTHSNEntities())
            {
                var pendingExpenses = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IsDeleted != true
                        && e.ReceiverWhID.HasValue
                        && e.ReceiverWhID.Value != e.SenderWHID)
                    .ToListAsync();

                foreach (var exp in pendingExpenses)
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var products = exp.EXPENSE_PRODUCTS.Select(ep => new ViewModels.ExpenseProductViewModel
                            {
                                ProductID = ep.ProductID,
                                Amount = (double)ep.Amount,
                                PiecePrice = (double)ep.PiecePrice,
                                TotalPrice = (double)ep.TotalPrice,
                                UnitID = ep.UnitID
                            }).ToList();

                            if (!products.Any())
                            {
                                skipped++;
                                continue;
                            }

                            var existing = await FindLinkedIncomeAsync(db, exp.ID, includeLines: false);
                            if (existing != null && existing.RecieveStatus == true)
                            {
                                // Qabul qilingan kirim qatorlari qayta yozilmaydi (qoldiq ular bo'yicha kirgan)
                                skipped++;
                                continue;
                            }

                            await CreateLinkedIncomeDocumentForExpenseAsync(exp, products, db);
                            transaction.Commit();
                            if (existing != null) updated++;
                            else created++;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            errors.Add($"{exp.DocNo}: {ex.Message}");
                        }
                    }
                }
            }

            if (errors.Any())
            {
                TempData["Error"] =
                    $"Tuzatish qisman bajarildi. Yaratildi: {created}, yangilandi: {updated}, o'tkazib yuborildi: {skipped}, xatolar: {errors.Count}. " +
                    string.Join(" | ", errors.Take(3));
            }
            else
            {
                TempData["Success"] =
                    $"Tuzatish tugadi. Yaratildi: {created}, yangilandi: {updated}, o'tkazib yuborildi: {skipped}.";
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Mavjud ma'lumotlarni tuzatish: qabul qilingan (RecieveStatus = true) ichki o'tkazma
        /// kirimlariga bog'langan chiqimlarni "yuborilgan" (SendStatus = true) deb belgilaydi.
        /// Shu sabab chiqim qilgan ombor ham qabul qilingan holatni ko'rsatadi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RepairTransferExpenseStatuses()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            int fixedCount = 0, alreadyOk = 0, skipped = 0;
            var errors = new List<string>();

            using (var db = new DBTHSNEntities())
            {
                var receivedTransferIncomes = await db.INCOMES
                    .Where(i => i.IsDeleted != true
                        && i.RecieveStatus == true
                        && i.Description != null
                        && i.Description.Contains(LinkedExpenseIdMarker))
                    .ToListAsync();

                foreach (var income in receivedTransferIncomes)
                {
                    try
                    {
                        var parsedId = DocumentMarkers.Parse(income.Description, LinkedExpenseIdMarker);
                        if (!parsedId.HasValue)
                        {
                            skipped++;
                            continue;
                        }
                        var expenseId = parsedId.Value;

                        var expense = await db.EXPENSES.FirstOrDefaultAsync(e => e.ID == expenseId && e.IsDeleted != true);
                        if (expense == null)
                        {
                            skipped++;
                            continue;
                        }

                        if (expense.SendStatus == true)
                        {
                            alreadyOk++;
                            continue;
                        }

                        expense.SendStatus = true;
                        fixedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{income.DocNo}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync();
            }

            var summary = $"Tuzatildi: {fixedCount}, allaqachon to'g'ri: {alreadyOk}, o'tkazib yuborildi: {skipped}, xatolar: {errors.Count}.";
            if (errors.Any())
                TempData["Error"] = summary + " " + string.Join(" | ", errors.Take(5));
            else
                TempData["Success"] = summary;

            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", "RepairTransferExpenseStatuses: " + summary);
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Ichki o'tkazma uchun avtomatik kirim hujjati yaratadi (Xomashyo → Ishlab chiqarish/Sotuv)
        /// Har bir chiqim uchun barcha mahsulotlar bilan BIRTA kirim hujjati yaratadi
        /// </summary>
        private async Task CreateLinkedIncomeDocumentForExpenseAsync(EXPENS expense, List<ViewModels.ExpenseProductViewModel> products, DBTHSNEntities db)
        {
            if (!expense.ReceiverWhID.HasValue || products == null || !products.Any())
                return;

            // Har qanday ichki o'tkazma (sender != receiver) uchun pending kirim yaratiladi.
            // Bu ombor nomlari/lookup o'zgarib ketganda ham barqaror ishlaydi.
            if (expense.ReceiverWhID.Value == expense.SenderWHID)
                return;

            var marker = DocumentMarkers.For(LinkedExpenseIdMarker, expense.ID);
            var linkedIncome = await FindLinkedIncomeAsync(db, expense.ID, includeLines: true);

            if (linkedIncome != null && linkedIncome.RecieveStatus == true)
            {
                // Qabul qilingan kirimni qayta yozish qoldiqni buzadi (qatorlar bo'yicha allaqachon kirgan)
                throw new InvalidOperationException(
                    $"Bog'langan kirim «{linkedIncome.DocNo}» allaqachon qabul qilingan — uni o'zgartirib bo'lmaydi.");
            }

            if (linkedIncome == null)
            {
                var incomeDocNo = await GenerateIncomeDocNo(db);
                linkedIncome = new INCOME
                {
                    DocNo = incomeDocNo,
                    IssueDateTime = expense.IssueDateTime,
                    ReceivingWHID = expense.ReceiverWhID.Value,
                    SenderWHID = expense.SenderWHID,
                    CurrencyID = expense.CurrencyID,
                    Description = $"Chiqimdan avtomatik bog'langan: {expense.DocNo} - {expense.Description} | {marker}",
                    RecieveStatus = false, // MRP qabul qilishi uchun pending
                    IsDeleted = false
                };
                db.INCOMES.Add(linkedIncome);
                await db.SaveChangesAsync();
            }
            else
            {
                linkedIncome.IssueDateTime = expense.IssueDateTime;
                linkedIncome.ReceivingWHID = expense.ReceiverWhID.Value;
                linkedIncome.SenderWHID = expense.SenderWHID;
                linkedIncome.CurrencyID = expense.CurrencyID;
                linkedIncome.Description = $"Chiqimdan avtomatik bog'langan: {expense.DocNo} - {expense.Description} | {marker}";
                linkedIncome.RecieveStatus = false;

                if (linkedIncome.INCOME_PRODUCTS != null && linkedIncome.INCOME_PRODUCTS.Any())
                {
                    db.INCOME_PRODUCTS.RemoveRange(linkedIncome.INCOME_PRODUCTS.ToList());
                }
            }

            double totalAmount = 0;
            double totalPrice = 0;
            foreach (var product in products)
            {
                var lineTotal = product.TotalPrice != 0
                    ? product.TotalPrice
                    : (product.Amount * product.PiecePrice);
                var incomeProduct = new INCOME_PRODUCTS
                {
                    IncomeID = linkedIncome.ID,
                    ProductID = product.ProductID,
                    Amount = (decimal)product.Amount,
                    PiecePrice = (decimal?)product.PiecePrice,
                    TotalPrice = (decimal?)lineTotal,
                    UnitID = product.UnitID,
                    Comment = $"Chiqimdan bog'langan: {expense.DocNo}"
                };

                db.INCOME_PRODUCTS.Add(incomeProduct);
                totalAmount += product.Amount;
                totalPrice += lineTotal;
            }

            linkedIncome.Amount = (decimal?)totalAmount;
            linkedIncome.TotalPrice = (decimal?)totalPrice;
            await db.SaveChangesAsync();

            LogHelper.LogToDatabase(User.Identity.Name, "Expenses",
                $"Chiqimdan avtomatik kirim upsert qilindi: {linkedIncome.DocNo}. Chiqim: {expense.DocNo}, Mahsulotlar: {products.Count} ta");
        }

        /// <summary>
        /// Bog'langan kirim raqami: INC-{yyyyMM}_{n}, tranzaksiya ichida qulf bilan (SRM-B07).
        /// </summary>
        private static Task<string> GenerateIncomeDocNo(DBTHSNEntities db)
        {
            return DocumentNumberService.NextAsync(db, "INCOMES", "DocNo", "INC-" + DateTime.Now.ToString("yyyyMM") + "_");
        }
    }

}
