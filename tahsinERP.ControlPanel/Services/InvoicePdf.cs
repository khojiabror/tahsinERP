using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.Extensions.Options;
using PdfSharp.Fonts;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Hisob-faktura PDF. Kutubxona: PDFsharp + MigraDoc 6.2.x — MIT litsenziya. DIQQAT: 6.5.0 dan boshlab AGPL-3.0
/// (2026-10) — versiyani oshirmang. QuestPDF Community litsenziyasi yillik daromadi $1M dan kam kompaniyalar uchun bepul,
/// lekin MIT emas (daromad oshsa pullik) — shuning uchun tanlanmadi.
/// </summary>
public sealed class InvoicePdfRenderer(IOptions<VendorOptions> vendor, Clock clock)
{
    public const string FontFamily = "PanelSans";

    public byte[] Render(Invoice invoice, Tenant tenant)
    {
        PanelFontResolver.EnsureRegistered();
        var v = vendor.Value;
        var doc = new Document();
        doc.Info.Title = "Hisob-faktura " + (invoice.Number ?? "(qoralama)");
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = 9;

        var section = doc.AddSection();
        section.PageSetup = doc.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromMillimeter(15);
        section.PageSetup.LeftMargin = Unit.FromMillimeter(15);
        section.PageSetup.RightMargin = Unit.FromMillimeter(15);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(15);

        var title = section.AddParagraph();
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText(invoice.Number is null ? "HISOB-FAKTURA (QORALAMA)" : $"HISOB-FAKTURA № {invoice.Number}");
        var sub = section.AddParagraph($"Sana: {invoice.IssueDate:dd.MM.yyyy}    To'lov muddati: {invoice.DueDate:dd.MM.yyyy}");
        sub.Format.Alignment = ParagraphAlignment.Center;
        sub.Format.SpaceAfter = Unit.FromMillimeter(6);

        var parties = section.AddTable();
        parties.Borders.Width = 0;
        parties.AddColumn(Unit.FromMillimeter(90));
        parties.AddColumn(Unit.FromMillimeter(90));
        var row = parties.AddRow();
        Party(row.Cells[0], "Sotuvchi (ijrochi)", v.LegalName, v.Inn, v.VatCode, v.Address, v.BankName, v.BankAccount, v.Mfo, v.Phone);
        Party(row.Cells[1], "Xaridor (buyurtmachi)", tenant.LegalName, tenant.Inn, null, tenant.Address, null, null, null, tenant.Phone);
        section.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(4);

        var t = section.AddTable();
        t.Borders.Width = 0.5;
        t.Format.Font.Size = 8.5;
        double[] widths = [8, 72, 16, 24, 26, 12, 22];
        foreach (var w in widths) t.AddColumn(Unit.FromMillimeter(w));
        var head = t.AddRow();
        head.HeadingFormat = true;
        head.Format.Font.Bold = true;
        head.Shading.Color = Colors.LightGray;
        string[] titles = ["№", "Xizmat nomi", "Miqdor", "Narx (QQS bilan)", "Summa (QQS bilan)", "QQS %", "QQS summasi"];
        for (int i = 0; i < titles.Length; i++) head.Cells[i].AddParagraph(titles[i]);
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNo))
        {
            var r = t.AddRow();
            r.Cells[0].AddParagraph(line.LineNo.ToString());
            r.Cells[1].AddParagraph(line.Description);
            r.Cells[2].AddParagraph(line.Quantity.ToString("0.###"));
            r.Cells[3].AddParagraph(Money.Format(line.UnitPriceTiyin));
            r.Cells[4].AddParagraph(Money.Format(line.AmountTiyin));
            r.Cells[5].AddParagraph(line.VatRatePercent.ToString("0.##"));
            r.Cells[6].AddParagraph(Money.Format(line.VatTiyin));
            for (int i = 2; i < 7; i++) r.Cells[i].Format.Alignment = ParagraphAlignment.Right;
        }
        Total(t, "Jami (QQS bilan):", invoice.TotalTiyin, bold: true);
        Total(t, "Shu jumladan QQS:", invoice.VatTiyin);
        Total(t, "QQSsiz summa:", invoice.TotalTiyin - invoice.VatTiyin);
        if (invoice.PaidTiyin > 0)
        {
            Total(t, "To'langan:", invoice.PaidTiyin);
            Total(t, "Qoldiq:", invoice.RemainingTiyin, bold: true);
        }

        var pay = section.AddParagraph();
        pay.Format.SpaceBefore = Unit.FromMillimeter(6);
        pay.AddFormattedText("To'lov maqsadi: ", TextFormat.Bold);
        pay.AddText($"{invoice.Number ?? "hisob-faktura"} bo'yicha tahsinERP xizmatlari uchun to'lov, QQS bilan.");
        if (!string.IsNullOrWhiteSpace(invoice.Note))
            section.AddParagraph("Izoh: " + invoice.Note).Format.SpaceBefore = Unit.FromMillimeter(2);

        var sign = section.AddTable();
        sign.Borders.Width = 0;
        sign.AddColumn(Unit.FromMillimeter(90));
        sign.AddColumn(Unit.FromMillimeter(90));
        var sr = sign.AddRow();
        sr.TopPadding = Unit.FromMillimeter(14);
        sr.Cells[0].AddParagraph($"Rahbar: ____________ {v.Director}");
        sr.Cells[1].AddParagraph($"Bosh hisobchi: ____________ {v.Accountant}");

        var footer = section.Footers.Primary.AddParagraph($"tahsinERP boshqaruv paneli · {clock.LocalNow:dd.MM.yyyy HH:mm}");
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Colors.Gray;

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static void Party(Cell cell, string role, string name, string? inn, string? vat, string? address, string? bank, string? account, string? mfo, string? phone)
    {
        var p = cell.AddParagraph(role);
        p.Format.Font.Bold = true;
        p.Format.Font.Color = Colors.DimGray;
        cell.AddParagraph(string.IsNullOrWhiteSpace(name) ? "(to'ldirilmagan)" : name).Format.Font.Bold = true;
        void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) cell.AddParagraph($"{label}: {value}"); }
        Add("STIR", inn);
        Add("QQS to'lovchi kodi", vat);
        Add("Manzil", address);
        Add("Bank", bank);
        Add("H/r", account);
        Add("MFO", mfo);
        Add("Tel", phone);
    }

    private static void Total(Table t, string label, long tiyin, bool bold = false)
    {
        var r = t.AddRow();
        r.Cells[0].MergeRight = 5;
        r.Cells[0].AddParagraph(label).Format.Alignment = ParagraphAlignment.Right;
        r.Cells[6].AddParagraph(Money.Format(tiyin)).Format.Alignment = ParagraphAlignment.Right;
        r.Format.Font.Bold = bold;
    }
}

