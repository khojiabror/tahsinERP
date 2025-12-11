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

namespace tahsinERP.Controllers.MES
{
    public class ProductProductionController : Controller
    {
        // GET: ProductProduction
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate, int? shopID, int? productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.PRODUCT_PRODUCTION
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

        // GET: ProductProduction/Create (Daily Entry)
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

        // POST: ProductProduction/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ProductProductionDailyEntryViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        foreach (var item in viewModel.ProductionItems.Where(i => i.Total > 0))
                        {
                            // Check if production already exists for this date/product/shop
                            var existing = db.PRODUCT_PRODUCTION
                                .FirstOrDefault(p => p.Date.Date == viewModel.Date.Date 
                                    && p.ProductID == item.ProductID 
                                    && p.ShopID == viewModel.ShopID);

                            if (existing != null)
                            {
                                // Update existing
                                existing.DayShift = item.DayShift;
                                existing.NightShift = item.NightShift;
                                existing.Total = item.Total;
                                existing.Comment = item.Comment;
                                db.Entry(existing).State = EntityState.Modified;
                            }
                            else
                            {
                                // Create new
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

                            // Update warehouse (INCOMES and PRODUCT_STOCKS)
                            UpdateWarehouseForProduction(item.ProductID, item.Total, viewModel.ShopID, db);
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish kiritildi: {viewModel.Date:yyyy-MM-dd}, Sex: {viewModel.ShopID}");

                        TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli saqlandi";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Reload ViewBag on error
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

        // GET: ProductProduction/Edit/5
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

                // Check if production is tied to Quality Inspection or Sales Invoice
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

        // POST: ProductProduction/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ProductProductionViewModel viewModel)
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

                    // Check if production is tied to Quality Inspection or Sales Invoice
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

                    // Revert old warehouse entry
                    var oldTotal = production.Total;
                    UpdateWarehouseForProduction(production.ProductID, -oldTotal, production.ShopID, db);

                    // Update production
                    production.ProductID = viewModel.ProductID;
                    production.ShopID = viewModel.ShopID;
                    production.Date = viewModel.Date;
                    production.DayShift = viewModel.DayShift;
                    production.NightShift = viewModel.NightShift;
                    production.Total = viewModel.Total;
                    production.Comment = viewModel.Comment;

                    // Update warehouse with new values
                    UpdateWarehouseForProduction(viewModel.ProductID, viewModel.Total, viewModel.ShopID, db);

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

        // GET: ProductProduction/Details/5
        public ActionResult Details(int? id)
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

                return View(production);
            }
        }

        // GET: ProductProduction/Delete/5
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

                // Check if production is tied to Quality Inspection or Sales Invoice
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

        // POST: ProductProduction/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION.Find(id);
                if (production == null)
                {
                    return HttpNotFound();
                }

                // Check if production is tied to Quality Inspection or Sales Invoice
                var hasQualityInspection = db.QUALITY_INSPECTION.Any(q => q.ProductionID == id && q.IsDeleted == false);
                var hasSalesInvoice = db.SALES_INVOICE_LINES
                    .Any(l => db.PRODUCT_PRODUCTION.Any(p => p.ID == id && p.ProductID == l.ProductID));

                if (hasQualityInspection || hasSalesInvoice)
                {
                    TempData["ErrorMessage"] = "Bu ishlab chiqarish sifat nazorati yoki sotuv fakturasi bilan bog'langan. O'chirish mumkin emas.";
                    return RedirectToAction("Index");
                }

                // Revert warehouse entry
                UpdateWarehouseForProduction(production.ProductID, -production.Total, production.ShopID, db);

                db.PRODUCT_PRODUCTION.Remove(production);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        // AJAX: Get planned products for date and shop
        [HttpGet]
        public JsonResult GetPlannedProducts(DateTime date, int shopID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Get products for this shop from production plans
                var dailyPlans = db.PRODUCTPLANS_DAILY
                    .Include(d => d.PRODUCTPLAN)
                    .Include(d => d.PRODUCTPLAN.PRODUCT)
                    .Where(d => d.Day.Date == date.Date 
                        && d.PRODUCTPLAN.PRODUCT.ShopID == shopID
                        && d.PRODUCTPLAN.IsDeleted == false)
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
                var existingProductions = db.PRODUCT_PRODUCTION
                    .Include(p => p.PRODUCT)
                    .Where(p => p.Date.Date == date.Date && p.ShopID == shopID)
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

        // Helper method to load planned products
        private void LoadPlannedProducts(ProductProductionDailyEntryViewModel viewModel, DBTHSNEntities db)
        {
                // Get products for this shop from production plans
                var dailyPlans = db.PRODUCTPLANS_DAILY
                    .Include(d => d.PRODUCTPLAN)
                    .Include(d => d.PRODUCTPLAN.PRODUCT)
                    .Where(d => d.Day.Date == viewModel.Date.Date 
                        && d.PRODUCTPLAN.PRODUCT.ShopID == viewModel.ShopID
                        && d.PRODUCTPLAN.IsDeleted == false)
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
            var existingProductions = db.PRODUCT_PRODUCTION
                .Include(p => p.PRODUCT)
                .Where(p => p.Date.Date == viewModel.Date.Date && p.ShopID == viewModel.ShopID)
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

        // Helper method to update warehouse (INCOMES and PRODUCT_STOCKS)
        private void UpdateWarehouseForProduction(int productID, double quantity, int shopID, DBTHSNEntities db)
        {
            // Get default finished goods warehouse (assuming warehouse ID 1 or find by type)
            var finishedGoodsWarehouse = db.WAREHOUSES.FirstOrDefault(w => w.WHName.Contains("Finished") || w.ID == 1);
            if (finishedGoodsWarehouse == null)
            {
                finishedGoodsWarehouse = db.WAREHOUSES.FirstOrDefault();
            }

            if (finishedGoodsWarehouse == null)
            {
                throw new Exception("Ombor topilmadi");
            }

            // Create or update INCOME record
            var income = new INCOME
            {
                DocNo = $"PROD-{DateTime.Now:yyyyMMddHHmmss}",
                ReceivingWHID = finishedGoodsWarehouse.ID,
                IssueDateTime = DateTime.Now,
                CurrencyID = 1, // Default currency
                IsDeleted = false,
                RecieveStatus = true
            };

            db.INCOMES.Add(income);
            db.SaveChanges();

            // Create INCOME_PRODUCTS
            var incomeProduct = new INCOME_PRODUCTS
            {
                IncomeID = income.ID,
                ProductID = productID,
                Amount = quantity,
                PiecePrice = 0 // Production cost can be calculated separately
            };

            db.INCOME_PRODUCTS.Add(incomeProduct);

            // Update or create PRODUCT_STOCKS
            var stock = db.PRODUCT_STOCKS
                .FirstOrDefault(s => s.WHID == finishedGoodsWarehouse.ID && s.ProductID == productID);

            if (stock != null)
            {
                stock.Amount += quantity;
                db.Entry(stock).State = EntityState.Modified;
            }
            else
            {
                var product = db.PRODUCTS.Find(productID);
                var newStock = new PRODUCT_STOCKS
                {
                    WHID = finishedGoodsWarehouse.ID,
                    ProductID = productID,
                    Amount = quantity,
                    UnitID = product?.UnitID ?? 1
                };
                db.PRODUCT_STOCKS.Add(newStock);
            }

            db.SaveChanges();
        }
    }
}

