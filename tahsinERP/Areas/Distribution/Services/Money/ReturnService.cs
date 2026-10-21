using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services.Money
{
    public sealed class ReturnLineInput
    {
        /// <summary>Jo'natish qatori (buyurtma bo'yicha qaytarish) — narx va tannarx undan olinadi.</summary>
        public int? ShipmentLineID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyBase { get; set; }
        /// <summary>Buyurtmasiz qaytarishda asosiy birlik narxi (bo'sh — mijozning joriy narxi).</summary>
        public decimal? UnitPrice { get; set; }
    }

    public sealed class ReturnInput
    {
        public int CustomerID { get; set; }
        public int? OrderID { get; set; }
        public int WarehouseID { get; set; }
        /// <summary>DLOC (joylashuvli) omborda majburiy.</summary>
        public int? LocationID { get; set; }
        public int? ReasonID { get; set; }
        public bool IsDefect { get; set; }
        public string Note { get; set; }
        public List<ReturnLineInput> Lines { get; set; } = new List<ReturnLineInput>();
    }

    /// <summary>
    /// Mijozdan qaytarish: ombor kirimi (dbo.INCOMES + StockLedger.ReceiveLine) chiqimdagi tannarx bilan (FIFO buzilmaydi),
    /// mijoz qarziga kredit. Buyurtma bo'yicha qaytarishda jo'natilgandan ko'p qaytarib bo'lmaydi.
    /// </summary>
    public static class ReturnService
    {
        public const string Prefix = "DQ";

        public static int Create(DistributionUnitOfWork uow, ReturnInput input, int userId, string source, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var lines = (input.Lines ?? new List<ReturnLineInput>()).Where(l => l != null && l.QtyBase > 0).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            if (db.CustomerProfiles.Find(input.CustomerID) == null)
                throw new DstValidationException(DstLocalization.T("Order_CustomerNotDistribution"));
            if (input.ReasonID.HasValue && !db.Reasons.Any(r => r.ID == input.ReasonID && r.Type == ReasonTypes.Return))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (!db.Warehouses.Any(w => w.ID == input.WarehouseID && w.IsDeleted != true))
                throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
            if (StockLedger.IsDlocWarehouse(erp, input.WarehouseID) && !input.LocationID.HasValue)
                throw new DstValidationException(DstLocalization.T("Return_LocationRequired"));
            var defectWarehouse = Bot.DstSettings.Int(db, DstSetting.DefectWarehouseId);
            if (input.IsDefect && defectWarehouse.HasValue && input.WarehouseID != defectWarehouse.Value)
                throw new DstValidationException(DstLocalization.T("Return_DefectWarehouse",
                    db.Warehouses.Where(w => w.ID == defectWarehouse.Value).Select(w => w.WHName).FirstOrDefault()));

            // narx va tannarx
            var shipmentLineIds = lines.Where(l => l.ShipmentLineID.HasValue).Select(l => l.ShipmentLineID.Value).Distinct().ToList();
            var shipped = (from sl in db.ShipmentLines
                           where shipmentLineIds.Contains(sl.ID)
                           join s in db.Shipments on sl.ShipmentID equals s.ID
                           join o in db.Orders on s.OrderID equals o.ID
                           select new { sl, s.Status, o.CustomerID, s.OrderID }).ToList().ToDictionary(x => x.sl.ID);
            var returned = db.ReturnLines
                .Where(r => r.ShipmentLineID != null && shipmentLineIds.Contains(r.ShipmentLineID.Value) && r.Return.Status == DocumentStatus.Posted)
                .GroupBy(r => r.ShipmentLineID.Value).Select(g => new { g.Key, Qty = g.Sum(x => x.QtyBase) }).ToList()
                .ToDictionary(x => x.Key, x => x.Qty);

            var freeProducts = lines.Where(l => !l.ShipmentLineID.HasValue).Select(l => l.ProductID).Distinct().ToList();
            var prices = new PricingService(db).Resolve(input.CustomerID, freeProducts, now);

            var entities = new List<DstReturnLine>();
            int? orderId = input.OrderID;
            foreach (var l in lines)
            {
                decimal unitPrice, unitCost;
                if (l.ShipmentLineID.HasValue)
                {
                    if (!shipped.TryGetValue(l.ShipmentLineID.Value, out var s) || s.CustomerID != input.CustomerID || s.Status != DocumentStatus.Posted)
                        throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    returned.TryGetValue(s.sl.ID, out var already);
                    if (l.QtyBase > s.sl.QtyBase - already)
                        throw new DstValidationException(DstLocalization.T("Return_OverShipped", Math.Max(0, s.sl.QtyBase - already).ToString("0.####")));
                    returned[s.sl.ID] = already + l.QtyBase;
                    unitPrice = s.sl.QtyBase == 0 ? 0 : s.sl.Amount / s.sl.QtyBase;
                    unitCost = s.sl.QtyBase == 0 ? 0 : (s.sl.CostAmount ?? 0) / s.sl.QtyBase;
                    l.ProductID = s.sl.ProductID;
                    orderId = orderId ?? s.OrderID;
                }
                else
                {
                    if (!db.Products.Any(p => p.ID == l.ProductID && !p.IsDeleted)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var resolved = prices.TryGetValue(l.ProductID, out var r) ? r : ResolvedPrice.None;
                    unitPrice = l.UnitPrice ?? resolved.Price ?? throw new DstValidationException(DstLocalization.T("Order_NoPrice", l.ProductID));
                    if (unitPrice < 0) throw new DstValidationException(DstLocalization.T("Error_PriceNegative"));
                    unitCost = LastReceiptCost(erp, l.ProductID);
                }
                entities.Add(new DstReturnLine
                {
                    ProductID = l.ProductID, ShipmentLineID = l.ShipmentLineID, QtyBase = l.QtyBase,
                    UnitPrice = Math.Round(unitPrice, 4), Amount = Math.Round(unitPrice * l.QtyBase, 2, MidpointRounding.AwayFromZero),
                    UnitCost = Math.Round(unitCost, 6)
                });
            }

            var doc = new DstReturn
            {
                DocNo = DstNumbering.Next(db, "D_RETURNS", Prefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                CustomerID = input.CustomerID, OrderID = orderId, WarehouseID = input.WarehouseID, ReasonID = input.ReasonID,
                IsDefect = input.IsDefect, Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                Source = source ?? AuditActor.Web, CreatedBy = userId, CreatedAt = now,
                Total = entities.Sum(e => e.Amount), CostTotal = Math.Round(entities.Sum(e => e.UnitCost * e.QtyBase), 2)
            };

            // ombor kirimi — mavjud ombor moduli bilan bir xil
            var productIds = entities.Select(e => e.ProductID).Distinct().ToList();
            var baseUnits = db.Products.Where(p => productIds.Contains(p.ID)).ToDictionary(p => p.ID, p => p.UnitID);
            var income = new INCOME
            {
                DocNo = doc.DocNo, ReceivingWHID = input.WarehouseID, IssueDateTime = now, CurrencyID = WarehouseAccess.DefaultCurrencyId(erp),
                IsDeleted = false, RecieveStatus = true,
                Description = "Distributsiya qaytarishi " + doc.DocNo + (doc.IsDefect ? " (brak)" : "") + " URL: /Distribution/DstReturns"
            };
            erp.INCOMES.Add(income);
            erp.SaveChanges();
            decimal totalQty = 0;
            foreach (var e in entities)
            {
                var ip = new INCOME_PRODUCTS
                {
                    IncomeID = income.ID, ProductID = e.ProductID, Amount = e.QtyBase, PiecePrice = e.UnitCost,
                    UnitID = baseUnits[e.ProductID], Comment = doc.DocNo
                };
                erp.INCOME_PRODUCTS.Add(ip);
                erp.SaveChanges();
                try
                {
                    StockLedger.ReceiveLine(erp, input.WarehouseID, income.ID, ip.ID, e.ProductID, e.QtyBase, ip.UnitID, input.LocationID, now);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Return_StockError") + " " + ex.Message);
                }
                totalQty += e.QtyBase;
            }
            income.Amount = totalQty;
            income.TotalPrice = doc.CostTotal;
            erp.SaveChanges();

            doc.IncomeID = income.ID;
            foreach (var e in entities) doc.Lines.Add(e);
            DocumentLifecycle.Post(doc, userId, now);
            db.Returns.Add(doc);
            db.SaveChanges();

            var agent = db.CustomerProfiles.Where(p => p.CustomerID == input.CustomerID).Select(p => p.AgentUserID).FirstOrDefault();
            DebtService.Credit(db, input.CustomerID, LedgerDocTypes.Return, doc.ID, orderId, doc.Total, now, agent, userId, doc.DocNo);
            db.SaveChanges();
            DebtService.SettleOrders(db, input.CustomerID, userId, now);
            db.SaveChanges();
            OrderService.RereserveWaiting(db, input.WarehouseID, entities.Select(e => e.ProductID).ToList(), now);   // K2-12
            return doc.ID;
        }

        /// <summary>Qaytarishni bekor qilish: ombor kirimi qaytariladi (qaytgan tovar allaqachon chiqarilgan bo'lsa — rad), qarz tiklanadi.</summary>
        public static void Cancel(DistributionUnitOfWork uow, int returnId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var doc = db.Returns.Find(returnId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            DocumentLifecycle.Cancel(doc, userId, reason, now);
            if (doc.IncomeID.HasValue)
            {
                try
                {
                    StockLedger.ReverseIncomeReceipt(uow.Erp, doc.IncomeID.Value, doc.WarehouseID);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Return_AlreadyIssued") + " " + ex.Message);
                }
                var income = uow.Erp.INCOMES.Find(doc.IncomeID.Value);
                if (income != null) income.IsDeleted = true;
                uow.Erp.SaveChanges();
            }
            DebtService.Reverse(db, LedgerDocTypes.Return, doc.ID, now, userId, doc.DocNo + ": " + doc.CancelReason);
            db.SaveChanges();
            DebtService.ReopenOrders(db, doc.CustomerID, userId, now, doc.DocNo + ": " + doc.CancelReason);
            db.SaveChanges();
        }

        /// <summary>Buyurtmasiz qaytarishda tannarx: tovarning oxirgi kirim narxi (QQSsiz).</summary>
        private static decimal LastReceiptCost(DBTHSNEntities erp, int productId)
        {
            return (from ip in erp.INCOME_PRODUCTS
                    join i in erp.INCOMES on ip.IncomeID equals i.ID
                    where ip.ProductID == productId && i.IsDeleted != true && ip.PiecePrice != null && ip.PiecePrice > 0
                    orderby i.IssueDateTime descending, ip.ID descending
                    select ip.PiecePrice).FirstOrDefault() ?? 0m;
        }
    }
}
