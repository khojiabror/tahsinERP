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
    /// <summary>Distributsiya paketi (distribyutorlar uchun, MES/BOM'siz) — EditionModules.Dist.</summary>
    public const string Dist = "DIST";
    /// <summary>Qo'shimcha xizmat «Sayt-katalog» — paketga kirmaydi, alohida obuna; kalitda Edition'ga "+SITE" bo'lib qo'shiladi.</summary>
    public const string Site = "SITE";
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
    /// <summary>
    /// Qo'shimcha xizmat (paketdan tashqari, masalan SITE): o'z obunasi, narxi va hisob-fakturasi bor, lekin tenant holatini
    /// belgilamaydi va kalitda alohida Edition emas — asosiy paket kodiga "+KOD" bo'lib qo'shiladi (EditionModules.AddOnsOf).
    /// Code = EditionModules'dagi xizmat kodi. MaxUsers = 0 (foydalanuvchiga bog'liq emas).
    /// </summary>
    public bool IsAddOn { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum DeploymentKind { SaaS = 1, OnPrem = 2 }

public enum TenantStatus { Trial = 1, Active = 2, Grace = 3, Suspended = 4, Archived = 5 }

/// <summary>
/// On-prem onlayn tekshiruv siyosati (00_PLAN.md 11 #5, 08_STAGE4.md 4). Optional — kalit shartnoma davri (≤ 1 yil), internet
/// bo'lsa pull, bo'lmasa offline. Required — kalit qisqa (OfflineGraceDays) va panel uni o'zi yangilab turadi: ERP pull qilmasa
/// kalit muddati tugaydi. SaaS'ga ta'sir qilmaydi.
/// </summary>
public enum OnlineCheckMode { Optional = 1, Required = 2 }

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
    /// <summary>
    /// Provisioning agenti siri (HMAC kaliti), SecretProtector bilan shifrlangan. Null — agent ulanmagan.
    /// Agent faqat shu server tenantlarining joblarini oladi (05_PROVISIONING.md).
    /// </summary>
    public string? AgentSecretProtected { get; set; }
    public DateTime? AgentLastSeenUtc { get; set; }
    public string? AgentVersion { get; set; }
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

    /// <summary>Create job muvaffaqiyatli tugagan vaqt (baza + admin + tenants.json tayyor). Null — hali provisioning qilinmagan.</summary>
    public DateTime? ProvisionedAtUtc { get; set; }
    /// <summary>ERP tenants.json dagi oxirgi qo'llangan holat: true/false — enabled, null — reestrda yo'q.</summary>
    public bool? RegistryEnabled { get; set; }
    /// <summary>Archive job yaratgan zaxira joyi (IBackupStore manzili). Delete'dan keyin null.</summary>
    public string? ArchiveLocation { get; set; }
    /// <summary>Delete job tugagan vaqt: baza, zaxira va reestr yozuvi o'chirilgan. Moliyaviy tarix uchun qator qoladi.</summary>
    public DateTime? DeletedAtUtc { get; set; }
    /// <summary>Baza namuna ma'lumotlar bilan yaratilgan/yaratiladi (Database/Demo skriptlari).</summary>
    public bool SampleData { get; set; }
    /// <summary>Sinov muddatidan oldin yakunlangan vaqt (EndTrial job tugagan).</summary>
    public DateTime? TrialEndedAtUtc { get; set; }

    /// <summary>On-prem: onlayn tekshiruv majburiymi (faqat On-prem uchun ma'noli).</summary>
    public OnlineCheckMode OnlineCheck { get; set; } = OnlineCheckMode.Optional;
    /// <summary>On-prem: internetsiz ishlash muddati, kun (Required — kalit uzunligi; Optional — ogohlantirish chegarasi). Null — Panel:OnPremOfflineGraceDays.</summary>
    public int? OfflineGraceDays { get; set; }

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
    /// <summary>Didox e-hisob-faktura ID (4-bosqich, 08_STAGE4.md 3).</summary>
    public string? DidoxId { get; set; }
    /// <summary>Didox hujjat holati (kod va nomi, masalan "3 Imzolangan"), oxirgi tekshiruvdagi.</summary>
    public string? DidoxStatus { get; set; }
    public DateTime? DidoxSentAtUtc { get; set; }
    public DateTime? DidoxCheckedAtUtc { get; set; }
    /// <summary>Oxirgi yuborish/tekshiruv xatosi (bo'sh — xato yo'q).</summary>
    public string? DidoxError { get; set; }
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
    /// <summary>Null — tizim (fon ishi yoki License API) yoki mijoz portali (PortalUserId).</summary>
    public int? StaffUserId { get; set; }
    /// <summary>Mijoz portali foydalanuvchisi amali (my.&lt;domen&gt;).</summary>
    public int? PortalUserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Ip { get; set; }
    public DateTime AtUtc { get; set; }
}

