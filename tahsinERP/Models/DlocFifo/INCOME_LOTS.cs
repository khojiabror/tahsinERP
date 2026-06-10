using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("INCOME_LOTS")]
    public class INCOME_LOTS
    {
        [Key]
        public int ID { get; set; }
        public int IncomeID { get; set; }
        public int IncomeProductID { get; set; }
        public int ProductID { get; set; }
        public int WarehouseID { get; set; }
        public string LotNo { get; set; }
        public DateTime ReceivedDateTime { get; set; }
        public decimal QtyReceived { get; set; }
        public decimal QtyConsumed { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal? QtyAvailable { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
