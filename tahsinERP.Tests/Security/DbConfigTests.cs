using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Configuration;

namespace tahsinERP.Tests.Security
{
    [TestClass]
    public class DbConfigTests
    {
        [TestMethod]
        public void Resolve_EnvironmentWins()
        {
            Assert.AreEqual("env-cs", DbConfig.Resolve("  env-cs  ", "file-cs"));
        }

        [TestMethod]
        public void Resolve_FileUsedWhenNoEnvironment()
        {
            Assert.AreEqual("file-cs", DbConfig.Resolve(null, "file-cs\r\n"));
            Assert.AreEqual("file-cs", DbConfig.Resolve("   ", "file-cs"));
        }

        [TestMethod]
        public void Resolve_FallsBackToEmbeddedBlob()
        {
            // Parolni test chiqishiga chiqarmaymiz — faqat tuzilishini tekshiramiz
            string cs = DbConfig.Resolve(null, null);
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(cs);
            Assert.AreEqual("DBTHSN-IPACK", builder.InitialCatalog);
            Assert.IsFalse(string.IsNullOrEmpty(builder.DataSource));
        }
    }
}
