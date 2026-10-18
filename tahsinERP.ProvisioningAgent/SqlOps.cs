using Microsoft.Data.SqlClient;
using tahsinERP.Helpers;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Lokal SQL amallari (faqat agentda): mavjudlik, LICENSE_STATE.RegToken, backup/verify/restore/drop, demo foydalanuvchi.
/// Sxema o'zgartirilmaydi — u faqat Database/ skriptlari (migratsiyalar) orqali.
/// </summary>
public sealed class SqlOps(AgentOptions options)
{
    public string ConnectionString(string database)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = options.Sql.Server, InitialCatalog = database, ApplicationName = "tahsinERP.ProvisioningAgent",
            ConnectTimeout = 30, TrustServerCertificate = true, Pooling = false,
        };
        if (string.IsNullOrWhiteSpace(options.Sql.User)) b.IntegratedSecurity = true;
        else { b.UserID = options.Sql.User; b.Password = options.Sql.Password; }
        return b.ConnectionString;
    }

    private async Task<SqlConnection> OpenAsync(string database, CancellationToken ct)
    {
        var c = new SqlConnection(ConnectionString(database));
        try { await c.OpenAsync(ct); }
        catch (SqlException ex)
        {
            await c.DisposeAsync();
            throw new TransientJobException($"SQL Server'ga ulanib bo'lmadi ({options.Sql.Server}/{database}): {ex.Message}", ex);
        }
        return c;
    }

    private static async Task<object?> ScalarAsync(SqlConnection c, string sql, CancellationToken ct, params (string, object?)[] p)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return await cmd.ExecuteScalarAsync(ct);
    }

    private static string Q(string name) => "[" + name.Replace("]", "]]") + "]";

    public async Task<bool> DatabaseExistsAsync(string db, CancellationToken ct)
    {
        await using var c = await OpenAsync("master", ct);
        return Convert.ToInt32(await ScalarAsync(c, "SELECT COUNT(*) FROM sys.databases WHERE name = @n", ct, ("@n", db))) > 0;
    }

    public sealed record OurDb(bool IsTahsin, bool AdminExists, bool AdminMustChange);

    /// <summary>Baza bizning skript yaratgan tahsinERP bazasimi (SCHEMA_VERSIONS + seed) va admin holati.</summary>
    public async Task<OurDb> InspectAsync(string db, string adminLogin, CancellationToken ct)
    {
        await using var c = await OpenAsync(db, ct);
        bool seeded = Convert.ToInt32(await ScalarAsync(c,
            "SELECT CASE WHEN OBJECT_ID('dbo.SCHEMA_VERSIONS','U') IS NOT NULL AND OBJECT_ID('dbo.USERS','U') IS NOT NULL " +
            "AND EXISTS (SELECT 1 FROM dbo.SCHEMA_VERSIONS WHERE Kind = 'seed') THEN 1 ELSE 0 END", ct)) == 1;
        if (!seeded) return new OurDb(false, false, false);
        var must = await ScalarAsync(c, "SELECT TOP 1 CAST(MustChangePassword AS int) FROM dbo.USERS WHERE Uname = @u AND ISNULL(IsDeleted,0) = 0", ct, ("@u", adminLogin));
        return new OurDb(true, must != null && must != DBNull.Value, must is int m && m == 1);
    }

    /// <summary>Create qayta urinishida: admin parolini panel ko'rsatgan vaqtinchalik parolga tenglash (faqat hali almashtirilmagan bo'lsa).</summary>
    public async Task<bool> ResetTempPasswordAsync(string db, string login, string password, CancellationToken ct)
    {
        string hash = PasswordHasher.Hash(password, out string salt);
        await using var c = await OpenAsync(db, ct);
        int n = Convert.ToInt32(await ScalarAsync(c,
            "UPDATE dbo.USERS SET Password = @p, HashCode = @s WHERE Uname = @u AND MustChangePassword = 1 AND ISNULL(IsDeleted,0) = 0; SELECT @@ROWCOUNT;",
            ct, ("@p", hash), ("@s", salt), ("@u", login)));
        return n > 0;
    }

    /// <summary>
    /// LICENSE_STATE.RegToken (00_PLAN.md 3.2). InstallationId YOZILMAYDI — ERP o'zi yaratadi (muhrsiz InstallationId bloklanadi).
    /// ERP allaqachon panelga bog'langan bo'lsa (PanelSecret bor) — token kerak emas, tegilmaydi.
    /// </summary>
    public async Task<string> WriteRegTokenAsync(string db, string regToken, CancellationToken ct)
    {
        await using var c = await OpenAsync(db, ct);
        bool hasTable = Convert.ToInt32(await ScalarAsync(c, "SELECT CASE WHEN OBJECT_ID('dbo.LICENSE_STATE','U') IS NULL THEN 0 ELSE 1 END", ct)) == 1;
        if (!hasTable) throw new InvalidJobException("LICENSE_STATE jadvali yo'q — baza baseline'dan yaratilmagan.");
        bool bound = Convert.ToInt32(await ScalarAsync(c, "SELECT COUNT(*) FROM dbo.LICENSE_STATE WHERE Name = N'PanelSecret' AND LEN(Value) > 0", ct)) > 0;
        if (bound) return "ERP allaqachon panelga bog'langan (PanelSecret bor) — RegToken yozilmadi";
        await ScalarAsync(c,
            "UPDATE dbo.LICENSE_STATE SET Value = @v, UpdatedAt = GETDATE() WHERE Name = N'RegToken'; " +
            "IF @@ROWCOUNT = 0 INSERT INTO dbo.LICENSE_STATE (Name, Value) VALUES (N'RegToken', @v);", ct, ("@v", regToken));
        return "LICENSE_STATE.RegToken yozildi";
    }

    /// <summary>COPY_ONLY + CHECKSUM backup va RESTORE VERIFYONLY.</summary>
    public async Task BackupAsync(string db, string file, CancellationToken ct)
    {
        await using var c = await OpenAsync("master", ct);
        await ScalarAsync(c, $"BACKUP DATABASE {Q(db)} TO DISK = @f WITH COPY_ONLY, INIT, FORMAT, CHECKSUM", ct, ("@f", file));
        await ScalarAsync(c, "RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM", ct, ("@f", file));
    }

    public async Task DropAsync(string db, CancellationToken ct)
    {
        SqlConnection.ClearAllPools();
        await using var c = await OpenAsync("master", ct);
        await ScalarAsync(c, $"IF DB_ID(@n) IS NOT NULL BEGIN ALTER DATABASE {Q(db)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Q(db)}; END", ct, ("@n", db));
    }

    /// <summary>Shablondan tiklash: fayllar instance'ning standart papkasiga &lt;db&gt;.mdf / &lt;db&gt;_log.ldf nomi bilan (REPLACE).</summary>
    public async Task RestoreAsync(string db, string bak, CancellationToken ct)
    {
        SqlConnection.ClearAllPools();
        await using var c = await OpenAsync("master", ct);
        string dataDir = (string)(await ScalarAsync(c, "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))", ct) ?? "");
        string logDir = (string)(await ScalarAsync(c, "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))", ct) ?? dataDir);
        var files = new List<(string Logical, string Type)>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = @f";
            cmd.Parameters.AddWithValue("@f", bak);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) files.Add((r.GetString(r.GetOrdinal("LogicalName")), r.GetString(r.GetOrdinal("Type"))));
        }
        var moves = new List<string>();
        var p = new List<(string, object?)> { ("@f", bak) };
        int i = 0, dataN = 0, logN = 0;
        foreach (var (logical, type) in files)
        {
            string target = type == "L"
                ? Path.Combine(logDir, $"{db}_log{(logN++ == 0 ? "" : logN.ToString())}.ldf")
                : Path.Combine(dataDir, $"{db}{(dataN++ == 0 ? "" : "_" + dataN)}.{(dataN == 1 ? "mdf" : "ndf")}");
            moves.Add($"MOVE @l{i} TO @t{i}");
            p.Add(($"@l{i}", logical));
            p.Add(($"@t{i}", target));
            i++;
        }
        await ScalarAsync(c, $"IF DB_ID(@n) IS NOT NULL ALTER DATABASE {Q(db)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", ct, ("@n", db));
        await ScalarAsync(c, $"RESTORE DATABASE {Q(db)} FROM DISK = @f WITH REPLACE, RECOVERY, {string.Join(", ", moves)}", ct, [.. p]);
        await ScalarAsync(c, $"ALTER DATABASE {Q(db)} SET MULTI_USER; ALTER DATABASE {Q(db)} SET RECOVERY SIMPLE;", ct);
    }

    /// <summary>
    /// Demo foydalanuvchi: Admin roli, MustChangePassword = 0 (har kecha tiklanadi — birinchi kelgan mehmon parolni almashtirib qo'ymasin).
    /// Admin roli SystemCode'siz — seed'dagi RName = 'Admin' (New-TenantDb.ps1 bilan bir xil qoida).
    /// </summary>
    public async Task EnsureDemoUserAsync(string db, string login, string password, CancellationToken ct)
    {
        string hash = PasswordHasher.Hash(password, out string salt);
        await using var c = await OpenAsync(db, ct);
        await ScalarAsync(c, """
            DECLARE @role int = (SELECT TOP 1 ID FROM dbo.ROLES WHERE RName = N'Admin' AND ISNULL(IsDeleted, 0) = 0);
            IF @role IS NULL THROW 50002, 'Admin roli topilmadi', 1;
            DECLARE @uid int = (SELECT TOP 1 ID FROM dbo.USERS WHERE Uname = @u);
            IF @uid IS NULL
            BEGIN
                INSERT INTO dbo.USERS (Uname, FullName, Password, Email, IsDeleted, IsActive, HashCode, CompanyID, MustChangePassword)
                VALUES (@u, N'Demo foydalanuvchi', @p, NULL, 0, 1, @s, 1, 0);
                SET @uid = CAST(SCOPE_IDENTITY() AS int);
            END
            ELSE
                UPDATE dbo.USERS SET Password = @p, HashCode = @s, IsDeleted = 0, IsActive = 1, MustChangePassword = 0 WHERE ID = @uid;
            IF NOT EXISTS (SELECT 1 FROM dbo.USERROLES WHERE UserID = @uid AND RoleID = @role)
                INSERT INTO dbo.USERROLES (UserID, RoleID) VALUES (@uid, @role);
            """, ct, ("@u", login), ("@p", hash), ("@s", salt));
    }

    /// <summary>ERP ulanishini tekshirish: ERP qiladigan kabi bazaga ulanib, COMPANIES va USERS o'qiladi.</summary>
    public async Task<string> ProbeAsync(string db, CancellationToken ct)
    {
        await using var c = await OpenAsync(db, ct);
        var company = await ScalarAsync(c, "SELECT TOP 1 Name FROM dbo.COMPANIES WHERE ID = 1", ct);
        int users = Convert.ToInt32(await ScalarAsync(c, "SELECT COUNT(*) FROM dbo.USERS WHERE ISNULL(IsDeleted,0) = 0", ct));
        return $"baza tekshirildi: COMPANIES#1='{company}', foydalanuvchilar={users}";
    }
}
