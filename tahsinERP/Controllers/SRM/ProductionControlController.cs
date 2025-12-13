using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers.SRM
{
    /// <summary>
    /// Production Control & Alerts - Checks stock availability vs production plans
    /// </summary>
    public class ProductionControlController : Controller
    {
        // GET: ProductionControl/MissingMaterials
        public async Task<ActionResult> MissingMaterials()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "ProductionControl", "MissingMaterials"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var alerts = await CheckStockAvailabilityForPlansAsync(db);
                return View(alerts);
            }
        }

        // GET: ProductionControl/CheckAvailability
        [HttpGet]
        public async Task<JsonResult> CheckAvailability(int planId)
        {
            using (var db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
                    .FirstOrDefaultAsync(p => p.ID == planId && p.IsDeleted == false);

                if (plan == null)
                {
                    return Json(new { success = false, message = "Reja topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                var alerts = await CheckStockAvailabilityForPlanAsync(plan, db);
                return Json(new { success = true, data = alerts }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Checks stock availability for all active production plans
        /// </summary>
        private async Task<List<StockAvailabilityAlert>> CheckStockAvailabilityForPlansAsync(DBTHSNEntities db)
        {
            var alerts = new List<StockAvailabilityAlert>();

            // Get all active production plans
            var plans = await db.PRODUCTPLANS
                .Include(p => p.PRODUCT)
                .Where(p => p.IsDeleted == false && 
                           p.StartDate <= DateTime.Now && 
                           p.DueDate >= DateTime.Now)
                .ToListAsync();

            // Get Production warehouse
            var productionWarehouse = db.WAREHOUSES
                .FirstOrDefault(w => w.WHName.Contains("Production") || w.WHName.Contains("Ishlab chiqarish"));

            if (productionWarehouse == null)
            {
                productionWarehouse = db.WAREHOUSES.FirstOrDefault();
            }

            if (productionWarehouse == null)
            {
                return alerts;
            }

            var consumptionService = new ProductionConsumptionService(db);

            foreach (var plan in plans)
            {
                var planAlerts = await CheckStockAvailabilityForPlanAsync(plan, db, productionWarehouse, consumptionService);
                alerts.AddRange(planAlerts);
            }

            return alerts;
        }

        /// <summary>
        /// Checks stock availability for a specific plan
        /// </summary>
        private async Task<List<StockAvailabilityAlert>> CheckStockAvailabilityForPlanAsync(
            PRODUCTPLAN plan, 
            DBTHSNEntities db, 
            Warehouse productionWarehouse = null,
            ProductionConsumptionService consumptionService = null)
        {
            var alerts = new List<StockAvailabilityAlert>();

            if (productionWarehouse == null)
            {
                productionWarehouse = db.WAREHOUSES
                    .FirstOrDefault(w => w.WHName.Contains("Production") || w.WHName.Contains("Ishlab chiqarish"));
                if (productionWarehouse == null)
                {
                    productionWarehouse = db.WAREHOUSES.FirstOrDefault();
                }
            }

            if (productionWarehouse == null)
            {
                return alerts;
            }

            if (consumptionService == null)
            {
                consumptionService = new ProductionConsumptionService(db);
            }

            // Calculate required materials for the plan
            var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                plan.ProductID, plan.Amount);

            // Check stock availability
            foreach (var req in requirements)
            {
                var stock = await db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .FirstOrDefaultAsync(s => s.WHID == productionWarehouse.ID && s.ProductID == req.MaterialID);

                var availableQty = stock?.Amount ?? 0;
                var requiredQty = req.TotalConsumption;
                var shortage = requiredQty - availableQty;

                if (shortage > 0)
                {
                    alerts.Add(new StockAvailabilityAlert
                    {
                        PlanID = plan.ID,
                        PlanProductPNo = plan.PRODUCT?.PNo ?? "N/A",
                        PlanProductName = plan.PRODUCT?.Name ?? "N/A",
                        PlanQty = plan.Amount,
                        MaterialID = req.MaterialID,
                        MaterialPNo = req.MaterialPNo,
                        MaterialName = req.MaterialName,
                        RequiredQty = requiredQty,
                        AvailableQty = availableQty,
                        Shortage = shortage,
                        RequiresCutting = req.RequiresCutting,
                        AlertLevel = shortage > requiredQty * 0.5 ? "CRITICAL" : "WARNING"
                    });
                }
            }

            return alerts;
        }
    }

    /// <summary>
    /// Stock availability alert model
    /// </summary>
    public class StockAvailabilityAlert
    {
        public int PlanID { get; set; }
        public string PlanProductPNo { get; set; }
        public string PlanProductName { get; set; }
        public double PlanQty { get; set; }
        public int MaterialID { get; set; }
        public string MaterialPNo { get; set; }
        public string MaterialName { get; set; }
        public double RequiredQty { get; set; }
        public double AvailableQty { get; set; }
        public double Shortage { get; set; }
        public bool RequiresCutting { get; set; }
        public string AlertLevel { get; set; }
    }
}
