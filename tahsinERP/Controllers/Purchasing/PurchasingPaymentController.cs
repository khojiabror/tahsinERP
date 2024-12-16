using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.Purchasing;

namespace tahsinERP.Controllers
{
    public class PurchasingPaymentController : Controller
    {
        private string ControllerName = "PurchasingPaymentController";
        // GET: PurchasingPayment
        public ActionResult Index()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                List<PURCHASING_CONTRACT_PAYMENTS> list = db.PURCHASING_CONTRACT_PAYMENTS
                    .Include(x => x.PURCHASING_CONTRACTS)
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public JsonResult GetContractDetails(int contractID)
        {
            // Fetch contract details based on the OrderID
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                
                var contract = db.PURCHASING_CONTRACTS.Include(c => c.CURRENCy).FirstOrDefault(c => c.ID == contractID && c.IsDeleted == false);

                if (contract != null)
                {
                    return Json(new
                    {
                        ContractAmount = Math.Round((double)contract.Amount, 2) + " " + contract.CURRENCy.Shortname,
                        PaidAmount = GetPaidAmountSoFar(contract.Amount, contractID) + " " + contract.CURRENCy.Shortname,
                        BalanceAmount = Math.Round((double)contract.Amount, 2) - GetPaidAmountSoFar(contract.Amount, contractID) + " " + contract.CURRENCy.Shortname
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                    return Json(null, JsonRequestBehavior.AllowGet);
            }
        }
        private double? GetPaidAmountSoFar(double? contractAmount, int contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var payment = db.PURCHASING_CONTRACT_PAYMENTS.Where(pop => pop.ContractID == contractID && pop.IsDeleted == false).FirstOrDefault();
                if (payment != null)
                {
                    double paymentAmount = db.PURCHASING_CONTRACT_PAYMENTS.Where(pcp => pcp.ContractID == contractID && pcp.IsDeleted == false).Sum(pcp => pcp.Payment_Amount);

                    return Math.Round(paymentAmount, 2);
                }
                else
                    return 0;
            }
        }
        public ActionResult Create()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View();
            }
        }
        [HttpPost]
        public ActionResult Create(PURCHASING_CONTRACT_PAYMENTS model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var existPurchasingPayment = db.PURCHASING_CONTRACT_PAYMENTS.Where(x => x.ContractID == model.ContractID).FirstOrDefault();
                    if (existPurchasingPayment is null)
                    {
                        model.IsDeleted = false;
                        db.PURCHASING_CONTRACT_PAYMENTS.Add(model);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.PURCHASING_CONTRACT_PAYMENTS.Find(model.ID).PURCHASING_CONTRACTS.ContractNo} - li harid shartnomasiga to'lovni kiritdi!");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                        ModelState.AddModelError("", "Ushbu Xarid shartnomasi uchun To'lov yaratilgan."); ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                        return View(model);
                    }
                }
                else
                {
                    ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo"); 
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

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
                var purchasingPayment = db.PURCHASING_CONTRACT_PAYMENTS
                            .Include(x => x.PURCHASING_CONTRACTS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID ==  Id && x.IsDeleted==false)
                            .FirstOrDefault();
                if (purchasingPayment == null)
                {
                    return HttpNotFound();
                }
                return View(purchasingPayment);
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
                var purchasingPayment = db.PURCHASING_CONTRACT_PAYMENTS
                            .Include(x => x.PURCHASING_CONTRACTS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID ==  Id && x.IsDeleted==false)
                            .FirstOrDefault();
                if (purchasingPayment == null)
                {
                    return HttpNotFound();
                }
                return View(purchasingPayment);
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
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingPaymentToDelete = db.PURCHASING_CONTRACT_PAYMENTS.Find(ID);
                if (purchasingPaymentToDelete != null)
                {
                    purchasingPaymentToDelete.IsDeleted = true;
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.PURCHASING_CONTRACT_PAYMENTS.Find(ID).PURCHASING_CONTRACTS.ContractNo} - shartnoma uchun to'lov ni o'chirdi");
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
                return View(purchasingPaymentToDelete);
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
                var purchasingPayment = db.PURCHASING_CONTRACT_PAYMENTS
                            .Include(x => x.PURCHASING_CONTRACTS)
                            .Include(x => x.CURRENCy)
                            .Where(x => x.ID == Id && x.IsDeleted == false)
                            .FirstOrDefault();

                if (purchasingPayment == null)
                {
                    return HttpNotFound();
                }
                ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo"); 
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(purchasingPayment);
            }
        }
        [HttpPost]
        public ActionResult Edit(PURCHASING_CONTRACT_PAYMENTS purchasingPayment)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var purchasingPaymentToUpdate = db.PURCHASING_CONTRACT_PAYMENTS.Find(purchasingPayment.ID);
                    if (purchasingPaymentToUpdate != null)
                    {
                        purchasingPaymentToUpdate.Supplier = purchasingPayment.Supplier;
                        purchasingPaymentToUpdate.ContractID = purchasingPayment.ContractID;
                        purchasingPaymentToUpdate.Payment_Amount = purchasingPayment.Payment_Amount;
                        purchasingPaymentToUpdate.Comment = purchasingPayment.Comment;
                        purchasingPaymentToUpdate.CurrencyID = purchasingPayment.CurrencyID;

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.PURCHASING_CONTRACT_PAYMENTS.Find(purchasingPayment.ID).PURCHASING_CONTRACTS.ContractNo} - shartnoma to'lovni taxrirladi");
                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                        return View(purchasingPayment);
                    }
                }
                else
                {
                    ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                    ModelState.AddModelError("", "Ma'lumotlatni to'liq kiritganingizga ishonch hosil qilib qaytadan urinib ko'ring.");
                    return View(purchasingPayment);
                }
            }
        }
    }
}