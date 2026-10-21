# 0. Distributsiya moduli bilan tanishuv

Bu bob tahsinERP'ning **Distributsiya** moduli bilan ishlaydigan barcha xodimlar uchun kirish qismi. Unda modul nima uchun kerakligi, kim qayerda ishlashi (web ERP, Telegram bot yoki Mini App), hujjatlar bir-biridan qanday kelib chiqishi va butun qo'llanma davomida ishlatiladigan asosiy tushunchalar tushuntiriladi.

Bobni hamma o'qiydi. Rahbar va administrator uchun eng muhim qismlar: 0.2, 0.4, 0.5 va 0.7. Agent va ekspeditor uchun: 0.2, 0.3 va 0.5.

Qo'llanmadagi barcha misollar bitta namunaviy kompaniya — **DEMO Qurilish Savdo MChJ** (Toshkent, qurilish mollari ulgurji savdosi) ma'lumotlarida ko'rsatiladi. Ssenariy 0.8-bo'limda.

## 0.1 Modul nima uchun

Distributsiya — ulgurji savdo uchun modul: kompaniya tovarni omborda saqlaydi, savdo agentlari do'konlarni aylanib buyurtma oladi, ofis buyurtmani tasdiqlaydi, ombor yig'ib jo'natadi, ekspeditor yetkazadi, mijoz esa qarzga yoki naqd to'laydi.

Modul quyidagi kundalik ishlarni bitta tizimda yuritadi:

- **Mijozlar bazasi** — minglab mayda savdo nuqtalari (xo'jalik do'konlari, qurilish bozori, brigadalar, prorablar), har birining hududi, agenti, narx turi, qarz limiti va to'lov muddati.
- **Buyurtma** — shartnomasiz tez buyurtma: narx avtomatik, chegirma rol bo'yicha cheklangan, qarz limiti tekshiriladi, tovar omborda rezervga olinadi.
- **Ombor** — haqiqiy, rezervdagi va mavjud qoldiq; yig'ish, jo'natish (FIFO tannarx bilan), qaytarish, inventarizatsiya, ko'chirma, hisobdan chiqarish.
- **Pul** — mijoz to'lovlari (naqd, karta, Click, Payme, Uzum, o'tkazma), agent qo'lidagi naqd va uni kassaga topshirish, mijozlar qarzi muddatlar bo'yicha, akt sverki.
- **Dala ishi** — agent ish kuni, marshrut, tashrif (joylashuv bilan tekshiriladi), foto, yangi savdo nuqtasi.
- **Yetkazish** — mashina, reys, marshrut, ekspeditor natijasi (yetkazildi / qisman / rad), reysni yopish va farq dalolatnomasi.
- **Nazorat** — rahbar paneli, savdo hisobotlari, tashrif reja/fakti, agent KPI.

Modul tahsinERP'ning mavjud qismlari bilan birga ishlaydi: tovar kartochkasi va o'lchov birliklari — umumiy **Mahsulotlar** ma'lumotnomasida, omborlar — **Omborxona** modulida, kassa va bank hisoblari — **Moliya** modulida, xodimlar va ish haqi — **Kadrlar (HR)** modulida. Distributsiya bu ma'lumotlarni qayta kiritmaydi, balki ulardan foydalanadi:

- jo'natish va qaytarish ombor kirim/chiqim hujjatlari sifatida Omborxona moduliga yoziladi (qoldiq bitta);
- kassaga tushgan to'lov Moliya modulida tranzaksiya bo'lib ko'rinadi;
- agent KPI natijasi HR ish haqi qoralamasiga qo'shiladi.

> **Eslatma:** Distributsiya buyurtmasi Sotuvlar (CRM) modulidagi shartnomali savdodan alohida. CRM — yirik korxonalar bilan shartnoma → buyurtma → invoys. Distributsiya — shartnomasiz, ko'p mayda mijozli savdo. Bitta korxona ikkala modulda ishlatilishi mumkin (0.6-bo'limdagi "Mavjud mijozni qo'shish").

## 0.2 Rollar va kim qayerda ishlaydi

Distributsiyada uchta ish joyi bor:

