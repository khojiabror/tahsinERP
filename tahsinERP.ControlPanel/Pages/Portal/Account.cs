using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Portal;

/// <summary>
/// [Required], xato matni joriy tilda: atributga L.T() yozib bo'lmaydi, shuning uchun kalit beriladi.
/// (Panelda DataAnnotations lokalizatori yo'q.)
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class PortalRequiredAttribute(string key) : RequiredAttribute
{
    public override string FormatErrorMessage(string name) => L.T(key);
}

internal static class PortalFlow
{
    public static string SafeReturn(PortalPageModel page, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && page.Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    /// <summary>Paroldan o'tgan, kod kutilayotgan foydalanuvchi (PortalTwoFactor cookie).</summary>
    public static async Task<PortalUser?> PendingUserAsync(PortalPageModel page, ControlDbContext db)
    {
        var result = await page.HttpContext.AuthenticateAsync(PortalSchemes.PortalTwoFactor);
        if (!result.Succeeded || !int.TryParse(result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
            return null;
        return await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
    }

    public static string TelegramLink(TelegramOptions tg, string token) =>
        string.IsNullOrWhiteSpace(tg.BotUsername) ? "" : $"https://t.me/{tg.BotUsername.Trim().TrimStart('@')}?start={token}";
}

[AllowAnonymous]
[EnableRateLimiting("login")]
public class LoginModel(PortalAuthService auth) : PortalPageModel
{
    [BindProperty, PortalRequired("Ptl_Val_LoginRequired")] public string Login { get; set; } = "";
    [BindProperty, PortalRequired("Ptl_Val_PasswordRequired"), DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => SignedIn ? LocalRedirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (user, error) = await auth.CheckPasswordAsync(Login, Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, error!);
            return Page();
        }
        if (!await TryAsync(() => auth.SendOtpAsync(user, HttpContext.Connection.RemoteIpAddress?.ToString())))
            return Page();
        await HttpContext.SignOutAsync(PortalSchemes.Portal);
        await HttpContext.SignInAsync(PortalSchemes.PortalTwoFactor, PortalPrincipal.Build(user, PortalSchemes.PortalTwoFactor));
        return RedirectToPage("/Portal/Otp", new { returnUrl = PortalFlow.SafeReturn(this, ReturnUrl) });
    }
}

[AllowAnonymous]
[EnableRateLimiting("login")]
public class OtpModel(ControlDbContext db, PortalAuthService auth) : PortalPageModel
{
    [BindProperty] public string? Code { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Destination { get; private set; }

    private void Describe(PortalUser user) => Destination = auth.ResolveTarget(user, user.Tenant!, out _)?.Display;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await PortalFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Portal/Login");
        Describe(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await PortalFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Portal/Login");
        Describe(user);
        if (string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(nameof(Code), L.T("Ptl_EnterCode"));
            return Page();
        }
        if (!await auth.VerifyOtpAndCompleteAsync(user, Code))
        {
            ModelState.AddModelError(string.Empty, auth.IsLocked(user) ? PortalAuthService.LockedError : L.T("Ptl_CodeInvalid"));
            return Page();
        }
        await HttpContext.SignOutAsync(PortalSchemes.PortalTwoFactor);
        await HttpContext.SignInAsync(PortalSchemes.Portal, PortalPrincipal.Build(user, PortalSchemes.Portal));
        return LocalRedirect(PortalFlow.SafeReturn(this, ReturnUrl));
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        var user = await PortalFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Portal/Login");
        Describe(user);
        ModelState.Clear();
        if (await TryAsync(() => auth.SendOtpAsync(user, HttpContext.Connection.RemoteIpAddress?.ToString())))
            FlashOk = L.T("Ptl_NewCodeSent");
        else
            return Page();
        return RedirectToPage(new { returnUrl = ReturnUrl });
    }
}

/// <summary>
/// «Parolni unutdingizmi?» (4-bosqich): login → bir martalik havola foydalanuvchining shaxsiy kanaliga. Javob hisob bor-yo'qligidan
/// qat'i nazar bir xil (foydalanuvchilarni sanab bo'lmaydi); IP bo'yicha «login» rate limiti, foydalanuvchiga — 2 daq / soatiga 3.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting("login")]
public class ForgotModel(PortalAuthService auth, IOptions<PanelHostOptions> hosts) : PortalPageModel
{
    [BindProperty, PortalRequired("Ptl_Val_LoginRequired")] public string Login { get; set; } = "";
    public bool Sent { get; private set; }

    public IActionResult OnGet() => SignedIn ? LocalRedirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await auth.RequestPasswordResetAsync(Login, HttpContext.Connection.RemoteIpAddress?.ToString(), HostZones.PortalBaseUrl(HttpContext, hosts.Value));
        Sent = true; // natija ataylab ko'rsatilmaydi
        return Page();
    }
}

/// <summary>Taklif / parolni tiklash havolasi: parol o'rnatish (+ kerak bo'lsa Telegram bog'lash ko'rsatmasi).</summary>
[AllowAnonymous]
[EnableRateLimiting("login")]
public class InviteModel(PortalAuthService auth, IOptions<TelegramOptions> tg) : PortalPageModel
{
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    [BindProperty, DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty, DataType(DataType.Password)] public string Confirm { get; set; } = "";
    public InviteInfo? Info { get; private set; }
    public string? Error { get; private set; }
    public bool Done { get; private set; }
    public bool CanLogin { get; private set; }
    public string? LinkToken { get; private set; }
    public string LinkUrl => LinkToken is null ? "" : PortalFlow.TelegramLink(tg.Value, LinkToken);
    public string? BotUsername => tg.Value.BotUsername;

    public async Task OnGetAsync() => (Info, Error) = await auth.FindInviteAsync(Token);

    public async Task<IActionResult> OnPostAsync()
    {
        (Info, Error) = await auth.FindInviteAsync(Token);
        if (Info is null) return Page();
        AcceptResult? result = null;
        if (!await TryAsync(async () => result = await auth.AcceptInviteAsync(Token!, Password, Confirm)))
            return Page();
        // Eski (shu brauzerdagi) portal sessiyasi bo'lsa — yopiladi.
        await HttpContext.SignOutAsync(PortalSchemes.Portal);
        await HttpContext.SignOutAsync(PortalSchemes.PortalTwoFactor);
        Done = true;
        CanLogin = result!.CanLogin;
        LinkToken = result.TelegramLinkToken;
        return Page();
    }
}

[AllowAnonymous]
public class LogoutModel(AuditLogger audit, ControlDbContext db) : PortalPageModel
{
    public IActionResult OnGet() => RedirectToPage("/Portal/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        if (SignedIn)
        {
            audit.Add("portal.logout", "PortalUser", PortalUserId, portalUserId: PortalUserId);
            await db.SaveChangesAsync();
        }
        await HttpContext.SignOutAsync(PortalSchemes.Portal);
        await HttpContext.SignOutAsync(PortalSchemes.PortalTwoFactor);
        return RedirectToPage("/Portal/Login");
    }
}
