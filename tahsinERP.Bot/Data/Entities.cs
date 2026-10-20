namespace tahsinERP.Bot.Data;

/// <summary>Tenant (firma): o'z boti va ERP manzili. Token shifrlangan (SecretBox).</summary>
public sealed class BotTenant
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string ErpBaseUrl { get; set; } = "";

    /// <summary>ERP tomonidagi tenant (imzo kaliti shundan): bitta bazali ERP — "default", DB-per-tenant — o'sha slug.</summary>
    public string? ErpTenant { get; set; }
    public string TokenEnc { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string? BotUsername { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateOnly? LastSummaryDate { get; set; }
}

/// <summary>Kelgan update (webhook darhol 200 qaytaradi, ishlov — fonda). (TenantId, UpdateId) — takrorlanmaydi.</summary>
public sealed class BotInboxItem
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public long UpdateId { get; set; }
    public long? ChatId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

/// <summary>Yuboriladigan Telegram chaqiruvi. Priority 0 — foydalanuvchiga javob, 1 — bildirishnoma/yakun.</summary>
public sealed class BotOutboxItem
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public long ChatId { get; set; }
    public string Method { get; set; } = "";
    public string Payload { get; set; } = "";
    public byte Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public long? ErpNotificationId { get; set; }
    public DateTime? AckedAt { get; set; }
}

/// <summary>ERP'dagi TELEGRAM_ACCOUNTS keshi (yo'qolsa — /DstApi/Account orqali tiklanadi).</summary>
public sealed class BotLink
{
    public int TenantId { get; set; }
    public long TelegramUserId { get; set; }
    public long ChatId { get; set; }
    public int? UserId { get; set; }
    public int? CustomerId { get; set; }
    public string Role { get; set; } = "";
    public string? Name { get; set; }
    public string Language { get; set; } = "uz";
    public DateTime LinkedAt { get; set; }
}

/// <summary>Suhbat holati (masalan, "to'lov summasini kutyapman"). Data — JSON.</summary>
public sealed class BotSession
{
    public int TenantId { get; set; }
    public long TelegramUserId { get; set; }
    public string State { get; set; } = "";
    public string Data { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
