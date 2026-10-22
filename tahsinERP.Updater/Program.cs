using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.Updater;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

// Ishga tushirish (Docs/Deploy/03_AUTO_UPDATE.md):
//   tahsinERP.Updater.exe                        — Windows Service (LocalSystem) yoki konsol: inbox'ni kuzatadi
//   tahsinERP.Updater.exe status                 — state.json qisqacha
//   tahsinERP.Updater.exe split-erp [slug ...]   — bir martalik: tenants.json'dagi firmalarni umumiy saytdan alohida
//                                                  instance'larga (erp-<slug>) ko'chirish, versiya "legacy"

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Services.AddWindowsService(o => o.ServiceName = "tahsinERP.Updater");
builder.Services.Configure<UpdaterOptions>(builder.Configuration);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<UpdaterOptions>>().Value);
builder.Services.AddSingleton<StateStore>();
builder.Services.AddSingleton<ISystemOps, WindowsSystemOps>();
builder.Services.AddSingleton<UpdateExecutor>();
builder.Services.AddSingleton<InboxWorker>();

string? command = args.FirstOrDefault(a => !a.StartsWith('-'));
if (command is null) builder.Services.AddHostedService(sp => sp.GetRequiredService<InboxWorker>());

using var host = builder.Build();
var options = host.Services.GetRequiredService<UpdaterOptions>();
if (string.IsNullOrWhiteSpace(options.Domain))
{
    Console.Error.WriteLine("Domain sozlanmagan (appsettings.json).");
    return 2;
}

switch (command)
{
    case null:
        await host.RunAsync();
        return 0;

    case "status":
        Console.WriteLine(JsonSerializer.Serialize(host.Services.GetRequiredService<StateStore>().Load(), StateStore.Json));
        return 0;

    case "split-erp":
    {
        string tenantsFile = Path.Combine(options.Paths.DataDir, "tenants.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(tenantsFile));
        var only = args.Skip(1).Where(a => !a.StartsWith('-')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executor = host.Services.GetRequiredService<UpdateExecutor>();
        var state = host.Services.GetRequiredService<StateStore>().Load();
        int failed = 0;
        long id = DateTime.UtcNow.Ticks;
        foreach (var t in doc.RootElement.GetProperty("tenants").EnumerateArray())
        {
            string slug = t.GetProperty("slug").GetString() ?? "";
            string? db = t.TryGetProperty("database", out var d) ? d.GetString() : null;
            if ((only.Count > 0 && !only.Contains(slug)) || state.Instances.ContainsKey(slug)) continue;
            if (t.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.False) { Console.WriteLine($"{slug}: o'chiq — o'tkazildi"); continue; }
            Console.WriteLine($"=== {slug} ({db})");
            var rep = new JobReporter(Path.Combine(options.Paths.Status, $"split-{slug}.json"), ++id);
            var r = await executor.ExecuteAsync(new InboxRequest
            {
                JobId = id, Action = UpdateAction.CreateInstance, App = ReleaseApps.Erp, Slug = slug, DbName = db, Version = UpdateExecutor.LegacyVersion,
            }, "", rep, CancellationToken.None);
            Console.WriteLine(rep.Log);
            if (r.Outcome != UpdateOutcome.Succeeded) failed++;
        }
        return failed == 0 ? 0 : 1;
    }

    default:
        Console.Error.WriteLine("Buyruq: status | split-erp [slug ...]");
        return 2;
}
