using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace tahsinERP.Services
{
    public class TextSuggestionService : ITextSuggestionService
    {
        private readonly string _aiApiKey;
        private readonly bool _useAI;
        private readonly HttpClient _httpClient;

        public TextSuggestionService()
        {
            // Check if AI is enabled in Web.config
            _useAI = ConfigurationManager.AppSettings["UseAITextSuggestion"] == "true";
            _aiApiKey = ConfigurationManager.AppSettings["GeminiAPIKey"] ?? "";
            _httpClient = new HttpClient();
        }

        public string SuggestOrderBody(CompanyOrderMeta meta)
        {
            // Try AI first if enabled, fallback to rule-based
            if (_useAI && !string.IsNullOrEmpty(_aiApiKey))
            {
                try
                {
                    var aiResult = SuggestOrderBodyWithAI(meta).Result;
                    if (!string.IsNullOrEmpty(aiResult))
                        return aiResult;
                }
                catch (Exception ex)
                {
                    // Log error but continue with rule-based fallback
                    System.Diagnostics.Debug.WriteLine($"AI suggestion failed: {ex.Message}");
                }
            }

            // Rule-based fallback
            return SuggestOrderBodyRuleBased(meta);
        }

        public string SuggestLetterBody(OfficialLetterMeta meta)
        {
            // Try AI first if enabled, fallback to rule-based
            if (_useAI && !string.IsNullOrEmpty(_aiApiKey))
            {
                try
                {
                    var aiResult = SuggestLetterBodyWithAI(meta).Result;
                    if (!string.IsNullOrEmpty(aiResult))
                        return aiResult;
                }
                catch (Exception ex)
                {
                    // Log error but continue with rule-based fallback
                    System.Diagnostics.Debug.WriteLine($"AI suggestion failed: {ex.Message}");
                }
            }

            // Rule-based fallback
            return SuggestLetterBodyRuleBased(meta);
        }

        private async Task<string> SuggestOrderBodyWithAI(CompanyOrderMeta meta)
        {
            try
            {
                // Use Google Gemini API (free tier available)
                var prompt = $"O'zbek tilida kompaniya buyrug'i matnini yozing. " +
                    $"Buyruq turi: {meta.OrderTypeCode}, " +
                    $"Sana: {meta.OrderDate:dd.MM.yyyy}, " +
                    $"Bo'lim: {meta.OrgUnitName ?? "Noma'lum"}, " +
                    $"Mavzu: {meta.Topic ?? "Noma'lum"}. " +
                    $"Matn rasmiy, professional va to'liq bo'lsin. " +
                    $"3-5 banddan iborat bo'lsin.";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_aiApiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseJson);
                    return result?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gemini API error: {ex.Message}");
            }

            return "";
        }

        private async Task<string> SuggestLetterBodyWithAI(OfficialLetterMeta meta)
        {
            try
            {
                // Use Google Gemini API
                var prompt = $"O'zbek tilida rasmiy maktub matnini yozing. " +
                    $"Qabul qiluvchi: {meta.ExternalCompanyName ?? "Noma'lum"} kompaniyasi, " +
                    $"Kontakt: {meta.ExternalContactName ?? "Noma'lum"}, " +
                    $"Mavzu: {meta.Topic ?? "Noma'lum"}, " +
                    $"Sana: {meta.LetterDate:dd.MM.yyyy}. " +
                    $"Matn xushmuomala, professional va tijorat maktubiga mos bo'lsin. " +
                    $"3-5 banddan iborat bo'lsin.";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_aiApiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseJson);
                    return result?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gemini API error: {ex.Message}");
            }

            return "";
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

