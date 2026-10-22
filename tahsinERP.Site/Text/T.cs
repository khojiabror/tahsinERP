using System.Globalization;
using tahsinERP.Localization;

namespace tahsinERP.Site.Text;

/// <summary>
/// Sayt interfeysi matnlari (uz/ru; "oz" — o'zbekchaning kirill transliteratsiyasi). Katalog ma'lumoti (nomlar,
/// tavsiflar) — ERP'dan, <see cref="Pick"/> orqali.
/// </summary>
public static class T
{
    private static readonly Dictionary<string, (string Uz, string Ru)> Map = new()
    {
        ["Catalog"] = ("Katalog", "Каталог"),
        ["AllProducts"] = ("Barcha tovarlar", "Все товары"),
        ["Categories"] = ("Kategoriyalar", "Категории"),
        ["Search"] = ("Qidirish", "Поиск"),
        ["SearchPlaceholder"] = ("Tovar nomi, artikul yoki shtrix-kod", "Название, артикул или штрих-код"),
        ["SearchResults"] = ("\"{0}\" bo'yicha natijalar", "Результаты по «{0}»"),
        ["NothingFound"] = ("Hech narsa topilmadi. So'zni qisqartirib yoki boshqacha yozib ko'ring.", "Ничего не найдено. Попробуйте сократить запрос или написать иначе."),
        ["Cart"] = ("Savat", "Корзина"),
        ["CartEmpty"] = ("Savat bo'sh.", "Корзина пуста."),
        ["ToCatalog"] = ("Katalogga o'tish", "Перейти в каталог"),
        ["AddToCart"] = ("Savatga", "В корзину"),
        ["Added"] = ("Savatga qo'shildi", "Добавлено в корзину"),
        ["InCart"] = ("Savatda", "В корзине"),
        ["Remove"] = ("O'chirish", "Удалить"),
        ["Unit"] = ("Birlik", "Единица"),
        ["Qty"] = ("Miqdor", "Количество"),
        ["Price"] = ("Narx", "Цена"),
        ["PriceAsk"] = ("Narxni so'rang", "Цену уточняйте"),
        ["Total"] = ("Jami", "Итого"),
        ["TotalApprox"] = ("Taxminiy summa", "Ориентировочная сумма"),
        ["PriceNote"] = ("Yakuniy narx va yetkazib berish menejer bilan kelishiladi.", "Окончательная цена и доставка согласуются с менеджером."),
        ["StockIn"] = ("Mavjud", "В наличии"),
        ["StockLow"] = ("Kam qoldi", "Осталось мало"),
        ["StockOut"] = ("Buyurtmaga", "Под заказ"),
        ["InStockOnly"] = ("Faqat mavjudlari", "Только в наличии"),
        ["Sku"] = ("Artikul", "Артикул"),
        ["Brand"] = ("Brend", "Бренд"),
        ["Specs"] = ("Xususiyatlar", "Характеристики"),
        ["Description"] = ("Tavsif", "Описание"),
        ["Related"] = ("O'xshash tovarlar", "Похожие товары"),
        ["Filters"] = ("Filtrlar", "Фильтры"),
        ["Reset"] = ("Tozalash", "Сбросить"),
        ["Apply"] = ("Ko'rsatish", "Показать"),
        ["SortBy"] = ("Saralash", "Сортировка"),
        ["Sort_default"] = ("Tavsiya", "Рекомендуемые"),
        ["Sort_price-asc"] = ("Arzonroq", "Сначала дешевле"),
        ["Sort_price-desc"] = ("Qimmatroq", "Сначала дороже"),
        ["Sort_name"] = ("Nomi bo'yicha", "По названию"),
        ["Sort_new"] = ("Yangilari", "Новинки"),
        ["Products"] = ("{0} ta tovar", "{0} товаров"),
        ["Prev"] = ("Oldingi", "Назад"),
        ["Next"] = ("Keyingi", "Далее"),
        ["Page"] = ("{0}-sahifa", "Страница {0}"),
        ["Grid"] = ("Setka", "Плитка"),
        ["List"] = ("Ro'yxat", "Список"),
        ["Badge_NEW"] = ("Yangi", "Новинка"),
        ["Badge_SALE"] = ("Aksiya", "Акция"),
        ["Badge_HIT"] = ("Xit", "Хит"),
        ["Home"] = ("Bosh sahifa", "Главная"),
        ["PopularCategories"] = ("Kategoriyalar", "Категории"),
        ["NewArrivals"] = ("Yangi tovarlar", "Новинки"),
        ["OnSale"] = ("Aksiyalar", "Акции"),
        ["Featured"] = ("Tavsiya etamiz", "Рекомендуем"),
        ["ViewAll"] = ("Hammasi", "Все"),
        ["Contacts"] = ("Kontaktlar", "Контакты"),
        ["Address"] = ("Manzil", "Адрес"),
        ["WorkingHours"] = ("Ish vaqti", "Время работы"),
        ["OpenMap"] = ("Xaritada ochish", "Открыть на карте"),
        ["Info"] = ("Ma'lumot", "Информация"),
        ["Menu"] = ("Menyu", "Меню"),
        ["Close"] = ("Yopish", "Закрыть"),
        ["Language"] = ("Til", "Язык"),

        ["Checkout"] = ("Ariza yuborish", "Отправить заявку"),
        ["CheckoutTitle"] = ("Ariza", "Заявка"),
        ["CheckoutHint"] = ("Ma'lumotlaringizni qoldiring — menejer siz bilan bog'lanib, narx, mavjudlik va yetkazib berishni tasdiqlaydi.", "Оставьте контакты — менеджер свяжется с вами и подтвердит цену, наличие и доставку."),
        ["YourName"] = ("Ismingiz", "Ваше имя"),
        ["Phone"] = ("Telefon", "Телефон"),
        ["Company"] = ("Tashkilot (ixtiyoriy)", "Организация (необязательно)"),
        ["Inn"] = ("STIR (ixtiyoriy)", "ИНН (необязательно)"),
        ["Comment"] = ("Izoh (ixtiyoriy)", "Комментарий (необязательно)"),
        ["Send"] = ("Yuborish", "Отправить"),
        ["AskQuestion"] = ("Savol berish", "Задать вопрос"),
        ["RequestPrice"] = ("Narxni so'rash", "Запросить цену"),
        ["ThanksTitle"] = ("Rahmat! Arizangiz qabul qilindi", "Спасибо! Заявка принята"),
        ["ThanksText"] = ("Menejer tez orada {0} raqamiga qo'ng'iroq qiladi.", "Менеджер скоро позвонит на номер {0}."),
        ["LeadsDisabled"] = ("Hozircha saytdan ariza qabul qilinmaydi — telefon orqali bog'laning.", "Сейчас заявки через сайт не принимаются — свяжитесь по телефону."),
        ["Err_Name"] = ("Ismingizni yozing.", "Укажите имя."),
        ["Err_Phone"] = ("Telefon raqamini to'g'ri yozing, masalan +998 90 123 45 67.", "Укажите телефон, например +998 90 123 45 67."),
        ["Err_TooMany"] = ("Juda ko'p urinish. Birozdan keyin qayta yuboring yoki qo'ng'iroq qiling.", "Слишком много попыток. Повторите позже или позвоните."),
        ["Err_Cart"] = ("Savatdagi tovarlar o'zgargan — sahifani yangilang.", "Товары в корзине изменились — обновите страницу."),

        ["NotFoundTitle"] = ("Sahifa topilmadi", "Страница не найдена"),
        ["NotFoundText"] = ("Bu sahifa o'chirilgan yoki manzil noto'g'ri.", "Страница удалена или адрес неверный."),
        ["SoonTitle"] = ("Sayt tez orada ochiladi", "Сайт скоро откроется"),
        ["SoonText"] = ("Hozircha telefon orqali bog'laning.", "Пока свяжитесь с нами по телефону."),
        ["Rights"] = ("Barcha huquqlar himoyalangan.", "Все права защищены."),
        ["PoweredBy"] = ("tahsinERP asosida", "на базе tahsinERP"),
    };

