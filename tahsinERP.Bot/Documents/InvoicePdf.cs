using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using tahsinERP.Bot.Erp;

namespace tahsinERP.Bot.Documents;

/// <summary>
/// Mijozga Telegram orqali yuboriladigan nakladnoy PDF (yetkazilgan miqdorlar bilan; qisman yetkazishda — faqat yetkazilgani).
/// Kutubxona: PDFsharp + MigraDoc 6.2.x — MIT (6.5.0 dan AGPL — versiyani oshirmang, ControlPanel/InvoicePdf.cs bilan bir xil qaror).
/// Ma'lumot ERP'dan (DstApi/StopInvoice), ERP PDF kutubxonasiz qoladi.
/// </summary>
public static class InvoicePdf
{
    public const string FontFamily = "BotSans";

    private static readonly Dictionary<string, (string Uz, string Ru)> L = new()
    {
        ["Title"] = ("NAKLADNOY № {0}", "НАКЛАДНАЯ № {0}"),
        ["Partial"] = ("(qisman yetkazilgan)", "(частичная доставка)"),
        ["Order"] = ("Buyurtma: {0} · Sana: {1}", "Заказ: {0} · Дата: {1}"),
        ["Supplier"] = ("Yetkazib beruvchi", "Поставщик"),
        ["Receiver"] = ("Qabul qiluvchi", "Получатель"),
        ["Inn"] = ("STIR: {0}", "ИНН: {0}"),
        ["No"] = ("№", "№"),
        ["Product"] = ("Tovar", "Товар"),
        ["Qty"] = ("Miqdor", "Кол-во"),
        ["Price"] = ("Narx", "Цена"),
        ["Amount"] = ("Summa", "Сумма"),
        ["Total"] = ("Jami:", "Итого:"),
        ["Debt"] = ("Joriy qarzingiz: {0} so'm", "Ваш текущий долг: {0} сум"),
        ["Delivery"] = ("Reys {0} · ekspeditor {1} · mashina {2} · yetkazildi {3}", "Рейс {0} · экспедитор {1} · машина {2} · доставлено {3}"),
        ["Released"] = ("Topshirdi", "Сдал"),
        ["Received"] = ("Qabul qildi", "Принял")
    };

    private static string T(string lang, string key, params object?[] args)
    {
        var s = lang == "ru" ? L[key].Ru : lang == "oz" ? tahsinERP.Localization.UzTransliterator.ToCyrillicCached(L[key].Uz) : L[key].Uz;
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }

    private static string Money(decimal v) => v.ToString("#,0.##", CultureInfo.InvariantCulture).Replace(",", " ");