| Ish joyi | Nima | Kim uchun |
|---|---|---|
| **Web ERP** | Brauzerda tahsinERP → chap menyu **Distributsiya** bo'limi | Rahbar, operator, omborchi, kassir, administrator |
| **Telegram bot** | Kompaniyaning o'z Telegram boti: tugmalar, joylashuv, rasm, bildirishnomalar | Agent, ekspeditor, omborchi, rahbar, mijoz |
| **Mini App** | Bot ichida ochiladigan sahifa: katalog, savat, imzo, skaner | Agent (buyurtma), ekspeditor (yetkazish, imzo), omborchi (sanoq, yig'ish) |

Alohida mobil ilova yo'q — dala xodimlari faqat Telegram orqali ishlaydi. Botga ulanish uchun xodimga bir martalik kod beriladi (1-bob, 1.8; 6-bob, 6.3).

### Rollar jadvali

| Rol | Asosiy vazifa | Web ERP | Bot / Mini App |
|---|---|---|---|
| **Rahbar** (direktor, savdo boshlig'i) | Sozlamalar, narx siyosati, limitdan oshgan buyurtmaga ruxsat, nazorat | Rahbar paneli, Hisobotlar, Qarzlar, Sozlamalar, Narxlar, Aksiyalar, Hisobdan chiqarishni tasdiqlash | Bildirishnomalar (yirik buyurtma, limit oshishi, kassaga topshirish, yangi nuqta, muammo), **✅ Tasdiqlash** tugmasi, **📊 Bugungi yakun**, har kuni 20:00 da kunlik yakun |
| **Supervayzer** | O'z jamoasi agentlarini boshqaradi | Faqat o'z jamoasi agentlari va ularning mijozlari, buyurtmalari ko'rinadi | Rahbar sifatida yoki agent sifatida ulanadi |
| **Operator** (ofis) | Buyurtmani tasdiqlash, bekor qilish, qaytarish kiritish, mijoz kartasi | Buyurtmalar, Mijozlar, Qaytarishlar, Import | — |
| **Agent** | Do'konlarni aylanadi, buyurtma va to'lov oladi | Odatda kerak emas (faqat o'z mijozlarini ko'radi) | Kun boshi/yakuni, marshrut, **📍 Keldim**, **🛒 Buyurtma** (Mini App), **💵 To'lov**, **🚫 Buyurtmasiz**, **📷 Foto**, **➕ Yangi nuqta**, **💵 Kassa**, ovozli buyurtma |
| **Ekspeditor** (haydovchi-yetkazuvchi) | Reys bo'yicha yetkazadi, to'lov oladi | Kerak emas | **🚚 Reys**, manzil kartasi, **✅ Yetkazildi (rasm)**, **✍️ Imzo / qisman**, **🚫 Rad etildi**, **💵 To'lov**, **⛽ Xarajat**, **🏁 Reysni yopish** |
| **Omborchi** | Yig'ish, jo'natish, sanoq, ko'chirma qabul qilish | Buyurtma kartasi (yig'ish, jo'natish, chop etish), Qoldiqlar, Inventarizatsiya, Ko'chirmalarni qabul qilish, Hisobdan chiqarish | Yangi tasdiqlangan buyurtma xabari, **📦 Ombor** — Mini App'da skaner bilan sanoq va yig'ish |
| **Kassir** | Ofisdagi to'lov, agent naqdini qabul qilish | To'lovlar, **Agentlar naqdi va kassa** | — |
| **Mijoz** (do'kon egasi) | Buyurtma holatini kuzatadi | — | Bildirishnomalar (jo'natildi, yo'lga chiqdi, yetkazildi + nakladnoy PDF, to'lov qabul qilindi), **💳 Qarzim**, **📄 Akt sverki** |

> **Muhim:** Botdagi rol (Rahbar, Omborchi, Agent, Ekspeditor) ulanish kodi olinayotganda tanlanadi va faqat botda qaysi menyu chiqishini belgilaydi. Web'da nimani ko'rish va o'zgartirish mumkinligi esa ERP **roli va huquqlari** bilan belgilanadi (1-bob, 1.10). Ikkalasini mos qilib sozlang: masalan limitdan oshishga botdagi **✅ Tasdiqlash** tugmasi faqat ERP'da "Limitdan oshishga ruxsat" huquqi bor rahbarda ishlaydi.

Operator va kassir uchun botga ulanish shart emas — ular faqat web'da ishlaydi.

## 0.3 Hujjatlar oqimi

Distributsiyadagi har bir hujjat oldingisidan kelib chiqadi. Bitta ma'lumot ikki marta qo'lda yozilmaydi.

```
 MA'LUMOTNOMALAR (1-bob)
 Hududlar, tovarlar va birliklar, narx turlari va narxlar,
 mijozlar (limit, muddat, agent), agentlar va jamoalar
        |
        v
 BUYURTMA (2-bob)                                   AKSIYALAR
 Agent (bot / Mini App / ovoz) yoki operator (web)  <-- chegirma, N+M sovg'a
   holat: Yangi                                        (saqlashda avtomatik)
        |
        |  Tasdiqlash: mijoz tasdiqlangan? minimal summa?
        |  qarz + buyurtma <= limit, muddati o'tgan qarz yo'q?
        |  (yo'q bo'lsa -> rahbar ruxsati)
        v
   holat: Tasdiqlangan ---- ombor tanlanadi, tovar REZERVga olinadi
        |                   (yetmagani "kirimni kutmoqda")
        |                   omborchiga Telegram xabari
        v
   holat: Yig'ilmoqda ----- Yig'ish varaqasi (chop)
        |
        v
 JO'NATISH (DJ-...) ------> OMBOR: chiqim, FIFO tannarx, rezerv yechiladi
   holat: Jo'natildi /      QARZ: mijoz hisobiga debet,
          Qisman jo'natildi       muddat = jo'natish sanasi + to'lov muddati
        |                   Nakladnoy (chop / PDF)
        |
        +--> REYS (mashina, ekspeditor) --> Yetkazildi / Qisman / Rad
        |                                   Reysni yopish: qaytgani omborga,
        |                                   kamomad dalolatnomasi
        v
   holat: Yetkazildi
        |
        |   TO'LOV (naqd agentda -> kassaga topshirish -> kassir qabul qiladi;
        |           karta/Click/... -> darhol kassa/bank hisobiga, Moliya)
        v
   holat: Yopildi  (buyurtma bo'yicha qarz to'liq yopilganda, avtomatik)

 Har qanday payt:  QAYTARISH (DQ-...) -> omborga kirim (yaroqli / brak),
                                         mijoz qarzi kamayadi
                   BEKOR QILISH        -> faqat jo'natilmagan buyurtma, sabab bilan
```

Ombor tomonidagi qo'shimcha hujjatlar (inventarizatsiya, ko'chirma qabul qilish, hisobdan chiqarish, pereso'rtitsa, qo'shimcha xarajatlar, buyurtma taklifi) shu qoldiq bilan ishlaydi va 4-bobda yoritilgan.

### Hujjat raqamlari

| Hujjat | Raqam ko'rinishi | Misol |
|---|---|---|
| Buyurtma | D-YYYY-NNNNN | D-2026-00001 |
| Jo'natish | DJ-YYYY-NNNNN | DJ-2026-00001 |
| Qaytarish | DQ-YYYY-NNNNN | DQ-2026-00001 |

Raqam har yil 1 dan boshlanadi va avtomatik beriladi; qo'lda kiritilmaydi.

## 0.4 Asosiy tushunchalar

### Birliklar

- **Asosiy birlik** — tovar omborda va narxda hisoblanadigan birlik. Tovar kartochkasida bitta bo'ladi. Misol: sement — **qop**, kafel — **m²**, gipsokarton — **list**. Barcha qoldiqlar, minimal qoldiq, narx varag'i va aksiya shartlari asosiy birlikda.
- **Sotish birligi** (qo'shimcha birlik) — agent va mijoz uchun qulay birlik: quti, poddon, blok. Har biriga **koeffitsient** beriladi — bitta sotish birligida nechta asosiy birlik bor.
- **Koeffitsient** — "1 poddon = 40 qop" bo'lsa koeffitsient 40; "1 quti = 1,44 m²" bo'lsa 1,44.

Buyurtmada agent "2 poddon" yozadi, tizim uni 80 qop deb hisoblaydi va omborda 80 qop rezervga oladi. Narx ham avtomatik qayta hisoblanadi: qop narxi × 40.

| Tovar | Asosiy birlik | Sotish birligi | Koeffitsient | Chakana narx (asosiy) | Sotish birligi narxi |
|---|---|---|---|---|---|
| DEMO-SEM-400 Sement M400 | qop | poddon | 40 | 62 000 so'm/qop | 2 480 000 so'm/poddon |
| DEMO-KAF-3030 Kafel 30×30 | m² | quti | 1,44 | 95 000 so'm/m² | 136 800 so'm/quti |
| DEMO-GIP-125 Gipsokarton 12,5 mm | list | — | — | 68 000 so'm/list | — |

### Narx

- **Narx turi** — narxlar ro'yxatining nomi: Chakana, Ulgurji, Diler va hokazo. Har mijozga bitta narx turi biriktiriladi.
- **Standart narx turi** — bittagina; mijozga narx turi berilmagan bo'lsa yoki uning narx turida tovarning narxi yo'q bo'lsa, shu turdagi narx olinadi.
- **Narx varag'i** — tovar × narx turi jadvali (asosiy birlik uchun, so'mda). Har o'zgarish sanasi va muallifi bilan tarixga yoziladi.
- **Maxsus narx** — bitta mijozga bitta tovar uchun alohida narx, boshlanish va (ixtiyoriy) tugash sanasi bilan. U narx turidan ustun turadi.
- **Minimal narx** — narx varag'ida har narx turi qatoriga yoziladi. Chegirma berilganda ham tovar shu narxdan arzon sotilmaydi. Maxsus narxga minimal narx qo'llanmaydi.

Buyurtmada narx **har doim avtomatik** aniqlanadi, qo'lda yozilmaydi. Tartib:

1. Mijozning amaldagi maxsus narxi (bugungi sana oralig'ida).
2. Mijoz narx turidagi narx.
3. Standart narx turidagi narx.
4. Hech biri yo'q bo'lsa — tovar buyurtmaga qo'shilmaydi ("narx belgilanmagan").

Misol: DEMO Bunyodkor qurilish — narx turi **Diler**. Sement uchun Diler narxi bor (55 000 so'm/qop), kafel uchun yo'q. Standart narx turi Chakana bo'lsa, Bunyodkorga kafel Chakana narxida (95 000 so'm/m²) chiqadi.

### Chegirma

- **Qator chegirmasi** — bitta tovar qatoriga %.
- **Buyurtma chegirmasi** — butun buyurtmaga %, qator chegirmasidan keyin qo'llanadi.
- **Chegirma limiti** — har ERP roliga maksimal % (Sozlamalar → Chegirma limitlari). Qator va buyurtma chegirmasi birgalikda shu limitdan oshmasligi kerak. Rolga limit yozilmagan bo'lsa — u umuman chegirma bera olmaydi. "Cheksiz chegirma" huquqi bor foydalanuvchi limit va minimal narxdan ozod.
- **Aksiya chegirmasi** — rahbar Aksiyalar sahifasida belgilaydi, buyurtma saqlanganda avtomatik qo'llanadi va rol limiti hamda minimal narx tekshiruvidan ozod.

### Qarz

- **Mijoz qarzi (balans)** = kirish qarzi + jo'natishlar − to'lovlar − qaytarishlar. Real vaqtda hisoblanadi.
- **Qarz (kredit) limiti** — mijozning qarzi + yangi buyurtma summasi shu limitdan oshsa, buyurtma tasdiqlanmaydi. **Limit 0** — mijoz faqat oldindan to'lov bilan oladi (avansi bo'lmasa har qanday buyurtma bloklanadi).
- **To'lov muddati (kechiktirish)** — kunlarda. Har jo'natish qarzining muddati = jo'natish sanasi + shu kunlar. Muddati o'tgan qarz bo'lsa, yangi buyurtma limitdan qat'i nazar tasdiqlanmaydi.
- **Rahbar ruxsati** — limitdan oshgan yoki muddati o'tgan qarzli mijoz buyurtmasini "Limitdan oshishga ruxsat" huquqi bor xodim izoh yozib tasdiqlaydi (web'da yoki botdagi tugma bilan).
- To'lov eng eski qarzni birinchi yopadi (FIFO). Qarz muddatlar bo'yicha guruhlanadi: **muddati kelmagan, 1–7, 8–30, 31–60 va 60 kundan ortiq**, alohida — **avans**.

