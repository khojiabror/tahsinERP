# 5. Ishlab chiqarish (MES)

MES moduli iPack sexining kundalik ishini boshqaradi. Sotuv buyurtmasi asosida ishlab chiqarish rejasi tuziladi va kunlarga bo'linadi. Har kuni smena bo'yicha fakt kiritiladi. Fakt kiritilganda tizim normadagi xomashyoni Ishlab chiqarish omboridan o'zi chiqim qiladi va tayyor mahsulotni kirim qiladi. Keyin sifat nazorati o'tkaziladi, sifatli mahsulot Savdo omboriga o'tkaziladi.
Asosiy foydalanuvchilar: **ishlab chiqarish boshlig'i** ("Texnolog+Ishlab chiqarish", "Savdo+Ishlab chiqarish" rollari), **Texnolog** (sifat nazorati), **Omborchi** (xomashyoni sexga berish, tayyor mahsulotni qabul qilish), rahbariyat (dashboard).
Bog'liq modullar: Muhandislik (norma va BOM, 2-bob), Ombor (qoldiq, kirim, chiqim, o'tkazma), Sotuv (buyurtma, yetkazish jadvali, invoys), Tannarx (fakt material — ishlab chiqarish chiqimidan), HR (ishbay ish haqi).

## 5.1 Asosiy tushunchalar

- **Ishlab chiqarish rejasi** — bitta mahsulot uchun muddat (boshlanish–tugash) va jami miqdor. Masalan, iPack P1: 400 dona, 02.11–09.11.2026.
- **Kunlik reja** — reja miqdori kunlarga teng bo'lib taqsimlanadi (yuqoriga yaxlitlanadi) va "Kunduzgi smena" ga yoziladi. Kerak bo'lsa qo'lda kunduzgi/tungi smenaga bo'linadi.
- **Reja sanasi va fakt sanasi** — reja sanasi: ishlab chiqarish qaysi reja kuniga tegishli. Fakt sanasi: amalda qachon ishlab chiqarilgan. Kechikib bajarilgan ish reja kuniga yoziladi.
- **Kunlik ishlab chiqarish (fakt)** — kun va sex bo'yicha har mahsulotning kunduzgi va tungi smena natijasi.
- **Ishlab chiqarish ombori** — sex ombori (omborlar sozlamasida turi "Ishlab chiqarish" bo'lgan ombor; iPack'da "Ishlab chiqarish ombori"). Xomashyo shu yerdan chiqim qilinadi, tayyor mahsulot shu yerga kirim qilinadi.
- **Lot** — bitta ishlab chiqarish yozuvi (masalan, 03.11 dagi 55 dona P1). Sotuvga o'tkazmada lot tanlanadi.
- **Sifat nazorati** — ishlab chiqarilgan miqdordan qancha qabul qilingani va qancha rad etilgani (brak), nuqson turi bilan.
- **Brak (shikastlangan mahsulot)** — rad etilgan mahsulotlar ro'yxati: mahsulot, nuqson turi, miqdor.
- **FTQ % (First Time Quality)** — birinchi tekshiruvda qabul qilingan ulush.

**iPack'da tovar oqimi:**
Xomashyo ombori → (ichki o'tkazma) → Ishlab chiqarish ombori → [kunlik ishlab chiqarish: xomashyo chiqimi, tayyor mahsulot kirimi] → sifat nazorati → (Ishlab chiqarishdan sotuvga o'tkazma) → Savdo ombori → sotuv invoysi.

## 5.2 Boshlashdan oldin: sozlamalar

| Nima | Qayerda | Nima uchun kerak |
|---|---|---|
| "Ishlab chiqarish ombori" turi "Ishlab chiqarish", "Savdo ombori" turi "Savdo" | Ombor sozlamalari (Ombor bobi) | Ombor topilmasa ishlab chiqarish saqlanmaydi: "Ishlab chiqarish ombori topilmadi" |
| Ishlab chiqarish omborida DLOC joylashuvi | Ombor sozlamalari | Tayyor mahsulot kirimi joylashuv orqali qabul qilinadi |
| Sex "Ishlab chiqarish" | `/Shop` | Kunlik ishlab chiqarish sex bo'yicha kiritiladi |
| Tayyor mahsulot kartochkasida Sex = Ishlab chiqarish | Muhandislik → Maxsulotlar | Faqat shu sexning mahsulotlari rejadan yuklanadi |
| Har tayyor mahsulot uchun norma (BOM) | Muhandislik → Norma-sarf | Xomashyo chiqimi BOM bo'yicha. BOM bo'lmasa, xomashyo chiqim qilinmaydi |
| Nuqson turlari | Ishlab chiqarish → Nuqson turlari | Sifat nazoratida rad etishda tanlanadi |
| Xomashyo Ishlab chiqarish omborida | Ombor → ichki o'tkazma | Chiqim faqat shu ombor qoldig'idan bo'ladi |

> **Diqqat:** Xarid qilingan barcha xomashyo avval "Xomashyo ombori" ga kirim qilinadi. Sex ishlatishi uchun omborchi uni ichki o'tkazma bilan "Ishlab chiqarish ombori" ga o'tkazishi va sex qabul qilishi kerak. Xomashyo "Xomashyo ombori" da turgan bo'lsa ham, Ishlab chiqarish omborida yetmasa, ishlab chiqarish saqlanmaydi.

