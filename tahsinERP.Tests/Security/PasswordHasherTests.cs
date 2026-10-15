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
            string legacy = tahsinERP.Controllers.Helper.EncodePassword("Eski_Parol", salt);

            bool needsRehash;
            Assert.IsTrue(PasswordHasher.Verify("Eski_Parol", legacy, salt, out needsRehash));
            Assert.IsTrue(needsRehash);
        }

        [TestMethod]
        public void Verify_LegacyFormat_WrongPassword_Fails()
        {
            const string salt = "abcDEF1234";
            string legacy = tahsinERP.Controllers.Helper.EncodePassword("Eski_Parol", salt);

            bool needsRehash;
            Assert.IsFalse(PasswordHasher.Verify("boshqa", legacy, salt, out needsRehash));
            Assert.IsFalse(needsRehash);
        }

        [TestMethod]
        public void LegacyHash_MatchesOriginalHelper()
        {
            Assert.AreEqual(
                tahsinERP.Controllers.Helper.EncodePassword("p@ss", "s4lt"),
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
    }
}