### Ombor qoldig'i

- **Haqiqiy qoldiq** — omborda jismonan turgan tovar (ombor hisobidan).
- **Rezerv** — tasdiqlangan, lekin hali jo'natilmagan buyurtmalarga band qilingan miqdor.
- **Mavjud = haqiqiy − rezerv.** Yangi buyurtma faqat mavjud qoldiqdan rezerv oladi. Ikki operator bir vaqtda oxirgi qoldiqni ikki marta rezerv qila olmaydi.
- **"Kirimni kutmoqda"** — tasdiqlashda mavjud qoldiq yetmagan qism. U rezervsiz qoladi; kirimdan keyin **Rezervni yangilash** bilan band qilinadi.
- **Savdo ombori** — Sozlamalarda "Savdoda" belgisi qo'yilgan ombor. Buyurtmalar faqat shu omborlardan jo'natiladi.
- **Minimal qoldiq** — ombor × tovar bo'yicha chegara; mavjud qoldiq undan pastga tushganda ogohlantirish beriladi va tovar "Buyurtma taklifi" ro'yxatiga tushadi.

### Tannarx (FIFO)

Ombordan chiqim har doim eng avval kelgan partiyadan olinadi (FIFO). Jo'natish tannarxi shu partiya kirim narxidan (QQSsiz) hisoblanadi va jo'natish hujjatida saqlanadi. Yalpi foyda = jo'natish summasi − FIFO tannarx. Tannarx, foyda va marja faqat "Tannarxni ko'rish" huquqi bor xodimlarga ko'rinadi — boshqalar uchun bu ustunlar ekranda ham, Excel'da ham yo'q.

