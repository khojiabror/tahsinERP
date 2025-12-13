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
    /// Inventory Reports for Production Warehouse
    /// Reports: DOH (Days on Hand), Coverage, Plan fulfillment
    /// </summary>
    public class InventoryReportsController : Controller
    {
        // GET: InventoryReports/ProductionWH
        public async Task<ActionResult> ProductionWH(int? warehouseId, DateTime? reportDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "ProductionWH"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var reportDateValue = reportDate ?? DateTime.Now;

                // Get Production warehouse
                var productionWarehouse = warehouseId.HasValue
                    ? await db.WAREHOUSES.FindAsync(warehouseId.Value)
                    : db.WAREHOUSES.FirstOrDefault(w => w.WHName.Contains("Production") || w.WHName.Contains("Ishlab chiqarish"));

                if (productionWarehouse == null)
                {
                    productionWarehouse = db.WAREHOUSES.FirstOrDefault();
                }

                if (productionWarehouse == null)
                {
                    ViewBag.Error = "Production ombor topilmadi";
                    return View(new InventoryReportViewModel());
                }

                var report = await GenerateInventoryReportAsync(productionWarehouse.ID, reportDateValue, db);

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != false).ToListAsync(),
                    "ID", "WHName", productionWarehouse.ID);
                ViewBag.ReportDate = reportDateValue;

                return View(report);
            }
        }

        /// <summary>
        /// Generates inventory report for Production warehouse
        /// </summary>
        private async Task<InventoryReportViewModel> GenerateInventoryReportAsync(
            int warehouseId, 
            DateTime reportDate, 
            DBTHSNEntities db)
        {
            var report = new InventoryReportViewModel
            {
                WarehouseID = warehouseId,
                ReportDate = reportDate,
                Items = new List<InventoryReportItem>()
            };

            // Get all stocks in Production warehouse
            var stocks = await db.PRODUCT_STOCKS
                .Include(s => s.PRODUCT)
                .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                .Include(s => s.UNIT)
                .Where(s => s.WHID == warehouseId && s.Amount > 0)
                .ToListAsync();

            // Get active production plans
            var activePlans = await db.PRODUCTPLANS
                .Include(p => p.PRODUCT)
                .Where(p => p.IsDeleted == false &&
                           p.StartDate <= reportDate &&
                           p.DueDate >= reportDate)
                .ToListAsync();

            var consumptionService = new ProductionConsumptionService(db);

            foreach (var stock in stocks)
            {
                // Calculate average daily consumption from active plans
                var materialPlans = activePlans
                    .Where(p => 
                    {
                        // Check if this material is used in the plan's BOM
                        // This is a simplified check - in production, you'd query BOM/NORMLINES
                        return true; // Placeholder - should check BOM relationship
                    })
                    .ToList();

                double totalPlannedQty = 0;
                int totalDays = 0;

                foreach (var plan in materialPlans)
                {
                    var days = (plan.DueDate - plan.StartDate).Days + 1;
                    totalDays += days;
                    // Get material requirement for this plan
                    var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                        plan.ProductID, plan.Amount);
                    var materialReq = requirements.FirstOrDefault(r => r.MaterialID == stock.ProductID);
                    if (materialReq != null)
                    {
                        totalPlannedQty += materialReq.TotalConsumption;
                    }
                }

                double avgDailyConsumption = totalDays > 0 ? totalPlannedQty / totalDays : 0;
                double doh = avgDailyConsumption > 0 ? stock.Amount / avgDailyConsumption : 0;

                // Calculate coverage (how many days the stock will last)
                double coverage = doh;

                // Calculate plan fulfillment (percentage of planned quantity available)
                double planFulfillment = 0;
                if (totalPlannedQty > 0)
                {
                    planFulfillment = (stock.Amount / totalPlannedQty) * 100;
                    if (planFulfillment > 100) planFulfillment = 100;
                }

                report.Items.Add(new InventoryReportItem
                {
                    ProductID = stock.ProductID,
                    ProductPNo = stock.PRODUCT?.PNo ?? "N/A",
                    ProductName = stock.PRODUCT?.Name ?? "N/A",
                    CurrentStock = stock.Amount,
                    UnitID = stock.UnitID,
                    UnitName = stock.UNIT?.ShortName ?? "N/A",
                    AvgDailyConsumption = Math.Round(avgDailyConsumption, 2),
                    DaysOnHand = Math.Round(doh, 1),
                    Coverage = Math.Round(coverage, 1),
                    PlanFulfillment = Math.Round(planFulfillment, 1),
                    IsRawMaterial = stock.PRODUCT?.IsRawMaterial ?? false,
                    RequiresCutting = stock.PRODUCT?.PRODUCT_CATEGORIES?.RequiresCutting ?? false
                });
            }

            return report;
        }

        // GET: InventoryReports/Export
        public async Task<ActionResult> Export(int warehouseId, DateTime reportDate)
        {
            using (var db = new DBTHSNEntities())
            {
                var report = await GenerateInventoryReportAsync(warehouseId, reportDate, db);
                
                // TODO: Implement Excel export
                // For now, return JSON
                return Json(report, JsonRequestBehavior.AllowGet);
            }
        }
    }

    /// <summary>
    /// Inventory report view model
    /// </summary>
    public class InventoryReportViewModel
    {
        public int WarehouseID { get; set; }
        public DateTime ReportDate { get; set; }
        public List<InventoryReportItem> Items { get; set; }
    }

    public class InventoryReportItem
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public double CurrentStock { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
        public double AvgDailyConsumption { get; set; }
        public double DaysOnHand { get; set; }
        public double Coverage { get; set; }
        public double PlanFulfillment { get; set; }
        public bool IsRawMaterial { get; set; }
        public bool RequiresCutting { get; set; }
    }
}
