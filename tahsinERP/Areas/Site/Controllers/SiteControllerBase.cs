using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using tahsinERP.Areas.Distribution.Controllers;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Models;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>
    /// Sayt admin controllerlari: distributsiya asosidan (ruxsat, til, DstJson) foydalanadi.
    /// Har saqlashdan keyin <see cref="Published"/> — ochiq sayt nusxasi keshdan chiqariladi (1 daqiqa ichida saytda).
    /// </summary>
    public abstract class SiteControllerBase : DstControllerBase
    {
        protected void Published() => SiteSnapshotCache.Invalidate();

        protected int? CurrentUserId => RoleHelper.GetUserId(CurrentUserName);

        protected static byte[] ReadUpload(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return null;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>Asosiy sayt manzili: tasdiqlangan asosiy domen, bo'lmasa standart subdomen ({tenant}.{SiteBaseDomain}).</summary>
        internal static string SiteUrl(DistributionDbContext db)
        {
            var primary = db.SiteDomains.Where(d => d.Status == (byte)DomainStatus.Active).OrderByDescending(d => d.IsPrimary)
                .Select(d => d.Host).FirstOrDefault();
            if (primary != null) return "https://" + primary + "/";
            var sub = DefaultSubdomain();
            return sub == null ? null : "https://" + sub + "/";
        }

        public static string DefaultSubdomain()
        {
            var baseDomain = ConfigurationManager.AppSettings["SiteBaseDomain"];
            if (string.IsNullOrWhiteSpace(baseDomain)) return null;
            return SiteText.Slugify(SiteSnapshotCache.CurrentTenant) + "." + baseDomain.Trim().TrimStart('.');
        }

        /// <summary>Kategoriyalar (sayt kontenti bilan) — tanlash ro'yxatlari uchun.</summary>
        protected static List<(int Id, string Name, bool Published)> CategoryList(DistributionDbContext db) =>
            (from c in db.Categories
             where c.IsDeleted != true
             orderby c.Name
             select new { c.ID, c.Name, Published = db.SiteCategories.Any(s => s.CategoryID == c.ID && s.IsPublished) })
            .ToList().Select(x => (x.ID, x.Name, x.Published)).ToList();

        protected static DateTime Now => DateTime.Now;

        protected static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        protected static Exception NotFound() => new Distribution.Services.DstValidationException(Distribution.Services.DstLocalization.T("Error_NotFound"));
    }
}
