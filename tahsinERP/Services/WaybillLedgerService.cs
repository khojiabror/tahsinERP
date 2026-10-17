using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Yo'l varaqasi bo'yicha "to'langan / qarz" hisobining yagona manbai (audit LOG-D01, LOG-B04).
    /// Avval bu hisob 8 joyda turlicha yozilgan edi (ba'zilari split'larni, ba'zilari qo'lda kiritilgan
    /// FIN to'lovlarni hisobga olmasdi), natijada har ekran boshqa qarz ko'rsatardi.
    ///
    /// Ta'riflar:
    ///  - Yo'l varaqasiga to'langan = split'siz (to'g'ridan-to'g'ri) to'lovlar (WaybillID bo'yicha)
    ///    + o'chirilmagan to'lovlarning split taqsimotlari (F_WAYBILL_PAYMENT_SPLITS, PayerType = 99).
    ///    Bir nechta yo'l varaqasiga bo'lingan to'lov faqat split'lari orqali hisoblanadi.
    ///  - Yuk tashuvchiga to'langan = to'lovning o'z ForwarderID'si bo'yicha barcha o'chirilmagan to'lovlar
    ///    (asosiy yo'l varaqasi o'chirilmagan) + FIN'da qo'lda kiritilgan yuk tashuvchi to'lovlari
    ///    (CounterpartyType = 4, Direction = 2, SourceDocType != "WaybillPayment").
    ///  - Valyuta (LOG-B13, CBU kursi, hujjat sanasi — <see cref="LogisticsMoney"/>): yo'l varaqasi bo'yicha to'langan
    ///    yo'l varaqasi valyutasida; to'lov taqsimoti to'lov valyutasida; yuk tashuvchi balanslari so'mda.
    /// </summary>
    public static class WaybillLedgerService
    {
        /// <summary>F_WAYBILL_PAYMENT_SPLITS.PayerType: split qatori F_WAYBILL_PAYMENTS.ID ga ishora qiladi.</summary>
        public const byte SplitPayerType = 99;

        /// <summary>FIN_TRANSACTIONS.CounterpartyType: yuk tashuvchi.</summary>
        public const byte FinCounterpartyForwarder = 4;

        /// <summary>FIN_TRANSACTIONS.Direction: chiqim.</summary>
        public const byte FinDirectionOut = 2;

        public const string FinSourceDocType = "WaybillPayment";

        private const double Epsilon = 0.005;

        // ---------------------------------------------------------------- bazaviy so'rovlar

        /// <summary>O'chirilmagan yo'l varaqalari (IsDeleted NULL ham faol — LOG-B14).</summary>
        public static IQueryable<F_WAYBILLS> ActiveWaybills(DBTHSNEntities db)
        {
            return db.F_WAYBILLS.Where(w => w.IsDeleted != true);
        }

        /// <summary>Yuk tashuvchi bo'yicha hisoblanadigan to'lovlar: to'lov va uning asosiy yo'l varaqasi o'chirilmagan.</summary>
        public static IQueryable<F_WAYBILL_PAYMENTS> ActivePayments(DBTHSNEntities db)
        {
            return db.F_WAYBILL_PAYMENTS.Where(p => !p.IsDeleted && p.F_WAYBILLS.IsDeleted != true);
        }

        /// <summary>FIN'da qo'lda kiritilgan yuk tashuvchi to'lovlari (WaybillPayment'dan yaratilganlar chiqariladi).</summary>
        public static IQueryable<FIN_TRANSACTIONS> ManualForwarderPayments(DBTHSNEntities db)
        {
            return db.FIN_TRANSACTIONS.Where(t => t.CounterpartyType == FinCounterpartyForwarder
                && t.Direction == FinDirectionOut
                && t.IsDeleted != true
                && t.CounterpartyID.HasValue
                && (t.SourceDocType == null || t.SourceDocType != FinSourceDocType));
        }

        // ---------------------------------------------------------------- yo'l varaqasi bo'yicha

        /// <summary>
        /// Yo'l varaqalari bo'yicha to'langan summa. <paramref name="waybillIds"/> — IQueryable bo'lsa
        /// SQL'da subquery (katta IN ro'yxatisiz) bo'ladi.
        /// </summary>
        /// <remarks>
        /// LOG-B13: natija yo'l varaqasi valyutasida — boshqa valyutadagi to'lov to'lov sanasidagi kurs bilan
        /// o'giriladi (kurs topilmasa qo'shilmaydi, <paramref name="money"/>.Warning).
        /// </remarks>
        public static Dictionary<int, double> GetPaidLookup(DBTHSNEntities db, IQueryable<int> waybillIds,
            int? excludePaymentId = null, LogisticsMoney money = null)
        {
            var direct = db.F_WAYBILL_PAYMENTS
                .Where(p => !p.IsDeleted
                            && waybillIds.Contains(p.WaybillID)
                            && (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value)
                            && !db.F_WAYBILL_PAYMENT_SPLITS.Any(s => s.PayerType == SplitPayerType && s.PayerID == p.ID))
                .GroupBy(p => new { p.WaybillID, WaybillCurrencyID = p.F_WAYBILLS.CurrencyID, p.CurrencyID, Day = DbFunctions.TruncateTime(p.PaymentDate) })
                .Select(g => new PaidPart
                {
                    WaybillID = g.Key.WaybillID,
                    WaybillCurrencyID = g.Key.WaybillCurrencyID,
                    CurrencyID = g.Key.CurrencyID,
                    Day = g.Key.Day,
                    Amount = g.Sum(x => x.Payment_Amount)
                })
                .ToList();

            var split = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                         join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                         join w in db.F_WAYBILLS on s.WaybillID equals w.ID
                         where s.PayerType == SplitPayerType
                               && !p.IsDeleted
                               && waybillIds.Contains(s.WaybillID)
                               && (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value)
                         group s by new { s.WaybillID, WaybillCurrencyID = w.CurrencyID, CurrencyID = s.CurrencyID ?? p.CurrencyID, Day = DbFunctions.TruncateTime(p.PaymentDate) } into g
                         select new PaidPart
                         {
                             WaybillID = g.Key.WaybillID,
                             WaybillCurrencyID = g.Key.WaybillCurrencyID,
                             CurrencyID = g.Key.CurrencyID,
                             Day = g.Key.Day,
                             Amount = g.Sum(x => x.Amount)
                         })
                .ToList();

            var result = new Dictionary<int, double>();
            foreach (var item in direct.Concat(split))
            {
                double? amount = item.Amount;
                if (item.CurrencyID.HasValue && item.WaybillCurrencyID.HasValue && item.CurrencyID != item.WaybillCurrencyID)
                {
                    money = money ?? new LogisticsMoney(db);
                    amount = money.Convert(item.Amount, item.CurrencyID, item.WaybillCurrencyID, item.Day);
                    if (!amount.HasValue) continue;
                }

                double current;
                result.TryGetValue(item.WaybillID, out current);
                result[item.WaybillID] = current + amount.Value;
            }
            return result;
        }

        private sealed class PaidPart
        {
            public int WaybillID { get; set; }
            public int? WaybillCurrencyID { get; set; }
            public int? CurrencyID { get; set; }
            public DateTime? Day { get; set; }
            public double Amount { get; set; }
        }

        /// <summary>Kichik ro'yxat (sahifa, tanlangan yo'l varaqalari) uchun.</summary>
        public static Dictionary<int, double> GetPaidLookup(DBTHSNEntities db, ICollection<int> waybillIds,
            int? excludePaymentId = null, LogisticsMoney money = null)
        {
            if (waybillIds == null || waybillIds.Count == 0)
                return new Dictionary<int, double>();
            var ids = waybillIds.Distinct().ToList();
            return GetPaidLookup(db, db.F_WAYBILLS.Where(w => ids.Contains(w.ID)).Select(w => w.ID), excludePaymentId, money);
        }

        public static double GetPaidTotal(DBTHSNEntities db, int waybillId, int? excludePaymentId = null, LogisticsMoney money = null)
        {
            double paid;
            GetPaidLookup(db, new[] { waybillId }, excludePaymentId, money).TryGetValue(waybillId, out paid);
            return paid;
        }

        public static double PaidOf(IDictionary<int, double> lookup, int waybillId)
        {
            double paid;
            return lookup != null && lookup.TryGetValue(waybillId, out paid) ? paid : 0;
        }

        /// <summary>
        /// Biror summasi bo'lgan, lekin hech qanday to'lov (to'g'ridan-to'g'ri yoki split) bog'lanmagan
        /// yo'l varaqalari — SQL'da NOT EXISTS (Reconciliation uchun).
        /// </summary>
        public static IQueryable<F_WAYBILLS> WithoutAnyPayment(DBTHSNEntities db, IQueryable<F_WAYBILLS> waybills)
        {
            return waybills.Where(w =>
                !db.F_WAYBILL_PAYMENTS.Any(p => !p.IsDeleted && p.WaybillID == w.ID) &&
                !(from s in db.F_WAYBILL_PAYMENT_SPLITS
                  join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                  where s.PayerType == SplitPayerType && s.WaybillID == w.ID && !p.IsDeleted
                  select s.ID).Any());
        }

        // ---------------------------------------------------------------- to'lov holati

        public static WaybillPaymentStatus StatusOf(double amount, double paid)
        {
            var outstanding = amount - paid;
            if (outstanding <= Epsilon) return WaybillPaymentStatus.Paid;
            return paid > Epsilon ? WaybillPaymentStatus.Partial : WaybillPaymentStatus.Unpaid;
        }

        /// <summary>Holat matni (LOG-M03: avval "To'landi"/"To'langan" aralash edi).</summary>
        public static string ToDisplay(this WaybillPaymentStatus status)
        {
            switch (status)
            {
                case WaybillPaymentStatus.Paid: return "To'langan";
                case WaybillPaymentStatus.Partial: return "Qisman";
                default: return "To'lanmagan";
            }
        }

        public static string BadgeClass(this WaybillPaymentStatus status)
        {
            switch (status)
            {
                case WaybillPaymentStatus.Paid: return "badge-success";
                case WaybillPaymentStatus.Partial: return "badge-warning";
                default: return "badge-danger";
            }
        }

        // ---------------------------------------------------------------- to'lov <-> yo'l varaqalari

        /// <summary>To'lov bog'langan yo'l varaqalari (split bo'lmasa — asosiy WaybillID).</summary>
        public static List<int> GetPaymentWaybillIds(DBTHSNEntities db, int paymentId, int fallbackWaybillId)
        {
            var ids = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == SplitPayerType && s.PayerID == paymentId)
                .Select(s => s.WaybillID)
                .Distinct()
                .ToList();

            if (ids.Count == 0 && fallbackWaybillId > 0)
                ids.Add(fallbackWaybillId);
            return ids;
        }

        /// <summary>Bir nechta to'lov uchun: to'lov ID → split qatorlari (WaybillID, Amount).</summary>
        public static ILookup<int, F_WAYBILL_PAYMENT_SPLITS> GetSplitsByPayment(DBTHSNEntities db, IQueryable<int> paymentIds)
        {
            return db.F_WAYBILL_PAYMENT_SPLITS.AsNoTracking()
                .Where(s => s.PayerType == SplitPayerType && s.PayerID.HasValue && paymentIds.Contains(s.PayerID.Value))
                .ToList()
                .ToLookup(s => s.PayerID.Value);
        }

        /// <summary>
        /// To'lov summasini tanlangan yo'l varaqalari bo'yicha taqsimlaydi: avval tartib bo'yicha qoldiqlarni
        /// yopadi, ortgan summa teng bo'linadi. <paramref name="paymentId"/> ning o'zi "allaqachon to'langan"
        /// hisoblanmaydi (LOG-B01: Create'da yangi to'lov birinchi yo'l varaqasini yopib qo'yardi).
        /// </summary>
        /// <remarks>
        /// LOG-B13: taqsimot to'lov valyutasida — yo'l varaqasi qoldig'i to'lov sanasidagi kurs bilan
        /// to'lov valyutasiga o'giriladi (kurs bo'lishi ValidatePayment'da tekshiriladi).
        /// </remarks>
        public static Dictionary<int, double> Allocate(DBTHSNEntities db, IList<int> waybillIds, double paymentAmount, int? paymentId,
            int? paymentCurrencyId = null, DateTime? paymentDate = null)
        {
            var result = new Dictionary<int, double>();
            if (waybillIds == null || waybillIds.Count == 0 || paymentAmount <= 0)
                return result;

            var ids = waybillIds.Distinct().ToList();
            var waybills = db.F_WAYBILLS
                .Where(w => ids.Contains(w.ID))
                .Select(w => new { w.ID, w.Amount, w.CurrencyID })
                .ToList()
                .ToDictionary(x => x.ID);
            var money = new LogisticsMoney(db);
            var paid = GetPaidLookup(db, ids, paymentId, money);

            var existing = ids.Where(waybills.ContainsKey).ToList();
            if (existing.Count == 0) return result;

            var remaining = paymentAmount;
            foreach (var id in existing)
            {
                if (remaining <= 0) break;
                var w = waybills[id];
                var outstandingInWaybillCurrency = Math.Max(0, (w.Amount ?? 0) - PaidOf(paid, id));
                var outstanding = money.Convert(outstandingInWaybillCurrency, w.CurrencyID, paymentCurrencyId, paymentDate)
                                  ?? outstandingInWaybillCurrency;
                if (outstanding <= 0) continue;

                var allocated = Math.Min(outstanding, remaining);
                result[id] = allocated;
                remaining -= allocated;
            }

            if (remaining > Epsilon)
            {
                // Ortiqcha summa (avans) tanlangan yo'l varaqalariga teng bo'linadi
                var per = remaining / existing.Count;
                foreach (var id in existing)
                {
                    double current;
                    result.TryGetValue(id, out current);
                    result[id] = current + per;
                }
            }

            return result;
        }

        /// <summary>
        /// To'lovning split qatorlarini qayta yozadi. SaveChanges chaqirmaydi — chaqiruvchi
        /// tranzaksiya ichida bitta SaveChanges bilan saqlaydi (LOG-B08).
        /// To'lov (<paramref name="paymentId"/>) allaqachon bazada bo'lishi kerak.
        /// </summary>
        public static void ReplacePaymentSplits(DBTHSNEntities db, int paymentId, IList<int> waybillIds,
            double paymentAmount, int? currencyId, string comment, DateTime? paymentDate)
        {
            var oldSplits = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == SplitPayerType && s.PayerID == paymentId)
                .ToList();
            if (oldSplits.Count > 0)
                db.F_WAYBILL_PAYMENT_SPLITS.RemoveRange(oldSplits);

            // Taqsimot eski split'larni hisobga olmasligi uchun paymentId chiqarib tashlanadi
            var alloc = Allocate(db, waybillIds, paymentAmount, paymentId, currencyId, paymentDate);
            foreach (var kv in alloc.Where(x => x.Value > 0))
            {
                db.F_WAYBILL_PAYMENT_SPLITS.Add(new F_WAYBILL_PAYMENT_SPLITS
                {
                    WaybillID = kv.Key,
                    PayerType = SplitPayerType,
                    PayerID = paymentId,
                    Amount = kv.Value,
                    CurrencyID = currencyId,
                    Comment = string.IsNullOrWhiteSpace(comment) ? "WaybillPaymentLink" : comment
                });
            }
        }

        public static void RemovePaymentSplits(DBTHSNEntities db, int paymentId)
        {
            var splits = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == SplitPayerType && s.PayerID == paymentId)
                .ToList();
            if (splits.Count > 0)
                db.F_WAYBILL_PAYMENT_SPLITS.RemoveRange(splits);
        }

        // ---------------------------------------------------------------- yuk tashuvchi bo'yicha

        /// <summary>
        /// Yuk tashuvchilar bo'yicha davr kesimidagi balans: davrdan oldingi va davr ichidagi
        /// navlun hamda to'lovlar — 3 ta guruhlangan SQL so'rov (LOG-P05).
        /// <paramref name="periodStart"/> null bo'lsa — hammasi "davr ichida" (boshlang'ich qoldiq 0).
        /// <paramref name="periodEnd"/> — kiritilgan (ichida) chegara; null bo'lsa yuqori chegara yo'q.
        /// </summary>
        public static Dictionary<int, ForwarderBalance> GetForwarderBalances(DBTHSNEntities db,
            DateTime? periodStart, DateTime? periodEnd, int? forwarderId = null, LogisticsMoney money = null)
        {
            // SQL datetime DateTime.MinValue'ni qabul qilmaydi
            var start = periodStart ?? new DateTime(1900, 1, 1);

            var waybills = ActiveWaybills(db).Where(w => w.ForwarderID.HasValue);
            var payments = ActivePayments(db);
            var manual = ManualForwarderPayments(db);
            if (periodStart.HasValue || periodEnd.HasValue)
            {
                // Davr berilsa sanasiz yozuvlar hech qaysi davrga tushmaydi; davrsiz (umumiy balans) — hammasi kiradi
                waybills = waybills.Where(w => w.IssueDate.HasValue);
                payments = payments.Where(p => p.PaymentDate.HasValue);
            }
            if (periodEnd.HasValue)
            {
                var endExclusive = periodEnd.Value.Date.AddDays(1);
                waybills = waybills.Where(w => w.IssueDate < endExclusive);
                payments = payments.Where(p => p.PaymentDate < endExclusive);
                manual = manual.Where(t => t.TxnDate < endExclusive);
            }
            if (forwarderId.HasValue)
            {
                waybills = waybills.Where(w => w.ForwarderID == forwarderId.Value);
                payments = payments.Where(p => p.ForwarderID == forwarderId.Value);
                manual = manual.Where(t => t.CounterpartyID == forwarderId.Value);
            }

            // LOG-B13: (yuk tashuvchi, valyuta, kun) kesimida guruhlanadi va so'mga hujjat sanasidagi kurs bilan o'giriladi
            money = money ?? new LogisticsMoney(db);

            var freight = waybills
                .GroupBy(w => new { ForwarderID = w.ForwarderID.Value, w.CurrencyID, Day = DbFunctions.TruncateTime(w.IssueDate) })
                .Select(g => new { g.Key.ForwarderID, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(w => w.Amount) ?? 0, Trips = g.Count() })
                .ToList();

            var paid = payments
                .GroupBy(p => new { p.ForwarderID, p.CurrencyID, Day = DbFunctions.TruncateTime(p.PaymentDate) })
                .Select(g => new { g.Key.ForwarderID, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(p => p.Payment_Amount) })
                .ToList();

            var fin = manual
                .GroupBy(t => new { ForwarderID = t.CounterpartyID.Value, t.CurrencyID, Day = DbFunctions.TruncateTime(t.TxnDate) })
                .Select(g => new { g.Key.ForwarderID, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(t => t.Amount) })
                .ToList();

            var result = new Dictionary<int, ForwarderBalance>();
            Func<int, ForwarderBalance> row = id =>
            {
                ForwarderBalance b;
                if (!result.TryGetValue(id, out b))
                {
                    b = new ForwarderBalance { ForwarderID = id };
                    result[id] = b;
                }
                return b;
            };

            // Sanasiz yozuvlar — davr ichida (avvalgidek)
            Func<DateTime?, bool> inPeriod = day => !day.HasValue || day.Value >= start;

            foreach (var x in freight)
            {
                var b = row(x.ForwarderID);
                var uzs = money.ToUzs(x.Amount, x.CurrencyID, x.Day);
                if (inPeriod(x.Day))
                {
                    b.FreightInPeriod += uzs;
                    b.TripsInPeriod += x.Trips;
                }
                else
                {
                    b.FreightBefore += uzs;
                }
            }
            foreach (var x in paid)
            {
                var b = row(x.ForwarderID);
                var uzs = money.ToUzs(x.Amount, x.CurrencyID, x.Day);
                if (inPeriod(x.Day)) b.PaidInPeriod += uzs; else b.PaidBefore += uzs;
            }
            foreach (var x in fin)
            {
                var b = row(x.ForwarderID);
                var uzs = money.ToUzs(x.Amount, x.CurrencyID, x.Day);
                if (inPeriod(x.Day)) b.PaidInPeriod += uzs; else b.PaidBefore += uzs;
            }
            return result;
        }

        /// <summary>Bitta yuk tashuvchining umumiy balansi (barcha davr, so'mda).</summary>
        public static ForwarderBalance GetForwarderBalance(DBTHSNEntities db, int forwarderId, LogisticsMoney money = null)
        {
            ForwarderBalance b;
            return GetForwarderBalances(db, null, null, forwarderId, money).TryGetValue(forwarderId, out b)
                ? b
                : new ForwarderBalance { ForwarderID = forwarderId };
        }
    }

    public enum WaybillPaymentStatus
    {
        Unpaid = 0,
        Partial = 1,
        Paid = 2
    }

    public sealed class ForwarderBalance
    {
        public int ForwarderID { get; set; }
        public double FreightBefore { get; set; }
        public double FreightInPeriod { get; set; }
        public double PaidBefore { get; set; }
        public double PaidInPeriod { get; set; }
        public int TripsInPeriod { get; set; }

        public double OpeningDebt => FreightBefore - PaidBefore;
        public double ClosingDebt => OpeningDebt + FreightInPeriod - PaidInPeriod;
        public double TotalFreight => FreightBefore + FreightInPeriod;
        public double TotalPaid => PaidBefore + PaidInPeriod;
    }
}
