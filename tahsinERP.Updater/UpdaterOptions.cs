using System.Text.RegularExpressions;

namespace tahsinERP.Updater;

/// <summary>
/// Updater sozlamalari: appsettings.json (yonida, faqat administratorlar yozadi). Sir yo'q. Standart qiymatlar prod server
/// tuzilishiga mos (Docs/Deploy/01_DEPLOY_GUIDE.md).
/// </summary>
public sealed class UpdaterOptions
{
    /// <summary>Bazaviy domen (tahsinerp.uz) — health URL'lari va firma host'lari ({slug}.{Domain}).</summary>
    public string Domain { get; set; } = "";
    public string SqlServer { get; set; } = ".";
    public int PollSeconds { get; set; } = 3;
    /// <summary>Har ilova/firma uchun saqlanadigan zaxiralar soni.</summary>
    public int KeepBackups { get; set; } = 3;
    public int HealthTimeoutSeconds { get; set; } = 120;
    public int ProcessTimeoutMinutes { get; set; } = 30;
    public string PowerShellExe { get; set; } = "powershell.exe";
    public string DotnetExe { get; set; } = "dotnet";
    public PathsSection Paths { get; set; } = new();
    public ErpSection Erp { get; set; } = new();
    /// <summary>Server ilovalari (panel, bot, site, agent). Bo'sh maydonlar <see cref="AppProfile.Defaults"/> dan to'ldiriladi.</summary>
    public Dictionary<string, AppProfile> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public sealed class PathsSection
    {
        /// <summary>Agent bilan almashuv: Inbox\&lt;jobId&gt;\ (agent yozadi), Status\&lt;jobId&gt;.json (Updater yozadi).</summary>
        public string UpdatesDir { get; set; } = @"C:\ProgramData\tahsinERP\Updates";
        public string StateFile { get; set; } = @"C:\ProgramData\tahsinERP\Updater\state.json";
        /// <summary>Ochilgan relizlar: Releases\&lt;app&gt;\&lt;versiya&gt;\.</summary>
        public string ReleasesDir { get; set; } = @"C:\tahsinERP\releases";
        public string BackupRoot { get; set; } = @"C:\inetpub\_backup";
        /// <summary>Baza .bak zaxiralari (SQL xizmati yoza oladi — Install-Agent.ps1).</summary>
        public string DbBackupDir { get; set; } = @"C:\ProgramData\tahsinERP\Backups";
        /// <summary>Agent ishlatadigan Database papkasi (yangi firma bazasi) — eng yangi o'rnatilgan ERP versiyasi bilan sinxron.</summary>
        public string DatabaseRoot { get; set; } = @"C:\tahsinERP\Database";
        /// <summary>tenants.json va db.connection joyi (ERP pool'lari o'qiydi).</summary>
        public string DataDir { get; set; } = @"C:\ProgramData\tahsinERP";

        public string Inbox => Path.Combine(UpdatesDir, "Inbox");
        public string Status => Path.Combine(UpdatesDir, "Status");
    }

    public sealed class ErpSection
    {
        /// <summary>Firma instance'lari: InstancesRoot\&lt;slug&gt;.</summary>
        public string InstancesRoot { get; set; } = @"C:\inetpub\erp";
        /// <summary>Pool va sayt nomi: PoolPrefix + slug (erp-ipack).</summary>
        public string PoolPrefix { get; set; } = "erp-";
        /// <summary>Umumiy (wildcard) ERP sayti — fallback va HTTP→HTTPS yo'naltirish; pool env va sertifikat shundan olinadi.</summary>
        public string SharedSite { get; set; } = "tahsinERP";
        public string SharedPath { get; set; } = @"C:\inetpub\tahsinERP";
        /// <summary>Pool bo'sh turganda shuncha daqiqada uxlaydi (RAM). 0 — doim yoqiq.</summary>
        public int IdleTimeoutMinutes { get; set; } = 20;
        /// <summary>Yangi Web.config'ga eski fayldan ko'chiriladigan appSettings (machineKey doim saqlanadi).</summary>
        public List<string> PreserveAppSettings { get; set; } = ["SiteBaseDomain", "SiteCnameTarget"];
        public string HealthPath { get; set; } = "/health";
    }

