using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.POrder;
using tahsinERP.Filters;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;

namespace tahsinERP.Controllers
{
    public class POrdersController : BaseController
    {
        private const string Module = "POrders";

        // Olish so'rovi: POrders — sahifa faqat filtr va jadval qobig'i; qatorlar IndexData orqali (SRM-P03)
        public ActionResult Index(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
            ViewBag.SupplierID = supplierID;
            ViewBag.ContractID = contractID;
            ViewBag.ChangePermit = CanChange(Module);
            return View();
        }

        /// <summary>Buyurtmalar ro'yxati qatori (proyeksiya).</summary>
        public sealed class POrderIndexRow
        {
            public int ID { get; set; }
            public string OrderNo { get; set; }
            public byte PurchaseType { get; set; }
            public DateTime IssuedDate { get; set; }
            public double? Amount { get; set; }
            public string Currency { get; set; }
        }

        private static IQueryable<POrderIndexRow> BuildIndexQuery(DBTHSNEntities db, string purchaseTypeFilter, int? supplierID, int? contractID)
        {
            var query = db.P_ORDERS.AsNoTracking().Where(o => o.IsDeleted != true);

            if (supplierID.HasValue && supplierID.Value > 0)
            {
                query = query.Where(o => o.SupplierID == supplierID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(o => o.ContractID == contractID.Value);
            }

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                query = query.Where(o => o.PurchaseType == purchaseType);
            }

            return query.Select(o => new POrderIndexRow
            {
                ID = o.ID,
                OrderNo = o.OrderNo,
                PurchaseType = o.PurchaseType,
                IssuedDate = o.IssuedDate,
                Amount = o.Amount,
                Currency = o.CURRENCy.Shortname
            });
        }

        private static IQueryable<POrderIndexRow> SearchIndex(IQueryable<POrderIndexRow> query, string s)
        {
            return query.Where(r => r.OrderNo.Contains(s) || r.Currency.Contains(s));
        }

