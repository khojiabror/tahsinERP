using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;

namespace tahsinERP.ControlPanel.Tests;

public class EconomicsCalculatorTests
{
    private const decimal Som = 100m; // tiyin

    /// <summary>Boshlang'ich reja (migratsiya seed'i) — tahsinERP_Tannarx_Narx_v2.xlsx, $ × 12 700.</summary>
    private static EconAssumptions Seed() => new(
        PayrollTaxPercent: 12m, ServerPrice: 508_000_00, FirmsPerServer: 8, MinServers: 1, PerFirm: 12_700_00, Platform: 25_400_00,
        SupportSalary: 6_350_000_00, FirmsPerSupport: 15, MinSupport: 1, TurnoverTaxPercent: 0m, BankFeePercent: 1m, ProfitTaxPercent: 15m,
        OpexDevelopment: 1_270_000 * Som, OpexSales: (6_350_000m * 1.12m + 3_810_000m) * Som, OpexAdmin: (381_000m + 1_270_000m) * Som);

    private static readonly Dictionary<string, int> Weights = new()
    {
        ["Core"] = 1, ["Srm"] = 2, ["Crm"] = 2, ["Tasks"] = 1, ["Mes"] = 3, ["Engineering"] = 2, ["Finance"] = 2,
        ["Hr"] = 2, ["Logistics"] = 2, ["Docs"] = 1, ["Distribution"] = 3
    };

    private static List<EconPackageInput> Packages(long? startPrice = null) =>
    [
        new(1, "START", "Start", 5, EconomicsCalculator.ModuleShare("Srm,Crm,Tasks".Split(','), Weights), 10, 40m, 45m, 12m, startPrice, null),
        new(6, "DIST", "Distributsiya", 20, EconomicsCalculator.ModuleShare("Srm,Crm,Tasks,Finance,Distribution".Split(','), Weights), 5, 40m, 60m, 12m, null, null),
        new(2, "BIZNES", "Biznes", 20, EconomicsCalculator.ModuleShare("Srm,Crm,Tasks,Mes,Engineering,Finance".Split(','), Weights), 3, 40m, 60m, 12m, null, null),
        new(3, "KORP", "Korporativ", 50, EconomicsCalculator.ModuleShare("Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution".Split(','), Weights), 2, 40m, 60m, 12m, null, null),
    ];

    [Fact]
    public void ModuleShare_CoreAlwaysIncluded()
    {
        Assert.Equal(6m / 21m, EconomicsCalculator.ModuleShare(["Srm", "Crm", "Tasks"], Weights));
        Assert.Equal(1m, EconomicsCalculator.ModuleShare("Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution".Split(','), Weights));
        Assert.Equal(1m / 21m, EconomicsCalculator.ModuleShare([], Weights));
    }

    [Fact]
    public void StepCosts_ServerAndSupportGrowWithCapacity()
    {
        var calc = new EconomicsCalculator(Seed());
        Assert.Equal((1, 1), (calc.At(0, 0).Servers, calc.At(0, 0).SupportStaff));   // minimal: mijoz bo'lmasa ham
        Assert.Equal((1, 1), (calc.At(8, 5).Servers, calc.At(8, 5).SupportStaff));
        Assert.Equal((2, 1), (calc.At(9, 5).Servers, calc.At(9, 5).SupportStaff));
        Assert.Equal((13, 7), (calc.At(100, 5).Servers, calc.At(100, 5).SupportStaff));
    }

