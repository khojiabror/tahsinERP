using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Qo'llanma sahifasi: modullar ro'yxati va tanlangan modul kontenti
    /// </summary>
    public class UserManualPageViewModel
    {
        public List<UserManualModuleViewModel> Modules { get; set; }
        public UserManualModuleViewModel Current { get; set; }
        public string ContentHtml { get; set; }
        public string Language { get; set; }
    }
}