## 5.3 Ishlab chiqarish rejasi

**Kim:** ishlab chiqarish boshlig'i   **Menyu:** Ishlab chiqarish → Ishlab chiqarish rejasi

Ro'yxat ustunlari: "Mahsulot", "Reja miqdori", "Savdo buyurtmasi", "Boshlanish sanasi", "Tugash sanasi", "Kunlar soni", "Amallar". Filtr: mahsulot, "Dan", "Gacha". Tugmalar: "Yangi Reja", "Jadvalda kiritish", "Jadvalda tahrirlash".

### 5.3.1 Sotuv buyurtmasidan reja yaratish

Ssenariy: UzAutoMotors Powertrain'ning noyabr buyurtmasi — 1 200 dona iPack P1 va 3 000 dona iPack B6.1, uch partiyada (10.11, 20.11, 30.11). Buyurtma Sotuv bo'limida kiritilgan (raqami misol uchun SO-2026-11-001).

1. "Yangi Reja" ni bosing.
2. "Mahsulot" = **iPack P1**, "Reja miqdori" = **1200**, "Boshlanish sanasi" = **02.11.2026**, "Tugash sanasi" = **25.11.2026**.
3. "Savdo buyurtmasi" = **SO-2026-11-001 (dd.mm.yyyy)** (ixtiyoriy, lekin tavsiya etiladi).
4. Saqlang: "Ishlab chiqarish rejasi muvaffaqiyatli yaratildi".
5. Xuddi shunday iPack B6.1: 3 000 dona, 02.11.2026 – 21.11.2026.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Mahsulot * | Tayyor mahsulot | iPack P1 |
| Reja miqdori * | Davr uchun jami miqdor (0 dan katta) | 1 200 |
| Boshlanish sanasi * | Reja birinchi kuni | 02.11.2026 |
| Tugash sanasi * | Reja oxirgi kuni (boshlanishdan kichik emas) | 25.11.2026 |
| Savdo buyurtmasi | Bog'lanadigan buyurtma (oxirgi 500 tasi) | SO-2026-11-001 |

**Natija:** Kunlik reja avtomatik tuziladi. iPack P1: 1 200 / 24 kun = **50 dona/kun**, hammasi "Kunduzgi smena" da. iPack B6.1: 3 000 / 20 kun = **150 dona/kun**.

Tizim tekshiruvlari:

| Holat | Xabar |
|---|---|
| Tugash sanasi boshlanishdan oldin | "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas" |
| Shu mahsulot uchun muddati kesishadigan reja bor | "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud." |
| Tanlangan buyurtmada bu mahsulot yo'q | "Xatolik: ushbu savdo buyurtmasida tanlangan mahsulot yo'q." |
| Reja miqdori buyurtma miqdoridan katta | "Xatolik: reja miqdori (1 500,00) savdo buyurtmasi miqdoridan (1 200,00) oshib ketdi." |

> **Diqqat:** Kunlik taqsimotda dam olish kunlari hisobga olinmaydi: barcha kalendar kunlari ishchi kun deb olinadi. Yakshanba ishlamasangiz, kunlik rejada yakshanba qatorini 0 qiling va qolgan kunlarga qo'shing (5.3.4).

> **Diqqat:** Miqdor yuqoriga yaxlitlanadi: 1 000 dona 3 kunga bo'linsa 334 + 334 + 334 = 1 002 chiqadi. Kerak bo'lsa oxirgi kunni qo'lda tuzating.

### 5.3.2 Partiyalar bo'yicha reja (kesishmalar qoidasi)

Bitta mahsulot uchun **bir vaqtda faqat bitta reja** bo'lishi mumkin. Muddatlar kesishsa (hatto bitta kun bo'lsa ham), ikkinchi reja saqlanmaydi. Shuning uchun partiyalar ketma-ket, ustma-ust tushmaydigan muddatlar bilan kiritiladi.

iPack P1 ni 3 partiya bo'yicha rejalash (bitta 1 200 lik reja o'rniga):

| Reja | Miqdor | Muddat | Kunlik |
|---|---|---|---|
| 1-partiya | 400 | 02.11 – 09.11.2026 | 50 |
| 2-partiya | 400 | 10.11 – 17.11.2026 | 50 |
| 3-partiya | 400 | 18.11 – 25.11.2026 | 50 |

Agar 2-partiyani 09.11 dan boshlasangiz (1-partiya 09.11 da tugaydi), "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud." xabari chiqadi.

### 5.3.3 Jadvalda kiritish va tahrirlash (oy bo'yicha)

**Menyu:** Ishlab chiqarish rejasi → "Jadvalda kiritish"

1. "Yil" va oyni tanlab "Oyni qo'llash" ni bosing (Noyabr 2026).
2. Har qatorga: "Mahsulot", "Reja miqdori", "Boshlanish sanasi", "Tugash sanasi", "Savdo buyurtmasi" ("-- ixtiyoriy bog'lash --").
3. "Yangi qator qo'shish" bilan P1 ning 3 partiyasini va B6.1 ni kiriting.
4. "Saqlash" ni bosing. Xabar: "N ta reja saqlandi."

