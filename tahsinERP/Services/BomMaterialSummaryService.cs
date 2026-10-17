using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.Norm;
using tahsinERP.ViewModels.BOM;

namespace tahsinERP.Services
{
    /// <summary>
    /// BOM daraxti va materiallar jadvali (1 tayyor mahsulot uchun) hisob-kitobi.
    /// BOM Details va ishlab chiqarish sarfi bir xil manbadan foydalanadi.
    ///
    /// Samaradorlik (audit MES-P09):
    ///  - daraxt tugunma-tugun sinxron so'rovlar o'rniga daraja bo'yicha oldindan yuklanadi
    ///    (har BOM darajasi uchun 2 ta so'rov: faol headerlar + ularning qatorlari);
    ///  - rasmlar faqat BOM Details uchun, bitta so'rovda;
    ///  - NORM_INPUT_LINES (narx + yog'och ma'lumoti) bir marta o'qiladi;
    ///  - <see cref="GetMaterialSummariesPerUnitAsync"/> natijasi shu DbContext doirasida keshlanadi —
    ///    ishlab chiqarish kiritishda "tekshirish" va "sarf" bosqichlari bir xil hisobni qayta qilmaydi;
    ///  - sikl himoyasi: BOM halqasi (A → B → A) StackOverflow emas, shu tugunda to'xtaydi.
    /// </summary>
    public class BomMaterialSummaryService
    {
        private const int MaxDepth = 25;

        private static readonly ConditionalWeakTable<DBTHSNEntities, Dictionary<string, List<BomMaterialSummaryViewModel>>> SummaryCache =
            new ConditionalWeakTable<DBTHSNEntities, Dictionary<string, List<BomMaterialSummaryViewModel>>>();

        private readonly DBTHSNEntities _db;

        // ProductID → eng so'nggi faol BOM header ID (null — BOM yo'q)
        private readonly Dictionary<int, int?> _latestHeaderByProduct = new Dictionary<int, int?>();
        // BOM header ID → qatorlar
        private readonly Dictionary<int, List<BomLineRow>> _linesByHeader = new Dictionary<int, List<BomLineRow>>();
        // (ProductID|Month) → norma qatorlari
        private readonly Dictionary<string, List<NormLineRow>> _normLinesCache = new Dictionary<string, List<NormLineRow>>();

        public BomMaterialSummaryService(DBTHSNEntities db)
        {
            _db = db;
        }

        public async Task<List<BomMaterialSummaryViewModel>> GetMaterialSummariesPerUnitAsync(int productId, int? bomHeaderId = null, DateTime? asOfDate = null)
        {
            var cacheKey = productId + "|" + bomHeaderId + "|" + (asOfDate.HasValue ? asOfDate.Value.ToString("yyyy-MM") : "");
            var cache = SummaryCache.GetOrCreateValue(_db);
            List<BomMaterialSummaryViewModel> cached;
            lock (cache)
            {
                cache.TryGetValue(cacheKey, out cached);
            }
            if (cached != null)
            {
                return Clone(cached);
            }

            var result = await ComputeMaterialSummariesPerUnitAsync(productId, bomHeaderId, asOfDate);
            lock (cache)
            {
                cache[cacheKey] = result;
            }
            return Clone(result);
        }

        private async Task<List<BomMaterialSummaryViewModel>> ComputeMaterialSummariesPerUnitAsync(int productId, int? bomHeaderId, DateTime? asOfDate)
        {
            var bomHeader = await SelectActiveBomHeaderAsync(productId, bomHeaderId, asOfDate);
            if (bomHeader == null)
            {
                return new List<BomMaterialSummaryViewModel>();
            }

            var product = await _db.PRODUCTS.AsNoTracking()
                .Where(p => p.ID == productId && p.IsDeleted == false)
                .Select(p => new { p.ID, p.PNo, p.Name })
                .FirstOrDefaultAsync();
            if (product == null)
            {
                return new List<BomMaterialSummaryViewModel>();
            }

            var rootLines = LoadLinesForHeaders(new[] { bomHeader.ID })[bomHeader.ID];
            var normVersion = bomHeader.VersionNo ?? string.Empty;
            var rootNode = BuildTree(product.ID, product.PNo, product.Name, rootLines, includeImages: false);
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
                    .Where(bh => GetBomEffectiveMonth(bh.VersionNo, bh.CreatedAt) <= asOfMonth)
                    .OrderByDescending(bh => GetBomEffectiveMonth(bh.VersionNo, bh.CreatedAt))
                    .ThenByDescending(bh => bh.CreatedAt)
                    .FirstOrDefault();

                if (effective != null)
                {
                    return effective;
                }

                // Buyurtma sanasidan oldin hech qanday versiya bo'lmasa — eng eski (birinchi) versiyaga qaytamiz.
                return bomHeaders
                    .OrderBy(bh => GetBomEffectiveMonth(bh.VersionNo, bh.CreatedAt))
                    .ThenBy(bh => bh.CreatedAt)
                    .FirstOrDefault();
            }

