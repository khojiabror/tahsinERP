# 7. Logistika

Logistika moduli iPack'ning barcha yuk tashishlarini hisobga oladi. Bunga ikki xil yuk kiradi: Wood Nam Import va IMPORT EVRO GRAND'dan keladigan xomashyo hamda UzAutoMotors Powertrain va boshqa mijozlarga jo'natiladigan tayyor mahsulot (P1 taglik, B6.1 quti). Modulda ekspeditorlar, ular bilan tuzilgan shartnomalar va tariflar, har bir reys uchun yo'l varaqasi, ekspeditorga to'lovlar hamda qarz hisobotlari yuritiladi.
Asosan logist ishlaydi. To'lov qismidan buxgalter ham foydalanadi, dashboard va oylik hisobotni rahbar ko'radi.
Modul bir necha bo'limga bog'langan: Ta'minot (xarid invoysi va buyurtmasi), Sotuv (sotuv invoysi, buyurtma, TTNK), Moliya (to'lov avtomatik **Chiqim** bo'lib yoziladi) va Muhandislik (yuklash simulyatsiyasi uchun mahsulot o'lchamlari).

> **Diqqat:** Logistika menyusi faqat "Waybill" (yo'l varaqasi) ruxsati bor foydalanuvchiga ko'rinadi. Ruxsati yo'q foydalanuvchi sahifa manzilini qo'lda yozsa ham "Sizga ruxsat berilmagan!" xabarini oladi.

## 7.1 Asosiy tushunchalar

- **Yuk tashuvchi (ekspeditor)** — yukni tashib beradigan kompaniya. iPack'da asosiy ekspeditor "Osiyo Trans Kargo" MChJ.
- **Shartnoma** — ekspeditor bilan tuzilgan shartnoma: raqami, amal qilish muddati, umumiy summasi va valyutasi bor. Tariflar shartnomaga bog'lanadi.
- **Tarif** — shartnoma doirasida bitta yo'nalish (qayerdan → qayerga) va bitta transport turi uchun belgilangan narx.
- **Tarif asosi** — narx nimaga qarab hisoblanishi. 4 xil bo'ladi:
  - "Mashina uchun" — har reys uchun qat'iy narx;
  - "Ton uchun" — stavka × brut vazn (ton);
  - "KM uchun" — stavka × masofa (km);
  - "Belgilangan narx" — kelishilgan qat'iy summa.
- **Eng kam to'lov summasi** — tarif bo'yicha hisoblangan summa shundan kam chiqsa, shu summa olinadi.
  Misol: 10 t × 50 000 = 500 000, eng kam to'lov 600 000 → yo'l varaqasida 600 000 yoziladi.
- **Transport turi** — mashina modeli va uning o'lchamlari (millimetrda). Masalan "Fura 20t" yoki "Isuzu 5t".
  O'lchamlardan hajm (m³) va polga sig'adigan palletlar soni hisoblanadi.
- **Yo'l varaqasi** — bitta reysning hujjati. Unda kim, nima, qayerdan qayerga, qancha og'irlikda va qancha summaga tashigani yoziladi.
- **Yo'nalish** — reysning turi: "Kiruvchi" (xomashyo bizga keladi), "Chiquvchi" (tayyor mahsulot mijozga ketadi) yoki "Ichki" (o'z omborlarimiz orasida).
- **Yuk turi** — tizim buni o'zi aniqlaydi:
  - "Tayyor mahsulot" — yo'l varaqasi sotuv invoysi, sotuv buyurtmasi yoki TTNKga bog'langan bo'lsa;
  - "Xomashyo/material" — xarid invoysi yoki xarid buyurtmasiga bog'langan bo'lsa.
- **To'lov holati** — yo'l varaqasi bo'yicha qancha to'langani: "To'lanmagan", "Qisman" yoki "To'langan".
- **Akt-sverki** — ekspeditor bilan o'zaro hisob-kitob solishtirmasi: yo'l varaqalari (bizning qarzimiz), to'lovlar va yakuniy saldo.

## 7.2 Ish tartibi (umumiy oqim)

1. Ekspeditorni kiriting (bir marta).
2. Transport turlarini kiriting (bir marta).
3. Ekspeditor bilan shartnoma tuzing.
4. Shartnomaga tariflar qo'shing.
5. Har reys uchun yo'l varaqasi tuzing (yoki Excel'dan yuklang).
6. Ekspeditorga to'lov qiling — Moliya'da chiqim avtomatik yoziladi.
7. Oy oxirida oylik hisobot va akt-sverkini tekshiring.

## 7.3 Yuk tashuvchilar (ekspeditorlar)

**Kim:** logist   **Menyu:** Logistika → Yuk tashish → Ma'lumotnoma va shartnomalar → Tashuvchi kompaniyalar

