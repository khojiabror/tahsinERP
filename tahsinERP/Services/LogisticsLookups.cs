using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika dropdown'lari (LOG-D03): shartnomalar va transport turlari — bitta o'chirilganlik filtri bilan,
    /// faqat ID/nom proyeksiyasi. Ular ReferenceDataCache'da yo'q (umumiy fayl — qo'shish taklifi hisobotda),
    /// shuning uchun keshsiz, lekin kichik so'rov. Yuk tashuvchi va valyutalar — ReferenceDataCache.
    /// <paramref name="includeId"/> — tahrirlanayotgan hujjatdagi (o'chirilgan bo'lsa ham) qiymat ro'yxatda qolsin.
    /// </summary>
    public static class LogisticsLookups
    {
        public static List<RefItem> Contracts(DBTHSNEntities db, int? includeId = null)
        {
            return db.F_CONTRACTS.AsNoTracking()
                .Where(c => !c.IsDeleted || (includeId.HasValue && c.ID == includeId.Value))
                .OrderBy(c => c.ContractNo)
                .Select(c => new RefItem { ID = c.ID, Name = c.ContractNo, ExtraId = c.ForwarderID })
                .ToList();
        }

        public static List<RefItem> TransportTypes(DBTHSNEntities db, int? includeId = null)
        {
            return db.F_TRANSPORT_TYPES.AsNoTracking()
                .Where(t => t.IsDeleted != true || (includeId.HasValue && t.ID == includeId.Value))
                .OrderBy(t => t.TransportType)
                .Select(t => new RefItem { ID = t.ID, Name = t.TransportType })
                .ToList();
        }
    }
}
