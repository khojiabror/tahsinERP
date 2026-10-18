using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// FIN-B01 (foydalanuvchi qarori): Finance hisobotlari so'mda — har summa hujjat (tranzaksiya) sanasidagi
    /// CBU kursi bo'yicha o'giriladi (Services/Common/CurrencyRateService + UzsConverter). Kurs topilmagan summa
    /// jamiga qo'shilmaydi va <see cref="Warning"/> orqali ko'rsatiladi.
    /// Bitta DB konteksti (so'rov) uchun bitta konvertor: <see cref="For"/>.
    /// </summary>
    public sealed class FinMoney
    {
        private static readonly ConditionalWeakTable<DBTHSNEntities, FinMoney> PerContext = new ConditionalWeakTable<DBTHSNEntities, FinMoney>();

        private readonly UzsConverter _uzs;

        private const string RequestKey = "tahsinERP.FinMoney.Instances";

        private FinMoney(DBTHSNEntities db)
        {
            _uzs = new UzsConverter(db);
            // So'rov davomidagi barcha konvertorlar — sahifa "kurs topilmadi" ogohlantirishini ko'rsatishi uchun
            var items = System.Web.HttpContext.Current?.Items;
            if (items != null)
            {
                if (!(items[RequestKey] is List<FinMoney> list))
                {
                    list = new List<FinMoney>();
                    items[RequestKey] = list;
                }
                list.Add(this);
            }
            BaseCurrencyId = _uzs.BaseCurrencyId;
            BaseShortname = BaseCurrencyId.HasValue
                ? db.CURRENCIES.AsNoTracking().Where(c => c.ID == BaseCurrencyId.Value).Select(c => c.Shortname).FirstOrDefault() ?? "UZS"
                : "UZS";
        }

        /// <summary>Shu kontekst uchun konvertor (kurslar keshi va ogohlantirish bitta joyda yig'iladi).</summary>
        public static FinMoney For(DBTHSNEntities db)
        {
            return PerContext.GetValue(db, d => new FinMoney(d));
        }

        public int? BaseCurrencyId { get; }

        /// <summary>Hisobotlar valyutasi (so'm).</summary>
        public string BaseShortname { get; }

        /// <summary>Summani so'mga; valyuta noma'lum yoki asos bo'lsa o'zgarmaydi; kurs topilmasa 0 (ogohlantirish bilan).</summary>
        public decimal Uzs(decimal amount, int? currencyId, DateTime date)
        {
            return _uzs.ToUzsOrZero(amount, currencyId, date);
        }

        /// <summary>Kurs topilmagan hujjatlar haqida ogohlantirish yoki null.</summary>
        public string Warning => _uzs.Warning;

        /// <summary>Joriy so'rovdagi barcha konvertorlarning ogohlantirishlari (Finance sahifalari ko'rsatadi).</summary>
        public static List<string> RequestWarnings()
        {
            var list = System.Web.HttpContext.Current?.Items[RequestKey] as List<FinMoney>;
            return list == null
                ? new List<string>()
                : list.Select(m => m.Warning).Where(w => !string.IsNullOrEmpty(w)).Distinct().ToList();
        }

        /// <summary>Asosiy valyuta (so'm) ID'si; topilmasa — bazadagi birinchi valyuta.</summary>
        public static int BaseCurrencyIdOrFirst(DBTHSNEntities db)
        {
            return CurrencyRateService.BaseCurrencyId() ?? db.CURRENCIES.Select(c => c.ID).OrderBy(id => id).FirstOrDefault();
        }
    }

    /// <summary>Tranzaksiyalar guruhi (kategoriya × hisob × yo'nalish × kun × valyuta), summa so'mda.</summary>
    public sealed class TxnBucket
    {
        public int CategoryID { get; set; }
        public int AccountID { get; set; }
        public byte Direction { get; set; }
        public DateTime Day { get; set; }
        public int CurrencyID { get; set; }
        /// <summary>Hujjat valyutasidagi summa.</summary>
        public decimal Amount { get; set; }
        /// <summary>So'mdagi summa (kun kursi bo'yicha).</summary>
        public decimal Uzs { get; set; }
        public int Count { get; set; }
        public decimal SignedUzs => Direction == 1 ? Uzs : -Uzs;
    }

    public static class FinMoneyQueries
    {
        /// <summary>
        /// Tranzaksiyalarni SQL'da (kategoriya, hisob, yo'nalish, valyuta, kun) bo'yicha guruhlab, har guruhni o'sha kun
        /// kursida so'mga o'giradi. Hisobotlar shu guruhlardan xotirada yig'iladi.
        /// </summary>
        public static List<TxnBucket> UzsBuckets(this IQueryable<FIN_TRANSACTIONS> query, DBTHSNEntities db)
        {
            var money = FinMoney.For(db);
            return query
                .GroupBy(t => new { t.CategoryID, t.AccountID, t.Direction, t.CurrencyID, Day = DbFunctions.TruncateTime(t.TxnDate) })
                .Select(g => new { g.Key.CategoryID, g.Key.AccountID, g.Key.Direction, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(t => t.Amount), Count = g.Count() })
                .ToList()
                .Select(x => new TxnBucket
                {
                    CategoryID = x.CategoryID,
                    AccountID = x.AccountID,
                    Direction = x.Direction,
                    CurrencyID = x.CurrencyID,
                    Day = x.Day.Value,
                    Amount = x.Amount,
                    Uzs = money.Uzs(x.Amount, x.CurrencyID, x.Day.Value),
                    Count = x.Count
                })
                .ToList();
        }

        /// <summary>Σ so'mda: kirim (+), chiqim (−).</summary>
        public static decimal NetUzs(this IQueryable<FIN_TRANSACTIONS> query, DBTHSNEntities db)
        {
            return query.UzsBuckets(db).Sum(b => b.SignedUzs);
        }

        /// <summary>Σ so'mda (yo'nalishdan qat'i nazar).</summary>
        public static decimal SumUzs(this IQueryable<FIN_TRANSACTIONS> query, DBTHSNEntities db)
        {
            return query.UzsBuckets(db).Sum(b => b.Uzs);
        }
    }
}
