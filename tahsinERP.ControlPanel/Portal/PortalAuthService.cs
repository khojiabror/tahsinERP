using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Portal;

public sealed record InviteInfo(PortalInvite Invite, PortalUser User, Tenant Tenant);

public sealed record InviteResult(string Token, PortalInvite Invite, bool SentToTelegram);

public sealed record AcceptResult(PortalUser User, string? TelegramLinkToken, bool CanLogin);

/// <summary>
/// Mijoz portali kirishi (06_CUSTOMER_PORTAL.md):
/// taklif (xodim) → parol o'rnatish (bir martalik havola) → [Telegram bog'lash] → email/telefon + parol → bir martalik kod.
/// 5 xato (parol yoki kod) → 15 daqiqa blok. Parol unutilsa — xodim yangi taklif yuboradi (eski parol va sessiyalar bekor).
/// Har amal AuditLog'ga (PortalUserId bilan).
/// </summary>
public sealed class PortalAuthService(ControlDbContext db, AuditLogger audit, Clock clock, IEnumerable<IOtpSender> senders,
    IOptions<PortalOptions> options, ILogger<PortalAuthService> log)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);
    public const int MaxOtpAttempts = 5;
    /// <summary>Kod qayta yuborish: kamida shuncha vaqt oralig'ida va 15 daqiqada ko'pi bilan MaxOtpPerWindow ta.</summary>
    public static readonly TimeSpan OtpResendInterval = TimeSpan.FromSeconds(60);
    public const int MaxOtpPerWindow = 5;
    public static readonly TimeSpan OtpWindow = TimeSpan.FromMinutes(15);
    public const string GenericLoginError = "Login yoki parol noto'g'ri.";
    public const string LockedError = "Hisob vaqtincha bloklangan (ko'p xato urinish). 15 daqiqadan keyin urinib ko'ring.";

    private static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash(Guid.NewGuid().ToString()));

    private PortalOptions O => options.Value;

    /// <summary>Arxivlangan yoki o'chirilgan tenant mijozlari portalga kira olmaydi (to'xtatilgan — kira oladi: to'lash uchun).</summary>
    public static bool TenantClosed(Tenant t) => t.Status == TenantStatus.Archived || t.DeletedAtUtc != null;

    // ── Foydalanuvchilar (xodim boshqaradi) ──────────────────────────────────────────────────────────────────────

    public async Task<PortalUser> CreateUserAsync(int tenantId, string fullName, string? email, string? phone, PortalRole role, int staffId,
        CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new DomainException("Tenant topilmadi.");
        if (TenantClosed(tenant)) throw new DomainException("Arxivdagi tenantga portal foydalanuvchisi qo'shilmaydi.");
        string? e = PortalIdentity.NormalizeEmail(email);
        string? p = string.IsNullOrWhiteSpace(phone) ? null : PortalIdentity.NormalizePhone(phone)
            ?? throw new DomainException("Telefon raqami noto'g'ri (masalan +998901234567).");
        if (e is null && p is null) throw new DomainException("Email yoki telefon kiriting (kirish uchun login).");
        if (e is not null && (e.Length < 5 || !e.Contains('@') || e.Length > 200)) throw new DomainException("Email noto'g'ri.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Ism kiriting.");
        if (e is not null && await db.PortalUsers.AnyAsync(u => u.Email == e, ct)) throw new DomainException("Bu email bilan portal foydalanuvchisi bor.");
        if (p is not null && await db.PortalUsers.AnyAsync(u => u.Phone == p, ct)) throw new DomainException("Bu telefon bilan portal foydalanuvchisi bor.");
        var user = new PortalUser
        {
            TenantId = tenantId, FullName = fullName.Trim(), Email = e, Phone = p, Role = role, IsActive = true,
            CreatedByStaffId = staffId, CreatedAtUtc = clock.UtcNow,
        };
        db.PortalUsers.Add(user);
        await db.SaveChangesAsync(ct);
        audit.Add("portal.user.create", "PortalUser", user.Id, after: new { user.TenantId, user.FullName, user.Email, user.Phone, user.Role }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Rol yoki faollikni o'zgartirish. Sessiyalar yopiladi (SecurityStamp).</summary>
    public async Task UpdateUserAsync(PortalUser user, PortalRole role, bool isActive, int staffId, CancellationToken ct = default)
    {
        var before = new { user.Role, user.IsActive };
        user.Role = role;
        user.IsActive = isActive;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        if (!isActive)
            await RevokeInvitesAsync(user.Id, ct);
        audit.Add("portal.user.update", "PortalUser", user.Id, before, new { user.Role, user.IsActive }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Telegram bog'lanishini bekor qilish (xodim; masalan telefon yo'qolganda).</summary>
    public async Task UnlinkTelegramAsync(PortalUser user, int staffId, CancellationToken ct = default)
    {
        audit.Add("portal.telegram-unlink", "PortalUser", user.Id, new { Chat = PortalIdentity.Mask(user.TelegramChatId) }, null, staffIdOverride: staffId);
        user.TelegramChatId = null;
        user.TelegramLinkHash = null;
        user.TelegramLinkExpiresUtc = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
    }

    // ── Taklif / parolni tiklash ─────────────────────────────────────────────────────────────────────────────────

    private async Task RevokeInvitesAsync(int userId, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        var open = await db.PortalInvites.Where(i => i.PortalUserId == userId && i.UsedAtUtc == null && i.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var i in open) i.RevokedAtUtc = now;
    }

    /// <summary>
    /// Yangi taklif (yoki parolni tiklash) havolasi. Avvalgi ochiq havolalar yopiladi. Token faqat shu natijada qaytadi —
    /// bazada SHA-256 xeshi. Foydalanuvchining shaxsiy Telegram chati bog'langan bo'lsa, havola o'sha chatga ham yuboriladi
    /// (tenant guruh chatiga — yo'q: guruhdagi hamma ko'radi).
    /// </summary>
    public async Task<InviteResult> CreateInviteAsync(PortalUser user, int staffId, string portalBaseUrl, PortalNotifier notifier, CancellationToken ct = default)
    {
        if (!user.IsActive) throw new DomainException("Foydalanuvchi o'chirilgan — avval faollashtiring.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == user.TenantId, ct);
        if (TenantClosed(tenant)) throw new DomainException("Tenant arxivda.");
        await RevokeInvitesAsync(user.Id, ct);
        string token = PortalTokens.New("PI-");
        var invite = new PortalInvite
        {
            PortalUserId = user.Id, TokenHash = PortalTokens.Hash(token), ExpiresUtc = clock.UtcNow.AddHours(O.InviteHours),
            CreatedByStaffId = staffId, CreatedAtUtc = clock.UtcNow,
        };
        db.PortalInvites.Add(invite);
        bool reset = user.PasswordHash is not null;
        audit.Add(reset ? "portal.reset-invite" : "portal.invite", "PortalUser", user.Id, after: new { invite.ExpiresUtc }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);

        bool sent = false;
        if (!string.IsNullOrWhiteSpace(user.TelegramChatId))
            sent = await notifier.SendDirectAsync(user.TelegramChatId,
                $"tahsinERP portal: {(reset ? "parolni tiklash" : "taklif")} havolasi ({O.InviteHours} soat amal qiladi, bir marta ishlaydi): {InviteUrl(portalBaseUrl, token)}", ct);
        return new InviteResult(token, invite, sent);
    }

    public static string InviteUrl(string portalBaseUrl, string token) => $"{portalBaseUrl.TrimEnd('/')}/Invite?token={Uri.EscapeDataString(token)}";

    public async Task<(InviteInfo? Info, string? Error)> FindInviteAsync(string? token, CancellationToken ct = default)
    {
        const string bad = "Havola yaroqsiz yoki muddati o'tgan. Yangi taklif so'rang.";
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return (null, bad);
        string hash = PortalTokens.Hash(token);
        var invite = await db.PortalInvites.Include(i => i.PortalUser!).ThenInclude(u => u.Tenant).FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invite is null || invite.UsedAtUtc != null || invite.RevokedAtUtc != null || invite.ExpiresUtc < clock.UtcNow
            || invite.PortalUser is not { IsActive: true } || invite.PortalUser.Tenant is null || TenantClosed(invite.PortalUser.Tenant))
            return (null, bad);
        return (new InviteInfo(invite, invite.PortalUser, invite.PortalUser.Tenant), null);
    }

    /// <summary>
    /// Parol o'rnatish. Havola yopiladi, eski sessiyalar bekor, blok olib tashlanadi. Kod yuborish kanali bo'lmasa
    /// (Telegram bog'lanmagan) — bog'lash tokeni qaytadi: havola egasi (taklif = egalik isboti) uni botga yuboradi.
    /// </summary>
    public async Task<AcceptResult> AcceptInviteAsync(string token, string password, string confirm, CancellationToken ct = default)
    {
        var (info, error) = await FindInviteAsync(token, ct);
        if (info is null) throw new DomainException(error!);
        if (password != confirm) throw new DomainException("Parollar mos emas.");
        if (PasswordHasher.ValidatePolicy(password) is string err) throw new DomainException(err);
        var user = info.User;
        bool reset = user.PasswordHash is not null;
        user.PasswordHash = PasswordHasher.Hash(password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.FailedLogins = 0;
        user.LockedUntilUtc = null;
        info.Invite.UsedAtUtc = clock.UtcNow;
        string? linkToken = null;
        bool canLogin = senders.Any(s => s.Resolve(user, info.Tenant) is not null);
        if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            linkToken = NewTelegramLink(user);
        audit.Add(reset ? "portal.password-reset" : "portal.password-set", "PortalUser", user.Id, after: new { InviteId = info.Invite.Id }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        return new AcceptResult(user, linkToken, canLogin);
    }

    // ── Telegram bog'lash ────────────────────────────────────────────────────────────────────────────────────────

    private string NewTelegramLink(PortalUser user)
    {
        string token = PortalTokens.New("L", 15);
        user.TelegramLinkHash = PortalTokens.Hash(token);
        user.TelegramLinkExpiresUtc = clock.UtcNow.AddHours(O.TelegramLinkHours);
        return token;
    }

    /// <summary>Kirgan foydalanuvchi uchun yangi bog'lash tokeni (Profil sahifasi).</summary>
    public async Task<string> CreateTelegramLinkAsync(PortalUser user, CancellationToken ct = default)
    {
        string token = NewTelegramLink(user);
        audit.Add("portal.telegram-link-code", "PortalUser", user.Id, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>Bot /start &lt;token&gt; — chatni foydalanuvchiga bog'laydi. Token bir martalik.</summary>
    public async Task<PortalUser?> LinkTelegramAsync(string token, string chatId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64 || string.IsNullOrWhiteSpace(chatId) || chatId.Length > 50) return null;
        string hash = PortalTokens.Hash(token);
        var user = await db.PortalUsers.FirstOrDefaultAsync(u => u.TelegramLinkHash == hash, ct);
        if (user is null || !user.IsActive || user.TelegramLinkExpiresUtc < clock.UtcNow) return null;
        var before = new { Chat = PortalIdentity.Mask(user.TelegramChatId) };
        user.TelegramChatId = chatId;
        user.TelegramLinkHash = null;
        user.TelegramLinkExpiresUtc = null;
        audit.Add("portal.telegram-link", "PortalUser", user.Id, before, new { Chat = PortalIdentity.Mask(chatId) }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        return user;
    }

    // ── Kirish ───────────────────────────────────────────────────────────────────────────────────────────────────

    public bool IsLocked(PortalUser user) => user.LockedUntilUtc > clock.UtcNow;

    public async Task<(PortalUser? User, string? Error)> CheckPasswordAsync(string? login, string? password, CancellationToken ct = default)
    {
        PortalUser? user = null;
        if (PortalIdentity.LooksLikeEmail(login))
        {
            string? e = PortalIdentity.NormalizeEmail(login);
            user = await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == e, ct);
        }
        else if (PortalIdentity.NormalizePhone(login) is string p)
            user = await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Phone == p, ct);

        if (user is null || !user.IsActive || user.PasswordHash is null || user.Tenant is null || TenantClosed(user.Tenant))
        {
            PasswordHasher.Verify(password ?? "", DummyHash.Value);
            return (null, GenericLoginError);
        }
        if (user.LockedUntilUtc > clock.UtcNow)
            return (null, LockedError);
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

    public async Task RegisterFailureAsync(PortalUser user, string stage, CancellationToken ct = default)
    {
        user.FailedLogins++;
        if (user.FailedLogins >= MaxFailedAttempts)
        {
            user.LockedUntilUtc = clock.UtcNow.Add(LockoutTime);
            user.FailedLogins = 0;
            audit.Add("portal.lockout", "PortalUser", user.Id, after: new { Stage = stage, user.LockedUntilUtc }, portalUserId: user.Id);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kod yuborish kanali: avval foydalanuvchi tanlagani (PortalUser.OtpChannel, standart telegram), u ishlamasa ro'yxatdagi
    /// birinchi mosi. Bo'lmasa null.
    /// </summary>
    public OtpTarget? ResolveTarget(PortalUser user, Tenant tenant, out IOtpSender? sender)
    {
        string preferred = string.IsNullOrWhiteSpace(user.OtpChannel) ? "telegram" : user.OtpChannel;
        foreach (var s in senders.OrderBy(s => s.Channel == preferred ? 0 : 1))
        {
            if (s.Resolve(user, tenant) is OtpTarget t)
            {
                sender = s;
                return t;
            }
        }
        sender = null;
        return null;
    }

    /// <summary>Yangi kod yaratib yuboradi (oldingilari yaroqsiz bo'ladi). Chastota cheklovi bilan.</summary>
    public async Task<OtpTarget> SendOtpAsync(PortalUser user, string? ip, CancellationToken ct = default)
    {
        var tenant = user.Tenant ?? await db.Tenants.FirstAsync(t => t.Id == user.TenantId, ct);
        if (user.LockedUntilUtc > clock.UtcNow) throw new DomainException(LockedError);
        var target = ResolveTarget(user, tenant, out var sender)
            ?? throw new DomainException("Kod yuborish uchun Telegram bog'lanmagan. Xodimdan yangi taklif havolasini so'rang.");
        DateTime now = clock.UtcNow;
        var recent = await db.PortalOtps.Where(o => o.PortalUserId == user.Id && o.CreatedAtUtc > now - OtpWindow)
            .OrderByDescending(o => o.CreatedAtUtc).Select(o => o.CreatedAtUtc).ToListAsync(ct);
        if (recent.Count > 0 && now - recent[0] < OtpResendInterval)
            throw new DomainException($"Kod yaqinda yuborildi — {OtpResendInterval.TotalSeconds:0} soniyadan keyin qayta so'rang.");
        if (recent.Count >= MaxOtpPerWindow)
            throw new DomainException("Juda ko'p kod so'raldi. 15 daqiqadan keyin urinib ko'ring.");

        // Avvalgi ishlatilmagan kodlar yopiladi — faqat oxirgisi amal qiladi.
        await db.PortalOtps.Where(o => o.PortalUserId == user.Id && o.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.ConsumedAtUtc, now), ct);

        string code = PortalTokens.NewOtpCode();
        string salt = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var otp = new PortalOtp
        {
            PortalUserId = user.Id, Salt = salt, CodeHash = PortalTokens.HashOtp(salt, code), Channel = target.Channel,
            ExpiresUtc = now.AddMinutes(O.OtpMinutes), CreatedAtUtc = now, Ip = ip,
        };
        db.PortalOtps.Add(otp);
        audit.Add("portal.otp-send", "PortalUser", user.Id, after: new { target.Channel, To = target.Display }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        try
        {
            await sender!.SendAsync(target, user, code, ct);
        }
        catch (OtpDeliveryException ex)
        {
            log.LogWarning(ex, "Portal OTP yuborilmadi (foydalanuvchi #{User})", user.Id);
            throw new DomainException(ex.Message);
        }
        return target;
    }

    /// <summary>Kodni tekshiradi; to'g'ri bo'lsa kirish yakunlanadi (hisoblagichlar, LastLogin, audit).</summary>
    public async Task<bool> VerifyOtpAndCompleteAsync(PortalUser user, string? code, CancellationToken ct = default)
    {
        if (user.LockedUntilUtc > clock.UtcNow) return false;
        DateTime now = clock.UtcNow;
        var otp = await db.PortalOtps.Where(o => o.PortalUserId == user.Id && o.ConsumedAtUtc == null && o.ExpiresUtc > now)
            .OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        string c = new((code ?? "").Where(char.IsDigit).ToArray());
        if (otp is null || c.Length != 6 || otp.Attempts >= MaxOtpAttempts || !PortalTokens.FixedEquals(PortalTokens.HashOtp(otp.Salt, c), otp.CodeHash))
        {
            if (otp is not null)
            {
                otp.Attempts++;
                if (otp.Attempts >= MaxOtpAttempts) otp.ConsumedAtUtc = now;
            }
            await RegisterFailureAsync(user, "otp", ct);
            return false;
        }
        otp.ConsumedAtUtc = now;
        user.FailedLogins = 0;
        user.LockedUntilUtc = null;
        user.LastLoginUtc = now;
        audit.Add("portal.login", "PortalUser", user.Id, after: new { otp.Channel }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── Kod kanali (4-bosqich) ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Profil sahifasida tanlash mumkin bo'lgan kanallar (shu foydalanuvchi uchun hozir ishlaydiganlari).</summary>
    public IReadOnlyList<OtpTarget> AvailableChannels(PortalUser user, Tenant tenant) =>
        senders.Select(s => s.Resolve(user, tenant)).Where(t => t is not null && t.Channel != "log").Select(t => t!)
            .GroupBy(t => t.Channel).Select(g => g.First()).ToList();

    public async Task SetOtpChannelAsync(PortalUser user, Tenant tenant, string? channel, CancellationToken ct = default)
    {
        string c = (channel ?? "").Trim().ToLowerInvariant();
        if (c is not ("telegram" or "sms" or "email")) throw new DomainException("Kanal noto'g'ri.");
        if (c != "telegram" && AvailableChannels(user, tenant).All(t => t.Channel != c))
            throw new DomainException(c == "sms" ? "SMS kanali mavjud emas (telefon kiritilmagan yoki SMS sozlanmagan)." : "Email kanali mavjud emas (email kiritilmagan yoki pochta sozlanmagan).");
        var before = new { Channel = user.OtpChannel ?? "telegram" };
        user.OtpChannel = c == "telegram" ? null : c;
        audit.Add("portal.otp-channel", "PortalUser", user.Id, before, new { Channel = c }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
    }

    // ── O'z-o'ziga parol tiklash (4-bosqich) ─────────────────────────────────────────────────────────────────────

    public const string ResetRequestedMessage =
        "Agar shu login bilan faol hisob bo'lsa, parolni tiklash havolasi uning shaxsiy kanaliga (Telegram, SMS yoki email) yuborildi. " +
        "Havola bir marta ishlaydi. Xabar kelmasa — xodimimizdan yangi havola so'rang.";
    public static readonly TimeSpan ResetInterval = TimeSpan.FromMinutes(2);
    public const int MaxResetsPerHour = 3;

    /// <summary>
    /// «Parolni unutdingizmi?»: login (email/telefon) bo'yicha bir martalik havola (PortalInvite, faqat xeshi) — foydalanuvchining
    /// shaxsiy kanaliga (tanlagani birinchi; tenant guruh chatiga emas). Hisob bor-yo'qligi oshkor qilinmaydi: chaqiruvchi har doim
    /// <see cref="ResetRequestedMessage"/> ni ko'rsatadi; bu metod faqat yuborildi-yuborilmadi qaytaradi (test/audit uchun).
    /// Cheklov: foydalanuvchiga 2 daqiqada bitta, soatiga 3 ta; IP bo'yicha — sahifaning «login» rate limiti.
    /// </summary>
    public async Task<bool> RequestPasswordResetAsync(string? login, string? ip, string portalBaseUrl, CancellationToken ct = default)
    {
        PortalUser? user = null;
        if (PortalIdentity.LooksLikeEmail(login))
        {
            string? e = PortalIdentity.NormalizeEmail(login);
            user = await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == e, ct);
        }
        else if (PortalIdentity.NormalizePhone(login) is string p)
            user = await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Phone == p, ct);
        // Taklifni hali qabul qilmagan (parolsiz) foydalanuvchi — xodim taklifi kerak; o'z-o'ziga tiklash yo'q.
        if (user is null || !user.IsActive || user.PasswordHash is null || user.Tenant is null || TenantClosed(user.Tenant))
            return false;

        DateTime now = clock.UtcNow;
        var recent = await db.PortalInvites.Where(i => i.PortalUserId == user.Id && i.CreatedByStaffId == null && i.CreatedAtUtc > now.AddHours(-1))
            .Select(i => i.CreatedAtUtc).ToListAsync(ct);
        if (recent.Count >= MaxResetsPerHour || recent.Any(t => now - t < ResetInterval))
        {
            audit.Add("portal.reset-throttled", "PortalUser", user.Id, after: new { Ip = ip }, portalUserId: user.Id);
            await db.SaveChangesAsync(ct);
            return false;
        }

        string preferred = string.IsNullOrWhiteSpace(user.OtpChannel) ? "telegram" : user.OtpChannel;
        IOtpSender? sender = null;
        OtpTarget? target = null;
        foreach (var s in senders.OrderBy(s => s.Channel == preferred ? 0 : 1))
            if (s.ResolveReset(user) is OtpTarget t) { sender = s; target = t; break; }
        if (sender is null || target is null)
        {
            audit.Add("portal.reset-request", "PortalUser", user.Id, after: new { Delivered = false, Reason = "no-channel" }, portalUserId: user.Id);
            await db.SaveChangesAsync(ct);
            return false;
        }

        await RevokeInvitesAsync(user.Id, ct);
        string token = PortalTokens.New("PR-");
        var invite = new PortalInvite
        {
            PortalUserId = user.Id, TokenHash = PortalTokens.Hash(token), ExpiresUtc = now.AddMinutes(Math.Clamp(O.ResetMinutes, 5, 24 * 60)),
            CreatedByStaffId = null, CreatedAtUtc = now,
        };
        db.PortalInvites.Add(invite);
        audit.Add("portal.reset-request", "PortalUser", user.Id, after: new { Delivered = true, target.Channel, To = target.Display, invite.ExpiresUtc }, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
        try
        {
            await sender.SendResetAsync(target, user, InviteUrl(portalBaseUrl, token), Math.Clamp(O.ResetMinutes, 5, 24 * 60), ct);
            return true;
        }
        catch (OtpDeliveryException ex)
        {
            log.LogWarning(ex, "Parol tiklash havolasi yuborilmadi (foydalanuvchi #{User})", user.Id);
            invite.RevokedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return false;
        }
    }

    public async Task ChangePasswordAsync(PortalUser user, string current, string next, string confirm, CancellationToken ct = default)
    {
        if (user.PasswordHash is null || !PasswordHasher.Verify(current ?? "", user.PasswordHash))
            throw new DomainException("Joriy parol noto'g'ri.");
        if (next != confirm) throw new DomainException("Parollar mos emas.");
        if (PasswordHasher.ValidatePolicy(next) is string err) throw new DomainException(err);
        user.PasswordHash = PasswordHasher.Hash(next);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add("portal.password-change", "PortalUser", user.Id, portalUserId: user.Id);
        await db.SaveChangesAsync(ct);
    }
}
