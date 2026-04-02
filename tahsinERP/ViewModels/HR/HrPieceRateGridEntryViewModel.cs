using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class HrPieceRateGridRowViewModel
    {
        public int? EntryID { get; set; }
        public int? EmployeeID { get; set; }
        public int? ProductID { get; set; }
        public int? OperationID { get; set; }
        public DateTime? WorkDate { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? TariffRate { get; set; }
        public decimal? Amount { get; set; }
    }

    public class HrPieceRateGridEntryViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? OrgUnitID { get; set; }
        public List<HrPieceRateGridRowViewModel> Rows { get; set; } = new List<HrPieceRateGridRowViewModel>();
    }
}
