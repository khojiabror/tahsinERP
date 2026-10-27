using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Licensing;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Demo firmalar — sotuv ko'rsatuvi uchun, to'lovsiz (masalan "demodist" — distributsiya demosi, "demoprod" — ishlab chiqarish).
/// Har biri o'z modullari va ma'lumoti bilan. Kalit oddiy sinov kaliti ("TRIAL:modullar") — ERP uchun boshqa narsa o'zgarmaydi;
/// farqi panelda: sinov obunasi <see cref="Years"/> yilga va tugashiga <see cref="RenewDays"/> kun qolganda o'zi uzayadi, foydalanuvchilar
/// <see cref="Users"/> tagacha, hisob-faktura, muddat eslatmalari, sotuv voronkasi va faollik statistikasiga kirmaydi (Tenant.IsDemo).
/// </summary>
public sealed class DemoService(ControlDbContext db, TenantService tenants, SubscriptionService subscriptions, ProvisioningService provisioning,
    LicenseIssuer issuer, AuditLogger audit, Clock clock)
{
    public const int Users = 10;
    public const int Years = 1;
    public const int RenewDays = 30;

    /// <summary>Yangi demo firma: tenant + demo obunasi + Create job (baza, rollar, tanlangan ma'lumot). Parol natijada bir marta.</summary>
    public async Task<ProvisioningService.EnqueueResult> CreateAsync(Tenant t, CancellationToken ct = default)
    {
        t.IsDemo = true;
        t.Deployment = DeploymentKind.SaaS;
        if (t.ServerId is null)
            t.ServerId = (await provisioning.PickServerAsync(ct))?.Id ?? throw new DomainException(L.T("Pnl_Ten_NoFreeServer"));
        await tenants.CreateAsync(t, startTrial: true, ct);
        await ConvertSubscriptionAsync(t, ct);
        return await provisioning.EnqueueAsync(t, ProvisioningJobType.Create, ct: ct);
    }

    /// <summary>
    /// Mavjud firmani demo qilish (masalan arxivdan tiklangan "demodist"): sinov obunasi demo muddatiga, firma to'xtatilgan bo'lsa qayta
    /// yoqiladi, kalit yangilanadi. Pullik obunasi bor firma demo qilinmaydi — mijoz to'lovi bilan aralashmasin.
    /// </summary>
    public async Task<ProvisioningJob?> MakeDemoAsync(Tenant t, CancellationToken ct = default)
    {
        if (t.Deployment != DeploymentKind.SaaS) throw new DomainException(L.T("Pnl_Demo_OnlySaas"));
        if (t.Status == TenantStatus.Archived || t.DeletedAtUtc != null) throw new DomainException(L.T("Pnl_Prov_Archived"));
        if (t.IsDemo) throw new DomainException(L.T("Pnl_Demo_AlreadyDemo"));
        if (await db.Subscriptions.AnyAsync(s => s.TenantId == t.Id && s.Period != BillingPeriod.Trial
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending) && !s.Plan!.IsAddOn, ct))
            throw new DomainException(L.T("Pnl_Demo_HasPaid"));
        audit.Add("tenant.demo", "Tenant", t.Id, new { IsDemo = false }, new { IsDemo = true });
        t.IsDemo = true;
        await db.SaveChangesAsync(ct);
        await ConvertSubscriptionAsync(t, ct);
        ProvisioningJob? job = null;
        if (t.Status == TenantStatus.Suspended)
            job = await provisioning.ResumeAsync(t, L.T("Pnl_Demo_Reason"), ct);
        await issuer.EnsureForTenantAsync(t, ct);
        return job;
    }

    /// <summary>Faol sinov obunasi demo muddatiga (bugundan <see cref="Years"/> yil) va <see cref="Users"/> foydalanuvchiga; yo'q bo'lsa — yangisi.</summary>
    private async Task ConvertSubscriptionAsync(Tenant t, CancellationToken ct)
    {
        DateOnly today = clock.Today;
        var sub = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.TenantId == t.Id && s.Period == BillingPeriod.Trial && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndDate).FirstOrDefaultAsync(ct);
        if (sub is null || sub.EndDate < today)
        {
            if (sub is not null) sub.Status = SubscriptionStatus.Expired;
            sub = await subscriptions.CreateTrialAsync(t, ct);   // modullar — firmadagi (Tenant.Modules; bo'sh — hammasi)
        }
        var before = new { sub.EndDate, sub.Users };
        sub.EndDate = today.AddYears(Years).AddDays(-1);
        sub.Users = Math.Max(sub.Users, Users);
        audit.Add("subscription.demo", "Subscription", sub.Id, before, new { sub.EndDate, sub.Users });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kunlik tekshiruv (SubscriptionService.RunLifecycleAsync): demo firmaning faol sinov obunasi tugashiga <see cref="RenewDays"/> kun
    /// qolganda yana <see cref="Years"/> yilga uzayadi — demo hech qachon to'xtab qolmaydi. Qaytaradi: uzaytirilgan obunalar soni.
    /// Obunalar <paramref name="t"/>.Subscriptions da yuklangan bo'lishi kerak; saqlash — chaqiruvchida.
    /// </summary>
    public static int ExtendIfEnding(Tenant t, DateOnly today, AuditLogger audit)
    {
        int n = 0;
        foreach (var s in t.Subscriptions.Where(s => s.Period == BillingPeriod.Trial && s.Status == SubscriptionStatus.Active
                     && s.EndDate < today.AddDays(RenewDays)))
        {
            var before = s.EndDate;
            s.EndDate = (s.EndDate < today ? today : s.EndDate).AddYears(Years);
            audit.Add("subscription.demo-renew", "Subscription", s.Id, new { EndDate = before }, new { s.EndDate }, staffIdOverride: null);
            n++;
        }
        return n;
    }
}
