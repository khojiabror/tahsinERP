using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers.Logistic
{
    public class WaybillPaymentController : Controller
    {
        private string ControllerName = "WaybillPaymentController";
        // GET: PurchasingPayment
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<F_WAYBILL_PAYMENTS> list = db.F_WAYBILL_PAYMENTS.Include(x => x.FORWARDER).Include(x => x.F_WAYBILLS).Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(F_WAYBILL_PAYMENTS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var existWaybillPayment = db.F_WAYBILL_PAYMENTS.Where(x => x.WaybillID == model.WaybillID).FirstOrDefault();
                    if (existWaybillPayment is null)
                    {
                        model.IsDeleted = false;
                        db.F_WAYBILL_PAYMENTS.Add(model);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.F_WAYBILLS.Find(model.WaybillID).WaybillNo} - li waybillga to'lovni kiritdi!");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                        ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                        ModelState.AddModelError("", "Ushbu Waybill uchun To'lov yaratilgan.");
                        return View(model);
                    }
                }
                else
                {
                    ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                    ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(model);
                }
            }
        }
        public ActionResult Details(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.F_WAYBILLS)
                            .Include(x => x.FORWARDER)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                return View(waybillPayment);
            }
        }
        public ActionResult Delete(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
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
            if (ID is null)
            {
                return HttpNotFound();
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPaymentToDelete = db.F_WAYBILL_PAYMENTS.Find(ID);
                if (waybillPaymentToDelete != null)
                {
                    waybillPaymentToDelete.IsDeleted = true;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.F_WAYBILL_PAYMENTS.Find(ID).F_WAYBILLS.WaybillNo} - waybill uchun to'lov ni o'chirdi");
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
                return View(waybillPaymentToDelete);
            }
        }
        public ActionResult Edit(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybillPayment = db.F_WAYBILL_PAYMENTS
                            .Include(x => x.FORWARDER)
                            .Include(x => x.F_WAYBILLS)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (waybillPayment == null)
                {
                    return HttpNotFound();
                }
                ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo", Id);
                ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName", Id);
                return View(waybillPayment);
            }
        }
        [HttpPost]
        public ActionResult Edit(F_WAYBILL_PAYMENTS waybillPayment)
        {
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
                        waybillPaymentToUpdate.Comment = waybillPayment.Comment;
                        waybillPaymentToUpdate.Currency = waybillPayment.Currency;

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.F_WAYBILL_PAYMENTS.Find(waybillPayment.ID).F_WAYBILLS.WaybillNo}  - waybill uchun to'lov ni o'chirdi");
                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                        ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                        return View(waybillPayment);
                    }
                }
                else
                {
                    ViewBag.waybills = new SelectList(db.F_WAYBILLS.Where(x => x.IsDeleted == false).ToList(), "ID", "WaybillNo");
                    ViewBag.forwarders = new SelectList(db.FORWARDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "ForwarderName");
                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(waybillPayment);
                }
            }
        }
    }
}