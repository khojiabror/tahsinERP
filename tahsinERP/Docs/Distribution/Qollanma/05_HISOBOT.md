# 5. Hisobotlar va nazorat

Bu bob rahbar va supervayzer uchun:
- **Rahbar paneli** — kunlik holat bir ekranda;
- **Hisobotlar** — savdo kesimlari, tashriflar reja/fakti, agentlar topshirgan pul, Excel;
- **Dala nazorati** — agentlarning ish kuni, selfi, tashriflar, jonli joylashuv;
- **Agent KPI** — % savdo, bonus, jarima va HR ish haqiga o'tkazish;
- Telegram'dagi kunlik yakun;
- **Xatolar jurnali** va botdagi ⚠️ Muammo;
- o'zgarishlar tarixi (kim nimani o'zgartirdi) va tannarxni ko'rish huquqi.

**Namunaviy ssenariy (umumiy demo, noyabr 2026):**

| Sana | Hodisa | Summa |
|---|---|---|
| 02.11 | Bunyodkor qurilish — avans (bank) | 10 000 000 |
| 03.11 | Jo'natish: Anvar do'koni, 100 qop sement × 62 000 (+10 sovg'a) | 6 200 000 |
| 04.11 | Jo'natish: Bunyodkor qurilish, 150 qop × 55 000 (+15 sovg'a) | 8 250 000 |
| 05.11 | Jasur: Anvar do'konidan naqd 2 500 000 + karta 1 000 000; yangi buyurtma 200 qop × 62 000 (tasdiqlangan, jo'natilmagan) | 12 400 000 |
| 05.11 | Kassaga topshirish, sanalgan 2 490 000 (kamomad 10 000) | |

Sement tannarxi 40 000 so'm/qop (FIFO). Bunyodkor qurilishga agent biriktirilmagan.

## 5.1 Hisob qoidalari (hamma hisobot uchun bir xil)

| Ko'rsatkich | Qanday hisoblanadi |
|---|---|
| **Tushum** | O'tkazilgan jo'natishlar summasi − o'tkazilgan qaytarishlar summasi, **hujjat sanasi** bo'yicha. Bekor qilinganlar kirmaydi |
| **Tannarx (FIFO)** | Jo'natishda saqlangan FIFO tannarx (sovg'a qatorlari ham kiradi); qaytarishda — qaytgan tovar tannarxi |
| **Yalpi foyda** | Tushum − tannarx |
| **Marja, %** | Yalpi foyda ÷ tushum × 100, bir xona aniqlikda |
| **Agent** | Buyurtmadagi agent (buyurtmasiz qaytarishda — mijoz kartasidagi) |
| **Hudud** | Mijozning **hozirgi** hududi |
| **Qarz va muddatlar** | Qarzlar sahifasi bilan bir xil (3.7) |
| **Kassa va bank** | Moliya modulidagi hisob qoldiqlari |
| **Agent qo'lidagi pul** | O'tkazilgan, lekin kassaga tushmagan naqd to'lovlar |
| **Qoldiq qiymati** | Savdo omborlaridagi qoldiq × FIFO kirim narxlari (eng yangi partiyalar qoldiqda deb olinadi) |

**Ko'rinish doirasi:** har kim faqat o'z doirasini ko'radi — agent o'zini, supervayzer jamoasini, mijoz/hudud/ombor cheklovi bor foydalanuvchi — shularni. Kassa va bank qoldiqlari faqat **hech qanday cheklovi yo'q** foydalanuvchiga ko'rinadi.

**Tannarx:** tannarx, yalpi foyda, marja va qoldiq qiymati faqat "tannarxni ko'rish" huquqi (DstSeeCost) bilan — ekranda ham, Excel'da ham (5.11).

## 5.2 Rahbar paneli

**Kim:** rahbar   **Menyu:** Distributsiya → Rahbar paneli

