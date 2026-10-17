using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.BOM;

namespace tahsinERP.Controllers.Engineering
{
    [Authorize]
    public class BomController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();

        // Olish so'rovi: Bom
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "BOM"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Tayyor mahsulotlar, normalar va faol BOM headerlar — 3 ta proyeksiyalangan so'rov
            // (avval har mahsulotga alohida BOM_HEADERS so'rovi ketardi, MES-P22)
            var finishedGoods = await db.PRODUCTS.AsNoTracking()
                .Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToListAsync();

            var productIds = finishedGoods.Select(p => p.ID).ToList();
            var normVersions = await db.NORMVERSIONS.AsNoTracking()
                .Where(n => n.ProductID.HasValue
                    && productIds.Contains(n.ProductID.Value)
                    && (n.IsDeleted == null || n.IsDeleted == false))
                .Select(n => new { ProductID = n.ProductID.Value, n.ID, n.EffectiveFrom })
                .ToListAsync();

            var latestNormPeriodByProduct = normVersions
                .GroupBy(n => n.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(n => n.EffectiveFrom ?? DateTime.MinValue)
                          .ThenByDescending(n => n.ID)
                          .Select(n => n.EffectiveFrom.HasValue ? n.EffectiveFrom.Value.ToString("yyyy-MM") : null)
                          .FirstOrDefault());

            var activeHeaders = await db.BOM_HEADERS.AsNoTracking()
                .Where(bh => bh.ProductID.HasValue && productIds.Contains(bh.ProductID.Value) && bh.IsActive == true)
                .Select(bh => new { ProductID = bh.ProductID.Value, bh.VersionNo, bh.CreatedAt })
                .ToListAsync();
            var headersByProduct = activeHeaders.GroupBy(h => h.ProductID).ToDictionary(g => g.Key, g => g.ToList());

            var bomIndexList = new List<BomIndexViewModel>();
            foreach (var product in finishedGoods)
            {
                if (!headersByProduct.TryGetValue(product.ID, out var headers))
                {
                    continue;
                }

                // Eng so'nggi faol BOM: VersionNo (yyyy-MM) bo'yicha, bo'lmasa CreatedAt bo'yicha
                var latestHeader = headers
                    .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                    .OrderByDescending(bh => ParseVersionNoDate(bh.VersionNo))
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault()
                    ?? headers.OrderByDescending(bh => bh.CreatedAt).First();

                latestNormPeriodByProduct.TryGetValue(product.ID, out var normVersion);
                if (string.IsNullOrEmpty(normVersion))
                {
                    normVersion = latestHeader.VersionNo ?? "N/A";
                }

                bomIndexList.Add(new BomIndexViewModel
                {
                    ProductId = product.ID,
                    ProductPno = product.PNo,
                    ProductName = product.Name,
                    NormVersion = normVersion
                });
            }

            // Order by product number
            bomIndexList = bomIndexList.OrderBy(b => b.ProductPno).ToList();

            return View(bomIndexList);
        }

        // Olish so'rovi: Bom/Details/5
        public async Task<ActionResult> Details(int productId, int? bomHeaderId = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "BOM"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Find the product
            var product = await db.PRODUCTS
                .FirstOrDefaultAsync(p => p.ID == productId && p.IsDeleted == false);

            if (product == null)
            {
                TempData["Error"] = "Mahsulot topilmadi.";
                return RedirectToAction("Index");
            }

            // Find the BOM header
            var summaryService = new BomMaterialSummaryService(db);
            BOM_HEADERS bomHeader = await summaryService.SelectActiveBomHeaderAsync(productId, bomHeaderId);

            if (bomHeader == null)
            {
                TempData["Error"] = "Bu mahsulot uchun BOM topilmadi.";
                return RedirectToAction("Index");
            }

            // Yuklash barcha BOM lines bilan related products va units
            var bomLines = await db.BOM_LINES
                .Include(bl => bl.PRODUCT)
                .Include(bl => bl.UNIT)
                .Where(bl => bl.BOMID == bomHeader.ID)
                .ToListAsync();

            // Build hierarchical tree
            var rootNode = summaryService.BuildBomTree(productId, bomLines, product, includeImages: true);
            var normVersion = bomHeader.VersionNo ?? "N/A";
            summaryService.ApplyWoodQuantitiesFromNorm(rootNode, product.ID, normVersion);

            // Mahsulot rasmi daraxt bilan birga yuklangan (avval ikkinchi marta alohida so'ralardi, MES-P23)
            string imageUrl = rootNode.ImageUrl;

            var viewModel = new BomDetailsViewModel
            {
                ProductId = product.ID,
                ProductPno = product.PNo,
                ProductName = product.Name,
                NormVersion = normVersion,
                ImageUrl = imageUrl,
                RootNode = rootNode,
                MaterialSummaries = summaryService.BuildMaterialSummaries(rootNode, product.ID, normVersion)
            };

            return View(viewModel);
        }

        private static DateTime ParseVersionNoDate(string versionNo)
        {
            if (string.IsNullOrWhiteSpace(versionNo))
                return DateTime.MinValue;

            versionNo = versionNo.Trim();
            if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                return parsedDate;

            var parts = versionNo.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month)
                && month >= 1 && month <= 12 && year > 1900 && year < 2100)
            {
                return new DateTime(year, month, 1);
            }
            return DateTime.MinValue;
        }

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
