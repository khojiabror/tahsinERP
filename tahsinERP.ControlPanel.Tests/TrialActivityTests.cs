using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;
using DataKind = tahsinERP.ControlPanel.Data.UsageEventKind;
using DtoKind = tahsinERP.Provisioning.Contracts.UsageEventKind;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Sinovni erta yakunlash va faollik — panel tomoni (agent API orqali, SQL'da faqat panel test bazasi).</summary>
public class TrialActivityQueueTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private Task<Tenant> TenantOnAsync(string slug, int serverId, bool trial = true, bool provisioned = false) => f.WithScopeAsync(async sp =>
    {
        var t = await sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = slug, Deployment = DeploymentKind.SaaS, ServerId = serverId }, trial);
        if (provisioned)
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var x = await db.Tenants.FirstAsync(y => y.Id == t.Id);
            x.ProvisionedAtUtc = f.Time.GetUtcNow().UtcDateTime.AddDays(-5);
            x.RegistryEnabled = true;
            await db.SaveChangesAsync();
        }
        return t;
    });

    private Task<T> Prov<T>(Func<ProvisioningService, ControlDbContext, Task<T>> a) => f.WithScopeAsync(sp =>
        a(sp.GetRequiredService<ProvisioningService>(), sp.GetRequiredService<ControlDbContext>()));

    [Fact]
    public async Task EndTrial_Validations_ThenJob_ArchivesAndDeletesWithoutBackup()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "et-srv-1");
        var agent = ProvisioningQueueTests.Client(f, sid, secret);
        var t = await TenantOnAsync("et-a", sid, provisioned: true);

        await Assert.ThrowsAsync<DomainException>(() => Prov(async (p, db) => await p.EndTrialAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), "", "et-a", true)));
        await Assert.ThrowsAsync<DomainException>(() => Prov(async (p, db) => await p.EndTrialAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), "kirmadi", "noto'g'ri", true)));

        var job = await Prov(async (p, db) => await p.EndTrialAsync(await db.Tenants.FirstAsync(x => x.Id == t.Id), "kirmadi", "et-a", backup: false));
        Assert.NotNull(job);
        var (tenant, subs) = await Prov(async (_, db) => (await db.Tenants.FirstAsync(x => x.Id == t.Id), await db.Subscriptions.Where(s => s.TenantId == t.Id).ToListAsync()));
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.All(subs, s => Assert.Equal(SubscriptionStatus.Cancelled, s.Status));

        var lease = (await agent.LeaseAsync(default))!;
        Assert.Equal(JobKind.EndTrial, lease.Type);
        Assert.False(lease.Payload.Backup);
        await agent.CompleteAsync(lease.JobId, new JobCompletion { LeaseToken = lease.LeaseToken, Outcome = JobOutcome.Succeeded }, default);
        tenant = await Prov((_, db) => db.Tenants.FirstAsync(x => x.Id == t.Id));
        Assert.Equal(TenantStatus.Archived, tenant.Status);
        Assert.NotNull(tenant.TrialEndedAtUtc);
        Assert.NotNull(tenant.DeletedAtUtc); // arxivsiz — butunlay o'chirildi
        Assert.Null(tenant.RegistryEnabled);
        Assert.Null(await agent.LeaseAsync(default)); // reconcile hech narsa qo'shmaydi

        var actions = await Prov((_, db) => db.AuditLog.Where(a => a.EntityId == t.Id.ToString() && a.Entity == "Tenant").Select(a => a.Action).ToListAsync());
        Assert.Contains("tenant.trial-end", actions);
        Assert.Contains("provisioning.succeeded", actions);
    }

    [Fact]
    public async Task EndTrial_NotForPaidTenant_AndNoDbArchivesImmediately()
    {
        var (sid, _) = await ProvisioningQueueTests.CreateServerAsync(f, "et-srv-2");
        var paid = await TenantOnAsync("et-paid", sid, provisioned: true);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Start);
            await sp.GetRequiredService<SubscriptionService>().CreateAsync(await db.Tenants.FirstAsync(x => x.Id == paid.Id), plan.Id, BillingPeriod.Month, null, new DateOnly(2026, 10, 18), false);
        });
        var ex = await Assert.ThrowsAsync<DomainException>(() => Prov(async (p, db) => await p.EndTrialAsync(await db.Tenants.FirstAsync(x => x.Id == paid.Id), "x", "et-paid", true)));
        Assert.Contains("pullik", ex.Message);

        var noDb = await TenantOnAsync("et-nodb", sid);
        Assert.Null(await Prov(async (p, db) => await p.EndTrialAsync(await db.Tenants.FirstAsync(x => x.Id == noDb.Id), "x", "et-nodb", true)));
        Assert.Equal(TenantStatus.Archived, (await Prov((_, db) => db.Tenants.FirstAsync(x => x.Id == noDb.Id))).Status);
    }

    [Fact]
    public async Task Usage_Targets_Ingest_Dedupe_Reset_InactiveTrials_Purge()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "us-srv-1");
        var agent = ProvisioningQueueTests.Client(f, sid, secret);
        var active = await TenantOnAsync("us-active", sid, provisioned: true);
        var idle = await TenantOnAsync("us-idle", sid, provisioned: true);
        await TenantOnAsync("us-nodb", sid); // baza yo'q — maqsad emas

        var targets = await agent.UsageTargetsAsync(default);
        Assert.Equal(["us-active", "us-idle"], targets.Targets.Select(x => x.Slug).OrderBy(x => x).ToArray());
        Assert.All(targets.Targets, x => Assert.Equal(0, x.LogCursor));

        DateTime now = f.Time.GetUtcNow().UtcDateTime;
        var report = new UsageReport
        {
            Slug = "us-active", CollectedAtUtc = now, EntryCursor = 2, LogCursor = 3,
            Users = [new() { UserId = 1, Login = "admin", IsActive = true }, new() { UserId = 2, Login = "ali", IsActive = true }],
            Events =
            [
                new() { Kind = DtoKind.Login, SourceId = 1, UserId = 1, AtUtc = now.AddHours(-30) },
                new() { Kind = DtoKind.Login, SourceId = 2, UserId = 2, AtUtc = now.AddHours(-1) },
                new() { Kind = DtoKind.Action, SourceId = 1, UserId = 2, AtUtc = now.AddMinutes(-50), Module = "SRM", Action = "Buyurtma yaratildi" },
                new() { Kind = DtoKind.Action, SourceId = 2, UserId = 2, AtUtc = now.AddMinutes(-40), Module = "SRM", Action = "Buyurtma tahrirlandi" },
                new() { Kind = DtoKind.Action, SourceId = 3, UserId = 1, AtUtc = now.AddDays(-200), Module = "Old", Action = "eski" }, // saqlash muddatidan tashqari
            ],
        };
        await agent.UsageAsync(report, default);
        await agent.UsageAsync(report, default); // takror — dublikat yo'q
        var targets2 = await agent.UsageTargetsAsync(default);
        Assert.Equal(["us-idle"], targets2.Targets.Select(x => x.Slug).ToArray()); // us-active endi 15 daqiqa kutadi

        var (events, state, users) = await Prov(async (_, db) => (
            await db.UsageEvents.Where(e => e.TenantId == active.Id).ToListAsync(),
            await db.UsageStates.FirstAsync(s => s.TenantId == active.Id),
            await db.UsageUsers.Where(u => u.TenantId == active.Id).ToListAsync()));
        Assert.Equal(4, events.Count);
        Assert.Equal(3, state.LogCursor);
        Assert.Equal(now.AddHours(-1), state.LastLoginUtc);
        Assert.Equal(now.AddMinutes(-40), users.Single(u => u.Login == "ali").LastActionUtc);

        // Demo reset: ID'lar qaytadan — vaqti boshqa bo'lgani uchun yangi yozuv, foydalanuvchi ro'yxati yangilanadi
        f.Time.Advance(TimeSpan.FromMinutes(20));
        await agent.UsageAsync(new UsageReport
        {
            Slug = "us-active", CollectedAtUtc = now, ResetDetected = true, EntryCursor = 1, LogCursor = 0,
            Users = [new() { UserId = 1, Login = "admin", IsActive = true }],
            Events = [new() { Kind = DtoKind.Login, SourceId = 1, UserId = 1, AtUtc = now.AddMinutes(10) }],
        }, default);
        Assert.Equal(5, await Prov((_, db) => db.UsageEvents.CountAsync(e => e.TenantId == active.Id)));
        Assert.Equal(["admin"], await Prov((_, db) => db.UsageUsers.Where(u => u.TenantId == active.Id).Select(u => u.Login).ToArrayAsync()));

        // Faol emas sinovlar: us-idle (baza 5 kun oldin, kirish yo'q); us-active kirgan
        var inactive = await f.WithScopeAsync(sp => sp.GetRequiredService<UsageService>().InactiveTrialsAsync());
        Assert.Contains(inactive, x => x.Tenant.Id == idle.Id);
        Assert.DoesNotContain(inactive, x => x.Tenant.Id == active.Id);

        // Tenant boshqa serverda bo'lsa — rad
        var (sid2, secret2) = await ProvisioningQueueTests.CreateServerAsync(f, "us-srv-2");
        var ex = await Assert.ThrowsAsync<PanelClient.PanelException>(() =>
            ProvisioningQueueTests.Client(f, sid2, secret2).UsageAsync(new UsageReport { Slug = "us-active" }, default));
        Assert.Equal((System.Net.HttpStatusCode)422, ex.Status);

        // 90 kundan eski yozuvlar kunlik ishda o'chadi
        f.Time.Advance(TimeSpan.FromDays(89.9));
        int purged = await f.WithScopeAsync(sp => sp.GetRequiredService<UsageService>().PurgeAsync());
        Assert.True(purged >= 1);
        f.Time.SetUtcNow(PanelFactory.Start);
    }

    [Fact]
    public void Redactor_MasksEmailPhoneLongNumbers_AndTruncates()
    {
        string s = UsageRedactor.Redact("Mijoz ali@firma.uz +998 90 123-45-67 STIR 123456789 buyurtma #42 yaratildi");
        Assert.DoesNotContain("ali@firma.uz", s);
        Assert.DoesNotContain("123-45-67", s);
        Assert.DoesNotContain("123456789", s);
        Assert.Contains("#42", s);
        Assert.Equal(UsageRedactor.MaxLength + 1, UsageRedactor.Redact(new string('a', 500)).Length);
    }
}

