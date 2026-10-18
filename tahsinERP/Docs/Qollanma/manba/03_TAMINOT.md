# 3. Ta'minot (xarid)

Ta'minot (SRM) moduli iPack'ga kerakli xomashyo va jihozlarni kimdan, qancha va qaysi narxda olishni hujjatlashtiradi: ta'minotchi kartasidan tortib xarid invoysi va kreditorlik qarzigacha. Asosiy foydalanuvchilar: **Xaridchi** va **Xaridchi+Savdogar** rollari (Xarid bo'limi), arizalarni tasdiqlovchi rahbar (Admin yoki Ta'sischi), invoys va qarzni kuzatuvchi buxgalter. Modul bevosita **Ombor** (invoysdan kirim), **Moliya** (kreditorlik qarzi, to'lovlar), **Muhandislik** (BOM orqali ehtiyoj hisobi) va **CRM** (sotuv buyurtmasidan xarid buyurtmasini generatsiya qilish) bilan bog'langan.

Namunaviy ssenariy: UzAutoMotors Powertrain noyabr oyi uchun 1 200 dona **iPack P1 Yog'och taglik** va 3 000 dona **iPack B6.1 quti** buyurtma qildi. Ishlab chiqarish uchun iPack MChJ "Wood Nam Import"dan taxta, "IMPORT EVRO GRAND"dan karton xarid qiladi. Quyidagi norma va narxlar **misol** sifatida olingan:

| Material | Norma (misol) | Ehtiyoj | Xarid (zahira bilan) | Narx, QQSsiz (misol) |
|---|---|---|---|---|
| Yog'och 100\*50\*6000 (dona) | 1 dona P1 uchun 2 dona | 1 200 × 2 = 2 400 dona | 2 600 dona | 54 000 so'm/dona |
| Karton_2090x1370 (m²) | 1 dona B6.1 uchun 0,62 m² | 3 000 × 0,62 = 1 860 m² | 2 000 m² | 6 500 so'm/m² |

## 3.1 Asosiy tushunchalar

- **Ta'minotchi** — iPack bilan haqiqatan ishlaydigan, hujjatlarda (buyurtma, shartnoma, invoys) tanlanadigan kompaniya. Masalan MChJ "Wood Nam Import", "IMPORT EVRO GRAND", WOODLAND BIZNES MCHJ, Mexmash MChJ.
- **Ta'minotchilar bazasi (Ma'lumotnoma)** — bozor o'rganish uchun ro'yxat: hali shartnoma tuzilmagan, lekin taxta yoki karton sotadigan korxona va do'konlar. Bu ro'yxat hujjatlarda tanlanmaydi; xaridchining "telefon daftari".
- **Direct (to'g'ridan-to'g'ri) xarid** — ishlab chiqarish xomashyosi xaridi (taxta, karton, selofan, metiz). Ariza talab qilinmaydi, buyurtma bevosita yoki sotuv buyurtmasidan BOM bo'yicha generatsiya qilinadi.
- **Indirect (bilvosita) xarid** — ishlab chiqarishga bevosita kirmaydigan narsalar (masalan, "Jihoz" kategoriyasidagi asbob-uskuna, sarf materiallar). Avval **xarid arizasi** yoziladi va tasdiqlanadi, keyin buyurtmaga aylantiriladi. Indirect xaridda faqat kategoriyasida "Sarf materiali" yoki "Xarid qilishga ruxsat" belgisi bor mahsulotlar tanlanadi.
- **Xarid arizasi (talabnoma)** — bo'lim xodimining "shu narsa kerak" degan rasmiy so'rovi. Bitta tasdiq bilan ishlaydi.
- **Xarid buyurtmasi (PO)** — ta'minotchiga beriladigan buyurtma: mahsulot, miqdor, narx, QQS.
- **Xarid shartnomasi** — ta'minotchi bilan tuzilgan shartnoma: muddat, to'lov shartlari, Incoterms, QQS, HS kodlar, PDF nusxa.
- **Xarid invoysi** — ta'minotchidan kelgan hisob-faktura (yuk bilan birga). Ombor kirimi va Moliyadagi kreditorlik qarzi shu hujjatga tayanadi.
- **HS kod (ТНВЭД)** — tovarning bojxona tasnif kodi. Import shartnomalarida qator darajasida ko'rsatiladi.
- **QQS** — qo'shilgan qiymat solig'i. iPack misolida 12%. Buyurtma, shartnoma va invoys summasi **QQS bilan** saqlanadi.
- **Kreditorlik qarzi** — iPack'ning ta'minotchilarga qarzi: invoyslar (yoki qabul qilingan kirimlar) summasi minus ta'minotchiga qilingan to'lovlar.

Xarid zanjiri:

