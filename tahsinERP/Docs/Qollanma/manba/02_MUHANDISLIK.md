# 2. Mahsulot va texnologiya (Muhandislik)

Muhandislik moduli iPack'ning "texnik pasporti": bu yerda har bir mahsulot (iPack P1 "Yog'och taglik", iPack B6.1 "B6.1 quti" va boshqalar) va har bir xomashyo (taxta, karton, mix) kartochkasi, 1 dona tayyor mahsulotga ketadigan xomashyo normasi (Norma-sarf), undan avtomatik quriladigan BOM va mahsulot tannarxi yuritiladi.
Asosiy foydalanuvchilar: **Texnolog**, **Konstruktor** (mahsulot, norma, BOM), **Texnolog+Ishlab chiqarish** roli, tannarx bo'yicha rahbariyat (**Ta'sischi**, direktor) va buxgalter.
Bog'liq modullar: Ta'minot (xarid faqat xomashyo kategoriyalari bo'yicha), Ombor (norma narxi oxirgi kirimdan olinadi), Ishlab chiqarish (kunlik ishlab chiqarishda xomashyo BOM bo'yicha chiqim qilinadi), Sotuv (sotuv shartnomasida faqat tayyor mahsulotlar), Moliya va HR (fakt tannarxga xarajatlar va ish haqi kiradi).

## 2.1 Asosiy tushunchalar

- **Mahsulot turi (kategoriya)** — mahsulotlar guruhi, masalan "Yog'och", "Karton", "Metiz", "Tayyor qadoq". Kategoriyadagi belgilar (bayroqlar) tizim xatti-harakatiga ta'sir qiladi: masalan "Kesish talab qiladi" belgisi bor kategoriyadagi taxta normada metrda hisoblanadi.
- **Mahsulot (PNo)** — har bir tovar yoki xomashyoning kartochkasi. "Maxsulot raqami" (PNo) takrorlanmas kod. Masalan, `iPack P1` — tayyor mahsulot, `Yog'och 100*50*6000` — xomashyo.
- **Turi** — mahsulot kartochkasida: "Maxsulot" (tayyor), "Yarim tayyor maxsulot" yoki "Xomashyo". Norma, BOM, reja va sotuv ro'yxatlarida faqat "Maxsulot" turidagilar chiqadi.
- **Standart uzunlik** — kesiladigan xomashyoning to'liq uzunligi (mm). "Yog'och 100*50*6000" uchun 6000 mm. Bundan chiqindi va kerakli taxta soni hisoblanadi.
- **Norma (Norma-sarf)** — 1 dona tayyor mahsulotga qaysi xomashyodan qancha ketishi, chiqindisi va summasi. Norma **oy bo'yicha versiyalanadi**: "2026-10", "2026-11" va h.k.
- **Kesim** — taxtadan kesib olinadigan detal: uzunligi (mm) va 1 mahsulotdagi soni. Masalan, taglik uchun 1200 mm × 5 dona.
- **Chiqindi** — taxtadan detallar kesilgandan keyin qoladigan qoldiq (m). Narxi tannarxga qo'shiladi.
- **Optimizatsiya** — uzun detallar kesilgandan keyin qolgan qoldiqdan kalta detallarni olish. Shunda kalta detal uchun qo'shimcha taxta kerak bo'lmaydi.
- **BOM (Bill of Materials)** — mahsulotning material tarkibi. iPack'da BOM qo'lda yozilmaydi: u **eng so'nggi oy normasidan avtomatik quriladi**. Ishlab chiqarishda ombordan xomashyo shu BOM bo'yicha chiqim qilinadi.
- **Reja tannarx** — oy normasi, ishbay tarif va standart foizlar bo'yicha hisoblangan tannarx va sotuv narxi (EXW, CIP).
- **Fakt tannarx** — oy yakunida haqiqiy ma'lumotlardan hisoblangan tannarx: ombordan FIFO chiqim, ish haqi, transport, Moliya xarajatlari.
- **EXW / CIP** — EXW: zavoddan olib ketish narxi. CIP: mijozgacha yetkazilgan narx (EXW + tayyor mahsulot tashuvi). Ikkalasi ham QQSsiz va QQS (12%) bilan ko'rsatiladi.

## 2.2 Mahsulot turlari (kategoriyalar)

**Kim:** Texnolog, Admin   **Menyu:** Muhandislik → Maxsulotlar → Maxsulot turlari

Ro'yxatda har bir tur uchun "Kod", "Nomi" ustunlari va "Xomashyo", "Tayyor maxsulot", "Yarim tayyor", "Sarflanish" belgilari ko'rinadi. Amallar: ko'rish, tahrirlash, o'chirish.

1. "Yangi maxsulot turi qo'shish" tugmasini bosing.
2. "Nomi" maydoniga **Yog'och** deb yozing. "Kod" maydonini bo'sh qoldiring: u avtomatik beriladi (5 xonali: 00000, 00001, ...; oxirgi koddan +1).
3. "Xomashyo guruhi" maydoniga **Yog'och** yozing (ixtiyoriy, lekin tavsiya etiladi — pastdagi "Diqqat"ga qarang).
4. "Xususiyatlar" bo'limida belgilang: **Xomashyo**, **Kesish talab qiladi**, **MRP ga kiritish**, **Xarid qilishga ruxsat**.
5. "Saqlash" ni bosing. Tur ro'yxatda avtomatik kod bilan paydo bo'ladi.
6. Xuddi shunday quyidagi turlarni yarating.

| Tur (iPack) | Belgilanadigan xususiyatlar | Izoh |
|---|---|---|
| Yog'och | Xomashyo, Kesish talab qiladi, MRP ga kiritish, Xarid qilishga ruxsat | Taxtalar, normada metrda hisoblanadi |
| Karton | Xomashyo, MRP ga kiritish, Xarid qilishga ruxsat | m² da hisoblanadi, kesish belgisi qo'yilmaydi |
| Selofan | Xomashyo, Xarid qilishga ruxsat | |
| Metiz | Xomashyo, Sarflanish, Xarid qilishga ruxsat | Mix, shurup, skoba |
| Jihoz | Sarflanish, Xarid qilishga ruxsat | Asbob-uskuna, xomashyo emas |
| Tayyor qadoq | Tayyor maxsulot, NORM talab qiladi, Sotishga ruxsat | iPack P1, B6.1 va boshqalar |

