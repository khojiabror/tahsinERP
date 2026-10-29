using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.Services.Editions;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Bir paket bo'yicha haqiqiy holat (oy ichida).</summary>
public sealed record ActualPlanRow(int PlanId, string Code, string Name, int Tenants, int Users, decimal Mrr);

/// <summary>
/// Oy fakti: to'layotgan firmalar (obunalardan), MRR (tarif narxi bo'yicha, QQSsiz), kassaga tushgan pul (to'lovlar, QQSsiz)
/// va kiritilgan xarajatlar. Foyda hisoblash usulida (MRR), pul oqimi kassa usulida.
/// </summary>
public sealed record EconActual(
    DateOnly From, DateOnly To,
    IReadOnlyList<ActualPlanRow> Plans,
    decimal CashNet,
    IReadOnlyDictionary<CostCategory, decimal> Expenses,
    decimal Deduction)
{
    public int Tenants => Plans.Sum(p => p.Tenants);
    public int Users => Plans.Sum(p => p.Users);
    public decimal Mrr => Plans.Sum(p => p.Mrr);
    public decimal Exp(CostCategory c) => Expenses.TryGetValue(c, out var v) ? v : 0;
    public decimal Cogs => Exp(CostCategory.Infra) + Exp(CostCategory.Support);
    public decimal Opex => Exp(CostCategory.Development) + Exp(CostCategory.Sales) + Exp(CostCategory.Admin);
    public decimal Total => Cogs + Opex;
    public decimal Deductions => Mrr * Deduction;
    public decimal Profit => Mrr - Deductions - Total;
    public decimal CashFlow => CashNet * (1 - Deduction) - Total;
    public decimal GrossMargin => Mrr > 0 ? (Mrr - Deductions - Cogs) / Mrr : 0;
    public decimal CostPerFirm => Tenants > 0 ? Total / Tenants : 0;
    public decimal CostPerUser => Users > 0 ? Total / Users : 0;
}

/// <summary>Reja (moslashuvchan byudjet: haqiqiy firmalar soniga qayta hisoblangan reja) va fakt.</summary>
public sealed record EconFlexPlan(CostAtVolume Cost, decimal Revenue, decimal Deduction)
{
    public decimal Deductions => Revenue * Deduction;
    public decimal Profit => Revenue - Deductions - Cost.Total;
    public decimal GrossMargin => Revenue > 0 ? (Revenue - Deductions - Cost.Cogs) / Revenue : 0;
}

public sealed record EconMonth(int Year, int Month, decimal Mrr, decimal CashNet, decimal Expenses, int Tenants)
{
    public decimal Profit(decimal deduction) => Mrr * (1 - deduction) - Expenses;
}

public sealed class EconomicsService(ControlDbContext db, Clock clock, AuditLogger audit)
{
    public static readonly string[] Currencies = ["UZS", "USD"];

    public Task<EconomicsSettings> SettingsAsync(CancellationToken ct = default) => db.EconomicsSettings.SingleAsync(x => x.Id == 1, ct);

    /// <summary>Sotiladigan paketlar (sinov va qo'shimcha xizmatlarsiz) — reja shular bo'yicha.</summary>
    public Task<List<Plan>> SellablePlansAsync(CancellationToken ct = default) =>
        db.Plans.Where(p => !p.IsAddOn && p.Code != PlanCodes.Trial).OrderBy(p => p.SortOrder).ToListAsync(ct);

    public async Task<EconomicsCalculator> CalculatorAsync(CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        var costs = await db.PlannedCosts.AsNoTracking().ToListAsync(ct);
        return new EconomicsCalculator(Assumptions(s, costs));
    }

    public static EconAssumptions Assumptions(EconomicsSettings s, IEnumerable<PlannedCost> costs)
    {
        decimal Sum(CostCategory c) => costs.Where(x => x.Category == c)
            .Sum(x => x.Kind == PlannedCostKind.Staff ? x.Count * (decimal)x.AmountTiyin * (1 + s.PayrollTaxPercent / 100m) : x.AmountTiyin);
        return new EconAssumptions(s.PayrollTaxPercent, s.ServerPriceTiyin, s.FirmsPerServer, s.MinServers, s.PerFirmTiyin, s.PlatformTiyin,
            s.SupportSalaryTiyin, s.FirmsPerSupport, s.MinSupport, s.TurnoverTaxPercent, s.BankFeePercent, s.ProfitTaxPercent,
            Sum(CostCategory.Development), Sum(CostCategory.Sales), Sum(CostCategory.Admin));
    }

