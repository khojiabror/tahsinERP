using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class SOrderController : Controller
    {
        // GET: SOrder
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string orderNo, int? customerID, int? contractID, DateTime? fromDate, DateTime? toDate, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.S_CONTRACTS)
                    .Include(o => o.CURRENCy1)
                    .Where(o => o.IsDeleted == false);

                // Filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(o => 
                        o.OrderNo.Contains(searchTerm) || 
                        (o.Description != null && o.Description.Contains(searchTerm)));
                }

                if (!string.IsNullOrEmpty(orderNo))
                {
                    query = query.Where(o => o.OrderNo.Contains(orderNo));
                }

                if (customerID.HasValue)
                {
                    query = query.Where(o => o.CustomerID == customerID.Value);
                }

                if (contractID.HasValue)
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
                    default:
                        query = query.OrderByDescending(o => o.IssuedDate);
                        break;
                }

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var orders = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // ViewBag for filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
                ViewBag.Contracts = new SelectList(await db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
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

        // GET: Details
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
                    .FirstOrDefault(o => o.ID == id && o.IsDeleted == false);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Get delivery schedule
                var deliverySchedule = db.SORDER_DELIVERY_SCHEDULE
                    .Include(d => d.PRODUCT)
                    .Where(d => d.SOrderID == id)
                    .OrderBy(d => d.DeliveryDate)
                    .ToList();

                ViewBag.DeliverySchedule = deliverySchedule;

                // Calculate delivery schedule summary per product
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

                // Get invoices count
                var invoicesCount = db.SALES_INVOICES.Where(i => i.OrderID == id).Count();
                ViewBag.InvoicesCount = invoicesCount;

                return View(order);
            }
        }

        // GET: Create
        [HttpGet]
        public ActionResult Create(int? customerID, int? contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = new SOrderViewModel
                {
                    IssuedDate = DateTime.Now,
                    Products = new List<SOrderProduct>()
                };

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", customerID);
                ViewBag.SContract = new SelectList(db.S_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo", contractID);
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsFinishedGood == true).ToList(), "ID", "PNo");

                if (customerID.HasValue)
                {
                    model.CustomerID = customerID.Value;
                }

                if (contractID.HasValue)
                {
                    model.ContractID = contractID.Value;
                }

                return View(model);
            }
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SOrderViewModel model, FormCollection form)
        {
            // Handle repeater form data (Parts[0].ProductID, Parts[1].ProductID, etc.)
            if (model.Products == null || !model.Products.Any())
            {
                var parts = new List<SOrderProduct>();
                int index = 0;
                while (form[$"Parts[{index}].ProductID"] != null)
                {
                    var productID = int.Parse(form[$"Parts[{index}].ProductID"]);
                    var price = float.Parse(form[$"Parts[{index}].Price"] ?? "0");
                    var amount = float.Parse(form[$"Parts[{index}].Amount"] ?? "0");
                    var moq = float.Parse(form[$"Parts[{index}].MOQ"] ?? "0");
                    var unitID = int.Parse(form[$"Parts[{index}].UnitID"]);

                    parts.Add(new SOrderProduct
                    {
                        ProductID = productID,
                        Price = price,
                        Amount = amount,
                        MOQ = moq,
                        UnitID = unitID
                    });
                    index++;
                }
                model.Products = parts;
            }

            if (ModelState.IsValid && model.Products != null && model.Products.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Get CurrencyID from contract if contract is selected
                        int currencyID = 0;
                        if (model.ContractID > 0)
                        {
                            var contract = db.S_CONTRACTS.FirstOrDefault(c => c.ID == model.ContractID);
                            if (contract != null)
                            {
                                currencyID = contract.CurrencyID;
                            }
                        }

                        var order = new S_ORDERS
                        {
                            OrderNo = model.OrderNo,
                            IssuedDate = model.IssuedDate,
                            CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                            CustomerID = model.CustomerID,
                            ContractID = model.ContractID > 0 ? model.ContractID : 0,
                            CurrencyID = currencyID,
                            Description = model.Description,
                            IsDeleted = false,
                            Amount = 0
                        };

                        db.S_ORDERS.Add(order);
                        db.SaveChanges();

                        // Add products
                        double totalAmount = 0;
                        foreach (var product in model.Products)
                        {
                            var orderProduct = new S_ORDER_PRODUCTS
                            {
                                OrderID = order.ID,
                                ProductID = product.ProductID,
                                Price = product.Price,
                                Amount = product.Amount,
                                MOQ = 0,
                                UnitID = product.UnitID,
                                TotalPrice = product.Price * product.Amount
                            };
                            totalAmount += orderProduct.TotalPrice;
                            db.S_ORDER_PRODUCTS.Add(orderProduct);
                        }

                        order.Amount = totalAmount;
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani yaratdi");

                        return RedirectToAction("DeliverySchedule", new { orderID = order.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }
            else
            {
                if (model.Products == null || !model.Products.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta mahsulot qo'shish kerak");
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                ViewBag.SContract = new SelectList(db.S_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo", model.ContractID);
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
            }

            return View(model);
        }

        // GET: Edit
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
                    .FirstOrDefault(o => o.ID == id && o.IsDeleted == false);

                if (order == null)
                {
                    return HttpNotFound();
                }

                var model = new SOrderViewModel
                {
                    OrderNo = order.OrderNo,
                    IssuedDate = order.IssuedDate,
                    CustomerID = order.CustomerID,
                    ContractID = order.ContractID,
                    CurrencyID = order.CurrencyID,
                    Description = order.Description,
                    Products = order.S_ORDER_PRODUCTS.Select(p => new SOrderProduct
                    {
                        ProductID = p.ProductID,
                        Price = (float)p.Price,
                        Amount = (float)p.Amount,
                        MOQ = (float)(p.MOQ ?? 0),
                        UnitID = p.UnitID
                    }).ToList()
                };

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", order.CustomerID);
                ViewBag.SContract = new SelectList(db.S_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo", order.ContractID);
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", order.CurrencyID);

                return View(model);
            }
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, SOrderViewModel model, FormCollection form)
        {
            // Handle repeater form data (Parts[0].ProductID, Parts[1].ProductID, etc.)
            if (model.Products == null || !model.Products.Any())
            {
                var parts = new List<SOrderProduct>();
                int index = 0;
                while (form[$"Parts[{index}].ProductID"] != null)
                {
                    var productID = int.Parse(form[$"Parts[{index}].ProductID"]);
                    var price = float.Parse(form[$"Parts[{index}].Price"] ?? "0");
                    var amount = float.Parse(form[$"Parts[{index}].Amount"] ?? "0");
                    var moq = float.Parse(form[$"Parts[{index}].MOQ"] ?? "0");
                    var unitID = int.Parse(form[$"Parts[{index}].UnitID"]);

                    parts.Add(new SOrderProduct
                    {
                        ProductID = productID,
                        Price = price,
                        Amount = amount,
                        MOQ = moq,
                        UnitID = unitID
                    });
                    index++;
                }
                model.Products = parts;
            }

            if (ModelState.IsValid && model.Products != null && model.Products.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var order = db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS)
                        .FirstOrDefault(o => o.ID == id && o.IsDeleted == false);

                    if (order == null)
                    {
                        return HttpNotFound();
                    }

                    try
                    {
                        order.OrderNo = model.OrderNo;
                        order.IssuedDate = model.IssuedDate;
                        order.CustomerID = model.CustomerID;
                        order.ContractID = model.ContractID > 0 ? model.ContractID : 0;
                        
                        // Get CurrencyID from contract if contract is selected
                        if (model.ContractID > 0)
                        {
                            var contract = db.S_CONTRACTS.FirstOrDefault(c => c.ID == model.ContractID);
                            if (contract != null)
                            {
                                order.CurrencyID = contract.CurrencyID;
                            }
                        }
                        
                        order.Description = model.Description;

                        // Remove existing products
                        var existingProducts = db.S_ORDER_PRODUCTS.Where(p => p.OrderID == id).ToList();
                        foreach (var product in existingProducts)
                        {
                            db.S_ORDER_PRODUCTS.Remove(product);
                        }

                        // Add updated products
                        double totalAmount = 0;
                        foreach (var product in model.Products)
                        {
                            var orderProduct = new S_ORDER_PRODUCTS
                            {
                                OrderID = order.ID,
                                ProductID = product.ProductID,
                                Price = product.Price,
                                Amount = product.Amount,
                                MOQ = 0,
                                UnitID = product.UnitID,
                                TotalPrice = product.Price * product.Amount
                            };
                            totalAmount += orderProduct.TotalPrice;
                            db.S_ORDER_PRODUCTS.Add(orderProduct);
                        }

                        order.Amount = totalAmount;
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani tahrirladi");

                        return RedirectToAction("Details", new { id = order.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }
            else
            {
                if (model.Products == null || !model.Products.Any())
                {
                    ModelState.AddModelError("", "Kamida bitta mahsulot qo'shish kerak");
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name", model.CustomerID);
                ViewBag.SContract = new SelectList(db.S_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo", model.ContractID);
                ViewBag.Units = new SelectList(db.UNITS.ToList(), "ID", "ShortName");
                ViewBag.ProductList = new SelectList(db.PRODUCTS.Where(x => x.IsDeleted == false && x.IsFinishedGood == true).ToList(), "ID", "PNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", model.CurrencyID);
            }

            return View(model);
        }

        // GET: Delete
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
                    .Include(o => o.S_ORDER_PRODUCTS)
                    .FirstOrDefault(o => o.ID == id && o.IsDeleted == false);

                if (order == null)
                {
                    return HttpNotFound();
                }

                return View(order);
            }
        }

        // POST: Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS.Find(id);
                if (order != null)
                {
                    order.IsDeleted = true;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"{order.OrderNo} - Buyurtmani o'chirdi");
                }

                return RedirectToAction("Index");
            }
        }

        // Manage Delivery Schedule
        public ActionResult DeliverySchedule(int? orderID)
        {
            if (orderID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .Include(o => o.CUSTOMER)
                    .FirstOrDefault(o => o.ID == orderID && o.IsDeleted == false);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Load delivery schedule
                var deliverySchedule = db.SORDER_DELIVERY_SCHEDULE
                    .Include(d => d.PRODUCT)
                    .Where(d => d.SOrderID == orderID)
                    .OrderBy(d => d.DeliveryDate)
                    .ThenBy(d => d.SOrderProductID)
                    .ToList();

                ViewBag.OrderProducts = order.S_ORDER_PRODUCTS.ToList();
                ViewBag.DeliverySchedule = deliverySchedule;

                // Calculate totals per product
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

                return View(order);
            }
        }

        // POST: Add Delivery Schedule Item
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDeliveryScheduleItem(int orderID, int orderProductID, DateTime deliveryDate, decimal qtyPlanned)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var orderProduct = db.S_ORDER_PRODUCTS.Find(orderProductID);
                    if (orderProduct == null || orderProduct.OrderID != orderID)
                    {
                        return Json(new { success = false, message = "Buyurtma mahsuloti topilmadi" });
                    }

                    // Validate total planned quantity
                    var existingPlanned = db.SORDER_DELIVERY_SCHEDULE
                        .Where(d => d.SOrderProductID == orderProductID)
                        .Sum(d => (decimal?)d.QtyPlanned) ?? 0;

                    if (existingPlanned + qtyPlanned > (decimal)orderProduct.Amount)
                    {
                        return Json(new { success = false, message = $"Jami rejalashtirilgan miqdor ({existingPlanned + qtyPlanned}) buyurtma miqdoridan ({orderProduct.Amount}) oshib ketdi" });
                    }

                    var scheduleItem = new SORDER_DELIVERY_SCHEDULE
                    {
                        SOrderID = orderID,
                        SOrderProductID = orderProductID,
                        ProductID = orderProduct.ProductID,
                        DeliveryDate = deliveryDate,
                        QtyPlanned = qtyPlanned,
                        QtyShipped = 0,
                        StatusCode = "PLANNED",
                        CreatedDT = DateTime.Now
                    };

                    db.SORDER_DELIVERY_SCHEDULE.Add(scheduleItem);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"Yetkazib berish rejasi qo'shildi (Buyurtma: {orderID})");

                    return Json(new { success = true, message = "Muvaffaqiyatli qo'shildi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // POST: Update Delivery Schedule Item
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateDeliveryScheduleItem(long scheduleID, DateTime deliveryDate, decimal qtyPlanned, decimal qtyShipped, string statusCode)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var scheduleItem = db.SORDER_DELIVERY_SCHEDULE.Find(scheduleID);
                    if (scheduleItem == null)
                    {
                        return Json(new { success = false, message = "Reja topilmadi" });
                    }

                    // Validate total planned quantity
                    var orderProduct = db.S_ORDER_PRODUCTS.Find(scheduleItem.SOrderProductID);
                    if (orderProduct != null)
                    {
                        var existingPlanned = db.SORDER_DELIVERY_SCHEDULE
                            .Where(d => d.SOrderProductID == scheduleItem.SOrderProductID && d.ID != scheduleID)
                            .Sum(d => (decimal?)d.QtyPlanned) ?? 0;

                        if (existingPlanned + qtyPlanned > (decimal)orderProduct.Amount)
                        {
                            return Json(new { success = false, message = $"Jami rejalashtirilgan miqdor buyurtma miqdoridan oshib ketdi" });
                        }
                    }

                    scheduleItem.DeliveryDate = deliveryDate;
                    scheduleItem.QtyPlanned = qtyPlanned;
                    scheduleItem.QtyShipped = qtyShipped;
                    scheduleItem.StatusCode = statusCode;
                    scheduleItem.UpdatedDT = DateTime.Now;

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"Yetkazib berish rejasi yangilandi (ID: {scheduleID})");

                    return Json(new { success = true, message = "Muvaffaqiyatli yangilandi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // POST: Delete Delivery Schedule Item
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDeliveryScheduleItem(long scheduleID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    var scheduleItem = db.SORDER_DELIVERY_SCHEDULE.Find(scheduleID);
                    if (scheduleItem != null)
                    {
                        db.SORDER_DELIVERY_SCHEDULE.Remove(scheduleItem);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SOrderController", $"Yetkazib berish rejasi o'chirildi (ID: {scheduleID})");

                        return Json(new { success = true, message = "Muvaffaqiyatli o'chirildi" });
                    }

                    return Json(new { success = false, message = "Reja topilmadi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // GET: GetContractProducts - AJAX endpoint to get contract products
        [HttpGet]
        public ActionResult GetContractProducts(int contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractProducts = db.S_CONTRACT_PRODUCTS
                    .Include(cp => cp.PRODUCT)
                    .Where(cp => cp.ContractID == contractId)
                    .Select(cp => new
                    {
                        ProductID = cp.ProductID,
                        ProductPNo = cp.PRODUCT.PNo,
                        ProductName = cp.PRODUCT.Name,
                        PiecePrice = cp.PiecePrice,
                        UnitID = cp.UnitID,
                        Amount = cp.Amount
                    })
                    .ToList();

                return Json(contractProducts, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
