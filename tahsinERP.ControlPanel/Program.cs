using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel;
using tahsinERP.ControlPanel.Api;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.I18n;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection.Extensions;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

if (Cli.IsCommand(args) && Cli.RunStandalone(args) is int standaloneExit)
    return standaloneExit;

// CLI istalgan papkadan ishga tushirilsa ham appsettings.json ilova yonidan o'qilsin.
var builder = Cli.IsCommand(args) && !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
    ? WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory })
    : WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;

services.Configure<PanelOptions>(config.GetSection("ControlPanel:Panel"));
services.Configure<VendorOptions>(config.GetSection("ControlPanel:Vendor"));
services.Configure<SecurityOptions>(config.GetSection("ControlPanel:Security"));
services.Configure<SigningOptions>(config.GetSection("ControlPanel:Signing"));
services.Configure<TelegramOptions>(config.GetSection("ControlPanel:Telegram"));
services.Configure<ProvisioningOptions>(config.GetSection("ControlPanel:Provisioning"));
services.Configure<UsageOptions>(config.GetSection("ControlPanel:Usage"));
services.Configure<PanelHostOptions>(config.GetSection("ControlPanel:Hosts"));
services.Configure<PortalOptions>(config.GetSection("ControlPanel:Portal"));
services.Configure<DidoxOptions>(config.GetSection("ControlPanel:Didox"));
services.Configure<EskizOptions>(config.GetSection("ControlPanel:Eskiz"));
services.Configure<SmtpOptions>(config.GetSection("ControlPanel:Smtp"));
services.Configure<AppsOptions>(config.GetSection("ControlPanel:Apps"));
services.Configure<UpdatesOptions>(config.GetSection("ControlPanel:Updates"));
services.Configure<LeadsOptions>(config.GetSection("ControlPanel:Leads"));
PanelFontResolver.ExtraFontsDir = config["ControlPanel:Pdf:FontsDir"];

services.AddDbContext<ControlDbContext>(o => o.UseSqlServer(
    config.GetConnectionString("Control") ?? throw new InvalidOperationException("ConnectionStrings:Control sozlanmagan.")));

services.AddSingleton(TimeProvider.System);
services.AddSingleton<Clock>();
services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
services.AddSingleton<ILicenseSigner>(sp =>
{
    var o = sp.GetRequiredService<IOptions<SigningOptions>>();
    return string.Equals(o.Value.Provider, "Kms", StringComparison.OrdinalIgnoreCase)
        ? new KmsSigner(o) : new LocalRsaSigner(o);
});
services.AddHttpContextAccessor();
services.AddMemoryCache();
services.AddScoped<ICurrentActor, HttpCurrentActor>();
services.AddScoped<AuditLogger>();
services.AddScoped<StaffAuthService>();
services.AddScoped<TenantService>();
services.AddScoped<SubscriptionService>();
services.AddScoped<BillingService>();
services.AddScoped<LicenseIssuer>();
services.AddScoped<DashboardService>();
services.AddScoped<ExpiryReminderService>();
services.AddScoped<ProvisioningService>();
services.AddScoped<UsageService>();
services.AddScoped<TractionService>();
services.AddScoped<DemoRequestService>();
services.AddScoped<tahsinERP.ControlPanel.Services.Bank.BankImportService>();
// Didox e-hisob-faktura: PartnerToken/Password/MXIK berilmasa o'chiq (tugma ko'rinmaydi).
services.AddSingleton<DidoxTokenCache>();
services.AddHttpClient<IDidoxClient, DidoxHttpClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
services.AddScoped<DidoxService>();
// Mijoz portali (my.<domen>): kirish, kod yuborish kanallari (IOtpSender — tartib bo'yicha birinchi mosi), so'rovlar.
services.AddScoped<IOtpSender, TelegramOtpSender>();
// 4-bosqich: SMS (Eskiz) va email (SMTP) — sozlanmagan bo'lsa Resolve null qaytaradi, kanal ko'rinmaydi.
services.AddSingleton<EskizTokenCache>();
services.AddHttpClient<ISmsSender, EskizSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
services.AddSingleton<IEmailSender, SmtpEmailSender>();
services.AddScoped<IOtpSender, SmsOtpSender>();
services.AddScoped<IOtpSender, EmailOtpSender>();
services.AddScoped<PortalAuthService>();
services.AddScoped<PortalNotifier>();
services.AddScoped<PortalRequestService>();
// Host bo'yicha zona: xodim sahifalari faqat admin.*, portal sahifalari faqat my.* da topiladi (boshqasida 404).
services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ZoneMatcherPolicy>());
services.AddSingleton<InvoicePdfRenderer>();
services.AddHttpClient<ITelegramSender, TelegramSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
services.AddHttpClient<IAppsClient, AppsHttpClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
services.AddScoped<TenantAppsService>();
// Avto-yangilash: GitHub relizlari (token faqat pool env'da), navbat, agent API (Docs/Deploy/03_AUTO_UPDATE.md)
services.AddSingleton<ReleaseTrust>();
services.AddHttpClient<IReleaseSource, GitHubReleaseSource>(c => c.Timeout = TimeSpan.FromMinutes(30));
services.AddScoped<UpdatesService>();
services.AddScoped<BackupMonitor>();
if (config.GetValue("ControlPanel:Panel:EnableDailyJob", true))
{
    services.AddHostedService<DailyJobHostedService>();
    services.AddHostedService<DidoxRefreshHostedService>();
    services.AddHostedService<ReleaseRefreshHostedService>();
}

