using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Economics;

// Iqtisodiyot bo'limi (faqat Owner — Program.cs AuthorizeFolder). Reja: Settings → Plan; fakt: Expenses + obuna/to'lovlar → Index.

public static class Econ
{
    public static string Of(CostCategory c) => c switch
    {
        CostCategory.Infra => L.T("Pnl_Econ_Cat_Infra"),
        CostCategory.Support => L.T("Pnl_Econ_Cat_Support"),
        CostCategory.Development => L.T("Pnl_Econ_Cat_Development"),
        CostCategory.Sales => L.T("Pnl_Econ_Cat_Sales"),
        _ => L.T("Pnl_Econ_Cat_Admin")
    };

    public static readonly CostCategory[] AllCategories = [CostCategory.Infra, CostCategory.Support, CostCategory.Development, CostCategory.Sales, CostCategory.Admin];
    public static readonly CostCategory[] OpexCategories = [CostCategory.Development, CostCategory.Sales, CostCategory.Admin];

    public static string Of(PriceHealth h) => h switch
    {
        PriceHealth.Ok => L.T("Pnl_Econ_Health_Ok"),
        PriceHealth.LowMargin => L.T("Pnl_Econ_Health_LowMargin"),
        PriceHealth.AboveMarket => L.T("Pnl_Econ_Health_AboveMarket"),
        PriceHealth.Loss => L.T("Pnl_Econ_Health_Loss"),
        _ => L.T("Pnl_Econ_Health_NoPrice")
    };

    public static string Badge(PriceHealth h) => h switch
    {
        PriceHealth.Ok => "ok", PriceHealth.Loss => "bad", PriceHealth.NoPrice => "muted", _ => "warn"
    };

    /// <summary>decimal tiyin → "1 234 567" so'm (butun so'mga yaxlitlab).</summary>
    public static string M(decimal tiyin) => Money.Format((long)Math.Round(tiyin / 100m, MidpointRounding.AwayFromZero) * 100);

    public static string Usd(decimal tiyin, decimal rate) =>
        rate > 0 ? "$" + (tiyin / 100m / rate).ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ") : "";

    public static string Pct(decimal fraction) => (fraction * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>Farq klassi: yaxshi tomonga — var-good, yomon tomonga — var-bad.</summary>
    public static string VarClass(decimal diff, bool higherIsBetter) =>
        diff == 0 ? "" : (diff > 0) == higherIsBetter ? "var-good" : "var-bad";

    public static readonly string[] FeatureKeys = ["Warehouse", "Sales", "Finance", "Production", "Hr", "Distribution", "Bot", "Pos"];

    public static string FeatureName(string key) => L.T("Pnl_Econ_Feat_" + key);

    /// <summary>"Warehouse,~Finance" → {Warehouse: "1", Finance: "~"}.</summary>
    public static Dictionary<string, string> ParseFeatures(string? features)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (features ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool partial = raw.StartsWith('~');
            d[partial ? raw[1..] : raw] = partial ? "~" : "1";
        }
        return d;
    }

    public static string FeatureMark(string? v) => v switch { "1" => "✓", "~" => "±", _ => "—" };

