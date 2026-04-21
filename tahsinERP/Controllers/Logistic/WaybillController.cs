using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using OfficeOpenXml;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers
{
    public class WaybillController : Controller
    {
        private const byte WaybillPaymentSplitPayerType = 99;
        // Olish so'rovi: Waybill
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
                    .Include(w => w.F_TRANSPORT_TYPES)
                    .Include(w => w.CURRENCy)
                    .Include(w => w.FORWARDER)
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

                var waybillList = await query.ToListAsync();

                var waybillIds = waybillList.Select(w => w.ID).ToList();
                var paymentLookup = BuildWaybillPaidLookup(db, waybillIds);

                var waybills = waybillList.Select(w =>
                {
                    var totalPaid = paymentLookup.ContainsKey(w.ID) ? paymentLookup[w.ID] : 0;
                    var amount = w.Amount ?? 0;
                    var outstanding = amount - totalPaid;
                    var paymentStatus = outstanding <= 0 ? "To'landi" : outstanding < amount ? "Qisman" : "To'lanmagan";

                    return new WaybillIndexItemViewModel
                    {
                        ID = w.ID,
                        WaybillNo = w.WaybillNo,
                        IssueDate = w.IssueDate,
                        Direction = w.Direction,
                        ForwarderName = w.FORWARDER?.ForwarderName ?? w.F_CONTRACTS?.FORWARDER?.ForwarderName,
                        ContractNo = w.F_CONTRACTS?.ContractNo,
                        TransportType = w.F_TRANSPORT_TYPES?.TransportType,
                        FromLocation = w.FromLocation,
                        ToLocation = w.ToLocation,
                        TruckNo = w.TruckNo,
                        DriverName = w.DriverName,
                        DistanceKm = w.DistanceKm,
                        GrWeight = w.GrWeight,
                        CBM = w.CBM,
                        Amount = w.Amount,
                        Currency = w.CURRENCy?.Shortname,
                        TotalPaid = totalPaid,
                        Outstanding = outstanding,
                        PaymentStatus = paymentStatus,
                        CargoTypeText = GetCargoTypeText(w),
                        LinkedDoc = GetLinkedDocText(w)
                    };
                }).ToList();

                // Tayyorlash ViewBag uchun filters
                ViewBag.Forwarders = new SelectList(await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName", forwarderId);
                ViewBag.Contracts = new SelectList(await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync(), "ID", "ContractNo", contractId);
                ViewBag.TransportTypes = new SelectList(await db.F_TRANSPORT_TYPES.ToListAsync(), "ID", "TransportType", transportTypeId);
                ViewBag.Directions = new SelectList(new[]
                {
                    new { Value = "0", Text = "Kiruvchi" },
                    new { Value = "1", Text = "Chiquvchi" },
                    new { Value = "2", Text = "Ichki" }
                }, "Value", "Text", direction);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Direction = direction;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(waybills);
            }
        }

        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = BuildWaybillViewModel(db);
                viewModel.IssueDate = DateTime.Now;

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
                if (viewModel.TariffID.HasValue)
                {
                    var selectedTariff = db.F_CONTRACT_TARIFFS
                        .FirstOrDefault(t => t.ID == viewModel.TariffID.Value && t.IsActive);
                    if (selectedTariff == null)
                    {
                        ModelState.AddModelError("TariffID", "Tanlangan tarif topilmadi yoki faol emas.");
                    }
                    else if (viewModel.ContractID.HasValue && selectedTariff.ContractID != viewModel.ContractID.Value)
                    {
                        ModelState.AddModelError("TariffID", "Tanlangan tarif ushbu shartnomaga tegishli emas.");
                    }
                    else
                    {
                        viewModel.TransportTypeID = selectedTariff.TransportTypeID;
                        viewModel.FreightBasis = selectedTariff.FreightBasis;
                        viewModel.Rate = selectedTariff.Rate;
                        if (!viewModel.CurrencyID.HasValue)
                        {
                            viewModel.CurrencyID = selectedTariff.CurrencyID;
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("TariffID", "Tarif tanlash majburiy.");
                }

                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(viewModel.WaybillNo))
                    {
                        viewModel.WaybillNo = GenerateNextWaybillNo(db);
                    }

                    // Calculate amount agar not manually set
                    double? calculatedAmount = CalculateAmount(viewModel);
                    if (!viewModel.Amount.HasValue && calculatedAmount.HasValue)
                    {
                        viewModel.Amount = calculatedAmount;
                    }

                    var contractForwarder = viewModel.ContractID.HasValue
                        ? db.F_CONTRACTS.Where(c => c.ID == viewModel.ContractID.Value).Select(c => (int?)c.ForwarderID).FirstOrDefault()
                        : null;

                    var waybill = new F_WAYBILLS
                    {
                        WaybillNo = viewModel.WaybillNo,
                        IssueDate = viewModel.IssueDate,
                        Direction = viewModel.Direction,
                        ForwarderID = viewModel.ForwarderID ?? contractForwarder,
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
                        $"{waybill.WaybillNo} - {LogisticsTerms.Waybill.ToLower()} yaratildi.");

                    return RedirectToAction("Index");
                }

                // Qayta ga'ldirish dropdowns on validation xatolik
                viewModel = BuildWaybillViewModel(db, viewModel);

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
                    .Include(w => w.FORWARDER)
                    .Include(w => w.F_WAYBILL_DOCS)
                    .Include(w => w.F_WAYBILL_PAYMENTS)
                    .FirstOrDefaultAsync(w => w.ID == ID);

                if (waybill == null)
                {
                    return HttpNotFound();
                }

                // Separate split-linked payments first to avoid complex nested EF expressions
                var splitPaymentIdsForWaybill = db.F_WAYBILL_PAYMENT_SPLITS
                    .Where(s => s.PayerType == WaybillPaymentSplitPayerType &&
                                s.WaybillID == waybill.ID &&
                                s.PayerID.HasValue)
                    .Select(s => s.PayerID.Value)
                    .Distinct()
                    .ToList();

                var allSplitPaymentIds = db.F_WAYBILL_PAYMENT_SPLITS
                    .Where(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID.HasValue)
                    .Select(s => s.PayerID.Value)
                    .Distinct()
                    .ToList();

                var directPaid = db.F_WAYBILL_PAYMENTS
                    .Where(p => p.IsDeleted == false &&
                                p.WaybillID == waybill.ID &&
                                !allSplitPaymentIds.Contains(p.ID))
                    .Sum(p => (double?)p.Payment_Amount) ?? 0;

                var splitPaid = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                                 join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                                 where s.PayerType == WaybillPaymentSplitPayerType &&
                                       s.WaybillID == waybill.ID &&
                                       p.IsDeleted == false
                                 select (double?)s.Amount).Sum() ?? 0;

                var totalPaid = directPaid + splitPaid;
                var outstanding = (waybill.Amount ?? 0) - totalPaid;
                var paymentStatus = outstanding <= 0
                    ? "To'langan"
                    : (totalPaid > 0 ? "Qisman" : "To'lanmagan");

                var linkedPayments = db.F_WAYBILL_PAYMENTS
                    .Where(p => p.IsDeleted == false &&
                                (p.WaybillID == waybill.ID || splitPaymentIdsForWaybill.Contains(p.ID)))
                    .Include(p => p.CURRENCy)
                    .Include(p => p.FORWARDER)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList();

                ViewBag.TotalPaid = totalPaid;
                ViewBag.Outstanding = outstanding;
                ViewBag.PaymentStatus = paymentStatus;
                ViewBag.Payments = linkedPayments;

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

                var tariffId = db.F_CONTRACT_TARIFFS
                    .Where(t => t.ContractID == waybill.ContractID && t.TransportTypeID == waybill.TransportTypeID && t.IsActive)
                    .OrderByDescending(t => t.CreatedDT)
                    .Select(t => (int?)t.ID)
                    .FirstOrDefault();

                var viewModel = new WaybillCreateViewModel
                {
                    ID = waybill.ID,
                    WaybillNo = waybill.WaybillNo,
                    IssueDate = waybill.IssueDate,
                    Direction = waybill.Direction,
                    ForwarderID = waybill.ForwarderID,
                    ContractID = waybill.ContractID,
                    TransportTypeID = waybill.TransportTypeID,
                    TariffID = tariffId,
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
                    ManualCorrectionReason = waybill.ManualCorrectionReason
                };

                viewModel = BuildWaybillViewModel(db, viewModel);

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
                if (viewModel.TariffID.HasValue)
                {
                    var selectedTariff = db.F_CONTRACT_TARIFFS
                        .FirstOrDefault(t => t.ID == viewModel.TariffID.Value && t.IsActive);
                    if (selectedTariff == null)
                    {
                        ModelState.AddModelError("TariffID", "Tanlangan tarif topilmadi yoki faol emas.");
                    }
                    else if (viewModel.ContractID.HasValue && selectedTariff.ContractID != viewModel.ContractID.Value)
                    {
                        ModelState.AddModelError("TariffID", "Tanlangan tarif ushbu shartnomaga tegishli emas.");
                    }
                    else
                    {
                        viewModel.TransportTypeID = selectedTariff.TransportTypeID;
                        viewModel.FreightBasis = selectedTariff.FreightBasis;
                        viewModel.Rate = selectedTariff.Rate;
                        if (!viewModel.CurrencyID.HasValue)
                        {
                            viewModel.CurrencyID = selectedTariff.CurrencyID;
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("TariffID", "Tarif tanlash majburiy.");
                }

                if (ModelState.IsValid)
                {
                    var waybillToUpdate = await db.F_WAYBILLS.FindAsync(viewModel.ID);
                    if (waybillToUpdate != null)
                    {
                        // Calculate amount agar not manually set
                        double? calculatedAmount = CalculateAmount(viewModel);
                        if (!viewModel.Amount.HasValue && calculatedAmount.HasValue)
                        {
                            viewModel.Amount = calculatedAmount;
                        }

                        waybillToUpdate.WaybillNo = viewModel.WaybillNo;
                        waybillToUpdate.IssueDate = viewModel.IssueDate;
                        waybillToUpdate.Direction = viewModel.Direction;
                        var contractForwarder = viewModel.ContractID.HasValue
                            ? db.F_CONTRACTS.Where(c => c.ID == viewModel.ContractID.Value).Select(c => (int?)c.ForwarderID).FirstOrDefault()
                            : null;

                        waybillToUpdate.ForwarderID = viewModel.ForwarderID ?? contractForwarder;
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
                                $"{waybillToUpdate.WaybillNo} - {LogisticsTerms.Waybill.ToLower()} tahrirlandi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                }

                // Qayta ga'ldirish dropdowns
                viewModel = BuildWaybillViewModel(db, viewModel);

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
                    .Include(w => w.FORWARDER)
                    .FirstOrDefaultAsync(w => w.ID == Id);

                if (waybill == null)
                {
                    return HttpNotFound();
                }

                return View(waybill);
            }
        }

        public ActionResult UploadExcel()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            return View();
        }

        public ActionResult DownloadTemplate()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Waybills");
                var headers = new[]
                {
                    "WaybillNo","IssueDate","Direction","ForwarderID","ForwarderName","ContractID","ContractNo",
                    "TransportTypeID","TransportType","FromLocation","ToLocation","DistanceKm","GrWeight","NetWeight",
                    "CBM","PalletsCount","FreightBasis","Rate","Amount","CurrencyID","Currency","PayerType","PayerID",
                    "InvoiceID","SalesInvoiceID","SalesOrderID","PurchaseOrderID","TTNKID","TruckNo","TrailerNo",
                    "TruckModel","TrailerModel","DriverName","DriverPINFL","PlannedDeliveryDate","ActualDeliveryDate",
                    "Description","ManualCorrectionReason"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                }

                worksheet.Cells[2, 1].Value = "WB-0001";
                worksheet.Cells[2, 2].Value = DateTime.Today;
                worksheet.Cells[2, 3].Value = "Kiruvchi";
                worksheet.Cells[2, 10].Value = "Supplier A";
                worksheet.Cells[2, 11].Value = "Warehouse B";
                worksheet.Cells[2, 12].Value = 25;
                worksheet.Cells[2, 13].Value = 12.5;
                worksheet.Cells[2, 18].Value = 150000;
                worksheet.Cells[2, 19].Value = 1875000;
                worksheet.Cells[2, 21].Value = "UZS";

                worksheet.Column(2).Style.Numberformat.Format = "yyyy-mm-dd";
                worksheet.Column(36).Style.Numberformat.Format = "yyyy-mm-dd";
                worksheet.Column(37).Style.Numberformat.Format = "yyyy-mm-dd";
                worksheet.Cells.AutoFitColumns();

                var bytes = package.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Waybill_Import_Template.xlsx");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadExcel(HttpPostedFileBase file)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (file == null || file.ContentLength == 0)
            {
                ViewBag.Error = "Excel fayl yuklanmadi.";
                return View();
            }

            var errors = new List<string>();
            var successCount = 0;
            var skipCount = 0;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var forwarders = await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync();
                var contracts = await db.F_CONTRACTS.Where(c => c.IsDeleted == false).ToListAsync();
                var transportTypes = await db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted == false || t.IsDeleted == null).ToListAsync();
                var currencies = await db.CURRENCIES.ToListAsync();
                var existingWaybillNos = new HashSet<string>(
                    db.F_WAYBILLS.Where(w => w.IsDeleted == false).Select(w => w.WaybillNo),
                    StringComparer.OrdinalIgnoreCase);
                var fileWaybillNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        ViewBag.Error = "Excel fayl bo'sh.";
                        return View();
                    }

                    var headerMap = BuildHeaderMap(worksheet);
                    if (!headerMap.ContainsKey("WaybillNo") || !headerMap.ContainsKey("IssueDate"))
                    {
                        ViewBag.Error = "Excel faylda majburiy ustunlar yo'q (WaybillNo, IssueDate).";
                        return View();
                    }

                    var lastRow = worksheet.Dimension.End.Row;
                    for (int row = 2; row <= lastRow; row++)
                    {
                        var waybillNo = GetCellString(worksheet, row, headerMap, "WaybillNo");
                        if (string.IsNullOrWhiteSpace(waybillNo))
                        {
                            skipCount++;
                            continue;
                        }
                        waybillNo = waybillNo.Trim();

                        if (fileWaybillNos.Contains(waybillNo))
                        {
                            errors.Add($"Qator {row}: Bu waybill raqami Excel faylida takrorlangan ({waybillNo}).");
                            continue;
                        }
                        fileWaybillNos.Add(waybillNo);

                        if (existingWaybillNos.Contains(waybillNo))
                        {
                            errors.Add($"Qator {row}: Bu waybill raqami avval kiritilgan ({waybillNo}).");
                            continue;
                        }

                        var issueDate = GetCellDate(worksheet, row, headerMap, "IssueDate");
                        if (!issueDate.HasValue)
                        {
                            errors.Add($"Qator {row}: sana noto'g'ri formatda.");
                            continue;
                        }

                        var contractId = GetCellInt(worksheet, row, headerMap, "ContractID");
                        var contractNo = GetCellString(worksheet, row, headerMap, "ContractNo");
                        var contract = contractId.HasValue
                            ? contracts.FirstOrDefault(c => c.ID == contractId.Value)
                            : (!string.IsNullOrWhiteSpace(contractNo) ? contracts.FirstOrDefault(c => c.ContractNo == contractNo) : null);

                        if (contract == null)
                        {
                            errors.Add($"Row {row}: Shartnoma topilmadi (ContractID/ContractNo).");
                            continue;
                        }

                        var forwarderId = GetCellInt(worksheet, row, headerMap, "ForwarderID");
                        var forwarderName = GetCellString(worksheet, row, headerMap, "ForwarderName");
                        var forwarder = forwarderId.HasValue
                            ? forwarders.FirstOrDefault(f => f.ID == forwarderId.Value)
                            : (!string.IsNullOrWhiteSpace(forwarderName) ? forwarders.FirstOrDefault(f => f.ForwarderName == forwarderName) : null);

                        var transportTypeId = GetCellInt(worksheet, row, headerMap, "TransportTypeID");
                        var transportTypeName = GetCellString(worksheet, row, headerMap, "TransportType");
                        var transportType = transportTypeId.HasValue
                            ? transportTypes.FirstOrDefault(t => t.ID == transportTypeId.Value)
                            : (!string.IsNullOrWhiteSpace(transportTypeName) ? transportTypes.FirstOrDefault(t => t.TransportType == transportTypeName) : null);

                        if (transportType == null)
                        {
                            errors.Add($"Row {row}: Transport turi topilmadi (TransportTypeID/TransportType).");
                            continue;
                        }

                        var currencyId = GetCellInt(worksheet, row, headerMap, "CurrencyID");
                        var currencyCode = GetCellString(worksheet, row, headerMap, "Currency");
                        var currency = currencyId.HasValue
                            ? currencies.FirstOrDefault(c => c.ID == currencyId.Value)
                            : (!string.IsNullOrWhiteSpace(currencyCode) ? currencies.FirstOrDefault(c => c.Shortname == currencyCode) : null);

                        var waybill = new F_WAYBILLS
                        {
                            WaybillNo = waybillNo,
                            IssueDate = issueDate.Value,
                            Direction = GetCellByte(worksheet, row, headerMap, "Direction", ParseDirection),
                            ForwarderID = forwarder?.ID ?? contract.ForwarderID,
                            ContractID = contract.ID,
                            TransportTypeID = transportType.ID,
                            FromLocation = GetCellString(worksheet, row, headerMap, "FromLocation"),
                            ToLocation = GetCellString(worksheet, row, headerMap, "ToLocation"),
                            DistanceKm = GetCellDouble(worksheet, row, headerMap, "DistanceKm"),
                            GrWeight = GetCellDouble(worksheet, row, headerMap, "GrWeight"),
                            NetWeight = GetCellDouble(worksheet, row, headerMap, "NetWeight"),
                            CBM = GetCellDouble(worksheet, row, headerMap, "CBM"),
                            PalletsCount = GetCellInt(worksheet, row, headerMap, "PalletsCount"),
                            FreightBasis = GetCellByte(worksheet, row, headerMap, "FreightBasis", ParseFreightBasis),
                            Rate = GetCellDouble(worksheet, row, headerMap, "Rate"),
                            Amount = GetCellDouble(worksheet, row, headerMap, "Amount"),
                            CurrencyID = currency?.ID,
                            PayerType = GetCellByte(worksheet, row, headerMap, "PayerType", ParsePayerType),
                            PayerID = GetCellInt(worksheet, row, headerMap, "PayerID"),
                            InvoiceID = GetCellInt(worksheet, row, headerMap, "InvoiceID"),
                            SalesInvoiceID = GetCellLong(worksheet, row, headerMap, "SalesInvoiceID"),
                            SalesOrderID = GetCellInt(worksheet, row, headerMap, "SalesOrderID"),
                            PurchaseOrderID = GetCellInt(worksheet, row, headerMap, "PurchaseOrderID"),
                            TTNKID = GetCellLong(worksheet, row, headerMap, "TTNKID"),
                            TruckNo = GetCellString(worksheet, row, headerMap, "TruckNo"),
                            TrailerNo = GetCellString(worksheet, row, headerMap, "TrailerNo"),
                            TruckModel = GetCellString(worksheet, row, headerMap, "TruckModel"),
                            TrailerModel = GetCellString(worksheet, row, headerMap, "TrailerModel"),
                            DriverName = GetCellString(worksheet, row, headerMap, "DriverName"),
                            DriverPINFL = GetCellString(worksheet, row, headerMap, "DriverPINFL"),
                            PlannedDeliveryDate = GetCellDate(worksheet, row, headerMap, "PlannedDeliveryDate"),
                            ActualDeliveryDate = GetCellDate(worksheet, row, headerMap, "ActualDeliveryDate"),
                            Description = GetCellString(worksheet, row, headerMap, "Description"),
                            ManualCorrectionReason = GetCellString(worksheet, row, headerMap, "ManualCorrectionReason"),
                            IsDeleted = false
                        };

                        db.F_WAYBILLS.Add(waybill);
                        successCount++;
                    }
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "UploadExcel",
                    $"{successCount} ta {LogisticsTerms.Waybill.ToLower()} Excel orqali yuklandi, {skipCount} ta qator o'tkazib yuborildi.");
            }

            ViewBag.SuccessCount = successCount;
            ViewBag.SkipCount = skipCount;
            ViewBag.Errors = errors;
            return View();
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
                                $"{waybillToDelete.WaybillNo} - {LogisticsTerms.Waybill.ToLower()} o'chirildi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Bunday {LogisticsTerms.Waybill.ToLower()} topilmadi.");
                    }
                }
            }
            return View();
        }

        /// <summary>
        /// Calculate freight amount based on basis va rate
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
        /// AJAX so'rovi: Get suggested rate dan contract tariffs
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> GetSuggestedRate(int? contractId, int? transportTypeId, string fromLocation, string toLocation)
        {
            if (!contractId.HasValue || !transportTypeId.HasValue)
            {
                return Json(new { success = false, message = "Shartnoma va transport turi majburiy." });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariffs = await db.F_CONTRACT_TARIFFS
                    .Include(t => t.CURRENCy)
                    .Where(t => t.ContractID == contractId.Value && t.IsActive)
                    .ToListAsync();

                var best = tariffs
                    .Where(t => t.TransportTypeID == transportTypeId.Value)
                    .Select(t => new
                    {
                        Tariff = t,
                        Score = MatchTariffScore(t, fromLocation, toLocation)
                    })
                    .Where(x => x.Score >= 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Tariff.CreatedDT)
                    .FirstOrDefault();

                if (best != null)
                {
                    return Json(new
                    {
                        success = true,
                        rate = best.Tariff.Rate,
                        basis = best.Tariff.FreightBasis,
                        currencyId = best.Tariff.CurrencyID,
                        minimumCharge = best.Tariff.MinimumCharge
                    });
                }

                // Fallback ga contract base amount
                var contract = await db.F_CONTRACTS.FindAsync(contractId.Value);
                if (contract != null)
                {
                    return Json(new { success = true, rate = contract.Amount, currencyId = contract.CurrencyID });
                }

                return Json(new { success = false, message = "Tarif topilmadi" });
            }
        }

        [HttpPost]
        public async Task<JsonResult> GetTariffsByContract(int? contractId)
        {
            if (!contractId.HasValue)
            {
                return Json(new { success = false, message = "Shartnoma IDsi kerak." });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariffs = await db.F_CONTRACT_TARIFFS
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Where(t => t.ContractID == contractId.Value && t.IsActive)
                    .OrderByDescending(t => t.CreatedDT)
                    .ToListAsync();

                var result = tariffs.Select(t => new
                {
                    id = t.ID,
                    text = FormatTariffDisplay(t),
                    rate = t.Rate,
                    basis = t.FreightBasis,
                    currencyId = t.CurrencyID,
                    transportTypeId = t.TransportTypeID,
                    minimumCharge = t.MinimumCharge
                }).ToList();

                return Json(new { success = true, items = result });
            }
        }

        /// <summary>
        /// AJAX so'rovi: Auto-fill waybill ma'lumot dan invoice/order
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
                            toLocation = "Bizning ombor",
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
                            fromLocation = "Bizning ombor",
                            toLocation = invoice.CUSTOMER?.Address,
                            direction = 1, // Outbound
                            currencyId = invoice.CurrencyID,
                            payerType = 1 // Customer
                        });
                    }
                }

                return Json(new { success = false, message = "Hisob-faktura topilmadi." });
            }
        }

        private static WaybillCreateViewModel BuildWaybillViewModel(DBTHSNEntities db, WaybillCreateViewModel model = null)
        {
            var viewModel = model ?? new WaybillCreateViewModel();

            viewModel.Forwarders = db.FORWARDERS.Where(f => f.IsDeleted == false)
                .Select(f => new SelectListItem { Value = f.ID.ToString(), Text = f.ForwarderName, Selected = f.ID == viewModel.ForwarderID })
                .ToList();
            viewModel.Contracts = db.F_CONTRACTS.Where(c => c.IsDeleted == false)
                .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo, Selected = c.ID == viewModel.ContractID })
                .ToList();
            viewModel.TransportTypes = db.F_TRANSPORT_TYPES
                .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TransportType, Selected = t.ID == viewModel.TransportTypeID })
                .ToList();
            viewModel.PurchaseInvoices = db.P_INVOICES.Where(i => i.IsDeleted == false)
                .Select(i => new SelectListItem { Value = i.ID.ToString(), Text = i.InvoiceNo, Selected = i.ID == viewModel.InvoiceID })
                .ToList();
            viewModel.SalesInvoices = db.SALES_INVOICES
                .Select(i => new SelectListItem { Value = i.ID.ToString(), Text = i.InvoiceNo, Selected = i.ID == viewModel.SalesInvoiceID })
                .ToList();
            viewModel.SalesOrders = db.S_ORDERS.Where(o => o.IsDeleted == false)
                .Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == viewModel.SalesOrderID })
                .ToList();
            viewModel.PurchaseOrders = db.P_ORDERS.Where(o => o.IsDeleted == false)
                .Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo, Selected = o.ID == viewModel.PurchaseOrderID })
                .ToList();
            viewModel.TTNKs = db.SALES_TTNK_HEADERS
                .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.TTNKNumber, Selected = t.ID == viewModel.TTNKID })
                .ToList();
            viewModel.Currencies = db.CURRENCIES
                .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Shortname, Selected = c.ID == viewModel.CurrencyID })
                .ToList();

            viewModel.Tariffs = new List<SelectListItem>();
            if (viewModel.ContractID.HasValue)
            {
                var tariffs = db.F_CONTRACT_TARIFFS
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Where(t => t.ContractID == viewModel.ContractID.Value && t.IsActive)
                    .OrderByDescending(t => t.CreatedDT)
                    .ToList();
                
                viewModel.Tariffs = tariffs.Select(t => new SelectListItem
                {
                    Value = t.ID.ToString(),
                    Text = FormatTariffDisplay(t),
                    Selected = viewModel.TariffID.HasValue && t.ID == viewModel.TariffID.Value
                }).ToList();
            }

            return viewModel;
        }

        private static int MatchTariffScore(F_CONTRACT_TARIFFS tariff, string fromLocation, string toLocation)
        {
            var score = 0;
            if (!string.IsNullOrWhiteSpace(tariff.FromCity))
            {
                if (string.IsNullOrWhiteSpace(fromLocation) || !fromLocation.Equals(tariff.FromCity, StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }
                score += 4;
            }
            if (!string.IsNullOrWhiteSpace(tariff.ToCity))
            {
                if (string.IsNullOrWhiteSpace(toLocation) || !toLocation.Equals(tariff.ToCity, StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }
                score += 4;
            }
            if (!string.IsNullOrWhiteSpace(tariff.FromRegion))
            {
                if (string.IsNullOrWhiteSpace(fromLocation) ||
                    fromLocation.IndexOf(tariff.FromRegion, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return -1;
                }
                score += 2;
            }
            if (!string.IsNullOrWhiteSpace(tariff.ToRegion))
            {
                if (string.IsNullOrWhiteSpace(toLocation) ||
                    toLocation.IndexOf(tariff.ToRegion, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return -1;
                }
                score += 2;
            }

            return score;
        }

        private static string GetCargoTypeText(F_WAYBILLS waybill)
        {
            if (waybill.SalesInvoiceID.HasValue || waybill.SalesOrderID.HasValue || waybill.TTNKID.HasValue)
            {
                return "Tayyor mahsulot";
            }

            if (waybill.InvoiceID.HasValue || waybill.PurchaseOrderID.HasValue)
            {
                return "Xomashyo/material";
            }

            return "Boshqa";
        }

        private static string GetLinkedDocText(F_WAYBILLS waybill)
        {
            if (waybill.SalesInvoiceID.HasValue)
            {
                return $"Sotuv hisob-fakturasi #{waybill.SalesInvoiceID}";
            }
            if (waybill.InvoiceID.HasValue)
            {
                return $"Xarid hisob-fakturasi #{waybill.InvoiceID}";
            }
            if (waybill.TTNKID.HasValue)
            {
                return $"TTNK #{waybill.TTNKID}";
            }
            if (waybill.SalesOrderID.HasValue)
            {
                return $"Sotuv buyurtmasi #{waybill.SalesOrderID}";
            }
            if (waybill.PurchaseOrderID.HasValue)
            {
                return $"Xarid buyurtmasi #{waybill.PurchaseOrderID}";
            }
            return null;
        }

        private static string FormatTariffDisplay(F_CONTRACT_TARIFFS tariff)
        {
            var transport = tariff.F_TRANSPORT_TYPES?.TransportType ?? "";
            var capacity = tariff.F_TRANSPORT_TYPES?.CapableOfLifting;
            var capacityText = capacity.HasValue ? $"{capacity.Value:0.##}t" : "";
            var transportToken = transport.Replace(" ", "_");
            return $"{tariff.ID}_{transportToken}_{capacityText}";
        }

        private static string GenerateNextWaybillNo(DBTHSNEntities db)
        {
            var existingNos = new HashSet<string>(
                db.F_WAYBILLS.Where(w => w.IsDeleted == false).Select(w => w.WaybillNo),
                StringComparer.OrdinalIgnoreCase);

            int? lastNumeric = db.F_WAYBILLS
                .Where(w => w.IsDeleted == false && w.WaybillNo.StartsWith("TB_"))
                .OrderByDescending(w => w.ID)
                .Select(w => w.WaybillNo)
                .AsEnumerable()
                .Select(ParseWaybillNumber)
                .FirstOrDefault(n => n.HasValue);

            var lastId = db.F_WAYBILLS
                .Where(w => w.IsDeleted == false)
                .OrderByDescending(w => w.ID)
                .Select(w => (int?)w.ID)
                .FirstOrDefault() ?? 0;

            var next = (lastNumeric ?? lastId) + 1;
            var candidate = FormatWaybillNo(next);
            while (existingNos.Contains(candidate))
            {
                next++;
                candidate = FormatWaybillNo(next);
            }

            return candidate;
        }

        private static int? ParseWaybillNumber(string waybillNo)
        {
            if (string.IsNullOrWhiteSpace(waybillNo) || !waybillNo.StartsWith("TB_"))
            {
                return null;
            }

            var numberPart = waybillNo.Substring(3);
            if (int.TryParse(numberPart, out var result))
            {
                return result;
            }

            return null;
        }

        private static string FormatWaybillNo(int number)
        {
            return $"TB_{number:D4}";
        }

        private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet worksheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = worksheet.Dimension.End.Column;
            for (int col = 1; col <= lastCol; col++)
            {
                var header = worksheet.Cells[1, col].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                {
                    map[header] = col;
                }
            }
            return map;
        }

        private static string GetCellString(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key)
        {
            if (!map.ContainsKey(key)) return null;
            return worksheet.Cells[row, map[key]].Text?.Trim();
        }

        private Dictionary<int, double> BuildWaybillPaidLookup(DBTHSNEntities db, List<int> waybillIds)
        {
            var result = waybillIds.Distinct().ToDictionary(id => id, id => 0d);
            if (!waybillIds.Any()) return result;

            var direct = db.F_WAYBILL_PAYMENTS
                .Where(p =>
                    p.IsDeleted == false &&
                    waybillIds.Contains(p.WaybillID) &&
                    !db.F_WAYBILL_PAYMENT_SPLITS.Any(s => s.PayerType == WaybillPaymentSplitPayerType && s.PayerID == p.ID))
                .GroupBy(p => p.WaybillID)
                .Select(g => new { WaybillID = g.Key, Amount = g.Sum(x => x.Payment_Amount) })
                .ToList();

            foreach (var item in direct)
            {
                if (result.ContainsKey(item.WaybillID))
                {
                    result[item.WaybillID] += item.Amount;
                }
            }

            var split = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                         join p in db.F_WAYBILL_PAYMENTS on s.PayerID equals p.ID
                         where s.PayerType == WaybillPaymentSplitPayerType
                               && waybillIds.Contains(s.WaybillID)
                               && p.IsDeleted == false
                         group s by s.WaybillID into g
                         select new { WaybillID = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToList();

            foreach (var item in split)
            {
                if (result.ContainsKey(item.WaybillID))
                {
                    result[item.WaybillID] += item.Amount;
                }
            }

            return result;
        }

        private double GetWaybillPaidTotal(DBTHSNEntities db, int waybillId)
        {
            return BuildWaybillPaidLookup(db, new List<int> { waybillId })[waybillId];
        }

        private static double? GetCellDouble(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key)
        {
            var text = GetCellString(worksheet, row, map, key);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (double.TryParse(text, out var value)) return value;
            return null;
        }

        private static int? GetCellInt(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key)
        {
            var text = GetCellString(worksheet, row, map, key);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (int.TryParse(text, out var value)) return value;
            return null;
        }

        private static long? GetCellLong(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key)
        {
            var text = GetCellString(worksheet, row, map, key);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (long.TryParse(text, out var value)) return value;
            return null;
        }

        private static byte? GetCellByte(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key, Func<string, byte?> parser = null)
        {
            var text = GetCellString(worksheet, row, map, key);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (byte.TryParse(text, out var value)) return value;
            return parser != null ? parser(text) : (byte?)null;
        }

        private static DateTime? GetCellDate(ExcelWorksheet worksheet, int row, Dictionary<string, int> map, string key)
        {
            if (!map.ContainsKey(key)) return null;
            var cell = worksheet.Cells[row, map[key]];
            if (cell.Value == null) return null;
            if (cell.Value is DateTime dt) return dt;
            if (double.TryParse(cell.Value.ToString(), out var oaDate))
            {
                return DateTime.FromOADate(oaDate);
            }
            if (DateTime.TryParse(cell.Text, out var parsed)) return parsed;
            return null;
        }

        private static byte? ParseDirection(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "kirim":
                case "kiruvchi":
                case "inbound":
                    return 0;
                case "chiqim":
                case "chiquvchi":
                case "outbound":
                    return 1;
                case "ichki":
                case "internal":
                    return 2;
                default:
                    return null;
            }
        }

        private static byte? ParseFreightBasis(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "mashina uchun":
                case "pertruck":
                    return 0;
                case "ton uchun":
                case "perton":
                    return 1;
                case "km uchun":
                case "perkm":
                    return 2;
                case "flat":
                    return 3;
                default:
                    return null;
            }
        }

        private static byte? ParsePayerType(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "bizning kompaniya":
                case "ourcompany":
                    return 0;
                case "mijoz":
                case "customer":
                    return 1;
                case "taminotchi":
                case "supplier":
                    return 2;
                case "bo'lib":
                case "shared":
                    return 3;
                default:
                    return null;
            }
        }
    }
}