Har qator alohida tekshiriladi. Xato qatorlar saqlanmaydi, to'g'rilari saqlanadi. Jadvalning o'zida bir mahsulot uchun ikki qator muddati kesishsa: "Bir xil mahsulot uchun jadvalda ustma-ust tushadigan qatorlar bor." Miqdor 0 bo'lsa: "Miqdor 0 dan katta bo'lishi kerak."

"Jadvalda tahrirlash" — tanlangan oyning mavjud rejalarini jadvalda o'zgartirish, yangi qator qo'shish yoki qatorni o'chirish. Natija: "Yangilandi: N, qo'shildi: M".

### 5.3.4 Rejani ko'rish va kunlik rejani qo'lda tahrirlash

Ro'yxatda rejani oching. Tafsilot sahifasida:

- **"Ishlab Chiqarish Rejasi Ma'lumotlari"** — mahsulot, reja miqdori, sanalar, kunlar soni, savdo buyurtmasi.
- **"Bog'langan yetkazib berish jadvallari"** — buyurtmaning shu reja muddatiga to'g'ri keladigan yetkazish sanalari (masalan, 10.11 dagi 400 dona).
- **Kunlik reja jadvali (Reja vs Fakt)** — "Sana", "Kunduzgi smena (Reja)", "Tungi smena (Reja)", "Jami (Reja)", "Kunduzgi smena (Fakt)", "Tungi smena (Fakt)", "Jami (Fakt)", "Farq".

Kunlik rejani qo'lda o'zgartirish:

1. Jadvalda kerakli kunlarning "Kunduzgi smena (Reja)" va "Tungi smena (Reja)" kataklarini o'zgartiring. Masalan, 03.11: kunduzgi 35, tungi 15; 08.11 (yakshanba): 0.
2. "Kunlik rejalarni saqlash" ni bosing. Yashil "Kunlik reja muvaffaqiyatli yangilandi" matni chiqadi.

> **Diqqat:** Rejaning miqdori yoki muddati o'zgartirilsa, kunlik reja **qayta taqsimlanadi** va qo'lda kiritilgan kunlik o'zgarishlar yo'qoladi. Xabar: "Muddat yoki miqdor o'zgargani uchun kunlik reja qayta taqsimlandi." Avval rejani muddat va miqdor bo'yicha yakunlang, kunlik tahrirni oxirida qiling.

**O'chirish:** reja yumshoq o'chiriladi ("Ishlab chiqarish rejasi muvaffaqiyatli o'chirildi"). Kiritilgan ishlab chiqarish faktlari va ombor harakatlari o'chmaydi.

## 5.4 Kunlik ishlab chiqarish faktini kiritish

**Kim:** ishlab chiqarish boshlig'i (smena yakunida yoki ertasi kuni ertalab)   **Menyu:** Ishlab chiqarish → Kunlik ishlab chiqarish

Ro'yxat standart holatda oxirgi 30 kunni ko'rsatadi. Ustunlar: "Sana", "TSex", "Mahsulot", "Kunduzgi", "Tungi", "Jami", "Izoh", "Amallar". Filtr: "Dan", "Gacha", "Sex", "Mahsulot".

### 5.4.1 Faktni kiritish

Misol: 03.11.2026 kuni sexda 55 ta iPack P1 (kunduzgi 40, tungi 15) va 150 ta iPack B6.1 (kunduzgi) ishlab chiqarildi.

1. "Yangi Ishlab Chiqarish" ni bosing.
2. "Reja sanasi" = **03.11.2026**, "Fakt ishlab chiqarish sanasi" = **03.11.2026**, "Sex" = **Ishlab chiqarish**.
3. "Rejalashtirilgan mahsulotlarni yuklash" ni bosing. Jadvalda shu kun va sex rejasidagi mahsulotlar chiqadi: "Mahsulot", "Reja (Kunduzgi)", "Reja (Tungi)", "Reja (Jami)". Shu kun uchun kiritilgan fakt bo'lsa, u ham ko'rinadi.
4. iPack P1 qatori: "Kunduzgi smena" = **40**, "Tungi smena" = **15** ("Jami" = 55). iPack B6.1: "Kunduzgi smena" = **150**.
5. Kerak bo'lsa "Izoh" ga yozing (masalan, "Tungi smenada 1 stanok ta'mirda").
6. Saqlang: "Ishlab chiqarish muvaffaqiyatli saqlandi".

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Reja sanasi * | Ish qaysi reja kuniga tegishli | 03.11.2026 |
| Fakt ishlab chiqarish sanasi * | Amalda ishlab chiqarilgan kun | 03.11.2026 |
| Sex * | Sex | Ishlab chiqarish |
| Kunduzgi smena | Kunduzgi smena natijasi (dona) | 40 |
| Tungi smena | Tungi smena natijasi (dona) | 15 |
| Jami | Avtomatik: kunduzgi + tungi | 55 |
| Izoh | Erkin matn | — |

Kamida bitta mahsulotda jami 0 dan katta bo'lishi kerak. Aks holda: "Kamida bitta mahsulot uchun jami miqdor (kunduzgi + tungi) 0 dan katta bo'lishi kerak."

### 5.4.2 Saqlashda omborda nima bo'ladi

Hammasi bitta tranzaksiyada bajariladi: yoki hammasi yoziladi, yoki hech narsa yozilmaydi.

