using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Bot
{
    /// <summary>Joylashuv hisoblari (sof funksiyalar).</summary>
    public static class Geo
    {
        /// <summary>Ikki nuqta orasidagi masofa, metr (Haversine).</summary>
        public static double DistanceM(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }
    }

    public sealed class RouteStop
    {
        public int CustomerID { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Landmark { get; set; }
        public string Phone { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public bool Planned { get; set; }
        public bool Visited { get; set; }
        public string Result { get; set; }
        public decimal Debt { get; set; }
        public decimal Overdue { get; set; }
    }

    public sealed class CustomerCard
    {
        public int CustomerID { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Landmark { get; set; }
        public string Phone { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public bool Approved { get; set; }
        public string PriceType { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal Debt { get; set; }
        public decimal Overdue { get; set; }
        public int PaymentDays { get; set; }
        public List<object> LastOrders { get; set; }
        public List<Recommendation> Recommended { get; set; }
        public int? OpenVisitId { get; set; }
    }

    public sealed class Recommendation
    {
        public int ProductID { get; set; }
        public string Product { get; set; }
        public int Times { get; set; }
        public decimal AvgQtyBase { get; set; }
        public int DaysSinceLast { get; set; }
        public bool Overdue { get; set; }   // odatda oladi, lekin uzoq vaqt olmagan
    }

    /// <summary>
    /// Dala ishi (agent): ish kuni, bugungi marshrut, mijoz kartasi, tavsiya etilgan buyurtma, tashrif ("keldim") va natijasi.
    /// </summary>
    public static class FieldService
    {
        public const int DefaultRadiusM = 200;

        // ---------- ish kuni ----------

        public static AgentDay StartDay(DistributionDbContext db, int userId, decimal? lat, decimal? lng, string selfieKey, DateTime now)
        {
            var day = db.AgentDays.FirstOrDefault(d => d.UserID == userId && d.WorkDate == now.Date);
            if (day != null) return day;   // takror bosilsa — o'sha kun
            DirectoryRules.ValidateGeo(lat, lng);
            day = new AgentDay { UserID = userId, WorkDate = now.Date, StartAt = now, StartLat = lat, StartLng = lng, SelfieKey = selfieKey };
            db.AgentDays.Add(day);
            return day;
        }

        public sealed class DaySummary
        {
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public int Planned { get; set; }
            public int Visited { get; set; }
            public int Orders { get; set; }
            public decimal OrdersTotal { get; set; }
            public decimal Collected { get; set; }
            public decimal CashOnHand { get; set; }
            public int NoOrder { get; set; }
        }

        public static DaySummary Summary(DistributionDbContext db, int userId, DateTime date)
        {
            var day = db.AgentDays.FirstOrDefault(d => d.UserID == userId && d.WorkDate == date.Date);
            var visits = db.Visits.Where(v => v.UserID == userId && v.VisitDate == date.Date).ToList();
            var next = date.Date.AddDays(1);
            var orders = db.Orders.Where(o => o.AgentUserID == userId && o.CreatedAt >= date.Date && o.CreatedAt < next && o.Status != DocumentStatus.Cancelled);
            var payments = db.Payments.Where(p => p.ReceivedByUserID == userId && p.CreatedAt >= date.Date && p.CreatedAt < next && p.Status == DocumentStatus.Posted);
            return new DaySummary
            {
                StartAt = day?.StartAt, EndAt = day?.EndAt,
                Planned = Route(db, userId, date).Count(r => r.Planned),
                Visited = visits.Select(v => v.CustomerID).Distinct().Count(),
                NoOrder = visits.Count(v => v.Result == VisitResults.NoOrder),
                Orders = orders.Count(), OrdersTotal = orders.Sum(o => (decimal?)o.Total) ?? 0,
                Collected = payments.Sum(p => (decimal?)p.Amount) ?? 0,
                CashOnHand = PaymentService.OnHand(db, userId).Sum(p => p.Amount)
            };
        }

        public static DaySummary EndDay(DistributionDbContext db, int userId, decimal? lat, decimal? lng, DateTime now)
        {
            var day = db.AgentDays.FirstOrDefault(d => d.UserID == userId && d.WorkDate == now.Date)
                      ?? throw new DstValidationException(DstLocalization.T("Bot_DayNotStarted"));
            day.EndAt = now;
            day.EndLat = lat;
            day.EndLng = lng;
            foreach (var v in db.Visits.Where(v => v.UserID == userId && v.VisitDate == now.Date && v.CheckOutAt == null).ToList())
                v.CheckOutAt = now;
            db.SaveChanges();
            return Summary(db, userId, now);
        }

        // ---------- marshrut va mijozlar ----------

        /// <summary>Bugungi marshrut: tashrif kuni bugun bo'lgan o'z mijozlari + bugun tashrif buyurilgan boshqalari.</summary>
        public static List<RouteStop> Route(DistributionDbContext db, int userId, DateTime date)
        {
            byte bit = VisitDays.For(date.DayOfWeek);
            var visits = db.Visits.Where(v => v.UserID == userId && v.VisitDate == date.Date)
                .Select(v => new { v.CustomerID, v.Result }).ToList();
            var visitedIds = visits.Select(v => v.CustomerID).Distinct().ToList();
            var rows = (from p in db.CustomerProfiles
                        join c in db.Customers on p.CustomerID equals c.ID
                        where c.IsDeleted != true && ((p.AgentUserID == userId && (p.VisitDays & bit) != 0) || visitedIds.Contains(p.CustomerID))
                        orderby c.Name
                        select new { p, c.Name, c.Address, c.Telephone }).ToList();
            var debts = DebtService.Load(db, rows.Select(r => r.p.CustomerID).ToList());
            return rows.Select(r =>
            {
                var aging = debts.TryGetValue(r.p.CustomerID, out var d) ? DebtMath.Aging(d.Debits, d.Credits, date) : new AgingBuckets();
                var v = visits.Where(x => x.CustomerID == r.p.CustomerID).ToList();
                return new RouteStop
                {
                    CustomerID = r.p.CustomerID, Name = r.Name, Address = r.Address, Landmark = r.p.Landmark, Phone = r.Telephone,
                    Lat = r.p.Lat, Lng = r.p.Lng, Planned = r.p.AgentUserID == userId && (r.p.VisitDays & bit) != 0,
                    Visited = v.Count > 0, Result = v.Select(x => x.Result).LastOrDefault(x => x != null),
                    Debt = aging.Total, Overdue = aging.Overdue
                };
            }).ToList();
        }

        public static CustomerCard Card(DistributionDbContext db, int customerId, int userId, DateTime now)
        {
            var c = db.Customers.FirstOrDefault(x => x.ID == customerId && x.IsDeleted != true) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var p = db.CustomerProfiles.Find(customerId) ?? throw new DstValidationException(DstLocalization.T("Order_CustomerNotDistribution"));
            var aging = DebtService.Aging(db, customerId, now);
            return new CustomerCard
            {
                CustomerID = c.ID, Name = c.Name, Address = c.Address, Landmark = p.Landmark, Phone = c.Telephone, Lat = p.Lat, Lng = p.Lng,
                Approved = p.IsApproved, CreditLimit = p.CreditLimit, PaymentDays = p.PaymentDays, Debt = aging.Total, Overdue = aging.Overdue,
                PriceType = db.PriceTypes.Where(t => t.ID == p.PriceTypeID).Select(t => t.Name).FirstOrDefault(),
                LastOrders = db.Orders.Where(o => o.CustomerID == customerId && o.Status != DocumentStatus.Cancelled)
                    .OrderByDescending(o => o.ID).Take(5).ToList()
                    .Select(o => (object)new { id = o.ID, docNo = o.DocNo, date = o.DocDate.ToString("dd.MM.yyyy"), total = o.Total, status = ((OrderStatus)o.OrderStatus).ToString() })
                    .ToList(),
                Recommended = Recommend(db, customerId, now),
                OpenVisitId = db.Visits.Where(v => v.CustomerID == customerId && v.UserID == userId && v.VisitDate == now.Date && v.CheckOutAt == null)
                    .OrderByDescending(v => v.ID).Select(v => (int?)v.ID).FirstOrDefault()
            };
        }

        /// <summary>
        /// Tavsiya etilgan buyurtma: oxirgi 90 kunda kamida 2 marta olgan tovarlari (o'rtacha miqdor bilan);
        /// odatiy oralig'idan uzoqroq vaqt olmaganlari birinchi ("uzoq vaqt olmagan").
        /// </summary>
        public static List<Recommendation> Recommend(DistributionDbContext db, int customerId, DateTime now, int take = 10)
        {
            var fromDate = now.Date.AddDays(-90);
            var lines = (from l in db.OrderLines
                         join o in db.Orders on l.OrderID equals o.ID
                         where o.CustomerID == customerId && o.DocDate >= fromDate && o.Status != DocumentStatus.Cancelled
                         select new { l.ProductID, l.QtyBase, o.DocDate }).ToList();
            var ids = lines.Select(x => x.ProductID).Distinct().ToList();
            var names = db.Products.Where(pr => ids.Contains(pr.ID))
                .Select(pr => new { pr.ID, pr.PNo, pr.Name }).ToList().ToDictionary(x => x.ID, x => x.PNo + " — " + x.Name);
            return RecommendFrom(lines.Select(x => (x.ProductID, x.QtyBase, x.DocDate)), now)
                .Select(r => { r.Product = names.TryGetValue(r.ProductID, out var n) ? n : null; return r; })
                .Take(take).ToList();
        }

        /// <summary>Sof hisob (testlanadi).</summary>
        public static List<Recommendation> RecommendFrom(IEnumerable<(int ProductID, decimal QtyBase, DateTime Date)> history, DateTime now)
        {
            return history.GroupBy(h => h.ProductID)
                .Select(g =>
                {
                    var dates = g.Select(x => x.Date.Date).Distinct().OrderBy(d => d).ToList();
                    int since = (int)(now.Date - dates.Last()).TotalDays;
                    double avgGap = dates.Count > 1 ? (dates.Last() - dates.First()).TotalDays / (dates.Count - 1) : 0;
                    return new Recommendation
                    {
                        ProductID = g.Key, Times = dates.Count, AvgQtyBase = Math.Round(g.Average(x => x.QtyBase), 2),
                        DaysSinceLast = since, Overdue = dates.Count >= 2 && avgGap > 0 && since > avgGap * 1.5
                    };
                })
                .Where(r => r.Times >= 2)
                .OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Times).ThenBy(r => r.ProductID)
                .ToList();
        }

        // ---------- tashrif ----------

        public sealed class CheckInResult
        {
            public int VisitId { get; set; }
            public int? DistanceM { get; set; }
            public int RadiusM { get; set; }
        }

        /// <summary>
        /// "Keldim": mijozning koordinatasi bo'lsa — radius ichida bo'lishi shart (sozlama VisitRadiusM, aniqlik hisobga olinadi);
        /// koordinata yo'q bo'lsa — qabul qilinadi (masofa noma'lum). Statik (live emas) joylashuv ham qabul qilinadi, lekin belgilanadi.
        /// </summary>
        public static CheckInResult CheckIn(DistributionDbContext db, int userId, int customerId, decimal lat, decimal lng, int? accuracyM, bool isLive, DateTime now)
        {
            DirectoryRules.ValidateGeo(lat, lng);
            var p = db.CustomerProfiles.Find(customerId) ?? throw new DstValidationException(DstLocalization.T("Order_CustomerNotDistribution"));
            int radius = DstSettings.Int(db, DstSetting.VisitRadiusM) ?? DefaultRadiusM;
            int? distance = null;
            if (p.Lat.HasValue && p.Lng.HasValue)
            {
                distance = (int)Math.Round(Geo.DistanceM((double)lat, (double)lng, (double)p.Lat.Value, (double)p.Lng.Value));
                int tolerance = Math.Min(Math.Max(accuracyM ?? 0, 0), radius);   // GPS xatosi — ko'pi bilan radiusga teng qo'shimcha
                if (distance > radius + tolerance)
                    throw new DstValidationException(DstLocalization.T("Bot_TooFar", distance, radius));
            }
            if (!db.AgentDays.Any(d => d.UserID == userId && d.WorkDate == now.Date))
                StartDay(db, userId, lat, lng, null, now);

            var visit = new Visit
            {
                UserID = userId, CustomerID = customerId, VisitDate = now.Date, CheckInAt = now, Lat = lat, Lng = lng, AccuracyM = accuracyM,
                DistanceM = distance, IsLive = isLive, IsPlanned = p.AgentUserID == userId && (p.VisitDays & VisitDays.For(now.DayOfWeek)) != 0
            };
            db.Visits.Add(visit);
            db.SaveChanges();
            return new CheckInResult { VisitId = visit.ID, DistanceM = distance, RadiusM = radius };
        }

        public static void SetResult(DistributionDbContext db, int visitId, int userId, string result, int? reasonId, int? orderId, int? paymentId, string photoKey, string note, DateTime now)
        {
            var v = db.Visits.FirstOrDefault(x => x.ID == visitId && x.UserID == userId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (result == VisitResults.NoOrder)
            {
                if (!reasonId.HasValue || !db.Reasons.Any(r => r.ID == reasonId && r.Type == ReasonTypes.NoOrder))
                    throw new DstValidationException(DstLocalization.T("Bot_ReasonRequired"));
                v.NoOrderReasonID = reasonId;
            }
            if (result != null) v.Result = result;
            if (orderId.HasValue) { v.OrderID = orderId; v.Result = VisitResults.Order; }
            if (paymentId.HasValue && v.Result != VisitResults.Order) { v.PaymentID = paymentId; v.Result = VisitResults.Payment; }
            else if (paymentId.HasValue) v.PaymentID = paymentId;
            if (!string.IsNullOrWhiteSpace(photoKey)) v.PhotoKey = photoKey;
            if (!string.IsNullOrWhiteSpace(note)) v.Note = note.Trim().Length > 300 ? note.Trim().Substring(0, 300) : note.Trim();
            if (result == VisitResults.NoOrder) v.CheckOutAt = now;
        }
    }
}