            return PickLatestHeader(bomHeaders, bh => bh.VersionNo, bh => bh.CreatedAt, fallbackToCreatedAt: true);
        }

        /// <summary>
        /// Eng so'nggi BOM header: VersionNo (yyyy-MM) bo'yicha, teng bo'lsa CreatedAt bo'yicha.
        /// <paramref name="fallbackToCreatedAt"/> = true bo'lsa VersionNo'si bor header bo'lmaganda eng yangi CreatedAt olinadi.
        /// BOM Index, Details va pastki BOM'lar uchun yagona qoida (avval uch joyda nusxalangan, dublikat #18).
        /// </summary>
        public static T PickLatestHeader<T>(IEnumerable<T> headers, Func<T, string> versionNo, Func<T, DateTime?> createdAt,
            bool fallbackToCreatedAt) where T : class
        {
            var list = headers as IList<T> ?? headers.ToList();
            var withVersion = list
                .Where(h => !string.IsNullOrEmpty(versionNo(h)))
                .OrderByDescending(h => ParseVersionNoDate(versionNo(h)))
                .ThenByDescending(createdAt)
                .FirstOrDefault();
            if (withVersion != null || !fallbackToCreatedAt)
                return withVersion;

            return list.OrderByDescending(createdAt).FirstOrDefault();
        }

        /// <summary>
        /// BOM versiyasining amal qilish oyi: VersionNo (yyyy-MM) dan, bo'lmasa CreatedAt dan (oyning 1-kuni).
        /// </summary>
        private static DateTime GetBomEffectiveMonth(string versionNo, DateTime? createdAt)
        {
            var fromVersion = ParseVersionNoDate(versionNo);
            if (fromVersion > DateTime.MinValue)
            {
                return new DateTime(fromVersion.Year, fromVersion.Month, 1);
            }

            if (createdAt.HasValue)
            {
                var c = createdAt.Value;
                return new DateTime(c.Year, c.Month, 1);
            }

            return DateTime.MinValue;
        }

        /// <summary>BOM Details uchun daraxt (berilgan root qatorlari bilan).</summary>
        public BomTreeNodeViewModel BuildBomTree(int parentProductId, List<BOM_LINES> bomLines, PRODUCT parentProduct, bool includeImages = true)
        {
            var rootLines = (bomLines ?? new List<BOM_LINES>())
                .Select(bl => new BomLineRow
                {
                    ComponentID = bl.ComponentID,
                    Qty = bl.Qty,
                    UnitID = bl.UnitID,
                    UnitShortName = bl.UNIT?.ShortName,
                    ComponentPNo = bl.PRODUCT?.PNo,
                    ComponentName = bl.PRODUCT?.Name,
                    ComponentIsDeleted = bl.PRODUCT == null || bl.PRODUCT.IsDeleted,
                    ComponentUnitID = bl.PRODUCT?.UnitID ?? 0
                })
                .ToList();
            return BuildTree(parentProduct.ID, parentProduct.PNo, parentProduct.Name, rootLines, includeImages);
        }

        private BomTreeNodeViewModel BuildTree(int rootId, string rootPNo, string rootName, List<BomLineRow> rootLines, bool includeImages)
        {
            PreloadClosure(rootLines);

            var root = new BomTreeNodeViewModel
            {
                ProductId = rootId,
                ProductPno = rootPNo,
                ProductName = rootName,
                Quantity = 1,
                Children = new List<BomTreeNodeViewModel>()
            };

            var path = new HashSet<int> { rootId };
            root.Children = BuildChildren(rootLines, path, 1);

            if (includeImages)
            {
                AttachImages(root);
            }

            return root;
        }

