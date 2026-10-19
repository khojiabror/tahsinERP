using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Portal;

namespace tahsinERP.ControlPanel.Pages.Requests;

/// <summary>
/// Mijoz portali so'rovlari navbati (tarifni oshirish / uzaytirish). Tasdiqlash → qoralama hisob-faktura avtomatik
/// (xodim tekshirib chiqaradi); rad etish — sabab bilan. Ikkalasida ham mijozga Telegram xabar. Owner va Sales.
/// </summary>
public class IndexModel(ControlDbContext db, PortalRequestService service) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public PortalRequestStatus? Status { get; set; } = PortalRequestStatus.Pending;
    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    [BindProperty] public int RequestId { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public List<PortalRequest> Rows { get; private set; } = [];
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    public Dictionary<int, (string? Number, InvoiceStatus Status)> Invoices { get; private set; } = [];

    private async Task LoadAsync()
    {
        var q = db.PortalRequests.AsNoTracking().Include(r => r.Tenant).Include(r => r.Plan).Include(r => r.PortalUser).AsQueryable();
        if (!All && Status is not null) q = q.Where(r => r.Status == Status);
        Rows = await q.OrderByDescending(r => r.Id).Take(300).ToListAsync();
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        var ids = Rows.Where(r => r.InvoiceId != null).Select(r => r.InvoiceId!.Value).ToList();
        Invoices = await db.Invoices.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => (i.Number, i.Status));
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!CanSell) return Forbidden();
        Invoice? inv = null;
        if (!await TryAsync(async () => inv = await service.ApproveAsync(RequestId, StaffId)))
        {
            await LoadAsync();
            return Page();
        }
        FlashOk = $"So'rov #{RequestId} tasdiqlandi, qoralama hisob-faktura yaratildi — tekshirib «Chiqarish» tugmasini bosing.";
        return RedirectToPage("/Invoices/Details", new { id = inv!.Id });
    }

    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!CanSell) return Forbidden();
        if (!await TryAsync(() => service.RejectAsync(RequestId, Reason ?? "", StaffId)))
        {
            await LoadAsync();
            return Page();
        }
        FlashOk = $"So'rov #{RequestId} rad etildi, mijozga xabar yuborildi.";
        return RedirectToPage();
    }

    public static string Of(PortalRequestStatus s) => Portal.PortalPageModel.Of(s);
    public static string Badge(PortalRequestStatus s) => Portal.PortalPageModel.Badge(s);
}
