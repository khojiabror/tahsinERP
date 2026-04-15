using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;
using tahsinERP.Services;
using tahsinERP.Helpers;

namespace tahsinERP.Controllers.MES
{
    public class ProductProductionController : Controller
    {
        // Olish so'rovi: ProductProduction
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate, int? shopID, int? productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.PRODUCT_PRODUCTION
                    .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                    .Include(p => p.PRODUCT)
                    .Include(p => p.SHOP)
                    .AsQueryable();

                if (fromDate.HasValue)
                {
                    query = query.Where(p => p.Date >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(p => p.Date <= toDate.Value);
                }

                if (shopID.HasValue)
                {
                    query = query.Where(p => p.ShopID == shopID.Value);
                }

                if (productID.HasValue)
                {
                    query = query.Where(p => p.ProductID == productID.Value);
                }

                var productions = await query
                    .OrderByDescending(p => p.Date)
                    .ThenBy(p => p.PRODUCT.PNo)
                    .ToListAsync();

                ViewBag.Shops = new SelectList(await db.SHOPS.Where(s => s.IsDeleted == false).ToListAsync(), "ID", "ShopName", shopID);
                ViewBag.Products = new SelectList(await db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToListAsync(), "ID", "PNo", productID);
                ViewBag.FromDate = fromDate ?? DateTime.Now.AddDays(-30);
                ViewBag.ToDate = toDate ?? DateTime.Now;

                return View(productions);
            }
        }

