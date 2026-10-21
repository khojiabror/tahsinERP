using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    public sealed class TripLineView
    {
        public int ID { get; set; }
        public int ShipmentLineID { get; set; }
        public int ProductID { get; set; }
        public string PNo { get; set; }
        public string Product { get; set; }
        public string Unit { get; set; }
        public decimal QtyBase { get; set; }
        public decimal Amount { get; set; }
        public decimal? QtyDelivered { get; set; }
        public decimal? QtyReturned { get; set; }
        public decimal UnitPrice => QtyBase == 0 ? 0 : Math.Round(Amount / QtyBase, 2);
        public decimal Undelivered => QtyDelivered.HasValue ? QtyBase - QtyDelivered.Value : 0;
    }

    public sealed class TripStopView
    {
        public int ID { get; set; }
        public int Seq { get; set; }
        public int ShipmentID { get; set; }
        public string ShipmentNo { get; set; }
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public int CustomerID { get; set; }
        public string Customer { get; set; }
        public string Address { get; set; }
        public string Landmark { get; set; }
        public string Phone { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public byte Status { get; set; }
        public decimal Amount { get; set; }
        public decimal? DeliveredAmount { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string PhotoKey { get; set; }
        public string SignatureKey { get; set; }
        public string Reason { get; set; }
        public string Note { get; set; }
        public int? ReturnID { get; set; }
        public string ReturnNo { get; set; }
        public decimal ShortageAmount { get; set; }
        public decimal Debt { get; set; }
        public string StatusName { get; set; }
        public List<TripLineView> Lines { get; set; } = new List<TripLineView>();
    }

    public sealed class TripExpenseView
    {
        public int ID { get; set; }
        public string Kind { get; set; }
        public string KindName { get; set; }
        public int? CategoryID { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public string Account { get; set; }
        public byte Status { get; set; }
        public string Note { get; set; }
        public string PhotoKey { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TripView
    {
        public Trip Trip { get; set; }
        public Vehicle Vehicle { get; set; }
        public string Expeditor { get; set; }
        public string ExpeditorPhone { get; set; }
        public string Warehouse { get; set; }
        public GeoPoint Depot { get; set; }
        public List<TripStopView> Stops { get; set; } = new List<TripStopView>();
        public List<TripExpenseView> Expenses { get; set; } = new List<TripExpenseView>();
        public decimal? LoadPctKg => DeliveryMath.LoadPct(Trip.WeightKg, Vehicle?.CapacityKg);
        public decimal? LoadPctM3 => DeliveryMath.LoadPct(Trip.VolumeM3, Vehicle?.CapacityM3);
        public decimal ExpensesTotal => Expenses.Where(e => e.Status != TripExpense.Cancelled).Sum(e => e.Amount);
        public string StatusName { get; set; }
        public string RouteUrl => DeliveryMath.RouteUrl(Depot?.Lat, Depot?.Lng, Stops.OrderBy(s => s.Seq).Select(s => new GeoPoint { Id = s.ID, Lat = s.Lat, Lng = s.Lng }));
    }

    public sealed class AvailableShipment
    {
        public int ID { get; set; }
        public string DocNo { get; set; }
        public DateTime Date { get; set; }
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int CustomerID { get; set; }
        public string Customer { get; set; }
        public string Address { get; set; }
        public string Territory { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public decimal Total { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
    }

    /// <summary>Reys ko'rinishlari: web sahifalar, API (bot) va nakladnoy PDF uchun bitta manba.</summary>
    public static class TripQueries
    {
        public static TripView Details(DistributionDbContext db, int tripId, bool russian = false)
        {
            var trip = db.Trips.AsNoTracking().FirstOrDefault(t => t.ID == tripId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var view = new TripView
            {
                Trip = trip,
                Vehicle = db.Vehicles.AsNoTracking().FirstOrDefault(v => v.ID == trip.VehicleID),
                Expeditor = db.Users.Where(u => u.ID == trip.ExpeditorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                ExpeditorPhone = db.SalesAgents.Where(a => a.UserID == trip.ExpeditorUserID).Select(a => a.Phone).FirstOrDefault(),
                Warehouse = db.Warehouses.Where(w => w.ID == trip.WarehouseID).Select(w => w.WHName).FirstOrDefault(),
                Depot = TripService.Depot(db, trip.WarehouseID),
                StatusName = DstLocalization.T("TripStatus_" + (TripStatus)trip.TripStatus)
            };

            var stops = (from s in db.TripStops.AsNoTracking()
                         where s.TripID == tripId
                         join c in db.Customers on s.CustomerID equals c.ID
                         join sh in db.Shipments on s.ShipmentID equals sh.ID
                         join o in db.Orders on s.OrderID equals o.ID
                         let p = db.CustomerProfiles.FirstOrDefault(x => x.CustomerID == s.CustomerID)
                         orderby s.Seq
                         select new
                         {
                             s, Customer = c.Name, c.Address, c.Telephone, ShipmentNo = sh.DocNo, OrderNo = o.DocNo,
                             Landmark = p.Landmark, p.Lat, p.Lng,
                             Reason = db.Reasons.Where(r => r.ID == s.ReasonID).Select(r => russian && r.NameRu != null ? r.NameRu : r.Name).FirstOrDefault(),
                             ReturnNo = db.Returns.Where(r => r.ID == s.ReturnID).Select(r => r.DocNo).FirstOrDefault()
                         }).ToList();
            var stopIds = stops.Select(x => x.s.ID).ToList();
            var lines = (from l in db.TripStopLines.AsNoTracking()
                         where stopIds.Contains(l.StopID)
                         join p in db.Products on l.ProductID equals p.ID
                         select new
                         {
                             l, p.PNo, p.Name, NameRu = db.ProductProfiles.Where(x => x.ProductID == p.ID).Select(x => x.NameRu).FirstOrDefault(),
                             Unit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault()
                         }).ToList();
            var debts = Money.DebtService.Load(db, stops.Select(x => x.s.CustomerID).Distinct().ToList());
            foreach (var x in stops)
            {
                var aging = debts.TryGetValue(x.s.CustomerID, out var d) ? Money.DebtMath.Aging(d.Debits, d.Credits, DateTime.Today) : new Money.AgingBuckets();
                view.Stops.Add(new TripStopView
                {
                    ID = x.s.ID, Seq = x.s.Seq, ShipmentID = x.s.ShipmentID, ShipmentNo = x.ShipmentNo, OrderID = x.s.OrderID, OrderNo = x.OrderNo,
                    CustomerID = x.s.CustomerID, Customer = x.Customer, Address = x.Address, Landmark = x.Landmark, Phone = x.Telephone,
                    Lat = x.Lat, Lng = x.Lng, Status = x.s.StopStatus, StatusName = DstLocalization.T("StopStatus_" + (StopStatus)x.s.StopStatus),
                    Amount = x.s.Amount, DeliveredAmount = x.s.DeliveredAmount, WeightKg = x.s.WeightKg, VolumeM3 = x.s.VolumeM3,
                    DeliveredAt = x.s.DeliveredAt, PhotoKey = x.s.PhotoKey, SignatureKey = x.s.SignatureKey, Reason = x.Reason, Note = x.s.Note,
                    ReturnID = x.s.ReturnID, ReturnNo = x.ReturnNo, ShortageAmount = x.s.ShortageAmount, Debt = aging.Total,
                    Lines = lines.Where(l => l.l.StopID == x.s.ID).OrderBy(l => l.l.ID).Select(l => new TripLineView
                    {
                        ID = l.l.ID, ShipmentLineID = l.l.ShipmentLineID, ProductID = l.l.ProductID, PNo = l.PNo,
                        Product = russian && !string.IsNullOrEmpty(l.NameRu) ? l.NameRu : l.Name, Unit = l.Unit,
                        QtyBase = l.l.QtyBase, Amount = l.l.Amount, QtyDelivered = l.l.QtyDelivered, QtyReturned = l.l.QtyReturned
                    }).ToList()
                });
            }

            view.Expenses = (from e in db.TripExpenses.AsNoTracking()
                             where e.TripID == tripId
                             orderby e.ID
                             select new
                             {
                                 e,
                                 Account = db.FinAccounts.Where(a => a.ID == e.FinAccountID).Select(a => a.Name).FirstOrDefault()
                             }).ToList()
                .Select(x => new TripExpenseView
                {
                    ID = x.e.ID, Kind = x.e.Kind, KindName = DstLocalization.T("TripExpense_" + x.e.Kind), CategoryID = x.e.CategoryID,
                    Amount = x.e.Amount, Account = x.Account, Status = x.e.Status, Note = x.e.Note, PhotoKey = x.e.PhotoKey, Source = x.e.Source,
                    CreatedAt = x.e.CreatedAt
                }).ToList();
            if (view.Expenses.Any(e => e.CategoryID.HasValue))
            {
                var ids = view.Expenses.Where(e => e.CategoryID.HasValue).Select(e => e.CategoryID.Value).Distinct().ToList();
                var names = db.Database.SqlQuery<IdName>("SELECT ID, Name FROM dbo.FIN_CATEGORIES WHERE ID IN (" + string.Join(",", ids) + ")").ToList()
                    .ToDictionary(n => n.ID, n => n.Name);
                foreach (var e in view.Expenses.Where(e => e.CategoryID.HasValue))
                    e.Category = names.TryGetValue(e.CategoryID.Value, out var n) ? n : null;
            }
            return view;
        }

        public sealed class IdName
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        /// <summary>Reysga biriktirish mumkin bo'lgan jo'natishlar: o'tkazilgan, yetkazish (o'zi olib ketish emas), yetkazilmagan, reyssiz.</summary>
        public static List<AvailableShipment> Available(DistributionDbContext db, int warehouseId, int? exceptTripId)
        {
            var rows = (from s in db.Shipments
                        join o in db.Orders on s.OrderID equals o.ID
                        join c in db.Customers on o.CustomerID equals c.ID
                        let p = db.CustomerProfiles.FirstOrDefault(x => x.CustomerID == o.CustomerID)
                        where s.WarehouseID == warehouseId && s.Status == DocumentStatus.Posted && !s.IsPickup && o.DeliveredAt == null
                              && o.Status != DocumentStatus.Cancelled
                              && !db.TripStops.Any(t => t.ShipmentID == s.ID && t.TripID != exceptTripId)
                        orderby o.DeliveryDate, s.ID
                        select new AvailableShipment
                        {
                            ID = s.ID, DocNo = s.DocNo, Date = s.DocDate, OrderID = o.ID, OrderNo = o.DocNo, DeliveryDate = o.DeliveryDate,
                            CustomerID = c.ID, Customer = c.Name, Address = c.Address, Lat = p.Lat, Lng = p.Lng, Total = s.Total,
                            Territory = db.Territories.Where(t => t.ID == p.TerritoryID).Select(t => t.Name).FirstOrDefault()
                        }).Take(500).ToList();
            var loads = TripService.Loads(db, rows.Select(r => r.ID).ToList());
            foreach (var r in rows)
            {
                r.WeightKg = Math.Round(loads[r.ID].WeightKg, 3);
                r.VolumeM3 = Math.Round(loads[r.ID].VolumeM3, 4);
            }
            return rows;
        }

        /// <summary>Ekspeditorning joriy reysi: yo'ldagisi, bo'lmasa bugungi (yoki eng yaqin) rejalangani.</summary>
        public static int? CurrentTripId(DistributionDbContext db, int expeditorUserId, DateTime today)
        {
            var active = db.Trips.Where(t => t.ExpeditorUserID == expeditorUserId && t.TripStatus == (byte)TripStatus.InTransit)
                .OrderBy(t => t.DocDate).Select(t => (int?)t.ID).FirstOrDefault();
            if (active.HasValue) return active;
            var since = today.Date.AddDays(-1);
            return db.Trips.Where(t => t.ExpeditorUserID == expeditorUserId && t.TripStatus == (byte)TripStatus.Planned && t.DocDate >= since)
                .OrderBy(t => t.DocDate).ThenBy(t => t.ID).Select(t => (int?)t.ID).FirstOrDefault();
        }

        public sealed class InvoiceLine
        {
            public string Product { get; set; }
            public string Unit { get; set; }
            public decimal Qty { get; set; }
            public decimal Price { get; set; }
            public decimal Amount { get; set; }
        }

        public sealed class InvoiceData
        {
            public string Company { get; set; }
            public string Warehouse { get; set; }
            public string ShipmentNo { get; set; }
            public string OrderNo { get; set; }
            public string Date { get; set; }
            public string Customer { get; set; }
            public string Inn { get; set; }
            public string Address { get; set; }
            public string Phone { get; set; }
            public string Expeditor { get; set; }
            public string Vehicle { get; set; }
            public string TripNo { get; set; }
            public bool Partial { get; set; }
            public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
            public decimal Total { get; set; }
            public decimal Debt { get; set; }
            public string DeliveredAt { get; set; }
        }

        /// <summary>Yetkazilgan to'xtash bo'yicha nakladnoy ma'lumoti (bot PDF tayyorlaydi): yetkazilgan miqdorlar, narx, jami, qarz.</summary>
        public static InvoiceData Invoice(DistributionDbContext db, int stopId, DateTime now)
        {
            var stop = db.TripStops.AsNoTracking().FirstOrDefault(s => s.ID == stopId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var view = Details(db, stop.TripID);
            var s = view.Stops.First(x => x.ID == stopId);
            var profile = db.CustomerProfiles.AsNoTracking().FirstOrDefault(p => p.CustomerID == s.CustomerID);
            var company = db.Database.SqlQuery<string>("SELECT TOP 1 Name FROM dbo.COMPANIES WHERE ISNULL(IsActive, 1) = 1 ORDER BY ID").FirstOrDefault();
            var data = new InvoiceData
            {
                Company = company, Warehouse = view.Warehouse, ShipmentNo = s.ShipmentNo, OrderNo = s.OrderNo,
                Date = (s.DeliveredAt ?? now).ToString("dd.MM.yyyy"), DeliveredAt = s.DeliveredAt?.ToString("dd.MM.yyyy HH:mm"),
                Customer = s.Customer, Inn = profile?.INN, Address = s.Address, Phone = s.Phone,
                Expeditor = view.Expeditor, Vehicle = view.Vehicle?.PlateNo, TripNo = view.Trip.DocNo, Partial = s.Status == (byte)StopStatus.Partial,
                Debt = Money.DebtService.Aging(db, s.CustomerID, now).Total
            };
            foreach (var l in s.Lines)
            {
                decimal qty = l.QtyDelivered ?? l.QtyBase;
                if (qty <= 0) continue;
                data.Lines.Add(new InvoiceLine
                {
                    Product = l.PNo + " — " + l.Product, Unit = l.Unit, Qty = qty, Price = l.UnitPrice,
                    Amount = DeliveryMath.Share(l.Amount, qty, l.QtyBase)
                });
            }
            data.Total = data.Lines.Sum(l => l.Amount);
            return data;
        }
    }
}
