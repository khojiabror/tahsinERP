using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace tahsinERP.Localization
{
    /// <summary>
    /// O'zbek lotin -> kirill transliteratsiyasi (1995-yil imlo qoidalari bo'yicha, interfeys matnlari uchun).
    /// Tegilmaydi: {0}/{0:N2} o'rinbosarlari, HTML teglari va &amp;entity; lari, katta harfli lotin qisqartmalar
    /// (ERP, PDF, SKU — o'zbekcha qisqartmalar QQS, STIR... bundan mustasno) va <see cref="KeepWords"/>.
    /// Noto'g'ri chiqqan so'z uchun — kalitni .uz-Cyrl.resx da alohida yozing.
    /// </summary>
    public static class UzTransliterator
    {
        /// <summary>Lotincha qoladigan so'zlar (brendlar, texnik atamalar).</summary>
        private static readonly HashSet<string> KeepWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tahsinERP", "Telegram", "Excel", "Word", "Google", "Yandex", "Click", "Payme", "Uzum", "WhatsApp",
            "email", "e-mail", "Mini", "App", "Windows", "Android", "iOS", "Chrome", "OK"
        };

        /// <summary>Katta harfda yozilsa ham transliteratsiya qilinadigan o'zbekcha qisqartmalar.</summary>
        private static readonly HashSet<string> UzbekAcronyms = new HashSet<string>(StringComparer.Ordinal)
        {
            "QQS", "STIR", "JSHSHIR", "MCHJ", "AJ", "XK", "YATT", "XT", "DSQ", "SHJ", "TTN", "HR", "AKT", "YO'Q", "HA", "VA"
        };

        /// <summary>Qoidaga bo'ysunmaydigan o'zlashma so'zlar (kichik harfda; registr saqlanadi).</summary>
        private static readonly Dictionary<string, string> WordOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = "дашборд", ["dashboardi"] = "дашборди", ["dashboardga"] = "дашбордга",
            ["kanban"] = "канбан", ["traction"] = "трекшн", ["coverage"] = "ковераж", ["engineering"] = "инжиниринг",
            ["provisioning"] = "провижининг", ["online"] = "онлайн", ["offline"] = "офлайн"
        };

        private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();

        /// <summary>Kesh bilan (resurs matnlari cheklangan to'plam).</summary>
        public static string ToCyrillicCached(string latin)
        {
            if (string.IsNullOrEmpty(latin)) return latin;
            if (Cache.Count > 50000) Cache.Clear();
            return Cache.GetOrAdd(latin, ToCyrillic);
        }

        public static string ToCyrillic(string latin)
        {
            if (string.IsNullOrEmpty(latin)) return latin;
            var sb = new StringBuilder(latin.Length);
            int i = 0;
            while (i < latin.Length)
            {
                char c = latin[i];
                // {0}, {0:N2}, {{ — o'zgarmaydi
                if (c == '{')
                {
                    int end = latin.IndexOf('}', i);
                    if (end > i) { sb.Append(latin, i, end - i + 1); i = end + 1; continue; }
                }
                // HTML teg
                if (c == '<')
                {
                    int end = latin.IndexOf('>', i);
                    if (end > i && end - i < 400 && i + 1 < latin.Length && (char.IsLetter(latin[i + 1]) || latin[i + 1] == '/' || latin[i + 1] == '!'))
                    { sb.Append(latin, i, end - i + 1); i = end + 1; continue; }
                }
                // &amp; &nbsp; &#39;
                if (c == '&')
                {
                    int end = latin.IndexOf(';', i);
                    if (end > i && end - i <= 8) { sb.Append(latin, i, end - i + 1); i = end + 1; continue; }
                }
                if (IsLatinLetter(c))
                {
                    int start = i;
                    // apostrof faqat ikki harf orasida so'z ichida (o'z, g'oya, ma'lumot); so'z oxiridagisi — qo'shtirnoq bo'lishi mumkin
                    while (i < latin.Length && (IsLatinLetter(latin[i]) || (IsApostrophe(latin[i]) && i > start && i + 1 < latin.Length && IsLatinLetter(latin[i + 1]))))
                        i++;
                    string word = latin.Substring(start, i - start);
                    // URL/fayl nomi/e-pochta bo'lagi: oldida yoki ortida . / @ _ bo'lsa — tegmaymiz
                    bool technical = (start > 0 && (latin[start - 1] == '.' || latin[start - 1] == '/' || latin[start - 1] == '@' || latin[start - 1] == '_' || latin[start - 1] == '#' || latin[start - 1] == '\\'))
                                     || (i < latin.Length && (latin[i] == '@' || latin[i] == '_' || latin[i] == '/' || latin[i] == '\\' || (latin[i] == '.' && i + 1 < latin.Length && IsLatinLetter(latin[i + 1]))));
                    sb.Append(technical || Keep(word) ? word : WithSuffix(word));
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // Excel'ga, PDF'dan, ERP'da — lotincha asos o'zgarmaydi, o'zbekcha qo'shimcha kirillga o'tadi
        private static string WithSuffix(string word)
        {
            for (int k = 1; k < word.Length - 1; k++)
            {
                if (!IsApostrophe(word[k]) || IsOG(word[k - 1]) && !Keep(word.Substring(0, k))) continue;
                var stem = word.Substring(0, k);
                if (Keep(stem)) return stem + word[k] + Word(word.Substring(k + 1));
            }
            return Word(word);
        }

        private static bool Keep(string word)
        {
            if (KeepWords.Contains(word)) return true;
            // lotin qisqartma (ERP, PDF, SKU, ID, QR) yoki aralash registr (tahsinERP, iPhone) — o'zgarmaydi
            int upper = 0, lower = 0;
            foreach (var ch in word)
            {
                if (char.IsUpper(ch)) upper++;
                else if (char.IsLower(ch)) lower++;
            }
            if (upper >= 2 && lower == 0) return word.Length <= 5 && !UzbekAcronyms.Contains(word.Replace('ʻ', '\'').Replace('‘', '\'').Replace('’', '\'').ToUpperInvariant());
            if (upper >= 2 && lower > 0 && !char.IsUpper(word[0])) return true;           // tahsinERP
            if (upper >= 2 && lower > 0 && HasInnerUpperAfterLower(word) && !IsUzDigraphCase(word)) return true; // WhatsApp, PowerBI
            return false;
        }

        private static bool HasInnerUpperAfterLower(string w)
        {
            for (int k = 1; k < w.Length; k++)
                if (char.IsUpper(w[k]) && char.IsLower(w[k - 1])) return true;
            return false;
        }

        // "ShChOT" kabi emas, lekin "YaTT", "JShShIR" — o'zbekcha qisqartmalar aralash registrda yoziladi
        private static bool IsUzDigraphCase(string w)
        {
            return UzbekAcronyms.Contains(w.ToUpperInvariant());
        }

        private static string Word(string w)
        {
            if (WordOverrides.TryGetValue(w, out var fixedWord))
                return IsAllUpper(w) ? fixedWord.ToUpperInvariant()
                     : char.IsUpper(w[0]) ? char.ToUpperInvariant(fixedWord[0]) + fixedWord.Substring(1) : fixedWord;
            var sb = new StringBuilder(w.Length);
            bool allUpper = IsAllUpper(w);
            int n = w.Length;
            for (int i = 0; i < n; i++)
            {
                char c = w[i];
                char lc = char.ToLowerInvariant(c);
                bool up = char.IsUpper(c);
                char next = i + 1 < n ? char.ToLowerInvariant(w[i + 1]) : '\0';
                bool nextApos = i + 1 < n && IsApostrophe(w[i + 1]);
                // ikkinchi harf ham katta bo'lsa (SH) yoki butun so'z katta bo'lsa — kirillda ham katta
                Func<string, string> cs = s => up ? (allUpper || s.Length == 1 ? s.ToUpperInvariant() : char.ToUpperInvariant(s[0]) + s.Substring(1)) : s;

                if ((lc == 'o' || lc == 'g') && nextApos)
                {
                    sb.Append(cs(lc == 'o' ? "ў" : "ғ"));
                    i++;
                    continue;
                }
                if (IsApostrophe(c))
                {
                    sb.Append(allUpper ? 'Ъ' : 'ъ');
                    continue;
                }
                if (lc == 's' && next == 'h')
                {
                    sb.Append(cs("ш")); i++; continue;
                }
                if (lc == 'c' && next == 'h')
                {
                    sb.Append(cs("ч")); i++; continue;
                }
                if (lc == 't' && next == 's' && TsIsTse(w, i))
                {
                    sb.Append(cs("ц")); i++; continue;
                }
                // funksiya, aksiya, reaksiya -> функция, акция, реакция
                if (lc == 's' && i > 0 && char.ToLowerInvariant(w[i - 1]) == 'k' && i + 2 < n && char.ToLowerInvariant(w[i + 1]) == 'i'
                    && (char.ToLowerInvariant(w[i + 2]) == 'y' || char.ToLowerInvariant(w[i + 2]) == 'o'))
                {
                    sb.Append(cs("ц")); continue;
                }
                if (lc == 'y' && (next == 'o' || next == 'u' || next == 'a' || next == 'e')
                    && !(next == 'o' && i + 2 < n && IsApostrophe(w[i + 2])))
                {
                    string y = next == 'o' ? "ё" : next == 'u' ? "ю" : next == 'a' ? "я" : "е";
                    sb.Append(cs(y)); i++; continue;
                }
                if (lc == 'e')
                {
                    bool afterVowel = i > 0 && IsVowel(char.ToLowerInvariant(w[i - 1]));
                    sb.Append(cs(i == 0 || afterVowel ? "э" : "е"));
                    continue;
                }
                string m = Map(lc);
                sb.Append(m == null ? c.ToString() : cs(m));
            }
            return sb.ToString();
        }

        // litsenziya -> лицензия, tsex -> цех, funksiya; lekin o'tsa -> ўтса
        private static bool TsIsTse(string w, int i)
        {
            if (i == 0) return true;
            int k = i + 2;
            if (k >= w.Length) return false;
            char a = char.ToLowerInvariant(w[k]);
            if (a == 'e') return true;
            if (a == 'i' && k + 1 < w.Length)
            {
                char b = char.ToLowerInvariant(w[k + 1]);
                return b == 'y' || b == 'o' || b == 'a' || b == 'e' || b == 'p' || b == 'k' || b == 'f';
            }
            return false;
        }

        private static string Map(char c)
        {
            switch (c)
            {
                case 'a': return "а";
                case 'b': return "б";
                case 'c': return "с";
                case 'd': return "д";
                case 'f': return "ф";
                case 'g': return "г";
                case 'h': return "ҳ";
                case 'i': return "и";
                case 'j': return "ж";
                case 'k': return "к";
                case 'l': return "л";
                case 'm': return "м";
                case 'n': return "н";
                case 'o': return "о";
                case 'p': return "п";
                case 'q': return "қ";
                case 'r': return "р";
                case 's': return "с";
                case 't': return "т";
                case 'u': return "у";
                case 'v': return "в";
                case 'w': return "в";
                case 'x': return "х";
                case 'y': return "й";
                case 'z': return "з";
                default: return null;
            }
        }

        private static bool IsAllUpper(string w)
        {
            bool any = false;
            foreach (var ch in w)
            {
                if (char.IsLower(ch)) return false;
                if (char.IsUpper(ch)) any = true;
            }
            return any && w.Length > 1;
        }

        private static bool IsVowel(char c) => c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';

        private static bool IsOG(char c) => c == 'o' || c == 'O' || c == 'g' || c == 'G';

        private static bool IsLatinLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool IsApostrophe(char c) => c == '\'' || c == 'ʻ' || c == 'ʼ' || c == '‘' || c == '’' || c == '`';
    }
}
