using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [Permission]
    public class SOrderController : BaseController
    {
        // Olish so'rovi: SOrder | Index - List barcha orders
        public async Task<ActionResult> Index(string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, searchTerm, orderNo, customerID, contractID, fromDate, toDate, sortOrder);

                // Server tomonida sahifalash (CRM-P05, umumiy blok — D16)
                var orders = await query.ToPageAsync(page, pageSize, ViewData);

                ViewBag.Customers = SalesLookups.Customers(customerID);
                ViewBag.Contracts = new SelectList(await db.S_CONTRACTS.AsNoTracking()
                    .Where(c => c.IsDeleted == false || c.IsDeleted == null)
                    .OrderBy(c => c.ContractNo)
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToListAsync(), "ID", "ContractNo", contractID);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.OrderNo = orderNo;
                ViewBag.CustomerID = customerID;
                ViewBag.ContractID = contractID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.SortOrder = sortOrder;

                return View(orders);
            }
        }

        /// <summary>Filtrlangan barcha buyurtmalar Excel'da.</summary>
        public ActionResult ExportExcel(string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var rows = BuildIndexQuery(db, searchTerm, orderNo, customerID, contractID, fromDate, toDate, sortOrder).ToList();
                var bytes = ExcelExport.Create(L.T("Menu_Orders"), rows,
                    ExcelExport.Col<S_ORDERS>(L.T("Crm_OrderNo"), o => o.OrderNo),
                    ExcelExport.Col<S_ORDERS>(L.T("Crm_Client"), o => o.CUSTOMER?.Name),
                    ExcelExport.Col<S_ORDERS>(L.T("Crm_Contract"), o => o.S_CONTRACTS?.ContractNo),
                    ExcelExport.Col<S_ORDERS>(L.T("Date"), o => o.IssuedDate, "dd.MM.yyyy"),
                    ExcelExport.Col<S_ORDERS>(L.T("Amount"), o => o.Amount, "#,##0.00"),
                    ExcelExport.Col<S_ORDERS>(L.T("Currency"), o => o.CURRENCy1?.Shortname));
                return File(bytes, ExcelExport.ContentType, $"{L.T("Crm_OrdExcelFile")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        private static IQueryable<S_ORDERS> BuildIndexQuery(DBTHSNEntities db, string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder)
        {
            var query = db.S_ORDERS.AsNoTracking()
                .Include(o => o.CUSTOMER)
                .Include(o => o.S_CONTRACTS)
                .Include(o => o.CURRENCy1)
                .Where(o => o.IsDeleted == false || o.IsDeleted == null);

            // Filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    o.OrderNo.Contains(searchTerm) ||
                    (o.Description != null && o.Description.Contains(searchTerm)) ||
                    (o.CUSTOMER != null && o.CUSTOMER.Name.Contains(searchTerm)));
            }

            if (!string.IsNullOrEmpty(orderNo))
            {
                query = query.Where(o => o.OrderNo.Contains(orderNo));
            }

            if (customerID.HasValue && customerID.Value > 0)
            {
                query = query.Where(o => o.CustomerID == customerID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(o => o.ContractID == contractID.Value);
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(o => o.IssuedDate >= from);
            }

            if (toDate.HasValue)
            {
                // Yarim ochiq oraliq — oxirgi kun to'liq kiradi
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(o => o.IssuedDate < toExclusive);
            }

            // Sorting
            switch (sortOrder)
            {
                case "orderNo_desc":
                    query = query.OrderByDescending(o => o.OrderNo);
                    break;
                case "orderNo":
                    query = query.OrderBy(o => o.OrderNo);
                    break;
                case "date":
                    query = query.OrderBy(o => o.IssuedDate);
                    break;
                case "customer":
                    query = query.OrderBy(o => o.CUSTOMER.Name);
                    break;
                case "customer_desc":
                    query = query.OrderByDescending(o => o.CUSTOMER.Name);
                    break;
                default:
                    query = query.OrderByDescending(o => o.IssuedDate);
                    break;
            }

            return query;
        }

        // Olish so'rovi: yaratish
        [HttpGet]
        public ActionResult Create(int? customerID, int? contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = new SOrderViewModel
                {
                    IssuedDate = DateTime.Now,
                    ProductQuantities = new List<ContractProductQuantity>()
                };

                if (customerID.HasValue && customerID.Value > 0)
                {
                    model.CustomerID = customerID.Value;
                }

                if (contractID.HasValue && contractID.Value > 0)
                {
                    model.ContractID = contractID.Value;
                    model.ProductQuantities = SalesOrderService.LoadContractProducts(db, contractID.Value);
                }

                SetupFormLookups(db, model);
                return View(model);
            }
        }

        // POST so'rovi: yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SOrderViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                model.ProductQuantities = ParseProductQuantities(form);
                if (!ModelState.IsValid)
                {
                    return FormError(db, model);
                }

                try
                {
                    var order = SalesOrderService.Create(db, model, CurrentUserName);
                    return RedirectToAction("DeliverySchedule", new { orderID = order.ID });
                }
                catch (SalesOrderException ex)
                {
                    return FormError(db, model, ex);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_ErrorPrefix", InnermostMessage(ex)));
                    return FormError(db, model);
                }
            }
        }

        // Olish so'rovi: GetContractsByCustomer - AJAX
        [HttpGet]
        public ActionResult GetContractsByCustomer(int customerId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return Json(SalesLookups.ContractsByCustomer(db, customerId), JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: GetContractProducts - AJAX
        [HttpGet]
        public ActionResult GetContractProducts(int contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProducts = SalesOrderService.LoadContractProducts(db, contractId)
                    .Select(cp => new
                    {
                        cp.ContractProductID,
                        cp.ProductID,
                        ProductPNo = cp.ProductPNo,
                        cp.ProductName,
                        cp.PiecePrice,
                        cp.ContractAmount,
                        cp.UnitID,
                        cp.UnitName,
                        cp.VatRate
                    })
                    .ToList();

                return Json(contractProducts, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: DeliverySchedule
        [HttpGet]
        public ActionResult DeliverySchedule(int? orderID)
        {
            if (orderID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .Active().FirstOrDefault(o => o.ID == orderID);

                if (order == null)
                {
                    return HttpNotFound();
                }

                var model = new DeliveryScheduleViewModel
                {
                    OrderID = order.ID,
                    OrderNo = order.OrderNo,
                    CustomerName = order.CUSTOMER?.Name ?? "",
                    IssuedDate = order.IssuedDate
                };

                model.OrderProducts = order.S_ORDER_PRODUCTS.Select(p => new OrderProductForSchedule
                {
                    SOrderProductID = p.ID,
                    ProductID = p.ProductID,
                    ProductPNo = p.PRODUCT?.PNo ?? "",
                    ProductName = p.PRODUCT?.Name ?? "",
                    OrderQuantity = p.Amount,
                    UnitID = p.UnitID,
                    UnitName = p.UNIT?.ShortName ?? ""
                }).ToList();

                // PRODUCT oldindan yuklanadi (avval har qator uchun lazy-load — CRM-P07)
                var scheduleItems = db.SORDER_DELIVERY_SCHEDULE
                    .Include(s => s.PRODUCT)
                    .Where(s => s.SOrderID == orderID)
                    .ToList();

                foreach (var orderProduct in model.OrderProducts)
                {
                    var productSchedules = scheduleItems.Where(s => s.SOrderProductID == orderProduct.SOrderProductID).ToList();
                    orderProduct.PlannedQuantity = productSchedules.Sum(s => s.QtyPlanned);
                    orderProduct.ShippedQuantity = productSchedules.Sum(s => s.QtyShipped);
                }

                model.ScheduleItems = scheduleItems.Select(s => new DeliveryScheduleItem
                {
                    ID = s.ID,
                    SOrderProductID = s.SOrderProductID,
                    ProductID = s.ProductID ?? 0,
                    ProductPNo = s.PRODUCT?.PNo ?? "",
                    ProductName = s.PRODUCT?.Name ?? "",
                    DeliveryDate = s.DeliveryDate,
                    QtyPlanned = s.QtyPlanned,
                    QtyShipped = s.QtyShipped,
                    StatusCode = s.StatusCode
                }).OrderBy(s => s.DeliveryDate).ToList();

                return View(model);
            }
        }

        /// <summary>Yetkazib berish rejasini saqlash — ID bo'yicha upsert (CRM-B06, mantiq SalesOrderService'da).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveDeliverySchedule(int orderID, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    SalesOrderService.SaveDeliverySchedule(db, orderID, ParseScheduleItems(form), CurrentUserName);
                    TempData["Success"] = L.T("Crm_OrdScheduleSaved");
                    return RedirectToAction("Details", new { id = orderID });
                }
                catch (SalesOrderException ex) when (ex.NotFound)
                {
                    return HttpNotFound();
                }
                catch (SalesOrderException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = L.T("Crm_OrdScheduleSaveFailed", InnermostMessage(ex));
                }
                return RedirectToAction("DeliverySchedule", new { orderID });
            }
        }

        // Olish so'rovi: Details
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.S_CONTRACTS)
                    .Include(o => o.CURRENCy1)
                    .Include(o => o.COMPANy)
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefault(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));

                if (order == null)
                {
                    return HttpNotFound();
                }

                var deliverySchedule = db.SORDER_DELIVERY_SCHEDULE
                    .Include(d => d.PRODUCT)
                    .Where(d => d.SOrderID == id)
                    .OrderBy(d => d.DeliveryDate)
                    .ToList();

                ViewBag.DeliverySchedule = deliverySchedule;
                ViewBag.DeliveryScheduleCount = deliverySchedule.Count;

                // Typed lug'at (CRM-B09: anonim tipni view'da Dictionary<int, dynamic> ga cast qilib bo'lmasdi)
                ViewBag.ScheduleSummary = deliverySchedule
                    .GroupBy(d => d.SOrderProductID)
                    .ToDictionary(g => g.Key, g => new ScheduleSummaryDto
                    {
                        SOrderProductID = g.Key,
                        TotalPlanned = g.Sum(d => d.QtyPlanned),
                        TotalShipped = g.Sum(d => d.QtyShipped)
                    });
                ViewBag.InvoicesCount = db.SALES_INVOICES.Active().Count(i => i.OrderID == id);
                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db, order.ContractID > 0 ? (int?)order.ContractID : null);
                ViewBag.ProductVatRates = SContractVatHelper.GetProductVatRates(db, order.ContractID);

                return View(order);
            }
        }

        // Olish so'rovi: Edit
        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.S_ORDER_PRODUCTS)
                    .FirstOrDefault(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));

                if (order == null)
                {
                    return HttpNotFound();
                }

                var model = new SOrderViewModel
                {
                    ID = order.ID,
                    OrderNo = order.OrderNo,
                    IssuedDate = order.IssuedDate,
                    CustomerID = order.CustomerID,
                    ContractID = order.ContractID,
                    CurrencyID = order.CurrencyID,
                    Description = order.Description,
                    Amount = (order.Amount)
                };

                SetupFormLookups(db, model);
                model.ProductQuantities = SalesOrderService.LoadContractProducts(db, order.ContractID);
                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db, order.ContractID > 0 ? (int?)order.ContractID : null);

                // Mavjud qatorlar miqdori (shartnoma qatori bo'yicha; topilmasa — mahsulot bo'yicha)
                foreach (var orderProduct in order.S_ORDER_PRODUCTS)
                {
                    var contractProduct = model.ProductQuantities.FirstOrDefault(pq => pq.ProductID == orderProduct.ProductID && pq.OrderQuantity == 0);
                    if (contractProduct != null)
                    {
                        contractProduct.OrderQuantity = orderProduct.Amount;
                    }
                }

                return View(model);
            }
        }

        /// <summary>Tahrirlash — qatorlar joyida yangilanadi (CRM-B10, mantiq SalesOrderService'da).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, SOrderViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                model.ID = id;
                model.ProductQuantities = ParseProductQuantities(form);
                if (!ModelState.IsValid)
                {
                    return FormError(db, model);
                }

                try
                {
                    var order = SalesOrderService.Update(db, id, model, CurrentUserName);
                    return RedirectToAction("Details", new { id = order.ID });
                }
                catch (SalesOrderException ex) when (ex.NotFound)
                {
                    return HttpNotFound();
                }
                catch (SalesOrderException ex)
                {
                    return FormError(db, model, ex);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_ErrorPrefix", InnermostMessage(ex)));
                    return FormError(db, model);
                }
            }
        }

        // Olish so'rovi: o'chirish
        [HttpGet]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.S_CONTRACTS)
                    .Include(o => o.CURRENCy1)
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefault(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));

                if (order == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db, order.ContractID > 0 ? (int?)order.ContractID : null);
                ViewBag.ProductVatRates = SContractVatHelper.GetProductVatRates(db, order.ContractID);

                return View(order);
            }
        }

        // POST so'rovi: o'chirish
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    await SalesOrderService.DeleteAsync(db, id, CurrentUserName);
                    return RedirectToAction("Index");
                }
                catch (SalesOrderException ex) when (ex.NotFound)
                {
                    return HttpNotFound();
                }
                catch (SalesOrderException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = L.T("Crm_OrdDeleteFailed", InnermostMessage(ex));
                }
                return RedirectToAction("Details", new { id });
            }
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        /// <summary>
        /// Formadagi "ProductQuantities[i].*" maydonlari (Create va Edit uchun bitta joy — D14).
        /// Narx/birlik formadan olinmaydi — SalesOrderService shartnomadan to'ldiradi (CRM-S03).
        /// </summary>
        private static List<ContractProductQuantity> ParseProductQuantities(FormCollection form)
        {
            const string prefix = "ProductQuantities";
            var result = new List<ContractProductQuantity>();
            foreach (var i in FormRows.Indexes(form, prefix, "OrderQuantity"))
            {
                decimal quantity;
                int contractProductId;
                if (!CrmNumber.TryDecimal(FormRows.Value(form, prefix, i, "OrderQuantity"), out quantity) || quantity <= 0) continue;
                if (!int.TryParse(FormRows.Value(form, prefix, i, "ContractProductID"), out contractProductId)) continue;

                result.Add(new ContractProductQuantity
                {
                    ContractProductID = contractProductId,
                    ProductID = FormRows.Int(form, prefix, i, "ProductID"),
                    OrderQuantity = quantity
                });
            }
            return result;
        }

        /// <summary>Formadagi "ScheduleItems[i].*" qatorlari (indekslarda bo'shliq bo'lsa ham — kalitlar bo'yicha o'qiladi).</summary>
        private static List<DeliveryScheduleItem> ParseScheduleItems(FormCollection form)
        {
            const string prefix = "ScheduleItems";
            var posted = new List<DeliveryScheduleItem>();
            foreach (var i in FormRows.Indexes(form, prefix, "DeliveryDate"))
            {
                var rawDate = FormRows.Value(form, prefix, i, "DeliveryDate");
                DateTime deliveryDate;
                decimal qtyPlanned;
                int orderProductId;
                if (!DateTime.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out deliveryDate) &&
                    !DateTime.TryParse(rawDate, out deliveryDate))
                    continue;
                if (!CrmNumber.TryDecimal(FormRows.Value(form, prefix, i, "QtyPlanned"), out qtyPlanned) || qtyPlanned <= 0)
                    continue;
                if (!int.TryParse(FormRows.Value(form, prefix, i, "OrderProductID"), out orderProductId))
                    continue;

                var id = FormRows.Long(form, prefix, i, "ID");
                posted.Add(new DeliveryScheduleItem
                {
                    ID = id > 0 ? id : (long?)null,
                    SOrderProductID = orderProductId,
                    DeliveryDate = deliveryDate,
                    QtyPlanned = qtyPlanned
                });
            }
            return posted;
        }

        /// <summary>
        /// Xato bilan qaytish: ro'yxatlar qayta quriladi, shartnoma mahsulotlari yuklanib,
        /// foydalanuvchi kiritgan miqdorlar saqlanadi (CRM-B11: avval hammasi 0 ga tushardi).
        /// </summary>
        private ActionResult FormError(DBTHSNEntities db, SOrderViewModel model, SalesOrderException error = null)
        {
            if (error != null)
            {
                foreach (var message in error.Errors)
                {
                    ModelState.AddModelError("", message);
                }
            }

            SetupFormLookups(db, model);
            if (model.ContractID > 0)
            {
                var posted = model.ProductQuantities ?? new List<ContractProductQuantity>();
                var reloaded = SalesOrderService.LoadContractProducts(db, model.ContractID);
                foreach (var line in reloaded)
                {
                    var q = posted.FirstOrDefault(p => p.ContractProductID == line.ContractProductID);
                    if (q != null) line.OrderQuantity = q.OrderQuantity;
                }
                model.ProductQuantities = reloaded;
            }
            return View(model);
        }

        /// <summary>Create/Edit ro'yxatlari (avvalgi SetupViewBagForError ≡ SetupViewBagForEdit — D11).</summary>
        private void SetupFormLookups(DBTHSNEntities db, SOrderViewModel model)
        {
            ViewBag.Customers = SalesLookups.CustomersWithPlaceholder(model.CustomerID, "0", L.T("SelectPlaceholder"));
            ViewBag.SContract = SalesLookups.ContractsFor(db, model.CustomerID, model.ContractID, "0", L.T("SelectPlaceholder"));
        }
    }
}