        private static IOrderedQueryable<POrderIndexRow> OrderIndex(IQueryable<POrderIndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.OrderNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.Amount, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.Currency, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.IssuedDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.IssuedDate).ThenBy(r => r.ID);
            }
        }

        // Olish so'rovi: POrders/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 2);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(
                    BuildIndexQuery(db, purchaseTypeFilter, supplierID, contractID), page, SearchIndex, OrderIndex);

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID,
                        orderNo = r.OrderNo,
                        indirect = r.PurchaseType == ProcurementLookups.IndirectPurchase,
                        date = r.IssuedDate.ToString("dd.MM.yyyy"),
                        amount = (r.Amount ?? 0).ToString("N2"),
                        currency = r.Currency ?? ""
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: POrders/ExportExcel — filtr va qidiruv bo'yicha barcha yozuvlar
        [HttpGet]
        public async Task<ActionResult> ExportExcel(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null, string q = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, purchaseTypeFilter, supplierID, contractID);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = SearchIndex(query, q.Trim());
                }

                var rows = await OrderIndex(query, 2, true).ToListAsync();
                var bytes = ExcelExport.Create("Xarid buyurtmalari", rows,
                    ExcelExport.Col<POrderIndexRow>("Buyurtma raqami", r => r.OrderNo),
                    ExcelExport.Col<POrderIndexRow>("Turi", r => r.PurchaseType == ProcurementLookups.IndirectPurchase ? "Indirect" : "Direct"),
                    ExcelExport.Col<POrderIndexRow>("Sana", r => r.IssuedDate, "dd.MM.yyyy"),
                    ExcelExport.Col<POrderIndexRow>("Summa (QQS bilan)", r => r.Amount ?? 0, "#,##0.00"),
                    ExcelExport.Col<POrderIndexRow>("Valyuta", r => r.Currency));

                return File(bytes, ExcelExport.ContentType, $"Xarid_buyurtmalari_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        // Olish so'rovi: POrders/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Details"))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
                    .AsNoTracking()
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Include(o => o.P_CONTRACTS)
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(o => o.ID == id && o.IsDeleted != true);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Stavka saqlangan net/brutto summadan olinadi; shartnoma stavkasi faqat qatorlar bo'lmasa kerak (SRM-P10)
                if (!order.P_ORDER_PRODUCTS.Any())
                {
                    ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, order.ContractID);
                }
                return View(order);
            }
        }

        // Olish so'rovi: POrders/yaratish
        public async Task<ActionResult> Create(int? requisitionId = null)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                // Buyurtma talabnomadan yaratilayotganini tekshirish (bilvosita xarid)
                P_REQUISITIONS requisition = null;
                if (requisitionId.HasValue)
                {
                    requisition = await db.P_REQUISITIONS
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.ID == requisitionId.Value && r.IsDeleted != true);

                    if (requisition == null)
                    {
                        TempData["ErrorMessage"] = "Talabnoma topilmadi.";
                        return RedirectToAction("Index", "PRequisitions");
                    }

                    if (requisition.Status != "Approved")
                    {
                        TempData["ErrorMessage"] = "Faqat tasdiqlangan talabnomani xarid buyurtmasiga aylantirish mumkin.";
                        return RedirectToAction("Details", "PRequisitions", new { id = requisitionId });
                    }
                }

                var model = new POrderCreateViewModel { RequisitionID = requisition?.ID };
                await PopulateCreateLookupsAsync(db, model, requisition);
                return View(model);
            }
        }

        // POST so'rovi: POrders/LoadRequisitionProducts - talabnomadagi mahsulotlarni yuklash
        [HttpPost]
        [ValidateAntiForgeryHeader]
        [Permission(Module)]
        public async Task<JsonResult> LoadRequisitionProducts(int requisitionId)
        {
            using (var db = new DBTHSNEntities())
            {
                try
                {
                    bool exists = await db.P_REQUISITIONS.AnyAsync(r => r.ID == requisitionId && r.IsDeleted != true);
                    if (!exists)
                    {
                        return Json(new { success = false, message = "Talabnoma topilmadi." });
                    }

                    var rows = await db.P_REQUISITION_PRODUCTS
                        .Where(rp => rp.RequisitionID == requisitionId)
                        .Select(rp => new
                        {
                            rp.ProductID,
                            ProductPNo = rp.PRODUCT.PNo,
                            ProductName = rp.PRODUCT.Name,
                            rp.Quantity,
                            rp.UnitID,
                            rp.EstimatedPrice,
                            rp.Priority,
                            rp.RequiredDate
                        })
                        .ToListAsync();

                    var products = rows.Select(rp => new
                    {
                        rp.ProductID,
                        rp.ProductPNo,
                        rp.ProductName,
                        rp.Quantity,
                        rp.UnitID,
                        rp.EstimatedPrice,
                        rp.Priority,
                        RequiredDate = rp.RequiredDate.HasValue ? rp.RequiredDate.Value.ToString("yyyy-MM-dd") : null
                    }).ToList();

                    return Json(new { success = true, data = products });
                }
                catch (Exception ex)
                {
                    return JsonError(ex);
                }
            }
        }

        // POST so'rovi: POrders/GenerateFromSalesOrders - S_ORDERS asosida BOM yechimini hisoblash
        [HttpPost]
        [ValidateAntiForgeryHeader]
        [Permission(Module)]
        public async Task<JsonResult> GenerateFromSalesOrders(int[] salesOrderIds, DateTime? issuedDate = null)
        {
            if (salesOrderIds == null || salesOrderIds.Length == 0)
            {
                return Json(new { success = false, message = "Hech qanday sotuv buyurtmasi tanlanmagan." });
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    // Sotuv buyurtmalari mavjudligi va ularda mahsulot borligini tekshirish
                    var orderStats = await db.S_ORDERS
                        .Where(o => salesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                        .Select(o => new { o.ID, HasProducts = o.S_ORDER_PRODUCTS.Any() })
                        .ToListAsync();

                    if (!orderStats.Any())
                    {
                        return Json(new { success = false, message = "Tanlangan sotuv buyurtmalari topilmadi." });
                    }

                    if (!orderStats.Any(o => o.HasProducts))
                    {
                        return Json(new { success = false, message = "Tanlangan sotuv buyurtmalarida mahsulotlar topilmadi." });
                    }

                    var generationService = new POrderGenerationService(db);
                    // Buyurtma sanasi berilsa — o'sha oyga tegishli BOM versiyasidan ehtiyojlar hisoblanadi.
                    var requirements = await generationService.GenerateRequirementsFromSalesOrdersAsync(salesOrderIds, issuedDate);

                    if (requirements == null || !requirements.Any())
                    {
                        return Json(new { success = false, message = "BOM asosida xom ashyo ehtiyojlari topilmadi. Mahsulotlar uchun faol BOM mavjud emas yoki BOM'da xom ashyo yo'q." });
                    }

                    var result = requirements.Select(cr => new
                    {
                        ComponentID = cr.ComponentID,
                        ComponentPNo = cr.ComponentPNo,
                        ComponentName = cr.ComponentName,
                        UnitID = cr.UnitID,
                        RequiredQty = (double)cr.RequiredQty,
                        EstimatedPrice = cr.EstimatedPrice.HasValue ? (double?)cr.EstimatedPrice.Value : null,
                        EstimatedAmount = cr.EstimatedAmount.HasValue ? (double?)cr.EstimatedAmount.Value : null,
                        MOQ = (double?)null,
                        CalculationExplanation = cr.CalculationExplanation ?? "",
                        CalculationSummary = cr.CalculationSummary ?? ""
                    }).ToList();

                    return Json(new { success = true, data = result });
                }
                catch (Exception ex)
                {
                    return JsonError(ex, "BOM asosida ehtiyojlarni hisoblashda xatolik.");
                }
            }
        }

        // POST so'rovi: POrders/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(POrderCreateViewModel model)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            // OrderNo berilmagan bo'lsa — qulf bilan avtomatik (SRM-B14)
                            if (string.IsNullOrWhiteSpace(model.OrderNo))
                            {
                                model.OrderNo = await GenerateOrderNoAsync(db);
                            }
                            else
                            {
                                model.OrderNo = model.OrderNo.Trim();
                                if (await ActiveOrderNoExistsAsync(db, model.OrderNo, excludeOrderId: null))
                                {
                                    ModelState.AddModelError("OrderNo", "Bu buyurtma raqami allaqachon mavjud (faol buyurtma).");
                                }
                                else
                                {
                                    await ReleaseOrderNoFromSoftDeletedRowsAsync(db, model.OrderNo, excludeOrderId: null);
                                }
                            }

                            if (!ModelState.IsValid)
                            {
                                transaction.Rollback();
                            }
                            else
                            {
                                var companyId = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db);

                                // Xarid turi: talabnoma asosida bo'lsa bilvosita
                                byte purchaseType = ProcurementLookups.DirectPurchase;
                                P_REQUISITIONS requisition = null;
                                if (model.RequisitionID.HasValue)
                                {
                                    requisition = await db.P_REQUISITIONS.FindAsync(model.RequisitionID.Value);
                                    if (requisition != null)
                                    {
                                        purchaseType = ProcurementLookups.IndirectPurchase;
                                    }
                                }

                                var supplierId = model.SupplierID;
                                if (purchaseType == ProcurementLookups.DirectPurchase || !supplierId.HasValue || supplierId.Value <= 0)
                                {
                                    supplierId = null;
                                }

                                int? contractId = model.ContractID;
                                if (purchaseType == ProcurementLookups.DirectPurchase || !contractId.HasValue || contractId.Value <= 0)
                                {
                                    contractId = null;
                                }

                                var order = new P_ORDERS
                                {
                                    OrderNo = model.OrderNo,
                                    IssuedDate = model.IssuedDate,
                                    CompanyID = companyId,
                                    SupplierID = supplierId,
                                    ContractID = contractId,
                                    CurrencyID = model.CurrencyID ?? 0,
                                    Description = model.Description,
                                    IsDeleted = false,
                                    Amount = 0,
                                    PurchaseType = purchaseType,
                                    RequisitionID = requisition?.ID,
                                    DepartmentID = requisition?.DepartmentID,
                                    RequesterID = requisition?.RequesterID,
                                    ContactPhone = requisition?.ContactPhone,
                                    Purpose = requisition?.Purpose,
                                    Status = "Created",
                                    ContractStatus = "NotCreated",
                                    InvoiceStatus = "NotCreated",
                                    WarehouseStatus = "NotReceived"
                                };
                                db.P_ORDERS.Add(order);

                                // Buyurtma mahsulotlari: mahsulotlar va talabnoma qatorlari oldindan bitta so'rovda (SRM-P06)
                                var lines = (model.OrderProducts ?? new List<POrderProductViewModel>()).Where(l => l.ProductID > 0).ToList();
                                var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
                                var products = await db.PRODUCTS.AsNoTracking()
                                    .Where(p => productIds.Contains(p.ID) && p.IsDeleted != true)
                                    .Select(p => new { p.ID, p.UnitID })
                                    .ToDictionaryAsync(p => p.ID, p => p.UnitID);

                                var requisitionLines = new Dictionary<int, P_REQUISITION_PRODUCTS>();
                                if (requisition != null)
                                {
                                    requisitionLines = (await db.P_REQUISITION_PRODUCTS.AsNoTracking()
                                            .Where(rp => rp.RequisitionID == requisition.ID && productIds.Contains(rp.ProductID))
                                            .ToListAsync())
                                        .GroupBy(rp => rp.ProductID)
                                        .ToDictionary(g => g.Key, g => g.First());
                                }

                                double totalAmount = 0;
                                foreach (var orderProduct in lines)
                                {
                                    if (!products.ContainsKey(orderProduct.ProductID))
                                    {
                                        continue; // o'chirilgan yoki topilmagan mahsulot
                                    }
                                    var productUnitId = products[orderProduct.ProductID];

                                    // Talabnomadan ustuvorlik va kerakli sana (mavjud bo'lsa).
                                    // Hisob tushuntirish (sotuv/BOM generatsiyasi) bo'lsa — Priority maydonida saqlanadi.
                                    string priority = null;
                                    DateTime? requiredDate = null;
                                    P_REQUISITION_PRODUCTS reqProduct;
                                    if (requisitionLines.TryGetValue(orderProduct.ProductID, out reqProduct))
                                    {
                                        priority = reqProduct.Priority;
                                        requiredDate = reqProduct.RequiredDate;
                                    }

                                    if (!string.IsNullOrWhiteSpace(orderProduct.CalculationSummary))
                                    {
                                        priority = orderProduct.CalculationSummary.Trim();
                                    }
                                    else if (!string.IsNullOrWhiteSpace(orderProduct.CalculationExplanation))
                                    {
                                        priority = orderProduct.CalculationExplanation.Trim();
                                    }

                                    var pOrderProduct = new P_ORDER_PRODUCTS
                                    {
                                        P_ORDERS = order,
                                        ProductID = orderProduct.ProductID,
                                        Price = orderProduct.Price,
                                        Amount = orderProduct.Amount,
                                        UnitID = orderProduct.UnitID ?? (productUnitId > 0 ? productUnitId : 1),
                                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                                        MOQ = orderProduct.MOQ,
                                        Priority = TruncateToPOrderProductPriority(priority),
                                        RequiredDate = requiredDate
                                    };

                                    db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                                    totalAmount += pOrderProduct.TotalPrice;
                                }

                                // Buyurtmaning umumiy summasi QQS bilan: Amount = net summa + QQS.
                                order.Amount = VatMath.ApplyVat(totalAmount, model.VatRate);

                                await db.SaveChangesAsync();

                                // Talabnomadan yaratilgan bo'lsa — talabnoma holati (o'sha tranzaksiyada)
                                if (requisition != null)
                                {
                                    requisition.Status = "ConvertedToOrder";
                                    requisition.ConvertedToOrderID = order.ID;
                                    requisition.ModifiedDT = DateTime.Now;
                                    requisition.ModifiedByUserID = CurrentUserId;
                                    await db.SaveChangesAsync();
                                }

                                transaction.Commit();

                                LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {order.OrderNo} buyurtmasi yaratildi (Xarid turi: {(purchaseType == ProcurementLookups.IndirectPurchase ? "Bilvosita" : "Bevosita")})");

                                return RedirectToAction("Index");
                            }
                        }
                        catch (Exception ex)
                        {
                            // SaveChanges xatosidan keyin ulanish yopilgan bo'lishi mumkin — bekor qilish xatosi e'tiborsiz
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception)
                            {
                            }

                            AppendDatabaseErrorsToModelState(ModelState, ex);
                        }
                    }
                }
            }

            // Xato: GET bilan bir xil spravochniklar va kiritilgan qatorlar qayta ko'rsatiladi (SRM-B15)
            using (var db = new DBTHSNEntities())
            {
                P_REQUISITIONS requisition = null;
                if (model.RequisitionID.HasValue)
                {
                    requisition = await db.P_REQUISITIONS.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.ID == model.RequisitionID.Value && r.IsDeleted != true);
                }
                await PopulateCreateLookupsAsync(db, model, requisition);
            }

            return View(model);
        }

        // Olish so'rovi: POrders/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
                    .AsNoTracking()
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(o => o.ID == id && o.IsDeleted != true);

                if (order == null)
                {
                    return HttpNotFound();
                }

                var model = new POrderEditViewModel
                {
                    ID = order.ID,
                    OrderNo = order.OrderNo,
                    IssuedDate = order.IssuedDate,
                    CompanyID = order.CompanyID ?? 0,
                    SupplierID = order.SupplierID,
                    ContractID = order.ContractID,
                    CurrencyID = order.CurrencyID ?? 0,
                    Description = order.Description,
                    OrderProducts = order.P_ORDER_PRODUCTS.Select(op => new POrderProductViewModel
                    {
                        ID = op.ID,
                        ProductID = op.ProductID,
                        ProductPNo = op.PRODUCT.PNo,
                        ProductName = op.PRODUCT.Name,
                        Price = op.Price,
                        Amount = op.Amount,
                        UnitID = op.UnitID,
                        TotalPrice = op.TotalPrice,
                        MOQ = op.MOQ,
                        CalculationExplanation = op.Priority ?? "",
                        CalculationSummary = op.Priority ?? ""
                    }).ToList()
                };

                await PopulateEditLookupsAsync(db, model, order.PurchaseType == ProcurementLookups.DirectPurchase);

                // Buyurtmaga avval qo'llangan QQS stavkasini saqlangan ma'lumotdan ajratamiz
                // (Amount = net + QQS). Tahrirlashda input shartnoma stavkasi bilan emas, haqiqiy
                // qo'llangan stavka bilan to'ldiriladi — aks holda qayta saqlashda QQS yo'qoladi (SRM-D12).
                double editNetTotal = order.P_ORDER_PRODUCTS.Sum(p => p.TotalPrice);
                model.VatRate = editNetTotal > 0
                    ? VatMath.RateFromGrossNet(editNetTotal, order.Amount ?? editNetTotal)
                    : (double)ViewBag.ContractVatRate;
                ViewBag.AppliedVatRate = model.VatRate;

                return View(model);
            }
        }

        // POST so'rovi: POrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(POrderEditViewModel model)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var order = await db.P_ORDERS.FindAsync(model.ID);
                        if (order == null || order.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        var editOrderNo = string.IsNullOrWhiteSpace(model.OrderNo) ? null : model.OrderNo.Trim();
                        if (!string.IsNullOrEmpty(editOrderNo))
                        {
                            if (await ActiveOrderNoExistsAsync(db, editOrderNo, excludeOrderId: model.ID))
                            {
                                ModelState.AddModelError("OrderNo", "Bu buyurtma raqami allaqachon mavjud (faol buyurtma).");
                            }
                            else
                            {
                                await ReleaseOrderNoFromSoftDeletedRowsAsync(db, editOrderNo, excludeOrderId: model.ID);
                            }
                        }

                        if (ModelState.IsValid)
                        {
                            using (var tx = db.Database.BeginTransaction())
                            {
                                order.OrderNo = editOrderNo ?? model.OrderNo;
                                order.IssuedDate = model.IssuedDate;
                                order.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db);

                                bool isDirectOrder = order.PurchaseType == ProcurementLookups.DirectPurchase;
                                int? supplierId = model.SupplierID;
                                if (isDirectOrder || !supplierId.HasValue || supplierId.Value <= 0)
                                {
                                    supplierId = null;
                                }

                                int? contractId = model.ContractID;
                                if (isDirectOrder || !contractId.HasValue || contractId.Value <= 0)
                                {
                                    contractId = null;
                                }

                                order.SupplierID = supplierId;
                                order.ContractID = contractId;
                                order.CurrencyID = model.CurrencyID;
                                order.Description = model.Description;

                                var existingProducts = await db.P_ORDER_PRODUCTS.Where(op => op.OrderID == order.ID).ToListAsync();
                                db.P_ORDER_PRODUCTS.RemoveRange(existingProducts);

                                // Mahsulot birliklari bitta so'rovda (avval har qatorga FindAsync, SRM-P06)
                                var lines = model.OrderProducts ?? new List<POrderProductViewModel>();
                                var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
                                var products = await db.PRODUCTS.AsNoTracking()
                                    .Where(p => productIds.Contains(p.ID))
                                    .Select(p => new { p.ID, p.UnitID })
                                    .ToDictionaryAsync(p => p.ID, p => p.UnitID);

                                double totalAmount = 0;
                                foreach (var orderProduct in lines)
                                {
                                    if (!products.ContainsKey(orderProduct.ProductID))
                                    {
                                        continue;
                                    }
                                    var productUnitId = products[orderProduct.ProductID];

                                    var pOrderProduct = new P_ORDER_PRODUCTS
                                    {
                                        OrderID = order.ID,
                                        ProductID = orderProduct.ProductID,
                                        Price = orderProduct.Price,
                                        Amount = orderProduct.Amount,
                                        UnitID = orderProduct.UnitID ?? (productUnitId > 0 ? productUnitId : 1),
                                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                                        MOQ = orderProduct.MOQ,
                                        Priority = TruncateToPOrderProductPriority(
                                            !string.IsNullOrWhiteSpace(orderProduct.CalculationSummary)
                                                ? orderProduct.CalculationSummary.Trim()
                                                : (string.IsNullOrWhiteSpace(orderProduct.CalculationExplanation)
                                                    ? null
                                                    : orderProduct.CalculationExplanation.Trim()))
                                    };

                                    db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                                    totalAmount += pOrderProduct.TotalPrice;
                                }

                                // Umumiy summa QQS bilan: Amount = net summa + QQS.
                                order.Amount = VatMath.ApplyVat(totalAmount, model.VatRate);
                                await db.SaveChangesAsync();
                                tx.Commit();
                            }

                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Edit - {order.OrderNo} buyurtmasi tahrirlandi");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendDatabaseErrorsToModelState(ModelState, ex);
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                var purchaseType = await db.P_ORDERS
                    .Where(o => o.ID == model.ID)
                    .Select(o => (byte?)o.PurchaseType)
                    .FirstOrDefaultAsync();
                await PopulateEditLookupsAsync(db, model, purchaseType == ProcurementLookups.DirectPurchase);
            }

            return View(model);
        }

        // Olish so'rovi: POrders/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
                    .AsNoTracking()
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(o => o.ID == id && o.IsDeleted != true);

                if (order == null)
                {
                    return HttpNotFound();
                }

                return View(order);
            }
        }

        // POST so'rovi: POrders/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS.FindAsync(id);
                if (order != null && order.IsDeleted != true)
                {
                    order.IsDeleted = true;
                    // O'chirilgan buyurtmaning OrderNo siga _d suffix qo'shamiz — xuddi shu OrderNo bilan
                    // darhol yangi buyurtma kiritilganda UNIQUE constraint xatosi chiqmasligi uchun.
                    order.OrderNo = BuildDeletedOrderNo(order.OrderNo, order.ID);
                    await SoftDeleteReferenceService.AfterPOrderSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(CurrentUserName, Module, $"Delete - {order.OrderNo} buyurtmasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [Permission(Module)]
        public async Task<JsonResult> GetContractsBySupplier(int supplierId)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var data = await ProcurementLookups.GetSupplierContractsWithVatAsync(db, supplierId);
                    return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return JsonError(ex);
            }
        }

        // ---------- yordamchilar ----------

        /// <summary>
        /// Create sahifasi spravochniklari — GET va xato yo'li uchun bitta joy (SRM-B15: avval xato yo'lida
        /// DefaultCompanyID va talabnoma o'rnatilmay, CompanyID=0 bilan saqlanardi).
        /// View'da ishlatilmaydigan ta'minotchi/shartnoma/bo'lim/xodim ro'yxatlari olib tashlandi (SRM-P07).
        /// </summary>
        private async Task PopulateCreateLookupsAsync(DBTHSNEntities db, POrderCreateViewModel model, P_REQUISITIONS requisition)
        {
            bool isIndirect = requisition != null;
            ViewBag.PurchaseType = isIndirect ? 1 : 0;
            ViewBag.Requisition = requisition;

            if (!isIndirect)
            {
                // Sotuv buyurtmalari (BOM generatsiyasi uchun) — faqat bevosita xaridda
                var salesOrders = await db.S_ORDERS.AsNoTracking()
                    .Where(o => o.IsDeleted != true)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo, CustomerName = o.CUSTOMER.Name })
                    .ToListAsync();

                ViewBag.SalesOrders = new MultiSelectList(
                    salesOrders.Select(so => new { so.ID, DisplayText = $"{so.OrderNo} - {so.CustomerName}" }),
                    "ID", "DisplayText",
                    model?.SelectedSalesOrderIds);
            }

            ViewBag.DefaultCompanyID = await ProcurementLookups.GetDefaultCompanyIdAsync(db);
            ViewBag.Currencies = ProcurementLookups.Currencies(model?.CurrencyID);

            // Bilvosita: uskuna, asbob, sarf materiallari; bevosita: xomashyo (BOM)
            var products = await ProcurementLookups.GetPurchasableProductsAsync(
                db,
                isIndirect ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase,
                model?.OrderProducts?.Select(p => p.ProductID));
            ViewBag.PurchasableProducts = new SelectList(products, "ID", "PNo");
            ViewBag.Units = ProcurementLookups.Units();
        }

        /// <summary>Edit sahifasi spravochniklari (GET va xato yo'li).</summary>
        private async Task PopulateEditLookupsAsync(DBTHSNEntities db, POrderEditViewModel model, bool isDirectOrder)
        {
            ViewBag.IsDirectOrder = isDirectOrder;
            ViewBag.Suppliers = ProcurementLookups.Suppliers(isDirectOrder ? null : model.SupplierID);
            if (!isDirectOrder && model.SupplierID.HasValue)
            {
                ViewBag.Contracts = await ProcurementLookups.ContractsBySupplierAsync(db, model.SupplierID, model.ContractID);
            }
            else
            {
                ViewBag.Contracts = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
                model.SupplierID = null;
                model.ContractID = null;
            }

            var companies = await db.COMPANIES.AsNoTracking()
                .Where(c => c.IsActive != false)
                .Select(c => new { c.ID, c.Name })
                .ToListAsync();
            ViewBag.Companies = new SelectList(companies, "ID", "Name", model.CompanyID);
            ViewBag.Currencies = ProcurementLookups.Currencies(model.CurrencyID);

            var products = await ProcurementLookups.GetPurchasableProductsAsync(
                db, ProcurementLookups.DirectPurchase, model.OrderProducts?.Select(p => p.ProductID));
            ViewBag.PurchasableProductsList = products;
            ViewBag.UnitsList = ProcurementLookups.UnitItems();
            ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, model.ContractID);
        }

        /// <summary>PO-{yil}-{oy}-NNNN — qulf bilan; "_d{ID}" bilan arxivlangan o'chirilgan raqamlar qayta ishlatilishi mumkin.</summary>
        private static Task<string> GenerateOrderNoAsync(DBTHSNEntities db)
        {
            var now = DateTime.Now;
            return DocumentNumberService.NextAsync(db, "P_ORDERS", "OrderNo", $"PO-{now.Year}-{now.Month:D2}-", 4);
        }

        private const int POrderNoMaxLength = 50;
        private const string ArchivedOrderNoSuffixDeleted = "_deleted";
        private const string ArchivedOrderNoSuffixDl = "_dl";

        /// <summary>
        /// O'chirish vaqtida OrderNo ni arxivlaydi: "_d{ID}" suffiksi (ID unikal — takror to'qnashuv bo'lmaydi).
        /// Shu sababli o'chirilgan buyurtma OrderNo si darhol bo'shaydi va yangi yozuvga to'sqinlik qilmaydi.
        /// </summary>
        private static string BuildDeletedOrderNo(string orderNo, int id)
        {
            var suffix = "_d" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return suffix;
            }

            var trimmed = orderNo.Trim();
            // Allaqachon arxivlangan bo'lsa (har qanday _d<raqam>), qayta suffix qo'shilmaydi.
            if (Regex.IsMatch(trimmed, @"_d\d+$", RegexOptions.IgnoreCase))
            {
                return trimmed;
            }

            var combined = trimmed + suffix;
            if (combined.Length <= POrderNoMaxLength)
            {
                return combined;
            }

            var keepBase = POrderNoMaxLength - suffix.Length;
            if (keepBase < 0)
            {
                keepBase = 0;
            }

            return trimmed.Substring(0, keepBase) + suffix;
        }

        private static string BuildArchivedOrderNo(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return orderNo;
            }

            var trimmed = orderNo.Trim();
            if (trimmed.EndsWith(ArchivedOrderNoSuffixDeleted, StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith(ArchivedOrderNoSuffixDl, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            var withDeleted = trimmed + ArchivedOrderNoSuffixDeleted;
            if (withDeleted.Length <= POrderNoMaxLength)
            {
                return withDeleted;
            }

            var withDl = trimmed + ArchivedOrderNoSuffixDl;
            if (withDl.Length <= POrderNoMaxLength)
            {
                return withDl;
            }

            var keepBase = POrderNoMaxLength - ArchivedOrderNoSuffixDl.Length;
            return trimmed.Substring(0, keepBase) + ArchivedOrderNoSuffixDl;
        }

        private static async Task<bool> ActiveOrderNoExistsAsync(DBTHSNEntities db, string orderNo, int? excludeOrderId)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return false;
            }

            var normalized = orderNo.Trim();
            return await db.P_ORDERS.AnyAsync(o =>
                o.OrderNo == normalized
                && o.IsDeleted != true
                && (!excludeOrderId.HasValue || o.ID != excludeOrderId.Value));
        }

        /// <summary>
        /// O'chirilgan buyurtmalarning OrderNo sini arxivlaydi — yangi yozuv uchun UNIQUE constraint bo'shatiladi.
        /// </summary>
        private static async Task ReleaseOrderNoFromSoftDeletedRowsAsync(
            DBTHSNEntities db, string orderNo, int? excludeOrderId)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return;
            }

            var normalized = orderNo.Trim();
            var rows = await db.P_ORDERS
                .Where(o => o.IsDeleted == true
                    && o.OrderNo == normalized
                    && (!excludeOrderId.HasValue || o.ID != excludeOrderId.Value))
                .ToListAsync();

            if (!rows.Any())
            {
                return;
            }

            foreach (var row in rows)
            {
                row.OrderNo = BuildArchivedOrderNo(row.OrderNo);
            }

            await db.SaveChangesAsync();
        }

        /// <summary>
        /// P_ORDER_PRODUCTS.Priority — jadvalda nvarchar(50): talabnomada "ustuvorlik" uchun.
        /// Hisob tushuntirish uzun bo'lsa, saqlashdan oldin qisqartiriladi (to'liq matn DB ga sig'maydi).
        /// </summary>
        private static string TruncateToPOrderProductPriority(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            const int maxLen = 50;
            var t = value.Trim();
            if (t.Length <= maxLen)
            {
                return t;
            }

            return t.Substring(0, maxLen - 3) + "...";
        }

        /// <summary>
        /// EF EntityValidationErrors va yangilanish xatolarini ModelState orqali forma ustida ko'rsatish.
        /// </summary>
        private static void AppendDatabaseErrorsToModelState(System.Web.Mvc.ModelStateDictionary modelState, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            var dbVal = ex as DbEntityValidationException ?? FindInnerException<DbEntityValidationException>(ex);
            if (dbVal != null)
            {
                foreach (var eve in dbVal.EntityValidationErrors)
                {
                    string entityLabel;
                    try
                    {
                        entityLabel = eve.Entry?.Entity?.GetType()?.Name ?? "Entity";
                    }
                    catch
                    {
                        entityLabel = "Entity";
                    }

                    if (eve.ValidationErrors == null || !eve.ValidationErrors.Any())
                    {
                        modelState.AddModelError("", entityLabel + ": validatsiya xatosi (batafsil yo'q).");
                        continue;
                    }

                    foreach (var ve in eve.ValidationErrors)
                    {
                        var prop = string.IsNullOrEmpty(ve.PropertyName) ? "(maydon)" : ve.PropertyName;
                        modelState.AddModelError("", $"{entityLabel} — {prop}: {ve.ErrorMessage}");
                    }
                }

                return;
            }

            var dbUp = ex as DbUpdateException ?? FindInnerException<DbUpdateException>(ex);
            if (dbUp != null)
            {
                modelState.AddModelError("", "Ma'lumotlar bazasiga yozishda xatolik: " + dbUp.Message);
                var inner = dbUp.InnerException;
                while (inner != null)
                {
                    modelState.AddModelError("", "Ichki: " + inner.Message);
                    inner = inner.InnerException;
                }

                return;
            }

            var msg = "Xatolik: " + ex.Message;
            for (var innerEx = ex.InnerException; innerEx != null; innerEx = innerEx.InnerException)
            {
                msg += " | Ichki xatolik: " + innerEx.Message;
            }

            modelState.AddModelError("", msg);
        }

        private static T FindInnerException<T>(Exception ex) where T : Exception
        {
            for (var cur = ex.InnerException; cur != null; cur = cur.InnerException)
            {
                if (cur is T match)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
