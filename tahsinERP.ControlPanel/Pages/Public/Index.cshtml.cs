using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Public;

/// <summary>
/// tahsinerp.uz bosh sahifasi (uz: "/", ru: "/ru", en: "/en") va demo so'rovi formasi. Forma JS bilan fetch orqali
/// yuboriladi (JSON javob); JS'siz ham ishlaydi — PRG bilan "#demo" ga qaytadi.
/// </summary>
public class IndexModel(DemoRequestService demos, IOptions<PanelHostOptions> hosts) : PageModel
{
    public const string Phone = "+998 90 770 66 60";
    public const string PhoneHref = "tel:+998907706660";
    public const string Telegram = "https://t.me/khoji_abror";
    public const string TelegramChannel = "https://t.me/schainuz";
    public const string SchainUrl = "https://schain.uz";

    public string Lang { get; private set; } = "uz";
    public LandingContent C { get; private set; } = LandingContent.For("uz");
    public string PortalUrl => "https://" + hosts.Value.EffectivePortalHost;

    [BindProperty] public DemoRequestInput Input { get; set; } = new();
    [TempData] public bool DemoSent { get; set; }
    public string? DemoError { get; private set; }

    public static string PathFor(string lang) => lang == "uz" ? "/" : "/" + lang;

    private bool Resolve(string? lang)
    {
        Lang = string.IsNullOrEmpty(lang) ? "uz" : lang.ToLowerInvariant();
        if (!LandingContent.Languages.Contains(Lang))
            return false;
        C = LandingContent.For(Lang);
        return true;
    }

    public IActionResult OnGet(string? lang)
    {
        if (!Resolve(lang)) return NotFound();
        // Yagona manzil: www.<domen> → <domen> (apex ommaviy host bo'lsa; lokal www.localhost — yo'q).
        string host = Request.Host.Host;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            && hosts.Value.EffectivePublicHosts.Contains(host[4..], StringComparer.OrdinalIgnoreCase))
            return RedirectPermanent($"{Request.Scheme}://{host[4..]}{(Request.Host.Port is int p ? ":" + p : "")}{PathFor(Lang)}");
        if (lang == "uz") return RedirectPermanent("/");
        Response.Headers.CacheControl = "no-cache";
        return Page();
    }

    public async Task<IActionResult> OnPostDemoAsync(string? lang)
    {
        if (!Resolve(lang)) return NotFound();
        bool json = Request.Headers.Accept.Any(a => a?.Contains("application/json") == true);

        string? error = Validate();
        if (error is null)
        {
            var (outcome, _) = await demos.SubmitAsync(Input, Lang, HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(), HttpContext.RequestAborted);
            if (outcome == DemoRequestService.Outcome.Throttled)
                error = "errThrottled";
            // Spam (honeypot) — botga muvaffaqiyat ko'rsatiladi, hech narsa saqlanmaydi.
        }

        if (json)
            return error is null
                ? new JsonResult(new { ok = true, title = C["demo.okTitle"], text = C["demo.okText"] })
                : new JsonResult(new { ok = false, error = C["demo." + error] }) { StatusCode = error == "errThrottled" ? 429 : 400 };
        if (error is not null)
        {
            DemoError = C["demo." + error];
            return Page();
        }
        DemoSent = true;
        return Redirect(PathFor(Lang) + "#demo");
    }

    /// <summary>Xato bo'lsa — matn kaliti (demo.err*), aks holda null.</summary>
    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Input.FullName) || string.IsNullOrWhiteSpace(Input.Company) || string.IsNullOrWhiteSpace(Input.Phone))
            return "errRequired";
        if (!DemoRequestService.IsValidPhone(Input.Phone))
            return "errPhone";
        if (!string.IsNullOrWhiteSpace(Input.Email) && !new EmailAddressAttribute().IsValid(Input.Email.Trim()))
            return "errEmail";
        if (Input.FullName.Length > 200 || Input.Company.Length > 300 || (Input.Message?.Length ?? 0) > 2000 || (Input.Email?.Length ?? 0) > 200)
            return "errGeneric";
        return null;
    }
}
