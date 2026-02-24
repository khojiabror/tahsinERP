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
                ViewBag.Products = new SelectList(await db.PRODUCTS.Where(p => p.IsDeleted == false).ToListAsync(), "ID", "PNo", productID);
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
                        var dateOnly = viewModel.Date.Date;
                        var allProductionsForDate = db.PRODUCT_PRODUCTION
                            .Where(p => p.ShopID == viewModel.ShopID)
                            .ToList();

                        foreach (var item in viewModel.ProductionItems.Where(i => i.Total > 0))
                        {
                            // Tekshirish agar production already mavjud uchun this date/product/shop
                            var existing = allProductionsForDate
                                .FirstOrDefault(p => p.Date.Date == dateOnly 
                                    && p.ProductID == item.ProductID);

                            if (existing != null)
                            {
                                // Yangilash existing
                                existing.DayShift = item.DayShift;
                                existing.NightShift = item.NightShift;
                                existing.Total = item.Total;
                                existing.Comment = item.Comment;
                                db.Entry(existing).State = EntityState.Modified;
                            }
                            else
                            {
                                // Yaratish new
                                var production = new PRODUCT_PRODUCTION
                                {
                                    ProductID = item.ProductID,
                                    ShopID = viewModel.ShopID,
                                    Date = viewModel.Date,
                                    DayShift = item.DayShift,
                                    NightShift = item.NightShift,
                                    Total = item.Total,
                                    Comment = item.Comment
                                };
                                db.PRODUCT_PRODUCTION.Add(production);
                            }

                            // Yangilash warehouse uchun finished goods (Production WH) without Income doc
                            var product = db.PRODUCTS.Find(item.ProductID);
                            if (product != null && product.IsFinishedGood == true)
                            {
                                WarehouseFlowService.IncreaseProductionStockWithoutIncome(
                                    item.ProductID, 
                                    item.Total, 
                                    viewModel.ShopID, 
                                    db, 
                                    User.Identity.Name,
                                    existing?.ID
                                );

                                // Consume raw materials dan Supply warehouse bilan BOM/NORM calculation
                                await ConsumeMaterialsForProduction(item.ProductID, item.Total, db);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish kiritildi: {viewModel.Date:yyyy-MM-dd}, Sex: {viewModel.ShopID}");

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
                        await ConsumeMaterialsForProduction(viewModel.ProductID, viewModel.Total, db);
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
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

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

                return View(production);
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
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Consumes materials dan Production warehouse when production is confirmed.
        /// uchun weight-based materials (e.g. shurup) BOM dona is converted ga kg va deducted dan stock.
        /// </summary>
        private async Task ConsumeMaterialsForProduction(int productId, double producedQty, DBTHSNEntities db)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db);
            if (productionWarehouse == null)
            {
                throw new Exception("Ishlab chiqarish ombori topilmadi");
            }

            var consumptionService = new ProductionConsumptionService(db);
            await consumptionService.ConsumeMaterialsForProductionAsync(
                productId, producedQty, productionWarehouse.ID);
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
                        .Where(p => p.Date.Date == dateOnly)
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
                .Where(p => p.Date.Date == dateOnly)
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

