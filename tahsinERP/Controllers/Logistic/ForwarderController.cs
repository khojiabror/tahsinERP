using DocumentFormat.OpenXml.Drawing.Charts;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class ForwarderController : Controller
    {
        // Olish so'rovi: Forwarder
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).ToListAsync();
                return View(list);
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
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(FORWARDER forwarder)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
        public async Task<ActionResult> Edit(FORWARDER forwarder)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
                    FORWARDER forwarderToDelete = await db.FORWARDERS.FirstOrDefaultAsync(f => f.ID == ID && f.IsDeleted != true);
                    if (forwarderToDelete != null)
                    {
                        forwarderToDelete.IsDeleted = true;
                        
                        try
                        {
                            await SoftDeleteReferenceService.AfterForwarderSoftDeletedAsync(db, forwarderToDelete.ID);
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "ForwarderController", "Delete", $"{forwarderToDelete.ForwarderName} - yuk tashuvchi o'chirildi.");

                            return RedirectToAction("Index");
                        }
                        catch (RetryLimitExceededException)
                        {
                            ModelState.AddModelError("", "Oʻzgarishlarni saqlab boʻlmadi. Qayta urinib ko'ring va agar muammo davom etsa, tizim administratoriga murojaat qiling.");
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Ma'lumotni o'chirishda xatolik yuz berdi. Qayta urinib ko'ring.");
                            LogHelper.LogToDatabase(User.Identity.Name, "ForwarderController", "Delete", $"Xatolik: {ex.Message}");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Yuk tashuvchi topilmadi.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Ma'lumotlar noto'g'ri yuborildi. Qayta urinib ko'ring.");
                }

                // LOG-B09: view null model bilan ochilmasin
                var model = await db.FORWARDERS.AsNoTracking().FirstOrDefaultAsync(f => f.ID == ID && f.IsDeleted != true);
                if (model == null)
                {
                    return HttpNotFound();
                }
                return View(model);
            }
        }

        /// <summary>
        /// Get waybills uchun a specific forwarder (uchun AJAX calls)
        /// </summary>
        public async Task<ActionResult> GetWaybills(int? forwarderId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return Json(new { error = "Unauthorized" }, JsonRequestBehavior.AllowGet);
            }

            if (forwarderId == null)
            {
                return Json(new { error = "Forwarder ID is required" }, JsonRequestBehavior.AllowGet);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybills = await db.F_WAYBILLS
                    .Where(w => w.ForwarderID == forwarderId && w.IsDeleted == false)
                    .Select(w => new
                    {
                        w.ID,
                        w.WaybillNo,
                        w.IssueDate,
                        w.Direction,
                        w.FromLocation,
                        w.ToLocation,
                        w.Amount,
                        w.CurrencyID
                    })
                    .ToListAsync();

                return Json(waybills, JsonRequestBehavior.AllowGet);
            }
        }

    }
}
