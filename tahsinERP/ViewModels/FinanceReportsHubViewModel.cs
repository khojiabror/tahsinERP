using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    public class FinanceReportsHubViewModel
    {
        public DateTime AsOfDate { get; set; }
        public decimal TotalCustomerReceivables { get; set; }
        public decimal TotalSupplierPayables { get; set; }
        public decimal TotalPayrollDebt { get; set; }
        public decimal TotalRecurringObligationDebt { get; set; }

        public List<FinanceCustomerReceivableItem> CustomerReceivables { get; set; } = new List<FinanceCustomerReceivableItem>();
        public List<FinanceSupplierPayableItem> SupplierPayables { get; set; } = new List<FinanceSupplierPayableItem>();
        public List<FinancePayrollDebtItem> PayrollDebtsByEmployee { get; set; } = new List<FinancePayrollDebtItem>();
        public List<FinanceRecurringObligationDebtItem> RecurringObligationDebts { get; set; } = new List<FinanceRecurringObligationDebtItem>();
        public List<FinanceMonthlyPoint> MonthlyPayrollDebtTrend { get; set; } = new List<FinanceMonthlyPoint>();
    }

    public class FinanceCustomerReceivableItem
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
    }

    public class FinanceSupplierPayableItem
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
        public decimal Amount { get; set; }
    }

    public class FinancePayrollDebtItem
    {
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DebtAmount { get; set; }
    }

    public class FinanceRecurringObligationDebtItem
    {
        public int ObligationID { get; set; }
        public string Name { get; set; }
        public decimal MonthlyAmount { get; set; }
        public int AccruedMonths { get; set; }
        public decimal AccruedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DebtAmount { get; set; }
    }

    public class FinanceMonthlyPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}
