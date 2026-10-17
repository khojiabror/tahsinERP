using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Licensing;

namespace tahsinERP.Tests.Licensing
{
    [TestClass]
    public class LicenseHelperTests
    {
        private static string _publicXml;
        private static string _privateXml;

        private const string InstA = "3F2504E0-4F89-11D3-9A0C-0305E82C3301";
        private const string InstB = "9B1DEB4D-3B7D-4BAD-9BDD-2B0D7B3DCB6D";

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            LicenseHelper.GenerateKeyPair(out _publicXml, out _privateXml);
        }

        private static string NewV2Key(string inst, DateTime start, DateTime expiry, int companyId = 1, int maxUsers = 10)
        {
            return LicenseHelper.GenerateKey(companyId, maxUsers, start, expiry, "Pro", inst, _privateXml);
        }

        #region Imzo

        [TestMethod]
        public void V2_ValidSignature_ParsesAllFields()
        {
            string key = NewV2Key(InstA.ToLowerInvariant(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 7, 25);

            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(key, _publicXml, out info));
            Assert.IsTrue(info.SignatureValid);
            Assert.AreEqual(2, info.Version);
            Assert.AreEqual(7, info.CompanyID);
            Assert.AreEqual(25, info.MaxUsers);
            Assert.AreEqual(new DateTime(2026, 1, 1), info.StartDate);
            Assert.AreEqual(new DateTime(2026, 12, 31), info.ExpiryDate);
            Assert.AreEqual("Pro", info.Edition);
            Assert.AreEqual(InstA, info.InstallationId, "InstallationId katta harfga normallashadi");
        }

        [TestMethod]
        public void TamperedPayload_SameSignature_Rejected()
        {
            string key = NewV2Key(InstA, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            int dot = key.LastIndexOf('.');
            string payload = Encoding.UTF8.GetString(Convert.FromBase64String(key.Substring(0, dot)));
            string forgedPayload = payload.Replace("20261231", "20991231");
            string forged = Convert.ToBase64String(Encoding.UTF8.GetBytes(forgedPayload)) + key.Substring(dot);

            LicenseHelper.LicenseInfo info;
            Assert.IsFalse(LicenseHelper.TryParseKey(forged, _publicXml, out info));
            Assert.IsNotNull(info);
            Assert.IsFalse(info.SignatureValid);
        }

        [TestMethod]
        public void KeySignedByOtherKeyPair_Rejected()
        {
            string otherPublic, otherPrivate;
            LicenseHelper.GenerateKeyPair(out otherPublic, out otherPrivate);
            string forged = LicenseHelper.GenerateKey(1, 1000, DateTime.Today, DateTime.Today.AddYears(10), "Enterprise", InstA, otherPrivate);

            LicenseHelper.LicenseInfo info;
            Assert.IsFalse(LicenseHelper.TryParseKey(forged, _publicXml, out info));
        }

        [TestMethod]
        public void GarbageKeys_Rejected()
        {
            LicenseHelper.LicenseInfo info;
            Assert.IsFalse(LicenseHelper.TryParseKey(null, _publicXml, out info));
            Assert.IsFalse(LicenseHelper.TryParseKey("", _publicXml, out info));
            Assert.IsFalse(LicenseHelper.TryParseKey("abc.def", _publicXml, out info));
            Assert.IsFalse(LicenseHelper.TryParseKey("no-separator", _publicXml, out info));
        }

        [TestMethod]
        public void EmbeddedPublicKey_IsPublicOnlyRsa2048()
        {
            Assert.IsFalse(LicenseHelper.PublicKeyXml.Contains("<D>"), "Kod ichida private key bo'lmasligi kerak");
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(LicenseHelper.PublicKeyXml);
                Assert.AreEqual(2048, rsa.KeySize);
            }
        }

        [TestMethod]
        public void DefaultOverload_UsesEmbeddedKey_RejectsTestKeyPair()
        {
            // Test kalit jufti bilan imzolangan kalit vendor public key bilan o'tmasligi kerak
            string key = NewV2Key(InstA, DateTime.Today, DateTime.Today.AddYears(1));
            LicenseHelper.LicenseInfo info;
            Assert.IsFalse(LicenseHelper.TryParseKey(key, out info));
        }

        #endregion

        #region O'rnatmaga bog'lanish, v1/v2

        [TestMethod]
        public void V2_BoundToItsInstallationOnly()
        {
            string key = NewV2Key(InstA, DateTime.Today, DateTime.Today.AddYears(1));
            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(key, _publicXml, out info));