| Maydon / belgi | Nima kiritiladi | Tizimga ta'siri |
|---|---|---|
| Kod | Avtomatik | 5 xonali takrorlanmas kod |
| Nomi * | Tur nomi | Nomida "yog'och" so'zi bo'lsa, tur yog'och deb hisoblanadi |
| Xomashyo guruhi / kichik guruhi | Guruh nomi | "Yog'och" yozilsa, tur yog'och deb hisoblanadi |
| Xomashyo | Belgi | Ta'minotda xarid uchun mahsulot tanlashda chiqadi |
| Tayyor maxsulot | Belgi | Bu turdagi mahsulotlar xarid ro'yxatlarida chiqmaydi |
| Yarim tayyor | Belgi | Tasniflash uchun |
| Sarflanish | Belgi | Xarid ro'yxatlarida chiqadi (sarf materiallari) |
| NORM talab qiladi | Belgi | Tasniflash uchun (norma kiritilishi kerak bo'lgan tur) |
| Kesish talab qiladi | Belgi | Normada uzunlik, standart uzunlik, chiqindi va optimizatsiya ishlaydi; BOM'da taxta soni butun songa yaxlitlanadi |
| MRP ga kiritish | Belgi | Tasniflash uchun |
| Xarid qilishga ruxsat | Belgi | Ta'minotda xarid uchun mahsulot tanlashda chiqadi |
| Sotishga ruxsat | Belgi | Tasniflash uchun |
| Hisob-kitoblar og'irlikka nisbatan qilinsin | Belgi | BOM'da donada, lekin xarid, kirim, chiqim va shartnoma qoldig'ida kg da yuritiladigan materiallar uchun (mix, shurup, bolt) |

> **Diqqat:** Tizim taxtani "yog'och" deb ikki belgi bo'yicha taniydi: tur nomida yoki "Xomashyo guruhi"da "yog'och" so'zi bo'lishi va "Kesish talab qiladi" belgisi. Yog'och deb tanilgan materialning kirim narxi (so'm/dona) normada avtomatik so'm/metrga aylantiriladi va BOM'da taxta soni butun songa yaxlitlanadi. Karton, selofan kabi m² yoki kg da hisoblanadigan materiallar turiga "Kesish talab qiladi" belgisini qo'ymang va nomiga "yog'och" so'zini yozmang.

> **Diqqat:** Mix va shurupni kg da sotib olsangiz, normada esa donada yozsangiz, "Metiz" turiga "Hisob-kitoblar og'irlikka nisbatan qilinsin" belgisini qo'ying va mahsulot kartochkasida "Og'irlik (kg da)" ni to'ldiring. Aks holda ombordan chiqim noto'g'ri birlikda bo'ladi.

**Natija:** Tur saqlanadi. Kategoriyani o'chirish yumshoq (yozuv bazada qoladi, eski hujjatlarda nomi bilan ko'rinadi), lekin yangi mahsulotlar uchun ro'yxatda chiqmaydi.

## 2.3 Mahsulotlar

**Kim:** Texnolog, Konstruktor, Admin   **Menyu:** Muhandislik → Maxsulotlar → Barcha (yoki "Tayyor maxsulot", "Yarim tayyor", "Xomashyo")

Ro'yxat ustunlari: "Maxsulot raqami", "Turi", "Nomi", "O'lchamlari, mm (U×E×B)", "m³ (CBM)", "O'lchov birligi", "Og'irligi", "Amallar". Yuqorida tur filtri va "Excel" eksport tugmasi bor. Menyudagi "Tayyor maxsulot", "Yarim tayyor", "Xomashyo" bandlari shu ro'yxatni tur bo'yicha filtrlab ochadi.

### 2.3.1 Yangi mahsulot yaratish

1. "Yangi maxsulot" tugmasini bosing.
2. Tayyor mahsulot uchun misol — **iPack P1**:
   - "Maxsulot raqami" = **iPack P1**, "Maxsulot nomi" = **Yog'och taglik**;
   - "Turi" = **Maxsulot**; "Kategoriya" = **Tayyor qadoq**;
   - "Sex (ishlab chiqarish bo'limi)" = **Ishlab chiqarish**;
   - "O'lchamlari, mm (UxExB)" = 1200 × 800 × 144; "O'lchov birligi" = dona;
   - rasm (ixtiyoriy, 2 MB gacha).
3. "Saqlash" ni bosing.
4. Xomashyo uchun misol — **Yog'och 100*50*6000**:
   - "Turi" = **Xomashyo**; "Kategoriya" = **Yog'och**;
   - "O'lchamlari" = 6000 × 100 × 50; "Standart uzunlik (kesish uchun), mm" = **6000**; "O'lchov birligi" = dona.
