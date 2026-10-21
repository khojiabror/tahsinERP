using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Documents;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Telegram;
using tahsinERP.Bot.Voice;

namespace tahsinERP.Bot.Tests;

/// <summary>2-bosqich: ovozli buyurtma ajratgichi, nakladnoy PDF, bildirishnoma payload'i, sendDocument.</summary>
public class DeliveryHelperTests
{
    [Fact]
    public void VoiceParser_CustomerAndItems_Uzbek()
    {
        var p = VoiceParser.Parse("Baraka do'koniga 20 qop sement M400, 5 quti kafel va yigirma besh dona g'isht");
        Assert.Equal("Baraka", p.Customer);
        Assert.Equal(3, p.Items.Count);
        Assert.Equal(new VoiceOrderItem("sement M400", 20, "qop"), p.Items[0]);
        Assert.Equal(new VoiceOrderItem("kafel", 5, "quti"), p.Items[1]);
        Assert.Equal(new VoiceOrderItem("g'isht", 25, "dona"), p.Items[2]);
    }

    [Fact]
    public void VoiceParser_Russian_WordsAndTrailingNumber()
    {
        var p = VoiceParser.Parse("Для магазина Анвар: двадцать мешков цемента, плитка 60x60 10 коробок");
        Assert.NotNull(p.Customer);
        Assert.Contains("Анвар", p.Customer);
        Assert.Equal(2, p.Items.Count);
        Assert.Equal(20, p.Items[0].Qty);
        Assert.Equal("мешков", p.Items[0].Unit);
        Assert.Equal("цемента", p.Items[0].Name);
        Assert.Equal(new VoiceOrderItem("плитка 60x60", 10, "коробок"), p.Items[1]);
    }

    [Theory]
    [InlineData("ikki yuz", 200)]
    [InlineData("bir ming besh yuz", 1500)]
    [InlineData("двести пятьдесят", 250)]
    [InlineData("2,5", 2.5)]
    [InlineData("12ta", 12)]
    public void VoiceParser_Numbers(string words, double expected) =>
        Assert.Equal((decimal)expected, VoiceParser.ReadNumber(words.Split(' '), 0).Qty);

    [Fact]
    public void VoiceParser_Empty_NoItems()
    {
        Assert.Empty(VoiceParser.Parse("").Items);
        Assert.Empty(VoiceParser.Parse("salom qalaysiz").Items);
        Assert.Null(VoiceParser.Parse("20 qop sement").Customer);
    }

    [Fact]
    public void ModelJson_ParsedAndValidated()
    {
        var p = GroqVoiceRecognizer.ParseModelJson("{\"customer\":\"Baraka\",\"items\":[{\"name\":\"sement M400\",\"qty\":20,\"unit\":\"qop\"},{\"name\":\"kafel\",\"qty\":\"5\"},{\"name\":\"\",\"qty\":3},{\"name\":\"x\",\"qty\":0}]}");
        Assert.NotNull(p);
        Assert.Equal("Baraka", p!.Customer);
        Assert.Equal(2, p.Items.Count);
        Assert.Equal(5, p.Items[1].Qty);
        Assert.Null(GroqVoiceRecognizer.ParseModelJson("not json"));
        Assert.Null(GroqVoiceRecognizer.ParseModelJson("{\"items\":[]}"));
    }

