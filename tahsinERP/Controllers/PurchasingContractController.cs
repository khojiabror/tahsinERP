using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class PurchasingContractController : Controller
    {
        // GET: PurchasingContract
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.orderList = new SelectList(db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.IsDeleted == false).ToList(), "ID", "OrderCodeEDO");
                ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                PURCHASING_CONTRACTS newPurchasingContract = new PURCHASING_CONTRACTS();
                PURCHASING_CONTRACT_ITEMS contractItem = new PURCHASING_CONTRACT_ITEMS();
            }
            return View();
        }

        
        public ActionResult Delete(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingContract = db.PURCHASING_CONTRACTS
                .Include(p => p.PURCHASING_ORDERS)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (purchasingContract == null)
                {
                    return HttpNotFound();
                }

                var partList = db.PURCHASING_CONTRACT_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ContractID == purchasingContract.ID)
                                  .ToList();

                ViewBag.PartList = partList;
                
                return View(purchasingContract);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection gfs)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_CONTRACTS purchasingContractToDelete = db.PURCHASING_CONTRACTS.Find(ID);
                    if (purchasingContractToDelete != null)
                        try
                        {
                            purchasingContractToDelete.IsDeleted = true;
                            db.SaveChanges();
                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingContractController", $"{purchasingContractToDelete.ContractNo} - xarid shartnomani o'chirdi");

                            db.Entry(purchasingContractToDelete).State = EntityState.Modified;
                            var purchasingContractItems = db.PURCHASING_CONTRACT_ITEMS.Where(pc => pc.ContractID == purchasingContractToDelete.ID).ToList();

                            foreach (var contractItem in purchasingContractItems)
                            {
                                contractItem.IsDeleted = true;
                            }
                            db.SaveChanges();
                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    else
                        ModelState.AddModelError("", "Bunday xarid shartnoma topilmadi.");
                }
            }
            return View();
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingContract = db.PURCHASING_CONTRACTS
                                  .Include(p => p.PURCHASING_ORDERS)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (purchasingContract == null)
                {
                    return HttpNotFound();
                }

                var partList = db.PURCHASING_CONTRACT_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ContractID == purchasingContract.ID && pc.IsDeleted == false)
                                  .ToList();

                ViewBag.PartList = partList;
                var Doc = db.PURCHASING_CONTRACT_DOCS.FirstOrDefault(pi => pi.ContractID == ID);
                if (Doc != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(Doc.Doc);
                }
                return View(purchasingContract);
            }
        }
        public ActionResult DownloadDoc(int? contractID)
        {
            if (contractID == null)
            {
                return Json(new { success = false, message = "Shartnoma ID ko'rsatilmagan." }, JsonRequestBehavior.AllowGet);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractDoc = db.PURCHASING_CONTRACT_DOCS
                                .Include(x => x.PURCHASING_CONTRACTS)
                                .FirstOrDefault(pi => pi.ContractID == contractID);
                if (contractDoc != null)
                {
                    var contractNo = contractDoc.PURCHASING_CONTRACTS.ContractNo;
                    // Returning the file for download via AJAX can be handled differently, so return success here
                    return File(contractDoc.Doc, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contractNo + "_ContractDoc.pdf");
                }
                else
                {
                    return Json(new { success = false, message = "Fayl yuklanmagan." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingContract = db.PURCHASING_CONTRACTS
                                  .Include(p => p.PURCHASING_ORDERS)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (purchasingContract == null)
                {
                    return HttpNotFound();
                }

                var partList = db.PURCHASING_CONTRACT_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ContractID == purchasingContract.ID && pc.IsDeleted == false)
                                  .ToList();

                ViewBag.PartList = partList;
                /*var Doc = db.PURCHASING_CONTRACT_DOCS.FirstOrDefault(pi => pi.ContractID == ID);
                if (Doc != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(Doc.Doc);
                }*/
                return View(purchasingContract);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PURCHASING_CONTRACTS purchasingContract)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_CONTRACTS purchasingContractToUpdate = db.PURCHASING_CONTRACTS.Find(purchasingContract.ID);
                    if (purchasingContractToUpdate != null)
                    {
                        purchasingContractToUpdate.ContractNo = purchasingContract.ContractNo;
                        purchasingContractToUpdate.Supplier = purchasingContract.Supplier;
                        purchasingContractToUpdate.IssueDT = purchasingContract.IssueDT;
                        purchasingContractToUpdate.DueDT = purchasingContract.DueDT;
                        purchasingContractToUpdate.Currency = purchasingContract.Currency;
                        purchasingContractToUpdate.Incoterms = purchasingContract.Incoterms;
                        purchasingContractToUpdate.PaymentTerms = purchasingContract.PaymentTerms;
                        purchasingContractToUpdate.Comment = purchasingContract.Comment;
                        purchasingContractToUpdate.IsDeleted = false;

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingContractController", $"{purchasingContractToUpdate.ContractNo} - PurchasingContractni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(purchasingContractToUpdate);
                }
            }
            var userEmail = User.Identity.Name;
            LogHelper.LogToDatabase(userEmail, "PurchasingContractController", "Edit[Post]");
            return View(purchasingContract);
        }
        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contractItem = db.PURCHASING_CONTRACT_ITEMS
                                    .Include(p => p.PURCHASING_ITEMS)
                                    .Include(p => p.PURCHASING_CONTRACTS)
                                    .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);
                if (contractItem == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PURCHASING_ITEMS
                                .Include(p => p.PURCHASING_CONTRACT_ITEMS)
                                .Include(p => p.UNIT)
                                .Where(p => p.IsDeleted == false)
                                .ToList();

                ViewBag.PartList = new SelectList(allParts, "ID", "ItemCode");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                return View(contractItem);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(PURCHASING_CONTRACT_ITEMS contractItem)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_CONTRACT_ITEMS contractPartToUpdate = db.PURCHASING_CONTRACT_ITEMS.Find(contractItem.ID);
                    if (contractPartToUpdate != null)
                    {
                        contractPartToUpdate.ItemID = contractItem.ItemID;
                        contractPartToUpdate.Price = contractItem.Price;
                        contractPartToUpdate.Amount = contractItem.Amount;
                        contractPartToUpdate.UnitID = contractItem.UnitID;
                        contractPartToUpdate.MOQ = contractItem.MOQ;
                        contractPartToUpdate.ActivePart = contractItem.ActivePart;
                        contractPartToUpdate.TotalPrice = contractItem.Amount * contractItem.Price;
                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingContractController", $"{contractPartToUpdate.ItemID} -  li PurchasingItemni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }

                    }
                    return RedirectToAction("Edit");
                }
            }
            return View();
        }
        public ActionResult DeletePart(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_CONTRACT_ITEMS contractPartToDelete = db.PURCHASING_CONTRACT_ITEMS.Find(id);
                if (ModelState.IsValid)
                {
                    if (contractPartToDelete != null)
                    {
                        try
                        {
                            //db.PURCHASING_CONTRACT_ITEMS.Remove(contractPartToDelete);
                            contractPartToDelete.IsDeleted = true;
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "PContractController", $"{contractPartToDelete.ItemID} - li PurchasingContractItemni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "PurchasingContractitem not found.");
                    }
                }
                return View(contractPartToDelete);
            }
        }
    }
}