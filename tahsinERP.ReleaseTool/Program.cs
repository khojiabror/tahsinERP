using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

// Foydalanish (Docs/Deploy/03_AUTO_UPDATE.md):
//   keygen   --out <papka> [--key-id r1]                         — ECDSA P-256 jufti (private PEM faqat GitHub secret'ga)
//   manifest --dir <artefaktlar> --version V [--commit C] [--prev-tag T] [--changed erp,panel,...]
//   sign     --dir <artefaktlar> --key-env RELEASE_SIGNING_KEY [--key-id r1]
//   verify   --dir <artefaktlar> [--pubkey <pem>]                — imzo + har ZIP'ning SHA-256

var opts = ParseArgs(args.Skip(1));
try
{
    switch (args.FirstOrDefault())
    {
        case "keygen": return KeyGen();
        case "manifest": return Manifest();
        case "sign": return Sign();
        case "verify": return Verify();
        default:
            Console.Error.WriteLine("Buyruq: keygen | manifest | sign | verify");
            return 2;
    }
}
catch (Exception ex) when (ex is ReleaseVerificationException or IOException or ArgumentException or CryptographicException)
{
    Console.Error.WriteLine("XATO: " + ex.Message);
    return 1;
}

int KeyGen()
{
    string dir = Required("out");
    string keyId = opts.GetValueOrDefault("key-id", "r1");
    Directory.CreateDirectory(dir);
    using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    string priv = Path.Combine(dir, $"release-{keyId}.private.pem");
    if (File.Exists(priv)) throw new IOException($"Fayl bor: {priv} (ustidan yozilmaydi).");
    File.WriteAllText(priv, ec.ExportPkcs8PrivateKeyPem());
    File.WriteAllText(Path.Combine(dir, $"release-{keyId}.public.pem"), ec.ExportSubjectPublicKeyInfoPem());
    Console.WriteLine($"Kalit yaratildi: {dir} (private faylni GitHub secret'ga yuklang va diskdan xavfsiz joyga oling).");
    return 0;
}

int Manifest()
{
    string dir = Required("dir");
    string version = Required("version");
    if (ReleaseVerifier.ParseVersion(version) is null) throw new ArgumentException($"Versiya noto'g'ri: {version}");
    var changed = opts.TryGetValue("changed", out var c)
        ? c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : null;
    var m = new ReleaseManifest
    {
        Version = version, Commit = opts.GetValueOrDefault("commit"), PreviousTag = opts.GetValueOrDefault("prev-tag"),
        BuiltAtUtc = DateTime.UtcNow,
    };
    foreach (var app in ReleaseApps.All)
    {
        string file = Path.Combine(dir, app + ".zip");
        if (!File.Exists(file)) continue;
        m.Artifacts.Add(new ReleaseArtifact
        {
            App = app, File = app + ".zip", Size = new FileInfo(file).Length, Sha256 = ReleaseVerifier.Sha256File(file),
            Changed = changed is null || changed.Contains(app),
        });
    }
    if (m.Artifacts.Count == 0) throw new IOException($"{dir} da *.zip artefakt yo'q.");
    File.WriteAllBytes(Path.Combine(dir, "manifest.json"), ReleaseVerifier.Serialize(m));
    foreach (var a in m.Artifacts) Console.WriteLine($"  {a.File,-12} {a.Size,12:N0}  {a.Sha256[..16]}…  {(a.Changed ? "o'zgargan" : "-")}");
    return 0;
}

int Sign()
{
    string dir = Required("dir");
    string pem = Environment.GetEnvironmentVariable(opts.GetValueOrDefault("key-env", "RELEASE_SIGNING_KEY")) ?? "";
    if (!pem.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new ArgumentException("Imzo kaliti env'da yo'q.");
    byte[] manifest = File.ReadAllBytes(Path.Combine(dir, "manifest.json"));
    var sig = ReleaseVerifier.Sign(manifest, opts.GetValueOrDefault("key-id", "r1"), pem);
    File.WriteAllText(Path.Combine(dir, "manifest.sig"), JsonSerializer.Serialize(sig, ReleaseVerifier.Json), new UTF8Encoding(false));
    // O'zini tekshirish: kodga joylangan public kalit bilan (kalit almashsa CI shu yerda to'xtaydi)
    ReleaseVerifier.Verify(manifest, File.ReadAllText(Path.Combine(dir, "manifest.sig")));
    Console.WriteLine("manifest.sig yozildi va ReleaseKeys bilan tekshirildi.");
    return 0;
}

int Verify()
{
    string dir = Required("dir");
    IReadOnlyDictionary<string, string>? trusted = null;
    if (opts.TryGetValue("pubkey", out var pub))
    {
        var s = JsonSerializer.Deserialize<ReleaseSignature>(File.ReadAllText(Path.Combine(dir, "manifest.sig")), ReleaseVerifier.Json)!;
        trusted = new Dictionary<string, string> { [s.KeyId] = File.ReadAllText(pub) };
    }
    var m = ReleaseVerifier.Verify(File.ReadAllBytes(Path.Combine(dir, "manifest.json")), File.ReadAllText(Path.Combine(dir, "manifest.sig")), trusted);
    foreach (var a in m.Artifacts) ReleaseVerifier.VerifyArtifact(a, Path.Combine(dir, a.File));
    Console.WriteLine($"OK: {m.Version}, {m.Artifacts.Count} artefakt.");
    return 0;
}

string Required(string key) => opts.TryGetValue(key, out var v) && v.Length > 0 ? v : throw new ArgumentException($"--{key} majburiy.");

static Dictionary<string, string> ParseArgs(IEnumerable<string> a)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string? key = null;
    foreach (var x in a)
    {
        if (x.StartsWith("--", StringComparison.Ordinal)) { key = x[2..]; d[key] = ""; }
        else if (key != null) { d[key] = x; key = null; }
    }
    return d;
}
