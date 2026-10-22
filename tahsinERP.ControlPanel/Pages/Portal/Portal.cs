using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Portal;

/// <summary>Bosh sahifa: paket, obuna holati va muddati, foydalanuvchi limiti, qolgan kunlar, ogohlantirishlar.</summary>
public class IndexModel(ControlDbContext db, PortalRequestService requests, Clock clock, IOptions<PanelOptions> panel) : PortalPageModel
{
    public TenantOverview Overview { get; private set; } = null!;
    public List<Invoice> Unpaid { get; private set; } = [];
    public PortalRequest? OpenRequest { get; private set; }
    public DateOnly Today { get; private set; }
    public int GraceDays => panel.Value.GraceDays;

    public async Task OnGetAsync()
    {
        Today = clock.Today;
        Overview = await requests.OverviewAsync(TenantId);
        Unpaid = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == TenantId && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid))
            .OrderBy(i => i.DueDate).ToListAsync();
        OpenRequest = await db.PortalRequests.AsNoTracking().Include(r => r.Plan)
            .Where(r => r.TenantId == TenantId && r.Status == PortalRequestStatus.Pending).FirstOrDefaultAsync();
    }
}

/// <summary>Hisob-fakturalar: ro'yxat (qoralamalar ko'rinmaydi), holat, PDF.</summary>
public class InvoicesModel(ControlDbContext db, InvoicePdfRenderer pdf, AuditLogger audit, Clock clock) : PortalPageModel
{
    public List<Invoice> Rows { get; private set; } = [];
    public DateOnly Today { get; private set; }

    /// <summary>Mijozga faqat raqamlangan (chiqarilgan) hisob-fakturalar — va faqat o'z tenant'iniki.</summary>
    private IQueryable<Invoice> Visible() =>
        db.Invoices.Where(i => i.TenantId == TenantId && i.Number != null && i.Status != InvoiceStatus.Draft);

    public async Task OnGetAsync()
    {
        Today = clock.Today;
        Rows = await Visible().AsNoTracking().OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).ToListAsync();
    }

    public async Task<IActionResult> OnGetPdfAsync(int id)
    {
        var inv = await Visible().Include(i => i.Lines).Include(i => i.Tenant).FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return NotFound();
        byte[] bytes = pdf.Render(inv, inv.Tenant!);
        audit.Add("portal.invoice.pdf", "Invoice", inv.Id, after: new { inv.Number }, portalUserId: PortalUserId);
        await db.SaveChangesAsync();
        return File(bytes, "application/pdf", inv.Number + ".pdf");
    }
}

/// <summary>To'lov rekvizitlari (sozlamadan) va to'lov maqsadiga hisob-faktura raqamini yozish ko'rsatmasi. Onlayn to'lov yo'q.</summary>
public class PaymentModel(ControlDbContext db, IOptions<VendorOptions> vendor) : PortalPageModel
{
    public VendorOptions Vendor => vendor.Value;
    public bool VendorReady => !string.IsNullOrWhiteSpace(Vendor.LegalName) && !string.IsNullOrWhiteSpace(Vendor.BankAccount);
    public List<Invoice> Unpaid { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int? Invoice { get; set; }

    public async Task OnGetAsync()
    {
        Unpaid = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == TenantId && i.Number != null && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid))
            .OrderBy(i => i.DueDate).ToListAsync();
        if (Invoice is int id && Unpaid.Any(i => i.Id == id))
            Unpaid = Unpaid.OrderBy(i => i.Id == id ? 0 : 1).ToList();
    }
}

/// <summary>Tarifni oshirish / uzaytirish so'rovi.</summary>
public class RequestsModel(ControlDbContext db, PortalRequestService service, Clock clock) : PortalPageModel
{
    public List<Plan> Plans { get; private set; } = [];
    public List<PortalRequest> Rows { get; private set; } = [];
    public Dictionary<int, string> InvoiceNumbers { get; private set; } = [];
    public TenantOverview Overview { get; private set; } = null!;
    public DateOnly Today { get; private set; }
    [BindProperty] public int PlanId { get; set; }
    [BindProperty] public BillingPeriod Period { get; set; } = BillingPeriod.Month;
    [BindProperty] public int PeriodCount { get; set; } = 1;
    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public int RequestId { get; set; }

    private async Task LoadAsync()
    {
        Today = clock.Today;
        Overview = await service.OverviewAsync(TenantId);
        Plans = await db.Plans.AsNoTracking().Where(p => p.IsActive && p.Code != PlanCodes.Trial && !p.IsAddOn).OrderBy(p => p.SortOrder).ToListAsync();
        Rows = await db.PortalRequests.AsNoTracking().Include(r => r.Plan).Where(r => r.TenantId == TenantId)
            .OrderByDescending(r => r.Id).Take(50).ToListAsync();
        var invIds = Rows.Where(r => r.InvoiceId != null).Select(r => r.InvoiceId!.Value).ToList();
        InvoiceNumbers = await db.Invoices.AsNoTracking().Where(i => invIds.Contains(i.Id) && i.Number != null)
            .ToDictionaryAsync(i => i.Id, i => i.Number!);
        if (PlanId == 0)
            PlanId = Overview.Current?.Period != BillingPeriod.Trial && Overview.Current?.PlanId is int cur ? cur : Plans.FirstOrDefault()?.Id ?? 0;
    }

