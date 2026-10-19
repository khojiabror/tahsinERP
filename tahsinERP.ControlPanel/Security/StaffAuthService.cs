using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Security;

public static class Policies
{
    /// <summary>Owner yoki Sales: tenant/obuna/hisob-faktura, to'lov belgilash.</summary>
    public const string Sales = "Sales";
    /// <summary>Faqat Owner: storno, kalit chiqarish/bekor qilish, tariflar, serverlar, xodimlar.</summary>
    public const string Owner = "Owner";
    /// <summary>Mijoz portali foydalanuvchisi (Portal sxemasi, my.&lt;domen&gt;).</summary>
    public const string Portal = "Portal";
}

public static class StaffClaims
{
    public const string Stamp = "stamp";
}

/// <summary>
/// Xodimlar kirishi: parol (PBKDF2) → TOTP (majburiy; ulanmagan bo'lsa birinchi kirishda ulanadi).
/// 5 ta xato urinish (parol yoki kod) → 15 daqiqa blok.
/// </summary>
public sealed class StaffAuthService(ControlDbContext db, ISecretProtector protector, AuditLogger audit, Clock clock)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);
    public const string GenericLoginError = "Email yoki parol noto'g'ri.";
    public const string Issuer = "tahsinERP Panel";

    // Mavjud bo'lmagan email uchun ham xesh hisoblanadi — javob vaqti bo'yicha email borligini bilib bo'lmasin.
    private static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash(Guid.NewGuid().ToString()));

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public async Task<(StaffUser? User, string? Error)> CheckPasswordAsync(string? email, string? password, CancellationToken ct = default)
    {
        string e = NormalizeEmail(email);
        var user = await db.StaffUsers.FirstOrDefaultAsync(u => u.Email == e, ct);
        if (user is null || !user.IsActive)
        {
            PasswordHasher.Verify(password ?? "", DummyHash.Value);
            return (null, GenericLoginError);
        }
        if (user.LockedUntilUtc > clock.UtcNow)
            return (null, "Hisob vaqtincha bloklangan (ko'p xato urinish). Keyinroq urinib ko'ring.");
        if (!PasswordHasher.Verify(password ?? "", user.PasswordHash))
        {
            await RegisterFailureAsync(user, "password", ct);
            return (null, GenericLoginError);
        }
        if (PasswordHasher.NeedsRehash(user.PasswordHash))
            user.PasswordHash = PasswordHasher.Hash(password!);
        await db.SaveChangesAsync(ct);
        return (user, null);
    }

    public async Task RegisterFailureAsync(StaffUser user, string stage, CancellationToken ct = default)
    {
        user.FailedLogins++;
        if (user.FailedLogins >= MaxFailedAttempts)
        {
            user.LockedUntilUtc = clock.UtcNow.Add(LockoutTime);
            user.FailedLogins = 0;
            audit.Add("staff.lockout", "StaffUser", user.Id, after: new { Stage = stage, user.LockedUntilUtc }, staffIdOverride: user.Id);
        }
        await db.SaveChangesAsync(ct);
    }

    public byte[]? GetTotpSecret(StaffUser user) =>
        user.TotpSecretProtected is null ? null : Base32.Decode(protector.Unprotect(user.TotpSecretProtected, SecretPurposes.StaffTotp));

    /// <summary>TOTP kodini tekshiradi (bir kod — bir marta). Muvaffaqiyatda kirishni yakunlaydi (hisoblagichlar, audit).</summary>
    public async Task<bool> VerifyTotpAndCompleteAsync(StaffUser user, string? code, CancellationToken ct = default)
    {
        if (user.LockedUntilUtc > clock.UtcNow) return false;
        var secret = GetTotpSecret(user);
        if (secret is null || !Totp.Verify(secret, code, clock.Now, user.TotpLastStep, out long step))
        {
            await RegisterFailureAsync(user, "totp", ct);
            return false;
        }
        user.TotpLastStep = step;
        await CompleteLoginAsync(user, ct);
        return true;
    }

    /// <summary>Birinchi ulash: yangi sir + kod mos kelsa saqlanadi.</summary>
    public async Task<bool> EnrollTotpAndCompleteAsync(StaffUser user, byte[] secret, string? code, CancellationToken ct = default)
    {
        if (!Totp.Verify(secret, code, clock.Now, 0, out long step))
        {
            await RegisterFailureAsync(user, "totp-enroll", ct);
            return false;
        }
        user.TotpSecretProtected = protector.Protect(Base32.Encode(secret), SecretPurposes.StaffTotp);
        user.TotpLastStep = step;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add("staff.totp-enroll", "StaffUser", user.Id, staffIdOverride: user.Id);
        await CompleteLoginAsync(user, ct);
        return true;
    }

    private async Task CompleteLoginAsync(StaffUser user, CancellationToken ct)
    {
        user.FailedLogins = 0;
        user.LockedUntilUtc = null;
        user.LastLoginUtc = clock.UtcNow;
        audit.Add("staff.login", "StaffUser", user.Id, staffIdOverride: user.Id);
        await db.SaveChangesAsync(ct);
    }

    public static ClaimsPrincipal BuildPrincipal(StaffUser user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(StaffClaims.Stamp, user.SecurityStamp),
        };
        if (scheme == AuthSchemes.Staff)
            claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    /// <summary>Har so'rovda: xodim faolmi va SecurityStamp o'zgarmaganmi (parol/2FA/rol o'zgarsa — chiqarib yuboriladi).</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null || !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
        {
            context.RejectPrincipal();
            return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<ControlDbContext>();
        var user = await db.StaffUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null || !user.IsActive || user.SecurityStamp != principal.FindFirstValue(StaffClaims.Stamp)
            || (context.Scheme.Name == AuthSchemes.Staff && principal.FindFirstValue(ClaimTypes.Role) != user.Role.ToString()))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }

    /// <summary>Yangi xodim (CLI yoki Owner). Parol siyosati tekshiriladi; TOTP birinchi kirishda ulanadi.</summary>
    public async Task<StaffUser> CreateStaffAsync(string email, string fullName, StaffRole role, string password, int? createdBy, CancellationToken ct = default)
    {
        string e = NormalizeEmail(email);
        if (e.Length < 5 || !e.Contains('@')) throw new DomainException("Email noto'g'ri.");
        if (PasswordHasher.ValidatePolicy(password) is string err) throw new DomainException(err);
        if (await db.StaffUsers.AnyAsync(u => u.Email == e, ct)) throw new DomainException("Bu email bilan xodim bor.");
        var user = new StaffUser
        {
            Email = e, FullName = fullName.Trim(), Role = role, PasswordHash = PasswordHasher.Hash(password),
            IsActive = true, CreatedAtUtc = clock.UtcNow,
        };
        db.StaffUsers.Add(user);
        await db.SaveChangesAsync(ct);
        audit.Add("staff.create", "StaffUser", user.Id, after: new { user.Email, user.FullName, user.Role }, staffIdOverride: createdBy);
        await db.SaveChangesAsync(ct);
        return user;
    }
}
