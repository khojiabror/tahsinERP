using System;
using System.Data.SqlClient;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Configuration;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Helpers;

namespace tahsinERP.Tests.Tenancy
{
    [TestClass]
    public class TenantTests
    {
        private const string Json = @"{
          ""baseDomain"": ""Example.UZ"",
          ""tenants"": [
            { ""slug"": ""firma1"", ""database"": ""DB_firma1"", ""companyId"": 3 },
            { ""slug"": ""Firma2"", ""database"": ""DB_firma2"", ""hosts"": [""erp.firma2.uz""] },
            { ""slug"": ""old"", ""database"": ""DB_old"", ""enabled"": false }
          ]
        }";

        [TestCleanup]
        public void Cleanup()
        {
            TenantRegistry.Override = null;
        }

        private static TenantRegistry Active()
        {
            var reg = TenantRegistry.Parse(Json, null);
            Assert.IsFalse(reg.IsBroken, reg.Error);
            return reg;
        }

        #region Reestr va host

        [TestMethod]
        public void Parse_EmptyOrMissing_IsInactive()
        {
            Assert.IsFalse(TenantRegistry.Parse(null, null).IsActive);
            Assert.IsFalse(TenantRegistry.Parse("", "x.uz").IsActive);
            Assert.IsFalse(TenantRegistry.Parse(@"{ ""baseDomain"": ""x.uz"", ""tenants"": [] }", null).IsActive);
        }

        [TestMethod]
        public void ResolveHost_SubdomainOfBaseDomain()
        {
            var reg = Active();
            Assert.IsTrue(reg.IsActive);
            Assert.AreEqual("DB_firma1", reg.ResolveHost("firma1.example.uz").Database);
            Assert.AreEqual("DB_firma1", reg.ResolveHost("FIRMA1.Example.Uz:8080").Database, "registr va port ahamiyatsiz");
            Assert.AreEqual("DB_firma1", reg.ResolveHost("firma1.example.uz.").Database);
            Assert.AreEqual(3, reg.ResolveHost("firma1.example.uz").CompanyId);
            Assert.AreEqual("firma2", reg.ResolveHost("firma2.example.uz").Slug, "slug kichik harfga keltiriladi");
        }

        [TestMethod]
        public void ResolveHost_AliasHost()
        {
            Assert.AreEqual("DB_firma2", Active().ResolveHost("ERP.firma2.uz").Database);
        }

        [TestMethod]
        public void ResolveHost_UnknownOrForeign_ReturnsNull()
        {
            var reg = Active();
            Assert.IsNull(reg.ResolveHost("nope.example.uz"));
            Assert.IsNull(reg.ResolveHost("example.uz"));
            Assert.IsNull(reg.ResolveHost("a.firma1.example.uz"), "ikki darajali subdomen qabul qilinmaydi");
            Assert.IsNull(reg.ResolveHost("firma1.example.uz.evil.com"));
            Assert.IsNull(reg.ResolveHost("firma1example.uz"));
            Assert.IsNull(reg.ResolveHost("localhost"));
            Assert.IsNull(reg.ResolveHost(null));
            Assert.IsNull(reg.ResolveHost("old.example.uz"), "o'chirilgan tenant");
        }

        [TestMethod]
        public void BaseDomainOverride_Wins()
        {
            var reg = TenantRegistry.Parse(Json, "other.uz");
            Assert.IsNull(reg.ResolveHost("firma1.example.uz"));
            Assert.IsNotNull(reg.ResolveHost("firma1.other.uz"));
        }

