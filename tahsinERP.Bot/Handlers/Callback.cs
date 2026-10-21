using System.Globalization;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// callback_data (≤ 64 bayt): "buyruq:arg1:arg2". Buyruqlar:
/// c:{mijoz} karta · ci:{mijoz} keldim · pay:{mijoz}:{tashrif} to'lov · pm:{usul} to'lov usuli · no:{tashrif} sabablar ·
/// nr:{tashrif}:{sabab} buyurtmasiz · ph:{tashrif} foto · map:{mijoz} xarita · rt marshrut · ho / ho! kassaga topshirish ·
/// ov:{buyurtma} rahbar tasdig'i · nc! yangi nuqtani saqlash · ak:{m0|m1|q} mijoz akt sverki davri · x bekor.
/// Ekspeditor va ovoz (2-bosqich) — UpdateRouter.Delivery.cs / UpdateRouter.Voice.cs izohlarida.
/// </summary>
public readonly record struct Callback(string Command, string[] Args)
{
    public static Callback Parse(string? data)
    {
        if (string.IsNullOrEmpty(data)) return new Callback("", []);
        var parts = data.Split(':');
        return new Callback(parts[0], parts[1..]);
    }

    public int? Int(int index) =>
        index < Args.Length && int.TryParse(Args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    public string? Str(int index) => index < Args.Length ? Args[index] : null;

    public static string Make(string command, params object?[] args) =>
        args.Length == 0 ? command : command + ":" + string.Join(":", args.Select(a => a switch
        {
            null => "0",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            var o => o.ToString()
        }));
}

public static class Amounts
{
    /// <summary>"250 000", "250,000", "250000.50" -> 250000(.50). Musbat bo'lmasa — null.</summary>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = text.Replace("so'm", "", StringComparison.OrdinalIgnoreCase).Replace("som", "", StringComparison.OrdinalIgnoreCase)
            .Replace("сум", "", StringComparison.OrdinalIgnoreCase);
        s = new string(s.Where(c => !char.IsWhiteSpace(c) && c != '\'' && c != '_').ToArray());
        // "250,000" (minglik) va "250,5" (kasr) — oxirgi verguldan keyin 3 raqam bo'lsa minglik
        int comma = s.LastIndexOf(',');
        if (s.Contains('.')) s = s.Replace(",", "");
        else if (comma >= 0) s = s.Length - comma - 1 == 3 ? s.Replace(",", "") : s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 && v < 1_000_000_000_000m
            ? Math.Round(v, 2) : null;
    }
}

public static class Phones
{
    /// <summary>"90 123 45 67", "+998 90 123-45-67", "998901234567" -> "+998901234567"; ERP'dagi NormalizePhone bilan bir xil qoida.</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Count(char.IsLetter) > 0) return null;
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;
        return digits.Length is >= 9 and <= 15 ? "+" + digits : null;
    }
}
