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

namespace tahsinERP.Controllers
{
    public class PurchasingOrderController : Controller
    {
        // GET: PurchosingOrder
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult GetProgressPercentage(int ID)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.PURCHASING_ORDERS.Find(ID);
                if (order == null)
                {
                    return Json(0, JsonRequestBehavior.AllowGet); // Return 0 if order not found
                }

                // Assuming you calculate progress based on some logic
                var progress = CalculateOrderProgress(order.ID); // Implement your logic here

                return Json(progress, JsonRequestBehavior.AllowGet);
            }
        }

        private int CalculateOrderProgress(int orderID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (orderID != 0)
                {
                    // Example logic to calculate progress
                    var existContract = db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted ==false && x.OrderID == orderID).FirstOrDefault();
                    if (existContract != null)
                    {
                        return 100;
                    }
                    /*if (order.Status == "Completed") return 100;
                    if (order.Status == "In Progress") return 50;*/
                    return 0; // Default
                }
                return 0;
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
                        var existOrderCodeEDO = db.PURCHASING_ORDERS.Where(x => x.IsDeleted == false && model.OrderCodeEDO.CompareTo(x.OrderCodeEDO) == 0).FirstOrDefault();
                        if (!string.IsNullOrEmpty(existOrderCodeEDO.OrderCodeEDO))
                        {
                            ModelState.AddModelError("", $"{existOrderCodeEDO.OrderCodeEDO} ushbu raqam bilan buyurtma kiritilgan");
                            ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                            ViewBag.partList = new SelectList(db.PURCHASING_ITEMS.ToList(), "ID", "ItemName");
                            return View(model);
                        }
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
        public ActionResult UpdateItemStatus()
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {
                var itemsList = db.PURCHASING_ORDER_ITEMS.Where(x => x.IsDeleted == false).ToList();
                return View(itemsList);
            }
        }
        [HttpPost]
        public ActionResult UpdateItemStatus(PURCHASING_ORDER_ITEMS orderItem)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
            {

            }
            return View(); 
        }
        public ActionResult Status(string orderNoEDO, string supplierName)
        {
            using(DBTHSNEntities db = new DBTHSNEntities())
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