using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.Services.Crm.EDocuments;
using Newtonsoft.Json;
using tahsinERP.ViewModels.Sales;

namespace tahsinERP.Controllers
{
    [Permission]
    public class SalesTTNKController : BaseController
    {
        // Olish so'rovi: SalesTTNK
        public async Task<ActionResult> Index(string searchTerm, string ttnkNumber, long? invoiceID, int? orderID, int? customerID, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // SALES_TTNK_INVOICES → SALES_INVOICES view'da ishlatiladi — kontekst yopilgandan keyin
                // lazy-load yiqilmasligi uchun oldindan yuklanadi (CRM-B14)
                var query = db.SALES_TTNK_HEADERS.AsNoTracking()
                    .Include(t => t.SALES_INVOICES)
                    .Include(t => t.SALES_TTNK_INVOICES.Select(ti => ti.SALES_INVOICES))
                    .AsQueryable();

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

                switch (sortOrder)
                {
                    case "ttnkNumber_desc":
                        query = query.OrderByDescending(t => t.TTNKNumber);
                        break;
                    case "ttnkNumber":
                        query = query.OrderBy(t => t.TTNKNumber);
                        break;
                    case "date":
                        query = query.OrderBy(t => t.TTNKDate);
                        break;
                    default:
                        query = query.OrderByDescending(t => t.TTNKDate);
                        break;
                }

                // Server tomonida sahifalash (CRM-P05, umumiy blok — D16)
                var ttnkHeaders = await query.ToPageAsync(page, pageSize, ViewData);

                // Filtr ro'yxatlari: proyeksiya, faqat faol invoyslar (CRM-P04: avval barcha SALES_INVOICES to'liq yuklanardi)
                ViewBag.Customers = SalesLookups.Customers(customerID);
                ViewBag.Invoices = new SelectList(await db.SALES_INVOICES.AsNoTracking().Active()
                    .OrderByDescending(i => i.InvoiceDate)
                    .Select(i => new { i.ID, i.InvoiceNo })
                    .ToListAsync(), "ID", "InvoiceNo", invoiceID);
                ViewBag.Orders = new SelectList(await db.S_ORDERS.AsNoTracking()
                    .Where(o => o.IsDeleted == false || o.IsDeleted == null)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo })
                    .ToListAsync(), "ID", "OrderNo", orderID);
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

        // Olish so'rovi: Details
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

                if (ttnkHeader.OrderID.HasValue)
                {
                    ViewBag.Order = db.S_ORDERS
                        .Include(o => o.CUSTOMER)
                        .FirstOrDefault(o => o.ID == ttnkHeader.OrderID.Value);
                }

