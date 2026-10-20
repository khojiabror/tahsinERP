namespace tahsinERP.Bot;

/// <summary>"Bot" bo'limi. MasterKey — muhit o'zgaruvchisi Bot__MasterKey (ERP'dagi TAHSINERP_BOT_KEY bilan bir xil).</summary>
public sealed class BotOptions
{
    public string MasterKey { get; set; } = "";

    /// <summary>Tashqi manzil (https): webhook va Mini App havolalari shundan quriladi.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Fotosurat va imzolar papkasi (nisbiy bo'lsa — ContentRoot ichida).</summary>
    public string FilesRoot { get; set; } = "files";

    public int NotificationPollSeconds { get; set; } = 12;
    public int DailySummaryHour { get; set; } = 20;
    public string TimeZone { get; set; } = "Asia/Tashkent";

    /// <summary>Bitta bot tokeni uchun soniyasiga xabar (Telegram chegarasi ~30).</summary>
    public int GlobalPerSecond { get; set; } = 25;

    public int ErpTimeoutSeconds { get; set; } = 20;
}
