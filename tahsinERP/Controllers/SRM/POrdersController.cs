using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.POrder;

namespace tahsinERP.Controllers
{
    public class POrdersController : Controller
    {
        // GET: POrders
        public async Task<ActionResult> Index(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "POrders", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_ORDERS
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Where(o => o.IsDeleted != true)
                    .AsQueryable();

                // Filter by supplier
                if (supplierID.HasValue && supplierID.Value > 0)
                {
                    query = query.Where(o => o.SupplierID == supplierID.Value);
                }

                // Filter by contract
                if (contractID.HasValue && contractID.Value > 0)
                {
                    query = query.Where(o => o.ContractID == contractID.Value);
                }

                // Filter by purchase type
                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                    query = query.Where(o => o.PurchaseType == purchaseType);
                }

                var orders = await query
                    .OrderByDescending(o => o.IssuedDate)
                    .ToListAsync();

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.SupplierID = supplierID;
                ViewBag.ContractID = contractID;

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
        public async Task<ActionResult> Create(int? requisitionId = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Check if creating from requisition (indirect purchase)
                P_REQUISITIONS requisition = null;
                if (requisitionId.HasValue)
                {
                    requisition = await db.P_REQUISITIONS
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
                        .FirstOrDefaultAsync(r => r.ID == requisitionId.Value && r.IsDeleted != true);

                    if (requisition == null)
                    {
                        TempData["ErrorMessage"] = "Requisition not found.";
                        return RedirectToAction("Index", "PRequisitions");
                    }

                    if (requisition.Status != "Approved")
                    {
                        TempData["ErrorMessage"] = "Only approved requisitions can be converted to purchase orders.";
                        return RedirectToAction("Details", "PRequisitions", new { id = requisitionId });
                    }

                    ViewBag.Requisition = requisition;
                    ViewBag.PurchaseType = 1; // Indirect
                }
                else
                {
                    ViewBag.PurchaseType = 0; // Direct
                }

                // Get sales orders for multi-select (S_ORDER orqali yaratish uchun) - only for direct purchases
                if (requisition == null)
                {
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
                }

                // Get suppliers
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name"
                );

                // Get default company (first active company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                // Get currencies
                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                // Get purchasable products
                // For indirect: equipment, tools, consumables, auxiliary
                // For direct: raw materials (BOM)
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true)
                    .Where(p => requisition != null 
                        ? (p.PRODUCT_CATEGORIES == null || 
                           (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                            (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                             p.PRODUCT_CATEGORIES.AllowPurchase == true)))
                        : (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                // Get units
                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                // For indirect purchases, get departments and employees
                if (requisition != null)
                {
                    var departments = await db.HR_ORG_UNITS
                        .Where(d => d.IsActive == true)
                        .OrderBy(d => d.Name)
                        .ToListAsync();
                    ViewBag.Departments = new SelectList(departments, "ID", "Name", requisition.DepartmentID);

                    var employees = await db.HR_EMPLOYEES
                        .Where(e => e.IsActive == true)
                        .OrderBy(e => e.FullName)
                        .ToListAsync();
                    ViewBag.Employees = new SelectList(employees, "ID", "FullName", requisition.RequesterID);
                }

                return View();
            }
        }

        // POST: POrders/LoadRequisitionProducts - Load products from requisition
        [HttpPost]
        public async Task<JsonResult> LoadRequisitionProducts(int requisitionId)
        {
            using (var db = new DBTHSNEntities())
            {
                try
                {
                    var requisition = await db.P_REQUISITIONS
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
                        .FirstOrDefaultAsync(r => r.ID == requisitionId && r.IsDeleted != true);

                    if (requisition == null)
                    {
                        return Json(new { success = false, message = "Requisition not found." });
                    }

                    var products = requisition.P_REQUISITION_PRODUCTS.Select(rp => new
                    {
                        ProductID = rp.ProductID,
                        ProductPNo = rp.PRODUCT.PNo,
                        ProductName = rp.PRODUCT.Name,
                        Quantity = rp.Quantity,
                        UnitID = rp.UnitID,
                        EstimatedPrice = rp.EstimatedPrice,
                        Priority = rp.Priority,
                        RequiredDate = rp.RequiredDate.HasValue ? rp.RequiredDate.Value.ToString("yyyy-MM-dd") : null
                    }).ToList();

                    return Json(new { success = true, data = products });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }

        // POST: POrders/GenerateFromSalesOrders - BOM explosion from S_ORDERS
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
                    // Verify sales orders exist and have products
                    var salesOrders = await db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS)
                        .Where(o => salesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                        .ToListAsync();

                    if (!salesOrders.Any())
                    {
                        return Json(new { success = false, message = "Tanlangan sotuv buyurtmalari topilmadi." });
                    }

                    // Check if sales orders have products
                    var hasProducts = salesOrders.Any(o => o.S_ORDER_PRODUCTS != null && o.S_ORDER_PRODUCTS.Any());
                    if (!hasProducts)
                    {
                        return Json(new { success = false, message = "Tanlangan sotuv buyurtmalarida mahsulotlar topilmadi." });
                    }

                    var generationService = new POrderGenerationService(db);
                    var requirements = await generationService.GenerateRequirementsFromSalesOrdersAsync(salesOrderIds);

                    if (requirements == null || !requirements.Any())
                    {
                        return Json(new { success = false, message = "BOM asosida xom ashyo ehtiyojlari topilmadi. Mahsulotlar uchun faol BOM mavjud emas yoki BOM'da xom ashyo yo'q." });
                    }

                    // Convert to list for JSON response
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
                        CalculationExplanation = cr.CalculationExplanation ?? ""
                    }).ToList();

