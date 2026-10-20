using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Invoices;

public class LineInput
{
    public string? Description { get; set; }
    public string? Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? VatRate { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Description) && string.IsNullOrWhiteSpace(UnitPrice);

    public static LineInput From(InvoiceLineInput l) => new()
    {
        Description = l.Description, Quantity = l.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
        UnitPrice = Money.Format(l.UnitPriceTiyin), VatRate = l.VatRatePercent.ToString("0.##", CultureInfo.InvariantCulture),
    };

    public static LineInput From(InvoiceLine l) => From(new InvoiceLineInput(l.Description, l.Quantity, l.UnitPriceTiyin, l.VatRatePercent));

    /// <summary>Forma qatorlarini xizmat kirishiga aylantiradi; xatolar ModelState'ga.</summary>
    public static List<InvoiceLineInput> Parse(IEnumerable<LineInput> lines, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ms)
    {
        var result = new List<InvoiceLineInput>();
        int n = 0;
        foreach (var l in lines)
        {
            n++;
            if (l.IsEmpty) continue;
            if (!decimal.TryParse((l.Quantity ?? "1").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
                ms.AddModelError("", $"{n}-qator: miqdor noto'g'ri.");
            if (!Money.TryParse(l.UnitPrice, out long price))
                ms.AddModelError("", $"{n}-qator: narx noto'g'ri.");
            if (!decimal.TryParse((l.VatRate ?? "12").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var vat))
                ms.AddModelError("", $"{n}-qator: QQS noto'g'ri.");
            result.Add(new InvoiceLineInput(l.Description ?? "", qty, price, vat));
        }
        return result;
    }

    public static List<LineInput> Pad(List<LineInput> lines, int total = 5)
    {
        while (lines.Count < total) lines.Add(new LineInput { Quantity = "1", VatRate = "12" });
        return lines;
    }
}

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public InvoiceStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public bool Overdue { get; set; }
    public List<Invoice> Rows { get; private set; } = [];
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync([FromServices] Clock clock)
    {
        Today = clock.Today;
        var q = db.Invoices.Include(i => i.Tenant).AsNoTracking().AsQueryable();
        if (Status is not null) q = q.Where(i => i.Status == Status);
        if (Overdue) q = q.Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < Today);
        Rows = await q.OrderByDescending(i => i.Id).Take(300).ToListAsync();
    }
}

[Authorize(Policy = Policies.Sales)]
public class CreateModel(ControlDbContext db, BillingService billing) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public int TenantId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SubscriptionId { get; set; }
    [BindProperty] public int PeriodCount { get; set; } = 1;
    [BindProperty] public List<LineInput> Lines { get; set; } = [];
    [BindProperty] public string? Note { get; set; }
    public Tenant Tenant { get; private set; } = null!;
    public List<Subscription> Subs { get; private set; } = [];

    private async Task<bool> LoadAsync()
    {
        var t = await db.Tenants.FindAsync(TenantId);
        if (t is null) return false;
        Tenant = t;
        Subs = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.TenantId == TenantId && (s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.Active) && s.Period != BillingPeriod.Trial)
            .ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var sub = Subs.FirstOrDefault(s => s.Id == SubscriptionId);
        if (sub is not null)
        {
            try { Lines.Add(LineInput.From(BillingService.DefaultLine(sub, sub.Plan!, PeriodCount))); }
            catch (DomainException ex) { ModelState.AddModelError("", ex.Message); }
        }
        LineInput.Pad(Lines);
        return Page();
    }

    /// <summary>Davrlar sonini o'zgartirib, standart qatorni qayta hisoblash.</summary>
    public async Task<IActionResult> OnPostRecalcAsync()
    {
        if (!await LoadAsync()) return NotFound();
        ModelState.Clear();
        Lines = [];
        var sub = Subs.FirstOrDefault(s => s.Id == SubscriptionId);
        if (sub is not null)
        {
            try { Lines.Add(LineInput.From(BillingService.DefaultLine(sub, sub.Plan!, PeriodCount))); }
            catch (DomainException ex) { ModelState.AddModelError("", ex.Message); }
        }
        LineInput.Pad(Lines);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var lines = LineInput.Parse(Lines, ModelState);
        if (!ModelState.IsValid) { LineInput.Pad(Lines); return Page(); }
        Invoice? inv = null;
        if (!await TryAsync(async () => inv = await billing.CreateDraftAsync(TenantId, SubscriptionId, PeriodCount, lines, Note, StaffId)))
        {
            LineInput.Pad(Lines);
            return Page();
        }
        FlashOk = "Qoralama yaratildi. Tekshirib, «Chiqarish» tugmasini bosing (raqam beriladi).";
        return RedirectToPage("Details", new { id = inv!.Id });
    }
}

