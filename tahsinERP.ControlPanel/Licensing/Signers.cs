using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Licensing;

/// <summary>
/// Litsenziya payload'ini imzolovchi. Imzo: RSASSA-PKCS1-v1_5 + SHA-256 (ERP LicenseHelper.Verify bilan bir xil).
/// Private key panel kodiga ochiq holda kirmaydi: Local — shifrlangan fayl, Kms — AWS KMS (kalit KMS'dan chiqmaydi).
/// </summary>
public interface ILicenseSigner
{
    /// <summary>ERP LicenseHelper.TrustedPublicKeys dagi nom (k2, ...). Licenses.KeyId ga yoziladi.</summary>
    string KeyId { get; }
    /// <summary>Tekshirish uchun public key (RSAKeyValue XML) — chiqarilgan kalit darhol shu bilan tekshiriladi.</summary>
    string PublicKeyXml { get; }
    Task<byte[]> SignAsync(byte[] payload, CancellationToken ct = default);
}

public class SigningOptions
{
    /// <summary>Local yoki Kms.</summary>
    public string Provider { get; set; } = "Local";
    public string KeyId { get; set; } = "k2";
    /// <summary>Local: <c>keygen</c> buyrug'i yaratgan shifrlangan fayl yo'li (git'dan tashqarida, masalan %ProgramData%\tahsinERP\panel-signing-k2.json).</summary>
    public string? KeyFile { get; set; }
    /// <summary>Local: kalit faylining paroli. Faqat env/user-secrets: <c>ControlPanel__Signing__Passphrase</c>.</summary>
    public string? Passphrase { get; set; }
    /// <summary>Kms: kalit ARN'i yoki alias (skelet).</summary>
    public string? KmsKeyId { get; set; }
    public string? KmsRegion { get; set; }
    /// <summary>Kms: KMS GetPublicKey natijasidan olingan RSAKeyValue XML (ERP K2PublicKeyXml bilan bir xil).</summary>
    public string? KmsPublicKeyXml { get; set; }
}

/// <summary>
/// Shifrlangan imzolash kaliti fayli: private key XML AES-256-GCM bilan, kalit — paroldan PBKDF2-SHA256 (600k).
/// Public qism ochiq turadi (ERP'ga qo'shish uchun kerak, sir emas).
/// </summary>
public sealed class SigningKeyFile
{
    public int V { get; set; } = 1;
    public string KeyId { get; set; } = "";
    public string PublicKeyXml { get; set; } = "";
    public int Iterations { get; set; }
    public string Salt { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Cipher { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static SigningKeyFile Create(string keyId, string privateXml, string publicXml, string passphrase, int iterations = 600_000)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 16)
            throw new ArgumentException("Kalit fayli paroli kamida 16 belgi bo'lsin.", nameof(passphrase));
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, 32);
        byte[] plain = Encoding.UTF8.GetBytes(privateXml);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes("tahsin-signing|" + keyId));
        CryptographicOperations.ZeroMemory(plain);
        CryptographicOperations.ZeroMemory(key);
        return new SigningKeyFile
        {
            KeyId = keyId, PublicKeyXml = publicXml, Iterations = iterations,
            Salt = Convert.ToBase64String(salt), Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag), Cipher = Convert.ToBase64String(cipher), CreatedUtc = DateTime.UtcNow
        };
    }

    public string DecryptPrivateXml(string passphrase)
    {
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(passphrase, Convert.FromBase64String(Salt), Iterations, HashAlgorithmName.SHA256, 32);
        byte[] cipher = Convert.FromBase64String(Cipher);
        byte[] plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(Nonce), cipher, Convert.FromBase64String(Tag), plain, Encoding.UTF8.GetBytes("tahsin-signing|" + KeyId));
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new CryptographicException("Imzolash kaliti paroli noto'g'ri yoki fayl buzilgan.");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        return Encoding.UTF8.GetString(plain);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SigningKeyFile FromJson(string json) =>
        JsonSerializer.Deserialize<SigningKeyFile>(json) ?? throw new CryptographicException("Kalit fayli bo'sh.");
}