    [Fact]
    public void Plan_MatchesExcelModel()
    {
        var plan = new EconomicsCalculator(Seed()).Plan(Packages());
        Assert.Equal(20, plan.PlannedFirms);
        Assert.Equal(15.5m, plan.AvgUsers);
        // Excel (v2): COGS $1 262, OPEX $1 090, jami $2 352 (20 firma) — so'mda × 12 700.
        Assert.Equal(16_027_400 * Som, plan.AtPlan.Cogs);
        Assert.Equal(13_843_000 * Som, plan.AtPlan.Opex);
        Assert.Equal(29_870_400 * Som, plan.AtPlan.Total);
        // 1 firma $117.60, 1 user $7.59.
        Assert.Equal(1_493_520 * Som, plan.AtPlan.PerFirm);
        Assert.Equal(96_356.13m, Math.Round(plan.AtPlan.PerUser / Som, 2));
        // START to'liq tannarxi $22.59 (Excel) ≈ 286 900 so'm.
        var start = plan.Packages.Single(p => p.Input.Code == "START");
        Assert.InRange(start.FullCost / Som, 286_800m, 287_000m);
        // Taqsimlash to'liq: Σ paket × firmalar = jami.
        Assert.Equal(Math.Round(plan.AtPlan.Total), Math.Round(plan.Packages.Sum(p => p.FullCost * p.Input.PlannedTenants)));
    }

    [Fact]
    public void PriceBuildUp_CostPlusTaxesAndVat()
    {
        var calc = new EconomicsCalculator(Seed());
        var r = calc.Plan(Packages()).Packages.Single(p => p.Input.Code == "START");
        decimal profit = r.FullCost * 0.40m;
        decimal ptax = profit / 0.85m - profit;
        decimal net = (r.FullCost + profit + ptax) / 0.99m;
        Assert.Equal(Math.Round(net, 4), Math.Round(r.CalcNet, 4));
        Assert.Equal(Math.Round(net * 1.12m, 4), Math.Round(r.CalcGross, 4));
        Assert.Equal(0, r.RecommendedGross % 1_000_000);               // 10 000 so'mga yaxlitlangan
        Assert.True(r.RecommendedGross >= r.CalcGross);
        // Tavsiya narxda sof foyda ≈ ustama (yaxlitlash hisobiga biroz ko'p).
        Assert.True(calc.NetProfitAfterTax(r, r.EffectiveNet) >= profit - 1);
        Assert.Equal(PriceHealth.Ok, calc.Health(r));
    }

    [Fact]
    public void Health_LossWhenTariffBelowCost()
    {
        var calc = new EconomicsCalculator(Seed());
        var r = calc.Plan(Packages(startPrice: 100_000 * 100)).Packages.Single(p => p.Input.Code == "START");
        Assert.Equal(100_000 * 100, r.EffectiveGross);
        Assert.Equal(PriceHealth.Loss, calc.Health(r));
    }

    [Fact]
    public void BreakEven_FirstProfitableVolume()
    {
        var calc = new EconomicsCalculator(Seed());
        var plan = calc.Plan(Packages());
        int be = Assert.IsType<int>(plan.BreakEvenFirms);
        Assert.True(calc.Profit(be, plan.AvgUsers, plan.AvgNetPrice) >= 0);
        Assert.True(calc.Profit(be - 1, plan.AvgUsers, plan.AvgNetPrice) < 0);
        Assert.Null(calc.BreakEven(plan.AvgUsers, 1_000 * 100)); // 1 000 so'mga hech qachon
    }

    [Fact]
    public void MarketPerUser_CompetitorMatchesSeveralPackages()
    {
        var comps = new List<Competitor>
        {
            new() { Company = "A", PriceMonthTiyin = 500_000_00, Users = 5, MatchPlanCodes = "DIST, SAVDO", Include = true },
            new() { Company = "B", PriceMonthTiyin = 300_000_00, Users = 1, MatchPlanCodes = "SAVDO", Include = true },
            new() { Company = "C", PriceMonthUsd = 10m, Users = 1, MatchPlanCodes = "START", Include = false },
        };
        var m = EconomicsService.MarketPerUser(comps, 12_700m, ["DIST", "SAVDO", "KORP"]);
        Assert.Equal(100_000_00m, m["DIST"]);
        Assert.Equal(200_000_00m, m["SAVDO"]);          // (100 000 + 300 000) / 2
        Assert.Equal(200_000_00m, m["KORP"]);           // mos yo'q — umumiy o'rtacha
        Assert.True(EconomicsService.Matches(comps[0], "savdo"));
    }

