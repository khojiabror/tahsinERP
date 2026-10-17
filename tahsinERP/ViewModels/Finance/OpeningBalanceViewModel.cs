using System;
using System.Collections.Generic;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.Finance
{
    public class OpeningBalanceDateRow
    {
        public DateTime SnapshotDate { get; set; }
        public int LineCount { get; set; }
    }

    public class OpeningBalanceDeleteViewModel
    {
        public DateTime SnapshotDate { get; set; }
        public int LineCount { get; set; }
    }

    public class OpeningBalanceCategoryItem
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int Type { get; set; }
        public string TypeName { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyID { get; set; }
        public string CurrencyShortname { get; set; }

        /// <summary>
        /// true: summasi 0 bo'lsa snapshot yozilmaydi (balansda "yo'q" — faqat tranzaksiyalar).
        /// false: 0 ham aniq opening sifatida FIN_BALANCE_SNAPSHOTS ga yoziladi.
        /// </summary>
        public bool LeaveEmpty { get; set; } = true;
    }

    /// <summary>
    /// Hisob (kassa/bank) kesimidagi opening (FIN-B16). FIN_BALANCE_SNAPSHOTS'da
    /// SourceType = "OpeningBalanceAccount", SourceID = AccountID sifatida saqlanadi; kiritilgan bo'lsa
    /// hisob kategoriyasining opening'i shu hisoblar yig'indisiga teng bo'ladi.
    /// </summary>
    public class OpeningBalanceAccountItem
    {
        public int AccountID { get; set; }
        public string AccountName { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public string CurrencyShortname { get; set; }
        /// <summary>Bo'sh — bu hisob uchun opening kiritilmagan.</summary>
        public decimal? Amount { get; set; }
    }

    public class OpeningBalanceViewModel
    {
        public List<OpeningBalanceAccountItem> AccountItems { get; set; } = new List<OpeningBalanceAccountItem>();
        public DateTime SnapshotDate { get; set; }
        public IEnumerable<SelectListItem> CurrencyList { get; set; }
        public int DefaultCurrencyID { get; set; }
        public List<OpeningBalanceCategoryItem> Items { get; set; } = new List<OpeningBalanceCategoryItem>();
        public List<OpeningBalanceContractLineItem> CustomerReceivableLines { get; set; } = new List<OpeningBalanceContractLineItem>();
        public List<OpeningBalanceContractLineItem> SupplierPayableLines { get; set; } = new List<OpeningBalanceContractLineItem>();
        public List<OpeningBalancePayrollLineItem> EmployeePayrollLines { get; set; } = new List<OpeningBalancePayrollLineItem>();
        public List<OpeningBalanceContractLineItem> ForwarderPayableLines { get; set; } = new List<OpeningBalanceContractLineItem>();
        public IEnumerable<SelectListItem> CustomerList { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> SupplierList { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> EmployeeList { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> ForwarderList { get; set; } = new List<SelectListItem>();
        public bool LeaveCustomerReceivableEmpty { get; set; }
        public bool LeaveSupplierPayableEmpty { get; set; }
        public bool LeavePayrollEmpty { get; set; }
        public bool LeaveForwarderPayableEmpty { get; set; }
    }

    public class OpeningBalanceContractLineItem
    {
        public int CounterpartyID { get; set; }
        public string CounterpartyName { get; set; }
        public int? ContractID { get; set; }
        public string ContractNo { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyID { get; set; }
    }

    public class OpeningBalancePayrollLineItem
    {
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyID { get; set; }
    }
}

