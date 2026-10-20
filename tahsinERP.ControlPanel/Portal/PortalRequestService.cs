using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Portal;

/// <summary>Mijoz ko'radigan obuna holati (portal bosh sahifasi va so'rov formasi uchun). Current/Pending/Upcoming — faqat paket obunalari.</summary>
public sealed record TenantOverview(Tenant Tenant, Subscription? Current, Subscription? Pending, Subscription? Upcoming, int? DaysLeft,
    IReadOnlyList<AddOnStatus> AddOns);

public enum AddOnState
{
    /// <summary>Obuna yo'q (yoki bekor qilingan) — xizmat ulanmagan.</summary>
    NotConnected = 1,
    /// <summary>Obuna ochilgan, to'lov kutilmoqda.</summary>
    PendingPayment = 2,
    /// <summary>To'langan, davr hali boshlanmagan.</summary>
    Upcoming = 3,
    /// <summary>Faol — kalitda "+KOD" bor.</summary>
    Active = 4,
    /// <summary>Davr tugagan (imtiyozli kunlar ham bo'lishi mumkin) — xizmat kalitdan olib tashlanadi.</summary>
    Expired = 5,
}

/// <summary>Qo'shimcha xizmat (masalan Sayt-katalog) holati — portalda paketdan alohida ko'rsatiladi.</summary>
public sealed record AddOnStatus(Plan Plan, AddOnState State, Subscription? Subscription, int? DaysLeft)
{
    public string StateText => State switch
    {
        AddOnState.Active => "Faol",
        AddOnState.PendingPayment => "To'lov kutilmoqda",
        AddOnState.Upcoming => "To'langan, hali boshlanmagan",
        AddOnState.Expired => "Muddati tugagan",
        _ => "Ulanmagan",
    };

    public string Badge => State switch
    {
        AddOnState.Active => "ok",
        AddOnState.PendingPayment or AddOnState.Upcoming => "warn",
        AddOnState.Expired => "bad",
        _ => "muted",
    };
}

