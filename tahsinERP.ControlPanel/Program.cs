using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel;
using tahsinERP.ControlPanel.Api;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

if (Cli.IsCommand(args) && Cli.RunStandalone(args) is int standaloneExit)
    return standaloneExit;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;

services.Configure<PanelOptions>(config.GetSection("ControlPanel:Panel"));
services.Configure<VendorOptions>(config.GetSection("ControlPanel:Vendor"));
services.Configure<SecurityOptions>(config.GetSection("ControlPanel:Security"));
services.Configure<SigningOptions>(config.GetSection("ControlPanel:Signing"));
services.Configure<TelegramOptions>(config.GetSection("ControlPanel:Telegram"));
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
services.AddSingleton<InvoicePdfRenderer>();
services.AddHttpClient<ITelegramSender, TelegramSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
if (config.GetValue("ControlPanel:Panel:EnableDailyJob", true))
    services.AddHostedService<DailyJobHostedService>();

bool isDev = builder.Environment.IsDevelopment();
services.AddAuthentication(AuthSchemes.Staff)
    .AddCookie(AuthSchemes.Staff, o =>
    {
        o.Cookie.Name = "tcp.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
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
    });

services.AddAuthorization(o =>
{
    var staff = new AuthorizationPolicyBuilder(AuthSchemes.Staff).RequireAuthenticatedUser().Build();
    o.DefaultPolicy = staff;
    o.FallbackPolicy = staff; // atributsiz har sahifa — faqat to'liq (2FA'dan o'tgan) xodim
    o.AddPolicy(Policies.Sales, p => p.AddAuthenticationSchemes(AuthSchemes.Staff).RequireRole(nameof(StaffRole.Owner), nameof(StaffRole.Sales)));
    o.AddPolicy(Policies.Owner, p => p.AddAuthenticationSchemes(AuthSchemes.Staff).RequireRole(nameof(StaffRole.Owner)));
});

services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(LicenseApi.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("ControlPanel:Panel:ApiRateLimitPerMinute", 60), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Staff", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Servers", Policies.Owner);
    o.Conventions.AuthorizeFolder("/Audit", Policies.Owner);
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
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("ok")).AllowAnonymous();
LicenseApi.Map(app);
app.MapRazorPages();

app.Run();
return 0;

public partial class Program;
