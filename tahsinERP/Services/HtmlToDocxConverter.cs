using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using D = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DP = DocumentFormat.OpenXml.Drawing.Pictures;

namespace tahsinERP.Services
{
    /// <summary>
    /// HTML kontentni .docx formatiga konvertatsiya qilish
    /// </summary>
    public class HtmlToDocxConverter
    {
        /// <summary>
        /// HTML kontentni .docx byte array ga konvertatsiya qilish
        /// </summary>
        public static byte[] ConvertHtmlToDocx(string htmlContent)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // HTML ni paragraflarga ajratish
                    var paragraphs = ParseHtmlToParagraphs(htmlContent);
                    
                    foreach (var paragraphHtml in paragraphs)
                    {
                        var paragraph = CreateParagraphFromHtml(paragraphHtml, mainPart);
                        body.AppendChild(paragraph);
                    }

                    mainPart.Document.Save();
                }

                return memoryStream.ToArray();
            }
        }

        private static string[] ParseHtmlToParagraphs(string html)
        {
            // HTML taglarini tozalash va paragraflarga ajratish
            html = Regex.Replace(html, @"<p[^>]*>", "<p>", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "</p>", RegexOptions.IgnoreCase);
            
            // <p> taglari orasidagi kontentni ajratish
            var matches = Regex.Matches(html, @"<p[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var paragraphs = new System.Collections.Generic.List<string>();
            
            foreach (Match match in matches)
            {
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    paragraphs.Add(match.Groups[0].Value);
                }
            }

            // Agar paragraflar topilmasa, butun HTML ni qaytarish
            if (paragraphs.Count == 0)
            {
                paragraphs.Add(html);
            }

            return paragraphs.ToArray();
        }

        private static Paragraph CreateParagraphFromHtml(string html, MainDocumentPart mainPart)
        {
            var paragraph = new Paragraph();
            var ParagraphProperties = new ParagraphProperties();
            paragraph.AppendChild(ParagraphProperties);

            // Rasmlarni qayta ishlash
            var imageMatches = Regex.Matches(html, @"<img[^>]+src=[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase);
            if (imageMatches.Count > 0)
            {
                foreach (Match imgMatch in imageMatches)
                {
                    var imageSrc = imgMatch.Groups[1].Value;
                    // Base64 rasm
                    if (imageSrc.StartsWith("data:image"))
                    {
                        var imageRun = CreateImageRunFromBase64(imageSrc, mainPart);
                        if (imageRun != null)
                        {
                            paragraph.AppendChild(imageRun);
                        }
                    }
                }
                // Rasmdan keyin matn bo'lsa
                var textAfterImage = Regex.Replace(html, @"<img[^>]+>", "", RegexOptions.IgnoreCase);
                if (!string.IsNullOrWhiteSpace(textAfterImage))
                {
                    var text = StripHtmlTags(textAfterImage);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var run = CreateTextRun(textAfterImage);
                        paragraph.AppendChild(run);
                    }
                }
            }
            else
            {
                // Faqat matn
                var text = StripHtmlTags(html);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var run = CreateTextRun(html);
                    paragraph.AppendChild(run);
                }
            }

            return paragraph;
        }

        private static Run CreateTextRun(string html)
        {
            var run = new Run();
            var runProperties = new RunProperties();

            // Bold
            if (html.Contains("<strong>") || html.Contains("<b>"))
            {
                runProperties.AppendChild(new Bold());
            }

            // Italic
            if (html.Contains("<em>") || html.Contains("<i>"))
            {
                runProperties.AppendChild(new Italic());
            }

            // Underline
            if (html.Contains("<u>"))
            {
                runProperties.AppendChild(new Underline { Val = UnderlineValues.Single });
            }

            // Font size
            var fontSizeMatch = Regex.Match(html, @"font-size:\s*(\d+)pt", RegexOptions.IgnoreCase);
            if (fontSizeMatch.Success)
            {
                var fontSize = fontSizeMatch.Groups[1].Value;
                runProperties.AppendChild(new FontSize { Val = fontSize });
            }

            // Color
            var colorMatch = Regex.Match(html, @"color:\s*([^;]+)", RegexOptions.IgnoreCase);
            if (colorMatch.Success)
            {
                var color = colorMatch.Groups[1].Value.Trim();
                runProperties.AppendChild(new Color { Val = ConvertColorToHex(color) });
            }

            if (runProperties.HasChildren)
            {
                run.AppendChild(runProperties);
            }

            // Matnni qo'shish
            var text = StripHtmlTags(html);
            var textElement = new Text(text);
            run.AppendChild(textElement);

            return run;
        }

        private static string StripHtmlTags(string html)
        {
            // HTML taglarini olib tashlash
            var text = Regex.Replace(html, @"<[^>]+>", " ");
            text = System.Web.HttpUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        private static string ConvertColorToHex(string color)
        {
            // Rang nomini hex ga o'tkazish
            color = color.Trim().ToLower();
            
            var colorMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "black", "000000" },
                { "white", "FFFFFF" },
                { "red", "FF0000" },
                { "green", "008000" },
                { "blue", "0000FF" },
                { "yellow", "FFFF00" },
                { "cyan", "00FFFF" },
                { "magenta", "FF00FF" }
            };

            if (colorMap.ContainsKey(color))
            {
                return colorMap[color];
            }

            // Hex format
            if (color.StartsWith("#"))
            {
                return color.Substring(1).ToUpper();
            }

            // RGB format
            var rgbMatch = Regex.Match(color, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
            if (rgbMatch.Success)
            {
                var r = int.Parse(rgbMatch.Groups[1].Value).ToString("X2");
                var g = int.Parse(rgbMatch.Groups[2].Value).ToString("X2");
                var b = int.Parse(rgbMatch.Groups[3].Value).ToString("X2");
                return r + g + b;
            }

            return "000000"; // Default: black
        }

        private static Run CreateImageRunFromBase64(string base64Image, MainDocumentPart mainPart)
        {
            try
            {
                // Base64 dan byte array ga o'tkazish
                var base64Data = base64Image.Substring(base64Image.IndexOf(",") + 1);
                var imageBytes = Convert.FromBase64String(base64Data);

                // Image part yaratish - formatga qarab
                ImagePart imagePart;
                if (base64Image.Contains("image/jpeg") || base64Image.Contains("image/jpg"))
                {
                    imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
                }
                else if (base64Image.Contains("image/gif"))
                {
                    imagePart = mainPart.AddImagePart(ImagePartType.Gif);
                }
                else
                {
                    imagePart = mainPart.AddImagePart(ImagePartType.Png);
                }
                using (var imageStream = new MemoryStream(imageBytes))
                {
                    imagePart.FeedData(imageStream);
                }

                // Relationship ID
                var relationshipId = mainPart.GetIdOfPart(imagePart);

                // Drawing yaratish - hozircha o'chirilgan
                // Rasmlar keyinchalik qo'shiladi
                System.Diagnostics.Debug.WriteLine("Rasm qo'shish hozircha qo'llab-quvvatlanmaydi");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rasm qo'shishda xatolik: {ex.Message}");
                return null;
            }
        }

        private static object CreateImageDrawing(string relationshipId, long imageSize)
        {
            // Drawing element yaratish - hozircha o'chirilgan, keyinchalik qayta qo'shiladi
            // Rasmlar bilan ishlash keyinchalik amalga oshiriladi
            try
            {
                // TODO: Rasmlar uchun Drawing element yaratish
                // Hozircha null qaytaradi, rasmlar qo'shilmaydi
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

