using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// CRM Dashboard - shartnomalar, buyurtmalar, invoyslar va kechikishlar bo'yicha statistika.
    /// Jahon yetuk ERP andozalariga mos (SAP, Oracle, Microsoft Dynamics).
    /// </summary>
    public class CRMDashboardViewModel
    {
        // ——— KPI kartalar ———
        public int TotalContracts { get; set; }
        public int ContractsOverdue { get; set; }
        public int ContractsCompleted { get; set; }
        public decimal ContractsTotalAmount { get; set; }

        public int TotalOrders { get; set; }
        public int OrdersWithInvoice { get; set; }
        public int OrdersWithoutInvoice { get; set; }
        public int OrdersDeliveryOverdue { get; set; }
        public decimal OrdersTotalAmount { get; set; }

        public int TotalInvoices { get; set; }
        public int InvoicesOverdue { get; set; }
        public decimal InvoicesTotalAmount { get; set; }

        public int TotalCustomers { get; set; }
        public int CustomersWithOrdersThisYear { get; set; }

        // ——— Grafiklar uchun ———
        /// <summary>Oy bo'yicha shartnomalar soni (ContractsByMonth)</summary>
        public List<CRMChartPoint> ContractsByMonth { get; set; } = new List<CRMChartPoint>();
        /// <summary>Oy bo'yicha buyurtmalar soni (OrdersByMonth)</summary>
        public List<CRMChartPoint> OrdersByMonth { get; set; } = new List<CRMChartPoint>();
        /// <summary>Buyurtma holati: invoys bilan / invoysiz</summary>
        public List<CRMChartPoint> OrderFulfillmentStatus { get; set; } = new List<CRMChartPoint>();
        /// <summary>Yetkazib berish rejasi bo'yicha kechikkan (delivery schedule)</summary>
        public List<CRMDeliveryOverdueItem> DeliveryOverdueList { get; set; } = new List<CRMDeliveryOverdueItem>();
        /// <summary>Invoys yaratilmagan buyurtmalar ro'yxati</summary>
        public List<CRMOrderWithoutInvoiceItem> OrdersWithoutInvoiceList { get; set; } = new List<CRMOrderWithoutInvoiceItem>();
        /// <summary>Muddati o'tgan shartnomalar ro'yxati</summary>
        public List<CRMContractOverdueItem> ContractsOverdueList { get; set; } = new List<CRMContractOverdueItem>();
        /// <summary>Muddati o'tgan invoyslar</summary>
        public List<CRMInvoiceOverdueItem> InvoicesOverdueList { get; set; } = new List<CRMInvoiceOverdueItem>();
        /// <summary>Mijoz bo'yicha buyurtma summasi (top N)</summary>
        public List<CRMCustomerOrderSum> TopCustomersByOrderAmount { get; set; } = new List<CRMCustomerOrderSum>();
        /// <summary>Mijozlar bo'yicha haqqimiz (invoys - to'lov), TOP ro'yxat</summary>
        public List<CRMCustomerReceivableItem> CustomerReceivables { get; set; } = new List<CRMCustomerReceivableItem>();
        /// <summary>Shartnoma bajarilishi: bajarilgan / davom etayotgan / muddati o'tgan</summary>
        public List<CRMChartPoint> ContractCompletionStatus { get; set; } = new List<CRMChartPoint>();
    }

    public class CRMChartPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public double NumericValue => (double)Value;
    }

    public class CRMDeliveryOverdueItem
    {
        public long ScheduleId { get; set; }
        public int OrderId { get; set; }
        public string OrderNo { get; set; }
        public string CustomerName { get; set; }
        public string ProductName { get; set; }
        public System.DateTime DeliveryDate { get; set; }
        public decimal QtyPlanned { get; set; }
        public decimal QtyShipped { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class CRMOrderWithoutInvoiceItem
    {
        public int OrderId { get; set; }
        public string OrderNo { get; set; }
        public string CustomerName { get; set; }
        public System.DateTime IssuedDate { get; set; }
        public decimal? Amount { get; set; }
        public string CurrencyCode { get; set; }
    }

    public class CRMContractOverdueItem
    {
        public int ContractId { get; set; }
        public string ContractNo { get; set; }
        public string CustomerName { get; set; }
        public System.DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal? Amount { get; set; }
    }

    public class CRMInvoiceOverdueItem
    {
        public long InvoiceId { get; set; }
        public string InvoiceNo { get; set; }
        public string CustomerName { get; set; }
        public System.DateTime? DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CRMCustomerOrderSum
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CRMCustomerReceivableItem
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal InvoicedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReceivableAmount { get; set; }
        public List<CRMCustomerReceivableContractItem> Contracts { get; set; } = new List<CRMCustomerReceivableContractItem>();
    }

    public class CRMCustomerReceivableContractItem
    {
        public int? ContractId { get; set; }
        public string ContractNo { get; set; }
        public decimal InvoicedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReceivableAmount { get; set; }
    }
}
