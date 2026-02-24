using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
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

            // Query finished goods that are not deleted
            var finishedGoods = await db.PRODUCTS
                .Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                .ToListAsync();

            var productIds = finishedGoods.Select(p => p.ID).ToList();
            var normVersions = await db.NORMVERSIONS
                .Where(n => n.ProductID.HasValue
                    && productIds.Contains(n.ProductID.Value)
                    && (n.IsDeleted == null || n.IsDeleted == false))
                .ToListAsync();

            var latestNormByProduct = normVersions
                .GroupBy(n => n.ProductID.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(n => n.EffectiveFrom ?? DateTime.MinValue)
                          .ThenByDescending(n => n.ID)
                          .FirstOrDefault());

            // Olish the latest BOM version uchun har bir product
            var bomIndexList = new List<BomIndexViewModel>();

            foreach (var product in finishedGoods)
            {
                // Olish the latest faol BOM header uchun this product
                // Sort by VersionNo as date (YYYY-MM format) descending, so'ng by CreatedAt
                var latestBomHeader = await db.BOM_HEADERS
                    .Where(bh => bh.ProductID == product.ID && bh.IsActive == true)
                    .ToListAsync();

                // Tahlil qilish VersionNo as date uchun proper sorting (YYYY-MM format)
                // VersionNo format: '2024-12' (year-month)
                var sortedBomHeaders = latestBomHeader
                    .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                    .OrderByDescending(bh =>
                    {
                        // Urinish ga tahlil VersionNo as YYYY-MM date
                        string versionNo = bh.VersionNo.Trim();
                        if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                        {
                            return parsedDate;
                        }
                        // agar parsing fails, try ga extract year va month manually
                        var parts = versionNo.Split('-');
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                        {
                            if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                            {
                                return new DateTime(year, month, 1);
                            }
                        }
                        // Fallback ga minimal date
                        return DateTime.MinValue;
                    })
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                // agar no BOM bilan VersionNo found, fallback ga latest by CreatedAt
                if (sortedBomHeaders == null)
                {
                    sortedBomHeaders = latestBomHeader
                        .OrderByDescending(bh => bh.CreatedAt)
                        .FirstOrDefault();
                }

                if (sortedBomHeaders != null)
                {
                    string normVersion = null;
                    if (latestNormByProduct.TryGetValue(product.ID, out var latestNorm) && latestNorm != null)
                    {
                        if (latestNorm.EffectiveFrom.HasValue)
                        {
                            normVersion = latestNorm.EffectiveFrom.Value.ToString("yyyy-MM");
                        }
                    }

                    if (string.IsNullOrEmpty(normVersion))
                    {
                        normVersion = sortedBomHeaders.VersionNo ?? "N/A";
                    }

                    bomIndexList.Add(new BomIndexViewModel
                    {
                        ProductId = product.ID,
                        ProductPno = product.PNo,
                        ProductName = product.Name,
                        NormVersion = normVersion
                    });
                }
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
            BOM_HEADERS bomHeader = null;

            if (bomHeaderId.HasValue)
            {
                // Yuklash specific BOM header
                bomHeader = await db.BOM_HEADERS
                    .Include(bh => bh.PRODUCT)
                    .FirstOrDefaultAsync(bh => bh.ID == bomHeaderId.Value);
            }
            else
            {
                // Yuklash latest faol BOM header uchun this product
                // Sort by VersionNo as date (YYYY-MM format) descending
                var bomHeaders = await db.BOM_HEADERS
                    .Include(bh => bh.PRODUCT)
                    .Where(bh => bh.ProductID == productId && bh.IsActive == true)
                    .ToListAsync();

                var bomHeaderWithVersion = bomHeaders
                    .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                    .OrderByDescending(bh =>
                    {
                        // Urinish ga tahlil VersionNo as YYYY-MM date
                        string versionNo = bh.VersionNo.Trim();
                        if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                        {
                            return parsedDate;
                        }
                        // agar parsing fails, try ga extract year va month manually
                        var parts = versionNo.Split('-');
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                        {
                            if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                            {
                                return new DateTime(year, month, 1);
                            }
                        }
                        // Fallback ga minimal date
                        return DateTime.MinValue;
                    })
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                // agar no BOM bilan VersionNo found, fallback ga latest by CreatedAt
                if (bomHeaderWithVersion == null)
                {
                    bomHeader = bomHeaders
                        .OrderByDescending(bh => bh.CreatedAt)
                        .FirstOrDefault();
                }
                else
                {
                    bomHeader = bomHeaderWithVersion;
                }
            }

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
            var rootNode = BuildBomTree(productId, bomLines, product);

            // Yuklash product image
            string imageUrl = null;
            var productImage = await db.PRODUCTIMAGES
                .Where(pi => pi.ProdID == productId && (pi.IsDeleted == null || pi.IsDeleted == false))
                .FirstOrDefaultAsync();

            if (productImage != null && productImage.Image != null)
            {
                imageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(productImage.Image);
            }

            var viewModel = new BomDetailsViewModel
            {
                ProductId = product.ID,
                ProductPno = product.PNo,
                ProductName = product.Name,
                NormVersion = bomHeader.VersionNo ?? "N/A",
                ImageUrl = imageUrl,
                RootNode = rootNode
            };

            return View(viewModel);
        }

        /// <summary>
        /// Builds a hierarchical BOM tree starting dan the root product
        /// </summary>
        private BomTreeNodeViewModel BuildBomTree(int parentProductId, List<BOM_LINES> bomLines, PRODUCT parentProduct)
        {
            // Yuklash product image uchun root
            string rootImageUrl = null;
            var rootProductImage = db.PRODUCTIMAGES
                .Where(pi => pi.ProdID == parentProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                .FirstOrDefault();

            if (rootProductImage != null && rootProductImage.Image != null)
            {
                rootImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(rootProductImage.Image);
            }

            var node = new BomTreeNodeViewModel
            {
                ProductId = parentProduct.ID,
                ProductPno = parentProduct.PNo,
                ProductName = parentProduct.Name,
                Quantity = 1, // Root always has quantity 1
                ImageUrl = rootImageUrl,
                Children = new List<BomTreeNodeViewModel>()
            };

            // barcha bomLines passed here are direct children of the root
            // BOM_LINES.ComponentID is the child component
            foreach (var bomLine in bomLines)
            {
                if (!bomLine.ComponentID.HasValue) continue;

                var childProduct = bomLine.PRODUCT;
                if (childProduct == null || childProduct.IsDeleted) continue;

                // Yuklash child product image
                string childImageUrl = null;
                var childProductImage = db.PRODUCTIMAGES
                    .Where(pi => pi.ProdID == childProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                    .FirstOrDefault();

                if (childProductImage != null && childProductImage.Image != null)
                {
                    childImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(childProductImage.Image);
                }

                var childNode = new BomTreeNodeViewModel
                {
                    ProductId = childProduct.ID,
                    ProductPno = childProduct.PNo,
                    ProductName = childProduct.Name,
                    Quantity = bomLine.Qty ?? 0,
                    UnitShortname = bomLine.UNIT?.ShortName ?? "",
                    ImageUrl = childImageUrl,
                    Children = new List<BomTreeNodeViewModel>()
                };

                // Tekshirish agar this child component has its own BOM (i.e., it's an assembly)
                var childBomHeaders = db.BOM_HEADERS
                    .Where(bh => bh.ProductID == childProduct.ID && bh.IsActive == true)
                    .ToList();

                var childBomHeader = childBomHeaders
                    .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                    .OrderByDescending(bh =>
                    {
                        string versionNo = bh.VersionNo.Trim();
                        if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                        {
                            return parsedDate;
                        }
                        var parts = versionNo.Split('-');
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                        {
                            if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                            {
                                return new DateTime(year, month, 1);
                            }
                        }
                        return DateTime.MinValue;
                    })
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                if (childBomHeader != null)
                {
                    // Yuklash child BOM lines
                    var childBomLines = db.BOM_LINES
                        .Include(bl => bl.PRODUCT)
                        .Include(bl => bl.UNIT)
                        .Where(bl => bl.BOMID == childBomHeader.ID)
                        .ToList();

                    // Recursively build children
                    childNode.Children = BuildBomTreeChildren(childProduct.ID, childBomLines);
                }

                node.Children.Add(childNode);
            }

            return node;
        }

        /// <summary>
        /// Recursively builds children uchun a node
        /// </summary>
        private List<BomTreeNodeViewModel> BuildBomTreeChildren(int parentProductId, List<BOM_LINES> bomLines)
        {
            var children = new List<BomTreeNodeViewModel>();

            foreach (var bomLine in bomLines)
            {
                if (!bomLine.ComponentID.HasValue) continue;

                var childProduct = bomLine.PRODUCT;
                if (childProduct == null || childProduct.IsDeleted) continue;

                // Yuklash child product image
                string childImageUrl = null;
                var childProductImage = db.PRODUCTIMAGES
                    .Where(pi => pi.ProdID == childProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                    .FirstOrDefault();

                if (childProductImage != null && childProductImage.Image != null)
                {
                    childImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(childProductImage.Image);
                }

                var childNode = new BomTreeNodeViewModel
                {
                    ProductId = childProduct.ID,
                    ProductPno = childProduct.PNo,
                    ProductName = childProduct.Name,
                    Quantity = bomLine.Qty ?? 0,
                    UnitShortname = bomLine.UNIT?.ShortName ?? "",
                    ImageUrl = childImageUrl,
                    Children = new List<BomTreeNodeViewModel>()
                };

                // Tekshirish agar this child has its own BOM
                var childBomHeaders = db.BOM_HEADERS
                    .Where(bh => bh.ProductID == childProduct.ID && bh.IsActive == true)
                    .ToList();

                var childBomHeader = childBomHeaders
                    .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                    .OrderByDescending(bh =>
                    {
                        string versionNo = bh.VersionNo.Trim();
                        if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                        {
                            return parsedDate;
                        }
                        var parts = versionNo.Split('-');
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                        {
                            if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                            {
                                return new DateTime(year, month, 1);
                            }
                        }
                        return DateTime.MinValue;
                    })
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                if (childBomHeader != null)
                {
                    var childBomLines = db.BOM_LINES
                        .Include(bl => bl.PRODUCT)
                        .Include(bl => bl.UNIT)
                        .Where(bl => bl.BOMID == childBomHeader.ID)
                        .ToList();

                    childNode.Children = BuildBomTreeChildren(childProduct.ID, childBomLines);
                }

                children.Add(childNode);
            }

            return children;
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
