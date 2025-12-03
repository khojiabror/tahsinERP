using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class SalesSFController : Controller
    {
        // GET: SalesSF
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string sfNumber, long? invoiceID, int? customerID, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SALES_SF_HEADERS
                    .Include(sf => sf.SALES_INVOICES)
                    .AsQueryable();

                // Filters
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

                // Sorting
                switch (sortOrder)
                {
                    case "sfNumber_desc":
                        query = query.OrderByDescending(sf => sf.SFNumber);
                        break;
                    case "sfNumber":
                        query = query.OrderBy(sf => sf.SFNumber);
                        break;
                    case "date_desc":
                        query = query.OrderByDescending(sf => sf.SFDate);
                        break;
                    case "date":
                        query = query.OrderBy(sf => sf.SFDate);
                        break;
                    default:
                        query = query.OrderByDescending(sf => sf.SFDate);
                        break;
                }

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var sfHeaders = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // ViewBag for filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
                ViewBag.Invoices = new SelectList(await db.SALES_INVOICES.ToListAsync(), "ID", "InvoiceNo", invoiceID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SFNumber = sfNumber;
                ViewBag.InvoiceID = invoiceID;
                ViewBag.CustomerID = customerID;
                ViewBag.StatusCode = statusCode;
                ViewBag.SortOrder = sortOrder;

                return View(sfHeaders);
            }
        }

        // GET: Details
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

                // Load customer
                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == sfHeader.CustomerID);

                // Load related order if invoice has one
                if (sfHeader.SALES_INVOICES != null && sfHeader.SALES_INVOICES.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == sfHeader.SALES_INVOICES.OrderID.Value);
                }

                // Load products for lines
                var productIDs = sfHeader.SALES_SF_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                return View(sfHeader);
            }
        }

        // GET: Generate from Invoice
        public ActionResult GenerateFromInvoice(long? invoiceID)
        {
            if (invoiceID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == invoiceID);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Check if SF already exists for this invoice
                var existingSF = db.SALES_SF_HEADERS.FirstOrDefault(sf => sf.InvoiceID == invoiceID);
                if (existingSF != null)
                {
                    TempData["InfoMessage"] = "Bu invoys uchun SF allaqachon yaratilgan";
                    return RedirectToAction("Details", new { id = existingSF.ID });
                }

                // Create SF header from invoice
                var sfHeader = new SALES_SF_HEADERS
                {
                    SFDate = DateTime.Now,
                    InvoiceID = invoice.ID,
                    CustomerID = invoice.CustomerID,
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now,
                    IntegrationUID = Guid.NewGuid(),
                    SourceSystem = "TAHSIN_ERP"
                };

                // Load customer for INN
                var customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);
                if (customer != null)
                {
                    // TODO: Get customer INN from customer record or settings
                    sfHeader.CustomerINN = customer.DUNS; // Using DUNS as placeholder
                }

                // TODO: Get supplier INN from company settings
                sfHeader.SupplierINN = ""; // To be filled from company settings

                ViewBag.Invoice = invoice;
                ViewBag.InvoiceLines = invoice.SALES_INVOICE_LINES.OrderBy(l => l.LineNo).ToList();
                
                // Load products
                var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                return View(sfHeader);
            }
        }

        // POST: Generate from Invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateFromInvoice(SALES_SF_HEADERS sfHeader, List<SalesSFLineViewModel> lines)
        {
            if (ModelState.IsValid && lines != null && lines.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Check if SF already exists
                        var existingSF = db.SALES_SF_HEADERS.FirstOrDefault(sf => sf.InvoiceID == sfHeader.InvoiceID);
                        if (existingSF != null)
                        {
                            ModelState.AddModelError("", "Bu invoys uchun SF allaqachon yaratilgan");
                            return View(sfHeader);
                        }

                        // Validate invoice exists
                        var invoice = db.SALES_INVOICES.Find(sfHeader.InvoiceID);
                        if (invoice == null)
                        {
                            ModelState.AddModelError("", "Invoys topilmadi");
                            return View(sfHeader);
                        }

                        sfHeader.SFDate = DateTime.Now;
                        sfHeader.CustomerID = invoice.CustomerID;
                        sfHeader.StatusCode = "DRAFT";
                        sfHeader.CreatedDT = DateTime.Now;
                        sfHeader.IntegrationUID = Guid.NewGuid();
                        sfHeader.SourceSystem = "TAHSIN_ERP";

                        db.SALES_SF_HEADERS.Add(sfHeader);
                        db.SaveChanges();

                        // Add lines from invoice lines
                        int lineNo = 1;
                        foreach (var line in lines)
                        {
                            var sfLine = new SALES_SF_LINES
                            {
                                SFHeaderID = sfHeader.ID,
                                LineNo = lineNo++,
                                InvoiceLineID = line.InvoiceLineID,
                                ProductID = line.ProductID,
                                Description = line.Description,
                                Qty = line.Qty,
                                UnitPrice = line.UnitPrice,
                                LineAmount = line.LineAmount,
                                VATRate = line.VATRate,
                                VATAmount = line.VATAmount,
                                TotalWithVAT = line.TotalWithVAT
                            };
                            db.SALES_SF_LINES.Add(sfLine);
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesSFController", $"{sfHeader.SFNumber} - SF yaratdi (Invoys: {invoice.InvoiceNo})");

                        return RedirectToAction("Details", new { id = sfHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (sfHeader.InvoiceID.HasValue)
                {
                    var invoice = db.SALES_INVOICES
                        .Include(i => i.SALES_INVOICE_LINES)
                        .FirstOrDefault(i => i.ID == sfHeader.InvoiceID);
                    ViewBag.Invoice = invoice;
                    ViewBag.InvoiceLines = invoice?.SALES_INVOICE_LINES.OrderBy(l => l.LineNo).ToList();
                    
                    if (invoice != null)
                    {
                        var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                        ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);
                    }
                }
            }

            return View(sfHeader);
        }

        // GET: Edit
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

                // Check if SF can be edited
                if (sfHeader.StatusCode == "SENT" || sfHeader.StatusCode == "ACCEPTED")
                {
                    TempData["ErrorMessage"] = "Yuborilgan yoki qabul qilingan SF ni tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                return View(sfHeader);
            }
        }

        // POST: Edit
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

                    // Check if SF can be edited
                    if (existingSF.StatusCode == "SENT" || existingSF.StatusCode == "ACCEPTED")
                    {
                        ModelState.AddModelError("", "Yuborilgan yoki qabul qilingan SF ni tahrirlash mumkin emas");
                        return View(sfHeader);
                    }

                    try
                    {
                        existingSF.SFNumber = sfHeader.SFNumber;
                        existingSF.SFDate = sfHeader.SFDate;
                        existingSF.SupplierINN = sfHeader.SupplierINN;
                        existingSF.CustomerINN = sfHeader.CustomerINN;
                        existingSF.StatusCode = sfHeader.StatusCode;

                        // Update lines if provided
                        if (lines != null && lines.Any())
                        {
                            // Remove existing lines
                            var existingLines = db.SALES_SF_LINES.Where(l => l.SFHeaderID == sfHeader.ID).ToList();
                            foreach (var line in existingLines)
                            {
                                db.SALES_SF_LINES.Remove(line);
                            }

                            // Add updated lines
                            int lineNo = 1;
                            foreach (var line in lines)
                            {
                                var sfLine = new SALES_SF_LINES
                                {
                                    SFHeaderID = sfHeader.ID,
                                    LineNo = lineNo++,
                                    InvoiceLineID = line.InvoiceLineID,
                                    ProductID = line.ProductID,
                                    Description = line.Description,
                                    Qty = line.Qty,
                                    UnitPrice = line.UnitPrice,
                                    LineAmount = line.LineAmount,
                                    VATRate = line.VATRate,
                                    VATAmount = line.VATAmount,
                                    TotalWithVAT = line.TotalWithVAT
                                };
                                db.SALES_SF_LINES.Add(sfLine);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesSFController", $"{existingSF.SFNumber} - SF ni tahrirladi");

                        return RedirectToAction("Details", new { id = sfHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            return View(sfHeader);
        }
    }

    // ViewModel for SF lines
    public class SalesSFLineViewModel
    {
        public long? InvoiceLineID { get; set; }
        public int ProductID { get; set; }
        public string Description { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalWithVAT { get; set; }
    }
}

