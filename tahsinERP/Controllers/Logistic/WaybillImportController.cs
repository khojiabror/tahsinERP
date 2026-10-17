using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Yo'l varaqalarini Excel'dan import qilish (LOG-M01: WaybillController'dan ajratildi; mantiq — WaybillImportService).
    /// Ruxsat "Waybill" yozuvlari bo'yicha: Upload/Download — o'zgartirish huquqi.
    /// </summary>
    [Permission(Controller = "Waybill")]
    public class WaybillImportController : BaseController
    {
        public ActionResult UploadExcel()
        {
            return View();
        }

        public ActionResult DownloadTemplate()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return File(WaybillImportService.BuildTemplate(db), ExcelExport.ContentType, "Waybill_Import_Template.xlsx");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadExcel(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                ViewBag.Error = "Excel fayl yuklanmadi.";
                return View();
            }

            WaybillImportResult result;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                result = await WaybillImportService.ImportAsync(db, file.InputStream);
            }

            if (result.FatalError != null)
            {
                ViewBag.Error = result.FatalError;
                return View();
            }

            LogHelper.LogToDatabase(CurrentUserName, "WaybillController", "UploadExcel",
                $"{result.SuccessCount} ta {LogisticsTerms.Waybill.ToLower()} Excel orqali yuklandi, {result.SkipCount} ta qator o'tkazib yuborildi, {result.Errors.Count} ta xato.");

            ViewBag.SuccessCount = result.SuccessCount;
            ViewBag.SkipCount = result.SkipCount;
            ViewBag.Errors = result.Errors;
            return View();
        }
    }
}
