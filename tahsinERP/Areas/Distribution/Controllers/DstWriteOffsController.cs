using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Hisobdan chiqarish rahbar tasdig'i bilan (3-bosqich).</summary>
    public class DstWriteOffsController : DstControllerBase
    {
        public sealed class ListRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime DocDate { get; set; }
            public string Warehouse { get; set; }
            public string Reason { get; set; }
            public DocumentStatus Status { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public decimal CostTotal { get; set; }
            public string Author { get; set; }
            public int Lines { get; set; }
        }

        public sealed class LineRow
        {
            public int ProductID { get; set; }
            public string Product { get; set; }
            public string Unit { get; set; }
            public decimal QtyBase { get; set; }
            public decimal? UnitCost { get; set; }
            public decimal? CostAmount { get; set; }
            public string Note { get; set; }
        }

        private bool CanApprove => DstPermissions.Has(CurrentUserName, DstPermissions.WriteOffApprove);

        public ActionResult Index(string status)
        {
            using (var db = NewDb())
            {
                var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
                var q = scope.Apply(db.WriteOffs.AsQueryable(), w => w.WarehouseID);
                if (status == "waiting") q = q.Where(w => w.Status == DocumentStatus.Draft && w.SubmittedAt != null);
                else if (status == "draft") q = q.Where(w => w.Status == DocumentStatus.Draft && w.SubmittedAt == null);
                else if (status == "posted") q = q.Where(w => w.Status == DocumentStatus.Posted);
                ViewBag.Rows = (from w in q
                                join wh in db.Warehouses on w.WarehouseID equals wh.ID
                                orderby w.ID descending
                                select new ListRow
                                {
                                    ID = w.ID, DocNo = w.DocNo, DocDate = w.DocDate, Warehouse = wh.WHName, Status = w.Status, SubmittedAt = w.SubmittedAt,
                                    CostTotal = w.CostTotal,
                                    Reason = db.Reasons.Where(r => r.ID == w.ReasonID).Select(r => r.Name).FirstOrDefault(),
                                    Author = db.Users.Where(u => u.ID == w.CreatedBy).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                    Lines = db.WriteOffLines.Count(l => l.WriteOffID == w.ID)
                                }).Take(300).ToList();
                ViewBag.Status = status;
                ViewBag.CanApprove = CanApprove;
                return View();
            }
        }

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                WriteOff doc = null;
                if (id.HasValue)
                {
                    doc = db.WriteOffs.Find(id.Value);
                    if (doc == null) return HttpNotFound();
                    if (doc.Status != DocumentStatus.Draft) return RedirectToAction("Details", new { id });
                    ViewBag.Lines = Lines(db, doc.ID, false);
                }
                ViewBag.Doc = doc;
                FillLists(db);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(int? id, int warehouseId, int? reasonId, string note, string data)
        {
            var lines = string.IsNullOrWhiteSpace(data) ? new List<WriteOffLineInput>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<WriteOffLineInput>>(data);
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    EnsureWarehouse(uow.Dst, warehouseId);
                    id = WriteOffService.SaveDraft(uow, new WriteOffInput { ID = id, WarehouseID = warehouseId, ReasonID = reasonId, Note = note, Lines = lines },
                        CurrentUserId, DateTime.Now);
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
                var doc = db.WriteOffs.Find(id);
                if (doc == null || !new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse).Allows(doc.WarehouseID)) return HttpNotFound();
                ViewBag.Doc = doc;
                ViewBag.Lines = Lines(db, id, CanSeeCost);
                ViewBag.Warehouse = db.Warehouses.Where(w => w.ID == doc.WarehouseID).Select(w => w.WHName).FirstOrDefault();
                ViewBag.Reason = doc.ReasonID.HasValue ? db.Reasons.Where(r => r.ID == doc.ReasonID).Select(r => r.Name).FirstOrDefault() : null;
                var userIds = new[] { doc.CreatedBy, doc.SubmittedBy ?? 0, doc.PostedBy ?? 0, doc.CancelledBy ?? 0 };
                ViewBag.Users = db.Users.Where(u => userIds.Contains(u.ID)).ToList().ToDictionary(u => u.ID, u => u.FullName ?? u.Uname);
                ViewBag.CanApprove = CanApprove;
                ViewBag.IsAuthor = doc.CreatedBy == CurrentUserId;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int id) => Act(id, db => WriteOffService.Submit(db, id, CurrentUserId, DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id) => ActUow(id, uow => WriteOffService.Approve(uow, id, CurrentUserId, CanApprove, RoleHelper.IsAdminOrDeveloper(CurrentUserName), DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int id, string reason) => Act(id, db => WriteOffService.Reject(db, id, CurrentUserId, CanApprove, reason, DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelPosted(int id, string reason) => ActUow(id, uow => WriteOffService.CancelPosted(uow, id, CurrentUserId, CanApprove, reason, DateTime.Now));

        [HttpGet]
        public JsonResult SearchProducts(string q, int? warehouseId) => ProductSearch(q, warehouseId);

        /// <summary>Foto biriktirish (yuborishdan oldin) — JPEG/PNG, 5 MB gacha, ERP serverida saqlanadi (K3-32).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadPhoto(int id, System.Web.HttpPostedFileBase file)
        {
            return Act(id, db =>
            {
                if (file == null || file.ContentLength == 0 || file.ContentLength > DstLocalFiles.MaxBytes)
                    throw new DstValidationException(DstLocalization.T("File_ImageOnly"));
                Services.Warehouse.WriteOffService.SetPhoto(db, id, DstLocalFiles.SaveImage(file.InputStream, "writeoffs"));
            });
        }

        /// <summary>Foto: web'da yuklangani — ERP'dan, botdan kelgani — bot fayl havolasi orqali.</summary>
        [HttpGet]
        public ActionResult Photo(int id)
        {
            using (var db = NewDb())
            {
                var doc = db.WriteOffs.Find(id);
                if (doc == null || doc.PhotoKey == null || !new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse).Allows(doc.WarehouseID))
                    return HttpNotFound();
                if (!DstLocalFiles.IsLocal(doc.PhotoKey))
                {
                    var url = Services.Bot.FileLinks.For(db, DateTimeOffset.UtcNow).Url(doc.PhotoKey);
                    return url == null ? (ActionResult)HttpNotFound() : Redirect(url);
                }
                var path = DstLocalFiles.PathOf(doc.PhotoKey);
                if (!System.IO.File.Exists(path)) return HttpNotFound();
                return File(path, DstLocalFiles.ContentType(doc.PhotoKey));
            }
        }

        // ---------- yordamchilar ----------

        private ActionResult Act(int id, Action<DistributionDbContext> action)
        {
            try
            {
                using (var db = NewDb())
                {
                    var doc = db.WriteOffs.Find(id);
                    if (doc != null) EnsureWarehouse(db, doc.WarehouseID);
                    action(db);
                }
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }

        private ActionResult ActUow(int id, Action<DistributionUnitOfWork> action)
        {
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    var doc = uow.Dst.WriteOffs.Find(id);
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
            return (from l in db.WriteOffLines
                    where l.WriteOffID == id
                    join p in db.Products on l.ProductID equals p.ID
                    join u in db.Units on p.UnitID equals u.ID into uj
                    from u in uj.DefaultIfEmpty()
                    orderby l.ID
                    select new LineRow
                    {
                        ProductID = l.ProductID, Product = p.PNo + " — " + p.Name, Unit = u.ShortName, QtyBase = l.QtyBase, Note = l.Note,
                        UnitCost = showCost ? l.UnitCost : null, CostAmount = showCost ? l.CostAmount : null
                    }).ToList();
        }

        private void FillLists(DistributionDbContext db)
        {
            ViewBag.Warehouses = WarehouseList(db);
            ViewBag.Reasons = db.Reasons.Where(r => r.Type == ReasonTypes.WriteOff && r.IsActive).OrderBy(r => r.SortOrder).ToList()
                .Select(r => new SelectListItem { Value = r.ID.ToString(), Text = Localized(r.Name, r.NameRu) }).ToList();
        }
    }
}
