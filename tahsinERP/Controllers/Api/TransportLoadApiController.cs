using System;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;
using tahsinERP.Filters;
using tahsinERP.I18n;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// API controller uchun transport yuklash simulation calculations
    /// </summary>
    [Permission(Controller = "TransportLoadSimulation")]
    public class TransportLoadApiController : Controller
    {
        /// <summary>
        /// Calculate maksimal yuklash uchun a product in a transport type
        /// GET /api/transportload/calculate?productId=...&transportId=...
        /// </summary>
        [HttpGet]
        public JsonResult Calculate(int? productId, int? transportId)
        {
            try
            {
                if (!productId.HasValue || !transportId.HasValue)
                {
                    return Json(new { success = false, error = L.T("Log_ProductTransportRequired") }, JsonRequestBehavior.AllowGet);
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
                // LOG-S02: ichki xato matni (SQL va h.k.) foydalanuvchiga ko'rsatilmaydi
                System.Diagnostics.Trace.TraceError("TransportLoadApi/Calculate: {0}", ex);
                return Json(new { success = false, error = L.T("Log_CalcErrorShort") }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