        private List<BomTreeNodeViewModel> BuildChildren(List<BomLineRow> lines, HashSet<int> path, int depth)
        {
            var children = new List<BomTreeNodeViewModel>();
            foreach (var line in lines)
            {
                if (!line.ComponentID.HasValue || line.ComponentIsDeleted)
                {
                    continue;
                }

                var childId = line.ComponentID.Value;
                var childNode = new BomTreeNodeViewModel
                {
                    ProductId = childId,
                    ProductPno = line.ComponentPNo,
                    ProductName = line.ComponentName,
                    Quantity = line.Qty ?? 0,
                    UnitShortname = line.UnitShortName ?? string.Empty,
                    UnitId = line.UnitID ?? (line.ComponentUnitID > 0 ? line.ComponentUnitID : 0),
                    Children = new List<BomTreeNodeViewModel>()
                };

                // Sikl himoyasi: tugun yo'lda allaqachon bor bo'lsa (A → B → A) yoki chuqurlik juda katta bo'lsa — yoymaymiz
                int? childHeaderId;
                if (depth < MaxDepth
                    && !path.Contains(childId)
                    && _latestHeaderByProduct.TryGetValue(childId, out childHeaderId)
                    && childHeaderId.HasValue)
                {
                    path.Add(childId);
                    childNode.Children = BuildChildren(_linesByHeader[childHeaderId.Value], path, depth + 1);
                    path.Remove(childId);
                }

                children.Add(childNode);
            }

            return children;
        }

        /// <summary>
        /// Root qatorlaridagi komponentlardan boshlab, barcha pastki BOM'larni daraja bo'yicha yuklaydi.
        /// </summary>
        private void PreloadClosure(List<BomLineRow> rootLines)
        {
            var frontier = rootLines
                .Where(l => l.ComponentID.HasValue && !l.ComponentIsDeleted)
                .Select(l => l.ComponentID.Value)
                .Distinct()
                .Where(id => !_latestHeaderByProduct.ContainsKey(id))
                .ToList();

            int level = 0;
            while (frontier.Count > 0 && level++ < MaxDepth)
            {
                var headers = _db.BOM_HEADERS.AsNoTracking()
                    .Where(bh => bh.ProductID.HasValue && frontier.Contains(bh.ProductID.Value) && bh.IsActive == true)
                    .Select(bh => new { bh.ID, ProductID = bh.ProductID.Value, bh.VersionNo, bh.CreatedAt })
                    .ToList();

                var newHeaderIds = new List<int>();
                foreach (var productId in frontier)
                {
                    // Pastki BOM: VersionNo'si bor eng so'nggi faol header (avvalgi SelectLatestChildBomHeader bilan bir xil)
                    var latest = PickLatestHeader(headers.Where(h => h.ProductID == productId),
                        h => h.VersionNo, h => h.CreatedAt, fallbackToCreatedAt: false);
                    _latestHeaderByProduct[productId] = latest?.ID;
                    if (latest != null && !_linesByHeader.ContainsKey(latest.ID))
                    {
                        newHeaderIds.Add(latest.ID);
                    }
                }

                if (newHeaderIds.Count == 0)
                {
                    break;
                }

                var loaded = LoadLinesForHeaders(newHeaderIds);
                frontier = loaded.Values
                    .SelectMany(ls => ls)
                    .Where(l => l.ComponentID.HasValue && !l.ComponentIsDeleted)
                    .Select(l => l.ComponentID.Value)
                    .Distinct()
                    .Where(id => !_latestHeaderByProduct.ContainsKey(id))
                    .ToList();
            }
        }

