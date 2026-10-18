using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tahsinERP.Provisioning.Contracts;

// Panel ↔ provisioning agenti kontrakti (Docs/ControlPanel/05_PROVISIONING.md, 3-bo'lim).
// Bu fayl agent loyihasiga ham ulangan (<Compile Include ... Link>) — ikki tomon bitta ta'rifdan foydalanadi.

/// <summary>HMAC-SHA256 imzo: agent har so'rovni server siri bilan imzolaydi; panel SQL'ga ulanmaydi, agent panelga.</summary>
public static class AgentProtocol
{
    public const string HeaderServer = "X-Agent-Server";
    public const string HeaderTimestamp = "X-Agent-Timestamp";
    public const string HeaderNonce = "X-Agent-Nonce";
    public const string HeaderSignature = "X-Agent-Signature";
    public const string HeaderVersion = "X-Agent-Version";
    public const int MaxClockSkewSeconds = 300;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BodyHash(ReadOnlySpan<byte> body) => Convert.ToHexStringLower(SHA256.HashData(body));

    /// <summary>METHOD\npath\ntimestamp\nnonce\nsha256hex(body)</summary>
    public static string StringToSign(string method, string path, string timestamp, string nonce, string bodyHash) =>
        method.ToUpperInvariant() + "\n" + path + "\n" + timestamp + "\n" + nonce + "\n" + bodyHash;

    public static string Sign(byte[] secret, string stringToSign) =>
        Convert.ToBase64String(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(stringToSign)));

    public static bool Verify(byte[] secret, string stringToSign, string? signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        byte[] given;
        try { given = Convert.FromBase64String(signature); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(stringToSign)), given);
    }

    /// <summary>So'rovga imzo sarlavhalarini qo'shadi (agent tomoni).</summary>
    public static void SignRequest(HttpRequestMessage request, int serverId, byte[] secret, byte[] body, DateTimeOffset now, string version)
    {
        string ts = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        string path = request.RequestUri!.AbsolutePath;
        request.Headers.Add(HeaderServer, serverId.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add(HeaderTimestamp, ts);
        request.Headers.Add(HeaderNonce, nonce);
        request.Headers.Add(HeaderVersion, version);
        request.Headers.Add(HeaderSignature, Sign(secret, StringToSign(request.Method.Method, path, ts, nonce, BodyHash(body))));
    }
}

public enum JobKind { Create = 1, Reset = 2, Suspend = 3, Resume = 4, Archive = 5, Delete = 6 }

public enum JobOutcome { Succeeded = 1, Failed = 2, Retry = 3 }

public sealed class LeaseRequest
{
    public string? Host { get; set; }
}

/// <summary>Agentga berilgan job. Secrets faqat shu javobda keladi (HTTPS), agent ularni log'ga yozmaydi.</summary>
public sealed class JobLease
{
    public long JobId { get; set; }
    public string LeaseToken { get; set; } = "";
    public JobKind Type { get; set; }
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime LeaseUntilUtc { get; set; }
    public JobPayload Payload { get; set; } = new();
    public JobSecrets? Secrets { get; set; }
}

public sealed class JobPayload
{
    public string Slug { get; set; } = "";
    public string DbName { get; set; } = "";
    public int CompanyId { get; set; } = 1;
    public string? CompanyName { get; set; }
    public string AdminLogin { get; set; } = "admin";
    public string? AdminFullName { get; set; }
    public string? AdminEmail { get; set; }
    /// <summary>tenants.json dagi enabled qiymati (panel holatidan: Suspended/Archived → false).</summary>
    public bool Enabled { get; set; } = true;
    public bool Demo { get; set; }
    /// <summary>Panel Servers.SqlInstance — ma'lumot uchun; agent o'z sozlamasidagi SQL instance'ga ulanadi.</summary>
    public string? SqlInstance { get; set; }
    public string? Reason { get; set; }
}

public sealed class JobSecrets
{
    public string? RegToken { get; set; }
    public string? AdminPassword { get; set; }
}

public sealed class JobCompletion
{
    public string LeaseToken { get; set; } = "";
    public JobOutcome Outcome { get; set; }
    /// <summary>Shu urinish log'i (maxfiy qiymatlarsiz).</summary>
    public string? Log { get; set; }
    /// <summary>Natija JSON (New-TenantDb RESULT — tempPassword o'chirilgan).</summary>
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int? ExitCode { get; set; }
    public int? RetryAfterSeconds { get; set; }
    /// <summary>Archive: zaxira manzili (IBackupStore).</summary>
    public string? ArchiveLocation { get; set; }
    /// <summary>tenants.json dagi yakuniy holat: true/false, null — yozuv yo'q.</summary>
    public bool? RegistryEnabled { get; set; }
}

public sealed class ExtendRequest
{
    public string LeaseToken { get; set; } = "";
}

/// <summary>Agent rejalashtirgan job (hozircha faqat demo tenantning kechki Reset'i).</summary>
public sealed class ScheduleRequest
{
    public JobKind Type { get; set; }
    public string Slug { get; set; } = "";
    /// <summary>Takrorlanmaslik kaliti, masalan sana "2026-10-19".</summary>
    public string Key { get; set; } = "";
}

public sealed class ScheduleResponse
{
    public long JobId { get; set; }
    public bool Created { get; set; }
}
