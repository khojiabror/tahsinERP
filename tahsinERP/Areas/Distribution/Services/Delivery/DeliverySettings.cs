using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    /// <summary>2-bosqich sozlamalari (dst.SETTINGS kalitlari; jadval o'zgarmaydi).</summary>
    public static class DeliverySettings
    {
        /// <summary>Yandex Maps JavaScript API kaliti (developer.tech.yandex.ru). Bo'sh — xarita o'rniga ro'yxat va Yandex havolalari.</summary>
        public const string YandexMapsKey = "YandexMapsKey";

        /// <summary>"1" — botda ovozli buyurtma yoqilgan (nutqni matnga — bot sozlamasidagi Groq kaliti bilan; kalit bo'lmasa bot o'chiq deydi).</summary>
        public const string VoiceOrders = "VoiceOrders";

        public static string MapsKey(DistributionDbContext db)
        {
            var key = DstSettings.Get(db, YandexMapsKey)?.Trim();
            return string.IsNullOrEmpty(key) || key.Any(c => !(char.IsLetterOrDigit(c) || c == '-')) ? null : key;
        }
    }
}
