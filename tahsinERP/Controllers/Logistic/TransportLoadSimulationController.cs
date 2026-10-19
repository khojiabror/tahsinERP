using System.Linq;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Transport yuklash simulyatsiyasi. Mahsulotlar select2 AJAX bilan qidiriladi (LOG-P13: avval barcha
    /// mahsulotlar ishlatilmaydigan data-* atributlar bilan sahifaga chiqarilardi).
    /// </summary>
    [Permission]
    public class TransportLoadSimulationController : BaseController
    {
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.TransportTypes = db.F_TRANSPORT_TYPES.AsNoTracking()
                    .Where(t => t.IsDeleted != true)
                    .OrderBy(t => t.TransportType)
                    .Select(t => new TransportSelectItem
                    {
                        ID = t.ID,
                        TransportType = t.TransportType,
                        IntLgth = (double)t.IntLgth,
                        IntWdth = (double)t.IntWdth,
                        IntHght = (double)(t.IntHght)
                    })
                    .ToList();

                return View("~/Views/Logistic/TransportLoadSimulation/Index.cshtml");
            }
        }

        /// <summary>select2 AJAX: PNo yoki nomi bo'yicha 20 ta mahsulot.</summary>
        [HttpGet]
        public JsonResult SearchProducts(string q)
        {
            q = (q ?? "").Trim();
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var results = db.PRODUCTS.AsNoTracking()
                    .Where(p => p.IsDeleted == false && (p.PNo.Contains(q) || p.Name.Contains(q)))
                    .OrderBy(p => p.PNo)
                    .Take(20)
                    .Select(p => new { id = p.ID, text = p.PNo + " - " + p.Name })
                    .ToList();
                return Json(new { results }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
