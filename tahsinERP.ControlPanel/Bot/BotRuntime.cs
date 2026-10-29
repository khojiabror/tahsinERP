using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>
/// Bot sozlamalari keshi: support guruhi va webhook secret (BotSettings jadvali, env ustun). Webhook har so'rovda
/// bazaga bormasligi uchun xotirada; panel sahifasi o'zgartirganda <see cref="Invalidate"/>.
/// </summary>
public sealed class BotConfig(IServiceScopeFactory scopes, IOptions<TelegramOptions> tg, ISecretProtector protector, TimeProvider time)
{
    public const string SecretPurpose = "telegram-webhook";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public sealed record Snapshot(long? SupportChatId, string? WebhookSecret, long? SalesChatId);

    private Snapshot? _cached;
    private DateTimeOffset _loadedAt;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<Snapshot> GetAsync(CancellationToken ct = default)
    {
        var c = _cached;
        if (c is not null && time.GetUtcNow() - _loadedAt < Ttl)
            return c;
        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null && time.GetUtcNow() - _loadedAt < Ttl)
                return _cached;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
            var s = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
            string? secret = !string.IsNullOrWhiteSpace(tg.Value.WebhookSecret) ? tg.Value.WebhookSecret
                : s?.WebhookSecret is { Length: > 0 } p ? protector.Unprotect(p, SecretPurpose) : null;
            long? support = s?.SupportChatId ?? (long.TryParse(tg.Value.SupportChatId, out long sc) ? sc : null);
            long? sales = long.TryParse(tg.Value.StaffChatId, out long st) ? st : null;
            _cached = new Snapshot(support, secret, sales);
            _loadedAt = time.GetUtcNow();
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _cached = null;

    /// <summary>Yangi secret_token (Telegram: 1–256 belgi, A-Z a-z 0-9 _ -).</summary>
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

/// <summary>
/// Webhook yangilanishlari navbati: webhook darhol 200 qaytaradi (Telegram kutmaydi va qayta yubormaydi), ishlov fonda.
/// Bir chatning yangilanishlari doim bitta oqimda — tartib saqlanadi (oqim qadamlari). Takroriy update_id tashlanadi.
/// </summary>
public sealed class BotQueue
{
    public const int Partitions = 4;
    private readonly Channel<JsonElement>[] _channels = Enumerable.Range(0, Partitions)
        .Select(_ => Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }))
        .ToArray();
    private readonly HashSet<long> _seen = [];
    private readonly Queue<long> _seenOrder = new();

    public ChannelReader<JsonElement> Reader(int partition) => _channels[partition].Reader;

    /// <returns>false — bu update_id avval kelgan (Telegram qayta yuborgan).</returns>
    public bool MarkSeen(JsonElement update)
    {
        if (!update.TryGetProperty("update_id", out var u) || !u.TryGetInt64(out long id))
            return true;
        lock (_seen)
        {
            if (!_seen.Add(id)) return false;
            _seenOrder.Enqueue(id);
            if (_seenOrder.Count > 5000) _seen.Remove(_seenOrder.Dequeue());
        }
        return true;
    }

    public void Enqueue(JsonElement update)
    {
        long key = SalesBot.ChatKey(update);
        _channels[(int)((ulong)key % Partitions)].Writer.TryWrite(update);
    }
}

/// <summary>Navbatdagi yangilanishlarni <see cref="SalesBot"/> bilan qayta ishlaydi (har bo'lim — alohida oqim).</summary>
public sealed class BotWorker(BotQueue queue, IServiceScopeFactory scopes, ILogger<BotWorker> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, BotQueue.Partitions).Select(p => RunAsync(p, stoppingToken)));

    private async Task RunAsync(int partition, CancellationToken ct)
    {
        try
        {
            await foreach (var update in queue.Reader(partition).ReadAllAsync(ct))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<SalesBot>().HandleAsync(update, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    log.LogError(ex, "Bot yangilanishini qayta ishlashda xato");
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
