using System;
using System.Linq;
using tahsinERP.Areas.Distribution.Controllers.Api;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Integration;

namespace tahsinERP.Areas.Distribution.Services.Bot
{
    /// <summary>
    /// Bot fayl omboridagi rasmlar (selfi, tashrif fotosi, imzo, yangi nuqta fotosi) uchun vaqtinchalik havola:
    /// {BotPublicUrl}/files/{kalit}?e=..&amp;s=.. — brauzer to'g'ridan-to'g'ri botdan oladi, ERP botga murojaat qilmaydi.
    /// Muddat soat boshiga yaxlitlanadi (sahifa qayta ochilganda URL o'zgarmaydi — brauzer keshi ishlaydi).
    /// </summary>
    public sealed class FileLinks
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

        private readonly string _baseUrl;
        private readonly byte[] _key;
        private readonly long _expires;

        private FileLinks(string baseUrl, byte[] key, long expires)
        {
            _baseUrl = baseUrl;
            _key = key;
            _expires = expires;
        }

        public bool Enabled => _key != null;

        /// <summary>Sozlama BotPublicUrl va bot kaliti bo'lmasa — havolasiz (rasm o'rniga belgisi ko'rsatiladi).</summary>
        public static FileLinks For(DistributionDbContext db, DateTimeOffset nowUtc)
        {
            var baseUrl = DstSettings.Get(db, DstSetting.BotPublicUrl)?.Trim().TrimEnd('/');
            var master = DstApiAuthAttribute.MasterKey();
            if (string.IsNullOrEmpty(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || string.IsNullOrWhiteSpace(master) || master.Length < 32)
                return new FileLinks(null, null, 0);
            var key = DstApiSignature.TenantKey(master, TenantContext.CurrentOrNull?.Slug ?? DstApiSignature.DefaultTenant);
            long hour = nowUtc.ToUnixTimeSeconds() / 3600 * 3600;
            return new FileLinks(baseUrl, key, hour + (long)Lifetime.TotalSeconds);
        }

        public string Url(string fileKey)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(fileKey)) return null;
            var path = string.Join("/", fileKey.Split('/').Select(Uri.EscapeDataString));
            return _baseUrl + "/files/" + path + "?e=" + _expires + "&s=" + DstApiSignature.SignFile(_key, fileKey, _expires);
        }
    }
}