1. **Xomashyo yetarliligi tekshiriladi** (5.5).
2. **Xomashyo chiqimi:** har mahsulot uchun BOM bo'yicha kerakli xomashyo Ishlab chiqarish omboridan chiqim hujjati bilan avtomatik chiqariladi. Chiqim narxi FIFO bo'yicha (eng eski partiya narxi, QQSsiz). Izoh: "03.11.2026y. kunlik reja asosida Ishlab chiqarish uchun xomashyo chiqimi: iPack P1, Miqdor 55".
3. **Tayyor mahsulot kirimi:** tayyor mahsulot Ishlab chiqarish omboriga avtomatik kirim qilinadi va avtomatik qabul qilingan bo'ladi.
4. Hujjatlar ishlab chiqarish yozuviga bog'lanadi, shuning uchun tahrirlash va o'chirishda to'liq qaytariladi.

Misol (2-bobdagi namunaviy normalar: P1 — 2 dona taxta va 36 dona mix; B6.1 — 0,62 m² karton va 4 dona skoba):

| Xomashyo | Hisob | Chiqim | FIFO narx | Summa |
|---|---|---|---|---|
| Yog'och 100*50*6000 | 55 × 2 | 110 dona | 54 000 | 5 940 000 |
| Mix 80 mm | 55 × 36 | 1 980 dona | 50 | 99 000 |
| Karton_2090x1370 | 150 × 0,62 | 93 m² | 6 500 | 604 500 |
| Skoba | 150 × 4 | 600 dona | 15 | 9 000 |

**Natija:** Ishlab chiqarish omborida taxta qoldig'i 300 dan 190 ga tushadi, mix 20 000 dan 18 020 ga, karton 2 000 m² dan 1 907 m² ga, skoba 15 000 dan 14 400 ga. iPack P1 qoldig'i +55, iPack B6.1 +150. "Ombor → Operatsiyalar → Chiqimlar" va "Kirimlar" da avtomatik hujjatlar paydo bo'ladi. Oy oxirida fakt tannarxning material qismi shu chiqimlardan olinadi (2.8).

> **Diqqat:** Bir xil **reja kuni** va mahsulot uchun qayta kiritilgan fakt avvalgisiga **qo'shilmaydi, uni almashtiradi**. Masalan, 03.11 uchun P1 = 55 kiritilgan bo'lsa va yana 03.11 ochib P1 = 20 kiritsangiz, natija 75 emas, **20** bo'ladi. Eski chiqim va kirim to'liq bekor qilinib, 20 dona bo'yicha qayta yoziladi. Tungi smena natijasini keyinroq qo'shmoqchi bo'lsangiz, kunduzgi va tungi smenani birga kiriting (40 + 15).

> **Diqqat:** Xomashyo **eng so'nggi oy normasi** (BOM) bo'yicha chiqim qilinadi. Normani o'zgartirsangiz, keyingi barcha ishlab chiqarishlar yangi norma bo'yicha chiqim qilinadi (2.4.5).

> **Maslahat:** Kechikib kiritilgan ishni to'g'ri reja kuniga yozing. Masalan, 05.11 rejasidagi 50 taglik 06.11 tongida tugatilgan bo'lsa: "Reja sanasi" = 05.11, "Fakt ishlab chiqarish sanasi" = 06.11. Reja tafsilotida fakt 05.11 qatorida ko'rinadi.

### 5.4.3 Ko'rish, tahrirlash, o'chirish

- **Ko'rish (Details):** mahsulot, sana, sex, smenalar. **"BOM/Norm bo'yicha sarf"** jadvali: "Mahsulot kodi", "Xom ashyo nomi", "Talab qilingan", "Jami sarf", "O'lchov birligi", "Dona narxi", "Summa (pul)". **"Chiqim hujjatlari"** bloki: bog'langan chiqim hujjatlari ("Hujjat raqami", "Sana va vaqt", "Mahsulotlar soni", "Jami miqdor", "Summa") va chiqim sahifasiga havola.
- **Tahrirlash:** smena miqdorlarini o'zgartirish. Saqlanganda eski chiqim va kirim bekor qilinib, yangi miqdor bo'yicha qayta yoziladi: "Ishlab chiqarish muvaffaqiyatli yangilandi". Masalan, 55 → 57 qilinsa: P1 +2, taxta −4, mix −72.
- **O'chirish:** ro'yxatdagi "O'chirish" tugmasi (tasdiqlash bilan). Xomashyo omborga qaytariladi, tayyor mahsulot kirimi bekor qilinadi: "Ishlab chiqarish muvaffaqiyatli o'chirildi". O'chirilgan yozuv sahifasi endi ochilmaydi.

Bloklanish qoidasi: yozuvga **sifat nazorati** yoki **sotuvga o'tkazma** (lot) bog'langan bo'lsa, uni tahrirlash ham, o'chirish ham mumkin emas:

