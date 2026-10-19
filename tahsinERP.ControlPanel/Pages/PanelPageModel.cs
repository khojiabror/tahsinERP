using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages;

/// <summary>Umumiy sahifa asosi: joriy xodim, rol tekshiruvi, flash xabarlar.</summary>
public abstract class PanelPageModel : PageModel
{
    [TempData] public string? FlashOk { get; set; }
    [TempData] public string? FlashError { get; set; }

    public int StaffId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public bool IsOwner => User.IsInRole(nameof(StaffRole.Owner));
    public bool CanSell => IsOwner || User.IsInRole(nameof(StaffRole.Sales));

    protected IActionResult Forbidden() => RedirectToPage("/Account/Denied");

    /// <summary>Biznes xatosini forma ustida ko'rsatadi; boshqa xatolar tashqariga chiqadi.</summary>
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
            ModelState.AddModelError(string.Empty, "Ma'lumot shu orada boshqa xodim tomonidan o'zgartirildi — sahifani yangilab, qayta urinib ko'ring.");
        }
        return false;
    }
}

public static class Labels
{
    public static string Of(TenantStatus s) => s switch
    {
        TenantStatus.Trial => "Sinov",
        TenantStatus.Active => "Faol",
        TenantStatus.Grace => "Imtiyozli muddat",
        TenantStatus.Suspended => "To'xtatilgan",
        TenantStatus.Archived => "Arxiv",
        _ => s.ToString()
    };

    public static string Of(DeploymentKind d) => d == DeploymentKind.SaaS ? "SaaS (bulut)" : "On-prem (mijoz serveri)";

    public static string Of(SubscriptionStatus s) => s switch
    {
        SubscriptionStatus.Pending => "To'lov kutilmoqda",
        SubscriptionStatus.Active => "Faol",
        SubscriptionStatus.Expired => "Tugagan",
        SubscriptionStatus.Cancelled => "Bekor qilingan",
        _ => s.ToString()
    };

    public static string Of(BillingPeriod p) => p switch { BillingPeriod.Month => "Oylik", BillingPeriod.Year => "Yillik", _ => "Sinov" };

    public static string Of(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft => "Qoralama",
        InvoiceStatus.Issued => "Chiqarilgan",
        InvoiceStatus.PartiallyPaid => "Qisman to'langan",
        InvoiceStatus.Paid => "To'langan",
        InvoiceStatus.Cancelled => "Bekor qilingan",
        _ => s.ToString()
    };

    public static string Of(PaymentMethod m) => m switch
    {
        PaymentMethod.Bank => "Bank o'tkazmasi",
        PaymentMethod.Cash => "Naqd",
        PaymentMethod.Card => "Karta",
        _ => "Boshqa"
    };

    public static string Of(StaffRole r) => r switch { StaffRole.Owner => "Egasi (Owner)", StaffRole.Sales => "Sotuv", _ => "Qo'llab-quvvatlash" };

    public static string Of(LicenseSource s) => s switch { LicenseSource.Auto => "Avtomatik", LicenseSource.Manual => "Qo'lda", _ => "Ro'yxatdan o'tish" };

    public static string Of(NotificationStatus s) => s switch { NotificationStatus.Sent => "Yuborildi", NotificationStatus.Logged => "Faqat log", _ => "Xato" };

    public static string Badge(TenantStatus s) => s switch
    {
        TenantStatus.Active => "ok", TenantStatus.Trial => "info", TenantStatus.Grace => "warn", TenantStatus.Suspended => "bad", _ => "muted"
    };

    public static string Badge(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Paid => "ok", InvoiceStatus.PartiallyPaid => "warn", InvoiceStatus.Issued => "info", InvoiceStatus.Cancelled => "muted", _ => "muted"
    };

    public static string Of(ProvisioningJobType t) => t switch
    {
        ProvisioningJobType.Create => "Yaratish",
        ProvisioningJobType.Reset => "Qayta tiklash",
        ProvisioningJobType.Suspend => "To'xtatish",
        ProvisioningJobType.Resume => "Qayta yoqish",
        ProvisioningJobType.Archive => "Arxivlash",
        ProvisioningJobType.Delete => "O'chirish",
        ProvisioningJobType.EndTrial => "Sinovni yakunlash",
        _ => t.ToString()
    };

    public static string Of(ProvisioningJobStatus s) => s switch
    {
        ProvisioningJobStatus.Pending => "Navbatda",
        ProvisioningJobStatus.Running => "Bajarilmoqda",
        ProvisioningJobStatus.Succeeded => "Bajarildi",
        ProvisioningJobStatus.Failed => "Xato",
        ProvisioningJobStatus.Cancelled => "Bekor qilindi",
        _ => s.ToString()
    };

    public static string Badge(ProvisioningJobStatus s) => s switch
    {
        ProvisioningJobStatus.Succeeded => "ok", ProvisioningJobStatus.Running => "info", ProvisioningJobStatus.Pending => "warn",
        ProvisioningJobStatus.Failed => "bad", _ => "muted"
    };

    public static string Month(int m) => m switch
    {
        1 => "Yanvar", 2 => "Fevral", 3 => "Mart", 4 => "Aprel", 5 => "May", 6 => "Iyun",
        7 => "Iyul", 8 => "Avgust", 9 => "Sentabr", 10 => "Oktabr", 11 => "Noyabr", _ => "Dekabr"
    };
}
