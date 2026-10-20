using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    public sealed class TripInput
    {
        public int? ID { get; set; }
        public DateTime DocDate { get; set; }
        public int VehicleID { get; set; }
        public int ExpeditorUserID { get; set; }
        public int? WarehouseID { get; set; }
        public string Note { get; set; }
        /// <summary>Jo'natishlar marshrut tartibida.</summary>
        public List<int> ShipmentIDs { get; set; } = new List<int>();
    }

    public sealed class DeliverInput
    {
        public int StopID { get; set; }
        /// <summary>true — hammasi yetkazildi (Delivered e'tiborsiz).</summary>
        public bool All { get; set; }
        /// <summary>TRIP_STOP_LINES.ID → yetkazilgan miqdor (asosiy birlikda). Bo'sh va All=false — rad.</summary>
        public Dictionary<int, decimal> Delivered { get; set; } = new Dictionary<int, decimal>();
        public int? ReasonID { get; set; }
        public string PhotoKey { get; set; }
        public string SignatureKey { get; set; }
        public string Note { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
    }

    public sealed class CloseInput
    {
        public int TripID { get; set; }
        /// <summary>TRIP_STOP_LINES.ID → omborga qaytgan miqdor; berilmagan qator — yetkazilmaganning hammasi qaytgan.</summary>
        public Dictionary<int, decimal> Returned { get; set; } = new Dictionary<int, decimal>();
        /// <summary>Joylashuvli (DLOC) omborda qaytarish uchun.</summary>
        public int? LocationID { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Jo'natishning og'irligi va hajmi.</summary>
    public sealed class ShipmentLoad
    {
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
    }

    /// <summary>
    /// Reys hayot sikli: rejalandi (jo'natishlar biriktiriladi, sig'im tekshiriladi, tartib) → yo'lda (mijozlarga "yo'lga chiqdi",
    /// ekspeditor yetkazadi: to'liq / qisman / rad) → yopildi (yetkazilmagan tovar — qaytarish hujjati bilan omborga, qaytmagani — kamomad,
    /// farq dalolatnomasi). Barcha usullar chaqiruvchining <see cref="DistributionUnitOfWork"/>'ida; Commit — chaqiruvchida.
    /// </summary>
    public static class TripService
    {
        public const string Prefix = "R";
        public const string NotifyTripAssigned = "TRIP_ASSIGNED";
        public const string NotifyTripStarted = "TRIP_STARTED";
        public const string NotifyStopDelivered = "STOP_DELIVERED";
        public const string NotifyStopProblem = "STOP_PROBLEM";
        public const string NotifyTripClosed = "TRIP_CLOSED";

        // ---------- rejalash ----------

        /// <summary>Reysni saqlash (faqat rejalangan): sarlavha va jo'natishlar ro'yxati (tartib bilan). Sig'imdan oshsa — rad.</summary>
        public static int Save(DistributionUnitOfWork uow, TripInput input, int userId, DateTime now)
        {
            var db = uow.Dst;
            var vehicle = db.Vehicles.Find(input.VehicleID);
            if (vehicle == null || !vehicle.IsActive) throw new DstValidationException(DstLocalization.T("Trip_VehicleRequired"));
            if (!db.SalesAgents.Any(a => a.UserID == input.ExpeditorUserID && a.IsActive && a.IsExpeditor))
                throw new DstValidationException(DstLocalization.T("Trip_ExpeditorRequired"));
            int warehouseId = input.WarehouseID ?? vehicle.WarehouseID ?? throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));
            if (!db.Warehouses.Any(w => w.ID == warehouseId && w.IsDeleted != true))
                throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));

            var shipmentIds = (input.ShipmentIDs ?? new List<int>()).Distinct().ToList();
            Trip trip;
            bool isNew = !input.ID.HasValue;
            int? previousExpeditor = null;
            if (isNew)
            {
                trip = new Trip
                {
                    DocNo = NextDocNo(db, now), Status = DocumentStatus.Draft, TripStatus = (byte)TripStatus.Planned,
                    CreatedBy = userId, CreatedAt = now
                };
            }
            else
            {
                trip = Load(db, input.ID.Value);
                EnsurePlanned(trip);
                previousExpeditor = trip.ExpeditorUserID;
            }

            // jo'natishlar: o'tkazilgan, o'zi olib ketish emas, shu ombordan, buyurtma yetkazilmagan va boshqa reysda emas
            var shipments = (from s in db.Shipments
                             join o in db.Orders on s.OrderID equals o.ID
                             where shipmentIds.Contains(s.ID)
                             select new { s, o.CustomerID, o.DeliveredAt, OrderStatus = o.Status }).ToList();
            foreach (var id in shipmentIds)
            {
                var x = shipments.FirstOrDefault(r => r.s.ID == id) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                if (x.s.Status != DocumentStatus.Posted || x.s.IsPickup || x.DeliveredAt != null || x.OrderStatus == DocumentStatus.Cancelled)
                    throw new DstValidationException(DstLocalization.T("Trip_ShipmentNotDeliverable", x.s.DocNo));
                if (x.s.WarehouseID != warehouseId)
                    throw new DstValidationException(DstLocalization.T("Trip_ShipmentOtherWarehouse", x.s.DocNo));
            }
            var busy = db.TripStops.Where(t => shipmentIds.Contains(t.ShipmentID) && (trip.ID == 0 || t.TripID != trip.ID))
                .Select(t => t.ShipmentID).ToList();
            if (busy.Count > 0)
                throw new DstValidationException(DstLocalization.T("Trip_ShipmentBusy", shipments.First(x => x.s.ID == busy[0]).s.DocNo));

            var loads = Loads(db, shipmentIds);
            decimal weight = loads.Values.Sum(l => l.WeightKg), volume = loads.Values.Sum(l => l.VolumeM3);
            var problems = DeliveryMath.CapacityProblems(weight, volume, vehicle.CapacityKg, vehicle.CapacityM3);
            if (problems.Count > 0) throw new DstValidationException(string.Join(" ", problems));

            trip.DocDate = input.DocDate.Date;
            trip.VehicleID = vehicle.ID;
            trip.ExpeditorUserID = input.ExpeditorUserID;
            trip.WarehouseID = warehouseId;
            trip.Note = string.IsNullOrWhiteSpace(input.Note) ? null : Cut(input.Note.Trim(), 500);
            trip.WeightKg = Math.Round(weight, 3);
            trip.VolumeM3 = Math.Round(volume, 4);
            trip.Total = shipments.Sum(x => x.s.Total);
            if (isNew) db.Trips.Add(trip);
            db.SaveChanges();

            // to'xtashlar: olib tashlanganlari o'chadi, yangilari qo'shiladi, tartib — ro'yxat bo'yicha
            var stops = db.TripStops.Where(s => s.TripID == trip.ID).ToList();
            foreach (var removed in stops.Where(s => !shipmentIds.Contains(s.ShipmentID)).ToList())
            {
                db.TripStopLines.RemoveRange(db.TripStopLines.Where(l => l.StopID == removed.ID).ToList());
                db.TripStops.Remove(removed);
                stops.Remove(removed);
            }
            var shipmentLines = db.ShipmentLines.Where(l => shipmentIds.Contains(l.ShipmentID)).ToList();
            int seq = 0;
            foreach (var id in shipmentIds)
            {
                var x = shipments.First(r => r.s.ID == id);
                var stop = stops.FirstOrDefault(s => s.ShipmentID == id);
                if (stop == null)
                {
                    stop = new TripStop { TripID = trip.ID, ShipmentID = id, OrderID = x.s.OrderID, CustomerID = x.CustomerID, StopStatus = (byte)StopStatus.Pending };
                    foreach (var sl in shipmentLines.Where(l => l.ShipmentID == id))
                        stop.Lines.Add(new TripStopLine { ShipmentLineID = sl.ID, ProductID = sl.ProductID, QtyBase = sl.QtyBase, Amount = sl.Amount });
                    db.TripStops.Add(stop);
                }
                stop.Seq = ++seq;
                stop.Amount = x.s.Total;
                stop.WeightKg = Math.Round(loads[id].WeightKg, 3);
                stop.VolumeM3 = Math.Round(loads[id].VolumeM3, 4);
            }
            db.SaveChanges();

            if (isNew || previousExpeditor != trip.ExpeditorUserID)
            {
                var plate = vehicle.PlateNo;
                NotificationService.ToUser(db, trip.ExpeditorUserID, NotifyTripAssigned,
                    l => DstLocalization.TFor(l, "Notify_TripAssigned", trip.DocNo, trip.DocDate.ToString("dd.MM.yyyy"), plate, shipmentIds.Count,
                        trip.WeightKg.ToString("0.#")), null, now);
                db.SaveChanges();
            }
            return trip.ID;
        }

        /// <summary>Tartibni qo'lda o'zgartirish (rejalangan va yo'ldagi reysda — kutayotgan to'xtashlar uchun ham).</summary>
        public static void Reorder(DistributionUnitOfWork uow, int tripId, IList<int> stopIds)
        {
            var db = uow.Dst;
            var trip = Load(db, tripId);
            if (trip.State != TripStatus.Planned && trip.State != TripStatus.InTransit)
                throw new DstValidationException(DstLocalization.T("Trip_NotEditable"));
            var stops = db.TripStops.Where(s => s.TripID == tripId).ToList();
            if (stopIds == null || stopIds.Count != stops.Count || stopIds.Distinct().Count() != stops.Count || stopIds.Any(id => stops.All(s => s.ID != id)))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            for (int i = 0; i < stopIds.Count; i++)
                stops.First(s => s.ID == stopIds[i]).Seq = i + 1;
            trip.RouteOptimized = false;
            db.SaveChanges();
        }

        /// <summary>Avtomatik tartib: ombordan eng yaqin qo'shni bo'yicha (koordinatasiz mijozlar oxirida).</summary>
        public static decimal Optimize(DistributionUnitOfWork uow, int tripId)
        {
            var db = uow.Dst;
            var trip = Load(db, tripId);
            if (trip.State != TripStatus.Planned && trip.State != TripStatus.InTransit)
                throw new DstValidationException(DstLocalization.T("Trip_NotEditable"));
            var stops = db.TripStops.Where(s => s.TripID == tripId).OrderBy(s => s.Seq).ToList();
            var customerIds = stops.Select(s => s.CustomerID).ToList();
            var geo = db.CustomerProfiles.Where(p => customerIds.Contains(p.CustomerID)).Select(p => new { p.CustomerID, p.Lat, p.Lng }).ToList()
                .ToDictionary(p => p.CustomerID);
            // yetkazilganlar o'z joyida (boshida), kutayotganlar qayta tartiblanadi
            var done = stops.Where(s => s.State != StopStatus.Pending).ToList();
            var pending = stops.Where(s => s.State == StopStatus.Pending).ToList();
            var depot = Depot(db, trip.WarehouseID);
            var points = pending.Select(s => new GeoPoint
            {
                Id = s.ID, Lat = geo.TryGetValue(s.CustomerID, out var g) ? g.Lat : null, Lng = g?.Lng
            }).ToList();
            var order = DeliveryMath.NearestNeighbor(points, depot?.Lat, depot?.Lng);
            int seq = 0;
            foreach (var s in done.OrderBy(s => s.Seq)) s.Seq = ++seq;
            foreach (var id in order) stops.First(s => s.ID == id).Seq = ++seq;
            trip.RouteOptimized = true;
            db.SaveChanges();
            var byId = points.ToDictionary(p => p.Id);
            return DeliveryMath.PathKm(order.Select(id => byId[id]), depot?.Lat, depot?.Lng);
        }

        /// <summary>Yo'lga chiqish: mijozlarga "yo'lga chiqdi" xabari (ekspeditor, telefon, mashina).</summary>
        public static void Start(DistributionUnitOfWork uow, int tripId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var trip = Load(db, tripId);
            EnsurePlanned(trip);
            var stops = db.TripStops.Where(s => s.TripID == tripId).ToList();
            if (stops.Count == 0) throw new DstValidationException(DstLocalization.T("Trip_NoStops"));
            trip.TripStatus = (byte)TripStatus.InTransit;
            trip.StartedAt = now;

            var vehicle = db.Vehicles.Find(trip.VehicleID);
            var expeditor = db.Users.Where(u => u.ID == trip.ExpeditorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault();
            var phone = db.SalesAgents.Where(a => a.UserID == trip.ExpeditorUserID).Select(a => a.Phone).FirstOrDefault();
            var orderIds = stops.Select(s => s.OrderID).Distinct().ToList();
            var orders = db.Orders.Where(o => orderIds.Contains(o.ID)).ToDictionary(o => o.ID);
            foreach (var g in stops.GroupBy(s => s.CustomerID))
            {
                var docs = string.Join(", ", g.Select(s => orders[s.OrderID].DocNo).Distinct());
                var amount = g.Sum(s => s.Amount);
                NotificationService.ToCustomer(db, g.Key, NotifyTripStarted,
                    l => DstLocalization.TFor(l, "Notify_TripStarted", docs, amount.ToString("N0"), expeditor, phone ?? "—", vehicle?.PlateNo), null, now);
            }
            db.SaveChanges();
        }

        /// <summary>Bekor qilish — faqat rejalangan reys (jo'natishlar bo'shaydi va boshqa reysga qo'shilishi mumkin).</summary>
        public static void Cancel(DistributionUnitOfWork uow, int tripId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var trip = Load(db, tripId);
            EnsurePlanned(trip);
            var stops = db.TripStops.Where(s => s.TripID == tripId).ToList();
            var ids = stops.Select(s => s.ID).ToList();
            db.TripStopLines.RemoveRange(db.TripStopLines.Where(l => ids.Contains(l.StopID)).ToList());
            db.TripStops.RemoveRange(stops);
            foreach (var x in db.TripExpenses.Where(e => e.TripID == tripId && e.Status == TripExpense.Pending).ToList())
                x.Status = TripExpense.Cancelled;
            db.SaveChanges();
            DocumentLifecycle.Cancel(trip, userId, reason, now);
            trip.TripStatus = (byte)TripStatus.Cancelled;
            db.SaveChanges();
        }

        // ---------- yetkazish ----------

        /// <summary>
        /// To'xtash natijasi (ekspeditor botda yoki ofis): hammasi / qisman / rad. Bot orqali yetkazishda rasm yoki imzo majburiy
        /// (<paramref name="requireProof"/>), qisman va radda — sabab. Buyurtmaning barcha jo'natishlari yetkazilsa — "Yetkazildi".
        /// Mijozga xabar (yetkazilganda — nakladnoy PDF, bot tayyorlaydi), muammo bo'lsa rahbarlarga.
        /// </summary>
        public static StopStatus Deliver(DistributionUnitOfWork uow, DeliverInput input, int userId, int? expeditorUserId, bool requireProof, DateTime now)
        {
            var db = uow.Dst;
            var stop = db.TripStops.Find(input.StopID) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var trip = Load(db, stop.TripID);
            if (expeditorUserId.HasValue && trip.ExpeditorUserID != expeditorUserId.Value)
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (trip.State != TripStatus.InTransit) throw new DstValidationException(DstLocalization.T("Trip_NotInTransit"));
            if (stop.State != StopStatus.Pending) throw new DstValidationException(DstLocalization.T("Trip_StopAlreadyDone"));

            var lines = db.TripStopLines.Where(l => l.StopID == stop.ID).ToList();
            foreach (var key in (input.Delivered ?? new Dictionary<int, decimal>()).Keys)
                if (lines.All(l => l.ID != key)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var plan = lines.Select(l =>
            {
                decimal q = input.All ? l.QtyBase : (input.Delivered != null && input.Delivered.TryGetValue(l.ID, out var v) ? v : 0);
                if (q < 0) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
                if (q > l.QtyBase) throw new DstValidationException(DstLocalization.T("Trip_DeliveredOverShipped", l.QtyBase.ToString("0.####")));
                return (Line: l, Qty: q);
            }).ToList();
            var status = DeliveryMath.Classify(plan.Select(p => (p.Line.QtyBase, p.Qty)));

            string photo = Key(input.PhotoKey), signature = Key(input.SignatureKey);
            if (status != StopStatus.Refused && requireProof && photo == null && signature == null)
                throw new DstValidationException(DstLocalization.T("Trip_ProofRequired"));
            Reason reason = null;
            if (status != StopStatus.Delivered)
            {
                reason = input.ReasonID.HasValue ? db.Reasons.FirstOrDefault(r => r.ID == input.ReasonID && r.Type == ReasonTypes.Return) : null;
                if (reason == null) throw new DstValidationException(DstLocalization.T("Trip_ReasonRequired"));
            }
            if (input.Lat.HasValue || input.Lng.HasValue) DirectoryRules.ValidateGeo(input.Lat, input.Lng);

            foreach (var p in plan) p.Line.QtyDelivered = p.Qty;
            stop.StopStatus = (byte)status;
            stop.DeliveredAmount = plan.Sum(p => DeliveryMath.Share(p.Line.Amount, p.Qty, p.Line.QtyBase));
            stop.DeliveredAt = now;
            stop.DeliveredBy = userId;
            stop.PhotoKey = photo;
            stop.SignatureKey = signature;
            stop.ReasonID = reason?.ID;
            stop.Note = string.IsNullOrWhiteSpace(input.Note) ? null : Cut(input.Note.Trim(), 300);
            stop.Lat = input.Lat;
            stop.Lng = input.Lng;
            db.SaveChanges();

            var order = db.Orders.Find(stop.OrderID);
            if (status != StopStatus.Refused && OrderFullyHandled(db, order))
                OrderService.MarkDeliveredSilently(uow, order.ID, userId, now, trip.DocNo);

            // xabarlar
            var shipmentNo = db.Shipments.Where(s => s.ID == stop.ShipmentID).Select(s => s.DocNo).FirstOrDefault();
            var reasonName = reason?.Name;
            if (status == StopStatus.Refused)
                NotificationService.ToCustomer(db, stop.CustomerID, NotifyStopDelivered,
                    l => DstLocalization.TFor(l, "Notify_StopRefused", order.DocNo, reasonName), now);
            else
            {
                var delivered = stop.DeliveredAmount ?? 0;
                NotificationService.ToCustomer(db, stop.CustomerID, NotifyStopDelivered,
                    l => status == StopStatus.Delivered
                        ? DstLocalization.TFor(l, "Notify_StopDelivered", order.DocNo, shipmentNo, delivered.ToString("N0"))
                        : DstLocalization.TFor(l, "Notify_StopPartial", order.DocNo, shipmentNo, delivered.ToString("N0"), stop.Amount.ToString("N0"), reasonName),
                    "{\"action\":\"invoice\",\"stopId\":" + stop.ID + "}", now);
            }
            if (status != StopStatus.Delivered)
            {
                var customer = db.Customers.Where(c => c.ID == stop.CustomerID).Select(c => c.Name).FirstOrDefault();
                var expeditor = db.Users.Where(u => u.ID == trip.ExpeditorUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault();
                NotificationService.ToManagers(db, NotifyStopProblem,
                    l => DstLocalization.TFor(l, "Notify_StopProblem", trip.DocNo, customer, order.DocNo,
                        DstLocalization.TFor(l, "StopStatus_" + status), reasonName, expeditor), null, now, userId);
            }
            db.SaveChanges();
            return status;
        }

        /// <summary>Natijani bekor qilish (ofis, reys yopilguncha) — to'xtash yana "kutmoqda".</summary>
        public static void ResetStop(DistributionUnitOfWork uow, int stopId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var stop = db.TripStops.Find(stopId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var trip = Load(db, stop.TripID);
            if (trip.State != TripStatus.InTransit) throw new DstValidationException(DstLocalization.T("Trip_NotInTransit"));
            foreach (var l in db.TripStopLines.Where(l => l.StopID == stopId).ToList()) l.QtyDelivered = null;
            stop.StopStatus = (byte)StopStatus.Pending;
            stop.DeliveredAmount = null;
            stop.DeliveredAt = null;
            stop.DeliveredBy = null;
            stop.ReasonID = null;
            db.SaveChanges();
        }

        /// <summary>Buyurtmaning o'tkazilgan barcha jo'natishlari to'liq jo'natilgan va har biri yetkazilgan (o'zi olib ketish yoki reysda hal qilingan).</summary>
        private static bool OrderFullyHandled(DistributionDbContext db, DstOrder order)
        {
            var status = (OrderStatus)order.OrderStatus;
            if (order.DeliveredAt != null || !(status == OrderStatus.Shipped || status == OrderStatus.Closed)) return false;
            var shipments = db.Shipments.Where(s => s.OrderID == order.ID && s.Status == DocumentStatus.Posted).Select(s => new { s.ID, s.IsPickup }).ToList();
            var ids = shipments.Where(s => !s.IsPickup).Select(s => s.ID).ToList();
            var handled = db.TripStops.Where(t => ids.Contains(t.ShipmentID) && t.StopStatus != (byte)StopStatus.Pending).Select(t => t.ShipmentID).ToList();
            return ids.All(handled.Contains);
        }

        // ---------- yopish ----------

        public sealed class CloseResult
        {
            public int Delivered { get; set; }
            public int Partial { get; set; }
            public int Refused { get; set; }
            public decimal DeliveredAmount { get; set; }
            public decimal ReturnedAmount { get; set; }
            public decimal ShortageAmount { get; set; }
            public decimal ShortageCost { get; set; }
            public List<int> ReturnIds { get; } = new List<int>();
        }

        /// <summary>
        /// Reysni yopish: hamma to'xtash hal qilingan bo'lishi kerak. Yetkazilmagan tovar: qaytgani — qaytarish hujjati (D_RETURNS, ombor kirimi
        /// FIFO tannarxda, mijoz qarzi kamayadi), qaytmagani — kamomad (mijozga tuzatish krediti sotuv narxida, tannarxi reysda — dalolatnoma va
        /// ekspeditor KPI uchun).
        /// </summary>
        public static CloseResult Close(DistributionUnitOfWork uow, CloseInput input, int userId, int? expeditorUserId, string source, DateTime now)
        {
            var db = uow.Dst;
            var trip = Load(db, input.TripID);
            if (expeditorUserId.HasValue && trip.ExpeditorUserID != expeditorUserId.Value)
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (trip.State != TripStatus.InTransit) throw new DstValidationException(DstLocalization.T("Trip_NotInTransit"));
            var stops = db.TripStops.Where(s => s.TripID == trip.ID).OrderBy(s => s.Seq).ToList();
            var pending = stops.Count(s => s.State == StopStatus.Pending);
            if (pending > 0) throw new DstValidationException(DstLocalization.T("Trip_PendingStops", pending));

            var stopIds = stops.Select(s => s.ID).ToList();
            var lines = db.TripStopLines.Where(l => stopIds.Contains(l.StopID)).ToList();
            foreach (var key in (input.Returned ?? new Dictionary<int, decimal>()).Keys)
                if (lines.All(l => l.ID != key)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var slIds = lines.Select(l => l.ShipmentLineID).ToList();
            var costs = db.ShipmentLines.Where(sl => slIds.Contains(sl.ID)).Select(sl => new { sl.ID, sl.CostAmount }).ToList()
                .ToDictionary(x => x.ID, x => x.CostAmount ?? 0);

            // avval hamma tekshiruv
            var math = lines.ToDictionary(l => l.ID, l =>
                DeliveryMath.CloseLine(l.QtyBase, l.QtyDelivered ?? 0, input.Returned != null && input.Returned.TryGetValue(l.ID, out var r) ? r : (decimal?)null));

            var result = new CloseResult();
            var orders = db.Orders.Where(o => db.TripStops.Any(s => s.TripID == trip.ID && s.OrderID == o.ID)).ToDictionary(o => o.ID);
            foreach (var stop in stops)
            {
                switch (stop.State)
                {
                    case StopStatus.Delivered: result.Delivered++; break;
                    case StopStatus.Partial: result.Partial++; break;
                    default: result.Refused++; break;
                }
                result.DeliveredAmount += stop.DeliveredAmount ?? 0;
                var mine = lines.Where(l => l.StopID == stop.ID).ToList();
                foreach (var l in mine) l.QtyReturned = math[l.ID].Returned;

                var back = mine.Where(l => math[l.ID].Returned > 0).ToList();
                if (back.Count > 0)
                {
                    var returnId = ReturnService.Create(uow, new ReturnInput
                    {
                        CustomerID = stop.CustomerID, OrderID = stop.OrderID, WarehouseID = trip.WarehouseID, LocationID = input.LocationID,
                        ReasonID = stop.ReasonID, IsDefect = false, Note = Cut(trip.DocNo + (stop.Note == null ? "" : ": " + stop.Note), 300),
                        Lines = back.Select(l => new ReturnLineInput { ShipmentLineID = l.ShipmentLineID, ProductID = l.ProductID, QtyBase = math[l.ID].Returned }).ToList()
                    }, userId, source, now);
                    stop.ReturnID = returnId;
                    result.ReturnIds.Add(returnId);
                    result.ReturnedAmount += db.Returns.Where(x => x.ID == returnId).Select(x => x.Total).FirstOrDefault();
                }

                decimal shortage = 0, shortageCost = 0;
                foreach (var l in mine.Where(l => math[l.ID].Shortage > 0))
                {
                    shortage += DeliveryMath.Share(l.Amount, math[l.ID].Shortage, l.QtyBase);
                    shortageCost += DeliveryMath.Share(costs.TryGetValue(l.ShipmentLineID, out var c) ? c : 0, math[l.ID].Shortage, l.QtyBase);
                }
                if (shortage > 0)
                {
                    // mijoz olmagan tovar uchun to'lamaydi; tovar omborga qaytmadi — reys kamomadi
                    DebtService.Credit(db, stop.CustomerID, LedgerDocTypes.Adjustment, stop.ID, stop.OrderID, shortage, now,
                        orders.TryGetValue(stop.OrderID, out var o) ? o.AgentUserID : null, userId, trip.DocNo + ": " + DstLocalization.T("Trip_Shortage"));
                    stop.ShortageAmount = shortage;
                    result.ShortageAmount += shortage;
                    result.ShortageCost += shortageCost;
                }
            }

            trip.ShortageCost = Math.Round(result.ShortageCost, 2);
            trip.TripStatus = (byte)TripStatus.Closed;
            trip.FinishedAt = trip.FinishedAt ?? now;
            if (!string.IsNullOrWhiteSpace(input.Note)) trip.Note = Cut(((trip.Note == null ? "" : trip.Note + " | ") + input.Note.Trim()), 500);
            DocumentLifecycle.Post(trip, userId, now);
            db.SaveChanges();
            foreach (var customerId in stops.Select(s => s.CustomerID).Distinct())
                DebtService.SettleOrders(db, customerId, userId, now);

            var expenses = db.TripExpenses.Where(e => e.TripID == trip.ID && e.Status != TripExpense.Cancelled).Select(e => (decimal?)e.Amount).Sum() ?? 0;
            NotificationService.ToManagers(db, NotifyTripClosed,
                l => DstLocalization.TFor(l, "Notify_TripClosed", trip.DocNo, result.Delivered + result.Partial, stops.Count,
                    result.DeliveredAmount.ToString("N0"), result.ReturnedAmount.ToString("N0"), result.ShortageAmount.ToString("N0"), expenses.ToString("N0")),
                null, now, userId);
            db.SaveChanges();
            return result;
        }

        // ---------- xarajatlar ----------

        /// <summary>Xarajat qo'shish (ofis yoki ekspeditor botdan). Kutmoqda holatida; moliyaga — <see cref="PostExpense"/>.</summary>
        public static int AddExpense(DistributionUnitOfWork uow, int tripId, string kind, int? categoryId, decimal amount, string note, string photoKey,
            int userId, int? expeditorUserId, string source, DateTime now)
        {
            var db = uow.Dst;
            var trip = Load(db, tripId);
            if (expeditorUserId.HasValue && trip.ExpeditorUserID != expeditorUserId.Value)
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (trip.State == TripStatus.Cancelled) throw new DstValidationException(DstLocalization.T("Trip_NotEditable"));
            if (amount <= 0) throw new DstValidationException(DstLocalization.T("Payment_AmountPositive"));
            if (!TripExpenseKinds.All.Contains(kind ?? "")) kind = TripExpenseKinds.Other;
            if (categoryId.HasValue) EnsureExpenseCategory(uow, categoryId.Value);
            var x = new TripExpense
            {
                TripID = tripId, Kind = kind, CategoryID = categoryId, Amount = Math.Round(amount, 2), Status = TripExpense.Pending,
                Note = string.IsNullOrWhiteSpace(note) ? null : Cut(note.Trim(), 300), PhotoKey = Key(photoKey),
                Source = source ?? AuditActor.Web, CreatedBy = userId, CreatedAt = now
            };
            db.TripExpenses.Add(x);
            db.SaveChanges();
            return x.ID;
        }

        /// <summary>Xarajatni moliyaga o'tkazish: kassa/hisobdan chiqim (FIN_TRANSACTIONS, Direction=2), kategoriya — chiqim moddasi.</summary>
        public static void PostExpense(DistributionUnitOfWork uow, int expenseId, int? categoryId, int accountId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var x = db.TripExpenses.Find(expenseId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (x.Status != TripExpense.Pending) throw new DstValidationException(DstLocalization.T("Trip_ExpenseNotPending"));
            var trip = Load(db, x.TripID);
            if (trip.State == TripStatus.Cancelled) throw new DstValidationException(DstLocalization.T("Trip_NotEditable"));
            var account = DstFinancePosting.RequireAccount(erp, accountId);
            int category;
            int? chosen = categoryId ?? x.CategoryID;
            if (chosen.HasValue)
                category = EnsureExpenseCategory(uow, chosen.Value);
            else
            {
                var c = tahsinERP.Services.FinancePostingService.EnsureSystemCategory(erp, ExpenseCategoryCode, "Reys xarajatlari", 2);
                if (c.ID == 0) erp.SaveChanges();
                category = c.ID;
            }
            var plate = db.Vehicles.Where(v => v.ID == trip.VehicleID).Select(v => v.PlateNo).FirstOrDefault();
            var tx = new tahsinERP.Models.FIN_TRANSACTIONS
            {
                TxnDate = now, AccountID = account.ID, CategoryID = category, Direction = 2, Amount = x.Amount, CurrencyID = account.CurrencyID,
                CounterpartyName = plate, Purpose = Cut("Reys " + trip.DocNo + ": " + DstLocalization.T("TripExpense_" + x.Kind) + (x.Note == null ? "" : " — " + x.Note), 500),
                SourceDocType = ExpenseSource, SourceDocID = x.ID, CreatedByUserID = userId, CreatedDT = now, IsDeleted = false
            };
            erp.FIN_TRANSACTIONS.Add(tx);
            erp.SaveChanges();
            x.CategoryID = category;
            x.FinAccountID = account.ID;
            x.FinTransactionID = tx.ID;
            x.Status = TripExpense.Posted;
            x.PostedBy = userId;
            x.PostedAt = now;
            db.SaveChanges();
        }

        public static void CancelExpense(DistributionUnitOfWork uow, int expenseId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var x = db.TripExpenses.Find(expenseId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (x.Status == TripExpense.Cancelled) return;
            if (x.Status == TripExpense.Posted) DstFinancePosting.RemovePosting(uow.Erp, x.FinTransactionID);
            x.Status = TripExpense.Cancelled;
            db.SaveChanges();
        }

        public const string ExpenseSource = "DstTripExpense";
        public const string ExpenseCategoryCode = "DST_TRIP_EXPENSE";

        private static int EnsureExpenseCategory(DistributionUnitOfWork uow, int categoryId)
        {
            var c = uow.Erp.FIN_CATEGORIES.Find(categoryId);
            if (c == null || c.IsDeleted == true || !c.IsActive || c.Type != 2)
                throw new DstValidationException(DstLocalization.T("Trip_ExpenseCategoryRequired"));
            return c.ID;
        }

        // ---------- yordamchilar ----------

        /// <summary>Jo'natishlar og'irligi/hajmi: asosiy birlik og'irligi (tovar kartasi), bo'lmasa sotish birligi og'irligi / Factor.</summary>
        public static Dictionary<int, ShipmentLoad> Loads(DistributionDbContext db, IReadOnlyCollection<int> shipmentIds)
        {
            var ids = shipmentIds.ToList();
            var rows = (from sl in db.ShipmentLines
                        where ids.Contains(sl.ShipmentID)
                        join ol in db.OrderLines on sl.OrderLineID equals ol.ID
                        select new
                        {
                            sl.ShipmentID, sl.QtyBase, ol.Factor,
                            BaseW = db.ProductProfiles.Where(p => p.ProductID == sl.ProductID).Select(p => p.BaseWeightKg).FirstOrDefault(),
                            BaseV = db.ProductProfiles.Where(p => p.ProductID == sl.ProductID).Select(p => p.BaseVolumeM3).FirstOrDefault(),
                            UnitW = db.ProductUnits.Where(u => u.ID == ol.ProductUnitID).Select(u => u.WeightKg).FirstOrDefault(),
                            UnitV = db.ProductUnits.Where(u => u.ID == ol.ProductUnitID).Select(u => u.VolumeM3).FirstOrDefault()
                        }).ToList();
            var result = ids.ToDictionary(id => id, id => new ShipmentLoad());
            foreach (var r in rows)
            {
                decimal factor = r.Factor <= 0 ? 1 : r.Factor;
                decimal w = r.BaseW ?? (r.UnitW.HasValue ? r.UnitW.Value / factor : 0);
                decimal v = r.BaseV ?? (r.UnitV.HasValue ? r.UnitV.Value / factor : 0);
                result[r.ShipmentID].WeightKg += r.QtyBase * w;
                result[r.ShipmentID].VolumeM3 += r.QtyBase * v;
            }
            return result;
        }

        /// <summary>Ombor koordinatasi (sozlama DepotGeo_{ombor}: "lat,lng") — marshrut boshi.</summary>
        public static GeoPoint Depot(DistributionDbContext db, int warehouseId)
        {
            var raw = DstSettings.Get(db, DepotKey(warehouseId));
            return DeliveryMath.TryParseGeo(raw, out var lat, out var lng) ? new GeoPoint { Id = warehouseId, Lat = lat, Lng = lng } : null;
        }

        public static string DepotKey(int warehouseId) => "DepotGeo_" + warehouseId;

        public static Trip Load(DistributionDbContext db, int tripId) =>
            db.Trips.Find(tripId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));

        private static void EnsurePlanned(Trip trip)
        {
            if (trip.State != TripStatus.Planned || trip.Status != DocumentStatus.Draft)
                throw new DstValidationException(DstLocalization.T("Trip_NotEditable"));
        }

        /// <summary>R-YYYY-00001 (tranzaksiya ichida, sp_getapplock bilan — DstNumbering bilan bir xil qoida).</summary>
        private static string NextDocNo(DistributionDbContext db, DateTime date)
        {
            if (db.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Hujjat raqami faqat tranzaksiya ichida olinadi.");
            string full = Prefix + "-" + date.Year + "-";
            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51001, 'docno lock timeout', 1;", "dst.docno:TRIPS:" + full);
            long? max = db.Database.SqlQuery<long?>(
                "SELECT MAX(TRY_CAST(SUBSTRING(DocNo, @p0, 30) AS bigint)) FROM dst.TRIPS WHERE DocNo LIKE @p1",
                full.Length + 1, full + "%").FirstOrDefault();
            return full + ((max ?? 0) + 1).ToString().PadLeft(5, '0');
        }

        private static string Key(string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
            if (key != null && key.Length > 200) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            return key;
        }

        private static string Cut(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }
}