> "Bu ishlab chiqarish sifat nazorati yoki lot o'tkazmasi (sotuvga transfer) bilan bog'langan. O'zgartirish mumkin emas." (o'chirishda — "... O'chirish mumkin emas.")

Mahsulotning boshqa partiyasi sotilgani yozuvni bloklamaydi. Tayyor mahsulot kirimining lotidan allaqachon chiqim qilingan bo'lsa ham, tahrirlash rad etiladi.

> **Maslahat:** Faktni sifat nazoratidan **oldin** tekshirib oling. Sifat nazorati kiritilgach, xatoni to'g'rilash uchun avval sifat nazoratini o'chirish kerak (faqat tasdiqlanmagan bo'lsa mumkin).

### 5.4.4 "Chiqimni tuzatish (BOM)"

Ro'yxat sahifasidagi bu tugma faqat chiqimlarni o'zgartirish huquqi bor foydalanuvchiga ko'rinadi. U har bir ishlab chiqarish uchun avtomatik xomashyo chiqimini "ishlab chiqarilgan son × BOM" ga moslab qayta hisoblaydi. Tayyor mahsulot kirimiga tegilmaydi. Natija: "Tuzatildi: N, allaqachon to'g'ri: M, o'tkazib yuborildi: K, xatolar: X."

> **Diqqat:** Bu xizmat amali, kundalik ish emas. Uni faqat administrator bilan kelishib bosing: chiqimlar **joriy** BOM bo'yicha qayta yoziladi, shuning uchun eski oylar xomashyo summasi ham o'zgarishi mumkin.

## 5.5 Xomashyo yetishmasa nima bo'ladi

Ishlab chiqarish saqlanishidan oldin har bir xomashyo Ishlab chiqarish omboridagi qoldiq bilan solishtiriladi. Biror xomashyo yetmasa, **hech narsa saqlanmaydi**: na fakt, na chiqim, na kirim.

Misol: 04.11 da yana 50 ta P1 kiritildi (taxta 190 → 90). 05.11 da 50 ta P1 kiritilmoqda, buning uchun 100 dona taxta kerak:

> "Ishlab chiqarish natijasini saqlab bo'lmadi: xomashyo yetarli emas. Mahsulot: Yog'och 100*50*6000, omborda 90,00 dona, kerak 100,00 dona"

Nima qilish kerak:

1. Omborchiga xabar bering: "Xomashyo ombori" dan "Ishlab chiqarish ombori" ga taxta o'tkazilsin (masalan, 500 dona). Sex o'tkazmani qabul qilsin.
2. "Xomashyo ombori" da ham taxta bo'lmasa, Ta'minot bo'limiga talabnoma bering (Wood Nam Import yoki WOODLAND BIZNES).
3. Taxta Ishlab chiqarish omboriga kelgach, 05.11 faktini qayta kiriting.

> **Diqqat:** Reja yaratishda xomashyo tekshirilmaydi, faqat fakt kiritishda tekshiriladi. Rejani tuzgandan keyin BOM bo'yicha ehtiyojni hisoblab (1 200 taglik × 2 = 2 400 taxta) xaridni oldindan boshlang.

> **Maslahat:** Har kuni ertalab "Ombor → Operatsiyalar → Qoldiqlar" da Ishlab chiqarish omboridagi asosiy xomashyo qoldig'ini kunlik reja bilan solishtiring: 50 taglik × 2 = 100 taxta, 150 quti × 0,62 = 93 m² karton.

## 5.6 Sifat nazorati

**Kim:** Texnolog yoki sifat nazorati mas'uli   **Menyu:** Ishlab chiqarish → Sifat nazorati

Ro'yxat ustunlari: "Sana", "Mahsulot", "Tekshirilgan", "Qabul qilingan", "Rad etilgan", holat, "Amallar". Filtr: qidiruv, mahsulot, "Holat" (Tasdiqlangan / Rad etilgan), "Dan", "Gacha". Standart holatda oxirgi 30 kun.

### 5.6.1 Sifat nazoratini kiritish

