using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using tahsinERP.Integration;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;
using tahsinERP.Site.Leads;
using tahsinERP.Site.Media;
using tahsinERP.Site.Text;

namespace tahsinERP.Site.Tests;

internal static class Sample
{
    /// <summary>ERP SiteSnapshotBuilder chiqaradigan shakl (camelCase, null maydonlar yo'q).</summary>
    public const string Json = """
    {
      "version": 1, "generatedAt": "2026-10-20T10:00:00",
      "settings": { "enabled": true, "name": "Demo", "primaryColor": "#111827", "font": "Inter", "radius": 10, "languages": ["uz","ru"],
                    "phone": "+998712000000", "priceMode": "SHOW", "stockMode": "STATUS", "leadsEnabled": true, "currency": "so'm" },
      "domains": [ { "host": "shop.demo.uz", "isPrimary": true } ],
      "categories": [ { "id": 1, "slug": "sement", "nameUz": "Sement", "nameRu": "Цемент", "sortOrder": 0 },
                      { "id": 2, "slug": "gisht", "nameUz": "G'isht", "sortOrder": 1 } ],
      "brands": [ { "id": 7, "name": "Qizilqumsement" }, { "id": 8, "name": "Akkermann" } ],
      "products": [
        { "id": 10, "slug": "sement-m400", "sku": "S-400", "nameUz": "Sement M400", "nameRu": "Цемент М400", "categoryId": 1, "brandId": 7,
          "badges": ["SALE"], "sortOrder": 0, "barcodes": ["4780000000017"],
          "units": [ { "name": "kg", "factor": 1, "price": 1500, "isDefault": false }, { "id": 5, "name": "qop", "factor": 50, "price": 75000, "isDefault": true } ],
          "price": 1500, "stockStatus": "in", "attributes": [ { "nameUz": "Marka", "nameRu": "Марка", "valueUz": "M400", "valueRu": "М400" } ],
          "media": [ { "id": 3 } ], "updatedAt": "2026-10-19T09:00:00" },
        { "id": 11, "slug": "sement-m500", "sku": "S-500", "nameUz": "Sement M500", "categoryId": 1, "brandId": 8, "sortOrder": 0,
          "units": [ { "name": "kg", "factor": 1, "price": 1700, "isDefault": true } ], "price": 1700, "stockStatus": "out",
          "attributes": [ { "nameUz": "Marka", "valueUz": "M500" } ], "media": [], "updatedAt": "2026-10-20T09:00:00" },
        { "id": 12, "slug": "gisht-qizil", "sku": "G-1", "nameUz": "G'isht qizil", "nameRu": "Кирпич красный", "categoryId": 2, "sortOrder": 0,
          "units": [ { "name": "dona", "factor": 1, "price": 900, "isDefault": true } ], "price": 900, "stockStatus": "low",
          "attributes": [], "media": [], "updatedAt": "2026-10-18T09:00:00" }
      ],
      "pages": [ { "slug": "yetkazib-berish", "titleUz": "Yetkazib berish", "showInMenu": true, "sortOrder": 0 } ],
      "banners": [],
      "media": [ { "id": 3, "hash": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", "width": 800, "height": 600, "contentType": "image/jpeg" } ]
    }
    """;

    public static CatalogIndex Index() => new("demo", "\"e1\"", Snapshot.Parse(Json));
}

public class CatalogIndexTests
{
    [Fact]
    public void Parse_ErpShape_AndLookups()
    {
        var idx = Sample.Index();
        Assert.Equal(3, idx.Products.Count);
        Assert.Equal(["uz", "ru"], idx.Languages);
        Assert.Equal(10, idx.ProductBySlug("sement-m400")!.Id);
        Assert.Null(idx.ProductBySlug("yoq"));
        Assert.Equal("Цемент", CatalogIndex.Name(idx.CategoryBySlug("sement")!, "ru"));
        Assert.Equal("G'isht", CatalogIndex.Name(idx.CategoryBySlug("gisht")!, "ru"));   // ruscha bo'lmasa — o'zbekcha
        Assert.Equal("Ғишт", CatalogIndex.Name(idx.CategoryBySlug("gisht")!, "oz"));     // kirill — o'zbekchaning transliteratsiyasi
        Assert.Equal(75000m, Ui.DefaultUnit(idx.ProductById(10)!)!.Price);
    }

    [Theory]
    [InlineData("sement", 2)]
    [InlineData("Цемент", 2)]          // kirill → lotin
    [InlineData("m400", 1)]
    [InlineData("4780000000017", 1)]   // shtrix-kod
    [InlineData("gisht", 1)]           // apostrofsiz
    [InlineData("кирпич", 1)]
    [InlineData("xyz", 0)]
    public void Search_Text(string q, int expected) =>
        Assert.Equal(expected, Sample.Index().Search(new CatalogIndex.Query(q), "uz").Total);