    public static byte[] Render(ErpInvoice inv, string lang)
    {
        BotFontResolver.EnsureRegistered();
        var doc = new Document();
        doc.Info.Title = "Nakladnoy " + inv.ShipmentNo;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = 9;

        var section = doc.AddSection();
        section.PageSetup = doc.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromMillimeter(15);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(15);
        section.PageSetup.LeftMargin = Unit.FromMillimeter(15);
        section.PageSetup.RightMargin = Unit.FromMillimeter(15);

        var title = section.AddParagraph(T(lang, "Title", inv.ShipmentNo) + (inv.Partial ? " " + T(lang, "Partial") : ""));
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.Format.Alignment = ParagraphAlignment.Center;
        var sub = section.AddParagraph(T(lang, "Order", inv.OrderNo, inv.Date));
        sub.Format.Alignment = ParagraphAlignment.Center;
        sub.Format.SpaceAfter = Unit.FromMillimeter(5);

        var parties = section.AddTable();
        parties.AddColumn(Unit.FromMillimeter(90));
        parties.AddColumn(Unit.FromMillimeter(90));
        var pr = parties.AddRow();
        var left = pr.Cells[0].AddParagraph();
        left.AddFormattedText(T(lang, "Supplier") + ": ", TextFormat.Bold);
        left.AddText(string.Join(", ", new[] { inv.Company, inv.Warehouse }.Where(x => !string.IsNullOrWhiteSpace(x))));
        var right = pr.Cells[1].AddParagraph();
        right.AddFormattedText(T(lang, "Receiver") + ": ", TextFormat.Bold);
        right.AddText(inv.Customer);
        if (!string.IsNullOrWhiteSpace(inv.Inn)) pr.Cells[1].AddParagraph(T(lang, "Inn", inv.Inn));
        var contact = string.Join(", ", new[] { inv.Address, inv.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contact.Length > 0) pr.Cells[1].AddParagraph(contact);
        section.AddParagraph().Format.SpaceAfter = Unit.FromMillimeter(3);

        var t = section.AddTable();
        t.Borders.Width = 0.5;
        foreach (var w in new double[] { 8, 92, 25, 25, 30 }) t.AddColumn(Unit.FromMillimeter(w));
        var head = t.AddRow();
        head.HeadingFormat = true;
        head.Format.Font.Bold = true;
        head.Shading.Color = Colors.LightGray;
        string[] titles = [T(lang, "No"), T(lang, "Product"), T(lang, "Qty"), T(lang, "Price"), T(lang, "Amount")];
        for (int i = 0; i < titles.Length; i++) head.Cells[i].AddParagraph(titles[i]);
        int n = 0;
        foreach (var l in inv.Lines)
        {
            var r = t.AddRow();
            r.Cells[0].AddParagraph((++n).ToString(CultureInfo.InvariantCulture));
            r.Cells[1].AddParagraph(l.Product);
            r.Cells[2].AddParagraph(l.Qty.ToString("0.####", CultureInfo.InvariantCulture) + " " + l.Unit);
            r.Cells[3].AddParagraph(Money(l.Price));
            r.Cells[4].AddParagraph(Money(l.Amount));
            for (int i = 2; i < 5; i++) r.Cells[i].Format.Alignment = ParagraphAlignment.Right;
        }
        var total = t.AddRow();
        total.Format.Font.Bold = true;
        total.Cells[0].MergeRight = 3;
        total.Cells[0].AddParagraph(T(lang, "Total")).Format.Alignment = ParagraphAlignment.Right;
        total.Cells[4].AddParagraph(Money(inv.Total)).Format.Alignment = ParagraphAlignment.Right;

        var after = section.AddParagraph(T(lang, "Debt", Money(inv.Debt)));
        after.Format.SpaceBefore = Unit.FromMillimeter(4);
        if (inv.TripNo != null)
            section.AddParagraph(T(lang, "Delivery", inv.TripNo, inv.Expeditor, inv.Vehicle, inv.DeliveredAt)).Format.Font.Size = 8;

        var sign = section.AddTable();
        sign.AddColumn(Unit.FromMillimeter(90));
        sign.AddColumn(Unit.FromMillimeter(90));
        var sr = sign.AddRow();
        sr.TopPadding = Unit.FromMillimeter(14);
        sr.Cells[0].AddParagraph(T(lang, "Released") + ": ____________________");
        sr.Cells[1].AddParagraph(T(lang, "Received") + ": ____________________");

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>Fayl nomi Telegram'da ko'rinadi: Nakladnoy_DJ-2026-00012.pdf.</summary>
    public static string FileName(ErpInvoice inv) =>
        "Nakladnoy_" + new string(inv.ShipmentNo.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray()) + ".pdf";
}

/// <summary>
/// PDFsharp Core build uchun shrift: Windows'da Arial, Linux'da DejaVu/Liberation, yoki Bot:FontsDir. Kirill va lotin (o', g') chiqadi.
/// </summary>
public sealed class BotFontResolver : IFontResolver
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
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new BotFontResolver();
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
                var path = Path.Combine(dir, bold ? c.Bold : c.Regular);
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "bot-bold" : "bot-regular", false, isItalic);

    public byte[]? GetFont(string faceName)
    {
        var path = Find(faceName == "bot-bold") ?? Find(false)
                   ?? throw new InvalidOperationException("PDF uchun shrift topilmadi (Arial/DejaVu/Liberation). Bot:FontsDir ni sozlang.");
        return File.ReadAllBytes(path);
    }
}
