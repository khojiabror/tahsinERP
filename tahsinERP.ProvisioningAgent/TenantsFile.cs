using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// ERP tenants.json (02_MULTI_TENANT.md) ga atomik yozish: lock fayl → o'qish → o'zgartirish → temp faylga yozish (flush) →
/// <see cref="File.Move(string,string,bool)"/> (bir disk ichida MoveFileEx REPLACE_EXISTING — atomik). ERP faylni 15 soniyada
/// LastWriteTimeUtc bo'yicha qayta o'qiydi, shuning uchun yangi faylning vaqti eskisidan katta bo'lishi kafolatlanadi.
/// Boshqa tenantlar va noma'lum maydonlar saqlanadi; ERP bilan bir xil tekshiruvlar (slug, rezerv nomlar, baza nomi, baseDomain).
/// </summary>
public sealed partial class TenantsFile(string path, string? configuredBaseDomain)
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]{1,128}$")]
    private static partial Regex DatabaseRegex();

    /// <summary>ERP TenantRegistry.ReservedSlugs.</summary>
    public static readonly string[] ReservedSlugs =
        ["admin", "my", "pay", "www", "api", "app", "mail", "smtp", "static", "cdn", "assets", "status", "panel", "erp"];

    public string Path { get; } = path;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void ValidateSlug(string slug)
    {
        if (!SlugRegex().IsMatch(slug)) throw new InvalidJobException($"Yaroqsiz slug: '{slug}'");
        if (ReservedSlugs.Contains(slug)) throw new InvalidJobException($"Rezerv slug: '{slug}'");
    }

    public static void ValidateDatabase(string db)
    {
        if (!DatabaseRegex().IsMatch(db)) throw new InvalidJobException($"Yaroqsiz baza nomi: '{db}'");
    }

    /// <summary>Tenant yozuvini qo'shadi yoki yangilaydi (database, companyId, enabled). O'zgarish bo'lmasa fayl tegilmaydi.</summary>
    public bool Upsert(string slug, string database, int companyId, bool enabled)
    {
        ValidateSlug(slug);
        ValidateDatabase(database);
        return Mutate(tenants =>
        {
            var t = Find(tenants, slug);
            if (t is null)
            {
                tenants.Add(new JsonObject { ["slug"] = slug, ["database"] = database, ["companyId"] = companyId, ["enabled"] = enabled });
                return true;
            }
            bool changed = false;
            changed |= SetIfDifferent(t, "database", database);
            changed |= SetIfDifferent(t, "companyId", companyId);
            changed |= SetIfDifferent(t, "enabled", enabled);
            return changed;
        });
    }

    public bool Remove(string slug) => Mutate(tenants =>
    {
        var t = Find(tenants, slug);
        if (t is null) return false;
        tenants.Remove(t);
        return true;
    });

    /// <summary>Joriy yozuv (test va tekshiruv uchun): (database, enabled) yoki null.</summary>
    public (string Database, bool Enabled)? Get(string slug)
    {
        if (!File.Exists(Path)) return null;
        var root = JsonNode.Parse(File.ReadAllText(Path)) as JsonObject;
        var t = root?["tenants"] is JsonArray arr ? Find(arr, slug) : null;
        if (t is null) return null;
        bool enabled = t["enabled"] is JsonValue v && v.TryGetValue(out bool b) ? b : true;
        return ((string?)t["database"] ?? "", enabled);
    }

    private static JsonObject? Find(JsonArray tenants, string slug) =>
        tenants.OfType<JsonObject>().FirstOrDefault(t => string.Equals(((string?)t["slug"])?.Trim(), slug, StringComparison.OrdinalIgnoreCase));

    private static bool SetIfDifferent<T>(JsonObject o, string name, T value)
    {
        var current = o[name];
        if (current is JsonValue v && v.TryGetValue(out T? existing) && EqualityComparer<T>.Default.Equals(existing, value)) return false;
        o[name] = JsonValue.Create(value);
        return true;
    }

    private bool Mutate(Func<JsonArray, bool> change)
    {
        string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
        Directory.CreateDirectory(dir);
        using var lockHandle = AcquireLock(Path + ".lock");

        JsonObject root;
        DateTime oldStamp = DateTime.MinValue;
        if (File.Exists(Path))
        {
            oldStamp = File.GetLastWriteTimeUtc(Path);
            string text = File.ReadAllText(Path);
            try { root = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text) as JsonObject ?? throw new JsonException("ildiz obyekt emas"); }
            catch (JsonException ex) { throw new InvalidJobException($"tenants.json buzilgan — qo'lda tuzating ({ex.Message})"); }
        }
        else root = new JsonObject();

        if (root["tenants"] is not JsonArray tenants)
        {
            tenants = new JsonArray();
            root["tenants"] = tenants;
        }
        bool changed = change(tenants);

        // ERP: tenantlar bo'lsa va baseDomain topilmasa reestr "Broken" (503) — bunday faylni yozmaymiz.
        string? fileDomain = (string?)root["baseDomain"];
        if (string.IsNullOrWhiteSpace(fileDomain) && !string.IsNullOrWhiteSpace(configuredBaseDomain))
        {
            root["baseDomain"] = configuredBaseDomain!.Trim().TrimStart('.');
            changed = true;
        }
        if (tenants.Count > 0 && string.IsNullOrWhiteSpace((string?)root["baseDomain"])
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TAHSINERP_BASE_DOMAIN")))
            throw new InvalidJobException("Bazaviy domen berilmagan (agent Paths:BaseDomain yoki TAHSINERP_BASE_DOMAIN) — ERP reestri buzilgan deb hisoblardi.");
        if (!changed) return false;

        // Takroriy slug bo'lmasin (ERP buni Broken deb biladi)
        var dup = tenants.OfType<JsonObject>().GroupBy(t => ((string?)t["slug"])?.Trim().ToLowerInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidJobException($"tenants.json da takroriy slug: '{dup.Key}'");

        string tmp = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(Path) + ".tmp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(root.ToJsonString(WriteOptions));
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes);
                fs.Flush(true);
            }
            // ERP o'zgarishni LastWriteTimeUtc bo'yicha aniqlaydi — yangi vaqt eskisidan qat'iy katta bo'lsin.
            var stamp = DateTime.UtcNow;
            if (stamp <= oldStamp) stamp = oldStamp.AddSeconds(1);
            File.SetLastWriteTimeUtc(tmp, stamp);
            if (File.Exists(Path)) File.Copy(Path, Path + ".prev", overwrite: true);
            File.Move(tmp, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        return true;
    }

    private static FileStream AcquireLock(string lockPath)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(200); }
        }
    }
}

/// <summary>Qayta urinish foyda bermaydigan xato (noto'g'ri parametr, buzilgan fayl) — job Failed.</summary>
public sealed class InvalidJobException(string message) : Exception(message);

/// <summary>Vaqtinchalik xato — keyinroq qayta urinish (job Retry).</summary>
public sealed class TransientJobException(string message, Exception? inner = null) : Exception(message, inner);
