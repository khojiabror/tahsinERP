using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Obunalar va hayot sikli (00_PLAN.md, 6-bo'lim).</summary>
public sealed class SubscriptionService(ControlDbContext db, AuditLogger audit, Clock clock, IOptions<PanelOptions> options,
    ILogger<SubscriptionService> log)
{
    private static object Snapshot(Subscription s) => new { s.TenantId, s.PlanId, s.Users, s.Period, s.StartDate, s.EndDate, s.AutoRenew, s.Status };

    public async Task<Subscription> CreateTrialAsync(Tenant t, CancellationToken ct = default)
    {
        var plan = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Trial, ct);
        DateOnly today = clock.Today;
        var sub = new Subscription
        {
            TenantId = t.Id, PlanId = plan.Id, Plan = plan, Users = plan.MaxUsers, Period = BillingPeriod.Trial,
            StartDate = today, EndDate = today.AddDays((plan.TrialDays ?? 14) - 1), Status = SubscriptionStatus.Active,
            CreatedAtUtc = clock.UtcNow,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        audit.Add("subscription.create", "Subscription", sub.Id, after: Snapshot(sub));
        return sub;
    }

    /// <summary>Pullik obuna (Pending — birinchi to'lovgacha). Qat'iy paket: Users = Plan.MaxUsers (kamroq ham bo'lishi mumkin, ko'p emas).</summary>
    public async Task<Subscription> CreateAsync(Tenant t, int planId, BillingPeriod period, int? users, DateOnly startDate, bool autoRenew, CancellationToken ct = default)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct) ?? throw new DomainException("Tarif topilmadi.");
        if (plan.Code == PlanCodes.Trial || period == BillingPeriod.Trial)
            throw new DomainException("Sinov obunasi tenant yaratilganda avtomatik ochiladi.");
        int u = users ?? plan.MaxUsers;
        if (u <= 0 || u > plan.MaxUsers)
            throw new DomainException($"{plan.Name} paketida ko'pi bilan {plan.MaxUsers} foydalanuvchi. Ko'prog'i — keyingi paket.");
        if (await db.Subscriptions.AnyAsync(s => s.TenantId == t.Id && s.Status == SubscriptionStatus.Pending, ct))
            throw new DomainException("Bu tenantda to'lanmagan (Pending) obuna allaqachon bor.");
        var sub = new Subscription
        {
            TenantId = t.Id, PlanId = plan.Id, Plan = plan, Users = u, Period = period, StartDate = startDate,
            EndDate = startDate.AddDays(-1), AutoRenew = autoRenew, Status = SubscriptionStatus.Pending, CreatedAtUtc = clock.UtcNow,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        audit.Add("subscription.create", "Subscription", sub.Id, after: Snapshot(sub));
        await db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task UpdateAsync(Subscription sub, int planId, int users, bool autoRenew, CancellationToken ct = default)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw new DomainException("Tarif topilmadi.");
        if (users <= 0 || users > plan.MaxUsers)
            throw new DomainException($"{plan.Name} paketida ko'pi bilan {plan.MaxUsers} foydalanuvchi.");
        var before = Snapshot(sub);
        sub.PlanId = plan.Id; sub.Plan = plan; sub.Users = users; sub.AutoRenew = autoRenew;
        audit.Add("subscription.update", "Subscription", sub.Id, before, Snapshot(sub));
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Subscription sub, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Sabab majburiy.");
        var before = Snapshot(sub);
        sub.Status = SubscriptionStatus.Cancelled;
        audit.Add("subscription.cancel", "Subscription", sub.Id, before, new { sub.Status, Reason = reason });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// To'liq to'langan hisob-faktura bo'yicha uzaytirish: yangi davr = max(joriy oxir + 1, bugun) dan PeriodCount oy/yil.
    /// Shu tenantning boshqa faol obunasi (masalan TRIAL) yopiladi. Tenant → Active. SaveChanges chaqiruvchida.
    /// </summary>
    public async Task ApplyPaidInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        if (invoice.SubscriptionId is null || invoice.SubscriptionAppliedUtc is not null)
            return;
        var sub = await db.Subscriptions.Include(s => s.Plan).Include(s => s.Tenant)
            .FirstAsync(s => s.Id == invoice.SubscriptionId, ct);
        if (sub.Status == SubscriptionStatus.Cancelled)
            throw new DomainException("Obuna bekor qilingan — uzaytirib bo'lmaydi.");
        var before = Snapshot(sub);
        DateOnly today = clock.Today;
        DateOnly from = sub.Status == SubscriptionStatus.Pending
            ? (sub.StartDate > today ? sub.StartDate : today)
            : (sub.EndDate >= today ? sub.EndDate.AddDays(1) : today);
        int n = Math.Max(1, invoice.PeriodCount);
        DateOnly end = sub.Period == BillingPeriod.Year ? from.AddYears(n).AddDays(-1) : from.AddMonths(n).AddDays(-1);
        if (sub.Status == SubscriptionStatus.Pending || sub.EndDate < today)
            sub.StartDate = from;
        sub.EndDate = end;
        sub.Status = SubscriptionStatus.Active;
        invoice.SubscriptionAppliedUtc = clock.UtcNow;
        audit.Add("subscription.extend", "Subscription", sub.Id, before, new { Snapshot = Snapshot(sub), InvoiceId = invoice.Id, invoice.Number });

        // Kelajakdan boshlanadigan davr (masalan portal so'rovi: pasaytirish faqat joriy davr oxirida) — joriy obuna
        // o'z muddatigacha ishlayveradi; yangi davr boshlanganda kunlik ish kalitni yangilaydi.
        var others = sub.StartDate > today ? new List<Subscription>()
            : await db.Subscriptions.Where(s => s.TenantId == sub.TenantId && s.Id != sub.Id && s.Status == SubscriptionStatus.Active).ToListAsync(ct);
        foreach (var o in others)
        {
            o.Status = SubscriptionStatus.Expired;
            audit.Add("subscription.close", "Subscription", o.Id, new { Status = SubscriptionStatus.Active }, new { o.Status, ReplacedBy = sub.Id });
        }

        var tenant = sub.Tenant!;
        if (tenant.Status != TenantStatus.Active && tenant.Status != TenantStatus.Archived)
        {
            audit.Add("tenant.status", "Tenant", tenant.Id, new { tenant.Status }, new { Status = TenantStatus.Active, Reason = "To'lov: " + invoice.Number });
            tenant.Status = TenantStatus.Active;
        }
    }

    /// <summary>
    /// Kunlik hayot sikli: muddati o'tgan obuna → Expired; tenant: Active → Grace (GraceDays) → Suspended, Trial → Suspended.
    /// Archived/Suspended'ga qo'lda tegilmaydi. Qaytaradi: o'zgargan tenantlar soni.
    /// </summary>
    public async Task<int> RunLifecycleAsync(CancellationToken ct = default)
    {
        DateOnly today = clock.Today;
        int grace = options.Value.GraceDays;
        var tenants = await db.Tenants.Include(t => t.Subscriptions)
            .Where(t => t.Status == TenantStatus.Trial || t.Status == TenantStatus.Active || t.Status == TenantStatus.Grace)
            .ToListAsync(ct);
        int changed = 0;
        foreach (var t in tenants)
        {
            foreach (var s in t.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active && s.EndDate.AddDays(grace) < today))
            {
                s.Status = SubscriptionStatus.Expired;
                audit.Add("subscription.expire", "Subscription", s.Id, new { Status = SubscriptionStatus.Active }, new { s.Status, s.EndDate }, staffIdOverride: null);
            }
            var active = t.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).OrderByDescending(s => s.EndDate).FirstOrDefault();
            TenantStatus target;
            if (active is null)
                target = TenantStatus.Suspended;
            else if (active.EndDate >= today)
                target = active.Period == BillingPeriod.Trial ? TenantStatus.Trial : TenantStatus.Active;
            else
                target = active.Period == BillingPeriod.Trial ? TenantStatus.Suspended : TenantStatus.Grace;
            if (target != t.Status)
            {
                audit.Add("tenant.status", "Tenant", t.Id, new { t.Status }, new { Status = target, Reason = "Kunlik tekshiruv (muddat)" });
                log.LogInformation("Tenant {Slug}: {From} → {To}", t.Slug, t.Status, target);
                t.Status = target;
                changed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
