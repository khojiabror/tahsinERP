using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    public class OfficialLetterEditVm
    {
        public long DocumentId { get; set; }

        [Display(Name = "Yo'nalish")]
        public string DirectionCode { get; set; }

        [Display(Name = "Maktub raqami")]
        [Required(ErrorMessage = "Maktub raqami kiritilishi shart")]
        public string LetterNo { get; set; }

        [Display(Name = "Maktub sanasi")]
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        public DateTime LetterDate { get; set; }

        [Display(Name = "Tashqi kompaniya nomi")]
        public string ExternalCompanyName { get; set; }

        [Display(Name = "Tashqi kontakt shaxs")]
        public string ExternalContactName { get; set; }

        [Display(Name = "Tashqi kompaniya rahbari lavozimi")]
        public string ExternalContactPosition { get; set; }

        [Display(Name = "Bizning havola raqami")]
        public string OurRefNo { get; set; }

        [Display(Name = "Ularning havola raqami")]
        public string TheirRefNo { get; set; }

        [Display(Name = "Kanal")]
        public string ChannelCode { get; set; }

        [Display(Name = "Mavzu")]
        [Required(ErrorMessage = "Mavzu kiritilishi shart")]
        public string Topic { get; set; }

        [Display(Name = "Salomlashish")]
        public string GreetingLine { get; set; }

        [Display(Name = "Asosiy matn")]
        [AllowHtml]
        public string BodyText { get; set; }

        // Mahsulot va narx satrlari (bir nechta bo'lishi mumkin)
        public List<OfficialLetterItemVm> Items { get; set; } = new List<OfficialLetterItemVm>();

        public SelectList Directions { get; set; }
        public SelectList Channels { get; set; }
    }
}
