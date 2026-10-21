using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Distributsiya buyurtmalari: ro'yxat, yaratish/tahrirlash (yangi holatda), tasdiqlash (limit + rezerv),
    /// yig'ish, jo'natish (FIFO), yetkazildi, bekor qilish, chop etish. Qoidalar — <see cref="OrderService"/>.
    /// Ko'rinish: agent — o'z buyurtmalari, supervayzer — jamoasi; mijoz/ombor scope'lari ham qo'llanadi.
    /// </summary>
    public class DstOrdersController : DstControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Agents = AgentList(db);
                ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                    .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
                return View();
            }
        }

        private IQueryable<DstOrder> Scoped(DistributionDbContext db)
        {
            var scopes = new ScopeService(db);
            IQueryable<DstOrder> q = db.Orders;
            q = scopes.AgentsFor(CurrentUserName).Apply(q, o => o.AgentUserID);
            q = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(q, o => o.CustomerID);
            q = scopes.For(CurrentUserName, ScopeTypes.Warehouse).Apply(q, o => o.WarehouseID);
            return q;
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(byte? status, int? agentId, int? warehouseId, DateTime? from, DateTime? to)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: true);
            using (var db = NewDb())
            {
                var q = from o in Scoped(db)
                        join c in db.Customers on o.CustomerID equals c.ID
                        select new
                        {
                            o.ID, o.DocNo, o.DocDate, o.OrderStatus, o.Total, o.ShippedTotal, o.WeightKg, o.AgentUserID, o.WarehouseID,
                            o.DeliveryDate, o.IsPickup, o.Source, Customer = c.Name,
                            Agent = db.Users.Where(u => u.ID == o.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                            Warehouse = db.Warehouses.Where(w => w.ID == o.WarehouseID).Select(w => w.WHName).FirstOrDefault()
                        };
                if (status.HasValue) q = q.Where(x => x.OrderStatus == status);
                if (agentId.HasValue) q = q.Where(x => x.AgentUserID == agentId);
                if (warehouseId.HasValue) q = q.Where(x => x.WarehouseID == warehouseId);
                if (from.HasValue) q = q.Where(x => x.DocDate >= from);
                if (to.HasValue) q = q.Where(x => x.DocDate <= to);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.DocNo.Contains(s) || r.Customer.Contains(s)),
                    (x, column, desc) =>
                    {
                        switch (column)
                        {
                            case 0: return ServerTablePage.By(x, r => r.DocNo, desc).ThenBy(r => r.ID);
                            case 2: return ServerTablePage.By(x, r => r.Customer, desc).ThenBy(r => r.ID);
                            case 5: return ServerTablePage.By(x, r => r.Total, desc).ThenBy(r => r.ID);
                            default: return ServerTablePage.By(x, r => r.DocDate, desc).ThenBy(r => r.ID);
                        }
                    });

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, docNo = r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), customer = r.Customer, agent = r.Agent,
                        warehouse = r.Warehouse, status = r.OrderStatus, statusText = DstLocalization.T("OrderStatus_" + (OrderStatus)r.OrderStatus),
                        total = r.Total, shipped = r.ShippedTotal, weight = r.WeightKg,
                        delivery = r.DeliveryDate?.ToString("dd.MM.yyyy"), pickup = r.IsPickup, source = r.Source
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ---------- yaratish / tahrirlash ----------

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                object model = null;
                if (id.HasValue)
                {
                    var order = Scoped(db).FirstOrDefault(o => o.ID == id);
                    if (order == null) return HttpNotFound();
                    if (!OrderStateMachine.IsEditable((OrderStatus)order.OrderStatus))
                        return RedirectToAction("Details", new { id });
                    var customer = db.Customers.Where(c => c.ID == order.CustomerID).Select(c => c.Name).FirstOrDefault();
                    var lines = (from l in db.OrderLines
                                 where l.OrderID == order.ID && !l.IsFree   // aksiya sovg'asi saqlashda qayta hisoblanadi
                                 join p in db.Products on l.ProductID equals p.ID
                                 orderby l.LineNo
                                 select new { l.ProductID, l.ProductUnitID, l.Qty, l.DiscountPct, l.PromoPct, p.PNo, p.Name }).ToList();
                    model = new
                    {
                        id = order.ID, docNo = order.DocNo, customerId = order.CustomerID, customer, agentUserId = order.AgentUserID,
                        warehouseId = order.WarehouseID, deliveryDate = order.DeliveryDate?.ToString("yyyy-MM-dd"), isPickup = order.IsPickup,
                        comment = order.Comment, discountPct = order.DiscountPct,
                        lines = lines.Select(l => new { productId = l.ProductID, productUnitId = l.ProductUnitID, qty = l.Qty, discountPct = OrderCalculator.ManualPct(l.DiscountPct, l.PromoPct), text = l.PNo + " — " + l.Name })
                    };
                }
                ViewBag.OrderJson = Newtonsoft.Json.JsonConvert.SerializeObject(model);
                ViewBag.Agents = AgentList(db);
                ViewBag.Warehouses = StockService.SalesWarehouses(db)
                    .Join(db.Warehouses.ToList(), x => x, w => w.ID, (x, w) => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName })
                    .ToList();
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(OrderInput input)
        {
            return DstJson(() =>
            {
                using (var check = NewDb())
                {
                    if (!new ScopeService(check).For(CurrentUserName, ScopeTypes.Customer).Allows(input.CustomerID))
                        throw new DstValidationException(DstLocalization.T("Forbidden"));
                    if (input.ID.HasValue && !Scoped(check).Any(o => o.ID == input.ID))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                }
                using (var uow = NewUnitOfWork())
                {
                    var actor = OrderActor.ForUser(uow.Dst, CurrentUserName);
                    var id = OrderService.SaveDraft(uow, input, actor, DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        // ---------- tafsilot va amallar ----------

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var order = Scoped(db).FirstOrDefault(o => o.ID == id);
                if (order == null) return HttpNotFound();
                DstPaymentsController.FillPaymentLookups(ViewBag, db);
                return View(OrderDetailsModel.Load(db, order, CanSeeCost, IsRussian));
            }
        }

        private JsonResult Act(int id, Action<DistributionUnitOfWork, OrderActor> action)
        {
            return DstJson(() =>
            {
                using (var check = NewDb())
                {
                    if (!Scoped(check).Any(o => o.ID == id))
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                }
                try
                {
                    using (var uow = NewUnitOfWork())
                    {
                        var actor = OrderActor.ForUser(uow.Dst, CurrentUserName);
                        action(uow, actor);
                        uow.Commit();
                        return null;
                    }
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateConcurrencyException)
                {
                    throw new DstValidationException(DstLocalization.T("Order_Concurrency"));
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Confirm(int id, string overrideNote) =>
            Act(id, (uow, actor) => OrderService.Confirm(uow, id, actor, overrideNote, DateTime.Now));

        /// <summary>Qarz limiti sabab rad etilgan bo'lsa — UI ruxsat so'rash oynasini ko'rsatishi uchun.</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult ConfirmCheck(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var order = Scoped(db).FirstOrDefault(o => o.ID == id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var check = OrderRules.CheckCredit(LedgerService.CreditState(db, order.CustomerID, DateTime.Now), order.Total);
                    return new { ok = check.Ok, problems = check.Problems, canOverride = DstPermissions.Has(CurrentUserName, DstPermissions.OverrideCredit) };
                }
            });
        }

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Unconfirm(int id) => Act(id, (uow, a) => OrderService.Unconfirm(uow, id, a.UserId, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Rereserve(int id) => Act(id, (uow, a) => OrderService.Rereserve(uow, id, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult StartPicking(int id) => Act(id, (uow, a) => OrderService.StartPicking(uow, id, a.UserId, DateTime.Now));

        public sealed class ShipLine
        {
            public int LineId { get; set; }
            public decimal QtyBase { get; set; }
        }

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Ship(int id, List<ShipLine> lines) =>
            Act(id, (uow, a) => OrderService.Ship(uow, id, lines?.Where(l => l.QtyBase > 0).ToDictionary(l => l.LineId, l => l.QtyBase), a.UserId, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult CloseRemainder(int id, string reason) => Act(id, (uow, a) => OrderService.CloseRemainder(uow, id, a.UserId, reason, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Deliver(int id) => Act(id, (uow, a) => OrderService.MarkDelivered(uow, id, a.UserId, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Cancel(int id, string reason) => Act(id, (uow, a) => OrderService.Cancel(uow, id, a.UserId, reason, DateTime.Now));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult Delete(int id) => Act(id, (uow, a) => OrderService.DeleteDraft(uow, id));

        [HttpPost, ValidateAntiForgeryHeader]
        public JsonResult CancelShipment(int id, int shipmentId, string reason) =>
            Act(id, (uow, a) =>
            {
                if (!uow.Dst.Shipments.Any(s => s.ID == shipmentId && s.OrderID == id))
                    throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                OrderService.CancelShipment(uow, shipmentId, a.UserId, reason, DateTime.Now);
            });

        // ---------- chop etish ----------

        /// <summary>kind: picking — yig'ish varaqasi; invoice — nakladnoy (jo'natish bo'yicha, shipmentId bilan).</summary>
        public ActionResult Print(int id, string kind, int? shipmentId)
        {
            using (var db = NewDb())
            {
                var order = Scoped(db).FirstOrDefault(o => o.ID == id);
                if (order == null) return HttpNotFound();
                var model = OrderDetailsModel.Load(db, order, false, IsRussian);
                if (kind == "invoice")
                {
                    var shipment = model.Shipments.FirstOrDefault(s => s.ID == shipmentId) ?? model.Shipments.LastOrDefault();
                    if (shipment == null) return HttpNotFound();
                    ViewBag.Shipment = shipment;
                    return View("PrintInvoice", model);
                }
                return View("PrintPicking", model);
            }
        }

        // ---------- qidiruvlar (forma uchun) ----------

        [HttpGet]
        public JsonResult SearchCustomers(string q)
        {
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var query = from p in db.CustomerProfiles
                            join c in db.Customers on p.CustomerID equals c.ID
                            where c.IsDeleted != true && (q == null || c.Name.Contains(q) || p.INN == q)
                            select new { c.ID, c.Name, p.AgentUserID, p.TerritoryID, p.IsApproved };
                query = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(query, x => x.ID);
                query = scopes.AgentsFor(CurrentUserName).Apply(query, x => x.AgentUserID);
                var rows = query.OrderBy(x => x.Name).Take(30).ToList()
                    .Select(x => new { id = x.ID, text = x.Name + (x.IsApproved ? "" : " (!)") });
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult CustomerInfo(int id)
        {
            using (var db = NewDb())
            {
                var p = db.CustomerProfiles.Find(id);
                if (p == null) return Json(null, JsonRequestBehavior.AllowGet);
                var credit = LedgerService.CreditState(db, id, DateTime.Now);
                return Json(new
                {
                    agentUserId = p.AgentUserID, approved = p.IsApproved, creditLimit = p.CreditLimit, paymentDays = p.PaymentDays,
                    balance = credit.Balance, overdue = credit.Overdue,
                    priceType = db.PriceTypes.Where(t => t.ID == p.PriceTypeID).Select(t => t.Name).FirstOrDefault()
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>Tovar qidiruvi: nom/artikul yoki aniq shtrix-kod; birliklar, mijoz narxi (birlik bo'yicha), mavjud qoldiq.</summary>
        [HttpGet]
        public JsonResult SearchProducts(string q, int? customerId, int? warehouseId)
        {
            using (var db = NewDb())
            {
                var rows = CatalogService.Search(db, q, customerId, warehouseId, null, DateTime.Now).Select(p => new
                {
                    id = p.Id, text = p.Text, available = p.Available,
                    units = p.Units.Select(u => new { productUnitId = u.ProductUnitId, name = u.Name, factor = u.Factor, price = u.Price, isDefault = u.IsDefault })
                }).ToList();
                return Json(new { results = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        private List<SelectListItem> AgentList(DistributionDbContext db)
        {
            return (from a in db.SalesAgents
                    join u in db.Users on a.UserID equals u.ID
                    where a.IsActive && !a.IsExpeditor
                    orderby u.FullName
                    select new { a.UserID, Name = u.FullName ?? u.Uname }).ToList()
                .Select(a => new SelectListItem { Value = a.UserID.ToString(), Text = a.Name }).ToList();
        }
    }
}
