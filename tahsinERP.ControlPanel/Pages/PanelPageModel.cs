using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
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
            ModelState.AddModelError(string.Empty, L.T("Pnl_ConcurrencyConflict"));
        }
        return false;
    }
}

/// <summary>Validatsiya atributlari uchun matnlar: <c>[Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.X))]</c>
/// (atribut ichida L.T ishlatib bo'lmaydi; xususiyat har safar joriy tilda o'qiladi).</summary>
public static class PnlVal
{
    public static string EmailRequired => L.T("Pnl_Val_EmailRequired");
    public static string EmailInvalid => L.T("Pnl_Val_EmailInvalid");
    public static string PasswordRequired => L.T("Pnl_Val_PasswordRequired");
    public static string CodeRequired => L.T("Pnl_Val_CodeRequired");
    public static string AppCodeRequired => L.T("Pnl_Val_AppCodeRequired");
    public static string CurrentPassword => L.T("Pnl_Val_CurrentPassword");
    public static string NewPassword => L.T("Pnl_Val_NewPassword");
    public static string Repeat => L.T("Pnl_Val_Repeat");
    public static string NameRequired => L.T("Pnl_Val_NameRequired");
    public static string HostRequired => L.T("Pnl_Val_HostRequired");
    public static string SqlInstanceRequired => L.T("Pnl_Val_SqlInstanceRequired");
    public static string EmailMandatory => L.T("Pnl_Val_EmailMandatory");
    public static string FullNameRequired => L.T("Pnl_Val_FullNameRequired");
    public static string FieldRequired => L.T("Pnl_Val_FieldRequired");
    public static string DevLoginFormat => L.T("Pnl_Val_DevLoginFormat");
    public static string TempPassword => L.T("Pnl_Val_TempPassword");
    public static string SlugRequired => L.T("Pnl_Val_SlugRequired");
    public static string LegalNameRequired => L.T("Pnl_Val_LegalNameRequired");
    public static string PositiveNumber => L.T("Pnl_Val_PositiveNumber");
}

public static class Labels
{
    public static string Of(TenantStatus s) => s switch
    {
        TenantStatus.Trial => L.T("Pnl_TenantStatus_Trial"),
        TenantStatus.Active => L.T("Pnl_TenantStatus_Active"),
        TenantStatus.Grace => L.T("Pnl_TenantStatus_Grace"),
        TenantStatus.Suspended => L.T("Pnl_TenantStatus_Suspended"),
        TenantStatus.Archived => L.T("Pnl_TenantStatus_Archived"),
        _ => s.ToString()
    };

    public static string Of(DeploymentKind d) => d == DeploymentKind.SaaS ? L.T("Pnl_Deployment_SaaS") : L.T("Pnl_Deployment_OnPrem");

    public static string Of(SubscriptionStatus s) => s switch
    {
        SubscriptionStatus.Pending => L.T("Pnl_SubStatus_Pending"),
        SubscriptionStatus.Active => L.T("Pnl_SubStatus_Active"),
        SubscriptionStatus.Expired => L.T("Pnl_SubStatus_Expired"),
        SubscriptionStatus.Cancelled => L.T("Pnl_SubStatus_Cancelled"),
        _ => s.ToString()
    };

    public static string Of(BillingPeriod p) => p switch { BillingPeriod.Month => L.T("Pnl_Period_Month"), BillingPeriod.Year => L.T("Pnl_Period_Year"), _ => L.T("Pnl_Period_Trial") };

    public static string Of(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft => L.T("Pnl_InvStatus_Draft"),
        InvoiceStatus.Issued => L.T("Pnl_InvStatus_Issued"),
        InvoiceStatus.PartiallyPaid => L.T("Pnl_InvStatus_PartiallyPaid"),
        InvoiceStatus.Paid => L.T("Pnl_InvStatus_Paid"),
        InvoiceStatus.Cancelled => L.T("Pnl_InvStatus_Cancelled"),
        _ => s.ToString()
    };