### Hujjat holatlari

Distributsiyaning barcha hujjatlarida (buyurtma, jo'natish, qaytarish, to'lov, kassaga topshirish, inventarizatsiya, hisobdan chiqarish va boshqalar) bir xil uch holat bor:

| Holat | Ma'nosi | Nima qilish mumkin |
|---|---|---|
| **Qoralama** | Hali omborga va qarzga ta'sir qilmagan | Tahrirlash, o'chirish |
| **O'tkazilgan** | Ombor, qarz yoki pulga yozilgan | O'chirib bo'lmaydi — faqat sabab ko'rsatib bekor qilinadi |
| **Bekor qilingan** | Ta'siri teskari yozuv bilan qaytarilgan, hujjat o'chmaydi | Tahrirlab bo'lmaydi; kim, qachon va qaysi sabab bilan bekor qilgani saqlanadi |

O'tkazilgan hujjatni o'chirishga urinilsa tizim "O'tkazilgan hujjatni o'chirib bo'lmaydi — sababini ko'rsatib bekor qiling." deydi. Bekor qilish sababi kamida 3 belgi.

Buyurtmaning bulardan tashqari o'z **ish holatlari** bor (Yangi, Tasdiqlangan, Yig'ilmoqda, Qisman jo'natildi, Jo'natildi, Yetkazildi, Yopildi, Bekor qilingan) — 2-bob, 2.4.

