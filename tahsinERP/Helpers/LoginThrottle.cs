using System;
using System.Runtime.Caching;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Login'ga urinishlar cheklovi (brute-force himoyasi). DB'ga tegmaydi —
    /// hisoblagichlar xotirada (MemoryCache), kalit = username + IP.
    ///
    /// <see cref="MaxFailures"/> marta ketma-ket noto'g'ri urinishdan keyin shu juftlik
    /// <see cref="LockoutDuration"/> davomida bloklanadi. Muvaffaqiyatli login hisobni tozalaydi.
    /// App pool qayta ishga tushsa hisoblagichlar nolga tushadi (ataylab — oddiy yechim).
    /// </summary>
    public class LoginThrottle
    {
        public static readonly LoginThrottle Default = new LoginThrottle(MemoryCache.Default, () => DateTime.UtcNow);

        public int MaxFailures { get; set; } = 5;
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

        private readonly ObjectCache _cache;
        private readonly Func<DateTime> _utcNow;
        private readonly object _sync = new object();

        public LoginThrottle(ObjectCache cache, Func<DateTime> utcNow)
        {
            _cache = cache;
            _utcNow = utcNow;
        }

        private class Entry
        {
            public int Failures;
            public DateTime? LockedUntil;
        }

        private static string Key(string userName, string ip)
        {
            return "login-throttle:" + (userName ?? "").Trim().ToLowerInvariant() + "|" + (ip ?? "");
        }

        /// <summary>Bloklangan bo'lsa true va qolgan vaqtni qaytaradi.</summary>
        public bool IsLockedOut(string userName, string ip, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            var entry = _cache.Get(Key(userName, ip)) as Entry;
            if (entry == null) return false;
            lock (_sync)
            {
                if (entry.LockedUntil == null) return false;
                var left = entry.LockedUntil.Value - _utcNow();
                if (left <= TimeSpan.Zero)
                {
                    _cache.Remove(Key(userName, ip));
                    return false;
                }
                remaining = left;
                return true;
            }
        }

        /// <summary>Noto'g'ri urinishni qayd etadi. Shu urinish bloklashga olib kelsa true.</summary>
        public bool RegisterFailure(string userName, string ip)
        {
            string key = Key(userName, ip);
            lock (_sync)
            {
                var entry = _cache.Get(key) as Entry ?? new Entry();
                entry.Failures++;
                bool locked = entry.Failures >= MaxFailures;
                if (locked)
                {
                    entry.LockedUntil = _utcNow() + LockoutDuration;
                    entry.Failures = 0;
                }
                // Hisoblagich oxirgi urinishdan LockoutDuration o'tgach o'z-o'zidan o'chadi
                _cache.Set(key, entry, new CacheItemPolicy { SlidingExpiration = LockoutDuration });
                return locked;
            }
        }

        public void Reset(string userName, string ip)
        {
            _cache.Remove(Key(userName, ip));
        }
    }
}
