using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Pages.Updates;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.Updater;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Test relizi: vaqtinchalik ECDSA kalit bilan imzolangan manifest + ZIP'lar.</summary>
public sealed class TestReleaseKit
{
    public const string KeyId = "t1";
    private readonly string _privatePem;
    public IReadOnlyDictionary<string, string> Trusted { get; }

    public TestReleaseKit()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _privatePem = ec.ExportPkcs8PrivateKeyPem();
        Trusted = new Dictionary<string, string> { [KeyId] = ec.ExportSubjectPublicKeyInfoPem() };
    }

    /// <summary>dir ichida &lt;app&gt;.zip (files: nisbiy yo'l → matn), manifest.json va manifest.sig yaratadi.</summary>
    public ReleaseManifest Build(string dir, string version, Dictionary<string, Dictionary<string, string>> apps, ISet<string>? changed = null)
    {
        Directory.CreateDirectory(dir);
        var m = new ReleaseManifest { Version = version, Commit = "abcdef1234567890", BuiltAtUtc = DateTime.UtcNow };
        foreach (var (app, files) in apps)
        {
            string zip = Path.Combine(dir, app + ".zip");
            if (File.Exists(zip)) File.Delete(zip);
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                foreach (var (rel, text) in files)
                {
                    var e = z.CreateEntry(rel.Replace('\\', '/'));
                    using var w = new StreamWriter(e.Open());
                    w.Write(text);
                }
            m.Artifacts.Add(new ReleaseArtifact
            {
                App = app, File = app + ".zip", Size = new FileInfo(zip).Length, Sha256 = ReleaseVerifier.Sha256File(zip),
                Changed = changed?.Contains(app) ?? true,
            });
        }
        byte[] bytes = ReleaseVerifier.Serialize(m);
        File.WriteAllBytes(Path.Combine(dir, "manifest.json"), bytes);
        File.WriteAllText(Path.Combine(dir, "manifest.sig"), SignatureJson(bytes));
        return m;
    }

    public string SignatureJson(byte[] manifest) =>
        JsonSerializer.Serialize(ReleaseVerifier.Sign(manifest, KeyId, _privatePem), ReleaseVerifier.Json);

    public static Dictionary<string, string> ErpFiles(string marker) => new()
    {
        ["app/bin/tahsinERP.dll"] = "dll " + marker,
        ["app/Web.config"] = """<?xml version="1.0"?><configuration><appSettings><add key="SiteBaseDomain" value="repo" /></appSettings><system.web><machineKey validationKey="REPO" decryptionKey="REPO" /></system.web></configuration>""",
        ["app/Views/x.cshtml"] = "view " + marker,
        ["database/tools/Invoke-DbMigrations.ps1"] = "# " + marker,
    };
}

/// <summary>ISystemOps soxtasi: fayllar haqiqiy (vaqtinchalik papka), IIS/SQL/HTTP — yozib boriladi va boshqariladi.</summary>
public sealed class FakeSystemOps : ISystemOps
{
    public List<string> Calls { get; } = [];
    public Func<string, int?> Health { get; set; } = _ => 200;
    public Func<string, IReadOnlyList<string>, ProcResult> Run { get; set; } = (_, _) => new ProcResult(0, "ok");
    public Dictionary<string, string> Env { get; } = new() { ["ConnectionStrings__Control"] = "Server=.;Password=SuperSecret123" };

    public Task StopAsync(Target t, CancellationToken ct) { Calls.Add("stop " + t.Name); return Task.CompletedTask; }
    public Task StartAsync(Target t, CancellationToken ct) { Calls.Add("start " + t.Name); return Task.CompletedTask; }
    public string? State(Target t) => "Started";

