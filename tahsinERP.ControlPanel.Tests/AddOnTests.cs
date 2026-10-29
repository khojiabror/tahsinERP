using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Services.Editions;
using tahsinERP.Services.LicenseSync;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Qo'shimcha xizmat «Sayt-katalog» (SITE): kalitdagi Edition qatori, alohida obuna va billing (bazasiz qismlar).</summary>
public class AddOnUnitTests
{
    [Theory]
    [InlineData("KORP", new string[0], "KORP")]
    [InlineData("KORP", new[] { "SITE" }, "KORP+SITE")]
    [InlineData("START", new[] { "site", " SITE ", "SITE" }, "START+SITE")] // takror va registr — bitta
    [InlineData("TRIAL", new[] { "", "SITE" }, "TRIAL+SITE")]
    public void ComposeEdition_BuildsStableKeyLine(string baseCode, string[] addOns, string expected) =>
        Assert.Equal(expected, LicenseIssuer.ComposeEdition(baseCode, addOns));

    [Theory]
    [InlineData("KORP", "NOMALUM")]
    [InlineData("KORP", "SITE+X")]
    [InlineData("KORP+SITE", "SITE")]
    [InlineData(" ", "SITE")]
    public void ComposeEdition_RejectsUnknownOrMalformed(string baseCode, string addOn) =>
        Assert.Throws<DomainException>(() => LicenseIssuer.ComposeEdition(baseCode, [addOn]));

    [Fact]
    public void ComposedEdition_IsReadByErpEditionMap()
    {
        string edition = LicenseIssuer.ComposeEdition(PlanCodes.Korp, [PlanCodes.Site]);
        Assert.Equal("KORP", EditionModules.BaseEdition(edition));
        Assert.Equal([ProductModule.Website], EditionModules.AddOnsOf(edition));
        Assert.True(EditionModules.IsAddOn(ProductModule.Website));
        Assert.True(EditionModules.IsModuleAllowed(edition, ProductModule.Website));
        Assert.True(EditionModules.IsControllerAllowed(edition, "SiteHome"));
        Assert.True(EditionModules.IsModuleAllowed(edition, ProductModule.Distribution)); // paket modullari saqlanadi
        Assert.False(EditionModules.IsModuleAllowed("KORP", ProductModule.Website));       // xizmatsiz — yopiq
        Assert.False(EditionModules.IsModuleAllowed("START+SITE", ProductModule.Mes));
        Assert.Equal("Korporativ + Sayt-katalog", EditionModules.EditionDisplayName(edition));
    }

    [Fact]
    public void ModulesOf_IncludesAddOns_ForTractionCoverage()
    {
        Assert.Equal(11, EditionModules.ModulesOf("KORP").Count);   // Savdo (Commerce) + Distributsiya alohida
        Assert.Equal(12, EditionModules.ModulesOf("KORP+SITE").Count);
        Assert.Equal(["Srm", "Crm", "Tasks", "Website"], EditionModules.ModulesOf("START+SITE").Select(m => m.ToString()));
        Assert.DoesNotContain(ProductModule.Website, EditionModules.ModulesOf("TRIAL"));
        Assert.Contains(ProductModule.Website, EditionModules.ModulesOf("TRIAL+SITE"));
    }

    private static Plan Site => new() { Id = 5, Code = PlanCodes.Site, Name = "Sayt-katalog", IsAddOn = true, PriceMonthTiyin = 50_000_000, PriceYearTiyin = 500_000_000, VatRatePercent = 12m };

    [Fact]
    public void DefaultLine_ForAddOn_HasNoUserCount()
    {
        var sub = new Subscription { Plan = Site, Users = 0, Period = BillingPeriod.Year };
        var line = BillingService.DefaultLine(sub, Site, 1);
        Assert.Equal("tahsinERP «Sayt-katalog» qo'shimcha xizmati — 1 yil", line.Description);
        Assert.Equal(500_000_000, line.UnitPriceTiyin);
        var noPrice = Site;
        noPrice.PriceMonthTiyin = null;
        var ex = Assert.Throws<DomainException>(() => BillingService.DefaultLine(new Subscription { Plan = noPrice, Period = BillingPeriod.Month }, noPrice, 1));
        Assert.Contains("xizmatining oylik narxi", ex.Message);
    }

