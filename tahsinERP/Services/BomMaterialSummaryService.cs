using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.ViewModels.BOM;

namespace tahsinERP.Services
{
    /// <summary>
    /// BOM daraxti va materiallar jadvali (1 tayyor mahsulot uchun) hisob-kitobi.
    /// BOM Details va ishlab chiqarish sarfi bir xil manbadan foydalanadi.
    /// </summary>
    public class BomMaterialSummaryService
    {
        private readonly DBTHSNEntities _db;

        public BomMaterialSummaryService(DBTHSNEntities db)
        {
            _db = db;
        }

        public async Task<List<BomMaterialSummaryViewModel>> GetMaterialSummariesPerUnitAsync(int productId, int? bomHeaderId = null, DateTime? asOfDate = null)
        {
            var bomHeader = await SelectActiveBomHeaderAsync(productId, bomHeaderId, asOfDate);
            if (bomHeader == null)
            {
                return new List<BomMaterialSummaryViewModel>();
            }

            var product = await _db.PRODUCTS
                .FirstOrDefaultAsync(p => p.ID == productId && p.IsDeleted == false);
            if (product == null)
            {
                return new List<BomMaterialSummaryViewModel>();
            }

            var bomLines = await _db.BOM_LINES
                .Include(bl => bl.PRODUCT)
                .Include(bl => bl.UNIT)
                .Where(bl => bl.BOMID == bomHeader.ID)
                .ToListAsync();

            var normVersion = bomHeader.VersionNo ?? string.Empty;
            var rootNode = BuildBomTree(productId, bomLines, product, includeImages: false);
            ApplyWoodQuantitiesFromNorm(rootNode, productId, normVersion);
            return BuildMaterialSummaries(rootNode, productId, normVersion);
        }

        public async Task<BOM_HEADERS> SelectActiveBomHeaderAsync(int productId, int? bomHeaderId = null, DateTime? asOfDate = null)
        {
            if (bomHeaderId.HasValue)
            {
                return await _db.BOM_HEADERS
                    .Include(bh => bh.PRODUCT)
                    .FirstOrDefaultAsync(bh => bh.ID == bomHeaderId.Value);
            }

            var bomHeaders = await _db.BOM_HEADERS
                .Include(bh => bh.PRODUCT)
                .Where(bh => bh.ProductID == productId && bh.IsActive == true)
                .ToListAsync();

            if (!bomHeaders.Any())
            {
                return null;
            }

            // Buyurtma sanasi berilgan bo'lsa — o'sha oyga (yoki undan oldingi) tegishli eng so'nggi BOM versiyasini olamiz,
            // ya'ni eng yangi versiyani emas, balki buyurtma sanasida amal qilgan versiyani.
            if (asOfDate.HasValue)
            {
                var asOfMonth = new DateTime(asOfDate.Value.Year, asOfDate.Value.Month, 1);
                var effective = bomHeaders
                    .Where(bh => GetBomEffectiveMonth(bh) <= asOfMonth)
                    .OrderByDescending(bh => GetBomEffectiveMonth(bh))
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                if (effective != null)
                {
                    return effective;
                }

                // Buyurtma sanasidan oldin hech qanday versiya bo'lmasa — eng eski (birinchi) versiyaga qaytamiz.
                return bomHeaders
                    .OrderBy(bh => GetBomEffectiveMonth(bh))
                    .ThenBy(bh => bh.CreatedAt)
                    .FirstOrDefault();
            }

            var bomHeaderWithVersion = bomHeaders
                .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                .OrderByDescending(bh => ParseVersionNoDate(bh.VersionNo))
                .ThenByDescending(bh => bh.CreatedAt)
                .FirstOrDefault();

            return bomHeaderWithVersion ?? bomHeaders
                .OrderByDescending(bh => bh.CreatedAt)
                .FirstOrDefault();
        }