            Assert.IsTrue(info.IsBoundTo(InstA));
            Assert.IsTrue(info.IsBoundTo("{" + InstA.ToLowerInvariant() + "}"), "GUID formati farq qilmasligi kerak");
            Assert.IsFalse(info.IsBoundTo(InstB));
            Assert.IsFalse(info.IsBoundTo(null));
            Assert.IsFalse(info.IsBoundTo("not-a-guid"));
        }

        [TestMethod]
        public void V1_LegacyKey_StillParses_AndIsNotInstallationBound()
        {
            string key = LicenseHelper.GenerateLegacyV1Key(1, 10, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "Basic", _privateXml);

            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(key, _publicXml, out info));
            Assert.AreEqual(1, info.Version);
            Assert.IsNull(info.InstallationId);
            Assert.AreEqual("Basic", info.Edition);
            Assert.IsTrue(info.IsBoundTo(InstA), "v1 o'tish davrida har qanday o'rnatmada qabul qilinadi");
            Assert.IsTrue(info.IsBoundTo(InstB));
        }

        [TestMethod]
        public void V1_KeyFromOldFormat_ExactBytes_Parses()
        {
            // Eski generator aynan shu ko'rinishdagi payload imzolagan: "CompanyID|MaxUsers|Start|Expiry|Edition"
            byte[] payload = Encoding.UTF8.GetBytes("1|5|20260101|20261231|Pro");
            string key;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(_privateXml);
                key = Convert.ToBase64String(payload) + "." + Convert.ToBase64String(rsa.SignData(payload, "SHA256"));
            }

            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(key, _publicXml, out info));
            Assert.AreEqual(1, info.Version);
            Assert.AreEqual(5, info.MaxUsers);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GenerateV2_RequiresValidInstallationId()
        {
            LicenseHelper.GenerateKey(1, 10, DateTime.Today, DateTime.Today.AddYears(1), "Pro", "123", _privateXml);
        }

        #endregion

        #region Muddat holatlari

        [TestMethod]
        public void State_ValidExpiringGraceExpiredNotStarted()
        {
            var info = new LicenseHelper.LicenseInfo
            {
                Version = 2,
                StartDate = new DateTime(2026, 1, 1),
                ExpiryDate = new DateTime(2026, 6, 30),
                SignatureValid = true
            };

            Assert.AreEqual(LicenseHelper.LicenseState.NotStarted, LicenseHelper.GetState(info, new DateTime(2025, 12, 31)));
            Assert.AreEqual(LicenseHelper.LicenseState.Valid, LicenseHelper.GetState(info, new DateTime(2026, 6, 15)));
            Assert.AreEqual(LicenseHelper.LicenseState.ExpiringSoon, LicenseHelper.GetState(info, new DateTime(2026, 6, 16)), "14 kun qolganda");
            Assert.AreEqual(LicenseHelper.LicenseState.ExpiringSoon, LicenseHelper.GetState(info, new DateTime(2026, 6, 30, 23, 0, 0)), "oxirgi kun hali amal qiladi");
            Assert.AreEqual(LicenseHelper.LicenseState.Grace, LicenseHelper.GetState(info, new DateTime(2026, 7, 1)));
            Assert.AreEqual(LicenseHelper.LicenseState.Grace, LicenseHelper.GetState(info, new DateTime(2026, 7, 3)));
            Assert.AreEqual(LicenseHelper.LicenseState.Expired, LicenseHelper.GetState(info, new DateTime(2026, 7, 4)));
        }

        [TestMethod]
        public void ExpiredKey_SignatureStillValid_ButExpired()
        {
            string key = NewV2Key(InstA, new DateTime(2020, 1, 1), new DateTime(2020, 12, 31));
            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(key, _publicXml, out info));
            Assert.IsTrue(info.IsExpired);
            Assert.IsFalse(info.IsValid);
            Assert.AreEqual(LicenseHelper.LicenseState.Expired, LicenseHelper.GetState(info, DateTime.Now));
        }

        #endregion

        #region Soat muhri

        [TestMethod]
        public void ClockStamp_RoundTrip()
        {
            var t = new DateTime(2026, 10, 15, 10, 30, 0, DateTimeKind.Utc);
            string stamp = LicenseHelper.CreateClockStamp(t, InstA);

            DateTime read;
            Assert.IsTrue(LicenseHelper.TryReadClockStamp(stamp, InstA.ToLowerInvariant(), out read));
            Assert.AreEqual(t, read);
        }

        [TestMethod]
        public void ClockStamp_EditedTicks_Rejected()
        {
            var t = new DateTime(2026, 10, 15, 10, 30, 0, DateTimeKind.Utc);
            string stamp = LicenseHelper.CreateClockStamp(t, InstA);
            string older = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks + stamp.Substring(stamp.IndexOf(':'));

            DateTime read;
            Assert.IsFalse(LicenseHelper.TryReadClockStamp(older, InstA, out read));
            Assert.IsFalse(LicenseHelper.TryReadClockStamp("garbage", InstA, out read));
            Assert.IsFalse(LicenseHelper.TryReadClockStamp(null, InstA, out read));
        }

        [TestMethod]
        public void ClockStamp_FromOtherInstallation_Rejected()
        {
            string stamp = LicenseHelper.CreateClockStamp(DateTime.UtcNow, InstA);
            DateTime read;
            Assert.IsFalse(LicenseHelper.TryReadClockStamp(stamp, InstB, out read));
        }

        [TestMethod]
        public void ClockRollback_DetectedBeyondTolerance()
        {
            var lastSeen = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(LicenseHelper.IsClockRolledBack(lastSeen.AddHours(5), lastSeen), "oldinga — normal");
            Assert.IsFalse(LicenseHelper.IsClockRolledBack(lastSeen.AddHours(-2), lastSeen), "kichik tuzatish — ruxsat");
            Assert.IsTrue(LicenseHelper.IsClockRolledBack(lastSeen.AddDays(-30), lastSeen), "bir oy orqaga — blok");
        }

        #endregion
    }
}
