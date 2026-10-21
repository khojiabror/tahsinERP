using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace tahsinERP.Licensing
{
    /// <summary>
    /// Litsenziya kalitini RSA imzo bilan yaratish va tekshirish.
    ///
    /// tahsinERP.Licensing (netstandard2.0) kutubxonasi — bitta manba:
    ///   1) ERP (tahsinERP, .NET 4.8) — faqat PUBLIC key bilan TEKSHIRADI.
    ///   2) tahsinERP.LicenseGen     — PRIVATE key bilan YARATADI (imzolaydi).
    ///   3) Markaziy panel (.NET 10) — AWS KMS bilan imzolaydi (k2): <see cref="BuildV2Payload"/> + <see cref="AssembleKey"/>.
    ///
    /// Imzo: RSASSA-PKCS1-v1_5 + SHA-256 (RSA.Create(), platformadan mustaqil). Eski
    /// RSACryptoServiceProvider.SignData(.., "SHA256") bilan bir xil natija — eski kalitlar ishlayveradi.
    /// AWS KMS'ning RSASSA_PKCS1_V1_5_SHA_256 algoritmi ham aynan shu.
    ///
    /// Public key'lar kod ichida (const) — Web.config'dan O'QILMAYDI. Aks holda mijoz
    /// o'z kalit juftini yaratib, config'dagi public key'ni almashtirib, o'zi
    /// kalit yasay olardi. Private key faqat WinForms generatorda (vendorda) va KMS'da turadi.
    ///
    /// Kalit formati:
    ///   v1 payload = "CompanyID|MaxUsers|StartDate|ExpiryDate|Edition"            (eski, o'tish davri)
    ///   v2 payload = "v2|CompanyID|MaxUsers|StartDate|ExpiryDate|InstallationId|Edition"
    ///   Key        = Base64(payloadUtf8) + "." + Base64(RSASign(payload, SHA256))
    ///
    /// v2 kalit faqat bitta o'rnatmada ishlaydi: InstallationId — ERP birinchi
    /// ishga tushganda yaratadigan GUID (License sahifasida ko'rinadi). Markaziy
    /// SaaS paneli ham shu formatda kalit chiqaradi (InstallationId = tenant o'rnatma ID'si).
    /// </summary>
    public static class LicenseHelper
    {
        private const char SEPARATOR = '.';
        private const char FIELD_SEP = '|';
        private const string DATE_FMT = "yyyyMMdd";
        private const string V2_TAG = "v2";

        /// <summary>
        /// k1 — vendor public key (RSA 2048, WinForms generator / on-prem). Faqat tekshirish uchun.
        /// Kalit juftini almashtirsangiz: shu qiymatni yangilang va ERP'ni qayta yig'ing.
        /// </summary>
        public const string PublicKeyXml =
            "<RSAKeyValue><Modulus>1kFRSYc6W2dBKs8GB8YWZdJ21jCz8JNRWwwgpzT9iJKfCI8EW6YasrAhInBF0Isnr/ZOmxVeWa0ZnWEbd0yQlJL5+qEVK2C1ThrZqAcdP7numttaq9R+pJauG8rBSBUm5ugzVoNhBVpeUmlv0EpLO0wXZSeqCZaP/Lcwu6Kb9FFe6GY2bDXFTl6fPPybaG3oMQ489PLf3BroHs1cSmYaG4yyoAbbaOnvD8uYKfLGN9CL3o9UDELsoqCO3vwCI5RP+nrzWkWxeELZOat/me3MxHw6SXmSasQxCBVgcKw/OmjjmeOjNe5wBF+vNNrONEFdk7M3R3tzX60rmjNGx15RLQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>
        /// k2 — markaziy panel kaliti (RSA 2048, Local signer). Prod serverda 2026-10-21 da
        /// `tahsinERP.ControlPanel.dll keygen --out %ProgramData%\tahsinERP\panel-signing-k2.json` bilan yaratilgan;
        /// private qism faqat shu shifrlangan faylda (parol — egasida). Kalit almashtirilsa: yangi public XML'ni shu yerga
        /// qo'ying va ERP'ni qayta yig'ing (eski kalit bilan berilgan litsenziyalar rad etiladi).
        /// </summary>
        public const string K2PublicKeyXml =
            "<RSAKeyValue><Modulus>+Sb5SglfByIhiHkJELMNIswmgYgRcCfx5ZcZCt4bptcCIvDGWb/aaPs0K/CxQDQ9VVRoeTYZoK1Ejm+DcGXb1HVt1x8CVCyPBDsbczlIfKG7bdvXQklmpyz+PqOGM7bRVHj904olKXl9+zPlPMJ2Nzdo4YeDgLV8e8C8AC5EInPUjkaLRWTvMht38hH/U3N09ztwoXWDOOksWrlnioWAxB9H6HWfyQkuv6Gdi9sjZ+g8mLgE4uQDC89+/SwKveAXwTRInaZ7qRDQ04FMTYt3J1noFSi1Onx9TgWdPgfTLILKRt4BlqMwwmuR0V1b7vwBO+q3M40gHy/xXls2dJYEVQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>Ishonchli public key'lar: tekshiruvda birortasi mos kelsa yetarli. Bo'sh qiymatlar tashlab ketiladi.</summary>
        public static readonly IReadOnlyList<TrustedPublicKey> TrustedPublicKeys = new[]
        {
            new TrustedPublicKey("k1", PublicKeyXml),
            new TrustedPublicKey("k2", K2PublicKeyXml),
        }.Where(k => !string.IsNullOrWhiteSpace(k.Xml)).ToList().AsReadOnly();

        /// <summary>Ishonchli public key. KeyId — kalit qaysi juft bilan imzolangani (panel Licenses.KeyId ham shu).</summary>
        public sealed class TrustedPublicKey
        {
            public TrustedPublicKey(string keyId, string xml) { KeyId = keyId; Xml = xml; }
            public string KeyId { get; private set; }
            public string Xml { get; private set; }
        }

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

            /// <summary>Imzoni tasdiqlagan ishonchli kalit (k1/k2). Ochiq XML bilan tekshirilganda null.</summary>
            public string KeyId { get; set; }

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
        /// Yangi RSA 2048 kalit juftini yaratadi (bir martalik operatsiya).
        /// privateXml — faqat sizda saqlanadi. publicXml — LicenseHelper.PublicKeyXml ga.
        /// </summary>
        public static void GenerateKeyPair(out string publicXml, out string privateXml)
        {
            using (var rsa = CreateRsa(KeySizeBits))
            {
                RSAParameters p = rsa.ExportParameters(true);
                if (p.Modulus == null || p.Modulus.Length * 8 < KeySizeBits)
                    throw new CryptographicException("RSA kaliti " + KeySizeBits + " bit bo'lmadi.");
                publicXml = ToXml(p, false);
                privateXml = ToXml(p, true);
            }
        }

        /// <summary>Yangi kalit juftlari hajmi.</summary>
        public const int KeySizeBits = 2048;

        // .NET Framework'da RSA.Create() — RSACryptoServiceProvider (1024 bit), unda KeySize setter kalitni
        // qayta yaratmaydi; shuning uchun CSP bo'lsa hajm konstruktorda beriladi. .NET Core/.NET 10 da setter ishlaydi.
        private static RSA CreateRsa(int keySize)
        {
            RSA rsa = RSA.Create();
            if (rsa is RSACryptoServiceProvider)
            {
                rsa.Dispose();
                return new RSACryptoServiceProvider(keySize) { PersistKeyInCsp = false };
            }
            rsa.KeySize = keySize;
            return rsa;
        }

        /// <summary>
        /// Imzolangan v2 litsenziya kalitini yaratadi (o'rnatmaga bog'langan). PRIVATE key kerak.
        /// </summary>
        public static string GenerateKey(int companyId, int maxUsers, DateTime startDate,
            DateTime expiryDate, string edition, string installationId, string privateXml)
        {
            return Sign(BuildV2Payload(companyId, maxUsers, startDate, expiryDate, edition, installationId), privateXml);
        }

        /// <summary>
        /// v2 payload baytlari (imzolanadigan qism). Panel buni KMS Sign(RSASSA_PKCS1_V1_5_SHA_256,
        /// MessageType=RAW) ga beradi va natijani <see cref="AssembleKey"/> bilan kalitga yig'adi.
        /// </summary>
        public static byte[] BuildV2Payload(int companyId, int maxUsers, DateTime startDate,
            DateTime expiryDate, string edition, string installationId)
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
            return Encoding.UTF8.GetBytes(payload);
        }

        /// <summary>Payload + imzo → kalit matni: Base64(payload) + "." + Base64(imzo).</summary>
        public static string AssembleKey(byte[] payloadUtf8, byte[] signature)
        {
            if (payloadUtf8 == null) throw new ArgumentNullException(nameof(payloadUtf8));
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            return Convert.ToBase64String(payloadUtf8) + SEPARATOR + Convert.ToBase64String(signature);
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
            return Sign(Encoding.UTF8.GetBytes(payload), privateXml);
        }

        private static string Sign(byte[] payloadBytes, string privateXml)
        {
            if (string.IsNullOrWhiteSpace(privateXml))
                throw new ArgumentException("Private key berilmadi.", nameof(privateXml));

            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(FromXml(privateXml, true));
                byte[] signature = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return AssembleKey(payloadBytes, signature);
            }
        }

        #endregion

        #region Tekshirish (PUBLIC key — ERP ishlatadi)

