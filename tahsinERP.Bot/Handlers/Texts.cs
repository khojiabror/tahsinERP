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
        ["Error_License"] = ("⛔ Tizim litsenziyasi faol emas. Kompaniya administratoriga murojaat qiling.", "⛔ Лицензия системы неактивна. Обратитесь к администратору компании."),
        ["NotAllowed"] = ("Bu amal sizning rolingiz uchun emas.", "Это действие недоступно для вашей роли."),
        ["Cancelled"] = ("Bekor qilindi.", "Отменено."),
        ["Menu"] = ("Asosiy menyu.", "Главное меню."),
        ["Customer_Menu"] = ("Buyurtmalaringiz holati va to'lovlar haqida xabarlar shu yerga keladi. Qarzingiz va akt sverki — pastdagi tugmalarda.",
                             "Сюда приходят уведомления о заказах и оплатах. Долг и акт сверки — кнопками внизу."),
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
        ["Photo_Failed"] = ("Rasmni olib bo'lmadi, qayta yuboring.", "Не удалось получить фото, отправьте ещё раз."),

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

        ["Btn_NewPoint"] = ("➕ Yangi nuqta", "➕ Новая точка"),
        ["Btn_Issue"] = ("⚠️ Muammo", "⚠️ Проблема"),
        ["Issue_Ask"] = ("Muammoni yozing: nima qildingiz, nima bo'ldi (buyurtma/mijoz raqami bo'lsa — qo'shing). Skrinshotni izoh bilan yuborishingiz mumkin.",
                         "Опишите проблему: что делали и что произошло (номер заказа/клиента, если есть). Можно отправить скриншот с подписью."),
        ["Issue_TooShort"] = ("Biroz batafsilroq yozing (kamida 5 belgi) yoki rasm yuboring.", "Опишите подробнее (минимум 5 символов) или отправьте фото."),
        ["Issue_PhotoOnly"] = ("(rasm, izohsiz)", "(фото без подписи)"),
        ["Issue_Saved"] = ("✅ Qayd qilindi: {0}. Rahbarga xabar berildi, rahmat!", "✅ Зарегистрировано: {0}. Руководитель уведомлён, спасибо!"),
        ["Btn_Live"] = ("📡 Jonli joylashuv", "📡 Живая геолокация"),
        ["Btn_Skip"] = ("⏭ O'tkazib yuborish", "⏭ Пропустить"),
        ["Btn_Save"] = ("✅ Saqlash", "✅ Сохранить"),
        ["Btn_MyDebt"] = ("💳 Qarzim", "💳 Мой долг"),
        ["Btn_Statement"] = ("📄 Akt sverki", "📄 Акт сверки"),

        ["Day_AskSelfie"] = ("📸 Endi selfi yuboring (kamera bilan, hozir tushiring).", "📸 Теперь отправьте селфи (снимите камерой сейчас)."),
        ["Day_AskSelfieOptional"] = ("📸 Selfi yuboring yoki ⏭ O'tkazib yuborish tugmasini bosing.", "📸 Отправьте селфи или нажмите ⏭ Пропустить."),
        ["Day_SelfieRequired"] = ("Kunni boshlash uchun selfi kerak.", "Для начала дня нужно селфи."),
        ["Day_LiveHint"] = ("📡 Rahbaringiz joylashuvingizni ko'rishi uchun jonli joylashuvni yoqing (📡 Jonli joylashuv tugmasi).",
                            "📡 Включите живую геолокацию, чтобы руководитель видел ваше местоположение (кнопка 📡 Живая геолокация)."),
        ["Live_Howto"] = ("Jonli joylashuvni yoqish: 📎 → Joylashuv → «Jonli joylashuvni ulashish» → 8 soat.\nJoylashuv faqat ish kuni ochiq paytda yoziladi; kunni yakunlagach ulashishni to'xtating.",
                          "Как включить: 📎 → Геопозиция → «Транслировать геопозицию» → 8 часов.\nГеолокация пишется только пока рабочий день открыт; после завершения дня остановите трансляцию."),
        ["Live_Started"] = ("📡 Jonli joylashuv qabul qilindi. Ish kuni davomida rahbaringiz joylashuvingizni ko'radi.",
                            "📡 Живая геолокация принята. В течение рабочего дня руководитель видит ваше местоположение."),
        ["Live_NoDay"] = ("Avval ▶️ Kunni boshlang — jonli joylashuv faqat ish kuni ochiq paytda yoziladi.",
                          "Сначала ▶️ начните день — живая геолокация пишется только при открытом рабочем дне."),

        ["New_AskName"] = ("➕ Yangi savdo nuqtasi.\nNuqta (do'kon) nomini yozing:", "➕ Новая торговая точка.\nВведите название точки (магазина):"),
        ["New_BadName"] = ("Nom 2–50 belgi bo'lsin.", "Название — от 2 до 50 символов."),
        ["New_AskPhone"] = ("Telefon raqamini yozing (masalan 90 123 45 67) yoki kontaktni yuboring:", "Введите телефон (например 90 123 45 67) или отправьте контакт:"),
        ["New_BadPhone"] = ("Telefon noto'g'ri. 9 raqam yoki +998… ko'rinishida yozing.", "Неверный телефон. Введите 9 цифр или в виде +998…"),
        ["New_AskLocation"] = ("Nuqta oldida turib, joylashuvni yuboring:", "Находясь у точки, отправьте геолокацию:"),
        ["New_AskLandmark"] = ("Mo'ljal yoki manzilni yozing (masalan: «bozor yonida»):", "Введите ориентир или адрес (например: «возле рынка»):"),
        ["New_AskPhoto"] = ("Nuqtaning fotosini yuboring (vivyeska, peshtaxta):", "Отправьте фото точки (вывеска, витрина):"),
        ["New_Confirm"] = ("Tekshiring:\n🏪 {0}\n☎️ {1}\n📍 {2}\n🧭 {3}\n📷 {4}\nSaqlaysizmi?", "Проверьте:\n🏪 {0}\n☎️ {1}\n📍 {2}\n🧭 {3}\n📷 {4}\nСохранить?"),
        ["New_Saved"] = ("✅ Nuqta qo'shildi. Ofis tasdiqlagach, unga buyurtmalar tasdiqlanadi.", "✅ Точка добавлена. После подтверждения офисом заказы по ней будут подтверждаться."),
        ["New_Duplicate"] = ("Bu nuqta yaqinda qo'shilgan — o'shasi ochildi.", "Эта точка недавно добавлена — открыта она."),
        ["Word_Yes"] = ("bor", "есть"),
        ["Word_No"] = ("yo'q", "нет"),

        ["Debt_Info"] = ("💳 {0}\nQarz: {1} so'm\n  muddati kelmagan: {2}\n  muddati o'tgan: {3}{4}\nLimit: {5} so'm, to'lov muddati {6} kun",
                         "💳 {0}\nДолг: {1} сум\n  срок не наступил: {2}\n  просрочено: {3}{4}\nЛимит: {5} сум, срок оплаты {6} дн."),
        ["Debt_MaxDays"] = (" (eng ko'pi {0} kun)", " (до {0} дн.)"),
        ["Debt_None"] = ("💳 {0}\nQarzingiz yo'q. ✅", "💳 {0}\nДолга нет. ✅"),
        ["Debt_Advance"] = ("Avans (oldindan to'langan): {0} so'm", "Аванс (предоплата): {0} сум"),
        ["Debt_LastPayments"] = ("So'nggi to'lovlar:", "Последние оплаты:"),
        ["Debt_PaymentLine"] = ("• {0} — {1} so'm", "• {0} — {1} сум"),
        ["Stmt_AskPeriod"] = ("📄 Akt sverki davri:", "📄 Период акта сверки:"),
        ["Stmt_ThisMonth"] = ("Shu oy", "Этот месяц"),
        ["Stmt_LastMonth"] = ("O'tgan oy", "Прошлый месяц"),
        ["Stmt_3Months"] = ("Oxirgi 3 oy", "Последние 3 месяца"),
        ["Stmt_Head"] = ("📄 Akt sverki: {0}\n{1} — {2}\nBoshlang'ich qoldiq: {3} so'm", "📄 Акт сверки: {0}\n{1} — {2}\nНачальное сальдо: {3} сум"),
        ["Stmt_Empty"] = ("Davrda harakat yo'q.", "Движений за период нет."),
        ["Stmt_Foot"] = ("Jami: +{0} / −{1}\nYakuniy qoldiq: {2} so'm\n(+ qarz oshgan, − kamaygan)", "Итого: +{0} / −{1}\nКонечное сальдо: {2} сум\n(+ долг вырос, − уменьшился)"),
        ["Stmt_More"] = ("… yana {0} ta yozuv (to'liq akt — ofisdan).", "… ещё {0} записей (полный акт — в офисе)."),
        ["Customer_Only"] = ("Bu bo'lim mijozlar uchun.", "Этот раздел для клиентов."),

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
