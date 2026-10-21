using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Ko'chirmani haqiqiy miqdor bilan qabul qilish va farq dalolatnomasi (3-bosqich).</summary>
    public class DstTransfersController : DstControllerBase
    {
        public sealed class ActRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime DocDate { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public decimal ShortageCost { get; set; }
            public decimal LossCost { get; set; }
            public decimal SurplusCost { get; set; }
            public int IncomeID { get; set; }
        }

        public sealed class ActLineRow
        {
            public string Product { get; set; }
            public string Unit { get; set; }
            public decimal SentQty { get; set; }
            public decimal ReceivedQty { get; set; }
            public decimal UnitCost { get; set; }
            public string Resolution { get; set; }
            public decimal? LossQty { get; set; }
            public string Note { get; set; }
            public decimal Diff => ReceivedQty - SentQty;
        }

        public sealed class LineInput
        {
            public int IncomeProductID { get; set; }
            public string Received { get; set; }
            public string Resolution { get; set; }
            public string LossQty { get; set; }
            public string Note { get; set; }
        }

        /// <summary>Dalolatnomani bekor qilish — ko'chirma yana qabul kutadi (K2-21).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            try
            {
                using (var uow = NewUnitOfWork())
                {
                    var act = uow.Dst.TransferActs.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    EnsureWarehouse(uow.Dst, act.ToWarehouseID);
                    TransferActService.Cancel(uow, id, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName), reason, DateTime.Now);
                    uow.Commit();
                }
                TempData["DstSaved"] = DstLocalization.T("Transfer_Cancelled");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            using (var erp = new DBTHSNEntities())
            {
                var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
                ViewBag.Pending = TransferActService.Pending(erp, scope.IsUnrestricted ? null : scope.Ids);
                ViewBag.Acts = (from a in scope.Apply(db.TransferActs.AsQueryable(), x => x.ToWarehouseID)
                                orderby a.ID descending
                                select new ActRow
                                {
                                    ID = a.ID, DocNo = a.DocNo, DocDate = a.DocDate, IncomeID = a.IncomeID,
                                    From = db.Warehouses.Where(w => w.ID == a.FromWarehouseID).Select(w => w.WHName).FirstOrDefault(),
                                    To = db.Warehouses.Where(w => w.ID == a.ToWarehouseID).Select(w => w.WHName).FirstOrDefault(),
                                    ShortageCost = a.ShortageCost, LossCost = a.LossCost, SurplusCost = a.SurplusCost
                                }).Take(200).ToList();
                return View();
            }
        }

        /// <summary>Qabul formasi: har qatorga kelgan miqdor (sukut — jo'natilgan).</summary>
        public ActionResult Receive(int id)
        {
            using (var db = NewDb())
            using (var erp = new DBTHSNEntities())
            {
                var pending = TransferActService.PendingOne(erp, id);
                if (pending == null) return HttpNotFound();
                EnsureWarehouse(db, pending.ToWarehouseID);
                ViewBag.Pending = pending;
                ViewBag.Lines = TransferActService.Lines(db, erp, id);
                ViewBag.CanReceive = TransferActService.CanReceive(db, pending.ToWarehouseID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName));
                ViewBag.Locations = LocationList(pending.ToWarehouseID);
                ViewBag.ReturnLocations = LocationList(pending.FromWarehouseID);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Receive(int id, string data, int? locationId, int? returnLocationId, string note)
        {
            var input = string.IsNullOrWhiteSpace(data) ? new List<LineInput>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<LineInput>>(data);
            try
            {
                int actId;
                using (var uow = NewUnitOfWork())
                {
                    var income = uow.Erp.INCOMES.Find(id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    EnsureWarehouse(uow.Dst, income.ReceivingWHID);
                    var lines = input.Select(l => new TransferReceiveLine
                    {
                        IncomeProductID = l.IncomeProductID, Resolution = string.IsNullOrWhiteSpace(l.Resolution) ? null : l.Resolution, Note = l.Note,
                        LossQty = DstModelBinder.TryParseDecimal(l.LossQty ?? "", out var lq) ? lq : (decimal?)null,
                        ReceivedQty = DstModelBinder.TryParseDecimal(l.Received ?? "", out var q) ? q : throw new DstValidationException(DstLocalization.T("Error_NumberFormat") + ": " + l.Received)
                    }).ToList();
                    actId = TransferActService.Receive(uow, id, lines, locationId, returnLocationId, note, CurrentUserId,
                        RoleHelper.IsAdminOrDeveloper(CurrentUserName), DateTime.Now);
                    uow.Commit();
                }
                TempData["DstSaved"] = DstLocalization.T("Transfer_Received");
                return RedirectToAction("Details", new { id = actId });
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
                return RedirectToAction("Receive", new { id });
            }
        }

        public ActionResult Details(int id)
        {
            using (var db = NewDb())
            {
                var act = db.TransferActs.Find(id);
                if (act == null || !new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse).Allows(act.ToWarehouseID)) return HttpNotFound();
                ViewBag.Act = act;
                ViewBag.From = db.Warehouses.Where(w => w.ID == act.FromWarehouseID).Select(w => w.WHName).FirstOrDefault();
                ViewBag.To = db.Warehouses.Where(w => w.ID == act.ToWarehouseID).Select(w => w.WHName).FirstOrDefault();
                ViewBag.Author = db.Users.Where(u => u.ID == act.CreatedBy).Select(u => u.FullName ?? u.Uname).FirstOrDefault();
                ViewBag.Lines = (from l in db.TransferActLines
                                 where l.ActID == id
                                 join p in db.Products on l.ProductID equals p.ID
                                 orderby l.ID
                                 select new ActLineRow
                                 {
                                     Product = p.PNo + " — " + p.Name, Unit = db.Units.Where(u => u.ID == l.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                                     SentQty = l.SentQty, ReceivedQty = l.ReceivedQty, UnitCost = l.UnitCost, Resolution = l.Resolution, LossQty = l.LossQty, Note = l.Note
                                 }).ToList();
                using (var erp = new DBTHSNEntities())
                {
                    ViewBag.IncomeDocNo = erp.INCOMES.Where(i => i.ID == act.IncomeID).Select(i => i.DocNo).FirstOrDefault();
                    ViewBag.ExpenseDocNo = act.ExpenseID.HasValue ? erp.EXPENSES.Where(e => e.ID == act.ExpenseID).Select(e => e.DocNo).FirstOrDefault() : null;
                }
                return View();
            }
        }

        /// <summary>Dalolatnomani chop etish (brauzer → PDF).</summary>
        public ActionResult Print(int id)
        {
            var result = Details(id);
            if (result is ViewResult v) v.ViewName = "Print";
            return result;
        }
    }
}
