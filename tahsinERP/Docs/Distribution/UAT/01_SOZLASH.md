# 1. Sozlash: ma'lumotnomalar, tovarlar, mijozlar, ruxsatlar

Bu bo'lim keyingi barcha testlar ishlaydigan asosni tayyorlaydi va shu bilan birga sozlash sahifalarining o'zini tekshiradi: sozlamalar, hududlar, narx turlari va narxlar, tovarlar (birlik, shtrix-kod), omborlar, jamoalar, mijozlar, Excel import (avval ko'rib chiqish), ruxsatlar va ko'rinish doirasi, Telegram'ga ulash. Testlarni T-DST-SET-01 … 25 tartibida bajaring.

Distributsiya sahifalari chap menyuda **Distributsiya** bo'limida. "Inventarizatsiya" va "Xatolar jurnali" menyuda yo'q — ular **Distributsiya → Bosh sahifa** plitalaridan ochiladi. Qadamlarda "**Distributsiya → X**" deb yozilgan joy — shu menyu bandi.

Kutilgan xabarlar: saqlash muvaffaqiyatli bo'lsa o'ng yuqorida "Saqlandi" chiqadi; biznes xatosi — qizil xabar (matni testda ko'rsatilgan); ruxsat yo'q bo'lsa sahifa "Sizga ruxsat berilmagan!" sahifasiga o'tadi, AJAX amalda esa "Ruxsat yo'q" xabari chiqadi.

---

### T-DST-SET-01. Modulga kirish, menyu va interfeys tili
**Nima tekshiriladi:** Litsenziyali bazada Distributsiya bo'limi ochiladi; bosh sahifada plitalar bor; interfeys tili o'zbek/rus almashadi va foydalanuvchiga saqlanadi (pilot M7).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** 00_INTRO tayyorgarlik (1–7)
**Qadamlar:**
1. demo_rahbar bilan kiring. Chap menyuda **Distributsiya** bo'limini oching va bandlar ro'yxatini yozib oling.
2. **Distributsiya → Bosh sahifa** ni oching.
3. Yuqori o'ngdagi **Русский** tugmasini bosing, sahifani yangilang, so'ng **Buyurtmalar** ni oching.
4. Chiqib, qayta kiring va Distributsiya sahifasini oching. Keyin **O'zbekcha** ni bosing.
**Kutilgan natija:**
- Sahifa License/Blocked sahifasiga yo'naltirmaydi (litsenziya faol).
- Menyuda (tartib bilan): Bosh sahifa, Rahbar paneli, Hisobotlar, Buyurtmalar, Qoldiqlar, Ko'chirmalarni qabul qilish, Hisobdan chiqarish, Pereso'rtitsa, Qo'shimcha xarajatlar (landed cost), Buyurtma taklifi, Aksiyalar, To'lovlar, Qarzlar, Qaytarishlar, Dala nazorati, Tashrif rejasi, Reyslar, Mashinalar, Agent KPI, Mijozlar, Tovarlar, Narxlar, Hududlar, Agentlar va jamoalar, Ma'lumotnomalar, Sozlamalar, Excel import.
- Bosh sahifada 28 ta plita bor, jumladan **Inventarizatsiya** ("Boshlang'ich va nazorat sanog'i") va **Xatolar jurnali** ("Pilot: xato va muammolar") — bu ikkalasi chap menyuda yo'q (Izohga yozing — K3 taklifi).
- 3-qadamda distributsiya sahifalari ruscha ("Заказы" va h.k.), 4-qadamda qayta kirgandan keyin ham ruscha qoladi (til foydalanuvchiga saqlangan). "O'zbekcha" dan keyin yana o'zbekcha.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-02. Rollar bo'yicha menyu va ruxsatsiz sahifalar
**Nima tekshiriladi:** Har rol faqat o'ziga berilgan sahifalarni ko'radi; URL orqali ham ruxsatsiz sahifa ochilmaydi; faqat ko'rish huquqi bilan o'zgartirish tugmalari ko'rinmaydi.
**Kim:** demo_agent, demo_kassir, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-01
**Qadamlar:**
1. demo_agent bilan kiring. Distributsiya menyusini ko'ring. Manzil satrida ketma-ket oching: `/Distribution/DstOrders/Edit`, `/Distribution/DstPrices`, `/Distribution/DstSettings`.
2. **Distributsiya → Mijozlar** ni oching.
3. demo_kassir bilan kiring. Menyuni ko'ring. `/Distribution/DstOrders`, `/Distribution/DstStock`, `/Distribution/DstSettings` ni oching.
4. demo_ombor bilan kiring. Menyuni ko'ring. `/Distribution/DstPayments`, `/Distribution/DstSettings`, `/Distribution/DstLandedCosts` ni oching.
**Kutilgan natija:**
- demo_agent menyusi: Bosh sahifa, Buyurtmalar, Mijozlar. 1-qadamdagi har uchala manzilda "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- 2-qadamda Mijozlar ro'yxati ochiladi, lekin "Yangi mijoz", "Mavjud mijozni qo'shish", "Excel import" tugmalari yo'q (faqat ko'rish huquqi).
- demo_kassir menyusi: Bosh sahifa, To'lovlar, Qarzlar. 3-qadamdagi uchala manzilda "Sizga ruxsat berilmagan!".
- demo_ombor menyusi: Bosh sahifa, Buyurtmalar, Qoldiqlar, Ko'chirmalarni qabul qilish, Hisobdan chiqarish, Pereso'rtitsa, Qaytarishlar, Reyslar. 4-qadamdagi uchala manzilda "Sizga ruxsat berilmagan!".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-03. Narx turlari, brend va sabablar (Ma'lumotnomalar)
**Nima tekshiriladi:** Standart narx turi faqat bitta; nofaol narx turi tanlovlarda chiqmaydi; sabablar ro'yxati (qaytarish, buyurtmasiz, bekor, hisobdan chiqarish) tayyor.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-01
**Qadamlar:**
1. **Distributsiya → Ma'lumotnomalar** ni oching, **Narx turlari** yorlig'iga o'ting. Ro'yxatni yozib oling (yangi bazada: Chakana, Ulgurji — standart, Yirik ulgurji, Diler).
2. **Chakana** qatorida **Standart qilish** ni bosing.
3. **Yirik ulgurji** qatorida **Faol / nofaol** ni bosing.
4. Agar ro'yxatda Chakana, Ulgurji yoki Diler yo'q bo'lsa — **Qo'shish** bilan yarating (Nomi, Nomi (rus), Tartib).
5. **Chakana** ni nofaol qilishga urining.
6. **Brendlar** yorlig'ida **Qo'shish**: Nomi = `DEMO Qizilqum`. Saqlang.
7. **Sabablar** yorlig'ini oching va turlar bo'yicha ro'yxatni ko'ring.
**Kutilgan natija:**
- 2-qadamdan keyin "standart" belgisi faqat Chakana yonida; Ulgurji'dan olib tashlangan.
- 3-qadamdan keyin Yirik ulgurji "Nofaol". U narxlar jadvali va mijoz kartasidagi "Narx turi" tanlovida chiqmaydi.
- 5-qadamda "Standart narx turi faol bo'lishi kerak." xabari chiqadi, Chakana faol qoladi.
- Brend ro'yxatda paydo bo'ladi.
- Sabablar: Buyurtmasiz tashrif — Do'kon yopiq, Pul yo'q, Tovar bor, Egasi yo'q; Qaytarish — Brak, Muddati o'tgan, Noto'g'ri tovar, Mijoz rad etdi; Bekor qilish — Mijoz bekor qildi, Qoldiq yetmadi, Xato kiritilgan; Hisobdan chiqarish — Sinish, Buzilish va boshqalar. (Bo'lmasa — xuddi shu nomlar bilan qo'shing; keyingi testlar shu nomlarga tayanadi.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-04. Hududlar importi (ko'rib chiqish → import → takror yuklash)
**Nima tekshiriladi:** Import avval faqat tekshiradi (hech narsa yozmaydi), keyin "Import qilish" bitta tranzaksiyada yozadi; takror yuklash dublikat yaratmaydi (pilot M1, import tartibi).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-01
**Qadamlar:**
1. **Distributsiya → Excel import** ni oching. Yorliqlar tartibini ko'ring. **1. Hududlar** yorlig'ida **Shablonni yuklab olish** ni bosing.
2. Shablonga ikkita qator yozing (ustunlar: Viloyat | Viloyat (rus) | Tuman | Tuman (rus) | Hudud | Hudud (rus)):
   - `Toshkent sh.` | `г. Ташкент` | `Chilonzor` | `Чиланзар` | | 
   - `Toshkent sh.` | `г. Ташкент` | `Yunusobod` | `Юнусабад` | |
3. Faylni tanlang, **Avval ko'rib chiqish (tavsiya)** belgisi qo'yilgan holda **Yuklash** ni bosing.
4. **Distributsiya → Hududlar** ni yangi oynada oching.
5. Import sahifasiga qaytib **Import qilish** ni bosing. Hududlar sahifasini yangilang.
6. Xuddi shu faylni yana yuklang (ko'rib chiqish bilan).
7. Faylga uchinchi qator qo'shing: Viloyat bo'sh, Tuman = `Sergeli`. Yuklang.
**Kutilgan natija:**
- 1-qadamda yorliqlar: 1. Hududlar, 2. Narx turlari, 3. Mijozlar, 4. Marshrutlar, 5. Tovarlar, 6. Tovar birliklari, 7. Narxlar, 8. Kirish qarzlari. Shablonda "Namuna" varag'i bor.
- 3-qadamda ko'k xabar: "Xato yo'q, hali hech narsa yozilmadi. Quyidagi ro'yxatni tekshiring va "Import qilish" ni bosing. yangi — 3, yangilandi — 0" (viloyat va ikki tuman). "Ko'rib chiqish" jadvalida 2 qator: `Toshkent sh. / Chilonzor` va `Toshkent sh. / Yunusobod`, amal — "Yangi". **Import qilish** tugmasi chiqadi.
- 4-qadamda Hududlar sahifasida bu hududlar hali **yo'q**.
- 5-qadamdan keyin yashil xabar "Import yakunlandi: yangi — 3, yangilandi — 0". Hududlar daraxtida: Toshkent sh. (Viloyat) → Chilonzor, Yunusobod (Tuman).
- 6-qadamda ikkala qator "O'zgarishsiz", "yangi — 0, yangilandi — 0, o'zgarishsiz — 2". Import qilinsa ham yangi yozuv paydo bo'lmaydi.
- 7-qadamda "1 ta xato — hech narsa yozilmadi." va xato jadvalida 4-qator: "Viloyat majburiy; hudud tumansiz bo'lmaydi" mazmunidagi xabar. Import qilish tugmasi chiqmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-05. Hududlar daraxti qoidalari
**Nima tekshiriladi:** Viloyat → tuman → hudud (3 daraja) dan chuqur ochib bo'lmaydi; ishlatilmagan hudud o'chiriladi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-04
**Qadamlar:**
1. **Distributsiya → Hududlar** da **Chilonzor** qatorida **Ichiga qo'shish** ni bosing. Nomi = `DEMO Chilonzor bozori`, Nomi (rus) = `Рынок Чиланзар`. Saqlang.
2. **DEMO Chilonzor bozori** qatorida **Ichiga qo'shish** ni bosib, `DEMO Ichki` nomli yozuv saqlashga urining.
3. **Toshkent sh.** ni tahrirlab, "Yuqori daraja" = **Chilonzor** qilib saqlashga urining.
4. **DEMO Chilonzor bozori** qatorida **O'chirish** ni bosing va tasdiqlang.
**Kutilgan natija:**
- 1-qadamdan keyin yangi yozuv "Hudud" darajasida (3), Chilonzor ostida.
- 2-qadamda "Hudud ostida yangi daraja ochib bo'lmaydi (maksimum 3)." xabari chiqadi.
- 3-qadamda "Ichida hududlar bor — darajasini o'zgartirib bo'lmaydi." yoki "Hududni o'z ichidagi hudud ostiga ko'chirib bo'lmaydi." xabari chiqadi, daraxt o'zgarmaydi.
- 4-qadamdan keyin DEMO Chilonzor bozori ro'yxatdan yo'qoladi (ishlatilmagan hudud).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-06. Umumiy sozlamalar
**Nima tekshiriladi:** Minimal va yirik buyurtma, tashrif radiusi, agent naqdi kassasi, bot nomi va ochiq manzili saqlanadi; noto'g'ri qiymatlar rad etiladi (pilot P6).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** 00_INTRO tayyorgarlik (DEMO Kassa yaratilgan)
**Qadamlar:**
1. **Distributsiya → Sozlamalar** ni oching. "Umumiy sozlamalar" kartasida: Minimal buyurtma, so'm = `500000`; Yirik buyurtma, so'm = `10000000`; Tashrif radiusi, m = `10`; Agent naqdi topshiriladigan kassa = `DEMO Kassa`; Telegram bot nomi = test botining nomi (`@` siz); Bot ochiq manzili = bot domeni (masalan `https://bot-test.<domen>`). **Saqlash** ni bosing.
2. Radiusni `200` qiling, Bot ochiq manzilini `ftp://bot` qiling. **Saqlash**.
3. Bot ochiq manzilini yana bot domeniga qaytaring. "Kun boshida selfi majburiy" belgisi **qo'yilmagan** bo'lsin. **Saqlash**. Sahifani yangilang.
**Kutilgan natija:**
- 1-qadamda "Son noto'g'ri kiritilgan" xabari chiqadi (radius 20–5000 oralig'ida bo'lishi kerak), hech narsa saqlanmaydi.
- 2-qadamda "Bot manzili https://... ko'rinishida bo'lsin." xabari chiqadi, hech narsa saqlanmaydi.
- 3-qadamda "Saqlandi"; yangilangandan keyin barcha qiymatlar saqlangan: 500000, 10000000, 200, DEMO Kassa, bot nomi, bot manzili; selfi belgisi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-07. Agent to'lovlari (usullar va hisoblar)
**Nima tekshiriladi:** Agent qaysi usulda to'lov olishi va naqdsiz usul qaysi hisobga tushishi sozlanadi; hisobsiz naqdsiz usul yoqilmaydi (pilot D3).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-06, 00_INTRO (DEMO Bank hisobi)
**Qadamlar:**
1. Sozlamalar → **Agent to'lovlari** kartasida: Naqd ✓, Karta ✓ (Hisob = `DEMO Bank hisobi`), Click ✓ (Hisob = `—` bo'sh), Payme, Uzum, Pul o'tkazmasi — belgisiz. **Saqlash**.
2. Click hisobini `DEMO Bank hisobi` qiling. **Saqlash**. Sahifani yangilang.
**Kutilgan natija:**
- Naqd qatorida hisob o'rniga "agent qo'lida → kassaga topshiradi" yozuvi turadi.
- 1-qadamda "To'lov usuli uchun hisob tanlanmagan (Sozlamalar → Agent to'lovlari). (Click)" xabari chiqadi, saqlanmaydi.
- 2-qadamdan keyin "Saqlandi"; yangilangandan keyin Naqd, Karta, Click belgilangan, Karta va Click hisobi — DEMO Bank hisobi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-08. Rol bo'yicha chegirma limitlari
**Nima tekshiriladi:** Chegirma limiti rol bo'yicha saqlanadi; bo'sh limit — chegirma berib bo'lmaydi (tekshiruvi T-DST-ORD-02 da).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-06
**Qadamlar:**
1. Sozlamalar → **Chegirma limitlari** jadvalida `DEMO-Rahbar` qatoriga `10`, `DEMO-Agent` qatoriga `5` yozing (har biridan keyin maydondan chiqing — saqlash avtomatik).
2. `DEMO-Kassir` qatoriga `150` yozib ko'ring.
3. Sahifani yangilang.
**Kutilgan natija:**
- 1-qadamda har o'zgarishdan keyin "Saqlandi".
- 2-qadamda "Foiz noto'g'ri." xabari chiqadi.
- 3-qadamda DEMO-Rahbar 10, DEMO-Agent 5, qolganlar bo'sh.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-09. Omborlar: savdo ombori, minus, zona
**Nima tekshiriladi:** "Savdoda" belgilangan omborlardan buyurtma jo'natiladi va avtomatik ombor tanlashda faqat ular qatnashadi; minusga ruxsat berilmaydi (pilot P6).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** 00_INTRO tayyorgarlik (6 — uchala DEMO ombor yaratilgan)
**Qadamlar:**
1. Sozlamalar → **Omborlar** jadvalida **DEMO Asosiy ombor**: Savdoda ✓, Minusga ruxsat ✗, Yetkazish zonasi = `Toshkent`; qator oxiridagi 💾 tugmasini bosing.
2. **DEMO Filial ombor**: Savdoda ✓, Minusga ruxsat ✗, zona = `Samarqand`; 💾.
3. **DEMO Brak ombor**: Savdoda ✗, Minusga ruxsat ✗; 💾.
4. Sahifani yangilang. **Distributsiya → Buyurtmalar → Yangi buyurtma** formasida "Ombor" tanlovini oching (saqlamang).
**Kutilgan natija:**
- Har saqlashda "Saqlandi". Yangilangandan keyin belgilar saqlangan.
- Buyurtma formasidagi "Ombor" tanlovida faqat "avtomatik tanlanadi", DEMO Asosiy ombor va DEMO Filial ombor (Brak ombor va boshqa sozlanmagan omborlar yo'q).
- "Minimal qoldiq" ustunida hozircha 0 (minimal qoldiq T-DST-WH-11 da qo'yiladi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-10. Tovarlar importi: xato fayl rad etiladi, to'g'risi yoziladi
**Nima tekshiriladi:** Bitta xato qator butun importni to'xtatadi; shtrix-kod nazorat raqami tekshiriladi; to'g'ri faylda tovar, distributsiya kartasi, qo'shimcha birlik va shtrix-kodlar birga yaratiladi (pilot M3, M4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-03, 00_INTRO (birliklar, kategoriyalar, sex)
**Qadamlar:**
1. Excel import → **5. Tovarlar** → shablonni yuklab oling. Quyidagi 4 qatorni yozing (ustunlar: Artikul | Nomi | Nomi (rus) | Kategoriya | Asosiy birlik | Brend | Ishlab chiqaruvchi | Og'irlik (kg) | Hajm (m3) | Shtrix-kod | Birlik 2 | Koeffitsient 2 | Shtrix-kod 2):
   - `DEMO-SEM-400` | `Sement M400` | `Цемент М400` | `DEMO Qurilish aralashmalari` | `qop` | `DEMO Qizilqum` | | `50` | `0.035` | `4780000000015` | `poddon` | `40` | `4780000000021`
   - `DEMO-KAF-3030` | `Kafel 30×30` | `Плитка 30×30` | `DEMO Kafel` | `m²` | | | `17` | `0.01` | `4780000000038` | | |
   - `DEMO-GIP-125` | `Gipsokarton 12,5 mm` | `Гипсокартон 12,5 мм` | `DEMO Qurilish aralashmalari` | `list` | | | `25` | `0.0375` | `4780000000052` | | |
   - `DEMO-KAF-3030-2` | `Kafel 30×30 (2-nav)` | `Плитка 30×30 (2 сорт)` | `DEMO Kafel` | `m²` | | | `17` | `0.01` | `4780000000069` | | |
2. Ko'rib chiqish bilan yuklang.
3. Birinchi qatordagi shtrix-kodni `4780000000014` ga tuzating. Yana yuklang.
4. **Import qilish** ni bosing. **Distributsiya → Tovarlar** da `DEMO` bo'yicha qidiring, so'ng qidiruvga `4780000000021` yozing.
**Kutilgan natija:**
- 2-qadamda "1 ta xato — hech narsa yozilmadi." Xato: 2-qator, "Shtrix-kod nazorat raqami noto'g'ri (EAN/UPC)." Tovarlar ro'yxatida DEMO tovar yo'q.
- 3-qadamda "Xato yo'q … yangi — 4, yangilandi — 0", ko'rib chiqish jadvalida 4 ta "Yangi" qator.
- 4-qadamdan keyin ro'yxatda 4 ta DEMO tovar: Sement M400 (birliklar: qop, poddon; shtrix-kodlar 2), Kafel 30×30 (m²; 1), Gipsokarton 12,5 mm (list; 1), Kafel 30×30 (2-nav) (m²; 1). Holat "Sotuvda". Brend DEMO Qizilqum — Sementda.
- `4780000000021` bo'yicha qidiruv DEMO-SEM-400 ni topadi (poddon shtrix-kodi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-11. Tovar birliklari importi (kafel qutisi)
**Nima tekshiriladi:** Mavjud tovarga qo'shimcha birlik koeffitsient va shtrix-kod bilan qo'shiladi; "odatiy sotish birligi" belgilanadi (pilot M3).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-10
**Qadamlar:**
1. Excel import → **6. Tovar birliklari** → shablon. Bitta qator: Artikul = `DEMO-KAF-3030`, Birlik = `quti`, Koeffitsient = `1.44`, Og'irlik (kg) — bo'sh, Hajm — bo'sh, Shtrix-kod = `4780000000045`, Odatiy sotish = `ha`.
2. Faylga ikkinchi qator qo'shing: `DEMO-GIP-125` | `list` | `1`. Ko'rib chiqish bilan yuklang.
3. Ikkinchi qatorni o'chirib, qayta yuklang va **Import qilish** ni bosing.
4. **Tovarlar** → DEMO-KAF-3030 → tahrirlash (✏️) sahifasini oching.
**Kutilgan natija:**
- 2-qadamda xato: "Bu tovarning asosiy birligi — qo'shimcha birlik sifatida qo'shilmaydi." Hech narsa yozilmaydi.
- 3-qadamda "Import yakunlandi: yangi — 1 …", ko'rib chiqishda "DEMO-KAF-3030 — quti" — Yangi.
- Tovar kartasidagi "O'lchov birliklari" jadvalida: m² (asosiy), quti — koeffitsient 1,44 ("Nechta m² (asosiy birlik)"), og'irlik avtomatik 17 × 1,44 = 24,48 kg, "Sotishda odatiy birlik" — quti. Shtrix-kodlar: `4780000000038` (m²) va `4780000000045` (quti).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-12. Tovar kartasi: profil, shtrix-kod qoidalari
**Nima tekshiriladi:** Distributsiya kartasi (og'irlik, hajm, sotuvda) saqlanadi; shtrix-kod takrorlanmaydi va nazorat raqami tekshiriladi; asosiy birlik ikki marta qo'shilmaydi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-11
**Qadamlar:**
1. **Tovarlar** → DEMO-SEM-400 kartasi. "Distributsiya kartasi"da Og'irlik, kg = `50`, Hajm, m³ = `0.035`, Sotuvda ✓ ekanini tekshiring. **Saqlash**.
2. "Shtrix-kodlar" blokiga `4780000000038` (Kafelga tegishli) qo'shishga urining.
3. `47800000000` (11 raqam, nazorat raqamsiz) va `4780000000015` ni qo'shishga urining.
4. "O'lchov birliklari" ga **Qo'shish**: Birlik = `qop`, Koeffitsient = `1`.
5. Birlik = `quti`, Koeffitsient = `0` ni qo'shishga urining.
**Kutilgan natija:**
- 1-qadamda "Saqlandi". Kartada "Narxlar 1 qop uchun." yozuvi bor (narxlar hali yo'q).
- 2-qadamda "Bu shtrix-kod boshqa tovarga biriktirilgan (ID …)." xabari.
- 3-qadamda `47800000000` qabul qilinadi (11 xonali kodda nazorat raqami tekshirilmaydi — uni darhol o'chiring), `4780000000015` uchun "Shtrix-kod nazorat raqami noto'g'ri (EAN/UPC)."
- 4-qadamda "Bu tovarning asosiy birligi — qo'shimcha birlik sifatida qo'shilmaydi."
- 5-qadamda "Koeffitsient 0 dan katta bo'lishi kerak."
- Oxirida Sementda faqat 2 shtrix-kod (`…014` qop, `…021` poddon) va 2 birlik (qop, poddon = 40).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-13. Narxlar importi
**Nima tekshiriladi:** Narxlar har narx turi ustuni va "<nom> min" ustuni bilan yuklanadi; o'zgarmagan narx qayta yozilmaydi (pilot M5).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-03, T-DST-SET-11
**Qadamlar:**
1. Excel import → **7. Narxlar** → shablonni yuklab oling. Ustunlarni tekshiring: Artikul, Chakana, Chakana min, Ulgurji, Ulgurji min, Diler, Diler min (nofaol Yirik ulgurji yo'q).
2. Qatorlar:
   - `DEMO-SEM-400` | `62000` | `52000` | `58000` | `52000` | `55000` | `52000`
   - `DEMO-KAF-3030` | `95000` | | `88000` | | |
   - `DEMO-GIP-125` | `68000` | | | | |
   - `DEMO-KAF-3030-2` | `80000` | | | | |
3. Ko'rib chiqish bilan yuklang, so'ng **Import qilish**.
4. Xuddi shu faylni yana yuklang (ko'rib chiqish bilan).
**Kutilgan natija:**
- 3-qadamda "yangi — 0, yangilandi — 7" (7 ta narx: Sement 3, Kafel 2, Gipsokarton 1, Kafel 2-nav 1). Import yakunlangach **Distributsiya → Narxlar** jadvalida DEMO qatorlarida: Sement Chakana 62 000 (min 52 000), Ulgurji 58 000 (min 52 000), Diler 55 000 (min 52 000); Kafel 95 000 / 88 000 / —; Gipsokarton 68 000 / — / —; Kafel 2-nav 80 000.
- 4-qadamda barcha qatorlar "O'zgarishsiz", "o'zgarishsiz — 7".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-14. Narxlar jadvali: tahrir, tarix, ommaviy o'zgartirish
**Nima tekshiriladi:** Katakchada tahrir va narx tarixi; minimal narx narxdan katta bo'lmaydi; ommaviy % o'zgartirish yaxlitlash bilan; bir xil narx tarixga yozilmaydi (pilot M5, N1).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-13
**Qadamlar:**
1. **Distributsiya → Narxlar** da DEMO-GIP-125 ning **Chakana** katagini bosing. Narx = `68000`, Minimal narx = `70000`. **Saqlash**.
2. Minimal narxni bo'sh qoldirib, Narx = `68000` (o'zgarishsiz) saqlang. Keyin shu tovarning **Narx tarixi** ni oching.
3. **Ommaviy o'zgartirish**: Narx turi = Chakana, Kategoriya = `DEMO Kafel`, Foiz = `5`, Yaxlitlash = `1000`. **Qo'llash**.
4. Jadvalni tekshiring. So'ng Kafel 30×30 Chakana = `95000`, Kafel 2-nav Chakana = `80000` qilib qo'lda qaytaring.
5. DEMO-KAF-3030 ning narx tarixini oching.
**Kutilgan natija:**
- 1-qadamda "Minimal narx 0 va narx oralig'ida bo'lishi kerak." Saqlanmaydi.
- 2-qadamda Gipsokarton narx tarixida faqat bitta yozuv (importdagi 68 000) — bir xil narx qayta yozilmagan.
- 3-qadamda "O'zgartirildi" xabari (2 ta tovar). Kafel 30×30: 95 000 × 1,05 = 99 750 → yaxlitlab **100 000**; Kafel 2-nav: 80 000 × 1,05 = **84 000**. Sement va Gipsokarton o'zgarmagan (boshqa kategoriya).
- 4-qadamdan keyin jadval yana 95 000 va 80 000.
- 5-qadamda tarixda 3 yozuv: 95 000 (import), 100 000 (ommaviy), 95 000 (qo'lda); har birida sana, narx turi va foydalanuvchi (DEMO Rahbar).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-15. Boshlang'ich kirim (ombor moduli) va Qoldiqlar sahifasi
**Nima tekshiriladi:** Ombor modulidagi kirim qabul qilingach distributsiya qoldiqlari (haqiqiy / rezerv / mavjud) to'g'ri ko'rinadi; FIFO tannarx qatlami yaratiladi.
**Kim:** demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-10, T-DST-SET-09
**Qadamlar:**
1. **Omborxona → Ombor → Operatsiyalar → Kirimlar → Yangi kirim** ni oching. Sana = bugun, Invoys = bo'sh, Qabul qiluvchi ombor = `DEMO Asosiy ombor`, Valyuta = `UZS`. Qatorlar (QQS % = `0`):
   - DEMO-SEM-400, `1000` qop × `40000`;
   - DEMO-KAF-3030, `500` m² × `70000`;
   - DEMO-GIP-125, `200` list × `50000`.
   Izoh = `DEMO boshlang'ich kirim`. Saqlang.
2. Kirimning Details sahifasida **Qabul qilish** ni bosing. Kirim raqamini yozib oling (keyin landed cost testida kerak).
3. **Distributsiya → Qoldiqlar** ni oching: Ombor = DEMO Asosiy ombor, filtr "Qoldig'i borlar".
4. Ombor = "Barcha omborlar" qiling.
**Kutilgan natija:**
- Kirim "Jami summa" = 1 000 × 40 000 + 500 × 70 000 + 200 × 50 000 = 40 000 000 + 35 000 000 + 10 000 000 = **85 000 000**.
- 2-qadamdan keyin kirim holati "Qabul qilingan".
- Qoldiqlar (DEMO Asosiy ombor): Sement — Haqiqiy **1 000** / Rezerv 0 / Mavjud 1 000 (qop); Kafel — **500** / 0 / 500 (m²); Gipsokarton — **200** / 0 / 200 (list). Kafel 2-nav ko'rinmaydi (qoldig'i yo'q).
- "Barcha omborlar" da ham shu raqamlar (boshqa DEMO omborlarda qoldiq yo'q).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-16. Jamoa, agent va ekspeditor
**Nima tekshiriladi:** Agent va ekspeditor ERP foydalanuvchisi sifatida jamoaga, omborga va telefon bilan qo'shiladi (pilot P7).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** 00_INTRO (foydalanuvchilar)
**Qadamlar:**
1. **Distributsiya → Agentlar va jamoalar** ni oching. Jamoalar kartasida ➕: Nomi = `DEMO Toshkent jamoasi`, Supervayzer = **bo'sh**, Faol ✓. **Saqlash**.
2. Agentlar kartasida **Qo'shish**: Foydalanuvchi = `demo_agent`, Jamoa = DEMO Toshkent jamoasi, Ombor = DEMO Asosiy ombor, Telefon = `90 333 33 33`, Ekspeditor ✗, Faol ✓. **Saqlash**.
3. **Qo'shish**: Foydalanuvchi = `demo_eksp`, Jamoa = DEMO Toshkent jamoasi, Ombor = DEMO Asosiy ombor, Telefon = `+998 90 444 44 44`, **Ekspeditor ✓**. **Saqlash**.
4. Telefonni `12345` qilib demo_eksp ni qayta saqlashga urining.
**Kutilgan natija:**
- Jamoa ro'yxatda, agentlar soni 2.
- Agentlar jadvalida: DEMO Jasur — rol Agent, telefon `+998903333333` (9 raqam → +998 qo'shilgan); DEMO Sherzod — Ekspeditor, `+998904444444`. Har qatorda 🔒 (Ko'rinish doirasi) va Telegram tugmalari bor.
- 4-qadamda "Telefon raqami noto'g'ri." Telefon o'zgarmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-17. Omborchining ko'rinish doirasi (scope)
**Nima tekshiriladi:** Ombor scope'i to'ldirilsa foydalanuvchi faqat tanlangan omborlarni ko'radi; bo'sh scope — cheklovsiz (pilot N3).
**Kim:** demo_rahbar, demo_ombor   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-15
**Qadamlar:**
1. Admin panel → Foydalanuvchilar ro'yxatida demo_ombor ning ID raqamini toping (tahrirlash havolasidagi oxirgi raqam). Manzil satrida `/Distribution/DstTeams/Scopes/<ID>` ni oching (omborchi agent bo'lmagani uchun Jamoalar sahifasida 🔒 tugmasi yo'q).
2. **Omborlar** ro'yxatida DEMO Asosiy ombor va DEMO Filial ombor ni belgilang. **Saqlash**.
3. demo_ombor bilan kiring: **Qoldiqlar** sahifasidagi Ombor tanlovini, **Hisobdan chiqarish → Yangi hisobdan chiqarish** dagi Ombor tanlovini oching (saqlamang).
**Kutilgan natija:**
- 2-qadamda "Saqlandi".
- 3-qadamda Hisobdan chiqarish formasida faqat DEMO Asosiy ombor va DEMO Filial ombor (DEMO Brak ombor yo'q).
- Izohga yozing: omborchi uchun scope sahifasiga UI'da havola yo'q (faqat URL orqali) — K3.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-18. Mijozni web'da yaratish (DEMO Anvar do'koni)
**Nima tekshiriladi:** Mijoz kartasi (CRM mijozi + distributsiya profili) bitta saqlashda yaratiladi; web'da yaratilgan mijoz darhol tasdiqlangan (pilot M2).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-04, T-DST-SET-16
**Qadamlar:**
1. **Distributsiya → Mijozlar → Yangi mijoz** ni oching.
2. "Asosiy ma'lumot": Nomi = `DEMO Anvar do'koni`, STIR = `300000011`, Nuqta turi = Xo'jalik do'koni, Egasi = `DEMO Anvar`, Telefon = `90 111 11 11`, Shahar = `Toshkent`, Manzil = `Chilonzor 9-kvartal, 12`, Mo'ljal = `Bozor yonida`, Kenglik = `41.285600`, Uzunlik = `69.203400`.
3. "Savdo shartlari": Hudud = Chilonzor, Kategoriya = `B`, Agent = DEMO Jasur, Narx turi = Chakana, Tashrif kunlari = Du, Se, Ch, Pa, Ju, Sh (Ya — belgisiz), Kredit limiti = `20000000`, To'lov muddati (kun) = `7`. **Saqlash**.
4. **Mijozlar** ro'yxatida `DEMO` bo'yicha qidiring. Filtrlar: Hudud = Chilonzor, Agent = DEMO Jasur.
**Kutilgan natija:**
- "Saqlandi"; karta qayta ochilganda telefon `+998901111111`, STIR 300000011.
- Ro'yxatda qator: DEMO Anvar do'koni, STIR, Hudud Chilonzor, Agent DEMO Jasur, toifa B, Kredit limiti 20 000 000, Narx turi Chakana. "Tasdiq kutilmoqda" belgisi yo'q.
- Filtrlar bilan ham shu mijoz chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-19. Mijoz kartasi tekshiruvlari (negativ)
**Nima tekshiriladi:** STIR formati va takrori, telefon, GPS juftligi, kredit muddati, nom uzunligi tekshiriladi; xato bo'lsa hech narsa saqlanmaydi.
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-18
**Qadamlar:** Har safar **Yangi mijoz** formasida Nomi = `DEMO Tekshiruv` va ko'rsatilgan bitta maydonni o'zgartirib **Saqlash** ni bosing:
1. STIR = `30000001` (8 raqam).
2. STIR = `300000011` (Anvarniki).
3. Telefon = `12345`.
4. Faqat Kenglik = `41.3` (Uzunlik bo'sh).
5. To'lov muddati = `400`.
6. Nomi = 51 belgili matn (masalan `DEMO ` + 46 ta `x`).
**Kutilgan natija:**
- 1: "STIR 9 raqam (yuridik) yoki JShShIR 14 raqam bo'lishi kerak."
- 2: "Bu STIR boshqa mijozda bor."
- 3: "Telefon raqami noto'g'ri."
- 4: "Kenglik va uzunlik birga kiritiladi."
- 5: "Kredit limiti 0 dan, to'lov muddati 0–365 kun."
- 6: "Nomi 50 belgidan oshmasin."
- Mijozlar ro'yxatida "DEMO Tekshiruv" yo'q; forma kiritilgan qiymatlarni yo'qotmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-20. Mijozlar va marshrutlar importi (DEMO Bunyodkor qurilish)
**Nima tekshiriladi:** Mijoz importi STIR bo'yicha yaratadi/yangilaydi; marshrut importi faqat mavjud mijozga agent va tashrif kunlarini yozadi (pilot M2, import 3–4).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-04, T-DST-SET-18
**Qadamlar:**
1. Excel import → **3. Mijozlar** → shablon. Qator: Nomi = `DEMO Bunyodkor qurilish`, STIR = `300000012`, Telefon = `+998902222222`, Shahar = `Toshkent`, Manzil = `Yunusobod 4-mavze, 7`, Nuqta turi = `MARKET`, Toifa (A/B/C) = `A`, Hudud = `Toshkent sh. / Yunusobod`, Agent (login) = bo'sh, Narx turi = `Diler`, Tashrif kunlari (1-7) = `2,5`, Kredit limiti = `50000000`, Kechiktirish (kun) = `14`, Kenglik = `41.364`, Uzunlik = `69.287`.
2. Ko'rib chiqish bilan yuklang, keyin **Import qilish**.
3. Xuddi shu faylni qayta yuklang (ko'rib chiqish).
4. **4. Marshrutlar** shablonida ikki qator: `300000011` | `demo_agent` | `1,2,3,4,5,6` va `DEMO Yo'q mijoz` | `demo_agent` | `1`. Yuklang.
5. Ikkinchi qatorni o'chirib qayta yuklang.
**Kutilgan natija:**
- 2-qadam ko'rib chiqishda "DEMO Bunyodkor qurilish (300000012)" — Yangi; import yakunlangach Mijozlar ro'yxatida: Hudud Yunusobod, Agent bo'sh, toifa A, limit 50 000 000, Narx turi Diler, tasdiqlangan.
- 3-qadamda shu qator "Yangilanadi" (mijoz STIR bo'yicha topildi, dublikat yaratilmaydi).
- 4-qadamda "1 ta xato — hech narsa yozilmadi.", 3-qator "Topilmadi: DEMO Yo'q mijoz".
- 5-qadamda Anvar qatori "O'zgarishsiz" (agent va kunlar T-DST-SET-18 dagidek).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-21. Kirish qarzlari importi (muddati o'tgan qarz)
**Nima tekshiriladi:** Tizimga o'tishdagi qarz mijozga bir marta yoziladi, berilgan muddat bilan; takror yuklash rad etiladi (pilot import 8).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-20
**Qadamlar:**
1. Excel import → **8. Kirish qarzlari** → shablon. Qator: Mijoz (STIR yoki nomi) = `300000012`, Qarz (+) / avans (-) = `5000000`, To'lov muddati = bugundan **10 kun oldingi** sana (dd.mm.yyyy).
2. Ko'rib chiqish bilan yuklang, keyin **Import qilish**.
3. **Distributsiya → Qarzlar** ni oching.
4. Xuddi shu faylni yana yuklang.
**Kutilgan natija:**
- Ko'rib chiqishda "DEMO Bunyodkor qurilish" — Yangi, tafsilot "+5 000 000".
- Qarzlar sahifasida Bunyodkor: Qarz **5 000 000**, Muddati o'tgan 5 000 000 ("8–30" ustunida — 10 kun kechikkan), Kredit limiti 50 000 000.
- 4-qadamda xato: "Bu mijozga kirish qoldig'i allaqachon kiritilgan." Qarz 5 000 000 bo'lib qoladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-22. Mijozning maxsus narxi
**Nima tekshiriladi:** Maxsus narx narx turidan ustun turadi; to'xtatilgandan keyin narx turi narxi qaytadi; kuchga kirgan narx to'xtatilganda tarixda qoladi, hali kuchga kirmagani (bugun boshlangan) o'chiriladi (pilot B2).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-13, T-DST-SET-18
**Qadamlar:**
1. Mijozlar → DEMO Anvar do'koni kartasi → **Maxsus narxlar** ni oching. Tovar = DEMO-GIP-125, Narx = `64000`, Boshlanish = **kecha**, Tugash = bo'sh. Saqlang.
2. Yana bitta narx: Tovar = DEMO-KAF-3030, Narx = `90000`, Boshlanish = **bugun**. Saqlang.
3. **Buyurtmalar → Yangi buyurtma**: Mijoz = DEMO Anvar do'koni, tovar qo'shish — `GIP`, so'ng `KAF-3030` (birlik m²). Narx ustunini ko'ring. **Saqlamang** (Orqaga).
4. Maxsus narxlar sahifasida ikkala qatorda **To'xtatish** ni bosing.
5. 3-qadamni takrorlang.
**Kutilgan natija:**
- 3-qadamda Gipsokarton narxi **64 000**, Kafel **90 000** (maxsus narxlar).
- 4-qadamdan keyin Gipsokarton qatori ro'yxatda qoladi, Tugash = kecha (tarix saqlangan); Kafel qatori (bugun boshlangan) ro'yxatdan **o'chadi**.
- 5-qadamda narxlar **68 000** va **95 000** (Chakana).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-23. Supervayzer ko'rinishi
**Nima tekshiriladi:** Supervayzer faqat o'z jamoasi agentlari va ularning mijozlarini ko'radi; agentsiz mijoz unga ko'rinmaydi (pilot N3).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-20
**Qadamlar:**
1. Agentlar va jamoalar → DEMO Toshkent jamoasi ni tahrirlang: Supervayzer = DEMO Rahbar. Saqlang.
2. **Mijozlar** ro'yxatida `DEMO` bo'yicha qidiring.
3. Jamoani qayta tahrirlab Supervayzer = bo'sh qiling. Mijozlar ro'yxatini yangilang.
**Kutilgan natija:**
- 2-qadamda faqat **DEMO Anvar do'koni** (agent Jasur — jamoa a'zosi). DEMO Bunyodkor qurilish (agentsiz) ko'rinmaydi.
- 3-qadamdan keyin ikkala mijoz ko'rinadi (ofis xodimi — cheklovsiz).
- Izohga yozing: supervayzer agentsiz mijozlarni umuman ko'rmaydi — bu kutilgan qoidami, biznes bilan aniqlashtiring.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-24. Xodimlar va mijozni Telegram'ga ulash
**Nima tekshiriladi:** Bir martalik kod (10 belgi, 3 kun) va `t.me/<bot>?start=<kod>` havolasi; har rolga o'z menyusi; mijoz o'z qarzini ko'radigan menyu oladi (pilot P10).
**Kim:** demo_rahbar (web), 5 ta Telegram hisobi   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-06, T-DST-SET-16, T-DST-SET-18
**Qadamlar:**
1. Agentlar va jamoalar → "Xodimni botga ulash" kartasida: foydalanuvchi = demo_rahbar, rol = Rahbar → **Kod olish**. Ochilgan "Telegram'ga ulash" oynasidagi havolani rahbar telefonida oching (yoki botga `/start KOD` yuboring).
2. Xuddi shunday: demo_ombor — Omborchi.
3. Agentlar jadvalida DEMO Jasur qatoridagi Telegram tugmasi → kod → agent telefonida oching. DEMO Sherzod qatoridagi Telegram tugmasi → ekspeditor telefonida oching.
4. Mijozlar → DEMO Anvar do'koni kartasi → **Telegram** (Telegram'ga ulash) → kod → mijoz telefonida oching.
**Kutilgan natija:**
- Oynada 10 belgili kod, havola `https://t.me/<bot nomi>?start=<kod>` va "Amal qiladi" sanasi (bugun + 3 kun) bor.
- Har telefonda: "✅ <F.I.Sh>, siz ulandingiz. Rol: rahbar / omborchi / agent / ekspeditor." (mijozda — "✅ DEMO Anvar do'koni, siz ulandingiz. Rol: mijoz.") va menyu:
  - Rahbar: 📊 Bugungi yakun, 📦 Ombor, ⚠️ Muammo.
  - Omborchi: 📦 Ombor, ⚠️ Muammo + "📦 Ombor — inventarizatsiya va buyurtma yig'ish (skaner bilan)."
  - Agent: ▶️ Kunni boshlash, 🗺 Marshrut, 🔎 Mijoz, ➕ Yangi nuqta, 💵 Kassa, 📡 Jonli joylashuv, 🏁 Kunni yakunlash, ⚠️ Muammo.
  - Ekspeditor: 🚚 Reys, 💵 Kassa, 📡 Jonli joylashuv, ⚠️ Muammo, 🏁 Reysni yopish.
  - Mijoz: 💳 Qarzim, 📄 Akt sverki + "Buyurtmalaringiz holati va to'lovlar haqida xabarlar shu yerga keladi…".
- Bot tili — ERP profilidagi til (o'zbekcha).
- Kodlarning chekka holatlari (eskirgan, takror ishlatilgan, qayta ulash) — T-DST-BOT-01 da.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DST-SET-25. Ishlatilgan hududni o'chirib bo'lmaydi
**Nima tekshiriladi:** Mijozlari bor hudud o'chirilmaydi — faqat nofaol qilinadi (tarix buzilmaydi).
**Kim:** demo_rahbar   **Oldin bajarilgan bo'lishi kerak:** T-DST-SET-18
**Qadamlar:**
1. **Hududlar** da **Chilonzor** qatorida **O'chirish** ni bosing va tasdiqlang.
2. **Faol / nofaol** ni bosing, so'ng yana bosing (qayta faol qiling).
**Kutilgan natija:**
- 1-qadamda "Yozuv ishlatilgan — o'chirib bo'lmaydi, nofaol qiling." Chilonzor o'chirilmaydi, "Mijozlar" ustunida 1.
- 2-qadamdan keyin Chilonzor yana "Faol". Anvar kartasida hudud Chilonzor bo'lib qolgan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ma'lum cheklovlar va kuzatish kerak bo'lgan joylar
- "Inventarizatsiya" va "Xatolar jurnali" chap menyuda yo'q — faqat Bosh sahifa plitasi orqali (T-DST-SET-01).
- Agent bo'lmagan xodim (omborchi) uchun ko'rinish doirasi sahifasiga UI havolasi yo'q — `/Distribution/DstTeams/Scopes/<ID>` (T-DST-SET-17).
- Supervayzer agentsiz mijozlarni ko'rmaydi (T-DST-SET-23) — agentsiz mijozlar bilan ishlaydigan xodim supervayzer qilinmasin.
- Import shablonlarining "Namuna" varag'idagi misol shtrix-kodlar (`4780000000017`, `4780000000024`) EAN-13 nazorat raqamidan o'tmaydi — misolni nusxa olgan foydalanuvchi xato oladi. Haqiqiy kodlar bilan ishlang.
- Tovarning o'zi (artikul, asosiy birlik, kategoriya) ERP'ning Mahsulot modulida; distributsiya importi mavjud tovarning asosiy birligini o'zgartirmaydi ("Mavjud tovarning asosiy birligini import orqali o'zgartirib bo'lmaydi.").
