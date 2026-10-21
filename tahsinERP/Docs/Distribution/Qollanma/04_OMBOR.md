# 4. Ombor (distributsiyada)

Bu bob distributsiya bo'limidagi ombor sahifalarini tushuntiradi:
- qoldiqlar: haqiqiy, rezerv, mavjud va minimaldan pastlari;
- inventarizatsiya (boshlang'ich va nazorat sanog'i);
- boshqa ombordan kelgan ko'chirmani haqiqiy miqdor bilan qabul qilish va farq dalolatnomasi;
- hisobdan chiqarish (rahbar tasdig'i bilan);
- pereso'rtitsa (bir tovar o'rniga boshqasi);
- qo'shimcha xarajatlar (landed cost) — yo'l haqi va bojxonani tannarxga qo'shish;
- minimal qoldiq ogohlantirishi va "Buyurtma taklifi" (yetkazib beruvchiga xarid buyurtmasi).

Asosiy foydalanuvchilar: **omborchi** (demo_ombor), **rahbar** (demo_rahbar). Tannarx bilan bog'liq sahifalarni faqat "tannarxni ko'rish" huquqi (DstSeeCost) borlar ko'radi.

**Namunaviy ssenariy (2026-yil noyabr, umumiy demo):**
- DEMO Asosiy ombor (Toshkent) — savdo ombori, mas'ul — demo_ombor; DEMO Filial ombor (Samarqand) — savdo ombori.
- Kirim (QQSsiz tannarx): 1 000 qop sement × 40 000, 500 m² kafel × 70 000, 200 list gipsokarton × 50 000 so'm.
- 03.11 — Anvar do'koniga 110 qop sement (100 + 10 sovg'a), 04.11 — Bunyodkor qurilishga 165 qop (150 + 15 sovg'a) jo'natildi.
- 05.11 — Anvar do'konining 200 qop (+20 sovg'a) buyurtmasi tasdiqlandi va rezervga olindi.

## 4.1 Asosiy tushunchalar va asosiy Ombor moduli bilan bog'lanish

- **Haqiqiy qoldiq** — ombor hisobidagi miqdor (asosiy Ombor modulidagi qoldiq bilan bir xil).
- **Rezerv** — tasdiqlangan, lekin hali jo'natilmagan buyurtmalar uchun band qilingan miqdor.
- **Mavjud** = haqiqiy − rezerv. Yangi buyurtma, hisobdan chiqarish va pereso'rtitsa faqat **mavjud** qoldiqdan foydalanadi.
- **Minimal qoldiq** — "tugayapti" chegarasi (asosiy birlikda), ombor × tovar bo'yicha. **Maksimal** — xarid taklifi shu darajagacha to'ldiradi (ixtiyoriy).
- **Asosiy birlik** — tovar kartasidagi birlik (sement — qop, kafel — m², gipsokarton — list). Ombor sahifalaridagi barcha miqdorlar asosiy birlikda.
- **FIFO tannarx** — chiqim narxi qo'lda yozilmaydi: eng eski kirim partiyalaridan, QQSsiz.
- **Hujjat holati** — Qoralama → O'tkazilgan → (kerak bo'lsa) Bekor qilingan. O'tkazilgan hujjat o'chirilmaydi, faqat sabab bilan bekor qilinadi.

Distributsiya alohida ombor yuritmaydi — u asosiy **Ombor (SRM)** modulining omborlari, qoldiqlari va FIFO partiyalari bilan ishlaydi:

| Amal | Qayerda bajariladi | Asosiy Ombor modulida nima paydo bo'ladi |
|---|---|---|
| Yetkazib beruvchidan kirim | Ta'minot (SRM): xarid invoysi → kirim → Qabul qilish | Kirim |
| Ombordan omborga ko'chirish (jo'natish) | Omborxona → Ombor → Operatsiyalar → Chiqimlar (qabul qiluvchi ombor bilan) | Chiqim + kutilayotgan kirim |
| Ko'chirmani farq bilan qabul qilish | Distributsiya → Ko'chirmalarni qabul qilish (4.4) | Kirim "qabul qilingan", kerak bo'lsa qaytarish kirimi `DT-…-RET` |
| Buyurtma jo'natish | Distributsiya → Buyurtmalar | Chiqim (FIFO) |
| Mijozdan qaytarish | Distributsiya → Qaytarishlar | Kirim |
| Hisobdan chiqarish | Distributsiya → Hisobdan chiqarish (4.5) | Chiqim `DW-…` |
| Pereso'rtitsa | Distributsiya → Pereso'rtitsa (4.6) | Chiqim `DR-…` + kirim `DR-…-IN` |
| Inventarizatsiya | Distributsiya → Inventarizatsiya (4.3) | `INV_…` kirim (ortiqcha) va chiqim (kamomad) |
| Landed cost | Distributsiya → Qo'shimcha xarajatlar (4.7) | Kirim qatorining birlik narxi oshadi |

Distributsiya hujjatlari asosiy Ombor modulidagi kirim/chiqim izohida `URL: /Distribution/...` havolasi bilan ko'rinadi.

> **Muhim:** Asosiy Ombor modulidagi chiqim yoki ko'chirma, shuningdek CRM savdo hisob-fakturasi tasdig'idagi savdo ombori chiqimi tovarni distributsiya rezervidan pastga tushirsa, u rad etiladi: "<artikul>: distributsiya buyurtmalari uchun X rezervda, chiqimdan keyin qoldiq Y bo'lib qoladi. Avval Distributsiya → Buyurtmalar'da rezervni yeching …". Tovar rostdan kerak bo'lsa, avval buyurtma rezervini yeching (tasdiqni qaytarish yoki bekor qilish).

