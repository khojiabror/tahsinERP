using System.Globalization;
using System.Text.RegularExpressions;

namespace tahsinERP.Provisioning.Contracts;

/// <summary>
/// Sayt-katalog o'z domeni formati — panel (kiritish) va agent (IIS/win-acme buyruqlariga berishdan oldin) bir xil qoida.
/// tahsinERP.Site'da shu qoidaning nusxasi: SiteTenantAdmin.NormalizeHost.
/// </summary>
public static partial class SiteDomainRules
{
    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z][a-z0-9-]{0,61}[a-z0-9]$")]
    private static partial Regex HostRegex();

    /// <summary>Kichik harf, bo'sh joy va oxirgi nuqtasiz, IDN → punycode. Yaroqsiz (sxema, yo'l, port, IP, bitta bo'lak) — null.</summary>
    public static string? Normalize(string? host)
    {
        var h = (host ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        if (h.Length == 0) return null;
        try { h = new IdnMapping().GetAscii(h); }
        catch (ArgumentException) { return null; }
        return HostRegex().IsMatch(h) ? h : null;
    }

    /// <summary>Allaqachon normallashtirilgan host (agent tomonida qayta tekshiruv).</summary>
    public static bool IsValid(string? host) => host != null && Normalize(host) == host;

    /// <summary>Host shu domen yoki uning subdomenimi (masalan tahsinerp.uz, *.tahsinerp.uz — o'z domeni sifatida band).</summary>
    public static bool IsUnder(string host, string? domain)
    {
        var d = (domain ?? "").Trim().TrimStart('.').TrimEnd('.').ToLowerInvariant();
        return d.Length > 0 && (host == d || host.EndsWith("." + d, StringComparison.Ordinal));
    }
}