#if DEBUG
        // Faqat Debug build: integratsion testlar o'z kalit jufti bilan imzolangan kalitni
        // tekshirishi uchun (reflection orqali o'rnatiladi). Release'da bu maydon umuman yo'q.
        private static string _debugTestPublicKeyXml = null;
#endif

        /// <summary>Kalitni kod ichidagi ishonchli public key'lar (k1, k2) bilan tekshiradi.</summary>
        public static bool TryParseKey(string licenseKey, out LicenseInfo info)
        {
            IEnumerable<TrustedPublicKey> keys = TrustedPublicKeys;
#if DEBUG
            if (_debugTestPublicKeyXml != null)
                keys = new[] { new TrustedPublicKey("debug", _debugTestPublicKeyXml) };
#endif
            return TryParseKey(licenseKey, keys, out info);
        }

        /// <summary>
        /// Kalitni ochib, imzosini berilgan PUBLIC key bilan tekshiradi.
        /// Imzo yaroqsiz/soxta yoki kalit buzilgan bo'lsa — false qaytaradi.
        /// </summary>
        public static bool TryParseKey(string licenseKey, string publicXml, out LicenseInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(publicXml))
                return false;
            return TryParseKey(licenseKey, new[] { new TrustedPublicKey(null, publicXml) }, out info);
        }

        /// <summary>
        /// Kalitni bir nechta ishonchli public key bilan tekshiradi — birortasi mos kelsa yetarli
        /// (<see cref="LicenseInfo.KeyId"/> = mos kelgan kalit). Hech biri mos kelmasa info
        /// to'ldiriladi, lekin SignatureValid=false va natija false.
        /// </summary>
        public static bool TryParseKey(string licenseKey, IEnumerable<TrustedPublicKey> trustedKeys, out LicenseInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(licenseKey) || trustedKeys == null)
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

                string payload = Encoding.UTF8.GetString(payloadBytes);
                var parsed = ParsePayload(payload);
                if (parsed == null)
                    return false;

                foreach (var key in trustedKeys)
                {
                    if (key == null || string.IsNullOrWhiteSpace(key.Xml))
                        continue;
                    if (Verify(payloadBytes, signature, key.Xml))
                    {
                        parsed.SignatureValid = true;
                        parsed.KeyId = key.KeyId;
                        break;
                    }
                }

                info = parsed;
                return parsed.SignatureValid;
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

        private static bool Verify(byte[] payloadBytes, byte[] signature, string publicXml)
        {
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(FromXml(publicXml, false));
                    return rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (CryptographicException)
            {
                return false; // shu kalit bilan emas — keyingisi sinab ko'riladi
            }
            catch (FormatException)
            {
                return false; // XML ichidagi Base64 buzilgan
            }
        }

        #endregion

        #region RSA kalit XML (RSACryptoServiceProvider.ToXmlString bilan bir xil format)

        // netstandard2.0 da RSA.FromXmlString/ToXmlString hamma platformada ishlamaydi (.NET Core 2.x da
        // PlatformNotSupported) — shuning uchun o'zimiz o'qiymiz/yozamiz. Format o'zgarmagan: mavjud
        // private key fayllari va PublicKeyXml const'i avvalgidek ishlaydi.

        private static readonly string[] PrivateElements = { "P", "Q", "DP", "DQ", "InverseQ", "D" };

        /// <summary>RSAKeyValue XML → RSAParameters.</summary>
        public static RSAParameters FromXml(string xml, bool includePrivate)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new CryptographicException("RSA kaliti berilmadi.");

            XElement root;
            try { root = XElement.Parse(xml); }
            catch (System.Xml.XmlException ex) { throw new CryptographicException("RSA kaliti XML'i buzilgan: " + ex.Message); }
            if (root.Name.LocalName != "RSAKeyValue")
                throw new CryptographicException("RSAKeyValue XML kutilgan edi.");

            Func<string, byte[]> read = name =>
            {
                XElement e = root.Element(name);
                return e == null || string.IsNullOrWhiteSpace(e.Value) ? null : Convert.FromBase64String(e.Value.Trim());
            };

            var p = new RSAParameters { Modulus = read("Modulus"), Exponent = read("Exponent") };
            if (p.Modulus == null || p.Exponent == null)
                throw new CryptographicException("RSA kalitida Modulus/Exponent yo'q.");

            if (includePrivate)
            {
                p.P = read("P"); p.Q = read("Q"); p.DP = read("DP"); p.DQ = read("DQ");
                p.InverseQ = read("InverseQ"); p.D = read("D");
                if (p.D == null || p.P == null || p.Q == null || p.DP == null || p.DQ == null || p.InverseQ == null)
                    throw new CryptographicException("Bu private key emas (D/P/Q/DP/DQ/InverseQ to'liq emas).");
            }
            return p;
        }

        /// <summary>RSAParameters → RSAKeyValue XML (ToXmlString bilan bir xil element tartibi).</summary>
        public static string ToXml(RSAParameters p, bool includePrivate)
        {
            var sb = new StringBuilder("<RSAKeyValue>");
            sb.Append("<Modulus>").Append(Convert.ToBase64String(p.Modulus)).Append("</Modulus>");
            sb.Append("<Exponent>").Append(Convert.ToBase64String(p.Exponent)).Append("</Exponent>");
            if (includePrivate)
            {
                byte[][] values = { p.P, p.Q, p.DP, p.DQ, p.InverseQ, p.D };
                for (int i = 0; i < PrivateElements.Length; i++)
                    sb.Append('<').Append(PrivateElements[i]).Append('>')
                      .Append(Convert.ToBase64String(values[i]))
                      .Append("</").Append(PrivateElements[i]).Append('>');
            }
            return sb.Append("</RSAKeyValue>").ToString();
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

        /// <summary>Doimiy vaqtli taqqoslash (timing hujumiga qarshi).</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
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
