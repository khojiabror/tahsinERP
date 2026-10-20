using System.Text.Json;

namespace tahsinERP.Site.Catalog;

// ERP SiteApi/Snapshot javobi (tahsinERP/Areas/Site/Services/SiteSnapshot.cs bilan bir xil shakl, camelCase).

public sealed record Snapshot(
    int Version,
    DateTime GeneratedAt,
    SnapshotSettings Settings,
    List<SnapshotDomain>? Domains,
    List<SnapshotCategory>? Categories,
    List<SnapshotBrand>? Brands,
    List<SnapshotProduct>? Products,
    List<SnapshotPage>? Pages,
    List<SnapshotBanner>? Banners,
    List<SnapshotMedia>? Media)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Snapshot Parse(string json) => JsonSerializer.Deserialize<Snapshot>(json, Json) ?? throw new JsonException("Bo'sh nusxa");
}

public sealed record SnapshotSettings(
    bool Enabled, string? Name, string? Tagline, int? LogoMediaId, string? PrimaryColor, string? Font, int Radius,
    List<string>? Languages, string? Phone, string? Phone2, string? Email, string? Address, string? WorkingHours,
    decimal? MapLat, decimal? MapLng, string? Telegram, string? Instagram, string? Facebook,
    string? PriceMode, string? StockMode, bool LeadsEnabled, string? SeoTitle, string? SeoDescription,
    string? AnalyticsId, string? MetrikaId, string? Currency);

public sealed record SnapshotDomain(string Host, bool IsPrimary);

public sealed record SnapshotCategory(int Id, string Slug, string NameUz, string? NameRu, string? DescriptionUz, string? DescriptionRu,
    int? MediaId, int SortOrder, string? SeoTitle, string? SeoDescription);

public sealed record SnapshotBrand(int Id, string Name);

public sealed record SnapshotUnit(int? Id, string? Name, decimal Factor, decimal? Price, bool IsDefault);

public sealed record SnapshotAttribute(string NameUz, string? NameRu, string ValueUz, string? ValueRu);

public sealed record SnapshotProductMedia(int Id, string? AltUz, string? AltRu);

public sealed record SnapshotProduct(
    int Id, string Slug, string Sku, string NameUz, string? NameRu, string? DescriptionUz, string? DescriptionRu,
    int? CategoryId, int? BrandId, List<string>? Badges, int SortOrder, string? SeoTitle, string? SeoDescription,
    List<string>? Barcodes, List<SnapshotUnit>? Units, decimal? Price, string? StockStatus, decimal? StockQty,
    List<SnapshotAttribute>? Attributes, List<SnapshotProductMedia>? Media, DateTime UpdatedAt);

public sealed record SnapshotPage(string Slug, string TitleUz, string? TitleRu, string? BodyUz, string? BodyRu, bool ShowInMenu, int SortOrder);

public sealed record SnapshotBanner(int MediaId, string? TitleUz, string? TitleRu, string? SubtitleUz, string? SubtitleRu, string? Link, int SortOrder);

public sealed record SnapshotMedia(int Id, string Hash, int Width, int Height, string ContentType);
