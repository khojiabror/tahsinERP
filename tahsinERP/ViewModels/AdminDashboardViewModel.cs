using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Admin Dashboard uchun statistika modeli
    /// </summary>
    public class AdminDashboardViewModel
    {
        // Foydalanuvchilar statistikasi
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }

        // Rollar statistikasi
        public int TotalRoles { get; set; }
        public int TotalPermissionModules { get; set; }

        // Log statistikasi
        public int TodayLogs { get; set; }
        public int ThisWeekLogs { get; set; }

        // Tizimga kirishlar
        public int TotalUserEntries { get; set; }
        public int TodayUserEntries { get; set; }

        // Ro'yxatlar
        public List<USER_ENTRIES> RecentUserEntries { get; set; }
        public List<USERLOG> RecentLogs { get; set; }
        public List<ROLE> Roles { get; set; }
        public List<USER> Users { get; set; }
    }

    /// <summary>
    /// Permission boshqaruv modeli
    /// </summary>
    public class PermissionManagementViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public List<ModulePermissionViewModel> Modules { get; set; }
    }

    /// <summary>
    /// Modul huquqlari modeli
    /// </summary>
    public class ModulePermissionViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Parameter { get; set; }
        public bool ViewPermit { get; set; }
        public bool ChangePermit { get; set; }
    }
}

