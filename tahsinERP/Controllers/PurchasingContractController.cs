    using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class PurchasingContractController : Controller
    {
        // GET: PurchasingContract
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_CONTRACTS.Include(x => x.COMPANy).Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                PURCHASING_CONTRACTS newPurchasingContract = new PURCHASING_CONTRACTS();
                PURCHASING_CONTRACT_ITEMS contractItem = new PURCHASING_CONTRACT_ITEMS();
            }
            return View();
        }

        public ActionResult Edit(int? id)
        {
            return View();
        }
        public ActionResult Delete(int? id)
        {
            return View();
        }
        public ActionResult Details(int? id)
        {
            return View();
        }
    }
}