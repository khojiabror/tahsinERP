# Distributsiya — S6 pilot: reja va qabul ro'yxati

Holat: 2026-10-20 — kod va hujjat tomoni tayyorlandi (branch `claude/dst-s6-pilot-a446b3`, S4 qoldiqlari va S5 hisobotlari shu branch'ga merge qilingan). Pilotni foydalanuvchi o'tkazadi.

**Qarorlar (2026-10-20):**

| Savol | Qaror |
|---|---|
| Pilot kompaniyasi | Hozircha yo'q — namunaviy kompaniya bilan mashq qilindi (6-bo'lim, `pilot-sample/`) |
| Sanalar | Ishlab chiqish tugagandan keyin |
| Litsenziya | KORP |
| Server, domen | Server tayyor emas; domen — 2026-10-21 |
| S4 qoldiqlari, S5 hisobotlar | Pilotdan oldin merge — bajarildi (shu branch) |
| Agent to'lovlari | Naqd, karta, Click — Sozlamalar → "Agent to'lovlari" (har usulga hisob) |
| Inventarizatsiya | ERP ichida: Distributsiya → Inventarizatsiya (boshlang'ich va nazorat sanog'i) |
| Chegirma, minimal va yirik buyurtma | Distributsiya → Sozlamalar (bor edi) |
| Xatolar jurnali | ERP ichida: Distributsiya → Xatolar jurnali; agentlar — botdagi "⚠️ Muammo" |
Bog'liq: `01_PHASE1_MVP.md` (S6 qatori, 1-bo'lim — qabul mezoni), qo'llanma `Qollanma_Distributsiya_uz.md` / `_ru.md` (shu papkada).

**TZ qabul mezoni:** 5 agent bir hafta faqat tizimda (bot + Mini App) ishlaydi; hafta oxirida qoldiqlar inventarizatsiya bilan mos keladi.

Belgilar: ⬜ tekshirilmagan · ✅ o'tdi · ❌ xato (jurnalga raqam bilan)

---

## 1. Pilotgacha tayyorgarlik (T−7 … T−1)

