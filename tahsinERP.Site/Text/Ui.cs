using System.Net;
using Microsoft.AspNetCore.Html;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Media;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Text;

/// <summary>Ko'rinish yordamchilari: narx, qoldiq belgisi, rasm (srcset, lazy), birliklar.</summary>
public static class Ui
{
    public static SnapshotUnit? DefaultUnit(SnapshotProduct p) =>
        p.Units?.FirstOrDefault(u => u.IsDefault) ?? p.Units?.FirstOrDefault();

    /// <summary>Kartochka va sahifadagi narx: standart birlik bo'yicha ("72 000 so'm / qop") yoki "Narxni so'rang".</summary>
    public static IHtmlContent Price(SiteContext s, SnapshotProduct p, string cssClass = "price")
    {
        if (s.Catalog.Settings.PriceMode == "HIDDEN") return HtmlString.Empty;
        var unit = DefaultUnit(p);
        if (!s.ShowPrices || unit?.Price == null)
            return new HtmlString($"<span class=\"{cssClass} price-ask\">{E(s.T("PriceAsk"))}</span>");
        return new HtmlString($"<span class=\"{cssClass}\" data-price>{E(SafeText.Money(unit.Price.Value, s.Lang))}<span class=\"per\"> / {E(unit.Name)}</span></span>");
    }

    public static IHtmlContent Stock(SiteContext s, SnapshotProduct p)
    {
        if (p.StockStatus == null) return HtmlString.Empty;
        var (key, cls) = p.StockStatus switch { "in" => ("StockIn", "in"), "low" => ("StockLow", "low"), _ => ("StockOut", "out") };
        var qty = p.StockQty.HasValue && p.StockStatus != "out"
            ? " · " + SafeText.Qty(p.StockQty.Value) + " " + E(p.Units?.FirstOrDefault(u => u.Id == null)?.Name ?? "") : "";
        return new HtmlString($"<span class=\"stock stock-{cls}\"><i></i>{E(s.T(key))}{qty}</span>");
    }

    public static IHtmlContent Badges(SiteContext s, SnapshotProduct p)
    {
        if (p.Badges is not { Count: > 0 }) return HtmlString.Empty;
        return new HtmlString("<div class=\"badges\">" + string.Concat(p.Badges.Select(b =>
            $"<span class=\"badge badge-{E(b.ToLowerInvariant())}\">{E(s.T("Badge_" + b))}</span>")) + "</div>");
    }

    /// <summary>&lt;img&gt; srcset bilan. Rasm bo'lmasa — joy egallovchi (tovar nomining bosh harfi).</summary>
    public static IHtmlContent Image(SiteContext s, int? mediaId, string alt, string sizes, int[] widths, bool eager = false, string cssClass = "")
    {
        if (mediaId is not int id || !s.Catalog.Media.TryGetValue(id, out var m))
        {
            var letter = string.IsNullOrWhiteSpace(alt) ? "•" : alt.Trim()[..1].ToUpperInvariant();
            return new HtmlString($"<div class=\"img-placeholder {cssClass}\" aria-hidden=\"true\"><span>{E(letter)}</span></div>");
        }
        var src = MediaCache.Url(m, widths[Math.Min(1, widths.Length - 1)]);
        return new HtmlString($"<img class=\"{cssClass}\" src=\"{src}\" srcset=\"{MediaCache.SrcSet(m, widths)}\" sizes=\"{E(sizes)}\" alt=\"{E(alt)}\" " +
                              $"width=\"{m.Width}\" height=\"{m.Height}\" {(eager ? "fetchpriority=\"high\"" : "loading=\"lazy\"")} decoding=\"async\">");
    }

    /// <summary>Savat JS uchun tovar ma'lumoti (data-product atributi): id, birliklar va narxlar.</summary>
    public static string ProductData(SiteContext s, SnapshotProduct p) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            id = p.Id,
            units = (p.Units ?? []).Select(u => new { id = u.Id, name = u.Name, factor = u.Factor, price = s.ShowPrices ? u.Price : null, def = u.IsDefault })
        });

    public static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
