using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    public class OfficialLetterCreateVm
    {
        [Display(Name = "Doc_Direction")]
        public string DirectionCode { get; set; } = "OUT";

        [Display(Name = "Doc_LetterNo")]
        public virtual string LetterNo { get; set; }

        [Display(Name = "Doc_LetterDate")]
        [Required(ErrorMessage = "Doc_DateRequired")]
        public DateTime LetterDate { get; set; } = DateTime.Now;

        [Display(Name = "Doc_ExternalCompany")]
        public string ExternalCompanyName { get; set; }

        [Display(Name = "Doc_ExternalContact")]
        public string ExternalContactName { get; set; }

        [Display(Name = "Doc_ExternalPosition")]
        public string ExternalContactPosition { get; set; }

        [Display(Name = "Doc_OurRefNo")]
        public string OurRefNo { get; set; }

        [Display(Name = "Doc_TheirRefNo")]
        public string TheirRefNo { get; set; }

        [Display(Name = "Doc_Channel")]
        public string ChannelCode { get; set; } // EMAIL, LETTER, COURIER, etc.

        [Display(Name = "Doc_Topic")]
        [Required(ErrorMessage = "Doc_TopicRequired")]
        public string Topic { get; set; }

        [Display(Name = "Doc_Greeting")]
        public string GreetingLine { get; set; }

        [Display(Name = "Doc_MainText")]
        [AllowHtml]
        public string BodyText { get; set; }

        // Mahsulot va narx satrlari (bir nechta bo'lishi mumkin)
        public List<OfficialLetterItemVm> Items { get; set; } = new List<OfficialLetterItemVm>();

        public SelectList Directions { get; set; }
        public SelectList Channels { get; set; }
    }
}
