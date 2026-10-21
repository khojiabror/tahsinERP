using System.Net;
using System.Text;

namespace tahsinERP.Bot.Tests;

/// <summary>So'rovlarni yozib oladigan va javobni funksiya bilan qaytaradigan HTTP handler.</summary>
public sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public sealed record Captured(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, byte[] Body)
    {
        public string Text => Encoding.UTF8.GetString(Body);
    }

    public List<Captured> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content == null ? [] : await request.Content.ReadAsByteArrayAsync(ct);
        lock (Requests)
            Requests.Add(new Captured(request.Method, request.RequestUri!, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), body));
        return respond(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Ovozli buyurtma uchun soxta tanuvchi: tayyor matn va qoidaviy ajratgich.</summary>
public sealed class FakeVoice : tahsinERP.Bot.Voice.IVoiceRecognizer
{
    public bool Enabled { get; set; } = true;
    public int MaxSeconds => 90;
    public string? Transcript { get; set; } = "Baraka do'koniga 20 qop sement M400, 5 quti kafel";
    public int Calls { get; private set; }

    public Task<string?> TranscribeAsync(byte[] audio, string fileName, string lang, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Transcript);
    }

    public Task<tahsinERP.Bot.Voice.VoiceOrderParse> ExtractAsync(string transcript, string lang, CancellationToken ct) =>
        Task.FromResult(tahsinERP.Bot.Voice.VoiceParser.Parse(transcript));
}
