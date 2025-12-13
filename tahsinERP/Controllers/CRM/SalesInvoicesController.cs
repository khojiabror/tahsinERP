using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    public class SalesInvoicesController : Controller
    {
        // GET: SalesInvoices
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string invoiceNo, int? customerID, int? orderID, int? contractID, DateTime? fromDate, DateTime? toDate, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SALES_INVOICES.AsQueryable();

                // Filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(i => i.InvoiceNo.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(invoiceNo))
                {
                    query = query.Where(i => i.InvoiceNo.Contains(invoiceNo));
                }

                if (customerID.HasValue)
                {
                    query = query.Where(i => i.CustomerID == customerID.Value);
                }

                if (orderID.HasValue)
                {
                    query = query.Where(i => i.OrderID == orderID.Value);
                }

                if (contractID.HasValue)
                {
                    query = query.Where(i => i.ContractID == contractID.Value);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate <= toDate.Value);
                }

                if (!string.IsNullOrEmpty(statusCode))
                {
                    query = query.Where(i => i.StatusCode == statusCode);
                }

                // Sorting
                switch (sortOrder)
                {
                    case "invoiceNo_desc":
                        query = query.OrderByDescending(i => i.InvoiceNo);
                        break;
                    case "invoiceNo":
                        query = query.OrderBy(i => i.InvoiceNo);
                        break;
                    case "date_desc":
                        query = query.OrderByDescending(i => i.InvoiceDate);
                        break;
                    case "date":
                        query = query.OrderBy(i => i.InvoiceDate);
                        break;
                    default:
                        query = query.OrderByDescending(i => i.InvoiceDate);
                        break;
                }

                // Pagination
                int currentPage = page ?? 1;
                int itemsPerPage = pageSize ?? 25;
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var invoices = await query
                    .Skip((currentPage - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                // ViewBag for filters
                ViewBag.Customers = new SelectList(await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "Name", customerID);
                ViewBag.Orders = new SelectList(await db.S_ORDERS.Where(o => o.IsDeleted == false).ToListAsync(), "ID", "OrderNo", orderID);
                ViewBag.Contracts = new SelectList(await db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractID);
                ViewBag.CurrentPage = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalItems;
                ViewBag.PageSize = itemsPerPage;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.InvoiceNo = invoiceNo;
                ViewBag.CustomerID = customerID;
                ViewBag.OrderID = orderID;
                ViewBag.ContractID = contractID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.StatusCode = statusCode;
                ViewBag.SortOrder = sortOrder;

                return View(invoices);
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
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Load customer
                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);

                // Get related order and contract
                if (invoice.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == invoice.OrderID.Value);
                }

                if (invoice.ContractID.HasValue)
                {
                    ViewBag.Contract = db.S_CONTRACTS
                        .Include(c => c.CUSTOMER)
                        .FirstOrDefault(c => c.ID == invoice.ContractID.Value);
                }

                // Load products for invoice lines
                var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                // Get SF count
                var sfCount = db.SALES_SF_HEADERS.Where(sf => sf.InvoiceID == id).Count();
                ViewBag.SFCount = sfCount;

                // Get TTNK count
                var ttnkCount = db.SALES_TTNK_INVOICES.Where(t => t.InvoiceID == id).Count();
                ViewBag.TTNKCount = ttnkCount;

                return View(invoice);
            }
        }

        // GET: Create from Order
        public ActionResult CreateFromOrder(int? orderID)
        {
            if (orderID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var order = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .Include(o => o.CURRENCy1)
                    .FirstOrDefault(o => o.ID == orderID && o.IsDeleted == false);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Check if order products have been quality inspected (business rule)
                // Quality inspection is required before invoicing

                // Pre-fill invoice from order
                var invoice = new SALES_INVOICES
                {
                    InvoiceDate = DateTime.Now,
                    CustomerID = order.CustomerID,
                    ContractID = order.ContractID,
                    OrderID = order.ID,
                    CurrencyCode = order.CURRENCy1?.Shortname ?? "UZS",
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now
                };

                ViewBag.Order = order;
                ViewBag.OrderProducts = order.S_ORDER_PRODUCTS.ToList();
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);

                return View(invoice);
            }
        }

        // POST: Create from Order
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateFromOrder(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            if (ModelState.IsValid && lines != null && lines.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Validate quantities against order
                        var order = db.S_ORDERS
                            .Include(o => o.S_ORDER_PRODUCTS)
                            .FirstOrDefault(o => o.ID == invoice.OrderID && o.IsDeleted == false);

                        if (order == null)
                        {
                            ModelState.AddModelError("", "Buyurtma topilmadi");
                            return View(invoice);
                        }

                        // Validate each line
                        foreach (var line in lines)
                        {
                            var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ProductID == line.ProductID);
                            if (orderProduct == null)
                            {
                                ModelState.AddModelError("", $"Mahsulot {line.ProductID} buyurtmada topilmadi");
                                return View(invoice);
                            }

                            // Check if invoiced quantity exceeds ordered quantity
                            var alreadyInvoiced = db.SALES_INVOICE_LINES
                                .Where(l => l.SALES_INVOICES.OrderID == order.ID && l.ProductID == line.ProductID)
                                .Sum(l => (decimal?)l.Qty) ?? 0;

                            if (alreadyInvoiced + line.Qty > (decimal)orderProduct.Amount)
                            {
                                ModelState.AddModelError("", $"Mahsulot {line.ProductID} uchun miqdor buyurtma miqdoridan oshib ketdi");
                                return View(invoice);
                            }

                            // QUALITY INSPECTION GATING: Check if quality inspection is approved and has enough accepted qty
                            var totalAccepted = db.QUALITY_INSPECTION
                                .Where(q => q.ProductID == line.ProductID 
                                    && q.StatusCode == "APPROVED" 
                                    && q.IsDeleted == false)
                                .Sum(q => (decimal?)q.QtyAccepted) ?? 0;

                            var totalAlreadyInvoicedForProduct = db.SALES_INVOICE_LINES
                                .Where(l => l.ProductID == line.ProductID)
                                .Sum(l => (decimal?)l.Qty) ?? 0;

                            var availableQty = totalAccepted - totalAlreadyInvoicedForProduct;

                            if (line.Qty > availableQty)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun sifat nazorati qabul qilingan miqdor yetarli emas. " +
                                    $"Mavjud: {availableQty}, Talab qilinmoqda: {line.Qty}, Jami qabul qilingan: {totalAccepted}, " +
                                    $"Allaqachon fakturalangan: {totalAlreadyInvoicedForProduct}");
                                return View(invoice);
                            }

                            // Check if product has been produced
                            var totalProduced = db.PRODUCT_PRODUCTION
                                .Where(p => p.ProductID == line.ProductID)
                                .Sum(p => (double?)p.Total) ?? 0;

                            if (totalProduced < (double)line.Qty)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun ishlab chiqarilgan miqdor yetarli emas. " +
                                    $"Ishlab chiqarilgan: {totalProduced}, Talab qilinmoqda: {line.Qty}");
                                return View(invoice);
                            }
                        }

                        // Calculate totals
                        decimal totalAmount = 0;
                        decimal totalVAT = 0;
                        foreach (var line in lines)
                        {
                            line.LineAmount = line.Qty * line.UnitPrice;
                            line.VATAmount = line.LineAmount * line.VATRate / 100;
                            line.TotalWithVAT = line.LineAmount + line.VATAmount;
                            totalAmount += line.LineAmount;
                            totalVAT += line.VATAmount;
                        }

                        invoice.TotalAmount = totalAmount;
                        invoice.TotalVAT = totalVAT;
                        invoice.TotalWithVAT = totalAmount + totalVAT;
                        invoice.CreatedDT = DateTime.Now;
                        invoice.CreatedByUserID = GetCurrentUserID();

                        db.SALES_INVOICES.Add(invoice);
                        db.SaveChanges();

                        // Add lines
                        int lineNo = 1;
                        foreach (var line in lines)
                        {
                            var invoiceLine = new SALES_INVOICE_LINES
                            {
                                InvoiceID = invoice.ID,
                                LineNo = lineNo++,
                                ProductID = line.ProductID,
                                Description = line.Description,
                                Qty = line.Qty,
                                UnitPrice = line.UnitPrice,
                                LineAmount = line.LineAmount,
                                VATRate = line.VATRate,
                                VATAmount = line.VATAmount,
                                TotalWithVAT = line.TotalWithVAT
                            };
                            db.SALES_INVOICE_LINES.Add(invoiceLine);
                        }

                        db.SaveChanges();

                        // Post to finance and decrease stock if invoice is created with FINALIZED or APPROVED status
                        if (invoice.StatusCode == "FINALIZED" || invoice.StatusCode == "APPROVED")
                        {
                            try
                            {
                                var userId = UserHelper.GetUserId(User.Identity.Name);
                                FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);

                                // Decrease stock in Sales Warehouse for each invoice line
                                foreach (var line in lines)
                                {
                                    try
                                    {
                                        WarehouseFlowService.DecreaseSalesStock(
                                            line.ProductID,
                                            line.Qty,
                                            db,
                                            User.Identity.Name,
                                            invoice.ID,
                                            invoice.InvoiceNo ?? invoice.ID.ToString()
                                        );
                                    }
                                    catch (Exception stockEx)
                                    {
                                        // Log stock error but don't fail the invoice creation
                                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "CreateFromOrder",
                                            $"Stock decrease error for product {line.ProductID} in invoice {invoice.ID}: {stockEx.Message}");
                                        // Re-throw if it's a critical stock shortage
                                        if (stockEx.Message.Contains("yetarli mahsulot yo'q"))
                                        {
                                            throw;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "CreateFromOrder", 
                                    $"Finance posting or stock decrease error for invoice {invoice.ID}: {ex.Message}");
                                // Re-throw if it's a stock shortage
                                if (ex.Message.Contains("yetarli mahsulot yo'q"))
                                {
                                    throw;
                                }
                            }
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoicesController", $"{invoice.InvoiceNo} - Invoysni yaratdi (Buyurtma: {order.OrderNo})");

                        return RedirectToAction("Details", new { id = invoice.ID });
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
                if (invoice.OrderID.HasValue)
                {
                    var order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                        .FirstOrDefault(o => o.ID == invoice.OrderID);
                    ViewBag.Order = order;
                    ViewBag.OrderProducts = order?.S_ORDER_PRODUCTS.ToList();
                }
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
            }

            return View(invoice);
        }

        // GET: Create (standalone)
        public ActionResult Create(int? customerID, int? orderID, int? contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = new SALES_INVOICES
                {
                    InvoiceDate = DateTime.Now,
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now
                };

                if (customerID.HasValue)
                {
                    invoice.CustomerID = customerID.Value;
                }

                if (orderID.HasValue)
                {
                    invoice.OrderID = orderID.Value;
                }

                if (contractID.HasValue)
                {
                    invoice.ContractID = contractID.Value;
                }

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", customerID);
                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", orderID);
                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", contractID);
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname");
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");

                return View(invoice);
            }
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            if (ModelState.IsValid && lines != null && lines.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Validate quality inspection for each line
                        foreach (var line in lines)
                        {
                            // QUALITY INSPECTION GATING: Check if quality inspection is approved and has enough accepted qty
                            var totalAccepted = db.QUALITY_INSPECTION
                                .Where(q => q.ProductID == line.ProductID 
                                    && q.StatusCode == "APPROVED" 
                                    && q.IsDeleted == false)
                                .Sum(q => (decimal?)q.QtyAccepted) ?? 0;

                            var totalAlreadyInvoicedForProduct = db.SALES_INVOICE_LINES
                                .Where(l => l.ProductID == line.ProductID)
                                .Sum(l => (decimal?)l.Qty) ?? 0;

                            var availableQty = totalAccepted - totalAlreadyInvoicedForProduct;

                            if (line.Qty > availableQty)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun sifat nazorati qabul qilingan miqdor yetarli emas. " +
                                    $"Mavjud: {availableQty}, Talab qilinmoqda: {line.Qty}, Jami qabul qilingan: {totalAccepted}, " +
                                    $"Allaqachon fakturalangan: {totalAlreadyInvoicedForProduct}");
                                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
                                return View(invoice);
                            }

                            // Check if product has been produced
                            var totalProduced = db.PRODUCT_PRODUCTION
                                .Where(p => p.ProductID == line.ProductID)
                                .Sum(p => (double?)p.Total) ?? 0;

                            if (totalProduced < (double)line.Qty)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun ishlab chiqarilgan miqdor yetarli emas. " +
                                    $"Ishlab chiqarilgan: {totalProduced}, Talab qilinmoqda: {line.Qty}");
                                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
                                return View(invoice);
                            }
                        }

                        // Calculate totals
                        decimal totalAmount = 0;
                        decimal totalVAT = 0;
                        foreach (var line in lines)
                        {
                            line.LineAmount = line.Qty * line.UnitPrice;
                            line.VATAmount = line.LineAmount * line.VATRate / 100;
                            line.TotalWithVAT = line.LineAmount + line.VATAmount;
                            totalAmount += line.LineAmount;
                            totalVAT += line.VATAmount;
                        }

                        invoice.TotalAmount = totalAmount;
                        invoice.TotalVAT = totalVAT;
                        invoice.TotalWithVAT = totalAmount + totalVAT;
                        invoice.CreatedDT = DateTime.Now;
                        invoice.CreatedByUserID = GetCurrentUserID();

                        db.SALES_INVOICES.Add(invoice);
                        db.SaveChanges();

                        // Add lines
                        int lineNo = 1;
                        foreach (var line in lines)
                        {
                            var invoiceLine = new SALES_INVOICE_LINES
                            {
                                InvoiceID = invoice.ID,
                                LineNo = lineNo++,
                                ProductID = line.ProductID,
                                Description = line.Description,
                                Qty = line.Qty,
                                UnitPrice = line.UnitPrice,
                                LineAmount = line.LineAmount,
                                VATRate = line.VATRate,
                                VATAmount = line.VATAmount,
                                TotalWithVAT = line.TotalWithVAT
                            };
                            db.SALES_INVOICE_LINES.Add(invoiceLine);
                        }

                        db.SaveChanges();

                        // Post to finance and decrease stock if invoice is created with FINALIZED or APPROVED status
                        if (invoice.StatusCode == "FINALIZED" || invoice.StatusCode == "APPROVED")
                        {
                            try
                            {
                                var userId = UserHelper.GetUserId(User.Identity.Name);
                                FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);

                                // Decrease stock in Sales Warehouse for each invoice line
                                foreach (var line in lines)
                                {
                                    try
                                    {
                                        WarehouseFlowService.DecreaseSalesStock(
                                            line.ProductID,
                                            line.Qty,
                                            db,
                                            User.Identity.Name,
                                            invoice.ID,
                                            invoice.InvoiceNo ?? invoice.ID.ToString()
                                        );
                                    }
                                    catch (Exception stockEx)
                                    {
                                        // Log stock error but don't fail the invoice creation
                                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Create",
                                            $"Stock decrease error for product {line.ProductID} in invoice {invoice.ID}: {stockEx.Message}");
                                        // Re-throw if it's a critical stock shortage
                                        if (stockEx.Message.Contains("yetarli mahsulot yo'q"))
                                        {
                                            throw;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Create", 
                                    $"Finance posting or stock decrease error for invoice {invoice.ID}: {ex.Message}");
                                // Re-throw if it's a stock shortage
                                if (ex.Message.Contains("yetarli mahsulot yo'q"))
                                {
                                    throw;
                                }
                            }
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoicesController", $"{invoice.InvoiceNo} - Invoysni yaratdi");

                        return RedirectToAction("Details", new { id = invoice.ID });
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
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
            }

            return View(invoice);
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
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Check if invoice can be edited (business rule)
                if (invoice.StatusCode == "FINALIZED" || invoice.StatusCode == "PAID")
                {
                    TempData["ErrorMessage"] = "Yakunlangan yoki to'langan invoysni tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");

                return View(invoice);
            }
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var existingInvoice = db.SALES_INVOICES.Find(invoice.ID);
                    if (existingInvoice == null)
                    {
                        return HttpNotFound();
                    }

                    // Check if invoice can be edited
                    if (existingInvoice.StatusCode == "FINALIZED" || existingInvoice.StatusCode == "PAID")
                    {
                        ModelState.AddModelError("", "Yakunlangan yoki to'langan invoysni tahrirlash mumkin emas");
                        return View(invoice);
                    }

                    try
                    {
                        existingInvoice.InvoiceNo = invoice.InvoiceNo;
                        existingInvoice.InvoiceDate = invoice.InvoiceDate;
                        existingInvoice.CustomerID = invoice.CustomerID;
                        existingInvoice.ContractID = invoice.ContractID;
                        existingInvoice.OrderID = invoice.OrderID;
                        existingInvoice.CurrencyCode = invoice.CurrencyCode;
                        existingInvoice.DueDate = invoice.DueDate;
                        existingInvoice.StatusCode = invoice.StatusCode;
                        existingInvoice.UpdatedDT = DateTime.Now;

                        // Remove existing lines
                        var existingLines = db.SALES_INVOICE_LINES.Where(l => l.InvoiceID == invoice.ID).ToList();
                        foreach (var line in existingLines)
                        {
                            db.SALES_INVOICE_LINES.Remove(line);
                        }

                        // Add updated lines
                        if (lines != null && lines.Any())
                        {
                            decimal totalAmount = 0;
                            decimal totalVAT = 0;
                            int lineNo = 1;

                            foreach (var line in lines)
                            {
                                line.LineAmount = line.Qty * line.UnitPrice;
                                line.VATAmount = line.LineAmount * line.VATRate / 100;
                                line.TotalWithVAT = line.LineAmount + line.VATAmount;
                                totalAmount += line.LineAmount;
                                totalVAT += line.VATAmount;

                                var invoiceLine = new SALES_INVOICE_LINES
                                {
                                    InvoiceID = invoice.ID,
                                    LineNo = lineNo++,
                                    ProductID = line.ProductID,
                                    Description = line.Description,
                                    Qty = line.Qty,
                                    UnitPrice = line.UnitPrice,
                                    LineAmount = line.LineAmount,
                                    VATRate = line.VATRate,
                                    VATAmount = line.VATAmount,
                                    TotalWithVAT = line.TotalWithVAT
                                };
                                db.SALES_INVOICE_LINES.Add(invoiceLine);
                            }

                        existingInvoice.TotalAmount = totalAmount;
                        existingInvoice.TotalVAT = totalVAT;
                        existingInvoice.TotalWithVAT = totalAmount + totalVAT;
                        }

                        // Check if status changed to FINALIZED or APPROVED - post to finance and decrease stock
                        bool statusChangedToFinalized = (existingInvoice.StatusCode != "FINALIZED" && existingInvoice.StatusCode != "APPROVED") 
                            && (invoice.StatusCode == "FINALIZED" || invoice.StatusCode == "APPROVED");

                        db.SaveChanges();

                        // Post to finance and decrease stock if status changed to finalized/approved
                        if (statusChangedToFinalized)
                        {
                            try
                            {
                                var userId = UserHelper.GetUserId(User.Identity.Name);
                                FinancePostingService.PostSalesInvoice((int)existingInvoice.ID, db, userId);

                                // Decrease stock in Sales Warehouse for each invoice line
                                if (lines != null && lines.Any())
                                {
                                    foreach (var line in lines)
                                    {
                                        try
                                        {
                                            WarehouseFlowService.DecreaseSalesStock(
                                                line.ProductID,
                                                line.Qty,
                                                db,
                                                User.Identity.Name,
                                                existingInvoice.ID,
                                                existingInvoice.InvoiceNo ?? existingInvoice.ID.ToString()
                                            );
                                        }
                                        catch (Exception stockEx)
                                        {
                                            // Log stock error but don't fail the invoice save
                                            LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Edit",
                                                $"Stock decrease error for product {line.ProductID} in invoice {existingInvoice.ID}: {stockEx.Message}");
                                            // Re-throw if it's a critical stock shortage
                                            if (stockEx.Message.Contains("yetarli mahsulot yo'q"))
                                            {
                                                throw;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but don't fail the invoice save (unless it's stock shortage)
                                LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Edit", 
                                    $"Finance posting or stock decrease error for invoice {existingInvoice.ID}: {ex.Message}");
                                // Re-throw if it's a stock shortage
                                if (ex.Message.Contains("yetarli mahsulot yo'q"))
                                {
                                    throw;
                                }
                            }
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoicesController", $"{existingInvoice.InvoiceNo} - Invoysni tahrirladi");

                        return RedirectToAction("Details", new { id = invoice.ID });
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
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = new SelectList(db.PRODUCTS.Where(p => p.IsDeleted == false).ToList(), "ID", "PNo");
            }

            return View(invoice);
        }

        // Helper method to get current user ID
        private int? GetCurrentUserID()
        {
            try
            {
                return UserHelper.GetUserId(User.Identity.Name);
            }
            catch
            {
                return null;
            }
        }

        // GET: SalesInvoices/Print/5
        public ActionResult Print(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Load customer
                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);

                // Get related order and contract
                if (invoice.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == invoice.OrderID.Value);
                }

                if (invoice.ContractID.HasValue)
                {
                    ViewBag.Contract = db.S_CONTRACTS
                        .Include(c => c.CUSTOMER)
                        .FirstOrDefault(c => c.ID == invoice.ContractID.Value);
                }

                // Load products for invoice lines
                var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);

                // Load company info
                ViewBag.Company = db.COMPANIES.FirstOrDefault();

                return View(invoice);
            }
        }
    }

    // ViewModel for invoice lines
    public class SalesInvoiceLineViewModel
    {
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

