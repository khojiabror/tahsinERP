using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Leads;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Pages;

/// <summary>
/// Savat (brauzerda saqlanadi) va ariza. Qatorlar serverda chiziladi (?handler=Lines&amp;i=id:birlik:miqdor) —
/// narx va nom har doim joriy nusxadan. Yuborish — oddiy POST forma (JS'siz ham ishlaydi, savat esa yashirin maydonda).
/// </summary>
[IgnoreAntiforgeryToken(Order = 1001)]   // ochiq forma, cookie yo'q; himoya — honeypot, vaqt va chegaralar (LeadService)
public sealed class CartModel(SiteContext site, LeadService leads) : SitePageModel(site)
{
    public sealed record Line(SnapshotProduct Product, SnapshotUnit Unit, decimal Qty, decimal? Sum);

    public List<Line> Lines { get; private set; } = [];
    public decimal? Total { get; private set; }
    public bool Sent { get; private set; }
    public string? SentPhone { get; private set; }
    public string? Error { get; private set; }
    [BindProperty] public LeadForm? Form { get; set; }

    public void OnGet()
    {
        Title = FullTitle(T("Cart"));
        NoIndex = true;
    }

    public IActionResult OnGetLines()
    {
        Lines = Parse(Request.Query["i"]);
        Total = Lines.Count > 0 && Lines.All(l => l.Sum.HasValue) ? Lines.Sum(l => l.Sum!.Value) : null;
        return Partial("_CartLines", this);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Title = FullTitle(T("Cart"));
        NoIndex = true;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var result = await leads.SubmitAsync(Site.Tenant!, Catalog, Form ?? new LeadForm(null, null, null, null, null, null, null, null, null), Lang, Site.Host, ip, ct);
        if (result.Ok)
        {
            Sent = true;
            SentPhone = result.Phone;
        }
        else
        {
            Error = T(result.ErrorKey ?? "Err_Cart");
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        }
        return Page();
    }

    /// <summary>"12:7:3" → tovar 12, birlik 7, miqdor 3; "12::2" — asosiy birlik. Noma'lumlari tashlanadi.</summary>
    internal List<Line> Parse(IEnumerable<string?> items)
    {
        var result = new List<Line>();
        foreach (var raw in items.Take(100))
        {
            var parts = (raw ?? "").Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var id)) continue;
            int? unitId = int.TryParse(parts[1], out var u) ? u : null;
            if (!decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0 || qty > 1_000_000) continue;
            var p = Catalog.ProductById(id);
            var unit = p?.Units?.FirstOrDefault(x => x.Id == unitId);
            if (p == null || unit == null) continue;
            decimal? sum = Site.ShowPrices && unit.Price.HasValue ? Math.Round(unit.Price.Value * qty, 2) : null;
            result.Add(new Line(p, unit, qty, sum));
        }
        return result;
    }
}
