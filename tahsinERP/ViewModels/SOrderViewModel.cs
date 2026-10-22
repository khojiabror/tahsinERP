using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class SOrderViewModel
    {
        public int? ID { get; set; }

        [Required(ErrorMessage = "Crm_OrdOrderNoRequired")]
        [Display(Name = "Crm_OrderNo")]
        public string OrderNo { get; set; }

        [Required(ErrorMessage = "Crm_OrdDateRequired")]
        [Display(Name = "Crm_OrdIssuedDateLong")]
        [DataType(DataType.Date)]
        public DateTime IssuedDate { get; set; }

        [Required(ErrorMessage = "Crm_OrdBuyerRequired")]
        [Display(Name = "Crm_Customer")]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Crm_OrdContractRequired")]
        [Display(Name = "Crm_ContractNo")]
        public int ContractID { get; set; }

        [Display(Name = "Currency")]
        public int CurrencyID { get; set; }

        [Display(Name = "Crm_OrdShortDescription")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Display(Name = "Crm_TotalAmount")]
        public decimal? Amount { get; set; }

        // Contract products with quantities
        public List<ContractProductQuantity> ProductQuantities { get; set; }

        public SOrderViewModel()
        {
            ProductQuantities = new List<ContractProductQuantity>();
            IssuedDate = DateTime.Now;
        }
    }

    public class ContractProductQuantity
    {
        public int ContractProductID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal PiecePrice { get; set; }
        public decimal ContractAmount { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
        public decimal OrderQuantity { get; set; } // User enters this
        public decimal VatRate { get; set; }
    }

    public class DeliveryScheduleViewModel
    {
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public string CustomerName { get; set; }
        public DateTime IssuedDate { get; set; }
        public List<OrderProductForSchedule> OrderProducts { get; set; }
        public List<DeliveryScheduleItem> ScheduleItems { get; set; }

        public DeliveryScheduleViewModel()
        {
            OrderProducts = new List<OrderProductForSchedule>();
            ScheduleItems = new List<DeliveryScheduleItem>();
        }
    }

    public class OrderProductForSchedule
    {
        public int SOrderProductID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal OrderQuantity { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal ShippedQuantity { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
    }

    /// <summary>Buyurtma qatori bo'yicha reja/yetkazilgan jami (CRM-B09: avval anonim tip — view'da doim null).</summary>
    public class ScheduleSummaryDto
    {
        public int SOrderProductID { get; set; }
        public decimal TotalPlanned { get; set; }
        public decimal TotalShipped { get; set; }
    }

    public class DeliveryScheduleItem
    {
        public long? ID { get; set; }
        public int SOrderProductID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public DateTime DeliveryDate { get; set; }
        public decimal QtyPlanned { get; set; }
        public decimal QtyShipped { get; set; }
        public string StatusCode { get; set; }
    }
}
