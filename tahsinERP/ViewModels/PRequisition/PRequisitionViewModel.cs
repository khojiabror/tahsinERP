using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels.PRequisition
{
    public class PRequisitionViewModel
    {
        public int ID { get; set; }
        
        // Bo'sh qoldirilsa avtomatik beriladi (forma ham shuni aytadi). [Required] tufayli
        // avtogeneratsiya hech qachon ishlamas edi (SRM-B03).
        [Display(Name = "Srm_RequisitionNo")]
        public string RequisitionNo { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Srm_RequisitionDate")]
        [DataType(DataType.Date)]
        public DateTime RequisitionDate { get; set; } = DateTime.Now;
        
        [Display(Name = "Srm_Department")]
        public int? DepartmentID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Srm_Requester")]
        public int RequesterID { get; set; }
        
        [Display(Name = "Srm_ContactPhone")]
        [Phone(ErrorMessage = "Val_Phone")]
        public string ContactPhone { get; set; }
        
        [Display(Name = "Srm_PurchasePurpose")]
        [DataType(DataType.MultilineText)]
        public string Purpose { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft";
        
        public List<PRequisitionProductViewModel> RequisitionProducts { get; set; }
        
        public List<PRequisitionApprovalViewModel> Approvals { get; set; }
        
        public PRequisitionViewModel()
        {
            RequisitionProducts = new List<PRequisitionProductViewModel>();
            Approvals = new List<PRequisitionApprovalViewModel>();
        }
    }
    
    public class PRequisitionProductViewModel
    {
        public int? ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        public int ProductID { get; set; }
        
        [Display(Name = "Srm_ProductCode")]
        public string ProductPNo { get; set; }
        
        [Display(Name = "Srm_ProductName")]
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Val_Positive")]
        [Display(Name = "Quantity")]
        public double Quantity { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Unit")]
        public int UnitID { get; set; }
        
        [Display(Name = "Srm_EstimatedPrice")]
        [Range(0, double.MaxValue, ErrorMessage = "Srm_Val_NonNegative")]
        public double? EstimatedPrice { get; set; }
        
        [Display(Name = "Amount")]
        public double? Amount { get; set; }
        
        [Display(Name = "Srm_Priority")]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
        
        [Display(Name = "Srm_RequiredDate")]
        [DataType(DataType.Date)]
        public DateTime? RequiredDate { get; set; }
        
        [Display(Name = "Description")]
        public string Description { get; set; }
    }
    
    public class PRequisitionApprovalViewModel
    {
        public int? ID { get; set; }
        
        [Required]
        public int RequisitionID { get; set; }
        
        [Required]
        [Display(Name = "Srm_Role")]
        public string Role { get; set; }
        
        [Display(Name = "Srm_Approver")]
        public int? ApproverID { get; set; }
        
        [Display(Name = "Srm_ApproverName")]
        public string ApproverName { get; set; }
        
        [Display(Name = "Srm_Signature")]
        public string Signature { get; set; }
        
        [Display(Name = "Srm_ApprovalDate")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";
        
        [Display(Name = "Comment")]
        public string Comments { get; set; }
        
        [Display(Name = "Srm_SortOrder")]
        public int SortOrder { get; set; }
    }
}

