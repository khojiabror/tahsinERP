using System;
using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Parol hashlash: PBKDF2 (HMAC-SHA256, 100 000 iteratsiya, 16 bayt tasodifiy salt).
    ///
    /// Saqlash formati (USERS jadvali, ustunlar o'zgarmaydi):
    ///   Password = "$p2$" + Base64(32 bayt hash, '=' siz)  → 47 belgi (varchar(50) ga sig'adi)
    ///   HashCode = Base64(16 bayt salt)                     → 24 belgi (varchar(30) ga sig'adi)
    ///
    /// Eski format (prefikssiz): MD5(Base64(SHA1(salt + parol))) — faqat tekshirish uchun.
    /// Eski hash mos kelsa, Verify needsRehash = true qaytaradi va login uni yangi
    /// formatda qayta saqlaydi.
    /// </summary>
    public static class PasswordHasher
    {
        public const string Prefix = "$p2$";
        // Oshirish kerak bo'lsa — yangi prefiks ("$p3$") bilan, eski hash'lar buzilmasin
        public const int Iterations = 100000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        /// <summary>Yangi parol uchun hash va salt yaratadi.</summary>
        public static string Hash(string password, out string salt)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            var saltBytes = new byte[SaltBytes];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(saltBytes);
            salt = Convert.ToBase64String(saltBytes);
            return Prefix + Convert.ToBase64String(Derive(password, saltBytes)).TrimEnd('=');
        }

        /// <summary>
        /// Parolni saqlangan hash bilan solishtiradi. Eski (SHA1+MD5) formatni ham taniydi;
        /// u holda <paramref name="needsRehash"/> = true.
        /// </summary>
        public static bool Verify(string password, string storedHash, string salt, out bool needsRehash)
        {
            needsRehash = false;
            if (password == null || string.IsNullOrEmpty(storedHash))
                return false;

            if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
            {
                byte[] saltBytes;
                byte[] expected;
                try
                {
                    saltBytes = Convert.FromBase64String(salt ?? "");
                    expected = Convert.FromBase64String(Pad(storedHash.Substring(Prefix.Length)));
                }
                catch (FormatException)
                {
                    return false;
                }
                return FixedTimeEquals(Derive(password, saltBytes), expected);
            }

            string legacy = LegacyHash(password, salt ?? "");
            bool ok = FixedTimeEquals(Encoding.ASCII.GetBytes(legacy), Encoding.ASCII.GetBytes(storedHash));
            needsRehash = ok;
            return ok;
        }

        /// <summary>Eski algoritm (tahsinERP 2026-10 gacha). Faqat migratsiya uchun.</summary>
        public static string LegacyHash(string pass, string salt)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(pass);
            byte[] src = Encoding.Unicode.GetBytes(salt);
            byte[] dst = new byte[src.Length + bytes.Length];
            Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            Buffer.BlockCopy(bytes, 0, dst, src.Length, bytes.Length);
            byte[] sha1;
            using (var algorithm = SHA1.Create())
                sha1 = algorithm.ComputeHash(dst);
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(Convert.ToBase64String(sha1))));
        }

        private static byte[] Derive(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                return pbkdf2.GetBytes(HashBytes);
        }

        private static string Pad(string b64)
        {
            int rem = b64.Length % 4;
            return rem == 0 ? b64 : b64 + new string('=', 4 - rem);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
