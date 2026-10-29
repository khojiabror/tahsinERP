using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Services.Editions;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// 2-bosqich: ekspeditor rejimi (bugungi reys, yetkazish — rasm/imzo, qisman/rad, to'lov, xarajat, reysni yopish), mijozga
    /// nakladnoy PDF ma'lumoti (bot hujjatni o'zi tayyorlaydi) va ovozli buyurtma qoralamasi.
    /// Ekspeditor faqat o'z reysi bilan ishlaydi (vakil foydalanuvchi = reys ekspeditori).
    /// </summary>
    public partial class DstApiController
    {
        /// <summary>Bot uchun: rol bayroqlari, ovozli buyurtma yoqilganmi, joriy reys.</summary>
        [HttpGet]
        public ActionResult DeliveryMe()
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var agent = db.SalesAgents.Find(userId);
                    bool expeditor = agent != null && agent.IsActive && agent.IsExpeditor;
                    return new
                    {
                        isExpeditor = expeditor,
                        isAgent = agent != null && agent.IsActive && !agent.IsExpeditor,
                        voiceOrders = DstSettings.Get(db, DeliverySettings.VoiceOrders) == "1",
                        tripId = expeditor ? TripQueries.CurrentTripId(db, userId, DateTime.Now) : null
                    };
                }
            });
        }

        /// <summary>Ekspeditorning joriy reysi (yo'ldagi yoki bugungi rejalangan): to'xtashlar ro'yxati.</summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpGet]
        public ActionResult MyTrip()
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var id = TripQueries.CurrentTripId(db, userId, DateTime.Now);
                    return id.HasValue ? TripJson(TripQueries.Details(db, id.Value, DstLocalization.LanguageOf(userId) == DstLocalization.Russian)) : null;
                }
            });
        }

        [DstApiModule(ProductModule.Distribution)]
        [HttpGet]
        public ActionResult TripStop(int id)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var view = StopOf(db, id, userId);
                    var s = view.Stops.First(x => x.ID == id);
                    return new
                    {
                        trip = new { view.Trip.ID, view.Trip.DocNo, status = (int)view.Trip.TripStatus },
                        stop = StopJson(s, true)
                    };
                }
            });
        }

        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult TripStart(int tripId)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var trip = TripService.Load(uow.Dst, tripId);
                    if (trip.ExpeditorUserID != userId) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    TripService.Start(uow, tripId, userId, DateTime.Now);
                    uow.Commit();
                    return new { ok = true, trip.DocNo };
                }
            });
        }

        public sealed class ApiQty
        {
            public int LineId { get; set; }
            public decimal Qty { get; set; }
        }

        public sealed class ApiDeliverInput
        {
            public int StopId { get; set; }
            public bool All { get; set; }
            public List<ApiQty> Lines { get; set; } = new List<ApiQty>();
            public int? ReasonId { get; set; }
            public string PhotoKey { get; set; }
            public string SignatureKey { get; set; }
            public string Note { get; set; }
            public decimal? Lat { get; set; }
            public decimal? Lng { get; set; }
        }

        /// <summary>Yetkazish natijasi: All — to'liq (rasm yoki imzo majburiy), aks holda qatorlar bo'yicha (qisman/rad — sabab bilan).</summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult StopDeliver(ApiDeliverInput input)
        {
            return Run(userId =>
            {
                if (input == null) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                using (var uow = Uow())
                {
                    var status = TripService.Deliver(uow, new DeliverInput
                    {
                        StopID = input.StopId, All = input.All,
                        Delivered = (input.Lines ?? new List<ApiQty>()).GroupBy(l => l.LineId).ToDictionary(g => g.Key, g => g.Last().Qty),
                        ReasonID = input.ReasonId, PhotoKey = input.PhotoKey, SignatureKey = input.SignatureKey, Note = input.Note,
                        Lat = input.Lat, Lng = input.Lng
                    }, userId, userId, true, DateTime.Now);
                    uow.Commit();
                    var stop = uow.Dst.TripStops.Find(input.StopId);
                    return new { status = status.ToString(), deliveredAmount = stop.DeliveredAmount ?? 0, stop.Amount };
                }
            });
        }

        public sealed class ApiStopPaymentInput
        {
            public int StopId { get; set; }
            public decimal Amount { get; set; }
            public string Method { get; set; }
            public Guid? ClientRequestId { get; set; }
        }

        /// <summary>Ekspeditor to'lov oldi (agent to'lovi kabi: naqd — qo'lida, naqdsiz — usul hisobiga). Mijoz — to'xtashdagi mijoz.</summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult StopPayment(ApiStopPaymentInput input)
        {
            return Run(userId =>
            {
                if (input == null) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                using (var uow = Uow())
                {
                    var stop = uow.Dst.TripStops.Find(input.StopId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var trip = TripService.Load(uow.Dst, stop.TripID);
                    if (trip.ExpeditorUserID != userId) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                    var method = input.Method ?? PaymentMethods.Cash;
                    bool replay = input.ClientRequestId.HasValue && uow.Dst.Payments.Any(p => p.ClientRequestId == input.ClientRequestId);
                    int? account = replay ? null : AgentPayments.ResolveAccount(uow.Dst, method);
                    var id = PaymentService.Receive(uow, new PaymentInput
                    {
                        CustomerID = stop.CustomerID, OrderID = stop.OrderID, Amount = input.Amount, Method = method, FinAccountID = account,
                        ReceivedByUserID = userId, Note = trip.DocNo, ClientRequestId = input.ClientRequestId
                    }, userId, AuditActor.Bot, DateTime.Now);
                    uow.Commit();
                    var p = uow.Dst.Payments.Find(id);
                    return new { id, p.DocNo, p.Amount, onHand = PaymentService.OnHand(uow.Dst, userId).Sum(x => x.Amount) };
                }
            });
        }

        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult TripExpense(int tripId, string kind, decimal amount, string note, string photoKey)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var id = TripService.AddExpense(uow, tripId, kind, null, amount, note, photoKey, userId, userId, AuditActor.Bot, DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        /// <summary>
        /// Ekspeditor reysni yakunlaydi (yopmaydi): qaytgan tovarni ombor sanaydi, reysni ofis yopadi — kamomad o'shanda (05_FINDINGS K2-8).
        /// </summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult TripClose(int tripId)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var r = TripService.Finish(uow, tripId, userId, DateTime.Now);
                    uow.Commit();
                    var docNo = uow.Dst.Trips.Where(t => t.ID == tripId).Select(t => t.DocNo).FirstOrDefault();
                    var collected = uow.Dst.Payments.Where(p => p.ReceivedByUserID == userId && p.Status == DocumentStatus.Posted && p.Note == docNo)
                        .Select(p => (decimal?)p.Amount).Sum() ?? 0;
                    return new
                    {
                        docNo, delivered = r.Delivered, partial = r.Partial, refused = r.Refused, r.DeliveredAmount, r.ReturnedAmount, collected,
                        onHand = PaymentService.OnHand(uow.Dst, userId).Sum(x => x.Amount)
                    };
                }
            });
        }

        /// <summary>Nakladnoy ma'lumoti (bildirishnoma bo'yicha bot PDF tayyorlaydi; tizim chaqiruvi).</summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpGet]
        public ActionResult StopInvoice(int stopId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                    return TripQueries.Invoice(db, stopId, DateTime.Now);
            });
        }

        /// <summary>Ovozli buyurtma qoralamasi: mijoz (berilgan yoki nomidan) va qatorlar katalogdan; tasdiqlangach — oddiy Order.</summary>
        [DstApiModule(ProductModule.Distribution)]
        [HttpPost]
        public ActionResult VoiceDraft(VoiceDraftInput input)
        {
            return Run(userId =>
            {
                if (input == null) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                using (var db = Db())
                {
                    if (DstSettings.Get(db, DeliverySettings.VoiceOrders) != "1") throw new DstValidationException(DstLocalization.T("Voice_Disabled"));
                    if (!db.SalesAgents.Any(a => a.UserID == userId && a.IsActive))   // ekspeditor/omborchi — yo'q (K3-35)
                        throw new DstValidationException(DstLocalization.T("Voice_AgentOnly"));
                    var scopes = new ScopeService(db);
                    var visible = from p in db.CustomerProfiles
                                  join c in db.Customers on p.CustomerID equals c.ID
                                  where c.IsDeleted != true
                                  select new { c.ID, p.AgentUserID, p.TerritoryID };
                    visible = scopes.For(UserName, ScopeTypes.Customer).Apply(visible, x => x.ID);
                    visible = scopes.For(UserName, ScopeTypes.Territory).Apply(visible, x => x.TerritoryID);
                    visible = scopes.AgentsFor(UserName).Apply(visible, x => x.AgentUserID);
                    var warehouse = db.SalesAgents.Where(a => a.UserID == userId).Select(a => a.WarehouseID).FirstOrDefault();
                    var d = VoiceOrderService.Draft(db, input, visible.Select(x => x.ID), warehouse, DateTime.Now);
                    return new
                    {
                        customerId = d.CustomerID, customer = d.Customer,
                        customerOptions = d.CustomerOptions.Select(o => new { id = o.Id, name = o.Name }).ToList(),
                        lines = d.Lines, unmatched = d.Unmatched, total = d.Total
                    };
                }
            });
        }

        // ---------- yordamchilar ----------

        private static TripView StopOf(DistributionDbContext db, int stopId, int expeditorUserId)
        {
            var tripId = db.TripStops.Where(s => s.ID == stopId).Select(s => (int?)s.TripID).FirstOrDefault()
                         ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var view = TripQueries.Details(db, tripId, DstLocalization.LanguageOf(expeditorUserId) == DstLocalization.Russian);
            if (view.Trip.ExpeditorUserID != expeditorUserId) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            return view;
        }

        private static object TripJson(TripView v) => new
        {
            v.Trip.ID, v.Trip.DocNo, date = v.Trip.DocDate.ToString("dd.MM.yyyy"), status = (int)v.Trip.TripStatus, statusName = v.StatusName,
            vehicle = v.Vehicle?.PlateNo, v.Warehouse, v.Trip.WeightKg, v.Trip.Total, routeUrl = v.RouteUrl,
            expenses = v.ExpensesTotal,
            stops = v.Stops.Select(s => StopJson(s, false)).ToList()
        };

        private static object StopJson(TripStopView s, bool withLines) => new
        {
            s.ID, s.Seq, s.Customer, s.Address, s.Landmark, s.Phone, s.Lat, s.Lng, s.OrderNo, s.ShipmentNo, s.Amount,
            deliveredAmount = s.DeliveredAmount, status = (int)s.Status, statusName = s.StatusName, s.Reason, s.Debt, s.WeightKg,
            navUrl = s.Lat.HasValue && s.Lng.HasValue ? DeliveryMath.NavigationUrl(s.Lat.Value, s.Lng.Value) : null,
            lines = withLines
                ? s.Lines.Select(l => new { l.ID, l.Product, l.Unit, qty = l.QtyBase, l.Amount, delivered = l.QtyDelivered }).ToList()
                : null
        };
    }
}
