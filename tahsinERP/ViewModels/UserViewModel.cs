using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class UserViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Core_EnterUserName")]
        [Display(Name = "Core_UserName")]
        public string UName { get; set; }

        public string FullName { get; set; }

        [EmailAddress(ErrorMessage = "Core_EmailInvalid")]
        public string Email { get; set; }

        [DataType(DataType.Upload)]
        public HttpPostedFileBase File;

        public int companyID { get; set; }

        [Required(ErrorMessage = "Core_EnterPassword")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        // Akkaunt sozlamalari: amaldagi parol (tekshiruv uchun) va yangi parol takrori
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        public string Role { get; set; }
        public int RoleID { get; set; }
        public bool IsActive { get; set; }

        [Display(Name = "Core_KeepMeSignedShort")]
        public bool KeepMeSigned { get; set; }

        public List<USER> userList { get; set; }
        public int SelectedRoleId { get; set; }
        public List<string> Roles { get; set; }
        public string IP_adrr { get; set; }
        public string MAC_adrr { get; set; }
        public DateTime datetime { get; set; }
        public List<USERLOG> logs { get; set; }
    }
}
