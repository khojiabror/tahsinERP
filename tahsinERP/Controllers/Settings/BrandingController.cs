using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers.Settings
{
    /// <summary>
    /// Korxona logosi (_Layout uchun). [Permission] yo'q — har kirgan foydalanuvchi ko'radi;
    /// yuklash/o'chirish Sozlamalar -> Korxona ma'lumotlari (CompanyController) orqali.
    /// </summary>
    public class BrandingController : Controller
    {
        private sealed class LogoRow
        {
            public byte[] LogoImage { get; set; }
            public string LogoContentType { get; set; }
        }

        [AllowAnonymous] // logo maxfiy emas; litsenziya/parol sahifalarida ham ko'rinsin
        [OutputCache(Duration = 600, VaryByParam = "v", Location = OutputCacheLocation.Client)]
        public ActionResult Logo(string v)
        {
            using (var db = new DBTHSNEntities())
            {
                LogoRow row = null;
                try
                {
                    row = db.Database.SqlQuery<LogoRow>(
                        "SELECT TOP 1 LogoImage, LogoContentType FROM dbo.COMPANIES WHERE ID = @p0",
                        LicenseService.InstallationCompanyId).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning("[Branding] logo o'qilmadi: " + ex.Message);
                }
                if (row?.LogoImage == null || row.LogoImage.Length == 0)
                    return Redirect(Url.Content("~/app-assets/images/logo/logo-32x32.png"));
                return File(row.LogoImage, string.IsNullOrEmpty(row.LogoContentType) ? "image/png" : row.LogoContentType);
            }
        }
    }
}
