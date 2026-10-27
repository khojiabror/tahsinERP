using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tahsinERP.Site.Admin;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Tests;

/// <summary>Firmaning o'z domeni: panel /admin/api orqali ulaydi, host bo'yicha tenant topiladi, kerak bo'lsa 301.</summary>
public class CustomDomainTests
{
    [Theory]
    [InlineData("Shop.Firma.UZ.", "shop.firma.uz")]
    [InlineData(" firma.uz ", "firma.uz")]
    [InlineData("дом.рф", "xn--d1aqf.xn--p1ai")]
    public void NormalizeHost_Valid(string input, string expected) => Assert.Equal(expected, SiteTenantAdmin.NormalizeHost(input));

    [Theory]
    [InlineData("firma")]
    [InlineData("1.2.3.4")]
    [InlineData("-a.uz")]
    [InlineData("a..uz")]
    [InlineData("http://firma.uz")]
    [InlineData("firma.uz/shop")]
    [InlineData("")]
    public void NormalizeHost_Invalid(string input) => Assert.Throws<ArgumentException>(() => SiteTenantAdmin.NormalizeHost(input));

    private static (SiteTenantAdmin Admin, CatalogStore Store, ServiceProvider Sp) Build()
    {
        var name = Guid.NewGuid().ToString();
        var sp = new ServiceCollection().AddDbContext<SiteDbContext>(o => o.UseInMemoryDatabase(name)).BuildServiceProvider();
        var store = new CatalogStore(sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new SiteOptions { BaseDomain = "sayt.tahsin.uz" }),
            NullLogger<CatalogStore>.Instance);
        var db = sp.CreateScope().ServiceProvider.GetRequiredService<SiteDbContext>();
        db.Tenants.AddRange(new SiteTenant { Slug = "firma", ErpBaseUrl = "https://firma.tahsin.uz/" }, new SiteTenant { Slug = "boshqa", ErpBaseUrl = "https://boshqa.tahsin.uz/" });
        db.SaveChanges();
        return (new SiteTenantAdmin(db, store), store, sp);
    }

    /// <summary>Firma panelda o'chirilganda: tenant, katalog nusxasi va ariza navbati yo'qoladi, host endi topilmaydi; boshqa firma tegilmaydi.</summary>
    [Fact]
    public async Task Delete_RemovesTenantSnapshotAndLeads_Idempotent()
    {
        var (admin, store, sp) = Build();
        using var _ = sp;
        var db = sp.CreateScope().ServiceProvider.GetRequiredService<SiteDbContext>();
        var firma = await db.Tenants.FirstAsync(t => t.Slug == "firma");
        var boshqa = await db.Tenants.FirstAsync(t => t.Slug == "boshqa");
        var now = DateTime.UtcNow;
        foreach (var id in new[] { firma.Id, boshqa.Id })
        {
            db.Snapshots.Add(new SiteSnapshotRow { TenantId = id, ETag = "1", Json = "{}", FetchedAt = now });
            db.LeadOutbox.Add(new LeadOutboxItem { TenantId = id, ClientRequestId = Guid.NewGuid(), Payload = "{}", CreatedAt = now, NextAt = now });
        }
        await db.SaveChangesAsync();
        await store.RefreshTenantsAsync(default);
        Assert.Equal("firma", store.TenantForHost("firma.sayt.tahsin.uz"));

        Assert.True(await admin.DeleteAsync("Firma", default));
        var check = sp.CreateScope().ServiceProvider.GetRequiredService<SiteDbContext>();
        Assert.False(await check.Tenants.AnyAsync(t => t.Slug == "firma"));
        Assert.False(await check.Snapshots.AnyAsync(s => s.TenantId == firma.Id));
        Assert.False(await check.LeadOutbox.AnyAsync(l => l.TenantId == firma.Id));
        Assert.Null(store.TenantForHost("firma.sayt.tahsin.uz"));
        Assert.Equal(1, await check.Snapshots.CountAsync(s => s.TenantId == boshqa.Id));
        Assert.Equal("boshqa", store.TenantForHost("boshqa.sayt.tahsin.uz"));
        Assert.False(await admin.DeleteAsync("firma", default));
    }

    [Fact]
    public async Task SetDomain_MapsHost_RejectsTakenAndReserved_AndClears()
    {
        var (admin, store, sp) = Build();
        using var _ = sp;
        await store.RefreshTenantsAsync(default);
        Assert.Null(store.TenantForHost("shop.firma.uz"));

        await admin.SetDomainAsync("firma", "Shop.Firma.uz", redirect: false, "sayt.tahsin.uz", default);
        Assert.Equal("firma", store.TenantForHost("shop.firma.uz"));
        Assert.Equal("firma", store.TenantForHost("www.shop.firma.uz"));
        Assert.Equal("firma", store.TenantForHost("firma.sayt.tahsin.uz"));   // standart manzil ham ishlaydi
        Assert.Equal("firma.sayt.tahsin.uz", store.PrimaryHost("firma"));    // yo'naltirish yoqilmaguncha kanonik — subdomen
        var st = await admin.StatusAsync("firma", default);
        Assert.Equal(("shop.firma.uz", false), (st.Domain, st.RedirectToDomain));

        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SetDomainAsync("boshqa", "shop.firma.uz", false, "sayt.tahsin.uz", default));
        await Assert.ThrowsAsync<ArgumentException>(() => admin.SetDomainAsync("boshqa", "x.sayt.tahsin.uz", false, "sayt.tahsin.uz", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SetDomainAsync("yoq", "yoq.uz", false, "sayt.tahsin.uz", default));

        await admin.SetDomainAsync("firma", "shop.firma.uz", redirect: true, "sayt.tahsin.uz", default);
        Assert.Equal("shop.firma.uz", store.PrimaryHost("firma"));

        await admin.SetDomainAsync("firma", null, redirect: true, "sayt.tahsin.uz", default);
        Assert.Null(store.TenantForHost("shop.firma.uz"));
        st = await admin.StatusAsync("firma", default);
        Assert.Equal((null, false), (st.Domain, st.RedirectToDomain));
        await admin.SetDomainAsync("boshqa", "shop.firma.uz", false, "sayt.tahsin.uz", default);   // bo'shagan domen boshqasiga
        Assert.Equal("boshqa", store.TenantForHost("shop.firma.uz"));
    }

    [Fact]
    public void DomainRedirect_Rules()
    {
        var t = new SiteTenant { Slug = "firma", CustomDomain = "shop.firma.uz" };
        var root = new PathString("/uz/");
        Assert.Null(TenantMiddleware.DomainRedirect(t, "firma.sayt.tahsin.uz", true, true, root));          // yo'naltirish o'chiq
        Assert.Null(TenantMiddleware.DomainRedirect(t, "shop.firma.uz", true, true, root));
        Assert.Equal("https://shop.firma.uz", TenantMiddleware.DomainRedirect(t, "shop.firma.uz", false, true, root));   // http → https
        Assert.Null(TenantMiddleware.DomainRedirect(t, "shop.firma.uz", false, false, root));
        Assert.Null(TenantMiddleware.DomainRedirect(t, "shop.firma.uz", false, true, new PathString("/.well-known/acme-challenge/x")));
        Assert.Null(TenantMiddleware.DomainRedirect(t, "firma.sayt.tahsin.uz", false, true, root));         // subdomen http — ERP qoidasi

        t.RedirectToDomain = true;
        Assert.Equal("https://shop.firma.uz", TenantMiddleware.DomainRedirect(t, "firma.sayt.tahsin.uz", true, true, root));
        Assert.Equal("https://shop.firma.uz", TenantMiddleware.DomainRedirect(t, "www.shop.firma.uz", true, true, root));
        Assert.Null(TenantMiddleware.DomainRedirect(t, "shop.firma.uz", true, true, root));
        Assert.Null(TenantMiddleware.DomainRedirect(new SiteTenant { Slug = "x", RedirectToDomain = true }, "x.sayt.tahsin.uz", true, true, root));
    }
}
