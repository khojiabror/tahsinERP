using System.Globalization;
using System.Security.Cryptography;

namespace tahsinERP.ControlPanel.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 parol xeshi. Format: "pbkdf2-sha256$&lt;iteratsiya&gt;$&lt;salt b64&gt;$&lt;xesh b64&gt;".
/// Iteratsiya soni formatda saqlanadi — keyin oshirilsa eski xeshlar ishlayveradi (NeedsRehash).
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    public const int DefaultIterations = 600_000; // OWASP 2023+ tavsiyasi (SHA-256)
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    public const int MinPasswordLength = 12;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Prefix, iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            return false;
        string[] parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) || iterations < 10_000)
            return false;
        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool NeedsRehash(string stored)
    {
        string[] parts = stored.Split('$');
        return parts.Length != 4 || !int.TryParse(parts[1], out int it) || it < DefaultIterations;
    }

    /// <summary>Parol siyosati: kamida 12 belgi, harf va raqam. Xato bo'lsa — sababi, aks holda null.</summary>
    public static string? ValidatePolicy(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return $"Parol kamida {MinPasswordLength} belgidan iborat bo'lsin.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Parolda harf ham, raqam ham bo'lsin.";
        return null;
    }
}
