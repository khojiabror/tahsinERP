using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>Ruxsat: [Permission] (GET — ko'rish, POST/Create/Edit/Delete — o'zgartirish; AJAX endpointlar ham, LOG-S02).</summary>
    // LOG-S03: POST entity binding faqat ruxsat etilgan maydonlar bilan ([Bind(Include)]) — over-posting yo'q
    [Permission]
    public class ForwarderController : BaseController
    {
        // Olish so'rovi: Forwarder
        public async Task<ActionResult> Index()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).ToListAsync();
                return View(list);
            }
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "ForwarderName,Director,ContactPerson,Description")] FORWARDER forwarder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        // Belgilash IsDeleted ga false va saqlash the forwarder ga get the ID
                        forwarder.IsDeleted = false;
                        db.FORWARDERS.Add(forwarder);
                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "ForwarderController", "Create", $"{forwarder.ForwarderName} - yuk tashuvchi yaratildi.");

                        return RedirectToAction("Index");
                    }
                    ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                    LogHelper.LogToDatabase(User.Identity.Name, "ForwarderController", "Create", $"Xatolik: {ex.Message}");
                }
            }

            return View(forwarder);
        }
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var forwarder = await db.FORWARDERS.AsNoTracking().FirstOrDefaultAsync(f => f.ID == id);
                if (forwarder == null)
                    return HttpNotFound();

                // LOG-P08: statistika SQL'da (avval barcha yo'l varaqalari va to'lovlar entity sifatida yuklanardi)
                var waybills = WaybillLedgerService.ActiveWaybills(db).Where(w => w.ForwarderID == id);
                var agg = await waybills
                    .GroupBy(w => 1)
                    .Select(g => new
                    {
                        Trips = g.Count(),
                        Inbound = g.Count(w => w.Direction == 0),
                        Outbound = g.Count(w => w.Direction == 1),
                        Internal = g.Count(w => w.Direction == 2),
                        Tonnage = g.Sum(w => w.GrWeight) ?? 0,
                        Cbm = g.Sum(w => w.CBM) ?? 0,
                        NetWeight = g.Sum(w => w.NetWeight) ?? 0,
                        Cost = g.Sum(w => w.Amount) ?? 0,
                        AvgPerKm = g.Where(w => w.DistanceKm > 0 && w.Amount != null).Average(w => w.Amount / w.DistanceKm),
                        OnTime = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null && w.ActualDeliveryDate <= w.PlannedDeliveryDate),
                        Delivered = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null)
                    })
                    .FirstOrDefaultAsync();

                // LOG-B04: qarz ta'rifi boshqa ekranlar bilan bir xil (qo'lda kiritilgan FIN to'lovlar ham)
                var balance = WaybillLedgerService.GetForwarderBalance(db, forwarder.ID);
                var today = DateTime.Today;

                var statistics = new ForwarderStatisticsViewModel
                {
                    Forwarder = forwarder,
                    TotalTrips = agg?.Trips ?? 0,
                    InboundTrips = agg?.Inbound ?? 0,
                    OutboundTrips = agg?.Outbound ?? 0,
                    InternalTrips = agg?.Internal ?? 0,
                    TotalTonnage = agg?.Tonnage ?? 0,
                    TotalCBM = agg?.Cbm ?? 0,
                    TotalNetWeight = agg?.NetWeight ?? 0,
                    TotalCost = agg?.Cost ?? 0,
                    PaidAmount = balance.TotalPaid,
                    UnpaidAmount = balance.TotalFreight - balance.TotalPaid,
                    AverageCostPerKm = agg?.AvgPerKm ?? 0,
                    ActiveContracts = await db.F_CONTRACTS.AsNoTracking()
                        .Where(c => c.ForwarderID == id && !c.IsDeleted && c.DueDate >= today)
                        .ToListAsync(),
                    RecentWaybills = await waybills.AsNoTracking().OrderByDescending(w => w.IssueDate).Take(10).ToListAsync(),
                    RecentPayments = await WaybillLedgerService.ActivePayments(db).AsNoTracking()
                        .Where(p => p.ForwarderID == id)
                        .OrderByDescending(p => p.PaymentDate)
                        .Take(10)
                        .ToListAsync(),
                    TopLanes = LogisticsAnalyticsService.GetLaneStats(db, waybills, 10)
                };

                if (statistics.TotalTonnage > 0)
                    statistics.AverageCostPerTon = statistics.TotalCost / statistics.TotalTonnage;
                if (statistics.TotalTrips > 0)
                    statistics.AverageCostPerTrip = statistics.TotalCost / statistics.TotalTrips;
                if (statistics.TotalCBM > 0)
                    statistics.AverageCostPerCBM = statistics.TotalCost / statistics.TotalCBM;

                if (agg != null && agg.Delivered > 0)
                {
                    statistics.OnTimeDeliveries = agg.OnTime;
                    statistics.TotalDeliveries = agg.Delivered;
                    statistics.LateDeliveries = agg.Delivered - agg.OnTime;
                    statistics.OTIFPercentage = (double)agg.OnTime / agg.Delivered * 100;
                }

                return View(statistics);
            }
        }
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var forwarder = await db.FORWARDERS.FirstOrDefaultAsync(f => f.ID == id && f.IsDeleted != true);
                if (forwarder == null)
                {
                    return HttpNotFound();
                }
                return View(forwarder);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,ForwarderName,Director,ContactPerson,Description")] FORWARDER forwarder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var forwarderToUpdate = await db.FORWARDERS.FirstOrDefaultAsync(f => f.ID == forwarder.ID && f.IsDeleted != true);
                    if (forwarderToUpdate == null)
                    {
                        ModelState.AddModelError("", "Yuk tashuvchi topilmadi.");
                        return View(forwarder);
                    }

                    forwarderToUpdate.ForwarderName = forwarder.ForwarderName;
                    forwarderToUpdate.Director = forwarder.Director;
                    forwarderToUpdate.Description = forwarder.Description;
                    forwarderToUpdate.ContactPerson = forwarder.ContactPerson;
                    
                    db.Entry(forwarderToUpdate).State = System.Data.Entity.EntityState.Modified;
                    try
                    {
                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "ForwarderController", "Edit", $"{forwarder.ForwarderName} - yuk tashuvchi tahrirlandi.");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Ma'lumotni saqlashda xatolik yuz berdi. Qayta urinib ko'ring.");
                        LogHelper.LogToDatabase(User.Identity.Name, "ForwarderController", "Edit", $"Xatolik: {ex.Message}");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Iltimos, majburiy maydonlarni to'ldiring.");
                }
                return View(forwarder);
            }
        }
        public async Task<ActionResult> Delete(int? Id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (Id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var forwarder = await db.FORWARDERS.AsNoTracking().FirstOrDefaultAsync(f => f.ID == Id && f.IsDeleted != true);
                if (forwarder == null)
                {
                    return HttpNotFound();
                }

                return View(forwarder);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(int? ID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (await SoftDeleteAsync<FORWARDER>(db, ID,
                        (ctx, f) => SoftDeleteReferenceService.AfterForwarderSoftDeletedAsync(ctx, f.ID),
                        f => $"{f.ForwarderName} - yuk tashuvchi o'chirildi.",
                        "Yuk tashuvchi topilmadi."))
                    return RedirectToAction("Index");

                // LOG-B09: view null model bilan ochilmasin
                var model = await db.FORWARDERS.AsNoTracking().FirstOrDefaultAsync(f => f.ID == ID && f.IsDeleted != true);
                if (model == null)
                {
                    return HttpNotFound();
                }
                return View(model);
            }
        }

    }
}
