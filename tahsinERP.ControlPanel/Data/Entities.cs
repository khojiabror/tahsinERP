namespace tahsinERP.ControlPanel.Data;

// tahsin_control bazasi modeli (Docs/ControlPanel/00_PLAN.md, 4-bo'lim).
// Pul summalari hamma joyda tiyin (bigint) — yaxlitlash xatosi bo'lmaydi.
// Sanalar: kun aniqligidagilar DateOnly (Toshkent vaqti bo'yicha), lahzalar DateTime UTC.

public enum StaffRole { Owner = 1, Sales = 2, Support = 3 }

public class StaffUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public StaffRole Role { get; set; }
    /// <summary>TOTP siri (SecretProtector bilan shifrlangan). Null — hali ulanmagan: birinchi kirishda majburiy ulanadi.</summary>
    public string? TotpSecretProtected { get; set; }
    /// <summary>Oxirgi qabul qilingan TOTP qadami — bitta kodni ikki marta ishlatib bo'lmaydi.</summary>
    public long TotpLastStep { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLogins { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Parol/2FA/rol o'zgarsa almashadi — eski cookie'lar bekor bo'ladi.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}

public static class PlanCodes
{
    public const string Start = "START";
    public const string Biznes = "BIZNES";
    public const string Korp = "KORP";
    public const string Trial = "TRIAL";
}

public class Plan
{
    public int Id { get; set; }
    /// <summary>START/BIZNES/KORP/TRIAL — litsenziya kalitidagi Edition shu qiymat.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Oylik narx, QQS bilan (tiyin). Null — hali belgilanmagan (narxlar ochiq savol).</summary>
    public long? PriceMonthTiyin { get; set; }
    public long? PriceYearTiyin { get; set; }
    public int MaxUsers { get; set; }
    /// <summary>ERP ProductModule nomlari vergul bilan (Srm,Crm,Tasks,...). Ma'lumot uchun — cheklovni ERP Edition bo'yicha qiladi.</summary>
    public string Modules { get; set; } = "";
    /// <summary>QQS stavkasi, foiz (12).</summary>
    public decimal VatRatePercent { get; set; } = 12m;
    /// <summary>Faqat TRIAL: sinov muddati, kun.</summary>
    public int? TrialDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum DeploymentKind { SaaS = 1, OnPrem = 2 }

public enum TenantStatus { Trial = 1, Active = 2, Grace = 3, Suspended = 4, Archived = 5 }

public class Server
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>ERP IIS xosti (DNS nomi yoki IP).</summary>
    public string Host { get; set; } = "";
    public string? Region { get; set; }
    /// <summary>SQL Server instance (masalan "localhost\SQLEXPRESS"). Parol/login bu yerda SAQLANMAYDI.</summary>
    public string SqlInstance { get; set; } = "";
    public int MaxTenants { get; set; } = 15;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class Tenant
{
    public int Id { get; set; }
    /// <summary>Subdomen: a-z0-9-, bitta daraja (ERP TenantRegistry qoidasi). On-prem uchun ham noyob qisqa nom.</summary>
    public string Slug { get; set; } = "";
    public string LegalName { get; set; } = "";
    /// <summary>STIR (9 raqam) yoki JShShIR (14 raqam).</summary>
    public string? Inn { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TelegramChatId { get; set; }
    public string? Address { get; set; }
    public DeploymentKind Deployment { get; set; }
    public TenantStatus Status { get; set; }
    /// <summary>Litsenziya kalitidagi CompanyID (ERP tenants.json companyId). Odatda 1.</summary>
    public int CompanyId { get; set; } = 1;

    /// <summary>SaaS: tenant bazasi nomi (DB_&lt;slug&gt;). 2-bosqich provisioning to'ldiradi.</summary>
    public string? DbName { get; set; }
    public int? ServerId { get; set; }
    public Server? Server { get; set; }

    /// <summary>ERP o'zi yaratgan o'rnatma GUID'i (katta harf, D format) — register'da bog'lanadi.</summary>
    public string? InstallationId { get; set; }
    /// <summary>reg_token'ning SHA-256 hex'i (tokenning o'zi saqlanmaydi).</summary>
    public string? RegTokenHash { get; set; }
    public DateTime? RegTokenExpiresUtc { get; set; }
    public DateTime? RegTokenUsedUtc { get; set; }
    /// <summary>tenant_secret (HMAC kaliti), SecretProtector bilan shifrlangan. Pull imzosini tekshirish uchun qayta ochiladi.</summary>
    public string? ApiSecretProtected { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string? LastVersion { get; set; }
    public string? LastHost { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<Subscription> Subscriptions { get; set; } = [];
}

public enum BillingPeriod { Month = 1, Year = 2, Trial = 3 }

public enum SubscriptionStatus { Pending = 1, Active = 2, Expired = 3, Cancelled = 4 }

public class Subscription
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    /// <summary>Foydalanuvchilar soni (qat'iy paket: odatda Plan.MaxUsers).</summary>
    public int Users { get; set; }
    public BillingPeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    /// <summary>To'langan davr oxiri (shu kun ham kiradi). Pending obunada — StartDate'dan oldingi kun.</summary>
    public DateOnly EndDate { get; set; }
    public bool AutoRenew { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum InvoiceStatus { Draft = 1, Issued = 2, PartiallyPaid = 3, Paid = 4, Cancelled = 5 }

public class Invoice
{
    public int Id { get; set; }
    /// <summary>TE-YYYY-000001. Draft'da null — raqam chiqarilganda (Issue) beriladi, shuning uchun raqamlar uzluksiz.</summary>
    public string? Number { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    /// <summary>To'liq to'langanda shu obuna uzaytiriladi (bo'sh bo'lsa — faqat hisob-faktura, masalan xizmat).</summary>
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    /// <summary>Obuna necha davrga uzaytiriladi (Subscription.Period birligida).</summary>
    public int PeriodCount { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; }
    /// <summary>Jami, QQS bilan.</summary>
    public long TotalTiyin { get; set; }
    public long VatTiyin { get; set; }
    /// <summary>Storno qilinmagan to'lovlar yig'indisi.</summary>
    public long PaidTiyin { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    /// <summary>Obuna shu hisob-faktura bo'yicha uzaytirilgan vaqt — storno + qayta to'lovda ikki marta uzaytirilmaydi.</summary>
    public DateTime? SubscriptionAppliedUtc { get; set; }
    public string? Note { get; set; }
    /// <summary>Didox e-hisob-faktura ID (4-bosqich).</summary>
    public string? DidoxId { get; set; }
    public int? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<InvoiceLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public long RemainingTiyin => Math.Max(0, TotalTiyin - PaidTiyin);
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int LineNo { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    /// <summary>Birlik narxi, QQS bilan (tiyin).</summary>
    public long UnitPriceTiyin { get; set; }
    public decimal VatRatePercent { get; set; }
    /// <summary>Qator summasi QQS bilan.</summary>
    public long AmountTiyin { get; set; }
    /// <summary>Shundan QQS.</summary>
    public long VatTiyin { get; set; }
}

public enum PaymentMethod { Bank = 1, Cash = 2, Card = 3, Other = 4 }

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public PaymentMethod Method { get; set; }
    public long AmountTiyin { get; set; }
    public DateOnly PaidAt { get; set; }
    /// <summary>To'lov topshirig'i raqami / kvitansiya.</summary>
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public int RecordedByStaffId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public bool IsReversed { get; set; }
    public int? ReversedByStaffId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReverseReason { get; set; }
}

public enum LicenseSource { Auto = 1, Manual = 2, Register = 3 }

public class License
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int? SubscriptionId { get; set; }
    public string InstallationId { get; set; } = "";
    /// <summary>Imzolagan kalit jufti (k1/k2/...) — ERP LicenseHelper.TrustedPublicKeys bilan bir xil nom.</summary>
    public string KeyId { get; set; } = "";
    public string LicenseKey { get; set; } = "";
    /// <summary>lowercase(hex(SHA256(UTF8(license_key)))) — pull'dagi ETag.</summary>
    public string ETag { get; set; } = "";
    public int CompanyId { get; set; }
    public int MaxUsers { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string Edition { get; set; } = "";
    public LicenseSource Source { get; set; }
    /// <summary>Qo'lda chiqarilgan bo'lsa — xodim; avtomatik bo'lsa null.</summary>
    public int? IssuedByStaffId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? Reason { get; set; }
    /// <summary>Shu kalit o'rniga chiqarilgan yangi kalit. Null va RevokedAtUtc null — joriy (faol) kalit.</summary>
    public int? SupersededById { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByStaffId { get; set; }
    public string? RevokeReason { get; set; }
}

public enum NotificationChannel { Telegram = 1, Email = 2, Sms = 3, Log = 4 }

public enum NotificationStatus { Sent = 1, Logged = 2, Failed = 3 }

public class Notification
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public NotificationChannel Channel { get; set; }
    /// <summary>Shablon: expiry-14, expiry-7, expiry-3, expiry-1, expiry-0.</summary>
    public string Template { get; set; } = "";
    /// <summary>Takrorlanmaslik kaliti: tenant + shablon + obuna tugash sanasi + qabul qiluvchi.</summary>
    public string DedupKey { get; set; } = "";
    public string? Recipient { get; set; }
    public string Message { get; set; } = "";
    public NotificationStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }
    /// <summary>Null — tizim (fon ishi yoki License API).</summary>
    public int? StaffUserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Ip { get; set; }
    public DateTime AtUtc { get; set; }
}

/// <summary>Hisob-faktura raqamlari hisoblagichi (yil bo'yicha).</summary>
public class InvoiceSequence
{
    public int Year { get; set; }
    public int LastValue { get; set; }
}
