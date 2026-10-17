using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// Valyuta kursi (1 birlik = N so'm) va so'mga konvertatsiya (CRM-B18, foydalanuvchi qarori:
    /// manba — CBU + CURRENCY_RATES jadvali, asos — UZS, hujjat sanasidagi kurs).
    ///
    /// Kurs qidirish tartibi:
    ///  1) CURRENCY_RATES (CurrencyID, RateDate) — qo'lda kiritilgan yoki avval CBU'dan saqlangan;
    ///  2) cbu.uz arxiv API — topilsa jadvalga yoziladi (jadval bo'lmasa faqat xotirada keshlanadi);
    ///  3) internet/CBU javob bermasa — jadvaldagi eng yaqin OLDINGI kurs.
    /// Topilmasa null — chaqiruvchi summani konvertatsiyasiz qo'shmaydi va ogohlantiradi.
    ///
    /// Jadval Database_Migrations/CRM_2026_10_CurrencyRates.sql bilan yaratiladi; yaratilmagan bo'lsa
    /// servis faqat CBU + xotira keshi bilan ishlaydi (raw SQL — EDMX o'zgarmagan).
    /// Umumiy qatlam: Finance (FIN-B01) va Logistika (LOG-B13) ham shu servisdan foydalanishi mumkin —
    /// Core sessiyasi uni Services/Common'ga ko'chirishi kerak.
    /// </summary>
    public static class CurrencyRateService
    {
        private const string CbuUrl = "https://cbu.uz/uz/arkhiv-kursov-valyut/json/{0}/{1:yyyy-MM-dd}/";
        private static readonly string[] BaseCodes = { "UZS", "SUM", "SO'M", "SOM", "СУМ", "UZB" };
        private static readonly Regex IsoCode = new Regex("^[A-Z]{3}$", RegexOptions.Compiled);
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // (CurrencyID, sana) -> kurs; null = topilmadi (qisqa muddat qayta so'ralmaydi)
        private static readonly ConcurrentDictionary<string, Tuple<decimal?, DateTime>> Memory =
            new ConcurrentDictionary<string, Tuple<decimal?, DateTime>>();
        private static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(10);

        private static bool? _tableExists;
        private static DateTime _tableCheckedAt;

        /// <summary>Asos valyuta (so'm) ID'si; topilmasa null.</summary>
        public static int? BaseCurrencyId()
        {
            return ReferenceDataCache.Get(ReferenceDataCache.Currencies)
                .Where(c => IsBaseCode(c.Name))
                .Select(c => (int?)c.ID)
                .FirstOrDefault();
        }

        public static bool IsBaseCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code) && BaseCodes.Contains(code.Trim().ToUpperInvariant());
        }

        /// <summary>Qisqa kod ("USD") bo'yicha valyuta ID'si.</summary>
        public static int? CurrencyIdByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var c = code.Trim();
            return ReferenceDataCache.Get(ReferenceDataCache.Currencies)
                .Where(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase))
                .Select(x => (int?)x.ID)
                .FirstOrDefault();
        }

        public static bool TableExists(DBTHSNEntities db)
        {
            // 5 daqiqada qayta tekshiriladi — migratsiyadan keyin qayta ishga tushirish shart emas
            if (_tableExists.HasValue && DateTime.Now - _tableCheckedAt < TimeSpan.FromMinutes(5))
                return _tableExists.Value;
            try
            {
                _tableExists = db.Database.SqlQuery<int>(
                    "SELECT CASE WHEN OBJECT_ID('dbo.CURRENCY_RATES','U') IS NOT NULL THEN 1 ELSE 0 END").FirstOrDefault() == 1;
            }
            catch
            {
                _tableExists = false;
            }
            _tableCheckedAt = DateTime.Now;
            return _tableExists.Value;
        }

        /// <summary>
        /// Valyutaning sanadagi kursi (1 birlik necha so'm). <paramref name="allowLiveFetch"/> = false bo'lsa
        /// CBU'ga murojaat qilinmaydi (so'rovlar soni cheklanganda).
        /// </summary>
        public static decimal? GetRate(DBTHSNEntities db, int currencyId, DateTime date, bool allowLiveFetch = true)
        {
            date = date.Date;
            if (currencyId == BaseCurrencyId())
                return 1m;

            var key = currencyId + "|" + date.ToString("yyyyMMdd");
            Tuple<decimal?, DateTime> cached;
            if (Memory.TryGetValue(key, out cached) && (cached.Item1.HasValue || DateTime.Now - cached.Item2 < MissTtl))
                return cached.Item1;

            bool table = TableExists(db);
            decimal? rate = null;

            if (table)
            {
                rate = db.Database.SqlQuery<decimal?>(
                    "SELECT TOP 1 Rate FROM dbo.CURRENCY_RATES WHERE CurrencyID = @p0 AND RateDate = @p1",
                    currencyId, date).FirstOrDefault();
            }

            if (!rate.HasValue && allowLiveFetch && date <= DateTime.Today)
            {
                rate = FetchFromCbu(currencyId, date);
                if (rate.HasValue && table)
                    Save(db, currencyId, date, rate.Value, "CBU");
            }

            if (!rate.HasValue && table)
            {
                // Internet yo'q yoki CBU'da bu valyuta yo'q — eng yaqin oldingi kurs (keshga yozilmaydi)
                var previous = db.Database.SqlQuery<decimal?>(
                    "SELECT TOP 1 Rate FROM dbo.CURRENCY_RATES WHERE CurrencyID = @p0 AND RateDate <= @p1 ORDER BY RateDate DESC",
                    currencyId, date).FirstOrDefault();
                return previous;
            }

            Memory[key] = Tuple.Create(rate, DateTime.Now);
            return rate;
        }

        /// <summary>Kursni qo'lda yozish/almashtirish (CurrencyRates sahifasi).</summary>
        public static void Save(DBTHSNEntities db, int currencyId, DateTime date, decimal rate, string source)
        {
            date = date.Date;
            db.Database.ExecuteSqlCommand(
                "IF EXISTS (SELECT 1 FROM dbo.CURRENCY_RATES WHERE CurrencyID = @p0 AND RateDate = @p1) " +
                "  UPDATE dbo.CURRENCY_RATES SET Rate = @p2, Source = @p3, UpdatedDT = GETDATE() WHERE CurrencyID = @p0 AND RateDate = @p1 " +
                "ELSE " +
                "  INSERT INTO dbo.CURRENCY_RATES (CurrencyID, RateDate, Rate, Source, CreatedDT) VALUES (@p0, @p1, @p2, @p3, GETDATE())",
                currencyId, date, rate, source);
            Memory[currencyId + "|" + date.ToString("yyyyMMdd")] = Tuple.Create((decimal?)rate, DateTime.Now);
        }

        public static void Forget(int currencyId, DateTime date)
        {
            Tuple<decimal?, DateTime> removed;
            Memory.TryRemove(currencyId + "|" + date.Date.ToString("yyyyMMdd"), out removed);
        }

        /// <summary>cbu.uz arxivi: "Rate" — "Nominal" birlik uchun so'm.</summary>
        public static decimal? FetchFromCbu(int currencyId, DateTime date)
        {
            var code = ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, currencyId)?.Trim().ToUpperInvariant();
            if (code == null || !IsoCode.IsMatch(code))
                return null;

            try
            {
                var json = Http.GetStringAsync(string.Format(CultureInfo.InvariantCulture, CbuUrl, code, date))
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                var rows = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CbuRow>>(json);
                var row = rows?.FirstOrDefault(r => string.Equals(r.Ccy, code, StringComparison.OrdinalIgnoreCase));
                if (row == null) return null;

                decimal rate, nominal;
                if (!decimal.TryParse(row.Rate, NumberStyles.Float, CultureInfo.InvariantCulture, out rate) || rate <= 0)
                    return null;
                if (!decimal.TryParse(row.Nominal, NumberStyles.Float, CultureInfo.InvariantCulture, out nominal) || nominal <= 0)
                    nominal = 1;
                return rate / nominal;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("[CurrencyRateService] CBU {0} {1:yyyy-MM-dd}: {2}", code, date, ex.Message);
                return null;
            }
        }

        private sealed class CbuRow
        {
            public string Ccy { get; set; }
            public string Rate { get; set; }
            public string Nominal { get; set; }
        }
    }

    /// <summary>
    /// Bitta sahifa/hisobot uchun so'mga konvertor. CBU'ga jonli so'rovlar soni cheklanadi
    /// (birinchi ochilishda yuzlab sana bo'lsa sahifa osilib qolmasligi uchun) — limitdan keyin
    /// jadvaldagi eng yaqin oldingi kurs ishlatiladi. Kurs topilmagan hujjatlar summaga qo'shilmaydi
    /// va <see cref="Warning"/> orqali ko'rsatiladi.
    /// </summary>
    public sealed class UzsConverter
    {
        private readonly DBTHSNEntities _db;
        private readonly Dictionary<string, decimal?> _rates = new Dictionary<string, decimal?>();
        private readonly HashSet<string> _missingCurrencies = new HashSet<string>();
        private int _liveFetchesLeft;

        public UzsConverter(DBTHSNEntities db, int liveFetchBudget = 30)
        {
            _db = db;
            _liveFetchesLeft = liveFetchBudget;
            BaseCurrencyId = CurrencyRateService.BaseCurrencyId();
        }

        public int? BaseCurrencyId { get; }

        /// <summary>Kursi topilmagan hujjatlar soni.</summary>
        public int MissingCount { get; private set; }

        /// <summary>Summani so'mga o'giradi; valyuta noma'lum (0/null) bo'lsa so'm deb olinadi. Kurs topilmasa null.</summary>
        public decimal? ToUzs(decimal amount, int? currencyId, DateTime date)
        {
            if (amount == 0) return 0m;
            if (!currencyId.HasValue || currencyId.Value <= 0 || currencyId == BaseCurrencyId)
                return amount;

            var key = currencyId.Value + "|" + date.Date.ToString("yyyyMMdd");
            decimal? rate;
            if (!_rates.TryGetValue(key, out rate))
            {
                bool live = _liveFetchesLeft > 0;
                rate = CurrencyRateService.GetRate(_db, currencyId.Value, date, live);
                if (live) _liveFetchesLeft--;
                _rates[key] = rate;
            }

            if (!rate.HasValue)
            {
                MissingCount++;
                _missingCurrencies.Add(ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, currencyId) ?? ("#" + currencyId));
                return null;
            }
            return amount * rate.Value;
        }

        /// <summary>Valyuta kodi bo'yicha (SALES_INVOICES.CurrencyCode).</summary>
        public decimal? ToUzs(decimal amount, string currencyCode, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) || CurrencyRateService.IsBaseCode(currencyCode))
                return amount;
            var id = CurrencyRateService.CurrencyIdByCode(currencyCode);
            if (!id.HasValue)
            {
                MissingCount++;
                _missingCurrencies.Add(currencyCode);
                return null;
            }
            return ToUzs(amount, id, date);
        }

        public decimal ToUzsOrZero(decimal amount, int? currencyId, DateTime date) => ToUzs(amount, currencyId, date) ?? 0m;

        public decimal ToUzsOrZero(decimal amount, string currencyCode, DateTime date) => ToUzs(amount, currencyCode, date) ?? 0m;

        /// <summary>Foydalanuvchiga ogohlantirish (kurs topilmagan bo'lsa), aks holda null.</summary>
        public string Warning
        {
            get
            {
                if (MissingCount == 0) return null;
                return $"{MissingCount} ta hujjat uchun valyuta kursi topilmadi ({string.Join(", ", _missingCurrencies)}) — " +
                       "ular so'mdagi jamiga qo'shilmadi. Kursni \"Valyuta kurslari\" sahifasida kiriting.";
            }
        }
    }
}
