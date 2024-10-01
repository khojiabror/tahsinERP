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
            using(DBTHSNEntities db = new DBTHSNEntities())
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
                            CreatedByUserID = model.CreatedByUserID,
                            BuyerUserID = model.BuyerUserID,
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

        public ActionResult Edit(int? id)
        {
            return View();
        }
        public ActionResult Delete(int? id)
        {
            return View();
        }
        public ActionResult Details(int? id)
        {
            return View();
        }
    }
}