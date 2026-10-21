# 5. Yetkazish, hisobotlar va uchidan-uchiga kun

Bu bo'lim 2-bosqich yetkazishni (mashinalar, reys rejalash va sig'im, xarita va tartiblash, reysni yopish va farq dalolatnomasi, reys xarajatlari, agent/ekspeditor KPI, tashrif rejasi), rahbar paneli va hisobotlarni (savdo kesimlari, drill-down, Excel, tashriflar, agentlar puli, dala nazorati, xatolar jurnali, audit, tannarx ko'rinishi) va oxirida **uchidan-uchiga ish kuni** ni tekshiradi. Oxirida barcha testlar uchun **Yakuniy natijalar** jadvali.

Bajarish tartibi (00_INTRO):
1. T-DST-DLV-01 … 06 (PAY/WH blokidan keyin) → 04_TELEGRAM: T-DST-BOT-26 … 32;
2. T-DST-DLV-07, T-DST-DLV-08 → T-DST-BOT-33 → T-DST-DLV-09 → T-DST-BOT-34 → T-DST-DLV-10, 11;
3. T-DST-REP-01 … 11;
4. T-DST-E2E-01, 02.

Boshlang'ich holat (PAY/WH blokidan keyin): DEMO Asosiy ombor S 758/10/748, K 405.48/10/395.48, G 99/5/94; Anvar qarzi **13 613 600**; Bunyodkor avansi **1 812 500**; C buyurtmasi Tasdiqlangan (rezervda); F, P1, M1 — jo'natilgan, yetkazilmagan; DEMO Kassa 7 536 400; DEMO Bank hisobi 6 812 500.

---

## Yetkazish

### T-DST-DLV-01. Mashina qo'shish va tekshiruvlar
**Nima tekshiriladi:** Mashina davlat raqami (takrorlanmaydi), yuk ko'tarish (majburiy) va hajm, haydovchi, odatiy ekspeditor va ombor bilan saqlanadi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-16
**Qadamlar:**
1. **Distributsiya → Mashinalar → Qo'shish**: Davlat raqami = `01 A 123 AA`, Rusumi = `Isuzu`, Yuk ko'tarish, kg = `0`, Hajm, m³ = `20`. **Saqlash**.
2. Yuk ko'tarish = `5000`, Haydovchi = `DEMO Sherzod`, Telefon = `+998 90 444 44 44`, Ekspeditor = DEMO Sherzod, Ombor = DEMO Asosiy ombor, Faol ✓. **Saqlash**.
3. Yana **Qo'shish**: Davlat raqami = `01 A 123 AA`, Yuk ko'tarish = `3000`. **Saqlash**. So'ng Davlat raqami = `AB`. **Saqlash**.
**Kutilgan natija:**
- 1-qadam: "Yuk ko'tarish va hajm musbat bo'lsin."
- 2-qadam: ro'yxatda 01 A 123 AA — Isuzu, 5 000 kg, 20 m³, DEMO Sherzod, DEMO Asosiy ombor.
- 3-qadam: "Bunday yozuv allaqachon bor.", keyin "Davlat raqamini kiriting (4–20 belgi)."
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-02. Yetkazish sozlamalari: xarita kaliti, ovozli buyurtma, ombor koordinatasi
**Nima tekshiriladi:** Yandex kaliti, ovozli buyurtma va omborlar koordinatasi (marshrut boshi) saqlanadi; noto'g'ri qiymat rad etiladi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-01
**Qadamlar:**
1. Mashinalar sahifasidagi **Yetkazish sozlamalari**: Yandex Maps JavaScript API kaliti = `abc`. **Saqlash**.
2. Kalitni haqiqiy test kaliti bilan almashtiring (yo'q bo'lsa — bo'sh qoldiring). DEMO Asosiy ombor koordinatasi = `41.311081;69.240562`. **Saqlash**.
3. Koordinata = `41.311081,69.240562`, "Botda ovozli buyurtma" ✓. **Saqlash**. Sahifani yangilang.
**Kutilgan natija:**
- 1-qadam: "Kalit formati noto'g'ri."
- 2-qadam: "Koordinatalar noto'g'ri."
- 3-qadam: "Saqlandi"; qiymatlar saqlangan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-03. Oldindan to'langan buyurtmani jo'natish (C)
**Nima tekshiriladi:** Avans jo'natish bilan yopiladi va buyurtma darhol "Yopildi" bo'ladi; yetkazish keyin reys orqali qayd qilinadi (T-DST-BOT-29).
**Kim:** demo_ombor, demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-08
**Qadamlar:**
1. demo_ombor: C kartasi → **Jo'natish** (Sement 10, Kafel 10, Gipsokarton 5) → **Jo'natish**.
2. demo_rahbar: C kartasi (tannarx), **Qarzlar**, **Qoldiqlar**. Omborchi botini ko'ring.
**Kutilgan natija:**
- Holat tarixi: Tasdiqlangan → Jo'natildi → **Yopildi**. Jo'natish Jami **1 812 500**, tannarx 10 × 40 000 + 10 × 70 000 + 5 × 50 000 = **1 350 000**, yalpi foyda 462 500.
- Bunyodkor: Qarz 0, Avans 0.
- Qoldiq: S **748/0/748**, K **395.48/0/395.48**, G **94/0/94**.
- "Minimal qoldiqdan past" xabari **qayta kelmaydi** (Gipsokarton allaqachon minimaldan past edi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-04. Reys rejalash va mashina sig'imi tekshiruvi (1-reys)
**Nima tekshiriladi:** Reysga faqat yetkazib berilmagan, o'zi olib ketish bo'lmagan, shu ombordagi jo'natishlar qo'shiladi; og'irlik/hajm mashina sig'imidan oshsa saqlanmaydi; ekspeditorga xabar.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-03
**Qadamlar:**
1. **Distributsiya → Reyslar → Yangi reys**: Sana = bugun, Mashina = 01 A 123 AA, Ekspeditor = DEMO Sherzod, Ombor = DEMO Asosiy ombor.
2. "Jo'natishlar (reyssiz)" ro'yxatini ko'ring. M1, C va F jo'natishlarini belgilang. Og'irlik/hajm ko'rsatkichini ko'ring. **Saqlash**.
3. F belgisini olib tashlang. **Saqlash**.
**Kutilgan natija:**
- 2-qadam ro'yxatda: M1 (DEMO Anvar do'koni, 4 049 kg, 5 233 600), C (DEMO Bunyodkor qurilish, 795 kg, 1 812 500), F (DEMO Anvar do'koni, 2 500 kg, 6 800 000), P1 (DEMO Anvar do'koni, 1 100 kg, 1 240 000). A (yetkazilgan) va D (o'zi olib ketgan) **yo'q**.
- Saqlashda "Og'irlik 7344 kg mashina sig'imidan (5000 kg) oshdi." (4 048,96 + 795 + 2 500 = 7 343,96).
- 3-qadam: og'irlik **4843.96 kg** (96,9%), hajm 3,4663 m³ (17,3%); reys `R-2026-NNNNN` (1-reys), holat **Rejalandi**, manzillar 2, summa **7 046 100**.
- Ekspeditorga botda reys xabari (T-DST-BOT-26).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-05. Xarita, tartiblash va marshrut varag'i
**Nima tekshiriladi:** Reys xaritada (kalit bo'lsa) yoki Yandex havolasi bilan ko'rinadi; "Tartiblash" ombordan eng yaqin manzil bo'yicha; qo'lda tartib; marshrut varag'i chop etiladi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-04
**Qadamlar:**
1. 1-reys kartasini oching. Xarita blokini ko'ring.
2. Manzillar tartibini ↑↓ bilan Bunyodkor birinchi bo'ladigan qilib o'zgartiring.
3. **Tartiblash** ni bosing.
4. **Marshrut varag'i** ni oching va chop eting.
**Kutilgan natija:**
- 1-qadam: kalit bo'lsa — xaritada ombor ⌂ va raqamlangan 2 nuqta, avtomobil yo'li; kalit bo'lmasa — "Xarita uchun Yandex kaliti sozlanmagan (Mashinalar → Yetkazish sozlamalari)." va "Yandex xaritada ochish" havolasi (ombor → manzillar).
- 3-qadam: tartib **1. DEMO Anvar do'koni, 2. DEMO Bunyodkor qurilish** (ombordan Anvar ≈ 4,2 km, Bunyodkor ≈ 7,0 km); marshrut uzunligi taxminan **15.4 km** (to'g'ri chiziq bo'yicha: 4,2 + 11,2).
- 4-qadam: varaqda Mashina, Haydovchi, Ekspeditor, Ombor; har manzil uchun mijoz, manzil/telefon, buyurtma raqami, tovarlar, summa (5 233 600 va 1 812 500), qarz (Anvar 13 613 600, Bunyodkor 0); "Berdi / Qabul qildi" imzolari.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-06. Rejalangan reysni bekor qilish; jo'natish ikki reysga kirmaydi
**Nima tekshiriladi:** Bitta jo'natish faqat bitta reysda; rejalangan reys bekor qilinsa jo'natish bo'shaydi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-04
**Qadamlar:**
1. **Yangi reys** (01 A 123 AA, DEMO Sherzod). Ro'yxatni ko'ring: M1 va C bormi? P1 ni belgilang. **Saqlash**.
2. Shu reysda **Bekor** → sabab `DEMO: sinov reysi`.
3. Yana **Yangi reys** ni ochib ro'yxatni ko'ring (saqlamang).
**Kutilgan natija:**
- 1-qadam: ro'yxatda M1 va C **yo'q** (1-reysda). Reys saqlanadi (Rejalandi, 1 100 kg).
- 2-qadam: holat "Bekor qilingan", reysda manzil yo'q.
- 3-qadam: P1 yana ro'yxatda (F ham).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

> Endi 04_TELEGRAM dagi T-DST-BOT-26 … 32 ni bajaring (1-reys botda: yo'lga chiqish, C — rasm bilan, M1 — qisman, manzilda to'lov 4 000 000, yoqilg'i 150 000).

### T-DST-DLV-07. Reys xarajatlarini moliyaga o'tkazish
**Nima tekshiriladi:** Ekspeditor yozgan xarajat ofisda kassa tanlanib moliyaga chiqim sifatida o'tkaziladi; ofis kiritgan xarajat darhol o'tadi; bekor qilish moliya yozuvini o'chiradi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-32
**Qadamlar:**
1. 1-reys kartasi → "Reys xarajatlari": Yoqilg'i 150 000 (Kutmoqda) → **Moliyaga**: Chiqim moddasi — bo'sh, Kassa / hisob = DEMO Kassa → tasdiqlang.
2. **Qo'shish**: Turi = To'xtash joyi, Summa = `20000`, Kassa / hisob = DEMO Kassa, Izoh = `DEMO`. **Saqlash**.
3. To'xtash joyi qatorini **Bekor** qiling.
4. **Moliya → Boshqaruv → Tranzaksiyalar** da "Reys" bo'yicha qidiring.
**Kutilgan natija:**
- 1-qadam: xarajat holati o'tkazilgan; moliyada chiqim **150 000**, hisob DEMO Kassa, kategoriya "Reys xarajatlari", maqsad "Reys R-…: Yoqilg'i — AI-92", kontragent "01 A 123 AA".
- 2-qadam: kassa tanlangani uchun darhol moliyaga chiqim 20 000.
- 3-qadam: xarajat bekor, moliyadagi 20 000 yozuvi o'chirilgan.
- DEMO Kassa qoldig'i 7 536 400 − 150 000 = **7 386 400**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-08. 1-reysni yopish: omborga qaytgan miqdor, kamomad, farq dalolatnomasi
**Nima tekshiriladi:** Yetkazilmagan tovar: qaytgani — qaytarish hujjati (ombor kirimi FIFO tannarxda, qarz kamayadi), qaytmagani — kamomad (mijozga tuzatish krediti, tannarxi reysda); rahbarga yakun xabari; dalolatnoma chop etiladi.
**Kim:** demo_ombor (qabul qiluvchi omborchi), demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-30, T-DST-BOT-31, T-DST-DLV-07
**Qadamlar:**
1. demo_ombor: 1-reys kartasi → "Reysni yopish" bloki. Sement qatorida "Omborga qaytdi" = `11` → **Reysni yopish**.
2. "Omborga qaytdi" = `9`, Izoh = `DEMO: 1 qop yo'lda yorildi` → **Reysni yopish** → tasdiqlang.
3. **Farq dalolatnomasi** ni oching va chop eting.
4. demo_rahbar: **Qaytarishlar**, **Qoldiqlar**, **Qarzlar**, Anvar akt sverki, rahbar boti.
**Kutilgan natija:**
- 1-qadam: blokda: Sement — Jo'natilgan 80, Yetkazildi 70, Yetkazilmagan 10, "Omborga qaytdi" (standart 10). 11 kiritilganda "Qaytgan miqdor yetkazilmagandan (10) ko'p bo'lmasin."
- 2-qadam: reys **Yopildi**. Yangi qaytarish DQ — DEMO Anvar do'koni, 9 qop × 62 000 = **558 000**, sabab "Mijoz rad etdi". Kamomad 1 qop: mijozga tuzatish krediti **62 000**, reysning "Kamomad tannarxi" **40 000**.
- 3-qadam: dalolatnomada manzillar (Anvar — Qisman, 4 613 600; Bunyodkor — Yetkazildi, 1 812 500), Sement: Yetkazilmagan 10, Omborga qaytgan (qaytarish hujjati) 9, reys kamomadi 1; Kamomad tannarxi 40 000; Reys xarajatlari 150 000.
- 4-qadam: Qoldiq S **757**. Anvar qarzi 13 613 600 − 4 000 000 − 558 000 − 62 000 = **8 993 600**; akt sverkida "Qaytarish 558 000" va "Tuzatish 62 000" (izoh "R-…: reys kamomadi"). M1 bo'yicha qarz = 5 233 600 − 558 000 − 62 000 = 4 613 600 = yetkazilgan summa.
- Rahbarga: "🏁 Reys R-… yopildi: yetkazildi 2/2, 6 426 100 so'm; omborga qaytdi 558 000 so'm, kamomad 62 000 so'm; xarajat 150 000 so'm." (1 812 500 + 4 613 600 = 6 426 100).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

> Endi T-DST-BOT-33 (ekspeditor naqdini topshirish va kassir qabuli) ni bajaring.

### T-DST-DLV-09. 2-reys: web'dan yo'lga chiqarish, ofis natijasi va uni bekor qilish
**Nima tekshiriladi:** Reys ofisdan boshlanadi (mijozga bitta umumlashgan xabar); ofis natija kiritadi (rasm/imzo talab qilinmaydi), qisman/radda sabab majburiy; natija reys yopilguncha bekor qilinadi.
**Kim:** demo_rahbar, mijoz telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-08, T-DST-BOT-33
**Qadamlar:**
1. **Yangi reys**: 01 A 123 AA, DEMO Sherzod, DEMO Asosiy ombor; F va P1 jo'natishlari. **Saqlash**.
2. **Yo'lga chiqarish** ni bosing.
3. P1 manzilida natija kiritish: "Qisman" — sababsiz saqlashga urining; so'ng "Rad etildi", Sabab = `Noto'g'ri tovar` → **Saqlash**.
4. P1 qatorida **Natijani bekor qilish** (↺).
**Kutilgan natija:**
- 1-qadam: og'irlik **3600 kg** (2 500 + 1 100, 72%), summa **8 040 000**, 2-reys `R-…` Rejalandi.
- 2-qadam: "Yo'lda"; Anvarga bitta xabar: "🚚 Buyurtmangiz yo'lga chiqdi: D-… , D-… (8 040 000 so'm). Ekspeditor: DEMO Sherzod, +998904444444 Mashina: 01 A 123 AA".
- 3-qadam: "Qisman yetkazish yoki rad uchun sababni tanlang."; "Rad etildi" rasmsiz saqlanadi (ofis — "Ofisdan kiritilganda rasm/imzo talab qilinmaydi"). Anvarga "🚫 Buyurtma D-… qabul qilinmadi. Sabab: Noto'g'ri tovar."
- 4-qadam: P1 yana "Kutmoqda". (Endi T-DST-BOT-34 ni bajaring.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-10. Agent va ekspeditor KPI
**Nima tekshiriladi:** Oy bo'yicha sof savdo (Savdo hisoboti bilan bir xil), % savdo, reja bajarilsa bonus va rejadan ortig'iga %, reys kamomadidan jarima, qo'shimcha bonus; HR'ga o'tkazish uchun xodim majburiy.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-34
**Qadamlar:**
1. **Distributsiya → Agent KPI**, Oy = joriy oy → **Ko'rsatish**.
2. DEMO Jasur → **Sxema**: HR xodimi — bo'sh, % savdodan = `2`, Oylik reja = `15000000`, Reja bajarilsa bonus = `500000`, Rejadan ortig'iga % = `1`, Tashrif rejasi min % = `0`, Bajarilmasa jarima = `0`, Muddati o'tgan qarzdan % = `0`, Reys kamomadidan % = `0`, Faol ✓. **Saqlash**.
3. DEMO Sherzod → **Sxema**: % savdodan = `0`, Reys kamomadidan % = `100`, qolganlari 0. **Saqlash**.
4. **Hisoblash**.
5. DEMO Jasur qatorida Qo'shimcha bonus = `100000`, Izoh = `DEMO` → **Saqlash**.
6. DEMO Jasur qatorida **Ish haqiga** ni bosing.
**Kutilgan natija:**
- 4-qadam (barcha amallar joriy oyda bajarilgan bo'lsa): DEMO Jasur — Sof savdo **19 362 000** (jo'natishlar 21 557 600 − qaytarishlar 2 195 600), Reja **129.08%**, % savdo 19 362 000 × 2% = **387 240**, Bonus 500 000 + (19 362 000 − 15 000 000) × 1% = **543 620**, Jarima 0, Jami **930 860**. DEMO Sherzod — Kamomad tannarxi 40 000, Jarima **40 000**.
- 5-qadam: Jasur Jami **1 030 860**.
- 6-qadam: "Agent HR xodimiga bog'lanmagan (KPI sxemasida xodimni tanlang)." (HR'da xodim va shu oyning qoralama ish haqi hisobi bo'lsa — ixtiyoriy: xodimni tanlab qayta o'tkazing va HR qatoriga mukofot/jarima qo'shilganini tekshiring.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-DLV-11. Tashrif rejasi (kunlik marshrut) va reja/fakt
**Nima tekshiriladi:** Mijoz kartasidagi tashrif kunlari ustiga kunlik reja: qo'shish, tartib, saqlash; botdagi marshrut shu tartibda; andozaga qaytish; reja/fakt jadvali va xarita ranglari.
**Kim:** demo_rahbar, agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-12
**Qadamlar:**
1. **Distributsiya → Tashrif rejasi**: Agent = DEMO Jasur, Sana = bugun. "Kun rejasi" (andoza — "Tashrif kunlaridan") ni ko'ring.
2. **Mijoz qo'shish…** → `DEMO Yangi nuqta`. Uni ↑ bilan birinchi qiling. **Saqlash**.
3. Agent: **🗺 Marshrut**.
4. **Andozaga qaytish** ni bosing. Agent: 🔄 Yangilash.
5. "Reja / fakt" jadvali va xaritani ko'ring; **Davr bo'yicha hisobot** ni oching.
**Kutilgan natija:**
- 1-qadam: rejada DEMO Anvar do'koni (andoza).
- 2-qadam: "Saqlangan reja": 1. DEMO Yangi nuqta, 2. DEMO Anvar do'koni.
- 3-qadam: "🗺 Bugungi marshrut: 2 ta mijoz, …" — tartib: DEMO Yangi nuqta, so'ng DEMO Anvar do'koni.
- 4-qadam: reja yana faqat Anvar; botda 1 ta (bugun tashrif buyurilganlar ro'yxat oxirida qoladi).
- 5-qadam: Rejada / Borildi / Rejadan tashqari / Borilmagan ustunlari; xaritada ko'k — kutilmoqda, yashil — borildi, binafsha — rejadan tashqari. Davr hisoboti Tashriflar hisobotini ochadi (T-DST-REP-05).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Hisobotlar va nazorat

Quyidagi raqamlar **barcha amallar joriy oyda va E2E dan oldin** bajarilgan deb hisoblangan. Davr filtrini UAT boshlangan kundan bugungacha qo'ying.

Nazorat jadvali (savdo, UAT davri):

| Hujjat | Jo'natilgan | Tannarx (FIFO) |
|---|---|---|
| A (DJ, qayta jo'natilgan) | 7 944 000 | 5 624 000 |
| F (qisman 100 list) | 6 800 000 | 5 000 000 |
| D | 340 000 | 250 000 |
| P1 (20 + 2 sovg'a) | 1 240 000 | 880 000 |
| M1 | 5 233 600 | 3 401 600 |
| C | 1 812 500 | 1 350 000 |
| **Jami jo'natish** | **23 370 100** | **16 505 600** |
| Qaytarishlar: kafel 2,88 m²; sement 2 qop (brak); 1-reys 9 qop; 2-reys 22 qop | −2 195 600 (273 600 + 124 000 + 558 000 + 1 240 000) | −1 521 600 (201 600 + 80 000 + 360 000 + 880 000) |
| **Tushum / sof tannarx** | **21 174 500** | **14 984 000** |
| **Yalpi foyda / marja** | **6 190 500** | **29.24%** |

Bekor qilingan birinchi A jo'natishi, bekor qilingan Bunyodkor qaytarishi va kamomad tuzatishi (62 000) tushumga kirmaydi.

### T-DST-REP-01. Rahbar paneli
**Nima tekshiriladi:** Bir ekranda tushum va yalpi foyda (kun / oy boshidan), yig'ilgan pul, qarz va muddatlar, kassa/bank qoldiqlari, agentlar qo'lidagi pul, qoldiq qiymati (FIFO), 14 kunlik grafik, buyurtmalar oqimi, agentlar reytingi; Excel (pilot N5).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-10
**Qadamlar:**
1. **Distributsiya → Rahbar paneli** (sana — bugun).
2. Sanani UAT birinchi kuniga o'zgartiring, keyin qaytaring.
3. **Excel** ni bosing.
**Kutilgan natija:**
- "Oy boshidan": Tushum **21 174 500**, Yalpi foyda **6 190 500** (marja 29,24%).
- Mijozlar qarzi: jami **7 753 600** (Anvar), muddati o'tgan 0 (Anvarning eng eski jo'natishidan 7 kun o'tmagan bo'lsa), avans 0; "Eng katta muddati o'tgan qarz" — "Muddati o'tgan qarz yo'q".
- Pul: DEMO Kassa **11 386 400**, DEMO Bank hisobi **6 812 500** (va kompaniyaning boshqa hisoblari). Agentlar qo'lida 0; "Topshirilgan, tasdiq kutmoqda" 0.
- Qoldiq qiymati (FIFO tannarxda, savdo omborlari) **87 303 600**: Asosiy — S 779 × 40 000 = 31 160 000, K 395,48 × 70 000 = 27 683 600, G 50 × 52 000 + 44 × 50 000 = 4 800 000; Filial — S 98 × 40 000 + 200 × 41 000 = 12 120 000, K 47 × 70 000 + 100 × 72 000 = 10 490 000, G 21 × 50 000 = 1 050 000. Brak ombori (savdoda emas) kirmaydi.
- "Jo'natish kutmoqda" 0, "Tasdiq kutmoqda" 0. Agentlar reytingi (oy boshidan): DEMO Jasur — 19 362 000.
- 2-qadam: "Bugun" kartalari o'sha kunning raqamlariga o'zgaradi; "… holatiga, yangilangan …" yozuvi.
- 3-qadam: Excel faylida Bo'lim / Ko'rsatkich / Qiymat ro'yxati.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-02. Savdo hisoboti: beshta kesim
**Nima tekshiriladi:** Agent, mijoz, tovar, hudud, kategoriya kesimlarida jo'natilgan, qaytarish, tushum, tannarx, yalpi foyda, marja va jami qatori bir xil (pilot N5).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-REP-01
**Qadamlar:**
1. **Hisobotlar → Savdo**, Boshlanish = UAT 1-kuni, Tugash = bugun. Yorliqlarni ketma-ket oching: Agent, Mijoz, Tovar, Hudud, Kategoriya.
**Kutilgan natija:**
- Har kesimda Jami: Jo'natilgan **23 370 100**, Qaytarishlar **2 195 600**, Tushum **21 174 500**, Tannarx **14 984 000**, Yalpi foyda **6 190 500**, Marja **29.24%**.
- Agent: DEMO Jasur — tushum 19 362 000, foyda 5 728 000, marja 29.58%; "(ko'rsatilmagan)" — 1 812 500, 462 500, 25.52% (Bunyodkor agentsiz).
- Mijoz: DEMO Anvar do'koni 19 362 000; DEMO Bunyodkor qurilish 1 812 500.
- Tovar: Sement — tushum 7 280 500 (9 202 500 − 1 922 000), sof miqdor 119 qop, foyda 2 520 500; Kafel — 5 054 000 (5 327 600 − 273 600), 53.2 m², foyda 1 330 000; Gipsokarton — 8 840 000, 130 list, foyda 2 340 000.
- Hudud: Chilonzor 19 362 000; Yunusobod 1 812 500.
- Kategoriya: DEMO Qurilish aralashmalari 16 120 500 (foyda 4 860 500); DEMO Kafel 5 054 000 (1 330 000).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-03. Drill-down va filtrlar
**Nima tekshiriladi:** Qatorni bosish keyingi kesimga filtr bilan o'tadi; agent, hudud (quyi hududlari bilan), kategoriya, ombor filtrlari.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-REP-02
**Qadamlar:**
1. Agent kesimida DEMO Jasur qatorini bosing. Keyin DEMO Anvar do'koni qatorini bosing.
2. **Filtrni olib tashlash**. Hudud filtri = Toshkent sh. (viloyat). Keyin Kategoriya = DEMO Kafel. Keyin Ombor = DEMO Filial ombor.
3. Qidiruv va ustun bo'yicha saralashni sinang.
**Kutilgan natija:**
- 1-qadam: Mijoz kesimi (agent filtri bilan) — faqat Anvar 19 362 000; so'ng Tovar kesimi Anvar uchun: Sement 6 758 000 (7 280 500 − Bunyodkor 522 500), Kafel 4 104 000 (5 054 000 − 950 000), Gipsokarton 8 500 000 (8 840 000 − 340 000).
- 2-qadam: Toshkent sh. — ikkala tuman (jami 21 174 500); DEMO Kafel — 5 054 000; DEMO Filial ombor — sotuv yo'q ("Bu davrda sotuv yo'q" / bo'sh).
- 3-qadam: saralash va qidiruv sahifani qayta yuklamaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-04. Excel eksport (joriy filtrlar bilan)
**Nima tekshiriladi:** Har hisobot Excel'ga joriy filtr bilan, barcha qatorlar bilan chiqadi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-REP-03
**Qadamlar:**
1. Savdo (Tovar kesimi, Kategoriya = DEMO Kafel) → **Excel**.
2. Tashriflar → **Excel**. Agentlar topshirgan pul → **Excel**.
**Kutilgan natija:**
- 1-qadam: faylda faqat Kafel va Kafel 2-nav (yoki faqat sotilgan Kafel) qatorlari, ustunlar ekrandagidek, jami 5 054 000.
- 2-qadam: fayllar ochiladi, ustunlar ekrandagi bilan bir xil.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-05. Tashriflar hisoboti (reja / fakt)
**Nima tekshiriladi:** Agent bo'yicha ish kunlari, reja, fakt, rejadagilar/rejadan tashqari, reja bajarilishi %, buyurtma bilan (samaradorlik %), to'lov bilan, buyurtmasiz, tashrif buyurtmalari summasi (pilot metrikasi ≥ 80%).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-11
**Qadamlar:**
1. **Hisobotlar → Tashriflar**, davr — UAT davri.
**Kutilgan natija:**
- DEMO Jasur: Ish kunlari = agent kun boshlagan kunlar soni; Reja = davrdagi Du–Sh kunlari × 1 (Anvar) (kunlik reja saqlangan kun uchun — o'sha reja); Fakt — kamida 2 (BOT kunidagi Anvar va DEMO Yangi nuqta), Rejadagilar ≥ 1, Rejadan tashqari ≥ 1, Buyurtmasiz 1, Buyurtma bilan ≥ 1; Reja bajarilishi % = Rejadagilar / Reja; Samaradorlik % = Buyurtma bilan / Fakt.
- Tashrif buyurtmalari summasi ≥ 5 233 600 (M1).
- Jami qatori agentlar yig'indisiga teng.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-06. Agentlar topshirgan pul
**Nima tekshiriladi:** Agent bo'yicha topshirgan, qabul qilingan, farq, kutilayotgan va hozir qo'lidagi naqd; topshirishlar ro'yxati holat filtri bilan (pilot Q3).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-33
**Qadamlar:**
1. **Hisobotlar → Agentlar topshirgan pul**, davr — UAT davri.
2. Holat filtri: Qabul qilindi; Rad etildi; Kutmoqda.
**Kutilgan natija:**
- DEMO Jasur: Topshirishlar 2, To'lovlar bo'yicha **1 600 000**, Sanalgan **1 590 000**, Farq **−10 000**, Kutmoqda 0, Agentlar qo'lida 0. DEMO Sherzod: 1, 4 000 000, 4 000 000, 0, 0, 0. (Rad etilgan 100 000 lik topshirish agent yig'indisiga kirmaydi.)
- Ro'yxat: Qabul qilindi — 3 ta (1 500 000→1 490 000, 100 000→100 000, 4 000 000→4 000 000); Rad etildi — 1 ta (100 000, sabab "DEMO: summa mos emas"); Kutmoqda — 0.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-07. Dala nazorati
**Nima tekshiriladi:** Sana bo'yicha agentlar: selfi, kun boshi/yakuni, tashriflar, buyurtmalar, so'nggi joylashuv; agent tafsiloti — tashriflar (masofa, natija, sabab, foto) va joylashuv izi xaritada (pilot A1–A6).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-15
**Qadamlar:**
1. **Distributsiya → Dala nazorati**, Sana = BOT bloki kuni.
2. DEMO Jasur → **Batafsil**. **Iz xaritada** ni bosing.
3. demo_agent bilan sahifani ochishga urining.
**Kutilgan natija:**
- 1-qadam: DEMO Jasur — selfi, Kun boshi, Kun yakuni, Tashriflar 2 (1 🚫), Buyurtmalar (M1 summasi bilan); DEMO Sherzod — ekspeditor, kun "boshlanmagan".
- 2-qadam: tashriflar jadvali (T-DST-BOT-12 dagidek) va joylashuv izi (kun boshi/yakuni, tashrif, jonli nuqtalar); xaritada 100 tagacha nuqta yoki Yandex havolasi.
- 3-qadam: "Sizga ruxsat berilmagan!".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-08. Xatolar jurnali: daraja, holat, yechim
**Nima tekshiriladi:** Web formadan muammo yozish; rahbar daraja (K1–K3) va holat qo'yadi; yopish uchun yechim majburiy; ochiq K1 soni sarlavhada.
**Kim:** demo_ombor (yozadi), demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-16
**Qadamlar:**
1. demo_ombor: **Xatolar jurnali** → "Xato yoki muammo yozish": Qayerda = `web: Qoldiqlar`, Hujjat raqami = bo'sh, Daraja — bo'sh, Nima qilindi va nima bo'ldi = `abc` → **Yuborish**; so'ng matn = `DEMO: qoldiqlar sahifasi sekin ochildi` → **Yuborish**.
2. demo_rahbar: P-001 ni Daraja = K1, Holat = Tuzatilmoqda → **Saqlash**. Sarlavhani ko'ring.
3. P-001: Holat = Yopildi, Yechim — bo'sh → **Saqlash**. So'ng Yechim = `DEMO: Mini App'da rasm keshi tozalandi` → **Saqlash**.
4. Qolgan DEMO muammolarni K3 / Yopildi (yechim bilan) qiling. "Ochiq" / "Hammasi" filtrlari.
**Kutilgan natija:**
- 1-qadam: `abc` — "Muammoni yozing (kamida 5 belgi)."; keyin yangi P-… (Yangi, "baholanmagan"), rahbarga botda "⚠️ Yangi muammo P-… (DEMO Omborchi): …".
- 2-qadam: sarlavhada ochiq K1 soni 1.
- 3-qadam: "Yopish uchun yechimni yozing."; yechim bilan — Yopildi, K1 soni 0.
- 4-qadam: "Ochiq" filtrida DEMO muammolar yo'q, "Hammasi" da bor.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-09. Audit va o'tkazilgan hujjatlar
**Nima tekshiriladi:** Har o'zgarish kim, qachon, eski → yangi qiymat bilan yoziladi; o'tkazilgan hujjat o'chirilmaydi — faqat sabab bilan bekor qilinadi (pilot N1, N2).
**Kim:** demo_rahbar, administrator (SQL)   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-14
**Qadamlar:**
1. Mijozlar → DEMO Anvar do'koni: Kredit limiti = `25000000` → Saqlash; so'ng yana `20000000` → Saqlash.
2. Administrator SSMS'da: `SELECT TOP 10 EntityName, EntityKey, Action, FieldName, OldValue, NewValue, UserID, Source, ChangedAt FROM dst.AUDIT_TRAIL WHERE FieldName = 'CreditLimit' ORDER BY ID DESC`.
3. Narxlar → DEMO-SEM-400 → **Narx tarixi**.
4. To'lovlar, Qaytarishlar, Hisobdan chiqarish ro'yxatlarida o'tkazilgan hujjatni o'chirish tugmasini qidiring.
**Kutilgan natija:**
- 2-qadam: ikki yozuv (Update): 20000000 → 25000000 va 25000000 → 20000000, UserID = demo_rahbar ID, Source = web, vaqt bilan.
- 3-qadam: har narx o'zgarishi sana, narx turi, foydalanuvchi bilan.
- 4-qadam: o'tkazilgan hujjatlarda faqat "Bekor qilish" (sabab bilan) — o'chirish yo'q. Izohga yozing: audit jurnali uchun web sahifa yo'q (faqat SQL / narx tarixi) — K3 taklifi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-10. Tannarx va foyda `DstSeeCost` huquqisiz ko'rinmaydi
**Nima tekshiriladi:** Tannarx, yalpi foyda, marja va qoldiq qiymati faqat `DstSeeCost` bilan — ekranda ham, Excel'da ham (pilot N4).
**Kim:** Admin (rol huquqi), demo_kassir, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-REP-02
**Qadamlar:**
1. Admin: **Rollar → DEMO-Kassir → Huquqlar**: DstReports va DstDashboard — View ✓ (vaqtincha). Saqlang.
2. demo_kassir: Hisobotlar → Savdo (Tovar kesimi) → **Excel**. Rahbar paneli.
3. demo_ombor: A buyurtmasi kartasi.
4. Admin: DEMO-Kassir huquqlaridan DstReports va DstDashboard ni olib tashlang.
**Kutilgan natija:**
- 2-qadam: Savdo jadvalida Tannarx (FIFO), Yalpi foyda, Marja ustunlari **yo'q** (Jo'natilgan, Qaytarishlar, Tushum, Buyurtmalar, Mijozlar, Miqdor bor); Excel'da ham yo'q. Rahbar panelida yalpi foyda va "Qoldiq qiymati" yo'q; kassa/bank qoldiqlari ko'rinadi (kassir cheklovsiz).
- 3-qadam: jo'natishlar jadvalida "Tannarx" ustuni va "Yalpi foyda" yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-REP-11. Davr filtri va bekor qilingan hujjatlar
**Nima tekshiriladi:** Hisobot hujjat sanasi bo'yicha; bekor qilingan jo'natish va qaytarish tushumga kirmaydi; kelajak davrda sotuv yo'q.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-REP-02
**Qadamlar:**
1. Savdo, Mijoz kesimi: davr = faqat ORD bloki kuni.
2. Davr = ertangi kun.
3. Mijoz kesimida DEMO Bunyodkor qurilish qatoridagi "Qaytarishlar" ni tekshiring.
**Kutilgan natija:**
- 1-qadam: Anvar jo'natilgani ORD kunidagi jo'natishlar: 7 944 000 + 6 800 000 + 340 000 + 1 240 000 = 16 324 000 (bekor qilingan birinchi A jo'natishi kirmaydi); qaytarishlar 397 600 (agar shu kunda bajarilgan bo'lsa).
- 2-qadam: "Bu davrda sotuv yo'q" / bo'sh jadval.
- 3-qadam: Bunyodkor qaytarishlari 0 (T-DST-ORD-22 dagi qaytarish bekor qilingan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Uchidan-uchiga ish kuni

### T-DST-E2E-01. Bir ish kuni: agent → ofis → ombor → reys → kassa
**Nima tekshiriladi:** `../03_PILOT.md` 3-bo'limdagi kunlik ssenariy haqiqiy rollar bilan: kun boshi, "Keldim", Mini App buyurtma (aksiya bilan), naqd to'lov, ofis tasdig'i, jo'natish, reys va yetkazish (PDF), reysni yopish, kassaga topshirish, kun yakuni, rahbar yakuni.
**Kim:** barcha rollar   **Oldin bajarilgan bo'lishi kerak:** barcha oldingi testlar (yaxshisi — yangi ish kuni)
**Qadamlar:**
1. demo_rahbar: Aksiyalar → DEMO Sement 10+1 → **Yoqish**.
2. Agent: **▶️ Kunni boshlash** → joylashuv → **⏭ O'tkazib yuborish** (selfi ixtiyoriy).
3. Agent: **🗺 Marshrut** → DEMO Anvar do'koni → **📍 Keldim** (`41.286000, 69.203400`).
4. Agent: **🛒 Buyurtma** → Sement birlik `qop` `15`; Kafel birlik `m²` `10` → Savat → yetkazish sanasi = bugun → imzo → **Buyurtma berish** (buyurtma **N**).
5. Agent: **💵 To'lov** → `1000000` → **Naqd**.
6. demo_rahbar: N kartasi → **Tahrirlash** → Ombor = DEMO Asosiy ombor → **Saqlash** → **Tasdiqlash**.
7. demo_ombor: N → **Jo'natish** → **Jo'natish**.
8. demo_rahbar: **Yangi reys** (01 A 123 AA, DEMO Sherzod) → N jo'natishi → **Saqlash**.
9. Ekspeditor: **🚚 Reys** → **▶️ Yo'lga chiqish** → manzil → **✅ Yetkazildi (rasm)** → rasm → **🏁 Reysni yopish** → **✅ Tasdiqlash**.
10. Agent: **💵 Kassa** → **📤 Kassaga topshirish** → **✅ Tasdiqlash**. Kassir: Agentlar naqdi va kassa → DEMO Jasur → **Qabul qilish** (DEMO Kassa, Sanalgan `1000000`).
11. Agent: **🏁 Kunni yakunlash** → joylashuv. Rahbar: **📊 Bugungi yakun**.
12. demo_rahbar: aksiyani **To'xtatish**.
**Kutilgan natija:**
- 2-qadam: "📸 Selfi yuboring yoki ⏭ O'tkazib yuborish tugmasini bosing." → ⏭ dan keyin "✅ Ish kuni boshlandi". (Agar kun shu kuni allaqachon boshlangan bo'lsa — selfi so'ralmaydi.)
- 4-qadam: katalogda Sement yonida "🏷 10+1"; savat jami 15 × 62 000 + 10 × 95 000 = **1 880 000**. Chatda "✅ Buyurtma D-… qabul qilindi: 1 880 000 so'm. Ofis tasdiqlaydi." N kartasida uchinchi qator — Sement **1 qop sovg'a** (narx 0). Limit xabari yo'q (7 753 600 + 1 880 000 < 20 000 000).
- 5-qadam: "Qo'lingizda naqd: 1 000 000 so'm"; Anvar qarzi 7 753 600 − 1 000 000 = **6 753 600**.
- 6-qadam: Tasdiqlangan, rezerv: Sement 15 + 1, Kafel 10. Qoldiq S **779/16/763**, K **395.48/10/385.48**. Omborchiga "📦 Yig'ish kerak …".
- 7-qadam: Jo'natildi, jo'natish **1 880 000**, tannarx 16 × 40 000 + 10 × 70 000 = **1 340 000** (yalpi foyda 540 000). Qoldiq S **763**, K **385.48**. Anvar qarzi **8 633 600**.
- 8-qadam: reys 970 kg (16 × 50 + 10 × 17), 1 manzil.
- 9-qadam: Anvarga "🚚 … yo'lga chiqdi …", so'ng "✅ Buyurtma D-… yetkazildi (DJ-…): 1 880 000 so'm. Nakladnoy ilova qilingan." + PDF; N **Yetkazildi**. Yopish: "🏁 Reys R-… yopildi. / Yetkazildi: 1, qisman: 0, rad: 0 / Yetkazilgan: 1 880 000 so'm, qaytdi: 0 so'm / Reysda yig'ilgan: 0 so'm, qo'lingizda: 0 so'm".
- 10-qadam: farq 0; DEMO Kassa 11 386 400 + 1 000 000 = **12 386 400**; agentlar qo'lida 0.
- 11-qadam: kun yakunida Yig'ilgan 1 000 000, Qo'lingizda naqd 0, Buyurtmalar 1 ta, 1 880 000 so'm; rahbar yakunida "Kun yetakchisi: DEMO Jasur — 1 880 000 so'm".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-E2E-02. Yakuniy nazorat: qoldiq = sanoq, qarz = akt sverki, kassa = pul
**Nima tekshiriladi:** TZ qabul mezonining ikkinchi qismi va pilot Q2–Q5: nazorat sanog'i farqsiz; mijoz qarzi = qarzlar sahifasi = akt sverki; kassa qoldig'i = sanalgan pul; agentlar qo'lida 0; ochiq K1 yo'q.
**Kim:** demo_ombor, demo_kassir, demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-E2E-01, T-DST-REP-08
**Qadamlar:**
1. demo_ombor: **Inventarizatsiya → Yangi sanoq**: DEMO Asosiy ombor, **Nazorat**. "Sanalgan" ga quyidagilarni kiriting (bu sinovda "haqiqiy sanoq" — kutilgan qoldiq): Sement `763`, Kafel `385.48`, Gipsokarton `94`, Kafel 2-nav `0` → **Saqlash** → "Farqni qoldiqqa yozish" belgisiz **Yakunlash**.
2. Xuddi shunday DEMO Filial ombor: Sement `298`, Kafel `147`, Gipsokarton `21`, Kafel 2-nav `0`.
3. demo_kassir: **Qarzlar** va DEMO Anvar do'koni akt sverki (joriy oy).
4. demo_kassir: **Moliya → Hisobotlar → Hisoblardagi qoldiq**; **To'lovlar → Agentlar naqdi va kassa**.
5. demo_rahbar: **Xatolar jurnali** sarlavhasi; **Rahbar paneli** (qoldiq qiymati).
**Kutilgan natija:**
- 1-qadam: "Tizimda" ustuni 763 / 385.48 / 94 / 0 — kiritilganga teng; yakunlangach **Farqlar: 0**.
- 2-qadam: **Farqlar: 0**.
- 3-qadam: Anvar qarzi **8 633 600** = akt sverki yakuniy qoldig'i 8 633 600 ("Mijozning qarzi"); Bunyodkor 0; DEMO Yangi nuqta 0.
- 4-qadam: DEMO Kassa **12 386 400**, DEMO Bank hisobi **6 812 500**; agentlar qo'lida naqd jadvali bo'sh.
- 5-qadam: ochiq K1 soni **0**. Qoldiq qiymati 87 303 600 − (16 × 40 000 + 10 × 70 000) = **85 963 600**.
- Hisob (nazorat): Anvar — 7 753 600 + 1 880 000 − 1 000 000 = 8 633 600; kassa — 7 536 400 (PAY) − 150 000 (yoqilg'i) + 4 000 000 (ekspeditor) + 1 000 000 (agent) = 12 386 400.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ma'lum cheklovlar va kuzatish kerak bo'lgan joylar
- Reysda "Yetkazildi" natijasi bekor qilinsa (↺), buyurtmaning "Yetkazildi" holati avtomatik qaytmaydi — natijani bekor qilish faqat noto'g'ri belgilangan "Rad etildi" uchun xavfsiz.
- Buyurtma kartasidagi qo'lda "Yetkazildi" reysdagi manzil holatini o'zgartirmaydi; reysga qo'yilgan buyurtmani faqat reys orqali yetkazing.
- Avtomatik ombor tanlashda ikki savdo ombori teng bo'lsa, ID kichigi tanlanadi (zona yoki masofa hisobga olinmaydi) — kerak bo'lsa omborni tasdiqlashdan oldin qo'lda tanlang (T-DST-E2E-01, 6-qadam).
- Marshrut uzunligi va tartiblash to'g'ri chiziq bo'yicha (eng yaqin qo'shni) — yo'l masofasi emas.
- KPI va hisobotlar hujjat sanasi bo'yicha; UAT oy chegarasidan o'tsa raqamlar oylarga bo'linadi.
- Audit jurnali (`dst.AUDIT_TRAIL`) uchun web sahifa yo'q.

---

## Yakuniy natijalar

Har qatorga natijani belgilang. "O'tmadi" bo'lsa — daraja (K1/K2/K3) va Xatolar jurnalidagi raqam (P-…).

| Kod | Test | O'tdi | O'tmadi | Daraja | P-№ | Izoh |
|---|---|---|---|---|---|---|
| T-DST-SET-01 | Modulga kirish, menyu va til | ☐ | ☐ | | | |
| T-DST-SET-02 | Rollar bo'yicha menyu va ruxsatsiz sahifalar | ☐ | ☐ | | | |
| T-DST-SET-03 | Narx turlari, brend va sabablar | ☐ | ☐ | | | |
| T-DST-SET-04 | Hududlar importi | ☐ | ☐ | | | |
| T-DST-SET-05 | Hududlar daraxti qoidalari | ☐ | ☐ | | | |
| T-DST-SET-06 | Umumiy sozlamalar | ☐ | ☐ | | | |
| T-DST-SET-07 | Agent to'lovlari | ☐ | ☐ | | | |
| T-DST-SET-08 | Chegirma limitlari | ☐ | ☐ | | | |
| T-DST-SET-09 | Omborlar sozlamalari | ☐ | ☐ | | | |
| T-DST-SET-10 | Tovarlar importi | ☐ | ☐ | | | |
| T-DST-SET-11 | Tovar birliklari importi | ☐ | ☐ | | | |
| T-DST-SET-12 | Tovar kartasi va shtrix-kod qoidalari | ☐ | ☐ | | | |
| T-DST-SET-13 | Narxlar importi | ☐ | ☐ | | | |
| T-DST-SET-14 | Narxlar jadvali, tarix, ommaviy o'zgartirish | ☐ | ☐ | | | |
| T-DST-SET-15 | Boshlang'ich kirim va qoldiqlar | ☐ | ☐ | | | |
| T-DST-SET-16 | Jamoa, agent, ekspeditor | ☐ | ☐ | | | |
| T-DST-SET-17 | Omborchining ko'rinish doirasi | ☐ | ☐ | | | |
| T-DST-SET-18 | Mijozni web'da yaratish | ☐ | ☐ | | | |
| T-DST-SET-19 | Mijoz kartasi tekshiruvlari | ☐ | ☐ | | | |
| T-DST-SET-20 | Mijozlar va marshrutlar importi | ☐ | ☐ | | | |
| T-DST-SET-21 | Kirish qarzlari importi | ☐ | ☐ | | | |
| T-DST-SET-22 | Maxsus narx | ☐ | ☐ | | | |
| T-DST-SET-23 | Supervayzer ko'rinishi | ☐ | ☐ | | | |
| T-DST-SET-24 | Xodimlar va mijozni Telegram'ga ulash | ☐ | ☐ | | | |
| T-DST-SET-25 | Ishlatilgan hududni o'chirib bo'lmaydi | ☐ | ☐ | | | |
| T-DST-ORD-01 | Web'da yangi buyurtma (A) | ☐ | ☐ | | | |
| T-DST-ORD-02 | Chegirma rol limiti, qoralamani o'chirish | ☐ | ☐ | | | |
| T-DST-ORD-03 | Minimal narx va narx turi qoidasi (C) | ☐ | ☐ | | | |
| T-DST-ORD-04 | Tasdiqlash va rezerv | ☐ | ☐ | | | |
| T-DST-ORD-05 | Minimal summa va o'zi olib ketish (D) | ☐ | ☐ | | | |
| T-DST-ORD-06 | Kredit limitidan oshish va rahbar ruxsati (E) | ☐ | ☐ | | | |
| T-DST-ORD-07 | Muddati o'tgan qarz bloki (C) | ☐ | ☐ | | | |
| T-DST-ORD-08 | "Kirimni kutmoqda" (F) | ☐ | ☐ | | | |
| T-DST-ORD-09 | Kirim va rezervni yangilash | ☐ | ☐ | | | |
| T-DST-ORD-10 | Ikki operator — oxirgi qoldiq | ☐ | ☐ | | | |
| T-DST-ORD-11 | Tasdiqni qaytarish va bekor qilish | ☐ | ☐ | | | |
| T-DST-ORD-12 | Yig'ish va yig'ish varaqasi | ☐ | ☐ | | | |
| T-DST-ORD-13 | To'liq jo'natish, FIFO, nakladnoy | ☐ | ☐ | | | |
| T-DST-ORD-14 | Jo'natishni bekor qilish va qayta jo'natish | ☐ | ☐ | | | |
| T-DST-ORD-15 | Qisman jo'natish | ☐ | ☐ | | | |
| T-DST-ORD-16 | Qolganidan voz kechish | ☐ | ☐ | | | |
| T-DST-ORD-17 | O'zi olib ketish | ☐ | ☐ | | | |
| T-DST-ORD-18 | "Yetkazildi" qo'lda | ☐ | ☐ | | | |
| T-DST-ORD-19 | Bekor qilish va jo'natilganni o'zgartirish taqiqi | ☐ | ☐ | | | |
| T-DST-ORD-20 | Qaytarish (yaroqli), ortiqcha rad | ☐ | ☐ | | | |
| T-DST-ORD-21 | Brak qaytarish | ☐ | ☐ | | | |
| T-DST-ORD-22 | Buyurtmasiz qaytarish va bekor qilish | ☐ | ☐ | | | |
| T-DST-ORD-23 | Aksiyalarni yaratish | ☐ | ☐ | | | |
| T-DST-ORD-24 | 10+1 sovg'a (P1) | ☐ | ☐ | | | |
| T-DST-ORD-25 | Kafel ≥ 100 m² −5% | ☐ | ☐ | | | |
| T-DST-ORD-26 | Aksiyalarni to'xtatish | ☐ | ☐ | | | |
| T-DST-ORD-27 | Ro'yxat filtrlari va ko'rinish doirasi | ☐ | ☐ | | | |
| T-DST-BOT-01 | Ulanish kodi chekka holatlari | ☐ | ☐ | | | |
| T-DST-BOT-02 | Rol cheklovlari va guruh chati | ☐ | ☐ | | | |
| T-DST-BOT-03 | Kun boshi, selfi majburiy | ☐ | ☐ | | | |
| T-DST-BOT-04 | Bugungi marshrut | ☐ | ☐ | | | |
| T-DST-BOT-05 | Mijoz qidirish va ko'rinish | ☐ | ☐ | | | |
| T-DST-BOT-06 | Mijoz kartasi va "Keldim" radiusi | ☐ | ☐ | | | |
| T-DST-BOT-07 | Mini App buyurtma (M1) | ☐ | ☐ | | | |
| T-DST-BOT-08 | Rahbar botda ruxsat, eskirgan tugma | ☐ | ☐ | | | |
| T-DST-BOT-09 | Naqd to'lov va takroriy bosish | ☐ | ☐ | | | |
| T-DST-BOT-10 | Karta to'lovi | ☐ | ☐ | | | |
| T-DST-BOT-11 | Yangi savdo nuqtasi va tasdiqlash | ☐ | ☐ | | | |
| T-DST-BOT-12 | Buyurtmasiz tashrif va foto | ☐ | ☐ | | | |
| T-DST-BOT-13 | Jonli joylashuv | ☐ | ☐ | | | |
| T-DST-BOT-14 | Agent naqdini topshirish | ☐ | ☐ | | | |
| T-DST-BOT-15 | Kunni yakunlash | ☐ | ☐ | | | |
| T-DST-BOT-16 | Muammo yozish | ☐ | ☐ | | | |
| T-DST-BOT-17 | Rahbar: bugungi va kunlik yakun | ☐ | ☐ | | | |
| T-DST-BOT-18 | Mijoz: xabarlar, qarzim, akt sverki | ☐ | ☐ | | | |
| T-DST-BOT-19 | Omborchi: Mini App'da yig'ish (M1) | ☐ | ☐ | | | |
| T-DST-BOT-20 | Omborchi: ko'r sanoq | ☐ | ☐ | | | |
| T-DST-BOT-21 | Rahbar: hisobdan chiqarish tasdiq/rad | ☐ | ☐ | | | |
| T-DST-BOT-22 | Ovozli buyurtma | ☐ | ☐ | | | |
| T-DST-BOT-23 | Mini App'da aksiya belgisi | ☐ | ☐ | | | |
| T-DST-BOT-24 | Oflayn va cheklov holatlari | ☐ | ☐ | | | |
| T-DST-BOT-25 | Rus tili | ☐ | ☐ | | | |
| T-DST-PAY-01 | Qarzlar va muddatlar | ☐ | ☐ | | | |
| T-DST-PAY-02 | Akt sverki | ☐ | ☐ | | | |
| T-DST-PAY-03 | Agent naqdini qabul qilish (kamomad) | ☐ | ☐ | | | |
| T-DST-PAY-04 | Topshirishni rad etish va qayta qabul | ☐ | ☐ | | | |
| T-DST-PAY-05 | Ofis to'lovi, muddati o'tgan qarz yopiladi | ☐ | ☐ | | | |
| T-DST-PAY-06 | To'lov tekshiruvlari va bekor qilish | ☐ | ☐ | | | |
| T-DST-PAY-07 | To'lov buyurtmani yopadi | ☐ | ☐ | | | |
| T-DST-PAY-08 | Avans | ☐ | ☐ | | | |
| T-DST-WH-01 | Qoldiqlar sahifasi | ☐ | ☐ | | | |
| T-DST-WH-02 | Boshlang'ich inventarizatsiya | ☐ | ☐ | | | |
| T-DST-WH-03 | Nazorat sanog'i, oyiga bir marta | ☐ | ☐ | | | |
| T-DST-WH-04 | Sanoq varag'i xato fayl | ☐ | ☐ | | | |
| T-DST-WH-05 | Ko'chirma va farq dalolatnomasi | ☐ | ☐ | | | |
| T-DST-WH-06 | Hisobdan chiqarish, qayta yuborish | ☐ | ☐ | | | |
| T-DST-WH-07 | O'z hujjatini tasdiqlash taqiqi | ☐ | ☐ | | | |
| T-DST-WH-08 | Rezerv himoyasi, o'tkazilganni bekor qilish | ☐ | ☐ | | | |
| T-DST-WH-09 | Pereso'rtitsa va bekor qilish | ☐ | ☐ | | | |
| T-DST-WH-10 | Landed cost | ☐ | ☐ | | | |
| T-DST-WH-11 | Minimal qoldiq ogohlantirishi | ☐ | ☐ | | | |
| T-DST-WH-12 | Buyurtma taklifi → SRM | ☐ | ☐ | | | |
| T-DST-DLV-01 | Mashina qo'shish | ☐ | ☐ | | | |
| T-DST-DLV-02 | Yetkazish sozlamalari | ☐ | ☐ | | | |
| T-DST-DLV-03 | Oldindan to'langan buyurtmani jo'natish | ☐ | ☐ | | | |
| T-DST-DLV-04 | Reys rejalash, sig'im | ☐ | ☐ | | | |
| T-DST-DLV-05 | Xarita, tartiblash, marshrut varag'i | ☐ | ☐ | | | |
| T-DST-DLV-06 | Rejalangan reysni bekor qilish | ☐ | ☐ | | | |
| T-DST-BOT-26 | Ekspeditor: reys xabari va ko'rinishi | ☐ | ☐ | | | |
| T-DST-BOT-27 | Yo'lga chiqish | ☐ | ☐ | | | |
| T-DST-BOT-28 | Natijasiz reysni yopib bo'lmaydi | ☐ | ☐ | | | |
| T-DST-BOT-29 | Yetkazildi (rasm), oldindan to'langan | ☐ | ☐ | | | |
| T-DST-BOT-30 | Imzo / qisman, nakladnoy PDF | ☐ | ☐ | | | |
| T-DST-BOT-31 | Manzilda to'lov | ☐ | ☐ | | | |
| T-DST-BOT-32 | Reys xarajati | ☐ | ☐ | | | |
| T-DST-DLV-07 | Reys xarajatlari moliyaga | ☐ | ☐ | | | |
| T-DST-DLV-08 | 1-reysni yopish, kamomad, dalolatnoma | ☐ | ☐ | | | |
| T-DST-BOT-33 | Ekspeditor naqdi va kassir qabuli | ☐ | ☐ | | | |
| T-DST-DLV-09 | 2-reys, ofis natijasi | ☐ | ☐ | | | |
| T-DST-BOT-34 | 2-reys: rad, yetkazildi, botdan yopish | ☐ | ☐ | | | |
| T-DST-DLV-10 | Agent va ekspeditor KPI | ☐ | ☐ | | | |
| T-DST-DLV-11 | Tashrif rejasi | ☐ | ☐ | | | |
| T-DST-REP-01 | Rahbar paneli | ☐ | ☐ | | | |
| T-DST-REP-02 | Savdo hisoboti: kesimlar | ☐ | ☐ | | | |
| T-DST-REP-03 | Drill-down va filtrlar | ☐ | ☐ | | | |
| T-DST-REP-04 | Excel eksport | ☐ | ☐ | | | |
| T-DST-REP-05 | Tashriflar hisoboti | ☐ | ☐ | | | |
| T-DST-REP-06 | Agentlar topshirgan pul | ☐ | ☐ | | | |
| T-DST-REP-07 | Dala nazorati | ☐ | ☐ | | | |
| T-DST-REP-08 | Xatolar jurnali | ☐ | ☐ | | | |
| T-DST-REP-09 | Audit va o'tkazilgan hujjatlar | ☐ | ☐ | | | |
| T-DST-REP-10 | Tannarx ko'rinishi DstSeeCost'siz | ☐ | ☐ | | | |
| T-DST-REP-11 | Davr filtri va bekor qilingan hujjatlar | ☐ | ☐ | | | |
| T-DST-E2E-01 | Uchidan-uchiga ish kuni | ☐ | ☐ | | | |
| T-DST-E2E-02 | Yakuniy nazorat | ☐ | ☐ | | | |

**Yig'indi:** jami testlar — 130; o'tdi — ____; o'tmadi — ____ (K1 — ____, K2 — ____, K3 — ____).

**Qaror:** ☐ Qabul qilindi (ochiq K1 yo'q, T-DST-E2E-02 o'tdi) ☐ Qabul qilinmadi — sabab: ____________

Tekshirdi: ____________ (F.I.Sh, sana, imzo)   Tasdiqladi: ____________ (rahbar)
