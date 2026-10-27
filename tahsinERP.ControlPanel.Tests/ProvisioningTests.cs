using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Agent tomonidagi sof unit testlar (SQL'siz).</summary>
public class AgentUnitTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cp2-unit-" + Guid.NewGuid().ToString("N")[..8]);

    public AgentUnitTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string FilePath => Path.Combine(_dir, "tenants.json");

    [Fact]
    public void TenantsFile_Upsert_IsReadByErpRegistry_AndIdempotent()
    {
        var tf = new TenantsFile(FilePath, "test.local");
        Assert.True(tf.Upsert("firma1", "DB_firma1", 1, true));
        var stamp = File.GetLastWriteTimeUtc(FilePath);
        Assert.False(tf.Upsert("firma1", "DB_firma1", 1, true)); // o'zgarish yo'q — fayl tegilmaydi
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(FilePath));

        var reg = TenantRegistry.Parse(File.ReadAllText(FilePath), null);
        Assert.False(reg.IsBroken, reg.Error);
        Assert.Equal("DB_firma1", reg.ResolveHost("firma1.test.local")?.Database);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }

    [Fact]
    public void TenantsFile_Disable_ChangesStamp_PreservesOthers_AndErpHidesTenant()
    {
        File.WriteAllText(FilePath, """
            { "baseDomain": "test.local", "custom": 42,
              "tenants": [ { "slug": "keep", "database": "DB_keep", "hosts": ["erp.keep.uz"] } ] }
            """);
        File.SetLastWriteTimeUtc(FilePath, DateTime.UtcNow.AddMinutes(5)); // kelajakdagi vaqt — yangi yozuv baribir kattaroq bo'lsin
        var old = File.GetLastWriteTimeUtc(FilePath);
        var tf = new TenantsFile(FilePath, null);
        tf.Upsert("firma2", "DB_firma2", 1, true);
        Assert.True(File.GetLastWriteTimeUtc(FilePath) > old);
        old = File.GetLastWriteTimeUtc(FilePath);
        Assert.True(tf.Upsert("firma2", "DB_firma2", 1, false));
        Assert.True(File.GetLastWriteTimeUtc(FilePath) > old);

        string json = File.ReadAllText(FilePath);
        Assert.Contains("\"custom\": 42", json);
        var reg = TenantRegistry.Parse(json, null);
        Assert.Null(reg.ResolveHost("firma2.test.local"));               // enabled=false → 404
        Assert.Equal("DB_keep", reg.ResolveHost("erp.keep.uz")?.Database); // boshqa tenant saqlangan
        Assert.True(File.Exists(FilePath + ".prev"));

        Assert.True(tf.Remove("firma2"));
        Assert.False(tf.Remove("firma2"));
        Assert.Null(tf.Get("firma2"));
    }

    [Fact]
    public void TenantsFile_RejectsReservedSlug_MissingDomain_AndBrokenFile()
    {
        Assert.Throws<InvalidJobException>(() => new TenantsFile(FilePath, "test.local").Upsert("admin", "DB_admin", 1, true));
        Assert.Throws<InvalidJobException>(() => new TenantsFile(FilePath, "test.local").Upsert("ok", "DB;drop", 1, true));
        if (Environment.GetEnvironmentVariable("TAHSINERP_BASE_DOMAIN") is null)
            Assert.Throws<InvalidJobException>(() => new TenantsFile(FilePath, null).Upsert("nodomain", "DB_x", 1, true));
        File.WriteAllText(FilePath, "{ buzilgan");
        Assert.Throws<InvalidJobException>(() => new TenantsFile(FilePath, "test.local").Upsert("ok", "DB_ok", 1, true));
        Assert.Equal("{ buzilgan", File.ReadAllText(FilePath)); // buzilgan fayl ustidan yozilmaydi
    }

    [Fact]
    public void Protocol_SignVerify_AndRedact()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        string sts = AgentProtocol.StringToSign("post", "/api/agent/lease", "123", "ab", AgentProtocol.BodyHash("{}"u8));
        string sig = AgentProtocol.Sign(secret, sts);
        Assert.True(AgentProtocol.Verify(secret, sts, sig));
        Assert.False(AgentProtocol.Verify(secret, sts + "x", sig));
        Assert.False(AgentProtocol.Verify(RandomNumberGenerator.GetBytes(32), sts, sig));
        Assert.False(AgentProtocol.Verify(secret, sts, "not-base64!"));
        Assert.Equal("parol=******** ok", ScriptRunner.Redact("parol=Sirli-123 ok", ["Sirli-123"]));
    }

    /// <summary>Umumiy demo standart holatda o'chiq (2026-10-26): agent Reset rejalashtirmaydi, panel hech bir tenantni demo demaydi.</summary>
    [Fact]
    public void Demo_IsOffByDefault()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 18, 22, 30, 0, TimeSpan.Zero)); // Toshkent 03:30 — oyna ichida
        var opts = new AgentOptions { Paths = { StateDir = _dir } };
        Assert.False(opts.Demo.Enabled);
        Assert.Null(new DemoScheduler(opts, clock, NullLogger<DemoScheduler>.Instance).DueKey());
        Assert.Equal("", new tahsinERP.ControlPanel.Services.ProvisioningOptions().DemoSlug);
    }

    [Fact]
    public void DemoScheduler_OnlyInWindow_OncePerDay()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 19, 5, 0, 0, TimeSpan.Zero)); // Toshkent 10:00
        var opts = new AgentOptions { Paths = { StateDir = _dir }, Demo = { Enabled = true, Slug = "demo" } };
        var s = new DemoScheduler(opts, clock, NullLogger<DemoScheduler>.Instance);
        Assert.Null(s.DueKey());
        clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 22, 30, 0, TimeSpan.Zero)); // Toshkent 19.10 03:30
        Assert.Equal("2026-10-19", s.DueKey());
        s.MarkDone("2026-10-19");
        Assert.Null(s.DueKey());
        clock.SetUtcNow(new DateTimeOffset(2026, 10, 19, 22, 1, 0, TimeSpan.Zero)); // ertasi 03:01
        Assert.Equal("2026-10-20", s.DueKey());
    }

    [Fact]
    public async Task LocalBackupStore_StoresFindsDeletes_InsideRootOnly()
    {
        var opts = new AgentOptions { Paths = { ArchiveDir = Path.Combine(_dir, "arch") } };
        var store = new LocalBackupStore(opts);
        string bak = Path.Combine(_dir, "DB_x_1.bak");
        File.WriteAllText(bak, "bak");
        string loc = await store.StoreAsync(bak, "firma1", default);
        Assert.StartsWith("file:", loc);
        Assert.False(File.Exists(bak));
        Assert.Equal(loc, store.FindLatestAsync("firma1", default).Result);
        await Assert.ThrowsAsync<InvalidJobException>(() => store.DeleteAllAsync(@"..\x", default));
        Assert.Equal(1, store.DeleteAllAsync("firma1", default).Result);
        Assert.Null(store.FindLatestAsync("firma1", default).Result);
    }
}

