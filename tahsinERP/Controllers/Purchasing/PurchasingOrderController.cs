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
using tahsinERP.ViewModels.Purchasing;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Mvc;
using Antlr.Runtime.Misc;
using DocumentFormat.OpenXml.EMMA;

namespace tahsinERP.Controllers
{
    public class PurchasingOrderController : Controller
    {
        // GET: PurchosingOrder
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_ORDERS
                    .Include(x => x.CURRENCy)
                    .Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult GetProgressPercentage(int ID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                double orderPercentage = 0, number = 0;
                var order = db.PURCHASING_ORDERS.Include(x => x.PURCHASING_ORDER_ITEMS).Where(x => x.ID == ID).FirstOrDefault();
                if (order == null)
                {
                    return Json(0, JsonRequestBehavior.AllowGet); // Return 0 if order not found
                }
                else
                {
                    foreach (var item in order.PURCHASING_ORDER_ITEMS)
                    {
                        number += item.Progress;
                    }
                    orderPercentage = number / order.PURCHASING_ORDER_ITEMS.Count * 100;
                    return Json((int)Math.Round(orderPercentage), JsonRequestBehavior.AllowGet);
                }
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
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

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

                    //if (ModelState.IsValid)
                    //{
                    var existOrderCodeEDO = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && model.OrderCodeEDO.CompareTo(x.OrderCodeEDO) == 0).FirstOrDefault();
                    if (existOrderCodeEDO != null)
                    {
                        ModelState.AddModelError("", $"{existOrderCodeEDO.OrderCodeEDO} ushbu raqam bilan buyurtma kiritilgan");
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
                        ViewBag.partList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
                        ViewBag.userList = new SelectList(
                                db.USERS.Where(u => u.ROLES.Any(r => r.RName == "Buyer"))
                                .ToList(), "ID", "Fullname");

                        return View(model);
                    }
                    else
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
                            OrderedByUser = model.OrderedByUser,
                            OrderedByDept = model.OrderedByDept,
                            CurrencyID = model.CurrencyID,
                            Progress = 0
                        };

                        db.PURCHASING_ORDERS.Add(newOrder);
                        db.SaveChanges();
                        LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{newOrder.OrderCodeEDO} - raqamli buyurtmani tizimga kiritdi");