    [Fact]
    public void AddOnStatus_States()
    {
        var today = new DateOnly(2026, 10, 18);
        Subscription S(SubscriptionStatus st, string from, string to, int id = 1) =>
            new() { Id = id, Plan = Site, PlanId = 5, Status = st, StartDate = DateOnly.Parse(from), EndDate = DateOnly.Parse(to), Period = BillingPeriod.Month };
        Assert.Equal(AddOnState.NotConnected, PortalRequestService.AddOnStatusOf(Site, [], today).State);
        Assert.Equal(AddOnState.NotConnected, PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Cancelled, "2026-10-18", "2026-10-17")], today).State);
        Assert.Equal(AddOnState.PendingPayment, PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Pending, "2026-10-18", "2026-10-17")], today).State);
        var active = PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Active, "2026-10-01", "2026-10-31")], today);
        Assert.Equal(AddOnState.Active, active.State);
        Assert.Equal(13, active.DaysLeft);
        Assert.Equal(AddOnState.Upcoming, PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Active, "2026-11-01", "2026-11-30")], today).State);
        Assert.Equal(AddOnState.Expired, PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Active, "2026-09-01", "2026-10-17")], today).State);
        Assert.Equal(AddOnState.Expired, PortalRequestService.AddOnStatusOf(Site, [S(SubscriptionStatus.Expired, "2026-09-01", "2026-09-30")], today).State);
        // Faol bo'lsa — keyingi to'lanmagan obuna holatni buzmaydi.
        Assert.Equal(AddOnState.Active, PortalRequestService.AddOnStatusOf(Site,
            [S(SubscriptionStatus.Active, "2026-10-01", "2026-10-31"), S(SubscriptionStatus.Pending, "2026-11-01", "2026-10-31", 2)], today).State);
    }
}

