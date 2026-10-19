using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Options;

namespace tahsinERP.ControlPanel.Hosting;

/// <summary>
/// Bitta ilova, ikki zona: xodimlar paneli (<c>admin.&lt;domen&gt;</c>) va mijoz portali (<c>my.&lt;domen&gt;</c>).
/// Zona so'rov host'idan aniqlanadi; har sahifa endpoint'iga zona belgisi qo'yiladi va boshqa zonadagi host'da
/// u sahifa umuman topilmaydi (404). API'lar (License/Agent/Telegram webhook, /health) zonasiz — har host'da.
/// </summary>
public enum Zone { None = 0, Admin = 1, Portal = 2 }

public class PanelHostOptions
{
    /// <summary>Asosiy domen (masalan "tahsinerp.uz"; nomi hali tanlanmagan). Lokal: "localhost" → admin.localhost / my.localhost.</summary>
    public string Domain { get; set; } = "localhost";
    /// <summary>Xodimlar host'i. Bo'sh — "admin." + Domain.</summary>
    public string? AdminHost { get; set; }
    /// <summary>Mijoz portali host'i. Bo'sh — "my." + Domain.</summary>
    public string? PortalHost { get; set; }
    /// <summary>
    /// Boshqa host'lar (masalan "localhost", IP) qaysi zonaga tushadi: "Admin" (lokal ishlab chiqish, eski havolalar) yoki
    /// "None" (serverda tavsiya — hech qaysi sahifa ochilmaydi, faqat API).
    /// </summary>
    public Zone OtherHosts { get; set; } = Zone.Admin;
    /// <summary>Taklif havolalari uchun portal manzili (masalan "https://my.tahsinerp.uz"). Bo'sh — joriy so'rov sxemasi/porti + PortalHost.</summary>
    public string? PortalPublicUrl { get; set; }

    public string EffectiveAdminHost => string.IsNullOrWhiteSpace(AdminHost) ? "admin." + Domain.Trim().Trim('.') : AdminHost.Trim();
    public string EffectivePortalHost => string.IsNullOrWhiteSpace(PortalHost) ? "my." + Domain.Trim().Trim('.') : PortalHost.Trim();
}

/// <summary>Endpoint qaysi zonaga tegishli (Razor Pages konvensiyasi qo'yadi).</summary>
public sealed record ZoneMetadata(Zone Zone);

public static class HostZones
{
    private const string ItemKey = "tcp.zone";

    public static Zone Resolve(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached) && cached is Zone z)
            return z;
        var o = context.RequestServices.GetRequiredService<IOptions<PanelHostOptions>>().Value;
        string host = context.Request.Host.Host;
        Zone zone = string.Equals(host, o.EffectivePortalHost, StringComparison.OrdinalIgnoreCase) ? Zone.Portal
            : string.Equals(host, o.EffectiveAdminHost, StringComparison.OrdinalIgnoreCase) ? Zone.Admin
            : o.OtherHosts;
        context.Items[ItemKey] = zone;
        return zone;
    }

    /// <summary>Portalning tashqi manzili (taklif havolasi uchun).</summary>
    public static string PortalBaseUrl(HttpContext? context, PanelHostOptions o)
    {
        if (!string.IsNullOrWhiteSpace(o.PortalPublicUrl))
            return o.PortalPublicUrl.TrimEnd('/');
        string scheme = context?.Request.Scheme ?? "https";
        int? port = context?.Request.Host.Port;
        return $"{scheme}://{o.EffectivePortalHost}{(port is int p && p != 443 && p != 80 ? ":" + p : "")}";
    }
}

/// <summary>
/// /Portal papkasidagi sahifalar: URL'dan "Portal/" prefiksi olib tashlanadi (my.&lt;domen&gt;/Invoices) va Portal zonasi
/// belgilanadi; qolgan barcha sahifalar — Admin zonasi. /Error — zonasiz (ikkala tomondagi xato sahifasi).
/// </summary>
public sealed class ZonePageRouteConvention : IPageRouteModelConvention
{
    public const string PortalFolder = "/Portal";

    public void Apply(PageRouteModel model)
    {
        if (model.ViewEnginePath == "/Error")
            return;
        bool portal = model.ViewEnginePath.StartsWith(PortalFolder + "/", StringComparison.OrdinalIgnoreCase);
        foreach (var selector in model.Selectors)
        {
            if (portal && selector.AttributeRouteModel?.Template is string t)
            {
                string stripped = t.Equals("Portal", StringComparison.OrdinalIgnoreCase) ? ""
                    : t.StartsWith("Portal/", StringComparison.OrdinalIgnoreCase) ? t["Portal/".Length..] : t;
                selector.AttributeRouteModel.Template = stripped;
            }
            selector.EndpointMetadata.Add(new ZoneMetadata(portal ? Zone.Portal : Zone.Admin));
        }
    }
}

/// <summary>
/// Routing siyosati: so'rov zonasiga mos kelmaydigan endpoint'lar nomzodlikdan chiqariladi. Natija: xodim sahifasi
/// my.* da va portal sahifasi admin.* da 404; bir xil yo'l (masalan "/Invoices") ikki zonada ziddiyatsiz ishlaydi.
/// </summary>
public sealed class ZoneMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(e => e.Metadata.GetMetadata<ZoneMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        Zone zone = HostZones.Resolve(httpContext);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
                continue;
            var meta = candidates[i].Endpoint.Metadata.GetMetadata<ZoneMetadata>();
            if (meta is not null && meta.Zone != zone)
                candidates.SetValidity(i, false);
        }
        return Task.CompletedTask;
    }
}
