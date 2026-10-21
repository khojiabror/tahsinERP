using System.Collections.Generic;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    /// <summary>Xaritadagi belgi: raqam (tartib), nom, rang (holat), qo'shimcha matn.</summary>
    public sealed class MapPoint
    {
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public string Label { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        /// <summary>Yandex preset rangi: blue, green, red, orange, gray, violet, darkGreen.</summary>
        public string Color { get; set; } = "blue";
    }

    /// <summary>
    /// Yandex Maps JS API 2.1 xaritasi (_YandexMap partial). Kalit sozlamada (YandexMapsKey) bo'lmasa — xarita o'rniga
    /// Yandex sayti havolasi (kalitsiz ishlaydi). Route — belgilar tartibida avtomobil yo'li (multiRouter, ko'pi bilan 50 nuqta),
    /// Track — chiziq (agent joylashuv izi).
    /// </summary>
    public sealed class MapModel
    {
        public string Id { get; set; } = "dstMap";
        public string ApiKey { get; set; }
        public string Language { get; set; } = "ru_RU";
        public int Height { get; set; } = 420;
        public bool Route { get; set; }
        public MapPoint Start { get; set; }
        public List<MapPoint> Points { get; set; } = new List<MapPoint>();
        public List<decimal[]> Track { get; set; } = new List<decimal[]>();
        /// <summary>Kalit bo'lmaganda ko'rsatiladigan tashqi havola (DeliveryMath.RouteUrl).</summary>
        public string FallbackUrl { get; set; }
    }
}
