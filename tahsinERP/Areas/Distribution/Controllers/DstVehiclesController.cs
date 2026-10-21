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
    /// Mashinalar (raqam, yuk ko'tarish, hajm, haydovchi, odatiy ekspeditor va ombor) va yetkazish sozlamalari:
    /// Yandex xarita kaliti, ombor koordinatalari (marshrut boshi), ovozli buyurtma.
    /// </summary>
    public class DstVehiclesController : DstControllerBase
    {
        public sealed class VehicleRow
        {
            public Vehicle Vehicle { get; set; }
            public string Expeditor { get; set; }
            public string Warehouse { get; set; }
            public int Trips { get; set; }
        }

        public sealed class DepotRow
        {
            public int WarehouseID { get; set; }
            public string Name { get; set; }
            public string Geo { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                var rows = (from v in db.Vehicles
                            orderby v.IsActive descending, v.PlateNo
                            select new VehicleRow
                            {
                                Vehicle = v,
                                Expeditor = db.Users.Where(u => u.ID == v.ExpeditorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                Warehouse = db.Warehouses.Where(w => w.ID == v.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                                Trips = db.Trips.Count(t => t.VehicleID == v.ID && t.Status != DocumentStatus.Cancelled)
                            }).ToList();
                FillLookups(db);
                var warehouses = StockService.SalesWarehouses(db);
                var names = db.Warehouses.Where(w => warehouses.Contains(w.ID)).ToDictionary(w => w.ID, w => w.WHName);
                ViewBag.Depots = warehouses.Select(id => new DepotRow { WarehouseID = id, Name = names.TryGetValue(id, out var n) ? n : "#" + id, Geo = DstSettings.Get(db, TripService.DepotKey(id)) }).ToList();
                ViewBag.MapsKey = DstSettings.Get(db, DeliverySettings.YandexMapsKey);
                ViewBag.VoiceOrders = DstSettings.Get(db, DeliverySettings.VoiceOrders) == "1";
                return View(rows);
            }
        }

        private void FillLookups(DistributionDbContext db)
        {
            ViewBag.Expeditors = Expeditors(db);
            ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
        }

        internal static List<SelectListItem> Expeditors(DistributionDbContext db) =>
            (from a in db.SalesAgents
             join u in db.Users on a.UserID equals u.ID
             where a.IsActive && a.IsExpeditor && u.IsDeleted != true
             orderby u.FullName ?? u.Uname
             select new { a.UserID, Name = u.FullName ?? u.Uname }).ToList()
            .Select(x => new SelectListItem { Value = x.UserID.ToString(), Text = x.Name }).ToList();

        public sealed class VehicleInput
        {
            public int? ID { get; set; }
            public string PlateNo { get; set; }
            public string Model { get; set; }
            public decimal CapacityKg { get; set; }
            public decimal? CapacityM3 { get; set; }
            public string DriverName { get; set; }
            public string DriverPhone { get; set; }
            public int? ExpeditorUserID { get; set; }
            public int? WarehouseID { get; set; }
            public bool IsActive { get; set; } = true;
            public string Note { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(VehicleInput input)
        {
            return DstJson(() =>
            {
                var plate = (input.PlateNo ?? "").Trim().ToUpperInvariant().Replace("  ", " ");
                if (plate.Length < 4 || plate.Length > 20) throw new DstValidationException(DstLocalization.T("Vehicle_PlateRequired"));
                if (input.CapacityKg <= 0 || input.CapacityM3.HasValue && input.CapacityM3 <= 0)
                    throw new DstValidationException(DstLocalization.T("Vehicle_CapacityPositive"));
                string phone = null;
                if (!string.IsNullOrWhiteSpace(input.DriverPhone))
                    phone = DirectoryRules.NormalizePhone(input.DriverPhone);
                using (var db = NewDb())
                {
                    if (db.Vehicles.Any(v => v.PlateNo == plate && v.ID != (input.ID ?? 0)))
                        throw new DstValidationException(DstLocalization.T("Error_Duplicate"));
                    if (input.ExpeditorUserID.HasValue && !db.SalesAgents.Any(a => a.UserID == input.ExpeditorUserID && a.IsExpeditor))
                        throw new DstValidationException(DstLocalization.T("Trip_ExpeditorRequired"));
                    var v = input.ID.HasValue ? db.Vehicles.Find(input.ID.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound")) : new Vehicle();
                    v.PlateNo = plate;
                    v.Model = Cut(input.Model, 100);
                    v.CapacityKg = Math.Round(input.CapacityKg, 2);
                    v.CapacityM3 = input.CapacityM3.HasValue ? Math.Round(input.CapacityM3.Value, 3) : (decimal?)null;
                    v.DriverName = Cut(input.DriverName, 100);
                    v.DriverPhone = phone;
                    v.ExpeditorUserID = input.ExpeditorUserID;
                    v.WarehouseID = input.WarehouseID;
                    v.IsActive = input.IsActive;
                    v.Note = Cut(input.Note, 300);
                    if (!input.ID.HasValue) db.Vehicles.Add(v);
                    db.SaveChanges();
                    return new { v.ID };
                }
            });
        }

        /// <summary>Yetkazish sozlamalari: Yandex kaliti, ovozli buyurtma, ombor koordinatalari ("lat,lng").</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveSettings(string mapsKey, bool voiceOrders, int[] depotWarehouse, string[] depotGeo)
        {
            return DstJson(() =>
            {
                mapsKey = string.IsNullOrWhiteSpace(mapsKey) ? null : mapsKey.Trim();
                if (mapsKey != null && (mapsKey.Length > 100 || mapsKey.Any(c => !(char.IsLetterOrDigit(c) || c == '-'))))
                    throw new DstValidationException(DstLocalization.T("Map_BadKey"));
                using (var db = NewDb())
                {
                    DstSettings.Set(db, DeliverySettings.YandexMapsKey, mapsKey);
                    DstSettings.Set(db, DeliverySettings.VoiceOrders, voiceOrders ? "1" : "0");
                    for (int i = 0; i < (depotWarehouse?.Length ?? 0); i++)
                    {
                        var geo = depotGeo != null && i < depotGeo.Length ? depotGeo[i]?.Trim() : null;
                        if (!string.IsNullOrEmpty(geo) && !DeliveryMath.TryParseGeo(geo, out _, out _))
                            throw new DstValidationException(DstLocalization.T("Error_GeoRange"));
                        DstSettings.Set(db, TripService.DepotKey(depotWarehouse[i]), string.IsNullOrEmpty(geo) ? null : geo);
                    }
                    db.SaveChanges();
                    return new { ok = true };
                }
            });
        }

        private static string Cut(string s, int max)
        {
            s = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            return s == null || s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
