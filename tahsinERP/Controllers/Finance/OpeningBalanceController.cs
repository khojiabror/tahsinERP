using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.Filters;
using tahsinERP.ViewModels.Finance;
using tahsinERP.Services.Common;
using tahsinERP.Services.Finance;

namespace tahsinERP.Controllers.Finance
{
    /// <summary>
    /// Opening balans sahifalari. Hisob-kitob va saqlash — OpeningBalanceService, Excel —
    /// OpeningBalanceExcelService, shartnoma ma'lumotnomalari — FinCounterpartyLookupService (FIN-M02, FIN-D17).
    /// </summary>
    [Permission("OpeningBalance")]
    public class OpeningBalanceController : Controller
    {
        /// <summary>Ro'yxat: OpeningBalance moduli — ko'rish ruxsati</summary>
        public ActionResult Index()
        {
            using (var db = new DBTHSNEntities())
            {
                ViewBag.CanChange = RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance");
                return View(new OpeningBalanceService(db).Dates());
            }
        }

        /// <summary>Tahrir / ko'rish — GET: ko'rish ruxsati</summary>
        [SkipPermission]
        public ActionResult Edit(DateTime? snapshotDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OpeningBalance"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                ViewBag.CanChange = RoleHelper.IsChangePermitted(User.Identity.Name, "OpeningBalance");
                return View(new OpeningBalanceService(db).BuildViewModel(date));
            }
        }

        /// <summary>Details — faqat ko'rish rejimi</summary>
        public ActionResult Details(DateTime? snapshotDate)
        {
            if (!snapshotDate.HasValue)
            {
                return RedirectToAction("Index");
            }

            using (var db = new DBTHSNEntities())
            {
                return View(new OpeningBalanceService(db).BuildViewModel(snapshotDate.Value.Date));
            }
        }

