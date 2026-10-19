using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Security;

namespace tahsinERP.Bot.Pipeline;

/// <summary>Faol tenantlar xotirada (token ochilgan holda); har daqiqada yoki o'zgarishda qayta o'qiladi.</summary>
public sealed class TenantRegistry(IServiceScopeFactory scopes, SecretBox secrets, TimeProvider clock, ILogger<TenantRegistry> log)
{
    public sealed record Tenant(int Id, string Slug, string ErpBaseUrl, string Token, string WebhookSecret, string? BotUsername, BotTenant Row);

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<int, Tenant> _byId = new Dictionary<int, Tenant>();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyCollection<Tenant>> AllAsync(CancellationToken ct) => (await MapAsync(ct)).Values.ToList();

    public async Task<Tenant?> ByIdAsync(int id, CancellationToken ct) => (await MapAsync(ct)).GetValueOrDefault(id);

    public async Task<Tenant?> BySlugAsync(string slug, CancellationToken ct) =>
        (await MapAsync(ct)).Values.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public void Invalidate() => _loadedAt = DateTimeOffset.MinValue;

    private async Task<IReadOnlyDictionary<int, Tenant>> MapAsync(CancellationToken ct)
    {
        if (clock.GetUtcNow() - _loadedAt < Ttl) return _byId;
        await _lock.WaitAsync(ct);
        try
        {
            if (clock.GetUtcNow() - _loadedAt < Ttl) return _byId;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var rows = await db.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
            var map = new Dictionary<int, Tenant>();
            foreach (var r in rows)
            {
                try
                {
                    map[r.Id] = new Tenant(r.Id, r.Slug, r.ErpBaseUrl, secrets.Unprotect(r.TokenEnc), r.WebhookSecret, r.BotUsername, r);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Tenant {Slug}: tokenni ochib bo'lmadi (MasterKey o'zgarganmi?)", r.Slug);
                }
            }
            _byId = map;
            _loadedAt = clock.GetUtcNow();
            return map;
        }
        finally
        {
            _lock.Release();
        }
    }
}
