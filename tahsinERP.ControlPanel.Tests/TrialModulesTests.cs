using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;
using tahsinERP.Services.Editions;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Sinovda tanlangan modullar: kalitdagi Edition qatori, agent argumentlari, SQL profil xaritasi (bazasiz qismlar).</summary>
public class TrialModulesUnitTests
{
    [Fact]
    public void ComposeModules_WritesSelectedList_AndErpReadsIt()
    {
        string edition = EditionModules.ComposeModules(EditionModules.Trial, [ProductModule.Crm, ProductModule.Srm, ProductModule.Website]);
        Assert.Equal("TRIAL:Srm,Crm+SITE", edition);
        Assert.Equal("TRIAL", EditionModules.BaseEdition(edition));
        Assert.True(EditionModules.IsRestrictedEdition(edition));
        Assert.Equal([ProductModule.Srm, ProductModule.Crm], EditionModules.SelectedModulesOf(edition));
        Assert.True(EditionModules.IsControllerAllowed(edition, "SOrder"));
        Assert.True(EditionModules.IsControllerAllowed(edition, "SiteHome"));
        Assert.True(EditionModules.IsControllerAllowed(edition, "Product"));      // Core har doim
        Assert.False(EditionModules.IsControllerAllowed(edition, "HrEmployees"));
        Assert.False(EditionModules.IsControllerAllowed(edition, "DstOrders"));
        Assert.Equal(["Srm", "Crm", "Website"], EditionModules.ModulesOf(edition).Select(m => m.ToString()));
        Assert.Equal("Sinov: Ta'minot (xarid va ombor), Sotuvlar (CRM) + Sayt-katalog", EditionModules.EditionDisplayName(edition));
    }

    [Fact]
    public void ComposeModules_AllOrNone_AndDependencies()
    {
        Assert.Equal("TRIAL", EditionModules.ComposeModules("TRIAL", EditionModules.PaidModules));       // hammasi — ro'yxatsiz
        Assert.Equal("TRIAL+SITE", EditionModules.ComposeModules("TRIAL", [.. EditionModules.PaidModules, ProductModule.Website]));
        Assert.Equal("TRIAL:+SITE", EditionModules.ComposeModules("TRIAL", [ProductModule.Website]));     // faqat xizmat
        Assert.False(EditionModules.IsModuleAllowed("TRIAL:+SITE", ProductModule.Srm));
        Assert.True(EditionModules.IsModuleAllowed("TRIAL:+SITE", ProductModule.Website));
        Assert.Equal("TRIAL:Mes,Engineering", EditionModules.ComposeModules("TRIAL", [ProductModule.Mes])); // MES -> Muhandislik
        Assert.Equal("START:Srm", EditionModules.ComposeModules("START", [ProductModule.Srm, ProductModule.Hr])); // paketdan kengaymaydi
        Assert.False(EditionModules.IsModuleAllowed("START:Srm,Hr", ProductModule.Hr));
        Assert.Throws<ArgumentException>(() => EditionModules.ComposeModules("TRIAL+SITE", [ProductModule.Srm]));
    }

    [Fact]
    public void Covers_And_Suggestions_UseSelectedModules()
    {
        Assert.True(EditionModules.Covers("START", "TRIAL:Srm,Crm"));
        Assert.False(EditionModules.Covers("START", "TRIAL:Srm,Mes,Engineering"));
        Assert.Equal("Biznes", EditionModules.SuggestEditionsFor("TRIAL:Srm,Crm,Mes,Engineering", ProductModule.Finance).Take(1).Select(EditionModules.EditionDisplayName).Single());
    }

    [Theory]
    [InlineData("TRIAL", new string[0], null, "TRIAL")]
    [InlineData("TRIAL", new string[0], "Crm,srm", "TRIAL:Srm,Crm")]
    [InlineData("TRIAL", new[] { "SITE" }, "Hr", "TRIAL:Hr+SITE")]
    [InlineData("TRIAL", new string[0], "Hr,Website", "TRIAL:Hr+SITE")]
    [InlineData("TRIAL", new[] { "SITE" }, "Hr,Website", "TRIAL:Hr+SITE")]                // takror xizmat — bitta
    [InlineData("TRIAL", new string[0], "nomalum, ,Core", "TRIAL")]                        // taniqli nom yo'q — paket hammasi
    public void LicenseIssuer_ComposeEdition_WithSubscriptionModules(string code, string[] addOns, string? modules, string expected) =>
        Assert.Equal(expected, LicenseIssuer.ComposeEdition(code, addOns, modules));

