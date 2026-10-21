# 2. Buyurtmalar

Bu bob distributsiya buyurtmasining butun hayotini yoritadi: yaratish (web, bot, Mini App), tasdiqlash va qarz limiti, rezerv, yig'ish, jo'natish, yetkazish, yopilish, bekor qilish, qaytarish va aksiyalar. Oxirida — tez-tez uchraydigan xato xabarlari va ularning ma'nosi.

Bobni operator, omborchi, rahbar va supervayzer o'qiydi. Agent uchun 2.3 va 2.17 yetarli (bot bilan ishlash — 6-bob, 6.4).

Misollar **DEMO Qurilish Savdo MChJ** ssenariysida (0-bob, 0.8): ombor qoldig'i — 1 000 qop sement, 500 m² kafel, 200 list gipsokarton (DEMO Asosiy ombor); sozlamalar — minimal buyurtma 500 000, yirik buyurtma 30 000 000, chegirma limiti: Agent 3%, Operator 5%, Rahbar 10% (1-bob, 1.2).

## 2.1 Buyurtmalar ro'yxati

**Kim:** Operator, Rahbar, Omborchi, Supervayzer   **Menyu:** Distributsiya → Buyurtmalar

Ro'yxat ustunlari: Raqam, Sana, Mijoz, Agent, Ombor, Holat, Jami, Yetkazish sanasi ("O'zi olib ketadi" belgisi bilan).

Filtrlar:

| Filtr | Qiymatlar |
|---|---|
| Holat | Barcha holatlar yoki bittasi (Yangi, Tasdiqlangan, Yig'ilmoqda, Qisman jo'natildi, Jo'natildi, Yetkazildi, Yopildi, Bekor qilingan) |
| Agent | Barcha agentlar yoki bittasi |
| Ombor | Barcha omborlar yoki bittasi |
| Boshlanish / Tugash | Buyurtma sanasi oralig'i |

Ko'rinish (0-bob, 0.4): agent — faqat o'z buyurtmalari, supervayzer — jamoasiniki, ombor doirasi berilgan omborchi — faqat o'z omborlaridagi buyurtmalar (ombori hali tanlanmagan yangi buyurtmalar unga ko'rinmaydi).

Kunlik ish uchun qulay filtrlar:

| Kim | Filtr | Nima uchun |
|---|---|---|
| Operator | Holat = Yangi | Tasdiqlash kerak bo'lgan buyurtmalar |
| Omborchi | Holat = Tasdiqlangan, Ombor = o'z ombori | Yig'ish va jo'natish |
| Operator | Holat = Jo'natildi | "Yetkazildi" belgilanmaganlar |
| Rahbar | Holat = Qisman jo'natildi | Qolgani jo'natilmagan buyurtmalar |

## 2.2 Web'da buyurtma yaratish

**Kim:** Operator (telefon orqali kelgan buyurtma), Rahbar   **Menyu:** Distributsiya → Buyurtmalar → **Yangi buyurtma**

### 2.2.1 Sarlavha

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Mijoz * | Nomi bo'yicha (yoki to'liq STIR bilan) qidiruv | DEMO Anvar do'koni |
| Agent | Bo'sh — mijoz kartasidagi agent | (bo'sh → Jasur) |
| Ombor | Faqat savdo omborlari; bo'sh — "avtomatik tanlanadi" (tasdiqlashda) | (bo'sh) |
| Yetkazish sanasi | | 10.11.2026 |
| Chegirma % | Buyurtma chegirmasi (butun buyurtmaga) | 0 |
| O'zi olib ketadi | Belgilansa mijoz tovarni ombordan o'zi oladi: minimal summa tekshirilmaydi, jo'natish bilan birga "Yetkazildi" bo'ladi | — |
| Izoh | Erkin matn | "Ertalab 10 gacha" |

Mijoz tanlangach o'ngda **Qarz va limit** paneli chiqadi: Qarz, Muddati o'tgan, Kredit limiti, Limitdan qolgan, Narx turi, To'lov muddati (kun). Mijoz tasdiqlanmagan bo'lsa "Tasdiq kutilmoqda" belgisi turadi.

### 2.2.2 Tovar qatorlari