        // Olish so'rovi: ProductProduction/yaratish (Daily Entry)
        public ActionResult Create(DateTime? date, int? shopID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new ProductProductionDailyEntryViewModel
                {
                    Date = date ?? DateTime.Now,
                    ProductionDate = date ?? DateTime.Now,
                    ShopID = shopID ?? 0
                };

                if (viewModel.ShopID > 0)
                {
                    LoadPlannedProducts(viewModel, db);
                }

                ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");

                return View(viewModel);
            }
        }

        // POST so'rovi: ProductProduction/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ProductProductionDailyEntryViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var planDay = viewModel.Date.Date;
                        var factDay = viewModel.ProductionDate.Date;
                        var allProductionsForShop = db.PRODUCT_PRODUCTION
                            .Where(p => p.ShopID == viewModel.ShopID)
                            .ToList();
                        var itemsToProcess = viewModel.ProductionItems.Where(i => i.Total > 0).ToList();

                        // Xomashyo yetarliligini oldindan tekshirish: production kiritishda bloklansin,
                        // reja yaratishda emas. Avval tekshirib olamiz, keyin yozishlarni boshlaymiz.
                        foreach (var item in itemsToProcess)
                        {
                            var existingForCheck = allProductionsForShop.FirstOrDefault(p =>
                                p.ProductID == item.ProductID &&
                                (p.Date.Date == factDay ||
                                 ProductProductionPlanDateMeta.TryParsePlanDate(p.Comment)?.Date == planDay));

                            var stockError = await ValidateRawMaterialStockForProductionAsync(
                                item.ProductID,
                                item.Total,
                                db,
                                existingForCheck?.ProductID,
                                existingForCheck?.Total);

                            if (!string.IsNullOrEmpty(stockError))
                            {
                                ModelState.AddModelError("", stockError);
                                LoadPlannedProducts(viewModel, db);
                                ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
                                return View(viewModel);
                            }
                        }

                        foreach (var item in itemsToProcess)
                        {
                            var productForLog = db.PRODUCTS.Find(item.ProductID);
                            // Bir xil reja kuni + mahsulot bo'yicha yangilash (fakt sanasi boshqacha bo'lishi mumkin)
                            var existing = allProductionsForShop.FirstOrDefault(p =>
                                p.ProductID == item.ProductID &&
                                (p.Date.Date == factDay ||
                                 ProductProductionPlanDateMeta.TryParsePlanDate(p.Comment)?.Date == planDay));

                            double? previousTotalForDelta = null;
                            if (existing != null)
                            {
                                previousTotalForDelta = existing.Total;
                                // Yangilash existing
                                existing.Date = viewModel.ProductionDate;
                                existing.DayShift = item.DayShift;
                                existing.NightShift = item.NightShift;
                                existing.Total = item.Total;
                                existing.Comment = ProductProductionPlanDateMeta.MergeIntoComment(item.Comment, viewModel.Date);
                                db.Entry(existing).State = EntityState.Modified;
                            }
                            else
                            {
                                // Yaratish new
                                var production = new PRODUCT_PRODUCTION
                                {
                                    ProductID = item.ProductID,
                                    ShopID = viewModel.ShopID,
                                    Date = viewModel.ProductionDate,
                                    DayShift = item.DayShift,
                                    NightShift = item.NightShift,
                                    Total = item.Total,
                                    Comment = ProductProductionPlanDateMeta.MergeIntoComment(item.Comment, viewModel.Date)
                                };
                                db.PRODUCT_PRODUCTION.Add(production);
                                // ID olish uchun saqlash (xomashyo chiqimni ProductionID bilan bog'lash uchun)
                                db.SaveChanges();
                                existing = production;
                            }

                            // Tayyor mahsulot: kirim hujjati (INCOME) + qoldiq — Edit bilan bir xil mantiq; tahrirda faqat delta
                            var product = db.PRODUCTS.Find(item.ProductID);
                            if (product != null && product.IsFinishedGood == true)
                            {
                                if (previousTotalForDelta.HasValue)
                                {
                                    var delta = item.Total - previousTotalForDelta.Value;
                                    WarehouseFlowService.ApplyProductionWarehouseStockDelta(
                                        item.ProductID,
                                        delta,
                                        viewModel.ShopID,
                                        db,
                                        User.Identity.Name,
                                        existing?.ID);

                                    if (delta > 0.0001)
                                    {
                                        await ConsumeMaterialsForProduction(item.ProductID, delta, db, existing?.ID, existing?.Date);
                                    }
                                    else if (delta < -0.0001)
                                    {
                                        await RevertMaterialConsumption(item.ProductID, -delta, db);
                                    }
                                }
                                else
                                {
                                    WarehouseFlowService.IncreaseProductionStock(
                                        item.ProductID,
                                        item.Total,
                                        viewModel.ShopID,
                                        db,
                                        User.Identity.Name,
                                        existing?.ID);

                                    await ConsumeMaterialsForProduction(item.ProductID, item.Total, db, existing?.ID, existing?.Date);
                                }
                            }

                            if (productForLog != null)
                                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController",
                                    $"{productForLog.PNo} uchun {item.Total} dona ishlab chiqarish kiritildi: {viewModel.ProductionDate:yyyy-MM-dd}");
                        }

                        db.SaveChanges();

                        TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli saqlandi";
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

            // Reload ViewBag on xatolik
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (viewModel.ShopID > 0)
                {
                    LoadPlannedProducts(viewModel, db);
                }
                ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
            }

            return View(viewModel);
        }

        // Olish so'rovi: ProductProduction/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION
                    .Include(p => p.PRODUCT)
                    .Include(p => p.SHOP)
                    .FirstOrDefault(p => p.ID == id);

                if (production == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar production is tied ga Quality Inspection yoki Sales Invoice
                var hasQualityInspection = db.QUALITY_INSPECTION.Any(q => q.ProductionID == id && q.IsDeleted == false);
                var hasSalesInvoice = db.SALES_INVOICE_LINES
                    .Any(l => db.PRODUCT_PRODUCTION.Any(p => p.ID == id && p.ProductID == l.ProductID));

                if (hasQualityInspection || hasSalesInvoice)
                {
                    TempData["ErrorMessage"] = "Bu ishlab chiqarish sifat nazorati yoki sotuv fakturasi bilan bog'langan. O'zgartirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                var viewModel = new ProductProductionViewModel
                {
                    ID = production.ID,
                    ProductID = production.ProductID,
                    ShopID = production.ShopID,
                    Date = production.Date,
                    DayShift = production.DayShift,
                    NightShift = production.NightShift,
                    Total = production.Total,
                    Comment = production.Comment,
                    Product = production.PRODUCT,
                    Shop = production.SHOP
                };

                ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);

                return View(viewModel);
            }
        }

        // POST so'rovi: ProductProduction/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ProductProductionViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var production = db.PRODUCT_PRODUCTION.Find(viewModel.ID);
                    if (production == null)
                    {
                        return HttpNotFound();
                    }

                    // Tekshirish agar production is tied ga Quality Inspection yoki Sales Invoice
                    var hasQualityInspection = db.QUALITY_INSPECTION.Any(q => q.ProductionID == viewModel.ID && q.IsDeleted == false);
                    var hasSalesInvoice = db.SALES_INVOICE_LINES
                        .Any(l => db.PRODUCT_PRODUCTION.Any(p => p.ID == viewModel.ID && p.ProductID == l.ProductID));

                    if (hasQualityInspection || hasSalesInvoice)
                    {
                        ModelState.AddModelError("", "Bu ishlab chiqarish sifat nazorati yoki sotuv fakturasi bilan bog'langan. O'zgartirish mumkin emas.");
                        ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                        ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);
                        return View(viewModel);
                    }

                    // Yangi natija uchun xomashyo yetarliligi: joriy yozuvning eski sarfi mavjud deb hisoblaymiz
                    // (go'yo revert qilingan) va shundan keyin yangi talabni tekshiramiz.
                    var stockError = await ValidateRawMaterialStockForProductionAsync(
                        viewModel.ProductID,
                        viewModel.Total,
                        db,
                        production.ProductID,
                        production.Total);
                    if (!string.IsNullOrEmpty(stockError))
                    {
                        ModelState.AddModelError("", stockError);
                        ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                        ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);
                        return View(viewModel);
                    }

                    // Revert old warehouse entry (uchun finished goods faqat)
                    var oldProduct = db.PRODUCTS.Find(production.ProductID);
                    if (oldProduct != null && oldProduct.IsFinishedGood == true)
                    {
                        // Decrease old stock
                        var productionWH = WarehouseFlowService.GetProductionWarehouse(db, production.ShopID);
                        if (productionWH != null)
                        {
                            var oldStock = db.PRODUCT_STOCKS
                                .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == production.ProductID);
                            if (oldStock != null)
                            {
                                oldStock.Amount -= production.Total;
                                if (oldStock.Amount < 0) oldStock.Amount = 0;
                                db.Entry(oldStock).State = EntityState.Modified;
                            }
                        }

                        // Revert old material consumption
                        await RevertMaterialConsumption(production.ProductID, production.Total, db);
                    }

                    // Yangilash production
                    production.ProductID = viewModel.ProductID;
                    production.ShopID = viewModel.ShopID;
                    production.Date = viewModel.Date;
                    production.DayShift = viewModel.DayShift;
                    production.NightShift = viewModel.NightShift;
                    production.Total = viewModel.Total;
                    production.Comment = viewModel.Comment;

                    // Yangilash warehouse bilan new values (uchun finished goods faqat)
                    var newProduct = db.PRODUCTS.Find(viewModel.ProductID);
                    if (newProduct != null && newProduct.IsFinishedGood == true)
                    {
                        WarehouseFlowService.IncreaseProductionStock(
                            viewModel.ProductID, 
                            viewModel.Total, 
                            viewModel.ShopID, 
                            db, 
                            User.Identity.Name,
                            viewModel.ID
                        );

                        // Consume materials uchun new production
                        await ConsumeMaterialsForProduction(viewModel.ProductID, viewModel.Total, db, viewModel.ID, viewModel.Date);
                    }

                    db.Entry(production).State = EntityState.Modified;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish tahrirlandi: ID {viewModel.ID}");

                    TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Shops = new SelectList(db.SHOPS.Where(s => s.IsDeleted == false).ToList(), "ID", "ShopName", viewModel.ShopID);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo", viewModel.ProductID);
            }

            return View(viewModel);
        }

        // Olish so'rovi: ProductProduction/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null || id <= 0)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION
                    .Where(p => p.ID == id && (p.IsDeleted == false || p.IsDeleted == null))
                    .Include(p => p.PRODUCT)
                    .Include(p => p.SHOP)
                    .FirstOrDefault();

                if (production == null)
                {
                    return HttpNotFound();
                }

                var viewModel = new ProductProductionDetailsViewModel
                {
                    Production = production
                };

                // Avval chiqimlarni yuklash (narxlar uchun).
                // Yangi format: DocNo = {yyyyMMdd}_{productPNo}_{miqdor}, Description = "... {productPNo}, Miqdor {miqdor}"
                // Eski format: DocNo = BOM-{timestamp}-{productId}, Description = "... Mahsulot ID {productId} ..."
                var productPNo = production.PRODUCT?.PNo ?? production.ProductID.ToString();
                var productIdSuffix = "-" + production.ProductID;
                var prodIdStr = "Mahsulot ID " + production.ProductID;
                var newFormatSuffix = "_" + productPNo + "_" + production.Total;
                var productionDateStr = production.Date.ToString("dd.MM.yyyy");
                var searchStart = production.Date.Date.AddDays(-30);
                var searchEnd = production.Date.Date.AddDays(31);

                // 0) Avval ProductionID marker bilan aniq topish (yangi yozuvlar uchun)
                var prodMarker = "ProductionID:" + production.ID;
                var expensesByProdId = await db.EXPENSES
                    .Where(e => e.IsDeleted == false
                        && e.Description != null
                        && e.Description.Contains(prodMarker))
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                // 1) Sana oralig'ida barcha chiqimlarni yuklash (fallback: eski yozuvlar uchun)
                var expensesInRange = await db.EXPENSES
                    .Where(e => e.IsDeleted == false
                        && e.IssueDateTime >= searchStart
                        && e.IssueDateTime < searchEnd)
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                // 1b) Izohda ishlab chiqarish sanasi va "kunlik reja asosida" bor chiqimlar (boshqa sana yaratilgan bo'lsa ham)
                var expensesByDescDate = await db.EXPENSES
                    .Where(e => e.IsDeleted == false
                        && e.Description != null
                        && e.Description.Contains(productionDateStr)
                        && e.Description.Contains("kunlik reja asosida")
                        && e.Description.Contains(productPNo))
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                // 2) Faqat ushbu mahsulotga tegishli BOM chiqimlari (DocNo yoki Description orqali)
                var fallbackFilter = new Func<EXPENS, bool>(e =>
                    (e.DocNo != null && e.DocNo.StartsWith("BOM-") && e.DocNo.EndsWith(productIdSuffix))
                    || (e.DocNo != null && e.DocNo.EndsWith(newFormatSuffix))
                    || (e.Description != null && e.Description.Contains("Ishlab chiqarish uchun xomashyo chiqimi") && e.Description.Contains(prodIdStr))
                    || (e.Description != null && e.Description.Contains("Ishlab chiqarish uchun xomashyo chiqimi") && e.Description.Contains(productPNo) && e.Description.Contains("Miqdor " + production.Total))
                    || (e.Description != null && e.Description.Contains(productionDateStr) && e.Description.Contains("kunlik reja asosida") && e.Description.Contains(productPNo)));
                var expensesInRangeFiltered = expensesInRange.Where(fallbackFilter).ToList();
                var expensesByDescDateFiltered = expensesByDescDate.Where(e => !expensesInRangeFiltered.Any(x => x.ID == e.ID)).ToList();
                var expenses = expensesByProdId.Any()
                    ? expensesByProdId
                    : expensesInRangeFiltered.Union(expensesByDescDateFiltered).OrderByDescending(e => e.IssueDateTime).ToList();

                // Chiqim hujjatlaridan material bo'yicha narx/summa (birinchi chiqimdan)
                var materialPrices = new Dictionary<int, Tuple<double, double>>();
                if (expenses.Any() && expenses.First().EXPENSE_PRODUCTS != null)
                {
                    foreach (var ep in expenses.First().EXPENSE_PRODUCTS)
                    {
                        materialPrices[ep.ProductID] = Tuple.Create(ep.PiecePrice, ep.TotalPrice);
                    }
                }

                // BOM/Norm bo'yicha sarf hisoblash va narx qo'shish
                var consumptionService = new ProductionConsumptionService(db);
                var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(production.ProductID, production.Total);
                var displayUnitIds = requirements.Select(r => consumptionService.GetDisplayUnitFromIncome(r.MaterialID)).Where(u => u.HasValue).Select(u => u.Value).Distinct().ToList();
                var units = displayUnitIds.Any() 
                    ? await db.UNITS.Where(u => displayUnitIds.Contains(u.ID)).ToDictionaryAsync(u => u.ID, u => u.ShortName ?? u.UnitName ?? "-")
                    : new Dictionary<int, string>();

                foreach (var req in requirements)
                {
                    double piecePrice = 0, totalPrice = 0;
                    if (materialPrices.ContainsKey(req.MaterialID))
                    {
                        piecePrice = materialPrices[req.MaterialID].Item1;
                        totalPrice = materialPrices[req.MaterialID].Item2;
                    }
                    var displayUnitId = consumptionService.GetDisplayUnitFromIncome(req.MaterialID);
                    var unitName = displayUnitId.HasValue && units.ContainsKey(displayUnitId.Value) ? units[displayUnitId.Value] : "-";
                    viewModel.BomConsumptionLines.Add(new BomConsumptionLineViewModel
                    {
                        MaterialID = req.MaterialID,
                        MaterialPNo = req.MaterialPNo,
                        MaterialName = req.MaterialName,
                        RequiredQtyPieces = req.RequiredQtyPieces,
                        RequiredQtyMeters = req.RequiredQtyMeters,
                        WasteQty = req.WasteQty,
                        TotalConsumption = req.TotalConsumption,
                        UnitName = unitName,
                        TotalConsumptionIsInKg = req.TotalConsumptionIsInKg,
                        PiecePrice = piecePrice,
                        TotalPrice = totalPrice
                    });
                }

                // Chiqim hujjatlari (2-datatable) — URL bilan
                foreach (var exp in expenses)
                {
                    var productCount = exp.EXPENSE_PRODUCTS != null ? exp.EXPENSE_PRODUCTS.Count : 0;
                    var amount = exp.Amount ?? 0;
                    var totalPrice = exp.TotalPrice ?? 0;
                    viewModel.ExpenseDocuments.Add(new ProductionExpenseLinkViewModel
                    {
                        ExpenseID = exp.ID,
                        DocNo = exp.DocNo,
                        IssueDateTime = exp.IssueDateTime,
                        Amount = amount,
                        TotalPrice = totalPrice,
                        ProductCount = productCount,
                        DetailsUrl = Url.Action("Details", "Expenses", new { id = exp.ID })
                    });
                }

                return View(viewModel);
            }
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController.Details", $"Xatolik: {ex.Message}");
                TempData["ErrorMessage"] = "Ma'lumotlarni yuklashda xatolik yuz berdi. Iltimos, qayta urinib ko'ring.";
                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: ProductProduction/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION
                    .Include(p => p.PRODUCT)
                    .Include(p => p.SHOP)
                    .FirstOrDefault(p => p.ID == id);

                if (production == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar production is tied ga Quality Inspection yoki Sales Invoice
                var hasQualityInspection = db.QUALITY_INSPECTION.Any(q => q.ProductionID == id && q.IsDeleted == false);
                var hasSalesInvoice = db.SALES_INVOICE_LINES
                    .Any(l => db.PRODUCT_PRODUCTION.Any(p => p.ID == id && p.ProductID == l.ProductID));

                if (hasQualityInspection || hasSalesInvoice)
                {
                    TempData["ErrorMessage"] = "Bu ishlab chiqarish sifat nazorati yoki sotuv fakturasi bilan bog'langan. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                return View(production);
            }
        }

        // POST so'rovi: ProductProduction/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION.Find(id);
                if (production == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar production is tied ga Quality Inspection yoki Sales Invoice
                var hasQualityInspection = db.QUALITY_INSPECTION.Any(q => q.ProductionID == id && q.IsDeleted == false);
                var hasSalesInvoice = db.SALES_INVOICE_LINES
                    .Any(l => db.PRODUCT_PRODUCTION.Any(p => p.ID == id && p.ProductID == l.ProductID));

                if (hasQualityInspection || hasSalesInvoice)
                {
                    TempData["ErrorMessage"] = "Bu ishlab chiqarish sifat nazorati yoki sotuv fakturasi bilan bog'langan. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                // Revert warehouse entry (uchun finished goods faqat)
                var product = db.PRODUCTS.Find(production.ProductID);
                if (product != null && product.IsFinishedGood == true)
                {
                    var productionWH = WarehouseFlowService.GetProductionWarehouse(db, production.ShopID);
                    if (productionWH != null)
                    {
                        var stock = db.PRODUCT_STOCKS
                            .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == production.ProductID);
                        if (stock != null)
                        {
                            stock.Amount -= production.Total;
                            if (stock.Amount < 0) stock.Amount = 0;
                            db.Entry(stock).State = EntityState.Modified;
                    }
                    }

                    // Revert material consumption
                    await RevertMaterialConsumption(production.ProductID, production.Total, db);
                }

                production.IsDeleted = true;
                db.Entry(production).State = System.Data.Entity.EntityState.Modified;
                await SoftDeleteReferenceService.AfterProductProductionSoftDeletedAsync(db, id);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Ishlab chiqarish natijasini kiritishda xomashyo yetarliligini tekshiradi.
        /// oldProductId/oldProducedQty berilsa (Edit holati), eski yozuv sarfi omborga qaytgandek hisoblanadi.
        /// </summary>
        private async Task<string> ValidateRawMaterialStockForProductionAsync(
            int productId,
            double producedQty,
            DBTHSNEntities db,
            int? oldProductId = null,
            double? oldProducedQty = null)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db);
            if (productionWarehouse == null)
            {
                return "Ishlab chiqarish ombori topilmadi";
            }

            var consumptionService = new ProductionConsumptionService(db);
            var newRequirements = await consumptionService.CalculateRequiredRawMaterialsAsync(productId, producedQty);
            if (newRequirements == null || newRequirements.Count == 0)
            {
                return null;
            }

            var oldConsumptionByMaterial = new Dictionary<int, double>();
            if (oldProductId.HasValue && oldProducedQty.HasValue && oldProducedQty.Value > 0)
            {
                var oldRequirements = await consumptionService.CalculateRequiredRawMaterialsAsync(oldProductId.Value, oldProducedQty.Value);
                if (oldRequirements != null)
                {
                    foreach (var req in oldRequirements)
                    {
                        if (!oldConsumptionByMaterial.ContainsKey(req.MaterialID))
                        {
                            oldConsumptionByMaterial[req.MaterialID] = 0d;
                        }
                        oldConsumptionByMaterial[req.MaterialID] += req.TotalConsumption;
                    }
                }
            }

            foreach (var req in newRequirements)
            {
                var stock = await db.PRODUCT_STOCKS
                    .Include(s => s.UNIT)
                    .FirstOrDefaultAsync(s => s.WHID == productionWarehouse.ID && s.ProductID == req.MaterialID);

                var availableQty = stock?.Amount ?? 0d;
                if (oldConsumptionByMaterial.ContainsKey(req.MaterialID))
                {
                    availableQty += oldConsumptionByMaterial[req.MaterialID];
                }

                if (availableQty + 1e-9 < req.TotalConsumption)
                {
                    var unitName = (stock?.UNIT != null)
                        ? (stock.UNIT.ShortName ?? stock.UNIT.UnitName ?? "").Trim()
                        : "";
                    var availableWithUnit = string.IsNullOrEmpty(unitName)
                        ? availableQty.ToString("N2")
                        : $"{availableQty:N2} {unitName}";
                    var requiredWithUnit = string.IsNullOrEmpty(unitName)
                        ? req.TotalConsumption.ToString("N2")
                        : $"{req.TotalConsumption:N2} {unitName}";

                    return $"Ishlab chiqarish natijasini saqlab bo'lmadi: xomashyo yetarli emas. Mahsulot: {req.MaterialPNo ?? req.MaterialID.ToString()}, omborda {availableWithUnit}, kerak {requiredWithUnit}.";
                }
            }

            return null;
        }

        /// <summary>
        /// Consumes materials dan Production warehouse when production is confirmed.
        /// uchun weight-based materials (e.g. shurup) BOM dona is converted ga kg va deducted dan stock.
        /// </summary>
        private async Task ConsumeMaterialsForProduction(int productId, double producedQty, DBTHSNEntities db, int? productionId = null, DateTime? productionDate = null)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db);
            if (productionWarehouse == null)
            {
                throw new Exception("Ishlab chiqarish ombori topilmadi");
            }

            var consumptionService = new ProductionConsumptionService(db);
            await consumptionService.ConsumeMaterialsForProductionAsync(
                productId, producedQty, productionWarehouse.ID, productionId, productionDate);
        }

        /// <summary>
        /// Reverts material consumption (uchun edit/o'chirish operations). Adds back ga production warehouse in same unit (kg uchun weight-based).
        /// </summary>
        private async Task RevertMaterialConsumption(int productId, double producedQty, DBTHSNEntities db)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db);

            if (productionWarehouse == null)
            {
                return; // Can't revert agar warehouse not found
            }

            var consumptionService = new ProductionConsumptionService(db);
            var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(productId, producedQty);

            // Revert consumption (add back ga stock)
            foreach (var req in requirements)
            {
                var stock = await db.PRODUCT_STOCKS
                    .FirstOrDefaultAsync(s => s.WHID == productionWarehouse.ID && s.ProductID == req.MaterialID);

                if (stock != null)
                {
                    stock.Amount += req.TotalConsumption;
                    db.Entry(stock).State = EntityState.Modified;
                }
            }

            await db.SaveChangesAsync();
        }

        // AJAX so'rovi: Get planned products uchun date va shop
        [HttpGet]
        public JsonResult GetPlannedProducts(DateTime? date, int? shopID)
        {
            try
            {
                if (!date.HasValue || !shopID.HasValue)
                {
                    return Json(new { success = false, message = "Sana va sex tanlanishi kerak" }, JsonRequestBehavior.AllowGet);
                }

                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var dateOnly = date.Value.Date;
                    
                    // Olish products uchun this shop dan production plans
                    // Yuklash ma'lumot birinchi, so'ng filtrlash in memory ga avoid LINQ ga Entities Date issues
                    var allDailyPlans = db.PRODUCTPLANS_DAILY
                        .Include(d => d.PRODUCTPLAN)
                        .Include(d => d.PRODUCTPLAN.PRODUCT)
                        .Where(d => d.PRODUCTPLAN.IsDeleted == false)
                        .ToList();

                    var dailyPlans = allDailyPlans
                        .Where(d => d.Day.Date == dateOnly 
                            && d.PRODUCTPLAN.PRODUCT.ShopID == shopID.Value)
                        .Select(d => new ProductionEntryItem
                        {
                            ProductID = d.PRODUCTPLAN.ProductID,
                            ProductNo = d.PRODUCTPLAN.PRODUCT.PNo,
                            ProductName = d.PRODUCTPLAN.PRODUCT.Name,
                            PlannedDayShift = d.DayShift,
                            PlannedNightShift = d.NightShift,
                            PlannedTotal = d.Total,
                            IsPlanned = true
                        })
                        .ToList();

                    // Also get existing production entries
                    var allProductions = db.PRODUCT_PRODUCTION
                        .Include(p => p.PRODUCT)
                        .Where(p => p.ShopID == shopID.Value)
                        .ToList();

                    var existingProductions = allProductions
                        .Where(p => ProductProductionPlanDateMeta.MatchesCalendarDay(p, dateOnly))
                        .ToList();

                    foreach (var existing in existingProductions)
                    {
                        var item = dailyPlans.FirstOrDefault(d => d.ProductID == existing.ProductID);
                        if (item != null)
                        {
                            item.ID = existing.ID;
                            item.DayShift = existing.DayShift;
                            item.NightShift = existing.NightShift;
                            item.Total = existing.Total;
                            item.Comment = existing.Comment;
                        }
                        else
                        {
                            // Unplanned production
                            dailyPlans.Add(new ProductionEntryItem
                            {
                                ID = existing.ID,
                                ProductID = existing.ProductID,
                                ProductNo = existing.PRODUCT.PNo,
                                ProductName = existing.PRODUCT.Name,
                                DayShift = existing.DayShift,
                                NightShift = existing.NightShift,
                                Total = existing.Total,
                                Comment = existing.Comment,
                                IsPlanned = false
                            });
                        }
                    }

                    return Json(dailyPlans, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Xatolik: {ex.Message}" }, JsonRequestBehavior.AllowGet);
            }
        }

        // yordamchi method ga yuklash planned products
        private void LoadPlannedProducts(ProductProductionDailyEntryViewModel viewModel, DBTHSNEntities db)
        {
            var dateOnly = viewModel.Date.Date;
            
            // Olish products uchun this shop dan production plans
            // Yuklash ma'lumot birinchi, so'ng filtrlash in memory ga avoid LINQ ga Entities Date issues
            var allDailyPlans = db.PRODUCTPLANS_DAILY
                .Include(d => d.PRODUCTPLAN)
                .Include(d => d.PRODUCTPLAN.PRODUCT)
                .Where(d => d.PRODUCTPLAN.IsDeleted == false)
                .ToList();

            var dailyPlans = allDailyPlans
                .Where(d => d.Day.Date == dateOnly 
                    && d.PRODUCTPLAN.PRODUCT.ShopID == viewModel.ShopID)
                .Select(d => new ProductionEntryItem
                {
                    ProductID = d.PRODUCTPLAN.ProductID,
                    ProductNo = d.PRODUCTPLAN.PRODUCT.PNo,
                    ProductName = d.PRODUCTPLAN.PRODUCT.Name,
                    PlannedDayShift = d.DayShift,
                    PlannedNightShift = d.NightShift,
                    PlannedTotal = d.Total,
                    IsPlanned = true
                })
                .ToList();

            // Also get existing production entries
            var allProductions = db.PRODUCT_PRODUCTION
                .Include(p => p.PRODUCT)
                .Where(p => p.ShopID == viewModel.ShopID)
                .ToList();

            var existingProductions = allProductions
                .Where(p => ProductProductionPlanDateMeta.MatchesCalendarDay(p, dateOnly))
                .ToList();

            foreach (var existing in existingProductions)
            {
                var item = dailyPlans.FirstOrDefault(d => d.ProductID == existing.ProductID);
                if (item != null)
                {
                    item.ID = existing.ID;
                    item.DayShift = existing.DayShift;
                    item.NightShift = existing.NightShift;
                    item.Total = existing.Total;
                    item.Comment = existing.Comment;
                }
                else
                {
                    dailyPlans.Add(new ProductionEntryItem
                    {
                        ID = existing.ID,
                        ProductID = existing.ProductID,
                        ProductNo = existing.PRODUCT.PNo,
                        ProductName = existing.PRODUCT.Name,
                        DayShift = existing.DayShift,
                        NightShift = existing.NightShift,
                        Total = existing.Total,
                        Comment = existing.Comment,
                        IsPlanned = false
                    });
                }
            }

            viewModel.ProductionItems = dailyPlans;
        }

    }
}

