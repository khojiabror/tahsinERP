using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    /// <summary>Buyurtma kartasi, chop etish shakllari va (keyin) bot uchun umumiy ko'rinish modeli.</summary>
    public sealed class OrderDetailsModel
    {
        public sealed class Line
        {
            public int ID { get; set; }
            public int LineNo { get; set; }
            public int ProductID { get; set; }
            public string PNo { get; set; }
            public string Product { get; set; }
            public string Unit { get; set; }
            public string BaseUnit { get; set; }
            public decimal Factor { get; set; }
            public decimal Qty { get; set; }
            public decimal QtyBase { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public decimal Amount { get; set; }
            public decimal QtyShippedBase { get; set; }
            public decimal ReservedBase { get; set; }
            public decimal RemainingBase => QtyBase - QtyShippedBase;
            public decimal WaitingBase => Math.Max(0, RemainingBase - ReservedBase);   // "kirimni kutmoqda"
            public string Barcode { get; set; }
        }

        public sealed class Shipment
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime Date { get; set; }
            public decimal Total { get; set; }
            public decimal? Cost { get; set; }
            public string User { get; set; }
            public List<(int OrderLineID, decimal QtyBase, decimal Amount)> Lines { get; set; }
        }

        public sealed class LogRow
        {
            public DateTime At { get; set; }
            public OrderStatus? From { get; set; }
            public OrderStatus To { get; set; }
            public string User { get; set; }
            public string Note { get; set; }
        }

        public DstOrder Order { get; set; }
        public OrderStatus Status => (OrderStatus)Order.OrderStatus;
        public CustomerRef Customer { get; set; }
        public CustomerProfile Profile { get; set; }
        public string Agent { get; set; }
        public string Warehouse { get; set; }
        public string CreatedBy { get; set; }
        public string OverrideBy { get; set; }
        public CreditState Credit { get; set; }
        public List<Line> Lines { get; set; }
        public List<Shipment> Shipments { get; set; }
        public List<LogRow> Log { get; set; }
        public bool ShowCost { get; set; }
        public decimal? CostTotal => Shipments.Any(s => s.Cost.HasValue) ? Shipments.Sum(s => s.Cost ?? 0) : (decimal?)null;

        public static OrderDetailsModel Load(DistributionDbContext db, DstOrder order, bool showCost, bool russian)
        {
            var lines = (from l in db.OrderLines
                         where l.OrderID == order.ID
                         join p in db.Products on l.ProductID equals p.ID
                         orderby l.LineNo
                         select new
                         {
                             l, p.PNo, p.Name,
                             NameRu = db.ProductProfiles.Where(x => x.ProductID == p.ID).Select(x => x.NameRu).FirstOrDefault(),
                             Unit = db.Units.Where(u => u.ID == l.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                             BaseUnit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault(),
                             Reserved = db.StockReservations.Where(r => r.OrderLineID == l.ID).Sum(r => (decimal?)r.QtyBase) ?? 0,
                             Barcode = db.ProductBarcodes.Where(b => b.ProductID == l.ProductID && b.ProductUnitID == l.ProductUnitID)
                                 .Select(b => b.Barcode).FirstOrDefault()
                         }).ToList();

            var shipments = db.Shipments.Where(s => s.OrderID == order.ID && s.Status != DocumentStatus.Cancelled)
                .OrderBy(s => s.ID).ToList();
            var shipmentIds = shipments.Select(s => s.ID).ToList();
            var shipmentLines = db.ShipmentLines.Where(x => shipmentIds.Contains(x.ShipmentID)).ToList();
            var userIds = shipments.Select(s => s.CreatedBy).Concat(new[] { order.CreatedBy })
                .Concat(new[] { order.AgentUserID ?? 0, order.CreditOverrideBy ?? 0 }).Distinct().ToList();
            var logs = db.OrderStatusLog.Where(x => x.OrderID == order.ID).OrderBy(x => x.ID).ToList();
            userIds.AddRange(logs.Where(x => x.UserID.HasValue).Select(x => x.UserID.Value));
            var users = db.Users.Where(u => userIds.Contains(u.ID)).ToList().ToDictionary(u => u.ID, u => u.FullName ?? u.Uname);
            string U(int? id) => id.HasValue && users.TryGetValue(id.Value, out var n) ? n : null;

            return new OrderDetailsModel
            {
                Order = order,
                ShowCost = showCost,
                Customer = db.Customers.Find(order.CustomerID),
                Profile = db.CustomerProfiles.Find(order.CustomerID),
                Agent = U(order.AgentUserID),
                Warehouse = db.Warehouses.Where(w => w.ID == order.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                CreatedBy = U(order.CreatedBy),
                OverrideBy = U(order.CreditOverrideBy),
                Credit = LedgerService.CreditState(db, order.CustomerID, DateTime.Now),
                Lines = lines.Select(x => new Line
                {
                    ID = x.l.ID, LineNo = x.l.LineNo, ProductID = x.l.ProductID, PNo = x.PNo,
                    Product = russian && !string.IsNullOrWhiteSpace(x.NameRu) ? x.NameRu : x.Name,
                    Unit = x.Unit, BaseUnit = x.BaseUnit, Factor = x.l.Factor, Qty = x.l.Qty, QtyBase = x.l.QtyBase,
                    UnitPrice = x.l.UnitPrice, DiscountPct = x.l.DiscountPct, Amount = x.l.Amount,
                    QtyShippedBase = x.l.QtyShippedBase, ReservedBase = x.Reserved, Barcode = x.Barcode
                }).ToList(),
                Shipments = shipments.Select(s => new Shipment
                {
                    ID = s.ID, DocNo = s.DocNo, Date = s.PostedAt ?? s.CreatedAt, Total = s.Total, Cost = s.CostTotal, User = U(s.CreatedBy),
                    Lines = shipmentLines.Where(x => x.ShipmentID == s.ID).Select(x => (x.OrderLineID, x.QtyBase, x.Amount)).ToList()
                }).ToList(),
                Log = logs.Select(x => new LogRow
                {
                    At = x.At, From = (OrderStatus?)x.FromStatus, To = (OrderStatus)x.ToStatus, User = U(x.UserID), Note = x.Note
                }).ToList()
            };
        }
    }
}
