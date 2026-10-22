using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Avto-yangilash sozlamalari (ControlPanel:Updates). GitHub token — fine-grained PAT, faqat shu repo, Contents: read-only;
/// pool environmentVariables'da (ControlPanel__Updates__GitHubToken) — faylda/bazada saqlanmaydi.
/// </summary>
public class UpdatesOptions
{
    /// <summary>owner/repo.</summary>
    public string Repo { get; set; } = "khojiabror/tahsinERP";
    public string? GitHubToken { get; set; }
    public string ApiBase { get; set; } = "https://api.github.com";
    /// <summary>Relizlar shuncha daqiqada bir tekshiriladi (fon ishi). 0 — faqat qo'lda.</summary>
    public int RefreshMinutes { get; set; } = 10;
    public bool IncludePrerelease { get; set; }
    /// <summary>Running job shuncha daqiqa agentdan xabarsiz qolsa — «aloqa uzildi».</summary>
    public int LeaseMinutes { get; set; } = 15;
}

public sealed record ReleaseAsset(long Id, string Name, long Size);

public sealed record ReleaseListing(string Tag, string? Name, string? Body, bool Prerelease, bool Draft, DateTime? PublishedAtUtc, IReadOnlyList<ReleaseAsset> Assets);

/// <summary>Reliz manbai (GitHub Releases). Testlarda soxtasi.</summary>
public interface IReleaseSource
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<ReleaseListing>> ListAsync(CancellationToken ct);
    /// <summary>Asset oqimi (chaqiruvchi yopadi).</summary>
    Task<Stream> OpenAssetAsync(long assetId, CancellationToken ct);
}

public sealed class GitHubReleaseSource(HttpClient http, IOptions<UpdatesOptions> options) : IReleaseSource
{
    private UpdatesOptions O => options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.GitHubToken) && O.Repo.Contains('/');

    private HttpRequestMessage Request(string url, string accept)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, url);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.GitHubToken);
        r.Headers.Accept.ParseAdd(accept);
        r.Headers.UserAgent.ParseAdd("tahsinERP-ControlPanel");
        r.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return r;
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; } = "";
        public string? Name { get; set; }
        public string? Body { get; set; }
        public bool Prerelease { get; set; }
        public bool Draft { get; set; }
        [JsonPropertyName("published_at")] public DateTime? PublishedAt { get; set; }
        public List<GhAsset> Assets { get; set; } = [];
    }

    private sealed class GhAsset
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public long Size { get; set; }
    }

    public async Task<IReadOnlyList<ReleaseListing>> ListAsync(CancellationToken ct)
    {
        if (!IsConfigured) throw new DomainException("GitHub token sozlanmagan (ControlPanel:Updates:GitHubToken).");
        using var req = Request($"{O.ApiBase.TrimEnd('/')}/repos/{O.Repo}/releases?per_page=30", "application/vnd.github+json");
        using var r = await http.SendAsync(req, ct);
        if (!r.IsSuccessStatusCode) throw new DomainException($"GitHub {(int)r.StatusCode}: relizlar o'qilmadi (token/repo huquqini tekshiring).");
        var list = await r.Content.ReadFromJsonAsync<List<GhRelease>>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? [];
        return list.Select(x => new ReleaseListing(x.Tag, x.Name, x.Body, x.Prerelease, x.Draft, x.PublishedAt?.ToUniversalTime(),
            x.Assets.Select(a => new ReleaseAsset(a.Id, a.Name, a.Size)).ToList())).ToList();
    }

    public async Task<Stream> OpenAssetAsync(long assetId, CancellationToken ct)
    {
        // GitHub 302 bilan S3'ga yo'naltiradi; HttpClient boshqa host'ga o'tishda Authorization'ni yubormaydi
        var req = Request($"{O.ApiBase.TrimEnd('/')}/repos/{O.Repo}/releases/assets/{assetId}", "application/octet-stream");
        var r = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!r.IsSuccessStatusCode)
        {
            r.Dispose();
            throw new DomainException($"GitHub asset #{assetId}: {(int)r.StatusCode}");
        }
        return await r.Content.ReadAsStreamAsync(ct);
    }
}
