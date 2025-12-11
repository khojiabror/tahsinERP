using System;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// API controller for transport load simulation calculations
    /// </summary>
    public class TransportLoadApiController : Controller
    {
        /// <summary>
        /// Calculate maximum load for a product in a transport type
        /// GET /api/transportload/calculate?productId=...&transportId=...
        /// </summary>
        [HttpGet]
        public JsonResult Calculate(int? productId, int? transportId)
        {
            try
            {
                if (!productId.HasValue || !transportId.HasValue)
                {
                    return Json(new { success = false, error = "Product ID and Transport ID are required" }, JsonRequestBehavior.AllowGet);
                }

                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var result = TransportLoadCalculator.CalculateMaxLoad(productId.Value, transportId.Value, db);
                    return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
