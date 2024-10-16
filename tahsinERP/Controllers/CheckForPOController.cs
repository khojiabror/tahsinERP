using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    [AllowAnonymous]
    public class CheckForPOController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            //using (DBTHSNEntities db = new DBTHSNEntities())
            //{
            //    var itemList = db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList();
            //}
            return View();
        }
        [HttpPost]
        public ActionResult Index(string ordercode)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_ORDERS po = db.PURCHASING_ORDERS.Where(p => p.OrderCode.CompareTo(ordercode) == 0 && p.IsDeleted == false).FirstOrDefault();
                if (po != null)
                {
                    return RedirectToAction("Tracing");
                }
                else
                {
                    ViewBag.Message = "Bunday buyurtma ushbu tizimda topilmadi, buyurtmangiz yoki hali to'liq tasdiqlanmagan yoki ushbu tizimga kiritilmagan!";
                    return View();
                }
            }
        }
        public ActionResult Tracing()
        {
            return View();
        }
    }
}