using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// FinanceReportsHub va LiabilitiesDashboard ma'lumotlari.
    /// LiabilitiesDashboard: AR/AP oy oxirlari bo'yicha BIR MARTA yuklangan ma'lumotdan kesiladi (FIN-P01, avval ~28 marta),
    /// payroll oylik trendi va hisob qoldiqlari bittadan GROUP BY bilan (FIN-P02, P10).
    /// </summary>
    public sealed class LiabilityReportsService
    {
        private readonly DBTHSNEntities _db;
        private readonly CounterpartyBalanceService _counterparties;
        private readonly BalanceSheetService _balanceSheet;

        public LiabilityReportsService(DBTHSNEntities db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _counterparties = new CounterpartyBalanceService(db);
            _balanceSheet = new BalanceSheetService(db, _counterparties);
        }

        #region FinanceReportsHub

        public FinanceReportsHubViewModel ReportsHub(DateTime asOf)
        {
            var db = _db;
            var model = new FinanceReportsHubViewModel { AsOfDate = asOf };

            // 1) Xaridorlardan haqqimiz (debitorlik)
            var customerSummary = _counterparties.Customers(asOf);
            var customerIds = customerSummary.Select(x => x.CustomerID).Distinct().ToList();
            var customerNames = customerIds.Count == 0
                ? new Dictionary<int, string>()
                : db.CUSTOMERS.Where(c => customerIds.Contains(c.ID)).Select(c => new { c.ID, c.Name }).ToList()
                    .ToDictionary(c => c.ID, c => c.Name ?? "Noma'lum");

            model.CustomerReceivables = customerSummary
                .Where(x => x.Outstanding > 0)
                .OrderByDescending(x => x.Outstanding)
                .Take(20)
                .Select(x => new FinanceCustomerReceivableItem
                {
                    CustomerID = x.CustomerID,
                    CustomerName = customerNames.ContainsKey(x.CustomerID) ? customerNames[x.CustomerID] : $"Customer #{x.CustomerID}",
                    Amount = x.Outstanding
                })
                .ToList();
            model.TotalCustomerReceivables = customerSummary.Sum(x => x.Outstanding);

            // 2) Ta'minotchilardan qarzimiz (kreditorlik)
            var supplierSummary = _counterparties.Suppliers(asOf);
            var supplierIds = supplierSummary.Select(x => x.SupplierID).Distinct().ToList();
            var supplierNames = supplierIds.Count == 0
                ? new Dictionary<int, string>()
                : db.SUPPLIERS.Where(s => supplierIds.Contains(s.ID)).Select(s => new { s.ID, s.Name }).ToList()
                    .ToDictionary(s => s.ID, s => s.Name ?? "Noma'lum");

            model.SupplierPayables = supplierSummary
                .Where(x => x.Outstanding > 0)
                .OrderByDescending(x => x.Outstanding)
                .Take(20)
                .Select(x => new FinanceSupplierPayableItem
                {
                    SupplierID = x.SupplierID,
                    SupplierName = supplierNames.ContainsKey(x.SupplierID) ? supplierNames[x.SupplierID] : $"Supplier #{x.SupplierID}",
                    Amount = x.Outstanding
                })
                .ToList();
            model.TotalSupplierPayables = supplierSummary.Sum(x => x.Outstanding);

            // 3) Oylik qarzi xodim kesimida
            FillPayrollDebts(model, asOf);

            // 4) Oylik qarzi trendi: oxirgi 12 oy, har oy oxiridagi yig'ma (hisoblangan − to'langan).
            var salaryCategoryIds = FinQueries.SalaryCategoryIds(db);
            var accrualByDate = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOf)
                .GroupBy(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value)
                .Select(g => new { Date = g.Key, Amount = g.Sum(l => l.NetSalary) })
                .ToList();
            var paidByDate = FinQueries.Active(db)
                .Where(t => t.Direction == 2 &&
                            t.TxnDate <= asOf &&
                            (t.CounterpartyType == 6 ||
                             (salaryCategoryIds.Count > 0 && salaryCategoryIds.Contains(t.CategoryID))))
                .GroupBy(t => t.TxnDate)
                .Select(g => new { Date = g.Key, Amount = g.Sum(t => t.Amount) })
                .ToList();

            var monthCursor = new DateTime(asOf.Year, asOf.Month, 1).AddMonths(-11);
            while (monthCursor <= new DateTime(asOf.Year, asOf.Month, 1))
            {
                var monthEnd = monthCursor.AddMonths(1).AddDays(-1);
                if (monthEnd > asOf) monthEnd = asOf;

                var accrued = accrualByDate.Where(x => x.Date <= monthEnd).Sum(x => x.Amount ?? 0m);
                var paid = paidByDate.Where(x => x.Date <= monthEnd).Sum(x => x.Amount);
                var debt = accrued - paid;
                if (debt < 0) debt = 0m;

                model.MonthlyPayrollDebtTrend.Add(new FinanceMonthlyPoint
                {
                    Label = $"{monthCursor.Year}-{monthCursor.Month:D2}",
                    Value = debt
                });
                monthCursor = monthCursor.AddMonths(1);
            }

            // 5) Doimiy harajatlardan to'lanmaganlari
            var obligations = FinLiabilities.ActiveRecurringObligations(db);
            var paidByObligation = FinLiabilities.RecurringObligationPayments(db, obligations.Select(o => o.ID).ToList(), asOf, null);
            var obligationDebts = new List<FinanceRecurringObligationDebtItem>();
            foreach (var ob in obligations)
            {
                var startDate = new DateTime(ob.StartYear, ob.StartMonth, 1);
                if (asOf < startDate) continue;

                var monthsCount = FinLiabilities.AccruedMonths(startDate, asOf);
                if (monthsCount < 1) continue;

                var accrued = ob.MonthlyAmount * monthsCount;
                var paid = paidByObligation.TryGetValue(ob.ID, out var p) ? p : 0m;
                var debt = accrued - paid;
                if (debt <= 0) continue;

                obligationDebts.Add(new FinanceRecurringObligationDebtItem
                {
                    ObligationID = ob.ID,
                    Name = ob.Name ?? "Noma'lum",
                    MonthlyAmount = ob.MonthlyAmount,
                    AccruedMonths = monthsCount,
                    AccruedAmount = accrued,
                    PaidAmount = paid,
                    DebtAmount = debt
                });
            }
            model.RecurringObligationDebts = obligationDebts.OrderByDescending(x => x.DebtAmount).ToList();
            model.TotalRecurringObligationDebt = model.RecurringObligationDebts.Sum(x => x.DebtAmount);

            return model;
        }

        private sealed class EmployeePayroll
        {
            public int EmployeeID { get; set; }
            public string EmployeeName { get; set; }
            public decimal Calculated { get; set; }
        }

        private void FillPayrollDebts(FinanceReportsHubViewModel model, DateTime asOf)
        {
            var db = _db;
            var payrollByEmployee = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_EMPLOYEES != null && l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOf)
                .GroupBy(l => new { l.EmployeeID, Name = l.HR_EMPLOYEES.FullName })
                .Select(g => new EmployeePayroll
                {
                    EmployeeID = g.Key.EmployeeID,
                    EmployeeName = g.Key.Name ?? "Noma'lum",
                    Calculated = g.Sum(x => (decimal?)x.NetSalary) ?? 0m
                })
                .ToList();

            var payrollPaidByEmployee = FinQueries.Active(db)
                .Where(t => t.Direction == 2 &&
                            t.TxnDate <= asOf &&
                            t.CounterpartyType == 6 &&
                            t.CounterpartyID.HasValue)
                .GroupBy(t => t.CounterpartyID.Value)
                .Select(g => new { EmployeeID = g.Key, Paid = g.Sum(x => (decimal?)x.Amount) ?? 0m })
                .ToList()
                .ToDictionary(x => x.EmployeeID, x => x.Paid);

            var openingDate = _counterparties.LatestOpeningDate(asOf, CounterpartyBalanceService.BalanceTypePayroll);
            var openingRows = openingDate.HasValue
                ? _counterparties.OpeningContractBalances(openingDate.Value, CounterpartyBalanceService.BalanceTypePayroll).Where(x => x.CounterpartyID > 0).ToList()
                : new List<OpeningContractBalance>();

            // Opening'da bor, lekin payroll'da yo'q xodimlar nomi — bitta so'rov (avval har qatorga alohida, FIN-P10).
            var missingIds = openingRows.Select(o => o.CounterpartyID).Where(id => payrollByEmployee.All(x => x.EmployeeID != id)).Distinct().ToList();
            var employeeNames = missingIds.Count == 0
                ? new Dictionary<int, string>()
                : db.HR_EMPLOYEES.Where(e => missingIds.Contains(e.ID) && e.IsDeleted != true)
                    .Select(e => new { e.ID, e.FullName })
                    .ToList()
                    .GroupBy(e => e.ID)
                    .ToDictionary(g => g.Key, g => g.First().FullName);

            foreach (var opening in openingRows)
            {
                var existing = payrollByEmployee.FirstOrDefault(x => x.EmployeeID == opening.CounterpartyID);
                if (existing == null)
                {
                    payrollByEmployee.Add(new EmployeePayroll
                    {
                        EmployeeID = opening.CounterpartyID,
                        EmployeeName = (employeeNames.TryGetValue(opening.CounterpartyID, out var n) ? n : null) ?? $"Xodim #{opening.CounterpartyID}",
                        Calculated = opening.TotalAmount
                    });
                }
                else
                {
                    existing.Calculated += opening.TotalAmount;
                }

                payrollPaidByEmployee[opening.CounterpartyID] =
                    (payrollPaidByEmployee.ContainsKey(opening.CounterpartyID) ? payrollPaidByEmployee[opening.CounterpartyID] : 0m)
                    + opening.PaidAmount;
            }

            var payrollDebts = payrollByEmployee
                .Select(x =>
                {
                    var paid = payrollPaidByEmployee.ContainsKey(x.EmployeeID) ? payrollPaidByEmployee[x.EmployeeID] : 0m;
                    return new FinancePayrollDebtItem
                    {
                        EmployeeID = x.EmployeeID,
                        EmployeeName = x.EmployeeName,
                        CalculatedAmount = x.Calculated,
                        PaidAmount = paid,
                        DebtAmount = x.Calculated - paid
                    };
                })
                .Where(x => x.DebtAmount > 0)
                .OrderByDescending(x => x.DebtAmount)
                .ToList();
            // FIN-B10: jami barcha qarzdor xodimlar bo'yicha (avval faqat ko'rsatiladigan 30 tasi qo'shilardi)
            model.TotalPayrollDebt = payrollDebts.Sum(x => x.DebtAmount);
            model.PayrollDebtsByEmployee = payrollDebts.Take(30).ToList();
        }

        #endregion

        #region LiabilitiesDashboard

        public sealed class SupplierAmount
        {
            public string SupplierName { get; set; }
            public decimal Amount { get; set; }
            public int TransactionCount { get; set; }
        }

        public sealed class CustomerAmount
        {
            public string CustomerName { get; set; }
            public decimal Amount { get; set; }
            public int TransactionCount { get; set; }
        }

        public sealed class LiabilitiesResult
        {
            public List<SupplierAmount> PayablesBySupplier { get; set; }
            public List<CustomerAmount> ReceivablesByCustomer { get; set; }
            public List<MonthlyData> MonthlyPayables { get; set; }
            public List<MonthlyData> MonthlyReceivables { get; set; }
            public List<MonthlyData> MonthlyPayrollLiability { get; set; }
            public List<AccountBalanceData> PayablesAccountBalances { get; set; }
            public List<AccountBalanceData> ReceivablesAccountBalances { get; set; }
            public List<BalanceCategoryDetailData> PassiveBalanceStats { get; set; }
            public string CurrencyShortname { get; set; }
        }

        private static List<(DateTime MonthStart, DateTime MonthEnd)> Months(DateTime fromDate, DateTime toDate)
        {
            var months = new List<(DateTime, DateTime)>();
            var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
            var endMonth = new DateTime(toDate.Year, toDate.Month, 1);
            while (cursor <= endMonth)
            {
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                if (monthEnd > toDate.Date) monthEnd = toDate.Date;
                months.Add((cursor, monthEnd));
                cursor = cursor.AddMonths(1);
            }
            return months;
        }

        public LiabilitiesResult Liabilities(DateTime fromDate, DateTime toDate)
        {
            var db = _db;
            var months = Months(fromDate, toDate);
            var result = new LiabilitiesResult();

            // Asosiy valyuta — davrdagi invoys "mirror" yozuvlari bo'yicha (avval ular faqat shu uchun to'liq yuklanardi).
            result.CurrencyShortname = FinQueries.PrimaryCurrencyShortname(db,
                FinQueries.Active(db)
                    .Where(t => t.SourceDocType == FinQueries.PurchaseInvoiceMirror || t.SourceDocType == FinQueries.SalesInvoiceMirror)
                    .InPeriod(fromDate, toDate));

            // AR/AP: oy oxirlari + toDate — ma'lumot bir marta yuklanadi.
            var dates = months.Select(m => m.MonthEnd).Concat(new[] { toDate }).ToList();
            var supplierSeries = _counterparties.SupplierSeries(dates);
            var customerSeries = _counterparties.CustomerSeries(dates);
            var supplierSummary = supplierSeries[supplierSeries.Count - 1];
            var customerSummary = customerSeries[customerSeries.Count - 1];

            result.MonthlyPayables = months
                .Select((m, i) => new MonthlyData { Period = $"{m.MonthStart.Year}-{m.MonthStart.Month:D2}", Amount = supplierSeries[i].Sum(s => s.Outstanding) })
                .ToList();
            result.MonthlyReceivables = months
                .Select((m, i) => new MonthlyData { Period = $"{m.MonthStart.Year}-{m.MonthStart.Month:D2}", Amount = customerSeries[i].Sum(c => c.Outstanding) })
                .ToList();

            var supplierIds = supplierSummary.Select(s => s.SupplierID).ToList();
            var supplierNames = db.SUPPLIERS.Where(s => supplierIds.Contains(s.ID)).Select(s => new { s.ID, s.Name }).ToList().ToDictionary(s => s.ID, s => s.Name);
            result.PayablesBySupplier = supplierSummary
                .Select(s => new SupplierAmount
                {
                    SupplierName = supplierNames.ContainsKey(s.SupplierID) ? supplierNames[s.SupplierID] : $"Supplier #{s.SupplierID}",
                    Amount = s.Outstanding,
                    TransactionCount = s.InvoiceCount + s.PaymentCount
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var customerIds = customerSummary.Select(c => c.CustomerID).ToList();
            var customerNames = db.CUSTOMERS.Where(c => customerIds.Contains(c.ID)).Select(c => new { c.ID, c.Name }).ToList().ToDictionary(c => c.ID, c => c.Name);
            result.ReceivablesByCustomer = customerSummary
                .Where(c => c.Outstanding != 0m)
                .Select(c => new CustomerAmount
                {
                    CustomerName = customerNames.ContainsKey(c.CustomerID) ? customerNames[c.CustomerID] : $"Customer #{c.CustomerID}",
                    Amount = c.Outstanding,
                    TransactionCount = c.InvoiceCount + c.ReceiptCount
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            // Oylik majburiyati oylar kesimida: oy ichida tasdiqlangan oylik − shu oydagi oylik to'lovlari (manfiy bo'lsa 0).
            var rangeStart = months.Count > 0 ? months[0].MonthStart : fromDate;
            var rangeEnd = months.Count > 0 ? months[months.Count - 1].MonthEnd : toDate.Date;
            var accrualByDate = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value >= rangeStart && l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= rangeEnd)
                .GroupBy(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value)
                .Select(g => new { Date = g.Key, Amount = g.Sum(l => l.NetSalary) })
                .ToList();
            var paidByDate = FinQueries.PayrollPayments(db, FinQueries.SalaryCategoryIds(db), rangeStart, rangeEnd)
                .GroupBy(t => t.TxnDate)
                .Select(g => new { Date = g.Key, Amount = g.Sum(t => t.Amount) })
                .ToList();
            result.MonthlyPayrollLiability = months.Select(m =>
            {
                var accrued = accrualByDate.Where(x => x.Date >= m.MonthStart && x.Date <= m.MonthEnd).Sum(x => x.Amount ?? 0m);
                var paid = paidByDate.Where(x => x.Date >= m.MonthStart && x.Date <= m.MonthEnd).Sum(x => x.Amount);
                var liability = accrued - paid;
                return new MonthlyData { Period = $"{m.MonthStart.Year}-{m.MonthStart.Month:D2}", Amount = liability < 0 ? 0m : liability };
            }).ToList();

            // Kreditor/debitor hisoblari qoldig'i (bitta GROUP BY bilan, FIN-P02).
            var payablesAccounts = db.FIN_ACCOUNTS.AsNoTracking().Include(a => a.CURRENCy)
                .Where(a => (a.Code.StartsWith("PAYABLES_") || a.Name.Contains("Kreditorlar")) && a.IsActive)
                .OrderBy(a => a.ID)
                .ToList();
            var receivablesAccounts = db.FIN_ACCOUNTS.AsNoTracking().Include(a => a.CURRENCy)
                .Where(a => (a.Code.StartsWith("RECEIVABLES_") || a.Name.Contains("Debitorlar")) && a.IsActive)
                .OrderBy(a => a.ID)
                .ToList();
            result.PayablesAccountBalances = FinAnalyticsService.PlainAccountBalances(db, payablesAccounts, fromDate, toDate);
            result.ReceivablesAccountBalances = FinAnalyticsService.PlainAccountBalances(db, receivablesAccounts, fromDate, toDate);

            // Passivlar statistikasi — yagona balans engine'idan; AP natijasi yuqoridagi hisobdan keshda olinadi.
            result.PassiveBalanceStats = _balanceSheet.LiabilityDetails(toDate);

            return result;
        }

        #endregion
    }
}
