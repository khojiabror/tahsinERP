using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Savdo jamoalari (supervayzer), agentlar va ekspeditorlar (ERP foydalanuvchilari), foydalanuvchi
    /// ko'rinish doiralari (dst.USER_SCOPES: ombor / hudud / mijoz).
    /// </summary>
    public class DstTeamsController : DstControllerBase
    {
        public sealed class AgentRow
        {
            public int UserID { get; set; }
            public string Name { get; set; }
            public string Login { get; set; }
            public int? TeamID { get; set; }
            public string Team { get; set; }
            public int? WarehouseID { get; set; }
            public string Warehouse { get; set; }
            public string Phone { get; set; }
            public bool IsExpeditor { get; set; }
            public bool IsActive { get; set; }
            public int Customers { get; set; }
        }

        public sealed class TeamRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? SupervisorUserID { get; set; }
            public string Supervisor { get; set; }
            public bool IsActive { get; set; }
            public int Members { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Teams = (from t in db.SalesTeams
                                 orderby t.Name
                                 select new TeamRow
                                 {
                                     ID = t.ID, Name = t.Name, SupervisorUserID = t.SupervisorUserID, IsActive = t.IsActive,
                                     Supervisor = db.Users.Where(u => u.ID == t.SupervisorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                     Members = db.SalesAgents.Count(a => a.TeamID == t.ID && a.IsActive)
                                 }).ToList();
                var agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              orderby a.IsExpeditor, u.FullName
                              select new AgentRow
                              {
                                  UserID = a.UserID, Name = u.FullName ?? u.Uname, Login = u.Uname, TeamID = a.TeamID,
                                  Team = db.SalesTeams.Where(t => t.ID == a.TeamID).Select(t => t.Name).FirstOrDefault(),
                                  WarehouseID = a.WarehouseID,
                                  Warehouse = db.Warehouses.Where(w => w.ID == a.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                                  Phone = a.Phone, IsExpeditor = a.IsExpeditor, IsActive = a.IsActive,
                                  Customers = db.CustomerProfiles.Count(p => p.AgentUserID == a.UserID)
                              }).ToList();
                FillLookups(db);
                return View(agents);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveTeam(int? id, string name, int? supervisorUserId, bool isActive = true)
        {
            return DstJson(() =>
            {
                name = name?.Trim();
                if (string.IsNullOrEmpty(name)) throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
                using (var db = NewDb())
                {
                    var team = id.HasValue
                        ? db.SalesTeams.Find(id.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"))
                        : db.SalesTeams.Add(new SalesTeam());
                    if (supervisorUserId.HasValue && !db.Users.Any(u => u.ID == supervisorUserId && u.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    team.Name = name;
                    team.SupervisorUserID = supervisorUserId;
                    team.IsActive = isActive;
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveAgent(int userId, int? teamId, int? warehouseId, string phone, bool isExpeditor, bool isActive = true)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Users.Any(u => u.ID == userId && u.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var agent = db.SalesAgents.Find(userId);
                    if (agent == null)
                        db.SalesAgents.Add(agent = new SalesAgent { UserID = userId });
                    agent.TeamID = teamId;
                    agent.WarehouseID = warehouseId;
                    agent.Phone = DirectoryRules.NormalizePhone(phone);
                    agent.IsExpeditor = isExpeditor;
                    agent.IsActive = isActive;
                    db.SaveChanges();
                    return null;
                }
            });
        }

        // ---------- ko'rinish doiralari ----------

        public ActionResult Scopes(int id)
        {
            using (var db = NewDb())
            {
                var user = db.Users.FirstOrDefault(u => u.ID == id && u.IsDeleted != true);
                if (user == null) return HttpNotFound();
                ViewBag.User = user;

                var scopes = db.UserScopes.Where(s => s.UserID == id).ToList();
                var warehouseIds = scopes.Where(s => s.ScopeType == ScopeTypes.Warehouse).Select(s => s.ScopeID).ToList();
                var territoryIds = scopes.Where(s => s.ScopeType == ScopeTypes.Territory).Select(s => s.ScopeID).ToList();
                var customerIds = scopes.Where(s => s.ScopeType == ScopeTypes.Customer).Select(s => s.ScopeID).ToList();

                ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                    .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName, Selected = warehouseIds.Contains(w.ID) }).ToList();
                ViewBag.Territories = TerritoryTree.Flatten(db.Territories.AsNoTracking().ToList(), IsRussian)
                    .Select(n => new SelectListItem { Value = n.Item.ID.ToString(), Text = n.Path, Selected = territoryIds.Contains(n.Item.ID) }).ToList();
                ViewBag.Customers = db.Customers.Where(c => customerIds.Contains(c.ID)).OrderBy(c => c.Name).ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name, Selected = true }).ToList();
                return View();
            }
        }

        /// <summary>Bitta tur bo'yicha ro'yxatni to'liq almashtiradi (bo'sh ro'yxat = cheklovsiz).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveScopes(int userId, string scopeType, int[] ids)
        {
            return DstJson(() =>
            {
                if (!ScopeTypes.All.Contains(scopeType)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                var wanted = new HashSet<int>(ids ?? new int[0]);
                using (var db = NewDb())
                {
                    if (!db.Users.Any(u => u.ID == userId)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var existing = db.UserScopes.Where(s => s.UserID == userId && s.ScopeType == scopeType).ToList();
                    foreach (var s in existing.Where(s => !wanted.Contains(s.ScopeID)))
                        db.UserScopes.Remove(s);
                    foreach (var id in wanted.Where(w => existing.All(s => s.ScopeID != w)))
                        db.UserScopes.Add(new UserScope { UserID = userId, ScopeType = scopeType, ScopeID = id });
                    db.SaveChanges();
                    return null;
                }
            });
        }

        [HttpGet]
        public JsonResult SearchCustomers(string q)
        {
            using (var db = NewDb())
            {
                var rows = (from p in db.CustomerProfiles
                            join c in db.Customers on p.CustomerID equals c.ID
                            where c.IsDeleted != true && (q == null || c.Name.Contains(q))
                            orderby c.Name
                            select new { id = c.ID, text = c.Name }).Take(30).ToList();
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        private void FillLookups(DistributionDbContext db)
        {
            ViewBag.Users = db.Users.Where(u => u.IsDeleted != true && u.IsActive).OrderBy(u => u.FullName).ToList()
                .Select(u => new SelectListItem { Value = u.ID.ToString(), Text = (u.FullName ?? u.Uname) + " (" + u.Uname + ")" }).ToList();
            ViewBag.TeamList = db.SalesTeams.Where(t => t.IsActive).OrderBy(t => t.Name).ToList()
                .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = t.Name }).ToList();
            ViewBag.WarehouseList = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
        }
    }
}