### Ko'rinish doirasi (scope)

Har bir xodim faqat o'ziga tegishli yozuvlarni ko'radi:

- **Agent** — faqat o'ziga biriktirilgan mijozlar va ularning buyurtmalari.
- **Supervayzer** — o'z jamoasidagi agentlar va o'zi.
- **Ofis xodimlari** (agent ham, supervayzer ham emas) — cheklovsiz.
- Bundan tashqari har foydalanuvchiga **omborlar, hududlar va mijozlar** bo'yicha ro'yxat berish mumkin (Agentlar va jamoalar → Ko'rinish doirasi). Ro'yxat bo'sh bo'lsa — cheklov yo'q; to'ldirilsa — faqat tanlanganlar ko'rinadi.
- **Admin** har doim hammasini ko'radi.

Qoida web, bot va Mini App'da bir xil ishlaydi. Masalan, omborchiga ombor doirasi "DEMO Asosiy ombor" qilib berilsa, u faqat shu ombor buyurtmalari haqida xabar oladi va faqat shu omborni ko'radi.

### Jurnal (audit)

Distributsiya ma'lumotlaridagi har o'zgarish (yaratish, o'zgartirish, o'chirish) kim, qachon, qaysi maydon, eski va yangi qiymat bilan jurnalga yoziladi. Foydalanuvchi ko'radigan qismi:

- buyurtma kartasidagi **Tarix** — har holat o'zgarishi, kim va izoh (masalan, bekor qilish sababi, limit ruxsati izohi);
- narxlar sahifasidagi **Narx tarixi**;
- bekor qilingan hujjatlarda — kim, qachon, qaysi sabab bilan.

To'liq jurnal bazada saqlanadi; uni ko'rish uchun alohida sahifa hozircha yo'q — kerak bo'lsa administratorga murojaat qiling.

## 0.5 Litsenziya va paket

Distributsiya moduli quyidagi paketlarga kiradi:

| Paket | Tarkibi | Distributsiya |
|---|---|---|
| Start | Ta'minot, Sotuvlar (CRM), Topshiriqlar | Yo'q |
| Biznes | Start + Ishlab chiqarish, Muhandislik, Moliya | Yo'q |
| **Distributsiya (DIST)** | Ta'minot, Sotuvlar (CRM), Topshiriqlar, Moliya, **Distributsiya** | **Bor** |
| **Korporativ (KORP)** | Biznes + Kadrlar, Logistika, Rasmiy hujjatlar, **Distributsiya** | **Bor** |
| **Sinov (TRIAL)** | Korporativ bilan bir xil, 14 kun | **Bor** |

Shuni bilish kerak:

- **DIST** paketida Kadrlar (HR) yo'q — agent KPI natijasini ish haqiga o'tkazish (HR ish haqi qoralamasi) bu paketda ishlamaydi.
- DIST'dan Biznes paketiga o'tilsa Distributsiya yopiladi (Biznes'da u yo'q). To'g'ri yo'l — Korporativ.
- Litsenziya faol bo'lmasa, Distributsiya sahifalari ochilmaydi va bot ham ishlamaydi — bot "⛔ Tizim litsenziyasi faol emas" deydi. Bu foydalanuvchi xatosi emas, administratorga ayting.
- Sayt-katalog (ochiq veb-sayt) — paketdan tashqari alohida xizmat.

## 0.6 Menyu xaritasi

**Menyu:** chap menyu → **Distributsiya** sarlavhasi → **Distributsiya** bandi. Ichida faqat sizga ko'rish huquqi berilgan sahifalar chiqadi. Bandlar menyudagi tartibda:

| Menyu bandi | Nima uchun | Bob |
|---|---|---|
| Bosh sahifa | Modul sahifalari plitkalar ko'rinishida, til tanlash (O'zbekcha / Русский) | 0.7 |
| Rahbar paneli | Kunlik holat: tushum, foyda, qarz, pul, qoldiq qiymati, agentlar | 5.2 |
| Hisobotlar | Savdo kesimlari (agent, mijoz, tovar, hudud, kategoriya), tashriflar, agentlar topshirgan pul | 5.3–5.5 |
| Buyurtmalar | Buyurtma, tasdiqlash, rezerv, yig'ish, jo'natish, chop etish | 2 |
| Qoldiqlar | Haqiqiy / rezerv / mavjud qoldiq, minimaldan past tovarlar | 4.2 |
| Ko'chirmalarni qabul qilish | Boshqa ombordan kelgan tovarni haqiqiy miqdor bilan qabul qilish, farq dalolatnomasi | 4.4 |
| Hisobdan chiqarish | Brak, sinish, kamomad — rahbar tasdig'i bilan | 4.5 |
| Pereso'rtitsa | Bir tovar o'rniga boshqasi (tannarx saqlanadi) | 4.6 |
| Qo'shimcha xarajatlar (landed cost) | Yo'l haqi va bojxonani kirim tannarxiga taqsimlash | 4.7 |
| Buyurtma taklifi | Minimal qoldiqdan past tovarlar → yetkazib beruvchiga xarid buyurtmasi | 4.8 |
| Aksiyalar | 10+1, hajmga qarab va muddatli chegirmalar | 2.17 |
| To'lovlar | Mijoz to'lovlari, **Agentlar naqdi va kassa** | 3.3–3.5 |
| Qarzlar | Mijozlar qarzi muddatlar bo'yicha, akt sverki | 3.7, 3.10 |
| Qaytarishlar | Mijozdan yaroqli / brak qaytarish | 2.16 |
| Dala nazorati | Agentlar ish kuni, selfi, tashriflar, jonli joylashuv | 5.6 |
| Tashrif rejasi | Agentning kunlik marshruti va reja/fakt | 7.10 |
| Reyslar | Reys rejalash, yetkazish natijasi, reysni yopish, farq dalolatnomasi | 7.5–7.9 |
| Mashinalar | Mashinalar, sig'im; yetkazish sozlamalari (xarita kaliti, ovozli buyurtma, ombor koordinatasi) | 7.3, 7.4 |
| Agent KPI | % savdo, bonus, jarima → ish haqi | 5.7 |
| Mijozlar | Savdo nuqtalari, limit, agent, tasdiqlash, Telegram'ga ulash | 1.9 |
| Tovarlar | Tovarning distributsiya kartasi, birliklar, shtrix-kodlar | 1.5 |
| Narxlar | Narx turlari bo'yicha narx varag'i, tarix, ommaviy o'zgartirish | 1.6 |
| Hududlar | Viloyat → tuman → hudud | 1.3 |
| Agentlar va jamoalar | Jamoalar, supervayzer, agentlar, ekspeditorlar, ko'rinish doirasi, botga ulash | 1.7, 1.8 |
| Ma'lumotnomalar | Brendlar, ishlab chiqaruvchilar, subkategoriyalar, narx turlari, sabablar | 1.4 |
| Sozlamalar | Umumiy, agent to'lovlari, chegirma limitlari, omborlar, minimal qoldiq | 1.2 |
| Excel import | Boshlang'ich ma'lumotni Excel'dan yuklash | 1.11 |

