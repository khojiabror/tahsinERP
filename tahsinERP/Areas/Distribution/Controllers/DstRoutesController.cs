using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Agentning tashrif rejasi: kun bo'yicha (mijoz kartasidagi tashrif kunlari andozasi ustiga — qo'shish, olib tashlash, tartib,
    /// xaritada optimallashtirish) va o'sha kunning reja/fakti (tashriflar, natija, joylashuv izi). Davr bo'yicha agentlar kesimidagi
    /// reja/fakt — Hisobotlar → Tashriflar (shu reja hisobga olinadi).
    /// </summary>
    public class DstRoutesController : DstControllerBase
    {
        public sealed class PageModel
        {
            public int? AgentId { get; set; }
            public DateTime Date { get; set; }
            public bool HasPlan { get; set; }
            public List<SelectListItem> Agents { get; set; }
            public List<PlanFactRow> Rows { get; set; } = new List<PlanFactRow>();
            public MapModel Map { get; set; }
            public int Planned => Rows.Count(r => r.Planned);
            public int PlannedVisited => Rows.Count(r => r.Planned && r.VisitId.HasValue);
            public int Unplanned => Rows.Count(r => !r.Planned);
            public decimal? CoveragePct => Planned == 0 ? (decimal?)null : Math.Round(PlannedVisited * 100m / Planned, 1);
        }

        public ActionResult Index(int? agentId, DateTime? date)
        {
            using (var db = NewDb())
            {
                var scope = new ScopeService(db).AgentsFor(CurrentUserName);
                var agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              where a.IsActive && !a.IsExpeditor && u.IsDeleted != true
                              select new { a.UserID, Name = u.FullName ?? u.Uname });
                agents = scope.Apply(agents, a => a.UserID);
                var list = agents.OrderBy(a => a.Name).ToList();
                var model = new PageModel
                {
                    Date = (date ?? DateTime.Today).Date,
                    AgentId = agentId.HasValue && list.Any(a => a.UserID == agentId) ? agentId : list.Select(a => (int?)a.UserID).FirstOrDefault(),
                    Agents = list.Select(a => new SelectListItem { Value = a.UserID.ToString(), Text = a.Name }).ToList()
                };
                if (model.AgentId.HasValue)
                {
                    int agent = model.AgentId.Value;
                    model.HasPlan = VisitPlanService.HasPlan(db, agent, model.Date);
                    model.Rows = VisitPlanService.PlanFact(db, agent, model.Date);
                    var next = model.Date.AddDays(1);
                    var track = db.AgentTrack.Where(p => p.UserID == agent && p.At >= model.Date && p.At < next).OrderBy(p => p.At)
                        .Select(p => new { p.Lat, p.Lng }).Take(2000).ToList();
                    int n = 0;
                    model.Map = new MapModel
                    {
                        Id = "routeMap", ApiKey = DeliverySettings.MapsKey(db), Language = IsRussian ? "ru_RU" : "uz_UZ",
                        Route = false,
                        FallbackUrl = DeliveryMath.RouteUrl(null, null, model.Rows.Where(r => r.Planned).Select(r => new GeoPoint { Id = r.Stop.CustomerID, Lat = r.Stop.Lat, Lng = r.Stop.Lng })),
                        Track = Thin(track.Select(p => new[] { p.Lat, p.Lng }).ToList(), 500),
                        Points = model.Rows.Where(r => r.Stop.Lat.HasValue && r.Stop.Lng.HasValue).Select(r => new MapPoint
                        {
                            Lat = r.Stop.Lat.Value, Lng = r.Stop.Lng.Value, Label = r.Planned ? (++n).ToString() : "+", Title = r.Stop.Name,
                            Text = r.VisitId.HasValue ? r.CheckInAt?.ToString("HH:mm") + " · " + DstLocalization.T("VisitResult_" + (r.Result ?? "NONE")) : DstLocalization.T("Plan_NotVisited"),
                            Color = !r.Planned ? "violet" : r.VisitId.HasValue ? "green" : model.Date < DateTime.Today ? "red" : "blue"
                        }).ToList()
                    };
                }
                return View(model);
            }
        }

        /// <summary>Iz nuqtalarini teng oraliqda siyraklashtirish (xarita uchun).</summary>
        private static List<decimal[]> Thin(List<decimal[]> points, int max)
        {
            if (points.Count <= max) return points;
            var step = (double)points.Count / max;
            return Enumerable.Range(0, max).Select(i => points[(int)(i * step)]).Concat(new[] { points[points.Count - 1] }).ToList();
        }

        [HttpGet]
        public JsonResult Customers(string q)
        {
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var query = from p in db.CustomerProfiles
                            join c in db.Customers on p.CustomerID equals c.ID
                            where c.IsDeleted != true && (q == null || c.Name.Contains(q) || p.INN == q || c.Address.Contains(q))
                            select new { c.ID, c.Name, c.Address, p.AgentUserID, p.TerritoryID };
                query = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(query, x => x.ID);
                query = scopes.For(CurrentUserName, ScopeTypes.Territory).Apply(query, x => x.TerritoryID);
                var rows = query.OrderBy(x => x.Name).Take(30).ToList()
                    .Select(x => new { id = x.ID, text = x.Name + (string.IsNullOrEmpty(x.Address) ? "" : " — " + x.Address) });
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SavePlan(int agentId, DateTime date, int[] customerIds)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureAgent(db, agentId);
                    var count = VisitPlanService.Save(db, agentId, date, customerIds ?? new int[0], UserId(), DateTime.Now);
                    return new { count };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveOptimized(int agentId, DateTime date)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    EnsureAgent(db, agentId);
                    var km = VisitPlanService.Optimize(db, agentId, date, UserId(), DateTime.Now);
                    return new { km };
                }
            });
        }

        private void EnsureAgent(DistributionDbContext db, int agentId)
        {
            if (!new ScopeService(db).AgentsFor(CurrentUserName).Allows(agentId)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        private int UserId() => tahsinERP.Models.RoleHelper.GetUserId(CurrentUserName) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
    }
}
