using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using OfficeOpenXml;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;
using tahsinERP.ViewModels;

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
                    TempData["Error"] = "DLOC moduli yoqilmagan. Avval migration scriptni ishga tushiring.";
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
                    TempData["Error"] = "DLOC qoldiq yuklanmadi: " + ex.Message;
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
                    TempData["Error"] = "DLOC moduli yoqilmagan. Avval migration scriptni ishga tushiring.";
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
                    TempData["Error"] = "DLOC moduli yoqilmagan.";
                    return RedirectToAction("Index");
                }

                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(model.WarehouseId))
                {
                    TempData["Error"] = "Ushbu ombor uchun ruxsat yo'q.";
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

                    TempData["Success"] = $"{count} ta mahsulot DLOC ga joylashtirildi.";
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
                    ViewBag.NoWarehouseError = "Sizga biriktirilgan ombor mavjud emas";
                    return View(new ViewModels.InventoryAdjustmentViewModel());
                }

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES
                        .Where(w => assignedWarehouseIds.Contains(w.ID) && w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .ToListAsync(),
                    "ID", "WHName"
                );

                var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                var lockedWarehouseIds = new List<int>();
                foreach (var whId in assignedWarehouseIds)
                {
                    if (await HasAdjustmentThisMonthAsync(currentUserId, whId, db))
                    {
                        lockedWarehouseIds.Add(whId);
                    }
                }
                ViewBag.LockedWarehouseIds = lockedWarehouseIds;

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

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Template");
                ws.Cells[1, 1].Value = "ProductPNo";
                ws.Cells[1, 2].Value = "Quantity";
                ws.Cells[2, 1].Value = "00000001";
                ws.Cells[2, 2].Value = 10;
                ws.Cells[1, 1, 1, 2].Style.Font.Bold = true;

                var bytes = package.GetAsByteArray();
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
                        ModelState.AddModelError("", "Sizga biriktirilgan ombor mavjud emas");
                    }
                    else if (!assignedWarehouseIds.Contains(model.WarehouseId))
                    {
                        ModelState.AddModelError("", "Siz faqat o'zingizga biriktirilgan omborlar bo'yicha inventarizatsiya qilishingiz mumkin.");
                    }

                    if (!model.Confirmed)
                    {
                        ModelState.AddModelError("", "Inventarizatsiya qoidalarini qabul qilishingiz shart.");
                    }

                    if (model.ExcelFile == null || model.ExcelFile.ContentLength == 0)
                    {
                        ModelState.AddModelError("", "Excel fayl yuklanmadi.");
                    }

                    var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                    if (await HasAdjustmentThisMonthAsync(currentUserId, model.WarehouseId, db))
                    {
                        ModelState.AddModelError("", "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi.");
                    }

                    if (!ModelState.IsValid)
                    {
                        await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                        var lockedWarehouseIds = new List<int>();
                        foreach (var whId in assignedWarehouseIds)
                        {
                            if (await HasAdjustmentThisMonthAsync(currentUserId, whId, db))
                            {
                                lockedWarehouseIds.Add(whId);
                            }
                        }
                        ViewBag.LockedWarehouseIds = lockedWarehouseIds;
                        return View(model);
                    }

                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(model.ExcelFile.InputStream))
                    {
                        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null || worksheet.Dimension == null)
                        {
                            ModelState.AddModelError("", "Excel fayl bo'sh.");
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        var headerMap = BuildHeaderMap(worksheet);
                        if (!headerMap.ContainsKey("productpno") || !headerMap.ContainsKey("quantity"))
                        {
                            ModelState.AddModelError("", "Excel format xato. Majburiy ustunlar: ProductPNo va Quantity.");
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        var productMap = db.PRODUCTS
                            .AsNoTracking()
                            .AsEnumerable()
                            .Where(p => !string.IsNullOrWhiteSpace(p.PNo))
                            .ToDictionary(
                                p => p.PNo.Trim().ToUpperInvariant(),
                                p => new { p.ID, p.PNo, p.Name, p.UnitID });

                        var qtyCol = headerMap["quantity"];
                        var pnoCol = headerMap["productpno"];
                        var parsedRows = new List<InventoryAdjustmentRow>();
                        var errorDetails = new List<string>();
                        var seenProducts = new HashSet<int>();

                        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                        {
                            var pno = GetCellString(worksheet, row, pnoCol);
                            var qtyValue = GetCellString(worksheet, row, qtyCol);

                            if (string.IsNullOrWhiteSpace(pno))
                            {
                                if (string.IsNullOrWhiteSpace(qtyValue))
                                {
                                    continue;
                                }
                                errorDetails.Add($"{row}-qator: ProductPNo bo'sh.");
                                continue;
                            }

                            var key = pno.Trim().ToUpperInvariant();
                            if (!productMap.TryGetValue(key, out var product))
                            {
                                errorDetails.Add($"{row}-qator: Mahsulot topilmadi ({pno}).");
                                continue;
                            }

                            if (!TryParseQuantity(qtyValue, out var qty))
                            {
                                errorDetails.Add($"{row}-qator: Quantity noto'g'ri ({qtyValue}).");
                                continue;
                            }

                            if (qty < 0)
                            {
                                errorDetails.Add($"{row}-qator: Quantity manfiy bo'lishi mumkin emas ({qtyValue}).");
                                continue;
                            }

                            if (!seenProducts.Add(product.ID))
                            {
                                errorDetails.Add($"{row}-qator: Bir xil ProductPNo bir martadan ko'p kiritilgan ({product.PNo}).");
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
                            ModelState.AddModelError("", "Excel faylda xatolar bor. Tuzatib qayta yuklang.");
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
                            foreach (var parsedRow in parsedRows)
                            {
                                var product = productMap[parsedRow.ProductPNo.Trim().ToUpperInvariant()];
                                double onHand;
                                onHandMap.TryGetValue(product.ID, out onHand);
                                if (onHand <= StockLedger.Epsilon)
                                    continue;
                                var ledgerQty = (double)DlocFifoDataAccess.GetLedgerQty(db, model.WarehouseId, product.ID);
                                var unassigned = onHand - ledgerQty;
                                if (unassigned > StockLedger.Epsilon)
                                {
                                    unassignedErrors.Add($"{parsedRow.RowNumber}-qator: {product.PNo} — DLOC ga joylashtirilmagan qoldiq {unassigned:N3}. Avval «DLOC ga joylashtirish» sahifasida joylashtiring.");
                                }
                            }

                            if (unassignedErrors.Any())
                            {
                                ViewBag.ErrorDetails = unassignedErrors;
                                ModelState.AddModelError("", "Ombordagi ayrim qoldiqlar DLOC ga joylashtirilmagan. Inventarizatsiyadan oldin joylashtiring.");
                                await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                                return View(model);
                            }
                        }

                        using (var transaction = db.Database.BeginTransaction())
                        {
                            try
                            {
                                var currencyId = await GetDefaultCurrencyIdAsync(db);
                                var now = DateTime.Now;
                                // Bitta inventarizatsiya = bitta INV kirim (ortiqcha) + bitta INV chiqim (kamomad), N qatorli
                                // (avval har mahsulotga alohida hujjat va 2 ta SaveChanges — SRM-P06).
                                var invToken = $"{now.Month:00}_{now.Day:00}_{{0}}_{model.WarehouseId}_{now:HHmmssfff}";

                                var plusLines = new List<Tuple<InventoryAdjustmentRow, int, double, double, int>>();
                                var minusLines = new List<Tuple<InventoryAdjustmentRow, int, double, double, int>>();
                                foreach (var parsedRow in parsedRows)
                                {
                                    var product = productMap[parsedRow.ProductPNo.Trim().ToUpperInvariant()];
                                    var unitId = product.UnitID > 0 ? product.UnitID : 1;

                                    // Qator tranzaksiya oxirigacha qulflanadi: hisoblangan farq parallel harakat bilan buzilmaydi
                                    var oldQty = StockLedger.GetOnHand(db, model.WarehouseId, product.ID, lockForUpdate: true);
                                    var diff = parsedRow.Quantity - oldQty;
                                    if (Math.Abs(diff) < StockLedger.Epsilon)
                                        continue;

                                    // Baholash: oxirgi kirim (xarid) narxi — balans hisoboti ham shu narxda baholaydi.
                                    var piecePrice = await GetLatestIncomePriceAsync(db, product.ID);
                                    var line = Tuple.Create(parsedRow, product.ID, Math.Abs(diff), piecePrice, unitId);
                                    if (diff > 0) plusLines.Add(line); else minusLines.Add(line);
                                }

                                if (plusLines.Any())
                                {
                                    var income = new INCOME
                                    {
                                        DocNo = "INV_" + string.Format(invToken, "IN"),
                                        ReceivingWHID = model.WarehouseId,
                                        SenderWHID = null,
                                        IssueDateTime = now,
                                        CurrencyID = currencyId,
                                        Amount = plusLines.Sum(l => l.Item3),
                                        TotalPrice = plusLines.Sum(l => l.Item3 * l.Item4),
                                        Description = $"Inventarizatsiya qo'shimcha kirim: {plusLines.Count} ta mahsulot",
                                        RecieveStatus = true,
                                        IsDeleted = false
                                    };
                                    db.INCOMES.Add(income);
                                    var incomeLines = plusLines.Select(l => new INCOME_PRODUCTS
                                    {
                                        INCOME = income,
                                        ProductID = l.Item2,
                                        Amount = l.Item3,
                                        PiecePrice = l.Item4,
                                        TotalPrice = l.Item3 * l.Item4,
                                        UnitID = l.Item5,
                                        Comment = "Inventarizatsiya"
                                    }).ToList();
                                    db.INCOME_PRODUCTS.AddRange(incomeLines);
                                    await db.SaveChangesAsync();

                                    for (int i = 0; i < plusLines.Count; i++)
                                    {
                                        var l = plusLines[i];
                                        int? locationId = null;
                                        if (requireDloc)
                                        {
                                            // Qo'shilgan miqdor DLOC ga avtomatik joylashtiriladi (ledger + umumiy qoldiq yangilanadi).
                                            locationId = ResolveAutoPutawayLocation(db, model.WarehouseId, l.Item2, (decimal)l.Item3);
                                            if (locationId <= 0)
                                            {
                                                throw new Exception($"Mahsulot {l.Item1.ProductPNo}: qo'shimcha miqdor uchun bo'sh DLOC joy topilmadi (sig'im yoki aralashtirish cheklovi).");
                                            }
                                        }

                                        StockLedger.ReceiveLine(db, model.WarehouseId, income.ID, incomeLines[i].ID, l.Item2,
                                            l.Item3, l.Item5, locationId, now, "INV");
                                    }
                                }

                                if (minusLines.Any())
                                {
                                    var expense = new EXPENS
                                    {
                                        DocNo = "INV_" + string.Format(invToken, "OUT"),
                                        SenderWHID = model.WarehouseId,
                                        ReceiverWhID = null,
                                        IssueDateTime = now,
                                        CurrencyID = currencyId,
                                        Amount = minusLines.Sum(l => l.Item3),
                                        TotalPrice = minusLines.Sum(l => l.Item3 * l.Item4),
                                        Description = $"Inventarizatsiya kamaytirish chiqimi: {minusLines.Count} ta mahsulot",
                                        SendStatus = true,
                                        IsDeleted = false
                                    };
                                    db.EXPENSES.Add(expense);
                                    var expenseLines = minusLines.Select(l => new EXPENSE_PRODUCTS
                                    {
                                        EXPENS = expense,
                                        ProductID = l.Item2,
                                        Amount = l.Item3,
                                        PiecePrice = l.Item4,
                                        TotalPrice = l.Item3 * l.Item4,
                                        UnitID = l.Item5,
                                        Comment = "Inventarizatsiya"
                                    }).ToList();
                                    db.EXPENSE_PRODUCTS.AddRange(expenseLines);
                                    await db.SaveChangesAsync();

                                    // Kamomad DLOC omborda FIFO bo'yicha, oddiy omborda umumiy qoldiqdan chiqariladi.
                                    foreach (var ep in expenseLines)
                                    {
                                        await StockLedger.IssueLineAsync(db, model.WarehouseId, ep.ProductID, ep.Amount, ep.UnitID, ep.ID);
                                    }
                                }

                                await db.SaveChangesAsync();
                                transaction.Commit();

                                tahsinERP.Controllers.LogHelper.LogToDatabase(
                                    User.Identity.Name,
                                    "InventoryAdjustment",
                                    $"Apply-WH-{model.WarehouseId}",
                                    null,
                                    HttpContext);

                                TempData["Success"] = "Inventarizatsiya muvaffaqiyatli yakunlandi.";
                                return RedirectToAction("Index");
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                                await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                                return View(model);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Xatolik: {ex.Message}";
                var innerEx = ex.InnerException;
                while (innerEx != null)
                {
                    errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                    innerEx = innerEx.InnerException;
                }
                ModelState.AddModelError("", errorMessage);
                using (var db = new DBTHSNEntities())
                {
                    var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                    await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                    var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                    var lockedWarehouseIds = new List<int>();
                    foreach (var whId in assignedWarehouseIds)
                    {
                        if (await HasAdjustmentThisMonthAsync(currentUserId, whId, db))
                        {
                            lockedWarehouseIds.Add(whId);
                        }
                    }
                    ViewBag.LockedWarehouseIds = lockedWarehouseIds;
                }
                return View(model);
            }
        }

        private async Task<List<int>> GetAssignedWarehouseIdsAsync(DBTHSNEntities db)
        {
            var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
            return await db.WAREHOUSES
                .Where(w => w.IsDeleted != true && w.MRP == currentUserId)
                .Select(w => w.ID)
                .ToListAsync();
        }

        private async Task<bool> HasAdjustmentThisMonthAsync(int userId, int warehouseId, DBTHSNEntities db)
        {
            if (userId <= 0)
            {
                return false;
            }

            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var actionName = $"Apply-WH-{warehouseId}";

            // USERLOGS.ControllerName/ActionName bazada text bo'lsa, SQL da = varchar bilan mos kelmaydi.
            // Shuning uchun matn solishtirishni SQL o'rniga xotirada qilamiz.
            var logsThisMonth = await db.USERLOGS
                .AsNoTracking()
                .Where(l => l.UserID == userId && l.DateTime >= startOfMonth)
                .ToListAsync();

            return logsThisMonth.Any(l =>
                string.Equals(l.ControllerName, "InventoryAdjustment", StringComparison.Ordinal) &&
                string.Equals(l.ActionName, actionName, StringComparison.Ordinal));
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

        private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet worksheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int endColumn = worksheet.Dimension.End.Column;

            for (int col = 1; col <= endColumn; col++)
            {
                var header = (worksheet.Cells[1, col].Text ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                if (header.Contains("productpno") || header.Contains("pno") || header.Contains("mahsulot"))
                {
                    if (!map.ContainsKey("productpno"))
                    {
                        map["productpno"] = col;
                    }
                }

                if (header.Contains("quantity") || header.Contains("qty") || header.Contains("miqdor") || header.Contains("qoldiq"))
                {
                    if (!map.ContainsKey("quantity"))
                    {
                        map["quantity"] = col;
                    }
                }
            }

            return map;
        }

        private static string GetCellString(ExcelWorksheet worksheet, int row, int col)
        {
            return (worksheet.Cells[row, col].Text ?? string.Empty).Trim();
        }

        private class InventoryAdjustmentRow
        {
            public string ProductPNo { get; set; }
            public double Quantity { get; set; }
            public int RowNumber { get; set; }
        }

        private bool TryParseQuantity(string input, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var cleaned = input.Trim().Replace(" ", "").Replace(",", ".");
            var filtered = new string(cleaned.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (double.TryParse(filtered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        private async Task<int> GetDefaultCurrencyIdAsync(DBTHSNEntities db)
        {
            var uzs = await db.CURRENCIES.FirstOrDefaultAsync(c => c.Shortname != null && c.Shortname.Trim().ToUpper() == "UZS");
            if (uzs != null) return uzs.ID;
            var first = await db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            return first?.ID ?? 1;
        }

        /// <summary>
        /// Mahsulot uchun oxirgi kirim (xarid) narxini qaytaradi. Balans hisoboti ham
        /// inventarizatsiya zaxirasini shu narxda baholaydi, shuning uchun mos keladi.
        /// </summary>
        private async Task<double> GetLatestIncomePriceAsync(DBTHSNEntities db, int productId)
        {
            var ip = await db.INCOME_PRODUCTS
                .Include(x => x.INCOME)
                .Where(x => x.ProductID == productId
                    && (x.INCOME.IsDeleted == null || x.INCOME.IsDeleted == false))
                .OrderByDescending(x => x.INCOME.IssueDateTime)
                .FirstOrDefaultAsync();

            if (ip == null)
            {
                return 0;
            }

            if (ip.PiecePrice.HasValue && ip.PiecePrice.Value > 0)
            {
                return ip.PiecePrice.Value;
            }

            if (ip.TotalPrice.HasValue && ip.Amount > 0)
            {
                return ip.TotalPrice.Value / ip.Amount;
            }

            return 0;
        }

        /// <summary>
        /// DLOC omborda qo'shimcha miqdorni joylashtirish uchun joy tanlaydi: avval mahsulot
        /// allaqachon turgan, sig'imi yetadigan joy; bo'lmasa bo'sh yoki aralash mahsulotga
        /// ruxsat etilgan, sig'imi yetadigan joy. Mos joy topilmasa 0 qaytaradi.
        /// </summary>
        private int ResolveAutoPutawayLocation(DBTHSNEntities db, int warehouseId, int productId, decimal qty)
        {
            var locations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            if (locations == null || !locations.Any())
            {
                return 0;
            }

            var productLocationIds = new HashSet<int>(
                DlocFifoDataAccess.GetInventoryByDloc(db, warehouseId, productId)
                    .Select(r => r.LocationID));

            int fallbackLocationId = 0;
            foreach (var loc in locations)
            {
                var currentQty = DlocFifoDataAccess.GetLocationCurrentQty(db, loc.ID);
                var free = loc.MaxCapacity - currentQty;
                if (free + 0.000001m < qty)
                {
                    continue; // sig'im yetmaydi
                }

                // Mahsulot allaqachon shu joyda bo'lsa — eng mos joy.
                if (productLocationIds.Contains(loc.ID))
                {
                    return loc.ID;
                }

                // Aks holda: bo'sh joy yoki aralash mahsulotga ruxsat etilgan joy.
                if (fallbackLocationId == 0 && (loc.AllowMixedProducts || currentQty <= 0.000001m))
                {
                    fallbackLocationId = loc.ID;
                }
            }

            return fallbackLocationId;
        }

    }
}
