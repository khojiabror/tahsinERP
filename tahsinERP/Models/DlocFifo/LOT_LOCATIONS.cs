using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("LOT_LOCATIONS")]
    public class LOT_LOCATIONS
    {
        [Key]
        public int ID { get; set; }
        public int LotID { get; set; }
        public int LocationID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyAvailable { get; set; }
        public DateTime? LastMovementAt { get; set; }

        [ForeignKey(nameof(LotID))]
        public virtual INCOME_LOTS INCOME_LOTS { get; set; }

        [ForeignKey(nameof(LocationID))]
        public virtual WAREHOUSE_LOCATIONS WAREHOUSE_LOCATIONS { get; set; }
    }
}
