using System;
using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.Finance
{
    // FinDashboard hisobotlari uchun DTO'lar (avval FinDashboardController.cs oxirida edi).

    public class AccountBalanceData
    {
        public int AccountID { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string OwnerName { get; set; }
        public string CurrencyShortname { get; set; }
        public decimal Kirim { get; set; } // Period kirim (vaqt oralig'idagi)
        public decimal Chiqim { get; set; } // Period chiqim (vaqt oralig'idagi)
        public decimal Qoldiq { get; set; } // Cumulative balance (barcha vaqt uchun, toDate gacha)

        /// <summary>
        /// true bo'lsa, qoldiq opening balans + opening sanasidan keyingi tranzaksiyalar bo'yicha hisoblangan.
        /// </summary>
        public bool IncludesOpeningBalance { get; set; }
    }

    public class AccountUsageData
    {
        public int AccountID { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public decimal TotalInflow { get; set; }
        public decimal TotalOutflow { get; set; }
        public int TransactionCount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class ExpenseByCategoryData
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ExpenseByAccountData
    {
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

    public class IncomeByCategoryData
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class IncomeByAccountData
    {
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

    public class MonthlyData
    {
        public string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class PLItemData
    {
        public FIN_PL_ITEMS Item { get; set; }
        public decimal Actual { get; set; }
        public decimal Plan { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercent { get; set; }
        public List<PLItemData> Children { get; set; }
    }

    public class BalanceCategoryData
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public decimal Amount { get; set; }

        /// <summary>
        /// true = "Aktivlar" ustuni (chap); false = "Passivlar va Kapital" (o'ng).
        /// </summary>
        public bool IsAssetColumn { get; set; }
    }

    public class BalanceCategoryDetailData
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public decimal Amount { get; set; }
        public string CalculationTypeName { get; set; }
        public int TransactionCount { get; set; }
        public decimal AddedAmount { get; set; }
        public decimal SubtractedAmount { get; set; }
        public string Explanation { get; set; }

        /// <summary>true = balansning "Aktivlar" ustuni (BalanceCategoryDisplayHelper.IsAssetColumn).</summary>
        public bool IsAssetColumn { get; set; }
    }

    public class ShareholderValueData
    {
        public SHAREHOLDER Shareholder { get; set; }
        public decimal SharePercent { get; set; }
        public decimal Value { get; set; }
    }

    /// <summary>EBITDA hisoblash natijasi (foiz/soliq/amortizatsiya add-back bilan).</summary>
    public class EbitdaData
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal OperatingIncome { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal TaxExpense { get; set; }
        public decimal InterestExpense { get; set; }
        public decimal DepreciationExpense { get; set; }

        /// <summary>Operatsion foyda = daromad - barcha xarajatlar.</summary>
        public decimal OperatingProfit { get { return OperatingIncome - OperatingExpenses; } }

        /// <summary>EBITDA = operatsion foyda + foiz + soliq + amortizatsiya.</summary>
        public decimal Ebitda { get { return OperatingProfit + TaxExpense + InterestExpense + DepreciationExpense; } }
    }

    /// <summary>Kompaniya qiymatini ikki uslubda hisoblash natijasi.</summary>
    public class CompanyValuationBreakdown
    {
        public string Method { get; set; }
        public decimal CompanyValue { get; set; }

        // EBITDA uslubi
        public EbitdaData Ebitda { get; set; }
        public decimal EbitdaMultiple { get; set; }

        // DCF uslubi
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal NetAssets { get; set; }
        public decimal AvgMarginPercent { get; set; }
        public decimal AnnualRevenue { get; set; }
        public decimal AnnualProfit { get; set; }
        public int DcfYears { get; set; }
        public decimal FutureProfit { get; set; }
    }

    /// <summary>Balans popup va eksport uchun ombor hisob-kitob qatori.</summary>
    public sealed class BalanceSheetInventoryBreakdownRow
    {
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public double Qty { get; set; }
        public string Unit { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string Note { get; set; }
    }
}