5. Karton uchun: **Karton_2090x1370**, Turi = Xomashyo, Kategoriya = Karton, "O'lchov birligi" = m². Standart uzunlik kiritilmaydi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Maxsulot raqami | Takrorlanmas kod (PNo) | iPack P1, iPack B6.1, Yog'och 100*50*6000 |
| Maxsulot nomi | To'liq nom | Yog'och taglik |
| Turi | Maxsulot / Yarim tayyor maxsulot / Xomashyo | P1 — Maxsulot; taxta — Xomashyo |
| Kategoriya | Mahsulot turi (2.2) | Tayyor qadoq, Yog'och, Karton, Metiz |
| Sex (ishlab chiqarish bo'limi) | Mahsulot ishlab chiqariladigan sex | Ishlab chiqarish |
| Og'irlik (kg da) | 1 dona og'irligi | Taglik 18 kg; mix 1 dona 0,004 kg |
| O'lchamlari, mm (UxExB) | Uzunlik × eni × balandlik | 1200 × 800 × 144 |
| Standart uzunlik (kesish uchun), mm | Faqat kesiladigan xomashyo uchun | 6000 |
| O'lchov birligi | dona, m², kg, m | Taxta — dona, karton — m² |
| HSCODES | TN VED kodi (ixtiyoriy) | — |
| Tavsifi | Erkin matn | "EUR o'lchamli taglik" |
| Alternativ maxsulot raqamlari | Mijoz yoki ta'minotchidagi boshqa kodlar (PNo2–PNo4) | UzAutoMotors Powertrain ichki kodi |

> **Diqqat:** Tayyor mahsulotning "Sex" maydoni majburiy ma'noga ega: kunlik ishlab chiqarishda sex tanlanganda faqat shu sexga biriktirilgan mahsulotlar rejasi yuklanadi. iPack'da barcha tayyor mahsulotlar "Ishlab chiqarish" sexiga biriktiriladi.

> **Diqqat:** "Standart uzunlik" bo'sh bo'lsa, tizim "O'lchamlari" dagi uzunlikni oladi. Ikkalasi ham bo'lmasa, normada chiqindi hisoblanmaydi va "Standart uzunlik yo'q — optimizatsiya qo'llanilmadi." izohi chiqadi.

**Natija:** Mahsulot ro'yxatga qo'shiladi. Tayyor mahsulot Norma-sarf, Ishlab chiqarish rejasi va sotuv shartnomasi ro'yxatlarida, xomashyo esa Ta'minot va Norma material qidiruvida paydo bo'ladi.

### 2.3.2 Ko'rish, tahrirlash, o'chirish, rasm

- **Ko'rish** — kartochka va rasm. Rasm sahifaga kichraytirilgan holda yuklanadi.
- **Tahrirlash** — barcha maydonlar o'zgaradi. Yangi rasm yuklansa, eskisi almashtiriladi.
- **O'chirish** — tasdiqlash sahifasidan keyin mahsulot yumshoq o'chiriladi: ro'yxatlardan yo'qoladi, lekin eski hujjatlarda (kirim, invoys, ishlab chiqarish) nomi bilan ko'rinib turadi.

> **Maslahat:** Mahsulotni o'chirish o'rniga uni tahrirlab nomiga "(ishlatilmaydi)" qo'shish xavfsizroq: eski normalar va BOM'lar buzilmaydi.

### 2.3.3 Excel orqali ommaviy yuklash

**Menyu:** Muhandislik → Maxsulotlar → Barcha → "Excel orqali maxsulot qo'shish"

1. "Shablon faylni yuklab oling" havolasidan `maxsulot.xlsx` shablonini oling.
2. Ustunlarni to'ldiring: **Partnumber** (majburiy), **Name**, **Type** ("XOMASHYO", "YARIM TAYYOR" yoki bo'sh = Maxsulot), **Weight**, **Length**, **Width**, **Height**, **PNo2**, **PNo3**, **PNo4**.
3. Faylni (.xlsx) tanlab "Excel orqali yuklash" ni bosing. Sahifada fayl jadvali va bazada allaqachon bor PNo'lar soni ko'rsatiladi.
4. Tekshirib, saqlang. Xabar: "N ta maxsulot muvaffaqiyatli saqlandi".

> **Diqqat:** Excel'dan yuklanganda bazada bor PNo va fayl ichida takrorlangan PNo o'tkazib yuboriladi. Birlik standart (1-birlik), sex esa birinchi sex bo'lib qo'yiladi, **kategoriya qo'yilmaydi**. Yuklagandan keyin har bir mahsulotni tahrirlab kategoriya, birlik, sex va standart uzunlikni to'g'rilang.

## 2.4 Norma-sarf (yog'och kesish va material normasi)

**Kim:** Texnolog, Konstruktor   **Menyu:** Muhandislik → BOM va norma → Norma-sarf

Norma — iPack'dagi eng muhim texnik hujjat. Ishlab chiqarishda ombordan xomashyo chiqimi, reja tannarx va xarid hisob-kitobi shu normaga tayanadi.

### 2.4.1 Sahifalar tuzilishi

| Sahifa | Nima ko'rinadi | Amallar |
|---|---|---|
| Norma-sarf (ro'yxat) | Barcha tayyor mahsulotlar: "Mahsulot raqami", "Mahsulot nomi", "Normalar soni" | Mahsulot normalari, "Yangi NORM" |
| Mahsulot normalari | Shu mahsulotning oy versiyalari: "Norma oyi", "Umumiy sarf bo'yicha summa", "Umumiy chiqindi bo'yicha summa", "Umumiy summa" | "Yangi NORMA qo'shish", Ko'rish, Tahrirlash, O'chirish |
| Norma kiritish (yaratish/tahrirlash) | Mahsulot, oy, kesish variantlari jadvali, jami hisoblar | Qator qo'shish, Sarf optimizatsiyasi, Saqlash va tasdiqlash |
| Ko'rish | Saqlangan norma (faqat o'qish) | — |

### 2.4.2 Norma formasi maydonlari

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Oy | Norma amal qila boshlaydigan oy | Noyabr 2026 |
| Oldingi oylardagi normadan yuklash | Oldingi oy normasini andoza sifatida tanlash + "Yuklash" | 2026-10 |
| Samarali shu sanadan / shu sanagacha | Amal qilish oralig'i. "gacha" bo'sh bo'lsa, keyingi oylarga ham amal qiladi | 01.11.2026 / bo'sh |
| Izoh | Erkin matn | "Yangi kesish xaritasi" |
| material narxlarini ombor avtomatik tortish | Belgilansa, narx materialning oxirgi kirim narxidan olinadi va qo'lda o'zgartirilmaydi | O'chiq (narxni o'zingiz kiritasiz) |

Kesish variantlari jadvali (har bir qator — bitta material yoki bitta kesim):

