namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Agent sozlamalari: appsettings.json (yonida) + env <c>TAHSIN_AGENT_</c> prefiksi bilan (masalan <c>TAHSIN_AGENT_Panel__Secret</c>).
/// Sirlar (Panel:Secret, Sql:Password, Demo:UserPassword) faqat env/user-secrets'da.
/// </summary>
public sealed class AgentOptions
{
    public PanelSection Panel { get; set; } = new();
    public SqlSection Sql { get; set; } = new();
    public PathsSection Paths { get; set; } = new();
    public DemoSection Demo { get; set; } = new();
    public PowerShellSection PowerShell { get; set; } = new();
    public UsageSection Usage { get; set; } = new();
    public UpdatesSection Updates { get; set; } = new();

    /// <summary>Avto-yangilash (Docs/Deploy/03_AUTO_UPDATE.md): paketni paneldan yuklab Updater inbox'iga qo'yish, inventar.</summary>
    public sealed class UpdatesSection
    {
        public bool Enabled { get; set; } = true;
        /// <summary>Updater bilan almashuv papkasi (Inbox\ — agent yozadi, Status\ — Updater).</summary>
        public string UpdatesDir { get; set; } = @"C:\ProgramData\tahsinERP\Updates";
        public string UpdaterStateFile { get; set; } = @"C:\ProgramData\tahsinERP\Updater\state.json";
        /// <summary>Kunlik zaxira natijasi (Backup-Databases.ps1 yozadi) — inventar bilan panelga yuboriladi.</summary>
        public string BackupStatusFile { get; set; } = @"C:\ProgramData\tahsinERP\Updates\Status\backup.json";
        /// <summary>Inventar (o'rnatilgan versiyalar) panelga shuncha daqiqada bir yuboriladi.</summary>
        public int InventoryMinutes { get; set; } = 5;
        /// <summary>Updater shuncha vaqtda status yozmasa — o'rnatilmagan/ishlamayapti deb hisoblanadi.</summary>
        public int UpdaterStartTimeoutSeconds { get; set; } = 120;
        public int JobTimeoutMinutes { get; set; } = 90;
    }

    /// <summary>Foydalanuvchi faolligini yig'ish (05_PROVISIONING.md, 10). Qaysi tenant va qachon — panel hal qiladi.</summary>
    public sealed class UsageSection
    {
        public bool Enabled { get; set; } = true;
        /// <summary>Paneldan maqsadlar shuncha daqiqada bir so'raladi.</summary>
        public int CheckMinutes { get; set; } = 2;
        /// <summary>Bir yig'ishda har jadvaldan ko'pi bilan shuncha yozuv (qolgani keyingi safar).</summary>
        public int BatchLimit { get; set; } = 2000;
        /// <summary>Birinchi yig'ishda shuncha kunlik tarix (panel saqlash muddati bilan bir xil).</summary>
        public int LookbackDays { get; set; } = 90;
        /// <summary>USERLOGS.ActionName matni (maskalangan) yuborilsinmi. false — faqat modul va vaqt.</summary>
        public bool IncludeActionText { get; set; } = true;
        /// <summary>Biznes hajmi (hujjatlar soni) shuncha kun orqaga har safar qayta hisoblanadi (orqa sana bilan kiritilganlar uchun).</summary>
        public int DocsWindowDays { get; set; } = 35;
    }

    public sealed class PanelSection
    {
        /// <summary>Panel manzili, masalan https://admin.&lt;domen&gt;.</summary>
        public string Url { get; set; } = "";
        /// <summary>Panel Servers.Id (Serverlar → server sahifasi).</summary>
        public int ServerId { get; set; }
        /// <summary>Panel chiqargan agent siri (Base64, 32 bayt). Faqat env.</summary>
        public string Secret { get; set; } = "";
        public int PollSeconds { get; set; } = 15;
        public int TimeoutSeconds { get; set; } = 60;
    }

