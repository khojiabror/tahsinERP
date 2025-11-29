using DocumentFormat.OpenXml.Drawing.Charts;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class WhIncomeController : Controller
    {
        private string[] sources = ConfigurationManager.AppSettings["partTypes"].Split(',');
        Warehouse warehouse = null;

        // GET: WhIncome
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
                    ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.Type.CompareTo(type) == 0 && s.IsDeleted == false).ToList(), "ID", "Name");
                    List<INCOME> list = new List<INCOME>();
                    return View(list);
                }
                else
                {
                    if (!string.IsNullOrEmpty(type))
                    {
                        if (supplierID.HasValue)
                        {
                            List<INCOME> list = db.INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.Currency)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SUPPLIER.Type.CompareTo(type) == 0 && pi.P_INVOICES.SupplierID == supplierID && pi.Warehouse.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.Type.CompareTo(type) == 0 && s.IsDeleted == false).ToList(), "ID", "Name", supplierID);
                            return View(list);
                        }
                        else
                        {
                            List<INCOME> list = db.INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.Currency)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SUPPLIER.Type.CompareTo(type) == 0 && pi.Warehouse.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.Type.CompareTo(type) == 0 && s.IsDeleted == false).ToList(), "ID", "Name");
                            return View(list);
                        }
                    }
                    else
                    {
                        if (supplierID.HasValue)
                        {
                            List<INCOME> list = db.INCOMES
                                .Include(pr => pr.Currency)
                                .Include(pr => pr.P_INVOICES)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SupplierID == supplierID && pi.Warehouse.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.IsDeleted == false).ToList(), "ID", "Name", supplierID);
                            return View(list);
                        }
                        else
                        {
                            List<INCOME> list = db.INCOMES
                                .Include(pr => pr.Currency)
                                .Include(pr => pr.P_INVOICES)
                                .Where(pi => pi.IsDeleted == false && pi.Warehouse.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.IsDeleted == false).ToList(), "ID", "Name");
                            return View(list);
                        }
                    }
                }

            }
        }
        private Warehouse GetWarehouseOfMRP(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true).FirstOrDefault();
                Warehouse his_warehouse = db.WAREHOUSES.Where(wrhs => wrhs.MRP == currentUser.ID).FirstOrDefault();

                return his_warehouse;
            }
        }
        public JsonResult GetInvoicesBySupplier(int supplierID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoices = db.P_INVOICES
                    .Where(x => x.IsDeleted == false && x.SupplierID == supplierID)
                    .Select(x => new
                    {
                        x.ID,
                        x.InvoiceNo,
                        x.CurrencyID,
                        x.CURRENCy, // Include Currency in the response
                    })
                    .ToList();

                return Json(invoices.Select(i => new
                {
                    Value = i.ID.ToString(),
                    Text = i.InvoiceNo,
                    Currency = i.CURRENCy.Shortname
                }), JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetInvoiceDetails(int invoiceID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.P_INVOICES
                                 .Where(c => c.ID == invoiceID && c.IsDeleted == false)
                                 .Select(c => new
                                 {
                                     c.ID,
                                     c.CurrencyID,
                                     c.CURRENCy
                                 })
                                 .FirstOrDefault();

                if (invoice != null)
                {
                    return Json(new { success = true, data = invoice }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Invoys topilmadi." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public JsonResult GetPriceAndMOQ(int partID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoicePart = db.P_INVOICE_PRODUCTS
                                     .Where(cp => cp.PartID == partID)
                                     .Select(cp => new
                                     {
                                         Price = cp.Price,
                                         Amount = cp.Quantity
                                     })
                                     .FirstOrDefault();

                if (invoicePart != null)
                {
                    return Json(new { success = true, data = invoicePart }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Ehtiyot qism shartnomada topilmadi." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        // Bu method tanlangan shartnomaga tegishli qismlar ro'yxatini qaytaradi
        public ActionResult GetPartList(int invoiceID)
        {
            using (var db = new DBTHSNEntities())
            {
                var partList = db.P_INVOICE_PRODUCTS
                    .Where(cp => cp.InvoiceID == invoiceID)
                    .Select(cp => new
                    {
                        cp.PartID,
                        cp.PRODUCT.PNo
                    })
                    .ToList();

                return Json(new { success = true, data = partList }, JsonRequestBehavior.AllowGet);
            }
        }
        public string GenerateDocNo(INCOME income)
        {
            // If the income is not null and has a DocNo with "_", split it and process
            if (income != null && income.DocNo.Contains("_"))
            {
                // Split the DocNo to get month and document number parts
                string[] docParts = income.DocNo.Split('_');
                int incomeMonth = int.Parse(docParts[0]);
                int currentMonth = DateTime.Now.Month;

                // Check if the income document month matches the current month
                if (incomeMonth == currentMonth)
                {
                    // Increment the document number if it's the current month
                    int docNoNumber = int.Parse(docParts[1]);
                    docNoNumber++;
                    return $"{currentMonth}_{docNoNumber}";
                }
                else
                {
                    // Start numbering from 1 if it's a different month
                    return $"{currentMonth}_1";
                }
            }
            else
            {
                // If no previous document exists, start with the current month and "1"
                return $"{DateTime.Now.Month}_1";
            }
        }

        public ActionResult Create()
        {
            string month = "";
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                ViewBag.Invoices = new SelectList(Enumerable.Empty<SelectListItem>());
                ViewBag.partList = new SelectList(Enumerable.Empty<SelectListItem>());
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                WrhsIncomeViewModel model = new WrhsIncomeViewModel();
                INCOME income = db.INCOMES.OrderByDescending(w => w.IssueDateTime).FirstOrDefault();
                warehouse = GetWarehouseOfMRP(User.Identity.Name);

                if (warehouse is null)
                    model.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    model.WHName = warehouse.WHName;

                model.DocNo = GenerateDocNo(income);

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(WrhsIncomeViewModel model)
        {
            double sumOfInvoiceAmount = 0;
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var sumModelPartsQuantity = model.Parts.Sum(x => x.Amount);
                var invoiceParts = db.P_INVOICE_PRODUCTS.Where(x => x.InvoiceID == model.InvoiceID).ToList();

                if (invoiceParts != null)
                {
                    foreach (var invoicePart in invoiceParts)
                        sumOfInvoiceAmount += invoicePart.Quantity;
                    if (sumModelPartsQuantity > sumOfInvoiceAmount)
                    {
                        ViewBag.Invoices = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.units = new SelectList(db.UNITS.ToList(), "ID", "UnitName");
                        ViewBag.partList = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.Suppliers = new SelectList(db.SUPPLIERS.Where(x => x.IsDeleted == false).ToList(), "ID", "Name");
                        ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                        ModelState.AddModelError("", "Invoice dan ortiqcha hajmni kirim qilib bo'lmaydi.");
                        return View(model);
                    }
                }
                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                {
                    ModelState.AddModelError("", "Sizga tegishli ombor topilmadi.");
                    return View(model);
                }
                INCOME newIncome = new INCOME
                {
                    DocNo = model.DocNo,
                    ReceivingWHID = warehouse.ID,
                    InvoiceID = model.InvoiceID,
                    Amount = model.Amount,
                    CurrencyID = model.CurrencyID,
                    IsDeleted = false,
                    Description = model.Description,
                    IssueDateTime = DateTime.Now,
                    SenderWHID = null,
                    RecieveStatus = true
                };
                db.INCOMES.Add(newIncome);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{newIncome.DocNo} - raqamli hujjat bilan kirim qildi");

                // Yangi yozuvning IncomeID sini olish
                int newIncomeID = newIncome.ID;
                DateTime toDate = DateTime.Now.Date;
                // Parts ni saqlash
                foreach (var part in model.Parts)
                {
                    INCOME_PRODUCTS newPart = new INCOME_PRODUCTS
                    {
                        IncomeID = newIncomeID, // part.IncomeID emas, yangi yaratilgan IncomeID ishlatiladi
                        ProductID = part.PartID,
                        UnitID = part.UnitID,
                        Amount = part.Amount,
                        PiecePrice = (double)part.PiecePrice,
                        //TotalPrice = part.TotalPrice,
                        Comment = part.Comment
                    };
                    PRODUCT_STOCKS existStock = db.PRODUCT_STOCKS.Where(s => s.WHID == newIncome.ReceivingWHID && s.ProductID == part.PartID).FirstOrDefault();
                    string partNo;
                    if (existStock == null)
                    {
                        PRODUCT_STOCKS newPartStock = new PRODUCT_STOCKS
                        {
                            WHID = (int)newIncome.ReceivingWHID,
                            ProductID = part.PartID,
                            UnitID = part.UnitID,
                            Amount = part.Amount
                        };
                        db.PRODUCT_STOCKS.Add(newPartStock);
                        partNo = db.PRODUCTS.Where(x => x.ID == newPartStock.ProductID).FirstOrDefault().PNo;
                        LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{partNo} - qism uchun qoldiq shakllantirdi");
                    }
                    else
                    {
                        existStock.Amount = existStock.Amount + part.Amount;
                        partNo = db.PRODUCTS.Where(x => x.ID == existStock.ProductID).FirstOrDefault().PNo;
                    }

                    db.INCOME_PRODUCTS.Add(newPart);

                    LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{partNo} - qismini {warehouse.WHName} omboriga kirim qildi");
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }
        }
        public JsonResult GetPartsByInvoice(int invoiceId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var parts = db.P_INVOICE_PRODUCTS
                              .Where(p => p.InvoiceID == invoiceId)
                              .Select(p => new { p.ID, p.PRODUCT.PNo }) // Assuming PNo is the part number
                              .ToList();

                return Json(parts, JsonRequestBehavior.AllowGet);

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
                var wrhsIncome = db1.INCOMES
                                  .Include(p => p.P_INVOICES)
                                  .Include(p => p.Currency)
                                  .FirstOrDefault(p => p.ID == ID);

                if (wrhsIncome == null)
                {
                    return HttpNotFound();
                }

                var partList = db1.INCOME_PRODUCTS
                                  .Include(pc => pc.PRODUCT)
                                  .Include(pc => pc.UNIT)
                                  .Where(pc => pc.IncomeID == wrhsIncome.ID)
                                  .ToList();

                ViewBag.PartList = partList;

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;


                return View(wrhsIncome);
            }
        }

        public ActionResult Edit(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }


            using (DBTHSNEntities db1 = new DBTHSNEntities())
            {
                var whrsIncome = db1.INCOMES
                    .Include(i => i.P_INVOICES)
                    .Include(p => p.Currency)
                    .FirstOrDefault(p => p.ID == ID);

                if (whrsIncome == null)
                {
                    return HttpNotFound();
                }
                var model = new WrhsIncomeViewModel
                {
                    ID = whrsIncome.ID,
                    DocNo = whrsIncome.DocNo,
                    InvoiceID = whrsIncome.InvoiceID ?? 0,  // Use 0 as the default if null
                    Amount = whrsIncome.Amount ?? 0.0,      // Use 0.0 as the default if null
                    CurrencyID = whrsIncome.CurrencyID, // Use 0 as the default if null
                    Description = whrsIncome.Description,
                    Parts = whrsIncome.INCOME_PRODUCTS.Select(p => new ViewModels.WhrsIncomePart
                    {
                        ID = p.ID,
                        PartID = p.ProductID,
                        Part = p.PRODUCT,
                        UnitID = p.UnitID,
                        Amount = p.Amount,             // Use 0.0 as the default if null
                        PiecePrice = p.PiecePrice ?? 0.0,     // Use 0.0 as the default if null
                        Comment = p.Comment
                    }).ToList()
                };


                // suppliers = new SelectList(db1.SUPPLIERS.ToList(), "ID", "Name", whrsIncome.SupplierID);

                //ViewBag.PartList = new SelectList(db1.INCOME_PRODUCTS.Include(p => p.PRODUCT).ToList(),"ID","PName");
                ViewBag.Invoices = new SelectList(db1.P_INVOICES.Where(i => i.IsDeleted == false).ToList(), "ID", "InvoiceNo");
                ViewBag.Waybills = new SelectList(db1.F_WAYBILLS.Where(w => w.IsDeleted == false).ToList(), "ID", "WaybillNo");
                ViewBag.units = new SelectList(db1.UNITS.ToList(), "ID", "UnitName");
                ViewBag.currencies = new SelectList(db1.CURRENCIES.ToList(), "ID", "Shortname");

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(WrhsIncomeViewModel whrsIncome)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    INCOME wrhsIncomeToUpdate = db.INCOMES.Find(whrsIncome.ID);
                    if (wrhsIncomeToUpdate != null)
                    {
                        wrhsIncomeToUpdate.DocNo = whrsIncome.DocNo;
                        wrhsIncomeToUpdate.InvoiceID = whrsIncome.InvoiceID;
                        wrhsIncomeToUpdate.CurrencyID = whrsIncome.CurrencyID;
                        wrhsIncomeToUpdate.Amount = whrsIncome.Amount;
                        wrhsIncomeToUpdate.Description = whrsIncome.Description;
                        wrhsIncomeToUpdate.IssueDateTime = whrsIncome.IssueDateTime;
                        // wrhsIncomeToUpdate.WHID = whrsIncome.WHID;
                        //wrhsIncomeToUpdate.WaybillID = whrsIncome.WaybillID;
                        //wrhsIncomeToUpdate.TotalPrice = whrsIncome.TotalPrice;
                        //wrhsIncomeToUpdate.SenderWHID = whrsIncome.SenderWHID;

                        db.Entry(wrhsIncomeToUpdate).State = EntityState.Modified;
                        db.SaveChanges();
                        double summ = 0;
                        foreach (var part in whrsIncome.Parts)
                        {
                            var existingPart = db.INCOME_PRODUCTS.Include(p => p.INCOME.Warehouse).Include(p => p.PRODUCT).Where(p => p.ProductID == part.ID).FirstOrDefault();
                            if (existingPart != null)
                            {
                                existingPart.PiecePrice = part.PiecePrice;
                                existingPart.UnitID = part.UnitID;
                                existingPart.Amount = part.Amount;
                                existingPart.TotalPrice = part.Amount * part.PiecePrice;

                                summ += (double)(existingPart.TotalPrice);
                                db.Entry(existingPart).State = EntityState.Modified;
                                try
                                {
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{existingPart.INCOME.Warehouse.WHName} omboriga qilingan kirimdagi {existingPart.PRODUCT.PNo} - qismni kirimini tahrirladi");
                                }
                                catch (RetryLimitExceededException)
                                {
                                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                                }
                            }
                        }
                        //invoysdagi miqdor o'zgarishi
                        wrhsIncomeToUpdate.TotalPrice = summ;
                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{wrhsIncomeToUpdate.DocNo} - raqamli kirim hujjatini tahrirladi");

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
        //hali toliq ozgartitilmagan

        public ActionResult EditPart(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var whIncomePart = db.INCOME_PRODUCTS
                                    .Include(p => p.INCOME)
                                    .Include(p => p.PRODUCT)
                                    .FirstOrDefault(p => p.ID == ID);
                if (whIncomePart == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PRODUCTS
                                .Include(p => p.INCOME_PRODUCTS)
                                .Include(p => p.UNIT)
                                .Select(p => new SelectListItem
                                {
                                    Value = p.ID.ToString(),
                                    Text = p.PNo
                                }).ToList();

                ViewBag.PartList = allParts;
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
        public ActionResult EditPart(INCOME_PRODUCTS whIncomePart)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    INCOME_PRODUCTS whIncomePartToUpdate = db.INCOME_PRODUCTS.Include(p => p.INCOME.Warehouse).Include(p => p.PRODUCT).Where(p => p.ProductID == whIncomePart.ID).FirstOrDefault();
                    if (whIncomePartToUpdate != null)
                    {
                        whIncomePartToUpdate.ProductID = whIncomePart.ProductID;
                        whIncomePartToUpdate.Amount = whIncomePart.Amount;
                        whIncomePartToUpdate.UnitID = whIncomePart.UnitID;
                        whIncomePartToUpdate.PiecePrice = whIncomePart.PiecePrice;
                        whIncomePartToUpdate.Comment = whIncomePart.Comment;
                        //whIncomePartToUpdate.Amount = whIncomePart.Quantity * whIncomePart.Price; SQL o'zi chiqarib beradi

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{whIncomePartToUpdate.INCOME.Warehouse.WHName} omboriga qilingan kirimdagi {whIncomePartToUpdate.PRODUCT.PNo} - qismni kirimini tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    return View(whIncomePartToUpdate);
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
                var whrsIncome = db.INCOMES.Find(Id);
                if (whrsIncome == null)
                {
                    return HttpNotFound();
                }
                else
                    ViewBag.partList = db.INCOME_PRODUCTS
                        .Include(pc => pc.PRODUCT)
                        .Include(pc => pc.UNIT)
                        .Where(pc => pc.IncomeID == whrsIncome.ID).ToList();

                db.Entry(whrsIncome).Reference(i => i.P_INVOICES).Load();
                db.Entry(whrsIncome).Reference(i => i.Currency).Load();

                warehouse = GetWarehouseOfMRP(User.Identity.Name);
                if (warehouse is null)
                    ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    ViewBag.WHName = warehouse.WHName;
                return View(whrsIncome);
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
                    INCOME whIncomeToDelete = db.INCOMES.Find(ID);
                    if (whIncomeToDelete != null)
                    {
                        whIncomeToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(whIncomeToDelete).State = System.Data.Entity.EntityState.Modified;
                            /*var contractParts = db.INCOME_PRODUCTS.Where(pc => pc.IncomeID == whIncomeToDelete.ID).ToList();
                            foreach (var whIncomePart in contractParts)
                            {
                                db.INCOME_PRODUCTS.Remove(whIncomePart);
                            }*/
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{whIncomeToDelete.DocNo} - raqamli kirim hujjatini o'chirdi");

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
                INCOME_PRODUCTS contractPartToDelete = db.INCOME_PRODUCTS.Include(p => p.INCOME.Warehouse).Include(p => p.PRODUCT).Where(p => p.ProductID == id).FirstOrDefault();
                if (ModelState.IsValid)
                {
                    if (contractPartToDelete != null)
                    {
                        try
                        {
                            db.INCOME_PRODUCTS.Remove(contractPartToDelete);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{contractPartToDelete.INCOME.Warehouse.WHName} omboriga {contractPartToDelete.INCOME.DocNo} hujjat raqami bilan kirim qilingan {contractPartToDelete.PRODUCT.PNo} - ehtiyot qismini o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "ehtiyot qism topilmadi.");
                    }
                }
                return View(contractPartToDelete);
            }
        }
        public async Task<ActionResult> Download()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SAMPLE_FILES invoys = db.SAMPLE_FILES.Where(s => s.FileName.CompareTo("ombor_kirim.xlsx") == 0).FirstOrDefault();
                if (invoys != null)
                    return File(invoys.File, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                return View();
            }
        }
        public ActionResult ClearDataTable()
        {
            ViewBag.DataTable = null;
            ViewBag.DataTableModel = null;
            ViewBag.IsFileUploaded = false;
            ViewBag.Message = "Jadval ma'lumotlari o'chirib yuborildi.";
            return View("UploadWithExcel");
        }
        public ActionResult UploadWithExcel()
        {
            warehouse = GetWarehouseOfMRP(User.Identity.Name);
            if (warehouse is null)
                ViewBag.WHName = "Siz uchun ombor biriktirilmagan.";
            else
                ViewBag.WHName = warehouse.WHName;

            ViewBag.IsFileUploaded = false;
            return View();
        }
        private List<string> missingInvoices = new List<string>();
        private List<string> missingWarehouses = new List<string>();
        private List<string> missingParts = new List<string>();
        private string whName, invoiceNo, docNo, partNo, waybillNo = "";

        private async Task<bool> CheckForExistenceOfInvoices(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string invoiceNo = row["Invoice No."].ToString();

                    bool invoiceExists = await db.P_INVOICES.AnyAsync(p => p.InvoiceNo.CompareTo(invoiceNo) == 0);
                    if (!invoiceExists)
                    {
                        if (!missingInvoices.Contains(invoiceNo))
                        {
                            missingInvoices.Add(invoiceNo);
                        }
                        return false;  // Return false immediately if any invoice is missing
                    }
                }
            }

            return true;  // Return true if all contracts exist
        }
        private async Task<bool> CheckForExistenceOfParts(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string partPNo = row["Part Number"].ToString();

                    bool partExists = await db.PRODUCTS.AnyAsync(p => p.PNo.CompareTo(partPNo) == 0);
                    if (!partExists)
                    {
                        if (!missingParts.Contains(partPNo))
                        {
                            missingParts.Add(partPNo);
                        }
                        return false;  // Return false immediately if any part is missing
                    }
                }
            }

            return true;  // Return true if all parts exist
        }
        private async Task<bool> CheckForExistenceOfWarehouses(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string warehouseName = row["Warehouse Name"].ToString();

                    bool warehouseExists = await db.WAREHOUSES.AnyAsync(s => s.WHName.CompareTo(warehouseName) == 0);
                    if (!warehouseExists)
                    {
                        if (!missingWarehouses.Contains(warehouseName))
                        {
                            missingWarehouses.Add(warehouseName);
                        }
                        return false;  // Return false immediately if any supplier is missing
                    }
                }
            }

            return true;  // Return true if all suppliers exist
        }
        [HttpPost]
        public async Task<ActionResult> UploadWithExcel(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                if (Path.GetExtension(file.FileName).ToLower() == ".xlsx")
                {
                    try
                    {
                        var dataTable = new System.Data.DataTable();
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                        using (var package = new ExcelPackage(file.InputStream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];
                            var rowCount = worksheet.Dimension.Rows;
                            var colCount = 10; // Currencies va Units ovomasligi uchun qo'lda yozildi

                            for (int col = 1; col <= colCount; col++)
                            {
                                dataTable.Columns.Add(worksheet.Cells[1, col].Text);
                            }

                            for (int row = 2; row <= rowCount; row++)
                            {
                                var dataRow = dataTable.NewRow();
                                for (int col = 1; col <= colCount; col++)
                                {
                                    dataRow[col - 1] = worksheet.Cells[row, col].Text;
                                }
                                dataTable.Rows.Add(dataRow);
                            }
                        }

                        if (await CheckForExistenceOfInvoices(dataTable))
                        {
                            if (await CheckForExistenceOfWarehouses(dataTable))
                            {
                                if (await CheckForExistenceOfParts(dataTable))
                                {
                                    ViewBag.DataTable = dataTable;
                                    ViewBag.DataTableModel = JsonConvert.SerializeObject(dataTable);
                                    ViewBag.IsFileUploaded = true;

                                    using (DBTHSNEntities db = new DBTHSNEntities())
                                    {
                                        foreach (DataRow row in dataTable.Rows)
                                        {
                                            whName = row["Warehouse Name"].ToString();
                                            invoiceNo = row["Invoice No."].ToString();
                                            waybillNo = row["Waybill No."].ToString();
                                            partNo = row["Part Number"].ToString();

                                            Warehouse warehouse = db.WAREHOUSES.Where(s => s.WHName.CompareTo(whName) == 0 && s.IsDeleted == false).FirstOrDefault();
                                            PRODUCT part = db.PRODUCTS.Where(p => p.PNo.CompareTo(partNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                                            P_INVOICES invoice = db.P_INVOICES.Where(p => p.InvoiceNo.CompareTo(invoiceNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                                            INCOME income = db.INCOMES.Where(po => po.ReceivingWHID == warehouse.ID && po.InvoiceID == invoice.ID && po.IsDeleted == false).FirstOrDefault();
                                            if (invoice != null && warehouse != null && part != null)
                                            {
                                                P_INVOICE_PRODUCTS invoicePart = db.P_INVOICE_PRODUCTS
                                                    .Where(pop => pop.PartID == part.ID && pop.InvoiceID == invoice.ID)
                                                    .FirstOrDefault();
                                                if (invoicePart != null)
                                                    ViewBag.ExistingRecordsCount = 1;
                                            }
                                            else if (income != null)
                                            {
                                                ViewBag.Message = "kiritilgan invoys bilan OMBOR KIRIM bajarilgan";
                                                return View("UploadWithExcel");
                                            }
                                            else
                                            {
                                                ViewBag.Message = "invoiys, ombor, Part topilmadi";
                                                return View("UploadWithExcel");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var message = "";
                                    foreach (var word in missingParts)
                                    {
                                        message += word + ", ";
                                    }
                                    ViewBag.Message = "Ushbu ombor kirimlar faylida kiritilgan ehtiyot qismlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ehtiyot qismlar bazasiga kiritib keyin qayta urining.";
                                    return View("UploadWithExcel");
                                }
                            }
                            else
                            {
                                var message = "";
                                foreach (var word in missingWarehouses)
                                {
                                    message += word + ", ";
                                }
                                ViewBag.Message = "Ushbu ombor kirimlar faylda kiritilgan omborlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Omborlar bazasiga kiritib keyin qayta urining.";
                                return View("UploadWithExcel");
                            }
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingInvoices)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu ombor kirimlar faylda kiritilgan Invoyslar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Invoyslar bazasiga kiritib keyin qayta urining.";
                            return View("UploadWithExcel");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Message = $"Faylni yuklashda quyidagicha muammo tug'ildi: {ex.Message}";
                        if (ex.InnerException != null)
                        {
                            ViewBag.Message += $" - Ichki xato: {ex.InnerException.Message}";
                        }
                        return View("UploadWithExcel");
                    }
                }
                else
                {
                    ViewBag.Message = "Format noto'g'ri. Faqat .xlsx fayllarni yuklash mumkin.";
                    return View("UploadWithExcel");
                }
            }
            else
            {
                ViewBag.Message = "Fayl bo'm-bo'sh yoki yuklanmadi!";
                return View("UploadWithExcel");
            }
            return View("UploadWithExcel");
        }
        [HttpPost]
        public async Task<ActionResult> Save(string dataTableModel)
        {
            if (!string.IsNullOrEmpty(dataTableModel))
            {
                var tableModel = JsonConvert.DeserializeObject<System.Data.DataTable>(dataTableModel);
                warehouse = GetWarehouseOfMRP(User.Identity.Name);

                try
                {
                    using (DBTHSNEntities db = new DBTHSNEntities())
                    {
                        foreach (DataRow row in tableModel.Rows)
                        {
                            try
                            {
                                //string orderNo = row["OrderNo"].ToString();
                                string invoiceNo = row["Invoice No."].ToString();
                                string warehouseName = row["Warehouse Name"].ToString();
                                string partNo = row["Part Number"].ToString();
                                string waybillNo = row["Waybill No."].ToString();

                                Warehouse excelWarehouse = db.WAREHOUSES
                                    .Where(s => s.WHName.CompareTo(warehouseName) == 0 && s.IsDeleted == false)
                                    .FirstOrDefault();

                                PRODUCT part = db.PRODUCTS
                                    .Where(p => p.PNo.CompareTo(partNo) == 0 && p.IsDeleted == false)
                                    .FirstOrDefault();

                                P_INVOICES invoice = db.P_INVOICES
                                    .Where(p => p.InvoiceNo.CompareTo(invoiceNo) == 0 && p.IsDeleted == false)
                                    .FirstOrDefault();

                                INCOME income = db.INCOMES
                                    .Where(po => po.ReceivingWHID == warehouse.ID && po.IsDeleted == false && po.InvoiceID == invoice.ID)
                                    .FirstOrDefault();

                                F_WAYBILLS waybill = db.F_WAYBILLS
                                    .Where(x => x.WaybillNo.CompareTo(waybillNo) == 0 && x.IsDeleted == false)
                                    .FirstOrDefault();

                                int currencyID = db.CURRENCIES.Where(x => x.Shortname.ToLower() == row["Currency"].ToString().ToLower()).Select(x => x.ID).FirstOrDefault();

                                if (income == null)
                                {
                                    INCOME new_income = new INCOME
                                    {
                                        DocNo = GenerateDocNo(income),
                                        InvoiceID = invoice.ID,
                                        ReceivingWHID = warehouse.ID,
                                        SenderWHID = excelWarehouse.ID,
                                        IssueDateTime = DateTime.Now,
                                        CurrencyID = currencyID,
                                        IsDeleted = false
                                    };

                                    db.INCOMES.Add(new_income);
                                    await db.SaveChangesAsync();

                                    LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{new_income.DocNo} - raqamli kirim hujjatini Excell orqali tizimga kiritdi");

                                    string unitFromFile = row["Unit"].ToString();
                                    UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitFromFile) == 0).FirstOrDefault();

                                    INCOME_PRODUCTS incomePart = db.INCOME_PRODUCTS
                                        .Where(pcp => pcp.IncomeID == new_income.ID && pcp.ProductID == part.ID)
                                        .FirstOrDefault();
                                    if (incomePart == null)
                                    {
                                        INCOME_PRODUCTS new_incomePart = new INCOME_PRODUCTS
                                        {
                                            ProductID = part.ID,
                                            IncomeID = new_income.ID,
                                            PiecePrice = Convert.ToDouble(row["Price"].ToString()),
                                            Amount = Convert.ToDouble(row["Amount"].ToString()),
                                            UnitID = unit.ID
                                        };
                                        new_incomePart.TotalPrice = new_incomePart.Amount * new_incomePart.PiecePrice;
                                        db.INCOME_PRODUCTS.Add(new_incomePart);
                                        await db.SaveChangesAsync();

                                        var logPart = db.PRODUCTS.Find(new_incomePart.ProductID);
                                        LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{new_income.DocNo} - raqamli kirim hujjatiga {logPart.PNo} - ehtiyot qismni Excel orqali biriktirdi");
                                    }
                                }
                                else
                                {
                                    INCOME_PRODUCTS orderPart = db.INCOME_PRODUCTS
                                        .Where(pcp => pcp.IncomeID == income.ID && pcp.ProductID == part.ID)
                                        .FirstOrDefault();
                                    if (orderPart == null)
                                    {
                                        string unitFromFile = row["Unit"].ToString();
                                        UNIT unit = db.UNITS.Where(u => u.ShortName.CompareTo(unitFromFile) == 0).FirstOrDefault();

                                        INCOME_PRODUCTS new_incomePart = new INCOME_PRODUCTS
                                        {
                                            ProductID = part.ID,
                                            IncomeID = income.ID,
                                            PiecePrice = Convert.ToDouble(row["Price"].ToString()),
                                            Amount = Convert.ToDouble(row["Amount"].ToString()),
                                            TotalPrice = Convert.ToDouble(row["Sum"].ToString()),
                                            UnitID = unit.ID,
                                        };

                                        db.INCOME_PRODUCTS.Add(new_incomePart);
                                        await db.SaveChangesAsync();
                                        var logPart = db.PRODUCTS.Find(new_incomePart.ProductID);
                                        LogHelper.LogToDatabase(User.Identity.Name, "Omborga kirim", $"{income.DocNo} - raqamli kirim hujjatiga {logPart.PNo} - ehtiyot qismni Excel orqali biriktirdi");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                }
            }

            return RedirectToAction("Index");
        }
    }
}
