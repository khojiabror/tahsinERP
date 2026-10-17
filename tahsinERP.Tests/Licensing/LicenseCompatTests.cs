using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Licensing;

namespace tahsinERP.Tests.Licensing
{
    /// <summary>
    /// tahsinERP.Licensing (RSA.Create + SHA256/Pkcs1) eski RSACryptoServiceProvider kodi bilan
    /// imzolangan kalitlarni qabul qilishi va bir nechta ishonchli kalit ishlashi.
    /// </summary>
    [TestClass]
    public class LicenseCompatTests
    {
        // ESKI LicenseHelper (RSACryptoServiceProvider.SignData(.., "SHA256"), 9fca261a) bilan test kalit jufti
        // orqali imzolangan haqiqiy kalitlar. Private key saqlanmagan — faqat tekshiruv.
        private const string LegacyPublicXml =
            "<RSAKeyValue><Modulus>2Q5zM4AzN0Rfl0mm3aAwmEg1qYwfJYC55uY97u8VDn4xoDkbc56jQx5p+ztpXBMKiFWzSCjBrdtmN1fMZiRPtyzxfE6pW0l9urmQn5n492GWVUfBmtwSVKS/BqEIUnoeQ335HUlmZqCzfEod4fTNzD3aKFFFdT/OFkXaDwDS+OUb3VrhzMguzVewqR0Jb5N9BCLE1DQlKdOKywIPby1cUvM1GBLLvR6JgYP08qv/3TqRk5yk765I3B4qZJd2RSRceErInDh7KO5vjg0t2KqlmyOOyZsmL3x8I3oyTLsraHbww9JK1CuBNDzgA30oXU0Z1GkqcWfX5AUQyAvW74h1DQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private const string LegacyV2Key =
            "djJ8MXwyMHwyMDI2MDEwMXwyMDM2MTIzMXwzRjI1MDRFMC00Rjg5LTExRDMtOUEwQy0wMzA1RTgyQzMzMDF8QklaTkVT.E7TIX22BZ/SlgH6fs/idaJphONknsWeX8DPdnSB2pAuxyh9psv6KTlwsdwr8NS8h71bZNkypnsJYL0MLr24MZa8MWQNwUE1EWF+2ZOgxfYzE5jKVJssqQLki50HZgwitdwQkWiLo81M9h+mxJYw3modNyeVdYxkgPyc62rI+WqbHaBm25sxhMHQ6vpz1CdBaF2BxXZmbVPLCa6DlwVBoGOzMMkv+SmeHdBEPsAHdXjayvAs7cgMJXTkMlK6qLF14wyaVTr9mZ+araE2K1uEbx4CK/1q5v7e6TJ9qFC8L/y61NC82Z7JU+Yf+vP+TVqJvmLnffbLPbXMrj1jptpA/0A==";

        private const string LegacyV1Key =
            "MXwxMHwyMDI1MDEwMXwyMDM2MTIzMXxTdGFuZGFyZA==.WBuQsZEfW2QlJBkaCYJEjlo2NmPbad9XVOOkSfhCXSK5YzzQ1mVmhO0LpJyUPZsZN7F9Egzd5VDRLTg16CtmmzNxL9lmJc6swp7/xXrXfYqKqz6YxcSq5X1PwmXv8emuPZAu2zs6z0dXkKaism6ZzPXsPnF7tGVvHMDQwgAY/dXjVdfan6Gn0koWtJm8azEf1M4yuBh51EMeKltd4AYdU1vCnMFR4W94ACy4pter3oEtE96Acolo2EVpTCehBBQssxbK7xljBKBMbwD33TC0xyUcs5IsYel1XKdFD/bBVCnKlngrL46LFx+0XK3Kajjaa1+pEqE6Cl18w1FlhByv1w==";

        private const string Inst = "3F2504E0-4F89-11D3-9A0C-0305E82C3301";

        [TestMethod]
        public void LegacySignedV2Key_StillVerifies()
        {
            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(LegacyV2Key, LegacyPublicXml, out info));
            Assert.AreEqual(2, info.Version);
            Assert.AreEqual(20, info.MaxUsers);
            Assert.AreEqual("BIZNES", info.Edition);
            Assert.AreEqual(Inst, info.InstallationId);
            Assert.AreEqual(new DateTime(2036, 12, 31), info.ExpiryDate);
        }

