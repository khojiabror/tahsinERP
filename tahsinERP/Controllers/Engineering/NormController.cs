using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;
using Newtonsoft.Json;

namespace tahsinERP.Controllers.Engineering
{
    public class NormController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();

        // Olish so'rovi: Norm
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Product lar ro'yxatini ko'rsatish (IsFinishedGood == true)
            var productsList = await db.PRODUCTS
                .Where(p => p.IsFinishedGood == true && p.IsDeleted != true)
                .OrderBy(p => p.PNo)
                .ToListAsync();

            // Extract product IDs as primitive list
            var productIds = productsList.Select(p => p.ID).ToList();

            // Count norms uchun har bir product
            var normCounts = await db.NORMVERSIONS
                .Where(n => n.ProductID.HasValue
                    && productIds.Contains(n.ProductID.Value)
                    && n.IsDeleted != true)
                .GroupBy(n => n.ProductID.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var products = productsList.Select(p => new ProductNormIndexViewModel
            {
                ProductID = p.ID,
                ProductCode = p.PNo,
                ProductName = p.Name,
                NormCount = normCounts.ContainsKey(p.ID) ? normCounts[p.ID] : 0
            }).ToList();

            return View(products);
        }

        // Olish so'rovi: Norm/Details/{productId}
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!id.HasValue)
            {
                TempData["Error"] = "Mahsulot ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            var product = await db.PRODUCTS.FindAsync(id.Value);
            if (product == null)
            {
                TempData["Error"] = "Mahsulot topilmadi.";
                return RedirectToAction("Index");
            }

            // Bu product uchun barcha norm versiyalari
            var normVersions = await db.NORMVERSIONS
                .Include(n => n.NORMLINES)
                .Where(n => n.ProductID == id.Value && n.IsDeleted != true)
                .OrderByDescending(n => n.EffectiveFrom)
                .ToListAsync();

            var viewModel = new ProductNormDetailsViewModel
            {
                ProductID = product.ID,
                ProductCode = product.PNo,
                ProductName = product.Name,
                NormVersions = normVersions.Select(n => 
                {
                    var period = n.EffectiveFrom.HasValue ? n.EffectiveFrom.Value.ToString("yyyy-MM") : "";
                    
                    // Calculate sums dan NORM_INPUT_LINES uchun this period
                    var inputLines = db.NORM_INPUT_LINES
                        .Where(il => il.ProductID == id.Value && il.Month == period)
                        .ToList();
                    
                    decimal totalSarfSumma = 0;
                    decimal totalChiqindiSumma = 0;
                    foreach (var il in inputLines)
                    {
                        if (il.Price <= 0)
                        {
                            continue;
                        }

                        decimal consumptionMeters = 0;
                        if (il.Consumption.HasValue && il.Consumption.Value > 0)
                        {
                            consumptionMeters = (decimal)(il.Consumption.Value / 1000.0);
                        }
                        else if (il.CutLengthMm > 0)
                        {
                            consumptionMeters = (decimal)(il.CutLengthMm / 1000.0) * il.PiecesPerProduct;
                        }
                        else
                        {
                            consumptionMeters = il.PiecesPerProduct;
                        }

                        decimal wasteMeters = 0;
                        if (il.WasteAmount.HasValue && il.WasteAmount.Value > 0)
                        {
                            wasteMeters = (decimal)(il.WasteAmount.Value / 1000.0);
                        }

                        totalSarfSumma += consumptionMeters * il.Price;
                        totalChiqindiSumma += wasteMeters * il.Price;
                    }
                    
                    // Umumiy summa: A + B
                    decimal umumiySumma = totalSarfSumma + totalChiqindiSumma;
                    
                    return new NormVersionItemViewModel
                    {
                        NormVersionID = n.ID,
                        Period = period,
                        EffectiveFrom = n.EffectiveFrom,
                        EffectiveTo = n.EffectiveTo,
                        Comment = n.Comment,
                        LineCount = n.NORMLINES.Count,
                        TotalSarfSumma = totalSarfSumma,
                        TotalChiqindiSumma = totalChiqindiSumma,
                        UmumiySumma = umumiySumma
                    };
                }).ToList()
            };

            ViewBag.ProductID = id.Value;
            ViewBag.ProductCode = product.PNo;
            ViewBag.ProductName = product.Name;

            return View(viewModel);
        }

        // Olish so'rovi: Norm/ViewDetails/{normVersionId}
        public async Task<ActionResult> ViewDetails(int? normVersionId)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!normVersionId.HasValue)
            {
                TempData["Error"] = "Norm versiyasi ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            var normVersion = await db.NORMVERSIONS
                .Include(n => n.PRODUCT)
                .Include(n => n.NORMLINES)
                .Include("NORMLINES.PRODUCT")
                .FirstOrDefaultAsync(n => n.ID == normVersionId.Value && n.IsDeleted != true);

            if (normVersion == null)
            {
                TempData["Error"] = "Norm versiyasi topilmadi.";
                return RedirectToAction("Index");
            }

            var viewModel = new ProductNormViewModel
            {
                ProductID = normVersion.ProductID ?? 0,
                ProductCode = normVersion.PRODUCT?.PNo ?? "",
                ProductName = normVersion.PRODUCT?.Name ?? "",
                Period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "",
                NormVersionID = normVersion.ID,
                EffectiveFrom = normVersion.EffectiveFrom,
                EffectiveTo = normVersion.EffectiveTo,
                Comment = normVersion.Comment
            };

            // Yuklash NORM_INPUT_LINES (detailed cutting variants) instead of aggregated NORMLINES
            var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
            var inputLines = await db.NORM_INPUT_LINES
                .Include("PRODUCT.PRODUCT_CATEGORIES")
                .Include("UNIT")
                .Where(il => il.ProductID == viewModel.ProductID && il.Month == period)
                .OrderBy(il => il.MaterialID)
                .ThenBy(il => il.CutLengthMm)
                .ToListAsync();

            foreach (var inputLine in inputLines)
            {
                // MaterialID — PRODUCTS (xomashyo); EDMX: PRODUCT (MaterialID). PRODUCT1 — ProductID (tayyor mahsulot).
                var material = inputLine.PRODUCT;
                if (material == null)
                {
                    material = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefaultAsync(p => p.ID == inputLine.MaterialID);
                }

                if (material != null)
                {
                    // Use Price dan NORM_INPUT_LINES (saved price per product)
                    decimal unitPrice = inputLine.Price;
                    
                    decimal totalAmount = 0;

                    // Use Consumption va WasteAmount dan NORM_INPUT_LINES agar available, otherwise calculate
                    bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                    var standardLengthMm = material.StandardLengthMm ?? material.Length ?? 0;
                    
                    decimal consumption = 0;
                    decimal waste = 0;
                    
                    // Avval, try ga use saved values dan NORM_INPUT_LINES
                    if (inputLine.Consumption.HasValue && inputLine.Consumption.Value > 0)
                    {
                        // Consumption is stored in mm, convert ga m
                        consumption = (decimal)(inputLine.Consumption.Value / 1000.0);
                    }
                    else if (requiresCutting && inputLine.CutLengthMm > 0)
                    {
                        // uchun cutting materials: convert mm ga m
                        consumption = (decimal)(inputLine.CutLengthMm / 1000.0) * inputLine.PiecesPerProduct;
                    }
                    else
                    {
                        // uchun non-cutting materials: use pieces as-is
                        consumption = inputLine.PiecesPerProduct;
                    }
                    
                    // Use WasteAmount dan NORM_INPUT_LINES agar available
                    if (inputLine.WasteAmount.HasValue)
                    {
                        // WasteAmount is stored in mm, convert ga m
                        waste = (decimal)(inputLine.WasteAmount.Value / 1000.0);
                    }
                    else if (requiresCutting && standardLengthMm > 0 && inputLine.CutLengthMm > 0)
                    {
                        // Calculate waste agar not saved
                        var wastePerBar = Math.Max(0, standardLengthMm - inputLine.CutLengthMm);
                        var barsNeeded = inputLine.PiecesPerProduct; // 1 piece per bar
                        waste = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }

                    if (unitPrice > 0)
                    {
                        var cleanCost = consumption * unitPrice;
                        var wasteCost = waste * unitPrice;
                        totalAmount = cleanCost + wasteCost;
                    }

                    viewModel.CuttingLines.Add(new NormLineViewModel
                    {
                        ID = inputLine.ID,
                        MaterialID = inputLine.MaterialID,
                        MaterialCode = material.PNo ?? "",
                        MaterialName = material.Name ?? "",
                        StandardLengthMm = standardLengthMm > 0 ? (double?)standardLengthMm : null,
                        CalculatedLengthMm = inputLine.CutLengthMm,
                        Quantity = inputLine.PiecesPerProduct,
                        PiecesPerBar = 1, // Always 1 (removed dan UI, PiecesPerProduct is stored as Quantity)
                        ConsumptionPerProduct = consumption,
                        WastePerProduct = waste,
                        IsWasteManual = inputLine.WasteAmount.HasValue,
                        UnitPrice = unitPrice > 0 ? (decimal?)unitPrice : null,
                        TotalCostPerProduct = totalAmount > 0 ? totalAmount : (consumption * unitPrice),
                        Norma = consumption,
                        Chiqindi = waste,
                        IsSelected = true,
                        UnitID = NormalizeUnitFk(inputLine.UnitID) ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : null)
                    });
                }
            }

            // Calculate totals - Umumiy norma faqat yog'och kategoriyasidagi materiallar uchun
            // Olish material categories ga tekshirish agar it's wood (yog'och)
            var materialIds = viewModel.CuttingLines.Select(l => l.MaterialID).Distinct().ToList();
            var materialCategories = await db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => materialIds.Contains(p.ID))
                .Select(p => new { 
                    p.ID, 
                    RequiresCutting = p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting,
                    IsWood = p.PRODUCT_CATEGORIES != null && (
                        (p.PRODUCT_CATEGORIES.RawMaterialGroup != null && 
                         (p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("Yo'goch") || 
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("Yog'och") ||
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("yog'och") ||
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("yo'goch"))) ||
                        (p.PRODUCT_CATEGORIES.Name != null && 
                         (p.PRODUCT_CATEGORIES.Name.Contains("Yo'goch") || 
                          p.PRODUCT_CATEGORIES.Name.Contains("Yog'och") ||
                          p.PRODUCT_CATEGORIES.Name.Contains("yog'och") ||
                          p.PRODUCT_CATEGORIES.Name.Contains("yo'goch")))
                    )
                })
                .ToDictionaryAsync(p => p.ID, p => new { p.RequiresCutting, p.IsWood });
            
            // TotalNorm: faqat yog'och kategoriyasidagi materiallar uchun
            viewModel.TotalNorm = viewModel.CuttingLines
                .Where(l => materialCategories.ContainsKey(l.MaterialID) && materialCategories[l.MaterialID].IsWood)
                .Sum(l => l.ConsumptionPerProduct);
            
            // TotalWaste: faqat yog'och kategoriyasidagi materiallar uchun
            viewModel.TotalWaste = viewModel.CuttingLines
                .Where(l => materialCategories.ContainsKey(l.MaterialID) && materialCategories[l.MaterialID].IsWood)
                .Sum(l => l.WastePerProduct);
            
            // TotalCost: barcha materiallar uchun
            viewModel.TotalCost = viewModel.CuttingLines.Sum(l => l.TotalCostPerProduct);
            
            // Pass materialCategories ga view uchun sum calculations (convert ga simple bool dictionary uchun compatibility)
            ViewBag.MaterialCategories = materialCategories.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.IsWood // Use IsWood instead of RequiresCutting
            );

            // UnitID -> unit short name map for ViewDetails table
            var unitIds = viewModel.CuttingLines
                .Where(l => l.UnitID.HasValue && l.UnitID.Value > 0)
                .Select(l => l.UnitID.Value)
                .Distinct()
                .ToList();
            ViewBag.UnitNameMap = await db.UNITS
                .Where(u => unitIds.Contains(u.ID))
                .ToDictionaryAsync(
                    u => u.ID,
                    u => !string.IsNullOrEmpty(u.ShortName) ? u.ShortName : (u.UnitName ?? u.ID.ToString()));

            ViewBag.IsReadOnly = true;
            return View("ViewDetails", viewModel);
        }

        // POST so'rovi: Norm/o'chirish/{normVersionId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? normVersionId)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!normVersionId.HasValue)
            {
                TempData["Error"] = "Norm versiyasi ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    var normVersion = await db.NORMVERSIONS
                        .Include(n => n.NORMLINES)
                        .FirstOrDefaultAsync(n => n.ID == normVersionId.Value);

                    if (normVersion == null)
                    {
                        TempData["Error"] = "Norm versiyasi topilmadi.";
                        return RedirectToAction("Index");
                    }

                    var productId = normVersion.ProductID;
                    var product = await db.PRODUCTS.FindAsync(productId);
                    var productCode = product?.PNo ?? "";
                    var period = normVersion.EffectiveFrom.HasValue ? normVersion.EffectiveFrom.Value.ToString("yyyy-MM") : "";

                    // O'chirish NORM_INPUT_LINES (detailed cutting variants) uchun this period
                    int deletedInputLinesCount = 0;
                    if (!string.IsNullOrEmpty(period))
                    {
                        var inputLines = await db.NORM_INPUT_LINES
                            .Where(il => il.ProductID == productId && il.Month == period)
                            .ToListAsync();
                        if (inputLines.Any())
                        {
                            deletedInputLinesCount = inputLines.Count;
                            db.NORM_INPUT_LINES.RemoveRange(inputLines);
                            await db.SaveChangesAsync();
                            
                            // Log deletion of NORM_INPUT_LINES
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm", 
                                $"Mahsulot PNo: {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}) dan {deletedInputLinesCount} ta NORM_INPUT_LINE o'chirildi.");
                        }
                    }

                    // O'chirish NORMLINES
                    int deletedNormLinesCount = 0;
                    if (normVersion.NORMLINES.Any())
                    {
                        deletedNormLinesCount = normVersion.NORMLINES.Count;
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES);
                        await db.SaveChangesAsync();
                        
                        // Log deletion of NORMLINES
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}) dan {deletedNormLinesCount} ta NORMLINE o'chirildi.");
                    }

                    // Hard o'chirish NORMVERSION (entry.remove)
                    db.NORMVERSIONS.Remove(normVersion);
                    await db.SaveChangesAsync();

                    // Reseed identity values (tag-tugi bilan o'chirishdan so'ng)
                    var maxInputId = await db.NORM_INPUT_LINES.Select(x => (int?)x.ID).MaxAsync() ?? 0;
                    var maxNormLineId = await db.NORMLINES.Select(x => (int?)x.ID).MaxAsync() ?? 0;
                    var maxNormVersionId = await db.NORMVERSIONS.Select(x => (int?)x.ID).MaxAsync() ?? 0;
                    await db.Database.ExecuteSqlCommandAsync($"DBCC CHECKIDENT ('NORM_INPUT_LINES', RESEED, {maxInputId})");
                    await db.Database.ExecuteSqlCommandAsync($"DBCC CHECKIDENT ('NORMLINES', RESEED, {maxNormLineId})");
                    await db.Database.ExecuteSqlCommandAsync($"DBCC CHECKIDENT ('NORMVERSIONS', RESEED, {maxNormVersionId})");
                    
                    // Note: BOM_HEADERS va BOM_LINES product ga bog'liq, norm versiyasiga emas
                    // Shuning uchun ularni o'chirmaymiz, chunki keyingi norm versiyasi yaratilganda yangilanadi

                    // Log final deletion
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                        $"Mahsulot PNo: {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}, Period: {period}) o'chirildi.");

                    transaction.Complete();

                    TempData["Success"] = "NORM muvaffaqiyatli o'chirildi.";
                    return RedirectToAction("Details", new { id = productId });
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                    return RedirectToAction("Index");
                }
            }
        }

        // Olish so'rovi: Norm/yaratish
        public async Task<ActionResult> Create(int? productId, string period)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var viewModel = new ProductNormViewModel();

            if (productId.HasValue)
            {
                // New norm uchun product
                var product = await db.PRODUCTS.FindAsync(productId.Value);
                if (product == null)
                {
                    TempData["Error"] = "Mahsulot topilmadi.";
                    return RedirectToAction("Index");
                }

                var selectedPeriod = period ?? DateTime.Now.ToString("yyyy-MM");
                
                // Tahlil qilish selectedPeriod ga year va month uchun SQL-compatible comparison
                var periodParts = selectedPeriod.Split('-');
                int selectedYear = 0, selectedMonth = 0;
                NORMVERSION existingNorm = null;
                
                if (periodParts.Length >= 2 && int.TryParse(periodParts[0], out selectedYear) && int.TryParse(periodParts[1], out selectedMonth))
                {
                    // Tekshirish agar norm already mavjud uchun this product va period
                    // Use SQL-compatible date comparison instead of ToString
                    var periodStart = new DateTime(selectedYear, selectedMonth, 1);
                    var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                    
                    existingNorm = await db.NORMVERSIONS
                        .Where(n => n.ProductID == productId.Value && 
                               n.IsDeleted == false &&
                               n.EffectiveFrom.HasValue && 
                               n.EffectiveFrom.Value >= periodStart &&
                               n.EffectiveFrom.Value <= periodEnd)
                        .FirstOrDefaultAsync();
                }
                
                if (existingNorm != null)
                {
                    TempData["Error"] = $"Bu mahsulot uchun {selectedPeriod} oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang.";
                    return RedirectToAction("Details", new { id = productId.Value });
                }

                viewModel.ProductID = product.ID;
                viewModel.ProductCode = product.PNo;
                viewModel.ProductName = product.Name;
                viewModel.Period = selectedPeriod;
            }
            else
            {
                TempData["Error"] = "Mahsulot ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            // Yuklash materials (raw materials, components, va other materials)
            var materials = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            // Ensure materials used in existing lines are included (even if not raw/component)
            var lineMaterialIds = viewModel.CuttingLines
                .Select(l => l.MaterialID)
                .Distinct()
                .ToList();
            var missingMaterialIds = lineMaterialIds
                .Where(id => !materials.Any(m => m.ID == id))
                .ToList();
            if (missingMaterialIds.Any())
            {
                var missingMaterials = await db.PRODUCTS
                    .Where(p => missingMaterialIds.Contains(p.ID) && p.IsDeleted == false)
                    .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                    .ToListAsync();
                materials.AddRange(missingMaterials);
            }

            // Yaratish SelectList bilan "PNo - Name" format
            var materialsSelectList = materials
                .OrderBy(m => m.PNo)
                .Select(m => new SelectListItem
                {
                    Value = m.ID.ToString(),
                    Text = $"{m.PNo} - {m.Name ?? m.PNo}"
                }).ToList();
            
            ViewBag.Materials = new SelectList(materialsSelectList, "Value", "Text", null);
            
            // Yaratish simple list uchun JSON serialization (avoid circular reference) - include Name uchun display
            ViewBag.MaterialsJson = materials
                .OrderBy(m => m.PNo)
                .Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo })
                .ToList();

            await SetUnitsViewBagAsync();
            return View("Create", viewModel);
        }

        // Olish so'rovi: Norm/Edit
        public async Task<ActionResult> Edit(int? normVersionId)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!normVersionId.HasValue)
            {
                TempData["Error"] = "Norm versiyasi ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            // Yuklash existing norm version
            var normVersion = await db.NORMVERSIONS
                .Include(n => n.PRODUCT)
                .FirstOrDefaultAsync(n => n.ID == normVersionId.Value && n.IsDeleted != true);

            if (normVersion == null)
            {
                TempData["Error"] = "Norm versiyasi topilmadi.";
                return RedirectToAction("Index");
            }

            var viewModel = new ProductNormViewModel
            {
                ProductID = normVersion.ProductID ?? 0,
                ProductCode = normVersion.PRODUCT?.PNo ?? "",
                ProductName = normVersion.PRODUCT?.Name ?? "",
                Period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "",
                NormVersionID = normVersion.ID,
                EffectiveFrom = normVersion.EffectiveFrom,
                EffectiveTo = normVersion.EffectiveTo,
                Comment = normVersion.Comment
            };

            // Yuklash NORM_INPUT_LINES (detailed cutting variants) instead of aggregated NORMLINES
            var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
            var inputLines = await db.NORM_INPUT_LINES
                .Include("PRODUCT.PRODUCT_CATEGORIES")
                .Include("UNIT")
                .Where(il => il.ProductID == viewModel.ProductID && il.Month == period)
                .OrderBy(il => il.MaterialID)
                .ThenBy(il => il.CutLengthMm)
                .ToListAsync();

            foreach (var inputLine in inputLines)
            {
                // MaterialID — PRODUCTS (xomashyo); EDMX: PRODUCT (MaterialID). PRODUCT1 — ProductID (tayyor mahsulot).
                var material = inputLine.PRODUCT;
                if (material == null)
                {
                    material = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefaultAsync(p => p.ID == inputLine.MaterialID);
                }

                if (material != null)
                {
                    // Use Price dan NORM_INPUT_LINES (saved price per product)
                    decimal unitPrice = inputLine.Price;
                    
                    decimal totalAmount = 0;

                    // Use Consumption va WasteAmount dan NORM_INPUT_LINES agar available, otherwise calculate
                    bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                    var standardLengthMm = material.StandardLengthMm ?? material.Length ?? 0;
                    
                    decimal consumption = 0;
                    decimal waste = 0;
                    
                    // Avval, try ga use saved values dan NORM_INPUT_LINES
                    if (inputLine.Consumption.HasValue && inputLine.Consumption.Value > 0)
                    {
                        // Consumption is stored in mm, convert ga m
                        consumption = (decimal)(inputLine.Consumption.Value / 1000.0);
                    }
                    else if (requiresCutting && inputLine.CutLengthMm > 0)
                    {
                        // uchun cutting materials: convert mm ga m
                        consumption = (decimal)(inputLine.CutLengthMm / 1000.0) * inputLine.PiecesPerProduct;
                    }
                    else
                    {
                        // uchun non-cutting materials: use pieces as-is
                        consumption = inputLine.PiecesPerProduct;
                    }
                    
                    // Use WasteAmount dan NORM_INPUT_LINES agar available
                    if (inputLine.WasteAmount.HasValue)
                    {
                        // WasteAmount is stored in mm, convert ga m
                        waste = (decimal)(inputLine.WasteAmount.Value / 1000.0);
                    }
                    else if (requiresCutting && standardLengthMm > 0 && inputLine.CutLengthMm > 0)
                    {
                        // Calculate waste agar not saved
                        var wastePerBar = Math.Max(0, standardLengthMm - inputLine.CutLengthMm);
                        var barsNeeded = inputLine.PiecesPerProduct; // 1 piece per bar
                        waste = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }

                    if (unitPrice > 0)
                    {
                        var cleanCost = consumption * unitPrice;
                        var wasteCost = waste * unitPrice;
                        totalAmount = cleanCost + wasteCost;
                    }

                    viewModel.CuttingLines.Add(new NormLineViewModel
                    {
                        ID = inputLine.ID,
                        MaterialID = inputLine.MaterialID,
                        MaterialCode = material.PNo ?? "",
                        MaterialName = material.Name ?? "",
                        StandardLengthMm = standardLengthMm > 0 ? (double?)standardLengthMm : null,
                        CalculatedLengthMm = inputLine.CutLengthMm,
                        Quantity = inputLine.PiecesPerProduct,
                        PiecesPerBar = 1, // Always 1 (removed dan UI, PiecesPerProduct is stored as Quantity)
                        ConsumptionPerProduct = consumption,
                        WastePerProduct = waste,
                        IsWasteManual = inputLine.WasteAmount.HasValue,
                        UnitPrice = unitPrice > 0 ? (decimal?)unitPrice : null,
                        TotalCostPerProduct = totalAmount > 0 ? totalAmount : (consumption * unitPrice),
                        Norma = consumption,
                        Chiqindi = waste,
                        IsSelected = true,
                        UnitID = NormalizeUnitFk(inputLine.UnitID) ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : null)
                    });
                }
            }

            // Calculate totals - Umumiy norma faqat yog'och kategoriyasidagi materiallar uchun
            // Olish material categories ga tekshirish agar it's wood (yog'och)
            var materialIdsEdit = viewModel.CuttingLines.Select(l => l.MaterialID).Distinct().ToList();
            var materialCategoriesEdit = await db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => materialIdsEdit.Contains(p.ID))
                .Select(p => new { 
                    p.ID, 
                    RequiresCutting = p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting,
                    IsWood = p.PRODUCT_CATEGORIES != null && (
                        (p.PRODUCT_CATEGORIES.RawMaterialGroup != null && 
                         (p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("Yo'goch") || 
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("Yog'och") ||
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("yog'och") ||
                          p.PRODUCT_CATEGORIES.RawMaterialGroup.Contains("yo'goch"))) ||
                        (p.PRODUCT_CATEGORIES.Name != null && 
                         (p.PRODUCT_CATEGORIES.Name.Contains("Yo'goch") || 
                          p.PRODUCT_CATEGORIES.Name.Contains("Yog'och") ||
                          p.PRODUCT_CATEGORIES.Name.Contains("yog'och") ||
                          p.PRODUCT_CATEGORIES.Name.Contains("yo'goch")))
                    )
                })
                .ToDictionaryAsync(p => p.ID, p => new { p.RequiresCutting, p.IsWood });
            
            // TotalNorm: faqat yog'och kategoriyasidagi materiallar uchun
            viewModel.TotalNorm = viewModel.CuttingLines
                .Where(l => materialCategoriesEdit.ContainsKey(l.MaterialID) && materialCategoriesEdit[l.MaterialID].IsWood)
                .Sum(l => l.ConsumptionPerProduct);
            
            // TotalWaste: faqat yog'och kategoriyasidagi materiallar uchun
            viewModel.TotalWaste = viewModel.CuttingLines
                .Where(l => materialCategoriesEdit.ContainsKey(l.MaterialID) && materialCategoriesEdit[l.MaterialID].IsWood)
                .Sum(l => l.WastePerProduct);
            
            // TotalCost: barcha materiallar uchun
            viewModel.TotalCost = viewModel.CuttingLines.Sum(l => l.TotalCostPerProduct);

            // Yuklash materials (raw materials, components, va other materials - selofan, karton, dona ketadigan maxsulotlar)
            var materials = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            // Edit holatda mavjud satrdagi materiallar filterga tushmasa ham dropdownda ko'rinishi kerak
            var lineMaterialIds = viewModel.CuttingLines
                .Select(l => l.MaterialID)
                .Distinct()
                .ToList();
            var missingMaterialIds = lineMaterialIds
                .Where(id => !materials.Any(m => m.ID == id))
                .ToList();
            if (missingMaterialIds.Any())
            {
                var missingMaterials = await db.PRODUCTS
                    .Where(p => missingMaterialIds.Contains(p.ID) && p.IsDeleted == false)
                    .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                    .ToListAsync();
                materials.AddRange(missingMaterials);
            }

            // Yaratish SelectList bilan "PNo - Name" format
            var materialsSelectList = materials
                .OrderBy(m => m.PNo)
                .Select(m => new SelectListItem
                {
                    Value = m.ID.ToString(),
                    Text = $"{m.PNo} - {m.Name ?? m.PNo}"
                }).ToList();
            
            ViewBag.Materials = new SelectList(materialsSelectList, "Value", "Text", null);
            
            // Yaratish simple list uchun JSON serialization (avoid circular reference) - include Name uchun display
            ViewBag.MaterialsJson = materials
                .OrderBy(m => m.PNo)
                .Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo })
                .ToList();

            await SetUnitsViewBagAsync();
            return View("Edit", viewModel);
        }

        // POST so'rovi: Norm/Edit (saqlash temporary lines)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ProductNormViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Edit ham Create kabi bevosita saqlaydi (UI dan kelgan qiymatlar as-is).
                return await Confirm(viewModel);
            }

            var materialsError = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            var lineMaterialIdsError = (viewModel.CuttingLines ?? new List<NormLineViewModel>())
                .Select(l => l.MaterialID)
                .Distinct()
                .ToList();
            var missingMaterialIdsError = lineMaterialIdsError
                .Where(id => !materialsError.Any(m => m.ID == id))
                .ToList();
            if (missingMaterialIdsError.Any())
            {
                var missingMaterialsError = await db.PRODUCTS
                    .Where(p => missingMaterialIdsError.Contains(p.ID) && p.IsDeleted == false)
                    .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                    .ToListAsync();
                materialsError.AddRange(missingMaterialsError);
            }

            var materialsErrorSelectList = materialsError
                .OrderBy(m => m.PNo)
                .Select(m => new SelectListItem
                {
                    Value = m.ID.ToString(),
                    Text = $"{m.PNo} - {m.Name ?? m.PNo}"
                }).ToList();

            ViewBag.Materials = new SelectList(materialsErrorSelectList, "Value", "Text", null);
            ViewBag.MaterialsJson = materialsError
                .OrderBy(m => m.PNo)
                .Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo })
                .ToList();

            await SetUnitsViewBagAsync();
            return View(viewModel);
        }

        // Olish so'rovi: Norm/GetMaterialData/{materialId}
        [HttpGet]
        public async Task<JsonResult> GetMaterialData(int materialId)
        {
            try
            {
                var material = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .FirstOrDefaultAsync(p => p.ID == materialId);
                    
                if (material == null)
                {
                    return Json(new { success = false, message = "Material topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Olish latest price dan INCOME_PRODUCTS
                var latestIncome = await db.INCOME_PRODUCTS
                    .Include(ip => ip.INCOME)
                    .Where(ip => ip.ProductID == materialId && ip.PiecePrice.HasValue)
                    .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                    .Select(ip => new { Price = ip.PiecePrice.Value })
                    .FirstOrDefaultAsync();

                // Tekshirish agar material requires cutting (dan category)
                bool requiresCutting = false;
                if (material.PRODUCT_CATEGORIES != null)
                {
                    requiresCutting = material.PRODUCT_CATEGORIES.RequiresCutting;
                }

                // Yog'och turini aniqlash (faqat shu tur uchun kirim narxini metrga aylantiramiz)
                bool isWoodMaterial = false;
                if (material.PRODUCT_CATEGORIES != null)
                {
                    var rawGroup = material.PRODUCT_CATEGORIES.RawMaterialGroup ?? "";
                    var catName = material.PRODUCT_CATEGORIES.Name ?? "";
                    isWoodMaterial =
                        rawGroup.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rawGroup.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        catName.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        catName.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                // Use StandardLengthMm agar available, otherwise use Length
                double standardLengthMm = 0;
                if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                {
                    standardLengthMm = material.StandardLengthMm.Value;
                }
                else if (material.Length.HasValue && material.Length.Value > 0)
                {
                    standardLengthMm = material.Length.Value;
                }

                double? unitPrice = latestIncome != null ? latestIncome.Price : (double?)null;
                if (isWoodMaterial && unitPrice.HasValue && standardLengthMm > 0)
                {
                    // Example: 24000 UZS (4000 mm bar) => 24000 / (4000/1000) = 6000 UZS/m
                    var meters = standardLengthMm / 1000.0;
                    if (meters > 0)
                    {
                        unitPrice = unitPrice.Value / meters;
                    }
                }

                var materialData = new
                {
                    success = true,
                    materialId = material.ID,
                    materialCode = material.PNo,
                    materialName = material.Name,
                    standardLengthMm = standardLengthMm,
                    length = material.Length ?? 0,
                    width = material.Width ?? 0,
                    height = material.Height ?? 0,
                    unitPrice = unitPrice,
                    requiresCutting = requiresCutting,
                    isWoodMaterial = isWoodMaterial,
                    categoryId = material.CategoryID,
                    defaultUnitId = NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : null)
                };

                return Json(materialData, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: Norm/Calculate (Optimization)
        [HttpPost]
        public async Task<JsonResult> Calculate(ProductNormViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                // Always read dan Request body (JSON) as client sends JSON
                string json = "";
                Request.InputStream.Position = 0;
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    json = reader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(json))
                {
                    return Json(new { success = false, message = "JSON ma'lumotlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                try
                {
                    // Use Newtonsoft.Json uchun deserialization
                    viewModel = JsonConvert.DeserializeObject<ProductNormViewModel>(json);
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"JSON deserialization xatosi: {ex.Message}. JSON: {json.Substring(0, Math.Min(200, json.Length))}" }, JsonRequestBehavior.AllowGet);
                }
                
                // Tekshirish agar viewModel is null
                if (viewModel == null)
                {
                    return Json(new { success = false, message = "Model ma'lumotlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Tekshirish agar CuttingLines is null yoki empty
                if (viewModel.CuttingLines == null || viewModel.CuttingLines.Count == 0)
                {
                    return Json(new { success = false, message = "Kesish variantlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Recalculate barcha lines
                RecalculateLineFields(viewModel.CuttingLines);

                // Group by MaterialID
                var materialGroups = viewModel.CuttingLines
                    .Where(l => l.IsSelected && l.MaterialID > 0)
                    .GroupBy(l => l.MaterialID)
                    .ToList();

                if (materialGroups.Count == 0)
                {
                    return Json(new { success = false, message = "Tanlangan kesish variantlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                var optimization = new OptimizationSuggestionViewModel
                {
                    SuggestedMaterials = new Dictionary<int, MaterialSummaryViewModel>()
                };

                decimal totalNorm = 0;
                decimal totalWaste = 0;
                decimal totalCost = 0;

                // Saqlash barcha optimized lines uchun detailed display
                var allOptimizedLines = new List<object>();

                foreach (var group in materialGroups)
                {
                    var materialId = group.Key;
                    var lines = group.ToList();
                    var material = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefaultAsync(p => p.ID == materialId);

                    // Simple optimization: select lines bilan minimal waste
                    var optimizationResult = OptimizeMaterialCutting(lines, material);
                    var optimizedLines = optimizationResult.Item1;
                    var optimizationNotes = optimizationResult.Item2;

                    if (optimizedLines != null && optimizedLines.Count > 0)
                    {
                        var summary = new MaterialSummaryViewModel
                        {
                            MaterialID = materialId,
                            MaterialName = material?.Name ?? "",
                            TotalConsumption = optimizedLines.Sum(l => l.ConsumptionPerProduct),
                            TotalWaste = optimizedLines.Sum(l => l.WastePerProduct),
                            TotalCost = optimizedLines.Sum(l => l.TotalCostPerProduct),
                            SelectedLineIDs = optimizedLines.Select(l => l.ID).ToList()
                        };

                        optimization.SuggestedMaterials[materialId] = summary;
                        totalNorm += summary.TotalConsumption;
                        totalWaste += summary.TotalWaste;
                        totalCost += summary.TotalCost;

                        // Qo'shish detailed lines uchun DataTable bilan notes
                        foreach (var line in optimizedLines)
                        {
                            var note = optimizationNotes.ContainsKey(line.ID) ? optimizationNotes[line.ID] : "";
                            allOptimizedLines.Add(new
                            {
                                id = line.ID,
                                materialId = materialId,
                                materialCode = line.MaterialCode ?? "",
                                materialName = line.MaterialName ?? material?.Name ?? "",
                                calculatedLengthMm = line.CalculatedLengthMm ?? 0,
                                quantity = line.Quantity,
                                consumptionPerProduct = line.ConsumptionPerProduct,
                                wastePerProduct = line.WastePerProduct,
                                unitPrice = line.UnitPrice ?? 0,
                                totalCostPerProduct = line.TotalCostPerProduct,
                                standardLengthMm = line.StandardLengthMm ?? 0,
                                usesWaste = line.WastePerProduct == 0 && line.Quantity > 0, // agar waste is 0, it might use waste dan previous
                                note = note
                            });
                        }
                    }
                }

                optimization.SuggestedTotalNorm = totalNorm;
                optimization.SuggestedTotalWaste = totalWaste;
                optimization.SuggestedTotalCost = totalCost;
                optimization.Message = $"Optimizatsiya natijasi: Umumiy norma {totalNorm:F4} m, Umumiy chiqindi {totalWaste:F4} m, Umumiy narx {totalCost:F2}";

                // Aylantirish Dictionary ga list uchun JSON serialization
                var suggestedMaterialsList = optimization.SuggestedMaterials.Select(kvp => new
                {
                    materialId = kvp.Key,
                    materialName = kvp.Value.MaterialName,
                    totalConsumption = kvp.Value.TotalConsumption,
                    totalWaste = kvp.Value.TotalWaste,
                    totalCost = kvp.Value.TotalCost,
                    selectedLineIDs = kvp.Value.SelectedLineIDs
                }).ToList();

                return Json(new
                {
                    success = true,
                    optimization = new
                    {
                        message = optimization.Message,
                        suggestedTotalNorm = optimization.SuggestedTotalNorm,
                        suggestedTotalWaste = optimization.SuggestedTotalWaste,
                        suggestedTotalCost = optimization.SuggestedTotalCost,
                        suggestedMaterials = suggestedMaterialsList,
                        optimizedLines = allOptimizedLines // Detailed lines uchun DataTable
                    },
                    totalNorm = totalNorm,
                    totalWaste = totalWaste,
                    totalCost = totalCost
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // To'liq xatolik tafsilotini diagnostika uchun yozamiz
                System.Diagnostics.Debug.WriteLine($"Hisoblash xatoligi: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack izi: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Ichki xatolik: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"Xatolik: {ex.Message}" }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: Norm/Confirm (saqlash ga BOM/NORM tables)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Confirm(ProductNormViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Tekshirish agar norm already mavjud uchun this product va period (faqat uchun new norms, not updates)
            if (!viewModel.NormVersionID.HasValue)
            {
                // Tahlil qilish viewModel.Period ga year va month uchun SQL-compatible comparison
                var periodParts = viewModel.Period?.Split('-');
                int selectedYear = 0, selectedMonth = 0;
                NORMVERSION existingNorm = null;
                
                if (periodParts != null && periodParts.Length >= 2 && 
                    int.TryParse(periodParts[0], out selectedYear) && 
                    int.TryParse(periodParts[1], out selectedMonth))
                {
                    var periodStart = new DateTime(selectedYear, selectedMonth, 1);
                    var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                    
                    existingNorm = await db.NORMVERSIONS
                        .Where(n => n.ProductID == viewModel.ProductID && 
                               n.IsDeleted == false &&
                               n.ID != viewModel.NormVersionID &&
                               n.EffectiveFrom.HasValue && 
                               n.EffectiveFrom.Value >= periodStart &&
                               n.EffectiveFrom.Value <= periodEnd)
                        .FirstOrDefaultAsync();
                }
                
                if (existingNorm != null)
                {
                    TempData["Error"] = $"Bu mahsulot uchun {viewModel.Period} oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang.";
                    return RedirectToAction("Details", new { id = viewModel.ProductID });
                }
            }

            // Tekshirish agar optimization was performed (dan hidden input)
            bool isOptimized = false;
            var isOptimizedValue = Request.Form["IsOptimized"];
            if (!string.IsNullOrEmpty(isOptimizedValue))
            {
                bool.TryParse(isOptimizedValue, out isOptimized);
            }
            
            System.Diagnostics.Debug.WriteLine($"Confirm action - IsOptimized: {isOptimized}");

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // IMPORTANT: Collect barcha CuttingLines dan Request.Form ga ensure nothing is missed
                    // Model binding might miss some qatorlar, so we rebuild the list dan form ma'lumot
                    var allFormLines = new List<NormLineViewModel>();
                    var formKeys = Request.Form.AllKeys.Where(k => k != null && k.StartsWith("CuttingLines[") && k.Contains("].MaterialID")).ToList();
                    
                    // Extract barcha line indices dan form keys
                    var lineIndices = formKeys
                        .Select(k => {
                            var match = System.Text.RegularExpressions.Regex.Match(k, @"CuttingLines\[(\d+)\]");
                            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
                        })
                        .Where(i => i >= 0)
                        .Distinct()
                        .OrderBy(i => i)
                        .ToList();
                    
                    // Build complete list dan form ma'lumot
                    foreach (var index in lineIndices)
                    {
                        var materialIdStr = Request.Form[$"CuttingLines[{index}].MaterialID"];
                        if (!string.IsNullOrEmpty(materialIdStr) && int.TryParse(materialIdStr, out int materialId) && materialId > 0)
                        {
                            int? unitIdForm = null;
                            // Bir nechta field yoki Select2 duplicate — birinchi to'g'ri qiymatni olamiz
                            var unitVals = Request.Form.GetValues($"CuttingLines[{index}].UnitID");
                            var unitFormStr = unitVals != null
                                ? unitVals.Select(v => v != null ? v.Trim() : "").FirstOrDefault(v => !string.IsNullOrEmpty(v))
                                : null;
                            if (!string.IsNullOrEmpty(unitFormStr) && int.TryParse(unitFormStr, out int uid) && uid > 0)
                                unitIdForm = uid;

                            var line = new NormLineViewModel
                            {
                                ID = int.TryParse(Request.Form[$"CuttingLines[{index}].ID"] ?? "0", out int id) ? id : 0,
                                MaterialID = materialId,
                                CalculatedLengthMm = double.TryParse(Request.Form[$"CuttingLines[{index}].CalculatedLengthMm"] ?? "0", out double len) ? (double?)len : null,
                                Quantity = int.TryParse(Request.Form[$"CuttingLines[{index}].Quantity"] ?? "1", out int qty) ? qty : 1,
                                WastePerProduct = decimal.TryParse(Request.Form[$"CuttingLines[{index}].WastePerProduct"] ?? "0", out decimal waste) ? waste : 0,
                                UnitPrice = decimal.TryParse(Request.Form[$"CuttingLines[{index}].UnitPrice"] ?? "0", out decimal price) ? (decimal?)price : null,
                                IsSelected = Request.Form[$"CuttingLines[{index}].IsSelected"] == "true",
                                IsWasteManual = (Request.Form[$"CuttingLines[{index}].IsWasteManual"] ?? "").ToLower() == "true",
                                UnitID = unitIdForm
                            };
                            
                            // Urinish ga get ConsumptionPerProduct va TotalCostPerProduct dan hidden fields
                            if (decimal.TryParse(Request.Form[$"CuttingLines[{index}].ConsumptionPerProduct"] ?? "0", out decimal consumption))
                            {
                                line.ConsumptionPerProduct = consumption;
                            }
                            if (decimal.TryParse(Request.Form[$"CuttingLines[{index}].TotalCostPerProduct"] ?? "0", out decimal totalCost))
                            {
                                line.TotalCostPerProduct = totalCost;
                            }
                            
                            allFormLines.Add(line);
                        }
                    }
                    
                    // Use form ma'lumot agar it has more lines than viewModel, otherwise use viewModel
                    if (allFormLines.Count > 0)
                    {
                        viewModel.CuttingLines = allFormLines;
                    }
                    
                    // STRICT mode: UI dan kelgan qiymatlar as-is saqlanadi, avtomatik recalculate qilinmaydi.

                    // Olish product PNo uchun logging
                    var product = await db.PRODUCTS.FindAsync(viewModel.ProductID);
                    var productPNo = product?.PNo ?? viewModel.ProductCode ?? "Noma'lum";
                    
                    // Olish yoki yaratish NORMVERSION
                    NORMVERSION normVersion;
                    bool isUpdate = false;
                    if (viewModel.NormVersionID.HasValue)
                    {
                        normVersion = await db.NORMVERSIONS
                            .Include(n => n.NORMLINES)
                            .FirstOrDefaultAsync(n => n.ID == viewModel.NormVersionID.Value && n.IsDeleted != true);
                        
                        if (normVersion == null)
                        {
                            TempData["Error"] = "Norm versiyasi topilmadi.";
                            return RedirectToAction("Index");
                        }

                        // Yangilash existing
                        isUpdate = true;
                        normVersion.EffectiveFrom = ParsePeriod(viewModel.Period);
                        normVersion.EffectiveTo = viewModel.EffectiveTo;
                        normVersion.Comment = viewModel.Comment;

                        // O'chirish existing lines
                        var deletedLinesCount = normVersion.NORMLINES.Count;
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES);
                        await db.SaveChangesAsync();
                        
                        // Log deletion of old NORMLINES
                        if (deletedLinesCount > 0)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm", 
                                $"Mahsulot PNo: {productPNo} uchun NORM versiyasi (ID: {viewModel.NormVersionID.Value}) yangilandi. {deletedLinesCount} ta eski NORMLINE o'chirildi.");
                        }
                    }
                    else
                    {
                        // Yaratish new
                        normVersion = new NORMVERSION
                        {
                            ProductID = viewModel.ProductID,
                            EffectiveFrom = ParsePeriod(viewModel.Period),
                            EffectiveTo = viewModel.EffectiveTo,
                            Comment = viewModel.Comment,
                            IsDeleted = false
                        };
                        db.NORMVERSIONS.Add(normVersion);
                        await db.SaveChangesAsync();
                        
                        // Log creation of new NORMVERSION
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productPNo} uchun yangi NORM versiyasi yaratildi. Period: {viewModel.Period}");
                    }

                    // O'chirish existing NORM_INPUT_LINES uchun this period (agar updating)
                    if (isUpdate)
                    {
                        var period2 = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? viewModel.Period;
                        var existingInputLines = await db.NORM_INPUT_LINES
                            .Where(il => il.ProductID == viewModel.ProductID && il.Month == period2)
                            .ToListAsync();
                        if (existingInputLines.Any())
                        {
                            db.NORM_INPUT_LINES.RemoveRange(existingInputLines);
                            await db.SaveChangesAsync();
                            
                            // Log deletion of old NORM_INPUT_LINES
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm", 
                                $"Mahsulot PNo: {productPNo} uchun NORM versiyasi (ID: {normVersion.ID}) dan {existingInputLines.Count} ta eski NORM_INPUT_LINE o'chirildi.");
                        }
                    }

                    // Saqlash detailed cutting variants ga NORM_INPUT_LINES
                    // IMPORTANT: saqlash barcha lines dan viewModel, not just IsSelected ones
                    // This ensures barcha datatable qatorlar are saved, even agar checkbox was not checked
                    int inputLinesCount = 0;
                    foreach (var line in viewModel.CuttingLines.Where(l => l.MaterialID > 0))
                    {
                        // Olish material ga tekshirish agar it requires cutting va get standard length
                        var material = await db.PRODUCTS
                            .Include(p => p.PRODUCT_CATEGORIES)
                            .FirstOrDefaultAsync(p => p.ID == line.MaterialID);
                        
                        bool requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                        double standardLengthMm = 0;
                        if (material != null)
                        {
                            if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                            {
                                standardLengthMm = material.StandardLengthMm.Value;
                            }
                            else if (material.Length.HasValue && material.Length.Value > 0)
                            {
                                standardLengthMm = material.Length.Value;
                            }
                        }
                        
                        // Sarf va chiqindi: UI (Create/Edit) dagi ConsumptionPerProduct / WastePerProduct — asosiy manba (1:1).
                        // Consumption DB: kesish uchun jami mm; VM da sarflanish (m) = ConsumptionPerProduct.
                        double consumption;
                        if (requiresCutting && line.CalculatedLengthMm.HasValue && line.CalculatedLengthMm.Value > 0)
                        {
                            if (line.ConsumptionPerProduct != 0)
                                consumption = (double)line.ConsumptionPerProduct * 1000.0;
                            else
                                consumption = (double)line.CalculatedLengthMm.Value * line.Quantity;
                        }
                        else
                        {
                            consumption = line.ConsumptionPerProduct != 0
                                ? (double)line.ConsumptionPerProduct
                                : (double)line.Quantity;
                        }

                        double wasteAmount;
                        if (line.IsWasteManual || line.WastePerProduct != 0)
                        {
                            wasteAmount = (double)line.WastePerProduct * 1000.0;
                        }
                        else if (requiresCutting
                            && standardLengthMm > 0
                            && line.CalculatedLengthMm.HasValue
                            && line.CalculatedLengthMm.Value > 0)
                        {
                            var wastePerPiece = Math.Max(0, standardLengthMm - (double)line.CalculatedLengthMm.Value);
                            wasteAmount = wastePerPiece * line.Quantity;
                        }
                        else
                        {
                            wasteAmount = 0;
                        }
                        
                        // Ensure Price is set correctly (price per product)
                        decimal price = line.UnitPrice ?? 0;
                        
                        var inputLine = new NORM_INPUT_LINES
                        {
                            ProductID = viewModel.ProductID,
                            MaterialID = line.MaterialID,
                            CutLengthMm = (int)(line.CalculatedLengthMm ?? 0),
                            PiecesPerProduct = line.Quantity,
                            Month = viewModel.Period,
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now,
                            Price = price,
                            Consumption = consumption > 0 ? (double?)consumption : null,
                            WasteAmount = line.IsWasteManual ? (double?)wasteAmount : (wasteAmount > 0 ? (double?)wasteAmount : null),
                            UnitID = NormalizeUnitFk(line.UnitID) ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : null)
                        };
                        
                        // Note: TotalAmount is a computed ustun in the database:
                        // TotalAmount = (((CutLengthMm / 1000.0) * PiecesPerProduct) * Price)
                        // It will be calculated automatically by SQL Server

                        db.NORM_INPUT_LINES.Add(inputLine);
                        inputLinesCount++;
                    }

                    await db.SaveChangesAsync();
                    
                    // Log creation of NORM_INPUT_LINES
                    if (inputLinesCount > 0)
                    {
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productPNo} uchun NORM versiyasi (ID: {normVersion.ID}) ga {inputLinesCount} ta NORM_INPUT_LINE qo'shildi.");
                    }

                    // Now yangilash NORM_LINES based on NORM_INPUT_LINES ma'lumot
                    // Group by MaterialID va aggregate - use barcha lines, not just IsSelected
                    var materialGroups = viewModel.CuttingLines
                        .Where(l => l.MaterialID > 0)
                        .GroupBy(l => l.MaterialID)
                        .ToList();

                    // Yangilash aggregated NORMLINE uchun har bir material based on NORM_INPUT_LINES
                    var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? viewModel.Period;
                    foreach (var group in materialGroups)
                    {
                        var materialId = group.Key;
                        var materialRowNl = await db.PRODUCTS.AsNoTracking().FirstOrDefaultAsync(p => p.ID == materialId);
                        
                        // Calculate QtyPerUnit va WastePercent dan NORM_INPUT_LINES
                        var inputLinesForMaterial = await db.NORM_INPUT_LINES
                            .Where(il => il.ProductID == viewModel.ProductID 
                                && il.MaterialID == materialId 
                                && il.Month == period)
                            .ToListAsync();
                        
                        // QtyPerUnit = Sum of Consumption dan NORM_INPUT_LINES
                        var qtyPerUnit = inputLinesForMaterial
                            .Where(il => il.Consumption.HasValue)
                            .Sum(il => (decimal)il.Consumption.Value);
                        
                        // WastePercent = Sum of WasteAmount / QtyPerUnit (as percentage)
                        var totalWasteAmount = inputLinesForMaterial
                            .Where(il => il.WasteAmount.HasValue)
                            .Sum(il => (decimal)il.WasteAmount.Value);
                        
                        decimal wastePercent = 0;
                        if (qtyPerUnit > 0 && totalWasteAmount > 0)
                        {
                            wastePercent = (totalWasteAmount / qtyPerUnit) * 100;
                            // DB: NORMLINES.WastePercent decimal(5,2) => max 999.99
                            if (wastePercent > 999.99m)
                                wastePercent = 999.99m;
                        }

                        int? normLineUnitId = NormalizeUnitFk(
                            inputLinesForMaterial.Select(il => il.UnitID).FirstOrDefault(u => u.HasValue && u.Value > 0))
                            ?? NormalizeUnitFk(materialRowNl != null && materialRowNl.UnitID > 0 ? materialRowNl.UnitID : null);

                        // Tekshirish agar NORMLINE already mavjud uchun this material
                        var existingNormLine = await db.NORMLINES
                            .FirstOrDefaultAsync(nl => nl.NormID == normVersion.ID && nl.MaterialID == materialId);
                        
                        if (existingNormLine != null)
                        {
                            // Yangilash existing
                            existingNormLine.QtyPerUnit = qtyPerUnit;
                            existingNormLine.WastePercent = wastePercent;
                            existingNormLine.UnitID = normLineUnitId;
                        }
                        else
                        {
                            // Yaratish new
                            var aggregatedLine = new NORMLINE
                            {
                                NormID = normVersion.ID,
                                MaterialID = materialId,
                                QtyPerUnit = qtyPerUnit,
                                WastePercent = wastePercent,
                                UnitID = normLineUnitId
                            };
                            db.NORMLINES.Add(aggregatedLine);
                        }
                    }

                    await db.SaveChangesAsync();
                    
                    // Log creation/yangilash of NORMLINES
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                        $"Mahsulot PNo: {productPNo} uchun NORM versiyasi (ID: {normVersion.ID}) ga {materialGroups.Count} ta NORMLINE yangilandi/yaratildi.");

                    // Yaratish/yangilash BOM_HEADERS va BOM_LINES
                    var bomHeader = await db.BOM_HEADERS
                        .FirstOrDefaultAsync(b => b.ProductID == viewModel.ProductID);

                    if (bomHeader == null)
                    {
                        bomHeader = new BOM_HEADERS
                        {
                            ProductID = viewModel.ProductID,
                            VersionNo = viewModel.Period,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        db.BOM_HEADERS.Add(bomHeader);
                        await db.SaveChangesAsync();

                        // Log creation of BOM_HEADERS
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productPNo} uchun yangi BOM_HEADERS yaratildi (ID: {bomHeader.ID}).");
                    }
                    else
                    {
                        // Yangilash version agar needed
                        bomHeader.VersionNo = viewModel.Period;
                        await db.SaveChangesAsync();
                    }

                    // O'chirish existing BOM_LINES
                    var existingBomLines = await db.BOM_LINES
                        .Where(b => b.BOMID == bomHeader.ID)
                        .ToListAsync();
                    var deletedBomLinesCount = existingBomLines.Count;
                    db.BOM_LINES.RemoveRange(existingBomLines);
                    await db.SaveChangesAsync();
                    
                    // Log deletion of old BOM_LINES
                    if (deletedBomLinesCount > 0)
                    {
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productPNo} uchun BOM_HEADERS (ID: {bomHeader.ID}) dan {deletedBomLinesCount} ta eski BOM_LINE o'chirildi.");
                    }

                    // Yaratish new BOM_LINES dan aggregated norms
                    foreach (var group in materialGroups)
                    {
                        var materialId = group.Key;
                        var lines = group.ToList();
                        
                        // Yuklash material bilan category ga tekshirish RequiresCutting
                        var material = await db.PRODUCTS
                            .Include(p => p.PRODUCT_CATEGORIES)
                            .FirstOrDefaultAsync(p => p.ID == materialId);

                        if (material == null) continue;

                        // UI dan tanlangan birlik (birinchi qator), bo'lmasa mahsulot kartasi; UNITS FK — 0 yoki noto'g'ri ID ishlatilmaydi
                        var firstLineUnit = lines.Select(l => l.UnitID).FirstOrDefault(u => u.HasValue && u.Value > 0);
                        int? bomUnitId = NormalizeUnitFk(firstLineUnit)
                            ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : null);

                        // BOM: 1 ta tayyor mahsulot uchun material umumiy talab = toza sarf + chiqindi (waste)
                        var bomLine = new BOM_LINES
                        {
                            BOMID = bomHeader.ID,
                            ComponentID = materialId,
                            Qty = lines.Sum(l => l.ConsumptionPerProduct + l.WastePerProduct),
                            UnitID = bomUnitId
                        };

                        db.BOM_LINES.Add(bomLine);
                    }

                    await db.SaveChangesAsync();
                    
                    // Log creation of BOM_LINES
                    if (materialGroups.Count > 0)
                    {
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                            $"Mahsulot PNo: {productPNo} uchun BOM_HEADERS (ID: {bomHeader.ID}) ga {materialGroups.Count} ta yangi BOM_LINE qo'shildi.");
                    }

                    // Ishlab chiqarish: ProductionConsumptionService NORMLINES ni BOMS_AND_NORMS orqali bog‘langan norm bilan ishlatadi.
                    // Bu bog'lash muvaffaqiyatsiz bo'lsa ham NORM saqlashni to'xtatmaymiz.
                    await TryUpsertBomNormLinkAsync(bomHeader.ID, normVersion.ID, User.Identity.Name, productPNo);

                    // Log final summary
                    var actionType = isUpdate ? "yangilandi" : "yaratildi";
                            LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                        $"Mahsulot PNo: {productPNo} uchun NORM versiyasi {actionType}. Period: {viewModel.Period}, {materialGroups.Count} ta material, {viewModel.CuttingLines.Count(l => l.IsSelected)} ta kesish variantlari.");

                    transaction.Complete();

                    TempData["Success"] = "NORM muvaffaqiyatli saqlandi.";
                    return RedirectToAction("Index", new { productId = viewModel.ProductID });
                }
                catch (Exception ex)
                {
                    TempData["Error"] = BuildDetailedErrorMessage(ex);
                    if (viewModel.NormVersionID.HasValue)
                    {
                        return RedirectToAction("Edit", new { normVersionId = viewModel.NormVersionID.Value });
                    }
                    return RedirectToAction("Create", new { productId = viewModel.ProductID, period = viewModel.Period });
                }
            }
        }

        #region Helper Methods

        /// <summary>
        /// UNITS FK: faqat mavjud ID (>0). 0 yoki null — null (FK buzilishining oldini olish).
        /// </summary>
        private static int? NormalizeUnitFk(int? unitId)
        {
            if (unitId.HasValue && unitId.Value > 0)
                return unitId;
            return null;
        }

        /// <summary>
        /// UI uchun xatolikni to'liqroq ko'rsatish: inner exception zanjiri bilan.
        /// </summary>
        private static string BuildDetailedErrorMessage(Exception ex)
        {
            if (ex == null)
                return "Xatolik yuz berdi.";

            var messages = new List<string>();
            var current = ex;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    messages.Add(current.Message.Trim());
                current = current.InnerException;
            }

            var merged = string.Join(" | Inner: ", messages.Distinct());
            return $"Xatolik yuz berdi: {merged}";
        }

        /// <summary>
        /// BOM ↔ norm bog‘lanishi (ProductionConsumptionService: BOMS_AND_NORMS, IsActive = 1).
        /// Bu qadam xato bersa ham NORM save operatsiyasini yiqitmaydi.
        /// </summary>
        private async Task TryUpsertBomNormLinkAsync(int bomId, int normId, string userName, string productPNo)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE BOMS_AND_NORMS SET IsActive = 0 WHERE BOMID = {0}", bomId);

                var exists = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM BOMS_AND_NORMS WHERE BOMID = {0} AND NormID = {1}",
                    bomId, normId).FirstOrDefault();

                if (exists > 0)
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        "UPDATE BOMS_AND_NORMS SET IsActive = 1 WHERE BOMID = {0} AND NormID = {1}",
                        bomId, normId);
                }
                else
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        "INSERT INTO BOMS_AND_NORMS (BOMID, NormID, IsActive) VALUES ({0}, {1}, 1)",
                        bomId, normId);
                }
            }
            catch (Exception ex)
            {
                // Bog'lashdagi xatoni log qilamiz, lekin foydalanuvchi kiritgan norm saqlanib qoladi.
                LogHelper.LogToDatabase(
                    userName,
                    "Norm",
                    $"BOMS_AND_NORMS bog'lash xatosi. Product: {productPNo}, BOMID: {bomId}, NormID: {normId}. Tafsilot: {BuildDetailedErrorMessage(ex)}");
            }
        }

        private async Task SetUnitsViewBagAsync()
        {
            var units = await db.UNITS
                .Where(u => u.IsDeleted == null || u.IsDeleted == false)
                .OrderBy(u => u.ShortName)
                .ToListAsync();
            var items = units.Select(u => new SelectListItem
            {
                Value = u.ID.ToString(),
                Text = !string.IsNullOrEmpty(u.ShortName) ? u.ShortName : (u.UnitName ?? u.ID.ToString())
            }).ToList();
            ViewBag.UnitsList = items;
            ViewBag.UnitsJson = units.Select(u => new { id = u.ID, text = !string.IsNullOrEmpty(u.ShortName) ? u.ShortName : (u.UnitName ?? "") }).ToList();
        }

        private void RecalculateLineFields(List<NormLineViewModel> lines)
        {
            foreach (var line in lines)
            {
                if (line.MaterialID > 0 && line.Quantity > 0)
                {
                    // Olish material ga tekshirish agar it requires cutting
                    var material = db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefault(p => p.ID == line.MaterialID);
                    
                    bool requiresCutting = false;
                    if (material?.PRODUCT_CATEGORIES != null)
                    {
                        requiresCutting = material.PRODUCT_CATEGORIES.RequiresCutting;
                    }

                    // agar StandardLengthMm is not set, try ga get it dan material
                    if (!line.StandardLengthMm.HasValue || line.StandardLengthMm.Value == 0)
                    {
                        if (material != null)
                        {
                            if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                            {
                                line.StandardLengthMm = material.StandardLengthMm.Value;
                            }
                            else if (material.Length.HasValue && material.Length.Value > 0)
                            {
                                line.StandardLengthMm = material.Length.Value;
                            }
                        }
                    }

                    // Calculate consumption per product
                    // faqat divide by 1000 uchun materials that require cutting (wood, cellophane, cardboard)
                    if (requiresCutting && line.CalculatedLengthMm.HasValue)
                    {
                        // uchun cutting materials: convert mm ga m
                        line.ConsumptionPerProduct = (decimal)((line.CalculatedLengthMm.Value / 1000.0) * line.Quantity);
                    }
                    else
                    {
                        // uchun non-cutting materials: use quantity as-is (piece-based)
                        line.ConsumptionPerProduct = line.Quantity;
                    }
                    
                    // Calculate waste faqat agar it's not already set by user (user can manually edit waste uchun barcha materials)
                    // agar waste is 0 yoki not set, va material requires cutting, calculate it automatically
                    if (!line.IsWasteManual && line.WastePerProduct == 0 && requiresCutting && line.StandardLengthMm.HasValue && line.StandardLengthMm.Value > 0 && line.CalculatedLengthMm.HasValue && line.CalculatedLengthMm.Value > 0)
                    {
                        var length = line.CalculatedLengthMm.Value;
                        var standardLength = line.StandardLengthMm.Value;
                        var quantity = line.Quantity;
                        var piecesPerBar = 1; // Always 1 (removed dan UI)
                        
                        // How many bars/varaqlar we need (1 piece per bar)
                        var barsNeeded = quantity;
                        // Waste per bar = standardLength - length (1 piece per bar)
                        var wastePerBar = Math.Max(0, standardLength - length);
                        // Total waste = waste per bar * number of bars needed (convert mm ga m)
                        line.WastePerProduct = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }
                    // agar waste is already set (by user), keep it as is
                    // agar material doesn't require cutting, waste can still be set manually by user

                    // Calculate total cost
                    if (line.UnitPrice.HasValue)
                    {
                        line.TotalCostPerProduct = (line.ConsumptionPerProduct + line.WastePerProduct) * line.UnitPrice.Value;
                    }

                    // Norma va Chiqindi
                    line.Norma = line.ConsumptionPerProduct;
                    line.Chiqindi = line.WastePerProduct;
                }
            }
        }

        private (List<NormLineViewModel> optimizedLines, Dictionary<int, string> notes) OptimizeMaterialCutting(List<NormLineViewModel> lines, PRODUCT material)
        {
            // Tekshirish agar material requires cutting
            bool requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
            
            var notes = new Dictionary<int, string>();
            
            if (!requiresCutting || lines.Count <= 1)
            {
                // uchun non-cutting materials yoki single line, return barcha lines as-is
                foreach (var line in lines)
                {
                    notes[line.ID] = "Optimizatsiya qo'llanilmadi";
                }
                return (lines, notes);
            }

            // Olish standard length
            double standardLengthMm = 0;
            if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
            {
                standardLengthMm = material.StandardLengthMm.Value;
            }
            else if (material.Length.HasValue && material.Length.Value > 0)
            {
                standardLengthMm = material.Length.Value;
            }

            if (standardLengthMm <= 0)
            {
                // No standard length, return barcha lines as-is
                foreach (var line in lines)
                {
                    notes[line.ID] = "Standart uzunlik yo'q";
                }
                return (lines, notes);
            }

            // Yaratish optimized lines bilan waste optimization
            // Algorithm: Try ga use waste dan larger cuts uchun smaller cuts
            var optimizedLines = new List<NormLineViewModel>();
            var wastePool = new List<WasteSegment>(); // Track available waste dan har bir bar

            // Yaratish a dictionary ga map line IDs ga their original position (1-based index)
            var linePositionMap = new Dictionary<int, int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].ID > 0)
                {
                    linePositionMap[lines[i].ID] = i + 1; // 1-based position
                }
            }

            // Sort lines by cut length (descending) ga process larger cuts birinchi
            // This way, larger cuts yaratish waste that can be used uchun smaller cuts
            var sortedLines = lines.Where(l => l.CalculatedLengthMm.HasValue && l.CalculatedLengthMm.Value > 0)
                .OrderByDescending(l => l.CalculatedLengthMm.Value)
                .ToList();

            // Track which lines use waste dan which previous lines (uchun notes)
            var lineNotes = new Dictionary<int, string>(); // line.ID -> note

            foreach (var line in sortedLines)
            {
                var cutLength = (double)line.CalculatedLengthMm.Value;
                var quantity = line.Quantity;
                var remainingQuantity = quantity;

                double totalWaste = 0;
                int newBarsNeeded = 0;
                string note = "";
                int piecesFromWaste = 0;
                double wasteUsedFrom = 0;

                // Urinish ga use available waste dan waste pool
                var wasteToRemove = new List<WasteSegment>();
                var wasteUsedFromLines = new List<(int sourceLineId, double wasteLength, int piecesUsed)>(); // Track which lines' waste we're foydalanib
                
                foreach (var waste in wastePool.OrderByDescending(w => w.Length).ToList())
                {
                    if (waste.Length >= cutLength && waste.RemainingQuantity > 0 && remainingQuantity > 0)
                    {
                        // How many pieces can we cut dan this waste?
                        var piecesFromThisWaste = Math.Min(
                            Math.Min((int)(waste.Length / cutLength), waste.RemainingQuantity),
                            remainingQuantity
                        );

                        if (piecesFromThisWaste > 0)
                        {
                            piecesFromWaste += piecesFromThisWaste;
                            wasteUsedFrom = waste.OriginalLength; // Store original waste length
                            wasteUsedFromLines.Add((waste.SourceLineID, waste.OriginalLength, piecesFromThisWaste));
                            remainingQuantity -= piecesFromThisWaste;
                            waste.RemainingQuantity -= piecesFromThisWaste;
                            
                            // Yangilash waste: when we cut dan waste, the waste length decreases
                            // But we need ga track how much waste is left dan har bir bar
                            // agar we used barcha pieces dan this waste segment, remove it
                            // Otherwise, yangilash the length (waste becomes smaller)
                            var usedLength = piecesFromThisWaste * cutLength;
                            
                            // Tekshirish agar we can still use this waste uchun more cuts
                            var remainingWasteLength = waste.Length - usedLength;
                            if (remainingWasteLength < cutLength || waste.RemainingQuantity <= 0)
                            {
                                wasteToRemove.Add(waste);
                            }
                            else
                            {
                                // Yangilash waste length uchun future use
                                waste.Length = remainingWasteLength;
                            }
                        }
                    }
                }

                // O'chirish used waste segments
                foreach (var waste in wasteToRemove)
                {
                    wastePool.Remove(waste);
                }

                // Calculate how many new bars we need uchun remaining quantity
                if (remainingQuantity > 0)
                {
                    var totalNeeded = cutLength * remainingQuantity;
                    newBarsNeeded = (int)Math.Ceiling(totalNeeded / standardLengthMm);
                    
                    // Calculate waste dan new bars
                    var totalLengthFromNewBars = newBarsNeeded * standardLengthMm;
                    var wasteFromNewBars = totalLengthFromNewBars - totalNeeded;
                    
                    // Qo'shish waste ga pool uchun future use
                    if (wasteFromNewBars > 0)
                    {
                        wastePool.Add(new WasteSegment
                        {
                            Length = wasteFromNewBars,
                            RemainingQuantity = newBarsNeeded, // Number of bars that produced this waste
                            OriginalLength = wasteFromNewBars,
                            SourceLineID = line.ID // Track which line generated this waste
                        });
                    }
                    
                    totalWaste = wasteFromNewBars;
                }

                // Yaratish detailed note about waste usage bilan position numbers
                if (piecesFromWaste > 0 && remainingQuantity == 0)
                {
                    // barcha pieces were cut dan waste
                    if (wasteUsedFromLines.Count > 0)
                    {
                        var wasteInfo = string.Join(", ", wasteUsedFromLines.Select(w => 
                        {
                            var pos = linePositionMap.ContainsKey(w.sourceLineId) ? linePositionMap[w.sourceLineId] : 0;
                            return pos > 0 
                                ? $"{w.piecesUsed} dona {pos}-pozitsiyadan {w.wasteLength:F0}mm chiqindidan"
                                : $"{w.piecesUsed} dona {w.wasteLength:F0}mm chiqindidan";
                        }));
                        note = $"Barcha {quantity} dona {cutLength}mm kesim ({wasteInfo}) ishlatilgan. Chiqindi yo'q.";
                    }
                    else
                    {
                        note = $"Barcha {quantity} dona {cutLength}mm uzunlikdagi kesim {wasteUsedFrom:F0}mm chiqindidan foydalanilgan. Chiqindi yo'q.";
                    }
                }
                else if (piecesFromWaste > 0 && remainingQuantity > 0)
                {
                    // Some pieces dan waste, some dan new bars
                    var wasteInfo = "";
                    if (wasteUsedFromLines.Count > 0)
                    {
                        wasteInfo = string.Join(", ", wasteUsedFromLines.Select(w => 
                        {
                            var pos = linePositionMap.ContainsKey(w.sourceLineId) ? linePositionMap[w.sourceLineId] : 0;
                            return pos > 0 
                                ? $"{w.piecesUsed} dona {pos}-pozitsiyadan {w.wasteLength:F0}mm chiqindidan"
                                : $"{w.piecesUsed} dona {w.wasteLength:F0}mm chiqindidan";
                        }));
                    }
                    else
                    {
                        wasteInfo = $"{piecesFromWaste} dona {wasteUsedFrom:F0}mm chiqindidan";
                    }
                    note = $"{piecesFromWaste} dona ({wasteInfo}), {remainingQuantity} dona yangi {standardLengthMm:F0}mm bar/sheet dan kesilgan. {totalWaste:F0}mm chiqindi hosil bo'lgan.";
                }
                else if (totalWaste > 0)
                {
                    // barcha dan new bars
                    note = $"Yangi {newBarsNeeded} ta {standardLengthMm:F0}mm bar/sheet ishlatilgan. {totalWaste:F0}mm chiqindi hosil bo'lgan (keyingi pozitsiyalar uchun ishlatilishi mumkin).";
                }
                else
                {
                    note = $"Yangi {newBarsNeeded} ta {standardLengthMm:F0}mm bar/sheet ishlatilgan. Chiqindi yo'q.";
                }

                // Yaratish optimized line
                var optimizedLine = new NormLineViewModel
                {
                    ID = line.ID,
                    MaterialID = line.MaterialID,
                    MaterialCode = line.MaterialCode,
                    MaterialName = line.MaterialName,
                    CalculatedLengthMm = line.CalculatedLengthMm,
                    Quantity = line.Quantity,
                    PiecesPerBar = 1,
                    StandardLengthMm = line.StandardLengthMm,
                    UnitPrice = line.UnitPrice,
                    IsSelected = line.IsSelected
                };

                // Recalculate consumption va waste
                optimizedLine.ConsumptionPerProduct = (decimal)((cutLength / 1000.0) * quantity);
                optimizedLine.WastePerProduct = (decimal)(totalWaste / 1000.0);
                optimizedLine.TotalCostPerProduct = (optimizedLine.ConsumptionPerProduct + optimizedLine.WastePerProduct) * (optimizedLine.UnitPrice ?? 0);
                optimizedLine.Norma = optimizedLine.ConsumptionPerProduct;
                optimizedLine.Chiqindi = optimizedLine.WastePerProduct;

                // Saqlash note uchun this line
                lineNotes[line.ID] = note;

                optimizedLines.Add(optimizedLine);
            }

            // Qo'shish lines without CalculatedLengthMm as-is
            foreach (var line in lines.Where(l => !l.CalculatedLengthMm.HasValue || l.CalculatedLengthMm.Value <= 0))
            {
                optimizedLines.Add(line);
                notes[line.ID] = "Uzunlik kiritilmagan";
            }

            return (optimizedLines, notes);
        }

        // yordamchi class uchun tracking waste segments
        private class WasteSegment
        {
            public double Length { get; set; }
            public int RemainingQuantity { get; set; }
            public double OriginalLength { get; set; } // Original waste length uchun tracking
            public int SourceLineID { get; set; } // Which line generated this waste
        }

        private DateTime? ParsePeriod(string period)
        {
            if (string.IsNullOrEmpty(period) || period.Length < 7)
                return null;

            if (DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime result))
                return result;

            return null;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

