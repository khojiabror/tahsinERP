using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Server holati: agent o'lchovi → inventar → kunlik jurnal (ServerHealthDays) va Serverlar sahifasi.</summary>
public class ServerHealthTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private const long Gb = 1L << 30;

    private static ServerHealth Sample(long ramUsedGb, double? cpu, long uptime, long diskFreeGb = 30) => new()
    {
        MachineName = "EC2AMAZ-TEST", Os = "Microsoft Windows 10.0.20348", CpuCount = 2, CpuPercent = cpu, UptimeSeconds = uptime,
        RamTotalBytes = 8 * Gb, RamAvailableBytes = (8 - ramUsedGb) * Gb,
        Disks = [new DiskHealth { Name = @"C:\", TotalBytes = 80 * Gb, FreeBytes = diskFreeGb * Gb }],
    };

    [Fact]
    public void Probe_MeasuresThisMachine()
    {
        var h = new ServerHealthProbe().Collect();
        Assert.True(h.RamTotalBytes > 0);
        Assert.True(h.CpuCount > 0);
        Assert.NotEmpty(h.Disks);
        Assert.All(h.Disks, d => Assert.True(d.TotalBytes >= d.FreeBytes));
        if (OperatingSystem.IsWindows()) Assert.InRange(h.RamAvailableBytes, 1, h.RamTotalBytes);
    }

    [Fact]
    public void Level_Thresholds()
    {
        Assert.Equal(HealthLevel.Ok, ServerHealthLog.LevelOf(50));
        Assert.Equal(HealthLevel.Warn, ServerHealthLog.LevelOf(90));
        Assert.Equal(HealthLevel.Bad, ServerHealthLog.LevelOf(97));
    }

    [Fact]
    public async Task Record_AggregatesDay_CountsReboots_AndStartsNewDay()
    {
        f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 3, 5, 0, 0, TimeSpan.Zero)); // Toshkent 10:00
        var rows = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var s = new Server { Name = "hl-agg", Host = "10.0.0.21", SqlInstance = ".", MaxTenants = 15, IsActive = true };
            db.Servers.Add(s);
            await db.SaveChangesAsync();
            var log = sp.GetRequiredService<ServerHealthLog>();
            await log.RecordAsync(s.Id, Sample(2, null, 10_000));
            await log.RecordAsync(s.Id, Sample(4, 20, 10_300));
            await log.RecordAsync(s.Id, Sample(6, 60, 120)); // qayta yuklangan
            f.Time.Advance(TimeSpan.FromDays(1));
            await log.RecordAsync(s.Id, Sample(3, 10, 86_520, diskFreeGb: 4));
            return await db.ServerHealthDays.AsNoTracking().Where(x => x.ServerId == s.Id).OrderBy(x => x.Day).ToListAsync();
        });

        Assert.Equal(2, rows.Count);
        var d1 = rows[0];
        Assert.Equal(new DateOnly(2026, 11, 3), d1.Day);
        Assert.Equal(3, d1.Samples);
        Assert.Equal(2, d1.CpuSamples);
        Assert.Equal(40, d1.CpuAvgPercent);
        Assert.Equal(60, d1.CpuMaxPercent);
        Assert.Equal(4 * Gb, d1.RamUsedAvgBytes);
        Assert.Equal(6 * Gb, d1.RamUsedMaxBytes);
        Assert.Equal(1, d1.Reboots);
        Assert.Equal(120, d1.UptimeSeconds);

        var d2 = rows[1];
        Assert.Equal(new DateOnly(2026, 11, 4), d2.Day);
        Assert.Equal(1, d2.Samples);
        Assert.Equal(0, d2.Reboots);
        Assert.Equal(4 * Gb, Assert.Single(ServerHealthLog.Disks(d2)).FreeBytes);
    }

    [Fact]
    public async Task ServersPage_ShowsCurrentHealth_AndDailyLog()
    {
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var s = new Server { Name = "hl-page", Host = "10.0.0.22", SqlInstance = ".", MaxTenants = 15, IsActive = true };
            db.Servers.Add(s);
            await db.SaveChangesAsync();
            var inv = new UpdateInventory { CollectedAtUtc = f.Time.GetUtcNow().UtcDateTime, Health = Sample(7, 35.5, 3 * 86_400 + 7_200, diskFreeGb: 2) };
            await sp.GetRequiredService<UpdatesService>().SaveInventoryAsync(s, inv, default);
            await sp.GetRequiredService<ServerHealthLog>().RecordAsync(s.Id, inv.Health!);
        });

        await f.EnsureStaffAsync("hl-owner@test.uz", StaffRole.Owner);
        using var app = f.WithWebHostBuilder(b => b.UseSetting("ControlPanel:Security:RequireStaffTotp", "false"));
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string login = await (await c.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        string token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "hl-owner@test.uz", ["Password"] = "TestParol-2026xx", ["__RequestVerificationToken"] = token,
        }))).StatusCode);

        string page = WebUtility.HtmlDecode(await (await c.GetAsync("/Servers")).Content.ReadAsStringAsync());
        Assert.Contains("hl-page", page);
        Assert.Contains("Operativ xotira (RAM)", page);
        Assert.Contains("7 GB", page);
        Assert.Contains("8 GB", page);
        Assert.Contains("3 kun 2 soat", page);
        Assert.Contains("Kunlik jurnal", page);
        Assert.Contains("lvl-bad", page); // disk 97.5% band
        Assert.Contains("<meter", page);
    }
}