    public string InstancePath(string slug) => Path.Combine(Erp.InstancesRoot, slug);
    public string InstancePool(string slug) => Erp.PoolPrefix + slug;
    public string InstanceHost(string slug) => slug + "." + Domain;

    public AppProfile App(string app)
    {
        var d = AppProfile.Defaults(app, Domain) ?? throw new UpdateRejectedException($"Noma'lum ilova: {app}");
        if (!Apps.TryGetValue(app, out var o)) return d;
        return new AppProfile
        {
            Kind = o.Kind ?? d.Kind, Path = o.Path ?? d.Path, Name = o.Name ?? d.Name, HealthUrl = o.HealthUrl ?? d.HealthUrl,
            Cli = o.Cli ?? d.Cli, CliDll = o.CliDll ?? d.CliDll, Exclude = o.Exclude ?? d.Exclude,
        };
    }

    private static readonly Regex SlugRe = new("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant);
    private static readonly Regex DbRe = new("^[A-Za-z0-9_.-]{1,128}$", RegexOptions.CultureInvariant);
    public static bool IsValidSlug(string? s) => s != null && SlugRe.IsMatch(s);
    public static bool IsValidDb(string? s) => s != null && DbRe.IsMatch(s);
}

/// <summary>Server ilovasi qanday o'rnatiladi.</summary>
public sealed class AppProfile
{
    /// <summary>"iis" (app pool) yoki "service" (Windows xizmati).</summary>
    public string? Kind { get; set; }
    public string? Path { get; set; }
    /// <summary>Pool yoki xizmat nomi.</summary>
    public string? Name { get; set; }
    /// <summary>Bo'sh — health HTTP emas (xizmat Running bo'lsa yetarli).</summary>
    public string? HealthUrl { get; set; }
    /// <summary>O'rnatilgandan keyin pool env bilan: dotnet &lt;CliDll&gt; &lt;Cli&gt; (masalan panel "migrate", bot "schema").</summary>
    public string? Cli { get; set; }
    public string? CliDll { get; set; }
    /// <summary>Ustidan yozilmaydigan fayllar (agent appsettings.json — Install-Agent.ps1 sozlaydi).</summary>
    public List<string>? Exclude { get; set; }

    public bool IsService => string.Equals(Kind, "service", StringComparison.OrdinalIgnoreCase);

    public static AppProfile? Defaults(string app, string domain) => app.ToLowerInvariant() switch
    {
        "panel" => new AppProfile
        {
            Kind = "iis", Path = @"C:\inetpub\tahsinERP.ControlPanel", Name = "tahsinERP.ControlPanel", HealthUrl = $"https://admin.{domain}/health",
            Cli = "migrate", CliDll = "tahsinERP.ControlPanel.dll", Exclude = ["appsettings.Development.json"],
        },
        "bot" => new AppProfile
        {
            Kind = "iis", Path = @"C:\inetpub\tahsinERP.Bot", Name = "tahsinERP.Bot", HealthUrl = $"https://bot.{domain}/health",
            Cli = "schema", CliDll = "tahsinERP.Bot.dll", Exclude = ["appsettings.Development.json"],
        },
        "site" => new AppProfile
        {
            Kind = "iis", Path = @"C:\inetpub\tahsinERP.Site", Name = "tahsinERP.Site", HealthUrl = $"https://api.sayt.{domain}/health",
            Cli = "schema", CliDll = "tahsinERP.Site.dll", Exclude = ["appsettings.Development.json"],
        },
        "agent" => new AppProfile
        {
            Kind = "service", Path = @"C:\tahsinERP\Agent", Name = "tahsinERP.ProvisioningAgent", Exclude = ["appsettings.json"],
        },
        _ => null,
    };
}

/// <summary>So'rov qabul qilinmadi (o'zgarish boshlanmagan): imzo, versiya, holat.</summary>
public sealed class UpdateRejectedException(string message) : Exception(message);