Yuqorida: sana (standart — bugun; kelajak sanani tanlab bo'lmaydi), **Bugun** tugmasi, **Excel**, o'ng tomonda "05.11.2026 holatiga, yangilangan 18:40".

### 5.2.1 Yuqori plitalar

| Plita | Asosiy son | Ostida | Huquq |
|---|---|---|---|
| Tushum | Tanlangan kun | Oy boshidan | — |
| Yalpi foyda | Tanlangan kun | Oy boshidan · marja % | Tannarx |
| Yig'ilgan pul | Tanlangan kunda mijozlardan olingan to'lovlar (qaysi kassaga tushganidan qat'i nazar, agent qo'lidagi naqd ham) | Oy boshidan | — |
| Qarz | Mijozlar ochiq qarzi jami | Muddati o'tgan (qizil) | — |
| Pul | Kassa/bank hisoblari qoldig'i, valyuta bo'yicha | Agentlar qo'lida | Cheklovsiz foydalanuvchi; boshqalarga "—" |
| Qoldiq qiymati | Savdo omborlari jami | "FIFO tannarxda, savdo omborlari" | Tannarx |

**Misol (05.11.2026):**

| Plita | Son | Ostida | Hisob |
|---|---|---|---|
| Tushum | 0 | Oy boshidan: 14 450 000 | 05.11 da jo'natish yo'q; 6 200 000 + 8 250 000 |
| Yalpi foyda | 0 | 3 450 000 · 23,9% | (6 200 000 − 110 × 40 000) + (8 250 000 − 165 × 40 000) = 1 800 000 + 1 650 000 |
| Yig'ilgan pul | 3 500 000 | 13 500 000 | 2 500 000 + 1 000 000; oy: + 10 000 000 |
| Qarz | 4 200 000 | Muddati o'tgan: 0 | Anvar do'koni, muddat 10.11 |
| Pul | 13 490 000 UZS | Agentlar qo'lida: 0 | Naqd pul 2 490 000 + Hisob raqam 10 000 000 + Korporativ karta 1 000 000 (hisoblar noldan boshlangan deb) |

Marja: 3 450 000 ÷ 14 450 000 × 100 = 23,875 → **23,9%**.

### 5.2.2 Oxirgi 14 kun

Kunlar bo'yicha tushum (ko'k) va — tannarx huquqi bilan — yalpi foyda (yashil) grafigi. Misolda 03.11: 6 200 000 / 1 800 000, 04.11: 8 250 000 / 1 650 000.

### 5.2.3 "Buyurtmalar va agentlar" bloki

| Qator | Ma'nosi | Misol (05.11) |
|---|---|---|
| Bugungi buyurtmalar | Tanlangan kunda yaratilgan (bekor qilinmagan) buyurtmalar soni va summasi | 1 · 12 400 000 |
| Tasdiq kutmoqda | "Yangi" holatdagi buyurtmalar (bosilsa — Buyurtmalar sahifasi) | 0 |
| Jo'natish kutmoqda | Tasdiqlangan, yig'ilmoqda va qisman jo'natilgan; summa — jo'natilmagan qism | 1 · 12 400 000 |
| Qaytarishlar | Tanlangan kundagi qaytarishlar summasi | 0 |
| Ishga chiqqan agentlar | Kunni boshlagan agentlar / barcha faol agentlar (ekspeditorsiz) | 1 / 1 |
| Tashriflar | Shu kundagi tashriflar (bosilsa — Tashriflar hisoboti) | 1 |

> **Eslatma:** "Tasdiq kutmoqda" va "Jo'natish kutmoqda" har doim **hozirgi** holatni ko'rsatadi — o'tgan sana tanlansa ham.

### 5.2.4 "Mijozlar qarzi" bloki

- Rangli chiziq: Muddati kelmagan, 1–7, 8–30, 31–60, 60+ ulushlari; ostida summalar va **Avans (oldindan to'lov)**.
- "Qarzdor mijozlar: 1, muddati o'tgani: 0".
- "Eng katta muddati o'tgan qarz" — 5 ta mijoz: muddati o'tgan summa va eng uzoq kechikish kunlari. Yo'q bo'lsa — "Muddati o'tgan qarz yo'q".
- **Hammasi →** — Qarzlar sahifasi "Muddati o'tgan" filtri bilan.

Misolda: Muddati kelmagan 4 200 000, Avans 1 750 000 (Bunyodkor qurilish).

### 5.2.5 "Pul" bloki

Har faol kassa/bank hisobi qoldig'i (valyuta bilan), **Agentlar qo'lida** (nechta agentda) va **Topshirilgan, tasdiq kutmoqda**. **Agentlar topshirgan pul →** — kassa hisobotiga.

### 5.2.6 "Agentlar reytingi (oy boshidan)"

Tushum bo'yicha 5 ta agent: Agent, Tushum, (tannarx bilan) Yalpi foyda, Buyurtmalar, Mijozlar. Agentsiz buyurtmalar "(ko'rsatilmagan)" qatorida. **Savdo →** — agent kesimidagi savdo hisoboti.