    /// <summary>"2026-10" → (2026, 10); noto'g'ri bo'lsa bugungi oy.</summary>
    public static (int Year, int Month) ParseMonth(string? m, DateOnly today) =>
        DateOnly.TryParseExact((m ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? (d.Year, d.Month) : (today.Year, today.Month);

    public static decimal? ParseDecimal(string? text) =>
        decimal.TryParse((text ?? "").Replace(" ", "").Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
}

// ================================================================================================ Index (reja va fakt)

public class IndexModel(EconomicsService econ, Clock clock) : PanelPageModel
{
    public string Month { get; private set; } = "";
    public EconomicsCalculator Calc { get; private set; } = null!;
    public EconPlanResult Plan { get; private set; } = null!;
    public EconomicsSettings Settings { get; private set; } = null!;
    public EconActual Actual { get; private set; } = null!;
    public EconFlexPlan Flex { get; private set; } = null!;
    public List<EconMonth> Trend { get; private set; } = [];

    public async Task OnGetAsync(string? m)
    {
        var (y, mo) = Econ.ParseMonth(m, clock.Today);
        Month = $"{y:0000}-{mo:00}";
        (Calc, Plan, Settings) = await econ.PlanAsync();
        Actual = await econ.ActualAsync(y, mo, Calc.Assumptions.Deduction);
        Flex = EconomicsService.Flex(Calc, Plan, Actual);
        Trend = await econ.TrendAsync(6);
    }
}

// ================================================================================================ Plan (reja tannarx va narx)

public class PlanModel(EconomicsService econ) : PanelPageModel
{
    public EconomicsCalculator Calc { get; private set; } = null!;
    public EconPlanResult Plan { get; private set; } = null!;
    public EconomicsSettings Settings { get; private set; } = null!;
    [BindProperty(SupportsGet = true)] public string? Sim { get; set; }

    public async Task OnGetAsync()
    {
        var sim = (Sim ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int n) ? n : 0).Where(n => n is > 0 and <= 100_000).Distinct().Take(8).ToList();
        if (sim.Count == 0) sim = [.. EconomicsCalculator.DefaultSimulation];
        Sim = string.Join(",", sim);
        (Calc, Plan, Settings) = await econ.PlanAsync(sim);
    }

    public async Task<IActionResult> OnPostApplyPriceAsync(int planId)
    {
        if (await TryAsync(async () =>
            {
                var p = await econ.ApplyRecommendedPriceAsync(planId);
                FlashOk = L.T("Pnl_Econ_PriceApplied", p.Name, Money.FormatSom(p.PriceMonthTiyin ?? 0));
            }))
            return RedirectToPage();
        FlashError = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToPage();
    }
}

// ================================================================================================ Xarajatlar

public class ExpenseForm
{
    [Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FieldRequired))]
    public DateOnly? Date { get; set; }
    public CostCategory Category { get; set; } = CostCategory.Infra;
    [Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FieldRequired)), StringLength(300)]
    public string Description { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FieldRequired))]
    public string Amount { get; set; } = "";
    public string Currency { get; set; } = "UZS";
    public string? Rate { get; set; }
    [StringLength(200)] public string? Reference { get; set; }

    /// <summary>Formadagi qiymatlarni xarajatga yozadi; xato bo'lsa DomainException (servis tekshiruvi bilan bir xil matnlar).</summary>
    public void ApplyTo(Expense e)
    {
        e.Date = Date ?? throw new DomainException(PnlVal.FieldRequired);
        e.Category = Category;
        e.Description = Description;
        e.Currency = Currency;
        e.AmountOriginal = Econ.ParseDecimal(Amount) ?? throw new DomainException(L.T("Pnl_Econ_ErrAmount"));
        e.Rate = Currency == "UZS" ? 1m : Econ.ParseDecimal(Rate) ?? 0m;
        e.Reference = Reference;
    }

    public static ExpenseForm From(Expense e) => new()
    {
        Date = e.Date, Category = e.Category, Description = e.Description, Currency = e.Currency, Reference = e.Reference,
        Amount = e.AmountOriginal.ToString("0.##", CultureInfo.InvariantCulture),
        Rate = e.Currency == "UZS" ? null : e.Rate.ToString("0.##", CultureInfo.InvariantCulture)
    };
}

public class ExpensesModel(ControlDbContext db, EconomicsService econ, Clock clock) : PanelPageModel
{
    public string Month { get; private set; } = "";
    public List<Expense> Items { get; private set; } = [];
    public EconActual Actual { get; private set; } = null!;
    public EconFlexPlan Flex { get; private set; } = null!;
    public EconomicsCalculator Calc { get; private set; } = null!;
    public decimal UsdRate { get; private set; }
    [BindProperty] public ExpenseForm Form { get; set; } = new();

    private async Task LoadAsync(int y, int mo)
    {
        Month = $"{y:0000}-{mo:00}";
        var (from, to) = EconomicsService.MonthRange(y, mo);
        Items = await db.Expenses.AsNoTracking().Where(e => e.Date >= from && e.Date <= to).OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync();
        (Calc, var plan, var s) = await econ.PlanAsync();
        UsdRate = s.UsdRate;
        Actual = await econ.ActualAsync(y, mo, Calc.Assumptions.Deduction);
        Flex = EconomicsService.Flex(Calc, plan, Actual);
    }

