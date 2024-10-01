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
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.partList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
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
                            OrderCode = model.OrderCode,
                            OrderCodeEDO = model.OrderCodeEDO,
                            Comment = model.Comment,
                            Status = model.Status,
                            CreatedByUserID = GetUserID(User.Identity.Name)
                        };

                        db.PURCHASING_ORDERS.Add(newOrder);
                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{newOrder.OrderCode} - PurchasingOrder ni yaratdi");


                        List<PURCHASING_ORDER_ITEMS> purchasingOrderParts = db.PURCHASING_ORDER_ITEMS.Where(pci => pci.OrderID == model.ID).ToList();

                        foreach (var part in model.Parts)
                        {
                            var newPart = new PURCHASING_ORDER_ITEMS
                            {
                                OrderID = newOrder.ID,
                                ItemID = part.ItemID,
                                Price = part.Price,
                                Currency = part.Currency
                            };

                            db.PURCHASING_ORDER_ITEMS.Add(newPart);
                            var logItem = db.PURCHASING_ITEMS.Find(newPart.ItemID);
                            LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{logItem.ItemName} - PurchasingOrderPartni yaratdi");

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
                                        PURCHASING_ORDER_DOCS purchasingOrderDoc = new PURCHASING_ORDER_DOCS();
                                        byte[] avatar = new byte[Request.Files["docUpload"].InputStream.Length];
                                        Request.Files["docUpload"].InputStream.Read(avatar, 0, avatar.Length);
                                        purchasingOrderDoc.OrderID = newOrder.ID;
                                        purchasingOrderDoc.Doc = avatar;

                                        db.PURCHASING_ORDER_DOCS.Add(purchasingOrderDoc);
                                        db.SaveChanges();

                                        LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $" kodi {newOrder.OrderCode} - uchun PurchasingOrderDoc yaratdi");
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