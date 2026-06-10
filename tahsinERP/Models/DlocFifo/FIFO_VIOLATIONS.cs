using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("FIFO_VIOLATIONS")]
    public class FIFO_VIOLATIONS
    {
        [Key]
        public int ID { get; set; }
        public int ExpenseProductID { get; set; }
        public int ProductID { get; set; }
        public int WarehouseID { get; set; }
        public int? ExpectedLotID { get; set; }
        public int? SelectedLotID { get; set; }
        public DateTime? ExpectedReceivedAt { get; set; }
        public DateTime? SelectedReceivedAt { get; set; }
        public string ReasonCode { get; set; }
        public string CreatedBy { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
