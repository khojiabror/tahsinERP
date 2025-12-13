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
    public class SalesTTNKController : Controller
    {
        // GET: SalesTTNK
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string ttnkNumber, long? invoiceID, int? orderID, int? customerID, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SALES_TTNK_HEADERS
                    .Include(t => t.SALES_INVOICES)
                    .AsQueryable();

                // Filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(t => t.TTNKNumber.Contains(searchTerm) || 
                                             (t.DriverName != null && t.DriverName.Contains(searchTerm)) ||
                                             (t.TruckNo != null && t.TruckNo.Contains(searchTerm)));
                }

                if (!string.IsNullOrEmpty(ttnkNumber))
                {
                    query = query.Where(t => t.TTNKNumber.Contains(ttnkNumber));
                }

                if (invoiceID.HasValue)
                {
                    query = query.Where(t => t.SALES_TTNK_INVOICES.Any(ti => ti.InvoiceID == invoiceID.Value));
                }

                if (orderID.HasValue)
                {
                    query = query.Where(t => t.OrderID == orderID.Value);
                }

                if (customerID.HasValue)
                {
                    query = query.Where(t => t.SALES_TTNK_INVOICES.Any(ti => ti.SALES_INVOICES.CustomerID == customerID.Value));
                }

                if (!string.IsNullOrEmpty(statusCode))
                {
                    query = query.Where(t => t.StatusCode == statusCode);
                }

                // Sorting
                switch (sortOrder)
                {
                    case "ttnkNumber_desc":
                        query = query.OrderByDescending(t => t.TTNKNumber);
                        break;
                    case "ttnkNumber":
                        query = query.OrderBy(t => t.TTNKNumber);
                        break;
                    case "date_desc":
                        query = query.OrderByDescending(t => t.TTNKDate);
                        break;
                    case "date":
                        query = query.OrderBy(t => t.TTNKDate);
                        break;
                    default:
                        query = query.OrderByDescending(t => t.TTNKDate);
                        break;
                }

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var ttnkHeaders = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // ViewBag for filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
                ViewBag.Invoices = new SelectList(await db.SALES_INVOICES.ToListAsync(), "ID", "InvoiceNo", invoiceID);
                ViewBag.Orders = new SelectList(await db.S_ORDERS.Where(o => o.IsDeleted == false).ToListAsync(), "ID", "OrderNo", orderID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.TTNKNumber = ttnkNumber;
                ViewBag.InvoiceID = invoiceID;
                ViewBag.OrderID = orderID;
                ViewBag.CustomerID = customerID;
                ViewBag.StatusCode = statusCode;
                ViewBag.SortOrder = sortOrder;

                return View(ttnkHeaders);
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
                var ttnkHeader = db.SALES_TTNK_HEADERS
                    .Include(t => t.SALES_INVOICES)
                    .Include(t => t.SALES_TTNK_INVOICES.Select(ti => ti.SALES_INVOICES))
                    .Include(t => t.SALES_TTNK_LINES)
                    .FirstOrDefault(t => t.ID == id);

                if (ttnkHeader == null)
                {
                    return HttpNotFound();
                }

                // Load related order if exists
                if (ttnkHeader.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == ttnkHeader.OrderID.Value);
                }

                // Load customer from invoice
                if (ttnkHeader.SALES_INVOICES != null)
                {
                    ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == ttnkHeader.SALES_INVOICES.CustomerID);
                }

                // Load products for lines
                var productIDs = ttnkHeader.SALES_TTNK_LINES.Where(l => l.ProductID.HasValue).Select(l => l.ProductID.Value).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                return View(ttnkHeader);
            }
        }

        // GET: Create from Invoice(s)
        public ActionResult CreateFromInvoices(string invoiceIDs)
        {
            if (string.IsNullOrEmpty(invoiceIDs))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invoys ID lari ko'rsatilmagan");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoiceIDList = invoiceIDs.Split(',').Select(id => long.Parse(id.Trim())).ToList();
                var invoices = db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .Include(i => i.SALES_INVOICE_LINES)
                    .Where(i => invoiceIDList.Contains(i.ID))
                    .ToList();

                if (!invoices.Any())
                {
                    return HttpNotFound();
                }

                // Check if all invoices belong to same customer
                var customerIDs = invoices.Select(i => i.CustomerID).Distinct().ToList();
                if (customerIDs.Count > 1)
                {
                    TempData["ErrorMessage"] = "Barcha invoyslar bir xil mijozga tegishli bo'lishi kerak";
                    return RedirectToAction("Index", "SalesInvoices");
                }

                // Create TTNK header
                var ttnkHeader = new SALES_TTNK_HEADERS
                {
                    TTNKDate = DateTime.Now,
                    InvoiceID = invoices.First().ID, // Primary invoice
                    OrderID = invoices.First().OrderID,
                    ContractID = invoices.First().ContractID,
                    //CustomerID = invoices.First().CustomerID,
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now,
                    IntegrationUID = Guid.NewGuid()
                };

                // Pre-fill from first invoice
                var firstInvoice = invoices.First();
                ttnkHeader.ShipperName = ""; // To be filled from company settings
                ttnkHeader.ConsigneeName = firstInvoice.CUSTOMER?.Name ?? "";
                ttnkHeader.ConsigneeINN = firstInvoice.CUSTOMER?.DUNS ?? "";
                ttnkHeader.CurrencyCode = firstInvoice.CurrencyCode;

                ViewBag.Invoices = invoices;
                ViewBag.InvoiceIDs = invoiceIDs;

                // Get all invoice lines
                var allInvoiceLines = invoices.SelectMany(i => i.SALES_INVOICE_LINES).OrderBy(l => l.LineNo).ToList();
                ViewBag.InvoiceLines = allInvoiceLines;

                // Load products
                var productIDs = allInvoiceLines.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                return View(ttnkHeader);
            }
        }

        // POST: Create from Invoices
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateFromInvoices(SALES_TTNK_HEADERS ttnkHeader, string invoiceIDs, List<SalesTTNKLineViewModel> lines)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        if (string.IsNullOrEmpty(invoiceIDs))
                        {
                            ModelState.AddModelError("", "Invoys ID lari ko'rsatilmagan");
                            return View(ttnkHeader);
                        }

                        var invoiceIDList = invoiceIDs.Split(',').Select(id => long.Parse(id.Trim())).ToList();
                        var invoices = db.SALES_INVOICES
                            .Where(i => invoiceIDList.Contains(i.ID))
                            .ToList();

                        if (!invoices.Any())
                        {
                            ModelState.AddModelError("", "Invoyslar topilmadi");
                            return View(ttnkHeader);
                        }

                        // Set defaults
                        ttnkHeader.TTNKDate = DateTime.Now;
                        ttnkHeader.InvoiceID = invoices.First().ID;
                        ttnkHeader.OrderID = invoices.First().OrderID;
                        ttnkHeader.ContractID = invoices.First().ContractID;
                        ttnkHeader.StatusCode = "DRAFT";
                        ttnkHeader.CreatedDT = DateTime.Now;
                        ttnkHeader.IntegrationUID = Guid.NewGuid();
                        
                        // Pre-fill consignee from customer
                        var customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoices.First().CustomerID);
                        if (customer != null)
                        {
                            ttnkHeader.ConsigneeName = customer.Name;
                            ttnkHeader.ConsigneeINN = customer.DUNS;
                        }

                        // Calculate totals
                        ttnkHeader.TotalDeliveryCost = ttnkHeader.DeliveryCost + ttnkHeader.VATAmount;

                        db.SALES_TTNK_HEADERS.Add(ttnkHeader);
                        db.SaveChanges();

                        // Link invoices
                        foreach (var invoiceID in invoiceIDList)
                        {
                            var ttnkInvoice = new SALES_TTNK_INVOICES
                            {
                                TTNKHeaderID = ttnkHeader.ID,
                                InvoiceID = invoiceID,
                                AmountShare = invoices.First(i => i.ID == invoiceID).TotalAmount
                            };
                            db.SALES_TTNK_INVOICES.Add(ttnkInvoice);
                        }

                        // Add lines
                        if (lines != null && lines.Any())
                        {
                            int lineNo = 1;
                            foreach (var line in lines)
                            {
                                var ttnkLine = new SALES_TTNK_LINES
                                {
                                    TTNKHeaderID = ttnkHeader.ID,
                                    LineNo = lineNo++,
                                    ProductID = line.ProductID,
                                    InvoiceLineID = line.InvoiceLineID,
                                    HSCode = line.HSCode,
                                    Description = line.Description,
                                    UnitOfMeasure = line.UnitOfMeasure,
                                    Qty = line.Qty,
                                    UnitPrice = line.UnitPrice,
                                    LineAmount = line.LineAmount,
                                    WeightNetT = line.WeightNetT,
                                    WeightGrossT = line.WeightGrossT,
                                    PlaceCount = line.PlaceCount,
                                    PackageType = line.PackageType,
                                    Remark = line.Remark
                                };
                                db.SALES_TTNK_LINES.Add(ttnkLine);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesTTNKController", $"{ttnkHeader.TTNKNumber} - TTNK yaratdi");

                        return RedirectToAction("Details", new { id = ttnkHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Xatolik: {ex.Message}";
                        var innerEx = ex.InnerException;
                        while (innerEx != null)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                            innerEx = innerEx.InnerException;
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            // Reload ViewBag on error
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (!string.IsNullOrEmpty(invoiceIDs))
                {
                    var invoiceIDList = invoiceIDs.Split(',').Select(id => long.Parse(id.Trim())).ToList();
                    var invoices = db.SALES_INVOICES
                        .Include(i => i.SALES_INVOICE_LINES)
                        .Where(i => invoiceIDList.Contains(i.ID))
                        .ToList();
                    ViewBag.Invoices = invoices;
                    ViewBag.InvoiceIDs = invoiceIDs;
                    ViewBag.InvoiceLines = invoices.SelectMany(i => i.SALES_INVOICE_LINES).OrderBy(l => l.LineNo).ToList();
                }
            }

            return View(ttnkHeader);
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
                var ttnkHeader = db.SALES_TTNK_HEADERS
                    .Include(t => t.SALES_TTNK_LINES)
                    .Include(t => t.SALES_TTNK_INVOICES)
                    .FirstOrDefault(t => t.ID == id);

                if (ttnkHeader == null)
                {
                    return HttpNotFound();
                }

                // Check if TTNK can be edited
                if (ttnkHeader.StatusCode == "FINALIZED" || ttnkHeader.StatusCode == "SENT")
                {
                    TempData["ErrorMessage"] = "Yakunlangan yoki yuborilgan TTNK ni tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                return View(ttnkHeader);
            }
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SALES_TTNK_HEADERS ttnkHeader, List<SalesTTNKLineViewModel> lines)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var existingTTNK = db.SALES_TTNK_HEADERS.Find(ttnkHeader.ID);
                    if (existingTTNK == null)
                    {
                        return HttpNotFound();
                    }

                    // Check if TTNK can be edited
                    if (existingTTNK.StatusCode == "FINALIZED" || existingTTNK.StatusCode == "SENT")
                    {
                        ModelState.AddModelError("", "Yakunlangan yoki yuborilgan TTNK ni tahrirlash mumkin emas");
                        return View(ttnkHeader);
                    }

                    try
                    {
                        // Update header fields
                        existingTTNK.TTNKNumber = ttnkHeader.TTNKNumber;
                        existingTTNK.TTNKDate = ttnkHeader.TTNKDate;
                        existingTTNK.ShipperName = ttnkHeader.ShipperName;
                        existingTTNK.ShipperINN = ttnkHeader.ShipperINN;
                        existingTTNK.ConsigneeName = ttnkHeader.ConsigneeName;
                        existingTTNK.ConsigneeINN = ttnkHeader.ConsigneeINN;
                        existingTTNK.CarrierName = ttnkHeader.CarrierName;
                        existingTTNK.CarrierINN = ttnkHeader.CarrierINN;
                        existingTTNK.ResponsiblePersonName = ttnkHeader.ResponsiblePersonName;
                        existingTTNK.ResponsiblePersonPINFL = ttnkHeader.ResponsiblePersonPINFL;
                        existingTTNK.DriverName = ttnkHeader.DriverName;
                        existingTTNK.DriverPINFL = ttnkHeader.DriverPINFL;
                        existingTTNK.TruckNo = ttnkHeader.TruckNo;
                        existingTTNK.TrailerNo = ttnkHeader.TrailerNo;
                        existingTTNK.TruckModel = ttnkHeader.TruckModel;
                        existingTTNK.TrailerModel = ttnkHeader.TrailerModel;
                        existingTTNK.DepartureAddress = ttnkHeader.DepartureAddress;
                        existingTTNK.DepartureRegion = ttnkHeader.DepartureRegion;
                        existingTTNK.ArrivalAddress = ttnkHeader.ArrivalAddress;
                        existingTTNK.ArrivalRegion = ttnkHeader.ArrivalRegion;
                        existingTTNK.DistanceKm = ttnkHeader.DistanceKm;
                        existingTTNK.CargoWeightGrossT = ttnkHeader.CargoWeightGrossT;
                        existingTTNK.CargoWeightNetT = ttnkHeader.CargoWeightNetT;
                        existingTTNK.CurrencyCode = ttnkHeader.CurrencyCode;
                        existingTTNK.DeliveryCost = ttnkHeader.DeliveryCost;
                        existingTTNK.VATAmount = ttnkHeader.VATAmount;
                        existingTTNK.TotalDeliveryCost = ttnkHeader.DeliveryCost + ttnkHeader.VATAmount;
                        existingTTNK.StatusCode = ttnkHeader.StatusCode;
                        existingTTNK.UpdatedDT = DateTime.Now;

                        // Update lines if provided
                        if (lines != null && lines.Any())
                        {
                            // Remove existing lines
                            var existingLines = db.SALES_TTNK_LINES.Where(l => l.TTNKHeaderID == ttnkHeader.ID).ToList();
                            foreach (var line in existingLines)
                            {
                                db.SALES_TTNK_LINES.Remove(line);
                            }

                            // Add updated lines
                            int lineNo = 1;
                            foreach (var line in lines)
                            {
                                var ttnkLine = new SALES_TTNK_LINES
                                {
                                    TTNKHeaderID = ttnkHeader.ID,
                                    LineNo = lineNo++,
                                    ProductID = line.ProductID,
                                    InvoiceLineID = line.InvoiceLineID,
                                    HSCode = line.HSCode,
                                    Description = line.Description,
                                    UnitOfMeasure = line.UnitOfMeasure,
                                    Qty = line.Qty,
                                    UnitPrice = line.UnitPrice,
                                    LineAmount = line.LineAmount,
                                    WeightNetT = line.WeightNetT,
                                    WeightGrossT = line.WeightGrossT,
                                    PlaceCount = line.PlaceCount,
                                    PackageType = line.PackageType,
                                    Remark = line.Remark
                                };
                                db.SALES_TTNK_LINES.Add(ttnkLine);
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesTTNKController", $"{existingTTNK.TTNKNumber} - TTNK ni tahrirladi");

                        return RedirectToAction("Details", new { id = ttnkHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Xatolik: {ex.Message}";
                        var innerEx = ex.InnerException;
                        while (innerEx != null)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                            innerEx = innerEx.InnerException;
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            return View(ttnkHeader);
        }
    }

    // ViewModel for TTNK lines
    public class SalesTTNKLineViewModel
    {
        public int? ProductID { get; set; }
        public long? InvoiceLineID { get; set; }
        public string HSCode { get; set; }
        public string Description { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal Qty { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineAmount { get; set; }
        public decimal? WeightNetT { get; set; }
        public decimal? WeightGrossT { get; set; }
        public int? PlaceCount { get; set; }
        public string PackageType { get; set; }
        public string Remark { get; set; }
    }
}

