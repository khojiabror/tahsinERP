using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Distributsiya mijozlari (savdo nuqtalari): dbo.CUSTOMERS + dst.CUSTOMER_PROFILES. Ro'yxatda faqat profili
    /// bor mijozlar (ishlab chiqarish mijozlari aralashmaydi); agent — faqat o'ziga biriktirilganlar (scope).
    /// </summary>
    public class DstCustomersController : DstControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                FillLookups(db);
                ViewBag.PendingCount = db.CustomerProfiles.Count(p => !p.IsApproved);
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(int? territoryId, int? agentId, string category, string approval)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 0, defaultDescending: false);
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var scope = scopes.For(CurrentUserName, ScopeTypes.Customer);
                var territoryScope = scopes.For(CurrentUserName, ScopeTypes.Territory);
                var agentScope = scopes.AgentsFor(CurrentUserName);

                var q = from p in db.CustomerProfiles
                        join c in db.Customers on p.CustomerID equals c.ID
                        where c.IsDeleted != true
                        select new
                        {
                            c.ID, c.Name, c.Address, c.Telephone, p.INN, p.Category, p.PointType, p.TerritoryID, p.AgentUserID,
                            p.CreditLimit, p.PaymentDays, p.IsApproved, p.Lat,
                            Territory = db.Territories.Where(t => t.ID == p.TerritoryID).Select(t => t.Name).FirstOrDefault(),
                            Agent = db.Users.Where(u => u.ID == p.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                            PriceType = db.PriceTypes.Where(t => t.ID == p.PriceTypeID).Select(t => t.Name).FirstOrDefault()
                        };

                q = scope.Apply(q, x => x.ID);
                q = territoryScope.Apply(q, x => x.TerritoryID);
                q = agentScope.Apply(q, x => x.AgentUserID);
                if (territoryId.HasValue) q = q.Where(x => x.TerritoryID == territoryId);
                if (agentId.HasValue) q = q.Where(x => x.AgentUserID == agentId);
                if (!string.IsNullOrEmpty(category)) q = q.Where(x => x.Category == category);
                if (approval == "pending") q = q.Where(x => !x.IsApproved);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(c => c.Name.Contains(s) || c.INN.Contains(s) || c.Telephone.Contains(s) || c.Address.Contains(s)),
                    (x, column, desc) =>
                    {
                        switch (column)
                        {
                            case 2: return ServerTablePage.By(x, c => c.Territory, desc).ThenBy(c => c.ID);
                            case 3: return ServerTablePage.By(x, c => c.Agent, desc).ThenBy(c => c.ID);
                            case 5: return ServerTablePage.By(x, c => c.CreditLimit, desc).ThenBy(c => c.ID);
                            default: return ServerTablePage.By(x, c => c.Name, desc).ThenBy(c => c.ID);
                        }
                    });

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(c => new
                    {
                        id = c.ID, name = c.Name, inn = c.INN, phone = c.Telephone, address = c.Address,
                        territory = c.Territory, agent = c.Agent, category = c.Category,
                        pointType = DstLocalization.T("PointType_" + c.PointType), priceType = c.PriceType,
                        creditLimit = c.CreditLimit, paymentDays = c.PaymentDays, approved = c.IsApproved, hasGeo = c.Lat != null
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                var input = new CustomerInput { PointType = PointTypes.Shop };
                if (id.HasValue)
                {
                    if (!CanAccess(db, id.Value))
                        return NotAuthorized();

                    var c = db.Customers.FirstOrDefault(x => x.ID == id && x.IsDeleted != true);
                    if (c == null) return HttpNotFound();
                    var p = db.CustomerProfiles.Find(id.Value) ?? new CustomerProfile { PointType = PointTypes.Shop };
                    input = new CustomerInput
                    {
                        ID = c.ID, Name = c.Name, Address = c.Address, City = c.City, Telephone = c.Telephone,
                        ContactPersonName = c.ContactPersonName, INN = p.INN ?? c.DUNS, Phone2 = p.Phone2, OwnerName = p.OwnerName,
                        Landmark = p.Landmark, Lat = p.Lat, Lng = p.Lng, PointType = p.PointType, Category = p.Category,
                        TerritoryID = p.TerritoryID, AgentUserID = p.AgentUserID, PriceTypeID = p.PriceTypeID,
                        VisitDays = p.VisitDays, CreditLimit = p.CreditLimit, PaymentDays = p.PaymentDays
                    };
                    ViewBag.IsApproved = p.IsApproved;
                }
                FillLookups(db);
                return View(input);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CustomerInput input, byte[] visitDay)
        {
            input.VisitDays = (byte)(visitDay?.Aggregate(0, (acc, d) => acc | d) ?? 0);
            try
            {
                using (var check = NewDb())
                {
                    if (input.ID.HasValue && !CanAccess(check, input.ID.Value))
                        return NotAuthorized();
                }

                int id;
                using (var uow = NewUnitOfWork())
                {
                    id = CustomerService.Save(uow, input, CurrentUserId, AuditActor.Web, DateTime.Now);
                    uow.Commit();
                }
                TempData["DstSaved"] = true;
                return RedirectToAction("Edit", new { id });
            }
            catch (DstValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("DstCustomers.Edit: {0}", ex);
                ModelState.AddModelError(string.Empty, DstLocalization.T("Error_SaveFailed"));
            }

            using (var db = NewDb())
                FillLookups(db);
            return View(input);
        }

        /// <summary>Agent qo'shgan yangi nuqtani tasdiqlash.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Approve(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    CustomerService.Approve(db, id, CurrentUserId, DateTime.Now);
                    db.SaveChanges();
                    return null;
                }
            });
        }

        /// <summary>Mavjud (ishlab chiqarish) mijozni distributsiyaga qo'shish — bo'sh profil yaratiladi.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Attach(int customerId)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    if (!db.Customers.Any(c => c.ID == customerId && c.IsDeleted != true))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    if (db.CustomerProfiles.Find(customerId) == null)
                    {
                        db.CustomerProfiles.Add(new CustomerProfile
                        {
                            CustomerID = customerId, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId,
                            ApprovedBy = CurrentUserId, ApprovedAt = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                    return new { id = customerId };
                }
            });
        }

        /// <summary>Profili yo'q mijozlar (qo'shish uchun qidiruv).</summary>
        [HttpGet]
        public JsonResult SearchUnattached(string q)
        {
            using (var db = NewDb())
            {
                var rows = db.Customers
                    .Where(c => c.IsDeleted != true && !db.CustomerProfiles.Any(p => p.CustomerID == c.ID)
                                && (q == null || c.Name.Contains(q)))
                    .OrderBy(c => c.Name).Take(20)
                    .Select(c => new { id = c.ID, text = c.Name }).ToList();
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>Mijoz kartasiga kirish: CUSTOMER/TERRITORY scope'lari va agent ierarxiyasi.</summary>
        private bool CanAccess(DistributionDbContext db, int customerId)
        {
            var scopes = new ScopeService(db);
            if (!scopes.For(CurrentUserName, ScopeTypes.Customer).Allows(customerId)) return false;
            var p = db.CustomerProfiles.Where(x => x.CustomerID == customerId)
                .Select(x => new { x.TerritoryID, x.AgentUserID }).FirstOrDefault();
            if (p == null) return true;   // profili yo'q (hali distributsiyaga qo'shilmagan)
            return scopes.For(CurrentUserName, ScopeTypes.Territory).Allows(p.TerritoryID)
                   && scopes.AgentsFor(CurrentUserName).Allows(p.AgentUserID);
        }

        private void FillLookups(DistributionDbContext db)
        {
            ViewBag.Territories = TerritoryTree.Flatten(db.Territories.AsNoTracking().Where(t => t.IsActive).ToList(), IsRussian)
                .Select(n => new SelectListItem { Value = n.Item.ID.ToString(), Text = n.Path }).ToList();
            ViewBag.Agents = (from a in db.SalesAgents
                              join u in db.Users on a.UserID equals u.ID
                              where a.IsActive && !a.IsExpeditor
                              orderby u.FullName
                              select new { a.UserID, Name = u.FullName ?? u.Uname }).ToList()
                .Select(a => new SelectListItem { Value = a.UserID.ToString(), Text = a.Name }).ToList();
            ViewBag.PriceTypes = db.PriceTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToList()
                .Select(t => new SelectListItem { Value = t.ID.ToString(), Text = Localized(t.Name, t.NameRu) + (t.IsDefault ? " *" : "") })
                .ToList();
            ViewBag.PointTypes = PointTypes.All
                .Select(p => new SelectListItem { Value = p, Text = DstLocalization.T("PointType_" + p) }).ToList();
        }
    }
}