    [Fact]
    public void Facets_CountsExcludeOwnGroup_AndFilter()
    {
        var idx = Sample.Index();
        var all = idx.Search(new CatalogIndex.Query(CategoryId: 1), "uz");
        Assert.Equal(2, all.Total);
        var brand = all.Facets.Single(f => f.Key == "brand");
        Assert.Equal([1, 1], brand.Values.Select(v => v.Count));
        var marka = all.Facets.Single(f => f.Key == "a:Marka");
        Assert.Equal(2, marka.Values.Count);

        var one = idx.Search(new CatalogIndex.Query(CategoryId: 1, Brands: [7]), "uz");
        Assert.Equal(1, one.Total);
        Assert.Equal(2, one.Facets.Single(f => f.Key == "brand").Values.Count);   // o'z guruhi bo'yicha sonlar — tanlovsiz
        Assert.True(one.Facets.Single(f => f.Key == "brand").Values.Single(v => v.Value == "7").Selected);

        var attr = idx.Search(new CatalogIndex.Query(Attributes: new Dictionary<string, IReadOnlyCollection<string>> { ["Marka"] = ["M500"] }), "ru");
        Assert.Equal(11, attr.Items.Single().Id);
        Assert.Equal(1, idx.Search(new CatalogIndex.Query(CategoryId: 1, InStock: true), "uz").Total);
    }

    [Fact]
    public void Sort_AndPaging_OutOfStockLast()
    {
        var idx = Sample.Index();
        Assert.Equal([12, 10, 11], idx.Search(new CatalogIndex.Query(Sort: "price-asc"), "uz").Items.Select(p => p.Id));
        Assert.Equal(11, idx.Search(new CatalogIndex.Query(), "uz").Items.Last().Id);   // "buyurtmaga" — oxirida
        var page = idx.Search(new CatalogIndex.Query(PageSize: 2, Page: 5), "uz");
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.Pages);
        Assert.Single(page.Items);
    }

    [Fact]
    public void Normalize_TransliteratesAndUnifiesHx() =>
        Assert.Equal(CatalogIndex.Normalize("Хона"), CatalogIndex.Normalize("hona"));
}

