using System;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Kirim/chiqim hujjatini o'chirishni tasdiqlash sahifasi (Views/Incomes/_DocDeleteConfirm.cshtml) —
    /// Incomes/Delete va Expenses/Delete faqat matnlari bilan farq qilardi (audit "Duplicates map").
    /// </summary>
    public class DocDeleteConfirmViewModel
    {
        public string Controller { get; set; }
        public string Title { get; set; }
        public string ListTitle { get; set; }
        public string DocKind { get; set; }
        public string WarehouseLabel { get; set; }

        public int ID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public string WarehouseName { get; set; }
        public double? TotalPrice { get; set; }
    }
}
