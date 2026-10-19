using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Portal;

public class PortalOptions
{
    /// <summary>Taklif / parol tiklash havolasi amal qilish muddati, soat.</summary>
    public int InviteHours { get; set; } = 72;
    /// <summary>Bir martalik kod amal qilish muddati, daqiqa.</summary>
    public int OtpMinutes { get; set; } = 5;
    /// <summary>Telegram bog'lash kodi muddati, soat.</summary>
    public int TelegramLinkHours { get; set; } = 24;
    /// <summary>Portal sessiyasi (sliding), soat.</summary>
    public int SessionHours { get; set; } = 4;
    /// <summary>
    /// Faqat lokal ishlab chiqish: Telegram sozlanmagan bo'lsa kod server log'iga yoziladi. Serverda HECH QACHON yoqilmasin.
    /// </summary>
    public bool OtpLogFallback { get; set; }
}

public static class PortalSchemes
{
    /// <summary>To'liq kirgan mijoz (parol + kod).</summary>
    public const string Portal = "Portal";
    /// <summary>Parol to'g'ri, kod hali kiritilmagan — faqat /Otp sahifasi.</summary>
    public const string PortalTwoFactor = "PortalTwoFactor";
    /// <summary>Host bo'yicha Staff yoki Portal'ga yo'naltiruvchi standart sxema (HttpContext.User shu zona foydalanuvchisi).</summary>
    public const string Zone = "Zone";
}

public static class PortalClaims
{
    public const string Tenant = "portal_tenant";
    public const string Role = "portal_role";
    public const string Stamp = "portal_stamp";
}

/// <summary>Mijoz kirgan holat: foydalanuvchi va tenant ID.</summary>
public static class PortalPrincipal
{
    public static ClaimsPrincipal Build(PortalUser user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.Phone ?? "") : user.FullName),
            new(PortalClaims.Tenant, user.TenantId.ToString()),
            new(PortalClaims.Stamp, user.SecurityStamp),
        };
        if (scheme == PortalSchemes.Portal)
            claims.Add(new Claim(PortalClaims.Role, user.Role.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    /// <summary>Har so'rovda: foydalanuvchi faol, SecurityStamp va rol o'zgarmagan, tenant yopilmagan.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var p = context.Principal;
        if (p is null || p.Identity?.AuthenticationType != context.Scheme.Name
            || !int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
        {
            context.RejectPrincipal();
            return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<ControlDbContext>();
        var user = await db.PortalUsers.AsNoTracking().Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null || !user.IsActive || user.SecurityStamp != p.FindFirstValue(PortalClaims.Stamp)
            || user.TenantId.ToString() != p.FindFirstValue(PortalClaims.Tenant)
            || user.Tenant is null || PortalAuthService.TenantClosed(user.Tenant)
            || (context.Scheme.Name == PortalSchemes.Portal && p.FindFirstValue(PortalClaims.Role) != user.Role.ToString()))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }
}

public static class PortalTokens
{
    /// <summary>Kriptografik tasodifiy token (Base32, URL va Telegram start parametri uchun xavfsiz belgilar).</summary>
    public static string New(string prefix, int bytes = 20) => prefix + Security.Base32.Encode(RandomNumberGenerator.GetBytes(bytes));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string HashOtp(string salt, string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + code.Trim())));

    public static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>Email / telefonni bir xil ko'rinishga keltirish.</summary>
public static class PortalIdentity
{
    public static string? NormalizeEmail(string? email)
    {
        string e = (email ?? "").Trim().ToLowerInvariant();
        return e.Length == 0 ? null : e;
    }

    /// <summary>"+998 90 123-45-67", "901234567", "998901234567" → "+998901234567". Noto'g'ri bo'lsa null.</summary>
    public static string? NormalizePhone(string? phone)
    {
        string digits = new((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;
        return digits.Length is >= 11 and <= 15 ? "+" + digits : null;
    }

    public static bool LooksLikeEmail(string? s) => (s ?? "").Contains('@');

    public static string Mask(string? chat) =>
        string.IsNullOrEmpty(chat) ? "" : chat.Length <= 4 ? "****" : new string('*', chat.Length - 3) + chat[^3..];
}

/// <summary>Mijozlarga (va xodimlar chatiga) Telegram xabarlari; Notifications jadvaliga yoziladi, takrorlanmaydi.</summary>
public sealed class PortalNotifier(ControlDbContext db, ITelegramSender telegram, IOptions<TelegramOptions> tg, Clock clock,
    ILogger<PortalNotifier> log)
{
    /// <summary>Tenant chati + tenant portal foydalanuvchilarining bog'langan chatlari.</summary>
    public async Task<int> NotifyTenantAsync(int tenantId, string template, string dedupBase, string text, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        var chats = await db.PortalUsers.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.TelegramChatId != null)
            .Select(u => u.TelegramChatId!).ToListAsync(ct);
        chats.Insert(0, tenant.TelegramChatId ?? "");
        var recipients = chats.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (recipients.Count == 0)
            return await SendAsync(tenantId, template, dedupBase, null, text, ct) ? 1 : 0;
        int n = 0;
        foreach (var chat in recipients)
            if (await SendAsync(tenantId, template, dedupBase, chat, text, ct)) n++;
        return n;
    }

    /// <summary>Xodimlar guruhiga (Telegram:StaffChatId), sozlanmagan bo'lsa — faqat log.</summary>
    public Task<bool> NotifyStaffAsync(int? tenantId, string template, string dedupBase, string text, CancellationToken ct = default) =>
        SendAsync(tenantId, template, dedupBase, string.IsNullOrWhiteSpace(tg.Value.StaffChatId) ? null : tg.Value.StaffChatId, text, ct);

    /// <summary>Bitta chatga bevosita (dedup'siz) — masalan bot javobi. Xato yutiladi.</summary>
    public async Task<bool> SendDirectAsync(string chatId, string text, CancellationToken ct = default)
    {
        if (!telegram.IsConfigured)
        {
            log.LogInformation("Telegram sozlanmagan → {Chat}: {Text}", chatId, text);
            return false;
        }
        try
        {
            await telegram.SendAsync(chatId, text, ct);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Telegram xabari yuborilmadi ({Chat})", chatId);
            return false;
        }
    }

    private async Task<bool> SendAsync(int? tenantId, string template, string dedupBase, string? chatId, string text, CancellationToken ct)
    {
        string dedup = $"{dedupBase}|{chatId ?? "-"}";
        if (dedup.Length > 200) dedup = dedup[..200];
        if (await db.Notifications.AnyAsync(n => n.DedupKey == dedup, ct))
            return false;
        var n = new Notification
        {
            TenantId = tenantId, Template = template, DedupKey = dedup, Recipient = chatId, Message = text, CreatedAtUtc = clock.UtcNow,
        };
        if (chatId is null || !telegram.IsConfigured)
        {
            n.Channel = NotificationChannel.Log;
            n.Status = NotificationStatus.Logged;
            log.LogInformation("Xabar (Telegram sozlanmagan yoki chat yo'q) → {Chat}: {Text}", chatId ?? "-", text);
        }
        else
        {
            n.Channel = NotificationChannel.Telegram;
            try
            {
                await telegram.SendAsync(chatId, text, ct);
                n.Status = NotificationStatus.Sent;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                n.Status = NotificationStatus.Failed;
                n.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                log.LogWarning(ex, "Telegram xabari yuborilmadi ({Chat})", chatId);
            }
        }
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
        return n.Status != NotificationStatus.Failed;
    }
}
