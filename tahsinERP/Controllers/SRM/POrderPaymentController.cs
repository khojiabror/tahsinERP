using DocumentFormat.OpenXml.Drawing.Charts;
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
        private string message = "";
        // GET: POrderPayment
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<P_ORDER_PAYMENTS> list = db.P_ORDER_PAYMENTS
                    .Include(x => x.P_ORDERS)
                    .Include(x => x.CURRENCy)
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
                ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

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
                    if (!CheckForAmountExcess(model.OrderID, model.Payment_Amount))
                    {
                        model.IsDeleted = false;
                        db.P_ORDER_PAYMENTS.Add(model);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "POrderPaymentController", $"{db.P_ORDERS.Find(model.OrderID).OrderNo} - li buyurtma uchun {model.Payment_Amount} miqdorida to'lov kiritdi");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                        ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());

                        ModelState.AddModelError("", message);
                        return View(model);
                    }
                }
                else
                {
                    ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                    ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(model);
                }
            }
        }
        private bool CheckForAmountExcess(int orderID, double amount)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.P_ORDERS.Where(pop => pop.ID == orderID && pop.IsDeleted == false).FirstOrDefault();
                if (order != null)
                {
                    double paidAmountSoFar = db.P_ORDER_PAYMENTS.Where(pop => pop.OrderID == orderID && pop.IsDeleted == false).Sum(pop => pop.Payment_Amount);
                    if (amount <= order.Amount - paidAmountSoFar)
                        return false;
                    else
                    {
                        message = "Kiritilayotgan summa: " + amount + " buyurtmaning umumiy summasi: " + order.Amount + " va shu paytgacha to'langan summa: " + paidAmountSoFar + " ning ayirmasidan ham katta. Buyurtmaning umumiy summasidan ortiq summada to'lov qilib bo'lmaydi";
                        return true;
                    }
                }
            }
            return false;
        }
        public JsonResult GetContractDetails(int orderId)
        {
            // Fetch contract details based on the OrderID
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                int? conractID = db.P_ORDERS.Where(po => po.ID == orderId && po.IsDeleted == false).Select(po => po.ContractID).FirstOrDefault();
                var contract = db.P_CONTRACTS.FirstOrDefault(c => c.ID == conractID && c.IsDeleted == false);

                if (contract != null)
                {
                    return Json(new
                    {
                        contract.ContractNo,
                        ContractAmount = Math.Round((double)contract.Amount, 2),
                        PaidAmount = GetPaidAmountSoFar(contract.Amount, orderId),
                        BalanceAmount = Math.Round((double)contract.Amount, 2) - GetPaidAmountSoFar(contract.Amount, orderId)
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                    return Json(null, JsonRequestBehavior.AllowGet);
            }
        }
        private double? GetPaidAmountSoFar(double? contractAmount, int orderId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var payment = db.P_ORDER_PAYMENTS.Where(pop => pop.OrderID == orderId && pop.IsDeleted == false).FirstOrDefault();
                if (payment != null)
                {
                    double paymentAmount = db.P_ORDER_PAYMENTS.Where(pop => pop.OrderID == orderId && pop.IsDeleted == false).Sum(pop => pop.Payment_Amount);

                    return Math.Round(paymentAmount, 2);
                }
                else
                    return 0;
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
                            .Include(x => x.CURRENCy)
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
                            .Include(x => x.CURRENCy)
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

                    LogHelper.LogToDatabase(User.Identity.Name, "POrderPaymentController", $"{db.P_ORDERS.Find(orderPaymentToDelete.OrderID).OrderNo} - buyurtma payment ni o'chirdi");
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
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (orderPayment == null)
                {
                    return HttpNotFound();
                }
                ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
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
                        orderPaymentToUpdate.CurrencyID = orderPayment.CurrencyID;

                        LogHelper.LogToDatabase(User.Identity.Name, "POrderPaymentController", $"{db.P_ORDERS.Find(orderPayment.OrderID).OrderNo} - shartnoma paymenti taxrirladi");
                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        //ViewBag.orders = new SelectList(db.P_ORDERS.Where(x => x.IsDeleted == false).ToList(), "ID", "OrderNo");
                        ViewBag.suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                        ViewBag.orders = new SelectList(Enumerable.Empty<SelectList>());
                        return View(orderPayment);
                    }
                }
                else
                {
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
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