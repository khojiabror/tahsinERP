using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.Purchasing
{
    public class PurchasingWhIncomeViewModel
    {
        public int ID { get; set; }
        public string DocNo { get; set; }
        public int WHID { get; set; }
        public string WHName { get; set; }
        public Nullable<int> ContractID { get; set; }
        public Nullable<double> Amount { get; set; }
        public string Currency { get; set; }
        public Nullable<double> TotalPrice { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public string Description { get; set; }
        public System.DateTime IssueDateTime { get; set; }
        public Nullable<int> SenderWHID { get; set; }
        public Nullable<bool> RecieveStatus { get; set; }

        public virtual PART_WRHS PART_WRHS { get; set; }
        public virtual PART_WRHS PART_WRHS1 { get; set; }
        public virtual PURCHASING_CONTRACTS PURCHASING_CONTRACTS { get; set; }
        public List<PurchasingIncomeItem> Items { get; set; }
        public PurchasingWhIncomeViewModel()
        {
            Items = new List<PurchasingIncomeItem>();
        }
    }
    public class PurchasingIncomeItem
    {
        public int ID { get; set; }
        public int IncomeID { get; set; }
        public int ItemID { get; set; }
        public double Amount { get; set; }
        public Nullable<double> PiecePrice { get; set; }
        public Nullable<double> TotalPrice { get; set; }
        public string Comment { get; set; }
        public int UnitID { get; set; }

        public virtual PURCHASING_ITEMS PURCHASING_ITEMS { get; set; }
        public virtual PURCHASING_WRHS_INCOMES PURCHASING_WRHS_INCOMES { get; set; }
        public virtual UNIT UNIT { get; set; }
    }
}