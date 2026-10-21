using System.Globalization;
using System.Text.RegularExpressions;

namespace tahsinERP.Bot.Voice;

/// <summary>Ovozli buyurtmadan ajratilgan natija: mijoz (taxminiy nom) va qatorlar (tovar matni, miqdor, birlik).</summary>
public sealed record VoiceOrderParse(string? Customer, List<VoiceOrderItem> Items);

public sealed record VoiceOrderItem(string Name, decimal Qty, string? Unit);

/// <summary>
/// Qoidaviy ajratgich (LLM bo'lmasa yoki xato bersa): matn vergul / "va" / "и" bo'yicha bo'linadi, har bo'lakda son
/// (raqam yoki uz/ru so'z bilan), undan keyin birlik (qop, quti, dona, kg, m2 …), qolgani — tovar nomi. Mijoz — "… do'koniga",
/// "… uchun", "магазин …", "для …" iboralaridan. Ataylab sodda: noaniq bo'lsa ERP qoralamasi "topilmadi" deydi, agent tasdiqlaydi.
/// </summary>
public static class VoiceParser
{
    private static readonly Dictionary<string, int> Numbers = new(StringComparer.OrdinalIgnoreCase)
    {
        // o'zbek
        ["bir"] = 1, ["ikki"] = 2, ["uch"] = 3, ["to'rt"] = 4, ["tort"] = 4, ["besh"] = 5, ["olti"] = 6, ["yetti"] = 7, ["sakkiz"] = 8,
        ["to'qqiz"] = 9, ["toqqiz"] = 9, ["o'n"] = 10, ["on"] = 10, ["yigirma"] = 20, ["o'ttiz"] = 30, ["ottiz"] = 30, ["qirq"] = 40,
        ["ellik"] = 50, ["oltmish"] = 60, ["yetmish"] = 70, ["sakson"] = 80, ["to'qson"] = 90, ["toqson"] = 90, ["yuz"] = 100, ["ming"] = 1000,
        // rus
        ["один"] = 1, ["одна"] = 1, ["одну"] = 1, ["два"] = 2, ["две"] = 2, ["три"] = 3, ["четыре"] = 4, ["пять"] = 5, ["шесть"] = 6,
        ["семь"] = 7, ["восемь"] = 8, ["девять"] = 9, ["десять"] = 10, ["пятнадцать"] = 15, ["двадцать"] = 20, ["тридцать"] = 30,
        ["сорок"] = 40, ["пятьдесят"] = 50, ["шестьдесят"] = 60, ["семьдесят"] = 70, ["восемьдесят"] = 80, ["девяносто"] = 90,
        ["сто"] = 100, ["двести"] = 200, ["триста"] = 300, ["пятьсот"] = 500, ["тысяча"] = 1000, ["тысячи"] = 1000
    };

    private static readonly string[] Units =
    [
        "qop", "qopda", "quti", "dona", "ta", "kg", "kilogramm", "kilo", "m2", "kv", "metr", "m", "poddon", "litr", "paket", "rulon", "list", "pachka",
        "мешок", "мешка", "мешков", "коробка", "коробки", "коробок", "штука", "штуки", "штук", "шт", "кг", "килограмм", "метр", "метра", "метров",
        "квадрат", "квадратов", "м2", "поддон", "поддона", "поддонов", "литр", "литра", "литров", "пачка", "пачки", "пачек", "рулон", "рулона", "рулонов", "лист", "листа", "листов"
    ];

    private static readonly Regex Splitter = new(@"[,;\n]+|\s+(?:va|ham|и|ещё|еще|плюс)\s+|\.\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CustomerUz = new(@"^(?<name>.+?)\s+(?:do'koniga|do‘koniga|dokoniga|do'konga|magaziniga|uchun|ga)\s*[:\-]?\s*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CustomerRu = new(@"^(?:для|магазин[уа]?|в\s+магазин)\s+(?<name>[^,:]+?)\s*[:,\-]\s*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static VoiceOrderParse Parse(string? text)
    {
        var items = new List<VoiceOrderItem>();
        if (string.IsNullOrWhiteSpace(text)) return new VoiceOrderParse(null, items);
        var s = text.Trim().TrimEnd('.', '!');
        string? customer = null;
        var m = CustomerRu.Match(s);
        if (!m.Success) m = CustomerUz.Match(s);
        if (m.Success && m.Groups["rest"].Value.Trim().Length > 0 && !StartsWithNumber(m.Groups["name"].Value))
        {
            customer = m.Groups["name"].Value.Trim();
            s = m.Groups["rest"].Value;
        }

        foreach (var chunk in Splitter.Split(s).Select(c => c.Trim()).Where(c => c.Length > 0))
        {
            var words = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var (qty, used) = ReadNumber(words, 0);
            if (used == 0)
            {
                // son oxirida: "sement M400 20 qop"
                for (int i = words.Count - 1; i > 0 && used == 0; i--)
                {
                    var (q2, u2) = ReadNumber(words, i);
                    if (u2 > 0 && (i + u2 == words.Count || i + u2 == words.Count - 1 && IsUnit(words[^1])))
                    {
                        qty = q2;
                        var unitTail = i + u2 < words.Count ? words[^1] : null;
                        var nameTail = string.Join(' ', words.Take(i));
                        if (nameTail.Length > 0) items.Add(new VoiceOrderItem(nameTail, qty, unitTail));
                        used = -1;
                    }
                }
                continue;
            }
            string? unit = null;
            int pos = used;
            if (pos < words.Count && IsUnit(words[pos])) unit = words[pos++];
            var name = string.Join(' ', words.Skip(pos)).Trim();
            if (name.Length > 0 && qty > 0) items.Add(new VoiceOrderItem(name, qty, unit));
        }
        return new VoiceOrderParse(customer, items);
    }

    private static bool StartsWithNumber(string s)
    {
        var w = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        return w.Count > 0 && ReadNumber(w, 0).Used > 0;
    }

    private static bool IsUnit(string w) => Units.Contains(Clean(w), StringComparer.OrdinalIgnoreCase);

    private static string Clean(string w) => w.Trim().Trim('.', ',', ':', ';').Replace('‘', '\'').Replace('’', '\'').Replace('ʻ', '\'');

    /// <summary>Raqam ("20", "2,5", "20ta") yoki so'zlar ("yigirma besh", "двадцать пять", "ikki yuz").</summary>
    internal static (decimal Qty, int Used) ReadNumber(IReadOnlyList<string> words, int start)
    {
        if (start >= words.Count) return (0, 0);
        var first = Clean(words[start]).ToLowerInvariant();
        var digits = Regex.Match(first, @"^(\d+(?:[.,]\d+)?)(?:ta|x)?$");
        if (digits.Success && decimal.TryParse(digits.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return (d, 1);
        decimal total = 0, current = 0;
        int used = 0;
        for (int i = start; i < words.Count; i++)
        {
            if (!Numbers.TryGetValue(Clean(words[i]), out var n)) break;
            if (n == 100 || n == 1000)
                current = (current == 0 ? 1 : current) * n;
            else
                current += n;
            if (n == 1000) { total += current; current = 0; }
            used++;
        }
        return used == 0 ? (0, 0) : (total + current, used);
    }
}
