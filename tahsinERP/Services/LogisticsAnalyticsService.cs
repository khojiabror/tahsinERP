using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika analitikasi — marshrut (lane) va yuk tashuvchi statistikasi SQL'da guruhlanadi
    /// (LOG-P07/P08: avval to'liq F_WAYBILLS entity'lari Include bilan xotiraga yuklanardi).
    /// LOG-D04: avval Dashboard, LanePerformance va Forwarder Details'da 3 xil nusxa va 3 xil view model edi.
    /// Valyutalar konvertatsiya qilinmaydi (LOG-B13).
    /// </summary>
    public static class LogisticsAnalyticsService
    {
        /// <summary>Marshrutlar bo'yicha statistika (trip soni bo'yicha kamayish tartibida).</summary>
        public static List<LanePerformanceViewModel> GetLaneStats(DBTHSNEntities db, IQueryable<F_WAYBILLS> waybills,
            int? top = null, bool withMainForwarder = false)
        {
            var lanesQuery = waybills
                .Where(w => w.FromLocation != null && w.FromLocation != "" && w.ToLocation != null && w.ToLocation != "");

            var grouped = lanesQuery
                .GroupBy(w => new { w.FromLocation, w.ToLocation })
                .Select(g => new
                {
                    g.Key.FromLocation,
                    g.Key.ToLocation,
                    TripCount = g.Count(),
                    TotalTonnage = g.Sum(w => w.GrWeight) ?? 0,
                    TotalCBM = g.Sum(w => w.CBM) ?? 0,
                    TotalCost = g.Sum(w => w.Amount) ?? 0,
                    TotalDistance = g.Sum(w => w.DistanceKm) ?? 0,
                    AvgCostPerTon = g.Where(w => w.GrWeight > 0 && w.Amount != null).Average(w => w.Amount / w.GrWeight),
                    AvgCostPerKm = g.Where(w => w.DistanceKm > 0 && w.Amount != null).Average(w => w.Amount / w.DistanceKm)
                })
                .OrderByDescending(x => x.TripCount)
                .ThenBy(x => x.FromLocation);

            var rows = (top.HasValue ? grouped.Take(top.Value) : grouped).ToList();

            Dictionary<string, string> mainForwarder = null;
            if (withMainForwarder && rows.Count > 0)
            {
                var perForwarder = lanesQuery
                    .Where(w => w.ForwarderID.HasValue)
                    .GroupBy(w => new { w.FromLocation, w.ToLocation, w.ForwarderID })
                    .Select(g => new { g.Key.FromLocation, g.Key.ToLocation, g.Key.ForwarderID, Trips = g.Count() })
                    .ToList();
                var names = ForwarderNames(db, perForwarder.Select(x => x.ForwarderID.Value));
                mainForwarder = perForwarder
                    .GroupBy(x => LaneKey(x.FromLocation, x.ToLocation))
                    .ToDictionary(g => g.Key, g =>
                    {
                        var best = g.OrderByDescending(x => x.Trips).First();
                        string name;
                        return names.TryGetValue(best.ForwarderID.Value, out name) ? name : "Noma'lum";
                    });
            }

            return rows.Select(x => new LanePerformanceViewModel
            {
                FromLocation = x.FromLocation,
                ToLocation = x.ToLocation,
                TripCount = x.TripCount,
                TotalTonnage = x.TotalTonnage,
                TotalCBM = x.TotalCBM,
                TotalCost = x.TotalCost,
                TotalDistance = x.TotalDistance,
                AvgCostPerTon = x.AvgCostPerTon ?? 0,
                AvgCostPerKm = x.AvgCostPerKm ?? 0,
                MainForwarder = mainForwarder == null
                    ? null
                    : (mainForwarder.TryGetValue(LaneKey(x.FromLocation, x.ToLocation), out var mf) ? mf : "Noma'lum")
            }).ToList();
        }

        /// <summary>Yuk tashuvchilar bo'yicha statistika va OTIF (trip soni bo'yicha kamayish tartibida).</summary>
        public static List<ForwarderComparisonViewModel> GetForwarderStats(DBTHSNEntities db, IQueryable<F_WAYBILLS> waybills, int? top = null)
        {
            var grouped = waybills
                .Where(w => w.ForwarderID.HasValue)
                .GroupBy(w => w.ForwarderID)
                .Select(g => new
                {
                    ForwarderID = g.Key,
                    TripCount = g.Count(),
                    TotalCost = g.Sum(w => w.Amount) ?? 0,
                    TotalTonnage = g.Sum(w => w.GrWeight) ?? 0,
                    TotalDistance = g.Sum(w => w.DistanceKm) ?? 0,
                    AvgCostPerTon = g.Where(w => w.GrWeight > 0 && w.Amount != null).Average(w => w.Amount / w.GrWeight),
                    AvgCostPerKm = g.Where(w => w.DistanceKm > 0 && w.Amount != null).Average(w => w.Amount / w.DistanceKm),
                    OnTime = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null && w.ActualDeliveryDate <= w.PlannedDeliveryDate),
                    Delivered = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null)
                })
                .OrderByDescending(x => x.TripCount);

            var rows = (top.HasValue ? grouped.Take(top.Value) : grouped).ToList();
            var names = ForwarderNames(db, rows.Select(x => x.ForwarderID.Value));

            return rows.Select(x => new ForwarderComparisonViewModel
            {
                ForwarderID = x.ForwarderID,
                ForwarderName = names.TryGetValue(x.ForwarderID.Value, out var n) ? n : "Noma'lum",
                TripCount = x.TripCount,
                TotalCost = x.TotalCost,
                TotalTonnage = x.TotalTonnage,
                TotalDistance = x.TotalDistance,
                AvgCostPerTon = x.AvgCostPerTon ?? 0,
                AvgCostPerKm = x.AvgCostPerKm ?? 0,
                OnTimeDeliveries = x.OnTime,
                TotalDeliveries = x.Delivered,
                OTIFPercentage = x.Delivered > 0 ? (double)x.OnTime / x.Delivered * 100 : 0
            }).ToList();
        }

        /// <summary>Yo'l varaqalari to'plami bo'yicha jami ko'rsatkichlar (yo'nalish kesimida).</summary>
        public static WaybillTotals GetTotals(IQueryable<F_WAYBILLS> waybills)
        {
            var byDirection = waybills
                .GroupBy(w => w.Direction)
                .Select(g => new
                {
                    Direction = g.Key,
                    Trips = g.Count(),
                    Cost = g.Sum(w => w.Amount) ?? 0,
                    Tonnage = g.Sum(w => w.GrWeight) ?? 0,
                    Cbm = g.Sum(w => w.CBM) ?? 0,
                    Distance = g.Sum(w => w.DistanceKm) ?? 0
                })
                .ToList();

            return new WaybillTotals
            {
                Trips = byDirection.Sum(x => x.Trips),
                Cost = byDirection.Sum(x => x.Cost),
                Tonnage = byDirection.Sum(x => x.Tonnage),
                Cbm = byDirection.Sum(x => x.Cbm),
                Distance = byDirection.Sum(x => x.Distance),
                InboundCost = byDirection.Where(x => x.Direction == 0).Sum(x => x.Cost),
                OutboundCost = byDirection.Where(x => x.Direction == 1).Sum(x => x.Cost),
                InternalCost = byDirection.Where(x => x.Direction == 2).Sum(x => x.Cost)
            };
        }

        public static Dictionary<int, string> ForwarderNames(DBTHSNEntities db, IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return new Dictionary<int, string>();
            // O'chirilgan yuk tashuvchilar ham (tarixiy statistikada nomi ko'rinsin)
            return db.FORWARDERS.AsNoTracking()
                .Where(f => list.Contains(f.ID))
                .Select(f => new { f.ID, f.ForwarderName })
                .ToList()
                .ToDictionary(x => x.ID, x => x.ForwarderName ?? "Noma'lum");
        }

        private static string LaneKey(string from, string to) => from + "\u0001" + to;
    }

    public sealed class WaybillTotals
    {
        public int Trips { get; set; }
        public double Cost { get; set; }
        public double Tonnage { get; set; }
        public double Cbm { get; set; }
        public double Distance { get; set; }
        public double InboundCost { get; set; }
        public double OutboundCost { get; set; }
        public double InternalCost { get; set; }
    }
}
