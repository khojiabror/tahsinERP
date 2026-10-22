using System;
using System.Data.SqlClient;
using System.Web.Mvc;
using tahsinERP.Configuration;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// GET /health — Updater (Docs/Deploy/03_AUTO_UPDATE.md) yangilashdan keyin tekshiradi: ilova ko'tarildi, host → tenant
    /// aniqlandi (aks holda TenantHttp 404 qaytaradi) va tenant bazasiga ulanish bor. 200 "ok" yoki 503. Ma'lumot oshkor qilinmaydi.
    /// </summary>
    [AllowAnonymous]
    public class HealthController : Controller
    {
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public ActionResult Index()
        {
            try
            {
                using (var c = new SqlConnection(DbConfig.GetSqlConnectionString()))
                {
                    c.Open();
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 1";
                        cmd.CommandTimeout = 10;
                        cmd.ExecuteScalar();
                    }
                }
                return Content("ok", "text/plain");
            }
            catch (Exception)
            {
                Response.StatusCode = 503;
                Response.TrySkipIisCustomErrors = true;
                return Content("db", "text/plain");
            }
        }
    }
}
