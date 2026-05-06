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

        // Olish so'rovi: HR/PieceRateTariffs
        public async Task<ActionResult> Index(int? productId, int? positionId, bool? isActive)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            IQueryable<HR_PIECE_RATE_TARIFFS> query = db.HR_PIECE_RATE_TARIFFS
                .Where(t => t.IsDeleted == false)
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

        // Olish so'rovi: HR/PieceRateTariffs/Details/5
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
                .Where(t => t.ID == id && t.IsDeleted == false)
                .Include(t => t.PRODUCT)
                .Include(t => t.HR_POSITIONS)
                .Include(t => t.HR_PIECE_RATE_OPERATIONS)
                .Include(t => t.CURRENCy)
                .Include(t => t.USER)
                .Include(t => t.USER1)
                .FirstOrDefaultAsync();

            if (tariff == null)
            {
                return HttpNotFound();
            }

            var productLabel = tariff.PRODUCT?.Name ?? tariff.PRODUCT?.PNo ?? "-";
            var positionLabel = tariff.HR_POSITIONS?.Name ?? "Barcha lavozimlar";
            var operationLabel = tariff.HR_PIECE_RATE_OPERATIONS?.Name ?? "-";
            logger.Log("HrPieceRateTariffs", "Details", $"Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Lavozim: {positionLabel}");
            return View(tariff);
        }

        // Olish so'rovi: HR/PieceRateTariffs/yaratish
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

        // POST so'rovi: HR/PieceRateTariffs/yaratish
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
                tariff.IsDeleted = false;

                db.HR_PIECE_RATE_TARIFFS.Add(tariff);
                await db.SaveChangesAsync();

                var product = await db.PRODUCTS.FindAsync(tariff.ProductID);
                var position = tariff.PositionID.HasValue ? await db.HR_POSITIONS.FindAsync(tariff.PositionID.Value) : null;
                var operation = tariff.OperationID.HasValue ? await db.HR_PIECE_RATE_OPERATIONS.FindAsync(tariff.OperationID.Value) : null;
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                var positionLabel = position?.Name ?? "Barcha lavozimlar";
                var operationLabel = operation?.Name ?? "-";
                logger.Log("HrPieceRateTariffs", "Create", $"Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Lavozim: {positionLabel}");
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

        // Olish so'rovi: HR/PieceRateTariffs/Edit/5
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

        // POST so'rovi: HR/PieceRateTariffs/Edit/5
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

                var product = await db.PRODUCTS.FindAsync(existingTariff.ProductID);
                var position = existingTariff.PositionID.HasValue ? await db.HR_POSITIONS.FindAsync(existingTariff.PositionID.Value) : null;
                var operation = existingTariff.OperationID.HasValue ? await db.HR_PIECE_RATE_OPERATIONS.FindAsync(existingTariff.OperationID.Value) : null;
                var productLabel = product?.Name ?? product?.PNo ?? "-";
                var positionLabel = position?.Name ?? "Barcha lavozimlar";
                var operationLabel = operation?.Name ?? "-";
                logger.Log("HrPieceRateTariffs", "Edit", $"Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Lavozim: {positionLabel}");
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

        // Olish so'rovi: HR/PieceRateTariffs/o'chirish/5
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

        // POST so'rovi: HR/PieceRateTariffs/o'chirish/5
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

            tariff.IsDeleted = true;
            db.Entry(tariff).State = System.Data.Entity.EntityState.Modified;
            await db.SaveChangesAsync();

            var product = await db.PRODUCTS.FindAsync(tariff.ProductID);
            var position = tariff.PositionID.HasValue ? await db.HR_POSITIONS.FindAsync(tariff.PositionID.Value) : null;
            var operation = tariff.OperationID.HasValue ? await db.HR_PIECE_RATE_OPERATIONS.FindAsync(tariff.OperationID.Value) : null;
            var productLabel = product?.Name ?? product?.PNo ?? "-";
            var positionLabel = position?.Name ?? "Barcha lavozimlar";
            var operationLabel = operation?.Name ?? "-";
            logger.Log("HrPieceRateTariffs", "Delete", $"Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Lavozim: {positionLabel}");
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