| Ustun | Ma'nosi |
|---|---|
| Material / Material kodi | Xomashyo. Kod yoki nomning bir qismini yozing, ro'yxat serverdan 30 tadan sahifalab chiqadi |
| Uzunlik (mm) | Detal (kesim) uzunligi — faqat kesiladigan material uchun |
| Standart uzunlik (mm) | Material kartochkasidan avtomatik |
| Miqdori | 1 mahsulotdagi detal soni yoki dona soni. **Faqat butun son** |
| Birlik | dona, m, m², kg |
| Sarf (m) | Kesiladigan materialda: uzunlik/1000 × miqdor. Boshqalarida: miqdor |
| Chiqindi (m) | Kesiladigan materialda: kerakli taxtalar soni × standart uzunlik − kesimlar jami. Qo'lda o'zgartirish mumkin |
| Narx | Kesiladigan yog'ochda so'm/m, boshqalarida birlik narxi |
| Toza sarf summa / Chiqindi summa / Jami narx | Sarf × narx, chiqindi × narx, ularning yig'indisi |
| Kesish usuli | Izoh (masalan, "5 ta 1200 bitta taxtadan") |

Jami hisoblar: "Umumiy norma" va "Umumiy chiqindi (m)" — faqat kesiladigan materiallar uchun; "Umumiy toza sarf summa", "Umumiy chiqindi summa", "Umumiy summa" — barcha qatorlar uchun.

### 2.4.3 Misol: iPack P1 "Yog'och taglik" normasi (Noyabr 2026)

Quyidagi raqamlar **misol** sifatida olingan (haqiqiy chizmangiz bo'yicha o'zgartiring). Taxta "Yog'och 100*50*6000" Wood Nam Import'dan 54 000 so'm/dona narxda kelgan. Standart uzunlik 6000 mm bo'lgani uchun tizim narxni 54 000 / 6 = **9 000 so'm/m** ga aylantiradi.

1. "Norma-sarf" ro'yxatida **iPack P1** qatorida "Yangi NORM" ni bosing.
2. "Oy" = **Noyabr 2026**. Narxni o'zingiz kiritasiz, shuning uchun "material narxlarini ombor avtomatik tortish" belgisini qo'ymang. Belgini qo'ysangiz, narx oxirgi kirimdan avtomatik olinadi.
3. "Qator qo'shish" → Material = **Yog'och 100*50*6000**, "Uzunlik (mm)" = **1200**, "Miqdori" = **5** (ustki taxtalar), "Birlik" = m, "Narx" = **9000**.
   Tizim hisoblaydi: sarf 6,0 m; 5 × 1200 = 6000 mm bitta taxtaga to'liq sig'adi, chiqindi **0**.
