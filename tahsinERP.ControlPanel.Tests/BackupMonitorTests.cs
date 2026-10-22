using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Kunlik zaxira holati: PowerShell yozgan fayl → agent inventari → Serverlar sahifasi va bosh sahifa ogohlantirishi.</summary>
public class BackupMonitorTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    // Backup-Databases.ps1 (ConvertTo-Json, PowerShell 5.1) yozadigan ko'rinish
    private const string PsJson = """
        {
            "Ok":  false,
            "StartedAt":  "2026-10-22T01:30:00",
            "FinishedAt":  "2026-10-22T01:31:10",
            "StartedAtUtc":  "2026-10-21T20:30:00Z",
            "FinishedAtUtc":  "2026-10-21T20:31:10Z",
            "Bucket":  "",
            "Folder":  "C:\\tahsinERP\\Backups\\2026-10-22_0130",
            "Files":  [ "DB_ipack.bak.gz", "config.zip" ],
            "TotalBytes":  3145728,
            "Uploaded":  [ ],
            "Errors":  [ "DB_demo : disk to'la" ]
        }
        """;

    [Fact]
    public void Agent_ReadsPowerShellStatusFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "backup-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "\uFEFF" + PsJson);
        try
        {
            var r = UpdateRunner.ReadBackupReport(path)!;
            Assert.False(r.Ok);
            Assert.Equal(new DateTime(2026, 10, 21, 20, 31, 10), r.FinishedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(["DB_ipack.bak.gz", "config.zip"], r.Files);
            Assert.Equal(3145728, r.TotalBytes);
            Assert.Single(r.Errors);
        }
        finally { File.Delete(path); }
        Assert.Null(UpdateRunner.ReadBackupReport(path));
    }

    [Fact]
    public void Health_OkStaleFailedUnknown()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(BackupHealth.Unknown, BackupMonitor.HealthOf(null, now));
        Assert.Equal(BackupHealth.Ok, BackupMonitor.HealthOf(new BackupReport { Ok = true, FinishedAtUtc = now.AddHours(-10) }, now));
        Assert.Equal(BackupHealth.Stale, BackupMonitor.HealthOf(new BackupReport { Ok = true, FinishedAtUtc = now.AddHours(-30) }, now));
        Assert.Equal(BackupHealth.Failed, BackupMonitor.HealthOf(new BackupReport { Ok = false, FinishedAtUtc = now }, now));
    }

    [Fact]
    public async Task FailedBackup_ShownOnServersPage_AndDashboard()
    {
        int serverId = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var s = new Server { Name = "bk-server", Host = "10.0.0.9", SqlInstance = ".", MaxTenants = 15, IsActive = true };
            db.Servers.Add(s);
            await db.SaveChangesAsync();
            var now = f.Time.GetUtcNow().UtcDateTime;
            await sp.GetRequiredService<UpdatesService>().SaveInventoryAsync(s, new UpdateInventory
            {
                CollectedAtUtc = now,
                Backup = new BackupReport { Ok = false, FinishedAtUtc = now.AddHours(-1), Files = ["DB_x.bak.gz"], TotalBytes = 2097152, Errors = ["DB_y : xato"] },
            }, default);
            return s.Id;
        });

        await f.EnsureStaffAsync("bk-owner@test.uz", StaffRole.Owner);
        using var app = f.WithWebHostBuilder(b => b.UseSetting("ControlPanel:Security:RequireStaffTotp", "false"));
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string login = await (await c.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        string token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "bk-owner@test.uz", ["Password"] = "TestParol-2026xx", ["__RequestVerificationToken"] = token,
        }))).StatusCode);

        string servers = await (await c.GetAsync("/Servers")).Content.ReadAsStringAsync();
        Assert.Contains("Oxirgi zaxira", servers);
        Assert.Contains("badge bad", servers);
        Assert.Contains("DB_y : xato", servers);
        Assert.Contains("2 MB", servers);

        string home = await (await c.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("bk-server", home);
        Assert.Contains("oxirgi zaxira xato bilan tugadi", home);
        Assert.True(serverId > 0);
    }
}
