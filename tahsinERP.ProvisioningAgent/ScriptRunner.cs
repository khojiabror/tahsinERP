using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Database/tools/*.ps1 ni chaqiradi (03_DB_PROVISIONING.md). Maxfiy qiymatlar buyruq satriga emas, env'ga
/// (TAHSINERP_ADMIN_PASSWORD, TAHSINERP_SQL_PASSWORD) beriladi va log'dan olib tashlanadi.
/// Natija — stdout'ning oxirgi "[RESULT] {json}" qatori.
/// </summary>
public sealed class ScriptRunner(AgentOptions options)
{
    public sealed record ScriptResult(int ExitCode, JsonObject? Result, string Log);

    public string ToolPath(string script) => Path.Combine(options.Paths.DatabaseRoot, "tools", script);

    public async Task<ScriptResult> RunAsync(string script, IReadOnlyList<string> args, IDictionary<string, string?> secretEnv, CancellationToken ct)
    {
        string path = ToolPath(script);
        if (!File.Exists(path)) throw new InvalidJobException($"Skript topilmadi: {path} (Paths:DatabaseRoot)");
        var psi = new ProcessStartInfo(options.PowerShell.Exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path }) psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        var secrets = new List<string>();
        foreach (var (k, v) in secretEnv)
        {
            psi.Environment[k] = v;
            if (!string.IsNullOrEmpty(v)) secrets.Add(v);
        }
        if (!string.IsNullOrEmpty(options.Sql.User))
        {
            psi.Environment["TAHSINERP_SQL_PASSWORD"] = options.Sql.Password;
            if (!string.IsNullOrEmpty(options.Sql.Password)) secrets.Add(options.Sql.Password);
        }

        var log = new StringBuilder();
        string? lastResult = null;
        object gate = new();
        void OnLine(string? line)
        {
            if (line is null) return;
            line = Redact(line, secrets);
            lock (gate)
            {
                int idx = line.IndexOf("[RESULT] ", StringComparison.Ordinal);
                if (idx >= 0) lastResult = line[(idx + 9)..];
                log.AppendLine(line);
            }
        }

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => OnLine(e.Data);
        p.ErrorDataReceived += (_, e) => OnLine(e.Data);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, options.PowerShell.TimeoutMinutes)));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TransientJobException($"{script} vaqt chegarasidan oshdi ({options.PowerShell.TimeoutMinutes} daq).");
        }
        p.WaitForExit(); // asinxron o'qishlar tugashi uchun

        JsonObject? result = null;
        if (lastResult != null)
        {
            try { result = JsonNode.Parse(lastResult) as JsonObject; } catch (System.Text.Json.JsonException) { }
        }
        // tempPassword maxfiy (03, 4-bo'lim) — natijani saqlashdan oldin olib tashlanadi.
        result?.Remove("tempPassword");
        return new ScriptResult(p.ExitCode, result, log.ToString());
    }

    public static string Redact(string text, IEnumerable<string> secrets)
    {
        foreach (var s in secrets)
            if (s.Length >= 4) text = text.Replace(s, "********", StringComparison.Ordinal);
        return text;
    }
}
