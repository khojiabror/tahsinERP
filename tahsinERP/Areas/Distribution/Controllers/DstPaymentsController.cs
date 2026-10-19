using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Filters;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Mijoz to'lovlari (kassada yoki agent orqali), agentlar qo'lidagi naqd pul va kassaga topshirish.
    /// Moliyaga faqat haqiqiy pul yoziladi (kassaga tushganda) — <see cref="DstFinancePosting"/>.
    /// </summary>
    public class DstPaymentsController : DstControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                FillLookups(db);
                return View();
            }
        }

        [HttpGet]
        public async Task<ActionResult> IndexData(int? customerId, string method, string state, DateTime? from, DateTime? to)
        {
            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1, defaultDescending: true);
            using (var db = NewDb())
            {
                var scopes = new ScopeService(db);
                var q = from p in db.Payments
                        join c in db.Customers on p.CustomerID equals c.ID
                        join pf in db.CustomerProfiles on p.CustomerID equals pf.CustomerID
                        select new
                        {
                            p.ID, p.DocNo, p.DocDate, p.Amount, p.Method, p.Status, p.FinAccountID, p.HandoverID, p.CustomerID, p.Source,
                            p.CancelReason, Customer = c.Name, pf.AgentUserID,
                            Receiver = db.Users.Where(u => u.ID == p.ReceivedByUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                            Account = db.FinAccounts.Where(a => a.ID == p.FinAccountID).Select(a => a.Name).FirstOrDefault(),
                            Order = db.Orders.Where(o => o.ID == p.OrderID).Select(o => o.DocNo).FirstOrDefault()
                        };
                q = scopes.For(CurrentUserName, ScopeTypes.Customer).Apply(q, x => x.CustomerID);
                q = scopes.AgentsFor(CurrentUserName).Apply(q, x => x.AgentUserID);
                if (customerId.HasValue) q = q.Where(x => x.CustomerID == customerId);
                if (!string.IsNullOrEmpty(method)) q = q.Where(x => x.Method == method);
                if (state == "onhand") q = q.Where(x => x.FinAccountID == null && x.Status == DocumentStatus.Posted);
                if (state == "cancelled") q = q.Where(x => x.Status == DocumentStatus.Cancelled);
                if (from.HasValue) q = q.Where(x => x.DocDate >= from);
                if (to.HasValue) q = q.Where(x => x.DocDate <= to);

                var result = await ServerTablePage.LoadAsync(q, page,
                    (x, s) => x.Where(r => r.DocNo.Contains(s) || r.Customer.Contains(s)),
                    (x, column, desc) => column == 3
                        ? ServerTablePage.By(x, r => r.Amount, desc).ThenBy(r => r.ID)
                        : ServerTablePage.By(x, r => r.DocDate, desc).ThenBy(r => r.ID));

                return Json(new
                {
                    draw = page.Draw, recordsTotal = result.Total, recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID, docNo = r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), customer = r.Customer, amount = r.Amount,
                        method = DstLocalization.T("PayMethod_" + r.Method), receiver = r.Receiver, account = r.Account, order = r.Order,
                        cancelled = r.Status == DocumentStatus.Cancelled, reason = r.CancelReason,
                        onHand = r.FinAccountID == null && r.Status == DocumentStatus.Posted, handedOver = r.HandoverID != null, source = r.Source
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public sealed class ReceiveInput
        {
            public int CustomerID { get; set; }
            public int? OrderID { get; set; }
            public decimal Amount { get; set; }
            public string Method { get; set; }
            public int? FinAccountID { get; set; }
            public string Note { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Receive(ReceiveInput input)
        {
            return DstJson(() =>
            {
                using (var check = NewDb())
                {
                    if (!new ScopeService(check).For(CurrentUserName, ScopeTypes.Customer).Allows(input.CustomerID))
                        throw new DstValidationException(DstLocalization.T("Forbidden"));
                }
                using (var uow = NewUnitOfWork())
                {
                    var id = PaymentService.Receive(uow, new PaymentInput
                    {
                        CustomerID = input.CustomerID, OrderID = input.OrderID, Amount = input.Amount, Method = input.Method,
                        FinAccountID = input.FinAccountID, Note = input.Note
                    }, CurrentUserId, AuditActor.Web, DateTime.Now);
                    uow.Commit();
                    return new { id };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult Cancel(int id, string reason)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    PaymentService.Cancel(uow, id, CurrentUserId, reason, DateTime.Now);
                    uow.Commit();
                    return null;
                }
            });
        }

        // ---------- agentlar qo'lidagi naqd pul va kassaga topshirish ----------

        public sealed class AgentCashRow
        {
            public int AgentUserID { get; set; }
            public string Agent { get; set; }
            public int Count { get; set; }
            public decimal Amount { get; set; }
            public DateTime Oldest { get; set; }
            public int? PendingHandoverID { get; set; }
        }

        public ActionResult Cash()
        {
            using (var db = NewDb())
            {
                var rows = (from p in db.Payments
                            where p.Status == DocumentStatus.Posted && p.FinAccountID == null
                            group p by p.ReceivedByUserID into g
                            select new AgentCashRow
                            {
                                AgentUserID = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount), Oldest = g.Min(x => x.DocDate),
                                Agent = db.Users.Where(u => u.ID == g.Key).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                PendingHandoverID = db.CashHandovers.Where(h => h.AgentUserID == g.Key && h.Status == DocumentStatus.Draft)
                                    .Select(h => (int?)h.ID).FirstOrDefault()
                            }).OrderByDescending(x => x.Amount).ToList();
                var agentScope = new ScopeService(db).AgentsFor(CurrentUserName);
                ViewBag.Handovers = (from h in db.CashHandovers
                                     orderby h.ID descending
                                     select new
                                     {
                                         h.ID, h.DocNo, h.DocDate, h.Status, h.AmountDeclared, h.AmountReceived, h.AgentUserID,
                                         Agent = db.Users.Where(u => u.ID == h.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                                         Account = db.FinAccounts.Where(a => a.ID == h.FinAccountID).Select(a => a.Name).FirstOrDefault()
                                     }).Take(100).ToList()
                    .Where(h => agentScope.Allows(h.AgentUserID))
                    .Select(h => new HandoverRow
                    {
                        ID = h.ID, DocNo = h.DocNo, Date = h.DocDate, Status = h.Status, Declared = h.AmountDeclared, Received = h.AmountReceived,
                        Agent = h.Agent, Account = h.Account
                    }).ToList();
                FillLookups(db);
                return View(rows.Where(r => agentScope.Allows(r.AgentUserID)).ToList());
            }
        }

        public sealed class HandoverRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime Date { get; set; }
            public DocumentStatus Status { get; set; }
            public decimal Declared { get; set; }
            public decimal? Received { get; set; }
            public string Agent { get; set; }
            public string Account { get; set; }
        }

        /// <summary>Agent kassaga keldi: qo'lidagi naqd to'lovlar sanaladi va qabul qilinadi (farq moliyaga yoziladi).</summary>
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult AcceptCash(int agentUserId, int finAccountId, decimal amountReceived, string note)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    var pending = uow.Dst.CashHandovers.Where(h => h.AgentUserID == agentUserId && h.Status == DocumentStatus.Draft)
                        .Select(h => (int?)h.ID).FirstOrDefault();
                    if (pending.HasValue)
                        PaymentService.ConfirmHandover(uow, pending.Value, amountReceived, CurrentUserId, DateTime.Now);
                    else
                        PaymentService.AcceptCash(uow, agentUserId, finAccountId, amountReceived, CurrentUserId, note, DateTime.Now);
                    uow.Commit();
                    return null;
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult RejectHandover(int id, string reason)
        {
            return DstJson(() =>
            {
                using (var uow = NewUnitOfWork())
                {
                    PaymentService.RejectHandover(uow, id, CurrentUserId, reason, DateTime.Now);
                    uow.Commit();
                    return null;
                }
            });
        }

        [HttpGet]
        public JsonResult OnHand(int agentUserId)
        {
            using (var db = NewDb())
            {
                var rows = (from p in db.Payments
                            where p.ReceivedByUserID == agentUserId && p.Status == DocumentStatus.Posted && p.FinAccountID == null
                            join c in db.Customers on p.CustomerID equals c.ID
                            orderby p.ID
                            select new { p.DocNo, p.DocDate, p.Amount, Customer = c.Name }).ToList();
                return Json(rows.Select(r => new { r.DocNo, date = r.DocDate.ToString("dd.MM.yyyy"), r.Amount, r.Customer }), JsonRequestBehavior.AllowGet);
            }
        }

        private void FillLookups(DistributionDbContext db) => FillPaymentLookups(ViewBag, db);

        /// <summary>_PaymentModal uchun ro'yxatlar (to'lovlar, buyurtma kartasi, qarzlar sahifalari).</summary>
        internal static void FillPaymentLookups(dynamic viewBag, DistributionDbContext db)
        {
            viewBag.Accounts = db.FinAccounts.Where(a => a.IsActive && a.IsDeleted != true && (a.IsCash || a.IsBank)).OrderBy(a => a.Name).ToList()
                .Select(a => new SelectListItem { Value = a.ID.ToString(), Text = a.Name + (a.IsCash ? "" : " (bank)") }).ToList();
            viewBag.Methods = PaymentMethods.All.Select(m => new SelectListItem { Value = m, Text = DstLocalization.T("PayMethod_" + m) }).ToList();
        }
    }
}