> **Muhim:** Omborlarning "Savdoda" belgisi va minimal qoldiqlar **Distributsiya → Sozlamalar → Omborlar** da, brak ombori — **Sozlamalar → Umumiy** da beriladi. Buyurtmalar faqat "Savdoda" belgili omborlardan jo'natiladi; brak ombori buyurtmalarga taklif qilinmaydi.

## 4.2 Qoldiqlar

**Kim:** omborchi, rahbar, operator   **Menyu:** Distributsiya → Qoldiqlar

Filtrlar: ombor (bo'sh — **Barcha omborlar** jami), kategoriya, holat:

| Holat filtri | Nimani ko'rsatadi |
|---|---|
| Qoldig'i borlar (standart) | Haqiqiy yoki rezervi noldan farqli tovarlar |
| Hammasi | Barcha tovarlar |
| Minimaldan past | Mavjud qoldiq minimal qoldiqdan kam |
| Rezerv qoldiqdan ko'p | Mavjud manfiy (rezerv haqiqiydan katta) |

Jadval ustunlari: Artikul, Nomi (/ birlik), **Haqiqiy**, **Rezerv**, **Mavjud** (manfiy — qizil, minimaldan past — sariq), **Minimal qoldiq**. Qidiruv — artikul yoki nom bo'yicha. Omboringiz ko'rinish doirasida bo'lmasa, u ro'yxatda chiqmaydi.

**Misol (05.11, DEMO Asosiy ombor):**

| Artikul | Haqiqiy | Rezerv | Mavjud | Minimal qoldiq |
|---|---|---|---|---|
| DEMO-SEM-400 | 725 | 220 | **505** (sariq) | 600 |

Hisob: 1 000 − 110 − 165 = 725 haqiqiy; rezerv 200 + 20 sovg'a = 220; mavjud 725 − 220 = 505 < 600.

> **Eslatma:** Ombor tanlanmasa, minimal qoldiq ham barcha omborlar bo'yicha qo'shilib ko'rsatiladi.

> **Eslatma:** Qoldiqlar sahifasida pul qiymati yo'q. Ombor qiymati (FIFO tannarxda) — Rahbar panelida, faqat tannarx huquqi bilan (5.2).

### Minimal va maksimal qoldiqni berish

**Kim:** rahbar   **Menyu:** Distributsiya → Sozlamalar → "Omborlar" jadvali → ombor qatori

1. Omborni tanlang — pastda "Minimal qoldiq — DEMO Asosiy ombor (Toshkent)" kartasi ochiladi.
2. Tovarni qidiring, **Minimal qoldiq** = `600`, **Maksimal (ixtiyoriy)** = `1500`, ➕.
3. Ro'yxatda "DEMO-SEM-400 Sement M400 — 600 qop → 1500" ko'rinadi. O'chirish — 🗑.

> **Muhim:** Maksimal minimaldan kam bo'lmasin ("Maksimal minimaldan kam bo'lmasin."). Maksimal bo'sh bo'lsa, xarid taklifi minimal × 2 gacha to'ldiradi.

## 4.3 Inventarizatsiya

**Kim:** omborchi (sanaydi), rahbar (yakunlaydi)   **Menyu:** Distributsiya → Inventarizatsiya (yoki bosh sahifadagi plita)

Ikki turdagi sanoq bor:

| Turi | Qachon | Yakunlanganda |
|---|---|---|
| **Boshlang'ich** | Tizimga o'tish (pilot boshi) | Qoldiq **har doim** sanalgan miqdorga tenglanadi: ortiqcha — kirim, kamomad — FIFO chiqim |
| **Nazorat** | Oy oxiri, pilot oxiri, tekshiruv | Faqat taqqoslaydi. "Farqni qoldiqqa yozish" belgilansa — qoldiq ham tenglanadi |

### 4.3.1 Sanoqni ochish

1. **Ombor**, **Turi** (Boshlang'ich / Nazorat), Izoh → **Yangi sanoq**.
2. Hujjat `DI-2026-00001` yaratiladi. Ro'yxatga avtomatik kiradi: shu omborda qoldig'i bor tovarlar + sotuvdagi barcha distributsiya tovarlari.

Ro'yxat ustunlari: Hujjat №, Sana, Ombor, Turi, Holat ("O'tkazilgan · qoldiqqa yozilgan"), Sanalgan (sanalgan qatorlar soni), Farqlar.

> **Muhim:** Bir omborda bir vaqtda faqat bitta ochiq sanoq: "Bu omborda ochiq (qoralama) sanoq bor — avval uni yakunlang yoki bekor qiling."

### 4.3.2 Sanash (uch usul)

**A. Excel varaq ("ko'r" sanoq).**
1. **Sanoq varag'i (Excel)** ni yuklab oling. Ustunlar: Artikul, Nomi, Birlik, Sanalgan, Tannarx (Tannarx — faqat boshlang'ich sanoqda va tannarx huquqi bilan). Tizim qoldig'i varaqda **yo'q** — sanovchi nima bo'lishi kerakligini bilmaydi.
2. Omborda sanab "Sanalgan" ustunini **asosiy birlikda** to'ldiring. Yo'q tovarga `0` yozing.
3. Faylni tanlab **Yuklash**. Ro'yxatda yo'q artikul bo'lsa, qator qo'shiladi; noma'lum artikul yoki takror — xato, hech narsa yozilmaydi.

**B. Ekranda.** Jadvaldagi "Sanalgan" katakchalarini to'ldirib **Saqlash**. Ro'yxatda yo'q tovar — **Artikul bilan qo'shish** maydoni.

**C. Telegram Mini App (shtrix-kod).** Omborchi (yoki rahbar) botda **📦 Ombor** → **📦 Ombor ilovasi** → ochiq sanoqni tanlaydi → har skanerlash sanalganga qo'shiladi (qutidagi shtrix-kod — quti koeffitsienti bilan, masalan kafel qutisi = 1,44 m²). Mini App'da tizim qoldig'i ko'rsatilmaydi. Qatorga aniq miqdorni ham yozish mumkin. Yakunlash — faqat web'da.

Ekrandagi jadval ustunlari: Tovar, Birlik, **Sanalgan**, **Tannarx** (faqat boshlang'ich sanoqda va tannarx huquqi bilan), Izoh. Yuqorida: "Sanalgan: 12 / 24".

> **Eslatma:** Sanoq davomida (qoralamada) web sahifada ham **Tizimda** va **Farq** ustunlari ko'rinmaydi — sanoq uch usulda ham "ko'r". Ular sanoq o'tkazilgandan keyin chiqadi (farqli qatorlar sariq, farqlar soni kamomad/ortiqcha bilan).

> **Eslatma:** Tannarx huquqi bo'lmagan foydalanuvchida "Tannarx" ustuni yo'q: Excel varaqda ham chiqmaydi, import qilingan faylda bo'lsa e'tiborsiz qoldiriladi, saqlashda mavjud qiymat o'zgarmaydi.

### 4.3.3 Yakunlash

1. Barcha qatorlar sanalganini tekshiring: bo'sh qator bo'lsa — "{n} ta qator sanalmagan. Yo'q tovar uchun 0 yozing."
2. Nazorat sanog'ida qoldiqni tuzatish kerak bo'lsa — **Farqni qoldiqqa yozish** belgisini qo'ying.
3. **Yakunlash va qoldiqni tenglash** (boshlang'ich) yoki **Yakunlash** (nazorat) → tasdiq "Sanoqni yakunlaysizmi? Keyin o'zgartirib bo'lmaydi."

**Natija:** "Tizimda" ustuni yakunlash paytidagi qoldiq bilan qotib qoladi, "Tizimda" va "Farq" ustunlari endi ko'rinadi. Qoldiqqa yozilsa, asosiy Ombor modulida bitta `INV_…` kirim (ortiqcha) va bitta chiqim (kamomad) paydo bo'ladi.

| Farq | Qanday yoziladi | Narx |
|---|---|---|
| Ortiqcha | Kirim | "Tannarx" ustunidagi narx; bo'sh bo'lsa — tovarning oxirgi kirim narxi (QQSsiz) |
| Kamomad | Chiqim | FIFO tannarx |

**Misol — boshlang'ich sanoq (30.10.2026, DEMO Asosiy ombor):**

| Tovar | Tizimda | Sanalgan | Farq | Pul qiymati |
|---|---|---|---|---|
| DEMO-SEM-400 Sement M400, qop | 980 | 1 000 | +20 | 20 × 40 000 = 800 000 (kirim) |
| DEMO-KAF-3030 Kafel 30×30, m² | 500 | 498 | −2 | 2 × 70 000 = 140 000 (FIFO chiqim) |
| DEMO-GIP-125 Gipsokarton 12,5 mm, list | 200 | 200 | 0 | — |

Yakunlangandan keyin qoldiq: sement 1 000, kafel 498, gipsokarton 200.

> **Muhim — oyiga bir marta:** "Farqni qoldiqqa yozish"li nazorat sanog'i har ombor uchun **kalendar oyida bir marta**. Ikkinchi urinishda: "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi." Bu qoida asosiy Ombor modulidagi inventarizatsiya bilan umumiy. Boshlang'ich sanoq oylik qulfni olmaydi — shu oyning o'zida "farqni yozish"li nazorat sanog'ini ham o'tkazish mumkin. Shu oyda yana tekshirish kerak bo'lsa — nazorat sanog'ini **belgisiz** (faqat taqqoslash) yakunlang.

> **Muhim:** Sanoq ochiqligida sanalgan tovar shu ombordan chiqmaydi (jo'natish, hisobdan chiqarish, pereso'rtitsa): "… inventarizatsiyada sanalgan (INV-…) — sanoq o'tkazilmaguncha yoki bekor qilinmaguncha ombordan chiqarilmaydi". Hali sanalmagan tovar chiqariladi. Sanoq paytida shu omborga kirim qilmaslikka harakat qiling — "Tizimda" ustuni yakunlash paytidagi qoldiq bilan olinadi.

> **Eslatma:** Ro'yxatga kirmagan tovar qoldig'iga tegilmaydi (boshlang'ich sanoqda ham). Yo'q tovar nolga tushirilishi uchun u ro'yxatda "0" bilan turishi kerak.

**Bekor qilish:** qoralama sanoqni "Bekor qilish sababi" bilan **Bekor**. Yakunlangan sanoq bekor qilinmaydi.

**Pilot qabul mezoni:** pilot oxirida nazorat sanog'i (qoldiqqa yozmasdan) "Farqlar: 0" ko'rsatishi kerak. Farq bo'lsa — har biri sababi bilan Xatolar jurnaliga (5.9).

## 4.4 Ko'chirmani qabul qilish va farq dalolatnomasi

**Kim:** qabul qiluvchi omborning mas'uli (Ombor kartasidagi MRP) yoki Admin   **Menyu:** Distributsiya → Ko'chirmalarni qabul qilish

Asosiy Ombor modulida ko'chirma "hammasi yoki hech narsa" qabul qilinadi. Bu sahifada esa **har qator bo'yicha haqiqatda kelgan miqdor** kiritiladi va farq rasmiylashtiriladi.

Oqim:
1. Jo'natuvchi omborchi asosiy Ombor modulida (**Omborxona → Ombor → Operatsiyalar → Chiqimlar**) qabul qiluvchi ombor bilan chiqim yozadi. Jo'natuvchidan qoldiq darhol kamayadi.
2. Qabul qiluvchida kirim "kutilmoqda" holatida paydo bo'ladi.
3. Qabul qiluvchi mas'ul shu sahifada sanab qabul qiladi.

Sahifada ikki jadval: **"Qabul kutayotgan ko'chirmalar"** (Hujjat №, Sana, Jo'natuvchi, Qabul qiluvchi, Qatorlar, Miqdor, **Qabul qilish**) va **"Farq dalolatnomalari"** (Hujjat №, Sana, Jo'natuvchi, Qabul qiluvchi; tannarx huquqi bilan — Kamomad summasi, Shundan yo'qotish, Ortiqcha summasi; farqli qatorlar sariq).

### Qabul qilish qadamlari

1. Ko'chirma qatorida **Qabul qilish**.
2. Har qator uchun:

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Tovar, Shtrix-kodlar | Avtomatik | DEMO-SEM-400 Sement M400 |
| Jo'natilgan | Avtomatik | 100 |
| Kelgan | Haqiqatda kelgan miqdor (standart — jo'natilgani) | 97 |
| Kamomad | Kelgan < jo'natilgan bo'lsa majburiy: **Yo'qotish**, **Jo'natuvchiga qaytarish** yoki **Bo'lish (yo'qotish + qaytarish)** | Yo'qotish |
| Izoh | Ixtiyoriy | "2 qop yirtilgan, 1 qop yuklanmagan" |

3. DLOC omborda — **Joylashuv (qabul qiluvchi ombor)**; qaytarish bo'lsa **Qaytarish joyi (jo'natuvchi ombor)**.
4. **Qabul qilish** → tasdiq "Ko'chirma qabul qilinadi va qoldiq yangilanadi. Davom etasizmi?"

| Holat | Tizim nima qiladi |
|---|---|
| Kelgan = jo'natilgan | Oddiy qabul |
| Kamomad → **Yo'qotish** | Tovar yo'lda yo'qolgan: hech qaysi omborga kirmaydi, summasi dalolatnomada "Shundan yo'qotish" |
| Kamomad → **Jo'natuvchiga qaytarish** | Jo'natilmay qolgan tovar jo'natuvchi omborga o'sha tannarxda kirim qilinadi (`DT-…-RET`) |
| Kamomad → **Bo'lish (yo'qotish + qaytarish)** | Yo'qotish miqdori kiritiladi; u "Shundan yo'qotish"ga tushadi, kamomadning qolgani jo'natuvchiga qaytariladi (`DT-…-RET`) |
| Ortiqcha (kelgan > jo'natilgan) | Ortig'i qabul qiluvchi omborga jo'natuvchi tannarxida kirim |

Turli sabablar bo'lsa (2 qop yo'qoldi, 1 qop yuklanmadi), **Bo'lish (yo'qotish + qaytarish)** ni tanlang va yo'qotish miqdoriga 2 yozing — qolgan 1 qop jo'natuvchiga qaytadi.

**Misol (10.11.2026):** Asosiy ombordan Filial omborga 100 qop sement (tannarx 40 000) va 50 list gipsokarton (50 000) jo'natildi. Samarqandda: sement 97 (kamomad 3 — Yo'qotish), gipsokarton 51 (ortiqcha 1).

| Ko'rsatkich | Hisob | Summa |
|---|---|---|
| Kamomad summasi | 3 × 40 000 | 120 000 |
| Shundan yo'qotish | 3 × 40 000 | 120 000 |
| Ortiqcha summasi | 1 × 50 000 | 50 000 |

Agar sement kamomadi "Jo'natuvchiga qaytarish" tanlanganida edi: Kamomad 120 000, yo'qotish 0, Asosiy omborga 3 qop 40 000 dan qaytib kirim qilinardi.

**Natija:** dalolatnoma `DT-2026-00001` (o'tkazilgan). Filialda +97 qop, +51 list. Asosiy Ombor modulidagi chiqim "Yuborilgan" holatiga o'tadi. Farq bo'lgani uchun rahbarlarga Telegram: "📦 Ko'chirma farqi DT-2026-00001 (…): DEMO Asosiy ombor (Toshkent) → DEMO Filial ombor (Samarqand) Kamomad: 120 000 (yo'qotish 120 000), ortiqcha: 50 000". Summalar faqat tannarx huquqi bor rahbarga; huquqsiz rahbarga faqat qatorlar soni: "Kamomad: 1 qator, ortiqcha: 1 qator".

**Chop etish:** dalolatnoma sahifasida **Chop etish** — "Ko'chirma dalolatnomasi": jo'natuvchi va qabul qiluvchi (hujjat raqamlari bilan), sana, qatorlar (Jo'natilgan, Kelgan, Farq, tannarx huquqi bilan — Birlik tannarxi va Summa, Kamomad, Izoh), imzo joylari "Topshirdi" va "Qabul qildi".

> **Muhim:** Qabul qilishni faqat qabul qiluvchi omborning mas'uli yoki Admin bajaradi: "Qabul qilishni faqat omborning mas'uli (yoki Admin) bajaradi." Ikki kishi bir vaqtda qabul qila olmaydi — ikkinchisiga "Ko'chirma qabul kutmayapti (allaqachon qabul qilingan yoki o'chirilgan)."

**Dalolatnomani bekor qilish:** xato qabul qilingan bo'lsa, dalolatnoma sahifasida **Dalolatnomani bekor qilish** ni bosing va sababni yozing. Qabul va qaytarish kirimi teskari qilinadi, ko'chirma yana "Qabul kutayotgan" ro'yxatiga qaytadi — uni qaytadan to'g'ri qabul qiling. Qabul qilingan tovar qabul qiluvchi ombordan allaqachon chiqarilgan bo'lsa, bekor qilib bo'lmaydi.

> **Eslatma:** Yo'qotish uchun Moliya yoki qarz yozuvi ataylab qilinmaydi — Moliya'ga faqat haqiqiy pul harakati yoziladi. Yo'qotish summasi dalolatnomada qayd qilinadi; aybdordan undirish rahbar qarori bilan ish haqi orqali qo'lda.

## 4.5 Hisobdan chiqarish (rahbar tasdig'i bilan)

**Kim:** omborchi (tuzadi), "hisobdan chiqarishni tasdiqlash" huquqi (DstWriteOffApprove) bor rahbar (tasdiqlaydi)   **Menyu:** Distributsiya → Hisobdan chiqarish

Brak, sinish, kamomad va boshqa sabablar bilan tovarni ombordan chiqarish. Hujjat rahbar tasdiqlaguncha ombor qoldig'iga ta'sir qilmaydi.

```
 Qoralama --(Tasdiqqa yuborish)--> Tasdiq kutilmoqda --(Tasdiqlash va chiqarish)--> Tasdiqlangan (ombordan chiqdi)
     |                                    |                                              |
     +--(Bekor, muallif)                  +--(Rad etish, sabab bilan)                    +--(Bekor qilish — tovar omborga qaytadi)
```

Ro'yxat yorliqlari: Hammasi, Tasdiq kutilmoqda, Qoralama, Tasdiqlangan. Ustunlar: Hujjat №, Sana, Ombor, Sabab, Tuzgan, Holat, Qatorlar, (tannarx huquqi bilan) Tannarx.

### 4.5.1 Hujjat tuzish (omborchi)

1. **Yangi hisobdan chiqarish**.
2. To'ldiring:

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Ombor | Qaysi ombordan | DEMO Asosiy ombor (Toshkent) |
| Sabab | "Hisobdan chiqarish" turidagi sabab (Ma'lumotnomalar → Sabablar) | Brak |
| Izoh | Ixtiyoriy, 300 belgigacha | "Tushirishda qoplar yirtildi" |
| Tovar | Nom yoki artikul bilan qo'shish | DEMO-SEM-400 Sement M400 |
| Mavjud | Avtomatik — rezervdan tashqari qoldiq | 505 |
| Miqdor | Asosiy birlikda | 5 |
| Izoh (qatorda) | Ixtiyoriy | |

3. **Saqlash** → hujjat `DW-2026-00001` (Qoralama).
4. Kerak bo'lsa hujjat kartasida **Foto biriktirish** (JPEG/PNG, 5 MB gacha) — rasm sahifada ko'rinadi; yuborishdan oldin **Foto almashtirish** bilan o'zgartirish mumkin.
5. Hujjat kartasida **Tasdiqqa yuborish**.

**Natija:** rahbarlarga Telegram xabari: "🗑 Hisobdan chiqarish DW-2026-00001 tasdiqni kutmoqda / Ombor: DEMO Asosiy ombor (Toshkent) / Sabab: Brak / Tuzgan: demo_ombor" + tovarlar ro'yxati (15 tagacha) va ✅ Tasdiqlash / ❌ Rad etish tugmalari.

### 4.5.2 Tasdiqlash (rahbar)

Web'da hujjat kartasida **Tasdiqlash va chiqarish** → "Tovar ombordan chiqariladi. Davom etasizmi?" yoki Telegram'da **✅ Tasdiqlash**.

**Natija (misol):** 5 qop FIFO tannarxda chiqariladi: 5 × 40 000 = **200 000 so'm**. Holat "Tasdiqlangan", kartada "Tasdiqlagan: demo_rahbar (vaqt)". Muallifga "✅ Hisobdan chiqarish DW-2026-00001 tasdiqlandi." Botda rahbarga: "✅ Hisobdan chiqarish DW-2026-00001 tasdiqlandi (tannarx 200 000)." Tannarx huquqi bor foydalanuvchi kartada har qatorning Birlik tannarxi va Tannarxini ko'radi.

Rad etish: web'da sabab yozib **Rad etish** yoki Telegram'da **❌ Rad etish** (sabab "Telegram" deb yoziladi). Hujjat "Bekor qilingan", ombor o'zgarmaydi, muallifga "❌ Hisobdan chiqarish DW-… rad etildi: sabab".

| Qoida | Xabar |
|---|---|
| Tasdiqlash huquqi kerak | "Hisobdan chiqarishni tasdiqlash huquqi yo'q." |
| O'z hujjatini tasdiqlab bo'lmaydi (Admin bundan mustasno) | "O'zingiz tuzgan hujjatni boshqa rahbar tasdiqlashi kerak." |
| Avval yuborilishi kerak | "Hujjat hali tasdiqqa yuborilmagan." |
| Rezervdagi tovar chiqarilmaydi | "DEMO-SEM-400 Sement M400: mavjud qoldiq 505, kerak 510 (rezervdagi tovar hisobga olinmaydi)." |
| Ochiq sanoqda sanalgan tovar chiqarilmaydi | "… inventarizatsiyada sanalgan (INV-…) — sanoq o'tkazilmaguncha yoki bekor qilinmaguncha ombordan chiqarilmaydi" |

> **Muhim — rezerv himoyasi:** Mijoz buyurtmasi uchun band qilingan tovarni hisobdan chiqarib bo'lmaydi. Misolda haqiqiy 725, rezerv 220 — 506 qop va undan ko'pini chiqarish rad etiladi. Rostdan ham tovar yo'q bo'lsa, avval buyurtma rezervini kamaytiring (buyurtmani tahrirlash yoki bekor qilish).

> **Eslatma:** Yuborilgan hujjatni muallif yoki tasdiqlovchi tahrirlay oladi, lekin shunda u qayta yuborilishi kerak: "Hujjat tasdiqqa yuborilgan. O'zgartirsangiz, qayta yuborish kerak bo'ladi."

### 4.5.3 Tasdiqlanganni bekor qilish

Xato tasdiqlangan bo'lsa, tasdiqlash huquqi bor foydalanuvchi sabab yozib **Bekor qilish (tovar omborga qaytadi)** ni bosadi. Ombor chiqimi bekor qilinadi, tovar va partiyalar aynan qaytadi.

> **Eslatma:** Hisobdan chiqarish Moliya'ga yozilmaydi — u ombor qiymatini kamaytiradi. Xarajat sifatida aks ettirish kerak bo'lsa, buxgalter Moliya'da qo'lda yozadi.

## 4.6 Pereso'rtitsa

**Kim:** omborchi, rahbar   **Menyu:** Distributsiya → Pereso'rtitsa

Bir tovar (masalan noto'g'ri navda kelgan) chiqim qilinadi, o'rniga boshqasi kirim qilinadi. Chiqimning FIFO tannarxi yangi tovarga o'tadi — **ombor qiymati o'zgarmaydi**.

**Formula:** B tovarning birlik tannarxi = A tovar chiqim summasi ÷ B miqdori.

1. **Yangi pereso'rtitsa**.
2. Ombor, Sabab ("Hisobdan chiqarish" turidagi sabablar), Izoh.
3. **Chiqim (qaysi tovar)** va **Kirim (qaysi tovarga)** ni tanlab ➕; qatorda ikkala miqdorni asosiy birlikda yozing. Bir hujjatda bir nechta juftlik bo'lishi mumkin.
4. **Saqlash** → `DR-2026-00001` (Qoralama). Kerak bo'lsa **Tahrirlash**.
5. DLOC omborda kirim joyini (**Joylashuv**) tanlang → **O'tkazish** → "Ombor qoldig'i o'zgaradi. Davom etasizmi?"

**Misol (misol uchun qo'shimcha tovar "Kafel 30×30 (2-nav)" ochilgan):** 10 quti kafel 2-nav bo'lib chiqdi.

| Qator | Chiqim | Miqdor | Kirim | Miqdor |
|---|---|---|---|---|
| 1 | DEMO-KAF-3030 Kafel 30×30 | 14,4 m² | Kafel 30×30 (2-nav) | 14,4 m² |

Chiqim summasi = 14,4 × 70 000 = 1 008 000. Yangi birlik tannarxi = 1 008 000 ÷ 14,4 = **70 000**.

Agar 1 quti (1,44 m²) butunlay singan bo'lib, kirimga 12,96 m² yozilsa: 1 008 000 ÷ 12,96 = **77 777,78** so'm/m² — singan qismning qiymati qolgan tovar tannarxiga o'tadi.

**Natija:** asosiy Ombor modulida `DR-2026-00001` chiqim va `DR-2026-00001-IN` kirim. Tannarx huquqi bilan kartada "Tannarx" va "Yangi birlik tannarxi" ko'rinadi.

> **Muhim:** Chiqim va kirim tovari bir xil bo'lmasin. Chiqim faqat mavjud (rezervdan tashqari) qoldiqdan.

**Bekor qilish:** qoralama — oddiy; o'tkazilgan — B tovar kirimi va A tovar chiqimi qaytariladi. B tovar allaqachon sotilgan yoki rezervga olingan bo'lsa (mavjud yetmasa), bekor qilish rad etiladi.

## 4.7 Qo'shimcha xarajatlar (landed cost)

**Kim:** faqat tannarx huquqi (DstSeeCost) bor foydalanuvchi — rahbar, buxgalter   **Menyu:** Distributsiya → Qo'shimcha xarajatlar (landed cost)

Yetkazib beruvchidan kelgan kirim(lar)ga yo'l haqi, bojxona va boshqa xarajatlarni qo'shib, tovar tannarxini oshirish. Huquqsiz foydalanuvchi sahifani ochsa, "ruxsat yo'q" sahifasiga yo'naltiriladi.

### Qadamlar

1. **Taqsimlash** usulini tanlang, Izoh → **Yangi hujjat** (`DL-2026-00001`).

| Taqsimlash | Ulush nimaga qarab | Qachon |
|---|---|---|
| qiymat bo'yicha | Qator qiymati (miqdor × birlik narxi) | Standart, turli tovarlar uchun eng to'g'ri |
| miqdor bo'yicha | Qator miqdori | Faqat bir xil birlikdagi tovarlar |
| og'irlik bo'yicha | Miqdor × tovarning asosiy birlik og'irligi (distributsiya kartasi) | Og'ir yuk, yo'l haqi tonnaga qarab |

2. **Kirimlar** bo'limida "Kirim raqami yoki yetkazib beruvchi" bo'yicha qidiring va kirimni qo'shing. Ro'yxatda faqat so'nggi 120 kundagi, **qabul qilingan**, yetkazib beruvchidan kelgan kirimlar (ko'chirma, ishlab chiqarish va DLOC joylashtirish kirimlari emas). Ustunlar: Hujjat №, Sana, Ombor, Yetkazib beruvchi, Qiymat.
3. **Xarajatlar** bo'limida har xarajat: Turi (Yo'l haqi / Bojxona / Boshqa), Izoh, Summa — asosiy valyutada, QQSsiz.
4. Pastdagi **Taqsimlash (oldindan ko'rish)** jadvalini tekshiring: Hujjat №, Tovar, Miqdor, (og'irlikda — kg), Qiymat, Xarajat ulushi, Tannarx (oldin), **Tannarx (keyin)**, Sotilgan.
5. **O'tkazish (tannarxga qo'shish)** → "Kirim qatorlarining tannarxi o'zgaradi. Davom etasizmi?"