/// <summary>
/// License API pull so'rovlarining ishlatilgan nonce'lari (replay himoyasi). Bazada — panel bir nechta instansda ishlasa ham
/// bitta nonce ikki marta qabul qilinmaydi. Muddati o'tganlari kunlik ishda o'chiriladi.
/// </summary>
public class ApiNonce
{
    public long Id { get; set; }
    public string InstallationId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}

public enum ProvisioningJobType { Create = 1, Reset = 2, Suspend = 3, Resume = 4, Archive = 5, Delete = 6, EndTrial = 7 }

public enum ProvisioningJobStatus { Pending = 1, Running = 2, Succeeded = 3, Failed = 4, Cancelled = 5 }

/// <summary>
/// Provisioning navbati (00_PLAN.md 7, 05_PROVISIONING.md). Panel yozadi, ERP serveridagi agent /api/agent orqali oladi
/// (lease) va natijani qaytaradi. Bir tenantda bir vaqtda faqat bitta faol (Pending/Running) job.
/// </summary>
public class ProvisioningJob
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public ProvisioningJobType Type { get; set; }
    public ProvisioningJobStatus Status { get; set; }
    /// <summary>Agentga beriladigan ochiq parametrlar (JSON): slug, baza, kompaniya nomi, admin login, demo...</summary>
    public string Payload { get; set; } = "{}";
    /// <summary>
    /// Maxfiy parametrlar (JSON: reg_token, admin paroli), SecretProtector bilan shifrlangan. Job tugagach (muvaffaqiyat
    /// yoki yakuniy xato) o'chiriladi.
    /// </summary>
    public string? SecretsProtected { get; set; }
    /// <summary>Takrorlanmaslik kaliti (masalan demo-reset:2026-10-19) — bir xil kalit bilan ikkinchi job yaratilmaydi.</summary>
    public string? IdempotencyKey { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    /// <summary>Pending job shu vaqtdan oldin berilmaydi (qayta urinish orasidagi pauza).</summary>
    public DateTime NotBeforeUtc { get; set; }
    /// <summary>Running: agent shu vaqtgacha band qilgan; o'tib ketsa (agent yiqilgan) — qayta beriladi.</summary>
    public DateTime? LeaseUntilUtc { get; set; }
    /// <summary>Joriy lease'ning tasodifiy tokeni — eski (muddati o'tgan) lease bilan natija qabul qilinmaydi.</summary>
    public string? LeaseToken { get; set; }
    public int? LeasedByServerId { get; set; }
    /// <summary>Agent log'i (har urinish qo'shib yoziladi, maxfiy qiymatlarsiz).</summary>
    public string Log { get; set; } = "";
    /// <summary>Oxirgi natija JSON (maxfiy maydonlarsiz).</summary>
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int? ExitCode { get; set; }
    public int? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive => Status is ProvisioningJobStatus.Pending or ProvisioningJobStatus.Running;
}

/// <summary>
/// Tenant faolligini yig'ish holati (agent → /api/agent/usage). Tenants qatoridan alohida — agent yozuvlari xodim tahririda
/// RowVersion to'qnashuvi bermasin.
/// </summary>
public class UsageState
{
    public int TenantId { get; set; }
    public long EntryCursor { get; set; }
    public long LogCursor { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastActionUtc { get; set; }
    public string? Error { get; set; }
    /// <summary>Agent limitga yetdi — keyingi so'rovda darhol davom etadi.</summary>
    public bool More { get; set; }
}

/// <summary>Tenant ERP foydalanuvchisi (faqat login va holat; F.I.Sh./email/parol panelga kelmaydi).</summary>
public class UsageUser
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string Login { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastActionUtc { get; set; }
}

public enum UsageEventKind { Login = 1, Action = 2 }

/// <summary>Kirish yoki amal (USER_ENTRIES / USERLOGS nusxasi, 90 kun saqlanadi; IP/MAC yo'q).</summary>
public class UsageEvent
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public UsageEventKind Kind { get; set; }
    public long SourceId { get; set; }
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    /// <summary>Modul (ProductModule nomi: Srm, Crm, ..., Core).</summary>
    public string? Module { get; set; }
    /// <summary>Normallashtirilgan controller — «sahifa».</summary>
    public string? Controller { get; set; }
    public UsageActionType ActionType { get; set; }
    public string? Action { get; set; }
}

