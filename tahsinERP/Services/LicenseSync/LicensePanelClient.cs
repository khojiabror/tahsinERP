using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tahsinERP.Services.LicenseSync
{
    /// <summary>
    /// Markaziy panelning License API klienti (kontrakt: Docs/ControlPanel/01_LICENSE_API.md).
    /// DB'ga tegmaydi — faqat HTTP; testda HttpMessageHandler o'rniga soxta panel beriladi.
    /// </summary>
    public class LicensePanelClient
    {
        public const string HeaderTimestamp = "X-Tahsin-Timestamp";
        public const string HeaderNonce = "X-Tahsin-Nonce";
        public const string HeaderSignature = "X-Tahsin-Signature";

        private readonly HttpClient _http;
        private readonly Uri _baseUri;
        private readonly Func<DateTime> _utcNow;

        public LicensePanelClient(string baseUrl, HttpMessageHandler handler = null, Func<DateTime> utcNow = null)
        {
            _baseUri = ParseBaseUrl(baseUrl);
            _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
            _http.Timeout = TimeSpan.FromSeconds(30);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Panel URL'ini tekshiradi: https, yoki faqat localhost uchun http (test).</summary>
        public static Uri ParseBaseUrl(string baseUrl)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out uri))
                throw new ArgumentException("Panel URL yaroqsiz: '" + baseUrl + "'", nameof(baseUrl));
            bool ok = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
            if (!ok)
                throw new ArgumentException("Panel URL faqat https bo'lishi kerak: '" + baseUrl + "'", nameof(baseUrl));
            return uri;
        }

        #region Natija turlari

        public enum PullStatus { Updated, NotModified, Unauthorized, NotFound, Revoked, Failed }

        public class RegisterResult
        {
            public bool Ok { get; set; }
            public string TenantSecret { get; set; }
            public string LicenseKey { get; set; }
            public int? PollHours { get; set; }
            /// <summary>On-prem onlayn tekshiruv siyosati: "required" / "optional" (eski panel — null).</summary>
            public string OnlineCheck { get; set; }
            /// <summary>Internetsiz ishlash muddati, kun (eski panel — null).</summary>
            public int? OfflineGraceDays { get; set; }
            /// <summary>Panel xato kodi (token_not_found, token_used, ...) yoki tarmoq xatosi matni.</summary>
            public string Error { get; set; }
            public int HttpStatus { get; set; }
        }

        public class PullResult
        {
            public PullStatus Status { get; set; }
            public string LicenseKey { get; set; }
            public string ETag { get; set; }
            public int? PollHours { get; set; }
            /// <summary>On-prem onlayn tekshiruv siyosati: "required" / "optional" (304 yoki eski panel — null).</summary>
            public string OnlineCheck { get; set; }
            public int? OfflineGraceDays { get; set; }
            public string Error { get; set; }
            public int HttpStatus { get; set; }
        }

        #endregion

        public async Task<RegisterResult> RegisterAsync(string regToken, string installationId, int companyId,
            string version, string host, CancellationToken ct = default(CancellationToken))
        {
            var body = new JObject
            {
                ["reg_token"] = regToken,
                ["installation_id"] = installationId,
                ["company_id"] = companyId,
                ["version"] = version,
                ["host"] = host
            };
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "api/license/register"))
            {
                Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };

            try
            {
                using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
                {
                    var json = await ReadJsonAsync(response).ConfigureAwait(false);
                    var result = new RegisterResult { HttpStatus = (int)response.StatusCode };
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        result.TenantSecret = (string)json?["tenant_secret"];
                        result.LicenseKey = (string)json?["license_key"];
                        result.PollHours = (int?)json?["poll_hours"];
                        result.OnlineCheck = (string)json?["online_check"];
                        result.OfflineGraceDays = (int?)json?["offline_grace_days"];
                        result.Ok = IsValidSecret(result.TenantSecret);
                        if (!result.Ok)
                            result.Error = "Panel javobida tenant_secret yo'q yoki yaroqsiz.";
                    }
                    else
                    {
                        result.Error = (string)json?["error"] ?? ("HTTP " + (int)response.StatusCode);
                    }
                    return result;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return new RegisterResult { Error = "Panelga ulanib bo'lmadi: " + ex.Message };
            }
        }

        public async Task<PullResult> PullAsync(string installationId, string tenantSecret, string currentLicenseKey,
            CancellationToken ct = default(CancellationToken))
        {
            string path = "/api/license/" + installationId;
            string etag = string.IsNullOrWhiteSpace(currentLicenseKey) ? null : ComputeETag(currentLicenseKey);
            string ts = ((long)(_utcNow() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
            string nonce = NewNonce();

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, path.TrimStart('/')));
            request.Headers.Add(HeaderTimestamp, ts);
            request.Headers.Add(HeaderNonce, nonce);
            request.Headers.Add(HeaderSignature, ComputeSignature(tenantSecret, "GET", path, ts, nonce, etag));
            if (etag != null)
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"" + etag + "\""));

            try
            {
                using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
                {
                    var result = new PullResult { HttpStatus = (int)response.StatusCode };
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        result.Status = PullStatus.NotModified;
                        return result;
                    }

                    var json = await ReadJsonAsync(response).ConfigureAwait(false);
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            result.LicenseKey = (string)json?["license_key"];
                            result.ETag = (string)json?["etag"];
                            result.PollHours = (int?)json?["poll_hours"];
                            result.OnlineCheck = (string)json?["online_check"];
                            result.OfflineGraceDays = (int?)json?["offline_grace_days"];
                            if (string.IsNullOrWhiteSpace(result.LicenseKey))
                            {
                                result.Status = PullStatus.Failed;
                                result.Error = "Panel javobida license_key yo'q.";
                            }
                            else
                            {
                                result.Status = etag != null && ComputeETag(result.LicenseKey) == etag
                                    ? PullStatus.NotModified : PullStatus.Updated;
                            }
                            break;
                        case HttpStatusCode.Unauthorized:
                            result.Status = PullStatus.Unauthorized;
                            break;
                        case HttpStatusCode.NotFound:
                            result.Status = PullStatus.NotFound;
                            break;
                        case HttpStatusCode.Gone:
                            result.Status = PullStatus.Revoked;
                            break;
                        default:
                            result.Status = PullStatus.Failed;
                            break;
                    }
                    if (result.Error == null && result.Status != PullStatus.Updated && result.Status != PullStatus.NotModified)
                        result.Error = (string)json?["error"] ?? ("HTTP " + (int)response.StatusCode);
                    return result;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return new PullResult { Status = PullStatus.Failed, Error = "Panelga ulanib bo'lmadi: " + ex.Message };
            }
        }

        public enum DevLoginStatus { Ok, Invalid, Locked, Unavailable }

        public class DevLoginResult
        {
            public DevLoginStatus Status { get; set; }
            public string Login { get; set; }
            public string FullName { get; set; }
            public string Error { get; set; }
        }

        /// <summary>
        /// Panel ERP dasturchisi (super-foydalanuvchi) login/parolini tekshiradi:
        /// POST /api/license/{id}/dev-login, imzoning ETAG o'rnida — tananing SHA-256 hex'i.
        /// </summary>
        public async Task<DevLoginResult> DevLoginAsync(string installationId, string tenantSecret, string login, string password,
            CancellationToken ct = default(CancellationToken))
        {
            string path = "/api/license/" + installationId + "/dev-login";
            byte[] body = Encoding.UTF8.GetBytes(new JObject { ["login"] = login, ["password"] = password }.ToString(Formatting.None));
            string bodyHash;
            using (var sha = SHA256.Create())
                bodyHash = ToHex(sha.ComputeHash(body));
            string ts = ((long)(_utcNow() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
            string nonce = NewNonce();

            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, path.TrimStart('/'))) { Content = content };
            request.Headers.Add(HeaderTimestamp, ts);
            request.Headers.Add(HeaderNonce, nonce);
            request.Headers.Add(HeaderSignature, ComputeSignature(tenantSecret, "POST", path, ts, nonce, bodyHash));

            try
            {
                using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
                {
                    var json = await ReadJsonAsync(response).ConfigureAwait(false);
                    switch ((int)response.StatusCode)
                    {
                        case 200:
                            return new DevLoginResult
                            {
                                Status = DevLoginStatus.Ok,
                                Login = (string)json?["login"],
                                FullName = (string)json?["full_name"]
                            };
                        case 401:
                            string error = (string)json?["error"];
                            // Imzo/soat xatosi — foydalanuvchi aybi emas
                            return error == "invalid_credentials"
                                ? new DevLoginResult { Status = DevLoginStatus.Invalid }
                                : new DevLoginResult { Status = DevLoginStatus.Unavailable, Error = error };
                        case 423:
                            return new DevLoginResult { Status = DevLoginStatus.Locked };
                        default:
                            return new DevLoginResult { Status = DevLoginStatus.Unavailable, Error = (string)json?["error"] ?? ("HTTP " + (int)response.StatusCode) };
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return new DevLoginResult { Status = DevLoginStatus.Unavailable, Error = "Panelga ulanib bo'lmadi: " + ex.Message };
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        #region Imzo (kontrakt 2-bo'lim)

        /// <summary>
        /// Base64(HMAC-SHA256(Base64Decode(secret), "METHOD\nPATH\nTS\nNONCE\nETAG")).
        /// </summary>
        public static string ComputeSignature(string tenantSecret, string method, string path, string timestamp, string nonce, string etag)
        {
            string data = method + "\n" + path + "\n" + timestamp + "\n" + nonce + "\n" + (etag ?? string.Empty);
            using (var hmac = new HMACSHA256(Convert.FromBase64String(tenantSecret)))
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }

        /// <summary>Kalitning ETag'i: SHA-256 hex (kichik harf).</summary>
        public static string ComputeETag(string licenseKey)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(licenseKey.Trim()));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>tenant_secret: Base64, kamida 32 bayt.</summary>
        public static bool IsValidSecret(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
                return false;
            try { return Convert.FromBase64String(secret).Length >= 32; }
            catch (FormatException) { return false; }
        }

        private static string NewNonce()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var sb = new StringBuilder(32);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static async Task<JObject> ReadJsonAsync(HttpResponseMessage response)
        {
            if (response.Content == null)
                return null;
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
                return null;
            try { return JObject.Parse(text); }
            catch (JsonException) { return null; }
        }

        #endregion
    }

    /// <summary>
    /// On-prem onlayn tekshiruv siyosati (Docs/ControlPanel/08_STAGE4.md 4). Panel pull/register javobida
    /// <c>online_check</c> ("required"/"optional") va <c>offline_grace_days</c> beradi; ERP ularni LICENSE_STATE'da saqlaydi.
    /// Required'da kalitning o'zi qisqa (panel uni pull orqali yangilab turadi) — bu yerda faqat oldindan ogohlantirish.
    /// DB'siz, sof funksiya (panel testlari ham shu faylni ishlatadi).
    /// </summary>
    public static class OnlineCheckPolicy
    {
        public const string Required = "required";
        public const string Optional = "optional";
        public const int DefaultGraceDays = 30;
        /// <summary>Required rejimida aloqa shuncha kundan ko'p yo'q bo'lsa ogohlantiriladi.</summary>
        public const int RequiredWarnAfterDays = 3;

        public static bool IsRequired(string onlineCheck)
        {
            return string.Equals((onlineCheck ?? "").Trim(), Required, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ogohlantirish matni yoki null. <paramref name="lastOkUtc"/> — oxirgi muvaffaqiyatli sinxronlash (null — hali yo'q:
        /// ogohlantirilmaydi). Optional: grace (standart 30) kundan ko'p aloqa bo'lmasa; Required: 3 kundan ko'p bo'lsa.
        /// </summary>
        public static string Warning(string onlineCheck, int? graceDays, DateTime? lastOkUtc, DateTime nowUtc)
        {
            if (!lastOkUtc.HasValue)
                return null;
            int grace = graceDays.HasValue && graceDays.Value > 0 ? Math.Min(graceDays.Value, 366) : DefaultGraceDays;
            int days = (int)Math.Floor((nowUtc - lastOkUtc.Value).TotalDays);
            if (IsRequired(onlineCheck))
            {
                if (days < RequiredWarnAfterDays)
                    return null;
                return "Litsenziya paneli bilan " + days + " kundan beri aloqa yo'q. Onlayn tekshiruv majburiy: kalit internetsiz ko'pi bilan "
                       + grace + " kun amal qiladi va panel uni avtomatik yangilaydi — server internetini tekshiring.";
            }
            if (days < grace)
                return null;
            return "Litsenziya paneli bilan " + days + " kundan beri aloqa yo'q. Kalit o'z muddatigacha ishlaydi, lekin yangilanishlar "
                   + "(to'lov, tarif o'zgarishi) yetib kelmaydi — internetni tekshiring yoki kalitni qo'lda kiriting.";
        }
    }
}
