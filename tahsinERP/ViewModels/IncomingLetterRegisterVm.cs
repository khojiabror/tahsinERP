using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class IncomingLetterRegisterVm
    {
        [Required(ErrorMessage = "Kompaniya/shaxs nomi kiritilishi shart")]
        [Display(Name = "Kompaniya/Shaxs nomi")]
        public string CompanyOrPersonName { get; set; }

        [Required(ErrorMessage = "Xat raqami kiritilishi shart")]
        [Display(Name = "Xat raqami")]
        public string LetterNo { get; set; }

        [Display(Name = "Xat sanasi")]
        [DataType(DataType.Date)]
        public DateTime? LetterDate { get; set; }

        [Display(Name = "Xat ilovasi (PDF)")]
        public HttpPostedFileBase AttachmentFile { get; set; }
    }
}

