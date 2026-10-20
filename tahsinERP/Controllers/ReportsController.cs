using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using System.Collections.Generic;
using System.Data.Entity;
using tahsinERP.ViewModels.Reports;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    [Permission("Reports")]
    public class ReportsController : Controller
    {
        /// <summary>
        /// /Reports — alohida sahifasi yo'q (PERMISSIONMODULES'dagi "Reports" moduli shu manzilga ishora qiladi);
        /// menyudagi birinchi hisobotga yo'naltiriladi.
        /// </summary>
        public ActionResult Index()
        {
            return RedirectToAction("Coverage");
        }

        /// <summary>
        /// ABC/XYZ segmentatsiya - xomashyolarni value va variabiliti bo'yicha klassifikatsiya.
        /// </summary>
        public async System.Threading.Tasks.Task<ActionResult> ABCSegmentation(int? warehouseId, DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var warehouses = ReferenceDataCache.Get(ReferenceDataCache.Warehouses).OrderBy(w => w.Name).ToList();

                if (!warehouses.Any())
                {
                    return View(new SRMABCSegmentationViewModel
                    {
                        Warehouses = new List<SelectListItem>(),
                        FromDate = DateTime.Today.AddDays(-90),
                        ToDate = DateTime.Today
                    });
                }

                var selectedWarehouseId = warehouseId ?? warehouses.First().ID;
                var from = (fromDate ?? DateTime.Today.AddDays(-90)).Date;
                var to = (toDate ?? DateTime.Today).Date;
                if (to < from) to = from;

                var days = (to - from).Days + 1;
                if (days <= 0) days = 1;

                // Faqat tanlangan ombordan chiqqan xomashyo chiqimlari
                var lines = await LoadDemandLinesAsync(db, selectedWarehouseId, from, to);

                if (!lines.Any())
                {
                    var emptyModel = new SRMABCSegmentationViewModel
                    {
                        SelectedWarehouseId = selectedWarehouseId,
                        Warehouses = WarehouseItems(warehouses, selectedWarehouseId),
                        FromDate = from,
                        ToDate = to,
                        Items = new List<SRMABCItemViewModel>()
                    };
                    return View(emptyModel);
                }

                var demandByProduct = lines
                    .GroupBy(x => x.ProductID)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            Total = g.Sum(x => x.Amount),
                            Daily = g.GroupBy(x => x.Date)
                                     .ToDictionary(gg => gg.Key, gg => gg.Sum(x => x.Amount))
                        });

                var productIds = demandByProduct.Keys.ToList();
                var products = await db.PRODUCTS
                    .Include(p => p.UNIT)
                    .Where(p => productIds.Contains(p.ID))
                    .ToListAsync();
                var productDict = products.ToDictionary(p => p.ID);

                var totalAll = demandByProduct.Values.Sum(v => v.Total);
                if (totalAll <= 0) totalAll = 1;

                var items = new List<SRMABCItemViewModel>();
                foreach (var kvp in demandByProduct)
                {
                    var pid = kvp.Key;
                    var v = kvp.Value;

                    productDict.TryGetValue(pid, out var p);

                    // Daily demand statistics
                    var dailyValues = DailySeries(v.Daily, from, days);
                    var avg = dailyValues.Average();
                    var std = StdDev(dailyValues, avg);

                    var cov = avg > 0 ? std / avg : 0;

                    items.Add(new SRMABCItemViewModel
                    {
                        ProductId = pid,
                        ProductCode = p?.PNo ?? "N/A",
                        ProductName = p?.Name ?? "N/A",
                        UnitName = p?.UNIT?.ShortName ?? "N/A",
                        TotalDemand = v.Total,
                        CoefficientOfVariation = cov
                    });
                }

                // ABC by cumulative share
                var orderedByValue = items.OrderByDescending(i => i.TotalDemand).ToList();
                double cum = 0;
                foreach (var item in orderedByValue)
                {
                    var share = item.TotalDemand / totalAll * 100.0;
                    cum += share;
                    item.SharePercent = share;
                    item.CumSharePercent = cum;

                    item.ABCClass = AbcClass(cum);
                    item.XYZClass = XyzClass(item.CoefficientOfVariation);
                }

                var model = new SRMABCSegmentationViewModel
                {
                    SelectedWarehouseId = selectedWarehouseId,
                    Warehouses = WarehouseItems(warehouses, selectedWarehouseId),
                    FromDate = from,
                    ToDate = to,
                    Items = orderedByValue
                };

                return View(model);
            }
        }

        /// <summary>
        /// Safety stock va reorder point hisobi (per xomashyo, ombor bo'yicha). Kategoriya va mahsulot turi filtri bilan.
        /// </summary>
        public async System.Threading.Tasks.Task<ActionResult> SafetyStock(int? warehouseId, int? categoryId, string productType, DateTime? fromDate, DateTime? toDate, int serviceLevel = 95, int leadTimeDays = 14)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (leadTimeDays <= 0) leadTimeDays = 1;

            using (var db = new DBTHSNEntities())
            {
                var warehouses = ReferenceDataCache.Get(ReferenceDataCache.Warehouses).OrderBy(w => w.Name).ToList();

                if (!warehouses.Any())
                {
                    return View(new SRMSafetyStockViewModel
                    {
                        Warehouses = new List<SelectListItem>(),
                        Categories = new List<SelectListItem>(),
                        FromDate = DateTime.Today.AddDays(-90),
                        ToDate = DateTime.Today,
                        ServiceLevel = serviceLevel,
                        LeadTimeDays = leadTimeDays
                    });
                }

                var selectedWarehouseId = warehouseId ?? warehouses.First().ID;
                var from = (fromDate ?? DateTime.Today.AddDays(-90)).Date;
                var to = (toDate ?? DateTime.Today).Date;
                if (to < from) to = from;

                var days = (to - from).Days + 1;
                if (days <= 0) days = 1;

                var lines = await LoadDemandLinesAsync(db, selectedWarehouseId, from, to);

                var demandByProduct = lines
                    .GroupBy(x => x.ProductID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(x => x.Date)
                              .ToDictionary(gg => gg.Key, gg => gg.Sum(x => x.Amount)));

                var productIds = demandByProduct.Keys.ToList();
                var products = await db.PRODUCTS
                    .Include(p => p.UNIT)
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => productIds.Contains(p.ID))
                    .ToListAsync();

                // Kategoriya va mahsulot turi bo'yicha filtrlash
                if (categoryId.HasValue)
                {
                    productIds = productIds.Where(pid =>
                    {
                        var p = products.FirstOrDefault(pr => pr.ID == pid);
                        return p != null && p.CategoryID == categoryId.Value;
                    }).ToList();
                }
                if (!string.IsNullOrWhiteSpace(productType))
                {
                    var pt = productType.ToLowerInvariant();
                    if (pt == "raw")
                    {
                        productIds = productIds.Where(pid =>
                        {
                            var p = products.FirstOrDefault(pr => pr.ID == pid);
                            return p != null && (p.IsRawMaterial == true || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsRawMaterial == true));
                        }).ToList();
                    }
                    else if (pt == "finished")
                    {
                        productIds = productIds.Where(pid =>
                        {
                            var p = products.FirstOrDefault(pr => pr.ID == pid);
                            return p != null && p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true;
                        }).ToList();
                    }
                }

                var productDict = products.ToDictionary(p => p.ID);

                // Z-value for given service level (approx)
                double z;
                if (serviceLevel >= 99) z = 2.33;
                else if (serviceLevel >= 98) z = 2.05;
                else if (serviceLevel >= 97) z = 1.88;
                else if (serviceLevel >= 95) z = 1.65;
                else if (serviceLevel >= 90) z = 1.28;
                else z = 1.0;

                var items = new List<SRMSafetyStockItemViewModel>();

                foreach (var kvp in demandByProduct)
                {
                    var pid = kvp.Key;
                    if (!productIds.Contains(pid)) continue;
                    var dailyDict = kvp.Value;

                    var dailyValues = DailySeries(dailyDict, from, days);
                    var avg = dailyValues.Average();
                    if (avg <= 0) continue; // talab yo'q bo'lsa hisoblash shart emas

                    var std = StdDev(dailyValues, avg);

                    var safety = z * std * Math.Sqrt(leadTimeDays);
                    var reorderPoint = avg * leadTimeDays + safety;
                    double? safetyDoh = null;
                    if (avg > 0)
                    {
                        safetyDoh = safety / avg;
                    }

                    productDict.TryGetValue(pid, out var p);

                    items.Add(new SRMSafetyStockItemViewModel
                    {
                        ProductId = pid,
                        ProductCode = p?.PNo ?? "N/A",
                        ProductName = p?.Name ?? "N/A",
                        UnitName = p?.UNIT?.ShortName ?? "N/A",
                        AverageDailyDemand = avg,
                        StdDevDailyDemand = std,
                        SafetyStock = safety,
                        ReorderPoint = reorderPoint,
                        SafetyDOH = safetyDoh
                    });
                }

                var categories = await db.PRODUCT_CATEGORIES
                    .Where(c => c.IsDeleted != true)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var model = new SRMSafetyStockViewModel
                {
                    SelectedWarehouseId = selectedWarehouseId,
                    Warehouses = WarehouseItems(warehouses, selectedWarehouseId),
                    CategoryId = categoryId,
                    Categories = new[]
                    {
                        new SelectListItem { Value = "", Text = "Barchasi", Selected = !categoryId.HasValue }
                    }.Concat(categories.Select(c => new SelectListItem
                    {
                        Value = c.ID.ToString(),
                        Text = c.Name,
                        Selected = categoryId.HasValue && c.ID == categoryId.Value
                    })),
                    ProductType = productType,
                    FromDate = from,
                    ToDate = to,
                    ServiceLevel = serviceLevel,
                    LeadTimeDays = leadTimeDays,
                    Items = items.OrderByDescending(i => i.AverageDailyDemand).ToList()
                };

                return View(model);
            }
        }

        /// <summary>
        /// Coverage report - xomashyo zaxirasi kunlarda (world-class ERP supply chain uslubida)
        /// Ombor bo'yicha, kategoriya va mahsulot turi filtrlari bilan, reja va fakt ishlab chiqarishdan kelib chiqib hisoblanadi.
        /// </summary>
        public async System.Threading.Tasks.Task<ActionResult> Coverage(int? warehouseId, int? categoryId, string productType, string viewMode, DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var warehouses = ReferenceDataCache.Get(ReferenceDataCache.Warehouses).OrderBy(w => w.Name).ToList();

                if (!warehouses.Any())
                {
                    return View(new SRMCoverageViewModel
                    {
                        Warehouses = new List<SelectListItem>(),
                        FromDate = DateTime.Today,
                        ToDate = DateTime.Today
                    });
                }

                var selectedWarehouseId = warehouseId ?? warehouses.First().ID;
                var from = (fromDate ?? DateTime.Today).Date;
                var to = (toDate ?? DateTime.Today.AddDays(30)).Date;
                if (to < from) to = from;

                var dates = Enumerable.Range(0, (to - from).Days + 1)
                    .Select(offset => from.AddDays(offset))
                    .ToList();
                var days = dates.Count;

                // View mode bo'yicha ko'rsatiladigan sanalar (kunlik / haftalik / oylik)
                var mode = string.IsNullOrWhiteSpace(viewMode) ? "daily" : viewMode.ToLowerInvariant();
                var effectiveDates = new List<DateTime>();
                for (int i = 0; i < dates.Count; i++)
                {
                    var d = dates[i];
                    bool include = false;
                    if (mode == "weekly")
                    {
                        var offset = (d.Date - from.Date).Days;
                        include = (offset % 7 == 0) || (i == dates.Count - 1);
                    }
                    else if (mode == "monthly")
                    {
                        include = d.Day == 1 || i == dates.Count - 1;
                    }
                    else // default: daily
                    {
                        include = true;
                    }

                    if (include)
                    {
                        effectiveDates.Add(d);
                    }
                }
                var effectiveDateSet = new HashSet<DateTime>(effectiveDates);

                // 1. Ombordagi xomashyo qoldiqlari (tanlangan ombor)
                var stockQuery = await db.PRODUCT_STOCKS
                    .AsNoTracking()
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Where(s => s.WHID == selectedWarehouseId && s.Amount > 0)
                    .ToListAsync();

                var rawStocks = stockQuery
                    .Where(s => s.PRODUCT != null && (
                        s.PRODUCT.IsRawMaterial == true ||
                        (s.PRODUCT.PRODUCT_CATEGORIES != null && s.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial == true)))
                    .ToList();

                // Kategoriya bo'yicha filtrlash
                if (categoryId.HasValue)
                {
                    rawStocks = rawStocks
                        .Where(s => s.PRODUCT != null && s.PRODUCT.CategoryID == categoryId.Value)
                        .ToList();
                }

                // Mahsulot turi bo'yicha filtrlash (tayyor / xomashyo) - coverage uchun faqat xomashyo yoki barchasi
                if (!string.IsNullOrWhiteSpace(productType))
                {
                    var pt = productType.ToLowerInvariant();
                    if (pt == "raw")
                    {
                        rawStocks = rawStocks
                            .Where(s => s.PRODUCT != null && (
                                s.PRODUCT.IsRawMaterial == true ||
                                (s.PRODUCT.PRODUCT_CATEGORIES != null && s.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial == true)))
                            .ToList();
                    }
                    else if (pt == "finished")
                    {
                        // Agar foydalanuvchi "Tayyor mahsulot"ni tanlasa, xomashyo qamrovi faqat tayyor mahsulot bilan bog'liq kategoriyalar bo'yicha cheklanadi.
                        rawStocks = rawStocks
                            .Where(s => s.PRODUCT != null && s.PRODUCT.PRODUCT_CATEGORIES != null && s.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood == true)
                            .ToList();
                    }
                }

                var rawIds = rawStocks.Select(s => s.ProductID).Distinct().ToList();

                // 2. Kunlik talab statistikasi (EXPENSES) - ABC/XYZ va Safety DOH uchun
                var lines = await LoadDemandLinesAsync(db, selectedWarehouseId, from, to);

                var demandByProduct = lines
                    .GroupBy(x => x.ProductID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(x => x.Date)
                              .ToDictionary(gg => gg.Key, gg => gg.Sum(x => x.Amount)));

                // ABC/XYZ va Safety DOH faqat talab bo'lgan mahsulotlar uchun
                double totalAllDemand = demandByProduct.Values.Sum(v => v.Values.Sum());
                if (totalAllDemand <= 0) totalAllDemand = 1;

                // Z-value va lead time (Coverage uchun default, SL ~95%)
                const int coverageLeadTimeDays = 14;
                const double z = 1.65;

                var demandStatsItems = new List<DemandClassification>();
                foreach (var kvp in demandByProduct)
                {
                    var pid = kvp.Key;
                    var dailyDict = kvp.Value;

                    var dailyValues = DailySeries(dailyDict, from, days);
                    var avg = dailyValues.Average();
                    var std = StdDev(dailyValues, avg);

                    var totalDemand = dailyValues.Sum();
                    var cov = avg > 0 ? std / avg : 0;
                    double? safetyDoh = null;
                    if (avg > 0)
                    {
                        var safety = z * std * Math.Sqrt(coverageLeadTimeDays);
                        safetyDoh = safety / avg;
                    }

                    demandStatsItems.Add(new DemandClassification
                    {
                        ProductId = pid,
                        TotalDemand = totalDemand,
                        CoefficientOfVariation = cov,
                        SafetyDOH = safetyDoh
                    });
                }

                // ABC klassifikatsiya
                var orderedByDemand = demandStatsItems
                    .OrderByDescending(i => i.TotalDemand)
                    .ToList();

                double cumShare = 0;
                foreach (var item in orderedByDemand)
                {
                    var share = item.TotalDemand / totalAllDemand * 100.0;
                    cumShare += share;
                    item.SharePercent = share;
                    item.CumSharePercent = cumShare;

                    item.ABCClass = AbcClass(cumShare);
                    item.XYZClass = XyzClass(item.CoefficientOfVariation);
                }

                var demandLookup = orderedByDemand.ToDictionary(i => i.ProductId);

                // 3. Aktiv BOMlar - tayyor mahsulot -> xomashyo
                // Komponent kategoriyasi ham birga (avval har satrda lazy-load — N+1, CORE-P13)
                var activeBoms = await db.BOM_HEADERS
                    .AsNoTracking()
                    .Include(b => b.BOM_LINES.Select(l => l.PRODUCT.PRODUCT_CATEGORIES))
                    .Where(b => b.IsActive == true && b.ProductID.HasValue)
                    .ToListAsync();

                var finishedToRaw = new Dictionary<int, List<BomComponent>>();
                foreach (var bom in activeBoms)
                {
                    if (!bom.ProductID.HasValue) continue;
                    var fgId = bom.ProductID.Value;
                    foreach (var line in bom.BOM_LINES)
                    {
                        if (!line.ComponentID.HasValue || !line.Qty.HasValue) continue;
                        var component = line.PRODUCT;
                        if (component == null) continue;

                        var isRaw = component.IsRawMaterial == true ||
                            (component.PRODUCT_CATEGORIES != null && component.PRODUCT_CATEGORIES.IsRawMaterial == true);
                        if (!isRaw) continue;

                        if (!finishedToRaw.ContainsKey(fgId))
                            finishedToRaw[fgId] = new List<BomComponent>();

                        finishedToRaw[fgId].Add(new BomComponent
                        {
                            RawProductId = line.ComponentID.Value,
                            QtyPerUnit = (double)line.Qty.Value,
                            UnitID = line.UnitID ?? component.UnitID
                        });
                    }
                }

                // 4. Reja - PRODUCTPLANS_DAILY
                var plans = await db.PRODUCTPLANS_DAILY
                    .AsNoTracking()
                    .Include(p => p.PRODUCTPLAN)
                    .Where(p => p.Day >= from && p.Day <= to && p.PRODUCTPLAN.IsDeleted == false)
                    .ToListAsync();

                var planByDateAndFg = plans
                    .GroupBy(p => p.Day.Date)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(x => x.PRODUCTPLAN.ProductID)
                              .ToDictionary(
                                  gg => gg.Key,
                                  gg => gg.Sum(x => x.Total)));

                // 5. Fakt ishlab chiqarish - PRODUCT_PRODUCTION (object join orqali reja kuniga attributsiya)
                var rangeStart = from;
                var rangeEnd = to;
                var prodFrom = from.AddMonths(-6);
                var prodTo = to.AddMonths(6);
                var linkedProductionIds = await (
                    from qi in db.QUALITY_INSPECTION
                    join pd in db.PRODUCTPLANS_DAILY on qi.ProductPlanDailyID equals pd.ID
                    where qi.IsDeleted == false
                          && qi.ProductionID.HasValue
                          && pd.Day >= rangeStart
                          && pd.Day <= rangeEnd
                    select qi.ProductionID.Value
                ).Distinct().ToListAsync();

                // Faqat kerakli 4 ustun (12 oylik oynada to'liq qatorlar o'rniga)
                var productions = await db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == null || p.IsDeleted == false)
                        && ((p.Date >= prodFrom && p.Date <= prodTo)
                            || linkedProductionIds.Contains(p.ID)))
                    .Select(p => new { p.ID, p.ProductID, p.Date, p.Total })
                    .ToListAsync();

                var productionIds = productions.Select(p => p.ID).ToList();
                var attributionDateByProductionId = new Dictionary<int, DateTime>();
                if (productionIds.Any())
                {
                    var links = await (
                        from qi in db.QUALITY_INSPECTION
                        join pd in db.PRODUCTPLANS_DAILY on qi.ProductPlanDailyID equals pd.ID
                        where qi.IsDeleted == false
                              && qi.ProductionID.HasValue
                              && productionIds.Contains(qi.ProductionID.Value)
                        select new
                        {
                            ProductionID = qi.ProductionID.Value,
                            PlanDay = DbFunctions.TruncateTime(pd.Day),
                            Qty = (double?)qi.QtyInspected ?? 0
                        }
                    ).ToListAsync();

                    attributionDateByProductionId = links
                        .Where(x => x.PlanDay.HasValue)
                        .GroupBy(x => x.ProductionID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.GroupBy(x => x.PlanDay.Value)
                                  .OrderByDescending(dayGroup => dayGroup.Sum(v => v.Qty))
                                  .ThenByDescending(dayGroup => dayGroup.Key)
                                  .Select(dayGroup => dayGroup.Key)
                                  .FirstOrDefault());
                }

                var factByDateAndFg = productions
                    .Where(p =>
                    {
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
                        return attributedDate >= from.Date && attributedDate <= to.Date;
                    })
                    .GroupBy(p =>
                    {
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
                        return attributedDate;
                    })
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(x => x.ProductID)
                              .ToDictionary(
                                  gg => gg.Key,
                                  gg => gg.Sum(x => x.Total)));

                // 5.1 Xomashyo birligi: har bir raw uchun ombor qoldig'i birligi (yoki mahsulot default birligi)
                var rawIdsFromBom = finishedToRaw.Values.SelectMany(list => list.Select(c => c.RawProductId)).Distinct().ToList();
                var allRawIdsList = rawIds.Union(rawIdsFromBom).Distinct().ToList();
                var rawProducts = await db.PRODUCTS
                    .Include(p => p.UNIT)
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => allRawIdsList.Contains(p.ID))
                    .ToListAsync();
                var rawProductDict = rawProducts.ToDictionary(p => p.ID);

                var rawIdToStockUnitId = new Dictionary<int, int>();
                foreach (var rid in allRawIdsList)
                {
                    var st = rawStocks.FirstOrDefault(s => s.ProductID == rid);
                    PRODUCT pr;
                    rawProductDict.TryGetValue(rid, out pr);
                    rawIdToStockUnitId[rid] = st?.UnitID ?? pr?.UnitID ?? 1;
                }

                var unitConversionService = new UnitConversionService(db);

                // 6. Reja/Faktni xomashyo darajasiga proyeksiya qilish (ombor birligiga o'tkazib)
                var planRawByDate = new Dictionary<DateTime, Dictionary<int, double>>();
                var factRawByDate = new Dictionary<DateTime, Dictionary<int, double>>();

                foreach (var date in dates)
                {
                    var planForDate = new Dictionary<int, double>();
                    if (planByDateAndFg.ContainsKey(date))
                    {
                        foreach (var fg in planByDateAndFg[date])
                        {
                            if (!finishedToRaw.ContainsKey(fg.Key)) continue;
                            foreach (var comp in finishedToRaw[fg.Key])
                            {
                                if (!planForDate.ContainsKey(comp.RawProductId))
                                    planForDate[comp.RawProductId] = 0;
                                PRODUCT compProduct;
                                rawProductDict.TryGetValue(comp.RawProductId, out compProduct);
                                var fromUnitId = comp.UnitID ?? compProduct?.UnitID ?? 1;
                                int toUnitId;
                                if (!rawIdToStockUnitId.TryGetValue(comp.RawProductId, out toUnitId)) toUnitId = 1;
                                var qtyInSource = fg.Value * (decimal)comp.QtyPerUnit;
                                var qtyInStockUnit = await unitConversionService.ConvertToUnitAsync(comp.RawProductId, qtyInSource, fromUnitId, toUnitId);
                                planForDate[comp.RawProductId] += (double)qtyInStockUnit;
                            }
                        }
                    }

                    var factForDate = new Dictionary<int, double>();
                    if (factByDateAndFg.ContainsKey(date))
                    {
                        foreach (var fg in factByDateAndFg[date])
                        {
                            if (!finishedToRaw.ContainsKey(fg.Key)) continue;
                            foreach (var comp in finishedToRaw[fg.Key])
                            {
                                if (!factForDate.ContainsKey(comp.RawProductId))
                                    factForDate[comp.RawProductId] = 0;
                                PRODUCT compProductFact;
                                rawProductDict.TryGetValue(comp.RawProductId, out compProductFact);
                                var fromUnitIdFact = comp.UnitID ?? compProductFact?.UnitID ?? 1;
                                int toUnitIdFact;
                                if (!rawIdToStockUnitId.TryGetValue(comp.RawProductId, out toUnitIdFact)) toUnitIdFact = 1;
                                var qtyInSourceFact = fg.Value * (decimal)comp.QtyPerUnit;
                                var qtyInStockUnitFact = await unitConversionService.ConvertToUnitAsync(comp.RawProductId, qtyInSourceFact, fromUnitIdFact, toUnitIdFact);
                                factForDate[comp.RawProductId] += (double)qtyInStockUnitFact;
                            }
                        }
                    }

                    planRawByDate[date] = planForDate;
                    factRawByDate[date] = factForDate;
                }

                // 7. Satrlarga yig'ish
                var rows = new List<SRMCoverageRowViewModel>();
                var rawIdsAll = new HashSet<int>(allRawIdsList);

                foreach (var rawId in rawIdsAll)
                {
                    rawProductDict.TryGetValue(rawId, out var product);
                    var stock = rawStocks.FirstOrDefault(s => s.ProductID == rawId);
                    var currentStock = stock != null ? stock.Amount : 0;

                    double totalPlan = 0;
                    double totalFact = 0;
                    var dayCells = new List<SRMCoverageDayCellViewModel>();
                    double cumulativeDemand = 0;

                    foreach (var date in dates)
                    {
                        var plan = planRawByDate[date].ContainsKey(rawId) ? planRawByDate[date][rawId] : 0;
                        var fact = factRawByDate[date].ContainsKey(rawId) ? factRawByDate[date][rawId] : 0;
                        totalPlan += plan;
                        totalFact += fact;

                        // World-class net requirement: cumulative plan minus cumulative fact
                        var remainingPlan = Math.Max(totalPlan - totalFact, 0);
                        cumulativeDemand = remainingPlan;

                        if (effectiveDateSet.Contains(date))
                        {
                            double? coverage = null;
                            if (cumulativeDemand > 0)
                            {
                                coverage = (double)currentStock / cumulativeDemand;
                            }

                            dayCells.Add(new SRMCoverageDayCellViewModel
                            {
                                Date = date,
                                CoverageInDays = coverage
                            });
                        }
                    }

                    // ABC/XYZ va Safety DOH ma'lumotlarini qo'shish (agar mavjud bo'lsa)
                    demandLookup.TryGetValue(rawId, out var demandInfo);

                    // Faqat ma'noli satrlar (stock > 0 yoki reja/fakt bor)
                    if (currentStock > 0 || totalPlan > 0 || totalFact > 0)
                    {
                        rows.Add(new SRMCoverageRowViewModel
                        {
                            ProductId = rawId,
                            ProductCode = product?.PNo ?? "N/A",
                            ProductName = product?.Name ?? "N/A",
                            UnitName = product?.UNIT?.ShortName ?? stock?.UNIT?.ShortName ?? "N/A",
                            CurrentStock = (double)currentStock,
                            PlannedTotal = totalPlan,
                            ActualTotal = totalFact,
                            ABCClass = demandInfo?.ABCClass,
                            XYZClass = demandInfo?.XYZClass,
                            SafetyDOH = demandInfo?.SafetyDOH,
                            DayCells = dayCells
                        });
                    }
                }

                // Kategoriya ro'yxati
                var categories = await db.PRODUCT_CATEGORIES
                    .Where(c => c.IsDeleted != true)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var model = new SRMCoverageViewModel
                {
                    SelectedWarehouseId = selectedWarehouseId,
                    Warehouses = WarehouseItems(warehouses, selectedWarehouseId),
                    CategoryId = categoryId,
                    Categories = new[]
                        {
                            new SelectListItem { Value = "", Text = "Barchasi", Selected = !categoryId.HasValue }
                        }.Concat(categories.Select(c => new SelectListItem
                        {
                            Value = c.ID.ToString(),
                            Text = c.Name,
                            Selected = categoryId.HasValue && c.ID == categoryId.Value
                        })),
                    ProductType = productType,
                    ViewMode = mode,
                    FromDate = from,
                    ToDate = to,
                    Dates = effectiveDates,
                    Rows = rows.OrderBy(r => r.ProductName).ToList()
                };

                return View(model);
            }
        }

        /// <summary>
        /// Ombordan chiqim satrlari (ABC, SafetyStock, Coverage uchun umumiy). Faqat 3 ustun proyeksiya qilinadi
        /// (avval EXPENSES + EXPENSE_PRODUCTS to'liq, kuzatiladigan entity bo'lib yuklanardi). CORE-P14: oxirgi kun
        /// to'liq kiradi (avval "IssueDateTime &lt;= to" kun boshida kesilardi).
        /// </summary>
        private static async System.Threading.Tasks.Task<List<DemandLine>> LoadDemandLinesAsync(DBTHSNEntities db, int warehouseId, DateTime from, DateTime to)
        {
            var toExclusive = to.Date.AddDays(1);
            var rows = await db.EXPENSES
                .Where(e => e.IssueDateTime >= from && e.IssueDateTime < toExclusive &&
                            e.SenderWHID == warehouseId &&
                            e.IsDeleted != true)
                .SelectMany(e => e.EXPENSE_PRODUCTS.Select(ep => new { ep.ProductID, e.IssueDateTime, ep.Amount }))
                .ToListAsync();

            return rows.Select(r => new DemandLine { ProductID = r.ProductID, Date = r.IssueDateTime.Date, Amount = (double)r.Amount }).ToList();
        }

        private class DemandLine
        {
            public int ProductID { get; set; }
            public DateTime Date { get; set; }
            public double Amount { get; set; }
        }

        // --- Talab statistikasi (ABC, SafetyStock, Coverage uchun umumiy; avval har birida ko'chirilgan) ---

        /// <summary>Davrdagi har kun uchun talab (yo'q kunlar 0).</summary>
        private static List<double> DailySeries(IDictionary<DateTime, double> daily, DateTime from, int days)
        {
            return Enumerable.Range(0, days)
                .Select(offset => daily.TryGetValue(from.AddDays(offset), out var v) ? v : 0.0)
                .ToList();
        }

        private static double StdDev(IList<double> values, double avg)
        {
            if (values.Count <= 1) return 0;
            return Math.Sqrt(values.Average(x => Math.Pow(x - avg, 2)));
        }

        /// <summary>Kumulyativ ulush (%) bo'yicha: A ≤ 80, B ≤ 95, qolgani C.</summary>
        private static string AbcClass(double cumulativePercent)
        {
            return cumulativePercent <= 80 ? "A" : cumulativePercent <= 95 ? "B" : "C";
        }

        /// <summary>Variatsiya koeffitsienti bo'yicha: X ≤ 0,1, Y ≤ 0,25, qolgani Z.</summary>
        private static string XyzClass(double cov)
        {
            return cov <= 0.1 ? "X" : cov <= 0.25 ? "Y" : "Z";
        }

        // Hisobotlardagi ombor tanlovi (avval 4 joyda qo'lda; ro'yxat ReferenceDataCache'dan)
        private static List<SelectListItem> WarehouseItems(IEnumerable<RefItem> warehouses, int selectedId)
        {
            return warehouses.Select(w => new SelectListItem
            {
                Value = w.ID.ToString(),
                Text = w.Name,
                Selected = w.ID == selectedId
            }).ToList();
        }

        private class BomComponent
        {
            public int RawProductId { get; set; }
            public double QtyPerUnit { get; set; }
            public int? UnitID { get; set; }
        }

        private class DemandClassification
        {
            public int ProductId { get; set; }
            public double TotalDemand { get; set; }
            public double CoefficientOfVariation { get; set; }
            public double? SafetyDOH { get; set; }
            public double SharePercent { get; set; }
            public double CumSharePercent { get; set; }
            public string ABCClass { get; set; }
            public string XYZClass { get; set; }
        }
    }
}
