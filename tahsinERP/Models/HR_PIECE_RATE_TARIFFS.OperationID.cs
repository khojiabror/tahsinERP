//------------------------------------------------------------------------------
// Partial class extension for HR_PIECE_RATE_TARIFFS to add OperationID and CurrencyID
// Note: After running SQL scripts and updating DBTHSNModel.edmx,
// these properties should be auto-generated. This file is a temporary workaround.
//------------------------------------------------------------------------------

namespace tahsinERP.Models
{
    public partial class HR_PIECE_RATE_TARIFFS
    {
        public Nullable<int> OperationID { get; set; }
        public Nullable<int> CurrencyID { get; set; }
        
        public virtual HR_PIECE_RATE_OPERATIONS HR_PIECE_RATE_OPERATIONS { get; set; }
        public virtual CURRENCy CURRENCy { get; set; }
    }
}