        /// <summary>
        /// BOM versiyasining amal qilish oyi: VersionNo (yyyy-MM) dan, bo'lmasa CreatedAt dan (oyning 1-kuni).
        /// </summary>
        private static DateTime GetBomEffectiveMonth(BOM_HEADERS bh)
        {
            var fromVersion = ParseVersionNoDate(bh?.VersionNo);
            if (fromVersion > DateTime.MinValue)
            {
                return new DateTime(fromVersion.Year, fromVersion.Month, 1);
            }

            if (bh?.CreatedAt != null)
            {
                var c = bh.CreatedAt.Value;
                return new DateTime(c.Year, c.Month, 1);
            }

            return DateTime.MinValue;
        }

        public BomTreeNodeViewModel BuildBomTree(int parentProductId, List<BOM_LINES> bomLines, PRODUCT parentProduct, bool includeImages = true)
        {
            string rootImageUrl = null;
            if (includeImages)
            {
                var rootProductImage = _db.PRODUCTIMAGES
                    .Where(pi => pi.ProdID == parentProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                    .FirstOrDefault();

                if (rootProductImage?.Image != null)
                {
                    rootImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(rootProductImage.Image);
                }
            }

            var node = new BomTreeNodeViewModel
            {
                ProductId = parentProduct.ID,
                ProductPno = parentProduct.PNo,
                ProductName = parentProduct.Name,
                Quantity = 1,
                ImageUrl = rootImageUrl,
                Children = new List<BomTreeNodeViewModel>()
            };

            foreach (var bomLine in bomLines)
            {
                if (!bomLine.ComponentID.HasValue)
                {
                    continue;
                }

                var childProduct = bomLine.PRODUCT;
                if (childProduct == null || childProduct.IsDeleted)
                {
                    continue;
                }

                string childImageUrl = null;
                if (includeImages)
                {
                    var childProductImage = _db.PRODUCTIMAGES
                        .Where(pi => pi.ProdID == childProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                        .FirstOrDefault();

                    if (childProductImage?.Image != null)
                    {
                        childImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(childProductImage.Image);
                    }
                }

                var childNode = new BomTreeNodeViewModel
                {
                    ProductId = childProduct.ID,
                    ProductPno = childProduct.PNo,
                    ProductName = childProduct.Name,
                    Quantity = bomLine.Qty ?? 0,
                    UnitShortname = bomLine.UNIT?.ShortName ?? string.Empty,
                    UnitId = bomLine.UnitID ?? (childProduct.UnitID > 0 ? childProduct.UnitID : 0),
                    ImageUrl = childImageUrl,
                    Children = new List<BomTreeNodeViewModel>()
                };

                var childBomHeader = SelectLatestChildBomHeader(childProduct.ID);
                if (childBomHeader != null)
                {
                    var childBomLines = _db.BOM_LINES
                        .Include(bl => bl.PRODUCT)
                        .Include(bl => bl.UNIT)
                        .Where(bl => bl.BOMID == childBomHeader.ID)
                        .ToList();

                    childNode.Children = BuildBomTreeChildren(childProduct.ID, childBomLines, includeImages);
                }

                node.Children.Add(childNode);
            }

            return node;
        }

        public void ApplyWoodQuantitiesFromNorm(BomTreeNodeViewModel rootNode, int rootProductId, string normVersion)
        {
            var woodQtyByMaterial = LoadNormWoodDataByMaterial(rootProductId, normVersion)
                .ToDictionary(x => x.Key, x => x.Value.Quantity);

            if (!woodQtyByMaterial.Any() || rootNode?.Children == null)
            {
                return;
            }

            foreach (var child in rootNode.Children)
            {
                if (woodQtyByMaterial.TryGetValue(child.ProductId, out var qty))
                {
                    child.Quantity = qty;
                }
            }
        }

        public List<BomMaterialSummaryViewModel> BuildMaterialSummaries(BomTreeNodeViewModel rootNode, int rootProductId, string normVersion)
        {
            if (rootNode == null)
            {
                return new List<BomMaterialSummaryViewModel>();
            }

            var flattenedLeaves = new List<(int ProductId, string Pno, string Name, string Unit, int UnitId, decimal Qty)>();
            FlattenLeafMaterials(rootNode, 1m, true, flattenedLeaves);

            var materialIds = flattenedLeaves.Select(x => x.ProductId).Distinct().ToList();
            var priceByMaterial = LoadNormPricesByMaterial(rootProductId, normVersion, materialIds);
            var woodNormData = LoadNormWoodDataByMaterial(rootProductId, normVersion);

            return flattenedLeaves
                .GroupBy(x => x.ProductId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Qty);
                    var unitPrice = priceByMaterial.ContainsKey(g.Key) ? priceByMaterial[g.Key] : 0m;
                    var totalAmount = totalQty * unitPrice;

                    if (woodNormData.TryGetValue(g.Key, out var woodData))
                    {
                        totalQty = woodData.Quantity;
                        totalAmount = woodData.TotalAmount;
                        unitPrice = totalQty > 0 ? totalAmount / totalQty : unitPrice;
                    }

                    return new BomMaterialSummaryViewModel
                    {
                        MaterialId = g.Key,
                        MaterialPno = first.Pno,
                        MaterialName = first.Name,
                        UnitShortname = first.Unit,
                        UnitId = first.UnitId,
                        TotalQuantity = totalQty,
                        UnitPrice = unitPrice,
                        TotalAmount = totalAmount
                    };
                })
                .OrderBy(x => x.MaterialPno)
                .ToList();
        }