                        foreach (var part in model.Parts)
                        {
                            var newPart = new PURCHASING_ORDER_ITEMS
                            {
                                OrderID = newOrder.ID,
                                ItemID = part.ItemID,
                                UnitID = part.UnitID,
                                Price = part.Price,
                                Amount = part.Amount
                            };

                            db.PURCHASING_ORDER_ITEMS.Add(newPart);
                            PURCHASING_ITEMS pItem = db.PURCHASING_ITEMS.Where(pi => pi.ID == newPart.ItemID).FirstOrDefault();
                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{newOrder.OrderCode} - buyurtmasiga {pItem.ItemName} - predmetni biriktirdi.");

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

                                    LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"Buyurtma: {newOrder.OrderCode} uchun fayl yukladi");
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
                    }

                    return RedirectToAction("Index");
                    //}
                    //else
                    //{
                    //    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                    //    ViewBag.partList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
                    //    ViewBag.userList = new SelectList(
                    //                db.USERS.Where(u => u.ROLES.Any(r => r.RName == "Buyer"))
                    //                .ToList(), "ID", "Fullname");
                    //    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                    //    return View(model);
                    //}
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
                db1.Entry(purchasingOrder).Reference(o => o.CURRENCy).Load();

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
                db1.Entry(purchasingOrder).Reference(o => o.CURRENCy).Load();

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

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingorderToDelete.OrderCodeEDO} - buyurtmani tizimdan o'chirib yubordi");

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
                PURCHASING_ORDER_ITEMS purchasingOrderItemToDelete = db.PURCHASING_ORDER_ITEMS.Include(i => i.PURCHASING_ORDERS).Where(i => i.ItemID == id).FirstOrDefault();
                if (ModelState.IsValid)
                {
                    if (purchasingOrderItemToDelete != null)
                    {
                        try
                        {
                            db.PURCHASING_ORDER_ITEMS.Remove(purchasingOrderItemToDelete);
                            db.SaveChanges();
                            var orderItem = db.PURCHASING_ORDER_ITEMS.Include(x => x.PURCHASING_ITEMS).Where(x => x.ID == id).FirstOrDefault();
                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingOrderItemToDelete.PURCHASING_ORDERS.OrderCodeEDO} buyurtmasidagi {orderItem.PURCHASING_ITEMS.ItemName} - predmetni o'chirib yubordi.");

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


            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var purchasingOrder = db.PURCHASING_ORDERS
                    .Include(x => x.USER1)
                    .Include(x => x.USER)
                    .Include(x => x.CURRENCy)
                    .FirstOrDefault(c => c.ID == ID && c.IsDeleted == false);

                if (purchasingOrder == null)
                {
                    return HttpNotFound();
                }
                var model = new PurchasingOrderViewModel
                {
                    ID = purchasingOrder.ID,
                    OrderCodeEDO = purchasingOrder.OrderCodeEDO,
                    CreatedDT = purchasingOrder.CreatedDT,
                    CreatedByUserID = purchasingOrder.CreatedByUserID,
                    ApprovedDTEDO = (DateTime)purchasingOrder.ApprovedDTEDO,
                    Comment = purchasingOrder.Comment,
                    OrderedByDept = purchasingOrder.OrderedByDept,
                    OrderedByUser = purchasingOrder.OrderedByUser,
                    CurrencyID = (int)purchasingOrder.CurrencyID,
                    BuyerUserID = purchasingOrder.BuyerUserID,
                    Parts = purchasingOrder.PURCHASING_ORDER_ITEMS.Select(x => new PurchasingOrderPart
                    {
                        ID = x.ID,
                        Item = x.PURCHASING_ITEMS,
                        ItemID = x.ItemID,
                        Price = x.Price,
                        Amount = x.Amount
                    }).ToList()
                };

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.userList = new SelectList(db.USERS.Where(u => u.ROLES.Any(r => r.RName == "Buyer")).ToList(), "ID", "Fullname");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PurchasingOrderViewModel purchasingOrder)
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
                        purchasingOrderToUpdate.CreatedDT = purchasingOrder.ApprovedDTEDO;
                        purchasingOrderToUpdate.OrderedByDept = purchasingOrder.OrderedByDept;
                        purchasingOrderToUpdate.OrderedByUser = purchasingOrder.OrderedByUser;
                        purchasingOrderToUpdate.CurrencyID = purchasingOrder.CurrencyID;

                        db.Entry(purchasingOrderToUpdate).State = EntityState.Modified;
                        db.SaveChanges();
                        foreach (var part in purchasingOrder.Parts)
                        {
                            var existingPart = db.PURCHASING_ORDER_ITEMS.Include(i => i.PURCHASING_ORDERS).Where(i => i.ItemID == part.ID).FirstOrDefault();
                            if (existingPart != null)
                            {
                                existingPart.Price = part.Price;
                                //existingPart.UnitID = product.UnitID;
                                existingPart.Amount = part.Amount;
                                existingPart.TotalPrice = part.Amount * part.Price;

                                db.Entry(existingPart).State = EntityState.Modified;
                                try
                                {
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingOrderToUpdate.OrderCodeEDO} buyurtmasidagi {existingPart.PURCHASING_ITEMS.ItemName} - ni tahrirladi");
                                }
                                catch (RetryLimitExceededException)
                                {
                                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                                }
                            }
                        }
                        try
                        {
                            // Correct this line
                            //db.Entry(purchasingOrderToUpdate).State = EntityState.Modified;
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingOrderToUpdate.OrderCode} - kodli buyurtmani tahrirladi");

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
                    PURCHASING_ORDER_ITEMS purchasingOrdetItemToUpdate = db.PURCHASING_ORDER_ITEMS.Include(i => i.PURCHASING_ORDERS).Include(i => i.PURCHASING_ITEMS).Where(i => i.ItemID == purchasingOrderItem.ID).FirstOrDefault();
                    if (purchasingOrdetItemToUpdate != null)
                    {
                        purchasingOrdetItemToUpdate.Price = purchasingOrderItem.Price;
                        purchasingOrdetItemToUpdate.Amount = purchasingOrderItem.Amount;
                        purchasingOrdetItemToUpdate.TotalPrice = purchasingOrderItem.TotalPrice;

                        //purchasingOrdetItemToUpdate.Amount = purchasingOrderItem.Quantity * purchasingOrderItem.Price; SQL o'zi chiqarib beradi

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingOrdetItemToUpdate.PURCHASING_ORDERS.OrderCodeEDO} buyurtmasidagi {purchasingOrdetItemToUpdate.PURCHASING_ITEMS.ItemName} - predmetini tahrirladi");

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
        public ActionResult UpdateItemStatus(int? ID)
        {
            if (ID == null)
                HttpNotFound();
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var orderItems = db.PURCHASING_ORDER_ITEMS.Include(x => x.PURCHASING_ITEMS)
                                                          .Include(x => x.PURCHASING_ORDERS)
                                                          .Where(x => x.IsDeleted == false && x.OrderID == ID).ToList();
                return View(orderItems);
            }
        }
        [HttpPost]
        public ActionResult UpdateItemStatus(int ID, string action, string comment)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (!string.IsNullOrEmpty(comment))
                    {
                        if (action == "approve")
                        {
                            // Approve logic
                            PURCHASING_ORDER_ITEMS purchasingItemToApprove = db.PURCHASING_ORDER_ITEMS.Find(ID);

                            purchasingItemToApprove.Comment = comment;
                            purchasingItemToApprove.Progress = 1;

                            db.Entry(purchasingItemToApprove).State = EntityState.Modified;
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Xarid buyurtmalari", $"{purchasingItemToApprove.OrderID} li buyurtmani 'yakunlandi' deb tasdiqladi");

                            return RedirectToAction("Index");
                        }
                        else if (action == "disapprove")
                        {
                            //    PURCHASING_ORDER_ITEMS purchasingItemToApprove = db.PURCHASING_ORDER_ITEMS.Find(ID);
                            //    purchasingItemToApprove.Comment = comment;

                            //    PURCHASING_ORDERS purchasingOrder = new PURCHASING_ORDERS();
                            //    purchasingOrder = db.PURCHASING_ORDERS.Find(purchasingItemToApprove.OrderID);
                            //    //Progress update
                            //    double totalStatusCount = db.PURCHASING_ORDER_ITEMS.Count(x => x.ID == ID);

                            //    db.Entry(purchasingItemToApprove).State = EntityState.Modified;
                            //    db.SaveChanges();

                            //    LogHelper.LogToDatabase(User.Identity.Name, "PurchasingOrderController", $"{purchasingItemToApprove.OrderID} li buyurtadan olib tashlandi");

                            return RedirectToAction("Index");
                        }
                        else
                            return HttpNotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Izoh yozish majburiy, izohni to'ldiring");
                        return RedirectToAction("UpdateItemStatus", ID);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View();
        }

        public ActionResult Status(string orderNoEDO, string supplierName)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<PurchasingStatusViewModel> purchasingStatuses = new List<PurchasingStatusViewModel>();

                if (!string.IsNullOrEmpty(orderNoEDO))
                {
                    if (!string.IsNullOrEmpty(supplierName))
                    {
                        int orderID = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.OrderCodeEDO.CompareTo(orderNoEDO) == 0).FirstOrDefault().ID;
                        var contractsBySupplier = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false && x.Supplier.CompareTo(supplierName) == 0)
                                                                         .Select(x => x.ID);

                        purchasingStatuses = (from orderItem in db.PURCHASING_ORDER_ITEMS
                                              join order in db.PURCHASING_ORDERS on orderItem.OrderID equals order.ID
                                              join contract in db.PURCHASING_CONTRACTS on order.ID equals contract.OrderID into contractGroup
                                              from contract in contractGroup.DefaultIfEmpty()

                                                  // Remove additional joins related to contract items or group them by OrderItemID to avoid duplicates
                                              let contractItem = db.PURCHASING_CONTRACT_ITEMS
                                                                  .Where(ci => ci.ContractID == contract.ID)
                                                                  .FirstOrDefault()

                                              join payment in db.PURCHASING_CONTRACT_PAYMENTS on contract.ID equals payment.ContractID into paymentGroup
                                              from payment in paymentGroup.DefaultIfEmpty()

                                              join income in db.PURCHASING_WRHS_INCOMES on contract.ID equals income.ContractID into incomeGroup
                                              from income in incomeGroup.DefaultIfEmpty()

                                              join incomeItem in db.PURCHASING_WRHS_INCOME_ITEMS on income.ID equals incomeItem.IncomeID into incomeItemGroup
                                              from incomeItem in incomeItemGroup.DefaultIfEmpty()

                                              where orderItem.IsDeleted == false &&
                                                    (contract == null || contract.IsDeleted == false) &&
                                                    (payment == null || payment.IsDeleted == false) &&
                                                    (income == null || income.IsDeleted == false) &&
                                                    contract.Supplier == supplierName &&
                                                    orderItem.OrderID == orderID

                                              // Group by OrderItemID to avoid repetition
                                              group new { orderItem, order, contract, contractItem, payment, incomeItem } by orderItem.ID into grouped

                                              select new PurchasingStatusViewModel
                                              {
                                                  ItemName = grouped.FirstOrDefault().orderItem.PURCHASING_ITEMS.ItemName,
                                                  OrderedByDept = grouped.FirstOrDefault().order.OrderedByDept,
                                                  OrderAmount = grouped.FirstOrDefault().orderItem.Amount,
                                                  Supplier = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.Supplier : string.Empty,

                                                  ContractAmount = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.Amount : 0,
                                                  IncomeAmount = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.Amount : 0,

                                                  // Additional IDs
                                                  OrderItemID = grouped.FirstOrDefault().orderItem.ID,
                                                  OrderID = grouped.FirstOrDefault().order.ID,
                                                  ContractID = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.ID : 0,
                                                  ContractItemID = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.ID : 0,
                                                  PaymentID = grouped.FirstOrDefault().payment != null ? grouped.FirstOrDefault().payment.ID : 0,
                                                  IncomeID = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.ID : 0,

                                                  // New percentage variables
                                                  ContractAmountPercentage = (grouped.FirstOrDefault().contractItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().contractItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0,

                                                  PaymentAmountPercentage = (grouped.FirstOrDefault().payment != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().payment.Payment_Amount / grouped.FirstOrDefault().contract.Amount) * 100 : 0,

                                                  IncomeAmountPercentage = (grouped.FirstOrDefault().incomeItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().incomeItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0
                                              }).ToList();
                    }
                    else
                    {
                        int orderID = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && x.OrderCodeEDO.CompareTo(orderNoEDO) == 0).FirstOrDefault().ID;

                        purchasingStatuses = (from orderItem in db.PURCHASING_ORDER_ITEMS
                                              join order in db.PURCHASING_ORDERS on orderItem.OrderID equals order.ID
                                              join contract in db.PURCHASING_CONTRACTS on order.ID equals contract.OrderID into contractGroup
                                              from contract in contractGroup.DefaultIfEmpty()

                                                  // Remove additional joins related to contract items or group them by OrderItemID to avoid duplicates
                                              let contractItem = db.PURCHASING_CONTRACT_ITEMS
                                                                  .Where(ci => ci.ContractID == contract.ID)
                                                                  .FirstOrDefault()

                                              join payment in db.PURCHASING_CONTRACT_PAYMENTS on contract.ID equals payment.ContractID into paymentGroup
                                              from payment in paymentGroup.DefaultIfEmpty()

                                              join income in db.PURCHASING_WRHS_INCOMES on contract.ID equals income.ContractID into incomeGroup
                                              from income in incomeGroup.DefaultIfEmpty()

                                              join incomeItem in db.PURCHASING_WRHS_INCOME_ITEMS on income.ID equals incomeItem.IncomeID into incomeItemGroup
                                              from incomeItem in incomeItemGroup.DefaultIfEmpty()

                                              where orderItem.IsDeleted == false &&
                                                    (contract == null || contract.IsDeleted == false) &&
                                                    (payment == null || payment.IsDeleted == false) &&
                                                    (income == null || income.IsDeleted == false) &&
                                                    orderItem.OrderID == orderID

                                              // Group by OrderItemID to avoid repetition
                                              group new { orderItem, order, contract, contractItem, payment, incomeItem } by orderItem.ID into grouped

                                              select new PurchasingStatusViewModel
                                              {
                                                  ItemName = grouped.FirstOrDefault().orderItem.PURCHASING_ITEMS.ItemName,
                                                  OrderedByDept = grouped.FirstOrDefault().order.OrderedByDept,
                                                  OrderAmount = grouped.FirstOrDefault().orderItem.Amount,
                                                  Supplier = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.Supplier : string.Empty,

                                                  ContractAmount = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.Amount : 0,
                                                  IncomeAmount = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.Amount : 0,

                                                  // Additional IDs
                                                  OrderItemID = grouped.FirstOrDefault().orderItem.ID,
                                                  OrderID = grouped.FirstOrDefault().order.ID,
                                                  ContractID = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.ID : 0,
                                                  ContractItemID = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.ID : 0,
                                                  PaymentID = grouped.FirstOrDefault().payment != null ? grouped.FirstOrDefault().payment.ID : 0,
                                                  IncomeID = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.ID : 0,

                                                  // New percentage variables
                                                  ContractAmountPercentage = (grouped.FirstOrDefault().contractItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().contractItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0,

                                                  PaymentAmountPercentage = (grouped.FirstOrDefault().payment != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().payment.Payment_Amount / grouped.FirstOrDefault().contract.Amount) * 100 : 0,

                                                  IncomeAmountPercentage = (grouped.FirstOrDefault().incomeItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().incomeItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0
                                              }).ToList();
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(supplierName))
                    {
                        purchasingStatuses = (from orderItem in db.PURCHASING_ORDER_ITEMS
                                              join order in db.PURCHASING_ORDERS on orderItem.OrderID equals order.ID
                                              join contract in db.PURCHASING_CONTRACTS on order.ID equals contract.OrderID into contractGroup
                                              from contract in contractGroup.DefaultIfEmpty()

                                                  // Remove additional joins related to contract items or group them by OrderItemID to avoid duplicates
                                              let contractItem = db.PURCHASING_CONTRACT_ITEMS
                                                                  .Where(ci => ci.ContractID == contract.ID)
                                                                  .FirstOrDefault()

                                              join payment in db.PURCHASING_CONTRACT_PAYMENTS on contract.ID equals payment.ContractID into paymentGroup
                                              from payment in paymentGroup.DefaultIfEmpty()

                                              join income in db.PURCHASING_WRHS_INCOMES on contract.ID equals income.ContractID into incomeGroup
                                              from income in incomeGroup.DefaultIfEmpty()

                                              join incomeItem in db.PURCHASING_WRHS_INCOME_ITEMS on income.ID equals incomeItem.IncomeID into incomeItemGroup
                                              from incomeItem in incomeItemGroup.DefaultIfEmpty()

                                              where orderItem.IsDeleted == false &&
                                                    (contract == null || contract.IsDeleted == false) &&
                                                    (payment == null || payment.IsDeleted == false) &&
                                                    (income == null || income.IsDeleted == false) &&
                                                    contract.Supplier == supplierName

                                              // Group by OrderItemID to avoid repetition
                                              group new { orderItem, order, contract, contractItem, payment, incomeItem } by orderItem.ID into grouped

                                              select new PurchasingStatusViewModel
                                              {
                                                  ItemName = grouped.FirstOrDefault().orderItem.PURCHASING_ITEMS.ItemName,
                                                  OrderedByDept = grouped.FirstOrDefault().order.OrderedByDept,
                                                  OrderAmount = grouped.FirstOrDefault().orderItem.Amount,
                                                  Supplier = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.Supplier : string.Empty,

                                                  ContractAmount = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.Amount : 0,
                                                  IncomeAmount = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.Amount : 0,

                                                  // Additional IDs
                                                  OrderItemID = grouped.FirstOrDefault().orderItem.ID,
                                                  OrderID = grouped.FirstOrDefault().order.ID,
                                                  ContractID = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.ID : 0,
                                                  ContractItemID = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.ID : 0,
                                                  PaymentID = grouped.FirstOrDefault().payment != null ? grouped.FirstOrDefault().payment.ID : 0,
                                                  IncomeID = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.ID : 0,

                                                  // New percentage variables
                                                  ContractAmountPercentage = (grouped.FirstOrDefault().contractItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().contractItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0,

                                                  PaymentAmountPercentage = (grouped.FirstOrDefault().payment != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().payment.Payment_Amount / grouped.FirstOrDefault().contract.Amount) * 100 : 0,

                                                  IncomeAmountPercentage = (grouped.FirstOrDefault().incomeItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().incomeItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0
                                              }).ToList();
                    }
                    else
                    {
                        purchasingStatuses = (from orderItem in db.PURCHASING_ORDER_ITEMS
                                              join order in db.PURCHASING_ORDERS on orderItem.OrderID equals order.ID
                                              join contract in db.PURCHASING_CONTRACTS on order.ID equals contract.OrderID into contractGroup
                                              from contract in contractGroup.DefaultIfEmpty()

                                                  // Remove additional joins related to contract items or group them by OrderItemID to avoid duplicates
                                              let contractItem = db.PURCHASING_CONTRACT_ITEMS
                                                                  .Where(ci => ci.ContractID == contract.ID)
                                                                  .FirstOrDefault()

                                              join payment in db.PURCHASING_CONTRACT_PAYMENTS on contract.ID equals payment.ContractID into paymentGroup
                                              from payment in paymentGroup.DefaultIfEmpty()

                                              join income in db.PURCHASING_WRHS_INCOMES on contract.ID equals income.ContractID into incomeGroup
                                              from income in incomeGroup.DefaultIfEmpty()

                                              join incomeItem in db.PURCHASING_WRHS_INCOME_ITEMS on income.ID equals incomeItem.IncomeID into incomeItemGroup
                                              from incomeItem in incomeItemGroup.DefaultIfEmpty()

                                              where orderItem.IsDeleted == false &&
                                                    (contract == null || contract.IsDeleted == false) &&
                                                    (payment == null || payment.IsDeleted == false) &&
                                                    (income == null || income.IsDeleted == false)

                                              // Group by OrderItemID to avoid repetition
                                              group new { orderItem, order, contract, contractItem, payment, incomeItem } by orderItem.ID into grouped

                                              select new PurchasingStatusViewModel
                                              {
                                                  ItemName = grouped.FirstOrDefault().orderItem.PURCHASING_ITEMS.ItemName,
                                                  OrderedByDept = grouped.FirstOrDefault().order.OrderedByDept,
                                                  OrderAmount = grouped.FirstOrDefault().orderItem.Amount,
                                                  Supplier = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.Supplier : string.Empty,

                                                  ContractAmount = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.Amount : 0,
                                                  IncomeAmount = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.Amount : 0,

                                                  // Additional IDs
                                                  OrderItemID = grouped.FirstOrDefault().orderItem.ID,
                                                  OrderID = grouped.FirstOrDefault().order.ID,
                                                  ContractID = grouped.FirstOrDefault().contract != null ? grouped.FirstOrDefault().contract.ID : 0,
                                                  ContractItemID = grouped.FirstOrDefault().contractItem != null ? grouped.FirstOrDefault().contractItem.ID : 0,
                                                  PaymentID = grouped.FirstOrDefault().payment != null ? grouped.FirstOrDefault().payment.ID : 0,
                                                  IncomeID = grouped.FirstOrDefault().incomeItem != null ? grouped.FirstOrDefault().incomeItem.ID : 0,

                                                  // New percentage variables
                                                  ContractAmountPercentage = (grouped.FirstOrDefault().contractItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().contractItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0,

                                                  PaymentAmountPercentage = (grouped.FirstOrDefault().payment != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().payment.Payment_Amount / grouped.FirstOrDefault().contract.Amount) * 100 : 0,

                                                  IncomeAmountPercentage = (grouped.FirstOrDefault().incomeItem != null && grouped.FirstOrDefault().orderItem.Amount > 0)
                                                      ? (grouped.FirstOrDefault().incomeItem.Amount / grouped.FirstOrDefault().orderItem.Amount) * 100 : 0
                                              }).ToList();

                    }
                }
                var supplierList = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false)
                                                               .Select(x => x.Supplier)
                                                               .Distinct()
                                                               .ToList();
                ViewBag.SupplierList = new SelectList(supplierList);

                var orderList = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false)
                                                                  .Select(x => x.OrderCodeEDO)
                                                                  .ToList();
                ViewBag.PurchasingOrderList = new SelectList(orderList);

                return View(purchasingStatuses);
            }
        }
    }
}