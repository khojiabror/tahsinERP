# -*- coding: utf-8 -*-
# 05_FINDINGS.md tuzatishlari (2026-10-21)
STRINGS = {
    "Credit_LimitExceeded": ("Qarz + jo'natilmagan buyurtmalar + shu buyurtma {0} — limit {1} dan oshadi.",
                             "Долг + неотгруженные заказы + этот заказ {0} — превышает лимит {1}."),
    "Credit_Committed": ("(shundan tasdiqlangan, hali jo'natilmagan buyurtmalar: {0})",
                         "(из них подтверждённые, ещё не отгруженные заказы: {0})"),
    "Credit_CommittedShort": ("Tasdiqlangan, jo'natilmagan", "Подтверждено, не отгружено"),
    "Cash_AfterHandover": ("topshirishdan keyin olingan: {0} (keyingi topshirishga)", "получено после сдачи: {0} (в следующую сдачу)"),
    "Cash_AfterHandoverHint": ("Agent topshirgandan keyin yana {0} naqd olgan — u bu hujjatga kirmaydi va agent qo'lida qoladi.",
                               "После сдачи агент получил ещё {0} наличными — они не входят в этот документ и остаются у агента."),
    "Cash_CancelHandover": ("Bekor qilish", "Отменить"),
    "Cash_CancelHandoverHint": ("Qabul bekor qilinadi: kassaga yozilgan to'lovlar va farq moliyadan olib tashlanadi, to'lovlar yana agent qo'lida bo'ladi.",
                                "Приём отменяется: оплаты и разница удаляются из финансов, оплаты снова числятся у агента."),
    "Cash_DifferenceHint": ("Kamomad kassada chiqim sifatida yoziladi; agent KPI sxemasida HR xodimiga bog'langan bo'lsa — xodimga berilgan pul (ish haqidan ushlanadi). Ortiqcha — kirim.",
                            "Недостача отражается в кассе расходом; если агент привязан к сотруднику HR в схеме KPI — как выдача сотруднику (удерживается из зарплаты). Излишек — приходом."),
    "Payment_AlreadyHandedOver": ("Bu to'lov kassaga topshirilgan. Avval Agentlar naqdi va kassa sahifasida topshirishni bekor qiling.",
                                  "Эта оплата сдана в кассу. Сначала отмените сдачу на странице «Наличные агентов и касса»."),
}
STRINGS.update({
    "Inventory_BlindHint": ("\"ko'r\" sanoq: tizim qoldig'i va farqlar sanoq o'tkazilgandan keyin ko'rsatiladi.",
                            "«слепой» пересчёт: учётный остаток и расхождения показываются после проведения."),
})
STRINGS.update({
    "Notify_TransferDiffQty": ("📦 Ko'chirma farqi {0} ({1}): {2} → {3}\nKamomad: {4} qator, ortiqcha: {5} qator",
                               "📦 Расхождение перемещения {0} ({1}): {2} → {3}\nНедостача: {4} строк, излишек: {5} строк"),
})
STRINGS.update({
    "Inventory_CountingBlocks": ("{0} inventarizatsiyada sanalgan ({1}) — sanoq o'tkazilmaguncha yoki bekor qilinmaguncha ombordan chiqarilmaydi.",
                                 "{0} уже посчитан в инвентаризации ({1}) — списание со склада невозможно до проведения или отмены пересчёта."),
})
STRINGS.update({
    "Trip_ResetNote": ("yetkazish natijasi bekor qilindi", "результат доставки отменён"),
    "Notify_StopReset": ("↺ {0} buyurtma bo'yicha yetkazish natijasi bekor qilindi va qayta kiritiladi. Oldingi xabarni hisobga olmang.",
                         "↺ Результат доставки по заказу {0} отменён и будет внесён заново. Предыдущее сообщение не учитывайте."),
    "Notify_TripFinished": ("🏁 Reys {0} yakunlandi ({1}). Qaytadigan tovar: {2} so'm — omborda sanab, Reyslar sahifasida yoping (qaytgan miqdor va kamomad).",
                            "🏁 Рейс {0} завершён ({1}). Возврат товара на {2} сум — пересчитайте на складе и закройте рейс на странице «Рейсы» (возврат и недостача)."),
})
STRINGS.update({
    "Trip_Redeliver": ("Qayta yetkazish", "Доставить повторно"),
    "Trip_RedeliverHint": ("Qaytgan tovar bilan yangi buyurtma qoralamasi yaratiladi (joriy narxda) — tasdiqlab, keyingi reysga qo'shing.",
                           "Будет создан черновик нового заказа с возвращённым товаром (по текущей цене) — подтвердите и добавьте в следующий рейс."),
    "Trip_RedeliverNothing": ("Qayta yetkazish uchun qaytgan tovar yo'q (reys yopilmagan yoki qaytarish hujjati yo'q).",
                              "Нет возвращённого товара для повторной доставки (рейс не закрыт или нет документа возврата)."),
    "Trip_RedeliverComment": ("Qayta yetkazish: {0} ({1})", "Повторная доставка: {0} ({1})"),
})
STRINGS.update({
    "Order_StopPicking": ("Yig'ishni to'xtatish", "Остановить сборку"),
})
STRINGS.update({
    "Opening_Reverse": ("Kirish qoldig'ini bekor qilish", "Отменить входящий остаток"),
    "Opening_ReverseConfirm": ("Kirish qoldig'i teskari yozuv bilan bekor qilinadi. Keyin to'g'ri summani Import → Kirish qoldig'i orqali qayta kiriting. Davom etilsinmi?",
                               "Входящий остаток будет сторнирован. Затем внесите правильную сумму через Импорт → Входящий остаток. Продолжить?"),
    "Opening_Reversed": ("Kirish qoldig'i bekor qilindi — to'g'ri summani qayta import qilishingiz mumkin.", "Входящий остаток отменён — можно импортировать правильную сумму."),
    "Opening_NothingToReverse": ("Bu mijozda bekor qilinadigan kirish qoldig'i yo'q.", "У клиента нет входящего остатка для отмены."),
})
STRINGS.update({
    # K2-18: "Tushum" — faqat jo'natish − qaytarish (panel, hisobotlar); kunlik xulosada — yig'ilgan pul
    "Summary_Text": ("Kun yakuni {0}\nBuyurtmalar: {1} ta, {2} so'm\nJo'natildi: {3} so'm\nYig'ilgan pul (to'lovlar): {4} so'm\nUmumiy qarz: {5} so'm (muddati o'tgan: {6})\nFaol agentlar: {7}/{8}",
                     "Итоги дня {0}\nЗаказы: {1} шт., {2} сум\nОтгружено: {3} сум\nСобрано денег (оплаты): {4} сум\nОбщий долг: {5} сум (просрочено: {6})\nАктивные агенты: {7}/{8}"),
})
STRINGS.update({
    "Dash_CurrentState": ("hozirgi holat", "текущее состояние"),
    "Dash_CurrentStateHint": ("Bu ko'rsatkich tanlangan sanaga emas, hozirgi holatga (o'tgan kun uchun tiklab bo'lmaydi).",
                              "Этот показатель отражает текущее состояние, а не выбранную дату."),
})
STRINGS.update({
    "Transfer_SPLIT": ("Bo'lish (yo'qotish + qaytarish)", "Разделить (потеря + возврат)"),
    "Transfer_LossQty": ("yo'qotish miqdori", "количество потери"),
    "Transfer_SplitQty": ("{0}: yo'qotish miqdori 0 dan katta va kamomad ({1}) dan kichik bo'lsin (qolgani qaytariladi).",
                          "{0}: количество потери должно быть больше 0 и меньше недостачи ({1}) (остаток возвращается)."),
    "Transfer_CancelAct": ("Dalolatnomani bekor qilish", "Отменить акт"),
    "Transfer_CancelHint": ("Qabul va qaytarish kirimi bekor qilinadi, ko'chirma yana qabul kutadi (tovar qabul qiluvchi ombordan chiqarilgan bo'lsa — bo'lmaydi).",
                            "Приёмка и приход возврата отменяются, перемещение снова ждёт приёмки (невозможно, если товар уже списан со склада-получателя)."),
    "Transfer_Cancelled": ("Dalolatnoma bekor qilindi — ko'chirmani qayta qabul qiling.", "Акт отменён — примите перемещение заново."),
    "Transfer_CancelConsumed": ("Bekor qilib bo'lmaydi: qabul qilingan tovardan chiqim qilingan.", "Нельзя отменить: по принятому товару уже был расход."),
})
STRINGS.update({
    "Report_LandedOnSold": ("Qo'shimcha xarajat — sotilgan tovarga tushgan qismi (davr xarajati)", "Доп. расходы — часть, пришедшаяся на проданный товар (расход периода)"),
    "Report_NetProfit": ("Foyda (qo'shimcha xarajatdan keyin)", "Прибыль (после доп. расходов)"),
})
STRINGS.update({
    "Bot_LiveRequired": ("\"Keldim\" uchun jonli joylashuv kerak: 📎 → Joylashuv → \"Jonli joylashuvni ulashish\".",
                         "Для «Я на месте» нужна трансляция геопозиции: 📎 → Геопозиция → «Транслировать геопозицию»."),
    "Settings_CheckInLiveOnly": ("\"Keldim\" — faqat jonli joylashuv", "«Я на месте» — только трансляция геопозиции"),
    "Settings_CheckInLiveOnlyHint": ("Yoqilsa, statik joylashuv (tugma bilan yuborilgan) rad etiladi — agent jonli joylashuvni ulashishi kerak. O'chiq — qabul qilinadi va hisobotda belgilanadi.",
                                     "Если включено, статичная геопозиция отклоняется — агент должен транслировать геопозицию. Выключено — принимается и отмечается в отчёте."),
})
# ishlatilmaydigan kalitlar (K3-30, K3-37) — resx'dan o'chiriladi (remove_keys.py)
REMOVE = ["Settings_AgentAccount", "Warehouse_AllowNegative", "Warehouse_Zone", "Product_Bulky", "Product_Weighted"]
STRINGS.update({
    "Audit": ("Audit jurnali", "Журнал аудита"),
    "Tile_Audit": ("Kim, qachon, nimani o'zgartirgan", "Кто, когда и что изменил"),
    "Audit_Hint": ("Distributsiya yozuvlaridagi har bir o'zgarish: yaratish (to'liq qiymat), tahrir (maydon bo'yicha eski → yangi), o'chirish. Faqat o'qish.",
                   "Каждое изменение записей дистрибуции: создание (полное значение), правка (по полям: старое → новое), удаление. Только чтение."),
    "Audit_Entity": ("Obyekt", "Объект"),
    "Audit_Key": ("Kalit (ID)", "Ключ (ID)"),
    "Audit_Action": ("Amal", "Действие"),
    "Audit_Field": ("Maydon", "Поле"),
    "Audit_Old": ("Eski qiymat", "Старое значение"),
    "Audit_New": ("Yangi qiymat", "Новое значение"),
    "Audit_Source": ("Manba", "Источник"),
    "Audit_I": ("yaratildi", "создано"),
    "Audit_U": ("o'zgartirildi", "изменено"),
    "Audit_D": ("o'chirildi", "удалено"),
})
STRINGS.update({
    "Settings_DefectWarehouse": ("Brak ombori", "Склад брака"),
    "Settings_DefectWarehouseHint": ("\"Brak\" belgili qaytarish faqat shu omborga kiradi; bu ombor buyurtmalarga taklif qilinmaydi.",
                                     "Возврат с отметкой «брак» принимается только на этот склад; склад не предлагается для заказов."),
    "Return_DefectWarehouse": ("Brak tovar brak omboriga qaytariladi: {0}.", "Бракованный товар возвращается на склад брака: {0}."),
})
STRINGS.update({
    "File_ImageOnly": ("Faqat JPEG yoki PNG rasm, 5 MB gacha.", "Только изображение JPEG или PNG, до 5 МБ."),
    "WriteOff_AttachPhoto": ("Foto biriktirish", "Прикрепить фото"),
    "WriteOff_ReplacePhoto": ("Fotoni almashtirish", "Заменить фото"),
})
STRINGS.update({
    "Voice_AgentOnly": ("Ovozli buyurtma faqat agentlar uchun.", "Голосовой заказ доступен только агентам."),
})
