using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class ExpenseController : Controller
    {
        private string[] sources = ConfigurationManager.AppSettings["partTypes"].Split(',');
        Warehouse warehouse = null;
        // GET: WhExpense
        public ActionResult Index(string type, int? supplierID)
        {
            this.warehouse = GetWarehouseOfMRP(User.Identity.Name);
            warehouse = GetWarehouseOfMRP(User.Identity.Name);
            if (warehouse is null)
                ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
            else
                ViewBag.WHName = warehouse.WHName;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (warehouse == null)
                {
                    ViewBag.SourceList = new SelectList(sources, type);
                    List<EXPENS> list = new List<EXPENS>();
                    return View(list);
                }
                else
                {
                    var list = db.EXPENSES
                        .Include(pe => pe.Warehouse)
                        .Include(x => x.CURRENCy)
                        .Where(pe => pe.IsDeleted == false && pe.Warehouse.ID == warehouse.ID)
                        .ToList();

                    warehouse = GetWarehouseOfMRP(User.Identity.Name);
                    if (warehouse is null)
                        ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                    else
                        ViewBag.WHName = warehouse.WHName;

                    return View(list);
                }
            }
        }
        private Warehouse GetWarehouseOfMRP(string username)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.FirstOrDefault(u => u.Uname == username && u.IsDeleted != true && u.IsActive);
                Warehouse his_warehouse = currentUser != null ? db.WAREHOUSES.FirstOrDefault(wrhs => wrhs.MRP == currentUser.ID) : null;

                return his_warehouse;
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var partInStock = from part in db.PRODUCTS
                                  join stock in db.PRODUCT_STOCKS on part.ID equals stock.ProductID
                                  where part.IsDeleted == false
                                  select part;

                // Populate dropdown lists
                ViewBag.PartWrhs = new SelectList(db.WAREHOUSES.Where(w => w.IsDeleted == false).ToList(), "ID", "WHName");
                ViewBag.InComes = new SelectList(db.EXPENSES.Where(wi => wi.IsDeleted == false).ToList(), "ID", "DocNo");
                ViewBag.InComeParts = new SelectList(partInStock.ToList(), "ID", "PNo");
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                // Initialize view model
                WrhsExpenseViewModel viewModel = new WrhsExpenseViewModel();
                EXPENS expense = db.EXPENSES.OrderByDescending(p => p.ID).FirstOrDefault();

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    viewModel.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    viewModel.WHName = warehouse.WHName;

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;

                if (expense == null)
                {
                    viewModel.DocNo = DateTime.Now.Month + "_" + 1;
                }
                else
                {
                    var monthAndNumber = expense.DocNo.Split('_');
                    if (int.Parse(monthAndNumber[0]) == DateTime.Now.Month)
                    {
                        int docNoNumber = int.Parse(monthAndNumber[1]) + 1;
                        viewModel.DocNo = DateTime.Now.Month + "_" + docNoNumber;
                    }
                    else
                    {
                        viewModel.DocNo = DateTime.Now.Month + "_" + 1;
                    }
                }

                return View(viewModel);
            }
        }

        [HttpPost]
        public ActionResult Create(WrhsExpenseViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (!ModelState.IsValid)
                {
                    PopulateViewBags(db);
                    return View(model);
                }
                // Validate ReceiverWhID exists
                var receiverWarehouse = db.WAREHOUSES.FirstOrDefault(w => w.ID == model.RecieverWHID && w.IsDeleted == false);
                if (receiverWarehouse == null)
                {
                    ModelState.AddModelError("RecieverWHID", "The selected warehouse does not exist.");
                    PopulateViewBags(db);
                    return View(model);
                }
                // add or subtract exist parts 
                foreach (var part in model.Parts)
                {
                    var existStock = db.PRODUCT_STOCKS.FirstOrDefault(x => x.ProductID == part.PartID);
                    if (existStock == null)
                    {
                        PopulateViewBags(db);
                        ModelState.AddModelError("", "Omborda bunday ehtiyot qism topilmadi.");
                        return View(model);
                    }
                    else if (existStock.Amount >= part.Amount)
                    {
                        existStock.Amount = existStock.Amount - part.Amount;
                        db.SaveChanges();
                    }
                    else
                    {
                        PopulateViewBags(db);
                        ModelState.AddModelError("", "Kiritilgan miqdor CHIQIM qilish uchun ombordagi bor imkoniyatdan oshib ketdi.");
                        return View(model);
                    }
                }
                // Create a new EXPENS record
                EXPENS newExpense = new EXPENS
                {
                    DocNo = model.DocNo,
                    ReceiverWhID = model.RecieverWHID,
                    CurrencyID = model.CurrencyID,
                    IsDeleted = false,
                    Description = model.Description,
                    IssueDateTime = DateTime.Now,
                    SendStatus = true,
                    SenderWHID = 1 // Assuming SenderWHID is always 1
                };

                db.EXPENSES.Add(newExpense);
                db.SaveChanges();

                // Update SenderWHID with the newly created ExpenseID
                //newExpense.SenderWHID = GetWarehouseOfMRP(User.Identity.Name).ID;
                db.Entry(newExpense).State = EntityState.Modified;
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "WhExpenseController", $"{newExpense.DocNo} - PartWrhsExpenseni yaratdi");
                double expenseTotalPrice = 0;
                // Save parts
                foreach (var item in model.Parts)
                {
                    var existPartInCome = db.INCOME_PRODUCTS.Where(x => x.ProductID == item.PartID).ToList().LastOrDefault();
                    var expensePart = new EXPENSE_PRODUCTS
                    {
                        ExpenseID = newExpense.ID,
                        ProductID = item.PartID,
                        UnitID = item.UnitID,
                        Amount = item.Amount,
                        PiecePrice = Convert.ToDouble(existPartInCome.PiecePrice),
                        Comment = item.Comment,
                        TotalPrice = Convert.ToDouble(item.Amount * existPartInCome.PiecePrice)
                    };
                    expenseTotalPrice += (double)expensePart.TotalPrice;
                    db.EXPENSE_PRODUCTS.Add(expensePart);
                    db.SaveChanges();
                }
                newExpense.TotalPrice = expenseTotalPrice;
                var updateExpense = db.EXPENSES.Find(newExpense.ID);
                updateExpense.TotalPrice = expenseTotalPrice;
                db.SaveChanges();

                return RedirectToAction("Index");
            }
        }
        private void PopulateViewBags(DBTHSNEntities db)
        {
            var partInStock = from part in db.PRODUCTS
                              join stock in db.PRODUCT_STOCKS on part.ID equals stock.ProductID
                              where part.IsDeleted == false
                              select part;

            ViewBag.PartWrhs = new SelectList(db.WAREHOUSES.Where(w => w.IsDeleted == false).ToList(), "ID", "WHName");
            ViewBag.InComes = new SelectList(db.EXPENSES.Where(wi => wi.IsDeleted == false).ToList(), "ID", "DocNo");
            ViewBag.InComeParts = new SelectList(partInStock.ToList(), "ID", "PNo");
            ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
            ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");
        }
        public ActionResult Details(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var wrhsExpense = db.EXPENSES
                                  .Include(x => x.Warehouse)
                                  .Include(x => x.CURRENCy)
                                  .FirstOrDefault(p => p.ID == ID);

                if (wrhsExpense == null)
                {
                    return HttpNotFound();
                }

                var partList = db.EXPENSE_PRODUCTS
                                  .Include(pc => pc.PRODUCT)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.ExpenseID == wrhsExpense.ID)
                                  .ToList();

                ViewBag.PartList = partList;
                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;

                return View(wrhsExpense);
            }
        }

        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            EXPENS wrhsExpense;
            List<EXPENSE_PRODUCTS> partList;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                wrhsExpense = db.EXPENSES
                    .Include(x => x.CURRENCy)
                    .FirstOrDefault(p => p.ID == ID);

                if (wrhsExpense == null)
                {
                    return HttpNotFound();
                }

                // suppliers = new SelectList(db.SUPPLIERS.ToList(), "ID", "Name", wrhsExpense.SupplierID);

                partList = db.EXPENSE_PRODUCTS
                    .Include(whp => whp.PRODUCT)
                    .Include(pc => pc.UNIT)
                    .Where(whp => whp.ExpenseID == ID)
                    .ToList();
                //ViewBag.PartList = new SelectList(db.Warehouse_EXPENSE_PARTS.Include(p => p.PART).ToList(),"ID","PName");
                // ViewBag.Invoices = new SelectList(db.P_INVOICES.Where(i => i.IsDeleted == false).ToList(), "ID", "InvoiceNo");
                ViewBag.PartWhrs = new SelectList(db.WAREHOUSES.Where(i => i.IsDeleted == false).ToList(), "ID", "WHName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;
            }

            // ViewBag.Supplier = suppliers;
            ViewBag.partList = partList;

            return View(wrhsExpense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EXPENS wrhsExpense)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    EXPENS wrhsExpenseToUpdate = db.EXPENSES.Find(wrhsExpense.ID);
                    if (wrhsExpenseToUpdate != null)
                    {
                        //.DocNo = wrhsExpense.DocNo;
                        wrhsExpenseToUpdate.ReceiverWhID = wrhsExpense.ReceiverWhID;
                        wrhsExpenseToUpdate.CurrencyID = wrhsExpense.CurrencyID;
                        //wrhsExpenseToUpdate.Amount = wrhsExpense.Amount;
                        // wrhsExpenseToUpdate.TotalPrice = wrhsExpense.TotalPrice;
                        wrhsExpenseToUpdate.Description = wrhsExpense.Description;
                        wrhsExpenseToUpdate.IssueDateTime = wrhsExpense.IssueDateTime;
                        //wrhsExpenseToUpdate.IsDeleted = false;
                        //wrhsExpenseToUpdate.SenderWHID = wrhsExpense.SenderWHID;

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhExpenseController", $"{wrhsExpenseToUpdate.DocNo} - PartWrhsExpenseni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(wrhsExpenseToUpdate);
                }
            }
            return View(wrhsExpense);
        }
        //hali toliq ozgartitilmagan

        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var whExpensePart = db.EXPENSE_PRODUCTS
                                    .Include(p => p.EXPENS)
                                    .Include(p => p.PRODUCT)
                                    .Include(pc => pc.UNIT)
                                    .FirstOrDefault(p => p.ID == ID);
                if (whExpensePart == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PRODUCTS
                                .Include(p => p.EXPENSE_PRODUCTS)
                                .Include(pc => pc.UNIT)
                                .Select(p => new SelectListItem
                                {
                                    Value = p.ID.ToString(),
                                    Text = p.PNo
                                }).ToList();

                ViewBag.PartList = allParts;
                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;

                return View(whExpensePart);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPart(EXPENSE_PRODUCTS whExpensePart)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    EXPENSE_PRODUCTS whExpensePartToUpdate = db.EXPENSE_PRODUCTS.Find(whExpensePart.ID);
                    db.Entry(whExpensePartToUpdate).Reference(x => x.EXPENS).Load();

                    var expense = db.EXPENSES.Where(x => x.ID == whExpensePart.ExpenseID && x.IsDeleted == false).FirstOrDefault();

                    if (whExpensePartToUpdate != null)
                    {
                        whExpensePartToUpdate.ProductID = whExpensePart.ProductID;
                        whExpensePartToUpdate.Amount = whExpensePart.Amount;
                        whExpensePartToUpdate.UnitID = whExpensePart.UnitID;
                        whExpensePartToUpdate.Comment = whExpensePart.Comment;
                        whExpensePartToUpdate.TotalPrice = whExpensePart.Amount * whExpensePart.PiecePrice;

                        try
                        {
                            expense.TotalPrice = db.EXPENSE_PRODUCTS.Where(x => x.ExpenseID == whExpensePart.ExpenseID).Sum(x => x.TotalPrice);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhExpenseController", $"{whExpensePartToUpdate.PRODUCT.PNo} - PartWrhsExpensePartni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(whExpensePartToUpdate);
                }
            }
            return View();
        }
        public ActionResult Delete(int? Id)
        {
            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var wrhsExpense = db.EXPENSES.Find(Id);
                if (wrhsExpense == null)
                {
                    return HttpNotFound();
                }
                else
                    ViewBag.partList = db.EXPENSE_PRODUCTS
                        .Include(pc => pc.PRODUCT)
                        .Include(pc => pc.UNIT)
                        .Where(pc => pc.ExpenseID == wrhsExpense.ID).ToList();

                db.Entry(wrhsExpense).Reference(i => i.Warehouse).Load();
                db.Entry(wrhsExpense).Reference(i => i.CURRENCy).Load();

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;

                return View(wrhsExpense);
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
                    EXPENS whExpenseToDelete = db.EXPENSES.Find(ID);
                    if (whExpenseToDelete != null)
                    {
                        whExpenseToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(whExpenseToDelete).State = System.Data.Entity.EntityState.Modified;

                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhExpenseController", $"{whExpenseToDelete.DocNo} - PartWrhsExpenseni o'chirdi");

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
                EXPENSE_PRODUCTS whExpensePartToDelete = db.EXPENSE_PRODUCTS.Find(id);
                if (ModelState.IsValid)
                {
                    if (whExpensePartToDelete != null)
                    {
                        try
                        {
                            db.Entry(whExpensePartToDelete).Reference(x => x.EXPENS).Load();

                            var expense = db.EXPENSES.Find(whExpensePartToDelete.ExpenseID);
                            expense.TotalPrice -= whExpensePartToDelete.TotalPrice;

                            db.Entry(whExpensePartToDelete).Reference(X => X.PRODUCT).Load();
                            db.EXPENSE_PRODUCTS.Remove(whExpensePartToDelete);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhExpenseController", $"{whExpensePartToDelete.PRODUCT.PNo} - PartWrhsExpensePartni o'chirdi");

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
                return View(whExpensePartToDelete);
            }
        }
        public async Task<ActionResult> Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES invoys = db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("ombor_chiqim.xlsx") == 0).FirstOrDefault();
                if (invoys != null)
                    return File(invoys.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                return View();
            }
        }

        public ActionResult UploadWithExcel()
        {
            ViewBag.IsFileUploaded = false;
            warehouse = GetWarehouseOfMRP(User.Identity.Name);
            if (warehouse is null)
                ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
            else
                ViewBag.WHName = warehouse.WHName;
            return View();
        }

        [HttpPost]
        public ActionResult UploadWithExcel(HttpPostedFileBase file)
        {
            // Process uploaded file
            // ...
            return View();
        }
    }
}
