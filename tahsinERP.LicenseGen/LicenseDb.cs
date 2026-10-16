using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace tahsinERP.LicenseGen
{
    public class CompanyRow
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public override string ToString() { return ID + " — " + Name; }
    }

    public class LicenseRow
    {
        public int ID { get; set; }
        public int MaxUsers { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Edition { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string LicenseKey { get; set; }
    }

    /// <summary>
    /// Client bazasiga xom ADO.NET orqali ulanish. EF/edmx ishlatilmaydi —
    /// generator ERP kodidan mustaqil.
    /// </summary>
    internal class LicenseDb
    {
        private string _connString;

        public LicenseDb(string connString)
        {
            _connString = connString;
        }

        public string ConnectionString
        {
            get { return _connString; }
            set { _connString = value; }
        }

        public static string DefaultConnectionString()
        {
            var cs = ConfigurationManager.ConnectionStrings["ClientDb"];
            return cs != null ? cs.ConnectionString : "Data Source=.;Initial Catalog=DBTHSN;Integrated Security=True;TrustServerCertificate=True";
        }

        public void TestConnection()
        {
            using (var con = new SqlConnection(_connString))
            {
                con.Open();
            }
        }

        public List<CompanyRow> GetCompanies()
        {
            var list = new List<CompanyRow>();
            using (var con = new SqlConnection(_connString))
            using (var cmd = new SqlCommand(
                "SELECT ID, Name FROM dbo.COMPANIES ORDER BY Name", con))
            {
                con.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CompanyRow
                        {
                            ID = r.GetInt32(0),
                            Name = r.IsDBNull(1) ? "(nomsiz)" : r.GetString(1)
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>Kompaniyadagi faol (o'chirilmagan) xodimlar soni — MaxUsers taklifi uchun.</summary>
        public int GetActiveUserCount(int companyId)
        {
            using (var con = new SqlConnection(_connString))
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.USERS WHERE CompanyID = @c AND (IsDeleted IS NULL OR IsDeleted = 0)", con))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                con.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        public List<LicenseRow> GetCompanyLicenses(int companyId)
        {
            var list = new List<LicenseRow>();
            using (var con = new SqlConnection(_connString))
            using (var cmd = new SqlCommand(
                @"SELECT ID, MaxUsers, StartDate, ExpiryDate, Edition, IsActive, CreatedAt, LicenseKey
                  FROM dbo.COMPANY_LICENSES
                  WHERE CompanyID = @c
                  ORDER BY ID DESC", con))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                con.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LicenseRow
                        {
                            ID = r.GetInt32(0),
                            MaxUsers = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                            StartDate = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2),
                            ExpiryDate = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                            Edition = r.IsDBNull(4) ? "" : r.GetString(4),
                            IsActive = !r.IsDBNull(5) && r.GetBoolean(5),
                            CreatedAt = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
                            LicenseKey = r.IsDBNull(7) ? "" : r.GetString(7)
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Yangi litsenziyani bazaga yozadi. Bir kompaniyada bir vaqtda bitta
        /// faol litsenziya bo'lishi uchun eskilarini IsActive=0 qiladi.
        /// </summary>
        public int InsertLicense(int companyId, string licenseKey, int maxUsers,
            DateTime startDate, DateTime expiryDate, string edition, string createdBy)
        {
            using (var con = new SqlConnection(_connString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    using (var deact = new SqlCommand(
                        "UPDATE dbo.COMPANY_LICENSES SET IsActive = 0 WHERE CompanyID = @c", con, tx))
                    {
                        deact.Parameters.AddWithValue("@c", companyId);
                        deact.ExecuteNonQuery();
                    }

                    using (var cmd = new SqlCommand(
                        @"INSERT INTO dbo.COMPANY_LICENSES
                            (CompanyID, LicenseKey, MaxUsers, StartDate, ExpiryDate, Edition, IsActive, CreatedAt, CreatedBy)
                          VALUES (@c, @k, @m, @s, @e, @ed, 1, GETDATE(), @by);
                          SELECT CAST(SCOPE_IDENTITY() AS INT);", con, tx))
                    {
                        cmd.Parameters.AddWithValue("@c", companyId);
                        cmd.Parameters.AddWithValue("@k", licenseKey);
                        cmd.Parameters.AddWithValue("@m", maxUsers);
                        cmd.Parameters.AddWithValue("@s", startDate);
                        cmd.Parameters.AddWithValue("@e", expiryDate);
                        cmd.Parameters.AddWithValue("@ed", (object)edition ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@by", (object)createdBy ?? DBNull.Value);
                        int newId = (int)cmd.ExecuteScalar();
                        tx.Commit();
                        return newId;
                    }
                }
            }
        }
    }
}
