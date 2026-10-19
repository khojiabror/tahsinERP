using System.Web.Mvc;

namespace tahsinERP.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        public ActionResult Error()
        {
            if (Response.StatusCode < 400) Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }

        // IIS httpErrors (statik fayl topilmadi) va Application_Error ikkalasi ham shu sahifani beradi — holat kodi 404
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }
    }

}