Bu sahifada iPack bilan ishlaydigan barcha ekspeditorlar ro'yxati turadi. Har bir ekspeditorning batafsil kartasida uning statistikasi ham ko'rinadi.

1. "Tashuvchi kompaniyalar" sahifasida yangi yozuv yaratish tugmasini bosing.
2. Maydonlarni to'ldiring (jadvalga qarang) va "Saqlash"ni bosing.
3. Ro'yxatda ekspeditor nomi yonidagi "Batafsil" orqali kartasini oching.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Yuk tashuvchi nomi | Kompaniyaning rasmiy nomi | "Osiyo Trans Kargo" MChJ |
| Direktor | Rahbar F.I.O. | (shartnomadagi rahbar ismi) |
| Bog'lanish uchun shaxs | Dispetcher yoki menejer | Dispetcher, telefoni bilan |
| Qisqacha bayoni | Erkin izoh | "Andijon–Toshkent yo'nalishi, fura va 5 t mashinalar" |

**Yuk tashuvchi kartasi (Batafsil)** quyidagi bloklardan iborat:
- **Yuk tashuvchi ma'lumotlari** — kiritilgan rekvizitlar.
- **Reyslar bo'yicha** — jami reyslar, shulardan Kiruvchi / Chiquvchi / Ichki reyslar soni.
- **O'rtacha ko'rsatkichlar** — jami xarajat, jami tonnaj, 1 ton uchun va 1 km uchun o'rtacha narx.
- **Qarz** — iPack shu ekspeditorga qancha qarzdor (so'mda).
- **OTIF (On Time In Full) ko'rsatkichi** — vaqtida yetkazilgan va kechikkan reyslar soni hamda OTIF foizi.
  Bu "Rejalashtirilgan yetkazish" va "Haqiqiy yetkazish" sanalaridan hisoblanadi.
- **Eng ko'p ishlatilgan marshrutlar**, **Faol shartnomalar**, **Oxirgi yo'l varaqalari**.
- **Shartnomalar (akt-sverki shartnoma bo'yicha)** — har shartnoma bo'yicha akt-sverkiga o'tish havolasi.

> **Diqqat:** Ekspeditor o'chirilsa, u yangi yo'l varaqasi formasidagi tanlov ro'yxatidan va tashuvchilar ro'yxatidan yo'qoladi. Lekin eski yo'l varaqalarida uning nomi qoladi, qarzi esa oylik hisobotdan yo'qolmaydi. O'chirilgan ekspeditorni tahrirlash orqali tiklab bo'lmaydi.

> **Maslahat:** OTIF to'g'ri chiqishi uchun har yo'l varaqasida ikkala yetkazish sanasini (rejalashtirilgan va haqiqiy) to'ldiring. Shunda ekspeditorlarni yil oxirida xolis solishtirasiz.

## 7.4 Transport turlari

**Kim:** logist   **Menyu:** Logistika → Yuk tashish → Ma'lumotnoma va shartnomalar → Transport turlari

Transport turi tariflarda, yo'l varaqalarida va yuklash simulyatsiyasida ishlatiladi. Barcha o'lchamlar **millimetrda** kiritiladi.

1. Yangi yozuv yarating.
2. Mashina nomini, o'lchov birligini va yuk ko'tarish imkoniyatini kiriting.
3. Tashqi va ichki o'lchamlarni kiriting, so'ng saqlang.
4. "Batafsil"da tizim hisoblagan qiymatlarni tekshiring: ichki hajm, tashqi hajm (m³) va 1200×1000 palletlarning polga bir qatlamda sig'adigan soni.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Transport turi | Mashina nomi | "Fura 20t" |
| O'lchov birligi | Sig'im birligi | dona |
| Ko'tarish imkoniyati | Yuk ko'tarish (kg) | 20000 |
| Og'irligi | Mashinaning o'z og'irligi (kg) | 8000 |
| Tashqi Uzunligi / Kengligi / Balandligi (mm) | Kuzovning tashqi o'lchami | 13800 / 2550 / 4000 |
| Ichki Uzunligi / Kengligi / Balandligi (mm) | Yuk bo'limining ichki o'lchami | 13600 / 2450 / 2700 |

**Natija:** "Fura 20t" kartasida ichki hajm **89,96 m³** (13,6 × 2,45 × 2,7), tashqi hajm **140,76 m³** va **26 ta** pallet (1200×1000, bir qatlam) ko'rinadi.

> **Diqqat:** O'lchamlarni santimetrda yoki metrda kiritmang. Kiritilsa, hajm va simulyatsiya natijasi 10–1000 marta xato chiqadi.

## 7.5 Shartnomalar

**Kim:** logist (rahbar tasdig'idan keyin)   **Menyu:** Logistika → Yuk tashish → Ma'lumotnoma va shartnomalar → Shartnomalar

1. Yangi shartnoma yarating va maydonlarni to'ldiring.
2. Sanalarni kalendardan tanlang.
3. Saqlang. Keyin "Batafsil" sahifasidagi "Tarif qo'shish" tugmasi bilan shu shartnomaga tarif qo'shing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Shartnoma raqami | Qog'oz shartnomadagi raqam | OTK-2026/07 |
| Yuk tashuvchi | Ekspeditor | Osiyo Trans Kargo MChJ |
| Tuzilgan sanasi | Imzolangan sana | 01.07.2026 |
| Tugaydigan sana | Amal qilish muddati | 31.12.2026 |
| Miqdori | Shartnomaning umumiy summasi | 150 000 000 |
| Valyuta | Shartnoma valyutasi | UZS |
| Batafsil ma'lumot | Izoh, shartlar | "To'lov reysdan keyin 10 bank kuni ichida" |

**Shartnoma kartasi (Batafsil)**:
- shartnoma ma'lumotlari;
- **Tariflar** jadvali: qayerdan, qayerga, transport turi, tarif asosi, stavka, valyuta, faol yoki yo'qligi;
- **"Akt-sverki (ushbu shartnoma)"** tugmasi.

## 7.6 Tariflar

**Kim:** logist   **Menyu:** Logistika → Yuk tashish → Ma'lumotnoma va shartnomalar → Tariflar (yoki shartnoma kartasidagi "Tarif qo'shish")

Tarif yo'l varaqasi summasini avtomatik hisoblash uchun kerak. Har bir yo'nalish va transport turi juftligiga alohida tarif tuziladi.

| Maydon | Nima kiritiladi | iPack misoli 1 (tayyor mahsulot) | iPack misoli 2 (xomashyo) |
|---|---|---|---|
| Shartnoma | Qaysi shartnoma | OTK-2026/07 | OTK-2026/07 |
| Transport turi | Mashina turi | Fura 20t | Fura 20t |
| Valyuta | Tarif valyutasi | UZS | UZS |
| Qayerdan (Viloyat / Shahar) | Jo'nash joyi | Andijon / Andijon | Toshkent / Toshkent |
| Qayerga (Viloyat / Shahar) | Borish joyi | Toshkent / Toshkent | Andijon / Andijon |
| Tarif asosi | Hisoblash usuli | Mashina uchun | Ton uchun |
| Tarif stavkasi | Narx | 3 500 000 | 150 000 |
| Eng kam to'lov summasi | Minimal summa | — | 2 000 000 |
| Faol | Hozir amal qiladimi | ✔ | ✔ |
| Izoh | Erkin matn | "UzAuto Powertrain'ga P1 taglik" | "Wood Nam Import taxtasi" |

(Narxlar misol sifatida berilgan.)

> **Diqqat:** Tarifni o'zgartirsangiz (masalan stavkani oshirsangiz), avval tuzilgan yo'l varaqalarining summasi **o'zgarmaydi**. Har yo'l varaqasi o'zi tanlangan tarif va stavkani eslab qoladi. Tarif nofaol qilinsa ham, eski yo'l varaqasini ochganda o'sha tarif ko'rinib turadi. Yangi stavka faqat yangi yo'l varaqalariga yoki tahrirda tarifni qayta tanlaganingizda qo'llanadi.

> **Maslahat:** Narx o'zgarganda eski tarifni tahrirlamang. Uni nofaol qiling va yangi tarif yarating — narxlar tarixi saqlanib qoladi.

## 7.7 Yo'l varaqasi (yuk xati)

**Kim:** logist   **Menyu:** Logistika → Yuk tashish → Operatsiyalar → Yo'l varaqalari → "Yangi yo'l varaqasi"

Har bir reys uchun bitta yo'l varaqasi tuziladi. Forma bir nechta bo'limdan iborat. Shartnoma tanlanganda uning tariflari ro'yxatga sahifa yangilanmasdan yuklanadi. Tarif tanlanganda tarif asosi va stavka avtomatik to'ladi.

### 7.7.1 Chiquvchi reys: UzAuto'ga P1 taglik yetkazish

Ssenariy: Noyabr buyurtmasining 1-partiyasi — 400 dona iPack P1 taglik Andijondan Toshkentga, UzAutoMotors Powertrain'ga jo'natiladi.

1. "Yangi yo'l varaqasi"ni oching.
2. **Asosiy ma'lumotlar** bo'limi:
   - Yo'l varaqasi raqami: "OTK-1101". Bo'sh qoldirsangiz, tizim "TB_0001" ko'rinishida avtomatik raqam beradi.
   - Sana: 05.11.2026.
   - Yo'nalish: "Chiquvchi".
   - Yuk tashuvchi: Osiyo Trans Kargo.
3. **Shartnoma va transport** bo'limi:
   - Shartnoma: OTK-2026/07.
   - Transport turi: Fura 20t.
   - Tarif ro'yxatidan "Andijon → Toshkent, Mashina uchun" tarifini tanlang.
4. **Bog'langan hujjatlar** bo'limi:
   - Sotuv invoysi maydoniga invoys raqamining 1–2 belgisini yozing va ro'yxatdan UzAutoMotors Powertrain invoysini tanlang.
   - Shu usulda Sotuv buyurtmasini ham tanlang. TTNK tayyor bo'lsa, uni ham bog'lang.
   - Sotuv invoysi tanlanganda tizim o'zi to'ldiradi: Yo'nalish "Chiquvchi", Qayerdan "Bizning ombor", Qayerga — mijoz manzili, Valyuta — invoys valyutasi.
5. **Marshrut va Yuk parametrlari** bo'limi:
   - Masofa (km): 320.
   - Brut vazn (ton): 8,4; Net vazn: 8,0.
   - CBM: 72.
   - Palletlar soni: 400 (P1 taglikning o'zi pallet).
6. **Tarif va summa** bo'limi:
   - Jami summa bo'sh bo'lsa, tarifdan hisoblanadi: "Mashina uchun" → 3 500 000.
   - To'lovchi: "Bizning kompaniya".
7. **Transport va haydovchi** bo'limi:
   - Mashina raqami 60A123BC, Traler raqami, modellar.
   - Haydovchi va uning PINFL raqami.
   - Rejalashtirilgan yetkazish: 06.11.2026. Haqiqiy yetkazish — mashina yetib kelgach kiritiladi.
8. "Saqlash"ni bosing.

> **Diqqat:** Sotuv invoysi tanlanganda "To'lovchi" maydoni avtomatik "Mijoz" bo'lib qoladi. Agar tashish xarajatini iPack to'lasa (odatdagi holat), uni "Bizning kompaniya"ga qaytaring.

### 7.7.2 Kiruvchi reys: Wood Nam Import'dan taxta

1. Yo'nalish: "Kiruvchi".
2. Shartnoma: OTK-2026/07. Tarif: "Toshkent → Andijon, Ton uchun, 150 000".
3. Xarid invoysi maydonida Wood Nam Import invoysini tanlang, Xarid buyurtmasini ham bog'lang.
   Tizim o'zi to'ldiradi: Qayerdan — ta'minotchi manzili, Qayerga "Bizning ombor", Valyuta — invoys valyutasi.
4. Brut vazn: 18 t.
5. Summa: 18 × 150 000 = **2 700 000**. Agar vazn 12 t bo'lsa, 12 × 150 000 = 1 800 000 chiqadi. Bu eng kam to'lov 2 000 000 dan kam, shuning uchun **2 000 000** yoziladi.

### 7.7.3 Forma maydonlari

| Bo'lim | Maydon | Izoh |
|---|---|---|
| Asosiy ma'lumotlar | Yo'l varaqasi raqami, Sana, Yo'nalish, Yuk tashuvchi | Raqam takrorlanmasligi kerak |
| Shartnoma va transport | Shartnoma, Transport turi, Valyuta, Tarif | Tarif ro'yxati shartnomaga qarab to'ladi |
| Marshrut | Qayerdan, Qayerga, Masofa (km) | "KM uchun" tarifda masofa majburiy |
| Yuk parametrlari | Brut vazn (ton), Net vazn (ton), CBM, Palletlar soni | "Ton uchun" tarif brut vazndan hisoblanadi |
| Tarif va summa | Tarif asosi, Tarif stavkasi, Jami summa, To'lovchi, To'lovchi ID, TTNK | Summa bo'sh bo'lsa, tarif bo'yicha hisoblanadi |
| Bog'langan hujjatlar | Xarid invoysi, Sotuv invoysi, Sotuv buyurtmasi, Xarid buyurtmasi | Qidiruv 1–2 harf yozilganda ishlaydi |
| Transport va haydovchi | Mashina/Traler raqami va modeli, Haydovchi, PINFL, Rejalashtirilgan va Haqiqiy yetkazish, Izoh | OTIF uchun sanalar muhim |
| — | Qo'lda tuzatish sababi | Summa yoki stavka tarifdan farq qilsa, sababini yozing |

> **Diqqat:** Bir xil raqamli ikkinchi yo'l varaqasi saqlanmaydi. Tizim "Bu raqamli yo'l varaqasi allaqachon mavjud." xabarini chiqaradi.

**Natija:**
- Yo'l varaqasi ro'yxatda paydo bo'ladi: summa 3 500 000 UZS, To'lov holati **"To'lanmagan"**, Qoldiq 3 500 000.
- Osiyo Trans Kargo kartasidagi Qarz shu summaga oshadi. Dashboard va oylik hisobotda ham u ko'rinadi.
- Ombor qoldig'iga yo'l varaqasi **ta'sir qilmaydi**: omborga kirim va chiqim Ombor/Sotuv hujjatlari bilan qilinadi.

### 7.7.4 Ro'yxat, ko'rish, tahrirlash, o'chirish

- **Ro'yxat**:
  - filtrlar: Boshlanish va Tugash sanasi, Yuk tashuvchi, Shartnoma, Yo'nalish, Qidirish;
  - ustunlar: raqam, sana, yo'nalish, yuk turi, ekspeditor, shartnoma, marshrut, vazn, CBM, mashina, haydovchi, summa, valyuta, To'langan, Qoldiq, To'lov holati.
  - Ro'yxat server tomonidan sahifalanadi, shuning uchun minglab yozuv bo'lsa ham tez ochiladi.
- **Batafsil**:
  - yo'l varaqasi ma'lumotlari va yetkazish sanalari;
  - **Bog'langan to'lovlar** ro'yxati, Jami summa / To'langan / Qoldiq;
  - "To'lov qo'shish" tugmasi.
- **Tahrirlash**: tarifga tegmasangiz, saqlangan stavka va summa o'zgarmaydi.
- **O'chirish**: yozuv ro'yxatdan yo'qoladi (yumshoq o'chirish). Agar unga to'lov bog'langan bo'lsa, avval to'lovni tahrirlab, yo'l varaqasini undan chiqaring.

| To'lov holati | Ma'nosi |
|---|---|
| To'lanmagan | Hech qanday to'lov bog'lanmagan |
| Qisman | To'langan summa yo'l varaqasi summasidan kam |
| To'langan | To'liq yopilgan (qoldiq 0) |

## 7.8 Excel'dan yo'l varaqalarini yuklash

**Kim:** logist   **Menyu:** Yo'l varaqalari → "Excel orqali yuklash" (sahifa manzili `/WaybillImport/UploadExcel`)

Oy davomida ekspeditor reyslar ro'yxatini Excel'da yuborsa, ularni bitta-bitta kiritish shart emas.

1. "Namuna faylni yuklab olish" tugmasini bosing. Namunadagi qator haqiqiy faol tarif qiymatlari bilan to'ldirilgan bo'ladi: shartnoma raqami, transport turi, tarif asosi, stavka, valyuta.
2. Har bir reys uchun bitta qator to'ldiring. Majburiy ustunlar:
   - **WaybillNo** — yo'l varaqasi raqami;
   - **IssueDate** — sana;
   - **ContractID** yoki **ContractNo** — shartnoma;
   - **TransportTypeID** yoki **TransportType** — transport turi.
3. Qo'shimcha ustunlar:
   - Direction: 0/1/2 yoki Kiruvchi/Chiquvchi/Ichki;
   - ForwarderName;
   - FromLocation, ToLocation, DistanceKm;
   - GrWeight, NetWeight, CBM, PalletsCount;
   - FreightBasis, Rate, Amount, Currency;
   - PlannedDeliveryDate, ActualDeliveryDate, Description, ManualCorrectionReason.
4. Faylni "Excel fayl (.xlsx)" maydonida tanlang va "Yuklash"ni bosing.

**Qoidalar:**
- FreightBasis, Rate yoki Currency bo'sh bo'lsa, ular shartnoma tarifidan olinadi.
- Amount bo'sh bo'lsa, summa tarif bo'yicha hisoblanadi (eng kam to'lov ham hisobga olinadi).
- Stavka tarifdan farq qilsa, **ManualCorrectionReason** ustuni to'ldirilishi shart. Aks holda qator rad etiladi, masalan: "Qator 5: tarif #… dan farq qiladi: stavka (45000 ≠ 50000) — ManualCorrectionReason kiriting."
- Fayl ichida takrorlangan raqam yoki bazada mavjud raqam rad etiladi.
- Noto'g'ri qator butun faylni to'xtatmaydi.

**Natija:** sahifada "Yuklandi: N, O'tkazib yuborildi: M" va xato qatorlar ro'yxati (qator raqami bilan) chiqadi. To'g'ri qatorlar "To'lanmagan" holatda yo'l varaqalariga qo'shiladi.

> **Maslahat:** Xato qatorlarni tuzatib, faqat shu qatorlardan iborat yangi fayl bilan qayta yuklang. Muvaffaqiyatli yuklangan raqamlar takror kiritilmaydi.

## 7.9 Ekspeditorga to'lovlar

**Kim:** buxgalter yoki logist   **Menyu:** Logistika → Yuk tashish → Operatsiyalar → To'lovlar → "Yangi to'lov"

Bitta to'lov bilan bir nechta yo'l varaqasini yopish mumkin. Masalan, Osiyo Trans Kargo noyabrdagi 3 ta reys uchun bitta hisob-faktura yuboradi va siz bitta bank to'lovi qilasiz.

1. "Yangi to'lov"ni oching.
2. To'ldiring:
   - **Yo'l varaqalari**: OTK-1101, OTK-1102, OTK-1103 (bir nechtasini tanlash mumkin);
   - **Yuk tashuvchi**: Osiyo Trans Kargo;
   - **To'lov miqdori**: 10 500 000;
   - **To'lov sanasi**: 20.11.2026;
   - **Valyuta**: UZS;
   - **To'lov hisobi (Hisob-raqam)**: 9001 Hisob raqam;
   - **Izoh**: "Noyabr 1–3 partiya tashish".
3. "Saqlash"ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Yo'l varaqalari * | To'lanayotgan reyslar | OTK-1101, OTK-1102, OTK-1103 |
| Yuk tashuvchi * | Ekspeditor | Osiyo Trans Kargo |
| To'lov miqdori * | To'langan summa (0 dan katta) | 10 500 000 |
| To'lov sanasi | Bank to'lovi sanasi | 20.11.2026 |
| Valyuta | To'lov valyutasi | UZS |
| To'lov hisobi (Hisob-raqam) | Qaysi bank hisobidan | 9001 Hisob raqam |
| Izoh | Erkin matn | "Noyabr 1–3 partiya" |

**Taqsimot qoidasi:**
1. Summa tanlangan yo'l varaqalariga ro'yxatdagi tartib bo'yicha taqsimlanadi (sanasi yangisi birinchi, sana bir xil bo'lsa raqam bo'yicha). Har bir yo'l varaqasining qoldig'i navbat bilan to'liq yopiladi.
2. Barcha qoldiqlar yopilgandan keyin ham summa ortib qolsa (avans), ortgan qism tanlangan yo'l varaqalariga teng bo'linadi. Bunda "Ortiqcha to'lov: … bo'yicha to'langan summa yo'l varaqasi summasidan oshdi" degan ogohlantirish chiqadi.
3. Misol: 7 000 000 to'lansa, 2 ta reys "To'langan" bo'ladi, uchinchisi "To'lanmagan" bo'lib qoladi. 5 000 000 to'lansa, bittasi "To'langan", ikkinchisi "Qisman" (1 500 000), uchinchisi "To'lanmagan" bo'ladi.

**Valyuta:**
- To'lov valyutasi yo'l varaqasi valyutasidan farq qilsa, summa to'lov sanasidagi CBU kursi bilan o'giriladi.
- Shu sana uchun kurs topilmasa, to'lov saqlanmaydi ("… sanasi uchun valyuta kursi topilmadi"). Bunday holda kursni Sozlamalar → Valyutalar'da kiriting.
- Turli valyutadagi yo'l varaqalarini bitta to'lovga bog'lab bo'lmaydi. Ularni alohida to'lang.

**Moliyaga ta'siri:**
- Saqlash bilan birga Moliya → Tranzaksiyalar'da bitta **Chiqim** yoziladi:
  - hisob — tanlangan bank hisobi;
  - kontragent — Osiyo Trans Kargo;
  - maqsad — "Yo'l varaqasi bo'yicha yuk tashish to'lovi: OTK-1101, OTK-1102, OTK-1103".
- Chiqim toifasi tizim tomonidan tanlanadi: tayyor mahsulot reysi (sotuv invoysi, buyurtma yoki TTNKga bog'langan) uchun "T.Tash", xomashyo reysi uchun "M.Tash".
- To'lov tahrirlansa, moliya yozuvi ham yangilanadi. To'lov o'chirilsa, uning taqsimoti ham, moliya chiqimi ham bekor qilinadi.
- To'lov, taqsimot va moliya yozuvi birga saqlanadi: bittasida xato bo'lsa, hech biri saqlanmaydi.

> **Diqqat (bank hisobi ogohlantirishi):** To'lov faqat **bank hisobi**dan (hisob-raqam, kassa emas) moliyaga yoziladi.
> - "To'lov hisobi" bo'sh bo'lsa, tizim to'lov valyutasidagi birinchi faol bank hisobini oladi.
> - Mos bank hisobi umuman topilmasa, to'lov saqlanadi, lekin sariq ogohlantirish chiqadi: "To'lov saqlandi, lekin moliyaga (Chiqim) o'tkazilmadi: mos bank hisob-raqami topilmadi. To'lovni tahrirlab, hisob-raqamni tanlang."
> - Bunday to'lov Moliya hisobotlariga tushmaydi, shuning uchun uni albatta tahrirlab, hisob-raqamni tanlang.

**Rad etiladigan holatlar:**
- To'lov miqdori 0: "To'lov miqdori noldan katta bo'lishi kerak."
- Yo'l varaqasi tanlanmagan: "Kamida bitta yo'l varaqasi tanlang."
- Yo'l varaqasi boshqa ekspeditorga tegishli: "Quyidagi yo'l varaqalari boshqa yuk tashuvchiga tegishli: …"

**Natija:**
- Yo'l varaqalari ro'yxatida To'langan / Qoldiq / To'lov holati yangilanadi.
- Ekspeditor qarzi kamayadi.
- Moliya'da 9001 hisobi qoldig'i 10 500 000 ga kamayadi.

**To'lovlar ro'yxati** ustunlari: Yo'l varaqalari, Yuk tashuvchi, Yo'l varaqasi summasi, Miqdori, Pul birligi, Qoldiq, To'lov sanasi, Izoh. "To'liq ma'lumot" sahifasida har bir yo'l varaqasiga qancha taqsimlangani ko'rinadi.

## 7.10 Transport yuklash simulyatsiyasi

**Kim:** logist, sotuv bo'limi   **Menyu:** Logistika → Yuk tashish → Operatsiyalar → Yuklash simulyatsiyasi

Simulyatsiya bitta mashinaga nechta mahsulot sig'ishini hisoblaydi. U mahsulot o'lchamlari (Muhandislik → Maxsulotlar'da uzunlik, eni, balandligi, mm) va transport turining ichki o'lchamlaridan foydalanadi.

1. "Mahsulot" maydoniga nomning bir qismini yozing (masalan "P1"). Ro'yxat serverdan keladi. "iPack P1 Yog'och taglik"ni tanlang.
2. "Transport turi": Fura 20t.
3. Hisoblash tugmasini bir marta bosing.
4. **Natijalar** bo'limini o'qing:
   - Mahsulot ma'lumotlari: nomi, PNo, o'lchamlari, hajmi;
   - Transport ma'lumotlari: ichki o'lchamlari, hajmi;
   - Hisoblash natijalari: **Maksimal miqdor**, Optimal orientatsiya, Jami hajm (m³), **Foydalanish** (%) va joylashtirish sxemasi (uzunlik × kenglik × qatlam);
   - 3D vizualizatsiya. "Alternativ orientatsiyani ko'rsatish" orqali boshqa joylashuvni ham ko'rish mumkin.

> **Maslahat:** Natija ikki narsaga yordam beradi: UzAuto bilan partiya hajmini kelishish va reyslar sonini rejalash. Masalan, bir furaga taxminan 400 dona P1 sig'sa, 1 200 dona uchun 3 ta reys kerak — bu buyurtmadagi 3 partiyaga mos keladi.

> **Diqqat:** Mahsulotda o'lcham kiritilmagan bo'lsa, natija 0 chiqadi. Avval Muhandislik'da mahsulot o'lchamlarini (mm, qadoqlangan holatda) to'ldiring.

## 7.11 Hisobotlar va dashboardlar

### Logistika Dashboard
**Menyu:** Dashboard → Logistika Dashboard
- **Filtrlar:** Boshlanish sanasi, Tugash sanasi, Yuk tashuvchi.
- **Kartalar:** Jami xarajat, Jami reyslar, Jami tonnaj, 1 ton uchun, 1 km uchun, Qarz.
- **Yo'nalish bo'yicha xarajat:** Kiruvchi / Chiquvchi / Ichki.
- **Yuk statistikasi** va **Eng ko'p ishlatilgan marshrutlar** (qayerdan → qayerga, reyslar soni, tonnaj).
- **Yuk tashuvchilar bo'yicha reyting:** har ekspeditor uchun reyslar, tonnaj, To'langan va Qoldiq.
- **Hisobotlar** havolalari:
  - **Oylik hisobot**;
  - **Marshrutlar samaradorligi** — har marshrut bo'yicha reyslar, tonnaj, CBM, masofa, jami xarajat, 1 ton va 1 km narxi, asosiy ekspeditor;
  - **Solishtirish hisoboti** — uchta ro'yxat: "Invoyssiz yo'l varaqalari", "To'lovsiz yo'l varaqalari", "Yuk tashish xarajati kiritilmagan sotuv invoyslari";
  - **Yuk tashuvchilarni taqqoslash** — bir marshrutda ekspeditorlarning narxi va OTIF %.

Yo'l varaqalari turli valyutada bo'lsa, hammasi hujjat sanasidagi CBU kursi bilan so'mga o'giriladi. Bu haqda izoh chiqadi. Kursi topilmagan hujjat jamiga qo'shilmaydi va ogohlantirish chiqadi.

### Oylik hisobot
**Menyu:** Logistika → Yuk tashish → Oylik hisobot
1. Yil va Oyni tanlang, "Hisobotni ko'rsatish"ni bosing.
2. Har bir ekspeditor bo'yicha ko'rsatkichlar chiqadi: Reyslar soni, Oldingi qarzdorlik, Oy xarajati, Oy to'lovi, **Oy oxiridagi qarzdorlik**.
3. "Chop etish" tugmasi rasmiy shakl ("LOGISTIKA OYLIK HISOBOTI", korxona nomi va sana bilan) chiqaradi.

Misol (noyabr 2026, Osiyo Trans Kargo):
- Oldingi qarzdorlik: 0;
- Oy xarajati: 3 × 3 500 000 + 2 700 000 = 13 200 000;
- Oy to'lovi: 10 500 000;
- Oy oxiridagi qarzdorlik: **2 700 000**.

### Ekspeditor akt-sverkisi
**Menyu:** Shartnomalar → shartnoma → Batafsil → "Akt-sverki (ushbu shartnoma)" (yoki ekspeditor kartasidagi "Shartnomalar (akt-sverki shartnoma bo'yicha)")
1. Sanani (masalan 30.11.2026) kiriting.
2. Hisobot ko'rinadi:
   - yo'l varaqalari — bizning ekspeditor oldidagi majburiyatimiz;
   - to'lovlar;
   - **yakuniy saldo**: musbat bo'lsa, iPack ekspeditorga qarzdor.
3. Chop etib, ekspeditor bilan imzolashingiz mumkin.

> **Diqqat:** Dashboard, oylik hisobot, ekspeditor kartasi, akt-sverki va solishtirish hisoboti bir xil qarz qoidasidan foydalanadi. Moliya'da qo'lda kiritilgan ekspeditor to'lovlari ham qarzni kamaytiradi. Agar bir ekranda qarz boshqacha chiqsa, filtr sanalarini tekshiring.

## 7.12 Oylik ish tartibi (logist uchun)

| Qachon | Nima qilinadi |
|---|---|
| Har reysda | Yo'l varaqasi: sotuv yoki xarid invoysiga bog'lang, rejalashtirilgan yetkazish sanasini kiriting |
| Mashina yetib kelganda | Haqiqiy yetkazish sanasini kiriting (OTIF uchun) |
| Ekspeditor hisob-faktura yuborganda | Bitta to'lov bilan tegishli yo'l varaqalarini yoping, bank hisobini tanlang |
| Oy oxirida | Solishtirish hisoboti: invoyssiz va to'lovsiz yo'l varaqalarini tozalang |
| Oy oxirida | Oylik hisobot va akt-sverkini chop etib, ekspeditor bilan solishtiring |

## 7.13 Tez-tez uchraydigan savollar va muammolar

**Tarif ro'yxati bo'sh chiqyapti.**
Tanlangan shartnomada faol tarif yo'q. Shartnoma kartasida "Tarif qo'shish" orqali tarif yarating yoki eski tarifni faol qiling.

**Jami summa tarifdan kam yoki ko'p chiqdi.**
"Eng kam to'lov summasi"ni tekshiring. Ton uchun tarifda brut vazn, KM uchun tarifda masofa to'g'ri kiritilganini ko'ring.

**Tarif narxi oshdi, eski yo'l varaqalari qayta hisoblanadimi?**
Yo'q. Faqat tahrirda tarifni qayta tanlasangiz yangi stavka qo'llanadi.

**To'lov saqlandi, lekin Moliya'da ko'rinmayapti.**
Mos bank hisobi topilmagan (sariq ogohlantirish chiqqan bo'lishi kerak). To'lovni tahrirlab, "To'lov hisobi"ni tanlang.

**To'lov "kurs topilmadi" xatosi bilan saqlanmadi.**
To'lov sanasi uchun CBU kursi yo'q. Sozlamalar → Valyutalar'da kursni kiriting yoki internet ulanishini tekshiring.

**Bir to'lovga USD va UZS yo'l varaqalarini qo'shib bo'lmayapti.**
Bu qoida. Har valyuta uchun alohida to'lov qiling.

**Excel'dan yuklashda "Shartnoma topilmadi".**
ContractNo ustunidagi raqam tizimdagi shartnoma raqami bilan harfma-harf mos bo'lishi kerak.

**Ekspeditorni o'chirdim, qarzi yo'qoladimi?**
Yo'q. Eski yo'l varaqalari va qarz hisobotlarda qoladi, faqat yangi formalarda u tanlanmaydi.

**Logistika menyusi ko'rinmayapti.**
Rolingizda "Waybill" ruxsati yo'q. Administratorga murojaat qiling.
