using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using tahsinERP.ControlPanel.Pages.Public;
using tahsinERP.Localization;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>Qo'llanma bo'limi (Bot/Content/kb.{uz,ru}.json — tools/manual/build_bot_kb.py quradi). Body — Telegram HTML.</summary>
public sealed record KbSection(string Num, string Title, string Body);

public sealed record KbModule(string Id, string Num, string Title, string Summary, IReadOnlyList<KbSection> Sections);

/// <summary>
/// Sotuv botining matnlari: Bot/Content/bot.{uz,ru}.json (interfeys), kb.{uz,ru}.json (qo'llanma) va sayt matnlari
/// (Pages/Public/Content — paketlar, FAQ, afzalliklar; bitta manba). Kirill o'zbekcha (oz) — lotin matnidan avtomatik
/// transliteratsiya (HTML teglari, havolalar o'zgarmaydi).
/// </summary>
public static partial class BotContent
{
    public static readonly string[] Languages = [tahsinERP.Localization.Languages.Uzbek, tahsinERP.Localization.Languages.UzbekCyrillic,
        tahsinERP.Localization.Languages.Russian];

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Texts = new();
    private static readonly ConcurrentDictionary<string, IReadOnlyList<KbModule>> Kbs = new();

    /// <summary>uz / oz / ru — boshqasi (masalan Telegram "en") o'zbekchaga tushadi.</summary>
    public static string Normalize(string? lang) =>
        tahsinERP.Localization.Languages.TryNormalize(lang) is { } l && Languages.Contains(l) ? l : tahsinERP.Localization.Languages.Uzbek;

    /// <summary>Lotin manbasi: oz → uz.</summary>
    private static string Source(string lang) => lang == "ru" ? "ru" : "uz";

    public static string T(string lang, string key, params object?[] args)
    {
        lang = Normalize(lang);
        var dict = Texts.GetOrAdd(lang, l =>
        {
            var src = Load<Dictionary<string, string>>($"bot.{Source(l)}.json");
            return l == "oz" ? src.ToDictionary(kv => kv.Key, kv => kv.Key == "chooseLang" ? kv.Value : ToCyrillic(kv.Value)) : src;
        });
        string text = dict.TryGetValue(key, out var v) ? v : key;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public static bool HasKey(string lang, string key) => T(lang, key) != key;

    /// <summary>Til tugmasidagi nom (o'z tilida).</summary>
    public static string LangName(string lang) => lang == "oz" ? "🇺🇿 Ўзбекча" : T(lang, "langName");

    public static IReadOnlyList<KbModule> Modules(string lang) => Kbs.GetOrAdd(Normalize(lang), l =>
    {
        using var stream = Open($"kb.{Source(l)}.json");
        using var doc = JsonDocument.Parse(stream);
        var list = new List<KbModule>();
        foreach (var m in doc.RootElement.GetProperty("modules").EnumerateArray())
        {
            var sections = m.GetProperty("sections").EnumerateArray()
                .Select(s => new KbSection(s.GetProperty("n").GetString()!, Tr(l, s.GetProperty("t").GetString()!), Tr(l, s.GetProperty("body").GetString()!)))
                .ToList();
            list.Add(new KbModule(m.GetProperty("id").GetString()!, m.GetProperty("num").GetString()!,
                Tr(l, m.GetProperty("title").GetString()!), Tr(l, m.GetProperty("summary").GetString()!), sections));
        }
        return list;
    });

    public static KbModule? Module(string lang, string num) => Modules(lang).FirstOrDefault(m => m.Num == num);

    /// <summary>Sayt matni (landing) shu tilda; oz uchun — uz manbasi, chiqishda <see cref="Tr"/>.</summary>
    public static LandingContent Site(string lang) => LandingContent.For(Source(Normalize(lang)));

    /// <summary>Lotin matni oz tiliga (boshqa tillarda — o'zgarmaydi).</summary>
    public static string Tr(string lang, string text) => lang == "oz" ? ToCyrillic(text) : text;

    /// <summary>HTML teglari, entity'lar va havolalar transliteratsiya qilinmaydi.</summary>
    public static string ToCyrillic(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var sb = new System.Text.StringBuilder(html.Length);
        int last = 0;
        foreach (Match m in Protected().Matches(html))
        {
            if (m.Index > last) sb.Append(UzTransliterator.ToCyrillic(html[last..m.Index]));
            sb.Append(m.Value);
            last = m.Index + m.Length;
        }
        if (last < html.Length) sb.Append(UzTransliterator.ToCyrillic(html[last..]));
        return sb.ToString();
    }

    private static T Load<T>(string file)
    {
        using var stream = Open(file);
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidOperationException($"Bot matni bo'sh: {file}");
    }

    private static Stream Open(string file)
    {
        string name = $"{typeof(BotContent).Namespace}.Content.{file}";
        return typeof(BotContent).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Bot matni topilmadi: {name}");
    }

    [GeneratedRegex(@"<[^>]+>|&#?\w+;|https?://\S+|[\w.-]+\.(?:uz|com|ru)\b|/[a-z_]+|\{\d+\}")]
    private static partial Regex Protected();
}
