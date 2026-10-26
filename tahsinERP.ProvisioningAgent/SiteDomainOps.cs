using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>Tashqi buyruq (appcmd, wacs, netsh) — testda soxtasi bilan almashtiriladi.</summary>
public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string exe, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct);
}

public sealed record CommandResult(int ExitCode, string Output);

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(string exe, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        var output = new StringBuilder();
        object gate = new();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (gate) output.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (gate) output.AppendLine(e.Data); };
        try { p.Start(); }
        catch (System.ComponentModel.Win32Exception ex) { throw new InvalidJobException($"{exe} ishga tushmadi: {ex.Message}"); }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TransientJobException($"{Path.GetFileName(exe)} vaqt chegarasidan oshdi ({timeout.TotalMinutes:0} daq).");
        }
        p.WaitForExit();
        lock (gate) return new CommandResult(p.ExitCode, output.ToString());
    }
}

/// <summary>Domen sertifikatlarini ombordan o'chirish (win-acme yangilanishni bekor qilganda sertifikatni o'zi o'chirmaydi).</summary>
public interface ICertificateCleaner
{
    /// <summary>FriendlyName aniq <paramref name="friendlyName"/> yoki "&lt;friendlyName&gt; @ sana" bo'lgan sertifikatlar. Qaytaradi: o'chirilganlar soni.</summary>
    int Remove(string storeName, string friendlyName);
}

public sealed class X509CertificateCleaner : ICertificateCleaner
{
    public int Remove(string storeName, string friendlyName)
    {
        using var store = new X509Store(storeName, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        int n = 0;
        foreach (var c in store.Certificates)
        {
            if (SiteDomainOps.IsOurCertificate(c.FriendlyName, friendlyName))
            {
                store.Remove(c);
                n++;
            }
            c.Dispose();
        }
        return n;
    }
}

/// <summary>
/// Sayt-katalog o'z domeni (Docs/ControlPanel/09_SITE_CUSTOM_DOMAIN.md). Domen Cloudflare'da emas — sertifikat HTTP-01 bilan:
/// win-acme <c>selfhosting</c> validatsiyasi http.sys orqali <c>/.well-known/acme-challenge/</c> ni o'zi beradi (IIS bilan birga ishlaydi),
/// <c>--installation iis</c> sayt ilovasiga https SNI binding qo'yadi, yangilanish win-acme'ning mavjud rejalashtirilgan vazifasida.
/// http binding — sayt ilovasi o'z domenini https'ga yo'naltiradi. Har qadam idempotent (qayta urinishda bor narsa tekshiriladi).
/// </summary>
public sealed class SiteDomainOps(AgentOptions options, ICommandRunner runner, ICertificateCleaner certificates)
{
    private AgentOptions.SiteDomainsSection O => options.SiteDomains;
    private TimeSpan Timeout => TimeSpan.FromMinutes(Math.Max(1, O.TimeoutMinutes));

    /// <summary>win-acme yangilanishi nomi — sertifikat FriendlyName ham shundan boshlanadi.</summary>
    public static string FriendlyName(string host) => "tahsin-site-" + host;

    public static bool IsOurCertificate(string? certFriendlyName, string friendlyName) =>
        certFriendlyName != null && (certFriendlyName == friendlyName || certFriendlyName.StartsWith(friendlyName + " @", StringComparison.Ordinal));

    private static string Binding(string protocol, string host) =>
        $"[protocol='{protocol}',bindingInformation='*:{(protocol == "https" ? 443 : 80)}:{host}']";

    private static void Check(string host)
    {
        if (!SiteDomainRules.IsValid(host)) throw new InvalidJobException($"Domen formati noto'g'ri: '{host}'.");
    }

    /// <summary>Sayt bindinglari: "https/*:443:*.sayt.x.uz,http/*:80:shop.firma.uz" → (protokol, host) ro'yxati.</summary>
    public async Task<List<(string Protocol, string Host)>> BindingsAsync(CancellationToken ct)
    {
        var r = await runner.RunAsync(O.AppCmd, ["list", "site", O.IisSite, "/text:bindings"], Timeout, ct);
        if (r.ExitCode != 0 || string.IsNullOrWhiteSpace(r.Output))
            throw new InvalidJobException($"IIS sayti topilmadi: '{O.IisSite}' (appcmd {r.ExitCode}: {Tail(r.Output)})");
        return ParseBindings(r.Output);
    }

