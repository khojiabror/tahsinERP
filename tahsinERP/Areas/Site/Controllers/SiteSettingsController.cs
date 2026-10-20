using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>Sayt sozlamalari (brend, kontaktlar, narx va qoldiq ko'rinishi, SEO) va domenlar.</summary>
    public class SiteSettingsController : SiteControllerBase
    {
        public static readonly string[] Fonts = { "Inter", "Manrope", "Roboto", "Montserrat", "Nunito Sans" };

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Settings = SiteSettingsStore.All(db);
                ViewBag.Domains = db.SiteDomains.OrderByDescending(d => d.IsPrimary).ThenBy(d => d.Host).ToList();
                ViewBag.PriceTypes = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList()
                    .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.Name }).ToList();
                ViewBag.Subdomain = DefaultSubdomain();
                ViewBag.CnameTarget = ConfigurationManager.AppSettings["SiteCnameTarget"];
                ViewBag.SiteUrl = SiteUrl(db);
                return View();
            }
        }

        /// <summary>Barcha maydonlar bitta formada (forma nomlari = SiteSettingKeys).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(FormCollection form)
        {
            return DstJson(() =>
            {
                string V(string key) => form[key];
                var color = V(SiteSettingKeys.PrimaryColor);
                if (!string.IsNullOrWhiteSpace(color) && !SiteText.IsColor(color)) throw new DstValidationException(DstLocalization.T("Site_ColorFormat"));
                if (!PriceModes.All.Contains(V(SiteSettingKeys.PriceMode)) || !StockModes.All.Contains(V(SiteSettingKeys.StockMode)))
                    throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                var langs = (V(SiteSettingKeys.Languages) ?? "").Split(',').Select(l => l.Trim()).Where(l => l == "uz" || l == "ru").Distinct().ToList();
                if (langs.Count == 0) throw new DstValidationException(DstLocalization.T("Site_LanguageRequired"));
                if (!Fonts.Contains(V(SiteSettingKeys.Font) ?? "Inter")) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                if (!string.IsNullOrWhiteSpace(V(SiteSettingKeys.Phone))) DirectoryRules.NormalizePhone(V(SiteSettingKeys.Phone));
                if (!string.IsNullOrWhiteSpace(V(SiteSettingKeys.Phone2))) DirectoryRules.NormalizePhone(V(SiteSettingKeys.Phone2));
                if (!string.IsNullOrWhiteSpace(V(SiteSettingKeys.MapLat)) || !string.IsNullOrWhiteSpace(V(SiteSettingKeys.MapLng)))
                    DirectoryRules.ValidateGeo(Dec(V(SiteSettingKeys.MapLat)) ?? throw Bad(), Dec(V(SiteSettingKeys.MapLng)) ?? throw Bad());
                int radius = int.TryParse(V(SiteSettingKeys.Radius), out var r) ? Math.Max(0, Math.Min(24, r)) : 10;
                decimal low = Dec(V(SiteSettingKeys.LowStockQty)) ?? 10;

                using (var db = NewDb())
                {
                    if (int.TryParse(V(SiteSettingKeys.PriceTypeId), out var pt) && !db.PriceTypes.Any(t => t.ID == pt))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var now = Now;
                    void Set(string key, string value, int max = 300) => SiteSettingsStore.Set(db, key, SiteText.Clean(value, max), now);
                    Set(SiteSettingKeys.Enabled, form[SiteSettingKeys.Enabled] == "true" || form[SiteSettingKeys.Enabled] == "1" ? "1" : "0");
                    Set(SiteSettingKeys.Name, V(SiteSettingKeys.Name), 100);
                    Set(SiteSettingKeys.Tagline, V(SiteSettingKeys.Tagline), 200);
                    Set(SiteSettingKeys.PrimaryColor, color);
                    Set(SiteSettingKeys.Font, V(SiteSettingKeys.Font));
                    Set(SiteSettingKeys.Radius, radius.ToString());
                    Set(SiteSettingKeys.Languages, string.Join(",", langs));
                    Set(SiteSettingKeys.Phone, string.IsNullOrWhiteSpace(V(SiteSettingKeys.Phone)) ? null : DirectoryRules.NormalizePhone(V(SiteSettingKeys.Phone)));
                    Set(SiteSettingKeys.Phone2, string.IsNullOrWhiteSpace(V(SiteSettingKeys.Phone2)) ? null : DirectoryRules.NormalizePhone(V(SiteSettingKeys.Phone2)));
                    Set(SiteSettingKeys.Email, V(SiteSettingKeys.Email), 100);
                    Set(SiteSettingKeys.Address, V(SiteSettingKeys.Address), 300);
                    Set(SiteSettingKeys.WorkingHours, V(SiteSettingKeys.WorkingHours), 100);
                    Set(SiteSettingKeys.MapLat, V(SiteSettingKeys.MapLat));
                    Set(SiteSettingKeys.MapLng, V(SiteSettingKeys.MapLng));
                    Set(SiteSettingKeys.Telegram, V(SiteSettingKeys.Telegram), 100);
                    Set(SiteSettingKeys.Instagram, V(SiteSettingKeys.Instagram), 100);
                    Set(SiteSettingKeys.Facebook, V(SiteSettingKeys.Facebook), 100);
                    Set(SiteSettingKeys.PriceMode, V(SiteSettingKeys.PriceMode));
                    Set(SiteSettingKeys.PriceTypeId, V(SiteSettingKeys.PriceTypeId));
                    Set(SiteSettingKeys.StockMode, V(SiteSettingKeys.StockMode));
                    Set(SiteSettingKeys.LowStockQty, low.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Set(SiteSettingKeys.LeadsEnabled, form[SiteSettingKeys.LeadsEnabled] == "true" || form[SiteSettingKeys.LeadsEnabled] == "1" ? "1" : "0");
                    Set(SiteSettingKeys.SeoTitle, V(SiteSettingKeys.SeoTitle), 150);
                    Set(SiteSettingKeys.SeoDescription, V(SiteSettingKeys.SeoDescription), 300);
                    Set(SiteSettingKeys.AnalyticsId, V(SiteSettingKeys.AnalyticsId), 50);
                    Set(SiteSettingKeys.MetrikaId, V(SiteSettingKeys.MetrikaId), 50);
                    db.SaveChanges();
                }
                Published();
                return null;
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult UploadLogo(HttpPostedFileBase file)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var old = int.TryParse(SiteSettingsStore.Get(SiteSettingsStore.All(db), SiteSettingKeys.LogoMediaId), out var o) ? o : (int?)null;
                    var media = SiteMediaService.Save(db, ReadUpload(file), CurrentUserId, Now);
                    SiteSettingsStore.Set(db, SiteSettingKeys.LogoMediaId, media.ID.ToString(), Now);
                    db.SaveChanges();
                    if (old.HasValue && old != media.ID) { SiteMediaService.DeleteIfUnused(db, old.Value); db.SaveChanges(); }
                    Published();
                    return new { id = media.ID };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult RemoveLogo()
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var old = int.TryParse(SiteSettingsStore.Get(SiteSettingsStore.All(db), SiteSettingKeys.LogoMediaId), out var o) ? o : (int?)null;
                    SiteSettingsStore.Set(db, SiteSettingKeys.LogoMediaId, null, Now);
                    db.SaveChanges();
                    if (old.HasValue) { SiteMediaService.DeleteIfUnused(db, old.Value); db.SaveChanges(); }
                    Published();
                    return null;
                }
            });
        }

        // ---------- domenlar ----------

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult AddDomain(string host)
        {
            return DstJson(() =>
            {
                var h = SiteText.NormalizeHost(host) ?? throw new DstValidationException(DstLocalization.T("Site_DomainFormat"));
                var baseDomain = ConfigurationManager.AppSettings["SiteBaseDomain"];
                if (!string.IsNullOrWhiteSpace(baseDomain) && (h == baseDomain || h.EndsWith("." + baseDomain.Trim().TrimStart('.'))))
                    throw new DstValidationException(DstLocalization.T("Site_DomainReserved"));
                using (var db = NewDb())
                {
                    if (db.SiteDomains.Any(d => d.Host == h)) throw new DstValidationException(DstLocalization.T("Error_Duplicate"));
                    db.SiteDomains.Add(new SiteDomain { Host = h, IsPrimary = !db.SiteDomains.Any(), Status = (byte)DomainStatus.Pending, CreatedAt = Now });
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>
        /// DNS tekshiruvi: domen sayt serveriga (SiteCnameTarget) qaraganini IP manzillar bo'yicha solishtiradi.
        /// Mos kelsa — faol (sayt shu domenni qabul qila boshlaydi).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult VerifyDomain(int id)
        {
            return DstJson(() =>
            {
                var target = ConfigurationManager.AppSettings["SiteCnameTarget"];
                if (string.IsNullOrWhiteSpace(target)) throw new DstValidationException(DstLocalization.T("Site_DomainNoTarget"));
                using (var db = NewDb())
                {
                    var d = db.SiteDomains.Find(id) ?? throw NotFound();
                    bool ok;
                    try
                    {
                        var expected = Dns.GetHostAddresses(target.Trim()).Select(a => a.ToString()).ToList();
                        ok = expected.Count > 0 && Dns.GetHostAddresses(d.Host).Select(a => a.ToString()).Any(expected.Contains);
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                        ok = false;
                    }
                    if (!ok) throw new DstValidationException(DstLocalization.T("Site_DomainNotPointing", d.Host, target));
                    d.Status = (byte)DomainStatus.Active;
                    d.VerifiedAt = Now;
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SetPrimaryDomain(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var target = db.SiteDomains.Find(id) ?? throw NotFound();
                    foreach (var d in db.SiteDomains.ToList()) d.IsPrimary = d.ID == target.ID;
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult RemoveDomain(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var d = db.SiteDomains.Find(id) ?? throw NotFound();
                    db.SiteDomains.Remove(d);
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        private static decimal? Dec(string s) =>
            decimal.TryParse((s ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null;

        private static Exception Bad() => new DstValidationException(DstLocalization.T("Error_NumberFormat"));
    }
}