/// <summary>Qo'shimcha xizmat: obuna → hisob-faktura → to'lov → "+SITE" kalit → ERP pull; muddati tugashi; portal va Traction.</summary>
public class AddOnTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public AddOnTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    private const long KorpMonth = 300_000_000, SiteMonth = 50_000_000;
    private static readonly DateOnly Today = new(2026, 10, 18);

    private Task SetPricesAsync() => f.WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        (await db.Plans.SingleAsync(p => p.Code == PlanCodes.Korp)).PriceMonthTiyin = KorpMonth;
        (await db.Plans.SingleAsync(p => p.Code == PlanCodes.Site)).PriceMonthTiyin = SiteMonth;
        await db.SaveChangesAsync();
    });

    private async Task<(Tenant Tenant, string Inst)> RegisteredTenantAsync(string slug, DeploymentKind deployment = DeploymentKind.SaaS)
    {
        var tenant = await f.CreateTenantAsync(slug, deployment: deployment);
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        return (tenant, inst);
    }

    /// <summary>Obuna (paket yoki xizmat) + hisob-faktura + to'liq to'lov.</summary>
    private Task<(Subscription Sub, PaymentResult Paid)> BuyAsync(int tenantId, string planCode, int months, DateOnly start) => f.WithScopeAsync(async sp =>
    {
        int staff = await f.EnsureStaffAsync();
        var db = sp.GetRequiredService<ControlDbContext>();
        var t = await db.Tenants.FirstAsync(x => x.Id == tenantId);
        var plan = await db.Plans.SingleAsync(p => p.Code == planCode);
        var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Month, null, start, false);
        var paid = await PayAsync(sp, tenantId, sub.Id, months, staff);
        return (sub, paid);
    });

    private static async Task<PaymentResult> PayAsync(IServiceProvider sp, int tenantId, int subId, int months, int staff)
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstAsync(s => s.Id == subId);
        var billing = sp.GetRequiredService<BillingService>();
        var inv = await billing.CreateDraftAsync(tenantId, sub.Id, months, [BillingService.DefaultLine(sub, sub.Plan!, months)], null, staff);
        await billing.IssueAsync(inv);
        return await billing.RecordPaymentAsync(inv.Id, inv.TotalTiyin, sp.GetRequiredService<Clock>().Today, PaymentMethod.Bank, "PT-S", null, staff);
    }

    private async Task<string> TenantSecretAsync(int tenantId) => await f.WithScopeAsync(async sp =>
    {
        var t = await sp.GetRequiredService<ControlDbContext>().Tenants.FirstAsync(x => x.Id == tenantId);
        return sp.GetRequiredService<Security.ISecretProtector>().Unprotect(t.ApiSecretProtected!, Security.SecretPurposes.TenantApiSecret);
    });

    [Fact]
    public async Task SitePlan_IsSeeded_AsAddOn()
    {
        await f.WithScopeAsync(async sp =>
        {
            var site = await sp.GetRequiredService<ControlDbContext>().Plans.SingleAsync(p => p.Code == PlanCodes.Site);
            Assert.True(site.IsAddOn);
            Assert.Equal(0, site.MaxUsers);
            Assert.Equal("Website", site.Modules);
            Assert.Equal([ProductModule.Website], EditionModules.AddOnsOf("X+" + site.Code)); // kod ERP xaritasidagi bilan bir xil
        });
    }

    [Fact]
    public async Task SitePaidSeparately_KeyGetsPlusSite_PackageUntouched_PullDelivers()
    {
        await SetPricesAsync();
        int staff = await f.EnsureStaffAsync();
        var (tenant, inst) = await RegisteredTenantAsync("site-flow");
        var (korp, korpPaid) = await BuyAsync(tenant.Id, PlanCodes.Korp, 1, Today);
        Assert.Equal("KORP", korpPaid.IssuedLicense!.Edition);

        // Xizmat obunasi: alohida qator, foydalanuvchi soni yo'q, o'z narxi.
        var (site, sitePaid) = await BuyAsync(tenant.Id, PlanCodes.Site, 1, Today);
        Assert.Equal(0, site.Users);
        Assert.Equal(SiteMonth, sitePaid.Invoice.TotalTiyin);
        Assert.Null(sitePaid.LicenseWarning);
        var key = sitePaid.IssuedLicense!;
        Assert.Equal("KORP+SITE", key.Edition);
        Assert.Equal(50, key.MaxUsers);                          // paketdan
        Assert.Equal(new DateOnly(2026, 11, 20), key.ExpiryDate); // paket davri oxiri + 3 kun

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var k = await db.Subscriptions.FirstAsync(s => s.Id == korp.Id);
            Assert.Equal(SubscriptionStatus.Active, k.Status);   // paket obunasi yopilmadi
            Assert.Equal(new DateOnly(2026, 11, 17), k.EndDate);
            var s = await db.Subscriptions.FirstAsync(x => x.Id == site.Id);
            Assert.Equal(SubscriptionStatus.Active, s.Status);
            Assert.Equal(new DateOnly(2026, 11, 17), s.EndDate);
            Assert.Equal(TenantStatus.Active, (await db.Tenants.FirstAsync(t => t.Id == tenant.Id)).Status);
            Assert.Contains("qo'shimcha xizmati", (await db.InvoiceLines.FirstAsync(l => l.InvoiceId == sitePaid.Invoice.Id)).Description);
            // Mos kalit bor — qayta chiqarilmaydi.
            Assert.Null(await sp.GetRequiredService<LicenseIssuer>().EnsureForTenantAsync(await db.Tenants.FirstAsync(t => t.Id == tenant.Id)));
        });

        // ERP: pull → KORP+SITE kaliti; ERP uni o'z xaritasi bilan o'qiydi.
        var pull = await f.ErpClient().PullAsync(inst, await TenantSecretAsync(tenant.Id), korpPaid.IssuedLicense.ETag);
        Assert.Equal(LicensePanelClient.PullStatus.Updated, pull.Status);
        Assert.True(ErpLicense.TryParseKey(pull.LicenseKey, f.ErpTrusted, out var info));
        Assert.True(info.IsBoundTo(inst));
        Assert.Equal("KORP+SITE", info.Edition);
        Assert.True(EditionModules.IsControllerAllowed(info.Edition, "SiteCatalog"));
        Assert.True(EditionModules.IsControllerAllowed(info.Edition, "HrPayroll"));

        // Ikkinchi xizmat obunasi ochilmaydi — uzaytirish mavjud obunaga hisob-faktura orqali.
        var dup = await Assert.ThrowsAsync<DomainException>(() => f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            await sp.GetRequiredService<SubscriptionService>().CreateAsync(await db.Tenants.FirstAsync(t => t.Id == tenant.Id),
                (await db.Plans.SingleAsync(p => p.Code == PlanCodes.Site)).Id, BillingPeriod.Month, null, Today, false);
        }));
        Assert.Contains("faol", dup.Message);

        var renew = await f.WithScopeAsync(sp => PayAsync(sp, tenant.Id, site.Id, 2, staff));
        Assert.Null(renew.IssuedLicense); // Edition va muddat o'zgarmadi (SaaS kalit paket davriga bog'liq)
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.Equal(new DateOnly(2027, 1, 17), (await db.Subscriptions.FirstAsync(x => x.Id == site.Id)).EndDate);
            Assert.Equal(new DateOnly(2026, 11, 17), (await db.Subscriptions.FirstAsync(x => x.Id == korp.Id)).EndDate);
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "subscription.extend" && a.EntityId == site.Id.ToString()));
        });
    }

    [Fact]
    public async Task AddOnRules_PendingSeparateFromPackage_PlanCannotBeSwapped()
    {
        await SetPricesAsync();
        var tenant = await f.CreateTenantAsync("site-rules");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var subs = sp.GetRequiredService<SubscriptionService>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var site = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Site);
            var korp = await db.Plans.SingleAsync(p => p.Code == PlanCodes.Korp);

            var sitePending = await subs.CreateAsync(t, site.Id, BillingPeriod.Year, users: 7, Today, false);
            Assert.Equal(SubscriptionStatus.Pending, sitePending.Status);
            Assert.Equal(0, sitePending.Users); // foydalanuvchi soni e'tiborga olinmaydi
            // To'lanmagan xizmat paket obunasini to'smaydi (va aksincha).
            var korpPending = await subs.CreateAsync(t, korp.Id, BillingPeriod.Month, null, Today, false);
            Assert.Equal(50, korpPending.Users);
            await Assert.ThrowsAsync<DomainException>(() => subs.CreateAsync(t, korp.Id, BillingPeriod.Month, null, Today, false));
            await Assert.ThrowsAsync<DomainException>(() => subs.CreateAsync(t, site.Id, BillingPeriod.Month, null, Today, false));

            // Xizmatni paketga (yoki paketni xizmatga) aylantirib bo'lmaydi.
            await Assert.ThrowsAsync<DomainException>(() => subs.UpdateAsync(sitePending, korp.Id, 50, false));
            await Assert.ThrowsAsync<DomainException>(() => subs.UpdateAsync(korpPending, site.Id, 0, false));
            await subs.UpdateAsync(sitePending, site.Id, 99, autoRenew: true);
            Assert.Equal(0, sitePending.Users);
            Assert.True(sitePending.AutoRenew);

            // Portal so'rovi faqat paketlar uchun.
            var pu = await sp.GetRequiredService<PortalAuthService>().CreateUserAsync(t.Id, "Mijoz", "rules@mijoz.uz", null, PortalRole.Owner, await f.EnsureStaffAsync());
            await Assert.ThrowsAsync<DomainException>(() => sp.GetRequiredService<PortalRequestService>()
                .CreateAsync(t.Id, pu.Id, site.Id, BillingPeriod.Month, 1, null));
        });
    }

    [Fact]
    public async Task SiteOnTrialTenant_DoesNotConvertTrial_KeyIsTrialPlusSite_Traction()
    {
        await SetPricesAsync();
        var (tenant, _) = await RegisteredTenantAsync("site-trial");
        var (site, paid) = await BuyAsync(tenant.Id, PlanCodes.Site, 1, Today);
        Assert.Equal("TRIAL+SITE", paid.IssuedLicense!.Edition);
        Assert.Equal(new DateOnly(2026, 10, 31), paid.IssuedLicense.ExpiryDate); // sinov oxiri — xizmat uni uzaytirmaydi

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            Assert.Equal(TenantStatus.Trial, t.Status); // xizmat to'lovi tenantni faollashtirmaydi
            Assert.Equal(SubscriptionStatus.Active, (await db.Subscriptions.FirstAsync(s => s.TenantId == t.Id && s.Period == BillingPeriod.Trial)).Status);
            await sp.GetRequiredService<SubscriptionService>().RunLifecycleAsync();
            Assert.Equal(TenantStatus.Trial, (await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == tenant.Id)).Status);

            var ov = await sp.GetRequiredService<PortalRequestService>().OverviewAsync(t.Id);
            Assert.Equal(PlanCodes.Trial, ov.Current!.Plan!.Code); // paket — sinov, xizmat emas
            Assert.Null(ov.Pending);
            var a = Assert.Single(ov.AddOns);
            Assert.Equal(AddOnState.Active, a.State);
            Assert.Equal(site.Id, a.Subscription!.Id);

            var traction = sp.GetRequiredService<TractionService>();
            var all = await traction.BuildAsync(new TractionService.Filter(new DateOnly(2026, 10, 1), Today, null, null));
            var row = all.Tenants.Single(r => r.Tenant.Id == t.Id);
            Assert.Equal("TRIAL+SITE", row.Edition);
            Assert.Equal(PlanCodes.Trial, row.PlanCode);
            Assert.Equal(12, row.ModulesAllowed);
            Assert.Contains("Website", row.UnusedModules);
            Assert.Contains("Sayt-katalog", row.Plan);
            Assert.Contains(all.Modules, m => m.Module == "Website" && m.UnusedInPackage >= 1);
            Assert.Equal(SiteMonth, row.MrrTiyin);
            Assert.True(all.Overview.AddOnMrrTiyin >= SiteMonth);
            Assert.True(all.Overview.AddOnTenants >= 1);
            Assert.True(all.Overview.TrialsStarted >= 1);
            // Faqat xizmat sotib olish — konversiya emas (boshqa testlar tenantlari KORP to'lagan — sinovdagilar bilan cheklaymiz).
            var trials = await traction.BuildAsync(new TractionService.Filter(new DateOnly(2026, 10, 1), Today, null, TenantStatus.Trial));
            Assert.Contains(trials.Tenants, r => r.Tenant.Id == t.Id);
            Assert.Equal(0, trials.Overview.TrialsConverted);

            var siteOnly = await traction.BuildAsync(new TractionService.Filter(new DateOnly(2026, 10, 1), Today, "SITE", null));
            Assert.Contains(siteOnly.Tenants, r => r.Tenant.Id == t.Id);
            Assert.All(siteOnly.Tenants, r => Assert.Contains("+SITE", r.Edition));
            var trialOnly = await traction.BuildAsync(new TractionService.Filter(new DateOnly(2026, 10, 1), Today, "TRIAL", null));
            Assert.Contains(trialOnly.Tenants, r => r.Tenant.Id == t.Id);
        });
    }

    [Fact]
    public async Task SiteExpires_KeyLosesPlusSite_TenantStaysActive_Reminders()
    {
        await SetPricesAsync();
        var (tenant, _) = await RegisteredTenantAsync("site-expiry");
        var (korp, _) = await BuyAsync(tenant.Id, PlanCodes.Korp, 3, Today);
        var (site, paid) = await BuyAsync(tenant.Id, PlanCodes.Site, 1, Today);
        Assert.Equal("KORP+SITE", paid.IssuedLicense!.Edition);
        try
        {
            // 14 kun qolganda: xizmat eslatmasi paketnikidan alohida (paket 17.01 da tugaydi — unga eslatma yo'q).
            f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 3, 5, 0, 0, TimeSpan.Zero));
            await f.WithScopeAsync(async sp =>
            {
                Assert.True(await sp.GetRequiredService<ExpiryReminderService>().RunAsync() >= 1);
                var db = sp.GetRequiredService<ControlDbContext>();
                var n = await db.Notifications.Where(x => x.TenantId == tenant.Id).ToListAsync();
                Assert.Contains(n, x => x.Template == "site-expiry-14" && x.Message.Contains("«Sayt-katalog» qo'shimcha xizmati"));
                Assert.DoesNotContain(n, x => x.Template == "expiry-14");
            });

            // Xizmat davri tugadi (17.11), paket davom etadi: kunlik ish kalitdan "+SITE" ni olib tashlaydi.
            f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 19, 5, 0, 0, TimeSpan.Zero));
            await f.WithScopeAsync(async sp =>
            {
                Assert.True(await DailyJobHostedService.EnsureKeysForStartedPeriodsAsync(sp, NullLogger.Instance, default) >= 1);
                var db = sp.GetRequiredService<ControlDbContext>();
                var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
                var current = await sp.GetRequiredService<LicenseIssuer>().GetCurrentAsync(t);
                Assert.Equal("KORP", current!.Edition);
                Assert.Equal(new DateOnly(2027, 1, 20), current.ExpiryDate);
                Assert.False(EditionModules.IsModuleAllowed(current.Edition, ProductModule.Website));
                var ov = await sp.GetRequiredService<PortalRequestService>().OverviewAsync(t.Id);
                Assert.Equal(AddOnState.Expired, ov.AddOns.Single().State);
                Assert.Equal(korp.Id, ov.Current!.Id);
            });

            // Imtiyozli kunlardan keyin: xizmat obunasi Expired, tenant baribir Active (paket to'langan).
            f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 25, 5, 0, 0, TimeSpan.Zero));
            await f.WithScopeAsync(async sp =>
            {
                await sp.GetRequiredService<SubscriptionService>().RunLifecycleAsync();
                var db = sp.GetRequiredService<ControlDbContext>();
                Assert.Equal(SubscriptionStatus.Expired, (await db.Subscriptions.FirstAsync(s => s.Id == site.Id)).Status);
                Assert.Equal(SubscriptionStatus.Active, (await db.Subscriptions.FirstAsync(s => s.Id == korp.Id)).Status);
                Assert.Equal(TenantStatus.Active, (await db.Tenants.FirstAsync(x => x.Id == tenant.Id)).Status);
            });
        }
        finally
        {
            f.Time.SetUtcNow(PanelFactory.Start);
        }
    }

    [Fact]
    public async Task OnPrem_KeyWithSite_DoesNotOutliveAddOnPeriod()
    {
        await f.WithScopeAsync(sp =>
        {
            var issuer = sp.GetRequiredService<LicenseIssuer>();
            var tenant = new Tenant { Id = 1, Deployment = DeploymentKind.OnPrem };
            var korp = new Plan { Code = PlanCodes.Korp, MaxUsers = 50 };
            var site = new Plan { Code = PlanCodes.Site, IsAddOn = true };
            var pkg = new Subscription { Id = 10, Plan = korp, Users = 50, Period = BillingPeriod.Year, Status = SubscriptionStatus.Active, StartDate = Today, EndDate = new DateOnly(2027, 10, 17) };
            var addOn = new Subscription { Id = 11, Plan = site, Period = BillingPeriod.Month, Status = SubscriptionStatus.Active, StartDate = Today, EndDate = new DateOnly(2026, 12, 17) };
            var withSite = issuer.DesiredFromSubscription(tenant, pkg, [addOn])!;
            Assert.Equal("KORP+SITE", withSite.Edition);
            Assert.Equal(new DateOnly(2026, 12, 17), withSite.ExpiryDate); // offline kalit — xizmat davridan oshmaydi
            var withoutSite = issuer.DesiredFromSubscription(tenant, pkg, [])!;
            Assert.Equal("KORP", withoutSite.Edition);
            Assert.Equal(new DateOnly(2027, 10, 17), withoutSite.ExpiryDate);
            // SaaS — kalit paket davriga bog'liq (pull orqali almashadi).
            var saas = issuer.DesiredFromSubscription(new Tenant { Id = 2, Deployment = DeploymentKind.SaaS }, pkg, [addOn])!;
            Assert.Equal(new DateOnly(2027, 10, 20), saas.ExpiryDate);
            // Boshlanmagan yoki to'lanmagan xizmat kalitga kirmaydi.
            addOn.StartDate = Today.AddDays(1);
            Assert.Equal("KORP", issuer.DesiredFromSubscription(tenant, pkg, [addOn])!.Edition);
            addOn.StartDate = Today; addOn.Status = SubscriptionStatus.Pending;
            Assert.Equal("KORP", issuer.DesiredFromSubscription(tenant, pkg, [addOn])!.Edition);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PortalAndStaffPages_ShowAddOn()
    {
        await SetPricesAsync();
        var (tenant, _) = await RegisteredTenantAsync("site-portal", DeploymentKind.OnPrem); // «Kalitlar» sahifasi — faqat on-prem
        await BuyAsync(tenant.Id, PlanCodes.Korp, 1, Today);
        await BuyAsync(tenant.Id, PlanCodes.Site, 1, Today);

        var (_, token) = await InviteAsync(f, tenant.Id, "site@mijoz.uz");
        await AcceptInviteAsync(f, token, linkChat: "7301");
        var (my, _) = await LoginAsync(f, "site@mijoz.uz", "7301");
        string home = Html(await my.GetAsync("/"));
        Assert.Contains("Qo'shimcha xizmatlar", home);
        Assert.Contains("Sayt-katalog", home);
        Assert.Contains("Faol", home);
        Assert.Contains("18.10.2026 – 17.11.2026", home);
        string licenses = Html(await my.GetAsync("/Licenses"));
        Assert.Contains("Korporativ + Sayt-katalog", licenses);
        string requests = Html(await my.GetAsync("/Requests"));
        Assert.DoesNotContain("Sayt-katalog", requests); // so'rov formasida faqat paketlar

        var (staff, _) = await StaffLoginAsync(f, "site-staff@test.uz", StaffRole.Owner);
        var details = await staff.GetAsync($"/Tenants/Details/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        string d = Html(details);
        Assert.Contains("qo'shimcha xizmat", d);
        Assert.Contains("KORP+SITE", d);
        Assert.Contains("Sayt-katalog", Html(await staff.GetAsync($"/Subscriptions/Create?tenantId={tenant.Id}")));
        string plans = Html(await staff.GetAsync("/Plans"));
        Assert.Contains("SITE", plans);
        string issue = Html(await staff.GetAsync($"/Licenses/Issue?tenantId={tenant.Id}"));
        Assert.Contains("name=\"AddOnCodes\" value=\"SITE\" checked", issue);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/Traction?Days=30&Plan=SITE")).StatusCode);
    }
}
