using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.Localization;

namespace tahsinERP.ControlPanel.Portal;

// Portal kod kanallari — SMS (Eskiz.uz) va email (SMTP), 4-bosqich (08_STAGE4.md 5). Sozlanmagan bo'lsa IsConfigured = false:
// kanal tanlovda ko'rinmaydi va kod Telegram'ga ketadi. Haqiqiy Eskiz/SMTP hisobi bilan TEKSHIRILMAGAN (faqat soxta sender'lar bilan).

public class EskizOptions
{
    public string BaseUrl { get; set; } = "https://notify.eskiz.uz/api";
    /// <summary>Eskiz kabinet logini (email). Bo'sh — SMS o'chiq.</summary>
    public string? Email { get; set; }
    /// <summary>Eskiz API paroli (kalit). Faqat env/user-secrets.</summary>
    public string? Password { get; set; }
    /// <summary>Jo'natuvchi nomi (Eskiz'da tasdiqlangan nik); standart 4546 — Eskiz test nomi.</summary>
    public string From { get; set; } = "4546";
}

public class SmtpOptions
{
    /// <summary>SMTP server (masalan email-smtp.eu-central-1.amazonaws.com — AWS SES). Bo'sh — email o'chiq.</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    /// <summary>STARTTLS/SSL. Serverda doim true.</summary>
    public bool EnableSsl { get; set; } = true;
    public string? User { get; set; }
    /// <summary>Faqat env/user-secrets.</summary>
    public string? Password { get; set; }
    /// <summary>Jo'natuvchi manzil (masalan no-reply@&lt;domen&gt;). Bo'sh — email o'chiq.</summary>
    public string? From { get; set; }
    public string FromName { get; set; } = "tahsinERP";
}

public interface ISmsSender
{
    bool IsConfigured { get; }
    /// <summary>phone: +998XXXXXXXXX. Xato — HttpRequestException.</summary>
    Task SendAsync(string phone, string text, CancellationToken ct = default);
}

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>Eskiz tokeni (singleton): /auth/login → data.token (≈30 kun); 401 bo'lsa qayta kiriladi.</summary>
public sealed class EskizTokenCache
{
    public string? Token;
    public readonly SemaphoreSlim Lock = new(1, 1);
}

/// <summary>
/// Eskiz.uz SMS: POST {BaseUrl}/auth/login (email, password) → token; POST {BaseUrl}/message/sms/send (mobile_phone=998XXXXXXXXX,
/// message, from) + Authorization: Bearer. Eskiz matn shablonlarini moderatsiya qiladi — kod matni Eskiz kabinetida tasdiqlangan bo'lishi shart.
/// </summary>
public sealed class EskizSmsSender(HttpClient http, IOptions<EskizOptions> options, EskizTokenCache cache) : ISmsSender
{
    private EskizOptions O => options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.Email) && !string.IsNullOrWhiteSpace(O.Password);
    private string Url(string path) => O.BaseUrl.TrimEnd('/') + "/" + path;

    private async Task<string> TokenAsync(bool force, CancellationToken ct)
    {
        if (!force && cache.Token is not null) return cache.Token;
        await cache.Lock.WaitAsync(ct);
        try
        {
            if (!force && cache.Token is not null) return cache.Token;
            using var form = new MultipartFormDataContent { { new StringContent(O.Email!), "email" }, { new StringContent(O.Password!), "password" } };
            using var resp = await http.PostAsync(Url("auth/login"), form, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"Eskiz login {(int)resp.StatusCode}: {Cut(body)}");
            string? token = (JsonNode.Parse(body)?["data"]?["token"])?.GetValue<string>();
            cache.Token = token ?? throw new HttpRequestException("Eskiz login javobida token yo'q.");
            return token;
        }
        finally { cache.Lock.Release(); }
    }

    public async Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        string digits = new(phone.Where(char.IsAsciiDigit).ToArray());
        for (int attempt = 0; ; attempt++)
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(digits), "mobile_phone" }, { new StringContent(text), "message" }, { new StringContent(O.From), "from" },
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, Url("message/sms/send")) { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(attempt > 0, ct));
            using var resp = await http.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) continue;
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Eskiz {(int)resp.StatusCode}: {Cut(await resp.Content.ReadAsStringAsync(ct))}");
            return;
        }
    }

    private static string Cut(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>SMTP orqali oddiy matnli xat (System.Net.Mail). AWS SES SMTP yoki istalgan SMTP server.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private SmtpOptions O => options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.Host) && !string.IsNullOrWhiteSpace(O.From);

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var msg = new MailMessage(new MailAddress(O.From!, O.FromName), new MailAddress(to)) { Subject = subject, Body = body, IsBodyHtml = false };
        using var client = new SmtpClient(O.Host!, O.Port) { EnableSsl = O.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrWhiteSpace(O.User)) client.Credentials = new NetworkCredential(O.User, O.Password);
        try { await client.SendMailAsync(msg, ct); }
        catch (SmtpException ex) { throw new HttpRequestException("SMTP: " + ex.Message, ex); }
    }
}

