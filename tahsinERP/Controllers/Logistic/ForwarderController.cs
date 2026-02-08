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
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    public class ForwarderController : Controller
    {
        // GET: Forwarder
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var list = await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync();
                return View(list);
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
                        // Set IsDeleted to false and save the forwarder to get the ID
                        forwarder.IsDeleted = false;
                        db.FORWARDERS.Add(forwarder);
                        await db.SaveChangesAsync();

                        var userEmail = User.Identity.Name;
                        LogHelper.LogToDatabase(userEmail, "ForwarderController", "Create", $"{forwarder.ForwarderName} - Forwarderni yaratdi");

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
                var forwarder = await db.FORWARDERS.FindAsync(id);
                if (forwarder == null)
                    return HttpNotFound();

                // Calculate statistics
                var waybills = await db.F_WAYBILLS
                    .Where(w => w.ForwarderID == id && w.IsDeleted == false)
                    .ToListAsync();

                var payments = await db.F_WAYBILL_PAYMENTS
                    .Where(p => p.ForwarderID == id && p.IsDeleted == false)
                    .ToListAsync();

                var statistics = new ForwarderStatisticsViewModel
                {
                    Forwarder = forwarder,
                    TotalTrips = waybills.Count,
                    InboundTrips = waybills.Count(w => w.Direction == 0),
                    OutboundTrips = waybills.Count(w => w.Direction == 1),
                    InternalTrips = waybills.Count(w => w.Direction == 2),
                    TotalTonnage = waybills.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                    TotalCBM = waybills.Where(w => w.CBM.HasValue).Sum(w => w.CBM.Value),
                    TotalNetWeight = waybills.Where(w => w.NetWeight.HasValue).Sum(w => w.NetWeight.Value),
                    TotalCost = waybills.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value),
                    PaidAmount = payments.Sum(p => p.Payment_Amount),
                    UnpaidAmount = waybills.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value) - payments.Sum(p => p.Payment_Amount),
                    ActiveContracts = await db.F_CONTRACTS
                        .Where(c => c.ForwarderID == id && c.IsDeleted == false && c.DueDate >= DateTime.Now)
                        .ToListAsync(),
                    RecentWaybills = waybills.OrderByDescending(w => w.IssueDate).Take(10).ToList(),
                    RecentPayments = payments.OrderByDescending(p => p.PaymentDate ?? DateTime.MinValue).Take(10).ToList()
                };

                // Calculate averages
                if (waybills.Any(w => w.DistanceKm.HasValue && w.DistanceKm > 0))
                {
                    statistics.AverageCostPerKm = waybills
                        .Where(w => w.Amount.HasValue && w.DistanceKm.HasValue && w.DistanceKm > 0)
                        .Average(w => w.Amount.Value / w.DistanceKm.Value);
                }

                if (statistics.TotalTonnage > 0)
                {
                    statistics.AverageCostPerTon = statistics.TotalCost / statistics.TotalTonnage;
                }

                if (statistics.TotalTrips > 0)
                {
                    statistics.AverageCostPerTrip = statistics.TotalCost / statistics.TotalTrips;
                }

                if (statistics.TotalCBM > 0)
                {
                    statistics.AverageCostPerCBM = statistics.TotalCost / statistics.TotalCBM;
                }

                // Calculate OTIF
                var waybillsWithDates = waybills.Where(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue).ToList();
                if (waybillsWithDates.Any())
                {
                    statistics.OnTimeDeliveries = waybillsWithDates.Count(w => w.ActualDeliveryDate <= w.PlannedDeliveryDate);
                    statistics.LateDeliveries = waybillsWithDates.Count - statistics.OnTimeDeliveries;
                    statistics.OTIFPercentage = (double)statistics.OnTimeDeliveries / waybillsWithDates.Count * 100;
                }

                // Calculate top lanes
                statistics.TopLanes = waybills
                    .Where(w => !string.IsNullOrEmpty(w.FromLocation) && !string.IsNullOrEmpty(w.ToLocation))
                    .GroupBy(w => new { w.FromLocation, w.ToLocation })
                    .Select(g => new LanePerformance
                    {
                        FromLocation = g.Key.FromLocation,
                        ToLocation = g.Key.ToLocation,
                        TripCount = g.Count(),
                        TotalTonnage = g.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                        TotalCost = g.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value),
                        AverageCostPerTon = g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue)
                            .Any() ? g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.GrWeight.Value) : 0,
                        AverageCostPerKm = g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue)
                            .Any() ? g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.DistanceKm.Value) : 0
                    })
                    .OrderByDescending(l => l.TripCount)
                    .Take(10)
                    .ToList();

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
                var forwarder = await db.FORWARDERS.FindAsync(id);
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
                    var forwarderToUpdate = await db.FORWARDERS.FindAsync(forwarder.ID);
                    if (forwarderToUpdate == null)
                    {
                        ModelState.AddModelError("", "Forwarder topilmadi.");
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
                        LogHelper.LogToDatabase(userEmail, "ForwarderController", "Edit", $"{forwarder.ForwarderName} - Forwarderni tahrirladi");

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
                var forwarder = await db.FORWARDERS.FindAsync(Id);
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
                    FORWARDER forwarderToDelete = await db.FORWARDERS.FindAsync(ID);
                    if (forwarderToDelete != null)
                    {
                        forwarderToDelete.IsDeleted = true;
                        
                        try
                        {
                            await db.SaveChangesAsync();

                            var userEmail = User.Identity.Name;
                            LogHelper.LogToDatabase(userEmail, "ForwarderController", "Delete", $"{forwarderToDelete.ForwarderName} - Forwarderni o'chirdi");

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
                        ModelState.AddModelError("", "Forwarder topilmadi.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Ma'lumotlar noto'g'ri yuborildi. Qayta urinib ko'ring.");
                }

                return View();
            }
        }

        /// <summary>
        /// Get waybills for a specific forwarder (for AJAX calls)
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