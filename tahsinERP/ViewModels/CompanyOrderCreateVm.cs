using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    public class CompanyOrderCreateVm
    {
        [Display(Name = "Buyruq raqami")]
        public string OrderNo { get; set; }

        [Display(Name = "Buyruq sanasi")]
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Display(Name = "Buyruq turi")]
        public string OrderTypeCode { get; set; }

        [Display(Name = "Tashkilot bo'limi")]
        public int? OrgUnitId { get; set; }

        [Display(Name = "Sarlavha")]
        [Required(ErrorMessage = "Sarlavha kiritilishi shart")]
        public string Title { get; set; }

        [Display(Name = "Matn")]
        [AllowHtml]
        public string BodyText { get; set; }

        [Display(Name = "Javobgar xodimlar")]
        public IList<int> ResponsibleEmployeeIds { get; set; } = new List<int>();

        public SelectList OrderTypes { get; set; }
        public SelectList OrgUnits { get; set; }
    }
}

