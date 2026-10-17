using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using tahsinERP.Models;
using tahsinERP.Services.Documents;

namespace tahsinERP.Services
{
    /// <summary>
    /// DOC_TEMPLATES'dagi .docx shablonni to'ldiradi.
    ///
    /// Avvalgi muammolar: maydonlar paragrafning barcha run'lari birlashtirilib, matn eski uzunliklar bo'yicha
    /// "qayta taqsimlanardi" — formatlash surilardi; matn har doim oddiy matnga aylantirilardi (qalin, ro'yxat
    /// yo'qolardi); to'ldirilmagan {{ITEM_NAME_5}} kabi maydonlar hujjatda qolardi; nomlar kod ichida
    /// qattiq yozilgan edi. Endi: maydon qaysi run'dan boshlansa — o'sha run formati bilan almashtiriladi
    /// (Word maydonni bir necha run'ga bo'lsa ham), alohida paragrafdagi matn/jadval maydonlari formatlangan
    /// blok bilan almashtiriladi, katalogdagi lekin bo'sh maydonlar tozalanadi, noma'lumlari hisobotga chiqadi.
    /// </summary>
    public class DocumentTemplateService : IDocumentTemplateService
    {
        public const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        // Shablonlar kam o'zgaradi — har hujjat yaratishda blob qayta o'qilmasin (DOC-P07)
        private static readonly MemoryCache TemplateCache = new MemoryCache("tahsinERP.DocTemplates");
        private static readonly TimeSpan TemplateTtl = TimeSpan.FromMinutes(10);

        // {{KEY}} (asosiy) va eski {KEY} ko'rinishi
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{\s*(?<k>[^{}]+?)\s*\}\}|\{(?<s>[A-Za-z][A-Za-z0-9_. ]*)\}", RegexOptions.Compiled);

        private sealed class CachedTemplate
        {
            public string DocTypeCode;
            public string FileExtension;
            public string MimeType;
            public byte[] FileContent;
        }

