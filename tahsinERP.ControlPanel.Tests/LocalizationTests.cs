using System.Net;
using System.Xml.Linq;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Ko'p tillilik: til tanlagichi (/lang), cookie bo'yicha sahifa tili, resx to'liqligi.</summary>
public class LocalizationTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    [Fact]
    public async Task LangSwitch_SetsCookie_AndRedirectsOnlyLocally()
    {
        using var c = f.Https();
        var r = await c.GetAsync("/lang/ru?r=%2FAccount%2FLogin");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Account/Login", r.Headers.Location!.OriginalString);
        Assert.Contains(r.Headers.GetValues("Set-Cookie"), v => v.StartsWith("tahsin_lang=ru"));

        var external = await c.GetAsync("/lang/ru?r=https%3A%2F%2Fevil.example%2F");
        Assert.Equal("/", external.Headers.Location!.OriginalString);
        var protocolRelative = await c.GetAsync("/lang/ru?r=%2F%2Fevil.example");
        Assert.Equal("/", protocolRelative.Headers.Location!.OriginalString);

        var unknown = await c.GetAsync("/lang/en?r=%2F");
        Assert.False(unknown.Headers.TryGetValues("Set-Cookie", out _), "noma'lum til cookie qo'ymaydi");
    }

    [Fact]
    public async Task LoginPage_FollowsLanguageCookie()
    {
        using var c = f.Https();
        string uz = await (await c.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        Assert.Contains("Boshqaruv paneli", uz);   // kirish sahifasidagi zona yozuvi (Pnl_Brand)
        Assert.Contains("lang=\"uz\"", uz);

        foreach (var (code, text, htmlLang) in new[] { ("ru", "Панель управления", "ru"), ("oz", "Бошқарув панели", "uz-Cyrl") })
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
            req.Headers.Add("Cookie", "tahsin_lang=" + code);
            string html = WebUtility.HtmlDecode(await (await c.SendAsync(req)).Content.ReadAsStringAsync());
            Assert.Contains(text, html);
            Assert.Contains("lang=\"" + htmlLang + "\"", html);
        }
    }

    [Fact]
    public void PanelStrings_EveryKeyHasRussian()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string res = Path.Combine(dir!.FullName, "tahsinERP.ControlPanel", "Resources");
        static string[] Keys(string p) => XDocument.Load(p).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var uz = Keys(Path.Combine(res, "PanelStrings.resx"));
        var ru = Keys(Path.Combine(res, "PanelStrings.ru.resx"));
        Assert.NotEmpty(uz);
        Assert.Equal(uz, ru);

        // kodda ishlatilgan har bir L.T("...") kaliti mavjud (panel yoki umumiy to'plamda)
        var common = Keys(Path.Combine(dir.FullName, "tahsinERP.Localization", "Resources", "Common.resx"));
        var all = new HashSet<string>(uz.Concat(common));
        var rx = new System.Text.RegularExpressions.Regex("\\bL\\.(?:T|TFor)\\((?:[^,()\"]+,\\s*)?\"([A-Za-z0-9_.]+)\"");
        var missing = Directory.GetFiles(Path.Combine(dir.FullName, "tahsinERP.ControlPanel"), "*.*", SearchOption.AllDirectories)
            .Where(p => (p.EndsWith(".cs") || p.EndsWith(".cshtml")) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .SelectMany(p => rx.Matches(File.ReadAllText(p)).Select(m => m.Groups[1].Value))
            .Where(k => !k.EndsWith('_') && !all.Contains(k))
            .Distinct().ToList();
        Assert.True(missing.Count == 0, "resx'da yo'q: " + string.Join(", ", missing));
    }
}