**Misol:** demo kirimiga 3 400 000 so'm yo'l haqi, qiymat bo'yicha.

| Tovar | Miqdor | Qiymat | Ulush | Xarajat ulushi | Tannarx (oldin) | Tannarx (keyin) |
|---|---|---|---|---|---|---|
| Sement M400 | 1 000 qop | 40 000 000 | 40/85 | 1 600 000 | 40 000 | **41 600** |
| Kafel 30×30 | 500 m² | 35 000 000 | 35/85 | 1 400 000 | 70 000 | **72 800** |
| Gipsokarton 12,5 mm | 200 list | 10 000 000 | 10/85 | 400 000 | 50 000 | **52 000** |
| **Jami** | | 85 000 000 | | **3 400 000** | | |

Hisob: 3 400 000 × 40 000 000 ÷ 85 000 000 = 1 600 000; 1 600 000 ÷ 1 000 = 1 600 so'm/qop qo'shiladi. Yaxlitlash qoldig'i (bir necha tiyin) eng katta qatorga qo'shiladi — jami har doim aynan xarajat summasiga teng.

### Allaqachon sotilgan qism

O'tkazishdan oldin sementdan 275 qop jo'natilgan bo'lsa (110 + 165), "Sotilgan" ustunida 275 chiqadi va ogohlantirish: "Xarajatning 440 000 so'mi allaqachon chiqarilgan tovarga to'g'ri keladi — o'tgan sotuvlar tannarxi o'zgarmaydi." (275 × 1 600 = 440 000.)

