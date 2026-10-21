using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Minimal qoldiqdan past tovarlar → yetkazib beruvchiga xarid buyurtmasi (SRM P_ORDERS), 3-bosqich.</summary>
    public class DstReorderController : DstControllerBase
    {
        public ActionResult Index(int? warehouseId)
        {
            using (var db = NewDb())
            using (var erp = new DBTHSNEntities())
            {
                var scope = new ScopeService(db).For(CurrentUserName, ScopeTypes.Warehouse);
                ViewBag.Rows = ReorderService.BelowMin(db, erp, warehouseId, scope.IsUnrestricted ? null : scope.Ids, DateTime.Now);
                ViewBag.Warehouses = WarehouseList(db);
                ViewBag.WarehouseId = warehouseId;
                return View();
            }
        }

        public sealed class SelectionInput
        {
            public int ProductID { get; set; }
            public int WarehouseID { get; set; }
            public string Qty { get; set; }
            public string Price { get; set; }
            public int? SupplierID { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateOrders(string data, int? warehouseId)
        {
            var input = string.IsNullOrWhiteSpace(data) ? new List<SelectionInput>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<SelectionInput>>(data);
            try
            {
                using (var db = NewDb())
                    foreach (var wh in input.Select(i => i.WarehouseID).Distinct()) EnsureWarehouse(db, wh);
                bool seeCost = CanSeeCost;   // huquqsiz — narx ko'rinmaydi, oxirgi xarid narxi serverda olinadi
                var selections = input.Select(i => new ReorderService.Selection
                {
                    ProductID = i.ProductID, WarehouseID = i.WarehouseID, SupplierID = i.SupplierID,
                    Qty = DstModelBinder.TryParseDecimal(i.Qty ?? "", out var q) ? q : 0,
                    Price = seeCost && DstModelBinder.TryParseDecimal(i.Price ?? "", out var p) ? p : (decimal?)null
                }).ToList();
                List<string> numbers;
                using (var erp = new DBTHSNEntities())
                    numbers = await ReorderService.CreateOrdersAsync(erp, selections, CurrentUserId, DateTime.Now);
                tahsinERP.Controllers.LogHelper.LogToDatabase(CurrentUserName, "DstReorder", "CreateOrders - " + string.Join(", ", numbers));
                TempData["DstSaved"] = DstLocalization.T("Reorder_Created", string.Join(", ", numbers));
            }
            catch (DstValidationException ex)
            {
                TempData["DstError"] = ex.Message;
            }
            return RedirectToAction("Index", new { warehouseId });
        }
    }
}
