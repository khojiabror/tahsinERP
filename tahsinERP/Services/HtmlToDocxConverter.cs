using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace tahsinERP.Services
{
    /// <summary>
    /// Quill muharriri HTML'ini .docx'ga aylantiradi (STD va — WordDocumentConverterService orqali — JobDesc).
    ///
    /// Avvalgi ikki konverterning kamchiliklari: paragrafda bitta &lt;strong&gt; bo'lsa butun paragraf qalin
    /// bo'lardi, sarlavha/ro'yxatlar yo'qolardi, rasm ImagePart sifatida qo'shilib hujjatda ko'rinmasdi
    /// (yetim qism — DOC-P09), JobDesc konverteri esa '&amp;' ni ikki marta escape qilardi (DOC-B10).
    /// Bu yerda HTML ketma-ket teglar bo'yicha o'qiladi: blok teglar — paragraf, inline teglar — run formati.
    /// </summary>
    public static class HtmlToDocxConverter
    {
        private const long MaxImageWidthEmu = 5943600L; // ~16.5 sm (A4 matn kengligi)
        private const long EmuPerPixel = 9525L;

        private static readonly Regex Token = new Regex(@"<(?<close>/?)(?<tag>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*)>|(?<text>[^<]+)", RegexOptions.Compiled);
        private static readonly Regex SrcAttr = new Regex(@"src\s*=\s*[""'](?<v>[^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClassAttr = new Regex(@"class\s*=\s*[""'](?<v>[^""']*)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StyleColor = new Regex(@"(?<![-\w])color\s*:\s*(?<v>[^;""']+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "pre"
        };

        public static byte[] ConvertHtmlToDocx(string htmlContent)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    foreach (var element in ConvertToElements(htmlContent, mainPart))
                        body.AppendChild(element);

                    if (!body.HasChildren)
                        body.AppendChild(new Paragraph());

                    mainPart.Document.Save();
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// HTML'ni paragraflar ro'yxatiga aylantiradi (mavjud hujjatga qo'yish uchun — shablondagi formatlangan maydon).
        /// Rasmlar <paramref name="mainPart"/> ga qo'shiladi. <paramref name="baseRun"/>/<paramref name="baseParagraph"/>
        /// berilsa — shablondagi shrift, o'lcham, rang, uslub va oraliqlar saqlanadi.
        /// </summary>
        public static List<OpenXmlElement> ConvertToElements(string html, MainDocumentPart mainPart,
            RunProperties baseRun = null, ParagraphProperties baseParagraph = null)
        {
            var output = new List<OpenXmlElement>();
            new Writer(mainPart, output, baseRun, baseParagraph).Write(html ?? string.Empty);
            return output;
        }

        private sealed class Writer
        {
            private readonly MainDocumentPart _mainPart;
            private readonly List<OpenXmlElement> _output;
            private readonly RunProperties _baseRun;
            private readonly ParagraphProperties _baseParagraph;
            private Paragraph _paragraph;

            private int _bold, _italic, _underline, _strike;
            private readonly Stack<string> _colors = new Stack<string>();
            private readonly Stack<string> _inlineStack = new Stack<string>();
            private readonly Stack<ListState> _lists = new Stack<ListState>();
            private string _blockTag;
            private uint _imageId;

            private sealed class ListState
            {
                public bool Ordered;
                public int Counter;
            }

            public Writer(MainDocumentPart mainPart, List<OpenXmlElement> output, RunProperties baseRun, ParagraphProperties baseParagraph)
            {
                _mainPart = mainPart;
                _output = output;
                _baseRun = baseRun;
                _baseParagraph = baseParagraph;
            }

            public void Write(string html)
            {
                foreach (Match m in Token.Matches(html))
                {
                    if (m.Groups["text"].Success)
                    {
                        AddText(System.Net.WebUtility.HtmlDecode(m.Groups["text"].Value));
                        continue;
                    }

                    string tag = m.Groups["tag"].Value.ToLowerInvariant();
                    bool close = m.Groups["close"].Value == "/";
                    string attrs = m.Groups["attrs"].Value;

                    if (BlockTags.Contains(tag))
                    {
                        if (close) EndBlock();
                        else StartBlock(tag, attrs);
                    }
                    else if (tag == "ol" || tag == "ul")
                    {
                        EndBlock();
                        if (close) { if (_lists.Count > 0) _lists.Pop(); }
                        else _lists.Push(new ListState { Ordered = tag == "ol" });
                    }
                    else if (tag == "br")
                    {
                        EnsureParagraph().AppendChild(new Run(new Break()));
                    }
                    else if (tag == "img" && !close)
                    {
                        var src = SrcAttr.Match(attrs);
                        if (src.Success)
                            AddImage(src.Groups["v"].Value);
                    }
                    else
                    {
                        Inline(tag, close, attrs);
                    }
                }
                EndBlock();
            }

            private void Inline(string tag, bool close, string attrs)
            {
                int delta = close ? -1 : 1;
                switch (tag)
                {
                    case "strong":
                    case "b":
                        _bold = Math.Max(0, _bold + delta);
                        break;
                    case "em":
                    case "i":
                        _italic = Math.Max(0, _italic + delta);
                        break;
                    case "u":
                        _underline = Math.Max(0, _underline + delta);
                        break;
                    case "s":
                    case "strike":
                    case "del":
                        _strike = Math.Max(0, _strike + delta);
                        break;
                    case "span":
                    case "font":
                        // Rang faqat span/font uslubidan; yopilishda o'z rangini oladi
                        if (close)
                        {
                            if (_inlineStack.Count > 0 && _inlineStack.Pop() == "color" && _colors.Count > 0)
                                _colors.Pop();
                        }
                        else
                        {
                            var color = StyleColor.Match(attrs);
                            string hex = color.Success ? ConvertColorToHex(color.Groups["v"].Value) : null;
                            if (hex != null)
                            {
                                _colors.Push(hex);
                                _inlineStack.Push("color");
                            }
                            else
                            {
                                _inlineStack.Push("none");
                            }
                        }
                        break;
                }
            }

            private void StartBlock(string tag, string attrs)
            {
                EndBlock();
                _blockTag = tag;
                // pPr bolalari sxema tartibida: pStyle, spacing, ind, jc
                var props = new ParagraphProperties();
                CopyBaseParagraph(props, includeLayout: tag != "li" && tag != "blockquote");
                if (tag == "li")
                    props.AppendChild(new Indentation { Left = (360 * Math.Max(1, _lists.Count)).ToString(), Hanging = "360" });
                else if (tag == "blockquote")
                    props.AppendChild(new Indentation { Left = "720" });

                var cls = ClassAttr.Match(attrs);
                if (cls.Success)
                {
                    string c = cls.Groups["v"].Value;
                    if (c.Contains("ql-align-center")) props.AppendChild(new Justification { Val = JustificationValues.Center });
                    else if (c.Contains("ql-align-right")) props.AppendChild(new Justification { Val = JustificationValues.Right });
                    else if (c.Contains("ql-align-justify")) props.AppendChild(new Justification { Val = JustificationValues.Both });
                }
                if (props.Justification == null && _baseParagraph?.Justification != null)
                    props.AppendChild((Justification)_baseParagraph.Justification.CloneNode(true));

                _paragraph = props.HasChildren ? new Paragraph(props) : new Paragraph();

                if (tag == "li")
                {
                    var list = _lists.Count > 0 ? _lists.Peek() : null;
                    string marker = list != null && list.Ordered ? (++list.Counter) + ". " : "• ";
                    _paragraph.AppendChild(MakeRun(marker));
                }
            }

            private void EndBlock()
            {
                if (_paragraph != null)
                {
                    _output.Add(_paragraph);
                    _paragraph = null;
                }
                _blockTag = null;
            }

            private Paragraph EnsureParagraph()
            {
                if (_paragraph == null)
                {
                    var props = new ParagraphProperties();
                    CopyBaseParagraph(props, includeLayout: true);
                    if (_baseParagraph?.Justification != null)
                        props.AppendChild((Justification)_baseParagraph.Justification.CloneNode(true));
                    _paragraph = props.HasChildren ? new Paragraph(props) : new Paragraph();
                }
                return _paragraph;
            }

            /// <summary>Shablon paragrafidan uslub va oraliqni (va kerak bo'lsa chekinish/tekislashni) ko'chiradi.</summary>
            private void CopyBaseParagraph(ParagraphProperties target, bool includeLayout)
            {
                if (_baseParagraph == null)
                    return;
                if (_baseParagraph.ParagraphStyleId != null)
                    target.AppendChild((ParagraphStyleId)_baseParagraph.ParagraphStyleId.CloneNode(true));
                if (_baseParagraph.SpacingBetweenLines != null)
                    target.AppendChild((SpacingBetweenLines)_baseParagraph.SpacingBetweenLines.CloneNode(true));
                if (includeLayout && _baseParagraph.Indentation != null)
                    target.AppendChild((Indentation)_baseParagraph.Indentation.CloneNode(true));
            }

            private void AddText(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return;
                // Bloklar orasidagi faqat bo'shliq/yangi qatordan iborat matn — HTML formatlash, kontent emas
                if (_paragraph == null && string.IsNullOrWhiteSpace(text))
                    return;

                EnsureParagraph().AppendChild(MakeRun(text.Replace("\r", "").Replace("\n", " ")));
            }

            private Run MakeRun(string text)
            {
                var run = new Run();
                var rp = new RunProperties();
                int heading = HeadingLevel();
                var b = _baseRun;

                // rPr bolalari sxema tartibida: rFonts, b, i, strike, color, sz, szCs, u
                if (b?.RunFonts != null) rp.AppendChild((RunFonts)b.RunFonts.CloneNode(true));
                if (_bold > 0 || heading > 0 || b?.Bold != null) rp.AppendChild(new Bold());
                if (_italic > 0 || b?.Italic != null) rp.AppendChild(new Italic());
                if (_strike > 0) rp.AppendChild(new Strike());
                if (_colors.Count > 0) rp.AppendChild(new Color { Val = _colors.Peek() });
                else if (b?.Color != null) rp.AppendChild((Color)b.Color.CloneNode(true));
                if (heading > 0) rp.AppendChild(new FontSize { Val = HeadingHalfPoints(heading) });
                else if (b?.FontSize != null) rp.AppendChild((FontSize)b.FontSize.CloneNode(true));
                if (heading == 0 && b?.FontSizeComplexScript != null) rp.AppendChild((FontSizeComplexScript)b.FontSizeComplexScript.CloneNode(true));
                if (_underline > 0) rp.AppendChild(new Underline { Val = UnderlineValues.Single });

                if (rp.HasChildren)
                    run.AppendChild(rp);
                run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                return run;
            }

            private int HeadingLevel()
            {
                if (_blockTag != null && _blockTag.Length == 2 && _blockTag[0] == 'h' && char.IsDigit(_blockTag[1]))
                    return _blockTag[1] - '0';
                return 0;
            }

            private static string HeadingHalfPoints(int level)
            {
                switch (level)
                {
                    case 1: return "32";
                    case 2: return "28";
                    case 3: return "26";
                    default: return "24";
                }
            }

            private void AddImage(string src)
            {
                if (!src.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    return; // tashqi URL'lar yuklanmaydi

                try
                {
                    int comma = src.IndexOf(',');
                    if (comma < 0) return;
                    var bytes = Convert.FromBase64String(src.Substring(comma + 1));
                    string header = src.Substring(0, comma).ToLowerInvariant();

                    var partType = header.Contains("jpeg") || header.Contains("jpg") ? ImagePartType.Jpeg
                        : header.Contains("gif") ? ImagePartType.Gif
                        : header.Contains("bmp") ? ImagePartType.Bmp
                        : ImagePartType.Png;

                    long cx, cy;
                    using (var ms = new MemoryStream(bytes))
                    using (var img = System.Drawing.Image.FromStream(ms, false, false))
                    {
                        cx = img.Width * EmuPerPixel;
                        cy = img.Height * EmuPerPixel;
                    }
                    if (cx > MaxImageWidthEmu)
                    {
                        cy = cy * MaxImageWidthEmu / cx;
                        cx = MaxImageWidthEmu;
                    }

                    var imagePart = _mainPart.AddImagePart(partType);
                    using (var ms = new MemoryStream(bytes))
                    {
                        imagePart.FeedData(ms);
                    }

                    EnsureParagraph().AppendChild(new Run(CreateDrawing(_mainPart.GetIdOfPart(imagePart), cx, cy, ++_imageId)));
                }
                catch (Exception ex)
                {
                    // Buzilgan rasm butun hujjatni to'xtatmasin
                    System.Diagnostics.Trace.TraceWarning("HtmlToDocx: rasm qo'shilmadi: {0}", ex.Message);
                }
            }
        }

        private static Drawing CreateDrawing(string relationshipId, long cx, long cy, uint id)
        {
            return new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = id, Name = "Rasm " + id },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "image" + id },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = cx, Cy = cy }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });
        }

        private static string ConvertColorToHex(string color)
        {
            color = color.Trim().ToLowerInvariant();

            switch (color)
            {
                case "black": return "000000";
                case "white": return "FFFFFF";
                case "red": return "FF0000";
                case "green": return "008000";
                case "blue": return "0000FF";
                case "yellow": return "FFFF00";
                case "cyan": return "00FFFF";
                case "magenta": return "FF00FF";
            }

            if (color.StartsWith("#"))
            {
                string hex = color.Substring(1);
                if (hex.Length == 3)
                    hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
                return Regex.IsMatch(hex, "^[0-9a-f]{6}$") ? hex.ToUpperInvariant() : null;
            }

            var rgbMatch = Regex.Match(color, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
            if (rgbMatch.Success)
            {
                return int.Parse(rgbMatch.Groups[1].Value).ToString("X2") +
                       int.Parse(rgbMatch.Groups[2].Value).ToString("X2") +
                       int.Parse(rgbMatch.Groups[3].Value).ToString("X2");
            }

            return null;
        }
    }
}
