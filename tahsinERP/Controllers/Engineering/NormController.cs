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
using tahsinERP.Filters;
using tahsinERP.Services.Norm;

namespace tahsinERP.Controllers.Engineering
{
    [Permission]
    public class NormController : BaseController
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
            var normVersions = await db.NORMVERSIONS.AsNoTracking()
                .Where(n => n.ProductID == id.Value && n.IsDeleted != true)
                .OrderByDescending(n => n.EffectiveFrom)
                .Select(n => new
                {
                    n.ID,
                    n.EffectiveFrom,
                    n.EffectiveTo,
                    n.Comment,
                    LineCount = n.NORMLINES.Count()
                })
                .ToListAsync();

            // Barcha davrlarning NORM_INPUT_LINES qatorlari va material kategoriyalari bir martada
            // (avval har versiyaga 2 ta so'rov ketardi, MES-P13).
            var periods = normVersions
                .Where(n => n.EffectiveFrom.HasValue)
                .Select(n => n.EffectiveFrom.Value.ToString("yyyy-MM"))
                .Distinct()
                .ToList();
            var allInputLines = await db.NORM_INPUT_LINES.AsNoTracking()
                .Where(il => il.ProductID == id.Value && periods.Contains(il.Month))
                .Select(il => new { il.Month, il.MaterialID, il.CutLengthMm, il.PiecesPerProduct, il.Consumption, il.WasteAmount, il.Price })
                .ToListAsync();
            var materialIdsAll = allInputLines.Select(il => il.MaterialID).Distinct().ToList();
            var requiresCuttingByMaterial = await db.PRODUCTS.AsNoTracking()
                .Where(p => materialIdsAll.Contains(p.ID))
                .Select(p => new { p.ID, RequiresCutting = p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting })
                .ToDictionaryAsync(p => p.ID, p => p.RequiresCutting);
            var inputLinesByPeriod = allInputLines.GroupBy(il => il.Month).ToDictionary(g => g.Key, g => g.ToList());

            var viewModel = new ProductNormDetailsViewModel
            {
                ProductID = product.ID,
                ProductCode = product.PNo,
                ProductName = product.Name,
                NormVersions = normVersions.Select(n =>
                {
                    var period = n.EffectiveFrom.HasValue ? n.EffectiveFrom.Value.ToString("yyyy-MM") : "";

                    decimal totalSarfSumma = 0;
                    decimal totalChiqindiSumma = 0;
                    if (inputLinesByPeriod.TryGetValue(period, out var inputLines))
                    {
                        foreach (var il in inputLines)
                        {
                            if (il.Price <= 0)
                            {
                                continue;
                            }

                            requiresCuttingByMaterial.TryGetValue(il.MaterialID, out var requiresCutting);

                            // ViewDetails bilan bir xil mantiq (NormLineMath)
                            var consumptionMeters = NormLineMath.LineConsumption(requiresCutting, il.CutLengthMm, il.PiecesPerProduct, il.Consumption);
                            var wasteMeters = NormLineMath.WasteToDisplay(il.WasteAmount);

                            totalSarfSumma += consumptionMeters * il.Price;
                            totalChiqindiSumma += wasteMeters * il.Price;
                        }
                    }

                    return new NormVersionItemViewModel
                    {
                        NormVersionID = n.ID,
                        Period = period,
                        EffectiveFrom = n.EffectiveFrom,
                        EffectiveTo = n.EffectiveTo,
                        Comment = n.Comment,
                        LineCount = n.LineCount,
                        TotalSarfSumma = totalSarfSumma,
                        TotalChiqindiSumma = totalChiqindiSumma,
                        // Umumiy summa: A + B
                        UmumiySumma = totalSarfSumma + totalChiqindiSumma
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
            await PopulateCuttingLinesFromPeriodAsync(viewModel, viewModel.ProductID, period);

            // Umumiy norma/chiqindi faqat yog'och kategoriyasidagi materiallar uchun; summa — barcha materiallar
            var woodByMaterial = await GetWoodFlagsAsync(viewModel.CuttingLines.Select(l => l.MaterialID));
            ApplyTotals(viewModel, woodByMaterial);

            // View yog'och belgisini kutadi (MaterialID -> IsWood)
            ViewBag.MaterialCategories = woodByMaterial;

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
                    var productCode = await db.PRODUCTS.Where(p => p.ID == productId).Select(p => p.PNo).FirstOrDefaultAsync() ?? "";
                    var period = normVersion.EffectiveFrom.HasValue ? normVersion.EffectiveFrom.Value.ToString("yyyy-MM") : "";

                    // O'chirish NORM_INPUT_LINES (detailed cutting variants) uchun this period
                    int deletedInputLinesCount = 0;
                    if (!string.IsNullOrEmpty(period))
                    {
                        var inputLines = await db.NORM_INPUT_LINES
                            .Where(il => il.ProductID == productId && il.Month == period)
                            .ToListAsync();
                        deletedInputLinesCount = inputLines.Count;
                        db.NORM_INPUT_LINES.RemoveRange(inputLines);
                    }

                    // O'chirish NORMLINES
                    var deletedNormLinesCount = normVersion.NORMLINES.Count;
                    db.NORMLINES.RemoveRange(normVersion.NORMLINES.ToList());

                    // BOM ↔ norm bog'lanishi: avval BOMS_AND_NORMS qatorlari qolib ketardi (FK xatosi yoki yetim yozuv, MES-B08)
                    await db.Database.ExecuteSqlCommandAsync("DELETE FROM BOMS_AND_NORMS WHERE NormID = {0}", normVersion.ID);

                    // Hard o'chirish NORMVERSION (entry.remove)
                    db.NORMVERSIONS.Remove(normVersion);
                    await db.SaveChangesAsync();

                    // Identity RESEED (DBCC CHECKIDENT) olib tashlandi: ID'lardagi "teshik" xato emas,
                    // reseed esa db_owner huquqini talab qiladi va parallel INSERT bilan PK to'qnashuviga olib keladi.

                    // BOM endi qolgan eng so'nggi norma bo'yicha qayta quriladi (o'chirilgan norma bo'yicha sarf davom etmaydi);
                    // norma qolmasa BOM faolsizlantiriladi.
                    string bomNote = null;
                    if (productId.HasValue)
                    {
                        bomNote = await SyncBomToLatestNormAsync(productId.Value, productCode, null, null);
                        await db.SaveChangesAsync();
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                        $"Mahsulot PNo: {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}, Period: {period}) o'chirildi: " +
                        $"{deletedInputLinesCount} ta NORM_INPUT_LINE, {deletedNormLinesCount} ta NORMLINE. {bomNote}");

                    transaction.Complete();

                    TempData["Success"] = "NORM muvaffaqiyatli o'chirildi." + (string.IsNullOrEmpty(bomNote) ? "" : " " + bomNote);
                    return RedirectToAction("Details", new { id = productId });
                }
                catch (Exception ex)
                {
                    TempData["Error"] = BuildDetailedErrorMessage(ex);
                    return RedirectToAction("Index");
                }
            }
        }

        // Olish so'rovi: Norm/yaratish
        public async Task<ActionResult> Create(int? productId, string period, int? templateNormVersionId)
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

                // Tekshirish agar norm already mavjud uchun this product va period
                if (await FindNormForPeriodAsync(productId.Value, selectedPeriod, null) != null)
                {
                    TempData["Error"] = $"Bu mahsulot uchun {selectedPeriod} oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang.";
                    return RedirectToAction("Details", new { id = productId.Value });
                }

                viewModel.ProductID = product.ID;
                viewModel.ProductCode = product.PNo;
                viewModel.ProductName = product.Name;
                viewModel.Period = selectedPeriod;

                if (templateNormVersionId.HasValue)
                {
                    var loaded = await ApplyTemplateFromNormVersionAsync(viewModel, templateNormVersionId.Value, overrideHeaderFields: true);
                    if (!loaded)
                    {
                        TempData["Error"] = "Tanlangan oldingi norma topilmadi yoki ushbu mahsulotga tegishli emas.";
                    }
                }
            }
            else
            {
                TempData["Error"] = "Mahsulot ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            await PopulateMaterialsAsync(viewModel.CuttingLines);

            ViewBag.PreviousNorms = await GetPreviousNormOptionsAsync(viewModel.ProductID, null);
            ViewBag.SelectedTemplateNormVersionId = templateNormVersionId;
            await SetUnitsViewBagAsync();
            return View("Create", viewModel);
        }

