using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika dropdown'lari (LOG-D03): shartnomalar va transport turlari ReferenceDataCache'dan
    /// (o'zgarganda avtomatik yangilanadi). Yuk tashuvchi va valyutalar — ham ReferenceDataCache.
    /// <paramref name="includeId"/> — tahrirlanayotgan hujjatdagi (o'chirilgan bo'lsa ham) qiymat ro'yxatda qolsin:
    /// keshda bo'lmasa, faqat shu bitta yozuv bazadan olinadi.
    /// </summary>
    public static class LogisticsLookups
    {
        public static List<RefItem> Contracts(DBTHSNEntities db, int? includeId = null)
        {
            var items = ReferenceDataCache.Get(ReferenceDataCache.FContracts).ToList();
            if (includeId.HasValue && !items.Any(x => x.ID == includeId.Value))
            {
                int id = includeId.Value;
                var extra = db.F_CONTRACTS.AsNoTracking()
                    .Where(c => c.ID == id)
                    .Select(c => new RefItem { ID = c.ID, Name = c.ContractNo, ExtraId = c.ForwarderID })
                    .FirstOrDefault();
                if (extra != null)
                    items.Add(extra);
            }
            return items.OrderBy(x => x.Name).ToList();
        }

        public static List<RefItem> TransportTypes(DBTHSNEntities db, int? includeId = null)
        {
            var items = ReferenceDataCache.Get(ReferenceDataCache.TransportTypes).ToList();
            if (includeId.HasValue && !items.Any(x => x.ID == includeId.Value))
            {
                int id = includeId.Value;
                var extra = db.F_TRANSPORT_TYPES.AsNoTracking()
                    .Where(t => t.ID == id)
                    .Select(t => new RefItem { ID = t.ID, Name = t.TransportType })
                    .FirstOrDefault();
                if (extra != null)
                    items.Add(extra);
            }
            return items.OrderBy(x => x.Name).ToList();
        }
    }
}