1. "Yangi Sifat Nazorati" ni bosing.
2. "Fakt ishlab chiqarish sanasi" = **03.11.2026** ni tanlab yuklang ("Sanani tanlang va yuklash").
3. Jadvalda shu kunning ishlab chiqarishlari: "Mahsulot", "Ishlab chiqarildi", "Tekshirildi" (avval), "Qoldiq" (hali tekshirilmagan).
4. iPack P1: "Qabul" = **54**, "Rad" = **1**, "Nuqson turi" = **Yorilgan taxta**.
5. iPack B6.1: "Qabul" = **148**, "Rad" = **2**, "Nuqson turi" = **Karton ezilgan**.
6. Saqlang: "Sifat nazorati muvaffaqiyatli yaratildi".

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Qabul | Sifatli deb qabul qilingan miqdor | 54 |
| Rad | Brak miqdor | 1 |
| Nuqson turi | Rad sababi (ma'lumotnomadan) | Yorilgan taxta |
| Izoh | Erkin matn | "Taxta namligi yuqori" |

Holat avtomatik beriladi:

| Holat | Qachon | Amallar |
|---|---|---|
| Tasdiqlangan (yashil) | "Qabul" > 0 | O'chirib bo'lmaydi |
| Rad etilgan (qizil) | "Qabul" = 0 va "Rad" > 0 | "O'chirish", "Tasdiqlash" |

Holat o'tishi: **Rad etilgan → (Tasdiqlash) → Tasdiqlangan**. "Tasdiqlash" bosilganda: "Sifat nazorati muvaffaqiyatli tasdiqlandi". Allaqachon tasdiqlangan bo'lsa: "Bu sifat nazorati allaqachon tasdiqlangan". Tasdiqlanganini o'chirishga urinilsa: "Tasdiqlangan sifat nazoratini o'chirish mumkin emas".

Tekshiruvlar:

- Tekshirilgan miqdor (qabul + rad) ishlab chiqarilgan qoldiqdan oshmaydi. Masalan, P1 da 55 dan 54 + 1 tekshirilgan, qoldiq 0. Yana "Qabul" = 3 kiritilsa: "Tekshirilgan miqdor ishlab chiqarilgan qoldiqdan oshib ketdi. Mahsulot: iPack P1, Ishlab chiqarildi: 55, ...".
- Hech bir qatorda miqdor kiritilmasa: "Tekshiruv uchun miqdor kiritilmadi".

**Natija:**
- Rad etilgan miqdor nuqson turi bilan **brak ro'yxatiga** avtomatik yoziladi (5.7). Bir tekshiruv uchun bitta yozuv; tasdiqlash qo'shimcha yozuv yaratmaydi.
- Qabul qilingan miqdor shu ishlab chiqarish lotini **sotuvga o'tkazishga ruxsat** beradi (5.8).
- Sifat nazorati ishlab chiqarish yozuvini qulflaydi (5.4.3).

> **Diqqat:** Sifat nazorati ombor qoldig'ini o'zgartirmaydi. Rad etilgan 1 ta taglik Ishlab chiqarish ombori qoldig'ida qoladi. Brak mahsulotni hisobdan chiqarish yoki qayta ishlash Ombor bo'limida alohida rasmiylashtiriladi.

> **Maslahat:** Sifat nazoratini har kuni ishlab chiqarish kiritilgandan keyin, o'sha kunning o'zida o'tkazing. Sifat nazoratidan o'tmagan lotni Savdo omboriga o'tkazib bo'lmaydi, demak uni yetkazib ham bo'lmaydi.

## 5.7 Shikastlangan mahsulot (brak)

**Kim:** Texnolog, ishlab chiqarish boshlig'i   **Menyu:** `/DamagedProduct` manzili (menyuda alohida band yo'q)

"Brak holatdagi mahsulotlar" ro'yxati: "Defect Turi", "Mahsulot raqami", "Miqdori", "Amallar". Sifat nazoratidagi rad etishlar bu yerga avtomatik tushadi.

Qo'lda kiritish (sifat nazoratidan tashqari aniqlangan brak, masalan, Savdo omborida yuklashda singan taglik):

1. Yaratish tugmasini bosing.
2. "Mahsulotni tanlang" = **iPack P1**, "Defect turini tanlang" = **Yorilgan taxta**, "Miqdori" = **2**.
3. Saqlang. Yozuv ro'yxatda bir marta paydo bo'ladi.

Tahrirlash (miqdor, mahsulot, nuqson turi), ko'rish va o'chirish odatdagidek ishlaydi.

> **Diqqat:** Brak yozuvi faqat statistika uchun (sifat hisobotlari, nuqson turlari tahlili). U ombor qoldig'ini kamaytirmaydi.

## 5.8 Ishlab chiqarishdan sotuvga o'tkazma

**Kim:** ishlab chiqarish boshlig'i yoki omborchi   **Menyu:** Ombor → Operatsiyalar → Ishlab chiqarishdan sotuvga

Sifat nazoratidan o'tgan tayyor mahsulot Ishlab chiqarish omboridan Savdo omboriga shu hujjat bilan o'tkaziladi. Sotuv invoysi esa faqat Savdo omboridan chiqim qiladi.

1. "Yangi o'tkazma" ni bosing. "Ishlab chiqarish ombori" va "Sotuv ombori" avtomatik qo'yiladi.
2. "O'tkazma sanasi" = **04.11.2026** (bugundan keyingi sana bo'lmasin). "Hujjat raqami" ni bo'sh qoldirsangiz, avtomatik beriladi. Kerak bo'lsa "Hujjat fayli" (PDF, Word yoki rasm) va "Izoh" qo'shing.
3. "Mahsulotlar" bo'limida: "Mahsulot" = **iPack P1** (ro'yxatda faqat Ishlab chiqarish omborida bor mahsulotlar "Mavjud: X" bilan chiqadi).
4. Lot ro'yxatidan **03.11.2026 lotini** tanlang (lotlar FIFO tartibida, eskisi birinchi). "Tanlangan lotlar qoldig'i" ko'rinadi.
5. "O'tkazish miqdori" = **54**.
6. Yana qator: iPack B6.1, 03.11 loti, miqdor **148**.
7. Saqlang: "O'tkazma muvaffaqiyatli yaratildi".

Tekshiruvlar:

| Holat | Xabar |
|---|---|
| Ishlab chiqarish omborida yetmaydi | "Mahsulot iPack P1 uchun ishlab chiqarish omborida yetarli qoldiq yo'q. Mavjud: ..., Talab qilinmoqda: ..." |
| Lot tanlanmagan | "Mahsulot iPack P1 uchun sifat nazoratidan o'tgan lot tanlash majburiy." |
| Lotlar yig'indisi miqdorga teng emas | "Mahsulot uchun tanlangan lotlar yig'indisi (...) kiritilgan miqdor (...)ga mos kelmadi." |
| Lot sifat nazoratidan o'tmagan yoki qabul qilingan miqdordan ko'p | Sifat haqida xato |
| Sana kelajakda | "O'tkazma sanasi bugungi kunidan oshmasligi kerak." |

