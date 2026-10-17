using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;
using tahsinERP.ViewModels;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.SRM
{
    public class IncomesController : Controller
    {
        private static readonly Regex IncomeLineVatInComment = new Regex(
            @"\s*\|\s*QQS\s*%:\s*[\d.,]+\s*%|\s*QQS\s*%:\s*[\d.,]+\s*%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Olish so'rovi: Incomes
        public async Task<ActionResult> Index(int? status, int? warehouseId)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                var incomesQuery = db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Where(i => i.IsDeleted != true);

                // View barcha uchun ochiq - faqat filtr bo'yicha (ombor, status tanlansa)
                if (warehouseId.HasValue)
                {
                    incomesQuery = incomesQuery.Where(i => i.ReceivingWHID == warehouseId.Value);
                }

                if (status.HasValue)
                {
                    if (status.Value == 1)
                    {
                        incomesQuery = incomesQuery.Where(i => i.RecieveStatus == true);
                    }
                    else if (status.Value == 0)
                    {
                        incomesQuery = incomesQuery.Where(i => i.RecieveStatus != true);
                    }
                }

                var incomes = await incomesQuery
                    .OrderByDescending(i => i.IssueDateTime)
                    .ToListAsync();

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId
                );
                ViewBag.Status = status;
                ViewBag.WarehouseId = warehouseId;
                ViewBag.AssignedWarehouseIds = assignedWarehouseIds;
                var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
                bool isMES = userRoles != null && userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
                bool isSales = userRoles != null && userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
                ViewBag.ChangePermit = !isMES && !isSales && tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes");
                ViewBag.CanCreateManualIncome = assignedWarehouseIds.Any(wid => WarehouseFlowService.CanCreateManualIncome(db, wid));
                return View(incomes);
            }
        }

        // Olish so'rovi: Incomes/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.UNIT))
                    .Include(i => i.WRHS_INCOME_DOCS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                // View barcha uchun ochiq; Edit/Delete/Receive faqat MRP uchun
                // Bir foydalanuvchi bir nechta omborga MRP bo'lishi mumkin,
                // shuning uchun bitta emas, barcha biriktirilgan omborlarni tekshiramiz.
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
                bool isMES = userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
                bool isSales = userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
                bool isMrpForWarehouse = assignedWarehouseIds.Contains(income.ReceivingWHID);
                var isTransferIncome = WarehouseFlowService.IsAutoLinkedTransferIncome(income);
                var receivePolicyError = WarehouseFlowService.ValidateIncomeReceive(db, income);
                var editPolicyError = WarehouseFlowService.ValidateIncomeEdit(db, income);

                ViewBag.CanMrpReceive = isMrpForWarehouse && income.RecieveStatus != true && receivePolicyError == null;
                ViewBag.IncomePolicyMessage = receivePolicyError;
                ViewBag.DlocEnabled = DlocFifoSchema.IsEnabled(db);
                ViewBag.HasDlocLocations = DlocFifoSchema.IsEnabled(db)
                    && DlocFifoDataAccess.WarehouseHasLocations(db, income.ReceivingWHID);
                ViewBag.DlocLocations = new SelectList(
                    DlocFifoDataAccess.GetLocations(db, income.ReceivingWHID),
                    "ID", "LocationCode");
                ViewBag.PutawayByLine = DlocFifoReporting.GetIncomePutawayByProductLine(db, income.ID);
                ViewBag.DlocLocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                    DlocFifoDataAccess.GetLocations(db, income.ReceivingWHID)
                        .Select(l => new { l.ID, l.LocationCode }).ToList());
                ViewBag.CanRepairTransferStock = isMrpForWarehouse && income.RecieveStatus == true && isTransferIncome
                    && (income.Description == null || !income.Description.Contains("| STOCK_REPAIRED"));
                ViewBag.CanEdit = !isMES && !isSales
                    && tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes")
                    && isMrpForWarehouse
                    && income.RecieveStatus != true
                    && editPolicyError == null;

                // Chiqim hujjatiga link (avtomatik yaratilgan kirimlar uchun)
                if (!string.IsNullOrEmpty(income.Description) && income.Description.Contains(WarehouseFlowService.AutoTransferIncomeMarker))
                {
                    var linkedExpense = await FindLinkedExpenseAsync(db, income, includeLines: false);
                    if (linkedExpense != null)
                    {
                        ViewBag.LinkedExpenseId = linkedExpense.ID;
                        ViewBag.LinkedExpenseDocNo = linkedExpense.DocNo;
                    }
                }

                return View(income);
            }
        }

        // Olish so'rovi: Incomes/yaratish
        public async Task<ActionResult> Create()
        {
            if (IsMesOrSalesRestrictedUser())
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Yaratish va tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }
            
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
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

                if (!WarehouseFlowService.CanCreateManualIncome(db, assignedWarehouseId.Value))
                {
                    TempData["Error"] = WarehouseFlowService.ValidateManualIncomeCreate(db, assignedWarehouseId.Value, null)
                        ?? "Ushbu omborga qo'lda kirim yaratish mumkin emas. Kutilayotgan kirimlarni ro'yxatdan tanlab qabul qiling.";
                    return RedirectToAction("Index");
                }

                await PopulateInvoiceSelectListAsync(db, requireInvoice: false);

                PopulateHeaderLookups(new[] { assignedWarehouseId.Value }, null, null);
                await PopulateLineLookupsAsync(db);
                PopulateCreateDlocViewBag(db, assignedWarehouseId.Value);
                return View();
            }
        }

        private void PopulateCreateDlocViewBag(DBTHSNEntities db, int receivingWhId)
        {
            var requireDloc = DlocFifoSchema.IsEnabled(db) && DlocFifoDataAccess.WarehouseHasLocations(db, receivingWhId);
            ViewBag.RequireDloc = requireDloc;
            ViewBag.DlocEnabled = DlocFifoSchema.IsEnabled(db);
            ViewBag.DefaultWarehouseId = receivingWhId;
            ViewBag.DlocLocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                DlocFifoDataAccess.GetLocations(db, receivingWhId)
                    .Select(l => new { l.ID, l.LocationCode, l.MinCapacity, l.MaxCapacity })
                    .ToList());
        }

        /// <summary>DLOC joyidagi qoldiq va sig'im (kirim formasi uchun).</summary>
        [HttpGet]
        public JsonResult GetDlocPutawayInfo(int locationId, decimal? incomingQty)
        {
            using (var db = new DBTHSNEntities())
            {
                if (!DlocFifoSchema.IsEnabled(db))
                    return Json(new { success = false, message = "DLOC moduli yoqilmagan" }, JsonRequestBehavior.AllowGet);

                var loc = DlocFifoDataAccess.GetLocation(db, locationId);
                if (loc == null)
                    return Json(new { success = false, message = "DLOC topilmadi" }, JsonRequestBehavior.AllowGet);

                var current = DlocFifoDataAccess.GetLocationCurrentQty(db, locationId);
                var max = loc.MaxCapacity;
                var available = max - current;
                var incoming = incomingQty ?? 0;
                var after = current + incoming;
                var ok = after <= max;

                return Json(new
                {
                    success = true,
                    locationCode = loc.LocationCode,
                    currentQty = current,
                    minCapacity = loc.MinCapacity,
                    maxCapacity = max,
                    availableQty = available,
                    afterQty = after,
                    isOk = ok,
                    message = ok
                        ? $"Joriy: {current:N2} / Max: {max:N2}. Kirimdan keyin: {after:N2}"
                        : $"Sig'im oshib ketadi! Joriy: {current:N2}, kirim: {incoming:N2}, jami: {after:N2}, Max: {max:N2}"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: Incomes/LoadInvoiceProducts
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> LoadInvoiceProducts(int invoiceId)
        {
            using (var db = new DBTHSNEntities())
            {
                var invoice = await db.P_INVOICES
                    .Include(i => i.P_ORDERS)
                    .FirstOrDefaultAsync(i => i.ID == invoiceId);

                Dictionary<int, double> vatByProduct = new Dictionary<int, double>();
                int? contractId = null;
                if (invoice != null)
                {
                    if (invoice.ContractID > 0)
                    {
                        contractId = invoice.ContractID;
                    }
                    else if (invoice.P_ORDERS?.ContractID > 0)
                    {
                        contractId = invoice.P_ORDERS.ContractID;
                    }
                }

                if (contractId.HasValue)
                {
                    vatByProduct = await PContractVatHelper.GetProductVatRatesByContractAsync(db, contractId.Value);
                }

                var invoiceProducts = await db.P_INVOICE_PRODUCTS
                    .Include(ip => ip.PRODUCT)
                    .Include(ip => ip.UNIT)
                    .Where(ip => ip.InvoiceID == invoiceId)
                    .Select(ip => new
                    {
                        ProductID = ip.ProductID,
                        ProductPNo = ip.PRODUCT.PNo,
                        ProductName = ip.PRODUCT.Name,
                        Amount = ip.Quantity,
                        UnitID = ip.UnitID,
                        PiecePrice = ip.Price,
                        TotalPrice = ip.TotalPrice
                    })
                    .ToListAsync();

                var data = invoiceProducts.Select(ip => new
                {
                    ip.ProductID,
                    ip.ProductPNo,
                    ip.ProductName,
                    ip.Amount,
                    ip.UnitID,
                    ip.PiecePrice,
                    ip.TotalPrice,
                    VATRate = vatByProduct.ContainsKey(ip.ProductID) ? vatByProduct[ip.ProductID] : 0d
                }).ToList();

                return Json(new { success = true, data });
            }
        }

        // POST so'rovi: Incomes/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ViewModels.IncomeCreateViewModel model, HttpPostedFileBase incomeFile)
        {
            if (IsMesOrSalesRestrictedUser())
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Yaratish va tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }

            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var dbPolicy = new DBTHSNEntities())
            {
                var policyError = WarehouseFlowService.ValidateManualIncomeCreate(dbPolicy, model.ReceivingWHID, model.InvoiceID);
                if (policyError != null)
                {
                    ModelState.AddModelError("", policyError);
                }
            }

            using (var dbPre = new DBTHSNEntities())
            {
                if (model.ReceivingWHID > 0 && DlocFifoSchema.IsEnabled(dbPre)
                    && DlocFifoDataAccess.WarehouseHasLocations(dbPre, model.ReceivingWHID))
                {
                    var lines = model.IncomeProducts ?? new List<ViewModels.IncomeProductViewModel>();
                    var pendingQtyByLocation = new Dictionary<int, decimal>();
                    foreach (var line in lines)
                    {
                        if (!line.LocationID.HasValue || line.LocationID.Value <= 0)
                        {
                            ModelState.AddModelError("", "Har bir mahsulot qatori uchun DLOC tanlanishi shart.");
                            continue;
                        }

                        try
                        {
                            DlocCapacityGuard.ValidatePutaway(dbPre, line.LocationID.Value, line.ProductID, (decimal)line.Amount);
                        }
                        catch (WarehouseDlocException wex)
                        {
                            ModelState.AddModelError("", wex.Message);
                            continue;
                        }

                        var locId = line.LocationID.Value;
                        if (!pendingQtyByLocation.ContainsKey(locId))
                            pendingQtyByLocation[locId] = 0;
                        pendingQtyByLocation[locId] += (decimal)line.Amount;

                        var loc = DlocFifoDataAccess.GetLocation(dbPre, locId);
                        if (loc != null)
                        {
                            var current = DlocFifoDataAccess.GetLocationCurrentQty(dbPre, locId) + pendingQtyByLocation[locId];
                            if (current > loc.MaxCapacity)
                                ModelState.AddModelError("",
                                    $"DLOC {loc.LocationCode}: sig'im oshib ketadi (Max {loc.MaxCapacity:N2}, jami kirimdan keyin {current:N2}).");
                        }
                    }
                }
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

                            if (model.ReceivingWHID != assignedWarehouseId.Value)
                            {
                                TempData["Error"] = "Siz faqat biriktirilgan omborga kirim qilishingiz mumkin.";
                                return RedirectToAction("Index");
                            }

                            var requireDloc = DlocFifoSchema.IsEnabled(db)
                                && DlocFifoDataAccess.WarehouseHasLocations(db, model.ReceivingWHID);

                            // Yaratish DocNo agar not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Yaratish income header
                            var income = new INCOME
                            {
                                DocNo = model.DocNo,
                                IssueDateTime = model.IssueDateTime,
                                ReceivingWHID = model.ReceivingWHID,
                                SenderWHID = model.SenderWHID,
                                InvoiceID = model.InvoiceID,
                                CurrencyID = model.CurrencyID,
                                Amount = 0,
                                TotalPrice = 0,
                                Description = model.Description,
                                RecieveStatus = model.InvoiceID.HasValue,
                                IsDeleted = false
                            };

                            db.INCOMES.Add(income);
                            await db.SaveChangesAsync();

                            // Qo'shish income products va yangilash stock
                            var conversionService = new UnitConversionService(db);
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.IncomeProducts != null && model.IncomeProducts.Any())
                            {
                                foreach (var product in model.IncomeProducts)
                                {
                                    var productEntity = await db.PRODUCTS
                                        .Include(p => p.PRODUCT_CATEGORIES)
                                        .FirstOrDefaultAsync(p => p.ID == product.ProductID);
                                    
                                    // Tekshirish agar product requires cutting
                                    bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                    
                                    // uchun requires_cutting products, use PIECES (UnitID = 1)
                                    // uchun other products, use user-selected unit yoki product's standart unit
                                    int unitId;
                                    if (requiresCutting)
                                    {
                                        unitId = 1; // Piece
                                    }
                                    else
                                    {
                                        unitId = product.UnitID ?? (productEntity?.UnitID ?? 1);
                                    }

                                    var vat = Math.Max(0, Math.Min(100, product.VATRate));
                                    var baseLine = product.Amount * (product.PiecePrice ?? 0);
                                    var lineTotal = baseLine * (1.0 + vat / 100.0);
                                    var mergedComment = MergeIncomeCommentWithVat(product.Comment, vat);

                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount, // In PIECES uchun cutting products, yoki user-selected unit uchun others
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = lineTotal,
                                        UnitID = unitId,
                                        Comment = mergedComment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);
                                    await db.SaveChangesAsync();

                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;

                                    // DLOC omborda kirim darhol joyga qabul qilinadi; oddiy omborda —
                                    // faqat invoys bilan (qabul qilingan) kirim qoldiqqa qo'shiladi.
                                    if (requireDloc)
                                    {
                                        if (!product.LocationID.HasValue || product.LocationID.Value <= 0)
                                            throw new WarehouseDlocException("DLOC_REQUIRED",
                                                $"Mahsulot {productEntity?.PNo ?? product.ProductID.ToString()}: DLOC tanlanishi shart.");

                                        StockLedger.ReceiveLine(db, income.ReceivingWHID, income.ID, incomeProduct.ID,
                                            product.ProductID, product.Amount, unitId, product.LocationID, income.IssueDateTime);
                                    }
                                    else if (income.RecieveStatus == true)
                                    {
                                        StockLedger.ReceiveLine(db, income.ReceivingWHID, income.ID, incomeProduct.ID,
                                            product.ProductID, product.Amount, unitId, null, income.IssueDateTime);
                                    }
                                }

                                if (requireDloc)
                                    income.RecieveStatus = true;

                                income.Amount = totalAmount;
                                income.TotalPrice = totalPrice;
                                await db.SaveChangesAsync();
                            }

                            // Upload income hujjat agar provided
                            if (incomeFile != null && incomeFile.ContentLength > 0)
                            {
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(incomeFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(incomeFile.ContentLength);
                                }

                                var incomeDoc = new WRHS_INCOME_DOCS
                                {
                                    IncomeID = income.ID,
                                    Doc = fileData
                                };

                                db.WRHS_INCOME_DOCS.Add(incomeDoc);
                                await db.SaveChangesAsync();
                            }

                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Create - {income.DocNo} kirim hujjati yaratildi");

                            return RedirectToAction("Index");
                        }
                        catch (WarehouseDlocException wex)
                        {
                            transaction.Rollback();
                            ModelState.AddModelError("", wex.Message);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            var errorMessage = $"Xatolik: {ex.Message}";
                            var innerEx = ex.InnerException;
                            while (innerEx != null)
                            {
                                errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                                innerEx = innerEx.InnerException;
                            }
                            ModelState.AddModelError("", errorMessage);
                        }
                    }
                }
            }

            // Xato bilan qaytishda forma to'liq qayta to'ldiriladi (SRM-B24: avval Units/Products yo'q edi)
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                await PopulateInvoiceSelectListAsync(db, requireInvoice: false, selectedInvoiceId: model.InvoiceID);
                PopulateHeaderLookups(assignedWarehouseId.HasValue ? new[] { assignedWarehouseId.Value } : null,
                    model.ReceivingWHID, model.CurrencyID);
                await PopulateLineLookupsAsync(db);

                if (assignedWarehouseId.HasValue)
                    PopulateCreateDlocViewBag(db, assignedWarehouseId.Value);
            }

            return View(model);
        }

        /// <summary>
        /// Keyingi kirim raqami: {yyyyMM}_{n} (masalan 202610_15). Tranzaksiya ichida, qulf bilan (SRM-B07):
        /// avval prefiks yilsiz ("10_") edi, satr bo'yicha saralanardi ("10_9" &gt; "10_10") va parallel
        /// so'rovlar bir xil raqam olardi.
        /// </summary>
        private static Task<string> GenerateDocNo(DBTHSNEntities db)
        {
            return DocumentNumberService.NextAsync(db, "INCOMES", "DocNo", DateTime.Now.ToString("yyyyMM") + "_");
        }

        // Olish so'rovi: Incomes/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            // MES va Sales xodimlari faqat ko'radi, Edit qila olmaydi
            var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
            bool isMES = userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
            bool isSales = userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
            
            if (isMES || isSales)
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }
            
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                var editorWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (editorWarehouseIds.Count == 0)
                {
                    TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                    return RedirectToAction("Index");
                }
                if (!editorWarehouseIds.Contains(income.ReceivingWHID))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                // Tekshirish agar already received (RecieveStatus == true)
                if (income.RecieveStatus == true)
                {
                    TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

                var editPolicyError = WarehouseFlowService.ValidateIncomeEdit(db, income);
                if (editPolicyError != null)
                {
                    TempData["Error"] = editPolicyError;
                    return RedirectToAction("Details", new { id });
                }

                var model = new ViewModels.IncomeEditViewModel
                {
                    ID = income.ID,
                    DocNo = income.DocNo,
                    IssueDateTime = income.IssueDateTime,
                    ReceivingWHID = income.ReceivingWHID,
                    SenderWHID = income.SenderWHID,
                    InvoiceID = income.InvoiceID,
                    CurrencyID = income.CurrencyID,
                    Amount = income.Amount,
                    TotalPrice = income.TotalPrice,
                    Description = income.Description,
                    IncomeProducts = income.INCOME_PRODUCTS.Select(ip => new ViewModels.IncomeProductViewModel
                    {
                        ID = ip.ID,
                        ProductID = ip.ProductID,
                        ProductPNo = ip.PRODUCT.PNo,
                        ProductName = ip.PRODUCT.Name,
                        Amount = ip.Amount,
                        UnitID = ip.UnitID,
                        PiecePrice = ip.PiecePrice,
                        TotalPrice = ip.TotalPrice,
                        Comment = ip.Comment,
                        VATRate = GetVatRateForIncomeLine(ip.TotalPrice, ip.Amount, ip.PiecePrice, ip.Comment)
                    }).ToList()
                };

                PopulateHeaderLookups(editorWarehouseIds, model.ReceivingWHID, model.CurrencyID);
                await PopulateLineLookupsAsync(db);
                PopulateEditDlocViewBag();

                return View(model);
            }
        }

        // POST so'rovi: Incomes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ViewModels.IncomeEditViewModel model, HttpPostedFileBase incomeFile)
        {
            if (IsMesOrSalesRestrictedUser())
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }

            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ModelState.Remove("InvoiceID");

            // Tahrirlash faqat qabul qilinmagan kirim uchun: DLOC joyi qabul qilishda (Receive) tanlanadi.
            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var income = await db.INCOMES.FindAsync(model.ID);
                            if (income == null || income.IsDeleted == true)
                            {
                                return HttpNotFound();
                            }

                            var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                            if (assignedWarehouseIds.Count == 0)
                            {
                                TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                                return RedirectToAction("Index");
                            }
                            // Eski va yangi qabul qiluvchi ombor ham foydalanuvchiga biriktirilgan bo'lishi shart
                            if (!assignedWarehouseIds.Contains(income.ReceivingWHID)
                                || !assignedWarehouseIds.Contains(model.ReceivingWHID))
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            if (income.RecieveStatus == true)
                            {
                                TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va tahrirlash mumkin emas.";
                                return RedirectToAction("Index");
                            }

                            var editPolicyError = WarehouseFlowService.ValidateIncomeEdit(db, income);
                            if (editPolicyError != null)
                            {
                                TempData["Error"] = editPolicyError;
                                return RedirectToAction("Details", new { id = income.ID });
                            }

                            // SRM-B02: qabul qilinmagan kirim qoldiqqa hali qo'shilmagan — tahrirda qoldiqqa
                            // TEGILMAYDI (avval eski qatorlar ayirilib, yangilari qo'shilardi, keyin Receive
                            // yana qo'shardi → ikki barobar). Qoldiq faqat Receive'da kiradi.
                            var oldProducts = await db.INCOME_PRODUCTS
                                .Where(ip => ip.IncomeID == income.ID)
                                .ToListAsync();

                            // Yangilash income header
                            income.DocNo = model.DocNo;
                            income.IssueDateTime = model.IssueDateTime;
                            income.ReceivingWHID = model.ReceivingWHID;
                            income.SenderWHID = model.SenderWHID;
                            income.CurrencyID = model.CurrencyID;
                            income.Description = model.Description;

                            // O'chirish existing income products
                            foreach (var existing in oldProducts)
                            {
                                db.INCOME_PRODUCTS.Remove(existing);
                            }

                            // Qo'shish updated income products va yangilash stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.IncomeProducts != null && model.IncomeProducts.Any())
                            {
                                foreach (var product in model.IncomeProducts)
                                {
                                    var productEntity = await db.PRODUCTS.FindAsync(product.ProductID);
                                    var unitId = product.UnitID ?? (productEntity != null ? productEntity.UnitID : 1);
                                    var vat = Math.Max(0, Math.Min(100, product.VATRate));
                                    var baseLine = product.Amount * (product.PiecePrice ?? 0);
                                    var lineTotal = baseLine * (1.0 + vat / 100.0);
                                    var mergedComment = MergeIncomeCommentWithVat(product.Comment, vat);

                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = lineTotal,
                                        UnitID = unitId,
                                        Comment = mergedComment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);

                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;
                                }
                            }

                            income.Amount = totalAmount;
                            income.TotalPrice = totalPrice;

                            // Handle fayl upload
                            if (incomeFile != null && incomeFile.ContentLength > 0)
                            {
                                // O'chirish existing docs
                                var existingDocs = db.WRHS_INCOME_DOCS.Where(id => id.IncomeID == income.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_INCOME_DOCS.Remove(doc);
                                }

                                // Qo'shish new doc
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(incomeFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(incomeFile.ContentLength);
                                }

                                var incomeDoc = new WRHS_INCOME_DOCS
                                {
                                    IncomeID = income.ID,
                                    Doc = fileData
                                };

                                db.WRHS_INCOME_DOCS.Add(incomeDoc);
                            }

                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Edit - {income.DocNo} kirim hujjati tahrirlandi");

                            return RedirectToAction("Index");
                        }
                        catch (WarehouseDlocException wex)
                        {
                            transaction.Rollback();
                            ModelState.AddModelError("", wex.Message);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            var errorMessage = $"Xatolik: {ex.Message}";
                            var innerEx = ex.InnerException;
                            while (innerEx != null)
                            {
                                errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                                innerEx = innerEx.InnerException;
                            }
                            ModelState.AddModelError("", errorMessage);
                        }
                    }
                }
            }

            // Xato bilan qaytishda forma to'liq qayta to'ldiriladi (SRM-B24: avval Units/Products yo'qligi
            // sababli Edit.cshtml InvalidOperationException bilan yiqilardi)
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                PopulateHeaderLookups(assignedWarehouseIds, model.ReceivingWHID, model.CurrencyID);
                await PopulateLineLookupsAsync(db);
                PopulateEditDlocViewBag();
            }

            return View(model);
        }

        // POST so'rovi: Incomes/Receive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Receive(int id, int? locationId, FormCollection form)
        {
            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .Include(i => i.INCOME_PRODUCTS)
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income == null)
                        {
                            return HttpNotFound();
                        }

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        if (income.RecieveStatus == true)
                        {
                            TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan.";
                            return RedirectToAction("Details", new { id });
                        }

                        var receivePolicyError = WarehouseFlowService.ValidateIncomeReceive(db, income);
                        if (receivePolicyError != null)
                        {
                            TempData["Error"] = receivePolicyError;
                            return RedirectToAction("Details", new { id });
                        }

                        var requireDloc = StockLedger.IsDlocWarehouse(db, income.ReceivingWHID);

                        if (income.INCOME_PRODUCTS == null || !income.INCOME_PRODUCTS.Any())
                        {
                            TempData["Error"] = "Kirim hujjatida mahsulotlar yo'q.";
                            return RedirectToAction("Details", new { id });
                        }

                        // SRM-B08: statusni atomik "band qilamiz" — ikki marta bosish yoki ikki foydalanuvchi
                        // bir vaqtda qabul qilsa, faqat bittasi qoldiqni oshiradi.
                        var claimed = await db.Database.ExecuteSqlCommandAsync(
                            "UPDATE dbo.INCOMES SET RecieveStatus = 1 WHERE ID = @p0 AND ISNULL(RecieveStatus, 0) = 0", income.ID);
                        if (claimed == 0)
                        {
                            transaction.Rollback();
                            TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan.";
                            return RedirectToAction("Details", new { id });
                        }

                        foreach (var product in income.INCOME_PRODUCTS)
                        {
                            var unitId = product.UnitID > 0 ? product.UnitID : 1;
                            int? lineLocationId = null;

                            if (requireDloc)
                            {
                                var key = "locationId_" + product.ID;
                                if (form != null && int.TryParse(form[key], out var parsed) && parsed > 0)
                                    lineLocationId = parsed;
                                else if (locationId.HasValue && locationId.Value > 0)
                                    lineLocationId = locationId;

                                if (!lineLocationId.HasValue)
                                {
                                    transaction.Rollback();
                                    TempData["Error"] = "Har bir mahsulot uchun DLOC tanlanishi shart.";
                                    return RedirectToAction("Details", new { id });
                                }
                            }

                            StockLedger.ReceiveLine(db, income.ReceivingWHID, income.ID, product.ID, product.ProductID,
                                product.Amount, unitId, lineLocationId, income.IssueDateTime);
                        }

                        income.RecieveStatus = true;

                        // Bog'langan chiqimni ham "yuborilgan" deb belgilaymiz, shunda chiqim qilgan
                        // ombor ham qabul qilingan holatni ko'rsatadi (status ikkala tomonda mos bo'ladi).
                        await MarkLinkedExpenseSentAsync(db, income);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        TempData["Success"] = $"Kirim «{income.DocNo}» qabul qilindi. Ombor qoldig'i yangilandi.";

                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"Receive - {income.DocNo} kirim hujjati qabul qilindi");

                        return RedirectToAction("Details", new { id });
                    }
                    catch (WarehouseDlocException wex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = wex.Message;
                        return RedirectToAction("Details", new { id });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Details", new { id });
                    }
                }
            }
        }

        // POST so'rovi: Incomes/RepairTransferStock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RepairTransferStock(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .Include(i => i.INCOME_PRODUCTS)
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income == null)
                        {
                            return HttpNotFound();
                        }

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        var isTransferIncome = WarehouseFlowService.IsAutoLinkedTransferIncome(income);
                        if (!isTransferIncome || income.RecieveStatus != true)
                        {
                            TempData["Error"] = "Bu hujjat uchun repair qo'llab bo'lmaydi.";
                            return RedirectToAction("Details", new { id });
                        }

                        if (!string.IsNullOrEmpty(income.Description) && income.Description.Contains("| STOCK_REPAIRED"))
                        {
                            TempData["Error"] = "Repair allaqachon bajarilgan.";
                            return RedirectToAction("Details", new { id });
                        }

                        if (income.INCOME_PRODUCTS != null)
                        {
                            foreach (var product in income.INCOME_PRODUCTS)
                            {
                                var unitId = product.UnitID > 0 ? product.UnitID : 1;
                                StockLedger.Increase(db, income.ReceivingWHID, product.ProductID, product.Amount, unitId);
                            }
                        }

                        income.Description = (income.Description ?? string.Empty) + " | STOCK_REPAIRED";

                        // Bog'langan chiqimni ham "yuborilgan" deb belgilaymiz — oldin qabul qilingan,
                        // ammo chiqim tomonida "kutilmoqda" bo'lib qolgan transferlarni ham tuzatadi.
                        await MarkLinkedExpenseSentAsync(db, income);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        TempData["Success"] = "Transfer kirimi uchun qoldiq qayta tiklandi.";
                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"RepairTransferStock - {income.DocNo} uchun qoldiq qayta tiklandi");
                        return RedirectToAction("Details", new { id });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Details", new { id });
                    }
                }
            }
        }

        // POST so'rovi: Incomes/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Reject(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .Include(i => i.INCOME_PRODUCTS)
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income == null)
                        {
                            return HttpNotFound();
                        }

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        // SRM-B08: qabul bilan bir vaqtda rad etilmasligi uchun status qulf ostida qayta o'qiladi
                        var receivedNow = await db.Database.SqlQuery<int>(
                            "SELECT CAST(ISNULL(RecieveStatus, 0) AS INT) FROM dbo.INCOMES WITH (UPDLOCK, HOLDLOCK) WHERE ID = @p0",
                            income.ID).FirstOrDefaultAsync();
                        if (income.RecieveStatus == true || receivedNow == 1)
                        {
                            TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va rad etib bo'lmaydi.";
                            return RedirectToAction("Details", new { id });
                        }

                        var expense = await FindLinkedExpenseAsync(db, income, includeLines: true);
                        if (expense == null)
                        {
                            TempData["Error"] = "Bog'langan chiqim topilmadi. Rad etish mumkin emas.";
                            return RedirectToAction("Details", new { id });
                        }

                        // SRM-B03: kirim qabul qilinmagan — qabul qiluvchi omborga hech narsa qo'shilmagan,
                        // shuning uchun faqat yuboruvchi ombor chiqimi (DLOC lot ajratmalari bilan) qaytariladi.
                        foreach (var product in expense.EXPENSE_PRODUCTS)
                        {
                            StockLedger.ReverseIssueLine(db, expense.SenderWHID, product.ProductID, product.Amount,
                                product.UnitID > 0 ? product.UnitID : 1, product.ID);
                        }

                        await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.WRHS_INCOME_DOCS WHERE IncomeID = @p0", income.ID);
                        await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.WRHS_EXPENSE_DOCS WHERE ExpenseID = @p0", expense.ID);

                        if (income.INCOME_PRODUCTS != null && income.INCOME_PRODUCTS.Any())
                        {
                            db.INCOME_PRODUCTS.RemoveRange(income.INCOME_PRODUCTS.ToList());
                        }

                        if (expense.EXPENSE_PRODUCTS != null && expense.EXPENSE_PRODUCTS.Any())
                        {
                            db.EXPENSE_PRODUCTS.RemoveRange(expense.EXPENSE_PRODUCTS.ToList());
                        }

                        db.INCOMES.Remove(income);
                        db.EXPENSES.Remove(expense);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"Reject - {income.DocNo} kirim hujjati rad etildi va bog'langan chiqim o'chirildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Details", new { id });
                    }
                }
            }
        }

        // Olish so'rovi: Incomes/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(income.ReceivingWHID))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                return View(income);
            }
        }

        // POST so'rovi: Incomes/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income != null)
                        {
                            var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                            if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(income.ReceivingWHID))
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            // SRM-B03: qabul qiluvchi ombor qoldig'i FAQAT kirim qabul qilingan bo'lsa qaytariladi
                            // (avval qabul qilinmagan kirim o'chirilsa ham ayirilardi va 0 ga "clamp" yashirardi).
                            // DLOC lotlari ham bekor qilinadi; lotdan chiqim qilingan bo'lsa — o'chirish taqiqlanadi.
                            var receivedNow = await db.Database.SqlQuery<int>(
                                "SELECT CAST(ISNULL(RecieveStatus, 0) AS INT) FROM dbo.INCOMES WITH (UPDLOCK, HOLDLOCK) WHERE ID = @p0",
                                income.ID).FirstOrDefaultAsync();
                            if (receivedNow == 1)
                            {
                                StockLedger.ReverseIncomeReceipt(db, income.ID, income.ReceivingWHID);
                            }

                            // Agar bu kirim chiqimdan bog'langan bo'lsa, bog'langan chiqimni ham soft-delete qilamiz
                            // va yuboruvchi ombor qoldig'ini (DLOC lot ajratmalari bilan) qaytaramiz.
                            var linkedExpense = await FindLinkedExpenseAsync(db, income, includeLines: true);
                            if (linkedExpense != null)
                            {
                                foreach (var ep in linkedExpense.EXPENSE_PRODUCTS)
                                {
                                    StockLedger.ReverseIssueLine(db, linkedExpense.SenderWHID, ep.ProductID, ep.Amount,
                                        ep.UnitID > 0 ? ep.UnitID : 1, ep.ID);
                                }

                                // Linked expense docs: fizik o'chirish (IsDeleted maydoni yo'q)
                                await db.Database.ExecuteSqlCommandAsync(
                                    "DELETE FROM dbo.WRHS_EXPENSE_DOCS WHERE ExpenseID = @p0", linkedExpense.ID);

                                linkedExpense.IsDeleted = true;
                            }

                            // Income docs: fizik o'chirish (IsDeleted maydoni yo'q)
                            await db.Database.ExecuteSqlCommandAsync(
                                "DELETE FROM dbo.WRHS_INCOME_DOCS WHERE IncomeID = @p0", income.ID);

                            income.IsDeleted = true;
                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Delete - {income.DocNo} kirim hujjati o'chirildi");
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

        // Olish so'rovi: Incomes/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.WRHS_INCOME_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Income_{doc.IncomeID}.pdf");
            }
        }

        private static string MergeIncomeCommentWithVat(string commentFromForm, double vatRate)
        {
            var userPart = string.IsNullOrWhiteSpace(commentFromForm)
                ? ""
                : IncomeLineVatInComment.Replace(commentFromForm, "").Trim().Trim('|').Trim();
            var pct = vatRate.ToString("0.##", CultureInfo.InvariantCulture);
            var qqs = "QQS %: " + pct + "%";
            if (string.IsNullOrEmpty(userPart))
            {
                return qqs;
            }

            return userPart + " | " + qqs;
        }

        private static double? TryParseVatFromComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return null;
            }

            var m = Regex.Match(comment, @"QQS\s*%:\s*([\d.,]+)\s*%", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            var s = m.Groups[1].Value.Replace(",", ".");
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            return null;
        }

        /// <summary>
        /// Tahrirlash: izohdan QQS o‘qiladi; yo‘q bo‘lsa, jami va asosiy summadan taxmin qilinadi (eski yozuvlar).
        /// </summary>
        private static double GetVatRateForIncomeLine(double? totalPrice, double amount, double? piecePrice, string comment)
        {
            var parsed = TryParseVatFromComment(comment);
            if (parsed.HasValue)
            {
                return Math.Max(0, Math.Min(100, parsed.Value));
            }

            var b = amount * (piecePrice ?? 0);
            if (b <= 0.0000001)
            {
                return 0;
            }

            if (!totalPrice.HasValue || Math.Abs(totalPrice.Value - b) < 0.02)
            {
                return 0;
            }

            var implied = (totalPrice.Value / b - 1.0) * 100.0;
            if (implied >= -0.5 && implied <= 100.5)
            {
                return Math.Max(0, Math.Min(100, Math.Round(implied, 4)));
            }

            return 0;
        }

        /// <summary>
        /// Kirim qabul qilinganda unga bog'langan chiqimni "yuborilgan" (SendStatus = true) deb
        /// belgilaydi. Shunday qilib chiqim qilgan ombor ham qabul qilingan holatni ko'rsatadi.
        /// </summary>
        private async Task MarkLinkedExpenseSentAsync(DBTHSNEntities db, INCOME income)
        {
            if (income == null)
            {
                return;
            }

            // Eng ishonchli yo'l: izohdagi EXPENSE_ID/EXPENS ID markeridan chiqimni to'g'ridan-to'g'ri
            // topib belgilash. DocNo + ombor (WHID) bo'yicha qayta qidirish noto'g'ri edi: SenderWHID/
            // ReceiverWhID null bo'lsa, EF "= NULL" generatsiya qiladi va hech qachon mos kelmaydi —
            // shu sabab chiqimda status doim "kutilmoqda" bo'lib qolardi.
            var expenseId = ExtractLinkedExpenseId(income.Description);
            if (expenseId.HasValue)
            {
                var expById = await db.EXPENSES
                    .FirstOrDefaultAsync(e => e.ID == expenseId.Value && e.IsDeleted != true);
                if (expById != null)
                {
                    if (expById.SendStatus != true)
                    {
                        expById.SendStatus = true;
                    }
                    return;
                }
            }

            // Zaxira: faqat eski matnli format uchun DocNo bo'yicha (ID markeri yo'q bo'lsa).
            var linkedExpense = await FindLinkedExpenseAsync(db, income, includeLines: false);
            if (linkedExpense != null && linkedExpense.SendStatus != true)
            {
                linkedExpense.SendStatus = true;
            }
        }

        /// <summary>
        /// Kirimga bog'langan chiqim: avval izohdagi ID markeri (EXPENSE_ID:n / EXPENS ID: n) bo'yicha aniq,
        /// bo'lmasa eski "Chiqimdan avtomatik bog'langan: DocNo" matni bo'yicha — faqat yagona mos chiqim bo'lsa
        /// (bir xil DocNo'li bir nechta chiqim bo'lsa, noto'g'ri hujjatni qaytarmaslik uchun null).
        /// </summary>
        private async Task<EXPENS> FindLinkedExpenseAsync(DBTHSNEntities db, INCOME income, bool includeLines)
        {
            if (income == null || string.IsNullOrWhiteSpace(income.Description))
                return null;

            IQueryable<EXPENS> query = db.EXPENSES;
            if (includeLines)
                query = query.Include(e => e.EXPENSE_PRODUCTS);

            var expenseId = ExtractLinkedExpenseId(income.Description);
            if (expenseId.HasValue)
                return await query.FirstOrDefaultAsync(e => e.ID == expenseId.Value && e.IsDeleted != true);

            var expenseDocNo = ExtractLinkedExpenseDocNoFromText(income.Description);
            if (string.IsNullOrWhiteSpace(expenseDocNo))
                return null;

            var receivingWhId = income.ReceivingWHID;
            var candidates = await query
                .Where(e => e.DocNo == expenseDocNo && e.ReceiverWhID == receivingWhId && e.IsDeleted != true)
                .Take(2)
                .ToListAsync();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Izohdan bog'langan chiqim ID'sini ajratadi (EXPENSE_ID:123 yoki eski "EXPENS ID: 123").</summary>
        private int? ExtractLinkedExpenseId(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var m = Regex.Match(description, @"EXPENSE_ID\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var id1))
            {
                return id1;
            }

            var m2 = Regex.Match(description, @"EXPENS\s*ID\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (m2.Success && int.TryParse(m2.Groups[1].Value, out var id2))
            {
                return id2;
            }

            return null;
        }

        /// <summary>Eski format: "Chiqimdan avtomatik bog'langan: {DocNo} - ..." matnidan chiqim raqami.</summary>
        private static string ExtractLinkedExpenseDocNoFromText(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            const string marker = "Chiqimdan avtomatik bog'langan: ";
            var index = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var start = index + marker.Length;
                var rest = description.Substring(start);
                var end = rest.IndexOf(" - ", StringComparison.Ordinal);
                if (end < 0) end = rest.Length;
                var docNo = rest.Substring(0, end).Trim();
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    return docNo;
                }
            }

            return null;
        }

        private bool IsMesOrSalesRestrictedUser()
        {
            var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
            if (userRoles == null)
            {
                return false;
            }

            return userRoles.Any(r => r.Contains("MES") || r.Contains("Production"))
                || userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
        }

        private async Task PopulateInvoiceSelectListAsync(DBTHSNEntities db, bool requireInvoice, int? selectedInvoiceId = null)
        {
            var invoices = await db.P_INVOICES
                .Include(i => i.SUPPLIER)
                .Where(i => i.IsDeleted != true)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            var invoiceSelectItems = invoices.Select(i => new SelectListItem
            {
                Value = i.ID.ToString(),
                Text = $"{i.InvoiceNo} - {i.SUPPLIER.Name} ({i.InvoiceDate:dd.MM.yyyy})",
                Selected = selectedInvoiceId.HasValue && selectedInvoiceId.Value == i.ID
            }).ToList();

            if (!requireInvoice)
            {
                invoiceSelectItems.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "---naqdga olingan invoys yo'q---"
                });
            }

            ViewBag.Invoices = new SelectList(
                invoiceSelectItems,
                "Value",
                "Text",
                selectedInvoiceId);
        }

        /// <summary>
        /// Kirim formasidagi sarlavha spravochniklari (keshdan): ombor (faqat ruxsat etilganlari) va valyuta.
        /// </summary>
        private void PopulateHeaderLookups(IEnumerable<int> allowedWarehouseIds, int? selectedWarehouseId, int? selectedCurrencyId)
        {
            var warehouses = ReferenceDataCache.Get(ReferenceDataCache.Warehouses).AsEnumerable();
            if (allowedWarehouseIds != null)
            {
                var allowed = new HashSet<int>(allowedWarehouseIds);
                warehouses = warehouses.Where(w => allowed.Contains(w.ID));
            }

            ViewBag.Warehouses = new SelectList(warehouses.ToList(), "ID", "Name", selectedWarehouseId);
            ViewBag.Currencies = ReferenceDataCache.SelectList(ReferenceDataCache.Currencies, selectedCurrencyId);
        }

        /// <summary>
        /// Qator spravochniklari: mahsulotlar (faqat ID/PNo/Name proyeksiyasi) va birliklar (keshdan).
        /// GET'da ham, har xato bilan qaytishda ham chaqiriladi (SRM-B24).
        /// </summary>
        private async Task PopulateLineLookupsAsync(DBTHSNEntities db)
        {
            var products = await db.PRODUCTS
                .AsNoTracking()
                .Where(p => p.IsDeleted != true)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToListAsync();

            ViewBag.Products = new SelectList(products.Select(p => new SelectListItem
            {
                Value = p.ID.ToString(),
                Text = $"{p.PNo} - {p.Name ?? ""}"
            }).ToList(), "Value", "Text");
            ViewBag.ProductsData = products.Select(p => new ViewModels.ProductLookupItem
            {
                Value = p.ID.ToString(),
                PNo = p.PNo,
                Name = p.Name ?? ""
            }).ToList();

            ViewBag.Units = new SelectList(
                ReferenceDataCache.Get(ReferenceDataCache.Units)
                    .Select(u => new { u.ID, ShortName = u.Extra ?? u.Name })
                    .ToList(),
                "ID", "ShortName");
        }

        /// <summary>
        /// Tahrirlashda DLOC ustuni ko'rsatilmaydi: kirim qabul qilinmagan, joy Receive'da tanlanadi
        /// (SRM-B02/B24: avval Edit DLOC omborda ViewBag.DlocLocations yo'qligi sababli yiqilardi).
        /// </summary>
        private void PopulateEditDlocViewBag()
        {
            ViewBag.RequireDloc = false;
            ViewBag.DlocEnabled = false;
            ViewBag.DlocLocationsJson = "[]";
        }

        /// <summary>Foydalanuvchiga biriktirilgan birinchi ombor; biriktirilmagan bo'lsa null (SRM-B16).</summary>
        private async Task<int?> GetAssignedWarehouseIdAsync(DBTHSNEntities db)
        {
            var ids = await GetAssignedWarehouseIdsAsync(db);
            return ids.Count == 0 ? (int?)null : ids[0];
        }

        private async Task<List<int>> GetAssignedWarehouseIdsAsync(DBTHSNEntities db)
        {
            var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
            return await db.WAREHOUSES
                .Where(w => w.IsDeleted != true && w.MRP == currentUserId)
                .Select(w => w.ID)
                .ToListAsync();
        }
    }

}
