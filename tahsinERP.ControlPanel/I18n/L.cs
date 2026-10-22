using System.Globalization;
using System.Resources;
using tahsinERP.Localization;

namespace tahsinERP.ControlPanel.I18n;

/// <summary>
/// Panel va mijoz portali matnlari: <c>@L.T(kalit)</c>. Qidiruv: Resources/PanelStrings -> umumiy atamalar
/// (tahsinERP.Localization Common, ERP bilan bitta tizim). Til so'rov boshida <see cref="UiLanguage"/> qo'yadi.
/// Kalit qo'shish: tahsinERP.Localization/tools/resx_add.py Resources/PanelStrings Resources/strings/strings_*.py
/// </summary>
public static class L
{
    public static readonly ResourceManager Panel = new("tahsinERP.ControlPanel.Resources.PanelStrings", typeof(L).Assembly);

    public static readonly Translator Translator = new(Panel, Translator.Common);

    public static string T(string key) => Translator.T(key);

    public static string T(string key, params object?[] args) => Translator.T(key, args!);

    /// <summary>Berilgan tilda (xat, Telegram xabari — qabul qiluvchining tilida).</summary>
    public static string TFor(string? language, string key, params object?[] args) => Translator.TFor(language!, key, args!);

    /// <summary>Joriy til: uz / oz / ru.</summary>
    public static string Lang => Languages.Current;

    public static string HtmlLang => Languages.HtmlLang(Lang);
}

/// <summary>
/// Til tanlovi: cookie <see cref="CookieName"/> (sarlavhadagi tanlagich, /lang/{kod}); bo'lmasa — o'zbekcha.
/// UI tili CurrentUICulture'ga, oy nomlari va valyuta yozuvi CurrentCulture'ga (son/sana ajratkichlari o'zgarmaydi).
/// </summary>
public static class UiLanguage
{
    public const string CookieName = "tahsin_lang";

    public static string Resolve(HttpContext http) =>
        Languages.TryNormalize(http.Request.Cookies[CookieName]) ?? Languages.Default;

    public static void Apply(string language)
    {
        CultureInfo.CurrentUICulture = Languages.CultureFor(language);
        CultureInfo.CurrentCulture = DisplayCultures.For(language, CultureInfo.CurrentCulture);
    }

    public static IApplicationBuilder UseUiLanguage(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            Apply(Resolve(ctx));
            await next();
        });

    /// <summary>GET /lang/{kod}?r=/qaytish — cookie (1 yil) va lokal manzilga qaytish.</summary>
    public static void MapLanguageSwitch(this IEndpointRouteBuilder app) =>
        app.MapGet("/lang/{code}", (HttpContext ctx, string code, string? r) =>
        {
            var language = Languages.TryNormalize(code);
            if (language != null)
                ctx.Response.Cookies.Append(CookieName, language, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = ctx.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true
                });
            bool local = !string.IsNullOrEmpty(r) && r.StartsWith('/') && !r.StartsWith("//") && !r.StartsWith("/\\");
            return Results.Redirect(local ? r! : "/");
        }).AllowAnonymous();
}
