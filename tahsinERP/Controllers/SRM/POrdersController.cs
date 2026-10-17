using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
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
                    POrderService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID), page,
                    POrderService.SearchIndex, POrderService.OrderIndex);

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
                var query = POrderService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = POrderService.SearchIndex(query, q.Trim());
                }

                var rows = await POrderService.OrderIndex(query, 2, true).ToListAsync();
                var bytes = ExcelExport.Create("Xarid buyurtmalari", rows,
                    ExcelExport.Col<POrderService.IndexRow>("Buyurtma raqami", r => r.OrderNo),
                    ExcelExport.Col<POrderService.IndexRow>("Turi", r => r.PurchaseType == ProcurementLookups.IndirectPurchase ? "Indirect" : "Direct"),
                    ExcelExport.Col<POrderService.IndexRow>("Sana", r => r.IssuedDate, "dd.MM.yyyy"),
                    ExcelExport.Col<POrderService.IndexRow>("Summa (QQS bilan)", r => r.Amount ?? 0, "#,##0.00"),
                    ExcelExport.Col<POrderService.IndexRow>("Valyuta", r => r.Currency));

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
                    try
                    {
                        // Raqam, sarlavha, qatorlar va talabnoma holati — bitta tranzaksiyada (xatoda bekor qilinadi)
                        var order = await new POrderService(db, ModelState).CreateAsync(model, CurrentUserId);
                        if (order != null)
                        {
                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {order.OrderNo} buyurtmasi yaratildi (Xarid turi: {(order.PurchaseType == ProcurementLookups.IndirectPurchase ? "Bilvosita" : "Bevosita")})");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendDatabaseErrorsToModelState(ModelState, ex);
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

                        if (await new POrderService(db, ModelState).UpdateAsync(order, model))
                        {
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
                var order = await new POrderService(db, ModelState).SoftDeleteAsync(id);
                if (order != null)
                {
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