/// <summary>USERLOGS amal turi (contracts bilan bir xil qiymatlar).</summary>
public enum UsageActionType : byte { Other = 0, View = 1, Create = 2, Edit = 3, Delete = 4, Export = 5 }

// ---------------- Traction agregatlari (07_TRACTION.md) — Toshkent sanasi bo'yicha, 2+ yil saqlanadi ----------------

/// <summary>Kunlik: tenant × modul × controller × amal turi → amallar soni. UsageEvents'dan qayta hisoblanadi.</summary>
public class UsageDailyModule
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string Module { get; set; } = "";
    public string Controller { get; set; } = "";
    public UsageActionType ActionType { get; set; }
    public int Count { get; set; }
}

/// <summary>Kunlik: tenantning faol foydalanuvchisi (DAU/WAU/MAU, kohortalar, faol kunlar shu jadvaldan).</summary>
public class UsageDailyUser
{
    public int TenantId { get; set; }
    public DateOnly Date { get; set; }
    public int UserId { get; set; }
    public int Logins { get; set; }
    public int Actions { get; set; }
}

/// <summary>Kunlik biznes hajmi: hujjat turi bo'yicha soni (hujjat sanasi bo'yicha, agent jadvallardan hisoblaydi).</summary>
public class UsageDailyDoc
{
    public int TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string DocType { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Hisob-faktura raqamlari hisoblagichi (yil bo'yicha).</summary>
public class InvoiceSequence
{
    public int Year { get; set; }
    public int LastValue { get; set; }
}

// ── Mijoz portali (3-bosqich, Docs/ControlPanel/06_CUSTOMER_PORTAL.md) ─────────────────────────────────────────────

public enum PortalRole { Owner = 1, Viewer = 2 }

/// <summary>
/// Mijoz portali foydalanuvchisi (my.&lt;domen&gt;). Tenant'ga bog'liq; bir tenant'da bir nechta. Xodimlardan (StaffUser) butunlay
/// alohida: boshqa jadval, boshqa cookie va authentication scheme. Kirish: email/telefon + parol (PBKDF2) + bir martalik kod (Telegram).
/// </summary>
public class PortalUser
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string FullName { get; set; } = "";
    /// <summary>Kichik harfda; global noyob (kirishda tenant tanlanmaydi).</summary>
    public string? Email { get; set; }
    /// <summary>+998XXXXXXXXX ko'rinishida; global noyob.</summary>
    public string? Phone { get; set; }
    /// <summary>Null — taklif hali qabul qilinmagan (parol o'rnatilmagan).</summary>
    public string? PasswordHash { get; set; }
    public PortalRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Bot /start orqali bog'langan shaxsiy chat. Null bo'lsa kod tenant chatiga (Tenant.TelegramChatId) yuboriladi.</summary>
    public string? TelegramChatId { get; set; }
    /// <summary>Kod kanali: null/"telegram" (standart), "sms" (Phone), "email" (Email). Kanal ishlamasa — Telegram'ga qaytadi.</summary>
    public string? OtpChannel { get; set; }
    /// <summary>Telegram bog'lash kodi xeshi (SHA-256 hex) — t.me/&lt;bot&gt;?start=&lt;kod&gt;.</summary>
    public string? TelegramLinkHash { get; set; }
    public DateTime? TelegramLinkExpiresUtc { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public int? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Parol/rol/holat o'zgarsa almashadi — eski portal sessiyalari bekor bo'ladi.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Taklif / parolni tiklash havolasi: bir martalik, muddatli, faqat SHA-256 xeshi saqlanadi.</summary>
public class PortalInvite
{
    public int Id { get; set; }
    public int PortalUserId { get; set; }
    public PortalUser? PortalUser { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    /// <summary>Yangi taklif chiqarilganda eskisi yopiladi.</summary>
    public DateTime? RevokedAtUtc { get; set; }
    public int? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Bir martalik kirish kodi. Kodning o'zi saqlanmaydi — tuz + SHA-256.</summary>
public class PortalOtp
{
    public long Id { get; set; }
    public int PortalUserId { get; set; }
    public string Salt { get; set; } = "";
    public string CodeHash { get; set; } = "";
    /// <summary>telegram / log / (keyin: sms, email).</summary>
    public string Channel { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Ip { get; set; }
}

public enum PortalRequestKind
{
    /// <summary>Joriy paket va davr — uzaytirish (joriy obunaga hisob-faktura).</summary>
    Renew = 1,
    /// <summary>Yuqoriroq paket — darhol (yangi obuna bugundan, to'langach eskisi yopiladi).</summary>
    Upgrade = 2,
    /// <summary>Pastroq paket yoki davr o'zgarishi — faqat joriy davr oxiridan.</summary>
    ChangeAtPeriodEnd = 3,
    /// <summary>Pullik obuna yo'q (sinov / to'xtatilgan) — yangi obuna bugundan.</summary>
    New = 4,
}

public enum PortalRequestStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

/// <summary>Tarifni oshirish / uzaytirish so'rovi: mijoz yuboradi → xodim tasdiqlaydi (qoralama hisob-faktura) yoki rad etadi.</summary>
public class PortalRequest
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int PortalUserId { get; set; }
    public PortalUser? PortalUser { get; set; }
    public PortalRequestKind Kind { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public BillingPeriod Period { get; set; }
    public int PeriodCount { get; set; }
    /// <summary>Yangi davr boshlanadigan sana (so'rov paytida hisoblangan; ChangeAtPeriodEnd — joriy davr oxiri + 1).</summary>
    public DateOnly EffectiveDate { get; set; }
    /// <summary>So'rov paytidagi joriy obuna (bo'lsa).</summary>
    public int? CurrentSubscriptionId { get; set; }
    public string? Comment { get; set; }
    public PortalRequestStatus Status { get; set; }
    public string? DecisionReason { get; set; }
    public int? DecidedByStaffId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    /// <summary>Tasdiqlanganda yaratilgan qoralama hisob-faktura va (kerak bo'lsa) yangi obuna.</summary>
    public int? InvoiceId { get; set; }
    public int? SubscriptionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

// ── Bank ko'chirmasi importi (4-bosqich, Docs/ControlPanel/08_STAGE4.md 2) ───────────────────────────────────────

public enum BankImportFormat { OneC = 1, Csv = 2, Xlsx = 3 }

/// <summary>Yuklangan bank ko'chirmasi fayli (sarlavha).</summary>
public class BankImport
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public BankImportFormat Format { get; set; }
    /// <summary>Fayl SHA-256 hex — bir xil fayl qayta yuklansa ogohlantirish uchun (qatorlar baribir DedupKey bilan himoyalangan).</summary>
    public string FileHash { get; set; } = "";
    /// <summary>1C fayldagi hisob raqami(lar)i (RasschSchet) yoki bo'sh.</summary>
    public string? Account { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public int UploadedByStaffId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public List<BankImportLine> Lines { get; set; } = [];
}

public enum BankMatchStatus
{
    /// <summary>Hisob-faktura topildi, qo'llash mumkin.</summary>
    Matched = 1,
    /// <summary>Bir nechta mos hisob-faktura — xodim tanlaydi.</summary>
    Ambiguous = 2,
    /// <summary>Mos hisob-faktura topilmadi.</summary>
    Unmatched = 3,
    /// <summary>Bu bank tranzaksiyasi (sana+summa+hujjat №+to'lovchi hisobi) allaqachon qo'llangan.</summary>
    Duplicate = 4,
    /// <summary>Kiruvchi to'lov emas (bizdan chiqqan) — e'tiborsiz.</summary>
    NotIncoming = 5,
    /// <summary>Hisob-faktura topildi, lekin to'lab bo'lmaydi (to'langan/bekor/summa qoldiqdan katta).</summary>
    Blocked = 6,
}

public enum BankMatchMethod { None = 0, InvoiceNumber = 1, InnAmount = 2, Manual = 3 }

/// <summary>Applying — band qilingan, to'lov yozilmoqda (jarayon uzilsa shu holatda qoladi: to'lovlar ro'yxatini tekshiring).</summary>
public enum BankLineState { Pending = 1, Applied = 2, Skipped = 3, Failed = 4, Applying = 5 }

/// <summary>Ko'chirma qatori: tahlil, moslash natijasi va qo'llash holati.</summary>
public class BankImportLine
{
    public long Id { get; set; }
    public int ImportId { get; set; }
    public int LineNo { get; set; }
    public string? DocNumber { get; set; }
    public DateOnly DocDate { get; set; }
    public long AmountTiyin { get; set; }
    public string? PayerName { get; set; }
    public string? PayerInn { get; set; }
    public string? PayerAccount { get; set; }
    public string? Purpose { get; set; }
    /// <summary>SHA-256(sana|summa|hujjat №|to'lovchi hisobi) — bitta bank tranzaksiyasining kaliti.</summary>
    public string DedupKey { get; set; } = "";
    /// <summary>Qo'llanganda = DedupKey. Noyob indeks (NULL emaslar orasida) — bir tranzaksiya ikki marta qo'llanmaydi.</summary>
    public string? AppliedKey { get; set; }
    public BankMatchStatus MatchStatus { get; set; }
    public BankMatchMethod MatchMethod { get; set; }
    public int? InvoiceId { get; set; }
    /// <summary>Moslash izohi (masalan "TE-2026-000012 raqami bo'yicha", "2 ta mos hisob-faktura").</summary>
    public string? MatchNote { get; set; }
    public BankLineState State { get; set; } = BankLineState.Pending;
    public int? PaymentId { get; set; }
    public int? AppliedByStaffId { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public string? Error { get; set; }
}

// ---------------- Avto-yangilash (Docs/Deploy/03_AUTO_UPDATE.md) ----------------

/// <summary>GitHub relizi (UpdatesService keshlaydi). Imzosi tekshirilmaganlari o'rnatishga berilmaydi.</summary>
public class AppRelease
{
    public int Id { get; set; }
    /// <summary>yyyy.M.d.N</summary>
    public string Version { get; set; } = "";
    public string Tag { get; set; } = "";
    public string? Commit { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    /// <summary>O'zgarishlar ro'yxati (CI commit xabarlaridan yig'adi).</summary>
    public string? Notes { get; set; }
    /// <summary>manifest.json baytlari (Base64) — imzo aynan shularga; agentga o'zgarishsiz uzatiladi.</summary>
    public string ManifestBase64 { get; set; } = "";
    public string Signature { get; set; } = "";
    public bool SignatureValid { get; set; }
    public string? VerifyError { get; set; }
    /// <summary>JSON: { "erp.zip": assetId, ... }</summary>
    public string AssetsJson { get; set; } = "{}";
    /// <summary>Manifestdagi ilovalar va o'zgarganlari: "erp*,panel,bot*" (* — o'zgargan).</summary>
    public string Apps { get; set; } = "";
    public bool Prerelease { get; set; }
    public DateTime FetchedAtUtc { get; set; }
}

public enum UpdateJobStatus { Pending = 1, Running = 2, Succeeded = 3, Failed = 4, RolledBack = 5, Cancelled = 6 }

/// <summary>Yangilash navbati: panel yozadi, agent /api/agent/updates orqali oladi, Updater bajaradi.</summary>
public class UpdateJob
{
    public long Id { get; set; }
    public int ServerId { get; set; }
    public Server? Server { get; set; }
    public tahsinERP.Provisioning.Contracts.UpdateAction Action { get; set; }
    /// <summary>erp, panel, bot, site, agent.</summary>
    public string App { get; set; } = "";
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string? Slug { get; set; }
    public string? DbName { get; set; }
    public string? Version { get; set; }
    /// <summary>Navbatga qo'yilgan paytdagi o'rnatilgan versiya (audit va ko'rsatish uchun).</summary>
    public string? FromVersion { get; set; }
    public bool RestoreDatabase { get; set; }
    public UpdateJobStatus Status { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? Stage { get; set; }
    public string Log { get; set; } = "";
    public string? Error { get; set; }
    public string? InstalledVersion { get; set; }
    public int? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive => Status is UpdateJobStatus.Pending or UpdateJobStatus.Running;
}

/// <summary>Server inventari (agent har necha daqiqada yuboradi): o'rnatilgan versiyalar va ERP instance'lari.</summary>
public class ServerInventory
{
    public int ServerId { get; set; }
    /// <summary>UpdateInventory JSON.</summary>
    public string Json { get; set; } = "{}";
    public string? UpdaterVersion { get; set; }
    public DateTime ReportedAtUtc { get; set; }
}
