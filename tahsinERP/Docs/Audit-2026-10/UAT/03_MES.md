# 3. Ishlab chiqarish, muhandislik va tannarx

Bu bo'lim DEMO-TM-01 "Ofis stoli 1200x600" ning to'liq yo'lini tekshiradi: ma'lumotnomalar, kategoriya va mahsulot → norma va BOM → ishlab chiqarish rejasi → kunlik ishlab chiqarish (ombordan xomashyo chiqimi, tayyor mahsulot kirimi) → sifat nazorati va brak → MES dashboard va hisobotlar → oktyabr oyi tannarxi (reja va fakt).
Testlarni ro'yxatdagi tartibda bajaring, chunki har bir test oldingisining natijasidan foydalanadi. T-TAN testlari T-MES testlaridan keyin bajariladi.

Umumiy hisob (bitta stol normasi): 0,9 m² × 85 000 = 76 500 so'm, 24 dona × 120 = 2 880 so'm, jami **79 380 so'm/dona**.
100 stol uchun: 90 m² (7 650 000 so'm) va 2 400 dona (288 000 so'm), jami **7 938 000 so'm**.

Oldindan bajarilgan bo'lishi kerak (boshqa bo'limlar):
- DEMO-XOM-01 (500 m²) va DEMO-XOM-02 (20 000 dona) xarid qilinib omborga kirim qilingan.
- Ikkala xomashyo **ishlab chiqarish omborida** turibdi. Tizim ishlab chiqarish uchun nomida "Ishlab chiqarish" (yoki "PROD") so'zi bor omborni ishlatadi: xomashyo shu yerdan chiqim qilinadi va tayyor mahsulot ham shu yerga kirim qilinadi. Bu omborda DLOC joylashuvi sozlangan bo'lishi kerak.
- Agar xomashyo "Xomashyo ombori"da bo'lsa, Ombor bo'limidagi ichki o'tkazma orqali ishlab chiqarish omboriga o'tkazing.

---

