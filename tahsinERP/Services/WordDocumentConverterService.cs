using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace tahsinERP.Services
{
    /// <summary>
    /// HTML va .docx o'rtasida konvertatsiya qilish uchun servis
    /// </summary>
    public class WordDocumentConverterService
    {
        /// <summary>
        /// HTML kontentni .docx formatiga konvertatsiya qilish
        /// </summary>
        public static byte[] ConvertHtmlToDocx(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                // Bo'sh .docx yaratish
                return CreateEmptyDocx();
            }

            using (var stream = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    // Main document part yaratish
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // HTML ni parchalash va Paragraph larga aylantirish
                    var paragraphs = ParseHtmlToParagraphs(htmlContent);
                    foreach (var paraText in paragraphs)
                    {
                        var paragraph = new Paragraph();
                        var run = new Run();
                        var text = new Text(SanitizeText(paraText));
                        run.Append(text);
                        paragraph.Append(run);
                        body.Append(paragraph);
                    }

                    mainPart.Document.Save();
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// .docx faylini HTML ga konvertatsiya qilish (oddiy matn sifatida)
        /// </summary>
        public static string ConvertDocxToHtml(byte[] docxBytes)
        {
            if (docxBytes == null || docxBytes.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                using (var stream = new MemoryStream(docxBytes))
                using (var wordDocument = WordprocessingDocument.Open(stream, false))
                {
                    if (wordDocument?.MainDocumentPart?.Document?.Body == null)
                    {
                        return string.Empty;
                    }

                    var body = wordDocument.MainDocumentPart.Document.Body;
                    var paragraphs = body.Elements<Paragraph>();
                    var html = new StringBuilder();

                    foreach (var paragraph in paragraphs)
                    {
                        var text = GetTextFromParagraph(paragraph);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // HTML formatida saqlash (Quill uchun)
                            html.Append($"<p>{EscapeHtml(text)}</p>");
                        }
                    }

                    return html.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Docx to HTML conversion error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Bo'sh .docx fayl yaratish
        /// </summary>
        public static byte[] CreateEmptyDocx()
        {
            using (var stream = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());
                    var paragraph = new Paragraph();
                    var run = new Run();
                    var text = new Text("");
                    run.Append(text);
                    paragraph.Append(run);
                    body.Append(paragraph);
                    mainPart.Document.Save();
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Template .docx faylni yuklash yoki bo'sh yaratish
        /// </summary>
        public static byte[] LoadTemplateDocx(string templatePath)
        {
            if (File.Exists(templatePath))
            {
                return File.ReadAllBytes(templatePath);
            }
            return CreateEmptyDocx();
        }

        private static string[] ParseHtmlToParagraphs(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return new string[0];
            }

            // HTML teglardan matnni ajratib olish
            // <p>, <div> va boshqa block elementlarni paragraflarga aylantirish
            var paragraphs = new List<string>();
            
            // <p> va </p> teglar orasidagi kontentni ajratish
            var pMatches = Regex.Matches(html, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in pMatches)
            {
                var content = match.Groups[1].Value;
                // Ichki HTML teglarni olib tashlash
                content = Regex.Replace(content, "<[^>]+>", "", RegexOptions.IgnoreCase);
                content = System.Net.WebUtility.HtmlDecode(content).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    paragraphs.Add(content);
                }
            }

            // Agar <p> teglar topilmasa, barcha HTML teglarni olib tashlash
            if (paragraphs.Count == 0)
            {
                var textOnly = Regex.Replace(html, "<[^>]+>", "", RegexOptions.IgnoreCase);
                textOnly = System.Net.WebUtility.HtmlDecode(textOnly);
                var lines = textOnly.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                paragraphs.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l.Trim())));
            }

            return paragraphs.ToArray();
        }

        private static string GetTextFromParagraph(Paragraph paragraph)
        {
            var textBuilder = new StringBuilder();
            var runs = paragraph.Elements<Run>();

            foreach (var run in runs)
            {
                var texts = run.Elements<Text>();
                foreach (var text in texts)
                {
                    textBuilder.Append(text.Text);
                }
            }

            return textBuilder.ToString();
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // XML uchun xavfli belgilarni almashtirish
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return System.Net.WebUtility.HtmlEncode(text);
        }
    }
}

