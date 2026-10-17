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
        /// HTML kontentni .docx formatiga konvertatsiya qilish. Yagona konverterga (HtmlToDocxConverter) yo'naltiriladi:
        /// avvalgi nusxa formatlashni tashlab, '&amp;' ni ikki marta escape qilardi (Word'da "&amp;amp;" ko'rinardi, DOC-B10).
        /// </summary>
        public static byte[] ConvertHtmlToDocx(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return CreateEmptyDocx();
            }

            return HtmlToDocxConverter.ConvertHtmlToDocx(htmlContent);
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

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return System.Net.WebUtility.HtmlEncode(text);
        }
    }
}

