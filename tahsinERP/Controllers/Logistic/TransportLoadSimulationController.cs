using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Controller uchun Transport yuklash Simulation module
    /// </summary>
    public class TransportLoadSimulationController : Controller
    {
        // Olish so'rovi: TransportLoadSimulation
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Olish products uchun dropdown (non-deleted faqat)
                var products = db.PRODUCTS
                    .Where(p => p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .Select(p => new ProductSelectItem
                    {
                        ID = p.ID,
                        PNo = p.PNo,
                        Name = p.Name,
                        Length = p.Length ?? 0,
                        Width = p.Width ?? 0,
                        Height = p.Height ?? 0
                    })
                    .ToList();

                ViewBag.Products = products;

                // Olish transport types uchun dropdown
                var transportTypes = db.F_TRANSPORT_TYPES
                    .OrderBy(t => t.TransportType)
                    .Select(t => new TransportSelectItem
                    {
                        ID = t.ID,
                        TransportType = t.TransportType,
                        IntLgth = t.IntLgth,
                        IntWdth = t.IntWdth,
                        IntHght = t.IntHght
                    })
                    .ToList();

                ViewBag.TransportTypes = transportTypes;

                return View("~/Views/Logistic/TransportLoadSimulation/Index.cshtml");
            }
        }
    }
}
