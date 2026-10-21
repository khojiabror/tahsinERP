using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Ombordagi sanoq (pilotning boshlang'ich va yakuniy inventarizatsiyasi). Qoralama: ro'yxat (qoldig'i bor va
    /// distributsiya tovarlari), sanalgan miqdor qo'lda yoki Excel'dan — asosiy birlikda. O'tkazish: o'sha paytdagi
    /// qoldiq SystemQty ga yoziladi; boshlang'ich sanoq (yoki "farqni yozish" belgilangan nazorat sanog'i) qoldiqni
    /// mavjud <see cref="InventoryAdjustmentService"/> orqali sanalganga tenglaydi (INV kirim/chiqim, FIFO, oyiga bir marta).
    /// </summary>
    public static class InventoryCountService
    {
        public const string Prefix = "DI";

        public sealed class Row
        {
            public int LineID { get; set; }
            public int ProductID { get; set; }
            public string PNo { get; set; }
            public string Name { get; set; }
            public string Unit { get; set; }
            public decimal System { get; set; }
            public decimal? Counted { get; set; }
            public decimal? UnitCost { get; set; }
            public string Note { get; set; }
            public decimal? Diff => Counted.HasValue ? Counted.Value - System : (decimal?)null;
        }

        public static int Create(DistributionUnitOfWork uow, int warehouseId, string kind, string note, int userId, DateTime now)
        {
            var db = uow.Dst;
            if (!InventoryKinds.All.Contains(kind)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (!db.Warehouses.Any(w => w.ID == warehouseId && w.IsDeleted != true)) throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
            if (db.InventoryCounts.Any(c => c.WarehouseID == warehouseId && c.Status == DocumentStatus.Draft))
                throw new DstValidationException(DstLocalization.T("Inventory_DraftExists"));

            var count = new InventoryCount
            {
                DocNo = DstNumbering.Next(db, "INVENTORY_COUNTS", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                WarehouseID = warehouseId, Kind = kind, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedBy = userId, CreatedAt = now
            };
            db.InventoryCounts.Add(count);
            db.SaveChanges();

            // ro'yxat: shu omborda qoldig'i bor + sotuvdagi distributsiya tovarlari
            var stocked = db.Stocks.Where(s => s.WHID == warehouseId && s.Amount != 0).Select(s => s.ProductID);
            var selling = db.ProductProfiles.Where(p => p.IsSaleActive).Select(p => p.ProductID);
            var ids = stocked.Union(selling).Distinct().ToList();
            var alive = new HashSet<int>(db.Products.Where(p => ids.Contains(p.ID) && !p.IsDeleted).Select(p => p.ID).ToList());
            foreach (var id in ids.Where(alive.Contains).OrderBy(x => x))
                db.InventoryCountRows.Add(new InventoryCountRow { CountID = count.ID, ProductID = id });
            db.SaveChanges();
            return count.ID;
        }

        /// <summary>
        /// Sanoq ochiqligida sanalgan tovar ombordan chiqmasin (05_FINDINGS K2-20): aks holda o'tkazishda tizim qoldig'i
        /// sanoqdan keyin kamaygan bo'lib, soxta ortiqcha chiqadi. Hali sanalmagan tovar — bemalol (sanoq keyinroq ko'radi).
        /// </summary>
        public static void EnsureNotCounting(DistributionDbContext db, int warehouseId, IReadOnlyCollection<int> productIds)
        {
            if (productIds == null || productIds.Count == 0) return;
            var ids = productIds.Distinct().ToList();
            var hit = (from l in db.InventoryCountRows
                       join c in db.InventoryCounts on l.CountID equals c.ID
                       where c.WarehouseID == warehouseId && c.Status == DocumentStatus.Draft && l.CountedQty != null && ids.Contains(l.ProductID)
                       join p in db.Products on l.ProductID equals p.ID
                       select new { c.DocNo, p.PNo, p.Name }).FirstOrDefault();
            if (hit != null)
                throw new DstValidationException(DstLocalization.T("Inventory_CountingBlocks", hit.PNo + " " + hit.Name, hit.DocNo));
        }

        public static InventoryCount RequireDraft(DistributionDbContext db, int countId)
        {
            var c = db.InventoryCounts.Find(countId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (c.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Inventory_NotDraft"));
            return c;
        }

        public static void AddProduct(DistributionDbContext db, int countId, int productId)
        {
            RequireDraft(db, countId);
            if (!db.Products.Any(p => p.ID == productId && !p.IsDeleted)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (db.InventoryCountRows.Any(l => l.CountID == countId && l.ProductID == productId)) return;
            db.InventoryCountRows.Add(new InventoryCountRow { CountID = countId, ProductID = productId });
            db.SaveChanges();
        }

        /// <summary>Sanalgan miqdorlar (bo'sh — hali sanalmagan). Manfiy miqdor yoki tannarx — xato.</summary>
        /// <param name="canSeeCost">DstSeeCost yo'q — tannarx ustuni ko'rinmaydi va mavjud qiymati o'zgarmaydi (05_FINDINGS K1-5).</param>
        public static void SaveCounts(DistributionDbContext db, int countId, IEnumerable<(int LineId, decimal? Counted, decimal? UnitCost, string Note)> values,
            bool canSeeCost = true)
        {
            RequireDraft(db, countId);
            var lines = db.InventoryCountRows.Where(l => l.CountID == countId).ToList().ToDictionary(l => l.ID);
            foreach (var v in values)
            {
                if (!lines.TryGetValue(v.LineId, out var line)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                if (v.Counted < 0 || v.UnitCost < 0) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
                line.CountedQty = v.Counted;
                if (canSeeCost) line.UnitCost = v.UnitCost;
                line.Note = string.IsNullOrWhiteSpace(v.Note) ? null : v.Note.Trim();
            }
            db.SaveChanges();
        }

        /// <summary>Sanoq qatorlari va qoldiq: o'tkazilgan — o'sha paytdagi (SystemQty), qoralama — hozirgi.</summary>
        public static List<Row> Rows(DistributionDbContext db, InventoryCount count)
        {
            var rows = (from l in db.InventoryCountRows
                        where l.CountID == count.ID
                        join p in db.Products on l.ProductID equals p.ID
                        join u in db.Units on p.UnitID equals u.ID into uj
                        from u in uj.DefaultIfEmpty()
                        orderby p.PNo
                        select new Row
                        {
                            LineID = l.ID, ProductID = p.ID, PNo = p.PNo, Name = p.Name, Unit = u.ShortName,
                            System = l.SystemQty ?? 0, Counted = l.CountedQty, UnitCost = l.UnitCost, Note = l.Note
                        }).ToList();
            if (count.Status == DocumentStatus.Draft)
            {
                var onHand = StockService.Levels(db, rows.Select(r => r.ProductID).ToList(), new[] { count.WarehouseID })
                    .ToDictionary(l => l.ProductID, l => l.OnHand);
                foreach (var r in rows) r.System = onHand.TryGetValue(r.ProductID, out var q) ? q : 0;
            }
            return rows;
        }

        /// <summary>
        /// O'tkazish. Hamma qator sanalgan bo'lishi shart. apply: qoldiqni sanalganga tenglash (boshlang'ich sanoqda doim).
        /// Chaqiruvchi tranzaksiyani Commit qiladi.
        /// </summary>
        public static async Task PostAsync(DistributionUnitOfWork uow, int countId, bool apply, int userId, DateTime now)
        {
            var db = uow.Dst;
            var count = RequireDraft(db, countId);
            var lines = db.InventoryCountRows.Where(l => l.CountID == countId).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            int notCounted = lines.Count(l => l.CountedQty == null);
            if (notCounted > 0) throw new DstValidationException(DstLocalization.T("Inventory_NotCounted", notCounted));
            apply = apply || count.Kind == InventoryKinds.Opening;

            var ids = lines.Select(l => l.ProductID).ToList();
            var onHand = StockService.Levels(db, ids, new[] { count.WarehouseID }).ToDictionary(l => l.ProductID, l => l.OnHand);
            foreach (var l in lines) l.SystemQty = onHand.TryGetValue(l.ProductID, out var q) ? q : 0;

            if (apply)
            {
                var units = db.Products.Where(p => ids.Contains(p.ID)).Select(p => new { p.ID, p.PNo, p.UnitID }).ToList().ToDictionary(p => p.ID);
                var coreLines = lines.Select(l => new tahsinERP.Services.Stock.InventoryCountLine
                {
                    ProductID = l.ProductID, ProductPNo = units[l.ProductID].PNo, UnitID = units[l.ProductID].UnitID, Quantity = l.CountedQty.Value
                }).ToList();
                var costs = lines.Where(l => l.UnitCost.HasValue).ToDictionary(l => l.ProductID, l => l.UnitCost.Value);
                db.SaveChanges();
                try
                {
                    count.AdjustmentID = await InventoryAdjustmentService.ApplyAsync(uow.Erp, count.WarehouseID, userId, coreLines, costs,
                        monthlyLock: count.Kind != InventoryKinds.Opening);
                }
                catch (InvalidOperationException ex)
                {
                    throw new DstValidationException(ex.Message);
                }
                count.Applied = true;
            }
            DocumentLifecycle.Post(count, userId, now);
            db.SaveChanges();
        }

        public static void CancelDraft(DistributionDbContext db, int countId, int userId, string reason, DateTime now)
        {
            var count = RequireDraft(db, countId);
            DocumentLifecycle.Cancel(count, userId, reason, now);
            db.SaveChanges();
        }

        // ---------- Excel ----------

        public static readonly ImportService.Column[] SheetColumns =
        {
            new ImportService.Column("pno", "Artikul", "Артикул", true),
            new ImportService.Column("name", "Nomi", "Наименование"),
            new ImportService.Column("unit", "Birlik", "Единица"),
            new ImportService.Column("counted", "Sanalgan", "Посчитано", true),
            new ImportService.Column("cost", "Tannarx", "Себестоимость"),
        };

        /// <summary>Sanoq varag'i: tovarlar ro'yxati, qoldiq ko'rsatilmaydi ("ko'r" sanoq).</summary>
        public static byte[] Sheet(DistributionDbContext db, InventoryCount count, bool russian, bool canSeeCost = true)
        {
            var rows = Rows(db, count);
            int columns = canSeeCost ? SheetColumns.Length : SheetColumns.Length - 1;   // tannarx — oxirgi ustun
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet(count.DocNo);
                for (int c = 0; c < columns; c++)
                {
                    ws.Cell(1, c + 1).Value = russian ? SheetColumns[c].Ru : SheetColumns[c].Uz;
                    ws.Cell(1, c + 1).Style.Font.Bold = true;
                }
                int r = 2;
                foreach (var row in rows)
                {
                    ws.Cell(r, 1).SetValue(row.PNo);
                    ws.Cell(r, 2).SetValue(row.Name);
                    ws.Cell(r, 3).SetValue(row.Unit);
                    if (row.Counted.HasValue) ws.Cell(r, 4).Value = row.Counted.Value;
                    if (canSeeCost && row.UnitCost.HasValue) ws.Cell(r, 5).Value = row.UnitCost.Value;
                    r++;
                }
                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents();
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Sanalgan miqdorlarni Excel'dan: artikul bo'yicha, ro'yxatda yo'q tovar qo'shiladi. Xato bo'lsa hech narsa yozilmaydi.</summary>
        public static ImportResult ImportSheet(DistributionDbContext db, int countId, Stream stream, bool canSeeCost = true)
        {
            var result = new ImportResult();
            RequireDraft(db, countId);
            var sheet = ImportService.Read(stream);
            int pnoCol = Col(sheet, SheetColumns[0], result), countedCol = Col(sheet, SheetColumns[3], result);
            int costCol = canSeeCost && (sheet.Headers.TryGetValue(SheetColumns[4].Uz, out var cc) || sheet.Headers.TryGetValue(SheetColumns[4].Ru, out cc)) ? cc : 0;
            if (!result.Ok) return result;

            var products = db.Products.Where(p => !p.IsDeleted).Select(p => new { p.ID, p.PNo }).ToList()
                .GroupBy(p => p.PNo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().ID, StringComparer.OrdinalIgnoreCase);
            var lines = db.InventoryCountRows.Where(l => l.CountID == countId).ToList().ToDictionary(l => l.ProductID);
            var values = new List<(int Row, int ProductId, decimal Counted, decimal? Cost)>();
            var seen = new HashSet<int>();
            foreach (var (row, cells) in sheet.Rows)
            {
                cells.TryGetValue(pnoCol, out var pno);
                cells.TryGetValue(countedCol, out var countedText);
                string costText = null;
                if (costCol > 0) cells.TryGetValue(costCol, out costText);
                if (string.IsNullOrWhiteSpace(countedText) && string.IsNullOrWhiteSpace(costText)) { result.Skipped++; continue; }   // sanalmagan qator
                if (string.IsNullOrWhiteSpace(pno) || !products.TryGetValue(pno.Trim(), out int productId))
                {
                    result.Errors.Add((row, DstLocalization.T("Import_UnknownValue", pno)));
                    continue;
                }
                if (!seen.Add(productId)) { result.Errors.Add((row, DstLocalization.T("Import_DuplicateInFile", pno))); continue; }
                if (!DstModelBinder.TryParseDecimal(countedText ?? "", out var counted) || counted < 0)
                {
                    result.Errors.Add((row, DstLocalization.T("Error_NumberFormat") + ": " + countedText));
                    continue;
                }
                decimal? cost = null;
                if (!string.IsNullOrWhiteSpace(costText))
                {
                    if (!DstModelBinder.TryParseDecimal(costText, out var c) || c < 0) { result.Errors.Add((row, DstLocalization.T("Error_NumberFormat") + ": " + costText)); continue; }
                    cost = c;
                }
                values.Add((row, productId, counted, cost));
            }
            if (!result.Ok) return result;

            foreach (var v in values)
            {
                if (!lines.TryGetValue(v.ProductId, out var line))
                {
                    db.InventoryCountRows.Add(line = new InventoryCountRow { CountID = countId, ProductID = v.ProductId });
                    result.Created++;
                }
                else result.Updated++;
                line.CountedQty = v.Counted;
                if (v.Cost.HasValue) line.UnitCost = v.Cost;
                result.Plan(v.Row, sheet.Rows.First(x => x.Row == v.Row).Cells[pnoCol], line.ID == 0 ? ImportAction.New : ImportAction.Update,
                    v.Counted.ToString("0.####", CultureInfo.InvariantCulture));
            }
            db.SaveChanges();
            return result;
        }

        private static int Col(ImportService.Sheet sheet, ImportService.Column c, ImportResult result)
        {
            if (sheet.Headers.TryGetValue(c.Uz, out int i) || sheet.Headers.TryGetValue(c.Ru, out i)) return i;
            result.Errors.Add((1, DstLocalization.T("Import_MissingColumn", c.Uz + " / " + c.Ru)));
            return 0;
        }
    }
}
