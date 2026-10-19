using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Portal;

/// <summary>Kod qayerga ketadi: kanal nomi va foydalanuvchiga ko'rsatiladigan niqoblangan manzil.</summary>
public sealed record OtpTarget(string Channel, string Display, string? Address);

/// <summary>
/// Bir martalik kod yuborish kanali. Hozir faqat Telegram (+ lokal dev uchun log). 4-bosqichda SMS (Eskiz) yoki email
/// qo'shish uchun shu interfeysni amalga oshirib DI'da ro'yxatdan o'tkazish kifoya — PortalAuthService ro'yxatdagi
/// birinchi mos kanalni tanlaydi (tartib = ro'yxatdan o'tish tartibi).
/// </summary>
public interface IOtpSender
{
    /// <summary>Shu foydalanuvchiga bu kanal orqali yuborish mumkinmi; mumkin bo'lmasa null.</summary>
    OtpTarget? Resolve(PortalUser user, Tenant tenant);
    Task SendAsync(OtpTarget target, PortalUser user, string code, CancellationToken ct = default);
}

public sealed class OtpDeliveryException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Telegram: foydalanuvchining bog'langan chati, bo'lmasa tenant chati (Tenant.TelegramChatId).
/// Bot token yo'q bo'lsa va <see cref="PortalOptions.OtpLogFallback"/> yoqilgan bo'lsa (faqat lokal) — kod log'ga yoziladi.
/// </summary>
public sealed class TelegramOtpSender(ITelegramSender telegram, IOptions<PortalOptions> options, ILogger<TelegramOtpSender> log) : IOtpSender
{
    public OtpTarget? Resolve(PortalUser user, Tenant tenant)
    {
        string? chat = !string.IsNullOrWhiteSpace(user.TelegramChatId) ? user.TelegramChatId
            : !string.IsNullOrWhiteSpace(tenant.TelegramChatId) ? tenant.TelegramChatId : null;
        if (telegram.IsConfigured && chat is not null)
            return new OtpTarget("telegram", "Telegram " + (chat == user.TelegramChatId ? "(shaxsiy chat " : "(tashkilot chati ") + PortalIdentity.Mask(chat) + ")", chat);
        if (options.Value.OtpLogFallback)
            return new OtpTarget("log", "server log'i (lokal rejim)", null);
        return null;
    }

    public async Task SendAsync(OtpTarget target, PortalUser user, string code, CancellationToken ct = default)
    {
        if (target.Channel == "log")
        {
            log.LogWarning("PORTAL OTP (OtpLogFallback, faqat lokal): foydalanuvchi #{User} kodi {Code}", user.Id, code);
            return;
        }
        try
        {
            await telegram.SendAsync(target.Address!, $"tahsinERP portal: kirish kodi {code}. {options.Value.OtpMinutes} daqiqa amal qiladi. Kodni hech kimga bermang.", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OtpDeliveryException("Kodni Telegram orqali yuborib bo'lmadi. Birozdan keyin qayta urinib ko'ring.", ex);
        }
    }
}
