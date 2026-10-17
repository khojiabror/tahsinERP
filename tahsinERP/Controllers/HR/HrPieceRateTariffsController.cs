using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.HR;

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

            IQueryable<HR_PIECE_RATE_TARIFFS> query = db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Where(t => t.IsDeleted != true)
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

            await PopulateViewBags(productId, positionId, null, null);

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

            var tariff = await db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Where(t => t.ID == id && t.IsDeleted != true)
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

            logger.Log("HrPieceRateTariffs", "Details", await TariffLabelAsync(tariff));
            return View(tariff);
        }

        // Olish so'rovi: HR/PieceRateTariffs/yaratish
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "HrPieceRateTariffs"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            await PopulateViewBags(null, null, null, null);
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

                logger.Log("HrPieceRateTariffs", "Create", await TariffLabelAsync(tariff));
                return RedirectToAction("Index");
            }

            // Avval xato yo'lida operatsiya/valyuta ro'yxati berilmasdi va view yiqilardi
            await PopulateViewBags(tariff.ProductID, tariff.PositionID, tariff.OperationID, tariff.CurrencyID);
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

            await PopulateViewBags(tariff.ProductID, tariff.PositionID, tariff.OperationID, tariff.CurrencyID);
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
                await db.SaveChangesAsync();

                logger.Log("HrPieceRateTariffs", "Edit", await TariffLabelAsync(existingTariff));
                return RedirectToAction("Index");
            }

            await PopulateViewBags(tariff.ProductID, tariff.PositionID, tariff.OperationID, tariff.CurrencyID);
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

            var tariff = await db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Include(t => t.PRODUCT)
                .Include(t => t.HR_POSITIONS)
                .Include(t => t.HR_PIECE_RATE_OPERATIONS)
                .Include(t => t.CURRENCy)
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted != true);

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
            tariff.ModifiedByUserID = UserHelper.GetUserId(User.Identity.Name);
            tariff.ModifiedDT = DateTime.Now;
            await db.SaveChangesAsync();

            logger.Log("HrPieceRateTariffs", "Delete", await TariffLabelAsync(tariff));
            return RedirectToAction("Index");
        }

        /// <summary>Log uchun nomlar (avval har amaldan keyin 3 ta FindAsync, HRM-D11).</summary>
        private async Task<string> TariffLabelAsync(HR_PIECE_RATE_TARIFFS tariff)
        {
            var productLabel = await db.PRODUCTS.AsNoTracking()
                .Where(p => p.ID == tariff.ProductID)
                .Select(p => p.Name ?? p.PNo)
                .FirstOrDefaultAsync() ?? "-";
            var positionLabel = HrLookups.NameOf(HrLookups.Positions, tariff.PositionID) ?? "Barcha lavozimlar";
            var operationLabel = HrLookups.NameOf(HrLookups.Operations, tariff.OperationID) ?? "-";
            return $"Mahsulot: {productLabel}, Operatsiya: {operationLabel}, Lavozim: {positionLabel}";
        }

        /// <summary>Barcha amallar uchun bitta dropdown to'ldiruvchi (avval 4 to'liq nusxa, HRM-D09).</summary>
        private async Task PopulateViewBags(int? productId, int? positionId, int? operationId, int? currencyId)
        {
            var products = await db.PRODUCTS.AsNoTracking()
                .Where(p => (p.IsFinishedGood == true && p.IsDeleted != true) || p.ID == productId)
                .OrderBy(p => p.PNo)
                .Select(p => new { p.ID, p.PNo })
                .ToListAsync();

            ViewBag.ProductID = new SelectList(products, "ID", "PNo", productId);
            ViewBag.PositionID = HrLookups.SelectList(HrLookups.Positions, positionId);
            ViewBag.OperationID = HrLookups.SelectList(HrLookups.Operations, operationId);
            ViewBag.CurrencyID = ReferenceDataCache.SelectList(ReferenceDataCache.Currencies, currencyId);
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
