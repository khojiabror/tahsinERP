using System.Web.Mvc;

namespace tahsinERP
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Barcha global filtrlar shu yerda, bajarilish tartibida (CORE-M01)
            filters.Add(new tahsinERP.I18n.UiLanguageFilter());           // Interfeys tili (uz/oz/ru) — binding va view'lardan oldin
            filters.Add(new HandleErrorAttribute());
            filters.Add(new AuthorizeAttribute());
            filters.Add(new tahsinERP.Filters.LicenseCheckAttribute());   // Litsenziya (company muddat) tekshiruvi
            filters.Add(new tahsinERP.Filters.MustChangePasswordAttribute()); // vaqtinchalik parol — avval almashtirish
            filters.Add(new tahsinERP.Filters.EditionCheckAttribute());   // Paket (Edition) bo'yicha modul cheklovi
        }
    }
}
