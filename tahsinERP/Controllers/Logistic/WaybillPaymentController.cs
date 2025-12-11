using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.Logistic
{
    public class WaybillPaymentController : Controller
    {
        private string ControllerName = "WaybillPaymentController";
        // GET: WaybillPayment
        public ActionResult Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<F_WAYBILL_PAYMENTS> list = db.F_WAYBILL_PAYMENTS
                    .Include(x => x.FORWARDER)
                    .Include(x => x.CURRENCy)
                    .Include(x => x.F_WAYBILLS)
                    .Where(x => x.IsDeleted == false)
                    .OrderByDescending(x => x.PaymentDate ?? DateTime.MinValue)
                    .ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.accounts = new SelectList(db.FIN_ACCOUNTS.Where(a => (a.IsCash || a.IsBank) && a.IsActive).ToList(), "ID", "Name");

                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(F_WAYBILL_PAYMENTS model, int? AccountID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    // Set PaymentDate if not provided
                    if (!model.PaymentDate.HasValue)
                    {
                        model.PaymentDate = DateTime.Now;
                    }

                    model.IsDeleted = false;
                    db.F_WAYBILL_PAYMENTS.Add(model);
                    db.SaveChanges();

                    // Post to finance
                    try
                    {
                        var userId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        FinancePostingService.PostWaybillPayment(model.ID, AccountID, db, userId);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create",
                            $"Finance posting error for waybill payment {model.ID}: {ex.Message}");
                    }

                    var waybill = db.F_WAYBILLS.Find(model.WaybillID);
                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Create",
                        $"{waybill?.WaybillNo} - li waybillga to'lovni kiritdi!");

                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                    ViewBag.accounts = new SelectList(db.FIN_ACCOUNTS.Where(a => (a.IsCash || a.IsBank) && a.IsActive).ToList(), "ID", "Name");

                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(model);
                }
            }
        }
        public ActionResult Details(int? Id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Include(x => x.FORWARDER)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }

                // Get waybill payment summary
                var waybill = waybillPayment.F_WAYBILLS;
                var totalPaid = db.F_WAYBILL_PAYMENTS
                    .Where(p => p.WaybillID == waybill.ID && p.IsDeleted == false)
                    .Sum(p => (double?)p.Payment_Amount) ?? 0;
                var outstanding = (waybill.Amount ?? 0) - totalPaid;

                ViewBag.TotalPaid = totalPaid;
                ViewBag.Outstanding = outstanding;
                ViewBag.WaybillAmount = waybill.Amount ?? 0;

                return View(waybillPayment);
            }
        }
        public ActionResult Delete(int? Id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                return View(waybillPayment);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection nimadir)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID is null)
            {
                return HttpNotFound();
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPaymentToDelete = db.F_WAYBILL_PAYMENTS.Find(ID);
                if (waybillPaymentToDelete != null)
                {
                    var waybillNo = waybillPaymentToDelete.F_WAYBILLS?.WaybillNo ?? "Unknown";
                    waybillPaymentToDelete.IsDeleted = true;
                    db.SaveChanges();

                    // Also delete/update related finance transaction
                    var finTransaction = db.FIN_TRANSACTIONS
                        .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == ID);
                    if (finTransaction != null)
                    {
                        // Optionally mark as deleted or remove - depends on finance module requirements
                        // For now, we'll leave it as is since FIN_TRANSACTIONS might not have IsDeleted
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Delete",
                        $"{waybillNo} - waybill uchun to'lovni o'chirdi");

                    return RedirectToAction("Index");
                }
                return View(waybillPaymentToDelete);
            }
        }
        public ActionResult Edit(int? Id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo", waybillPayment.WaybillID);
                ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName", waybillPayment.ForwarderID);
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname", waybillPayment.CurrencyID);
                ViewBag.accounts = new SelectList(db.FIN_ACCOUNTS.Where(a => (a.IsCash || a.IsBank) && a.IsActive).ToList(), "ID", "Name");

                return View(waybillPayment);
            }
        }

        [HttpPost]
        public ActionResult Edit(F_WAYBILL_PAYMENTS waybillPayment, int? AccountID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var waybillPaymentToUpdate = db.F_WAYBILL_PAYMENTS.Find(waybillPayment.ID);
                    if (waybillPaymentToUpdate != null)
                    {
                        waybillPaymentToUpdate.ForwarderID = waybillPayment.ForwarderID;
                        waybillPaymentToUpdate.WaybillID = waybillPayment.WaybillID;
                        waybillPaymentToUpdate.Payment_Amount = waybillPayment.Payment_Amount;
                        waybillPaymentToUpdate.PaymentDate = waybillPayment.PaymentDate ?? DateTime.Now;
                        waybillPaymentToUpdate.Comment = waybillPayment.Comment;
                        waybillPaymentToUpdate.CurrencyID = waybillPayment.CurrencyID;

                        db.SaveChanges();

                        // Update finance transaction if exists
                        var finTransaction = db.FIN_TRANSACTIONS
                            .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == waybillPayment.ID);
                        if (finTransaction != null && AccountID.HasValue)
                        {
                            finTransaction.AccountID = AccountID.Value;
                            finTransaction.Amount = (decimal)waybillPayment.Payment_Amount;
                            finTransaction.TxnDate = waybillPayment.PaymentDate ?? DateTime.Now;
                            db.SaveChanges();
                        }

                        var waybill = db.F_WAYBILLS.Find(waybillPayment.WaybillID);
                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, "Edit",
                            $"{waybill?.WaybillNo} - waybill uchun to'lovni tahrirladi");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                        ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                        ViewBag.accounts = new SelectList(db.FIN_ACCOUNTS.Where(a => (a.IsCash || a.IsBank) && a.IsActive).ToList(), "ID", "Name");

                        return View(waybillPayment);
                    }
                }
                else
                {
                    ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                    ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ViewBag.accounts = new SelectList(db.FIN_ACCOUNTS.Where(a => (a.IsCash || a.IsBank) && a.IsActive).ToList(), "ID", "Name");

                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(waybillPayment);
                }
            }
        }
    }
}