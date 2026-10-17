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
        [Display(Name = "Requisition No")]
        public string RequisitionNo { get; set; }
        
        [Required(ErrorMessage = "Requisition date is required")]
        [Display(Name = "Requisition Date")]
        [DataType(DataType.Date)]
        public DateTime RequisitionDate { get; set; } = DateTime.Now;
        
        [Display(Name = "Department")]
        public int? DepartmentID { get; set; }
        
        [Required(ErrorMessage = "Requester is required")]
        [Display(Name = "Requester")]
        public int RequesterID { get; set; }
        
        [Display(Name = "Contact Phone")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string ContactPhone { get; set; }
        
        [Display(Name = "Purpose of Purchase")]
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
        
        [Required(ErrorMessage = "Product is required")]
        public int ProductID { get; set; }
        
        [Display(Name = "Product Code")]
        public string ProductPNo { get; set; }
        
        [Display(Name = "Product Name")]
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        [Display(Name = "Quantity")]
        public double Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit is required")]
        [Display(Name = "Unit")]
        public int UnitID { get; set; }
        
        [Display(Name = "Estimated Price")]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
        public double? EstimatedPrice { get; set; }
        
        [Display(Name = "Amount")]
        public double? Amount { get; set; }
        
        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
        
        [Display(Name = "Required Date")]
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
        [Display(Name = "Role")]
        public string Role { get; set; }
        
        [Display(Name = "Approver")]
        public int? ApproverID { get; set; }
        
        [Display(Name = "Approver Name")]
        public string ApproverName { get; set; }
        
        [Display(Name = "Signature")]
        public string Signature { get; set; }
        
        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }
        
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";
        
        [Display(Name = "Comments")]
        public string Comments { get; set; }
        
        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }
    }
}

