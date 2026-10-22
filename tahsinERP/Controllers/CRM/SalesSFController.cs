using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.Services.Crm.EDocuments;
using Newtonsoft.Json;
using tahsinERP.ViewModels.Sales;

namespace tahsinERP.Controllers
{
    [Permission]
    public class SalesSFController : BaseController
    {
        // Olish so'rovi: SalesSF
        public async Task<ActionResult> Index(string searchTerm, string sfNumber, long? invoiceID, int? customerID, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SALES_SF_HEADERS.AsNoTracking()
                    .Include(sf => sf.SALES_INVOICES)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(sf => sf.SFNumber.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(sfNumber))
                {
                    query = query.Where(sf => sf.SFNumber.Contains(sfNumber));
                }

                if (invoiceID.HasValue)
                {
                    query = query.Where(sf => sf.InvoiceID == invoiceID.Value);
                }

                if (customerID.HasValue)
                {
                    query = query.Where(sf => sf.CustomerID == customerID.Value);
                }

                if (!string.IsNullOrEmpty(statusCode))
                {
                    query = query.Where(sf => sf.StatusCode == statusCode);
                }

                switch (sortOrder)
                {
                    case "sfNumber_desc":
                        query = query.OrderByDescending(sf => sf.SFNumber);
                        break;
                    case "sfNumber":
                        query = query.OrderBy(sf => sf.SFNumber);
                        break;
                    case "date":
                        query = query.OrderBy(sf => sf.SFDate);
                        break;
                    default:
                        query = query.OrderByDescending(sf => sf.SFDate);
                        break;
                }

                // Server tomonida sahifalash (CRM-P05, umumiy blok — D16)
                var sfHeaders = await query.ToPageAsync(page, pageSize, ViewData);

                // Filtr ro'yxatlari: proyeksiya, faqat faol invoyslar (CRM-P04)
                ViewBag.Customers = SalesLookups.Customers(customerID);
                ViewBag.Invoices = new SelectList(await db.SALES_INVOICES.AsNoTracking().Active()
                    .OrderByDescending(i => i.InvoiceDate)
                    .Select(i => new { i.ID, i.InvoiceNo })
                    .ToListAsync(), "ID", "InvoiceNo", invoiceID);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SFNumber = sfNumber;
                ViewBag.InvoiceID = invoiceID;
                ViewBag.CustomerID = customerID;
                ViewBag.StatusCode = statusCode;
                ViewBag.SortOrder = sortOrder;

                return View(sfHeaders);
            }
        }

        // Olish so'rovi: Details
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var sfHeader = db.SALES_SF_HEADERS
                    .Include(sf => sf.SALES_INVOICES)
                    .Include(sf => sf.SALES_SF_LINES)
                    .FirstOrDefault(sf => sf.ID == id);

                if (sfHeader == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == sfHeader.CustomerID);