    public async Task OnGetAsync(string? m)
    {
        var (y, mo) = Econ.ParseMonth(m, clock.Today);
        await LoadAsync(y, mo);
        var today = clock.Today;
        Form.Date = today.Year == y && today.Month == mo ? today : new DateOnly(y, mo, 1);
        Form.Rate = UsdRate.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public async Task<IActionResult> OnPostAddAsync(string? m)
    {
        var (y, mo) = Econ.ParseMonth(m, clock.Today);
        if (ModelState.IsValid && await TryAsync(async () =>
            {
                var e = new Expense();
                Form.ApplyTo(e);
                await econ.AddExpenseAsync(e, StaffId);
                FlashOk = L.T("Pnl_Econ_ExpenseAdded", e.Description);
                (y, mo) = (e.Date.Year, e.Date.Month);
            }))
            return RedirectToPage(new { m = $"{y:0000}-{mo:00}" });
        await LoadAsync(y, mo);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? m)
    {
        if (!await TryAsync(() => econ.DeleteExpenseAsync(id)))
            FlashError = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        else
            FlashOk = L.T("Pnl_Econ_ExpenseDeleted");
        return RedirectToPage(new { m });
    }

    public async Task<IActionResult> OnPostCopyAsync(string? m)
    {
        var (y, mo) = Econ.ParseMonth(m, clock.Today);
        int n = await econ.CopyPreviousMonthAsync(y, mo, StaffId);
        if (n > 0) FlashOk = L.T("Pnl_Econ_Copied", n);
        else FlashError = L.T("Pnl_Econ_NothingToCopy");
        return RedirectToPage(new { m = $"{y:0000}-{mo:00}" });
    }
}

public class ExpenseEditModel(ControlDbContext db, EconomicsService econ) : PanelPageModel
{
    public Expense Item { get; private set; } = null!;
    [BindProperty] public ExpenseForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await db.Expenses.FindAsync(id);
        if (e is null) return NotFound();
        Item = e;
        Form = ExpenseForm.From(e);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var e = await db.Expenses.FindAsync(id);
        if (e is null) return NotFound();
        Item = e;
        if (!ModelState.IsValid) return Page();
        var before = EconomicsService.Snapshot(e);
        if (!await TryAsync(async () => { Form.ApplyTo(e); await econ.UpdateExpenseAsync(e, before); }))
        {
            db.Entry(e).State = EntityState.Unchanged;
            return Page();
        }
        FlashOk = L.T("Pnl_SavedNamed", e.Description);
        return RedirectToPage("Expenses", new { m = $"{e.Date:yyyy-MM}" });
    }
}

// ================================================================================================ Sozlamalar (reja farazlari)

public class CostRow
{
    public int Id { get; set; }
    public PlannedCostKind Kind { get; set; } = PlannedCostKind.Other;
    public string? Name { get; set; }
    public int Count { get; set; } = 1;
    public string? Amount { get; set; }
    public CostCategory Category { get; set; } = CostCategory.Admin;
    public bool Delete { get; set; }
}

public class PackageRow
{
    public int PlanId { get; set; }
    public int PlannedTenants { get; set; }
    public decimal MarkupPercent { get; set; }
    public decimal TargetGrossMarginPercent { get; set; }
}

public class SettingsModel(ControlDbContext db, AuditLogger audit, Clock clock) : PanelPageModel
{
    public List<Plan> Plans { get; private set; } = [];
    public EconomicsSettings Current { get; private set; } = null!;

    [BindProperty] public string UsdRate { get; set; } = "";
    [BindProperty, Range(0, 100)] public decimal PayrollTaxPercent { get; set; }
    [BindProperty] public string ServerPrice { get; set; } = "";
    [BindProperty, Range(1, 1000)] public int FirmsPerServer { get; set; }
    [BindProperty, Range(0, 100)] public int MinServers { get; set; }
    [BindProperty] public string PerFirm { get; set; } = "";
    [BindProperty] public string Platform { get; set; } = "";
    [BindProperty] public string SupportSalary { get; set; } = "";
    [BindProperty, Range(1, 1000)] public int FirmsPerSupport { get; set; }
    [BindProperty, Range(0, 100)] public int MinSupport { get; set; }
    [BindProperty, Range(0, 99)] public decimal TurnoverTaxPercent { get; set; }
    [BindProperty, Range(0, 99)] public decimal BankFeePercent { get; set; }
    [BindProperty, Range(0, 99)] public decimal ProfitTaxPercent { get; set; }