        // Olish so'rovi: Norm/Edit
        public async Task<ActionResult> Edit(int? normVersionId, int? templateNormVersionId)
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

            if (templateNormVersionId.HasValue)
            {
                var loaded = await ApplyTemplateFromNormVersionAsync(viewModel, templateNormVersionId.Value, overrideHeaderFields: true);
                if (!loaded)
                {
                    TempData["Error"] = "Tanlangan oldingi norma topilmadi yoki ushbu mahsulotga tegishli emas.";
                    var currentPeriod = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
                    await PopulateCuttingLinesFromPeriodAsync(viewModel, viewModel.ProductID, currentPeriod);
                    await RecalculateTotalsForViewModelAsync(viewModel);
                }
            }
            else
            {
                var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
                await PopulateCuttingLinesFromPeriodAsync(viewModel, viewModel.ProductID, period);
                await RecalculateTotalsForViewModelAsync(viewModel);
            }

            // Materiallar (xomashyo, komponent va satrlardagi boshqa materiallar)
            await PopulateMaterialsAsync(viewModel.CuttingLines);

            ViewBag.PreviousNorms = await GetPreviousNormOptionsAsync(viewModel.ProductID, null);
            ViewBag.SelectedTemplateNormVersionId = templateNormVersionId;
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

            await PopulateMaterialsAsync(viewModel.CuttingLines);

