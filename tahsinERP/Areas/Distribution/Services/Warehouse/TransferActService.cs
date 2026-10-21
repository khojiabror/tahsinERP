using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    public sealed class TransferReceiveLine
    {
        public int IncomeProductID { get; set; }
        public decimal ReceivedQty { get; set; }
        public string Resolution { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// Ko'chirmani farq dalolatnomasi bilan qabul qilish (TZ 5-bo'lim). Mavjud oqim: jo'natuvchi chiqimi (EXPENSES, qoldiq
    /// darhol kamayadi) → qabul qiluvchida kutilayotgan kirim (INCOMES, RecieveStatus = 0, izohda EXPENSE_ID:N). Ombor
    /// modulida qabul — "hammasi yoki hech narsa"; bu yerda har qator uchun haqiqatda kelgan miqdor kiritiladi:
    ///   - kelgan miqdor qabul qilinadi (kirim qatori shu miqdorga tuzatiladi — FIFO qatlami haqiqiy miqdorda);
    ///   - kamomad: LOSS — yo'qotish (tovar jo'natuvchidan chiqib ketgan, hech qayerga kirmaydi; summasi dalolatnomada)
    ///     yoki RETURN — jo'natuvchi omboriga qaytariladi (o'sha tannarxda kirim);
    ///   - ortiqcha: kirimga qo'shiladi (jo'natuvchi tannarxida).
    /// Farq bo'lsa rahbarlarga xabar. Qabul qiluvchi — omborning mas'uli (WAREHOUSES.MRP) yoki Admin.
    /// </summary>
    public static class TransferActService
    {
        public const string Prefix = "DT";
        public const string NotifyType = "TRANSFER_DIFF";

        /// <summary>Sinov uchun: rahbar tannarxni ko'radimi (null — DstSeeCost huquqi bo'yicha).</summary>
        internal static Func<int, string, bool> CanSeeCostOverride;

        public sealed class PendingRow
        {
            public int IncomeID { get; set; }
            public string DocNo { get; set; }
            public DateTime IssueDateTime { get; set; }
            public int FromWarehouseID { get; set; }
            public string FromWarehouse { get; set; }
            public int ToWarehouseID { get; set; }
            public string ToWarehouse { get; set; }
            public int Lines { get; set; }
            public decimal Qty { get; set; }
        }

        public sealed class LineRow
        {
            public int IncomeProductID { get; set; }
            public int ProductID { get; set; }
            public string PNo { get; set; }
            public string Product { get; set; }
            public int UnitID { get; set; }
            public string Unit { get; set; }
            public decimal SentQty { get; set; }
            public decimal UnitCost { get; set; }
            public string Barcodes { get; set; }
        }

        private static IQueryable<INCOME> PendingQuery(DBTHSNEntities erp) =>
            erp.INCOMES.Where(i => i.IsDeleted != true && (i.RecieveStatus == null || i.RecieveStatus == false) && i.SenderWHID != null
                                   && i.Description.Contains(DocumentMarkers.LinkedExpenseId));

        /// <summary>Qabul kutayotgan ko'chirmalar (<paramref name="warehouseIds"/> — qabul qiluvchi omborlar; null — hammasi).</summary>
        public static List<PendingRow> Pending(DBTHSNEntities erp, IReadOnlyCollection<int> warehouseIds)
        {
            var q = PendingQuery(erp);
            if (warehouseIds != null)
            {
                var ids = warehouseIds.ToList();
                q = q.Where(i => ids.Contains(i.ReceivingWHID));
            }
            return q.OrderBy(i => i.IssueDateTime).Take(300).Select(i => new PendingRow
            {
                IncomeID = i.ID, DocNo = i.DocNo, IssueDateTime = i.IssueDateTime, FromWarehouseID = i.SenderWHID.Value, ToWarehouseID = i.ReceivingWHID,
                FromWarehouse = erp.WAREHOUSES.Where(w => w.ID == i.SenderWHID).Select(w => w.WHName).FirstOrDefault(),
                ToWarehouse = erp.WAREHOUSES.Where(w => w.ID == i.ReceivingWHID).Select(w => w.WHName).FirstOrDefault(),
                Lines = i.INCOME_PRODUCTS.Count(), Qty = i.INCOME_PRODUCTS.Sum(p => (decimal?)p.Amount) ?? 0
            }).ToList();
        }

        public static PendingRow PendingOne(DBTHSNEntities erp, int incomeId) =>
            Pending(erp, null).FirstOrDefault(p => p.IncomeID == incomeId);

        /// <summary>Kutilayotgan ko'chirma qatorlari: jo'natilgan miqdor va jo'natuvchi FIFO tannarxi.</summary>
        public static List<LineRow> Lines(DistributionDbContext db, DBTHSNEntities erp, int incomeId)
        {
            var income = erp.INCOMES.Find(incomeId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            int? expenseId = DocumentMarkers.Parse(income.Description, DocumentMarkers.LinkedExpenseId);
            var expenseCosts = expenseId.HasValue
                ? erp.EXPENSE_PRODUCTS.Where(p => p.ExpenseID == expenseId.Value).ToList()
                    .GroupBy(p => p.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount) == 0 ? 0 : g.Sum(x => x.Amount * x.PiecePrice) / g.Sum(x => x.Amount))
                : new Dictionary<int, decimal>();
            var rows = (from ip in erp.INCOME_PRODUCTS
                        where ip.IncomeID == incomeId
                        orderby ip.ID
                        select new LineRow
                        {
                            IncomeProductID = ip.ID, ProductID = ip.ProductID, PNo = ip.PRODUCT.PNo, Product = ip.PRODUCT.Name,
                            UnitID = ip.UnitID, Unit = ip.UNIT.ShortName, SentQty = ip.Amount, UnitCost = ip.PiecePrice ?? 0
                        }).ToList();
            var ids = rows.Select(r => r.ProductID).Distinct().ToList();
            var barcodes = db.ProductBarcodes.Where(b => ids.Contains(b.ProductID)).Select(b => new { b.ProductID, b.Barcode }).ToList()
                .GroupBy(b => b.ProductID).ToDictionary(g => g.Key, g => string.Join(" ", g.Select(x => x.Barcode)));
            foreach (var r in rows)
            {
                if (expenseCosts.TryGetValue(r.ProductID, out var c) && c > 0) r.UnitCost = Math.Round(c, 6);
                r.Barcodes = barcodes.TryGetValue(r.ProductID, out var b) ? b : null;
            }
            return rows;
        }

        public static bool CanReceive(DistributionDbContext db, int warehouseId, int userId, bool isAdmin) =>
            isAdmin || db.Warehouses.Any(w => w.ID == warehouseId && w.MRP == userId);

        /// <summary>Qabul va dalolatnoma (o'tkazilgan hujjat). DLOC omborda joy — <paramref name="locationId"/>. Chaqiruvchi Commit qiladi.</summary>
        public static int Receive(DistributionUnitOfWork uow, int incomeId, IList<TransferReceiveLine> input, int? locationId, int? returnLocationId,
            string note, int userId, bool isAdmin, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var income = erp.INCOMES.Find(incomeId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (income.IsDeleted == true || income.RecieveStatus == true || income.SenderWHID == null)
                throw new DstValidationException(DstLocalization.T("Transfer_NotPending"));
            if (!CanReceive(db, income.ReceivingWHID, userId, isAdmin)) throw new DstValidationException(DstLocalization.T("Transfer_NotResponsible"));
            var policy = WarehouseFlowService.ValidateIncomeReceive(erp, income);
            if (policy != null) throw new DstValidationException(policy);

            var lines = Lines(db, erp, incomeId);
            var byId = (input ?? new List<TransferReceiveLine>()).Where(x => x != null).GroupBy(x => x.IncomeProductID).ToDictionary(g => g.Key, g => g.First());
            if (lines.Any(l => !byId.ContainsKey(l.IncomeProductID))) throw new DstValidationException(DstLocalization.T("Transfer_AllLinesRequired"));
            foreach (var l in lines)
            {
                var x = byId[l.IncomeProductID];
                if (x.ReceivedQty < 0) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
                if (x.ReceivedQty < l.SentQty && !TransferResolutions.All.Contains(x.Resolution ?? ""))
                    throw new DstValidationException(DstLocalization.T("Transfer_ResolutionRequired", l.PNo + " " + l.Product));
            }

            // atomik "band qilish" — ombor modulidagi qabul bilan bir xil (ikki marta qabul bo'lmasin)
            int claimed = erp.Database.ExecuteSqlCommand("UPDATE dbo.INCOMES SET RecieveStatus = 1 WHERE ID = @p0 AND ISNULL(RecieveStatus, 0) = 0", incomeId);
            if (claimed == 0) throw new DstValidationException(DstLocalization.T("Transfer_NotPending"));
            erp.Entry(income).Reload();

            int? expenseId = DocumentMarkers.Parse(income.Description, DocumentMarkers.LinkedExpenseId);
            var act = new TransferAct
            {
                DocNo = DstNumbering.Next(db, "TRANSFER_ACTS", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                IncomeID = incomeId, ExpenseID = expenseId, FromWarehouseID = income.SenderWHID.Value, ToWarehouseID = income.ReceivingWHID,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim().Length > 300 ? note.Trim().Substring(0, 300) : note.Trim(),
                CreatedBy = userId, CreatedAt = now
            };

            var ips = erp.INCOME_PRODUCTS.Where(p => p.IncomeID == incomeId).ToList().ToDictionary(p => p.ID);
            var toReturn = new List<(LineRow Line, decimal Qty)>();
            foreach (var l in lines)
            {
                var x = byId[l.IncomeProductID];
                var ip = ips[l.IncomeProductID];
                decimal received = x.ReceivedQty;
                string resolution = received < l.SentQty ? x.Resolution : null;
                ip.Amount = received;      // FIFO qatlami haqiqiy kelgan miqdorda
                if (ip.PiecePrice == null || ip.PiecePrice == 0) ip.PiecePrice = l.UnitCost;
                erp.SaveChanges();
                try
                {
                    StockLedger.ReceiveLine(erp, income.ReceivingWHID, incomeId, ip.ID, ip.ProductID, received, ip.UnitID, locationId, income.IssueDateTime);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Stock_WriteError") + " " + ex.Message);
                }

                decimal diff = received - l.SentQty;
                decimal value = Math.Round(Math.Abs(diff) * l.UnitCost, 2, MidpointRounding.AwayFromZero);
                if (diff < 0)
                {
                    act.ShortageCost += value;
                    if (resolution == TransferResolutions.Loss) act.LossCost += value;
                    else toReturn.Add((l, -diff));
                }
                else if (diff > 0) act.SurplusCost += value;

                act.Lines.Add(new TransferActLine
                {
                    IncomeProductID = l.IncomeProductID, ProductID = l.ProductID, UnitID = l.UnitID, SentQty = l.SentQty, ReceivedQty = received,
                    UnitCost = l.UnitCost, Resolution = resolution,
                    Note = string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim().Length > 200 ? x.Note.Trim().Substring(0, 200) : x.Note.Trim()
                });
            }
            income.Amount = lines.Sum(l => byId[l.IncomeProductID].ReceivedQty);
            if (expenseId.HasValue)
            {
                var expense = erp.EXPENSES.Find(expenseId.Value);
                if (expense != null) expense.SendStatus = true;
            }
            erp.SaveChanges();

            db.TransferActs.Add(act);
            db.SaveChanges();

            if (toReturn.Count > 0)
            {
                // qaytariladigan miqdor qoldiq birligiga (asosiy birlikka) o'girilmaydi — kirim qatorining o'z birligida yoziladi
                var back = new INCOME
                {
                    DocNo = act.DocNo + "-RET", ReceivingWHID = act.FromWarehouseID, IssueDateTime = now, CurrencyID = income.CurrencyID,
                    IsDeleted = false, RecieveStatus = true,
                    Description = "Ko'chirma kamomadi qaytarildi " + act.DocNo + " (" + income.DocNo + ") URL: /Distribution/DstTransfers/Details/" + act.ID
                };
                erp.INCOMES.Add(back);
                erp.SaveChanges();
                foreach (var (l, qty) in toReturn)
                {
                    var ip = new INCOME_PRODUCTS { IncomeID = back.ID, ProductID = l.ProductID, Amount = qty, PiecePrice = l.UnitCost, UnitID = l.UnitID, Comment = act.DocNo };
                    erp.INCOME_PRODUCTS.Add(ip);
                    erp.SaveChanges();
                    try
                    {
                        StockLedger.ReceiveLine(erp, act.FromWarehouseID, back.ID, ip.ID, l.ProductID, qty, l.UnitID, returnLocationId, now);
                    }
                    catch (Exception ex) when (!(ex is DstValidationException))
                    {
                        throw new DstValidationException(DstLocalization.T("Stock_WriteError") + " " + ex.Message);
                    }
                }
                back.Amount = toReturn.Sum(r => r.Qty);
                back.TotalPrice = Math.Round(toReturn.Sum(r => r.Qty * r.Line.UnitCost), 2);
                erp.SaveChanges();
                act.ReturnIncomeID = back.ID;
            }

            DocumentLifecycle.Post(act, userId, now);
            if (act.ShortageCost > 0 || act.SurplusCost > 0)
            {
                var from = db.Warehouses.Where(w => w.ID == act.FromWarehouseID).Select(w => w.WHName).FirstOrDefault();
                var to = db.Warehouses.Where(w => w.ID == act.ToWarehouseID).Select(w => w.WHName).FirstOrDefault();
                int shortLines = act.Lines.Count(x => x.ReceivedQty < x.SentQty), surplusLines = act.Lines.Count(x => x.ReceivedQty > x.SentQty);
                NotificationService.ToManagers(db, NotifyType,
                    l => DstLocalization.TFor(l, "Notify_TransferDiff", act.DocNo, income.DocNo, from, to,
                        act.ShortageCost.ToString("N0"), act.LossCost.ToString("N0"), act.SurplusCost.ToString("N0")),
                    l => DstLocalization.TFor(l, "Notify_TransferDiffQty", act.DocNo, income.DocNo, from, to, shortLines, surplusLines),
                    null, now, userId, CanSeeCostOverride);
            }
            db.SaveChanges();
            return act.ID;
        }
    }
}
