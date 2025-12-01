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

namespace tahsinERP.Controllers.Engineering
{
    public class NormCuttingController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();

        // GET: NormCutting
        public async Task<ActionResult> Index(int? productId, string period, string version)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var query = db.NORMVERSIONS
                .Include(n => n.PRODUCT)
                .Include(n => n.NORMLINES)
                .Where(n => n.PRODUCT.IsDeleted == false);

            if (productId.HasValue)
            {
                query = query.Where(n => n.ProductID == productId.Value);
            }

            if (!string.IsNullOrEmpty(period))
            {
                query = query.Where(n => n.EffectiveFrom.HasValue && 
                    n.EffectiveFrom.Value.ToString("yyyy-MM") == period);
            }

            var norms = await query.OrderByDescending(n => n.EffectiveFrom).ToListAsync();

            var viewModel = norms.Select(n => new
            {
                NormVersionID = n.ID,
                ProductID = n.ProductID,
                ProductCode = n.PRODUCT.PNo,
                ProductName = n.PRODUCT.Name,
                Period = n.EffectiveFrom.HasValue ? n.EffectiveFrom.Value.ToString("yyyy-MM") : "",
                EffectiveFrom = n.EffectiveFrom,
                EffectiveTo = n.EffectiveTo,
                Comment = n.Comment,
                LineCount = n.NORMLINES.Count
            }).ToList();

            ViewBag.Products = new SelectList(
                await db.PRODUCTS
                    .Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .ToListAsync(),
                "ID", "PNo", productId);

