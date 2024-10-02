    using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
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
                var list = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList();
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
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingContract = db.PURCHASING_CONTRACTS
                                  .Include(p => p.PURCHASING_ORDERS)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (purchasingContract == null)
                {
                    return HttpNotFound();
                }

                var partList = db.PURCHASING_CONTRACT_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ContractID == purchasingContract.ID)
                                  .ToList();

                ViewBag.PartList = partList;
                var Doc = db.PURCHASING_CONTRACT_DOCS.FirstOrDefault(pi => pi.ContractID == ID);
                if (Doc != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(Doc.Doc);
                }
                return View(purchasingContract);
            }
        }
    }
}