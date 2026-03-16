using System;
using System.Collections.Generic;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.Finance
{
    public class OpeningBalanceCategoryItem
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int Type { get; set; }
        public string TypeName { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyID { get; set; }
        public string CurrencyShortname { get; set; }
    }

    public class OpeningBalanceViewModel
    {
        public DateTime SnapshotDate { get; set; }
        public IEnumerable<SelectListItem> CurrencyList { get; set; }
        public int DefaultCurrencyID { get; set; }
        public List<OpeningBalanceCategoryItem> Items { get; set; } = new List<OpeningBalanceCategoryItem>();
    }
}

