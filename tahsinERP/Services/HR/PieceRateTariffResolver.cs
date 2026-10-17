using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// Ishbay tarifini aniqlash — yagona qoida (HRM-D01). Avval 6 joyda nusxa bo'lib, har ish yozuviga 1-2 so'rov yuborardi
    /// (Index, Details, GetTariff, Payroll, Koreshok; Koreshok nusxasida IsDeleted filtri ham yo'q edi).
    ///
    /// Qoida: tarif (Mahsulot, Operatsiya) bo'yicha, IsActive va o'chirilmagan, sana EffectiveFrom..EffectiveTo oralig'ida.
    /// Avval xodim lavozimiga xos tarif, topilmasa umumiy (PositionID = NULL) tarif.
    /// Bir nechta mos kelsa — eng so'nggi EffectiveFrom, keyin eng katta ID (avval tartib aniqlanmagan edi).
    ///
    /// Foydalanish: davr uchun bir marta <see cref="LoadAsync"/>, keyin xotirada <see cref="Resolve"/>.
    /// </summary>
    public sealed class PieceRateTariffResolver
    {
        private readonly Dictionary<long, List<HR_PIECE_RATE_TARIFFS>> _byProductOperation;

        private PieceRateTariffResolver(IEnumerable<HR_PIECE_RATE_TARIFFS> tariffs)
        {
            _byProductOperation = tariffs
                .Where(t => t.OperationID.HasValue)
                .GroupBy(t => Key(t.ProductID, t.OperationID.Value))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(t => t.EffectiveFrom).ThenByDescending(t => t.ID).ToList());
        }

        private static long Key(int productId, int operationId) => ((long)productId << 32) | (uint)operationId;

        /// <summary>
        /// [from, to] davri bilan kesishadigan barcha faol tariflarni bitta so'rov bilan yuklaydi.
        /// <paramref name="productIds"/> berilsa — faqat shu mahsulotlar (katta ro'yxat bo'lsa null bering).
        /// </summary>
        public static async Task<PieceRateTariffResolver> LoadAsync(
            DBTHSNEntities db, DateTime from, DateTime to, ICollection<int> productIds = null, bool includeCurrency = false)
        {
            var fromDate = from.Date;
            var toDate = to.Date;
            IQueryable<HR_PIECE_RATE_TARIFFS> query = db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Where(t => t.IsActive &&
                            t.IsDeleted != true &&
                            t.OperationID != null &&
                            t.EffectiveFrom <= toDate &&
                            (t.EffectiveTo == null || t.EffectiveTo >= fromDate));

            if (productIds != null)
            {
                var ids = productIds.Distinct().ToList();
                if (ids.Count == 0) return new PieceRateTariffResolver(Enumerable.Empty<HR_PIECE_RATE_TARIFFS>());
                if (ids.Count <= 500) query = query.Where(t => ids.Contains(t.ProductID));
            }

            if (includeCurrency)
            {
                query = query.Include(t => t.CURRENCy);
            }

            return new PieceRateTariffResolver(await query.ToListAsync());
        }

        /// <summary>Tarifni topadi (topilmasa null).</summary>
        public HR_PIECE_RATE_TARIFFS Resolve(int productId, int operationId, int? positionId, DateTime workDate)
        {
            List<HR_PIECE_RATE_TARIFFS> candidates;
            if (!_byProductOperation.TryGetValue(Key(productId, operationId), out candidates))
                return null;

            var date = workDate.Date;
            HR_PIECE_RATE_TARIFFS general = null;
            foreach (var t in candidates)
            {
                if (t.EffectiveFrom.Date > date) continue;
                if (t.EffectiveTo.HasValue && t.EffectiveTo.Value.Date < date) continue;

                if (positionId.HasValue && t.PositionID == positionId)
                    return t;
                if (t.PositionID == null && general == null)
                {
                    general = t;
                    if (!positionId.HasValue) return general;
                }
            }
            return general;
        }

        /// <summary>Miqdor × tarif (tarif topilmasa 0).</summary>
        public decimal Amount(int productId, int operationId, int? positionId, DateTime workDate, decimal quantity)
        {
            var tariff = Resolve(productId, operationId, positionId, workDate);
            return tariff == null ? 0m : quantity * tariff.RatePerUnit;
        }
    }
}
