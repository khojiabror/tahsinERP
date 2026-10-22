using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using ClosedXML.Excel;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;
using tahsinERP.ViewModels;
using tahsinERP.Services.Common;
using tahsinERP.I18n;

namespace tahsinERP.Controllers
{
    public class InventoryController : Controller
    {
        // Olish so'rovi: Inventory
        public async Task<ActionResult> Index(int? warehouseId, int? categoryId, bool? isFinishedGood, bool? isRawMaterial)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);

                var query = db.PRODUCT_STOCKS
                    .AsNoTracking()
                    .Include(s => s.Warehouse)
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Where(s => s.Amount > 0);

                if (assignedWarehouseIds.Any())
                {
                    query = query.Where(s => assignedWarehouseIds.Contains(s.WHID));
                    if (warehouseId.HasValue && !assignedWarehouseIds.Contains(warehouseId.Value))
                    {
                        warehouseId = null;
                    }
                }

                // Filtrlash bo'yicha warehouse
                if (warehouseId.HasValue)
                {
                    query = query.Where(s => s.WHID == warehouseId.Value);
                }

                // Filtrlash bo'yicha category
                if (categoryId.HasValue)
                {
                    query = query.Where(s => s.PRODUCT.CategoryID == categoryId.Value);
                }

                // Filtrlash bo'yicha IsFinishedGood
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

                // Filtrlash bo'yicha IsRawMaterial
                if (isRawMaterial.HasValue)
                {
                    if (isRawMaterial.Value)
                    {
                        query = query.Where(s => s.PRODUCT.IsRawMaterial == true || 
                                                  (s.PRODUCT.PRODUCT_CATEGORIES != null && 
                                                   s.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial == true));
                    }
                }

                var rawStocks = await query
                    .OrderBy(s => s.Warehouse.WHName)
                    .ThenBy(s => s.PRODUCT.PNo)
                    .ToListAsync();

                // Prevent duplicate display when legacy duplicate rows exist for same WHID+ProductID.
                var stocks = rawStocks
                    .GroupBy(s => new { s.WHID, s.ProductID })
                    .Select(g =>
                    {
                        var first = g.First();
                        first.Amount = g.Sum(x => x.Amount);
                        return first;
                    })
                    .OrderBy(s => s.Warehouse?.WHName)
                    .ThenBy(s => s.PRODUCT?.PNo)
                    .Where(s => s.Amount > 0)
                    .ToList();

                // ViewBag uchun filters
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES
                        .Where(w => w.IsDeleted != true &&
                                    (!assignedWarehouseIds.Any() || assignedWarehouseIds.Contains(w.ID)))
                        .OrderBy(w => w.WHName)
                        .ToListAsync(),
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
                ViewBag.HasMrpWarehouses = assignedWarehouseIds.Any();
                ViewBag.DlocStockEnabled = DlocFifoSchema.IsEnabled(db);