    public sealed class SqlSection
    {
        /// <summary>Lokal SQL instance. Panel yuborgan SqlInstance faqat log uchun — agent doim shu yerga ulanadi.</summary>
        public string Server { get; set; } = ".";
        /// <summary>Bo'sh — Windows auth (xizmat hisobi: dbcreator + bazalarda db_owner).</summary>
        public string? User { get; set; }
        public string? Password { get; set; }
        /// <summary>
        /// ERP ilovasining SQL login'i (masalan <c>IIS APPPOOL\tahsinERP</c>). Berilsa — har yaratilgan/tiklangan tenant bazasida
        /// shu login uchun foydalanuvchi ochiladi va db_owner beriladi (ERP ish vaqtida DDL ham bajaradi). Login'ning o'zini
        /// server administratori bir marta yaratadi (Install-Erp.ps1) — agent hisobida ALTER ANY LOGIN yo'q.
        /// Bo'sh — huquq berilmaydi (ERP sysadmin bilan ishlasa yoki qo'lda beriladi).
        /// </summary>
        public string? AppLogin { get; set; }
    }

    public sealed class PathsSection
    {
        /// <summary>tahsinERP/Database papkasi (ERP release bilan bir xil versiya): tools/New-TenantDb.ps1, Invoke-DbMigrations.ps1.</summary>
        public string DatabaseRoot { get; set; } = "";
        /// <summary>ERP reestri. Bo'sh — TAHSINERP_TENANTS_FILE env yoki %ProgramData%\tahsinERP\tenants.json (ERP bilan bir xil).</summary>
        public string? TenantsFile { get; set; }
        /// <summary>Bazaviy domen (fayl yangi bo'lsa yoki baseDomain yo'q bo'lsa yoziladi). Domen hali tanlanmagan.</summary>
        public string? BaseDomain { get; set; }
        /// <summary>SQL Server xizmati yoza oladigan papka: vaqtinchalik .bak va demo shabloni.</summary>
        public string BackupDir { get; set; } = @"C:\ProgramData\tahsinERP\Backups";
        /// <summary>Arxiv (LocalBackupStore ildizi). Keyinroq S3BackupStore — IBackupStore orqali.</summary>
        public string ArchiveDir { get; set; } = @"C:\ProgramData\tahsinERP\Archive";
        /// <summary>Job loglari va agent holati (demo reset sanasi).</summary>
        public string StateDir { get; set; } = @"C:\ProgramData\tahsinERP\Agent";
    }

    /// <summary>
    /// Umumiy ko'rgazma demo tenant (har kecha shablondan tiklanadi). 2026-10-26 dan standart holatda O'CHIQ: mijozlar shaxsiy
    /// sinov muhitida (tanlangan modullar + namuna ma'lumot) ishlaydi, prod'dagi demo.tahsinerp.uz olib tashlandi
    /// (05_PROVISIONING.md 6). Qayta yoqish: Demo:Enabled = true, Demo:Slug va panel Provisioning:DemoSlug bir xil.
    /// </summary>
    public sealed class DemoSection
    {
        public bool Enabled { get; set; }
        public string Slug { get; set; } = "";
        /// <summary>Demo shablon zaxirasi. Yo'q bo'lsa Reset vaqtida Database/Demo skriptlaridan yaratiladi.</summary>
        public string? TemplateBak { get; set; }
        /// <summary>Kechki reset soati (TimeZone bo'yicha) va oyna (shu soatdan keyin necha soat ichida — agent o'chiq bo'lsa kunduzi reset qilinmasin).</summary>
        public int ResetHour { get; set; } = 3;
        public int ResetWindowHours { get; set; } = 3;
        public string TimeZone { get; set; } = "Asia/Tashkent";
        /// <summary>Login sahifasida ko'rsatiladigan demo foydalanuvchi (parol almashtirish majburiy EMAS — har kecha tiklanadi).</summary>
        public string UserLogin { get; set; } = "demo";
        public string UserPassword { get; set; } = "";
    }

    public sealed class PowerShellSection
    {
        public string Exe { get; set; } = "powershell.exe";
        public int TimeoutMinutes { get; set; } = 30;
    }

    public string ResolveTenantsFile()
    {
        if (!string.IsNullOrWhiteSpace(Paths.TenantsFile)) return Paths.TenantsFile!;
        string? env = Environment.GetEnvironmentVariable("TAHSINERP_TENANTS_FILE");
        return string.IsNullOrWhiteSpace(env)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "tahsinERP", "tenants.json")
            : env.Trim();
    }

    public string ResolveDemoTemplate() =>
        string.IsNullOrWhiteSpace(Demo.TemplateBak) ? Path.Combine(Paths.BackupDir, "DB_DEMO_TEMPLATE.bak") : Demo.TemplateBak!;
}
