using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Valyuta kurslari (CURRENCY_RATES): ko'rish, qo'lda kiritish/tuzatish, CBU'dan davr bo'yicha yuklash (CRM-B18).
    /// Ruxsat — Currency spravochnigi bilan bir xil (ko'rish / o'zgartirish).
    /// </summary>
    [Permission(Controller = "Currency")]
    public class CurrencyRatesController : BaseController
    {
        private const int MaxFetchDays = 92;

        public sealed class RateRow
        {
            public int ID { get; set; }
            public int CurrencyID { get; set; }
            public DateTime RateDate { get; set; }
            public decimal Rate { get; set; }
            public string Source { get; set; }
        }

        public ActionResult Index(int? currencyId, DateTime? fromDate, DateTime? toDate)
        {
            var from = (fromDate ?? DateTime.Today.AddDays(-30)).Date;
            var to = (toDate ?? DateTime.Today).Date;
            if (from > to) { var t = from; from = to; to = t; }

            using (var db = new DBTHSNEntities())
            {
                bool table = CurrencyRateService.TableExists(db);
                var rows = new List<RateRow>();
                if (table)
                {
                    rows = db.Database.SqlQuery<RateRow>(
                        "SELECT ID, CurrencyID, CAST(RateDate AS datetime) AS RateDate, Rate, Source FROM dbo.CURRENCY_RATES " +
                        "WHERE RateDate >= @p0 AND RateDate <= @p1 AND (@p2 = 0 OR CurrencyID = @p2) ORDER BY RateDate DESC, CurrencyID",
                        from, to, currencyId ?? 0).ToList();
                }

                ViewBag.TableExists = table;
                ViewBag.Rows = rows;
                ViewBag.CurrencyNames = ReferenceDataCache.Get(ReferenceDataCache.Currencies).ToDictionary(c => c.ID, c => c.Name);
                ViewBag.Currencies = ForeignCurrencies(currencyId);
                ViewBag.CurrencyId = currencyId;
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View();
            }
        }

        /// <summary>Kursni qo'lda yozish (shu sana uchun mavjud bo'lsa almashtiriladi).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(int currencyId, DateTime rateDate, string rate)
        {
            decimal value;
            if (!CrmNumber.TryDecimal(rate, out value) || value <= 0)
            {
                TempData["ErrorMessage"] = "Kurs musbat son bo'lishi kerak.";
                return RedirectToAction("Index", new { currencyId });
            }

            using (var db = new DBTHSNEntities())
            {
                if (!CurrencyRateService.TableExists(db))
                {
                    TempData["ErrorMessage"] = "CURRENCY_RATES jadvali yo'q — avval Database_Migrations/CRM_2026_10_CurrencyRates.sql ni ishga tushiring.";
                    return RedirectToAction("Index");
                }

                CurrencyRateService.Save(db, currencyId, rateDate, value, "MANUAL");
                LogHelper.LogToDatabase(CurrentUserName, "CurrencyRates", $"Kurs: {ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, currencyId)} {rateDate:dd.MM.yyyy} = {value}");
            }

            TempData["SuccessMessage"] = "Kurs saqlandi.";
            return RedirectToAction("Index", new { currencyId });
        }

        /// <summary>Davr uchun yetishmayotgan kurslarni CBU'dan yuklash (har kun alohida so'rov).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FetchFromCbu(int currencyId, DateTime fromDate, DateTime toDate)
        {
            var from = fromDate.Date;
            var to = (toDate.Date > DateTime.Today ? DateTime.Today : toDate.Date);
            if (from > to || (to - from).TotalDays > MaxFetchDays)
            {
                TempData["ErrorMessage"] = $"Davr noto'g'ri yoki {MaxFetchDays} kundan uzun.";
                return RedirectToAction("Index", new { currencyId });
            }

            int saved = 0, failed = 0;
            using (var db = new DBTHSNEntities())
            {
                if (!CurrencyRateService.TableExists(db))
                {
                    TempData["ErrorMessage"] = "CURRENCY_RATES jadvali yo'q — avval migratsiya skriptini ishga tushiring.";
                    return RedirectToAction("Index");
                }

                var existing = new HashSet<DateTime>(db.Database.SqlQuery<DateTime>(
                    "SELECT CAST(RateDate AS datetime) FROM dbo.CURRENCY_RATES WHERE CurrencyID = @p0 AND RateDate >= @p1 AND RateDate <= @p2",
                    currencyId, from, to).ToList());

                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    if (existing.Contains(d)) continue;
                    var rate = CurrencyRateService.FetchFromCbu(currencyId, d);
                    if (rate.HasValue)
                    {
                        CurrencyRateService.Save(db, currencyId, d, rate.Value, "CBU");
                        saved++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            TempData[failed == 0 ? "SuccessMessage" : "ErrorMessage"] =
                $"CBU'dan {saved} ta kurs yuklandi" + (failed > 0 ? $", {failed} ta sana uchun javob olinmadi." : ".");
            return RedirectToAction("Index", new { currencyId, fromDate = from, toDate = to });
        }

        private static SelectList ForeignCurrencies(int? selected)
        {
            var items = ReferenceDataCache.Get(ReferenceDataCache.Currencies)
                .Where(c => !CurrencyRateService.IsBaseCode(c.Name))
                .ToList();
            return new SelectList(items, "ID", "Name", selected);
        }
    }
}