/// <summary>Integratsion (lokal SQL): namuna ma'lumotli sinov, faollik yig'ish, sinovni erta yakunlash (arxiv bilan).</summary>
public partial class ProvisioningIntegrationTests
{
    [Fact]
    public async Task SampleTrial_UsageCollected_ThenEndTrialWithBackup()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv4-" + _suffix);
        var (worker, opts) = Agent(sid, secret);
        string slug = "it4-" + _suffix;
        string dbName = "DB_it4_" + _suffix;
        _databases.Add(dbName);

        var (tenant, enq) = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(
                new Tenant { Slug = slug, LegalName = "\"Namuna\" MChJ", Deployment = DeploymentKind.SaaS, ServerId = sid, SampleData = true }, startTrial: true);
            return (t, await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create));
        });
        Assert.True((await JobAsync(enq.Job.Id)).Payload.Contains("\"sampleData\":true"));
        await RunAsync(worker, enq.Job.Id);

        // Namuna ma'lumot bor, demo foydalanuvchi o'chirilgan, admin — vaqtinchalik parol bilan
        Assert.NotEqual("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.PRODUCTS"));
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT CAST(IsActive AS int) FROM dbo.USERS WHERE Uname = 'demo'"));
        string demoHash = (await ScalarAsync(dbName, "SELECT Password FROM dbo.USERS WHERE Uname = 'demo'"))!;
        string demoSalt = (await ScalarAsync(dbName, "SELECT HashCode FROM dbo.USERS WHERE Uname = 'demo'"))!;
        Assert.False(tahsinERP.Helpers.PasswordHasher.Verify("demo2026", demoHash, demoSalt, out _));
        Assert.Equal("1", await ScalarAsync(dbName, "SELECT CAST(MustChangePassword AS int) FROM dbo.USERS WHERE Uname = 'admin'"));
        Assert.Equal("\"Namuna\" MChJ", await ScalarAsync(dbName, "SELECT Name FROM dbo.COMPANIES WHERE ID = 1"));

        // ERP'da kirish va amal (ERP DateTime.Now bilan yozadi), maxfiy ma'lumot bilan
        string adminId = (await ScalarAsync(dbName, "SELECT ID FROM dbo.USERS WHERE Uname = 'admin'"))!;
        await ScalarAsync(dbName, $"INSERT INTO dbo.USER_ENTRIES (UserID, [DateTime], IP, MAC) VALUES ({adminId}, GETDATE(), '10.1.2.3', 'AA-BB')");
        await ScalarAsync(dbName, $"INSERT INTO dbo.USERLOGS (ControllerName, ActionName, [DateTime], UserID, IP, MacAddr) VALUES ('SOrder', N'Buyurtma: mijoz info@client.uz tel +998901234567', GETDATE(), {adminId}, '10.1.2.3', 'AA-BB')");

        var sp2 = _agentServices!;
        // ERP yozuvlari haqiqiy vaqt bilan — panel soati ham haqiqiy vaqtga (aks holda "kelajakdagi" yozuv deb rad etiladi)
        f.Time.SetUtcNow(DateTimeOffset.UtcNow);
        int sent = await worker.CollectUsageAsync(sp2.GetRequiredService<PanelClient>(), sp2.GetRequiredService<UsageCollector>(), default);
        Assert.True(sent >= 1);
        var (events, users, state) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            return (await db.UsageEvents.Where(e => e.TenantId == tenant.Id).ToListAsync(),
                await db.UsageUsers.Where(u => u.TenantId == tenant.Id).ToListAsync(),
                await db.UsageStates.FirstAsync(s => s.TenantId == tenant.Id));
        });
        Assert.Null(state.Error);
        Assert.Contains(events, e => e.Kind == DataKind.Login);
        var action = Assert.Single(events, e => e.Kind == DataKind.Action && e.Controller == "SOrder");
        Assert.Equal("Crm", action.Module); // EditionModules xaritasi (umumiy kutubxona)
        Assert.DoesNotContain("info@client.uz", action.Action);
        Assert.DoesNotContain("901234567", action.Action);
        Assert.Contains(users, u => u.Login == "admin" && u.LastLoginUtc != null);
        Assert.Contains(users, u => u.Login == "demo" && !u.IsActive);
        Assert.NotNull(state.LastLoginUtc);
        Assert.True(Math.Abs((state.LastLoginUtc!.Value - DateTime.UtcNow).TotalMinutes) < 5, "lokal → UTC konvertatsiya");

        // Faollik sahifasi uchun xulosa
        var summary = await f.WithScopeAsync(sp => sp.GetRequiredService<UsageService>().SummaryAsync(tenant.Id, 7));
        Assert.Contains(summary.Modules, m => m.Module == "Crm");
        // Traction agregatlari: kunlik modul va faol foydalanuvchi, namuna hujjatlar soni
        var (dailyModules, dailyUsers, dailyDocs) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            return (await db.UsageDailyModules.Where(x => x.TenantId == tenant.Id).ToListAsync(),
                await db.UsageDailyUsers.Where(x => x.TenantId == tenant.Id).ToListAsync(),
                await db.UsageDailyDocs.Where(x => x.TenantId == tenant.Id).ToListAsync());
        });
        Assert.Contains(dailyModules, x => x.Module == "Crm" && x.Controller == "SOrder" && x.Count == 1);
        Assert.Contains(dailyUsers, x => x.Logins == 1 && x.Actions == 1);
        Assert.Contains(dailyDocs, x => x.DocType == UsageDocTypes.SalesOrder && x.Count > 0); // Database/Demo sotuv buyurtmalari

        // Sinovni erta yakunlash: arxiv bilan → baza o'chdi, tenants.json'da yo'q, .bak bor
        var job = await f.WithScopeAsync(async sp => await sp.GetRequiredService<ProvisioningService>().EndTrialAsync(
            await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(t => t.Id == tenant.Id), "mijoz ishlatmayapti", slug, backup: true));
        await RunAsync(worker, job!.Id);
        var t2 = await TenantAsync(tenant.Id);
        Assert.Equal(TenantStatus.Archived, t2.Status);
        Assert.NotNull(t2.TrialEndedAtUtc);
        Assert.Null(t2.DeletedAtUtc);
        Assert.False(await DbExistsAsync(dbName));
        Assert.Null(new TenantsFile(opts.Paths.TenantsFile!, null).Get(slug));
        Assert.True(File.Exists(t2.ArchiveLocation!["file:".Length..]));
        // arxivlangan tenant faollik maqsadi emas
        Assert.DoesNotContain((await sp2.GetRequiredService<PanelClient>().UsageTargetsAsync(default)).Targets, x => x.Slug == slug);
        f.Time.SetUtcNow(PanelFactory.Start);
    }
}
