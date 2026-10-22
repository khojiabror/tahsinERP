using System.Resources;
using System.Web;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Localization;

namespace tahsinERP.I18n
{
    /// <summary>
    /// ERP matnlari: <c>@L.T("Key")</c> (view'larda namespace Views/web.config'da ulangan).
    /// Qidiruv tartibi: Resources/ErpStrings (ERP) -> distributsiya DstStrings -> umumiy atamalar
    /// (tahsinERP.Localization Common). Til — joriy so'rovning CurrentUICulture'i (<see cref="UiLanguage"/>).
    /// Kalit qo'shish: tahsinERP.Localization/tools/resx_add.py Resources/ErpStrings strings_*.py
    /// </summary>
    public static class L
    {
        public static readonly ResourceManager Erp = new ResourceManager("tahsinERP.Resources.ErpStrings", typeof(L).Assembly);

        public static readonly Translator Translator = new Translator(Erp, DstLocalization.ResourceManager, Translator.Common);

        public static string T(string key) => Translator.T(key);

        public static string T(string key, params object[] args) => Translator.T(key, args);

        /// <summary>Berilgan tilda (bildirishnoma, eksport — qabul qiluvchining tilida).</summary>
        public static string TFor(string language, string key, params object[] args) => Translator.TFor(language, key, args);

        /// <summary>JavaScript satri ichiga (qo'shtirnoqsiz): <c>'@L.Js("Key")'</c>.</summary>
        public static IHtmlString Js(string key) => new HtmlString(HttpUtility.JavaScriptStringEncode(T(key)));

        /// <summary>
        /// JS uchun matnlar (window.erpI18n.t): "Js_" va "Dt_" kalitlari joriy tilda, JSON (&lt;/script&gt; xavfsiz).
        /// Sahifa skriptlari: erpT("Js_Key") yoki erpT("Js_Key", [arg0]).
        /// </summary>
        public static IHtmlString JsBundle()
        {
            var dict = new System.Collections.Generic.SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var prefix in new[] { "Js_", "Dt_" })
                foreach (var key in Translator.Keys(prefix))
                    dict[key] = T(key);
            return new HtmlString(Newtonsoft.Json.JsonConvert.SerializeObject(dict,
                new Newtonsoft.Json.JsonSerializerSettings { StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeHtml }));
        }

        /// <summary>Joriy til kodi: uz / oz / ru.</summary>
        public static string Lang => Languages.Current;

        public static bool IsRussian => Lang == Languages.Russian;

        /// <summary>Joriy tilda so'm yozuvi (so'm / сўм / сум).</summary>
        public static string Sum => DisplayCultures.Sum(Lang);
    }
}
