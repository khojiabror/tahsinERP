using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Inventarizatsiya: boshlang'ich (qoldiqni tenglaydi) va nazorat (taqqoslash) sanoqlari.</summary>
    public class DstInventoryController : DstControllerBase
    {
        public sealed class ListRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime DocDate { get; set; }
            public string Warehouse { get; set; }
            public string Kind { get; set; }
            public DocumentStatus Status { get; set; }
            public bool Applied { get; set; }
            public int Lines { get; set; }
            public int Counted { get; set; }
            public int Differences { get; set; }
        }

        public sealed class LineInput
        {
            public int LineID { get; set; }
            public string Counted { get; set; }
            public string UnitCost { get; set; }
            public string Note { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Rows = (from c in db.InventoryCounts
                                join w in db.Warehouses on c.WarehouseID equals w.ID
                                orderby c.ID descending
                                select new ListRow
                                {
                                    ID = c.ID, DocNo = c.DocNo, DocDate = c.DocDate, Warehouse = w.WHName, Kind = c.Kind, Status = c.Status, Applied = c.Applied,
                                    Lines = db.InventoryCountRows.Count(l => l.CountID == c.ID),
                                    Counted = db.InventoryCountRows.Count(l => l.CountID == c.ID && l.CountedQty != null),
                                    Differences = db.InventoryCountRows.Count(l => l.CountID == c.ID && l.SystemQty != null && l.CountedQty != l.SystemQty)
                                }).Take(200).ToList();
                ViewBag.Warehouses = db.Warehouses.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToList()
                    .Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName }).ToList();
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(int warehouseId, string kind, string note)
        {
            try
            {
                int id;
                using (var uow = NewUnitOfWork())
                {
                    id = InventoryCountService.Create(uow, warehouseId, kind, note, CurrentUserId, DateTime.Now);
                    uow.Commit();
                }
                return RedirectToAction("Details", new { id });
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var count = db.InventoryCounts.Find(id);
                if (count == null) return HttpNotFound();
                ViewBag.Count = count;
                ViewBag.Warehouse = db.Warehouses.Where(w => w.ID == count.WarehouseID).Select(w => w.WHName).FirstOrDefault();
                ViewBag.Rows = InventoryCountService.Rows(db, count);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCounts(int id, string data)
        {
            // qatorlar JSON'da (forma kalitlari cheklovi 1000 — katta ro'yxatda buzilmasin)
            var lines = string.IsNullOrWhiteSpace(data) ? new List<LineInput>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<LineInput>>(data);
            return Draft(id, db => InventoryCountService.SaveCounts(db, id, lines.Select(l => (l.LineID, Num(l.Counted), Num(l.UnitCost), l.Note))));
        }

        private static decimal? Num(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DstModelBinder.TryParseDecimal(value, out var d) ? d : throw new DstValidationException(DstLocalization.T("Error_NumberFormat") + ": " + value);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddProduct(int id, string pno)
        {
            return Draft(id, db =>
            {
                var productId = db.Products.Where(p => p.PNo == pno && !p.IsDeleted).Select(p => (int?)p.ID).FirstOrDefault()
                                ?? throw new DstValidationException(DstLocalization.T("Import_UnknownValue", pno));
                InventoryCountService.AddProduct(db, id, productId);
            });
        }

        [HttpGet]
        public ActionResult Sheet(int id)
        {
            using (var db = NewDb())
            {
                var count = db.InventoryCounts.Find(id);
                if (count == null) return HttpNotFound();
                return File(InventoryCountService.Sheet(db, count, IsRussian), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", count.DocNo + ".xlsx");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadSheet(int id, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                || file.ContentLength > DstImportController.MaxFileBytes)
            {
                TempData["DstError"] = DstLocalization.T("Import_XlsxOnly");
                return RedirectToAction("Details", new { id });
            }
            try
            {
                using (var db = NewDb())
                {
                    var r = InventoryCountService.ImportSheet(db, id, file.InputStream);
                    if (r.Ok) TempData["DstSaved"] = DstLocalization.T("Import_Done") + ": " + (r.Created + r.Updated);
                    else TempData["DstError"] = string.Join("; ", r.Errors.Take(20).Select(e => DstLocalization.T("Row") + " " + e.Row + ": " + e.Message));
                }
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("DstInventory upload {0}: {1}", id, ex);
                TempData["DstError"] = DstLocalization.T("Import_ReadFailed");
            }
            return RedirectToAction("Details", new { id });
        }

        /// <summary>O'tkazish. apply: farqni qoldiqqa yozish (boshlang'ich sanoqda doim).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Post(int id, bool apply = false)
        {
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    await InventoryCountService.PostAsync(uow, id, apply, CurrentUserId, DateTime.Now);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            return Draft(id, db => InventoryCountService.CancelDraft(db, id, CurrentUserId, reason, DateTime.Now));
        }

        private ActionResult Draft(int id, Action<Data.DistributionDbContext> action)
        {
            try
            {
                using (var db = NewDb())
                    action(db);
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }
    }
}