1. **Tovar qo'shish (nom yoki artikul)...** maydoniga yozing va ro'yxatdan tanlang. Yoki **Shtrix-kod + Enter** maydoniga skaner bilan o'qiting — tovar shu shtrix-kod birligida qo'shiladi (topilmasa "Shtrix-kod topilmadi.").
2. **Birlik** ni tanlang (asosiy birlik yoki qo'shimcha: quti, poddon).
3. **Miqdor** ni yozing (tanlangan birlikda).
4. Kerak bo'lsa **%** ustuniga qator chegirmasini yozing.
5. **Saqlash**.

Har qatorda: **Narx** — tanlangan birlik uchun mijoz narxi (avtomatik, o'zgartirilmaydi; narx yo'q bo'lsa "narx yo'q"), **Summa**, **Mavjud** — savdo omborlaridagi mavjud qoldiq. Pastda: Jami (chegirmagacha), Chegirma, Jami.

> Narx mijozning narx turidan (yoki maxsus narxdan) avtomatik olinadi; chegirma va minimal narx saqlashda tekshiriladi.

Saqlangach buyurtma **Yangi** holatda, raqami beriladi (masalan D-2026-00001) va buyurtma kartasi ochiladi. Shu paytda tizim aksiyalarni qo'llaydi (2.17), summa 30 000 000 dan katta bo'lsa rahbarlarga "Yirik buyurtma" xabari, mijoz limiti yetmasa — "kredit shartidan chiqdi" xabari **✅ Tasdiqlash** tugmasi bilan ketadi.

### 2.2.3 DEMO misoli: Anvar do'koni buyurtmasi

Agent Jasur (limit 3%) DEMO Anvar do'koni (Chakana) uchun:

| # | Tovar | Birlik | Miqdor | Asosiy birlikda | Narx (birlik) | Yalpi | Chegirma | Summa |
|---|---|---|---|---|---|---|---|---|
| 1 | Sement M400 | poddon | 2 | 80 qop | 2 480 000 | 4 960 000 | — | 4 960 000 |
| 2 | Kafel 30×30 | quti | 70 | 100,8 m² | 136 800 | 9 576 000 | 5% aksiya = 478 800 | 9 097 200 |
| 3 | Gipsokarton | list | 20 | 20 list | 68 000 | 1 360 000 | 3% qo'lda = 40 800 | 1 319 200 |
| 4 | Sement M400 (**sovg'a**) | qop | 8 | 8 qop | 0 | 0 | — | 0 |
| | | | | | | | **Jami** | **15 376 400** |

Qanday hisoblandi:

- Poddon narxi = qop narxi × koeffitsient = 62 000 × 40 = 2 480 000. Quti narxi = 95 000 × 1,44 = 136 800.
- 70 quti = 70 × 1,44 = 100,8 m² ≥ 100 m² — "Kafel ≥ 100 m² — 5%" aksiyasi avtomatik.
- 80 qop sementga "10+1" aksiyasi: 80 / 10 = 8 qop sovg'a — narxsiz alohida qator.
- Gipsokartonga 3% — agent limiti ichida.

Buyurtma og'irligi shu jumladan: sement 88 qop × 50 kg = 4 400 kg (+ kafel va gipsokarton og'irligi, kartasida berilgan bo'lsa).

### 2.2.4 Tahrirlash va o'chirish

- Faqat **Yangi** buyurtma tahrirlanadi: kartada **Tahrirlash**. Boshqa holatda: "Bu holatda o'zgartirib bo'lmaydi."
- Tasdiqlangan buyurtmani o'zgartirish kerak bo'lsa — avval **Tasdiqni qaytarish** (2.6).
- Har saqlashda narxlar narx varag'idan va aksiyalar qaytadan hisoblanadi. Sovg'a qatorlari formada ko'rinmaydi — ular saqlashda o'zi qayta qo'shiladi.
- Yangi buyurtmani kartadagi savat tugmasi bilan butunlay o'chirish mumkin. Boshqa holatda o'chirilmaydi — faqat bekor qilinadi (2.15).
- Ikki xodim bir vaqtda bitta buyurtma bilan ishlasa: "Buyurtmani boshqa foydalanuvchi o'zgartirdi — sahifani yangilang."

## 2.3 Bot, Mini App va boshqa manbalardan buyurtma

Buyurtma kartasida **Manba** ko'rinadi: `web`, `bot` yoki `api`.

| Manba | Kim | Qanday |
|---|---|---|
| Mini App | Agent | Botda mijoz → **📍 Keldim** → **🛒 Buyurtma**: katalog (qidiruv, kategoriya, tavsiya, aksiya belgisi), birlik, miqdor, savat, yetkazish sanasi, izoh, mijoz imzosi → Yuborish |
| Ovozli xabar | Agent | Ovozli xabar ("Anvar do'koniga 2 poddon sement, 70 quti kafel") → bot qoralama ko'rsatadi → **✅ Buyurtma berish** (Sozlamada yoqilgan bo'lsa) |
| Sayt-katalog | Ofis | Saytdan kelgan ariza → **Buyurtma yaratish** (Sayt moduli) |

Botdan kelgan buyurtma ham web bilan bir xil qoidadan o'tadi:

- narx avtomatik, chegirma agent rolining limiti bilan, minimal narx tekshiriladi, aksiyalar qo'llanadi;
- buyurtma **Yangi** holatda ofisga tushadi; agent uni o'zi tasdiqlamaydi;
- ombor — agent kartasidagi ombor (1-bob, 1.7.2), bo'sh bo'lsa tasdiqlashda avtomatik;
- aloqa uzilib agent qayta yuborsa ham buyurtma bitta bo'ladi.

Batafsil — 6-bob, 6.4.

## 2.4 Buyurtma holatlari

| Holat | Qachon | Rezerv | Qarz | Mumkin amallar (kartada) |
|---|---|---|---|---|
| **Yangi** | Saqlangan | yo'q | yo'q | Tahrirlash, Tasdiqlash, O'chirish, Bekor qilish |
| **Tasdiqlangan** | Tasdiqlashdan o'tgan | bor | yo'q | Tasdiqni qaytarish, Yig'ishni boshlash, Rezervni yangilash, Jo'natish, Bekor qilish |
| **Yig'ilmoqda** | Omborchi yig'ishni boshlagan | bor | yo'q | Rezervni yangilash, Jo'natish, Bekor qilish |
| **Qisman jo'natildi** | Bir qismi jo'natilgan | qolgan qismga | jo'natilgan qism | Jo'natish (qolganini), Qolganidan voz kechish, Yetkazildi, To'lov, Qaytarish, jo'natishni bekor qilish |
| **Jo'natildi** | Hammasi jo'natilgan (yoki qolganidan voz kechilgan) | yo'q | to'liq | Yetkazildi, To'lov, Qaytarish, jo'natishni bekor qilish |
| **Yetkazildi** | Mijoz qabul qilgan | yo'q | to'liq | To'lov, Qaytarish |
| **Yopildi** | Buyurtma qarzi to'liq yopilgan (avtomatik) | yo'q | 0 | Qaytarish |
| **Bekor qilingan** | Sabab bilan bekor qilingan | yechilgan | yo'q | — |

O'tishlar sxemasi:

```
 Yangi ──Tasdiqlash──> Tasdiqlangan ──Yig'ishni boshlash──> Yig'ilmoqda
   ^                      |   |                                  |
   └──Tasdiqni qaytarish──┘   └──────────── Jo'natish ───────────┤
                                                                  v
                          Qisman jo'natildi <──Jo'natish (qisman)─┤
                             |       |                            |
          Qolganidan voz kechish     └──Jo'natish (qolgani)──> Jo'natildi
                             └──────────────────────────────> Jo'natildi
                                                                  |
                                       Yetkazildi <──Yetkazildi───┘
                                           |
              qarz to'liq to'langanda ──> Yopildi

 Yangi / Tasdiqlangan / Yig'ilmoqda ──Bekor qilish (sabab)──> Bekor qilingan
 Jo'natishni bekor qilish: Jo'natildi/Qisman ──> Qisman yoki Tasdiqlangan
```

Ruxsat etilmagan o'tishda: "Holatni o'zgartirib bo'lmaydi: Yangi → Jo'natildi." kabi xabar.

Kartadagi **Tarix** blokida har holat o'zgarishi, kim bajargani va izohi (sabab, ruxsat izohi, jo'natish raqami) ko'rinadi.

> **Eslatma:** "Yopildi" faqat **Jo'natildi** yoki **Yetkazildi** holatidan avtomatik keladi. Qisman jo'natilgan buyurtma to'liq to'langan bo'lsa ham qisman holatda qoladi — qolganini jo'nating yoki "Qolganidan voz kechish" qiling.

## 2.5 Tasdiqlash

**Kim:** Operator, Rahbar   **Menyu:** Buyurtma kartasi → **Tasdiqlash**

Tasdiqlash tugmasi bosilganda tizim ketma-ket tekshiradi:

| # | Tekshiruv | O'tmasa |
|---|---|---|
| 1 | Mijoz tasdiqlanganmi | "Mijoz hali tasdiqlanmagan — avval mijoz kartasini tasdiqlang." |
| 2 | Minimal buyurtma summasi (o'zi olib ketishdan tashqari) | "Yetkazish uchun minimal buyurtma summasi — 500 000." |
| 3 | Muddati o'tgan qarz yo'qmi | "Muddati o'tgan qarz bor: …" |
| 4 | Qarz + buyurtma ≤ kredit limiti | "Qarz + buyurtma … — limit … dan oshadi." |
| 5 | Ombor (tanlanmagan bo'lsa — avtomatik) | "Hech bir savdo omborida bu tovarlar yo'q — omborni qo'lda tanlang." |
| 6 | Rezerv | Yetmagan qism "kirimni kutmoqda" bo'ladi (xato emas) |

Hammasi o'tsa buyurtma **Tasdiqlangan**, agentga "Buyurtma … tasdiqlandi" xabari, omborchiga "📦 Yig'ish kerak" xabari (mijoz, ombor, qatorlar soni, kg, summa, yetkazish sanasi yoki "o'zi olib ketadi") boradi.

### 2.5.1 Qarz limiti va rahbar ruxsati

DEMO misoli. DEMO Anvar do'koni D-2026-00001 bo'yicha 15 376 400 so'm qarzdor (2.16-dagi qaytarishdan keyin 15 252 400), uning limiti 20 000 000. Jasur yana 6 000 000 so'mlik buyurtma yuboradi:

- qarz + buyurtma = 15 252 400 + 6 000 000 = 21 252 400 > 20 000 000;
- tasdiqlashda: "Qarz + buyurtma 21 252 400 — limit 20 000 000 dan oshadi.";
- agar to'lov muddati (7 kun) ham o'tgan bo'lsa, qo'shimcha: "Muddati o'tgan qarz bor: 15 252 400."

Nima qilinadi:

1. **Ruxsat huquqi yo'q xodim** (operator) xatoni ko'radi va buyurtmani shu holatda qoldiradi — rahbarga botda xabar allaqachon ketgan.
2. **"Limitdan oshishga ruxsat" huquqi bor xodim** (rahbar) web'da **Tasdiqlash** ni bossa, **Limitdan oshishga ruxsat** oynasi chiqadi: muammolar ro'yxati va izoh maydoni. Izoh (masalan "Ertaga 10 mln to'laydi, kafolat — egasi") yozib **Qo'llash** — buyurtma tasdiqlanadi.
3. Yoki rahbar botdagi xabardagi **✅ Tasdiqlash** tugmasini bosadi — natija bir xil.

Ruxsat kim tomonidan va qaysi izoh bilan berilgani kartada "Limitdan oshishga ruxsat berdi: …" qatorida va Tarixda saqlanadi. Izohsiz ruxsat berilmaydi.

> **Eslatma:** Limit 0 bo'lgan mijoz — "faqat oldindan to'lov". Uning avansi buyurtmani qoplamasa, har buyurtma rahbar ruxsatini talab qiladi.

Bunyodkor misoli: kirish qarzi 3 000 000 (muddati 20.11.2026), limit 50 000 000. 10.11.2026 da 40 612 500 so'mlik buyurtma (2.5.3): 3 000 000 + 40 612 500 = 43 612 500 ≤ 50 000 000, muddati o'tgan qarz yo'q — tasdiqlanadi. Lekin buyurtma 30 000 000 dan katta — saqlanganda rahbarga "Yirik buyurtma" xabari ketgan.

### 2.5.2 Omborni avtomatik tanlash

Ombor tanlanmagan bo'lsa (web'da "avtomatik tanlanadi", botda agent ombori bo'lmasa) tasdiqlashda tizim savdo omborlaridan tanlaydi:

1. Barcha tovarni to'liq qoplaydigan ombor;
2. bunday ombor bo'lmasa — eng ko'p qismini qoplaydigani;
3. teng bo'lsa — ro'yxatda birinchisi.

Hech bir savdo omborida bu tovarlar umuman bo'lmasa — xato, omborni formada qo'lda tanlang. Buyurtma bitta ombordan jo'natiladi; boshqa ombordagi qism uchun alohida buyurtma oching.

### 2.5.3 Rezerv va "kirimni kutmoqda"

Tasdiqlashda har qator uchun tanlangan omborda **mavjud** qoldiqdan rezerv olinadi (sovg'a qatorlari ham). Boshqa buyurtmalar rezervi daxlsiz.

D-2026-00001 tasdiqlangandan keyin **Qoldiqlar** (DEMO Asosiy ombor):

| Tovar | Haqiqiy | Rezerv | Mavjud |
|---|---|---|---|
| Sement M400, qop | 1 000 | 88 | 912 |
| Kafel 30×30, m² | 500 | 100,8 | 399,2 |
| Gipsokarton, list | 200 | 20 | 180 |

Endi DEMO Bunyodkor qurilish (Diler) 450 m² kafel buyuradi. Diler narx turida kafel narxi yo'q — standart Chakana narxi 95 000 olinadi: 450 × 95 000 = 42 750 000, aksiya 5% (≥ 100 m²) = 2 137 500, jami **40 612 500 so'm**. Tasdiqlashda mavjud faqat 399,2 m²:

- rezervga 399,2 m² olinadi;
- kartadagi qatorda: "kirimni kutmoqda: 50,8";
- yig'ish varaqasida ham "kirimni kutmoqda" ko'rsatiladi.

Kirim kelgach (masalan 300 m² kafel qabul qilindi) kartada **Rezervni yangilash** ni bosing — yetmagan 50,8 m² rezervga olinadi. Shu paytgacha 399,2 m² ni qisman jo'natish ham mumkin (2.8).

> **Muhim:** Rezerv avtomatik yangilanmaydi. Kirimdan keyin "kutmoqda" qatorli buyurtmalarni ochib **Rezervni yangilash** ni bosing — aks holda yangi kelgan tovarni boshqa buyurtma rezerv qilib oladi.

## 2.6 Tasdiqni qaytarish

**Kim:** Operator, Rahbar   **Menyu:** Buyurtma kartasi → **Tasdiqni qaytarish**

Tasdiqlangan buyurtmani o'zgartirish kerak bo'lsa (mijoz miqdorni o'zgartirdi):

1. **Tasdiqni qaytarish** ni bosing va tasdiqlang.
2. Rezerv yechiladi, buyurtma yana **Yangi** — tahrirlang.
3. Qayta **Tasdiqlash** (barcha tekshiruvlar va rahbar ruxsati qaytadan).

Faqat **Tasdiqlangan** holatda ishlaydi. Yig'ish boshlangan yoki jo'natish bo'lgan buyurtmada tugma yo'q. Jo'natish bo'lsa: "Buyurtma bo'yicha jo'natish bor — avval qaytarish hujjati kerak."

## 2.7 Yig'ish

**Kim:** Omborchi   **Menyu:** Buyurtma kartasi → **Yig'ishni boshlash**, **Yig'ish varaqasi**

1. Buyurtmalar ro'yxatida Holat = Tasdiqlangan, Ombor = o'z omboringiz.
2. Kartani oching va **Yig'ishni boshlash** ni bosing — holat **Yig'ilmoqda**, ofis omborchi ishlayotganini ko'radi.
3. **Mahsulotlar** blokidagi **Yig'ish varaqasi** — yangi oynada chop etish shakli (brauzerda Chop etish → printer yoki PDF).
4. Varaq bo'yicha yig'ing.

**Yig'ish varaqasi** tarkibi: buyurtma raqami, sana, mijoz, ombor, yetkazish sanasi yoki "O'zi olib ketadi"; har qatorda artikul, tovar, shtrix-kodlar, yig'ish miqdori (sotish birligida va asosiy birlikda), "kirimni kutmoqda" qismi; jami og'irlik va hajm; "Yig'di" va "Tekshirdi" imzo joylari.

Mini App'da ham yig'ish mumkin: omborchi botda **📦 Ombor** → buyurtma → yig'ishni boshlash → shtrix-kodlarni skanerlaydi → "Jo'natish" (6-bob, 6.6).

> **Eslatma:** "Yig'ishni boshlash" majburiy emas — tasdiqlangan buyurtmani to'g'ridan-to'g'ri jo'natish mumkin.

## 2.8 Jo'natish

**Kim:** Omborchi   **Menyu:** Buyurtma kartasi → **Jo'natish**

1. **Jo'natish** ni bosing — oyna ochiladi.
2. Har qatorda: Tovar, Qolgan, Rezerv va **Miqdor (asosiy birlik)**. Sukut bo'yicha — rezervdagi miqdor.
3. Kerak bo'lsa miqdorni kamaytiring (qisman jo'natish) yoki 0 qoldiring.
4. Oynadagi **Jo'natish** ni bosing.

> Miqdor asosiy birlikda. Sukut bo'yicha — rezervdagi miqdor. Ombordan sof FIFO bilan chiqariladi, mijoz qarziga yoziladi.

Natija:

| Qayerda | Nima bo'ladi |
|---|---|
| Hujjat | Jo'natish DJ-2026-00001 — kartadagi **Jo'natishlar** blokida |
| Ombor | Ombor chiqimi (Omborxona modulida ham ko'rinadi), FIFO partiyadan; rezerv kamayadi |
| Tannarx | Har qator FIFO tannarxi jo'natishda saqlanadi |
| Qarz | Mijoz hisobiga debet = jo'natish summasi, muddat = bugun + to'lov muddati |
| Holat | Hammasi jo'natilsa — **Jo'natildi**, aks holda **Qisman jo'natildi** |
| Xabar | Mijozga (botga ulangan bo'lsa): "Buyurtmangiz … jo'natildi: … so'm. Joriy qarzingiz: … so'm." |
| Minimal qoldiq | Qoldiq minimaldan pastga tushsa rahbar va omborchiga ogohlantirish |

### 2.8.1 DEMO misoli: to'liq jo'natish

10.11.2026 da D-2026-00001 to'liq jo'natildi (DJ-2026-00001):

| Tovar | Miqdor | Summa | FIFO tannarx |
|---|---|---|---|
| Sement M400 | 80 qop | 4 960 000 | 80 × 40 000 = 3 200 000 |
| Sement M400 (sovg'a) | 8 qop | 0 | 8 × 40 000 = 320 000 |
| Kafel 30×30 | 100,8 m² | 9 097 200 | 100,8 × 70 000 = 7 056 000 |
| Gipsokarton | 20 list | 1 319 200 | 20 × 50 000 = 1 000 000 |
| **Jami** | | **15 376 400** | **11 576 000** |

- Anvar do'koni qarzi: 15 376 400 so'm, to'lov muddati 17.11.2026 (10.11 + 7 kun).
- Yalpi foyda (faqat tannarx huquqi bilan ko'rinadi): 15 376 400 − 11 576 000 = **3 800 400 so'm**.
- Qoldiq: sement 912 qop, kafel 399,2 m², gipsokarton 180 list (rezerv 0).

### 2.8.2 Qisman jo'natish

Kafel mashinaga sig'madi — omborchi kafel qatoriga 0 yozadi:

| Jo'natish | Tarkib | Summa | Holat |
|---|---|---|---|
| DJ-2026-00001 | 80 + 8 qop sement, 20 list gipsokarton | 4 960 000 + 0 + 1 319 200 = 6 279 200 | Qisman jo'natildi |
| DJ-2026-00002 (ertasi kuni) | 100,8 m² kafel | 9 097 200 | Jo'natildi |

Birinchi jo'natishdan keyin qarz 6 279 200, ikkinchisidan keyin 15 376 400. Kafel shu orada rezervda turadi. Har jo'natishning qarz muddati o'z sanasidan hisoblanadi.

Qisman jo'natilgan summa = qator summasi × (jo'natilgan / buyurtma miqdori), buyurtma chegirmasi bilan.

### 2.8.3 O'zi olib ketish

Buyurtmada "O'zi olib ketadi" belgilangan bo'lsa, oxirgi jo'natish bilan buyurtma darhol **Yetkazildi** bo'ladi — alohida belgilash kerak emas. Bunday jo'natish reysga qo'shilmaydi.

### 2.8.4 Jo'natishdagi xatolar

| Xabar | Sabab |
|---|---|
| "Bu holatdagi buyurtmani jo'natib bo'lmaydi." | Buyurtma Yangi, Jo'natildi, Yetkazildi, Yopildi yoki Bekor |
| "Ombor tanlanmagan." | Buyurtmada ombor yo'q |
| "Jo'natish uchun miqdor yo'q." | Barcha qatorlar 0 |
| "N-qator: buyurtmadan ko'p jo'natib bo'lmaydi." | Qolgandan ko'p miqdor |
| "N-qator: omborda yetarli emas (mavjud …)." | Rezerv + mavjud qoldiqdan ko'p; boshqa buyurtmalar rezervi olinmaydi |
| "N-qator: ombor chiqimi xatosi." | Ombor hisobida muammo (masalan, joylashuvli omborda partiya topilmadi) — administratorga |

## 2.9 Nakladnoy

**Kim:** Omborchi, Operator   **Menyu:** Buyurtma kartasi → **Jo'natishlar** → jo'natish qatoridagi **Nakladnoy**

Nakladnoy har **jo'natish** bo'yicha alohida chop etiladi (qisman jo'natishda — har biriga). Yangi oynada ochiladi; brauzerda Chop etish → printer yoki "PDF sifatida saqlash".

Tarkibi: Jo'natuvchi (kompaniya), Qabul qiluvchi (mijoz, STIR), raqam va sana, tovarlar (miqdor, narx, summa), jami so'mda, mijozning joriy qarzi va to'lov muddati (kun), "Berdi" va "Qabul qildi" imzo joylari.

Reys bilan yetkazilganda mijozga nakladnoy PDF Telegram'da avtomatik yuboriladi (7-bob, 7.13).

## 2.10 Jo'natishni bekor qilish

**Kim:** Omborchi, Operator   **Menyu:** Buyurtma kartasi → **Jo'natishlar** → jo'natish qatoridagi bekor qilish tugmasi

Xato jo'natilgan bo'lsa (noto'g'ri miqdor, noto'g'ri mijoz):

1. Jo'natish qatoridagi bekor qilish tugmasini bosing.
2. **Jo'natishni bekor qilish sababi** ni yozing (kamida 3 belgi).

Natija: ombor chiqimi teskari qaytariladi (tovar omborga qaytadi), mijoz qarzidan "Jo'natish bekor qilindi" yozuvi bilan chiqariladi, buyurtma qatorlari qaytadi, holat **Qisman jo'natildi** (boshqa jo'natish bo'lsa) yoki **Tasdiqlangan**, tovar yana rezervga olinadi.

Cheklovlar:

| Holat | Xabar |
|---|---|
| Buyurtma Yetkazildi yoki Yopildi | "Faqat jo'natilgan (yetkazilmagan) buyurtma jo'natishi bekor qilinadi — aks holda qaytarish." |
| Jo'natish bo'yicha qaytarish bor | "Jo'natish bo'yicha qaytarish bor — avval uni bekor qiling." |

Yetkazilgan tovar faqat **qaytarish** hujjati bilan qaytariladi (2.16).

## 2.11 Qolganidan voz kechish

**Kim:** Operator, Rahbar   **Menyu:** Buyurtma kartasi → **Qolganidan voz kechish** (faqat Qisman jo'natildi holatida)

Qisman jo'natilgan buyurtmaning qolgani endi kerak emas bo'lsa:

1. **Qolganidan voz kechish** ni bosing.
2. **Sabab** ni yozing (kamida 3 belgi), **Qo'llash**.

Qolgan qism rezervi yechiladi, buyurtma **Jo'natildi** holatiga o'tadi. Qarz faqat jo'natilgan qism bo'yicha.

DEMO: 2.8.2 dagi buyurtmada kafel jo'natilmasdan mijoz undan voz kechdi. Sabab: "Mijoz kafelni boshqa joydan oldi". Natija: holat Jo'natildi, qarz 6 279 200, kafel rezervi (100,8 m²) bo'shaydi.

## 2.12 Yetkazildi

**Kim:** Operator, agent (ofis orqali), ekspeditor (reys orqali)   **Menyu:** Buyurtma kartasi → **Yetkazildi**

Ikki yo'l:

| Yo'l | Qanday |
|---|---|
| Reys bilan | Ekspeditor botda **✅ Yetkazildi (rasm)** yoki **✍️ Imzo / qisman** — buyurtmaning barcha jo'natishlari hal bo'lgach buyurtma avtomatik **Yetkazildi**, mijozga nakladnoy PDF (7-bob, 7.7) |
| Qo'lda | Kartadagi **Yetkazildi** tugmasi — Jo'natildi yoki Qisman jo'natildi holatida; mijozga "Buyurtmangiz … yetkazildi: … so'm. Rahmat!" |

Oldindan to'langan buyurtma jo'natish bilan darhol **Yopildi** bo'ladi. Bunday buyurtmada ham **Yetkazildi** tugmasi turadi — bosilsa yetkazish sanasi qayd qilinadi, holat Yopildi qoladi.

## 2.13 To'lov va Yopildi

**Kim:** Kassir, Operator   **Menyu:** Buyurtma kartasi → **To'lov qabul qilish** (jo'natish bo'lgandan keyin)

Oynada: summa (sukut — mijozning joriy qarzi), to'lov usuli, kassa/hisob, izoh. Naqd pulni agent olgan bo'lsa hisob tanlanmaydi ("Agent qo'lida (naqd)") — u kassaga topshirilganda yoziladi. Batafsil — 3-bob, 3.3–3.5.

To'lov mijozning **eng eski** qarzini birinchi yopadi (buyurtma tanlansa ham). Buyurtmaning jo'natish qarzlari to'liq yopilganda Jo'natildi yoki Yetkazildi holatidagi buyurtma avtomatik **Yopildi** bo'ladi.

DEMO: 2.16-dagi qaytarishdan keyin Anvar do'koni qarzi 15 252 400. Kassir 15 252 400 so'm naqd (kassaga) qabul qiladi — qarz 0, D-2026-00001 **Yopildi**.

## 2.14 Hisob-kitob qoidalari (qisqacha)

| Hisob | Formula |
|---|---|
| Birlik narxi | asosiy birlik narxi × koeffitsient (so'mgacha yaxlitlanadi) |
| Qator yalpi summasi | miqdor × birlik narxi |
| Qator chegirmasi | aksiya % va qo'lda % ketma-ket: 1 − (1 − aksiya)(1 − qo'lda) |
| Qator summasi | yalpi − qator chegirmasi |
| Buyurtma jami | qatorlar summasi − buyurtma chegirmasi % |
| Chegirma limiti tekshiruvi | faqat **qo'lda** berilgan: 1 − (1 − qator %)(1 − buyurtma %) ≤ rol limiti |
| Minimal narx tekshiruvi | qo'lda chegirmadan keyingi birlik narxi ≥ minimal narx × koeffitsient (aksiya chegirmasi hisobga olinmaydi) |
| Qarz muddati | jo'natish sanasi + mijozning to'lov muddati |

Misol (chegirma ketma-ketligi): kafel qatoriga 5% aksiya + operator 2% qo'lda → qatordagi chegirma 1 − 0,95 × 0,98 = 6,9%. 70 quti: 9 576 000 × 6,9% = 660 744, qator summasi 8 915 256. Limit tekshiruvida faqat 2% hisobga olinadi (operator limiti 5% — o'tadi).

## 2.15 Buyurtmani bekor qilish

**Kim:** Operator, Rahbar, Supervayzer   **Menyu:** Buyurtma kartasi → **Bekor qilish**

1. **Bekor qilish** ni bosing.
2. **Sabab** ni yozing (kamida 3 belgi) — masalan "Mijoz bekor qildi", **Qo'llash**.

Natija: rezerv yechiladi, holat **Bekor qilingan**, sabab kartada va Tarixda; buyurtmani agent bermagan bo'lsa agentga "Buyurtma … bekor qilindi: … Sabab: …" xabari.

Faqat **Yangi**, **Tasdiqlangan**, **Yig'ilmoqda** holatlarida. Jo'natish bo'lsa: "Buyurtma bo'yicha jo'natish bor — avval qaytarish hujjati kerak." — bunda xato jo'natishni bekor qiling (2.10) yoki qaytarish qiling (2.16). Bekor qilingan buyurtma tiklanmaydi — kerak bo'lsa yangisini yarating.

## 2.16 Qaytarish

**Kim:** Operator, Omborchi   **Menyu:** Distributsiya → Qaytarishlar → **Qaytarish** (yoki buyurtma kartasidagi **Qaytarish** — shu buyurtma bilan)

Mijoz tovarni qaytarsa — qaytarish hujjati (DQ-...) tuziladi. U ombor kirimi va mijoz qarzining kamayishi.

### 2.16.1 Forma

| Maydon | Nima kiritiladi |
|---|---|
| Mijoz * | Mijoz (buyurtma kartasidan ochilsa — tayyor) |
| Ombor * | Qaysi omborga kirim bo'ladi. Brak uchun alohida ombor bo'lsa — uni tanlang |
| Joylashuv * | Faqat joylashuvli (yacheykali) omborda — "Bu omborda joylashuvni tanlang." |
| Sabab | Qaytarish sabablaridan (Brak, Muddati o'tgan, Noto'g'ri tovar, Mijoz rad etdi) |
| Brak | Belgi — qaytgan tovar yaroqsiz (hujjat va ombor kirimida "(brak)" deb belgilanadi) |
| Izoh | |

Ikki blok:

- **Jo'natilgan tovarlardan** — mijoz tanlangach uning jo'natishlari qatorlari: jo'natish raqami, tovar, **Qaytarish mumkin** (jo'natilgan − avval qaytarilgan), narx. Qaytariladigan miqdorni yozing (asosiy birlikda).
- **Buyurtmasiz** — tovarni qidirib qo'shing, miqdor va (ixtiyoriy) narx. "Narx bo'sh bo'lsa — mijozning joriy narxi; tannarx — oxirgi kirim narxi."

**Saqlash** — hujjat darhol o'tkaziladi.

> **Muhim:** "Brak" belgisi tovarni o'zi boshqa omborga o'tkazmaydi — tovar siz tanlagan omborga kiradi. Yaroqsiz tovar sotuvga tushmasligi uchun brak omborini tanlang yoki keyin hisobdan chiqaring (4-bob, 4.5).

### 2.16.2 Ta'siri

| Qayerda | Buyurtma bo'yicha (jo'natishdan) | Buyurtmasiz |
|---|---|---|
| Narx | Jo'natishdagi haqiqiy narx (chegirma va aksiya bilan) | Kiritilgan yoki mijozning joriy narxi |
| Tannarx (omborga kirim) | Jo'natishdagi FIFO tannarx | Tovarning oxirgi kirim narxi |
| Ombor | Kirim — qoldiq oshadi | Kirim — qoldiq oshadi |
| Qarz | Kredit — qarz kamayadi | Kredit — qarz kamayadi |
| Buyurtma holati | O'zgarmaydi | — |
| Hisobotlar | Tushum kamayadi (qaytarish sanasi bo'yicha) | Shu |

Qaytarishdan keyin mijozga pul qaytarilmaydi — faqat qarzi kamayadi (yoki avans hosil bo'ladi). Qaytarish bilan qarz to'liq yopilsa buyurtma **Yopildi** bo'ladi.

### 2.16.3 DEMO misollari

**Buyurtma bo'yicha.** 12.11.2026: Anvar do'koni D-2026-00001 dan 2 qop yirtilgan sement qaytardi.

| | Qiymat |
|---|---|
| Narx | jo'natish qatori: 4 960 000 / 80 = 62 000 so'm/qop |
| Qaytarish summasi | 2 × 62 000 = 124 000 |
| Omborga kirim tannarxi | 2 × 40 000 = 80 000 |
| Brak | ✓, sabab "Brak" |
| Qarz | 15 376 400 − 124 000 = **15 252 400** |

Sovg'a qatoridan qaytarilgan qop 0 so'm bo'ladi (sovg'a narxi 0) — qarz kamaymaydi.

**Buyurtmasiz.** Mijoz boshqa yo'l bilan olgan 3 list gipsokartonni qaytardi, narx bo'sh: 3 × 68 000 (joriy Chakana) = 204 000 so'm kredit; omborga kirim tannarxi 3 × 50 000 = 150 000.

### 2.16.4 Cheklovlar va bekor qilish

| Holat | Xabar |
|---|---|
| Jo'natilgandan ko'p | "Jo'natilgandan ko'p qaytarib bo'lmaydi (qolgan …)." |
| Qatorlar bo'sh | "Kamida bitta tovar qo'shing." |
| Buyurtmasiz qatorda narx topilmadi va kiritilmadi | "…: narx belgilanmagan." |
| Ombor kirimi muammosi | "Ombor kirimi xatosi." |

Xato qaytarish **Qaytarishlar** ro'yxatida sabab bilan bekor qilinadi: ombor kirimi qaytariladi, qarz tiklanadi. Qaytgan tovar allaqachon boshqa mijozga chiqarilgan bo'lsa: "Qaytgan tovar allaqachon chiqarilgan — bekor qilib bo'lmaydi."

## 2.17 Aksiyalar

**Kim:** Rahbar   **Menyu:** Distributsiya → Aksiyalar

> Aksiya buyurtma saqlanganda avtomatik qo'llanadi (web, bot, Mini App). Chegirma rol limiti va minimal narxdan ozod; sovg'a narxsiz alohida qator bo'lib qo'shiladi.

Ro'yxatda: Nomi, Turi, Nimaga, Shart, Muddat, Narx turi, Holat, **Qatorlarda** (nechta buyurtma qatorida ishlatilgani). Sukut bo'yicha amaldagilari; **Hammasi** — tugaganlari va to'xtatilganlari bilan.

### 2.17.1 Aksiya turlari

| Turi | Nimaga | Shart | Natija |
|---|---|---|---|
| **Chegirma** | Bitta tovar, yoki kategoriya, yoki hamma tovar | Qatorda kamida N (asosiy birlikda); 0 — shartsiz (muddatli chegirma) | Qatorga aksiya % |
| **Sovg'a (N+M)** | Bitta tovar (majburiy) | Buyurtmada shu tovar jami ≥ N (asosiy birlikda) | M birlik sovg'a — o'sha yoki boshqa tovar, narxsiz qator |

### 2.17.2 Forma maydonlari

| Maydon | Nima kiritiladi |
|---|---|
| Nomi | Aksiya nomi (buyurtma kartasida belgi ustida ko'rinadi) |
| Turi | Chegirma yoki Sovg'a (N+M) |
| Faol | Olib tashlansa aksiya to'xtaydi |
| Boshlanishi / Tugashi (bo'sh — muddatsiz) | Amal qilish sanalari |
| Narx turi (faqat shu narx turidagi mijozlarga) | Bo'sh — "Hamma mijoz" |
| Tovar | Chegirmada — ixtiyoriy; sovg'ada — majburiy |
| Kategoriya (tovar tanlanmasa) | Chegirma uchun; tovar ham, kategoriya ham bo'sh — "Hamma tovar" |
| Chegirma, % | 0 dan katta, 100 gacha |
| Qatorda kamida (asosiy birlikda) | Chegirma sharti |
| Har N | Sovg'a sharti — asosiy birlikda |
| M sovg'a | Sovg'a miqdori — asosiy birlikda |
| Sovg'a tovar (bo'sh — o'sha tovar) | Boshqa tovar sovg'a qilinsa |
| Takrorlanadi (20 → 2 sovg'a) | Belgilansa har N uchun M; belgilanmasa bir marta |
| Izoh | |

### 2.17.3 DEMO aksiyalari

**Sement 10+1:**

| Maydon | Qiymat |
|---|---|
| Nomi | Sement 10+1 |
| Turi | Sovg'a (N+M) |
| Boshlanishi | 01.11.2026 |
| Tovar | DEMO-SEM-400 Sement M400 |
| Har N / M sovg'a | 10 / 1 (qop) |
| Takrorlanadi | ✓ |

| Buyurtmada | Sovg'a |
|---|---|
| 8 qop | yo'q (10 dan kam) |
| 25 qop | 2 qop (25 / 10 = 2 marta) |
| 2 poddon = 80 qop | 8 qop |
| 80 qop, "Takrorlanadi" belgisiz | 1 qop |

Shart bitta tovarning buyurtmadagi **jami** miqdori bo'yicha — sement ikki qatorda (poddon va qop) bo'lsa ham qo'shib hisoblanadi.

**Kafel ≥ 100 m² — 5%:**

| Maydon | Qiymat |
|---|---|
| Turi | Chegirma |
| Tovar | DEMO-KAF-3030 Kafel 30×30 |
| Chegirma, % | 5 |
| Qatorda kamida | 100 (m²) |

69 quti = 99,36 m² — aksiyasiz; 70 quti = 100,8 m² — 5%.

### 2.17.4 Qanday qo'llanadi

- **Har saqlashda qayta hisoblanadi.** Miqdorni kamaytirsangiz sovg'a kamayadi yoki yo'qoladi. Buyurtma saqlangan kunning aksiyalari olinadi.
- **Bosqichli chegirma.** Hajmga qarab chegirma uchun bir nechta aksiya turli "Qatorda kamida" bilan tuziladi; qatorga mos keladiganlarining **eng kattasi** qo'llanadi. Masalan, "Kafel ≥ 100 m² — 5%" va "Kafel ≥ 300 m² — 8%" bo'lsa, 450 m² ga 8%, 150 m² ga 5%.
- **Chegirmalar qo'shilmaydi.** Bir qatorga faqat bitta aksiya chegirmasi (eng kattasi). Qo'lda berilgan chegirma esa ustiga ketma-ket qo'shiladi (2.14).
- **Narx turi bo'yicha cheklash.** Narx turi tanlangan aksiya faqat shu narx turidagi mijozlarga. Masalan "faqat Diler"ga qo'yilgan aksiya Anvar do'koni (Chakana) buyurtmasiga tushmaydi.
- **Sovg'a qatori** — narxi 0, "sovg'a" belgisi bilan, asosiy birlikda. U ham rezervga olinadi, yig'ish varaqasida va jo'natishda bor, FIFO tannarxi foydani kamaytiradi. Sotuvdan olingan tovar sovg'a sifatida qo'shilmaydi.
- Aksiya chegirmasi rol limiti va minimal narxdan **ozod** — rahbar qarori hisoblanadi.
- Katalogda (web forma va Mini App) aksiyali tovar yonida belgi: "aksiya −5%" yoki "10+1".
- Kartada aksiyali qatorda "aksiya −5%" belgisi, sichqoncha bilan ustiga borilsa aksiya nomi.

### 2.17.5 To'xtatish

Aksiya o'chirilmaydi (eski buyurtmalarda iz qoladi). Ro'yxatdagi **To'xtatish** tugmasi uni nofaol qiladi, **Yoqish** — qayta yoqadi. Muddati tugagan aksiya o'zi ishlamay qoladi. Allaqachon saqlangan buyurtmalar o'zgarmaydi, toki ular qayta saqlanmaguncha.

Forma xatolari:

| Xabar | Sabab |
|---|---|
| "Tugash sanasi boshlanishdan oldin." | Sanalar teskari |
| "Chegirma 0 dan katta va 100 dan oshmasin." | Foiz noto'g'ri |
| "Sovg'a aksiyasi uchun tovarni tanlang." | Sovg'ada tovar bo'sh |
| "N va M musbat bo'lsin." | Har N yoki M sovg'a 0 |
| "Majburiy maydon to'ldirilmagan." | Nomi bo'sh |

## 2.18 Tez-tez uchraydigan xatolar

| Xabar | Nimani anglatadi | Nima qilish kerak |
|---|---|---|
| "Mijozni tanlang." | Forma mijozsiz saqlanmoqda | Mijozni tanlang |
| "Kamida bitta tovar qo'shing." | Qatorlar yo'q yoki hammasi 0 | Tovar qo'shing |
| "Miqdor musbat bo'lishi kerak." | Manfiy miqdor | Musbat son yozing |
| "Mijoz distributsiyaga qo'shilmagan." | CRM mijozida distributsiya kartasi yo'q | Mijozlar → Mavjud mijozni qo'shish (1-bob, 1.9.3) |
| "…: narx belgilanmagan." | Mijoz narx turida ham, standart turda ham narx yo'q | Narxlar sahifasida narx kiriting |
| "… sotuvdan olingan." | Tovar kartasida "Sotuvda" belgisi yo'q | Tovar kartasini tekshiring |
| "Chegirma 5% — sizning limitingiz 3%." | Qo'lda chegirma rol limitidan katta | Chegirmani kamaytiring yoki rahbarga bering |
| "DEMO-SEM-400 Sement M400: narx 51 700 minimal narxdan (52 000) past." | Chegirmadan keyingi narx minimaldan past (Bunyodkor, Diler 55 000 − 6% = 51 700 so'm/qop) | Chegirmani kamaytiring |
| "Foiz noto'g'ri." | Chegirma 0–100 oralig'ida emas | To'g'ri foiz yozing |
| "Bu holatda o'zgartirib bo'lmaydi." | Yangi bo'lmagan buyurtmani tahrirlash | Tasdiqni qaytaring (2.6) |
| "Buyurtmani boshqa foydalanuvchi o'zgartirdi — sahifani yangilang." | Bir vaqtda ikki xodim | Sahifani yangilab qayta urining |
| "Mijoz hali tasdiqlanmagan — avval mijoz kartasini tasdiqlang." | Agent qo'shgan yangi nuqta | Mijozlar → Tasdiqlash (1-bob, 1.9.2) |
| "Yetkazish uchun minimal buyurtma summasi — 500 000." | Buyurtma minimal summadan kam | Buyurtmani ko'paytiring yoki "O'zi olib ketadi" |
| "Qarz + buyurtma … — limit … dan oshadi." | Kredit limiti yetmaydi | To'lov oling yoki rahbar ruxsati (2.5.1) |
| "Muddati o'tgan qarz bor: …" | To'lov muddati o'tgan qarz | To'lov oling yoki rahbar ruxsati |
| "Hech bir savdo omborida bu tovarlar yo'q — omborni qo'lda tanlang." | Avtomatik ombor topilmadi | Formada omborni tanlang yoki kirim kuting |
| "Holatni o'zgartirib bo'lmaydi: … → …." | Bu holatdan bunday amal yo'q | 2.4 jadvaliga qarang |
| "Buyurtma bo'yicha jo'natish bor — avval qaytarish hujjati kerak." | Jo'natilgan buyurtmani bekor qilish yoki tasdiqni qaytarish | Jo'natishni bekor qiling (2.10) yoki qaytarish (2.16) |
| "Bu holatdagi buyurtmani jo'natib bo'lmaydi." | Tasdiqlanmagan yoki yakunlangan | Avval tasdiqlang |
| "N-qator: omborda yetarli emas (mavjud …)." | Qoldiq yetmaydi | Miqdorni kamaytiring, kirimdan keyin Rezervni yangilang |
| "Faqat jo'natilgan (yetkazilmagan) buyurtma jo'natishi bekor qilinadi — aks holda qaytarish." | Yetkazilgan buyurtma | Qaytarish qiling |
| "Bekor qilish sababini yozing." | Sabab bo'sh yoki 3 belgidan qisqa | Sababni yozing |
| "Jo'natilgandan ko'p qaytarib bo'lmaydi (qolgan …)." | Qaytarish miqdori ortiq | Miqdorni kamaytiring |
| "Ruxsat yo'q" | Sizda sahifa yoki amal uchun Change huquqi yo'q | Administratorga (1-bob, 1.10) |
| "Saqlashda xatolik yuz berdi." | Kutilmagan tizim xatosi | Xatolar jurnaliga yozing (K1/K2) |
