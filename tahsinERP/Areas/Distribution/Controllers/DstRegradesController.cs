using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Warehouse;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Pereso'rtitsa: bir tovar o'rniga boshqasi (tannarx saqlanadi), 3-bosqich.</summary>
    public class DstRegradesController : DstControllerBase
    {
        public sealed class ListRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime DocDate { get; set; }
            public string Warehouse { get; set; }
            public DocumentStatus Status { get; set; }
            public int Lines { get; set; }
            public decimal CostTotal { get; set; }
            public string Note { get; set; }
        }

        public sealed class LineRow
        {
            public int FromProductID { get; set; }
            public string From { get; set; }
            public string FromUnit { get; set; }
            public decimal FromQtyBase { get; set; }
            public int ToProductID { get; set; }
            public string To { get; set; }
            public string ToUnit { get; set; }
            public decimal ToQtyBase { get; set; }
            public decimal? CostAmount { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
                ViewBag.Rows = (from r in scope.Apply(db.Regrades.AsQueryable(), x => x.WarehouseID)
                                join w in db.Warehouses on r.WarehouseID equals w.ID
                                orderby r.ID descending
                                select new ListRow
                                {
                                    ID = r.ID, DocNo = r.DocNo, DocDate = r.DocDate, Warehouse = w.WHName, Status = r.Status, CostTotal = r.CostTotal, Note = r.Note,
                                    Lines = db.RegradeLines.Count(l => l.RegradeID == r.ID)
                                }).Take(300).ToList();
                return View();
            }
        }

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                Regrade doc = null;
                if (id.HasValue)
                {
                    doc = db.Regrades.Find(id.Value);
                    if (doc == null) return HttpNotFound();
                    if (doc.Status != DocumentStatus.Draft) return RedirectToAction("Details", new { id });
                    ViewBag.Lines = Lines(db, doc.ID, false);
                }
                ViewBag.Doc = doc;
                ViewBag.Warehouses = WarehouseList(db);
                ViewBag.Reasons = db.Reasons.Where(r => r.Type == ReasonTypes.WriteOff && r.IsActive).OrderBy(r => r.SortOrder).ToList()
                    .Select(r => new SelectListItem { Value = r.ID.ToString(), Text = Localized(r.Name, r.NameRu) }).ToList();
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(int? id, int warehouseId, int? reasonId, string note, string data)
        {
            var lines = string.IsNullOrWhiteSpace(data) ? new List<RegradeLineInput>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<RegradeLineInput>>(data);
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureWarehouse(uow.Dst, warehouseId);
                    id = RegradeService.SaveDraft(uow, new RegradeInput { ID = id, WarehouseID = warehouseId, ReasonID = reasonId, Note = note, Lines = lines }, CurrentUserId, DateTime.Now);
                    uow.Commit();
                }
                TempData["DstSaved"] = DstLocalization.T("Saved");
                return RedirectToAction("Details", new { id });
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
                return id.HasValue ? RedirectToAction("Edit", new { id }) : RedirectToAction("Index");
            }
        }

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var doc = db.Regrades.Find(id);
                if (doc == null || !new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse).Allows(doc.WarehouseID)) return HttpNotFound();
                ViewBag.Doc = doc;
                ViewBag.Lines = Lines(db, id, CanSeeCost);
                ViewBag.Warehouse = db.Warehouses.Where(w => w.ID == doc.WarehouseID).Select(w => w.WHName).FirstOrDefault();
                ViewBag.Reason = doc.ReasonID.HasValue ? db.Reasons.Where(r => r.ID == doc.ReasonID).Select(r => r.Name).FirstOrDefault() : null;
                ViewBag.Locations = doc.Status == DocumentStatus.Draft ? LocationList(doc.WarehouseID) : null;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Post(int id, int? locationId) => ActUow(id, uow => RegradeService.Post(uow, id, locationId, CurrentUserId, DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason) => ActUow(id, uow => RegradeService.Cancel(uow, id, CurrentUserId, reason, DateTime.Now));

        [HttpGet]
        public JsonResult SearchProducts(string q, int? warehouseId) => ProductSearch(q, warehouseId);

        private ActionResult ActUow(int id, Action<DistributionUnitOfWork> action)
        {
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    var doc = uow.Dst.Regrades.Find(id);
                    if (doc != null) EnsureWarehouse(uow.Dst, doc.WarehouseID);
                    action(uow);
                    uow.Commit();
                }
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }

        private static List<LineRow> Lines(DistributionDbContext db, int id, bool showCost)
        {
            return (from l in db.RegradeLines
                    where l.RegradeID == id
                    join a in db.Products on l.FromProductID equals a.ID
                    join b in db.Products on l.ToProductID equals b.ID
                    orderby l.ID
                    select new LineRow
                    {
                        FromProductID = a.ID, From = a.PNo + " — " + a.Name, FromQtyBase = l.FromQtyBase,
                        FromUnit = db.Units.Where(u => u.ID == a.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                        ToProductID = b.ID, To = b.PNo + " — " + b.Name, ToQtyBase = l.ToQtyBase,
                        ToUnit = db.Units.Where(u => u.ID == b.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                        CostAmount = showCost ? l.CostAmount : null
                    }).ToList();
        }
    }
}
