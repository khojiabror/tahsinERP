using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Bot;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    /// <summary>Xaritadagi nuqta (marshrut tartibi uchun).</summary>
    public sealed class GeoPoint
    {
        public int Id { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public bool HasGeo => Lat.HasValue && Lng.HasValue;
    }

    /// <summary>Yetkazish hisoblari (sof funksiyalar): sig'im, marshrut tartibi, to'xtash natijasi, farq.</summary>
    public static class DeliveryMath
    {
        /// <summary>Mashina sig'imi tekshiruvi: og'irlik (majburiy) va hajm (mashinada berilgan bo'lsa). Bo'sh — sig'adi.</summary>
        public static List<string> CapacityProblems(decimal weightKg, decimal volumeM3, decimal capacityKg, decimal? capacityM3)
        {
            var problems = new List<string>();
            if (weightKg > capacityKg)
                problems.Add(DstLocalization.T("Trip_OverWeight", weightKg.ToString("0.#"), capacityKg.ToString("0.#")));
            if (capacityM3.HasValue && volumeM3 > capacityM3.Value)
                problems.Add(DstLocalization.T("Trip_OverVolume", volumeM3.ToString("0.###"), capacityM3.Value.ToString("0.###")));
            return problems;
        }

        /// <summary>Sig'im foizi (progress uchun), 0 bo'lsa — null.</summary>
        public static decimal? LoadPct(decimal value, decimal? capacity) =>
            capacity.HasValue && capacity.Value > 0 ? Math.Round(value * 100m / capacity.Value, 1) : (decimal?)null;

        /// <summary>
        /// Marshrut tartibi — eng yaqin qo'shni: boshlang'ich nuqtadan (ombor; bo'lmasa birinchi koordinatali nuqta) har safar eng yaqin
        /// borilmagan nuqtaga. Koordinatasiz nuqtalar oxirida, avvalgi tartibda. Ko'p nuqtada aniq optimal emas, lekin barqaror va tez.
        /// </summary>
        public static List<int> NearestNeighbor(IReadOnlyList<GeoPoint> points, decimal? startLat, decimal? startLng)
        {
            var withGeo = points.Where(p => p.HasGeo).ToList();
            var result = new List<int>();
            double? lat = (double?)startLat, lng = (double?)startLng;
            if ((lat == null || lng == null) && withGeo.Count > 0)
            {
                var first = withGeo[0];
                result.Add(first.Id);
                withGeo.RemoveAt(0);
                lat = (double)first.Lat.Value;
                lng = (double)first.Lng.Value;
            }
            while (withGeo.Count > 0)
            {
                int best = 0;
                double bestDistance = double.MaxValue;
                for (int i = 0; i < withGeo.Count; i++)
                {
                    double d = Geo.DistanceM(lat.Value, lng.Value, (double)withGeo[i].Lat.Value, (double)withGeo[i].Lng.Value);
                    if (d < bestDistance - 0.001) { best = i; bestDistance = d; }
                }
                var next = withGeo[best];
                withGeo.RemoveAt(best);
                result.Add(next.Id);
                lat = (double)next.Lat.Value;
                lng = (double)next.Lng.Value;
            }
            result.AddRange(points.Where(p => !p.HasGeo).Select(p => p.Id));
            return result;
        }

        /// <summary>Marshrut uzunligi (to'g'ri chiziq bo'yicha, km) — tartibni solishtirish uchun.</summary>
        public static decimal PathKm(IEnumerable<GeoPoint> ordered, decimal? startLat, decimal? startLng)
        {
            double total = 0;
            double? lat = (double?)startLat, lng = (double?)startLng;
            foreach (var p in ordered.Where(p => p.HasGeo))
            {
                if (lat.HasValue && lng.HasValue)
                    total += Geo.DistanceM(lat.Value, lng.Value, (double)p.Lat.Value, (double)p.Lng.Value);
                lat = (double)p.Lat.Value;
                lng = (double)p.Lng.Value;
            }
            return Math.Round((decimal)total / 1000m, 1);
        }

        /// <summary>To'xtash natijasi: hammasi yetkazildi — Delivered, hech narsa — Refused, aks holda Partial.</summary>
        public static StopStatus Classify(IEnumerable<(decimal Shipped, decimal Delivered)> lines)
        {
            var list = lines.ToList();
            if (list.Count == 0 || list.All(l => l.Delivered <= 0)) return StopStatus.Refused;
            return list.All(l => l.Delivered >= l.Shipped) ? StopStatus.Delivered : StopStatus.Partial;
        }

        /// <summary>Qator summasining ulushi (yetkazilgan yoki qaytgan miqdorga).</summary>
        public static decimal Share(decimal amount, decimal qty, decimal ofQty) =>
            ofQty <= 0 ? 0 : Math.Round(amount * qty / ofQty, 2, MidpointRounding.AwayFromZero);

        /// <summary>Yopishdagi farq: yetkazilmagan = jo'natilgan − yetkazilgan; qaytgan ≤ yetkazilmagan; kamomad = yetkazilmagan − qaytgan.</summary>
        public static (decimal Undelivered, decimal Returned, decimal Shortage) CloseLine(decimal shipped, decimal delivered, decimal? returned)
        {
            decimal undelivered = Math.Max(0, shipped - delivered);
            decimal back = returned ?? undelivered;
            if (back < 0) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));
            if (back > undelivered) throw new DstValidationException(DstLocalization.T("Trip_ReturnedOverUndelivered", undelivered.ToString("0.####")));
            return (undelivered, back, undelivered - back);
        }

        /// <summary>Yandex xaritalar navigatsiya havolasi (ilova yoki sayt ochadi): joriy joydan nuqtagacha avtomobil yo'li.</summary>
        public static string NavigationUrl(decimal lat, decimal lng) =>
            "https://yandex.uz/maps/?rtext=~" + lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
            lng.ToString(System.Globalization.CultureInfo.InvariantCulture) + "&rtt=auto";

        /// <summary>Bir nechta nuqtali marshrut havolasi (Yandex, kalitsiz): ombor → nuqtalar tartibda.</summary>
        public static string RouteUrl(decimal? startLat, decimal? startLng, IEnumerable<GeoPoint> ordered)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (startLat.HasValue && startLng.HasValue) parts.Add(startLat.Value.ToString(inv) + "," + startLng.Value.ToString(inv));
            parts.AddRange(ordered.Where(p => p.HasGeo).Take(50).Select(p => p.Lat.Value.ToString(inv) + "," + p.Lng.Value.ToString(inv)));
            return parts.Count == 0 ? null : "https://yandex.uz/maps/?rtext=" + string.Join("~", parts) + "&rtt=auto";
        }

        /// <summary>"lat,lng" matnidan (sozlama DepotGeo_*).</summary>
        public static bool TryParseGeo(string text, out decimal lat, out decimal lng)
        {
            lat = lng = 0;
            var parts = (text ?? "").Split(',');
            return parts.Length == 2
                   && decimal.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out lat)
                   && decimal.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out lng)
                   && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
        }
    }
}
