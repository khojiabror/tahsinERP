using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers
{
    public class WaybillController : Controller
    {
        // GET: Waybill
        public async Task<ActionResult> Index(string searchTerm, int? direction, int? forwarderId, int? contractId, 
            int? transportTypeId, DateTime? startDate, DateTime? endDate, string sortOrder, int? page, int? pageSize)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.F_WAYBILLS
                    .Include(w => w.F_CONTRACTS)
                    .Include(w => w.F_CONTRACTS.FORWARDER)
                    .Include(w => w.P_INVOICES)
                    .Include(w => w.F_TRANSPORT_TYPES)
                    .Include(w => w.CURRENCy)
                    .Where(w => w.IsDeleted == false);

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(w => 
                        w.WaybillNo.Contains(searchTerm) ||
                        (w.FromLocation != null && w.FromLocation.Contains(searchTerm)) ||
                        (w.ToLocation != null && w.ToLocation.Contains(searchTerm)) ||
                        (w.TruckNo != null && w.TruckNo.Contains(searchTerm)) ||
                        (w.DriverName != null && w.DriverName.Contains(searchTerm)));
                }

                if (direction.HasValue)
                {
                    query = query.Where(w => w.Direction == direction.Value);
                }

                if (forwarderId.HasValue)
                {
                    query = query.Where(w => w.ForwarderID == forwarderId.Value);
                }

                if (contractId.HasValue)
                {
                    query = query.Where(w => w.ContractID == contractId.Value);
                }

                if (transportTypeId.HasValue)
                {
                    query = query.Where(w => w.TransportTypeID == transportTypeId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(w => w.IssueDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(w => w.IssueDate <= endDate.Value);
                }

                // Sorting
                switch (sortOrder)
                {
                    case "date_desc":
                        query = query.OrderByDescending(w => w.IssueDate);
                        break;
                    case "date":
                        query = query.OrderBy(w => w.IssueDate);
                        break;
                    case "waybillno":
                        query = query.OrderBy(w => w.WaybillNo);
                        break;
                    case "waybillno_desc":
                        query = query.OrderByDescending(w => w.WaybillNo);
                        break;
                    default:
                        query = query.OrderByDescending(w => w.IssueDate);
                        break;
                }

                var waybills = await query.ToListAsync();

                // Prepare ViewBag for filters
                ViewBag.Forwarders = new SelectList(await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName", forwarderId);
                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractId);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.ToListAsync(), "ID", "TransportType", transportTypeId);
                ViewBag.Directions = new SelectList(new[]
                {
                    new { Value = "0", Text = "Kirim (Inbound)" },
                    new { Value = "1", Text = "Chiqim (Outbound)" },
                    new { Value = "2", Text = "Ichki (Internal)" }
                }, "Value", "Text", direction);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Direction = direction;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(waybills);
            }
        }

        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new WaybillCreateViewModel
                {
                    IssueDate = DateTime.Now,
                    Forwarders = db.FORWARDERS.Where(f => f.IsDeleted == false)
                        .Select(f => new SelectListItem { Value = f.ID.ToString(), Text = f.ForwarderName })
                        .ToList(),
                    Contracts = db.F_CONTRACTS.Where(c => c.IsDeleted == false)
                        .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo })
                        .ToList(),
                    TransportTypes = db.F_TRANSPORT_TYPES
                        .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TransportType })
                        .ToList(),
                    PurchaseInvoices = db.P_INVOICES.Where(i => i.IsDeleted == false)
                        .Select(i => new SelectListItem { Value = i.ID.ToString(), Text = i.InvoiceNo })
                        .ToList(),
                    SalesInvoices = db.SALES_INVOICES
                        .Select(i => new SelectListItem { Value = i.ID.ToString(), Text = i.InvoiceNo })
                        .ToList(),
                    SalesOrders = db.S_ORDERS.Where(o => o.IsDeleted == false)
                        .Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo })
                        .ToList(),
                    PurchaseOrders = db.P_ORDERS.Where(o => o.IsDeleted == false)
                        .Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo })
                        .ToList(),
                    TTNKs = db.SALES_TTNK_HEADERS
                        .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TTNKNumber })
                        .ToList(),
                    Currencies = db.CURRENCIES
                        .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Shortname })
                        .ToList()
                };

                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(WaybillCreateViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    // Calculate amount if not manually set
                    double? calculatedAmount = CalculateAmount(viewModel);
                    if (!viewModel.Amount.HasValue && calculatedAmount.HasValue)
                    {
                        viewModel.Amount = calculatedAmount;
                    }

                    var waybill = new F_WAYBILLS
                    {
                        WaybillNo = viewModel.WaybillNo,
                        IssueDate = viewModel.IssueDate,
                        Direction = viewModel.Direction,
                        ForwarderID = viewModel.ForwarderID,
                        ContractID = viewModel.ContractID,
                        TransportTypeID = viewModel.TransportTypeID,
                        FromLocation = viewModel.FromLocation,
                        ToLocation = viewModel.ToLocation,
                        DistanceKm = viewModel.DistanceKm,
                        GrWeight = viewModel.GrWeight,
                        NetWeight = viewModel.NetWeight,
                        CBM = viewModel.CBM,
                        PalletsCount = viewModel.PalletsCount,
                        FreightBasis = viewModel.FreightBasis,
                        Rate = viewModel.Rate,
                        Amount = viewModel.Amount,
                        CurrencyID = viewModel.CurrencyID,
                        PayerType = viewModel.PayerType,
                        PayerID = viewModel.PayerID,
                        InvoiceID = viewModel.InvoiceID,
                        SalesInvoiceID = viewModel.SalesInvoiceID,
                        SalesOrderID = viewModel.SalesOrderID,
                        PurchaseOrderID = viewModel.PurchaseOrderID,
                        TTNKID = viewModel.TTNKID,
                        TruckNo = viewModel.TruckNo,
                        TrailerNo = viewModel.TrailerNo,
                        TruckModel = viewModel.TruckModel,
                        TrailerModel = viewModel.TrailerModel,
                        DriverName = viewModel.DriverName,
                        DriverPINFL = viewModel.DriverPINFL,
                        PlannedDeliveryDate = viewModel.PlannedDeliveryDate,
                        ActualDeliveryDate = viewModel.ActualDeliveryDate,
                        Description = viewModel.Description,
                        ManualCorrectionReason = viewModel.ManualCorrectionReason,
                        IsDeleted = false
                    };

                    db.F_WAYBILLS.Add(waybill);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "Create", 
                        $"{waybill.WaybillNo} - Waybillni yaratdi");

                    return RedirectToAction("Index");
                }

                // Repopulate dropdowns on validation error
                viewModel.Forwarders = db.FORWARDERS.Where(f => f.IsDeleted == false)
                    .Select(f => new SelectListItem { Value = f.ID.ToString(), Text = f.ForwarderName })
                    .ToList();
                viewModel.Contracts = db.F_CONTRACTS.Where(c => c.IsDeleted == false)
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo })
                    .ToList();
                viewModel.TransportTypes = db.F_TRANSPORT_TYPES
                    .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TransportType })
                    .ToList();
                viewModel.Currencies = db.CURRENCIES
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Shortname })
                    .ToList();

                return View(viewModel);
            }
        }

        public async Task<ActionResult> Details(int? ID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybill = await db.F_WAYBILLS
                    .Include(w => w.F_CONTRACTS)
                    .Include(w => w.F_CONTRACTS.FORWARDER)
                    .Include(w => w.P_INVOICES)
                    .Include(w => w.F_TRANSPORT_TYPES)
                    .Include(w => w.CURRENCy)
                    .Include(w => w.F_WAYBILL_DOCS)
                    .Include(w => w.F_WAYBILL_PAYMENTS)
                    .FirstOrDefaultAsync(w => w.ID == ID);

                if (waybill == null)
                {
                    return HttpNotFound();
                }

                // Calculate payment summary
                var payments = await db.F_WAYBILL_PAYMENTS
                    .Where(p => p.WaybillID == ID && p.IsDeleted == false)
                    .ToListAsync();

                ViewBag.TotalPaid = payments.Sum(p => p.Payment_Amount);
                ViewBag.Outstanding = (waybill.Amount ?? 0) - payments.Sum(p => p.Payment_Amount);
                ViewBag.Payments = payments;

                return View(waybill);
            }
        }

        public async Task<ActionResult> Edit(int? ID)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybill = await db.F_WAYBILLS.FindAsync(ID);
                if (waybill == null)
                {
                    return HttpNotFound();
                }

                var viewModel = new WaybillCreateViewModel
                {
                    ID = waybill.ID,
                    WaybillNo = waybill.WaybillNo,
                    IssueDate = waybill.IssueDate,
                    Direction = waybill.Direction,
                    ForwarderID = waybill.ForwarderID,
                    ContractID = waybill.ContractID,
                    TransportTypeID = waybill.TransportTypeID,
                    FromLocation = waybill.FromLocation,
                    ToLocation = waybill.ToLocation,
                    DistanceKm = waybill.DistanceKm,
                    GrWeight = waybill.GrWeight,
                    NetWeight = waybill.NetWeight,
                    CBM = waybill.CBM,
                    PalletsCount = waybill.PalletsCount,
                    FreightBasis = waybill.FreightBasis,
                    Rate = waybill.Rate,
                    Amount = waybill.Amount,
                    CurrencyID = waybill.CurrencyID,
                    PayerType = waybill.PayerType,
                    PayerID = waybill.PayerID,
                    InvoiceID = waybill.InvoiceID,
                    SalesInvoiceID = waybill.SalesInvoiceID,
                    SalesOrderID = waybill.SalesOrderID,
                    PurchaseOrderID = waybill.PurchaseOrderID,
                    TTNKID = waybill.TTNKID,
                    TruckNo = waybill.TruckNo,
                    TrailerNo = waybill.TrailerNo,
                    TruckModel = waybill.TruckModel,
                    TrailerModel = waybill.TrailerModel,
                    DriverName = waybill.DriverName,
                    DriverPINFL = waybill.DriverPINFL,
                    PlannedDeliveryDate = waybill.PlannedDeliveryDate,
                    ActualDeliveryDate = waybill.ActualDeliveryDate,
                    Description = waybill.Description,
                    ManualCorrectionReason = waybill.ManualCorrectionReason,
                    Forwarders = db.FORWARDERS.Where(f => f.IsDeleted == false)
                        .Select(f => new SelectListItem { Value = f.ID.ToString(), Text = f.ForwarderName, Selected = f.ID == waybill.ForwarderID })
                        .ToList(),
                    Contracts = db.F_CONTRACTS.Where(c => c.IsDeleted == false)
                        .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == waybill.ContractID })
                        .ToList(),
                    TransportTypes = db.F_TRANSPORT_TYPES
                        .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TransportType, Selected = t.ID == waybill.TransportTypeID })
                        .ToList(),
                    Currencies = db.CURRENCIES
                        .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Shortname, Selected = c.ID == waybill.CurrencyID })
                        .ToList()
                };

                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(WaybillCreateViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var waybillToUpdate = await db.F_WAYBILLS.FindAsync(viewModel.ID);
                    if (waybillToUpdate != null)
                    {
                        // Calculate amount if not manually set
                        double? calculatedAmount = CalculateAmount(viewModel);
                        if (!viewModel.Amount.HasValue && calculatedAmount.HasValue)
                        {
                            viewModel.Amount = calculatedAmount;
                        }

                        waybillToUpdate.WaybillNo = viewModel.WaybillNo;
                        waybillToUpdate.IssueDate = viewModel.IssueDate;
                        waybillToUpdate.Direction = viewModel.Direction;
                        waybillToUpdate.ForwarderID = viewModel.ForwarderID;
                        waybillToUpdate.ContractID = viewModel.ContractID;
                        waybillToUpdate.TransportTypeID = viewModel.TransportTypeID;
                        waybillToUpdate.FromLocation = viewModel.FromLocation;
                        waybillToUpdate.ToLocation = viewModel.ToLocation;
                        waybillToUpdate.DistanceKm = viewModel.DistanceKm;
                        waybillToUpdate.GrWeight = viewModel.GrWeight;
                        waybillToUpdate.NetWeight = viewModel.NetWeight;
                        waybillToUpdate.CBM = viewModel.CBM;
                        waybillToUpdate.PalletsCount = viewModel.PalletsCount;
                        waybillToUpdate.FreightBasis = viewModel.FreightBasis;
                        waybillToUpdate.Rate = viewModel.Rate;
                        waybillToUpdate.Amount = viewModel.Amount;
                        waybillToUpdate.CurrencyID = viewModel.CurrencyID;
                        waybillToUpdate.PayerType = viewModel.PayerType;
                        waybillToUpdate.PayerID = viewModel.PayerID;
                        waybillToUpdate.InvoiceID = viewModel.InvoiceID;
                        waybillToUpdate.SalesInvoiceID = viewModel.SalesInvoiceID;
                        waybillToUpdate.SalesOrderID = viewModel.SalesOrderID;
                        waybillToUpdate.PurchaseOrderID = viewModel.PurchaseOrderID;
                        waybillToUpdate.TTNKID = viewModel.TTNKID;
                        waybillToUpdate.TruckNo = viewModel.TruckNo;
                        waybillToUpdate.TrailerNo = viewModel.TrailerNo;
                        waybillToUpdate.TruckModel = viewModel.TruckModel;
                        waybillToUpdate.TrailerModel = viewModel.TrailerModel;
                        waybillToUpdate.DriverName = viewModel.DriverName;
                        waybillToUpdate.DriverPINFL = viewModel.DriverPINFL;
                        waybillToUpdate.PlannedDeliveryDate = viewModel.PlannedDeliveryDate;
                        waybillToUpdate.ActualDeliveryDate = viewModel.ActualDeliveryDate;
                        waybillToUpdate.Description = viewModel.Description;
                        waybillToUpdate.ManualCorrectionReason = viewModel.ManualCorrectionReason;
                        waybillToUpdate.IsDeleted = false;

                        try
                        {
                            await db.SaveChangesAsync();

                            LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "Edit",
                                $"{waybillToUpdate.WaybillNo} - Waybillni tahrirladi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                }

                // Repopulate dropdowns
                viewModel.Forwarders = db.FORWARDERS.Where(f => f.IsDeleted == false)
                    .Select(f => new SelectListItem { Value = f.ID.ToString(), Text = f.ForwarderName })
                    .ToList();
                viewModel.Contracts = db.F_CONTRACTS.Where(c => c.IsDeleted == false)
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo })
                    .ToList();
                viewModel.TransportTypes = db.F_TRANSPORT_TYPES
                    .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TransportType })
                    .ToList();
                viewModel.Currencies = db.CURRENCIES
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Shortname })
                    .ToList();

                return View(viewModel);
            }
        }

        public async Task<ActionResult> Delete(int? Id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (Id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybill = await db.F_WAYBILLS
                    .Include(w => w.F_CONTRACTS)
                    .Include(w => w.F_TRANSPORT_TYPES)
                    .Include(w => w.P_INVOICES)
                    .FirstOrDefaultAsync(w => w.ID == Id);

                if (waybill == null)
                {
                    return HttpNotFound();
                }

                return View(waybill);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection gfs)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var waybillToDelete = await db.F_WAYBILLS.FindAsync(ID);
                    if (waybillToDelete != null)
                    {
                        waybillToDelete.IsDeleted = true;
                        try
                        {
                            db.Entry(waybillToDelete).State = EntityState.Modified;
                            await db.SaveChangesAsync();

                            LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "Delete",
                                $"{waybillToDelete.WaybillNo} - Waybillni o'chirdi");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Bunday waybill topilmadi.");
                    }
                }
            }
            return View();
        }

        /// <summary>
        /// Calculate freight amount based on basis and rate
        /// </summary>
        private double? CalculateAmount(WaybillCreateViewModel viewModel)
        {
            if (!viewModel.Rate.HasValue || !viewModel.FreightBasis.HasValue)
                return null;

            double rate = viewModel.Rate.Value;
            byte basis = viewModel.FreightBasis.Value;

            switch (basis)
            {
                case 0: // PerTruck
                    return rate;
                case 1: // PerTon
                    return viewModel.GrWeight.HasValue ? (double?)(rate * viewModel.GrWeight.Value) : (double?)null;
                case 2: // PerKm
                    return viewModel.DistanceKm.HasValue ? (double?)(rate * viewModel.DistanceKm.Value) : (double?)null;
                case 3: // Flat
                    return rate;
                default:
                    return null;
            }
        }

        /// <summary>
        /// AJAX: Get suggested rate from contract tariffs
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> GetSuggestedRate(int? contractId, int? transportTypeId, string fromLocation, string toLocation)
        {
            if (!contractId.HasValue || !transportTypeId.HasValue)
            {
                return Json(new { success = false, message = "Contract and Transport Type are required" });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Try to find matching tariff (this would require F_CONTRACT_TARIFFS table)
                // For now, return contract base rate
                var contract = await db.F_CONTRACTS.FindAsync(contractId.Value);
                if (contract != null)
                {
                    return Json(new { success = true, rate = contract.Amount, currencyId = contract.CurrencyID });
                }

                return Json(new { success = false, message = "Contract not found" });
            }
        }

        /// <summary>
        /// AJAX: Auto-fill waybill data from invoice/order
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> GetDataFromInvoice(int? invoiceId, string invoiceType)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (invoiceType == "Purchase" && invoiceId.HasValue)
                {
                    var invoice = await db.P_INVOICES
                        .Include(i => i.SUPPLIER)
                        .FirstOrDefaultAsync(i => i.ID == invoiceId.Value);

                    if (invoice != null)
                    {
                        return Json(new
                        {
                            success = true,
                            fromLocation = invoice.SUPPLIER?.Address,
                            toLocation = "Our Warehouse",
                            direction = 0, // Inbound
                            currencyId = invoice.CurrencyID,
                            payerType = 0 // OurCompany
                        });
                    }
                }
                else if (invoiceType == "Sales" && invoiceId.HasValue)
                {
                    var invoice = await db.SALES_INVOICES
                        .Include(i => i.CUSTOMER)
                        .FirstOrDefaultAsync(i => i.ID == invoiceId.Value);

                    if (invoice != null)
                    {
                        return Json(new
                        {
                            success = true,
                            fromLocation = "Our Warehouse",
                            toLocation = invoice.CUSTOMER?.Address,
                            direction = 1, // Outbound
                            currencyId = invoice.CurrencyID,
                            payerType = 1 // Customer
                        });
                    }
                }

                return Json(new { success = false, message = "Invoice not found" });
            }
        }
    }
}
