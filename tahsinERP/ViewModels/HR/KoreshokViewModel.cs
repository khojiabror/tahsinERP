using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.HR
{
    public class KoreshokFilterViewModel
    {
        public int EmployeeID { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
    }

    public class KoreshokResultViewModel
    {
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string TabNo { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        public decimal HourlyCalculated { get; set; }
        public decimal HourlyWorkedDays { get; set; }
        public decimal HourlyWorkedHours { get; set; }
        public decimal HourlyPaid { get; set; }
        public decimal HourlyBalance { get; set; }

        public decimal PieceCalculated { get; set; }
        public decimal PiecePaid { get; set; }
        public decimal PieceBalance { get; set; }

        public decimal TotalCalculated => HourlyCalculated + PieceCalculated;
        public decimal TotalPaid => HourlyPaid + PiecePaid;
        public decimal TotalBalance => TotalCalculated - TotalPaid;

        public List<string> Notes { get; set; } = new List<string>();
    }
}
