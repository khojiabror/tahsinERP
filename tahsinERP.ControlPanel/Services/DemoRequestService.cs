using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.Portal;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Demo so'rovi xabarnomalari (<c>ControlPanel:Leads</c>).</summary>
public class LeadsOptions
{
    /// <summary>Yangi so'rov nusxasi shu manzil(lar)ga emailda (vergul bilan). SMTP (ControlPanel:Smtp) sozlanmagan bo'lsa yuborilmaydi.</summary>
    public string? NotifyEmail { get; set; }
}

/// <summary>Ommaviy sahifadagi demo formasi maydonlari (tahsinerp.uz).</summary>
public class DemoRequestInput
{
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [Required, StringLength(300)] public string Company { get; set; } = "";
    [Required, StringLength(50)] public string Phone { get; set; } = "";
    [StringLength(200), EmailAddress] public string? Email { get; set; }
    [StringLength(30)] public string? Industry { get; set; }
    [StringLength(20)] public string? CompanySize { get; set; }
    public List<string> Modules { get; set; } = [];
    [StringLength(20)] public string? Plan { get; set; }
    [StringLength(2000)] public string? Message { get; set; }
    /// <summary>Honeypot: odam ko'rmaydigan maydon — to'ldirilgan bo'lsa, bot.</summary>
    public string? Website { get; set; }
}