Ikki sahifa chap menyuda yo'q — ular faqat **Bosh sahifa** plitkalari orqali ochiladi:

| Plitka | Nima uchun |
|---|---|
| Inventarizatsiya | Boshlang'ich (qoldiqni tenglash) va nazorat sanog'i (4.3) |
| Xatolar jurnali | Xato va muammolarni yozish va kuzatish, K1–K3 (5.9) |

> **Eslatma:** Menyuda kerakli band ko'rinmasa — bu xato emas. Sizning rolingizda shu sahifani ko'rish huquqi yo'q. Administrator 1.10-bo'limga ko'ra huquq qo'shadi.

## 0.7 Interfeys tili (o'zbek / rus)

Distributsiya sahifalari va bot ikki tilda ishlaydi: **o'zbekcha (lotin)** va **ruscha**.

**Kim:** har bir foydalanuvchi o'zi uchun   **Menyu:** Distributsiya → Bosh sahifa (yuqori o'ng burchak)

1. **Distributsiya → Bosh sahifa** ni oching.
2. Yuqori o'ngdagi **O'zbekcha** yoki **Русский** tugmasini bosing.
3. Sahifa qayta ochiladi va barcha Distributsiya sahifalari tanlangan tilda chiqadi.

Shuni bilish kerak:

