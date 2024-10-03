using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.POrder;

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
        public JsonResult GetContractDetails(int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.PURCHASING_ORDERS
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
                ViewBag.orderList = new SelectList(db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.IsDeleted == false).ToList(), "ID", "OrderCodeEDO");
                ViewBag.items = new SelectList(db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList(), "ID", "ItemName");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
    
                PURCHASING_CONTRACTS newPurchasingContract = new PURCHASING_CONTRACTS();
                PURCHASING_CONTRACT_ITEMS contractItem = new PURCHASING_CONTRACT_ITEMS();
            }
            return View();
        }/*
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PurchasingContractViewModel model)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    foreach (var part in model.Parts)
                    {
                        if (part.MOQ > part.Amount)
                        {
                            ModelState.AddModelError("", db.PURCHASING_ITEMS.Where(p => p.ID == part.ItemID).Select(p => p.ItemName) + " uchun kiritilgan miqdor MOQ dan kichik");
                        }
                    }
                    if (checkForContractPartsAmount(model))
                    {
                        var message = "";
                        foreach (var word in contractExceedingParts)
                        {
                            message += word + " ,";
                        }
                        ModelState.AddModelError("", "Ushbu xarid qismlarda " + message + " buyurtnadan ortiqcha hajmni shartnoma qilmoqchisiz, bunday qilib bo'lmaydi!");

                    }
                    if (ModelState.IsValid)
                    {
                        var newOrder = new P_ORDERS()
                        {
                            OrderNo = model.OrderNo,
                            IssuedDate = model.IssuedDate,
                            CompanyID = 1,
                            SupplierID = model.SupplierID,
                            ContractID = model.ContractID,
                            Currency = model.Currency,
                            Description = model.Description,
                            IsDeleted = false
                        };

                        db.P_ORDERS.Add(newOrder);
                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, "POrderController", $"{newOrder.OrderNo} - POrderni yaratdi");


                        List<P_CONTRACT_PARTS> contractParts = db.P_CONTRACT_PARTS.Where(pcp => pcp.ContractID == model.ContractID).ToList();
                        List<string> notInContractParts = new List<string>();

                        foreach (var part in model.Parts)
                        {
                            var contractPart = contractParts.Where(p => p.PartID == part.PartID).FirstOrDefault();
                            if (contractParts.Where(cp => cp.PartID == part.PartID).Any())
                            {
                                var newPart = new P_ORDER_PARTS
                                {
                                    OrderID = newOrder.ID,
                                    PartID = part.PartID,
                                    UnitID = part.UnitID,
                                    Amount = part.Amount
                                };

                                newPart.MOQ = contractPart.MOQ;
                                newPart.Price = contractPart.Price;

                                db.P_ORDER_PARTS.Add(newPart);
                                var logPart = db.PARTS.Find(newPart.PartID);
                                LogHelper.LogToDatabase(User.Identity.Name, "POrderController", $"{logPart.PNo} - POrderPartni yaratdi");
                            }
                            else
                            {
                                PART paart = db.PARTS.Where(p => p.ID == part.PartID).FirstOrDefault();
                                notInContractParts.Add(paart.PNo);
                            }
                        }
                        if (notInContractParts.Count > 0)
                        {
                            var message = "";
                            foreach (var word in notInContractParts)
                            {
                                message += word + " ,";
                            }
                            ModelState.AddModelError("", "Ushbu ehtiyot qism(lar): " + message + " shartnomadan topilmadi, shartnomada yo'q narsaga buyurtma qilib bo'lmaydi! Qaytadan urinib ko'ring!");
                            db.Entry(newOrder).State = EntityState.Deleted;
                            db.SaveChanges();
                            return View(model);
                        }
                        db.SaveChanges();
                        if (model.File != null)
                        {
                            if (Request.Files["docUpload"].ContentLength > 0)
                            {
                                if (Request.Files["docUpload"].InputStream.Length < 5242880)
                                {
                                    if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                                    {
                                        P_ORDER_DOCS orderDoc = new P_ORDER_DOCS();
                                        byte[] avatar = new byte[Request.Files["docUpload"].InputStream.Length];
                                        Request.Files["docUpload"].InputStream.Read(avatar, 0, avatar.Length);
                                        orderDoc.OrderID = newOrder.ID;
                                        orderDoc.Doc = avatar;

                                        db.P_ORDER_DOCS.Add(orderDoc);
                                        db.SaveChanges();

                                        LogHelper.LogToDatabase(User.Identity.Name, "PContractController", $"{orderDoc.P_ORDERS.OrderNo} - uchun POrderDocni yaratdi");
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

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.Supplier = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.PContract = new SelectList(db.P_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        //ViewBag.partList = new SelectList(db.PARTS.Where(x => x.IsDeleted == false).ToList(), "ID", "PNo");
                        ViewBag.partList = new SelectList(Enumerable.Empty<SelectList>());

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
        */
        private List<string> contractExceedingParts = new List<string>();
        private bool checkForContractPartsAmount(PurchasingContractViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<PURCHASING_CONTRACT_ITEMS> contractParts = db.PURCHASING_CONTRACT_ITEMS.Where(pcp => pcp.ContractID == model.ID).ToList();

                foreach (var orderPart in model.Parts)
                {
                    for (int i = 0; i < contractParts.Count; i++)
                    {
                        if (contractParts[i].ItemID == orderPart.ItemID)
                            if (orderPart.Amount > contractParts[i].Amount)
                                contractExceedingParts.Add(contractParts[i].PURCHASING_ITEMS.ItemName);
                    }
                }
                if (contractExceedingParts.Count > 0)
                    return true;
                else
                    return false;
            }
        }
        public ActionResult GetOrderItems(int orderId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Assuming you have a service or context to fetch order items from the database
                var orderItems = db.PURCHASING_ORDER_ITEMS
                    .Include(o => o.PURCHASING_ITEMS)
                    .Include(o => o.PURCHASING_ITEMS.UNIT)
                                     .Where(o => o.OrderID == orderId)
                                     .Select(o => new
                                     {
                                         o.ID,
                                         o.ItemID,
                                         o.PURCHASING_ITEMS.ItemName,
                                         o.Amount,
                                         o.PURCHASING_ITEMS.UNIT.ShortName,
                                         o.Price,
                                         o.TotalPrice
                                     }).ToList();

                return Json(orderItems, JsonRequestBehavior.AllowGet);
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