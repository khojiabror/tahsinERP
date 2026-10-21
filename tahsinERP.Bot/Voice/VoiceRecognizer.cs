using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace tahsinERP.Bot.Voice;

/// <summary>Ovozli buyurtma: nutq → matn → tovar/miqdor. Kalit yo'q bo'lsa <see cref="Enabled"/> = false (bot "sozlanmagan" deydi).</summary>
public interface IVoiceRecognizer
{
    bool Enabled { get; }
    int MaxSeconds { get; }
    Task<string?> TranscribeAsync(byte[] audio, string fileName, string lang, CancellationToken ct);
    Task<VoiceOrderParse> ExtractAsync(string transcript, string lang, CancellationToken ct);
}

/// <summary>"Bot:Voice" bo'limi. Kalit — muhit o'zgaruvchisi Bot__Voice__GroqApiKey (Taskly'dagi kabi Groq).</summary>
public sealed class VoiceOptions
{
    public string? GroqApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public string TranscribeModel { get; set; } = "whisper-large-v3";
    /// <summary>Bo'sh bo'lsa — faqat qoidaviy ajratgich (<see cref="VoiceParser"/>).</summary>
    public string? ChatModel { get; set; } = "llama-3.3-70b-versatile";
    public int MaxSeconds { get; set; } = 90;
}

/// <summary>
/// Groq (OpenAI bilan mos API): Whisper — nutqni matnga (uz/ru, qurilish mollari lug'ati prompt sifatida), so'ng chat modeli JSON rejimida
/// mijoz va qatorlarni ajratadi. Model xato bersa yoki JSON yaroqsiz bo'lsa — qoidaviy ajratgich. Ovoz faqat shu so'rov uchun yuboriladi, saqlanmaydi.
/// </summary>
public sealed class GroqVoiceRecognizer(HttpClient http, IOptions<BotOptions> options, ILogger<GroqVoiceRecognizer> log) : IVoiceRecognizer
{
    public const string HttpClientName = "groq";
    private const string Vocabulary = "sement, M400, M500, kafel, g'isht, armatura, shpaklyovka, gips, profil, qop, quti, dona, poddon, m2; цемент, плитка, кирпич, арматура, мешок, коробка";

    private VoiceOptions O => options.Value.Voice ?? new VoiceOptions();

    public bool Enabled => !string.IsNullOrWhiteSpace(O.GroqApiKey);

    public int MaxSeconds => Math.Clamp(O.MaxSeconds, 10, 300);

    private Uri Url(string path) => new(new Uri(O.BaseUrl.EndsWith('/') ? O.BaseUrl : O.BaseUrl + "/"), path);

    public async Task<string?> TranscribeAsync(byte[] audio, string fileName, string lang, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(O.TranscribeModel), "model");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent(Vocabulary), "prompt");
        if (lang == "ru") form.Add(new StringContent("ru"), "language");
        using var req = new HttpRequestMessage(HttpMethod.Post, Url("audio/transcriptions")) { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.GroqApiKey);
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("Groq transcription {Status}: {Body}", (int)res.StatusCode, text.Length > 300 ? text[..300] : text);
            throw new HttpRequestException("Groq " + (int)res.StatusCode);
        }
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString()?.Trim() : null;
    }

    public async Task<VoiceOrderParse> ExtractAsync(string transcript, string lang, CancellationToken ct)
    {
        var fallback = VoiceParser.Parse(transcript);
        if (string.IsNullOrWhiteSpace(O.ChatModel)) return fallback;
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model = O.ChatModel,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Siz qurilish mollari ulgurji savdosida agentning ovozli buyurtmasini tahlil qilasiz (o'zbek yoki rus tilida). " +
                                  "Faqat JSON qaytaring: {\"customer\": string|null, \"items\": [{\"name\": string, \"qty\": number, \"unit\": string|null}]}. " +
                                  "customer — do'kon/mijoz nomi (bo'lsa), name — tovar nomi va markasi (masalan \"sement M400\"), qty — son (so'z bilan aytilgan bo'lsa ham raqam), " +
                                  "unit — qop/quti/dona/kg/m2/poddon kabi birlik. Taxmin qilmang, aytilmaganini qo'shmang."
                    },
                    new { role = "user", content = transcript }
                }
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, Url("chat/completions"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.GroqApiKey);
            using var res = await http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("Groq chat {Status}", (int)res.StatusCode);
                return fallback;
            }
            using var doc = JsonDocument.Parse(text);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return ParseModelJson(content) ?? fallback;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException
                                       || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Groq ajratish xatosi — qoidaviy ajratgich");
            return fallback;
        }
    }

    /// <summary>Model javobi: {"customer":..,"items":[{"name","qty","unit"}]}; yaroqsiz yoki bo'sh — null.</summary>
    internal static VoiceOrderParse? ParseModelJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? customer = root.TryGetProperty("customer", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var items = new List<VoiceOrderItem>();
            if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in arr.EnumerateArray().Take(30))
                {
                    var name = it.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() : null;
                    decimal qty = 0;
                    if (it.TryGetProperty("qty", out var q))
                    {
                        if (q.ValueKind == JsonValueKind.Number) qty = q.GetDecimal();
                        else if (q.ValueKind == JsonValueKind.String)
                            decimal.TryParse(q.GetString()?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out qty);
                    }
                    var unit = it.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                    if (!string.IsNullOrEmpty(name) && qty > 0 && qty < 1_000_000) items.Add(new VoiceOrderItem(name.Length > 150 ? name[..150] : name, Math.Round(qty, 4), unit));
                }
            }
            return items.Count == 0 ? null : new VoiceOrderParse(string.IsNullOrWhiteSpace(customer) ? null : customer.Trim(), items);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