// Cookie/antiforgery kalitlari: serverda doimiy papkada (aks holda IIS qayta ishga tushganda xodimlar qayta kiradi).
// Windows'da DPAPI (LocalMachine) bilan shifrlanadi. Papka bo'sh bo'lsa — standart joy (dev).
var dataProtection = services.AddDataProtection().SetApplicationName("tahsinERP.ControlPanel");
if (config["ControlPanel:DataProtection:KeysDir"] is { Length: > 0 } keysDir)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDir));
    if (OperatingSystem.IsWindows())
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

bool isDev = builder.Environment.IsDevelopment();
int staffIdleMinutes = (config.GetSection("ControlPanel:Security").Get<SecurityOptions>() ?? new()).EffectiveStaffIdleMinutes;
int portalSessionHours =Math.Clamp(config.GetValue("ControlPanel:Portal:SessionHours", 4), 1, 24);
services.AddAuthentication(PortalSchemes.Zone)
    // HttpContext.User — shu host zonasining foydalanuvchisi (admin.* → xodim, my.* → mijoz). Avtorizatsiya siyosatlari
    // baribir o'z sxemasini aniq ko'rsatadi: xodim cookie'si portalda va mijoz cookie'si panelda hech narsa ochmaydi.
    .AddPolicyScheme(PortalSchemes.Zone, null, o =>
        o.ForwardDefaultSelector = ctx => HostZones.Resolve(ctx) == Zone.Portal ? PortalSchemes.Portal : AuthSchemes.Staff)
    .AddCookie(AuthSchemes.Staff, o =>
    {
        o.Cookie.Name = "tcp.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        // Harakatsizlik taymauti: cookie har so'rovda yangilanadi (standart sliding faqat yarmidan keyin yangilaydi).
        o.ExpireTimeSpan = TimeSpan.FromMinutes(staffIdleMinutes);
        o.SlidingExpiration = true;
        o.Events.OnCheckSlidingExpiration = ctx => { ctx.ShouldRenew = true; return Task.CompletedTask; };
        o.Events.OnValidatePrincipal = StaffAuthService.ValidatePrincipalAsync;
    })
    .AddCookie(AuthSchemes.TwoFactor, o =>
    {
        o.Cookie.Name = "tcp.2fa";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        o.SlidingExpiration = false;
        o.Events.OnValidatePrincipal = StaffAuthService.ValidatePrincipalAsync;
    })
    // Mijoz portali: boshqa cookie nomi; Domain berilmaydi — cookie faqat my.<domen> host'iga qaytadi (admin.* ga hech qachon).
    // Serverda "__Host-" prefiksi: brauzer uni faqat Secure, Path=/, Domain'siz qabul qiladi.
    .AddCookie(PortalSchemes.Portal, o =>
    {
        o.Cookie.Name = isDev ? "tcp.my" : "__Host-tcp.my";
        o.Cookie.Path = "/";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Login";
        o.LogoutPath = "/Logout";
        o.AccessDeniedPath = "/Login";
        o.ReturnUrlParameter = "returnUrl";
        o.ExpireTimeSpan = TimeSpan.FromHours(portalSessionHours);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = PortalPrincipal.ValidateAsync;
    })
    .AddCookie(PortalSchemes.PortalTwoFactor, o =>
    {
        o.Cookie.Name = isDev ? "tcp.my2fa" : "__Host-tcp.my2fa";
        o.Cookie.Path = "/";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        o.SlidingExpiration = false;
        o.Events.OnValidatePrincipal = PortalPrincipal.ValidateAsync;
    });