    /// <summary>Tanlangan paket bo'yicha nima bo'lishini oldindan tushuntirish (sahifada).</summary>
    public string Explain(Plan p, BillingPeriod period)
    {
        var (kind, eff) = PortalRequestService.Classify(Overview.Current, p, period, Today);
        return kind switch
        {
            PortalRequestKind.Renew => L.T("Ptl_ExplainRenew", eff.ToString("dd.MM.yyyy")),
            PortalRequestKind.Upgrade => L.T("Ptl_ExplainUpgrade"),
            PortalRequestKind.ChangeAtPeriodEnd => L.T("Ptl_ExplainChange", eff.ToString("dd.MM.yyyy")),
            _ => L.T("Ptl_ExplainNew"),
        };
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsPortalOwner) return OwnerOnly();
        await LoadAsync();
        if (!await TryAsync(() => service.CreateAsync(TenantId, PortalUserId, PlanId, Period, PeriodCount, Comment)))
            return Page();
        FlashOk = L.T("Ptl_RequestSent");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        if (!IsPortalOwner) return OwnerOnly();
        await LoadAsync();
        if (!await TryAsync(() => service.CancelAsync(RequestId, TenantId, PortalUserId)))
            return Page();
        FlashOk = L.T("Ptl_RequestCancelled");
        return RedirectToPage();
    }
}

/// <summary>On-prem mijoz: o'z o'rnatmalari (InstallationId) bo'yicha amaldagi kalit. SaaS tenant uchun sahifa yo'q (404).</summary>
public class LicensesModel(ControlDbContext db, AuditLogger audit, Clock clock) : PortalPageModel
{
    public List<License> Current { get; private set; } = [];
    public License? Revealed { get; private set; }
    public DateOnly Today { get; private set; }
    [BindProperty] public int LicenseId { get; set; }

    /// <summary>Tenant'ning amaldagi kalitlari: almashtirilmagan, bekor qilinmagan; har o'rnatma uchun oxirgisi.</summary>
    private async Task<bool> LoadAsync()
    {
        if (CurrentTenant is not { Deployment: DeploymentKind.OnPrem }) return false;
        Today = clock.Today;
        var all = await db.Licenses.AsNoTracking()
            .Where(l => l.TenantId == TenantId && l.SupersededById == null && l.RevokedAtUtc == null)
            .OrderByDescending(l => l.IssuedAtUtc).ThenByDescending(l => l.Id).ToListAsync();
        Current = all.GroupBy(l => l.InstallationId).Select(g => g.First()).ToList();
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    /// <summary>Kalit matnini ko'rsatish (nusxa olish uchun) — faqat Owner, auditga yoziladi.</summary>
    public async Task<IActionResult> OnPostRevealAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (!IsPortalOwner) return OwnerOnly();
        Revealed = Current.FirstOrDefault(l => l.Id == LicenseId);
        if (Revealed is null) return NotFound();
        audit.Add("portal.license.view", "License", Revealed.Id, after: new { Revealed.InstallationId }, portalUserId: PortalUserId);
        await db.SaveChangesAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        if (!await LoadAsync()) return NotFound();
        if (!IsPortalOwner) return OwnerOnly();
        var lic = Current.FirstOrDefault(l => l.Id == id);
        if (lic is null) return NotFound();
        audit.Add("portal.license.download", "License", lic.Id, after: new { lic.InstallationId }, portalUserId: PortalUserId);
        await db.SaveChangesAsync();
        return File(Encoding.UTF8.GetBytes(lic.LicenseKey.Trim() + "\r\n"), "text/plain", $"tahsinERP-license-{lic.InstallationId[..8]}.txt");
    }
}

/// <summary>Profil: parolni almashtirish, Telegram bog'lash havolasi.</summary>
public class ProfileModel(ControlDbContext db, PortalAuthService auth, IOptions<TelegramOptions> tg) : PortalPageModel
{
    public PortalUser Me { get; private set; } = null!;
    /// <summary>Hozir ishlaydigan kod kanallari va joriy tanlangan kanal (4-bosqich).</summary>
    public IReadOnlyList<OtpTarget> Channels { get; private set; } = [];
    public string? ActiveChannel { get; private set; }
    [BindProperty] public string? Channel { get; set; }
    public string? LinkToken { get; private set; }
    public string LinkUrl => LinkToken is null ? "" : PortalFlow.TelegramLink(tg.Value, LinkToken);
    [BindProperty] public string? Current { get; set; }
    [BindProperty] public string? New { get; set; }
    [BindProperty] public string? Confirm { get; set; }

    private async Task<bool> LoadAsync()
    {
        var me = await db.PortalUsers.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == PortalUserId);
        if (me is null) return false;
        Me = me;
        Channels = auth.AvailableChannels(me, me.Tenant!);
        ActiveChannel = auth.ResolveTarget(me, me.Tenant!, out _)?.Channel;
        return true;
    }

    public async Task<IActionResult> OnPostChannelAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Portal/Login");
        if (!await TryAsync(() => auth.SetOtpChannelAsync(Me, Me.Tenant!, Channel))) return Page();
        FlashOk = L.T("Ptl_ChannelSaved");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : RedirectToPage("/Portal/Login");

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Portal/Login");
        if (!await TryAsync(() => auth.ChangePasswordAsync(Me, Current ?? "", New ?? "", Confirm ?? "")))
            return Page();
        // SecurityStamp almashdi — joriy sessiya yangilanadi, boshqa qurilmalardagilari yopiladi.
        await HttpContext.SignInAsync(PortalSchemes.Portal, PortalPrincipal.Build(Me, PortalSchemes.Portal));
        FlashOk = L.T("Ptl_PasswordChanged");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTelegramAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Portal/Login");
        LinkToken = await auth.CreateTelegramLinkAsync(Me);
        return Page();
    }
}
