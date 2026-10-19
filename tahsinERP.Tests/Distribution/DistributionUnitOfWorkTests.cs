using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Configuration;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// S0 spike: distributsiya (Code-First) va ERP (EDMX) kontekstlari bitta tranzaksiyada.
    /// Lokal bazada (DbConfig, Debug) ishlaydi va HECH NARSANI saqlamaydi — har test rollback bilan tugaydi.
    /// Baza yoki dst sxemasi (migratsiya 013) bo'lmasa — Inconclusive.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DistributionUnitOfWorkTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.AUDIT_TRAIL', 'U')", c))
                    {
                        if (cmd.ExecuteScalar() is DBNull)
                            _sql = null;
                    }
                }
            }
            catch (Exception)
            {
                _sql = null;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (_sql == null)
                Assert.Inconclusive("Lokal baza yoki dst sxemasi (migratsiya 013) mavjud emas.");
        }

        private static object Scalar(string sql, params SqlParameter[] args)
        {
            using (var c = new SqlConnection(_sql))
            using (var cmd = new SqlCommand(sql, c))
            {
                cmd.Parameters.AddRange(args);
                c.Open();
                return cmd.ExecuteScalar();
            }
        }

        private static object Scalar(DistributionUnitOfWork uow, string sql, params SqlParameter[] args)
        {
            using (var cmd = new SqlCommand(sql, uow.Connection, uow.Transaction))
            {
                cmd.Parameters.AddRange(args);
                return cmd.ExecuteScalar();
            }
        }

        [TestMethod]
        public void SharedTransaction_BothContextsSeeAndRollBackTogether()
        {
            // ERP tomoni: dbo.CUSTOMERS (float -> decimal ishiga tegishli emas, EDMX va baza mos)
            var correlation = Guid.NewGuid();
            int customerId;
            string before;

            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var customer = uow.Erp.CUSTOMERS.OrderBy(c => c.ID).FirstOrDefault();
                if (customer == null) Assert.Inconclusive("CUSTOMERS bo'sh.");
                customerId = customer.ID;
                before = customer.ContactPersonName;

                uow.Dst.AuditTrail.Add(new AuditTrailEntry
                {
                    CorrelationId = correlation, EntityName = "Spike", EntityKey = "1", Action = AuditTrailEntry.Insert,
                    Source = AuditActor.System, ChangedAt = DateTime.Now
                });
                uow.Dst.SaveChanges();

                customer.ContactPersonName = "spike-" + correlation.ToString("N").Substring(0, 8);
                uow.Erp.SaveChanges();

                // Tranzaksiya ichida ikkala yozuv ham ko'rinadi
                Assert.AreEqual(1, Scalar(uow, "SELECT COUNT(*) FROM dst.AUDIT_TRAIL WHERE CorrelationId = @c",
                    new SqlParameter("@c", correlation)));
                Assert.AreEqual(customer.ContactPersonName, Scalar(uow, "SELECT ContactPersonName FROM dbo.CUSTOMERS WHERE ID = @id",
                    new SqlParameter("@id", customerId)));
                // Commit yo'q -> Dispose'da rollback
            }

            Assert.AreEqual(0, Scalar("SELECT COUNT(*) FROM dst.AUDIT_TRAIL WHERE CorrelationId = @c", new SqlParameter("@c", correlation)));
            var after = Scalar("SELECT ContactPersonName FROM dbo.CUSTOMERS WHERE ID = @id", new SqlParameter("@id", customerId));
            Assert.AreEqual(before, after is DBNull ? null : (string)after);
        }

        [TestMethod]
        public void AuditTrail_RecordsInsertUpdateDelete_InsideUnitOfWork()
        {
            var userIdObj = Scalar("SELECT TOP 1 ID FROM dbo.USERS ORDER BY ID");
            if (userIdObj == null) Assert.Inconclusive("USERS bo'sh.");
            int userId = (int)userIdObj;
            const int fakeScopeId = int.MaxValue - 17;
            int scopeRowId;

            using (var uow = new DistributionUnitOfWork(new AuditActor(userId, AuditActor.System)))
            {
                var scope = new UserScope { UserID = userId, ScopeType = ScopeTypes.Customer, ScopeID = fakeScopeId };
                uow.Dst.UserScopes.Add(scope);
                uow.Dst.SaveChanges();
                scopeRowId = scope.ID;
                Assert.IsTrue(scopeRowId > 0);

                scope.ScopeID = fakeScopeId - 1;
                uow.Dst.SaveChanges();

                uow.Dst.UserScopes.Remove(scope);
                uow.Dst.SaveChanges();

                var trail = uow.Dst.AuditTrail
                    .Where(a => a.EntityName == "UserScope" && a.EntityKey == scopeRowId.ToString())
                    .OrderBy(a => a.ID).ToList();

                Assert.AreEqual(3, trail.Count, string.Join(" | ", trail.Select(t => t.Action + ":" + t.FieldName)));
                Assert.AreEqual("I", trail[0].Action);
                StringAssert.Contains(trail[0].NewValue, "\"ScopeID\":\"" + fakeScopeId + "\"");

                Assert.AreEqual("U", trail[1].Action);
                Assert.AreEqual("ScopeID", trail[1].FieldName);
                Assert.AreEqual(fakeScopeId.ToString(), trail[1].OldValue);
                Assert.AreEqual((fakeScopeId - 1).ToString(), trail[1].NewValue);

                Assert.AreEqual("D", trail[2].Action);
                Assert.IsNotNull(trail[2].OldValue);
                Assert.IsTrue(trail.All(t => t.UserID == userId && t.Source == AuditActor.System));
            }

            Assert.AreEqual(0, Scalar("SELECT COUNT(*) FROM dst.AUDIT_TRAIL WHERE EntityName = 'UserScope' AND EntityKey = @k",
                new SqlParameter("@k", scopeRowId.ToString())));
        }

        [TestMethod]
        public void Standalone_SaveChanges_InsertIsAtomicWithItsAudit()
        {
            // UoW'siz kontekst: o'z tranzaksiyasini ochadi (qator + audit). Bu yerda tashqi tranzaksiya
            // ichida tekshiriladi va rollback qilinadi — bazada iz qolmaydi.
            var userIdObj = Scalar("SELECT TOP 1 ID FROM dbo.USERS ORDER BY ID");
            if (userIdObj == null) Assert.Inconclusive("USERS bo'sh.");
            int userId = (int)userIdObj;

            using (var c = new SqlConnection(_sql))
            {
                c.Open();
                using (var db = new DistributionDbContext(c, false) { Actor = new AuditActor(userId, AuditActor.System) })
                using (var outer = db.Database.BeginTransaction())
                {
                    var scope = new UserScope { UserID = userId, ScopeType = ScopeTypes.Customer, ScopeID = int.MaxValue - 23 };
                    db.UserScopes.Add(scope);
                    db.SaveChanges();

                    Assert.AreEqual(1, db.AuditTrail.Count(a => a.EntityName == "UserScope" && a.EntityKey == scope.ID.ToString() && a.Action == "I"));
                    outer.Rollback();
                }
            }
        }
    }
}
