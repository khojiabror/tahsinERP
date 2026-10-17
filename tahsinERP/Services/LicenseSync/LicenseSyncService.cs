using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Licensing;
using tahsinERP.Models;

namespace tahsinERP.Services.LicenseSync
{
    /// <summary>
    /// Markaziy panel bilan litsenziya sinxronlash: bir martalik ro'yxatdan o'tish (reg_token) va
    /// har 6 soatda joriy kalitni olish. Kontrakt: Docs/ControlPanel/01_LICENSE_API.md.
    ///
    /// Panel URL bo'sh bo'lsa (on-prem offline) — xizmat o'chiq, hech narsa qilmaydi.
    /// Kelgan kalit FAQAT <see cref="LicenseService.ActivateCompanyLicense"/> orqali saqlanadi —
    /// imzo, CompanyID, InstallationId, muddat va soat muhri tekshiruvlari o'z-o'zidan ishlaydi.
    /// InstallationId tashqaridan yozilmaydi: <see cref="LicenseService.GetOrCreateInstallationId"/>.
    /// </summary>
    public static class LicenseSyncService
    {
        public const string AppSettingPanelUrl = "LicensePanelUrl";
        public const string EnvPanelUrl = "TAHSINERP_LICENSE_PANEL_URL";
        public const string SyncUser = "panel-sync";

        private static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2);

        private static Timer _timer;
        private static int _running;
        private static LicensePanelClient _client;
        private static readonly object _clientLock = new object();

        /// <summary>Testlar uchun: soxta panel klienti (null — sozlamadan).</summary>
        internal static LicensePanelClient ClientOverride { get; set; }

        public static string PanelUrl
        {
            get
            {
                string env = Environment.GetEnvironmentVariable(EnvPanelUrl);
                if (!string.IsNullOrWhiteSpace(env))
                    return env.Trim();
                try { return (ConfigurationManager.AppSettings[AppSettingPanelUrl] ?? "").Trim(); }
                catch (ConfigurationErrorsException) { return ""; }
            }
        }

        /// <summary>Panel URL berilganmi (aks holda offline rejim).</summary>
        public static bool IsEnabled
        {
            get { return ClientOverride != null || !string.IsNullOrWhiteSpace(PanelUrl); }
        }

        private static LicensePanelClient Client
        {
            get
            {
                if (ClientOverride != null)
                    return ClientOverride;
                lock (_clientLock)
                {
                    if (_client == null)
                        _client = new LicensePanelClient(PanelUrl);
                    return _client;
                }
            }
        }

        public class SyncResult
        {
            public bool Ok { get; set; }
            /// <summary>Yangi kalit faollashtirildimi.</summary>
            public bool Activated { get; set; }
            public string Message { get; set; }
        }

        #region Taymer

        /// <summary>Application_Start'dan chaqiriladi. Panel URL bo'sh bo'lsa hech narsa qilmaydi.</summary>
        public static void Start()
        {
            if (!IsEnabled)
                return;
            try { LicensePanelClient.ParseBaseUrl(PanelUrl); }
            catch (ArgumentException ex)
            {
                Trace.TraceError("[LicenseSync] " + ex.Message + " — sinxronlash o'chiq.");
                return;
            }
            _timer = new Timer(_ => OnTimer(), null, FirstRunDelay, DefaultInterval);
        }