    [Fact]
    public void ParseFormatModules_NormalizeOrderAndDependencies()
    {
        Assert.Equal("Srm,Mes,Engineering,Website", LicenseIssuer.FormatModules(LicenseIssuer.ParseModules("website, mes ,SRM")));
        Assert.Null(LicenseIssuer.ParseModules(" "));
        Assert.Null(LicenseIssuer.FormatModules([]));
        // tahsinerp.uz demo so'rovidagi kodlar (DemoRequestService.ModuleCodes) ham taniladi
        Assert.Equal(DemoRequestService.ModuleCodes.Length + 1, LicenseIssuer.ParseModules(string.Join(",", DemoRequestService.ModuleCodes))!.Count);
    }

    [Fact]
    public void Agent_SetupModules_NormalizesAndRejectsUnknown()
    {
        Assert.Empty(JobExecutor.SetupModules(new JobPayload()));
        Assert.Equal(["Crm", "Mes", "Engineering"], JobExecutor.SetupModules(new JobPayload { Modules = ["mes", "Crm"] }));
        Assert.Throws<InvalidJobException>(() => JobExecutor.SetupModules(new JobPayload { Modules = ["Srm; DROP"] }));
        Assert.Throws<InvalidJobException>(() => JobExecutor.SetupModules(new JobPayload { Modules = ["Core"] }));
    }

