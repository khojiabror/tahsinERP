# Distributsiya UAT — kirish va tayyorgarlik

Bu hujjat tahsinERP'ning **Distributsiya** modulini (web ERP + Telegram bot + Mini App) qo'lda, haqiqiy foydalanuvchi ko'zi bilan tekshirish uchun tuzilgan. Har bir test bitta biznes amalini demo ma'lumot bilan bajaradi va natijani raqam bilan tekshiradi: buyurtma tasdiqlangach rezerv qo'yildimi, jo'natishdan keyin qoldiq kamaydimi, to'lovdan keyin mijoz qarzi qancha qoldi, FIFO tannarx to'g'ri hisoblandimi.

Testlar zanjir bo'lib bog'langan: sozlangan ma'lumotnomalar buyurtmaga, buyurtma jo'natishga, jo'natish qarz va reysga, reys esa qaytarish va hisobotlarga o'tadi. Kutilgan raqamlar oldingi testlar aynan yozilgandek bajarilgan deb hisoblangan. Shuning uchun testlarni **pastdagi "Bajarish tartibi" jadvalidagi tartibda** bajaring va qiymatlarni o'zgartirmang.

Bog'liq hujjatlar: `../01_PHASE1_MVP.md` (10–17-bo'limlar), `../03_PILOT.md` (pilot rejasi va qabul ro'yxati), `../04_PHASE3_WAREHOUSE.md`, qo'llanma `../Qollanma_Distributsiya_uz.md`, demo-ssenariy `../Qollanma/_DEMO.md`.

## Qanday ishlatiladi

- Har bir testdagi qadamlarni ketma-ket bajaring va "Kutilgan natija" bilan solishtiring.
- Natija mos kelsa "☐ O'tdi", mos kelmasa "☐ O'tmadi" ni belgilang. Izohga nima ko'rganingizni yozing: raqam, xato matni, sahifa yoki bot xabari.
- Xato topilsa, skrinshot oling (bot xabari bo'lsa — Telegram skrinshoti) va test kodini yozing (masalan `T-DST-ORD-13`). Xatoni **Distributsiya → Bosh sahifa → Xatolar jurnali** ga yoki botdagi **⚠️ Muammo** tugmasi bilan kiriting va darajasini qo'ying (pastdagi K1/K2/K3 shkalasi).
- "Oldin bajarilgan bo'lishi kerak" qatoridagi test hali o'tmagan bo'lsa, avval o'shani bajaring.
- Kutilgan natijadagi raqam mos kelmasa, keyingi testga o'tishdan oldin sababini aniqlang: keyingi raqamlar shu testdan keyingi holatga tayanadi.
- Hujjat raqamlari tizim tomonidan beriladi: buyurtma `D-2026-NNNNN`, jo'natish `DJ-…`, to'lov `DP-…`, kassaga topshirish `DK-…`, qaytarish `DQ-…`, hisobdan chiqarish `DW-…`, pereso'rtitsa `DR-…`, ko'chirma dalolatnomasi `DT-…`, inventarizatsiya `DI-…`, reys `R-2026-NNNNN`, xato `P-001`. Matnda "NNNNN" deb yozilgan joyga keyingi bo'sh tartib raqami chiqadi — uni Izohga yozib qo'ying, keyingi testlarda kerak bo'ladi.
- Test bazasi ishchi bazaning nusxasi bo'lsa, unda boshqa mijoz, tovar va omborlar ham bor. Ro'yxat va hisobotlarda `DEMO` bo'yicha qidiring yoki filtrlang; jami raqamlar faqat DEMO qatorlari uchun berilgan. Inventarizatsiya varag'iga bazadagi barcha sotuvdagi distributsiya tovarlari tushadi — DEMO bo'lmaganlariga "Tizimda" ustunidagi qiymatni yozing (farq bermasin).
- Sanalar: "bugun" — testni bajarayotgan kun. Muddatli hisoblar (qarz muddati, aging) bugungi sanaga nisbatan yozilgan. Agar UAT bir necha kunga cho'zilsa, muddatlar shunga mos siljiydi — buni Izohda ko'rsating.

## Muhit (bir marta)

**Ishlab chiqarish (production) bazasida test qilinmaydi.** Testlar ombor qoldig'i, mijoz qarzi va kassa yozuvlarini haqiqatan o'zgartiradi.

| # | Talab | Izoh |
|---|---|---|
| 1 | **Test serveri** yoki lokal SQL Server'da ishchi bazaning **nusxasi** | Migratsiyalar 013–026 (DST) qo'llangan bo'lsin (`Database/Migrations/0xx_DST_*.sql`), ruxsat modullari `Seed/105` da |
| 2 | **Amaldagi kompaniya litsenziyasi** (Edition KORP yoki TRIAL — Distribution moduli ichida) | Litsenziyasiz web sahifalar License sahifasiga yo'naltiradi, bot API 403 qaytaradi va bot "⛔ Tizim litsenziyasi faol emas" deydi |
| 3 | ERP **HTTPS** orqali ochiladi (masalan `https://erp-test.<domen>`) | Bot ERP'ga shu manzil orqali murojaat qiladi |
| 4 | `tahsinERP.Bot` servisi alohida **HTTPS domenda** (masalan `https://bot-test.<domen>`), `Bot__PublicBaseUrl` shu manzil | Telegram webhook va Mini App faqat HTTPS bilan ishlaydi |
| 5 | Muhit o'zgaruvchisi: ERP'da `TAHSINERP_BOT_KEY` = bot'dagi `Bot__MasterKey` | Bo'lmasa ERP API o'chiq (503) |
| 6 | **Telegram test boti** (BotFather → `/newbot`), so'ng BotFather → `/setdomain` → bot domeni (Mini App uchun) | Ishlab chiqarishdagi botni ishlatmang |
| 7 | Bot serverida: `tenant add <slug> <ERP url> <token>` va `webhook set <slug>` (qarang `tahsinERP.Bot/Cli.cs`, `deploy/README`) | `tenant add` chiqargan bot nomini ERP sozlamasiga yozasiz (T-DST-SET-06) |
| 8 | (Ixtiyoriy) Ovozli buyurtma uchun bot muhitida `Bot__Voice__GroqApiKey` | Bo'lmasa T-DST-BOT-23 faqat "sozlanmagan" javobi bilan tekshiriladi |
| 9 | (Ixtiyoriy) Yandex Maps JavaScript API kaliti | Bo'lmasa xarita o'rniga Yandex havolalari ko'rsatiladi — bu ham tekshiriladi |
| 10 | **5 ta Telegram hisobi** (telefon): rahbar, agent, ekspeditor, omborchi, mijoz (Anvar) | Kassir botdan foydalanmaydi. Mini App va kamerani **mobil Telegram ilovasida** (Android va iOS) sinang — Telegram Web/Desktop kamera bermasligi mumkin |
| 11 | Brauzer: Chrome yoki Edge; ikkinchi brauzer yoki "Inkognito" oynasi | Bir vaqtda ikki foydalanuvchi va parallel tasdiq testlari uchun |

## Tayyorgarlik (bir marta)

1. **Zaxira nusxa.** SSMS'da test bazasining zaxira nusxasini oling (Tasks → Back Up...), masalan `<baza>_before_DST_UAT.bak`. UAT oxirida bazani shu holatga qaytarish mumkin bo'ladi.
2. **Oxirgi kod.** Ishchi branch (`master`) ning oxirgi versiyasi ERP'ga va bot serveriga o'rnatilgan, ERP va bot qayta ishga tushirilgan bo'lsin. Bot sog'ligini tekshiring: test botiga `/start` yuboring — "Assalomu alaykum! Botdan foydalanish uchun …" javobi kelishi kerak.
3. **Asosiy ma'lumotnomalar (ERP).** Quyidagilar ERP'da mavjud bo'lsin (yo'q bo'lsa — Admin yaratadi):
   - O'lchov birliklari (`/Unit`): `qop`, `poddon`, `m²` (bazada `m2` deb yozilgan bo'lsa — hamma joyda shu yozuvni ishlating), `quti`, `list`.
   - Mahsulot kategoriyalari: `DEMO Qurilish aralashmalari`, `DEMO Kafel`.
   - Kamida bitta sex (SHOPS) — tovar importi uchun.
   - Moliya hisoblari (**Moliya → Boshqaruv → Hisoblar**): `DEMO Kassa` (naqd, UZS, boshlang'ich qoldiq 0) va `DEMO Bank hisobi` (bank, UZS, qoldiq 0).
4. **Rollar** (**Admin panel → Rollar → Yangi rol**, keyin **Huquqlar**: sahifa hajmi "Barchasi", avval "Barchasini bekor qilish", so'ng quyidagilarni belgilash):

| Rol | View + Change | Faqat View | Maxsus (View) |
|---|---|---|---|
| `DEMO-Rahbar` | barcha `Dst*` modullari: DstHome, DstDashboard, DstReports, DstOrders, DstStock, DstTransfers, DstWriteOffs, DstRegrades, DstLandedCosts, DstReorder, DstPromotions, DstPayments, DstDebts, DstReturns, DstField, DstRoutes, DstTrips, DstVehicles, DstKpi, DstCustomers, DstProducts, DstPrices, DstTerritories, DstTeams, DstDictionaries, DstSettings, DstImport, DstInventory, DstIssues | SRM `POrder` (xarid buyurtmalarini ko'rish), Moliya tranzaksiyalari va hisoblar | `DstSeeCost`, `DstOverrideCredit`, `DstWriteOffApprove` (**`DstDiscountUnlimited` berilmaydi**) |
| `DEMO-Agent` | — | DstHome, DstCustomers, DstOrders, DstIssues | — |
| `DEMO-Ekspeditor` | — | DstHome, DstIssues | — |
| `DEMO-Omborchi` | DstOrders, DstInventory, DstTransfers, DstWriteOffs, DstRegrades, DstTrips; ERP ombor: `Incomes`, `Expenses` (kirim/chiqim/o'tkazma) | DstHome, DstStock, DstReturns, DstIssues | — (tannarx ko'rmaydi) |
| `DEMO-Kassir` | DstPayments | DstHome, DstDebts, DstIssues, Moliya tranzaksiyalari | — |

5. **Foydalanuvchilar** (**Admin panel → Foydalanuvchilar → Yangi**). Parolni o'zingiz tanlang va faqat o'zingiz biling. Profil tili — o'zbekcha.

| Login | To'liq F.I.Sh | Rol |
|---|---|---|
| `demo_rahbar` | DEMO Rahbar | DEMO-Rahbar |
| `demo_agent` | DEMO Jasur | DEMO-Agent |
| `demo_eksp` | DEMO Sherzod | DEMO-Ekspeditor |
| `demo_ombor` | DEMO Omborchi | DEMO-Omborchi |
| `demo_kassir` | DEMO Kassir | DEMO-Kassir |

6. **Omborlar** (**Omborxona → Ombor → Omborlar**): `DEMO Asosiy ombor (Toshkent)` (MRP = demo_ombor), `DEMO Filial ombor (Samarqand)` (MRP = demo_ombor), `DEMO Brak ombor` (MRP = demo_rahbar). Ombor turi — "Oddiy ombor". Matnda qisqartirib "DEMO Asosiy ombor", "DEMO Filial ombor" deb yoziladi.
7. Admin (siz) faqat tayyorgarlik va ma'lumotnomalar uchun. **Biznes amallarini "Kim" qatorida ko'rsatilgan demo foydalanuvchi bilan bajaring** — Admin barcha cheklovlardan (chegirma limiti, scope, tannarx) ozod, u bilan tekshiruv noto'g'ri natija beradi.

## Umumiy demo ma'lumotlar

Qo'llanma va UAT uchun yagona demo-ssenariy (`../Qollanma/_DEMO.md`):

Kompaniya: **DEMO Qurilish Savdo MChJ** — qurilish mollari ulgurji savdosi (Toshkent).

| Turi | Nomi | Qiymatlar |
|---|---|---|
| Ombor | DEMO Asosiy ombor (Toshkent) | savdo ombori, mas'ul — demo_ombor |
| Ombor | DEMO Filial ombor (Samarqand) | savdo ombori |
| Hudud | Toshkent sh. → Chilonzor, Yunusobod | ierarxiya |
| Narx turlari | Chakana (standart), Ulgurji, Diler | |
| Tovar | DEMO-SEM-400 Sement M400 | asosiy birlik: qop (50 kg); poddon = 40 qop; Chakana 62 000, Ulgurji 58 000, Diler 55 000, min 52 000 so'm/qop |
| Tovar | DEMO-KAF-3030 Kafel 30×30 | asosiy birlik: m²; quti = 1,44 m²; Chakana 95 000, Ulgurji 88 000 so'm/m² |
| Tovar | DEMO-GIP-125 Gipsokarton 12,5 mm | asosiy birlik: list; Chakana 68 000 so'm |
| Mijoz | DEMO Anvar do'koni | Chilonzor, Chakana, limit 20 000 000, to'lov muddati 7 kun, agent — Jasur |
| Mijoz | DEMO Bunyodkor qurilish | Yunusobod, Diler, limit 50 000 000, 14 kun |
| Foydalanuvchilar | demo_rahbar (Rahbar), demo_agent (Jasur, agent), demo_eksp (Sherzod, ekspeditor), demo_ombor (omborchi), demo_kassir (kassir) | |
| Mashina | 01 A 123 AA, Isuzu, 5 000 kg, 20 m³ | haydovchi-ekspeditor Sherzod |
| Aksiya | Sement 10+1 (har 10 qopga 1 qop sovg'a) | |
| Aksiya | Kafel ≥ 100 m² — 5% chegirma | |
| Kirim | 1 000 qop sement × 40 000, 500 m² kafel × 70 000, 200 list gipsokarton × 50 000 so'm (QQSsiz tannarx) | |

Sanalar: 2026-yil noyabr (yoki bugungi sana).

### UAT uchun aniqlashtirilgan qiymatlar

Yuqoridagi jadvalda ko'rsatilmagan, lekin testlar uchun kerak bo'lgan qiymatlar (barchasi DEMO prefiksli — keyin oson topib o'chiriladi):

| Narsa | Qiymat |
|---|---|
| Qo'shimcha ombor | `DEMO Brak ombor` — savdoda emas (brak qaytarishlar uchun) |
| Qo'shimcha tovar | `DEMO-KAF-3030-2 Kafel 30×30 (2-nav)` — m², Chakana 80 000 (faqat pereso'rtitsa testi uchun) |
| Tovar og'irligi / hajmi (asosiy birlikka) | Sement 50 kg / 0,035 m³ (qop); Kafel 17 kg / 0,01 m³ (m²); Gipsokarton 25 kg / 0,0375 m³ (list); Kafel 2-nav 17 kg / 0,01 m³ |
| Shtrix-kodlar (EAN-13, nazorat raqami to'g'ri) | Sement qop `4780000000014`, Sement poddon `4780000000021`, Kafel m² `4780000000038`, Kafel quti `4780000000045`, Gipsokarton `4780000000052`, Kafel 2-nav `4780000000069`. Noto'g'ri nazorat raqamli sinov kodi: `4780000000015` |
| Tovar kategoriyalari | Sement, Gipsokarton — `DEMO Qurilish aralashmalari`; Kafel, Kafel 2-nav — `DEMO Kafel` |
| Minimal narx | Sement — barcha uchala narx turida 52 000; boshqa tovarlarda minimal narx yo'q |
| DEMO Anvar do'koni | STIR 300000011, tel. +998 90 111 11 11, Toshkent, "Chilonzor 9-kvartal, 12", GPS 41.285600, 69.203400, toifa B, tashrif kunlari Du–Sh (har kuni, yakshanbadan tashqari) |
| DEMO Bunyodkor qurilish | STIR 300000012, tel. +998 90 222 22 22, Toshkent, "Yunusobod 4-mavze, 7", GPS 41.364000, 69.287000, nuqta turi "Qurilish bozori", toifa A, tashrif kunlari Se, Ju, **agent biriktirilmagan**; kirish qarzi 5 000 000 (muddati bugun − 10 kun) |
| Sozlamalar | Minimal buyurtma 500 000; yirik buyurtma 10 000 000; tashrif radiusi 200 m; agent naqdi topshiriladigan kassa — DEMO Kassa; agent to'lovlari: Naqd, Karta (→ DEMO Bank hisobi), Click (→ DEMO Bank hisobi) |
| Chegirma limiti | DEMO-Rahbar — 10%, DEMO-Agent — 5%, qolganlari — bo'sh (chegirma bera olmaydi) |
| Jamoa | `DEMO Toshkent jamoasi` (supervayzer — bo'sh); agent demo_agent (ombor DEMO Asosiy, +998 90 333 33 33); ekspeditor demo_eksp (ombor DEMO Asosiy, +998 90 444 44 44) |
| demo_ombor ko'rinish doirasi | Omborlar: DEMO Asosiy ombor, DEMO Filial ombor (Brak ombor ko'rinmaydi) |
| Ombor koordinatasi | DEMO Asosiy ombor: `41.311081,69.240562` |

### Asosiy raqamlar (nazorat uchun)

Buyurtma va qoldiq zanjiri bo'yicha asosiy hujjatlar (matnda shu harflar bilan ataladi):

| Belgi | Mijoz | Tarkib | Summa | Taqdiri |
|---|---|---|---|---|
| A | Anvar | 1 poddon sement (40 qop) + 30 quti kafel (43,2 m²) + 20 list gipsokarton | 7 944 000 | jo'natiladi, bekor qilinib qayta jo'natiladi, yetkaziladi, qaytarishlar, to'lov bilan yopiladi |
| C | Bunyodkor | 10 qop sement (−5%) + 10 m² kafel + 5 list gipsokarton | 1 812 500 | muddati o'tgan qarz bilan rahbar ruxsati, oldindan to'lanadi, 1-reysda yetkaziladi |
| D | Anvar | 5 list gipsokarton, o'zi olib ketadi | 340 000 | o'zi olib ketish |
| E | Anvar | 9 poddon sement (360 qop) | 22 320 000 | limitdan oshish, rahbar ruxsati, keyin bekor qilinadi |
| F | Anvar | 180 list gipsokarton | 12 240 000 | kirimni kutish, qisman jo'natish (100), qolganidan voz kechish, 2-reysda yetkaziladi |
| P1 | Anvar | 20 qop sement + 2 qop sovg'a (10+1) | 1 240 000 | aksiya, 2-reysda rad etiladi va omborga qaytadi |
| M1 | Anvar | Mini App: 2 poddon sement (80 qop) + 2 quti kafel (2,88 m²) | 5 233 600 | bot orqali rahbar ruxsati, Mini App'da yig'iladi, 1-reysda qisman yetkaziladi |

`DEMO Asosiy ombor` qoldig'i (haqiqiy) asosiy bloklar oxirida:

| Holat | Sement, qop | Kafel, m² | Gipsokarton, list |
|---|---|---|---|
| Boshlang'ich kirimdan keyin (SET) | 1 000 | 500 | 200 |
| Buyurtmalar bloki oxirida (ORD) | 938 | 459,68 | 125 |
| Bot bloki oxirida (BOT-01…25) | 858 | 455,36 | 125 |
| Ombor bloki oxirida (WH) | 758 | 405,48 | 99 |
| Reyslar yopilgach (DLV) | 779 | 395,48 | 94 |
| Uchidan-uchiga kun oxirida (E2E) | 763 | 385,48 | 94 |

DEMO Anvar do'koni qarzi: ORD oxirida 15 926 400 → BOT oxirida 19 160 000 → PAY oxirida 13 613 600 → reyslardan keyin 7 753 600 → E2E oxirida 8 633 600.

## Test kodlari

| Prefiks | Bo'lim | Fayl |
|---|---|---|
| T-DST-SET | Sozlash: sozlamalar, hududlar, narxlar, tovarlar, jamoalar, mijozlar, import, ruxsatlar, botga ulash | `01_SOZLASH.md` |
| T-DST-ORD | Buyurtma: yaratish, chegirma va narx qoidalari, kredit, rezerv, yig'ish, jo'natish, qaytarish, aksiyalar | `02_BUYURTMA.md` |
| T-DST-PAY | To'lovlar, kassaga topshirish, qarz muddatlari, akt sverki, avans | `03_PUL_OMBOR.md` |
| T-DST-WH | Ombor: qoldiqlar, inventarizatsiya, ko'chirma, hisobdan chiqarish, pereso'rtitsa, landed cost, minimal qoldiq, buyurtma taklifi | `03_PUL_OMBOR.md` |
| T-DST-BOT | Telegram bot va Mini App: agent, rahbar, omborchi, mijoz, ekspeditor | `04_TELEGRAM.md` |
| T-DST-DLV | Yetkazish: mashinalar, reyslar, xarita, yopish va dalolatnoma, KPI, tashrif rejasi | `05_YETKAZISH_HISOBOT.md` |
| T-DST-REP | Rahbar paneli, hisobotlar, dala nazorati, xatolar jurnali, audit, tannarx ko'rinishi | `05_YETKAZISH_HISOBOT.md` |
| T-DST-E2E | Uchidan-uchiga ish kuni va yakuniy nazorat | `05_YETKAZISH_HISOBOT.md` |

## Bajarish tartibi

Bot agent bloki pul/ombor blokidan **oldin** bajariladi (agent naqdi botda yig'iladi va kassir uni PAY blokida qabul qiladi), ekspeditor testlari esa reyslar rejalanganidan keyin.

| № | Blok | Testlar | Taxminiy vaqt |
|---|---|---|---|
| 1 | Sozlash va botga ulash | T-DST-SET-01 … 25 | 2,5–3 soat |
| 2 | Buyurtmalar | T-DST-ORD-01 … 27 | 3–4 soat |
| 3 | Bot: agent, rahbar, omborchi, mijoz | T-DST-BOT-01 … 25 | 3 soat |
| 4 | Pul va ombor | T-DST-PAY-01 … 08, T-DST-WH-01 … 12 | 3–3,5 soat |
| 5 | Mashina va reyslarni rejalash | T-DST-DLV-01 … 06 | 1,5 soat |
| 6 | Bot: ekspeditor (1-reys) | T-DST-BOT-26 … 32 | 1 soat |
| 7 | Reyslarni yopish, 2-reys, KPI, tashrif rejasi | T-DST-DLV-07 … 11, T-DST-BOT-33 … 34 | 1,5–2 soat |
| 8 | Hisobotlar va nazorat | T-DST-REP-01 … 11 | 1,5 soat |
| 9 | Uchidan-uchiga kun va yakuniy nazorat | T-DST-E2E-01 … 02 | 1,5–2 soat |

Jami: taxminan 18–21 soat (2,5–3 ish kuni, 2 kishi: biri web'da, biri telefonlarda).

## Xato darajalari (`../03_PILOT.md`, 7-bo'lim)

| Daraja | Ma'nosi | Tartib |
|---|---|---|
| **K1** | Ishni to'xtatadi yoki **pul / qoldiq / qarz noto'g'ri** | Darhol telefon + jurnal; shu kuni tuzatish yoki aylanma yo'l. Qoldiq yoki qarz farqi — har doim K1: avval sababi topiladi, keyin tuzatish hujjati (qo'lda bazani o'zgartirish yo'q) |
| **K2** | Xato bor, lekin aylanma yo'l mavjud | Ertasi kun relizi |
| **K3** | Noqulaylik, matn, dizayn | Pilotdan keyin |

Xatolar jurnalida holat: Yangi → Tuzatilmoqda → Tekshirishda → Yopildi (yopish uchun yechim majburiy). Daraja va holatni rahbar qo'yadi; ochiq K1 soni sahifa sarlavhasida ko'rinadi.

## Qabul mezoni

**UAT qabul mezoni** (shu hujjat):
- Barcha testlar o'tgan yoki o'tmaganlari K2/K3 darajasida va jurnalga yozilgan; **ochiq K1 yo'q**.
- T-DST-E2E-02 to'liq o'tgan: nazorat sanog'i "Farqlar: 0", mijoz qarzi = qarzlar sahifasi = akt sverki, kassa qoldig'i = sanalgan pul, agentlar qo'lida 0.

**TZ qabul mezoni** (pilotda, `../03_PILOT.md` 4.6-bo'lim):
- **5 agent bir hafta (5 ish kuni) faqat tizimda** (bot + Mini App) ishlaydi: qog'oz/Excel buyurtma = 0, har agentda har kuni kamida 1 buyurtma yoki tashrif (Q1).
- **Hafta oxirida qoldiqlar inventarizatsiya bilan mos keladi**: Inventarizatsiya → "Nazorat" sanog'i (qoldiqqa yozmasdan) "Farqlar: 0"; farq bo'lsa — har biri sababi bilan, K1 sifatida (Q2).
- Kassa = sanalgan pul, agentlar qo'lida 0 (Q3); 10 ta tasodifiy mijoz: tizim qarzi = akt sverki (Q4); ochiq K1 yo'q (Q5).

UAT pilotdan oldin o'tkaziladi; pilot ro'yxatidagi bandlar (M1–M7, B1–B11, D1–D7, A1–A9, N1–N5) shu hujjatdagi testlar bilan qamralgan — har testning "Nima tekshiriladi" qatorida mos band ko'rsatilgan (masalan "pilot B4").

Tekshiruv tugagach, `05_YETKAZISH_HISOBOT.md` oxiridagi "Yakuniy natijalar" jadvalini to'ldiring.
