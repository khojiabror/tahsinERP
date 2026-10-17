using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Procurement;

namespace tahsinERP.Controllers
{
    public class TenderController : BaseController
    {
        // Olish so'rovi: Tender
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, "Tender", "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                // Xarid qilinadigan materiallar (tayyor mahsulot emas) — umumiy keshdan
                var materials = await ProcurementLookups.GetPurchasableProductsAsync(db, ProcurementLookups.DirectPurchase);
                ViewBag.Materials = new SelectList(materials, "ID", "PNo");

                return View();
            }
        }
    }
}