services.AddAuthorization(o =>
{
    var staff = new AuthorizationPolicyBuilder(AuthSchemes.Staff).RequireAuthenticatedUser().Build();
    o.DefaultPolicy = staff;
    o.FallbackPolicy = staff; // atributsiz har sahifa — faqat to'liq (2FA'dan o'tgan) xodim
    o.AddPolicy(Policies.Sales, p => p.AddAuthenticationSchemes(AuthSchemes.Staff).RequireRole(nameof(StaffRole.Owner), nameof(StaffRole.Sales)));
    o.AddPolicy(Policies.Owner, p => p.AddAuthenticationSchemes(AuthSchemes.Staff).RequireRole(nameof(StaffRole.Owner)));
    // Mijoz: faqat Portal sxemasi (xodim cookie'si bu siyosatni hech qachon qondirmaydi).
    o.AddPolicy(Policies.Portal, p => p.AddAuthenticationSchemes(PortalSchemes.Portal).RequireAuthenticatedUser().RequireClaim(PortalClaims.Tenant));
});

services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(LicenseApi.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("ControlPanel:Panel:ApiRateLimitPerMinute", 60), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("ControlPanel:Panel:LoginRateLimitPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
});

// Kirill/o'zbek matni HTML'da entity (&#x...;) bo'lib chiqmasin — ommaviy sahifa (ru) va panel uchun.
services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

services.AddRazorPages(o =>
{
    o.Conventions.Add(new ZonePageRouteConvention());
    o.Conventions.AuthorizeFolder(ZonePageRouteConvention.PortalFolder, Policies.Portal);
    o.Conventions.AllowAnonymousToFolder(ZonePageRouteConvention.PublicFolder); // tahsinerp.uz — mahsulot sahifasi
    o.Conventions.AuthorizeFolder("/Staff", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Developers", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Servers", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Updates", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Audit", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Bank", Policies.Sales); // to'lov belgilash huquqi (00_PLAN.md 5.2)
});

var app = builder.Build();

if (Cli.IsCommand(args))
    return await Cli.RunAsync(args, app.Services);

if (config.GetValue("ControlPanel:Panel:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ControlDbContext>().Database.Migrate();
}

if (!isDev)
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XFrameOptions = "DENY";
    h.XContentTypeOptions = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
app.UseUiLanguage();   // til: cookie tahsin_lang (I18n/L.cs)
app.UseRouting();
// Endpoint topilmadi (yoki boshqa zonaga tegishli — ZoneMatcherPolicy chiqarib tashlagan) → 404. Aks holda FallbackPolicy
// anonim so'rovni login sahifasiga yo'naltirib, sahifa "bor"ligini oshkor qilardi.
app.Use(async (ctx, next) =>
{
    if (ctx.GetEndpoint() is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("ok")).AllowAnonymous();
// Brauzer faol foydalanuvchi seansini yangilab turadi (app.js); xodim cookie'si shu so'rovda uzaytiriladi.
app.MapGet("/Account/KeepAlive", (HttpContext ctx) => { ctx.Response.Headers.CacheControl = "no-store"; return Results.NoContent(); });
LicenseApi.Map(app);
AgentApi.Map(app);
TelegramWebhook.Map(app);
app.MapLanguageSwitch();
app.MapRazorPages();

app.Run();
return 0;

public partial class Program;