        private List<BomTreeNodeViewModel> BuildBomTreeChildren(int parentProductId, List<BOM_LINES> bomLines, bool includeImages)
        {
            var children = new List<BomTreeNodeViewModel>();

            foreach (var bomLine in bomLines)
            {
                if (!bomLine.ComponentID.HasValue)
                {
                    continue;
                }

                var childProduct = bomLine.PRODUCT;
                if (childProduct == null || childProduct.IsDeleted)
                {
                    continue;
                }

                string childImageUrl = null;
                if (includeImages)
                {
                    var childProductImage = _db.PRODUCTIMAGES
                        .Where(pi => pi.ProdID == childProduct.ID && (pi.IsDeleted == null || pi.IsDeleted == false))
                        .FirstOrDefault();

                    if (childProductImage?.Image != null)
                    {
                        childImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(childProductImage.Image);
                    }
                }

                var childNode = new BomTreeNodeViewModel
                {
                    ProductId = childProduct.ID,
                    ProductPno = childProduct.PNo,
                    ProductName = childProduct.Name,
                    Quantity = bomLine.Qty ?? 0,
                    UnitShortname = bomLine.UNIT?.ShortName ?? string.Empty,
                    UnitId = bomLine.UnitID ?? (childProduct.UnitID > 0 ? childProduct.UnitID : 0),
                    ImageUrl = childImageUrl,
                    Children = new List<BomTreeNodeViewModel>()
                };

                var childBomHeader = SelectLatestChildBomHeader(childProduct.ID);
                if (childBomHeader != null)
                {
                    var childBomLines = _db.BOM_LINES
                        .Include(bl => bl.PRODUCT)
                        .Include(bl => bl.UNIT)
                        .Where(bl => bl.BOMID == childBomHeader.ID)
                        .ToList();

                    childNode.Children = BuildBomTreeChildren(childProduct.ID, childBomLines, includeImages);
                }

                children.Add(childNode);
            }

            return children;
        }

        private BOM_HEADERS SelectLatestChildBomHeader(int productId)
        {
            var childBomHeaders = _db.BOM_HEADERS
                .Where(bh => bh.ProductID == productId && bh.IsActive == true)
                .ToList();

            return childBomHeaders
                .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                .OrderByDescending(bh => ParseVersionNoDate(bh.VersionNo))
                .ThenByDescending(bh => bh.CreatedAt)
                .FirstOrDefault();
        }

        private static DateTime ParseVersionNoDate(string versionNo)
        {
            if (string.IsNullOrWhiteSpace(versionNo))
            {
                return DateTime.MinValue;
            }

            versionNo = versionNo.Trim();
            if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }

            var parts = versionNo.Split('-');
            if (parts.Length >= 2
                && int.TryParse(parts[0], out var year)
                && int.TryParse(parts[1], out var month)
                && month >= 1 && month <= 12
                && year > 1900 && year < 2100)
            {
                return new DateTime(year, month, 1);
            }