    /// <summary>Setup/300_ModuleProfile.sql dagi controller -> modul xaritasi EditionModules bilan bir xil (rollar shundan quriladi).</summary>
    [Fact]
    public void ModuleProfileSql_ControllerMap_MatchesEditionModules()
    {
        string sql = File.ReadAllText(Path.Combine(RepoRoot(), "tahsinERP", "Database", "Setup", "300_ModuleProfile.sql"));
        var block = Regex.Match(sql, "-- @module-map-begin(.*?)-- @module-map-end", RegexOptions.Singleline);
        Assert.True(block.Success);
        var fromSql = Regex.Matches(block.Groups[1].Value, @"\('(\w+)',\s*'(\w+)'\)")
            .Select(m => (Module: m.Groups[1].Value, Controller: m.Groups[2].Value)).ToList();
        var expected = EditionModules.MappedControllers
            .Select(c => (Module: EditionModules.ModuleOf(c).ToString(), Controller: c))
            .Where(x => x.Module != nameof(ProductModule.Core)).ToList();
        Assert.Equal(expected.OrderBy(x => x.Controller, StringComparer.OrdinalIgnoreCase),
            fromSql.OrderBy(x => x.Controller, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Ruxsat matritsasidagi aniq bo'limlar ko'rsatilgan modulga tegishli (xato yozilgan controller jim o'tib ketmasin).</summary>
    [Fact]
    public void ModuleProfileSql_PermissionMatrix_ControllersBelongToTargetModule()
    {
        string sql = File.ReadAllText(Path.Combine(RepoRoot(), "tahsinERP", "Database", "Setup", "300_ModuleProfile.sql"));
        var block = Regex.Match(sql, @"INSERT INTO #matrix \(RoleModule, Target, Controller, Access\) VALUES(.*?);", RegexOptions.Singleline);
        Assert.True(block.Success);
        var rows = Regex.Matches(block.Groups[1].Value, @"\('([\w*]+)', '(\w+)', '([\w*]+)', '([CV])'\)").ToList();
        Assert.True(rows.Count > 50);
        var roleModules = new HashSet<string>();
        foreach (var r in rows)
        {
            string role = r.Groups[1].Value, target = r.Groups[2].Value, controller = r.Groups[3].Value;
            if (role != "*") roleModules.Add(role);
            if (controller == "*") { Assert.NotEqual("Core", target); continue; }
            Assert.True(EditionModules.ModuleOf(controller).ToString() == target, $"{role}: {controller} -> {EditionModules.ModuleOf(controller)}, matritsada {target}");
        }
        // har modul rolining o'z moduli — o'zgartirish (C)
        foreach (var m in roleModules)
            Assert.Contains(rows, r => r.Groups[1].Value == m && r.Groups[2].Value == m && r.Groups[3].Value == "*" && r.Groups[4].Value == "C");
        Assert.Equal(EditionModules.PaidModules.Concat(EditionModules.AddOnModules).Where(m => m != ProductModule.Tasks).Select(m => m.ToString()).OrderBy(x => x),
            roleModules.OrderBy(x => x));
    }

    /// <summary>New-TenantDb.ps1 va 300 profilidagi modul ro'yxati ProductModule bilan bir xil.</summary>
    [Fact]
    public void ScriptModuleLists_MatchProductModule()
    {
        string root = Path.Combine(RepoRoot(), "tahsinERP", "Database");
        var expected = EditionModules.PaidModules.Concat(EditionModules.AddOnModules).Select(m => m.ToString()).ToList();
        string ps = File.ReadAllText(Path.Combine(root, "tools", "New-TenantDb.ps1"));
        var psList = Regex.Match(ps, @"\$AllModules = @\(([^)]*)\)").Groups[1].Value;
        Assert.Equal(expected, Regex.Matches(psList, "'(\\w+)'").Select(m => m.Groups[1].Value));
        string profile = File.ReadAllText(Path.Combine(root, "Setup", "300_ModuleProfile.sql"));
        var sqlList = Regex.Match(profile, @"FROM \(VALUES ((?:\('\w+'\),?\s*)+)\) v \(M\)").Groups[1].Value;
        Assert.Equal(expected, Regex.Matches(sqlList, "'(\\w+)'").Select(m => m.Groups[1].Value));
        // har demo fayl (Core'dan tashqari) o'z modulini tekshiradi
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Demo"), "*.sql"))
        {
            var m = Regex.Match(Path.GetFileName(f), @"^\d{3}_([A-Za-z]+)_");
            Assert.True(m.Success, f);
            if (m.Groups[1].Value == "Core") continue;
            Assert.Contains(m.Groups[1].Value, expected);
            Assert.Contains($"dbo.fn_SetupHasModule('{m.Groups[1].Value}') = 0", File.ReadAllText(f));
        }
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("tahsinERP.sln topilmadi");
    }
}

/// <summary>Modullar bilan sinov: panel -> agent -> New-TenantDb.ps1 -Modules -> baza faqat tanlangan modullar bilan (lokal SQL).</summary>
public partial class ProvisioningIntegrationTests
{
    [Fact]
    public async Task TrialWithModules_DatabaseHasOnlySelectedModulesSetupAndSampleData()
    {
        var (sid, secret) = await ProvisioningQueueTests.CreateServerAsync(f, "it-srv-mod-" + _suffix);
        var (worker, _) = Agent(sid, secret);
        string slug = "itm-" + _suffix;
        string dbName = "DB_itm_" + _suffix;
        _databases.Add(dbName);

        var (tenant, enq) = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<TenantService>().CreateAsync(
                new Tenant { Slug = slug, LegalName = "\"Modul\" MChJ", Deployment = DeploymentKind.SaaS, ServerId = sid, SampleData = true, Modules = "Crm,Finance,Tasks" },
                startTrial: true);
            return (t, await sp.GetRequiredService<ProvisioningService>().EnqueueAsync(t, ProvisioningJobType.Create));
        });
        Assert.Contains("\"modules\":[\"Crm\",\"Tasks\",\"Finance\"]", (await JobAsync(enq.Job.Id)).Payload);
        await RunAsync(worker, enq.Job.Id);

        // kalit: obunadagi modullar Edition qatoriga
        var desired = await f.WithScopeAsync(async sp =>
        {
            var sub = await sp.GetRequiredService<ControlDbContext>().Subscriptions.Include(s => s.Plan).FirstAsync(s => s.TenantId == tenant.Id);
            return sp.GetRequiredService<LicenseIssuer>().DesiredFromSubscription(tenant, sub);
        });
        Assert.Equal("TRIAL:Crm,Tasks,Finance", desired!.Edition);

        // baza: tanlangan modullar yozilgan, profil va namuna faqat ular uchun
        Assert.Equal("Crm,Finance,Tasks", await ScalarAsync(dbName, "SELECT STRING_AGG(Module, ',') WITHIN GROUP (ORDER BY Module) FROM dbo.SETUP_MODULES"));
        Assert.NotEqual("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.SALES_INVOICES"));
        Assert.NotEqual("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.APP_TASKS WHERE IsDeleted = 0"));
        Assert.NotEqual("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.FIN_TRANSACTIONS WHERE SourceDocType = 'SalesInvoice'"));
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.P_INVOICES"));          // Srm yo'q
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.PRODUCT_PRODUCTION"));  // Mes yo'q
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dbo.HR_EMPLOYEES"));        // Hr yo'q
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT COUNT(*) FROM dst.D_ORDERS"));            // Distribution yo'q
        Assert.Null(await ScalarAsync(dbName, "SELECT name FROM sys.schemas WHERE name = 'demo_tmp'"));
        Assert.Equal("SALES", await ScalarAsync(dbName, "SELECT STRING_AGG(WarehouseType, ',') FROM dbo.WAREHOUSES WHERE ISNULL(IsDeleted, 0) = 0"));
        Assert.Equal("Hisobchi,Sotuv menejeri", await ScalarAsync(dbName,
            "SELECT STRING_AGG(RName, ',') WITHIN GROUP (ORDER BY RName) FROM dbo.ROLES WHERE SystemCode IS NULL AND ISNULL(IsDeleted, 0) = 0"));
        // modul rollari tanlanmagan modul controllerlariga huquq olmaydi
        Assert.Equal("0", await ScalarAsync(dbName, @"SELECT COUNT(*) FROM dbo.PERMISSIONS p JOIN dbo.ROLES r ON r.ID = p.RoleID AND r.SystemCode IS NULL
            JOIN dbo.PERMISSIONMODULES pm ON pm.ID = p.PermissionModuleID WHERE pm.Controller IN ('HrEmployees', 'ProductProduction', 'PInvoices', 'DstOrders', 'Waybill')"));
        // ko'rish / o'zgartirish matritsasi: o'z moduli C, qo'shni bo'lim V, spravochnik V, o'chirish — hech kimga
        async Task<string?> Access(string role, string controller) => await ScalarAsync(dbName, $@"SELECT CASE WHEN COUNT(*) = 0 THEN 'none' WHEN MIN(CAST(p.ViewPermit AS int)) = 0 THEN '-'
                WHEN MAX(CAST(p.ChangePermit AS int)) = 1 THEN 'C' ELSE 'V' END
            FROM dbo.PERMISSIONS p JOIN dbo.ROLES r ON r.ID = p.RoleID AND r.RName = N'{role}'
            JOIN dbo.PERMISSIONMODULES pm ON pm.ID = p.PermissionModuleID WHERE pm.Controller = '{controller}'");
        Assert.Equal("C", await Access("Sotuv menejeri", "SOrder"));
        Assert.Equal("C", await Access("Sotuv menejeri", "Tasks"));
        Assert.Equal("V", await Access("Sotuv menejeri", "TaskStatus"));
        Assert.Equal("V", await Access("Sotuv menejeri", "Product"));
        Assert.Equal("V", await Access("Sotuv menejeri", "FinTransactions"));
        Assert.Equal("none", await Access("Sotuv menejeri", "FinAccounts"));
        Assert.Equal("C", await Access("Hisobchi", "FinTransactions"));
        Assert.Equal("V", await Access("Hisobchi", "SalesInvoices"));
        Assert.Equal("none", await Access("Hisobchi", "SOrder"));
        Assert.Equal("0", await ScalarAsync(dbName,
            "SELECT COUNT(*) FROM dbo.PERMISSIONS p JOIN dbo.ROLES r ON r.ID = p.RoleID WHERE r.SystemCode IS NULL AND p.DeletePermit = 1"));
        Assert.Equal("0", await ScalarAsync(dbName, "SELECT CAST(IsActive AS int) FROM dbo.USERS WHERE Uname = 'demo'"));
    }
}