| Agent | Tushum | Yalpi foyda | Buyurtmalar | Mijozlar |
|---|---|---|---|---|
| (ko'rsatilmagan) | 8 250 000 | 1 650 000 | 1 | 1 |
| Jasur | 6 200 000 | 1 800 000 | 1 | 1 |

### 5.2.7 "Qoldiq qiymati" (tannarx huquqi bilan)

Savdo omborlari: Ombor, Tovarlar soni, Qiymati. Kirim narxi topilmagan tovarlar soni sariq belgi bilan ("Kirim narxi topilmagan tovarlar soni (qiymati 0 deb olingan)").

Misol (05.11, DEMO Asosiy ombor): sement 725 × 40 000 = 29 000 000, kafel 498 × 70 000 = 34 860 000, gipsokarton 200 × 50 000 = 10 000 000 → **73 860 000**.

### 5.2.8 Excel

**Excel** tugmasi ko'rsatkichlar ro'yxatini yuklaydi: Bo'lim, Ko'rsatkich, sana ustuni — Bugun/Oy boshidan tushum, (tannarx bilan) yalpi foyda, qaytarishlar, yig'ilgan pul, buyurtmalar, qarz muddatlari, hisoblar qoldig'i, agentlar qo'lida, kutayotgan topshirishlar, (tannarx bilan) omborlar qiymati. Fayl nomi `dashboard_20261105.xlsx`.

## 5.3 Savdo hisoboti

**Kim:** rahbar, supervayzer, agent (o'zi)   **Menyu:** Distributsiya → Hisobotlar → Savdo

Yuqoridagi yorliqlar (kesimlar): **Agent**, **Mijoz**, **Tovar**, **Hudud**, **Kategoriya**.

Filtrlar: davr (standart — oy boshidan bugungacha), Barcha agentlar, Barcha hududlar (tanlangan hudud **ichidagi hududlar bilan**), Barcha kategoriyalar, Barcha omborlar. Drill-down'dan kelgan mijoz yoki tovar filtri yuqorida belgi bo'lib turadi — ✕ bosilsa olib tashlanadi.

| Ustun | Ma'nosi |
|---|---|
| (kesim nomi) | Agent / mijoz / tovar / hudud / kategoriya |
| Miqdor | Faqat Tovar kesimida: sof miqdor (jo'natilgan − qaytgan), asosiy birlikda |
| Jo'natilgan | Jo'natishlar summasi |
| Qaytarishlar | Qaytarishlar summasi |
| Tushum | Jo'natilgan − qaytarishlar |
| Yalpi foyda, Marja, % | Tannarx huquqi bilan |
| Buyurtmalar, Mijozlar | Nechta buyurtma va mijoz |

Pastda **Jami** qatori. Qidiruv, saralash va sahifalash — server tomonida (katta bazada ham tez).

### Drill-down (qatorni bosish)

| Hozirgi kesim | Qator bosilsa |
|---|---|
| Agent | O'sha agent mijozlari |
| Hudud | O'sha hudud mijozlari |
| Mijoz | O'sha mijoz olgan tovarlar |
| Kategoriya | O'sha kategoriya tovarlari |
| Tovar | O'sha tovarni olgan mijozlar |

**Misol (01.11–05.11.2026):**

Agent kesimi:

| Agent | Jo'natilgan | Qaytarishlar | Tushum | Yalpi foyda | Marja, % |
|---|---|---|---|---|---|
| (ko'rsatilmagan) | 8 250 000 | 0 | 8 250 000 | 1 650 000 | 20,0 |
| Jasur | 6 200 000 | 0 | 6 200 000 | 1 800 000 | 29,0 |
| **Jami** | 14 450 000 | 0 | 14 450 000 | 3 450 000 | 23,9 |

Jasur qatori bosilsa → Mijoz kesimi, filtr "Agent: Jasur" → DEMO Anvar do'koni 6 200 000. U bosilsa → Tovar kesimi: DEMO-SEM-400 Sement M400, miqdor 110 qop (100 + 10 sovg'a), tushum 6 200 000.

Hudud kesimi: Chilonzor 6 200 000, Yunusobod 8 250 000. Toshkent sh. filtri tanlansa — ikkalasi ham (ichki hududlar).

Marjalar: 1 650 000 ÷ 8 250 000 = 20,0%; 1 800 000 ÷ 6 200 000 = 29,03 → 29,0%.

> **Eslatma:** Aksiya sovg'asi narxsiz, lekin uning tannarxi hisobga olinadi — shuning uchun 10+1 aksiyasi marjani pasaytiradi (Jasur: sovg'asiz bo'lganida foyda 6 200 000 − 4 000 000 = 2 200 000 bo'lardi).

> **Eslatma:** Hudud — mijozning hozirgi hududi. Mijoz boshqa hududga o'tkazilsa, uning o'tgan sotuvlari ham yangi hududda ko'rinadi.

**Excel:** joriy kesim va filtrlar bilan, 100 000 qatorgacha (`sales_agent_20261101_20261105.xlsx`). Tannarx huquqi bilan qo'shimcha "Tannarx (FIFO)", "Yalpi foyda", "Marja, %" ustunlari.

## 5.4 Tashriflar hisoboti (reja/fakt)

**Kim:** rahbar, supervayzer   **Menyu:** Distributsiya → Hisobotlar → Tashriflar

Filtrlar: davr, agent, hudud. Yuqorida plitalar: Reja, Fakt, Reja bajarilishi, Samaradorlik, Tashrif buyurtmalari.

| Ustun | Ma'nosi |
|---|---|
| Ish kunlari | Agent kunni boshlagan kunlar |
| Reja | Rejadagi tashriflar: mijoz kartasidagi tashrif kunlari × davrdagi o'sha hafta kunlari; kunlik reja saqlangan kunlarda — o'sha reja (Tashrif rejasi sahifasi) |
| Fakt | Haqiqiy tashriflar ("Keldim") |
| Rejadagilar | Faktdan rejaga kirganlari |
| Rejadan tashqari | Fakt − rejadagilar |
| Reja bajarilishi | Rejadagilar ÷ Reja × 100 |
| Buyurtma bilan | Buyurtma bilan yakunlangan tashriflar |
| Samaradorlik | Buyurtma bilan ÷ Fakt × 100 |
| To'lov bilan, Buyurtmasiz | Shu natijali tashriflar |
| Tashrif buyurtmalari, O'rtacha buyurtma | Tashriflarda olingan buyurtmalar summasi va o'rtachasi |

**Misol (02.11–06.11.2026, Jasur):** Anvar do'koni tashrif kunlari Du va Pa → reja 2 (02.11 dushanba, 05.11 payshanba). Fakt: 02.11 — buyurtmasiz (sabab tanlangan), 05.11 — buyurtma 12 400 000 va to'lov.

| Reja | Fakt | Rejadagilar | Rejadan tashqari | Reja bajarilishi | Buyurtma bilan | Samaradorlik | Tashrif buyurtmalari |
|---|---|---|---|---|---|---|---|
| 2 | 2 | 2 | 0 | 100% | 1 | 50% | 12 400 000 |

> **Eslatma:** Reja mijoz kartasidagi **joriy** agent va tashrif kunlaridan olinadi — o'tgan oyda boshqa agentga tegishli bo'lgan mijoz bugungi agent rejasida hisoblanadi.

**Excel:** barcha ustunlar bilan (`visits_20261102_20261106.xlsx`).

## 5.5 Agentlar topshirgan pul

**Kim:** rahbar, kassir   **Menyu:** Distributsiya → Hisobotlar → Agentlar topshirgan pul

Filtrlar: davr, agent, holat (Barcha holatlar / Kutmoqda / Qabul qilindi / Rad etildi). Yuqorida plitalar: To'lovlar bo'yicha, Sanalgan, Farq, Agentlar qo'lida.

**"Agentlar bo'yicha"** jadvali: Agent, Topshirishlar, To'lovlar bo'yicha, Sanalgan, Farq, Topshirilgan tasdiq kutmoqda, Agentlar qo'lida (hozir), Eng eski to'lov.

**"Topshirishlar"** jadvali: Hujjat №, Sana, Agent, Kassa / hisob, To'lovlar bo'yicha, Sanalgan, Farq, Holat (server tomonida sahifalash).

Misol: DK-2026-00001, 05.11, Jasur, Naqd pul, 2 500 000, 2 490 000, **−10 000**, Qabul qilindi.

> **Muhim:** "Eng eski to'lov" ustuni uzoq kun topshirilmagan pulni ko'rsatadi. Qoida: agent naqdini o'sha kuni topshiradi; 1 kundan eski sana — rahbar uchun signal.

**Excel:** topshirishlar ro'yxati, izoh bilan (`handovers_…xlsx`).

## 5.6 Dala nazorati

**Kim:** rahbar, supervayzer (jamoasi)   **Menyu:** Distributsiya → Dala nazorati

Tanlangan sana (standart — bugun) bo'yicha har agent va ekspeditor:

| Ustun | Ma'nosi |
|---|---|
| Agent | Ism; ekspeditorda "Ekspeditor" belgisi |
| Selfi | Kun boshidagi selfi (bosilsa kattalashadi) |
| Kun boshi / Kun yakuni | Vaqt va joy; boshlamagan bo'lsa — "boshlanmagan" |
| Tashriflar | Tashriflar soni (buyurtmasizlari bilan) |
| Buyurtmalar | Shu kundagi buyurtmalar |
| So'nggi joylashuv | Oxirgi nuqta; 🟢 "Jonli joylashuv faol" — oxirgi 10 daqiqada jonli nuqta kelgan |
| | **Batafsil** |

**Batafsil** (bitta agent):
- **Tashriflar:** Mijoz, Keldi (vaqt), Masofa (mijoz nuqtasidan, m), Natija (Buyurtma / To'lov / Buyurtmasiz + sabab), Foto; belgilar "rejadan tashqari" va "statik" (jonli emas, oddiy joylashuv yuborilgan).
- **Joylashuv izi:** Vaqt, Manba (jonli / kun boshi/yakuni / tashrif). **Iz xaritada (N)** — Yandex xaritada 100 tagacha nuqta bilan ochiladi.

Bugungi sahifa har daqiqada o'zi yangilanadi (rasm ochiq bo'lsa kutadi).

> **Muhim:** Selfi va fotolarni ko'rish uchun Sozlamalarda **Bot ochiq manzili (rasmlar uchun)** to'ldirilgan bo'lishi kerak, aks holda "Rasmlarni ko'rish uchun Sozlamalarda bot manzilini (Bot ochiq manzili) kiriting." Selfi majburiyligi — Sozlamalar → "Kun boshida selfi majburiy".

> **Eslatma:** Jonli joylashuv faqat ish kuni ochiq paytda (kun boshidan yakunigacha) yoziladi, ko'pi bilan 30 soniyada bir nuqta — agentning shaxsiy vaqti kuzatilmaydi.

Kunlik reja va marshrut xaritasi (rejadagi/borilgan/borilmagan nuqtalar) — **Tashrif rejasi** sahifasida (2-bob).

## 5.7 Agent KPI

**Kim:** rahbar, HR   **Menyu:** Distributsiya → Agent KPI

### 5.7.1 Sxema (har agent yoki ekspeditorga)

Agent qatoridagi ✏️ → "Sxema — Jasur":

| Maydon | Nima kiritiladi | Misol (Jasur) |
|---|---|---|
| HR xodimi (ish haqi qatori) | HR modulidagi xodim — ish haqi shu xodim qatoriga tushadi | Jasur (HR) |
| % savdodan | Sof savdodan komissiya | 1 |
| Oylik reja | So'mda; 0 — rejasiz | 100 000 000 |
| Reja bajarilsa bonus | Qat'iy summa | 1 000 000 |
| Rejadan ortig'iga % | Rejadan oshgan qismdan | 0,5 |
| Tashrif rejasi min, % | Bundan past bo'lsa jarima | 90 |
| Bajarilmasa jarima | Qat'iy summa | 300 000 |
| Muddati o'tgan qarzdan, % | Agent mijozlarining 30 kundan ortiq kechikkan qarzidan | 1 |
| Reys kamomadidan, % | Ekspeditor uchun: yopilgan reyslar kamomad tannarxidan | 0 |
| Faol | Hisoblashga kiradi | ✓ |

### 5.7.2 Hisoblash

1. **Oy** ni tanlab **Ko'rsatish**, keyin **Hisoblash**.
2. Faol sxemali har agent uchun:

| Ko'rsatkich | Manba |
|---|---|
| Sof savdo | Savdo hisobotining o'zi: jo'natilgan − qaytarilgan, shu agent, shu oy |
| Reja, % | Sof savdo ÷ oylik reja × 100 |
| Tashrif, % | Tashriflar hisobotidagi "Reja bajarilishi" (oy boshidan bugungacha yoki oy oxirigacha) |
| % savdo | Sof savdo × % savdodan |
| Bonus | Reja bajarilsa: bonus + (sof savdo − reja) × ortig'iga % ; + qo'lda bonus |
| Jarima | Tashrif min % dan past bo'lsa — qat'iy jarima; + muddati o'tgan qarz × %; + reys kamomadi × %; + qo'lda jarima |
| Jami | % savdo + bonus − jarima |

Hamma summa butun so'mga yaxlitlanadi. Ish haqiga o'tkazilgan natija qayta hisoblanmaydi. Sxemasi yo'q yoki hisoblanmagan agentda — "Hisoblanmagan".

**Misol — Jasur, noyabr 2026:** sof savdo 120 000 000; tashrif rejasi 85% bajarilgan; oy oxirida 30 kundan ortiq kechikkan qarz 2 000 000.

| Ko'rsatkich | Hisob | Summa |
|---|---|---|
| Reja, % | 120 000 000 ÷ 100 000 000 × 100 | 120% |
| % savdo | 120 000 000 × 1% | 1 200 000 |
| Bonus | 1 000 000 + (120 000 000 − 100 000 000) × 0,5% = 1 000 000 + 100 000 | 1 100 000 |
| Jarima | 300 000 (85% < 90%) + 2 000 000 × 1% = 300 000 + 20 000 | 320 000 |
| **Jami** | 1 200 000 + 1 100 000 − 320 000 | **1 980 000** |

> **Eslatma:** "Muddati o'tgan qarz" KPI'da — faqat 31–60 va 60+ ustunlari (30 kundan ortiq), oy oxiri holatida, agentning **hozirgi** mijozlari bo'yicha. Anvar do'konining 4 200 000 (muddat 10.11) 30.11 da 20 kun kechikkan — 8–30 ustunida, jarimaga kirmaydi.

### 5.7.3 Qo'lda tuzatish

Natija qatoridagi **±** → **Qo'shimcha bonus**, **Qo'shimcha jarima**, Izoh → Saqlash. Faqat ish haqiga o'tkazilmagan natijada.

### 5.7.4 HR ish haqiga o'tkazish

**Oldindan:** HR modulida shu oy uchun ish haqi hisobi **qoralama** holatida yaratilgan va unda xodim qatori bo'lishi kerak.

1. Natija qatorida **Ish haqiga**.
2. Tizim xodim qatoriga qo'shadi: mukofotga — % savdo + bonus (+ qo'shimcha bonus), jarimaga — jarima (+ qo'shimcha jarima); qator izohiga "KPI 2026-11". "To'lovga" HR formulasi bilan qayta hisoblanadi.
3. Natija "Ish haqida" belgisi bilan qotadi.

**Misol:** Jasurning qatori: hisoblangan 5 000 000, daromad solig'i 12%. KPI'dan keyin: mukofot +2 300 000 (1 200 000 + 1 100 000), jarima +320 000. To'lovga = 5 000 000 + 2 300 000 − 320 000 − 5 000 000 × 12% = 5 000 000 + 2 300 000 − 320 000 − 600 000 = **6 380 000**.

| Xato | Ma'nosi |
|---|---|
| "Agent HR xodimiga bog'lanmagan (KPI sxemasida xodimni tanlang)." | Sxemada HR xodimi yo'q |
| "2026-11 uchun xodim qatori bor qoralama ish haqi hisobi topilmadi — avval HR'da hisobni yarating." | HR'da qoralama hisob yo'q yoki unda xodim qatori yo'q |
| "KPI ish haqiga o'tkazilgan." | Qayta o'tkazib yoki tuzatib bo'lmaydi |

**Qaytarish:** "Ish haqida" yonidagi ↺ — summalar qatordan ayiriladi va natija yana tahrirlanadi. Faqat HR hisobi hali qoralama bo'lsa: aks holda "Ish haqi hisobi tasdiqlangan — qaytarib bo'lmaydi."

> **Eslatma:** HR formulasi bo'yicha daromad solig'i faqat **hisoblangan** ish haqidan olinadi; KPI mukofoti soliq summasini o'zgartirmaydi. Soliq hisobi bo'yicha savol bo'lsa — buxgalter bilan kelishing.

## 5.8 Telegram'dagi kunlik yakun

**Kim:** rahbar (Telegram'ga "Rahbar" roli bilan ulangan)   **Qayerda:** bot, har kuni **20:00 (Toshkent vaqti)**; istalgan payt — **📊 Bugungi yakun** tugmasi

Xabar:

```
Kun yakuni 05.11.2026
Buyurtmalar: 1 ta, 12 400 000 so'm
Jo'natildi: 0 so'm
Tushum: 3 500 000 so'm
Umumiy qarz: 4 200 000 so'm (muddati o'tgan: 0)
Faol agentlar: 1/1
Kun yetakchisi: Jasur — 12 400 000 so'm
```

| Qator | Manba |
|---|---|
| Buyurtmalar | Shu kunda yaratilgan, bekor qilinmagan buyurtmalar soni va summasi |
| Jo'natildi | Shu kunda o'tkazilgan jo'natishlar summasi |
| Tushum | Shu kunda kiritilgan mijoz to'lovlari (agent qo'lidagi naqd ham) |
| Umumiy qarz | Barcha mijozlar ochiq qarzi (avanslar ayirilmaydi) |
| Faol agentlar | Kunni boshlaganlar / faol agentlar |
| Kun yetakchisi | Shu kun buyurtmalari summasi eng katta agent |

> **Muhim:** Kunlik yakundagi "Tushum" — **yig'ilgan pul** (Rahbar panelidagi "Yig'ilgan pul" bilan bir xil ma'noda), Rahbar panelidagi va Savdo hisobotidagi "Tushum" esa **jo'natish minus qaytarish**. Raqamlarni solishtirishda shuni yodda tuting.

> **Eslatma:** Kunlik yakun ko'rinish doirasidan qat'i nazar butun kompaniya bo'yicha hisoblanadi.

Boshqa avtomatik rahbar xabarlari (tegishli boblarda): yirik buyurtma va limitdan oshgan buyurtma (✅ Tasdiqlash tugmasi bilan), kassaga topshirish, yangi savdo nuqtasi, hisobdan chiqarish tasdig'i, ko'chirma farqi, minimal qoldiqdan past tovar, reys natijalari, yangi muammo.

## 5.9 Xatolar jurnali

**Kim:** hamma (yozadi), rahbar/mas'ul (baholaydi va yopadi)   **Menyu:** Distributsiya bosh sahifasi → **Xatolar jurnali** plitasi; bot — **⚠️ Muammo** yoki `/xato`

> **Eslatma:** Xatolar jurnali chap menyuda yo'q — Distributsiya bosh sahifasidagi plita orqali kiriladi.

### 5.9.1 Botdan yozish (agent, ekspeditor, omborchi)

1. **⚠️ Muammo** (yoki `/xato`) ni bosing.
2. Nima qildingiz va nima bo'ldi — buyurtma/mijoz raqami bilan yozing. Skrinshotni izoh bilan yuborish mumkin.
3. Bot raqam beradi (P-001, P-002 …). Rahbarlarga: "⚠️ Yangi muammo P-001 (Jasur): …".

### 5.9.2 Web'dan yozish

"Xato yoki muammo yozish" formasi:

| Maydon | Nima kiritiladi | Misol |
|---|---|---|
| Qayerda | bot / Mini App / sahifa | Qarzlar sahifasi |
| Hujjat raqami | Buyurtma, to'lov va h.k. | DP-2026-00002 |
| Daraja | baholanmagan / K1 / K2 / K3 | K1 — ishni to'xtatadi |
| Nima qilindi va nima bo'ldi * | Kamida 5 belgi | "To'lov kiritildi, qarz kamaymadi" |
| Kutilgan natija | | "Qarz 2 500 000 ga kamayishi kerak" |

**Yuborish** → raqam beriladi, rahbarlarga xabar ketadi.

### 5.9.3 Darajalar va holatlar

| Daraja | Ma'nosi | Muddat |
|---|---|---|
| K1 — ishni to'xtatadi | Ish to'xtagan yoki pul/qoldiq noto'g'ri | Darhol (telefon + jurnal) |
| K2 — aylanma yo'l bor | Ishlash mumkin, lekin noqulay yo'l bilan | Ertasi kun |
| K3 — noqulaylik | Matn, ko'rinish | Pilotdan keyin |

Holatlar: **Yangi → Tuzatilmoqda → Tekshirishda → Yopildi**. Har qatorda holat, daraja va "Yechim / izoh (yopish uchun majburiy)" → **Saqlash**. Yechimsiz yopib bo'lmaydi: "Yopish uchun yechimni yozing."

Ro'yxat: **Ochiq** (standart) yoki **Hammasi**; tartib — avval ochiqlar, K1 yuqorida. Ustunlar: raqam va daraja, Sana, Kim, Qayerda, Nima qilindi va nima bo'ldi, Holat, Kutilgan natija, Rasm. Sarlavhada ochiq K1 soni qizil belgi bilan ("K1: 2").

> **Muhim:** Pul yoki qoldiq farqi — har doim K1. Avval sabab topiladi, keyin tuzatish hujjati kiritiladi; ma'lumotni "qo'lda" o'zgartirish yo'q.

## 5.10 O'zgarishlar tarixi (kim nimani o'zgartirdi)

Distributsiya ma'lumotlaridagi har o'zgarish (mijoz kartasi, narx, limit, tovar birligi, buyurtma qatorlari, to'lov, hisob varag'i yozuvlari, ombor hujjatlari, KPI sxemasi va boshqalar) avtomatik **o'zgarishlar jurnaliga** yoziladi: kim (foydalanuvchi yoki bot), qachon, qaysi maydon, eski va yangi qiymat. Yozuv shu amalning o'zi bilan bitta tranzaksiyada — amal bekor bo'lsa, jurnal ham yozilmaydi.

Ekranda ko'rinadigan tarix:

| Qayerda | Nima ko'rinadi |
|---|---|
| Buyurtma kartasi → **Tarix** | Har holat o'zgarishi: vaqt, yangi holat, kim, izoh (masalan limitdan oshishga ruxsat izohi) |
| Narxlar → **Narx tarixi** | Har narx o'zgarishi |
| Hisobdan chiqarish kartasi | Tuzgan, Yuborilgan, Tasdiqlagan, Bekor qilgan (vaqt bilan) |
| To'lovlar ro'yxati | "Qabul qildi", manba (web/bot) |
| Hujjatlar | Bekor qilingan hujjatda sabab |

> **Eslatma:** To'liq maydon darajasidagi jurnal uchun alohida sahifa yo'q — kerak bo'lsa administrator bazadan (o'zgarishlar jurnali jadvali) hisobot tayyorlaydi. O'tkazilgan hujjatni o'chirib bo'lmaydi va bekor qilingan hujjatni tahrirlab bo'lmaydi — tarix doim saqlanadi.

## 5.11 Tannarxni ko'rish huquqi (DstSeeCost)

**Kim beradi:** Admin   **Menyu:** Admin panel → Rollar → (rol qatori) → Huquqlar → `DstSeeCost`

Huquqsiz foydalanuvchi (agent, operator, omborchi) quyidagilarni **ko'rmaydi**:

| Joy | Nima yashiriladi |
|---|---|
| Rahbar paneli | Yalpi foyda plitasi, grafikdagi foyda chizig'i, agentlar reytingidagi foyda, Qoldiq qiymati (plita va blok); Excel'da ham |
| Savdo hisoboti | Yalpi foyda, Marja, %, Excel'da Tannarx (FIFO) ham |
| Buyurtma kartasi | Tannarx va foyda |
| Ko'chirma dalolatnomalari | Kamomad, yo'qotish, ortiqcha summalari, qator tannarxi va summasi (chop etishda ham) |
| Hisobdan chiqarish, Pereso'rtitsa | Tannarx ustunlari |
| Buyurtma taklifi | Narx ustuni (buyurtma 0 narx bilan yaratiladi) |
| Qo'shimcha xarajatlar (landed cost) | Butun sahifa |

Boshqa maxsus huquqlar (Admin panel → Rollar → Huquqlar):

| Huquq | Nima beradi |
|---|---|
| DstOverrideCredit | Limitdan oshgan yoki muddati o'tgan qarzli mijoz buyurtmasini tasdiqlash (web va bot) |
| DstDiscountUnlimited | Rol chegirma limitidan yuqori chegirma |
| DstWriteOffApprove | Hisobdan chiqarishni tasdiqlash / rad etish, tasdiqlanganni bekor qilish |

> **Muhim:** Admin va Developer barcha maxsus huquqlarga ega. Kassa/bank qoldiqlari (Rahbar paneli "Pul") esa huquqdan tashqari yana bir shartga bog'liq — foydalanuvchining hech qanday ko'rinish cheklovi bo'lmasligi kerak.

> **Eslatma:** Ba'zi joylarda summa huquqsiz ham ko'rinadi: inventarizatsiya boshlang'ich sanog'idagi "Tannarx" ustuni va Telegram'dagi ko'chirma farqi xabaridagi summalar (u faqat rahbarlarga boradi).

## 5.12 Rahbarning kundalik nazorat tartibi

| Qachon | Nima | Qayerda |
|---|---|---|
| 09:30 | Agentlar ishga chiqdimi, selfi | Dala nazorati |
| Kun davomida | Limit va yirik buyurtma ruxsatlari | Telegram |
| 18:00 | Tushum, yig'ilgan pul, jo'natish kutayotganlar | Rahbar paneli |
| 19:00 | Kassaga topshirishlar, farqlar | Hisobotlar → Agentlar topshirgan pul |
| 20:00 | Kunlik yakun | Telegram |
| Haftada bir | Muddati o'tgan qarzlar, reja bajarilishi | Qarzlar, Hisobotlar → Tashriflar |
| Oy oxiri | KPI hisoblash va ish haqiga o'tkazish; nazorat sanog'i | Agent KPI, Inventarizatsiya |
| Har kuni | Ochiq K1 muammolar | Xatolar jurnali |