**Natija:**
- Ishlab chiqarish omboridan bitta chiqim hujjati yaratiladi (barcha qatorlar bitta hujjatda).
- Savdo omboriga **kutilayotgan kirim** yaratiladi. Tafsilotda holat "Kutilmoqda".
- Savdo ombori omborchisi "Ombor → Operatsiyalar → Kirimlar" da shu kirimni **"Qabul qilish"** bilan tasdiqlaydi. Shundan keyin Savdo omboridagi qoldiq oshadi va sotuv invoysi (10.11 dagi 1-partiya: 400 P1 + 1 000 B6.1) shu qoldiqdan chiqim qiladi.
- Lot o'tkazmasi ishlab chiqarish yozuvini qulflaydi (5.4.3).

O'tkazma tafsiloti: "Hujjat raqami", omborlar, "Sana va vaqt", "Holati", "Qabul qilish hujjati" ("Qabul qilish sanasi", holat), "Mahsulotlar", "Bog'langan hujjatlar".

> **Diqqat:** Rad etilgan (brak) miqdorni o'tkazib bo'lmaydi: lotdan faqat sifat nazoratida "Qabul" qilingan miqdor o'tkaziladi. 03.11 lotida P1 55 dona ishlab chiqarilgan, 54 tasi qabul qilingan, demak ko'pi bilan 54 dona o'tkaziladi.

## 5.9 MES dashboard va hisobotlar

### 5.9.1 MES Dashboard

**Menyu:** Ishlab chiqarish → Dashboard (yoki umumiy "Dashboard → MES Dashboard")

Filtr: "Yil", oy, "Kun". Birinchi ochilganda bugungi kun tanlanadi. Kun tanlansa, asosiy davr kun, ikkinchi davr oy bo'ladi. Faqat oy tanlansa, asosiy davr oy, ikkinchisi yil bo'ladi.

