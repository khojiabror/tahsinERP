using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.POrder;
using System.IO;
using tahsinERP.ViewModels.PurchasingOrder;
using System.Net;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace tahsinERP.Controllers
{
    public class PurchasingOrderController : Controller
    {
        // GET: PurchosingOrder
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_ORDERS.ToList();
                return View(list);
            }
        }

        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.userList = new SelectList(
                                    db.USERS.Where(u => u.ROLES.Any(r => r.RName == "Buyer"))
                                    .ToList(), "ID", "Fullname");

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.itemList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
            }
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PurchasingOrderViewModel model)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {

                    if (ModelState.IsValid)
                    {
                        var newOrder = new PURCHASING_ORDERS()
                        {
                            OrderCode = model.OrderCodeEDO,
                            OrderCodeEDO = model.OrderCodeEDO,
                            Comment = model.Comment,
                            Status = "Jarayonda",
                            CreatedByUserID = GetUserID(User.Identity.Name),
                            BuyerUserID = model.BuyerUserID,
                            CreatedDT = model.ApprovedDTEDO,
                            ApprovedDTEDO = model.ApprovedDTEDO,
                            OrderedByUser = model.OrderByUser,
                            OrderedByDept = model.OrderByDept
                        };

                        db.PURCHASING_ORDERS.Add(newOrder);
                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{newOrder.OrderCode} - buyurtmani kiritdi");

                        foreach (var part in model.Parts)
                        {
                            var newPart = new PURCHASING_ORDER_ITEMS
                            {
                                OrderID = newOrder.ID,
                                ItemID = part.ItemID,
                                Price = part.Price,
                                Amount = part.Amount,
                                Currency = part.Currency
                            };

                            db.PURCHASING_ORDER_ITEMS.Add(newPart);
                            PURCHASING_ITEMS pItem = db.PURCHASING_ITEMS.Where(pi => pi.ID == newPart.ItemID).FirstOrDefault();
                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{newOrder.OrderCode} - buyurtmasiga {pItem.ItemName} - predmetni biriktirdi.");

                        }
                        db.SaveChanges();
                        if (model.File != null)
                        {
                            if (model.File.ContentLength < 5242880)
                            {
                                if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                                {
                                    PURCHASING_ORDER_DOCS purchasingOrderDoc = new PURCHASING_ORDER_DOCS();
                                    byte[] fileData = new byte[model.File.InputStream.Length];
                                    model.File.InputStream.Read(fileData, 0, fileData.Length);

                                    purchasingOrderDoc.OrderID = newOrder.ID;
                                    purchasingOrderDoc.Doc = fileData;

                                    db.PURCHASING_ORDER_DOCS.Add(purchasingOrderDoc);
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"Buyurtma: {newOrder.OrderCode} uchun fayl yukladi");
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Format noto'g'ri. Faqat .pdf fayllarni yuklash mumkin.");
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", "Faylni yuklab bo'lmadi, u 5MBdan kattaroq. Qayta urinib ko'ring, agar muammo yana qaytarilsa, tizim administratoriga murojaat qiling.");
                                throw new RetryLimitExceededException();
                            }

                        }

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        ViewBag.partList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
                        //ViewBag.partList = new SelectList(Enumerable.Empty<SelectList>());

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

        private int GetUserID(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                int userID = db.USERS.Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true).Select(u => u.ID).FirstOrDefault();
                return userID;
            }

        }
        public ActionResult DownloadDoc(int? purchasingOrderID)
        {
            if (purchasingOrderID == null)
            {
                return Json(new { success = false, message = "PurchasingOrder ID ko'rsatilmagan." }, JsonRequestBehavior.AllowGet);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingOrderDoc = db.PURCHASING_ORDER_DOCS
                                .Include(x => x.PURCHASING_ORDERS)
                                .FirstOrDefault(pi => pi.OrderID == purchasingOrderID);
                if (purchasingOrderDoc != null)
                {
                    var contractNo = purchasingOrderDoc.PURCHASING_ORDERS.OrderCode;
                    // Returning the file for download via AJAX can be handled differently, so return success here
                    return File(purchasingOrderDoc.Doc, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contractNo + "_ContractDoc.pdf");
                }
                else
                {
                    return Json(new { success = false, message = "Fayl yuklanmagan." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            PURCHASING_ORDERS purchasingOrder;
            List<PURCHASING_ORDER_ITEMS> partList;
            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                purchasingOrder = db1.PURCHASING_ORDERS.Find(ID);
                if (purchasingOrder == null)
                    return HttpNotFound();

                db1.Entry(purchasingOrder).Reference(o => o.USER).Load();
                db1.Entry(purchasingOrder).Reference(o => o.USER1).Load();

                partList = db1.PURCHASING_ORDER_ITEMS
                    .Include(x => x.PURCHASING_ITEMS)
                    .Where(pc => pc.OrderID == ID).ToList();
            }

            ViewBag.partList = partList;
            return View(purchasingOrder);
        }
        public ActionResult Delete(int? ID)
        {
            if (ID == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            PURCHASING_ORDERS purchasingOrder;
            List<PURCHASING_ORDER_ITEMS> partList;
            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                purchasingOrder = db1.PURCHASING_ORDERS.Find(ID);
                if (purchasingOrder == null)
                    return HttpNotFound();

                db1.Entry(purchasingOrder).Reference(o => o.USER).Load();
                db1.Entry(purchasingOrder).Reference(o => o.USER1).Load();

                partList = db1.PURCHASING_ORDER_ITEMS
                            .Include(x => x.PURCHASING_ITEMS)
                            .Where(pc => pc.OrderID == ID).ToList();
            }

            ViewBag.partList = partList;
            return View(purchasingOrder);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int? ID, FormCollection gfs)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_ORDERS purchasingorderToDelete = db.PURCHASING_ORDERS.Find(ID);
                    if (purchasingorderToDelete != null)
                    {
                        purchasingorderToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(purchasingorderToDelete).State = EntityState.Modified;

                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{purchasingorderToDelete.OrderCode} - PurchasingOrderni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday sotuv buyurtma topilmadi.");
                    }
                }
            }
            return View();
        }
        public ActionResult DeletePart(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_ORDER_ITEMS purchasingOrderItemToDelete = db.PURCHASING_ORDER_ITEMS.Find(id);
                if (ModelState.IsValid)
                {
                    if (purchasingOrderItemToDelete != null)
                    {
                        try
                        {
                            db.PURCHASING_ORDER_ITEMS.Remove(purchasingOrderItemToDelete);
                            db.SaveChanges();
                            var orderItem = db.PURCHASING_ORDER_ITEMS.Include(x => x.PURCHASING_ITEMS).Where(x => x.ID == id).FirstOrDefault();
                            LogHelper.LogToDatabase(User.Identity.Name, "POrderController", $"{orderItem.PURCHASING_ITEMS.ItemName}  - PurchasingOrderni o'chirdii");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "OrderPart not found.");
                    }
                }
                return View(purchasingOrderItemToDelete);
            }
        }
        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            PURCHASING_ORDERS purchasingOrder;
            List<PURCHASING_ORDER_ITEMS> partList;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                purchasingOrder = db.PURCHASING_ORDERS
                    .Include(x => x.USER1)
                    .Include(x => x.USER)
                    .FirstOrDefault(c => c.ID == ID && c.IsDeleted == false);

                if (purchasingOrder == null)
                {
                    return HttpNotFound();
                }

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                partList = db.PURCHASING_ORDER_ITEMS
                                .Include(x => x.PURCHASING_ITEMS)
                                .Where(x => x.IsDeleted == false && x.OrderID == ID)
                                .ToList();
                ViewBag.userList = new SelectList(
                                    db.USERS.Where(u => u.ROLES.Any(r => r.RName == "Buyer"))
                                    .ToList(), "ID", "Fullname");
            }

            ViewBag.partList = partList;
            return View(purchasingOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PURCHASING_ORDERS purchasingOrder)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_ORDERS purchasingOrderToUpdate = db.PURCHASING_ORDERS.Find(purchasingOrder.ID);
                    if (purchasingOrderToUpdate != null)
                    {
                        purchasingOrderToUpdate.OrderCode = purchasingOrder.OrderCodeEDO;
                        purchasingOrderToUpdate.OrderCodeEDO = purchasingOrder.OrderCodeEDO;
                        purchasingOrderToUpdate.BuyerUserID = purchasingOrder.BuyerUserID;
                        purchasingOrderToUpdate.Comment = purchasingOrder.Comment;
                        purchasingOrderToUpdate.ApprovedDTEDO = purchasingOrder.ApprovedDTEDO;
                        purchasingOrderToUpdate.CreatedDT = purchasingOrder.CreatedDT;
                        purchasingOrderToUpdate.OrderedByDept = purchasingOrder.OrderedByDept;
                        purchasingOrderToUpdate.OrderedByUser = purchasingOrder.OrderedByUser;
                        try
                        {
                            // Correct this line
                            //db.Entry(purchasingOrderToUpdate).State = EntityState.Modified;
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{purchasingOrderToUpdate.OrderCode} - kodli tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(purchasingOrderToUpdate);
                }
            }
            var userEmail = User.Identity.Name;
            LogHelper.LogToDatabase(userEmail, "PurchasingOrderController", "Edit[Post]");
            return View(purchasingOrder);
        }

        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingOrderItem = db.PURCHASING_ORDER_ITEMS.Find(ID);
                if (purchasingOrderItem == null)
                {
                    return HttpNotFound();
                }
                db.Entry(purchasingOrderItem).Reference(x => x.PURCHASING_ORDERS).Load();
                var allParts = db.PURCHASING_ITEMS.Where(x => x.IsDeleted == false).ToList();

                ViewBag.partList = new SelectList(allParts, "ID", "ItemName");

                return View(purchasingOrderItem);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(PURCHASING_ORDER_ITEMS purchasingOrderItem)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_ORDER_ITEMS purchasingOrdetItemToUpdate = db.PURCHASING_ORDER_ITEMS.Find(purchasingOrderItem.ID);
                    if (purchasingOrdetItemToUpdate != null)
                    {
                        purchasingOrdetItemToUpdate.Price = purchasingOrderItem.Price;
                        purchasingOrdetItemToUpdate.Amount = purchasingOrderItem.Amount;
                        purchasingOrdetItemToUpdate.TotalPrice = purchasingOrderItem.TotalPrice;
                        purchasingOrdetItemToUpdate.Currency = purchasingOrderItem.Currency;

                        //purchasingOrdetItemToUpdate.Amount = purchasingOrderItem.Quantity * purchasingOrderItem.Price; SQL o'zi chiqarib beradi

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "POrderController", $"{purchasingOrdetItemToUpdate.ItemID} - li POrderPartni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(purchasingOrdetItemToUpdate);
                }
            }
            return View();
        }
    }
}