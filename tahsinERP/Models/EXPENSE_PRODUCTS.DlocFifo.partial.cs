namespace tahsinERP.Models
{
    public partial class EXPENSE_PRODUCTS
    {
        public int? LotID { get; set; }
        public int? FromLocationID { get; set; }
        public string AllocationPolicy { get; set; }
        public string ViolationReasonCode { get; set; }
    }
}