/// <summary>Panel tomoni: navbat qoidalari, agent API (HMAC), lease/complete, qayta urinish, idempotentlik, audit.</summary>
public class ProvisioningQueueTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    internal static Task<(int ServerId, string Secret)> CreateServerAsync(PanelFactory f, string name) => f.WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var s = new Server
        {
            Name = name, Host = "localhost", SqlInstance = f.SqlServer, MaxTenants = 50, CreatedAtUtc = DateTime.UtcNow,
            AgentSecretProtected = sp.GetRequiredService<ISecretProtector>().Protect(secret, SecretPurposes.AgentSecret),
        };
        db.Servers.Add(s);
        await db.SaveChangesAsync();
        return (s.Id, secret);
    });

    internal static PanelClient Client(PanelFactory f, int serverId, string secret) =>
        new(new HttpClient(f.Server.CreateHandler()), new AgentOptions { Panel = { Url = "https://localhost", ServerId = serverId, Secret = secret } }, f.Time);

    private Task<Tenant> TenantOnAsync(string slug, int serverId, bool trial = true) => f.WithScopeAsync(sp =>
        sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = slug + " MChJ", Deployment = DeploymentKind.SaaS, ServerId = serverId }, trial));

    private Task<T> Prov<T>(Func<ProvisioningService, ControlDbContext, Task<T>> a) => f.WithScopeAsync(sp =>
        a(sp.GetRequiredService<ProvisioningService>(), sp.GetRequiredService<ControlDbContext>()));

    [Fact]
    public async Task Enqueue_IsIdempotent_RejectsConflicts_AndAudits()
    {
        var (sid, _) = await CreateServerAsync(f, "q-srv-1");
        var t = await TenantOnAsync("q-idem", sid);
        var r1 = await Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Create));
        Assert.True(r1.Created);
        Assert.Equal(12, r1.TempPassword!.Length);
        var r2 = await Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Create));
        Assert.False(r2.Created);
        Assert.Equal(r1.Job.Id, r2.Job.Id);
        await Assert.ThrowsAsync<DomainException>(() =>
            Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Archive, "x")));
        // Delete faqat Archived'dan keyin
        await Prov(async (p, db) => { var j = await db.ProvisioningJobs.FirstAsync(x => x.Id == r1.Job.Id); await p.CancelAsync(j); return 0; });
        await Assert.ThrowsAsync<DomainException>(() =>
            Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Delete, "x")));
        var job = await Prov((_, db) => db.ProvisioningJobs.FirstAsync(x => x.Id == r1.Job.Id));
        Assert.Equal(ProvisioningJobStatus.Cancelled, job.Status);
        Assert.Null(job.SecretsProtected);
        var actions = await Prov((_, db) => db.AuditLog.Where(a => a.Entity == "Tenant" && a.EntityId == t.Id.ToString()).Select(a => a.Action).ToListAsync());
        Assert.Contains("provisioning.enqueue", actions);
        Assert.Contains("provisioning.cancel", actions);
        Assert.Contains("tenant.regtoken", actions);
    }

    [Fact]
    public async Task AgentApi_RejectsBadSignature_Replay_AndForeignServer()
    {
        var (sid, secret) = await CreateServerAsync(f, "q-srv-2");
        Assert.Null(await Client(f, sid, secret).LeaseAsync(default)); // to'g'ri imzo, navbat bo'sh → 204
        var bad = Client(f, sid, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var ex = await Assert.ThrowsAsync<PanelClient.PanelException>(() => bad.LeaseAsync(default));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);

        // replay: aynan bir xil so'rovni ikki marta
        using var http = new HttpClient(f.Server.CreateHandler());
        byte[] body = "{}"u8.ToArray();
        HttpRequestMessage Make()
        {
            var m = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/agent/lease") { Content = new ByteArrayContent(body) };
            return m;
        }
        var first = Make();
        AgentProtocol.SignRequest(first, sid, Convert.FromBase64String(secret), body, f.Time.GetUtcNow(), "test");
        var second = Make();
        foreach (var h in first.Headers) second.Headers.TryAddWithoutValidation(h.Key, h.Value);
        Assert.Equal(HttpStatusCode.NoContent, (await http.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(second)).StatusCode);

        // boshqa serverning tenanti bu agentga berilmaydi
        var (sid2, secret2) = await CreateServerAsync(f, "q-srv-3");
        var t = await TenantOnAsync("q-foreign", sid2);
        await Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Create));
        Assert.Null(await Client(f, sid, secret).LeaseAsync(default));
        var lease = await Client(f, sid2, secret2).LeaseAsync(default);
        Assert.NotNull(lease);
        Assert.Equal("q-foreign", lease!.Payload.Slug);
        Assert.NotNull(lease.Secrets?.RegToken);
        Assert.NotNull(lease.Secrets?.AdminPassword);
    }

    [Fact]
    public async Task Lease_Complete_Retry_LeaseExpiry_AndReconcile()
    {
        var (sid, secret) = await CreateServerAsync(f, "q-srv-4");
        var agent = Client(f, sid, secret);
        var t = await TenantOnAsync("q-flow", sid);
        var enq = await Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Create));

        // 1) vaqtinchalik xato → Pending, pauza bilan
        var l1 = (await agent.LeaseAsync(default))!;
        await agent.CompleteAsync(l1.JobId, new JobCompletion { LeaseToken = l1.LeaseToken, Outcome = JobOutcome.Retry, Error = "SQL band", ExitCode = 6, Log = "log-1" }, default);
        Assert.Null(await agent.LeaseAsync(default)); // NotBefore hali kelmagan
        f.Time.Advance(TimeSpan.FromMinutes(2));

        // 2) lease olinadi, agent "yiqiladi" — muddat o'tgach qayta beriladi; eski token bilan natija rad etiladi
        var l2 = (await agent.LeaseAsync(default))!;
        Assert.Equal(2, l2.Attempt);
        f.Time.Advance(TimeSpan.FromMinutes(31));
        var l3 = (await agent.LeaseAsync(default))!;
        Assert.Equal(3, l3.Attempt);
        var ex = await Assert.ThrowsAsync<PanelClient.PanelException>(() =>
            agent.CompleteAsync(l2.JobId, new JobCompletion { LeaseToken = l2.LeaseToken, Outcome = JobOutcome.Succeeded }, default));
        Assert.Equal(HttpStatusCode.Conflict, ex.Status);

        // 3) muvaffaqiyat → tenant provisioned, sirlar o'chirilgan
        await agent.ExtendAsync(l3.JobId, l3.LeaseToken, default);
        await agent.CompleteAsync(l3.JobId, new JobCompletion { LeaseToken = l3.LeaseToken, Outcome = JobOutcome.Succeeded, RegistryEnabled = true, Log = "ok" }, default);
        var (job, tenant) = await Prov(async (_, db) => (await db.ProvisioningJobs.FirstAsync(x => x.Id == enq.Job.Id), await db.Tenants.FirstAsync(x => x.Id == t.Id)));
        Assert.Equal(ProvisioningJobStatus.Succeeded, job.Status);
        Assert.Null(job.SecretsProtected);
        Assert.Contains("log-1", job.Log);
        Assert.NotNull(tenant.ProvisionedAtUtc);
        Assert.True(tenant.RegistryEnabled);

        // 4) holat Suspended bo'ldi (masalan kunlik tekshiruv) → keyingi lease'da avtomatik Suspend job
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            await sp.GetRequiredService<TenantService>().ChangeStatusAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), TenantStatus.Suspended, "test");
        });
        var l4 = (await agent.LeaseAsync(default))!;
        Assert.Equal(JobKind.Suspend, l4.Type);
        Assert.False(l4.Payload.Enabled);
        await agent.CompleteAsync(l4.JobId, new JobCompletion { LeaseToken = l4.LeaseToken, Outcome = JobOutcome.Succeeded, RegistryEnabled = false }, default);
        Assert.Null(await agent.LeaseAsync(default)); // holat mos — yangi job yo'q

        // 5) baza yaratilgach DbName/Server o'zgarmaydi; boshqa maydonlar o'zgaradi
        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(async sp =>
        {
            var x = await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(y => y.Id == t.Id);
            var svc = sp.GetRequiredService<TenantService>();
            var before = svc.TakeSnapshot(x);
            x.DbName = "DB_boshqa";
            await svc.UpdateAsync(x, before);
        }));
        await f.WithScopeAsync(async sp =>
        {
            var x = await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(y => y.Id == t.Id);
            var svc = sp.GetRequiredService<TenantService>();
            var before = svc.TakeSnapshot(x);
            x.Phone = "+998 90 000 00 00";
            await svc.UpdateAsync(x, before);
        });

        // 6) qo'lda arxivlash (status dropdown) bazasi yaratilgan tenantda taqiqlangan
        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            await sp.GetRequiredService<TenantService>().ChangeStatusAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), TenantStatus.Archived, "x");
        }));
    }

    [Fact]
    public async Task FinalFailure_AfterMaxAttempts_ThenManualRetry()
    {
        var (sid, secret) = await CreateServerAsync(f, "q-srv-5");
        var agent = Client(f, sid, secret);
        var t = await TenantOnAsync("q-fail", sid);
        var enq = await Prov(async (p, db) => await p.EnqueueAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), ProvisioningJobType.Create));
        var l = (await agent.LeaseAsync(default))!;
        await agent.CompleteAsync(l.JobId, new JobCompletion { LeaseToken = l.LeaseToken, Outcome = JobOutcome.Failed, Error = "kod 4", ExitCode = 4 }, default);
        var job = await Prov((_, db) => db.ProvisioningJobs.FirstAsync(x => x.Id == enq.Job.Id));
        Assert.Equal(ProvisioningJobStatus.Failed, job.Status);
        Assert.NotNull(job.SecretsProtected); // qayta urinishda o'sha parol ishlatiladi
        await Prov(async (p, db) => { await p.RetryAsync(await db.ProvisioningJobs.FirstAsync(x => x.Id == enq.Job.Id)); return 0; });
        var again = (await agent.LeaseAsync(default))!;
        Assert.Equal(enq.Job.Id, again.JobId);
        Assert.Equal(enq.TempPassword, again.Secrets?.AdminPassword);
    }

    [Fact]
    public async Task Schedule_DemoReset_IsIdempotentPerKey_AndOnlyForDemo()
    {
        var (sid, secret) = await CreateServerAsync(f, "q-srv-6");
        var agent = Client(f, sid, secret);
        var demo = await TenantOnAsync(PanelFactory.DemoSlug, sid);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.Tenants.FirstAsync(x => x.Id == demo.Id)).ProvisionedAtUtc = DateTime.UtcNow;
            (await db.Tenants.FirstAsync(x => x.Id == demo.Id)).RegistryEnabled = true;
            await db.SaveChangesAsync();
        });
        var a = await agent.ScheduleAsync(new ScheduleRequest { Type = JobKind.Reset, Slug = PanelFactory.DemoSlug, Key = "2026-10-19" }, default);
        var b = await agent.ScheduleAsync(new ScheduleRequest { Type = JobKind.Reset, Slug = PanelFactory.DemoSlug, Key = "2026-10-19" }, default);
        Assert.True(a.Created);
        Assert.False(b.Created);
        Assert.Equal(a.JobId, b.JobId);
        var other = await TenantOnAsync("q-notdemo", sid);
        var ex = await Assert.ThrowsAsync<PanelClient.PanelException>(() =>
            agent.ScheduleAsync(new ScheduleRequest { Type = JobKind.Reset, Slug = "q-notdemo", Key = "2026-10-19" }, default));
        Assert.Equal((HttpStatusCode)422, ex.Status);
    }
}

