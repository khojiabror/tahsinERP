using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.Services.Editions;

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
                    string? controller = Short(EditionModules.NormalizeLogController(rd.IsDBNull(3) ? null : rd.GetString(3)), 50);
                    string? text = rd.IsDBNull(4) ? null : rd.GetString(4);
                    r.Events.Add(new UsageEventDto
                    {
                        Kind = UsageEventKind.Action, SourceId = id, UserId = rd.GetInt32(1), AtUtc = ToUtc(rd.GetDateTime(2)),
                        Module = controller is null ? null : EditionModules.ModuleOf(controller).ToString(),
                        Controller = controller,
                        ActionType = UsageClassifier.Classify(text),
                        Action = options.Usage.IncludeActionText && text != null ? UsageRedactor.Redact(text) : null,
                    });
                    logCursor = Math.Max(logCursor, id);
                    logs++;
                }
            }
            if (logs < limit) logCursor = Math.Max(logCursor, maxLog);

            await CollectDocsAsync(c, r, ct);

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

    // Asosiy hujjatlar: (tur, jadval, sana ustuni, IsDeleted bormi). Sana bo'yicha — USERLOGS'ga bog'liq emas (orqa sana bilan
    // kiritilganini ham to'g'ri ko'rsatadi). Jadval/ustun yo'q bo'lsa (eski baza) — o'sha tur o'tkazib yuboriladi.
    private static readonly (string Type, string Table, string DateColumn, bool SoftDelete)[] DocSources =
    [
        (UsageDocTypes.SalesOrder, "S_ORDERS", "IssuedDate", true),
        (UsageDocTypes.PurchaseOrder, "P_ORDERS", "IssuedDate", true),
        (UsageDocTypes.Income, "INCOMES", "IssueDateTime", true),
        (UsageDocTypes.SalesInvoice, "SALES_INVOICES", "InvoiceDate", false),
        (UsageDocTypes.Production, "PRODUCT_PRODUCTION", "Date", true),
        (UsageDocTypes.Waybill, "F_WAYBILLS", "IssueDate", true),
        (UsageDocTypes.Payroll, "HR_PAYROLL_HEADERS", "CreatedDT", true),
    ];

    private async Task CollectDocsAsync(SqlConnection c, UsageReport r, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(DateTime.Now.AddDays(-Math.Max(1, options.Usage.DocsWindowDays)));
        r.DocsFromDate = from;
        foreach (var (type, table, col, soft) in DocSources)
        {
            try
            {
                await using var cmd = c.CreateCommand();
                cmd.CommandText = $"SELECT CAST([{col}] AS date), COUNT(*) FROM dbo.[{table}] WHERE [{col}] >= @f{(soft ? " AND ISNULL(IsDeleted, 0) = 0" : "")} GROUP BY CAST([{col}] AS date)";
                cmd.Parameters.AddWithValue("@f", from.ToDateTime(TimeOnly.MinValue));
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                    if (!rd.IsDBNull(0))
                        r.Docs.Add(new UsageDocDay { DocType = type, Date = DateOnly.FromDateTime(rd.GetDateTime(0)), Count = rd.GetInt32(1) });
            }
            catch (SqlException) { /* jadval/ustun yo'q — o'tkazib yuboriladi */ }
        }
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

/// <summary>USERLOGS.ActionName fe'li bo'yicha amal turi (ERP log matnlari o'zbekcha: "... yaratildi / tahrirlandi / o'chirildi").</summary>
public static partial class UsageClassifier
{
    [GeneratedRegex(@"o'?chir|o‘chir|delete|udal", RegexOptions.IgnoreCase)]
    private static partial Regex Delete();

    [GeneratedRegex(@"eksport|export|excel|pdf|chop|print|yuklab", RegexOptions.IgnoreCase)]
    private static partial Regex Export();

    [GeneratedRegex(@"yarat|qo'?sh|qo‘sh|create|import|yuklandi", RegexOptions.IgnoreCase)]
    private static partial Regex Create();

    [GeneratedRegex(@"tahrir|yangila|o'?zgar|o‘zgar|edit|update|tasdiq|bekor|status|holat|tikla", RegexOptions.IgnoreCase)]
    private static partial Regex Edit();

    [GeneratedRegex(@"ko'?r|ko‘r|view|details|ochdi", RegexOptions.IgnoreCase)]
    private static partial Regex View();

    public static UsageActionType Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return UsageActionType.Other;
        // Tartib muhim: "o'chirildi" "o'zgar"dan, "eksport" "yukla"dan oldin
        if (Delete().IsMatch(text)) return UsageActionType.Delete;
        if (Export().IsMatch(text)) return UsageActionType.Export;
        if (Create().IsMatch(text)) return UsageActionType.Create;
        if (Edit().IsMatch(text)) return UsageActionType.Edit;
        if (View().IsMatch(text)) return UsageActionType.View;
        return UsageActionType.Other;
    }
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
