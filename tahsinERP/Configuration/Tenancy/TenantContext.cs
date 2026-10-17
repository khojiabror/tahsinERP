using System;
using System.Threading;
using System.Web;

namespace tahsinERP.Configuration.Tenancy
{
    /// <summary>Ko'p-tenant rejimida tenant aniqlanmagan joyda bazaga murojaat qilindi.</summary>
    public class TenantNotResolvedException : InvalidOperationException
    {
        public TenantNotResolvedException(string message) : base(message) { }
    }

    /// <summary>
    /// Joriy tenant (so'rov yoki fon ishi uchun).
    ///
    /// Manba tartibi:
    ///   1) <see cref="Use"/> bilan ochilgan aniq doira (fon ishlari, LicenseSyncService) — AsyncLocal,
    ///      await va Task.Run ichiga ham o'tadi;
    ///   2) HttpContext.Current.Items — Application_BeginRequest host bo'yicha o'rnatadi.
    ///
    /// Reestr nofaol (bitta-baza rejimi) bo'lsa — har doim null, hamma narsa avvalgidek.
    /// Reestr faol, lekin tenant topilmasa — <see cref="Current"/> istisno otadi: noto'g'ri
    /// (umumiy) bazaga jimgina ulanishdan ko'ra xato yaxshiroq.
    ///
    /// Fon ishi uchun: <c>var t = TenantContext.Current; Task.Run(() => { using (TenantContext.Use(t)) { ... } });</c>
    /// </summary>
    public static class TenantContext
    {
        private const string ItemsKey = "tahsinERP.Tenant";

        private static readonly AsyncLocal<TenantInfo> _scoped = new AsyncLocal<TenantInfo>();

        /// <summary>Ko'p-tenant rejimi yoqilganmi.</summary>
        public static bool IsMultiTenant
        {
            get { return TenantRegistry.Current.IsActive; }
        }

        /// <summary>
        /// Joriy tenant. Bitta-baza rejimida null. Ko'p-tenant rejimida tenant aniqlanmagan
        /// bo'lsa <see cref="TenantNotResolvedException"/>.
        /// </summary>
        public static TenantInfo Current
        {
            get
            {
                var t = CurrentOrNull;
                if (t == null && IsMultiTenant)
                    throw new TenantNotResolvedException(
                        "Tenant aniqlanmagan (HttpContext yo'q yoki host reestrda yo'q). Fon ishida TenantContext.Use(...) ishlating.");
                return t;
            }
        }

        /// <summary>Joriy tenant yoki null (istisnosiz).</summary>
        public static TenantInfo CurrentOrNull
        {
            get
            {
                var scoped = _scoped.Value;
                if (scoped != null)
                    return scoped;
                var ctx = HttpContext.Current;
                return ctx != null ? ctx.Items[ItemsKey] as TenantInfo : null;
            }
        }

        /// <summary>So'rov uchun tenant'ni o'rnatadi (Application_BeginRequest).</summary>
        public static void SetForRequest(HttpContext context, TenantInfo tenant)
        {
            if (context != null)
                context.Items[ItemsKey] = tenant;
        }

        /// <summary>
        /// Shu doira (va undan boshlangan await/Task'lar) uchun tenant'ni belgilaydi.
        /// Dispose qilinganda avvalgi qiymat tiklanadi. null berilsa — bitta-baza rejimidagi kabi.
        /// </summary>
        public static IDisposable Use(TenantInfo tenant)
        {
            var previous = _scoped.Value;
            _scoped.Value = tenant;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly TenantInfo _previous;
            private bool _disposed;
            public Scope(TenantInfo previous) { _previous = previous; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _scoped.Value = _previous;
            }
        }

        /// <summary>
        /// Xotiradagi kesh kaliti: bitta-baza rejimida o'zgarmaydi, aks holda "t:&lt;slug&gt;:" prefiksi.
        /// Barcha umumiy keshlar (MemoryCache, HttpRuntime.Cache, statik lug'atlar) shu orqali o'tishi SHART —
        /// aks holda bir tenantning ma'lumoti boshqasiga ko'rinadi.
        /// </summary>
        public static string CacheKey(string key)
        {
            return CacheKeyPrefix + key;
        }

        /// <summary>"" (bitta-baza) yoki "t:&lt;slug&gt;:".</summary>
        public static string CacheKeyPrefix
        {
            get
            {
                var t = Current;
                return t == null ? string.Empty : "t:" + t.Key + ":";
            }
        }
    }
}