            ViewBag.PreviousNorms = await GetPreviousNormOptionsAsync(viewModel.ProductID, viewModel.NormVersionID);
            ViewBag.SelectedTemplateNormVersionId = null;
            await SetUnitsViewBagAsync();
            return View(viewModel);
        }

        // Olish so'rovi: Norm/GetMaterialData/{materialId}
        [HttpGet]
        public async Task<JsonResult> GetMaterialData(int materialId)
        {
            try
            {
                var material = await db.PRODUCTS.AsNoTracking()
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .FirstOrDefaultAsync(p => p.ID == materialId);

                if (material == null)
                {
                    return Json(new { success = false, message = "Material topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Olish latest price dan INCOME_PRODUCTS
                var latestIncome = await db.INCOME_PRODUCTS.AsNoTracking()
                    .Where(ip => ip.ProductID == materialId && ip.PiecePrice.HasValue)
                    .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                    .Select(ip => new { Price = ip.PiecePrice.Value })
                    .FirstOrDefaultAsync();

                // Kesish talab qilinadimi (kategoriya) va yog'och turi (faqat shu tur uchun kirim narxini metrga aylantiramiz)
                bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                bool isWoodMaterial = NormLineMath.IsWoodCategory(material.PRODUCT_CATEGORIES);

                // Use StandardLengthMm agar available, otherwise use Length
                double standardLengthMm = NormLineMath.EffectiveStandardLengthMm(material);

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
                return JsonError(ex);
            }
        }

        // POST so'rovi: Norm/Calculate (Optimization)
        [HttpPost]
        [ValidateAntiForgeryHeader]
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
                catch (JsonException)
                {
                    // Xom JSON foydalanuvchiga qaytarilmaydi (MES-B16)
                    return Json(new { success = false, message = "Ma'lumotlar formati noto'g'ri (JSON)." }, JsonRequestBehavior.AllowGet);
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

                // Butun jadval bo'yicha (faqat tanlanganlar emas) — material (xomashyo) bo'yicha guruhlaymiz.
                // Bir xil xomashyoning uzun kesimidan qolgan chiqindi, undan kalta kesimlarga ishlatiladi.
                var materialGroups = viewModel.CuttingLines
                    .Where(l => l.MaterialID > 0)
                    .GroupBy(l => l.MaterialID)
                    .ToList();

                if (materialGroups.Count == 0)
                {
                    return Json(new { success = false, message = "Kesish variantlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                decimal totalNorm = 0;
                decimal totalWaste = 0;
                decimal totalCost = 0;
                int reusedLinesCount = 0;

                // Barcha optimizatsiya qilingan qatorlar — jadvalda ko'rsatish uchun.
                var allOptimizedLines = new List<object>();

                // Materiallar bir so'rovda (avval har guruhga alohida so'rov)
                var groupMaterialIds = materialGroups.Select(g => g.Key).ToList();
                var materialsById = await db.PRODUCTS.AsNoTracking()
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => groupMaterialIds.Contains(p.ID))
                    .ToDictionaryAsync(p => p.ID);

                foreach (var group in materialGroups)
                {
                    var materialId = group.Key;
                    var lines = group.ToList();
                    materialsById.TryGetValue(materialId, out var material);

                    bool requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;

                    // Chiqindidan foydalanish algoritmi (faqat kesiladigan xomashyo uchun ish beradi).
                    var optimizedLines = OptimizeMaterialCutting(lines, material);

                    foreach (var line in optimizedLines)
                    {
                        if (line.UsesWaste)
                            reusedLinesCount++;

                        // Norm/chiqindi kartalari faqat kesiladigan materiallar bo'yicha hisoblanadi.
                        if (requiresCutting)
                        {
                            totalNorm += line.ConsumptionPerProduct;
                            totalWaste += line.WastePerProduct;
                        }
                        totalCost += line.TotalCostPerProduct;

                        allOptimizedLines.Add(new
                        {
                            id = line.ID,
                            materialId = materialId,
                            materialCode = line.MaterialCode ?? material?.PNo ?? "",
                            materialName = line.MaterialName ?? material?.Name ?? "",
                            calculatedLengthMm = line.CalculatedLengthMm ?? 0,
                            quantity = line.Quantity,
                            consumptionPerProduct = line.ConsumptionPerProduct,
                            wastePerProduct = line.WastePerProduct,
                            unitPrice = line.UnitPrice ?? 0,
                            totalCostPerProduct = line.TotalCostPerProduct,
                            standardLengthMm = line.StandardLengthMm ?? 0,
                            usesWaste = line.UsesWaste,
                            note = line.OptimizationNote ?? ""
                        });
                    }
                }

                var message = reusedLinesCount > 0
                    ? $"Optimizatsiya: {reusedLinesCount} ta kesim chiqindidan olindi (sarf 0). Umumiy norma {totalNorm:F4} m, umumiy chiqindi {totalWaste:F4} m, umumiy summa {totalCost:F2}."
                    : $"Optimizatsiya: chiqindidan qayta ishlatish imkoni topilmadi. Umumiy norma {totalNorm:F4} m, umumiy chiqindi {totalWaste:F4} m, umumiy summa {totalCost:F2}.";

                return Json(new
                {
                    success = true,
                    optimization = new
                    {
                        message = message,
                        suggestedTotalNorm = totalNorm,
                        suggestedTotalWaste = totalWaste,
                        suggestedTotalCost = totalCost,
                        optimizedLines = allOptimizedLines
                    },
                    totalNorm = totalNorm,
                    totalWaste = totalWaste,
                    totalCost = totalCost
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return JsonError(ex, "Hisoblashda xatolik yuz berdi.");
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

            // Shu davr uchun boshqa norma bormi — yangi normada ham, tahrirda ham (davr o'zgartirilishi mumkin).
            // Avval faqat yangi normalar tekshirilardi va tahrirda boshqa versiya qatorlari o'chib ketardi (MES-B06).
            if (await FindNormForPeriodAsync(viewModel.ProductID, viewModel.Period, viewModel.NormVersionID) != null)
            {
                TempData["Error"] = $"Bu mahsulot uchun {viewModel.Period} oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang.";
                return RedirectToAction("Details", new { id = viewModel.ProductID });
            }

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
                                CuttingMethod = Request.Form[$"CuttingLines[{index}].CuttingMethod"],
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
                    // Samaradorlik (MES-P14): materiallar va foydalanuvchi ID bir marta olinadi, NORMLINES xotiradagi
                    // yangi qatorlardan yig'iladi, bitta yakuniy log yoziladi.
                    var productPNo = await db.PRODUCTS.Where(p => p.ID == viewModel.ProductID).Select(p => p.PNo).FirstOrDefaultAsync()
                        ?? viewModel.ProductCode ?? "Noma'lum";
                    var userId = RoleHelper.GetUserId(CurrentUserName);
                    var linesToSave = viewModel.CuttingLines.Where(l => l.MaterialID > 0).ToList();
                    var materials = await LoadMaterialInfoAsync(linesToSave.Select(l => l.MaterialID));

                    // Olish yoki yaratish NORMVERSION
                    NORMVERSION normVersion;
                    bool isUpdate = false;
                    string oldPeriod = null;
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

                        // Davr o'zgarishidan OLDINGI qiymat — eski NORM_INPUT_LINES shu bo'yicha o'chiriladi
                        isUpdate = true;
                        oldPeriod = normVersion.EffectiveFrom?.ToString("yyyy-MM");
                        normVersion.EffectiveFrom = ParsePeriod(viewModel.Period);
                        normVersion.EffectiveTo = viewModel.EffectiveTo;
                        normVersion.Comment = viewModel.Comment;

                        // O'chirish existing lines
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES.ToList());
                    }
                    else
                    {
                        normVersion = new NORMVERSION
                        {
                            ProductID = viewModel.ProductID,
                            EffectiveFrom = ParsePeriod(viewModel.Period),
                            EffectiveTo = viewModel.EffectiveTo,
                            Comment = viewModel.Comment,
                            IsDeleted = false
                        };
                        db.NORMVERSIONS.Add(normVersion);
                    }

                    // Eski davr (o'z qatorlari) va yangi davr (boshqa versiya yo'qligi yuqorida tekshirildi —
                    // u yerdagi qatorlar yetim va NORMLINES yig'indisiga aralashib qolardi) qatorlarini tozalaymiz.
                    var newPeriod = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? viewModel.Period;
                    var periodsToClear = new[] { oldPeriod, newPeriod, viewModel.Period }
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct()
                        .ToList();
                    var existingInputLines = await db.NORM_INPUT_LINES
                        .Where(il => il.ProductID == viewModel.ProductID && periodsToClear.Contains(il.Month))
                        .ToListAsync();
                    db.NORM_INPUT_LINES.RemoveRange(existingInputLines);

                    // Saqlash detailed cutting variants ga NORM_INPUT_LINES
                    // IMPORTANT: saqlash barcha lines dan viewModel, not just IsSelected ones
                    var newInputLines = new List<NORM_INPUT_LINES>();
                    foreach (var line in linesToSave)
                    {
                        materials.TryGetValue(line.MaterialID, out var material);
                        bool requiresCutting = material?.RequiresCutting ?? false;
                        double standardLengthMm = material?.StandardLengthMm ?? 0;

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

                        // Note: TotalAmount — DB'da hisoblanadigan ustun (((CutLengthMm / 1000.0) * PiecesPerProduct) * Price)
                        var inputLine = new NORM_INPUT_LINES
                        {
                            ProductID = viewModel.ProductID,
                            MaterialID = line.MaterialID,
                            CutLengthMm = (int)(line.CalculatedLengthMm ?? 0),
                            PiecesPerProduct = line.Quantity,
                            Month = viewModel.Period,
                            CreatedByUserID = userId,
                            CreatedDT = DateTime.Now,
                            Price = line.UnitPrice ?? 0,
                            Consumption = consumption > 0 ? (double?)consumption : null,
                            WasteAmount = line.IsWasteManual ? (double?)wasteAmount : (wasteAmount > 0 ? (double?)wasteAmount : null),
                            // material topilmasa (o'chirilgan) — NRE emas, birliksiz saqlanadi (MES-B21)
                            UnitID = NormalizeUnitFk(line.UnitID) ?? NormalizeUnitFk(material != null && material.UnitID > 0 ? material.UnitID : (int?)null),
                            Comment = string.IsNullOrWhiteSpace(line.CuttingMethod) ? null : line.CuttingMethod.Trim()
                        };

                        db.NORM_INPUT_LINES.Add(inputLine);
                        newInputLines.Add(inputLine);
                    }

                    await db.SaveChangesAsync();

                    // Aggregated NORMLINE har material uchun — yangi NORM_INPUT_LINES dan (xotirada)
                    var materialGroups = newInputLines.GroupBy(il => il.MaterialID).ToList();
                    foreach (var group in materialGroups)
                    {
                        // QtyPerUnit = Sum of Consumption
                        var qtyPerUnit = group
                            .Where(il => il.Consumption.HasValue)
                            .Sum(il => (decimal)il.Consumption.Value);

                        // WastePercent = Sum of WasteAmount / QtyPerUnit (as percentage)
                        var totalWasteAmount = group
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

                        materials.TryGetValue(group.Key, out var materialNl);
                        int? normLineUnitId = NormalizeUnitFk(
                            group.Select(il => il.UnitID).FirstOrDefault(u => u.HasValue && u.Value > 0))
                            ?? NormalizeUnitFk(materialNl != null && materialNl.UnitID > 0 ? materialNl.UnitID : (int?)null);

                        db.NORMLINES.Add(new NORMLINE
                        {
                            NormID = normVersion.ID,
                            MaterialID = group.Key,
                            QtyPerUnit = qtyPerUnit,
                            WastePercent = wastePercent,
                            UnitID = normLineUnitId
                        });
                    }

                    await db.SaveChangesAsync();

                    // BOM faqat mahsulotning ENG SO'NGGI normasi bo'yicha quriladi. Avval har saqlashda (eski oy bo'lsa ham)
                    // yagona BOM_HEADERS ustidan yozilardi va sarf oxirgi tahrirlangan oy normasiga o'tib qolardi (MES-B07).
                    var bomNote = await SyncBomToLatestNormAsync(
                        viewModel.ProductID, productPNo, normVersion, BomSourceLinesFromViewModel(linesToSave));
                    await db.SaveChangesAsync();

                    var actionType = isUpdate ? "yangilandi" : "yaratildi";
                    LogHelper.LogToDatabase(User.Identity.Name, "Norm",
                        $"Mahsulot PNo: {productPNo} uchun NORM versiyasi (ID: {normVersion.ID}) {actionType}. Period: {viewModel.Period}" +
                        (isUpdate && oldPeriod != newPeriod ? $" (avval {oldPeriod})" : "") +
                        $", {newInputLines.Count} ta NORM_INPUT_LINE ({existingInputLines.Count} ta eski o'chirildi), {materialGroups.Count} ta material. {bomNote}");

                    transaction.Complete();

                    TempData["Success"] = "NORM muvaffaqiyatli saqlandi." + (string.IsNullOrEmpty(bomNote) ? "" : " " + bomNote);
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

        /// <summary>Material ma'lumoti (Confirm/BOM qurish uchun bir martada yuklanadi).</summary>
        private sealed class MaterialInfo
        {
            public int ID { get; set; }
            public string PNo { get; set; }
            public string Name { get; set; }
            public int UnitID { get; set; }
            public bool RequiresCutting { get; set; }
            public double StandardLengthMm { get; set; }
        }

        /// <summary>BOM qatorini hisoblash uchun manba (VM qatori yoki DB'dagi NORM_INPUT_LINES).</summary>
        private sealed class BomSourceLine
        {
            public int MaterialID { get; set; }
            public decimal CutLengthMm { get; set; }
            public decimal Quantity { get; set; }
            public decimal ConsumptionMeters { get; set; }
            public decimal WasteMeters { get; set; }
            public int? UnitID { get; set; }
        }

        private async Task<Dictionary<int, MaterialInfo>> LoadMaterialInfoAsync(IEnumerable<int> materialIds)
        {
            var ids = materialIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, MaterialInfo>();

            var rows = await db.PRODUCTS.AsNoTracking()
                .Where(p => ids.Contains(p.ID))
                .Select(p => new
                {
                    p.ID,
                    p.PNo,
                    p.Name,
                    p.UnitID,
                    p.StandardLengthMm,
                    p.Length,
                    RequiresCutting = p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting
                })
                .ToListAsync();

            return rows.ToDictionary(p => p.ID, p => new MaterialInfo
            {
                ID = p.ID,
                PNo = p.PNo,
                Name = p.Name,
                UnitID = p.UnitID,
                RequiresCutting = p.RequiresCutting,
                StandardLengthMm = NormLineMath.EffectiveStandardLengthMm(p.StandardLengthMm, p.Length)
            });
        }

        /// <summary>Shu mahsulot va davr (yyyy-MM) uchun boshqa (excludeId dan farqli) aktiv norma (dublikat #5).</summary>
        private async Task<NORMVERSION> FindNormForPeriodAsync(int productId, string period, int? excludeNormVersionId)
        {
            var parts = period?.Split('-');
            if (parts == null || parts.Length < 2 ||
                !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) ||
                month < 1 || month > 12 || year < 1900 || year > 2100)
            {
                return null;
            }

            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1);
            return await db.NORMVERSIONS
                .Where(n => n.ProductID == productId &&
                       n.IsDeleted != true &&
                       (!excludeNormVersionId.HasValue || n.ID != excludeNormVersionId.Value) &&
                       n.EffectiveFrom.HasValue &&
                       n.EffectiveFrom.Value >= periodStart &&
                       n.EffectiveFrom.Value < periodEnd)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Materiallar dropdowni (xomashyo + komponent + satrlarda ishlatilgan boshqa materiallar) — avval Create/Edit GET va
        /// Edit POST xatosida uch marta nusxalangan edi (dublikat #4).
        /// </summary>
        private async Task PopulateMaterialsAsync(IEnumerable<NormLineViewModel> lines)
        {
            var materials = await db.PRODUCTS.AsNoTracking()
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToListAsync();

            // Satrdagi materiallar filterga tushmasa ham dropdownda ko'rinishi kerak
            var known = new HashSet<int>(materials.Select(m => m.ID));
            var missingMaterialIds = (lines ?? Enumerable.Empty<NormLineViewModel>())
                .Select(l => l.MaterialID)
                .Where(id => id > 0 && !known.Contains(id))
                .Distinct()
                .ToList();
            if (missingMaterialIds.Any())
            {
                materials.AddRange(await db.PRODUCTS.AsNoTracking()
                    .Where(p => missingMaterialIds.Contains(p.ID) && p.IsDeleted == false)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync());
            }

            var ordered = materials.OrderBy(m => m.PNo).ToList();

            // SelectList "PNo - Name" formatida
            ViewBag.Materials = new SelectList(
                ordered.Select(m => new SelectListItem { Value = m.ID.ToString(), Text = $"{m.PNo} - {m.Name ?? m.PNo}" }).ToList(),
                "Value", "Text", null);

            // JSON uchun oddiy ro'yxat (circular reference bo'lmasligi uchun)
            ViewBag.MaterialsJson = ordered
                .Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo })
                .ToList();
        }

        /// <summary>MaterialID → yog'och kategoriyasimi.</summary>
        private async Task<Dictionary<int, bool>> GetWoodFlagsAsync(IEnumerable<int> materialIds)
        {
            var ids = materialIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, bool>();

            var rows = await db.PRODUCTS.AsNoTracking()
                .Where(p => ids.Contains(p.ID))
                .Select(p => new
                {
                    p.ID,
                    HasCategory = p.PRODUCT_CATEGORIES != null,
                    p.PRODUCT_CATEGORIES.RawMaterialGroup,
                    CategoryName = p.PRODUCT_CATEGORIES.Name
                })
                .ToListAsync();
            return rows.ToDictionary(p => p.ID, p => p.HasCategory && NormLineMath.IsWoodCategory(p.RawMaterialGroup, p.CategoryName));
        }

        /// <summary>Umumiy norma/chiqindi — faqat yog'och materiallar; umumiy summa — barchasi.</summary>
        private static void ApplyTotals(ProductNormViewModel viewModel, Dictionary<int, bool> woodByMaterial)
        {
            var lines = viewModel.CuttingLines ?? new List<NormLineViewModel>();
            Func<NormLineViewModel, bool> isWood = l => woodByMaterial.TryGetValue(l.MaterialID, out var w) && w;
            viewModel.TotalNorm = lines.Where(isWood).Sum(l => l.ConsumptionPerProduct);
            viewModel.TotalWaste = lines.Where(isWood).Sum(l => l.WastePerProduct);
            viewModel.TotalCost = lines.Sum(l => l.TotalCostPerProduct);
        }

        private static List<BomSourceLine> BomSourceLinesFromViewModel(IEnumerable<NormLineViewModel> lines)
        {
            return lines.Select(l => new BomSourceLine
            {
                MaterialID = l.MaterialID,
                CutLengthMm = (decimal)(l.CalculatedLengthMm ?? 0),
                Quantity = l.Quantity,
                ConsumptionMeters = l.ConsumptionPerProduct,
                WasteMeters = l.WastePerProduct,
                UnitID = l.UnitID
            }).ToList();
        }

        /// <summary>
        /// BOM'ni mahsulotning eng so'nggi (EffectiveFrom bo'yicha) normasiga moslaydi (MES-B07/B08):
        ///  - eng so'nggi norma hozir saqlangan bo'lsa — uning UI qatorlaridan;
        ///  - boshqa norma eng so'nggi bo'lsa va BOM allaqachon o'sha davrda bo'lsa — tegilmaydi, aks holda
        ///    o'sha normaning NORM_INPUT_LINES qatorlaridan qayta quriladi;
        ///  - norma qolmagan bo'lsa — BOM faolsizlantiriladi.
        /// Qaytaradi: foydalanuvchiga ko'rsatiladigan izoh (yoki null). SaveChanges'ni chaqiruvchi qiladi.
        /// </summary>
        private async Task<string> SyncBomToLatestNormAsync(int productId, string productPNo, NORMVERSION current, List<BomSourceLine> currentLines)
        {
            var versions = await db.NORMVERSIONS
                .Where(n => n.ProductID == productId && n.IsDeleted != true)
                .Select(n => new { n.ID, n.EffectiveFrom })
                .ToListAsync();
            var latest = versions
                .OrderByDescending(n => n.EffectiveFrom ?? DateTime.MinValue)
                .ThenByDescending(n => n.ID)
                .FirstOrDefault();

            var bomHeader = await db.BOM_HEADERS.FirstOrDefaultAsync(b => b.ProductID == productId);

            if (latest == null)
            {
                if (bomHeader != null && bomHeader.IsActive == true)
                {
                    bomHeader.IsActive = false;
                    await db.Database.ExecuteSqlCommandAsync("UPDATE BOMS_AND_NORMS SET IsActive = 0 WHERE BOMID = {0}", bomHeader.ID);
                    return "Mahsulotda norma qolmadi — BOM faolsizlantirildi.";
                }
                return null;
            }

            var latestPeriod = latest.EffectiveFrom?.ToString("yyyy-MM");
            List<BomSourceLine> sourceLines;
            string versionNo;
            if (current != null && latest.ID == current.ID)
            {
                sourceLines = currentLines ?? new List<BomSourceLine>();
                versionNo = current.EffectiveFrom?.ToString("yyyy-MM") ?? latestPeriod;
            }
            else
            {
                if (bomHeader != null && bomHeader.IsActive == true && bomHeader.VersionNo == latestPeriod)
                {
                    return current != null
                        ? $"BOM o'zgarmadi: u eng so'nggi norma ({latestPeriod}) bo'yicha qoladi."
                        : null;
                }

                var latestLines = await db.NORM_INPUT_LINES.AsNoTracking()
                    .Where(il => il.ProductID == productId && il.Month == latestPeriod)
                    .ToListAsync();
                var latestMaterials = await LoadMaterialInfoAsync(latestLines.Select(il => il.MaterialID));
                sourceLines = latestLines.Select(il =>
                {
                    latestMaterials.TryGetValue(il.MaterialID, out var m);
                    var requiresCutting = m?.RequiresCutting ?? false;
                    return new BomSourceLine
                    {
                        MaterialID = il.MaterialID,
                        CutLengthMm = il.CutLengthMm,
                        Quantity = il.PiecesPerProduct,
                        ConsumptionMeters = NormLineMath.LineConsumption(requiresCutting, il.CutLengthMm, il.PiecesPerProduct, il.Consumption),
                        WasteMeters = NormLineMath.LineWaste(requiresCutting, m?.StandardLengthMm ?? 0, il.CutLengthMm, il.PiecesPerProduct, il.WasteAmount),
                        UnitID = il.UnitID
                    };
                }).ToList();
                versionNo = latestPeriod;
            }

            if (bomHeader == null)
            {
                bomHeader = new BOM_HEADERS
                {
                    ProductID = productId,
                    VersionNo = versionNo,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.BOM_HEADERS.Add(bomHeader);
                await db.SaveChangesAsync();
            }
            else
            {
                bomHeader.VersionNo = versionNo;
                bomHeader.IsActive = true;
            }

            await ReplaceBomLinesAsync(bomHeader.ID, sourceLines);

            // Ishlab chiqarish: ProductionConsumptionService NORMLINES ni BOMS_AND_NORMS orqali bog'langan norm bilan ishlatadi.
            await TryUpsertBomNormLinkAsync(bomHeader.ID, latest.ID, User.Identity.Name, productPNo);

            return current != null && latest.ID != current.ID
                ? $"BOM eng so'nggi norma ({latestPeriod}) bo'yicha yangilandi."
                : null;
        }

        /// <summary>BOM qatorlarini manba qatorlardan qayta yozadi (material bo'yicha guruhlab).</summary>
        private async Task ReplaceBomLinesAsync(int bomId, List<BomSourceLine> sourceLines)
        {
            var existingBomLines = await db.BOM_LINES.Where(b => b.BOMID == bomId).ToListAsync();
            db.BOM_LINES.RemoveRange(existingBomLines);

            var materials = await LoadMaterialInfoAsync(sourceLines.Select(l => l.MaterialID));
            foreach (var group in sourceLines.Where(l => l.MaterialID > 0).GroupBy(l => l.MaterialID))
            {
                if (!materials.TryGetValue(group.Key, out var material))
                    continue;

                var lines = group.ToList();

                // UI dan tanlangan birlik (birinchi qator), bo'lmasa mahsulot kartasi; UNITS FK — 0 yoki noto'g'ri ID ishlatilmaydi
                var firstLineUnit = lines.Select(l => l.UnitID).FirstOrDefault(u => u.HasValue && u.Value > 0);
                int? bomUnitId = NormalizeUnitFk(firstLineUnit)
                    ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : (int?)null);

                decimal bomQty;
                if (material.RequiresCutting)
                {
                    // Kesiladigan material: real ishlatiladigan taxta soni = ceil(Σ(kesim × dona + chiqindi) / standart uzunlik).
                    // Bir xil material bir nechta qatorda kelsa, hammasi sum qilinadi (offcut ham hisobga olinadi).
                    bomQty = NormLineMath.BarQuantity(
                        lines.Select(l => new CutPiece(l.CutLengthMm, l.Quantity, l.WasteMeters * 1000m)),
                        material.StandardLengthMm);
                }
                else
                {
                    // Kesilmaydigan material: sarf + chiqindi as-is.
                    bomQty = lines.Sum(l => l.ConsumptionMeters + l.WasteMeters);
                }

                db.BOM_LINES.Add(new BOM_LINES
                {
                    BOMID = bomId,
                    ComponentID = group.Key,
                    Qty = bomQty,
                    UnitID = bomUnitId
                });
            }
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

        private async Task<List<SelectListItem>> GetPreviousNormOptionsAsync(int productId, int? excludeNormVersionId)
        {
            var versions = await db.NORMVERSIONS
                .Where(n => n.ProductID == productId
                            && n.IsDeleted != true
                            && (!excludeNormVersionId.HasValue || n.ID != excludeNormVersionId.Value))
                .OrderByDescending(n => n.EffectiveFrom)
                .ThenByDescending(n => n.ID)
                .Select(n => new
                {
                    n.ID,
                    n.EffectiveFrom,
                    n.Comment
                })
                .ToListAsync();

            // Aynan shu mahsulot uchun NORM oylarini NORM_INPUT_LINES dan olamiz.
            var productMonths = await db.NORM_INPUT_LINES
                .Where(x => x.ProductID == productId && x.Month != null && x.Month != "")
                .Select(x => x.Month)
                .Distinct()
                .ToListAsync();

            return versions
                .Select(n => new SelectListItem
                {
                    Value = n.ID.ToString(),
                    Text = BuildNormMonthDisplayText(n.EffectiveFrom, n.Comment, productMonths)
                })
                .ToList();
        }

        private static string BuildNormMonthDisplayText(DateTime? effectiveFrom, string comment, List<string> productMonths)
        {
            var month = effectiveFrom.HasValue ? effectiveFrom.Value.ToString("yyyy-MM") : null;
            if (string.IsNullOrWhiteSpace(month) && productMonths != null && productMonths.Count > 0)
            {
                month = productMonths.OrderByDescending(x => x).FirstOrDefault();
            }

            var text = string.IsNullOrWhiteSpace(month) ? "Oy yo'q" : month;
            if (!string.IsNullOrWhiteSpace(comment))
            {
                text += " | " + comment;
            }
            return text;
        }

        private async Task<bool> ApplyTemplateFromNormVersionAsync(ProductNormViewModel viewModel, int templateNormVersionId, bool overrideHeaderFields)
        {
            var sourceNormVersion = await db.NORMVERSIONS
                .Include(n => n.PRODUCT)
                .FirstOrDefaultAsync(n => n.ID == templateNormVersionId && n.IsDeleted != true);

            if (sourceNormVersion == null || sourceNormVersion.ProductID != viewModel.ProductID)
            {
                return false;
            }

            if (overrideHeaderFields)
            {
                viewModel.Period = sourceNormVersion.EffectiveFrom?.ToString("yyyy-MM") ?? viewModel.Period;
                viewModel.EffectiveFrom = sourceNormVersion.EffectiveFrom;
                viewModel.EffectiveTo = sourceNormVersion.EffectiveTo;
                viewModel.Comment = sourceNormVersion.Comment;
            }

            var sourcePeriod = sourceNormVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
            await PopulateCuttingLinesFromPeriodAsync(viewModel, viewModel.ProductID, sourcePeriod);
            await RecalculateTotalsForViewModelAsync(viewModel);
            return true;
        }

        private async Task PopulateCuttingLinesFromPeriodAsync(ProductNormViewModel viewModel, int productId, string period)
        {
            viewModel.CuttingLines = new List<NormLineViewModel>();
            if (string.IsNullOrWhiteSpace(period))
            {
                return;
            }

            var inputLines = await db.NORM_INPUT_LINES.AsNoTracking()
                .Include("PRODUCT.PRODUCT_CATEGORIES")
                .Where(il => il.ProductID == productId && il.Month == period)
                .OrderByDescending(il => il.PRODUCT.Name)
                .ThenBy(il => il.MaterialID)
                .ThenBy(il => il.CutLengthMm)
                .ToListAsync();

            foreach (var inputLine in inputLines)
            {
                // MaterialID — PRODUCTS (xomashyo); EDMX: PRODUCT (MaterialID). PRODUCT1 — ProductID (tayyor mahsulot).
                var material = inputLine.PRODUCT;
                if (material == null)
                {
                    continue;
                }

                var unitPrice = inputLine.Price;
                var requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                var standardLengthMm = NormLineMath.EffectiveStandardLengthMm(material);

                // Saqlangan Consumption/WasteAmount, bo'lmasa kesim bo'yicha hisob (NormLineMath)
                var consumption = NormLineMath.LineConsumption(requiresCutting, inputLine.CutLengthMm, inputLine.PiecesPerProduct, inputLine.Consumption);
                var waste = NormLineMath.LineWaste(requiresCutting, standardLengthMm, inputLine.CutLengthMm, inputLine.PiecesPerProduct, inputLine.WasteAmount);

                var totalAmount = consumption * unitPrice + waste * unitPrice;

                viewModel.CuttingLines.Add(new NormLineViewModel
                {
                    ID = inputLine.ID,
                    MaterialID = inputLine.MaterialID,
                    MaterialCode = material.PNo ?? "",
                    MaterialName = material.Name ?? "",
                    StandardLengthMm = standardLengthMm > 0 ? (double?)standardLengthMm : null,
                    CalculatedLengthMm = inputLine.CutLengthMm,
                    Quantity = inputLine.PiecesPerProduct,
                    PiecesPerBar = 1, // Always 1 (UI dan olib tashlangan, PiecesPerProduct Quantity sifatida saqlanadi)
                    ConsumptionPerProduct = consumption,
                    WastePerProduct = waste,
                    IsWasteManual = inputLine.WasteAmount.HasValue,
                    UnitPrice = unitPrice > 0 ? (decimal?)unitPrice : null,
                    TotalCostPerProduct = totalAmount > 0 ? totalAmount : (consumption * unitPrice),
                    Norma = consumption,
                    Chiqindi = waste,
                    IsSelected = true,
                    CuttingMethod = inputLine.Comment,
                    UnitID = NormalizeUnitFk(inputLine.UnitID) ?? NormalizeUnitFk(material.UnitID > 0 ? material.UnitID : (int?)null)
                });
            }
        }

        private async Task RecalculateTotalsForViewModelAsync(ProductNormViewModel viewModel)
        {
            var woodByMaterial = await GetWoodFlagsAsync((viewModel.CuttingLines ?? new List<NormLineViewModel>()).Select(l => l.MaterialID));
            ApplyTotals(viewModel, woodByMaterial);
        }

        private Task SetUnitsViewBagAsync()
        {
            // Birliklar keshdan (ReferenceDataCache: Name = UnitName, Extra = ShortName)
            var units = tahsinERP.Services.Common.ReferenceDataCache.Get(tahsinERP.Services.Common.ReferenceDataCache.Units)
                .Select(u => new { u.ID, Text = !string.IsNullOrEmpty(u.Extra) ? u.Extra : (u.Name ?? u.ID.ToString()) })
                .OrderBy(u => u.Text)
                .ToList();
            ViewBag.UnitsList = units.Select(u => new SelectListItem { Value = u.ID.ToString(), Text = u.Text }).ToList();
            ViewBag.UnitsJson = units.Select(u => new { id = u.ID, text = u.Text }).ToList();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Bitta qator uchun bazaviy sarf (m) va chiqindini (m) hisoblaydi.
        /// Kesiladigan material: sarf = (kesim/1000) * dona; chiqindi = ((standart - kesim)/1000) * dona.
        /// Kesilmaydigan material: sarf = dona, chiqindi = 0.
        /// </summary>
        private static void ApplyBaseConsumptionWaste(NormLineViewModel line, bool requiresCutting, double standardLengthMm)
        {
            if (requiresCutting && line.CalculatedLengthMm.HasValue && line.CalculatedLengthMm.Value > 0)
            {
                line.ConsumptionPerProduct = (decimal)((line.CalculatedLengthMm.Value / 1000.0) * line.Quantity);

                if (standardLengthMm > 0)
                {
                    var perPieceWasteMm = Math.Max(0, standardLengthMm - line.CalculatedLengthMm.Value);
                    line.WastePerProduct = (decimal)((perPieceWasteMm / 1000.0) * line.Quantity);
                }
                else
                {
                    line.WastePerProduct = 0;
                }
            }
            else
            {
                line.ConsumptionPerProduct = line.Quantity;
                line.WastePerProduct = 0;
            }

            var price = line.UnitPrice ?? 0;
            line.TotalCostPerProduct = (line.ConsumptionPerProduct + line.WastePerProduct) * price;
            line.Norma = line.ConsumptionPerProduct;
            line.Chiqindi = line.WastePerProduct;
        }

        /// <summary>
        /// Chiqindidan qayta foydalanish optimizatsiyasi (bitta xomashyo bo'yicha).
        ///
        /// Algoritm:
        ///  1. Xomashyo kesiladigan bo'lmasa (RequiresCutting == false) yoki standart uzunlik bo'lmasa — qiymatlar o'zgarmaydi.
        ///  2. Kesim qatorlari uzunligi bo'yicha kamayish tartibida saralanadi: uzun kesim — chiqindi MANBAI,
        ///     kalta kesim — chiqindidan OLUVCHI.
        ///  3. Har bir manba qatorning bitta donadan chiqadigan tilimi = (standart - kesim). Jami chiqindi = tilim * dona.
        ///  4. Kalta kesim (target) chiqindidan to'liq olinadi, agar:
        ///        - manba tilimi >= target kesim uzunligi (har bir tilimga sig'adi), VA
        ///        - manbaning qolgan jami chiqindisi >= target ning jami kerakli uzunligi (kesim * dona).
        ///     Bunday qatorda dona va sarf 0 ga tushiriladi, izohga qaysi qatordan olingani yoziladi.
        /// </summary>
        private static List<NormLineViewModel> OptimizeMaterialCutting(List<NormLineViewModel> lines, PRODUCT material)
        {
            bool requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
            double standardLengthMm = NormLineMath.EffectiveStandardLengthMm(material);

            // Har bir qatorga material ma'lumotlarini va bazaviy sarf/chiqindini qo'yamiz.
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line.MaterialCode))
                    line.MaterialCode = material?.PNo ?? "";
                if (string.IsNullOrEmpty(line.MaterialName))
                    line.MaterialName = material?.Name ?? "";
                if (standardLengthMm > 0)
                    line.StandardLengthMm = standardLengthMm;

                line.UsesWaste = false;
                line.OptimizationNote = "";
                ApplyBaseConsumptionWaste(line, requiresCutting, standardLengthMm);
            }

            // Kesilmaydigan material yoki standart uzunlik yo'q — optimizatsiya qo'llanilmaydi.
            if (!requiresCutting || standardLengthMm <= 0)
            {
                foreach (var line in lines)
                {
                    line.OptimizationNote = !requiresCutting
                        ? "Kesilmaydigan material — optimizatsiya qo'llanilmadi."
                        : "Standart uzunlik yo'q — optimizatsiya qo'llanilmadi.";
                }
                return lines;
            }

            // Uzunligi bo'lgan kesim qatorlari, kamayish tartibida (uzun — manba, kalta — oluvchi).
            var cuttingLines = lines
                .Where(l => l.CalculatedLengthMm.HasValue && l.CalculatedLengthMm.Value > 0 && l.Quantity > 0)
                .OrderByDescending(l => l.CalculatedLengthMm.Value)
                .ToList();

            // 1-based pozitsiya (izohlarda "N-qator" deb ko'rsatish uchun).
            var positionMap = new Dictionary<NormLineViewModel, int>();
            for (int i = 0; i < cuttingLines.Count; i++)
                positionMap[cuttingLines[i]] = i + 1;

            // Manba qatorlarining hozircha mavjud (qolgan) chiqindisi, mm.
            var availableWasteMm = new Dictionary<NormLineViewModel, double>();
            foreach (var src in cuttingLines)
            {
                var perPieceWasteMm = Math.Max(0, standardLengthMm - src.CalculatedLengthMm.Value);
                availableWasteMm[src] = perPieceWasteMm * src.Quantity;
            }

            // Greedy: uzun kesimdan boshlab, undan kalta kesimlarni chiqindiga joylashtiramiz.
            for (int i = 0; i < cuttingLines.Count; i++)
            {
                var src = cuttingLines[i];

                // O'zi chiqindidan olingan qator yangi chiqindi hosil qilmaydi — manba bo'la olmaydi.
                if (src.UsesWaste)
                    continue;

                var srcPerPieceWasteMm = Math.Max(0, standardLengthMm - src.CalculatedLengthMm.Value);
                if (srcPerPieceWasteMm <= 0)
                    continue;

                for (int j = i + 1; j < cuttingLines.Count; j++)
                {
                    var tgt = cuttingLines[j];
                    if (tgt.UsesWaste)
                        continue;
                    if (!tgt.CalculatedLengthMm.HasValue || tgt.CalculatedLengthMm.Value <= 0 || tgt.Quantity <= 0)
                        continue;

                    var tgtCut = tgt.CalculatedLengthMm.Value;
                    var tgtTotalNeedMm = tgtCut * tgt.Quantity;

                    // Shart: har bir tilim kalta kesimni sig'dirsin VA jami chiqindi yetarli bo'lsin.
                    if (srcPerPieceWasteMm >= tgtCut && availableWasteMm[src] >= tgtTotalNeedMm)
                    {
                        availableWasteMm[src] -= tgtTotalNeedMm;

                        var pos = positionMap[src];
                        var origQty = tgt.Quantity;

                        tgt.UsesWaste = true;
                        tgt.OptimizationNote =
                            $"{tgtCut:F0}mm × {origQty} dona {pos}-qatordagi {src.CalculatedLengthMm.Value:F0}mm kesim chiqindisidan olinadi. Sarf 0 qilindi.";

                        // Foydalanuvchi talabi: dona va sarf 0 qilinadi (xomashyo qo'shimcha sotib olinmaydi).
                        tgt.Quantity = 0;
                        tgt.ConsumptionPerProduct = 0;
                        tgt.WastePerProduct = 0;
                        tgt.IsWasteManual = true;
                        tgt.TotalCostPerProduct = 0;
                        tgt.Norma = 0;
                        tgt.Chiqindi = 0;
                    }
                }
            }

            // Manba qatorlarining qolgan chiqindisini yangilaymiz va izoh qo'yamiz.
            foreach (var src in cuttingLines)
            {
                if (src.UsesWaste)
                    continue;

                var remainingWasteMm = availableWasteMm[src];
                src.WastePerProduct = (decimal)(remainingWasteMm / 1000.0);
                src.Chiqindi = src.WastePerProduct;

                var price = src.UnitPrice ?? 0;
                src.TotalCostPerProduct = (src.ConsumptionPerProduct + src.WastePerProduct) * price;

                if (string.IsNullOrEmpty(src.OptimizationNote))
                {
                    var perPieceWasteMm = Math.Max(0, standardLengthMm - src.CalculatedLengthMm.Value);
                    var totalWasteMm = perPieceWasteMm * src.Quantity;
                    var usedMm = totalWasteMm - remainingWasteMm;
                    src.OptimizationNote = usedMm > 0
                        ? $"Chiqindidan {usedMm:F0}mm boshqa kalta kesimlarga ishlatildi. Qolgan chiqindi {remainingWasteMm:F0}mm."
                        : $"Chiqindi {remainingWasteMm:F0}mm (boshqa kesimga ishlatilmadi).";
                }
            }

            return lines;
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