    public Task CopyAsync(string src, string dst, bool mirror, IReadOnlyCollection<string>? excludeFiles, IReadOnlyCollection<string>? excludeDirs, CancellationToken ct)
    {
        Calls.Add($"copy {(mirror ? "mirror " : "")}{src} -> {dst}");
        if (mirror && Directory.Exists(dst))
            foreach (var f in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(dst, f);
                if (excludeDirs?.Any(d => rel.Split(Path.DirectorySeparatorChar).Contains(d)) == true) continue;
                if (!File.Exists(Path.Combine(src, rel))) File.Delete(f);
            }
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f);
            if (excludeFiles?.Contains(Path.GetFileName(f)) == true) continue;
            if (excludeDirs?.Any(d => rel.Split(Path.DirectorySeparatorChar).Contains(d)) == true) continue;
            string target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target, true);
        }
        Directory.CreateDirectory(dst);
        return Task.CompletedTask;
    }

    public Task<ProcResult> RunAsync(string exe, IReadOnlyList<string> args, IDictionary<string, string>? env, string? workDir, CancellationToken ct)
    {
        Calls.Add($"run {Path.GetFileName(exe)} {string.Join(' ', args.Select(Path.GetFileName))}" + (env is null ? "" : " +env"));
        return Task.FromResult(Run(exe, args));
    }

    public Task<int?> HttpStatusAsync(string url, CancellationToken ct) { Calls.Add("health " + url); return Task.FromResult(Health(url)); }
    public IDictionary<string, string> PoolEnv(string pool) => Env;
    public Task BackupDbAsync(string db, string file, CancellationToken ct)
    {
        Calls.Add("backup-db " + db);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "bak");
        return Task.CompletedTask;
    }
    public Task RestoreDbAsync(string db, string file, CancellationToken ct) { Calls.Add("restore-db " + db); return Task.CompletedTask; }
    public string? SchemaVersion(string db) => "027_x";
    public Task GrantDbAccessAsync(string pool, string db, CancellationToken ct) { Calls.Add($"grant {pool} {db}"); return Task.CompletedTask; }
    public Task EnsureErpPoolAsync(ErpSiteSpec spec, CancellationToken ct) { Calls.Add($"ensure-pool {spec.Name}"); return Task.CompletedTask; }
    public Task EnsureErpSiteAsync(ErpSiteSpec spec, CancellationToken ct) { Calls.Add($"ensure-site {spec.Name} {spec.HostName}"); return Task.CompletedTask; }
    public Task RemoveErpSiteAsync(string name, CancellationToken ct) { Calls.Add("remove-site " + name); return Task.CompletedTask; }

    public int IndexOf(string prefix) => Calls.FindIndex(c => c.StartsWith(prefix, StringComparison.Ordinal));
}

