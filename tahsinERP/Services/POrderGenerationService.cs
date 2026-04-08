using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Sotuv buyurtmalari asosida BOM portlash orqali xarid buyurtmasi uchun xomashyo ehtiyoji.
    /// Bir nechta sotuv buyurtmasi / turli tayyor mahsulotlar: xuddi shu xomashyo ComponentID bo'yicha yig'iladi.
    /// BOM: faol versiyalar ichidan eng yangi (VersionNo sana + CreatedAt).
    /// Tayyor mahsulot (FG) komponenti: agar uning o'zining BOM'i bo'lsa, rekursiv portlanadi.
    /// </summary>
    public class POrderGenerationService
    {
        private const int MaxBomDepth = 10;

        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;

        public POrderGenerationService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// Generates raw material requirements from Sales Orders (aggregated by material).
        /// </summary>
        public async Task<List<RawMaterialRequirement>> GenerateRequirementsFromSalesOrdersAsync(int[] salesOrderIds)
        {
            var normalizedSalesOrderIds = (salesOrderIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (normalizedSalesOrderIds.Length == 0)
            {
                throw new ArgumentException("Sales order IDs cannot be empty.");
            }

            var salesOrders = await _db.S_ORDERS
                .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT.PRODUCT_CATEGORIES))
                .Where(o => normalizedSalesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                .OrderBy(o => o.ID)
                .ToListAsync();

            var componentRequirements = new Dictionary<int, RawMaterialRequirement>();
            var bomCache = new Dictionary<int, BOM_HEADERS>();
            var productCache = new Dictionary<int, PRODUCT>();

            foreach (var sOrder in salesOrders)
            {
                var lines = (sOrder.S_ORDER_PRODUCTS ?? Enumerable.Empty<S_ORDER_PRODUCTS>())
                    .OrderBy(l => l.ID)
                    .ToList();

                foreach (var sOrderProduct in lines)
                {
                    if (sOrderProduct.PRODUCT == null)
                    {
                        continue;
                    }

                    decimal lineQty = (decimal)sOrderProduct.Amount;
                    if (lineQty <= 0)
                    {
                        continue;
                    }

                    var orderNo = string.IsNullOrWhiteSpace(sOrder.OrderNo)
                        ? ("Sotuv №" + sOrder.ID)
                        : sOrder.OrderNo.Trim();

                    await ExplodeProductBomAsync(
                        sOrderProduct.PRODUCT.ID,
                        lineQty,
                        componentRequirements,
                        new HashSet<int>(),
                        0,
                        orderNo,
                        sOrderProduct.PRODUCT.PNo ?? "",
                        sOrderProduct.PRODUCT.Name ?? "",
                        bomCache,
                        productCache);
                }
            }

            return componentRequirements.Values
                .OrderBy(x => x.ComponentName ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// BOM ni portlash: varaqali xomashyolarni jamlash; FG bo'lsa va uning BOM'i bo'lsa — ichkariga kirish.
        /// </summary>
        private async Task ExplodeProductBomAsync(
            int productId,
            decimal demandQty,
            Dictionary<int, RawMaterialRequirement> componentRequirements,
            HashSet<int> recursionStack,
            int depth,
            string salesOrderNo,
            string bomOwnerPNo,
            string bomOwnerName,
            Dictionary<int, BOM_HEADERS> bomCache,
            Dictionary<int, PRODUCT> productCache)
        {
            if (demandQty <= 0 || depth > MaxBomDepth)
            {
                return;
            }

            if (recursionStack.Contains(productId))
            {
                return;
            }

            recursionStack.Add(productId);
            try
            {
                var bomHeader = await GetLatestActiveBOMCachedAsync(productId, bomCache);
                if (bomHeader?.BOM_LINES == null || !bomHeader.BOM_LINES.Any())
                {
                    return;
                }
                var bomVersionText = FormatBomVersionForUz(bomHeader);

                foreach (var bomLine in bomHeader.BOM_LINES.OrderBy(l => l.ID))
                {
                    if (!bomLine.ComponentID.HasValue || !bomLine.Qty.HasValue || bomLine.Qty.Value <= 0)
                    {
                        continue;
                    }

                    int componentId = bomLine.ComponentID.Value;
                    decimal scaledQty = bomLine.Qty.Value * demandQty;

                    var component = await GetProductForExplosionAsync(componentId, productCache);

                    if (component == null)
                    {
                        continue;
                    }

                    bool isFinishedGood = component.PRODUCT_CATEGORIES?.IsFinishedGood == true;
                    if (isFinishedGood)
                    {
                        var subBom = await GetLatestActiveBOMCachedAsync(componentId, bomCache);
                        if (subBom != null && subBom.BOM_LINES != null && subBom.BOM_LINES.Any())
                        {
                            await ExplodeProductBomAsync(
                                componentId,
                                scaledQty,
                                componentRequirements,
                                recursionStack,
                                depth + 1,
                                salesOrderNo,
                                component.PNo ?? "",
                                component.Name ?? "",
                                bomCache,
                                productCache);
                        }
                        continue;
                    }

                    bool isPurchasable = component.PRODUCT_CATEGORIES == null ||
                                         component.PRODUCT_CATEGORIES.IsFinishedGood == false;

                    if (!isPurchasable)
                    {
                        continue;
                    }

                    await AddPurchasedLeafAsync(
                        component,
                        bomLine,
                        scaledQty,
                        demandQty,
                        componentRequirements,
                        salesOrderNo,
                        bomOwnerPNo,
                        bomOwnerName,
                        bomVersionText);
                }
            }
            finally
            {
                recursionStack.Remove(productId);
            }
        }

        private async Task<BOM_HEADERS> GetLatestActiveBOMCachedAsync(int productId, Dictionary<int, BOM_HEADERS> bomCache)
        {
            if (bomCache.TryGetValue(productId, out var cached))
            {
                return cached;
            }

            var bom = await GetLatestActiveBOMAsync(productId);
            bomCache[productId] = bom;
            return bom;
        }

        private async Task<PRODUCT> GetProductForExplosionAsync(int productId, Dictionary<int, PRODUCT> productCache)
        {
            if (productCache.TryGetValue(productId, out var cached))
            {
                return cached;
            }

            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(p => p.ID == productId && p.IsDeleted != true);

            productCache[productId] = product;
            return product;
        }

        /// <summary>
        /// Buy# = sotuv buyurtma raqami (raqamlar); Mx = xomashyo PNo; Ts = tayyor mahsulot (BOM egasi) PNo;
        /// S = soni (kerak miqdor), N = norma (BOM 1 dona uchun), S*N=R (BOM qatori), F = yakun (xarid birligi).
        /// Qator generatsiya paytida 50 belgidan oshmaydi (uzun matnning boshini DB ga yozib kesish emas).
        /// </summary>
        private static string BuildCompactPriorityLine50(
            string salesOrderRef,
            string materialPNo,
            string fgPNo,
            string bomVersionText,
            decimal norma,
            decimal soni,
            decimal rBom,
            decimal finalQty,
            string finalUnitName)
        {
            var buy = ExtractBuyDigits(salesOrderRef);
            var u = AbbrevUnit(finalUnitName);

            for (var mxLen = 10; mxLen >= 3; mxLen--)
            {
                var mx = SanitizePNo(materialPNo, mxLen);
                var ts = SanitizePNo(fgPNo, Math.Min(8, mxLen + 2));
                var vRaw = string.IsNullOrWhiteSpace(bomVersionText) ? "" : bomVersionText.Replace("—", "-").Trim();
                var v = string.IsNullOrEmpty(vRaw) ? "" : SanitizePNo(vRaw, Math.Min(8, mxLen + 1));

                for (var numCap = 12; numCap >= 3; numCap--)
                {
                    var S = FormatQtyForLine(soni, numCap);
                    var N = FormatQtyForLine(norma, numCap);
                    var R = FormatQtyForLine(rBom, numCap);
                    var F = FormatQtyForLine(finalQty, numCap);

                    var candidates = new[]
                    {
                        $"Buy#{buy};Mx:{mx};Ts:{ts};v:{v};S:{S};N:{N};{S}*{N}={R};F:{F}{u}",
                        $"Buy#{buy};Mx:{mx};Ts:{ts};{S}*{N}={R};F:{F}{u}",
                        $"Buy#{buy};Mx:{mx};Ts:{ts};v:{v};{S}*{N}={R}",
                        $"Buy#{buy};Mx:{mx};{S}*{N}={R};F:{F}{u}",
                        $"Buy#{buy};Mx:{mx};{S}*{N}={R}",
                        $"Mx:{mx};Ts:{ts};v:{v};{S}*{N}={R};F:{F}{u}",
                        $"Mx:{mx};Ts:{ts};{S}*{N}={R};F:{F}{u}",
                        $"Mx:{mx};{S}*{N}={R};F:{F}{u}",
                        $"Mx:{mx};{S}*{N}={R}",
                        $"Mx:{mx};F:{F}{u}",
                        $"B:{buy};M:{mx};{S}*{N}={R};F:{F}{u}",
                        $"M:{mx};{S}*{N}={R};F:{F}{u}"
                    };

                    foreach (var c in candidates)
                    {
                        if (c.Length <= 50)
                        {
                            return c;
                        }
                    }
                }
            }

            var mxMin = SanitizePNo(materialPNo, 3);
            var fMin = FormatQtyForLine(finalQty, 8);
            var tail = $"M:{mxMin};F:{fMin}{u}";
            return tail.Length <= 50 ? tail : $"M:{mxMin};F:{fMin}";
        }

        /// <summary> Raqamni qatorga joylash uchun qisqartiradi; uzun tushuntirish o‘rniga kompakt ko‘rinish. </summary>
        private static string FormatQtyForLine(decimal d, int maxLen)
        {
            if (maxLen < 1)
            {
                return "?";
            }

            var s = FormatQtyShort(d);
            if (s.Length <= maxLen)
            {
                return s;
            }

            s = d.ToString("0.##", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            if (s.Length <= maxLen)
            {
                return s;
            }

            s = d.ToString("G2", CultureInfo.InvariantCulture);
            if (s.Length <= maxLen)
            {
                return s;
            }

            s = ((double)d).ToString("E1", CultureInfo.InvariantCulture);
            return s.Length <= maxLen ? s : s.Substring(0, maxLen);
        }

        private static string ExtractBuyDigits(string orderRef)
        {
            if (string.IsNullOrWhiteSpace(orderRef))
            {
                return "?";
            }

            var digits = new string(orderRef.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                var t = orderRef.Trim();
                return t.Length <= 6 ? t : t.Substring(0, 6);
            }

            return digits.Length <= 6 ? digits : digits.Substring(digits.Length - 6);
        }

        private static string SanitizePNo(string pno, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(pno))
            {
                return "?";
            }

            var t = pno.Trim().Replace(" ", "");
            return t.Length <= maxLen ? t : t.Substring(0, maxLen);
        }

        private static string FormatQtyShort(decimal d)
        {
            var x = (double)d;
            if (Math.Abs(x - Math.Round(x, 4)) < 0.0000001)
            {
                return ((long)Math.Round(x)).ToString(CultureInfo.InvariantCulture);
            }

            var s = d.ToString("0.####", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            return s.Length > 12 ? d.ToString("G4", CultureInfo.InvariantCulture) : s;
        }

        private static string AbbrevUnit(string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
            {
                return "";
            }

            var u = unitName.Trim();
            return u.Length <= 3 ? u : u.Substring(0, 3);
        }

        private static string CombineCompactPriority50(string existing, string addition)
        {
            if (string.IsNullOrWhiteSpace(addition))
            {
                return existing ?? "";
            }

            if (string.IsNullOrWhiteSpace(existing))
            {
                return addition.Length <= 50 ? addition : BuildCompactFromAdditionOnly(addition);
            }

            var a = existing.Trim();
            var b = addition.Trim();
            if (a == b)
            {
                return a;
            }

            var mergedBuy = TryMergeSameRestAfterBuy(a, b);
            if (!string.IsNullOrEmpty(mergedBuy) && mergedBuy.Length <= 50)
            {
                return mergedBuy;
            }

            var x = a + "|" + b;
            if (x.Length <= 50)
            {
                return x;
            }

            if (!string.IsNullOrEmpty(mergedBuy))
            {
                return mergedBuy.Length <= 50 ? mergedBuy : PickShorterSingleLine(a, b);
            }

            return PickShorterSingleLine(a, b);
        }

        /// <summary> Bir xil qolgan qism bo‘lsa, faqat Buy# raqamlarini birlashtiradi (matn boshini kesmaydi). </summary>
        private static string TryMergeSameRestAfterBuy(string a, string b)
        {
            var ia = a.IndexOf(';');
            var ib = b.IndexOf(';');
            if (ia < 0 || ib < 0)
            {
                return null;
            }

            var restA = a.Substring(ia + 1);
            var restB = b.Substring(ib + 1);
            if (!string.Equals(restA, restB, StringComparison.Ordinal))
            {
                return null;
            }

            var buyA = ExtractBuyToken(a.Substring(0, ia));
            var buyB = ExtractBuyToken(b.Substring(0, ib));
            if (buyA == null || buyB == null)
            {
                return null;
            }

            if (buyA == buyB)
            {
                return a;
            }

            return $"Buy#{buyA},{buyB};{restA}";
        }

        private static string ExtractBuyToken(string buySegment)
        {
            var t = buySegment.Trim();
            if (t.StartsWith("Buy#", StringComparison.OrdinalIgnoreCase))
            {
                return t.Substring(4).Trim();
            }

            if (t.StartsWith("B:", StringComparison.OrdinalIgnoreCase))
            {
                return t.Substring(2).Trim();
            }

            return null;
        }

        private static string PickShorterSingleLine(string a, string b)
        {
            if (a.Length <= 50 && b.Length <= 50)
            {
                return a.Length <= b.Length ? a : b;
            }

            if (a.Length <= 50)
            {
                return a;
            }

            if (b.Length <= 50)
            {
                return b;
            }

            var ca = BuildCompactFromAdditionOnly(a);
            var cb = BuildCompactFromAdditionOnly(b);
            if (ca.Length <= 50 && cb.Length <= 50)
            {
                return ca.Length >= cb.Length ? ca : cb;
            }

            if (ca.Length <= 50)
            {
                return ca;
            }

            if (cb.Length <= 50)
            {
                return cb;
            }

            return ca.Length <= cb.Length ? ca : cb;
        }

        /// <summary> Eski/uzun qatorni Mx/F bo‘yicha qayta jamlaydi; umumiy matn boshini 50 belgi qilib kesmaydi. </summary>
        private static string BuildCompactFromAdditionOnly(string addition)
        {
            var mx = TryExtractMx(addition);
            var fv = TryExtractF(addition);
            if (!string.IsNullOrEmpty(mx) && !string.IsNullOrEmpty(fv))
            {
                var u = AbbrevUnit(StripLeadingNumber(fv));
                var numPart = ExtractLeadingDecimal(fv);
                if (!string.IsNullOrEmpty(numPart) && decimal.TryParse(numPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var fd))
                {
                    var f = FormatQtyForLine(fd, 10);
                    var tail = $"M:{SanitizePNo(mx, 10)};F:{f}{u}";
                    if (tail.Length <= 50)
                    {
                        return tail;
                    }

                    tail = $"M:{SanitizePNo(mx, 4)};F:{FormatQtyForLine(fd, 6)}{u}";
                    if (tail.Length <= 50)
                    {
                        return tail;
                    }
                }
            }

            return "M:?;F:?";
        }

        private static string StripLeadingNumber(string fv)
        {
            var i = 0;
            while (i < fv.Length && (char.IsDigit(fv[i]) || fv[i] == '.' || fv[i] == '-' || fv[i] == 'e' || fv[i] == 'E' || fv[i] == '+'))
            {
                i++;
            }

            return i < fv.Length ? fv.Substring(i).Trim() : "";
        }

        private static string ExtractLeadingDecimal(string fv)
        {
            var i = 0;
            while (i < fv.Length && (char.IsDigit(fv[i]) || fv[i] == '.' || fv[i] == '-' || fv[i] == 'e' || fv[i] == 'E' || fv[i] == '+'))
            {
                i++;
            }

            return i > 0 ? fv.Substring(0, i) : "";
        }

        private static string TryExtractMx(string s)
        {
            var idx = s.IndexOf("Mx:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                idx = s.IndexOf("M:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    return null;
                }

                return ExtractUntilSemicolon(s, idx + 2);
            }

            return ExtractUntilSemicolon(s, idx + 3);
        }

        private static string ExtractUntilSemicolon(string s, int start)
        {
            var end = s.IndexOf(';', start);
            return end < 0 ? s.Substring(start).Trim() : s.Substring(start, end - start).Trim();
        }

        private static string TryExtractF(string s)
        {
            var idx = s.IndexOf("F:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return null;
            }

            var tail = s.Substring(idx + 2);
            var end = tail.IndexOfAny(new[] { ';', '|' });
            return end < 0 ? tail.Trim() : tail.Substring(0, end).Trim();
        }

        private static string FormatBomVersionForUz(BOM_HEADERS bomHeader)
        {
            if (bomHeader == null)
            {
                return "noma'lum";
            }

            if (!string.IsNullOrWhiteSpace(bomHeader.VersionNo))
            {
                var raw = bomHeader.VersionNo.Trim();
                var parts = raw.Split('-');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out var y) &&
                    int.TryParse(parts[1], out var m))
                {
                    return y + "-" + m;
                }

                return raw;
            }

            var dt = bomHeader.CreatedAt ?? DateTime.Now;
            return dt.Year + "-" + dt.Month;
        }

        private async Task<string> GetUnitShortNameAsync(int unitId)
        {
            if (unitId <= 0)
            {
                return "?";
            }

            var u = await _db.UNITS.AsNoTracking().FirstOrDefaultAsync(x => x.ID == unitId);
            return u?.ShortName ?? u?.UnitName ?? ("ID " + unitId);
        }

        /// <summary>
        /// Konvertatsiya xatosi bo'lsa — mahsulot kartasidagi asosiy birlik (bo'sh bo'lsa 1 = dona).
        /// </summary>
        private static int GetProductDefaultUnitId(PRODUCT component)
        {
            if (component != null && component.UnitID > 0)
            {
                return component.UnitID;
            }

            return 1;
        }

        /// <summary>
        /// Faqat yog'och uchun BOM metr → dona (rulon) aylantirish. Selofan/karton RequiresCutting bo'lsa ham
        /// bu yerda yog'och emas — metr norma/BOM bo'yicha saqlanadi (PRODUCT.Length bilan bo'linmaydi).
        /// </summary>
        private static bool IsWoodProductCategory(PRODUCT component)
        {
            var c = component?.PRODUCT_CATEGORIES;
            if (c == null)
            {
                return false;
            }

            if (c.RawMaterialGroup != null)
            {
                var g = c.RawMaterialGroup;
                if (g.IndexOf("Yo'goch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    g.IndexOf("Yog'och", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    g.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    g.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(c.Name))
            {
                var n = c.Name;
                if (n.IndexOf("Yo'goch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Yog'och", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("yog'och", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("yo'goch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task AddPurchasedLeafAsync(
            PRODUCT component,
            BOM_LINES bomLine,
            decimal requiredQty,
            decimal parentProductQtyForExplanation,
            Dictionary<int, RawMaterialRequirement> componentRequirements,
            string salesOrderNo,
            string bomOwnerPNo,
            string bomOwnerName,
            string bomVersionText)
        {
            _ = bomOwnerName;
            int componentId = component.ID;
            decimal bomQtyPerUnit = bomLine.Qty ?? 0;
            int bomUnitId = bomLine.UnitID ?? component.UnitID;

            var orderLine = string.IsNullOrWhiteSpace(salesOrderNo) ? "—" : salesOrderNo.Trim();
            var fgPnRaw = (bomOwnerPNo ?? "").Trim();

            bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(componentId);
            bool requiresCutting = await _conversionService.RequiresCuttingAsync(componentId);

            int productDefaultUnitId = GetProductDefaultUnitId(component);

            decimal finalQuantity = requiredQty;
            int finalUnitId = bomUnitId;
            var skipStockConversion = false;

            // BOM birligi metr (5) bo'lmaganda: metiz/shurup — dona miqdorini kg ga (RequiresCutting + metr bloki bundan oldin
            // ishga tushib, kg konvertatsiyasini "yeb qo'yardi").
            bool bomQtyInMeters = bomUnitId == 5;

            // 1) Metiz va h.m.: BOM dona/kg boshqa — Weight bo'yicha kg (birinchi, chunki yog'och/selofan metr sharti bilan to'qnashmasin)
            if (calculateByWeight && !bomQtyInMeters)
            {
                try
                {
                    finalQuantity = await _conversionService.ConvertPieceToKgAsync(componentId, requiredQty);
                    var kgUnitId = await _conversionService.FindKgUnitIdAsync();
                    finalUnitId = kgUnitId ?? (component.UnitID > 0 ? component.UnitID : 2);
                    skipStockConversion = true;
                }
                catch (Exception)
                {
                    finalQuantity = requiredQty;
                    finalUnitId = productDefaultUnitId;
                }
            }
            // 2) Yog'och: BOM metr → dona (rulon). Selofan/yelim/karton: metrni saqlash.
            else if (requiresCutting && bomQtyInMeters)
            {
                if (IsWoodProductCategory(component))
                {
                    try
                    {
                        finalQuantity = await _conversionService.ConvertMeterToPieceAsync(componentId, requiredQty);
                        finalUnitId = await _conversionService.FindDonaUnitIdAsync() ?? 1;
                        skipStockConversion = true;
                    }
                    catch (Exception)
                    {
                        finalQuantity = requiredQty;
                        finalUnitId = productDefaultUnitId;
                    }
                }
                else
                {
                    finalQuantity = requiredQty;
                    finalUnitId = bomUnitId;
                    skipStockConversion = true;
                }
            }

            if (!skipStockConversion)
            {
                int stockUnitId = component.UnitID > 0 ? component.UnitID : finalUnitId;
                if (stockUnitId > 0 && finalUnitId > 0 && stockUnitId != finalUnitId)
                {
                    var qtyBeforeStock = finalQuantity;
                    try
                    {
                        finalQuantity = await _conversionService.ConvertToUnitAsync(
                            componentId,
                            finalQuantity,
                            finalUnitId,
                            stockUnitId);
                        finalUnitId = stockUnitId;
                    }
                    catch (Exception)
                    {
                        finalQuantity = qtyBeforeStock;
                        finalUnitId = productDefaultUnitId;
                    }
                }
            }

            if (componentRequirements.ContainsKey(componentId))
            {
                var existing = componentRequirements[componentId];
                if (existing.UnitID != finalUnitId)
                {
                    try
                    {
                        var merged = await _conversionService.ConvertToUnitAsync(
                            componentId,
                            finalQuantity,
                            finalUnitId,
                            existing.UnitID);
                        finalQuantity = merged;
                        finalUnitId = existing.UnitID;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            var merged = await _conversionService.ConvertToUnitAsync(
                                componentId,
                                requiredQty,
                                productDefaultUnitId,
                                existing.UnitID);
                            finalQuantity = merged;
                            finalUnitId = existing.UnitID;
                        }
                        catch (Exception)
                        {
                            if (existing.UnitID != finalUnitId)
                            {
                                finalUnitId = existing.UnitID;
                                finalQuantity = requiredQty;
                            }
                        }
                    }
                }

                var finalUnitName = await GetUnitShortNameAsync(finalUnitId);
                var compactLine = BuildCompactPriorityLine50(
                    orderLine,
                    component.PNo,
                    fgPnRaw,
                    bomVersionText,
                    bomQtyPerUnit,
                    parentProductQtyForExplanation,
                    requiredQty,
                    finalQuantity,
                    finalUnitName);

                existing.RequiredQty += finalQuantity;
                existing.CalculationExplanation = CombineCompactPriority50(existing.CalculationExplanation, compactLine);
                existing.CalculationSummary = CombineCompactPriority50(existing.CalculationSummary, compactLine);
            }
            else
            {
                var finalUnitName = await GetUnitShortNameAsync(finalUnitId);
                var compactLine = BuildCompactPriorityLine50(
                    orderLine,
                    component.PNo,
                    fgPnRaw,
                    bomVersionText,
                    bomQtyPerUnit,
                    parentProductQtyForExplanation,
                    requiredQty,
                    finalQuantity,
                    finalUnitName);

                decimal? estimatedPrice = await GetEstimatedPriceAsync(componentId, finalUnitId);

                componentRequirements[componentId] = new RawMaterialRequirement
                {
                    ComponentID = componentId,
                    ComponentPNo = component.PNo,
                    ComponentName = component.Name,
                    UnitID = finalUnitId,
                    RequiredQty = finalQuantity,
                    EstimatedPrice = estimatedPrice,
                    CalculationExplanation = compactLine,
                    CalculationSummary = compactLine
                };
            }
        }

        /// <summary>
        /// Eng mos BOM: barcha faol yozuvlar ichidan VersionNo (oy/yil) va CreatedAt bo'yicha eng yangisi.
        /// VersionNo bo'sh bo'lsa ham CreatedAt bilan tanlanadi (oldingi logikada faqat VersionNo bo'lganlar tanlanib, yangi yozuv e'tiborsiz qolardi).
        /// </summary>
        private async Task<BOM_HEADERS> GetLatestActiveBOMAsync(int productId)
        {
            var bomHeaders = await _db.BOM_HEADERS
                .Include(b => b.BOM_LINES)
                .Include(b => b.BOM_LINES.Select(bl => bl.PRODUCT))
                .Include(b => b.BOM_LINES.Select(bl => bl.UNIT))
                .Where(b => b.ProductID == productId && (b.IsActive == true || b.IsActive == null))
                .ToListAsync();

            if (!bomHeaders.Any())
            {
                return null;
            }

            return bomHeaders
                .OrderByDescending(bh => GetBomSortKey(bh))
                .ThenByDescending(bh => bh.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(bh => bh.ID)
                .FirstOrDefault();
        }

        private static DateTime GetBomSortKey(BOM_HEADERS bh)
        {
            var fromVersion = TryParseBomVersionDate(bh?.VersionNo);
            if (fromVersion.HasValue)
            {
                return fromVersion.Value;
            }

            return bh?.CreatedAt ?? DateTime.MinValue;
        }

        private static DateTime? TryParseBomVersionDate(string versionNo)
        {
            if (string.IsNullOrWhiteSpace(versionNo))
            {
                return null;
            }

            versionNo = versionNo.Trim();

            if (DateTime.TryParseExact(versionNo, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime ym))
            {
                return ym;
            }

            if (DateTime.TryParseExact(versionNo, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime ymd))
            {
                return ymd;
            }

            var parts = versionNo.Split('-');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) &&
                month >= 1 && month <= 12 && year > 1900 && year < 2100)
            {
                return new DateTime(year, month, 1);
            }

            return null;
        }

        /// <summary>
        /// Narx: avvalo eng so'nggi qabul qilingan ombor kirimi (1 qator birligi bo'yicha o'rtacha narx, masalan 1 kg),
        /// keyin norma (NORM_INPUT_LINES). Metizlar kg da kirganda PiecePrice dona uchun bo'lishi mumkin — TotalPrice/Amount ustun.
        /// </summary>
        private async Task<decimal?> GetEstimatedPriceAsync(int materialId, int targetUnitId)
        {
            var fromIncome = await TryGetEstimatedPriceFromLatestIncomeAsync(materialId, targetUnitId);
            if (fromIncome.HasValue)
            {
                return fromIncome;
            }

            var normLine = await _db.NORM_INPUT_LINES
                .AsNoTracking()
                .Where(n => n.MaterialID == materialId && n.Price > 0)
                .OrderByDescending(n => n.CreatedDT)
                .FirstOrDefaultAsync();

            if (normLine == null)
            {
                return null;
            }

            var normPrice = normLine.Price;
            var normUnitId = normLine.UnitID ?? 0;
            if (normUnitId <= 0)
            {
                var product = await _db.PRODUCTS.AsNoTracking().FirstOrDefaultAsync(p => p.ID == materialId);
                normUnitId = product != null && product.UnitID > 0 ? product.UnitID : targetUnitId;
            }

            if (normUnitId == targetUnitId)
            {
                return normPrice;
            }

            try
            {
                var normQtyPerOneTarget = await _conversionService.ConvertToUnitAsync(
                    materialId,
                    1m,
                    targetUnitId,
                    normUnitId);

                if (normQtyPerOneTarget > 0)
                {
                    return normPrice * normQtyPerOneTarget;
                }
            }
            catch
            {
                // ignore
            }

            return normPrice;
        }

        /// <summary>
        /// Kirim qatoridagi <b>birlik narxi</b>: asosan TotalPrice/Amount (masalan kg bo'lsa — so'm/kg).
        /// </summary>
        private async Task<decimal?> TryGetEstimatedPriceFromLatestIncomeAsync(int materialId, int targetUnitId)
        {
            var latestIncomeProduct = await _db.INCOME_PRODUCTS
                .Include(ip => ip.INCOME)
                .Where(ip => ip.ProductID == materialId
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false)
                    && ip.INCOME.RecieveStatus == true
                    && ip.Amount > 0)
                .OrderByDescending(ip => ip.INCOME.IssueDateTime)
                .FirstOrDefaultAsync();

            if (latestIncomeProduct == null)
            {
                return null;
            }

            decimal sourcePrice = 0m;
            if (latestIncomeProduct.TotalPrice.HasValue && latestIncomeProduct.TotalPrice.Value > 0)
            {
                sourcePrice = (decimal)(latestIncomeProduct.TotalPrice.Value / latestIncomeProduct.Amount);
            }
            else if (latestIncomeProduct.PiecePrice.HasValue && latestIncomeProduct.PiecePrice.Value > 0)
            {
                sourcePrice = (decimal)latestIncomeProduct.PiecePrice.Value;
            }
            else
            {
                return null;
            }

            if (sourcePrice <= 0)
            {
                return null;
            }

            var sourceUnitId = latestIncomeProduct.UnitID;
            if (sourceUnitId == targetUnitId)
            {
                return sourcePrice;
            }

            try
            {
                var qtyInSourceForOneTarget = await _conversionService.ConvertToUnitAsync(
                    materialId,
                    1m,
                    targetUnitId,
                    sourceUnitId);

                if (qtyInSourceForOneTarget > 0)
                {
                    return sourcePrice * qtyInSourceForOneTarget;
                }
            }
            catch
            {
                // ignore
            }

            return sourcePrice;
        }
    }

    /// <summary>
    /// DTO for raw material requirements
    /// </summary>
    public class RawMaterialRequirement
    {
        public int ComponentID { get; set; }
        public string ComponentPNo { get; set; }
        public string ComponentName { get; set; }
        public int UnitID { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal? EstimatedPrice { get; set; }
        public string CalculationExplanation { get; set; }

        /// <summary> P_ORDER_PRODUCTS.Priority (max 50) uchun qisqa matn. </summary>
        public string CalculationSummary { get; set; }

        public decimal? EstimatedAmount => EstimatedPrice.HasValue
            ? RequiredQty * EstimatedPrice.Value
            : (decimal?)null;
    }
}
