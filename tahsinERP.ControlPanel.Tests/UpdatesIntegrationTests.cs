using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;
using tahsinERP.Updater;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>GitHub o'rniga: test reliz papkasidagi fayllar (asset id = tartib raqami).</summary>
public sealed class FakeReleaseSource : IReleaseSource
{
    public List<(string Tag, string Dir)> Releases { get; } = [];
    /// <summary>true — ZIP'lar buzib beriladi (yo'lda o'zgartirilgan paket).</summary>
    public bool CorruptZips { get; set; }
    public bool IsConfigured => true;

    private IEnumerable<(long Id, string Path)> Files() =>
        Releases.SelectMany((r, ri) => Directory.GetFiles(r.Dir).OrderBy(x => x).Select((p, fi) => ((long)(ri * 100 + fi + 1), p)));

    public Task<IReadOnlyList<ReleaseListing>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseListing>>(
        Releases.Select((r, ri) => new ReleaseListing(r.Tag, r.Tag, "### erp\n- test o'zgarish", false, false, DateTime.UtcNow,
            Directory.GetFiles(r.Dir).OrderBy(x => x).Select((p, fi) => new ReleaseAsset(ri * 100 + fi + 1, Path.GetFileName(p), new FileInfo(p).Length)).ToList()))
        .ToList());

