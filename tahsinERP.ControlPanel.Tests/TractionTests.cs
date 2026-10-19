using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;
using tahsinERP.Services.Editions;
using DtoKind = tahsinERP.Provisioning.Contracts.UsageEventKind;
using DtoAction = tahsinERP.Provisioning.Contracts.UsageActionType;

namespace tahsinERP.ControlPanel.Tests;

public class TractionUnitTests
{
    [Theory]
    [InlineData("SO-0001 buyurtma yaratildi", DtoAction.Create)]
    [InlineData("Ali - Foydalanuvchini tizimga qo'shdi", DtoAction.Create)]
    [InlineData("Shartnoma tahrirlandi", DtoAction.Edit)]
    [InlineData("Status o'zgartirildi", DtoAction.Edit)]
    [InlineData("Hujjat o'chirildi", DtoAction.Delete)]
    [InlineData("Excel'ga eksport", DtoAction.Export)]
    [InlineData("Tafsilotlarni ko'rdi", DtoAction.View)]
    [InlineData("Noma'lum", DtoAction.Other)]
    [InlineData(null, DtoAction.Other)]
    public void Classifier_UzbekVerbs(string? text, DtoAction expected) => Assert.Equal(expected, UsageClassifier.Classify(text));

    [Theory]
    [InlineData("SOrderController", "SOrder", ProductModule.Crm)]
    [InlineData("ProductProductionController.Details", "ProductProduction", ProductModule.Mes)]
    [InlineData("Waybill", "Waybill", ProductModule.Logistics)]
    [InlineData("Foydalanuvchilar", "User", ProductModule.Core)]
    [InlineData("ТНВЭД kodlar", "HSCode", ProductModule.Srm)]
    [InlineData("HrPayroll", "HrPayroll", ProductModule.Hr)]
    public void SharedEditionMap_NormalizesLogNames(string log, string controller, ProductModule module)
    {
        Assert.Equal(controller, EditionModules.NormalizeLogController(log));
        Assert.Equal(module, EditionModules.ModuleOf(EditionModules.NormalizeLogController(log)));
        Assert.True(EditionModules.IsMapped(controller));
    }

    [Fact]
    public void SharedEditionMap_PackagesMatchPanelPlans()
    {
        Assert.Equal(["Srm", "Crm", "Tasks"], EditionModules.ModulesOf("START").Select(m => m.ToString()));
        Assert.Equal(9, EditionModules.ModulesOf("TRIAL").Count);
        Assert.Equal(9, EditionModules.ModulesOf("eski-v1").Count); // cheklovsiz
        Assert.False(EditionModules.IsMapped("Foydalanuvchilar"));
    }

    [Fact]
    public void HealthScore_Components()
    {
        var now = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(100, TractionService.HealthScore(now.AddHours(-2), 5, 5, 3, 3, 100, 80, now));
        Assert.Equal(0, TractionService.HealthScore(null, 0, 5, 0, 3, 0, 0, now));
        Assert.Equal(20 + 12 + 7 + 5, TractionService.HealthScore(now.AddDays(-5), 5, 10, 1, 3, 50, 100, now));
        Assert.Equal("Yuqori", TractionService.Risk(39));
        Assert.Equal("O'rta", TractionService.Risk(59));
        Assert.Equal("Past", TractionService.Risk(60));
    }
}

