using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace tahsinERP.Configuration.Tenancy
{
    /// <summary>Bitta tashkilot (tenant) — o'z bazasi bilan.</summary>
    public sealed class TenantInfo
    {
        /// <summary>Subdomen: firma1 → firma1.&lt;bazaviy domen&gt;. Kichik harf, kesh kaliti ham shu.</summary>
        public string Slug { get; set; }

        /// <summary>Tenant bazasi (Initial Catalog), masalan DB_firma1.</summary>
        public string Database { get; set; }

        /// <summary>Kalitdagi CompanyID. Berilmasa Web.config'dagi companyID ishlatiladi.</summary>
        public int? CompanyId { get; set; }

        /// <summary>Qo'shimcha to'liq host nomlari (mijozning o'z domeni), ixtiyoriy.</summary>
        public string[] Hosts { get; set; }

        /// <summary>
        /// Ixtiyoriy: boshqa SQL serverdagi tenant uchun to'liq connection string.
        /// Berilmasa umumiy (DbConfig) connection string olinib, faqat Initial Catalog almashtiriladi.
        /// </summary>
        public string ConnectionString { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>Kesh/HMAC/ticket uchun barqaror kalit.</summary>
        [JsonIgnore]
        public string Key { get { return Slug; } }

        // DbConfig hisoblagan EntityConnection string (reestr qayta yuklansa obyekt yangilanadi — kesh ham)
        [JsonIgnore]
        internal string CachedEntityConnectionString;

        public override string ToString() { return Slug + " → " + Database; }
    }

    /// <summary>
    /// Tenant reestri: slug → baza. ERP kodida EMAS — serverdagi JSON faylda (git'ga kirmaydi,
    /// keyinchalik markaziy panelning provisioning agenti to'ldiradi).
    ///
    /// Fayl: TAHSINERP_TENANTS_FILE env yoki %ProgramData%\tahsinERP\tenants.json
    /// <code>
    /// {
    ///   "baseDomain": "tahsinerp.uz",
    ///   "tenants": [
    ///     { "slug": "firma1", "database": "DB_firma1", "companyId": 1 },
    ///     { "slug": "firma2", "database": "DB_firma2", "hosts": ["erp.firma2.uz"] }
    ///   ]
    /// }
    /// </code>
    /// Bazaviy domen tartibi: TAHSINERP_BASE_DOMAIN env → fayldagi baseDomain → Web.config TenantBaseDomain.
    ///
    /// Fayl yo'q yoki tenantlar ro'yxati bo'sh — reestr NOFAOL: ilova avvalgidek bitta baza bilan ishlaydi.
    /// Fayl buzilgan bo'lsa — reestr "Broken": so'rovlar 503 bilan rad etiladi (bitta-baza rejimiga
    /// tushib qolish boshqa tenantlarning so'rovini umumiy bazaga yo'naltirib yuborardi).
    /// </summary>
    public sealed class TenantRegistry
    {
        public const string EnvTenantsFile = "TAHSINERP_TENANTS_FILE";
        public const string EnvBaseDomain = "TAHSINERP_BASE_DOMAIN";
        public const string AppSettingBaseDomain = "TenantBaseDomain";

        public static readonly string DefaultTenantsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "tahsinERP", "tenants.json");

        private static readonly Regex SlugRegex = new Regex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.Compiled);
        /// <summary>
        /// Domen bitta: korxonalar ham, markaziy panel ham shu domen ostida (admin./my./pay.) — bu nomlar
        /// tenant slug bo'la olmaydi, aks holda panel subdomeni ERP tenantiga tushib qolardi.
        /// </summary>
        public static readonly string[] ReservedSlugs =
        {
            "admin", "my", "pay", "www", "api", "app", "mail", "smtp", "static", "cdn", "assets", "status", "panel", "erp"
        };

        private static readonly Regex DatabaseRegex = new Regex(@"^[A-Za-z0-9_\-\.]{1,128}$", RegexOptions.Compiled);

        private readonly Dictionary<string, TenantInfo> _bySlug;
        private readonly Dictionary<string, TenantInfo> _byHost;

        public static readonly TenantRegistry Empty = new TenantRegistry(null, new List<TenantInfo>(), null);

        public string BaseDomain { get; private set; }
        public IReadOnlyList<TenantInfo> Tenants { get; private set; }

        /// <summary>Fayl o'qilmadi/buzilgan — so'rovlar rad etiladi.</summary>
        public string Error { get; private set; }
        public bool IsBroken { get { return Error != null; } }

        /// <summary>Ko'p-tenant rejimi yoqilganmi (kamida bitta tenant bor yoki reestr buzilgan).</summary>
        public bool IsActive { get { return IsBroken || Tenants.Count > 0; } }

        private TenantRegistry(string baseDomain, List<TenantInfo> tenants, string error)
        {
            BaseDomain = NormalizeHost(baseDomain);
            Tenants = tenants.AsReadOnly();
            Error = error;
            _bySlug = new Dictionary<string, TenantInfo>(StringComparer.Ordinal);
            _byHost = new Dictionary<string, TenantInfo>(StringComparer.Ordinal);
            foreach (var t in tenants)
            {
                _bySlug[t.Slug] = t;
                foreach (var h in t.Hosts ?? new string[0])
                {
                    string host = NormalizeHost(h);
                    if (host != null)
                        _byHost[host] = t;
                }
            }
        }

        public static TenantRegistry CreateBroken(string error)
        {
            return new TenantRegistry(null, new List<TenantInfo>(), error ?? "Tenant reestri o'qilmadi.");
        }

        public TenantInfo FindBySlug(string slug)
        {
            TenantInfo t;
            return slug != null && _bySlug.TryGetValue(slug.Trim().ToLowerInvariant(), out t) && t.Enabled ? t : null;
        }

        /// <summary>
        /// So'rov host'idan tenant'ni topadi: avval aniq host (hosts ro'yxati), keyin
        /// &lt;slug&gt;.&lt;baseDomain&gt;. Faqat bitta daraja subdomen qabul qilinadi.
        /// </summary>
        public TenantInfo ResolveHost(string host)
        {
            host = NormalizeHost(host);
            if (host == null)
                return null;

            TenantInfo t;
            if (_byHost.TryGetValue(host, out t))
                return t.Enabled ? t : null;

            if (string.IsNullOrEmpty(BaseDomain) || !host.EndsWith("." + BaseDomain, StringComparison.Ordinal))
                return null;

            string label = host.Substring(0, host.Length - BaseDomain.Length - 1);
            if (label.Length == 0 || label.IndexOf('.') >= 0)
                return null;
            return FindBySlug(label);
        }

        /// <summary>Host'ni kichik harf, portsiz, oxirgi nuqtasiz ko'rinishga keltiradi.</summary>
        public static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return null;
            host = host.Trim().ToLowerInvariant();
            int colon = host.IndexOf(':');
            if (colon >= 0 && host.IndexOf(']') < 0)
                host = host.Substring(0, colon);
            host = host.TrimEnd('.');
            return host.Length == 0 ? null : host;
        }

        #region JSON

        private sealed class RegistryFile
        {
            public string BaseDomain { get; set; }
            public List<TenantInfo> Tenants { get; set; }
        }

        /// <summary>
        /// JSON matnidan reestr quradi. Xato bo'lsa (sintaksis, takroriy slug, yaroqsiz baza nomi)
        /// — <see cref="CreateBroken"/> qaytaradi, istisno otmaydi.
        /// </summary>
        public static TenantRegistry Parse(string json, string baseDomainOverride)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new TenantRegistry(baseDomainOverride, new List<TenantInfo>(), null);

            RegistryFile file;
            try
            {
                file = JsonConvert.DeserializeObject<RegistryFile>(json);
            }
            catch (JsonException ex)
            {
                return CreateBroken("tenants.json o'qilmadi: " + ex.Message);
            }
            if (file == null)
                return new TenantRegistry(baseDomainOverride, new List<TenantInfo>(), null);

            var list = new List<TenantInfo>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in file.Tenants ?? new List<TenantInfo>())
            {
                if (t == null)
                    continue;
                string slug = (t.Slug ?? "").Trim().ToLowerInvariant();
                if (!SlugRegex.IsMatch(slug))
                    return CreateBroken("Yaroqsiz slug: '" + t.Slug + "'");
                if (Array.IndexOf(ReservedSlugs, slug) >= 0)
                    return CreateBroken("Band (rezerv) slug: '" + slug + "' — panel subdomenlari uchun ajratilgan.");
                if (!seen.Add(slug))
                    return CreateBroken("Takroriy slug: '" + slug + "'");
                string db = (t.Database ?? "").Trim();
                if (!DatabaseRegex.IsMatch(db))
                    return CreateBroken("'" + slug + "' uchun baza nomi yaroqsiz: '" + t.Database + "'");
                t.Slug = slug;
                t.Database = db;
                list.Add(t);
            }

            string baseDomain = !string.IsNullOrWhiteSpace(baseDomainOverride) ? baseDomainOverride : file.BaseDomain;
            if (list.Count > 0 && string.IsNullOrWhiteSpace(baseDomain) && !list.Any(x => x.Hosts != null && x.Hosts.Length > 0))
                return CreateBroken("Bazaviy domen berilmagan (baseDomain / " + EnvBaseDomain + ").");
            return new TenantRegistry(baseDomain, list, null);
        }

        #endregion

        #region Joriy reestr (fayldan, o'zgarsa qayta yuklanadi)

        private static readonly object _loadLock = new object();
        private static TenantRegistry _current;
        private static DateTime _lastCheckUtc = DateTime.MinValue;
        private static DateTime _fileStampUtc = DateTime.MinValue;
        private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(15);

        /// <summary>Testlar uchun: reestrni qo'lda o'rnatish (null — fayldan qayta o'qish).</summary>
        internal static TenantRegistry Override { get; set; }

        /// <summary>
        /// Joriy reestr. Fayl 15 soniyada bir marta tekshiriladi — panel yangi tenant qo'shsa
        /// ilovani qayta ishga tushirish shart emas.
        /// </summary>
        public static TenantRegistry Current
        {
            get
            {
                var over = Override;
                if (over != null)
                    return over;

                var cur = _current;
                if (cur != null && DateTime.UtcNow - _lastCheckUtc < RecheckInterval)
                    return cur;

                lock (_loadLock)
                {
                    if (_current != null && DateTime.UtcNow - _lastCheckUtc < RecheckInterval)
                        return _current;
                    _current = LoadFromDisk(_current);
                    _lastCheckUtc = DateTime.UtcNow;
                    return _current;
                }
            }
        }

        public static string TenantsFilePath
        {
            get
            {
                string env = Environment.GetEnvironmentVariable(EnvTenantsFile);
                return string.IsNullOrWhiteSpace(env) ? DefaultTenantsFile : env.Trim();
            }
        }

        private static string ConfiguredBaseDomain()
        {
            string env = Environment.GetEnvironmentVariable(EnvBaseDomain);
            if (!string.IsNullOrWhiteSpace(env))
                return env;
            try { return ConfigurationManager.AppSettings[AppSettingBaseDomain]; }
            catch (ConfigurationErrorsException) { return null; }
        }

        private static TenantRegistry LoadFromDisk(TenantRegistry previous)
        {
            string path = TenantsFilePath;
            try
            {
                if (!File.Exists(path))
                {
                    // Ilgari faol bo'lgan reestr fayli yo'qolsa — oxirgi holatni saqlaymiz
                    // (aks holda barcha tenant so'rovlari umumiy bazaga tushib qolardi).
                    if (previous != null && previous.IsActive)
                    {
                        Trace.TraceError("[Tenancy] " + path + " topilmadi — oxirgi reestr saqlanmoqda.");
                        return previous;
                    }
                    _fileStampUtc = DateTime.MinValue;
                    return Empty;
                }

                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (previous != null && stamp == _fileStampUtc)
                    return previous;

                var parsed = Parse(File.ReadAllText(path), ConfiguredBaseDomain());
                if (parsed.IsBroken)
                {
                    Trace.TraceError("[Tenancy] " + parsed.Error);
                    // Ishlab turgan reestr bo'lsa — uni saqlaymiz, faylni tuzatishgacha
                    if (previous != null && previous.IsActive && !previous.IsBroken)
                        return previous;
                }
                _fileStampUtc = stamp;
                return parsed;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.TraceError("[Tenancy] tenants.json o'qilmadi: " + ex.Message);
                return previous != null && previous.IsActive ? previous : CreateBroken(ex.Message);
            }
        }

        #endregion
    }
}