    public Task<Stream> OpenAssetAsync(long assetId, CancellationToken ct)
    {
        string path = Files().First(f => f.Id == assetId).Path;
        byte[] bytes = File.ReadAllBytes(path);
        if (CorruptZips && path.EndsWith(".zip", StringComparison.Ordinal)) bytes[^1] ^= 0xFF;
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}

/// <summary>Panel ↔ agent (UpdateRunner) ↔ Updater (InboxWorker, soxta tizim amallari) — to'liq oqim, lokal SQL bilan.</summary>
public sealed class UpdatesIntegrationTests(PanelFactory f) : IClassFixture<PanelFactory>, IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "upd-int-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly TestReleaseKit _kit = new();
    private readonly FakeReleaseSource _source = new();

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Panel() => f.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
        s.RemoveAll<IReleaseSource>();
        s.AddSingleton<IReleaseSource>(_source);
        s.RemoveAll<ReleaseTrust>();
        s.AddSingleton(new ReleaseTrust { Keys = _kit.Trusted });
    }));

    [Fact]
    public async Task Release_Detected_CreateInstance_ThroughAgentAndUpdater_InventoryAndGuards()
    {
        string suffix = Guid.NewGuid().ToString("N")[..6];
        string slug = "upd-" + suffix;
        _kit.Build(Path.Combine(_work, "rel1"), "2026.10.22.1", new() { ["erp"] = TestReleaseKit.ErpFiles("r1"), ["panel"] = new() { ["p.dll"] = "p" } },
            changed: new HashSet<string> { "erp" });
        _source.Releases.Add(("v2026.10.22.1", Path.Combine(_work, "rel1")));
        // Imzosi boshqa kalit bilan — yaroqsiz deb belgilanadi
        new TestReleaseKit().Build(Path.Combine(_work, "rel2"), "2026.10.22.2", new() { ["erp"] = TestReleaseKit.ErpFiles("r2") });
        _source.Releases.Add(("v2026.10.22.2", Path.Combine(_work, "rel2")));

        using var panel = Panel();
        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        int serverId = 0, tenantId = 0;
        await WithAsync(panel, async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var server = new Server
            {
                Name = "upd-srv-" + suffix, Host = "localhost", SqlInstance = ".", CreatedAtUtc = DateTime.UtcNow,
                AgentSecretProtected = sp.GetRequiredService<ISecretProtector>().Protect(secret, SecretPurposes.AgentSecret),
            };
            db.Servers.Add(server);
            await db.SaveChangesAsync();
            serverId = server.Id;
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = slug, Deployment = DeploymentKind.SaaS, CompanyId = 1 }, true);
            t = await db.Tenants.FirstAsync(x => x.Id == t.Id);
            t.ServerId = serverId;
            t.DbName = "DB_" + slug.Replace('-', '_');
            await db.SaveChangesAsync();
            tenantId = t.Id;

            Assert.Equal(2, await sp.GetRequiredService<UpdatesService>().RefreshAsync(default));
            var rels = await db.AppReleases.AsNoTracking().Where(r => r.Tag.StartsWith("v2026.10.22.")).ToListAsync();
            Assert.True(rels.Single(r => r.Version == "2026.10.22.1").SignatureValid);
            Assert.False(rels.Single(r => r.Version == "2026.10.22.2").SignatureValid);
            Assert.Equal("erp*,panel", rels.Single(r => r.Version == "2026.10.22.1").Apps);
        });

        // Agent va Updater (bir server, umumiy Updates papkasi)
        var agentOpts = new AgentOptions
        {
            Panel = { Url = "https://localhost", ServerId = serverId, Secret = secret },
            Sql = { Server = f.SqlServer },
            Paths = { StateDir = Path.Combine(_work, "agent") },
            Updates = { UpdatesDir = Path.Combine(_work, "Updates"), UpdaterStateFile = Path.Combine(_work, "state.json"), UpdaterStartTimeoutSeconds = 60 },
        };
        var client = new PanelClient(new HttpClient(panel.Server.CreateHandler()), agentOpts, f.Time,
            new HttpClient(panel.Server.CreateHandler()) { BaseAddress = new Uri("https://localhost/") });
        var runner = new UpdateRunner(agentOpts, new SqlOps(agentOpts), f.Time, NullLogger<UpdateRunner>.Instance) { TrustedKeys = _kit.Trusted };
        var updOpts = new UpdaterOptions
        {
            Domain = "test.local", HealthTimeoutSeconds = 5,
            Paths =
            {
                UpdatesDir = agentOpts.Updates.UpdatesDir, StateFile = agentOpts.Updates.UpdaterStateFile, ReleasesDir = Path.Combine(_work, "releases"),
                BackupRoot = Path.Combine(_work, "_backup"), DbBackupDir = Path.Combine(_work, "bak"), DatabaseRoot = Path.Combine(_work, "Database"),
                DataDir = Path.Combine(_work, "data"),
            },
            Erp = { InstancesRoot = Path.Combine(_work, "erp"), SharedPath = Path.Combine(_work, "shared") },
        };
        var store = new StateStore(updOpts);
        store.Update(s => s.UpdaterVersion = "1.0.0");
        var sys = new FakeSystemOps();
        var updater = new InboxWorker(updOpts, new UpdateExecutor(updOpts, sys, store, NullLogger<UpdateExecutor>.Instance) { TrustedKeys = _kit.Trusted },
            store, NullLogger<InboxWorker>.Instance);

        // Inventar: Updater bor, instance yo'q
        Assert.False(await runner.RunOnceAsync(client, default));
        await WithAsync(panel, async sp =>
        {
            var svc = sp.GetRequiredService<UpdatesService>();
            var (inv, _) = await svc.InventoryAsync(serverId);
            Assert.Equal("1.0.0", inv!.UpdaterVersion);
            Assert.Empty(inv.Instances);
            // Yaroqsiz imzoli reliz va instance'siz yangilash rad etiladi
            await Assert.ThrowsAsync<DomainException>(() => svc.EnqueueAsync(serverId, UpdateAction.CreateInstance, "erp", tenantId, "2026.10.22.2", false));
            await Assert.ThrowsAsync<DomainException>(() => svc.EnqueueAsync(serverId, UpdateAction.InstallTenant, "erp", tenantId, "2026.10.22.1", false));
            await svc.EnqueueAsync(serverId, UpdateAction.CreateInstance, "erp", tenantId, "2026.10.22.1", false);
            await Assert.ThrowsAsync<DomainException>(() => svc.EnqueueAsync(serverId, UpdateAction.CreateInstance, "erp", tenantId, "2026.10.22.1", false));
        });

        // Updater fon rejimida inbox'ni kuzatadi; agent job'ni oladi va natijani kutadi
        using var stop = new CancellationTokenSource();
        var updaterLoop = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await updater.ProcessPendingAsync(default);
                try { await Task.Delay(300, stop.Token); } catch (OperationCanceledException) { }
            }
        });
        Assert.True(await runner.RunOnceAsync(client, default));
        await stop.CancelAsync();
        await updaterLoop;

        UpdateJob job = null!;
        await WithAsync(panel, async sp => job = await sp.GetRequiredService<ControlDbContext>().UpdateJobs.AsNoTracking().OrderByDescending(j => j.Id).FirstAsync(j => j.ServerId == serverId));
        Assert.True(job.Status == UpdateJobStatus.Succeeded, $"{job.Status}: {job.Error}\n{job.Log}");
        Assert.Equal("2026.10.22.1", job.InstalledVersion);
        Assert.Contains("Imzo OK", job.Log);
        Assert.Contains(sys.Calls, c => c.StartsWith("ensure-site erp-" + slug));
        Assert.False(File.Exists(Path.Combine(agentOpts.Paths.StateDir, "update-pending.json")));

        // Keyingi aylanish — inventarda yangi instance
        Assert.False(await runner.RunOnceAsync(client, default));
        await WithAsync(panel, async sp =>
        {
            var (inv, _) = await sp.GetRequiredService<UpdatesService>().InventoryAsync(serverId);
            var inst = Assert.Single(inv!.Instances);
            Assert.Equal(slug, inst.Slug);
            Assert.Equal("2026.10.22.1", inst.Version);
            // Bir xil versiyani qayta o'rnatish rad etiladi
            await Assert.ThrowsAsync<DomainException>(() => sp.GetRequiredService<UpdatesService>()
                .EnqueueAsync(serverId, UpdateAction.InstallTenant, "erp", tenantId, "2026.10.22.1", false));
        });

        // Yo'lda buzilgan paket: agent erta tekshiruvda to'xtatadi, Updater'gacha yetmaydi
        _kit.Build(Path.Combine(_work, "rel3"), "2026.10.23.1", new() { ["erp"] = TestReleaseKit.ErpFiles("r3") });
        _source.Releases.Add(("v2026.10.23.1", Path.Combine(_work, "rel3")));
        _source.CorruptZips = true;
        await WithAsync(panel, async sp =>
        {
            await sp.GetRequiredService<UpdatesService>().RefreshAsync(default);
            await sp.GetRequiredService<UpdatesService>().EnqueueAsync(serverId, UpdateAction.InstallTenant, "erp", tenantId, "2026.10.23.1", false);
        });
        sys.Calls.Clear();
        Assert.True(await runner.RunOnceAsync(client, default));
        await WithAsync(panel, async sp => job = await sp.GetRequiredService<ControlDbContext>().UpdateJobs.AsNoTracking().OrderByDescending(j => j.Id).FirstAsync(j => j.ServerId == serverId));
        Assert.Equal(UpdateJobStatus.Failed, job.Status);
        Assert.Contains("mos emas", job.Error);
        Assert.Empty(sys.Calls);
        Assert.Empty(Directory.GetDirectories(Path.Combine(agentOpts.Updates.UpdatesDir, "Inbox")));
    }

    private static async Task WithAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, Func<IServiceProvider, Task> action)
    {
        using var scope = app.Services.CreateScope();
        await action(scope.ServiceProvider);
    }
}
