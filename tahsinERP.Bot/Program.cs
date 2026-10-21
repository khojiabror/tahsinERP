using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Bot;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.MiniApp;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;
using tahsinERP.Bot.Telegram;

var isCli = Cli.IsCommand(args);
var builder = WebApplication.CreateBuilder(isCli ? [] : args);
var config = builder.Configuration;
var services = builder.Services;

services.Configure<BotOptions>(config.GetSection("Bot"));
var connectionString = config.GetConnectionString("Bot") ?? throw new InvalidOperationException("ConnectionStrings:Bot sozlanmagan.");
services.AddDbContext<BotDbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

services.AddSingleton(TimeProvider.System);
services.AddSingleton(sp => new SecretBox(sp.GetRequiredService<IOptions<BotOptions>>().Value.MasterKey));
services.AddSingleton<TenantRegistry>();
services.AddSingleton<OutboxSignal>();
services.AddSingleton<InboxSignal>();
services.AddSingleton<IFileStore, LocalFileStore>();
services.AddSingleton<ErpClientFactory>();
services.AddSingleton<LiveThrottle>();
services.AddHttpClient<tahsinERP.Bot.Voice.IVoiceRecognizer, tahsinERP.Bot.Voice.GroqVoiceRecognizer>(c => c.Timeout = TimeSpan.FromSeconds(60));
services.AddScoped<Outbox>();
services.AddScoped<SessionStore>();
services.AddScoped<UpdateRouter>();
services.AddScoped<MiniAppEndpoints.Deps>();
services.AddScoped<tahsinERP.Bot.Admin.BotTenantAdmin>();

services.AddHttpClient<TelegramApi>(c => c.Timeout = TimeSpan.FromSeconds(30));
services.AddHttpClient(ErpClientFactory.HttpClientName, (sp, c) =>
        c.Timeout = TimeSpan.FromSeconds(Math.Clamp(sp.GetRequiredService<IOptions<BotOptions>>().Value.ErpTimeoutSeconds, 5, 120)))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });   // login sahifasiga ketmasin

if (!isCli)
{
    services.AddHostedService<InboxWorker>();
    services.AddHostedService<OutboxWorker>();
    services.AddHostedService<NotificationPoller>();
    services.AddHostedService<DailySummaryWorker>();
    services.AddHostedService<HousekeepingWorker>();
}

services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("miniapp", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<BotOptions>>().Value.MasterKey is not { Length: >= 32 })
    throw new InvalidOperationException("Bot:MasterKey (muhit o'zgaruvchisi Bot__MasterKey) kamida 32 belgi bo'lishi kerak.");
await SchemaInitializer.EnsureAsync(connectionString);

if (isCli) return await Cli.RunAsync(app.Services, args);

app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    if (ctx.Request.Path.StartsWithSegments("/app"))
        h["Content-Security-Policy"] = "default-src 'self'; script-src 'self' https://telegram.org; style-src 'self' 'unsafe-inline'; " +
                                       "img-src 'self' data:; connect-src 'self'; frame-ancestors https://web.telegram.org https://*.telegram.org";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGet("/health", async (BotDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { ok = true }) : Results.StatusCode(503));
WebhookEndpoint.Map(app);
tahsinERP.Bot.Admin.AdminEndpoints.Map(app);
FileEndpoint.Map(app);
MiniAppEndpoints.Map(app.MapGroup("").RequireRateLimiting("miniapp"));

app.Run();
return 0;