/// <summary>
/// Integratsion: haqiqiy panel (test bazasi) + haqiqiy agent (PanelClient/AgentWorker/JobExecutor) + LOKAL SQL Server + Database/tools skriptlari.
/// Test tenant yaratiladi, tenants.json ERP'ning haqiqiy TenantRegistry'si bilan o'qiladi, bazaga ERP kabi ulaniladi, so'ng
/// Suspend/Resume/Archive/Delete va demo Reset — oxirida hamma narsa tozalanadi.
/// </summary>
public partial class ProvisioningIntegrationTests(PanelFactory f) : IClassFixture<PanelFactory>, IAsyncLifetime
{
    private readonly string _work = Path.Combine(@"C:\ProgramData\tahsinERP-test", "cp2-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..6];
    private readonly List<string> _databases = [];
    private IServiceProvider? _agentServices;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_work, "sql"));
        // SQL Server xizmati .bak yoza olishi uchun (lokal dev: NT Service\MSSQLSERVER)
        var psi = new System.Diagnostics.ProcessStartInfo("icacls", $"\"{Path.Combine(_work, "sql")}\" /grant \"NT Service\\MSSQLSERVER:(OI)(CI)M\"")
        { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        System.Diagnostics.Process.Start(psi)!.WaitForExit();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var db in _databases) // shablon yig'ish bazasi noyob nomli, agent o'zi o'chiradi
        {
            await using var c = new SqlConnection($"Server={f.SqlServer};Database=master;Trusted_Connection=True;TrustServerCertificate=True");
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"IF DB_ID('{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END";
            await cmd.ExecuteNonQueryAsync();
        }
        try
        {
            Directory.Delete(_work, true);
            string parent = Path.GetDirectoryName(_work)!;
            if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string DatabaseRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tahsinERP", "Database", "tools"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("tahsinERP/Database topilmadi"), "tahsinERP", "Database");
    }

    private (AgentWorker Worker, AgentOptions Options) Agent(int serverId, string secret)
    {
        var opts = new AgentOptions
        {
            Panel = { Url = "https://localhost", ServerId = serverId, Secret = secret, PollSeconds = 1 },
            Sql = { Server = f.SqlServer },
            Paths =
            {
                DatabaseRoot = DatabaseRoot(), TenantsFile = Path.Combine(_work, "tenants.json"), BaseDomain = PanelFactory.BaseDomain,
                BackupDir = Path.Combine(_work, "sql"), ArchiveDir = Path.Combine(_work, "archive"), StateDir = Path.Combine(_work, "state"),
            },
            Demo = { Enabled = true, Slug = PanelFactory.DemoSlug, UserLogin = "demo", UserPassword = "DemoParol-2026", TemplateBak = Path.Combine(_work, "sql", "DB_DEMO_TEMPLATE.bak") },
        };
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(opts);
        services.AddSingleton<TimeProvider>(f.Time);
        services.AddSingleton<SqlOps>();
        services.AddSingleton<ScriptRunner>();
        services.AddSingleton<IBackupStore, LocalBackupStore>();
        services.AddSingleton<DemoScheduler>();
        services.AddScoped<JobExecutor>();
        services.AddSingleton<UsageCollector>();
        services.AddTransient(sp => new PanelClient(new HttpClient(f.Server.CreateHandler()), opts, f.Time));
        var sp = services.BuildServiceProvider();
        _agentServices = sp;
        return (new AgentWorker(sp, opts, sp.GetRequiredService<ILogger<AgentWorker>>()), opts);
    }

    private Task<ProvisioningJob> JobAsync(long id) => f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().ProvisioningJobs.AsNoTracking().FirstAsync(j => j.Id == id));
    private Task<Tenant> TenantAsync(int id) => f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Tenants.AsNoTracking().FirstAsync(t => t.Id == id));

    private async Task<string?> ScalarAsync(string db, string sql)
    {
        await using var c = new SqlConnection($"Server={f.SqlServer};Database={db};Trusted_Connection=True;TrustServerCertificate=True;Pooling=false");
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    private async Task<bool> DbExistsAsync(string db) => await ScalarAsync("master", $"SELECT COUNT(*) FROM sys.databases WHERE name = '{db}'") == "1";

    private TenantRegistry Erp(AgentOptions o) => TenantRegistry.Parse(File.ReadAllText(o.Paths.TenantsFile!), null);

    private async Task<ProvisioningJob> RunAsync(AgentWorker worker, long jobId)
    {
        Assert.True(await worker.RunOnceAsync(default), "agent job olmadi");
        var job = await JobAsync(jobId);
        Assert.True(job.Status == ProvisioningJobStatus.Succeeded, $"job #{jobId} {job.Status}: {job.Error}\n{job.Log}");
        return job;
    }

    [Fact]
    public async Task TrialCreate_Suspend_Resume_Archive_Delete_EndToEnd()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv-" + _suffix);
        var (worker, opts) = Agent(sid, secret);
        string slug = "it-" + _suffix;
        string dbName = "DB_it_" + _suffix;
        _databases.Add(dbName);

        // Sinov: tenant + TRIAL + Create job (sinov formasi bilan bir xil servislar)
        var (tenant, enq) = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(
                new Tenant { Slug = slug, LegalName = "\"IT Test\" MChJ", Deployment = DeploymentKind.SaaS, ServerId = sid, TelegramChatId = "-100123" }, startTrial: true);
            return (t, await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create));
        });
        Assert.Equal(dbName, tenant.DbName);
        string tempPassword = enq.TempPassword!;

        var job = await RunAsync(worker, enq.Job.Id);
        Assert.DoesNotContain(tempPassword, job.Log);
        Assert.DoesNotContain(tempPassword, job.Result ?? "");
        Assert.DoesNotContain("tempPassword", job.Result ?? "");

        // Baza: admin vaqtinchalik parol bilan, almashtirish majburiy
        Assert.True(await DbExistsAsync(dbName));
        Assert.Equal("1", await ScalarAsync(dbName, "SELECT CAST(MustChangePassword AS int) FROM dbo.USERS WHERE Uname = 'admin'"));
        string hash = (await ScalarAsync(dbName, "SELECT Password FROM dbo.USERS WHERE Uname = 'admin'"))!;
        string salt = (await ScalarAsync(dbName, "SELECT HashCode FROM dbo.USERS WHERE Uname = 'admin'"))!;
        Assert.True(tahsinERP.Helpers.PasswordHasher.Verify(tempPassword, hash, salt, out _));
        Assert.Equal("\"IT Test\" MChJ", await ScalarAsync(dbName, "SELECT Name FROM dbo.COMPANIES WHERE ID = 1"));

        // Litsenziya: RegToken yozilgan (panel xeshiga mos), InstallationId YO'Q
        string regToken = (await ScalarAsync(dbName, "SELECT Value FROM dbo.LICENSE_STATE WHERE Name = 'RegToken'"))!;
        Assert.Null(await ScalarAsync(dbName, "SELECT Value FROM dbo.LICENSE_STATE WHERE Name = 'InstallationId'"));
        var t1 = await TenantAsync(tenant.Id);
        Assert.Equal(TenantService.HashToken(regToken), t1.RegTokenHash);
        Assert.Equal(TenantStatus.Trial, t1.Status);
        Assert.NotNull(t1.ProvisionedAtUtc);
        Assert.True(t1.RegistryEnabled);

        // ERP reestri (haqiqiy TenantRegistry): host → baza
        var info = Erp(opts).ResolveHost($"{slug}.{PanelFactory.BaseDomain}");
        Assert.NotNull(info);
        Assert.Equal(dbName, info!.Database);

        // ERP register oqimi shu token bilan ishlaydi (InstallationId'ni ERP o'zi yaratadi)
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(regToken, inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);

        // Xush kelibsiz xabari (Telegram sozlanmagan → log), parolsiz
        var welcome = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Notifications.FirstAsync(n => n.TenantId == tenant.Id && n.Template == "welcome"));
        Assert.DoesNotContain(tempPassword, welcome.Message);

        // Suspend → enabled=false (ERP 404), Resume → true
        var susp = await f.WithScopeAsync(async sp =>
            await sp.GetRequiredService<ProvisioningService>().SuspendAsync(await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), "test"));
        await RunAsync(worker, susp!.Id);
        Assert.Null(Erp(opts).ResolveHost($"{slug}.{PanelFactory.BaseDomain}"));
        var res = await f.WithScopeAsync(async sp =>
            await sp.GetRequiredService<ProvisioningService>().ResumeAsync(await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), "test"));
        await RunAsync(worker, res!.Id);
        Assert.NotNull(Erp(opts).ResolveHost($"{slug}.{PanelFactory.BaseDomain}"));
        Assert.Equal(TenantStatus.Trial, (await TenantAsync(tenant.Id)).Status);

        // Archive → .bak lokal arxivda, baza o'chirilgan, tenants.json enabled=false
        var arch = await f.WithScopeAsync(async sp => (await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(
            await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), ProvisioningJobType.Archive, "test arxiv")).Job);
        await RunAsync(worker, arch.Id);
        var t2 = await TenantAsync(tenant.Id);
        Assert.Equal(TenantStatus.Archived, t2.Status);
        Assert.False(await DbExistsAsync(dbName));
        string bak = t2.ArchiveLocation!["file:".Length..];
        Assert.True(File.Exists(bak), bak);
        Assert.True(File.Exists(bak + ".sha256"));
        Assert.Equal((false), new TenantsFile(opts.Paths.TenantsFile!, null).Get(slug)?.Enabled);

        // Delete → arxiv va reestr yozuvi o'chadi; moliyaviy tarix (tenant qatori) qoladi
        var del = await f.WithScopeAsync(async sp => (await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(
            await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), ProvisioningJobType.Delete, "test o'chirish")).Job);
        await RunAsync(worker, del.Id);
        var t3 = await TenantAsync(tenant.Id);
        Assert.NotNull(t3.DeletedAtUtc);
        Assert.Null(t3.RegistryEnabled);
        Assert.False(File.Exists(bak));
        Assert.Null(new TenantsFile(opts.Paths.TenantsFile!, null).Get(slug));
        // nom va baza nomi bo'shadi — shu nom bilan yangi firma ochiladi (eski yozuv tarix uchun "<slug>-o<Id>")
        Assert.Equal(ProvisioningService.ReleasedSlug(slug, tenant.Id), t3.Slug);
        Assert.Null(t3.DbName);
        var again = await f.WithScopeAsync(async sp => await sp.GetRequiredService<TenantService>().CreateAsync(
            new Tenant { Slug = slug, LegalName = "Yangi", Deployment = DeploymentKind.SaaS, ServerId = sid }, true));
        Assert.Equal(slug, again.Slug);
        Assert.Equal(dbName, again.DbName);

        var actions = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().AuditLog
            .Where(a => a.Entity == "Tenant" && a.EntityId == tenant.Id.ToString()).Select(a => a.Action).ToListAsync());
        Assert.Equal(5, actions.Count(a => a == "provisioning.succeeded"));
    }

    /// <summary>Sinov firmasi arxivlanadi, so'ng arxivdan YANGI nom bilan tiklanadi (smarttech → demodist holati); eski nom darhol bo'shaydi.</summary>
    [Fact]
    public async Task Archive_ThenRestoreWithNewSlug_FreesOldSlug()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv-rs-" + _suffix);
        var (worker, opts) = Agent(sid, secret);
        string slug = "itr-" + _suffix, newSlug = "itn-" + _suffix;
        string dbName = "DB_itr_" + _suffix, newDb = "DB_itn_" + _suffix;
        _databases.Add(dbName);
        _databases.Add(newDb);
        var (tenant, enq) = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(
                new Tenant { Slug = slug, LegalName = "\"Restore\" MChJ", Deployment = DeploymentKind.SaaS, ServerId = sid }, startTrial: true);
            return (t, await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create));
        });
        await RunAsync(worker, enq.Job.Id);
        await ScalarAsync(dbName, "UPDATE dbo.COMPANIES SET Name = N'Arxivdagi ma''lumot' WHERE ID = 1");

        var arch = await f.WithScopeAsync(async sp => (await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(
            await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), ProvisioningJobType.Archive, "test arxiv")).Job);
        await RunAsync(worker, arch.Id);
        Assert.False(await DbExistsAsync(dbName));

        bool renamed = false;
        var restore = await f.WithScopeAsync(async sp => (await sp.GetRequiredService<ProvisioningService>().RestoreArchiveAsync(
            await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), newSlug, "demo uchun",
            () => { renamed = true; return Task.CompletedTask; })).Job);
        Assert.True(renamed);
        var t1 = await TenantAsync(tenant.Id);
        Assert.Equal((newSlug, newDb), (t1.Slug, t1.DbName));   // nom navbatga qo'yishdayoq almashdi
        Assert.Contains("\"previousSlug\":\"" + slug + "\"", (await JobAsync(restore.Id)).Payload);

        await RunAsync(worker, restore.Id);
        var t2 = await TenantAsync(tenant.Id);
        Assert.Equal(TenantStatus.Trial, t2.Status);
        Assert.True(t2.RegistryEnabled);
        Assert.Null(t2.ArchiveLocation);
        Assert.True(await DbExistsAsync(newDb));
        Assert.Equal("Arxivdagi ma'lumot", await ScalarAsync(newDb, "SELECT Name FROM dbo.COMPANIES WHERE ID = 1"));
        var registry = new TenantsFile(opts.Paths.TenantsFile!, null);
        Assert.Null(registry.Get(slug));
        Assert.Equal(true, registry.Get(newSlug)?.Enabled);
        Assert.NotNull(Erp(opts).ResolveHost($"{newSlug}.{PanelFactory.BaseDomain}"));

        // eski nom bo'sh — shu nom bilan yangi firma ochiladi
        var again = await f.WithScopeAsync(async sp => await sp.GetRequiredService<TenantService>().CreateAsync(
            new Tenant { Slug = slug, LegalName = "Yangi", Deployment = DeploymentKind.SaaS, ServerId = sid }, true));
        Assert.Equal(dbName, again.DbName);
        // tiklashni qayta bosish — firma endi arxivda emas
        await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(async sp => await sp.GetRequiredService<ProvisioningService>()
            .RestoreArchiveAsync(await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), null, "x")));
    }

    [Fact]
    public async Task Create_IsIdempotent_WhenDatabaseAlreadyCreatedByEarlierAttempt()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv2-" + _suffix);
        var (worker, opts) = Agent(sid, secret);
        string slug = "it2-" + _suffix;
        string dbName = "DB_it2_" + _suffix;
        _databases.Add(dbName);
        var (tenant, enq) = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = "IT2", Deployment = DeploymentKind.SaaS, ServerId = sid }, true);
            return (t, await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create));
        });
        // Oldingi urinish bazani boshqa parol bilan yaratib, panelga javob qaytarolmay qolgan holat
        var r = await new ScriptRunner(opts).RunAsync("New-TenantDb.ps1", ["-Server", f.SqlServer, "-DbName", dbName, "-AdminPassword", "BoshqaParol-1"], new Dictionary<string, string?>(), default);
        Assert.Equal(0, r.ExitCode);
        await RunAsync(worker, enq.Job.Id);
        string hash = (await ScalarAsync(dbName, "SELECT Password FROM dbo.USERS WHERE Uname = 'admin'"))!;
        string salt = (await ScalarAsync(dbName, "SELECT HashCode FROM dbo.USERS WHERE Uname = 'admin'"))!;
        Assert.True(tahsinERP.Helpers.PasswordHasher.Verify(enq.TempPassword!, hash, salt, out _)); // panel ko'rsatgan parol amal qiladi
        Assert.NotNull(Erp(opts).ResolveHost($"{slug}.{PanelFactory.BaseDomain}"));
    }

    /// <summary>
    /// Prod (Lightsail): server collation SQL_Latin1_General_CP1_CI_AS, firma bazasi Cyrillic_General_CI_AS — #temp jadvallar tempdb
    /// collation'ini oladi va migratsiya/demo skriptlari "collation conflict" bilan yiqilardi. Baza server collation'idan farqli bo'lsa ham
    /// baseline + migratsiya + seed + Demo o'tishi kerak.
    /// </summary>
    [Fact]
    public async Task NewTenantDb_Demo_Works_WhenDatabaseCollationDiffersFromServer()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv4-" + _suffix);
        var (_, opts) = Agent(sid, secret);
        string dbName = "DB_coll_" + _suffix;
        _databases.Add(dbName);
        string serverCollation = (await ScalarAsync("master", "SELECT CAST(SERVERPROPERTY('Collation') AS nvarchar(128))"))!;
        string collation = serverCollation.StartsWith("Cyrillic", StringComparison.OrdinalIgnoreCase) ? "SQL_Latin1_General_CP1_CI_AS" : "Cyrillic_General_CI_AS";

        var r = await new ScriptRunner(opts).RunAsync("New-TenantDb.ps1",
            ["-Server", f.SqlServer, "-DbName", dbName, "-Collation", collation, "-Demo", "-NoAdmin"], new Dictionary<string, string?>(), default);

        Assert.True(r.ExitCode == 0, r.Log);
        Assert.Equal(collation, await ScalarAsync("master", $"SELECT collation_name FROM sys.databases WHERE name = '{dbName}'"));
        Assert.Equal("1", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.PRODUCTS WHERE PNo = 'TM-011'"));
    }

    [Fact]
    public async Task DemoReset_ScheduledByAgentAt3am_RestoresFromTemplate()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv3-" + _suffix);
        var (worker, opts) = Agent(sid, secret);
        string dbName = "DB_demo_it_" + _suffix;
        _databases.Add(dbName);
        var demo = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(
                new Tenant { Slug = PanelFactory.DemoSlug, LegalName = "Demo", Deployment = DeploymentKind.SaaS, ServerId = sid, DbName = dbName, SampleData = true }, true);
            var job = (await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create)).Job;
            return (t, job);
        });
        await RunAsync(worker, demo.job.Id);
        // Demo tenantda namuna foydalanuvchisi o'chirilmaydi — birinchi kechagacha ham demo kirishi ishlaydi
        Assert.Equal("1", await ScalarAsync(dbName, "SELECT CAST(IsActive AS int) FROM dbo.USERS WHERE Uname = 'demo'"));
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT CAST(MustChangePassword AS int) FROM dbo.USERS WHERE Uname = 'demo'"));
        Assert.True(tahsinERP.Helpers.PasswordHasher.Verify("DemoParol-2026",
            (await ScalarAsync(dbName, "SELECT Password FROM dbo.USERS WHERE Uname = 'demo'"))!,
            (await ScalarAsync(dbName, "SELECT HashCode FROM dbo.USERS WHERE Uname = 'demo'"))!, out _));
        // Mehmon ma'lumot qo'shdi — reset'dan keyin yo'qolishi kerak
        await ScalarAsync(dbName, "INSERT INTO dbo.CUSTOMERS (Name, IsDeleted) VALUES ('Mehmon yozuvi', 0)");

        // Toshkent 03:30 — agent o'zi Reset rejalashtiradi va bajaradi
        var saved = f.Time.GetUtcNow();
        try
        {
            f.Time.SetUtcNow(new DateTimeOffset(2026, 10, 18, 22, 30, 0, TimeSpan.Zero));
            Assert.True(await worker.RunOnceAsync(default));
            var reset = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().ProvisioningJobs.AsNoTracking()
                .OrderByDescending(j => j.Id).FirstAsync(j => j.TenantId == demo.t.Id));
            Assert.Equal(ProvisioningJobType.Reset, reset.Type);
            Assert.True(reset.Status == ProvisioningJobStatus.Succeeded, reset.Error + "\n" + reset.Log);
            Assert.Equal("demo-reset:" + PanelFactory.DemoSlug + ":2026-10-19", reset.IdempotencyKey);
            Assert.False(await worker.RunOnceAsync(default)); // bugun ikkinchi marta yo'q
        }
        finally { f.Time.SetUtcNow(saved); }

        Assert.True(File.Exists(opts.ResolveDemoTemplate()));
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.CUSTOMERS WHERE Name = 'Mehmon yozuvi'"));
        Assert.Equal("1", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.PRODUCTS WHERE PNo = 'TM-011'")); // Demo/200_Demo_Master.sql
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT CAST(MustChangePassword AS int) FROM dbo.USERS WHERE Uname = 'demo'"));
        Assert.NotNull(await ScalarAsync(dbName, "SELECT Value FROM dbo.LICENSE_STATE WHERE Name = 'RegToken'"));
        Assert.Null(await ScalarAsync(dbName, "SELECT Value FROM dbo.LICENSE_STATE WHERE Name = 'InstallationId'"));
        Assert.Equal(dbName, Erp(opts).ResolveHost($"{PanelFactory.DemoSlug}.{PanelFactory.BaseDomain}")?.Database);
    }
}
