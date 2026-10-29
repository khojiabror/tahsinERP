namespace tahsinERP.Bot.Telegram;

/// <summary>Klaviatura obyektlari (Bot API JSON shaklida, anonim tiplar).</summary>
public static class Kb
{
    public static object Button(string text, string callbackData) => new { text, callback_data = callbackData };

    public static object WebApp(string text, string url) => new { text, web_app = new { url } };

    public static object Url(string text, string url) => new { text, url };

    public static object Inline(IEnumerable<IEnumerable<object>> rows) =>
        new { inline_keyboard = rows.Select(r => r.ToArray()).Where(r => r.Length > 0).ToArray() };

    public static object Inline(params object[][] rows) => Inline(rows.AsEnumerable());

    public static object Reply(IEnumerable<IEnumerable<object>> rows, bool oneTime = false) =>
        new { keyboard = rows.Select(r => r.ToArray()).ToArray(), resize_keyboard = true, one_time_keyboard = oneTime, is_persistent = !oneTime };

    public static object Text(string text) => new { text };

    public static object Location(string text) => new { text, request_location = true };

    /// <summary>O'z telefon raqamini yuborish tugmasi (Telegram kontakt — raqam foydalanuvchiniki ekani kafolatlangan).</summary>
    public static object Contact(string text) => new { text, request_contact = true };

    public static object Remove() => new { remove_keyboard = true };
}
