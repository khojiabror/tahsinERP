# 4. Telegram bot va Mini App

Bu bo'lim botni har rol ko'zi bilan tekshiradi: **agent** (kun boshi, marshrut, "Keldim", Mini App buyurtma, to'lov, buyurtmasiz tashrif, yangi nuqta, jonli joylashuv, kassaga topshirish, kun yakuni, muammo), **rahbar** (limit ruxsati, hisobdan chiqarish tasdig'i, kunlik yakun), **omborchi** (Mini App'da yig'ish va ko'r sanoq), **mijoz** (xabarlar, qarzim, akt sverki), **ekspeditor** (2-bosqich: reys, yetkazish, imzo/qisman, rad, to'lov, xarajat, reysni yakunlash — reysni ofis yopadi), ovozli buyurtma va oflayn holatlar.

Ikki qismda bajariladi (00_INTRO, "Bajarish tartibi"):
- **T-DST-BOT-01 … 25** — buyurtmalar blokidan (02) keyin, pul/ombor blokidan (03) **oldin**;
- **T-DST-BOT-26 … 34** — reyslar rejalangandan keyin (05: T-DST-DLV-04 … 06, so'ng DLV-07 … 09 bilan aralash).

Qoidalar:
- Bot tugmalari matni aynan qo'shtirnoqsiz yozilgan (masalan **▶️ Kunni boshlash**). Bot javoblari qo'shtirnoq ichida; "…" — o'zgaruvchan qism (raqam, sana).
- Joylashuvni ikki usulda yuborish mumkin: **📍 Joylashuvni yuborish** tugmasi (hozirgi joy, GPS aniqligi bilan) yoki 📎 → Joylashuv → xaritada nuqtani tanlash (aniqligi yo'q). Masofa testlarida **xaritada nuqta tanlang** va ko'rsatilgan koordinatani qo'ying.
- Mini App va kamera — **mobil Telegram ilovasida**. Har kamera testini Android'da ham, iOS'da ham bajaring (Izohga qurilmani yozing).
- Agar qadam bajarilgan kun ORD blokidan boshqa kun bo'lsa, "bugungi" yig'indilar (kun yakuni, rahbar yakuni) farq qiladi — testda ikki variant ko'rsatilgan.

Boshlang'ich holat (ORD blokidan keyin): DEMO Anvar do'koni qarzi **15 926 400** (muddati kelmagan); DEMO Asosiy ombor: S 938/10/928, K 459.68/10/449.68, G 125/5/120. Agent — DEMO Jasur, mijoz telefonida DEMO Anvar do'koni ulangan.

---

### T-DST-BOT-01. Ulanish kodining chekka holatlari va botdan uzilish
**Nima tekshiriladi:** Kod bir martalik; yangi kod eskisini bekor qiladi; noto'g'ri kod aniq xabar beradi; `/stop` bog'lanishni uzadi, qayta ulash mumkin (pilot P10).
**Kim:** demo_rahbar (web), agent telefoni, ulanmagan 6-telefon (istalgan hamkasb)   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-24
**Qadamlar:**
1. Ulanmagan telefondan botga `salom` yozing.
2. Shu telefondan `/start ABCDEFGHJK` yuboring.
3. T-DST-SET-24 da agent ishlatgan kodni shu telefondan yuboring: `/start <eski kod>`.
4. Agent telefonida `/stop` yuboring. So'ng istalgan matn yozing.
5. Web: Agentlar va jamoalar → DEMO Jasur qatoridagi Telegram tugmasi → kod 1 ni yozib oling; yana Telegram tugmasi → kod 2.
6. Agent telefonida `/start <kod 1>`, so'ng `/start <kod 2>`.
**Kutilgan natija:**
- 1-qadam: "Assalomu alaykum! Botdan foydalanish uchun rahbaringizdan ulanish kodini oling va havolani oching yoki /start KOD yuboring."
- 2- va 3-qadam: "⚠️ Kod noto'g'ri yoki muddati o'tgan."
- 4-qadam: "Bot hisobingizdan uzildi. Qayta ulash uchun yangi kod oling.", menyu yo'qoladi; keyingi xabarga 1-qadamdagi salomlashuv matni.
- 6-qadam: kod 1 — "⚠️ Kod noto'g'ri yoki muddati o'tgan." (kod 2 uni bekor qilgan); kod 2 — "✅ DEMO Jasur, siz ulandingiz. Rol: agent." va agent menyusi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-02. Rol bo'yicha cheklovlar va guruh chati
**Nima tekshiriladi:** Rolga tegishli bo'lmagan amal bajarilmaydi; bot guruhlarda javob bermaydi.
**Kim:** omborchi, agent, mijoz telefonlari   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-01
**Qadamlar:**
1. Omborchi telefonida matn sifatida `🗺 Marshrut` yuboring (nusxa olib).
2. Agent telefonida `📦 Ombor` yuboring.
3. Mijoz telefonida `🔎 Mijoz` yuboring, so'ng **💳 Qarzim** ni bosing.
4. Botni sinov guruhiga qo'shing va guruhda `/start` yozing.
**Kutilgan natija:**
- 1–3-qadam (birinchi qismi): "Bu amal sizning rolingiz uchun emas."
- 3-qadam (Qarzim) mijozda ishlaydi (javob T-DST-BOT-18 da batafsil tekshiriladi).
- 4-qadam: bot guruhda hech narsa yozmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-03. Kun boshi: selfi majburiy rejimi, kunsiz "Keldim"
**Nima tekshiriladi:** Joylashuv → selfi → kun boshlanadi; selfi majburiy bo'lsa o'tkazib yuborib bo'lmaydi va "Keldim" kunni o'zi boshlamaydi; selfi Dala nazoratida ko'rinadi (pilot A1, A2).
**Kim:** demo_rahbar (web), agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-01
**Qadamlar:**
1. Web: Sozlamalar → "Kun boshida selfi majburiy" ✓ → **Saqlash**.
2. Agent: **🔎 Mijoz** → `Anvar` → **DEMO Anvar do'koni** → **📍 Keldim** → joylashuv yuboring (xaritada `41.286000, 69.203400`).
3. **▶️ Kunni boshlash** → **📍 Joylashuvni yuborish**.
4. Selfi o'rniga `salom` yozing.
5. Kamera bilan selfi yuboring.
6. **▶️ Kunni boshlash** ni yana bosib joylashuv yuboring.
7. Web: **Distributsiya → Dala nazorati** (bugun). Keyin Sozlamalar → "Kun boshida selfi majburiy" belgisini olib **Saqlash**.
**Kutilgan natija:**
- 2-qadam: "⚠️ Avval ish kunini boshlang."
- 3-qadam: "Ish kunini boshlash uchun joylashuvingizni yuboring." (tugmalar 📍 Joylashuvni yuborish, ✖️ Bekor qilish) → joylashuvdan keyin "📸 Endi selfi yuboring (kamera bilan, hozir tushiring)." — ⏭ O'tkazib yuborish tugmasi **yo'q**.
- 4-qadam: yana selfi so'raladi.
- 5-qadam: "✅ Ish kuni boshlandi (HH:mm). Omad!" + "📡 Rahbaringiz joylashuvingizni ko'rishi uchun jonli joylashuvni yoqing (📡 Jonli joylashuv tugmasi)." va bugungi marshrut.
- 6-qadam: selfi so'ralmaydi, o'sha boshlanish vaqti qaytadi (yangi kun ochilmaydi).
- 7-qadam: Dala nazoratida DEMO Jasur qatorida selfi rasmi (bosilsa kattalashadi), "Kun boshi" vaqti va joyi. Rasm ko'rinmasa — "Rasmlarni ko'rish uchun Sozlamalarda bot manzilini (Bot ochiq manzili) kiriting." (T-DST-SET-06 ni tekshiring).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-04. Bugungi marshrut va qarz belgisi
**Nima tekshiriladi:** Marshrut tashrif kunlari bo'yicha, faqat agentning o'z mijozlari; qarz summasi va holat belgilari (pilot A4).
**Kim:** agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-03
**Qadamlar:**
1. **🗺 Marshrut** ni bosing.
2. **🔄 Yangilash** ni bosing.
**Kutilgan natija:**
- "🗺 Bugungi marshrut: 1 ta mijoz, 0 tasiga borildi." (bugun yakshanba bo'lsa — "Bugun uchun marshrutda mijoz yo'q. 🔎 Mijoz tugmasi bilan qidiring.")
- Tugma: "⏳ DEMO Anvar do'koni · 15 926 400" (muddati o'tgan qarz yo'q — ❗ belgisi yo'q). DEMO Bunyodkor qurilish ro'yxatda yo'q (agenti yo'q).
- 2-qadamda xabar o'rnida yangilanadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-05. Mijoz qidirish va ko'rinish doirasi
**Nima tekshiriladi:** Agent nom, telefon yoki STIR bo'yicha faqat o'z mijozlarini topadi (pilot N3).
**Kim:** agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-03
**Qadamlar:**
1. **🔎 Mijoz** → `Bunyod`.
2. **🔎 Mijoz** → `300000012`.
3. **🔎 Mijoz** → `300000011`.
4. **🔎 Mijoz** → `90 111`.
**Kutilgan natija:**
- 1- va 2-qadam: "Hech narsa topilmadi." (Bunyodkor agentga biriktirilmagan).
- 3- va 4-qadam: "Topildi: 1" va "DEMO Anvar do'koni" tugmasi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-06. Mijoz kartasi, xarita va "Keldim" radiusi
**Nima tekshiriladi:** Karta qarz, limit va narx turini ko'rsatadi; "Keldim" faqat radius ichida (200 m) qabul qilinadi; tashrif ochilgach tugmalar o'zgaradi (pilot A3).
**Kim:** agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-04
**Qadamlar:**
1. Marshrutda **DEMO Anvar do'koni** ni bosing.
2. **🧭 Xarita** ni bosing.
3. **📍 Keldim** → 📎 → Joylashuv → xaritada `41.288300, 69.203400` (taxminan 300 m shimolda) nuqtani yuboring.
4. **📍 Keldim** → `41.286000, 69.203400` (taxminan 44 m) nuqtani yuboring.
**Kutilgan natija:**
- 1-qadam karta: "🏪 DEMO Anvar do'koni / 📍 Chilonzor 9-kvartal, 12, Bozor yonida / ☎️ +998901111111 / Narx turi: Chakana / Qarz: 15 926 400 so'm (muddati o'tgan: 0) / Limit: 20 000 000 so'm, muddat: 7 kun". Tugmalar: 📍 Keldim, 🛒 Buyurtma, 💵 To'lov, 🧭 Xarita, ⬅️ Orqaga. ("💡 Tavsiya" bloki faqat kamida 2 xil kunda olingan tovarlar bo'lsa chiqadi — ORD bloki bir kunda bajarilgan bo'lsa, yo'q.)
- 2-qadam: bot mijoz joylashuvini xarita nuqtasi sifatida yuboradi.
- 3-qadam: "⚠️ Siz mijozdan 300 m uzoqdasiz (ruxsat: 200 m)." (±1 m).
- 4-qadam: "✅ Tashrif boshlandi (44 m)." va karta qayta keladi: "🟢 Tashrif ochiq.", tugmalar — 🛒 Buyurtma, 💵 To'lov, 🚫 Buyurtmasiz, 📷 Foto, 🧭 Xarita, ⬅️ Orqaga (📍 Keldim yo'q).
- Izoh: hozirgi joyni GPS aniqligi bilan yuborsangiz, aniqlik (ko'pi bilan radiusga teng) qo'shiladi — ruxsat chegarasi kengayadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-07. Mini App'da buyurtma: katalog, birlik, skaner, imzo (M1)
**Nima tekshiriladi:** Agent do'konda Mini App'da buyurtma beradi: qidiruv, birlik tanlash, kamera bilan shtrix-kod, savat, yetkazish sanasi, izoh, imzo; buyurtma ofisda darhol "Yangi" holatda (pilot B1, B11, M3).
**Kim:** agent telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-06
**Qadamlar:**
1. Kartada **🛒 Buyurtma** ni bosing. Sekundomerni ishga tushiring. Yuqoridagi mijoz satri va "Hammasi" + kategoriya chiplarini ko'ring.
2. Qidiruvga `sement` yozing. Sement qatorida birlik = `poddon (×40)`, **Qo'shish**, so'ng **+** bilan miqdor = 2.
3. 📷 tugmasini bosing. Kafel qutisi shtrix-kodini (`4780000000045`, qog'ozga chop etilgan yoki boshqa ekranda) kameraga tuting. Kamerani burib, yana tuting. So'ng boshqa noma'lum shtrix-kodni tuting. **Tayyor**.
4. **Savat** yorlig'iga o'ting. Yetkazish sanasi = ertaga, Izoh = `DEMO: Mini App sinovi`, imzo maydoniga barmoq bilan imzo chizing.
5. Pastdagi asosiy tugma — **Buyurtma berish · …** ni bosing (iloji bo'lsa ketma-ket ikki marta tez). Sekundomerni to'xtating.
6. Web: Buyurtmalar ro'yxatini yangilang va yangi buyurtmani oching (buyurtma **M1**).
**Kutilgan natija:**
- 1-qadam: "DEMO Anvar do'koni", "Qarz: 15 926 400 so'm". Narxlar: Sement "62 000 so'm / qop", Kafel "136 800 so'm / quti" (odatiy birlik — quti), Gipsokarton "68 000 so'm / list"; "Qoldiq": 928, 449.68, 120 (savdo omborlaridagi mavjud qoldiq).
- 2-qadam: Sement narxi "2 480 000 so'm / poddon".
- 3-qadam: "Qo'shildi: Kafel 30×30 — 1 quti", keyin "— 2 quti"; noma'lum kod — "Topilmadi: <kod>". Kod kadrda turganda qayta sanalmaydi.
- 4-qadam savat: "2 poddon × 2 480 000 = 4 960 000 so'm", "2 quti × 136 800 = 273 600 so'm", Jami **5 233 600 so'm**.
- 5-qadam: "Buyurtma D-… qabul qilindi" (toast), Mini App yopiladi; chatda "✅ Buyurtma D-… qabul qilindi: 5 233 600 so'm. Ofis tasdiqlaydi." Ikki marta bosilganda ham **bitta** buyurtma. Vaqt ≤ 3 daqiqa (Izohga yozing, pilot B1).
- Web: M1 holati Yangi, Manba bot/Mini App, Agent DEMO Jasur, qatorlar 2 poddon (= 80 qop) va 2 quti (= 2.88 m²), Jami 5 233 600, og'irlik 4048.96 kg.
- Rahbar botiga: "Buyurtma D-… (DEMO Anvar do'koni, 5 233 600 so'm) kredit shartidan chiqdi — tasdiqlash kerak: Qarz + jo'natilmagan buyurtmalar + shu buyurtma 21 160 000 — limit 20 000 000 dan oshadi." + ✅ Tasdiqlash (15 926 400 + 0 + 5 233 600 = 21 160 000; Anvarda tasdiqlangan, jo'natilmagan buyurtma yo'q — "(shundan …)" qismi chiqmaydi).
- Marshrutda Anvar belgisi ✅ (natija — buyurtma).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-08. Rahbar botda limitdan oshishga ruxsat beradi; eskirgan tugma
**Nima tekshiriladi:** Rahbar bot tugmasi bilan buyurtmani tasdiqlaydi (`DstOverrideCredit` ERP'da tekshiriladi); bekor qilingan buyurtmaning eski tugmasi ishlamaydi (pilot B4, A8).
**Kim:** rahbar telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-07, T-DST-ORD-19
**Qadamlar:**
1. Rahbar telefonida E buyurtmasi haqidagi eski xabardagi (T-DST-ORD-06) **✅ Tasdiqlash** ni bosing.
2. M1 xabaridagi **✅ Tasdiqlash** ni bosing.
3. Web: M1 kartasi va **Qoldiqlar** ni oching. Agent va omborchi botlarini ko'ring.
**Kutilgan natija:**
- 1-qadam: "⚠️ Holatni o'zgartirib bo'lmaydi: Bekor qilingan → Tasdiqlangan." E o'zgarmaydi.
- 2-qadam: tugmalar xabardan olib tashlanadi, "✅ Buyurtma D-… tasdiqlandi."
- Web: M1 Tasdiqlangan, Ombor DEMO Asosiy ombor, "Limitdan oshishga ruxsat berdi: DEMO Rahbar — Telegram". Rezerv: 80 qop, 2.88 m². Qoldiq S **938/90/848**, K **459.68/12.88/446.8**.
- Agentga: "Buyurtma D-… tasdiqlandi: DEMO Anvar do'koni, 5 233 600 so'm." Omborchiga: "📦 Yig'ish kerak: … 2 qator, 4049 kg, 5 233 600 so'm …".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-09. Naqd to'lov va takroriy bosish
**Nima tekshiriladi:** Agent naqd to'lov oladi — pul agent qo'lida, mijoz qarzi darhol kamayadi; tugmani takror bosish ikkinchi to'lov yaratmaydi (pilot D2, D7).
**Kim:** agent telefoni, mijoz telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-06
**Qadamlar:**
1. Anvar kartasida (tashrif ochiq) **💵 To'lov** ni bosing.
2. `abc` yozing.
3. `1 500 000` yozing. Ko'rsatilgan usul tugmalarini yozib oling.
4. **Naqd** ni ketma-ket ikki marta tez bosing.
5. Web: **To'lovlar** ro'yxati va **Qarzlar**.
**Kutilgan natija:**
- 1-qadam: "💵 DEMO Anvar do'koni / Qarz: 15 926 400 so'm. Olingan summani yozing:"
- 2-qadam: "Summani raqam bilan yozing, masalan 250000."
- 3-qadam: "1 500 000 so'm. To'lov usuli:" — tugmalar faqat **Naqd, Karta, Click** (T-DST-SET-07) va ✖️ Bekor qilish.
- 4-qadam: "✅ To'lov DP-… qabul qilindi: 1 500 000 so'm. Qo'lingizda naqd: 1 500 000 so'm." To'lovlar ro'yxatida **bitta** DP yozuvi: usul Naqd, Qabul qildi DEMO Jasur, Kassa/hisob — "Agent qo'lida (naqd)".
- Anvar qarzi **14 426 400**. Mijozga: "To'lovingiz qabul qilindi: 1 500 000 so'm (DP-…). Qolgan qarz: 14 426 400 so'm."
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-10. Karta to'lovi darhol hisobga tushadi
**Nima tekshiriladi:** Naqdsiz usul sozlamadagi hisobga darhol yoziladi, agent qo'lidagi naqdga qo'shilmaydi (pilot D3).
**Kim:** agent telefoni, demo_kassir (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-09
**Qadamlar:**
1. **💵 To'lov** → `500000` → **Karta**.
2. Web (kassir): **Moliya → Boshqaruv → Tranzaksiyalar** da "Distributsiya to'lovi" bo'yicha qidiring.
**Kutilgan natija:**
- "✅ To'lov DP-… qabul qilindi: 500 000 so'm. Qo'lingizda naqd: 1 500 000 so'm." (naqd o'zgarmadi).
- Moliyada kirim yozuvi: hisob **DEMO Bank hisobi**, summa 500 000, kontragent DEMO Anvar do'koni, maqsad "Distributsiya to'lovi DP-…". 1 500 000 naqd uchun yozuv **yo'q** (kassaga topshirilmagan).
- Anvar qarzi **13 926 400**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-11. Botdan yangi savdo nuqtasi, tasdiqlanmagan mijoz bloki va tasdiqlash
**Nima tekshiriladi:** Agent nuqta qo'shadi (nom, telefon, joylashuv, mo'ljal, foto); nuqta tasdiqlanmaguncha buyurtmasi tasdiqlanmaydi; rahbar web'da tasdiqlaydi; qayta yuborish dublikat yaratmaydi (pilot M6).
**Kim:** agent telefoni, rahbar telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-03
**Qadamlar:**
1. **➕ Yangi nuqta** → `D` → `DEMO Yangi nuqta`.
2. Telefon: `90 123 45 67`. Joylashuv: xaritada `41.290000, 69.210000`. Mo'ljal: `Metro yonida`. Foto: do'kon rasmini yuboring. Tekshirish xabarida **✅ Saqlash**.
3. Yana **➕ Yangi nuqta** → `DEMO Yangi nuqta` → xuddi shu ma'lumotlar → **✅ Saqlash**.
4. Rahbar botini ko'ring. Agent: **🔎 Mijoz** → `Yangi` → kartani oching.
5. Kartadagi **🛒 Buyurtma** → Gipsokarton `10` list → **Buyurtma berish**.
6. Web: shu buyurtmada **Tasdiqlash**.
7. Web: **Mijozlar** → "Tasdiq kutilmoqda" filtri → DEMO Yangi nuqta → kartani oching, so'ng ro'yxatda **Tasdiqlash**.
8. Web: 5-qadamdagi buyurtmani **Bekor qilish** → sabab `DEMO: sinov buyurtmasi`. (Rahbar botidagi limit xabaridagi ✅ ni **bosmang**.)
**Kutilgan natija:**
- 1-qadam: "➕ Yangi savdo nuqtasi. Nuqta (do'kon) nomini yozing:" → `D` ga "Nom 2–50 belgi bo'lsin."
- 2-qadam: tekshirish xabari "🏪 DEMO Yangi nuqta / ☎️ +998901234567 / 📍 <yuborilgan koordinata> / 🧭 Metro yonida / 📷 bor / Saqlaysizmi?" → "✅ Nuqta qo'shildi. Ofis tasdiqlagach, unga buyurtmalar tasdiqlanadi."
- 3-qadam: "Bu nuqta yaqinda qo'shilgan — o'shasi ochildi." — ikkinchi mijoz yaratilmaydi.
- 4-qadam: rahbarga "🆕 Yangi savdo nuqtasi: DEMO Yangi nuqta (+998901234567), agent DEMO Jasur. Distributsiya → Mijozlar sahifasida tasdiqlang." Qidiruvda "⚠️ DEMO Yangi nuqta"; kartada "⚠️ Mijoz hali tasdiqlanmagan.", Narx turi — (standart).
- 5-qadam: buyurtma 680 000 qabul qilinadi (Yangi). Rahbarga limit xabari (limit 0 — faqat oldindan to'lov).
- 6-qadam: "Mijoz hali tasdiqlanmagan — avval mijoz kartasini tasdiqlang."
- 7-qadam: kartada sariq "Bu nuqtani agent qo'shgan — ma'lumotni tekshirib, ro'yxatda tasdiqlang." va "Nuqta fotosi"; tasdiqlangach "Tasdiq kutilmoqda" belgisi yo'qoladi. Agent — DEMO Jasur, Kredit limiti 0.
- 8-qadam: buyurtma Bekor qilingan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-12. Buyurtmasiz tashrif (sabab majburiy) va tokcha rasmi
**Nima tekshiriladi:** Buyurtmasiz tashrifga sabab tanlanadi; rasm tashrifga biriktiriladi va Dala nazoratida ko'rinadi (pilot A5, A6).
**Kim:** agent telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-11
**Qadamlar:**
1. DEMO Yangi nuqta kartasi → **📍 Keldim** → xaritada `41.290000, 69.210000`.
2. **🚫 Buyurtmasiz** → ro'yxatni ko'ring → **Do'kon yopiq**.
3. **📷 Foto** → javon rasmini yuboring.
4. Web: **Dala nazorati** → DEMO Jasur → **Batafsil**.
**Kutilgan natija:**
- 1-qadam: "✅ Tashrif boshlandi (0 m)."
- 2-qadam: "Buyurtma bo'lmagan sababni tanlang:" — Do'kon yopiq, Pul yo'q, Tovar bor, Egasi yo'q, ✖️ Bekor qilish. Tanlangach "✅ Saqlandi: Do'kon yopiq." va marshrut (DEMO Yangi nuqta — 🚫, rejadan tashqari).
- 3-qadam: "Rasmni yuboring (javon, vitrina)." → "✅ Rasm saqlandi."
- 4-qadam: tashriflar jadvalida ikki qator: DEMO Anvar do'koni (Keldi vaqti, Masofa 44 m, Natija Buyurtma) va DEMO Yangi nuqta (0 m, Buyurtmasiz, sabab "Do'kon yopiq", foto, "rejadan tashqari"); statik joylashuv yuborilgani "statik" belgisi bilan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-13. Jonli joylashuv
**Nima tekshiriladi:** Jonli joylashuv faqat ish kuni ochiq paytda yoziladi; rahbar uni Dala nazoratida ko'radi.
**Kim:** agent telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-03
**Qadamlar:**
1. **📡 Jonli joylashuv** ni bosing.
2. 📎 → Joylashuv → "Jonli joylashuvni ulashish" → 15 daqiqa. 2–3 daqiqa yuring.
3. Web: Dala nazorati (bugun) va DEMO Jasur → Batafsil → "Joylashuv izi".
**Kutilgan natija:**
- 1-qadam: "Jonli joylashuvni yoqish: 📎 → Joylashuv → «Jonli joylashuvni ulashish» → 8 soat. …"
- 2-qadam: "📡 Jonli joylashuv qabul qilindi. Ish kuni davomida rahbaringiz joylashuvingizni ko'radi." Keyingi yangilanishlarga bot javob yozmaydi.
- 3-qadam: "So'nggi joylashuv" ustunida 🟢 "Jonli joylashuv faol"; izda nuqtalar 30 soniyada ko'pi bilan bittadan, manba "jonli".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-14. Agent naqdini kassaga topshirish
**Nima tekshiriladi:** Agent qo'lidagi barcha naqd bitta topshirish hujjatiga bog'lanadi; kassir tasdiqlaguncha kutadi; rahbarga xabar (pilot D2).
**Kim:** agent telefoni, rahbar telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-10
**Qadamlar:**
1. **💵 Kassa** ni bosing.
2. **📤 Kassaga topshirish** → **✅ Tasdiqlash**.
3. Yana **💵 Kassa**.
**Kutilgan natija:**
- 1-qadam: "💵 Qo'lingizdagi naqd: 1 500 000 so'm. Bugun yig'ilgan: 2 000 000 so'm." va 📤 tugmasi.
- 2-qadam: "1 500 000 so'mni kassaga topshirasizmi? Kassir sanab qabul qiladi." → "✅ Topshirish DK-…: 1 500 000 so'm. Kassir qabul qilishini kuting."
- Rahbarga: "DEMO Jasur kassaga 1 500 000 so'm topshirdi — qabul qiling."
- 3-qadam: "Qo'lingizdagi naqd: 0 so'm", 📤 tugmasi yo'q. (Kassir qabul qilishi — T-DST-PAY-03.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-15. Kunni yakunlash
**Nima tekshiriladi:** Kun yakuni joylashuv bilan; bot tashriflar, buyurtmalar, yig'ilgan va qo'ldagi pulni ko'rsatadi.
**Kim:** agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-12, T-DST-BOT-14
**Qadamlar:**
1. **🏁 Kunni yakunlash** → joylashuv yuboring.
**Kutilgan natija:**
- "🏁 Kun yakuni / Tashriflar: 2/1 (buyurtmasiz: 1) / Buyurtmalar: … / Yig'ilgan: 2 000 000 so'm / Qo'lingizda naqd: 0 so'm".
- Tashriflar 2/1: borilgan mijozlar 2 (Anvar, Yangi nuqta) / rejadagi 1 (Anvar).
- Buyurtmalar — bugun yaratilgan, agenti DEMO Jasur bo'lgan, bekor qilinmagan: BOT bloki ORD blokidan keyingi kunda bajarilgan bo'lsa "1 ta, 5 233 600 so'm" (M1); bir kunda bajarilgan bo'lsa "5 ta, 26 997 600 so'm" (A 7 944 000 + D 340 000 + F 12 240 000 + P1 1 240 000 + M1 5 233 600).
- Dala nazoratida "Kun yakuni" vaqti to'ldirilgan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-16. Muammo yozish (⚠️ Muammo)
**Nima tekshiriladi:** Xodim botdan matn yoki skrinshot bilan muammo yozadi; P-raqam beriladi; rahbarga xabar; web jurnalda ko'rinadi (pilot 7-bo'lim).
**Kim:** agent telefoni, rahbar telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-01
**Qadamlar:**
1. **⚠️ Muammo** → `abc`.
2. **⚠️ Muammo** → skrinshotni izoh bilan yuboring: `DEMO: Mini App'da kafel rasmi chiqmadi`.
3. Web: **Distributsiya → Xatolar jurnali**.
**Kutilgan natija:**
- 1-qadam: "Muammoni yozing: …" → `abc` ga "Biroz batafsilroq yozing (kamida 5 belgi) yoki rasm yuboring."
- 2-qadam: "✅ Qayd qilindi: P-001. Rahbarga xabar berildi, rahmat!" (raqam ketma-ket). Rahbarga "⚠️ Yangi muammo P-001 (DEMO Jasur): DEMO: Mini App'da kafel rasmi chiqmadi".
- 3-qadam: P-001 — Yangi, daraja "baholanmagan", Kim DEMO Jasur, Qayerda "bot", rasm bor.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-17. Rahbar: bugungi yakun va kunlik avtomatik yakun
**Nima tekshiriladi:** Rahbar istalgan paytda kun yakunini so'raydi; har kuni 20:00 (Toshkent) da avtomatik keladi (pilot A8).
**Kim:** rahbar telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-15
**Qadamlar:**
1. **📊 Bugungi yakun** ni bosing.
2. 20:00 dan keyin rahbar chatini tekshiring.
**Kutilgan natija:**
- "Kun yakuni <bugun> / Buyurtmalar: N ta, … so'm / Jo'natildi: … so'm / Yig'ilgan pul (to'lovlar): … so'm / Umumiy qarz: … so'm (muddati o'tgan: …) / Faol agentlar: 1/1" va "Kun yetakchisi: DEMO Jasur — … so'm".
- "Buyurtmalar" — bugun yaratilgan barcha bekor qilinmagan buyurtmalar (agentsizlar ham); "Yig'ilgan pul (to'lovlar)" — bugungi barcha to'lovlar (shu kungacha: 2 000 000 agent to'lovlari; "Tushum" so'zi faqat jo'natish − qaytarish ma'nosida — panel va hisobotlarda); "Umumiy qarz" = **Qarzlar** sahifasidagi jami (Anvar 13 926 400 + Bunyodkor 5 000 000 = **18 926 400**), muddati o'tgan 5 000 000. Faol agentlar 1/1 (ekspeditor hisobga kirmaydi).
- 2-qadam: xuddi shunday matn kuniga bir marta avtomatik.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-18. Mijoz: holat xabarlari, qarzim, akt sverki
**Nima tekshiriladi:** Ulangan mijoz jo'natildi/yetkazildi/to'lov xabarlarini oladi; botda qarzini va akt sverkini ko'radi (pilot A7, D6).
**Kim:** mijoz (Anvar) telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-10
**Qadamlar:**
1. Chatdagi oldingi xabarlarni ko'ring.
2. **💳 Qarzim** ni bosing.
3. **📄 Akt sverki** → **Shu oy**.
**Kutilgan natija:**
- 1-qadam: ORD va BOT bloklaridagi xabarlar: A jo'natildi (ikki marta), F jo'natildi, D **yetkazildi** (o'zi olib ketish — bitta xabar, "jo'natildi" yo'q), P1 jo'natildi, A yetkazildi, ikki to'lov (1 500 000 va 500 000). Xabarlar mijozning Telegram tilida.
- 2-qadam: "💳 DEMO Anvar do'koni / Qarz: 13 926 400 so'm / muddati kelmagan: 13 926 400 / muddati o'tgan: 0 / Limit: 20 000 000 so'm, to'lov muddati 7 kun", "So'nggi to'lovlar:" — "• <sana> — 500 000 so'm", "• <sana> — 1 500 000 so'm".
- 3-qadam: "📄 Akt sverki: DEMO Anvar do'koni / 01.MM.yyyy — <bugun> / Boshlang'ich qoldiq: 0 so'm", yozuvlar (+ jo'natishlar, − bekor qilish, qaytarishlar, to'lovlar), "Jami: +24 268 000 / −10 341 600 / Yakuniy qoldiq: 13 926 400 so'm".
  Hisob: + (7 944 000 × 2 + 6 800 000 + 340 000 + 1 240 000) = 24 268 000; − (7 944 000 bekor + 273 600 + 124 000 + 1 500 000 + 500 000) = 10 341 600.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-19. Omborchi: Mini App'da yig'ish va jo'natish (M1)
**Nima tekshiriladi:** Omborchi Mini App'da tasdiqlangan buyurtmani skaner bilan yig'adi; quti/poddon shtrix-kodi koeffitsient bilan sanaladi; ortiqcha va buyurtmada yo'q tovar ogohlantiriladi; "Jo'natish" web'dagi bilan bir xil natija beradi (pilot B10).
**Kim:** omborchi telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-08, T-DST-BOT-18
**Qadamlar:**
1. **📦 Ombor** → **📦 Ombor ilovasi**. **Yig'ish** yorlig'ini oching.
2. M1 ni oching. Asosiy tugma **Yig'ishni boshlash** ni bosing.
3. Kod maydoniga (yoki 📷 bilan) ketma-ket: `4780000000021`, `4780000000021`, `4780000000021`, `4780000000045`, `4780000000045`, `4780000000052`, `12345`.
4. **Jo'natish** → tasdiqlang.
5. Web: M1 kartasi, **Qoldiqlar**, **Qarzlar**. Mijoz chatini ko'ring.
**Kutilgan natija:**
- 1-qadam: ro'yxatda M1 — "tasdiqlangan · <sana> · 4048.96 kg" (va C — Bunyodkor).
- 2-qadam: qatorlar "DEMO-SEM-400 · kerak 80 qop", "DEMO-KAF-3030 · kerak 2.88 m²"; holat "yig'ilmoqda".
- 3-qadam: "+40 …Sement M400" ikki marta (80 qop), uchinchisida "Ortiqcha: …"; Kafel "+1.44" ikki marta (2.88); `…052` — "Buyurtmada yo'q: …Gipsokarton…"; `12345` — "Topilmadi: 12345". "Yig'ildi: 2 / 2".
- 4-qadam: "Yig'ilgan miqdor jo'natiladi. Davom etasizmi?" → "Jo'natildi: DJ-…".
- Web: M1 Jo'natildi, jo'natish 5 233 600, tannarx 80 × 40 000 + 2,88 × 70 000 = **3 401 600**. Qoldiq S **858/10/848**, K **456.8/10/446.8**. Anvar qarzi 13 926 400 + 5 233 600 = **19 160 000**.
- Mijozga: "Buyurtmangiz D-… jo'natildi: 5 233 600 so'm. Joriy qarzingiz: 19 160 000 so'm."
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-20. Omborchi: Mini App'da "ko'r" sanoq
**Nima tekshiriladi:** Ochiq sanoq Mini App'da skaner bilan to'ldiriladi; tizim qoldig'i Mini App'da ham, web qoralamada ham ko'rsatilmaydi; qutidagi shtrix-kod koeffitsient bilan qo'shiladi; miqdorni qo'lda kiritish mumkin; sanoq ochiqligida sanalgan tovar ombordan chiqmaydi, sanalmagani chiqadi.
**Kim:** demo_ombor (web), demo_rahbar (web), omborchi telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-19
**Qadamlar:**
1. Web: **Distributsiya → Inventarizatsiya** → **Yangi sanoq**: Ombor = DEMO Asosiy ombor, Turi = Nazorat, Izoh = `DEMO bot sinovi`.
2. Mini App → **Inventarizatsiya** yorlig'i → shu sanoqni oching.
3. Ketma-ket: `4780000000014` uch marta, `4780000000021` bir marta, `4780000000045` bir marta. Gipsokarton qatorini bosib `125` kiriting.
4. Web: sanoq kartasini yangilang.
5. Web (demo_ombor): **Hisobdan chiqarish → Yangi**: Asosiy, Sabab = Sinish, DEMO-SEM-400 `1` → Saqlash → Tasdiqqa yuborish. demo_rahbar: **Tasdiqlash va chiqarish**. So'ng **Rad etish** → sabab `DEMO: sanoq ochiq`.
6. Web: **Bekor** (sanoq) → sabab `DEMO: bot sinovi tugadi`.
**Kutilgan natija:**
- 2-qadam: "Sanaldi: 0 / 4" (4 qator: Sement, Kafel, Gipsokarton, Kafel 2-nav); qatorlarda tizim qoldig'i **yo'q**.
- 3-qadam: Sement — 1, 2, 3, so'ng +40 → **43**; Kafel **1.44**; Gipsokarton **125**. "Sanaldi: 3 / 4".
- 4-qadam: web'da "Sanalgan" ustuni 43 / 1.44 / 125 / bo'sh; "Tizimda" va "Farq" ustunlari **ko'rinmaydi** ("ko'r" sanoq — o'tkazilgandan keyin ko'rsatiladi).
- 5-qadam: tasdiqlashda "DEMO-SEM-400 … inventarizatsiyada sanalgan (DI-…) — sanoq o'tkazilmaguncha yoki bekor qilinmaguncha ombordan chiqarilmaydi." Ombor o'zgarmaydi; hujjat rad etiladi (Bekor qilingan). (Kafel 2-nav kabi hali sanalmagan tovar chiqishi mumkin.)
- 6-qadam: holat Bekor qilingan; qoldiq o'zgarmagan (S 858, K 456.8, G 125).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-21. Rahbar hisobdan chiqarishni botda tasdiqlaydi va rad etadi
**Nima tekshiriladi:** "Tasdiqqa yuborish" rahbarga ✅/❌ tugmali xabar yuboradi; tasdiq omborni FIFO tannarxda kamaytiradi; rad — ombor o'zgarmaydi; muallifga natija xabari.
**Kim:** demo_ombor (web), rahbar va omborchi telefonlari   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-19
**Qadamlar:**
1. Web (demo_ombor): **Hisobdan chiqarish → Yangi hisobdan chiqarish**: Ombor = DEMO Asosiy ombor, Sabab = Sinish, Izoh = `DEMO: 1 quti singan`, tovar DEMO-KAF-3030 Miqdor `1.44`. **Saqlash** → **Tasdiqqa yuborish**.
2. Rahbar telefonida kelgan xabarda **✅ Tasdiqlash**.
3. Yana bitta hujjat: Sabab = Buzilish, DEMO-GIP-125 `2`. Saqlash → Tasdiqqa yuborish. Rahbar — **❌ Rad etish**.
4. Web: ikkala hujjat va **Qoldiqlar**.
**Kutilgan natija:**
- 1-qadam: holat "Tasdiq kutilmoqda". Rahbarga: "🗑 Hisobdan chiqarish DW-… tasdiqni kutmoqda / Ombor: DEMO Asosiy ombor / Sabab: Sinish / Tuzgan: demo_ombor / • DEMO-KAF-3030 Kafel 30×30 — 1.44 / DEMO: 1 quti singan" + ✅ Tasdiqlash, ❌ Rad etish.
- 2-qadam: tugmalar olib tashlanadi, "✅ Hisobdan chiqarish DW-… tasdiqlandi (tannarx 100 800)." (1,44 × 70 000). Omborchiga: "✅ Hisobdan chiqarish DW-… tasdiqlandi."
- 3-qadam: "❌ Hisobdan chiqarish DW-… rad etildi." Omborchiga: "❌ Hisobdan chiqarish DW-… rad etildi: Telegram".
- 4-qadam: birinchisi "Tasdiqlangan" (Tasdiqlagan — DEMO Rahbar, Tannarx 100 800), ikkinchisi "Bekor qilingan". Qoldiq K haqiqiy **455.36**, G **125** (o'zgarmagan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-22. Ovozli buyurtma
**Nima tekshiriladi:** Agent ovozli xabaridan qoralama (mijoz, tovarlar, narx) tuziladi va tugma bilan buyurtma beriladi; sozlama o'chiq yoki kalit yo'q bo'lsa aniq javob; 90 soniyadan uzun xabar rad etiladi.
**Kim:** agent telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-19
**Qadamlar:**
1. Web: **Mashinalar → Yetkazish sozlamalari**: "Botda ovozli buyurtma" ✗ → **Saqlash**. Agent: qisqa ovozli xabar yuboring.
2. "Botda ovozli buyurtma" ✓ → **Saqlash**. Agent: ovozli xabar — "Anvar do'koniga o'n qop sement M400, ikki quti kafel".
3. Qoralama chiqsa, **✅ Buyurtma berish** ni ikki marta bosing.
4. Ovozli xabar — "o'n qop sement" (mijozsiz). Bot so'raganda `Anvar` yozing. Qoralamani tasdiqlamang (✖️).
5. 95 soniyalik ovozli xabar yuboring.
6. Web: 3-qadamdagi buyurtmani **Bekor qilish** → sabab `DEMO: ovozli sinov`.
7. Ekspeditor telefonidan qisqa ovozli xabar yuboring.
**Kutilgan natija:**
- 1-qadam: "🎤 Ovozli buyurtma o'chirilgan."
- 2-qadam: bot serverida Groq kaliti bo'lmasa — "🎤 Ovozli buyurtma sozlanmagan (nutqni aniqlash kaliti yo'q)." (qolgan qadamlarni o'tkazib yuboring). Kalit bo'lsa: "🎤 «…» / Mijoz: DEMO Anvar do'koni / • DEMO-SEM-400 … — 10 qop × 62 000 = 620 000 / • DEMO-KAF-3030 … — 2 quti × 136 800 = 273 600 / Jami: 893 600 so'm. Buyurtma berilsinmi?"
- 3-qadam: "✅ Buyurtma D-… qabul qilindi: 893 600 so'm. Ofis tasdiqlaydi." — ikki bosishda ham bitta buyurtma. Rahbarga limit xabari keladi (19 160 000 + 893 600 = 20 053 600 > 20 000 000) — ✅ ni bosmang.
- 4-qadam: "🎤 «o'n qop sement» / Qaysi mijozga? Nomini yozing." → `Anvar` dan keyin qoralama.
- 5-qadam: "🎤 Xabar juda uzun — 90 soniyagacha yozing."
- 7-qadam: "🎙 Ovozli buyurtma faqat agentlar uchun." (ekspeditor va rahbar ovozli buyurtma bera olmaydi).
- Nutq sifatini (to'g'ri tanilgan tovar va miqdor) Izohga yozing.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-23. Mini App katalogida aksiya belgisi
**Nima tekshiriladi:** Amaldagi aksiya Mini App katalogida tovar yonida ko'rsatiladi; savatda "aksiya buyurtma qabul qilinganda hisoblanadi" eslatmasi (savat jami aksiyasiz — ataylab); savat telefon xotirasida saqlanadi (oyna yopilsa ham, 3 kun).
**Kim:** demo_rahbar (web), agent telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-ORD-26, T-DST-BOT-06
**Qadamlar:**
1. Web: **Aksiyalar** → "Hammasi" → DEMO Sement 10+1 → **Yoqish**.
2. Agent: Anvar kartasi → **🛒 Buyurtma**. Sement qatorini ko'ring, 10 qop savatga qo'shing, **Savat** ni oching.
3. Mini App'ni yoping (buyurtma bermang) va **🛒 Buyurtma** bilan qayta oching, **Savat** ni ko'ring.
4. Savatni tozalang (O'chirish) va Mini App'ni yoping. Web: aksiyani yana **To'xtatish**.
**Kutilgan natija:**
- 2-qadam: Sement qatorida "🏷 10+1". Savatda Jami 620 000 va "Aksiya chegirmasi va sovg'alar buyurtma qabul qilinganda hisoblanadi."
- 3-qadam: savatda 10 qop sement saqlangan (Jami 620 000).
- 4-qadamdan keyin savat bo'sh, yangi buyurtma yaratilmagan; aksiya nofaol.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-24. Oflayn va cheklov holatlari
**Nima tekshiriladi:** ERP to'xtasa bot "aloqa yo'q" deydi va amal ikki marta bajarilmaydi; bot to'xtaganda bildirishnomalar ERP navbatida kutadi va keyin yetkaziladi; Mini App Telegram'dan tashqarida ochilmaydi; (ixtiyoriy) litsenziya xabari (pilot A9).
**Kim:** administrator (server), agent va rahbar telefonlari, demo_ombor (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-04
**Qadamlar:**
1. Kompyuter brauzerida `https://<bot domeni>/app/?t=<slug>&c=1`, `…/app/store.html?t=<slug>`, `…/app/deliver.html?t=<slug>&s=1` ni oching.
2. Administrator ERP test saytini (IIS app pool) to'xtatadi. Agent **🗺 Marshrut** ni bosadi. ERP qayta ishga tushiriladi, agent yana bosadi.
3. Administrator bot servisini to'xtatadi. demo_ombor web'da **Xatolar jurnali** formasidan muammo yozadi (Qayerda = `web`, matn = `DEMO: oflayn navbat sinovi`). 2 daqiqadan keyin bot servisi ishga tushiriladi.
4. (Ixtiyoriy, faqat test serverida va administrator ruxsati bilan) kompaniya litsenziyasi vaqtincha bloklanadi; agent **🗺 Marshrut** ni bosadi; litsenziya tiklanadi.
**Kutilgan natija:**
- 1-qadam: har uchala sahifada "Bu sahifa Telegram ichida ochiladi." — ma'lumot ko'rinmaydi.
- 2-qadam: "⚠️ ERP bilan aloqa yo'q, birozdan keyin qayta urinib ko'ring."; ERP tiklangach marshrut odatdagidek.
- 3-qadam: bot ishga tushgach taxminan 12–30 soniyada rahbarga "⚠️ Yangi muammo P-00N (DEMO Omborchi): DEMO: oflayn navbat sinovi" keladi (xabar yo'qolmaydi, bir marta).
- 4-qadam: "⛔ Tizim litsenziyasi faol emas. Kompaniya administratoriga murojaat qiling." ("aloqa yo'q" emas).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-25. Rus tili: bot va Mini App
**Nima tekshiriladi:** Foydalanuvchi tili bot va Mini App'da qo'llanadi; ERP'da til o'zgarsa Mini App darhol, bot tili `/start` yoki `/menu` bosilganda ruschaga o'tadi (qayta ulash shart emas); mijoz bildirishnomalari mijozning Telegram tilida (pilot M7).
**Kim:** demo_agent (web), agent va mijoz telefonlari   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-07
**Qadamlar:**
1. demo_agent bilan web'ga kiring, **Distributsiya → Bosh sahifa** → **Русский**.
2. Agent telefonida **🗺 Marshrut** ni bosing, so'ng Anvar kartasidan **🛒 Buyurtma** (Mini App) ni oching va yoping.
3. Agent: `/menu`.
4. Agent menyusini va **🗺 Marshrut** ni ko'ring. Rahbar web'da istalgan Jasur buyurtmasi bilan xabar keltiradigan amal bajarmasin — faqat menyu tekshiriladi.
5. Qaytarish: demo_agent web'da **O'zbekcha**; agent `/menu`.
6. (Ixtiyoriy) Mijoz telefonida Telegram tilini rus tiliga o'zgartirib, botga `/menu` yuboring; keyingi mijoz xabari (masalan, T-DST-PAY-04 dagi to'lov xabari) ruscha keladi. Shundan keyin Telegram tilini qaytaring va `/menu`.
**Kutilgan natija:**
- 2-qadam: bot javoblari hali o'zbekcha (`/start` yoki `/menu` bosilmagan); Mini App ruscha: "Каталог", "Корзина", "Название, артикул или штрих-код".
- 4-qadam: menyu ruscha (qayta ulashsiz): ▶️ Начать день, 🗺 Маршрут, 🔎 Клиент, ➕ Новая точка, 💵 Касса, 📡 Живая геолокация, 🏁 Завершить день, ⚠️ Проблема; "🗺 Маршрут на сегодня: 1 клиентов, посещено …".
- 5-qadamdan keyin yana o'zbekcha.
- 6-qadam: mijoz bildirishnomasi mijozning Telegram tilida (ulanishda va har `/start`, `/menu` da yangilanadi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ekspeditor (2-bosqich)

Quyidagi testlar T-DST-DLV-04 … 06 dan keyin bajariladi: **1-reys** `R-2026-NNNNN` rejalangan — mashina 01 A 123 AA, ekspeditor DEMO Sherzod, ombor DEMO Asosiy ombor; manzillar (tartiblangandan keyin): **1. DEMO Anvar do'koni** — M1 jo'natishi (80 qop sement, 2,88 m² kafel; 5 233 600), **2. DEMO Bunyodkor qurilish** — C jo'natishi (10 qop sement, 10 m² kafel, 5 list gipsokarton; 1 812 500, oldindan to'langan). Jami 7 046 100, og'irlik 4843.96 kg.

Shu paytdagi qarzlar (PAY blokidan keyin): Anvar **13 613 600**, Bunyodkor **0**.

### T-DST-BOT-26. Ekspeditorga reys xabari va reys ko'rinishi
**Nima tekshiriladi:** Reys biriktirilganda ekspeditor xabar oladi; botda reys holati, yuk, manzillar va havolalar.
**Kim:** ekspeditor telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-05
**Qadamlar:**
1. Chatdagi reys xabarini ko'ring.
2. **🚚 Reys** ni bosing.
3. **1. DEMO Anvar do'koni** tugmasini bosing.
**Kutilgan natija:**
- 1-qadam: "🚚 Sizga reys R-… (<sana>) biriktirildi: mashina 01 A 123 AA, 2 ta manzil, 4844 kg. Botda: 🚚 Reys." (T-DST-DLV-06 dagi vaqtinchalik reys uchun ham shunday xabar kelgan bo'ladi.)
- 2-qadam: "🚚 Reys R-… (<sana>) — Rejalandi / Mashina: 01 A 123 AA · 4843.96 kg · 7 046 100 so'm / Manzillar: 0/2 hal qilindi". Tugmalar: ▶️ Yo'lga chiqish; "⏳ 1. DEMO Anvar do'koni · 5 233 600"; "⏳ 2. DEMO Bunyodkor qurilish · 1 812 500"; 🗺 Marshrut (Yandex) (ombordan ikki nuqtagacha), ⛽ Xarajat, 🔄 Yangilash.
- 3-qadam: "📍 1. DEMO Anvar do'koni / Chilonzor 9-kvartal, 12, Bozor yonida / ☎️ +998901111111 / Buyurtma D-… (DJ-…) / Summa: 5 233 600 so'm · qarz: 13 613 600 so'm / Holat: Kutmoqda", "Tovarlar: • DEMO-SEM-400 — Sement M400 — 80 qop • DEMO-KAF-3030 — Kafel 30×30 — 2.88 m²". Reys yo'lda bo'lmagani uchun faqat 🧭 Navigatsiya, 💵 To'lov, ⬅️ Reys tugmalari.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-27. Yo'lga chiqish va mijozga "yo'lga chiqdi"
**Nima tekshiriladi:** Ekspeditor reysni boshlaydi; mijozlarga buyurtma, summa, ekspeditor telefoni va mashina bilan xabar ketadi.
**Kim:** ekspeditor va mijoz telefonlari   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-26
**Qadamlar:**
1. **🚚 Reys** → **▶️ Yo'lga chiqish**.
2. Manzil 1 kartasini oching, **🧭 Navigatsiya** ni bosing.
3. Mijoz (Anvar) chatini ko'ring. Web: Reyslar ro'yxati.
**Kutilgan natija:**
- 1-qadam: "✅ Yo'lga chiqdingiz. Mijozlarga "yo'lga chiqdi" xabari yuborildi." Reys holati "Yo'lda". Ekspeditorning ish kuni avtomatik ochiladi (selfisiz): Dala nazoratida DEMO Sherzod — "Kun boshi" = yo'lga chiqish vaqti; jonli joylashuv yoqilsa yoziladi.
- 2-qadam: kartada endi ✅ Yetkazildi (rasm), ✍️ Imzo / qisman, 🚫 Rad etildi, 💵 To'lov tugmalari bor; Navigatsiya Yandex Maps'ni `rtext=~41.2856,69.2034` bilan ochadi.
- 3-qadam: Anvarga "🚚 Buyurtmangiz yo'lga chiqdi: D-… (5 233 600 so'm). Ekspeditor: DEMO Sherzod, +998904444444 Mashina: 01 A 123 AA". Web'da reys holati "Yo'lda", "Yo'lga chiqdi" vaqti.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-28. Natijasiz manzil bilan reysni yakunlab bo'lmaydi
**Nima tekshiriladi:** Reys barcha manzillar hal bo'lmaguncha yakunlanmaydi.
**Kim:** ekspeditor telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-27
**Qadamlar:**
1. **🏁 Reysni yakunlash** ni bosing.
**Kutilgan natija:**
- "Hali 2 ta manzil bo'yicha natija yo'q." va reys ko'rinishi qayta yuboriladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-29. "Yetkazildi (rasm)": oldindan to'langan buyurtma (C)
**Nima tekshiriladi:** To'liq yetkazishni rasm tasdiqlaydi; oldindan to'langani uchun jo'natishda "Yopildi" bo'lgan buyurtmada ham yetkazish qayd qilinadi (pilot tuzatishi, `03_PILOT.md` 6-bo'lim).
**Kim:** ekspeditor telefoni, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-27, T-DST-DLV-03
**Qadamlar:**
1. **2. DEMO Bunyodkor qurilish** → **✅ Yetkazildi (rasm)**.
2. Rasm o'rniga `tayyor` yozing (menyu chiqadi), so'ng qaytadan **✅ Yetkazildi (rasm)** → topshirilgan tovar rasmini yuboring.
3. Web: C kartasi.
**Kutilgan natija:**
- 1-qadam: "📷 Topshirilgan tovarning yoki imzolangan nakladnoyning rasmini yuboring."
- 2-qadam: "✅ Yetkazildi: 1 812 500 so'm. Mijozga nakladnoy yuboriladi." Reysda "✅ 2. DEMO Bunyodkor qurilish", "Manzillar: 1/2 hal qilindi". (Bunyodkor botga ulanmagan — unga xabar yo'q.)
- 3-qadam: C holati **Yopildi** bo'lib qoladi, tarixda yangi yozuv "Yopildi — R-…" (yetkazish qayd qilingan); "Yetkazildi" tugmasi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-30. Imzo va qisman yetkazish (Mini App), nakladnoy PDF (M1)
**Nima tekshiriladi:** Mini App'da yetkazilgan miqdor va sabab kiritiladi, mijoz imzo qo'yadi; mijozga qisman yetkazish xabari nakladnoy PDF bilan; rahbarga muammo xabari; buyurtma "Yetkazildi".
**Kim:** ekspeditor, mijoz va rahbar telefonlari, demo_rahbar (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-27
**Qadamlar:**
1. **1. DEMO Anvar do'koni** → **✍️ Imzo / qisman**. Mini App sarlavhasi va qatorlarini ko'ring.
2. Sement miqdorini `70` qiling. Asosiy tugma **Tasdiqlash**.
3. Mijoz imzosini chizing, **Tasdiqlash**.
4. Sabab = "Mijoz rad etdi", Izoh = `10 qop yorilgan`, **Tasdiqlash**.
5. Mini App'ni shu manzil uchun yana oching.
6. Mijoz va rahbar chatlarini, web'da M1 va reys kartasini ko'ring.
**Kutilgan natija:**
- 1-qadam: "1. DEMO Anvar do'koni", "D-… · 5 233 600 so'm"; qatorlar "80 qop jo'natilgan · 4 960 000" (maydon 80), "2.88 m² jo'natilgan · 273 600" (2.88); "Yetkaziladi 5 233 600 so'm".
- 2-qadam: "Yetkaziladi **4 613 600** so'm" (4 960 000 × 70 / 80 = 4 340 000 + 273 600); imzosiz — "Mijoz imzosini oling."
- 3-qadam: "Qisman yetkazish yoki rad uchun sababni tanlang."
- 4-qadam: "Saqlandi", oyna yopiladi; chatda "🟡 Qisman yetkazildi: 4 613 600 so'm (5 233 600 so'mdan)."
- 5-qadam: "Bu manzil bo'yicha natija allaqachon kiritilgan." va tasdiqlash tugmasi yo'q.
- Mijozga: "✅ Buyurtma D-… qisman yetkazildi (DJ-…): 4 613 600 so'm (5 233 600 so'mdan). Sabab: Mijoz rad etdi. Nakladnoy ilova qilingan." + **PDF fayl** (nakladnoy: Sement 70 qop, Kafel 2.88 m², jami 4 613 600; kirill/lotin harflari to'g'ri).
- Rahbarga: "⚠️ Reys R-…: DEMO Anvar do'koni, buyurtma D-… — Qisman. Sabab: Mijoz rad etdi. Ekspeditor: DEMO Sherzod."
- Web: M1 **Yetkazildi**; reysda manzil 1 — Qisman, yetkazildi 4 613 600, Tasdiq — imzo.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-31. Manzilda to'lov (ekspeditor)
**Nima tekshiriladi:** Ekspeditor manzilda to'lov oladi — mijoz va buyurtma manzildan olinadi; naqd ekspeditor qo'lida.
**Kim:** ekspeditor va mijoz telefonlari   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-30
**Qadamlar:**
1. Manzil 1 → **💵 To'lov** → `4000000` → **Naqd**.
2. Web: **To'lovlar** ro'yxati.
**Kutilgan natija:**
- 1-qadam: "💵 DEMO Anvar do'koni / Buyurtma: 5 233 600 so'm, qarz: 13 613 600 so'm. Olingan summani yozing:" → usullar Naqd, Karta, Click → "✅ To'lov DP-… qabul qilindi: 4 000 000 so'm. Qo'lingizda naqd: 4 000 000 so'm."
- Mijozga: "To'lovingiz qabul qilindi: 4 000 000 so'm (DP-…). Qolgan qarz: 9 613 600 so'm."
- Web: DP — Qabul qildi DEMO Sherzod, "Agent qo'lida (naqd)", buyurtma M1.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-32. Reys xarajati (ekspeditor)
**Nima tekshiriladi:** Ekspeditor botdan xarajat (tur, summa, izoh) yozadi; ofis uni moliyaga o'tkazadi (T-DST-DLV-07).
**Kim:** ekspeditor telefoni   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-27
**Qadamlar:**
1. **🚚 Reys** → **⛽ Xarajat** → **Yoqilg'i** → `150000 AI-92`.
**Kutilgan natija:**
- "Xarajat turi:" — Yoqilg'i, To'xtash joyi, Ta'mir, Ovqat, Boshqa, ✖️ Bekor qilish → "Yoqilg'i: summani yozing (izoh bilan bo'lishi mumkin, masalan «150000 AI-92»):" → "✅ Xarajat qayd qilindi: 150 000 so'm. Ofis moliyaga o'tkazadi."
- Web reys kartasida "Reys xarajatlari": Yoqilg'i 150 000, izoh AI-92, holat Kutmoqda.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-33. Ekspeditor naqdini kassaga topshirish va kassir qabuli
**Nima tekshiriladi:** Ekspeditor ham agent kabi naqdni topshiradi; kassir farqsiz qabul qiladi; kassa qoldig'i oshadi.
**Kim:** ekspeditor telefoni, demo_kassir (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-BOT-31, T-DST-DLV-08
**Qadamlar:**
1. **💵 Kassa** → **📤 Kassaga topshirish** → **✅ Tasdiqlash**.
2. Kassir: **To'lovlar → Agentlar naqdi va kassa** → DEMO Sherzod qatorida **Qabul qilish**: Kassa = DEMO Kassa, Sanalgan = `4000000` → tasdiqlang.
3. **Moliya → Hisobotlar → Hisoblardagi qoldiq** da DEMO Kassa.
**Kutilgan natija:**
- 1-qadam: "💵 Qo'lingizdagi naqd: 4 000 000 so'm. Bugun yig'ilgan: 4 000 000 so'm." → "✅ Topshirish DK-…: 4 000 000 so'm. Kassir qabul qilishini kuting." Rahbarga "DEMO Sherzod kassaga 4 000 000 so'm topshirdi — qabul qiling."
- 2-qadam: farq 0; "Kassaga topshirishlar" jadvalida DK — To'lovlar bo'yicha 4 000 000, Sanalgan 4 000 000, Farq 0. Sherzod qatori agentlar qo'lidagi naqd ro'yxatidan yo'qoladi.
- 3-qadam: DEMO Kassa qoldig'i 7 386 400 + 4 000 000 = **11 386 400** (PAY blokidagi 7 536 400 − T-DST-DLV-07 dagi yoqilg'i 150 000 = 7 386 400).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-BOT-34. 2-reys: natijani qaytarish (↺), rad etish, botda yakunlash va ofisda yopish
**Nima tekshiriladi:** Ofis "Yetkazildi" natijasini ↺ bilan qaytarsa buyurtma "Jo'natildi" ga qaytadi va mijozga tuzatish xabari ketadi; manzil "Rad etildi" (sabab bilan) — mijozga va rahbarga xabar; ekspeditor reysni botda **yakunlaydi** (reys yopilmaydi — "Yo'lda" qoladi, ombor va rahbarga xabar); omborchi qaytgan tovarni sanab reysni web'da yopadi — qaytarish hujjati shu yerda.
**Kim:** ekspeditor, mijoz, rahbar va omborchi telefonlari, demo_rahbar va demo_ombor (web)   **Oldin bajarilgan bo'lishi kerak:** T-DST-DLV-09
**Qadamlar:**
1. **🚚 Reys** (2-reys, Yo'lda; manzillar F — 6 800 000 va P1 — 1 240 000).
2. F manzili → **✅ Yetkazildi (rasm)** → rasm.
3. Web (demo_rahbar): 2-reys kartasida F qatoridagi **↺** (natijani bekor qilish). F kartasini va mijoz chatini ko'ring.
4. Ekspeditor: F manzili → **✅ Yetkazildi (rasm)** → rasm (qayta).
5. P1 manzili → **🚫 Rad etildi** → ro'yxatni ko'ring → **Mijoz rad etdi**.
6. **🏁 Reysni yakunlash** → **✅ Tasdiqlash**. So'ng **🚚 Reys**.
7. Web (demo_ombor): 2-reys kartasi → "Reysni yopish" bloki: P1 qatorlarida "Omborga qaytdi" standart qiymatlarini o'zgartirmang (20 + 2 sovg'a = 22 qop sanalgan) → **Reysni yopish** → tasdiqlang.
8. Web: **Qaytarishlar**, **Qoldiqlar**, **Qarzlar**, F va P1 kartalari.
**Kutilgan natija:**
- 2-qadam: "✅ Yetkazildi: 6 800 000 so'm. …"; mijozga "✅ Buyurtma D-… yetkazildi (DJ-…): 6 800 000 so'm. Nakladnoy ilova qilingan." + PDF. F holati **Yetkazildi**.
- 3-qadam: F manzili yana "Kutmoqda" (foto, izoh, koordinata tozalangan); F holati **Jo'natildi**, yetkazish sanasi bo'sh, tarixda "Yetkazildi → Jo'natildi" ("R-…: yetkazish natijasi bekor qilindi"). Mijozga: "yetkazildi" xabari hali ketmagan bo'lsa — o'chiriladi; ketgan bo'lsa — "↺ D-… buyurtma bo'yicha yetkazish natijasi bekor qilindi va qayta kiritiladi. Oldingi xabarni hisobga olmang."
- 4-qadam: F yana **Yetkazildi**, mijozga yangi "yetkazildi" xabari + PDF.
- 5-qadam: sabablar — Brak, Muddati o'tgan, Noto'g'ri tovar, Mijoz rad etdi, ✖️ Bekor qilish. "🚫 Rad etildi qayd qilindi. Tovar reys yopilganda omborga qaytariladi." Mijozga "🚫 Buyurtma D-… qabul qilinmadi. Sabab: Mijoz rad etdi." Rahbarga "⚠️ Reys R-…: DEMO Anvar do'koni, buyurtma D-… — Rad etildi. Sabab: Mijoz rad etdi. Ekspeditor: DEMO Sherzod."
- 6-qadam: "Reys R-…ni yakunlaysizmi? Yetkazilmagan tovarni omborga topshiring — ombor sanaydi, reysni ofis yopadi." → "🏁 Reys R-… yakunlandi. / Yetkazildi: 1, qisman: 0, rad: 1 / Yetkazilgan: 6 800 000 so'm, omborga topshiriladi: 1 240 000 so'm / Reysda yig'ilgan: 0 so'm, qo'lingizda: 0 so'm / Ombor tovarni sanab, reysni ofis yopadi." Rahbar va omborchiga: "🏁 Reys R-… yakunlandi (DEMO Sherzod). Qaytadigan tovar: 1 240 000 so'm — omborda sanab, Reyslar sahifasida yoping (qaytgan miqdor va kamomad)." Web'da reys hali **Yo'lda**, qaytarish hujjati **yo'q**. Ekspeditor ish kuni yopiladi. **🚚 Reys** — "Sizga hozircha reys biriktirilmagan." (yakunlangan reys ko'rsatilmaydi; rejalangan reys bo'lsa — o'sha).
- 7-qadam: reys **Yopildi**. Rahbarga "🏁 Reys R-… yopildi: yetkazildi 1/2, 6 800 000 so'm; omborga qaytdi 1 240 000 so'm, kamomad 0 so'm; xarajat 0 so'm."
- 8-qadam: yangi qaytarish DQ — P1 bo'yicha 22 qop (20 + 2 sovg'a), Jami **1 240 000** (sovg'a qatori 0). Qoldiq S haqiqiy 757 + 22 = **779**. Anvar qarzi 8 993 600 − 1 240 000 = **7 753 600**. P1 — yetkazilmagan (Jo'natildi/Yopildi — qarz FIFO bo'yicha), yetkazish sanasi bo'sh.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ma'lum cheklovlar va kuzatish kerak bo'lgan joylar
- Xodim boti tili ERP'da til o'zgargach `/start` yoki `/menu` bosilganda yangilanadi (qayta ulash shart emas); Mini App darhol yangi tilda. Mijoz bildirishnomalari — mijozning Telegram tilida (T-DST-BOT-25).
- Ekspeditor menyusida "▶️ Kunni boshlash" yo'q — ish kuni reys boshlanganda (Yo'lga chiqish) avtomatik ochiladi va reys yakunlanganda/yopilganda (boshqa yo'ldagi reysi bo'lmasa) yopiladi; selfi va "Keldim" ishlatilmaydi.
- Botdagi "🏁 Reysni yakunlash" reysni yopmaydi: qaytgan tovarni ombor sanaydi va reysni ofis (Reyslar → Reysni yopish) yopadi; kamomad shu yerda kiritiladi. Yakunlangan reys keyingi rejalangan reysni to'smaydi.
- Ekspeditor botida joriy reys: yo'ldagi (yakunlanmagan), bo'lmasa rejalanganlardan sanasi bugunga eng yaqini (bugungi, keyin o'tib ketgani, keyin kelajakdagi).
- Mini App savat jami aksiyasiz — ataylab; rahbar Mini App buyurtma oynasiga kira olmaydi (agent va ekspeditor); ovozli buyurtma faqat agent uchun.
- Agent o'z mijozi bo'lmagan nuqtada (masalan, Bunyodkor) ishlay olmaydi — agentsiz mijozlar botda ko'rinmaydi.
- Galereyadan yuborilgan rasm selfi sifatida qabul qilinadi (Telegram cheklovi — farqlab bo'lmaydi).
- Telegram Web/Desktop'da Mini App kamerasi ishlamasligi mumkin; skaner o'rniga kodni qidiruvga yozing.
- Kun yakuni va rahbar yakunidagi "Buyurtmalar" soni yaratilgan sana bo'yicha — ofis agent nomidan kiritgan buyurtmalar ham agentning kun yakuniga kiradi.
