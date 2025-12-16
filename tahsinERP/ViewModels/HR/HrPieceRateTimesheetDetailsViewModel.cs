using System;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.HR
{
    public class HrPieceRateTimesheetDetailsViewModel
    {
        public HR_PIECE_RATE_WORK_ENTRIES Entry { get; set; }
        public HR_PIECE_RATE_TARIFFS Tariff { get; set; }
        public decimal EntryAmount { get; set; }
    }
}

