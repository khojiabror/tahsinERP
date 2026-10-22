using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.I18n;
using tahsinERP.Localization;

namespace tahsinERP.Tests.Localization
{
    [TestClass]
    public class LocalizationTests
    {
        internal static string SolutionDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "tahsinERP.sln topilmadi");
            return dir.FullName;
        }

        internal static string[] Keys(string path)
        {
            return XDocument.Load(path).Root.Elements("data").Select(d => (string)d.Attribute("name")).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        /// <summary>ERP'da ishlatiladigan barcha kalitlar: ErpStrings + DstStrings + umumiy atamalar.</summary>
        internal static HashSet<string> AllErpKeys()
        {
            var root = SolutionDir();
            return new HashSet<string>(
                Keys(Path.Combine(root, "tahsinERP", "Resources", "ErpStrings.resx"))
                .Concat(Keys(Path.Combine(root, "tahsinERP", "Areas", "Distribution", "Resources", "DstStrings.resx")))
                .Concat(Keys(Path.Combine(root, "tahsinERP.Localization", "Resources", "Common.resx"))));
        }

        private static void AssertSameKeys(string neutral, string ru)
        {
            var uz = Keys(neutral);
            var r = Keys(ru);
            Assert.IsTrue(uz.Length > 0, neutral);
            CollectionAssert.AreEqual(uz, r, Path.GetFileName(neutral) + ": uz va ru kalitlari bir xil bo'lishi kerak: farq = " +
                string.Join(", ", uz.Except(r).Concat(r.Except(uz))));
        }

        [TestMethod]
        public void ErpAndCommon_EveryKeyHasRussian()
        {
            var root = SolutionDir();
            AssertSameKeys(Path.Combine(root, "tahsinERP", "Resources", "ErpStrings.resx"), Path.Combine(root, "tahsinERP", "Resources", "ErpStrings.ru.resx"));
            AssertSameKeys(Path.Combine(root, "tahsinERP.Localization", "Resources", "Common.resx"), Path.Combine(root, "tahsinERP.Localization", "Resources", "Common.ru.resx"));
        }

        [TestMethod]
        public void ResourceValues_HaveMatchingPlaceholders()
        {
            var root = SolutionDir();
            foreach (var pair in new[]
            {
                new[] { "tahsinERP/Resources/ErpStrings.resx", "tahsinERP/Resources/ErpStrings.ru.resx" },
                new[] { "tahsinERP.Localization/Resources/Common.resx", "tahsinERP.Localization/Resources/Common.ru.resx" }
            })
            {
                var uz = Values(Path.Combine(root, pair[0]));
                var ru = Values(Path.Combine(root, pair[1]));
                var bad = uz.Where(kv => ru.ContainsKey(kv.Key) && Placeholders(kv.Value) != Placeholders(ru[kv.Key])).Select(kv => kv.Key).ToList();
                Assert.AreEqual(0, bad.Count, pair[0] + ": {n} o'rinbosarlari mos emas: " + string.Join(", ", bad));
            }
        }

        private static Dictionary<string, string> Values(string path)
        {
            return XDocument.Load(path).Root.Elements("data").ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));
        }

        private static string Placeholders(string s)
        {
            return string.Join(",", Regex.Matches(s ?? "", @"\{(\d+)[^}]*\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));
        }

        /// <summary>Kodda ishlatilgan har bir L.T("...") / DstLocalization.T("...") kaliti resx'da bor.</summary>
        [TestMethod]
        public void EveryKeyUsedInErpCode_Exists()
        {
            var keys = AllErpKeys();
            var project = Path.Combine(SolutionDir(), "tahsinERP");
            var skip = new[] { "\\obj\\", "\\bin\\", "\\app-assets\\", "\\Scripts\\", "\\packages\\" };
            var files = Directory.GetFiles(project, "*.*", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml")) && !skip.Any(f.Contains) && !f.Contains("\\I18n\\"));
            var rx = new Regex("(?:\\bL|DstLocalization|\\bL\\.Translator)\\.(?:T|Js|TFor)\\((?:[^,()\"]+,\\s*)?\"([A-Za-z0-9_.]+)\"");
            var missing = files.SelectMany(f => rx.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[1].Value + " (" + Path.GetFileName(f) + ")")
                                                 .Where(k => !k.Contains("_ (") && !keys.Contains(k.Substring(0, k.IndexOf(' ')))))   // "Prefix_" + kod — dinamik
                .Distinct().ToList();
            Assert.AreEqual(0, missing.Count, "resx'da yo'q: " + string.Join(", ", missing.Take(50)));
        }

        [TestMethod]
        public void Translator_FallsBack_AndNeverThrows()
        {
            var t = L.Translator;
            Assert.AreEqual("Saqlash", t.Get("Save", "uz"));
            Assert.AreEqual("Сохранить", t.Get("Save", "ru"));
            Assert.AreEqual("Сақлаш", t.Get("Save", "oz"));
            Assert.AreEqual("Saqlash", t.Get("Save", "en"), "noma'lum til — o'zbekcha");
            Assert.AreEqual("No_Such_Key_X", t.Get("No_Such_Key_X", "ru"), "yo'q kalit — kalitning o'zi");
            Assert.AreEqual("\"{0}\" maydoni to'ldirilishi shart.", t.TFor("uz", "Val_Required"), "argumentsiz — formatlanmaydi");
            Assert.AreEqual("Поле «Имя» обязательно для заполнения.", t.TFor("ru", "Val_Required", "Имя"));
        }

        [TestMethod]
        public void T_FollowsCurrentUiCulture()
        {
            var saved = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = Languages.CultureFor("ru");
                Assert.AreEqual("ru", L.Lang);
                Assert.AreEqual("Обязательное поле.", L.T("Val_RequiredShort"));
                Thread.CurrentThread.CurrentUICulture = Languages.CultureFor("oz");
                Assert.AreEqual("oz", L.Lang);
                Assert.AreEqual("Тўлдирилиши шарт.", L.T("Val_RequiredShort"));
                // distributsiya kaliti ham shu tizimdan
                Assert.AreEqual("Дистрибуция", L.TFor("ru", "Module_Title"));
                Assert.AreEqual("Дистрибуция", DstLocalization.TFor("ru", "Module_Title"));
                Assert.AreEqual("Дистрибуция", DstLocalization.TFor("oz", "Module_Title"));
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = saved;
            }
        }

        [TestMethod]
        public void Languages_Normalize()
        {
            Assert.AreEqual("ru", Languages.Normalize("ru-RU"));
            Assert.AreEqual("oz", Languages.Normalize("uz-Cyrl"));
            Assert.AreEqual("uz", Languages.Normalize("uz-Latn-UZ"));
            Assert.AreEqual("uz", Languages.Normalize(null));
            Assert.IsNull(Languages.TryNormalize("en"));
            Assert.AreEqual("oz", Languages.FromCulture(CultureInfo.GetCultureInfo("uz-Cyrl-UZ")));
            Assert.AreEqual("uz", Languages.FromCulture(CultureInfo.GetCultureInfo("uz-UZ")));
        }

        [TestMethod]
        public void Transliteration_FollowsUzbekRules()
        {
            var cases = new Dictionary<string, string>
            {
                ["O'zbekiston"] = "Ўзбекистон",
                ["Yo'q, ma'lumot saqlanmadi"] = "Йўқ, маълумот сақланмади",
                ["g'alla"] = "ғалла",
                ["Litsenziya va inventarizatsiya"] = "Лицензия ва инвентаризация",
                ["Funksiya"] = "Функция",
                ["Yangi yetkazib beruvchi"] = "Янги етказиб берувчи",
                ["Choy, shakar"] = "Чой, шакар",
                ["Eslatma: poeziya"] = "Эслатма: поэзия",
                ["o'tsa"] = "ўтса",
                ["Shartnoma raqami: {0}"] = "Шартнома рақами: {0}",
                ["<b>Jami:</b> {0:N2} so'm"] = "<b>Жами:</b> {0:N2} сўм",
                ["ERP va PDF hisobot"] = "ERP ва PDF ҳисобот",
                ["Excel'ga eksport"] = "Excel'га экспорт",
                ["QQS bilan"] = "ҚҚС билан",
                ["tahsinERP tizimi"] = "tahsinERP тизими",
                ["SHARTNOMA"] = "ШАРТНОМА",
                ["Ta'minotchilar"] = "Таъминотчилар",
                ["&nbsp;Kirim"] = "&nbsp;Кирим",
                ["info@tahsinerp.uz"] = "info@tahsinerp.uz",
            };
            foreach (var c in cases)
                Assert.AreEqual(c.Value, UzTransliterator.ToCyrillic(c.Key), c.Key);
        }

        [TestMethod]
        public void DisplayCulture_KeepsParsingFormats_ChangesNames()
        {
            var baseCulture = CultureInfo.GetCultureInfo("uz-UZ");
            foreach (var lang in Languages.All)
            {
                var c = DisplayCultures.For(lang, baseCulture);
                Assert.AreEqual(baseCulture.NumberFormat.NumberDecimalSeparator, c.NumberFormat.NumberDecimalSeparator, lang);
                Assert.AreEqual(baseCulture.NumberFormat.NumberGroupSeparator, c.NumberFormat.NumberGroupSeparator, lang);
                Assert.AreEqual(baseCulture.DateTimeFormat.ShortDatePattern, c.DateTimeFormat.ShortDatePattern, lang);
                Assert.AreEqual(new DateTime(2026, 3, 5), DateTime.Parse(new DateTime(2026, 3, 5).ToString("d", c), c), lang);
            }
            Assert.AreEqual("Март", DisplayCultures.For("ru", baseCulture).DateTimeFormat.MonthNames[2]);
            Assert.AreEqual("сум", DisplayCultures.For("ru", baseCulture).NumberFormat.CurrencySymbol);
            Assert.AreEqual("сўм", DisplayCultures.For("oz", baseCulture).NumberFormat.CurrencySymbol);

            // distributsiya formasi madaniyati ("." o'nlik) ham saqlanadi
            var form = DisplayCultures.For("ru", DstLocalization.FormCulture);
            Assert.AreEqual(".", form.NumberFormat.NumberDecimalSeparator);
            Assert.AreEqual(1234.5m, decimal.Parse("1234.5", form));
        }
    }
}