    public static List<(string Protocol, string Host)> ParseBindings(string text) =>
        text.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(b => b.Split('/', 2))
            .Where(p => p.Length == 2)
            .Select(p => (p[0].ToLowerInvariant(), p[1].Split(':').Length >= 3 ? p[1].Split(':', 3)[2].ToLowerInvariant() : ""))
            .ToList();

    public async Task BindAsync(string host, Action<string> log, CancellationToken ct)
    {
        Check(host);
        if (!File.Exists(O.WacsExe)) throw new InvalidJobException($"win-acme topilmadi: {O.WacsExe} (SiteDomains:WacsExe)");
        var bindings = await BindingsAsync(ct);
        if (!bindings.Contains(("http", host)))
        {
            await AppCmdAsync(["set", "site", O.IisSite, "/+bindings." + Binding("http", host)], ct);
            log($"IIS: http binding qo'shildi ({host}:80 → {O.IisSite})");
        }
        else log($"IIS: http binding bor ({host}:80)");

        var idRun = await runner.RunAsync(O.AppCmd, ["list", "site", O.IisSite, "/text:id"], Timeout, ct);
        string siteId = idRun.Output.Trim();
        if (idRun.ExitCode != 0 || !int.TryParse(siteId, out _)) throw new InvalidJobException($"IIS sayt ID olinmadi: {Tail(idRun.Output)}");

        var args = new List<string>
        {
            "--source", "manual", "--host", host, "--friendlyname", FriendlyName(host),
            "--validation", "selfhosting",
            "--store", "certificatestore", "--certificatestore", O.CertificateStore,
            "--installation", "iis", "--installationsiteid", siteId,
            "--accepttos",
        };
        if (!string.IsNullOrWhiteSpace(O.Email)) { args.Add("--emailaddress"); args.Add(O.Email.Trim()); }
        log($"win-acme: {host} uchun sertifikat (HTTP-01 selfhosting) → {O.CertificateStore}, IIS sayt #{siteId}");
        var w = await runner.RunAsync(O.WacsExe, args, Timeout, ct);
        if (w.ExitCode != 0)
            throw new InvalidJobException($"win-acme xatosi (kod {w.ExitCode}) — DNS server IP'ga qaraganini va 80-port ochiqligini tekshiring: {Tail(w.Output)}");

        if (!(await BindingsAsync(ct)).Contains(("https", host)))
            throw new InvalidJobException($"win-acme tugadi, lekin {host}:443 binding paydo bo'lmadi: {Tail(w.Output)}");
        log($"IIS: https binding va sertifikat tayyor (https://{host})");
    }

    public async Task UnbindAsync(string host, Action<string> log, CancellationToken ct)
    {
        Check(host);
        if (File.Exists(O.WacsExe))
        {
            var w = await runner.RunAsync(O.WacsExe, ["--cancel", "--friendlyname", FriendlyName(host)], Timeout, ct);
            log(w.ExitCode == 0 ? $"win-acme: yangilanish bekor qilindi ({FriendlyName(host)})" : $"win-acme: yangilanish bekor qilinmadi (kod {w.ExitCode}) — davom etiladi");
        }
        else log($"win-acme topilmadi ({O.WacsExe}) — yangilanish bekor qilinmadi");

        var bindings = await BindingsAsync(ct);
        foreach (var protocol in new[] { "https", "http" })
        {
            if (!bindings.Contains((protocol, host))) continue;
            await AppCmdAsync(["set", "site", O.IisSite, "/-bindings." + Binding(protocol, host)], ct);
            log($"IIS: {protocol} binding olib tashlandi ({host})");
        }
        // IIS bindingi appcmd bilan o'chirilganda http.sys'dagi SNI sertifikat bog'lanishi qoladi
        var ns = await runner.RunAsync("netsh", ["http", "delete", "sslcert", $"hostnameport={host}:443"], Timeout, ct);
        if (ns.ExitCode == 0) log($"http.sys: sslcert {host}:443 o'chirildi");
        int removed = certificates.Remove(O.CertificateStore, FriendlyName(host));
        log($"sertifikat ombori {O.CertificateStore}: {removed} ta o'chirildi");
    }

    private async Task AppCmdAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var r = await runner.RunAsync(O.AppCmd, args, Timeout, ct);
        if (r.ExitCode != 0) throw new InvalidJobException($"appcmd {string.Join(' ', args)}: kod {r.ExitCode}: {Tail(r.Output)}");
    }

    private static string Tail(string text, int lines = 12)
    {
        var all = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" | ", all.Skip(Math.Max(0, all.Length - lines))).Trim();
    }
}
