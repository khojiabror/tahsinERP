using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace tahsinERP.Updater;

public enum TargetKind { Pool, Service }

public sealed record Target(TargetKind Kind, string Name)
{
    public override string ToString() => (Kind == TargetKind.Pool ? "pool " : "xizmat ") + Name;
}

public sealed record ProcResult(int ExitCode, string Output);

public sealed record ErpSiteSpec(string Name, string Path, string HostName, string SharedSite, int IdleMinutes, string DataDir);

/// <summary>Tizim amallari (IIS, xizmatlar, robocopy, SQL, HTTP). Testlarda soxtasi bilan almashtiriladi.</summary>
public interface ISystemOps
{
    Task StopAsync(Target t, CancellationToken ct);
    Task StartAsync(Target t, CancellationToken ct);
    string? State(Target t);
    /// <summary>robocopy src dst /E (mirror=true — /MIR). Chiqish kodi ≥ 8 — istisno.</summary>
    Task CopyAsync(string src, string dst, bool mirror, IReadOnlyCollection<string>? excludeFiles, IReadOnlyCollection<string>? excludeDirs, CancellationToken ct);
    Task<ProcResult> RunAsync(string exe, IReadOnlyList<string> args, IDictionary<string, string>? env, string? workDir, CancellationToken ct);
    /// <summary>HTTP status (null — ulanib bo'lmadi). So'rov lokal 127.0.0.1 ga, Host/SNI — URL'dagi nom.</summary>
    Task<int?> HttpStatusAsync(string url, CancellationToken ct);
    /// <summary>Pool environmentVariables (sirlar — faqat xotirada, log'ga yozilmaydi).</summary>
    IDictionary<string, string> PoolEnv(string pool);
    Task BackupDbAsync(string db, string file, CancellationToken ct);
    Task RestoreDbAsync(string db, string file, CancellationToken ct);
    string? SchemaVersion(string db);
    /// <summary>IIS APPPOOL\&lt;pool&gt; login'i va bazada db_owner.</summary>
    Task GrantDbAccessAsync(string pool, string db, CancellationToken ct);
    /// <summary>Faqat pool (env bilan) — IIS APPPOOL\&lt;pool&gt; hisobi paydo bo'ladi (SQL login shundan keyin).</summary>
    Task EnsureErpPoolAsync(ErpSiteSpec spec, CancellationToken ct);
    Task EnsureErpSiteAsync(ErpSiteSpec spec, CancellationToken ct);
    Task RemoveErpSiteAsync(string name, CancellationToken ct);
}

