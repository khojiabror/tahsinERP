using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>
    /// Rahbar paneli — kunlik holat bir ekranda: tushum va yalpi foyda (kun / oy boshidan), buyurtmalar oqimi, mijozlar qarzi
    /// va muddati o'tgani, kassalardagi va agentlar qo'lidagi pul, qoldiqning pul qiymati, eng yaxshi agentlar.
    /// Hamma ko'rsatkich mavjud manbalardan: jo'natish/qaytarish hujjatlari, CUSTOMER_LEDGER + <see cref="DebtMath"/>,
    /// FIN_TRANSACTIONS, PRODUCT_STOCKS + kirim qatlamlari.
    /// </summary>
    public static class DashboardService
    {
        public sealed class Model
        {
            public DateTime Date { get; set; }
            public SalesTotals Day { get; set; }
            public SalesTotals Month { get; set; }
            public List<(DateTime Date, decimal Net, decimal Profit)> Trend { get; set; }

            public int OrdersToday { get; set; }
            public decimal OrdersTodayTotal { get; set; }
            public int AwaitingConfirm { get; set; }
            public decimal AwaitingConfirmTotal { get; set; }
            public int AwaitingShipment { get; set; }
            public decimal AwaitingShipmentTotal { get; set; }

            public decimal CollectedToday { get; set; }
            public decimal CollectedMonth { get; set; }

            public AgingBuckets Debt { get; set; }
            public int DebtorCount { get; set; }
            public int OverdueCount { get; set; }
            public List<(int CustomerID, string Customer, decimal Overdue, int Days)> TopOverdue { get; set; }

            public List<CashReportService.AccountBalance> Accounts { get; set; }
            public decimal AgentsOnHand { get; set; }
            public int AgentsOnHandCount { get; set; }
            public decimal PendingHandovers { get; set; }

            /// <summary>null — tannarxni ko'rish huquqi yo'q.</summary>
            public List<StockValuationService.WarehouseValue> Stock { get; set; }

            public List<SalesRow> TopAgents { get; set; }
            public int AgentsActive { get; set; }
            public int AgentsTotal { get; set; }
            public int VisitsToday { get; set; }
        }

        public static Model Build(DistributionUnitOfWork uow, DateTime date, ReportScope scope, bool withCost, bool withCash)
        {
            var db = uow.Dst;
            scope = scope ?? ReportScope.Unrestricted;
            var day = date.Date;
            var monthStart = new DateTime(day.Year, day.Month, 1);
            var next = day.AddDays(1);
            var m = new Model { Date = day };

            // savdo
            var dayFilter = new ReportFilter { From = day, To = day };
            var monthFilter = new ReportFilter { From = monthStart, To = day };
            m.Day = SalesReportService.Totals(SalesReportService.Facts(db, dayFilter, scope));
            m.Month = SalesReportService.Totals(SalesReportService.Facts(db, monthFilter, scope));
            m.Trend = SalesReportService.Daily(SalesReportService.Facts(db, new ReportFilter { From = day.AddDays(-13), To = day }, scope));
            m.TopAgents = SalesReportService.Order(
                    SalesReportService.ByDimension(db, SalesReportService.Facts(db, monthFilter, scope), SalesDimension.Agent), "net", true)
                .Take(5).ToList();

            // buyurtmalar oqimi
            IQueryable<DstOrder> orders = db.Orders;
            orders = scope.Agents.Apply(orders, o => o.AgentUserID);
            orders = scope.Customers.Apply(orders, o => o.CustomerID);
            orders = scope.Warehouses.Apply(orders, o => o.WarehouseID);
            var today = orders.Where(o => o.CreatedAt >= day && o.CreatedAt < next && o.Status != DocumentStatus.Cancelled)
                .GroupBy(o => 1).Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Total) }).FirstOrDefault();
            m.OrdersToday = today?.Count ?? 0;
            m.OrdersTodayTotal = today?.Total ?? 0;
            byte sNew = (byte)OrderStatus.New, sConfirmed = (byte)OrderStatus.Confirmed, sPicking = (byte)OrderStatus.Picking,
                sPartial = (byte)OrderStatus.PartiallyShipped;
            var pipeline = orders.Where(o => o.Status != DocumentStatus.Cancelled &&
                                             (o.OrderStatus == sNew || o.OrderStatus == sConfirmed || o.OrderStatus == sPicking || o.OrderStatus == sPartial))
                .GroupBy(o => o.OrderStatus == sNew)
                .Select(g => new { IsNew = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total - o.ShippedTotal) }).ToList();
            var n = pipeline.FirstOrDefault(p => p.IsNew);
            var s = pipeline.FirstOrDefault(p => !p.IsNew);
            m.AwaitingConfirm = n?.Count ?? 0;
            m.AwaitingConfirmTotal = n?.Total ?? 0;
            m.AwaitingShipment = s?.Count ?? 0;
            m.AwaitingShipmentTotal = s?.Total ?? 0;

            // to'lovlar (qaysi kassaga tushganidan qat'i nazar — mijozdan olingan pul)
            IQueryable<DstPayment> payments = db.Payments.Where(p => p.Status == DocumentStatus.Posted && p.DocDate >= monthStart && p.DocDate <= day);
            payments = scope.Customers.Apply(payments, p => p.CustomerID);
            if (!scope.Agents.IsUnrestricted || !scope.Territories.IsUnrestricted)
            {
                var visible = scope.VisibleCustomers(db);
                payments = payments.Where(p => visible.Contains(p.CustomerID));
            }
            var collected = payments.GroupBy(p => p.DocDate == day)
                .Select(g => new { Today = g.Key, Amount = g.Sum(p => p.Amount) }).ToList();
            m.CollectedToday = collected.Where(c => c.Today).Sum(c => c.Amount);
            m.CollectedMonth = collected.Sum(c => c.Amount);

            // qarz: CUSTOMER_LEDGER → DebtService.Net → DebtMath.Aging (qarzlar sahifasi bilan bir xil)
            var debts = scope.IsUnrestricted ? DebtService.Load(db) : DebtService.Load(db, scope.VisibleCustomers(db).ToList());
            var debt = new AgingBuckets();
            var overdue = new List<(int CustomerID, decimal Overdue, int Days)>();
            foreach (var pair in debts)
            {
                var a = DebtMath.Aging(pair.Value.Debits, pair.Value.Credits, day);
                debt.NotDue += a.NotDue;
                debt.Days1To7 += a.Days1To7;
                debt.Days8To30 += a.Days8To30;
                debt.Days31To60 += a.Days31To60;
                debt.Over60 += a.Over60;
                debt.Advance += a.Advance;
                debt.MaxOverdueDays = Math.Max(debt.MaxOverdueDays, a.MaxOverdueDays);
                if (a.Total > 0) m.DebtorCount++;
                if (a.Overdue > 0)
                {
                    m.OverdueCount++;
                    overdue.Add((pair.Key, a.Overdue, a.MaxOverdueDays));
                }
            }
            m.Debt = debt;
            var top = overdue.OrderByDescending(o => o.Overdue).Take(5).ToList();
            var topIds = top.Select(t => t.CustomerID).ToList();
            var customerNames = db.Customers.Where(c => topIds.Contains(c.ID)).ToDictionary(c => c.ID, c => c.Name);
            m.TopOverdue = top.Select(t => (t.CustomerID, customerNames.TryGetValue(t.CustomerID, out var cn) ? cn : "#" + t.CustomerID, t.Overdue, t.Days)).ToList();

            // pul
            if (withCash) m.Accounts = CashReportService.Accounts(uow.Erp, day);
            var onHand = CashReportService.OnHandQuery(db, scope).GroupBy(p => 1)
                .Select(g => new { Amount = g.Sum(p => p.Amount), Agents = g.Select(p => p.ReceivedByUserID).Distinct().Count() }).FirstOrDefault();
            m.AgentsOnHand = onHand?.Amount ?? 0;
            m.AgentsOnHandCount = onHand?.Agents ?? 0;
            var pending = scope.Agents.Apply(db.CashHandovers.Where(h => h.Status == DocumentStatus.Draft), h => h.AgentUserID);
            m.PendingHandovers = pending.Sum(h => (decimal?)h.AmountDeclared) ?? 0;

            // qoldiq qiymati (tannarx — faqat DstSeeCost bilan)
            if (withCost)
            {
                var warehouses = StockService.SalesWarehouses(db).Where(id => scope.Warehouses.Allows(id)).ToList();
                m.Stock = StockValuationService.ByWarehouse(db, warehouses);
            }

            // agentlar faolligi
            var agents = scope.Agents.Apply(db.SalesAgents.Where(a => a.IsActive && !a.IsExpeditor), a => a.UserID);
            m.AgentsTotal = agents.Count();
            m.AgentsActive = agents.Count(a => db.AgentDays.Any(d => d.UserID == a.UserID && d.WorkDate == day));
            m.VisitsToday = scope.Agents.Apply(db.Visits.Where(v => v.VisitDate == day), v => v.UserID).Count();
            return m;
        }
    }
}
