using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace tahsinERP.Bot.Security;

/// <summary>
/// Telegram Mini App initData tekshiruvi (core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app):
/// secret = HMAC-SHA256(key "WebAppData", botToken); hash = hex(HMAC-SHA256(secret, data_check_string)),
/// data_check_string — "hash"dan boshqa maydonlar, kalit bo'yicha saralangan, "k=v" lar "\n" bilan.
/// </summary>
public static class WebAppInitData
{
    public sealed record Result(long UserId, string? FirstName, string? LanguageCode, DateTimeOffset AuthDate);

    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    public static Result? Validate(string? initData, string botToken, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(botToken)) return null;
        var pairs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0) continue;
            pairs[Uri.UnescapeDataString(part[..i].Replace('+', ' '))] = Uri.UnescapeDataString(part[(i + 1)..].Replace('+', ' '));
        }
        if (!pairs.Remove("hash", out var hash) || hash.Length != 64) return null;

        var check = string.Join("\n", pairs.Select(p => p.Key + "=" + p.Value));
        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check));
        byte[] given;
        try { given = Convert.FromHexString(hash); }
        catch (FormatException) { return null; }
        if (!CryptographicOperations.FixedTimeEquals(expected, given)) return null;

        if (!pairs.TryGetValue("auth_date", out var authRaw) || !long.TryParse(authRaw, out var auth)) return null;
        var authDate = DateTimeOffset.FromUnixTimeSeconds(auth);
        if (now - authDate > MaxAge || authDate - now > TimeSpan.FromMinutes(5)) return null;

        if (!pairs.TryGetValue("user", out var userJson)) return null;
        using var doc = JsonDocument.Parse(userJson);
        var u = doc.RootElement;
        if (!u.TryGetProperty("id", out var idEl) || !idEl.TryGetInt64(out var id)) return null;
        return new Result(id,
            u.TryGetProperty("first_name", out var fn) ? fn.GetString() : null,
            u.TryGetProperty("language_code", out var lc) ? lc.GetString() : null,
            authDate);
    }

    /// <summary>Test va diagnostika uchun: berilgan maydonlardan to'g'ri imzolangan initData.</summary>
    internal static string Build(IDictionary<string, string> fields, string botToken)
    {
        var sorted = new SortedDictionary<string, string>(fields, StringComparer.Ordinal);
        var check = string.Join("\n", sorted.Select(p => p.Key + "=" + p.Value));
        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check)));
        return string.Join("&", sorted.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value))) + "&hash=" + hash;
    }
}
