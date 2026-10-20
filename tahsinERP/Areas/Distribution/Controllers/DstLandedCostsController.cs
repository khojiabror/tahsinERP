using System;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Kirimga qo'shimcha xarajat (landed cost) — tannarxga taqsimlash, 3-bosqich. Tannarx ko'rish huquqi (DstSeeCost) kerak.</summary>
    public class DstLandedCostsController : DstControllerBase
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!CanSeeCost)
            {
                filterContext.Result = RedirectToAction("Index", "ViewNotAuthorized", new { area = "" });
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Rows = db.LandedCosts.OrderByDescending(d => d.ID).Take(300).ToList();
                var ids = ((System.Collections.Generic.List<LandedCost>)ViewBag.Rows).Select(d => d.ID).ToList();
                ViewBag.IncomeCounts = db.LandedCostIncomes.Where(x => ids.Contains(x.LandedCostID)).GroupBy(x => x.LandedCostID)
                    .ToDictionary(g => g.Key, g => g.Count());
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(LandedCostBasis basis, string note, int? incomeId)
        {
            try
            {
                int id;
                using (var uow = NewUnitOfWork())
                {
                    id = LandedCostService.Create(uow, basis, note, CurrentUserId, DateTime.Now);
                    if (incomeId.HasValue) LandedCostService.AddIncome(uow.Dst, uow.Erp, id, incomeId.Value);
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

        public ActionResult Details(int id, string q)
        {
            using (var db = NewDb())
            using (var erp = new DBTHSNEntities())
            {
                var doc = db.LandedCosts.Find(id);
                if (doc == null) return HttpNotFound();
                ViewBag.Doc = doc;
                var incomeIds = db.LandedCostIncomes.Where(x => x.LandedCostID == id).Select(x => x.IncomeID).ToList();
                ViewBag.Incomes = incomeIds.Select(i => LandedCostService.Income(erp, i) ?? new LandedCostService.IncomeRow { ID = i, DocNo = "#" + i }).ToList();
                ViewBag.Items = db.LandedCostItems.Where(x => x.LandedCostID == id).OrderBy(x => x.ID).ToList();
                ViewBag.Allocation = LandedCostService.Allocate(db, erp, doc);
                if (doc.Status == DocumentStatus.Draft)
                {
                    ViewBag.Q = q;
                    ViewBag.Eligible = LandedCostService.EligibleIncomes(erp, q, DateTime.Now).Where(r => !incomeIds.Contains(r.ID)).ToList();
                }
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveHeader(int id, LandedCostBasis basis, string note) => Draft(id, (db, erp) => LandedCostService.SetHeader(db, id, basis, note));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddIncome(int id, int incomeId) => Draft(id, (db, erp) => LandedCostService.AddIncome(db, erp, id, incomeId));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveIncome(int id, int incomeId) => Draft(id, (db, erp) => LandedCostService.RemoveIncome(db, id, incomeId));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddItem(int id, string kind, string description, string amount) => Draft(id, (db, erp) =>
            LandedCostService.AddItem(db, id, kind, description,
                DstModelBinder.TryParseDecimal(amount ?? "", out var a) ? a : throw new DstValidationException(DstLocalization.T("Error_NumberFormat") + ": " + amount)));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveItem(int id, int itemId) => Draft(id, (db, erp) => LandedCostService.RemoveItem(db, id, itemId));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Post(int id) => Uow(id, uow => LandedCostService.Post(uow, id, CurrentUserId, DateTime.Now));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason) => Uow(id, uow => LandedCostService.Cancel(uow, id, CurrentUserId, reason, DateTime.Now));

        private ActionResult Draft(int id, Action<DistributionDbContext, DBTHSNEntities> action)
        {
            try
            {
                using (var db = NewDb())
                using (var erp = new DBTHSNEntities())
                    action(db, erp);
                TempData["DstSaved"] = DstLocalization.T("Saved");
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }

        private ActionResult Uow(int id, Action<DistributionUnitOfWork> action)
        {
            try
            {
                using (var uow = NewUnitOfWork())
                {
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
    }
}
