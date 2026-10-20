using System.Globalization;
using System.Text;

namespace tahsinERP.Site.Catalog;

/// <summary>
/// Bitta tenant katalogining xotiradagi indeksi (nusxadan quriladi, o'zgarmas): slug bo'yicha qidirish, kategoriya,
/// matnli qidiruv (lotin/kirill, artikul, shtrix-kod), filtrlar (brend, xususiyat, "bor") va ularning natija sonlari.
/// 20 ming tovargacha xotirada yetarli tez; kattaroq katalogda — alohida qidiruv dvigateli.
/// </summary>
public sealed class CatalogIndex
{
    public string Tenant { get; }
    public string ETag { get; }
    public Snapshot Snapshot { get; }
    public SnapshotSettings Settings => Snapshot.Settings;
    public IReadOnlyList<SnapshotProduct> Products { get; }
    public IReadOnlyList<SnapshotCategory> Categories { get; }
    public IReadOnlyDictionary<int, SnapshotBrand> Brands { get; }
    public IReadOnlyDictionary<int, SnapshotMedia> Media { get; }
    public IReadOnlyList<string> Languages { get; }
    public string DefaultLanguage => Languages[0];

    private readonly Dictionary<string, SnapshotProduct> _bySlug;
    private readonly Dictionary<int, SnapshotProduct> _byId;
    private readonly Dictionary<string, SnapshotCategory> _categoryBySlug;
    private readonly Dictionary<int, SnapshotCategory> _categoryById;
    private readonly Dictionary<int, string> _searchText;   // normallashtirilgan nom (uz+ru), artikul, shtrix-kodlar

    public CatalogIndex(string tenant, string etag, Snapshot snapshot)
    {
        Tenant = tenant;
        ETag = etag;
        Snapshot = snapshot;
        Products = snapshot.Products ?? [];
        Categories = (snapshot.Categories ?? []).OrderBy(c => c.SortOrder).ThenBy(c => c.NameUz).ToList();
        Brands = (snapshot.Brands ?? []).ToDictionary(b => b.Id);
        Media = (snapshot.Media ?? []).ToDictionary(m => m.Id);
        var langs = (snapshot.Settings.Languages ?? []).Where(l => l is "uz" or "ru").Distinct().ToList();
        Languages = langs.Count > 0 ? langs : ["uz"];
        _bySlug = Products.GroupBy(p => p.Slug).ToDictionary(g => g.Key, g => g.First());
        _byId = Products.ToDictionary(p => p.Id);
        _categoryBySlug = Categories.GroupBy(c => c.Slug).ToDictionary(g => g.Key, g => g.First());
        _categoryById = Categories.ToDictionary(c => c.Id);
        _searchText = Products.ToDictionary(p => p.Id, p =>
            Normalize(p.NameUz + " " + p.NameRu + " " + p.Sku + " " + string.Join(" ", p.Barcodes ?? [])));
    }

    public SnapshotProduct? ProductBySlug(string slug) => _bySlug.GetValueOrDefault(slug);
    public SnapshotProduct? ProductById(int id) => _byId.GetValueOrDefault(id);
    public SnapshotCategory? CategoryBySlug(string slug) => _categoryBySlug.GetValueOrDefault(slug);
    public SnapshotCategory? CategoryById(int? id) => id.HasValue ? _categoryById.GetValueOrDefault(id.Value) : null;
    public bool SupportsLanguage(string lang) => Languages.Contains(lang);

    public int CountIn(int categoryId) => Products.Count(p => p.CategoryId == categoryId);

    // ---------- so'rov ----------

    public sealed record Query(string? Text = null, int? CategoryId = null, IReadOnlyCollection<int>? Brands = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Attributes = null, bool InStock = false,
        string Sort = "default", int Page = 1, int PageSize = 24);

    public sealed record FacetValue(string Value, string Label, int Count, bool Selected);

    public sealed record Facet(string Key, string Title, List<FacetValue> Values);

    public sealed record Result(List<SnapshotProduct> Items, int Total, int Page, int Pages, List<Facet> Facets);

