using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
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
        private static readonly Regex IndexPattern = new Regex(@"\[(\d+)\]", RegexOptions.Compiled);

        // Olish so'rovi: SOrder | Index - List barcha orders
        public async Task<ActionResult> Index(string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, searchTerm, orderNo, customerID, contractID, fromDate, toDate, sortOrder);

                // Server tomonida sahifalash (CRM-P05: avval butun jadval Include bilan yuklanardi)
                int itemsPerPage = Math.Min(Math.Max(pageSize ?? 25, 1), 200);
                int totalItems = await query.CountAsync();
                int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / itemsPerPage));
                int currentPage = Math.Min(Math.Max(page ?? 1, 1), totalPages);
                var orders = await query.Skip((currentPage - 1) * itemsPerPage).Take(itemsPerPage).ToListAsync();
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;

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
                var bytes = ExcelExport.Create("Buyurtmalar", rows,
                    ExcelExport.Col<S_ORDERS>("Buyurtma raqami", o => o.OrderNo),
                    ExcelExport.Col<S_ORDERS>("Mijoz", o => o.CUSTOMER?.Name),
                    ExcelExport.Col<S_ORDERS>("Shartnoma", o => o.S_CONTRACTS?.ContractNo),
                    ExcelExport.Col<S_ORDERS>("Sana", o => o.IssuedDate, "dd.MM.yyyy"),
                    ExcelExport.Col<S_ORDERS>("Summa", o => o.Amount, "#,##0.00"),
                    ExcelExport.Col<S_ORDERS>("Valyuta", o => o.CURRENCy1 != null ? o.CURRENCy1.Shortname : o.Currency));
                return File(bytes, ExcelExport.ContentType, $"Buyurtmalar_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
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
                    model.ProductQuantities = LoadContractProducts(db, contractID.Value);
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

                if (!model.ProductQuantities.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta maxsulot uchun miqdor kiritish kerak");
                    return FormError(db, model);
                }

                if (!ResolveContractLines(db, model) || !ValidateOrderQuantities(db, model, null) || !ModelState.IsValid)
                {
                    return FormError(db, model);
                }

                try
                {
                    var contractCurrencyId = db.S_CONTRACTS.Where(c => c.ID == model.ContractID).Select(c => (int?)c.CurrencyID).FirstOrDefault() ?? 0;
                    var vatByProduct = SContractVatHelper.GetProductVatRates(db, model.ContractID);

                    var order = new S_ORDERS
                    {
                        OrderNo = model.OrderNo,
                        IssuedDate = model.IssuedDate,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        CustomerID = model.CustomerID,
                        ContractID = model.ContractID,
                        CurrencyID = contractCurrencyId,
                        Description = model.Description,
                        IsDeleted = false
                    };

                    // Sarlavha va qatorlar bitta SaveChanges'da (avval ikki bosqichda — qatorlarsiz buyurtma qolishi mumkin edi)
                    double totalAmount = 0;
                    foreach (var q in model.ProductQuantities)
                    {
                        var line = new S_ORDER_PRODUCTS { MOQ = 0 };
                        totalAmount += ApplyLine(line, q, vatByProduct);
                        order.S_ORDER_PRODUCTS.Add(line);
                    }
                    order.Amount = totalAmount;

                    db.S_ORDERS.Add(order);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(CurrentUserName, "SOrderController", $"{order.OrderNo} - Buyurtmani yaratdi");

                    return RedirectToAction("DeliverySchedule", new { orderID = order.ID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
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
                var contractProducts = LoadContractProducts(db, contractId)
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
                    .FirstOrDefault(o => o.ID == orderID && (o.IsDeleted == false || o.IsDeleted == null));

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
                    orderProduct.PlannedQuantity = (double)productSchedules.Sum(s => s.QtyPlanned);
                    orderProduct.ShippedQuantity = (double)productSchedules.Sum(s => s.QtyShipped);
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

        /// <summary>
        /// Yetkazib berish rejasini saqlash — ID bo'yicha upsert (CRM-B06).
        /// Avval hamma qatorlar o'chirilib, QtyShipped = 0 / PLANNED bilan qayta yozilardi: yetkazilgan miqdor
        /// yo'qolardi, QUALITY_INSPECTION.DeliveryScheduleID bog'lanishi uzilardi, xatoda esa xom JSON qaytardi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveDeliverySchedule(int orderID, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefault(o => o.ID == orderID && (o.IsDeleted == false || o.IsDeleted == null));

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Formadagi qatorlar (indekslarda bo'shliq bo'lsa ham — kalitlar bo'yicha o'qiladi)
                var posted = new List<DeliveryScheduleItem>();
                foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("ScheduleItems[") && k.EndsWith("].DeliveryDate")))
                {
                    var m = IndexPattern.Match(key);
                    if (!m.Success) continue;
                    var i = m.Groups[1].Value;

                    DateTime deliveryDate;
                    decimal qtyPlanned;
                    int orderProductId;
                    long id;
                    if (!DateTime.TryParseExact(form[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out deliveryDate) &&
                        !DateTime.TryParse(form[key], out deliveryDate))
                        continue;
                    if (!CrmNumber.TryDecimal(form[$"ScheduleItems[{i}].QtyPlanned"], out qtyPlanned) || qtyPlanned <= 0)
                        continue;
                    if (!int.TryParse(form[$"ScheduleItems[{i}].OrderProductID"], out orderProductId))
                        continue;

                    posted.Add(new DeliveryScheduleItem
                    {
                        ID = long.TryParse(form[$"ScheduleItems[{i}].ID"], out id) && id > 0 ? id : (long?)null,
                        SOrderProductID = orderProductId,
                        DeliveryDate = deliveryDate,
                        QtyPlanned = qtyPlanned
                    });
                }

                var existing = db.SORDER_DELIVERY_SCHEDULE.Where(s => s.SOrderID == orderID).ToList();
                var existingById = existing.ToDictionary(s => s.ID);
                var errors = new List<string>();

                foreach (var item in posted)
                {
                    if (order.S_ORDER_PRODUCTS.All(p => p.ID != item.SOrderProductID))
                        errors.Add("Reja qatori buyurtmada yo'q mahsulotga bog'langan.");
                    if (item.ID.HasValue)
                    {
                        SORDER_DELIVERY_SCHEDULE row;
                        if (!existingById.TryGetValue(item.ID.Value, out row))
                            errors.Add("Reja qatori bu buyurtmaga tegishli emas.");
                        else if (item.QtyPlanned < row.QtyShipped)
                            errors.Add($"{row.DeliveryDate:dd.MM.yyyy} rejasida {row.QtyShipped:N2} yetkazilgan — reja miqdori undan kam bo'lishi mumkin emas.");
                    }
                }

                foreach (var g in posted.GroupBy(i => i.SOrderProductID))
                {
                    var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ID == g.Key);
                    var totalPlanned = (double)g.Sum(i => i.QtyPlanned);
                    if (orderProduct != null && totalPlanned > orderProduct.Amount + 0.0001)
                        errors.Add($"{orderProduct.PRODUCT?.PNo} - Rejalashtirilgan miqdor ({totalPlanned:N2}) buyurtma miqdoridan ({orderProduct.Amount:N2}) oshib ketdi");
                }

                var postedIds = new HashSet<long>(posted.Where(i => i.ID.HasValue).Select(i => i.ID.Value));
                var toRemove = existing.Where(s => !postedIds.Contains(s.ID)).ToList();
                if (toRemove.Any())
                {
                    var removeIds = toRemove.Select(s => (long?)s.ID).ToList();
                    var inspected = new HashSet<long?>(db.QUALITY_INSPECTION
                        .Where(q => removeIds.Contains(q.DeliveryScheduleID))
                        .Select(q => q.DeliveryScheduleID)
                        .Distinct()
                        .ToList());
                    foreach (var s in toRemove)
                    {
                        if (s.QtyShipped > 0)
                            errors.Add($"{s.DeliveryDate:dd.MM.yyyy} rejasi bo'yicha yetkazib berilgan — uni o'chirib bo'lmaydi.");
                        else if (inspected.Contains(s.ID))
                            errors.Add($"{s.DeliveryDate:dd.MM.yyyy} rejasiga sifat nazorati bog'langan — uni o'chirib bo'lmaydi.");
                    }
                }

                if (errors.Any())
                {
                    TempData["ErrorMessage"] = string.Join(" ", errors.Distinct());
                    return RedirectToAction("DeliverySchedule", new { orderID });
                }

                try
                {
                    int? userId = RoleHelper.GetUserId(CurrentUserName);
                    foreach (var item in posted)
                    {
                        var productId = order.S_ORDER_PRODUCTS.First(p => p.ID == item.SOrderProductID).ProductID;
                        if (item.ID.HasValue)
                        {
                            var row = existingById[item.ID.Value];
                            row.SOrderProductID = item.SOrderProductID;
                            row.ProductID = productId;
                            row.DeliveryDate = item.DeliveryDate;
                            row.QtyPlanned = item.QtyPlanned;
                            row.UpdatedDT = DateTime.Now;
                        }
                        else
                        {
                            db.SORDER_DELIVERY_SCHEDULE.Add(new SORDER_DELIVERY_SCHEDULE
                            {
                                SOrderID = orderID,
                                SOrderProductID = item.SOrderProductID,
                                ProductID = productId,
                                DeliveryDate = item.DeliveryDate,
                                QtyPlanned = item.QtyPlanned,
                                QtyShipped = 0,
                                StatusCode = "PLANNED",
                                CreatedByUserID = userId,
                                CreatedDT = DateTime.Now
                            });
                        }
                    }

                    db.SORDER_DELIVERY_SCHEDULE.RemoveRange(toRemove);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(CurrentUserName, "SOrderController", $"Yetkazib berish rejasi saqlandi (Buyurtma: {order.OrderNo})");

                    TempData["Success"] = "Yetkazib berish rejasi muvaffaqiyatli saqlandi";
                    return RedirectToAction("Details", new { id = orderID });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Rejani saqlab bo'lmadi: " + InnermostMessage(ex);
                    return RedirectToAction("DeliverySchedule", new { orderID });
                }
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
                    Amount = order.Amount
                };

                SetupFormLookups(db, model);
                model.ProductQuantities = LoadContractProducts(db, order.ContractID);
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

        /// <summary>
        /// Tahrirlash — qatorlar joyida yangilanadi (CRM-B10). Avval hammasi o'chirilib qayta qo'shilardi:
        /// S_ORDER_PRODUCTS.ID o'zgarib, yetkazib berish rejasi "yo'qolardi". Invoys qilingan miqdordan
        /// kamaytirish va rejasi bor qatorni olib tashlash taqiqlanadi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, SOrderViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                model.ID = id;
                model.ProductQuantities = ParseProductQuantities(form);

                if (!model.ProductQuantities.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta maxsulot uchun miqdor kiritish kerak");
                    return FormError(db, model);
                }

                if (!ResolveContractLines(db, model) || !ValidateOrderQuantities(db, model, id) || !ModelState.IsValid)
                {
                    return FormError(db, model);
                }

                try
                {
                    var order = db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS)
                        .FirstOrDefault(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));

                    if (order == null)
                    {
                        return HttpNotFound();
                    }

                    // Qatorlarni moslash: avval shu mahsulot + narx, keyin shu mahsulot
                    var unmatched = order.S_ORDER_PRODUCTS.ToList();
                    var pairs = new List<Tuple<S_ORDER_PRODUCTS, ContractProductQuantity>>();
                    foreach (var q in model.ProductQuantities)
                    {
                        var match = unmatched.FirstOrDefault(l => l.ProductID == q.ProductID && Math.Abs(l.Price - q.PiecePrice) < 0.000001)
                                    ?? unmatched.FirstOrDefault(l => l.ProductID == q.ProductID);
                        if (match != null) unmatched.Remove(match);
                        pairs.Add(Tuple.Create(match, q));
                    }

                    if (!ValidateOrderEditAgainstDocuments(db, order, pairs, unmatched))
                    {
                        return FormError(db, model);
                    }

                    order.OrderNo = model.OrderNo;
                    order.IssuedDate = model.IssuedDate;
                    order.CustomerID = model.CustomerID;
                    order.ContractID = model.ContractID;
                    order.Description = model.Description;
                    var contractCurrencyId = db.S_CONTRACTS.Where(c => c.ID == model.ContractID).Select(c => (int?)c.CurrencyID).FirstOrDefault();
                    if (contractCurrencyId.HasValue)
                    {
                        order.CurrencyID = contractCurrencyId.Value;
                    }

                    var vatByProduct = SContractVatHelper.GetProductVatRates(db, model.ContractID);
                    double totalAmount = 0;
                    foreach (var pair in pairs)
                    {
                        var line = pair.Item1;
                        if (line == null)
                        {
                            line = new S_ORDER_PRODUCTS { OrderID = order.ID, MOQ = 0 };
                            db.S_ORDER_PRODUCTS.Add(line);
                        }
                        totalAmount += ApplyLine(line, pair.Item2, vatByProduct);
                    }

                    db.S_ORDER_PRODUCTS.RemoveRange(unmatched);
                    order.Amount = totalAmount;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(CurrentUserName, "SOrderController", $"{order.OrderNo} - Buyurtmani tahrirladi");

                    return RedirectToAction("Details", new { id = order.ID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
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
                var order = await db.S_ORDERS.FirstOrDefaultAsync(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));
                if (order == null)
                {
                    return HttpNotFound();
                }

                // Faol invoysi bor buyurtmani o'chirish taqiqlanadi (CRM-B10)
                if (await db.SALES_INVOICES.Active().AnyAsync(i => i.OrderID == id))
                {
                    TempData["ErrorMessage"] = "Buyurtmaga faol invoys bog'langan — avval invoysni bekor qiling.";
                    return RedirectToAction("Details", new { id });
                }

                try
                {
                    order.IsDeleted = true;
                    await SoftDeleteReferenceService.AfterSOrderSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();
                    LogHelper.LogToDatabase(CurrentUserName, "SOrderController", $"{order.OrderNo} - Buyurtmani o'chirdi");
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Buyurtmani o'chirishda xatolik: " + InnermostMessage(ex);
                    return RedirectToAction("Details", new { id });
                }
            }
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        /// <summary>
        /// Formadagi "ProductQuantities[i].*" maydonlari (Create va Edit uchun bitta joy — D14).
        /// Narx/birlik formadan olinmaydi — <see cref="ResolveContractLines"/> shartnomadan to'ldiradi (CRM-S03).
        /// </summary>
        private static List<ContractProductQuantity> ParseProductQuantities(FormCollection form)
        {
            var result = new List<ContractProductQuantity>();
            foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("ProductQuantities[") && k.EndsWith("].OrderQuantity")))
            {
                var m = IndexPattern.Match(key);
                if (!m.Success) continue;
                var i = m.Groups[1].Value;

                double quantity;
                int contractProductId, productId;
                if (!CrmNumber.TryDouble(form[key], out quantity) || quantity <= 0) continue;
                if (!int.TryParse(form[$"ProductQuantities[{i}].ContractProductID"], out contractProductId)) continue;
                int.TryParse(form[$"ProductQuantities[{i}].ProductID"], out productId);

                result.Add(new ContractProductQuantity
                {
                    ContractProductID = contractProductId,
                    ProductID = productId,
                    OrderQuantity = quantity
                });
            }
            return result;
        }

        /// <summary>Har qator uchun mahsulot, narx va birlik shartnoma qatoridan olinadi (brauzerdagi yashirin maydonga ishonilmaydi).</summary>
        private bool ResolveContractLines(DBTHSNEntities db, SOrderViewModel model)
        {
            if (model.ContractID <= 0)
            {
                ModelState.AddModelError("", "Shartnoma tanlanmagan yoki noto'g'ri.");
                return false;
            }

            var contractLines = db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(cp => cp.ContractID == model.ContractID)
                .Select(cp => new { cp.ID, cp.ProductID, cp.PiecePrice, cp.UnitID })
                .ToList()
                .ToDictionary(cp => cp.ID);

            foreach (var q in model.ProductQuantities)
            {
                if (!contractLines.TryGetValue(q.ContractProductID, out var cp))
                {
                    ModelState.AddModelError("", "Tanlangan mahsulot shu shartnomaga tegishli emas. Sahifani yangilab qayta urinib ko'ring.");
                    return false;
                }
                q.ProductID = cp.ProductID;
                q.PiecePrice = cp.PiecePrice;
                q.UnitID = cp.UnitID;
            }
            return true;
        }

        /// <summary>Qator maydonlarini to'ldiradi va QQS bilan summani qaytaradi.</summary>
        private static double ApplyLine(S_ORDER_PRODUCTS line, ContractProductQuantity q, IDictionary<int, double> vatByProduct)
        {
            var lineBase = q.PiecePrice * q.OrderQuantity;
            double vatRate;
            vatByProduct.TryGetValue(q.ProductID, out vatRate);

            line.ProductID = q.ProductID;
            line.Price = q.PiecePrice;
            line.Amount = q.OrderQuantity;
            line.UnitID = q.UnitID;
            line.TotalPrice = lineBase;
            return lineBase + SContractVatHelper.CalculateVatAmount(lineBase, vatRate);
        }

        private static List<ContractProductQuantity> LoadContractProducts(DBTHSNEntities db, int contractId)
        {
            return db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(cp => cp.ContractID == contractId)
                .OrderBy(cp => cp.PRODUCT.PNo)
                .Select(cp => new
                {
                    cp.ID,
                    cp.ProductID,
                    PNo = cp.PRODUCT.PNo,
                    Name = cp.PRODUCT.Name,
                    cp.PiecePrice,
                    cp.Amount,
                    cp.UnitID,
                    UnitName = cp.UNIT.ShortName,
                    cp.vatamount
                })
                .ToList()
                .Select(cp => new ContractProductQuantity
                {
                    ContractProductID = cp.ID,
                    ProductID = cp.ProductID,
                    ProductPNo = cp.PNo,
                    ProductName = cp.Name,
                    PiecePrice = cp.PiecePrice,
                    ContractAmount = cp.Amount,
                    UnitID = cp.UnitID,
                    UnitName = cp.UnitName,
                    OrderQuantity = 0,
                    VatRate = SContractVatHelper.GetVatPercentFromLine(
                        SContractVatHelper.GetLineBaseAmount(cp.PiecePrice, cp.Amount), cp.vatamount)
                })
                .ToList();
        }

        private bool ValidateOrderQuantities(DBTHSNEntities db, SOrderViewModel model, int? currentOrderId)
        {
            var productIds = model.ProductQuantities.Select(pq => pq.ProductID).Distinct().ToList();

            // Shartnomada mahsulot takrorlansa — miqdorlar yig'iladi (avval ToDictionary yiqilardi — CRM-B13)
            var contractAmounts = db.S_CONTRACT_PRODUCTS
                .Where(cp => cp.ContractID == model.ContractID)
                .GroupBy(cp => cp.ProductID)
                .Select(g => new { ProductID = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToList()
                .ToDictionary(cp => cp.ProductID, cp => cp.Amount);

            var orderedSumsQuery = db.S_ORDER_PRODUCTS
                .Where(p => p.S_ORDERS.ContractID == model.ContractID &&
                            (p.S_ORDERS.IsDeleted == false || p.S_ORDERS.IsDeleted == null));

            if (currentOrderId.HasValue)
            {
                orderedSumsQuery = orderedSumsQuery.Where(p => p.OrderID != currentOrderId.Value);
            }

            var orderedSums = orderedSumsQuery
                .GroupBy(p => p.ProductID)
                .Select(g => new { ProductID = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList()
                .ToDictionary(x => x.ProductID, x => x.Total);

            var productNames = ProductLabels(db, productIds);

            var hasErrors = false;
            foreach (var g in model.ProductQuantities.GroupBy(q => q.ProductID))
            {
                var label = productNames(g.Key);
                if (!contractAmounts.TryGetValue(g.Key, out var contractAmount))
                {
                    ModelState.AddModelError("", $"{label} uchun shartnoma miqdori topilmadi.");
                    hasErrors = true;
                    continue;
                }

                var alreadyOrdered = orderedSums.ContainsKey(g.Key) ? orderedSums[g.Key] : 0d;
                var remaining = Math.Max(0, contractAmount - alreadyOrdered);
                var requested = g.Sum(q => q.OrderQuantity);

                if (requested > remaining + 0.0001)
                {
                    ModelState.AddModelError("",
                        $"{label} bo'yicha shartnoma miqdori {contractAmount:N2}, " +
                        $"oldin buyurtma {alreadyOrdered:N2}, qolgan {remaining:N2}. " +
                        $"Kiritilgan {requested:N2}.");
                    hasErrors = true;
                }
            }

            return !hasErrors;
        }

        /// <summary>Invoys va yetkazib berish rejasiga zid o'zgarishlarni rad etadi.</summary>
        private bool ValidateOrderEditAgainstDocuments(DBTHSNEntities db, S_ORDERS order,
            List<Tuple<S_ORDER_PRODUCTS, ContractProductQuantity>> pairs, List<S_ORDER_PRODUCTS> removed)
        {
            var invoiced = db.SALES_INVOICE_LINES.Active()
                .Where(l => l.SALES_INVOICES.OrderID == order.ID)
                .GroupBy(l => l.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToList()
                .ToDictionary(x => x.ProductID, x => (double)x.Qty);

            var lineIds = order.S_ORDER_PRODUCTS.Select(l => l.ID).ToList();
            var planned = db.SORDER_DELIVERY_SCHEDULE
                .Where(s => s.SOrderID == order.ID && lineIds.Contains(s.SOrderProductID))
                .GroupBy(s => s.SOrderProductID)
                .Select(g => new { LineID = g.Key, Qty = g.Sum(x => x.QtyPlanned) })
                .ToList()
                .ToDictionary(x => x.LineID, x => (double)x.Qty);

            var names = ProductLabels(db, pairs.Select(p => p.Item2.ProductID).Concat(removed.Select(r => r.ProductID)));
            var newTotals = pairs.GroupBy(p => p.Item2.ProductID).ToDictionary(g => g.Key, g => g.Sum(p => p.Item2.OrderQuantity));
            bool ok = true;

            foreach (var kv in invoiced)
            {
                double newQty;
                newTotals.TryGetValue(kv.Key, out newQty);
                if (newQty + 0.0001 < kv.Value)
                {
                    ModelState.AddModelError("", $"{names(kv.Key)}: {kv.Value:N2} invoys qilingan — buyurtma miqdori undan kam bo'lishi mumkin emas.");
                    ok = false;
                }
            }

            foreach (var pair in pairs.Where(p => p.Item1 != null))
            {
                double plannedQty;
                if (planned.TryGetValue(pair.Item1.ID, out plannedQty) && pair.Item2.OrderQuantity + 0.0001 < plannedQty)
                {
                    ModelState.AddModelError("", $"{names(pair.Item2.ProductID)}: yetkazib berish rejasida {plannedQty:N2} — miqdorni avval rejada kamaytiring.");
                    ok = false;
                }
            }

            foreach (var line in removed)
            {
                if (planned.ContainsKey(line.ID))
                {
                    ModelState.AddModelError("", $"{names(line.ProductID)}: yetkazib berish rejasi bor — qatorni olib tashlashdan oldin rejadan o'chiring.");
                    ok = false;
                }
            }

            return ok;
        }

        private static Func<int, string> ProductLabels(DBTHSNEntities db, IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            var map = db.PRODUCTS.AsNoTracking()
                .Where(p => ids.Contains(p.ID))
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToList()
                .ToDictionary(p => p.ID, p => $"{p.PNo} {p.Name}".Trim());
            return id => map.ContainsKey(id) ? map[id] : $"ProductID={id}";
        }

        /// <summary>
        /// Xato bilan qaytish: ro'yxatlar qayta quriladi, shartnoma mahsulotlari yuklanib,
        /// foydalanuvchi kiritgan miqdorlar saqlanadi (CRM-B11: avval hammasi 0 ga tushardi).
        /// </summary>
        private ActionResult FormError(DBTHSNEntities db, SOrderViewModel model)
        {
            SetupFormLookups(db, model);
            if (model.ContractID > 0)
            {
                var posted = model.ProductQuantities ?? new List<ContractProductQuantity>();
                var reloaded = LoadContractProducts(db, model.ContractID);
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
            ViewBag.Customers = SalesLookups.CustomersWithPlaceholder(model.CustomerID, "0", "---Tanlang---");
            ViewBag.SContract = SalesLookups.ContractsFor(db, model.CustomerID, model.ContractID, "0", "---Tanlang---");
        }
    }
}
