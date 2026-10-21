# 3. Pul va qarz

Bu bob distributsiyada pul qanday yig'ilishi va mijoz qarzi qanday hisoblanishini tushuntiradi:
- mijoz to'lovi — ofisda (kassa yoki bank) va dalada agent orqali (naqd, karta, Click va boshqa usullar);
- agent qo'lidagi naqd pul va uni kassaga topshirish;
- mijoz hisob varag'i (ledger), qarz muddatlari va akt sverki;
- muddati o'tgan qarz va kredit limitining buyurtmaga ta'siri;
- oldindan to'lov (avans) va uning avtomatik yopilishi;
- tizimga o'tishdagi boshlang'ich qarzlar;
- to'lovlarning Moliya moduliga qanday tushishi va nima ataylab tushmasligi.

Bob bilan asosan **kassir**, **rahbar** va **agent** ishlaydi. Buxgalter 3.9 bo'limni albatta o'qishi kerak.

**Namunaviy ssenariy (2026-yil noyabr, umumiy demo):**
- DEMO Anvar do'koni (Chilonzor, Chakana narx turi, limit 20 000 000, to'lov muddati 7 kun, agent — Jasur).
- DEMO Bunyodkor qurilish (Yunusobod, Diler narx turi, limit 50 000 000, to'lov muddati 14 kun, agent biriktirilmagan).
- Hisoblar Moliya'da: "Naqd pul" (kassa), "Hisob raqam" (bank), "Korporativ karta" (bank). Hammasi so'mda.

## 3.1 Asosiy tushunchalar

- **To'lov** — mijozdan olingan pul hujjati (raqami `DP-2026-00001` ko'rinishida). U darhol mijoz qarzini kamaytiradi.
- **To'lov usuli** — Naqd, Karta, Click, Payme, Uzum, Pul o'tkazmasi.
- **Kassa / hisob** — pul tushgan Moliya hisobi. Faqat **so'mdagi**, faol, "Naqd pul" yoki "Bank" belgili hisob tanlanadi.
- **Agent qo'lida (naqd)** — agent mijozdan olgan, lekin hali kassaga topshirmagan naqd pul. Mijoz qarzi darhol kamayadi, kassa qoldig'i esa topshirilgandagina oshadi.
- **Kassaga topshirish** — agent qo'lidagi barcha naqd to'lovlarni bitta hujjatga (raqami `DK-2026-00001`) bog'lash. Kassir pulni sanab **qabul qiladi** yoki **rad etadi**.
- **Farq** — kassir sanagan summa minus to'lovlar bo'yicha summa. Manfiy — kamomad, musbat — ortiqcha.
- **Mijoz hisob varag'i (ledger)** — har mijoz bo'yicha debet (jo'natish, kirish qarzi) va kredit (to'lov, qaytarish, tuzatish) yozuvlari. Qarz, muddatlar va akt sverki faqat shu yerdan hisoblanadi.
- **Balans** — barcha debetlar minus barcha kreditlar. Musbat — mijoz qarzdor; manfiy — mijozning avansi.
- **To'lov muddati** — har jo'natish debetining muddati = jo'natish sanasi + mijoz kartasidagi "To'lov muddati (kun)".
- **FIFO yopilish** — har qanday kredit (to'lov, qaytarish) mijozning **eng eski muddatli** qarzini birinchi yopadi. To'lovni ma'lum bir buyurtmaga "bog'lash" qarz hisobini o'zgartirmaydi.
- **Muddatlar (aging)** — ochiq qarzning muddatidan necha kun o'tgani: Muddati kelmagan, 1–7, 8–30, 31–60, 60+ (60 kundan ortiq).
- **Avans (oldindan to'lov)** — mijoz to'lagan, lekin hali hech qaysi jo'natish bilan yopilmagan summa.
- **Akt sverki** — tanlangan davr uchun mijoz bilan o'zaro hisob-kitob solishtirmasi: boshlang'ich qoldiq, yozuvlar va yakuniy qoldiq.

### Pul qanday harakatlanadi

```
 Mijoz
   | naqd (dala)                    | karta / Click (dala)          | ofisda (kassa yoki bank)
   v                                v                               v
 [Agent qo'lida]               [Sozlamadagi usul hisobi]       [Tanlangan kassa / hisob]
   | 📤 Kassaga topshirish          |                               |
   v                                |                               |
 [Kassir: sanab qabul qiladi]       |                               |
   | farq -> alohida yozuv          |                               |
   v                                v                               v
 Moliya: FIN tranzaksiya "Kirim" (Sotuv daromadi, kontragent = mijoz)

 Har uchala holatda ham mijoz qarzi to'lov kiritilgan ondayoq kamayadi.
```

## 3.2 Agent to'lovlarini sozlash

**Kim:** rahbar (Admin)   **Menyu:** Distributsiya → Sozlamalar

Agent botda faqat rahbar yoqqan usullarni ko'radi. Sozlamalar sahifasida ikki joy to'ldiriladi.

**1. "Umumiy sozlamalar" kartasi:**

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Agent naqdi topshiriladigan kassa | Agent botdan "Kassaga topshirish" bosganda pul qaysi kassaga yozilishi | Naqd pul |

**2. "Agent to'lovlari" kartasi** — har usul uchun bitta qator:

| Ustun | Ma'nosi | Misol |
|---|---|---|
| To'lov usuli | Naqd, Karta, Click, Payme, Uzum, Pul o'tkazmasi | Karta |
| Agent qabul qiladi | Belgilangan bo'lsa — botda shu usul tugmasi chiqadi | ✓ |
| Hisob | Naqdsiz usulda pul darhol tushadigan hisob. Naqd uchun hisob tanlanmaydi — "agent qo'lida → kassaga topshiradi" yozuvi turadi | Korporativ karta (bank) |

Demo uchun sozlash:
1. Naqd — ✓ (hisob kerak emas).
2. Karta — ✓, Hisob = `Korporativ karta (bank)`.
3. Click — ✓, Hisob = `Hisob raqam (bank)`.
4. Payme, Uzum, Pul o'tkazmasi — belgilanmagan.
5. **Saqlash** ni bosing.

> **Eslatma:** Hech narsa saqlanmagan bo'lsa, tizim standart sifatida Naqd, Karta va Click ni yoqilgan deb hisoblaydi. Karta va Click uchun hisob baribir tanlanishi shart.

> **Muhim:** Usul yoqilgan, lekin hisobi tanlanmagan bo'lsa, agent shu usulda to'lov kiritganda bot "To'lov usuli uchun hisob tanlanmagan (Sozlamalar → Agent to'lovlari)" deydi. O'chirilgan usul uchun — "Bu to'lov usuli agentlar uchun yoqilmagan".

> **Muhim:** "Agent naqdi topshiriladigan kassa" bo'sh bo'lsa, agent botdan kassaga topshira olmaydi ("Kassa yoki hisobni tanlang ..."). Bu holda kassir pulni web'da o'zi qabul qiladi (3.5).

## 3.3 Ofisda to'lov qabul qilish

**Kim:** kassir   **Menyu:** Distributsiya → To'lovlar

To'lov oynasini uch joydan ochish mumkin:
- **To'lovlar** sahifasida **To'lov qabul qilish** → mijozni tanlang → **Qo'llash**;
- **Qarzlar** sahifasida mijoz qatoridagi 💵 tugma (Summa maydoniga muddati o'tgan qarz oldindan yoziladi, u 0 bo'lsa — bo'sh);
- buyurtma kartasidagi **To'lov** tugmasi (to'lov shu buyurtmaga belgilanadi).

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Summa | To'langan summa, so'mda, 0 dan katta | 10 000 000 |
| To'lov usuli | Naqd / Karta / Click / Payme / Uzum / Pul o'tkazmasi | Pul o'tkazmasi |
| Kassa / hisob | Pul tushgan hisob. Bo'sh qoldirilsa — "Agent qo'lida (naqd)" | Hisob raqam (bank) |
| Izoh | Ixtiyoriy, 300 belgigacha | "Avans, to'lov topshiriqnomasi №45" |

**Misol (02.11.2026):** DEMO Bunyodkor qurilish bank orqali 10 000 000 so'm avans o'tkazdi.
1. **To'lovlar** → **To'lov qabul qilish** → mijoz `DEMO Bunyodkor qurilish` → **Qo'llash**.
2. Summa `10000000`, To'lov usuli `Pul o'tkazmasi`, Kassa / hisob `Hisob raqam (bank)`.
3. **To'lov qabul qilish** ni bosing.

**Natija:** `DP-2026-00001` yaratiladi. Mijoz balansi −10 000 000 (avans). Moliya'da "Hisob raqam" ga 10 000 000 kirim yoziladi. Mijoz Telegram'ga ulangan bo'lsa, unga "To'lovingiz qabul qilindi: 10 000 000 so'm (DP-2026-00001). Qolgan qarz: −10 000 000 so'm" xabari boradi.

> **Muhim:** Naqddan boshqa usulda hisob tanlash shart. Aks holda: "Kassa yoki hisobni tanlang (naqddan boshqa to'lov darhol hisobga tushadi)."

> **Muhim:** Ofisda naqd to'lov kiritib, hisobni bo'sh qoldirsangiz ("Agent qo'lida (naqd)"), pul **buyurtma agenti** qo'lida deb yoziladi (buyurtmasiz to'lovda — mijoz kartasidagi agent) va "Agentlar naqdi va kassa" sahifasida o'sha agent naqdida chiqadi. Agent yo'q bo'lsa — to'lovni kiritgan foydalanuvchi qo'lida. Kassaga darhol tushgan naqd uchun hisobda `Naqd pul` ni tanlang.

> **Eslatma:** Web'da hamma 6 usul tanlanadi — "Agent to'lovlari" sozlamasi faqat botga ta'sir qiladi.

### To'lovlar ro'yxati

Ustunlar: Raqam (botdan kelgan bo'lsa yonida "bot" belgisi), Sana, Mijoz, Summa, To'lov usuli, Qabul qildi, Kassa / hisob, Buyurtmalar. "Kassa / hisob" ustunida hali topshirilmagan naqd uchun sariq **Agent qo'lida (naqd)** belgisi, bekor qilingan to'lov uchun qizil **Bekor qilingan** yozuvi chiqadi (summa ustidan chizilgan).

Filtrlar: to'lov usuli; holat (Hammasi / Agent qo'lida (naqd) / Bekor qilingan); sana dan–gacha; qidiruv (raqam yoki mijoz nomi).

Ro'yxatda faqat ko'rinish doirangizdagi mijozlar va agentlar to'lovlari chiqadi (agent — o'z mijozlari, supervayzer — jamoasi).

### To'lovni bekor qilish

Xato kiritilgan to'lov qatoridagi ✕ tugmasi → sababni yozing → OK.

**Natija:** to'lov "Bekor qilingan" bo'ladi, Moliya yozuvi o'chiriladi, mijoz hisob varag'iga teskari yozuv ("To'lov bekor qilindi") tushadi — qarz qayta oshadi. Buyurtma shu to'lov bilan **Yopildi** bo'lgan bo'lsa va qarzi endi yopilmasa, u qayta ochiladi (Yetkazildi yoki Jo'natildi; sabab holat tarixida).

> **Muhim:** Kassaga topshirilgan (topshirish hujjatiga kirgan) agent to'lovini to'g'ridan-to'g'ri bekor qilib bo'lmaydi: "Bu to'lov kassaga topshirilgan. Avval Agentlar naqdi va kassa sahifasida topshirishni bekor qiling." Avval topshirish hujjatini bekor qiling (3.5.4) — to'lovlar yana agent qo'lida bo'ladi, keyin xato to'lovni shu yerda oddiy tartibda bekor qiling.

## 3.4 Dalada to'lov (agent, bot)

**Kim:** agent, ekspeditor   **Menyu:** Telegram bot → mijoz kartasi → 💵 To'lov

1. Mijozni tanlang (🗺 Marshrut yoki 🔎 Mijoz), **📍 Keldim**.
2. **💵 To'lov** ni bosing va summani raqam bilan yozing, masalan `2500000`.
3. Bot "2 500 000 so'm. To'lov usuli:" deb so'raydi — rahbar yoqqan usullardan birini bosing.
4. Bot javobi: "✅ To'lov DP-2026-00002 qabul qilindi: 2 500 000 so'm. Qo'lingizda naqd: 2 500 000 so'm."

| Usul | Pul qayerda | Mijoz qarzi | Moliya |
|---|---|---|---|
| Naqd | Agent qo'lida | Darhol kamayadi | Faqat kassir qabul qilganda |
| Karta, Click va boshqalar | Sozlamadagi usul hisobida | Darhol kamayadi | Darhol kirim |

**Misol (05.11.2026):** Jasur DEMO Anvar do'konidan 2 500 000 so'm naqd va 1 000 000 so'm karta orqali oldi.
- `DP-2026-00002` — Naqd, 2 500 000 → Jasurning qo'lida.
- `DP-2026-00003` — Karta, 1 000 000 → darhol "Korporativ karta" hisobiga.
- Anvar do'koni qarzi 3 500 000 ga kamaydi.

> **Eslatma:** Tugmani ikki marta bossangiz yoki internet uzilib bot qayta yuborsa ham to'lov **bitta** yoziladi.

> **Eslatma:** Tashrif paytida kiritilgan to'lov tashrif natijasiga ("To'lov") ham yoziladi — hisobotda "To'lov bilan" ustuniga kiradi (5-bob).

> **Eslatma:** Ekspeditor reys paytida manzil kartasidagi 💵 To'lov bilan xuddi shu qoidada pul oladi: naqd — ekspeditor qo'lida, naqdsiz — usul hisobiga.

Agent qo'lidagi pulni botda ko'rish: **💵 Kassa** → "💵 Qo'lingizdagi naqd: … so'm. Bugun yig'ilgan: … so'm."

## 3.5 Agent naqdini kassaga topshirish

**Kim:** agent (bot) va kassir (web)   **Menyu:** Telegram bot → 💵 Kassa → 📤 Kassaga topshirish; web: Distributsiya → To'lovlar → **Agentlar naqdi va kassa**

### 3.5.1 Ikki yo'l

| Yo'l | Kim boshlaydi | Qadamlar |
|---|---|---|
| A. Botdan topshirish | Agent | Bot: 📤 Kassaga topshirish → tasdiq → rahbarlarga xabar → kassir web'da sanab **Qabul qilish** yoki **Rad etish** |
| B. To'g'ridan-to'g'ri kassada | Kassir | Agent pulni olib keladi → web'da agent qatoridagi **Qabul qilish** → sanalgan summa → saqlash (topshirish va qabul bitta amalda) |

### 3.5.2 Botdan topshirish (agent)

1. Kechqurun **💵 Kassa** ni bosing — qo'lingizdagi summa ko'rinadi.
2. **📤 Kassaga topshirish** → bot: "2 500 000 so'mni kassaga topshirasizmi? Kassir sanab qabul qiladi." → tasdiqlang.
3. Pulni kassirga bering.

**Natija:** `DK-2026-00001` hujjati "kutmoqda" holatida yaratiladi. Unga qo'lingizdagi **barcha** naqd to'lovlar kiradi. Rahbarlarga Telegram'da "Jasur kassaga 2 500 000 so'm topshirdi — qabul qiling." xabari boradi. Hujjatga "Agent naqdi topshiriladigan kassa" sozlamasidagi kassa yoziladi; kassir qabul qilishda boshqa kassani tanlashi mumkin (3.5.3).

> **Muhim:** Agentda bitta kutilayotgan topshirish bo'lsa, ikkinchisini yuborib bo'lmaydi: "Agentning kutilayotgan topshirishi bor — avval uni qabul qiling yoki rad eting." Qo'lida naqd bo'lmasa: "Agent qo'lida topshiriladigan naqd pul yo'q."

### 3.5.3 Kassir: sanab qabul qilish

**Agentlar naqdi va kassa** sahifasi ikki qismdan iborat.

**"Agentlar qo'lidagi naqd pul"** jadvali:

| Ustun | Ma'nosi |
|---|---|
| Agent | Agent ismi; botdan topshirgan bo'lsa "topshirdi, kutmoqda" belgisi |
| To'lovlar | Topshirilmagan naqd to'lovlar soni. Raqamni bossangiz, ro'yxat ochiladi (raqam, sana, mijoz, summa) |
| Summa | Agent qo'lidagi jami naqd |
| Eng eskisi | Eng eski topshirilmagan to'lov sanasi |
| (tugma) | **Qabul qilish** |

Qadamlar:
1. Agent qatoridagi **Qabul qilish** ni bosing.
2. Oynada "To'lovlar bo'yicha: 2 500 000" ko'rinadi. Agent botdan topshirgan bo'lsa — bu topshirish hujjatidagi summa.
3. Maydonlarni to'ldiring:

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Kassa / hisob | Pul qo'yiladigan kassa | Naqd pul |
| Sanalgan | Qo'lda sanalgan haqiqiy summa (oldindan "To'lovlar bo'yicha" summa yozilgan) | 2 490 000 |
| Izoh | Ixtiyoriy | "1 ta 10 000 lik yetmadi" |

4. **Qabul qilish** ni bosing.

**Natija (misol):** `DK-2026-00001` qabul qilindi, Farq = 2 490 000 − 2 500 000 = **−10 000** (kamomad). Moliya'da:
- "Naqd pul" hisobiga **2 500 000 kirim** ("Sotuv daromadi", kontragent — DEMO Anvar do'koni, maqsad "Distributsiya to'lovi DP-2026-00002");
- "Naqd pul" hisobidan **10 000 chiqim** ("Agent kassa farqi" moddasi, maqsad "Kamomad: DK-2026-00001"). Jasur agent KPI sxemasida HR xodimiga bog'langan bo'lsa, chiqim kontragenti — shu **xodim** (kontragent turi Xodim): kamomad agent qarzi bo'lib ish haqi qarzidan ayriladi. Bog'lanmagan bo'lsa — oddiy kassa chiqimi (kontragent nomi — Jasur).
- Kassa qoldig'i jami +2 490 000 — qutidagi haqiqiy pulga teng. Jasurning qo'lida 0 qoladi.

Ortiqcha bo'lsa (masalan sanalgan 2 505 000), farq +5 000 shu moddada **kirim** bo'lib yoziladi.

> **Eslatma:** Mijoz qarzi farqdan o'zgarmaydi: mijoz 2 500 000 to'lagan, kamomad — agent va kompaniya o'rtasidagi masala. Agent HR xodimiga bog'langan bo'lsa (5-bob, 5.7), kamomad uning ish haqi qarzidan avtomatik ayriladi; bog'lanmagan bo'lsa undirish rahbar qarori bilan qo'lda qilinadi.

> **Eslatma:** Agent botdan topshirgan bo'lsa (qatorda "topshirdi, kutmoqda"), oynadagi summa — topshirish hujjatidagi summa, "Kassa / hisob" tanlovi esa hisobga olinadi: boshqa kassani tanlasangiz pul o'sha kassaga yoziladi. Agent topshirgandan keyin yana naqd olgan bo'lsa, qatorda "topshirishdan keyin olingan: … (keyingi topshirishga)" deb ko'rsatiladi va oynada ogohlantirish chiqadi — bu pul hujjatga kirmaydi va agent qo'lida qoladi. "Sanalgan" ga faqat topshirish hujjatidagi pulni yozing.

### 3.5.4 Rad etish

Pastdagi **"Kassaga topshirishlar"** jadvalida (oxirgi 100 ta) kutayotgan hujjat qatorida **Rad etish** → sabab → OK.

**Natija:** hujjat "Bekor qilingan", to'lovlar yana agent qo'lida. Agent qaytadan topshiradi.

### 3.5.5 Qabul qilingan topshirishni bekor qilish

Qabul qilingan topshirishda xato topilsa (masalan, ichida xato kiritilgan to'lov bor):

1. **"Kassaga topshirishlar"** jadvalida qabul qilingan hujjat qatoridagi **Bekor qilish** ni bosing.
2. Sababni yozing → OK.

**Natija:** hujjat "Bekor qilingan"; to'lovlarning Moliya yozuvlari va farq yozuvi bekor qilinadi, to'lovlar yana agent qo'lida. Keyin xato to'lovni To'lovlar ro'yxatida oddiy tartibda bekor qiling (3.3), qolganini agent qaytadan topshiradi.

Jadval ustunlari: Raqam, Sana, Agent, Kassa / hisob, To'lovlar bo'yicha, Sanalgan, Farq (kamomad qizil, ortiqcha yashil).

## 3.6 Mijoz hisob varag'i va balansi

Mijoz qarzi hech qachon qo'lda yozilmaydi — u hujjatlardan avtomatik yig'iladi:

| Hujjat (akt sverkida "Turi") | Yozuv | Muddat |
|---|---|---|
| Kirish qoldig'i | Debet (qarz) yoki kredit (avans) | Importdagi muddat yoki import sanasi + to'lov muddati |
| Kirish qoldig'i bekor qilindi | Teskari yozuv (3.11) | — |
| Jo'natish | Debet — jo'natilgan summa | Jo'natish sanasi + to'lov muddati |
| Jo'natish bekor qilindi | Teskari yozuv | — |
| To'lov | Kredit | — |
| To'lov bekor qilindi | Teskari yozuv | — |
| Qaytarish | Kredit — qaytgan tovar summasi | — |
| Qaytarish bekor qilindi | Teskari yozuv | — |
| Tuzatish | Kredit — reys yopilganda mijozga yetkazilmagan va omborga qaytmagan tovar summasi (mijoz olmagan tovarga to'lamaydi) | — |

**Formula:** Balans = kirish qarzi + jo'natishlar − to'lovlar − qaytarishlar − tuzatishlar.

> **Eslatma:** Buyurtma tasdiqlanganda qarz yozilmaydi. Qarz faqat **jo'natishda** (ombordan chiqqanda) paydo bo'ladi.

> **Eslatma:** Teskari yozuv (bekor qilish) muddatlar hisobida asl yozuv bilan birga chiqarib tashlanadi — bekor qilingan jo'natish eski qarzlarni "yopib" qo'ymaydi.

Mijoz Telegram'ga ulangan bo'lsa, botda **💳 Qarzim** (qarz, muddati kelmagan/o'tgan, eng uzoq kechikish, limit, avans, so'nggi 5 to'lov) va **📄 Akt sverki** (shu oy / o'tgan oy / oxirgi 3 oy) tugmalari bor.

## 3.7 Qarzlar sahifasi va muddatlar

**Kim:** rahbar, kassir, agent (o'z mijozlari)   **Menyu:** Distributsiya → Qarzlar

Yuqorida to'rtta karta: **Qarz** (jami), **Muddati o'tgan** (qizil), **60 kundan ortiq** (qizil), **Avans (oldindan to'lov)** (yashil).

Filtrlar (tanlanganda sahifa darhol yangilanadi):
- Barcha agentlar / bitta agent;
- Barcha hududlar / hudud;
- Hammasi / Muddati o'tgan / Limitdan oshgan / Avans (oldindan to'lov).

Jadval ustunlari:

| Ustun | Ma'nosi |
|---|---|
| Mijoz | Nomi va hududi |
| Agent | Mijoz kartasidagi agent |
| Qarz | Ochiq qarz jami; avans bo'lsa ostida yashil "+summa" |
| Muddati kelmagan | Muddati hali kelmagan qism |
| 1–7, 8–30 | Muddatidan 1–7 va 8–30 kun o'tgan qism (sariq) |
| 31–60, 60+ | 31–60 va 60 kundan ortiq o'tgan qism (qizil) |
| Kredit limiti | Qarz limitdan oshsa — qizil |
| (tugmalar) | 📄 akt sverki, 💵 to'lov qabul qilish |

Ro'yxatda faqat qarzi yoki avansi bor mijozlar chiqadi; tartib — avval muddati o'tgani ko'plar.

### Muddat qanday hisoblanadi

1. Mijozning barcha kreditlari (to'lov, qaytarish, tuzatish) jamlanadi.
2. Ular debetlarni **muddati eng eskisidan** boshlab yopadi.
3. Har yopilmay qolgan qism muddatidan bugungacha o'tgan kunlar bo'yicha ustunga tushadi: 0 va kam — muddati kelmagan; 1–7; 8–30; 31–60; 61 va ko'p — 60+.
4. Kreditlar debetlardan ko'p bo'lsa, ortig'i — **Avans**.

**Misol — DEMO Anvar do'koni:**

| Sana | Yozuv | Summa | Muddat |
|---|---|---|---|
| 01.11 | Kirish qoldig'i | +1 500 000 | 05.11 |
| 03.11 | Jo'natish DJ-2026-00001 (100 qop sement × 62 000) | +6 200 000 | 10.11 (03.11 + 7 kun) |
| 05.11 | To'lov DP-2026-00002 (naqd) | −2 500 000 | |
| 05.11 | To'lov DP-2026-00003 (karta) | −1 000 000 | |

Kreditlar jami 3 500 000. FIFO: avval 05.11 muddatli kirish qoldig'i 1 500 000 to'liq yopiladi, qolgan 2 000 000 jo'natishdan ayriladi → ochiq qism 6 200 000 − 2 000 000 = **4 200 000**, muddati 10.11.

| Qaysi sanada qaraladi | Ustun | Summa |
|---|---|---|
| 08.11 | Muddati kelmagan | 4 200 000 |
| 12.11 (2 kun o'tdi) | 1–7 | 4 200 000 |
| 20.11 (10 kun o'tdi) | 8–30 | 4 200 000 |
| 15.12 (35 kun o'tdi) | 31–60 | 4 200 000 |

### Buyurtmaning "Yopildi" holati

Har to'lov, qaytarish va jo'natishdan keyin tizim tekshiradi: buyurtmaning barcha jo'natish debetlari FIFO bo'yicha to'liq yopilgan bo'lsa, "Jo'natildi" yoki "Yetkazildi" holatidagi buyurtma avtomatik **Yopildi** ga o'tadi. To'lov, qaytarish yoki kirish qoldig'i bekor qilinib qarz yana ochilsa, Yopildi buyurtma qayta ochiladi (Yetkazildi; yetkazilmagan bo'lsa Jo'natildi). "Qisman jo'natildi" holatidagi buyurtma yopilmaydi — ataylab, qolgani hali jo'natilishi mumkin. "Qolganidan voz kechish" qilinsa va jo'natilgani to'langan bo'lsa — darhol Yopildi (2-bob, 2.11).

## 3.8 Muddati o'tgan qarz va kredit limiti: buyurtmaga ta'siri

Mijoz kartasida "Kredit limiti" va "To'lov muddati (kun)" beriladi (limit 0 dan, muddat 0–365 kun). Buyurtma **tasdiqlanayotganda** tizim ikki shartni tekshiradi:

| Shart | Xabar |
|---|---|
| Mijozda muddati o'tgan qarz bor (1 kundan ko'p) | "Muddati o'tgan qarz bor: {summa}." |
| Balans + tasdiqlangan, jo'natilmagan buyurtmalar + yangi buyurtma summasi > kredit limiti | "Qarz + buyurtma {summa} — limit {limit} dan oshadi." (+ "(shundan tasdiqlangan, hali jo'natilmagan buyurtmalar: {summa})") |

Shartlardan biri buzilsa, buyurtma tasdiqlanmaydi. Uni faqat **"Limitdan oshishga ruxsat"** huquqi (DstOverrideCredit) bor foydalanuvchi izoh yozib tasdiqlay oladi — web'da buyurtma kartasida yoki Telegram'da rahbarga kelgan xabardagi **✅ Tasdiqlash** tugmasi bilan. Agent buyurtmani saqlaganda ham shu tekshiruv bajariladi va shart buzilsa rahbarlarga "Buyurtma … kredit shartidan chiqdi — tasdiqlash kerak" xabari ketadi. Xabar tahrirdan keyin limitdan oshganda va tasdiqlash qarz sabab rad etilganda ham yuboriladi — bir buyurtmaga kuniga bir marta.

**Misol 1 — muddati o'tgan qarz (12.11.2026):** Anvar do'koni 50 list gipsokarton × 68 000 = 3 400 000 so'mlik buyurtma berdi. Balans 4 200 000, shundan 4 200 000 muddati o'tgan (2 kun). Limit bo'yicha 4 200 000 + 3 400 000 = 7 600 000 < 20 000 000 — muammo yo'q, lekin muddati o'tgan qarz bor → "Muddati o'tgan qarz bor: 4 200 000." Buyurtma "Yangi" holatida qoladi, rahbar ruxsati kerak.

**Misol 2 — limit (06.11.2026):** Balans 4 200 000 (muddati hali kelmagan). Yangi buyurtma 16 000 000 → 4 200 000 + 16 000 000 = 20 200 000 > 20 000 000 → "Qarz + buyurtma 20 200 000 — limit 20 000 000 dan oshadi."

> **Muhim:** Limit 0 — mijoz faqat oldindan to'lov bilan ishlaydi: avansi buyurtma summasini qoplamasa, tasdiq bloklanadi.

> **Eslatma:** Limit tekshiruvida balansga tasdiqlangan, yig'ilayotgan va qisman jo'natilgan buyurtmalarning hali **jo'natilmagan** qismi ham qo'shiladi. Masalan, Anvar do'konining 05.11 dagi 12 400 000 so'mlik tasdiqlangan (rezervdagi) buyurtmasi jo'natilmagan bo'lsa ham limitni band qiladi: Misol 2 dagi buyurtma uchun 4 200 000 + 12 400 000 + 16 000 000 = 32 600 000 > 20 000 000. Buyurtma kartasida va yangi buyurtma oynasida "Tasdiqlangan, jo'natilmagan" qatori ko'rinadi, "Limitdan qolgan" uni ayirib ko'rsatadi.

## 3.9 Oldindan to'lov (avans) va avtomatik yopilish

Avans uchun alohida hujjat yo'q — bu oddiy to'lov, faqat jo'natishdan **oldin** kiritilgan.

**Misol — DEMO Bunyodkor qurilish:**
1. 02.11 — 10 000 000 so'm avans (3.3 dagi `DP-2026-00001`). Balans −10 000 000; Qarzlar sahifasida mijoz "Avans" filtri bilan chiqadi.
2. 04.11 — buyurtma 150 qop sement × 55 000 (Diler) = 8 250 000 so'm (aksiya 10+1 bo'yicha yana 15 qop sovg'a, narxsiz). Tasdiqda: balans −10 000 000 + 8 250 000 = −1 750 000 < 50 000 000 — limit muammosi yo'q.
3. 04.11 — jo'natish `DJ-2026-00002`: debet 8 250 000, muddati 18.11 (04.11 + 14 kun). Shu ondayoq avans FIFO bo'yicha debetni to'liq yopadi va buyurtma darhol **Yopildi** holatiga o'tadi. Keyin yetkazilganda holat "Yopildi" bo'lib qoladi, buyurtma tarixiga yetkazish vaqti yoziladi.
4. Qolgan avans: 10 000 000 − 8 250 000 = **1 750 000**. U keyingi jo'natishni avtomatik yopadi.

> **Eslatma:** Avans hech qachon pul sifatida mijozga qaytarilmaydi — qaytarish hujjati ham faqat qarzni kamaytiradi. Mijozga pul qaytarish kerak bo'lsa, u Moliya modulida chiqim sifatida qo'lda yoziladi.

## 3.10 Akt sverki

**Kim:** kassir, rahbar, agent   **Menyu:** Distributsiya → Qarzlar → mijoz qatoridagi 📄 tugma

1. Sahifa ochiladi: standart davr — joriy oy boshidan bugungacha.
2. **Boshlanish** va **Tugash** sanalarini tanlab **Qo'llash** ni bosing.
3. Chop etish uchun **Chop etish** (yangi oynada imzo joylari bilan shakl).

Jadval: Sana, Turi, Raqam, Debet (sotuv), Kredit (to'lov), Qoldiq. Birinchi qator — "Boshlang'ich qoldiq" (davr boshigacha bo'lgan barcha yozuvlar), oxirida "Jami" va "Yakuniy qoldiq". Jadval ostida bugungi muddatlar: Muddati o'tgan, 1–7, 8–30, 31–60, 60+.

**Misol — Anvar do'koni, 01.11–12.11.2026:**

| Sana | Turi | Raqam | Debet | Kredit | Qoldiq |
|---|---|---|---|---|---|
| | Boshlang'ich qoldiq 01.11.2026 | | | | 0 |
| 01.11 | Kirish qoldig'i | Kirish qoldig'i | 1 500 000 | | 1 500 000 |
| 03.11 | Jo'natish | DJ-2026-00001 | 6 200 000 | | 7 700 000 |
| 05.11 | To'lov | DP-2026-00002 | | 2 500 000 | 5 200 000 |
| 05.11 | To'lov | DP-2026-00003 | | 1 000 000 | 4 200 000 |
| | Jami | | 7 700 000 | 3 500 000 | |
| | Yakuniy qoldiq 12.11.2026 | | | | **4 200 000** |

Chop etilgan shaklda yakuniy qoldiq ostida "Mijozning qarzi: 4 200 000 so'm" (qoldiq manfiy bo'lsa — "Mijozning avansi"), pastda kompaniya va mijoz nomi bilan imzo joylari bo'ladi.

> **Muhim:** Akt sverkida bekor qilingan hujjatlar ham ko'rinadi: asl yozuv va uning teskari yozuvi ("… bekor qilindi") yonma-yon. Yakuniy qoldiqqa ular birgalikda ta'sir qilmaydi.

> **Eslatma:** Qarzlar sahifasidagi "Qarz", akt sverkidagi "Yakuniy qoldiq" (bugungi sana bilan) va mijozning botdagi "Qarzim" bir xil manbadan hisoblanadi va doim teng bo'lishi kerak. Avans bo'lsa: Qarzlar sahifasida qarz 0 va "+avans", akt sverkida manfiy qoldiq.

## 3.11 Boshlang'ich qarzlarni import qilish

**Kim:** rahbar (Admin)   **Menyu:** Distributsiya → Excel import → "Kirish qarzlari"

Tizimga o'tish kunidagi mijoz qarzlari va avanslari bir marta yuklanadi. Tartib bo'yicha bu **oxirgi** import (avval hududlar, narx turlari, mijozlar, marshrutlar, tovarlar, birliklar, narxlar).

1. **Shablonni yuklab olish** (shablonda "Namuna" varag'i bor).
2. To'ldiring:

| Ustun | Nima kiritiladi | Misol |
|---|---|---|
| Mijoz (STIR yoki nomi) * | Distributsiyadagi mavjud mijoz: avval STIR, keyin nom bo'yicha qidiriladi | DEMO Anvar do'koni |
| Qarz (+) / avans (-) * | Musbat — mijoz qarzi, manfiy — mijozning avansi | 1500000 |
| To'lov muddati | Qarz muddati (kun.oy.yil). Bo'sh — import kuni + mijozning to'lov muddati | 05.11.2026 |

3. Faylni tanlang, **Avval ko'rib chiqish (tavsiya)** belgisini qoldirib **Yuklash** ni bosing. Hali hech narsa yozilmaydi; har qator ko'rsatiladi: mijoz, amal "Yangi", summa (+1,500,000).
4. Xato yo'q bo'lsa — **Import qilish**.

**Natija:** har mijozga "Kirish qoldig'i" yozuvi (import vaqti bilan) tushadi; qarzlar sahifasi, akt sverki va limit tekshiruvi uni darhol hisobga oladi.

> **Muhim:** Har mijozga kirish qoldig'i faqat **bir marta**: ikkinchi marta "Bu mijozga kirish qoldig'i allaqachon kiritilgan." Bitta faylda bir mijoz ikki marta — "Faylda takrorlangan". Bitta qatorda xato bo'lsa ham hech narsa yozilmaydi.

> **Muhim:** Xato yuklangan kirish qoldig'ini tuzatish: **Qarzlar → mijoz akti** (📄) sahifasida **Kirish qoldig'ini bekor qilish** ni bosing va sababni yozing — teskari yozuv qilinadi. Keyin to'g'ri summani shu import orqali qayta kiriting. Shunga qaramay, avval ko'rib chiqish bilan tekshirish tavsiya etiladi.

> **Eslatma:** Nol summali qator o'tkazib yuboriladi. Kirish qoldig'i Moliya moduliga yozilmaydi (3.12).

## 3.12 Moliya moduli bilan bog'lanish

Distributsiya Moliya moduliga (FIN tranzaksiyalar) **faqat haqiqiy pul harakatini** yozadi.

| Distributsiyadagi amal | Moliya'da nima paydo bo'ladi | Hisob |
|---|---|---|
| Ofisda to'lov (hisob tanlangan) | Kirim, "Sotuv daromadi", kontragent — mijoz, maqsad "Distributsiya to'lovi DP-…", izohda to'lov usuli | Tanlangan kassa/hisob |
| Agent naqdsiz to'lovi (karta, Click …) | Xuddi shunday kirim, darhol | "Agent to'lovlari" dagi usul hisobi |
| Agent naqd to'lovi | Hech narsa — toki kassir qabul qilmaguncha | — |
| Kassir topshirishni qabul qildi | Har to'lov uchun alohida kirim + farq bo'lsa "Agent kassa farqi" moddasida kirim (ortiqcha) yoki chiqim (kamomad; agent HR xodimiga bog'langan bo'lsa — kontragent xodim, ish haqi qarzidan ayriladi) | Kassir tanlagan kassa |
| To'lovni bekor qilish | Moliya yozuvi o'chiriladi | — |
| Qabul qilingan topshirishni bekor qilish | To'lovlar kirimlari va farq yozuvi bekor qilinadi | — |
| Reys xarajati "Moliyaga" o'tkazildi (2-bob) | Chiqim, tanlangan chiqim moddasi | Tanlangan kassa |

**Ataylab yozilmaydigan** amallar:

| Amal | Nega |
|---|---|
| Jo'natish (sotuv) | Bu pul emas, qarz. U faqat distributsiya hisob varag'ida. Moliya'ga "ko'zgu" yozuv qo'shilsa kassa qoldiqlari buzilardi |
| Kirish qoldig'i (import) | Pul harakati emas |
| Qaytarish | Faqat mijoz qarzini kamaytiradi; mijozga pul qaytarilmaydi |
| Avansning jo'natish bilan yopilishi | Pul allaqachon to'lov kunida yozilgan |
| Kassalar o'rtasida o'tkazma, inkassatsiya (kassadan bankka) | Moliya modulining o'z ishi — u yerda qo'lda |
| Ombor amallari (hisobdan chiqarish, landed cost va h.k.) | Ombor qiymatiga ta'sir qiladi, pulga emas (4-bob) |

**Misol — noyabr boshidagi Moliya yozuvlari:**

| Sana | Hisob | Turi | Summa | Modda | Kontragent |
|---|---|---|---|---|---|
| 02.11 | Hisob raqam | Kirim | 10 000 000 | Sotuv daromadi | DEMO Bunyodkor qurilish |
| 05.11 | Korporativ karta | Kirim | 1 000 000 | Sotuv daromadi | DEMO Anvar do'koni |
| 05.11 | Naqd pul | Kirim | 2 500 000 | Sotuv daromadi | DEMO Anvar do'koni |
| 05.11 | Naqd pul | Chiqim | 10 000 | Agent kassa farqi | Jasur |

Jami pul: 10 000 000 + 1 000 000 + 2 500 000 − 10 000 = **13 490 000**. Mijozlardan yig'ilgan (Rahbar panelidagi "Yig'ilgan pul"): 13 500 000 — farq 10 000 aynan kamomad.

> **Eslatma:** Moliya modulida distributsiya to'lovlari pul sifatida (kassa, tushum) ko'rinadi, lekin kontragentlar qoldig'i va debitorlik hisobotlarida "avans" bo'lib qatnashmaydi — distributsiya jo'natishlari Moliya'ga yozilmagani uchun. **Distributsiya mijozining qarzi — faqat Distributsiya → Qarzlar sahifasi va akt sverkida.**

> **Eslatma:** "Agent kassa farqi" va "Sotuv daromadi" moddalari topilmasa, tizim ularni o'zi yaratadi. Ularning nomini o'zgartirmang.

## 3.13 Kunlik tekshiruv ro'yxati (kassir va rahbar)

| Qachon | Kim | Nima tekshiriladi | Qayerda |
|---|---|---|---|
| Kechqurun | Kassir | Har agent qo'lida 0 qoldimi; kutayotgan topshirish yo'qmi | To'lovlar → Agentlar naqdi va kassa |
| Kechqurun | Kassir | Kassadagi pul = Moliya'dagi "Naqd pul" qoldig'i | Moliya → Hisoblar |
| Har kuni | Rahbar | Muddati o'tgan qarz va 60+ | Qarzlar (filtr "Muddati o'tgan"), Rahbar paneli |
| Har kuni | Rahbar | Kamomad bilan qabul qilingan topshirishlar | Hisobotlar → Agentlar topshirgan pul |
| Oy oxiri | Kassir | Yirik mijozlar bilan akt sverki (chop, imzo) | Qarzlar → 📄 |

> **Muhim:** Pul yoki qarz farqi topilsa — bu K1 darajali muammo: darhol Xatolar jurnaliga yozing (5.9) va rahbarga qo'ng'iroq qiling. Qarzni "to'g'rilash" uchun soxta to'lov kiritmang — sababini topib, tuzatish hujjati bilan ishlang.