    [Fact]
    public void AllModules_CoveredWithoutAddOns()
    {
        var all = EconomicsService.AllModules().ToList();
        Assert.Contains("Core", all);
        Assert.Contains("Distribution", all);
        Assert.DoesNotContain("Website", all);
    }

    [Fact]
    public void ExpenseConversion_UsdToTiyin()
    {
        Assert.Equal(508_000_00, EconomicsService.ToTiyin(40m, "USD", 12_700m));
        Assert.Equal(150_000_50, EconomicsService.ToTiyin(150_000.5m, "UZS", 99m));
    }
}

public class EconomicsWebTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    [Fact]
    public async Task Owner_SeesPages_AddsExpense_ActualCounted_SalesDenied()
    {
        var (c, _) = await StaffLoginAsync(f, "econ-owner@test.uz", StaffRole.Owner);
        foreach (var url in new[] { "/Economics", "/Economics/Plan", "/Economics/Expenses", "/Economics/Competitors", "/Economics/Settings", "/Economics/Plan?sim=1,3,200" })
        {
            var r = await c.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.OK, url + ": " + r.StatusCode);
        }
        string plan = await (await c.GetAsync("/Economics/Plan")).Content.ReadAsStringAsync();
        Assert.Contains("29 870 400", plan); // jami xarajat rejadagi 20 firmada
        Assert.Contains("Bito ERP", await (await c.GetAsync("/Economics/Competitors")).Content.ReadAsStringAsync());

        // Xarajat: $40 server (kurs 12 700) — oktyabr 2026 (PanelFactory.Start).
        var add = await PostAsync(c, "/Economics/Expenses?m=2026-10&handler=Add", new()
        {
            ["Form.Date"] = "2026-10-05", ["Form.Category"] = "Infra", ["Form.Description"] = "AWS Lightsail oktyabr",
            ["Form.Amount"] = "40", ["Form.Currency"] = "USD", ["Form.Rate"] = "12700"
        }, "/Economics/Expenses?m=2026-10");
        Assert.True(add.StatusCode == HttpStatusCode.Redirect, Html(add));
        await PostAsync(c, "/Economics/Expenses?m=2026-10&handler=Add", new()
        {
            ["Form.Date"] = "2026-10-10", ["Form.Category"] = "Sales", ["Form.Description"] = "Reklama",
            ["Form.Amount"] = "1 000 000", ["Form.Currency"] = "UZS"
        }, "/Economics/Expenses?m=2026-10");

        // Pullik mijoz: START, oktyabrdan.
        await SetPricesAsync(f);
        var t = await f.CreateTenantAsync("econ-firma", trial: false);
        await PaidAsync(f, t.Id, PlanCodes.Start, 1, new DateOnly(2026, 10, 1));

        var actual = await f.WithScopeAsync(sp => sp.GetRequiredService<EconomicsService>().ActualAsync(2026, 10, 0.01m));
        Assert.Equal(1, actual.Tenants);
        Assert.Equal(508_000_00 + 1_000_000_00, actual.Total);
        Assert.Equal(508_000_00, actual.Cogs);
        decimal startGross = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Code == PlanCodes.Start).Select(p => p.PriceMonthTiyin!.Value).SingleAsync());
        Assert.Equal(Math.Round(startGross / 1.12m), Math.Round(actual.Mrr));
        Assert.Equal(Math.Round(startGross / 1.12m), Math.Round(actual.CashNet));
        string index = await (await c.GetAsync("/Economics?m=2026-10")).Content.ReadAsStringAsync();
        Assert.Contains("1 508 000", index);

        // Keyingi oyga nusxalash.
        var copy = await PostAsync(c, "/Economics/Expenses?m=2026-11&handler=Copy", new(), "/Economics/Expenses?m=2026-11");
        Assert.Equal(HttpStatusCode.Redirect, copy.StatusCode);
        Assert.Equal(2, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Expenses.CountAsync(e => e.Date.Month == 11)));

        // Tavsiya narxni tarifga qo'yish.
        int startId = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Code == PlanCodes.Start).Select(p => p.Id).SingleAsync());
        var apply = await PostAsync(c, "/Economics/Plan?handler=ApplyPrice", new() { ["planId"] = startId.ToString() }, "/Economics/Plan");
        Assert.Equal(HttpStatusCode.Redirect, apply.StatusCode);
        long applied = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Id == startId).Select(p => p.PriceMonthTiyin!.Value).SingleAsync());
        Assert.Equal(0, applied % 1_000_000);
        Assert.NotEqual((long)startGross, applied);

        // Sozlamalar: support maoshi o'zgaradi.
        var save = await PostAsync(c, "/Economics/Settings?handler=Assumptions", new()
        {
            ["UsdRate"] = "12800", ["PayrollTaxPercent"] = "12", ["ServerPrice"] = "508 000", ["FirmsPerServer"] = "10", ["MinServers"] = "1",
            ["PerFirm"] = "12 700", ["Platform"] = "25 400", ["SupportSalary"] = "7 000 000", ["FirmsPerSupport"] = "15", ["MinSupport"] = "1",
            ["TurnoverTaxPercent"] = "0", ["BankFeePercent"] = "1", ["ProfitTaxPercent"] = "15"
        }, "/Economics/Settings");
        Assert.True(save.StatusCode == HttpStatusCode.Redirect, Html(save));
        var s = await f.WithScopeAsync(sp => sp.GetRequiredService<EconomicsService>().SettingsAsync());
        Assert.Equal((7_000_000_00L, 10, 12800m), (s.SupportSalaryTiyin, s.FirmsPerServer, s.UsdRate));

        // Yangi paket va modul og'irligi bazada yo'q bo'lsa ham avtomatik ko'rinadi.
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            db.Plans.Add(new Plan { Code = "TESTPKG", Name = "Test paket", MaxUsers = 10, Modules = "Srm,Crm,Docs", VatRatePercent = 12m, SortOrder = 50 });
            db.ModuleWeights.Remove(await db.ModuleWeights.SingleAsync(w => w.Module == "Docs"));
            var bito = await db.Competitors.SingleAsync(c => c.Company == "Bito ERP" && c.Tariff == "Business");
            Assert.Equal("DIST,SAVDO", bito.MatchPlanCodes);
            await db.SaveChangesAsync();
        });
        var (_, withNew, _) = await f.WithScopeAsync(sp => sp.GetRequiredService<EconomicsService>().PlanAsync());
        var testPkg = Assert.Single(withNew.Packages, p => p.Input.Code == "TESTPKG");
        Assert.True(testPkg.Input.ModuleShare > 0);
        Assert.Equal(EconomicsService.DefaultModuleWeight, (await f.WithScopeAsync(sp => sp.GetRequiredService<EconomicsService>().WeightsAsync()))["Docs"]);
        Assert.Contains("TESTPKG", await (await c.GetAsync("/Economics/Plan")).Content.ReadAsStringAsync());
        string comp = await (await c.GetAsync("/Economics/Competitors")).Content.ReadAsStringAsync();
        Assert.Contains("value=\"TESTPKG\"", comp);
        string settings = await (await c.GetAsync("/Economics/Settings")).Content.ReadAsStringAsync();
        Assert.Contains("name=\"Weights[Docs]\"", settings);

        // Raqobatchini ikki paketga bog'lab saqlash (forma: Rows[0] — Bito Start).
        var compPost = await PostAsync(c, "/Economics/Competitors", new()
        {
            ["Rows[0].Id"] = "1", ["Rows[0].Company"] = "Bito ERP", ["Rows[0].Tariff"] = "Start", ["Rows[0].PriceSom"] = "290 000",
            ["Rows[0].Users"] = "3", ["Rows[0].MatchPlanCodes"] = "START", ["Rows[0].Include"] = "true",
        }, "/Economics/Competitors");
        Assert.Equal(HttpStatusCode.Redirect, compPost.StatusCode);

        // Sales xodimi — kirmaydi.
        var (sales, _) = await StaffLoginAsync(f, "econ-sales@test.uz", StaffRole.Sales);
        var denied = await sales.GetAsync("/Economics");
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
    }
}