        [TestMethod]
        public void Parse_Invalid_IsBroken()
        {
            Assert.IsTrue(TenantRegistry.Parse("{ not json", "x.uz").IsBroken);
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""a"",""database"":""D1""},{""slug"":""A"",""database"":""D2""}]}", "x.uz").IsBroken, "takroriy slug");
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""a"",""database"":""D1;Password=x""}]}", "x.uz").IsBroken, "baza nomida in'ektsiya");
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""a.b"",""database"":""D1""}]}", "x.uz").IsBroken, "slugda nuqta");
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""a"",""database"":""D1""}]}", null).IsBroken, "bazaviy domen yo'q");
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""admin"",""database"":""D1""}]}", "x.uz").IsBroken, "panel subdomeni");
            Assert.IsTrue(TenantRegistry.Parse(@"{""tenants"":[{""slug"":""pay"",""database"":""D1""}]}", "x.uz").IsBroken, "to'lov subdomeni");
            var broken = TenantRegistry.Parse("{ not json", "x.uz");
            Assert.IsTrue(broken.IsActive, "buzilgan reestr bitta-baza rejimiga tushmaydi");
        }

        #endregion

        #region Connection string

        [TestMethod]
        public void TenantConnectionString_ReplacesOnlyCatalog()
        {
            const string baseCs = "Data Source=srv1;Initial Catalog=DBTHSN-IPACK;User ID=u;Password=p;";
            var t = new TenantInfo { Slug = "firma1", Database = "DB_firma1" };
            var b = new SqlConnectionStringBuilder(DbConfig.BuildTenantSqlConnectionString(baseCs, t));
            Assert.AreEqual("DB_firma1", b.InitialCatalog);
            Assert.AreEqual("srv1", b.DataSource);
            Assert.AreEqual("u", b.UserID);
            Assert.AreEqual("p", b.Password);
        }

        [TestMethod]
        public void TenantConnectionString_OwnServerOverride()
        {
            var t = new TenantInfo { Slug = "f", Database = "DB_f", ConnectionString = "Data Source=srv2;Initial Catalog=ignored;Integrated Security=True" };
            var b = new SqlConnectionStringBuilder(DbConfig.BuildTenantSqlConnectionString("Data Source=srv1;Initial Catalog=X", t));
            Assert.AreEqual("srv2", b.DataSource);
            Assert.AreEqual("DB_f", b.InitialCatalog);
        }

        [TestMethod]
        public void EntityConnectionString_FollowsCurrentTenant()
        {
            var reg = Active();
            TenantRegistry.Override = reg;
            using (TenantContext.Use(reg.FindBySlug("firma1")))
                StringAssert.Contains(DbConfig.GetEntityConnectionString(), "DB_firma1");
            using (TenantContext.Use(reg.FindBySlug("firma2")))
                StringAssert.Contains(DbConfig.GetEntityConnectionString(), "DB_firma2");
        }

        #endregion

        #region TenantContext

        [TestMethod]
        public void SingleDbMode_NoTenant_KeysUnchanged()
        {
            TenantRegistry.Override = TenantRegistry.Empty;
            Assert.IsFalse(TenantContext.IsMultiTenant);
            Assert.IsNull(TenantContext.Current);
            Assert.AreEqual("k", TenantContext.CacheKey("k"));
            StringAssert.Contains(DbConfig.GetEntityConnectionString(), "DBTHSN-IPACK", "bitta-baza rejimi avvalgidek");
        }

        [TestMethod]
        public void MultiTenant_NoContext_FailsClosed()
        {
            TenantRegistry.Override = Active();
            Assert.ThrowsException<TenantNotResolvedException>(() => { var x = TenantContext.Current; });
            Assert.ThrowsException<TenantNotResolvedException>(() => DbConfig.GetEntityConnectionString());
            Assert.ThrowsException<TenantNotResolvedException>(() => TenantContext.CacheKey("k"));
        }

        [TestMethod]
        public void Scope_PrefixesCacheKeys_AndRestores()
        {
            var reg = Active();
            TenantRegistry.Override = reg;
            using (TenantContext.Use(reg.FindBySlug("firma1")))
            {
                Assert.AreEqual("t:firma1:k", TenantContext.CacheKey("k"));
                using (TenantContext.Use(reg.FindBySlug("firma2")))
                    Assert.AreEqual("t:firma2:k", TenantContext.CacheKey("k"));
                Assert.AreEqual("t:firma1:k", TenantContext.CacheKey("k"), "ichki doira yopilgach tiklanadi");
            }
            Assert.IsNull(TenantContext.CurrentOrNull);
        }

        [TestMethod]
        public async Task Scope_FlowsIntoTaskRunAndAwait()
        {
            var reg = Active();
            TenantRegistry.Override = reg;
            using (TenantContext.Use(reg.FindBySlug("firma2")))
            {
                string inTask = await Task.Run(() => TenantContext.Current.Slug);
                Assert.AreEqual("firma2", inTask);
                await Task.Delay(1);
                Assert.AreEqual("firma2", TenantContext.Current.Slug);
            }
        }

        #endregion

        #region Auth ticket bog'lash

        [TestMethod]
        public void TicketUserData_BindsTenant()
        {
            var reg = Active();
            TenantRegistry.Override = reg;
            string data;
            using (TenantContext.Use(reg.FindBySlug("firma1")))
                data = TenantHttp.BindUserData("Ali Valiyev");

            Assert.AreEqual("firma1", TenantHttp.TenantOf(data));
            Assert.AreEqual("Ali Valiyev", TenantHttp.StripTenant(data));
            Assert.IsNull(TenantHttp.TenantOf("Ali Valiyev"), "eski (tenant'siz) ticket");
            Assert.AreEqual("Ali Valiyev", TenantHttp.StripTenant("Ali Valiyev"));
        }

        [TestMethod]
        public void TicketUserData_SingleDbMode_Unchanged()
        {
            TenantRegistry.Override = TenantRegistry.Empty;
            Assert.AreEqual("Ali", TenantHttp.BindUserData("Ali"));
        }

        #endregion

        #region Keshlar tenant bo'yicha ajralgan

        [TestMethod]
        public void LoginThrottle_IsolatedPerTenant()
        {
            var reg = Active();
            TenantRegistry.Override = reg;
            var throttle = new LoginThrottle(new MemoryCache("tenant-test"), () => DateTime.UtcNow) { MaxFailures = 2 };
            TimeSpan left;

            using (TenantContext.Use(reg.FindBySlug("firma1")))
            {
                throttle.RegisterFailure("admin", "1.2.3.4");
                Assert.IsTrue(throttle.RegisterFailure("admin", "1.2.3.4"));
                Assert.IsTrue(throttle.IsLockedOut("admin", "1.2.3.4", out left));
            }
            using (TenantContext.Use(reg.FindBySlug("firma2")))
                Assert.IsFalse(throttle.IsLockedOut("admin", "1.2.3.4", out left), "boshqa tenantdagi admin bloklanmaydi");
        }

        #endregion
    }
}