4. "Qator qo'shish" → shu material, "Uzunlik (mm)" = **800**, "Miqdori" = **3** (ko'ndalang taxtalar), "Narx" = 9000.
   Sarf 2,4 m; 3 × 800 = 2400 mm uchun 1 ta taxta ochiladi, chiqindi 6000 − 2400 = **3,6 m**.
5. "Qator qo'shish" → Material = **Mix 80 mm** (Metiz), "Miqdori" = **36**, "Birlik" = dona, "Narx" = **50**.
6. "Sarf optimizatsiyasi (chiqindidan foydalanish)" tugmasini bosing va "Optimizatsiya qilingan pozitsiyalar" jadvalini tekshiring (2.4.4).
7. "Saqlash va tasdiqlash" ni bosing. Xabar: "NORM muvaffaqiyatli saqlandi." va BOM haqida izoh.

| Qator | Sarf | Chiqindi | Narx | Jami narx |
|---|---|---|---|---|
| Yog'och 100*50*6000, 1200 mm × 5 | 6,0 m | 0 m | 9 000 | 54 000 |
| Yog'och 100*50*6000, 800 mm × 3 | 2,4 m | 3,6 m | 9 000 | 54 000 |
| Mix 80 mm | 36 dona | — | 50 | 1 800 |
| **Umumiy summa** | | | | **109 800 so'm** |

Natijada BOM'ga quyidagilar yoziladi: taxta = ⌈(6000 + 0 + 2400 + 3600) / 6000⌉ = **2 dona** taxta/taglik, mix = 36 dona/taglik. 1 200 taglik uchun 2 400 dona taxta va 43 200 dona mix kerak bo'ladi.

**Misol: iPack B6.1 quti normasi.** Karton_2090x1370 (m², kesilmaydigan): sarf **0,62 m²**, narx 6 500 so'm/m² → 4 030 so'm. Skoba (Metiz): 4 dona × 15 = 60 so'm. Umumiy summa **4 090 so'm/dona**.

> **Diqqat:** "Miqdori" faqat butun son qabul qiladi. Karton kabi kasr sarfni (0,62 m²) kiritish uchun avval qatorni "Miqdori" = 1 bilan qo'shing. Keyin "Sarf optimizatsiyasi" ni bosib, "Optimizatsiya qilingan pozitsiyalar" jadvalidagi "Sarf" katagiga **0.62** yozing va saqlang. Shu jadvaldagi qiymatlar saqlanadi.

### 2.4.4 Sarf optimizatsiyasi qanday ishlaydi

"Sarf optimizatsiyasi (chiqindidan foydalanish)" tugmasi har bir kesiladigan material bo'yicha alohida ishlaydi:

1. Kesimlar uzunligi bo'yicha kamayish tartibida saralanadi: uzun detal chiqindi **manbai**, kalta detal chiqindidan **oluvchi**.
2. Optimizatsiya har bir uzun detalni alohida taxtadan kesiladi deb hisoblaydi va bitta taxtadan qoladigan qoldiqni (standart − kesim) topadi.
3. Kalta detal ikki shart bajarilsa chiqindidan olinadi: bitta qoldiq bo'lagiga sig'adi va manbaning qolgan jami chiqindisi yetadi. Shunday qatorda miqdor va sarf 0 ga tushadi. Izohga "...mm × N dona K-qatordagi ...mm kesim chiqindisidan olinadi. Sarf 0 qilindi." yoziladi.
4. Manba qatorga "Chiqindidan ...mm boshqa kalta kesimlarga ishlatildi. Qolgan chiqindi ...mm." izohi qo'yiladi.
5. Kesilmaydigan materiallar (karton, mix) uchun "Kesilmaydigan material — optimizatsiya qo'llanilmadi." izohi chiqadi.
6. Yuqorida jami: "Optimizatsiya: N ta kesim chiqindidan olindi (sarf 0). Umumiy norma ... m, umumiy chiqindi ... m, umumiy summa ...".

> **Diqqat:** Optimizatsiya — taklif. U har bir uzun detalni alohida taxtadan kesilgan deb hisoblagani uchun bir taxtadan bir nechta detal kesiladigan holatda (5 × 1200 = 6000) chiqindini oshirib ko'rsatishi mumkin. "Saqlash va tasdiqlash" dan oldin "Optimizatsiya qilingan pozitsiyalar" jadvalidagi "Sarf" va "Chiqindi" ni sexdagi haqiqiy kesish xaritasi bilan solishtiring va kerak bo'lsa qo'lda to'g'rilang. Masalan, 1200 mm qatori uchun chiqindi 0, 800 mm qatori uchun 3,6 m bo'lishi kerak.

> **Maslahat:** "Kesish usuli" ustuniga kesish xaritasini qisqa yozib qo'ying ("1-taxta: 5×1200; 2-taxta: 3×800, qoldiq 3600"). Keyingi oy normasini tekshirishda va yangi texnologni o'rgatishda juda qo'l keladi.

### 2.4.5 Norma versiyalari (oylar) va BOM bilan bog'lanish

- Bitta mahsulot uchun bitta oyda **faqat bitta** norma bo'ladi. Shu oy uchun ikkinchisini yaratmoqchi bo'lsangiz yoki tahrirlashda oyni band oyga o'zgartirsangiz: "Bu mahsulot uchun 2026-10 oyi uchun norm allaqachon mavjud. Iltimos, mavjud normni tahrirlang yoki boshqa oy tanlang."
- Yangi oy normasini tez tuzish: "Yangi NORMA qo'shish" → "Oy" = yangi oy → "Oldingi oylardagi normadan yuklash" ro'yxatidan oldingi oyni tanlab "Yuklash". Qatorlar ko'chiriladi, faqat o'zgarganini tahrirlaysiz (masalan, taxta narxi 54 000 dan 56 000 ga oshdi).
- Normaning alohida tasdiqlash bosqichi yo'q: **"Saqlash va tasdiqlash" bosilganda norma darhol amalga kiradi**.
- **BOM har doim eng so'nggi oy normasidan quriladi:**
  - eng so'nggi oy normasi saqlansa: "BOM eng so'nggi norma (2026-11) bo'yicha yangilandi.";
  - eski oy normasi tahrirlansa: "BOM o'zgarmadi: u eng so'nggi norma (2026-11) bo'yicha qoladi.";
  - eng so'nggi norma o'chirilsa, BOM qolgan eng so'nggi normadan qayta quriladi;
  - mahsulotda birorta norma qolmasa: "Mahsulotda norma qolmadi — BOM faolsizlantirildi."
- **O'chirish:** mahsulot normalari ro'yxatida "O'chirish" → tasdiqlash. Xabar: "NORM muvaffaqiyatli o'chirildi."

> **Diqqat:** Ishlab chiqarishda xomashyo har doim **eng so'nggi** oy normasi (BOM) bo'yicha chiqim qilinadi, ishlab chiqarish oyining normasi bo'yicha emas. Masalan, 25 noyabrda dekabr normasini kiritsangiz, 26 noyabrdagi ishlab chiqarish ham dekabr normasi bo'yicha chiqim qilinadi. Kelgusi oy normasini oy boshida kiriting.

> **Diqqat:** Reja tannarx esa **o'sha oyda amal qilayotgan** norma bo'yicha hisoblanadi ("Samarali shu sanadan" ≤ oy, "shu sanagacha" bo'sh yoki ≥ oy). Shuning uchun oktyabr normasi "gacha" sanasiz bo'lsa, u noyabr tannarxi uchun ham ishlaydi.

**Natija:** Norma saqlanadi, "Normalar soni" oshadi, BOM yangilanadi. Ishlab chiqarishdagi xomashyo chiqimi va reja tannarx shu normaga tayanadi.

## 2.5 BOM (mahsulot tarkibi)

**Kim:** Texnolog, Ishlab chiqarish boshlig'i, Xaridchi (ko'rish)   **Menyu:** Muhandislik → BOM va norma → BOM

BOM sahifasi **faqat ko'rish uchun**: unda yaratish yoki tahrirlash tugmasi yo'q, BOM normadan avtomatik quriladi.

- **Ro'yxat:** normasi va faol BOM'i bor tayyor mahsulotlar. Ustunlar: mahsulot raqami, nomi, "Norm versiyasi" (masalan, 2026-11).
- **Tafsilotlar:**
  - "Mahsulot ma'lumotlari" — raqam, nom, "Norm versiyasi", rasm;
  - "BOM Strukturasi" — daraxt. Komponentning o'z BOM'i bo'lsa (yarim tayyor mahsulot), u ham ochiladi;
  - "Materiallar bo'yicha sarf va summa" — "Material kodi", "Material nomi", "Xomashyo sarfi", "O'lchov birligi", "Narxi", "Xomashyo summasi", "Jami".

iPack P1 uchun (2.4.3 misoli) jadvalda: Yog'och 100*50*6000 — sarf 2 dona taxta (yog'och yaxlit taxta sonida ko'rsatiladi); Mix 80 mm — 36 dona. "Jami" norma summasiga yaqin bo'ladi.

> **Maslahat:** Xaridchi katta buyurtma uchun xomashyo hajmini BOM'dan hisoblaydi: 1 200 taglik × 2 = 2 400 dona taxta, 3 000 quti × 0,62 = 1 860 m² karton.

## 2.6 Ishlab chiqarish ma'lumotnomalari: Sex, jarayonlar, nuqson turlari

| Ma'lumotnoma | Qayerda | Maydonlar | iPack misoli |
|---|---|---|---|
| Sex | `/Shop` manzili (menyuda yo'q) | "TSex nomi" | Ishlab chiqarish |
| Ishlab chiqarish jarayonlari | `/ProductionProcess` manzili (menyuda yo'q) | "Process nomi", "Batafsil" | Kesish, Yig'ish, Qadoqlash |
| Nuqson turlari | Ishlab chiqarish → Nuqson turlari | "Nuqson turi", "Batafsil" | Yorilgan taxta, Mix chiqib qolgan, Karton ezilgan, O'lcham xatosi |

Har birida yaratish, tahrirlash va o'chirish odatdagidek bajariladi. O'chirilgan yozuv ro'yxatdan yo'qoladi, eski hujjatlarda nomi bilan qoladi. Nuqson turi sifat nazoratida va brak yozuvida tanlanadi (5-bob).

> **Maslahat:** Nuqson turlari ro'yxatini sifat nazorati boshlanishidan oldin to'ldiring. "Sifat oylik" hisobotidagi "Eng ko'p uchraydigan nuqson turlari" faqat aniq nomlangan turlar bilan foydali bo'ladi.

## 2.7 Reja tannarx

**Kim:** Texnolog, direktor, Ta'sischi, buxgalter   **Menyu:** Muhandislik → Tannarx → Tannarx

### 2.7.1 Oy tannarxini shakllantirish

1. "Yil" = 2026, "Oy" = Noyabr ni tanlang.
2. "Tannarx shakllantirish" ni bosing. Shu oyda amal qiluvchi normasi bor har bir tayyor mahsulot uchun bitta tannarx qatori yaratiladi. Xabar: "N ta tannarx muvaffaqiyatli yaratildi."
3. Qayta bossangiz, dublikat yaratilmaydi: "Yangi tannarx yaratilmadi. Norma mavjud mahsulotlar uchun tannarx allaqachon yaratilgan." Normasi yo'q oyda: "Ushbu oy uchun norma kiritilgan mahsulot topilmadi."

Reja tannarx qanday hisoblanadi:

| Element | Qayerdan olinadi | iPack P1 (misol) |
|---|---|---|
| Material narxi | Oy normasining "Umumiy summa"si | 109 800 |
| Material tashuvi | Material narxining 2% | 2 196 |
| A — Ish haqi | HR'dagi ishbay tariflar yig'indisi (mahsulot bo'yicha, faol) | 12 000 |
| B — Soliq va bank | 5% (standart) | 6 200 |
| C — Administrativ | 5% | 6 200 |
| D — Boshqa | 5% | 6 200 |
| E — Foyda | 10% | 12 400 |
| Sotuv narxi EXW QQSsiz | Yig'indi | ~154 995 |
| Sotuv narxi QQS bilan EXW | × 1,12 | ~173 594 |
| CIP | EXW + tayyor mahsulot tashuvi (kiritilgandan keyin) | — |

Birinchi shakllantirishda foizlar (material + tashuv + ish haqi) asosidan olinadi: 123 996 × 5% ≈ 6 200.

### 2.7.2 Tannarxni tahrirlash

1. Ro'yxatda iPack P1 qatorida "Tahrirlash" ni bosing.
2. O'zgartirish mumkin bo'lgan maydonlar: ish haqi, B/C/D foizlari, foyda foizi, "Belgilangan narxi QQS bilan EXW", "Tayyor maxsulot tashuvi", "Belgilangan QQS bilan CIP".
3. Material narxi qo'lda tahrirlanmaydi. Norma o'zgargan bo'lsa, "Shu oydagi normadan material narxini yangilash" tugmasini bosing: "Material narxi normadan yangilandi."
4. Saqlang: "Tannarx muvaffaqiyatli yangilandi."

**Misol:** UzAutoMotors Powertrain bilan kelishilgan narx 185 000 so'm (QQSsiz). "Belgilangan narxi QQS bilan EXW" = **207 200** (185 000 × 1,12) kiriting. Tizim B, C, D va foydani endi shu narxning QQSsiz qismidan qayta hisoblaydi: B = C = D = 9 250, E = 18 500. EXW QQSsiz = 109 800 + 2 196 + 12 000 + 3 × 9 250 + 18 500 = **170 246**. Osiyo Trans Kargo tashuvi 1 taglikka 3 000 so'm bo'lsa, "Tayyor maxsulot tashuvi" = 3 000 kiriting: CIP QQSsiz = 173 246, QQS bilan = 194 035,5.

> **Diqqat:** Ro'yxat sahifasi ustun nomlarida harflar boshqacha ("D Foyda", "C Boshqa xarajatlar"). Tahlil sahifalarida esa A — Ish haqi, B — Soliq va bank, C — Administrativ, D — Boshqa, E — Foyda. Ma'nosi bir xil, faqat harflar farq qiladi.

**O'chirish:** "O'chirish" → tasdiqlash → "Tannarx muvaffaqiyatli o'chirildi." Keyin shu oy uchun "Tannarx shakllantirish" ni yana bosib, qatorni qayta yaratish mumkin.

## 2.8 Fakt tannarx

**Kim:** buxgalter, direktor   **Menyu:** Muhandislik → Tannarx → Dashboard (yoki Hisobot) → "Fakt xarajatlarni yangilash"

Oy yakunlangach (barcha kunlik ishlab chiqarish, kirimlar, yo'l varaqalari, ish haqi va Moliya tranzaksiyalari kiritilgandan keyin) fakt tannarx hisoblanadi.

1. Dashboard'da "Yil" = 2026, oy = Noyabr ni tanlang.
2. "Fakt xarajatlarni yangilash" ni bosing. Xabar: "N ta tannarx yozuvi uchun fakt xarajatlar yangilandi." Sariq ogohlantirish chiqsa, uni o'qing: odatda ma'lumot yetishmayotgani haqida bo'ladi.

Fakt elementlar qanday hisoblanadi:

| Element | Manba | Qanday taqsimlanadi |
|---|---|---|
| Material | 1-navbat (PATH0): ishlab chiqarish uchun ombordan avtomatik qilingan xomashyo chiqimi, FIFO lot narxida (QQSsiz). Bu yo'l ishlamasa, navbat bilan: sotuv buyurtmasi → xarid buyurtmasi → kirim (PATH2), → xarid invoysi (PATH1), → Moliya "Material" kategoriyasi xarajatlari (PATH3), oxirida reja | Mahsulotning oy chiqim summasi ÷ ishlab chiqarilgan dona |
| Material tashuvi | Oyning kirim yo'l varaqalari (sotuvga bog'lanmagan, ichki emas) | Mahsulotlarning material summasi ulushida |
| A — Ish haqi | HR ishbay tabellari (mahsulot bo'yicha) | Mahsulot summasi ÷ dona |
| C — Administrativ | Tasdiqlangan soatbay ish haqi. Tasdiqlangan oylik bo'lmasa: tabel soatlari × (oklad / 160) | Oy jami ÷ oyda ishlab chiqarilgan jami dona |
| B — Soliq va bank | Moliya: nomida "soliq"/"bank" bo'lgan xarajat kategoriyalari | Oy jami dona ulushida |
| D — Boshqa | Moliya: qolgan, boshqa joyda hisoblanmagan xarajatlar | Oy jami dona ulushida |
| Tayyor mahsulot tashuvi | Sotuv invoyslariga bog'langan yo'l varaqalari | Mahsulot bo'yicha |
| E — Foyda | Reja foyda | Fakt = reja |

**Misol (noyabr):** iPack P1 bo'yicha oyda 1 200 dona ishlab chiqarilgan. Ishlab chiqarish ombordan 2 400 dona taxtani FIFO bo'yicha chiqim qilgan: 1 600 dona 54 000 dan, 800 dona 56 000 dan, jami 131 200 000 so'm. Mix 43 200 × 50 = 2 160 000 so'm. Fakt material = 133 360 000 / 1 200 = **111 133 so'm/dona**, reja 109 800. Farq taxtaning yangi partiyasi qimmatroq kelgani bilan izohlanadi.

> **Diqqat:** Faqat UZS'dagi Moliya xarajatlari tannarxga olinadi; boshqa valyutadagilari faqat izohda ko'rsatiladi. "Fakt xarajatlarni yangilash" ni oy yopilgandan keyin bosing. Kech kiritilgan har qanday hujjatdan keyin uni qayta bosish mumkin: natija qayta hisoblanadi, dublikat bo'lmaydi.

### 2.8.1 "Oy xarajatlari manbalari" sahifasi

**Menyu:** Tannarx → Dashboard → "Oy xarajatlari manbalari"

Bu sahifa hech narsani yozmaydi, faqat hisob qanday chiqqanini ko'rsatadi:

- **"Xarajat elementlari: manba → taqsimlangan"** — har element (Material, Mat. tashuvi, A Ish haqi, B Soliq/bank, C Admin, D Boshqa, T.M. tashuvi) uchun "Manbada (oy jami)", "Tannarxga taqsimlangan", "Taqsimlanmagan", "Izoh". Har doim **manba = taqsimlangan + taqsimlanmagan**.
- **Mahsulotlar (1 dona uchun fakt)** — "Ishlab chiq.", "Material algoritmi" (PATH0/1/2/3), har element va "Jami xarajat". "Material algoritmi" ustiga sichqonchani olib borsangiz, barcha urinishlar va rad etilish sabablari ko'rinadi.
- **Finance kategoriyalari (shu oy)** — har xarajat kategoriyasi: "Qayerga", "Sabab", "Tranzaksiya", "Summa (chiqim − qaytish)".
- **"Ishlab chiqarilgan, lekin tannarx qatori yo'q"** — shu oyda ishlab chiqarilgan, lekin tannarx qatori yaratilmagan mahsulotlar. Ularning overhead ulushi hech qayerga yozilmaydi. Bu ro'yxatni bo'shatish uchun mahsulotga norma kiriting va "Tannarx shakllantirish" ni bosing.
- **"Shu natijani tannarxga yozish"** tugmasi Dashboard'dagi "Fakt xarajatlarni yangilash" bilan bir xil natija beradi.

### 2.8.2 Moliya kategoriyasini tannarx guruhiga bog'lash

**Menyu:** Oy xarajatlari manbalari → "Finance kategoriyalari" → "Finance xarajat kategoriyalari → tannarx guruhi"

Tizim har bir Moliya xarajat kategoriyasini avval **nomi bo'yicha** avtomatik guruhlaydi:

| Kategoriya nomida | Avtomatik guruh |
|---|---|
| "material" ("tash" bo'lmasa) | Material (PATH3) |
| "soliq", "bank" | B — Soliq va bank |
| "boshqa" | D — Boshqa |
| kredit, o'tkazma, naqd, qarz | Tannarxga kirmaydi (moliyaviy harakat) |
| dividend, aksiyador | Tannarxga kirmaydi (foydani taqsimlash) |
| QQS | Tannarxga kirmaydi |
| asosiy vosita, uskuna, jihoz, qurilish, investitsiya | Tannarxga kirmaydi (kapital xarajat) |
| tashuv, transport, logist, yuk | Tannarxga kirmaydi (yo'l varaqalari orqali hisoblanadi) |
| oylik, ish haqi, maosh, avans | Tannarxga kirmaydi (HR ish haqi orqali hisoblanadi) |
| xarid, ta'minotchi | Tannarxga kirmaydi (material ombor orqali hisoblanadi) |
| boshqa hamma narsa | D — Boshqa (avtomatik) |

Avtomatik guruh to'g'ri kelmasa, qo'lda belgilang:

1. Jadvalda kategoriyani toping (ustunlar: "Kategoriya", "Tranzaksiya", "Tannarx guruhi", "Avtomatik bo'lsa", "Hozir amalda").
2. "Tannarx guruhi" ro'yxatidan tanlang: "Avtomatik (nom bo'yicha)", "Material (PATH3)", "B — Soliq va bank", "D — Boshqa", "Tannarxga kirmaydi".
3. "Saqlash" → "Kategoriya guruhlari saqlandi. Tannarxga ta'sir qilishi uchun oy bo'yicha "Fakt xarajatlarni yangilash" ni bosing."
4. Dashboard'da "Fakt xarajatlarni yangilash" ni bosing.

**Misol:** iPack'da "Direktor shaxsiy xarajatlari" kategoriyasi avtomatik "D — Boshqa" ga tushadi. Uni "Tannarxga kirmaydi" qilsangiz, "Sabab" ustunida "Qo'lda: tannarxga kirmaydi" ko'rinadi va D manbasi shu summaga kamayadi. "Sex ijarasi" esa D da qolishi to'g'ri.

> **Diqqat:** Qo'lda belgilangan guruh nom qoidasidan ustun turadi. Bola kategoriyaning o'z belgisi bo'lmasa, u ota kategoriyaning guruhini oladi.

## 2.9 Tannarx analitikasi (hisobotlar va dashboardlar)

| Sahifa | Menyu | Nima beradi |
|---|---|---|
| Tannarx Dashboard | Muhandislik → Tannarx → Dashboard (yoki umumiy "Dashboard → Tannarx Dashboard") | Yil/oy/mahsulot filtri; Material narxi, Ish haqi, EXW, CIP bo'yicha reja va fakt, "Farq %"; mahsulot tanlansa "Oylik trend (reja va fakt)" grafigi; "Umumiy statistika": jami mahsulotlar, fakt ma'lumoti borlar, to'ldirilgan foiz; "Oy xarajatlari manbalari" va "Fakt xarajatlarni yangilash" tugmalari |
| Hisobot | Muhandislik → Tannarx → Hisobot | Mahsulotlar jadvali: Material, Ish haqi, EXW, CIP — Reja/Fakt; "Umumiy xulosa": jami farq, o'rtacha farq; har mahsulotdan tafsilotga o'tish |
| Mahsulot tafsiloti | Hisobotdagi mahsulot | "Tannarx — reja va fakt": material, tashuv, A–D, EXW, tayyor mahsulot tashuvi, CIP, E — Foyda; "Fakt material narxi hisob izohi" (qaysi algoritm tanlangani); "Tannarx chop etish" |
| Oy xarajatlari manbalari | Dashboard'dan | 2.8.1 |

> **Maslahat:** Har oy oxirida direktor uchun tartib: (1) Tannarx → "Tannarx shakllantirish"; (2) Dashboard → "Fakt xarajatlarni yangilash"; (3) "Oy xarajatlari manbalari" da "Taqsimlanmagan" va "Ishlab chiqarilgan, lekin tannarx qatori yo'q" ni tekshirish; (4) Hisobotda iPack P1 va B6.1 ning reja/fakt farqini ko'rish.

## 2.10 Tez-tez uchraydigan savollar va muammolar

**Normada karton uchun 0,62 kiritolmayapman, "Miqdori" faqat butun son.**
"Miqdori" = 1 qo'ying, "Sarf optimizatsiyasi" ni bosing, "Optimizatsiya qilingan pozitsiyalar" jadvalidagi "Sarf" katagiga 0.62 yozing va "Saqlash va tasdiqlash" ni bosing.

**Kartonning sarfi butun songa yaxlitlanib ketdi (0,62 o'rniga 1).**
Karton kategoriyasida "Kesish talab qiladi" belgisi bor yoki nomida "yog'och" so'zi bor. Belgini olib tashlang va normani qayta saqlang.

**Taxta narxi normada 54 000 emas, 9 000 bo'lib chiqdi.**
Bu to'g'ri: yog'och uchun narx so'm/metrda yuritiladi (54 000 / 6 m). Summa metrlar × narx bo'yicha to'g'ri chiqadi.

**Eski oy normasini tuzatdim, lekin BOM o'zgarmadi.**
BOM faqat eng so'nggi oy normasiga ergashadi. Eski oyni tahrirlash faqat o'sha oy tannarxiga ta'sir qiladi.

**Mahsulot Norma-sarf yoki BOM ro'yxatida yo'q.**
Mahsulot kartochkasida "Turi" = "Maxsulot" bo'lishi kerak. BOM'da esa mahsulot faqat kamida bitta norma saqlangandan keyin chiqadi.

**Normada material qidiruvda chiqmayapti.**
Kod yoki nomning bir qismini yozing (masalan, "100*50"). Mahsulot o'chirilmagan bo'lishi kerak.

**"Tannarx shakllantirish" P1 uchun qator yaratmadi.**
Shu oyda amal qiluvchi norma yo'q (oy normasi kiritilmagan yoki oldingi normaning "Samarali shu sanagacha" sanasi o'tib ketgan) yoki qator allaqachon bor.

**Fakt material "PATH0" emas, "PATH3" yoki reja bo'lib chiqdi.**
Shu oyda ishlab chiqarish ombordan xomashyo chiqimisiz kiritilgan, chiqim narxi 0 yoki qamrovi past. "Oy xarajatlari manbalari" da "Material algoritmi" ustiga sichqonchani olib borib sababini ko'ring. Keyin Ombor va Ishlab chiqarish ma'lumotlarini tekshiring.

**D — Boshqa juda katta chiqdi.**
"Finance kategoriyalari (shu oy)" jadvalida qaysi kategoriyalar D ga tushganini ko'ring. Tannarxga tegishli bo'lmaganlarini "Tannarxga kirmaydi" qiling va faktni yangilang.