    public static string Get(string lang, string key, params object?[] args)
    {
        if (!Map.TryGetValue(key, out var t)) return key;
        var s = lang == "ru" ? t.Ru : lang == "oz" ? UzTransliterator.ToCyrillicCached(t.Uz) : t.Uz;
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }

    internal static IEnumerable<string> Keys => Map.Keys;

    /// <summary>Saytda qo'llab-quvvatlanadigan til kodlari (URL'ning birinchi qismi: /uz/, /oz/, /ru/).</summary>
    public static bool IsLanguage(string? lang) => lang is "uz" or "oz" or "ru";

    /// <summary>Ikki tilli ERP ma'lumoti: ru — ruschasi (bo'lmasa o'zbekcha), oz — o'zbekchaning kirillchasi.</summary>
    public static string? Pick(string lang, string? uz, string? ru) => lang switch
    {
        "ru" => string.IsNullOrWhiteSpace(ru) ? uz : ru,
        "oz" => uz == null ? null : UzTransliterator.ToCyrillicCached(uz),
        _ => uz
    };

    /// <summary>hreflang / html lang qiymati.</summary>
    public static string HrefLang(string lang) => lang == "oz" ? "uz-Cyrl" : lang;

    /// <summary>Til tanlagichidagi yozuv.</summary>
    public static string LangLabel(string lang) => lang switch { "oz" => "ЎЗ", "ru" => "RU", _ => "UZ" };
}
