using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Provisioning UI: agent kaliti, «Sinov ochish», job sahifalari, o'chirish tasdig'i, rollar.</summary>
public partial class WebUiTests
{
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
        var trial = await PostAsync(c, "/Tenants/Trial", new() { ["Input.Slug"] = "ui-trial", ["Input.LegalName"] = "UI Trial MChJ" });
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
        string details = await (await c.GetAsync($"/Tenants/Details/{tenantId}")).Content.ReadAsStringAsync();
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
}
