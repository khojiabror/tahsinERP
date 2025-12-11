using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class InventoryController : Controller
    {
        // GET: Inventory
        public async Task<ActionResult> Index(int? warehouseId, int? categoryId, bool? isFinishedGood, bool? isRawMaterial)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.PRODUCT_STOCKS
                    .Include(s => s.Warehouse)
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Where(s => s.Amount > 0);

                // Filter by warehouse
                if (warehouseId.HasValue)
                {
                    query = query.Where(s => s.WHID == warehouseId.Value);
                }

                // Filter by category
                if (categoryId.HasValue)
                {
                    query = query.Where(s => s.PRODUCT.CategoryID == categoryId.Value);
                }

                // Filter by IsFinishedGood
                if (isFinishedGood.HasValue)
                {
                    if (isFinishedGood.Value)
                    {
                        query = query.Where(s => s.PRODUCT.PRODUCT_CATEGORIES != null && 
                                                  s.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood == true);
                    }
                    else
                    {
                        query = query.Where(s => s.PRODUCT.PRODUCT_CATEGORIES == null || 
                                                  s.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood != true);
                    }
                }

                // Filter by IsRawMaterial
                if (isRawMaterial.HasValue)
                {
                    if (isRawMaterial.Value)
                    {
                        query = query.Where(s => s.PRODUCT.IsRawMaterial == true || 
                                                  (s.PRODUCT.PRODUCT_CATEGORIES != null && 
                                                   s.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial == true));
                    }
                }

                var stocks = await query
                    .OrderBy(s => s.Warehouse.WHName)
                    .ThenBy(s => s.PRODUCT.PNo)
                    .ToListAsync();

                // ViewBag for filters
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId
                );

                ViewBag.Categories = new SelectList(
                    await db.PRODUCT_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                    "ID", "Name", categoryId
                );

                ViewBag.WarehouseId = warehouseId;
                ViewBag.CategoryId = categoryId;
                ViewBag.IsFinishedGood = isFinishedGood;
                ViewBag.IsRawMaterial = isRawMaterial;

                return View(stocks);
            }
        }
    }
}
