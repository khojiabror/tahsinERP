using System.Collections.Concurrent;
using System.Text.Json;

namespace tahsinERP.ControlPanel.Pages.Public;

/// <summary>
/// Ommaviy sahifa matnlari: Pages/Public/Content/{uz,ru,en}.json (ilovaga EmbeddedResource sifatida kiradi). Shablon bitta —
/// til faqat matnni almashtiradi. Yo'l nuqta bilan: <c>C["hero.title"]</c>, ro'yxat — <c>C.List("faq.items")</c>.
/// </summary>
public sealed class LandingContent
{
    public static readonly string[] Languages = ["uz", "ru", "en"];
    private static readonly ConcurrentDictionary<string, LandingContent> Cache = new();

    private readonly JsonElement _node;

    private LandingContent(JsonElement node) => _node = node;

    public static LandingContent For(string lang) => Cache.GetOrAdd(lang, l =>
    {
        string name = $"{typeof(LandingContent).Namespace}.Content.{l}.json";
        using var stream = typeof(LandingContent).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Sahifa matni topilmadi: {name}");
        using var doc = JsonDocument.Parse(stream);
        return new LandingContent(doc.RootElement.Clone());
    });

    public string this[string path] => Find(path) is { ValueKind: JsonValueKind.String } e ? e.GetString()! : Find(path)?.ToString() ?? "";

    public bool Flag(string path) => Find(path) is { ValueKind: JsonValueKind.True };

    public bool Has(string path) => Find(path) is { ValueKind: not JsonValueKind.Null };

    public IEnumerable<LandingContent> List(string path) =>
        Find(path) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().Select(e => new LandingContent(e)) : [];

    public IEnumerable<string> Strings(string path) => List(path).Select(c => c._node.ToString());

    public IEnumerable<int> Ints(string path) =>
        Find(path) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().Select(e => e.GetInt32()) : [];

    private JsonElement? Find(string path)
    {
        JsonElement cur = _node;
        foreach (string part in path.Split('.'))
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(part, out cur))
                return null;
        }
        return cur;
    }
}
