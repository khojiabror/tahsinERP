using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Provisioning UI: agent kaliti, «Sinov ochish», job sahifalari, o'chirish tasdig'i, rollar.</summary>
public partial class WebUiTests
{
    /// <summary>Boshqa test klasslari uchun to'liq (2FA bilan) kirgan klient.</summary>
    internal static Task<HttpClient> LoginStaticAsync(PanelFactory factory, string email, StaffRole role) => new WebUiTests(factory).LoginAsync(email, role);

    [Fact]
    public async Task Provisioning_Ui_TrialShowsPasswordOnce_JobPages_AgentKey_DeleteGuard()
    {
        using var c = await LoginAsync("ui-prov-owner@test.uz", StaffRole.Owner);

        // Server + agent kaliti (bir marta ko'rsatiladi)
        var save = await PostAsync(c, "/Servers/Edit", new() { ["Name"] = "ui-srv", ["Host"] = "localhost", ["SqlInstance"] = ".", ["MaxTenants"] = "10", ["IsActive"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        int sid = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Servers.Where(s => s.Name == "ui-srv").Select(s => s.Id).FirstAsync());
        var key = await PostAsync(c, $"/Servers/Edit/{sid}?handler=AgentSecret", [], tokenFrom: $"/Servers/Edit/{sid}");
        string keyHtml = WebUtility.HtmlDecode(await key.Content.ReadAsStringAsync());
        Assert.Matches("<div class=\"mono\">[A-Za-z0-9+/]{43}=</div>", keyHtml);
        Assert.DoesNotMatch("<div class=\"mono\">[A-Za-z0-9+/]{43}=</div>", WebUtility.HtmlDecode(await (await c.GetAsync($"/Servers/Edit/{sid}")).Content.ReadAsStringAsync()));

        // Sinov ochish → parol bir marta, job navbatda
        // modul belgilanmasa — xato, tenant yaratilmaydi
        var noModules = await PostAsync(c, "/Tenants/Trial", new() { ["Input.Slug"] = "ui-trial", ["Input.LegalName"] = "UI Trial MChJ" });
        Assert.Contains("Kamida bitta modulni belgilang", WebUtility.HtmlDecode(await noModules.Content.ReadAsStringAsync()));
        var trial = await PostAsync(c, "/Tenants/Trial", new() { ["Input.Slug"] = "ui-trial", ["Input.LegalName"] = "UI Trial MChJ", ["Input.Modules"] = "Mes" });
        Assert.Equal(HttpStatusCode.OK, trial.StatusCode);
        string trialHtml = await trial.Content.ReadAsStringAsync();
        var m = Regex.Match(trialHtml, "Vaqtinchalik parol: ([A-Za-z0-9]{12})");
        Assert.True(m.Success, trialHtml);
        Assert.Contains("ui-trial.test.local", trialHtml);
        var (tenantId, jobId) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Slug == "ui-trial");
            return (t.Id, (await db.ProvisioningJobs.FirstAsync(j => j.TenantId == t.Id)).Id);
        });
        // tanlangan modul (+ bog'liqligi) tenantga, sinov obunasiga va Create job payload'iga yetadi
        var (tModules, sModules, payload) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenantId);
            var s = await db.Subscriptions.FirstAsync(x => x.TenantId == tenantId);
            return (t.Modules, s.Modules, (await db.ProvisioningJobs.FirstAsync(j => j.Id == jobId)).Payload);
        });
        Assert.Equal("Mes,Engineering", tModules);
        Assert.Equal("Mes,Engineering", sModules);
        Assert.Contains("\"modules\":[\"Mes\",\"Engineering\"]", payload);
        string details = await (await c.GetAsync($"/Tenants/Details/{tenantId}")).Content.ReadAsStringAsync();
        Assert.Contains("Ishlab chiqarish (MES), Muhandislik (BOM, norma, tannarx)", WebUtility.HtmlDecode(details));
        Assert.Contains("Provisioning (baza va ERP reestri)", details);
        Assert.Contains($"/Provisioning/Job/{jobId}", details);
        Assert.DoesNotContain(m.Groups[1].Value, details);
        string job = await (await c.GetAsync($"/Provisioning/Job/{jobId}")).Content.ReadAsStringAsync();
        Assert.Contains("Navbatda", job);
        Assert.DoesNotContain(m.Groups[1].Value, job);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Provisioning")).StatusCode);

        // Bekor qilish (Pending) — tugmalar ishlaydi
        var cancel = await PostAsync(c, $"/Provisioning/Job/{jobId}?handler=Cancel", [], tokenFrom: $"/Provisioning/Job/{jobId}");
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);

        // O'chirish faqat arxivlangan tenant uchun
        var del = await PostAsync(c, $"/Tenants/Delete/{tenantId}", new() { ["ConfirmSlug"] = "ui-trial", ["Reason"] = "x", ["Understand"] = "true", ["Step"] = "2" });
        Assert.Contains("faqat arxivlangan", await del.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Provisioning_Ui_SalesCannotDelete_SupportCannotTrial()
    {
        using var sales = await LoginAsync("ui-prov-sales@test.uz", StaffRole.Sales);
        Assert.Equal(HttpStatusCode.OK, (await sales.GetAsync("/Tenants/Trial")).StatusCode);
        Assert.Contains("/Account/Denied", (await sales.GetAsync("/Tenants/Delete/1")).Headers.Location!.OriginalString);
        using var support = await LoginAsync("ui-prov-support@test.uz", StaffRole.Support);
        Assert.Contains("/Account/Denied", (await support.GetAsync("/Tenants/Trial")).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync("/Provisioning")).StatusCode);
    }

    [Fact]
    public async Task TrialActivity_Ui_SampleCheckbox_EndTrialForm_ActivityPage_Dashboard()
    {
        using var c = await LoginAsync("ui-ta-sales@test.uz", StaffRole.Sales);
        string form = await (await c.GetAsync("/Tenants/Trial")).Content.ReadAsStringAsync();
        Assert.Matches("<input[^>]*(checked[^>]*name=\"Input.SampleData\"|name=\"Input.SampleData\"[^>]*checked)", form);

        var (tenantId, serverId) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var s = new Server { Name = "ui-ta-srv", Host = "h", SqlInstance = ".", MaxTenants = 5, CreatedAtUtc = DateTime.UtcNow };
            db.Servers.Add(s);
            await db.SaveChangesAsync();
            var t = await sp.GetRequiredService<tahsinERP.ControlPanel.Services.TenantService>().CreateAsync(
                new Tenant { Slug = "ui-ta", LegalName = "UI TA", Deployment = DeploymentKind.SaaS, ServerId = s.Id }, true);
            t = await db.Tenants.FirstAsync(x => x.Id == t.Id);
            t.ProvisionedAtUtc = DateTime.UtcNow.AddDays(-10);
            db.UsageStates.Add(new UsageState { TenantId = t.Id, SyncedAtUtc = DateTime.UtcNow, LastLoginUtc = DateTime.UtcNow.AddDays(-6) });
            db.UsageUsers.Add(new UsageUser { TenantId = t.Id, UserId = 1, Login = "admin", IsActive = true });
            db.UsageEvents.Add(new UsageEvent { TenantId = t.Id, Kind = UsageEventKind.Action, SourceId = 1, UserId = 1, AtUtc = DateTime.UtcNow.AddDays(-6), Module = "SOrder", Action = "Buyurtma" });
            await db.SaveChangesAsync();
            return (t.Id, s.Id);
        });
        string details = await (await c.GetAsync($"/Tenants/Details/{tenantId}")).Content.ReadAsStringAsync();
        Assert.Contains("Sinovni yakunlash", details);
        Assert.Contains($"/Tenants/Activity/{tenantId}", details);
        var activity = await c.GetAsync($"/Tenants/Activity/{tenantId}?Days=7&Module=SOrder");
        Assert.Equal(HttpStatusCode.OK, activity.StatusCode);
        Assert.Contains("Buyurtma", await activity.Content.ReadAsStringAsync());
        Assert.Contains("ui-ta", await (await c.GetAsync("/")).Content.ReadAsStringAsync()); // faol emas sinovlar

        // noto'g'ri slug — xato, holat o'zgarmaydi
        var bad = await PostAsync(c, $"/Tenants/Details/{tenantId}?handler=EndTrial", new() { ["EndTrialReason"] = "x", ["EndTrialConfirmSlug"] = "boshqa", ["EndTrialBackup"] = "true" }, tokenFrom: $"/Tenants/Details/{tenantId}");
        Assert.Contains("slug mos kelmadi", await bad.Content.ReadAsStringAsync());
        var ok = await PostAsync(c, $"/Tenants/Details/{tenantId}?handler=EndTrial", new() { ["EndTrialReason"] = "kirmadi", ["EndTrialConfirmSlug"] = "ui-ta", ["EndTrialBackup"] = "true" }, tokenFrom: $"/Tenants/Details/{tenantId}");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal(TenantStatus.Suspended, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Tenants.Where(t => t.Id == tenantId).Select(t => t.Status).FirstAsync()));
    }
}

