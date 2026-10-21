using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Verify_RoundTrip()
    {
        string h = PasswordHasher.Hash("Juda-maxfiy-123", iterations: 10_000);
        Assert.StartsWith("pbkdf2-sha256$10000$", h);
        Assert.True(PasswordHasher.Verify("Juda-maxfiy-123", h));
        Assert.False(PasswordHasher.Verify("juda-maxfiy-123", h));
        Assert.False(PasswordHasher.Verify("", h));
        Assert.False(PasswordHasher.Verify("x", "buzilgan$xesh"));
        Assert.True(PasswordHasher.NeedsRehash(h)); // 10k < standart
        Assert.NotEqual(h, PasswordHasher.Hash("Juda-maxfiy-123", iterations: 10_000)); // tasodifiy salt
    }

    [Theory]
    [InlineData("qisqa1", false)]
    [InlineData("faqatharflardan", false)]
    [InlineData("123456789012345", false)]
    [InlineData("YaxshiParol2026", true)]
    public void Policy(string password, bool ok) => Assert.Equal(ok, PasswordHasher.ValidatePolicy(password) is null);
}

public class TotpTests
{
    // RFC 6238 ilova B: SHA1 siri "12345678901234567890", T=59 → 94287082 (oxirgi 6 raqam).
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Fact]
    public void Rfc6238_Vectors()
    {
        Assert.Equal("287082", Totp.Compute(RfcSecret, 59 / 30));
        Assert.Equal("081804", Totp.Compute(RfcSecret, 1111111109 / 30));
        Assert.Equal("005924", Totp.Compute(RfcSecret, 1234567890 / 30));
    }

    [Fact]
    public void Verify_Window_And_NoReuse()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        long step = Totp.CurrentStep(now);
        string code = Totp.Compute(RfcSecret, step);
        Assert.True(Totp.Verify(RfcSecret, code, now, 0, out long matched));
        Assert.Equal(step, matched);
        Assert.False(Totp.Verify(RfcSecret, code, now, matched, out _)); // bir kod — bir marta
        Assert.True(Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step - 1), now, 0, out _)); // 30 s soat farqi
        Assert.False(Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step - 3), now, 0, out _));
        Assert.False(Totp.Verify(RfcSecret, "12345", now, 0, out _));
    }

    [Fact]
    public void Base32_RoundTrip_And_Uri()
    {
        byte[] data = RandomNumberGenerator.GetBytes(20);
        Assert.Equal(data, Base32.Decode(Base32.Encode(data)));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Encode(RfcSecret));
        Assert.Contains("secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.BuildUri("tahsinERP Panel", "a@b.uz", RfcSecret));
    }
}

public class SecretProtectorTests
{
    private static AesGcmSecretProtector Create() =>
        new(Options.Create(new SecurityOptions { MasterKey = AesGcmSecretProtector.GenerateMasterKey() }));

    [Fact]
    public void RoundTrip_PurposeBound()
    {
        var p = Create();
        string c = p.Protect("tenant-siri", SecretPurposes.TenantApiSecret);
        Assert.StartsWith("v1.", c);
        Assert.Equal("tenant-siri", p.Unprotect(c, SecretPurposes.TenantApiSecret));
        Assert.ThrowsAny<CryptographicException>(() => p.Unprotect(c, SecretPurposes.StaffTotp));
        Assert.ThrowsAny<CryptographicException>(() => Create().Unprotect(c, SecretPurposes.TenantApiSecret)); // boshqa master kalit
    }

    [Fact]
    public void MissingKey_FailsFast() =>
        Assert.Throws<InvalidOperationException>(() => new AesGcmSecretProtector(Options.Create(new SecurityOptions())));
}

