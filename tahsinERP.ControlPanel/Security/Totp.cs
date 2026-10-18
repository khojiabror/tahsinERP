using System.Security.Cryptography;
using System.Text;

namespace tahsinERP.ControlPanel.Security;

/// <summary>
/// TOTP (RFC 6238): HMAC-SHA1, 6 raqam, 30 soniyalik qadam — Google Authenticator, Microsoft Authenticator,
/// Aegis va boshqalar bilan mos. Bitta qadam oldin/keyin qabul qilinadi (soat farqi uchun).
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static long CurrentStep(DateTimeOffset now) => now.ToUnixTimeSeconds() / StepSeconds;

    public static string Compute(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (int i = 7; i >= 0; i--) { counter[i] = (byte)(step & 0xFF); step >>= 8; }
        byte[] hash = HMACSHA1.HashData(secret, counter);
        int offset = hash[^1] & 0x0F;
        int code = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (code % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Kodni tekshiradi. Muvaffaqiyatli bo'lsa mos kelgan qadamni qaytaradi; u <paramref name="lastUsedStep"/> dan
    /// katta bo'lishi shart (bitta kod ikki marta ishlatilmaydi).
    /// </summary>
    public static bool Verify(byte[] secret, string? code, DateTimeOffset now, long lastUsedStep, out long matchedStep)
    {
        matchedStep = 0;
        code = code?.Replace(" ", "").Trim();
        if (code is null || code.Length != Digits || !code.All(char.IsAsciiDigit))
            return false;
        long current = CurrentStep(now);
        for (long s = current - 1; s <= current + 1; s++)
        {
            if (s <= lastUsedStep)
                continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(secret, s)), Encoding.ASCII.GetBytes(code)))
            {
                matchedStep = s;
                return true;
            }
        }
        return false;
    }

    /// <summary>otpauth:// URI (QR kod uchun).</summary>
    public static string BuildUri(string issuer, string account, byte[] secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decode(string text)
    {
        text = text.Replace(" ", "").TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (char c in text)
        {
            int v = Alphabet.IndexOf(c);
            if (v < 0) throw new FormatException("Base32 emas: " + c);
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
