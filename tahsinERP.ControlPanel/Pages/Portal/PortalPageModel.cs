using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Portal;

/// <summary>Mijoz portali sahifalari asosi: joriy mijoz va uning tenant'i, rol, flash xabarlar.</summary>
public abstract class PortalPageModel : PageModel
{
    [Microsoft.AspNetCore.Mvc.TempData] public string? FlashOk { get; set; }
    [Microsoft.AspNetCore.Mvc.TempData] public string? FlashError { get; set; }

    public bool SignedIn => User.Identity?.IsAuthenticated == true && User.Identity.AuthenticationType == PortalSchemes.Portal;
    public int PortalUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public int TenantId => int.Parse(User.FindFirstValue(PortalClaims.Tenant)!);
    /// <summary>Owner: so'rov yuborish, kalitni ko'rish/yuklab olish. Viewer: faqat ko'rish.</summary>
    public bool IsPortalOwner => User.FindFirstValue(PortalClaims.Role) == nameof(PortalRole.Owner);
    /// <summary>Kirgan mijozning tenant'i (layout menyusi uchun ham).</summary>
    public Tenant? CurrentTenant { get; private set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (SignedIn)
        {
            var db = HttpContext.RequestServices.GetRequiredService<ControlDbContext>();
            CurrentTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == TenantId);
        }
        await next();
    }

    /// <summary>Viewer roli o'zgartiruvchi amalni bajara olmaydi.</summary>
    protected Microsoft.AspNetCore.Mvc.IActionResult OwnerOnly()
    {
        FlashError = "Bu amal faqat tashkilot mas'uli (Owner roli) uchun.";
        return RedirectToPage("/Portal/Index");
    }

    protected async Task<bool> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex) when (BillingService.IsConcurrencyConflict(ex))
        {
            ModelState.AddModelError(string.Empty, "Ma'lumot shu orada o'zgardi — sahifani yangilab, qayta urinib ko'ring.");
        }
        return false;
    }

    public static string InvoiceState(Invoice i) => i.Status switch
    {
        InvoiceStatus.Paid => "To'langan",
        InvoiceStatus.PartiallyPaid => "Qisman to'langan",
        InvoiceStatus.Issued => "To'lanmagan",
        InvoiceStatus.Cancelled => "Bekor qilingan",
        _ => "Tayyorlanmoqda",
    };

    public static string InvoiceBadge(Invoice i, DateOnly today) => i.Status switch
    {
        InvoiceStatus.Paid => "ok",
        InvoiceStatus.PartiallyPaid => "warn",
        InvoiceStatus.Issued => i.DueDate < today ? "bad" : "info",
        _ => "muted",
    };

    public static string Of(PortalRequestStatus s) => s switch
    {
        PortalRequestStatus.Pending => "Ko'rib chiqilmoqda",
        PortalRequestStatus.Approved => "Tasdiqlangan",
        PortalRequestStatus.Rejected => "Rad etilgan",
        _ => "Bekor qilingan",
    };

    public static string Badge(PortalRequestStatus s) => s switch
    {
        PortalRequestStatus.Pending => "warn", PortalRequestStatus.Approved => "ok", PortalRequestStatus.Rejected => "bad", _ => "muted",
    };

    public static string PaymentPurpose(Invoice i) =>
        $"{i.Number} hisob-faktura bo'yicha tahsinERP dasturiy ta'minot xizmati uchun to'lov, QQS bilan";
}
