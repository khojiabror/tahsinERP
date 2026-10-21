using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace tahsinERP.Site.Data;

/// <summary>Sayt servisi tenanti: ERP manzili va ERP tomonidagi tenant (imzo kaliti shundan).</summary>
public sealed class SiteTenant
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string ErpBaseUrl { get; set; } = "";
    public string? ErpTenant { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Oxirgi olingan katalog nusxasi (servis qayta ishga tushsa — ERP'siz ham sayt ishlaydi).</summary>
public sealed class SiteSnapshotRow
{
    public int TenantId { get; set; }
    public string ETag { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime FetchedAt { get; set; }
}

/// <summary>Ariza navbati: avval shu yerga, keyin ERP'ga (ERP o'chiq bo'lsa ham yo'qolmaydi).</summary>
public sealed class LeadOutboxItem
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime NextAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public int? ErpLeadId { get; set; }
}

public sealed class SiteDbContext(DbContextOptions<SiteDbContext> options) : DbContext(options)
{
    public DbSet<SiteTenant> Tenants => Set<SiteTenant>();
    public DbSet<SiteSnapshotRow> Snapshots => Set<SiteSnapshotRow>();
    public DbSet<LeadOutboxItem> LeadOutbox => Set<LeadOutboxItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SiteTenant>(e =>
        {
            e.ToTable("SiteTenants");
            e.Property(x => x.Slug).HasMaxLength(50);
            e.Property(x => x.ErpBaseUrl).HasMaxLength(300);
            e.Property(x => x.ErpTenant).HasMaxLength(50);
        });
        b.Entity<SiteSnapshotRow>(e =>
        {
            e.ToTable("SiteSnapshots");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.ETag).HasMaxLength(100);
        });
        b.Entity<LeadOutboxItem>(e => e.ToTable("LeadOutbox"));
    }
}

/// <summary>Baza bo'lmasa yaratadi va Schema.sql ni qo'llaydi (idempotent; bo'sh qator bilan ajratilgan buyruqlar).</summary>
public static class SchemaInitializer
{
    public static async Task EnsureAsync(string connectionString, CancellationToken ct = default)
    {
        var csb = new SqlConnectionStringBuilder(connectionString);
        var database = csb.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("ConnectionStrings:Site — Database ko'rsatilmagan.");
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
        using var s = typeof(SchemaInitializer).Assembly.GetManifestResourceStream("tahsinERP.Site.Data.Schema.sql")
                      ?? throw new InvalidOperationException("Schema.sql topilmadi.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    internal static IEnumerable<string> Batches(string script) =>
        script.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => string.Join("\n", b.Split('\n').Where(l => !l.TrimStart().StartsWith("--"))).Trim())
            .Where(b => b.Length > 0);
}