        private Dictionary<int, List<BomLineRow>> LoadLinesForHeaders(IEnumerable<int> headerIds)
        {
            var ids = headerIds.Distinct().ToList();
            var rows = _db.BOM_LINES.AsNoTracking()
                .Where(bl => bl.BOMID.HasValue && ids.Contains(bl.BOMID.Value))
                .OrderBy(bl => bl.ID)
                .Select(bl => new BomLineRow
                {
                    BomID = bl.BOMID.Value,
                    ComponentID = bl.ComponentID,
                    Qty = bl.Qty,
                    UnitID = bl.UnitID,
                    UnitShortName = bl.UNIT.ShortName,
                    ComponentPNo = bl.PRODUCT.PNo,
                    ComponentName = bl.PRODUCT.Name,
                    ComponentIsDeleted = bl.PRODUCT == null || bl.PRODUCT.IsDeleted,
                    ComponentUnitID = bl.PRODUCT == null ? 0 : bl.PRODUCT.UnitID
                })
                .ToList();

            foreach (var id in ids)
            {
                _linesByHeader[id] = rows.Where(r => r.BomID == id).ToList();
            }
            return ids.ToDictionary(id => id, id => _linesByHeader[id]);
        }

        private void AttachImages(BomTreeNodeViewModel root)
        {
            var nodes = new List<BomTreeNodeViewModel>();
            CollectNodes(root, nodes);
            var productIds = nodes.Select(n => n.ProductId).Distinct().ToList();

            // Rasm baytlari yuklanmaydi — faqat qaysi mahsulotda rasm borligi; tugun rasmni URL orqali oladi
            // (keshlanadi, kichraytirilgan; avval har tugunga base64 data-URI, MES-P23)
            var images = _db.PRODUCTIMAGES.AsNoTracking()
                .Where(pi => productIds.Contains(pi.ProdID) && pi.IsDeleted != true && pi.Image != null)
                .GroupBy(pi => pi.ProdID)
                .Select(g => new { ProdID = g.Key, ImageID = g.Max(pi => pi.ID) })
                .ToList()
                .ToDictionary(x => x.ProdID, x => tahsinERP.Controllers.ProductImageController.UrlFor(x.ProdID, x.ImageID, 320));

            foreach (var node in nodes)
            {
                string url;
                if (images.TryGetValue(node.ProductId, out url))
                {
                    node.ImageUrl = url;
                }
            }
        }

        private static void CollectNodes(BomTreeNodeViewModel node, List<BomTreeNodeViewModel> sink)
        {
            if (node == null) return;
            sink.Add(node);
            if (node.Children == null) return;
            foreach (var child in node.Children)
            {
                CollectNodes(child, sink);
            }
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

        /// <summary>BOM VersionNo ("yyyy-MM" yoki "yyyy-MM-...") → oyning 1-kuni; tushunarsiz bo'lsa DateTime.MinValue.</summary>
        public static DateTime ParseVersionNoDate(string versionNo)
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

        private static void FlattenLeafMaterials(
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

        private List<NormLineRow> LoadNormLines(int rootProductId, string normVersion)
        {
            if (string.IsNullOrWhiteSpace(normVersion))
            {
                return new List<NormLineRow>();
            }

            var key = rootProductId + "|" + normVersion;
            List<NormLineRow> lines;
            if (_normLinesCache.TryGetValue(key, out lines))
            {
                return lines;
            }

            lines = _db.NORM_INPUT_LINES.AsNoTracking()
                .Where(n => n.ProductID == rootProductId && n.Month == normVersion)
                .Select(n => new NormLineRow
                {
                    MaterialID = n.MaterialID,
                    CutLengthMm = n.CutLengthMm,
                    PiecesPerProduct = n.PiecesPerProduct,
                    Consumption = n.Consumption,
                    WasteAmount = n.WasteAmount,
                    Price = n.Price,
                    CreatedDT = n.CreatedDT,
                    StandardLengthMm = n.PRODUCT.StandardLengthMm,
                    Length = n.PRODUCT.Length,
                    RequiresCutting = n.PRODUCT.PRODUCT_CATEGORIES != null && n.PRODUCT.PRODUCT_CATEGORIES.RequiresCutting,
                    RawMaterialGroup = n.PRODUCT.PRODUCT_CATEGORIES.RawMaterialGroup,
                    CategoryName = n.PRODUCT.PRODUCT_CATEGORIES.Name,
                    HasCategory = n.PRODUCT.PRODUCT_CATEGORIES != null
                })
                .ToList();

            _normLinesCache[key] = lines;
            return lines;
        }

        private Dictionary<int, decimal> LoadNormPricesByMaterial(int rootProductId, string normVersion, List<int> materialIds)
        {
            if (materialIds == null || !materialIds.Any() || string.IsNullOrWhiteSpace(normVersion))
            {
                return new Dictionary<int, decimal>();
            }

            var idSet = new HashSet<int>(materialIds);
            return LoadNormLines(rootProductId, normVersion)
                .Where(n => idSet.Contains(n.MaterialID))
                .OrderByDescending(n => n.CreatedDT)
                .GroupBy(n => n.MaterialID)
                .ToDictionary(g => g.Key, g => g.First().Price);
        }

        private Dictionary<int, (decimal Quantity, decimal TotalAmount)> LoadNormWoodDataByMaterial(int rootProductId, string normVersion)
        {
            var lines = LoadNormLines(rootProductId, normVersion)
                .Where(n => n.HasCategory && NormLineMath.IsWoodCategory(n.RawMaterialGroup, n.CategoryName))
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
                        Quantity: ComputeWoodBarQuantity(g.ToList()),
                        TotalAmount: g.Sum(ComputeNormWoodLineAmount)));
        }

