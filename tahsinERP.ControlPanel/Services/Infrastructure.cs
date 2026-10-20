using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services;

public class PanelOptions
{
    /// <summary>Biznes sanalari (obuna muddati, eslatmalar) shu vaqt zonasida hisoblanadi.</summary>
    public string TimeZone { get; set; } = "Asia/Tashkent";
    /// <summary>ERP'ga qaytariladigan poll_hours (1–24).</summary>
    public int PollHours { get; set; } = 6;
    /// <summary>reg_token amal qilish muddati, kun (kontrakt: 7).</summary>
    public int RegTokenDays { get; set; } = 7;
    /// <summary>SaaS kaliti = to'langan davr oxiri + shuncha kun (00_PLAN.md 3.4).</summary>
    public int SaasKeyExtraDays { get; set; } = 3;
    /// <summary>Muddat tugagandan keyin Grace holatidagi kunlar (ERP GraceDays bilan bir xil).</summary>
    public int GraceDays { get; set; } = 3;
    /// <summary>Hisob-faktura to'lov muddati, kun.</summary>
    public int InvoiceDueDays { get; set; } = 5;
    /// <summary>Kunlik fon ishi soati (TimeZone bo'yicha).</summary>
    public int DailyJobHour { get; set; } = 9;
    /// <summary>
    /// On-prem internetsiz ishlash muddati, kun (tenant'da OfflineGraceDays berilmasa). Required: kalit shuncha kunlik va yarmi
    /// o'tganda yangilanadi; Optional: ERP shuncha kun aloqa bo'lmasa ogohlantiradi, panelda «aloqa yo'q» ro'yxati (00_PLAN.md 3.3).
    /// </summary>
    public int OnPremOfflineGraceDays { get; set; } = 30;
}

/// <summary>Sotuvchi (vendor MChJ) rekvizitlari — hisob-faktura PDF'ida.</summary>
public class VendorOptions
{
    public string LegalName { get; set; } = "";
    public string Inn { get; set; } = "";
    public string VatCode { get; set; } = "";
    public string Address { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccount { get; set; } = "";
    public string Mfo { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Director { get; set; } = "";
    public string Accountant { get; set; } = "";
}

/// <summary>Vaqt manbai: UTC lahza va Toshkent bo'yicha bugungi sana. Testlarda TimeProvider almashtiriladi.</summary>
public sealed class Clock(TimeProvider time, IOptions<PanelOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    public DateTimeOffset Now => time.GetUtcNow();
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _zone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    /// <summary>Toshkent kunining boshlanishi (UTC).</summary>
    public DateTime LocalDayStartUtc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), _zone);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}

/// <summary>Joriy xodim va IP (HTTP so'rovdan; fon ishida — tizim).</summary>
public interface ICurrentActor
{
    int? StaffId { get; }
    string? Ip { get; }
}

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public int? StaffId =>
        int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
        && accessor.HttpContext!.User.Identity?.AuthenticationType == AuthSchemes.Staff ? id : null;

    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public static class AuthSchemes
{
    public const string Staff = "Staff";
    /// <summary>Parol to'g'ri, lekin TOTP hali kiritilmagan — faqat /Account/Totp va /Account/EnrollTotp ga kiradi.</summary>
    public const string TwoFactor = "TwoFactor";
}

/// <summary>
/// AuditLog yozuvchi. Yozuv kontekstga qo'shiladi va amal bilan BIRGA (bitta SaveChanges/tranzaksiya) saqlanadi —
/// amal saqlanib audit yo'qolishi yoki aksincha bo'lmaydi.
/// </summary>
public sealed class AuditLogger(ControlDbContext db, ICurrentActor actor, Clock clock)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    /// <summary>Joriy xodim (HTTP so'rovda), fon ishi/agentda null.</summary>
    public int? CurrentStaffId => actor.StaffId;

    public static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, Json);

    public void Add(string action, string entity, object? entityId, object? before = null, object? after = null, int? staffIdOverride = null,
        int? portalUserId = null)
    {
        db.AuditLog.Add(new AuditEntry
        {
            StaffUserId = staffIdOverride ?? actor.StaffId,
            PortalUserId = portalUserId,
            Action = action,
            Entity = entity,
            EntityId = entityId?.ToString(),
            Before = Serialize(before),
            After = Serialize(after),
            Ip = actor.Ip,
            AtUtc = clock.UtcNow,
        });
    }
}

/// <summary>Biznes qoidasi buzilganda (UI'da xabar sifatida ko'rsatiladi).</summary>
public class DomainException(string message) : Exception(message);