    // Jadval formalari handler parametrlari orqali aniq prefiks bilan bog'lanadi: Dictionary/List xususiyat bo'lsa, prefiks topilmaganda
    // bog'lovchi bo'sh prefiksga qaytib, boshqa formaning maydonlarini (ServerPrice...) o'ziga olishga urinadi.
    public List<CostRow> Costs { get; set; } = [];
    public List<PackageRow> Packages { get; set; } = [];
    public Dictionary<string, int> Weights { get; set; } = [];

    private async Task LoadAsync(bool fillForms)
    {
        Current = await db.EconomicsSettings.SingleAsync(x => x.Id == 1);
        Plans = await db.Plans.AsNoTracking().Where(p => !p.IsAddOn && p.Code != PlanCodes.Trial).OrderBy(p => p.SortOrder).ToListAsync();
        if (!fillForms) return;
        var s = Current;
        UsdRate = s.UsdRate.ToString("0.##", CultureInfo.InvariantCulture);
        PayrollTaxPercent = s.PayrollTaxPercent;
        ServerPrice = Money.Format(s.ServerPriceTiyin); FirmsPerServer = s.FirmsPerServer; MinServers = s.MinServers;
        PerFirm = Money.Format(s.PerFirmTiyin); Platform = Money.Format(s.PlatformTiyin);
        SupportSalary = Money.Format(s.SupportSalaryTiyin); FirmsPerSupport = s.FirmsPerSupport; MinSupport = s.MinSupport;
        TurnoverTaxPercent = s.TurnoverTaxPercent; BankFeePercent = s.BankFeePercent; ProfitTaxPercent = s.ProfitTaxPercent;

        Costs = (await db.PlannedCosts.AsNoTracking().OrderBy(c => c.Kind).ThenBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync())
            .Select(c => new CostRow { Id = c.Id, Kind = c.Kind, Name = c.Name, Count = c.Count, Amount = Money.Format(c.AmountTiyin), Category = c.Category }).ToList();
        Costs.Add(new CostRow { Kind = PlannedCostKind.Staff, Category = CostCategory.Development });
        Costs.Add(new CostRow { Kind = PlannedCostKind.Other, Category = CostCategory.Admin });

        var econ = await db.PlanEconomics.AsNoTracking().ToDictionaryAsync(e => e.PlanId);
        Packages = Plans.Select(p => econ.TryGetValue(p.Id, out var e)
            ? new PackageRow { PlanId = p.Id, PlannedTenants = e.PlannedTenants, MarkupPercent = e.MarkupPercent, TargetGrossMarginPercent = e.TargetGrossMarginPercent }
            : new PackageRow { PlanId = p.Id, MarkupPercent = 40m, TargetGrossMarginPercent = 60m }).ToList();
        Weights = await db.ModuleWeights.AsNoTracking().OrderByDescending(w => w.Module == "Core").ThenBy(w => w.Module).ToDictionaryAsync(w => w.Module, w => w.Weight);
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    private long Parse(string text, string field)
    {
        if (Money.TryParse(text, out long t)) return t;
        ModelState.AddModelError(field, L.T("Pnl_Plan_AmountInvalid"));
        return 0;
    }

    public async Task<IActionResult> OnPostAssumptionsAsync()
    {
        await LoadAsync(false);
        decimal? rate = Econ.ParseDecimal(UsdRate);
        if (rate is null or <= 0) ModelState.AddModelError(nameof(UsdRate), L.T("Pnl_Econ_ErrRate"));
        long server = Parse(ServerPrice, nameof(ServerPrice)), perFirm = Parse(PerFirm, nameof(PerFirm)),
            platform = Parse(Platform, nameof(Platform)), support = Parse(SupportSalary, nameof(SupportSalary));
        if (TurnoverTaxPercent + BankFeePercent >= 100) ModelState.AddModelError(nameof(BankFeePercent), "< 100%");
        if (!ModelState.IsValid) { await LoadOthersAsync(); return Page(); }
        var s = Current;
        var before = Snapshot(s);
        s.UsdRate = rate!.Value; s.PayrollTaxPercent = PayrollTaxPercent;
        s.ServerPriceTiyin = server; s.FirmsPerServer = FirmsPerServer; s.MinServers = MinServers;
        s.PerFirmTiyin = perFirm; s.PlatformTiyin = platform;
        s.SupportSalaryTiyin = support; s.FirmsPerSupport = FirmsPerSupport; s.MinSupport = MinSupport;
        s.TurnoverTaxPercent = TurnoverTaxPercent; s.BankFeePercent = BankFeePercent; s.ProfitTaxPercent = ProfitTaxPercent;
        s.UpdatedAtUtc = clock.UtcNow;
        audit.Add("economics.settings", "EconomicsSettings", 1, before, Snapshot(s));
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Econ_Saved");
        return RedirectToPage();
    }

    /// <summary>Xato bilan qaytganda boshqa formalar (xarajatlar, paketlar) bazadan, shu forma maydonlari — foydalanuvchi kiritgani.</summary>
    private async Task LoadOthersAsync()
    {
        var keep = (UsdRate, PayrollTaxPercent, ServerPrice, FirmsPerServer, MinServers, PerFirm, Platform, SupportSalary, FirmsPerSupport, MinSupport, TurnoverTaxPercent, BankFeePercent, ProfitTaxPercent);
        await LoadAsync(true);
        (UsdRate, PayrollTaxPercent, ServerPrice, FirmsPerServer, MinServers, PerFirm, Platform, SupportSalary, FirmsPerSupport, MinSupport, TurnoverTaxPercent, BankFeePercent, ProfitTaxPercent) = keep;
    }

    private static object Snapshot(EconomicsSettings s) => new
    {
        s.UsdRate, s.PayrollTaxPercent, s.ServerPriceTiyin, s.FirmsPerServer, s.MinServers, s.PerFirmTiyin, s.PlatformTiyin,
        s.SupportSalaryTiyin, s.FirmsPerSupport, s.MinSupport, s.TurnoverTaxPercent, s.BankFeePercent, s.ProfitTaxPercent
    };

    public async Task<IActionResult> OnPostCostsAsync([FromForm(Name = "Costs")] List<CostRow> costs)
    {
        Costs = costs;
        var existing = await db.PlannedCosts.ToDictionaryAsync(c => c.Id);
        var errors = new List<string>();
        int order = 0;
        foreach (var row in Costs)
        {
            order++;
            bool blank = string.IsNullOrWhiteSpace(row.Name);
            existing.TryGetValue(row.Id, out var c);
            if (row.Delete || (blank && c is not null))
            {
                if (c is not null) { db.PlannedCosts.Remove(c); audit.Add("economics.cost-delete", "PlannedCost", c.Id, new { c.Name, c.AmountTiyin }); }
                continue;
            }
            if (blank) continue;
            if (!Money.TryParse(string.IsNullOrWhiteSpace(row.Amount) ? "0" : row.Amount, out long amount)) { errors.Add(L.T("Pnl_Econ_ErrCostAmount", row.Name!)); continue; }
            if (row.Category is CostCategory.Infra or CostCategory.Support) row.Category = CostCategory.Admin;
            if (c is null) { c = new PlannedCost(); db.PlannedCosts.Add(c); }
            c.Kind = row.Kind; c.Name = row.Name!.Trim(); c.Count = row.Kind == PlannedCostKind.Staff ? Math.Clamp(row.Count, 0, 1000) : 1;
            c.AmountTiyin = amount; c.Category = row.Category; c.SortOrder = order;
        }
        if (errors.Count > 0)
        {
            FlashError = string.Join("; ", errors);
            return RedirectToPage();
        }
        audit.Add("economics.costs", "PlannedCost", null, null, Costs.Where(r => !r.Delete && !string.IsNullOrWhiteSpace(r.Name)).Select(r => new { r.Name, r.Count, r.Amount, r.Category }));
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Econ_Saved");
        return RedirectToPage(null, null, "costs");
    }

    public async Task<IActionResult> OnPostPackagesAsync([FromForm(Name = "Packages")] List<PackageRow> packages, [FromForm(Name = "Weights")] Dictionary<string, int> weights)
    {
        Packages = packages;
        Weights = weights;
        var plans = await db.Plans.Where(p => !p.IsAddOn && p.Code != PlanCodes.Trial).Select(p => p.Id).ToListAsync();
        var econ = await db.PlanEconomics.ToDictionaryAsync(e => e.PlanId);
        foreach (var row in Packages.Where(r => plans.Contains(r.PlanId)))
        {
            if (!econ.TryGetValue(row.PlanId, out var e)) { e = new PlanEconomics { PlanId = row.PlanId }; db.PlanEconomics.Add(e); }
            e.PlannedTenants = Math.Clamp(row.PlannedTenants, 0, 100_000);
            e.MarkupPercent = Math.Clamp(row.MarkupPercent, 0, 1000);
            e.TargetGrossMarginPercent = Math.Clamp(row.TargetGrossMarginPercent, 0, 99);
        }
        var stored = await db.ModuleWeights.ToDictionaryAsync(w => w.Module);
        foreach (var (module, weight) in Weights)
            if (stored.TryGetValue(module, out var w)) w.Weight = Math.Clamp(weight, 0, 100);
        audit.Add("economics.packages", "PlanEconomics", null, null, new { Packages, Weights });
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Econ_Saved");
        return RedirectToPage(null, null, "packages");
    }
}

// ================================================================================================ Raqobatchilar

public class CompetitorRow
{
    public int Id { get; set; }
    public string? Company { get; set; }
    public string? Tariff { get; set; }
    public string? PriceSom { get; set; }
    public string? PriceUsd { get; set; }
    public int? Users { get; set; }
    public string? MatchPlanCode { get; set; }
    public bool Include { get; set; } = true;
    public Dictionary<string, string> Features { get; set; } = [];
    public string? Note { get; set; }
    public bool Delete { get; set; }
}

public class CompetitorsModel(ControlDbContext db, EconomicsService econ, AuditLogger audit) : PanelPageModel
{
    public List<Plan> Plans { get; private set; } = [];
    public List<Competitor> Items { get; private set; } = [];
    public EconPlanResult Plan { get; private set; } = null!;
    public decimal UsdRate { get; private set; }
    [BindProperty] public List<CompetitorRow> Rows { get; set; } = [];

