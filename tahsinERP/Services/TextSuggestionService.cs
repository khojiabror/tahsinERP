using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace tahsinERP.Services
{
    /// <summary>
    /// Buyruq/xat matni taklifi: Gemini (Web.config: UseAITextSuggestion=true, GeminiAPIKey) yoki qoidaga asoslangan.
    ///
    /// Avvalgi muammolar (DOC-B09, DOC-S04): har controller nusxasida yangi HttpClient (soket tugashi),
    /// async metod .Result bilan kutilardi (ASP.NET sync-context'da deadlock), API kaliti URL query'da
    /// (loglarga tushadi). Endi — bitta statik HttpClient, to'liq async, kalit header'da.
    /// </summary>
    public class TextSuggestionService : ITextSuggestionService
    {
        private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private readonly string _aiApiKey;
        private readonly bool _useAI;

        public TextSuggestionService()
        {
            _useAI = ConfigurationManager.AppSettings["UseAITextSuggestion"] == "true";
            _aiApiKey = ConfigurationManager.AppSettings["GeminiAPIKey"] ?? "";
        }

        private bool AiEnabled => _useAI && !string.IsNullOrEmpty(_aiApiKey);

        public async Task<string> SuggestOrderBodyAsync(CompanyOrderMeta meta)
        {
            if (AiEnabled)
            {
                var prompt = $"O'zbek tilida kompaniya buyrug'i matnini yozing. " +
                    $"Buyruq turi: {meta.OrderTypeCode}, " +
                    $"Sana: {meta.OrderDate:dd.MM.yyyy}, " +
                    $"Bo'lim: {meta.OrgUnitName ?? "Noma'lum"}, " +
                    $"Mavzu: {meta.Topic ?? "Noma'lum"}. " +
                    $"Matn rasmiy, professional va to'liq bo'lsin. " +
                    $"3-5 banddan iborat bo'lsin.";

                var aiResult = await CallGeminiAsync(prompt).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(aiResult))
                    return aiResult;
            }

            return SuggestOrderBodyRuleBased(meta);
        }

        public async Task<string> SuggestLetterBodyAsync(OfficialLetterMeta meta)
        {
            if (AiEnabled)
            {
                var prompt = $"O'zbek tilida rasmiy maktub matnini yozing. " +
                    $"Qabul qiluvchi: {meta.ExternalCompanyName ?? "Noma'lum"} kompaniyasi, " +
                    $"Kontakt: {meta.ExternalContactName ?? "Noma'lum"}, " +
                    $"Mavzu: {meta.Topic ?? "Noma'lum"}, " +
                    $"Sana: {meta.LetterDate:dd.MM.yyyy}. " +
                    $"Matn xushmuomala, professional va tijorat maktubiga mos bo'lsin. " +
                    $"3-5 banddan iborat bo'lsin.";

                var aiResult = await CallGeminiAsync(prompt).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(aiResult))
                    return aiResult;
            }

            return SuggestLetterBodyRuleBased(meta);
        }

        /// <summary>Gemini'ga so'rov; xato bo'lsa bo'sh satr (qoidaga asoslangan matnga o'tiladi).</summary>
        private async Task<string> CallGeminiAsync(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                using (var request = new HttpRequestMessage(HttpMethod.Post, GeminiUrl))
                {
                    request.Headers.Add("x-goog-api-key", _aiApiKey);
                    request.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                    using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Trace.TraceWarning("Gemini API: HTTP {0}", (int)response.StatusCode);
                            return "";
                        }

                        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseJson);
                        return result?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Gemini API xatosi: {0}", ex.Message);
                return "";
            }
        }

        private string SuggestOrderBodyRuleBased(CompanyOrderMeta meta)
        {
            var sb = new StringBuilder();

            // Build Uzbek text based on order type and metadata
            sb.AppendLine("Ushbu buyruq asosida quyidagilar amalga oshiriladi:");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(meta.Topic))
            {
                sb.AppendLine(meta.Topic);
                sb.AppendLine();
            }

            if (meta.ResponsibleEmployeeNames != null && meta.ResponsibleEmployeeNames.Any())
            {
                sb.AppendLine("Javobgar shaxslar:");
                foreach (var emp in meta.ResponsibleEmployeeNames)
                {
                    sb.AppendLine($"- {emp}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(meta.OrgUnitName))
            {
                sb.AppendLine($"Tashkilot bo'limi: {meta.OrgUnitName}");
                sb.AppendLine();
            }

            sb.AppendLine($"Buyruq sanasi: {meta.OrderDate:dd.MM.yyyy}");
            sb.AppendLine();
            sb.AppendLine("Buyruqni bajarish muddati: 30 kun");

            return sb.ToString();
        }

        private string SuggestLetterBodyRuleBased(OfficialLetterMeta meta)
        {
            var sb = new StringBuilder();

            // Build polite commercial Uzbek letter
            sb.AppendLine("Hurmatli " + (string.IsNullOrEmpty(meta.ExternalContactName) ? "hamkor" : meta.ExternalContactName) + "!");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(meta.Topic))
            {
                sb.AppendLine(meta.Topic);
                sb.AppendLine();
            }

            sb.AppendLine("Ushbu maktub orqali sizga quyidagi ma'lumotlarni yetkazmoqdamiz:");
            sb.AppendLine();
            sb.AppendLine("Bizning kompaniyamiz yuqori sifatli mahsulotlar va xizmatlarni taklif etmoqda.");
            sb.AppendLine("Batafsil ma'lumot uchun biz bilan bog'laning.");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(meta.OurRefNo))
            {
                sb.AppendLine($"Bizning havola raqami: {meta.OurRefNo}");
            }

            if (!string.IsNullOrEmpty(meta.TheirRefNo))
            {
                sb.AppendLine($"Sizning havola raqamingiz: {meta.TheirRefNo}");
            }

            sb.AppendLine();
            sb.AppendLine("Hurmat bilan,");
            sb.AppendLine("Direktor");

            return sb.ToString();
        }
    }
}
