using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class ProformaInvoiceController : Controller
    {
        // GET: ProformaInvoice
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.P_PROFORMA_INVOICES
                    .Where(x => x.IsDeleted == false)
                    .Include(x => x.P_INVOICES)
                    .Include(x => x.SUPPLIER)
                    .Include(x => x.CURRENCy)
                    .ToListAsync();

                return View(list);
            }
        }
        public async Task<ActionResult> Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Supplier = new SelectList(await db.SUPPLIERS.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "Name");
                ViewBag.PInvoices = new SelectList(await db.P_INVOICES.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "InvoiceNo");
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                //ViewBag.PInvoices = new SelectList(Enumerable.Empty<SelectListItem>());
            }
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ProformaInvoiceViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    P_PROFORMA_INVOICES invoice = new P_PROFORMA_INVOICES()
                    {
                        PrInvoiceNo = model.PrInvoiceNo,
                        InvoiceID = model.InvoiceID,
                        SupplierID = model.SupplierID,
                        Amount = model.Amount,
                        CurrencyID = model.CurrencyID,
                        InvoiceDate = model.InvoiceDate,
                        CompanyID = 1,
                        IsDeleted = false
                    };

                    db.P_PROFORMA_INVOICES.Add(invoice);
                    await db.SaveChangesAsync();
                    LogHelper.LogToDatabase(User.Identity.Name, "ProformaInvoiceController", $"{model.ID} ID ga ega PProformaInvoiceni yaratdi");
                    
                    // proformanvoice fayl upload, hozircha models ichida yoq shunga comment ga olib qo'yildi
                   /* if (model.File != null)
                    {
                        if (model.File.ContentLength > 0)
                        {
                            if (model.File.InputStream.Length < 5242880)
                            {
                                if (Path.GetExtension(model.File.FileName).ToLower() == ".pdf")
                                {
                                    P_PROFORMA_INVOICE_DOCS proformaDoc = new P_PROFORMA_INVOICE_DOCS();
                                    byte[] avatar = new byte[model.File.InputStream.Length];
                                    model.File.InputStream.Read(avatar, 0, avatar.Length);

                                    proformaDoc.ProformaID = invoice.ID;
                                    proformaDoc.Doc = avatar;

                                    db.P_PROFORMA_INVOICE_DOCS.Add(proformaDoc);
                                    db.SaveChanges();

                                    LogHelper.LogToDatabase(User.Identity.Name, "ProformaInvoiceController", $"{invoice.PrInvoiceNo} - uchun ProformaInvoiceDocni yaratdi");
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
                    }*/

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(ex.Message, "Ma'lumotlarni kiritishda xatolik yuz berdi.");
                    ViewBag.Supplier = new SelectList(await db.SUPPLIERS.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "Name");
                    ViewBag.PInvoices = new SelectList(await db.P_INVOICES.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "InvoiceNo");
                    ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                    return View(model);
                }
            }
        }
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var  invoice = await db.P_PROFORMA_INVOICES
                    .Include(x => x.P_INVOICES)
                    .Include(x => x.SUPPLIER)
                    .Include(x => x.CURRENCy)
                    .Where(x => x.ID == id)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                    return HttpNotFound();
 
                return View(invoice);
            }
        }
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = await db.P_PROFORMA_INVOICES
                    .Include(x => x.P_INVOICES)
                    .Include(x => x.SUPPLIER)
                    .Include(x => x.CURRENCy)
                    .Where(x => x.ID == id)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                    return HttpNotFound();

                return View(invoice);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection gfs)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var invoiceToDelete = await db.P_PROFORMA_INVOICES.FindAsync(ID);
                    if (invoiceToDelete != null)
                    {
                        invoiceToDelete.IsDeleted = true;
                        try
                        {
                            await db.SaveChangesAsync();

                            LogHelper.LogToDatabase(User.Identity.Name, "ProformaInvoiceController", $"{ID} ID ga ega PProformaInvoiceni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday invoice topilmadi.");
                    }
                }
            }
            return View();
        }
        public ActionResult GetInvoicesBySupplier(int supplierID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoices = db.P_INVOICES
                    .Where(x => x.IsDeleted == false && x.SupplierID == supplierID)
                    .Select(x => new { x.ID, x.InvoiceNo })
                    .ToList();

                return Json(invoices.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.InvoiceNo }), JsonRequestBehavior.AllowGet);
            }
        }
        public async Task<ActionResult> Edit(int? ID)
        {
            if (ID == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = await db.P_PROFORMA_INVOICES
                    .Include(x => x.P_INVOICES)
                    .Where(x => x.ID == ID && x.IsDeleted == false)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
                    //return HttpNotFound();

                ViewBag.Supplier = new SelectList(await db.SUPPLIERS.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "Name", invoice.SupplierID);
                //ViewBag.PInvoices = new SelectList(await db.P_INVOICES.Where(x => x.IsDeleted == false).ToListAsync(), "ID", "InvoiceNo", invoice.InvoiceID);
                ViewBag.PInvoices = new SelectList(Enumerable.Empty<SelectListItem>()); 
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(invoice);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(P_PROFORMA_INVOICES invoice)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var  invoiceToUpdate = await db.P_PROFORMA_INVOICES.FindAsync(invoice.ID);
                    if (invoiceToUpdate != null)    
                    {
                        invoiceToUpdate.PrInvoiceNo = invoice.PrInvoiceNo;
                        invoiceToUpdate.SupplierID = invoice.SupplierID;
                        invoiceToUpdate.InvoiceID = invoice.InvoiceID;
                        invoiceToUpdate.InvoiceDate = invoice.InvoiceDate;
                        invoiceToUpdate.CurrencyID = invoice.CurrencyID;
                        invoiceToUpdate.Amount = invoice.Amount;

                        db.Entry(invoiceToUpdate).State = EntityState.Modified;

                        try
                        {
                            db.SaveChanges();

                            LogHelper.LogToDatabase(User.Identity.Name, "ProformaInvoiceController", $"{invoiceToUpdate.ID} ID ga ega PProformaInvoiceni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (DbUpdateException ex)
                        {
                            if (ex.InnerException?.InnerException is SqlException sqlEx)
                            {
                                if (sqlEx.Number == 547) // Foreign key constraint violation
                                {
                                    ModelState.AddModelError("", "The OrderID does not exist in the P_ORDERS table.");
                                }
                                else
                                {
                                    ModelState.AddModelError("", $"Database update error: {sqlEx.Message}");
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", $"Unexpected error: {ex.Message}");
                            }
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invoice not found.");
                    }
                }

                // Re-populate dropdown lists in case of an error
                ViewBag.Supplier = new SelectList(await db.SUPPLIERS.ToListAsync(), "ID", "Name", invoice.SupplierID);
                ViewBag.PInvoices = new SelectList(await db.P_INVOICES.ToListAsync(), "ID", "InvoiceNo", invoice.InvoiceID);
                ViewBag.currencies = new SelectList(db.CURRENCIES.ToList(), "ID", "Shortname");

                return View(invoice);
            }
        }
        public ActionResult GetContractsBySupplier(int supplierID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoices = db.P_INVOICES
                    .Where(x => x.IsDeleted == false && x.SupplierID == supplierID)
                    .Select(x => new { x.ID, x.InvoiceNo })
                    .ToList();

                return Json(invoices.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.InvoiceNo }), JsonRequestBehavior.AllowGet);
            }
        }
    }
}