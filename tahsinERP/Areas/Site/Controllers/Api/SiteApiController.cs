using System;
using System.Net;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using tahsinERP.Areas.Distribution.Controllers.Api;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Services;

namespace tahsinERP.Areas.Site.Controllers.Api
{
    /// <summary>
    /// Ochiq sayt servisi (tahsinERP.Site) uchun: /Site/SiteApi/... Faqat imzoli so'rovlar (DstApiSignature, bot bilan bir xil kalit),
    /// litsenziya va "Sayt" qo'shimcha xizmati tekshiriladi. Vakil foydalanuvchi yo'q — faqat ochiq ma'lumot va ariza qabul qilish.
    /// </summary>
    [AllowAnonymous]
    [DstApiAuth(RequiredModule = "Website")]
    public class SiteApiController : Controller
    {
        private DistributionDbContext Db() => new DistributionDbContext { Actor = new AuditActor(null, "site") };

        private ActionResult JsonContent(string json) => Content(json, "application/json", Encoding.UTF8);

        private ActionResult Error(HttpStatusCode code, string message)
        {
            Response.StatusCode = (int)code;
            Response.TrySkipIisCustomErrors = true;
            return JsonContent(JsonConvert.SerializeObject(new { error = message }));
        }

        /// <summary>Katalog nusxasi. If-None-Match mos kelsa — 304 (sayt har daqiqada so'raydi).</summary>
        [HttpGet]
        public ActionResult Snapshot()
        {
            var tenant = SiteSnapshotCache.CurrentTenant;
            var snap = SiteSnapshotCache.Get(tenant, () =>
            {
                using (var db = Db())
                    return SiteSnapshotBuilder.Serialize(SiteSnapshotBuilder.Build(db, DateTime.Now));
            });
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.AppendHeader("ETag", snap.ETag);
            if (string.Equals(Request.Headers["If-None-Match"], snap.ETag, StringComparison.Ordinal))
                return new HttpStatusCodeResult(HttpStatusCode.NotModified);
            return JsonContent(snap.Json);
        }

        /// <summary>Rasm (asl nusxa). Hash — o'zgarmas, sayt keshlaydi.</summary>
        [HttpGet]
        public ActionResult Media(int id)
        {
            using (var db = Db())
            {
                var m = db.SiteMedia.Find(id);
                if (m == null) return Error(HttpStatusCode.NotFound, "media");
                Response.AppendHeader("ETag", "\"" + m.Hash + "\"");
                return File(m.Content, m.ContentType);
            }
        }

        /// <summary>Saytdan ariza. ClientRequestId — takroriy yuborish xavfsiz.</summary>
        [HttpPost]
        public ActionResult Lead(LeadInput input)
        {
            try
            {
                using (var db = Db())
                    return JsonContent(JsonConvert.SerializeObject(new { id = SiteLeadService.Receive(db, input, DateTime.Now) }));
            }
            catch (DstValidationException ex)
            {
                return Error(HttpStatusCode.BadRequest, ex.Message);
            }
        }
    }
}
