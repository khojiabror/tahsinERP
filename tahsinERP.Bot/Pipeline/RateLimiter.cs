namespace tahsinERP.Bot.Pipeline;

/// <summary>
/// Telegram cheklovlari (core.telegram.org/bots/faq#my-bot-is-hitting-limits-how-do-i-avoid-this):
/// bot bo'yicha ~30 xabar/s (biz — GlobalPerSecond), bitta chatga ~1 xabar/s (qisqa portlash — 3 tagacha),
/// 429 javobidagi retry_after — chat to'xtatiladi. Token-bucket; vaqt tashqaridan beriladi (testlanadi).
/// </summary>
public sealed class RateLimiter(int globalPerSecond, int chatBurst = 3, double chatPerSecond = 1)
{
    private sealed class Bucket
    {
        private readonly double _capacity, _rate;
        private double _tokens;
        private DateTime _at;
        public DateTime PausedUntil = DateTime.MinValue;

        public Bucket(double capacity, double rate, DateTime now)
        {
            _capacity = _tokens = capacity;
            _rate = rate;
            _at = now;
        }

        public void Refill(DateTime now)
        {
            if (now > _at)
            {
                _tokens = Math.Min(_capacity, _tokens + (now - _at).TotalSeconds * _rate);
                _at = now;
            }
        }

        public bool Has => _tokens >= 1;
        public bool Full => _tokens >= _capacity;
        public void Take() => _tokens -= 1;
        public TimeSpan WaitForOne => _tokens >= 1 ? TimeSpan.Zero : TimeSpan.FromSeconds((1 - _tokens) / _rate);
    }

    private readonly object _sync = new();
    private readonly Dictionary<int, Bucket> _global = new();
    private readonly Dictionary<(int, long), Bucket> _chats = new();

    /// <summary>Yuborish mumkinmi; mumkin bo'lsa — kvota olinadi. countsForChat=false — answerCallbackQuery kabi (chat xabari emas).</summary>
    public bool TryAcquire(int tenantId, long chatId, bool countsForChat, DateTime now, out TimeSpan wait)
    {
        lock (_sync)
        {
            var g = Get(_global, tenantId, globalPerSecond, globalPerSecond, now);
            Bucket? c = countsForChat ? Get(_chats, (tenantId, chatId), chatBurst, chatPerSecond, now) : null;
            if (c != null && c.PausedUntil > now)
            {
                wait = c.PausedUntil - now;
                return false;
            }
            if (!g.Has || c is { Has: false })
            {
                wait = g.WaitForOne > (c?.WaitForOne ?? TimeSpan.Zero) ? g.WaitForOne : c!.WaitForOne;
                return false;
            }
            g.Take();
            c?.Take();
            wait = TimeSpan.Zero;
            return true;
        }
    }

    public void Pause(int tenantId, long chatId, TimeSpan duration, DateTime now)
    {
        lock (_sync)
        {
            var c = Get(_chats, (tenantId, chatId), chatBurst, chatPerSecond, now);
            var until = now + duration;
            if (until > c.PausedUntil) c.PausedUntil = until;
        }
    }

    /// <summary>Uzoq vaqt ishlatilmagan chat chelaklarini tozalash (xotira).</summary>
    public void Trim(DateTime now)
    {
        lock (_sync)
        {
            foreach (var key in _chats.Where(kv => { kv.Value.Refill(now); return kv.Value.Full && kv.Value.PausedUntil < now; })
                         .Select(kv => kv.Key).ToList())
                _chats.Remove(key);
        }
    }

    private static Bucket Get<TKey>(Dictionary<TKey, Bucket> map, TKey key, double capacity, double rate, DateTime now) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var b)) map[key] = b = new Bucket(capacity, rate, now);
        b.Refill(now);
        return b;
    }
}