/// <summary>
/// Tarifni oshirish / uzaytirish so'rovlari. Qoidalar:
/// - bir tenant'da bir vaqtda bitta ochiq (Pending) so'rov;
/// - Renew: joriy pullik obuna (o'sha paket va davr) — joriy obunaga hisob-faktura;
/// - Upgrade (yuqoriroq paket) / New (pullik obuna yo'q): yangi obuna bugundan, to'langach eskisi yopiladi;
/// - ChangeAtPeriodEnd (pastroq paket yoki davr o'zgarishi): yangi obuna joriy davr oxiridan (pasaytirish — faqat davr oxirida);
/// - tasdiqlash → qoralama hisob-faktura avtomatik (xodim tekshirib «Chiqarish» bosadi), rad etish — sabab bilan; mijozga Telegram.
/// </summary>
public sealed class PortalRequestService(ControlDbContext db, AuditLogger audit, Clock clock, SubscriptionService subscriptions,
    BillingService billing, PortalNotifier notifier)
{
    public static int MaxPeriods(BillingPeriod p) => p == BillingPeriod.Year ? 3 : 12;

    public async Task<TenantOverview> OverviewAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        var all = await db.Subscriptions.AsNoTracking().Include(s => s.Plan).Where(s => s.TenantId == tenantId).ToListAsync(ct);
        DateOnly today = clock.Today;
        var subs = all.Where(s => !SubscriptionService.IsAddOn(s)).ToList();
        var active = subs.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        // Joriy: hozir amal qilayotgan (boshlangan) faol obunalardan eng uzog'i; Upcoming — kelajakdan boshlanadigan to'langan.
        var current = active.Where(s => s.StartDate <= today).OrderByDescending(s => s.EndDate).FirstOrDefault();
        var upcoming = active.Where(s => s.StartDate > today).OrderBy(s => s.StartDate).FirstOrDefault();
        var pending = subs.Where(s => s.Status == SubscriptionStatus.Pending).OrderByDescending(s => s.Id).FirstOrDefault();
        int? days = current is null ? null : current.EndDate.DayNumber - today.DayNumber;
        var addOnPlans = await db.Plans.AsNoTracking().Where(p => p.IsAddOn).OrderBy(p => p.SortOrder).ToListAsync(ct);
        var addOns = addOnPlans
            .Select(p => AddOnStatusOf(p, all.Where(s => s.PlanId == p.Id).ToList(), today))
            .Where(a => a.Plan.IsActive || a.State != AddOnState.NotConnected) // o'chirilgan xizmat — faqat obunasi bo'lsa
            .ToList();
        return new TenantOverview(tenant, current, pending, upcoming, days, addOns);
    }

    /// <summary>Bitta xizmatning holati: faol → kelajakdagi to'langan → to'lov kutilmoqda → tugagan → ulanmagan.</summary>
    public static AddOnStatus AddOnStatusOf(Plan plan, IReadOnlyList<Subscription> subs, DateOnly today)
    {
        var active = subs.Where(s => s.Status == SubscriptionStatus.Active).OrderByDescending(s => s.EndDate).ToList();
        if (active.FirstOrDefault(s => s.StartDate <= today && s.EndDate >= today) is { } cur)
            return new AddOnStatus(plan, AddOnState.Active, cur, cur.EndDate.DayNumber - today.DayNumber);
        if (active.FirstOrDefault(s => s.StartDate > today) is { } up)
            return new AddOnStatus(plan, AddOnState.Upcoming, up, null);
        if (subs.Where(s => s.Status == SubscriptionStatus.Pending).OrderByDescending(s => s.Id).FirstOrDefault() is { } pend)
            return new AddOnStatus(plan, AddOnState.PendingPayment, pend, null);
        if (subs.Where(s => s.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired).OrderByDescending(s => s.EndDate).FirstOrDefault() is { } old)
            return new AddOnStatus(plan, AddOnState.Expired, old, null);
        return new AddOnStatus(plan, AddOnState.NotConnected, null, null);
    }

    /// <summary>So'rov turini aniqlaydi va yangi davr boshlanish sanasini hisoblaydi.</summary>
    public static (PortalRequestKind Kind, DateOnly Effective) Classify(Subscription? current, Plan plan, BillingPeriod period, DateOnly today)
    {
        if (current is null || current.Period == BillingPeriod.Trial || current.Plan is null || current.EndDate < today.AddDays(-30))
            return (PortalRequestKind.New, today);
        if (current.PlanId == plan.Id && current.Period == period)
            return (PortalRequestKind.Renew, current.EndDate >= today ? current.EndDate.AddDays(1) : today);
        if (plan.SortOrder > current.Plan.SortOrder)
            return (PortalRequestKind.Upgrade, today);
        return (PortalRequestKind.ChangeAtPeriodEnd, current.EndDate >= today ? current.EndDate.AddDays(1) : today);
    }

    public async Task<PortalRequest> CreateAsync(int tenantId, int portalUserId, int planId, BillingPeriod period, int periodCount, string? comment,
        CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId, ct);
        if (PortalAuthService.TenantClosed(tenant)) throw new DomainException("Tashkilot arxivda.");
        // Qo'shimcha xizmat portal so'rovi orqali emas — xodim alohida obuna ochadi (portalda holati ko'rinadi).
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive && p.Code != PlanCodes.Trial && !p.IsAddOn, ct)
            ?? throw new DomainException("Paket topilmadi.");
        if (period is not (BillingPeriod.Month or BillingPeriod.Year)) throw new DomainException("Davr noto'g'ri.");
        if (periodCount < 1 || periodCount > MaxPeriods(period)) throw new DomainException($"Davrlar soni 1–{MaxPeriods(period)}.");
        if (await db.PortalRequests.AnyAsync(r => r.TenantId == tenantId && r.Status == PortalRequestStatus.Pending, ct))
            throw new DomainException("Sizda ko'rib chiqilayotgan so'rov bor. Avval uni bekor qiling yoki javobni kuting.");
        var ov = await OverviewAsync(tenantId, ct);
        if (ov.Upcoming is not null)
            throw new DomainException($"Keyingi davr ({ov.Upcoming.StartDate:dd.MM.yyyy} dan) allaqachon to'langan. Yangi so'rovni o'sha davr boshlangach yuboring.");
        var (kind, effective) = Classify(ov.Current, plan, period, clock.Today);
        var req = new PortalRequest
        {
            TenantId = tenantId, PortalUserId = portalUserId, Kind = kind, PlanId = plan.Id, Period = period, PeriodCount = periodCount,
            EffectiveDate = effective, CurrentSubscriptionId = ov.Current?.Id ?? ov.Pending?.Id,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()[..Math.Min(comment.Trim().Length, 1000)],
            Status = PortalRequestStatus.Pending, CreatedAtUtc = clock.UtcNow,
        };
        db.PortalRequests.Add(req);
        await db.SaveChangesAsync(ct);
        audit.Add("portal.request.create", "PortalRequest", req.Id,
            after: new { req.TenantId, req.Kind, Plan = plan.Code, req.Period, req.PeriodCount, req.EffectiveDate, req.Comment }, portalUserId: portalUserId);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync(tenantId, "portal-request", $"portal-req|{req.Id}|new",
            $"tahsinERP portal: {tenant.LegalName} ({tenant.Slug}) — yangi so'rov #{req.Id}: {KindText(kind)}, «{plan.Name}», {periodCount} {PeriodUnit(period)}. Panel → So'rovlar.", ct);
        return req;
    }

    public async Task CancelAsync(int requestId, int tenantId, int portalUserId, CancellationToken ct = default)
    {
        var req = await db.PortalRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            ?? throw new DomainException("So'rov topilmadi.");
        if (req.Status != PortalRequestStatus.Pending) throw new DomainException("So'rov allaqachon ko'rib chiqilgan.");
        req.Status = PortalRequestStatus.Cancelled;
        req.DecidedAtUtc = clock.UtcNow;
        audit.Add("portal.request.cancel", "PortalRequest", req.Id, new { Status = PortalRequestStatus.Pending }, new { req.Status }, portalUserId: portalUserId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tasdiqlash: kerak bo'lsa yangi (Pending) obuna + qoralama hisob-faktura (paket narxi × davrlar). Hammasi bitta
    /// tranzaksiyada; narx belgilanmagan bo'lsa yoki to'lanmagan obuna bo'lsa — xato, hech narsa saqlanmaydi.
    /// </summary>
    public async Task<Invoice> ApproveAsync(int requestId, int staffId, CancellationToken ct = default)
    {
        Invoice invoice;
        PortalRequest req;
        Plan plan;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            req = await db.PortalRequests.Include(r => r.Tenant).Include(r => r.Plan).FirstOrDefaultAsync(r => r.Id == requestId, ct)
                ?? throw new DomainException("So'rov topilmadi.");
            if (req.Status != PortalRequestStatus.Pending) throw new DomainException("So'rov allaqachon ko'rib chiqilgan.");
            plan = req.Plan!;
            var tenant = req.Tenant!;
            // Holat so'rovdan keyin o'zgargan bo'lishi mumkin (to'lov, xodim amali) — tur qayta hisoblanadi.
            var ov = await OverviewAsync(tenant.Id, ct);
            var (kind, effective) = Classify(ov.Current, plan, req.Period, clock.Today);

            Subscription target;
            if (kind == PortalRequestKind.Renew)
            {
                target = await db.Subscriptions.Include(s => s.Plan).FirstAsync(s => s.Id == ov.Current!.Id, ct);
            }
            else if (ov.Pending is { } p && p.PlanId == plan.Id && p.Period == req.Period)
            {
                // Mos to'lanmagan obuna allaqachon bor (masalan xodim oldinroq ochgan) — o'shanga hisob-faktura.
                target = await db.Subscriptions.Include(s => s.Plan).FirstAsync(s => s.Id == p.Id, ct);
            }
            else
            {
                if (ov.Pending is not null)
                    throw new DomainException($"Tenantda to'lanmagan obuna #{ov.Pending.Id} bor — avval uni bekor qiling (Obunalar).");
                target = await subscriptions.CreateAsync(tenant, plan.Id, req.Period, null, effective, autoRenew: false, ct);
            }
            var line = BillingService.DefaultLine(target, target.Plan!, req.PeriodCount);
            invoice = await billing.CreateDraftAsync(tenant.Id, target.Id, req.PeriodCount, [line],
                $"Portal so'rovi #{req.Id} ({KindText(kind)})", staffId, ct);

            var before = new { req.Status, req.Kind };
            req.Kind = kind;
            req.EffectiveDate = effective;
            req.Status = PortalRequestStatus.Approved;
            req.DecidedByStaffId = staffId;
            req.DecidedAtUtc = clock.UtcNow;
            req.InvoiceId = invoice.Id;
            req.SubscriptionId = target.Id;
            audit.Add("portal.request.approve", "PortalRequest", req.Id, before,
                new { req.Status, req.Kind, req.InvoiceId, req.SubscriptionId, req.EffectiveDate }, staffIdOverride: staffId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await notifier.NotifyTenantAsync(req.TenantId, "portal-request-approved", $"portal-req|{req.Id}|approved",
            $"tahsinERP: so'rovingiz #{req.Id} tasdiqlandi ({KindText(req.Kind)}, «{plan.Name}», {req.PeriodCount} {PeriodUnit(req.Period)}). " +
            "Hisob-faktura tayyorlanmoqda — chiqarilgach portalda (Hisob-fakturalar) ko'rinadi.", ct);
        return invoice;
    }

    public async Task RejectAsync(int requestId, string reason, int staffId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Rad etish sababi majburiy (mijozga yuboriladi).");
        var req = await db.PortalRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct) ?? throw new DomainException("So'rov topilmadi.");
        if (req.Status != PortalRequestStatus.Pending) throw new DomainException("So'rov allaqachon ko'rib chiqilgan.");
        req.Status = PortalRequestStatus.Rejected;
        req.DecisionReason = reason.Trim()[..Math.Min(reason.Trim().Length, 1000)];
        req.DecidedByStaffId = staffId;
        req.DecidedAtUtc = clock.UtcNow;
        audit.Add("portal.request.reject", "PortalRequest", req.Id, new { Status = PortalRequestStatus.Pending }, new { req.Status, Reason = req.DecisionReason },
            staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyTenantAsync(req.TenantId, "portal-request-rejected", $"portal-req|{req.Id}|rejected",
            $"tahsinERP: so'rovingiz #{req.Id} rad etildi. Sabab: {req.DecisionReason}", ct);
    }

    public static string KindText(PortalRequestKind k) => k switch
    {
        PortalRequestKind.Renew => "uzaytirish",
        PortalRequestKind.Upgrade => "tarifni oshirish",
        PortalRequestKind.ChangeAtPeriodEnd => "davr oxirida o'zgartirish",
        _ => "yangi obuna",
    };

    public static string PeriodUnit(BillingPeriod p) => p == BillingPeriod.Year ? "yil" : "oy";
}
