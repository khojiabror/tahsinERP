using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services
{
    public enum PriceSource
    {
        None = 0,
        CustomerSpecial = 1,
        CustomerPriceType = 2,
        DefaultPriceType = 3,
        Promotion = 4      // aksiya sovg'asi (narxsiz qator)
    }

    /// <summary>Asosiy birlik uchun aniqlangan narx.</summary>
    public sealed class ResolvedPrice
    {
        public static readonly ResolvedPrice None = new ResolvedPrice(null, null, PriceSource.None, null);

        public ResolvedPrice(decimal? price, decimal? minPrice, PriceSource source, int? priceTypeId)
        {
            Price = price;
            MinPrice = minPrice;
            Source = source;
            PriceTypeID = priceTypeId;
        }

        public decimal? Price { get; }
        public decimal? MinPrice { get; }
        public PriceSource Source { get; }
        public int? PriceTypeID { get; }
        public bool Found => Price.HasValue;
    }

    /// <summary>
    /// Narx aniqlash qoidasi (sof funksiya — testlanadi):
    ///  1) mijozning maxsus narxi (sana oralig'ida, eng so'nggi ValidFrom);
    ///  2) mijoz narx turidagi narx varag'i (sanagacha eng so'nggi ValidFrom);
    ///  3) standart narx turi.
    /// Minimal narx narx turi qatoridan olinadi. Maxsus narx — rahbar qarori, unga minimal narx qo'llanmaydi.
    /// </summary>
    public static class PriceResolver
    {
        public static ResolvedPrice Resolve(
            IEnumerable<CustomerPrice> customerPrices,
            IEnumerable<PriceListItem> priceList,
            int? customerPriceTypeId,
            int? defaultPriceTypeId,
            DateTime at)
        {
            var day = at.Date;
            var special = (customerPrices ?? Enumerable.Empty<CustomerPrice>())
                .Where(c => c.ValidFrom <= day && (c.ValidTo == null || c.ValidTo >= day))
                .OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.ID)
                .FirstOrDefault();
            if (special != null)
                return new ResolvedPrice(special.Price, null, PriceSource.CustomerSpecial, null);

            var items = (priceList ?? Enumerable.Empty<PriceListItem>()).Where(p => p.ValidFrom <= at).ToList();

            PriceListItem Latest(int? typeId) => typeId == null
                ? null
                : items.Where(p => p.PriceTypeID == typeId.Value)
                    .OrderByDescending(p => p.ValidFrom).ThenByDescending(p => p.ID).FirstOrDefault();

            var own = Latest(customerPriceTypeId);
            if (own != null)
                return new ResolvedPrice(own.Price, own.MinPrice, PriceSource.CustomerPriceType, own.PriceTypeID);

            if (defaultPriceTypeId != customerPriceTypeId)
            {
                var def = Latest(defaultPriceTypeId);
                if (def != null)
                    return new ResolvedPrice(def.Price, def.MinPrice, PriceSource.DefaultPriceType, def.PriceTypeID);
            }
            return ResolvedPrice.None;
        }

        /// <summary>Ommaviy o'zgartirish: narx × (1 + foiz/100), <paramref name="roundTo"/> so'mgacha yaxlitlab.</summary>
        public static decimal ApplyPercent(decimal price, decimal percent, decimal roundTo)
        {
            if (percent <= -100) throw new DstValidationException(DstLocalization.T("Error_PercentRange"));
            var value = price * (1 + percent / 100m);
            if (roundTo > 0)
                value = Math.Round(value / roundTo, 0, MidpointRounding.AwayFromZero) * roundTo;
            return Math.Round(Math.Max(0, value), 2);
        }
    }

    public class PricingService
    {
        private readonly DistributionDbContext _db;

        public PricingService(DistributionDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public int? DefaultPriceTypeId()
        {
            return _db.PriceTypes.Where(t => t.IsDefault && t.IsActive).Select(t => (int?)t.ID).FirstOrDefault();
        }

        /// <summary>Bitta mijoz uchun bir nechta tovar narxi (asosiy birlikda) — buyurtma va katalog uchun.</summary>
        public Dictionary<int, ResolvedPrice> Resolve(int? customerId, IReadOnlyCollection<int> productIds, DateTime at)
        {
            var result = new Dictionary<int, ResolvedPrice>();
            if (productIds == null || productIds.Count == 0) return result;

            int? customerType = customerId == null
                ? null
                : _db.CustomerProfiles.Where(p => p.CustomerID == customerId).Select(p => p.PriceTypeID).FirstOrDefault();
            int? defaultType = DefaultPriceTypeId();
            var types = new[] { customerType, defaultType }.Where(t => t.HasValue).Select(t => t.Value).Distinct().ToList();
            var ids = productIds.Distinct().ToList();

            var list = _db.PriceListItems.AsNoTracking()
                .Where(p => types.Contains(p.PriceTypeID) && ids.Contains(p.ProductID) && p.ValidFrom <= at)
                .ToList()
                .ToLookup(p => p.ProductID);

            var day = at.Date;
            var special = customerId == null
                ? Enumerable.Empty<CustomerPrice>().ToLookup(c => c.ProductID)
                : _db.CustomerPrices.AsNoTracking()
                    .Where(c => c.CustomerID == customerId && ids.Contains(c.ProductID) && c.ValidFrom <= day)
                    .ToList()
                    .ToLookup(c => c.ProductID);

            foreach (var id in ids)
                result[id] = PriceResolver.Resolve(special[id], list[id], customerType, defaultType, at);
            return result;
        }

        /// <summary>Tovarning har bir faol narx turidagi joriy narxi (kartochka uchun).</summary>
        public List<(PriceType Type, PriceListItem Item)> CurrentByType(int productId, DateTime at)
        {
            var types = _db.PriceTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList();
            var items = _db.PriceListItems.AsNoTracking()
                .Where(p => p.ProductID == productId && p.ValidFrom <= at)
                .ToList()
                .GroupBy(p => p.PriceTypeID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ValidFrom).ThenByDescending(p => p.ID).First());
            return types.Select(t => (t, items.TryGetValue(t.ID, out var item) ? item : null)).ToList();
        }

        /// <summary>
        /// Narx varag'iga yangi narx: oxirgisi bilan bir xil bo'lsa — yozilmaydi (tarix shovqinsiz).
        /// Qaytaradi: yangi qator yoki null.
        /// </summary>
        public PriceListItem SetPrice(int priceTypeId, int productId, decimal price, decimal? minPrice, int? userId, DateTime at)
        {
            if (price < 0) throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
            if (minPrice.HasValue && (minPrice < 0 || minPrice > price))
                throw new DstValidationException(DstLocalization.T("Error_MinPriceRange"));

            var last = _db.PriceListItems
                .Where(p => p.PriceTypeID == priceTypeId && p.ProductID == productId && p.ValidFrom <= at)
                .OrderByDescending(p => p.ValidFrom).ThenByDescending(p => p.ID)
                .FirstOrDefault();
            if (last != null && last.Price == price && last.MinPrice == minPrice)
                return null;

            var item = new PriceListItem
            {
                PriceTypeID = priceTypeId, ProductID = productId, Price = price, MinPrice = minPrice,
                ValidFrom = at, CreatedBy = userId, CreatedAt = at
            };
            _db.PriceListItems.Add(item);
            return item;
        }
    }
}