/// <summary>Lokal RSA imzolovchi (1-bosqich). Kalit fayldan birinchi so'rovda ochiladi va xotirada RSA obyekti sifatida turadi.</summary>
public sealed class LocalRsaSigner : ILicenseSigner, IDisposable
{
    private readonly Lazy<(RSA Rsa, string PublicXml)> _key;
    public string KeyId { get; }

    public LocalRsaSigner(IOptions<SigningOptions> options)
    {
        var o = options.Value;
        KeyId = o.KeyId;
        _key = new Lazy<(RSA, string)>(() => Load(o), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Testlar uchun: tayyor private key XML bilan.</summary>
    public LocalRsaSigner(string keyId, string privateXml)
    {
        KeyId = keyId;
        _key = new Lazy<(RSA, string)>(() => FromPrivateXml(privateXml));
    }

    public string PublicKeyXml => _key.Value.PublicXml;

    public Task<byte[]> SignAsync(byte[] payload, CancellationToken ct = default) =>
        Task.FromResult(_key.Value.Rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    private static (RSA, string) Load(SigningOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.KeyFile))
            throw new InvalidOperationException("Imzolash kaliti sozlanmagan: ControlPanel:Signing:KeyFile (README, `keygen` buyrug'i).");
        if (!File.Exists(o.KeyFile))
            throw new InvalidOperationException("Imzolash kaliti fayli topilmadi: " + o.KeyFile);
        if (string.IsNullOrEmpty(o.Passphrase))
            throw new InvalidOperationException("ControlPanel:Signing:Passphrase berilmagan (env yoki user-secrets).");
        var file = SigningKeyFile.FromJson(File.ReadAllText(o.KeyFile));
        if (!string.Equals(file.KeyId, o.KeyId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Kalit fayli KeyId='{file.KeyId}', sozlamada '{o.KeyId}'.");
        var result = FromPrivateXml(file.DecryptPrivateXml(o.Passphrase));
        if (result.Item2 != file.PublicKeyXml)
            throw new CryptographicException("Kalit faylidagi public key private key'ga mos emas.");
        return result;
    }

    private static (RSA, string) FromPrivateXml(string privateXml)
    {
        RSAParameters p = ErpLicense.FromXml(privateXml, includePrivate: true);
        var rsa = RSA.Create();
        rsa.ImportParameters(p);
        if (rsa.KeySize < 2048)
            throw new CryptographicException("Imzolash kaliti kamida 2048 bit bo'lsin.");
        return (rsa, ErpLicense.ToXml(p, includePrivate: false));
    }

    public void Dispose()
    {
        if (_key.IsValueCreated)
            _key.Value.Rsa.Dispose();
    }
}

/// <summary>
/// AWS KMS imzolovchi — FAQAT SKELET, AWS'ga murojaat qilmaydi. Keyingi bosqichda:
///   AWSSDK.KeyManagementService paketi; <c>SignAsync(new SignRequest { KeyId = KmsKeyId, Message = payload,
///   MessageType = MessageType.RAW, SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256 })</c> → Signature.
///   KMS RAW xabarni o'zi SHA-256 qiladi — natija .NET SignData(SHA256, Pkcs1) bilan bir xil (00_PLAN.md 3.1, 12).
///   Kirish: EC2/Lightsail roli yoki SSM orqali, kalit kodda/konfiguratsiyada saqlanmaydi.
/// </summary>
public sealed class KmsSigner(IOptions<SigningOptions> options) : ILicenseSigner
{
    public string KeyId { get; } = options.Value.KeyId;

    public string PublicKeyXml => options.Value.KmsPublicKeyXml
        ?? throw new InvalidOperationException("ControlPanel:Signing:KmsPublicKeyXml sozlanmagan.");

    public Task<byte[]> SignAsync(byte[] payload, CancellationToken ct = default) =>
        throw new NotSupportedException("KmsSigner hali ulanmagan (1-bosqichda faqat skelet). ControlPanel:Signing:Provider=Local ishlating.");
}
