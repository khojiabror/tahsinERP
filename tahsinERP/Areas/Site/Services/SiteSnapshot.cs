using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Data;

namespace tahsinERP.Areas.Site.Services
{
    /// <summary>site.SETTINGS — kalit/qiymat.</summary>
    public static class SiteSettingsStore
    {
        public static Dictionary<string, string> All(DistributionDbContext db) =>
            db.SiteSettings.AsNoTracking().ToList().ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        public static void Set(DistributionDbContext db, string key, string value, DateTime now)
        {
            var row = db.SiteSettings.Find(key);
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (row == null)
            {
                if (value == null) return;
                db.SiteSettings.Add(new SiteSetting { Key = key, Value = value, UpdatedAt = now });
            }
            else if (row.Value != value)
            {
                row.Value = value;
                row.UpdatedAt = now;
            }
        }

        public static string Get(IReadOnlyDictionary<string, string> all, string key, string fallback = null) =>
            all.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    }

    // ---------- ochiq saytga beriladigan nusxa (tahsinERP.Site/Catalog/SnapshotModels.cs bilan bir xil shakl) ----------

    public sealed class SnapshotDto
    {
        public int Version { get; set; } = 1;
        public DateTime GeneratedAt { get; set; }
        public SnapshotSettings Settings { get; set; }
        public List<SnapshotDomain> Domains { get; set; }
        public List<SnapshotCategory> Categories { get; set; }
        public List<SnapshotBrand> Brands { get; set; }
        public List<SnapshotProduct> Products { get; set; }
        public List<SnapshotPage> Pages { get; set; }
        public List<SnapshotBanner> Banners { get; set; }
        public List<SnapshotMedia> Media { get; set; }
    }

    public sealed class SnapshotSettings
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Tagline { get; set; }
        public int? LogoMediaId { get; set; }
        public string PrimaryColor { get; set; }
        public string Font { get; set; }
        public int Radius { get; set; }
        public List<string> Languages { get; set; }
        public string Phone { get; set; }
        public string Phone2 { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string WorkingHours { get; set; }
        public decimal? MapLat { get; set; }
        public decimal? MapLng { get; set; }
        public string Telegram { get; set; }
        public string Instagram { get; set; }
        public string Facebook { get; set; }
        public string PriceMode { get; set; }
        public string StockMode { get; set; }
        public bool LeadsEnabled { get; set; }
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
        public string AnalyticsId { get; set; }
        public string MetrikaId { get; set; }
        public string Currency { get; set; }
    }

    public sealed class SnapshotDomain
    {
        public string Host { get; set; }
        public bool IsPrimary { get; set; }
    }

    public sealed class SnapshotCategory
    {
        public int Id { get; set; }
        public string Slug { get; set; }
        public string NameUz { get; set; }
        public string NameRu { get; set; }
        public string DescriptionUz { get; set; }
        public string DescriptionRu { get; set; }
        public int? MediaId { get; set; }
        public int SortOrder { get; set; }
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
    }

