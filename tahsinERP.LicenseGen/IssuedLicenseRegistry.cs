using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace tahsinERP.LicenseGen
{
    public class IssuedLicenseRow
    {
        public DateTime IssuedAt { get; set; }
        public string IssuedBy { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; }
        public string InstallationId { get; set; }
        public int MaxUsers { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Edition { get; set; }
        public int KeyVersion { get; set; }
        public string Fingerprint { get; set; }
        public string LicenseKey { get; set; }
    }

    /// <summary>
    /// Vendor'ning O'Z lokal reestri: chiqarilgan har bir kalit shu yerga yoziladi
    /// (kim, qachon, qaysi kompaniya/InstallationId, muddat, kalitning o'zi).
    /// Mijoz bazasidan mustaqil — generator mijoz DB'siga ulanmasdan ham ishlaydi.
    ///
    /// Format: UTF-8 TSV, faqat qo'shib boriladi (append-only), private key fayli yonida.
    /// Qo'shimcha paket (SQLite) talab qilmaydi; Excel'da ochish mumkin.
    /// </summary>
    internal static class IssuedLicenseRegistry
    {
        private const string FILE_NAME = "issued_licenses.tsv";
        private const string DATE_FMT = "yyyy-MM-dd";
        private const string TS_FMT = "yyyy-MM-dd HH:mm:ss";

        private static readonly string[] Header =
        {
            "IssuedAt", "IssuedBy", "CompanyID", "CompanyName", "InstallationId", "MaxUsers",
            "StartDate", "ExpiryDate", "Edition", "KeyVersion", "Fingerprint", "LicenseKey"
        };

        public static string FilePath
        {
            get { return Path.Combine(Application.StartupPath, FILE_NAME); }
        }

        public static void Append(IssuedLicenseRow row)
        {
            bool isNew = !File.Exists(FilePath);
            var sb = new StringBuilder();
            if (isNew)
                sb.AppendLine(string.Join("\t", Header));

            sb.AppendLine(string.Join("\t", new[]
            {
                row.IssuedAt.ToString(TS_FMT, CultureInfo.InvariantCulture),
                Clean(row.IssuedBy),
                row.CompanyID.ToString(CultureInfo.InvariantCulture),
                Clean(row.CompanyName),
                Clean(row.InstallationId),
                row.MaxUsers.ToString(CultureInfo.InvariantCulture),
                row.StartDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                row.ExpiryDate.ToString(DATE_FMT, CultureInfo.InvariantCulture),
                Clean(row.Edition),
                row.KeyVersion.ToString(CultureInfo.InvariantCulture),
                Clean(row.Fingerprint),
                Clean(row.LicenseKey)
            }));

            File.AppendAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
        }

        public static List<IssuedLicenseRow> ReadAll()
        {
            var list = new List<IssuedLicenseRow>();
            if (!File.Exists(FilePath))
                return list;

            bool first = true;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                if (first) { first = false; continue; } // sarlavha
                if (string.IsNullOrWhiteSpace(line)) continue;

                var p = line.Split('\t');
                if (p.Length < Header.Length) continue;

                DateTime issued, start, expiry;
                int companyId, maxUsers, version;
                if (!DateTime.TryParseExact(p[0], TS_FMT, CultureInfo.InvariantCulture, DateTimeStyles.None, out issued)) continue;
                if (!int.TryParse(p[2], out companyId)) continue;
                int.TryParse(p[5], out maxUsers);
                DateTime.TryParseExact(p[6], DATE_FMT, CultureInfo.InvariantCulture, DateTimeStyles.None, out start);
                DateTime.TryParseExact(p[7], DATE_FMT, CultureInfo.InvariantCulture, DateTimeStyles.None, out expiry);
                int.TryParse(p[9], out version);

                list.Add(new IssuedLicenseRow
                {
                    IssuedAt = issued,
                    IssuedBy = p[1],
                    CompanyID = companyId,
                    CompanyName = p[3],
                    InstallationId = p[4],
                    MaxUsers = maxUsers,
                    StartDate = start,
                    ExpiryDate = expiry,
                    Edition = p[8],
                    KeyVersion = version,
                    Fingerprint = p[10],
                    LicenseKey = p[11]
                });
            }
            return list;
        }

        /// <summary>Kalitning qisqa barmoq izi (SHA-256, birinchi 8 bayt) — qidirish/taqqoslash uchun.</summary>
        public static string Fingerprint(string licenseKey)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(licenseKey ?? string.Empty));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }

        private static string Clean(string s)
        {
            return (s ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