public class SafeTextTests
{
    [Fact]
    public void Render_EncodesHtml_AndSupportsMarkup()
    {
        var html = SafeText.Render("**Qalin** <script>alert(1)</script>\nikkinchi qator\n\n- bir\n- <b>ikki</b>\n\n## Sarlavha").ToString();
        Assert.Contains("<strong>Qalin</strong>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("<br>", html);
        Assert.Contains("<ul><li>bir</li><li>&lt;b&gt;ikki&lt;/b&gt;</li></ul>", html);
        Assert.Contains("<h3>Sarlavha</h3>", html);
        Assert.Equal("", SafeText.Render(null).ToString());
    }

    [Fact]
    public void Plain_AndMoney()
    {
        Assert.Equal("Qalin matn bir ikki", SafeText.Plain("**Qalin** matn\n- bir\n- ikki"));
        Assert.True(SafeText.Plain(new string('a', 500), 160).Length <= 160);
        Assert.Equal("1 250 000 so'm", SafeText.Money(1250000m, "uz"));
        Assert.Equal("99,50 сум", SafeText.Money(99.5m, "ru"));
    }

    [Fact]
    public void JsonLd_CannotBreakOutOfScript()
    {
        var html = Seo.Script(new System.Text.Json.Nodes.JsonObject { ["name"] = "</script><script>x()</script>" }).ToString();
        Assert.Equal(1, html.Split("</script>").Length - 1);
    }

    [Fact]
    public void Social_OnlySafeHandles()
    {
        Assert.Equal("https://t.me/demo_uz", Social.Telegram("@demo_uz"));
        Assert.Null(Social.Telegram("javascript:alert(1)"));
        Assert.Equal("https://instagram.com/x", Social.Instagram("https://instagram.com/x"));
    }

    [Fact]
    public void AllTexts_HaveBothLanguages()
    {
        foreach (var k in T.Keys)
        {
            Assert.NotEqual(k, T.Get("uz", k));
            Assert.False(string.IsNullOrWhiteSpace(T.Get("ru", k)), k);
            Assert.False(string.IsNullOrWhiteSpace(T.Get("oz", k)), k);
        }
        Assert.Equal("Каталог", T.Get("oz", "Catalog"));
        Assert.Equal("uz-Cyrl", T.HrefLang("oz"));
        Assert.True(T.IsLanguage("oz") && !T.IsLanguage("en"));
    }
}

public class LeadTests
{
    [Theory]
    [InlineData("90 111 22 33", "+998901112233")]
    [InlineData("+998 (90) 111-22-33", "+998901112233")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void Phone(string input, string? expected) => Assert.Equal(expected, LeadService.NormalizePhone(input));

    [Fact]
    public void Items_OnlyKnownProductsAndUnits()
    {
        var idx = Sample.Index();
        var items = LeadService.ParseItems("""[{"id":10,"unit":5,"qty":2},{"id":12,"unit":null,"qty":1.5}]""", idx);
        Assert.Equal(2, items.Count);
        Assert.Equal(5, items[0].ProductUnitId);
        Assert.Throws<FormatException>(() => LeadService.ParseItems("""[{"id":99,"unit":null,"qty":1}]""", idx));
        Assert.Throws<FormatException>(() => LeadService.ParseItems("""[{"id":10,"unit":6,"qty":1}]""", idx));
        Assert.Throws<FormatException>(() => LeadService.ParseItems("""[{"id":10,"unit":5,"qty":-1}]""", idx));
        Assert.Throws<FormatException>(() => LeadService.ParseItems("not json", idx));
        Assert.Empty(LeadService.ParseItems("", idx));
    }

    private static (LeadService Svc, SiteDbContext Db) Svc(DateTimeOffset now)
    {
        var db = new SiteDbContext(new DbContextOptionsBuilder<SiteDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (new LeadService(db, new FixedClock(now)), db);
    }

    [Fact]
    public async Task Submit_Validates_Dedupes_AndTrapsBots()
    {
        LeadService.ResetLimits();
        var now = DateTimeOffset.FromUnixTimeSeconds(1792500000);
        var (svc, db) = Svc(now);
        var tenant = new SiteTenant { Id = 1, Slug = "t" + Guid.NewGuid().ToString("N")[..6] };
        var idx = Sample.Index();
        var old = now.AddMinutes(-1).ToUnixTimeMilliseconds();
        var rid = Guid.NewGuid();

        Assert.Equal("Err_Name", (await svc.SubmitAsync(tenant, idx, new LeadForm("A", "901112233", null, null, null, null, null, old, rid), "uz", "h", "1.1.1.1", default)).ErrorKey);
        Assert.Equal("Err_Phone", (await svc.SubmitAsync(tenant, idx, new LeadForm("Ali", "123", null, null, null, null, null, old, rid), "uz", "h", "1.1.1.1", default)).ErrorKey);
        Assert.Equal("Err_Cart", (await svc.SubmitAsync(tenant, idx, new LeadForm("Ali", "901112233", null, null, null, """[{"id":1}]""", null, old, rid), "uz", "h", "1.1.1.1", default)).ErrorKey);

        var ok = await svc.SubmitAsync(tenant, idx, new LeadForm("Ali Valiyev", "90 111 22 33", "MChJ", null, "salom", """[{"id":10,"unit":5,"qty":2}]""", null, old, rid), "ru", "shop.demo.uz", "1.1.1.1", default);
        Assert.True(ok.Ok);
        Assert.Equal("+998901112233", ok.Phone);
        await svc.SubmitAsync(tenant, idx, new LeadForm("Ali Valiyev", "90 111 22 33", null, null, null, null, null, old, rid), "ru", "h", "2.2.2.2", default);
        var row = Assert.Single(db.LeadOutbox);
        Assert.Contains("\"productUnitId\":5", row.Payload);
        Assert.Contains("\"language\":\"ru\"", row.Payload);

        // bot: yashirin maydon yoki juda tez — "muvaffaqiyat", lekin yozilmaydi
        Assert.True((await svc.SubmitAsync(tenant, idx, new LeadForm("Bot", "901112244", null, null, null, null, "http://spam", old, Guid.NewGuid()), "uz", "h", "3.3.3.3", default)).Ok);
        Assert.True((await svc.SubmitAsync(tenant, idx, new LeadForm("Bot", "901112255", null, null, null, null, null, now.ToUnixTimeMilliseconds(), Guid.NewGuid()), "uz", "h", "3.3.3.3", default)).Ok);
        Assert.Single(db.LeadOutbox);
    }

    [Fact]
    public async Task Submit_RateLimitedPerIp()
    {
        LeadService.ResetLimits();
        var now = DateTimeOffset.FromUnixTimeSeconds(1792500000);
        var (svc, _) = Svc(now);
        var tenant = new SiteTenant { Id = 1, Slug = "rl" };
        var idx = Sample.Index();
        var old = now.AddMinutes(-1).ToUnixTimeMilliseconds();
        for (int i = 0; i < 5; i++)
            Assert.True((await svc.SubmitAsync(tenant, idx, new LeadForm("Ali", "9011100" + (10 + i), null, null, null, null, null, old, Guid.NewGuid()), "uz", "h", "9.9.9.9", default)).Ok);
        Assert.Equal("Err_TooMany", (await svc.SubmitAsync(tenant, idx, new LeadForm("Ali", "901110099", null, null, null, null, null, old, Guid.NewGuid()), "uz", "h", "9.9.9.9", default)).ErrorKey);
    }
}

public class InfraTests
{
    [Fact]
    public void Media_UrlSrcSetAndResize()
    {
        var m = new SnapshotMedia(3, "abcdef0123456789", 800, 600, "image/jpeg");
        Assert.Equal("/media/3-abcdef01-400.webp", MediaCache.Url(m, 350));
        Assert.Equal("/media/3-abcdef01-1600.webp", MediaCache.Url(m, 5000));
        Assert.Equal("/media/3-abcdef01-400.webp 400w, /media/3-abcdef01-600.webp 600w", MediaCache.SrcSet(m, 400, 600, 1200));

        using var bmp = new SKBitmap(1000, 500);
        using (var canvas = new SKCanvas(bmp)) canvas.Clear(SKColors.SteelBlue);
        using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        var webp = MediaCache.Resize(png.ToArray(), 400);
        using var decoded = SKBitmap.Decode(webp);
        Assert.Equal(400, decoded.Width);
        Assert.Equal(200, decoded.Height);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(webp, 0, 4));
        using var small = SKBitmap.Decode(MediaCache.Resize(png.ToArray(), 1600));
        Assert.Equal(1000, small.Width);   // kattalashtirilmaydi
    }

    [Fact]
    public async Task TenantForHost_SubdomainAndCustomDomain()
    {
        var store = new CatalogStore(null!, Options.Create(new SiteOptions { BaseDomain = "tahsin.uz" }), NullLogger<CatalogStore>.Instance);
        typeof(CatalogStore).GetField("_tenants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(store, new List<SiteTenant> { new() { Id = 1, Slug = "demo" } });
        store.Set(Sample.Index());
        Assert.Equal("demo", store.TenantForHost("demo.tahsin.uz"));
        Assert.Equal("demo", store.TenantForHost("SHOP.demo.uz"));
        Assert.Equal("demo", store.TenantForHost("www.shop.demo.uz"));
        Assert.Null(store.TenantForHost("boshqa.tahsin.uz"));
        Assert.Null(store.TenantForHost("a.demo.tahsin.uz"));
        Assert.Null(store.TenantForHost("evil.uz"));
        Assert.Equal("shop.demo.uz", store.PrimaryHost("demo"));
        await Task.CompletedTask;
    }

    [Fact]
    public void ErpClient_SignsVerifiably()
    {
        const string master = "0123456789abcdef0123456789abcdef-site";
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1792500000));
        var client = new SiteErpClient(null!, Options.Create(new SiteOptions { MasterKey = master }), clock);
        var tenant = new SiteTenant { Slug = "demo", ErpBaseUrl = "https://erp.example.uz/firma" };
        using var req = client.Create(tenant, HttpMethod.Get, "Media?id=3", null);
        Assert.Equal("/firma/Site/SiteApi/Media?id=3", req.RequestUri!.PathAndQuery);
        Assert.False(req.Headers.Contains(DstApiSignature.ActingUserHeader));
        Assert.True(DstApiSignature.Verify(DstApiSignature.TenantKey(master, "default"), req.Headers.GetValues(DstApiSignature.TimestampHeader).Single(),
            req.Headers.GetValues(DstApiSignature.SignatureHeader).Single(), "GET", req.RequestUri.PathAndQuery, null, [], clock.GetUtcNow()));

        tenant.ErpTenant = "firma";
        var body = System.Text.Encoding.UTF8.GetBytes("{\"a\":1}");
        using var post = client.Create(tenant, HttpMethod.Post, "Lead", body);
        Assert.True(DstApiSignature.Verify(DstApiSignature.TenantKey(master, "firma"), post.Headers.GetValues(DstApiSignature.TimestampHeader).Single(),
            post.Headers.GetValues(DstApiSignature.SignatureHeader).Single(), "POST", post.RequestUri!.PathAndQuery, null, body, clock.GetUtcNow()));
    }

    [Fact]
    public void Schema_Batches()
    {
        var batches = SchemaInitializer.Batches(SchemaInitializer.ReadScript()).ToList();
        Assert.True(batches.Count >= 6);
        Assert.All(batches, b => Assert.StartsWith("IF ", b));
    }
}

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