/// <summary>Kod SMS'da — foydalanuvchi telefoniga (PortalUser.Phone). SMS sozlanmagan bo'lsa — kanal yo'q.</summary>
public sealed class SmsOtpSender(ISmsSender sms, IOptions<PortalOptions> options) : IOtpSender
{
    public string Channel => "sms";

    public OtpTarget? Resolve(PortalUser user, Tenant tenant) => ResolveReset(user);

    public OtpTarget? ResolveReset(PortalUser user) =>
        sms.IsConfigured && !string.IsNullOrWhiteSpace(user.Phone) ? new OtpTarget("sms", L.T("Ptl_SmsTarget", PortalIdentity.Mask(user.Phone)), user.Phone) : null;

    public async Task SendAsync(OtpTarget target, PortalUser user, string code, CancellationToken ct = default)
    {
        // SMS matni Eskiz kabinetida tasdiqlangan shablonga mos bo'lishi shart — doim asosiy tilda (o'zbekcha).
        try { await sms.SendAsync(target.Address!, L.TFor(Languages.Default, "Ptl_MsgOtp", code, options.Value.OtpMinutes), ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { throw new OtpDeliveryException(L.T("Ptl_SmsOtpFailed"), ex); }
    }

    public async Task SendResetAsync(OtpTarget target, PortalUser user, string url, int minutes, CancellationToken ct = default)
    {
        try { await sms.SendAsync(target.Address!, L.TFor(Languages.Default, "Ptl_MsgResetLinkSms", minutes, url), ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { throw new OtpDeliveryException(L.T("Ptl_SmsFailed"), ex); }
    }
}

/// <summary>Kod emailda — PortalUser.Email ga. Email sozlanmagan bo'lsa — kanal yo'q.</summary>
public sealed class EmailOtpSender(IEmailSender email, IOptions<PortalOptions> options) : IOtpSender
{
    public string Channel => "email";

    public OtpTarget? Resolve(PortalUser user, Tenant tenant) => ResolveReset(user);

    public OtpTarget? ResolveReset(PortalUser user) =>
        email.IsConfigured && !string.IsNullOrWhiteSpace(user.Email) ? new OtpTarget("email", L.T("Ptl_EmailTarget", MaskEmail(user.Email)), user.Email) : null;

    public static string MaskEmail(string e)
    {
        int at = e.IndexOf('@');
        return at <= 1 ? "***" + e[Math.Max(0, at)..] : e[0] + new string('*', Math.Max(1, at - 1)) + e[at..];
    }

    public async Task SendAsync(OtpTarget target, PortalUser user, string code, CancellationToken ct = default)
    {
        try
        {
            await email.SendAsync(target.Address!, L.T("Ptl_EmailOtpSubject"),
                L.T("Ptl_EmailOtpBody", user.FullName, code, options.Value.OtpMinutes), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        { throw new OtpDeliveryException(L.T("Ptl_EmailOtpFailed"), ex); }
    }

    public async Task SendResetAsync(OtpTarget target, PortalUser user, string url, int minutes, CancellationToken ct = default)
    {
        try
        {
            await email.SendAsync(target.Address!, L.T("Ptl_EmailResetSubject"),
                L.T("Ptl_EmailResetBody", user.FullName, minutes, url), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        { throw new OtpDeliveryException(L.T("Ptl_EmailFailed"), ex); }
    }
}