    public static string Of(PaymentMethod m) => m switch
    {
        PaymentMethod.Bank => L.T("Pnl_PayMethod_Bank"),
        PaymentMethod.Cash => L.T("Pnl_PayMethod_Cash"),
        PaymentMethod.Card => L.T("Pnl_PayMethod_Card"),
        _ => L.T("Pnl_PayMethod_Other")
    };

    public static string Of(StaffRole r) => r switch { StaffRole.Owner => L.T("Pnl_Role_Owner"), StaffRole.Sales => L.T("Pnl_RoleSales"), _ => L.T("Pnl_RoleSupport") };

    public static string Of(LicenseSource s) => s switch { LicenseSource.Auto => L.T("Pnl_LicSource_Auto"), LicenseSource.Manual => L.T("Pnl_LicSource_Manual"), _ => L.T("Pnl_LicSource_Registration") };

    public static string Of(NotificationStatus s) => s switch { NotificationStatus.Sent => L.T("Pnl_NotifStatus_Sent"), NotificationStatus.Logged => L.T("Pnl_NotifStatus_Logged"), _ => L.T("Pnl_NotifStatus_Failed") };

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
        ProvisioningJobType.Create => L.T("Pnl_JobType_Create"),
        ProvisioningJobType.Reset => L.T("Pnl_JobType_Reset"),
        ProvisioningJobType.Suspend => L.T("Pnl_JobType_Suspend"),
        ProvisioningJobType.Resume => L.T("Pnl_JobType_Resume"),
        ProvisioningJobType.Archive => L.T("Pnl_JobType_Archive"),
        ProvisioningJobType.Delete => L.T("Pnl_JobType_Delete"),
        ProvisioningJobType.EndTrial => L.T("Pnl_JobType_EndTrial"),
        ProvisioningJobType.SiteDomain => L.T("Pnl_JobType_SiteDomain"),
        _ => t.ToString()
    };

    public static string Of(ProvisioningJobStatus s) => s switch
    {
        ProvisioningJobStatus.Pending => L.T("Pnl_JobStatus_Pending"),
        ProvisioningJobStatus.Running => L.T("Pnl_JobStatus_Running"),
        ProvisioningJobStatus.Succeeded => L.T("Pnl_JobStatus_Succeeded"),
        ProvisioningJobStatus.Failed => L.T("Pnl_JobStatus_Failed"),
        ProvisioningJobStatus.Cancelled => L.T("Pnl_JobStatus_Cancelled"),
        _ => s.ToString()
    };

    public static string Badge(ProvisioningJobStatus s) => s switch
    {
        ProvisioningJobStatus.Succeeded => "ok", ProvisioningJobStatus.Running => "info", ProvisioningJobStatus.Pending => "warn",
        ProvisioningJobStatus.Failed => "bad", _ => "muted"
    };

    public static string Of(SiteDomainStatus s) => s switch
    {
        SiteDomainStatus.PendingDns => L.T("Pnl_SDom_St_PendingDns"),
        SiteDomainStatus.Binding => L.T("Pnl_SDom_St_Binding"),
        SiteDomainStatus.Active => L.T("Pnl_SDom_St_Active"),
        SiteDomainStatus.Failed => L.T("Pnl_SDom_St_Failed"),
        SiteDomainStatus.Removing => L.T("Pnl_SDom_St_Removing"),
        _ => s.ToString()
    };

    public static string Badge(SiteDomainStatus s) => s switch
    {
        SiteDomainStatus.Active => "ok", SiteDomainStatus.Binding or SiteDomainStatus.Removing => "info", SiteDomainStatus.PendingDns => "warn",
        _ => "bad"
    };

    public static string Month(int m) => m switch
    {
        1 => L.T("Month_1"), 2 => L.T("Month_2"), 3 => L.T("Month_3"), 4 => L.T("Month_4"), 5 => L.T("Month_5"), 6 => L.T("Month_6"),
        7 => L.T("Month_7"), 8 => L.T("Month_8"), 9 => L.T("Month_9"), 10 => L.T("Month_10"), 11 => L.T("Month_11"), _ => L.T("Month_12")
    };
}
