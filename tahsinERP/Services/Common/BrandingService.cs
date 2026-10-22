using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Models;

namespace tahsinERP.Services.Common
{
    /// <summary>Joriy korxona brendi (_Layout: nom, asosiy rang, logo).</summary>
    public sealed class BrandInfo
    {
        public string CompanyName { get; set; }
        /// <summary>Tanlangan asosiy rang (#rrggbb, kichik harf).</summary>
        public string Primary { get; set; }
        /// <summary>Rang korxona tomonidan tanlanganmi (false — standart neytral).</summary>
        public bool IsCustomColor { get; set; }
        public AccentTokens Tokens { get; set; }
        public bool HasLogo { get; set; }
        /// <summary>Logo URL'ini keshdan chiqarish uchun (logo almashsa o'zgaradi).</summary>
        public string LogoVersion { get; set; }
    }

    /// <summary>
    /// Asosiy rangdan hosil qilingan CSS o'zgaruvchilari (assets/css/theme.css ularni ishlatadi).
    /// Light — kunduzgi rejim, Dark* — tungi rejim (fon to'q, matn rangi yorituladi).
    /// </summary>
    public sealed class AccentTokens
    {
        public string Accent { get; set; }          // --accent: tugma/fon
        public string Accent2 { get; set; }         // --accent-2: gradient oxiri
        public string Hover { get; set; }           // --accent-hover
        public string Active { get; set; }          // --accent-active
        public string Contrast { get; set; }        // --accent-contrast: accent fonidagi matn
        public string Soft { get; set; }            // --accent-soft: och fon (menyu aktiv, badge-light)
        public string Ring { get; set; }            // --accent-ring: fokus halqasi
        public string Text { get; set; }            // --accent-text: oq fondagi havola/sarlavha
        public string TextHover { get; set; }
        public string TextVisited { get; set; }     // --accent-text-visited
        public string TableLink { get; set; }       // --accent-table-link: DataTables ichidagi havola
        public string TableLinkHover { get; set; }
        public string DarkText { get; set; }        // tungi rejim: --accent-text
        public string DarkTextHover { get; set; }
        public string DarkSoft { get; set; }
        public string DarkTableLink { get; set; }
        public string DarkTableLinkHover { get; set; }
    }

    /// <summary>Admin palitrasidagi tayyor rang.</summary>
    public sealed class BrandPreset
    {
        public BrandPreset(string name, string hex) { Name = name; Hex = hex; }
        public string Name { get; }
        public string Hex { get; }
    }

    /// <summary>
    /// Brend COMPANIES dan (Name, BrandColor, LogoImage — migratsiya 005); ustunlar EDMX'da yo'q, xom SQL.
    /// BrandColor NULL = standart neytral rang (<see cref="DefaultColor"/>). Yashil (<see cref="LegacyGreen"/>) tanlansa —
    /// avvalgi iPack ko'rinishi aynan qaytadi. Tenant bo'yicha 10 daqiqa keshlanadi;
    /// Sozlamalar -> Korxona ma'lumotlari saqlanganda <see cref="Invalidate"/>.
    /// </summary>
    public static class BrandingService
    {
        /// <summary>Standart (neytral) rang — boshqaruv paneli bilan bir xil indigo.</summary>
        public const string DefaultColor = "#3b5bfd";
        /// <summary>Avvalgi qattiq yozilgan yashil (iPack).</summary>
        public const string LegacyGreen = "#198754";

        public static readonly IReadOnlyList<BrandPreset> Presets = new[]
        {
            new BrandPreset("Indigo (standart)", DefaultColor),
            new BrandPreset("Yashil", LegacyGreen),
            new BrandPreset("Ko'k", "#1f6feb"),
            new BrandPreset("Moviy", "#0e7c86"),
            new BrandPreset("Binafsha", "#7c3aed"),
            new BrandPreset("Qizil", "#c2253b"),
            new BrandPreset("To'q sariq", "#d9480f"),
            new BrandPreset("Grafit", "#475569"),
        };

        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.Branding");
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private const string Key = "Brand";

        // Tungi rejim fonlari (theme-dark.css: sahifa #1a233a, karta #272e48) — matn kontrasti shunga qarab
        private static readonly Rgb DarkSurface = new Rgb(0x27, 0x2e, 0x48);
        private static readonly Rgb White = new Rgb(255, 255, 255);

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

        /// <summary>Bazaga yoziladigan qiymat: standart rang — NULL (standart keyin o'zgarsa ham ergashadi).</summary>
        public static string ToStoredValue(string color)
        {
            if (!IsValidColor(color) || string.Equals(color, DefaultColor, StringComparison.OrdinalIgnoreCase))
                return null;
            return color.ToUpperInvariant();
        }

        private static BrandInfo Build(string name, string color, bool hasLogo, int logoLength)
        {
            bool custom = IsValidColor(color) && !string.Equals(color, DefaultColor, StringComparison.OrdinalIgnoreCase);
            string primary = custom ? color.ToLowerInvariant() : DefaultColor;
            return new BrandInfo
            {
                CompanyName = string.IsNullOrWhiteSpace(name) ? "tahsinERP" : name.Trim(),
                Primary = primary,
                IsCustomColor = custom,
                Tokens = BuildTokens(primary),
                HasLogo = hasLogo,
                LogoVersion = logoLength.ToString(CultureInfo.InvariantCulture)
            };
        }

