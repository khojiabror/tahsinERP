using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Services.Tasks
{
    /// <summary>
    /// Tasks modulidagi umumiy qoidalar. Avval 13 joyda inline nusxa bo'lgan
    /// "Admin yoki Developer" tekshiruvi va "bajarildi" predikati shu yerda.
    /// </summary>
    public static class TaskRules
    {
        public const string DoneCode = "DONE";
        public const string CancelledCode = "CANCELLED";

        /// <summary>Barcha topshiriqlarni ko'radigan/boshqaradigan rollar (RoleHelper snapshot keshidan).</summary>
        public static bool IsAdminOrDeveloper(string username)
        {
            return RoleHelper.IsInRole(username, "Admin") || RoleHelper.IsInRole(username, "Developer");
        }

        /// <summary>Oddiy foydalanuvchi faqat o'ziga biriktirilgan topshiriq bilan ishlaydi.</summary>
        public static bool CanAccess(int? assigneeUserId, int currentUserId, bool isAdminOrDeveloper)
        {
            return isAdminOrDeveloper || assigneeUserId == currentUserId;
        }

    }

    /// <summary>
    /// Status/prioritet/loyiha dropdownlari uchun kesh (avval har GET va har validatsiya
    /// xatosida qayta so'ralardi, prioritetda IsDeleted filtri ham yo'q edi — TSK-P07/TSK-B04).
    /// TaskSettings controllerlari saqlashdan keyin <see cref="Invalidate"/> chaqiradi.
    /// </summary>
    public static class TaskLookups
    {
        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.TaskLookups");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private const string StatusesKey = "statuses";
        private const string PrioritiesKey = "priorities";
        private const string ProjectsKey = "projects";

        public sealed class Item
        {
            public int ID { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
        }

        /// <summary>Faol, o'chirilmagan statuslar (SortOrder bo'yicha).</summary>
        public static IReadOnlyList<Item> Statuses()
        {
            return Get(StatusesKey, db => db.APP_TASK_STATUS_REF.AsNoTracking()
                .Where(s => s.IsActive && s.IsDeleted != true)
                .OrderBy(s => s.SortOrder)
                .Select(s => new Item { ID = s.ID, Code = s.Code, Name = s.Name })
                .ToList());
        }

        /// <summary>O'chirilmagan prioritetlar (SortOrder bo'yicha).</summary>
        public static IReadOnlyList<Item> Priorities()
        {
            return Get(PrioritiesKey, db => db.APP_TASK_PRIORITY_REF.AsNoTracking()
                .Where(p => p.IsDeleted != true)
                .OrderBy(p => p.SortOrder)
                .Select(p => new Item { ID = p.ID, Code = p.Code, Name = p.Name })
                .ToList());
        }

        /// <summary>Faol, o'chirilmagan loyihalar (nom bo'yicha).</summary>
        public static IReadOnlyList<Item> Projects()
        {
            return Get(ProjectsKey, db => db.APP_TASK_PROJECTS.AsNoTracking()
                .Where(p => p.IsActive && p.IsDeleted != true)
                .OrderBy(p => p.Name)
                .Select(p => new Item { ID = p.ID, Code = p.Code, Name = p.Name })
                .ToList());
        }

        public static SelectList StatusList(object selected = null) => new SelectList(Statuses(), "ID", "Name", selected);

        public static SelectList PriorityList(object selected = null) => new SelectList(Priorities(), "ID", "Name", selected);

        public static SelectList ProjectList(object selected = null) => new SelectList(Projects(), "ID", "Name", selected);

        /// <summary>Faol foydalanuvchilar dropdowni — faqat ID va ism (avval to'liq USER entity: parol xeshi va h.k., TSK-P06).</summary>
        public static async Task<SelectList> UserListAsync(DBTHSNEntities db, object selected = null, int? onlyUserId = null)
        {
            var query = db.USERS.AsNoTracking().Where(u => u.IsDeleted != true && u.IsActive);
            if (onlyUserId.HasValue)
                query = query.Where(u => u.ID == onlyUserId.Value);

            var users = await query
                .Select(u => new { u.ID, FullName = u.FullName ?? u.Uname })
                .OrderBy(u => u.FullName)
                .ToListAsync();
            return new SelectList(users, "ID", "FullName", selected);
        }

        /// <summary>
        /// Berilgan ID'lar uchun ism lug'ati — bitta so'rov (avval har qator uchun UserHelper.GetFullName,
        /// o'chirilgan foydalanuvchida esa istisno tashlardi — TSK-P01/TSK-P05).
        /// </summary>
        public static async Task<Dictionary<int, string>> UserNamesAsync(DBTHSNEntities db, IEnumerable<int?> ids)
        {
            var list = ids.Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
            if (list.Count == 0)
                return new Dictionary<int, string>();

            return await db.USERS.AsNoTracking()
                .Where(u => list.Contains(u.ID))
                .Select(u => new { u.ID, Name = u.FullName ?? u.Uname })
                .ToDictionaryAsync(u => u.ID, u => u.Name);
        }

        public static string NameOf(Dictionary<int, string> names, int? id)
        {
            string name;
            return id.HasValue && names.TryGetValue(id.Value, out name) ? name : null;
        }

        public static void Invalidate()
        {
            Cache.Remove(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(StatusesKey));
            Cache.Remove(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(PrioritiesKey));
            Cache.Remove(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(ProjectsKey));
        }

        private static IReadOnlyList<Item> Get(string key, Func<DBTHSNEntities, List<Item>> load)
        {
            var cached = Cache.Get(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(key)) as List<Item>;
            if (cached != null)
                return cached;

            using (var db = new DBTHSNEntities())
            {
                cached = load(db);
            }
            Cache.Set(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(key), cached, DateTimeOffset.Now.Add(Ttl));
            return cached;
        }
    }
}