                if (sfHeader.SALES_INVOICES != null && sfHeader.SALES_INVOICES.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == sfHeader.SALES_INVOICES.OrderID.Value);
                }

                var productIDs = sfHeader.SALES_SF_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);
                ViewBag.EDocument = EDocumentService.Panel(db, sfHeader,
                    RoleHelper.IsPermittedForAction(CurrentUserName, "SalesSF", "SendEDocument", true));

                return View(sfHeader);
            }
        }

        // Olish so'rovi: yaratish dan Invoice
        public ActionResult GenerateFromInvoice(long? invoiceID)
        {
            if (invoiceID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES.Active()
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == invoiceID);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                var existingSF = db.SALES_SF_HEADERS.FirstOrDefault(sf => sf.InvoiceID == invoiceID);
                if (existingSF != null)
                {
                    TempData["InfoMessage"] = L.T("Crm_SfAlreadyForInvoice");
                    return RedirectToAction("Details", new { id = existingSF.ID });
                }

                var sfHeader = new SALES_SF_HEADERS
                {
                    SFDate = DateTime.Now,
                    InvoiceID = invoice.ID,
                    CustomerID = invoice.CustomerID,
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now,
                    IntegrationUID = Guid.NewGuid(),
                    SourceSystem = "TAHSIN_ERP",
                    IntegrationStatusCode = EDocumentStatus.Local,
                    // TODO: haqiqiy INN maydonlari (mijoz/kompaniya sozlamalari) — hozircha DUNS
                    CustomerINN = db.CUSTOMERS.Where(c => c.ID == invoice.CustomerID).Select(c => c.DUNS).FirstOrDefault(),
                    SupplierINN = ""
                };

                PopulateGenerateLookups(db, invoice);
                return View(sfHeader);
            }
        }

        /// <summary>
        /// SF yaratish. Qator miqdori, narxi va QQS invoys qatoridan olinadi va summalar serverda qayta
        /// hisoblanadi (CRM-S03: avval brauzer yuborgan LineAmount/VATAmount/TotalWithVAT saqlanardi).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateFromInvoice(SALES_SF_HEADERS sfHeader, List<SalesSFLineViewModel> lines)
        {
            if (lines == null || !lines.Any())
            {
                ModelState.AddModelError("", L.T("Crm_SfNoLines"));
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var invoice = db.SALES_INVOICES.Active()
                            .Include(i => i.SALES_INVOICE_LINES)
                            .FirstOrDefault(i => i.ID == sfHeader.InvoiceID);

                        if (invoice == null)
                        {
                            ModelState.AddModelError("", L.T("Crm_SfInvoiceNotFound"));
                        }
                        else if (db.SALES_SF_HEADERS.Any(sf => sf.InvoiceID == sfHeader.InvoiceID))
                        {
                            ModelState.AddModelError("", L.T("Crm_SfAlreadyForInvoice"));
                        }
                        else
                        {
                            sfHeader.SFDate = DateTime.Now;
                            sfHeader.CustomerID = invoice.CustomerID;
                            sfHeader.StatusCode = "DRAFT";
                            sfHeader.CreatedDT = DateTime.Now;
                            sfHeader.CreatedByUserID = RoleHelper.GetUserId(CurrentUserName);
                            sfHeader.IntegrationUID = Guid.NewGuid();
                            sfHeader.SourceSystem = "TAHSIN_ERP";
                            // NOT NULL ustun (bazada DEFAULT 'LOCAL', lekin EF null yuborib validatsiyada yiqilardi)
                            sfHeader.IntegrationStatusCode = EDocumentStatus.Local;

                            var built = BuildLines(invoice, lines);
                            if (built == null)
                            {
                                ModelState.AddModelError("", L.T("Crm_SfLinesMismatch"));
                            }
                            else
                            {
                                foreach (var line in built)
                                    sfHeader.SALES_SF_LINES.Add(line);

                                using (var tx = db.Database.BeginTransaction())
                                {
                                    if (string.IsNullOrWhiteSpace(sfHeader.SFNumber))
                                    {
                                        sfHeader.SFNumber = DocumentNumberService.Next(db, "SALES_SF_HEADERS", "SFNumber", $"SF-{DateTime.Now:yyyy}-", 4);
                                    }
                                    db.SALES_SF_HEADERS.Add(sfHeader);
                                    db.SaveChanges();
                                    tx.Commit();
                                }

                                LogHelper.LogToDatabase(CurrentUserName, "SalesSFController", $"{sfHeader.SFNumber} - SF yaratdi (Invoys: {invoice.InvoiceNo})");

                                return RedirectToAction("Details", new { id = sfHeader.ID });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", L.T("Crm_ErrorPrefix", CrmErrors.Describe(ex)));
                    }
                }
            }

            // Barcha xato yo'llarida ro'yxatlar qayta quriladi (CRM-B15)
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = sfHeader.InvoiceID.HasValue
                    ? db.SALES_INVOICES.Include(i => i.SALES_INVOICE_LINES).FirstOrDefault(i => i.ID == sfHeader.InvoiceID)
                    : null;
                PopulateGenerateLookups(db, invoice);
            }

            return View(sfHeader);
        }

        // Olish so'rovi: Edit
        public ActionResult Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var sfHeader = db.SALES_SF_HEADERS
                    .Include(sf => sf.SALES_SF_LINES)
                    .FirstOrDefault(sf => sf.ID == id);

                if (sfHeader == null)
                {
                    return HttpNotFound();
                }

                if (sfHeader.StatusCode == "SENT" || sfHeader.StatusCode == "ACCEPTED")
                {
                    TempData["ErrorMessage"] = L.T("Crm_SfCannotEditSent");
                    return RedirectToAction("Details", new { id = id });
                }

                return View(sfHeader);
            }
        }

        // POST so'rovi: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SALES_SF_HEADERS sfHeader, List<SalesSFLineViewModel> lines)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var existingSF = db.SALES_SF_HEADERS.Find(sfHeader.ID);
                    if (existingSF == null)
                    {
                        return HttpNotFound();
                    }

                    if (existingSF.StatusCode == "SENT" || existingSF.StatusCode == "ACCEPTED")
                    {
                        ModelState.AddModelError("", L.T("Crm_SfCannotEditSent"));
                        return View(sfHeader);
                    }

                    try
                    {
                        existingSF.SFNumber = string.IsNullOrWhiteSpace(sfHeader.SFNumber) ? existingSF.SFNumber : sfHeader.SFNumber;
                        existingSF.SFDate = sfHeader.SFDate;
                        existingSF.SupplierINN = sfHeader.SupplierINN;
                        existingSF.CustomerINN = sfHeader.CustomerINN;
                        existingSF.StatusCode = sfHeader.StatusCode;

                        if (lines != null && lines.Any())
                        {
                            var invoice = db.SALES_INVOICES
                                .Include(i => i.SALES_INVOICE_LINES)
                                .FirstOrDefault(i => i.ID == existingSF.InvoiceID);
                            var built = invoice != null ? BuildLines(invoice, lines) : null;
                            if (built == null)
                            {
                                ModelState.AddModelError("", L.T("Crm_SfLinesMismatch"));
                                return View(sfHeader);
                            }

                            db.SALES_SF_LINES.RemoveRange(db.SALES_SF_LINES.Where(l => l.SFHeaderID == sfHeader.ID).ToList());
                            foreach (var line in built)
                            {
                                line.SFHeaderID = sfHeader.ID;
                                db.SALES_SF_LINES.Add(line);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(CurrentUserName, "SalesSFController", $"{existingSF.SFNumber} - SF ni tahrirladi");

                        return RedirectToAction("Details", new { id = sfHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", L.T("Crm_ErrorPrefix", CrmErrors.Describe(ex)));
                    }
                }
            }

            return View(sfHeader);
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        /// <summary>
        /// Har SF qatori invoys qatoriga (InvoiceLineID) bog'lanadi; miqdor/narx/QQS invoys qatoridan,
        /// summalar qayta hisoblanadi. Mos kelmasa null.
        /// </summary>
        private static List<SALES_SF_LINES> BuildLines(SALES_INVOICES invoice, IEnumerable<SalesSFLineViewModel> lines)
        {
            var invoiceLines = invoice.SALES_INVOICE_LINES.ToDictionary(l => l.ID);
            var result = new List<SALES_SF_LINES>();
            int lineNo = 1;
            foreach (var posted in lines)
            {
                SALES_INVOICE_LINES src;
                if (!posted.InvoiceLineID.HasValue || !invoiceLines.TryGetValue(posted.InvoiceLineID.Value, out src))
                    return null;

                var lineAmount = src.Qty * src.UnitPrice;
                var vatAmount = lineAmount * src.VATRate / 100m;
                result.Add(new SALES_SF_LINES
                {
                    LineNo = lineNo++,
                    InvoiceLineID = src.ID,
                    ProductID = src.ProductID,
                    Description = string.IsNullOrWhiteSpace(posted.Description) ? src.Description : posted.Description,
                    Qty = src.Qty,
                    UnitPrice = src.UnitPrice,
                    LineAmount = lineAmount,
                    VATRate = src.VATRate,
                    VATAmount = vatAmount,
                    TotalWithVAT = lineAmount + vatAmount
                });
            }
            return result;
        }

        private void PopulateGenerateLookups(DBTHSNEntities db, SALES_INVOICES invoice)
        {
            ViewBag.Invoice = invoice;
            ViewBag.InvoiceLines = invoice?.SALES_INVOICE_LINES.OrderBy(l => l.LineNo).ToList() ?? new List<SALES_INVOICE_LINES>();
            var productIDs = invoice?.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList() ?? new List<int>();
            ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);
        }
        // ───────────────────────────── elektron hujjat (EDO) ─────────────────────────────

        /// <summary>Provayderga yuboriladigan hujjat ma'lumotlari va kamchiliklar (integratsiyani sozlash uchun).</summary>
        [HttpGet]
        public ActionResult EDocumentPayload(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var doc = EDocumentService.BuildInvoice(db, id);
                if (doc == null)
                {
                    return HttpNotFound();
                }

                var json = JsonConvert.SerializeObject(new
                {
                    Provider = EDocumentProviders.Active().Code,
                    EDocumentService.SchemaVersion,
                    Errors = EDocumentService.Validate(doc),
                    Document = doc
                }, Formatting.Indented);
                return Content(json, "application/json");
            }
        }

        /// <summary>SF'ni faol EDO provayderiga (Didox / Faktura.uz) yuborish. Ichki rejimda o'chiq.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendEDocument(long id)
        {
            var result = await EDocumentService.SendInvoiceAsync(id, CurrentUserName);
            if (result.Success)
                TempData["EDocSuccess"] = L.T("Crm_SfSent", EDocumentStatus.Title(result.Status)) + (string.IsNullOrEmpty(result.ExternalId) ? "" : " (ID: " + result.ExternalId + ")");
            else
                TempData["EDocError"] = result.Message;
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RefreshEDocument(long id)
        {
            var result = await EDocumentService.RefreshAsync(EDocumentKind.Invoice, id);
            if (result.Success)
                TempData["EDocSuccess"] = L.T("Crm_EdocStatusLabel", EDocumentStatus.Title(result.Status));
            else
                TempData["EDocError"] = result.Message;
            return RedirectToAction("Details", new { id });
        }
    }
}
