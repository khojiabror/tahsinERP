using System;
using System.Collections.Generic;
using System.Linq;
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
        public ActionResult Create()
        {
            string month = "";
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.contracts = new SelectList(db.PURCHASING_CONTRACTS.Where(x => x.IsDeleted == false).ToList(), "ID", "ContractNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");

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


    }
}