    [Fact]
    public void InvoicePdf_RendersCyrillicAndLatin()
    {
        var inv = new ErpInvoice("Qurilish Savdo MChJ", "Asosiy ombor", "DJ-2026-00012", "D-2026-00031", "21.10.2026", "Магазин «Баракa» o'g'il",
            "123456789", "Chilonzor 5", "+998901234567", "Ali", "01A123BC", "R-2026-00001", true,
            [new ErpInvoiceLine("T1 — Sement M400 (цемент)", "qop", 6, 55000, 330000)], 330000, 330000, "21.10.2026 14:30");
        var pdf = InvoicePdf.Render(inv, "uz");
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal("Nakladnoy_DJ-2026-00012.pdf", InvoicePdf.FileName(inv));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(InvoicePdf.Render(inv with { Partial = false }, "ru"), 0, 4));
    }

    [Fact]
    public void InvoicePayload_StopId()
    {
        Assert.Equal(15, NotificationPoller.InvoiceStopId("{\"action\":\"invoice\",\"stopId\":15}"));
        Assert.Null(NotificationPoller.InvoiceStopId("{\"action\":\"override\",\"orderId\":15}"));
        Assert.Null(NotificationPoller.InvoiceStopId(null));
        Assert.Null(NotificationPoller.InvoiceStopId("bad"));
    }

    [Fact]
    public async Task SendDocument_Multipart()
    {
        var h = new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"));
        var api = new TelegramApi(new HttpClient(h));
        var r = await api.SendDocumentAsync("123:tok", 777, "✅ yetkazildi", "Nakladnoy_DJ-1.pdf", [0x25, 0x50, 0x44, 0x46], CancellationToken.None);
        Assert.True(r.Ok);
        var req = h.Requests.Single();
        Assert.EndsWith("/bot123:tok/sendDocument", req.Uri.AbsoluteUri);
        Assert.Matches("name=\"?document\"?; filename=\"?Nakladnoy_DJ-1.pdf", req.Text);
        Assert.Contains("777", req.Text);
        Assert.Contains("application/pdf", req.Text);
    }

    [Fact]
    public void Urls_AndStopButton()
    {
        Assert.Equal("https://bot.uz/app/deliver.html?t=firma1&s=9", UpdateRouter.DeliverAppUrl("https://bot.uz/", "firma1", 9));
        var s = new ErpStop(9, 2, "Do'kon", null, null, null, null, null, "D-1", "DJ-1", 550000, null, 0, "Kutmoqda", null, 0, 500, null, null);
        Assert.Equal("⏳ 2. Do'kon · 550 000", UpdateRouter.StopButton(s));
        Assert.StartsWith("✅", UpdateRouter.StopButton(s with { Status = 1 }));
        Assert.StartsWith("🚫", UpdateRouter.StopButton(s with { Status = 3 }));
    }
}

/// <summary>Ekspeditor va ovozli buyurtma oqimlari (soxta ERP bilan).</summary>
public partial class HandlerTests
{
    private const string TripJson = "{\"id\":4,\"docNo\":\"R-2026-00004\",\"date\":\"21.10.2026\",\"status\":1,\"statusName\":\"Yo'lda\",\"vehicle\":\"01A123BC\"," +
        "\"weightKg\":1500,\"total\":1650000,\"routeUrl\":\"https://yandex.uz/maps/?rtext=41.3,69.2~41.31,69.24&rtt=auto\",\"expenses\":0,\"stops\":[" +
        "{\"id\":11,\"seq\":1,\"customer\":\"Baraka\",\"amount\":550000,\"status\":0,\"statusName\":\"Kutmoqda\",\"debt\":550000,\"weightKg\":500}," +
        "{\"id\":12,\"seq\":2,\"customer\":\"Anvar\",\"amount\":1100000,\"status\":1,\"statusName\":\"Yetkazildi\",\"debt\":0,\"weightKg\":1000}]}";

    private const string StopJson = "{\"trip\":{\"id\":4,\"docNo\":\"R-2026-00004\",\"status\":1},\"stop\":{\"id\":11,\"seq\":1,\"customer\":\"Baraka\"," +
        "\"address\":\"Chilonzor 5\",\"phone\":\"+998901112233\",\"lat\":41.31,\"lng\":69.24,\"orderNo\":\"D-2026-00031\",\"shipmentNo\":\"DJ-2026-00012\"," +
        "\"amount\":550000,\"status\":0,\"statusName\":\"Kutmoqda\",\"debt\":550000,\"weightKg\":500,\"navUrl\":\"https://yandex.uz/maps/?rtext=~41.31,69.24&rtt=auto\"," +
        "\"lines\":[{\"id\":21,\"product\":\"T1 — Sement\",\"unit\":\"qop\",\"qty\":10,\"amount\":550000,\"delivered\":null}]}}";