/// <summary>
/// PDFsharp Core build uchun shrift: Windows'da Arial, Linux'da DejaVu/Liberation, yoki <c>ControlPanel:Pdf:FontsDir</c>.
/// Kirill va lotin (o', g') harflari ham chiqadi.
/// </summary>
public sealed class PanelFontResolver : IFontResolver
{
    private static readonly object Gate = new();
    private static bool _registered;
    public static string? ExtraFontsDir { get; set; }

    private static readonly (string Regular, string Bold)[] Candidates =
    [
        ("arial.ttf", "arialbd.ttf"),
        ("DejaVuSans.ttf", "DejaVuSans-Bold.ttf"),
        ("LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf"),
    ];

    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered) return;
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new PanelFontResolver();
            _registered = true;
        }
    }

    private static IEnumerable<string> Dirs()
    {
        if (!string.IsNullOrWhiteSpace(ExtraFontsDir)) yield return ExtraFontsDir;
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        yield return "/usr/share/fonts/truetype/dejavu";
        yield return "/usr/share/fonts/truetype/liberation";
        yield return "/usr/share/fonts/dejavu";
    }

    private static string? Find(bool bold)
    {
        foreach (var dir in Dirs())
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            foreach (var c in Candidates)
            {
                string path = Path.Combine(dir, bold ? c.Bold : c.Regular);
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "panel-bold" : "panel-regular", false, isItalic);

    public byte[]? GetFont(string faceName)
    {
        string? path = Find(faceName == "panel-bold") ?? Find(false)
            ?? throw new InvalidOperationException("PDF uchun shrift topilmadi (Arial/DejaVu/Liberation). ControlPanel:Pdf:FontsDir ni sozlang.");
        return File.ReadAllBytes(path);
    }
}