- Til har foydalanuvchiga alohida saqlanadi va boshqa xodimlarga ta'sir qilmaydi. Hech narsa tanlanmagan bo'lsa — o'zbekcha.
- Faqat matnlar almashadi. Sana va son ko'rinishi (masalan `15 376 400`) o'zgarmaydi.
- Bot va bildirishnomalar ham xodimning shu tilida keladi (Telegram ilovasining tili emas). Agent ruscha ishlashi uchun uning ERP foydalanuvchisi nomidan bir marta Русский tanlanadi.
- Ma'lumotnomalarda ikkita nom bor: **Nomi** va **Nomi (rus)** (hudud, tovar, narx turi, sabab, brend). Ruscha interfeysda rus nomi to'ldirilgan bo'lsa u, bo'lmasa o'zbekcha nom ko'rsatiladi. Excel shablonlari ham interfeys tilida yuklanadi.
- Tizimning boshqa modullari (Omborxona, Moliya va hokazo) hozircha o'zbekcha; til tugmasi faqat Distributsiyaga ta'sir qiladi.
- Mijozga boradigan bildirishnomalar hozircha o'zbek tilida.

> **Eslatma:** Tilni almashtirish uchun alohida huquq kerak emas — Distributsiya bosh sahifasini ko'ra oladigan har kim o'z tilini tanlaydi.

## 0.8 Namunaviy ssenariy

Qo'llanmaning barcha boblarida bitta kompaniya ishlatiladi.

**Kompaniya:** DEMO Qurilish Savdo MChJ — qurilish mollari ulgurji savdosi, Toshkent. Sanalar — 2026-yil noyabr.

