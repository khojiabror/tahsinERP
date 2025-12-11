using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.POrder;

namespace tahsinERP.Controllers
{
    public class POrdersController : Controller
    {
        // GET: POrders
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "POrders", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var orders = await db.P_ORDERS
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Where(o => o.IsDeleted != true)
                    .OrderByDescending(o => o.IssuedDate)
                    .ToListAsync();

                return View(orders);
            }
        }

        // GET: POrders/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "POrders", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
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

                return View(order);
            }
        }

        // GET: POrders/Create
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Get sales orders for multi-select
                var salesOrders = await db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Where(o => o.IsDeleted != true)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo, CustomerName = o.CUSTOMER.Name })
                    .ToListAsync();

                ViewBag.SalesOrders = new MultiSelectList(
                    salesOrders.Select(so => new { 
                        ID = so.ID, 
                        DisplayText = $"{so.OrderNo} - {so.CustomerName}" 
                    }),
                    "ID",
                    "DisplayText"
                );

                // Get suppliers
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name"
                );

                // Get companies
                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name"
                );

                // Get currencies
                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                // Get purchasable products (for manual addition)
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                // Get units
                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View();
            }
        }

        // POST: POrders/GenerateFromSalesOrders
        [HttpPost]
        public async Task<JsonResult> GenerateFromSalesOrders(int[] salesOrderIds)
        {
            if (salesOrderIds == null || salesOrderIds.Length == 0)
            {
                return Json(new { success = false, message = "Hech qanday sotuv buyurtmasi tanlanmagan." });
            }

            using (var db = new DBTHSNEntities())
            {
                try
                {
                    // Load selected sales orders with products
                    var salesOrders = await db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                        .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT.PRODUCT_CATEGORIES))
                        .Where(o => salesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                        .ToListAsync();

                    // Dictionary to aggregate required quantities by component (raw material)
                    var componentRequirements = new Dictionary<int, ComponentRequirement>();

                    // Process each sales order
                    foreach (var sOrder in salesOrders)
                    {
                        foreach (var sOrderProduct in sOrder.S_ORDER_PRODUCTS)
                        {
                            var product = sOrderProduct.PRODUCT;
                            
                            // Check if product is finished good
                            if (product.PRODUCT_CATEGORIES != null && 
                                product.PRODUCT_CATEGORIES.IsFinishedGood == true)
                            {
                                // Find active BOM for this finished good
                                var bomHeader = await db.BOM_HEADERS
                                    .Include(b => b.BOM_LINES)
                                    .Where(b => b.ProductID == product.ID && 
                                                b.IsActive == true)
                                    .OrderByDescending(b => b.CreatedAt)
                                    .FirstOrDefaultAsync();

                                if (bomHeader != null && bomHeader.BOM_LINES != null)
                                {
                                    // Calculate required quantity for each component
                                    double finishedGoodQty = sOrderProduct.Amount;

                                    foreach (var bomLine in bomHeader.BOM_LINES)
                                    {
                                        if (bomLine.ComponentID.HasValue && bomLine.Qty.HasValue)
                                        {
                                            int componentId = bomLine.ComponentID.Value;
                                            double requiredQty = (double)(finishedGoodQty * (double)bomLine.Qty.Value);

                                            // Check if component is purchasable
                                            var component = await db.PRODUCTS
                                                .Include(p => p.PRODUCT_CATEGORIES)
                                                .FirstOrDefaultAsync(p => p.ID == componentId && p.IsDeleted != true);

                                            if (component != null)
                                            {
                                                bool isPurchasable = component.PRODUCT_CATEGORIES == null ||
                                                    (component.PRODUCT_CATEGORIES.IsFinishedGood != true &&
                                                     (component.PRODUCT_CATEGORIES.AllowPurchase == true || 
                                                      component.IsRawMaterial == true));

                                                if (isPurchasable)
                                                {
                                                    // Aggregate quantities
                                                    if (componentRequirements.ContainsKey(componentId))
                                                    {
                                                        componentRequirements[componentId].RequiredQty += requiredQty;
                                                    }
                                                    else
                                                    {
                                                        // Get estimated price from NORM_INPUT_LINES
                                                        decimal? estimatedPrice = null;
                                                        var normLine = await db.NORM_INPUT_LINES
                                                            .Where(n => n.ProductID == product.ID && 
                                                                        n.MaterialID == componentId)
                                                            .OrderByDescending(n => n.CreatedDT)
                                                            .FirstOrDefaultAsync();

                                                        if (normLine != null)
                                                        {
                                                            estimatedPrice = normLine.Price;
                                                        }

                                                        componentRequirements[componentId] = new ComponentRequirement
                                                        {
                                                            ComponentID = componentId,
                                                            ComponentPNo = component.PNo,
                                                            ComponentName = component.Name,
                                                            UnitID = component.UnitID,
                                                            RequiredQty = requiredQty,
                                                            EstimatedPrice = estimatedPrice.HasValue ? (double?)estimatedPrice.Value : null
                                                        };
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Convert to list for JSON response
                    var result = componentRequirements.Values.Select(cr => new
                    {
                        ComponentID = cr.ComponentID,
                        ComponentPNo = cr.ComponentPNo,
                        ComponentName = cr.ComponentName,
                        UnitID = cr.UnitID,
                        RequiredQty = cr.RequiredQty,
                        EstimatedPrice = cr.EstimatedPrice,
                        EstimatedAmount = cr.EstimatedPrice.HasValue ? cr.RequiredQty * cr.EstimatedPrice.Value : (double?)null
                    }).ToList();

                    return Json(new { success = true, data = result });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // Helper class for component requirements
        private class ComponentRequirement
        {
            public int ComponentID { get; set; }
            public string ComponentPNo { get; set; }
            public string ComponentName { get; set; }
            public int UnitID { get; set; }
            public double RequiredQty { get; set; }
            public double? EstimatedPrice { get; set; }
        }

        // POST: POrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(POrderCreateViewModel model)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // Generate OrderNo if not provided
                        if (string.IsNullOrEmpty(model.OrderNo))
                        {
                            model.OrderNo = await GenerateOrderNo(db);
                        }

                        // Create order header
                        var order = new P_ORDERS
                        {
                            OrderNo = model.OrderNo,
                            IssuedDate = model.IssuedDate,
                            CompanyID = model.CompanyID,
                            SupplierID = model.SupplierID,
                            ContractID = model.ContractID,
                            CurrencyID = model.CurrencyID,
                            Description = model.Description,
                            IsDeleted = false,
                            Amount = 0
                        };

                        db.P_ORDERS.Add(order);
                        await db.SaveChangesAsync();

                        // Calculate total amount and create order products
                        double totalAmount = 0;
                        if (model.OrderProducts != null && model.OrderProducts.Any())
                        {
                            foreach (var orderProduct in model.OrderProducts)
                            {
                                var product = await db.PRODUCTS.FindAsync(orderProduct.ProductID);
                                if (product != null)
                                {
                                    var pOrderProduct = new P_ORDER_PRODUCTS
                                    {
                                        OrderID = order.ID,
                                        PartID = orderProduct.ProductID,
                                        Price = orderProduct.Price,
                                        Amount = orderProduct.Amount,
                                        UnitID = orderProduct.UnitID ?? product.UnitID,
                                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                                        MOQ = orderProduct.MOQ
                                    };

                                    db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                                    totalAmount += pOrderProduct.TotalPrice;
                                }
                            }

                            order.Amount = totalAmount;
                            await db.SaveChangesAsync();
                        }

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Create - {order.OrderNo} buyurtmasi yaratildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                var salesOrders = await db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Where(o => o.IsDeleted != true)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo, CustomerName = o.CUSTOMER.Name })
                    .ToListAsync();

                ViewBag.SalesOrders = new MultiSelectList(
                    salesOrders.Select(so => new { ID = so.ID, DisplayText = $"{so.OrderNo} - {so.CustomerName}" }),
                    "ID", "DisplayText"
                );

                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );
            }

            return View(model);
        }

        // Generate OrderNo
        private async Task<string> GenerateOrderNo(DBTHSNEntities db)
        {
            var year = DateTime.Now.Year;
            var month = DateTime.Now.Month;
            var prefix = $"PO-{year}-{month:D2}-";

            var lastOrder = await db.P_ORDERS
                .Where(o => o.OrderNo.StartsWith(prefix))
                .OrderByDescending(o => o.OrderNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastOrder != null)
            {
                var lastNumberStr = lastOrder.OrderNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        // GET: POrders/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
                    .Include(o => o.P_ORDER_PRODUCTS)
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
                    CompanyID = order.CompanyID,
                    SupplierID = order.SupplierID,
                    ContractID = order.ContractID,
                    CurrencyID = order.CurrencyID,
                    Description = order.Description,
                    OrderProducts = order.P_ORDER_PRODUCTS.Select(op => new POrderProductViewModel
                    {
                        ID = op.ID,
                        ProductID = op.PartID,
                        ProductPNo = op.PRODUCT.PNo,
                        ProductName = op.PRODUCT.Name,
                        Price = op.Price,
                        Amount = op.Amount,
                        UnitID = op.UnitID,
                        TotalPrice = op.TotalPrice,
                        MOQ = op.MOQ
                    }).ToList()
                };

                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View(model);
            }
        }

        // POST: POrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(POrderEditViewModel model)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
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

                        // Update order header
                        order.OrderNo = model.OrderNo;
                        order.IssuedDate = model.IssuedDate;
                        order.CompanyID = model.CompanyID;
                        order.SupplierID = model.SupplierID;
                        order.ContractID = model.ContractID;
                        order.CurrencyID = model.CurrencyID;
                        order.Description = model.Description;

                        // Remove existing order products
                        var existingProducts = db.P_ORDER_PRODUCTS.Where(op => op.OrderID == order.ID).ToList();
                        foreach (var existing in existingProducts)
                        {
                            db.P_ORDER_PRODUCTS.Remove(existing);
                        }

                        // Add updated order products
                        double totalAmount = 0;
                        if (model.OrderProducts != null && model.OrderProducts.Any())
                        {
                            foreach (var orderProduct in model.OrderProducts)
                            {
                                var product = await db.PRODUCTS.FindAsync(orderProduct.ProductID);
                                if (product != null)
                                {
                                    var pOrderProduct = new P_ORDER_PRODUCTS
                                    {
                                        OrderID = order.ID,
                                        PartID = orderProduct.ProductID,
                                        Price = orderProduct.Price,
                                        Amount = orderProduct.Amount,
                                        UnitID = orderProduct.UnitID ?? product.UnitID,
                                        TotalPrice = orderProduct.Price * orderProduct.Amount,
                                        MOQ = orderProduct.MOQ
                                    };

                                    db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                                    totalAmount += pOrderProduct.TotalPrice;
                                }
                            }
                        }

                        order.Amount = totalAmount;
                        await db.SaveChangesAsync();

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Edit - {order.OrderNo} buyurtmasi tahrirlandi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );
            }

            return View(model);
        }

        // GET: POrders/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
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

        // POST: POrders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS.FindAsync(id);
                if (order != null)
                {
                    order.IsDeleted = true;
                    await db.SaveChangesAsync();

                    // Log the action
                    LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Delete - {order.OrderNo} buyurtmasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }
    }
}
