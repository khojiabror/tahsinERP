using System;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Crm
{
    /// <summary>Hisobot davri: kunlar to'liq kiradi, so'rovlar uchun yuqori chegara yarim ochiq.</summary>
    public struct ReportPeriod
    {
        public ReportPeriod(DateTime from, DateTime to)
        {
            From = from.Date;
            To = to.Date;
        }

        public DateTime From { get; }
        public DateTime To { get; }
        public DateTime ToExclusive => To.AddDays(1);
    }

    /// <summary>
    /// Sotuv hisobotlari va CRM dashboard'ning umumiy boshlanishi (CRM-D23): ruxsat, davrni
    /// normallashtirish (bo'sh bo'lsa standart, teskari bo'lsa almashtirish) va kompaniya nomi.
    /// </summary>
    public static class CrmReport
    {
        /// <summary>Foydalanuvchida sanab o'tilgan controller'lardan kamida biriga ko'rish ruxsati bormi.</summary>
        public static bool AnyViewPermitted(string userName, params string[] controllers)
        {
            return controllers.Any(c => RoleHelper.IsViewPermitted(userName, c));
        }

        public static ReportPeriod Period(DateTime? fromDate, DateTime? toDate, DateTime defaultFrom)
        {
            var from = fromDate ?? defaultFrom;
            var to = toDate ?? DateTime.Now.Date;
            return from > to ? new ReportPeriod(to, from) : new ReportPeriod(from, to);
        }

        /// <summary>Joriy oy boshidan bugungacha.</summary>
        public static ReportPeriod Period(DateTime? fromDate, DateTime? toDate)
        {
            var now = DateTime.Now;
            return Period(fromDate, toDate, new DateTime(now.Year, now.Month, 1));
        }

        public static string CompanyName(DBTHSNEntities db)
        {
            return db.COMPANIES.Select(c => c.Name).FirstOrDefault() ?? "Tahsin ERP";
        }
    }
}
