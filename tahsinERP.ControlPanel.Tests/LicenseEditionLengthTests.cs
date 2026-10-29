using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using Xunit;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>
/// Sinovda tanlangan modullar kalitning Edition qatoriga yoziladi; bog'liqliklar (Savdo -> Moliya, Distributsiya -> Savdo) bilan qator
/// uzun bo'ladi. Licenses.Edition nvarchar(20) edi — ERP'ni ulash (reg_token) va "Kalitni obunadan yangilash" 500 berardi.
/// </summary>
public class LicenseEditionLengthTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public LicenseEditionLengthTests(PanelFactory factory) => f = factory;

    [Fact]
    public async Task TrialWithManyModules_RegisterAndEnsure_IssueLongEdition()
    {
        var tenant = await f.CreateTenantAsync("edition-long");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.TenantId == tenant.Id);
            sub.Modules = LicenseIssuer.FormatModules(LicenseIssuer.ParseModules("Srm,Crm,Tasks,Distribution,Website"));
            await db.SaveChangesAsync();
        });
        const string expected = "TRIAL:Srm,Crm,Tasks,Finance,Commerce,Distribution+SITE";
        Assert.True(expected.Length > 50);   // ERP'dagi eski COMPANY_LICENSES.Edition (50) dan ham uzun

        // ERP ulanishi (reg_token): panel shu yerda kalit chiqaradi
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);

        var edition = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Licenses
            .Where(l => l.TenantId == tenant.Id).OrderByDescending(l => l.Id).Select(l => l.Edition).FirstAsync());
        Assert.Equal(expected, edition);

        // "Kalitni obunadan yangilash" — kalit mos, qayta chiqarilmaydi, xato ham yo'q
        var again = await f.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(x => x.Id == tenant.Id);
            return await sp.GetRequiredService<LicenseIssuer>().EnsureForTenantAsync(t);
        });
        Assert.Null(again);
    }
}