public partial class WebUiTests
{
    [Fact]
    public async Task Demo_Ui_OwnerOpensDemo_MakeDemoButton_SalesDenied()
    {
        using var c = await LoginAsync("ui-demo-owner@test.uz", StaffRole.Owner);
        await ProvisioningQueueTests.CreateServerAsync(f, "ui-demo-srv");
        Assert.Contains("/Tenants/Demo", await (await c.GetAsync("/Tenants")).Content.ReadAsStringAsync());
        var page = await PostAsync(c, "/Tenants/Demo", new()
        {
            ["Input.Slug"] = "ui-demo", ["Input.LegalName"] = "UI Demo", ["Input.Modules"] = "Distribution", ["Input.Data"] = "full",
        });
        string html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Matches("Vaqtinchalik parol: [A-Za-z0-9]{12}", html);
        var t = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Tenants.AsNoTracking().FirstAsync(x => x.Slug == "ui-demo"));
        Assert.True(t.IsDemo && t.SampleData && t.FullSampleData);
        Assert.Equal("Finance,Commerce,Distribution", t.Modules);   // dala Savdo va Moliyasiz ishlamaydi — bog'liqliklar qo'shiladi
        string details = await (await c.GetAsync($"/Tenants/Details/{t.Id}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("handler=MakeDemo", details);

        var other = await f.CreateTenantAsync("ui-demo-make", trial: true);
        Assert.Contains("handler=MakeDemo", await (await c.GetAsync($"/Tenants/Details/{other.Id}")).Content.ReadAsStringAsync());
        var make = await PostAsync(c, $"/Tenants/Details/{other.Id}?handler=MakeDemo", [], tokenFrom: $"/Tenants/Details/{other.Id}");
        Assert.Equal(HttpStatusCode.Redirect, make.StatusCode);
        Assert.True(await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Tenants.Where(x => x.Id == other.Id).Select(x => x.IsDemo).FirstAsync()));

        using var sales = await LoginAsync("ui-demo-sales@test.uz", StaffRole.Sales);
        Assert.Contains("/Account/Denied", (await sales.GetAsync("/Tenants/Demo")).Headers.Location!.OriginalString);
    }
}
