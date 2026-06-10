using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("EXPENSE_PRODUCT_ALLOCATIONS")]
    public class EXPENSE_PRODUCT_ALLOCATIONS
    {
        [Key]
        public int ID { get; set; }
        public int ExpenseProductID { get; set; }
        public int LotID { get; set; }
        public int FromLocationID { get; set; }
        public int? ToLocationID { get; set; }
        public decimal Qty { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
