using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Services.Editions;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Licensing;

public record LicenseRequest(string Edition, int MaxUsers, DateOnly StartDate, DateOnly ExpiryDate, int? SubscriptionId, string? Reason);

/// <summary>
/// v2 litsenziya kalitlarini chiqaradi (tahsinERP.Licensing.BuildV2Payload + ILicenseSigner + AssembleKey).
/// Har kalit chiqqach darhol ERP qiladigan tekshiruv (TryParseKey + IsBoundTo) bilan qayta tekshiriladi.
/// Tarix saqlanadi: yangi kalit avvalgi faol kalitni SupersededById bilan yopadi, hech narsa o'chirilmaydi.
/// </summary>
public sealed class LicenseIssuer(ControlDbContext db, ILicenseSigner signer, AuditLogger audit, Clock clock,
    IOptions<PanelOptions> options, ILogger<LicenseIssuer> log)
{
    public const int OnPremMaxDays = 366;
    /// <summary>Onlayn tekshiruv majburiy bo'lganda internetsiz muddat chegarasi: pull oralig'i (≤ 24 soat) va uzilishlarga joy qolsin.</summary>
    public const int MinOfflineGraceDays = 7;
    public const int MaxOfflineGraceDays = 90;

    /// <summary>Tenant uchun internetsiz ishlash muddati (kun), chegaralangan.</summary>
    public static int OfflineGraceDays(Tenant tenant, PanelOptions o) =>
        Math.Clamp(tenant.OfflineGraceDays ?? o.OnPremOfflineGraceDays, MinOfflineGraceDays, MaxOfflineGraceDays);

    /// <summary>Required on-prem: kalit qisqa (grace kun) va panel uni yangilab turadi — "aylanma kalit".</summary>
    public static bool IsRollingKey(Tenant tenant) =>
        tenant.Deployment == DeploymentKind.OnPrem && tenant.OnlineCheck == OnlineCheckMode.Required;

    /// <summary>Tenant'ning joriy (faol) kaliti: yopilmagan, bekor qilinmagan, joriy o'rnatmaga tegishli.</summary>
    public static IQueryable<License> CurrentQuery(ControlDbContext db, Tenant tenant) =>
        db.Licenses.Where(l => l.TenantId == tenant.Id && l.InstallationId == tenant.InstallationId
                               && l.SupersededById == null && l.RevokedAtUtc == null)
                   .OrderByDescending(l => l.IssuedAtUtc).ThenByDescending(l => l.Id);

    public Task<License?> GetCurrentAsync(Tenant tenant, CancellationToken ct = default) =>
        tenant.InstallationId is null ? Task.FromResult<License?>(null) : CurrentQuery(db, tenant).FirstOrDefaultAsync(ct);

    public static string ComputeETag(string licenseKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(licenseKey.Trim())));

    /// <summary>Kalit chiqaradi va saqlaydi (SaveChanges shu yerda). Chaqiruvchi kerak bo'lsa tranzaksiyaga o'raydi.</summary>
    public async Task<License> IssueAsync(Tenant tenant, LicenseRequest r, LicenseSource source, int? staffId, CancellationToken ct = default)
    {
        if (tenant.InstallationId is null)
            throw new DomainException("Tenant hali ERP o'rnatmasiga bog'lanmagan (InstallationId yo'q) — avval reg_token bilan ro'yxatdan o'tsin.");
        if (string.IsNullOrWhiteSpace(r.Edition))
            throw new DomainException("Edition (paket kodi) bo'sh.");
        if (r.MaxUsers < 0)
            throw new DomainException("Foydalanuvchilar soni manfiy bo'lishi mumkin emas.");
        if (r.ExpiryDate < r.StartDate)
            throw new DomainException("Tugash sanasi boshlanishdan oldin.");
        if (tenant.Deployment == DeploymentKind.OnPrem && r.ExpiryDate.DayNumber - r.StartDate.DayNumber > OnPremMaxDays)
            throw new DomainException("On-prem kalit 1 yildan uzun bo'lmasin (offline kalitni qaytarib olib bo'lmaydi, 00_PLAN.md 3.4).");

        string inst = ErpLicense.NormalizeInstallationId(tenant.InstallationId)
            ?? throw new DomainException("Tenant InstallationId yaroqsiz.");
        byte[] payload = ErpLicense.BuildV2Payload(tenant.CompanyId, r.MaxUsers,
            r.StartDate.ToDateTime(TimeOnly.MinValue), r.ExpiryDate.ToDateTime(TimeOnly.MinValue), r.Edition, inst);
        byte[] signature = await signer.SignAsync(payload, ct);
        string key = ErpLicense.AssembleKey(payload, signature);

        // ERP aynan shunday tekshiradi (LicenseService.ActivateCompanyLicense → TryParseKey + IsBoundTo + CompanyID).
        var trusted = new[] { new ErpLicense.TrustedPublicKey(signer.KeyId, signer.PublicKeyXml) };
        if (!ErpLicense.TryParseKey(key, trusted, out var info) || !info.IsBoundTo(inst) || info.CompanyID != tenant.CompanyId
            || info.Edition != r.Edition.Trim() || info.MaxUsers != r.MaxUsers)
            throw new CryptographicException("Chiqarilgan kalit o'z-o'zini tekshiruvdan o'tmadi — imzolash kaliti noto'g'ri sozlangan.");

        var previous = await CurrentQuery(db, tenant).ToListAsync(ct);
        var license = new License
        {
            TenantId = tenant.Id,
            SubscriptionId = r.SubscriptionId,
            InstallationId = inst,
            KeyId = signer.KeyId,
            LicenseKey = key,
            ETag = ComputeETag(key),
            CompanyId = tenant.CompanyId,
            MaxUsers = r.MaxUsers,
            StartDate = r.StartDate,
            ExpiryDate = r.ExpiryDate,
            Edition = r.Edition.Trim(),
            Source = source,
            IssuedByStaffId = staffId,
            IssuedAtUtc = clock.UtcNow,
            Reason = r.Reason,
        };
        db.Licenses.Add(license);
        await db.SaveChangesAsync(ct);

        foreach (var old in previous)
            old.SupersededById = license.Id;
        audit.Add("license.issue", "License", license.Id, before: previous.Select(p => new { p.Id, p.Edition, p.MaxUsers, p.ExpiryDate }).ToList(),
            after: new { license.TenantId, license.InstallationId, license.KeyId, license.Edition, license.MaxUsers, license.StartDate, license.ExpiryDate, license.Source, license.Reason },
            staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Kalit chiqarildi: tenant {Tenant} {Edition} {Users} → {Expiry} ({Source})", tenant.Slug, license.Edition, license.MaxUsers, license.ExpiryDate, source);
        return license;
    }

    /// <summary>
    /// Kalitdagi Edition qatori: paket kodi + qo'shimcha xizmatlar "+" bilan ("KORP" + ["SITE"] → "KORP+SITE").
    /// Takrorlar olib tashlanadi, tartib barqaror (EditionModules.AddOnModules tartibi) — bir xil to'plam doim bir xil qator,
    /// aks holda EnsureForTenantAsync har safar yangi kalit chiqarardi. ERP bu qatorni BaseEdition / AddOnsOf bilan o'qiydi.
    /// </summary>
    public static string ComposeEdition(string baseCode, IEnumerable<string>? addOnCodes)
    {
        if (string.IsNullOrWhiteSpace(baseCode) || baseCode.Contains('+'))
            throw new DomainException("Paket kodi noto'g'ri.");
        var codes = new List<(int Order, string Code)>();
        foreach (var raw in addOnCodes ?? [])
        {
            string code = (raw ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) continue;
            var modules = EditionModules.AddOnsOf("X+" + code);
            if (code.Contains('+') || modules.Count != 1)
                throw new DomainException($"Noma'lum qo'shimcha xizmat: {code}.");
            int order = EditionModules.AddOnModules.ToList().IndexOf(modules[0]);
            if (!codes.Any(c => c.Code == code)) codes.Add((order, code));
        }
        return baseCode.Trim() + string.Concat(codes.OrderBy(c => c.Order).ThenBy(c => c.Code, StringComparer.Ordinal).Select(c => "+" + c.Code));
    }

    /// <summary>
    /// <see cref="ComposeEdition(string, IEnumerable{string}?)"/> + obunada tanlangan modullar (Subscription.Modules, sinov):
    /// ("TRIAL", [], "Srm,Crm,Website") → "TRIAL:Srm,Crm+SITE". Tanlangan xizmat moduli (Website) faol xizmat obunasi bilan bir xil
    /// "+KOD" bo'ladi. <paramref name="modules"/> bo'sh/null — paketning hamma modullari (oldingi xatti-harakat).
    /// </summary>
    public static string ComposeEdition(string baseCode, IEnumerable<string>? addOnCodes, string? modules)
    {
        var selected = ParseModules(modules);
        if (selected is null)
            return ComposeEdition(baseCode, addOnCodes);
        string withModules = EditionModules.ComposeModules(baseCode.Trim(), selected);   // "TRIAL:Srm,Crm+SITE"
        int plus = withModules.IndexOf('+');
        string head = plus < 0 ? withModules : withModules[..plus];
        var addOns = (addOnCodes ?? []).Concat(plus < 0 ? [] : withModules[(plus + 1)..].Split('+'));
        return ComposeEdition(head, addOns);
    }

    /// <summary>"Srm,crm, Website" → modullar (NormalizeModules); bo'sh yoki birorta ham taniqli nom yo'q — null.</summary>
    public static IReadOnlyList<ProductModule>? ParseModules(string? modules)
    {
        if (string.IsNullOrWhiteSpace(modules)) return null;
        var list = new List<ProductModule>();
        foreach (var name in modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (EditionModules.TryParseModule(name, out var m) && m != ProductModule.Core) list.Add(m);
        return list.Count == 0 ? null : EditionModules.NormalizeModules(list);
    }

    /// <summary>Modullar → saqlanadigan qator ("Srm,Crm,Website"); null/bo'sh — null.</summary>
    public static string? FormatModules(IEnumerable<ProductModule>? modules)
    {
        var list = modules is null ? [] : EditionModules.NormalizeModules(modules);
        return list.Count == 0 ? null : string.Join(",", list);
    }

    /// <summary>Kalitga kiradigan qo'shimcha xizmat: faol, boshlangan va davri tugamagan obuna.</summary>
    public static bool AddOnCovers(Subscription s, DateOnly today) =>
        s.Plan is { IsAddOn: true } && s.Status == SubscriptionStatus.Active && s.StartDate <= today && s.EndDate >= today;

    /// <summary>
    /// Obunadan kelib chiqadigan kalit: Edition = paket kodi (+ faol qo'shimcha xizmatlar), MaxUsers = obuna, muddat — SaaS: davr
    /// oxiri + 3 kun, Trial: sinov oxiri, OnPrem: davr oxiri (≤ 1 yil). OnPrem'da qo'shimcha xizmat bo'lsa kalit uning davri oxiridan
    /// oshmaydi (offline kalitni qaytarib olib bo'lmaydi — xizmat to'lanmasa keyingi kalit "+KOD"siz chiqadi).
    /// </summary>
    public LicenseRequest? DesiredFromSubscription(Tenant tenant, Subscription? sub) => DesiredFromSubscription(tenant, sub, []);

    public LicenseRequest? DesiredFromSubscription(Tenant tenant, Subscription? sub, IReadOnlyList<Subscription> addOns)
    {
        if (sub?.Plan is null || sub.Status != SubscriptionStatus.Active)
            return null;
        DateOnly today = clock.Today;
        if (sub.EndDate < today)
            return null;
        DateOnly start = sub.StartDate < today ? sub.StartDate : today;
        DateOnly expiry = sub.Period == BillingPeriod.Trial || tenant.Deployment == DeploymentKind.OnPrem
            ? sub.EndDate
            : sub.EndDate.AddDays(options.Value.SaasKeyExtraDays);
        if (tenant.Deployment == DeploymentKind.OnPrem)
        {
            // Offline kalitni qaytarib olib bo'lmaydi — bugundan ko'pi bilan 1 yil; qolgan davr keyingi kalitda.
            start = today;
            DateOnly cap = today.AddYears(1).AddDays(-1);
            // Onlayn tekshiruv majburiy: kalit faqat grace kunga — ERP pull qilmasa (internet yo'q yoki uzib qo'yilgan) tugaydi.
            if (IsRollingKey(tenant))
                cap = today.AddDays(OfflineGraceDays(tenant, options.Value) - 1);
            if (expiry > cap)
                expiry = cap;
        }
        var covering = addOns.Where(a => AddOnCovers(a, today)).ToList();
        if (tenant.Deployment == DeploymentKind.OnPrem && covering.Count > 0)
        {
            DateOnly addOnEnd = covering.Min(a => a.EndDate);
            if (expiry > addOnEnd)
                expiry = addOnEnd;
        }
        string edition = ComposeEdition(sub.Plan.Code, covering.Select(a => a.Plan!.Code), sub.Modules);
        return new LicenseRequest(edition, sub.Users, start, expiry, sub.Id, covering.Count == 0 ? "Obuna bo'yicha" : "Obuna bo'yicha (+ qo'shimcha xizmat)");
    }

    /// <summary>
    /// Joriy kalit obunaga mos bo'lmasa — yangisini avtomatik chiqaradi. Mos bo'lsa yoki chiqarib bo'lmasa (bog'lanmagan,
    /// faol obuna yo'q) — null. Imzolash xatosi tashqariga chiqadi.
    /// </summary>
    public async Task<License?> EnsureForTenantAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (tenant.InstallationId is null || tenant.Status == TenantStatus.Archived)
            return null;
        DateOnly today = clock.Today;
        // Faqat boshlangan davr: kelajakdan boshlanadigan (to'langan) obuna kalitni oldindan o'zgartirmaydi.
        var active = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.TenantId == tenant.Id && s.Status == SubscriptionStatus.Active && s.StartDate <= today)
            .ToListAsync(ct);
        var sub = active.Where(s => !SubscriptionService.IsAddOn(s)).OrderByDescending(s => s.EndDate).FirstOrDefault();
        var desired = DesiredFromSubscription(tenant, sub, active.Where(SubscriptionService.IsAddOn).ToList());
        if (desired is null)
            return null;
        var current = await GetCurrentAsync(tenant, ct);
        if (current is not null && current.Edition == desired.Edition && current.MaxUsers == desired.MaxUsers
            && current.CompanyId == tenant.CompanyId)
        {
            if (!IsRollingKey(tenant) && current.ExpiryDate >= desired.ExpiryDate)
                return null;
            // Aylanma kalit: grace'ning yarmi o'tguncha yangilanmaydi (har kuni yangi kalit chiqmasin); ruxsatdan uzun kalit
            // (masalan Optional → Required o'tilganda) darhol qisqasiga almashtiriladi — ERP keyingi pull'da oladi.
            int renewWindow = OfflineGraceDays(tenant, options.Value) / 2;
            if (IsRollingKey(tenant) && current.ExpiryDate <= desired.ExpiryDate && current.ExpiryDate >= desired.ExpiryDate.AddDays(-renewWindow))
                return null;
        }
        return await IssueAsync(tenant, desired, LicenseSource.Auto, staffId: null, ct);
    }

    /// <summary>
    /// Kalitni bekor qilish. <paramref name="replacementExpiry"/> berilsa — o'rniga shu sanagacha qisqa kalit chiqariladi
    /// (SaaS'da keyingi pull'da yetib boradi, 00_PLAN.md 3.4). Berilmasa — pull 410 revoked qaytaradi.
    /// </summary>
    public async Task<License?> RevokeAsync(License license, string reason, int staffId, DateOnly? replacementExpiry, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Bekor qilish sababi majburiy.");
        if (license.RevokedAtUtc is not null)
            throw new DomainException("Kalit allaqachon bekor qilingan.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == license.TenantId, ct);

        license.RevokedAtUtc = clock.UtcNow;
        license.RevokedByStaffId = staffId;
        license.RevokeReason = reason.Trim();
        audit.Add("license.revoke", "License", license.Id, before: new { license.ExpiryDate },
            after: new { license.RevokeReason, replacementExpiry }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);

        if (replacementExpiry is null || license.InstallationId != tenant.InstallationId)
            return null;
        DateOnly start = license.StartDate <= replacementExpiry.Value ? license.StartDate : replacementExpiry.Value;
        var replacement = await IssueAsync(tenant,
            new LicenseRequest(license.Edition, license.MaxUsers, start, replacementExpiry.Value, license.SubscriptionId, "Bekor qilish: " + reason.Trim()),
            LicenseSource.Manual, staffId, ct);
        license.SupersededById = replacement.Id;
        await db.SaveChangesAsync(ct);
        return replacement;
    }
}