| Turi | Nomi | Qiymatlar |
|---|---|---|
| Ombor | DEMO Asosiy ombor (Toshkent) | savdo ombori, mas'ul — demo_ombor |
| Ombor | DEMO Filial ombor (Samarqand) | savdo ombori |
| Hudud | Toshkent sh. → Chilonzor, Yunusobod | ierarxiya |
| Narx turlari | Chakana (standart), Ulgurji, Diler | |
| Tovar | DEMO-SEM-400 Sement M400 | asosiy birlik qop (50 kg); poddon = 40 qop; Chakana 62 000, Ulgurji 58 000, Diler 55 000, minimal 52 000 so'm/qop |
| Tovar | DEMO-KAF-3030 Kafel 30×30 | asosiy birlik m²; quti = 1,44 m²; Chakana 95 000, Ulgurji 88 000 so'm/m² |
| Tovar | DEMO-GIP-125 Gipsokarton 12,5 mm | asosiy birlik list; Chakana 68 000 so'm |
| Mijoz | DEMO Anvar do'koni | Chilonzor, Chakana, limit 20 000 000, to'lov muddati 7 kun, agent — Jasur |
| Mijoz | DEMO Bunyodkor qurilish | Yunusobod, Diler, limit 50 000 000, to'lov muddati 14 kun |
| Foydalanuvchilar | demo_rahbar (Rahbar), demo_agent (Jasur, agent), demo_eksp (Sherzod, ekspeditor), demo_ombor (omborchi), demo_kassir (kassir) | |
| Mashina | 01 A 123 AA, Isuzu, 5 000 kg, 20 m³ | haydovchi-ekspeditor Sherzod |
| Aksiya | Sement 10+1 | har 10 qopga 1 qop sovg'a |
| Aksiya | Kafel ≥ 100 m² | 5% chegirma |
| Kirim | 1 000 qop sement × 40 000, 500 m² kafel × 70 000, 200 list gipsokarton × 50 000 so'm | QQSsiz tannarx, DEMO Asosiy ombor |

**Kun ssenariysi qisqacha** (batafsil — keyingi boblarda):

1. 09:30 — agent Jasur DEMO Anvar do'konida **📍 Keldim**, Mini App'da buyurtma: 2 poddon sement, 70 quti kafel, 20 list gipsokarton. Tizim aksiyalarni o'zi qo'llaydi: 8 qop sement sovg'a, kafelga 5% chegirma. Jami **15 376 400 so'm** (2.2-bo'limdagi hisob).
2. 10:00 — operator buyurtmani tasdiqlaydi: limit yetadi, tovar DEMO Asosiy omborda rezervga olinadi; demo_ombor Telegram'da "📦 Yig'ish kerak" xabarini oladi.
3. 14:00 — omborchi yig'ish varaqasi bo'yicha yig'adi va jo'natadi: omborda 88 qop sement, 100,8 m² kafel va 20 list gipsokarton chiqadi, Anvar do'koni qarzi 15 376 400 so'm bo'ladi, to'lov muddati — 7 kundan keyin.
4. 15:00 — Sherzod reys bilan yetkazadi, mijoz imzo qo'yadi, Telegram'da nakladnoy PDF keladi.
5. Kechqurun — Jasur olgan naqdni kassaga topshiradi, demo_kassir sanab qabul qiladi.

## 0.9 Boblar ro'yxati

| Bob | Mavzu |
|---|---|
| 0 | Kirish (shu bob) |
| 1 | Boshlang'ich sozlash: sozlamalar, hududlar, tovarlar, narxlar, jamoalar, mijozlar, import, huquqlar |
| 2 | Buyurtmalar: yaratish, tasdiqlash, rezerv, yig'ish, jo'natish, yetkazish, bekor qilish, qaytarish, aksiyalar |
| 3 | Pul va qarz: to'lovlar, agent naqdi va kassa, qarzlar, akt sverki |
| 4 | Ombor: qoldiqlar, inventarizatsiya, ko'chirma, hisobdan chiqarish, pereso'rtitsa, landed cost, buyurtma taklifi |
| 5 | Hisobotlar va nazorat: rahbar paneli, savdo, tashriflar, dala nazorati, agent KPI, xatolar jurnali |
| 6 | Telegram bot va Mini App: agent, ekspeditor, omborchi, rahbar, mijoz |
| 7 | Yetkazish: mashinalar, reyslar, reysni yopish, tashrif rejasi, ovozli buyurtma |
