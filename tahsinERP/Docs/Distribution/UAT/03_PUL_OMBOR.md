# 3. Pul va ombor

Bu bo'lim ikki qismdan iborat:
- **T-DST-PAY** — to'lovlar (ofis, agent), agent naqdini kassada sanab qabul qilish (kamomad bilan) va rad etish, qarz muddatlari (aging), akt sverki, to'lovni bekor qilish, to'lov buyurtmani yopishi, avans.
- **T-DST-WH** — ombor kengaytmasi: qoldiqlar, boshlang'ich va nazorat inventarizatsiyasi (oyiga bir marta qoidasi), ko'chirmani farq dalolatnomasi bilan qabul qilish, hisobdan chiqarish rahbar tasdig'i bilan, pereso'rtitsa, landed cost (sotilgan qism bilan) va bekor qilish, minimal qoldiq ogohlantirishi, buyurtma taklifi → SRM xarid buyurtmasi.

**Bu blok T-DST-BOT-01 … 25 dan keyin bajariladi** (agent naqdi botda yig'ilgan). Testlarni T-DST-PAY-01 … 08, keyin T-DST-WH-01 … 12 tartibida bajaring.

Boshlang'ich holat (BOT-01…25 dan keyin):
- DEMO Anvar do'koni qarzi **19 160 000** (muddati kelmagan); DEMO Bunyodkor qurilish **5 000 000** (muddati o'tgan).
- Agent DEMO Jasur topshirgan, kassir qabul qilmagan naqd: **1 500 000** (DK-…, T-DST-BOT-14).
- DEMO Asosiy ombor: S **858/10/848**, K **455.36/10/445.36**, G **125/5/120**. DEMO Brak ombor: Sement 2. DEMO Filial ombor: bo'sh.
- FIFO qatlamlari (Asosiy): Sement — boshlang'ich kirim 40 000; Kafel — boshlang'ich kirim 70 000 (452,48 qolgan) + qaytarish 2,88 × 70 000; Gipsokarton — boshlang'ich kirim 75 × 50 000 + 2-partiya 50 × 52 000.
- Moliya: DEMO Kassa 0, DEMO Bank hisobi 500 000 (agentning karta to'lovi).

---

## To'lovlar va qarz

### T-DST-PAY-01. Qarzlar sahifasi: muddatlar bo'yicha taqsimot
**Nima tekshiriladi:** Mijoz qarzi real vaqtda = kirish qarzi + jo'natishlar − to'lovlar − qaytarishlar; muddatlar: muddati kelmagan, 1–7, 8–30, 31–60, 60+, avans (pilot D1, D5).
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-19
**Qadamlar:**
1. **Distributsiya → Qarzlar** ni oching. DEMO qatorlarini toping.
2. Filtrlarni sinang: Agent = DEMO Jasur; "Muddati o'tgan"; "Limitdan oshgan".
**Kutilgan natija:**
- DEMO Anvar do'koni: Qarz **19 160 000**, "Muddati kelmagan" 19 160 000, boshqa ustunlar 0, Kredit limiti 20 000 000.
- DEMO Bunyodkor qurilish: Qarz **5 000 000**, "8–30" ustunida 5 000 000 (kirish qarzining muddati 10+ kun oldin o'tgan), Muddati o'tgan 5 000 000.
- DEMO Yangi nuqta — qarz 0 (ro'yxatda yo'q yoki 0).
- Agent filtri: faqat Anvar (va Yangi nuqta). "Muddati o'tgan": faqat Bunyodkor. "Limitdan oshgan": DEMO mijozlar yo'q (19 160 000 < 20 000 000).
- Agar UAT 7 kundan uzoq davom etgan bo'lsa, Anvarning eng eski jo'natishlari "1–7" ustuniga o'tadi — Izohga yozing.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-02. Akt sverki (ekran va chop etish)
**Nima tekshiriladi:** Akt sverki davr boshidagi qoldiq, har hujjat (bekor qilingani teskari yozuv bilan) va yakuniy qoldiqni ko'rsatadi; yakuniy qoldiq = qarzlar sahifasi (pilot D1, D6).
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-01
**Qadamlar:**
1. Qarzlar → DEMO Anvar do'koni → **Akt sverki**. Boshlanish = joriy oyning 1-kuni, Tugash = bugun → **Qo'llash**.
2. **Chop etish** ni bosing (PDF ga saqlang).
3. DEMO Bunyodkor qurilish akt sverkini xuddi shu davr uchun oching.
**Kutilgan natija:**
- Anvar: Boshlang'ich qoldiq 0. Yozuvlar (sana tartibida, Turi / Raqam / Debet / Kredit / Qoldiq):
  Jo'natish DJ (A) 7 944 000 → 7 944 000; Jo'natish bekor qilindi 7 944 000 (kredit) → 0; Jo'natish DJ (A) 7 944 000 → 7 944 000; Jo'natish DJ (F) 6 800 000 → 14 744 000; Jo'natish DJ (D) 340 000 → 15 084 000; Qaytarish DQ 273 600 → 14 810 400; Qaytarish DQ 124 000 → 14 686 400; Jo'natish DJ (P1) 1 240 000 → 15 926 400; To'lov DP 1 500 000 → 14 426 400; To'lov DP 500 000 → 13 926 400; Jo'natish DJ (M1) 5 233 600 → 19 160 000.
- Jami: Debet **29 501 600**, Kredit **10 341 600**, Yakuniy qoldiq **19 160 000** — "Mijozning qarzi" (= Qarzlar sahifasi).
- Chop etish varag'ida xuddi shu jadval, mijoz STIR va imzo joylari.
- Bunyodkor: Kirish qoldig'i 5 000 000 → 5 000 000; Qaytarish 68 000 → 4 932 000; Qaytarish bekor qilindi 68 000 (debet) → 5 000 000; Yakuniy **5 000 000**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-03. Agent naqdini kassada qabul qilish (kamomad bilan)
**Nima tekshiriladi:** Kassir agent topshirgan pulni sanaydi; har to'lov kassaga yoziladi, farq alohida chiqim — kassa qoldig'i haqiqiy pulga teng; agent qo'lida 0 (pilot D2, Q3).
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-14
**Qadamlar:**
1. **To'lovlar → Agentlar naqdi va kassa** ni oching.
2. DEMO Jasur qatorida **Qabul qilish**: Kassa / hisob = DEMO Kassa, Sanalgan = `1490000`, Izoh = `DEMO: 10 000 yetmadi`. Tasdiqlang.
3. **Moliya → Boshqaruv → Tranzaksiyalar** (bugun) va **Moliya → Hisobotlar → Hisoblardagi qoldiq**.
**Kutilgan natija:**
- 1-qadam: "Agentlar qo'lidagi naqd pul" jadvalida DEMO Jasur — 1 500 000, eng eski to'lov sanasi, "topshirdi, kutmoqda".
- 2-qadam oynasida: To'lovlar bo'yicha 1 500 000, Farq **−10 000** va eslatma "Kamomad agent hisobida qoladi va kassada chiqim sifatida, ortiqcha — kirim sifatida yoziladi." Qabul qilingach Jasur qatori yo'qoladi; "Kassaga topshirishlar" jadvalida DK — 1 500 000 / 1 490 000 / Farq −10 000, holat "Qabul qilindi".
- 3-qadam: DEMO Kassa'ga kirim **1 500 000** ("Distributsiya to'lovi DP-…", kontragent DEMO Anvar do'koni) va chiqim **10 000** ("Kamomad: DK-…", kategoriya "Agent kassa farqi"). DEMO Kassa qoldig'i **1 490 000**.
- Anvar qarzi o'zgarmaydi (19 160 000) — to'lov agent olgan paytda yozilgan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-04. Topshirishni rad etish va qayta qabul qilish
**Nima tekshiriladi:** Rad etilgan topshirishning to'lovlari yana agent qo'lida; kassir agent kelganda topshirish va qabulni bitta amalda bajaradi.
**Kim:** agent telefoni, demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-03
**Qadamlar:**
1. Agent: **🔎 Mijoz** → Anvar → **💵 To'lov** → `100000` → **Naqd**. So'ng **💵 Kassa** → **📤 Kassaga topshirish** → **✅ Tasdiqlash**.
2. Kassir: Agentlar naqdi va kassa → DEMO Jasur → **Rad etish** → sabab `DEMO: summa mos emas`.
3. Agent: **💵 Kassa**.
4. Kassir: DEMO Jasur → **Qabul qilish**: DEMO Kassa, Sanalgan = `100000`.
**Kutilgan natija:**
- 1-qadam: "✅ To'lov DP-… qabul qilindi: 100 000 so'm. Qo'lingizda naqd: 100 000 so'm."; "✅ Topshirish DK-…: 100 000 so'm. …". Anvar qarzi **19 060 000**.
- 2-qadam: topshirish "Rad etildi"; Jasur qatori qoladi — 100 000, "kutmoqda" belgisisiz.
- 3-qadam: "💵 Qo'lingizdagi naqd: 100 000 so'm." va 📤 tugmasi.
- 4-qadam: yangi DK — 100 000 / 100 000 / farq 0, "Qabul qilindi". DEMO Kassa qoldig'i **1 590 000**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-05. Ofisdagi to'lov: pul o'tkazmasi, muddati o'tgan qarz yopiladi
**Nima tekshiriladi:** Ofis to'lovi darhol tanlangan hisobga tushadi; kreditlar eng eski qarzni yopadi (FIFO) va muddati o'tgan qarz yo'qoladi.
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-01
**Qadamlar:**
1. **Qarzlar** → DEMO Bunyodkor qurilish qatorida **To'lov qabul qilish**: Summa = `5000000`, To'lov usuli = Pul o'tkazmasi, Kassa / hisob = DEMO Bank hisobi, Izoh = `DEMO: kirish qarzi to'landi`. **To'lov qabul qilish**.
2. **Qarzlar** va Moliya tranzaksiyalarini tekshiring.
**Kutilgan natija:**
- Bunyodkor: Qarz **0**, "8–30" va Muddati o'tgan 0.
- Moliyada DEMO Bank hisobiga kirim 5 000 000 (kategoriya "Sotuv daromadi", kontragent DEMO Bunyodkor qurilish). DEMO Bank hisobi qoldig'i **5 500 000**.
- To'lovlar ro'yxatida DP — Pul o'tkazmasi, Qabul qildi DEMO Kassir, hisob DEMO Bank hisobi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-06. To'lov tekshiruvlari va to'lovni bekor qilish
**Nima tekshiriladi:** Naqdsiz to'lov hisobsiz qabul qilinmaydi; xato to'lov sabab bilan bekor qilinadi (moliya yozuvi o'chadi, qarz tiklanadi); kassaga topshirilgan agent puli bekor qilinmaydi.
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-04
**Qadamlar:**
1. Qarzlar → DEMO Anvar do'koni → **To'lov qabul qilish**: Summa = `0` → saqlang.
2. Summa = `100000`, usul = Karta, Kassa / hisob = "Agent qo'lida (naqd)" → saqlang.
3. Oynani yoping. **To'lovlar** ro'yxatida agentning **Karta 500 000** to'lovini toping → **Bekor qilish** → sabab `DEMO: karta to'lovi qaytarildi`.
4. Agentning **Naqd 1 500 000** (kassaga topshirilgan) to'lovini bekor qilishga urining.
5. **Qarzlar** va Moliya tranzaksiyalarini tekshiring.
**Kutilgan natija:**
- 1-qadam: "Summa 0 dan katta bo'lsin."
- 2-qadam: "Kassa yoki hisobni tanlang (naqddan boshqa to'lov darhol hisobga tushadi)."
- 3-qadam: to'lov "Bekor qilingan"; Moliyada uning kirim yozuvi o'chirilgan; DEMO Bank hisobi **5 000 000**.
- 4-qadam: "Bu to'lov kassaga topshirilgan — bekor qilib bo'lmaydi."
- Anvar qarzi 19 060 000 + 500 000 = **19 560 000**. Akt sverkida "To'lov bekor qilindi 500 000" (debet) yozuvi paydo bo'ladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-07. To'lov buyurtmani yopadi ("Yopildi")
**Nima tekshiriladi:** To'lovlar va qaytarishlar eng eski jo'natishni FIFO bo'yicha qoplaydi; qarzi to'liq yopilgan yetkazilgan buyurtma "Yopildi" holatiga o'tadi (pilot B6).
**Kim:** demo_kassir, demo_rahbar (buyurtma kartasini ko'rish)   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-06
**Qadamlar:**
1. Hisoblab qo'ying: Anvarning eng eski ochiq jo'natishi — A (7 944 000). Unga tushgan kreditlar: qaytarishlar 273 600 + 124 000 + naqd to'lovlar 1 500 000 + 100 000 = 1 997 600. A bo'yicha qolgan qarz = 7 944 000 − 1 997 600 = **5 946 400**.
2. Qarzlar → Anvar → **To'lov qabul qilish**: Summa = `5946400`, usul = Naqd, Kassa / hisob = DEMO Kassa.
3. demo_rahbar: A, D, F kartalarini oching.
**Kutilgan natija:**
- A holati **Yopildi** (tarixda "Yetkazildi → Yopildi"). D (Yetkazildi) va F (Jo'natildi) holati o'zgarmagan — ularning qarzi hali ochiq.
- Anvar qarzi 19 560 000 − 5 946 400 = **13 613 600** (F 6 800 000 + D 340 000 + P1 1 240 000 + M1 5 233 600).
- DEMO Kassa qoldig'i 1 590 000 + 5 946 400 = **7 536 400**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-PAY-08. Avans (oldindan to'lov)
**Nima tekshiriladi:** Qarzidan ortiq to'lov avans sifatida ko'rinadi; akt sverkida "Mijozning avansi" (T-DST-DLV-03 da shu avans jo'natish bilan yopiladi).
**Kim:** demo_kassir   **Oldin bajarilgan bo'lishi kerak:** T-DST-PAY-05
**Qadamlar:**
1. Qarzlar → DEMO Bunyodkor qurilish → **To'lov qabul qilish**: Summa = `1812500` (C buyurtmasi summasi), usul = Payme, Kassa / hisob = DEMO Bank hisobi, Izoh = `DEMO: C uchun oldindan to'lov`.
2. **Qarzlar** (filtrsiz) va Bunyodkor akt sverkini oching.
**Kutilgan natija:**
- Qarzlar: Bunyodkor — Qarz 0, "Avans (oldindan to'lov)" **1 812 500**.
- Akt sverki: yakuniy qoldiq **−1 812 500**, yozuv "Mijozning avansi".
- DEMO Bank hisobi **6 812 500**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ombor

### T-DST-WH-01. Qoldiqlar sahifasi: haqiqiy / rezerv / mavjud
**Nima tekshiriladi:** Qoldiqlar ombor bo'yicha va jami ko'rinadi; mavjud = haqiqiy − rezerv; filtrlar (pilot B5).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-21
**Qadamlar:**
1. **Distributsiya → Qoldiqlar**: Ombor = DEMO Asosiy ombor, "Qoldig'i borlar".
2. Ombor = "Barcha omborlar".
3. Filtrlar: "Minimaldan past", "Rezerv qoldiqdan ko'p".
**Kutilgan natija:**
- 1-qadam: Sement 858 / 10 / 848 (qop), Kafel 455.36 / 10 / 445.36 (m²), Gipsokarton 125 / 5 / 120 (list). Kafel 2-nav yo'q.
- 2-qadam: Sement **860** (Asosiy 858 + Brak 2), qolganlari o'zgarmagan.
- 3-qadam: ikkala filtr bo'sh (minimal qoldiq hali qo'yilmagan, rezerv qoldiqdan oshmagan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-02. Boshlang'ich inventarizatsiya (Excel varaq, tannarx ustuni)
**Nima tekshiriladi:** Boshlang'ich sanoq qoldiqni sanalganga tenglaydi; varaqda tizim qoldig'i yo'q ("ko'r" sanoq); kirim tarixi yo'q tovarga tannarx ustunidan narx olinadi; hamma qator sanalishi shart (pilot P9).
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-17
**Qadamlar:**
1. **Bosh sahifa → Inventarizatsiya → Yangi sanoq**: Ombor = DEMO Filial ombor, Turi = Boshlang'ich, Izoh = `DEMO Samarqand boshlang'ich`.
2. Sanoq kartasida **Sanoq varag'i (Excel)** ni yuklab oling. Ustunlarni ko'ring.
3. Varaqda: DEMO-SEM-400 Sanalgan `200`, Tannarx `41000`; DEMO-KAF-3030 `100`, `72000`; DEMO-KAF-3030-2 `0`; DEMO-GIP-125 — **bo'sh**. Faylni **Yuklash**.
4. **Yakunlash va qoldiqni tenglash** ni bosing.
5. Gipsokarton "Sanalgan" = `0` → **Saqlash** → **Yakunlash va qoldiqni tenglash** → tasdiqlang.
6. **Qoldiqlar** (Filial) va ombor modulidagi **Kirimlar** (`INV` bo'yicha).
**Kutilgan natija:**
- 2-qadam: ustunlar Artikul, Nomi, Birlik, Sanalgan, Tannarx — tizim qoldig'i **yo'q**. Qatorlar: 4 ta sotuvdagi DEMO tovar.
- 4-qadam: "1 ta qator sanalmagan. Yo'q tovar uchun 0 yozing."
- 5-qadam: "Sanoqni yakunlaysizmi? Keyin o'zgartirib bo'lmaydi." → holat "O'tkazilgan", "qoldiqqa yozilgan"; Farqlar **2** (ikkalasi ortiqcha: +200, +100).
- 6-qadam: Filial: Sement **200**, Kafel **100**. Ombor modulida INV kirimi: 200 × 41 000 = 8 200 000 va 100 × 72 000 = 7 200 000 (jami 15 400 000).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-03. Nazorat sanog'i va "oyiga bir marta" qoidasi
**Nima tekshiriladi:** Nazorat sanog'i tizim bilan taqqoslaydi; farqni qoldiqqa yozish shu ombor uchun oyiga bir marta; yozmasdan yakunlash mumkin (pilot Q2).
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-02
**Qadamlar:**
1. Yangi sanoq: Filial, Turi = **Nazorat**. Kartada "Sanalgan" ni ekranda kiriting: Sement `199`, Kafel `100`, Gipsokarton `0`, Kafel 2-nav `0` → **Saqlash**.
2. "Farqni qoldiqqa yozish" ✓ → **Yakunlash**.
3. Belgini olib **Yakunlash**.
4. Filial qoldiqlarini tekshiring.
**Kutilgan natija:**
- 1-qadam: "Tizimda" ustuni 200 / 100 / 0 / 0, "Farq" Sement −1.
- 2-qadam: "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi." Sanoq qoralama bo'lib qoladi.
- 3-qadam: "O'tkazilgan" (qoldiqqa yozilmagan), Farqlar **1** (kamomad −1).
- 4-qadam: Filial Sement **200** (o'zgarmagan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-04. Sanoq varag'ini xato fayl bilan yuklash
**Nima tekshiriladi:** Varaqda noma'lum artikul yoki takror qator bo'lsa hech narsa yozilmaydi; ombordagi ochiq sanoq bo'lsa yangisi ochilmaydi.
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-01
**Qadamlar:**
1. Yangi sanoq: DEMO Asosiy ombor, Nazorat. Varaqni yuklab oling.
2. Varaqqa qo'shimcha qatorlar yozing: `DEMO-YOQ` | | | `5` va ikkinchi `DEMO-SEM-400` qatori (Sanalgan `1`). Birinchi DEMO-SEM-400 qatoriga `858`. **Yuklash**.
3. Yana **Yangi sanoq** → DEMO Asosiy ombor.
4. Ochiq sanoqni **Bekor** → sabab `DEMO: fayl sinovi`.
**Kutilgan natija:**
- 2-qadam: xatolar — "Topilmadi: DEMO-YOQ" va "Faylda takrorlangan: DEMO-SEM-400"; hech bir "Sanalgan" qiymati yozilmagan.
- 3-qadam: "Bu omborda ochiq (qoralama) sanoq bor — avval uni yakunlang yoki bekor qiling."
- 4-qadam: sanoq "Bekor qilingan", qoldiq o'zgarmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-05. Ko'chirmani haqiqiy miqdor bilan qabul qilish va farq dalolatnomasi
**Nima tekshiriladi:** Boshqa ombordan kelgan tovar har qator bo'yicha qabul qilinadi: kamomad — yo'qotish yoki jo'natuvchiga qaytarish, ortiqcha — kirimga; qabul — faqat ombor mas'uli; ikki marta qabul yo'q; rahbarga farq xabari.
**Kim:** demo_ombor (yuboradi va Filial mas'uli sifatida qabul qiladi), demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-03
**Qadamlar:**
1. demo_ombor: **Omborxona → Ombor → Operatsiyalar → Chiqimlar → Yangi chiqim**: Yuboruvchi ombor = DEMO Asosiy ombor, Qabul qiluvchi ombor = DEMO Filial ombor; qatorlar DEMO-SEM-400 `100`, DEMO-KAF-3030 `50`, DEMO-GIP-125 `20`. Saqlang. **Distributsiya → Qoldiqlar** (Asosiy va Filial).
2. demo_rahbar: **Distributsiya → Ko'chirmalarni qabul qilish** → kutilayotgan qatorda **Qabul qilish** → miqdorlarni o'zgartirmasdan **Qabul qilish**.
3. demo_ombor: shu ko'chirmani oching. Kelgan: Sement `98` (Kamomad = Yo'qotish), Kafel `47` (Kamomad — tanlanmagan), Gipsokarton `21`. **Qabul qilish**.
4. Kafel uchun Kamomad = "Jo'natuvchiga qaytarish" → **Qabul qilish** → tasdiqlang.
5. Dalolatnomani oching va **Chop etish**. Brauzerda "Orqaga" bilan qabul sahifasiga qaytib yana **Qabul qilish** ni bosing.
6. **Qoldiqlar** (Asosiy, Filial), rahbar boti.
**Kutilgan natija:**
- 1-qadam: Asosiy darhol S 758, K 405.36, G 105 (haqiqiy); Filial o'zgarmagan (kirim "Kutilmoqda").
- 2-qadam: "Qabul qilishni faqat omborning mas'uli (yoki Admin) bajaradi." (demo_rahbar Filial mas'uli emas).
- 3-qadam: "DEMO-KAF-3030 Kafel 30×30: kamomadni nima qilishni tanlang." — hech narsa yozilmaydi.
- 4-qadam: "Ko'chirma qabul qilindi." Dalolatnoma DT-…: Sement 100 → 98, farq −2 × 40 000 = **80 000** (Yo'qotish); Kafel 50 → 47, −3 × 70 000 = **210 000** (Jo'natuvchiga qaytarish); Gipsokarton 20 → 21, +1 × 50 000 = **50 000**. Kamomad summasi **290 000**, Shundan yo'qotish **80 000**, Ortiqcha summasi **50 000**.
- 5-qadam: chop etishda "Topshirdi / Qabul qildi" imzo joylari. Takroriy qabul: "Ko'chirma qabul kutmayapti (allaqachon qabul qilingan yoki o'chirilgan)."
- 6-qadam: Asosiy: S **758**, K **408.36** (405,36 + 3 qaytgan), G **105**. Filial: S **298**, K **147**, G **21**. Rahbarga: "📦 Ko'chirma farqi DT-… (INC-…): DEMO Asosiy ombor → DEMO Filial ombor Kamomad: 290 000 (yo'qotish 80 000), ortiqcha: 50 000".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-06. Hisobdan chiqarish: web'da tasdiqlash, o'zgartirilsa qayta yuborish
**Nima tekshiriladi:** Qoralama → tasdiqqa yuborish → rahbar tasdig'i: FIFO tannarxda ombor chiqimi; yuborilgan hujjat o'zgartirilsa qayta yuboriladi.
**Kim:** demo_ombor, demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-05
**Qadamlar:**
1. demo_ombor: **Hisobdan chiqarish → Yangi**: Asosiy, Sabab = Sinish, DEMO-KAF-3030 `2.88`, Izoh = `DEMO: 2 quti singan`. Saqlang → **Tasdiqqa yuborish**.
2. demo_ombor: **Tahrirlash** ni oching (ogohlantirishni o'qing), o'zgartirmasdan **Saqlash**.
3. demo_rahbar: hujjatda **Tasdiqlash va chiqarish**.
4. demo_ombor: **Tasdiqqa yuborish**. demo_rahbar: **Tasdiqlash va chiqarish** → tasdiqlang.
5. **Qoldiqlar** (Asosiy).
**Kutilgan natija:**
- 1-qadam: holat "Tasdiq kutilmoqda", Yuborilgan vaqti; rahbarga botda ✅/❌ xabari (T-DST-BOT-21 dagidek).
- 2-qadam: "Hujjat tasdiqqa yuborilgan. O'zgartirsangiz, qayta yuborish kerak bo'ladi."; saqlangach holat "Qoralama".
- 3-qadam: "Hujjat hali tasdiqqa yuborilmagan."
- 4-qadam: "Tovar ombordan chiqariladi. Davom etasizmi?" → "Tasdiqlangan", Tasdiqlagan DEMO Rahbar, Tannarx 2,88 × 70 000 = **201 600**. Omborchiga "✅ Hisobdan chiqarish DW-… tasdiqlandi."
- 5-qadam: K haqiqiy **405.48**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-07. O'z hujjatini tasdiqlash taqiqi va muallif rad etishi
**Nima tekshiriladi:** Tasdiqlovchi o'zi tuzgan hujjatni tasdiqlay olmaydi (faqat Admin); muallif o'z qoralamasini bekor qila oladi; tasdiqlash huquqi yo'q foydalanuvchiga tasdiq tugmasi ko'rinmaydi.
**Kim:** demo_rahbar, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-06
**Qadamlar:**
1. demo_rahbar: yangi hisobdan chiqarish — Asosiy, Sabab = Buzilish, DEMO-GIP-125 `1`. Saqlang → **Tasdiqqa yuborish** → **Tasdiqlash va chiqarish**.
2. demo_ombor bilan shu hujjatni oching.
3. demo_rahbar: **Rad etish** → sabab `DEMO: o'z hujjatim`.
**Kutilgan natija:**
- 1-qadam: "O'zingiz tuzgan hujjatni boshqa rahbar tasdiqlashi kerak." Ombor o'zgarmaydi. (Rahbarga bot xabari kelmaydi — u muallif.)
- 2-qadam: demo_omborda "Tasdiqlash va chiqarish" tugmasi yo'q.
- 3-qadam: holat "Bekor qilingan". G **105**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-08. Rezervdagi tovar hisobdan chiqarilmaydi; o'tkazilganini bekor qilish
**Nima tekshiriladi:** Buyurtmalar rezervidagi tovar chiqarilmaydi (mavjud qoldiq tekshiriladi); xato chiqarilgan hujjat bekor qilinsa tovar omborga qaytadi.
**Kim:** demo_ombor, demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-07
**Qadamlar:**
1. demo_ombor: hisobdan chiqarish — Asosiy, Sinish, DEMO-GIP-125 `101` (formada "Mavjud" ustunini ko'ring). Saqlang → Tasdiqqa yuborish. demo_rahbar: **Tasdiqlash va chiqarish**.
2. demo_ombor: Tahrirlash → Miqdor `1` → Saqlash → **Tasdiqqa yuborish**. demo_rahbar: **Tasdiqlash va chiqarish**.
3. demo_rahbar: **Bekor qilish (tovar omborga qaytadi)** → sabab `DEMO: xato chiqarildi`.
4. **Qoldiqlar** (har qadamdan keyin).
**Kutilgan natija:**
- 1-qadam: "Mavjud" 100 (haqiqiy 105 − C rezervi 5). Tasdiqlashda "DEMO-GIP-125 Gipsokarton 12,5 mm: mavjud qoldiq 100, kerak 101 (rezervdagi tovar hisobga olinmaydi)." Ombor o'zgarmaydi.
- 2-qadam: "Tasdiqlangan", tannarx **50 000** (eski 50 000 so'mlik partiyadan). G **104**.
- 3-qadam: "Bekor qilingan", G yana **105**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-09. Pereso'rtitsa (bir tovar o'rniga boshqasi) va bekor qilish
**Nima tekshiriladi:** A tovar FIFO tannarxda chiqim, B tovar shu summada kirim (B birlik tannarxi = A summasi / B miqdori); ombor qiymati o'zgarmaydi; bekor qilish ikkalasini qaytaradi.
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-08
**Qadamlar:**
1. **Pereso'rtitsa → Yangi pereso'rtitsa**: Ombor = Asosiy, Izoh = `DEMO: 2-navga o'tkazildi`. Qator: Chiqim = DEMO-KAF-3030 `10`, Kirim = DEMO-KAF-3030 `10`. **Saqlash**.
2. Kirim tovarini DEMO-KAF-3030-2, miqdorni `8` qiling. **Saqlash** → kartada **O'tkazish** → tasdiqlang.
3. **Qoldiqlar** (Asosiy).
4. **Bekor qilish** → sabab `DEMO: pereso'rtitsa sinovi`. **Qoldiqlar**.
**Kutilgan natija:**
- 1-qadam: "Chiqim va kirim tovari bir xil bo'lmasin."
- 2-qadam: "Ombor qoldig'i o'zgaradi. Davom etasizmi?" → "O'tkazilgan": Tannarx 10 × 70 000 = **700 000**, Yangi birlik tannarxi 700 000 / 8 = **87 500**.
- 3-qadam: Kafel **395.48**, Kafel 2-nav **8** (qiymati 8 × 87 500 = 700 000 — ombor qiymati o'zgarmagan).
- 4-qadam: "Bekor qilingan"; Kafel **405.48**, Kafel 2-nav **0**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-10. Landed cost: taqsimlash, sotilgan qism, o'tkazish va bekor qilish
**Nima tekshiriladi:** Yo'l haqi kirim qatorlariga qiymat (yoki miqdor/og'irlik) ulushida taqsimlanadi va tannarxni oshiradi; allaqachon chiqarilgan qismga tushgan summa ko'rsatiladi va o'tgan sotuvlar qayta baholanmaydi; bekor qilish narxni qaytaradi; faqat `DstSeeCost` bilan.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-09, T-DST-SET-15
**Qadamlar:**
1. **Distributsiya → Qo'shimcha xarajatlar (landed cost) → Yangi hujjat**: Taqsimlash = qiymat bo'yicha, Izoh = `DEMO yo'l haqi`.
2. "Kirimlar" blokida "Kirim raqami yoki yetkazib beruvchi" qidiruviga T-DST-SET-15 kirim raqamini yozing va qo'shing. Ombor modulidagi ko'chirma kirimini (Filial, INC-…) qidirib ko'ring.
3. "Xarajatlar": Turi = Yo'l haqi, Summa = `850000` → qo'shing. "Taqsimlash (oldindan ko'rish)" jadvalini ko'ring.
4. Taqsimlashni "miqdor bo'yicha" qilib saqlang, jadvalni ko'ring; so'ng yana "qiymat bo'yicha".
5. **O'tkazish (tannarxga qo'shish)** → tasdiqlang. Ombor modulida SET-15 kirimining qator narxlarini ko'ring. A buyurtmasi kartasida jo'natish tannarxini ko'ring.
6. **Bekor** → sabab `DEMO: landed cost sinovi`. Kirim narxlarini qayta ko'ring.
**Kutilgan natija:**
- 2-qadam: kirim — DEMO Asosiy ombor, Qiymat 85 000 000, 3 qator. Ko'chirma kirimi qidiruvda chiqmaydi.
- 3-qadam (qiymat bo'yicha): Sement — qiymat 40 000 000, ulush **400 000**, tannarx 40 000 → **40 400**, Sotilgan **242**; Kafel — 35 000 000, **350 000**, 70 000 → **70 700**, Sotilgan **100.4**; Gipsokarton — 10 000 000, **100 000**, 50 000 → **50 500**, Sotilgan **145**. Ogohlantirish: "Xarajatning **239 580** so'mi allaqachon chiqarilgan tovarga to'g'ri keladi — o'tgan sotuvlar tannarxi o'zgarmaydi." (242 × 400 + 100,4 × 700 + 145 × 500 = 96 800 + 70 280 + 72 500).
- 4-qadam (miqdor bo'yicha, jami 1 700): Sement 500 000, Kafel 250 000, Gipsokarton 100 000 — har birida +500 so'm/birlik.
- 5-qadam: "O'tkazilgan". Kirim qatorlari narxi 40 400 / 70 700 / 50 500. A jo'natishi tannarxi o'zgarmagan (5 624 000).
- 6-qadam: "Bekor qilingan", narxlar yana 40 000 / 70 000 / 50 000.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-11. Minimal qoldiq va "tugayapti" ogohlantirishi
**Nima tekshiriladi:** Minimal va maksimal qoldiq sozlanadi; qoldiq minimaldan pastga **tushganda** omborchiga va rahbarlarga (amalni bajargandan tashqari) bir marta xabar; "Minimaldan past" filtri.
**Kim:** demo_rahbar, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-10
**Qadamlar:**
1. **Sozlamalar** → Omborlar jadvalida DEMO Asosiy ombor "Minimal qoldiq" ustunidagi raqamni bosing. Tovar = DEMO-GIP-125, Minimal = `100`, Maksimal = `50` → ➕.
2. Maksimal = `200` → ➕.
3. demo_ombor: hisobdan chiqarish — Asosiy, Sinish, DEMO-GIP-125 `6` → Tasdiqqa yuborish. demo_rahbar: **Tasdiqlash va chiqarish**.
4. Omborchi va rahbar botlarini, **Qoldiqlar** → "Minimaldan past" ni tekshiring.
**Kutilgan natija:**
- 1-qadam: "Maksimal minimaldan kam bo'lmasin."
- 2-qadam: ro'yxatda "DEMO-GIP-125 — Gipsokarton 12,5 mm 100 list → 200"; Omborlar jadvalida Minimal qoldiq = 1.
- 3-qadam: tannarx 6 × 50 000 = **300 000**; G haqiqiy **99**.
- 4-qadam: omborchiga "⚠️ Minimal qoldiqdan past — DEMO Asosiy ombor: • DEMO-GIP-125 Gipsokarton 12,5 mm: 99 / min 100" (rahbarga yuborilmaydi — tasdiqlagan o'zi). "Minimaldan past" filtrida Gipsokarton.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-WH-12. Buyurtma taklifi → SRM xarid buyurtmasi
**Nima tekshiriladi:** Mavjud (rezervdan tashqari) qoldiq minimaldan past tovarlar uchun taklif = maqsad − mavjud; tanlanganlar SRM xarid buyurtmasiga aylanadi (pilot 3-bosqich).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-WH-11
**Qadamlar:**
1. **Distributsiya → Buyurtma taklifi**: Ombor = DEMO Asosiy ombor.
2. Narx maydoniga `52000` yozing, belgi qo'yilgan holda **Xarid buyurtmasi yaratish**.
3. **Ta'minot (SRM) → Xarid jarayoni → Xarid buyurtmalari** da yaratilgan buyurtmani oching.
4. Buyurtma taklifi sahifasini yangilang.
**Kutilgan natija:**
- 1-qadam: qator — DEMO Asosiy ombor, DEMO-GIP-125: Mavjud **94** list (99 − C rezervi 5), Minimal 100, Maqsad 200, Buyurtma **106**, Yetkazib beruvchi "—" (SRM hisob-fakturasi yo'q).
- 2-qadam: "Xarid buyurtmalari yaratildi: PO-2026-MM-NNNN".
- 3-qadam: holat Created, izoh "DST_REORDER: minimal qoldiq bo'yicha (distributsiya). Omborlar: DEMO Asosiy ombor", qator DEMO-GIP-125 106 × 52 000 = **5 512 000**, QQS 0.
- 4-qadam: "30 kunda buyurtma" ustunida PO raqami, qator belgisi olib tashlangan (ikki marta buyurtma qilinmasin).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Bo'lim oxiridagi nazorat holati

| Ko'rsatkich | Qiymat |
|---|---|
| DEMO Asosiy ombor | S 758 / rezerv 10; K 405.48 / 10; G 99 / 5; Kafel 2-nav 0 |
| DEMO Filial ombor | S 298, K 147, G 21 |
| DEMO Brak ombor | Sement 2 |
| DEMO Anvar do'koni qarzi | 13 613 600 (A yopilgan) |
| DEMO Bunyodkor qurilish | avans 1 812 500 |
| DEMO Kassa | 7 536 400 (1 500 000 − 10 000 + 100 000 + 5 946 400) |
| DEMO Bank hisobi | 6 812 500 (5 000 000 + 1 812 500; karta 500 000 bekor qilingan) |
| Agentlar qo'lida | 0 |

## Ma'lum cheklovlar va kuzatish kerak bo'lgan joylar
- Web to'lov oynasida "Agent qo'lida (naqd)" tanlansa, naqd **to'lovni kiritgan web foydalanuvchining** (masalan, kassirning) qo'lida hisoblanadi, buyurtma agentining emas. Agent naqdini faqat bot orqali qabul qiling.
- To'lov bekor qilinganda allaqachon "Yopildi" bo'lgan buyurtma holati avtomatik qaytmaydi (qarz to'g'ri tiklanadi, holat — yo'q). Bekor qilishni buyurtma yopilishidan oldin bajaring yoki holatni kuzating.
- Ombor modulidagi chiqim/ko'chirma distributsiya rezervini tekshirmaydi — ko'chirmadan keyin "Rezerv qoldiqdan ko'p" filtrini tekshirib turing.
- Inventarizatsiyada qoldiqqa yozish ombor bo'yicha oyiga bir marta (boshlang'ich sanoq ham hisobga kiradi) — xato kiritilsa tuzatish keyingi oyda yoki administrator orqali.
- Buyurtma taklifida yetkazib beruvchi va narx faqat SRM xarid hisob-fakturasidan olinadi; ombor modulidagi oddiy kirim (invoyssiz) hisobga olinmaydi.
- Landed cost valyutasiz (asosiy valyutada) kiritiladi.
