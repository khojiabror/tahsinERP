using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace tahsinERP.LicenseGen
{
    /// <summary>
    /// RSA private key'ni master parol bilan AES-256 shifrlab, dastur yonidagi
    /// faylda saqlaydi. Private key HECH QACHON ochiq holda diskda turmaydi va
    /// .exe ichida ham bo'lmaydi. Master parolsiz faylni ochib bo'lmaydi.
    ///
    /// Fayl formati:  [16 bayt salt][16 bayt IV][AES(ciphertext)]
    /// Plaintext ichida "TAHSINPK1\n" magic bor — noto'g'ri parolni aniqlash uchun.
    /// </summary>
    internal static class PrivateKeyStore
    {
        private const string FILE_NAME = "license_private.dat";
        private const string MAGIC = "TAHSINPK1\n";
        private const int SALT_SIZE = 16;
        private const int IV_SIZE = 16;
        private const int KEY_SIZE = 32;      // AES-256
        private const int ITERATIONS = 100000;

        public static string FilePath
        {
            get { return Path.Combine(Application.StartupPath, FILE_NAME); }
        }

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        /// <summary>Private key XML'ni master parol bilan shifrlab saqlaydi.</summary>
        public static void Save(string privateKeyXml, string masterPassword)
        {
            if (string.IsNullOrWhiteSpace(privateKeyXml))
                throw new ArgumentException("Private key bo'sh.", nameof(privateKeyXml));
            if (string.IsNullOrEmpty(masterPassword))
                throw new ArgumentException("Master parol bo'sh.", nameof(masterPassword));

            byte[] salt = new byte[SALT_SIZE];
            byte[] iv = new byte[IV_SIZE];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] key = DeriveKey(masterPassword, salt);
            byte[] plain = Encoding.UTF8.GetBytes(MAGIC + privateKeyXml);

            using (var aes = new AesManaged { KeySize = 256, BlockSize = 128, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            using (var enc = aes.CreateEncryptor(key, iv))
            {
                byte[] cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(salt, 0, salt.Length);
                    fs.Write(iv, 0, iv.Length);
                    fs.Write(cipher, 0, cipher.Length);
                }
            }
        }

        /// <summary>
        /// Master parol bilan private key XML'ni ochadi.
        /// Noto'g'ri parol bo'lsa null qaytaradi.
        /// </summary>
        public static string Load(string masterPassword)
        {
            if (!Exists())
                return null;

            byte[] all = File.ReadAllBytes(FilePath);
            if (all.Length <= SALT_SIZE + IV_SIZE)
                return null;

            byte[] salt = new byte[SALT_SIZE];
            byte[] iv = new byte[IV_SIZE];
            Buffer.BlockCopy(all, 0, salt, 0, SALT_SIZE);
            Buffer.BlockCopy(all, SALT_SIZE, iv, 0, IV_SIZE);
            int cipherLen = all.Length - SALT_SIZE - IV_SIZE;
            byte[] cipher = new byte[cipherLen];
            Buffer.BlockCopy(all, SALT_SIZE + IV_SIZE, cipher, 0, cipherLen);

            byte[] key = DeriveKey(masterPassword, salt);

            try
            {
                using (var aes = new AesManaged { KeySize = 256, BlockSize = 128, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
                using (var dec = aes.CreateDecryptor(key, iv))
                {
                    byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                    string text = Encoding.UTF8.GetString(plain);
                    if (!text.StartsWith(MAGIC))
                        return null; // noto'g'ri parol
                    return text.Substring(MAGIC.Length);
                }
            }
            catch (CryptographicException)
            {
                return null; // noto'g'ri parol (padding xatosi)
            }
        }

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS))
            {
                return pbkdf2.GetBytes(KEY_SIZE);
            }
        }
    }
}
