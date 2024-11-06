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
using System.Web.UI.WebControls.WebParts;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class WhIncomeController : Controller
    {
        private string[] sources = ConfigurationManager.AppSettings["partTypes"].Split(',');
        PART_WRHS warehouse = null;

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
                    List<PART_WRHS_INCOMES> list = new List<PART_WRHS_INCOMES>();
                    return View(list);
                }
                else
                {
                    if (!string.IsNullOrEmpty(type))
                    {
                        if (supplierID.HasValue)
                        {
                            List<PART_WRHS_INCOMES> list = db.PART_WRHS_INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.F_WAYBILLS)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SUPPLIER.Type.CompareTo(type) == 0 && pi.P_INVOICES.SupplierID == supplierID && pi.PART_WRHS.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.Type.CompareTo(type) == 0 && s.IsDeleted == false).ToList(), "ID", "Name", supplierID);
                            return View(list);
                        }
                        else
                        {
                            List<PART_WRHS_INCOMES> list = db.PART_WRHS_INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.F_WAYBILLS)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SUPPLIER.Type.CompareTo(type) == 0 && pi.PART_WRHS.ID == warehouse.ID)
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
                            List<PART_WRHS_INCOMES> list = db.PART_WRHS_INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.F_WAYBILLS)
                                .Where(pi => pi.IsDeleted == false && pi.P_INVOICES.SupplierID == supplierID && pi.PART_WRHS.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.IsDeleted == false).ToList(), "ID", "Name", supplierID);
                            return View(list);
                        }
                        else
                        {
                            List<PART_WRHS_INCOMES> list = db.PART_WRHS_INCOMES
                                .Include(pr => pr.P_INVOICES)
                                .Include(pr => pr.F_WAYBILLS)
                                .Where(pi => pi.IsDeleted == false && pi.PART_WRHS.ID == warehouse.ID)
                                .ToList();
                            ViewBag.SourceList = new SelectList(sources, type);
                            ViewBag.SupplierList = new SelectList(db.SUPPLIERS.Where(s => s.IsDeleted == false).ToList(), "ID", "Name");
                            return View(list);
                        }
                    }
                }

            }
        }
        private PART_WRHS GetWarehouseOfMRP(string email)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                USER currentUser = db.USERS.Where(u => u.Email.CompareTo(email) == 0 && u.IsDeleted == false && u.IsActive == true).FirstOrDefault();
                PART_WRHS his_warehouse = db.PART_WRHS.Where(wrhs => wrhs.MRP == currentUser.ID).FirstOrDefault();

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
                    return Json(new { success = false, message = "Invoice not found." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public JsonResult GetPriceAndMOQ(int partID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoicePart = db.P_INVOICE_PARTS
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
                    return Json(new { success = false, message = "Part not found in contract." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        // Bu method tanlangan shartnomaga tegishli qismlar ro'yxatini qaytaradi
        public ActionResult GetPartList(int invoiceID)
        {
            using (var db = new DBTHSNEntities())
            {
                var partList = db.P_INVOICE_PARTS
                    .Where(cp => cp.InvoiceID == invoiceID)
                    .Select(cp => new
                    {
                        cp.PartID,
                        cp.PART.PNo
                    })
                    .ToList();

                return Json(new { success = true, data = partList }, JsonRequestBehavior.AllowGet);
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
                PART_WRHS_INCOMES income = db.PART_WRHS_INCOMES.OrderByDescending(w => w.IssueDateTime).FirstOrDefault();
                warehouse = GetWarehouseOfMRP(User.Identity.Name);

                if (warehouse is null)
                    model.WHName = "Siz uchun ombor biriktirilmagan.";
                else
                    model.WHName = warehouse.WHName;

                if (income != null && income.DocNo.Contains("_"))
                {
                    // Oy va hujjat raqamini ajratamiz
                    string[] docParts = income.DocNo.Split('_');
                    int incomeMonth = int.Parse(docParts[0]);
                    int currentMonth = DateTime.Now.Month;

                    if (incomeMonth == currentMonth)
                    {
                        // Agar hujjat shu oyga tegishli bo'lsa, raqamni oshiramiz
                        int doc_No_number = int.Parse(docParts[1]);
                        doc_No_number++;
                        model.DocNo = $"{currentMonth}_{doc_No_number}";
                    }
                    else
                    {
                        // Agar hujjat boshqa oydan bo'lsa, raqamni 1 dan boshlaymiz
                        model.DocNo = $"{currentMonth}_1";
                    }
                }
                else
                {
                    // Agar hujjat mavjud bo'lmasa, shu oydan boshlaymiz
                    model.DocNo = $"{DateTime.Now.Month}_1";
                }


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
                var invoiceParts = db.P_INVOICE_PARTS.Where(x => x.InvoiceID == model.InvoiceID).ToList();

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
                PART_WRHS_INCOMES newIncome = new PART_WRHS_INCOMES
                {
                    DocNo = model.DocNo,
                    WHID = warehouse.ID,
                    InvoiceID = model.InvoiceID,
                    Amount = model.Amount,
                    CurrencyID = model.CurrencyID,
                    IsDeleted = false,
                    Description = model.Description,
                    IssueDateTime = DateTime.Now,
                    SenderWHID = null,
                    RecieveStatus = true
                };
                db.PART_WRHS_INCOMES.Add(newIncome);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{newIncome.DocNo} - PartWrhsIncomeni yaratdi");

                // Yangi yozuvning IncomeID sini olish
                int newIncomeID = newIncome.ID;
                DateTime toDate = DateTime.Now.Date;
                // Parts ni saqlash
                foreach (var part in model.Parts)
                {
                    PART_WRHS_INCOME_PARTS newPart = new PART_WRHS_INCOME_PARTS
                    {
                        IncomeID = newIncomeID, // part.IncomeID emas, yangi yaratilgan IncomeID ishlatiladi
                        PartID = part.PartID,
                        UnitID = part.UnitID,
                        Amount = part.Amount,
                        PiecePrice = (double)part.PiecePrice,
                        //TotalPrice = part.TotalPrice,
                        Comment = part.Comment
                    };
                    PART_STOCKS existStock = db.PART_STOCKS.Where(s => s.WHID == newIncome.WHID && s.PartID == part.PartID).FirstOrDefault();
                    string partNo;
                    if (existStock == null)
                    {
                        PART_STOCKS newPartStock = new PART_STOCKS
                        {
                            WHID = (int)newIncome.WHID,
                            PartID = part.PartID,
                            UnitID = part.UnitID,
                            Amount = part.Amount
                        };
                        db.PART_STOCKS.Add(newPartStock);
                         partNo = db.PARTS.Where(x => x.ID == newPartStock.PartID).FirstOrDefault().PNo;
                        LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{partNo} - PartStockni yaratdi");
                    }
                    else
                    {
                        existStock.Amount = existStock.Amount + part.Amount;
                        partNo = db.PARTS.Where(x => x.ID == existStock.PartID).FirstOrDefault().PNo;
                    }

                    db.PART_WRHS_INCOME_PARTS.Add(newPart);

                    LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{partNo} - PartWrhsIncomePartni yaratdi");
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }
        }
        public JsonResult GetPartsByInvoice(int invoiceId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var parts = db.P_INVOICE_PARTS
                              .Where(p => p.InvoiceID == invoiceId)
                              .Select(p => new { p.ID, p.PART.PNo }) // Assuming PNo is the part number
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
                var wrhsIncome = db1.PART_WRHS_INCOMES
                                  .Include(p => p.P_INVOICES)
                                  .Include(p => p.F_WAYBILLS)
                                  .Include(p => p.CURRENCy)
                                  .FirstOrDefault(p => p.ID == ID);

                if (wrhsIncome == null)
                {
                    return HttpNotFound();
                }

                var partList = db1.PART_WRHS_INCOME_PARTS
                                  .Include(pc => pc.PART)
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
                var whrsIncome = db1.PART_WRHS_INCOMES
                    .Include(i => i.P_INVOICES)
                    .Include(i => i.F_WAYBILLS)
                    .Include(p => p.CURRENCy)
                    .FirstOrDefault(p => p.ID == ID);

                if (whrsIncome == null)
                {
                    return HttpNotFound();
                }

                var model = new WrhsIncomeViewModel
                {
                    ID = whrsIncome.ID,
                    DocNo = whrsIncome.DocNo,
                    InvoiceID = (int)whrsIncome.InvoiceID,
                    Amount = (double)whrsIncome.Amount,
                    CurrencyID = (int)whrsIncome.CurrencyID,
                    Description = whrsIncome.Description,
                    Parts = whrsIncome.PART_WRHS_INCOME_PARTS.Select(p => new ViewModels.WhrsIncomePart
                    {
                        ID = p.ID,
                        PartID = p.PartID,
                        Part = p.PART,
                        UnitID = p.UnitID,
                        Amount = p.Amount,
                        PiecePrice = (double)p.PiecePrice,
                        Comment = p.Comment
                    }).ToList()
                };

                // suppliers = new SelectList(db1.SUPPLIERS.ToList(), "ID", "Name", whrsIncome.SupplierID);

                //ViewBag.PartList = new SelectList(db1.PART_WRHS_INCOME_PARTS.Include(p => p.PART).ToList(),"ID","PName");
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
                    PART_WRHS_INCOMES wrhsIncomeToUpdate = db.PART_WRHS_INCOMES.Find(whrsIncome.ID);
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
                        foreach (var product in whrsIncome.Parts)
                        {
                            var existingPart = db.PART_WRHS_INCOME_PARTS.Find(product.ID);
                            if (existingPart != null)
                            {
                                existingPart.PiecePrice = product.PiecePrice;
                                existingPart.UnitID = product.UnitID;
                                existingPart.Amount = product.Amount;
                                existingPart.TotalPrice = product.Amount * product.PiecePrice;

                                summ += (double)(existingPart.TotalPrice);
                                db.Entry(existingPart).State = EntityState.Modified;
                                try
                                {
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{existingPart.PartID} - li WhIncomePartni tahrirladi");
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

                            LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{wrhsIncomeToUpdate.DocNo} - PartWrhsIncomeni tahrirladi");

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
                var whIncomePart = db.PART_WRHS_INCOME_PARTS
                                    .Include(p => p.PART_WRHS_INCOMES)
                                    .Include(p => p.PART)
                                    .FirstOrDefault(p => p.ID == ID);
                if (whIncomePart == null)
                {
                    return HttpNotFound();
                }
                var allParts = db.PARTS
                                .Include(p => p.PART_WRHS_INCOME_PARTS)
                                .Include(p => p.UNIT)
                                .Select(p => new SelectListItem
                                {
                                    Value = p.ID.ToString(),
                                    Text = p.PNo
                                }).ToList();

                ViewBag.PartList = allParts;
                ViewBag.units = new SelectList( db.UNITS.ToList(), "ID", "UnitName");

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
        public ActionResult EditPart(PART_WRHS_INCOME_PARTS whIncomePart)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    PART_WRHS_INCOME_PARTS whIncomePartToUpdate = db.PART_WRHS_INCOME_PARTS.Find(whIncomePart.ID);
                    if (whIncomePartToUpdate != null)
                    {
                        whIncomePartToUpdate.PartID = whIncomePart.PartID;
                        whIncomePartToUpdate.Amount = whIncomePart.Amount;
                        whIncomePartToUpdate.UnitID = whIncomePart.UnitID;
                        whIncomePartToUpdate.PiecePrice = whIncomePart.PiecePrice;
                        whIncomePartToUpdate.Comment = whIncomePart.Comment;
                        //whIncomePartToUpdate.Amount = whIncomePart.Quantity * whIncomePart.Price; SQL o'zi chiqarib beradi

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{whIncomePartToUpdate.PART.PNo} - PartWrhsIncomePartni tahrirladi");

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
                var whrsIncome = db.PART_WRHS_INCOMES.Find(Id);
                if (whrsIncome == null)
                {
                    return HttpNotFound();
                }
                else
                    ViewBag.partList = db.PART_WRHS_INCOME_PARTS
                        .Include(pc => pc.PART)
                        .Include(pc => pc.UNIT)
                        .Where(pc => pc.IncomeID == whrsIncome.ID).ToList();

                db.Entry(whrsIncome).Reference(i => i.P_INVOICES).Load();
                db.Entry(whrsIncome).Reference(i => i.F_WAYBILLS).Load();
                db.Entry(whrsIncome).Reference(i => i.CURRENCy).Load();

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
                    PART_WRHS_INCOMES whIncomeToDelete = db.PART_WRHS_INCOMES.Find(ID);
                    if (whIncomeToDelete != null)
                    {
                        whIncomeToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(whIncomeToDelete).State = System.Data.Entity.EntityState.Modified;
                            /*var contractParts = db.PART_WRHS_INCOME_PARTS.Where(pc => pc.IncomeID == whIncomeToDelete.ID).ToList();
                            foreach (var whIncomePart in contractParts)
                            {
                                db.PART_WRHS_INCOME_PARTS.Remove(whIncomePart);
                            }*/
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{whIncomeToDelete.DocNo} - partWrhsIncomeni o'chirdi");

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
                PART_WRHS_INCOME_PARTS contractPartToDelete = db.PART_WRHS_INCOME_PARTS.Find(id);
                if (ModelState.IsValid)
                {
                    if (contractPartToDelete != null)
                    {
                        try
                        {
                            db.PART_WRHS_INCOME_PARTS.Remove(contractPartToDelete);
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "WhIncomeController", $"{contractPartToDelete.PART.PNo} - PartWrhsIncomePartni o'chirdi");

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
        private List<string> missingContracts = new List<string>();
        private List<string> missingSuppliers = new List<string>();
        private List<string> missingParts = new List<string>();
        private string whName, invoiceNo, docNo, partNo, waybillNo = "";

        private async Task<bool> CheckForExistenceOfContracts(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string contractNo = row["ContractNo"].ToString();

                    bool contractExists = await db.P_CONTRACTS.AnyAsync(p => p.ContractNo.CompareTo(contractNo) == 0);
                    if (!contractExists)
                    {
                        if (!missingContracts.Contains(contractNo))
                        {
                            missingContracts.Add(contractNo);
                        }
                        return false;  // Return false immediately if any contract is missing
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

                    bool partExists = await db.PARTS.AnyAsync(p => p.PNo.CompareTo(partPNo) == 0);
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


        private async Task<bool> CheckForExistenceOfSuppliers(System.Data.DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string supplierName = row["Supplier Name"].ToString();

                    bool supplierExists = await db.SUPPLIERS.AnyAsync(s => s.Name.CompareTo(supplierName) == 0);
                    if (!supplierExists)
                    {
                        if (!missingSuppliers.Contains(supplierName))
                        {
                            missingSuppliers.Add(supplierName);
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
                            var colCount = worksheet.Dimension.Columns;

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
                        
                        if (await CheckForExistenceOfContracts(dataTable))
                        {
                            if (await CheckForExistenceOfSuppliers(dataTable))
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
                                            whName = row["OrderNo"].ToString();
                                            invoiceNo = row["Invoice No."].ToString();
                                            waybillNo = row["Waybill No."].ToString();
                                            partNo = row["Part Number"].ToString();

                                            PART_WRHS warehouse = db.PART_WRHS.Where(s => s.WHName.CompareTo(whName) == 0 && s.IsDeleted == false).FirstOrDefault();
                                            PART part = db.PARTS.Where(p => p.PNo.CompareTo(partNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                                            P_CONTRACTS contract = db.P_CONTRACTS.Where(p => p.ContractNo.CompareTo(invoiceNo) == 0 && p.IsDeleted == false).FirstOrDefault();
                                            P_ORDERS order = db.P_ORDERS.Where(po => po.OrderNo.CompareTo(invoiceNo) == 0 && po.SupplierID == warehouse.ID && po.ContractID == contract.ID && po.IsDeleted == false).FirstOrDefault();
                                            if (order != null && warehouse != null && part != null)
                                            {
                                                P_ORDER_PARTS orderPart = db.P_ORDER_PARTS
                                                    .Where(pop => pop.PartID == part.ID && pop.OrderID == order.ID)
                                                    .FirstOrDefault();
                                                if (orderPart != null)
                                                    ViewBag.ExistingRecordsCount = 1;
                                            }
                                            else
                                            {
                                                ViewBag.Message = "Order/Supplier/Part topilmadi";
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
                                    ViewBag.Message = "Ushbu buyurtmalar faylida kiritilgan ehtiyot qismlar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ehtiyot qismlar bazasiga kiritib keyin qayta urining.";
                                    return View("UploadWithExcel");
                                }
                            }
                            else
                            {
                                var message = "";
                                foreach (var word in missingSuppliers)
                                {
                                    message += word + ", ";
                                }
                                ViewBag.Message = "Ushbu buyurtmalar faylda kiritilgan ta'minotchilar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Ta'minotchilar bazasiga kiritib keyin qayta urining.";
                                return View("UploadWithExcel");
                            }
                        }
                        else
                        {
                            var message = "";
                            foreach (var word in missingContracts)
                            {
                                message += word + ", ";
                            }
                            ViewBag.Message = "Ushbu buyurtmalar faylda kiritilgan shartnomalar: " + message + " tizim bazasida mavjud emas. Qaytadan tekshiring, avval Shartnomalar bazasiga kiritib keyin qayta urining.";
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
    }
}
