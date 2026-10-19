using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Traction;

/// <summary>
/// Traction: tenantlar qancha ishlayapti, qaysi modullar faol, nimaning ustida ishlash kerak (07_TRACTION.md).
/// Faqat o'qish — barcha xodimlar ko'radi (Support ham). Agregat jadvallardan o'qiydi.
/// </summary>
public class IndexModel(TractionService traction, Clock clock) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 30;
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public TenantStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "score";

    public TractionService.Report R { get; private set; } = null!;
    public List<TractionService.TenantRow> Rows { get; private set; } = [];
    public string ChartJson { get; private set; } = "{}";

    private TractionService.Filter BuildFilter()
    {
        DateOnly to = To ?? clock.Today;
        DateOnly from = From ?? to.AddDays(-(Days is 7 or 30 or 90 ? Days : 30) + 1);
        if (from > to) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 730) from = to.AddDays(-730);
        return new TractionService.Filter(from, to, string.IsNullOrWhiteSpace(Plan) ? null : Plan, Status);
    }

    public async Task OnGetAsync()
    {
        R = await traction.BuildAsync(BuildFilter());
        Rows = SortRows(R.Tenants).ToList();
        ChartJson = JsonSerializer.Serialize(new
        {
            active = new { labels = R.ActiveUsersDaily.Labels, series = R.ActiveUsersDaily.Data },
            modules = new { labels = R.ModulesDaily.Labels, series = R.ModulesDaily.Data },
            moduleTotals = new { labels = R.Modules.Where(m => m.Actions > 0).Select(m => m.Name), values = R.Modules.Where(m => m.Actions > 0).Select(m => m.Actions) },
            funnel = new { labels = R.Funnel.Select(x => x.Name), values = R.Funnel.Select(x => x.Count) },
        });
    }

    private IEnumerable<TractionService.TenantRow> SortRows(IEnumerable<TractionService.TenantRow> rows) => Sort switch
    {
        "name" => rows.OrderBy(r => r.Tenant.LegalName),
        "mrr" => rows.OrderByDescending(r => r.MrrTiyin),
        "last" => rows.OrderByDescending(r => r.LastLoginUtc ?? DateTime.MinValue),
        "actions" => rows.OrderByDescending(r => r.Actions),
        "risk" or "score" => rows.OrderBy(r => r.Score),
        _ => rows.OrderBy(r => r.Score),
    };

    /// <summary>CSV eksport: section = tenants | modules | pages | cohorts.</summary>
    public async Task<IActionResult> OnGetCsvAsync(string section)
    {
        var r = await traction.BuildAsync(BuildFilter());
        var sb = new StringBuilder();
        static string Q(object? v) { string s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""; return s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }
        void Line(params object?[] cells) => sb.AppendLine(string.Join(";", cells.Select(Q)));
        switch (section)
        {
            case "modules":
                Line("Modul", "Nomi", "Tenantlar", "Amallar", "Oldingi davr", "Trend %", "Paketda bor, ishlatilmagan (tenant)");
                foreach (var m in r.Modules) Line(m.Module, m.Name, m.Tenants, m.Actions, m.PrevActions, m.TrendPct, m.UnusedInPackage);
                break;
            case "pages":
                Line("Controller", "Modul", "Amal turi", "Amallar", "Tenantlar", "Xaritada");
                foreach (var p in r.Pages) Line(p.Controller, p.Module, p.ActionType, p.Actions, p.Tenants, p.Mapped ? "ha" : "yo'q");
                break;
            case "cohorts":
                Line(new object?[] { "Oy", "Tenantlar" }.Concat(Enumerable.Range(0, 12).Select(w => (object?)("H" + w))).ToArray());
                foreach (var c in r.Cohorts) Line(new object?[] { c.Month, c.Size }.Concat(c.Weeks.Select(w => (object?)w)).ToArray());
                break;
            default:
                Line("Slug", "Nomi", "Holat", "Paket", "MRR (so'm)", "Oxirgi kirish (UTC)", "Faol foydalanuvchi", "Limit", "Modullar", "Ruxsat", "Amallar", "Oldingi", "Hujjatlar", "Ball", "Xavf", "Ishlatilmagan modullar");
                foreach (var t in SortRows(r.Tenants))
                    Line(t.Tenant.Slug, t.Tenant.LegalName, t.Tenant.Status, t.PlanCode, t.MrrTiyin / 100, t.LastLoginUtc?.ToString("yyyy-MM-dd HH:mm"), t.ActiveUsers, t.SeatLimit,
                        t.ModulesUsed, t.ModulesAllowed, t.Actions, t.PrevActions, t.Docs, t.Score, t.Risk, string.Join(",", t.UnusedModules));
                break;
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"traction-{section ?? "tenants"}-{r.Filter.From:yyyyMMdd}-{r.Filter.To:yyyyMMdd}.csv");
    }

    public string Query(string? sort = null, string? csv = null)
    {
        var q = new List<string>();
        if (From != null || To != null) { if (From != null) q.Add("From=" + From.Value.ToString("yyyy-MM-dd")); if (To != null) q.Add("To=" + To.Value.ToString("yyyy-MM-dd")); }
        else q.Add("Days=" + Days);
        if (!string.IsNullOrEmpty(Plan)) q.Add("Plan=" + Uri.EscapeDataString(Plan));
        if (Status != null) q.Add("Status=" + Status);
        q.Add("Sort=" + (sort ?? Sort));
        if (csv != null) { q.Add("handler=Csv"); q.Add("section=" + csv); }
        return "?" + string.Join("&", q);
    }
}