```
(Indirect)  Xarid arizasi --tasdiq--> Xarid buyurtmasi --> Xarid shartnomasi --> Xarid invoysi --> Ombor kirimi (qabul)
(Direct)                              Xarid buyurtmasi --> Xarid shartnomasi --> Xarid invoysi --> Ombor kirimi (qabul)
                                                                                     |
                                                                                     +--> Moliya: "Kreditorlar qarzi" yozuvi
```

Shartnoma ixtiyoriy: kichik xarid shartnomasiz (faqat buyurtma va invoys bilan) ham yuritiladi.

## 3.2 Ta'minotchilar

**Kim:** Xaridchi, Admin   **Menyu:** Ta'minot (SRM) → Ta'minotchilar → Ta'minotchilar

Ro'yxatda ta'minotchi nomi, turi, aloqa shaxsi va telefoni ko'rinadi. Qidiruv maydoni, **Yangi ta'minotchi** va **Ro'yxatni yangilash** tugmalari bor; ro'yxatni tur bo'yicha filtrlash mumkin.

1. **Yangi ta'minotchi** tugmasini bosing.
2. Nomi = `MChJ "Wood Nam Import"`, DUNS = korxona INN raqami, Turi = `Mahalliy`, Mamlakat = `O'zbekiston`, Shahar va manzilni yozing.
3. Telefon, E-mail, aloqa xodimi va direktor ismini kiriting.
4. **Saqlash** ni bosing. Xuddi shunday "IMPORT EVRO GRAND" ni ham kiriting.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Nomi | Ta'minotchining rasmiy nomi (majburiy) | MChJ "Wood Nam Import" |
| DUNS | Raqamli identifikator. Alohida INN maydoni yo'q — INN shu yerga yoziladi | 30xxxxxxx (INN) |
| Turi | Erkin matn, guruhlash va filtr uchun (majburiy) | Mahalliy / Import |
| Mamlakat, Shahar, Manzil | Joylashuv | O'zbekiston, Andijon |
| Telefon raqami, E-mail | Aloqa | +998 ... |
| Aloqa uchun hodim | Kim bilan gaplashiladi | Savdo menejeri F.I.Sh. |
| Direktor ismi | Rahbar | — |

**Ko'rish (Details)** sahifasida ta'minotchi ma'lumotlari va "Bog'liq hujjatlar" bloki chiqadi: shu ta'minotchiga tegishli **Buyurtmalar (N)**, **Shartnomalar (N)**, **Invoyslar (N)**. Bu sahifa — ta'minotchi bilan butun tarixni bir joyda ko'rishning eng tez yo'li.

**Tahrirlash** va **O'chirish** ro'yxatdagi tugmalar yoki Details sahifasi orqali bajariladi.

> **Diqqat:** O'chirish yozuvni bazadan butunlay o'chirmaydi, faqat yashiradi (soft-delete). Eski buyurtma va invoyslarda ta'minotchi nomi saqlanib qoladi.
> **Maslahat:** "Turi" maydoniga har doim bir xil yozing (masalan faqat `Mahalliy` va `Import`), aks holda filtr ikkita "turli" guruhni ko'rsatadi.

**Natija:** Ta'minotchi buyurtma, shartnoma va invoys formalaridagi "Ta'minotchi" ro'yxatida paydo bo'ladi.

## 3.3 Ta'minotchilar bazasi (Ma'lumotnoma)

**Kim:** Xaridchi   **Menyu:** Ta'minot (SRM) → Ta'minotchilar → Ma'lumotnoma

Bu yerda iPack hali ishlamagan, lekin kelajakda ishlashi mumkin bo'lgan potensial ta'minotchilar saqlanadi. Ro'yxat tepasida **Faoliyati** (mahsulot turi) filtri bor: masalan "Yog'och" ni tanlab, faqat taxta sotuvchilarni ko'rasiz.

