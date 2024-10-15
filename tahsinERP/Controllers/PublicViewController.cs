using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class PublicViewController : Controller
    {
        // GET: PublicView
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var itemList = db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList();
            }
            return View();
        }
    }
}