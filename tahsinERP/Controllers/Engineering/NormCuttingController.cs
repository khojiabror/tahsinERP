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
    public class NormCuttingController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();

        // GET: NormCutting
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Product lar ro'yxatini ko'rsatish (IsFinishedGood == true)
            var productsList = await db.PRODUCTS
                .Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .ToListAsync();

            // Extract product IDs as primitive list
            var productIds = productsList.Select(p => p.ID).ToList();

            // Count norms for each product
            var normCounts = await db.NORMVERSIONS
                .Where(n => n.ProductID.HasValue && productIds.Contains(n.ProductID.Value))
                .GroupBy(n => n.ProductID.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var products = productsList.Select(p => new ProductNormCuttingIndexViewModel
            {
                ProductID = p.ID,
                ProductCode = p.PNo,
                ProductName = p.Name,
                NormCount = normCounts.ContainsKey(p.ID) ? normCounts[p.ID] : 0
            }).ToList();

            return View(products);
        }

        // GET: NormCutting/Details/{productId}
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
                .Where(n => n.ProductID == id.Value)
                .OrderByDescending(n => n.EffectiveFrom)
                .ToListAsync();

            var viewModel = new ProductNormCuttingDetailsViewModel
            {
                ProductID = product.ID,
                ProductCode = product.PNo,
                ProductName = product.Name,
                NormVersions = normVersions.Select(n => new NormVersionItemViewModel
                {
                    NormVersionID = n.ID,
                    Period = n.EffectiveFrom.HasValue ? n.EffectiveFrom.Value.ToString("yyyy-MM") : "",
                    EffectiveFrom = n.EffectiveFrom,
                    EffectiveTo = n.EffectiveTo,
                    Comment = n.Comment,
                    LineCount = n.NORMLINES.Count
                }).ToList()
            };

            ViewBag.ProductID = id.Value;
            ViewBag.ProductCode = product.PNo;
            ViewBag.ProductName = product.Name;

            return View(viewModel);
        }

        // GET: NormCutting/ViewDetails/{normVersionId}
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
                .FirstOrDefaultAsync(n => n.ID == normVersionId.Value);

            if (normVersion == null)
            {
                TempData["Error"] = "Norm versiyasi topilmadi.";
                return RedirectToAction("Index");
            }

            var viewModel = new ProductNormCuttingViewModel
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

            // Load NORM_INPUT_LINES (detailed cutting variants) instead of aggregated NORMLINES
            var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
            var inputLines = await db.NORM_INPUT_LINES
                .Where(il => il.ProductID == viewModel.ProductID && il.Month == period)
                .OrderBy(il => il.MaterialID)
                .ThenBy(il => il.CutLengthMm)
                .ToListAsync();

            foreach (var inputLine in inputLines)
            {
                var material = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .FirstOrDefaultAsync(p => p.ID == inputLine.MaterialID);

                if (material != null)
                {
                    // Get latest price
                    var latestIncome = await db.INCOME_PRODUCTS
                        .Include(ip => ip.INCOME)
                        .Where(ip => ip.ProductID == inputLine.MaterialID && ip.PiecePrice.HasValue)
                        .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                        .Select(ip => new { Price = ip.PiecePrice.Value })
                        .FirstOrDefaultAsync();

                    // Use Consumption and WasteAmount from NORM_INPUT_LINES if available, otherwise calculate
                    bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                    var standardLengthMm = material.StandardLengthMm ?? material.Length ?? 0;
                    
                    decimal consumption = 0;
                    decimal waste = 0;
                    
                    // First, try to use saved values from NORM_INPUT_LINES
                    if (inputLine.Consumption.HasValue && inputLine.Consumption.Value > 0)
                    {
                        // Consumption is stored in mm, convert to m
                        consumption = (decimal)(inputLine.Consumption.Value / 1000.0);
                    }
                    else if (requiresCutting && inputLine.CutLengthMm > 0)
                    {
                        // For cutting materials: convert mm to m
                        consumption = (decimal)(inputLine.CutLengthMm / 1000.0) * inputLine.PiecesPerProduct;
                    }
                    else
                    {
                        // For non-cutting materials: use pieces as-is
                        consumption = inputLine.PiecesPerProduct;
                    }
                    
                    // Use WasteAmount from NORM_INPUT_LINES if available
                    if (inputLine.WasteAmount.HasValue && inputLine.WasteAmount.Value > 0)
                    {
                        // WasteAmount is stored in mm, convert to m
                        waste = (decimal)(inputLine.WasteAmount.Value / 1000.0);
                    }
                    else if (requiresCutting && standardLengthMm > 0 && inputLine.CutLengthMm > 0)
                    {
                        // Calculate waste if not saved
                        var wastePerBar = Math.Max(0, standardLengthMm - inputLine.CutLengthMm);
                        var barsNeeded = inputLine.PiecesPerProduct; // 1 piece per bar
                        waste = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }

                    var unitPrice = latestIncome != null ? (decimal?)latestIncome.Price : null;
                    var totalCost = consumption * (unitPrice ?? 0);

                    viewModel.CuttingLines.Add(new NormCuttingLineViewModel
                    {
                        ID = inputLine.ID,
                        MaterialID = inputLine.MaterialID,
                        MaterialCode = material.PNo ?? "",
                        MaterialName = material.Name ?? "",
                        StandardLengthMm = standardLengthMm > 0 ? (double?)standardLengthMm : null,
                        CalculatedLengthMm = inputLine.CutLengthMm,
                        Quantity = inputLine.PiecesPerProduct,
                        PiecesPerBar = 1, // Always 1 (removed from UI, PiecesPerProduct is stored as Quantity)
                        ConsumptionPerProduct = consumption,
                        WastePerProduct = waste,
                        UnitPrice = unitPrice,
                        TotalCostPerProduct = totalCost,
                        Norma = consumption,
                        Chiqindi = waste,
                        IsSelected = true
                    });
                }
            }

            // Calculate totals
            viewModel.TotalNorm = viewModel.CuttingLines.Sum(l => l.ConsumptionPerProduct);
            viewModel.TotalWaste = viewModel.CuttingLines.Sum(l => l.WastePerProduct);
            viewModel.TotalCost = viewModel.CuttingLines.Sum(l => l.TotalCostPerProduct);

            ViewBag.IsReadOnly = true;
            return View("ViewDetails", viewModel);
        }

        // POST: NormCutting/Delete/{normVersionId}
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

                    // Delete NORM_INPUT_LINES (detailed cutting variants) for this period
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
                            LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                                $"Mahsulot {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}) dan {deletedInputLinesCount} ta NORM_INPUT_LINE o'chirildi.");
                        }
                    }

                    // Delete NORMLINES
                    int deletedNormLinesCount = 0;
                    if (normVersion.NORMLINES.Any())
                    {
                        deletedNormLinesCount = normVersion.NORMLINES.Count;
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES);
                        await db.SaveChangesAsync();
                        
                        // Log deletion of NORMLINES
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}) dan {deletedNormLinesCount} ta NORMLINE o'chirildi.");
                    }

                    // Delete NORMVERSION
                    db.NORMVERSIONS.Remove(normVersion);
                    await db.SaveChangesAsync();
                    
                    // Note: BOM_HEADERS va BOM_LINES product ga bog'liq, norm versiyasiga emas
                    // Shuning uchun ularni o'chirmaymiz, chunki keyingi norm versiyasi yaratilganda yangilanadi

                    // Log final deletion
                    LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                        $"Mahsulot {productCode} uchun NORM versiyasi (ID: {normVersionId.Value}, Period: {period}) o'chirildi.");

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

        // GET: NormCutting/Create
        public async Task<ActionResult> Create(int? productId, string period)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var viewModel = new ProductNormCuttingViewModel();

            if (productId.HasValue)
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

            // Load materials (raw materials, components, and other materials - selofan, karton, dona ketadigan maxsulotlar)
            var materials = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            // Create SelectList with "PNo - Name" format
            var materialsSelectList = materials.Select(m => new SelectListItem
            {
                Value = m.ID.ToString(),
                Text = $"{m.PNo} - {m.Name ?? m.PNo}"
            }).ToList();
            
            ViewBag.Materials = new SelectList(materialsSelectList, "Value", "Text", null);
            
            // Create simple list for JSON serialization (avoid circular reference) - include Name for display
            ViewBag.MaterialsJson = materials.Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo }).ToList();

            return View("Create", viewModel);
        }

        // GET: NormCutting/Edit
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

            // Load existing norm version
            var normVersion = await db.NORMVERSIONS
                .Include(n => n.PRODUCT)
                .FirstOrDefaultAsync(n => n.ID == normVersionId.Value);

            if (normVersion == null)
            {
                TempData["Error"] = "Norm versiyasi topilmadi.";
                return RedirectToAction("Index");
            }

            var viewModel = new ProductNormCuttingViewModel
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

            // Load NORM_INPUT_LINES (detailed cutting variants) instead of aggregated NORMLINES
            var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? "";
            var inputLines = await db.NORM_INPUT_LINES
                .Where(il => il.ProductID == viewModel.ProductID && il.Month == period)
                .OrderBy(il => il.MaterialID)
                .ThenBy(il => il.CutLengthMm)
                .ToListAsync();

            foreach (var inputLine in inputLines)
            {
                var material = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .FirstOrDefaultAsync(p => p.ID == inputLine.MaterialID);

                if (material != null)
                {
                    // Get latest price
                    var latestIncome = await db.INCOME_PRODUCTS
                        .Include(ip => ip.INCOME)
                        .Where(ip => ip.ProductID == inputLine.MaterialID && ip.PiecePrice.HasValue)
                        .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                        .Select(ip => new { Price = ip.PiecePrice.Value })
                        .FirstOrDefaultAsync();

                    // Use Consumption and WasteAmount from NORM_INPUT_LINES if available, otherwise calculate
                    bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
                    var standardLengthMm = material.StandardLengthMm ?? material.Length ?? 0;
                    
                    decimal consumption = 0;
                    decimal waste = 0;
                    
                    // First, try to use saved values from NORM_INPUT_LINES
                    if (inputLine.Consumption.HasValue && inputLine.Consumption.Value > 0)
                    {
                        // Consumption is stored in mm, convert to m
                        consumption = (decimal)(inputLine.Consumption.Value / 1000.0);
                    }
                    else if (requiresCutting && inputLine.CutLengthMm > 0)
                    {
                        // For cutting materials: convert mm to m
                        consumption = (decimal)(inputLine.CutLengthMm / 1000.0) * inputLine.PiecesPerProduct;
                    }
                    else
                    {
                        // For non-cutting materials: use pieces as-is
                        consumption = inputLine.PiecesPerProduct;
                    }
                    
                    // Use WasteAmount from NORM_INPUT_LINES if available
                    if (inputLine.WasteAmount.HasValue && inputLine.WasteAmount.Value > 0)
                    {
                        // WasteAmount is stored in mm, convert to m
                        waste = (decimal)(inputLine.WasteAmount.Value / 1000.0);
                    }
                    else if (requiresCutting && standardLengthMm > 0 && inputLine.CutLengthMm > 0)
                    {
                        // Calculate waste if not saved
                        var wastePerBar = Math.Max(0, standardLengthMm - inputLine.CutLengthMm);
                        var barsNeeded = inputLine.PiecesPerProduct; // 1 piece per bar
                        waste = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }

                    var unitPrice = latestIncome != null ? (decimal?)latestIncome.Price : null;
                    var totalCost = consumption * (unitPrice ?? 0);

                    viewModel.CuttingLines.Add(new NormCuttingLineViewModel
                    {
                        ID = inputLine.ID,
                        MaterialID = inputLine.MaterialID,
                        MaterialCode = material.PNo ?? "",
                        MaterialName = material.Name ?? "",
                        StandardLengthMm = standardLengthMm > 0 ? (double?)standardLengthMm : null,
                        CalculatedLengthMm = inputLine.CutLengthMm,
                        Quantity = inputLine.PiecesPerProduct,
                        PiecesPerBar = 1, // Always 1 (removed from UI, PiecesPerProduct is stored as Quantity)
                        ConsumptionPerProduct = consumption,
                        WastePerProduct = waste,
                        UnitPrice = unitPrice,
                        TotalCostPerProduct = totalCost,
                        Norma = consumption,
                        Chiqindi = waste,
                        IsSelected = true
                    });
                }
            }

            // Calculate totals
            viewModel.TotalNorm = viewModel.CuttingLines.Sum(l => l.ConsumptionPerProduct);
            viewModel.TotalWaste = viewModel.CuttingLines.Sum(l => l.WastePerProduct);
            viewModel.TotalCost = viewModel.CuttingLines.Sum(l => l.TotalCostPerProduct);

            // Load materials (raw materials, components, and other materials - selofan, karton, dona ketadigan maxsulotlar)
            var materials = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            // Create SelectList with "PNo - Name" format
            var materialsSelectList = materials.Select(m => new SelectListItem
            {
                Value = m.ID.ToString(),
                Text = $"{m.PNo} - {m.Name ?? m.PNo}"
            }).ToList();
            
            ViewBag.Materials = new SelectList(materialsSelectList, "Value", "Text", null);
            
            // Create simple list for JSON serialization (avoid circular reference) - include Name for display
            ViewBag.MaterialsJson = materials.Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo }).ToList();

            return View("Edit", viewModel);
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
                var materialsPost = await db.PRODUCTS
                    .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                    .ToListAsync();

                ViewBag.Materials = new SelectList(materialsPost, "ID", "PNo");
                ViewBag.MaterialsJson = materialsPost.Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo }).ToList();

                return View(viewModel);
            }

            var materialsError = await db.PRODUCTS
                .Where(p => (p.IsRawMaterial == true || p.IsComponent == true) && p.IsDeleted == false)
                .OrderBy(p => p.PNo)
                .Select(p => new { ID = p.ID, PNo = p.PNo, Name = p.Name })
                .ToListAsync();

            ViewBag.Materials = new SelectList(materialsError, "ID", "PNo");
            ViewBag.MaterialsJson = materialsError.Select(m => new { id = m.ID, text = m.PNo, name = m.Name ?? m.PNo }).ToList();

            return View(viewModel);
        }

        // GET: NormCutting/GetMaterialData/{materialId}
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

                // Get latest price from INCOME_PRODUCTS
                var latestIncome = await db.INCOME_PRODUCTS
                    .Include(ip => ip.INCOME)
                    .Where(ip => ip.ProductID == materialId && ip.PiecePrice.HasValue)
                    .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                    .Select(ip => new { Price = ip.PiecePrice.Value })
                    .FirstOrDefaultAsync();

                // Check if material requires cutting (from category)
                bool requiresCutting = false;
                if (material.PRODUCT_CATEGORIES != null)
                {
                    requiresCutting = material.PRODUCT_CATEGORIES.RequiresCutting;
                }

                // Use StandardLengthMm if available, otherwise use Length
                double standardLengthMm = 0;
                if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                {
                    standardLengthMm = material.StandardLengthMm.Value;
                }
                else if (material.Length.HasValue && material.Length.Value > 0)
                {
                    standardLengthMm = material.Length.Value;
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
                    unitPrice = latestIncome != null ? latestIncome.Price : (double?)null,
                    requiresCutting = requiresCutting,
                    categoryId = material.CategoryID
                };

                return Json(materialData, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: NormCutting/Calculate (Optimization)
        [HttpPost]
        public async Task<JsonResult> Calculate(ProductNormCuttingViewModel viewModel)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                // Always read from Request body (JSON) as client sends JSON
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
                    // Use Newtonsoft.Json for deserialization
                    viewModel = JsonConvert.DeserializeObject<ProductNormCuttingViewModel>(json);
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"JSON deserialization xatosi: {ex.Message}. JSON: {json.Substring(0, Math.Min(200, json.Length))}" }, JsonRequestBehavior.AllowGet);
                }
                
                // Check if viewModel is null
                if (viewModel == null)
                {
                    return Json(new { success = false, message = "Model ma'lumotlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Check if CuttingLines is null or empty
                if (viewModel.CuttingLines == null || viewModel.CuttingLines.Count == 0)
                {
                    return Json(new { success = false, message = "Kesish variantlari topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                // Recalculate all lines
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

                // Store all optimized lines for detailed display
                var allOptimizedLines = new List<object>();

                foreach (var group in materialGroups)
                {
                    var materialId = group.Key;
                    var lines = group.ToList();
                    var material = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefaultAsync(p => p.ID == materialId);

                    // Simple optimization: select lines with minimal waste
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

                        // Add detailed lines for DataTable with notes
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
                                usesWaste = line.WastePerProduct == 0 && line.Quantity > 0, // If waste is 0, it might use waste from previous
                                note = note
                            });
                        }
                    }
                }

                optimization.SuggestedTotalNorm = totalNorm;
                optimization.SuggestedTotalWaste = totalWaste;
                optimization.SuggestedTotalCost = totalCost;
                optimization.Message = $"Optimizatsiya natijasi: Umumiy norma {totalNorm:F4} m, Umumiy chiqindi {totalWaste:F4} m, Umumiy narx {totalCost:F2}";

                // Convert Dictionary to list for JSON serialization
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
                        optimizedLines = allOptimizedLines // Detailed lines for DataTable
                    },
                    totalNorm = totalNorm,
                    totalWaste = totalWaste,
                    totalCost = totalCost
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                System.Diagnostics.Debug.WriteLine($"Calculate error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"Xatolik: {ex.Message}" }, JsonRequestBehavior.AllowGet);
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

            // Check if optimization was performed (from hidden input)
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
                    // Recalculate fields
                    RecalculateLineFields(viewModel.CuttingLines);

                    // Get or create NORMVERSION
                    NORMVERSION normVersion;
                    bool isUpdate = false;
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
                        isUpdate = true;
                        normVersion.EffectiveFrom = ParsePeriod(viewModel.Period);
                        normVersion.EffectiveTo = viewModel.EffectiveTo;
                        normVersion.Comment = viewModel.Comment;

                        // Delete existing lines
                        var deletedLinesCount = normVersion.NORMLINES.Count;
                        db.NORMLINES.RemoveRange(normVersion.NORMLINES);
                        await db.SaveChangesAsync();
                        
                        // Log deletion of old NORMLINES
                        if (deletedLinesCount > 0)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                                $"Mahsulot {viewModel.ProductCode} uchun NORM versiyasi (ID: {viewModel.NormVersionID.Value}) yangilandi. {deletedLinesCount} ta eski NORMLINE o'chirildi.");
                        }
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
                        await db.SaveChangesAsync();
                        
                        // Log creation of new NORMVERSION
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {viewModel.ProductCode} uchun yangi NORM versiyasi yaratildi. Period: {viewModel.Period}");
                    }

                    // Delete existing NORM_INPUT_LINES for this period (if updating)
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
                            LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                                $"Mahsulot {viewModel.ProductCode} uchun NORM versiyasi (ID: {normVersion.ID}) dan {existingInputLines.Count} ta eski NORM_INPUT_LINE o'chirildi.");
                        }
                    }

                    // Save detailed cutting variants to NORM_INPUT_LINES
                    int inputLinesCount = 0;
                    foreach (var line in viewModel.CuttingLines.Where(l => l.IsSelected && l.MaterialID > 0))
                    {
                        // Get material to check if it requires cutting and get standard length
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
                        
                        // Calculate Consumption and WasteAmount
                        // Use WastePerProduct from viewModel (user can manually edit it for all materials)
                        double consumption = 0;
                        double wasteAmount = 0;
                        
                        if (line.CalculatedLengthMm.HasValue && line.CalculatedLengthMm.Value > 0)
                        {
                            // Consumption = CutLengthMm * PiecesPerProduct (in mm)
                            consumption = (double)line.CalculatedLengthMm.Value * line.Quantity;
                            
                            // WasteAmount: Use WastePerProduct from viewModel (already in meters, convert to mm)
                            // User can set waste manually for any material type
                            if (line.WastePerProduct > 0)
                            {
                                // Convert from meters to mm
                                wasteAmount = (double)line.WastePerProduct * 1000.0;
                            }
                            else if (requiresCutting && standardLengthMm > 0)
                            {
                                // If waste is 0 and material requires cutting, calculate it
                                var wastePerPiece = Math.Max(0, standardLengthMm - (double)line.CalculatedLengthMm.Value);
                                wasteAmount = wastePerPiece * line.Quantity;
                            }
                            // For non-cutting materials, wasteAmount remains 0 unless user sets it
                        }
                        
                        var inputLine = new NORM_INPUT_LINES
                        {
                            ProductID = viewModel.ProductID,
                            MaterialID = line.MaterialID,
                            CutLengthMm = (int)(line.CalculatedLengthMm ?? 0),
                            PiecesPerProduct = line.Quantity,
                            Month = viewModel.Period,
                            CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                            CreatedDT = DateTime.Now,
                            Price = line.UnitPrice ?? 0,
                            Consumption = consumption > 0 ? (double?)consumption : null,
                            WasteAmount = wasteAmount > 0 ? (double?)wasteAmount : null
                        };

                        db.NORM_INPUT_LINES.Add(inputLine);
                        inputLinesCount++;
                    }

                    await db.SaveChangesAsync();
                    
                    // Log creation of NORM_INPUT_LINES
                    if (inputLinesCount > 0)
                    {
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {viewModel.ProductCode} uchun NORM versiyasi (ID: {normVersion.ID}) ga {inputLinesCount} ta NORM_INPUT_LINE qo'shildi.");
                    }

                    // Now update NORM_LINES based on NORM_INPUT_LINES data
                    // Group by MaterialID and aggregate
                    var materialGroups = viewModel.CuttingLines
                        .Where(l => l.IsSelected && l.MaterialID > 0)
                        .GroupBy(l => l.MaterialID)
                        .ToList();

                    // Update aggregated NORMLINE for each material based on NORM_INPUT_LINES
                    var period = normVersion.EffectiveFrom?.ToString("yyyy-MM") ?? viewModel.Period;
                    foreach (var group in materialGroups)
                    {
                        var materialId = group.Key;
                        
                        // Calculate QtyPerUnit and WastePercent from NORM_INPUT_LINES
                        var inputLinesForMaterial = await db.NORM_INPUT_LINES
                            .Where(il => il.ProductID == viewModel.ProductID 
                                && il.MaterialID == materialId 
                                && il.Month == period)
                            .ToListAsync();
                        
                        // QtyPerUnit = Sum of Consumption from NORM_INPUT_LINES
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
                        }

                        // Check if NORMLINE already exists for this material
                        var existingNormLine = await db.NORMLINES
                            .FirstOrDefaultAsync(nl => nl.NormID == normVersion.ID && nl.MaterialID == materialId);
                        
                        if (existingNormLine != null)
                        {
                            // Update existing
                            existingNormLine.QtyPerUnit = qtyPerUnit;
                            existingNormLine.WastePercent = wastePercent;
                        }
                        else
                        {
                            // Create new
                            var aggregatedLine = new NORMLINE
                            {
                                NormID = normVersion.ID,
                                MaterialID = materialId,
                                QtyPerUnit = qtyPerUnit,
                                WastePercent = wastePercent
                            };
                            db.NORMLINES.Add(aggregatedLine);
                        }
                    }

                    await db.SaveChangesAsync();
                    
                    // Log creation/update of NORMLINES
                    LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                        $"Mahsulot {viewModel.ProductCode} uchun NORM versiyasi (ID: {normVersion.ID}) ga {materialGroups.Count} ta NORMLINE yangilandi/yaratildi.");

                    // Create/Update BOM_HEADERS and BOM_LINES
                    var bomHeader = await db.BOM_HEADERS
                        .FirstOrDefaultAsync(b => b.ProductID == viewModel.ProductID);

                    bool bomHeaderCreated = false;
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
                        bomHeaderCreated = true;
                        
                        // Log creation of BOM_HEADERS
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {viewModel.ProductCode} uchun yangi BOM_HEADERS yaratildi (ID: {bomHeader.ID}).");
                    }
                    else
                    {
                        // Update version if needed
                        bomHeader.VersionNo = viewModel.Period;
                        await db.SaveChangesAsync();
                    }

                    // Delete existing BOM_LINES
                    var existingBomLines = await db.BOM_LINES
                        .Where(b => b.BOMID == bomHeader.ID)
                        .ToListAsync();
                    var deletedBomLinesCount = existingBomLines.Count;
                    db.BOM_LINES.RemoveRange(existingBomLines);
                    await db.SaveChangesAsync();
                    
                    // Log deletion of old BOM_LINES
                    if (deletedBomLinesCount > 0)
                    {
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {viewModel.ProductCode} uchun BOM_HEADERS (ID: {bomHeader.ID}) dan {deletedBomLinesCount} ta eski BOM_LINE o'chirildi.");
                    }

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
                    
                    // Log creation of BOM_LINES
                    if (materialGroups.Count > 0)
                    {
                        LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                            $"Mahsulot {viewModel.ProductCode} uchun BOM_HEADERS (ID: {bomHeader.ID}) ga {materialGroups.Count} ta yangi BOM_LINE qo'shildi.");
                    }

                    // Log final summary
                    var actionType = isUpdate ? "yangilandi" : "yaratildi";
                    LogHelper.LogToDatabase(User.Identity.Name, "NormCutting", 
                        $"Mahsulot {viewModel.ProductCode} uchun NORM versiyasi {actionType}. Period: {viewModel.Period}, {materialGroups.Count} ta material, {viewModel.CuttingLines.Count(l => l.IsSelected)} ta kesish variantlari.");

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
                if (line.MaterialID > 0 && line.Quantity > 0)
                {
                    // Get material to check if it requires cutting
                    var material = db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .FirstOrDefault(p => p.ID == line.MaterialID);
                    
                    bool requiresCutting = false;
                    if (material?.PRODUCT_CATEGORIES != null)
                    {
                        requiresCutting = material.PRODUCT_CATEGORIES.RequiresCutting;
                    }

                    // If StandardLengthMm is not set, try to get it from material
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
                    // Only divide by 1000 for materials that require cutting (wood, cellophane, cardboard)
                    if (requiresCutting && line.CalculatedLengthMm.HasValue)
                    {
                        // For cutting materials: convert mm to m
                        line.ConsumptionPerProduct = (decimal)((line.CalculatedLengthMm.Value / 1000.0) * line.Quantity);
                    }
                    else
                    {
                        // For non-cutting materials: use quantity as-is (piece-based)
                        line.ConsumptionPerProduct = line.Quantity;
                    }
                    
                    // Calculate waste only if it's not already set by user (user can manually edit waste for all materials)
                    // If waste is 0 or not set, and material requires cutting, calculate it automatically
                    if (line.WastePerProduct == 0 && requiresCutting && line.StandardLengthMm.HasValue && line.StandardLengthMm.Value > 0 && line.CalculatedLengthMm.HasValue && line.CalculatedLengthMm.Value > 0)
                    {
                        var length = line.CalculatedLengthMm.Value;
                        var standardLength = line.StandardLengthMm.Value;
                        var quantity = line.Quantity;
                        var piecesPerBar = 1; // Always 1 (removed from UI)
                        
                        // How many bars/sheets we need (1 piece per bar)
                        var barsNeeded = quantity;
                        // Waste per bar = standardLength - length (1 piece per bar)
                        var wastePerBar = Math.Max(0, standardLength - length);
                        // Total waste = waste per bar * number of bars needed (convert mm to m)
                        line.WastePerProduct = (decimal)(wastePerBar / 1000.0) * barsNeeded;
                    }
                    // If waste is already set (by user), keep it as is
                    // If material doesn't require cutting, waste can still be set manually by user

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

        private (List<NormCuttingLineViewModel> optimizedLines, Dictionary<int, string> notes) OptimizeMaterialCutting(List<NormCuttingLineViewModel> lines, PRODUCT material)
        {
            // Check if material requires cutting
            bool requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
            
            var notes = new Dictionary<int, string>();
            
            if (!requiresCutting || lines.Count <= 1)
            {
                // For non-cutting materials or single line, return all lines as-is
                foreach (var line in lines)
                {
                    notes[line.ID] = "Optimizatsiya qo'llanilmadi";
                }
                return (lines, notes);
            }

            // Get standard length
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
                // No standard length, return all lines as-is
                foreach (var line in lines)
                {
                    notes[line.ID] = "Standart uzunlik yo'q";
                }
                return (lines, notes);
            }

            // Create optimized lines with waste optimization
            // Algorithm: Try to use waste from larger cuts for smaller cuts
            var optimizedLines = new List<NormCuttingLineViewModel>();
            var wastePool = new List<WasteSegment>(); // Track available waste from each bar

            // Create a dictionary to map line IDs to their original position (1-based index)
            var linePositionMap = new Dictionary<int, int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].ID > 0)
                {
                    linePositionMap[lines[i].ID] = i + 1; // 1-based position
                }
            }

            // Sort lines by cut length (descending) to process larger cuts first
            // This way, larger cuts generate waste that can be used for smaller cuts
            var sortedLines = lines.Where(l => l.CalculatedLengthMm.HasValue && l.CalculatedLengthMm.Value > 0)
                .OrderByDescending(l => l.CalculatedLengthMm.Value)
                .ToList();

            // Track which lines use waste from which previous lines (for notes)
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

                // Try to use available waste from waste pool
                var wasteToRemove = new List<WasteSegment>();
                var wasteUsedFromLines = new List<(int sourceLineId, double wasteLength, int piecesUsed)>(); // Track which lines' waste we're using
                
                foreach (var waste in wastePool.OrderByDescending(w => w.Length).ToList())
                {
                    if (waste.Length >= cutLength && waste.RemainingQuantity > 0 && remainingQuantity > 0)
                    {
                        // How many pieces can we cut from this waste?
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
                            
                            // Update waste: when we cut from waste, the waste length decreases
                            // But we need to track how much waste is left from each bar
                            // If we used all pieces from this waste segment, remove it
                            // Otherwise, update the length (waste becomes smaller)
                            var usedLength = piecesFromThisWaste * cutLength;
                            
                            // Check if we can still use this waste for more cuts
                            var remainingWasteLength = waste.Length - usedLength;
                            if (remainingWasteLength < cutLength || waste.RemainingQuantity <= 0)
                            {
                                wasteToRemove.Add(waste);
                            }
                            else
                            {
                                // Update waste length for future use
                                waste.Length = remainingWasteLength;
                            }
                        }
                    }
                }

                // Remove used waste segments
                foreach (var waste in wasteToRemove)
                {
                    wastePool.Remove(waste);
                }

                // Calculate how many new bars we need for remaining quantity
                if (remainingQuantity > 0)
                {
                    var totalNeeded = cutLength * remainingQuantity;
                    newBarsNeeded = (int)Math.Ceiling(totalNeeded / standardLengthMm);
                    
                    // Calculate waste from new bars
                    var totalLengthFromNewBars = newBarsNeeded * standardLengthMm;
                    var wasteFromNewBars = totalLengthFromNewBars - totalNeeded;
                    
                    // Add waste to pool for future use
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

                // Create detailed note about waste usage with position numbers
                if (piecesFromWaste > 0 && remainingQuantity == 0)
                {
                    // All pieces were cut from waste
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
                    // Some pieces from waste, some from new bars
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
                    // All from new bars
                    note = $"Yangi {newBarsNeeded} ta {standardLengthMm:F0}mm bar/sheet ishlatilgan. {totalWaste:F0}mm chiqindi hosil bo'lgan (keyingi pozitsiyalar uchun ishlatilishi mumkin).";
                }
                else
                {
                    note = $"Yangi {newBarsNeeded} ta {standardLengthMm:F0}mm bar/sheet ishlatilgan. Chiqindi yo'q.";
                }

                // Create optimized line
                var optimizedLine = new NormCuttingLineViewModel
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

                // Recalculate consumption and waste
                optimizedLine.ConsumptionPerProduct = (decimal)((cutLength / 1000.0) * quantity);
                optimizedLine.WastePerProduct = (decimal)(totalWaste / 1000.0);
                optimizedLine.TotalCostPerProduct = optimizedLine.ConsumptionPerProduct * (optimizedLine.UnitPrice ?? 0);
                optimizedLine.Norma = optimizedLine.ConsumptionPerProduct;
                optimizedLine.Chiqindi = optimizedLine.WastePerProduct;

                // Store note for this line
                lineNotes[line.ID] = note;

                optimizedLines.Add(optimizedLine);
            }

            // Add lines without CalculatedLengthMm as-is
            foreach (var line in lines.Where(l => !l.CalculatedLengthMm.HasValue || l.CalculatedLengthMm.Value <= 0))
            {
                optimizedLines.Add(line);
                notes[line.ID] = "Uzunlik kiritilmagan";
            }

            return (optimizedLines, notes);
        }

        // Helper class for tracking waste segments
        private class WasteSegment
        {
            public double Length { get; set; }
            public int RemainingQuantity { get; set; }
            public double OriginalLength { get; set; } // Original waste length for tracking
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