                return View(stocks);
            }
        }

        /// <summary>Qoldiq joy (DLOC) bo'yicha — alohida sahifa.</summary>
        public async Task<ActionResult> IndexDloc(int? warehouseId, int? categoryId, bool? isFinishedGood, bool? isRawMaterial)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                if (!DlocFifoSchema.IsEnabled(db))
                {
                    TempData["Error"] = L.T("Wh_DlocDisabledRunMigration");
                    return RedirectToAction("Index");
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && warehouseId.HasValue && !assignedWarehouseIds.Contains(warehouseId.Value))
                    warehouseId = null;

                List<InventoryDlocStockRow> dlocRows;
                try
                {
                    dlocRows = DlocFifoDataAccess.GetInventoryByDloc(
                        db, warehouseId, null,
                        assignedWarehouseIds.Any() ? assignedWarehouseIds : null);
                }
                catch (Exception ex)
                {
                    dlocRows = new List<InventoryDlocStockRow>();
                    TempData["Error"] = L.T("Wh_DlocStockLoadFailed", ex.Message);
                }

                if (categoryId.HasValue)
                {
                    var catProductIds = await db.PRODUCTS
                        .Where(p => p.CategoryID == categoryId.Value && p.IsDeleted != true)
                        .Select(p => p.ID)
                        .ToListAsync();
                    dlocRows = dlocRows.Where(r => catProductIds.Contains(r.ProductID)).ToList();
                }

                if (isFinishedGood.HasValue)
                {
                    var fgIds = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .Where(p => p.IsDeleted != true)
                        .Where(p => isFinishedGood.Value
                            ? p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true
                            : p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood != true)
                        .Select(p => p.ID)
                        .ToListAsync();
                    dlocRows = dlocRows.Where(r => fgIds.Contains(r.ProductID)).ToList();
                }

                if (isRawMaterial.HasValue && isRawMaterial.Value)
                {
                    var rmIds = await db.PRODUCTS
                        .Include(p => p.PRODUCT_CATEGORIES)
                        .Where(p => p.IsDeleted != true)
                        .Where(p => p.IsRawMaterial == true ||
                                    (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsRawMaterial == true))
                        .Select(p => p.ID)
                        .ToListAsync();
                    dlocRows = dlocRows.Where(r => rmIds.Contains(r.ProductID)).ToList();
                }

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true &&
                        (!assignedWarehouseIds.Any() || assignedWarehouseIds.Contains(w.ID)))
                        .OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId);
                ViewBag.Categories = new SelectList(
                    await db.PRODUCT_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                    "ID", "Name", categoryId);
                ViewBag.WarehouseId = warehouseId;
                ViewBag.CategoryId = categoryId;
                ViewBag.IsFinishedGood = isFinishedGood;
                ViewBag.IsRawMaterial = isRawMaterial;
                ViewBag.HasMrpWarehouses = assignedWarehouseIds.Any();
                ViewBag.TotalRows = dlocRows.Count;

                return View(dlocRows);
            }
        }

        /// <summary>DLOC belgilanmagan umumiy qoldiqlarni joylashtirish.</summary>
        public async Task<ActionResult> AssignDloc(int? warehouseId)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Inventory"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                if (!DlocFifoSchema.IsEnabled(db))
                {
                    TempData["Error"] = L.T("Wh_DlocDisabledRunMigration");
                    return RedirectToAction("Index");
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && warehouseId.HasValue && !assignedWarehouseIds.Contains(warehouseId.Value))
                    warehouseId = null;

                var dlocWarehouses = await db.WAREHOUSES
                    .Where(w => w.IsDeleted != true &&
                                (!assignedWarehouseIds.Any() || assignedWarehouseIds.Contains(w.ID)))
                    .OrderBy(w => w.WHName)
                    .ToListAsync();

                dlocWarehouses = dlocWarehouses
                    .Where(w => DlocFifoDataAccess.WarehouseHasLocations(db, w.ID))
                    .ToList();

                ViewBag.Warehouses = new SelectList(dlocWarehouses, "ID", "WHName", warehouseId);
                ViewBag.WarehouseId = warehouseId;

                var rows = warehouseId.HasValue
                    ? LegacyDlocAssignmentService.GetUnassignedRows(db, warehouseId, assignedWarehouseIds.Any() ? assignedWarehouseIds : null)
                    : new List<UnassignedDlocStockRow>();

                if (warehouseId.HasValue)
                {
                    var locations = DlocFifoDataAccess.GetLocations(db, warehouseId.Value, activeOnly: true);
                    ViewBag.DlocLocations = locations;
                    ViewBag.DlocLocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                        locations.Select(l => new
                        {
                            l.ID,
                            l.LocationCode,
                            l.LocationName,
                            l.MaxCapacity,
                            CurrentQty = DlocFifoDataAccess.GetLocationCurrentQty(db, l.ID)
                        }));
                }

                var model = new AssignDlocPageViewModel
                {
                    WarehouseId = warehouseId,
                    Rows = rows
                };

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignDloc(AssignDlocPostViewModel model)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Inventory"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            using (var db = new DBTHSNEntities())
            {
                if (!DlocFifoSchema.IsEnabled(db))
                {
                    TempData["Error"] = L.T("Wh_DlocDisabled");
                    return RedirectToAction("Index");
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(model.WarehouseId))
                {
                    TempData["Error"] = L.T("Wh_NoAccessToWarehouse");
                    return RedirectToAction("AssignDloc");
                }

                try
                {
                    var requests = (model.Lines ?? new List<AssignDlocPostLine>())
                        .Where(l => l.Selected && l.LocationID > 0 && l.AssignQty > 0)
                        .Select(l => new AssignDlocLineRequest
                        {
                            ProductID = l.ProductID,
                            LocationID = l.LocationID,
                            AssignQty = l.AssignQty
                        })
                        .ToList();

                    var count = LegacyDlocAssignmentService.AssignToLocations(
                        db, model.WarehouseId, requests, User.Identity.Name);

                    TempData["Success"] = L.T("Wh_Asg_Done", count);
                    return RedirectToAction("AssignDloc", new { warehouseId = model.WarehouseId });
                }
                catch (WarehouseDlocException wex)
                {
                    TempData["Error"] = wex.Message;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;
                }

                return RedirectToAction("AssignDloc", new { warehouseId = model.WarehouseId });
            }
        }

        // Olish so'rovi: Inventory/Adjust
        public async Task<ActionResult> Adjust()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Inventory"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (!assignedWarehouseIds.Any())
                {
                    ViewBag.NoWarehouseError = L.T("Wh_NoAssignedWarehouse");
                    return View(new ViewModels.InventoryAdjustmentViewModel());
                }

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES
                        .Where(w => assignedWarehouseIds.Contains(w.ID) && w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .ToListAsync(),
                    "ID", "WHName"
                );

                ViewBag.LockedWarehouseIds = await InventoryAdjustmentService.GetLockedWarehouseIdsAsync(db, assignedWarehouseIds);

                return View(new ViewModels.InventoryAdjustmentViewModel());
            }
        }

        // Olish so'rovi: Inventory/DownloadTemplate
        public ActionResult DownloadTemplate()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Template");
                ws.Cell(1, 1).Value = "ProductPNo";
                ws.Cell(1, 2).Value = "Quantity";
                ws.Cell(2, 1).Value = "00000001";
                ws.Cell(2, 2).Value = 10;
                ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

                var bytes = workbook.ToByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Inventory_Adjustment_Template.xlsx");
            }
        }

        // POST so'rovi: Inventory/Adjust
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Adjust(ViewModels.InventoryAdjustmentViewModel model)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Inventory"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                    if (!assignedWarehouseIds.Any())
                    {
                        ModelState.AddModelError("", L.T("Wh_NoAssignedWarehouse"));
                    }
                    else if (!assignedWarehouseIds.Contains(model.WarehouseId))
                    {
                        ModelState.AddModelError("", L.T("Wh_Adj_OnlyAssigned"));
                    }

                    if (!model.Confirmed)
                    {
                        ModelState.AddModelError("", L.T("Wh_Adj_MustAcceptRules"));
                    }

                    if (model.ExcelFile == null || model.ExcelFile.ContentLength == 0)
                    {
                        ModelState.AddModelError("", L.T("Wh_Adj_NoFile"));
                    }

                    var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                    var lockedWarehouseIds = await InventoryAdjustmentService.GetLockedWarehouseIdsAsync(db, assignedWarehouseIds);
                    if (lockedWarehouseIds.Contains(model.WarehouseId))
                    {
                        ModelState.AddModelError("", L.T("Wh_Adj_AlreadyThisMonth"));
                    }

                    if (!ModelState.IsValid)
                    {
                        await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                        ViewBag.LockedWarehouseIds = lockedWarehouseIds;
                        return View(model);
                    }

                    using (var workbook = new XLWorkbook(model.ExcelFile.InputStream))
                    {
                        var worksheet = workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null || worksheet.LastRowNumber() == 0)
                        {
                            ModelState.AddModelError("", L.T("Wh_Adj_EmptyFile"));
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        var cells = new ExcelCellReader(worksheet)
                            .Alias("productpno", "productpno", "pno", "mahsulot")
                            .Alias("quantity", "quantity", "qty", "miqdor", "qoldiq");
                        if (!cells.Has("productpno") || !cells.Has("quantity"))
                        {
                            ModelState.AddModelError("", L.T("Wh_Adj_BadFormat"));
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        // SRM-P06: faqat kerakli ustunlar (avval PRODUCTS ning barcha ustunlari xotiraga olinardi)
                        var productMap = (await db.PRODUCTS
                                .AsNoTracking()
                                .Where(p => p.PNo != null && p.PNo != "")
                                .Select(p => new { p.ID, p.PNo, p.Name, p.UnitID })
                                .ToListAsync())
                            .Where(p => !string.IsNullOrWhiteSpace(p.PNo))
                            .GroupBy(p => p.PNo.Trim().ToUpperInvariant())
                            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ID).First());

                        var parsedRows = new List<InventoryAdjustmentRow>();
                        var errorDetails = new List<string>();
                        var seenProducts = new HashSet<int>();
                        var lastRow = worksheet.LastRowNumber();

                        for (int row = 2; row <= lastRow; row++)
                        {
                            var pno = cells.String(row, "productpno") ?? string.Empty;
                            var qtyValue = cells.String(row, "quantity") ?? string.Empty;

                            if (string.IsNullOrWhiteSpace(pno))
                            {
                                if (string.IsNullOrWhiteSpace(qtyValue))
                                {
                                    continue;
                                }
                                errorDetails.Add(L.T("Wh_Adj_RowEmptyPno", row));
                                continue;
                            }

                            var key = pno.Trim().ToUpperInvariant();
                            if (!productMap.TryGetValue(key, out var product))
                            {
                                errorDetails.Add(L.T("Wh_Adj_RowProductNotFound", row, pno));
                                continue;
                            }

                            if (!TryParseQuantity(qtyValue, out var qty))
                            {
                                errorDetails.Add(L.T("Wh_Adj_RowBadQty", row, qtyValue));
                                continue;
                            }

                            if (qty < 0)
                            {
                                errorDetails.Add(L.T("Wh_Adj_RowNegativeQty", row, qtyValue));
                                continue;
                            }

                            if (!seenProducts.Add(product.ID))
                            {
                                errorDetails.Add(L.T("Wh_Adj_RowDuplicate", row, product.PNo));
                                continue;
                            }

                            parsedRows.Add(new InventoryAdjustmentRow
                            {
                                ProductPNo = product.PNo,
                                Quantity = qty,
                                RowNumber = row
                            });
                        }

                        if (errorDetails.Any())
                        {
                            ViewBag.ErrorDetails = errorDetails;
                            ModelState.AddModelError("", L.T("Wh_Adj_FileHasErrors"));
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        // DLOC yoqilgan omborda kirim/chiqim ledger orqali yoziladi (hisobotlar ham shundan o'qiydi).
                        var requireDloc = StockLedger.IsDlocWarehouse(db, model.WarehouseId);

                        // SRM-B09: joriy qoldiq — PRODUCT_STOCKS (ledger + joylashtirilmagan). Avval DLOC omborda faqat
                        // ledger olinardi: joylashtirilmagan 100 ta bor omborda 100 sanalsa +100 kirim qilinib, qoldiq 200
                        // bo'lib qolardi. Joylashtirilmagan qoldiq bor mahsulot bo'yicha inventarizatsiya rad etiladi.
                        if (requireDloc)
                        {
                            var unassignedErrors = new List<string>();
                            var onHandMap = StockLedger.GetOnHand(db, model.WarehouseId,
                                parsedRows.Select(r => productMap[r.ProductPNo.Trim().ToUpperInvariant()].ID));
                            var ledgerMap = DlocFifoDataAccess.GetLedgerQtyByProduct(db, model.WarehouseId);
                            foreach (var parsedRow in parsedRows)
                            {
                                var product = productMap[parsedRow.ProductPNo.Trim().ToUpperInvariant()];
                                decimal onHand;
                                onHandMap.TryGetValue(product.ID, out onHand);
                                if (onHand <= StockLedger.Epsilon)
                                    continue;
                                decimal ledger;
                                ledgerMap.TryGetValue(product.ID, out ledger);
                                var unassigned = onHand - ledger;
                                if (unassigned > StockLedger.Epsilon)
                                {
                                    unassignedErrors.Add(L.T("Wh_Adj_RowUnassigned", parsedRow.RowNumber, product.PNo, unassigned.ToString("N3")));
                                }
                            }

                            if (unassignedErrors.Any())
                            {
                                ViewBag.ErrorDetails = unassignedErrors;
                                ModelState.AddModelError("", L.T("Wh_Adj_UnassignedStock"));
                                await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                                return View(model);
                            }
                        }

                        using (var transaction = db.Database.BeginTransaction())
                        {
                            try
                            {
                                // Yozish — InventoryAdjustmentService (SRM-M01): qulf, INV kirim/chiqim, DLOC joylash, FIFO tannarx
                                var countLines = parsedRows.Select(r =>
                                {
                                    var product = productMap[r.ProductPNo.Trim().ToUpperInvariant()];
                                    return new InventoryCountLine
                                    {
                                        RowNumber = r.RowNumber,
                                        ProductPNo = product.PNo,
                                        ProductID = product.ID,
                                        UnitID = product.UnitID > 0 ? product.UnitID : 1,
                                        Quantity = r.Quantity
                                    };
                                }).ToList();
                                await InventoryAdjustmentService.ApplyAsync(db, model.WarehouseId,
                                    currentUserId > 0 ? (int?)currentUserId : null, countLines);
                                transaction.Commit();

                                // Faqat audit uchun (qulf endi INVENTORY_ADJUSTMENTS'da)
                                tahsinERP.Controllers.LogHelper.LogToDatabase(
                                    User.Identity.Name,
                                    "InventoryAdjustment",
                                    $"Apply-WH-{model.WarehouseId}",
                                    null,
                                    HttpContext);

                                TempData["Success"] = L.T("Wh_Adj_Done");
                                return RedirectToAction("Index");
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                ModelState.AddModelError("", L.T("Wh_ErrorWithMessage", ex.Message));
                                await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                                return View(model);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = WarehouseAccess.ErrorMessage(ex);
                ModelState.AddModelError("", errorMessage);
                using (var db = new DBTHSNEntities())
                {
                    var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                    await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                    ViewBag.LockedWarehouseIds = await InventoryAdjustmentService.GetLockedWarehouseIdsAsync(db, assignedWarehouseIds);
                }
                return View(model);
            }
        }

        private Task<List<int>> GetAssignedWarehouseIdsAsync(DBTHSNEntities db)
        {
            return WarehouseAccess.AssignedWarehouseIdsAsync(db, User.Identity.Name);
        }

        private async Task PopulateAdjustmentViewBags(DBTHSNEntities db, List<int> assignedWarehouseIds, int? selectedWarehouseId)
        {
            ViewBag.Warehouses = new SelectList(
                await db.WAREHOUSES
                    .Where(w => assignedWarehouseIds.Contains(w.ID) && w.IsDeleted != true)
                    .OrderBy(w => w.WHName)
                    .ToListAsync(),
                "ID", "WHName", selectedWarehouseId
            );
        }

        private class InventoryAdjustmentRow
        {
            public string ProductPNo { get; set; }
            public decimal Quantity { get; set; }
            public int RowNumber { get; set; }
        }

        private bool TryParseQuantity(string input, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var cleaned = input.Trim().Replace(" ", "").Replace(",", ".");
            var filtered = new string(cleaned.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (decimal.TryParse(filtered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }


    }
}