    public async Task OnGetAsync()
    {
        Plans = await econ.SellablePlansAsync();
        Items = await db.Competitors.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        (_, Plan, var s) = await econ.PlanAsync();
        UsdRate = s.UsdRate;
        Rows = Items.Select(c => new CompetitorRow
        {
            Id = c.Id, Company = c.Company, Tariff = c.Tariff, PriceSom = Money.Format(c.PriceMonthTiyin),
            PriceUsd = c.PriceMonthUsd?.ToString("0.##", CultureInfo.InvariantCulture), Users = c.Users, MatchPlanCode = c.MatchPlanCode,
            Include = c.Include, Features = Econ.ParseFeatures(c.Features), Note = c.Note
        }).ToList();
        for (int i = 0; i < 3; i++) Rows.Add(new CompetitorRow());
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await db.Competitors.ToDictionaryAsync(c => c.Id);
        var errors = new List<string>();
        int order = 0;
        foreach (var row in Rows)
        {
            order++;
            existing.TryGetValue(row.Id, out var c);
            bool blank = string.IsNullOrWhiteSpace(row.Company);
            if (row.Delete || (blank && c is not null))
            {
                if (c is not null) { db.Competitors.Remove(c); audit.Add("economics.competitor-delete", "Competitor", c.Id, new { c.Company, c.Tariff }); }
                continue;
            }
            if (blank) continue;
            long? som = null;
            if (!string.IsNullOrWhiteSpace(row.PriceSom))
            {
                if (Money.TryParse(row.PriceSom, out long t)) som = t;
                else { errors.Add(L.T("Pnl_Econ_ErrCostAmount", row.Company!)); continue; }
            }
            decimal? usd = string.IsNullOrWhiteSpace(row.PriceUsd) ? null : Econ.ParseDecimal(row.PriceUsd);
            if (c is null) { c = new Competitor(); db.Competitors.Add(c); }
            c.Company = row.Company!.Trim(); c.Tariff = (row.Tariff ?? "").Trim(); c.PriceMonthTiyin = som; c.PriceMonthUsd = usd;
            c.Users = row.Users is > 0 ? row.Users : null; c.MatchPlanCode = string.IsNullOrWhiteSpace(row.MatchPlanCode) ? null : row.MatchPlanCode;
            c.Include = row.Include; c.Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim(); c.SortOrder = order;
            c.Features = string.Join(",", Econ.FeatureKeys.Where(k => row.Features.TryGetValue(k, out var v) && v is "1" or "~")
                .Select(k => row.Features[k] == "~" ? "~" + k : k));
        }
        if (errors.Count > 0) { FlashError = string.Join("; ", errors); return RedirectToPage(); }
        audit.Add("economics.competitors", "Competitor", null, null, new { Count = Rows.Count(r => !r.Delete && !string.IsNullOrWhiteSpace(r.Company)) });
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Econ_Saved");
        return RedirectToPage();
    }
}