/// <summary>
/// Demo so'rovini qabul qiladi: tekshiradi, spamni cheklaydi (IP bo'yicha soatiga 5 ta, bir telefondan 10 daqiqada 1 ta),
/// bazaga yozadi va xodimlar Telegram guruhiga xabar yuboradi. Sinovni xodim panelda ochadi (Leads → "Sinov ochish").
/// </summary>
public sealed partial class DemoRequestService(ControlDbContext db, ITelegramSender telegram, IOptions<TelegramOptions> tgOptions,
    IOptions<PanelHostOptions> hosts, IEmailSender email, IOptions<LeadsOptions> leads, Clock clock, ILogger<DemoRequestService> log)
{
    public const int PerIpPerHour = 5;

    public static readonly string[] Industries = ["production", "distribution", "trade", "services", "other"];
    public static readonly string[] Sizes = ["1-20", "21-100", "101-500", "500+"];
    public static readonly string[] Plans = ["START", "DIST", "BIZNES", "KORP"];
    public static readonly string[] ModuleCodes = ["srm", "crm", "mes", "finance", "hr", "logistics", "docs", "tasks", "distribution", "website"];

    public enum Outcome { Saved, Duplicate, Throttled, Spam }

    public async Task<(Outcome Outcome, DemoRequest? Request)> SubmitAsync(DemoRequestInput input, string lang, string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(input.Website))
            return (Outcome.Spam, null);

        DateTime now = clock.UtcNow;
        string phone = NormalizePhone(input.Phone);
        if (ip is not null && await db.DemoRequests.CountAsync(r => r.Ip == ip && r.CreatedAtUtc > now.AddHours(-1), ct) >= PerIpPerHour)
            return (Outcome.Throttled, null);
        // Ikki marta bosilgan tugma yoki qayta yuborish — yangi yozuv emas, mavjudi qaytadi (mijoz baribir "qabul qilindi" ko'radi).
        var dup = await db.DemoRequests.FirstOrDefaultAsync(r => r.Phone == phone && r.CreatedAtUtc > now.AddMinutes(-10), ct);
        if (dup is not null)
            return (Outcome.Duplicate, dup);

        var r = new DemoRequest
        {
            FullName = input.FullName.Trim(),
            Company = input.Company.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            Industry = Industries.Contains(input.Industry) ? input.Industry : null,
            CompanySize = Sizes.Contains(input.CompanySize) ? input.CompanySize : null,
            Modules = Truncate(string.Join(",", input.Modules.Where(ModuleCodes.Contains).Distinct()), 200) is { Length: > 0 } m ? m : null,
            Plan = Plans.Contains(input.Plan) ? input.Plan : null,
            Message = string.IsNullOrWhiteSpace(input.Message) ? null : Truncate(input.Message.Trim(), 2000),
            Lang = lang,
            Ip = ip is null ? null : Truncate(ip, 64),
            UserAgent = userAgent is null ? null : Truncate(userAgent, 400),
            Status = DemoRequestStatus.New,
            CreatedAtUtc = now,
        };
        db.DemoRequests.Add(r);
        await db.SaveChangesAsync(ct);
        await NotifyStaffAsync(r, ct);
        return (Outcome.Saved, r);
    }

    private async Task NotifyStaffAsync(DemoRequest r, CancellationToken ct)
    {
        string text = BuildStaffText(r, "https://" + hosts.Value.EffectiveAdminHost);
        await EmailStaffAsync(r, text, ct);
        if (!TelegramReady(out _))
        {
            log.LogInformation("Demo so'rovi #{Id} (Telegram sozlanmagan): {Text}", r.Id, text);
            return;
        }
        await TrySendTelegramAsync(r, text, ct);
    }

    /// <summary>Qayta urinishlar shu muddat ichida (so'rov yaratilganidan) — undan keyin xabar eskirgan, faqat panelda.</summary>
    public static readonly TimeSpan RetryWindow = TimeSpan.FromHours(24);

    /// <summary>N-urinishdan keyingi kutish: 1, 2, 4, ... daqiqa, ko'pi bilan 30 daqiqa.</summary>
    public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromMinutes(Math.Min(30, 1 << Math.Clamp(attempts - 1, 0, 5)));

    /// <summary>
    /// Telegram'ga yetib bormagan so'rovlarni qayta yuboradi (fon ishi har daqiqada chaqiradi). Yuborilganlar soni.
    /// Faqat kamida bir marta urinilganlar — Telegram umuman sozlanmagan bo'lsa so'rovlar faqat panelda.
    /// </summary>
    public async Task<int> RetryPendingAsync(CancellationToken ct = default)
    {
        if (!TelegramReady(out _))
            return 0;
        DateTime now = clock.UtcNow;
        var pending = await db.DemoRequests
            .Where(r => r.StaffNotifiedAtUtc == null && r.StaffNotifyAttempts > 0 && r.CreatedAtUtc > now - RetryWindow)
            .OrderBy(r => r.Id).Take(20).ToListAsync(ct);
        string adminBase = "https://" + hosts.Value.EffectiveAdminHost;
        int sent = 0;
        foreach (var r in pending)
        {
            if (r.StaffNotifyLastAtUtc is DateTime last && now < last + RetryDelay(r.StaffNotifyAttempts))
                continue;
            string text = BuildStaffText(r, adminBase) + "\n" +
                          $"⏱ Kechikib yuborildi — so'rov vaqti {clock.ToLocal(r.CreatedAtUtc):dd.MM.yyyy HH:mm}";
            if (await TrySendTelegramAsync(r, text, ct))
                sent++;
            else
                break; // Telegram hali ishlamayapti — qolganlarini keyingi aylanishda
        }
        return sent;
    }

    private bool TelegramReady(out string chat)
    {
        chat = tgOptions.Value.StaffChatId ?? "";
        return !string.IsNullOrWhiteSpace(chat) && telegram.IsConfigured;
    }

    /// <summary>Bitta urinish; natija so'rovga yoziladi (yuborilgan vaqt yoki urinishlar soni + xato).</summary>
    private async Task<bool> TrySendTelegramAsync(DemoRequest r, string text, CancellationToken ct)
    {
        TelegramReady(out string chat);
        bool ok;
        try
        {
            await telegram.SendAsync(chat, text, ct);
            r.StaffNotifiedAtUtc = clock.UtcNow;
            r.StaffNotifyError = null;
            ok = true;
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // So'rov bazada saqlangan — panelda ko'rinadi; fon ishi qayta yuboradi, mijozga ta'sir qilmaydi.
            r.StaffNotifyAttempts++;
            r.StaffNotifyLastAtUtc = clock.UtcNow;
            r.StaffNotifyError = Truncate(ex.Message, 500);
            log.LogWarning(ex, "Demo so'rovi #{Id} Telegram xabari yuborilmadi ({Attempts}-urinish)", r.Id, r.StaffNotifyAttempts);
            ok = false;
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return ok;
    }

    private async Task EmailStaffAsync(DemoRequest r, string text, CancellationToken ct)
    {
        var to = (leads.Value.NotifyEmail ?? "").Split(',', ';').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (to.Count == 0)
            return;
        if (!email.IsConfigured)
        {
            log.LogInformation("Demo so'rovi #{Id}: email yuborilmadi — SMTP (ControlPanel:Smtp) sozlanmagan", r.Id);
            return;
        }
        foreach (var addr in to)
        {
            try
            {
                await email.SendAsync(addr, $"tahsinERP: demo so'rovi #{r.Id} — {r.Company}", text, ct);
            }
            catch (Exception ex)
            {
                // Bazada saqlangan, panelda ko'rinadi — email xatosi mijozga ta'sir qilmaydi.
                log.LogWarning(ex, "Demo so'rovi #{Id} emaili {To} ga yuborilmadi", r.Id, addr);
            }
        }
    }

    public static string BuildStaffText(DemoRequest r, string adminBaseUrl)
    {
        var lines = new List<string>
        {
            $"🆕 Demo so'rovi #{r.Id} (tahsinerp.uz, {r.Lang.ToUpperInvariant()})",
            $"Kompaniya: {r.Company}",
            $"Ism: {r.FullName}",
            $"Telefon: {r.Phone}",
        };
        if (r.Email is not null) lines.Add($"Email: {r.Email}");
        if (r.Industry is not null) lines.Add($"Faoliyat: {IndustryText(r.Industry)}");
        if (r.CompanySize is not null) lines.Add($"Xodimlar: {r.CompanySize}");
        if (r.Modules is not null) lines.Add($"Modullar: {r.Modules}");
        if (r.Plan is not null) lines.Add($"Paket: {r.Plan}");
        if (r.Message is not null) lines.Add($"Izoh: {r.Message}");
        lines.Add($"{adminBaseUrl}/Leads");
        return string.Join("\n", lines);
    }

    public static string IndustryText(string? code) => code switch
    {
        "production" => "Ishlab chiqarish",
        "distribution" => "Distributsiya / ulgurji",
        "trade" => "Savdo",
        "services" => "Xizmatlar",
        "other" => "Boshqa",
        _ => code ?? "",
    };

    /// <summary>Raqamlardan boshqasini olib tashlaydi; 9 xonali mahalliy raqamga +998 qo'shiladi.</summary>
    public static string NormalizePhone(string phone)
    {
        string digits = NonDigits().Replace(phone ?? "", "");
        if (digits.Length == 9) digits = "998" + digits;
        return digits.Length >= 7 ? "+" + digits : (phone ?? "").Trim();
    }

    public static bool IsValidPhone(string phone) => NonDigits().Replace(phone ?? "", "").Length is >= 9 and <= 15;

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}

/// <summary>Har daqiqada Telegram'ga yetib bormagan demo so'rovlarini qayta yuboradi (DemoRequestService.RetryPendingAsync).</summary>
public sealed class DemoNotifyRetryHostedService(IServiceScopeFactory scopes, ILogger<DemoNotifyRetryHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
            try
            {
                using var scope = scopes.CreateScope();
                int n = await scope.ServiceProvider.GetRequiredService<DemoRequestService>().RetryPendingAsync(stoppingToken);
                if (n > 0) log.LogInformation("Demo so'rovlari: {Count} ta kechikkan Telegram xabari yuborildi", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Demo so'rovlarini qayta yuborish xatosi: {Message}", ex.Message);
            }
        }
    }
}
