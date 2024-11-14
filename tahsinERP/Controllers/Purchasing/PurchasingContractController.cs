using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.POrder;
using tahsinERP.ViewModels.Purchasing;

namespace tahsinERP.Controllers
{
    public class PurchasingContractController : Controller
    {
        // GET: PurchasingContract
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_CONTRACTS
                    .Include(x => x.PURCHASING_ORDERS)
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public JsonResult GetContractDetails(int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.PURCHASING_ORDERS
                                 .Include(x => x.CURRENCy)
                                 .Where(c => c.ID == orderID && c.IsDeleted == false)
                                 .Select(c => new
                                 {
                                     c.ID
                                 })
                                 .FirstOrDefault();

                if (order != null)
                {
                    return Json(new { success = true, data = order }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Contract not found." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public JsonResult GetPriceAndMOQ(int itemID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var itemDetails = db.PURCHASING_ORDER_ITEMS
                    .Where(i => i.ID == itemID && i.IsDeleted == false)
                    .Select(i => new
                    {
                        i.Price,
                        i.Amount
                    })
                    .FirstOrDefault();

                if (itemDetails != null)
                {
                    return Json(new { success = true, data = itemDetails }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Item not found." }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        public JsonResult GetPartList(int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var partList = db.PURCHASING_ORDER_ITEMS
                    .Include(x => x.PURCHASING_ITEMS)
                    .Where(p => p.OrderID == orderID && p.IsDeleted == false)
                    .Select(p => new
                    {
                        p.ItemID,
                        p.PURCHASING_ITEMS.ItemName
                    })
                    .ToList();

                return Json(new { success = true, data = partList }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.orderList = new SelectList(db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.IsDeleted == false).ToList(), "ID", "OrderCodeEDO");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                /*PURCHASING_CONTRACTS newPurchasingContract = new PURCHASING_CONTRACTS();
                PURCHASING_CONTRACT_ITEMS orderItem = new PURCHASING_CONTRACT_ITEMS();*/
            }
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PurchasingContractViewModel model)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    if (ModelState.IsValid)
                    {
                        try
                        {
                            var newPurchasingContract = new PURCHASING_CONTRACTS()
                            {
                                ContractNo = model.ContractNo,
                                IssueDT = model.IssueDT,
                                DueDT = model.DueDT,
                                CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                                Supplier = model.Supplier,
                                CurrencyID = model.CurrencyID,
                                Incoterms = model.Incoterms,
                                PaymentTerms = model.PaymentTerms,
                                Comment = model.Comment,
                                IsDeleted = false,
                                OrderID = model.OrderID,
                            };


                            db.PURCHASING_CONTRACTS.Add(newPurchasingContract);
                            db.SaveChanges();
                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{newPurchasingContract.ContractNo} - shartnomani yaratdi");

                            List<PURCHASING_ORDER_ITEMS> orderItems = db.PURCHASING_ORDER_ITEMS.Where(pcp => pcp.OrderID == model.OrderID).ToList();
                            List<string> notOrderedParts = new List<string>();

                            foreach (var part in model.Parts)
                            {
                                PURCHASING_ORDER_ITEMS temporaryPart = db.PURCHASING_ORDER_ITEMS.Where(x => x.ItemID == part.ItemID).FirstOrDefault();
                                var unt = db.UNITS.Where(x => x.ID == part.UnitID).FirstOrDefault();
                                if (part != null && orderItems.Contains(temporaryPart))
                                {
                                    var newPart = new PURCHASING_CONTRACT_ITEMS
                                    {
                                        ContractID = newPurchasingContract.ID,
                                        ItemID = part.ItemID,
                                        UnitID = unt.ID,
                                        Amount = part.Amount,
                                        Price = part.Price,
                                        ActivePart = true,
                                        IsDeleted = false,
                                    };

                                    db.PURCHASING_CONTRACT_ITEMS.Add(newPart);
                                    var logPart = db.PURCHASING_ITEMS.Find(newPart.ItemID);
                                    SetOrderProgress(temporaryPart.ID, part.Amount);
                                    LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{logPart.ItemName} - shartnomaga ushbu qismni qo'shdi");
                                }
                                else
                                {
                                    PURCHASING_ITEMS item = db.PURCHASING_ITEMS.Where(x => x.ID == part.ID).FirstOrDefault();
                                    notOrderedParts.Add(item.ItemName);
                                    break;
                                }
                            }

                            if (model.File != null)
                            {
                                if (model.File.ContentLength > 0)
                                {
                                    if (model.File.InputStream.Length < 5242880)
                                    {
                                        if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                                        {
                                            PURCHASING_CONTRACT_DOCS orderDoc = new PURCHASING_CONTRACT_DOCS();
                                            byte[] avatar = new byte[model.File.InputStream.Length];
                                            model.File.InputStream.Read(avatar, 0, avatar.Length);

                                            orderDoc.ContractID = newPurchasingContract.ID;
                                            orderDoc.Doc = avatar;

                                            db.PURCHASING_CONTRACT_DOCS.Add(orderDoc);
                                            db.SaveChanges();

                                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingContractController", $"{newPurchasingContract.ContractNo} - uchun shartnoma faylni yukladi");
                                        }
                                        else
                                        {
                                            ModelState.AddModelError("", "Format noto'g'ri. Faqat .pdf fayllarni yuklash mumkin.");
                                        }
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("", "Faylni yuklab bo'lmadi, u 2MBdan kattaroq. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                                        throw new RetryLimitExceededException();
                                    }
                                }
                            }
                            if (notOrderedParts.Count > 0)
                            {
                                var message = "";
                                foreach (var word in notOrderedParts)
                                {
                                    message += word + " ,";
                                }
                                ModelState.AddModelError("", "Ushbu xarid qism(lar): " + message + " buyurtmadan topilmadi, buyurtmada yo'q narsaga shartnoma qilib bo'lmaydi! Qaytadan urinib ko'ring!");

                                db.Entry(newPurchasingContract).State = EntityState.Deleted;
                                db.SaveChanges();
                                LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{newPurchasingContract.ContractNo} - vaqtincha shartnomani tizim o'zi o'chirib yubordi");

                                return View(model);
                            }

                            db.SaveChanges();
                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", ex.Message);
                            return View(model);
                        }

                    }
                    else
                    {
                        ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        ViewBag.orderList = new SelectList(db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.IsDeleted == false).ToList(), "ID", "OrderCodeEDO");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                        return View(model);
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            return View(model);
        }

        private void SetOrderProgress(int ID, double Amount)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_ORDER_ITEMS orderItem = db.PURCHASING_ORDER_ITEMS.Find(ID);
                if (orderItem != null)
                {
                    orderItem.Progress = Amount / orderItem.Amount;

                    db.Entry(orderItem).State = EntityState.Modified;
                    db.SaveChanges();
                }
            }
        }

        public ActionResult GetOrderItems(int orderId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Fetch order items with related item and unit data
                var orderItems = db.PURCHASING_ORDER_ITEMS
                    .Include(o => o.PURCHASING_ITEMS)
                    .Include(o => o.PURCHASING_ITEMS.UNIT)
                    .Where(o => o.OrderID == orderId)
                    .Select(o => new
                    {
                        o.PURCHASING_ITEMS.ID,
                        o.ItemID,
                        o.PURCHASING_ITEMS.ItemName,
                        o.Amount,
                        o.PURCHASING_ITEMS.UnitID,
                        o.PURCHASING_ITEMS.UNIT.ShortName,
                        o.Price,
                        o.TotalPrice
                    })
                    .ToList();

                // Fetch all available items and units for the dropdowns
                var items = db.PURCHASING_ITEMS
                    .Select(i => new
                    {
                        i.ID,
                        i.ItemName
                    })
                    .ToList();

                var units = db.UNITS
                    .Select(u => new
                    {
                        u.ID,
                        u.ShortName
                    })
                    .ToList();

                // Prepare the response with both order items, items, and units
                var response = orderItems.Select(item => new
                {
                    item.ID,
                    item.ItemID,
                    item.ItemName,
                    item.UnitID,
                    item.Amount,
                    item.Price,
                    item.TotalPrice,
                    Items = items,  // All available items for dropdown
                    Units = units   // All available units for dropdown
                }).ToList();

                return Json(response, JsonRequestBehavior.AllowGet);
            }
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
                                .Include(x => x.CURRENCy)
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

                ViewBag.partList = partList;

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
                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{purchasingContractToDelete.ContractNo} - xarid shartnomani o'chirdi");

                            db.Entry(purchasingContractToDelete).State = EntityState.Modified;
                            var purchasingContractItems = db.PURCHASING_CONTRACT_ITEMS.Where(pc => pc.ContractID == purchasingContractToDelete.ID).ToList();

                            foreach (var orderItem in purchasingContractItems)
                            {
                                orderItem.IsDeleted = true;
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
                            var purchasingContractTotalPrice = db.PURCHASING_CONTRACT_ITEMS.Where(x => x.IsDeleted == false && x.ContractID == contractPartToDelete.ContractID).Sum(x => x.TotalPrice);
                            PURCHASING_CONTRACTS purchasingContract = db.PURCHASING_CONTRACTS.Find(contractPartToDelete.ContractID);
                            if (purchasingContract != null)
                            {
                                purchasingContract.Amount = purchasingContractTotalPrice;
                            }
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{contractPartToDelete.ItemID} - li PurchasingContractItemni o'chirdi");

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
                                  .Include(x => x.CURRENCy)
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

                ViewBag.partList = partList;
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
                                  .Include(x => x.CURRENCy)
                                  .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);

                if (purchasingContract == null)
                {
                    return HttpNotFound();
                }

                var model = new PurchasingContractViewModel
                {
                    ID = purchasingContract.ID,
                    ContractNo = purchasingContract.ContractNo,
                    IssueDT = purchasingContract.IssueDT,
                    Supplier = purchasingContract.Supplier,
                    Amount = purchasingContract.Amount,
                    Incoterms = purchasingContract.Incoterms,
                    PaymentTerms = purchasingContract.PaymentTerms,
                    DueDT = purchasingContract.DueDT,
                    Comment = purchasingContract.Comment,
                    OrderID = purchasingContract.OrderID,
                    CurrencyID = (int)purchasingContract.CurrencyID,
                    Parts = purchasingContract.PURCHASING_CONTRACT_ITEMS.Select(x => new PurchasingContractItem
                    {
                        ID = x.ID,
                        ItemID = x.ItemID,
                        ItemName = x.PURCHASING_ITEMS.ItemName,
                        UnitID = (int)x.UnitID,
                        Price = x.Price,
                        Amount = x.Amount,
                        TotalPrice = x.TotalPrice,
                        MOQ = (double)x.MOQ
                    }).ToList()
                };

                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.orderList = new SelectList(db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.IsDeleted == false).ToList(), "ID", "OrderCodeEDO");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PurchasingContractViewModel purchasingContract)
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
                        purchasingContractToUpdate.CurrencyID = purchasingContract.CurrencyID;
                        purchasingContractToUpdate.Incoterms = purchasingContract.Incoterms;
                        purchasingContractToUpdate.PaymentTerms = purchasingContract.PaymentTerms;
                        purchasingContractToUpdate.Comment = purchasingContract.Comment;
                        purchasingContractToUpdate.IsDeleted = false;

