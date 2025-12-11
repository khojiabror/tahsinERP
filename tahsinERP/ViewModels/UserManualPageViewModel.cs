using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// User Manual page view model
    /// </summary>
    public class UserManualPageViewModel
    {
        public List<UserManualChapterViewModel> Chapters { get; set; }
        public string InitialChapterId { get; set; }
    }
}
