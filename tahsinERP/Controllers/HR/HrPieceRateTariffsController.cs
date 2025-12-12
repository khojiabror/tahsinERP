using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers.HR
{
    [Authorize]
    public class HrPieceRateTariffsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private IUserActionLogger logger;

        public HrPieceRateTariffsController()
        {
            logger = new UserActionLogger(User?.Identity?.Name ?? "System");
        }

        // GET: HR/PieceRateTariffs
        public async Task<ActionResult> Index(int? productId, int? positionId, bool? isActive)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_PIECE_RATE_TARIFFS> query = db.HR_PIECE_RATE_TARIFFS
                .Include(t => t.PRODUCT)
                .Include(t => t.HR_POSITIONS)
                .Include(t => t.HR_PIECE_RATE_OPERATIONS)
                .Include(t => t.CURRENCy);

            if (productId.HasValue)
            {
                query = query.Where(t => t.ProductID == productId.Value);
            }

            if (positionId.HasValue)
            {
                query = query.Where(t => t.PositionID == positionId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(t => t.IsActive == isActive.Value);
            }

            var tariffs = await query.OrderByDescending(t => t.CreatedDT).ToListAsync();

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo", productId);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", positionId);

            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name");

            ViewBag.CurrencyID = new SelectList(
                await db.CURRENCIES.OrderBy(c => c.Shortname).ToListAsync(),
                "ID", "Shortname");

            logger.Log("HrPieceRateTariffs", "Index");
            return View(tariffs);
        }

        // GET: HR/PieceRateTariffs/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var tariff = await db.HR_PIECE_RATE_TARIFFS
                .Include(t => t.PRODUCT)
                .Include(t => t.HR_POSITIONS)
                .Include(t => t.HR_PIECE_RATE_OPERATIONS)
                .Include(t => t.CURRENCy)
                .Include(t => t.USER)
                .Include(t => t.USER1)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (tariff == null)
            {
                return HttpNotFound();
            }

            logger.Log("HrPieceRateTariffs", "Details", $"ID: {id}");
            return View(tariff);
        }

        // GET: HR/PieceRateTariffs/Create
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo");

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name");

            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name");

            ViewBag.CurrencyID = new SelectList(
                await db.CURRENCIES.OrderBy(c => c.Shortname).ToListAsync(),
                "ID", "Shortname");

            return View();
        }

        // POST: HR/PieceRateTariffs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "ProductID,PositionID,OperationID,CurrencyID,RatePerUnit,EffectiveFrom,EffectiveTo,IsActive")] HR_PIECE_RATE_TARIFFS tariff)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                tariff.CreatedByUserID = UserHelper.GetUserId(User.Identity.Name);
                tariff.CreatedDT = DateTime.Now;

                db.HR_PIECE_RATE_TARIFFS.Add(tariff);
                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTariffs", "Create", $"ID: {tariff.ID}, ProductID: {tariff.ProductID}, PositionID: {tariff.PositionID}");
                return RedirectToAction("Index");
            }

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo", tariff.ProductID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", tariff.PositionID);

            return View(tariff);
        }

        // GET: HR/PieceRateTariffs/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var tariff = await db.HR_PIECE_RATE_TARIFFS.FindAsync(id);
            if (tariff == null)
            {
                return HttpNotFound();
            }

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo", tariff.ProductID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", tariff.PositionID);

            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name", tariff.OperationID);

            ViewBag.CurrencyID = new SelectList(
                await db.CURRENCIES.OrderBy(c => c.Shortname).ToListAsync(),
                "ID", "Shortname", tariff.CurrencyID);

            return View(tariff);
        }

        // POST: HR/PieceRateTariffs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ID,ProductID,PositionID,OperationID,CurrencyID,RatePerUnit,EffectiveFrom,EffectiveTo,IsActive")] HR_PIECE_RATE_TARIFFS tariff)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                var existingTariff = await db.HR_PIECE_RATE_TARIFFS.FindAsync(tariff.ID);
                if (existingTariff == null)
                {
                    return HttpNotFound();
                }

                existingTariff.ProductID = tariff.ProductID;
                existingTariff.PositionID = tariff.PositionID;
                existingTariff.OperationID = tariff.OperationID;
                existingTariff.CurrencyID = tariff.CurrencyID;
                existingTariff.RatePerUnit = tariff.RatePerUnit;
                existingTariff.EffectiveFrom = tariff.EffectiveFrom;
                existingTariff.EffectiveTo = tariff.EffectiveTo;
                existingTariff.IsActive = tariff.IsActive;
                existingTariff.ModifiedByUserID = UserHelper.GetUserId(User.Identity.Name);
                existingTariff.ModifiedDT = DateTime.Now;

                db.Entry(existingTariff).State = EntityState.Modified;
                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTariffs", "Edit", $"ID: {tariff.ID}");
                return RedirectToAction("Index");
            }

            ViewBag.ProductID = new SelectList(
                await db.PRODUCTS.Where(p => p.IsFinishedGood == true && p.IsDeleted == false)
                    .OrderBy(p => p.PNo).ToListAsync(),
                "ID", "PNo", tariff.ProductID);

            ViewBag.PositionID = new SelectList(
                await db.HR_POSITIONS.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "ID", "Name", tariff.PositionID);

            ViewBag.OperationID = new SelectList(
                await db.HR_PIECE_RATE_OPERATIONS.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ToListAsync(),
                "ID", "Name", tariff.OperationID);

            ViewBag.CurrencyID = new SelectList(
                await db.CURRENCIES.OrderBy(c => c.Shortname).ToListAsync(),
                "ID", "Shortname", tariff.CurrencyID);

            return View(tariff);
        }

        // GET: HR/PieceRateTariffs/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var tariff = await db.HR_PIECE_RATE_TARIFFS
                .Include(t => t.PRODUCT)
                .Include(t => t.HR_POSITIONS)
                .Include(t => t.HR_PIECE_RATE_OPERATIONS)
                .Include(t => t.CURRENCy)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (tariff == null)
            {
                return HttpNotFound();
            }

            return View(tariff);
        }

        // POST: HR/PieceRateTariffs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var tariff = await db.HR_PIECE_RATE_TARIFFS.FindAsync(id);
            if (tariff == null)
            {
                return HttpNotFound();
            }

            db.HR_PIECE_RATE_TARIFFS.Remove(tariff);
            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTariffs", "Delete", $"ID: {id}");
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

