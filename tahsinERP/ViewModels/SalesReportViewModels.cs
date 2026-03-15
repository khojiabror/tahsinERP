using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Oylik savdo hisoboti — bitta oy uchun mahsulot bo'yicha buyurtma va sotuv (invoys).
    /// Savdo xodimi qo'l qo'yadi, rahbariyatga topshiradi.
    /// </summary>
    public class MonthlySalesReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public string CompanyName { get; set; }
        public List<MonthlySalesReportLineViewModel> Lines { get; set; } = new List<MonthlySalesReportLineViewModel>();
        public decimal TotalOrderAmount { get; set; }
        public decimal TotalSoldAmount { get; set; }
        public int TotalOrderCount { get; set; }
        public int TotalInvoiceCount { get; set; }
    }

    public class MonthlySalesReportLineViewModel
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        /// <summary>Buyurtma qilingan miqdor</summary>
        public decimal OrderQty { get; set; }
        /// <summary>Buyurtma bo'yicha summa (subtotal)</summary>
        public decimal OrderAmount { get; set; }
        /// <summary>Sotilgan (invoys) miqdor</summary>
        public decimal SoldQty { get; set; }
        /// <summary>Sotuv (invoys) summa</summary>
        public decimal SoldAmount { get; set; }
        /// <summary>Konversiya % (sotuv / buyurtma)</summary>
        public decimal ConversionPercent { get; set; }
    }

    /// <summary>
    /// Mijoz bo'yicha sotuv hisoboti — davr bo'yicha.</summary>
    public class SalesByCustomerReportViewModel
    {
        public System.DateTime FromDate { get; set; }
        public System.DateTime ToDate { get; set; }
        public string CompanyName { get; set; }
        public List<SalesByCustomerLineViewModel> Lines { get; set; } = new List<SalesByCustomerLineViewModel>();
        public decimal TotalAmount { get; set; }
    }

    public class SalesByCustomerLineViewModel
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public int OrderCount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalOrderAmount { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
    }

    /// <summary>
    /// Mahsulot bo'yicha sotuv hisoboti — davr bo'yicha.</summary>
    public class SalesByProductReportViewModel
    {
        public System.DateTime FromDate { get; set; }
        public System.DateTime ToDate { get; set; }
        public string CompanyName { get; set; }
        public List<SalesByProductLineViewModel> Lines { get; set; } = new List<SalesByProductLineViewModel>();
        public decimal TotalOrderAmount { get; set; }
        public decimal TotalSoldAmount { get; set; }
    }

    public class SalesByProductLineViewModel
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public decimal OrderQty { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal SoldQty { get; set; }
        public decimal SoldAmount { get; set; }
        public decimal ConversionPercent { get; set; }
    }
}
