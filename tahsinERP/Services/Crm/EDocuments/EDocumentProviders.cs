using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using tahsinERP.I18n;

namespace tahsinERP.Services.Crm.EDocuments
{
    /// <summary>
    /// Elektron hujjat almashinuvi provayderi (EDO operatori). Hozir standart — <see cref="InternalEDocumentProvider"/>:
    /// SF/TTNK faqat tizimda yaratiladi. Didox / Faktura.uz ulanishi web.config appSettings orqali yoqiladi
    /// (Docs/EDocuments_Integration.md).
    /// </summary>
    public interface IEDocumentProvider
    {
        /// <summary>INTERNAL, DIDOX, FAKTURA_UZ — ExternalSystemCode ustuniga yoziladi.</summary>
        string Code { get; }
        string Title { get; }
        /// <summary>Hujjat tashqi tizimga yuboriladimi.</summary>
        bool IsExternal { get; }
        /// <summary>Ulanish uchun kerakli sozlamalar berilganmi.</summary>
        bool IsConfigured { get; }

        Task<EDocumentResult> SendInvoiceAsync(EInvoiceDocument document);
        Task<EDocumentResult> SendWaybillAsync(EWaybillDocument document);
        Task<EDocumentResult> GetStatusAsync(EDocumentKind kind, string externalId);
    }

    /// <summary>EDO sozlamalari — web.config appSettings, "EDoc:" prefiksi bilan.</summary>
    public static class EDocumentSettings
    {
        public const string InternalCode = "INTERNAL";
        public const string DidoxCode = "DIDOX";
        public const string FakturaUzCode = "FAKTURA_UZ";

