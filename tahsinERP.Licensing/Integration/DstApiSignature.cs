using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.Integration
{
    /// <summary>
    /// Bot servisi → ERP distributsiya API so'rovlari imzosi (servisdan-servisga). Ikkala tomon shu kodni ishlatadi.
    ///
    /// Kalit: tenant kaliti = HMAC-SHA256(master, "dst-api:" + slug) — master faqat muhit o'zgaruvchisida
    /// (ERP: TAHSINERP_BOT_KEY, bot: Bot:MasterKey), bazada saqlanmaydi; har tenant o'z kaliti bilan.
    /// Sarlavhalar: X-Dst-Timestamp (Unix soniya), X-Dst-Signature (base64), X-Acting-User (ERP USERS.ID, ixtiyoriy).
    /// Imzolanadigan matn: timestamp \n METHOD \n /yo'l?so'rov \n actingUser \n base64(SHA256(body)).
    /// Vaqt farqi 300 soniyadan oshsa — rad (takroriy yuborish himoyasi).
    /// </summary>
    public static class DstApiSignature
    {
        public const string TimestampHeader = "X-Dst-Timestamp";
        public const string SignatureHeader = "X-Dst-Signature";
        public const string ActingUserHeader = "X-Acting-User";
        public const int MaxSkewSeconds = 300;
        public const string DefaultTenant = "default";

        public static byte[] TenantKey(string masterKey, string tenantSlug)
        {
            if (string.IsNullOrEmpty(masterKey) || masterKey.Length < 32)
                throw new ArgumentException("Master kalit kamida 32 belgi bo'lishi kerak.", nameof(masterKey));
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(masterKey)))
                return h.ComputeHash(Encoding.UTF8.GetBytes("dst-api:" + (string.IsNullOrWhiteSpace(tenantSlug) ? DefaultTenant : tenantSlug.Trim().ToLowerInvariant())));
        }

        public static string Sign(byte[] tenantKey, long timestamp, string method, string pathAndQuery, string actingUser, byte[] body)
        {
            string bodyHash;
            using (var sha = SHA256.Create())
                bodyHash = Convert.ToBase64String(sha.ComputeHash(body ?? new byte[0]));
            var text = timestamp.ToString(CultureInfo.InvariantCulture) + "\n" + (method ?? "").ToUpperInvariant() + "\n" +
                       (pathAndQuery ?? "") + "\n" + (actingUser ?? "") + "\n" + bodyHash;
            using (var h = new HMACSHA256(tenantKey))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static bool Verify(byte[] tenantKey, string timestampHeader, string signatureHeader, string method, string pathAndQuery,
            string actingUser, byte[] body, DateTimeOffset now)
        {
            if (string.IsNullOrEmpty(signatureHeader) ||
                !long.TryParse(timestampHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
                return false;
            if (Math.Abs(now.ToUnixTimeSeconds() - ts) > MaxSkewSeconds)
                return false;
            var expected = Sign(tenantKey, ts, method, pathAndQuery, actingUser, body);
            return FixedTimeEquals(expected, signatureHeader);
        }

        /// <summary>
        /// Bot fayl omboridagi rasm (selfi, tashrif fotosi, imzo) uchun vaqtinchalik havola: ERP sahifasi brauzerga
        /// {bot}/files/{kalit}?e={muddat}&amp;s={imzo} beradi, bot o'sha tenant kaliti bilan tekshiradi. ERP botga murojaat qilmaydi.
        /// </summary>
        public static string SignFile(byte[] tenantKey, string fileKey, long expires)
        {
            using (var h = new HMACSHA256(tenantKey))
            {
                var mac = h.ComputeHash(Encoding.UTF8.GetBytes("file\n" + (fileKey ?? "") + "\n" + expires.ToString(CultureInfo.InvariantCulture)));
                return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        public static bool VerifyFile(byte[] tenantKey, string fileKey, string expires, string signature, DateTimeOffset now)
        {
            if (string.IsNullOrEmpty(signature) || !long.TryParse(expires, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp))
                return false;
            if (exp < now.ToUnixTimeSeconds()) return false;
            return FixedTimeEquals(SignFile(tenantKey, fileKey, exp), signature);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
