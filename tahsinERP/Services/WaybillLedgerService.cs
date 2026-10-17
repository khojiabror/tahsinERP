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
    ///  - Valyutalar konvertatsiya qilinmaydi (LOG-B13 — biznes qarori kutilmoqda), summalar qo'shiladi.
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
        public static Dictionary<int, double> GetPaidLookup(DBTHSNEntities db, IQueryable<int> waybillIds, int? excludePaymentId = null)
        {
            var direct = db.F_WAYBILL_PAYMENTS
                .Where(p => !p.IsDeleted
                            && waybillIds.Contains(p.WaybillID)
                            && (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value)
                            && !db.F_WAYBILL_PAYMENT_SPLITS.Any(s => s.PayerType == SplitPayerType && s.PayerID == p.ID))
                .GroupBy(p => p.WaybillID)
                .Select(g => new { WaybillID = g.Key, Amount = g.Sum(x => x.Payment_Amount) })
                .ToList();

            var split = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                         join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                         where s.PayerType == SplitPayerType
                               && !p.IsDeleted
                               && waybillIds.Contains(s.WaybillID)
                               && (!excludePaymentId.HasValue || p.ID != excludePaymentId.Value)
                         group s by s.WaybillID into g
                         select new { WaybillID = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToList();

            var result = new Dictionary<int, double>();
            foreach (var item in direct.Concat(split))
            {
                double current;
                result.TryGetValue(item.WaybillID, out current);
                result[item.WaybillID] = current + item.Amount;
            }
            return result;
        }

        /// <summary>Kichik ro'yxat (sahifa, tanlangan yo'l varaqalari) uchun.</summary>
        public static Dictionary<int, double> GetPaidLookup(DBTHSNEntities db, ICollection<int> waybillIds, int? excludePaymentId = null)
        {
            if (waybillIds == null || waybillIds.Count == 0)
                return new Dictionary<int, double>();
            var ids = waybillIds.Distinct().ToList();
            return GetPaidLookup(db, db.F_WAYBILLS.Where(w => ids.Contains(w.ID)).Select(w => w.ID), excludePaymentId);
        }

        public static double GetPaidTotal(DBTHSNEntities db, int waybillId, int? excludePaymentId = null)
        {
            double paid;
            GetPaidLookup(db, new[] { waybillId }, excludePaymentId).TryGetValue(waybillId, out paid);
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
        public static Dictionary<int, double> Allocate(DBTHSNEntities db, IList<int> waybillIds, double paymentAmount, int? paymentId)
        {
            var result = new Dictionary<int, double>();
            if (waybillIds == null || waybillIds.Count == 0 || paymentAmount <= 0)
                return result;

            var ids = waybillIds.Distinct().ToList();
            var amounts = db.F_WAYBILLS
                .Where(w => ids.Contains(w.ID))
                .Select(w => new { w.ID, w.Amount })
                .ToList()
                .ToDictionary(x => x.ID, x => x.Amount ?? 0);
            var paid = GetPaidLookup(db, ids, paymentId);

            var existing = ids.Where(amounts.ContainsKey).ToList();
            if (existing.Count == 0) return result;

            var remaining = paymentAmount;
            foreach (var id in existing)
            {
                if (remaining <= 0) break;
                var outstanding = Math.Max(0, amounts[id] - PaidOf(paid, id));
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
            double paymentAmount, int? currencyId, string comment)
        {
            var oldSplits = db.F_WAYBILL_PAYMENT_SPLITS
                .Where(s => s.PayerType == SplitPayerType && s.PayerID == paymentId)
                .ToList();
            if (oldSplits.Count > 0)
                db.F_WAYBILL_PAYMENT_SPLITS.RemoveRange(oldSplits);

            // Taqsimot eski split'larni hisobga olmasligi uchun paymentId chiqarib tashlanadi
            var alloc = Allocate(db, waybillIds, paymentAmount, paymentId);
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
            DateTime? periodStart, DateTime? periodEnd, int? forwarderId = null)
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

            var freight = waybills
                .GroupBy(w => w.ForwarderID.Value)
                .Select(g => new
                {
                    ForwarderID = g.Key,
                    Before = g.Sum(w => w.IssueDate < start ? (w.Amount ?? 0) : 0),
                    InPeriod = g.Sum(w => (w.IssueDate == null || w.IssueDate >= start) ? (w.Amount ?? 0) : 0),
                    Trips = g.Sum(w => (w.IssueDate == null || w.IssueDate >= start) ? 1 : 0)
                })
                .ToList();

            var paid = payments
                .GroupBy(p => p.ForwarderID)
                .Select(g => new
                {
                    ForwarderID = g.Key,
                    Before = g.Sum(p => p.PaymentDate < start ? p.Payment_Amount : 0),
                    InPeriod = g.Sum(p => (p.PaymentDate == null || p.PaymentDate >= start) ? p.Payment_Amount : 0)
                })
                .ToList();

            var fin = manual
                .GroupBy(t => t.CounterpartyID.Value)
                .Select(g => new
                {
                    ForwarderID = g.Key,
                    Before = g.Sum(t => t.TxnDate < start ? t.Amount : 0m),
                    InPeriod = g.Sum(t => t.TxnDate >= start ? t.Amount : 0m)
                })
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

            foreach (var x in freight)
            {
                var b = row(x.ForwarderID);
                b.FreightBefore += x.Before;
                b.FreightInPeriod += x.InPeriod;
                b.TripsInPeriod += x.Trips;
            }
            foreach (var x in paid)
            {
                var b = row(x.ForwarderID);
                b.PaidBefore += x.Before;
                b.PaidInPeriod += x.InPeriod;
            }
            foreach (var x in fin)
            {
                var b = row(x.ForwarderID);
                b.PaidBefore += (double)x.Before;
                b.PaidInPeriod += (double)x.InPeriod;
            }
            return result;
        }

        /// <summary>Bitta yuk tashuvchining umumiy balansi (barcha davr).</summary>
        public static ForwarderBalance GetForwarderBalance(DBTHSNEntities db, int forwarderId)
        {
            ForwarderBalance b;
            return GetForwarderBalances(db, null, null, forwarderId).TryGetValue(forwarderId, out b)
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
