using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;

using tahsinERP.I18n;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika analitikasi — marshrut (lane) va yuk tashuvchi statistikasi SQL'da guruhlanadi
    /// (LOG-P07/P08: avval to'liq F_WAYBILLS entity'lari Include bilan xotiraga yuklanardi).
    /// LOG-D04: avval Dashboard, LanePerformance va Forwarder Details'da 3 xil nusxa va 3 xil view model edi.
    /// LOG-B13: summalar so'mda — SQL (kalit, valyuta, kun) kesimida guruhlaydi, har guruh hujjat sanasidagi
    /// kurs bilan o'giriladi (<see cref="LogisticsMoney"/>), so'ng xotirada kalit bo'yicha yig'iladi.
    /// </summary>
    public static class LogisticsAnalyticsService
    {
        /// <summary>Bir guruhning (kalit + valyuta + kun) yig'indilari.</summary>
        private sealed class Part
        {
            public string From { get; set; }
            public string To { get; set; }
            public int? ForwarderID { get; set; }
            public byte? Direction { get; set; }
            public int? CurrencyID { get; set; }
            public DateTime? Day { get; set; }
            public int Trips { get; set; }
            public decimal Tonnage { get; set; }
            public decimal Cbm { get; set; }
            public decimal Distance { get; set; }
            public decimal Cost { get; set; }
            public decimal PerTonSum { get; set; }
            public int PerTonCount { get; set; }
            public decimal PerKmSum { get; set; }
            public int PerKmCount { get; set; }
            public int OnTime { get; set; }
            public int Delivered { get; set; }
        }

        /// <summary>So'mga o'girilgan va kalit bo'yicha yig'ilgan qiymatlar.</summary>
        private sealed class Sum
        {
            public int Trips;
            public decimal Tonnage, Cbm, Distance, Cost, PerTonSum, PerKmSum;
            public int PerTonCount, PerKmCount, OnTime, Delivered;

            public void Add(Part p, LogisticsMoney money)
            {
                Trips += p.Trips;
                Tonnage += p.Tonnage;
                Cbm += p.Cbm;
                Distance += p.Distance;
                OnTime += p.OnTime;
                Delivered += p.Delivered;
                PerTonCount += p.PerTonCount;
                PerKmCount += p.PerKmCount;
                // Stavka (so'm/birlik) chiziqli — kurs bilan ko'paytirish o'rtacha qiymatni saqlaydi
                Cost += money.ToUzs(p.Cost, p.CurrencyID, p.Day);
                PerTonSum += money.ToUzs(p.PerTonSum, p.CurrencyID, p.Day);
                PerKmSum += money.ToUzs(p.PerKmSum, p.CurrencyID, p.Day);
            }

            public decimal AvgPerTon => PerTonCount > 0 ? PerTonSum / PerTonCount : 0;
            public decimal AvgPerKm => PerKmCount > 0 ? PerKmSum / PerKmCount : 0;
        }

        /// <summary>Marshrutlar bo'yicha statistika (trip soni bo'yicha kamayish tartibida), summalar so'mda.</summary>
        public static List<LanePerformanceViewModel> GetLaneStats(DBTHSNEntities db, IQueryable<F_WAYBILLS> waybills,
            int? top = null, bool withMainForwarder = false, LogisticsMoney money = null)
        {
            money = money ?? new LogisticsMoney(db);
            var lanesQuery = waybills
                .Where(w => w.FromLocation != null && w.FromLocation != "" && w.ToLocation != null && w.ToLocation != "");

            var parts = lanesQuery
                .GroupBy(w => new { w.FromLocation, w.ToLocation, w.CurrencyID, Day = DbFunctions.TruncateTime(w.IssueDate) })
                .Select(g => new Part
                {
                    From = g.Key.FromLocation,
                    To = g.Key.ToLocation,
                    CurrencyID = g.Key.CurrencyID,
                    Day = g.Key.Day,
                    Trips = g.Count(),
                    Tonnage = g.Sum(w => w.GrWeight) ?? 0,
                    Cbm = g.Sum(w => w.CBM) ?? 0,
                    Cost = g.Sum(w => w.Amount) ?? 0,
                    Distance = g.Sum(w => w.DistanceKm) ?? 0,
                    PerTonSum = g.Sum(w => w.GrWeight > 0 && w.Amount != null ? w.Amount / w.GrWeight : (decimal?)0) ?? 0,
                    PerTonCount = g.Count(w => w.GrWeight > 0 && w.Amount != null),
                    PerKmSum = g.Sum(w => w.DistanceKm > 0 && w.Amount != null ? w.Amount / w.DistanceKm : (decimal?)0) ?? 0,
                    PerKmCount = g.Count(w => w.DistanceKm > 0 && w.Amount != null)
                })
                .ToList();

            var lanes = parts
                .GroupBy(p => new { p.From, p.To })
                .Select(g =>
                {
                    var s = new Sum();
                    foreach (var p in g) s.Add(p, money);
                    return new { g.Key.From, g.Key.To, S = s };
                })
                .OrderByDescending(x => x.S.Trips)
                .ThenBy(x => x.From)
                .ToList();
            if (top.HasValue)
                lanes = lanes.Take(top.Value).ToList();

            Dictionary<string, string> mainForwarder = null;
            if (withMainForwarder && lanes.Count > 0)
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
                        return names.TryGetValue(best.ForwarderID.Value, out name) ? name : L.T("Log_Unknown");
                    });
            }

            return lanes.Select(x => new LanePerformanceViewModel
            {
                FromLocation = x.From,
                ToLocation = x.To,
                TripCount = x.S.Trips,
                TotalTonnage = x.S.Tonnage,
                TotalCBM = x.S.Cbm,
                TotalCost = x.S.Cost,
                TotalDistance = x.S.Distance,
                AvgCostPerTon = x.S.AvgPerTon,
                AvgCostPerKm = x.S.AvgPerKm,
                MainForwarder = mainForwarder == null
                    ? null
                    : (mainForwarder.TryGetValue(LaneKey(x.From, x.To), out var mf) ? mf : L.T("Log_Unknown"))
            }).ToList();
        }

        /// <summary>Yuk tashuvchilar bo'yicha statistika va OTIF (trip soni bo'yicha kamayish tartibida), summalar so'mda.</summary>
        public static List<ForwarderComparisonViewModel> GetForwarderStats(DBTHSNEntities db, IQueryable<F_WAYBILLS> waybills,
            int? top = null, LogisticsMoney money = null)
        {
            money = money ?? new LogisticsMoney(db);
            var parts = waybills
                .Where(w => w.ForwarderID.HasValue)
                .GroupBy(w => new { w.ForwarderID, w.CurrencyID, Day = DbFunctions.TruncateTime(w.IssueDate) })
                .Select(g => new Part
                {
                    ForwarderID = g.Key.ForwarderID,
                    CurrencyID = g.Key.CurrencyID,
                    Day = g.Key.Day,
                    Trips = g.Count(),
                    Cost = g.Sum(w => w.Amount) ?? 0,
                    Tonnage = g.Sum(w => w.GrWeight) ?? 0,
                    Distance = g.Sum(w => w.DistanceKm) ?? 0,
                    PerTonSum = g.Sum(w => w.GrWeight > 0 && w.Amount != null ? w.Amount / w.GrWeight : (decimal?)0) ?? 0,
                    PerTonCount = g.Count(w => w.GrWeight > 0 && w.Amount != null),
                    PerKmSum = g.Sum(w => w.DistanceKm > 0 && w.Amount != null ? w.Amount / w.DistanceKm : (decimal?)0) ?? 0,
                    PerKmCount = g.Count(w => w.DistanceKm > 0 && w.Amount != null),
                    OnTime = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null && w.ActualDeliveryDate <= w.PlannedDeliveryDate),
                    Delivered = g.Count(w => w.PlannedDeliveryDate != null && w.ActualDeliveryDate != null)
                })
                .ToList();

            var rows = parts
                .GroupBy(p => p.ForwarderID.Value)
                .Select(g =>
                {
                    var s = new Sum();
                    foreach (var p in g) s.Add(p, money);
                    return new { ForwarderID = g.Key, S = s };
                })
                .OrderByDescending(x => x.S.Trips)
                .ToList();
            if (top.HasValue)
                rows = rows.Take(top.Value).ToList();

            var names = ForwarderNames(db, rows.Select(x => x.ForwarderID));

            return rows.Select(x => new ForwarderComparisonViewModel
            {
                ForwarderID = x.ForwarderID,
                ForwarderName = names.TryGetValue(x.ForwarderID, out var n) ? n : L.T("Log_Unknown"),
                TripCount = x.S.Trips,
                TotalCost = x.S.Cost,
                TotalTonnage = x.S.Tonnage,
                TotalDistance = x.S.Distance,
                AvgCostPerTon = x.S.AvgPerTon,
                AvgCostPerKm = x.S.AvgPerKm,
                OnTimeDeliveries = x.S.OnTime,
                TotalDeliveries = x.S.Delivered,
                OTIFPercentage = x.S.Delivered > 0 ? (double)x.S.OnTime / x.S.Delivered * 100 : 0
            }).ToList();
        }

        /// <summary>Yo'l varaqalari to'plami bo'yicha jami ko'rsatkichlar (yo'nalish kesimida), summalar so'mda.</summary>
        public static WaybillTotals GetTotals(IQueryable<F_WAYBILLS> waybills, LogisticsMoney money)
        {
            var parts = waybills
                .GroupBy(w => new { w.Direction, w.CurrencyID, Day = DbFunctions.TruncateTime(w.IssueDate) })
                .Select(g => new Part
                {
                    Direction = g.Key.Direction,
                    CurrencyID = g.Key.CurrencyID,
                    Day = g.Key.Day,
                    Trips = g.Count(),
                    Cost = g.Sum(w => w.Amount) ?? 0,
                    Tonnage = g.Sum(w => w.GrWeight) ?? 0,
                    Cbm = g.Sum(w => w.CBM) ?? 0,
                    Distance = g.Sum(w => w.DistanceKm) ?? 0
                })
                .ToList();

            var totals = new WaybillTotals();
            foreach (var p in parts)
            {
                var cost = money.ToUzs(p.Cost, p.CurrencyID, p.Day);
                totals.Trips += p.Trips;
                totals.Cost += cost;
                totals.Tonnage += p.Tonnage;
                totals.Cbm += p.Cbm;
                totals.Distance += p.Distance;
                if (p.Direction == 0) totals.InboundCost += cost;
                else if (p.Direction == 1) totals.OutboundCost += cost;
                else if (p.Direction == 2) totals.InternalCost += cost;
            }
            return totals;
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
                .ToDictionary(x => x.ID, x => x.ForwarderName ?? L.T("Log_Unknown"));
        }

        private static string LaneKey(string from, string to) => from + "\u0001" + to;
    }

    public sealed class WaybillTotals
    {
        public int Trips { get; set; }
        public decimal Cost { get; set; }
        public decimal Tonnage { get; set; }
        public decimal Cbm { get; set; }
        public decimal Distance { get; set; }
        public decimal InboundCost { get; set; }
        public decimal OutboundCost { get; set; }
        public decimal InternalCost { get; set; }
    }
}