        [TestMethod]
        public void LegacySignedV1Key_StillVerifies()
        {
            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(LegacyV1Key, LegacyPublicXml, out info));
            Assert.AreEqual(1, info.Version);
            Assert.AreEqual("Standard", info.Edition);
        }

        [TestMethod]
        public void Interop_CspSignature_EqualsNewSignature()
        {
            using (var csp = new RSACryptoServiceProvider(2048))
            {
                csp.PersistKeyInCsp = false;
                string privateXml = csp.ToXmlString(true);
                string publicXml = csp.ToXmlString(false);

                string newKey = LicenseHelper.GenerateKey(1, 5, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "START", Inst, privateXml);
                byte[] payload = LicenseHelper.BuildV2Payload(1, 5, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "START", Inst);
                // eski yo'l: RSACryptoServiceProvider.SignData(.., "SHA256")
                string oldKey = LicenseHelper.AssembleKey(payload, csp.SignData(payload, "SHA256"));

                Assert.AreEqual(oldKey, newKey, "PKCS#1 v1.5 deterministik — imzo bayt-ma-bayt bir xil");

                // va teskari: yangi imzoni eski CSP tekshiradi
                byte[] sig = Convert.FromBase64String(newKey.Substring(newKey.LastIndexOf('.') + 1));
                Assert.IsTrue(csp.VerifyData(payload, "SHA256", sig));

                LicenseHelper.LicenseInfo info;
                Assert.IsTrue(LicenseHelper.TryParseKey(oldKey, publicXml, out info));
            }
        }

        [TestMethod]
        public void ExternalSigner_KmsStyle_AssembleKeyVerifies()
        {
            // KMS RSASSA_PKCS1_V1_5_SHA_256 = SignData(SHA256, Pkcs1) — payload + tashqi imzo → kalit
            using (var rsa = RSA.Create())
            {
                rsa.KeySize = 2048;
                string publicXml = LicenseHelper.ToXml(rsa.ExportParameters(false), false);
                byte[] payload = LicenseHelper.BuildV2Payload(9, 50, DateTime.Today, DateTime.Today.AddYears(1), "KORP", Inst);
                string key = LicenseHelper.AssembleKey(payload, rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

                LicenseHelper.LicenseInfo info;
                Assert.IsTrue(LicenseHelper.TryParseKey(key, publicXml, out info));
                Assert.AreEqual("KORP", info.Edition);
            }
        }

        [TestMethod]
        public void MultipleTrustedKeys_AnyMatches_ReportsKeyId()
        {
            string pub1, priv1, pub2, priv2, pub3, priv3;
            LicenseHelper.GenerateKeyPair(out pub1, out priv1);
            LicenseHelper.GenerateKeyPair(out pub2, out priv2);
            LicenseHelper.GenerateKeyPair(out pub3, out priv3);
            var trusted = new[]
            {
                new LicenseHelper.TrustedPublicKey("k1", pub1),
                new LicenseHelper.TrustedPublicKey("k2", pub2),
            };

            LicenseHelper.LicenseInfo info;
            string byK2 = LicenseHelper.GenerateKey(1, 5, DateTime.Today, DateTime.Today.AddDays(30), "START", Inst, priv2);
            Assert.IsTrue(LicenseHelper.TryParseKey(byK2, trusted, out info));
            Assert.AreEqual("k2", info.KeyId);

            string byK1 = LicenseHelper.GenerateKey(1, 5, DateTime.Today, DateTime.Today.AddDays(30), "START", Inst, priv1);
            Assert.IsTrue(LicenseHelper.TryParseKey(byK1, trusted, out info));
            Assert.AreEqual("k1", info.KeyId);

            string byOther = LicenseHelper.GenerateKey(1, 5, DateTime.Today, DateTime.Today.AddDays(30), "START", Inst, priv3);
            Assert.IsFalse(LicenseHelper.TryParseKey(byOther, trusted, out info));
            Assert.IsFalse(info.SignatureValid);
            Assert.IsNull(info.KeyId);
        }

        [TestMethod]
        public void ProductionTrustedKeys_K1Present_EmptyK2Skipped()
        {
            Assert.IsTrue(LicenseHelper.TrustedPublicKeys.Any(k => k.KeyId == "k1" && k.Xml == LicenseHelper.PublicKeyXml));
            if (string.IsNullOrWhiteSpace(LicenseHelper.K2PublicKeyXml))
                Assert.IsFalse(LicenseHelper.TrustedPublicKeys.Any(k => k.KeyId == "k2"));
        }

        [TestMethod]
        public void KeyXml_SameFormatAsToXmlString()
        {
            using (var csp = new RSACryptoServiceProvider(2048))
            {
                csp.PersistKeyInCsp = false;
                var p = csp.ExportParameters(true);
                Assert.AreEqual(csp.ToXmlString(false), LicenseHelper.ToXml(p, false));
                Assert.AreEqual(csp.ToXmlString(true), LicenseHelper.ToXml(p, true));

                var back = LicenseHelper.FromXml(csp.ToXmlString(true), true);
                CollectionAssert.AreEqual(p.D, back.D);
            }
            Assert.ThrowsException<CryptographicException>(() => LicenseHelper.FromXml(LegacyPublicXml, true), "public key private sifatida");
        }

        [TestMethod]
        public void MalformedTrustedKey_DoesNotBreakOthers()
        {
            var trusted = new[]
            {
                new LicenseHelper.TrustedPublicKey("bad", "<RSAKeyValue><Modulus>!!</Modulus></RSAKeyValue>"),
                new LicenseHelper.TrustedPublicKey("legacy", LegacyPublicXml),
            };
            LicenseHelper.LicenseInfo info;
            Assert.IsTrue(LicenseHelper.TryParseKey(LegacyV2Key, trusted, out info));
            Assert.AreEqual("legacy", info.KeyId);
        }
    }
}
