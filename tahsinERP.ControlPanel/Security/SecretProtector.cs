using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace tahsinERP.ControlPanel.Security;

public class SecurityOptions
{
    /// <summary>
    /// Bazadagi sirlarni (tenant_secret, TOTP siri) shifrlash uchun 32 baytlik master kalit, Base64.
    /// Git'ga YOZILMAYDI: env <c>ControlPanel__Security__MasterKey</c> yoki user-secrets. Yo'qolsa — tenantlar qayta
    /// ro'yxatdan o'tadi va xodimlar 2FA'ni qayta ulaydi; shuning uchun zaxira nusxasi baza zaxirasidan ALOHIDA saqlansin.
    /// </summary>
    public string? MasterKey { get; set; }

    /// <summary>
    /// Xodimlar kirishida TOTP (2FA) majburiymi. Standart — ha. false (env <c>ControlPanel__Security__RequireStaffTotp=false</c>)
    /// bo'lsa xodim faqat email + parol bilan kiradi; ulangan 2FA sirlari bazada qoladi va qayta yoqilganda ishlaydi.
    /// Egasi qarori bilan o'chirilsa — panel kuchli parol va (ixtiyoriy) IP cheklovi bilan himoyalansin.
    /// </summary>
    public bool RequireStaffTotp { get; set; } = true;

    /// <summary>
    /// Xodim seansi shuncha daqiqa harakatsizlikdan keyin yopiladi (cookie har so'rovda yangilanadi; brauzerda
    /// 1 daqiqa oldin ogohlantirish va avtomatik chiqish). Standart — 15, oraliq 5–480.
    /// </summary>
    public int StaffIdleMinutes { get; set; } = 15;

    public int EffectiveStaffIdleMinutes => Math.Clamp(StaffIdleMinutes, 5, 480);
}

/// <summary>AES-256-GCM: "v1." + Base64(nonce[12] | tag[16] | shifrmatn). AAD — maqsad nomi (bir maqsad uchun shifrlangan sirni boshqasiga qo'yib bo'lmaydi).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext, string purpose);
    string Unprotect(string protectedText, string purpose);
}

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Version = "v1.";
    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<SecurityOptions> options)
    {
        string? b64 = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException(
                "ControlPanel:Security:MasterKey sozlanmagan. Yaratish: `dotnet run -- gen-master-key`, keyin env yoki user-secrets'ga yozing (README).");
        try { _key = Convert.FromBase64String(b64.Trim()); }
        catch (FormatException) { throw new InvalidOperationException("ControlPanel:Security:MasterKey Base64 emas."); }
        if (_key.Length != 32)
            throw new InvalidOperationException("ControlPanel:Security:MasterKey 32 bayt bo'lishi kerak.");
    }

    public string Protect(string plaintext, string purpose)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] cipher = new byte[plain.Length];
        using (var aes = new AesGcm(_key, 16))
            aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(purpose));
        return Version + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedText, string purpose)
    {
        if (!protectedText.StartsWith(Version, StringComparison.Ordinal))
            throw new CryptographicException("Noma'lum shifr versiyasi.");
        byte[] all = Convert.FromBase64String(protectedText[Version.Length..]);
        if (all.Length < 28)
            throw new CryptographicException("Shifrlangan qiymat buzilgan.");
        byte[] plain = new byte[all.Length - 28];
        using (var aes = new AesGcm(_key, 16))
            aes.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain, Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(plain);
    }

    public static string GenerateMasterKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

public static class SecretPurposes
{
    public const string TenantApiSecret = "tenant-api-secret";
    public const string StaffTotp = "staff-totp";
    public const string AgentSecret = "agent-secret";
    public const string JobSecrets = "provisioning-job-secrets";
}
