using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;

namespace tahsinERP.Bot.Handlers;

/// <summary>Suhbat holatlari. Holat 2 soatdan keyin eskiradi.</summary>
public static class States
{
    public const string DayStart = "day_start";
    public const string DayEnd = "day_end";
    public const string Search = "search";
    public const string CheckIn = "checkin";
    public const string PayAmount = "pay_amount";
    public const string PayMethod = "pay_method";
    public const string Photo = "photo";
    public const string DaySelfie = "day_selfie";
    public const string NewName = "new_name";
    public const string NewPhone = "new_phone";
    public const string NewLocation = "new_location";
    public const string NewLandmark = "new_landmark";
    public const string NewPhoto = "new_photo";
    public const string NewConfirm = "new_confirm";
    public const string IssueText = "issue_text";
}

/// <summary>Holat ma'lumoti (barcha holatlar uchun bitta shakl — kerakli maydonlar to'ldiriladi).</summary>
public sealed class SessionData
{
    public int? CustomerId { get; set; }
    public int? VisitId { get; set; }
    public decimal? Amount { get; set; }
    public Guid? RequestId { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public bool SelfieRequired { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Landmark { get; set; }
    public string? PhotoKey { get; set; }
}

public sealed class SessionStore(BotDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    public async Task<(string? State, SessionData Data)> GetAsync(int tenantId, long telegramUserId, CancellationToken ct)
    {
        var s = await db.Sessions.FindAsync([tenantId, telegramUserId], ct);
        if (s == null || string.IsNullOrEmpty(s.State) || clock.GetUtcNow().UtcDateTime - s.UpdatedAt > Ttl) return (null, new SessionData());
        return (s.State, JsonSerializer.Deserialize<SessionData>(s.Data) ?? new SessionData());
    }

    public async Task SetAsync(int tenantId, long telegramUserId, string state, SessionData? data, CancellationToken ct)
    {
        var s = await db.Sessions.FindAsync([tenantId, telegramUserId], ct);
        if (s == null) db.Sessions.Add(s = new BotSession { TenantId = tenantId, TelegramUserId = telegramUserId });
        s.State = state;
        s.Data = JsonSerializer.Serialize(data ?? new SessionData());
        s.UpdatedAt = clock.GetUtcNow().UtcDateTime;
    }

    public async Task ClearAsync(int tenantId, long telegramUserId, CancellationToken ct)
    {
        var s = await db.Sessions.FindAsync([tenantId, telegramUserId], ct);
        if (s != null) db.Sessions.Remove(s);
    }

    /// <summary>Eskirgan holatlarni tozalash (fon xizmati chaqiradi).</summary>
    public Task<int> PurgeAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - Ttl;
        return db.Sessions.Where(s => s.UpdatedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
