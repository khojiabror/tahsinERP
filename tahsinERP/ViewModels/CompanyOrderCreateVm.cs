using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    public class CompanyOrderCreateVm
    {
        [Display(Name = "Doc_OrderNo")]
        public string OrderNo { get; set; }

        [Display(Name = "Doc_OrderDate")]
        [Required(ErrorMessage = "Doc_DateRequired")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Display(Name = "Doc_OrderType")]
        public string OrderTypeCode { get; set; }

        [Display(Name = "Doc_OrgUnit")]
        public int? OrgUnitId { get; set; }

        [Display(Name = "Doc_Title")]
        [Required(ErrorMessage = "Doc_TitleRequired")]
        public string Title { get; set; }

        [Display(Name = "Doc_Text")]
        [AllowHtml]
        public string BodyText { get; set; }

        [Display(Name = "Doc_ResponsibleEmployees")]
        public IList<int> ResponsibleEmployeeIds { get; set; } = new List<int>();

        public SelectList OrderTypes { get; set; }
        public SelectList OrgUnits { get; set; }
    }
}

