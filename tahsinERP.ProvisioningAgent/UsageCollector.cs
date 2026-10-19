using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Tenant bazasidan foydalanuvchi faolligini o'qiydi (faqat SELECT): USERS (ID, Uname, IsActive), USER_ENTRIES (ID, UserID, DateTime),
/// USERLOGS (ID, UserID, DateTime, ControllerName, ActionName). Parol/HashCode, FullName, Email, IP va MAC o'qilmaydi ham.
/// ERP vaqtni server lokal vaqtida (DateTime.Now) yozadi — agent ERP bilan bir serverda, shuning uchun lokal → UTC.
/// </summary>
public sealed class UsageCollector(AgentOptions options, SqlOps sql, TimeProvider time)
{
    public async Task<UsageReport> CollectAsync(UsageTarget target, CancellationToken ct)
    {
        var r = new UsageReport { Slug = target.Slug, CollectedAtUtc = time.GetUtcNow().UtcDateTime, EntryCursor = target.EntryCursor, LogCursor = target.LogCursor };
        try
        {
            TenantsFile.ValidateDatabase(target.DbName);
            await using var c = new SqlConnection(sql.ConnectionString(target.DbName));
            await c.OpenAsync(ct);
            int limit = Math.Clamp(options.Usage.BatchLimit, 100, 10_000);
            DateTime since = DateTime.Now.AddDays(-Math.Max(1, options.Usage.LookbackDays)); // lokal vaqt (ERP yozuvi bilan bir xil)

            long maxEntry = Convert.ToInt64(await ScalarAsync(c, "SELECT ISNULL(MAX(ID), 0) FROM dbo.USER_ENTRIES", ct));
            long maxLog = Convert.ToInt64(await ScalarAsync(c, "SELECT ISNULL(MAX(ID), 0) FROM dbo.USERLOGS", ct));
            long entryCursor = target.EntryCursor, logCursor = target.LogCursor;
            if (maxEntry < entryCursor || maxLog < logCursor)
            {
                r.ResetDetected = true; // baza qayta tiklangan (demo reset) — boshidan
                entryCursor = 0;
                logCursor = 0;
            }

            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT ID, Uname, IsActive, ISNULL(IsDeleted, 0) FROM dbo.USERS";
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                    if (!rd.GetBoolean(3))
                        r.Users.Add(new UsageUserDto { UserId = rd.GetInt32(0), Login = rd.GetString(1), IsActive = rd.GetBoolean(2) });
            }

            int entries = 0;
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT TOP ({limit}) ID, UserID, [DateTime] FROM dbo.USER_ENTRIES WHERE ID > @c AND [DateTime] >= @s ORDER BY ID";
                cmd.Parameters.AddWithValue("@c", entryCursor);
                cmd.Parameters.AddWithValue("@s", since);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    long id = Convert.ToInt64(rd.GetValue(0));
                    r.Events.Add(new UsageEventDto { Kind = UsageEventKind.Login, SourceId = id, UserId = rd.GetInt32(1), AtUtc = ToUtc(rd.GetDateTime(2)) });
                    entryCursor = Math.Max(entryCursor, id);
                    entries++;
                }
            }
            if (entries < limit) entryCursor = Math.Max(entryCursor, maxEntry); // 90 kundan eski qatorlarni qayta ko'rmaslik

            int logs = 0;
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT TOP ({limit}) ID, UserID, [DateTime], ControllerName, CAST(ActionName AS nvarchar(600)) FROM dbo.USERLOGS WHERE ID > @c AND [DateTime] >= @s ORDER BY ID";
                cmd.Parameters.AddWithValue("@c", logCursor);
                cmd.Parameters.AddWithValue("@s", since);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    long id = Convert.ToInt64(rd.GetValue(0));
                    r.Events.Add(new UsageEventDto
                    {
                        Kind = UsageEventKind.Action, SourceId = id, UserId = rd.GetInt32(1), AtUtc = ToUtc(rd.GetDateTime(2)),
                        Module = Short(rd.IsDBNull(3) ? null : rd.GetString(3), 50),
                        Action = options.Usage.IncludeActionText && !rd.IsDBNull(4) ? UsageRedactor.Redact(rd.GetString(4)) : null,
                    });
                    logCursor = Math.Max(logCursor, id);
                    logs++;
                }
            }
            if (logs < limit) logCursor = Math.Max(logCursor, maxLog);

            r.EntryCursor = entryCursor;
            r.LogCursor = logCursor;
            r.More = entries >= limit || logs >= limit;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or InvalidJobException)
        {
            r.Error = ex.Message;
            r.Users.Clear();
            r.Events.Clear();
        }
        return r;
    }

    private static async Task<object?> ScalarAsync(SqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync(ct);
    }

    private static DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    private static string? Short(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s.Trim();
}

/// <summary>Amal matnini panelga yuborishdan oldin: email, telefon, 9+ xonali raqamlar (STIR, karta, hisob) maskalanadi, 200 belgigacha.</summary>
public static partial class UsageRedactor
{
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+?\d[\d\s\-()]{7,}\d")]
    private static partial Regex Phone();

    public const int MaxLength = 200;

    public static string Redact(string text)
    {
        string s = Email().Replace(text, "[email]");
        s = Phone().Replace(s, m => m.Value.Count(char.IsDigit) >= 9 ? "[raqam]" : m.Value);
        s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > MaxLength ? s[..MaxLength] + "…" : s;
    }
}