public sealed class WindowsSystemOps(UpdaterOptions options, ILogger<WindowsSystemOps> log) : ISystemOps
{
    private static string AppCmd => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
    private static string ApplicationHostConfig =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "config", "applicationHost.config");

    public async Task StopAsync(Target t, CancellationToken ct)
    {
        if (t.Kind == TargetKind.Service)
        {
            using var sc = new ServiceController(t.Name);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                if (sc.Status != ServiceControllerStatus.StopPending) sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(2));
            }
        }
        else
        {
            var r = await RunAsync(AppCmd, ["stop", "apppool", "/apppool.name:" + t.Name], null, null, ct);
            if (r.ExitCode != 0 && !r.Output.Contains("already stopped", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{t} to'xtatilmadi: {r.Output.Trim()}");
        }
        await Task.Delay(TimeSpan.FromSeconds(3), ct); // w3wp/xizmat fayllarni qo'yib yuborsin
    }

    public async Task StartAsync(Target t, CancellationToken ct)
    {
        if (t.Kind == TargetKind.Service)
        {
            using var sc = new ServiceController(t.Name);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                if (sc.Status != ServiceControllerStatus.StartPending) sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(2));
            }
            return;
        }
        var r = await RunAsync(AppCmd, ["start", "apppool", "/apppool.name:" + t.Name], null, null, ct);
        if (r.ExitCode != 0 && !r.Output.Contains("already started", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{t} ishga tushmadi: {r.Output.Trim()}");
    }

    public string? State(Target t)
    {
        try
        {
            if (t.Kind == TargetKind.Service)
            {
                using var sc = new ServiceController(t.Name);
                return sc.Status.ToString();
            }
            var r = RunAsync(AppCmd, ["list", "apppool", "/apppool.name:" + t.Name, "/text:state"], null, null, CancellationToken.None).GetAwaiter().GetResult();
            return r.ExitCode == 0 ? r.Output.Trim() : null;
        }
        // ServiceController yo'q xizmat uchun InvalidOperationException beradi; Win32Exception — ruxsat.
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    public async Task CopyAsync(string src, string dst, bool mirror, IReadOnlyCollection<string>? excludeFiles, IReadOnlyCollection<string>? excludeDirs,
        CancellationToken ct)
    {
        var args = new List<string> { src, dst, mirror ? "/MIR" : "/E", "/R:5", "/W:2", "/NFL", "/NDL", "/NJH", "/NJS", "/NP" };
        if (excludeFiles is { Count: > 0 }) { args.Add("/XF"); args.AddRange(excludeFiles); }
        if (excludeDirs is { Count: > 0 }) { args.Add("/XD"); args.AddRange(excludeDirs); }
        var r = await RunAsync("robocopy.exe", args, null, null, ct);
        if (r.ExitCode >= 8) throw new IOException($"robocopy {src} -> {dst}: kod {r.ExitCode}. {Tail(r.Output)}");
    }

    public async Task<ProcResult> RunAsync(string exe, IReadOnlyList<string> args, IDictionary<string, string>? env, string? workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        if (workDir != null) psi.WorkingDirectory = workDir;
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env != null) foreach (var (k, v) in env) psi.Environment[k] = v;
        var sb = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, options.ProcessTimeoutMinutes)));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new System.TimeoutException($"{Path.GetFileName(exe)} vaqt chegarasidan oshdi.");
        }
        p.WaitForExit();
        lock (sb) return new ProcResult(p.ExitCode, sb.ToString());
    }

    public async Task<int?> HttpStatusAsync(string url, CancellationToken ct)
    {
        var uri = new Uri(url);
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (ctx, token) =>
            {
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await s.ConnectAsync(new IPEndPoint(IPAddress.Loopback, ctx.DnsEndPoint.Port), token);
                    return new NetworkStream(s, ownsSocket: true);
                }
                catch { s.Dispose(); throw; }
            },
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        try
        {
            using var r = await http.GetAsync(uri, ct);
            return (int)r.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogDebug("Health {Url}: {Message}", url, ex.Message);
            return null;
        }
    }

    public IDictionary<string, string> PoolEnv(string pool)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var doc = new XmlDocument { XmlResolver = null };
        using (var r = XmlReader.Create(ApplicationHostConfig, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            doc.Load(r);
        foreach (XmlElement add in doc.SelectNodes("/configuration/system.applicationHost/applicationPools/add")!)
        {
            if (!string.Equals(add.GetAttribute("name"), pool, StringComparison.OrdinalIgnoreCase)) continue;
            if (add.SelectNodes("environmentVariables/add") is { } vars)
                foreach (XmlElement v in vars) d[v.GetAttribute("name")] = v.GetAttribute("value");
        }
        return d;
    }

    private SqlConnection Open(string db)
    {
        var csb = new SqlConnectionStringBuilder
        {
            DataSource = options.SqlServer, InitialCatalog = db, IntegratedSecurity = true, TrustServerCertificate = true,
            ApplicationName = "tahsinERP.Updater", Pooling = false,
        };
        var c = new SqlConnection(csb.ConnectionString);
        c.Open();
        return c;
    }

    private async Task ExecAsync(string db, string sql, CancellationToken ct, params (string, object)[] ps)
    {
        await using var c = Open(db);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 3600;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task BackupDbAsync(string db, string file, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        return ExecAsync("master", $"BACKUP DATABASE [{db}] TO DISK = @f WITH COPY_ONLY, INIT, CHECKSUM", ct, ("@f", file));
    }

    public async Task RestoreDbAsync(string db, string file, CancellationToken ct)
    {
        try
        {
            await ExecAsync("master", $"""
                IF DB_ID(@d) IS NOT NULL ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{db}] FROM DISK = @f WITH REPLACE, CHECKSUM;
                """, ct, ("@d", db), ("@f", file));
        }
        finally
        {
            try { await ExecAsync("master", $"IF DB_ID(@d) IS NOT NULL ALTER DATABASE [{db}] SET MULTI_USER", CancellationToken.None, ("@d", db)); }
            catch (SqlException ex) { log.LogWarning("{Db} MULTI_USER qaytarilmadi: {Message}", db, ex.Message); }
        }
    }

    public string? SchemaVersion(string db)
    {
        try
        {
            using var c = Open(db);
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.SCHEMA_VERSIONS', N'U') IS NOT NULL
                SELECT TOP 1 ScriptName FROM dbo.SCHEMA_VERSIONS WHERE Kind IN ('baseline','migration') ORDER BY Version DESC, AppliedAt DESC
                """;
            return cmd.ExecuteScalar() as string;
        }
        catch (SqlException) { return null; }
    }

    public async Task GrantDbAccessAsync(string pool, string db, CancellationToken ct)
    {
        string login = @"IIS APPPOOL\" + pool;
        await ExecAsync("master", $"IF SUSER_ID(@l) IS NULL CREATE LOGIN [{login}] FROM WINDOWS", ct, ("@l", login));
        await ExecAsync(db, $"IF USER_ID(@l) IS NULL CREATE USER [{login}] FOR LOGIN [{login}] ELSE ALTER USER [{login}] WITH LOGIN = [{login}]; ALTER ROLE db_owner ADD MEMBER [{login}];",
            ct, ("@l", login));
    }

    private string IisOpsScript => Path.Combine(AppContext.BaseDirectory, "IisOps.ps1");

    public Task EnsureErpPoolAsync(ErpSiteSpec s, CancellationToken ct) => IisErpAsync("EnsureErpPool", s, ct);

    public Task EnsureErpSiteAsync(ErpSiteSpec s, CancellationToken ct) => IisErpAsync("EnsureErpSite", s, ct);

    private async Task IisErpAsync(string command, ErpSiteSpec s, CancellationToken ct)
    {
        var r = await RunAsync(options.PowerShellExe,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", IisOpsScript, "-Command", command,
             "-Name", s.Name, "-PhysicalPath", s.Path, "-HostName", s.HostName, "-SharedSite", s.SharedSite,
             "-IdleMinutes", s.IdleMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture), "-DataDir", s.DataDir], null, null, ct);
        if (r.ExitCode != 0) throw new InvalidOperationException($"IIS {command} {s.Name}: {Tail(r.Output)}");
    }

    public async Task RemoveErpSiteAsync(string name, CancellationToken ct)
    {
        var r = await RunAsync(options.PowerShellExe,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", IisOpsScript, "-Command", "RemoveErpSite", "-Name", name], null, null, ct);
        if (r.ExitCode != 0) throw new InvalidOperationException($"IIS sayt {name} o'chirilmadi: {Tail(r.Output)}");
    }

    private static string Tail(string s) => s.Length > 2000 ? "…" + s[^2000..] : s;
}
