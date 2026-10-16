using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Litsenziya kalitini RSA imzo bilan yaratish va tekshirish.
    ///
    /// Bu fayl IKKI joyda ishlatiladi (bitta manba):
    ///   1) ERP (tahsinERP)      — faqat PUBLIC key bilan TEKSHIRADI.
    ///   2) tahsinERP.LicenseGen — PRIVATE key bilan YARATADI (imzolaydi).
    ///
    /// Public key kod ichida (const) — Web.config'dan O'QILMAYDI. Aks holda mijoz
    /// o'z kalit juftini yaratib, config'dagi public key'ni almashtirib, o'zi
    /// kalit yasay olardi. Private key faqat WinForms generatorda (vendorda) turadi.
    ///
    /// Kalit formati:
    ///   v1 payload = "CompanyID|MaxUsers|StartDate|ExpiryDate|Edition"            (eski, o'tish davri)
    ///   v2 payload = "v2|CompanyID|MaxUsers|StartDate|ExpiryDate|InstallationId|Edition"
    ///   Key        = Base64(payloadUtf8) + "." + Base64(RSASign(payload, SHA256))
    ///
    /// v2 kalit faqat bitta o'rnatmada ishlaydi: InstallationId — ERP birinchi
    /// ishga tushganda yaratadigan GUID (License sahifasida ko'rinadi). Markaziy
    /// SaaS paneli ham shu formatda kalit chiqaradi (InstallationId = tenant ID).
    /// </summary>
    public static class LicenseHelper
    {
        private const char SEPARATOR = '.';
        private const char FIELD_SEP = '|';
        private const string DATE_FMT = "yyyyMMdd";
        private const string V2_TAG = "v2";

        /// <summary>
        /// Vendor public key (RSA 2048). Faqat tekshirish uchun — u bilan kalit yasab bo'lmaydi.
        /// Kalit juftini almashtirsangiz: shu qiymatni yangilang va ERP'ni qayta yig'ing.
        /// </summary>
        public const string PublicKeyXml =
            "<RSAKeyValue><Modulus>1kFRSYc6W2dBKs8GB8YWZdJ21jCz8JNRWwwgpzT9iJKfCI8EW6YasrAhInBF0Isnr/ZOmxVeWa0ZnWEbd0yQlJL5+qEVK2C1ThrZqAcdP7numttaq9R+pJauG8rBSBUm5ugzVoNhBVpeUmlv0EpLO0wXZSeqCZaP/Lcwu6Kb9FFe6GY2bDXFTl6fPPybaG3oMQ489PLf3BroHs1cSmYaG4yyoAbbaOnvD8uYKfLGN9CL3o9UDELsoqCO3vwCI5RP+nrzWkWxeELZOat/me3MxHw6SXmSasQxCBVgcKw/OmjjmeOjNe5wBF+vNNrONEFdk7M3R3tzX60rmjNGx15RLQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>Muddat tugashidan necha kun oldin admin'ga ogohlantirish ko'rsatiladi.</summary>
        public const int WarningDays = 14;

        /// <summary>Muddat tugagandan keyin faqat-o'qish rejimidagi kunlar soni.</summary>
        public const int GraceDays = 3;

        /// <summary>Soat shuncha orqaga ketsa — bloklanadi (NTP/vaqt zonasi tuzatishlari uchun zaxira).</summary>
        public static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromHours(48);

        /// <summary>Shu sanadan keyin YANGI v1 kalitni faollashtirib bo'lmaydi (faol v1 kalitlar muddati tugaguncha ishlaydi).</summary>
        public static readonly DateTime V1ActivationCutoff = new DateTime(2027, 1, 1);

        // "Oxirgi ko'rilgan vaqt" muhri uchun HMAC siri (InstallationId bilan birga kalitga aylanadi).
        // Mijoz DB'dagi qiymatni qo'lda o'zgartirsa, HMAC mos kelmaydi.
        private const string STAMP_SECRET = "tSN-ERP/clk#7f3c9a1e-58b2-4d0e-9c61-2b8e4a7d0f53";

        public enum LicenseState
        {
            Valid,          // to'liq ishlaydi
            ExpiringSoon,   // ishlaydi, admin'ga ogohlantirish
            Grace,          // muddati tugagan, lekin GraceDays ichida — faqat o'qish
            Expired,        // bloklangan
            NotStarted      // hali kuchga kirmagan — bloklangan
        }

        /// <summary>
        /// Litsenziya ichidagi ma'lumotlar (kalitdan o'qiladi).
        /// </summary>
        public class LicenseInfo
        {
            /// <summary>Kalit formati versiyasi: 1 (eski) yoki 2.</summary>
            public int Version { get; set; }
            public int CompanyID { get; set; }
            public int MaxUsers { get; set; }        // 0 = cheksiz
            public DateTime StartDate { get; set; }
            public DateTime ExpiryDate { get; set; }
            public string Edition { get; set; }

            /// <summary>v2: kalit bog'langan o'rnatma. v1 da null.</summary>
            public string InstallationId { get; set; }

            /// <summary>Imzo to'g'ri va kalit shakli buzilmaganmi.</summary>
            public bool SignatureValid { get; set; }

            /// <summary>Bugungi kunga nisbatan muddati amal qiladimi.</summary>
            public bool IsExpired
            {
                get { return IsExpiredAt(DateTime.Now); }
            }

            /// <summary>Boshlanmaganmi (kelajakdagi sana).</summary>
            public bool IsNotStarted
            {
                get { return IsNotStartedAt(DateTime.Now); }
            }

            /// <summary>Imzo to'g'ri, boshlangan va tugamagan bo'lsa — to'liq yaroqli.</summary>
            public bool IsValid
            {
                get { return SignatureValid && !IsExpired && !IsNotStarted; }
            }

            public int DaysRemaining
            {
                get { return DaysRemainingAt(DateTime.Now); }
            }

            public bool IsExpiredAt(DateTime now) { return now.Date > ExpiryDate.Date; }
            public bool IsNotStartedAt(DateTime now) { return now.Date < StartDate.Date; }
            public int DaysRemainingAt(DateTime now) { return (int)Math.Ceiling((ExpiryDate.Date - now.Date).TotalDays); }

            /// <summary>Grace oxirgi kuni (shu kun ham faqat-o'qish rejimida ishlaydi).</summary>
            public DateTime GraceUntil { get { return ExpiryDate.Date.AddDays(GraceDays); } }

            /// <summary>
            /// Kalit shu o'rnatmaga tegishlimi. v1 kalitlar hech qaysi o'rnatmaga
            /// bog'lanmagan — o'tish davrida har qanday o'rnatmada qabul qilinadi.
            /// </summary>
            public bool IsBoundTo(string installationId)
            {
                if (Version < 2)
                    return true;
                string a = NormalizeInstallationId(InstallationId);
                string b = NormalizeInstallationId(installationId);
                return a != null && b != null && string.Equals(a, b, StringComparison.Ordinal);
            }
        }

        /// <summary>Imzo yaroqli kalit holatini berilgan vaqtga nisbatan aniqlaydi.</summary>
        public static LicenseState GetState(LicenseInfo info, DateTime now)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            if (info.IsNotStartedAt(now))
                return LicenseState.NotStarted;
            if (info.IsExpiredAt(now))
                return now.Date <= info.GraceUntil ? LicenseState.Grace : LicenseState.Expired;
            if (info.DaysRemainingAt(now) <= WarningDays)
                return LicenseState.ExpiringSoon;
            return LicenseState.Valid;
        }

        /// <summary>
        /// InstallationId'ni yagona ko'rinishga keltiradi (GUID → katta harfli "D" format).
        /// Yaroqsiz bo'lsa null.
        /// </summary>
        public static string NormalizeInstallationId(string installationId)
        {
            if (string.IsNullOrWhiteSpace(installationId))
                return null;
            Guid g;
            if (!Guid.TryParse(installationId.Trim(), out g) || g == Guid.Empty)
                return null;
            return g.ToString("D").ToUpperInvariant();
        }

        #region Generatsiya (PRIVATE key — faqat generator ishlatadi)

        /// <summary>
        /// Yangi RSA kalit juftini yaratadi (bir martalik operatsiya).
        /// privateXml — faqat sizda saqlanadi. publicXml — LicenseHelper.PublicKeyXml ga.
        /// </summary>
        public static void GenerateKeyPair(out string publicXml, out string privateXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                try
                {
                    publicXml = rsa.ToXmlString(false);
                    privateXml = rsa.ToXmlString(true);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }

        /// <summary>
        /// Imzolangan v2 litsenziya kalitini yaratadi (o'rnatmaga bog'langan). PRIVATE key kerak.
        /// </summary>
        public static string GenerateKey(int companyId, int maxUsers, DateTime startDate,
            DateTime expiryDate, string edition, string installationId, string privateXml)
        {
            string instId = NormalizeInstallationId(installationId);
            if (instId == null)
                throw new ArgumentException("InstallationId yaroqsiz (GUID bo'lishi kerak).", nameof(installationId));

            string payload = string.Join(FIELD_SEP.ToString(), new[]
            {
                V2_TAG,
                companyId.ToString(CultureInfo.InvariantCulture),
                maxUsers.ToString(CultureInfo.InvariantCulture),
                startDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                expiryDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                instId,
                SafeEdition(edition)
            });
            return Sign(payload, privateXml);
        }

        /// <summary>
        /// ESKI v1 formatdagi kalit (o'rnatmaga bog'lanmagan). Faqat testlar va
        /// moslikni tekshirish uchun — yangi kalitlar uchun ishlatmang.
        /// </summary>
        public static string GenerateLegacyV1Key(int companyId, int maxUsers, DateTime startDate,
            DateTime expiryDate, string edition, string privateXml)
        {
            string payload = string.Join(FIELD_SEP.ToString(), new[]
            {
                companyId.ToString(CultureInfo.InvariantCulture),
                maxUsers.ToString(CultureInfo.InvariantCulture),
                startDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                expiryDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                SafeEdition(edition)
            });
            return Sign(payload, privateXml);
        }

        private static string Sign(string payload, string privateXml)
        {
            if (string.IsNullOrWhiteSpace(privateXml))
                throw new ArgumentException("Private key berilmadi.", nameof(privateXml));

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(privateXml);
                byte[] signature = rsa.SignData(payloadBytes, "SHA256");
                return Convert.ToBase64String(payloadBytes) + SEPARATOR + Convert.ToBase64String(signature);
            }
        }

        #endregion

        #region Tekshirish (PUBLIC key — ERP ishlatadi)

