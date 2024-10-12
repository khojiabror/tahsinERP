using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers.SRM
{
    public class POrderPaymentController : Controller
    {
        private string ControllerName = "POrderPaymentController";
        // GET: POrderPayment
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<P_ORDER_PAYMENTS> list = db.P_ORDER_PAYMENTS
                    .Include(x => x.P_ORDERS)
                    .Include(x => x.SUPPLIER)
                    .Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult GetOrdersBySupplier(int supplierID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contracts = db.P_ORDERS
                    .Where(x => x.IsDeleted == false && x.SupplierID == supplierID)
                    .Select(x => new { x.ID, x.OrderNo })
                    .ToList();

                return Json(contracts.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.OrderNo }), JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(P_ORDER_PAYMENTS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var existPurchasingPayment = db.P_ORDER_PAYMENTS.Where(x => x.OrderID == model.OrderID).FirstOrDefault();
                    if (existPurchasingPayment is null)
                    {
                        model.IsDeleted = false;
                        db.P_ORDER_PAYMENTS.Add(model);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.P_ORDERS.Find(model.OrderID).OrderNo} - li buyurtma uchun POrderPaymentni yaratdi");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                        //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                        ModelState.AddModelError("", "Ushbu Buyurtma  uchun To'lov yaratilgan.");
                        return View(model);
                    }
                }
                else
                {
                    ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                    //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                    ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
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
                var orderPayment = db.P_ORDER_PAYMENTS
                            .Include(x => x.P_ORDERS)
                            .Include(x => x.SUPPLIER)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (orderPayment == null)
                {
                    return HttpNotFound();
                }
                return View(orderPayment);
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
                var orderPayment = db.P_ORDER_PAYMENTS
                            .Include(x => x.P_ORDERS)
                            .Include(x => x.SUPPLIER)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();
                if (orderPayment == null)
                {
                    return HttpNotFound();
                }
                return View(orderPayment);
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
                var orderPaymentToDelete = db.P_ORDER_PAYMENTS.Find(ID);
                if (orderPaymentToDelete != null)
                {
                    orderPaymentToDelete.IsDeleted = true;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.P_ORDERS.Find(orderPaymentToDelete.OrderID).OrderNo} - buyurtma payment ni o'chirdi");
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
                return View(orderPaymentToDelete);
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
                var orderPayment = db.P_ORDER_PAYMENTS
                            .Include(x => x.P_ORDERS)
                            .Include(x => x.SUPPLIER)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (orderPayment == null)
                {
                    return HttpNotFound();
                }
                ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                return View(orderPayment);
            }
        }
        [HttpPost]
        public ActionResult Edit(P_ORDER_PAYMENTS orderPayment)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var orderPaymentToUpdate = db.P_ORDER_PAYMENTS.Find(orderPayment.ID);
                    if (orderPaymentToUpdate != null)
                    {
                        orderPaymentToUpdate.SupplierID = orderPayment.SupplierID;
                        orderPaymentToUpdate.OrderID = orderPayment.OrderID;
                        orderPaymentToUpdate.Payment_Amount = orderPayment.Payment_Amount;
                        orderPaymentToUpdate.Comment = orderPayment.Comment;
                        orderPaymentToUpdate.Currency = orderPayment.Currency;

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.P_ORDERS.Find(orderPayment.OrderID).OrderNo} - shartnoma paymenti taxrirladi");
                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                        ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                        return View(orderPayment);
                    }
                }
                else
                {
                    ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                    //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                    ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(orderPayment);
                }
            }
        }
    }
}