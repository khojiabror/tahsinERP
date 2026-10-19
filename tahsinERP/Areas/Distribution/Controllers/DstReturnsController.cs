using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Mijozdan qaytarish: buyurtma bo'yicha (jo'natish narxi va tannarxi) yoki buyurtmasiz; yaroqli yoki brak omborga.</summary>
    public class DstReturnsController : DstControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(DateTime? from, DateTime? to)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: true);
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var q = from r in db.Returns
                        join c in db.Customers on r.CustomerID equals c.ID
                        join pf in db.CustomerProfiles on r.CustomerID equals pf.CustomerID
                        select new
                        {
                            r.ID, r.DocNo, r.DocDate, r.Total, r.Status, r.IsDefect, r.CustomerID, r.CancelReason, pf.AgentUserID,
                            Customer = c.Name,
                            Warehouse = db.Warehouses.Where(w => w.ID == r.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                            Reason = db.Reasons.Where(x => x.ID == r.ReasonID).Select(x => x.Name).FirstOrDefault(),
                            Order = db.Orders.Where(o => o.ID == r.OrderID).Select(o => o.DocNo).FirstOrDefault()
                        };
                q = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(q, x => x.CustomerID);
                q = scopes.AgentsFor(CurrentUserName).Apply(q, x => x.AgentUserID);
                if (from.HasValue) q = q.Where(x => x.DocDate >= from);
                if (to.HasValue) q = q.Where(x => x.DocDate <= to);
                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.DocNo.Contains(s) || r.Customer.Contains(s)),
                    (x, column, desc) => ServerTablePage.By(x, r => r.DocDate, desc).ThenBy(r => r.ID));
                return Json(new
                {
                    draw = page.Draw, recordsTotal = result.Total, recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, docNo = r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), customer = r.Customer, total = r.Total,
                        warehouse = r.Warehouse, reason = r.Reason, order = r.Order, defect = r.IsDefect,
                        cancelled = r.Status == DocumentStatus.Cancelled, cancelReason = r.CancelReason
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Create(int? orderId)
        {
            using (var db = NewDb())
            {
                ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                    .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
                ViewBag.Reasons = db.Reasons.Where(r => r.Type == ReasonTypes.Return && r.IsActive).OrderBy(r => r.SortOrder).ToList()
                    .Select(r => new SelectListItem { Value = r.ID.ToString(), Text = Localized(r.Name, r.NameRu) }).ToList();
                object order = null;
                if (orderId.HasValue)
                {
                    order = (from o in db.Orders
                             where o.ID == orderId
                             join c in db.Customers on o.CustomerID equals c.ID
                             select new { id = o.ID, docNo = o.DocNo, customerId = o.CustomerID, customer = c.Name, warehouseId = o.WarehouseID })
                        .FirstOrDefault();
                }
                ViewBag.OrderJson = Newtonsoft.Json.JsonConvert.SerializeObject(order);
                return View();
            }
        }

        /// <summary>Mijozning qaytarish mumkin bo'lgan jo'natilgan qatorlari (jo'natilgan − qaytarilgan).</summary>
        [HttpGet]
        public JsonResult Returnable(int customerId, int? orderId)
        {
            using (var db = NewDb())
            {
                var rows = (from sl in db.ShipmentLines
                            join s in db.Shipments on sl.ShipmentID equals s.ID
                            join o in db.Orders on s.OrderID equals o.ID
                            join p in db.Products on sl.ProductID equals p.ID
                            where o.CustomerID == customerId && s.Status == DocumentStatus.Posted && (orderId == null || o.ID == orderId)
                            orderby s.ID descending
                            select new
                            {
                                sl.ID, sl.QtyBase, sl.Amount, s.DocNo, s.DocDate, OrderNo = o.DocNo, p.PNo, p.Name,
                                Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                                Returned = db.ReturnLines.Where(r => r.ShipmentLineID == sl.ID && r.Return.Status == DocumentStatus.Posted)
                                    .Sum(r => (decimal?)r.QtyBase) ?? 0
                            }).Take(200).ToList();
                return Json(rows.Where(r => r.QtyBase - r.Returned > 0).Select(r => new
                {
                    shipmentLineId = r.ID, shipment = r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), order = r.OrderNo,
                    product = r.PNo + " — " + r.Name, unit = r.Unit, available = r.QtyBase - r.Returned,
                    unitPrice = r.QtyBase == 0 ? 0 : Math.Round(r.Amount / r.QtyBase, 2)
                }), JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>DLOC omborning faol joylashuvlari (kirim uchun).</summary>
        [HttpGet]
        public JsonResult Locations(int warehouseId)
        {
            using (var uow = NewUnitOfWork())
            {
                bool dloc = tahsinERP.Services.Stock.StockLedger.IsDlocWarehouse(uow.Erp, warehouseId);
                var rows = dloc
                    ? uow.Erp.WAREHOUSE_LOCATIONS.Where(l => l.WarehouseID == warehouseId && l.IsActive && !l.IsDeleted)
                        .OrderBy(l => l.LocationCode).Select(l => new { id = l.ID, text = l.LocationCode + " " + (l.LocationName ?? "") }).ToList()
                        .Cast<object>().ToList()
                    : new List<object>();
                return Json(new { dloc, locations = rows }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Save(ReturnInput input)
        {
            return DstJson(() =>
            {
                using (var check = NewDb())
                {
                    if (!new ScopeService(check).For(CurrentUserName, ScopeTypes.Customer).Allows(input.CustomerID))
                        throw new DstValidationException(DstLocalization.T("Forbidden"));
                }
                using (var uow = NewUnitOfWork())
                {
                    var id = ReturnService.Create(uow, input, CurrentUserId, AuditActor.Web, DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Cancel(int id, string reason)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    ReturnService.Cancel(uow, id, CurrentUserId, reason, DateTime.Now);
                    uow.Commit();
                    return null;
                }
            });
        }
    }
}
