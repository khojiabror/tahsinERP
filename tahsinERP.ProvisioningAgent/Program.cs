using Microsoft.Extensions.Options;
using tahsinERP.ProvisioningAgent;

// Ishga tushirish (05_PROVISIONING.md, 4-bo'lim):
//   tahsinERP.ProvisioningAgent.exe                 — konsol yoki Windows Service (sc.exe create ... binPath=...)
//   tahsinERP.ProvisioningAgent.exe --once          — bitta aylanish (bitta job) va chiqish (rejalashtiruvchi/test)
//   tahsinERP.ProvisioningAgent.exe build-demo-template — Database/Demo dan DB_DEMO_TEMPLATE.bak ni (qayta) yaratish

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.AddEnvironmentVariables("TAHSIN_AGENT_");
builder.Services.AddWindowsService(o => o.ServiceName = "tahsinERP.ProvisioningAgent");
builder.Services.Configure<AgentOptions>(builder.Configuration);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SqlOps>();
builder.Services.AddSingleton<ScriptRunner>();
builder.Services.AddSingleton<IBackupStore, LocalBackupStore>();
builder.Services.AddSingleton<DemoScheduler>();
builder.Services.AddScoped<JobExecutor>();
builder.Services.AddHttpClient<PanelClient>();
builder.Services.AddSingleton<AgentWorker>();

bool once = args.Contains("--once");
bool buildTemplate = args.Contains("build-demo-template");
if (!once && !buildTemplate)
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentWorker>());

using var host = builder.Build();
var options = host.Services.GetRequiredService<AgentOptions>();
if (string.IsNullOrWhiteSpace(options.Paths.DatabaseRoot) || !Directory.Exists(options.Paths.DatabaseRoot))
{
    Console.Error.WriteLine($"Paths:DatabaseRoot topilmadi: '{options.Paths.DatabaseRoot}' (tahsinERP/Database papkasi).");
    return 2;
}

if (buildTemplate)
{
    using var scope = host.Services.CreateScope();
    string template = options.ResolveDemoTemplate();
    if (File.Exists(template)) File.Delete(template);
    await scope.ServiceProvider.GetRequiredService<JobExecutor>().BuildDemoTemplateAsync(template, CancellationToken.None);
    Console.WriteLine("Demo shabloni: " + template);
    return 0;
}

if (once)
{
    bool worked = await host.Services.GetRequiredService<AgentWorker>().RunOnceAsync(CancellationToken.None);
    Console.WriteLine(worked ? "Job bajarildi." : "Navbat bo'sh.");
    return 0;
}

await host.RunAsync();
return 0;