[Authorize(Policy = Policies.Sales)]
public class EditModel(ControlDbContext db, BillingService billing) : PanelPageModel
{
    [BindProperty] public List<LineInput> Lines { get; set; } = [];
    [BindProperty] public DateOnly DueDate { get; set; }
    [BindProperty] public string? Note { get; set; }
    public Invoice Invoice { get; private set; } = null!;

    private async Task<bool> LoadAsync(int id)
    {
        var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Tenant).FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return false;
        Invoice = inv;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Invoice.Status != InvoiceStatus.Draft) return RedirectToPage("Details", new { id });
        Lines = LineInput.Pad(Invoice.Lines.OrderBy(l => l.LineNo).Select(LineInput.From).ToList());
        DueDate = Invoice.DueDate; Note = Invoice.Note;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var lines = LineInput.Parse(Lines, ModelState);
        if (!ModelState.IsValid || !await TryAsync(() => billing.UpdateDraftAsync(Invoice, lines, DueDate, Note)))
        {
            LineInput.Pad(Lines);
            return Page();
        }
        FlashOk = "Qoralama saqlandi.";
        return RedirectToPage("Details", new { id });
    }
}

public class DetailsModel(ControlDbContext db, BillingService billing, InvoicePdfRenderer pdf, Clock clock, DidoxService didox) : PanelPageModel
{
    public bool DidoxEnabled => didox.Enabled;
    public IReadOnlyList<string> DidoxMissing => didox.Missing;

    public Invoice Invoice { get; private set; } = null!;
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    [BindProperty] public string? Amount { get; set; }
    [BindProperty] public DateOnly PaidAt { get; set; }
    [BindProperty] public PaymentMethod Method { get; set; } = PaymentMethod.Bank;
    [BindProperty] public string? Reference { get; set; }
    [BindProperty] public string? PaymentNote { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public int PaymentId { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Tenant)
            .Include(i => i.Subscription).ThenInclude(s => s!.Plan).FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return false;
        Invoice = inv;
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        PaidAt = clock.Today;
        Amount = Money.Format(Invoice.RemainingTiyin);
        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        byte[] bytes = pdf.Render(Invoice, Invoice.Tenant!);
        return File(bytes, "application/pdf", (Invoice.Number ?? $"qoralama-{Invoice.Id}") + ".pdf");
    }

    public async Task<IActionResult> OnPostIssueAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => billing.IssueAsync(Invoice))) return Page();
        FlashOk = $"Hisob-faktura chiqarildi: {Invoice.Number}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => billing.CancelAsync(Invoice, Reason ?? ""))) return Page();
        FlashOk = "Hisob-faktura bekor qilindi.";
        return RedirectToPage(new { id });
    }

    /// <summary>"To'lov qabul qilindi" — Owner va Sales (00_PLAN.md 5.2).</summary>
    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!Money.TryParse(Amount, out long tiyin))
        {
            ModelState.AddModelError(nameof(Amount), "Summa noto'g'ri.");
            return Page();
        }
        PaymentResult? result = null;
        if (!await TryAsync(async () => result = await billing.RecordPaymentAsync(id, tiyin, PaidAt, Method, Reference, PaymentNote, StaffId)))
        {
            await LoadAsync(id);
            return Page();
        }
        var r = result!;
        FlashOk = $"To'lov belgilandi: {Money.FormatSom(tiyin)}. Holat: {Labels.Of(r.Invoice.Status)}."
                  + (r.IssuedLicense is not null ? $" Obuna uzaytirildi, yangi kalit chiqdi (#{r.IssuedLicense.Id}, {r.IssuedLicense.ExpiryDate:dd.MM.yyyy} gacha)." : "");
        if (r.LicenseWarning is not null) FlashError = r.LicenseWarning;
        return RedirectToPage(new { id });
    }

    /// <summary>Didox'ga e-hisob-faktura qoralamasi sifatida yuborish (Owner/Sales). Imzo — Didox kabinetida E-IMZO bilan.</summary>
    public async Task<IActionResult> OnPostDidoxSendAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => didox.SendAsync(id, StaffId))) { await LoadAsync(id); return Page(); }
        FlashOk = "Didox'ga yuborildi (qoralama). Didox kabinetida E-IMZO bilan imzolang — holat avtomatik yangilanadi.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDidoxRefreshAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var state = await didox.RefreshAsync(Invoice);
        if (state is null) FlashError = "Didox holati olinmadi: " + (Invoice.DidoxError ?? "Didox o'chiq.");
        else FlashOk = "Didox holati: " + state;
        return RedirectToPage(new { id });
    }

    /// <summary>Storno — faqat Owner, sabab majburiy.</summary>
    public async Task<IActionResult> OnPostReverseAsync(int id)
    {
        if (!IsOwner) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => billing.ReversePaymentAsync(PaymentId, Reason ?? "", StaffId))) return Page();
        FlashOk = "To'lov storno qilindi. Obuna va kalit o'zgarmadi — kerak bo'lsa qo'lda hal qiling.";
        return RedirectToPage(new { id });
    }
}
