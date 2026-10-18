using System.Text;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel;

/// <summary>
/// Xizmat buyruqlari: <c>dotnet run -- &lt;buyruq&gt;</c> (yoki <c>tahsinERP.ControlPanel.exe &lt;buyruq&gt;</c>).
/// Parol va kalit paroli KODGA/argumentga yozilmaydi: env yoki yashirin so'rov.
/// </summary>
public static class Cli
{
    private static readonly string[] Commands = ["migrate", "create-owner", "gen-master-key", "keygen", "print-public-key", "help"];

    public static bool IsCommand(string[] args) => args.Length > 0 && Commands.Contains(args[0]);

    /// <summary>Konfiguratsiya/DB kerak bo'lmagan buyruqlar — host qurilmasdan bajariladi.</summary>
    public static int? RunStandalone(string[] args)
    {
        switch (args[0])
        {
            case "help":
                Console.WriteLine("""
                    Buyruqlar:
                      migrate                                  tahsin_control bazasini yaratadi/yangilaydi (EF migratsiyalar)
                      create-owner --email E [--name N]        birinchi Owner (parol: env CP_OWNER_PASSWORD yoki so'raladi)
                      gen-master-key                           ControlPanel:Security:MasterKey uchun yangi 32 bayt kalit
                      keygen --out FAYL [--key-id k2]          imzolash RSA 2048 jufti: shifrlangan fayl + public XML
                                                               (parol: env ControlPanel__Signing__Passphrase yoki so'raladi)
                      print-public-key                         sozlangan imzolovchining public key XML'i (ERP'ga qo'shish uchun)
                    """);
                return 0;
            case "gen-master-key":
                Console.WriteLine(AesGcmSecretProtector.GenerateMasterKey());
                return 0;
            case "keygen":
                return KeyGen(args);
            default:
                return null;
        }
    }

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        try
        {
            switch (args[0])
            {
                case "migrate":
                    await sp.GetRequiredService<ControlDbContext>().Database.MigrateAsync();
                    Console.WriteLine("Baza yangilandi.");
                    return 0;
                case "create-owner":
                    return await CreateOwnerAsync(args, sp);
                case "print-public-key":
                    var signer = sp.GetRequiredService<ILicenseSigner>();
                    Console.WriteLine($"KeyId: {signer.KeyId}");
                    Console.WriteLine(signer.PublicKeyXml);
                    return 0;
            }
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine("Xato: " + ex.Message);
            return 1;
        }
        return 2;
    }

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static async Task<int> CreateOwnerAsync(string[] args, IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        string? email = Arg(args, "--email");
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("--email majburiy.");
            return 1;
        }
        if (await db.StaffUsers.AnyAsync(u => u.Role == StaffRole.Owner && u.IsActive))
        {
            Console.Error.WriteLine("Faol Owner allaqachon bor. Qo'shimcha xodimlarni panelning Xodimlar sahifasidan qo'shing.");
            return 1;
        }
        string? password = Environment.GetEnvironmentVariable("CP_OWNER_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            password = ReadSecret("Parol: ");
            if (ReadSecret("Parol (takror): ") != password)
            {
                Console.Error.WriteLine("Parollar mos emas.");
                return 1;
            }
        }
        var user = await sp.GetRequiredService<StaffAuthService>()
            .CreateStaffAsync(email, Arg(args, "--name") ?? email, StaffRole.Owner, password, createdBy: null);
        Console.WriteLine($"Owner yaratildi: {user.Email}. Birinchi kirishda TOTP (2FA) ulanadi.");
        return 0;
    }

    private static int KeyGen(string[] args)
    {
        string keyId = Arg(args, "--key-id") ?? "k2";
        string? output = Arg(args, "--out");
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("--out FAYL majburiy (git'dan tashqarida, masalan %ProgramData%\\tahsinERP\\panel-signing-k2.json).");
            return 1;
        }
        if (File.Exists(output))
        {
            Console.Error.WriteLine("Fayl allaqachon bor — ustiga yozilmaydi: " + output);
            return 1;
        }
        if (keyId == "k1")
        {
            Console.Error.WriteLine("k1 — WinForms generatorning mavjud jufti; panel uchun yangi nom tanlang (k2, k3...).");
            return 1;
        }
        string? pass = Environment.GetEnvironmentVariable("ControlPanel__Signing__Passphrase");
        if (string.IsNullOrEmpty(pass))
        {
            pass = ReadSecret("Kalit fayli paroli (kamida 16 belgi): ");
            if (ReadSecret("Takror: ") != pass)
            {
                Console.Error.WriteLine("Parollar mos emas.");
                return 1;
            }
        }
        ErpLicense.GenerateKeyPair(out string publicXml, out string privateXml);
        var file = SigningKeyFile.Create(keyId, privateXml, publicXml, pass);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, file.ToJson(), new UTF8Encoding(false));
        Console.WriteLine($"Shifrlangan kalit fayli: {Path.GetFullPath(output)}  (KeyId={keyId})");
        Console.WriteLine("ERP'ga qo'shish: tahsinERP.Licensing/LicenseHelper.cs → K2PublicKeyXml = quyidagi qiymat, ERP'ni qayta yig'ing:");
        Console.WriteLine(publicXml);
        return 0;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? "";
        var sb = new StringBuilder();
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter) break;
            if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
            if (!char.IsControl(k.KeyChar)) sb.Append(k.KeyChar);
        }
        Console.WriteLine();
        return sb.ToString();
    }
}
