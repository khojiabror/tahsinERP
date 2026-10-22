using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class IncomingLetterRegisterVm
    {
        [Required(ErrorMessage = "Doc_CompanyOrPersonRequired")]
        [Display(Name = "Doc_CompanyOrPerson")]
        public string CompanyOrPersonName { get; set; }

        [Required(ErrorMessage = "Doc_IncomingNoRequired")]
        [Display(Name = "Doc_IncomingNo")]
        public string LetterNo { get; set; }

        [Display(Name = "Doc_IncomingDate")]
        [DataType(DataType.Date)]
        public DateTime? LetterDate { get; set; }

        [Display(Name = "Doc_IncomingAttachment")]
        public HttpPostedFileBase AttachmentFile { get; set; }
    }
}

