using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.Bot.Security;

/// <summary>
/// Bot tokenlari bazada AES-256-GCM bilan shifrlanadi; kalit = HMAC-SHA256(master, "bot-secrets").
/// Format: base64(nonce[12] | tag[16] | ciphertext).
/// </summary>
public sealed class SecretBox
{
    private readonly byte[] _key;

    public SecretBox(string masterKey)
    {
        if (string.IsNullOrEmpty(masterKey) || masterKey.Length < 32)
            throw new InvalidOperationException("Bot:MasterKey kamida 32 belgi bo'lishi kerak.");
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(masterKey), "bot-secrets"u8);
    }

    public string Protect(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using (var aes = new AesGcm(_key, 16)) aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedText)
    {
        var all = Convert.FromBase64String(protectedText);
        if (all.Length < 28) throw new CryptographicException("Shifrlangan qiymat noto'g'ri.");
        var plain = new byte[all.Length - 28];
        using (var aes = new AesGcm(_key, 16)) aes.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
