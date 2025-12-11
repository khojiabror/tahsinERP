using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class TenderController : Controller
    {
        // GET: Tender
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Tender", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Get purchasable materials
                var materials = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo, p.Name })
                    .ToListAsync();

                ViewBag.Materials = new SelectList(materials, "ID", "PNo");

                return View();
            }
        }
    }
}
