using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services.Bank;

/// <summary>Ko'chirmadagi bitta to'lov (tahlildan keyin, moslashdan oldin).</summary>
public sealed record ParsedBankLine(
    int LineNo, string? DocNumber, DateOnly Date, long AmountTiyin,
    string? PayerName, string? PayerInn, string? PayerAccount,
    string? ReceiverInn, string? ReceiverAccount, string? Purpose,
    /// <summary>Fayldan aniq bo'lsa: true — kirim, false — chiqim (CSV'da manfiy summa). Null — hisob/STIR bo'yicha aniqlanadi.</summary>
    bool? IsIncoming);

public sealed record ParsedStatement(BankImportFormat Format, IReadOnlyList<string> Accounts, DateOnly? From, DateOnly? To,
    IReadOnlyList<ParsedBankLine> Lines, IReadOnlyList<string> Warnings);

/// <summary>
/// Bank ko'chirmasi o'quvchisi (08_STAGE4.md 2):
/// <list type="bullet">
/// <item>1C «Клиент-Банк» almashinuv fayli (<c>1CClientBankExchange</c>, Windows-1251 yoki UTF-8): <c>СекцияДокумент</c> … <c>КонецДокумента</c>.</item>
/// <item>CSV (<c>;</c>, <c>,</c> yoki TAB) va XLSX: birinchi qator — sarlavha; kerakli ustunlar: sana, summa, to'lovchi STIR, to'lov maqsadi;
///       ixtiyoriy: hujjat №, to'lovchi nomi, to'lovchi hisobi. Sarlavha nomlari o'zbek/rus/ingliz tilida tanib olinadi.</item>
/// </list>
/// Tashqi kutubxonasiz (XLSX — ZIP + XML). Xato bo'lsa <see cref="DomainException"/>.
/// </summary>
public static class BankStatementParser
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxLines = 5000;

    static BankStatementParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static ParsedStatement Parse(string fileName, byte[] content)
    {
        if (content.Length == 0) throw new DomainException("Fayl bo'sh.");
        if (content.Length > MaxBytes) throw new DomainException($"Fayl juda katta (ko'pi bilan {MaxBytes / 1024 / 1024} MB).");
        string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (content.Length > 4 && content[0] == 'P' && content[1] == 'K' && content[2] == 3 && content[3] == 4)
            return ParseXlsx(content);
        if (ext is ".xlsx") throw new DomainException("XLSX fayl o'qilmadi (ZIP emas). Faylni Excel'da qayta saqlang.");
        if (ext is ".xls") throw new DomainException("Eski .xls formati qo'llanmaydi — Excel'da .xlsx yoki CSV qilib saqlang.");
        string text = Decode(content);
        if (text.TrimStart('﻿', ' ', '\r', '\n').StartsWith("1CClientBankExchange", StringComparison.Ordinal))
            return Parse1C(text);
        return ParseCsv(text);
    }

    /// <summary>UTF-8 (BOM yoki to'g'ri ketma-ketlik), aks holda Windows-1251 (1C fayllari odatda shunday).</summary>
    public static string Decode(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(1251).GetString(content); }
    }

    // ── 1C ───────────────────────────────────────────────────────────────────────────────────────────────────────

    public static ParsedStatement Parse1C(string text)
    {
        var accounts = new List<string>();
        var warnings = new List<string>();
        var lines = new List<ParsedBankLine>();
        DateOnly? from = null, to = null;
        Dictionary<string, string>? doc = null;
        int docNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            int eq = line.IndexOf('=');
            string key = (eq < 0 ? line : line[..eq]).Trim();
            string value = eq < 0 ? "" : line[(eq + 1)..].Trim();
            if (key == "СекцияДокумент") { doc = new Dictionary<string, string>(StringComparer.Ordinal); continue; }
            if (key == "КонецДокумента")
            {
                if (doc is not null)
                {
                    docNo++;
                    var parsed = From1CDoc(docNo, doc, warnings);
                    if (parsed is not null) lines.Add(parsed);
                    if (lines.Count > MaxLines) throw new DomainException($"Ko'chirmada {MaxLines} tadan ko'p hujjat — davrni bo'lib yuklang.");
                }
                doc = null;
                continue;
            }
            if (doc is not null)
            {
                // Takrorlangan kalit (masalan Плательщик1, НазначениеПлатежа1) — birinchisi qoladi; ko'p qatorli maqsad qo'shib yoziladi.
                if (doc.TryGetValue(key, out var prev)) { if (key.StartsWith("НазначениеПлатежа", StringComparison.Ordinal)) doc[key] = prev + " " + value; }
                else doc[key] = value;
                continue;
            }
            switch (key)
            {
                case "РасчСчет": if (value.Length > 0 && !accounts.Contains(value)) accounts.Add(value); break;
                case "ДатаНачала": from = ParseDate(value); break;
                case "ДатаКонца": to = ParseDate(value); break;
            }
        }
        if (doc is not null) warnings.Add("Oxirgi hujjat «КонецДокумента» bilan yopilmagan — e'tiborsiz qoldirildi.");
        if (lines.Count == 0 && warnings.Count == 0) warnings.Add("Faylda to'lov hujjati (СекцияДокумент) topilmadi.");
        return new ParsedStatement(BankImportFormat.OneC, accounts, from, to, lines, warnings);
    }

    private static ParsedBankLine? From1CDoc(int no, Dictionary<string, string> d, List<string> warnings)
    {
        string? Get(params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return null;
        }
        string? number = Get("Номер");
        DateOnly? date = ParseDate(Get("ДатаПоступило") ?? Get("Дата") ?? Get("ДатаСписано"));
        if (date is null) { warnings.Add($"{no}-hujjat ({number}): sana o'qilmadi — o'tkazib yuborildi."); return null; }
        if (!TryParseAmount(Get("Сумма"), out long amount) || amount == 0)
        { warnings.Add($"{no}-hujjat ({number}): summa o'qilmadi — o'tkazib yuborildi."); return null; }
        string? purpose = string.Join(" ", d.Where(kv => kv.Key.StartsWith("НазначениеПлатежа", StringComparison.Ordinal)).OrderBy(kv => kv.Key)
            .Select(kv => kv.Value).Where(v => v.Length > 0));
        bool? incoming = Get("ДатаПоступило") is not null ? true : Get("ДатаСписано") is not null ? false : null;
        return new ParsedBankLine(no, number, date.Value, Math.Abs(amount),
            Get("Плательщик1", "Плательщик"), Digits(Get("ПлательщикИНН")), Digits(Get("ПлательщикСчет", "ПлательщикРасчСчет")),
            Digits(Get("ПолучательИНН")), Digits(Get("ПолучательСчет", "ПолучательРасчСчет")),
            string.IsNullOrWhiteSpace(purpose) ? null : purpose, incoming);
    }

    // ── CSV / XLSX ───────────────────────────────────────────────────────────────────────────────────────────────

    private enum Col { Date, Amount, Inn, Purpose, Number, Payer, Account }

    // Tartib muhim: "инн плательщика" — STIR (to'lovchi emas), "счет плательщика" — hisob.
    private static readonly (Col Col, string[] Keys)[] HeaderRules =
    [
        (Col.Inn, ["stir", "inn", "инн", "tin", "jshshir", "pinfl"]),
        (Col.Account, ["hisobraqam", "hisobi", "hisobvaraq", "account", "счет", "счёт", "rs"]),
        (Col.Date, ["sana", "date", "дата"]),
        (Col.Amount, ["summa", "amount", "сумма", "kirim", "credit", "кредит", "приход"]),
        (Col.Purpose, ["maqsad", "purpose", "назначение", "details", "izoh", "description"]),
        (Col.Number, ["raqam", "number", "номер", "hujjat", "doc", "№", "no"]),
        (Col.Payer, ["tolovchi", "payer", "плательщик", "kontragent", "контрагент", "name", "nomi"]),
    ];

    /// <summary>Juda qisqa kalitlar faqat to'liq mos kelganda (aks holda "nomi" → "no", "address" → "rs" bo'lib qolardi).</summary>
    private static readonly HashSet<string> ExactOnly = ["no", "rs", "tin", "№", "doc", "name"];

    private static string NormHeader(string h) => new string((h ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '№').ToArray());

    private static Dictionary<Col, int> MapHeader(IReadOnlyList<string> header)
    {
        var map = new Dictionary<Col, int>();
        for (int i = 0; i < header.Count; i++)
        {
            string h = NormHeader(header[i]);
            if (h.Length == 0) continue;
            foreach (var (col, keys) in HeaderRules)
            {
                if (map.ContainsKey(col)) continue;
                bool hit = keys.Any(k => ExactOnly.Contains(k) ? h == k : h.Contains(k, StringComparison.Ordinal));
                if (hit) { map[col] = i; break; }
            }
        }
        return map;
    }

    private static ParsedStatement FromTable(BankImportFormat format, List<List<string>> rows, Dictionary<(int Row, int Col), double>? numericDates = null)
    {
        int headerRow = -1;
        Dictionary<Col, int> map = [];
        for (int r = 0; r < Math.Min(rows.Count, 15); r++)
        {
            var m = MapHeader(rows[r]);
            if (m.ContainsKey(Col.Date) && m.ContainsKey(Col.Amount)) { headerRow = r; map = m; break; }
        }
        if (headerRow < 0)
            throw new DomainException("Sarlavha qatori topilmadi: kamida «Sana» va «Summa» ustunlari bo'lsin (ixtiyoriy: STIR, To'lov maqsadi, Hujjat №, To'lovchi, Hisob).");
        var warnings = new List<string>();
        if (!map.ContainsKey(Col.Inn)) warnings.Add("«STIR» ustuni yo'q — moslash faqat hisob-faktura raqami bo'yicha.");
        if (!map.ContainsKey(Col.Purpose)) warnings.Add("«To'lov maqsadi» ustuni yo'q — moslash faqat STIR + summa bo'yicha.");
        var lines = new List<ParsedBankLine>();
        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Cell(Col c) => map.TryGetValue(c, out int i) && i < row.Count ? row[i].Trim() : "";
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            int lineNo = r + 1;
            DateOnly? date = numericDates is not null && map.TryGetValue(Col.Date, out int dc) && numericDates.TryGetValue((r, dc), out double oa)
                ? DateOnly.FromDateTime(DateTime.FromOADate(oa)) : ParseDate(Cell(Col.Date));
            string amountText = Cell(Col.Amount);
            if (date is null && amountText.Length == 0) continue; // jami / bo'sh qatorlar
            if (date is null) { warnings.Add($"{lineNo}-qator: sana o'qilmadi — o'tkazib yuborildi."); continue; }
            if (!TryParseAmount(amountText, out long amount) || amount == 0) { warnings.Add($"{lineNo}-qator: summa o'qilmadi — o'tkazib yuborildi."); continue; }
            string? Opt(Col c) => Cell(c) is { Length: > 0 } v ? v : null;
            lines.Add(new ParsedBankLine(lineNo, Opt(Col.Number), date.Value, Math.Abs(amount), Opt(Col.Payer), Digits(Opt(Col.Inn)), Digits(Opt(Col.Account)),
                null, null, Opt(Col.Purpose), amount < 0 ? false : null));
            if (lines.Count > MaxLines) throw new DomainException($"Faylda {MaxLines} tadan ko'p qator — davrni bo'lib yuklang.");
        }
        return new ParsedStatement(format, [], lines.Count == 0 ? null : lines.Min(l => l.Date), lines.Count == 0 ? null : lines.Max(l => l.Date), lines, warnings);
    }

    public static ParsedStatement ParseCsv(string text)
    {
        var physical = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('﻿').Split('\n');
        string sample = physical.FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        char sep = new[] { ';', '\t', ',' }.OrderByDescending(c => sample.Count(x => x == c)).First();
        var rows = new List<List<string>>();
        var cur = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        // RFC 4180: qo'shtirnoq ichida ajratgich va yangi qator bo'lishi mumkin.
        string all = string.Join("\n", physical);
        for (int i = 0; i < all.Length; i++)
        {
            char c = all[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < all.Length && all[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == sep) { cur.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { cur.Add(cell.ToString()); cell.Clear(); rows.Add(cur); cur = []; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); rows.Add(cur); }
        return FromTable(BankImportFormat.Csv, rows);
    }

    public static ParsedStatement ParseXlsx(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var shared = new List<string>();
            if (zip.GetEntry("xl/sharedStrings.xml") is { } ss)
            {
                using var s = ss.Open();
                shared = XDocument.Load(s).Root!.Elements(ns + "si")
                    .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToList();
            }
            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")
                ?? zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal))
                    .OrderBy(e => e.FullName, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new DomainException("XLSX faylda varaq topilmadi.");
            using var sheet = sheetEntry.Open();
            var rows = new List<List<string>>();
            var numeric = new Dictionary<(int, int), double>();
            foreach (var rowEl in XDocument.Load(sheet).Root!.Descendants(ns + "row"))
            {
                var row = new List<string>();
                int r = rows.Count;
                int next = 0;
                foreach (var c in rowEl.Elements(ns + "c"))
                {
                    int col = ColumnIndex((string?)c.Attribute("r")) ?? next;
                    while (row.Count < col) row.Add("");
                    string type = (string?)c.Attribute("t") ?? "n";
                    string v = (string?)c.Element(ns + "v") ?? "";
                    string value = type switch
                    {
                        "s" => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int idx) && idx < shared.Count ? shared[idx] : "",
                        "inlineStr" => string.Concat(c.Descendants(ns + "t").Select(t => t.Value)),
                        _ => v,
                    };
                    if (type == "n" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d > 20000 && d < 80000)
                        numeric[(r, col)] = d; // sana bo'lishi mumkin (OADate); faqat «Sana» ustunida ishlatiladi
                    row.Add(value);
                    next = col + 1;
                }
                rows.Add(row);
                if (rows.Count > MaxLines + 20) break;
            }
            return FromTable(BankImportFormat.Xlsx, rows, numeric);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            throw new DomainException("XLSX fayl o'qilmadi: " + ex.Message);
        }
    }

    private static int? ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return null;
        int n = 0, i = 0;
        for (; i < cellRef.Length && char.IsAsciiLetter(cellRef[i]); i++) n = n * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
        return i == 0 ? null : n - 1;
    }

    // ── Umumiy ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] DateFormats =
        ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "yyyyMMdd"];

    public static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string t = s.Trim();
        int space = t.IndexOfAny([' ', 'T']);
        if (space > 0) t = t[..space];
        return DateOnly.TryParseExact(t, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year is > 2000 and < 2100 ? d : null;
    }

    /// <summary>"1 234 567,89", "1234567.89", "1,234,567.89", "-500" → tiyin (ishora saqlanadi). Kasr 2 xonadan ko'p — xato.</summary>
    public static bool TryParseAmount(string? s, out long tiyin)
    {
        tiyin = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        string t = new string(s.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());
        if (t.Length == 0) return false;
        int lastComma = t.LastIndexOf(','), lastDot = t.LastIndexOf('.');
        int dec = Math.Max(lastComma, lastDot);
        if (dec >= 0 && (t.Length - dec - 1 is 1 or 2))
            t = t[..dec].Replace(",", "").Replace(".", "") + "." + t[(dec + 1)..];
        else
            t = t.Replace(",", "").Replace(".", "");
        if (!decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal som)) return false;
        decimal x = som * 100m;
        if (x != decimal.Truncate(x) || Math.Abs(x) > long.MaxValue / 4) return false;
        tiyin = (long)x;
        return true;
    }

    private static string? Digits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string d = new string(s.Where(char.IsAsciiDigit).ToArray());
        return d.Length == 0 ? null : d;
    }
}
