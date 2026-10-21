# 2. Buyurtmalar: narx, kredit, rezerv, jo'natish, qaytarish, aksiyalar

Bu bo'lim buyurtmaning to'liq hayot siklini tekshiradi: Yangi → Tasdiqlangan → Yig'ilmoqda → (Qisman) Jo'natildi → Yetkazildi → Yopildi, Bekor (sabab bilan). Narx avtomatik (maxsus → narx turi → standart), chegirma rol limiti bilan, minimal narx, minimal summa, qarz limiti va muddati o'tgan qarz bloki, rezerv va "kirimni kutmoqda", sof FIFO tannarx, nakladnoy, jo'natishni bekor qilish, qaytarishlar va aksiyalar.

Testlarni T-DST-ORD-01 … 27 tartibida bajaring. Buyurtmalar matnda harf bilan ataladi (A, B, C, D, E, F, G1, G2, P1) — raqamini (`D-2026-NNNNN`) Izohga yozib qo'ying.

Qayerda nima tekshiriladi:
- Buyurtma kartasi: **Distributsiya → Buyurtmalar** → qatorni bosing. Yuqorida holat va amal tugmalari, "Tovarlar" jadvalida Rezerv va Jo'natildi ustunlari, o'ngda mijoz qarzi va "Tarix".
- Qoldiq: **Distributsiya → Qoldiqlar**, Ombor = DEMO Asosiy ombor. Ustunlar: Haqiqiy / Rezerv / Mavjud (asosiy birlikda). Matnda "S 1 000/40/960" — Sement: haqiqiy 1 000, rezerv 40, mavjud 960; K — Kafel (m²), G — Gipsokarton (list).
- Mijoz qarzi: **Distributsiya → Qarzlar** yoki buyurtma kartasidagi "Qarz".
- Tannarx va yalpi foyda buyurtma kartasida faqat `DstSeeCost` huquqi bilan ko'rinadi (demo_rahbar ko'radi, demo_ombor ko'rmaydi).
- Bot xabarlari: agent (Jasur), omborchi, rahbar va mijoz (Anvar) telefonlarida — T-DST-SET-24 da ulangan. Bildirishnoma amalni bajargan foydalanuvchining o'ziga yuborilmaydi.

Boshlang'ich holat (T-DST-SET-15 dan keyin): S 1 000/0/1 000, K 500/0/500, G 200/0/200. Anvar qarzi 0, Bunyodkor qarzi 5 000 000 (muddati o'tgan).

---

