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
        // Olish so'rovi: POrders
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

                // Ta'minotchi bo'yicha filtrlash
                if (supplierID.HasValue && supplierID.Value > 0)
                {
                    query = query.Where(o => o.SupplierID == supplierID.Value);
                }

                // Shartnoma bo'yicha filtrlash
                if (contractID.HasValue && contractID.Value > 0)
                {
                    query = query.Where(o => o.ContractID == contractID.Value);
                }

                // Xarid turi bo'yicha filtrlash
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
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders");

                return View(orders);
            }
        }

        // Olish so'rovi: POrders/Details/5
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

        // Olish so'rovi: POrders/yaratish
        public async Task<ActionResult> Create(int? requisitionId = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "POrders"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Buyurtma talabnomadan yaratilayotganini tekshirish (bilvosita xarid)
                P_REQUISITIONS requisition = null;
                if (requisitionId.HasValue)
                {
                    requisition = await db.P_REQUISITIONS
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                        .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
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

                    ViewBag.Requisition = requisition;
                    ViewBag.PurchaseType = 1; // Bilvosita
                }
                else
                {
                    ViewBag.PurchaseType = 0; // Bevosita
                }

                // Ko'p tanlov uchun sotuv buyurtmalarini olish (S_ORDER orqali yaratish uchun) - faqat bevosita xaridlar
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

                // Ta'minotchilarni olish
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name"
                );

                // Standart kompaniyani olish (birinchi faol kompaniya)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                // Valyutalarni olish
                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                // Xarid qilinadigan mahsulotlarni olish
                // Bilvosita xarid uchun: uskuna, asbob, sarf materiallari, yordamchi tovarlar
                // Bevosita xarid uchun: xomashyo (BOM)
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

                // O'lchov birliklarini olish
                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                // Bilvosita xaridlar uchun bo'lim va xodimlarni olish
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

        // POST so'rovi: POrders/LoadRequisitionProducts - talabnomadagi mahsulotlarni yuklash
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
                        return Json(new { success = false, message = "Talabnoma topilmadi." });
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

        // POST so'rovi: POrders/GenerateFromSalesOrders - S_ORDERS asosida BOM yechimini hisoblash
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
                    // Sotuv buyurtmalari mavjudligi va ularda mahsulot borligini tekshirish
                    var salesOrders = await db.S_ORDERS
                        .Include(o => o.S_ORDER_PRODUCTS)
                        .Where(o => salesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                        .ToListAsync();

                    if (!salesOrders.Any())
                    {
                        return Json(new { success = false, message = "Tanlangan sotuv buyurtmalari topilmadi." });
                    }

                    // Sotuv buyurtmalarida mahsulot borligini tekshirish
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

                    // JSON javobi uchun ro'yxatga aylantirish
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

        // POST so'rovi: POrders/yaratish
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
                            // OrderNo berilmagan bo'lsa, avtomatik yaratish
                            if (string.IsNullOrEmpty(model.OrderNo))
                            {
                                model.OrderNo = await GenerateOrderNo(db);
                            }

                            // Kompaniya berilmagan bo'lsa, standart kompaniyani olish
                            var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                            var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                            // Xarid turini aniqlash
                            byte purchaseType = 0; // Standart: Bevosita
                            int? requisitionId = null;
                            int? departmentId = null;
                            int? requesterId = null;
                            string contactPhone = null;
                            string purpose = null;

                            // Buyurtma talabnoma asosida yaratilganini tekshirish
                            if (Request.Form["RequisitionID"] != null && int.TryParse(Request.Form["RequisitionID"], out int reqId))
                            {
                                var requisition = await db.P_REQUISITIONS.FindAsync(reqId);
                                if (requisition != null)
                                {
                                    purchaseType = 1; // Bilvosita
                                    requisitionId = reqId;
                                    departmentId = requisition.DepartmentID;
                                    requesterId = requisition.RequesterID;
                                    contactPhone = requisition.ContactPhone;
                                    purpose = requisition.Purpose;
                                }
                            }

                            // Buyurtma sarlavhasini yaratish
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

                            // Buyurtma mahsulotlarini qo'shish
                            double totalAmount = 0;
                            if (model.OrderProducts != null && model.OrderProducts.Any())
                            {
                                foreach (var orderProduct in model.OrderProducts)
                                {
                                    if (orderProduct.ProductID <= 0)
                                    {
                                        continue; // Noto'g'ri mahsulotlarni o'tkazib yuborish
                                    }

                                    var product = await db.PRODUCTS.FindAsync(orderProduct.ProductID);
                                    if (product == null || product.IsDeleted == true)
                                    {
                                        continue; // O'chirilgan yoki topilmagan mahsulotlarni o'tkazib yuborish
                                    }

                                        // Talabnomadan ustuvorlik va kerakli sanani olish (mavjud bo'lsa)
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

                                // Buyurtmaning umumiy summasini yangilash
                                order.Amount = totalAmount;
                                await db.SaveChangesAsync();
                            }

                            // Agar buyurtma talabnomadan yaratilgan bo'lsa, talabnoma holatini yangilash
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

                            // Barcha jarayon muvaffaqiyatli tugagandan keyin tranzaksiyani tasdiqlash
                            transaction.Commit();

                            // Amalni logga yozish
                            LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Create - {order.OrderNo} buyurtmasi yaratildi (Xarid turi: {(purchaseType == 1 ? "Bilvosita" : "Bevosita")})");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            // Tranzaksiyani xavfsiz bekor qilish - SaveChanges paytida xatolik bo'lsa ulanish nul bo'lishi mumkin
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
                                // Bekor qilish xatolarini e'tiborsiz qoldiramiz - tranzaksiya allaqachon yaroqsiz holatda bo'lishi mumkin
                                // Bu SaveChangesAsync xatoga uchrab ulanish yopilganida yuz berishi mumkin
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


            // Xatolikda ViewBag qiymatlarini qayta ga'ldirish
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

        // OrderNo yaratish
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

        // Olish so'rovi: POrders/Edit/5
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

        // POST so'rovi: POrders/Edit/5
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

                        // Buyurtma sarlavhasini yangilash
                        order.OrderNo = model.OrderNo;
                        // Kompaniya berilmagan bo'lsa, standart kompaniyani olish
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                        var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                        order.IssuedDate = model.IssuedDate;
                        order.CompanyID = companyId;
                        order.SupplierID = model.SupplierID;
                        order.ContractID = model.ContractID;
                        order.CurrencyID = model.CurrencyID;
                        order.Description = model.Description;

                        // Mavjud buyurtma mahsulotlarini o'chirish
                        var existingProducts = db.P_ORDER_PRODUCTS.Where(op => op.OrderID == order.ID).ToList();
                        foreach (var existing in existingProducts)
                        {
                            db.P_ORDER_PRODUCTS.Remove(existing);
                        }

                        // Yangilangan buyurtma mahsulotlarini qo'shish
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

                        // Amalni logga yozish
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

            // Xatolikda ViewBag qiymatlarini qayta ga'ldirish
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

        // Olish so'rovi: POrders/o'chirish/5
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

        // POST so'rovi: POrders/o'chirish/5
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

                    // Amalni logga yozish
                    LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Delete - {order.OrderNo} buyurtmasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }
    }
}
