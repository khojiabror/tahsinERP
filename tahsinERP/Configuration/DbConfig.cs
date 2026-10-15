using System;
using System.Data.Entity.Core.EntityClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.Configuration
{
    /// <summary>
    /// Ma'lumotlar bazasiga ulanish sozlamalarini bitta joydan boshqaradi.
    ///
    /// MAQSAD: parol/server web.config'da OCHIQ turmasin va publish (deploy)
    /// papkasida ochiq matnda ko'rinmasin. Connection string shu class ichida
    /// AES bilan SHIFRLANGAN holda turadi — kompilyatsiyada tahsinERP.dll ichiga
    /// tushadi. Publish papkasida ochiq matn qolmaydi.
    ///
    /// DIQQAT (xavfsizlik darajasi): kalit ham shu kodda bo'lgani uchun bu
    /// "obfuscation" darajasidagi himoya. Serverda papka titkilaydigan yoki
    /// web.config'ni ochadigan odamdan himoya qiladi; DLL'ni ataylab decompile
    /// qiladigan odamdan himoya qilmaydi. Shuning uchun serverda connection
    /// string TAHSINERP_DB muhit o'zgaruvchisi (AWS SSM Parameter Store) yoki
    /// %ProgramData%\tahsinERP\db.connection fayli orqali beriladi — ular
    /// bo'lsa kod ichidagi blob ishlatilmaydi (qarang: Resolve).
    ///
    /// KREDENSIAL O'ZGARSA (server IP / user / parol):
    ///   1) tools\Encrypt-ConnString.ps1 -Plain "yangi connection string"
    ///   2) chiqqan Base64 blob'ni pastdagi tegishli const ga qo'ying
    ///      (DEBUG = lokal, ELSE = server)
    ///   3) Rebuild + Publish
    /// </summary>
    public static class DbConfig
    {
        // --- tools\Encrypt-ConnString.ps1 dagi bilan AYNAN bir xil bo'lishi SHART ---
        private const string KeyPhrase = "tahsinERP.DbConfig.Key.2026#Sch";
        private const string IvPhrase = "tahsinERP.DbConfig.IV.2026#Sch";
        // --------------------------------------------------------------------------

#if DEBUG
        // LOKAL ishlab chiqish (localhost). Faqat Debug build'da ishlatiladi.
        private const string EncryptedSql =
        "8ae2LohYKhwKItHeH3ewORvSn95hkqrXHceR4zKLpvKqb3RgR6SKK2+Lo6xxVQ3xUTnyNUJSgXSIm97slCo/PPVv6cYwqBIvaxNeSWWTVPKA09nhI5wtCsmGpA8Sg8df74gZlNLB5+Zp/SfY0AbdKEHXft9RB1TAQNZqmu/sMHJuyj2g4C6nIgJgpohGKVdQcmllR3R8QaIaWr5LBmIK8Q==";
#else
        // SERVER (publish/Release) — 192.168.100.200. Fallback: muhit o'zgaruvchisi
        // yoki tashqi fayl berilmagan bo'lsa shu ishlatiladi.
        private const string EncryptedSql =
        "nZZIpAZEm5KkSqhxX/Pma5TwCIwlpXA9n134oEgUAiaUDfFCf03uIUDUrQiDd3s9BscqSiUoAiNKDeSIKYLmyIWXJcaPnvNIT3/XhZa/wNr9m7CtGmPLMuVeqg+gadfymSkEWqmxuCWt/w0P3BPnUth7glbVkUXijLwUkRQRSBLUZMttVtv1pKQTGgbFj/Vjb+lypQF8gXSM0YXlzskxNnUvg0BttCNPpPuE/8xgo3E=";
#endif

        /// <summary>EDMX metadata — model o'zgarmaguncha o'zgarmaydi.</summary>
        private const string Metadata =
            "res://*/Models.DBTHSNModel.csdl|" +
            "res://*/Models.DBTHSNModel.ssdl|" +
            "res://*/Models.DBTHSNModel.msl";

        private const string ProviderName = "System.Data.SqlClient";

        /// <summary>
        /// Entity Framework DbContext uchun to'liq EntityConnection string qaytaradi.
        /// DBTHSNEntities context ana shu qiymatdan foydalanadi.
        /// </summary>
        public static string GetEntityConnectionString()
        {
            var builder = new EntityConnectionStringBuilder
            {
                Metadata = Metadata,
                Provider = ProviderName,
                ProviderConnectionString = GetSqlConnectionString()
            };
            return builder.ToString();
        }

        /// <summary>
        /// Faqat SQL (provider) connection string — shifrdan ochilgan holda.
        /// Kelajakda xom ADO.NET (SqlConnection) kerak bo'lsa shundan foydalaning.
        /// </summary>
        public static string GetSqlConnectionString()
        {
            return _sql.Value;
        }

        /// <summary>Muhit o'zgaruvchisi: ochiq SQL connection string (AWS SSM → env).</summary>
        public const string EnvConnectionString = "TAHSINERP_DB";

        /// <summary>Muhit o'zgaruvchisi: connection string yozilgan fayl yo'li (ixtiyoriy).</summary>
        public const string EnvConnectionFile = "TAHSINERP_DB_FILE";

        /// <summary>Standart tashqi fayl — webroot'dan TASHQARIDA, git'ga kirmaydi.</summary>
        public static readonly string DefaultConnectionFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "tahsinERP", "db.connection");

        private static readonly Lazy<string> _sql = new Lazy<string>(() => Resolve(
            Environment.GetEnvironmentVariable(EnvConnectionString),
            ReadFileOrNull(Environment.GetEnvironmentVariable(EnvConnectionFile))
                ?? ReadFileOrNull(DefaultConnectionFile)));

        /// <summary>
        /// Manba tartibi: 1) TAHSINERP_DB env, 2) tashqi fayl (TAHSINERP_DB_FILE
        /// yoki %ProgramData%\tahsinERP\db.connection), 3) kod ichidagi shifrlangan blob.
        /// </summary>
        public static string Resolve(string envValue, string fileValue)
        {
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue.Trim();
            if (!string.IsNullOrWhiteSpace(fileValue))
                return fileValue.Trim();
            return Decrypt(EncryptedSql);
        }

        private static string ReadFileOrNull(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string Decrypt(string base64Cipher)
        {
            byte[] cipher = Convert.FromBase64String(base64Cipher);
            using (var aes = CreateAes())
            using (var decryptor = aes.CreateDecryptor())
            {
                byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
        }

        private static Aes CreateAes()
        {
            byte[] key, iv;
            using (var sha = SHA256.Create())
                key = sha.ComputeHash(Encoding.UTF8.GetBytes(KeyPhrase));
            using (var md5 = MD5.Create())
                iv = md5.ComputeHash(Encoding.UTF8.GetBytes(IvPhrase)); // 16 bayt

            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            return aes;
        }
    }
}