    public sealed class SnapshotBrand
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class SnapshotUnit
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public decimal Factor { get; set; }
        public decimal? Price { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class SnapshotAttribute
    {
        public string NameUz { get; set; }
        public string NameRu { get; set; }
        public string ValueUz { get; set; }
        public string ValueRu { get; set; }
    }

    public sealed class SnapshotProductMedia
    {
        public int Id { get; set; }
        public string AltUz { get; set; }
        public string AltRu { get; set; }
    }

    public sealed class SnapshotProduct
    {
        public int Id { get; set; }
        public string Slug { get; set; }
        public string Sku { get; set; }
        public string NameUz { get; set; }
        public string NameRu { get; set; }
        public string DescriptionUz { get; set; }
        public string DescriptionRu { get; set; }
        public int? CategoryId { get; set; }
        public int? BrandId { get; set; }
        public List<string> Badges { get; set; }
        public int SortOrder { get; set; }
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
        public List<string> Barcodes { get; set; }
        public List<SnapshotUnit> Units { get; set; }
        public decimal? Price { get; set; }
        public string StockStatus { get; set; }   // in / low / out (StockMode=HIDDEN — null)
        public decimal? StockQty { get; set; }    // faqat StockMode=EXACT
        public List<SnapshotAttribute> Attributes { get; set; }
        public List<SnapshotProductMedia> Media { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class SnapshotPage
    {
        public string Slug { get; set; }
        public string TitleUz { get; set; }
        public string TitleRu { get; set; }
        public string BodyUz { get; set; }
        public string BodyRu { get; set; }
        public bool ShowInMenu { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class SnapshotBanner
    {
        public int MediaId { get; set; }
        public string TitleUz { get; set; }
        public string TitleRu { get; set; }
        public string SubtitleUz { get; set; }
        public string SubtitleRu { get; set; }
        public string Link { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class SnapshotMedia
    {
        public int Id { get; set; }
        public string Hash { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ContentType { get; set; }
    }

    /// <summary>
    /// Nashr qilingan katalogning to'liq nusxasi (faqat ochiq ma'lumot: tannarx, boshqa narx turlari, mijozlar YO'Q).
    /// Narx — sozlamadagi narx turi bo'yicha (PriceMode=SHOW bo'lsagina), qoldiq — savdo omborlarida mavjud (haqiqiy − rezerv).
    /// </summary>
    public static class SiteSnapshotBuilder
    {
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss"
        };

        public static SnapshotDto Build(DistributionDbContext db, DateTime now)
        {
            var s = SiteSettingsStore.All(db);
            string priceMode = PriceModes.All.Contains(SiteSettingsStore.Get(s, SiteSettingKeys.PriceMode)) ? s[SiteSettingKeys.PriceMode] : PriceModes.Ask;
            string stockMode = StockModes.All.Contains(SiteSettingsStore.Get(s, SiteSettingKeys.StockMode)) ? s[SiteSettingKeys.StockMode] : StockModes.Status;
            decimal low = ParseDecimal(SiteSettingsStore.Get(s, SiteSettingKeys.LowStockQty)) ?? 10m;

            var settings = new SnapshotSettings
            {
                Enabled = SiteSettingsStore.Get(s, SiteSettingKeys.Enabled) == "1",
                Name = SiteSettingsStore.Get(s, SiteSettingKeys.Name),
                Tagline = SiteSettingsStore.Get(s, SiteSettingKeys.Tagline),
                LogoMediaId = ParseInt(SiteSettingsStore.Get(s, SiteSettingKeys.LogoMediaId)),
                PrimaryColor = SiteText.IsColor(SiteSettingsStore.Get(s, SiteSettingKeys.PrimaryColor)) ? s[SiteSettingKeys.PrimaryColor] : "#111827",
                Font = SiteSettingsStore.Get(s, SiteSettingKeys.Font, "Inter"),
                Radius = ParseInt(SiteSettingsStore.Get(s, SiteSettingKeys.Radius)) ?? 10,
                Languages = (SiteSettingsStore.Get(s, SiteSettingKeys.Languages, "uz,ru")).Split(',').Select(l => l.Trim()).Where(l => l == "uz" || l == "oz" || l == "ru").Distinct().ToList(),
                Phone = SiteSettingsStore.Get(s, SiteSettingKeys.Phone),
                Phone2 = SiteSettingsStore.Get(s, SiteSettingKeys.Phone2),
                Email = SiteSettingsStore.Get(s, SiteSettingKeys.Email),
                Address = SiteSettingsStore.Get(s, SiteSettingKeys.Address),
                WorkingHours = SiteSettingsStore.Get(s, SiteSettingKeys.WorkingHours),
                MapLat = ParseDecimal(SiteSettingsStore.Get(s, SiteSettingKeys.MapLat)),
                MapLng = ParseDecimal(SiteSettingsStore.Get(s, SiteSettingKeys.MapLng)),
                Telegram = SiteSettingsStore.Get(s, SiteSettingKeys.Telegram),
                Instagram = SiteSettingsStore.Get(s, SiteSettingKeys.Instagram),
                Facebook = SiteSettingsStore.Get(s, SiteSettingKeys.Facebook),
                PriceMode = priceMode,
                StockMode = stockMode,
                LeadsEnabled = SiteSettingsStore.Get(s, SiteSettingKeys.LeadsEnabled, "1") == "1",
                SeoTitle = SiteSettingsStore.Get(s, SiteSettingKeys.SeoTitle),
                SeoDescription = SiteSettingsStore.Get(s, SiteSettingKeys.SeoDescription),
                AnalyticsId = SiteSettingsStore.Get(s, SiteSettingKeys.AnalyticsId),
                MetrikaId = SiteSettingsStore.Get(s, SiteSettingKeys.MetrikaId),
                Currency = "so'm"
            };
            if (settings.Languages.Count == 0) settings.Languages.Add("uz");

            // ---------- tovarlar ----------
            var rows = (from c in db.SiteProducts.AsNoTracking()
                        join p in db.Products on c.ProductID equals p.ID
                        where c.IsPublished && !p.IsDeleted
                        select new
                        {
                            c, p.PNo, p.Name, p.CategoryID, p.UnitID,
                            Profile = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => new { pf.NameRu, pf.BrandID, pf.IsSaleActive }).FirstOrDefault(),
                            BaseUnit = db.Units.Where(u => u.ID == p.UnitID).Select(u => u.ShortName).FirstOrDefault()
                        }).ToList()
                        .Where(r => r.Profile == null || r.Profile.IsSaleActive).ToList();
            var ids = new HashSet<int>(rows.Select(r => r.c.ProductID));

            var extraUnits = (from pu in db.ProductUnits.AsNoTracking()
                              join c in db.SiteProducts on pu.ProductID equals c.ProductID
                              join u in db.Units on pu.UnitID equals u.ID
                              where c.IsPublished && pu.IsActive
                              select new { pu.ID, pu.ProductID, u.ShortName, pu.Factor, pu.IsDefaultSale }).ToList().ToLookup(x => x.ProductID);
            var barcodes = (from b in db.ProductBarcodes.AsNoTracking()
                            join c in db.SiteProducts on b.ProductID equals c.ProductID
                            where c.IsPublished
                            select new { b.ProductID, b.Barcode }).ToList().ToLookup(x => x.ProductID, x => x.Barcode);
            var attributes = (from a in db.SiteProductAttributes.AsNoTracking()
                              join c in db.SiteProducts on a.ProductID equals c.ProductID
                              where c.IsPublished
                              orderby a.SortOrder, a.ID
                              select a).ToList().ToLookup(a => a.ProductID);
            var gallery = (from m in db.SiteProductMedia.AsNoTracking()
                           join c in db.SiteProducts on m.ProductID equals c.ProductID
                           join md in db.SiteMedia on m.MediaID equals md.ID
                           where c.IsPublished
                           orderby m.SortOrder, m.MediaID
                           select new { m.ProductID, m.MediaID, md.AltUz, md.AltRu }).ToList().ToLookup(x => x.ProductID);

            Dictionary<int, decimal> prices = new Dictionary<int, decimal>();
            if (priceMode == PriceModes.Show)
            {
                int? priceType = ParseInt(SiteSettingsStore.Get(s, SiteSettingKeys.PriceTypeId)) ?? new PricingService(db).DefaultPriceTypeId();
                if (priceType.HasValue)
                    prices = (from pl in db.PriceListItems.AsNoTracking()
                              join c in db.SiteProducts on pl.ProductID equals c.ProductID
                              where c.IsPublished && pl.PriceTypeID == priceType && pl.ValidFrom <= now
                              group pl by pl.ProductID into g
                              select g.OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ID).FirstOrDefault())
                        .ToList().ToDictionary(x => x.ProductID, x => x.Price);
            }

            Dictionary<int, decimal> available = new Dictionary<int, decimal>();
            if (stockMode != StockModes.Hidden)
            {
                var warehouses = Distribution.Services.Orders.StockService.SalesWarehouses(db);
                var onHand = (from st in db.Stocks.AsNoTracking()
                              join c in db.SiteProducts on st.ProductID equals c.ProductID
                              where c.IsPublished && warehouses.Contains(st.WHID)
                              group st by st.ProductID into g
                              select new { ProductID = g.Key, Qty = g.Sum(x => x.Amount) }).ToList();
                var reserved = (from r in db.StockReservations.AsNoTracking()
                                join c in db.SiteProducts on r.ProductID equals c.ProductID
                                where c.IsPublished && warehouses.Contains(r.WarehouseID)
                                group r by r.ProductID into g
                                select new { ProductID = g.Key, Qty = g.Sum(x => x.QtyBase) }).ToList().ToDictionary(x => x.ProductID, x => x.Qty);
                available = onHand.ToDictionary(x => x.ProductID, x => x.Qty - (reserved.TryGetValue(x.ProductID, out var rq) ? rq : 0));
            }

            var products = rows.Select(r =>
            {
                prices.TryGetValue(r.c.ProductID, out var basePriceValue);
                decimal? basePrice = prices.ContainsKey(r.c.ProductID) ? basePriceValue : (decimal?)null;
                var extra = extraUnits[r.c.ProductID].OrderBy(u => u.Factor).ToList();
                var units = new List<SnapshotUnit>
                {
                    new SnapshotUnit { Id = null, Name = r.BaseUnit, Factor = 1m, Price = basePrice, IsDefault = !extra.Any(e => e.IsDefaultSale) }
                };
                units.AddRange(extra.Select(e => new SnapshotUnit
                {
                    Id = e.ID, Name = e.ShortName, Factor = e.Factor, IsDefault = e.IsDefaultSale,
                    Price = basePrice.HasValue ? UnitMath.UnitPrice(basePrice.Value, e.Factor) : (decimal?)null
                }));
                available.TryGetValue(r.c.ProductID, out var qty);
                var nameUz = r.c.TitleUz ?? r.Name;
                return new SnapshotProduct
                {
                    Id = r.c.ProductID, Slug = r.c.Slug, Sku = r.PNo, NameUz = nameUz, NameRu = r.c.TitleRu ?? r.Profile?.NameRu ?? nameUz,
                    DescriptionUz = r.c.DescriptionUz, DescriptionRu = r.c.DescriptionRu, CategoryId = r.CategoryID, BrandId = r.Profile?.BrandID,
                    Badges = string.IsNullOrEmpty(r.c.Badges) ? new List<string>() : r.c.Badges.Split(',').ToList(),
                    SortOrder = r.c.SortOrder, SeoTitle = r.c.SeoTitle, SeoDescription = r.c.SeoDescription,
                    Barcodes = barcodes[r.c.ProductID].ToList(), Units = units, Price = basePrice,
                    StockStatus = stockMode == StockModes.Hidden ? null : qty <= 0 ? "out" : qty <= low ? "low" : "in",
                    StockQty = stockMode == StockModes.Exact ? Math.Max(0, qty) : (decimal?)null,
                    Attributes = attributes[r.c.ProductID].Select(a => new SnapshotAttribute { NameUz = a.NameUz, NameRu = a.NameRu, ValueUz = a.ValueUz, ValueRu = a.ValueRu }).ToList(),
                    Media = gallery[r.c.ProductID].Select(m => new SnapshotProductMedia { Id = m.MediaID, AltUz = m.AltUz, AltRu = m.AltRu }).ToList(),
                    UpdatedAt = r.c.UpdatedAt
                };
            }).OrderBy(p => p.SortOrder).ThenBy(p => p.NameUz).ToList();

            // ---------- kategoriyalar, brendlar, sahifalar, bannerlar ----------
            var categories = (from c in db.SiteCategories.AsNoTracking()
                              join pc in db.Categories on c.CategoryID equals pc.ID
                              where c.IsPublished && pc.IsDeleted != true
                              select new { c, pc.Name }).ToList()
                .Select(x => new SnapshotCategory
                {
                    Id = x.c.CategoryID, Slug = x.c.Slug, NameUz = x.c.NameUz ?? x.Name, NameRu = x.c.NameRu ?? x.c.NameUz ?? x.Name,
                    DescriptionUz = x.c.DescriptionUz, DescriptionRu = x.c.DescriptionRu, MediaId = x.c.MediaID, SortOrder = x.c.SortOrder,
                    SeoTitle = x.c.SeoTitle, SeoDescription = x.c.SeoDescription
                }).OrderBy(c => c.SortOrder).ThenBy(c => c.NameUz).ToList();
            var brandIds = products.Where(p => p.BrandId.HasValue).Select(p => p.BrandId.Value).Distinct().ToList();
            var brands = db.Brands.AsNoTracking().Where(b => brandIds.Contains(b.ID)).OrderBy(b => b.Name)
                .Select(b => new SnapshotBrand { Id = b.ID, Name = b.Name }).ToList();
            var pages = db.SitePages.AsNoTracking().Where(p => p.IsPublished).OrderBy(p => p.SortOrder).ThenBy(p => p.TitleUz).ToList()
                .Select(p => new SnapshotPage { Slug = p.Slug, TitleUz = p.TitleUz, TitleRu = p.TitleRu ?? p.TitleUz, BodyUz = p.BodyUz, BodyRu = p.BodyRu, ShowInMenu = p.ShowInMenu, SortOrder = p.SortOrder })
                .ToList();
            var today = now.Date;
            var banners = db.SiteBanners.AsNoTracking()
                .Where(b => b.IsActive && (b.ActiveFrom == null || b.ActiveFrom <= today) && (b.ActiveTo == null || b.ActiveTo >= today))
                .OrderBy(b => b.SortOrder).ThenBy(b => b.ID).ToList()
                .Select(b => new SnapshotBanner { MediaId = b.MediaID, TitleUz = b.TitleUz, TitleRu = b.TitleRu ?? b.TitleUz, SubtitleUz = b.SubtitleUz, SubtitleRu = b.SubtitleRu ?? b.SubtitleUz, Link = b.Link, SortOrder = b.SortOrder })
                .ToList();
            var domains = db.SiteDomains.AsNoTracking().Where(d => d.Status == (byte)DomainStatus.Active).OrderByDescending(d => d.IsPrimary).ThenBy(d => d.Host)
                .Select(d => new SnapshotDomain { Host = d.Host, IsPrimary = d.IsPrimary }).ToList();

            var mediaIds = new HashSet<int>(products.SelectMany(p => p.Media.Select(m => m.Id))
                .Concat(categories.Where(c => c.MediaId.HasValue).Select(c => c.MediaId.Value))
                .Concat(banners.Select(b => b.MediaId)));
            if (settings.LogoMediaId.HasValue) mediaIds.Add(settings.LogoMediaId.Value);
            var mediaList = mediaIds.ToList();
            var media = db.SiteMedia.AsNoTracking().Where(m => mediaList.Contains(m.ID))
                .Select(m => new SnapshotMedia { Id = m.ID, Hash = m.Hash, Width = m.Width, Height = m.Height, ContentType = m.ContentType })
                .ToList().OrderBy(m => m.Id).ToList();

            return new SnapshotDto
            {
                GeneratedAt = now, Settings = settings, Domains = domains, Categories = categories, Brands = brands,
                Products = products, Pages = pages, Banners = banners, Media = media
            };
        }

        /// <summary>Barqaror ETag uchun: GeneratedAt hisobga olinmaydi (vaqt o'zgarishi "yangi versiya" bo'lmasin).</summary>
        public static (string Json, string ETag) Serialize(SnapshotDto snapshot)
        {
            var generated = snapshot.GeneratedAt;
            snapshot.GeneratedAt = default(DateTime);
            var stable = JsonConvert.SerializeObject(snapshot, Json);
            snapshot.GeneratedAt = generated;
            var etag = "\"" + SiteMediaService.Sha256Hex(Encoding.UTF8.GetBytes(stable)).Substring(0, 32) + "\"";
            return (JsonConvert.SerializeObject(snapshot, Json), etag);
        }

        private static int? ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null;

        private static decimal? ParseDecimal(string s) =>
            decimal.TryParse(s?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null;
    }

    /// <summary>
    /// Nusxa keshi (tenant bo'yicha, 60 soniya): sayt har daqiqada so'raydi, ERP har safar qayta hisoblamaydi.
    /// Admin panelda saqlanganda — darhol yangilanadi (Invalidate).
    /// </summary>
    public static class SiteSnapshotCache
    {
        public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private sealed class Entry
        {
            public string Json;
            public string ETag;
            public DateTime BuiltAt;
        }

        private static readonly ConcurrentDictionary<string, Entry> Entries = new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, object> Locks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static string CurrentTenant => tahsinERP.Configuration.Tenancy.TenantContext.CurrentOrNull?.Slug ?? "default";

        public static (string Json, string ETag) Get(string tenant, Func<(string Json, string ETag)> build)
        {
            if (Entries.TryGetValue(tenant, out var e) && DateTime.UtcNow - e.BuiltAt < Ttl) return (e.Json, e.ETag);
            lock (Locks.GetOrAdd(tenant, _ => new object()))
            {
                if (Entries.TryGetValue(tenant, out e) && DateTime.UtcNow - e.BuiltAt < Ttl) return (e.Json, e.ETag);
                var built = build();
                Entries[tenant] = new Entry { Json = built.Json, ETag = built.ETag, BuiltAt = DateTime.UtcNow };
                return built;
            }
        }

        public static void Invalidate(string tenant = null) => Entries.TryRemove(tenant ?? CurrentTenant, out _);
    }
}