        /// <summary>
        /// Yog'och (kesiladigan) material uchun BOM dona soni — <see cref="NormLineMath.BarQuantity"/>.
        /// WasteAmount bazada qator bo'yicha JAMI chiqindi (mm) sifatida saqlanadi.
        /// </summary>
        private static decimal ComputeWoodBarQuantity(List<NormLineRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            var standardLengthMm = NormLineMath.EffectiveStandardLengthMm(list[0].StandardLengthMm, list[0].Length);
            return NormLineMath.BarQuantity(
                list.Select(x => new CutPiece(x.CutLengthMm, x.PiecesPerProduct, x.WasteAmount.HasValue ? (decimal?)(decimal)x.WasteAmount.Value : null)),
                standardLengthMm);
        }

        private static decimal ComputeNormWoodLineAmount(NormLineRow line)
        {
            var standardLengthMm = NormLineMath.EffectiveStandardLengthMm(line.StandardLengthMm, line.Length);
            var consumption = NormLineMath.LineConsumption(line.RequiresCutting, line.CutLengthMm, line.PiecesPerProduct, line.Consumption);
            var waste = NormLineMath.LineWaste(line.RequiresCutting, standardLengthMm, line.CutLengthMm, line.PiecesPerProduct, line.WasteAmount);
            return consumption * line.Price + waste * line.Price;
        }

        private static List<BomMaterialSummaryViewModel> Clone(List<BomMaterialSummaryViewModel> source)
        {
            return source.Select(s => new BomMaterialSummaryViewModel
            {
                MaterialId = s.MaterialId,
                MaterialPno = s.MaterialPno,
                MaterialName = s.MaterialName,
                UnitShortname = s.UnitShortname,
                UnitId = s.UnitId,
                TotalQuantity = s.TotalQuantity,
                UnitPrice = s.UnitPrice,
                TotalAmount = s.TotalAmount
            }).ToList();
        }

        private sealed class BomLineRow
        {
            public int BomID { get; set; }
            public int? ComponentID { get; set; }
            public decimal? Qty { get; set; }
            public int? UnitID { get; set; }
            public string UnitShortName { get; set; }
            public string ComponentPNo { get; set; }
            public string ComponentName { get; set; }
            public bool ComponentIsDeleted { get; set; }
            public int ComponentUnitID { get; set; }
        }

        private sealed class NormLineRow
        {
            public int MaterialID { get; set; }
            public int CutLengthMm { get; set; }
            public int PiecesPerProduct { get; set; }
            public double? Consumption { get; set; }
            public double? WasteAmount { get; set; }
            public decimal Price { get; set; }
            public DateTime CreatedDT { get; set; }
            public double? StandardLengthMm { get; set; }
            public double? Length { get; set; }
            public bool RequiresCutting { get; set; }
            public string RawMaterialGroup { get; set; }
            public string CategoryName { get; set; }
            public bool HasCategory { get; set; }
        }
    }
}
