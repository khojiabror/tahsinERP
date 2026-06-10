using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("WAREHOUSE_LOCATIONS")]
    public class WAREHOUSE_LOCATIONS
    {
        [Key]
        public int ID { get; set; }
        public int WarehouseID { get; set; }
        public string LocationCode { get; set; }
        public string LocationName { get; set; }
        public int? CapacityUomID { get; set; }
        public decimal MinCapacity { get; set; }
        public decimal MaxCapacity { get; set; }
        public bool AllowMixedLots { get; set; }
        public bool AllowMixedProducts { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(WarehouseID))]
        public virtual Warehouse Warehouse { get; set; }
    }
}