            return View(viewModel);
        }

        // GET: NormCutting/Edit
        public async Task<ActionResult> Edit(int? productId, string period, int? normVersionId)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var viewModel = new ProductNormCuttingViewModel();

            if (normVersionId.HasValue)
            {
                // Load existing norm version
                var normVersion = await db.NORMVERSIONS
                    .Include(n => n.PRODUCT)
                    .Include(n => n.NORMLINES)
                    .FirstOrDefaultAsync(n => n.ID == normVersionId.Value);

                if (normVersion == null)
                {
                    TempData["Error"] = "Norm versiyasi topilmadi.";
                    return RedirectToAction("Index");
                }

                viewModel.ProductID = normVersion.ProductID ?? 0;
                viewModel.ProductCode = normVersion.PRODUCT?.PNo ?? "";
                viewModel.ProductName = normVersion.PRODUCT?.Name ?? "";
                viewModel.Period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
                viewModel.NormVersionID = normVersion.ID;
                viewModel.EffectiveFrom = normVersion.EffectiveFrom;
                viewModel.EffectiveTo = normVersion.EffectiveTo;
                viewModel.Comment = normVersion.Comment;

                // Load existing norm lines
                var normLines = normVersion.NORMLINES.ToList();
                foreach (var line in normLines)
                {
                    var material = await db.PRODUCTS.FindAsync(line.MaterialID);
                    viewModel.CuttingLines.Add(new NormCuttingLineViewModel
                    {
                        ID = line.ID,
                        MaterialID = line.MaterialID ?? 0,
                        MaterialCode = material?.PNo ?? "",
                        MaterialName = material?.Name ?? "",
                        ConsumptionPerProduct = line.QtyPerUnit ?? 0,
                        WastePerProduct = line.WastePercent ?? 0,
                        Norma = line.QtyPerUnit ?? 0,
                        Chiqindi = line.WastePercent ?? 0,
                        Quantity = 1
                    });
                }
            }
            else if (productId.HasValue)
            {
                // New norm for product
                var product = await db.PRODUCTS.FindAsync(productId.Value);
                if (product == null)
                {
                    TempData["Error"] = "Mahsulot topilmadi.";
                    return RedirectToAction("Index");
                }

                viewModel.ProductID = product.ID;
                viewModel.ProductCode = product.PNo;
                viewModel.ProductName = product.Name;
                viewModel.Period = period ?? DateTime.Now.ToString("yyyy-MM");
            }
            else
            {
                TempData["Error"] = "Mahsulot ID kiritilmagan.";
                return RedirectToAction("Index");
            }

            // Load materials (raw materials)
            ViewBag.Materials = new SelectList(
                await db.PRODUCTS
                    .Where(p => p.IsRawMaterial == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .ToListAsync(),
                "ID", "PNo");

            return View(viewModel);
        }

        // POST: NormCutting/Edit (Save temporary lines)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ProductNormCuttingViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                // Recalculate fields
                RecalculateLineFields(viewModel.CuttingLines);

                // Save to session or temp storage (for now, just return to view)
                ViewBag.Materials = new SelectList(
                    await db.PRODUCTS
                        .Where(p => p.IsRawMaterial == true && p.IsDeleted == false)
                        .OrderBy(p => p.PNo)
                        .ToListAsync(),
                    "ID", "PNo");

                return View(viewModel);
            }

            ViewBag.Materials = new SelectList(
                await db.PRODUCTS
                    .Where(p => p.IsRawMaterial == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .ToListAsync(),
                "ID", "PNo");

            return View(viewModel);
        }

        // POST: NormCutting/Calculate (Optimization)
        [HttpPost]
        public async Task<JsonResult> Calculate(ProductNormCuttingViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" });
            }

            try
            {
                // Recalculate all lines
                RecalculateLineFields(viewModel.CuttingLines);

                // Group by MaterialID
                var materialGroups = viewModel.CuttingLines
                    .Where(l => l.IsSelected && l.MaterialID > 0)
                    .GroupBy(l => l.MaterialID)
                    .ToList();

                var optimization = new OptimizationSuggestionViewModel
                {
                    SuggestedMaterials = new Dictionary<int, MaterialSummaryViewModel>()
                };

                decimal totalNorm = 0;
                decimal totalWaste = 0;
                decimal totalCost = 0;

                foreach (var group in materialGroups)
                {
                    var materialId = group.Key;
                    var lines = group.ToList();
                    var material = await db.PRODUCTS.FindAsync(materialId);

                    // Simple optimization: select lines with minimal waste
                    var optimizedLines = OptimizeMaterialCutting(lines, material);

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
                }

                optimization.SuggestedTotalNorm = totalNorm;
                optimization.SuggestedTotalWaste = totalWaste;
                optimization.SuggestedTotalCost = totalCost;
                optimization.Message = $"Optimizatsiya natijasi: Umumiy norma {totalNorm:F4} m, Umumiy chiqindi {totalWaste:F4} m, Umumiy narx {totalCost:F2}";

                return Json(new
                {
                    success = true,
                    optimization = optimization,
                    totalNorm = totalNorm,
                    totalWaste = totalWaste,
                    totalCost = totalCost
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
            }
        }

        // POST: NormCutting/Confirm (Save to BOM/NORM tables)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Confirm(ProductNormCuttingViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // Recalculate fields
                    RecalculateLineFields(viewModel.CuttingLines);

                    // Get or create NORMVERSION
                    NORMVERSION normVersion;
                    if (viewModel.NormVersionID.HasValue)
                    {
                        normVersion = await db.NORMVERSIONS
                            .Include(n => n.NORMLINES)
                            .FirstOrDefaultAsync(n => n.ID == viewModel.NormVersionID.Value);
                        
                        if (normVersion == null)
                        {
                            TempData["Error"] = "Norm versiyasi topilmadi.";
                            return RedirectToAction("Index");
                        }

                        // Update existing
                        normVersion.EffectiveFrom = ParsePeriod(viewModel.Period);
                        normVersion.EffectiveTo = viewModel.EffectiveTo;
                        normVersion.Comment = viewModel.Comment;

                        // Delete existing lines
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES);
                    }
                    else
                    {
                        // Create new
                        normVersion = new NORMVERSION
                        {
                            ProductID = viewModel.ProductID,
                            EffectiveFrom = ParsePeriod(viewModel.Period),
                            EffectiveTo = viewModel.EffectiveTo,
                            Comment = viewModel.Comment
                        };
                        db.NORMVERSIONS.Add(normVersion);
                    }

                    await db.SaveChangesAsync();

                    // Group by MaterialID and aggregate
                    var materialGroups = viewModel.CuttingLines
                        .Where(l => l.IsSelected && l.MaterialID > 0)
                        .GroupBy(l => l.MaterialID)
                        .ToList();

                    // Create aggregated NORMLINE for each material
                    foreach (var group in materialGroups)
                    {
                        var materialId = group.Key;
                        var lines = group.ToList();

                        var aggregatedLine = new NORMLINE
                        {
                            NormID = normVersion.ID,
                            MaterialID = materialId,
                            QtyPerUnit = lines.Sum(l => l.ConsumptionPerProduct),
                            WastePercent = lines.Sum(l => l.WastePerProduct)
                        };

                        db.NORMLINES.Add(aggregatedLine);
                    }

                    await db.SaveChangesAsync();

                    // Save detailed cutting variants to NORM_INPUT_LINES
                    foreach (var line in viewModel.CuttingLines.Where(l => l.IsSelected && l.MaterialID > 0))
                    {
                        var inputLine = new NORM_INPUT_LINES
                        {
                            ProductID = viewModel.ProductID,
                            MaterialID = line.MaterialID,
                            CutLengthMm = (int)(line.CalculatedLengthMm ?? 0),
                            PiecesPerProduct = line.PiecesPerBar,
                            Month = viewModel.Period,
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now
                        };

                        db.NORM_INPUT_LINES.Add(inputLine);
                    }

                    await db.SaveChangesAsync();

                    // Create/Update BOM_HEADERS and BOM_LINES
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
                    }

                    // Delete existing BOM_LINES
                    var existingBomLines = await db.BOM_LINES
                        .Where(b => b.BOMID == bomHeader.ID)
                        .ToListAsync();
                    db.BOM_LINES.RemoveRange(existingBomLines);

                    // Create new BOM_LINES from aggregated norms
                    foreach (var group in materialGroups)
                    {
                        var materialId = group.Key;
                        var lines = group.ToList();
                        var material = await db.PRODUCTS.FindAsync(materialId);

                        var bomLine = new BOM_LINES
                        {
                            BOMID = bomHeader.ID,
                            ComponentID = materialId,
                            Qty = lines.Sum(l => l.ConsumptionPerProduct),
                            UnitID = material?.UnitID ?? 1
                        };

                        db.BOM_LINES.Add(bomLine);
                    }

                    await db.SaveChangesAsync();

                    // Log
                    LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                        $"Mahsulot {viewModel.ProductCode} uchun NORM saqlandi. {materialGroups.Count} ta material, {viewModel.CuttingLines.Count(l => l.IsSelected)} ta kesish variantlari.");

                    transaction.Complete();

                    TempData["Success"] = "NORM muvaffaqiyatli saqlandi.";
                    return RedirectToAction("Index", new { productId = viewModel.ProductID });
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                    return RedirectToAction("Edit", new { productId = viewModel.ProductID, period = viewModel.Period });
                }
            }
        }

        #region Helper Methods

        private void RecalculateLineFields(List<NormCuttingLineViewModel> lines)
        {
            foreach (var line in lines)
            {
                if (line.MaterialID > 0 && line.PiecesPerBar > 0 && line.Quantity > 0)
                {
                    // Calculate consumption per product
                    line.ConsumptionPerProduct = (decimal)((line.CalculatedLengthMm ?? 0) / 1000.0) * line.Quantity;
                    
                    // Calculate waste (if standard length is available)
                    if (line.StandardLengthMm.HasValue && line.StandardLengthMm.Value > 0)
                    {
                        var usedLength = line.CalculatedLengthMm ?? 0;
                        var standardLength = line.StandardLengthMm.Value;
                        var wasteLength = Math.Max(0, standardLength - usedLength);
                        line.WastePerProduct = (decimal)(wasteLength / 1000.0) * line.Quantity;
                    }

                    // Calculate total cost
                    if (line.UnitPrice.HasValue)
                    {
                        line.TotalCostPerProduct = line.ConsumptionPerProduct * line.UnitPrice.Value;
                    }

                    // Norma and Chiqindi
                    line.Norma = line.ConsumptionPerProduct;
                    line.Chiqindi = line.WastePerProduct;
                }
            }
        }

        private List<NormCuttingLineViewModel> OptimizeMaterialCutting(List<NormCuttingLineViewModel> lines, PRODUCT material)
        {
            // Simple heuristic: prefer lines with less waste
            // In a real scenario, this would be more complex (linear programming, etc.)
            return lines
                .OrderBy(l => l.WastePerProduct)
                .ThenByDescending(l => l.PiecesPerBar)
                .ToList();
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