    public Result Search(Query q, string lang)
    {
        IEnumerable<SnapshotProduct> scope = Products;
        if (q.CategoryId.HasValue) scope = scope.Where(p => p.CategoryId == q.CategoryId);
        var terms = Terms(q.Text);
        if (terms.Count > 0) scope = scope.Where(p => terms.All(t => _searchText[p.Id].Contains(t, StringComparison.Ordinal)));
        var baseSet = scope.ToList();

        // filtrlar: har guruh sonlari — o'sha guruhdan tashqari boshqa filtrlar qo'llanganda (multi-select odatiy usuli)
        bool BrandOk(SnapshotProduct p) => q.Brands is not { Count: > 0 } || p.BrandId.HasValue && q.Brands.Contains(p.BrandId.Value);
        bool StockOk(SnapshotProduct p) => !q.InStock || p.StockStatus is "in" or "low";
        bool AttrOk(SnapshotProduct p, string? except) => q.Attributes is null || q.Attributes.All(kv =>
            kv.Key == except || kv.Value.Count == 0 || (p.Attributes ?? []).Any(a => AttrName(a, "uz") == kv.Key && kv.Value.Contains(AttrValue(a, "uz"))));

        var filtered = baseSet.Where(p => BrandOk(p) && StockOk(p) && AttrOk(p, null)).ToList();

        var facets = new List<Facet>();
        var brandCounts = baseSet.Where(p => StockOk(p) && AttrOk(p, null) && p.BrandId.HasValue && Brands.ContainsKey(p.BrandId.Value))
            .GroupBy(p => p.BrandId!.Value).Select(g => new FacetValue(g.Key.ToString(CultureInfo.InvariantCulture), Brands[g.Key].Name, g.Count(),
                q.Brands?.Contains(g.Key) == true)).OrderBy(v => v.Label).ToList();
        if (brandCounts.Count > 1 || brandCounts.Any(v => v.Selected)) facets.Add(new Facet("brand", "brand", brandCounts));

        var attrGroups = baseSet.SelectMany(p => (p.Attributes ?? []).Select(a => (p, a)))
            .GroupBy(x => AttrName(x.a, "uz")).Where(g => g.Select(x => AttrValue(x.a, "uz")).Distinct().Count() > 1).Take(8);
        foreach (var g in attrGroups)
        {
            var title = AttrName(g.First().a, lang);
            var values = g.Where(x => BrandOk(x.p) && StockOk(x.p) && AttrOk(x.p, g.Key))
                .GroupBy(x => AttrValue(x.a, "uz"))
                .Select(v => new FacetValue(v.Key, AttrValue(v.First().a, lang), v.Select(x => x.p.Id).Distinct().Count(),
                    q.Attributes?.GetValueOrDefault(g.Key)?.Contains(v.Key) == true))
                .OrderBy(v => v.Label, StringComparer.Create(CultureInfo.InvariantCulture, true)).Take(30).ToList();
            if (values.Count > 0) facets.Add(new Facet("a:" + g.Key, title, values));
        }

        IEnumerable<SnapshotProduct> sorted = q.Sort switch
        {
            "price-asc" => filtered.OrderBy(p => p.Price ?? decimal.MaxValue),
            "price-desc" => filtered.OrderByDescending(p => p.Price ?? -1),
            "name" => filtered.OrderBy(p => Name(p, lang), StringComparer.Create(CultureInfo.InvariantCulture, true)),
            "new" => filtered.OrderByDescending(p => p.UpdatedAt),
            _ => terms.Count > 0
                ? filtered.OrderByDescending(p => Normalize(Name(p, lang)).StartsWith(terms[0], StringComparison.Ordinal)).ThenBy(p => p.SortOrder)
                : filtered.OrderBy(p => p.StockStatus == "out").ThenBy(p => p.SortOrder).ThenBy(p => Name(p, lang))
        };
        int pageSize = Math.Clamp(q.PageSize, 1, 96);
        int pages = Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
        int page = Math.Clamp(q.Page, 1, pages);
        return new Result(sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList(), filtered.Count, page, pages, facets);
    }

    /// <summary>O'xshash tovarlar: shu kategoriya, keyin shu brend.</summary>
    public List<SnapshotProduct> Related(SnapshotProduct p, int take = 8) =>
        Products.Where(x => x.Id != p.Id && x.StockStatus != "out")
            .OrderByDescending(x => x.CategoryId == p.CategoryId).ThenByDescending(x => x.BrandId == p.BrandId && p.BrandId.HasValue)
            .ThenBy(x => x.SortOrder).Take(take).ToList();

    // ---------- til ----------

    public static string Name(SnapshotProduct p, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(p.NameRu) ? p.NameRu! : p.NameUz;
    public static string? Description(SnapshotProduct p, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(p.DescriptionRu) ? p.DescriptionRu : p.DescriptionUz;
    public static string Name(SnapshotCategory c, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(c.NameRu) ? c.NameRu! : c.NameUz;
    public static string? Description(SnapshotCategory c, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(c.DescriptionRu) ? c.DescriptionRu : c.DescriptionUz;
    public static string AttrName(SnapshotAttribute a, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(a.NameRu) ? a.NameRu! : a.NameUz;
    public static string AttrValue(SnapshotAttribute a, string lang) => lang == "ru" && !string.IsNullOrWhiteSpace(a.ValueRu) ? a.ValueRu! : a.ValueUz;

    // ---------- matnni normallashtirish ----------

    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "j", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "x", ['ц'] = "s", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sh", ['ъ'] = "", ['ы'] = "i", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ў'] = "o", ['қ'] = "q", ['ғ'] = "g", ['ҳ'] = "h"
    };

    /// <summary>Kichik harf, kirill → lotin, apostrof va belgilarsiz: "Цемент М-400" → "tsement m400".</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
        {
            if (Translit.TryGetValue(ch, out var t)) sb.Append(t);
            else if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is '\'' or '‘' or '’' or 'ʻ' or 'ʼ' or '`' or '-') continue;
            else sb.Append(' ');
        }
        // "x" ham "h" sifatida qidirilsin (xona / хона) — lotinda ikkala variant uchraydi
        return sb.ToString().Replace('h', 'x');
    }

    public static List<string> Terms(string? text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8).ToList();
}
