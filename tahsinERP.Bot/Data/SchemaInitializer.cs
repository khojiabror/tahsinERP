using Microsoft.Data.SqlClient;

namespace tahsinERP.Bot.Data;

/// <summary>Baza bo'lmasa yaratadi va Schema.sql ni qo'llaydi (idempotent).</summary>
public static class SchemaInitializer
{
    public static async Task EnsureAsync(string connectionString, CancellationToken ct = default)
    {
        var csb = new SqlConnectionStringBuilder(connectionString);
        var database = csb.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("ConnectionStrings:Bot — Database ko'rsatilmagan.");

        csb.InitialCatalog = "master";
        await using (var master = new SqlConnection(csb.ConnectionString))
        {
            await master.OpenAsync(ct);
            await using var cmd = new SqlCommand("IF DB_ID(@db) IS NULL EXEC('CREATE DATABASE ' + @q)", master);
            cmd.Parameters.AddWithValue("@db", database);
            cmd.Parameters.AddWithValue("@q", "[" + database.Replace("]", "]]") + "]");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var batch in Batches(ReadScript()))
        {
            await using var cmd = new SqlCommand(batch, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    internal static string ReadScript()
    {
        using var s = typeof(SchemaInitializer).Assembly.GetManifestResourceStream("tahsinERP.Bot.Data.Schema.sql")
                      ?? throw new InvalidOperationException("Schema.sql topilmadi.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>Bo'sh qator bilan ajratilgan buyruqlar; izoh qatorlari tashlanadi.</summary>
    internal static IEnumerable<string> Batches(string script) =>
        script.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => string.Join("\n", b.Split('\n').Where(l => !l.TrimStart().StartsWith("--"))).Trim())
            .Where(b => b.Length > 0);
}
