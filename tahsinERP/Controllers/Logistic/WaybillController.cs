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
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class WaybillController : Controller
    {
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
                var query = WaybillLedgerService.ActiveWaybills(db).AsNoTracking();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(w =>
                        w.WaybillNo.Contains(searchTerm) ||
                        w.FromLocation.Contains(searchTerm) ||
                        w.ToLocation.Contains(searchTerm) ||
                        w.TruckNo.Contains(searchTerm) ||
                        w.DriverName.Contains(searchTerm));
                }
                if (direction.HasValue)
                    query = query.Where(w => w.Direction == direction.Value);
                if (forwarderId.HasValue)
                    query = query.Where(w => w.ForwarderID == forwarderId.Value);
                if (contractId.HasValue)
                    query = query.Where(w => w.ContractID == contractId.Value);
                if (transportTypeId.HasValue)
                    query = query.Where(w => w.TransportTypeID == transportTypeId.Value);
                if (startDate.HasValue)
                    query = query.Where(w => w.IssueDate >= startDate.Value);
                if (endDate.HasValue)
                {
                    var endExclusive = endDate.Value.Date.AddDays(1);
                    query = query.Where(w => w.IssueDate < endExclusive);
                }

                switch (sortOrder)
                {
                    case "date":
                        query = query.OrderBy(w => w.IssueDate).ThenBy(w => w.ID);
                        break;
                    case "waybillno":
                        query = query.OrderBy(w => w.WaybillNo);
                        break;
                    case "waybillno_desc":
                        query = query.OrderByDescending(w => w.WaybillNo);
                        break;
                    default:
                        query = query.OrderByDescending(w => w.IssueDate).ThenByDescending(w => w.ID);
                        break;
                }

                // LOG-P02: server tomonida sahifalash va SQL proyeksiya (avval butun jadval 5 ta Include bilan
                // yuklanib, brauzerda DataTables bilan sahifalanardi)
                var paging = new LogisticsPageInfo(page, pageSize, await query.CountAsync());
                var rows = await query
                    .Skip(paging.Skip)
                    .Take(paging.PageSize)
                    .Select(w => new
                    {
                        w.ID, w.WaybillNo, w.IssueDate, w.Direction,
                        ForwarderName = w.FORWARDER != null ? w.FORWARDER.ForwarderName : w.F_CONTRACTS.FORWARDER.ForwarderName,
                        ContractNo = w.F_CONTRACTS.ContractNo,
                        TransportType = w.F_TRANSPORT_TYPES.TransportType,
                        w.FromLocation, w.ToLocation, w.TruckNo, w.DriverName, w.DistanceKm, w.GrWeight, w.CBM, w.Amount,
                        Currency = w.CURRENCy.Shortname,
                        w.SalesInvoiceID, w.SalesOrderID, w.TTNKID, w.InvoiceID, w.PurchaseOrderID
                    })
                    .ToListAsync();

                var paymentLookup = WaybillLedgerService.GetPaidLookup(db, rows.Select(r => r.ID).ToList());

                var waybills = rows.Select(w =>
                {
                    var totalPaid = WaybillLedgerService.PaidOf(paymentLookup, w.ID);
                    var amount = w.Amount ?? 0;
                    return new WaybillIndexItemViewModel
                    {
                        ID = w.ID,
                        WaybillNo = w.WaybillNo,
                        IssueDate = w.IssueDate,
                        Direction = w.Direction,
                        ForwarderName = w.ForwarderName,
                        ContractNo = w.ContractNo,
                        TransportType = w.TransportType,
                        FromLocation = w.FromLocation,
                        ToLocation = w.ToLocation,
                        TruckNo = w.TruckNo,
                        DriverName = w.DriverName,
                        DistanceKm = w.DistanceKm,
                        GrWeight = w.GrWeight,
                        CBM = w.CBM,
                        Amount = w.Amount,
                        Currency = w.Currency,
                        TotalPaid = totalPaid,
                        Outstanding = amount - totalPaid,
                        PaymentStatus = WaybillLedgerService.StatusOf(amount, totalPaid).ToDisplay(),
                        CargoTypeText = GetCargoTypeText(w.SalesInvoiceID, w.SalesOrderID, w.TTNKID, w.InvoiceID, w.PurchaseOrderID),
                        LinkedDoc = GetLinkedDocText(w.SalesInvoiceID, w.SalesOrderID, w.TTNKID, w.InvoiceID, w.PurchaseOrderID)
                    };
                }).ToList();

                ViewBag.Forwarders = ReferenceDataCache.SelectList(ReferenceDataCache.Forwarders, forwarderId);
                ViewBag.Contracts = new SelectList(LogisticsLookups.Contracts(db), "ID", "Name", contractId);
                ViewBag.TransportTypes = new SelectList(LogisticsLookups.TransportTypes(db), "ID", "Name", transportTypeId);
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

                paging.RouteValues = new System.Web.Routing.RouteValueDictionary
                {
                    { "searchTerm", searchTerm }, { "direction", direction }, { "forwarderId", forwarderId },
                    { "contractId", contractId }, { "transportTypeId", transportTypeId },
                    { "startDate", ViewBag.StartDate }, { "endDate", ViewBag.EndDate }, { "sortOrder", sortOrder }
                };
                ViewBag.Paging = paging;

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
                var tariff = ApplyTariff(db, viewModel, requireTariff: true, reapply: true);
                ValidateWaybillNo(db, viewModel);

                if (ModelState.IsValid)
                {
                    // Navlun summasi qo'lda kiritilmagan bo'lsa — tarif bo'yicha (MinimumCharge bilan, LOG-B06)
                    if (!viewModel.Amount.HasValue)
                    {
                        viewModel.Amount = FreightCalculator.Calculate(viewModel.FreightBasis, viewModel.Rate,
                            viewModel.GrWeight, viewModel.DistanceKm, tariff?.MinimumCharge);
                    }

                    var waybill = new F_WAYBILLS { IsDeleted = false };
                    ApplyToEntity(viewModel, waybill, GetContractForwarder(db, viewModel.ContractID));

                    // LOG-P04/B07: raqam qulf ostida beriladi va INSERT bilan bitta tranzaksiyada
                    using (var tx = db.Database.BeginTransaction())
                    {
                        if (string.IsNullOrWhiteSpace(waybill.WaybillNo))
                        {
                            waybill.WaybillNo = await DocumentNumberService.NextAsync(db, "F_WAYBILLS", "WaybillNo", WaybillNoPrefix, 4);
                        }
                        else
                        {
                            await DocumentNumberService.AcquireLockAsync(db, "F_WAYBILLS.WaybillNo:" + WaybillNoPrefix);
                            if (WaybillNoExists(db, waybill.WaybillNo, null))
                            {
                                tx.Rollback();
                                ModelState.AddModelError("WaybillNo", $"Bu raqamli {LogisticsTerms.Waybill.ToLower()} allaqachon mavjud.");
                                return View(BuildWaybillViewModel(db, viewModel));
                            }
                        }

                        db.F_WAYBILLS.Add(waybill);
                        await db.SaveChangesAsync();
                        tx.Commit();
                    }

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
                // LOG-P03: F_WAYBILL_DOCS (blob), F_WAYBILL_PAYMENTS va P_INVOICES view'da ishlatilmaydi
                var waybill = await db.F_WAYBILLS.AsNoTracking()
                    .Include(w => w.F_CONTRACTS.FORWARDER)
                    .Include(w => w.F_TRANSPORT_TYPES)
                    .Include(w => w.CURRENCy)
                    .Include(w => w.FORWARDER)
                    .FirstOrDefaultAsync(w => w.ID == ID && w.IsDeleted != true);

                if (waybill == null)
                {
                    return HttpNotFound();
                }

                // To'langan/qarz — yagona WaybillLedgerService (LOG-D01); P03: jadval bo'yicha split ID'lar yuklanmaydi
                var splitPaymentIdsForWaybill = db.F_WAYBILL_PAYMENT_SPLITS
                    .Where(s => s.PayerType == WaybillLedgerService.SplitPayerType &&
                                s.WaybillID == waybill.ID &&
                                s.PayerID.HasValue)
                    .Select(s => s.PayerID.Value);

                var totalPaid = WaybillLedgerService.GetPaidTotal(db, waybill.ID);
                var outstanding = (waybill.Amount ?? 0) - totalPaid;
                var paymentStatus = WaybillLedgerService.StatusOf(waybill.Amount ?? 0, totalPaid).ToDisplay();

                var linkedPayments = db.F_WAYBILL_PAYMENTS.AsNoTracking()
                    .Where(p => !p.IsDeleted &&
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
                var waybill = await db.F_WAYBILLS.AsNoTracking().FirstOrDefaultAsync(w => w.ID == ID && w.IsDeleted != true);
                if (waybill == null)
                {
                    return HttpNotFound();
                }

                // Yo'l varaqasida TariffID ustuni yo'q (sxema taklifi: Database_Migrations/Proposal_Logistics_Schema.sql),
                // shuning uchun shartnoma + transport turi bo'yicha eng so'nggi tarif taxmin qilinadi.
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
                    OriginalTariffID = tariffId,
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
                // LOG-B05: tarif faqat foydalanuvchi uni almashtirganda qayta qo'llanadi (tarixiy stavka yangi
                // tarif bilan jimgina almashtirilmaydi); faol tarif bo'lmasa ham saqlash mumkin.
                bool tariffChanged = viewModel.TariffID.HasValue && viewModel.TariffID != viewModel.OriginalTariffID;
                var tariff = ApplyTariff(db, viewModel, requireTariff: false, reapply: tariffChanged);
                ValidateWaybillNo(db, viewModel);

                if (ModelState.IsValid)
                {
                    // LOG-B16: o'chirilgan yozuv Edit orqali "tiklanmaydi", IsDeleted'ga tegilmaydi
                    var waybillToUpdate = await db.F_WAYBILLS.FirstOrDefaultAsync(w => w.ID == viewModel.ID && w.IsDeleted != true);
                    if (waybillToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    if (!viewModel.Amount.HasValue)
                    {
                        viewModel.Amount = FreightCalculator.Calculate(viewModel.FreightBasis, viewModel.Rate,
                            viewModel.GrWeight, viewModel.DistanceKm, tariff?.MinimumCharge);
                    }

                    ApplyToEntity(viewModel, waybillToUpdate, GetContractForwarder(db, viewModel.ContractID));

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
                var waybill = await LoadForDeleteAsync(db, Id.Value);

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

        private static readonly string[] ImportHeaders =
        {
            "WaybillNo","IssueDate","Direction","ForwarderID","ForwarderName","ContractID","ContractNo",
            "TransportTypeID","TransportType","FromLocation","ToLocation","DistanceKm","GrWeight","NetWeight",
            "CBM","PalletsCount","FreightBasis","Rate","Amount","CurrencyID","Currency","PayerType","PayerID",
            "InvoiceID","SalesInvoiceID","SalesOrderID","PurchaseOrderID","TTNKID","TruckNo","TrailerNo",
            "TruckModel","TrailerModel","DriverName","DriverPINFL","PlannedDeliveryDate","ActualDeliveryDate",
            "Description","ManualCorrectionReason"
        };

        private static readonly HashSet<string> ImportDateHeaders = new HashSet<string>
        {
            "IssueDate", "PlannedDeliveryDate", "ActualDeliveryDate"
        };

        public ActionResult DownloadTemplate()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // LOG-B12(d): namuna qatori haqiqiy shartnoma va transport turi bilan — shablonni o'zgartirmasdan import qilsa ham o'tadi
            var sample = new Dictionary<string, object>
            {
                { "WaybillNo", "WB-0001" },
                { "IssueDate", DateTime.Today },
                { "Direction", "Kiruvchi" },
                { "FromLocation", "Supplier A" },
                { "ToLocation", "Warehouse B" },
                { "DistanceKm", 25 },
                { "GrWeight", 12.5 },
                { "FreightBasis", 1 },
                { "Rate", 150000 },
                { "Amount", 1875000 },
                { "Currency", "UZS" }
            };
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                sample["ContractNo"] = db.F_CONTRACTS.Where(c => !c.IsDeleted).OrderByDescending(c => c.ID).Select(c => c.ContractNo).FirstOrDefault();
                sample["TransportType"] = db.F_TRANSPORT_TYPES.Where(t => t.IsDeleted != true).OrderBy(t => t.ID).Select(t => t.TransportType).FirstOrDefault();
            }

            // LOG-B12(c): sana formati ustun nomi bo'yicha (avval 36-37 ustunlar — Description — sana formatida edi)
            var columns = ImportHeaders
                .Select(h => ExcelExport.Col<Dictionary<string, object>>(h,
                    row => row.ContainsKey(h) ? row[h] : null,
                    ImportDateHeaders.Contains(h) ? "yyyy-mm-dd" : null))
                .ToArray();

            var bytes = ExcelExport.Create("Waybills", new[] { sample }, columns);
            return File(bytes, ExcelExport.ContentType, "Waybill_Import_Template.xlsx");
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
                var forwarders = await db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).Select(f => new { f.ID, f.ForwarderName }).ToListAsync();
                var contracts = await db.F_CONTRACTS.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.ID, c.ContractNo, c.ForwarderID }).ToListAsync();
                var transportTypes = await db.F_TRANSPORT_TYPES.AsNoTracking().Where(t => t.IsDeleted != true).Select(t => new { t.ID, t.TransportType }).ToListAsync();
                var currencies = ReferenceDataCache.Get(ReferenceDataCache.Currencies);
                var existingWaybillNos = new HashSet<string>(
                    db.F_WAYBILLS.Where(w => w.IsDeleted != true).Select(w => w.WaybillNo),
                    StringComparer.OrdinalIgnoreCase);
                var fileWaybillNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        ViewBag.Error = "Excel fayl bo'sh.";
                        return View();
                    }

                    var cells = new ExcelCellReader(worksheet);
                    if (!cells.Has("WaybillNo") || !cells.Has("IssueDate"))
                    {
                        ViewBag.Error = "Excel faylda majburiy ustunlar yo'q (WaybillNo, IssueDate).";
                        return View();
                    }

                    var lastRow = worksheet.Dimension.End.Row;
                    for (int row = 2; row <= lastRow; row++)
                    {
                        var waybillNo = cells.String(row, "WaybillNo");
                        if (string.IsNullOrWhiteSpace(waybillNo))
                        {
                            skipCount++;
                            continue;
                        }

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

                        var issueDate = cells.Date(row, "IssueDate");
                        if (!issueDate.HasValue)
                        {
                            errors.Add($"Qator {row}: sana noto'g'ri formatda.");
                            continue;
                        }

                        var contractId = cells.Int(row, "ContractID");
                        var contractNo = cells.String(row, "ContractNo");
                        var contract = contractId.HasValue
                            ? contracts.FirstOrDefault(c => c.ID == contractId.Value)
                            : (!string.IsNullOrWhiteSpace(contractNo) ? contracts.FirstOrDefault(c => c.ContractNo == contractNo) : null);

                        if (contract == null)
                        {
                            errors.Add($"Qator {row}: Shartnoma topilmadi (ContractID/ContractNo).");
                            continue;
                        }

                        var forwarderId = cells.Int(row, "ForwarderID");
                        var forwarderName = cells.String(row, "ForwarderName");
                        var forwarder = forwarderId.HasValue
                            ? forwarders.FirstOrDefault(f => f.ID == forwarderId.Value)
                            : (!string.IsNullOrWhiteSpace(forwarderName) ? forwarders.FirstOrDefault(f => f.ForwarderName == forwarderName) : null);

                        var transportTypeId = cells.Int(row, "TransportTypeID");
                        var transportTypeName = cells.String(row, "TransportType");
                        var transportType = transportTypeId.HasValue
                            ? transportTypes.FirstOrDefault(t => t.ID == transportTypeId.Value)
                            : (!string.IsNullOrWhiteSpace(transportTypeName) ? transportTypes.FirstOrDefault(t => t.TransportType == transportTypeName) : null);

                        if (transportType == null)
                        {
                            errors.Add($"Qator {row}: Transport turi topilmadi (TransportTypeID/TransportType).");
                            continue;
                        }

                        var currencyId = cells.Int(row, "CurrencyID");
                        var currencyCode = cells.String(row, "Currency");
                        var currency = currencyId.HasValue
                            ? currencies.FirstOrDefault(c => c.ID == currencyId.Value)
                            : (!string.IsNullOrWhiteSpace(currencyCode) ? currencies.FirstOrDefault(c => string.Equals(c.Name, currencyCode, StringComparison.OrdinalIgnoreCase)) : null);

                        var waybill = new F_WAYBILLS
                        {
                            WaybillNo = waybillNo,
                            IssueDate = issueDate.Value,
                            Direction = cells.Byte(row, "Direction", ParseDirection),
                            ForwarderID = forwarder?.ID ?? contract.ForwarderID,
                            ContractID = contract.ID,
                            TransportTypeID = transportType.ID,
                            FromLocation = cells.String(row, "FromLocation"),
                            ToLocation = cells.String(row, "ToLocation"),
                            DistanceKm = cells.Double(row, "DistanceKm"),
                            GrWeight = cells.Double(row, "GrWeight"),
                            NetWeight = cells.Double(row, "NetWeight"),
                            CBM = cells.Double(row, "CBM"),
                            PalletsCount = cells.Int(row, "PalletsCount"),
                            FreightBasis = cells.Byte(row, "FreightBasis", ParseFreightBasis),
                            Rate = cells.Double(row, "Rate"),
                            Amount = cells.Double(row, "Amount"),
                            CurrencyID = currency?.ID,
                            PayerType = cells.Byte(row, "PayerType", ParsePayerType),
                            PayerID = cells.Int(row, "PayerID"),
                            InvoiceID = cells.Int(row, "InvoiceID"),
                            SalesInvoiceID = cells.Long(row, "SalesInvoiceID"),
                            SalesOrderID = cells.Int(row, "SalesOrderID"),
                            PurchaseOrderID = cells.Int(row, "PurchaseOrderID"),
                            TTNKID = cells.Long(row, "TTNKID"),
                            TruckNo = cells.String(row, "TruckNo"),
                            TrailerNo = cells.String(row, "TrailerNo"),
                            TruckModel = cells.String(row, "TruckModel"),
                            TrailerModel = cells.String(row, "TrailerModel"),
                            DriverName = cells.String(row, "DriverName"),
                            DriverPINFL = cells.String(row, "DriverPINFL"),
                            PlannedDeliveryDate = cells.Date(row, "PlannedDeliveryDate"),
                            ActualDeliveryDate = cells.Date(row, "ActualDeliveryDate"),
                            Description = cells.String(row, "Description"),
                            ManualCorrectionReason = cells.String(row, "ManualCorrectionReason"),
                            IsDeleted = false
                        };

                        // LOG-B06: summa berilmagan bo'lsa stavka va asos bo'yicha hisoblanadi
                        if (!waybill.Amount.HasValue)
                        {
                            waybill.Amount = FreightCalculator.Calculate(waybill.FreightBasis, waybill.Rate, waybill.GrWeight, waybill.DistanceKm);
                        }

                        // LOG-B12(a): har qator alohida saqlanadi — bitta noto'g'ri qator (uzunlik, FK) butun faylni
                        // yiqitmaydi va xato qator raqami bilan ko'rsatiladi
                        db.F_WAYBILLS.Add(waybill);
                        try
                        {
                            await db.SaveChangesAsync();
                            existingWaybillNos.Add(waybillNo);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            db.Entry(waybill).State = EntityState.Detached;
                            errors.Add($"Qator {row}: saqlab bo'lmadi — {DescribeSaveError(ex)}");
                        }
                    }
                }

                LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "UploadExcel",
                    $"{successCount} ta {LogisticsTerms.Waybill.ToLower()} Excel orqali yuklandi, {skipCount} ta qator o'tkazib yuborildi, {errors.Count} ta xato.");
            }

            ViewBag.SuccessCount = successCount;
            ViewBag.SkipCount = skipCount;
            ViewBag.Errors = errors;
            return View();
        }

        private static string DescribeSaveError(Exception ex)
        {
            var validation = ex as System.Data.Entity.Validation.DbEntityValidationException;
            if (validation != null)
            {
                return string.Join("; ", validation.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => e.PropertyName + ": " + e.ErrorMessage));
            }

            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            var sql = inner as System.Data.SqlClient.SqlException;
            if (sql != null && (sql.Number == 547))
                return "bog'langan hujjat (invoys/buyurtma/TTNK ID) topilmadi.";
            if (sql != null && (sql.Number == 2601 || sql.Number == 2627))
                return "raqam takrorlangan.";
            if (sql != null && sql.Number == 8152)
                return "matn juda uzun.";
            return "ma'lumot formati noto'g'ri.";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int? ID, FormCollection gfs)
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
                var waybillToDelete = await db.F_WAYBILLS.FirstOrDefaultAsync(w => w.ID == ID && w.IsDeleted != true);
                if (waybillToDelete == null)
                {
                    return HttpNotFound();
                }

                waybillToDelete.IsDeleted = true;
                try
                {
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "WaybillController", "Delete",
                        $"{waybillToDelete.WaybillNo} - {LogisticsTerms.Waybill.ToLower()} o'chirildi.");

                    return RedirectToAction("Index");
                }
                catch (RetryLimitExceededException)
                {
                    ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                }

                // LOG-B09: xatoda view null model bilan ochilib yiqilmasin
                db.Entry(waybillToDelete).State = EntityState.Detached;
                return View(await LoadForDeleteAsync(db, ID.Value));
            }
        }

        private static Task<F_WAYBILLS> LoadForDeleteAsync(DBTHSNEntities db, int id)
        {
            return db.F_WAYBILLS.AsNoTracking()
                .Include(w => w.F_CONTRACTS.FORWARDER)
                .Include(w => w.F_TRANSPORT_TYPES)
                .Include(w => w.FORWARDER)
                .FirstOrDefaultAsync(w => w.ID == id && w.IsDeleted != true);
        }

        private const string WaybillNoPrefix = "TB_";

        /// <summary>
        /// Tanlangan tarifni tekshiradi va (reapply bo'lsa) stavka/asos/transport turini undan oladi.
        /// Avval Create va Edit'da bir xil blok takrorlangan edi.
        /// </summary>
        private F_CONTRACT_TARIFFS ApplyTariff(DBTHSNEntities db, WaybillCreateViewModel viewModel, bool requireTariff, bool reapply)
        {
            if (!viewModel.TariffID.HasValue)
            {
                if (requireTariff)
                    ModelState.AddModelError("TariffID", "Tarif tanlash majburiy.");
                return null;
            }

            if (!reapply)
            {
                // Tarif o'zgarmagan: saqlangan stavka qoladi; MinimumCharge uchun tarif o'qiladi (faol bo'lmasa ham)
                return db.F_CONTRACT_TARIFFS.AsNoTracking().FirstOrDefault(t => t.ID == viewModel.TariffID.Value);
            }

            var selectedTariff = db.F_CONTRACT_TARIFFS.AsNoTracking()
                .FirstOrDefault(t => t.ID == viewModel.TariffID.Value && t.IsActive);
            if (selectedTariff == null)
            {
                ModelState.AddModelError("TariffID", "Tanlangan tarif topilmadi yoki faol emas.");
                return null;
            }
            if (viewModel.ContractID.HasValue && selectedTariff.ContractID != viewModel.ContractID.Value)
            {
                ModelState.AddModelError("TariffID", "Tanlangan tarif ushbu shartnomaga tegishli emas.");
                return null;
            }

            viewModel.TransportTypeID = selectedTariff.TransportTypeID;
            viewModel.FreightBasis = selectedTariff.FreightBasis;
            viewModel.Rate = selectedTariff.Rate;
            if (!viewModel.CurrencyID.HasValue)
            {
                viewModel.CurrencyID = selectedTariff.CurrencyID;
            }
            return selectedTariff;
        }

        /// <summary>LOG-B07: qo'lda kiritilgan raqam takrorlanmasin (Excel import'dagi tekshiruv Create/Edit'da ham).</summary>
        private void ValidateWaybillNo(DBTHSNEntities db, WaybillCreateViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.WaybillNo))
            {
                if (viewModel.ID.HasValue)
                    ModelState.AddModelError("WaybillNo", $"{LogisticsTerms.Waybill} raqami majburiy.");
                return;
            }

            viewModel.WaybillNo = viewModel.WaybillNo.Trim();
            if (WaybillNoExists(db, viewModel.WaybillNo, viewModel.ID))
            {
                ModelState.AddModelError("WaybillNo", $"Bu raqamli {LogisticsTerms.Waybill.ToLower()} allaqachon mavjud.");
            }
        }

        private static bool WaybillNoExists(DBTHSNEntities db, string waybillNo, int? exceptId)
        {
            return db.F_WAYBILLS.Any(w => w.WaybillNo == waybillNo && w.IsDeleted != true &&
                                          (!exceptId.HasValue || w.ID != exceptId.Value));
        }

        private static int? GetContractForwarder(DBTHSNEntities db, int? contractId)
        {
            return contractId.HasValue
                ? db.F_CONTRACTS.Where(c => c.ID == contractId.Value).Select(c => (int?)c.ForwarderID).FirstOrDefault()
                : null;
        }

        /// <summary>View model → entity (avval Create va Edit'da 37 qatorli nusxa edi).</summary>
        private static void ApplyToEntity(WaybillCreateViewModel viewModel, F_WAYBILLS waybill, int? contractForwarder)
        {
            waybill.WaybillNo = viewModel.WaybillNo;
            waybill.IssueDate = viewModel.IssueDate;
            waybill.Direction = viewModel.Direction;
            waybill.ForwarderID = viewModel.ForwarderID ?? contractForwarder;
            waybill.ContractID = viewModel.ContractID;
            waybill.TransportTypeID = viewModel.TransportTypeID;
            waybill.FromLocation = viewModel.FromLocation;
            waybill.ToLocation = viewModel.ToLocation;
            waybill.DistanceKm = viewModel.DistanceKm;
            waybill.GrWeight = viewModel.GrWeight;
            waybill.NetWeight = viewModel.NetWeight;
            waybill.CBM = viewModel.CBM;
            waybill.PalletsCount = viewModel.PalletsCount;
            waybill.FreightBasis = viewModel.FreightBasis;
            waybill.Rate = viewModel.Rate;
            waybill.Amount = viewModel.Amount;
            waybill.CurrencyID = viewModel.CurrencyID;
            waybill.PayerType = viewModel.PayerType;
            waybill.PayerID = viewModel.PayerID;
            waybill.InvoiceID = viewModel.InvoiceID;
            waybill.SalesInvoiceID = viewModel.SalesInvoiceID;
            waybill.SalesOrderID = viewModel.SalesOrderID;
            waybill.PurchaseOrderID = viewModel.PurchaseOrderID;
            waybill.TTNKID = viewModel.TTNKID;
            waybill.TruckNo = viewModel.TruckNo;
            waybill.TrailerNo = viewModel.TrailerNo;
            waybill.TruckModel = viewModel.TruckModel;
            waybill.TrailerModel = viewModel.TrailerModel;
            waybill.DriverName = viewModel.DriverName;
            waybill.DriverPINFL = viewModel.DriverPINFL;
            waybill.PlannedDeliveryDate = viewModel.PlannedDeliveryDate;
            waybill.ActualDeliveryDate = viewModel.ActualDeliveryDate;
            waybill.Description = viewModel.Description;
            waybill.ManualCorrectionReason = viewModel.ManualCorrectionReason;
        }

        /// <summary>
        /// AJAX so'rovi: Get suggested rate dan contract tariffs
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
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
        [ValidateAntiForgeryHeader]
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
        [ValidateAntiForgeryHeader]
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

        /// <summary>
        /// Forma dropdown'lari. LOG-P01: invoys/buyurtma/TTNK ro'yxatlari to'liq yuklanmaydi — faqat
        /// tanlangan qiymat (agar bo'lsa) oldindan chiqariladi, qolgani SearchDocuments (select2 AJAX) orqali.
        /// Yuk tashuvchi va valyutalar — ReferenceDataCache.
        /// </summary>
        private static WaybillCreateViewModel BuildWaybillViewModel(DBTHSNEntities db, WaybillCreateViewModel model = null)
        {
            var viewModel = model ?? new WaybillCreateViewModel();

            viewModel.Forwarders = ToItems(ReferenceDataCache.Get(ReferenceDataCache.Forwarders), viewModel.ForwarderID);
            viewModel.Currencies = ToItems(ReferenceDataCache.Get(ReferenceDataCache.Currencies), viewModel.CurrencyID);
            viewModel.Contracts = ToItems(LogisticsLookups.Contracts(db, viewModel.ContractID), viewModel.ContractID);
            viewModel.TransportTypes = ToItems(LogisticsLookups.TransportTypes(db, viewModel.TransportTypeID), viewModel.TransportTypeID);

            viewModel.PurchaseInvoices = SelectedOnly(viewModel.InvoiceID,
                id => db.P_INVOICES.Where(i => i.ID == id).Select(i => i.InvoiceNo).FirstOrDefault());
            viewModel.SalesInvoices = SelectedOnly(viewModel.SalesInvoiceID,
                id => db.SALES_INVOICES.Where(i => i.ID == id).Select(i => i.InvoiceNo).FirstOrDefault());
            viewModel.SalesOrders = SelectedOnly(viewModel.SalesOrderID,
                id => db.S_ORDERS.Where(o => o.ID == id).Select(o => o.OrderNo).FirstOrDefault());
            viewModel.PurchaseOrders = SelectedOnly(viewModel.PurchaseOrderID,
                id => db.P_ORDERS.Where(o => o.ID == id).Select(o => o.OrderNo).FirstOrDefault());
            viewModel.TTNKs = SelectedOnly(viewModel.TTNKID,
                id => db.SALES_TTNK_HEADERS.Where(t => t.ID == id).Select(t => t.TTNKNumber).FirstOrDefault());

            viewModel.Tariffs = new List<SelectListItem>();
            if (viewModel.ContractID.HasValue)
            {
                var tariffs = db.F_CONTRACT_TARIFFS.AsNoTracking()
                    .Include(t => t.F_TRANSPORT_TYPES)
                    .Where(t => t.ContractID == viewModel.ContractID.Value &&
                                (t.IsActive || (viewModel.TariffID.HasValue && t.ID == viewModel.TariffID.Value)))
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

        private static List<SelectListItem> ToItems(IEnumerable<RefItem> items, int? selected)
        {
            return items.Select(x => new SelectListItem
            {
                Value = x.ID.ToString(),
                Text = x.Name,
                Selected = selected.HasValue && x.ID == selected.Value
            }).ToList();
        }

        private static List<SelectListItem> SelectedOnly<TId>(TId? id, Func<TId, string> text) where TId : struct
        {
            var list = new List<SelectListItem>();
            if (id.HasValue)
            {
                list.Add(new SelectListItem { Value = id.Value.ToString(), Text = text(id.Value) ?? ("#" + id.Value), Selected = true });
            }
            return list;
        }

        /// <summary>
        /// select2 AJAX qidiruvi: type = pinvoice | sinvoice | sorder | porder | ttnk; eng so'nggi 20 ta mos yozuv.
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> SearchDocuments(string type, string q)
        {
            const int take = 20;
            q = (q ?? "").Trim();

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                List<SearchItem> items;
                switch (type)
                {
                    case "pinvoice":
                        items = await db.P_INVOICES.AsNoTracking()
                            .Where(i => i.IsDeleted != true && i.InvoiceNo.Contains(q))
                            .OrderByDescending(i => i.ID).Take(take)
                            .Select(i => new SearchItem { id = i.ID, text = i.InvoiceNo }).ToListAsync();
                        break;
                    case "sinvoice":
                        items = await db.SALES_INVOICES.AsNoTracking()
                            .Where(i => i.InvoiceNo.Contains(q))
                            .OrderByDescending(i => i.ID).Take(take)
                            .Select(i => new SearchItem { id = i.ID, text = i.InvoiceNo }).ToListAsync();
                        break;
                    case "sorder":
                        items = await db.S_ORDERS.AsNoTracking()
                            .Where(o => o.IsDeleted != true && o.OrderNo.Contains(q))
                            .OrderByDescending(o => o.ID).Take(take)
                            .Select(o => new SearchItem { id = o.ID, text = o.OrderNo }).ToListAsync();
                        break;
                    case "porder":
                        items = await db.P_ORDERS.AsNoTracking()
                            .Where(o => o.IsDeleted != true && o.OrderNo.Contains(q))
                            .OrderByDescending(o => o.ID).Take(take)
                            .Select(o => new SearchItem { id = o.ID, text = o.OrderNo }).ToListAsync();
                        break;
                    case "ttnk":
                        items = await db.SALES_TTNK_HEADERS.AsNoTracking()
                            .Where(t => t.TTNKNumber.Contains(q))
                            .OrderByDescending(t => t.ID).Take(take)
                            .Select(t => new SearchItem { id = t.ID, text = t.TTNKNumber }).ToListAsync();
                        break;
                    default:
                        items = new List<SearchItem>();
                        break;
                }

                return Json(new { results = items }, JsonRequestBehavior.AllowGet);
            }
        }

        public sealed class SearchItem
        {
            // select2 kutadigan nomlar
            public long id { get; set; }
            public string text { get; set; }
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

        private static string GetCargoTypeText(long? salesInvoiceId, int? salesOrderId, long? ttnkId, int? invoiceId, int? purchaseOrderId)
        {
            if (salesInvoiceId.HasValue || salesOrderId.HasValue || ttnkId.HasValue)
                return "Tayyor mahsulot";
            if (invoiceId.HasValue || purchaseOrderId.HasValue)
                return "Xomashyo/material";
            return "Boshqa";
        }

        private static string GetLinkedDocText(long? salesInvoiceId, int? salesOrderId, long? ttnkId, int? invoiceId, int? purchaseOrderId)
        {
            if (salesInvoiceId.HasValue) return $"Sotuv hisob-fakturasi #{salesInvoiceId}";
            if (invoiceId.HasValue) return $"Xarid hisob-fakturasi #{invoiceId}";
            if (ttnkId.HasValue) return $"TTNK #{ttnkId}";
            if (salesOrderId.HasValue) return $"Sotuv buyurtmasi #{salesOrderId}";
            if (purchaseOrderId.HasValue) return $"Xarid buyurtmasi #{purchaseOrderId}";
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
