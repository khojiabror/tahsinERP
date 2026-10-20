using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.Services.Editions;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Traction analitikasi (Docs/ControlPanel/07_TRACTION.md — metrika ta'riflari). Faqat agregat jadvallardan
/// (UsageDailyUsers / UsageDailyModules / UsageDailyDocs) va billing jadvallaridan o'qiydi — xom UsageEvents'dan emas.
/// Sanalar Toshkent bo'yicha, davr [From, To] (ikkala kun ham kiradi).
/// Qo'shimcha xizmatlar (Plan.IsAddOn, masalan SITE): tenantning paketi, sinov konversiyasi va churn faqat paket obunasidan;
/// ruxsat etilgan modullar = EditionModules.ModulesOf(kalitdagi Edition, ya'ni "KORP+SITE") — xizmat modullari ham kiradi;
/// MRR xizmat obunalarini ham qo'shadi (alohida ko'rsatkich — AddOnMrrTiyin).
/// </summary>
public sealed class TractionService(ControlDbContext db, Clock clock, ProvisioningService provisioning, IOptions<PanelOptions> panel)
{
    public sealed record Filter(DateOnly From, DateOnly To, string? Plan, TenantStatus? Status)
    {
        public int Days => To.DayNumber - From.DayNumber + 1;
        public DateOnly PrevFrom => From.AddDays(-Days);
        public DateOnly PrevTo => From.AddDays(-1);
    }

    public sealed record Overview(int ActiveTenants, int TotalTenants, double Dau, int Wau, int Mau, double Stickiness,
        int TrialsStarted, int TrialsConverted, double Conversion, long MrrTiyin, long RevenueTiyin,
        int PayingAtStart, int Churned, double ChurnRate, int DocsTotal, long AddOnMrrTiyin = 0, int AddOnTenants = 0);

    public sealed record ModuleRow(string Module, string Name, int Tenants, int Actions, int PrevActions, double? TrendPct, int UnusedInPackage);
    public sealed record PageRow(string Controller, string Module, UsageActionType ActionType, int Actions, int Tenants, bool Mapped);
    public sealed record DocRow(string DocType, int Count, int Tenants);
    public sealed record TenantRow(Tenant Tenant, string? Plan, string? PlanCode, long MrrTiyin, DateTime? LastLoginUtc, int ActiveUsers, int SeatLimit,
        int ModulesUsed, int ModulesAllowed, int Actions, int PrevActions, int Docs, int Score, string Risk, IReadOnlyList<string> UnusedModules,
        string? Edition = null);
    public sealed record CohortRow(string Month, int Size, double?[] Weeks);
    public sealed record FunnelStep(string Name, int Count);
    public sealed record Series(List<string> Labels, Dictionary<string, List<int>> Data);

    public sealed record Report(Filter Filter, Overview Overview, List<ModuleRow> Modules, List<PageRow> Pages, List<DocRow> Docs,
        Dictionary<UsageActionType, int> ActionTypes, List<TenantRow> Tenants, List<CohortRow> Cohorts, List<FunnelStep> Funnel,
        Series ActiveUsersDaily, Series ModulesDaily);

    public static readonly string[] DocNames = ["SALES_ORDER", "PURCHASE_ORDER", "INCOME", "SALES_INVOICE", "PRODUCTION", "WAYBILL", "PAYROLL"];

    public static string DocLabel(string t) => t switch
    {
        "SALES_ORDER" => "Sotuv buyurtmasi", "PURCHASE_ORDER" => "Xarid buyurtmasi", "INCOME" => "Omborga kirim",
        "SALES_INVOICE" => "Sotuv hisob-fakturasi", "PRODUCTION" => "Ishlab chiqarish", "WAYBILL" => "Yo'l varaqasi", "PAYROLL" => "Oylik",
        _ => t
    };

    public static string ModuleLabel(string m) => Enum.TryParse<ProductModule>(m, out var pm) ? EditionModules.DisplayName(pm) : m;

    public async Task<Report> BuildAsync(Filter f, CancellationToken ct = default)
    {
        DateOnly today = clock.Today;
        int grace = panel.Value.GraceDays;

        // ---- tenantlar va obunalar ----
        var tenantsAll = await db.Tenants.AsNoTracking().Where(t => t.Deployment == DeploymentKind.SaaS).ToListAsync(ct);
        var subs = await db.Subscriptions.AsNoTracking().Include(s => s.Plan).ToListAsync(ct);
        var subsByTenant = subs.GroupBy(s => s.TenantId).ToDictionary(g => g.Key, g => g.ToList());
        Subscription? Current(int tid) => subsByTenant.GetValueOrDefault(tid)?
            .Where(s => s.Status == SubscriptionStatus.Active && !SubscriptionService.IsAddOn(s))
            .OrderByDescending(s => s.Period != BillingPeriod.Trial).ThenByDescending(s => s.EndDate).FirstOrDefault();
        List<Subscription> AddOns(int tid) => subsByTenant.GetValueOrDefault(tid)?.Where(s => LicenseIssuer.AddOnCovers(s, today)).ToList() ?? [];
        // Kalitdagi kabi Edition: paket kodi + faol xizmatlar ("KORP+SITE"). Paket yo'q — null (cheklovsiz deb hisoblanadi).
        string? Edition(int tid) => Current(tid)?.Plan?.Code is { } code ? LicenseIssuer.ComposeEdition(code, AddOns(tid).Select(a => a.Plan!.Code)) : null;
        bool Matches(int tid, string plan) => EditionModules.AddOnsOf("X+" + plan).Count > 0
            ? AddOns(tid).Any(a => string.Equals(a.Plan!.Code, plan, StringComparison.OrdinalIgnoreCase))
            : string.Equals(Current(tid)?.Plan?.Code, plan, StringComparison.OrdinalIgnoreCase);

        var tenants = tenantsAll.Where(t => t.DeletedAtUtc == null && !provisioning.IsDemo(t)).ToList();
        if (f.Status is not null) tenants = tenants.Where(t => t.Status == f.Status).ToList();
        if (!string.IsNullOrWhiteSpace(f.Plan)) tenants = tenants.Where(t => Matches(t.Id, f.Plan.Trim())).ToList();
        var ids = tenants.Select(t => t.Id).ToHashSet();

        // ---- MRR: faol pullik obuna × oxirgi to'langan hisob-faktura (davrga bo'lingan); bo'lmasa tarif narxi ----
        var paidInvoices = await db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Paid && i.SubscriptionId != null)
            .Select(i => new { i.SubscriptionId, i.TotalTiyin, i.PeriodCount, i.PaidAtUtc, i.Id }).ToListAsync(ct);
        var lastInvoice = paidInvoices.GroupBy(i => i.SubscriptionId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PaidAtUtc).ThenByDescending(x => x.Id).First());
        long Mrr(Subscription s)
        {
            if (s.Period == BillingPeriod.Trial || s.Status != SubscriptionStatus.Active) return 0;
            int months = s.Period == BillingPeriod.Year ? 12 : 1;
            if (lastInvoice.TryGetValue(s.Id, out var inv) && inv.PeriodCount > 0) return inv.TotalTiyin / (inv.PeriodCount * months);
            return s.Period == BillingPeriod.Year ? (s.Plan?.PriceYearTiyin ?? 0) / 12 : s.Plan?.PriceMonthTiyin ?? 0;
        }
        long TenantMrr(int tid) => subsByTenant.GetValueOrDefault(tid)?.Where(s => s.Status == SubscriptionStatus.Active && s.Period != BillingPeriod.Trial).Sum(Mrr) ?? 0;
        long TenantAddOnMrr(int tid) => subsByTenant.GetValueOrDefault(tid)?.Where(s => s.Status == SubscriptionStatus.Active && SubscriptionService.IsAddOn(s)).Sum(Mrr) ?? 0;

        // ---- agregatlar ----
        DateOnly mauFrom = f.To.AddDays(-29);
        DateOnly loadFrom = new[] { f.PrevFrom, mauFrom }.Min();
        var du = (await db.UsageDailyUsers.AsNoTracking().Where(x => x.Date >= loadFrom && x.Date <= f.To).ToListAsync(ct))
            .Where(x => ids.Contains(x.TenantId)).ToList();
        var dm = (await db.UsageDailyModules.AsNoTracking().Where(x => x.Date >= f.PrevFrom && x.Date <= f.To).ToListAsync(ct))
            .Where(x => ids.Contains(x.TenantId)).ToList();
        var dd = (await db.UsageDailyDocs.AsNoTracking().Where(x => x.Date >= f.PrevFrom && x.Date <= f.To).ToListAsync(ct))
            .Where(x => ids.Contains(x.TenantId)).ToList();
        var lastLogin = await db.UsageStates.AsNoTracking().ToDictionaryAsync(s => s.TenantId, s => s.LastLoginUtc, ct);

        bool InPeriod(DateOnly d) => d >= f.From && d <= f.To;
        bool InPrev(DateOnly d) => d >= f.PrevFrom && d <= f.PrevTo;

        // ---- Overview ----
        var duPeriod = du.Where(x => InPeriod(x.Date)).ToList();
        int activeTenants = duPeriod.Select(x => x.TenantId).Distinct().Count();
        double dau = Enumerable.Range(0, f.Days).Select(i => f.From.AddDays(i))
            .Average(d => du.Count(x => x.Date == d));
        int wau = du.Where(x => x.Date > f.To.AddDays(-7)).Select(x => (x.TenantId, x.UserId)).Distinct().Count();
        int mau = du.Where(x => x.Date >= mauFrom).Select(x => (x.TenantId, x.UserId)).Distinct().Count();
        double dau30 = Enumerable.Range(0, 30).Select(i => mauFrom.AddDays(i)).Average(d => du.Count(x => x.Date == d));
        double stickiness = mau == 0 ? 0 : dau30 / mau;

        // Sinov -> pullik va churn: faqat paket obunasi (sinovdagi tenant faqat xizmat sotib olsa — konversiya emas).
        bool EverPaid(int tid) => subsByTenant.GetValueOrDefault(tid)?.Any(s => s.Period != BillingPeriod.Trial && !SubscriptionService.IsAddOn(s)
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expired)) == true;
        var trialStarts = subs.Where(s => s.Period == BillingPeriod.Trial && InPeriod(s.StartDate) && ids.Contains(s.TenantId))
            .Select(s => s.TenantId).Distinct().ToList();
        int converted = trialStarts.Count(EverPaid);

        bool PaidCovering(int tid, DateOnly d) => subsByTenant.GetValueOrDefault(tid)?.Any(s => s.Period != BillingPeriod.Trial && !SubscriptionService.IsAddOn(s)
            && s.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired && s.StartDate <= d && s.EndDate.AddDays(grace) >= d) == true;
        var payingAtStart = tenantsAll.Where(t => !provisioning.IsDemo(t) && (f.Plan == null || ids.Contains(t.Id)) && PaidCovering(t.Id, f.From)).Select(t => t.Id).ToList();
        DateOnly churnCheck = f.To < today ? f.To : today;
        int churned = payingAtStart.Count(tid => !PaidCovering(tid, churnCheck));

        DateOnly revFrom = f.From, revTo = f.To;
        long revenue = await db.Payments.AsNoTracking().Where(p => !p.IsReversed && p.PaidAt >= revFrom && p.PaidAt <= revTo).SumAsync(p => (long?)p.AmountTiyin, ct) ?? 0;

        var overview = new Overview(activeTenants, tenants.Count(t => t.Status != TenantStatus.Archived), Math.Round(dau, 1), wau, mau,
            Math.Round(stickiness, 3), trialStarts.Count, converted, trialStarts.Count == 0 ? 0 : (double)converted / trialStarts.Count,
            tenants.Sum(t => TenantMrr(t.Id)), revenue, payingAtStart.Count, churned,
            payingAtStart.Count == 0 ? 0 : (double)churned / payingAtStart.Count, dd.Where(x => InPeriod(x.Date)).Sum(x => x.Count),
            tenants.Sum(t => TenantAddOnMrr(t.Id)), tenants.Count(t => t.Status != TenantStatus.Archived && AddOns(t.Id).Count > 0));

        // ---- Tenantlar va health score ----
        var activeStatuses = new[] { TenantStatus.Trial, TenantStatus.Active, TenantStatus.Grace };
        var tenantRows = new List<TenantRow>();
        var unusedByModule = new Dictionary<string, int>();
        foreach (var t in tenants.Where(t => t.Status != TenantStatus.Archived))
        {
            var cur = Current(t.Id);
            string? planCode = cur?.Plan?.Code;
            string? edition = Edition(t.Id);
            var allowed = EditionModules.ModulesOf(edition).Select(m => m.ToString()).ToList();
            var tdm = dm.Where(x => x.TenantId == t.Id).ToList();
            var usedModules = tdm.Where(x => InPeriod(x.Date) && x.Module != nameof(ProductModule.Core)).Select(x => x.Module).Distinct().ToList();
            int actions = tdm.Where(x => InPeriod(x.Date)).Sum(x => x.Count), prev = tdm.Where(x => InPrev(x.Date)).Sum(x => x.Count);
            int users = duPeriod.Where(x => x.TenantId == t.Id).Select(x => x.UserId).Distinct().Count();
            int seats = cur?.Users ?? cur?.Plan?.MaxUsers ?? 0;
            var ll = lastLogin.GetValueOrDefault(t.Id);
            var unused = allowed.Where(m => !usedModules.Contains(m)).ToList();
            if (activeStatuses.Contains(t.Status))
                foreach (var m in unused) unusedByModule[m] = unusedByModule.GetValueOrDefault(m) + 1;
            int score = HealthScore(ll, users, seats, usedModules.Count(allowed.Contains), allowed.Count, actions, prev, clock.UtcNow);
            string? planName = cur?.Plan?.Name is { } pn ? string.Join(" + ", AddOns(t.Id).Select(a => a.Plan!.Name).Distinct().Prepend(pn)) : null;
            tenantRows.Add(new TenantRow(t, planName, planCode, TenantMrr(t.Id), ll, users, seats, usedModules.Count(allowed.Contains), allowed.Count,
                actions, prev, dd.Where(x => x.TenantId == t.Id && InPeriod(x.Date)).Sum(x => x.Count), score, Risk(score), unused, edition));
        }

        // ---- Modullar ----
        var modules = new List<ModuleRow>();
        foreach (var m in EditionModules.PaidModules.Concat(EditionModules.AddOnModules).Select(x => x.ToString()).Append(nameof(ProductModule.Core)))
        {
            var rows = dm.Where(x => x.Module == m).ToList();
            int a = rows.Where(x => InPeriod(x.Date)).Sum(x => x.Count), p = rows.Where(x => InPrev(x.Date)).Sum(x => x.Count);
            modules.Add(new ModuleRow(m, ModuleLabel(m), rows.Where(x => InPeriod(x.Date)).Select(x => x.TenantId).Distinct().Count(), a, p,
                p == 0 ? (a > 0 ? null : 0) : Math.Round(100.0 * (a - p) / p, 1), unusedByModule.GetValueOrDefault(m)));
        }
        modules = modules.OrderByDescending(x => x.Actions).ToList();

        var pages = dm.Where(x => InPeriod(x.Date)).GroupBy(x => (x.Controller, x.Module, x.ActionType))
            .Select(g => new PageRow(g.Key.Controller, g.Key.Module, g.Key.ActionType, g.Sum(x => x.Count), g.Select(x => x.TenantId).Distinct().Count(),
                g.Key.Controller.Length > 0 && EditionModules.IsMapped(g.Key.Controller)))
            .OrderByDescending(x => x.Actions).ToList();
        var actionTypes = dm.Where(x => InPeriod(x.Date)).GroupBy(x => x.ActionType).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var docs = DocNames.Select(n => new DocRow(n, dd.Where(x => x.DocType == n && InPeriod(x.Date)).Sum(x => x.Count),
            dd.Where(x => x.DocType == n && InPeriod(x.Date)).Select(x => x.TenantId).Distinct().Count())).ToList();

        // ---- Seriyalar (grafiklar) ----
        var labels = Enumerable.Range(0, f.Days).Select(i => f.From.AddDays(i)).ToList();
        var activeSeries = new Series(labels.Select(d => d.ToString("dd.MM")).ToList(), new()
        {
            ["Faol foydalanuvchilar"] = labels.Select(d => du.Count(x => x.Date == d)).ToList(),
            ["Faol tenantlar"] = labels.Select(d => du.Where(x => x.Date == d).Select(x => x.TenantId).Distinct().Count()).ToList(),
        });
        var moduleSeries = new Series(activeSeries.Labels, modules.Where(m => m.Actions > 0).Take(6)
            .ToDictionary(m => m.Name, m => labels.Select(d => dm.Where(x => x.Module == m.Module && x.Date == d).Sum(x => x.Count)).ToList()));

        // ---- Kohortalar: yaratilgan oy → keyingi haftalarda faol bo'lganlar ulushi ----
        var cohorts = await CohortsAsync(tenants, today, ct);

        // ---- Funnel: davrda sinov ochganlar ----
        var funnelIds = trialStarts.ToHashSet();
        var everDu = funnelIds.Count == 0 ? [] : await db.UsageDailyUsers.AsNoTracking().Where(x => funnelIds.Contains(x.TenantId))
            .Select(x => new { x.TenantId, x.Date, x.Logins }).ToListAsync(ct);
        var everDocs = funnelIds.Count == 0 ? [] : await db.UsageDailyDocs.AsNoTracking().Where(x => funnelIds.Contains(x.TenantId)).Select(x => x.TenantId).Distinct().ToListAsync(ct);
        var everCreates = funnelIds.Count == 0 ? [] : await db.UsageDailyModules.AsNoTracking()
            .Where(x => funnelIds.Contains(x.TenantId) && x.ActionType == UsageActionType.Create).Select(x => x.TenantId).Distinct().ToListAsync(ct);
        var withLogin = everDu.Where(x => x.Logins > 0).Select(x => x.TenantId).ToHashSet();
        var withDoc = everDocs.Concat(everCreates).Where(withLogin.Contains).ToHashSet();
        var active3 = everDu.GroupBy(x => x.TenantId).Where(g => g.Select(x => x.Date).Distinct().Count() >= 3).Select(g => g.Key).Where(withDoc.Contains).ToHashSet();
        var funnel = new List<FunnelStep>
        {
            new("Sinov ochildi", funnelIds.Count),
            new("Birinchi kirish", withLogin.Count),
            new("Birinchi hujjat", withDoc.Count),
            new("3+ kun faol", active3.Count),
            new("To'ladi", funnelIds.Count(EverPaid)),
        };

        return new Report(f, overview, modules, pages, docs, actionTypes, tenantRows, cohorts, funnel, activeSeries, moduleSeries);
    }

    /// <summary>Health score 0–100 (07_TRACTION.md): yaqinlik 40 + o'rinlar 25 + modul qamrovi 20 + trend 15.</summary>
    public static int HealthScore(DateTime? lastLoginUtc, int activeUsers, int seats, int modulesUsed, int modulesAllowed, int actions, int prevActions, DateTime? nowUtc = null)
    {
        double days = lastLoginUtc is null ? double.MaxValue : ((nowUtc ?? DateTime.UtcNow) - lastLoginUtc.Value).TotalDays;
        int recency = days <= 1 ? 40 : days <= 3 ? 30 : days <= 7 ? 20 : days <= 14 ? 10 : 0;
        int seat = seats <= 0 ? (activeUsers > 0 ? 25 : 0) : (int)Math.Round(25.0 * Math.Min(1.0, (double)activeUsers / seats));
        int coverage = modulesAllowed <= 0 ? 0 : (int)Math.Round(20.0 * Math.Min(1.0, (double)modulesUsed / modulesAllowed));
        int trend = actions == 0 ? 0 : prevActions == 0 ? 15 : actions >= prevActions ? 15 : actions >= 0.7 * prevActions ? 10 : actions >= 0.4 * prevActions ? 5 : 0;
        return recency + seat + coverage + trend;
    }

    public static string Risk(int score) => score < 40 ? "Yuqori" : score < 60 ? "O'rta" : "Past";

    private async Task<List<CohortRow>> CohortsAsync(List<Tenant> tenants, DateOnly today, CancellationToken ct)
    {
        const int months = 6, weeks = 12;
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var cohortTenants = tenants.Select(t => (t.Id, Created: DateOnly.FromDateTime(clock.ToLocal(t.CreatedAtUtc)))).Where(x => x.Created >= firstMonth).ToList();
        var cids = cohortTenants.Select(x => x.Id).ToList();
        var days = cids.Count == 0 ? [] : await db.UsageDailyUsers.AsNoTracking().Where(x => cids.Contains(x.TenantId) && x.Date >= firstMonth)
            .Select(x => new { x.TenantId, x.Date }).Distinct().ToListAsync(ct);
        var byTenant = days.GroupBy(x => x.TenantId).ToDictionary(g => g.Key, g => g.Select(x => x.Date).ToHashSet());
        var rows = new List<CohortRow>();
        for (int m = 0; m < months; m++)
        {
            var start = firstMonth.AddMonths(m);
            var members = cohortTenants.Where(x => x.Created >= start && x.Created < start.AddMonths(1)).ToList();
            var cells = new double?[weeks];
            for (int w = 0; w < weeks; w++)
            {
                if (members.Count == 0) continue;
                var eligible = members.Where(x => x.Created.AddDays(7 * w) <= today).ToList();
                if (eligible.Count == 0) continue; // hafta hali kelmagan
                int activeN = eligible.Count(x => byTenant.TryGetValue(x.Id, out var ds) && ds.Any(d => d >= x.Created.AddDays(7 * w) && d < x.Created.AddDays(7 * (w + 1))));
                cells[w] = Math.Round(100.0 * activeN / eligible.Count, 0);
            }
            rows.Add(new CohortRow(start.ToString("yyyy-MM"), members.Count, cells));
        }
        return rows;
    }
}
