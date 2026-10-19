using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// Bot servisi va Mini App uchun ERP API (/Distribution/DstApi/...). Faqat imzolangan so'rovlar (<see cref="DstApiAuthAttribute"/>);
    /// "vakil foydalanuvchi" (X-Acting-User) nomidan — web bilan bir xil servislar, ruxsatlar va scope'lar.
    /// Javob: JSON (camelCase); biznes xatosi — 400 { error }, topilmadi/ruxsat yo'q — 404/403.
    /// </summary>
    [AllowAnonymous]
    [DstApiAuth]
    public class DstApiController : Controller
    {
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss"
        };

        private int? ActingUserId => HttpContext.Items[DstApiAuthAttribute.ActingUserIdItem] as int?;
        private string UserName => User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

        private ActionResult Ok(object data) =>
            Content(JsonConvert.SerializeObject(data, Json), "application/json", Encoding.UTF8);

        private ActionResult Error(HttpStatusCode code, string message)
        {
            Response.StatusCode = (int)code;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(new { error = message }, Json), "application/json", Encoding.UTF8);
        }

        /// <summary>Vakil foydalanuvchi talab qilinadigan amal; biznes xatolari 400.</summary>
        private ActionResult Run(Func<int, object> body)
        {
            if (!ActingUserId.HasValue) return Error(HttpStatusCode.Forbidden, "X-Acting-User talab qilinadi");
            try
            {
                return Ok(body(ActingUserId.Value));
            }
            catch (CreditBlockedException ex)
            {
                Response.StatusCode = 409;
                Response.TrySkipIisCustomErrors = true;
                return Content(JsonConvert.SerializeObject(new { error = ex.Message, credit = true, problems = ex.Problems }, Json), "application/json", Encoding.UTF8);
            }
            catch (DstValidationException ex)
            {
                return Error(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        private ActionResult RunSystem(Func<object> body)
        {
            try
            {
                return Ok(body());
            }
            catch (DstValidationException ex)
            {
                return Error(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        private DistributionUnitOfWork Uow(string source = AuditActor.Bot) => new DistributionUnitOfWork(new AuditActor(ActingUserId, source));

        private DistributionDbContext Db() => new DistributionDbContext { Actor = new AuditActor(ActingUserId, AuditActor.Bot) };

        /// <summary>Mijoz agentning ko'rinish doirasidami (mijoz/hudud scope + agent ierarxiyasi).</summary>
        private void EnsureCustomer(DistributionDbContext db, int customerId)
        {
            var scopes = new ScopeService(db);
            var p = db.CustomerProfiles.Where(x => x.CustomerID == customerId).Select(x => new { x.AgentUserID, x.TerritoryID }).FirstOrDefault();
            if (p == null || !scopes.For(UserName, ScopeTypes.Customer).Allows(customerId)
                || !scopes.For(UserName, ScopeTypes.Territory).Allows(p.TerritoryID)
                || !scopes.AgentsFor(UserName).Allows(p.AgentUserID))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        // ---------- bog'lash (vakil foydalanuvchisiz) ----------

        [HttpPost]
        public ActionResult Link(string code, long telegramUserId, long chatId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    var account = TelegramLinkService.Redeem(db, code, telegramUserId, chatId, DateTime.Now);
                    db.SaveChanges();
                    return AccountInfo(db, account);
                }
            });
        }

        [HttpPost]
        public ActionResult Unlink(long telegramUserId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    TelegramLinkService.Unlink(db, telegramUserId);
                    db.SaveChanges();
                    return new { ok = true };
                }
            });
        }

        /// <summary>Bot o'z bazasini yo'qotsa — bog'lanishni ERP'dan tiklash.</summary>
        [HttpGet]
        public ActionResult Account(long telegramUserId)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    var a = db.TelegramAccounts.FirstOrDefault(x => x.TelegramUserId == telegramUserId && x.IsActive);
                    return a == null ? null : AccountInfo(db, a);
                }
            });
        }

        private static object AccountInfo(DistributionDbContext db, TelegramAccount a)
        {
            string name = a.UserID.HasValue
                ? db.Users.Where(u => u.ID == a.UserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault()
                : db.Customers.Where(c => c.ID == a.CustomerID).Select(c => c.Name).FirstOrDefault();
            return new
            {
                a.UserID, a.CustomerID, a.Role, Name = name,
                Language = a.UserID.HasValue ? DstLocalization.LanguageOf(a.UserID.Value) : DstLocalization.Uzbek
            };
        }

        // ---------- agent ----------

        [HttpGet]
        public ActionResult Me()
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var agent = db.SalesAgents.Find(userId);
                    var day = FieldService.Summary(db, userId, DateTime.Now);
                    return new
                    {
                        userId, name = db.Users.Where(u => u.ID == userId).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                        isAgent = agent != null && agent.IsActive && !agent.IsExpeditor, isExpeditor = agent != null && agent.IsActive && agent.IsExpeditor,
                        canOverrideCredit = DstPermissions.Has(UserName, DstPermissions.OverrideCredit),
                        visitRadiusM = DstSettings.Int(db, DstSetting.VisitRadiusM) ?? FieldService.DefaultRadiusM,
                        day
                    };
                }
            });
        }

        [HttpPost]
        public ActionResult DayStart(decimal? lat, decimal? lng, string selfieKey)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var day = FieldService.StartDay(db, userId, lat, lng, selfieKey, DateTime.Now);
                    db.SaveChanges();
                    return new { day.ID, day.StartAt };
                }
            });
        }

        [HttpPost]
        public ActionResult DayEnd(decimal? lat, decimal? lng)
        {
            return Run(userId =>
            {
                using (var db = Db())
                    return FieldService.EndDay(db, userId, lat, lng, DateTime.Now);
            });
        }

        [HttpGet]
        public ActionResult Route()
        {
            return Run(userId =>
            {
                using (var db = Db())
                    return FieldService.Route(db, userId, DateTime.Now);
            });
        }

        [HttpGet]
        public ActionResult Customers(string q)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var scopes = new ScopeService(db);
                    var query = from p in db.CustomerProfiles
                                join c in db.Customers on p.CustomerID equals c.ID
                                where c.IsDeleted != true && (q == null || c.Name.Contains(q) || p.INN == q || c.Telephone.Contains(q))
                                select new { c.ID, c.Name, c.Address, p.AgentUserID, p.TerritoryID, p.IsApproved };
                    query = scopes.For(UserName, ScopeTypes.Customer).Apply(query, x => x.ID);
                    query = scopes.For(UserName, ScopeTypes.Territory).Apply(query, x => x.TerritoryID);
                    query = scopes.AgentsFor(UserName).Apply(query, x => x.AgentUserID);
                    return query.OrderBy(x => x.Name).Take(30).ToList()
                        .Select(x => new { id = x.ID, name = x.Name, address = x.Address, approved = x.IsApproved }).ToList();
                }
            });
        }

        [HttpGet]
        public ActionResult Customer(int id)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    EnsureCustomer(db, id);
                    return FieldService.Card(db, id, userId, DateTime.Now);
                }
            });
        }

        [HttpPost]
        public ActionResult NewCustomer(CustomerInput input)
        {
            return Run(userId =>
            {
                input.ID = null;
                input.AgentUserID = input.AgentUserID ?? userId;
                using (var uow = Uow())
                {
                    var id = CustomerService.Save(uow, input, userId, AuditActor.Bot, DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        [HttpGet]
        public ActionResult Categories()
        {
            return Run(userId =>
            {
                using (var db = Db())
                    return db.Categories.Where(c => c.IsDeleted != true && c.IsActive).OrderBy(c => c.Name)
                        .Select(c => new { id = c.ID, name = c.Name }).ToList();
            });
        }

        [HttpGet]
        public ActionResult Catalog(string q, int? customerId, int? categoryId)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    if (customerId.HasValue) EnsureCustomer(db, customerId.Value);
                    var warehouse = db.SalesAgents.Where(a => a.UserID == userId).Select(a => a.WarehouseID).FirstOrDefault();
                    return CatalogService.Search(db, q, customerId, warehouse, categoryId, DateTime.Now, 50);
                }
            });
        }

        [HttpGet]
        public ActionResult Reasons(string type)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    var lang = DstLocalization.LanguageOf(userId);
                    return db.Reasons.Where(r => r.Type == type && r.IsActive).OrderBy(r => r.SortOrder).ToList()
                        .Select(r => new { id = r.ID, name = lang == DstLocalization.Russian && !string.IsNullOrEmpty(r.NameRu) ? r.NameRu : r.Name }).ToList();
                }
            });
        }

        public sealed class ApiOrderInput
        {
            public int CustomerID { get; set; }
            public DateTime? DeliveryDate { get; set; }
            public string Comment { get; set; }
            public decimal DiscountPct { get; set; }
            public Guid? ClientRequestId { get; set; }
            public int? VisitId { get; set; }
            public string SignatureKey { get; set; }
            public List<OrderLineInput> Lines { get; set; } = new List<OrderLineInput>();
        }

        /// <summary>Agent buyurtmasi (yangi holatda — ofis tasdiqlaydi). ClientRequestId bilan takror yuborish xavfsiz.</summary>
        [HttpPost]
        public ActionResult Order(ApiOrderInput input)
        {
            return Run(userId =>
            {
                using (var check = Db()) EnsureCustomer(check, input.CustomerID);
                using (var uow = Uow())
                {
                    var actor = OrderActor.ForUser(uow.Dst, UserName, AuditActor.Bot);
                    var warehouse = uow.Dst.SalesAgents.Where(a => a.UserID == userId).Select(a => a.WarehouseID).FirstOrDefault();
                    var id = OrderService.SaveDraft(uow, new OrderInput
                    {
                        CustomerID = input.CustomerID, AgentUserID = userId, WarehouseID = warehouse, DeliveryDate = input.DeliveryDate,
                        Comment = input.Comment, DiscountPct = input.DiscountPct, ClientRequestId = input.ClientRequestId, Lines = input.Lines
                    }, actor, DateTime.Now);
                    var order = uow.Dst.Orders.Find(id);
                    if (!string.IsNullOrWhiteSpace(input.SignatureKey)) order.SignatureKey = input.SignatureKey;
                    if (input.VisitId.HasValue)
                        FieldService.SetResult(uow.Dst, input.VisitId.Value, userId, null, null, id, null, null, null, DateTime.Now);
                    uow.Dst.SaveChanges();
                    uow.Commit();
                    return new { id, order.DocNo, order.Total };
                }
            });
        }

        [HttpGet]
        public ActionResult Orders(int? customerId)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    if (customerId.HasValue) EnsureCustomer(db, customerId.Value);
                    var q = db.Orders.Where(o => o.AgentUserID == userId);
                    if (customerId.HasValue) q = q.Where(o => o.CustomerID == customerId);
                    return (from o in q
                            join c in db.Customers on o.CustomerID equals c.ID
                            orderby o.ID descending
                            select new { o.ID, o.DocNo, o.DocDate, o.Total, o.OrderStatus, Customer = c.Name }).Take(20).ToList()
                        .Select(o => new { o.ID, o.DocNo, date = o.DocDate.ToString("dd.MM.yyyy"), o.Total, status = ((OrderStatus)o.OrderStatus).ToString(), o.Customer })
                        .ToList();
                }
            });
        }

        public sealed class ApiPaymentInput
        {
            public int CustomerID { get; set; }
            public int? OrderID { get; set; }
            public decimal Amount { get; set; }
            public string Method { get; set; }
            public string Note { get; set; }
            public Guid? ClientRequestId { get; set; }
            public int? VisitId { get; set; }
        }

        /// <summary>Agent to'lov oldi: naqd — agent qo'lida; boshqa usul — sozlamadagi hisobga (AgentCashAccountId).</summary>
        [HttpPost]
        public ActionResult Payment(ApiPaymentInput input)
        {
            return Run(userId =>
            {
                using (var check = Db()) EnsureCustomer(check, input.CustomerID);
                using (var uow = Uow())
                {
                    int? account = input.Method == PaymentMethods.Cash ? null : DstSettings.Int(uow.Dst, DstSetting.AgentCashAccountId);
                    var id = PaymentService.Receive(uow, new PaymentInput
                    {
                        CustomerID = input.CustomerID, OrderID = input.OrderID, Amount = input.Amount, Method = input.Method ?? PaymentMethods.Cash,
                        FinAccountID = account, ReceivedByUserID = userId, Note = input.Note, ClientRequestId = input.ClientRequestId
                    }, userId, AuditActor.Bot, DateTime.Now);
                    if (input.VisitId.HasValue)
                        FieldService.SetResult(uow.Dst, input.VisitId.Value, userId, null, null, null, id, null, null, DateTime.Now);
                    uow.Dst.SaveChanges();
                    uow.Commit();
                    var p = uow.Dst.Payments.Find(id);
                    return new { id, p.DocNo, p.Amount, onHand = PaymentService.OnHand(uow.Dst, userId).Sum(x => x.Amount) };
                }
            });
        }

        [HttpPost]
        public ActionResult CheckIn(int customerId, decimal lat, decimal lng, int? accuracyM, bool live = false)
        {
            return Run(userId =>
            {
                using (var check = Db()) EnsureCustomer(check, customerId);
                using (var db = Db())
                    return FieldService.CheckIn(db, userId, customerId, lat, lng, accuracyM, live, DateTime.Now);
            });
        }

        [HttpPost]
        public ActionResult VisitResult(int visitId, string result, int? reasonId, string photoKey, string note)
        {
            return Run(userId =>
            {
                using (var db = Db())
                {
                    FieldService.SetResult(db, visitId, userId, result, reasonId, null, null, photoKey, note, DateTime.Now);
                    db.SaveChanges();
                    return new { ok = true };
                }
            });
        }

        [HttpPost]
        public ActionResult HandoverSubmit()
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var account = DstSettings.Int(uow.Dst, DstSetting.AgentCashAccountId)
                                  ?? throw new DstValidationException(DstLocalization.T("Payment_AccountRequired"));
                    var id = PaymentService.SubmitHandover(uow, userId, account, userId, null, DateTime.Now);
                    var h = uow.Dst.CashHandovers.Find(id);
                    NotificationService.ToManagers(uow.Dst, NotificationService.HandoverPending,
                        l => DstLocalization.TFor(l, "Notify_HandoverPending", uow.Dst.Users.Where(u => u.ID == userId).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                            h.AmountDeclared.ToString("N0")), null, DateTime.Now);
                    uow.Dst.SaveChanges();
                    uow.Commit();
                    return new { id, h.DocNo, h.AmountDeclared };
                }
            });
        }

        /// <summary>Rahbar botdagi tugma orqali limitdan oshgan buyurtmani tasdiqlaydi (DstOverrideCredit huquqi bilan).</summary>
        [HttpPost]
        public ActionResult ConfirmOverride(int orderId, string note)
        {
            return Run(userId =>
            {
                using (var uow = Uow())
                {
                    var actor = OrderActor.ForUser(uow.Dst, UserName, AuditActor.Bot);
                    if (!actor.CanOverrideCredit) throw new DstValidationException(DstLocalization.T("Forbidden"));
                    OrderService.Confirm(uow, orderId, actor, string.IsNullOrWhiteSpace(note) ? "Telegram" : note, DateTime.Now);
                    uow.Commit();
                    var o = uow.Dst.Orders.Find(orderId);
                    return new { o.ID, o.DocNo, status = ((OrderStatus)o.OrderStatus).ToString() };
                }
            });
        }

        // ---------- bildirishnomalar (tizim) ----------

        [HttpGet]
        public ActionResult Notifications(int max = 50)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                    return NotificationService.Pending(db, Math.Max(1, Math.Min(max, 200)), DateTime.Now);
            });
        }

        [HttpPost]
        public ActionResult NotificationsAck(long[] sent, long[] failed)
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                {
                    NotificationService.Ack(db, sent, failed, DateTime.Now);
                    db.SaveChanges();
                    return new { ok = true };
                }
            });
        }

        /// <summary>Kunlik yakun rahbarlarga (bot jadval bo'yicha chaqiradi): har bog'langan rahbar uchun matn.</summary>
        [HttpGet]
        public ActionResult DailySummary()
        {
            return RunSystem(() =>
            {
                using (var db = Db())
                    return DailySummaryService.ForManagers(db, DateTime.Now);
            });
        }
    }
}
