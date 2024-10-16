using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.Purchasing;

namespace tahsinERP.Controllers.Purchasing
{
    public class PurchasingWhIncomeController : Controller
    {
        PART_WRHS warehouse = null;
        private string ControllerName = "PurchasingWhIncomeController";
        private PART_WRHS GetWarehouseOfMRP(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true).FirstOrDefault();
                PART_WRHS his_warehouse = db.PART_WRHS.Where(wrhs => wrhs.MRP == currentUser.ID).FirstOrDefault();

                return his_warehouse;
            }
        }
        // GET: PurchasingWhIncome
        public ActionResult Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = db.PURCHASING_WRHS_INCOMES.Where(x => x.IsDeleted == false).ToList();
                return View(list);
            }
        }
        public ActionResult GetContractItems(int contractId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Assuming you have a service or context to fetch order items from the database
                var contractItems = db.PURCHASING_CONTRACT_ITEMS
                    .Include(o => o.PURCHASING_ITEMS)
                    .Include(o => o.PURCHASING_ITEMS.UNIT)
                                     .Where(o => o.ContractID == contractId)
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
                                     }).ToList();

                return Json(contractItems, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetContractDetails(int contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var contract = db.PURCHASING_CONTRACTS
                                 .Where(c => c.ID == contractID && c.IsDeleted == false)
                                 .Select(c => new
                                 {
                                     c.ID,
                                     c.Currency,
                                     c.Amount
                                 })
                                 .FirstOrDefault();

                if (contract != null)
                {
                    return Json(new { success = true, data = contract }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Invoice not found." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public ActionResult Create()
        {
            string month = "";
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.items = new SelectList(Enumerable.Empty<SelectListItem>());

                PurchasingWhIncomeViewModel model = new PurchasingWhIncomeViewModel();
                PURCHASING_WRHS_INCOMES income = db.PURCHASING_WRHS_INCOMES.OrderByDescending(w => w.IssueDateTime).FirstOrDefault();

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null) model.WHName = "Siz uchun ombor biriktirilmagan.";
                else model.WHName = warehouse.WHName;

                if (income != null)
                {
                    month = income.DocNo.Substring(0, 1);
                    if (int.Parse(month) == int.Parse(DateTime.Now.Month.ToString()))
                    {
                        int doc_No_number = Convert.ToInt32(income.DocNo.Substring(income.DocNo.LastIndexOf('_') + 1));
                        doc_No_number++;
                        model.DocNo = DateTime.Now.Month + "_" + doc_No_number;
                    }
                    else model.DocNo = DateTime.Now.Month.ToString() + "_1";
                }
                else
                    model.DocNo = DateTime.Now.Month.ToString() + "_1";

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PurchasingWhIncomeViewModel model)
        {
            double sumOfContractAmount = 0;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var itemsTotalAmount = model.Items.Sum(x => x.Amount);
                    var contractItems = db.PURCHASING_CONTRACT_ITEMS.Where(x => x.ContractID == model.ContractID).ToList();

                    if (contractItems != null)
                    {
                        foreach (var invoicePart in contractItems)
                            sumOfContractAmount += invoicePart.Amount;

                        if (itemsTotalAmount > sumOfContractAmount)
                        {
                            ViewBag.contracts = new SelectList(Enumerable.Empty<SelectListItem>());
                            ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                            ViewBag.partList = new SelectList(Enumerable.Empty<SelectListItem>());

                            ModelState.AddModelError("", "Shartnomadan dan ortiqcha hajmni kirim qilib bo'lmaydi.");
                            return View(model);
                        }
                    }
                    warehouse = GetWarehouseOfMRP(User.Identity.Name);
                    if (warehouse is null)
                    {
                        ModelState.AddModelError("", "Sizga tegishli ombor topilmadi.");
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");

                        return View(model);
                    }
                    PURCHASING_WRHS_INCOMES newIncome = new PURCHASING_WRHS_INCOMES()
                    {
                        DocNo = model.DocNo,
                        WHID = warehouse.ID,
                        ContractID = model.ContractID,
                        Amount = model.Amount,
                        Currency = model.Currency,
                        IsDeleted = false,
                        Description = model.Description,
                        IssueDateTime = DateTime.Now,
                        SenderWHID = null,
                        ReceiveStatus = model.RecieveStatus
                    };
                    db.PURCHASING_WRHS_INCOMES.Add(newIncome);
                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{newIncome.DocNo} - PurchasingWrhsIncomeni yaratdi");

                    // Items ni saqlash
                    foreach (var part in model.Items)
                    {
                        PURCHASING_WRHS_INCOME_ITEMS newPart = new PURCHASING_WRHS_INCOME_ITEMS
                        {
                            IncomeID = newIncome.ID,
                            ItemID = part.ItemID,
                            UnitID = part.UnitID,
                            Amount = part.Amount,
                            PiecePrice = (double)part.PiecePrice,
                            TotalPrice = part.TotalPrice,
                            Comment = part.Comment
                        };
                        PURCHASING_ITEM_STOCKS existStock = db.PURCHASING_ITEM_STOCKS.Where(s => s.WHID == newIncome.WHID && s.ItemID == part.ItemID).FirstOrDefault();
                        string partNo;
                        if (existStock == null)
                        {
                            PURCHASING_ITEM_STOCKS newPartStock = new PURCHASING_ITEM_STOCKS
                            {
                                WHID = (int)newIncome.WHID,
                                ItemID = part.ItemID,
                                Unit = db.UNITS.Where(x => x.ID == part.UnitID).FirstOrDefault().ShortName,
                                Amount = part.Amount
                            };
                            db.PURCHASING_ITEM_STOCKS.Add(newPartStock);
                            partNo = db.PURCHASING_ITEMS.Where(x => x.ID == newPartStock.ItemID).FirstOrDefault().ItemName;
                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{partNo} - ItemStockni yaratdi");
                        }
                        else
                        {
                            existStock.Amount = existStock.Amount + part.Amount;
                            partNo = db.PARTS.Where(x => x.ID == existStock.ItemID).FirstOrDefault().PNo;
                        }

                        db.PURCHASING_WRHS_INCOME_ITEMS.Add(newPart);

                        LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{partNo} - PurchasingWrhsIncomeItem ni yaratdi");
                    }

                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Ma'umotlarni to'liq kiritganingizga ishonch hosil qilib, qaytadan urinib ko'ring.");
                    ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                    ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");

                    return View(model);
                }
            }
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var wrhsIncome = db1.PURCHASING_WRHS_INCOMES
                                  .Include(p => p.PURCHASING_CONTRACTS)
                                  .FirstOrDefault(p => p.ID == ID);

                if (wrhsIncome == null)
                {
                    return HttpNotFound();
                }

                var partList = db1.PURCHASING_WRHS_INCOME_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.IncomeID == wrhsIncome.ID)
                                  .ToList();

                ViewBag.partList = partList;

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;


                return View(wrhsIncome);
            }
        }
        public ActionResult Delete(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var wrhsIncome = db1.PURCHASING_WRHS_INCOMES
                                  .Include(p => p.PURCHASING_CONTRACTS)
                                  .FirstOrDefault(p => p.ID == ID);

                if (wrhsIncome == null)
                {
                    return HttpNotFound();
                }

                var partList = db1.PURCHASING_WRHS_INCOME_ITEMS
                                  .Include(pc => pc.PURCHASING_ITEMS)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.IncomeID == wrhsIncome.ID)
                                  .ToList();

                ViewBag.partList = partList;

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;


                return View(wrhsIncome);
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
                    PURCHASING_WRHS_INCOMES whIncomeToDelete = db.PURCHASING_WRHS_INCOMES.Find(ID);
                    if (whIncomeToDelete != null)
                    {
                        whIncomeToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(whIncomeToDelete).State = System.Data.Entity.EntityState.Modified;
                            var purchasingItems = db.PURCHASING_WRHS_INCOME_ITEMS.Where(pc => pc.IncomeID == whIncomeToDelete.ID).ToList();
                            foreach (var whIncomeItem in purchasingItems)
                            {
                                db.PURCHASING_WRHS_INCOME_ITEMS.Remove(whIncomeItem);
                            }
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{whIncomeToDelete.DocNo} - itemWrhsIncomeni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday shartnoma topilmadi.");
                    }
                }
            }
            return View();
        }
        public ActionResult DeletePart(int? id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PURCHASING_WRHS_INCOME_ITEMS purchasingItemToDelete = db.PURCHASING_WRHS_INCOME_ITEMS.Find(id);
                if (ModelState.IsValid)
                {
                    if (purchasingItemToDelete != null)
                    {
                        try
                        {
                            db.PURCHASING_WRHS_INCOME_ITEMS.Remove(purchasingItemToDelete);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.PURCHASING_ITEMS.Find(purchasingItemToDelete.ItemID).ItemName} - purchasingWrhsIncomeItemni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "ContractPart not found.");
                    }
                }
                return View(purchasingItemToDelete);
            }
        }
        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            PURCHASING_WRHS_INCOMES whrsIncome;
            List<PURCHASING_WRHS_INCOME_ITEMS> partList;

            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                whrsIncome = db1.PURCHASING_WRHS_INCOMES
                            .Include(i => i.PURCHASING_CONTRACTS)
                            .FirstOrDefault(p => p.ID == ID);

                if (whrsIncome == null)
                {
                    return HttpNotFound();
                }

                // suppliers = new SelectList(db1.SUPPLIERS.ToList(), "ID", "Name", whrsIncome.SupplierID);

                partList = db1.PURCHASING_WRHS_INCOME_ITEMS
                            .Include(whp => whp.PURCHASING_ITEMS)
                            .Include(whp => whp.UNIT)
                            .Where(whp => whp.IncomeID == ID)
                            .ToList();
                //ViewBag.PartList = new SelectList(db1.PART_WRHS_INCOME_PARTS.Include(p => p.PART).ToList(),"ID","PName");

                ViewBag.contracts = new SelectList(db1.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                ViewBag.units = new SelectList(db1.UNITS.ToList(), "ID", "UnitName");

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;
            }

            // ViewBag.Supplier = suppliers;
            ViewBag.partList = partList;

            return View(whrsIncome);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PURCHASING_WRHS_INCOMES whrsIncome)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db1 = new DBTHSNEntities())
                {
                    PURCHASING_WRHS_INCOMES wrhsIncomeToUpdate = db1.PURCHASING_WRHS_INCOMES.Find(whrsIncome.ID);
                    if (wrhsIncomeToUpdate != null)
                    {
                        wrhsIncomeToUpdate.DocNo = whrsIncome.DocNo;
                        wrhsIncomeToUpdate.WHID = warehouse.ID;
                        wrhsIncomeToUpdate.ContractID = whrsIncome.ContractID;
                        wrhsIncomeToUpdate.Currency = whrsIncome.Currency;
                        wrhsIncomeToUpdate.Amount = whrsIncome.Amount;
                        wrhsIncomeToUpdate.TotalPrice = whrsIncome.TotalPrice;
                        wrhsIncomeToUpdate.Description = whrsIncome.Description;
                        wrhsIncomeToUpdate.IssueDateTime = whrsIncome.IssueDateTime;
                        //wrhsIncomeToUpdate.SenderWHID = whrsIncome.SenderWHID;

                        try
                        {
                            db1.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{wrhsIncomeToUpdate.DocNo} - PurchasingWrhsIncomeni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(wrhsIncomeToUpdate);
                }
            }
            return View(whrsIncome);
        }

        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var whIncomePart = db.PURCHASING_WRHS_INCOME_ITEMS
                                    .Include(p => p.PURCHASING_WRHS_INCOMES)
                                    .Include(p => p.PURCHASING_ITEMS)
                                    .FirstOrDefault(p => p.ID == ID);
                if (whIncomePart == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PURCHASING_ITEMS
                                .Include(p => p.PURCHASING_WRHS_INCOME_ITEMS)
                                .Include(p => p.UNIT)
                                .Select(p => new SelectListItem
                                {
                                    Value = p.ID.ToString(),
                                    Text = p.ItemName
                                }).ToList();

                ViewBag.partList = allParts;
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;

                return View(whIncomePart);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(PURCHASING_WRHS_INCOME_ITEMS whIncomeItem)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PURCHASING_WRHS_INCOME_ITEMS whIncomeItemToUpdate = db.PURCHASING_WRHS_INCOME_ITEMS.Find(whIncomeItem.ID);
                    if (whIncomeItemToUpdate != null)
                    {
                        whIncomeItemToUpdate.ItemID = whIncomeItem.ItemID;
                        whIncomeItemToUpdate.Amount = whIncomeItem.Amount;
                        whIncomeItemToUpdate.UnitID = whIncomeItem.UnitID;
                        whIncomeItemToUpdate.PiecePrice = whIncomeItem.PiecePrice;
                        whIncomeItemToUpdate.Comment = whIncomeItem.Comment;
                        whIncomeItemToUpdate.TotalPrice = whIncomeItem.Amount * whIncomeItem.PiecePrice;

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, ControllerName, $"{db.PURCHASING_ITEMS.Find(whIncomeItemToUpdate.ItemID).ItemName} - PurchasingWrhsIncomeItem ni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(whIncomeItemToUpdate);
                }
            }
            return View();
        }
    }
}