public class MoneyTests
{
    [Theory]
    [InlineData("1 500 000", 150_000_000)]
    [InlineData("1500000,50", 150_000_050)]
    [InlineData("1500000.5", 150_000_050)]
    [InlineData("0", 0)]
    public void Parse(string text, long tiyin)
    {
        Assert.True(Money.TryParse(text, out long t));
        Assert.Equal(tiyin, t);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,005")]
    [InlineData("-5")]
    public void Parse_Rejects(string text) => Assert.False(Money.TryParse(text, out _));

    [Fact]
    public void Vat_Included_12()
    {
        Assert.Equal(1_200_000, Money.VatIncluded(11_200_000, 12m)); // 112 000 so'm → 12 000 so'm QQS
        Assert.Equal(0, Money.VatIncluded(11_200_000, 0m));
        Assert.Equal("1 234 567", Money.Format(123_456_700));
        Assert.Equal("1 234 567,89", Money.Format(123_456_789));
    }

    [Fact]
    public void InvoiceLines_Totals()
    {
        var inv = new Invoice();
        BillingService.SetLines(inv, [new("Paket", 3, 50_000_000, 12m), new("O'rnatish", 1, 11_200_000, 12m)]);
        Assert.Equal(161_200_000, inv.TotalTiyin);
        Assert.Equal(Money.VatIncluded(150_000_000, 12m) + 1_200_000, inv.VatTiyin);
        Assert.Throws<DomainException>(() => BillingService.SetLines(new Invoice(), [new("x", 0, 1, 12m)]));
        Assert.Equal("TE-2026-000123", BillingService.FormatNumber(2026, 123));
    }
}

public class TenantRulesTests
{
    [Theory]
    [InlineData("firma1", true)]
    [InlineData("my-firm", true)]
    [InlineData("admin", false)]   // ERP ReservedSlugs
    [InlineData("pay", false)]
    [InlineData("Firma", false)]
    [InlineData("-firma", false)]
    [InlineData("a.b", false)]
    public void Slug(string slug, bool ok) => Assert.Equal(ok, TenantService.ValidateSlug(slug) is null);

    [Fact]
    public void Inn_And_Db()
    {
        Assert.Null(TenantService.ValidateInn("123456789"));
        Assert.Null(TenantService.ValidateInn("12345678901234"));
        Assert.NotNull(TenantService.ValidateInn("12345"));
        Assert.Null(TenantService.ValidateDbName("DB_firma1"));
        Assert.NotNull(TenantService.ValidateDbName("DB firma"));
        Assert.Equal("DB_my_firm", TenantService.DefaultDbName("my-firm"));
    }
}

public class SigningTests
{
    [Fact]
    public void KeyFile_Encrypts_PrivateKey()
    {
        ErpLicense.GenerateKeyPair(out string pub, out string priv);
        var file = SigningKeyFile.Create("k2", priv, pub, "uzun-va-maxfiy-parol-2026", iterations: 10_000);
        string json = file.ToJson();
        Assert.DoesNotContain("<D>", json);
        string d = priv[(priv.IndexOf("<D>") + 3)..priv.IndexOf("</D>")];
        Assert.DoesNotContain(d[..40], json);
        var back = SigningKeyFile.FromJson(json);
        Assert.Equal(priv, back.DecryptPrivateXml("uzun-va-maxfiy-parol-2026"));
        Assert.Throws<CryptographicException>(() => back.DecryptPrivateXml("noto'g'ri-parol-xxxxxx"));
        Assert.Throws<ArgumentException>(() => SigningKeyFile.Create("k2", priv, pub, "qisqa"));
    }