1. **Yangi** tugmasini bosing.
2. Korxona / Do'kon = `Namangan yog'och bozori, 12-do'kon`, Maxsulot turi = `Yog'och`, Davlat = `O'zbekiston`, Viloyat = `Namangan`.
3. Aloqa uchun, Lavozimi, Telefon va Izoh (masalan "100\*50 taxta, naqd, 54 000 so'm atrofida") ni yozing va saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Korxona / Do'kon | Nomi | Namangan yog'och bozori, 12-do'kon |
| Maxsulot turi | Nima sotadi (filtr shu bo'yicha) | Yog'och / Karton / Metiz |
| Davlat, Viloyat, Region | Joylashuv | O'zbekiston, Namangan |
| Aloqa uchun, Lavozimi, Telefon | Kim bilan bog'lanish | Sotuvchi, +998 ... |
| Izoh | Narx, shartlar, taassurot | "Taxta sifati o'rtacha" |

> **Diqqat:** Ma'lumotnomadagi yozuv hujjatlarda tanlanmaydi. Korxona bilan ishlash boshlanganda uni **Ta'minotchilar** sahifasiga alohida qo'shing.

**Natija:** Xarid bo'limining potensial ta'minotchilar bazasi bir joyda turadi; tender taqqoslashga nomzodlar shu yerdan olinadi.

## 3.4 HS (ТНВЭД) kodlar

**Kim:** Admin yoki HS kod ruxsati bor xaridchi   **Menyu:** menyuda havola yo'q — brauzer manzil satriga `/HSCode` yozib oching.

Ro'yxatda HSCODE va qisqacha tavsif ustunlari bor; ro'yxat serverda sahifalanadi, kerakli kodni qidiruv orqali toping.

1. `/HSCode` sahifasida **Yangi** (Create) ni bosing.
2. HsCODE = `4407` bilan boshlanadigan 10 xonali kod (arralangan yog'och uchun, misol), batafsil ma'lumot = `Arralangan taxta, qarag'ay`. Saqlang.
3. Karton uchun ham kod kiriting (masalan `4808...` guruhidan, misol).

> **Diqqat:** Mahsulot yoki shartnoma qatorida ishlatilgan kodni o'chirib bo'lmaydi: "Bu ТНВЭД kod mahsulot yoki hujjatlarda ishlatilgan, o'chirib bo'lmaydi." xabari chiqadi.
> **Diqqat:** Ruxsati yo'q foydalanuvchi `/HSCode/Create` yoki `/HSCode/Edit/...` ni ochsa, "Sizga ruxsat berilmagan!" sahifasi chiqadi.

**Natija:** Kod xarid shartnomasi qatoridagi "HS Code" ustunida tanlanadi.

## 3.5 Xarid arizasi (talabnoma)

**Kim:** ariza beruvchi — istalgan bo'lim xodimi (Ishlab chiqarish bo'limi, Texnologiya bo'limi); tasdiqlovchi — rahbar yoki Admin   **Menyu:** Ta'minot (SRM) → Xarid jarayoni → Xarid arizalari

Ariza **Indirect** xarid uchun ishlatiladi. iPack misoli: Ishlab chiqarish bo'limi sexga 2 ta pnevmatik mix qoqish apparati ("Jihoz" kategoriyasi) so'raydi.

### Holatlar

| Holat | Ma'nosi | Kim nima qila oladi |
|---|---|---|
| Draft | Qoralama, yangi yaratilgan | **Tasdiqlashga yuborish** |
| Submitted | Tasdiqlash kutilmoqda | **Tasdiqlash** yoki **Rad etish** (sabab bilan) |
| Approved | Tasdiqlangan (yashil) | **Buyurtmaga aylantirish** |
| Rejected | Rad etilgan (qizil), sabab ko'rinadi | Hech narsa (yangi ariza yozing) |

O'tishlar: `Draft → Submitted → Approved` yoki `Draft → Submitted → Rejected`. Ariza har doim Draft holatida yaratiladi.

### Ariza yaratish

1. **Yangi ariza** tugmasini bosing.
2. Ariza raqamini **bo'sh qoldiring** — tizim `PR-2026-NNNN` raqamini o'zi beradi.
3. Ariza sanasi = `02.11.2026`, Bo'lim = `Ishlab chiqarish bo'limi`, So'rovchi = sex boshlig'i, Aloqa telefon, Xarid qilish maqsadi = `Noyabr buyurtmasi: paddon yig'ish tezligini oshirish`.
4. Mahsulotlar jadvaliga qator qo'shing: Mahsulot = `Mix qoqish apparati (pnevmatik)`, Miqdor `2`, Birlik `dona`, Taxminiy narx `3 500 000`, Prioritet `High`, Kerak bo'lgan sana `10.11.2026`, Izoh.
5. Saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ariza raqami | Bo'sh qoldiring — avtomatik | PR-2026-0007 |
| Ariza sanasi | Ariza kuni | 02.11.2026 |
| Bo'lim / So'rovchi | Qaysi bo'lim, kim so'rayapti | Ishlab chiqarish bo'limi / sex boshlig'i |
| Aloqa telefon | So'rovchi telefoni | +998 ... |
| Xarid qilish maqsadi | Nega kerak | Noyabr buyurtmasi uchun |
| Mahsulot, Miqdor, Birlik | Nima va qancha | Mix qoqish apparati, 2 dona |
| Tahminiy narx, Summa | Taxminiy narx; summa avtomatik | 3 500 000 → 7 000 000 |
| Prioritet | High / Medium / Low | High |
| Kerak bo'lgan sana | Qachongacha kerak | 10.11.2026 |

### Yuborish, tasdiqlash, rad etish

1. Arizaning **Details** sahifasini oching va **Tasdiqlashga yuborish** ni bosing → "Talabnoma tasdiqlashga yuborildi." xabari, holat Submitted.
2. Tasdiqlovchi shu sahifada **Tasdiqlash** ni bosadi → "Talabnoma tasdiqlandi.", holat Approved. "Tasdiqlash jarayoni" jadvalidagi barcha bosqichlar bir vaqtda Approved bo'ladi, Tasdiqlovchi ustunida tasdiqlagan foydalanuvchi F.I.Sh. turadi.
3. Rad etish uchun "Rad etish sababi" maydoniga sabab yozing (masalan `Hozircha mavjud apparatlar yetarli`) va **Rad etish** ni bosing → "Talabnoma rad etildi.", sahifada sabab ko'rsatiladi.

> **Diqqat:** Sababsiz rad etib bo'lmaydi — maydon majburiy.
> **Diqqat:** Faqat Approved arizani buyurtmaga aylantirish mumkin. Boshqa holatda "Faqat tasdiqlangan talabnomani xarid buyurtmasiga aylantirish mumkin." xabari chiqadi.
> **Diqqat:** Tizimda bitta tasdiq bosqichi bor (biznes qarori). Ko'p bosqichli (rol bo'yicha) tasdiqlash yo'q.
> **Maslahat:** Ro'yxatdagi "Status bo'yicha filtrlash" orqali tasdiqlovchi faqat Submitted arizalarni ko'rib, navbat bilan ishlaydi.

**Natija:** Tasdiqlangan arizada **Buyurtmaga aylantirish** tugmasi paydo bo'ladi. Ro'yxatda ariza raqami, sana, bo'lim, so'rovchi, mahsulotlar soni, jami summa va status ko'rinadi.

## 3.6 Xarid buyurtmasi (PO)

**Kim:** Xaridchi   **Menyu:** Ta'minot (SRM) → Xarid jarayoni → Xarid buyurtmalari

Ro'yxat ustunlari: Buyurtma raqami, Sana, Ta'minotchi, Turi (Direct/Indirect), Valyuta, **Summa (QQS bilan)**. "Xarid turi" filtri bor. Tepada ikkita tugma: **Yangi buyurtma (Direct)** va **Xarid arizasi (Indirect)**.

### Raqamlash

Buyurtma raqami bo'sh qoldirilsa `PO-YYYY-MM-NNNN` ko'rinishida beriladi (masalan `PO-2026-11-0003`). O'chirilgan buyurtma raqami qayta berilmaydi — keyingi buyurtma navbatdagi raqamni oladi.

### A) Direct buyurtma: Wood Nam Import'dan taxta

1. **Yangi buyurtma (Direct)** ni bosing.
2. Buyurtma raqami = bo'sh, Sana = `03.11.2026`, Ta'minotchi = `MChJ "Wood Nam Import"`, Valyuta = `UZS`, QQS % = `12`.
3. **Qo'shish** bilan qator kiriting: `Yog'och 100*50*6000`, Miqdor `3200`, Birlik `dona`, Narx `42000`.
4. Saqlang.

Hisob: 2 600 × 54 000 = 140 400 000; QQS 12% = 16 848 000; **Jami umumiy summa = 157 248 000 so'm**.

### B) Sotuv buyurtmasidan generatsiya (BOM bo'yicha ehtiyoj)

Direct buyurtma formasida "Sotuv buyurtmalaridan generatsiya qilish" bloki bor. U tanlangan sotuv buyurtmalaridagi tayyor mahsulotlar uchun BOM'dan xomashyo ehtiyojini hisoblab, qatorlarni o'zi to'ldiradi.

1. "Sotuv buyurtmalari (bir yoki bir nechta tanlash mumkin)" ro'yxatidan UzAutoMotors Powertrain'ning noyabr buyurtmasini tanlang.
2. **Generatsiya qilish** ni bosing.
3. Jadvalga karton qatori tushadi: `Karton_2090x1370`, 1 860 m² (3 000 × 0,62). "Hisob tushuntirishi" ustunida qaysi mahsulot va norma bo'yicha hisoblangani yoziladi.
4. Keraksiz qatorlarni (masalan taxta — u Wood Nam buyurtmasiga alohida yozilgan) o'chiring, miqdorni zahira bilan `3800` ga oshiring, Ta'minotchi = `IMPORT EVRO GRAND`, Narx = `6500`, QQS % = `12`. Saqlang.

Hisob: 2 000 × 6 500 = 13 000 000; QQS 1 560 000; **jami 14 560 000 so'm**.

> **Diqqat:** Mahsulot uchun faol BOM bo'lmasa, "BOM asosida xom ashyo ehtiyojlari topilmadi..." xabari chiqadi. Avval Muhandislik bo'limida norma/BOM tayyor bo'lishi kerak.
> **Maslahat:** Buyurtma sanasi qaysi oyga tushsa, ehtiyoj o'sha oyning BOM versiyasidan hisoblanadi.

### C) Indirect buyurtma: tasdiqlangan arizadan

1. 3.5-bo'limdagi tasdiqlangan arizada **Buyurtmaga aylantirish** ni bosing.
2. Ochilgan formada "Indirect Purchase" bloki va arizadagi mahsulotlar avtomatik to'ldirilgan bo'ladi.
3. Ta'minotchi = `Mexmash MChJ`, Valyuta = `UZS`, QQS % = `12`, haqiqiy narxni yozing va saqlang.

### Buyurtma qatorlari

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Buyurtma raqami | Bo'sh — avtomatik | PO-2026-11-0003 |
| Sana | Buyurtma sanasi | 03.11.2026 |
| Ta'minotchi, Shartnoma | Kimdan; shartnoma bo'lsa tanlanadi | Wood Nam Import |
| Valyuta | UZS, USD, RUB, KRW | UZS |
| Mahsulot kodi / nomi | Xomashyo | Yog'och 100\*50\*6000 |
| Miqdor, Birlik | Qancha | 2 600 dona |
| Narx | Birlik narxi QQSsiz | 54 000 |
| MOQ | Ta'minotchining minimal buyurtma miqdori (ma'lumot uchun) | 500 |
| QQS %, QQS miqdori | Stavka; summa avtomatik | 12% → 16 128 000 |
| Jami | Qator summasi | 140 400 000 |
| Izoh, Hisob tushuntirishi | Erkin izoh; generatsiyadan kelgan izoh | — |

> **Diqqat:** Saqlashda xato bo'lsa (masalan ta'minotchi tanlanmagan), forma kiritilgan qatorlar bilan qayta ochiladi — qatorlar yo'qolmaydi. Xatoni tuzatib qayta saqlang.
> **Maslahat:** O'rtadagi qatorni qizil tugma bilan o'chirsangiz, qolgan qatorlar saqlanadi.

**Details** sahifasida: Buyurtma ma'lumotlari (Korxona, Ta'minotchi, Shartnoma, Valyuta, QQS %), qatorlar (Narx, Narx (QQS bilan), QQS summa, Jami (QQS bilan)) va jami: "Jami summa", "Jami QQS", "Jami (QQS bilan)". **Tahrirlash** va **O'chirish** (tasdiqlash bilan) mavjud.

**Natija:** Buyurtma ro'yxatda QQS bilan summada turadi, **Buyurtma kuzatuv** da "Created" holatida paydo bo'ladi va invoys formasida tanlanadi.

## 3.7 Xarid shartnomasi

**Kim:** Xaridchi, Xarid bo'limi boshlig'i   **Menyu:** Ta'minot (SRM) → Xarid jarayoni → Xarid shartnomalari

Ro'yxatda Shartnoma raqami, Sana, Ta'minotchi, Turi, To'lov shartlari, Shartnoma summasi ustunlari; "Xarid turi" va sana (Dan/Gacha) filtri. Tugmalar: **Yangi shartnoma (Direct)** va **Yangi shartnoma (Indirect)**.

1. **Yangi shartnoma (Direct)** ni bosing.
2. Shartnoma raqami = bo'sh (avtomatik `PC-2026-NNNN`), Sana = `03.11.2026`, Ta'minotchi = `MChJ "Wood Nam Import"`, Valyuta = `UZS`, Muddat = `31.12.2026`, To'lov shartlari = `50% oldindan, 50% yetkazilgandan keyin 15 kun ichida`, Incoterms (agar import bo'lsa, masalan `DAP Andijon`).
3. Qatorlar: `Yog'och 100*50*6000`, Miqdor `3200`, Birlik `dona`, Narx `42000`, QQS (%) `12`, HS Code = taxta kodi. Mahsulot tanlovida nomning bir qismini (`Yog'och`) yozib qidiring.
4. "Shartnoma hujjati (PDF)" ga imzolangan shartnoma skanini yuklang.
5. Saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Shartnoma raqami | Bo'sh — avtomatik | PC-2026-0012 |
| Sana, Muddat | Imzolangan kun, amal qilish muddati | 03.11.2026, 31.12.2026 |
| Ta'minotchi, Valyuta | Kim bilan, qaysi valyutada | Wood Nam Import, UZS |
| To'lov shartlari | Matn | 50% oldindan |
| Incoterms, IDN | Yetkazish sharti; import identifikatori (bo'lsa) | DAP Andijon |
| Mahsulot, Miqdor, Birlik, Narx | Shartnoma qatori | 2 600 dona × 54 000 |
| MOQ | Minimal partiya | 500 |
| QQS (%) | Qator stavkasi | 12 |
| HS Code | ТНВЭД kodi | 4407... |
| Shartnoma hujjati (PDF) | Faqat PDF | skan.pdf |

Shartnoma summasi qatorlardagi QQS bilan hisoblanadi: 140 400 000 + 16 848 000 = **157 248 000 so'm**.

> **Diqqat:** Hujjat sifatida faqat haqiqiy PDF qabul qilinadi. .jpg yoki .xlsx yuklansa "Faqat PDF fayl yuklash mumkin." yoki "Fayl PDF emas." xabari chiqadi, kiritilgan qatorlar saqlanib qoladi.
> **Maslahat:** Tahrirlashda birlik va HS kod saqlanib qoladi — faqat o'zgartirmoqchi bo'lgan maydonni tuzating.

**Details** sahifasi: "Shartnoma ma'lumotlari", qatorlar, "Xulosa" (QQSsiz summa, QQS, Jami QQS bilan), "Bog'langan invoyslar" (invoys raqami, sana, summa, holati), "Shartnoma hujjatlari" (PDF yuklab olish) va bog'liq buyurtma/invoyslar soni. **Akt-sverki** tugmasi ta'minotchi bilan shu shartnoma bo'yicha solishtirma dalolatnomani ochadi (Moliya bo'limi).

**Natija:** Shartnoma invoys formasida tanlanadi; invoyslar kiritilgan sari shartnoma bajarilishi (balansi) kuzatiladi. Kuzatuvda buyurtma "Contract Created" holatiga o'tadi.

## 3.8 Xarid invoysi

**Kim:** Xaridchi yoki buxgalter   **Menyu:** Ta'minot (SRM) → Xarid jarayoni → Xarid invoyslari

Ro'yxat ustunlari: Invoys raqami, Sana, Ta'minotchi, Buyurtma raqami, Shartnoma raqami, Turi va **Invoys summasi (QQS bilan)** (ostida "QQSsiz summa + QQS 12%" yozuvi). Filtrlar: Xarid turi, sana Dan/Gacha.

Wood Nam Import taxtani ikki partiyada yetkazadi. Birinchi partiya invoysi:

1. **Yangi invoys** ni bosing.
2. Invoys raqami = bo'sh (avtomatik `PI-2026-NNNN`) yoki ta'minotchi hisob-fakturasidagi raqam; Sana = `06.11.2026`.
3. Buyurtma = `PO-2026-11-0003`, Ta'minotchi = `Wood Nam Import`, Shartnoma = `PC-2026-0012`, Valyuta = `UZS`.
4. **Buyurtma mahsulotlarini yuklash** ni bosing — qatorlar buyurtmadan tushadi. Miqdorni haqiqatda kelganiga moslang: `2000` dona × `42000`.
5. "QQS % (ko'rinish uchun)" maydoni shartnomadan olingan 12 ni ko'rsatadi.
6. "Invoys hujjati (PDF)" ga hisob-faktura skanini yuklang va saqlang.

Hisob: 1 600 × 54 000 = 86 400 000; QQS 10 368 000; **invoys summasi 96 768 000 so'm**. Ikkinchi partiya (1 000 dona) uchun alohida invoys: 54 000 000 + 6 480 000 = 60 480 000 so'm.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Invoys raqami | Bo'sh — avtomatik yoki ta'minotchi raqami. Butun tizimda takrorlanmaydi | PI-2026-0031 |
| Sana | Hisob-faktura sanasi (valyuta kursi shu sana bo'yicha) | 06.11.2026 |
| Buyurtma | Qaysi PO bo'yicha | PO-2026-11-0003 |
| Ta'minotchi, Shartnoma | Kimdan, qaysi shartnoma | Wood Nam Import, PC-2026-0012 |
| Valyuta | Invoys valyutasi | UZS |
| Mahsulot, Miqdor, Birlik, Narx | Haqiqatda kelgan | 1 600 dona × 54 000 |
| QQS % (ko'rinish uchun) | Shartnomadan olinadi | 12 |
| Invoys hujjati (PDF) | Faqat PDF | hisob-faktura.pdf |

> **Diqqat:** Invoys raqami butun tizimda yagona. Takrorlansa "Shu raqamli invoys allaqachon mavjud (o'chirilganlar orasida ham bo'lishi mumkin)." xabari chiqadi — boshqa ta'minotchi tanlansa ham.
> **Diqqat:** Invoys summasi **QQS bilan** saqlanadi (biznes qarori). Valyutadagi invoys hisobotlarda hujjat sanasidagi CBU kursi bilan so'mga o'giriladi.
> **Maslahat:** Invoysni ombor kirimidan oldin tekshirib qo'ying: kirim qabul qilingandan keyin miqdorni o'zgartirish murakkablashadi.

### Moliyadagi ko'zgu yozuvi

Invoys saqlanganda **Moliya → Boshqaruv → Tranzaksiyalar** da avtomatik yozuv paydo bo'ladi: chiqim, hisob "Kreditorlar qarzi" (invoys valyutasida), kategoriya nomida "Xarid", kontragent = ta'minotchi, maqsad = "Xarid invoysi #PI-2026-0031", summa = 96 768 000.

- Invoys tahrirlanganda **yangi yozuv qo'shilmaydi** — mavjud yozuv summasi yangilanadi.
- Ko'zgu yozuvi faqat invoys saqlanganda yangilanadi. Keyin yaratilgan ombor kirimi summasi u yerga avtomatik qayta yozilmaydi; farq bo'lsa invoysni qayta saqlang.

### Shartnoma balansi

Invoysning **Details** sahifasida "Jami summa (QQSsiz)", "QQS (%)", "Jami QQS", "Jami (QQS bilan)" va **Shartnoma balansi** bloki chiqadi: shartnoma bo'yicha qancha invoys qilingani va **Bajarilish %**. Misolda birinchi invoysdan keyin taxta bo'yicha bajarilish 1 600 / 2 600 = 61,5%, ikkinchisidan keyin 100%.

**Natija:** Invoys ombor kirimi formasidagi "Invoys" ro'yxatida paydo bo'ladi; Moliyada ta'minotchiga qarz yoziladi; Buyurtma kuzatuvda buyurtma "Invoiced" holatiga o'tadi; SRM Dashboard'dagi "Jami xarajat" oshadi.

## 3.9 Buyurtma kuzatuvi

**Kim:** Xaridchi, rahbar   **Menyu:** Ta'minot (SRM) → Kuzatuv va tender → Buyurtma kuzatuv

Har bir xarid buyurtmasi zanjirning qaysi bosqichida ekanini ko'rsatadi. Ustunlar: Buyurtma raqami, Ta'minotchi, Sana, Turi, Shartnoma, Invoys, Ombor, Status. Filtrlar: Xarid turi va Status (sahifani qayta yuklamasdan ishlaydi).

| Status | Qachon | iPack misoli |
|---|---|---|
| Created | Buyurtma bor, shartnoma ham, invoys ham yo'q | IMPORT EVRO GRAND buyurtmasi yaratilgan kuni |
| Contract Created | Buyurtmaga shartnoma bog'langan | Wood Nam bilan PC-2026-0012 imzolandi |
| Invoiced | Buyurtma bo'yicha invoys kiritilgan | PI-2026-0031 kiritildi, yuk hali omborga qabul qilinmagan |
| Received | Invoys bo'yicha ombor kirimi qabul qilingan | Omborchi taxtani Xomashyo omboriga qabul qildi |

Holat hujjatlardan avtomatik hisoblanadi — uni qo'lda o'zgartirib bo'lmaydi. **Details** sahifasida "Buyurtma holati" bloki uchta belgini ko'rsatadi: **Shartnoma yaratildi**, **Invoys yaratildi**, **Omborga keldi**; pastda bog'liq Shartnomalar, Invoyslar va Ombor kirimlari ro'yxati (hujjat raqami, sana, summa, **Ko'rish** tugmasi).

> **Maslahat:** Har kuni ertalab Status = `Invoiced` filtrini qo'ying: bu "ta'minotchi yukni jo'natgan, lekin ombor hali qabul qilmagan" buyurtmalar ro'yxati. Omborchi bilan shu ro'yxat bo'yicha gaplashing.

**Natija:** Xarid bo'limi qaysi yuk qayerda qolib ketganini bir sahifada ko'radi.

## 3.10 Tender taqqoslash

**Kim:** Xaridchi   **Menyu:** Ta'minot (SRM) → Kuzatuv va tender → Tender taqqoslash

Bir material bo'yicha bir nechta ta'minotchi takliflarini solishtirish uchun. **Yangi taklif qo'shish** orqali ta'minotchi, material, taklif narxi, MOQ, yetkazib berish muddati (kun) va to'lov shartlari kiritiladi. "Takliflar ro'yxati" va "Taqqoslash grafigi" eng arzon/eng tez taklifni ko'rsatadi; afzal taklif belgilanadi. Natijani **PDF ga eksport qilish** mumkin.

iPack misoli: taxta bo'yicha Wood Nam Import (54 000, 5 kun) va WOODLAND BIZNES MCHJ (53 000, 12 kun) takliflarini solishtirib, noyabr buyurtmasi muddati qisqaligi uchun Wood Nam tanlanadi.

## 3.11 Hisobotlar va dashboardlar

### SRM Dashboard

**Kim:** Xaridchi, rahbar   **Menyu:** Ta'minot (SRM) → Hisobotlar → SRM Dashboard (yoki Dashboard → SRM Dashboard)

Standart davr — oxirgi 1 oy. "Dan sana" / "Gacha sana" ni o'zgartirib **Ko'rsatish** ni bosing; "Gacha" sanasining oxirgi kuni to'liq hisobga kiradi.

| Ko'rsatkich | Nimani ko'rsatadi |
|---|---|
| Ta'minotchilar soni | Faol ta'minotchilar |
| Jami xarajat | Davrdagi **barcha** xarid invoyslari summasi (QQS bilan) va invoyslar soni |
| Qarzdorlik (kreditor) | Ta'minotchilarga jami qarzimiz (davr oxiri holatiga) |
| Vaqtida yetkazish | Yo'l varaqalarida rejadagi va haqiqiy yetkazish sanasi solishtiriladi (%) |
| Xarajat ta'minotchi bo'yicha (top 15) | Eng ko'p xarid qilingan 15 ta ta'minotchi |
| Ta'minotchilar bo'yicha qarzimiz (top 15) | Eng katta qarz turgan 15 ta ta'minotchi |

iPack noyabr misoli: Jami xarajat = 96 768 000 + 60 480 000 (Wood Nam) + 14 560 000 (IMPORT EVRO GRAND) = 171 808 000 so'm (boshqa xarid bo'lmasa).

### Kreditorlik qarzi

Kreditorlik qarzi Moliya bilan **yagona hisob** bo'yicha olinadi: boshlang'ich qoldiq + invoyslar (yoki qabul qilingan kirimlar) − ta'minotchiga to'lovlar. Shuning uchun SRM Dashboard'dagi "Kreditorlik" va Moliya dashboard'idagi ta'minotchilar qarzi bir xil. Dashboard faqat musbat qarzni ko'rsatadi (oldindan to'lov qilingan ta'minotchi qarz ro'yxatida chiqmaydi).

Misol: Wood Nam Import'ga ikki invoys = 157 248 000. Buxgalter Moliyada 9001 Hisob raqamdan 100 000 000 so'm to'lov qildi → qarz **57 248 000 so'm**. To'lov kiritish tartibi Moliya bobida.

### Boshqa SRM hisobotlari

**Ta'minot (SRM) → Hisobotlar** guruhida: **Coverage** (zahira necha kunga yetadi), **ABC / XYZ** (materiallarni qiymat va barqarorlik bo'yicha segmentlash), **Xavfsiz zahira** (minimal zahira hisobi). Bu hisobotlar qaysi materialni birinchi navbatda buyurtma qilishni tanlashga yordam beradi.

### Eksport

Buyurtmalar, shartnomalar, invoyslar va arizalar ro'yxatlarida qidiruv va saralash serverda bajariladi. **Excel** tugmasi joriy filtr va qidiruvga mos **barcha** yozuvlarni yuklaydi (faqat ekrandagi sahifani emas).

## 3.12 Ruxsatlar

- Chap menyudagi "Ta'minot (SRM)" bo'limi faqat tegishli ruxsati (Supplier, PInvoices, PurchaseOrderTracking va h.k.) bor foydalanuvchiga ko'rinadi.
- Ruxsati yo'q foydalanuvchi sahifani URL orqali ochsa ham ("/PInvoices/Create", "/PContracts/Edit/...", PDF yuklab olish) "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- iPack'da odatda: **Xaridchi** — ta'minot bo'limi to'liq; **Omborchi** — SRM ko'rinmaydi, faqat ombor; **Ta'sischi** — dashboardlar va tasdiqlash.

## 3.13 Tez-tez uchraydigan savollar va muammolar

**"Buyurtmaga aylantirish" tugmasi yo'q.** Ariza hali Approved emas. Avval **Tasdiqlashga yuborish**, keyin tasdiqlovchi **Tasdiqlash** ni bosishi kerak.

**Indirect buyurtmada kerakli mahsulot ro'yxatda chiqmayapti.** Mahsulot kategoriyasida "Sarf materiali" yoki "Xarid qilishga ruxsat" belgisi yo'q, yoki u tayyor mahsulot kategoriyasida. Muhandislik bo'limida kategoriyani to'g'rilang.

**Generatsiya "BOM asosida ehtiyoj topilmadi" deyapti.** Tanlangan sotuv buyurtmasidagi mahsulot (masalan iPack B6.1) uchun faol BOM yo'q yoki BOM'da xomashyo yo'q. Texnologga murojaat qiling.

**Invoys saqlanmayapti: "Shu raqamli invoys allaqachon mavjud".** Ta'minotchilar har xil bo'lsa ham invoys raqami tizimda yagona. Raqam oxiriga ta'minotchi qisqartmasini qo'shing (masalan `123-WNI`) yoki raqamni bo'sh qoldiring.

**Shartnomaga skan yuklanmayapti.** Faqat haqiqiy PDF qabul qilinadi. Rasmni (jpg) avval PDF ga aylantiring.

**Moliyadagi qarz invoysdan farq qilyapti.** Invoys tahrirlangan, lekin ko'zgu yozuvi faqat invoys saqlanganda yangilanadi. Invoysni ochib qayta saqlang. Shuningdek, to'lovlar to'g'ri ta'minotchiga bog'langanini tekshiring.

**Buyurtma kuzatuvida "Invoiced" turibdi, yuk esa allaqachon omborda.** Omborchi kirim yaratgan, lekin **Qabul qilish** ni bosmagan. Kuzatuv faqat qabul qilingan kirimni "Received" deb hisoblaydi.

**HS kod sahifasi menyuda yo'q.** To'g'ri — u faqat `/HSCode` manzili orqali ochiladi.

**O'chirilgan buyurtma raqami qayta berilmadi.** Bu ataylab qilingan: raqamlar takrorlanmasligi uchun o'chirilganlar ham hisobga olinadi.