#if DEBUG
        // Faqat Debug build: integratsion testlar o'z kalit jufti bilan imzolangan kalitni
        // tekshirishi uchun (reflection orqali o'rnatiladi). Release'da bu maydon umuman yo'q.
        private static string _debugTestPublicKeyXml = null;
#endif

        /// <summary>Kalitni kod ichidagi vendor public key bilan tekshiradi.</summary>
        public static bool TryParseKey(string licenseKey, out LicenseInfo info)
        {
            string publicXml = PublicKeyXml;
#if DEBUG
            if (_debugTestPublicKeyXml != null)
                publicXml = _debugTestPublicKeyXml;
#endif
            return TryParseKey(licenseKey, publicXml, out info);
        }

        /// <summary>
        /// Kalitni ochib, imzosini berilgan PUBLIC key bilan tekshiradi.
        /// Imzo yaroqsiz/soxta yoki kalit buzilgan bo'lsa — false qaytaradi.
        /// </summary>
        public static bool TryParseKey(string licenseKey, string publicXml, out LicenseInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(publicXml))
                return false;

            try
            {
                licenseKey = licenseKey.Trim();
                int sepIndex = licenseKey.LastIndexOf(SEPARATOR);
                if (sepIndex <= 0 || sepIndex >= licenseKey.Length - 1)
                    return false;

                string payloadB64 = licenseKey.Substring(0, sepIndex).Trim();
                string signB64 = licenseKey.Substring(sepIndex + 1).Trim();

                byte[] payloadBytes = Convert.FromBase64String(payloadB64);
                byte[] signature = Convert.FromBase64String(signB64);

                bool valid;
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(publicXml);
                    valid = rsa.VerifyData(payloadBytes, "SHA256", signature);
                }

                string payload = Encoding.UTF8.GetString(payloadBytes);
                var parsed = ParsePayload(payload);
                if (parsed == null)
                    return false;

                parsed.SignatureValid = valid;
                info = parsed;
                return valid;
            }
            catch (FormatException)
            {
                return false; // Base64 buzilgan
            }
            catch (CryptographicException)
            {
                return false; // imzo/kalit xatosi
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Soat muhri ("oxirgi ko'rilgan vaqt", HMAC bilan)

        /// <summary>
        /// UTC vaqtni InstallationId'ga bog'langan HMAC bilan muhrlaydi: "ticks:base64(hmac)".
        /// </summary>
        public static string CreateClockStamp(DateTime utc, string installationId)
        {
            string ticks = utc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
            return ticks + ":" + Convert.ToBase64String(ComputeStampHmac(ticks, installationId));
        }

        /// <summary>Muhrni o'qiydi. HMAC mos kelmasa (qo'lda o'zgartirilgan) — false.</summary>
        public static bool TryReadClockStamp(string stamp, string installationId, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(stamp))
                return false;

            int idx = stamp.IndexOf(':');
            if (idx <= 0 || idx >= stamp.Length - 1)
                return false;

            string ticksStr = stamp.Substring(0, idx);
            long ticks;
            if (!long.TryParse(ticksStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
                || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                return false;

            byte[] given;
            try { given = Convert.FromBase64String(stamp.Substring(idx + 1)); }
            catch (FormatException) { return false; }

            byte[] expected = ComputeStampHmac(ticksStr, installationId);
            if (!FixedTimeEquals(given, expected))
                return false;

            utc = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }

        /// <summary>Hozirgi vaqt oxirgi ko'rilgan vaqtdan ruxsat etilganidan ko'proq orqadami.</summary>
        public static bool IsClockRolledBack(DateTime nowUtc, DateTime lastSeenUtc)
        {
            return lastSeenUtc - nowUtc > ClockRollbackTolerance;
        }

        private static byte[] ComputeStampHmac(string ticks, string installationId)
        {
            string inst = NormalizeInstallationId(installationId) ?? (installationId ?? string.Empty);
            byte[] key;
            using (var sha = SHA256.Create())
                key = sha.ComputeHash(Encoding.UTF8.GetBytes(STAMP_SECRET + FIELD_SEP + inst));
            using (var hmac = new HMACSHA256(key))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes("clock" + FIELD_SEP + ticks));
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        #endregion

        #region Payload yordamchilari

        private static string SafeEdition(string edition)
        {
            // Edition ichida ajratuvchi belgilar bo'lmasligi kerak
            return (edition ?? string.Empty)
                .Replace(FIELD_SEP.ToString(), " ")
                .Replace(SEPARATOR.ToString(), " ")
                .Trim();
        }

        private static LicenseInfo ParsePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return null;

            string[] parts = payload.Split(FIELD_SEP);
            if (parts.Length > 0 && parts[0] == V2_TAG)
                return ParseV2(parts);
            return ParseV1(parts);
        }

        // v1: CompanyID|MaxUsers|Start|Expiry|Edition
        private static LicenseInfo ParseV1(string[] parts)
        {
            if (parts.Length < 4)
                return null;

            var info = ParseCommon(parts[0], parts[1], parts[2], parts[3]);
            if (info == null)
                return null;
            info.Version = 1;
            info.Edition = parts.Length > 4 ? parts[4] : string.Empty;
            return info;
        }

        // v2: v2|CompanyID|MaxUsers|Start|Expiry|InstallationId|Edition
        private static LicenseInfo ParseV2(string[] parts)
        {
            if (parts.Length < 7)
                return null;

            var info = ParseCommon(parts[1], parts[2], parts[3], parts[4]);
            if (info == null)
                return null;

            string instId = NormalizeInstallationId(parts[5]);
            if (instId == null)
                return null;

            info.Version = 2;
            info.InstallationId = instId;
            info.Edition = parts[6];
            return info;
        }

        private static LicenseInfo ParseCommon(string companyStr, string maxStr, string startStr, string expiryStr)
        {
            int companyId, maxUsers;
            DateTime start, expiry;

            if (!int.TryParse(companyStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out companyId))
                return null;
            if (!int.TryParse(maxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxUsers))
                return null;
            if (!DateTime.TryParseExact(startStr, DATE_FMT, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                return null;
            if (!DateTime.TryParseExact(expiryStr, DATE_FMT, CultureInfo.InvariantCulture, DateTimeStyles.None, out expiry))
                return null;

            return new LicenseInfo
            {
                CompanyID = companyId,
                MaxUsers = maxUsers,
                StartDate = start,
                ExpiryDate = expiry
            };
        }

        #endregion
    }
}