/// <summary>Traction hisob-kitoblari: agent API orqali faollik yuborilib, so'ng TractionService va sahifa tekshiriladi.</summary>
public class TractionTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private async Task<Tenant> TenantAsync(string slug, int serverId, DateTime createdUtc) => await f.WithScopeAsync(async sp =>
    {
        var t = await sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = slug, Deployment = DeploymentKind.SaaS, ServerId = serverId }, true);
        var db = sp.GetRequiredService<ControlDbContext>();
        var x = await db.Tenants.FirstAsync(y => y.Id == t.Id);
        x.ProvisionedAtUtc = createdUtc;
        x.CreatedAtUtc = createdUtc;
        await db.SaveChangesAsync();
        return x;
    });

    [Fact]
    public async Task Report_Overview_Modules_Tenants_Funnel_Cohorts_AndPage()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "tr-srv");
        var agent = ProvisioningQueueTests.Client(f, sid, secret);
        DateTime now = f.Time.GetUtcNow().UtcDateTime; // 2026-10-18 05:00 UTC = 10:00 Toshkent
        var a = await TenantAsync("tr-a", sid, now.AddDays(-10));
        var b = await TenantAsync("tr-b", sid, now.AddDays(-10));

        // tr-a: 2 foydalanuvchi, bugun va kecha faol, SRM va CRM, hujjatlar; tr-b: hech narsa
        // Agent qiladigani kabi: log nomi → normallashtirilgan controller → modul
        UsageEventDto Ev(DtoKind k, long id, int user, DateTime at, string? log = null, DtoAction t = DtoAction.Other)
        {
            string? controller = EditionModules.NormalizeLogController(log);
            return new()
            {
                Kind = k, SourceId = id, UserId = user, AtUtc = at, Controller = controller, ActionType = t,
                Module = controller is null ? null : EditionModules.ModuleOf(controller).ToString(),
            };
        }
        await agent.UsageAsync(new UsageReport
        {
            Slug = "tr-a", CollectedAtUtc = now, EntryCursor = 3, LogCursor = 4,
            Users = [new() { UserId = 1, Login = "admin", IsActive = true }, new() { UserId = 2, Login = "ali", IsActive = true }],
            Events =
            [
                Ev(DtoKind.Login, 1, 1, now.AddHours(-1)), Ev(DtoKind.Login, 2, 2, now.AddHours(-2)), Ev(DtoKind.Login, 3, 1, now.AddDays(-1)),
                Ev(DtoKind.Action, 1, 1, now.AddMinutes(-50), "POrders", DtoAction.Create),
                Ev(DtoKind.Action, 2, 2, now.AddMinutes(-40), "SOrder", DtoAction.Create),
                Ev(DtoKind.Action, 3, 2, now.AddMinutes(-30), "SOrder", DtoAction.Edit),
                Ev(DtoKind.Action, 4, 1, now.AddDays(-1), "Foydalanuvchilar", DtoAction.Create),
            ],
            DocsFromDate = DateOnly.FromDateTime(now.AddDays(-35)),
            Docs = [new() { DocType = UsageDocTypes.SalesOrder, Date = DateOnly.FromDateTime(now), Count = 3 }, new() { DocType = "BEGONA", Date = DateOnly.FromDateTime(now), Count = 9 }],
        }, default);

        var report = await f.WithScopeAsync(sp => sp.GetRequiredService<TractionService>().BuildAsync(
            new TractionService.Filter(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), null, null)));
        var o = report.Overview;
        Assert.Equal(1, o.ActiveTenants);
        Assert.Equal(2, o.Wau);
        Assert.Equal(2, o.Mau);
        Assert.Equal(3, o.DocsTotal); // begona tur qabul qilinmaydi
        Assert.True(o.Dau > 0);

        var crm = report.Modules.Single(m => m.Module == "Crm");
        Assert.Equal(2, crm.Actions);
        Assert.Equal(1, crm.Tenants);
        // TRIAL = KORP: tr-a Tasks/Mes/... ishlatmagan, tr-b hech narsa — Crm faqat tr-b da ishlatilmagan
        Assert.Equal(1, crm.UnusedInPackage);
        Assert.Equal(2, report.Modules.Single(m => m.Module == "Hr").UnusedInPackage);
        Assert.Contains(report.Pages, p => p.Controller == "User" && p.Module == "Core" && p.Mapped);

        var rowA = report.Tenants.Single(t => t.Tenant.Id == a.Id);
        var rowB = report.Tenants.Single(t => t.Tenant.Id == b.Id);
        Assert.Equal(2, rowA.ActiveUsers);
        Assert.Equal(2, rowA.ModulesUsed);
        Assert.Equal(9, rowA.ModulesAllowed);
        Assert.True(rowA.Score > rowB.Score);
        Assert.Equal("Yuqori", rowB.Risk);

        // Funnel: ikkalasi davrda sinov ochgan? (trial StartDate = yaratilgan kun, testda 18.10) → 2 ochildi, 1 kirdi, 1 hujjat
        Assert.Equal([2, 1, 1, 0, 0], report.Funnel.Select(x => x.Count).ToArray());
        Assert.Contains(report.Cohorts, c => c.Size >= 2 && c.Weeks[0] is not null);

        // Sahifa va CSV
        using var c = await WebUiTests.LoginStaticAsync(f, "tr-support@test.uz", StaffRole.Support);
        var page = await c.GetAsync("/Traction?Days=7");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();
        Assert.Contains("tr-a", html);
        Assert.Contains("chart.umd.js", html);
        Assert.Contains("\"funnel\"", html);
        var csv = await c.GetAsync("/Traction?Days=7&handler=Csv&section=tenants");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("tr-b", await csv.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/lib/chartjs/chart.umd.js")).StatusCode);
        foreach (var s in new[] { "modules", "pages", "cohorts" })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/Traction?Days=30&handler=Csv&section={s}")).StatusCode);
    }

    [Fact]
    public async Task Mrr_Conversion_Churn_FromBilling()
    {
        int staff = await f.EnsureStaffAsync();
        var (sid, _) = await ProvisioningQueueTests.CreateServerAsync(f, "tr-srv-b");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            plan.PriceMonthTiyin = 112_000_000;
            await db.SaveChangesAsync();
        });
        var paid = await TenantAsync("tr-paid", sid, DateTime.UtcNow.AddDays(-3));
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == paid.Id);
            var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Biznes);
            var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Month, null, new DateOnly(2026, 10, 18), false);
            var billing = sp.GetRequiredService<BillingService>();
            var inv = await billing.CreateDraftAsync(t.Id, sub.Id, 2, [BillingService.DefaultLine(sub, plan, 2)], null, staff);
            await billing.IssueAsync(inv);
            await billing.RecordPaymentAsync(inv.Id, inv.TotalTiyin, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PP-1", null, staff);
        });
        var r = await f.WithScopeAsync(sp => sp.GetRequiredService<TractionService>().BuildAsync(
            new TractionService.Filter(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 18), null, null)));
        Assert.Equal(112_000_000, r.Tenants.Single(t => t.Tenant.Id == paid.Id).MrrTiyin); // 2 oylik hisob-faktura / 2
        Assert.True(r.Overview.MrrTiyin >= 112_000_000);
        Assert.True(r.Overview.RevenueTiyin >= 224_000_000);
        Assert.True(r.Overview.TrialsConverted >= 1);
        Assert.Equal(0, r.Overview.Churned);
        var biznesOnly = await f.WithScopeAsync(sp => sp.GetRequiredService<TractionService>().BuildAsync(
            new TractionService.Filter(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 18), "BIZNES", null)));
        Assert.All(biznesOnly.Tenants, t => Assert.Equal("BIZNES", t.PlanCode));
    }
}