| # | Ish | Kim | Holat |
|---|---|---|---|
| P1 | Pilot kompaniyasi bazasi (`New-TenantDb`), migratsiyalar 013–024 | dasturchi | ⬜ |
| P2 | **Kompaniya litsenziyasi** (Edition **KORP** — Distribution moduli ichida). Litsenziyasiz web ham, bot API ham bloklanadi | vendor | ⬜ |
| P3 | Server: ERP (IIS, HTTPS), `tahsinERP.Bot` (bot domeni, HTTPS), `TAHSINERP_BOT_KEY` = bot `Bot__MasterKey`, kunlik zaxira | dasturchi | ⬜ |
| P4 | BotFather: bot, `/setdomain` (Mini App), `tenant add`, `webhook set` | dasturchi | ⬜ |
| P5 | Foydalanuvchilar va rollar: rahbar, operator, omborchi, kassir, 5 agent (ERP `USERS` + rol ruxsatlari: `DstOrders`, `DstStock`, `DstPayments`, `DstReturns`, `DstDebts`, `DstImport`, `DstInventory`, `DstIssues` — ko'rish hammaga (xato yozish uchun), o'zgartirish rahbarga; maxsus: `DstOverrideCredit` — faqat rahbar, `DstSeeCost` — rahbar) | admin | ⬜ |
| P6 | Distributsiya → Sozlamalar: yirik buyurtma summasi, minimal buyurtma, tashrif radiusi (200 m), agent naqdi topshiriladigan kassa, **Agent to'lovlari** (naqd, karta, Click — karta va Click uchun hisob tanlanadi, aks holda agent ularni qabul qila olmaydi), bot nomi; rol bo'yicha chegirma limiti; ombor sozlamalari (savdo ombori, minusga ruxsat yo'q) | admin | ⬜ |
| P7 | Jamoalar: supervayzer jamoasi, 5 agent (ombori, telefoni) | admin | ⬜ |
| P8 | Boshlang'ich ma'lumot importi (2-bo'lim) | operator | ⬜ |
| P9 | **Boshlang'ich inventarizatsiya:** Distributsiya → Inventarizatsiya → "Boshlang'ich" sanoq: varaq (Excel) → sanab to'ldirish → yuklash → "Yakunlash va qoldiqni tenglash" (kirim tarixi yo'q tovarga tannarx ustuni). Qoldiqqa yozish ombor uchun oyiga bir marta. Pilot oxiridagi taqqoslash shu qoldiqdan boshlanadi | omborchi | ⬜ |
| P10 | Telegram'ga ulash: har xodim va pilot mijozlari uchun kod (Jamoalar / mijoz kartasi → "Telegram'ga ulash") | admin | ⬜ |
| P11 | O'qitish: 1 soat ofis, 30 daqiqa agentlar (qo'llanma bo'yicha), sinov buyurtmasi → bekor qilish | dasturchi + rahbar | ⬜ |
| P12 | Mashq kuni (T−1): 3-bo'limdagi ssenariy 2–3 ta haqiqiy mijozda | hamma | ⬜ |

## 2. Boshlang'ich ma'lumot (Import sahifasi, tartib bilan)

`/Distribution/DstImport` — har tur uchun shablon (`Namuna` varag'ida misol). Oqim: fayl → **ko'rib chiqish** (hech narsa yozilmaydi; har qator: yangi / yangilanadi / o'zgarishsiz yoki xatolar qator raqami bilan) → **"Import qilish"** (o'sha fayl qayta tekshirilib, bitta tranzaksiyada yoziladi). Bitta xato — hech narsa yozilmaydi.

| # | Tur | Kalit | Eslatma |
|---|---|---|---|
| 1 | Hududlar | Viloyat / Tuman / Hudud nomi | Takror yuklash xavfsiz |
| 2 | Narx turlari | Nomi | Bitta "standart" |
| — | Agentlar | — | Jamoalar sahifasida qo'lda (login kerak) |
| 3 | Mijozlar (savdo nuqtalari) | STIR, bo'lmasa nomi | Hudud, agent (login), narx turi, tashrif kunlari, limit, kechiktirish, GPS |
| 4 | Marshrutlar | Mijoz (STIR/nomi) | Agent + tashrif kunlari (Du=1 … Ya=7); qayta taqsimlash uchun |
| 5 | Tovarlar | Artikul | Asosiy birlik, brend, og'irlik, shtrix-kod, 2–3 qo'shimcha birlik |
| 6 | Tovar birliklari | Artikul + birlik | Mavjud tovarlarga (quti, poddon …), koeffitsient asosiy birlikda |
| 7 | Narxlar | Artikul | Har narx turi ustuni + "min" |
| 8 | Kirish qarzlari | Mijoz | Har mijozga bir marta; manfiy — avans |
| 9 | Boshlang'ich qoldiq | Artikul | Import emas — **Inventarizatsiya** sahifasidagi boshlang'ich sanoq (P9) |

Namuna fayllar (mashq kompaniyasi: 6 hudud, 40 nuqta, 5 agent, 24 tovar): `pilot-sample/1_hududlar.xlsx` … `9_boshlangich_qoldiq.xlsx`.

Tekshiruv: import natijasidagi soni = Excel'dagi qatorlar soni; 5 ta tasodifiy mijoz kartasini ochib solishtirish; agent botida "Marshrut" — bugungi mijozlar to'g'ri.

## 3. Kunlik ssenariy

| Vaqt | Kim | Qayerda | Qadam |
|---|---|---|---|
| 08:30 | Agent | Bot | ▶️ Kunni boshlash (joylashuv) → 🗺 Marshrut |
| 09:00–17:00 | Agent | Bot + Mini App | Mijoz → "Keldim" (jonli joylashuv, 200 m) → **Buyurtma** (Mini App: katalog, birlik, savat, imzo) yoki **Buyurtmasiz** (sabab) → **To'lov** (naqd — agent qo'lida; karta/Click — hisobga) |
| darhol | Rahbar | Bot | Yirik buyurtma / limit oshdi → "Tasdiqlash" tugmasi (`DstOverrideCredit`) |
| har 1–2 soat | Operator | Web: Buyurtmalar | Yangi → **Tasdiqlash** (limit, rezerv, ombor) |
| tushdan keyin | Omborchi | Web: Buyurtma kartasi | **Yig'ish** (yig'ish varaqasi chop) → **Jo'natish** (miqdor; qisman bo'lsa — qolganini yopish yoki keyin) → nakladnoy |
| yetkazilgach | Operator / agent | Web | **Yetkazildi** (1-bosqichda qo'lda; reys/ekspeditor — 2-bosqich) |
| kun davomida | Kassir | Web: To'lovlar | Ofisdagi to'lov (naqd/karta/o'tkazma → hisob) |
| 17:30 | Agent | Bot | 💰 Kassa → kassaga topshirish |
| 17:30–18:00 | Kassir | Web: To'lovlar → Agentlar naqdi | Sanab qabul qilish (farq bo'lsa — haqiqiy summa) yoki rad |
| kerak bo'lsa | Operator | Web: Qaytarishlar | Buyurtma bo'yicha qaytarish (yaroqli / brak ombor, sabab) |
| 18:00 | Agent | Bot | ⏹ Kunni yakunlash |
| 20:00 | Rahbar | Bot | Kunlik yakun (avtomatik) |
| istalgan vaqt | Hamma | Bot "⚠️ Muammo" / web Xatolar jurnali | Xato yoki muammo (7-bo'lim) |
| 18:30 | Rahbar | Web: Qarzlar, Qoldiqlar | Kun tekshiruvi (5-bo'lim metrikalari) |

Shu ssenariy kod darajasida avtomatik tekshirilgan — 6-bo'lim.

## 4. Qabul ro'yxati (TZ → tekshiruv)

### 4.1 Ma'lumotnomalar
| # | Talab | Qanday tekshiriladi | Holat |
|---|---|---|---|
| M1 | Hudud: viloyat → tuman → hudud | Import 1, Hududlar sahifasi | ⬜ |
| M2 | Mijoz kartasi: STIR, telefonlar, GPS, tur, A/B/C, agent, narx turi, tashrif kuni, limit, kechiktirish | Import 3, karta | ⬜ |
| M3 | Bir nechta o'lchov birligi koeffitsient bilan | Import 5–6; Mini App'da birlik tanlash, buyurtmada asosiy birlikka o'tadi | ⬜ |
| M4 | Bir nechta shtrix-kod | Import 5–6; web formada skaner bilan qidiruv | ⬜ |
| M5 | Narx turlari, minimal narx, narx tarixi, ommaviy % | Import 7; Narxlar sahifasi | ⬜ |
| M6 | Agent yangi nuqta qo'shadi → rahbar tasdiqlaydi | Botda "Yangi nuqta" → web'da Mijozlar → tasdiqlash | ⬜ |
| M7 | Rus/o'zbek interfeys | Profil tili ru → distributsiya sahifalari va bot | ⬜ |

### 4.2 Buyurtma va ombor
| # | Talab | Qanday tekshiriladi | Holat |
|---|---|---|---|
| B1 | Agent do'konda **3 daqiqada** buyurtma beradi, ofisda darhol ko'rinadi | Har agentda 3 ta buyurtma vaqti (Mini App ochilishidan "Yuborish"gacha) | ⬜ |
| B2 | Narx avtomatik: maxsus → narx turi → standart | Turli narx turli 2 mijoz, bir tovar | ⬜ |
| B3 | Chegirma rol limiti bilan, min narxdan past emas | Limitdan katta chegirma — rad | ⬜ |
| B4 | Qarz limiti / muddati o'tgan qarz → blok → rahbar ruxsati | Limitga yaqin mijoz; botdagi "Tasdiqlash" tugmasi | ⬜ |
| B5 | Rezerv: mavjud = haqiqiy − rezerv; ikki operator oxirgi qoldiqni ikki marta ololmaydi | Qoldiqlar sahifasi | ⬜ |
| B6 | Holatlar: Yangi → Tasdiqlangan → Yig'ilmoqda → Jo'natildi → Yetkazildi → Yopildi; Bekor (sabab) | Buyurtma tarixi | ⬜ |
| B7 | Qisman jo'natish, qolganini yopish | 1 ta buyurtma | ⬜ |
| B8 | O'zi olib ketish | 1 ta buyurtma | ⬜ |
| B9 | Yig'ish varaqasi, nakladnoy (chop/PDF) | Chop etish | ⬜ |
| B10 | Omborchiga yangi tasdiqlangan buyurtma xabari | Omborchi boti | ⬜ |
| B11 | Mini App'da kamera bilan shtrix-kod | 3 ta tovarni kamera bilan topish | ⬜ |

### 4.3 Pul va qarz
| # | Talab | Qanday tekshiriladi | Holat |
|---|---|---|---|
| D1 | Mijoz balansi real vaqtda = kirish qarzi + jo'natish − to'lov − qaytarish | Har kuni 3 mijoz: Qarzlar sahifasi = akt sverki = qo'lda hisob | ⬜ |
| D2 | Agent naqd oladi → qo'lida → kassaga topshiradi → kassir tasdiqlaydi (farq bilan) | Har kech; kassa qoldig'i = sanalgan pul | ⬜ |
| D3 | Agent: naqd, karta, Click; ofis: yana Payme/Uzum/o'tkazma — hisobga | Botda faqat yoqilgan usullar chiqadi; karta/Click darhol tanlangan hisobga | ⬜ |
| D4 | Qaytarish (yaroqli/brak) qarzni kamaytiradi, omborga qaytadi | 1 ta qaytarish | ⬜ |
| D5 | Qarz muddati: muddati kelmagan, 1–7, 8–30, 31–60, 60+ | Qarzlar sahifasi | ⬜ |
| D6 | Akt sverki (ekran, chop) | 1 mijoz | ⬜ |
| D7 | Takror bosish ikki to'lov/buyurtma yaratmaydi | Botda ikki marta bosish | ⬜ |

### 4.4 Agent va bot
| # | Talab | Qanday tekshiriladi | Holat |
|---|---|---|---|
| A1 | Kun boshi joylashuv bilan | `AGENT_DAYS` / kunlik yakun | ⬜ |
| A2 | Kun boshi selfi (Sozlamalarda majburiy qilish mumkin) | Dala nazorati sahifasi | ⬜ |
| A3 | "Keldim" radius ichida, forward qilingan joylashuv belgilanadi | 300 m uzoqdan — rad | ⬜ |
| A4 | Bugungi marshrut (tashrif kuni bo'yicha), qarz belgisi | Har agent | ⬜ |
| A5 | Buyurtmasiz tashrif — sabab majburiy | 1 ta | ⬜ |
| A6 | Tokcha rasmi | 1 ta | ⬜ |
| A7 | Mijozga xabar: jo'natildi / yetkazildi / to'lov | Ulangan 2 mijoz | ⬜ |
| A8 | Rahbarga: yirik buyurtma, limit, kassaga topshirish, kunlik yakun 20:00 | Rahbar boti | ⬜ |
| A9 | Litsenziya tugasa bot aniq aytadi ("litsenziya faol emas", aloqa xatosi emas) | S6'da tuzatildi | ✅ |

### 4.5 Nazorat va hisobotlar
| # | Talab | Qanday tekshiriladi | Holat |
|---|---|---|---|
| N1 | Audit: kim, qachon, eski → yangi | Narxni o'zgartirib, audit jurnalini ko'rish | ⬜ |
| N2 | O'tkazilgan hujjat o'chirilmaydi, sabab bilan bekor | Jo'natilgan buyurtmani bekor qilish — rad | ⬜ |
| N3 | Agent faqat o'z mijozlarini ko'radi | Agent A botida agent B mijozi yo'q | ⬜ |
| N4 | Xarid narxi/foyda faqat `DstSeeCost` bilan | Operator kartasida tannarx yo'q | ⬜ |
| N5 | Rahbar paneli, savdo kesimlari, tashrif reja/fakt, Excel | Rahbar paneli va Hisobotlar sahifalari, har kech | ⬜ |

### 4.6 Yakuniy mezon (pilot oxiri, T+7)
| # | Mezon | Qanday | Holat |
|---|---|---|---|
| Q1 | 5 agent 5 ish kuni **faqat tizimda** ishladi | Qog'oz/Excel buyurtma = 0; har agentda har kuni ≥1 buyurtma yoki tashrif | ⬜ |
| Q2 | **Qoldiqlar inventarizatsiya bilan mos** | Inventarizatsiya → "Nazorat" sanog'i (qoldiqqa yozmasdan): "Farqlar: 0". Farq bo'lsa — har biri sababi bilan (Xatolar jurnalida, K1) | ⬜ |
| Q3 | Kassa = sanalgan pul; agentlar qo'lida 0 | Kassa hisobi, "Agentlar naqdi" | ⬜ |
| Q4 | 10 ta tasodifiy mijoz: tizim qarzi = mijoz bilan akt sverki | Akt sverki chop + imzo | ⬜ |
| Q5 | Kritik (K1) xato ochiq qolmagan | Xato jurnali | ⬜ |

## 5. Metrikalar (kunlik, rahbar to'ldiradi)

| Metrika | Manba | Maqsad |
|---|---|---|
| Faol agentlar / 5 | Kunlik yakun | 5 |
| Rejadagi tashriflar / bajarilgan | Bot marshruti, `VISITS` | ≥ 80% |
| Buyurtmalar soni va summasi, agent bo'yicha | Buyurtmalar ro'yxati (filtr: agent, sana) | — |
| Buyurtma tuzish vaqti (o'lchov: 3 ta / agent / kun) | Sekundomer | ≤ 3 daqiqa |
| Buyurtma → tasdiq vaqti | Buyurtma tarixi | ≤ 1 soat |
| Tasdiq → jo'natish vaqti | Buyurtma tarixi | shu kuni |
| Limit bloklari va rahbar ruxsatlari | Buyurtma kartasi | — |
| Yig'ilgan pul / kassaga topshirilgan / farq | To'lovlar, Agentlar naqdi | farq 0 |
| Qaytarishlar soni va sababi | Qaytarishlar | — |
| Buyurtmasiz tashriflar sababi | Bot | — |
| Xatolar: K1/K2/K3 soni | Xato jurnali | K1 = 0 |
| Qog'oz yoki telefon orqali buyurtma (tizimdan tashqari) | Rahbar so'rovi | 0 |

Har kecha: `Qarzlar` va `Qoldiqlar` sahifalaridan Excel/ekran nusxa — kunlik taqqoslash uchun.

## 6. Avtomatik tekshiruv (kod tomoni, 2026-10-20)

- **`PilotDayScenarioTests.FullDay_FromImportToDayEnd`** — 3-bo'limdagi kunni servislar orqali (bot API'si chaqiradigan xuddi shu kod) haqiqiy yozuvlar bilan o'tkazadi: Excel import (hudud, narx turlari, mijozlar, marshrut, birlik, narx, kirish qarzi) → kun boshi → marshrut → "Keldim" (1 km — rad, 55 m — qabul) → bot buyurtmasi (takror yuborish — bitta) → buyurtmasiz (sababsiz — rad) → tasdiq → limit bloki → rahbar ruxsati → yig'ish → jo'natish (to'liq va qisman + qoldiqni yopish) → yetkazildi → agent naqdi (takror bosish — bitta) → ofis kartasi → kassaga topshirish (10 000 kamomad) → qaytarish → kun yakuni → kunlik yakun. Tekshiradi: ombor harakati = −jo'natilgan + qaytgan; mijoz balansi = aging = akt sverki; kassa = qabul qilingan naqd; agent qo'lida 0; audit. Faqat `DST_PILOT_E2E=<baza nomi>` bilan va sinov bazasida ishlaydi (yozadi). `DB_DST_S6` (DB_DST_UI nusxasi) da 2 marta o'tdi.
- **`PilotWeekSimulationTests.FiveAgents_FiveDays_AcceptanceCriteria`** — namunaviy kompaniya (`PilotSampleData`, fayllari `pilot-sample/`) bilan **5 agent × 5 ish kuni**: import → boshlang'ich inventarizatsiya → har kuni marshrut, "Keldim", buyurtma/buyurtmasiz, to'lov (naqd/karta/Click), bot qayta yuborishi, ofis tasdig'i va rahbar ruxsati, jo'natish (qisman ham), yetkazildi, qaytarish, kassaga topshirish (kamomad bilan), xatolar jurnali. Oxirida qabul mezoni **mustaqil model** bilan: nazorat sanog'i farqsiz, har mijoz qarzi = model = aging = akt sverki, kassa = sanalgan naqd, bank = karta/Click, agentlar qo'lida 0. `DB_DST_S6` da ketma-ket 2 marta o'tdi (takroriy mashq ham). Birinchi ishga tushish:

| Kun | Buyurtma | Summa | Jo'natildi | Yig'ildi | Faol agent |
|---|---|---|---|---|---|
| 21.10 Wed | 16 | 46,869,100 | 41,577,100 | 5,026,000 | 5/6 |
| 22.10 Thu | 12 | 38,349,000 | 36,909,000 | 1,846,000 | 5/6 |
| 23.10 Fri | 12 | 28,442,800 | 25,173,800 | 9,622,000 | 5/6 |
| 24.10 Sat | 15 | 34,331,600 | 32,427,600 | 7,510,000 | 5/6 |
| 26.10 Mon | 12 | 60,906,800 | 51,624,800 | 19,539,000 | 5/6 |

Rejadagi tashrif 100, bajarildi 100; buyurtma 67 (208,899,300), buyurtmasiz 18; bot qayta yuborishi 6 (ikki buyurtma bo'lmadi); limit bo'yicha rahbar ruxsati 31; qisman jo'natish 9 (qoldiq yetmay bekor 0); qaytarish 4; to'lov 40 (43,543,000), kamomad 20,000; xato yozuvlari 5. Nazorat sanog'i: farq 0; mijoz qarzlari = model = akt sverki; kassa va bank mos.

- Mashq topgan xato (tuzatildi): oldindan to'langan buyurtma jo'natishda darhol "Yopildi" bo'lardi va "Yetkazildi"ni belgilab bo'lmasdi (yetkazish vaqti va mijozga xabar yo'qolardi) — endi yopilgan buyurtmada ham yetkazish qayd qilinadi.
- Import testlari: `PilotImportTests` (rollback; avansli buyurtma yetkazish, agent to'lov usullari), `PilotImportRulesTests`.
- Bot offline: webhook (noto'g'ri secret 401, dublikat update — bitta yozuv) → inbox → handler → ERP → 403 litsenziya → foydalanuvchiga to'g'ri xabar (avval "aloqa yo'q" / "rolingiz uchun emas" chiqardi — tuzatildi).
- **Tekshirilmagan:** brauzerda web sahifalar va haqiqiy ERP bilan bot (sinov bazasida litsenziya yo'q — chetlab o'tilmadi), Telegram ichidagi Mini App (HTTPS va BotFather `/setdomain` kerak). Bular P2–P4 dan keyin, mashq kunida (P12).

Qayta ishga tushirish (sinov bazasi):
```
TAHSINERP_DB=Data Source=localhost;Initial Catalog=DB_DST_S6;Integrated Security=True
DST_PILOT_E2E=DB_DST_S6
vstest.console tahsinERP.Tests.dll /TestCaseFilter:"FullyQualifiedName~PilotDayScenario|FullyQualifiedName~PilotWeekSimulation"
```
Ixtiyoriy: `DST_PILOT_REPORT=<fayl.md>` — metrikalar jadvali, `DST_PILOT_SAMPLE_OUT=<papka>` — namuna Excel fayllari.

## 7. Xatolarni qayd qilish

**tahsinERP ichida:** Distributsiya → **Xatolar jurnali** (`DstIssues`). Agent va boshqa xodimlar botdagi **⚠️ Muammo** tugmasi (yoki `/xato`) bilan yozadi — matn va skrinshot; ofis — sahifadagi forma bilan. Har yozuvga raqam (P-001 …) beriladi, rahbarlarga botda xabar boradi. Maydonlar:

| Ustun | Mazmuni |
|---|---|
| № | P-001, P-002 … |
| Sana, vaqt | |
| Kim topdi, rol | agent / operator / … |
| Qayerda | bot / Mini App / web sahifa (URL) |
| Hujjat | buyurtma, to'lov, mijoz raqami |
| Nima qilindi | qadamlar |
| Kutilgan / bo'lgan | |
| Skrinshot | Telegram yoki ekran |
| Daraja | **K1** — ishni to'xtatadi yoki pul/qoldiq noto'g'ri; **K2** — aylanma yo'l bor; **K3** — noqulaylik, matn |
| Holat | yangi → tuzatilmoqda → tekshirishda → yopildi (yopish uchun yechim majburiy) |

Daraja va holatni rahbar (DstIssues o'zgartirish huquqi) qo'yadi; ochiq K1 soni sahifa sarlavhasida. Tartib: K1 — darhol telefon + jadval, shu kuni tuzatish yoki aylanma yo'l; K2 — ertasi kun relizi; K3 — pilotdan keyin. Reliz kechqurun 19:00 dan keyin (zaxiradan so'ng), har reliz jadvalda qaysi xatolarni yopgani bilan. Qoldiq yoki qarz farqi — har doim K1, avval sabab topiladi, keyin tuzatish hujjati (qo'lda o'zgartirish yo'q).

## 8. Rollar (pilotda)

| Rol | Kim | Vazifa |
|---|---|---|
| Pilot rahbari | kompaniya rahbari | qarorlar, ruxsatlar (limit), kunlik metrikalar, xato jurnali |
| Operator | 1 kishi | tasdiq, yetkazildi, qaytarish, import |
| Omborchi | 1 kishi | yig'ish, jo'natish, boshlang'ich va yakuniy inventarizatsiya |
| Kassir | 1 kishi | ofis to'lovlari, agent naqdini qabul qilish |
| Agentlar | 5 kishi | bot + Mini App, qog'ozsiz |
| Dasturchi | vendor | o'rnatish, o'qitish, K1/K2 tuzatish, kunlik zaxira |

## 9. Qolgan ishlar

Server va domen (P3–P4), litsenziya (P2) tayyor bo'lgach: brauzerda web sahifalar, Telegram ichida bot va Mini App bilan mashq kuni (P12). Pilot kompaniyasi va sanalar — ishlab chiqish tugagach.