### T-MES-01. Sex, nuqson turi va jarayon ma'lumotnomalari
**Nima tekshiriladi:** Uch ma'lumotnoma (Sex, Nuqson turi, Ishlab chiqarish jarayoni) endi bitta umumiy mexanizmda ishlaydi. Yaratish, tahrirlash va o'chirish odatdagidek ishlashi kerak. O'chirilgan yozuv ro'yxatga qaytib chiqmasligi kerak (audit ID: MES-B17, dublikat #20).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Brauzer manziliga `/Shop` deb yozib oching (bu sahifa menyuda yo'q). Yaratish tugmasini bosing va "TSex nomi" maydoniga **DEMO Mebel sexi** deb yozib saqlang.
2. "Ishlab chiqarish → Nuqson turlari" sahifasini oching. **DEMO Tirnalgan** nomli yangi nuqson turini yarating ("Batafsil" maydoniga: "Sirt tirnalgan").
3. Shu sahifada **DEMO O'chiriladigan** nomli nuqson turini yarating. Keyin uni tahrirlab nomini **DEMO O'chiriladigan 2** qiling va saqlang.
4. "DEMO O'chiriladigan 2" ni o'chiring (o'chirish sahifasida tasdiqlang).
5. `/ProductionProcess` manzilini oching. **DEMO Yig'ish** nomli jarayonni yarating, keyin nomini **DEMO Yig'ish va qadoqlash** ga o'zgartiring.
**Kutilgan natija:**
- Har bir saqlashdan keyin tegishli ro'yxat sahifasi xatosiz ochiladi va yangi yozuv ro'yxatda turadi.
- Tahrirlangan nom ro'yxatda yangilangan holda ko'rinadi.
- "DEMO O'chiriladigan 2" o'chirilgandan keyin ro'yxatda yo'q va sahifani yangilaganda ham qaytib chiqmaydi.
- "DEMO Mebel sexi" va "DEMO Tirnalgan" ro'yxatda qoladi (keyingi testlarda kerak bo'ladi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-02. Kategoriya va tayyor mahsulot DEMO-TM-01
**Nima tekshiriladi:** Kategoriya kodi avtomatik va takrorlanmas beriladi. Mahsulot formasida "Standart uzunlik (kesish uchun), mm" maydoni bor va saqlanadi. Mahsulot sahifalariga ruxsat server tomonida tekshiriladi (audit ID: MES-B10, MES-B17, MES-B18, MES-B21, MES-B05).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-01
**Qadamlar:**
1. "Muhandislik → Maxsulotlar → Maxsulot turlari" sahifasida yangi tur yarating. "Nomi" = **DEMO Ofis mebeli**. "Xususiyatlar" bo'limida "Tayyor maxsulot", "NORM talab qiladi" va "Sotishga ruxsat" belgilarini qo'ying.
2. Saqlangandan keyin ro'yxatda "DEMO Ofis mebeli" ning "Kod" ustunini yozib oling.
3. "Muhandislik → Maxsulotlar → Barcha" sahifasida "Yangi maxsulot" tugmasini bosing. Maydonlarni to'ldiring: "Maxsulot raqami" = **DEMO-TM-01**, "Maxsulot nomi" = **Ofis stoli 1200x600**, "Turi" = Maxsulot, "Kategoriya" = DEMO Ofis mebeli, "Sex (ishlab chiqarish bo'limi)" = DEMO Mebel sexi, "O'lchamlari" = 1200 x 600 x 750, "Standart uzunlik (kesish uchun), mm" = 1200, "O'lchov birligi" = dona. "Saqlash" ni bosing.
4. DEMO-TM-01 ni tahrirlash sahifasida oching va "Standart uzunlik" qiymatini tekshiring.
5. DEMO-XOM-01 xomashyosini oching va uning kategoriyasini tekshiring. Kategoriyada "Kesish talab qiladi" belgisi **qo'yilmagan** bo'lishi kerak va kategoriya nomida "yog'och" so'zi bo'lmasligi kerak. Agar shunday bo'lmasa, "Muhandislik → Maxsulotlar → Maxsulot turlari" da **DEMO Plita materiallari** turini yarating ("Xomashyo" va "Xarid qilishga ruxsat" belgilari bilan) va DEMO-XOM-01 ni shu turga o'tkazing.
**Kutilgan natija:**
- Turga avtomatik kod berilgan va bu kod boshqa turlar kodlari bilan takrorlanmaydi.
- DEMO-TM-01 "Barcha" va "Tayyor maxsulot" ro'yxatlarida turibdi.
- Tahrirlash sahifasida "Standart uzunlik" = 1200 ko'rinadi (avval bu maydon formada umuman yo'q edi).
- DEMO-XOM-01 kesilmaydigan, yog'och bo'lmagan turda. Bu muhim: aks holda norma sarfi taxta soniga yaxlitlanadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-03. Oktyabr normasi: optimizatsiya va saqlash (yagona forma)
**Nima tekshiriladi:** Norma Create va Edit sahifalari endi bitta umumiy formada ishlaydi. Optimizatsiya jadvalida kiritilgan sarf saqlanadi. Yangi qatorlarning sarfi serverga to'g'ri yetib boradi. Material ro'yxati sahifaga to'liq yuklanmaydi: kod yoki nom yozilganda serverdan qidiriladi (audit ID: MES dublikat #6, MES-B16, MES-B21, MES-P15).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-02
**Qadamlar:**
1. "Muhandislik → BOM va norma → Norma-sarf" sahifasini oching. DEMO-TM-01 qatorida "Yangi NORM" tugmasini bosing.
2. "Oy" maydonida **Oktyabr 2026** ni tanlang. "material narxlarini ombor avtomatik tortish" belgisi o'chiq turgan bo'lsin (bu standart holat).
3. "Qator qo'shish" tugmasini bosing. Material ro'yxatini oching va qidiruv maydoniga **DEMO-XOM** deb yozing, natijadan DEMO-XOM-01 ni tanlang. Qatorning qolganini to'ldiring: Miqdori = 1, Birlik = m², Narx = 85000.
4. Yana bitta qator qo'shing: Material = DEMO-XOM-02 (xuddi shunday qidirib tanlang), Miqdori = 24, Birlik = dona, Narx = 120.
5. "Sarf optimizatsiyasi (chiqindidan foydalanish)" tugmasini bosing.
6. "Optimizatsiya qilingan pozitsiyalar" jadvalida DEMO-XOM-01 qatorining "Sarf (m)" katagiga **0.9** yozing. "Miqdori" maydoni faqat butun son qabul qiladi, shuning uchun kasr sarf shu jadval orqali kiritiladi.
7. "Saqlash va tasdiqlash" tugmasini bosing.
8. Ochilgan sahifada "Norma oyi" = 2026-10 qatoridagi summalarni tekshiring. Keyin "Ko'rish" belgisini bosing.
**Kutilgan natija:**
- 3-qadam: material ro'yxati yozilgan matn bo'yicha 30 tadan sahifalab chiqadi, pastga aylantirilganda keyingilari qo'shiladi. Tanlangandan keyin qatorda material nomi paydo bo'ladi.
- 5-qadamdan keyin optimizatsiya bloki chiqadi. Ikkala qatorda "Kesilmaydigan material — optimizatsiya qo'llanilmadi." izohi turadi. Xato oynasi chiqmaydi (avval CSRF tufayli 500 xato berardi).
- Saqlashdan keyin "NORM muvaffaqiyatli saqlandi." xabari chiqadi.
- "Umumiy summa" = **79 380** so'm (76 500 + 2 880).
- "Ko'rish" sahifasida DEMO-XOM-01 uchun sarf 0,9 (jami narx 76 500) va DEMO-XOM-02 uchun sarf 24 (jami narx 2 880) ko'rinadi.
- "Norma-sarf" ro'yxatida DEMO-TM-01 ning "Normalar soni" = 1.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-04. BOM normadan to'g'ri quriladi
**Nima tekshiriladi:** BOM eng so'nggi normadan quriladi. BOM ro'yxati va daraxti tez ochiladi (audit ID: MES-B07, MES-P09, MES-P22, MES-P23).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-03
**Qadamlar:**
1. "Muhandislik → BOM va norma → BOM" sahifasini oching va ochilish vaqtini kuzating.
2. DEMO-TM-01 qatoridagi "Norm versiyasi" ustunini tekshiring.
3. DEMO-TM-01 ning BOM tafsilotlarini oching va "Materiallar bo'yicha sarf va summa" jadvalini ko'ring.
**Kutilgan natija:**
- BOM ro'yxati sezilarli kechikishsiz ochiladi (3 soniyadan kam).
- "Norm versiyasi" = 2026-10.
- Jadvalda: DEMO-XOM-01 uchun "Xomashyo sarfi" 0,9, birligi m², "Narxi" 85 000, "Xomashyo summasi" 76 500. DEMO-XOM-02 uchun sarf 24, birligi dona, narxi 120, summasi 2 880.
- "Jami" = **79 380**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-05. Noyabr normasi va BOM faqat eng so'nggi normaga ergashishi
**Nima tekshiriladi:** Yangi oy normasi oldingi normadan yuklanadi. BOM eng so'nggi oy normasini oladi. Eski oy normasi tahrirlanganda BOM qayta yozilmaydi (audit ID: MES-B07).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-04
**Qadamlar:**
1. "Norma-sarf" sahifasida DEMO-TM-01 normalarini oching va "Yangi NORMA qo'shish" tugmasini bosing.
2. "Oy" = **Noyabr 2026** ni tanlang. "Oldingi oylardagi normadan yuklash" ro'yxatidan 2026-10 normasini tanlang va "Yuklash" ni bosing.
3. DEMO-XOM-02 qatorida "Miqdori" ni **26** ga o'zgartiring.
4. "Sarf optimizatsiyasi" tugmasini bosing. DEMO-XOM-01 sarfi optimizatsiya jadvalida 0.9 ekanini tekshiring (0.9 bo'lmasa, 0.9 yozing). "Saqlash va tasdiqlash" ni bosing.
5. BOM sahifasida DEMO-TM-01 ni tekshiring.
6. 2026-10 normasini "Tahrirlash" orqali oching. "Izoh" maydoniga "DEMO tekshiruv" deb yozing. Optimizatsiyani bajarib, DEMO-XOM-01 sarfi 0.9 ekanini tekshiring va saqlang.
7. BOM sahifasini qayta tekshiring.
**Kutilgan natija:**
- 2-qadamda qatorlar oktyabr normasidan to'ldiriladi.
- 4-qadamdan keyin: "NORM muvaffaqiyatli saqlandi.", "Normalar soni" = 2.
- 5-qadam: BOM "Norm versiyasi" = 2026-11, DEMO-XOM-02 sarfi 26, "Jami" = 76 500 + 3 120 = **79 620**.
- 6-qadam: xabarda "BOM o'zgarmadi: u eng so'nggi norma (2026-11) bo'yicha qoladi." matni bor.
- 7-qadam: BOM hamon 2026-11 bo'yicha (26 dona). Avval eski oyni tahrirlash BOM'ni ham o'zgartirib yuborardi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-06. Norma davrini o'zgartirish: boshqa versiya qatorlari o'chmaydi
**Nima tekshiriladi:** Normani tahrirlashda oy boshqa normasi bor oyga o'zgartirilsa, rad etiladi. Bo'sh oyga o'zgartirilganda boshqa oy normasining qatorlari o'chib ketmaydi (audit ID: MES-B06).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-05
**Qadamlar:**
1. DEMO-TM-01 normalaridan 2026-11 normasini "Tahrirlash" orqali oching.
2. "Oy" ni **Oktyabr 2026** ga o'zgartiring va "Saqlash va tasdiqlash" ni bosing.
3. Shu formada "Oy" ni **Dekabr 2026** ga o'zgartiring va saqlang.
4. Normalar ro'yxatida 2026-10 normasining "Ko'rish" sahifasini oching.
**Kutilgan natija:**
- 2-qadam: saqlanmaydi va "Bu mahsulot uchun 2026-10 oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang." xabari chiqadi.
- 3-qadam: "NORM muvaffaqiyatli saqlandi."
- Ro'yxatda 2026-10 va 2026-12 normalari bor, 2026-11 yo'q. "Normalar soni" = 2.
- 2026-10 normasi buzilmagan: DEMO-XOM-01 sarfi 0,9, DEMO-XOM-02 sarfi 24, "Umumiy summa" = 79 380.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-07. Normani o'chirish va BOM'ning oldingi normaga qaytishi
**Nima tekshiriladi:** Norma o'chirilganda xato chiqmaydi va BOM qolgan eng so'nggi normadan qayta quriladi (audit ID: MES-B08, MES 3.2 tuzatishi).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-06
**Qadamlar:**
1. DEMO-TM-01 normalari ro'yxatida 2026-12 qatoridagi "O'chirish" tugmasini bosing va tasdiqlang.
2. BOM sahifasida DEMO-TM-01 ni tekshiring.
**Kutilgan natija:**
- "NORM muvaffaqiyatli o'chirildi." xabari chiqadi. "Invalid object name" kabi xato yo'q.
- "Normalar soni" = 1 (faqat 2026-10).
- BOM "Norm versiyasi" = 2026-10: DEMO-XOM-01 sarfi 0,9 va DEMO-XOM-02 sarfi 24, "Jami" = 79 380. Keyingi ishlab chiqarish testlari shu normaga tayanadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-08. Ishlab chiqarish rejasi: yaratish, muddatni o'zgartirish, ustma-ust tushish
**Nima tekshiriladi:** Reja yaratilganda kunlik reja avtomatik taqsimlanadi. Muddat o'zgarsa, kunlik reja qayta taqsimlanadi. Bir mahsulotga bir xil muddatda ikkinchi reja kiritib bo'lmaydi (audit ID: MES-B09, MES-B10).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-07
**Qadamlar:**
1. "Ishlab chiqarish → Ishlab chiqarish rejasi" sahifasida "Yangi Reja" ni bosing. Maydonlar: "Mahsulot" = DEMO-TM-01, "Reja miqdori (Jami)" = **100**, "Boshlanish sanasi" = **20.10.2026**, "Tugash sanasi" = **21.10.2026**. Saqlang.
2. Rejaning tafsilot sahifasidagi "Kunlik Reja" jadvalini ko'ring.
3. Rejani tahrirlab "Tugash sanasi" ni **24.10.2026** ga o'zgartiring va saqlang.
4. Tafsilot sahifasidagi kunlik rejani qayta ko'ring.
5. Yana "Yangi Reja" ni bosing: DEMO-TM-01, miqdor 50, sanalar **24.10.2026 – 31.10.2026**. Saqlashga harakat qiling.
**Kutilgan natija:**
- 1-qadam: "Ishlab chiqarish rejasi muvaffaqiyatli yaratildi".
- 2-qadam: 2 kun, har kuni "Jami (Reja)" = 50.
- 3-qadam: "Ishlab chiqarish rejasi muvaffaqiyatli yangilandi" va "Muddat yoki miqdor o'zgargani uchun kunlik reja qayta taqsimlandi." xabarlari chiqadi.
- 4-qadam: 20.10 dan 24.10 gacha 5 kun, har kuni 20, jami 100. Ro'yxatdagi "Kunlar soni" = 5. Avval kunlik qatorlar eski holicha qolardi.
- 5-qadam: saqlanmaydi, "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud." xabari chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-09. Jadvalda reja kiritish va kunlik rejani qo'lda tahrirlash
**Nima tekshiriladi:** Jadval (grid) orqali kiritishda jadvaldagi qatorlar bir-biri bilan ham tekshiriladi. Kunlik rejani joyida tahrirlab saqlash ishlaydi (avval bu tugma hech kimda ishlamasdi) (audit ID: MES-B10, MES-P25, MES-B15, MES 3.2).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-08
**Qadamlar:**
1. "Ishlab chiqarish rejasi" sahifasida "Jadvalda kiritish" ni bosing. Oy sifatida Noyabr 2026 ni tanlang.
2. Birinchi qator: DEMO-TM-01, miqdor 50, sanalar 01.11.2026 – 05.11.2026. "Yangi qator qo'shish" ni bosib ikkinchi qatorni kiriting: DEMO-TM-01, miqdor 30, sanalar 04.11.2026 – 08.11.2026. "Saqlash" ni bosing.
3. Ro'yxatda noyabr rejasini (01.11 – 05.11) oching. "Kunlik Reja (Inline Tahrirlash)" jadvalida 01.11.2026 qatorining "Kunduzgi smena (Reja)" qiymatini 10 dan **15** ga o'zgartiring va "Kunlik rejalarni saqlash" ni bosing.
4. Sahifani yangilang (F5).
**Kutilgan natija:**
- 2-qadam: "1 ta reja saqlandi." va xato "Bir xil mahsulot uchun jadvalda ustma-ust tushadigan qatorlar bor." chiqadi. Faqat birinchi qator (50 dona, 5 kun × 10) saqlangan.
- 3-qadam: tugma yonida yashil "Kunlik reja muvaffaqiyatli yangilandi" matni chiqadi. Kunlik reja maydonlari tahrirlanadigan holatda (kulrang emas).
- 4-qadam: 01.11 qatorida 15 saqlanib qolgan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-10. Kunlik ishlab chiqarish №1: xomashyo chiqimi va tayyor mahsulot kirimi
**Nima tekshiriladi:** Ishlab chiqarish kiritilganda xomashyo norma bo'yicha ombordan chiqim qilinadi. Bu chiqim ishlab chiqarishga ProductionID orqali bog'lanadi. Chiqim narxi haqiqiy FIFO tannarx (QQSsiz): omborda qolgan eng yangi partiyalar emas, navbatdagi eng eski partiya narxi olinadi. Tayyor mahsulot kirimi ham ProductionID bilan yoziladi. Hammasi bitta tranzaksiyada bajariladi (audit ID: MES-P12, MES-B20 PlanDate, MES-B02, MES-B11, MES-P11, SRM 2.6, SRM-B19).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-08
**Qadamlar:**
1. Avval "Ombor → Operatsiyalar → Qoldiqlar" sahifasida ishlab chiqarish omboridagi qoldiqlarni yozib oling: DEMO-XOM-01 (kutilmoqda 500 m²), DEMO-XOM-02 (20 000 dona), DEMO-TM-01 (0).
2. "Ishlab chiqarish → Kunlik ishlab chiqarish" sahifasida "Yangi Ishlab Chiqarish" ni bosing. Maydonlar: "Reja sanasi" = **20.10.2026**, "Fakt ishlab chiqarish sanasi" = **20.10.2026**, "Sex" = DEMO Mebel sexi. "Rejalashtirilgan mahsulotlarni yuklash" ni bosing.
3. DEMO-TM-01 qatorida "Reja (Jami)" = 20 ekanini tekshiring. "Kunduzgi smena" = **70** kiriting va saqlang.
4. Ro'yxatda yangi yozuvni oching. "BOM/Norm bo'yicha sarf" va "Chiqim hujjatlari" bloklarini ko'ring.
5. "Ombor → Operatsiyalar → Qoldiqlar" va "Ombor → Operatsiyalar → Kirimlar" sahifalarini tekshiring.
**Kutilgan natija:**
- "Ishlab chiqarish muvaffaqiyatli saqlandi". Ro'yxatda: sana 20.10.2026, DEMO-TM-01, Kunduzgi 70, Jami 70.
- Tafsilotda sarf: DEMO-XOM-01 = 70 × 0,9 = **63 m²**, DEMO-XOM-02 = 70 × 24 = **1 680 dona**.
- "Chiqim hujjatlari" blokida 1 ta hujjat. Uning summasi FIFO tannarxda (QQSsiz): 63 × 85 000 = 5 355 000 va 1 680 × 120 = 201 600, jami **5 556 600 so'm**. Bu xomashyo ishlab chiqarish omboriga 85 000 / 120 narxli birinchi partiyadan kelgan bo'lsa to'g'ri. Agar o'tkazmaga 90 000 narxli "DEMO 2-partiya" (T-OMB) ham qo'shilgan bo'lsa, 85 000 li qism tugagandan keyingi miqdor 90 000 da hisoblanadi. Hujjat izohi "20.10.2026y. kunlik reja asosida Ishlab chiqarish uchun xomashyo chiqimi: DEMO-TM-01, Miqdor 70" ko'rinishida.
- Qoldiqlar: DEMO-XOM-01 = 500 − 63 = **437 m²**, DEMO-XOM-02 = 20 000 − 1 680 = **18 320 dona**, DEMO-TM-01 = **70 dona**.
- "Kirimlar" da DEMO-TM-01 uchun 70 dona ishlab chiqarish kirimi bor.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-11. Ishlab chiqarish №2 (kechikkan fakt), xomashyo yetishmasligi va reja oralig'i bo'yicha fakt
**Nima tekshiriladi:**
- Fakt reja kuniga yoziladi (PlanDate), fakt sanasiga emas.
- Reja tafsilotida faqat shu reja oralig'idagi fakt ko'rinadi.
- Xomashyo yetmasa, ishlab chiqarish saqlanmaydi.
(audit ID: MES-B20, MES-P04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-10, T-MES-09
**Qadamlar:**
1. "Yangi Ishlab Chiqarish": "Reja sanasi" = **21.10.2026**, "Fakt ishlab chiqarish sanasi" = **22.10.2026**, Sex = DEMO Mebel sexi. Rejani yuklang, DEMO-TM-01 "Kunduzgi smena" = **30** kiriting va saqlang.
2. Yana "Yangi Ishlab Chiqarish": "Reja sanasi" = 23.10.2026, "Fakt" = 23.10.2026, DEMO-TM-01 "Kunduzgi smena" = **1000**. Saqlashga harakat qiling.
3. Oktyabr rejasining (20.10 – 24.10) tafsilot sahifasida "Reja vs Fakt (Kunlik)" jadvalini ko'ring.
4. Noyabr rejasining (01.11 – 05.11) tafsilot sahifasini oching.
**Kutilgan natija:**
- 1-qadam saqlanadi. Qoldiq: DEMO-XOM-01 = 437 − 27 = **410 m²**, DEMO-XOM-02 = 18 320 − 720 = **17 600 dona**, DEMO-TM-01 = **100 dona**. Chiqim summasi 27 × 85 000 + 720 × 120 = **2 381 400 so'm**.
- 2-qadam saqlanmaydi. Xabar: "Ishlab chiqarish natijasini saqlab bo'lmadi: xomashyo yetarli emas. Mahsulot: DEMO-XOM-01, omborda 410,00 m², kerak 900,00 m²". Qoldiqlar o'zgarmaydi.
- Oktyabr rejasi: 20.10 = Reja 20 / Fakt 70 / Farq +50; **21.10** = Reja 20 / Fakt 30 / Farq +10; 22.10 da fakt yo'q (30 dona 22-sanada ishlab chiqarilgan bo'lsa ham reja kuni 21.10). Jami fakt 100.
- Noyabr rejasida birorta kunda fakt yo'q: oktyabr ishlab chiqarishi boshqa rejaga tushmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-12. O'chirilgan ishlab chiqarish hisobga olinmaydi (o'chirish → qayta kiritish)
**Nima tekshiriladi:**
- O'chirish qoldiqlarni to'liq qaytaradi.
- O'chirilgan yozuv yashirin holda qayta ishlatilmaydi: qayta kiritilganda yangi, ko'rinadigan yozuv paydo bo'ladi.
- O'chirish faqat POST tugma orqali bajariladi.
(audit ID: MES-B01 Critical, MES-B02, MES-B04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-11
**Qadamlar:**
1. "Yangi Ishlab Chiqarish": Reja va Fakt sanasi = **24.10.2026**, Sex = DEMO Mebel sexi, DEMO-TM-01 "Kunduzgi smena" = **5**. Saqlang. Qoldiqlarni tekshiring.
2. Ro'yxatda shu yozuvning "O'chirish" tugmasini bosing va tasdiqlang. Yozuvning manzildagi ID raqamini yozib oling (masalan `/ProductProduction/Details/123`).
3. Qoldiqlarni tekshiring. Keyin `/ProductProduction/Details/<ID>` manzilini qo'lda oching.
4. Xuddi 1-qadamdagidek yana 5 dona kiriting (24.10.2026).
5. Qoldiqlar, ro'yxat va oktyabr rejasining 24.10 kunini tekshiring. Keyin bu yangi yozuvni ham o'chiring.
**Kutilgan natija:**
- 1-qadamdan keyin: DEMO-TM-01 = 105, DEMO-XOM-01 = 410 − 4,5 = 405,5 m², DEMO-XOM-02 = 17 600 − 120 = 17 480 dona.
- 2-qadam: "Ishlab chiqarish muvaffaqiyatli o'chirildi". 3-qadamda qoldiqlar qaytgan (100 / 410 / 17 600). O'chirilgan yozuv sahifasi "topilmadi" (404) beradi.
- 4-qadamdan keyin ro'yxatda **yangi** ko'rinadigan qator bor (5 dona). Qoldiq aniq +5 ga oshadi (105). 0 yoki 110 bo'lmasligi kerak.
- Oktyabr rejasida 24.10 fakt = 5 (o'chirilgan birinchi yozuv qo'shilmaydi).
- Yakuniy o'chirishdan keyin: DEMO-TM-01 = **100**, DEMO-XOM-01 = **410 m²**, DEMO-XOM-02 = **17 600 dona**. 24.10 da fakt yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-13. Sifat nazorati: qabul/rad, tasdiqlash, qoldiqdan oshmaslik
**Nima tekshiriladi:**
- Sifat nazorati yaratiladi va mavjud bo'lmagan sahifalarga havolalar yo'q.
- "Rad etilgan" yozuvni tasdiqlash xatosiz ishlaydi (avval NotSupportedException berardi).
- Brak yozuvi takrorlanmaydi.
- Tekshirilgan miqdor ishlab chiqarilgan qoldiqdan oshmaydi.
(audit ID: MES-B04, MES-P16)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-12, T-MES-01
**Qadamlar:**
1. "Ishlab chiqarish → Sifat nazorati" sahifasida "Yangi Sifat Nazorati" ni bosing. Sana sifatida **20.10.2026** ni tanlab yuklang.
2. DEMO-TM-01 qatorida "Ishlab chiqarildi" = 70 va "Qoldiq" = 70 ekanini tekshiring. "Qabul" = **68**, "Rad" = **2**, "Nuqson turi" = DEMO Tirnalgan kiriting va saqlang.
3. Yana "Yangi Sifat Nazorati" (20.10.2026): DEMO-TM-01 "Qoldiq" = 2 bo'ladi. "Qabul" = **3** kiriting va saqlashga harakat qiling.
4. Shu sahifada "Qabul" = 0, "Rad" = **1**, Nuqson turi = DEMO Tirnalgan kiritib saqlang.
5. Ro'yxatda holatlarni ko'ring. "Rad etilgan" yozuvdagi "Tasdiqlash" tugmasini bosing va tasdiqlang.
6. "Batafsil" sahifasini oching. Keyin `/DamagedProduct` manzilini oching.
**Kutilgan natija:**
- 2-qadam: "Sifat nazorati muvaffaqiyatli yaratildi". Holat "Tasdiqlangan" (yashil).
- 3-qadam saqlanmaydi. Xabar "Tekshirilgan miqdor ishlab chiqarilgan qoldiqdan oshib ketdi. Mahsulot: DEMO-TM-01, Ishlab chiqarildi: 70, Qabul qilingan (oldingi): 68, Qoldiq: 2" ko'rinishida.
- 4-qadamdan keyin holat "Rad etilgan" (qizil). Uning yonida "O'chirish" va "Tasdiqlash" tugmalari bor. "Tahrirlash" havolasi yo'q.
- 5-qadam: "Sifat nazorati muvaffaqiyatli tasdiqlandi", holat "Tasdiqlangan", xato sahifa chiqmaydi. Tasdiqlangan yozuvlarda "O'chirish" tugmasi ko'rinmaydi.
- "Batafsil" sahifasi ochiladi ("view not found" xatosi yo'q).
- Brak ro'yxatida DEMO-TM-01 / DEMO Tirnalgan uchun 2 ta yozuv bor: 2 dona va 1 dona, jami 3. Tasdiqlash qo'shimcha (uchinchi) yozuv yaratmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-14. Tahrirlash/o'chirish guard'i: sotilgan mahsulot bloklanmaydi
**Nima tekshiriladi:** Ishlab chiqarish faqat o'ziga sifat nazorati yoki lot o'tkazmasi (sotuvga transfer) bog'langanda bloklanadi. Mahsulot boshqa partiyadan sotilgani sababli bloklanmaydi (avval mahsulot bir marta sotilsa, uning barcha ishlab chiqarishlari qulflanardi) (audit ID: MES-B03).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-13. Iloji bo'lsa, CRM bo'limidagi DEMO-TM-01 sotuvi (60 stol) ham bajarilgan bo'lsin, ya'ni mahsulot sotuv invoysida bor.
**Qadamlar:**
1. "Kunlik ishlab chiqarish" ro'yxatida 20.10.2026 (70 dona) yozuvining "Tahrirlash" tugmasini bosing.
2. Shu yozuvni "O'chirish" ga harakat qiling.
3. 21.10.2026 reja kunli (fakt 22.10.2026, 30 dona) yozuvni "Tahrirlash" orqali oching. "Kunduzgi smena" ni **32** ga o'zgartirib saqlang va qoldiqni tekshiring.
4. Shu yozuvni qayta tahrirlab, "Kunduzgi smena" ni **30** ga qaytaring.
**Kutilgan natija:**
- 1- va 2-qadamlar bajarilmaydi. Xabar: "Bu ishlab chiqarish sifat nazorati yoki lot o'tkazmasi (sotuvga transfer) bilan bog'langan. O'zgartirish mumkin emas." (o'chirishda "... O'chirish mumkin emas.").
- 3-qadam: "Ishlab chiqarish muvaffaqiyatli yangilandi". DEMO-TM-01 sotuv invoysida bo'lsa ham tahrirlash ochiq. Qoldiq: DEMO-TM-01 +2, DEMO-XOM-01 −1,8 m², DEMO-XOM-02 −48 dona.
- 4-qadamdan keyin qoldiqlar T-MES-12 dagi yakuniy holatga qaytadi (DEMO-XOM-01 410 m², DEMO-XOM-02 17 600 dona, DEMO-TM-01 esa transferlar va sotuvni hisobga olib T-MES-12 dagidek). Reja kuni 21.10 saqlanib qolgan (oktyabr rejasida fakt 21.10 kunida).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-15. Brak (Damaged product) qo'lda kiritish va tahrirlash
**Nima tekshiriladi:** Brak yozuvini yaratish va tahrirlashda xato sahifa chiqmaydi. Yozuv takrorlanmaydi (avval saqlangandan keyin "saqlab bo'lmadi" chiqib, qayta bosilganda dublikat yaratilardi) (audit ID: MES-B12, MES-B17).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-13
**Qadamlar:**
1. `/DamagedProduct` sahifasida yaratish tugmasini bosing. "Mahsulotni tanlang" = DEMO-TM-01, "Defect turini tanlang" = DEMO Tirnalgan, "Miqdori" = **1**. Saqlang.
2. Shu yangi yozuvni tahrirlab "Miqdori" ni **2** ga o'zgartiring va saqlang.
3. Yozuvni o'chiring.
**Kutilgan natija:**
- 1-qadam: ro'yxat sahifasiga qaytadi. Yangi yozuv (DEMO-TM-01, 1) **bitta** marta ko'rinadi, xato xabari yo'q.
- 2-qadam: xato sahifasi chiqmaydi, miqdor 2 ga o'zgaradi.
- 3-qadamdan keyin ro'yxatda faqat T-MES-13 dagi 2 ta yozuv (2 va 1) qoladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-16. MES dashboard va oylik hisobotlar: raqamlar va tezlik
**Nima tekshiriladi:** Dashboard va hisobotlar fakt ishlab chiqarishni bitta qoida (reja kuni = PlanDate) bo'yicha hisoblaydi va tez ochiladi (audit ID: MES-B20, MES-P01, MES-P20, MES-P18).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-14
**Qadamlar:**
1. "Ishlab chiqarish → Dashboard" sahifasini oching va sana oralig'ini 01.10.2026 – 31.10.2026 qilib tanlang. Ochilish vaqtini kuzating.
2. "Ishlab chiqarish → Hisobotlar → Ishlab chiqarish oylik" sahifasida Yil 2026, Oktyabr ni tanlang.
3. "Ishlab chiqarish → Hisobotlar → Sifat oylik" sahifasida Yil 2026, Oktyabr ni tanlang.
4. "Kunlik ishlab chiqarish" va "Sifat nazorati" ro'yxatlarini filtrsiz oching.
**Kutilgan natija:**
- Har bir sahifa sezilarli kechikishsiz ochiladi (3 soniyadan kam).
- Ro'yxatlar standart holatda oxirgi 30 kunni ko'rsatadi.
- Dashboard "Kunlik Reja vs Fakt" da DEMO-TM-01 fakti 20.10 va 21.10 kunlarida (70 va 30), 22.10 da emas. Bu reja tafsilotidagi (T-MES-11) bilan bir xil.
- "Ishlab chiqarish oylik": DEMO-TM-01 uchun Reja **100**, Fakt **100**, Farq **0**. "Qoldiq (ishlab chiqarish ombori)" "Ombor → Qoldiqlar" dagi DEMO-TM-01 qoldig'iga teng.
- "Sifat oylik": DEMO-TM-01 uchun Tekshiruvlar soni 2, Tekshirilgan miqdor 71, Qabul qilingan 68, Rad etilgan 3. "Eng ko'p uchraydigan nuqson turlari" da DEMO Tirnalgan, 3.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-17. Stored XSS: Norma va reja jadvallarida mahsulot nomi kod sifatida ishlamaydi
**Nima tekshiriladi:** Mahsulot nomiga yozilgan HTML/JS kod Norma formasi va reja jadvalida oddiy matn bo'lib ko'rinishi va ishga tushmasligi kerak (audit ID: MES-B14).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-03
**Qadamlar:**
1. "Muhandislik → Maxsulotlar → Barcha → Yangi maxsulot". Maydonlar: "Maxsulot raqami" = **DEMO-XSS-01**, nomi = `DEMO <img src=x onerror=alert('XSS1')>`, Turi = Xomashyo, birlik = dona. Saqlang.
2. Xuddi shunday **DEMO-XSS-02** yarating: nomi = `DEMO <img src=x onerror=alert('XSS2')>`, Turi = Maxsulot, Sex = DEMO Mebel sexi.
3. "Norma-sarf" sahifasida DEMO-TM-01 uchun "Yangi NORMA qo'shish" ni oching. "Qator qo'shish" ni bosing, Material ro'yxatini oching va qidiruvga **DEMO-XSS** deb yozib DEMO-XSS-01 ni toping va tanlang. **Saqlamang**, "Bekor qilish" ni bosing.
4. "Ishlab chiqarish rejasi → Jadvalda kiritish" ni oching, "Yangi qator qo'shish" ni bosing va mahsulot ro'yxatida DEMO-XSS-02 ni toping. Saqlamang.
5. "Jadvalda tahrirlash" sahifasini ham oching.
6. Tekshiruvdan keyin DEMO-XSS-01 va DEMO-XSS-02 ni mahsulotlar ro'yxatidan o'chiring.
**Kutilgan natija:**
- Hech bir qadamda "XSS1" yoki "XSS2" ogohlantirish oynasi (alert) chiqmaydi.
- Ro'yxatlarda nom `DEMO <img src=x onerror=...>` ko'rinishida oddiy matn bo'lib turadi. Rasm belgisi chiqmaydi.
- Jadval sahifalarining tuzilishi buzilmaydi: qator qo'shish va o'chirish ishlaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-MES-18. Ruxsat: CRM foydalanuvchisi MES va Tannarx sahifalariga kira olmaydi
**Nima tekshiriladi:** Ruxsat endi faqat menyuda yashirish bilan emas, server tomonida ham tekshiriladi. To'g'ridan-to'g'ri URL ochish ham bloklanadi (audit ID: MES-B05).
**Kim:** demo_sotuv (faqat CRM)   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. demo_sotuv bilan tizimga kiring. Menyuda "Ishlab chiqarish" va "Muhandislik" bo'limlari yo'qligini tekshiring.
2. Quyidagi manzillarni brauzerga qo'lda yozib, birma-bir oching: `/ProductProduction/Create`, `/ProductPlan/Index`, `/Norm`, `/Product/Create`, `/DefectType/Create`, `/DamagedProduct`, `/QualityInspection/Index`, `/MESDashboard/Dashboard`, `/Tannarx`, `/TannarxAnalytics/MonthSources`, `/TannarxAnalytics/FinanceCategories`.
**Kutilgan natija:**
- Har bir manzilda "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- Hech qaysi forma yoki ma'lumot ochilmaydi va xato (500) sahifasi chiqmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

### T-TAN-01. Oktyabr tannarxini shakllantirish (reja) va dublikat himoyasi
**Nima tekshiriladi:** Norma bor mahsulotlar uchun oy tannarxi yaratiladi. Material narxi normadan olinadi. Qayta bosish dublikat yaratmaydi (audit ID: MES-P05, MES-P06, MES-B10).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-MES-16
**Qadamlar:**
1. "Muhandislik → Tannarx → Hisobot" sahifasini oching, Yil 2026, Oktyabr ni tanlang. Keyin "Dashboard" dagi "Oy xarajatlari manbalari" tugmasini bosing va ogohlantirishlarni ko'ring.
2. "Muhandislik → Tannarx → Tannarx" sahifasida Yil 2026, Oy Oktyabr ni tanlang va "Tannarx shakllantirish" ni bosing.
3. DEMO-TM-01 qatorini toping. Brauzerda "Orqaga" qilib, formani yana bir marta yuboring (yoki tugma ko'rinsa, yana bosing).
4. "Oy xarajatlari manbalari" sahifasini qayta oching.
**Kutilgan natija:**
- 1-qadam: "Ishlab chiqarilgan, lekin tannarx qatori yo'q" ogohlantirishida DEMO-TM-01 bor (100 dona).
- 2-qadam: "N ta tannarx muvaffaqiyatli yaratildi." DEMO-TM-01 qatorida "Material narxi" = **79 380**.
- 3-qadam: yangi qator yaratilmaydi. Xabar "Yangi tannarx yaratilmadi. Norma mavjud mahsulotlar uchun tannarx allaqachon yaratilgan." (yoki tugma umuman ko'rinmaydi). DEMO-TM-01 ro'yxatda bir marta.
- 4-qadam: DEMO-TM-01 ogohlantirishda endi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-TAN-02. "Fakt xarajatlarni yangilash": material FIFO bo'yicha (ombordan chiqim)
**Nima tekshiriladi:**
- Fakt xarajatlar bitta C# engine bilan hisoblanadi.
- Material fakti birinchi navbatda ishlab chiqarish uchun ombordan qilingan FIFO chiqimdan olinadi (PATH0).
- Fakt foyda reja foydaga teng. QQS ikki marta qo'shilmaydi.
(Tannarx commitlari d0b2f6c9, a68659b8; biznes qarori: fakt foyda = reja foyda)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-TAN-01
**Qadamlar:**
1. "Muhandislik → Tannarx → Dashboard" sahifasida Yil 2026, Oktyabr ni tanlang. "Fakt xarajatlarni yangilash" ni bosing.
2. "Muhandislik → Tannarx → Hisobot" (Oktyabr 2026) sahifasida DEMO-TM-01 ning tafsilotini (ProductDetails) oching.
3. "Fakt material narxi hisob izohi" ni o'qing.
4. "EXW narxlari" blokidagi Reja va Fakt ustunlarini solishtiring.
**Kutilgan natija:**
- 1-qadam: "N ta tannarx yozuvi uchun fakt xarajatlar yangilandi." Agar sariq ogohlantirish chiqsa, matnini izohga yozing.
- Material xarajatlari: Reja **79 380**, Fakt **79 380** (7 938 000 so'm ÷ 100 dona; T-MES-12 dagi o'chirilgan 5 dona hisobga kirmaydi).
- Material izohida PATH0 / ombordan chiqim (FIFO) usuli tanlangani yozilgan.
- "E — Foyda": Fakt = Reja.
- "Sotuv narxi EXW QQS siz" fakti = fakt xarajatlar yig'indisi + foyda. "Sotuv narxi QQS bilan EXW" fakti = QQSsiz fakt × 1,12 (QQS bir marta).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-TAN-03. "Oy xarajatlari manbalari" sahifasi: manba = taqsimlangan + qoldiq
**Nima tekshiriladi:** Har bir xarajat elementi bo'yicha oy jami manbasi, tannarxga taqsimlangan summa va taqsimlanmagan qoldiq ko'rinadi. Sahifa hech narsa yozmaydi. "Shu natijani tannarxga yozish" tugmasi Dashboard'dagi tugma bilan bir xil natija beradi (Tannarx commiti 05574fa9).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-TAN-02
**Qadamlar:**
1. Dashboard'dan "Oy xarajatlari manbalari" ni oching (Oktyabr 2026).
2. "Xarajat elementlari: manba → taqsimlangan" jadvalidagi har bir qatorni tekshiring: Material, Mat. tashuvi, A Ish haqi, B Soliq/bank, C Admin, D Boshqa, T.M. tashuvi.
3. "Mahsulotlar (1 dona uchun fakt)" jadvalida DEMO-TM-01 ni toping va "Material algoritmi" ustuni ustiga sichqonchani olib boring.
4. Sahifani yangilang (F5). DEMO-TM-01 ning ProductDetails sahifasidagi fakt summalarini yozib oling.
5. "Shu natijani tannarxga yozish" ni bosing. ProductDetails ni qayta oching.
**Kutilgan natija:**
- Sahifa xatosiz va sezilarli kechikishsiz (3 soniyadan kam) ochiladi.
- Har qatorda "Manbada (oy jami)" = "Tannarxga taqsimlangan" + "Taqsimlanmagan".
- DEMO-TM-01 "Ishlab chiqarilgan" = 100. "Material algoritmi" = PATH0. Tooltipda boshqa urinishlar va sabablar ko'rinadi.
- Sahifani ochish/yangilash tannarxdagi qiymatlarni o'zgartirmaydi.
- 5-qadamdan keyin DEMO-TM-01 fakt qiymatlari T-TAN-02 dagi bilan bir xil (bitta engine).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-TAN-04. Finance kategoriyasidan tannarx guruhi (qo'lda belgilash)
**Nima tekshiriladi:**
- Finance'dagi xarajat kategoriyasi nom qoidasi bo'yicha avtomatik guruhga tushadi ("aniqlanmagan" xarajat → D — Boshqa).
- Qo'lda belgilangan guruh nom qoidasidan ustun turadi.
- Tannarxdan chiqarilgan summa D ga qo'shilmaydi.
(Tannarx commitlari aa8761f7, 4655bddd)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-TAN-03
**Qadamlar:**
1. "Moliya → Boshqaruv → Kategoriyalar" da chiqim turidagi **DEMO Ijara** kategoriyasini yarating.
2. "Moliya → Boshqaruv → Tranzaksiyalar" da mavjud UZS hisobidan chiqim tranzaksiyasi kiriting: 25.10.2026, kategoriya DEMO Ijara, summa **2 000 000 so'm**, izoh "DEMO sex ijarasi".
3. "Oy xarajatlari manbalari" (Oktyabr 2026) sahifasini oching. "Finance kategoriyalari (shu oy)" jadvalida DEMO Ijara ni toping va "D Boshqa" qatoridagi "Manbada (oy jami)" ni yozib oling.
4. Sahifadagi Finance kategoriyalari tugmasini bosib "Finance xarajat kategoriyalari → tannarx guruhi" sahifasini oching. DEMO Ijara uchun "Tannarx guruhi" = **Tannarxga kirmaydi** ni tanlang va "Saqlash" ni bosing.
5. Dashboard'da "Fakt xarajatlarni yangilash" ni bosing. "Oy xarajatlari manbalari" ni qayta oching.
6. DEMO Ijara guruhini "Avtomatik (nom bo'yicha)" ga qaytaring, saqlang va yana "Fakt xarajatlarni yangilash" ni bosing.
**Kutilgan natija:**
- 3-qadam: DEMO Ijara summasi 2 000 000, "Qayerga" = D — Boshqa (avtomatik).
- 4-qadam: "Kategoriya guruhlari saqlandi. Tannarxga ta'sir qilishi uchun oy bo'yicha "Fakt xarajatlarni yangilash" ni bosing."
- 5-qadam: DEMO Ijara "tannarxga kirmaydi" deb ko'rsatilgan va sababi "Qo'lda: tannarxga kirmaydi". "D Boshqa" manbasi 3-qadamdagidan aynan **2 000 000 ga kam**.
- 6-qadamdan keyin "D Boshqa" yana 3-qadamdagi qiymatga qaytadi.
- DEMO-TM-01 ning D fakti taxminan "D jami ÷ oy jami ishlab chiqarilgan dona" ga teng.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-TAN-05. Tannarx analitikasi: Dashboard, Hisobot, ProductDetails (tezlik va moslik)
**Nima tekshiriladi:** Analitika sahifalari tez ochiladi, raqamlar bir-biriga mos keladi va trendlar xatosiz chiqadi (audit ID: MES-P05/P06, Tannarx N+1 tuzatishlari).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-TAN-04
**Qadamlar:**
1. "Muhandislik → Tannarx → Dashboard" (2026, Oktyabr) sahifasini oching va vaqtni kuzating.
2. "Muhandislik → Tannarx → Hisobot" (2026, Oktyabr) sahifasini oching.
3. Hisobotdan DEMO-TM-01 ning ProductDetails sahifasini oching.
4. "Muhandislik → Tannarx → Tannarx" da DEMO-TM-01 oktyabr qatorini "Tahrirlash" orqali oching va hech narsani o'zgartirmasdan saqlang.
**Kutilgan natija:**
- Har bir sahifa 3 soniyadan kam vaqtda ochiladi. "Oylik trend (reja va fakt)" grafigi chiqadi.
- "Umumiy statistika" da fakt ma'lumoti bor mahsulotlar soni noldan katta.
- Hisobotdagi DEMO-TM-01 Material (Reja / Fakt) = 79 380 / 79 380. Bu ProductDetails bilan bir xil.
- 4-qadam: "Tannarx muvaffaqiyatli yangilandi." Brauzer konsolida xato yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

## Ma'lum cheklovlar
- Normada "Miqdori" faqat butun son qabul qiladi. Kasr sarf (0,9 m²) faqat optimizatsiya jadvalining "Sarf" katagi orqali kiritiladi.
- Ishlab chiqarish chiqimi narxi ombor bilan bir xil qoida bo'yicha (StockCostService, SRM-B19): DLOC lot ajratmasi bo'lsa shu lot narxi, qolgani FIFO partiyalari, QQSsiz. Faqat qabul qilingan (RecieveStatus = 1) kirimlar hisobga olinadi. Kirim tarixi to'liq bo'lmasa (qoldiq hujjatsiz kiritilgan), yetishmagan qism eng eski ma'lum narxda olinadi. Avvalgi MES-B11 cheklovi (doim eng eski kirim narxi) tuzatilgan.
- Bitta mahsulotda bitta BOM bor. Ishlab chiqarish har doim eng so'nggi oy normasi bo'yicha sarflanadi, ishlab chiqarish oyining normasi bo'yicha emas.
- Tannarx overhead (B, D) oy jami dona ulushida taqsimlanadi. Aniq summa bazadagi boshqa mahsulotlarga bog'liq, shuning uchun testda faqat "manba = taqsimlangan + qoldiq" tekshiriladi.
- Material tashuvi fakti oyning barcha kirim yo'l varaqalaridan material ulushida taqsimlanadi. Mahsulot bo'yicha aniq raqam boshqa ma'lumotlarga bog'liq.
- Sex (Shop), Ishlab chiqarish jarayoni va Brak sahifalari menyuda yo'q, ular faqat URL orqali ochiladi.
- Lokal bazada MES skriptlari bajarilgan: MES_2026_10_PlanDate_ProductionID (PlanDate va ProductionID ustunlari), MES_Audit_2026_10_Indexes, DataFix_MES_02 va 03. DataFix_MES_01, 02 va 03 topuvchilari endi 0 qator qaytaradi. Faqat 03 da reja 271 (kunlik reja yig'indisi 3, reja miqdori 20) qolgan, chunki kunlik qatorlar qo'lda tahrirlangan. Boshqa bazada (prod) bu skriptlar avtomatik ishga tushmaydi, ularni qo'lda bajarish kerak.
- Faqat UZS'dagi Finance xarajatlari tannarxga olinadi; boshqa valyutalar faqat izohda ko'rsatiladi.
- Mahsulot, kategoriya va reja yozuvlarida o'chirilganni tiklash (Recover) imkoniyati bu bo'limda tekshirilmaydi.