    /// <summary>Javob klaviaturasidagi tugma matnlari (JSON'da emoji escape qilinadi).</summary>
    private static List<string> Buttons(BotOutboxItem o)
    {
        var result = new List<string>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == "text" && p.Value.ValueKind == JsonValueKind.String) result.Add(p.Value.GetString()!);
                    else Walk(p.Value);
                }
            else if (e.ValueKind == JsonValueKind.Array)
                foreach (var x in e.EnumerateArray()) Walk(x);
        }
        var root = JsonDocument.Parse(o.Payload).RootElement;
        if (root.TryGetProperty("reply_markup", out var m)) Walk(m);
        return result;
    }

    private static TgUpdate VoiceMsg(long updateId, int seconds = 5) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, null, null, null, null, null,
            null, new TgVoice("voice1", seconds, "audio/ogg", 2000)), null, null);

    [Fact]
    public async Task Expeditor_MenuTripStopAndPhotoDelivery()
    {
        LinkAs("EXPEDITOR");
        var menu = Buttons(Out().Last());
        Assert.Contains("🚚 Reys", menu);
        Assert.Contains("🏁 Reysni yopish", menu);
        Assert.DoesNotContain("➕ Yangi nuqta", menu);

        _routes["MyTrip"] = _ => (HttpStatusCode.OK, TripJson);
        await Handle(Text(2, "🚚 Reys"));
        var trip = Out().Last();
        Assert.Contains("R-2026-00004", TextOf(trip));
        Assert.Contains("1/2", TextOf(trip));
        Assert.Contains("\"callback_data\":\"st:11\"", trip.Payload);
        Assert.Contains("yandex.uz/maps", trip.Payload);
        Assert.Contains("tx:4", trip.Payload);
        Assert.DoesNotContain("ts:4", trip.Payload);   // allaqachon yo'lda

        _routes["TripStop"] = _ => (HttpStatusCode.OK, StopJson);
        await Handle(Click(3, "st:11"));
        var card = Out().Last();
        Assert.Contains("+998901112233", TextOf(card));
        Assert.Contains("T1 — Sement — 10 qop", TextOf(card));
        Assert.Contains("rtext=~41.31,69.24", card.Payload);
        Assert.Contains("/app/deliver.html?t=firma1&s=11", card.Payload);
        Assert.Contains("sd:11", card.Payload);
        Assert.Contains("sr:11", card.Payload);

        await Handle(Click(4, "sd:11"));
        Assert.Contains("rasmini", TextOf(Out().Last()));
        _routes["StopDeliver"] = _ => (HttpStatusCode.OK, "{\"status\":\"Delivered\",\"deliveredAmount\":550000,\"amount\":550000}");
        await Handle(Photo(5));
        var body = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/StopDeliver")).Text).RootElement;
        Assert.Equal(11, body.GetProperty("stopId").GetInt32());
        Assert.True(body.GetProperty("all").GetBoolean());
        Assert.StartsWith("firma1/", body.GetProperty("photoKey").GetString());
        Assert.Contains(Out().Select(TextOf), t => t.Contains("Yetkazildi: 550 000"));
    }

    [Fact]
    public async Task Expeditor_RefusePaymentExpenseAndClose()
    {
        LinkAs("EXPEDITOR");
        _routes["MyTrip"] = _ => (HttpStatusCode.OK, TripJson);
        _routes["Reasons"] = r => (HttpStatusCode.OK, r.Uri.Query.Contains("type=RETURN") ? "[{\"id\":7,\"name\":\"Mijoz rad etdi\"}]" : "[]");
        _routes["StopDeliver"] = _ => (HttpStatusCode.OK, "{\"status\":\"Refused\",\"deliveredAmount\":0,\"amount\":550000}");
        await Handle(Click(2, "sr:11"));
        Assert.Contains("srr:11:7", Out().Last().Payload);
        await Handle(Click(3, "srr:11:7"));
        var refuse = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/StopDeliver")).Text).RootElement;
        Assert.False(refuse.GetProperty("all").GetBoolean());
        Assert.Equal(7, refuse.GetProperty("reasonId").GetInt32());

        // to'lov manzil bo'yicha: StopPayment
        _routes["TripStop"] = _ => (HttpStatusCode.OK, StopJson);
        _routes["Me"] = _ => (HttpStatusCode.OK, "{\"userId\":12,\"payMethods\":[\"CASH\"]}");
        _routes["StopPayment"] = _ => (HttpStatusCode.OK, "{\"id\":3,\"docNo\":\"TOL-9\",\"amount\":200000,\"onHand\":200000}");
        await Handle(Click(4, "sp:11"));
        Assert.Contains("550 000", TextOf(Out().Last()));
        await Handle(Text(5, "200000"));
        await Handle(Click(6, "pm:CASH"));
        var pay = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/StopPayment")).Text).RootElement;
        Assert.Equal(11, pay.GetProperty("stopId").GetInt32());
        Assert.Equal(200000m, pay.GetProperty("amount").GetDecimal());
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/Payment"));
        Assert.Contains("TOL-9", TextOf(Out().Last()));

        // xarajat: tur → summa (izoh bilan)
        _routes["TripExpense"] = _ => (HttpStatusCode.OK, "{\"id\":1}");
        await Handle(Click(7, "tx:4"));
        Assert.Contains("txk:4:FUEL", Out().Last().Payload);
        await Handle(Click(8, "txk:4:FUEL"));
        await Handle(Text(9, "150000 AI-92"));
        var exp = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/TripExpense")).Text).RootElement;
        Assert.Equal("FUEL", exp.GetProperty("kind").GetString());
        Assert.Equal(150000m, exp.GetProperty("amount").GetDecimal());
        Assert.Equal("AI-92", exp.GetProperty("note").GetString());

        // yopish: kutilayotgan manzil bor — rad; hammasi hal — tasdiq → TripClose
        await Handle(Text(10, "🏁 Reysni yopish"));
        Assert.Contains(Out().Select(TextOf), t => t.Contains("1 ta manzil"));
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/TripClose"));
        _routes["MyTrip"] = _ => (HttpStatusCode.OK, TripJson.Replace("\"status\":0", "\"status\":3"));
        await Handle(Text(11, "🏁 Reysni yopish"));
        Assert.Contains("tc!:4", Out().Last().Payload);
        _routes["TripClose"] = _ => (HttpStatusCode.OK, "{\"docNo\":\"R-2026-00004\",\"delivered\":1,\"partial\":0,\"refused\":1,\"deliveredAmount\":1100000,\"returnedAmount\":550000,\"collected\":200000,\"onHand\":200000}");
        await Handle(Click(12, "tc!:4"));
        Assert.Contains(Out().Select(TextOf), t => t.Contains("R-2026-00004 yopildi") && t.Contains("200 000"));
        Assert.Contains("\"callback_data\":\"ho\"", Out().Last().Payload);
    }

    [Fact]
    public async Task Expeditor_PlannedTrip_StartButton()
    {
        LinkAs("EXPEDITOR");
        _routes["MyTrip"] = _ => (HttpStatusCode.OK, TripJson.Replace("\"status\":1,\"statusName\":\"Yo'lda\"", "\"status\":0,\"statusName\":\"Rejalandi\""));
        _routes["TripStart"] = _ => (HttpStatusCode.OK, "{\"ok\":true}");
        await Handle(Text(2, "🚚 Reys"));
        Assert.Contains("ts:4", Out().Last().Payload);
        await Handle(Click(3, "ts:4"));
        Assert.Contains("\"tripId\":4", _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/TripStart")).Text);
        Assert.Contains(Out().Select(TextOf), t => t.Contains("Yo'lga chiqdingiz"));
    }

    [Fact]
    public async Task Voice_DisabledOrNoKey_Explained()
    {
        LinkAs("AGENT");
        _routes["DeliveryMe"] = _ => (HttpStatusCode.OK, "{\"isAgent\":true,\"voiceOrders\":false}");
        await Handle(VoiceMsg(2));
        Assert.Contains("o'chirilgan", TextOf(Out().Last()));
        _routes["DeliveryMe"] = _ => (HttpStatusCode.OK, "{\"isAgent\":true,\"voiceOrders\":true}");
        _voice.Enabled = false;
        await Handle(VoiceMsg(3));
        Assert.Contains("sozlanmagan", TextOf(Out().Last()));
        _voice.Enabled = true;
        await Handle(VoiceMsg(4, seconds: 300));
        Assert.Contains("90", TextOf(Out().Last()));
        Assert.Equal(0, _voice.Calls);
    }

    [Fact]
    public async Task Voice_DraftConfirm_OrderWithSameRequestId()
    {
        LinkAs("AGENT");
        _routes["DeliveryMe"] = _ => (HttpStatusCode.OK, "{\"isAgent\":true,\"voiceOrders\":true}");
        _routes["VoiceDraft"] = _ => (HttpStatusCode.OK, "{\"customerID\":5,\"customer\":\"Baraka\",\"customerOptions\":[],\"lines\":[" +
            "{\"text\":\"sement M400\",\"productID\":31,\"productUnitID\":7,\"product\":\"T1 — Sement M400\",\"unit\":\"qop\",\"qty\":20,\"price\":55000,\"amount\":1100000,\"available\":80,\"score\":100}]," +
            "\"unmatched\":[\"kafel — 5\"],\"total\":1100000}");
        await Handle(VoiceMsg(2));
        var draftReq = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/VoiceDraft")).Text).RootElement;
        Assert.Equal("Baraka", draftReq.GetProperty("customerHint").GetString());
        Assert.Equal(2, draftReq.GetProperty("items").GetArrayLength());
        Assert.Equal("qop", draftReq.GetProperty("items")[0].GetProperty("unit").GetString());
        var draft = Out().Last();
        Assert.Contains("T1 — Sement M400 — 20 qop × 55 000 = 1 100 000", TextOf(draft));
        Assert.Contains("Topilmadi: kafel — 5", TextOf(draft));
        Assert.Contains("vo!", draft.Payload);

        var calls = 0;
        _routes["Order"] = _ => ++calls == 1
            ? (HttpStatusCode.ServiceUnavailable, "")
            : (HttpStatusCode.OK, "{\"id\":9,\"docNo\":\"D-2026-00099\",\"total\":1100000}");
        await Handle(Click(3, "vo!"));
        await Handle(Click(4, "vo!"));
        var orders = _erp.Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/Order")).Select(r => JsonDocument.Parse(r.Text).RootElement).ToList();
        Assert.Equal(2, orders.Count);
        Assert.Equal(orders[0].GetProperty("clientRequestId").GetString(), orders[1].GetProperty("clientRequestId").GetString());
        Assert.Equal(5, orders[1].GetProperty("customerId").GetInt32());
        Assert.Equal(7, orders[1].GetProperty("lines")[0].GetProperty("productUnitId").GetInt32());
        Assert.Contains("D-2026-00099", TextOf(Out().Last()));
    }

    [Fact]
    public async Task Voice_AmbiguousCustomer_Choose()
    {
        LinkAs("AGENT");
        _voice.Transcript = "20 qop sement";
        _routes["DeliveryMe"] = _ => (HttpStatusCode.OK, "{\"isAgent\":true,\"voiceOrders\":true}");
        _routes["VoiceDraft"] = r => r.Text.Contains("\"customerId\":6")
            ? (HttpStatusCode.OK, "{\"customerID\":6,\"customer\":\"Anvar 2\",\"lines\":[{\"text\":\"sement\",\"productID\":31,\"product\":\"Sement\",\"qty\":20,\"price\":55000,\"amount\":1100000}],\"total\":1100000}")
            : r.Text.Contains("\"customerHint\":\"Anvar\"")
                ? (HttpStatusCode.OK, "{\"customerID\":null,\"customerOptions\":[{\"id\":5,\"name\":\"Anvar 1\"},{\"id\":6,\"name\":\"Anvar 2\"}],\"lines\":[],\"total\":0}")
                : (HttpStatusCode.OK, "{\"customerID\":null,\"customerOptions\":[],\"lines\":[],\"total\":0}");
        await Handle(VoiceMsg(2));
        Assert.Contains("Qaysi mijozga", TextOf(Out().Last()));
        await Handle(Text(3, "Anvar"));
        Assert.Contains("vc:6", Out().Last().Payload);
        await Handle(Click(4, "vc:6"));
        Assert.Contains("Mijoz: Anvar 2", TextOf(Out().Last()));
        Assert.Contains("vo!", Out().Last().Payload);
    }
}