    /// <summary>Raqobatchi oylik narxi so'mda (tiyin): so'm berilgan bo'lsa o'zi, bo'lmasa $ × kurs.</summary>
    public static decimal? CompetitorPrice(Competitor c, decimal usdRate) =>
        c.PriceMonthTiyin is { } som ? som : c.PriceMonthUsd is { } usd ? usd * usdRate * 100m : null;

    /// <summary>"DIST,SAVDO" → [DIST, SAVDO].</summary>
    public static string[] MatchCodes(string? codes) =>
        (codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Matches(Competitor c, string planCode) =>
        MatchCodes(c.MatchPlanCodes).Contains(planCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>Paket bo'yicha raqobatchilar 1 foydalanuvchi o'rtacha narxi; mos raqobatchi bo'lmasa — umumiy o'rtacha.</summary>
    public static Dictionary<string, decimal?> MarketPerUser(IReadOnlyList<Competitor> competitors, decimal usdRate, IEnumerable<string> planCodes)
    {
        var priced = competitors.Where(c => c.Include && c.Users is > 0)
            .Select(c => new { Competitor = c, PerUser = CompetitorPrice(c, usdRate) / c.Users!.Value })
            .Where(x => x.PerUser is > 0).ToList();
        decimal? all = priced.Count > 0 ? priced.Average(x => x.PerUser!.Value) : null;
        return planCodes.ToDictionary(code => code, code =>
        {
            var m = priced.Where(x => Matches(x.Competitor, code)).ToList();
            return m.Count > 0 ? m.Average(x => x.PerUser!.Value) : all;
        }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Yangi modul uchun og'irlik (Sozlamalarda o'zgartirilguncha).</summary>
    public const int DefaultModuleWeight = 2;

    /// <summary>
    /// Modul og'irliklari: bazadagilar + mahsulotga keyin qo'shilgan modullar (ProductModule) standart og'irlik bilan.
    /// Yangi paket yoki modul qo'shilsa reja o'zi qamrab oladi; bazaga faqat Sozlamalarda saqlanganda yoziladi.
    /// Qo'shimcha xizmatlar (Website) paketga kirmaydi — og'irliksiz.
    /// </summary>
    public async Task<Dictionary<string, int>> WeightsAsync(CancellationToken ct = default)
    {
        var weights = await db.ModuleWeights.AsNoTracking().ToDictionaryAsync(w => w.Module, w => w.Weight, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var m in AllModules())
            weights.TryAdd(m, DefaultModuleWeight);
        return weights;
    }

    /// <summary>"Core" + paketga kiradigan barcha ProductModule nomlari.</summary>
    public static IEnumerable<string> AllModules() =>
        Enum.GetValues<ProductModule>().Where(m => !EditionModules.IsAddOn(m)).Select(m => m.ToString());

    public async Task<(EconomicsCalculator Calc, EconPlanResult Plan, EconomicsSettings Settings)> PlanAsync(IReadOnlyList<int>? simulation = null, CancellationToken ct = default)
    {
        var settings = await SettingsAsync(ct);
        var costs = await db.PlannedCosts.AsNoTracking().ToListAsync(ct);
        var calc = new EconomicsCalculator(Assumptions(settings, costs));
        var weights = await WeightsAsync(ct);
        var plans = await SellablePlansAsync(ct);
        var econ = await db.PlanEconomics.AsNoTracking().ToDictionaryAsync(e => e.PlanId, ct);
        var competitors = await db.Competitors.AsNoTracking().ToListAsync(ct);
        var market = MarketPerUser(competitors, settings.UsdRate, plans.Select(p => p.Code));

        var inputs = plans.Select(p =>
        {
            econ.TryGetValue(p.Id, out var e);
            return new EconPackageInput(p.Id, p.Code, p.Name, p.MaxUsers,
                EconomicsCalculator.ModuleShare(p.Modules.Split(','), weights),
                e?.PlannedTenants ?? 0, e?.MarkupPercent ?? 40m, e?.TargetGrossMarginPercent ?? 60m, p.VatRatePercent,
                p.PriceMonthTiyin, market.GetValueOrDefault(p.Code));
        }).ToList();
        return (calc, calc.Plan(inputs, simulation), settings);
    }

    // ---------------------------------------------------------------------------------------------- fakt

    public static (DateOnly From, DateOnly To) MonthRange(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    public async Task<EconActual> ActualAsync(int year, int month, decimal deduction, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);

        // To'layotgan firmalar: oy ichida amalda bo'lgan pullik paket obunasi (sinov va qo'shimcha xizmatlarsiz).
        var subs = await SubscriptionService.PackageOnly(db.Subscriptions).AsNoTracking()
            .Where(s => s.Plan!.Code != PlanCodes.Trial && s.Period != BillingPeriod.Trial
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expired)
                && s.StartDate <= to && s.EndDate >= from)
            .Select(s => new
            {
                s.TenantId, s.StartDate, s.Users, s.Period, s.PlanId, s.Plan!.Code, s.Plan.Name,
                s.Plan.PriceMonthTiyin, s.Plan.PriceYearTiyin, s.Plan.VatRatePercent
            })
            .ToListAsync(ct);
        var current = subs.GroupBy(s => s.TenantId).Select(g => g.OrderByDescending(s => s.StartDate).First()).ToList();
        var rows = current.GroupBy(s => new { s.PlanId, s.Code, s.Name })
            .Select(g => new ActualPlanRow(g.Key.PlanId, g.Key.Code, g.Key.Name, g.Count(), g.Sum(s => s.Users),
                g.Sum(s =>
                {
                    decimal gross = s.Period == BillingPeriod.Year && s.PriceYearTiyin is > 0 ? s.PriceYearTiyin.Value / 12m : s.PriceMonthTiyin ?? 0;
                    return gross / (1 + s.VatRatePercent / 100m);
                })))
            .OrderBy(r => r.Code).ToList();

        decimal cash = await CashNetAsync(from, to, ct);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.Date >= from && e.Date <= to)
            .GroupBy(e => e.Category).Select(g => new { g.Key, Sum = g.Sum(e => e.AmountTiyin) })
            .ToDictionaryAsync(x => x.Key, x => (decimal)x.Sum, ct);
        return new EconActual(from, to, rows, cash, expenses, deduction);
    }

    /// <summary>Oy ichida kassaga tushgan pul (storno qilinmagan to'lovlar), hisob-faktura QQS ulushi bo'yicha QQSsiz.</summary>
    private async Task<decimal> CashNetAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var pays = await db.Payments.AsNoTracking().Where(p => !p.IsReversed && p.PaidAt >= from && p.PaidAt <= to)
            .Select(p => new { p.AmountTiyin, p.Invoice!.TotalTiyin, p.Invoice.VatTiyin }).ToListAsync(ct);
        return pays.Sum(p => p.TotalTiyin > 0 ? p.AmountTiyin * (1 - p.VatTiyin / (decimal)p.TotalTiyin) : p.AmountTiyin);
    }

