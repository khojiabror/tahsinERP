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

                var stocks = await query
                    .OrderBy(s => s.Warehouse.WHName)
                    .ThenBy(s => s.PRODUCT.PNo)
                    .ToListAsync();

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

                return View(stocks);
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

                return View(new ViewModels.InventoryAdjustmentViewModel());
            }
        }

        // Olish so'rovi: Inventory/AdjustResult
        public ActionResult AdjustResult(string key)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Inventory"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!IsValidLogKey(key))
            {
                return RedirectToAction("Index");
            }

            ViewBag.LogKey = key;
            return View();
        }

        // Olish so'rovi: Inventory/DownloadAdjustmentLog
        public ActionResult DownloadAdjustmentLog(string key, string type)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Inventory", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!IsValidLogKey(key))
            {
                return RedirectToAction("Index");
            }

            var extension = string.Equals(type, "pdf", StringComparison.OrdinalIgnoreCase) ? ".pdf" : ".xlsx";
            var path = GetAdjustmentLogPath(key, extension);
            if (!System.IO.File.Exists(path))
            {
                return HttpNotFound();
            }

            var contentType = extension == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var downloadName = $"{key}{extension}";
            return File(path, contentType, downloadName);
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

                    if (!ModelState.IsValid)
                    {
                        await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                        return View(model);
                    }

                    var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                    if (await HasAdjustmentThisMonthAsync(currentUserId, model.WarehouseId, db))
                    {
                        ModelState.AddModelError("", "Bu ombor uchun bu oyda inventarizatsiya allaqachon o'tkazilgan.");
                        await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
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
                            ModelState.AddModelError("", "Excel faylda majburiy ustunlar yo'q (ProductPNo, Quantity).");
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        var errors = new List<string>();
                        var adjustments = new List<InventoryAdjustmentRow>();

                        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                        {
                            var pno = GetCellString(worksheet, row, headerMap["productpno"]);
                            var qtyValue = GetCellString(worksheet, row, headerMap["quantity"]);

                            if (string.IsNullOrWhiteSpace(pno) && string.IsNullOrWhiteSpace(qtyValue))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(pno))
                            {
                                errors.Add($"Qator {row}: Mahsulot kodi (ProductPNo) bo'sh.");
                                continue;
                            }

                            if (!double.TryParse(qtyValue, out double qty) || qty < 0)
                            {
                                errors.Add($"Qator {row}: Miqdor noto'g'ri yoki manfiy ({qtyValue}).");
                                continue;
                            }

                            adjustments.Add(new InventoryAdjustmentRow
                            {
                                ProductPNo = pno.Trim(),
                                Quantity = qty,
                                RowNumber = row
                            });
                        }

                        if (!adjustments.Any())
                        {
                            errors.Add("Excel faylda qayta ishlanadigan qatorlar topilmadi.");
                        }

                        if (errors.Any())
                        {
                            ViewBag.ErrorDetails = errors;
                            await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                            return View(model);
                        }

                        using (var transaction = db.Database.BeginTransaction())
                        {
                            try
                            {
                                var productMap = db.PRODUCTS
                                    .AsNoTracking()
                                    .AsEnumerable()
                                    .Where(p => !string.IsNullOrWhiteSpace(p.PNo))
                                    .ToDictionary(
                                        p => p.PNo.Trim().ToUpperInvariant(),
                                        p => new { p.ID, p.PNo, p.Name, p.UnitID });

                                var logRows = new List<InventoryAdjustmentLogRow>();
                                foreach (var adj in adjustments)
                                {
                                    var key = adj.ProductPNo.Trim().ToUpperInvariant();
                                    if (!productMap.TryGetValue(key, out var product))
                                    {
                                        errors.Add($"Qator {adj.RowNumber}: Mahsulot topilmadi ({adj.ProductPNo}).");
                                        continue;
                                    }

                                    if (!await HasWarehouseMovementAsync(model.WarehouseId, product.ID, db))
                                    {
                                        errors.Add($"Qator {adj.RowNumber}: {adj.ProductPNo} mahsuloti bo'yicha bu omborda kirim/chiqim bo'lmagan, qoldiqni to'g'rilab bo'lmaydi.");
                                        continue;
                                    }

                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == model.WarehouseId && s.ProductID == product.ID);

                                    var oldQty = stock?.Amount ?? 0;
                                    var unit = await db.UNITS.FindAsync(product.UnitID);

                                    if (stock == null)
                                    {
                                        stock = new PRODUCT_STOCKS
                                        {
                                            WHID = model.WarehouseId,
                                            ProductID = product.ID,
                                            Amount = adj.Quantity,
                                            UnitID = product.UnitID
                                        };
                                        db.PRODUCT_STOCKS.Add(stock);
                                    }
                                    else
                                    {
                                        stock.Amount = adj.Quantity;
                                    }

                                    logRows.Add(new InventoryAdjustmentLogRow
                                    {
                                        ProductPNo = product.PNo,
                                        ProductName = product.Name,
                                        OldQuantity = oldQty,
                                        NewQuantity = adj.Quantity,
                                        UnitName = unit?.ShortName ?? ""
                                    });
                                }

                                if (errors.Any())
                                {
                                    transaction.Rollback();
                                    ViewBag.ErrorDetails = errors;
                                    await PopulateAdjustmentViewBags(db, assignedWarehouseIds, model.WarehouseId);
                                    return View(model);
                                }

                                await db.SaveChangesAsync();
                                transaction.Commit();

                                tahsinERP.Controllers.LogHelper.LogToDatabase(
                                    User.Identity.Name,
                                    "InventoryAdjustment",
                                    $"Apply-WH-{model.WarehouseId}"
                                );

                                var logKey = await SaveAdjustmentLogsAsync(
                                    model.WarehouseId,
                                    currentUserId,
                                    logRows
                                );

                                TempData["Success"] = "Inventarizatsiya muvaffaqiyatli yakunlandi.";
                                return RedirectToAction("AdjustResult", new { key = logKey });
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

        private async Task<bool> HasWarehouseMovementAsync(int warehouseId, int productId, DBTHSNEntities db)
        {
            var hasIncome = await db.INCOME_PRODUCTS
                .AnyAsync(ip => ip.ProductID == productId && ip.INCOME.ReceivingWHID == warehouseId);

            if (hasIncome)
            {
                return true;
            }

            var hasExpense = await db.EXPENSE_PRODUCTS
                .AnyAsync(ep =>
                    ep.ProductID == productId &&
                    (ep.EXPENS.SenderWHID == warehouseId || ep.EXPENS.ReceiverWhID == warehouseId));

            return hasExpense;
        }

        private async Task<bool> HasAdjustmentThisMonthAsync(int userId, int warehouseId, DBTHSNEntities db)
        {
            if (userId <= 0)
            {
                return false;
            }

            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var actionName = $"Apply-WH-{warehouseId}";

            return await db.USERLOGS.AnyAsync(l =>
                l.UserID == userId &&
                l.ControllerName == "InventoryAdjustment" &&
                l.ActionName == actionName &&
                l.DateTime >= startOfMonth);
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

        private async Task<string> SaveAdjustmentLogsAsync(int warehouseId, int userId, List<InventoryAdjustmentLogRow> logRows)
        {
            var logKey = $"InvAdj_{warehouseId}_{DateTime.Now:yyyyMMddHHmmss}_{userId}";
            var excelPath = GetAdjustmentLogPath(logKey, ".xlsx");
            var pdfPath = GetAdjustmentLogPath(logKey, ".pdf");

            Directory.CreateDirectory(Path.GetDirectoryName(excelPath));

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Log");
                ws.Cells[1, 1].Value = "ProductPNo";
                ws.Cells[1, 2].Value = "ProductName";
                ws.Cells[1, 3].Value = "OldQty";
                ws.Cells[1, 4].Value = "NewQty";
                ws.Cells[1, 5].Value = "Diff";
                ws.Cells[1, 6].Value = "Unit";

                int row = 2;
                foreach (var item in logRows)
                {
                    ws.Cells[row, 1].Value = item.ProductPNo;
                    ws.Cells[row, 2].Value = item.ProductName;
                    ws.Cells[row, 3].Value = item.OldQuantity;
                    ws.Cells[row, 4].Value = item.NewQuantity;
                    ws.Cells[row, 5].Value = item.NewQuantity - item.OldQuantity;
                    ws.Cells[row, 6].Value = item.UnitName;
                    row++;
                }

                ws.Cells[1, 1, 1, 6].Style.Font.Bold = true;
                ws.Cells.AutoFitColumns();

                var bytes = package.GetAsByteArray();
                await Task.Run(() => System.IO.File.WriteAllBytes(excelPath, bytes));
            }

            var pdfBytes = BuildPdfLogBytes(logRows, warehouseId, userId);
            await Task.Run(() => System.IO.File.WriteAllBytes(pdfPath, pdfBytes));

            return logKey;
        }

        private string GetAdjustmentLogPath(string logKey, string extension)
        {
            var folder = Server.MapPath("~/App_Data/InventoryAdjustments");
            return Path.Combine(folder, $"{logKey}{extension}");
        }

        private bool IsValidLogKey(string logKey)
        {
            if (string.IsNullOrWhiteSpace(logKey))
            {
                return false;
            }

            return logKey.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        }

        private byte[] BuildPdfLogBytes(List<InventoryAdjustmentLogRow> logRows, int warehouseId, int userId)
        {
            var lines = new List<string>
            {
                "Inventarizatsiya log",
                $"Ombor ID: {warehouseId}",
                $"Foydalanuvchi ID: {userId}",
                $"Sana: {DateTime.Now:dd.MM.yyyy HH:mm}",
                ""
            };

            lines.Add("ProductPNo | ProductName | OldQty | NewQty | Diff | Unit");
            foreach (var item in logRows)
            {
                var diff = item.NewQuantity - item.OldQuantity;
                lines.Add($"{item.ProductPNo} | {item.ProductName} | {item.OldQuantity:N2} | {item.NewQuantity:N2} | {diff:N2} | {item.UnitName}");
            }

            return BuildSimplePdf(lines);
        }

        private byte[] BuildSimplePdf(List<string> lines)
        {
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("BT");
            contentBuilder.AppendLine("/F1 10 Tf");
            contentBuilder.AppendLine("50 760 Td");

            foreach (var line in lines)
            {
                var safeLine = EscapePdfText(line);
                contentBuilder.AppendLine($"({safeLine}) Tj");
                contentBuilder.AppendLine("0 -14 Td");
            }

            contentBuilder.AppendLine("ET");

            var contentBytes = Encoding.ASCII.GetBytes(contentBuilder.ToString());
            using (var ms = new MemoryStream())
            {
                void Write(string text)
                {
                    var bytes = Encoding.ASCII.GetBytes(text);
                    ms.Write(bytes, 0, bytes.Length);
                }

                var offsets = new List<long>();
                Write("%PDF-1.4\n");

                offsets.Add(ms.Position);
                Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                offsets.Add(ms.Position);
                Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

                offsets.Add(ms.Position);
                Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");

                offsets.Add(ms.Position);
                Write($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
                ms.Write(contentBytes, 0, contentBytes.Length);
                Write("\nendstream\nendobj\n");

                offsets.Add(ms.Position);
                Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

                var xrefStart = ms.Position;
                Write("xref\n0 6\n0000000000 65535 f \n");
                foreach (var offset in offsets)
                {
                    Write($"{offset:0000000000} 00000 n \n");
                }

                Write("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n");
                Write($"{xrefStart}\n%%EOF");

                return ms.ToArray();
            }
        }

        private string EscapePdfText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        private class InventoryAdjustmentRow
        {
            public string ProductPNo { get; set; }
            public double Quantity { get; set; }
            public int RowNumber { get; set; }
        }

        private class InventoryAdjustmentLogRow
        {
            public string ProductPNo { get; set; }
            public string ProductName { get; set; }
            public double OldQuantity { get; set; }
            public double NewQuantity { get; set; }
            public string UnitName { get; set; }
        }
    }
}