### T-DST-ORD-01. Web'da yangi buyurtma: birliklar, shtrix-kod, avtomatik narx (A)
**Nima tekshiriladi:** Narx mijoz narx turidan avtomatik olinadi va sotish birligiga (poddon, quti) koeffitsient bilan o'giriladi; shtrix-kod bilan qator qo'shiladi; og'irlik va hajm hisoblanadi; yangi buyurtma rezerv qo'ymaydi (pilot M3, M4, B2).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-13, T-DST-SET-15, T-DST-SET-18
**Qadamlar:**
1. **Distributsiya → Buyurtmalar → Yangi buyurtma** ni oching. Mijoz = `DEMO Anvar do'koni`. O'ngdagi "Qarz va limit" panelini ko'ring.
2. Agent = DEMO Jasur, Ombor = "avtomatik tanlanadi", Yetkazish sanasi = ertaga, Chegirma % = 0, O'zi olib ketadi ✗.
3. "Tovar qo'shish (nom yoki artikul)..." maydoniga `SEM` yozib **DEMO-SEM-400** ni tanlang. Birlik = `poddon`, Miqdor = `1`.
4. "Shtrix-kod + Enter" maydoniga `4780000000045` yozib Enter bosing. Qo'shilgan Kafel qatorida Miqdor = `30` (birlik — quti).
5. `9999999999999` yozib Enter bosing.
6. `GIP` bo'yicha **DEMO-GIP-125** ni qo'shing, Miqdor = `20`.
7. **Saqlash** ni bosing.
**Kutilgan natija:**
- 1-qadamda panel: Qarz 0, Muddati o'tgan 0, Tasdiqlangan, jo'natilmagan 0, Kredit limiti 20 000 000, Limitdan qolgan 20 000 000, Narx turi Chakana, To'lov muddati 7.
- Narxlar: Sement **2 480 000** / poddon (62 000 × 40), Kafel **136 800** / quti (95 000 × 1,44), Gipsokarton **68 000** / list. "Mavjud" ustunida 1 000, 500, 200.
- 5-qadamda "Shtrix-kod topilmadi." xabari, qator qo'shilmaydi.
- Summalar: 2 480 000 + 30 × 136 800 = 4 104 000 + 20 × 68 000 = 1 360 000 → **Jami 7 944 000**.
- Saqlangach buyurtma kartasi ochiladi: raqam `D-2026-NNNNN` (buyurtma **A**), holat **Yangi**, Manba web. Qatorlarda "1 poddon = 40 qop", "30 quti = 43.2 m²", "20 list". Og'irlik **3234.4 kg** (2 000 + 734,4 + 500), Hajm **2.582 m³**. Rezerv ustuni 0.
- Qoldiqlar o'zgarmagan: S 1 000/0/1 000, K 500/0/500, G 200/0/200.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-02. Chegirma rol limiti (qator + buyurtma chegirmasi) va qoralamani o'chirish (B)
**Nima tekshiriladi:** Qo'lda berilgan chegirma rol limitidan oshmaydi; qator va buyurtma chegirmasi birgalikdagi samarali foizi tekshiriladi; yangi buyurtma o'chiriladi (pilot B3).
**Kim:** demo_rahbar (rol limiti 10%)   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-08, T-DST-ORD-01
**Qadamlar:**
1. Yangi buyurtma: Mijoz = DEMO Anvar do'koni, DEMO-GIP-125 `10` list, qator "%" = `12`. **Saqlash**.
2. Qator % = `5`, yuqoridagi "Chegirma %" = `6`. **Saqlash**.
3. Qator % = `5`, Chegirma % = `0`. **Saqlash** (buyurtma **B**).
4. B kartasida 🗑 (o'chirish) tugmasini bosing va tasdiqlang.
**Kutilgan natija:**
- 1-qadamda "Chegirma 12% — sizning limitingiz 10%." Buyurtma yaratilmaydi.
- 2-qadamda "Chegirma 10.7% — sizning limitingiz 10%." (100 − 95 × 94 / 100 = 10,7%).
- 3-qadamda saqlanadi: 10 × 68 000 = 680 000, qator chegirmasi 5% = 34 000 → summa va **Jami 646 000**, qatorda % = 5.
- 4-qadamdan keyin Buyurtmalar ro'yxatiga qaytadi, B ro'yxatda yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-03. Minimal narx va narx turi qoidasi (C)
**Nima tekshiriladi:** Chegirmadan keyingi narx minimal narxdan past bo'lmaydi; mijoz narx turida narxi yo'q tovarga standart narx turi narxi olinadi (pilot B2, B3).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-21
**Qadamlar:**
1. Yangi buyurtma: Mijoz = `DEMO Bunyodkor qurilish`. Panelni ko'ring.
2. Qatorlar: DEMO-SEM-400 birlik `qop` `10`, qator % = `6`; DEMO-KAF-3030 birlik `m²` `10`; DEMO-GIP-125 `5`. Yetkazish sanasi = ertaga. **Saqlash**.
3. Sement qator % ni `5` qiling. **Saqlash** (buyurtma **C**).
**Kutilgan natija:**
- Panel: Qarz 5 000 000, Muddati o'tgan **5 000 000** (qizil), Tasdiqlangan, jo'natilmagan 0, Kredit limiti 50 000 000, Limitdan qolgan 45 000 000, Narx turi Diler, muddat 14.
- Narxlar: Sement **55 000** (Diler), Kafel **95 000** (Diler narxi yo'q — standart Chakana), Gipsokarton **68 000** (standart).
- 2-qadamda "DEMO-SEM-400 Sement M400: narx 51 700 minimal narxdan (52 000) past." (55 000 × 0,94 = 51 700). Saqlanmaydi.
- 3-qadamda saqlanadi: Sement 550 000 − 5% = **522 500**; Kafel **950 000**; Gipsokarton **340 000**; **Jami 1 812 500**. Og'irlik 795 kg (500 + 170 + 125). Holat Yangi, Agent — bo'sh.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-04. Tasdiqlash: ombor tanlash va rezerv (A)
**Nima tekshiriladi:** Tasdiqlashda savdo omborlaridan tovarni to'liq qoplaydigani tanlanadi va rezerv qo'yiladi; mavjud = haqiqiy − rezerv; agent va omborchiga xabar (pilot B5, B6, B10).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-01, T-DST-SET-24
**Qadamlar:**
1. A kartasida **Tasdiqlash** ni bosing.
2. Kartani va **Qoldiqlar** sahifasini tekshiring.
3. Agent (Jasur) va omborchi telefonlaridagi bot xabarlarini ko'ring.
**Kutilgan natija:**
- Holat **Tasdiqlangan**, Ombor = DEMO Asosiy ombor (Filial omborida qoldiq yo'q). Rezerv ustuni: Sement 40, Kafel 43.2, Gipsokarton 20. "Tahrirlash" tugmasi yo'q; "Tasdiqni qaytarish", "Yig'ishni boshlash", "Rezervni yangilash", "Jo'natish", "Bekor qilish" bor. Tarixda "Yangi → Tasdiqlangan — DEMO Rahbar".
- Qoldiqlar: S 1 000/40/960, K 500/43.2/456.8, G 200/20/180.
- Agent boti: "Buyurtma D-2026-NNNNN tasdiqlandi: DEMO Anvar do'koni, 7 944 000 so'm."
- Omborchi boti: "📦 Yig'ish kerak: buyurtma D-… tasdiqlandi. Mijoz: DEMO Anvar do'koni Ombor: DEMO Asosiy ombor 3 qator, 3234.4 kg, 7 944 000 so'm Yetkazish: <ertangi sana>" (qatorlarga bo'lingan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-05. Minimal buyurtma summasi va "O'zi olib ketadi" (D)
**Nima tekshiriladi:** Yetkazish uchun minimal summadan kichik buyurtma tasdiqlanmaydi; o'zi olib ketishda bu cheklov yo'q (pilot B8).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-04
**Qadamlar:**
1. Yangi buyurtma: Mijoz = DEMO Anvar do'koni, Agent = DEMO Jasur, DEMO-GIP-125 `5` list. **Saqlash** (buyurtma **D**), keyin **Tasdiqlash**.
2. **Tahrirlash** → "O'zi olib ketadi" ✓ → **Saqlash** → **Tasdiqlash**.
**Kutilgan natija:**
- 1-qadam: summa 5 × 68 000 = 340 000; tasdiqlashda "Yetkazish uchun minimal buyurtma summasi — 500 000." Holat Yangi qoladi.
- 2-qadam: holat Tasdiqlangan, "Yetkazish sanasi" o'rnida "O'zi olib ketadi", rezerv Gipsokarton 5. Qoldiq G 200/25/175.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-06. Kredit limitidan oshish: huquqsiz rad, rahbar ruxsati (E)
**Nima tekshiriladi:** Qarz + tasdiqlangan, hali jo'natilmagan buyurtmalar + shu buyurtma limitdan oshsa buyurtma `DstOverrideCredit` huquqisiz tasdiqlanmaydi; rahbar izoh bilan ruxsat beradi; yirik buyurtma va limit haqida rahbarga botda xabar (pilot B4, A8).
**Kim:** demo_ombor (operator vazifasida), demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-05
**Qadamlar:**
1. demo_ombor bilan: Yangi buyurtma — Mijoz = DEMO Anvar do'koni (o'ngdagi "Qarz va limit" panelini ko'ring), Agent = DEMO Jasur, **Ombor = DEMO Asosiy ombor**, DEMO-SEM-400 birlik `poddon` `9`. **Saqlash** (buyurtma **E**). E kartasidagi qarz blokini ko'ring.
2. Rahbar telefonidagi bot xabarlarini ko'ring (**✅ Tasdiqlash tugmasini bosmang** — u T-DST-BOT-09 da sinaladi).
3. demo_ombor bilan E kartasida **Tasdiqlash**.
4. demo_rahbar bilan E kartasida **Tasdiqlash**. Ochilgan "Limitdan oshishga ruxsat" oynasida izohni bo'sh qoldirib **Qo'llash**.
5. Izoh = `DEMO: rahbar ruxsati, oldindan kelishilgan` → **Qo'llash**.
**Kutilgan natija:**
- E summasi 9 × 2 480 000 = **22 320 000**.
- 1-qadam panel (va E kartasi): Qarz 0, **Tasdiqlangan, jo'natilmagan 8 284 000** (A 7 944 000 + D 340 000), Kredit limiti 20 000 000, Limitdan qolgan **11 716 000**.
- 2-qadam: rahbarga ikki xabar — "Yirik buyurtma D-…: DEMO Anvar do'koni, 22 320 000 so'm." va "Buyurtma D-… (DEMO Anvar do'koni, 22 320 000 so'm) kredit shartidan chiqdi — tasdiqlash kerak: Qarz + jo'natilmagan buyurtmalar + shu buyurtma 30 604 000 — limit 20 000 000 dan oshadi. (shundan tasdiqlangan, hali jo'natilmagan buyurtmalar: 8 284 000)" + **✅ Tasdiqlash** tugmasi (0 + 8 284 000 + 22 320 000 = 30 604 000).
- 3-qadam: "Qarz + jo'natilmagan buyurtmalar + shu buyurtma 30 604 000 — limit 20 000 000 dan oshadi. (shundan tasdiqlangan, hali jo'natilmagan buyurtmalar: 8 284 000)" xabari; ruxsat oynasi chiqmaydi (huquq yo'q); holat Yangi. Rahbarga shu buyurtma bo'yicha bugun ikkinchi limit xabari kelmaydi (kuniga bir marta).
- 4-qadam: oynada shu muammo matni qizil; bo'sh izoh bilan buyurtma tasdiqlanmaydi (oyna qayta chiqadi).
- 5-qadam: holat Tasdiqlangan; kartada sariq blok "Limitdan oshishga ruxsat berdi: DEMO Rahbar — DEMO: rahbar ruxsati, oldindan kelishilgan"; tarix izohida ham shu matn. Rezerv Sement 360. Qoldiq S 1 000/400/600.
- Limit hisobiga tasdiqlangan, yig'ilayotgan va qisman jo'natilgan buyurtmalarning jo'natilmagan qismi ham kiradi (endi E ham — keyingi F, G1, G2 tasdig'ida rahbar ruxsati kerak bo'ladi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-07. Muddati o'tgan qarz bloki va ruxsat (C)
**Nima tekshiriladi:** Limit yetarli bo'lsa ham muddati o'tgan qarzli mijoz buyurtmasi rahbar ruxsatisiz tasdiqlanmaydi (pilot B4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-03
**Qadamlar:**
1. C kartasida **Tasdiqlash**.
2. Oynada izoh = `DEMO: Bunyodkor bilan to'lov jadvali kelishilgan` → **Qo'llash**.
**Kutilgan natija:**
- 1-qadam: "Limitdan oshishga ruxsat" oynasida "Muddati o'tgan qarz bor: 5 000 000." (limit muammosi yo'q: 5 000 000 + 1 812 500 = 6 812 500 < 50 000 000).
- 2-qadam: Tasdiqlangan, Ombor DEMO Asosiy ombor, rezerv: Sement 10, Kafel 10, Gipsokarton 5. Qoldiq: S 1 000/410/590, K 500/53.2/446.8, G 200/30/170.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-08. Qoldiq yetmasa — "kirimni kutmoqda" (F)
**Nima tekshiriladi:** Mavjud qoldiqdan ko'p buyurtma tasdiqlanadi, lekin faqat mavjud miqdor rezervga olinadi; qolgani "kirimni kutmoqda"; mavjud qoldiq manfiy bo'lmaydi (pilot B5).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-07
**Qadamlar:**
1. Yangi buyurtma: DEMO Anvar do'koni, Agent DEMO Jasur, DEMO-GIP-125 `180` list. **Saqlash** (buyurtma **F**), **Tasdiqlash**. "Limitdan oshishga ruxsat" oynasida izoh = `DEMO: F — kirimni kutish sinovi` → **Qo'llash**.
2. **Qoldiqlar**: filtr "Rezerv qoldiqdan ko'p" ni ham tanlab ko'ring.
**Kutilgan natija:**
- F summasi 180 × 68 000 = **12 240 000**. Oynada: "Qarz + jo'natilmagan buyurtmalar + shu buyurtma 42 844 000 — limit 20 000 000 dan oshadi. (shundan tasdiqlangan, hali jo'natilmagan buyurtmalar: 30 604 000)" (A + D + E = 30 604 000). Izoh bilan tasdiqlanadi.
- F kartasida Gipsokarton: Rezerv **170**, ostida qizil "kirimni kutmoqda: 10" (mavjud edi 200 − 30 = 170).
- Qoldiq G **200/200/0**. "Rezerv qoldiqdan ko'p" filtri bo'sh.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-09. Kirimdan keyin avtomatik rezerv (F)
**Nima tekshiriladi:** Ombor modulidagi yangi kirim qabul qilingach "kirimni kutmoqda" qatorlar shu omborda avtomatik rezervga olinadi (birinchi tasdiqlangan — birinchi); "Rezervni yangilash" tugmasi qo'lda ham ishlaydi. Yangi kirim — FIFO'da alohida (52 000 so'mlik) qatlam.
**Kim:** demo_ombor (kirim), demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-08
**Qadamlar:**
1. demo_ombor: **Omborxona → Ombor → Operatsiyalar → Kirimlar → Yangi kirim**: Invoys bo'sh, Qabul qiluvchi ombor = DEMO Asosiy ombor, DEMO-GIP-125 `50` list × `52000`, QQS % 0, Izoh = `DEMO 2-partiya gipsokarton`. Saqlang va **Qabul qilish**.
2. **Qoldiqlar** ni va F kartasini tekshiring.
3. demo_rahbar: F kartasida **Rezervni yangilash**.
**Kutilgan natija:**
- Kirim summasi 2 600 000. 2-qadamda (qabul qilish bilanoq) F: Gipsokarton rezerv **180**, "kirimni kutmoqda" yozuvi yo'q. Qoldiq G **250/210/40**.
- 3-qadam: o'zgarish yo'q (rezerv allaqachon to'liq) — qoldiq G 250/210/40.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-10. Ikki operator oxirgi qoldiqni ikki marta ololmaydi (G1, G2)
**Nima tekshiriladi:** Bir vaqtda tasdiqlashda rezerv (ombor+tovar) qulfi ostida hisoblanadi — umumiy rezerv haqiqiy qoldiqdan oshmaydi (pilot B5).
**Kim:** demo_rahbar (ikki brauzer oynasi)   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-09
**Qadamlar:**
1. Ikkita yangi buyurtma: DEMO Anvar do'koni, Agent DEMO Jasur, har birida DEMO-GIP-125 `40` list (buyurtmalar **G1** va **G2**, har biri 2 720 000). Saqlang.
2. G1 kartasini 1-oynada, G2 kartasini 2-oynada (Inkognito, demo_rahbar) oching. Ikkala oynada **Tasdiqlash** ni bosing — limit oshgani uchun "Limitdan oshishga ruxsat" oynasi chiqadi; ikkalasiga izoh `DEMO: parallel sinov` yozing va **Qo'llash** ni imkon qadar bir vaqtda bosing.
3. Ikkala kartani yangilang va **Qoldiqlar** ni tekshiring.
**Kutilgan natija:**
- Ruxsat oynasida limit matni (tasdiqlangan, jo'natilmagan: kamida 42 844 000 — A, D, E, F).
- Ikkala buyurtma Tasdiqlangan. Ulardan birida rezerv **40**, ikkinchisida rezerv **0** va "kirimni kutmoqda: 40".
- Qoldiq G **250/250/0** — rezerv 290 emas, mavjud manfiy emas.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-11. Tasdiqni qaytarish va yangi/tasdiqlangan buyurtmani bekor qilish (G1, G2)
**Nima tekshiriladi:** "Tasdiqni qaytarish" rezervni yechadi va buyurtmani tahrirlashga qaytaradi; bekor qilish sababi majburiy (kamida 3 belgi); agentga xabar (pilot B6).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-10
**Qadamlar:**
1. Rezervi 40 bo'lgan buyurtmada **Tasdiqni qaytarish** → tasdiqlang.
2. Shu buyurtmada **Bekor qilish** → sabab = `ab` → **Qo'llash**.
3. Sabab = `DEMO: parallel sinov` → **Qo'llash**.
4. Ikkinchi buyurtmani (rezerv 0) ham shu sabab bilan bekor qiling. **Qoldiqlar** ni tekshiring.
**Kutilgan natija:**
- 1-qadam: holat Yangi, "Tahrirlash" qaytadi, rezerv 0, tarixda "Tasdiqlangan → Yangi". Qoldiq G 250/210/40.
- 2-qadam: "Bekor qilish sababini yozing."
- 3–4-qadam: ikkala buyurtma "Bekor qilingan", kartada qizil "Bekor qilingan: DEMO: parallel sinov". Agent botiga "Buyurtma D-… bekor qilindi: DEMO Anvar do'koni. Sabab: DEMO: parallel sinov" (har biriga).
- Qoldiq G **250/210/40** (A 20 + D 5 + C 5 + F 180).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-12. Yig'ish va yig'ish varaqasi (A)
**Nima tekshiriladi:** Omborchi tasdiqlangan buyurtmani yig'ishga oladi va kerak bo'lsa to'xtatadi (rezerv saqlanadi); yig'ish varaqasi sotish va asosiy birlikda, shtrix-kodlar bilan chop etiladi (pilot B9).
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-04
**Qadamlar:**
1. **Buyurtmalar**: filtr Holat = Tasdiqlangan, Ombor = DEMO Asosiy ombor. A ni oching.
2. **Yig'ishni boshlash** ni bosing. So'ng **Yig'ishni to'xtatish** ni bosing, keyin yana **Yig'ishni boshlash**.
3. **Yig'ish varaqasi** ni oching (yangi oynada) va **Chop etish** ni bosing (PDF ga saqlang).
**Kutilgan natija:**
- "Yig'ishni to'xtatish" dan keyin holat **Tasdiqlangan**, rezerv o'zgarmagan (40 / 43.2 / 20). Oxirida holat **Yig'ilmoqda**; tarixda "Tasdiqlangan → Yig'ilmoqda", "Yig'ilmoqda → Tasdiqlangan", "Tasdiqlangan → Yig'ilmoqda" — DEMO Omborchi.
- Varaqada: Ombor DEMO Asosiy ombor, Mijoz DEMO Anvar do'koni, Yetkazish sanasi; 3 qator: DEMO-SEM-400 — `4780000000014 4780000000021` — Miqdor "1 poddon", Yig'ish "40 qop"; DEMO-KAF-3030 — "30 quti" / "43.2 m²"; DEMO-GIP-125 — "20 list" / "20 list". Og'irlik 3234.4 kg, Hajm 2.582 m³; "Yig'di" va "Tekshirdi" imzo joylari.
- demo_ombor kartada tannarx va yalpi foydani ko'rmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-13. To'liq jo'natish: FIFO tannarx, qarz, nakladnoy (A)
**Nima tekshiriladi:** Jo'natish ombor chiqimini yaratadi (sof FIFO), rezervni yechadi, mijoz qarziga muddat bilan yozadi; mijozga botda xabar; nakladnoy chop etiladi (pilot B6, B9, A7).
**Kim:** demo_ombor (jo'natish), demo_rahbar (tannarxni ko'rish)   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-12
**Qadamlar:**
1. A kartasida **Jo'natish** ni bosing. Oynadagi miqdorlarni tekshiring va **Jo'natish** ni bosing.
2. Jo'natishlar jadvalidagi **Nakladnoy** ni oching va chop eting.
3. **Qoldiqlar**, **Qarzlar** ni tekshiring. Mijoz (Anvar) botini ko'ring.
4. demo_rahbar bilan A kartasini oching. **Omborxona → Ombor → Operatsiyalar → Chiqimlar** da `DJ-` bo'yicha qidiring.
**Kutilgan natija:**
- Oynada (asosiy birlikda): Sement Qolgan 40, Rezerv 40, Miqdor 40; Kafel 43.2/43.2/43.2; Gipsokarton 20/20/20.
- Holat **Jo'natildi**. Jo'natishlar jadvalida `DJ-2026-NNNNN`, Jami **7 944 000**. "Jo'natildi" ustuni: 40 qop, 43.2 m², 20 list.
- Nakladnoy: № DJ-…, buyurtma raqami; Jo'natuvchi DEMO Asosiy ombor, Qabul qiluvchi DEMO Anvar do'koni, STIR 300000011; qatorlar sotish birligida: 1 poddon × 2 480 000.00, 30 quti × 136 800.00 = 4 104 000.00, 20 list × 68 000.00 = 1 360 000.00; Jami 7 944 000.00; "To'lov muddati (kun): 7 · Qarz: 7 944 000 so'm"; "Berdi / Qabul qildi".
- Qoldiq: S **960/370/590**, K **456.8/10/446.8**, G **230/190/40**.
- Qarzlar: Anvar **7 944 000**, "Muddati kelmagan" (muddat — bugun + 7 kun).
- Anvar boti: "Buyurtmangiz D-… jo'natildi: 7 944 000 so'm. Joriy qarzingiz: 7 944 000 so'm."
- demo_rahbar kartada: jo'natish Tannarx **5 624 000** (40 × 40 000 + 43,2 × 70 000 + 20 × 50 000 = 1 600 000 + 3 024 000 + 1 000 000), Yalpi foyda **2 320 000**.
- Ombor modulida `DJ-…` chiqimi bor, izohida "Distributsiya: D-… → DJ-…".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-14. Jo'natishni bekor qilish va qayta jo'natish (A)
**Nima tekshiriladi:** Xato jo'natish bekor qilinsa ombor, rezerv, qarz va holat aynan tiklanadi; qayta jo'natishda FIFO partiyalar ikki marta sarflanmaydi (pilot N2).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-13
**Qadamlar:**
1. A kartasidagi jo'natish qatorida ❌ (Jo'natishni bekor qilish) ni bosing. "Jo'natishni bekor qilish sababi" ga `DEMO: xato jo'natildi` yozing.
2. Kartani, **Qoldiqlar** va **Qarzlar** ni tekshiring.
3. A da yana **Jo'natish** (oynadagi standart miqdorlar bilan).
**Kutilgan natija:**
- 2-qadam: holat **Tasdiqlangan**; rezerv yana 40 / 43.2 / 20; tarixda "Tasdiqlangan — DJ-…: DEMO: xato jo'natildi". Qoldiq S 1 000/410/590, K 500/53.2/446.8, G 250/210/40. Anvar qarzi **0**. Ombor modulida DJ chiqimi o'chirilgan.
- 3-qadam: yangi `DJ-…` raqami, Jami 7 944 000, Tannarx yana **5 624 000** (40 000 / 70 000 / 50 000 partiyalaridan). Qoldiq S 960/370/590, K 456.8/10/446.8, G 230/190/40. Anvar qarzi **7 944 000**.
- Anvar botiga ikkinchi "jo'natildi" xabari keladi (bekor qilish haqida xabar yo'q).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-15. Qisman jo'natish (F)
**Nima tekshiriladi:** Buyurtmaning bir qismi jo'natiladi, qolgani rezervda qoladi; buyurtmadan ko'p jo'natib bo'lmaydi; qisman jo'natilgan buyurtmani "Yetkazildi" qilib bo'lmaydi (pilot B7).
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-09
**Qadamlar:**
1. F kartasida **Jo'natish** → Gipsokarton Miqdor = `200` → **Jo'natish**.
2. Miqdor = `100` → **Jo'natish**.
**Kutilgan natija:**
- 1-qadam: "1-qator: buyurtmadan ko'p jo'natib bo'lmaydi."
- 2-qadam: holat **Qisman jo'natildi**; jo'natish Jami 100 × 68 000 = **6 800 000**; rezerv 80; "Jo'natildi" 100 list. Kartada **"Yetkazildi" tugmasi yo'q** (avval qolgani jo'natiladi yoki "Qolganidan voz kechish").
- Qoldiq G **130/90/40**. Anvar qarzi 7 944 000 + 6 800 000 = **14 744 000**.
- (demo_rahbar uchun) tannarx 100 × 50 000 = **5 000 000** — eski 50 000 so'mlik partiyadan (unda 180 list qolgan edi), 52 000 so'mlik yangi partiyaga tegilmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-16. Qolganidan voz kechish (F)
**Nima tekshiriladi:** Qisman jo'natilgan buyurtmaning qolgan qismi sabab bilan yopiladi va rezerv yechiladi (pilot B7).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-15
**Qadamlar:**
1. F kartasida **Qolganidan voz kechish** → sabab = `ab` → **Qo'llash**.
2. Sabab = `DEMO: mijoz qolganidan voz kechdi` → **Qo'llash**.
**Kutilgan natija:**
- 1-qadam: "Bekor qilish sababini yozing."
- 2-qadam: holat **Jo'natildi**, rezerv 0, tarix izohida sabab. Qoldiq G **130/10/120**. Qarz o'zgarmaydi (14 744 000). (Jo'natilgan qism to'langan bo'lsa buyurtma darhol "Yopildi" bo'lardi; F hali to'lanmagan.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-17. O'zi olib ketish: jo'natish bilan birga "Yetkazildi" (D)
**Nima tekshiriladi:** O'zi olib ketadigan buyurtma to'liq jo'natilganda darhol "Yetkazildi" bo'ladi; mijozga bitta "yetkazildi" xabari ketadi (pilot B8).
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-05
**Qadamlar:**
1. D kartasida **Jo'natish** → **Jo'natish**. Mijoz (Anvar) botini ko'ring.
**Kutilgan natija:**
- Holat to'g'ridan-to'g'ri **Yetkazildi**; tarixda ketma-ket "Jo'natildi" va "Yetkazildi".
- Anvar botiga **bitta** xabar: "Buyurtmangiz D-… yetkazildi: 340 000 so'm. Rahmat!" ("jo'natildi" xabari kelmaydi).
- Jo'natish 340 000 (tannarx 5 × 50 000 = 250 000). Qoldiq G **125/5/120**. Anvar qarzi **15 084 000**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-18. "Yetkazildi" qo'lda va mijozga xabar (A)
**Nima tekshiriladi:** Operator jo'natilgan buyurtmani yetkazildi deb belgilaydi; mijozga botda xabar boradi (pilot B6, A7).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-14
**Qadamlar:**
1. A kartasida **Yetkazildi** ni bosing.
**Kutilgan natija:**
- Holat **Yetkazildi**. Qolgan tugmalar: "To'lov qabul qilish", "Qaytarish"; "Bekor qilish" va jo'natishni bekor qilish (❌) yo'q.
- Anvar boti: "Buyurtmangiz D-… yetkazildi: 7 944 000 so'm. Rahmat!"
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-19. Tasdiqlangan buyurtmani bekor qilish; jo'natilganini o'zgartirib bo'lmaydi (E, A)
**Nima tekshiriladi:** Jo'natilmagan buyurtma sabab bilan bekor qilinadi va rezerv yechiladi; jo'natilgan buyurtma tahrirlanmaydi va bekor qilinmaydi — faqat qaytarish orqali (pilot B6, N2).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-06, T-DST-ORD-18
**Qadamlar:**
1. E kartasida **Bekor qilish** → sabab = `DEMO: mijoz bekor qildi` → **Qo'llash**.
2. **Qoldiqlar** ni tekshiring.
3. A ning ID raqami bilan manzil satrida `/Distribution/DstOrders/Edit/<A ID>` ni oching.
**Kutilgan natija:**
- E: **Bekor qilingan**, rezerv 0; agent botiga "Buyurtma D-… bekor qilindi: DEMO Anvar do'koni. Sabab: DEMO: mijoz bekor qildi".
- Qoldiq S **960/10/950** (faqat C rezervi qoldi).
- 3-qadam: tahrirlash formasi ochilmaydi — A kartasiga qaytadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-20. Buyurtma bo'yicha qaytarish (yaroqli), ortiqcha qaytarish rad (A)
**Nima tekshiriladi:** Qaytarish jo'natishdagi narx va FIFO tannarxda bo'ladi, omborga kirim qiladi va qarzni kamaytiradi; jo'natilgandan ko'p qaytarilmaydi (pilot D4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-18
**Qadamlar:**
1. A kartasida **Qaytarish** ni bosing. "Jo'natilgan tovarlardan" jadvalini ko'ring.
2. Ombor = DEMO Asosiy ombor, Sabab = `Noto'g'ri tovar`, Brak ✗. Gipsokarton Miqdor = `25`. **Saqlash**.
3. Gipsokarton = `0`, Kafel = `2.88`. Izoh = `DEMO: 2 quti boshqa rangda`. **Saqlash**.
4. **Qaytarishlar** ro'yxati, **Qoldiqlar**, **Qarzlar**, ombor modulidagi **Kirimlar** (`DQ-` bo'yicha) ni tekshiring.
**Kutilgan natija:**
- Jadvalda faqat amaldagi (ikkinchi) jo'natish qatorlari: Sement 40 qop, Kafel 43.2 m², Gipsokarton 20 list "Qaytarish mumkin"; narxlar 62 000, 95 000, 68 000 (asosiy birlikka). Bekor qilingan birinchi jo'natish yo'q.
- 2-qadam: "Jo'natilgandan ko'p qaytarib bo'lmaydi (qolgan 20)."
- 3-qadam: `DQ-2026-NNNNN`, Jami 2,88 × 95 000 = **273 600**.
- Qoldiq K haqiqiy **459.68** (456,8 + 2,88). Anvar qarzi 15 084 000 − 273 600 = **14 810 400**.
- Ombor modulida `DQ-…` kirimi "Qabul qilingan", tannarx 2,88 × 70 000 = **201 600** (jo'natishdagi FIFO tannarx).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-21. Brak qaytarish brak omboriga (A)
**Nima tekshiriladi:** "Brak" belgili qaytarish faqat Sozlamalardagi brak omboriga kiradi (T-DST-SET-09) — sotiladigan qoldiq oshmaydi; qarz baribir kamayadi (pilot D4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-20, T-DST-SET-09
**Qadamlar:**
1. A → **Qaytarish**: Ombor = `DEMO Asosiy ombor`, Sabab = `Brak`, **Brak ✓** belgisini qo'ying va Ombor maydonini ko'ring. Ombor ni qo'lda yana `DEMO Asosiy ombor` qilib, Sement Miqdor = `2` → **Saqlash**.
2. Ombor = `DEMO Brak ombor`, Brak ✓, Sement `2` → **Saqlash**.
3. **Qoldiqlar**: Ombor = DEMO Asosiy ombor, keyin DEMO Brak ombor. **Qarzlar**.
**Kutilgan natija:**
- 1-qadam: Brak ✓ qo'yilganda Ombor o'zi `DEMO Brak ombor` ga o'tadi; Asosiy omborga saqlashda "Brak tovar brak omboriga qaytariladi: DEMO Brak ombor." — hech narsa yozilmaydi.
- 2-qadam: qaytarish 2 × 62 000 = **124 000**, ro'yxatda "Brak" belgisi bilan.
- DEMO Brak ombor: Sement **2**. DEMO Asosiy ombor Sement haqiqiy **960** (o'zgarmagan).
- Anvar qarzi 14 810 400 − 124 000 = **14 686 400**. A da Sement "Qaytarish mumkin" endi 38.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-22. Buyurtmasiz qaytarish va qaytarishni bekor qilish
**Nima tekshiriladi:** Buyurtmasiz qaytarishda narx — mijozning joriy narxi, tannarx — oxirgi kirim narxi; bekor qilinganda ombor va qarz tiklanadi (pilot D4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-09, T-DST-SET-21
**Qadamlar:**
1. **Qaytarishlar → Qaytarish**: Mijoz = DEMO Bunyodkor qurilish, Ombor = DEMO Asosiy ombor, Sabab = `Muddati o'tgan`. "Buyurtmasiz" blokida DEMO-GIP-125, Miqdor `1`, Narx — bo'sh. **Saqlash**.
2. **Qarzlar** (Bunyodkor) va **Qoldiqlar** (G) ni tekshiring.
3. **Qaytarishlar** ro'yxatida shu hujjatda **Bekor qilish** → sabab = `DEMO: sinov qaytarishi`.
4. Yana tekshiring.
**Kutilgan natija:**
- 1-qadam: summa **68 000** (Bunyodkorning Diler narx turida Gipsokarton narxi yo'q — standart 68 000), tannarx **52 000** (oxirgi kirim — T-DST-ORD-09).
- 2-qadam: Bunyodkor qarzi **4 932 000** (muddati o'tgan qism ham 4 932 000), G haqiqiy **126**.
- 4-qadam: hujjat "Bekor qilingan"; Bunyodkor qarzi yana **5 000 000**, G haqiqiy **125**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-23. Aksiyalarni yaratish va tekshiruvlar
**Nima tekshiriladi:** Sovg'a (N+M) va hajmga qarab chegirma aksiyalari yaratiladi; noto'g'ri qiymatlar rad etiladi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-13
**Qadamlar:**
1. **Distributsiya → Aksiyalar → Yangi aksiya**: Nomi = `DEMO Sement 10+1`, Turi = "Sovg'a (N+M)", Faol ✓, Boshlanishi = bugun, Tugashi = bo'sh, Narx turi = "Hamma mijoz", Tovar = DEMO-SEM-400, Har N = `10`, M sovg'a = `1`, Sovg'a tovar = bo'sh, Takrorlanadi ✓. **Saqlash**.
2. **Yangi aksiya**: Nomi = `DEMO Kafel 100+ m2 -5%`, Turi = "Chegirma", Tovar = DEMO-KAF-3030, Chegirma, % = `0`, Qatorda kamida = `100`. **Saqlash**.
3. Chegirma % = `5` qilib saqlang.
4. Yana bir aksiya: Turi = Sovg'a, Tovar — bo'sh, Har N = 10, M = 1 → **Saqlash**; so'ng Tovar tanlab Tugashi = kechagi sana → **Saqlash**. (Saqlanmaydi — Orqaga.)
**Kutilgan natija:**
- 2-qadamda "Chegirma 0 dan katta va 100 dan oshmasin."
- Ro'yxatda ikki aksiya: "DEMO Sement 10+1" — Sovg'a (N+M), Shart **10+1**, Muddat bugundan, Holat Faol, Qatorlarda 0; "DEMO Kafel 100+ m2 -5%" — Chegirma, Shart **−5% ≥100**.
- 4-qadamda "Sovg'a aksiyasi uchun tovarni tanlang.", keyin "Tugash sanasi boshlanishdan oldin."
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-24. 10+1 sovg'a: tahrirda qayta hisoblash, rezerv va jo'natish (P1)
**Nima tekshiriladi:** Sovg'a narxsiz alohida qator bo'lib qo'shiladi va har saqlashda qayta hisoblanadi (takrorlanuvchi); rezervga olinadi va jo'natiladi; tannarx sovg'a uchun ham hisoblanadi.
**Kim:** demo_rahbar, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-23
**Qadamlar:**
1. Yangi buyurtma: DEMO Anvar do'koni, Agent DEMO Jasur, DEMO-SEM-400 birlik `qop` `25`. **Saqlash** (buyurtma **P1**).
2. **Tahrirlash**: formada nechta qator borligini ko'ring. Miqdor = `31` → **Saqlash**.
3. **Tahrirlash**: Miqdor = `9` → **Saqlash**.
4. **Tahrirlash**: Miqdor = `20` → **Saqlash**. So'ng **Tasdiqlash**.
5. demo_ombor: **Jo'natish** (standart miqdorlar) → **Jo'natish**.
6. demo_rahbar: kartadagi tannarx va **Aksiyalar** ro'yxatini ko'ring.
**Kutilgan natija:**
- 1-qadam: qatorlar — 1) Sement 25 qop × 62 000 = 1 550 000; 2) Sement **2 qop**, narx 0, summa 0, "sovg'a" belgisi. Jami **1 550 000**.
- 2-qadam: tahrir formasida faqat 1 qator (sovg'a qatori ko'rsatilmaydi). Saqlangach sovg'a **3 qop**, Jami 1 922 000.
- 3-qadam: sovg'a qatori yo'q, Jami 558 000.
- 4-qadam: sovg'a **2 qop**, Jami **1 240 000**. Tasdiqlangach rezerv: 20 va 2. Qoldiq S 960/32/928.
- 5-qadam: jo'natish Jami **1 240 000**, "Jo'natildi" 20 va 2 qop. Qoldiq S **938/10/928**. Anvar qarzi 14 686 400 + 1 240 000 = **15 926 400**.
- 6-qadam: tannarx 22 × 40 000 = **880 000**, yalpi foyda 360 000. "DEMO Sement 10+1" qatorida "Qatorlarda" 1.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-25. Kafel ≥ 100 m² −5%: aksiya chegirmasi rol limitidan ozod
**Nima tekshiriladi:** Hajm sharti bajarilsa qatorga aksiya foizi qo'yiladi; qo'lda berilgan chegirma alohida limit bilan tekshiriladi, aksiya foizi limitga qo'shilmaydi; shart bajarilmasa aksiya olib tashlanadi.
**Kim:** demo_rahbar (limit 10%)   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-23
**Qadamlar:**
1. Yangi buyurtma: DEMO Anvar do'koni, DEMO-KAF-3030 birlik **m²** `100`. **Saqlash**.
2. **Tahrirlash**: qator % maydonini ko'ring, `11` yozing → **Saqlash**.
3. Qator % = `6` → **Saqlash**.
4. **Tahrirlash**: Miqdor = `99` → **Saqlash**.
5. 🗑 bilan buyurtmani o'chiring.
**Kutilgan natija:**
- 1-qadam: qatorda "aksiya −5%" belgisi, % = 5, summa 100 × 95 000 − 5% = **9 025 000**.
- 2-qadam: formada qator % = 0 (qo'lda berilgan qism). Saqlashda "Chegirma 11% — sizning limitingiz 10%." (aksiya 5% hisobga olinmagan).
- 3-qadam: saqlanadi; qatorda % = **10.7** (5% aksiya va 6% qo'lda ketma-ket: 100 − 95 × 94 / 100), summa 9 500 000 − 1 016 500 = **8 483 500**.
- 4-qadam: aksiya belgisi yo'q, % = 6, summa 99 × 95 000 = 9 405 000 − 564 300 = **8 840 700**.
- 5-qadamdan keyin buyurtma ro'yxatda yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-26. Aksiyalarni to'xtatish
**Nima tekshiriladi:** To'xtatilgan aksiya yangi buyurtmalarga qo'llanmaydi; ro'yxatda "Amaldagilari" / "Hammasi" filtri.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-24, T-DST-ORD-25
**Qadamlar:**
1. **Aksiyalar** da ikkala DEMO aksiya uchun **To'xtatish** ni bosing.
2. "Amaldagilari" va "Hammasi" filtrlarini almashtiring.
3. Yangi buyurtma: DEMO Anvar do'koni, DEMO-SEM-400 `20` qop. **Saqlash**. Qatorlarni ko'ring, so'ng 🗑 bilan o'chiring.
**Kutilgan natija:**
- "Amaldagilari" da DEMO aksiyalar yo'q; "Hammasi" da ikkalasi nofaol holatda.
- 3-qadamda faqat bitta qator (sovg'a yo'q), Jami 1 240 000. Buyurtma o'chiriladi.
- P1 dagi sovg'a qatori o'zgarmagan (o'tgan buyurtma qayta hisoblanmaydi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-ORD-27. Buyurtmalar ro'yxati: filtrlar va ko'rinish doirasi
**Nima tekshiriladi:** Ro'yxat holat/agent/ombor/sana bo'yicha filtrlanadi; agent faqat o'z buyurtmalarini, ombor scope'li omborchi faqat o'z omborlari buyurtmalarini ko'radi (pilot N3).
**Kim:** demo_rahbar, demo_agent, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-26
**Qadamlar:**
1. demo_rahbar: **Buyurtmalar** da filtr Holat = "Jo'natildi", keyin "Barcha holatlar" + Agent = DEMO Jasur, keyin Ombor = DEMO Asosiy ombor. Qidiruvga `Bunyodkor` yozing.
2. demo_rahbar: yangi qoralama — DEMO Anvar do'koni, DEMO-GIP-125 `1`, Ombor "avtomatik". Saqlang (o'chirmang).
3. demo_agent bilan **Buyurtmalar** va **Mijozlar** ni oching.
4. demo_ombor bilan **Buyurtmalar** ni oching.
5. demo_rahbar: 2-qadamdagi qoralamani 🗑 bilan o'chiring.
**Kutilgan natija:**
- 1-qadam: "Jo'natildi" — F va P1; Agent = DEMO Jasur — A, D, E, F, G1, G2, P1 (C yo'q); qidiruv — faqat C. "Jami" ustunida qisman jo'natilgan F uchun jo'natilgan summa ham ko'rinadi.
- 3-qadam: demo_agent faqat Agent = DEMO Jasur bo'lgan buyurtmalarni (C yo'q) va faqat DEMO Anvar do'koni mijozini ko'radi (Bunyodkor yo'q).
- 4-qadam: demo_omborda ombori DEMO Asosiy ombor bo'lgan buyurtmalar va ombori hali tanlanmagan buyurtmalar — 2-qadamdagi omborsiz qoralama ham **ko'rinadi** (ombor tasdiqda taklif qilinadi). Boshqa omborning buyurtmalari ko'rinmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Bo'lim oxiridagi nazorat holati

| Ko'rsatkich | Qiymat |
|---|---|
| DEMO Asosiy ombor | S 938 / rezerv 10 / mavjud 928; K 459.68 / 10 / 449.68; G 125 / 5 / 120 |
| DEMO Brak ombor | Sement 2 |
| FIFO qatlamlari (Asosiy) | S: 938 × 40 000; K: 456,8 × 70 000 + qaytarish 2,88 × 70 000; G: 75 × 50 000 + 50 × 52 000 |
| DEMO Anvar do'koni qarzi | 15 926 400 (jo'natishlar A 7 944 000 + F 6 800 000 + D 340 000 + P1 1 240 000 = 16 324 000 − qaytarishlar 273 600 − 124 000) |
| DEMO Bunyodkor qurilish qarzi | 5 000 000 (muddati o'tgan) |
| Ochiq buyurtmalar | C — Tasdiqlangan (rezervda); A — Yetkazildi; D — Yetkazildi; F, P1 — Jo'natildi; E, G1, G2 — Bekor qilingan |

## Ma'lum cheklovlar va kuzatish kerak bo'lgan joylar
- Kredit limiti = qarz + tasdiqlangan/yig'ilayotgan/qisman jo'natilgan buyurtmalarning jo'natilmagan qismi + shu buyurtma (T-DST-ORD-06). Shu sababli bir mijozga ketma-ket bir nechta yirik buyurtma tasdiqlansa, keyingilari rahbar ruxsatini talab qiladi (ORD-08, ORD-10).
- O'chirilgan qoralama hujjat raqami qayta berilmaydi (oxirgi raqam saqlanadi) — raqamlar ketma-ketligida "teshik" bo'lishi normal.
- Brak ombori Sozlamalarda belgilanmagan bo'lsa, "Brak" belgisi qaytgan tovarni sotuvdan avtomatik chiqarmaydi — omborni qo'lda tanlang (T-DST-SET-09, ORD-21).
- Qisman jo'natilgan buyurtma "Qolganidan voz kechish" bilan yopilganda jo'natilgan qismi to'langan bo'lsa — darhol "Yopildi"; qolgani hali jo'natilishi mumkin bo'lgan holatda avtomatik yopilmaydi (ataylab).
- Mini App savati jami summasi aksiya chegirmasi va sovg'asiz ko'rsatiladi ("Aksiya chegirmasi va sovg'alar buyurtma qabul qilinganda hisoblanadi.") — ataylab, yakuniy summa ERP'da.