        public GeneratedDocument Generate(int templateId, TemplateData data)
        {
            var template = GetTemplate(templateId);
            if (!string.Equals(template.DocTypeCode, data.DocTypeCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Shablon boshqa hujjat turiga tegishli.");

            var result = Fill(template.FileContent, data);
            result.FileExtension = string.IsNullOrEmpty(template.FileExtension) ? ".docx" : template.FileExtension;
            result.MimeType = string.IsNullOrEmpty(template.MimeType) ? DocxMime : template.MimeType;
            return result;
        }

        /// <summary>Shablon baytlarini ma'lumot bilan to'ldiradi (bazaga murojaat qilmaydi; manba o'zgarmaydi).</summary>
        public static GeneratedDocument Fill(byte[] templateContent, TemplateData data)
        {
            var result = new GeneratedDocument { FileExtension = ".docx", MimeType = DocxMime };

            using (var stream = new MemoryStream())
            {
                // Keshdagi baytlar umumiy — nusxa ustida ishlanadi
                stream.Write(templateContent, 0, templateContent.Length);
                stream.Position = 0;

                using (var doc = WordprocessingDocument.Open(stream, true))
                {
                    var mainPart = doc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                        throw new InvalidOperationException("Shablon fayli buzilgan: hujjat tanasi topilmadi.");

                    var filler = new Filler(data, mainPart, result.UnknownPlaceholders);
                    filler.Fill(mainPart.Document.Body, allowBlocks: true);
                    foreach (var header in mainPart.HeaderParts)
                        filler.Fill(header.Header, allowBlocks: false);
                    foreach (var footer in mainPart.FooterParts)
                        filler.Fill(footer.Footer, allowBlocks: false);

                    mainPart.Document.Save();
                    foreach (var header in mainPart.HeaderParts) header.Header.Save();
                    foreach (var footer in mainPart.FooterParts) footer.Footer.Save();
                }

                result.Content = stream.ToArray();
            }

            return result;
        }

        /// <summary>Yuklanayotgan shablonni tekshiradi: haqiqiy .docx'mi, qaysi maydonlar tanildi/tanilmadi.</summary>
        public static TemplateScanResult Scan(byte[] content, string docTypeCode)
        {
            var result = new TemplateScanResult();
            try
            {
                using (var stream = new MemoryStream(content, false))
                using (var doc = WordprocessingDocument.Open(stream, false))
                {
                    var mainPart = doc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                    {
                        result.Error = "Faylda Word hujjat tanasi topilmadi.";
                        return result;
                    }

                    var roots = new List<OpenXmlElement> { mainPart.Document.Body };
                    roots.AddRange(mainPart.HeaderParts.Select(h => (OpenXmlElement)h.Header));
                    roots.AddRange(mainPart.FooterParts.Select(f => (OpenXmlElement)f.Footer));

                    foreach (var root in roots)
                    {
                        foreach (var paragraph in root.Descendants<Paragraph>())
                        {
                            foreach (Match m in PlaceholderRegex.Matches(ParagraphText(paragraph, OwnTexts(paragraph))))
                            {
                                bool doubleBrace = m.Groups["k"].Success;
                                string raw = doubleBrace ? m.Groups["k"].Value : m.Groups["s"].Value;
                                string key = DocPlaceholders.Resolve(docTypeCode, raw);
                                if (key != null)
                                {
                                    if (!result.Recognized.Contains(key)) result.Recognized.Add(key);
                                }
                                else if (doubleBrace && !result.Unknown.Contains(raw.Trim()))
                                {
                                    result.Unknown.Add(raw.Trim());
                                }
                            }
                        }
                    }
                }
                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.Error = "Fayl Word (.docx) formatida emas yoki buzilgan: " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Namunaviy shablon: tayyor tuzilma va barcha maydonlar ro'yxati. Foydalanuvchi uni Word'da
        /// o'z blankasi (logotip, rekvizitlar) bilan bezatib, qayta yuklaydi.
        /// </summary>
        public static byte[] BuildSample(string docTypeCode)
        {
            using (var stream = new MemoryStream())
            {
                using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    var mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document(new Body());
                    var body = mainPart.Document.Body;

                    Action<string, bool, JustificationValues?> line = (text, bold, align) =>
                    {
                        var p = new Paragraph();
                        if (align.HasValue)
                            p.AppendChild(new ParagraphProperties(new Justification { Val = align.Value }));
                        var r = new Run();
                        if (bold) r.AppendChild(new RunProperties(new Bold()));
                        r.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                        p.AppendChild(r);
                        body.AppendChild(p);
                    };

                    if (string.Equals(docTypeCode, DocPlaceholders.LetterType, StringComparison.OrdinalIgnoreCase))
                    {
                        line("№ {{LETTER_NO}}          {{LETTER_DATE}}", false, null);
                        line("{{RECIPIENT_BLOCK}}", true, JustificationValues.Right);
                        line("", false, null);
                        line("{{GREETING_LINE}}", true, JustificationValues.Center);
                        line("{{LETTER_BODY}}", false, JustificationValues.Both);
                        line("{{ITEMS_TABLE}}", false, null);
                        line("", false, null);
                        line("Hurmat bilan,", false, null);
                        line("Direktor                                        {{DIRECTOR_NAME}}", true, null);
                    }
                    else if (string.Equals(docTypeCode, DocPlaceholders.OrderType, StringComparison.OrdinalIgnoreCase))
                    {
                        line("BUYRUQ № {{ORDER_NO}}", true, JustificationValues.Center);
                        line("{{ORDER_DATE}}", false, JustificationValues.Right);
                        line("{{ORDER_TOPIC}}", true, JustificationValues.Center);
                        line("{{ORDER_BODY}}", false, JustificationValues.Both);
                        line("", false, null);
                        line("Direktor                                        {{DIRECTOR_NAME}}", true, null);
                    }
                    else
                    {
                        line("Korxona standarti", true, JustificationValues.Center);
                        line("Ushbu fayl Create sahifasida muharrirga yuklanadi; uni korxona blankasi bilan bezang.", false, null);
                    }

                    var placeholders = DocPlaceholders.For(docTypeCode);
                    if (placeholders.Count > 0)
                    {
                        body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                        line("Mavjud maydonlar (bu sahifani shablondan o'chirib tashlang)", true, null);
                        var table = new DocTable();
                        table.Headers.Add("Maydon");
                        table.Headers.Add("Ma'nosi");
                        foreach (var p in placeholders)
                        {
                            string kind = p.Kind == PlaceholderKind.RichText ? " — alohida qatorda: formatlangan matn"
                                : p.Kind == PlaceholderKind.Table ? " — alohida qatorda: jadval" : "";
                            // Ro'yxatda qavssiz — bu sahifa qoldirilsa ham to'ldirilib ketmaydi
                            table.Rows.Add(new List<string> { p.Key, p.Description + kind });
                        }
                        body.AppendChild(Filler.BuildTable(table, null));
                        line("Shablonda maydon nomini ikki juft jingalak qavs ichida yozing (yuqoridagi sahifadagidek).", false, null);
                    }

                    mainPart.Document.Save();
                }
                return stream.ToArray();
            }
        }

        private static CachedTemplate GetTemplate(int templateId)
        {
            string key = templateId.ToString();
            var cached = TemplateCache.Get(key) as CachedTemplate;
            if (cached != null)
                return cached;

            using (var db = new DBTHSNEntities())
            {
                cached = (from t in db.DOC_TEMPLATES
                          join dt in db.DOC_DOCUMENT_TYPES on t.DocTypeID equals dt.ID
                          where t.ID == templateId && t.IsActive
                          select new CachedTemplate
                          {
                              DocTypeCode = dt.Code,
                              FileExtension = t.FileExtension,
                              MimeType = t.MimeType,
                              FileContent = t.FileContent
                          }).FirstOrDefault();
            }

            if (cached == null || cached.FileContent == null)
                throw new InvalidOperationException("Shablon topilmadi yoki faol emas.");

            TemplateCache.Set(key, cached, DateTimeOffset.Now.Add(TemplateTtl));
            return cached;
        }

        /// <summary>Shablon yangilanganda/arxivlanganda chaqiriladi.</summary>
        public static void InvalidateTemplate(int templateId)
        {
            TemplateCache.Remove(templateId.ToString());
        }

        /// <summary>Paragrafning o'ziga tegishli Text'lari (ichki matn qutilaridagi paragraflar alohida qayta ishlanadi).</summary>
        private static List<Text> OwnTexts(Paragraph paragraph)
        {
            return paragraph.Descendants<Text>().Where(t => t.Ancestors<Paragraph>().FirstOrDefault() == paragraph).ToList();
        }

        private static string ParagraphText(Paragraph paragraph, List<Text> texts)
        {
            return string.Concat(texts.Select(t => t.Text));
        }

        private sealed class Filler
        {
            private readonly TemplateData _data;
            private readonly MainDocumentPart _mainPart;
            private readonly List<string> _unknown;

            public Filler(TemplateData data, MainDocumentPart mainPart, List<string> unknown)
            {
                _data = data;
                _mainPart = mainPart;
                _unknown = unknown;
            }

            public void Fill(OpenXmlElement root, bool allowBlocks)
            {
                if (root == null)
                    return;

                foreach (var paragraph in root.Descendants<Paragraph>().ToList())
                {
                    if (paragraph.Parent == null)
                        continue; // oldingi blok almashtirishda olib tashlangan
                    FillParagraph(paragraph, allowBlocks);
                }
            }

            private void FillParagraph(Paragraph paragraph, bool allowBlocks)
            {
                var texts = OwnTexts(paragraph);
                if (texts.Count == 0)
                    return;

                string full = ParagraphText(paragraph, texts);
                if (full.IndexOf('{') < 0)
                    return;

                var matches = PlaceholderRegex.Matches(full).Cast<Match>()
                    .Select(m => new { Match = m, Raw = m.Groups["k"].Success ? m.Groups["k"].Value : m.Groups["s"].Value, Double = m.Groups["k"].Success })
                    .Select(x => new { x.Match, x.Raw, x.Double, Key = DocPlaceholders.Resolve(_data.DocTypeCode, x.Raw) })
                    .ToList();

                foreach (var x in matches.Where(x => x.Key == null && x.Double))
                {
                    if (!_unknown.Contains(x.Raw.Trim())) _unknown.Add(x.Raw.Trim());
                }

                var known = matches.Where(x => x.Key != null).ToList();
                if (known.Count == 0)
                    return;

                // Paragrafda yolg'iz turgan matn/jadval maydoni — formatlangan blok
                if (allowBlocks && known.Count == 1 && full.Trim() == known[0].Match.Value.Trim() &&
                    TryReplaceWithBlock(paragraph, texts, known[0].Key))
                {
                    return;
                }

                // Ichki almashtirish oxiridan boshlab — oldingi pozitsiyalar o'zgarmaydi
                var starts = new int[texts.Count];
                int pos = 0;
                for (int i = 0; i < texts.Count; i++)
                {
                    starts[i] = pos;
                    pos += texts[i].Text.Length;
                }

                foreach (var x in known.OrderByDescending(k => k.Match.Index))
                {
                    ReplaceRange(texts, starts, x.Match.Index, x.Match.Length, InlineValue(x.Key));
                }

                foreach (var t in texts)
                {
                    if (t.Text.IndexOf('\n') >= 0)
                        SplitLines(t);
                }
            }

            private string InlineValue(string key)
            {
                string value;
                if (_data.Values.TryGetValue(key, out value))
                    return value ?? "";
                if (_data.Html.TryGetValue(key, out value))
                    return OfficialLetterDataService.HtmlToPlainText(value);
                DocTable table;
                if (_data.Tables.TryGetValue(key, out table))
                    return table.ToPlainText();
                return ""; // katalogdagi, lekin ma'lumoti yo'q maydon (masalan, ITEM_NAME_7) — tozalanadi
            }

            private static void ReplaceRange(List<Text> texts, int[] starts, int index, int length, string value)
            {
                int end = index + length; // exclusive
                int si = LocateText(starts, texts, index);
                int ei = LocateText(starts, texts, end - 1);
                int so = index - starts[si];
                int eo = end - starts[ei];

                if (si == ei)
                {
                    var t = texts[si];
                    t.Text = t.Text.Substring(0, so) + value + t.Text.Substring(eo);
                    t.Space = SpaceProcessingModeValues.Preserve;
                    return;
                }

                texts[si].Text = texts[si].Text.Substring(0, so) + value;
                texts[si].Space = SpaceProcessingModeValues.Preserve;
                for (int i = si + 1; i < ei; i++)
                    texts[i].Text = "";
                texts[ei].Text = texts[ei].Text.Substring(eo);
                texts[ei].Space = SpaceProcessingModeValues.Preserve;
            }

            private static int LocateText(int[] starts, List<Text> texts, int position)
            {
                for (int i = texts.Count - 1; i >= 0; i--)
                {
                    if (starts[i] <= position && texts[i].Text.Length > 0)
                        return i;
                }
                return 0;
            }

            private static void SplitLines(Text text)
            {
                var lines = text.Text.Replace("\r", "").Split('\n');
                text.Text = lines[0];
                OpenXmlElement after = text;
                for (int i = 1; i < lines.Length; i++)
                {
                    var br = new Break();
                    after.InsertAfterSelf(br);
                    var next = new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve };
                    br.InsertAfterSelf(next);
                    after = next;
                }
            }

            private bool TryReplaceWithBlock(Paragraph paragraph, List<Text> texts, string key)
            {
                var kind = DocPlaceholders.KindOf(_data.DocTypeCode, key);
                var baseRun = texts[0].Ancestors<Run>().FirstOrDefault()?.RunProperties;
                var basePara = paragraph.ParagraphProperties;

                List<OpenXmlElement> blocks = null;
                string html;
                DocTable table;

                if (kind == PlaceholderKind.RichText && _data.Html.TryGetValue(key, out html) && !string.IsNullOrWhiteSpace(html))
                {
                    blocks = HtmlToDocxConverter.ConvertToElements(html, _mainPart, baseRun, basePara);
                }
                else if (kind == PlaceholderKind.Table && _data.Tables.TryGetValue(key, out table))
                {
                    blocks = table.Rows.Count == 0
                        ? new List<OpenXmlElement>()
                        : new List<OpenXmlElement> { BuildTable(table, baseRun) };
                }
                else
                {
                    return false; // oddiy matn sifatida almashtiriladi
                }

                foreach (var block in blocks)
                    paragraph.InsertBeforeSelf(block);

                // Jadval katagi paragraf bilan tugashi shart; bo'sh blokda paragrafning o'zi (bo'sh) qoladi
                bool keepParagraph = blocks.Count == 0 ||
                                     (paragraph.Parent is TableCell && blocks.Last() is Table);
                if (keepParagraph)
                {
                    foreach (var t in texts)
                        t.Text = "";
                }
                else
                {
                    paragraph.Remove();
                }
                return true;
            }

            public static Table BuildTable(DocTable data, RunProperties baseRun)
            {
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

                int columns = Math.Max(data.Headers.Count, data.Rows.Count > 0 ? data.Rows.Max(r => r.Count) : 0);
                var grid = new TableGrid();
                for (int i = 0; i < columns; i++)
                    grid.AppendChild(new GridColumn());
                table.AppendChild(grid);

                if (data.Headers.Count > 0)
                {
                    var header = new TableRow(new TableRowProperties(new TableHeader()));
                    for (int i = 0; i < columns; i++)
                        header.AppendChild(Cell(i < data.Headers.Count ? data.Headers[i] : "", baseRun, true, false));
                    table.AppendChild(header);
                }

                foreach (var row in data.Rows)
                {
                    var tr = new TableRow();
                    for (int i = 0; i < columns; i++)
                        tr.AppendChild(Cell(i < row.Count ? row[i] : "", baseRun, false, data.NumericColumns.Contains(i)));
                    table.AppendChild(tr);
                }
                return table;
            }

            private static TableCell Cell(string text, RunProperties baseRun, bool bold, bool right)
            {
                var rp = new RunProperties();
                if (baseRun?.RunFonts != null) rp.AppendChild((RunFonts)baseRun.RunFonts.CloneNode(true));
                if (bold) rp.AppendChild(new Bold());
                if (baseRun?.FontSize != null) rp.AppendChild((FontSize)baseRun.FontSize.CloneNode(true));

                var run = new Run();
                if (rp.HasChildren) run.AppendChild(rp);
                run.AppendChild(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });

                var paragraph = new Paragraph();
                if (right || bold)
                    paragraph.AppendChild(new ParagraphProperties(new Justification { Val = bold ? JustificationValues.Center : JustificationValues.Right }));
                paragraph.AppendChild(run);
                return new TableCell(paragraph);
            }
        }
    }
}
