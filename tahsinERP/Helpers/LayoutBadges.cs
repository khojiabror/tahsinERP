using System;
using System.Linq;
using System.Web;
using System.Web.Caching;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// _Layout header'idagi badge'lar (ochiq topshiriqlar, qabul qilinmagan kirimlar).
    /// Avval har sahifa ochilganda 3–4 ta keshsiz so'rov edi; endi foydalanuvchi bo'yicha 60 soniya keshlanadi.
    /// </summary>
    public static class LayoutBadges
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        [Serializable]
        public sealed class Counts
        {
            public int OpenTasks { get; set; }
            public int PendingIncomes { get; set; }
            public bool IsMrpUser { get; set; }
        }

        public static Counts Get(string username)
        {
            if (string.IsNullOrEmpty(username))
                return new Counts();

            string key = tahsinERP.Configuration.Tenancy.TenantContext.CacheKey("LayoutBadges_" + username.ToLowerInvariant());
            var cached = HttpRuntime.Cache.Get(key) as Counts;
            if (cached != null)
                return cached;

            var result = new Counts();
            try
            {
                var userId = RoleHelper.GetUserId(username);
                if (userId.HasValue)
                {
                    int uid = userId.Value;
                    using (var db = new DBTHSNEntities())
                    {
                        result.OpenTasks = db.APP_TASKS.Count(t => t.AssigneeUserID == uid && t.IsDeleted == false &&
                            (t.APP_TASK_STATUS_REF == null || t.APP_TASK_STATUS_REF.Code != "DONE"));

                        var mrpWarehouseIds = db.WAREHOUSES
                            .Where(w => w.IsDeleted != true && w.MRP == uid)
                            .Select(w => w.ID)
                            .ToList();
                        result.IsMrpUser = mrpWarehouseIds.Count > 0;
                        if (result.IsMrpUser)
                        {
                            result.PendingIncomes = db.INCOMES.Count(i =>
                                i.IsDeleted != true &&
                                i.RecieveStatus != true &&
                                mrpWarehouseIds.Contains(i.ReceivingWHID));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[LayoutBadges] " + ex.Message);
            }

            HttpRuntime.Cache.Insert(key, result, null, DateTime.Now.Add(Ttl), Cache.NoSlidingExpiration);
            return result;
        }
    }
}
