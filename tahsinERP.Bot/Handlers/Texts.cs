using System.Globalization;

namespace tahsinERP.Bot.Handlers;

/// <summary>Bot matnlari (uz/ru). ERP bildirishnomalari ERP'da tayyorlanadi; bu yerda — faqat bot interfeysi.</summary>
public static class Texts
{
    public const string Uz = "uz";
    public const string Ru = "ru";

    private static readonly Dictionary<string, (string Uz, string Ru)> Map = new()
    {
        ["Welcome_Unlinked"] = ("Assalomu alaykum! Botdan foydalanish uchun rahbaringizdan ulanish kodini oling va havolani oching yoki /start KOD yuboring.",
                                "Здравствуйте! Чтобы пользоваться ботом, получите у руководителя код привязки и откройте ссылку или отправьте /start КОД."),
        ["Linked"] = ("✅ {0}, siz ulandingiz. Rol: {1}.", "✅ {0}, вы подключены. Роль: {1}."),
        ["Unlinked"] = ("Bot hisobingizdan uzildi. Qayta ulash uchun yangi kod oling.", "Бот отвязан. Для повторной привязки получите новый код."),
        ["Error_Generic"] = ("⚠️ Xatolik yuz berdi, birozdan keyin qayta urinib ko'ring.", "⚠️ Произошла ошибка, попробуйте чуть позже."),
        ["Error_Erp"] = ("⚠️ ERP bilan aloqa yo'q, birozdan keyin qayta urinib ko'ring.", "⚠️ Нет связи с ERP, попробуйте чуть позже."),
        ["NotAllowed"] = ("Bu amal sizning rolingiz uchun emas.", "Это действие недоступно для вашей роли."),
        ["Cancelled"] = ("Bekor qilindi.", "Отменено."),
        ["Menu"] = ("Asosiy menyu.", "Главное меню."),
        ["Customer_Menu"] = ("Buyurtmalaringiz holati va to'lovlar haqida xabarlar shu yerga keladi.", "Сюда приходят уведомления о заказах и оплатах."),
        ["Staff_Menu"] = ("Bildirishnomalar shu yerga keladi.", "Сюда приходят уведомления."),

        ["Btn_DayStart"] = ("▶️ Kunni boshlash", "▶️ Начать день"),
        ["Btn_Route"] = ("🗺 Marshrut", "🗺 Маршрут"),
        ["Btn_Search"] = ("🔎 Mijoz", "🔎 Клиент"),
        ["Btn_Cash"] = ("💵 Kassa", "💵 Касса"),
        ["Btn_DayEnd"] = ("🏁 Kunni yakunlash", "🏁 Завершить день"),
        ["Btn_Summary"] = ("📊 Bugungi yakun", "📊 Итоги дня"),
        ["Btn_Cancel"] = ("✖️ Bekor qilish", "✖️ Отмена"),
        ["Btn_SendLocation"] = ("📍 Joylashuvni yuborish", "📍 Отправить геолокацию"),
        ["Btn_CheckIn"] = ("📍 Keldim", "📍 Я на месте"),
        ["Btn_Order"] = ("🛒 Buyurtma", "🛒 Заказ"),
        ["Btn_Payment"] = ("💵 To'lov", "💵 Оплата"),
        ["Btn_NoOrder"] = ("🚫 Buyurtmasiz", "🚫 Без заказа"),
        ["Btn_Photo"] = ("📷 Foto", "📷 Фото"),
        ["Btn_Map"] = ("🧭 Xarita", "🧭 Карта"),
        ["Btn_Back"] = ("⬅️ Orqaga", "⬅️ Назад"),
        ["Btn_Handover"] = ("📤 Kassaga topshirish", "📤 Сдать в кассу"),
        ["Btn_Confirm"] = ("✅ Tasdiqlash", "✅ Подтвердить"),
        ["Btn_Refresh"] = ("🔄 Yangilash", "🔄 Обновить"),

        ["Day_AskLocation"] = ("Ish kunini boshlash uchun joylashuvingizni yuboring.", "Чтобы начать день, отправьте геолокацию."),
        ["Day_Started"] = ("✅ Ish kuni boshlandi ({0}). Omad!", "✅ Рабочий день начат ({0}). Удачи!"),
        ["Day_AskEndLocation"] = ("Kunni yakunlash uchun joylashuvingizni yuboring.", "Чтобы завершить день, отправьте геолокацию."),
        ["Day_Summary"] = ("🏁 Kun yakuni\nTashriflar: {0}/{1} (buyurtmasiz: {2})\nBuyurtmalar: {3} ta, {4} so'm\nYig'ilgan: {5} so'm\nQo'lingizda naqd: {6} so'm",
                           "🏁 Итоги дня\nВизиты: {0}/{1} (без заказа: {2})\nЗаказы: {3} шт., {4} сум\nСобрано: {5} сум\nНаличные на руках: {6} сум"),
        ["Day_NotAgent"] = ("Siz agent yoki ekspeditor sifatida ro'yxatdan o'tmagansiz.", "Вы не зарегистрированы как агент или экспедитор."),

        ["Route_Title"] = ("🗺 Bugungi marshrut: {0} ta mijoz, {1} tasiga borildi.", "🗺 Маршрут на сегодня: {0} клиентов, посещено {1}."),
        ["Route_Empty"] = ("Bugun uchun marshrutda mijoz yo'q. 🔎 Mijoz tugmasi bilan qidiring.", "На сегодня в маршруте нет клиентов. Найдите через 🔎 Клиент."),
        ["Search_Ask"] = ("Mijoz nomi, telefoni yoki STIR'ini yozing.", "Введите название, телефон или ИНН клиента."),
        ["Search_None"] = ("Hech narsa topilmadi.", "Ничего не найдено."),
        ["Search_Found"] = ("Topildi: {0}", "Найдено: {0}"),

        ["Card"] = ("🏪 {0}\n📍 {1}\n☎️ {2}\nNarx turi: {3}\nQarz: {4} so'm (muddati o'tgan: {5})\nLimit: {6} so'm, muddat: {7} kun",
                    "🏪 {0}\n📍 {1}\n☎️ {2}\nТип цены: {3}\nДолг: {4} сум (просрочено: {5})\nЛимит: {6} сум, срок: {7} дн."),
        ["Card_NotApproved"] = ("⚠️ Mijoz hali tasdiqlanmagan.", "⚠️ Клиент ещё не подтверждён."),
        ["Card_Recommended"] = ("💡 Tavsiya:", "💡 Рекомендация:"),
        ["Card_RecommendedLine"] = ("• {0} — ~{1} ({2} kun oldin){3}", "• {0} — ~{1} ({2} дн. назад){3}"),
        ["Card_LongAgo"] = (" ⏰", " ⏰"),
        ["Card_Visit"] = ("🟢 Tashrif ochiq.", "🟢 Визит открыт."),

        ["CheckIn_Ask"] = ("Mijoz oldidaligingizni tasdiqlash uchun joylashuvni yuboring.", "Отправьте геолокацию, чтобы подтвердить, что вы у клиента."),
        ["CheckIn_Ok"] = ("✅ Tashrif boshlandi{0}.", "✅ Визит начат{0}."),
        ["CheckIn_Distance"] = (" ({0} m)", " ({0} м)"),
        ["CheckIn_First"] = ("Avval 📍 Keldim tugmasi bilan tashrifni boshlang.", "Сначала начните визит кнопкой 📍 Я на месте."),

        ["NoOrder_Ask"] = ("Buyurtma bo'lmagan sababni tanlang:", "Выберите причину отсутствия заказа:"),
        ["NoOrder_Saved"] = ("✅ Saqlandi: {0}.", "✅ Сохранено: {0}."),
        ["Photo_Ask"] = ("Rasmni yuboring (javon, vitrina).", "Отправьте фото (полка, витрина)."),
        ["Photo_Saved"] = ("✅ Rasm saqlandi.", "✅ Фото сохранено."),
        ["Photo_TooBig"] = ("Rasm juda katta.", "Фото слишком большое."),

        ["Pay_AskAmount"] = ("💵 {0}\nQarz: {1} so'm. Olingan summani yozing:", "💵 {0}\nДолг: {1} сум. Введите полученную сумму:"),
        ["Pay_BadAmount"] = ("Summani raqam bilan yozing, masalan 250000.", "Введите сумму числом, например 250000."),
        ["Pay_AskMethod"] = ("{0} so'm. To'lov usuli:", "{0} сум. Способ оплаты:"),
        ["Pay_Done"] = ("✅ To'lov {0} qabul qilindi: {1} so'm.\nQo'lingizda naqd: {2} so'm.", "✅ Оплата {0} принята: {1} сум.\nНаличные на руках: {2} сум."),

        ["Cash_Info"] = ("💵 Qo'lingizdagi naqd: {0} so'm.\nBugun yig'ilgan: {1} so'm.", "💵 Наличные на руках: {0} сум.\nСобрано сегодня: {1} сум."),
        ["Cash_ConfirmHandover"] = ("{0} so'mni kassaga topshirasizmi? Kassir sanab qabul qiladi.", "Сдать {0} сум в кассу? Кассир пересчитает и примет."),
        ["Cash_HandedOver"] = ("✅ Topshirish {0}: {1} so'm. Kassir qabul qilishini kuting.", "✅ Сдача {0}: {1} сум. Ожидайте приёма кассиром."),

        ["Override_Done"] = ("✅ Buyurtma {0} tasdiqlandi.", "✅ Заказ {0} подтверждён."),
        ["Summary_None"] = ("Yakun hozircha tayyor emas.", "Итоги пока не готовы."),
        ["Order_Accepted"] = ("✅ Buyurtma {0} qabul qilindi: {1} so'm. Ofis tasdiqlaydi.", "✅ Заказ {0} принят: {1} сум. Офис подтвердит."),
        ["Order_MiniApp"] = ("Buyurtma oynasini oching:", "Откройте окно заказа:"),

        ["Role_AGENT"] = ("agent", "агент"),
        ["Role_EXPEDITOR"] = ("ekspeditor", "экспедитор"),
        ["Role_MANAGER"] = ("rahbar", "руководитель"),
        ["Role_STOREKEEPER"] = ("omborchi", "кладовщик"),
        ["Role_CUSTOMER"] = ("mijoz", "клиент"),

        ["PayMethod_CASH"] = ("Naqd", "Наличные"),
        ["PayMethod_CARD"] = ("Karta", "Карта"),
        ["PayMethod_CLICK"] = ("Click", "Click"),
        ["PayMethod_PAYME"] = ("Payme", "Payme"),
        ["PayMethod_UZUM"] = ("Uzum", "Uzum"),
        ["PayMethod_TRANSFER"] = ("O'tkazma", "Перечисление"),
    };

    public static readonly string[] PayMethods = ["CASH", "CARD", "CLICK", "PAYME", "UZUM", "TRANSFER"];

    public static string Get(string? lang, string key, params object?[] args)
    {
        if (!Map.TryGetValue(key, out var t)) return key;
        var s = lang == Ru ? t.Ru : t.Uz;
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }

    /// <summary>Tugma matni qaysi kalitga tegishli (ikkala tilda ham taniladi).</summary>
    public static string? KeyOf(string text, params string[] keys) =>
        keys.FirstOrDefault(k => Map.TryGetValue(k, out var t) && (t.Uz == text || t.Ru == text));

    public static string Money(decimal v) => v.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

    public static string Qty(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    internal static IEnumerable<string> Keys => Map.Keys;
}
