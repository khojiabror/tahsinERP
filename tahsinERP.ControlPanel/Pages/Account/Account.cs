using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using QRCoder;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Account;

internal static class AccountFlow
{
    public static string SafeReturn(PageModel page, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && page.Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    public static async Task<StaffUser?> PendingUserAsync(PageModel page, ControlDbContext db)
    {
        var result = await page.HttpContext.AuthenticateAsync(AuthSchemes.TwoFactor);
        if (!result.Succeeded || !int.TryParse(result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
            return null;
        return await db.StaffUsers.FindAsync(id);
    }

    public static async Task CompleteAsync(PageModel page, StaffUser user)
    {
        await page.HttpContext.SignOutAsync(AuthSchemes.TwoFactor);
        await page.HttpContext.SignInAsync(AuthSchemes.Staff, StaffAuthService.BuildPrincipal(user, AuthSchemes.Staff));
    }
}

[AllowAnonymous]
[EnableRateLimiting("login")]
public class LoginModel(StaffAuthService auth, IOptions<SecurityOptions> security) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Email kiriting"), EmailAddress(ErrorMessage = "Email noto'g'ri")] public string Email { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Parol kiriting"), DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public bool TotpRequired => security.Value.RequireStaffTotp;
    public int IdleMinutes => security.Value.EffectiveStaffIdleMinutes;
    [BindProperty(SupportsGet = true)] public bool Expired { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (user, error) = await auth.CheckPasswordAsync(Email, Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, error!);
            return Page();
        }
        await HttpContext.SignOutAsync(AuthSchemes.Staff);
        string next = AccountFlow.SafeReturn(this, ReturnUrl);
        if (!security.Value.RequireStaffTotp)
        {
            // 2FA egasi qarori bilan o'chirilgan - parol yetarli
            await auth.CompletePasswordOnlyLoginAsync(user);
            await HttpContext.SignInAsync(AuthSchemes.Staff, StaffAuthService.BuildPrincipal(user, AuthSchemes.Staff));
            return LocalRedirect(next);
        }
        await HttpContext.SignInAsync(AuthSchemes.TwoFactor, StaffAuthService.BuildPrincipal(user, AuthSchemes.TwoFactor));
        return user.TotpSecretProtected is null
            ? RedirectToPage("/Account/EnrollTotp", new { returnUrl = next })
            : RedirectToPage("/Account/Totp", new { returnUrl = next });
    }
}

[Authorize(AuthenticationSchemes = AuthSchemes.TwoFactor)]
[EnableRateLimiting("login")]
public class TotpModel(ControlDbContext db, StaffAuthService auth) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Kodni kiriting")] public string Code { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await AccountFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Account/Login");
        if (user.TotpSecretProtected is null) return RedirectToPage("/Account/EnrollTotp", new { returnUrl = ReturnUrl });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await AccountFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Account/Login");
        if (!ModelState.IsValid) return Page();
        if (!await auth.VerifyTotpAndCompleteAsync(user, Code))
        {
            ModelState.AddModelError(string.Empty, user.LockedUntilUtc != null ? "Hisob vaqtincha bloklandi." : "Kod noto'g'ri yoki eskirgan.");
            return Page();
        }
        await AccountFlow.CompleteAsync(this, user);
        return LocalRedirect(AccountFlow.SafeReturn(this, ReturnUrl));
    }
}

/// <summary>2FA birinchi ulanishi: yangi sir faqat forma ichida shifrlangan holda yuradi, kod tasdiqlangandan keyin saqlanadi.</summary>
[Authorize(AuthenticationSchemes = AuthSchemes.TwoFactor)]
public class EnrollTotpModel(ControlDbContext db, StaffAuthService auth, ISecretProtector protector) : PageModel
{
    [BindProperty] public string ProtectedSecret { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Ilovadagi 6 xonali kodni kiriting")] public string Code { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string SecretText { get; private set; } = "";
    public string QrDataUri { get; private set; } = "";

    private string Purpose(StaffUser u) => "totp-enroll|" + u.Id + "|" + u.SecurityStamp;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await AccountFlow.PendingUserAsync(this, db);
        if (user is null) return RedirectToPage("/Account/Login");
        if (user.TotpSecretProtected is not null) return RedirectToPage("/Account/Totp", new { returnUrl = ReturnUrl });
        byte[] secret = Totp.NewSecret();
        ProtectedSecret = protector.Protect(Base32.Encode(secret), Purpose(user));
        Prepare(user, secret);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await AccountFlow.PendingUserAsync(this, db);
        if (user is null || user.TotpSecretProtected is not null) return RedirectToPage("/Account/Login");
        byte[] secret;
        try { secret = Base32.Decode(protector.Unprotect(ProtectedSecret, Purpose(user))); }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return RedirectToPage(new { returnUrl = ReturnUrl });
        }
        Prepare(user, secret);
        if (!ModelState.IsValid) return Page();
        if (!await auth.EnrollTotpAndCompleteAsync(user, secret, Code))
        {
            ModelState.AddModelError(string.Empty, "Kod mos kelmadi. Telefon soati to'g'riligini tekshiring.");
            return Page();
        }
        await AccountFlow.CompleteAsync(this, user);
        return LocalRedirect(AccountFlow.SafeReturn(this, ReturnUrl));
    }

    private void Prepare(StaffUser user, byte[] secret)
    {
        SecretText = string.Join(' ', Base32.Encode(secret).Chunk(4).Select(c => new string(c)));
        string uri = Totp.BuildUri(StaffAuthService.Issuer, user.Email, secret);
        using var data = new QRCodeGenerator().CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        QrDataUri = "data:image/png;base64," + Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(5));
    }
}

[AllowAnonymous]
public class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Staff);
        await HttpContext.SignOutAsync(AuthSchemes.TwoFactor);
        return RedirectToPage("/Account/Login");
    }
}

[AllowAnonymous]
public class DeniedModel : PageModel
{
    public void OnGet() { }
}

public class ChangePasswordModel(ControlDbContext db, AuditLogger audit) : PanelPageModel
{
    [BindProperty, Required(ErrorMessage = "Joriy parol"), DataType(DataType.Password)] public string Current { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Yangi parol"), DataType(DataType.Password)] public string New { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Takror"), DataType(DataType.Password)] public string Confirm { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await db.StaffUsers.FindAsync(StaffId);
        if (user is null) return RedirectToPage("/Account/Login");
        if (!PasswordHasher.Verify(Current, user.PasswordHash)) ModelState.AddModelError(nameof(Current), "Joriy parol noto'g'ri.");
        else if (New != Confirm) ModelState.AddModelError(nameof(Confirm), "Parollar mos emas.");
        else if (PasswordHasher.ValidatePolicy(New) is string err) ModelState.AddModelError(nameof(New), err);
        if (!ModelState.IsValid) return Page();
        user.PasswordHash = PasswordHasher.Hash(New);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add("staff.password", "StaffUser", user.Id);
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(AuthSchemes.Staff, StaffAuthService.BuildPrincipal(user, AuthSchemes.Staff));
        FlashOk = "Parol almashtirildi.";
        return RedirectToPage("/Index");
    }
}
