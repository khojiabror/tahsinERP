using System.Web.Mvc;

namespace tahsinERP.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        public ActionResult Error()
        {
            return View();
        }

        public ActionResult NotFound()
        {
            return View();
        }
    }

}