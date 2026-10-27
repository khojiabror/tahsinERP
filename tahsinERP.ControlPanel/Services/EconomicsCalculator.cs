using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services;

// Reja tannarx/narx modeli (Docs/ControlPanel/10_ECONOMICS.md). Sof hisob — bazaga tegmaydi, testlanadi.
// Summalar decimal tiyin: oraliq hisobda yaxlitlanmaydi, ekranda Money.Format((long)x).

/// <summary>Model farazlari (EconomicsSettings + rejadagi doimiy xarajatlar).</summary>
public sealed record EconAssumptions(
    decimal PayrollTaxPercent,
    long ServerPrice, int FirmsPerServer, int MinServers,
    long PerFirm, long Platform,
    long SupportSalary, int FirmsPerSupport, int MinSupport,
    decimal TurnoverTaxPercent, decimal BankFeePercent, decimal ProfitTaxPercent,
    decimal OpexDevelopment, decimal OpexSales, decimal OpexAdmin)
{
    public decimal Opex => OpexDevelopment + OpexSales + OpexAdmin;
    /// <summary>Tushumdan ajratma ulushi (aylanma soliq + bank), 0..1.</summary>
    public decimal Deduction => (TurnoverTaxPercent + BankFeePercent) / 100m;
    public decimal ProfitTax => ProfitTaxPercent / 100m;
}

/// <summary>Paket kirishi. <paramref name="ModuleShare"/> — paket modullari balli ÷ barcha modullar balli (0..1).</summary>
public sealed record EconPackageInput(
    int PlanId, string Code, string Name, int Users, decimal ModuleShare, int PlannedTenants,
    decimal MarkupPercent, decimal TargetGrossMarginPercent, decimal VatPercent,
    long? CurrentPriceGross, decimal? MarketPerUser)
{
    /// <summary>Yuk koeffitsienti: modul ulushi × foydalanuvchi — xarajat shu nisbatda taqsimlanadi (ABC usuli).</summary>
    public decimal Load => ModuleShare * Users;
}

/// <summary>N ta firmada oylik xarajat (pog'onali: server va support sig'im to'lganda qo'shiladi).</summary>
public sealed record CostAtVolume(int Firms, decimal Users, int Servers, int SupportStaff,
    decimal Server, decimal PerFirmAndPlatform, decimal Support, decimal Opex)
{
    public decimal Infra => Server + PerFirmAndPlatform;
    public decimal Cogs => Infra + Support;
    public decimal Total => Cogs + Opex;
    public decimal PerFirm => Firms > 0 ? Total / Firms : 0;
    public decimal CogsPerFirm => Firms > 0 ? Cogs / Firms : 0;
    public decimal PerUser => Users > 0 ? Total / Users : 0;
}

public enum PriceHealth { Ok, LowMargin, AboveMarket, Loss, NoPrice }

public sealed record EconPackageResult(
    EconPackageInput Input,
    decimal Infra, decimal SupportShare, decimal OpexShare,
    decimal NetProfit, decimal ProfitTax, decimal Deductions,
    decimal MarketPrice)
{
    public decimal Cogs => Infra + SupportShare;
    public decimal FullCost => Cogs + OpexShare;
    public decimal FullCostPerUser => Input.Users > 0 ? FullCost / Input.Users : 0;
    /// <summary>Hisoblangan narx QQSsiz: tannarx + sof foyda + foyda solig'i + tushumdan ajratmalar.</summary>
    public decimal CalcNet => FullCost + NetProfit + ProfitTax + Deductions;
    public decimal CalcVat => CalcNet * Input.VatPercent / 100m;
    public decimal CalcGross => CalcNet + CalcVat;
    /// <summary>Tavsiya: QQS bilan, 10 000 so'mga yuqoriga yaxlitlangan (tiyin).</summary>
    public long RecommendedGross => EconomicsCalculator.RoundUp(CalcGross, 10_000 * 100);
    /// <summary>Tahlil qilinadigan narx: tarifda belgilangani, bo'lmasa tavsiya.</summary>
    public long EffectiveGross => Input.CurrentPriceGross is > 0 ? Input.CurrentPriceGross.Value : RecommendedGross;
    public decimal EffectiveNet => EffectiveGross / (1m + Input.VatPercent / 100m);
    public decimal MarketDiff => MarketPrice > 0 ? CalcNet / MarketPrice - 1m : 0;
}

public sealed record EconPlanResult(
    EconAssumptions Assumptions,
    IReadOnlyList<EconPackageResult> Packages,
    int PlannedFirms, decimal AvgUsers, decimal AvgLoad,
    CostAtVolume AtPlan,
    IReadOnlyList<CostAtVolume> Simulation,
    decimal AvgNetPrice,
    int? BreakEvenFirms);

public sealed class EconomicsCalculator(EconAssumptions a)
{
    public static readonly int[] DefaultSimulation = [1, 5, 10, 25, 50, 100];
    public const int BreakEvenSearchLimit = 1000;

    public EconAssumptions Assumptions => a;

    public static long RoundUp(decimal value, long step) => value <= 0 ? 0 : (long)(Math.Ceiling(value / step) * step);

    private static int Steps(int firms, int perUnit, int min) => Math.Max(min, perUnit > 0 ? (int)Math.Ceiling(firms / (decimal)perUnit) : min);