            return DateTime.MinValue;
        }

        private void FlattenLeafMaterials(
            BomTreeNodeViewModel node,
            decimal multiplier,
            bool isRoot,
            List<(int ProductId, string Pno, string Name, string Unit, int UnitId, decimal Qty)> sink)
        {
            if (node == null)
            {
                return;
            }

            var nodeMultiplier = isRoot ? multiplier : multiplier * node.Quantity;
            var hasChildren = node.Children != null && node.Children.Any();
            if (!isRoot && !hasChildren && nodeMultiplier > 0)
            {
                sink.Add((node.ProductId, node.ProductPno, node.ProductName, node.UnitShortname, node.UnitId, nodeMultiplier));
            }

            if (!hasChildren)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                FlattenLeafMaterials(child, nodeMultiplier, false, sink);
            }
        }

        private Dictionary<int, decimal> LoadNormPricesByMaterial(int rootProductId, string normVersion, List<int> materialIds)
        {
            if (materialIds == null || !materialIds.Any() || string.IsNullOrWhiteSpace(normVersion))
            {
                return new Dictionary<int, decimal>();
            }

            return _db.NORM_INPUT_LINES
                .Where(n => n.ProductID == rootProductId
                    && n.Month == normVersion
                    && materialIds.Contains(n.MaterialID))
                .OrderByDescending(n => n.CreatedDT)
                .AsEnumerable()
                .GroupBy(n => n.MaterialID)
                .ToDictionary(g => g.Key, g => g.First().Price);
        }

        private Dictionary<int, (decimal Quantity, decimal TotalAmount)> LoadNormWoodDataByMaterial(int rootProductId, string normVersion)
        {
            if (string.IsNullOrWhiteSpace(normVersion))
            {
                return new Dictionary<int, (decimal Quantity, decimal TotalAmount)>();
            }

            var lines = _db.NORM_INPUT_LINES
                .Include(n => n.PRODUCT)
                .Include(n => n.PRODUCT.PRODUCT_CATEGORIES)
                .Where(n => n.ProductID == rootProductId && n.Month == normVersion)
                .ToList()
                .Where(n => IsWoodProductCategory(n.PRODUCT?.PRODUCT_CATEGORIES))
                .ToList();

            if (!lines.Any())
            {
                return new Dictionary<int, (decimal Quantity, decimal TotalAmount)>();
            }

            return lines
                .GroupBy(n => n.MaterialID)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Quantity: ComputeWoodBarQuantity(g),
                        TotalAmount: g.Sum(ComputeNormWoodLineAmount)));
        }

        /// <summary>
        /// Yog'och (kesiladigan) material uchun BOM dona soni:
        /// ceil( Σ((CutLengthMm + chiqindi_mm) × PiecesPerProduct) / standart uzunlik ).
        /// Ya'ni har bir qatorda kesim uzunligi + normadagi chiqindi (offcut) ham hisobga olinadi,
        /// shunda taxta soni real ishlatiladigan materialni aks ettiradi (chiqindini tashlab ketmaydi).
        /// WasteAmount bazada qator bo'yicha JAMI chiqindi (mm) sifatida saqlanadi, shuning uchun to'g'ridan-to'g'ri qo'shiladi.
        /// Masalan: 4000mm taxta, 1140×12 (chiqindi 2320) + 940×4 (240) + 880×3 (1360) + 720×4 (1120)
        ///          = 28000mm → ceil(28000/4000) = 7 dona.
        /// </summary>
        private static decimal ComputeWoodBarQuantity(IEnumerable<NORM_INPUT_LINES> lines)
        {
            var list = lines?.ToList();
            if (list == null || list.Count == 0)
                return 0;

            // Standart uzunlik (mm): StandardLengthMm > 0 bo'lsa, aks holda Length > 0 bo'lsa (null/0 emas).
            var material = list[0].PRODUCT;
            double standardLengthMm = 0;
            if (material != null)
            {
                if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                    standardLengthMm = material.StandardLengthMm.Value;
                else if (material.Length.HasValue && material.Length.Value > 0)
                    standardLengthMm = material.Length.Value;
            }

            // Jami uzunlik (mm) = Σ(kesim × dona) + Σ(chiqindi). Chiqindi WasteAmount (jami mm) dan,
            // bo'lmasa (standart - kesim) × dona dan hisoblanadi.
            decimal totalLengthMm = 0;
            foreach (var x in list)
            {
                var cutPartMm = (decimal)x.CutLengthMm * x.PiecesPerProduct;

                decimal wasteMm;
                if (x.WasteAmount.HasValue)
                    wasteMm = (decimal)x.WasteAmount.Value;
                else if (standardLengthMm > 0 && x.CutLengthMm > 0)
                    wasteMm = (decimal)Math.Max(0, standardLengthMm - x.CutLengthMm) * x.PiecesPerProduct;
                else
                    wasteMm = 0;

                totalLengthMm += cutPartMm + wasteMm;
            }

            if (standardLengthMm > 0 && totalLengthMm > 0)
                return Math.Ceiling(totalLengthMm / (decimal)standardLengthMm);

            // Standart uzunlik yo'q bo'lsa — eski xatti-harakat (donalar yig'indisi).
            return (decimal)list.Sum(x => x.PiecesPerProduct);
        }

        private static decimal ComputeNormWoodLineAmount(NORM_INPUT_LINES line)
        {
            var material = line.PRODUCT;
            var requiresCutting = material?.PRODUCT_CATEGORIES?.RequiresCutting ?? false;
            // StandardLengthMm bazada 0 (null emas) bo'lishi mumkin — bunda Length ishlatiladi (> 0 tekshiruvi bilan).
            double standardLengthMm = 0;
            if (material != null)
            {
                if (material.StandardLengthMm.HasValue && material.StandardLengthMm.Value > 0)
                    standardLengthMm = material.StandardLengthMm.Value;
                else if (material.Length.HasValue && material.Length.Value > 0)
                    standardLengthMm = material.Length.Value;
            }

            decimal consumption;
            if (line.Consumption.HasValue && line.Consumption.Value > 0)
            {
                consumption = NormInputLineConsumptionToDisplay(requiresCutting, line.CutLengthMm, line.Consumption);
            }
            else if (requiresCutting && line.CutLengthMm > 0)
            {
                consumption = (decimal)(line.CutLengthMm / 1000.0) * line.PiecesPerProduct;
            }
            else
            {
                consumption = line.PiecesPerProduct;
            }

            decimal waste;
            if (line.WasteAmount.HasValue)
            {
                waste = NormInputLineWasteToDisplay(line.WasteAmount);
            }
            else if (requiresCutting && standardLengthMm > 0 && line.CutLengthMm > 0)
            {
                var wastePerBar = Math.Max(0, standardLengthMm - line.CutLengthMm);
                waste = (decimal)(wastePerBar / 1000.0) * line.PiecesPerProduct;
            }
            else
            {
                waste = 0;
            }

            return consumption * line.Price + waste * line.Price;
        }

        private static decimal NormInputLineConsumptionToDisplay(bool requiresCutting, int cutLengthMm, double? consumptionDb)
        {
            if (!consumptionDb.HasValue || consumptionDb.Value <= 0)
                return 0;
            if (requiresCutting && cutLengthMm > 0)
                return (decimal)(consumptionDb.Value / 1000.0);
            return (decimal)consumptionDb.Value;
        }

        private static decimal NormInputLineWasteToDisplay(double? wasteAmountDb)
        {
            if (!wasteAmountDb.HasValue || wasteAmountDb.Value <= 0)
                return 0;
            return (decimal)(wasteAmountDb.Value / 1000.0);
        }

        private static bool IsWoodProductCategory(PRODUCT_CATEGORIES category)
        {
            if (category == null)
                return false;

            var rawGroup = category.RawMaterialGroup ?? string.Empty;
            var catName = category.Name ?? string.Empty;
            return rawGroup.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0
                || rawGroup.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0
                || catName.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0
                || catName.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