    /// <summary>Haqiqiy firmalar soni va tarkibiga qayta hisoblangan reja (moslashuvchan byudjet) — fakt bilan adolatli solishtirish.</summary>
    public static EconFlexPlan Flex(EconomicsCalculator calc, EconPlanResult plan, EconActual actual)
    {
        decimal avgUsers = actual.Tenants > 0 ? actual.Users / (decimal)actual.Tenants : plan.AvgUsers;
        var cost = calc.At(actual.Tenants, avgUsers);
        decimal revenue = actual.Plans.Sum(r =>
            r.Tenants * (plan.Packages.FirstOrDefault(p => p.Input.PlanId == r.PlanId)?.EffectiveNet ?? 0));
        return new EconFlexPlan(cost, revenue, calc.Assumptions.Deduction);
    }

    public async Task<List<EconMonth>> TrendAsync(int months, CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        decimal ded = (s.TurnoverTaxPercent + s.BankFeePercent) / 100m;
        DateOnly today = clock.Today;
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var list = new List<EconMonth>();
        for (int i = 0; i < months; i++)
        {
            var m = first.AddMonths(i);
            var a = await ActualAsync(m.Year, m.Month, ded, ct);
            list.Add(new EconMonth(m.Year, m.Month, a.Mrr, a.CashNet, a.Total, a.Tenants));
        }
        return list;
    }

    // ---------------------------------------------------------------------------------------------- xarajatlar

    public static long ToTiyin(decimal amount, string currency, decimal rate) =>
        (long)Math.Round(amount * (currency == "UZS" ? 1m : rate) * 100m, MidpointRounding.AwayFromZero);

