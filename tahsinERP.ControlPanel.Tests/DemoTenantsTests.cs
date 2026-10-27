using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>To'lovsiz demo firmalar (DemoService): ochish, mavjud firmani demo qilish, kunlik avto-uzaytirish, eslatmalardan chetlash.</summary>
public class DemoTenantsTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(PanelFactory.Start.UtcDateTime);

    [Fact]
    public async Task Create_OpensDemo_YearSubscription_TenUsers_FullSamplePayload()
    {
        var (sid, _) = await ProvisioningQueueTests.CreateServerAsync(f, "demo-srv-1");
        var r = await f.WithScopeAsync(sp => sp.GetRequiredService<DemoService>().CreateAsync(new Tenant
        {
            Slug = "demo-dist-t", LegalName = "Demo Distributsiya", CompanyId = 1, ServerId = sid,
            SampleData = true, FullSampleData = true, Modules = "Distribution,Website",
        }));
        Assert.True(r.Created);
        Assert.False(string.IsNullOrEmpty(r.TempPassword));
        var payload = JsonSerializer.Deserialize<JobPayload>(r.Job.Payload, AgentProtocol.Json)!;
        Assert.True(payload.SampleData);
        Assert.True(payload.FullSample);
        Assert.False(payload.Demo);   // ommaviy demo emas — tungi Reset va demo foydalanuvchi yo'q
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.Include(x => x.Subscriptions).SingleAsync(x => x.Slug == "demo-dist-t");
            Assert.True(t.IsDemo);
            Assert.Equal(DeploymentKind.SaaS, t.Deployment);
            var s = t.Subscriptions.Single(x => x.Status == SubscriptionStatus.Active);
            Assert.Equal(BillingPeriod.Trial, s.Period);
            Assert.Equal(Today.AddYears(DemoService.Years).AddDays(-1), s.EndDate);
            Assert.True(s.Users >= DemoService.Users);
            Assert.True(sp.GetRequiredService<ProvisioningService>().IsDemo(t));
            Assert.False(sp.GetRequiredService<ProvisioningService>().IsPublicDemo(t));
        });
    }

    [Fact]
    public async Task MakeDemo_ConvertsTrial_RefusesPaidAndRepeat()
    {
        var trial = await f.CreateTenantAsync("demo-make-t", trial: true);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var demos = sp.GetRequiredService<DemoService>();
            var t = await db.Tenants.SingleAsync(x => x.Id == trial.Id);
            await demos.MakeDemoAsync(t);
            Assert.True(t.IsDemo);
            var s = await db.Subscriptions.SingleAsync(x => x.TenantId == t.Id && x.Status == SubscriptionStatus.Active);
            Assert.Equal(Today.AddYears(1).AddDays(-1), s.EndDate);
            Assert.True(s.Users >= DemoService.Users);
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "tenant.demo" && a.EntityId == t.Id.ToString()));
            await Assert.ThrowsAsync<DomainException>(() => demos.MakeDemoAsync(t));
        });

        var paid = await f.CreateTenantAsync("demo-paid-t", trial: false);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            db.Subscriptions.Add(new Subscription
            {
                TenantId = paid.Id, PlanId = plan.Id, Users = 5, Period = BillingPeriod.Month, StartDate = Today, EndDate = Today.AddMonths(1),
                Status = SubscriptionStatus.Active,
            });
            await db.SaveChangesAsync();
            var t = await db.Tenants.SingleAsync(x => x.Id == paid.Id);
            await Assert.ThrowsAsync<DomainException>(() => sp.GetRequiredService<DemoService>().MakeDemoAsync(t));
            Assert.False((await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == paid.Id)).IsDemo);
        });
    }

    [Fact]
    public async Task Lifecycle_ExtendsDemo_InsteadOfSuspending_AndNoReminders()
    {
        var created = await f.CreateTenantAsync("demo-life-t", trial: true);
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.Tenants.SingleAsync(x => x.Id == created.Id)).IsDemo = true;
            await db.SaveChangesAsync();
        });
        // Oddiy sinov 31.10 da tugaydi: 24.10 — eslatma yo'q, 01.11 — to'xtatilmaydi, obuna bir yilga uzaygan.
        f.Time.SetUtcNow(new DateTimeOffset(2026, 10, 24, 5, 0, 0, TimeSpan.Zero));
        try
        {
            await f.WithScopeAsync(sp => sp.GetRequiredService<ExpiryReminderService>().RunAsync());
            await f.WithScopeAsync(async sp =>
                Assert.False(await sp.GetRequiredService<ControlDbContext>().Notifications.AnyAsync(n => n.TenantId == created.Id)));
            f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero));
            await f.WithScopeAsync(sp => sp.GetRequiredService<SubscriptionService>().RunLifecycleAsync());
            await f.WithScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<ControlDbContext>();
                var t = await db.Tenants.Include(x => x.Subscriptions).SingleAsync(x => x.Id == created.Id);
                Assert.Equal(TenantStatus.Trial, t.Status);
                var s = t.Subscriptions.Single(x => x.Status == SubscriptionStatus.Active);
                Assert.True(s.EndDate > new DateOnly(2027, 10, 1));
                Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "subscription.demo-renew" && a.EntityId == s.Id.ToString()));
            });
        }
        finally { f.Time.SetUtcNow(PanelFactory.Start); }
    }
}