**Natija:** kirim qatorlarining birlik narxi oshadi. Keyingi jo'natishlar, hisobdan chiqarishlar, qoldiq qiymati va hisobotlar yangi narxni (sement — 41 600) oladi. O'tgan jo'natishlarning tannarxi va foydasi o'zgarmaydi.

> **Eslatma:** Sotilgan qismga to'g'ri kelgan summa (misolda 440 000) o'tgan jo'natishlar tannarxini o'zgartirmaydi — u davr xarajati sifatida Savdo hisobotida (filtrsiz, tannarx huquqi bilan) jami ostida "Qo'shimcha xarajat — sotilgan tovarga tushgan qismi (davr xarajati)" qatorida, landed cost o'tkazilgan kun bo'yicha ko'rinadi va "Foyda (qo'shimcha xarajatdan keyin)" dan ayiriladi (5-bob). Agent/mijoz bo'yicha taqsimlanmaydi, shuning uchun yo'l haqi hujjatini imkon qadar tovar sotilishidan oldin kiriting.

> **Muhim:** "Og'irlik bo'yicha" uchun har tovarda asosiy birlik og'irligi bo'lishi va kirim qatori asosiy birlikda bo'lishi shart. Aks holda qator qizil va o'tkazishda: "Og'irlik bo'yicha taqsimlab bo'lmaydi — og'irligi yo'q: …".

> **Eslatma:** Landed cost Moliya'ga pul yozmaydi. Yo'l haqi yoki bojxona to'lovini buxgalter Moliya modulida alohida chiqim sifatida kiritadi.

**Bekor qilish:** sabab yozib **Bekor** — o'tkazilgan bo'lsa, qo'shilgan narx kirim qatorlaridan ayiriladi (sement yana 40 000). Landed cost o'tkazilgan va bekor qilingan oraliqda bo'lgan jo'natishlar oshirilgan narxda (41 600) qoladi — ular qayta hisoblanmaydi.

## 4.8 Minimal qoldiq ogohlantirishi va Buyurtma taklifi

### 4.8.1 Telegram ogohlantirishi

Buyurtma tasdiqlash (rezerv), jo'natish, hisobdan chiqarish yoki pereso'rtitsa natijasida tovarning **mavjud** qoldig'i (haqiqiy − rezerv) minimal qoldiqdan **pastga tushgan** paytda rahbarlarga va shu ombor omborchilariga bir marta xabar boradi:

"⚠️ Minimal qoldiqdan past — DEMO Asosiy ombor (Toshkent):
• DEMO-SEM-400 Sement M400: 505 / min 600"

Qoldiq allaqachon minimaldan past bo'lsa, keyingi chiqimlarda xabar takrorlanmaydi.

> **Eslatma:** Xabar ham, "Buyurtma taklifi" sahifasi ham bir xil — **mavjud** qoldiqqa qaraydi. Misolda 05.11 da 220 qopli buyurtma tasdiqlanganda mavjud 725 → 505 < 600 — xabar shu paytda keladi va tovar taklif sahifasiga tushadi. Keyin shu rezervdagi tovar jo'natilganda mavjud qoldiq o'zgarmaydi — xabar takrorlanmaydi.

### 4.8.2 Buyurtma taklifi

**Kim:** rahbar, ta'minotchi   **Menyu:** Distributsiya → Buyurtma taklifi

Sahifada mavjud qoldig'i minimaldan past tovarlar (minimal qoldiq berilgan ombor × tovar juftliklari). Ombor filtri — Barcha omborlar yoki bitta ombor.

| Ustun | Ma'nosi |
|---|---|
| ☐ | Buyurtmaga kiritish. So'nggi 30 kunda shu tovarga taklifdan xarid buyurtmasi yaratilgan bo'lsa, belgi olib qo'yilgan |
| Ombor, Tovar | |
| Mavjud | Haqiqiy − rezerv |
| Minimal | Minimal qoldiq |
| Maqsad | Maksimal; bo'lmasa minimal × 2 |
| Buyurtma | Taklif = maqsad − mavjud (butun songa yuqoriga yaxlitlanadi), tahrirlanadi |
| Yetkazib beruvchi | Tovarning oxirgi xarid hisob-fakturasidan |
| Narx | Oxirgi xarid narxi (ustun faqat tannarx huquqi bilan ko'rinadi va tahrirlanadi) |
| 30 kunda buyurtma | So'nggi 30 kundagi taklif buyurtmasi raqami |

**Misol (05.11):** Sement — mavjud 505, minimal 600, maksimal 1 500 → taklif 1 500 − 505 = **995 qop**; oxirgi xarid narxi 40 000 → buyurtma summasi 995 × 40 000 = 39 800 000 so'm. Maksimal berilmaganida: 600 × 2 − 505 = 695 qop.

### 4.8.3 Xarid buyurtmasini yaratish

1. Kerakli qatorlarni belgilang, miqdor va narxni tekshiring.
2. **Xarid buyurtmasi yaratish**.

**Natija:** yetkazib beruvchi bo'yicha alohida SRM xarid buyurtmasi(lar) "Yaratilgan" holatida (yetkazib beruvchisi yo'q tovarlar — bitta umumiy buyurtmada). Xabar: "Xarid buyurtmalari yaratildi: …". Buyurtma izohida "DST_REORDER: minimal qoldiq bo'yicha (distributsiya). Taklif qilingan yetkazib beruvchi: … Omborlar: …", qatorda "Min qoldiq: DEMO Asosiy ombor (Toshkent) 995".

Keyingi qadamlar (shartnoma, xarid invoysi, kirim, qabul) — odatdagidek **Ta'minot (SRM)** modulida.

> **Muhim:** SRM qoidasi bo'yicha xarid buyurtmasida yetkazib beruvchi maydoni bo'sh qoladi — taklif qilingan yetkazib beruvchi faqat izohda. Ta'minotchi uni SRM'da tanlaydi.

> **Eslatma:** Tannarx huquqi bo'lmagan foydalanuvchida "Narx" ustuni yo'q — xarid buyurtmasiga oxirgi xarid narxi server tomonida o'zi yoziladi. Huquqi bor foydalanuvchi narxni bo'sh qoldirsa ham shunday.

## 4.9 Omborchining kundalik ish tartibi

| Qachon | Nima | Qayerda |
|---|---|---|
| Ertalab | Tasdiqlangan buyurtmalarni yig'ish (Telegram'da "📦 Yig'ish kerak" xabari keladi) | Buyurtmalar yoki 📦 Ombor ilovasi |
| Tovar kelganda (boshqa ombordan) | Sanab qabul qilish, farq bo'lsa — dalolatnoma | Ko'chirmalarni qabul qilish |
| Brak/sinish topilsa | Hisobdan chiqarish → tasdiqqa yuborish | Hisobdan chiqarish |
| Kun oxiri | Minimaldan past tovarlar | Qoldiqlar (filtr "Minimaldan past") |
| Oy oxiri | Nazorat sanog'i | Inventarizatsiya |

> **Muhim:** Qoldiq farqi har doim K1 darajali muammo: avval sababini toping (jo'natish, qaytarish, ko'chirma), keyin tuzatish hujjati (hisobdan chiqarish, inventarizatsiya) bilan tuzating. Asosiy Ombor modulida qoldiqni "qo'lda" o'zgartirmang.
