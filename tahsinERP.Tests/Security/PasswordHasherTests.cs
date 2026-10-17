using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Helpers;

namespace tahsinERP.Tests.Security
{
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod]
        public void Hash_NewFormat_HasPrefixAndFitsColumns()
        {
            string salt;
            string hash = PasswordHasher.Hash("Parol123!", out salt);

            StringAssert.StartsWith(hash, PasswordHasher.Prefix);
            Assert.IsTrue(hash.Length <= 50, "USERS.Password varchar(50)");
            Assert.IsTrue(salt.Length <= 30, "USERS.HashCode varchar(30)");
        }

        [TestMethod]
        public void Verify_NewFormat_CorrectPassword_NoRehash()
        {
            string salt;
            string hash = PasswordHasher.Hash("Parol123!", out salt);

            bool needsRehash;
            Assert.IsTrue(PasswordHasher.Verify("Parol123!", hash, salt, out needsRehash));
            Assert.IsFalse(needsRehash);
        }

        [TestMethod]
        public void Verify_NewFormat_WrongPassword_Fails()
        {
            string salt;
            string hash = PasswordHasher.Hash("Parol123!", out salt);

            bool needsRehash;
            Assert.IsFalse(PasswordHasher.Verify("parol123!", hash, salt, out needsRehash));
            Assert.IsFalse(needsRehash);
        }

        [TestMethod]
        public void Hash_SamePassword_DifferentSaltAndHash()
        {
            string salt1, salt2;
            string h1 = PasswordHasher.Hash("x", out salt1);
            string h2 = PasswordHasher.Hash("x", out salt2);

            Assert.AreNotEqual(salt1, salt2);
            Assert.AreNotEqual(h1, h2);
        }

        [TestMethod]
        public void Verify_LegacyFormat_CorrectPassword_RequestsRehash()
        {
            const string salt = "abcDEF1234";
            string legacy = OriginalEncodePassword("Eski_Parol", salt);

            bool needsRehash;
            Assert.IsTrue(PasswordHasher.Verify("Eski_Parol", legacy, salt, out needsRehash));
            Assert.IsTrue(needsRehash);
        }

        [TestMethod]
        public void Verify_LegacyFormat_WrongPassword_Fails()
        {
            const string salt = "abcDEF1234";
            string legacy = OriginalEncodePassword("Eski_Parol", salt);

            bool needsRehash;
            Assert.IsFalse(PasswordHasher.Verify("boshqa", legacy, salt, out needsRehash));
            Assert.IsFalse(needsRehash);
        }

        [TestMethod]
        public void LegacyHash_MatchesOriginalHelper()
        {
            Assert.AreEqual(
                OriginalEncodePassword("p@ss", "s4lt"),
                PasswordHasher.LegacyHash("p@ss", "s4lt"));
        }

        [TestMethod]
        public void Verify_EmptyOrCorrupt_ReturnsFalse()
        {
            bool needsRehash;
            Assert.IsFalse(PasswordHasher.Verify("x", null, "s", out needsRehash));
            Assert.IsFalse(PasswordHasher.Verify("x", "", "s", out needsRehash));
            Assert.IsFalse(PasswordHasher.Verify("x", PasswordHasher.Prefix + "!!!notbase64", "also-bad", out needsRehash));
        }

        /// <summary>
        /// Eski Controllers/AdminSetting/Helper.EncodePassword ning aynan nusxasi (fayl o'lik kod sifatida
        /// o'chirildi). PasswordHasher.LegacyHash ga mustaqil namuna bo'lib qoladi.
        /// </summary>
        private static string OriginalEncodePassword(string pass, string salt)
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(pass);
            byte[] src = System.Text.Encoding.Unicode.GetBytes(salt);
            byte[] dst = new byte[src.Length + bytes.Length];
            System.Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            System.Buffer.BlockCopy(bytes, 0, dst, src.Length, bytes.Length);
            byte[] inArray = System.Security.Cryptography.HashAlgorithm.Create("SHA1").ComputeHash(dst);
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] encoded = md5.ComputeHash(System.Text.Encoding.Default.GetBytes(System.Convert.ToBase64String(inArray)));
            return System.BitConverter.ToString(encoded);
        }
    }
}