/// <summary>Updater: imzo/hash, zaxira, rollback, ERP instance (SQL va IIS'siz — FakeSystemOps).</summary>
public sealed class UpdaterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "upd-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly TestReleaseKit _kit = new();
    private readonly FakeSystemOps _sys = new();
    private readonly UpdaterOptions _o;
    private readonly StateStore _store;
    private readonly UpdateExecutor _exec;

    public UpdaterTests()
    {
        _o = new UpdaterOptions
        {
            Domain = "test.local", HealthTimeoutSeconds = 5,
            Paths =
            {
                UpdatesDir = Path.Combine(_root, "Updates"), StateFile = Path.Combine(_root, "state.json"), ReleasesDir = Path.Combine(_root, "releases"),
                BackupRoot = Path.Combine(_root, "_backup"), DbBackupDir = Path.Combine(_root, "bak"), DatabaseRoot = Path.Combine(_root, "Database"),
                DataDir = Path.Combine(_root, "data"),
            },
            Erp = { InstancesRoot = Path.Combine(_root, "erp"), SharedPath = Path.Combine(_root, "shared") },
            Apps = { ["panel"] = new AppProfile { Path = Path.Combine(_root, "panel") } },
        };
        _store = new StateStore(_o);
        _exec = new UpdateExecutor(_o, _sys, _store, NullLogger<UpdateExecutor>.Instance) { TrustedKeys = _kit.Trusted };
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private string Job(string version, Dictionary<string, Dictionary<string, string>> apps)
    {
        string dir = Path.Combine(_root, "job-" + Guid.NewGuid().ToString("N")[..6]);
        _kit.Build(dir, version, apps);
        return dir;
    }

    private Task<ExecutionResult> RunAsync(InboxRequest r, string jobDir) =>
        _exec.ExecuteAsync(r, jobDir, new JobReporter(Path.Combine(_root, "status.json"), r.JobId), default);

    private static Dictionary<string, Dictionary<string, string>> Panel(string marker) =>
        new() { ["panel"] = new() { ["tahsinERP.ControlPanel.dll"] = marker, ["appsettings.json"] = "{}" } };

    [Fact]
    public async Task InstallApp_Succeeds_BacksUp_RunsMigrateWithPoolEnv_AndRecordsVersion()
    {
        Directory.CreateDirectory(_o.App("panel").Path!);
        File.WriteAllText(Path.Combine(_o.App("panel").Path!, "tahsinERP.ControlPanel.dll"), "old");
        var r = await RunAsync(new InboxRequest { JobId = 1, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.5" }, Job("2026.10.22.5", Panel("new")));

        Assert.Equal(UpdateOutcome.Succeeded, r.Outcome);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_o.App("panel").Path!, "tahsinERP.ControlPanel.dll")));
        var st = _store.Load().Apps["panel"];
        Assert.Equal("2026.10.22.5", st.Version);
        Assert.Single(st.Backups);
        Assert.Equal("old", File.ReadAllText(Path.Combine(st.Backups[0].Path, "tahsinERP.ControlPanel.dll")));
        Assert.Contains(_sys.Calls, c => c.StartsWith("run dotnet tahsinERP.ControlPanel.dll migrate +env"));
        int stop = _sys.IndexOf("stop tahsinERP.ControlPanel"), install = _sys.IndexOf("copy " + _o.Paths.ReleasesDir);
        Assert.True(_sys.IndexOf("copy") < stop && stop < install && install < _sys.IndexOf("run dotnet") && _sys.IndexOf("run dotnet") < _sys.IndexOf("start"),
            string.Join(" | ", _sys.Calls));
        Assert.Contains(_sys.Calls, c => c == "health https://admin.test.local/health");
    }

    [Fact]
    public async Task InstallApp_HealthFails_RollsBackFiles_StateUnchanged_SecretsRedacted()
    {
        string path = _o.App("panel").Path!;
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "tahsinERP.ControlPanel.dll"), "old");
        _sys.Run = (_, _) => new ProcResult(0, "connected with Password=SuperSecret123");
        _sys.Health = _ => 500;
        string status = Path.Combine(_root, "status.json");
        var r = await _exec.ExecuteAsync(new InboxRequest { JobId = 2, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.6" },
            Job("2026.10.22.6", Panel("broken")), new JobReporter(status, 2), default);

        Assert.Equal(UpdateOutcome.RolledBack, r.Outcome);
        Assert.Equal("old", File.ReadAllText(Path.Combine(path, "tahsinERP.ControlPanel.dll")));
        Assert.Null(_store.Load().Apps["panel"].Version);
        Assert.Contains(_sys.Calls, c => c.StartsWith("copy mirror"));
        string log = File.ReadAllText(status);
        Assert.DoesNotContain("SuperSecret123", log);
        Assert.Contains("Rollback bajarildi", log);
    }

    [Fact]
    public async Task TamperedPackage_OrForeignKey_IsRejected_BeforeAnyChange()
    {
        string dir = Job("2026.10.22.7", Panel("x"));
        File.AppendAllText(Path.Combine(dir, "panel.zip"), "!"); // hash buziladi
        var r = await RunAsync(new InboxRequest { JobId = 3, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.7" }, dir);
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.Contains("mos emas", r.Error);

        // Boshqa kalit bilan imzolangan reliz
        var other = new TestReleaseKit();
        string dir2 = Path.Combine(_root, "foreign");
        other.Build(dir2, "2026.10.22.8", Panel("x"));
        r = await RunAsync(new InboxRequest { JobId = 4, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.8" }, dir2);
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.Contains("Imzo mos kelmadi", r.Error);

        // Manifest o'zgartirilgan (imzo eski)
        string dir3 = Job("2026.10.22.9", Panel("x"));
        string mf = Path.Combine(dir3, "manifest.json");
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("2026.10.22.9", "2026.10.22.99"));
        r = await RunAsync(new InboxRequest { JobId = 5, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.99" }, dir3);
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.DoesNotContain(_sys.Calls, c => c.StartsWith("stop"));
    }

    [Fact]
    public async Task Downgrade_IsRejected_RollbackRestoresPreviousVersion()
    {
        string path = _o.App("panel").Path!;
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "tahsinERP.ControlPanel.dll"), "v1");
        Assert.Equal(UpdateOutcome.Succeeded, (await RunAsync(new InboxRequest { JobId = 6, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.2" }, Job("2026.10.22.2", Panel("v2")))).Outcome);
        var r = await RunAsync(new InboxRequest { JobId = 7, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.21.1" }, Job("2026.10.21.1", Panel("old")));
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.Contains("yangi emas", r.Error);

        r = await RunAsync(new InboxRequest { JobId = 8, Action = UpdateAction.RollbackApp, App = "panel" }, "");
        Assert.Equal(UpdateOutcome.Succeeded, r.Outcome);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(path, "tahsinERP.ControlPanel.dll")));
        var st = _store.Load().Apps["panel"];
        Assert.Null(st.Version); // birinchi o'rnatishdan oldingi versiya noma'lum edi
        Assert.Equal("2026.10.22.2", st.PreviousVersion);
        Assert.Empty(st.Backups);
    }

    private async Task<string> CreateInstanceAsync(string slug, string version)
    {
        Directory.CreateDirectory(Path.Combine(_o.Erp.SharedPath, "App_Data", "DstFiles", slug));
        File.WriteAllText(Path.Combine(_o.Erp.SharedPath, "App_Data", "DstFiles", slug, "logo.png"), "png");
        Directory.CreateDirectory(Path.Combine(_o.Erp.SharedPath, "App_Data", "DstFiles", "boshqa"));
        File.WriteAllText(Path.Combine(_o.Erp.SharedPath, "App_Data", "DstFiles", "boshqa", "x.png"), "x");
        File.WriteAllText(Path.Combine(_o.Erp.SharedPath, "Web.config"),
            """<?xml version="1.0"?><configuration><appSettings><add key="SiteBaseDomain" value="sayt.test.local" /></appSettings><system.web><machineKey validationKey="SERVERVK" decryptionKey="SERVERDK" validation="HMACSHA256" decryption="AES" /></system.web></configuration>""");
        var r = await RunAsync(new InboxRequest { JobId = 10, Action = UpdateAction.CreateInstance, App = "erp", Slug = slug, DbName = "DB_" + slug, Version = version },
            Job(version, new() { ["erp"] = TestReleaseKit.ErpFiles(version) }));
        Assert.True(r.Outcome == UpdateOutcome.Succeeded, r.Error);
        return Path.Combine(_o.Erp.InstancesRoot, slug);
    }

    [Fact]
    public async Task CreateInstance_CopiesOnlyOwnFiles_KeepsServerMachineKey_SiteCreatedAfterMigration()
    {
        string path = await CreateInstanceAsync("ipack", "2026.10.22.1");
        Assert.True(File.Exists(Path.Combine(path, "bin", "tahsinERP.dll")));
        Assert.True(File.Exists(Path.Combine(path, "App_Data", "DstFiles", "ipack", "logo.png")));
        Assert.False(Directory.Exists(Path.Combine(path, "App_Data", "DstFiles", "boshqa")));
        string cfg = File.ReadAllText(Path.Combine(path, "Web.config"));
        Assert.Contains("SERVERVK", cfg);
        Assert.Contains("sayt.test.local", cfg);
        Assert.DoesNotContain("REPO", cfg);
        Assert.True(_sys.IndexOf("run powershell.exe") < _sys.IndexOf("ensure-site erp-ipack ipack.test.local"));
        Assert.Contains(_sys.Calls, c => c == "grant erp-ipack DB_ipack");
        // IIS APPPOOL\erp-ipack hisobi faqat pool yaratilgach mavjud — pool SQL huquqidan OLDIN (prod'dagi xato)
        Assert.True(_sys.IndexOf("ensure-pool erp-ipack") < _sys.IndexOf("grant erp-ipack"), string.Join(" | ", _sys.Calls));
        var inst = _store.Load().Instances["ipack"];
        Assert.Equal("2026.10.22.1", inst.Version);
        Assert.Equal("DB_ipack", inst.DbName);
        Assert.True(File.Exists(Path.Combine(_o.Paths.DatabaseRoot, "tools", "Invoke-DbMigrations.ps1"))); // agent Database papkasi
    }

    [Fact]
    public async Task CreateInstance_HealthFails_RemovesSite_RestoresDb_DeletesFolder()
    {
        _sys.Health = _ => 503;
        var r = await RunAsync(new InboxRequest { JobId = 11, Action = UpdateAction.CreateInstance, App = "erp", Slug = "firma2", DbName = "DB_firma2", Version = "2026.10.22.1" },
            Job("2026.10.22.1", new() { ["erp"] = TestReleaseKit.ErpFiles("a") }));
        Assert.Equal(UpdateOutcome.RolledBack, r.Outcome);
        Assert.Contains(_sys.Calls, c => c == "remove-site erp-firma2");
        Assert.True(_sys.IndexOf("ensure-pool erp-firma2") < _sys.IndexOf("grant erp-firma2"));
        Assert.Contains(_sys.Calls, c => c == "restore-db DB_firma2");
        Assert.False(Directory.Exists(Path.Combine(_o.Erp.InstancesRoot, "firma2")));
        Assert.False(_store.Load().Instances.ContainsKey("firma2"));
    }

    [Fact]
    public async Task InstallTenant_MigrationFails_RestoresFilesAndDb_OtherTenantUntouched()
    {
        string ipack = await CreateInstanceAsync("ipack", "2026.10.22.1");
        File.WriteAllText(Path.Combine(ipack, "App_Data", "user-upload.txt"), "keep");
        _sys.Calls.Clear();
        _sys.Run = (exe, args) => args.Any(a => a.EndsWith("Invoke-DbMigrations.ps1")) ? new ProcResult(3, "migration 028 failed") : new ProcResult(0, "");
        var r = await RunAsync(new InboxRequest { JobId = 12, Action = UpdateAction.InstallTenant, App = "erp", Slug = "ipack", Version = "2026.10.23.1" },
            Job("2026.10.23.1", new() { ["erp"] = TestReleaseKit.ErpFiles("v2") }));

        Assert.Equal(UpdateOutcome.RolledBack, r.Outcome);
        Assert.Equal("dll 2026.10.22.1", File.ReadAllText(Path.Combine(ipack, "bin", "tahsinERP.dll")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(ipack, "App_Data", "user-upload.txt")));
        Assert.Contains(_sys.Calls, c => c == "restore-db DB_ipack");
        Assert.DoesNotContain(_sys.Calls, c => c.Contains("firma2"));
        Assert.Equal("2026.10.22.1", _store.Load().Instances["ipack"].Version);
    }

    [Fact]
    public async Task InstallTenant_Succeeds_PreservesMachineKey_ThenRollbackWithDatabase()
    {
        string ipack = await CreateInstanceAsync("ipack", "2026.10.22.1");
        var r = await RunAsync(new InboxRequest { JobId = 13, Action = UpdateAction.InstallTenant, App = "erp", Slug = "ipack", Version = "2026.10.23.1" },
            Job("2026.10.23.1", new() { ["erp"] = TestReleaseKit.ErpFiles("v2") }));
        Assert.True(r.Outcome == UpdateOutcome.Succeeded, r.Error);
        Assert.Equal("dll v2", File.ReadAllText(Path.Combine(ipack, "bin", "tahsinERP.dll")));
        Assert.Contains("SERVERVK", File.ReadAllText(Path.Combine(ipack, "Web.config")));
        var inv = UpdaterFiles.ToInventory(_store.Load(), "1.0.0");
        Assert.True(inv.Instances.Single().CanRollback);
        Assert.True(inv.Instances.Single().HasDatabaseBackup);

        _sys.Calls.Clear();
        r = await RunAsync(new InboxRequest { JobId = 14, Action = UpdateAction.RollbackTenant, App = "erp", Slug = "ipack", RestoreDatabase = true }, "");
        Assert.True(r.Outcome == UpdateOutcome.Succeeded, r.Error);
        Assert.Equal("dll 2026.10.22.1", File.ReadAllText(Path.Combine(ipack, "bin", "tahsinERP.dll")));
        Assert.Contains(_sys.Calls, c => c == "restore-db DB_ipack");
        Assert.Equal("2026.10.22.1", _store.Load().Instances["ipack"].Version);
    }

    [Fact]
    public async Task InvalidSlug_IsRejected()
    {
        var r = await RunAsync(new InboxRequest { JobId = 15, Action = UpdateAction.MigrateTenant, App = "erp", Slug = "a b; rm" }, "");
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.Contains("Slug", r.Error);
    }

    [Fact]
    public async Task InboxWorker_ProcessesRequest_WritesFinalStatus_AndDoesNotRerunInterruptedJob()
    {
        var worker = new InboxWorker(_o, _exec, _store, NullLogger<InboxWorker>.Instance);
        string inbox = Path.Combine(_o.Paths.Inbox, "21");
        _kit.Build(inbox, "2026.10.22.3", Panel("w"));
        File.WriteAllText(Path.Combine(inbox, "request.json"), JsonSerializer.Serialize(
            new InboxRequest { JobId = 21, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.3" }, UpdaterFiles.Json));
        Assert.Equal(1, await worker.ProcessPendingAsync(default));
        var st = JsonSerializer.Deserialize<JobStatus>(File.ReadAllText(Path.Combine(_o.Paths.Status, "21.json")), UpdaterFiles.Json)!;
        Assert.True(st.Final);
        Assert.Equal(UpdateOutcome.Succeeded, st.Outcome);
        Assert.False(Directory.Exists(inbox));
        Assert.Empty(Directory.GetFiles(Path.Combine(_o.Paths.UpdatesDir, "Done", "21"), "*.zip"));

        // Uzilgan job: status bor, request qayta paydo bo'lgan — qayta bajarilmaydi
        string inbox2 = Path.Combine(_o.Paths.Inbox, "22");
        _kit.Build(inbox2, "2026.10.22.4", Panel("w2"));
        File.WriteAllText(Path.Combine(inbox2, "request.json"), JsonSerializer.Serialize(
            new InboxRequest { JobId = 22, Action = UpdateAction.InstallApp, App = "panel", Version = "2026.10.22.4" }, UpdaterFiles.Json));
        Directory.CreateDirectory(_o.Paths.Status);
        File.WriteAllText(Path.Combine(_o.Paths.Status, "22.json"), "{\"jobId\":22,\"stage\":\"Fayllar\",\"log\":\"\",\"final\":false}");
        _sys.Calls.Clear();
        await worker.ProcessPendingAsync(default);
        st = JsonSerializer.Deserialize<JobStatus>(File.ReadAllText(Path.Combine(_o.Paths.Status, "22.json")), UpdaterFiles.Json)!;
        Assert.Equal(UpdateOutcome.Failed, st.Outcome);
        Assert.DoesNotContain(_sys.Calls, c => c.StartsWith("stop"));
    }
}

/// <summary>Reliz imzosi, versiyalar, Web.config, panelning "taklif" mantiqi (SQL'siz).</summary>
public sealed class ReleaseTests
{
    [Fact]
    public void EmbeddedKey_IsValidP256_AndVerifierRejectsUnknownKeyAndBadJson()
    {
        foreach (var pem in ReleaseKeys.Trusted.Values)
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            Assert.Equal(256, ec.KeySize);
        }
        var kit = new TestReleaseKit();
        byte[] m = ReleaseVerifier.Serialize(new ReleaseManifest { Version = "2026.10.22.1" });
        Assert.Throws<ReleaseVerificationException>(() => ReleaseVerifier.Verify(m, kit.SignatureJson(m))); // t1 — ishonchli emas
        Assert.Equal("2026.10.22.1", ReleaseVerifier.Verify(m, kit.SignatureJson(m), kit.Trusted).Version);
        Assert.Throws<ReleaseVerificationException>(() => ReleaseVerifier.Verify(m, "not json", kit.Trusted));
        Assert.Throws<ReleaseVerificationException>(() => ReleaseVerifier.Verify(Encoding.UTF8.GetBytes("{\"format\":1,\"version\":\"x\"}"),
            kit.SignatureJson(Encoding.UTF8.GetBytes("{\"format\":1,\"version\":\"x\"}")), kit.Trusted));
    }

    [Theory]
    [InlineData("2026.10.22.2", "2026.10.22.1", true)]
    [InlineData("2026.10.22.10", "2026.10.22.9", true)]
    [InlineData("2026.10.22.1", "2026.10.22.1", false)]
    [InlineData("2026.9.30.50", "2026.10.1.1", false)]
    [InlineData("2026.10.22.1", null, true)]
    [InlineData("2026.10.22.1", "legacy", true)]
    [InlineData("legacy", "2026.10.22.1", false)]
    public void IsNewer(string a, string? b, bool expected) => Assert.Equal(expected, ReleaseVerifier.IsNewer(a, b));

    [Fact]
    public void Offer_OnlyWhenAppChangedInRange_AndNotesSectionPerApp()
    {
        var valid = new List<AppRelease>
        {
            new() { Version = "2026.10.24.3", Apps = "erp,panel*", SignatureValid = true, Notes = "x\n### panel\n- panel fix\n### erp\n- erp" },
            new() { Version = "2026.10.23.2", Apps = "erp*,panel", SignatureValid = true },
            new() { Version = "2026.10.22.1", Apps = "erp*,panel*", SignatureValid = true },
        };
        Assert.Equal("2026.10.24.3", UpdatesService.Offer(valid, "erp", "2026.10.22.1")?.Version); // 23.2 da o'zgargan → eng yangisi
        Assert.Null(UpdatesService.Offer(valid, "erp", "2026.10.23.2"));                          // keyin o'zgarmagan
        Assert.Equal("2026.10.24.3", UpdatesService.Offer(valid, "panel", "2026.10.23.2")?.Version);
        Assert.Null(UpdatesService.Offer(valid, "bot", null));
        Assert.Equal("- panel fix", IndexModel.NotesFor(valid[0], "panel"));
        Assert.Equal("- erp", IndexModel.NotesFor(valid[0], "erp"));
    }

    /// <summary>
    /// ERP Web.config'dagi umumiy &lt;deny users="?"/&gt; /health'ni login'ga (302) yo'naltirib, Updater yangilashni
    /// "health o'tmadi" deb qaytarardi (prod, 2026-10-22). /health anonim ochiq bo'lishi shart.
    /// </summary>
    [Fact]
    public void ErpWebConfig_AllowsAnonymousHealth()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP", "Web.config"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var doc = System.Xml.Linq.XDocument.Load(Path.Combine(dir!.FullName, "tahsinERP", "Web.config"));
        var loc = doc.Root!.Elements("location").FirstOrDefault(l => string.Equals((string?)l.Attribute("path"), "health", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(loc);
        Assert.Equal("*", (string?)loc!.Element("system.web")?.Element("authorization")?.Element("allow")?.Attribute("users"));
        Assert.True(File.Exists(Path.Combine(dir.FullName, "tahsinERP", "Controllers", "HealthController.cs")));
    }

    [Fact]
    public void WebConfigMerger_PreservesServerValues_OrGeneratesNewKey()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wc-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string old = Path.Combine(dir, "old.config"), fresh = Path.Combine(dir, "new.config");
            File.WriteAllText(old, """<configuration><appSettings><add key="SiteBaseDomain" value="sayt.x.uz" /><add key="Other" value="o" /></appSettings><system.web><machineKey validationKey="VK" decryptionKey="DK" validation="HMACSHA256" decryption="AES" /></system.web></configuration>""");
            File.WriteAllText(fresh, """<configuration><appSettings><add key="SiteBaseDomain" value="repo" /></appSettings><system.web><compilation debug="false" /></system.web></configuration>""");
            var p = WebConfigMerger.Capture(old, ["SiteBaseDomain", "SiteCnameTarget"]);
            WebConfigMerger.Apply(fresh, p);
            string s = File.ReadAllText(fresh);
            Assert.Contains("validationKey=\"VK\"", s);
            Assert.Contains("sayt.x.uz", s);
            Assert.DoesNotContain("Other", s);
            Assert.Contains("compilation", s);

            File.WriteAllText(fresh, """<configuration><system.web /></configuration>""");
            WebConfigMerger.Apply(fresh, WebConfigMerger.Capture(Path.Combine(dir, "yoq.config"), []));
            Assert.Matches("validationKey=\"[0-9A-F]{128}\"", File.ReadAllText(fresh));
        }
        finally { Directory.Delete(dir, true); }
    }
}