    public CostAtVolume At(int firms, decimal avgUsers)
    {
        int servers = Steps(firms, a.FirmsPerServer, a.MinServers);
        int support = Steps(firms, a.FirmsPerSupport, a.MinSupport);
        return new CostAtVolume(firms, firms * avgUsers, servers, support,
            servers * (decimal)a.ServerPrice,
            firms * (decimal)a.PerFirm + a.Platform,
            support * (decimal)a.SupportSalary * (1 + a.PayrollTaxPercent / 100m),
            a.Opex);
    }

    /// <summary>N firmada oylik foyda (soliqdan oldin): tushum × (1 − ajratma) − jami xarajat (pog'onali).</summary>
    public decimal Profit(int firms, decimal avgUsers, decimal avgNetPrice) =>
        firms * avgNetPrice * (1 - a.Deduction) - At(firms, avgUsers).Total;

    /// <summary>Foyda ≥ 0 bo'ladigan eng kichik firmalar soni (pog'onali xarajat hisobga olinadi). Null — limit ichida yo'q.</summary>
    public int? BreakEven(decimal avgUsers, decimal avgNetPrice)
    {
        if (avgNetPrice <= 0) return null;
        for (int n = 1; n <= BreakEvenSearchLimit; n++)
            if (Profit(n, avgUsers, avgNetPrice) >= 0) return n;
        return null;
    }

    public EconPlanResult Plan(IReadOnlyList<EconPackageInput> packages, IReadOnlyList<int>? simulation = null)
    {
        int plannedFirms = packages.Sum(p => p.PlannedTenants);
        decimal totalLoad = packages.Sum(p => p.Load * p.PlannedTenants);
        decimal avgUsers = plannedFirms > 0 ? packages.Sum(p => (decimal)p.Users * p.PlannedTenants) / plannedFirms : 0;
        decimal avgLoad = plannedFirms > 0 ? totalLoad / plannedFirms : 0;
        var atPlan = At(plannedFirms, avgUsers);

        var results = packages.Select(p =>
        {
            // Reja bo'sh bo'lsa paket yolg'iz (1 firma) deb olinadi — taqsimlashga asos bo'lishi uchun.
            var basis = plannedFirms > 0 ? atPlan : At(1, p.Users);
            decimal firms = plannedFirms > 0 ? plannedFirms : 1;
            decimal loadBase = totalLoad > 0 ? totalLoad : p.Load;
            decimal share = loadBase > 0 ? p.Load / loadBase : 0;
            decimal infra = basis.Infra / firms;
            decimal support = basis.Support * share;
            decimal opex = basis.Opex * share;
            decimal full = infra + support + opex;
            decimal profit = full * p.MarkupPercent / 100m;
            decimal ptax = a.ProfitTax < 1 ? profit / (1 - a.ProfitTax) - profit : 0;
            decimal beforeDed = full + profit + ptax;
            decimal ded = a.Deduction < 1 ? beforeDed / (1 - a.Deduction) - beforeDed : 0;
            decimal market = (p.MarketPerUser ?? 0) * p.Users;
            return new EconPackageResult(p, infra, support, opex, profit, ptax, ded, market);
        }).ToList();

        decimal avgNet = plannedFirms > 0
            ? results.Sum(r => r.EffectiveNet * r.Input.PlannedTenants) / plannedFirms
            : 0;
        var sim = (simulation ?? DefaultSimulation).Select(n => At(n, avgUsers)).ToList();
        return new EconPlanResult(a, results, plannedFirms, avgUsers, avgLoad, atPlan, sim, avgNet, BreakEven(avgUsers, avgNet));
    }

    /// <summary>Narx sog'ligi: zarar (to'liq tannarxni qoplamaydi) → gross margin past → bozordan 20%+ qimmat → yaxshi.</summary>
    public PriceHealth Health(EconPackageResult r)
    {
        decimal net = r.EffectiveNet;
        if (net <= 0) return PriceHealth.NoPrice;
        if (NetProfitAfterTax(r, net) < 0) return PriceHealth.Loss;
        if (GrossMargin(r, net) * 100m < r.Input.TargetGrossMarginPercent) return PriceHealth.LowMargin;
        if (r.MarketPrice > 0 && net > r.MarketPrice * 1.2m) return PriceHealth.AboveMarket;
        return PriceHealth.Ok;
    }

    /// <summary>(narx − ajratma − COGS) ÷ narx.</summary>
    public decimal GrossMargin(EconPackageResult r, decimal net) => net > 0 ? (net * (1 - a.Deduction) - r.Cogs) / net : 0;

    /// <summary>Sof foyda 1 firmadan, foyda solig'idan keyin (zarar bo'lsa soliqsiz).</summary>
    public decimal NetProfitAfterTax(EconPackageResult r, decimal net)
    {
        decimal pretax = net * (1 - a.Deduction) - r.FullCost;
        return pretax > 0 ? pretax * (1 - a.ProfitTax) : pretax;
    }

    /// <summary>Paket modul ulushi: Core har doim + paket modullari balli ÷ barcha ballar yig'indisi.</summary>
    public static decimal ModuleShare(IEnumerable<string> planModules, IReadOnlyDictionary<string, int> weights)
    {
        int total = weights.Values.Sum();
        if (total <= 0) return 0;
        var set = new HashSet<string>(planModules.Select(m => m.Trim()).Where(m => m.Length > 0), StringComparer.OrdinalIgnoreCase) { "Core" };
        return weights.Where(w => set.Contains(w.Key)).Sum(w => w.Value) / (decimal)total;
    }

    public static bool IsCogs(CostCategory c) => c is CostCategory.Infra or CostCategory.Support;
}