| Blok | Nima ko'rsatadi |
|---|---|
| Reja / fakt kartalari | Tanlangan davr rejasi, fakti, reja/fakt bajarilish foizi (100% va undan yuqori — yashil); ikkinchi davr bajarilishi |
| Kunlik Reja vs Fakt (grafik) | Har kun bo'yicha reja va fakt. Fakt **reja kuniga** yoziladi |
| Ishlab chiqarish bo'yicha sexlar | Sexlar kesimida ishlab chiqarilgan miqdor |
| Sifat Nazorati Xulosa | "Jami tekshiruvlar", "Tasdiqlangan", "Qabul qilingan miqdor", "Brak miqdor", "Brak chiqqan tekshiruvlar" |
| Buyurtma To'siqlari (Top 10) | "Buyurtma", "Mahsulot", "Reja", "Fakt", "Qoldiq", "Izoh". Qoldiq = buyurtma − invoys qilingan; fakt — mahsulot bo'yicha jami ishlab chiqarilgan (buyurtmaga bevosita bog'lanmagan) |

### 5.9.2 Oylik hisobotlar

**Menyu:** Ishlab chiqarish → Oylik hisobotlar → Ishlab chiqarish oylik / Sifat oylik

Ikkalasida "Yil" va oy tanlanib "Ko'rsatish" bosiladi. "Chop etish" — imzo joyi bor bosma shakl.

**Ishlab chiqarish oylik hisoboti:** "Mahsulot kodi", "Mahsulot nomi", "Reja", "Fakt", "Farq", "Qoldiq (ishlab chiqarish ombori)", "JAMI". Imzo: "Ishlab chiqarish boshlig'i". Noyabr oxirida kutiladigan natija: iPack P1 — reja 1 200, fakt 1 200, farq 0; iPack B6.1 — 3 000 / 3 000 / 0. "Qoldiq" ustuni "Ombor → Qoldiqlar" dagi Ishlab chiqarish ombori qoldig'iga teng bo'ladi.

**Sifat oylik hisoboti:** "Mahsulot bo'yicha" — "Ishlab chiqarilgan", "Tekshiruvlar soni", "Tekshirilgan miqdor", "Qabul qilingan", "Rad etilgan", "FTQ %". "Eng ko'p uchraydigan nuqson turlari" — "Nuqson turi", "Rad etilgan miqdor". Imzo: "Sifat nazorati mas'uli".

> **Maslahat:** Reja tafsiloti, Dashboard va oylik hisobotlarda fakt bitta qoida bilan hisoblanadi: **reja kuni** bo'yicha. Shuning uchun uchala sahifadagi raqamlar bir-biriga mos keladi. Mos kelmasa, reja sanasi noto'g'ri kiritilgan faktni qidiring.

## 5.10 Ishlab chiqarish boshlig'ining kunlik ish tartibi

**Oy boshida (yoki yangi buyurtma kelganda)**
1. Sotuv bo'limidan yangi buyurtmani oling (SO-2026-11-001: 1 200 P1, 3 000 B6.1, 3 partiya).
2. Texnolog bilan norma (BOM) dolzarbligini tekshiring: Muhandislik → BOM.
3. BOM bo'yicha xomashyo ehtiyojini hisoblang va Ta'minotga bering: 2 400 taxta, 43 200 mix, 1 860 m² karton, 12 000 skoba (misol).
4. "Jadvalda kiritish" orqali oy rejasini partiyalar bo'yicha kiriting. Dam olish kunlarini kunlik rejada 0 qiling.

**Har kuni ertalab (08:00)**
1. MES Dashboard → bugungi kun: kechagi reja/fakt foizi, "Buyurtma To'siqlari".
2. Ombor → Qoldiqlar: Ishlab chiqarish omboridagi xomashyo bugungi rejaga yetadimi (5.5). Yetmasa, omborchidan o'tkazma so'rang.
3. Kechagi tungi smena natijasini kiriting yoki tekshiring (5.4).

**Smena yakunida**
1. "Kunlik ishlab chiqarish" → "Yangi Ishlab Chiqarish": reja sanasi, fakt sanasi, sex → "Rejalashtirilgan mahsulotlarni yuklash" → kunduzgi va tungi smena miqdorlari → saqlash.
2. Ishlab chiqarish tafsilotida "BOM/Norm bo'yicha sarf" va "Chiqim hujjatlari" ni tekshiring.
3. Sifat nazorati mas'uli bilan shu kun uchun "Yangi Sifat Nazorati" kiriting (qabul, rad, nuqson turi).
4. Sifatdan o'tgan miqdorni "Ishlab chiqarishdan sotuvga" o'tkazing (lot tanlab). Savdo omborchisiga "Qabul qilish" ni eslating.

**Yetkazish kunlari oldidan (09.11, 19.11, 29.11)**
1. Reja tafsilotida "Bog'langan yetkazib berish jadvallari" va "Reja vs Fakt" ni tekshiring.
2. Savdo omborida partiya uchun yetarli qabul qilingan mahsulot borligini tasdiqlang (400 P1 + 1 000 B6.1). Sotuv bo'limiga xabar bering.

**Oy oxirida**
1. "Ishlab chiqarish oylik" va "Sifat oylik" hisobotlarini chop eting, imzolang.
2. Ishlab chiqarish omboridagi qoldiqni (xomashyo va tayyor mahsulot) omborchi bilan solishtiring.
3. Buxgalteriyaga oy yopilganini xabar qiling: ular tannarx faktini yangilaydi (2.8).

## 5.11 Tez-tez uchraydigan savollar va muammolar

**"Rejalashtirilgan mahsulotlarni yuklash" bo'sh jadval qaytardi.**
Shu kunga reja yo'q yoki mahsulot kartochkasida boshqa sex tanlangan. Rejani va mahsulotning "Sex" maydonini tekshiring.

**"Ishlab chiqarish ombori topilmadi".**
Omborlar sozlamasida turi "Ishlab chiqarish" bo'lgan ombor yo'q. Administratorga murojaat qiling.

**Ishlab chiqarish saqlandi, lekin xomashyo chiqim qilinmadi.**
Mahsulotda BOM (norma) yo'q. Texnologga norma kiritishni so'rang. Keyin faktni tahrirlab qayta saqlang yoki administrator "Chiqimni tuzatish (BOM)" ni bajarsin.

**Bir kunga ikki marta kiritdim, miqdor qo'shilmadi.**
Shu reja kuni va mahsulot uchun yangi kiritish eskisini almashtiradi (5.4.2). To'liq kunlik miqdorni bitta yozuvda kiriting.

**Ishlab chiqarishni tahrirlab bo'lmayapti: "...sifat nazorati yoki lot o'tkazmasi bilan bog'langan".**
Avval bog'langan sifat nazoratini o'chiring (tasdiqlanmagan bo'lsa). Lot sotuvga o'tkazilgan bo'lsa, tuzatish uchun administratorga murojaat qiling.

**Reja tafsilotida fakt boshqa kunda ko'rinyapti.**
Fakt "Reja sanasi" ga yoziladi, "Fakt ishlab chiqarish sanasi" ga emas. Yozuvni tahrirlab reja sanasini tekshiring.

**Rejani uzaytirgandim, qo'lda kiritgan kunlik rejam yo'qoldi.**
Muddat yoki miqdor o'zgarsa, kunlik reja qayta taqsimlanadi. Kunlik tahrirni muddat yakunlangandan keyin qiling.

**Sotuvga o'tkazmada lot ro'yxati bo'sh yoki "lot tanlash majburiy".**
Shu mahsulotning lotlari uchun sifat nazorati kiritilmagan yoki qabul qilingan miqdori allaqachon o'tkazilgan.

**Savdo omborida qoldiq ko'rinmayapti, o'tkazma qilingan bo'lsa ham.**
O'tkazmadan keyin Savdo omboridagi kirim "Kutilmoqda" holatida. Savdo omborchisi "Ombor → Kirimlar" da "Qabul qilish" ni bosishi kerak.

**Brak kiritilgandan keyin ham ombordagi qoldiq o'zgarmadi.**
Bu to'g'ri: brak va sifat nazorati ombor qoldig'iga ta'sir qilmaydi. Brak mahsulotni ombordan chiqarish Ombor bo'limida alohida rasmiylashtiriladi.
