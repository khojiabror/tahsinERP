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
using tahsinERP.Services.DlocFifo;
using tahsinERP.Helpers;
using tahsinERP.Filters;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers.MES
{
    [Permission]
    public class ProductProductionController : BaseController
    {
        private const string LockedMessageEdit = "Bu ishlab chiqarish sifat nazorati yoki lot o'tkazmasi (sotuvga transfer) bilan bog'langan. O'zgartirish mumkin emas.";
        private const string LockedMessageDelete = "Bu ishlab chiqarish sifat nazorati yoki lot o'tkazmasi (sotuvga transfer) bilan bog'langan. O'chirish mumkin emas.";

        // Olish so'rovi: ProductProduction
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate, int? shopID, int? productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Parametrsiz ochilganda ham ko'rsatilgan "oxirgi 30 kun" filtri so'rovga qo'llanadi
                // (avval butun jadval yuklanardi).
                var effectiveFrom = (fromDate ?? DateTime.Now.AddDays(-30)).Date;

                var query = db.PRODUCT_PRODUCTION.AsNoTracking()
                    .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                    .Include(p => p.PRODUCT)
                    .Include(p => p.SHOP)
                    .Where(p => p.Date >= effectiveFrom);

                if (toDate.HasValue)
                {
                    // Date vaqt qismi bilan saqlanadi — tanlangan kun to'liq kirishi uchun < toDate+1
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(p => p.Date < toExclusive);
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

                ViewBag.Shops = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, shopID);
                ViewBag.Products = new SelectList(await db.PRODUCTS.AsNoTracking()
                    .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo })
                    .ToListAsync(), "ID", "PNo", productID);
                ViewBag.FromDate = effectiveFrom;
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

                ViewBag.Shops = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, viewModel.ShopID);

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
                    using (var transaction = db.Database.BeginTransaction())
                    {
                    try
                    {
                        var planDay = viewModel.Date.Date;
                        var factDay = viewModel.ProductionDate.Date;
                        var itemsToProcess = (viewModel.ProductionItems ?? new List<ProductionEntryItem>())
                            .Where(i => i.Total > 0)
                            .ToList();

                        // Faqat kerakli qatorlar: o'chirilmagan, shu sex/mahsulotlar, fakt kuni yoki reja meta kuni.
                        // Avval sexning butun tarixi (o'chirilganlari ham) yuklanardi — o'chirilgan yashirin yozuvga
                        // qoldiq qayta yozilishi mumkin edi (MES-B01, P03).
                        var productIds = itemsToProcess.Select(i => i.ProductID).Distinct().ToList();
                        var factDayEnd = factDay.AddDays(1);
                        var planDayEnd = planDay.AddDays(1);
                        // Reja kuni PlanDate ustunida (avval Comment'dagi "[RejaSana:]" matni LIKE bilan qidirilardi, MES-B20)
                        var candidateProductions = db.PRODUCT_PRODUCTION
                            .Where(p => p.ShopID == viewModel.ShopID
                                && (p.IsDeleted == false || p.IsDeleted == null)
                                && productIds.Contains(p.ProductID)
                                && ((p.Date >= factDay && p.Date < factDayEnd)
                                    || (p.PlanDate >= planDay && p.PlanDate < planDayEnd)))
                            .ToList();
                        var existingByProduct = productIds.ToDictionary(
                            pid => pid,
                            pid => candidateProductions.FirstOrDefault(p =>
                                p.ProductID == pid &&
                                (p.Date.Date == factDay || ProductProductionPlanDateMeta.PlanDay(p) == planDay)));

                        // Bog'langan (QI yoki lot o'tkazmasi bor) yozuvni Create orqali ham qayta yozib bo'lmaydi
                        var lockedProductIds = existingByProduct.Values
                            .Where(p => p != null && ProductionLockGuard.IsLocked(db, p.ID))
                            .Select(p => p.ProductID)
                            .ToList();
                        if (lockedProductIds.Any())
                        {
                            var pnos = db.PRODUCTS.Where(p => lockedProductIds.Contains(p.ID)).Select(p => p.PNo).ToList();
                            ModelState.AddModelError("", LockedMessageEdit + " Mahsulot(lar): " + string.Join(", ", pnos));
                            transaction.Rollback();
                            goto RepopulateAfterCreateError;
                        }

                        if (!itemsToProcess.Any())
                        {
                            ModelState.AddModelError("", "Kamida bitta mahsulot uchun jami miqdor (kunduzgi + tungi) 0 dan katta bo'lishi kerak.");
                            transaction.Rollback();
                            goto RepopulateAfterCreateError;
                        }

                        // Xomashyo yetarliligini oldindan tekshirish: production kiritishda bloklansin,
                        // reja yaratishda emas. Avval tekshirib olamiz, keyin yozishlarni boshlaymiz.
                        foreach (var item in itemsToProcess)
                        {
                            var existingForCheck = existingByProduct[item.ProductID];

                            var stockError = await ValidateRawMaterialStockForProductionAsync(
                                item.ProductID,
                                item.Total,
                                viewModel.ShopID,
                                db,
                                existingForCheck?.ProductID,
                                existingForCheck?.Total);

                            if (!string.IsNullOrEmpty(stockError))
                            {
                                ModelState.AddModelError("", stockError);
                                transaction.Rollback();
                                goto RepopulateAfterCreateError;
                            }
                        }

                        foreach (var item in itemsToProcess)
                        {
                            var productForLog = db.PRODUCTS.Find(item.ProductID);
                            // Bir xil reja kuni + mahsulot bo'yicha yangilash (fakt sanasi boshqacha bo'lishi mumkin)
                            var existing = existingByProduct[item.ProductID];

                            // Mavjud yozuvni to'liq bekor qilib, joriy to'liq miqdor bo'yicha qayta yozamiz
                            // (delta o'rniga). Shunday qilib avtomatik kirim/chiqim doim
                            // "ishlab chiqarilgan son × BOM" ga teng bo'ladi va eski/dublikat
                            // hujjatlar qolmaydi.
                            if (existing != null)
                            {
                                var oldProductionWH = WarehouseFlowService.GetProductionWarehouse(db, existing.ShopID);
                                if (oldProductionWH == null)
                                {
                                    throw new Exception("Ishlab chiqarish ombori topilmadi (eski qoldiqni bekor qilish uchun).");
                                }

                                WarehouseFlowService.ReverseProductionIncome(
                                    db, existing.ID, existing.ProductID, oldProductionWH.ID, User.Identity.Name);
                                await ReverseProductionConsumption(existing.ID, db);

                                // Yangilash existing
                                existing.Date = viewModel.ProductionDate;
                                existing.DayShift = item.DayShift;
                                existing.NightShift = item.NightShift;
                                existing.Total = item.Total;
                                existing.PlanDate = planDay;
                                existing.Comment = ProductProductionPlanDateMeta.StripMeta(item.Comment);
                                db.Entry(existing).State = EntityState.Modified;
                                db.SaveChanges();
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
                                    PlanDate = planDay,
                                    Comment = ProductProductionPlanDateMeta.StripMeta(item.Comment)
                                };
                                db.PRODUCT_PRODUCTION.Add(production);
                                // ID olish uchun saqlash (xomashyo chiqimni ProductionID bilan bog'lash uchun)
                                db.SaveChanges();
                                existing = production;
                            }

                            // Ishlab chiqarish → avtomatik kirim (tayyor mahsulot) + xomashyo chiqimi,
                            // joriy to'liq miqdor bo'yicha.
                            if (item.Total > 0)
                            {
                                WarehouseFlowService.IncreaseProductionStock(
                                    item.ProductID,
                                    item.Total,
                                    viewModel.ShopID,
                                    db,
                                    User.Identity.Name,
                                    existing.ID);

                                await ConsumeMaterialsForProduction(item.ProductID, item.Total, viewModel.ShopID, db, existing.ID, existing.Date);
                            }

                            if (productForLog != null)
                                LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController",
                                    $"{productForLog.PNo} uchun {item.Total} dona ishlab chiqarish kiritildi: {viewModel.ProductionDate:yyyy-MM-dd}");
                        }

                        db.SaveChanges();
                        transaction.Commit();

                        TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli saqlandi";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        var errorMessage = BuildErrorMessage(ex);
                        ModelState.AddModelError("", errorMessage);
                        TempData["Error"] = errorMessage;
                    }
                    }

                    RepopulateAfterCreateError:
                    if (!ModelState.IsValid || ModelState.Values.SelectMany(v => v.Errors).Any())
                    {
                        LoadPlannedProducts(viewModel, db);
                        ViewBag.Shops = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, viewModel.ShopID);
                        return View(viewModel);
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
                ViewBag.Shops = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, viewModel.ShopID);
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
                    .FirstOrDefault(p => p.ID == id && (p.IsDeleted == false || p.IsDeleted == null));

                if (production == null)
                {
                    return HttpNotFound();
                }

                if (ProductionLockGuard.IsLocked(db, production.ID))
                {
                    TempData["ErrorMessage"] = LockedMessageEdit;
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

                PopulateEditLookups(db, viewModel);

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
                    if (production == null || production.IsDeleted == true)
                    {
                        return HttpNotFound();
                    }

                    if (ProductionLockGuard.IsLocked(db, production.ID))
                    {
                        ModelState.AddModelError("", LockedMessageEdit);
                        PopulateEditLookups(db, viewModel);
                        return View(viewModel);
                    }

                    // Yangi natija uchun xomashyo yetarliligi: joriy yozuvning eski sarfi mavjud deb hisoblaymiz
                    // (go'yo revert qilingan) va shundan keyin yangi talabni tekshiramiz.
                    var stockError = await ValidateRawMaterialStockForProductionAsync(
                        viewModel.ProductID,
                        viewModel.Total,
                        viewModel.ShopID,
                        db,
                        production.ProductID,
                        production.Total);
                    if (!string.IsNullOrEmpty(stockError))
                    {
                        ModelState.AddModelError("", stockError);
                        PopulateEditLookups(db, viewModel);
                        return View(viewModel);
                    }

                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var oldProductionWH = WarehouseFlowService.GetProductionWarehouse(db, production.ShopID);
                            if (oldProductionWH == null)
                            {
                                throw new Exception("Ishlab chiqarish ombori topilmadi (eski qoldiqni bekor qilish uchun).");
                            }

                            // Eski kirim/DLOC lotini to'liq bekor qilamiz, shundan keyingina yangi miqdorni qo'shamiz.
                            // Shunday qilib qo'shish bosqichi ayirish muvaffaqiyatsiz bo'lgan holatda ham ishlab
                            // ketib qoldiqni ikki marta oshirib yubormaydi (transaction butunlay bekor qilinadi).
                            WarehouseFlowService.ReverseProductionIncome(
                                db, production.ID, production.ProductID, oldProductionWH.ID, User.Identity.Name);

                            await ReverseProductionConsumption(production.ID, db);

                            // Yangilash production
                            production.ProductID = viewModel.ProductID;
                            production.ShopID = viewModel.ShopID;
                            if (!production.PlanDate.HasValue || production.PlanDate.Value.Date == production.Date.Date)
                            {
                                production.PlanDate = viewModel.Date.Date;
                            }
                            production.Date = viewModel.Date;
                            production.DayShift = viewModel.DayShift;
                            production.NightShift = viewModel.NightShift;
                            production.Total = viewModel.Total;
                            production.Comment = ProductProductionPlanDateMeta.StripMeta(viewModel.Comment);

                            WarehouseFlowService.IncreaseProductionStock(
                                viewModel.ProductID,
                                viewModel.Total,
                                viewModel.ShopID,
                                db,
                                User.Identity.Name,
                                viewModel.ID);

                            await ConsumeMaterialsForProduction(viewModel.ProductID, viewModel.Total, viewModel.ShopID, db, viewModel.ID, viewModel.Date);

                            db.Entry(production).State = EntityState.Modified;
                            db.SaveChanges();
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            ModelState.AddModelError("", BuildErrorMessage(ex));
                            PopulateEditLookups(db, viewModel);
                            return View(viewModel);
                        }
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", $"Ishlab chiqarish tahrirlandi: ID {viewModel.ID}");

                    TempData["Success"] = "Ishlab chiqarish muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateEditLookups(db, viewModel);
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

                // 0) EXPENSES.ProductionID ustuni bo'yicha aniq topish (avval Description LIKE '%ProductionID:x%', MES-P12)
                var productionId = production.ID;
                var expensesByProdId = await db.EXPENSES
                    .Where(e => e.IsDeleted == false && e.ProductionID == productionId)
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                // 1) Sana oralig'ida ishlab chiqarishga bog'lanmagan chiqimlar (fallback: marker'dan oldingi eski yozuvlar)
                var expensesInRange = expensesByProdId.Any() ? new List<EXPENS>() : await db.EXPENSES
                    .Where(e => e.IsDeleted == false
                        && e.ProductionID == null
                        && e.IssueDateTime >= searchStart
                        && e.IssueDateTime < searchEnd)
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                // 1b) Izohda ishlab chiqarish sanasi va "kunlik reja asosida" bor chiqimlar (boshqa sana yaratilgan bo'lsa ham)
                var expensesByDescDate = expensesByProdId.Any() ? new List<EXPENS>() : await db.EXPENSES
                    .Where(e => e.IsDeleted == false
                        && e.ProductionID == null
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
                // Har material uchun ko'rsatish birligi bir marta olinadi (avval ikki marta so'ralardi, MES-P10)
                var displayUnitByMaterial = requirements
                    .Select(r => r.MaterialID)
                    .Distinct()
                    .ToDictionary(mid => mid, mid => consumptionService.GetDisplayUnitFromIncome(mid));
                var displayUnitIds = displayUnitByMaterial.Values.Where(u => u.HasValue).Select(u => u.Value).Distinct().ToList();
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
                    var displayUnitId = displayUnitByMaterial[req.MaterialID];
                    var unitName = displayUnitId.HasValue && units.ContainsKey(displayUnitId.Value) ? units[displayUnitId.Value] : "-";
                    viewModel.BomConsumptionLines.Add(new BomConsumptionLineViewModel
                    {
                        MaterialID = req.MaterialID,
                        MaterialPNo = req.MaterialPNo,
                        MaterialName = req.MaterialName,
                        RequiredQtyPieces = req.RequiredQtyPieces,
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

                ViewBag.ChangePermit = RoleHelper.IsChangePermitted(CurrentUserName, "ProductProduction", "Index")
                    && !ProductionLockGuard.IsLocked(db, production.ID);

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

        // POST so'rovi: ProductProduction/Delete/5
        // GET tasdiqlash sahifasi (Delete.cshtml) mavjud emas edi — o'chirish faqat POST tugma orqali (MES-B04).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var production = db.PRODUCT_PRODUCTION.Find(id);
                if (production == null || production.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                if (ProductionLockGuard.IsLocked(db, id))
                {
                    TempData["ErrorMessage"] = LockedMessageDelete;
                    return RedirectToAction("Index");
                }

                // Kirim/chiqimni bekor qilish va soft delete bitta tranzaksiyada (MES-B02):
                // o'rtada xato bo'lsa qoldiq yarim-bekor holatda qolmaydi.
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var productionWH = WarehouseFlowService.GetProductionWarehouse(db, production.ShopID);
                        if (productionWH != null)
                        {
                            // Tayyor mahsulot kirimini (kirim hujjati + DLOC lot + qoldiq) to'liq bekor qilamiz.
                            WarehouseFlowService.ReverseProductionIncome(
                                db, production.ID, production.ProductID, productionWH.ID, User.Identity.Name);
                        }
                        await ReverseProductionConsumption(production.ID, db);

                        production.IsDeleted = true;
                        db.Entry(production).State = EntityState.Modified;
                        await SoftDeleteReferenceService.AfterProductProductionSoftDeletedAsync(db, id);
                        db.SaveChanges();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = BuildErrorMessage(ex);
                        return RedirectToAction("Index");
                    }
                }

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
            int shopId,
            DBTHSNEntities db,
            int? oldProductId = null,
            double? oldProducedQty = null)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db, shopId);
            if (productionWarehouse == null)
            {
                return "Ishlab chiqarish ombori topilmadi";
            }

            // Tekshiruv mantiqi servisda (ConsumeMaterialsForProductionAsync ham shuni ishlatadi, MES-D23)
            return await new ProductionConsumptionService(db).ValidateStockForProductionAsync(
                productId, producedQty, productionWarehouse.ID, oldProductId, oldProducedQty);
        }

        /// <summary>
        /// Consumes materials dan Production warehouse when production is confirmed.
        /// uchun weight-based materials (e.g. shurup) BOM dona is converted ga kg va deducted dan stock.
        /// </summary>
        private async Task ConsumeMaterialsForProduction(int productId, double producedQty, int shopId, DBTHSNEntities db, int? productionId = null, DateTime? productionDate = null)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db, shopId);
            if (productionWarehouse == null)
            {
                throw new Exception("Ishlab chiqarish ombori topilmadi");
            }

            var consumptionService = new ProductionConsumptionService(db);
            await consumptionService.ConsumeMaterialsForProductionAsync(
                productId, producedQty, productionWarehouse.ID, productionId, productionDate);
        }

        /// <summary>
        /// Ushbu ishlab chiqarish uchun avtomatik yaratilgan xomashyo chiqimini to'liq bekor qiladi:
        /// DLOC lot + umumiy qoldiq qaytariladi va eski EXPENSE hujjatlari fizik o'chiriladi
        /// (eski/dublikat hujjatlar qolib ketmasligi uchun).
        /// </summary>
        private async Task ReverseProductionConsumption(int productionId, DBTHSNEntities db)
        {
            var consumptionService = new ProductionConsumptionService(db);
            await consumptionService.ReverseProductionConsumptionAsync(productionId);
        }

        /// <summary>
        /// Mavjud ma'lumotlarni tuzatish: har bir ishlab chiqarish uchun avtomatik xomashyo
        /// chiqimini "ishlab chiqarilgan son × BOM" ga moslab qayta hisoblaydi (faqat chiqim,
        /// tayyor mahsulot kirimi tegilmaydi). Har bir yozuv alohida tranzaksiyada, xatolar
        /// yig'ib hisobot beriladi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ReconcileProductionConsumption()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            int reconciled = 0, alreadyOk = 0, skipped = 0;
            var errors = new List<string>();

            List<int> productionIds;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                productionIds = await db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null) && p.Total > 0)
                    .OrderBy(p => p.ID)
                    .Select(p => p.ID)
                    .ToListAsync();
            }

            foreach (var pid in productionIds)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var production = await db.PRODUCT_PRODUCTION.FindAsync(pid);
                        if (production == null || production.IsDeleted == true || production.Total <= 0)
                        {
                            skipped++;
                            transaction.Rollback();
                            continue;
                        }

                        var consumptionService = new ProductionConsumptionService(db);
                        var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                            production.ProductID, production.Total);
                        var expectedTotal = requirements?.Sum(r => r.TotalConsumption) ?? 0d;

                        var productionId = production.ID;
                        var existingExpenses = await db.EXPENSES
                            .Where(e => e.IsDeleted != true
                                && e.ReceiverWhID == null
                                && e.ProductionID == productionId)
                            .ToListAsync();
                        var actualTotal = existingExpenses.Sum(e => e.Amount ?? 0d);

                        // Allaqachon to'g'ri va bitta hujjat bo'lsa, tegmaymiz.
                        if (existingExpenses.Count <= 1 && Math.Abs(actualTotal - expectedTotal) < 0.001)
                        {
                            alreadyOk++;
                            transaction.Rollback();
                            continue;
                        }

                        // Faqat chiqim qismini qayta hisoblash (tayyor mahsulot kirimi tegilmaydi).
                        await consumptionService.ReverseProductionConsumptionAsync(production.ID);

                        var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db, production.ShopID);
                        if (productionWarehouse == null)
                        {
                            throw new Exception("Ishlab chiqarish ombori topilmadi");
                        }

                        await consumptionService.ConsumeMaterialsForProductionAsync(
                            production.ProductID, production.Total, productionWarehouse.ID, production.ID, production.Date);

                        await db.SaveChangesAsync();
                        transaction.Commit();
                        reconciled++;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        var msg = ex.InnerException != null ? $"{ex.Message} | {ex.InnerException.Message}" : ex.Message;
                        errors.Add($"#{pid}: {msg}");
                    }
                }
            }

            var summary = $"Tuzatildi: {reconciled}, allaqachon to'g'ri: {alreadyOk}, o'tkazib yuborildi: {skipped}, xatolar: {errors.Count}.";
            if (errors.Any())
            {
                TempData["Error"] = summary + " " + string.Join(" | ", errors.Take(5));
            }
            else
            {
                TempData["Success"] = summary;
            }

            LogHelper.LogToDatabase(User.Identity.Name, "ProductProductionController", "ReconcileProductionConsumption: " + summary);
            return RedirectToAction("Index");
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
                    return Json(BuildPlannedProducts(db, date.Value, shopID.Value), JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return JsonError(ex);
            }
        }

        // yordamchi method ga yuklash planned products
        private static void LoadPlannedProducts(ProductProductionDailyEntryViewModel viewModel, DBTHSNEntities db)
        {
            viewModel.ProductionItems = BuildPlannedProducts(db, viewModel.Date, viewModel.ShopID);
        }

        /// <summary>
        /// Kun + sex bo'yicha rejalashtirilgan mahsulotlar va shu kunga tegishli mavjud ishlab chiqarishlar.
        /// Avval butun PRODUCTPLANS_DAILY va sexning butun PRODUCT_PRODUCTION tarixi (o'chirilganlari ham)
        /// xotiraga yuklanib filtrlanardi (MES-P02/P03, B01) — endi filtr SQL'da.
        /// </summary>
        private static List<ProductionEntryItem> BuildPlannedProducts(DBTHSNEntities db, DateTime date, int shopId)
        {
            var dateOnly = date.Date;
            var nextDay = dateOnly.AddDays(1);

            var dailyPlans = db.PRODUCTPLANS_DAILY.AsNoTracking()
                .Where(d => d.Day >= dateOnly && d.Day < nextDay
                    && d.PRODUCTPLAN.IsDeleted == false
                    && d.PRODUCTPLAN.PRODUCT.ShopID == shopId)
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

            var existingProductions = db.PRODUCT_PRODUCTION.AsNoTracking()
                .Where(p => p.ShopID == shopId
                    && (p.IsDeleted == false || p.IsDeleted == null)
                    && ((p.Date >= dateOnly && p.Date < nextDay)
                        || (p.PlanDate >= dateOnly && p.PlanDate < nextDay)))
                .Select(p => new
                {
                    p.ID,
                    p.ProductID,
                    p.Date,
                    p.DayShift,
                    p.NightShift,
                    p.Total,
                    p.Comment,
                    p.PRODUCT.PNo,
                    p.PRODUCT.Name
                })
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
                    // Rejadan tashqari ishlab chiqarish
                    dailyPlans.Add(new ProductionEntryItem
                    {
                        ID = existing.ID,
                        ProductID = existing.ProductID,
                        ProductNo = existing.PNo,
                        ProductName = existing.Name,
                        DayShift = existing.DayShift,
                        NightShift = existing.NightShift,
                        Total = existing.Total,
                        Comment = existing.Comment,
                        IsPlanned = false
                    });
                }
            }

            return dailyPlans;
        }

        private void PopulateEditLookups(DBTHSNEntities db, ProductProductionViewModel viewModel)
        {
            ViewBag.Shops = ReferenceDataCache.SelectList(ReferenceDataCache.Shops, viewModel.ShopID);
            ViewBag.Products = new SelectList(db.PRODUCTS.AsNoTracking()
                .Where(p => p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo })
                .ToList(), "ID", "PNo", viewModel.ProductID);
        }

        private static string BuildErrorMessage(Exception ex)
        {
            if (ex is WarehouseDlocException)
                return ex.Message;
            var errorMessage = $"Xatolik: {ex.Message}";
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                errorMessage += $" | Ichki xatolik: {inner.Message}";
            return errorMessage;
        }
    }
}

