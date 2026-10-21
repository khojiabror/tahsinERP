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