    public void Validate(Expense e)
    {
        if (string.IsNullOrWhiteSpace(e.Description)) throw new DomainException(L.T("Pnl_Econ_ErrDescription"));
        if (e.AmountOriginal <= 0) throw new DomainException(L.T("Pnl_Econ_ErrAmount"));
        if (!Currencies.Contains(e.Currency)) throw new DomainException(L.T("Pnl_Econ_ErrCurrency"));
        if (e.Currency != "UZS" && e.Rate <= 0) throw new DomainException(L.T("Pnl_Econ_ErrRate"));
        if (!Enum.IsDefined(e.Category)) throw new DomainException(L.T("Pnl_Econ_ErrCategory"));
        if (e.Currency == "UZS") e.Rate = 1m;
        e.Description = e.Description.Trim();
        e.Reference = string.IsNullOrWhiteSpace(e.Reference) ? null : e.Reference.Trim();
        e.AmountTiyin = ToTiyin(e.AmountOriginal, e.Currency, e.Rate);
    }

    public async Task<Expense> AddExpenseAsync(Expense e, int staffId, CancellationToken ct = default)
    {
        Validate(e);
        e.RecordedByStaffId = staffId;
        e.RecordedAtUtc = clock.UtcNow;
        db.Expenses.Add(e);
        await db.SaveChangesAsync(ct);
        audit.Add("expense.add", "Expense", e.Id, null, Snapshot(e));
        await db.SaveChangesAsync(ct);
        return e;
    }

    public async Task UpdateExpenseAsync(Expense e, object before, CancellationToken ct = default)
    {
        Validate(e);
        audit.Add("expense.update", "Expense", e.Id, before, Snapshot(e));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteExpenseAsync(int id, CancellationToken ct = default)
    {
        var e = await db.Expenses.FindAsync([id], ct) ?? throw new DomainException(L.T("Pnl_Econ_ErrNotFound"));
        db.Expenses.Remove(e);
        audit.Add("expense.delete", "Expense", e.Id, Snapshot(e));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>O'tgan oy xarajatlarini shu oyga nusxalaydi (oylik takrorlanadiganlar: maosh, server, ijara). Sana — oy ichida o'sha kun.</summary>
    public async Task<int> CopyPreviousMonthAsync(int year, int month, int staffId, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);
        var (pFrom, pTo) = MonthRange(from.AddMonths(-1).Year, from.AddMonths(-1).Month);
        var prev = await db.Expenses.AsNoTracking().Where(e => e.Date >= pFrom && e.Date <= pTo).OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync(ct);
        foreach (var p in prev)
        {
            var day = Math.Min(p.Date.Day, to.Day);
            db.Expenses.Add(new Expense
            {
                Date = new DateOnly(year, month, day), Category = p.Category, Description = p.Description, Currency = p.Currency,
                AmountOriginal = p.AmountOriginal, Rate = p.Rate, AmountTiyin = p.AmountTiyin, Reference = p.Reference,
                RecordedByStaffId = staffId, RecordedAtUtc = clock.UtcNow
            });
        }
        if (prev.Count > 0)
        {
            audit.Add("expense.copy-month", "Expense", $"{year:0000}-{month:00}", null, new { From = $"{pFrom:yyyy-MM}", prev.Count });
            await db.SaveChangesAsync(ct);
        }
        return prev.Count;
    }

    public static object Snapshot(Expense e) => new { e.Date, e.Category, e.Description, e.Currency, e.AmountOriginal, e.Rate, e.AmountTiyin, e.Reference };

    /// <summary>Hisoblangan tavsiya narxni tarifga yozadi: oylik QQS bilan, yillik = 10 oy (2 oy tekin).</summary>
    public async Task<Plan> ApplyRecommendedPriceAsync(int planId, CancellationToken ct = default)
    {
        var (_, result, _) = await PlanAsync(null, ct);
        var r = result.Packages.FirstOrDefault(p => p.Input.PlanId == planId) ?? throw new DomainException(L.T("Pnl_Econ_ErrNotFound"));
        var plan = await db.Plans.SingleAsync(p => p.Id == planId, ct);
        var before = new { plan.PriceMonthTiyin, plan.PriceYearTiyin };
        plan.PriceMonthTiyin = r.RecommendedGross;
        plan.PriceYearTiyin = r.RecommendedGross * 10;
        audit.Add("plan.price-from-economics", "Plan", plan.Id, before, new { plan.PriceMonthTiyin, plan.PriceYearTiyin });
        await db.SaveChangesAsync(ct);
        return plan;
    }
}
