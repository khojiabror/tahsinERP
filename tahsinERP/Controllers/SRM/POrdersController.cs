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

                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, order.ContractID);
                ViewBag.BomVersionNotes = BuildBomVersionNotes(order);
                return View(order);
            }
        }

        private static List<string> BuildBomVersionNotes(P_ORDERS order)
        {
            var notes = new List<string>();
            if (order?.P_ORDER_PRODUCTS == null)
            {
                return notes;
            }

            var pattern = new Regex(
                @"BOM qaysi mahsulot uchun:\s*(?<owner>.+?)\r?\n.*?BOM versiyasi:\s*(?<version>.+?)\r?\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (var row in order.P_ORDER_PRODUCTS)
            {
                if (string.IsNullOrWhiteSpace(row.Priority))
                {
                    continue;
                }

                foreach (Match m in pattern.Matches(row.Priority))
                {
                    var ownerRaw = (m.Groups["owner"].Value ?? "").Trim();
                    var versionRaw = (m.Groups["version"].Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(ownerRaw) || string.IsNullOrWhiteSpace(versionRaw))
                    {
                        continue;
                    }

                    var ownerName = ownerRaw;
                    var dashIndex = ownerRaw.IndexOf("—");
                    if (dashIndex >= 0 && dashIndex < ownerRaw.Length - 1)
                    {
                        ownerName = ownerRaw.Substring(dashIndex + 1).Trim();
                    }

                    var line = ownerName + " maxsuloti uchun BOM " + versionRaw + " oyidagi versiyasi dan olindi";
                    if (!notes.Contains(line))
                    {
                        notes.Add(line);
                    }
                }
            }

            return notes;
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
                ViewBag.Contracts = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");

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

                return View(new POrderCreateViewModel
                {
                    RequisitionID = requisition?.ID
                });
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
                        CalculationExplanation = cr.CalculationExplanation ?? "",
                        CalculationSummary = cr.CalculationSummary ?? ""
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
                            else
                            {
                                model.OrderNo = model.OrderNo.Trim();
                            }

                            if (!string.IsNullOrEmpty(model.OrderNo))
                            {
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
                            if (model.RequisitionID.HasValue)
                            {
                                var requisition = await db.P_REQUISITIONS.FindAsync(model.RequisitionID.Value);
                                if (requisition != null)
                                {
                                    purchaseType = 1; // Bilvosita
                                    requisitionId = requisition.ID;
                                    departmentId = requisition.DepartmentID;
                                    requesterId = requisition.RequesterID;
                                    contactPhone = requisition.ContactPhone;
                                    purpose = requisition.Purpose;
                                }
                            }

                            var supplierId = model.SupplierID;
                            if (purchaseType == 0 || !supplierId.HasValue || supplierId.Value <= 0)
                            {
                                supplierId = null;
                            }

                            int? contractId = model.ContractID;
                            if (purchaseType == 0 || !contractId.HasValue || contractId.Value <= 0)
                            {
                                contractId = null;
                            }

                            // Buyurtma sarlavhasini yaratish
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

                                        // Talabnomadan ustuvorlik va kerakli sanani olish (mavjud bo'lsa).
                                        // Hisob tushuntirish (sotuv/BOM generatsiyasi) bo'lsa — P_ORDER_PRODUCTS.Priority maydonida saqlanadi.
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

                                        if (!string.IsNullOrWhiteSpace(orderProduct.CalculationSummary))
                                        {
                                            priority = orderProduct.CalculationSummary.Trim();
                                        }
                                        else if (!string.IsNullOrWhiteSpace(orderProduct.CalculationExplanation))
                                        {
                                            priority = orderProduct.CalculationExplanation.Trim();
                                        }

                                        priority = TruncateToPOrderProductPriority(priority);

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
                            catch (Exception)
                            {
                                // Bekor qilish xatolarini e'tiborsiz qoldiramiz - tranzaksiya allaqachon yaroqsiz holatda bo'lishi mumkin
                                // Bu SaveChangesAsync xatoga uchrab ulanish yopilganida yuz berishi mumkin
                            }

                            AppendDatabaseErrorsToModelState(ModelState, ex);
                        }
                    }
                }
            }


            // Xatolikda ViewBag qiymatlarini qayta ga'ldirish
            using (var db = new DBTHSNEntities())
            {
                bool isIndirect = model.RequisitionID.HasValue;
                ViewBag.PurchaseType = isIndirect ? 1 : 0;

                var salesOrders = await db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Where(o => o.IsDeleted != true)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo, CustomerName = o.CUSTOMER.Name })
                    .ToListAsync();

                ViewBag.SalesOrders = new MultiSelectList(
                    salesOrders.Select(so => new { ID = so.ID, DisplayText = $"{so.OrderNo} - {so.CustomerName}" }),
                    "ID", "DisplayText",
                    model.SelectedSalesOrderIds
                );

                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", isIndirect ? model.SupplierID : null
                );
                if (isIndirect && model.SupplierID.HasValue)
                {
                    var createContracts = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new { c.ID, c.ContractNo })
                        .ToListAsync();
                    ViewBag.Contracts = new SelectList(createContracts, "ID", "ContractNo", model.ContractID);
                }
                else
                {
                    ViewBag.Contracts = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
                    model.SupplierID = null;
                    model.ContractID = null;
                }

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
                .Where(o => o.IsDeleted != true && o.OrderNo != null && o.OrderNo.StartsWith(prefix))
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

                bool isDirectOrder = order.PurchaseType == 0;
                ViewBag.IsDirectOrder = isDirectOrder;
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", isDirectOrder ? null : model.SupplierID
                );
                if (!isDirectOrder && model.SupplierID.HasValue)
                {
                    var editContracts = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new { c.ID, c.ContractNo })
                        .ToListAsync();
                    ViewBag.Contracts = new SelectList(editContracts, "ID", "ContractNo", model.ContractID);
                }
                else
                {
                    ViewBag.Contracts = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
                    model.SupplierID = null;
                    model.ContractID = null;
                }

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

                ViewBag.PurchasableProductsList = purchasableProducts;
                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                var units = await db.UNITS.ToListAsync();
                ViewBag.UnitsList = units;
                ViewBag.Units = new SelectList(units, "ID", "ShortName");
                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, model.ContractID);

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

                        if (!ModelState.IsValid)
                        {
                            // ViewBag qayta to'ldirish blokiga o'tiladi
                        }
                        else
                        {
                        // Buyurtma sarlavhasini yangilash
                        order.OrderNo = editOrderNo ?? model.OrderNo;
                        // Kompaniya berilmagan bo'lsa, standart kompaniyani olish
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                        var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                        order.IssuedDate = model.IssuedDate;
                        order.CompanyID = companyId;
                        bool isDirectOrder = order.PurchaseType == 0;
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
                            }
                        }

                        order.Amount = totalAmount;
                        await db.SaveChangesAsync();

                        // Amalni logga yozish
                        LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Edit - {order.OrderNo} buyurtmasi tahrirlandi");

                        return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendDatabaseErrorsToModelState(ModelState, ex);
                    }
                }
            }

            // Xatolikda ViewBag qiymatlarini qayta ga'ldirish
            using (var db = new DBTHSNEntities())
            {
                var currentOrder = await db.P_ORDERS.AsNoTracking().FirstOrDefaultAsync(o => o.ID == model.ID);
                bool isDirectOrder = currentOrder != null && currentOrder.PurchaseType == 0;
                ViewBag.IsDirectOrder = isDirectOrder;
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", isDirectOrder ? null : model.SupplierID
                );
                if (!isDirectOrder && model.SupplierID.HasValue)
                {
                    var failContracts = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new { c.ID, c.ContractNo })
                        .ToListAsync();
                    ViewBag.Contracts = new SelectList(failContracts, "ID", "ContractNo", model.ContractID);
                }
                else
                {
                    ViewBag.Contracts = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
                    model.SupplierID = null;
                    model.ContractID = null;
                }

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

                ViewBag.PurchasableProductsList = purchasableProducts;
                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

                var units = await db.UNITS.ToListAsync();
                ViewBag.UnitsList = units;
                ViewBag.Units = new SelectList(units, "ID", "ShortName");
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
                    await SoftDeleteReferenceService.AfterPOrderSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    // Amalni logga yozish
                    LogHelper.LogToDatabase(User.Identity.Name, "POrders", $"Delete - {order.OrderNo} buyurtmasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<JsonResult> GetContractsBySupplier(int supplierId)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var contracts = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == supplierId && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new { c.ID, c.ContractNo })
                        .ToListAsync();

                    var contractIds = contracts.Select(c => c.ID).ToList();
                    var contractProducts = await db.P_CONTRACT_PRODUCTS
                        .Where(p => contractIds.Contains(p.ContractID))
                        .Select(p => new PContractVatHelper.ContractProductVatLine
                        {
                            ContractID = p.ContractID,
                            Amount = p.Amount,
                            VATAmount = p.VATAmount
                        })
                        .ToListAsync();

                    var vatByContract = PContractVatHelper.BuildContractVatRates(contractProducts);

                    var data = contracts.Select(c => new
                    {
                        c.ID,
                        c.ContractNo,
                        VatRate = vatByContract.ContainsKey(c.ID) ? vatByContract[c.ID] : 0d
                    }).ToList();

                    return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private const int POrderNoMaxLength = 50;
        private const string ArchivedOrderNoSuffixDeleted = "_deleted";
        private const string ArchivedOrderNoSuffixDl = "_dl";

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
