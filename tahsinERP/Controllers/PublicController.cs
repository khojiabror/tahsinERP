using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace tahsinERP.Controllers
{
    public class PublicController : Controller
    {
        // GET: Public
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }
    }
}