    [Fact]
    public async Task LocalSigner_LoadsFromEncryptedFile()
    {
        ErpLicense.GenerateKeyPair(out string pub, out string priv);
        string path = Path.Combine(Path.GetTempPath(), "cp-test-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, SigningKeyFile.Create("k2", priv, pub, "uzun-va-maxfiy-parol-2026", 10_000).ToJson());
        try
        {
            using var signer = new LocalRsaSigner(Options.Create(new SigningOptions { KeyId = "k2", KeyFile = path, Passphrase = "uzun-va-maxfiy-parol-2026" }));
            Assert.Equal(pub, signer.PublicKeyXml);
            byte[] payload = ErpLicense.BuildV2Payload(1, 5, new DateTime(2026, 10, 18), new DateTime(2026, 10, 31), "TRIAL", Guid.NewGuid().ToString());
            string key = ErpLicense.AssembleKey(payload, await signer.SignAsync(payload));
            Assert.True(ErpLicense.TryParseKey(key, pub, out _));

            using var wrong = new LocalRsaSigner(Options.Create(new SigningOptions { KeyId = "k2", KeyFile = path, Passphrase = "boshqa-parol-xxxxxxxxxx" }));
            Assert.Throws<CryptographicException>(() => wrong.PublicKeyXml);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task KmsSigner_IsSkeleton_DoesNotCallAws()
    {
        var kms = new KmsSigner(Options.Create(new SigningOptions { KeyId = "k2", Provider = "Kms" }));
        await Assert.ThrowsAsync<NotSupportedException>(() => kms.SignAsync([1, 2, 3]));
    }

    /// <summary>
    /// Panel kaliti ERP'da qabul qilinishi: ERP LicenseService.ActivateCompanyLicense ishlatadigan aynan shu kod
    /// (tahsinERP.Licensing.TryParseKey + IsBoundTo + CompanyID + muddat). Panel public key'i ERP ishonadigan
    /// ro'yxatda bo'lsa — qabul; bo'lmasa (hozirgi k1-only build) — rad. Ya'ni prod'da K2PublicKeyXml to'ldirilishi SHART.
    /// </summary>
    [Fact]
    public async Task PanelKey_AcceptedByErpVerifier_OnlyWhenPublicKeyTrusted()
    {
        ErpLicense.GenerateKeyPair(out string pub, out string priv);
        var signer = new LocalRsaSigner("k2", priv);
        string inst = Guid.NewGuid().ToString("D").ToUpperInvariant();
        byte[] payload = ErpLicense.BuildV2Payload(7, 20, new DateTime(2026, 10, 18), new DateTime(2026, 11, 20), "BIZNES", inst);
        string key = ErpLicense.AssembleKey(payload, await signer.SignAsync(payload));

        var erpTrusted = ErpLicense.TrustedPublicKeys.Concat([new ErpLicense.TrustedPublicKey("k2", pub)]).ToList();
        Assert.True(ErpLicense.TryParseKey(key, erpTrusted, out var info));
        Assert.Equal("k2", info.KeyId);
        Assert.Equal(2, info.Version);
        Assert.Equal(7, info.CompanyID);
        Assert.Equal(20, info.MaxUsers);
        Assert.Equal("BIZNES", info.Edition);
        Assert.True(info.IsBoundTo(inst));
        Assert.True(info.IsBoundTo(inst.ToLowerInvariant()));
        Assert.False(info.IsBoundTo(Guid.NewGuid().ToString()));
        Assert.Equal(ErpLicense.LicenseState.Valid, ErpLicense.GetState(info, new DateTime(2026, 10, 18)));
        Assert.Equal(ErpLicense.LicenseState.Grace, ErpLicense.GetState(info, new DateTime(2026, 11, 22)));

        // ERP build'i ishonmaydigan (k1/prod k2 emas) kalit bilan imzolangan panel kaliti rad etiladi.
        Assert.False(ErpLicense.TryParseKey(key, ErpLicense.TrustedPublicKeys, out var rejected));
        Assert.False(rejected.SignatureValid);

        // Bitta bayt o'zgarsa — imzo buziladi.
        byte[] p2 = ErpLicense.BuildV2Payload(7, 50, new DateTime(2026, 10, 18), new DateTime(2027, 11, 20), "KORP", inst);
        string forged = ErpLicense.AssembleKey(p2, Convert.FromBase64String(key[(key.LastIndexOf('.') + 1)..]));
        Assert.False(ErpLicense.TryParseKey(forged, erpTrusted, out _));
    }
}

public class InvoicePdfTests
{
    [Fact]
    public void Render_ProducesPdf_WithCyrillicAndLatin()
    {
        var clock = new Clock(TimeProvider.System, Options.Create(new PanelOptions()));
        var vendor = Options.Create(new VendorOptions { LegalName = "«TAHSIN SOFT» MChJ", Inn = "301234567", BankAccount = "20208000900123456001", Mfo = "00440" });
        var inv = new Invoice { Number = "TE-2026-000001", IssueDate = new DateOnly(2026, 10, 18), DueDate = new DateOnly(2026, 10, 23), PaidTiyin = 50_000_000 };
        BillingService.SetLines(inv, [new("tahsinERP «Biznes» paketi, 20 foydalanuvchi — 1 oy", 1, 112_000_000, 12m)]);
        var tenant = new Tenant { LegalName = "ООО «Ўзбек Текстиль» / O'zbek To'qimachilik", Inn = "123456789" };
        byte[] pdf = new InvoicePdfRenderer(vendor, clock).Render(inv, tenant);
        Assert.True(pdf.Length > 2000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("CP_PDF_OUT") is string outPath) File.WriteAllBytes(outPath, pdf); // ko'z bilan tekshirish uchun
    }
}
