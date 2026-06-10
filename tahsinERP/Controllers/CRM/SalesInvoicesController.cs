using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;
using tahsinERP.Services;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Controllers
{
    public class SalesInvoicesController : Controller
    {
        // Olish so'rovi: SalesInvoices
        public async System.Threading.Tasks.Task<ActionResult> Index(string searchTerm, string invoiceNo, int? customerID, int? orderID, int? contractID, DateTime? fromDate, DateTime? toDate, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.SALES_INVOICES.AsQueryable();
                query = query.Where(i => i.StatusCode == null
                    || (i.StatusCode != "CANCELED" && i.StatusCode != "CANCELLED"));

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

                // ViewBag uchun filters
                var customersList = await db.CUSTOMERS.Where(c => c.IsDeleted == false).ToListAsync();
                ViewBag.Customers = new SelectList(customersList, "ID", "Name", customerID);
                ViewBag.CustomerNames = customersList.ToDictionary(c => c.ID, c => c.Name);
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

        // Olish so'rovi: Details
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

                // Yuklash customer
                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);

                // Olish related order va contract
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

                // Yuklash products uchun invoice lines
                var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS
                    .Include(p => p.UNIT)
                    .Where(p => productIDs.Contains(p.ID))
                    .ToDictionary(p => p.ID, p => p);

                // Olish SF count
                var sfCount = db.SALES_SF_HEADERS.Where(sf => sf.InvoiceID == id).Count();
                ViewBag.SFCount = sfCount;

                // Olish TTNK count
                var ttnkCount = db.SALES_TTNK_INVOICES.Where(t => t.InvoiceID == id).Count();
                ViewBag.TTNKCount = ttnkCount;

                return View(invoice);
            }
        }

        // Olish so'rovi: yaratish dan Order
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

                // Tekshirish agar order products have been quality inspected (business rule)
                // Quality inspection is majburiy before invoicing

                // Pre-fill invoice dan order
                var invoice = new SALES_INVOICES
                {
                    InvoiceDate = DateTime.Now,
                    CustomerID = order.CustomerID,
                    ContractID = order.ContractID,
                    OrderID = order.ID,
                    CurrencyCode = order.CURRENCy1?.Shortname ?? "UZS",
                    StatusCode = "APPROVED",
                    CreatedDT = DateTime.Now
                };

                ViewBag.Order = order;
                ViewBag.OrderProducts = order.S_ORDER_PRODUCTS.ToList();
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);

                return View(invoice);
            }
        }

        // POST so'rovi: yaratish dan Order
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
                        // Tekshirish quantities against order
                        var order = db.S_ORDERS
                            .Include(o => o.S_ORDER_PRODUCTS)
                            .FirstOrDefault(o => o.ID == invoice.OrderID && o.IsDeleted == false);

                        if (order == null)
                        {
                            ModelState.AddModelError("", "Buyurtma topilmadi");
                            return View(invoice);
                        }

                        // Tekshirish har bir line
                        foreach (var line in lines)
                        {
                            var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ProductID == line.ProductID);
                            if (orderProduct == null)
                            {
                                ModelState.AddModelError("", $"Mahsulot {line.ProductID} buyurtmada topilmadi");
                                return View(invoice);
                            }

                            // Tekshirish agar invoiced quantity exceeds ordered quantity
                            var alreadyInvoiced = db.SALES_INVOICE_LINES
                                .Where(l => l.SALES_INVOICES.OrderID == order.ID && l.ProductID == line.ProductID)
                                .Sum(l => (decimal?)l.Qty) ?? 0;

                            if (alreadyInvoiced + line.Qty > (decimal)orderProduct.Amount)
                            {
                                ModelState.AddModelError("", $"Mahsulot {line.ProductID} uchun miqdor buyurtma miqdoridan oshib ketdi");
                                return View(invoice);
                            }

                            // QUALITY INSPECTION GATING: tekshirish agar quality inspection is approved va has enough accepted qty
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

                            // Tekshirish agar product has been produced
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
                        // Biznes qoidasi: invoys yaratildimi — darhol APPROVED
                        invoice.StatusCode = "APPROVED";
                        if (string.IsNullOrWhiteSpace(invoice.InvoiceNo))
                            invoice.InvoiceNo = GetNextInvoiceNo(db);

                        db.SALES_INVOICES.Add(invoice);
                        db.SaveChanges();

                        // Qo'shish lines
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

                        // Har doim sotuv omboridan chiqim qilamiz (invoys yaratilganda avtomatik expense + stock kamayish)
                        try
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
                                        invoice.ID,
                                        invoice.InvoiceNo ?? invoice.ID.ToString(),
                                        invoice.InvoiceDate,
                                        invoice.ContractID
                                    );
                                }
                                catch (Exception stockEx)
                                {
                                    LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "CreateFromOrder",
                                        $"Stock decrease error for product {line.ProductID} in invoice {invoice.ID}: {stockEx.Message}");
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
                                $"Stock decrease error for invoice {invoice.ID}: {ex.Message}");
                            if (ex.Message.Contains("yetarli mahsulot yo'q"))
                            {
                                throw;
                            }
                        }

                        // Biznes qoidasi: invoys yaratilganda darhol finance posting qilinadi
                        try
                        {
                            var userId = UserHelper.GetUserId(User.Identity.Name);
                            FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "CreateFromOrder",
                                $"Finance posting error for invoice {invoice.ID}: {ex.Message}");
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoicesController", $"{invoice.InvoiceNo} - Invoysni yaratdi (Buyurtma: {order.OrderNo})");

                        return RedirectToAction("Details", new { id = invoice.ID });
                    }
                    catch (DbEntityValidationException dbEx)
                    {
                        var validationErrors = new List<string>();
                        foreach (var entityErrors in dbEx.EntityValidationErrors)
                        {
                            foreach (var error in entityErrors.ValidationErrors)
                            {
                                validationErrors.Add($"Maydon: {error.PropertyName}, Xatolik: {error.ErrorMessage}");
                            }
                        }

                        var errorMessage = "Ma'lumotlar validatsiyadan o'tmadi. Quyidagi maydonlarni tekshiring: "
                            + string.Join("; ", validationErrors);
                        ModelState.AddModelError("", errorMessage);
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

            // Reload ViewBag on xatolik
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

        private string GetNextInvoiceNo(DBTHSNEntities db)
        {
            var last = db.SALES_INVOICES
                .Where(i => i.StatusCode == null || (i.StatusCode != "CANCELED" && i.StatusCode != "CANCELLED"))
                .OrderByDescending(i => i.ID)
                .Select(i => i.InvoiceNo)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(last)) return "INV_0001";
            var m = System.Text.RegularExpressions.Regex.Match(last, @"^(.*?)(\d+)$");
            if (!m.Success) return "INV_0001";
            var prefix = m.Groups[1].Value;
            var num = int.Parse(m.Groups[2].Value) + 1;
            var padLen = m.Groups[2].Length;
            return prefix + num.ToString("D" + padLen);
        }

        // Olish so'rovi: yaratish (standalone) - SOrder/Create uslubida: xaridor -> shartnoma -> buyurtma
        public ActionResult Create(int? customerID, int? orderID, int? contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = new SALES_INVOICES
                {
                    InvoiceNo = GetNextInvoiceNo(db),
                    InvoiceDate = DateTime.Now,
                    StatusCode = "APPROVED",
                    CreatedDT = DateTime.Now
                };

                if (customerID.HasValue)
                {
                    invoice.CustomerID = customerID.Value;
                }

                if (orderID.HasValue)
                {
                    invoice.OrderID = orderID.Value;
                    var order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .Include(o => o.S_CONTRACTS)
                        .Include(o => o.CURRENCy1)
                        .FirstOrDefault(o => o.ID == orderID.Value && (o.IsDeleted == false || o.IsDeleted == null));
                    if (order != null)
                    {
                        invoice.CustomerID = order.CustomerID;
                        invoice.ContractID = order.ContractID;
                        invoice.CurrencyCode = order.CURRENCy1?.Shortname ?? "UZS";
                        customerID = order.CustomerID;
                        contractID = order.ContractID;
                    }
                }

                if (contractID.HasValue)
                {
                    invoice.ContractID = contractID.Value;
                    var contract = db.S_CONTRACTS.Include(c => c.CURRENCy).FirstOrDefault(c => c.ID == contractID.Value);
                    if (contract?.CURRENCy != null)
                    {
                        invoice.CurrencyCode = contract.CURRENCy.Shortname;
                    }
                }

                // Xaridorlar
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.Name).ToList(), "ID", "Name", customerID);

                // Faqat tanlangan xaridorning shartnomalari
                var contractsList = new List<S_CONTRACTS>();
                if (customerID.HasValue && customerID.Value > 0)
                {
                    contractsList = db.S_CONTRACTS
                        .Where(c => c.CustomerID == customerID.Value && (c.IsDeleted == false || c.IsDeleted == null))
                        .OrderBy(c => c.ContractNo)
                        .ToList();
                }
                var contractsSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                contractsSelect.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == contractID }));
                ViewBag.Contracts = new SelectList(contractsSelect, "Value", "Text", contractID?.ToString() ?? "");

                // Faqat tanlangan shartnomaning buyurtmalari
                var ordersList = new List<S_ORDERS>();
                if (contractID.HasValue && contractID.Value > 0)
                {
                    ordersList = db.S_ORDERS
                        .Where(o => o.ContractID == contractID.Value && (o.IsDeleted == false || o.IsDeleted == null))
                        .OrderByDescending(o => o.IssuedDate)
                        .ToList();
                }
                var ordersSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                ordersSelect.AddRange(ordersList.Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == orderID }));
                ViewBag.Orders = new SelectList(ordersSelect, "Value", "Text", orderID?.ToString() ?? "");

                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode ?? "UZS");
                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);

                return View(invoice);
            }
        }

        // AJAX: Xaridorga qarab shartnomalar
        [HttpGet]
        public ActionResult GetContractsByCustomer(int customerId)
        {
            using (var db = new DBTHSNEntities())
            {
                var contracts = db.S_CONTRACTS
                    .Where(c => c.CustomerID == customerId && (c.IsDeleted == false || c.IsDeleted == null))
                    .OrderBy(c => c.ContractNo)
                    .Select(c => new { ID = c.ID, ContractNo = c.ContractNo })
                    .ToList();
                return Json(contracts, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnomaga qarab buyurtmalar
        [HttpGet]
        public ActionResult GetOrdersByContract(int contractId)
        {
            using (var db = new DBTHSNEntities())
            {
                var orders = db.S_ORDERS
                    .Where(o => o.ContractID == contractId && (o.IsDeleted == false || o.IsDeleted == null))
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { ID = o.ID, OrderNo = o.OrderNo })
                    .ToList();
                return Json(orders, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Buyurtma mahsulotlari — Create formda buyurtma bo'yicha qatorlarni to'ldirish uchun
        [HttpGet]
        public ActionResult GetOrderProductsForInvoice(int orderId)
        {
            using (var db = new DBTHSNEntities())
            {
                var lines = db.S_ORDER_PRODUCTS
                    .Include(op => op.PRODUCT)
                    .Include(op => op.UNIT)
                    .Where(op => op.OrderID == orderId)
                    .Select(op => new
                    {
                        ProductID = op.ProductID,
                        ProductName = op.PRODUCT != null ? op.PRODUCT.Name : "",
                        PNo = op.PRODUCT != null ? op.PRODUCT.PNo : "",
                        Qty = op.Amount,
                        UnitPrice = op.Price
                    })
                    .ToList();
                return Json(lines, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnoma va maxsulot bo'yicha narx
        [HttpGet]
        public ActionResult GetContractPriceForProduct(int contractId, int productId)
        {
            using (var db = new DBTHSNEntities())
            {
                var cp = db.S_CONTRACT_PRODUCTS
                    .FirstOrDefault(c => c.ContractID == contractId && c.ProductID == productId);
                if (cp == null)
                    return Json(new { PiecePrice = (decimal?)null }, JsonRequestBehavior.AllowGet);
                return Json(new { PiecePrice = (decimal)cp.PiecePrice }, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnoma valyutasi
        [HttpGet]
        public ActionResult GetContractCurrency(int contractId)
        {
            using (var db = new DBTHSNEntities())
            {
                var contract = db.S_CONTRACTS
                    .Include(c => c.CURRENCy)
                    .FirstOrDefault(c => c.ID == contractId);
                var code = contract?.CURRENCy?.Shortname ?? "UZS";
                return Json(new { CurrencyCode = code }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            // Biznes qoidasi: invoys yaratilganda status doim APPROVED.
            invoice.StatusCode = "APPROVED";
            if (ModelState.ContainsKey("StatusCode"))
            {
                ModelState["StatusCode"].Errors.Clear();
            }

            if (ModelState.IsValid && lines != null && lines.Any())
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Agar shartnoma/buyurtma tanlangan bo'lsa, mahsulotlar mosligini tekshirish
                        HashSet<int> contractProductIds = null;
                        HashSet<int> orderProductIds = null;

                        if (invoice.ContractID.HasValue)
                        {
                            contractProductIds = new HashSet<int>(
                                db.S_CONTRACT_PRODUCTS
                                    .Where(cp => cp.ContractID == invoice.ContractID.Value)
                                    .Select(cp => cp.ProductID)
                                    .ToList()
                            );
                        }

                        if (invoice.OrderID.HasValue)
                        {
                            orderProductIds = new HashSet<int>(
                                db.S_ORDER_PRODUCTS
                                    .Where(op => op.OrderID == invoice.OrderID.Value)
                                    .Select(op => op.ProductID)
                                    .ToList()
                            );
                        }

                        foreach (var line in lines)
                        {
                            if (contractProductIds != null && contractProductIds.Count > 0 && !contractProductIds.Contains(line.ProductID))
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("",
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} tanlangan shartnomada mavjud emas.");
                                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).ToList(), "ID", "Name", invoice.CustomerID);
                                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);
                                return View(invoice);
                            }

                            if (orderProductIds != null && orderProductIds.Count > 0 && !orderProductIds.Contains(line.ProductID))
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("",
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} tanlangan buyurtmada mavjud emas.");
                                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).ToList(), "ID", "Name", invoice.CustomerID);
                                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);
                                return View(invoice);
                            }
                        }

                        // Tekshirish sales warehouse stock uchun har bir line
                        var salesWH = WarehouseFlowService.GetSalesWarehouse(db);
                        if (salesWH == null)
                        {
                            ModelState.AddModelError("", "Sotuv ombori topilmadi");
                            ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                            ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                            ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                            ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                            ViewBag.Products = BuildSalesWarehouseProductSelectList(db);
                            return View(invoice);
                        }

                        foreach (var line in lines)
                        {
                            var availableQty = InventoryValuationService.GetQty(db, salesWH.ID, line.ProductID);
                            if (line.Qty > (decimal)availableQty + 1e-9m)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("",
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun sotuv omborida yetarli qoldiq yo'q. " +
                                    $"Mavjud: {availableQty}, Talab: {line.Qty}");
                                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false).ToList(), "ID", "Name", invoice.CustomerID);
                                ViewBag.Orders = new SelectList(db.S_ORDERS.Where(o => o.IsDeleted == false).ToList(), "ID", "OrderNo", invoice.OrderID);
                                ViewBag.Contracts = new SelectList(db.S_CONTRACTS.Where(c => c.IsDeleted == false).ToList(), "ID", "ContractNo", invoice.ContractID);
                                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);
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
                        if (string.IsNullOrWhiteSpace(invoice.InvoiceNo))
                            invoice.InvoiceNo = GetNextInvoiceNo(db);

                        db.SALES_INVOICES.Add(invoice);
                        db.SaveChanges();

                        // Qo'shish lines
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

                        // Har doim sotuv omboridan chiqim qilamiz (invoys yaratilganda avtomatik expense + stock kamayish)
                        try
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
                                        invoice.ID,
                                        invoice.InvoiceNo ?? invoice.ID.ToString(),
                                        invoice.InvoiceDate,
                                        invoice.ContractID
                                    );
                                }
                                catch (Exception stockEx)
                                {
                                    LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Create",
                                        $"Stock decrease error for product {line.ProductID} in invoice {invoice.ID}: {stockEx.Message}");
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
                                $"Stock decrease error for invoice {invoice.ID}: {ex.Message}");
                            if (ex.Message.Contains("yetarli mahsulot yo'q"))
                            {
                                throw;
                            }
                        }

                        // Biznes qoidasi: invoys yaratilganda darhol finance posting qilinadi
                        try
                        {
                            var userId = UserHelper.GetUserId(User.Identity.Name);
                            FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Create",
                                $"Finance posting error for invoice {invoice.ID}: {ex.Message}");
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoicesController", $"{invoice.InvoiceNo} - Invoysni yaratdi");

                        return RedirectToAction("Details", new { id = invoice.ID });
                    }
                    catch (DbEntityValidationException dbEx)
                    {
                        var validationErrors = new List<string>();
                        foreach (var entityErrors in dbEx.EntityValidationErrors)
                        {
                            foreach (var error in entityErrors.ValidationErrors)
                            {
                                validationErrors.Add($"Maydon: {error.PropertyName}, Xatolik: {error.ErrorMessage}");
                            }
                        }

                        var errorMessage = "Ma'lumotlar validatsiyadan o'tmadi. Quyidagi maydonlarni tekshiring: "
                            + string.Join("; ", validationErrors);
                        ModelState.AddModelError("", errorMessage);
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

            if (lines == null || !lines.Any())
            {
                ModelState.AddModelError("", "Kamida bitta maxsulot qo'shish kerak. Mahsulot tanlang, miqdor va QQS % kiriting.");
            }

            // Reload ViewBag on xatolik
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.Name).ToList(), "ID", "Name", invoice.CustomerID);
                var contractsList = db.S_CONTRACTS.Where(c => c.CustomerID == invoice.CustomerID && (c.IsDeleted == false || c.IsDeleted == null)).OrderBy(c => c.ContractNo).ToList();
                var contractsSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                contractsSelect.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == invoice.ContractID }));
                ViewBag.Contracts = new SelectList(contractsSelect, "Value", "Text", invoice.ContractID?.ToString() ?? "");
                var ordersList = invoice.ContractID.HasValue ? db.S_ORDERS.Where(o => o.ContractID == invoice.ContractID && (o.IsDeleted == false || o.IsDeleted == null)).OrderByDescending(o => o.IssuedDate).ToList() : new List<S_ORDERS>();
                var ordersSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                ordersSelect.AddRange(ordersList.Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == invoice.OrderID }));
                ViewBag.Orders = new SelectList(ordersSelect, "Value", "Text", invoice.OrderID?.ToString() ?? "");
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = BuildSalesWarehouseProductSelectList(db, lines?.FirstOrDefault()?.ProductID);
            }

            return View(invoice);
        }

        private SelectList BuildSalesWarehouseProductSelectList(DBTHSNEntities db, int? selectedProductId = null)
        {
            var salesWH = WarehouseFlowService.GetSalesWarehouse(db);
            if (salesWH == null)
            {
                return new SelectList(new List<SelectListItem>(), "Value", "Text", selectedProductId);
            }

            // Sales invoice uchun mahsulotlar (qat'iy qoida):
            // 1) Savdo omborida qoldiq > 0
            // 2) O'chirilmagan
            // 3) IsFinishedGood == true (PRODUCT yoki CATEGORY)
            // 4) IsRawMaterial == true bo'lsa ko'rsatilmaydi (PRODUCT yoki CATEGORY)
            var productIds = db.PRODUCTS
                .Where(p => p.IsDeleted == false
                    && (p.IsFinishedGood == true
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true))
                    && (p.IsRawMaterial != true)
                    && (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsRawMaterial != true))
                .Select(p => p.ID)
                .ToList();

            var selectItems = productIds
                .Select(pid =>
                {
                    var p = db.PRODUCTS.Find(pid);
                    var qty = InventoryValuationService.GetQty(db, salesWH.ID, pid);
                    return new { Product = p, Qty = qty };
                })
                .Where(x => x.Qty > 0 && x.Product != null)
                .OrderBy(x => x.Product.PNo)
                .Select(x => new SelectListItem
                {
                    Value = x.Product.ID.ToString(),
                    Text = $"{x.Product.PNo} - {x.Product.Name} (Mavjud: {x.Qty:N2})"
                })
                .ToList();

            return new SelectList(selectItems, "Value", "Text", selectedProductId);
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
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Tekshirish agar invoice can be edited (business rule)
                if (invoice.StatusCode == "FINALIZED" || invoice.StatusCode == "PAID" || invoice.StatusCode == "CANCELED")
                {
                    TempData["ErrorMessage"] = "Yakunlangan/to'langan/bekor qilingan invoysni tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.Name).ToList(), "ID", "Name", invoice.CustomerID);
                var contractsList = db.S_CONTRACTS.Where(c => c.CustomerID == invoice.CustomerID && (c.IsDeleted == false || c.IsDeleted == null)).OrderBy(c => c.ContractNo).ToList();
                var contractsSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                contractsSelect.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == invoice.ContractID }));
                ViewBag.Contracts = new SelectList(contractsSelect, "Value", "Text", invoice.ContractID?.ToString() ?? "");
                var ordersList = invoice.ContractID.HasValue
                    ? db.S_ORDERS.Where(o => o.ContractID == invoice.ContractID && (o.IsDeleted == false || o.IsDeleted == null)).OrderByDescending(o => o.IssuedDate).ToList()
                    : new List<S_ORDERS>();
                var ordersSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                ordersSelect.AddRange(ordersList.Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == invoice.OrderID }));
                ViewBag.Orders = new SelectList(ordersSelect, "Value", "Text", invoice.OrderID?.ToString() ?? "");
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);

                return View(invoice);
            }
        }

        // POST so'rovi: Edit
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

                    // Tekshirish agar invoice can be edited
                    if (existingInvoice.StatusCode == "FINALIZED" || existingInvoice.StatusCode == "PAID" || existingInvoice.StatusCode == "CANCELED")
                    {
                        ModelState.AddModelError("", "Yakunlangan/to'langan/bekor qilingan invoysni tahrirlash mumkin emas");
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
                        existingInvoice.UpdatedDT = DateTime.Now;

                        // O'chirish existing lines
                        var existingLines = db.SALES_INVOICE_LINES.Where(l => l.InvoiceID == invoice.ID).ToList();
                        foreach (var line in existingLines)
                        {
                            db.SALES_INVOICE_LINES.Remove(line);
                        }

                        // Qo'shish updated lines
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

                        // Biznes qoidasi: yaratilgan/tahrirlangan invoys doim approved oqimida yuradi
                        existingInvoice.StatusCode = "APPROVED";

                        db.SaveChanges();

                        // Editdan keyin finance postingni yangilaymiz (upsert)
                        try
                        {
                            var userId = UserHelper.GetUserId(User.Identity.Name);
                            FinancePostingService.PostSalesInvoice((int)existingInvoice.ID, db, userId);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", "Edit",
                                $"Finance posting update error for invoice {existingInvoice.ID}: {ex.Message}");
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

            // Reload ViewBag on xatolik (Edit - cascade format)
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ViewBag.Customers = new SelectList(db.CUSTOMERS.Where(c => c.IsDeleted == false || c.IsDeleted == null).OrderBy(c => c.Name).ToList(), "ID", "Name", invoice.CustomerID);
                var contractsList = db.S_CONTRACTS.Where(c => c.CustomerID == invoice.CustomerID && (c.IsDeleted == false || c.IsDeleted == null)).OrderBy(c => c.ContractNo).ToList();
                var contractsSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                contractsSelect.AddRange(contractsList.Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == invoice.ContractID }));
                ViewBag.Contracts = new SelectList(contractsSelect, "Value", "Text", invoice.ContractID?.ToString() ?? "");
                var ordersList = invoice.ContractID.HasValue ? db.S_ORDERS.Where(o => o.ContractID == invoice.ContractID && (o.IsDeleted == false || o.IsDeleted == null)).OrderByDescending(o => o.IssuedDate).ToList() : new List<S_ORDERS>();
                var ordersSelect = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
                ordersSelect.AddRange(ordersList.Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == invoice.OrderID }));
                ViewBag.Orders = new SelectList(ordersSelect, "Value", "Text", invoice.OrderID?.ToString() ?? "");
                ViewBag.Currencies = new SelectList(db.CURRENCIES.ToList(), "Shortname", "Shortname", invoice.CurrencyCode);
                ViewBag.Products = BuildSalesWarehouseProductSelectList(db);
            }

            return View(invoice);
        }

        // Olish so'rovi: SalesInvoices/Delete/5
        public ActionResult Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                return View(invoice);
            }
        }

        // POST so'rovi: SalesInvoices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES.FirstOrDefault(i => i.ID == id);
                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Paid invoysni bekor qilishga ruxsat bermaymiz
                if (invoice.StatusCode == "PAID")
                {
                    TempData["ErrorMessage"] = "To'langan invoysni o'chirib bo'lmaydi.";
                    return RedirectToAction("Details", new { id });
                }

                invoice.StatusCode = "CANCELED";
                invoice.UpdatedDT = DateTime.Now;

                // Invoysga bog'langan auto-expense larni fizik o'chirish + stock rollback
                WarehouseFlowService.RollbackSalesInvoiceStockAndDeleteAutoExpenses(id, db, User.Identity.Name);

                // Auto-post qilingan finance yozuvini ham bekor qilamiz
                FinancePostingService.RemoveSalesInvoicePosting((int)id, db);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "SalesInvoices", $"Delete - {invoice.InvoiceNo} invoysi bekor qilindi");
                return RedirectToAction("Index");
            }
        }

        // yordamchi method ga get current user ID
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

        // Olish so'rovi: SalesInvoices/Print/5
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

                // Yuklash customer
                ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);

                // Olish related order va contract
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

                // Yuklash products uchun invoice lines
                var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS
                    .Include(p => p.UNIT)
                    .Where(p => productIDs.Contains(p.ID))
                    .ToDictionary(p => p.ID, p => p);

                // Yuklash company info
                ViewBag.Company = db.COMPANIES.FirstOrDefault();

                return View(invoice);
            }
        }
    }

    // ViewModel uchun invoice lines
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