        /// <summary>Rangdan barcha hosilalar. Noto'g'ri qiymat — standart rang.</summary>
        public static AccentTokens BuildTokens(string color)
        {
            if (!IsValidColor(color))
                color = DefaultColor;
            var c = Rgb.Parse(color);

            if (string.Equals(color, LegacyGreen, StringComparison.OrdinalIgnoreCase))
            {
                // Avvalgi (iPack) _Layout qiymatlari aynan
                return new AccentTokens
                {
                    Accent = LegacyGreen,
                    Accent2 = "#20c997",
                    Hover = "#157347",
                    Active = "#146c43",
                    Contrast = "#fff",
                    Soft = "rgba(25, 135, 84, 0.15)",
                    Ring = "rgba(25, 135, 84, 0.25)",
                    Text = LegacyGreen,
                    TextHover = "#157347",
                    TextVisited = "#146c43",
                    TableLink = "#33cc00",
                    TableLinkHover = "#2db300",
                    DarkText = "#3ccf86",
                    DarkTextHover = "#6ee0a6",
                    DarkSoft = "rgba(60, 207, 134, 0.16)",
                    DarkTableLink = "#4ade3a",
                    DarkTableLinkHover = "#7be86f",
                };
            }

            // Oq matn accent fonida o'qiladimi (tugma — yirik/qalin matn, 3:1 yetarli)
            bool whiteOnAccent = Contrast(White, c) >= 3.0;
            var text = EnsureContrast(c, White, 4.5, toward: new Rgb(0, 0, 0));
            var darkText = EnsureContrast(c, DarkSurface, 4.5, toward: White);
            return new AccentTokens
            {
                Accent = c.Hex,
                Accent2 = c.Mix(White, 0.22).Hex,
                Hover = c.Scale(0.85).Hex,
                Active = c.Scale(0.80).Hex,
                Contrast = whiteOnAccent ? "#fff" : "#1b2333",
                Soft = c.Rgba(0.15),
                Ring = c.Rgba(0.25),
                Text = text.Hex,
                TextHover = text.Scale(0.85).Hex,
                TextVisited = text.Scale(0.80).Hex,
                TableLink = text.Hex,
                TableLinkHover = text.Scale(0.85).Hex,
                DarkText = darkText.Hex,
                DarkTextHover = darkText.Mix(White, 0.25).Hex,
                DarkSoft = darkText.Rgba(0.16),
                DarkTableLink = darkText.Hex,
                DarkTableLinkHover = darkText.Mix(White, 0.25).Hex,
            };
        }

        /// <summary>_Layout &lt;style&gt; ichiga qo'yiladigan :root o'zgaruvchilari (kunduzgi + tungi).</summary>
        public static string RenderCssVariables(AccentTokens t)
        {
            var sb = new StringBuilder();
            sb.Append(":root{")
              .Append("--accent:").Append(t.Accent).Append(';')
              .Append("--accent-2:").Append(t.Accent2).Append(';')
              .Append("--accent-hover:").Append(t.Hover).Append(';')
              .Append("--accent-active:").Append(t.Active).Append(';')
              .Append("--accent-contrast:").Append(t.Contrast).Append(';')
              .Append("--accent-ring:").Append(t.Ring).Append(';')
              .Append("--accent-soft:").Append(t.Soft).Append(';')
              .Append("--accent-text:").Append(t.Text).Append(';')
              .Append("--accent-text-hover:").Append(t.TextHover).Append(';')
              .Append("--accent-text-visited:").Append(t.TextVisited).Append(';')
              .Append("--accent-table-link:").Append(t.TableLink).Append(';')
              .Append("--accent-table-link-hover:").Append(t.TableLinkHover).Append(';')
              .Append("}\n:root[data-theme=\"dark\"]{")
              .Append("--accent-soft:").Append(t.DarkSoft).Append(';')
              .Append("--accent-text:").Append(t.DarkText).Append(';')
              .Append("--accent-text-hover:").Append(t.DarkTextHover).Append(';')
              .Append("--accent-text-visited:").Append(t.DarkText).Append(';')
              .Append("--accent-table-link:").Append(t.DarkTableLink).Append(';')
              .Append("--accent-table-link-hover:").Append(t.DarkTableLinkHover).Append(';')
              .Append('}');
            return sb.ToString();
        }

        /// <summary>WCAG nisbiy kontrast (1..21).</summary>
        public static double Contrast(string a, string b)
        {
            return Contrast(Rgb.Parse(a), Rgb.Parse(b));
        }

        private static double Contrast(Rgb a, Rgb b)
        {
            double la = a.Luminance(), lb = b.Luminance();
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        // Rangni `toward` tomon bosqichma-bosqich surib, `bg` ga nisbatan kerakli kontrastga yetkazadi
        private static Rgb EnsureContrast(Rgb c, Rgb bg, double min, Rgb toward)
        {
            var x = c;
            for (int i = 1; i <= 20 && Contrast(x, bg) < min; i++)
                x = c.Mix(toward, i * 0.05);
            return x;
        }

        private struct Rgb
        {
            public readonly int R, G, B;
            public Rgb(int r, int g, int b) { R = Clamp(r); G = Clamp(g); B = Clamp(b); }

            public static Rgb Parse(string hex)
            {
                return new Rgb(Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
            }

            public string Hex => string.Format("#{0:x2}{1:x2}{2:x2}", R, G, B);

            public string Rgba(double a)
            {
                return string.Format(CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})", R, G, B, a);
            }

            public Rgb Scale(double k) => new Rgb((int)(R * k), (int)(G * k), (int)(B * k));

            public Rgb Mix(Rgb o, double t) => new Rgb(
                (int)Math.Round(R + (o.R - R) * t), (int)Math.Round(G + (o.G - G) * t), (int)Math.Round(B + (o.B - B) * t));

            public double Luminance()
            {
                return 0.2126 * Lin(R) + 0.7152 * Lin(G) + 0.0722 * Lin(B);
            }

            private static double Lin(int v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
        }
    }
}