                        double summ = 0;
                        foreach (var product in purchasingContract.Parts)
                        {
                            var existingPart = db.PURCHASING_CONTRACT_ITEMS.Find(product.ID);
                            if (existingPart != null)
                            {
                                existingPart.Price = product.Price;
                                //existingPart.UnitID = product.UnitID;
                                existingPart.Amount = product.Amount;
                                existingPart.MOQ = product.MOQ;
                                existingPart.TotalPrice = product.Amount * product.Price;
                                summ += existingPart.TotalPrice;
                                db.Entry(existingPart).State = EntityState.Modified;
                                try
                                {
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{existingPart.ItemID} - li PContractPartni tahrirladi");
                                }
                                catch (RetryLimitExceededException)
                                {
                                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                                }
                            }
                        }
                        purchasingContractToUpdate.Amount = summ;
                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{purchasingContractToUpdate.ContractNo} - PurchasingContractni tahrirladi");

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
            LogHelper.LogToDatabase(userEmail, "Xarid shartnomalari", "Edit[Post]");
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
                var orderItem = db.PURCHASING_CONTRACT_ITEMS
                                    .Include(p => p.PURCHASING_ITEMS)
                                    .Include(p => p.PURCHASING_CONTRACTS)
                                    .FirstOrDefault(p => p.ID == ID && p.IsDeleted == false);
                if (orderItem == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PURCHASING_ITEMS
                                .Include(p => p.PURCHASING_CONTRACT_ITEMS)
                                .Include(p => p.UNIT)
                                .Where(p => p.IsDeleted == false)
                                .ToList();

                ViewBag.partList = new SelectList(allParts, "ID", "ItemCode");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                return View(orderItem);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(PURCHASING_CONTRACT_ITEMS orderItem)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_CONTRACT_ITEMS contractPartToUpdate = db.PURCHASING_CONTRACT_ITEMS.Find(orderItem.ID);
                    if (contractPartToUpdate != null)
                    {
                        contractPartToUpdate.ItemID = orderItem.ItemID;
                        contractPartToUpdate.Price = orderItem.Price;
                        contractPartToUpdate.Amount = orderItem.Amount;
                        contractPartToUpdate.UnitID = orderItem.UnitID;
                        contractPartToUpdate.MOQ = orderItem.MOQ;
                        contractPartToUpdate.ActivePart = orderItem.ActivePart;
                        contractPartToUpdate.TotalPrice = orderItem.Amount * orderItem.Price;
                        try
                        {
                            db.SaveChanges();

                            var purchasingContractTotalPrice = db.PURCHASING_CONTRACT_ITEMS.Where(x => x.IsDeleted == false && x.ContractID == contractPartToUpdate.ContractID).Sum(x => x.TotalPrice);
                            PURCHASING_CONTRACTS purchasingContract = db.PURCHASING_CONTRACTS.Find(contractPartToUpdate.ContractID);
                            if (purchasingContract != null)
                            {
                                purchasingContract.Amount = purchasingContractTotalPrice;
                            }
                            db.SaveChanges();
                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid shartnomalari", $"{contractPartToUpdate.ItemID} -  li PurchasingItemni tahrirladi");

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
    }
}