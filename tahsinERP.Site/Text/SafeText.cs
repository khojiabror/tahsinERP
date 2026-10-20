using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace tahsinERP.Site.Text;

/// <summary>
/// ERP'da kiritilgan tavsif matnini xavfsiz HTML'ga aylantiradi (HTML kiritib bo'lmaydi — hammasi kodlanadi):
/// bo'sh qator — paragraf, "- " yoki "• " — ro'yxat, "## " — kichik sarlavha, **qalin**, qator ichidagi yangi qator — &lt;br&gt;.
/// </summary>
public static partial class SafeText
{
    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    public static IHtmlContent Render(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return HtmlString.Empty;
        var sb = new StringBuilder();
        var blocks = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in blocks)
        {
            var lines = raw.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) continue;
            if (lines.All(IsBullet))
            {
                sb.Append("<ul>");
                foreach (var l in lines) sb.Append("<li>").Append(Inline(l.TrimStart()[2..].Trim())).Append("</li>");
                sb.Append("</ul>");
            }
            else if (lines.Count == 1 && lines[0].StartsWith("## "))
            {
                sb.Append("<h3>").Append(Inline(lines[0][3..].Trim())).Append("</h3>");
            }
            else
            {
                sb.Append("<p>").Append(string.Join("<br>", lines.Select(Inline))).Append("</p>");
            }
        }
        return new HtmlString(sb.ToString());
    }

    /// <summary>Meta description uchun: belgilarsiz, bitta qator, uzunligi cheklangan.</summary>
    public static string Plain(string? text, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = Bold().Replace(text, "$1").Replace("## ", "").Replace("\n- ", " ").Replace("\n• ", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim().TrimStart('-', '•', ' ');
        return s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
    }

    private static bool IsBullet(string l)
    {
        var t = l.TrimStart();
        return t.StartsWith("- ") || t.StartsWith("• ");
    }

    private static string Inline(string s) => Bold().Replace(WebUtility.HtmlEncode(s), "<strong>$1</strong>");

    // ---------- pul va miqdor ----------

    private static readonly NumberFormatInfo Group = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = "," };

    public static string Money(decimal value, string lang) =>
        Math.Round(value, value % 1 == 0 ? 0 : 2).ToString(value % 1 == 0 ? "#,0" : "#,0.00", Group) + " " + (lang == "ru" ? "сум" : "so'm");

    public static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
