using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Dala nazorati: agentlarning ish kuni (boshlanishi, selfi, yakuni), tashriflar va fotolar, so'nggi joylashuv
    /// (Telegram jonli joylashuvi) va kun izi. Ko'rinish — agent ierarxiyasi bo'yicha (supervayzer — jamoasi).
    /// </summary>
    public class DstFieldController : DstControllerBase
    {
        /// <summary>Jonli joylashuv "faol" hisoblanadigan oraliq (oxirgi nuqtadan beri).</summary>
        public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(10);

        public sealed class AgentRow
        {
            public int UserID { get; set; }
            public string Name { get; set; }
            public bool IsExpeditor { get; set; }
            public DateTime? StartAt { get; set; }
            public decimal? StartLat { get; set; }
            public decimal? StartLng { get; set; }
            public string SelfieUrl { get; set; }
            public bool HasSelfie { get; set; }
            public DateTime? EndAt { get; set; }
            public int Visits { get; set; }
            public int NoOrder { get; set; }
            public int Orders { get; set; }
            public decimal OrdersTotal { get; set; }
            public AgentTrackPoint Last { get; set; }
            public bool IsLive { get; set; }
        }

        public sealed class VisitRow
        {
            public string Customer { get; set; }
            public DateTime CheckInAt { get; set; }
            public DateTime? CheckOutAt { get; set; }
            public int? DistanceM { get; set; }
            public bool IsLive { get; set; }
            public bool IsPlanned { get; set; }
            public string Result { get; set; }
            public string Reason { get; set; }
            public string PhotoUrl { get; set; }
            public bool HasPhoto { get; set; }
            public decimal? Lat { get; set; }
            public decimal? Lng { get; set; }
        }

        public sealed class FieldModel
        {
            public DateTime Date { get; set; }
            public int? UserID { get; set; }
            public bool LinksEnabled { get; set; }
            public List<AgentRow> Agents { get; set; }
            public List<VisitRow> Visits { get; set; }
            public List<AgentTrackPoint> Track { get; set; }
        }

        public ActionResult Index(DateTime? date, int? userId)
        {
            var day = (date ?? DateTime.Today).Date;
            var next = day.AddDays(1);
            var now = DateTime.Now;
            using (var db = NewDb())
            {
                var scope = new ScopeService(db).AgentsFor(CurrentUserName);
                var agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              where a.IsActive && u.IsDeleted != true
                              select new { a.UserID, Name = u.FullName ?? u.Uname, a.IsExpeditor }).ToList()
                    .Where(a => scope.Allows(a.UserID)).ToList();
                var ids = agents.Select(a => a.UserID).ToList();

                var days = db.AgentDays.Where(d => d.WorkDate == day && ids.Contains(d.UserID)).ToList().ToDictionary(d => d.UserID);
                var visits = db.Visits.Where(v => v.VisitDate == day && ids.Contains(v.UserID))
                    .Select(v => new { v.UserID, v.CustomerID, v.Result }).ToList();
                var orders = db.Orders.Where(o => o.AgentUserID != null && ids.Contains(o.AgentUserID.Value) && o.CreatedAt >= day && o.CreatedAt < next
                                                  && o.Status != DocumentStatus.Cancelled)
                    .GroupBy(o => o.AgentUserID.Value).Select(g => new { UserID = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total) }).ToList()
                    .ToDictionary(x => x.UserID);
                var last = (from t in db.AgentTrack
                            where ids.Contains(t.UserID) && t.At >= day && t.At < next
                            group t by t.UserID into g
                            select g.OrderByDescending(x => x.At).ThenByDescending(x => x.ID).FirstOrDefault()).ToList()
                    .ToDictionary(t => t.UserID);
                var links = FileLinks.For(db, DateTimeOffset.UtcNow);

                var model = new FieldModel
                {
                    Date = day, UserID = userId, LinksEnabled = links.Enabled,
                    Agents = agents.Select(a =>
                    {
                        days.TryGetValue(a.UserID, out var d);
                        orders.TryGetValue(a.UserID, out var o);
                        last.TryGetValue(a.UserID, out var p);
                        var mine = visits.Where(v => v.UserID == a.UserID).ToList();
                        return new AgentRow
                        {
                            UserID = a.UserID, Name = a.Name, IsExpeditor = a.IsExpeditor,
                            StartAt = d?.StartAt, StartLat = d?.StartLat, StartLng = d?.StartLng, EndAt = d?.EndAt,
                            HasSelfie = d?.SelfieKey != null, SelfieUrl = links.Url(d?.SelfieKey),
                            Visits = mine.Select(v => v.CustomerID).Distinct().Count(), NoOrder = mine.Count(v => v.Result == VisitResults.NoOrder),
                            Orders = o?.Count ?? 0, OrdersTotal = o?.Total ?? 0,
                            Last = p, IsLive = p != null && p.Source == TrackSources.Live && d?.EndAt == null && now - p.At <= LiveWindow
                        };
                    })
                    .OrderByDescending(r => r.StartAt.HasValue).ThenBy(r => r.Name).ToList()
                };

                if (userId.HasValue && scope.Allows(userId.Value))
                {
                    var reasons = db.Reasons.Select(r => new { r.ID, r.Name, r.NameRu }).ToList().ToDictionary(r => r.ID, r => Localized(r.Name, r.NameRu));
                    model.Visits = (from v in db.Visits
                                    join c in db.Customers on v.CustomerID equals c.ID
                                    where v.UserID == userId && v.VisitDate == day
                                    orderby v.CheckInAt
                                    select new { v, c.Name }).ToList()
                        .Select(x => new VisitRow
                        {
                            Customer = x.Name, CheckInAt = x.v.CheckInAt, CheckOutAt = x.v.CheckOutAt, DistanceM = x.v.DistanceM, IsLive = x.v.IsLive,
                            IsPlanned = x.v.IsPlanned, Result = x.v.Result, Lat = x.v.Lat, Lng = x.v.Lng,
                            Reason = x.v.NoOrderReasonID.HasValue && reasons.TryGetValue(x.v.NoOrderReasonID.Value, out var r) ? r : null,
                            HasPhoto = x.v.PhotoKey != null, PhotoUrl = links.Url(x.v.PhotoKey)
                        }).ToList();
                    model.Track = db.AgentTrack.Where(t => t.UserID == userId && t.At >= day && t.At < next)
                        .OrderBy(t => t.At).ThenBy(t => t.ID).ToList();
                }
                return View(model);
            }
        }

        /// <summary>Yandex xaritasi havolasi: bitta nuqta yoki iz (ko'pi bilan 100 nuqta, teng oraliqda tanlanadi).</summary>
        public static string MapUrl(IEnumerable<(decimal Lat, decimal Lng)> points)
        {
            var list = points.ToList();
            if (list.Count == 0) return null;
            if (list.Count > 100)
            {
                var step = (list.Count - 1) / 99.0;
                list = Enumerable.Range(0, 100).Select(i => list[(int)Math.Round(i * step)]).ToList();
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var pt = string.Join("~", list.Select(p => p.Lng.ToString(inv) + "," + p.Lat.ToString(inv)));
            return "https://yandex.uz/maps/?pt=" + pt + (list.Count == 1 ? "&z=17" : "") + "&l=map";
        }
    }
}