                    return Json(new { success = true, data = result });
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Xatolik: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $" | Ichki xatolik: {ex.InnerException.Message}";
                    }
                    return Json(new { success = false, message = errorMessage });
                }
            }
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
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            // Generate OrderNo if not provided
                            if (string.IsNullOrEmpty(model.OrderNo))
                            {
                                model.OrderNo = await GenerateOrderNo(db);
                            }

                            // Get default company if not provided
                            var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                            var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                            // Determine purchase type
                            byte purchaseType = 0; // Default: Direct
                            int? requisitionId = null;
                            int? departmentId = null;
                            int? requesterId = null;
                            string contactPhone = null;
                            string purpose = null;

                            // Check if order is created from requisition
                            if (Request.Form["RequisitionID"] != null && int.TryParse(Request.Form["RequisitionID"], out int reqId))
                            {
                                var requisition = await db.P_REQUISITIONS.FindAsync(reqId);
                                if (requisition != null)
                                {
                                    purchaseType = 1; // Indirect
                                    requisitionId = reqId;
                                    departmentId = requisition.DepartmentID;
                                    requesterId = requisition.RequesterID;
                                    contactPhone = requisition.ContactPhone;
                                    purpose = requisition.Purpose;
                                }
                            }

                            // Create order header
                            var order = new P_ORDERS
                            {
                                OrderNo = model.OrderNo,
                                IssuedDate = model.IssuedDate,
                                CompanyID = companyId,
                                SupplierID = model.SupplierID ?? 0,
                                ContractID = model.ContractID,
                                CurrencyID = model.CurrencyID ?? 0,
                                Description = model.Description,
                                IsDeleted = false,
                                Amount = 0,
                                PurchaseType = purchaseType,
                                RequisitionID = requisitionId,
                                DepartmentID = departmentId,
                                RequesterID = requesterId,
                                ContactPhone = contactPhone,
                                Purpose = purpose,
                                Status = "Created",
                                ContractStatus = "NotCreated",
                                InvoiceStatus = "NotCreated",
                                WarehouseStatus = "NotReceived"
                            };

                            db.P_ORDERS.Add(order);
                            await db.SaveChangesAsync();

                            // Add order products
                            double totalAmount = 0;
                            if (model.OrderProducts != null && model.OrderProducts.Any())
                            {
                                foreach (var orderProduct in model.OrderProducts)
                                {
                                    if (orderProduct.ProductID <= 0)
                                    {
                                        continue; // Skip invalid products
                                    }

                                    var product = await db.PRODUCTS.FindAsync(orderProduct.ProductID);
                                    if (product == null || product.IsDeleted == true)
                                    {
                                        continue; // Skip deleted or missing products
                                    }

                                        // Get priority and required date from requisition if available
                                        string priority = null;
                                        DateTime? requiredDate = null;
                                        if (requisitionId.HasValue)
                                        {
                                            var reqProduct = await db.P_REQUISITION_PRODUCTS
                                                .FirstOrDefaultAsync(rp => rp.RequisitionID == requisitionId.Value && 
                                                                          rp.ProductID == orderProduct.ProductID);
                                            if (reqProduct != null)
                                            {
                                                priority = reqProduct.Priority;
                                                requiredDate = reqProduct.RequiredDate;
                                            }
                                        }

                                        var pOrderProduct = new P_ORDER_PRODUCTS
                                        {
                                            OrderID = order.ID,
                                            ProductID = orderProduct.ProductID,
                                            Price = orderProduct.Price,
                                            Amount = orderProduct.Amount,
                                            UnitID = orderProduct.UnitID ?? (product.UnitID > 0 ? product.UnitID : 1),
                                            TotalPrice = orderProduct.Price * orderProduct.Amount,
                                            MOQ = orderProduct.MOQ,
                                            Priority = priority,
                                            RequiredDate = requiredDate
                                        };

                                    db.P_ORDER_PRODUCTS.Add(pOrderProduct);
                                    totalAmount += pOrderProduct.TotalPrice;
                                }

                                // Update order total amount
                                order.Amount = totalAmount;
                                await db.SaveChangesAsync();
                            }

                            // Update requisition status if created from requisition
                            if (requisitionId.HasValue)
                            {
                                var requisition = await db.P_REQUISITIONS.FindAsync(requisitionId.Value);
                                if (requisition != null)
                                {
                                    requisition.Status = "ConvertedToOrder";
                                    requisition.ConvertedToOrderID = order.ID;
                                    requisition.ModifiedDT = DateTime.Now;
                                    requisition.ModifiedByUserID = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                                }
                            }

                            // Commit transaction only after everything succeeds
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Create - {order.OrderNo} buyurtmasi yaratildi (PurchaseType: {(purchaseType == 1 ? "Indirect" : "Direct")})");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            // Safely rollback transaction - connection might be null if exception occurred during SaveChanges
                            try
                            {
                                var dbTransaction = transaction as DbContextTransaction;
                                if (dbTransaction != null)
                                {
                                    dbTransaction.Rollback();
                                }
                            }
                            catch (Exception rollbackEx)
                            {
                                // Ignore rollback errors - transaction might already be in invalid state
                                // This can happen if SaveChangesAsync failed and closed the connection
                            }

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
                                (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
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
                    CompanyID = order.CompanyID ?? 0,
                    SupplierID = order.SupplierID ?? 0,
                    ContractID = order.ContractID ?? 0,
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
                                (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
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
                        // Get default company if not provided
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                        var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                        order.IssuedDate = model.IssuedDate;
                        order.CompanyID = companyId;
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
                                        ProductID = orderProduct.ProductID,
                                        Price = orderProduct.Price,
                                        Amount = orderProduct.Amount,
                                        UnitID = orderProduct.UnitID ?? (product.UnitID > 0 ? product.UnitID : 1),
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
                                (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
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
