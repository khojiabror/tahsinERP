using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;

namespace tahsinERP.ControlPanel.Services;

public record DebtorRow(int TenantId, string Slug, string LegalName, int InvoiceCount, long DebtTiyin, DateOnly OldestDue);
public record ExpiringRow(int TenantId, string Slug, string LegalName, string Plan, DateOnly EndDate, int DaysLeft, TenantStatus Status);
public record MonthRevenue(int Year, int Month, long AmountTiyin, int Count);
/// <summary>Panelga ulangan, lekin uzoq vaqt pull qilmagan on-prem o'rnatma (onlayn tekshiruv siyosati, 08_STAGE4.md 4).</summary>
public record OfflineRow(int TenantId, string Slug, string LegalName, DateTime? LastSeenUtc, int DaysSilent, bool Required, int GraceDays);

public record DashboardData(
    Dictionary<TenantStatus, int> TenantsByStatus,
    int SaasCount, int OnPremCount,
    IReadOnlyList<DebtorRow> Debtors,
    IReadOnlyList<ExpiringRow> Expiring,
    IReadOnlyList<MonthRevenue> Revenue,
    long UnpaidTotalTiyin)
{
    public IReadOnlyList<OfflineRow> OfflineOnPrem { get; init; } = [];
}

public sealed class DashboardService(ControlDbContext db, Clock clock, IOptions<PanelOptions> options)
{
    public async Task<DashboardData> GetAsync(int expiringDays = 14, int months = 6, CancellationToken ct = default)
    {
        DateOnly today = clock.Today;

        var byStatus = await db.Tenants.GroupBy(t => t.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        int saas = await db.Tenants.CountAsync(t => t.Deployment == DeploymentKind.SaaS && t.Status != TenantStatus.Archived, ct);
        int onprem = await db.Tenants.CountAsync(t => t.Deployment == DeploymentKind.OnPrem && t.Status != TenantStatus.Archived, ct);

        // Qarzdorlar: to'lov muddati o'tgan, to'liq to'lanmagan hisob-fakturalar.
        var overdue = await db.Invoices
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today)
            .Select(i => new { i.TenantId, i.Tenant!.Slug, i.Tenant.LegalName, Debt = i.TotalTiyin - i.PaidTiyin, i.DueDate })
            .ToListAsync(ct);
        var debtors = overdue.GroupBy(x => new { x.TenantId, x.Slug, x.LegalName })
            .Select(g => new DebtorRow(g.Key.TenantId, g.Key.Slug, g.Key.LegalName, g.Count(), g.Sum(x => x.Debt), g.Min(x => x.DueDate)))
            .OrderByDescending(d => d.DebtTiyin).ToList();

        long unpaid = await db.Invoices.Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .SumAsync(i => (long?)(i.TotalTiyin - i.PaidTiyin), ct) ?? 0;

        // Muddati yaqinlashganlar: tenantning eng uzoq faol paket obunasi bo'yicha (qo'shimcha xizmatlar paket muddatini yashirmasin).
        DateOnly horizon = today.AddDays(expiringDays);
        var subs = await SubscriptionService.PackageOnly(db.Subscriptions).Where(s => s.Status == SubscriptionStatus.Active && s.Tenant!.Status != TenantStatus.Archived)
            .Select(s => new { s.TenantId, s.Tenant!.Slug, s.Tenant.LegalName, Plan = s.Plan!.Name, s.EndDate, s.Tenant.Status })
            .ToListAsync(ct);
        var expiring = subs.GroupBy(s => s.TenantId).Select(g => g.OrderByDescending(s => s.EndDate).First())
            .Where(s => s.EndDate <= horizon)
            .Select(s => new ExpiringRow(s.TenantId, s.Slug, s.LegalName, s.Plan, s.EndDate, s.EndDate.DayNumber - today.DayNumber, s.Status))
            .OrderBy(s => s.EndDate).ToList();

        // Oylik tushum: storno qilinmagan to'lovlar, to'lov sanasi bo'yicha.
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var pays = await db.Payments.Where(p => !p.IsReversed && p.PaidAt >= from)
            .Select(p => new { p.PaidAt, p.AmountTiyin }).ToListAsync(ct);
        var revenue = Enumerable.Range(0, months).Select(i => from.AddMonths(i)).Select(m =>
        {
            var inMonth = pays.Where(p => p.PaidAt.Year == m.Year && p.PaidAt.Month == m.Month).ToList();
            return new MonthRevenue(m.Year, m.Month, inMonth.Sum(p => p.AmountTiyin), inMonth.Count);
        }).ToList();

        return new DashboardData(byStatus, saas, onprem, debtors, expiring, revenue, unpaid) { OfflineOnPrem = await OfflineOnPremAsync(ct) };
    }

    /// <summary>
    /// Ro'yxatdan o'tgan on-prem o'rnatmalar, oxirgi pull'dan beri: Required — 3+ kun (kalit tez orada tugaydi), Optional — grace
    /// (standart 30) kundan ko'p. Panelga umuman ulanmaganlar (offline kalit) bu ro'yxatga kirmaydi.
    /// </summary>
    public async Task<IReadOnlyList<OfflineRow>> OfflineOnPremAsync(CancellationToken ct = default)
    {
        DateTime now = clock.UtcNow;
        var rows = await db.Tenants.AsNoTracking()
            .Where(t => t.Deployment == DeploymentKind.OnPrem && t.RegisteredAtUtc != null && t.Status != TenantStatus.Archived && t.DeletedAtUtc == null)
            .ToListAsync(ct);
        return rows.Select(t =>
            {
                int grace = LicenseIssuer.OfflineGraceDays(t, options.Value);
                bool required = LicenseIssuer.IsRollingKey(t);
                int silent = (int)Math.Floor((now - (t.LastSeenUtc ?? t.RegisteredAtUtc!.Value)).TotalDays);
                return new OfflineRow(t.Id, t.Slug, t.LegalName, t.LastSeenUtc, silent, required, grace);
            })
            .Where(r => r.Required ? r.DaysSilent >= 3 : r.DaysSilent >= r.GraceDays)
            .OrderByDescending(r => r.Required).ThenByDescending(r => r.DaysSilent).ToList();
    }
}
