using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Models;

namespace tahsinERP.Services.Common
{
    /// <summary>Joriy korxona brendi (_Layout: nom, asosiy rang, logo).</summary>
    public sealed class BrandInfo
    {
        public string CompanyName { get; set; }
        public string Primary { get; set; }
        public string PrimaryHover { get; set; }
        public string PrimaryDark { get; set; }
        public string PrimaryLight { get; set; }
        public bool HasLogo { get; set; }
        /// <summary>Logo URL'ini keshdan chiqarish uchun (logo almashsa o'zgaradi).</summary>
        public string LogoVersion { get; set; }
    }

    /// <summary>
    /// Brend COMPANIES dan (Name, BrandColor, LogoImage — migratsiya 005); ustunlar EDMX'da yo'q, xom SQL.
    /// Bo'sh qiymatlar = standart ko'rinish (yashil #198754, app-assets logosi). Tenant bo'yicha 10 daqiqa keshlanadi;
    /// Sozlamalar -> Korxona ma'lumotlari saqlanganda <see cref="Invalidate"/>.
    /// </summary>
    public static class BrandingService
    {
        public const string DefaultColor = "#198754";
        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.Branding");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private const string Key = "Brand";

        private sealed class Row
        {
            public string Name { get; set; }
            public string BrandColor { get; set; }
            public int LogoLength { get; set; }
        }

        // Ustunlar hali qo'shilmagan bazada ham ishlaydi (005 qo'llanmagan bo'lsa — standart brend)
        private const string Sql =
            "IF COL_LENGTH('dbo.COMPANIES', 'BrandColor') IS NULL " +
            "    SELECT TOP 1 Name, CAST(NULL AS varchar(7)) AS BrandColor, 0 AS LogoLength FROM dbo.COMPANIES WHERE ID = @p0 " +
            "ELSE " +
            "    EXEC sp_executesql N'SELECT TOP 1 Name, BrandColor, ISNULL(CAST(DATALENGTH(LogoImage) AS int), 0) AS LogoLength FROM dbo.COMPANIES WHERE ID = @id', N'@id int', @id = @p0";

        public static BrandInfo Current()
        {
            string key = TenantContext.CacheKey(Key);
            if (Cache.Get(key) is BrandInfo cached)
                return cached;

            Row row = null;
            try
            {
                using (var db = new DBTHSNEntities())
                    row = db.Database.SqlQuery<Row>(Sql, LicenseService.InstallationCompanyId).FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("[Branding] o'qilmadi: " + ex.Message);
            }

            var brand = Build(row?.Name, row?.BrandColor, row != null && row.LogoLength > 0, row?.LogoLength ?? 0);
            Cache.Set(key, brand, DateTimeOffset.Now.Add(Ttl));
            return brand;
        }

        public static void Invalidate()
        {
            Cache.Remove(TenantContext.CacheKey(Key));
        }

        public static bool IsValidColor(string color)
        {
            return color != null && Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");
        }

        private static BrandInfo Build(string name, string color, bool hasLogo, int logoLength)
        {
            var brand = new BrandInfo
            {
                CompanyName = string.IsNullOrWhiteSpace(name) ? "tahsinERP" : name.Trim(),
                HasLogo = hasLogo,
                LogoVersion = logoLength.ToString(CultureInfo.InvariantCulture)
            };
            if (!IsValidColor(color) || string.Equals(color, DefaultColor, StringComparison.OrdinalIgnoreCase))
            {
                // Avvalgi (iPack) ko'rinish aynan saqlanadi
                brand.Primary = DefaultColor;
                brand.PrimaryHover = "#157347";
                brand.PrimaryDark = "#146c43";
                brand.PrimaryLight = "rgba(25, 135, 84, 0.15)";
                return brand;
            }
            int r = Convert.ToInt32(color.Substring(1, 2), 16), g = Convert.ToInt32(color.Substring(3, 2), 16), b = Convert.ToInt32(color.Substring(5, 2), 16);
            brand.Primary = color.ToLowerInvariant();
            brand.PrimaryHover = Shade(r, g, b, 0.85);
            brand.PrimaryDark = Shade(r, g, b, 0.80);
            brand.PrimaryLight = string.Format(CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, 0.15)", r, g, b);
            return brand;
        }

        private static string Shade(int r, int g, int b, double k)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", (int)(r * k), (int)(g * k), (int)(b * k));
        }
    }
}
