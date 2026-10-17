using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission]
    public class SOrderController : Controller
    {
        // Olish so'rovi: SOrder | Index - List barcha orders
        public async Task<ActionResult> Index(string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.S_ORDERS
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
                    query = query.Where(o => o.IssuedDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(o => o.IssuedDate <= toDate.Value);
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
                    case "date_desc":
                        query = query.OrderByDescending(o => o.IssuedDate);
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

                // Barcha mos yozuvlarni yuklaymiz. Sahifalash (show entries) DataTable
                // tomonidan klient tarafida bajariladi, shu sababli server tomonida
                // Skip/Take bilan cheklamaymiz.
                var orders = await query.ToListAsync();

                // ViewBag uchun filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.Name).ToListAsync(), "ID", "Name", customerID);
                ViewBag.Contracts = new SelectList(await db.S_CONTRACTS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.ContractNo).ToListAsync(), "ID", "ContractNo", contractID);
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

                // Dropdowns
                var customersList = db.CUSTOMERS.Where(x => x.IsDeleted == false || x.IsDeleted == null).OrderBy(c => c.Name).ToList();
                var customersSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
                customersSelectList.AddRange(customersList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }));
                ViewBag.Customers = new SelectList(customersSelectList, "Value", "Text", customerID.HasValue && customerID.Value > 0 ? customerID.Value.ToString() : "0");

                // Faqat tanlangan xaridorning shartnomalarini yuklash
                // Faqat tanlangan xaridorning shartnomalarini yuklash
                var contractsList = new List<S_CONTRACTS>();
                if (customerID.HasValue && customerID.Value > 0)
                {
                    contractsList = db.S_CONTRACTS
                        .Where(x => x.CustomerID == customerID.Value && (x.IsDeleted == false || x.IsDeleted == null))
                        .OrderBy(c => c.ContractNo)
                        .ToList();
                }
                // Agar xaridor tanlanmagan bo'lsa, shartnomalar bo'sh bo'lsin
                // (AJAX orqali xaridor tanlanganda yuklanadi)
                var contractsSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
                if (contractsList.Any())
                {
                    contractsSelectList.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo }));
                }
                ViewBag.SContract = new SelectList(contractsSelectList, "Value", "Text", contractID.HasValue && contractID.Value > 0 ? contractID.Value.ToString() : "0");

                if (customerID.HasValue && customerID.Value > 0)
                {
                    model.CustomerID = customerID.Value;
                }

                if (contractID.HasValue && contractID.Value > 0)
                {
                    model.ContractID = contractID.Value;
                    // Yuklash contract products
                    LoadContractProducts(model, contractID.Value);
                }

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
                // Tahlil qilish product quantities dan form
                model.ProductQuantities = new List<ContractProductQuantity>();
                var keys = form.AllKeys.Where(k => k != null && k.StartsWith("ProductQuantities[") && k.Contains("].OrderQuantity"));
                
                foreach (var key in keys)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(key, @"\[(\d+)\]");
                    if (indexMatch.Success)
                    {
                        var index = int.Parse(indexMatch.Groups[1].Value);
                        var quantityStr = form[key];
                        if (!string.IsNullOrEmpty(quantityStr) && double.TryParse(quantityStr, out double quantity) && quantity > 0)
                        {
                            var contractProductIdStr = form[$"ProductQuantities[{index}].ContractProductID"];
                            var productIdStr = form[$"ProductQuantities[{index}].ProductID"];
                            var priceStr = form[$"ProductQuantities[{index}].PiecePrice"];
                            var unitIdStr = form[$"ProductQuantities[{index}].UnitID"];

                            if (int.TryParse(contractProductIdStr, out int contractProductId) &&
                                int.TryParse(productIdStr, out int productId) &&
                                double.TryParse(priceStr, out double price) &&
                                int.TryParse(unitIdStr, out int unitId))
                            {
                                model.ProductQuantities.Add(new ContractProductQuantity
                                {
                                    ContractProductID = contractProductId,
                                    ProductID = productId,
                                    PiecePrice = price,
                                    OrderQuantity = quantity,
                                    UnitID = unitId
                                });
                            }
                        }
                    }
                }

                // Validation
                if (model.ProductQuantities == null || !model.ProductQuantities.Any())
                {
                    SetupViewBagForError(db, model);
                    ModelState.AddModelError("", "Kamida bitta maxsulot uchun miqdor kiritish kerak");
                    return View(model);
                }

                if (!ValidateOrderQuantities(db, model, null))
                {
                    SetupViewBagForError(db, model);
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    SetupViewBagForError(db, model);
                    return View(model);
                }

                try
                {
                    // Olish CurrencyID dan contract
                    int currencyID = 0;
                    Dictionary<int, double> vatByProduct = null;
                    if (model.ContractID > 0)
                    {
                        var contract = db.S_CONTRACTS.FirstOrDefault(c => c.ID == model.ContractID);
                        if (contract != null)
                        {
                            currencyID = contract.CurrencyID;
                        }

                        vatByProduct = db.S_CONTRACT_PRODUCTS
                            .Where(p => p.ContractID == model.ContractID)
                            .ToList()
                            .ToDictionary(
                                p => p.ProductID,
                                p => SContractVatHelper.GetVatPercentFromLine(
                                    SContractVatHelper.GetLineBaseAmount(p), p.vatamount));
                    }

                    // Yaratish order
                    var order = new S_ORDERS
                    {
                        OrderNo = model.OrderNo,
                        IssuedDate = model.IssuedDate,
                        CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                        CustomerID = model.CustomerID,
                        ContractID = model.ContractID,
                        CurrencyID = currencyID,
                        Description = model.Description,
                        IsDeleted = false,
                        Amount = 0
                    };

                    db.S_ORDERS.Add(order);
                    db.SaveChanges();

                    // Qo'shish products
                    double totalAmount = 0;
                    foreach (var productQty in model.ProductQuantities)
                    {
                        var lineBase = productQty.PiecePrice * productQty.OrderQuantity;
                        var vatRate = vatByProduct != null && vatByProduct.ContainsKey(productQty.ProductID)
                            ? vatByProduct[productQty.ProductID]
                            : 0d;
                        var orderProduct = new S_ORDER_PRODUCTS
                        {
                            OrderID = order.ID,
                            ProductID = productQty.ProductID,
                            Price = productQty.PiecePrice,
                            Amount = productQty.OrderQuantity,
                            MOQ = 0,
                            UnitID = productQty.UnitID,
                            TotalPrice = lineBase
                        };
                        totalAmount += lineBase + SContractVatHelper.CalculateVatAmount(lineBase, vatRate);
                        db.S_ORDER_PRODUCTS.Add(orderProduct);
                    }

                    order.Amount = totalAmount;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani yaratdi");

                    return RedirectToAction("DeliverySchedule", new { orderID = order.ID });
                }
                catch (Exception ex)
                {
                    SetupViewBagForError(db, model);
                    var errorMessage = $"Xatolik: {ex.Message}";
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    ModelState.AddModelError("", errorMessage);
                    return View(model);
                }
            }
        }

        // Olish so'rovi: GetContractsByCustomer - AJAX
        [HttpGet]
        public ActionResult GetContractsByCustomer(int customerId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contracts = db.S_CONTRACTS
                    .Where(c => c.CustomerID == customerId && (c.IsDeleted == false || c.IsDeleted == null))
                    .OrderBy(c => c.ContractNo)
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList();

                var contractIds = contracts.Select(c => c.ID).ToList();
                var contractProducts = db.S_CONTRACT_PRODUCTS
                    .Where(p => contractIds.Contains(p.ContractID))
                    .ToList()
                    .Select(p => new SContractVatHelper.ContractProductVatLine
                    {
                        ContractID = p.ContractID,
                        LineBaseAmount = SContractVatHelper.GetLineBaseAmount(p),
                        VATAmount = p.vatamount
                    })
                    .ToList();

                var vatByContract = SContractVatHelper.BuildContractVatRates(contractProducts);

                var result = contracts.Select(c => new
                {
                    ID = c.ID,
                    ContractNo = c.ContractNo,
                    VatRate = vatByContract.ContainsKey(c.ID) ? vatByContract[c.ID] : 0d
                }).ToList();

                return Json(result, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: GetContractProducts - AJAX
        [HttpGet]
        public ActionResult GetContractProducts(int contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProducts = db.S_CONTRACT_PRODUCTS
                    .Include(cp => cp.PRODUCT)
                    .Include(cp => cp.UNIT)
                    .Where(cp => cp.ContractID == contractId)
                    .OrderBy(cp => cp.PRODUCT.PNo)
                    .ToList()
                    .Select(cp => new
                    {
                        ContractProductID = cp.ID,
                        ProductID = cp.ProductID,
                        ProductPNo = cp.PRODUCT.PNo,
                        ProductName = cp.PRODUCT.Name,
                        PiecePrice = cp.PiecePrice,
                        ContractAmount = cp.Amount,
                        UnitID = cp.UnitID,
                        UnitName = cp.UNIT.ShortName,
                        VatRate = SContractVatHelper.GetVatPercentFromLine(
                            SContractVatHelper.GetLineBaseAmount(cp), cp.vatamount)
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

                // Olish order products
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

                // Calculate planned va shipped quantities
                var scheduleItems = db.SORDER_DELIVERY_SCHEDULE
                    .Where(s => s.SOrderID == orderID)
                    .ToList();

                foreach (var orderProduct in model.OrderProducts)
                {
                    var productSchedules = scheduleItems.Where(s => s.SOrderProductID == orderProduct.SOrderProductID).ToList();
                    orderProduct.PlannedQuantity = (double)productSchedules.Sum(s => s.QtyPlanned);
                    orderProduct.ShippedQuantity = (double)productSchedules.Sum(s => s.QtyShipped);
                }

                // Olish existing schedule items
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

        // POST so'rovi: SaveDeliverySchedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveDeliverySchedule(int orderID, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var order = db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS)
                        .FirstOrDefault(o => o.ID == orderID && (o.IsDeleted == false || o.IsDeleted == null));

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Buyurtma topilmadi" });
                    }

                    // Tahlil qilish schedule items dan form
                    var scheduleItems = new List<DeliveryScheduleItem>();
                    var keys = form.AllKeys.Where(k => k != null && k.StartsWith("ScheduleItems[") && k.Contains("].DeliveryDate"));

                    foreach (var key in keys)
                    {
                        var indexMatch = System.Text.RegularExpressions.Regex.Match(key, @"\[(\d+)\]");
                        if (indexMatch.Success)
                        {
                            var index = int.Parse(indexMatch.Groups[1].Value);
                            var dateStr = form[key];
                            var qtyStr = form[$"ScheduleItems[{index}].QtyPlanned"];
                            var productIdStr = form[$"ScheduleItems[{index}].OrderProductID"];

                            if (DateTime.TryParse(dateStr, out DateTime deliveryDate) &&
                                decimal.TryParse(qtyStr, out decimal qtyPlanned) &&
                                int.TryParse(productIdStr, out int orderProductID) &&
                                qtyPlanned > 0)
                            {
                                scheduleItems.Add(new DeliveryScheduleItem
                                {
                                    SOrderProductID = orderProductID,
                                    DeliveryDate = deliveryDate,
                                    QtyPlanned = qtyPlanned
                                });
                            }
                        }
                    }

                    // Tekshirish quantities don't exceed order quantities
                    foreach (var item in scheduleItems.GroupBy(i => i.SOrderProductID))
                    {
                        var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ID == item.Key);
                        if (orderProduct != null)
                        {
                            var totalPlanned = (double)item.Sum(i => i.QtyPlanned);
                            if (totalPlanned > orderProduct.Amount)
                            {
                                return Json(new { success = false, message = $"{orderProduct.PRODUCT?.PNo} - Rejalashtirilgan miqdor ({totalPlanned}) buyurtma miqdoridan ({orderProduct.Amount}) oshib ketdi" });
                            }
                        }
                    }

                    // O'chirish existing schedule items
                    var existingSchedules = db.SORDER_DELIVERY_SCHEDULE.Where(s => s.SOrderID == orderID).ToList();
                    foreach (var existing in existingSchedules)
                    {
                        db.SORDER_DELIVERY_SCHEDULE.Remove(existing);
                    }

                    // Qo'shish new schedule items
                    foreach (var item in scheduleItems)
                    {
                        var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ID == item.SOrderProductID);
                        if (orderProduct != null)
                        {
                            var scheduleItem = new SORDER_DELIVERY_SCHEDULE
                            {
                                SOrderID = orderID,
                                SOrderProductID = item.SOrderProductID,
                                ProductID = orderProduct.ProductID,
                                DeliveryDate = item.DeliveryDate,
                                QtyPlanned = item.QtyPlanned,
                                QtyShipped = 0,
                                StatusCode = "PLANNED",
                                CreatedByUserID = null, // Set agar you have user ID
                                CreatedDT = DateTime.Now
                            };
                            db.SORDER_DELIVERY_SCHEDULE.Add(scheduleItem);
                        }
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"Yetkazib berish rejasi saqlandi (Buyurtma: {order.OrderNo})");

                    TempData["Success"] = "Yetkazib berish rejasi muvaffaqiyatli saqlandi";
                    return RedirectToAction("Details", new { id = orderID });
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Xatolik: {ex.Message}";
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        errorMessage += $" | {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    return Json(new { success = false, message = errorMessage });
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

                var scheduleSummary = deliverySchedule
                    .GroupBy(d => d.SOrderProductID)
                    .Select(g => new
                    {
                        SOrderProductID = g.Key,
                        TotalPlanned = g.Sum(d => d.QtyPlanned),
                        TotalShipped = g.Sum(d => d.QtyShipped)
                    })
                    .ToDictionary(s => s.SOrderProductID, s => s);

                ViewBag.ScheduleSummary = scheduleSummary;
                ViewBag.InvoicesCount = db.SALES_INVOICES.Where(i => i.OrderID == id).Count();
                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db,order.ContractID > 0 ? (int?)order.ContractID : null);
                ViewBag.ProductVatRates = BuildProductVatRates(db, order.ContractID);

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

                SetupViewBagForEdit(db, model);
                LoadContractProducts(model, order.ContractID);
                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db, order.ContractID > 0 ? (int?)order.ContractID : null);

                // Belgilash quantities dan existing order products
                foreach (var orderProduct in order.S_ORDER_PRODUCTS)
                {
                    var contractProduct = model.ProductQuantities.FirstOrDefault(pq => pq.ProductID == orderProduct.ProductID);
                    if (contractProduct != null)
                    {
                        contractProduct.OrderQuantity = orderProduct.Amount;
                    }
                }

                return View(model);
            }
        }

        // POST so'rovi: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, SOrderViewModel model, FormCollection form)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tahlil qilish product quantities
                model.ProductQuantities = new List<ContractProductQuantity>();
                var keys = form.AllKeys.Where(k => k != null && k.StartsWith("ProductQuantities[") && k.Contains("].OrderQuantity"));

                foreach (var key in keys)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(key, @"\[(\d+)\]");
                    if (indexMatch.Success)
                    {
                        var index = int.Parse(indexMatch.Groups[1].Value);
                        var quantityStr = form[key];
                        if (!string.IsNullOrEmpty(quantityStr) && double.TryParse(quantityStr, out double quantity) && quantity > 0)
                        {
                            var contractProductIdStr = form[$"ProductQuantities[{index}].ContractProductID"];
                            var productIdStr = form[$"ProductQuantities[{index}].ProductID"];
                            var priceStr = form[$"ProductQuantities[{index}].PiecePrice"];
                            var unitIdStr = form[$"ProductQuantities[{index}].UnitID"];

                            if (int.TryParse(contractProductIdStr, out int contractProductId) &&
                                int.TryParse(productIdStr, out int productId) &&
                                double.TryParse(priceStr, out double price) &&
                                int.TryParse(unitIdStr, out int unitId))
                            {
                                model.ProductQuantities.Add(new ContractProductQuantity
                                {
                                    ContractProductID = contractProductId,
                                    ProductID = productId,
                                    PiecePrice = price,
                                    OrderQuantity = quantity,
                                    UnitID = unitId
                                });
                            }
                        }
                    }
                }

                if (model.ProductQuantities == null || !model.ProductQuantities.Any())
                {
                    SetupViewBagForError(db, model);
                    ModelState.AddModelError("", "Kamida bitta maxsulot uchun miqdor kiritish kerak");
                    return View(model);
                }

                if (!ValidateOrderQuantities(db, model, id))
                {
                    SetupViewBagForError(db, model);
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    SetupViewBagForError(db, model);
                    return View(model);
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

                    order.OrderNo = model.OrderNo;
                    order.IssuedDate = model.IssuedDate;
                    order.CustomerID = model.CustomerID;
                    order.ContractID = model.ContractID;
                    order.Description = model.Description;

                    // Olish CurrencyID dan contract
                    if (model.ContractID > 0)
                    {
                        var contract = db.S_CONTRACTS.FirstOrDefault(c => c.ID == model.ContractID);
                        if (contract != null)
                        {
                            order.CurrencyID = contract.CurrencyID;
                        }
                    }

                    // O'chirish existing products
                    var existingProducts = db.S_ORDER_PRODUCTS.Where(p => p.OrderID == id).ToList();
                    foreach (var product in existingProducts)
                    {
                        db.S_ORDER_PRODUCTS.Remove(product);
                    }

                    // Qo'shish updated products
                    var vatByProduct = model.ContractID > 0
                        ? db.S_CONTRACT_PRODUCTS
                            .Where(p => p.ContractID == model.ContractID)
                            .ToList()
                            .ToDictionary(
                                p => p.ProductID,
                                p => SContractVatHelper.GetVatPercentFromLine(
                                    SContractVatHelper.GetLineBaseAmount(p), p.vatamount))
                        : new Dictionary<int, double>();

                    double totalAmount = 0;
                    foreach (var productQty in model.ProductQuantities)
                    {
                        var lineBase = productQty.PiecePrice * productQty.OrderQuantity;
                        var vatRate = vatByProduct.ContainsKey(productQty.ProductID)
                            ? vatByProduct[productQty.ProductID]
                            : 0d;
                        var orderProduct = new S_ORDER_PRODUCTS
                        {
                            OrderID = order.ID,
                            ProductID = productQty.ProductID,
                            Price = productQty.PiecePrice,
                            Amount = productQty.OrderQuantity,
                            MOQ = 0,
                            UnitID = productQty.UnitID,
                            TotalPrice = lineBase
                        };
                        totalAmount += lineBase + SContractVatHelper.CalculateVatAmount(lineBase, vatRate);
                        db.S_ORDER_PRODUCTS.Add(orderProduct);
                    }

                    order.Amount = totalAmount;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani tahrirladi");

                    return RedirectToAction("Details", new { id = order.ID });
                }
                catch (Exception ex)
                {
                    SetupViewBagForError(db, model);
                    var errorMessage = $"Xatolik: {ex.Message}";
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    ModelState.AddModelError("", errorMessage);
                    return View(model);
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
                    .Include(o => o.S_ORDER_PRODUCTS)
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefault(o => o.ID == id && (o.IsDeleted == false || o.IsDeleted == null));

                if (order == null)
                {
                    return HttpNotFound();
                }

                ViewBag.ContractVatRate = SContractVatHelper.CalculateContractVatRate(db, order.ContractID > 0 ? (int?)order.ContractID : null);
                ViewBag.ProductVatRates = BuildProductVatRates(db, order.ContractID);

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

                try
                {
                    order.IsDeleted = true;
                    await SoftDeleteReferenceService.AfterSOrderSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();
                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani o'chirdi");
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Buyurtmani o'chirishda xatolik: {ex.Message}";
                    return RedirectToAction("Details", new { id });
                }
            }
        }

        // yordamchi methods

        // Shartnoma maxsulotlari bo'yicha QQS foizini (ProductID -> VAT %) qaytaradi.
        // Agar shartnomada QQS 0 bo'lsa, foiz ham 0 bo'ladi (narxlar QQSsiz ko'rsatiladi).
        private Dictionary<int, double> BuildProductVatRates(DBTHSNEntities db, int contractId)
        {
            if (contractId <= 0)
            {
                return new Dictionary<int, double>();
            }

            return db.S_CONTRACT_PRODUCTS
                .Where(p => p.ContractID == contractId)
                .ToList()
                .GroupBy(p => p.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => SContractVatHelper.GetVatPercentFromLine(
                        g.Sum(p => SContractVatHelper.GetLineBaseAmount(p)),
                        g.Sum(p => p.vatamount ?? 0)));
        }

        private void LoadContractProducts(SOrderViewModel model, int contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProducts = db.S_CONTRACT_PRODUCTS
                    .Include(cp => cp.PRODUCT)
                    .Include(cp => cp.UNIT)
                    .Where(cp => cp.ContractID == contractId)
                    .OrderBy(cp => cp.PRODUCT.PNo)
                    .ToList();

                model.ProductQuantities = contractProducts.Select(cp => new ContractProductQuantity
                {
                    ContractProductID = cp.ID,
                    ProductID = cp.ProductID,
                    ProductPNo = cp.PRODUCT.PNo,
                    ProductName = cp.PRODUCT.Name,
                    PiecePrice = cp.PiecePrice,
                    ContractAmount = cp.Amount,
                    UnitID = cp.UnitID,
                    UnitName = cp.UNIT.ShortName,
                    OrderQuantity = 0,
                    VatRate = SContractVatHelper.GetVatPercentFromLine(
                        SContractVatHelper.GetLineBaseAmount(cp), cp.vatamount)
                }).ToList();
            }
        }

        private bool ValidateOrderQuantities(DBTHSNEntities db, SOrderViewModel model, int? currentOrderId)
        {
            if (model == null || model.ContractID <= 0)
            {
                ModelState.AddModelError("", "Shartnoma tanlanmagan yoki noto'g'ri.");
                return false;
            }

            var productIds = model.ProductQuantities.Select(pq => pq.ProductID).Distinct().ToList();
            if (!productIds.Any())
            {
                ModelState.AddModelError("", "Maxsulotlar topilmadi.");
                return false;
            }

            var contractProducts = db.S_CONTRACT_PRODUCTS
                .Where(cp => cp.ContractID == model.ContractID)
                .Select(cp => new { cp.ProductID, cp.Amount })
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

            var productNames = db.PRODUCTS
                .Where(p => productIds.Contains(p.ID))
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToList()
                .ToDictionary(p => p.ID, p => $"{p.PNo} {p.Name}".Trim());

            var hasErrors = false;
            foreach (var item in model.ProductQuantities)
            {
                if (!contractProducts.TryGetValue(item.ProductID, out var contractAmount))
                {
                    var labelMissing = productNames.ContainsKey(item.ProductID) ? productNames[item.ProductID] : $"ProductID={item.ProductID}";
                    ModelState.AddModelError("", $"{labelMissing} uchun shartnoma miqdori topilmadi.");
                    hasErrors = true;
                    continue;
                }

                var alreadyOrdered = orderedSums.ContainsKey(item.ProductID) ? orderedSums[item.ProductID] : 0d;
                var remaining = contractAmount - alreadyOrdered;
                if (remaining < 0)
                {
                    remaining = 0;
                }

                if (item.OrderQuantity > remaining + 0.0001)
                {
                    var label = productNames.ContainsKey(item.ProductID) ? productNames[item.ProductID] : $"ProductID={item.ProductID}";
                    ModelState.AddModelError("",
                        $"{label} bo'yicha shartnoma miqdori {contractAmount:N2}, " +
                        $"oldin buyurtma {alreadyOrdered:N2}, qolgan {remaining:N2}. " +
                        $"Kiritilgan {item.OrderQuantity:N2}.");
                    hasErrors = true;
                }
            }

            return !hasErrors;
        }

        private void SetupViewBagForError(DBTHSNEntities db, SOrderViewModel model)
        {
            var customersList = db.CUSTOMERS.Where(x => x.IsDeleted == false || x.IsDeleted == null).OrderBy(c => c.Name).ToList();
            var customersSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
            customersSelectList.AddRange(customersList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }));
            ViewBag.Customers = new SelectList(customersSelectList, "Value", "Text", model.CustomerID > 0 ? model.CustomerID.ToString() : "0");

            var contractsList = db.S_CONTRACTS
                .Where(x => (x.CustomerID == model.CustomerID || model.CustomerID == 0) && (x.IsDeleted == false || x.IsDeleted == null))
                .OrderBy(c => c.ContractNo)
                .ToList();
            var contractsSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
            contractsSelectList.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo }));
            ViewBag.SContract = new SelectList(contractsSelectList, "Value", "Text", model.ContractID > 0 ? model.ContractID.ToString() : "0");

            if (model.ContractID > 0)
            {
                LoadContractProducts(model, model.ContractID);
            }
        }

        private void SetupViewBagForEdit(DBTHSNEntities db, SOrderViewModel model)
        {
            var customersList = db.CUSTOMERS.Where(x => x.IsDeleted == false || x.IsDeleted == null).OrderBy(c => c.Name).ToList();
            var customersSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
            customersSelectList.AddRange(customersList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }));
            ViewBag.Customers = new SelectList(customersSelectList, "Value", "Text", model.CustomerID > 0 ? model.CustomerID.ToString() : "0");

            var contractsList = db.S_CONTRACTS
                .Where(x => (x.CustomerID == model.CustomerID || model.CustomerID == 0) && (x.IsDeleted == false || x.IsDeleted == null))
                .OrderBy(c => c.ContractNo)
                .ToList();
            var contractsSelectList = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "---Tanlang---" } };
            contractsSelectList.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo }));
            ViewBag.SContract = new SelectList(contractsSelectList, "Value", "Text", model.ContractID > 0 ? model.ContractID.ToString() : "0");
        }
    }
}
