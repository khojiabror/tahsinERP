using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>Qidiruv natijasi: qo'llanma bo'limi (<see cref="Module"/>, <see cref="Section"/> indeksi) yoki sayt FAQ'i (<see cref="Faq"/>).</summary>
public sealed record SearchHit(string? Module, int Section, int Faq, double Score)
{
    public bool IsFaq => Faq >= 0;
}

/// <summary>
/// Qo'llanma va FAQ bo'yicha kalit so'z qidiruvi (BM25). O'zbek/rus so'z qo'shimchalari uchun oddiy "stemming":
/// 5 harfdan uzun so'zning birinchi 5 harfi (qoldiq / qoldig'i / qoldiqlar → "qoldi"; склад / складе → "склад").
/// Apostroflar olib tashlanadi (o'zgartirish = ozgartirish). Indeks har til uchun bir marta, xotirada.
/// </summary>
public static partial class BotSearch
{
    private const double K1 = 1.4, B = 0.75;
    private const int StemLength = 5;

    private static readonly HashSet<string> StopWords = new(
        ("va bilan uchun qanday qanaqa nima nimaga nega qilish qilib qiladi qilsa bu shu u ular bor yoq ham da ni ga mi qayerda " +
         "qachon kerak mumkin men biz siz sizning bizning qaysi qancha agar yoki lekin endi har bir edi ekan emas " +
         "и в во на как что это с со по для ли не можно где когда есть ли а но или же бы мне нам вы вас ваш наш какой какие " +
         "сколько чтобы если при из у от до о об так тоже то же ещё еще уже все всё")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Stem));

    private sealed record Doc(string? Module, int Section, int Faq, Dictionary<string, int> Tf, int Length, HashSet<string> TitleTerms);

    private sealed record Index(List<Doc> Docs, Dictionary<string, int> Df, double AvgLength);

    private static readonly ConcurrentDictionary<string, Index> Indexes = new();

    public static IReadOnlyList<SearchHit> Search(string lang, string query, int take = 6)
    {
        lang = BotContent.Normalize(lang);
        var terms = Terms(query).Distinct().ToList();
        if (terms.Count == 0)
            return [];
        var index = Indexes.GetOrAdd(lang, Build);
        int n = index.Docs.Count;
        int need = terms.Count <= 2 ? 1 : (int)Math.Ceiling(terms.Count * 0.5);
        var hits = new List<SearchHit>();
        foreach (var d in index.Docs)
        {
            double score = 0;
            int matched = 0;
            foreach (var t in terms)
            {
                if (!d.Tf.TryGetValue(t, out int tf)) continue;
                matched++;
                double idf = Math.Log(1 + (n - index.Df[t] + 0.5) / (index.Df[t] + 0.5));
                score += idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * d.Length / index.AvgLength));
                if (d.TitleTerms.Contains(t)) score += idf * 0.8;   // sarlavhadagi moslik kuchliroq
            }
            if (matched < need || score <= 0) continue;
            score *= (double)matched / terms.Count;                // ko'proq so'z mos kelgani yuqorida
            if (d.Faq >= 0) score *= 1.25;                         // FAQ — tayyor qisqa javob
            hits.Add(new SearchHit(d.Module, d.Section, d.Faq, score));
        }
        return hits.OrderByDescending(h => h.Score).Take(take).ToList();
    }

    /// <summary>Bo'lim matnidan so'rov so'zi uchragan birinchi qatorning qisqa parchasi (oddiy matn).</summary>
    public static string Snippet(string html, string query, int max = 260)
    {
        var terms = Terms(query).ToHashSet();
        var lines = html.Split('\n').Select(PlainText).Where(l => l.Length > 0).ToList();
        string best = lines.FirstOrDefault(l => Terms(l).Any(terms.Contains)) ?? lines.FirstOrDefault() ?? "";
        return best.Length > max ? best[..max].TrimEnd() + "…" : best;
    }

    public static string PlainText(string html) => WebUtility.HtmlDecode(Tag().Replace(html, "")).Trim();

    public static IEnumerable<string> Terms(string text)
    {
        string s = text.ToLowerInvariant().Replace('ё', 'е');
        s = Apostrophes().Replace(s, "");
        foreach (Match m in Word().Matches(s))
        {
            if (m.Value.Length < 2) continue;
            string stem = Stem(m.Value);
            if (!StopWords.Contains(stem)) yield return stem;
        }
    }

    private static string Stem(string word) => word.Length > StemLength ? word[..StemLength] : word;

    private static Index Build(string lang)
    {
        var docs = new List<Doc>();
        foreach (var m in BotContent.Modules(lang))
            for (int i = 0; i < m.Sections.Count; i++)
            {
                var s = m.Sections[i];
                var title = Terms(s.Title).ToList();
                // Sarlavha uch marta (vazn), modul nomi bir marta — "ombor inventarizatsiya" kabi so'rovlar uchun.
                docs.Add(Make(m.Num, i, -1, title.Concat(title).Concat(title).Concat(Terms(m.Title)).Concat(Terms(PlainText(s.Body))), title));
            }
        var site = BotContent.Site(lang);
        int f = 0;
        foreach (var item in site.List("faq.items"))
        {
            var q = Terms(BotContent.Tr(lang, item["q"])).ToList();
            docs.Add(Make(null, -1, f++, q.Concat(q).Concat(q).Concat(Terms(BotContent.Tr(lang, item["a"]))), q));
        }
        var df = new Dictionary<string, int>();
        foreach (var d in docs)
            foreach (var t in d.Tf.Keys)
                df[t] = df.GetValueOrDefault(t) + 1;
        return new Index(docs, df, docs.Count == 0 ? 1 : docs.Average(d => d.Length));
    }

    private static Doc Make(string? module, int section, int faq, IEnumerable<string> terms, IEnumerable<string> title)
    {
        var tf = new Dictionary<string, int>();
        int len = 0;
        foreach (var t in terms)
        {
            tf[t] = tf.GetValueOrDefault(t) + 1;
            len++;
        }
        return new Doc(module, section, faq, tf, Math.Max(len, 1), title.ToHashSet());
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"['ʻʼ’‘`´]")]
    private static partial Regex Apostrophes();

    [GeneratedRegex(@"[\p{L}][\p{L}\p{Nd}]*")]
    private static partial Regex Word();
}