        public static string Get(string key, string fallback = null)
        {
            var value = ConfigurationManager.AppSettings["EDoc:" + key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        /// <summary>Faol provayder kodi; berilmagan bo'lsa INTERNAL.</summary>
        public static string ActiveProviderCode => (Get("Provider", InternalCode) ?? InternalCode).ToUpperInvariant();

        /// <summary>Sotuvchi INN (bo'lmasa COMPANIES.DUNS ishlatiladi).</summary>
        public static string SellerTin => Get("SellerTin");

        public static int TimeoutSeconds
        {
            get
            {
                int value;
                return int.TryParse(Get("TimeoutSeconds"), out value) && value > 0 ? value : 30;
            }
        }
    }

    public static class EDocumentProviders
    {
        public static IEnumerable<IEDocumentProvider> All()
        {
            yield return new InternalEDocumentProvider();
            yield return new DidoxEDocumentProvider();
            yield return new FakturaUzEDocumentProvider();
        }

        public static IEDocumentProvider ByCode(string code)
        {
            return All().FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Sozlamadagi provayder; noma'lum kod bo'lsa — ichki.</summary>
        public static IEDocumentProvider Active()
        {
            return ByCode(EDocumentSettings.ActiveProviderCode) ?? new InternalEDocumentProvider();
        }
    }

    /// <summary>Ichki rejim: hujjat tizimda qoladi, tashqariga hech narsa yuborilmaydi.</summary>
    public sealed class InternalEDocumentProvider : IEDocumentProvider
    {
        public string Code => EDocumentSettings.InternalCode;
        public string Title => L.T("Crm_EdocProviderInternal");
        public bool IsExternal => false;
        public bool IsConfigured => true;

        public Task<EDocumentResult> SendInvoiceAsync(EInvoiceDocument document)
        {
            return Task.FromResult(Local());
        }

        public Task<EDocumentResult> SendWaybillAsync(EWaybillDocument document)
        {
            return Task.FromResult(Local());
        }

        public Task<EDocumentResult> GetStatusAsync(EDocumentKind kind, string externalId)
        {
            return Task.FromResult(Local());
        }

        private static EDocumentResult Local()
        {
            return new EDocumentResult { Success = true, Status = EDocumentStatus.Local, Message = L.T("Crm_EdocStoredLocally") };
        }
    }

    /// <summary>
    /// HTTP/JSON asosidagi EDO provayderi uchun umumiy qism: manzil, avtorizatsiya sarlavhasi, vaqt chegarasi,
    /// javobdan hujjat ID va holatini o'qish. Sozlamalar: EDoc:{Code}:BaseUrl, :AuthHeader, :Token,
    /// :InvoicePath, :WaybillPath, :StatusPath ("{id}" va "{kind}" o'rinbosarlari bilan).
    ///
    /// Provayder API'si shartnoma tuzilgach hujjatlari bo'yicha <see cref="MapInvoice"/>, <see cref="MapWaybill"/>
    /// va <see cref="ParseResponse"/> qayta yoziladi — qolgan oqim (holat saqlash, UI) o'zgarmaydi.
    /// </summary>
    public abstract class HttpEDocumentProvider : IEDocumentProvider
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(EDocumentSettings.TimeoutSeconds) };

        public abstract string Code { get; }
        public abstract string Title { get; }
        public bool IsExternal => true;

        protected string Setting(string key, string fallback = null)
        {
            return EDocumentSettings.Get(SettingsPrefix + ":" + key, fallback);
        }

        /// <summary>appSettings'dagi bo'lim nomi (masalan "Didox").</summary>
        protected abstract string SettingsPrefix { get; }

        public virtual bool IsConfigured =>
            !string.IsNullOrEmpty(Setting("BaseUrl")) && !string.IsNullOrEmpty(Setting("Token"))
            && !string.IsNullOrEmpty(Setting("InvoicePath"));

        public Task<EDocumentResult> SendInvoiceAsync(EInvoiceDocument document)
        {
            return PostAsync(Setting("InvoicePath"), MapInvoice(document));
        }

        public Task<EDocumentResult> SendWaybillAsync(EWaybillDocument document)
        {
            var path = Setting("WaybillPath");
            if (string.IsNullOrEmpty(path))
                return Task.FromResult(EDocumentResult.Fail(L.T("Crm_EdocWaybillPathMissing", Title, "EDoc:" + SettingsPrefix + ":WaybillPath")));
            return PostAsync(path, MapWaybill(document));
        }

        public async Task<EDocumentResult> GetStatusAsync(EDocumentKind kind, string externalId)
        {
            var path = Setting("StatusPath");
            if (!IsConfigured || string.IsNullOrEmpty(path))
                return EDocumentResult.Fail(L.T("Crm_EdocStatusPathMissing", Title, "EDoc:" + SettingsPrefix + ":StatusPath"));
            if (string.IsNullOrEmpty(externalId))
                return EDocumentResult.Fail(L.T("Crm_EdocNotSentYet"));

            var url = Combine(path.Replace("{id}", Uri.EscapeDataString(externalId)).Replace("{kind}", KindSegment(kind)));
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    Authorize(request);
                    using (var response = await Client.SendAsync(request).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = ParseResponse(response.IsSuccessStatusCode, body);
                        result.ExternalId = result.ExternalId ?? externalId;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                return EDocumentResult.Fail(L.T("Crm_EdocConnectionError", Title, ex.GetBaseException().Message));
            }
        }

        /// <summary>Hujjat turi uchun URL bo'lagi (StatusPath'dagi {kind}).</summary>
        protected virtual string KindSegment(EDocumentKind kind)
        {
            return kind == EDocumentKind.Invoice ? "invoice" : "waybill";
        }

        /// <summary>Provayder formatidagi SF JSON'i. Standart — umumiy model o'zi.</summary>
        protected virtual object MapInvoice(EInvoiceDocument document)
        {
            return document;
        }

        protected virtual object MapWaybill(EWaybillDocument document)
        {
            return document;
        }

        /// <summary>Javobdan hujjat ID, holat va xabarni o'qiydi (keng tarqalgan maydon nomlari bo'yicha).</summary>
        protected virtual EDocumentResult ParseResponse(bool httpSuccess, string body)
        {
            JToken json = null;
            try { json = string.IsNullOrWhiteSpace(body) ? null : JToken.Parse(body); } catch (JsonException) { }

            string Pick(params string[] names)
            {
                if (!(json is JObject obj)) return null;
                foreach (var n in names)
                {
                    var token = obj.SelectToken(n);
                    if (token != null && token.Type != JTokenType.Null && token.Type != JTokenType.Object && token.Type != JTokenType.Array)
                        return token.ToString();
                }
                return null;
            }

            var message = Pick("message", "error", "errorMessage", "data.message") ?? (json == null ? Truncate(body, 300) : null);
            if (!httpSuccess)
                return EDocumentResult.Fail(L.T("Crm_EdocProviderReturnedError", Title, message ?? L.T("Crm_EdocUnknownError")));

            var status = (Pick("status", "doc_status", "data.status", "state") ?? "").ToUpperInvariant();
            return new EDocumentResult
            {
                Success = true,
                ExternalId = Pick("id", "_id", "doc_id", "document_id", "data._id", "data.id", "uid"),
                Status = MapStatus(status),
                Message = message
            };
        }

        /// <summary>Provayder holat kodi → <see cref="EDocumentStatus"/>.</summary>
        protected virtual string MapStatus(string providerStatus)
        {
            if (string.IsNullOrEmpty(providerStatus)) return EDocumentStatus.Sent;
            if (providerStatus.Contains("REJECT") || providerStatus.Contains("CANCEL") || providerStatus.Contains("DECLIN")) return EDocumentStatus.Rejected;
            if (providerStatus.Contains("ACCEPT") || providerStatus.Contains("SIGNED") || providerStatus.Contains("APPROV")) return EDocumentStatus.Accepted;
            return EDocumentStatus.Sent;
        }

        private async Task<EDocumentResult> PostAsync(string path, object payload)
        {
            if (!IsConfigured)
                return EDocumentResult.Fail(L.T("Crm_EdocProviderNotConfigured", Title, "EDoc:" + SettingsPrefix + ":BaseUrl / :Token / :InvoicePath"));

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, Combine(path)))
                {
                    Authorize(request);
                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    using (var response = await Client.SendAsync(request).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return ParseResponse(response.IsSuccessStatusCode, body);
                    }
                }
            }
            catch (Exception ex)
            {
                return EDocumentResult.Fail(L.T("Crm_EdocConnectionError", Title, ex.GetBaseException().Message));
            }
        }

        private void Authorize(HttpRequestMessage request)
        {
            var header = Setting("AuthHeader", "Authorization");
            var token = Setting("Token");
            if (!string.IsNullOrEmpty(token))
                request.Headers.TryAddWithoutValidation(header, token);
        }

        private string Combine(string path)
        {
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute)) return path;
            return Setting("BaseUrl", "").TrimEnd('/') + "/" + (path ?? "").TrimStart('/');
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max) + "…";
        }
    }

    /// <summary>Didox (didox.uz) — partner API. Endpoint va maydonlar shartnoma tuzilgach moslashtiriladi.</summary>
    public sealed class DidoxEDocumentProvider : HttpEDocumentProvider
    {
        public override string Code => EDocumentSettings.DidoxCode;
        public override string Title => "Didox";
        protected override string SettingsPrefix => "Didox";
    }

    /// <summary>Faktura.uz — API. Endpoint va maydonlar shartnoma tuzilgach moslashtiriladi.</summary>
    public sealed class FakturaUzEDocumentProvider : HttpEDocumentProvider
    {
        public override string Code => EDocumentSettings.FakturaUzCode;
        public override string Title => "Faktura.uz";
        protected override string SettingsPrefix => "FakturaUz";
    }
}
