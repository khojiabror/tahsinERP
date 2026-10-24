using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

/// <summary>DNS so'rovi — testda soxtasi bilan almashtiriladi.</summary>
public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Dns.GetHostAddressesAsync(host, ct);
}

/// <summary>
/// Sayt-katalogning firma o'z domeni (Docs/ControlPanel/09_SITE_CUSTOM_DOMAIN.md). Tartib:
/// 1) xodim domenni kiritadi — format, band emasligi tekshiriladi, sayt ilovasiga darhol yetkaziladi (host bo'yicha ochiladi);
/// 2) DNS tekshiruvi: domenning barcha A/AAAA manzillari sayt serveriniki bo'lsa — agentga SiteDomain job (HTTP-01 sertifikat + IIS binding);
/// 3) job tugagach — Active, kerak bo'lsa standart subdomendan 301 yoqiladi. Olib tashlash — saytdan darhol, server tozalash — agent.
/// </summary>
public sealed class SiteDomainService(ControlDbContext db, ProvisioningService provisioning, IAppsClient apps, AuditLogger audit, Clock clock,
    IDnsResolver dns, IOptions<AppsOptions> appsOptions, IOptions<ProvisioningOptions> provisioningOptions, ILogger<SiteDomainService> log)
{
    public Task<TenantSiteDomain?> GetAsync(int tenantId, CancellationToken ct = default) =>
        db.SiteDomains.FirstOrDefaultAsync(d => d.TenantId == tenantId, ct);

    /// <summary>Standart sayt hosti: SiteUrl hostidagi birinchi bo'lak o'rniga slug (api.sayt.x.uz → firma.sayt.x.uz).</summary>
    public string? DefaultSiteHost(Tenant t)
    {
        if (!Uri.TryCreate(appsOptions.Value.SiteUrl, UriKind.Absolute, out var u) || !u.Host.Contains('.')) return null;
        return $"{t.Slug}.{u.Host[(u.Host.IndexOf('.') + 1)..]}";
    }

    /// <summary>Domen qarashi kerak bo'lgan manzillar: sozlamadagi SiteServerIp, bo'lmasa standart sayt hostining manzillari.</summary>
    public async Task<List<IPAddress>> ExpectedAddressesAsync(Tenant t, CancellationToken ct = default)
    {
        var configured = (appsOptions.Value.SiteServerIp ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => IPAddress.TryParse(s, out var ip) ? ip : null).OfType<IPAddress>().ToList();
        if (configured.Count > 0) return configured;
        var host = DefaultSiteHost(t);
        if (host is null) return [];
        try { return [.. await dns.ResolveAsync(host, ct)]; }
        catch (SocketException) { return []; }
    }

    /// <summary>Xodim kiritgan domen → normallashtirilgan host. Band domenlar (bizning domen va uning subdomenlari) rad etiladi.</summary>
    public string NormalizeOrThrow(string? input)
    {
        var host = SiteDomainRules.Normalize(input) ?? throw new DomainException(L.T("Pnl_SDom_BadFormat"));
        if (SiteDomainRules.IsUnder(host, provisioningOptions.Value.BaseDomain)
            || (Uri.TryCreate(appsOptions.Value.SiteUrl, UriKind.Absolute, out var u) && SiteDomainRules.IsUnder(host, u.Host[(u.Host.IndexOf('.') + 1)..])))
            throw new DomainException(L.T("Pnl_SDom_Reserved"));
        return host;
    }

    private async Task EnsureNoActiveJobAsync(Tenant t, CancellationToken ct)
    {
        var active = await db.ProvisioningJobs.FirstOrDefaultAsync(j => j.TenantId == t.Id
            && (j.Status == ProvisioningJobStatus.Pending || j.Status == ProvisioningJobStatus.Running), ct);
        if (active != null) throw new DomainException(L.T("Pnl_Prov_OtherActive", Pages.Labels.Of(active.Type), active.Id));
    }

    private static void EnsureSaaS(Tenant t)
    {
        if (t.Deployment != DeploymentKind.SaaS) throw new DomainException(L.T("Pnl_Apps_OnlySaas"));
        if (t.Status == TenantStatus.Archived || t.DeletedAtUtc != null) throw new DomainException(L.T("Pnl_Apps_Archived"));
    }

    private static bool MayBeBound(SiteDomainStatus s) => s is SiteDomainStatus.Binding or SiteDomainStatus.Active or SiteDomainStatus.Failed;

    private static List<string> Split(string? hosts) => (hosts ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static void AddRemoval(TenantSiteDomain d, string host)
    {
        var list = Split(d.PendingRemoval);
        if (!list.Contains(host)) list.Add(host);
        d.PendingRemoval = string.Join(',', list);
    }

    /// <summary>
    /// Domenni ulaydi yoki almashtiradi (redirect — faol bo'lgach yo'naltirish). Sayt yoqilgan bo'lishi shart. Shu zahoti DNS tekshiriladi:
    /// to'g'ri bo'lsa agentga job qo'yiladi. Xuddi shu faol domen — faqat yo'naltirish sozlamasi o'zgaradi.
    /// </summary>
    public async Task<TenantSiteDomain> SetAsync(Tenant t, string? input, bool redirect, CancellationToken ct = default)
    {
        EnsureSaaS(t);
        string host = NormalizeOrThrow(input);
        var d = await GetAsync(t.Id, ct);
        if (await db.SiteDomains.AnyAsync(x => x.Host == host && x.TenantId != t.Id, ct)) throw new DomainException(L.T("Pnl_SDom_Taken", host));
        var site = await apps.GetSiteAsync(t.Slug, ct);
        if (!site.Exists || !site.Active) throw new DomainException(L.T("Pnl_SDom_SiteFirst"));

        if (d != null && d.Host == host)
        {
            var beforeRedirect = d.RedirectToDomain;
            d.RedirectToDomain = redirect;
            d.UpdatedAtUtc = clock.UtcNow;
            audit.Add("tenant.site-domain-set", "Tenant", t.Id, new { d.Host, RedirectToDomain = beforeRedirect }, new { d.Host, d.RedirectToDomain, d.Status });
            await db.SaveChangesAsync(ct);
            await PushAsync(t, d, ct);
            if (d.Status is SiteDomainStatus.PendingDns or SiteDomainStatus.Failed) await CheckAsync(t, ct);
            return d;
        }

        await EnsureNoActiveJobAsync(t, ct);
        var before = d is null ? null : new { d.Host, d.Status, d.RedirectToDomain };
        if (d is null) db.SiteDomains.Add(d = new TenantSiteDomain { TenantId = t.Id, CreatedAtUtc = clock.UtcNow });
        else
        {
            if (MayBeBound(d.Status) || d.Status == SiteDomainStatus.Removing) AddRemoval(d, d.Host);
            d.PendingRemoval = string.Join(',', Split(d.PendingRemoval).Where(h => h != host)) is { Length: > 0 } rest ? rest : null;
        }
        d.Host = host;
        d.Status = SiteDomainStatus.PendingDns;
        d.RedirectToDomain = redirect;
        d.DnsOk = false;
        d.DnsCheckedAtUtc = null;
        d.DnsResult = null;
        d.Error = null;
        d.ActivatedAtUtc = null;
        d.UpdatedAtUtc = clock.UtcNow;
        audit.Add("tenant.site-domain-set", "Tenant", t.Id, before, new { d.Host, d.RedirectToDomain, d.PendingRemoval });
        await db.SaveChangesAsync(ct);
        await PushAsync(t, d, ct);
        await CheckAsync(t, ct);
        return d;
    }

    public sealed record CheckResult(bool Ok, string Message, ProvisioningJob? Job);

    /// <summary>
    /// DNS tekshiruvi. Domenning barcha manzillari kutilgan manzillar ichida bo'lsa (CNAME ham shu manzillarga ochiladi) va domen
    /// hali ulanmagan bo'lsa — agentga SiteDomain job. Let's Encrypt AAAA bo'lsa IPv6'ni afzal ko'radi — begona AAAA ham xato.
    /// </summary>
    public async Task<CheckResult> CheckAsync(Tenant t, CancellationToken ct = default)
    {
        EnsureSaaS(t);
        var d = await GetAsync(t.Id, ct) ?? throw new DomainException(L.T("Pnl_SDom_None"));
        if (d.Status == SiteDomainStatus.Removing) throw new DomainException(L.T("Pnl_SDom_Removing"));
        var expected = await ExpectedAddressesAsync(t, ct);
        if (expected.Count == 0) throw new DomainException(L.T("Pnl_SDom_NoExpectedIp"));
        IPAddress[] found;
        string message;
        try
        {
            found = await dns.ResolveAsync(d.Host, ct);
            d.DnsResult = found.Length == 0 ? "—" : string.Join(", ", found.Select(a => a.ToString()));
        }
        catch (SocketException ex)
        {
            found = [];
            d.DnsResult = ex.SocketErrorCode.ToString();
        }
        d.DnsOk = found.Length > 0 && found.All(a => expected.Contains(a));
        d.DnsCheckedAtUtc = clock.UtcNow;
        d.UpdatedAtUtc = clock.UtcNow;
        message = d.DnsOk ? L.T("Pnl_SDom_DnsOk", d.Host)
            : found.Length == 0 ? L.T("Pnl_SDom_DnsNotFound", d.Host)
            : L.T("Pnl_SDom_DnsWrong", d.Host, d.DnsResult, string.Join(", ", expected));

        ProvisioningJob? job = null;
        if (d.DnsOk && d.Status is SiteDomainStatus.PendingDns or SiteDomainStatus.Failed)
        {
            await EnsureNoActiveJobAsync(t, ct);
            job = await EnqueueAsync(t, d, d.Host, ct);
            d.Status = SiteDomainStatus.Binding;
            d.Error = null;
            message = L.T("Pnl_SDom_JobQueued", d.Host, job.Id);
        }
        audit.Add("tenant.site-domain-check", "Tenant", t.Id, null, new { d.Host, d.DnsOk, d.DnsResult, d.Status, JobId = job?.Id });
        await db.SaveChangesAsync(ct);
        return new CheckResult(d.DnsOk, message, job);
    }

    /// <summary>Domenni olib tashlaydi: sayt ilovasidan darhol; serverda ulangan bo'lishi mumkin bo'lsa — agent tozalaydi, keyin yozuv o'chadi.</summary>
    public async Task<ProvisioningJob?> RemoveAsync(Tenant t, CancellationToken ct = default)
    {
        var d = await GetAsync(t.Id, ct) ?? throw new DomainException(L.T("Pnl_SDom_None"));
        await EnsureNoActiveJobAsync(t, ct);
        await apps.SetSiteDomainAsync(t.Slug, null, false, ct);
        if (MayBeBound(d.Status) || d.Status == SiteDomainStatus.Removing) AddRemoval(d, d.Host);
        audit.Add("tenant.site-domain-remove", "Tenant", t.Id, new { d.Host, d.Status }, new { d.PendingRemoval });
        if (d.PendingRemoval is null)
        {
            db.SiteDomains.Remove(d);
            await db.SaveChangesAsync(ct);
            return null;
        }
        if (t.ServerId is null || t.Status == TenantStatus.Archived || t.DeletedAtUtc != null)
        {
            // agent yo'q (arxiv/o'chirilgan) — serverdagi binding'ni qo'lda tozalash kerak (hujjatda), yozuv o'chiriladi
            log.LogWarning("Domen {Host} serverda tozalanmadi: tenant {Slug} agentsiz", d.PendingRemoval, t.Slug);
            db.SiteDomains.Remove(d);
            await db.SaveChangesAsync(ct);
            return null;
        }
        var job = await EnqueueAsync(t, d, null, ct);
        d.Status = SiteDomainStatus.Removing;
        d.Error = null;
        d.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return job;
    }

    private async Task<ProvisioningJob> EnqueueAsync(Tenant t, TenantSiteDomain d, string? bind, CancellationToken ct)
    {
        var remove = Split(d.PendingRemoval).Where(h => h != bind).ToList();
        var r = await provisioning.EnqueueAsync(t, ProvisioningJobType.SiteDomain, bind != null ? "O'z domeni: " + bind : "O'z domeni olib tashlanadi", ct: ct,
            payloadExtra: p => { p.SiteDomain = bind; p.SiteDomainRemove = remove.Count > 0 ? remove : null; });
        d.LastJobId = r.Job.Id;
        return r.Job;
    }

    /// <summary>Sayt ilovasiga joriy holat: host doim (host bo'yicha ochilsin), yo'naltirish — faqat sertifikat tayyor bo'lsa.</summary>
    private Task PushAsync(Tenant t, TenantSiteDomain d, CancellationToken ct) =>
        apps.SetSiteDomainAsync(t.Slug, d.Status == SiteDomainStatus.Removing ? null : d.Host, d.RedirectToDomain && d.Status == SiteDomainStatus.Active, ct);

    /// <summary>
    /// SiteDomain job natijasi (ProvisioningService.CompleteAsync/CancelAsync chaqiradi, o'sha SaveChanges'da). Qaytaradi: sayt ilovasiga
    /// keyin yuboriladigan holat (slug, host, redirect) yoki null.
    /// </summary>
    public static async Task<(string Slug, string? Host, bool Redirect)?> ApplyJobResultAsync(ControlDbContext db, ProvisioningJob job, Tenant t,
        bool success, string? error, DateTime now, CancellationToken ct)
    {
        var d = await db.SiteDomains.FirstOrDefaultAsync(x => x.TenantId == t.Id, ct);
        if (d is null || d.LastJobId != job.Id) return null;
        d.UpdatedAtUtc = now;
        if (!success)
        {
            d.Error = error;
            if (d.Status == SiteDomainStatus.Binding) d.Status = SiteDomainStatus.Failed;
            return null;
        }
        d.PendingRemoval = null;
        d.Error = null;
        if (d.Status == SiteDomainStatus.Removing)
        {
            db.SiteDomains.Remove(d);
            return null;
        }
        d.Status = SiteDomainStatus.Active;
        d.ActivatedAtUtc = now;
        return (t.Slug, d.Host, d.RedirectToDomain);
    }
}
