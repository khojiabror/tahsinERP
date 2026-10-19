using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Money;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Mijozlar qarzi: muddatlar bo'yicha (0–7, 8–30, 31–60, 60+), akt sverki (ekranda va chop etish).</summary>
    public class DstDebtsController : DstControllerBase
    {
        public sealed class DebtRow
        {
            public int CustomerID { get; set; }
            public string Customer { get; set; }
            public string Agent { get; set; }
            public string Territory { get; set; }
            public decimal CreditLimit { get; set; }
            public AgingBuckets Aging { get; set; }
        }

        public ActionResult Index(int? agentId, int? territoryId, string filter)
        {
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var q = from p in db.CustomerProfiles
                        join c in db.Customers on p.CustomerID equals c.ID
                        where c.IsDeleted != true
                        select new
                        {
                            p.CustomerID, c.Name, p.AgentUserID, p.TerritoryID, p.CreditLimit,
                            Agent = db.Users.Where(u => u.ID == p.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                            Territory = db.Territories.Where(t => t.ID == p.TerritoryID).Select(t => t.Name).FirstOrDefault()
                        };
                q = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(q, x => x.CustomerID);
                q = scopes.For(CurrentUserName, ScopeTypes.Territory).Apply(q, x => x.TerritoryID);
                q = scopes.AgentsFor(CurrentUserName).Apply(q, x => x.AgentUserID);
                if (agentId.HasValue) q = q.Where(x => x.AgentUserID == agentId);
                if (territoryId.HasValue) q = q.Where(x => x.TerritoryID == territoryId);
                var customers = q.ToList();

                var debts = DebtService.Load(db, customers.Select(c => c.CustomerID).ToList());
                var today = DateTime.Today;
                var rows = customers.Select(c => new DebtRow
                {
                    CustomerID = c.CustomerID, Customer = c.Name, Agent = c.Agent, Territory = c.Territory, CreditLimit = c.CreditLimit,
                    Aging = debts.TryGetValue(c.CustomerID, out var d) ? DebtMath.Aging(d.Debits, d.Credits, today) : new AgingBuckets()
                }).Where(r => r.Aging.Total > 0 || r.Aging.Advance > 0).ToList();

                if (filter == "overdue") rows = rows.Where(r => r.Aging.Overdue > 0).ToList();
                if (filter == "overlimit") rows = rows.Where(r => r.Aging.Total > r.CreditLimit).ToList();
                if (filter == "advance") rows = rows.Where(r => r.Aging.Advance > 0).ToList();

                ViewBag.Agents = db.SalesAgents.Join(db.Users, a => a.UserID, u => u.ID, (a, u) => new { a.UserID, a.IsExpeditor, Name = u.FullName ?? u.Uname })
                    .Where(a => !a.IsExpeditor).OrderBy(a => a.Name).ToList()
                    .Select(a => new SelectListItem { Value = a.UserID.ToString(), Text = a.Name, Selected = a.UserID == agentId }).ToList();
                ViewBag.Territories = TerritoryTree.Flatten(db.Territories.AsNoTracking().ToList(), IsRussian)
                    .Select(n => new SelectListItem { Value = n.Item.ID.ToString(), Text = n.Path, Selected = n.Item.ID == territoryId }).ToList();
                ViewBag.Filter = filter;
                DstPaymentsController.FillPaymentLookups(ViewBag, db);
                return View(rows.OrderByDescending(r => r.Aging.Overdue).ThenByDescending(r => r.Aging.Total).ToList());
            }
        }

        public ActionResult Statement(int id, DateTime? from, DateTime? to, bool print = false)
        {
            using (var db = NewDb())
            {
                if (!new ScopeService(db).For(CurrentUserName, ScopeTypes.Customer).Allows(id)) return NotAuthorized();
                var customer = db.Customers.FirstOrDefault(c => c.ID == id);
                if (customer == null) return HttpNotFound();
                var f = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
                var t = (to ?? DateTime.Today).Date;
                ViewBag.Customer = customer;
                ViewBag.Profile = db.CustomerProfiles.Find(id);
                ViewBag.From = f;
                ViewBag.To = t;
                ViewBag.Aging = DebtService.Aging(db, id, DateTime.Today);
                ViewBag.Company = db.Database.SqlQuery<string>("SELECT TOP 1 Name FROM dbo.COMPANIES ORDER BY ID").FirstOrDefault();
                var statement = DebtService.BuildStatement(db, id, f, t);
                return View(print ? "PrintStatement" : "Statement", statement);
            }
        }
    }
}
