using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.Services.Bank;

namespace tahsinERP.ControlPanel.Pages.Bank;

/// <summary>Bank ko'chirmasi: yuklash va importlar ro'yxati (Owner/Sales — /Bank papkasi Sales siyosati bilan).</summary>
public class IndexModel(ControlDbContext db, BankImportService bank) : PanelPageModel
{
    public record Row(BankImport Import, int Lines, int Matched, int Applied, long AppliedTiyin);
    public List<Row> Rows { get; private set; } = [];
    [BindProperty] public IFormFile? Upload { get; set; }

    public async Task OnGetAsync()
    {
        var imports = await db.BankImports.AsNoTracking().OrderByDescending(i => i.Id).Take(100).ToListAsync();
        var ids = imports.Select(i => i.Id).ToList();
        var stats = await db.BankImportLines.Where(l => ids.Contains(l.ImportId)).GroupBy(l => l.ImportId)
            .Select(g => new
            {
                g.Key,
                Lines = g.Count(),
                Matched = g.Count(l => l.MatchStatus == BankMatchStatus.Matched && l.State == BankLineState.Pending),
                Applied = g.Count(l => l.State == BankLineState.Applied),
                Sum = g.Where(l => l.State == BankLineState.Applied).Sum(l => (long?)l.AmountTiyin) ?? 0,
            })
            .ToDictionaryAsync(x => x.Key);
        Rows = imports.Select(i => stats.TryGetValue(i.Id, out var s) ? new Row(i, s.Lines, s.Matched, s.Applied, s.Sum) : new Row(i, 0, 0, 0, 0)).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Upload is null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Faylni tanlang.");
            await OnGetAsync();
            return Page();
        }
        if (Upload.Length > BankStatementParser.MaxBytes)
        {
            ModelState.AddModelError(nameof(Upload), "Fayl juda katta (ko'pi bilan 5 MB).");
            await OnGetAsync();
            return Page();
        }
        using var ms = new MemoryStream();
        await Upload.CopyToAsync(ms);
        BankImport? import = null;
        IReadOnlyList<string> warnings = [];
        if (!await TryAsync(async () => (import, warnings) = await bank.ImportAsync(Upload.FileName, ms.ToArray(), StaffId)))
        {
            await OnGetAsync();
            return Page();
        }
        FlashOk = $"Yuklandi: {import!.Lines.Count} qator. Moslashni tekshirib, «Tanlanganlarni qo'llash» tugmasini bosing.";
        if (warnings.Count > 0)
            FlashError = string.Join(" ", warnings.Take(5)) + (warnings.Count > 5 ? $" (+{warnings.Count - 5})" : "");
        return RedirectToPage("Details", new { id = import.Id });
    }
}

public class DetailsModel(ControlDbContext db, BankImportService bank) : PanelPageModel
{
    public BankImport Import { get; private set; } = null!;
    public Dictionary<int, Invoice> Invoices { get; private set; } = [];
    [BindProperty] public List<long> Selected { get; set; } = [];
    [BindProperty] public long LineId { get; set; }
    [BindProperty] public string? InvoiceNumber { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var imp = await db.BankImports.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        if (imp is null) return false;
        Import = imp;
        var ids = imp.Lines.Where(l => l.InvoiceId != null).Select(l => l.InvoiceId!.Value).Distinct().ToList();
        Invoices = await db.Invoices.AsNoTracking().Include(i => i.Tenant).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostApplyAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Selected.Count == 0)
        {
            FlashError = "Hech qaysi qator tanlanmagan.";
            return RedirectToPage(new { id });
        }
        var results = await bank.ApplyAsync(id, Selected, StaffId);
        int ok = results.Count(r => r.Ok);
        FlashOk = $"Qo'llandi: {ok} ta. " + string.Join("; ", results.Where(r => r.Ok).Take(10).Select(r => $"{r.LineNo}-qator: {r.Message}"));
        var failed = results.Where(r => !r.Ok).ToList();
        if (failed.Count > 0)
            FlashError = $"Qo'llanmadi: {failed.Count} ta. " + string.Join("; ", failed.Take(10).Select(r => $"{r.LineNo}-qator: {r.Message}"));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAssignAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => bank.AssignAsync(LineId, InvoiceNumber, StaffId))) return Page();
        FlashOk = string.IsNullOrWhiteSpace(InvoiceNumber) ? "Qator qayta moslandi." : "Hisob-faktura tanlandi.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSkipAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => bank.SkipAsync(LineId, StaffId))) return Page();
        FlashOk = "Qator o'tkazib yuborildi.";
        return RedirectToPage(new { id });
    }

    public static string Of(BankMatchStatus s) => s switch
    {
        BankMatchStatus.Matched => "Mos",
        BankMatchStatus.Ambiguous => "Bir nechta",
        BankMatchStatus.Unmatched => "Topilmadi",
        BankMatchStatus.Duplicate => "Takror",
        BankMatchStatus.NotIncoming => "Chiqim",
        _ => "To'sib qo'yilgan",
    };

    public static string Badge(BankMatchStatus s) => s switch
    {
        BankMatchStatus.Matched => "ok", BankMatchStatus.Ambiguous => "warn", BankMatchStatus.Unmatched => "bad", BankMatchStatus.Blocked => "bad", _ => "muted",
    };

    public static string Of(BankLineState s) => s switch
    {
        BankLineState.Pending => "Kutilmoqda",
        BankLineState.Applied => "Qo'llandi",
        BankLineState.Skipped => "O'tkazildi",
        BankLineState.Applying => "Jarayonda (to'lovlarni tekshiring)",
        _ => "Xato",
    };

    public static bool CanApply(BankImportLine l) =>
        l.MatchStatus == BankMatchStatus.Matched && l.InvoiceId != null && l.State is BankLineState.Pending or BankLineState.Failed;

    public static bool CanEdit(BankImportLine l) =>
        l.State is BankLineState.Pending or BankLineState.Failed && l.MatchStatus is not (BankMatchStatus.NotIncoming or BankMatchStatus.Duplicate);
}
