using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Reyslar: rejalash (jo'natishlarni biriktirish, sig'im, xaritada tartib), yo'lga chiqarish, yetkazish natijalari (ekspeditor
    /// botdan yoki ofis), reysni yopish (farq dalolatnomasi: qaytgan va yetkazilmagan tovar, kamomad), reys xarajatlari moliyaga.
    /// Ko'rinish — ombor scope'i bo'yicha.
    /// </summary>
    public class DstTripsController : DstControllerBase
    {
        public sealed class TripRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime Date { get; set; }
            public byte Status { get; set; }
            public string Vehicle { get; set; }
            public string Expeditor { get; set; }
            public string Warehouse { get; set; }
            public int Stops { get; set; }
            public int Done { get; set; }
            public decimal WeightKg { get; set; }
            public decimal Total { get; set; }
            public decimal? Delivered { get; set; }
        }

        private ScopeFilter Warehouses(DistributionDbContext db) => new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);

        public ActionResult Index(DateTime? from, DateTime? to, byte? status, int? expeditorId)
        {
            var dFrom = (from ?? DateTime.Today.AddDays(-7)).Date;
            var dTo = (to ?? DateTime.Today.AddDays(7)).Date;
            using (var db = NewDb())
            {
                var q = db.Trips.Where(t => t.DocDate >= dFrom && t.DocDate <= dTo);
                q = Warehouses(db).Apply(q, t => t.WarehouseID);
                if (status.HasValue) q = q.Where(t => t.TripStatus == status);
                if (expeditorId.HasValue) q = q.Where(t => t.ExpeditorUserID == expeditorId);
                var rows = (from t in q
                            orderby t.DocDate descending, t.ID descending
                            select new TripRow
                            {
                                ID = t.ID, DocNo = t.DocNo, Date = t.DocDate, Status = t.TripStatus, WeightKg = t.WeightKg, Total = t.Total,
                                Vehicle = db.Vehicles.Where(v => v.ID == t.VehicleID).Select(v => v.PlateNo).FirstOrDefault(),
                                Expeditor = db.Users.Where(u => u.ID == t.ExpeditorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                Warehouse = db.Warehouses.Where(w => w.ID == t.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                                Stops = db.TripStops.Count(s => s.TripID == t.ID),
                                Done = db.TripStops.Count(s => s.TripID == t.ID && s.StopStatus != (byte)StopStatus.Pending),
                                Delivered = db.TripStops.Where(s => s.TripID == t.ID).Sum(s => s.DeliveredAmount)
                            }).Take(300).ToList();
                ViewBag.From = dFrom;
                ViewBag.To = dTo;
                ViewBag.Status = status;
                ViewBag.ExpeditorId = expeditorId;
                ViewBag.Expeditors = DstVehiclesController.Expeditors(db);
                return View(rows);
            }
        }

        // ---------- rejalash ----------

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                TripView view = null;
                if (id.HasValue)
                {
                    view = TripQueries.Details(db, id.Value, IsRussian);
                    if (!Warehouses(db).Allows(view.Trip.WarehouseID)) return HttpNotFound();
                    if (view.Trip.TripStatus != (byte)TripStatus.Planned) return RedirectToAction("Details", new { id });
                }
                FillPlanLookups(db);
                ViewBag.MapsKey = DeliverySettings.MapsKey(db);
                return View(view);
            }
        }

        private void FillPlanLookups(DistributionDbContext db)
        {
            var scope = Warehouses(db);
            var vehicles = db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.PlateNo).ToList();
            ViewBag.Vehicles = vehicles;
            ViewBag.Expeditors = DstVehiclesController.Expeditors(db);
            var warehouses = StockService.SalesWarehouses(db).Where(w => scope.Allows(w)).ToList();
            ViewBag.Warehouses = db.Warehouses.Where(w => warehouses.Contains(w.ID)).OrderBy(w => w.WHName).ToList()
                .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
            ViewBag.Depots = warehouses.ToDictionary(w => w, w => TripService.Depot(db, w));
        }

        /// <summary>Biriktirish mumkin bo'lgan jo'natishlar (ombor bo'yicha), og'irlik/hajm va koordinata bilan.</summary>
        [HttpGet]
        public JsonResult Available(int warehouseId, int? tripId)
        {
            using (var db = NewDb())
            {
                if (!Warehouses(db).Allows(warehouseId)) return Json(new object[0], JsonRequestBehavior.AllowGet);
                var depot = TripService.Depot(db, warehouseId);
                var rows = TripQueries.Available(db, warehouseId, tripId);
                return Json(new
                {
                    depot = depot == null ? null : new { lat = depot.Lat, lng = depot.Lng },
                    rows = rows.Select(r => new
                    {
                        id = r.ID, docNo = r.DocNo, date = r.Date.ToString("dd.MM.yyyy"), order = r.OrderNo, orderId = r.OrderID,
                        delivery = r.DeliveryDate?.ToString("dd.MM.yyyy"), customer = r.Customer, address = r.Address, territory = r.Territory,
                        lat = r.Lat, lng = r.Lng, total = r.Total, kg = r.WeightKg, m3 = r.VolumeM3
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(TripInput input)
        {
            return DstJson(() =>
            {
                using (var check = NewDb())
                {
                    var warehouse = input.WarehouseID ?? check.Vehicles.Where(v => v.ID == input.VehicleID).Select(v => v.WarehouseID).FirstOrDefault();
                    if (warehouse.HasValue && !Warehouses(check).Allows(warehouse.Value)) throw new DstValidationException(DstLocalization.T("Forbidden"));
                }
                using (var uow = NewUnitOfWork())
                {
                    var id = TripService.Save(uow, input, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        // ---------- reys kartasi ----------

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var view = TripQueries.Details(db, id, IsRussian);
                if (!Warehouses(db).Allows(view.Trip.WarehouseID)) return HttpNotFound();
                var links = FileLinks.For(db, DateTimeOffset.UtcNow);
                ViewBag.Links = links;
                ViewBag.MapsKey = DeliverySettings.MapsKey(db);
                ViewBag.Reasons = db.Reasons.Where(r => r.Type == ReasonTypes.Return && r.IsActive).OrderBy(r => r.SortOrder).ToList()
                    .Select(r => new SelectListItem { Value = r.ID.ToString(), Text = Localized(r.Name, r.NameRu) }).ToList();
                DstPaymentsController.FillPaymentLookups(ViewBag, db);
                ViewBag.Categories = db.Database.SqlQuery<TripQueries.IdName>(
                        "SELECT ID, Name FROM dbo.FIN_CATEGORIES WHERE Type = 2 AND IsActive = 1 AND ISNULL(IsDeleted, 0) = 0 ORDER BY SortOrder, Name").ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }).ToList();
                return View(view);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Start(int id) => Act(id, uow => TripService.Start(uow, id, UserId(), DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Cancel(int id, string reason) => Act(id, uow => TripService.Cancel(uow, id, UserId(), reason, DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Optimize(int id)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, id);
                    var km = TripService.Optimize(uow, id);
                    uow.Commit();
                    return new { km };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Reorder(int id, int[] stopIds) => Act(id, uow => TripService.Reorder(uow, id, stopIds ?? new int[0]));

        /// <summary>Ofis natija kiritadi (telefon orqali tasdiq): mode = all | partial | refuse; qisman — qator miqdorlari.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Deliver(int stopId, string mode, int[] lineIds, decimal[] qtys, int? reasonId, string note)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, uow.Dst.TripStops.Where(s => s.ID == stopId).Select(s => (int?)s.TripID).FirstOrDefault());
                    var delivered = new Dictionary<int, decimal>();
                    if (mode == "partial")
                        for (int i = 0; i < (lineIds?.Length ?? 0); i++)
                            delivered[lineIds[i]] = qtys != null && i < qtys.Length ? qtys[i] : 0;
                    var status = TripService.Deliver(uow, new DeliverInput
                    {
                        StopID = stopId, All = mode == "all", Delivered = delivered, ReasonID = reasonId, Note = note
                    }, UserId(), null, false, DateTime.Now);
                    uow.Commit();
                    return new { status = status.ToString() };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ResetStop(int stopId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, uow.Dst.TripStops.Where(s => s.ID == stopId).Select(s => (int?)s.TripID).FirstOrDefault());
                    TripService.ResetStop(uow, stopId, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        /// <summary>Yopilgan reysda qaytgan tovarni qayta yetkazish — yangi buyurtma qoralamasi (K2-10).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Redeliver(int stopId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, uow.Dst.TripStops.Where(s => s.ID == stopId).Select(s => (int?)s.TripID).FirstOrDefault());
                    var orderId = TripService.Redeliver(uow, stopId, Services.Orders.OrderActor.ForUser(uow.Dst, CurrentUserName), DateTime.Now);
                    uow.Commit();
                    return new { url = Url.Action("Edit", "DstOrders", new { id = orderId }) };
                }
            });
        }

        /// <summary>Reysni yopish: har qator bo'yicha omborga qaytgan miqdor (berilmasa — yetkazilmaganning hammasi).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Close(int id, int[] lineIds, decimal[] returned, int? locationId, string note)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, id);
                    var map = new Dictionary<int, decimal>();
                    for (int i = 0; i < (lineIds?.Length ?? 0); i++)
                        if (returned != null && i < returned.Length) map[lineIds[i]] = returned[i];
                    var r = TripService.Close(uow, new CloseInput { TripID = id, Returned = map, LocationID = locationId, Note = note }, UserId(), null, AuditActor.Web, DateTime.Now);
                    uow.Commit();
                    return new { r.ReturnedAmount, r.ShortageAmount, returns = r.ReturnIds.Count };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveExpense(int tripId, string kind, int? categoryId, decimal amount, string note, int? accountId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, tripId);
                    var id = TripService.AddExpense(uow, tripId, kind, categoryId, amount, note, null, UserId(), null, AuditActor.Web, DateTime.Now);
                    if (accountId.HasValue) TripService.PostExpense(uow, id, categoryId, accountId.Value, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult PostExpense(int expenseId, int? categoryId, int accountId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, uow.Dst.TripExpenses.Where(x => x.ID == expenseId).Select(x => (int?)x.TripID).FirstOrDefault());
                    TripService.PostExpense(uow, expenseId, categoryId, accountId, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult CancelExpense(int expenseId)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, uow.Dst.TripExpenses.Where(x => x.ID == expenseId).Select(x => (int?)x.TripID).FirstOrDefault());
                    TripService.CancelExpense(uow, expenseId, UserId(), DateTime.Now);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }

        /// <summary>DLOC omborda qaytarish uchun joylashuvlar.</summary>
        [HttpGet]
        public JsonResult Locations(int warehouseId)
        {
            using (var uow = NewUnitOfWork())
            {
                bool dloc = tahsinERP.Services.Stock.StockLedger.IsDlocWarehouse(uow.Erp, warehouseId);
                var rows = dloc
                    ? uow.Erp.WAREHOUSE_LOCATIONS.Where(l => l.WarehouseID == warehouseId && l.IsActive && !l.IsDeleted)
                        .OrderBy(l => l.LocationCode).Select(l => new { id = l.ID, text = l.LocationCode + " " + (l.LocationName ?? "") }).ToList()
                        .Cast<object>().ToList()
                    : new List<object>();
                return Json(new { dloc, locations = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        // ---------- chop etish ----------

        /// <summary>Marshrut varag'i (ekspeditorga) yoki farq dalolatnomasi (yopilgan reys).</summary>
        public ActionResult Print(int id, string kind)
        {
            using (var db = NewDb())
            {
                var view = TripQueries.Details(db, id, IsRussian);
                if (!Warehouses(db).Allows(view.Trip.WarehouseID)) return HttpNotFound();
                ViewBag.Company = db.Database.SqlQuery<string>("SELECT TOP 1 Name FROM dbo.COMPANIES WHERE ISNULL(IsActive, 1) = 1 ORDER BY ID").FirstOrDefault();
                return View(kind == "act" ? "PrintAct" : "PrintRoute", view);
            }
        }

        // ---------- yordamchilar ----------

        private int UserId() => tahsinERP.Models.RoleHelper.GetUserId(CurrentUserName) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));

        private void EnsureTrip(DistributionDbContext db, int? tripId)
        {
            var warehouse = tripId.HasValue ? db.Trips.Where(t => t.ID == tripId).Select(t => (int?)t.WarehouseID).FirstOrDefault() : null;
            if (warehouse == null || !Warehouses(db).Allows(warehouse.Value)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        private JsonResult Act(int id, Action<DistributionUnitOfWork> action)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureTrip(uow.Dst, id);
                    action(uow);
                    uow.Commit();
                    return new { ok = true };
                }
            });
        }
    }
}
