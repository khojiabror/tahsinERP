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
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Yo'l varaqasi CRUD (LOG-M01: Excel import — WaybillImportController, AJAX — WaybillLookupController,
    /// biznes qoidalari — WaybillService). Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish).
    /// </summary>
    [Permission]
    public class WaybillController : BaseController
    {
        // Olish so'rovi: Waybill
        public async Task<ActionResult> Index(string searchTerm, int? direction, int? forwarderId, int? contractId,
            int? transportTypeId, DateTime? startDate, DateTime? endDate, string sortOrder, int? page, int? pageSize)
        {
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
                        CargoTypeText = WaybillService.CargoTypeText(w.SalesInvoiceID, w.SalesOrderID, w.TTNKID, w.InvoiceID, w.PurchaseOrderID),
                        LinkedDoc = WaybillService.LinkedDocText(w.SalesInvoiceID, w.SalesOrderID, w.TTNKID, w.InvoiceID, w.PurchaseOrderID)
                    };
                }).ToList();

                ViewBag.Forwarders = ReferenceDataCache.SelectList(ReferenceDataCache.Forwarders, forwarderId);
                ViewBag.Contracts = new SelectList(LogisticsLookups.Contracts(db), "ID", "Name", contractId);
                ViewBag.TransportTypes = new SelectList(LogisticsLookups.TransportTypes(db), "ID", "Name", transportTypeId);
                ViewBag.Directions = LogisticsEnums.DirectionList(direction);

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
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = WaybillService.BuildViewModel(db);
                viewModel.IssueDate = DateTime.Now;

                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(WaybillCreateViewModel viewModel)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var tariff = WaybillService.ApplyTariff(db, viewModel, ModelState, requireTariff: true, reapply: true);
                WaybillService.ValidateWaybillNo(db, viewModel, ModelState);

                if (ModelState.IsValid)
                {
                    WaybillService.FillAmount(viewModel, tariff);

                    var waybill = new F_WAYBILLS { IsDeleted = false };
                    WaybillService.ApplyToEntity(viewModel, waybill, WaybillService.GetContractForwarder(db, viewModel.ContractID));

                    // LOG-P04/B07: raqam qulf ostida beriladi va INSERT bilan bitta tranzaksiyada
                    using (var tx = db.Database.BeginTransaction())
                    {
                        if (string.IsNullOrWhiteSpace(waybill.WaybillNo))
                        {
                            waybill.WaybillNo = await DocumentNumberService.NextAsync(db, "F_WAYBILLS", "WaybillNo", WaybillService.WaybillNoPrefix, 4);
                        }
                        else
                        {
                            await DocumentNumberService.AcquireLockAsync(db, "F_WAYBILLS.WaybillNo:" + WaybillService.WaybillNoPrefix);
                            if (WaybillService.WaybillNoExists(db, waybill.WaybillNo, null))
                            {
                                tx.Rollback();
                                ModelState.AddModelError("WaybillNo", WaybillService.DuplicateNoMessage);
                                return View(WaybillService.BuildViewModel(db, viewModel));
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
                viewModel = WaybillService.BuildViewModel(db, viewModel);

                return View(viewModel);
            }
        }

        public async Task<ActionResult> Details(int? ID)
        {
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

                var viewModel = WaybillService.ToViewModel(waybill, WaybillService.ResolveTariffId(db, waybill));
                viewModel = WaybillService.BuildViewModel(db, viewModel);

                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(WaybillCreateViewModel viewModel)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // LOG-B05: tarif faqat foydalanuvchi uni almashtirganda qayta qo'llanadi (tarixiy stavka yangi
                // tarif bilan jimgina almashtirilmaydi); faol tarif bo'lmasa ham saqlash mumkin.
                bool tariffChanged = viewModel.TariffID.HasValue && viewModel.TariffID != viewModel.OriginalTariffID;
                var tariff = WaybillService.ApplyTariff(db, viewModel, ModelState, requireTariff: false, reapply: tariffChanged);
                WaybillService.ValidateWaybillNo(db, viewModel, ModelState);

                if (ModelState.IsValid)
                {
                    // LOG-B16: o'chirilgan yozuv Edit orqali "tiklanmaydi", IsDeleted'ga tegilmaydi
                    var waybillToUpdate = await db.F_WAYBILLS.FirstOrDefaultAsync(w => w.ID == viewModel.ID && w.IsDeleted != true);
                    if (waybillToUpdate == null)
                    {
                        return HttpNotFound();
                    }

                    WaybillService.FillAmount(viewModel, tariff);

                    WaybillService.ApplyToEntity(viewModel, waybillToUpdate, WaybillService.GetContractForwarder(db, viewModel.ContractID));

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
                viewModel = WaybillService.BuildViewModel(db, viewModel);

                return View(viewModel);
            }
        }

        public async Task<ActionResult> Delete(int? Id)
        {
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(int? ID)
        {
            if (ID == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (await SoftDeleteAsync<F_WAYBILLS>(db, ID, null,
                        w => $"{w.WaybillNo} - {LogisticsTerms.Waybill.ToLower()} o'chirildi.",
                        LogisticsTerms.Waybill + " topilmadi."))
                    return RedirectToAction("Index");
            }

            // LOG-B09: xatoda view null model bilan ochilib yiqilmasin
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var model = await LoadForDeleteAsync(db, ID.Value);
                if (model == null)
                {
                    return HttpNotFound();
                }
                return View(model);
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
    }
}