                if (ttnkHeader.SALES_INVOICES != null)
                {
                    ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == ttnkHeader.SALES_INVOICES.CustomerID);
                }

                var productIDs = ttnkHeader.SALES_TTNK_LINES.Where(l => l.ProductID.HasValue).Select(l => l.ProductID.Value).Distinct().ToList();
                ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);
                ViewBag.EDocument = EDocumentService.Panel(db, ttnkHeader,
                    RoleHelper.IsPermittedForAction(CurrentUserName, "SalesTTNK", "SendEDocument", true));

                return View(ttnkHeader);
            }
        }

        // Olish so'rovi: yaratish dan Invoice(s) — kirish nuqtasi: invoys Details sahifasidagi "TTNK yaratish"
        public ActionResult CreateFromInvoices(string invoiceIDs)
        {
            var invoiceIDList = ParseIds(invoiceIDs);
            if (invoiceIDList == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invoys ID lari noto'g'ri yoki ko'rsatilmagan");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoices = LoadInvoices(db, invoiceIDList);
                if (invoices.Count != invoiceIDList.Count)
                {
                    return HttpNotFound();
                }

                if (invoices.Select(i => i.CustomerID).Distinct().Count() > 1)
                {
                    TempData["ErrorMessage"] = "Barcha invoyslar bir xil mijozga tegishli bo'lishi kerak";
                    return RedirectToAction("Index", "SalesInvoices");
                }

                var firstInvoice = invoices.First();
                var ttnkHeader = new SALES_TTNK_HEADERS
                {
                    TTNKDate = DateTime.Now,
                    InvoiceID = firstInvoice.ID,
                    OrderID = firstInvoice.OrderID,
                    ContractID = firstInvoice.ContractID,
                    StatusCode = "DRAFT",
                    CreatedDT = DateTime.Now,
                    IntegrationUID = Guid.NewGuid(),
                    IntegrationStatusCode = EDocumentStatus.Local,
                    ShipperName = "",
                    ConsigneeName = firstInvoice.CUSTOMER?.Name ?? "",
                    ConsigneeINN = firstInvoice.CUSTOMER?.DUNS ?? "",
                    CurrencyCode = SalesLookups.CurrencyCode(firstInvoice.CurrencyID)
                };

                PopulateCreateLookups(db, invoices, invoiceIDs);
                return View(ttnkHeader);
            }
        }

        // POST so'rovi: yaratish dan Invoices
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateFromInvoices(SALES_TTNK_HEADERS ttnkHeader, string invoiceIDs, List<SalesTTNKLineViewModel> lines)
        {
            var invoiceIDList = ParseIds(invoiceIDs);
            if (invoiceIDList == null)
            {
                ModelState.AddModelError("", "Invoys ID lari noto'g'ri yoki ko'rsatilmagan");
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var invoices = db.SALES_INVOICES.Active()
                            .Where(i => invoiceIDList.Contains(i.ID))
                            .ToList();

                        if (invoices.Count != invoiceIDList.Count)
                        {
                            ModelState.AddModelError("", "Invoyslar topilmadi yoki bekor qilingan");
                        }
                        else if (invoices.Select(i => i.CustomerID).Distinct().Count() > 1)
                        {
                            ModelState.AddModelError("", "Barcha invoyslar bir xil mijozga tegishli bo'lishi kerak");
                        }
                        else
                        {
                            var first = invoices.OrderBy(i => invoiceIDList.IndexOf(i.ID)).First();
                            ttnkHeader.TTNKDate = DateTime.Now;
                            ttnkHeader.InvoiceID = first.ID;
                            ttnkHeader.OrderID = first.OrderID;
                            ttnkHeader.ContractID = first.ContractID;
                            ttnkHeader.StatusCode = "DRAFT";
                            ttnkHeader.CreatedDT = DateTime.Now;
                            ttnkHeader.IntegrationUID = Guid.NewGuid();
                            // NOT NULL ustun (bazada DEFAULT 'LOCAL', lekin EF null yuborib validatsiyada yiqilardi)
                            ttnkHeader.IntegrationStatusCode = EDocumentStatus.Local;

                            var customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == first.CustomerID);
                            if (customer != null)
                            {
                                ttnkHeader.ConsigneeName = customer.Name;
                                ttnkHeader.ConsigneeINN = customer.DUNS;
                            }

                            ttnkHeader.TotalDeliveryCost = ttnkHeader.DeliveryCost + ttnkHeader.VATAmount;

                            foreach (var invoice in invoices)
                            {
                                ttnkHeader.SALES_TTNK_INVOICES.Add(new SALES_TTNK_INVOICES
                                {
                                    InvoiceID = invoice.ID,
                                    AmountShare = invoice.TotalAmount
                                });
                            }

                            int lineNo = 1;
                            foreach (var line in lines ?? new List<SalesTTNKLineViewModel>())
                            {
                                ttnkHeader.SALES_TTNK_LINES.Add(new SALES_TTNK_LINES
                                {
                                    LineNo = lineNo++,
                                    ProductID = line.ProductID,
                                    InvoiceLineID = line.InvoiceLineID,
                                    HSCode = line.HSCode,
                                    Description = line.Description,
                                    UnitOfMeasure = line.UnitOfMeasure,
                                    Qty = line.Qty,
                                    UnitPrice = line.UnitPrice,
                                    LineAmount = line.UnitPrice.HasValue ? line.Qty * line.UnitPrice.Value : line.LineAmount,
                                    WeightNetT = line.WeightNetT,
                                    WeightGrossT = line.WeightGrossT,
                                    PlaceCount = line.PlaceCount,
                                    PackageType = line.PackageType,
                                    Remark = line.Remark
                                });
                            }

                            // Sarlavha, bog'lanishlar va qatorlar bitta tranzaksiyada; raqam bo'sh bo'lsa — takrorlanmas avtomatik (CRM-B17)
                            using (var tx = db.Database.BeginTransaction())
                            {
                                if (string.IsNullOrWhiteSpace(ttnkHeader.TTNKNumber))
                                {
                                    ttnkHeader.TTNKNumber = DocumentNumberService.Next(db, "SALES_TTNK_HEADERS", "TTNKNumber", $"TTNK-{DateTime.Now:yyyy}-", 4);
                                }
                                db.SALES_TTNK_HEADERS.Add(ttnkHeader);
                                db.SaveChanges();
                                tx.Commit();
                            }

                            LogHelper.LogToDatabase(CurrentUserName, "SalesTTNKController", $"{ttnkHeader.TTNKNumber} - TTNK yaratdi");

                            return RedirectToAction("Details", new { id = ttnkHeader.ID });
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
                    }
                }
            }

            // Barcha xato yo'llarida ro'yxatlar qayta quriladi (CRM-B15)
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                PopulateCreateLookups(db, invoiceIDList != null ? LoadInvoices(db, invoiceIDList) : new List<SALES_INVOICES>(), invoiceIDs);
            }

            return View(ttnkHeader);
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
                var ttnkHeader = db.SALES_TTNK_HEADERS
                    .Include(t => t.SALES_TTNK_LINES)
                    .Include(t => t.SALES_TTNK_INVOICES)
                    .FirstOrDefault(t => t.ID == id);

                if (ttnkHeader == null)
                {
                    return HttpNotFound();
                }

                if (ttnkHeader.StatusCode == "FINALIZED" || ttnkHeader.StatusCode == "SENT")
                {
                    TempData["ErrorMessage"] = "Yakunlangan yoki yuborilgan TTNK ni tahrirlash mumkin emas";
                    return RedirectToAction("Details", new { id = id });
                }

                return View(ttnkHeader);
            }
        }

        // POST so'rovi: Edit
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

                    if (existingTTNK.StatusCode == "FINALIZED" || existingTTNK.StatusCode == "SENT")
                    {
                        ModelState.AddModelError("", "Yakunlangan yoki yuborilgan TTNK ni tahrirlash mumkin emas");
                        return View(ttnkHeader);
                    }

                    try
                    {
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

                        if (lines != null && lines.Any())
                        {
                            db.SALES_TTNK_LINES.RemoveRange(db.SALES_TTNK_LINES.Where(l => l.TTNKHeaderID == ttnkHeader.ID).ToList());

                            int lineNo = 1;
                            foreach (var line in lines)
                            {
                                db.SALES_TTNK_LINES.Add(new SALES_TTNK_LINES
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
                                    LineAmount = line.UnitPrice.HasValue ? line.Qty * line.UnitPrice.Value : line.LineAmount,
                                    WeightNetT = line.WeightNetT,
                                    WeightGrossT = line.WeightGrossT,
                                    PlaceCount = line.PlaceCount,
                                    PackageType = line.PackageType,
                                    Remark = line.Remark
                                });
                            }
                        }

                        db.SaveChanges();

                        LogHelper.LogToDatabase(CurrentUserName, "SalesTTNKController", $"{existingTTNK.TTNKNumber} - TTNK ni tahrirladi");

                        return RedirectToAction("Details", new { id = ttnkHeader.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Xatolik: " + InnermostMessage(ex));
                    }
                }
            }

            return View(ttnkHeader);
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        /// <summary>"1,2,3" → ID ro'yxati; noto'g'ri qiymatda null (avval long.Parse 500 qaytarardi — CRM-B17).</summary>
        private static List<long> ParseIds(string invoiceIDs)
        {
            if (string.IsNullOrWhiteSpace(invoiceIDs))
                return null;

            var result = new List<long>();
            foreach (var part in invoiceIDs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                long id;
                if (!long.TryParse(part.Trim(), out id) || id <= 0)
                    return null;
                if (!result.Contains(id))
                    result.Add(id);
            }
            return result.Count > 0 ? result : null;
        }

        private static List<SALES_INVOICES> LoadInvoices(DBTHSNEntities db, List<long> ids)
        {
            return db.SALES_INVOICES.Active()
                .Include(i => i.CUSTOMER)
                .Include(i => i.SALES_INVOICE_LINES)
                .Where(i => ids.Contains(i.ID))
                .ToList();
        }

        private void PopulateCreateLookups(DBTHSNEntities db, List<SALES_INVOICES> invoices, string invoiceIDs)
        {
            var allInvoiceLines = invoices.SelectMany(i => i.SALES_INVOICE_LINES).OrderBy(l => l.InvoiceID).ThenBy(l => l.LineNo).ToList();
            var productIDs = allInvoiceLines.Select(l => l.ProductID).Distinct().ToList();

            ViewBag.Invoices = invoices;
            ViewBag.InvoiceIDs = invoiceIDs;
            ViewBag.InvoiceLines = allInvoiceLines;
            ViewBag.Products = db.PRODUCTS.Where(p => productIDs.Contains(p.ID)).ToDictionary(p => p.ID, p => p);
        }
        // ───────────────────────────── elektron hujjat (EDO) ─────────────────────────────

        /// <summary>Provayderga yuboriladigan hujjat ma'lumotlari va kamchiliklar (integratsiyani sozlash uchun).</summary>
        [HttpGet]
        public ActionResult EDocumentPayload(long id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var doc = EDocumentService.BuildWaybill(db, id);
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

        /// <summary>TTN'ni faol EDO provayderiga (Didox / Faktura.uz) yuborish. Ichki rejimda o'chiq.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendEDocument(long id)
        {
            var result = await EDocumentService.SendWaybillAsync(id, CurrentUserName);
            if (result.Success)
                TempData["EDocSuccess"] = "TTN yuborildi: " + EDocumentStatus.Title(result.Status) + (string.IsNullOrEmpty(result.ExternalId) ? "" : " (ID: " + result.ExternalId + ")");
            else
                TempData["EDocError"] = result.Message;
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RefreshEDocument(long id)
        {
            var result = await EDocumentService.RefreshAsync(EDocumentKind.Waybill, id);
            if (result.Success)
                TempData["EDocSuccess"] = "Holat: " + EDocumentStatus.Title(result.Status);
            else
                TempData["EDocError"] = result.Message;
            return RedirectToAction("Details", new { id });
        }
    }
}