        private static void OnTimer()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return; // oldingi aylanish hali tugamagan
            try
            {
                var registry = TenantRegistry.Current;
                if (registry.IsBroken)
                    return;
                if (!registry.IsActive)
                {
                    RunSafe(null);
                    return;
                }
                foreach (var tenant in registry.Tenants.Where(t => t.Enabled))
                    RunSafe(tenant);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private static void RunSafe(TenantInfo tenant)
        {
            try
            {
                using (TenantContext.Use(tenant))
                {
                    var r = SyncNowAsync(null).GetAwaiter().GetResult();
                    if (!r.Ok)
                        Trace.TraceWarning("[LicenseSync] " + (tenant != null ? tenant.Slug + ": " : "") + r.Message);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[LicenseSync] " + (tenant != null ? tenant.Slug + ": " : "") + ex);
            }
        }

        #endregion

        #region Sinxronlash

        /// <summary>Admin License sahifasida reg_token kiritdi — saqlaydi (keyin <see cref="SyncNowAsync"/>).</summary>
        public static LicenseService.Result SaveRegToken(DBTHSNEntities db, string regToken)
        {
            regToken = (regToken ?? "").Trim();
            if (regToken.Length < 8 || regToken.Length > 200)
                return LicenseService.Result.Fail("Ro'yxat tokeni yaroqsiz.");
            LicenseService.WriteSyncState(db, LicenseService.STATE_REG_TOKEN, regToken);
            return LicenseService.Result.Success();
        }

        /// <summary>
        /// Bir aylanish: secret yo'q va RegToken bor bo'lsa — register; secret bor bo'lsa — pull.
        /// Joriy tenant (so'rov yoki fon doirasi) saqlanadi — await'lar ConfigureAwait(false) bo'lsa ham.
        /// </summary>
        public static async Task<SyncResult> SyncNowAsync(string host, CancellationToken ct = default(CancellationToken))
        {
            if (!IsEnabled)
                return new SyncResult { Ok = false, Message = "Panel bilan sinxronlash o'chiq (LicensePanelUrl berilmagan)." };

            // HttpContext await'dan keyin yo'qolishi mumkin — tenant'ni AsyncLocal doiraga olamiz
            var tenant = TenantContext.Current;
            using (TenantContext.Use(tenant))
            {
                var result = await SyncCoreAsync(host, ct).ConfigureAwait(false);
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        LicenseService.WriteSyncState(db, LicenseService.STATE_LAST_SYNC,
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                        LicenseService.WriteSyncState(db, LicenseService.STATE_LAST_SYNC_RESULT,
                            (result.Ok ? "OK: " : "XATO: ") + result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[LicenseSync] holatni yozib bo'lmadi: " + ex);
                }
                return result;
            }
        }

        private static async Task<SyncResult> SyncCoreAsync(string host, CancellationToken ct)
        {
            string installationId, secret, regToken, currentKey;
            int companyId = LicenseService.InstallationCompanyId;
            using (var db = new DBTHSNEntities())
            {
                installationId = LicenseService.GetOrCreateInstallationId(db);
                secret = LicenseService.ReadSyncState(db, LicenseService.STATE_PANEL_SECRET);
                regToken = LicenseService.ReadSyncState(db, LicenseService.STATE_REG_TOKEN);
                LicenseHelper.LicenseInfo ignored;
                var active = LicenseService.GetActiveCompanyLicense(db, out ignored);
                currentKey = active != null ? active.LicenseKey : null;
            }

            string offeredKey = null;
            if (!LicensePanelClient.IsValidSecret(secret))
            {
                if (string.IsNullOrWhiteSpace(regToken))
                    return new SyncResult { Ok = false, Message = "Panelga ulanmagan: ro'yxat tokenini (reg_token) kiriting." };

                var reg = await Client.RegisterAsync(regToken, installationId, companyId, AppVersion, host, ct).ConfigureAwait(false);
                if (!reg.Ok)
                    return new SyncResult { Ok = false, Message = "Ro'yxatdan o'tmadi: " + DescribeRegisterError(reg) };

                using (var db = new DBTHSNEntities())
                {
                    LicenseService.WriteSyncState(db, LicenseService.STATE_PANEL_SECRET, reg.TenantSecret);
                    LicenseService.WriteSyncState(db, LicenseService.STATE_REG_TOKEN, null);
                }
                secret = reg.TenantSecret;
                offeredKey = reg.LicenseKey;
            }

            if (string.IsNullOrWhiteSpace(offeredKey))
            {
                var pull = await Client.PullAsync(installationId, secret, currentKey, ct).ConfigureAwait(false);
                switch (pull.Status)
                {
                    case LicensePanelClient.PullStatus.NotModified:
                        return new SyncResult { Ok = true, Message = "Kalit o'zgarmagan." };
                    case LicensePanelClient.PullStatus.Updated:
                        offeredKey = pull.LicenseKey;
                        break;
                    case LicensePanelClient.PullStatus.Unauthorized:
                        return new SyncResult { Ok = false, Message = "Panel so'rovni rad etdi (imzo/soat): " + pull.Error };
                    case LicensePanelClient.PullStatus.NotFound:
                        return new SyncResult { Ok = false, Message = "Panel bu o'rnatmani tanimaydi." };
                    case LicensePanelClient.PullStatus.Revoked:
                        return new SyncResult { Ok = false, Message = "Panelda obuna bekor qilingan." };
                    default:
                        return new SyncResult { Ok = false, Message = pull.Error ?? "Noma'lum xato." };
                }
            }

            return ActivateIfNew(offeredKey, currentKey);
        }

        private static SyncResult ActivateIfNew(string offeredKey, string currentKey)
        {
            if (currentKey != null && string.Equals(currentKey.Trim(), offeredKey.Trim(), StringComparison.Ordinal))
                return new SyncResult { Ok = true, Message = "Kalit o'zgarmagan." };

            using (var db = new DBTHSNEntities())
            {
                var r = LicenseService.ActivateCompanyLicense(db, offeredKey, SyncUser);
                return r.Ok
                    ? new SyncResult { Ok = true, Activated = true, Message = "Paneldan yangi kalit olindi va faollashtirildi." }
                    : new SyncResult { Ok = false, Message = "Paneldan kelgan kalit rad etildi: " + r.Message };
            }
        }

        private static string DescribeRegisterError(LicensePanelClient.RegisterResult reg)
        {
            switch (reg.Error)
            {
                case "token_not_found": return "token topilmadi yoki muddati o'tgan.";
                case "token_used": return "token boshqa o'rnatmada ishlatilgan.";
                default: return reg.Error;
            }
        }

        private static string AppVersion
        {
            get { return typeof(LicenseSyncService).Assembly.GetName().Version.ToString(); }
        }

        #endregion
    }
}