        /// <summary>Saqlash — faqat o'zgartirish ruxsati</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(OpeningBalanceViewModel model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            try
            {
                // O'chirish va qayta yozish bitta tranzaksiyada (FIN-B06): xato bo'lsa eski opening saqlanib qoladi.
                using (var db = new DBTHSNEntities())
                using (var tx = db.Database.BeginTransaction())
                {
                    new OpeningBalanceService(db).Save(model, key => Request.Form[key], UserHelper.GetUserId(User.Identity.Name));
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Saqlashda xatolik: " + ErrorChain(ex);
                return RedirectToAction("Edit", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
            }

            TempData["Success"] = "Opening balans muvaffaqiyatli saqlandi.";
            return RedirectToAction("Edit", new { snapshotDate = model.SnapshotDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AutoFillFromCurrentBalances(DateTime snapshotDate)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                using (var tx = db.Database.BeginTransaction())
                {
                    new OpeningBalanceService(db).AutoFill(snapshotDate, UserHelper.GetUserId(User.Identity.Name));
                    tx.Commit();
                }

                TempData["Success"] = "Auto-fill bajarildi: current balances asosida detailed opening qatorlari yangilandi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Auto-fill xatoligi: " + ex.Message;
            }

            return RedirectToAction("Edit", new { snapshotDate = snapshotDate.ToString("yyyy-MM-dd") });
        }

        private static string ErrorChain(Exception ex)
        {
            var msg = ex.Message;
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                msg += " | " + inner.Message;
            }
            return msg;
        }

        #region Shartnoma ma'lumotnomalari (AJAX)

        private JsonResult ContractsJson(byte type, int counterpartyId)
        {
            using (var db = new DBTHSNEntities())
            {
                var data = new FinCounterpartyLookupService(db).Contracts(type, counterpartyId)
                    .Select(c => new { id = c.ID, text = c.ContractNo ?? ("Shartnoma #" + c.ID) })
                    .ToList();
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
        }

        private JsonResult TotalsJson(byte type, int counterpartyId, int? contractId, DateTime? snapshotDate)
        {
            if (counterpartyId <= 0 || !contractId.HasValue || contractId.Value <= 0)
            {
                return Json(new { success = false, message = "Invalid parameters" }, JsonRequestBehavior.AllowGet);
            }

            var date = (snapshotDate ?? DateTime.Today).Date;
            using (var db = new DBTHSNEntities())
            {
                var totals = new FinCounterpartyLookupService(db).Totals(type, counterpartyId, contractId.Value, date);
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalAmount = totals.Total,
                        paidAmount = totals.Paid,
                        amount = totals.Total - totals.Paid
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetCustomerContracts(int customerId)
        {
            return ContractsJson(FinCounterpartyLookupService.Customer, customerId);
        }

        [HttpGet]
        public JsonResult GetSupplierContracts(int supplierId)
        {
            return ContractsJson(FinCounterpartyLookupService.Supplier, supplierId);
        }

        [HttpGet]
        public JsonResult GetForwarderContracts(int forwarderId)
        {
            return ContractsJson(FinCounterpartyLookupService.Forwarder, forwarderId);
        }

        [HttpGet]
        public JsonResult GetCustomerContractTotals(int customerId, int? contractId, DateTime? snapshotDate)
        {
            return TotalsJson(FinCounterpartyLookupService.Customer, customerId, contractId, snapshotDate);
        }

        [HttpGet]
        public JsonResult GetSupplierContractTotals(int supplierId, int? contractId, DateTime? snapshotDate)
        {
            return TotalsJson(FinCounterpartyLookupService.Supplier, supplierId, contractId, snapshotDate);
        }

        [HttpGet]
        public JsonResult GetForwarderContractTotals(int forwarderId, int? contractId, DateTime? snapshotDate)
        {
            return TotalsJson(FinCounterpartyLookupService.Forwarder, forwarderId, contractId, snapshotDate);
        }

        #endregion

        #region Excel

        [HttpGet]
        public ActionResult DownloadOpeningBalanceExcelTemplate()
        {
            return File(OpeningBalanceExcelService.Template(), ExcelExport.ContentType, "opening-balance-template.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ImportOpeningBalanceExcel(DateTime snapshotDate)
        {
            var file = Request.Files["file"];
            if (file == null || file.ContentLength <= 0)
            {
                return Json(new { success = false, message = "Fayl tanlanmagan." });
            }
            if (Path.GetExtension(file.FileName ?? "").ToLowerInvariant() != ".xlsx")
            {
                return Json(new { success = false, message = "Faqat .xlsx format qabul qilinadi." });
            }

            using (var db = new DBTHSNEntities())
            {
                var result = OpeningBalanceExcelService.Import(file.InputStream, snapshotDate, db);
                return Json(new
                {
                    success = true,
                    customerLines = result.CustomerLines,
                    supplierLines = result.SupplierLines,
                    errors = result.Errors
                });
            }
        }

        #endregion

        /// <summary>O'chirish tasdiq — faqat o'zgartirish ruxsati</summary>
        [HttpGet]
        public ActionResult Delete(DateTime? snapshotDate)
        {
            if (snapshotDate == null)
            {
                return RedirectToAction("Index");
            }

            var date = snapshotDate.Value.Date;
            using (var db = new DBTHSNEntities())
            {
                var lineCount = new OpeningBalanceService(db).LineCount(date);
                if (lineCount == 0)
                {
                    TempData["Error"] = "Tanlangan sana uchun opening balans yozuvlari topilmadi.";
                    return RedirectToAction("Index");
                }

                return View(new OpeningBalanceDeleteViewModel
                {
                    SnapshotDate = date,
                    LineCount = lineCount
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(DateTime snapshotDate)
        {
            var date = snapshotDate.Date;
            using (var db = new DBTHSNEntities())
            using (var tx = db.Database.BeginTransaction())
            {
                new OpeningBalanceService(db).DeleteByDate(date);
                tx.Commit();
            }

            TempData["Success"] = $"Opening balans ({date:yyyy-MM-dd}) o'chirildi.";
            return RedirectToAction("Index");
        }
    }
}
