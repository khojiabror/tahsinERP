using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.I18n;
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

        /// <summary>
        /// Panel ERP dasturchisining login/parolini panelda tekshirish (<see cref="PanelDeveloperLogin"/>).
        /// Baza joriy (so'rov) potokida o'qiladi — tenant HttpContext'dan; HTTP chaqiruv kontekstsiz potokda (deadlock bo'lmasin).
        /// Panel ulanmagan (URL yoki tenant_secret yo'q) bo'lsa — Unavailable.
        /// </summary>
        public static LicensePanelClient.DevLoginResult VerifyDeveloper(string login, string password, TimeSpan timeout)
        {
            if (!IsEnabled)
                return new LicensePanelClient.DevLoginResult { Status = LicensePanelClient.DevLoginStatus.Unavailable, Error = "panel_disabled" };

            string installationId, secret;
            using (var db = new DBTHSNEntities())
            {
                installationId = LicenseService.GetOrCreateInstallationId(db);
                secret = LicenseService.ReadSyncState(db, LicenseService.STATE_PANEL_SECRET);
            }
            if (!LicensePanelClient.IsValidSecret(secret))
                return new LicensePanelClient.DevLoginResult { Status = LicensePanelClient.DevLoginStatus.Unavailable, Error = "not_registered" };

            var client = Client;
            using (var cts = new CancellationTokenSource(timeout))
                return Task.Run(() => client.DevLoginAsync(installationId, secret, login, password, cts.Token)).GetAwaiter().GetResult();
        }

        public class SyncResult
        {
            public bool Ok { get; set; }
            /// <summary>Yangi kalit faollashtirildimi.</summary>
            public bool Activated { get; set; }
            public string Message { get; set; }
            /// <summary>Panel bergan onlayn tekshiruv siyosati (register/pull 200 javobida; 304 da null).</summary>
            public string OnlineCheck { get; set; }
            public int? OfflineGraceDays { get; set; }
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
                return LicenseService.Result.Fail(L.T("Core_SyncRegTokenInvalid"));
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
                return new SyncResult { Ok = false, Message = L.T("Core_SyncDisabled") };

            // HttpContext await'dan keyin yo'qolishi mumkin — tenant'ni AsyncLocal doiraga olamiz
            var tenant = TenantContext.Current;
            using (TenantContext.Use(tenant))
            {
                var policy = new SyncResult();
                var result = await SyncCoreAsync(host, policy, ct).ConfigureAwait(false);
                result.OnlineCheck = policy.OnlineCheck;
                result.OfflineGraceDays = policy.OfflineGraceDays;
                try
                {
                    using (var db = new DBTHSNEntities())
                    {
                        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                        LicenseService.WriteSyncState(db, LicenseService.STATE_LAST_SYNC, now);
                        LicenseService.WriteSyncState(db, LicenseService.STATE_LAST_SYNC_RESULT,
                            (result.Ok ? "OK: " : L.T("Core_SyncErrorPrefix") + " ") + result.Message);
                        if (result.Ok)
                            LicenseService.WriteSyncState(db, LicenseService.STATE_LAST_SYNC_OK, now);
                        // Siyosat faqat panel javob bergan bo'lsa yangilanadi (304 — o'zgarmagan, oxirgisi qoladi).
                        if (!string.IsNullOrWhiteSpace(policy.OnlineCheck))
                        {
                            LicenseService.WriteSyncState(db, LicenseService.STATE_ONLINE_CHECK, policy.OnlineCheck.Trim().ToLowerInvariant());
                            LicenseService.WriteSyncState(db, LicenseService.STATE_OFFLINE_GRACE_DAYS,
                                policy.OfflineGraceDays.HasValue ? policy.OfflineGraceDays.Value.ToString(CultureInfo.InvariantCulture) : null);
                        }
                    }
                    LicenseService.InvalidateCache();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("[LicenseSync] holatni yozib bo'lmadi: " + ex);
                }
                return result;
            }
        }

        private static async Task<SyncResult> SyncCoreAsync(string host, SyncResult policy, CancellationToken ct)
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
                    return new SyncResult { Ok = false, Message = L.T("Core_SyncNotConnected") };

                var reg = await Client.RegisterAsync(regToken, installationId, companyId, AppVersion, host, ct).ConfigureAwait(false);
                if (!reg.Ok)
                    return new SyncResult { Ok = false, Message = L.T("Core_SyncRegisterFailed", DescribeRegisterError(reg)) };

                using (var db = new DBTHSNEntities())
                {
                    LicenseService.WriteSyncState(db, LicenseService.STATE_PANEL_SECRET, reg.TenantSecret);
                    LicenseService.WriteSyncState(db, LicenseService.STATE_REG_TOKEN, null);
                }
                secret = reg.TenantSecret;
                offeredKey = reg.LicenseKey;
                policy.OnlineCheck = reg.OnlineCheck;
                policy.OfflineGraceDays = reg.OfflineGraceDays;
            }

            if (string.IsNullOrWhiteSpace(offeredKey))
            {
                var pull = await Client.PullAsync(installationId, secret, currentKey, ct).ConfigureAwait(false);
                policy.OnlineCheck = pull.OnlineCheck;
                policy.OfflineGraceDays = pull.OfflineGraceDays;
                switch (pull.Status)
                {
                    case LicensePanelClient.PullStatus.NotModified:
                        return new SyncResult { Ok = true, Message = L.T("Core_SyncKeyUnchanged") };
                    case LicensePanelClient.PullStatus.Updated:
                        offeredKey = pull.LicenseKey;
                        break;
                    case LicensePanelClient.PullStatus.Unauthorized:
                        return new SyncResult { Ok = false, Message = L.T("Core_SyncUnauthorized", pull.Error) };
                    case LicensePanelClient.PullStatus.NotFound:
                        return new SyncResult { Ok = false, Message = L.T("Core_SyncNotFound") };
                    case LicensePanelClient.PullStatus.Revoked:
                        return new SyncResult { Ok = false, Message = L.T("Core_SyncRevoked") };
                    default:
                        return new SyncResult { Ok = false, Message = pull.Error ?? L.T("Core_SyncUnknownError") };
                }
            }

            return ActivateIfNew(offeredKey, currentKey);
        }

        private static SyncResult ActivateIfNew(string offeredKey, string currentKey)
        {
            if (currentKey != null && string.Equals(currentKey.Trim(), offeredKey.Trim(), StringComparison.Ordinal))
                return new SyncResult { Ok = true, Message = L.T("Core_SyncKeyUnchanged") };

            using (var db = new DBTHSNEntities())
            {
                var r = LicenseService.ActivateCompanyLicense(db, offeredKey, SyncUser);
                return r.Ok
                    ? new SyncResult { Ok = true, Activated = true, Message = L.T("Core_SyncKeyActivated") }
                    : new SyncResult { Ok = false, Message = L.T("Core_SyncKeyRejected", r.Message) };
            }
        }

        private static string DescribeRegisterError(LicensePanelClient.RegisterResult reg)
        {
            switch (reg.Error)
            {
                case "token_not_found": return L.T("Core_SyncTokenNotFound");
                case "token_used": return L.T("Core_SyncTokenUsed");
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
