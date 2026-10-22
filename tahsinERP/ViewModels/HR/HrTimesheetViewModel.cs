using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.HR
{
    public class HrTimesheetHeaderViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_Year")]
        public int Year { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Hr_Month")]
        public int Month { get; set; }
        
        [Display(Name = "Hr_OrgUnit")]
        public int? OrgUnitID { get; set; }
        public string OrgUnitName { get; set; }

        [Display(Name = "Hr_FundHours")]
        [Range(1, 744, ErrorMessage = "Val_Range")]
        public decimal FundHours { get; set; } = 160m;
        
        [Display(Name = "Status")]
        public string Status { get; set; }
        
        public int? CreatedByUserID { get; set; }
        public DateTime CreatedDT { get; set; }
        public int? ApprovedByUserID { get; set; }
        public DateTime? ApprovedDT { get; set; }
        
        public List<HrTimesheetLineViewModel> Lines { get; set; }
        
        public HrTimesheetHeaderViewModel()
        {
            Lines = new List<HrTimesheetLineViewModel>();
        }
    }
    
    public class HrTimesheetLineViewModel
    {
        public int ID { get; set; }
        public int TimesheetID { get; set; }
        public int EmployeeID { get; set; }
        public string EmployeeTabNo { get; set; }
        public string EmployeeFullName { get; set; }
        public string PositionName { get; set; }
        
        public decimal? Day01Hours { get; set; }
        public decimal? Day02Hours { get; set; }
        public decimal? Day03Hours { get; set; }
        public decimal? Day04Hours { get; set; }
        public decimal? Day05Hours { get; set; }
        public decimal? Day06Hours { get; set; }
        public decimal? Day07Hours { get; set; }
        public decimal? Day08Hours { get; set; }
        public decimal? Day09Hours { get; set; }
        public decimal? Day10Hours { get; set; }
        public decimal? Day11Hours { get; set; }
        public decimal? Day12Hours { get; set; }
        public decimal? Day13Hours { get; set; }
        public decimal? Day14Hours { get; set; }
        public decimal? Day15Hours { get; set; }
        public decimal? Day16Hours { get; set; }
        public decimal? Day17Hours { get; set; }
        public decimal? Day18Hours { get; set; }
        public decimal? Day19Hours { get; set; }
        public decimal? Day20Hours { get; set; }
        public decimal? Day21Hours { get; set; }
        public decimal? Day22Hours { get; set; }
        public decimal? Day23Hours { get; set; }
        public decimal? Day24Hours { get; set; }
        public decimal? Day25Hours { get; set; }
        public decimal? Day26Hours { get; set; }
        public decimal? Day27Hours { get; set; }
        public decimal? Day28Hours { get; set; }
        public decimal? Day29Hours { get; set; }
        public decimal? Day30Hours { get; set; }
        public decimal? Day31Hours { get; set; }
        
        public decimal TotalWorkedDays { get; set; }
        public decimal TotalWorkedHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal AbsentDays { get; set; }
        public string Notes { get; set; }
        
        // Piece-rate fields
        public int? ProductID { get; set; }
        public string ProductName { get; set; }
        public bool IsPieceRate { get; set; }
        
        public decimal? Day01Quantity { get; set; }
        public decimal? Day02Quantity { get; set; }
        public decimal? Day03Quantity { get; set; }
        public decimal? Day04Quantity { get; set; }
        public decimal? Day05Quantity { get; set; }
        public decimal? Day06Quantity { get; set; }
        public decimal? Day07Quantity { get; set; }
        public decimal? Day08Quantity { get; set; }
        public decimal? Day09Quantity { get; set; }
        public decimal? Day10Quantity { get; set; }
        public decimal? Day11Quantity { get; set; }
        public decimal? Day12Quantity { get; set; }
        public decimal? Day13Quantity { get; set; }
        public decimal? Day14Quantity { get; set; }
        public decimal? Day15Quantity { get; set; }
        public decimal? Day16Quantity { get; set; }
        public decimal? Day17Quantity { get; set; }
        public decimal? Day18Quantity { get; set; }
        public decimal? Day19Quantity { get; set; }
        public decimal? Day20Quantity { get; set; }
        public decimal? Day21Quantity { get; set; }
        public decimal? Day22Quantity { get; set; }
        public decimal? Day23Quantity { get; set; }
        public decimal? Day24Quantity { get; set; }
        public decimal? Day25Quantity { get; set; }
        public decimal? Day26Quantity { get; set; }
        public decimal? Day27Quantity { get; set; }
        public decimal? Day28Quantity { get; set; }
        public decimal? Day29Quantity { get; set; }
        public decimal? Day30Quantity { get; set; }
        public decimal? Day31Quantity { get; set; }
        public decimal? TotalQuantity { get; set; }
        
        public int DaysInMonth { get; set; }
    }
}

