using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Models
{
    [Table("FIFO_POLICY")]
    public class FIFO_POLICY
    {
        [Key]
        public int ID { get; set; }
        public int WarehouseID { get; set; }
        public string DocumentType { get; set; }
        public bool IsStrictFifo { get; set; }
        public bool AllowOverride { get; set; }
        public bool RequiresApprovalOnOverride { get; set; }
    }
}
