using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Models;
using tahsinERP.Services.Editions;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// _Layout header'idagi badge'lar va profil menyusidagi shortcut'lar (ochiq topshiriqlar, qabul qilinmagan kirimlar,
    /// tasdiq kutayotgan distributsiya/sayt hujjatlari). Har shortcut faqat tegishli ruxsat bo'lsa chiqadi.
    /// Avval har sahifa ochilganda 3–4 ta keshsiz so'rov edi; endi foydalanuvchi bo'yicha 60 soniya keshlanadi.
    /// </summary>
    public static class LayoutBadges
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        [Serializable]
        public sealed class Shortcut
        {
            public string Url { get; set; }
            public string Icon { get; set; }
            /// <summary>ErpStrings kaliti (L.T).</summary>
            public string Label { get; set; }
            public int Count { get; set; }
        }

        [Serializable]
        public sealed class Counts
        {
            public int OpenTasks { get; set; }
            public bool CanSeeTasks { get; set; }
            public int PendingIncomes { get; set; }
            public bool IsMrpUser { get; set; }
            /// <summary>Tasdiq/amal kutayotgan hujjatlar — ruxsati borlari, menyu tartibida.</summary>
            public List<Shortcut> Approvals { get; set; } = new List<Shortcut>();

            public bool HasAlert => (CanSeeTasks && OpenTasks > 0) || PendingIncomes > 0 || Approvals.Any(a => a.Count > 0);
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
                    result.CanSeeTasks = RoleHelper.IsViewPermitted(username, "Tasks");
                    using (var db = new DBTHSNEntities())
                    {
                        if (result.CanSeeTasks)
                        {
                            result.OpenTasks = db.APP_TASKS.Count(t => t.AssigneeUserID == uid && t.IsDeleted == false &&
                                (t.APP_TASK_STATUS_REF == null || t.APP_TASK_STATUS_REF.Code != "DONE"));
                        }

                        var mrpWarehouseIds = db.WAREHOUSES
                            .Where(w => w.IsDeleted != true && w.MRP == uid)
                            .Select(w => w.ID)
                            .ToList();
                        result.IsMrpUser = mrpWarehouseIds.Count > 0 && RoleHelper.IsViewPermitted(username, "Incomes");
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

            try
            {
                result.Approvals = Approvals(username);
            }
            catch (Exception ex)
            {
                // dst/site sxemasi yo'q yoki eski baza — header buzilmasin
                System.Diagnostics.Trace.TraceError("[LayoutBadges] approvals: " + ex.Message);
            }

            HttpRuntime.Cache.Insert(key, result, null, DateTime.Now.Add(Ttl), Cache.NoSlidingExpiration);
            return result;
        }

        /// <summary>Modul paketda bor va foydalanuvchida o'zgartirish (amal) huquqi bor.</summary>
        private static bool CanAct(string username, string module) =>
            EditionGate.IsControllerAllowed(module) && RoleHelper.IsChangePermitted(username, module);

        private static List<Shortcut> Approvals(string username)
        {
            var list = new List<Shortcut>();
            bool orders = CanAct(username, "DstOrders");
            bool writeOffs = DstPermissions.Has(username, DstPermissions.WriteOffApprove) && EditionGate.IsControllerAllowed("DstWriteOffs");
            bool cash = CanAct(username, "DstPayments");
            bool points = CanAct(username, "DstCustomers");
            bool leads = CanAct(username, "SiteLeads");
            if (!(orders || writeOffs || cash || points || leads))
                return list;

            using (var db = new DistributionDbContext())
            {
                var scope = ReportScope.For(db, username);
                if (orders)
                {
                    // Rahbar paneli bilan bir xil ko'rinish doirasi (agent — o'zi, omborchi — o'z ombori)
                    IQueryable<DstOrder> q = db.Orders.Where(o => o.Status != DocumentStatus.Cancelled);
                    q = scope.Agents.Apply(q, o => o.AgentUserID);
                    q = scope.Customers.Apply(q, o => o.CustomerID);
                    q = scope.Warehouses.Apply(q, o => o.WarehouseID);
                    byte sNew = (byte)OrderStatus.New, sConfirmed = (byte)OrderStatus.Confirmed, sPicking = (byte)OrderStatus.Picking;
                    var byStatus = q.Where(o => o.OrderStatus == sNew || o.OrderStatus == sConfirmed || o.OrderStatus == sPicking)
                        .GroupBy(o => o.OrderStatus == sNew).Select(g => new { IsNew = g.Key, N = g.Count() }).ToList();
                    list.Add(new Shortcut { Url = "~/Distribution/DstOrders?status=" + sNew, Icon = "bx-check-circle", Label = "Hdr_OrdersToConfirm",
                        Count = byStatus.Where(s => s.IsNew).Sum(s => s.N) });
                    list.Add(new Shortcut { Url = "~/Distribution/DstOrders?status=" + sConfirmed, Icon = "bx-package", Label = "Hdr_OrdersToShip",
                        Count = byStatus.Where(s => !s.IsNew).Sum(s => s.N) });
                }
                if (writeOffs)
                {
                    var q = scope.Warehouses.Apply(db.WriteOffs.AsQueryable(), w => w.WarehouseID);
                    list.Add(new Shortcut { Url = "~/Distribution/DstWriteOffs?status=waiting", Icon = "bx-trash", Label = "Hdr_WriteOffsToApprove",
                        Count = q.Count(w => w.Status == DocumentStatus.Draft && w.SubmittedAt != null) });
                }
                if (cash)
                {
                    list.Add(new Shortcut { Url = "~/Distribution/DstPayments/Cash", Icon = "bx-wallet", Label = "Hdr_CashHandovers",
                        Count = db.CashHandovers.Count(h => h.Status == DocumentStatus.Draft) });
                }
                if (points)
                {
                    list.Add(new Shortcut { Url = "~/Distribution/DstCustomers?approval=pending", Icon = "bx-store", Label = "Hdr_NewPoints",
                        Count = scope.VisibleCustomers(db).Join(db.CustomerProfiles.Where(p => !p.IsApproved), id => id, p => p.CustomerID, (id, p) => id).Count() });
                }
                if (leads)
                {
                    byte lNew = (byte)Areas.Site.Data.LeadStatus.New;
                    list.Add(new Shortcut { Url = "~/Site/SiteLeads?status=" + lNew, Icon = "bx-globe", Label = "Hdr_SiteLeads",
                        Count = db.SiteLeads.Count(l => l.Status == lNew) });
                }
            }
            return list;
        }
    }
}
