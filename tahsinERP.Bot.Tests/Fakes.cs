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
