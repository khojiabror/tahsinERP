using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace tahsinERP.ViewModels
{
    /// <summary>Tahrirlash = yaratish maydonlari + hujjat ID (Create/Edit bitta _Form partial'ini ishlatadi — dublikat #15).</summary>
    public class OfficialLetterEditVm : OfficialLetterCreateVm
    {
        public long DocumentId { get; set; }

        [Display(Name = "Doc_LetterNo")]
        [Required(ErrorMessage = "Doc_LetterNoRequired")]
        public override string LetterNo
        {
            get { return base.LetterNo; }
            set { base.LetterNo = value; }
        }
    }
}
