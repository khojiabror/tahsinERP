# 6. Telegram bot va Mini App

Distributsiya modulining "dala" qismi Telegram orqali ishlaydi: agent kunini boshlaydi, mijozga boradi, buyurtma va to'lov oladi; ekspeditor reysni yetkazadi; omborchi buyurtmani shtrix-kod bilan yig'adi; rahbar xabarlarni oladi va tasdiqlaydi; mijoz buyurtmasi holatini, nakladnoyni va qarzini ko'radi. Alohida mobil ilova o'rnatilmaydi — hammasi kompaniyaning o'z Telegram boti va bot ichida ochiladigan **Mini App** (Telegram ichidagi veb-oyna) orqali.

Bob tuzilishi: avval umumiy tushunchalar va ulanish (6.1–6.3), keyin har bir rol uchun alohida bo'lim (6.4–6.8), oxirida cheklovlar va muammolarni hal qilish (6.9–6.10).

Misollar umumiy demo-ssenariy bo'yicha: kompaniya **DEMO Qurilish Savdo MChJ**, agent **Jasur** (`demo_agent`), ekspeditor **Sherzod** (`demo_eksp`), mashina **01 A 123 AA**, mijoz **DEMO Anvar do'koni**, rahbar `demo_rahbar`, omborchi `demo_ombor`.

## 6.1 Asosiy tushunchalar

- **Bot** — kompaniyaning Telegram boti (masalan `@demo_qurilish_bot`). Har kompaniyaning boti alohida; ERP bilan bot xizmati orqali bog'langan. Bot faqat **shaxsiy chatda** ishlaydi — guruhga qo'shilsa javob bermaydi.
- **Ulanish (bog'lash)** — Telegram hisobini ERP foydalanuvchisiga yoki mijozga biriktirish. Bir martalik **ulash kodi** bilan bajariladi. Bitta Telegram hisobi bir vaqtda faqat bitta ERP foydalanuvchisi yoki mijozga ulangan bo'ladi.
- **Rol** — bot nimani ko'rsatishini belgilaydi: **agent**, **ekspeditor**, **rahbar**, **omborchi** (xodimlar) yoki **mijoz**. Rol ulash kodi berilayotganda tanlanadi.
- **Asosiy menyu** — chat pastidagi doimiy tugmalar (klaviatura). Har rolda o'z tugmalari bor.
- **Inline tugmalar** — xabar tagidagi tugmalar (mijoz kartasi, to'lov usuli, tasdiqlash va h.k.).
- **Mini App** — tugma bosilganda Telegram ichida ochiladigan oyna: buyurtma oynasi (agent), yetkazish oynasi (ekspeditor), ombor ilovasi (omborchi va rahbar). Mini App faqat Telegram ichida ishlaydi; brauzerda ochilsa "Bu sahifa Telegram ichida ochiladi." deydi.
- **Bildirishnoma** — ERP'dagi hodisa haqida botning o'zi yuboradigan xabar (buyurtma tasdiqlandi, reys biriktirildi, yetkazildi va h.k.). Xabar faqat Telegram'ga ulangan odamga ketadi. Bot vaqtincha ishlamay qolsa, xabarlar ERP navbatida kutadi va bot qayta ishga tushganda yuboriladi.

### Kim nimani bajaradi

| Rol | Asosiy menyu | Mini App | Asosiy ishlar |
|---|---|---|---|
| Agent | ▶️ Kunni boshlash, 🗺 Marshrut, 🔎 Mijoz, ➕ Yangi nuqta, 💵 Kassa, 📡 Jonli joylashuv, 🏁 Kunni yakunlash, ⚠️ Muammo | Buyurtma oynasi | Tashrif, buyurtma, to'lov, foto, yangi nuqta, naqdni topshirish |
| Ekspeditor | 🚚 Reys, 💵 Kassa, 📡 Jonli joylashuv, ⚠️ Muammo, 🏁 Reysni yakunlash | Yetkazish oynasi | Reys manzillari, yetkazish (rasm/imzo), qisman/rad, to'lov, xarajat, reysni yakunlash |
| Omborchi | 📦 Ombor, ⚠️ Muammo | Ombor ilovasi | Inventarizatsiya sanog'i, buyurtma yig'ish va jo'natish |
| Rahbar | 📊 Bugungi yakun, 📦 Ombor, ⚠️ Muammo | Ombor ilovasi | Xabarlar, kredit va hisobdan chiqarish tasdig'i, kunlik yakun |
| Mijoz | 💳 Qarzim, 📄 Akt sverki | — | Buyurtma holati xabarlari, nakladnoy PDF, qarz va akt sverki |

> **Eslatma:** Bot amallari ERP'dagi xuddi shu foydalanuvchi nomidan bajariladi — web sahifadagi ruxsatlar, ko'rinish (scope: hudud, mijoz, ombor) va biznes qoidalari (kredit limiti, minimal buyurtma, qoldiq) botda ham amal qiladi.

## 6.2 Botni ulash (sozlash)

**Kim:** Admin (bir marta)   **Qayerda:** server + web menyu Distributsiya → Sozlamalar

Botning o'zini (BotFather tokeni, server) tizim administratori o'rnatadi. Foydalanuvchi tomonidan ERP'da faqat quyidagilar to'ldiriladi:

1. Distributsiya → **Sozlamalar** → "Umumiy sozlamalar" kartasida **Telegram bot nomi** maydoniga bot nomini yozing (masalan `demo_qurilish_bot`, `@` siz). Bo'lmasa ulash oynasi havola o'rniga "Bot nomi sozlanmagan (Sozlamalar → Umumiy). Kodni botga qo'lda yuboring." deydi.
2. **Bot ochiq manzili (rasmlar uchun)** maydoniga bot manzilini yozing (`https://...`). Usiz selfi, tashrif fotosi, yetkazish rasmi va imzo ERP sahifalarida ko'rinmaydi (Dala nazorati sahifasi "Rasmlarni ko'rish uchun Sozlamalarda bot manzilini (Bot ochiq manzili) kiriting." deydi).
3. Shu sahifada dala ishiga oid sozlamalar: **Tashrif radiusi, m** (standart 200), **Kun boshida selfi majburiy**, **Keldim — faqat jonli joylashuv** (standart o'chiq), **Yirik buyurtma, so'm** (0 — xabar yuborilmaydi), **Agent to'lovlari** (agent botda qaysi usulda to'lov qabul qila oladi). Ular quyida tegishli bo'limlarda tushuntirilgan.

## 6.3 Xodim va mijozni botga ulash

### 6.3.1 Xodimga ulash kodi berish

**Kim:** Rahbar / Admin (Jamoalar sahifasida o'zgartirish huquqi bilan)   **Qayerda:** web menyu Distributsiya → Agentlar va jamoalar

Ikki usul bor:

- **Agent yoki ekspeditor uchun** — "Agentlar" jadvalida xodim qatoridagi Telegram tugmasini (ko'k, Telegram belgisi) bosing. Rol avtomatik: agent ro'yxatdan o'tgan bo'lsa — **Agent**, "Ekspeditor" belgisi bo'lsa — **Ekspeditor**.
- **Istalgan xodim uchun** (rahbar, omborchi ham) — chap tomondagi **Xodimni botga ulash** kartasida foydalanuvchini va rolni (**Rahbar**, **Omborchi**, **Agent**, **Ekspeditor**) tanlab **Kod olish** ni bosing.

Ochilgan **Telegram'ga ulash** oynasida:

| Element | Mazmuni |
|---|---|
| Kod | 10 belgili kod, masalan `K7MRT2XQ9P` (chalkashtiriladigan 0/O, 1/I belgilarisiz) |
| Havola | `https://t.me/<bot nomi>?start=<kod>` — nusxa olish tugmasi bilan |
| Amal qiladi | Kod muddati — berilgan paytdan **3 kun** |

Havolani xodimga yuboring (SMS, Telegram, WhatsApp) yoki kodni aytib bering.

> **Muhim:** Kod **bir martalik**. Xuddi shu xodimga yangi kod berilsa, avvalgi ishlatilmagan kod darhol bekor bo'ladi.

### 6.3.2 Mijozga ulash kodi berish

**Kim:** Mijozlar sahifasida o'zgartirish huquqi bor xodim   **Qayerda:** web menyu Distributsiya → Mijozlar → mijoz kartasi

1. Mijoz kartasini oching (masalan **DEMO Anvar do'koni**).
2. **Telegram** tugmasini bosing — xuddi shu "Telegram'ga ulash" oynasi chiqadi, rol avtomatik **mijoz**.
3. Havolani mijozga (do'kon egasiga) yuboring.

### 6.3.3 Telegram'da ulanish

**Kim:** xodim yoki mijoz   **Qayerda:** bot

1. Havolani oching — Telegram botni ochadi; **Start** (yoki **Boshlash**) ni bosing. Havola bo'lmasa, botga `/start K7MRT2XQ9P` ko'rinishida yozing.
2. Bot javobi: `✅ Jasur, siz ulandingiz. Rol: agent.` va rolga mos asosiy menyu.
3. Kod noto'g'ri, ishlatilgan yoki muddati o'tgan bo'lsa: `⚠️ Kod noto'g'ri yoki muddati o'tgan.` — rahbardan yangi kod so'rang.

Ulanmagan odam botga nima yozsa ham javob bitta: "Assalomu alaykum! Botdan foydalanish uchun rahbaringizdan ulanish kodini oling va havolani oching yoki /start KOD yuboring."

### 6.3.4 Qayta ulash, uzish va til

| Vaziyat | Nima qilinadi |
|---|---|
| Xodim telefonini (Telegram hisobini) almashtirdi | Yangi kod bering, xodim yangi hisobdan ulanadi. Shu xodimning eski Telegram ulanishi avtomatik o'chadi |
| Bitta telefondan boshqa rolga o'tish | Yangi kod (yangi rol bilan) — eski ulanish o'chadi, yangisi ishlaydi |
| Botdan uzilish | Botga `/stop` yozing → "Bot hisobingizdan uzildi. Qayta ulash uchun yangi kod oling." Menyu yo'qoladi, xabarlar kelmaydi |
| Menyu yo'qolib qoldi | `/menu` yoki `/start` (kodsiz) yuboring — menyu qayta chiqadi |
| Joriy amalni to'xtatish | **✖️ Bekor qilish** tugmasi yoki `/menu` → "Bekor qilindi." |
| Xodim ishdan ketdi | ERP'da foydalanuvchini faolsizlantiring — uning kodi ishlamaydi; ulangan bo'lsa, botga `/stop` yuborishini so'rang |

**Til.** Bot o'zbek va rus tillarida ishlaydi:

- **Xodim** uchun til — ERP'dagi shaxsiy til (Distributsiya bosh sahifasidagi til tanlovi). ERP'dan keladigan bildirishnomalar joriy tilda tayyorlanadi. Tilni o'zgartirgandan keyin botga **/start** yoki **/menu** yuboring — menyu va tugmalar yangi tilga o'tadi (qayta ulash shart emas).
- **Mijoz** uchun bot tugmalari, bildirishnomalar va nakladnoy PDF tili — mijozning Telegram ilovasi tili (rus tilidagi Telegram — rus, qolganlari — o'zbek). Til ulanishda va har **/start**, **/menu** da yangilanadi.
- Mini App tili ulanishdagi tilga mos.

## 6.4 Agent

**Kim:** agent (masalan Jasur)   **Qayerda:** bot + buyurtma Mini App

Agent menyusi:

```
[ ▶️ Kunni boshlash ] [ 🗺 Marshrut      ]
[ 🔎 Mijoz          ] [ ➕ Yangi nuqta   ]
[ 💵 Kassa          ] [ 📡 Jonli joylashuv ]
[ 🏁 Kunni yakunlash ] [ ⚠️ Muammo       ]
```

Odatiy ish kuni:

```
▶️ Kunni boshlash (joylashuv + selfi)
   → 🗺 Marshrut → mijoz → 📍 Keldim (joylashuv)
        → 🛒 Buyurtma (Mini App) / 💵 To'lov / 🚫 Buyurtmasiz / 📷 Foto
   → keyingi mijoz ...
→ 💵 Kassa → 📤 Kassaga topshirish
→ 🏁 Kunni yakunlash (joylashuv) → kun yakuni
```

### 6.4.1 Kunni boshlash

1. **▶️ Kunni boshlash** ni bosing. Bot: "Ish kunini boshlash uchun joylashuvingizni yuboring." va **📍 Joylashuvni yuborish** tugmasi.
2. **📍 Joylashuvni yuborish** ni bosing (Telegram ruxsat so'rasa — ruxsat bering).
3. Bot selfi so'raydi:
   - Sozlamada "Kun boshida selfi majburiy" **o'chiq** bo'lsa: "📸 Selfi yuboring yoki ⏭ O'tkazib yuborish tugmasini bosing."
   - **Yoqiq** bo'lsa: "📸 Endi selfi yuboring (kamera bilan, hozir tushiring)." — ⏭ bosilsa "Kunni boshlash uchun selfi kerak." deydi.
4. Old kamerada selfi tushirib yuboring.
5. Bot: `✅ Ish kuni boshlandi (08:45). Omad!`, jonli joylashuvni yoqish haqida eslatma va darhol bugungi marshrut.

> **Eslatma:** Kun allaqachon boshlangan bo'lsa, selfi qayta so'ralmaydi. Selfi, kun boshi joyi va vaqti rahbarga **Dala nazorati** sahifasida ko'rinadi.

### 6.4.2 Bugungi marshrut

**🗺 Marshrut** bosilganda: `🗺 Bugungi marshrut: 12 ta mijoz, 3 tasiga borildi.` va har mijoz uchun tugma (40 tagacha) hamda **🔄 Yangilash**.

Marshrutga kiradiganlar: ofis shu kunga saqlagan **tashrif rejasi** (7.10-bo'lim, o'sha tartibda), reja bo'lmasa — mijoz kartasida tashrif kuni bugun bo'lgan agentning o'z mijozlari (nom bo'yicha); oxirida — bugun rejadan tashqari borilganlar.

| Belgi | Ma'nosi |
|---|---|
| ⏳ | Rejada, hali borilmagan |
| 🟢 | Tashrif ochilgan, natija yo'q |
| ✅ | Buyurtma olingan |
| 💵 | To'lov olingan |
| 🚫 | Buyurtmasiz (sabab bilan) |
| `· 1 250 000` | Mijozning qarzi; **❗** — muddati o'tgan qarz bor |

Marshrut bo'sh bo'lsa: "Bugun uchun marshrutda mijoz yo'q. 🔎 Mijoz tugmasi bilan qidiring."

### 6.4.3 Mijozni qidirish

1. **🔎 Mijoz** → "Mijoz nomi, telefoni yoki STIR'ini yozing."
2. Masalan `Anvar` deb yozing. Bot: `Topildi: 2` va 20 tagacha mijoz tugmasi. Oldida **⚠️** bo'lganlar — ofis hali tasdiqlamagan nuqtalar.
3. Hech narsa topilmasa: "Hech narsa topilmadi."

Qidiruv faqat agentning ko'rinishidagi (o'z hududi, o'z mijozlari) mijozlar orasida.

### 6.4.4 Mijoz kartasi

Mijoz tugmasi bosilganda karta chiqadi:

```
🏪 DEMO Anvar do'koni
📍 Chilonzor 9-kvartal, bozor yonida
☎️ +998901234567
Narx turi: Chakana
Qarz: 3 400 000 so'm (muddati o'tgan: 0)
Limit: 20 000 000 so'm, muddat: 7 kun

💡 Tavsiya:
• DEMO-SEM-400 Sement M400 — ~40 (9 kun oldin) ⏰
```

- **💡 Tavsiya** — mijoz oxirgi 90 kunda kamida 2 marta olgan, lekin odatdagidan uzoq vaqt olmagan tovarlar (5 tagacha). **⏰** — odatiy oraliqdan ancha uzoq. Miqdor asosiy birlikda o'rtacha.
- "⚠️ Mijoz hali tasdiqlanmagan." — yangi nuqta, ofis tasdiqlamagan.
- "🟢 Tashrif ochiq." — bugun shu mijozda "Keldim" bosilgan.

Karta tugmalari:

| Tashrif ochilmagan | Tashrif ochiq |
|---|---|
| 📍 Keldim · 🛒 Buyurtma | 🛒 Buyurtma |
| 💵 To'lov | 💵 To'lov · 🚫 Buyurtmasiz |
| 🧭 Xarita (koordinata bo'lsa) | 📷 Foto · 🧭 Xarita |
| ⬅️ Orqaga | ⬅️ Orqaga |

**🧭 Xarita** — mijoz joylashuvini chatga xarita nuqtasi qilib yuboradi (Telegram'da bosib navigatorda ochish mumkin).

### 6.4.5 "Keldim" — tashrifni boshlash

1. Kartada **📍 Keldim** ni bosing. Bot: "Mijoz oldidaligingizni tasdiqlash uchun joylashuvni yuboring."
2. **📍 Joylashuvni yuborish** ni bosing.
3. Bot masofani tekshiradi:
   - Mijozda koordinata bor va siz **Tashrif radiusi** (standart 200 m) ichidasiz → `✅ Tashrif boshlandi (35 m).` va karta qayta chiqadi.
   - Uzoqdasiz → `⚠️ Siz mijozdan 350 m uzoqdasiz (ruxsat: 200 m).` — yaqinroq boring va qayta urining.
   - Mijozda koordinata yo'q → tashrif qabul qilinadi, masofa yozilmaydi.

Qoidalar:

- GPS aniqligi hisobga olinadi: telefon "aniqlik 50 m" desa, ruxsat 200 + 50 m bo'ladi (qo'shimcha ko'pi bilan radiusga teng).
- Kun boshlanmagan bo'lsa, "Keldim" kunni o'zi boshlaydi. Lekin selfi majburiy bo'lsa — "Avval ish kunini boshlang." deydi.
- Tashrif rejadami yoki rejadan tashqarimi — avtomatik belgilanadi (rahbar Tashrif rejasi sahifasida ko'radi).
- Oddiy (jonli bo'lmagan) joylashuv standart bo'yicha qabul qilinadi, lekin Dala nazoratida va tashriflar hisobotida "statik" deb belgilanadi. Sozlamalarda **Keldim — faqat jonli joylashuv** yoqilgan bo'lsa, statik joylashuv rad etiladi: `"Keldim" uchun jonli joylashuv kerak: 📎 → Joylashuv → "Jonli joylashuvni ulashish".` (**📍 Joylashuvni yuborish** tugmasi faqat statik joylashuv yuboradi — shuning uchun sozlama standart o'chiq.)

### 6.4.6 Buyurtma (Mini App)

**Qayerda:** mijoz kartasidagi **🛒 Buyurtma** tugmasi → Mini App

Oyna tepasida mijoz nomi va qarzi (`Qarz: 3 400 000 so'm`, muddati o'tgan bo'lsa qizil — `(muddati o'tgan: 1 200 000)`), ikkita yorliq: **Katalog** va **Savat (n)**.

**Katalog:**

1. Qidiruv maydoni — "Nomi, artikul yoki shtrix-kod"; yonida skaner tugmasi (shtrix-kod belgisi).
2. Kategoriya tugmalari (**Hammasi**, ...) va mijozga **💡 Tavsiya** tugmalari (bosilsa shu tovar qidiriladi).
3. Har tovar qatorida: nomi, aksiya bo'lsa **🏷** belgisi bilan aksiya nomi, `artikul · Qoldiq: 960` (qoldiq 0 bo'lsa qizil), narx (`62 000 so'm / qop` yoki "narx yo'q"). Narx — mijozning narx turi bo'yicha.
4. Tovarda bir nechta birlik bo'lsa ro'yxatdan tanlang: masalan `qop` yoki `poddon (×40)`. Narx tanlangan birlikka o'zgaradi.
5. **Qo'shish** ni bosing → miqdor tugmalari **−  1  +** chiqadi; miqdorni qo'lda ham yozish mumkin (kasr ham: `1,5`). 0 qilinsa savatdan chiqadi.

**Savat:**

- Har qator: `20 qop × 62 000 = 1 240 000 so'm` va **O'chirish**.
- **Yetkazish sanasi** (ixtiyoriy), **Izoh** (500 belgigacha).
- **Mijoz imzosi (ixtiyoriy)** — barmoq bilan imzo chiziladi; **Tozalash** — qayta chizish.
- **Jami** — narxlar bo'yicha summa. Savatda aksiyali tovar bo'lsa ostida: "Aksiya chegirmasi va sovg'alar buyurtma qabul qilinganda hisoblanadi."

**Yuborish:** pastdagi katta Telegram tugmasi katalogda `Savat (2) · 1 680 000` ko'rinishida (savatga o'tadi), savatda — `Buyurtma berish · 1 680 000`. Bosilgach oynada "Buyurtma D-2026-00045 qabul qilindi" chiqadi va oyna yopiladi; chatga xabar keladi:

`✅ Buyurtma D-2026-00045 qabul qilindi: 1 680 000 so'm. Ofis tasdiqlaydi.`

> **Muhim:** Agent buyurtmasi **qoralama** sifatida tushadi — ofis tasdiqlaydi (rezerv, kredit tekshiruvi). Tasdiqlanganda agentga "Buyurtma D-2026-00045 tasdiqlandi: ..." xabari keladi, bekor qilinsa — sababi bilan.
> **Eslatma:** "Buyurtma berish"ni ikki marta bosish yoki aloqa uzilib qayta yuborish ikkinchi buyurtma yaratmaydi — bitta savat bitta buyurtma.
> **Eslatma:** Aksiya chegirmasi va sovg'a qatorlari (masalan "Sement 10+1") Mini App jamisida ataylab ko'rinmaydi — aksiya buyurtma qabul qilinganda ERP'da hisoblanadi (savatda shu haqda ogohlantirish bor). Agentga keladigan "qabul qilindi" xabaridagi summa — ERP hisoblagan yakuniy summa.
> **Eslatma:** Savat telefon xotirasida **3 kun** saqlanadi — oyna yopilsa ham yo'qolmaydi.
> **Eslatma:** Buyurtma oynasi faqat agent va ekspeditor uchun — rahbar uni ocha olmaydi.

**Shtrix-kod skaneri:**

1. Qidiruv yonidagi skaner tugmasini bosing — orqa kamera ochiladi ("Shtrix-kodni kameraga tuting").
2. Kodni ramkaga tuting. Har yangi kod savatga **+1** qo'shadi (shtrix-kod qaysi birlikka tegishli bo'lsa, o'sha birlikda — masalan quti kodi → +1 quti). Pastda: `Qo'shildi: Sement M400 — 3 qop`.
3. Xuddi shu tovarni yana qo'shish uchun kamerani boshqa tomonga burib qaytaring — kod kadrda turgan paytda (1,5 soniya) qayta sanalmaydi.
4. Kod topilmasa: `Topilmadi: 4780000000000`.
5. **Tayyor** — skanerni yopish.

Qo'llab-quvvatlanadigan kodlar: EAN-13, EAN-8, UPC-A/E, Code128, Code39, ITF. Kameraga ruxsat berilmasa — "Kameraga ruxsat berilmadi yoki kamera yo'q."; skaner ishlamaydigan qurilmada — "Bu qurilmada skaner ishlamaydi — kodni qidiruvga yozing."

### 6.4.7 Ovozli buyurtma

**Qachon ishlaydi:** ERP'da Mashinalar → Yetkazish sozlamalari → **Botda ovozli buyurtma** yoqilgan va bot serverida nutqni aniqlash kaliti sozlangan bo'lsa (7.12-bo'lim).

1. Botga ovozli xabar yuboring (90 soniyagacha), masalan: *"Anvar do'koniga 20 qop sement M400, 5 quti kafel"*.
2. Bot matnni va qoralamani ko'rsatadi:

```
🎤 «Anvar do'koniga 20 qop sement M400, 5 quti kafel»
Mijoz: DEMO Anvar do'koni
• DEMO-SEM-400 Sement M400 — 20 qop × 62 000 = 1 240 000
• DEMO-KAF-3030 Kafel 30×30 — 5 quti × 136 800 = 684 000
Jami: 1 924 000 so'm. Buyurtma berilsinmi?
```

3. Tugmalar: **✅ Buyurtma berish** (darhol oddiy buyurtma yaratiladi), **✖️ Bekor qilish**, **🛒 Buyurtma** (Mini App'ni ochib qo'lda tuzatish).
4. Mijoz aniqlanmasa: bir nechta o'xshash mijoz bo'lsa — "Mijozni tanlang:" (5 tagacha tugma); umuman topilmasa — "Qaysi mijozga? Nomini yozing." — nomni matn bilan yozing.
5. Tovar topilmagan qismlar: `Topilmadi: ...`. Hech bir tovar topilmasa — "Tovar topilmadi. Mini App'da buyurtma bering yoki qayta ayting."

| Bot javobi | Sabab |
|---|---|
| 🎤 Ovozli buyurtma o'chirilgan. | ERP sozlamasi o'chiq |
| 🎤 Ovozli buyurtma sozlanmagan (nutqni aniqlash kaliti yo'q). | Bot serverida kalit yo'q — administratorga |
| 🎤 Xabar juda uzun — 90 soniyagacha yozing. | Xabar uzun |
| 🎙 Ovozli buyurtma faqat agentlar uchun. | Ovozli xabarni ekspeditor (yoki boshqa rol) yuborgan |
| 🎤 Tushunilmadi. Aniqroq ayting: «Anvar do'koniga 20 qop sement M400, 5 quti kafel». | Nutq aniqlanmadi yoki tovar/miqdor ajratilmadi |

> **Eslatma:** Ovozli buyurtma ham ofis tasdig'iga tushadi; izohida `🎤` va aytilgan matn saqlanadi. Ovoz yozuvi saqlanmaydi. Mijoz faqat agentning ko'rinishidagi mijozlar orasidan tanlanadi.

### 6.4.8 To'lov qabul qilish

1. Kartada **💵 To'lov** → `💵 DEMO Anvar do'koni\nQarz: 3 400 000 so'm. Olingan summani yozing:`
2. Summani raqam bilan yozing: `1500000` yoki `1 500 000`. Noto'g'ri bo'lsa: "Summani raqam bilan yozing, masalan 250000."
3. To'lov usulini tanlang. Tugmalar — Sozlamalar → **Agent to'lovlari** da agentga ruxsat etilgan usullar: **Naqd**, **Karta**, **Click**, **Payme**, **Uzum**, **O'tkazma**.
4. Bot: `✅ To'lov DP-2026-00112 qabul qilindi: 1 500 000 so'm.\nQo'lingizda naqd: 2 300 000 so'm.`

- **Naqd** — pul agent qo'lida qoladi va keyin kassaga topshiriladi (6.4.13).
- **Karta, Click** va boshqalar — darhol Sozlamalarda shu usulga tanlangan hisobga tushadi.
- Mijoz Telegram'ga ulangan bo'lsa unga: `To'lovingiz qabul qilindi: 1 500 000 so'm (...). Qolgan qarz: 1 900 000 so'm.`

> **Eslatma:** Usul tugmasini qayta bosish ikkinchi to'lov yaratmaydi.

### 6.4.9 Buyurtmasiz tashrif

1. Tashrif ochiq kartada **🚫 Buyurtmasiz** → "Buyurtma bo'lmagan sababni tanlang:".
2. Sababni tanlang (ro'yxat — Distributsiya → Ma'lumotnomalar → Sabablar, turi "Buyurtmasiz tashrif"; masalan "Tovar hali bor", "Egasi yo'q").
3. Bot: `✅ Saqlandi: Tovar hali bor.` va marshrut qayta chiqadi (mijoz 🚫 bilan).

### 6.4.10 Javon fotosi

1. Tashrif ochiq kartada **📷 Foto** → "Rasmni yuboring (javon, vitrina)."
2. Kamerada tushirib yuboring → "✅ Rasm saqlandi." Foto tashrifga biriktiriladi va Dala nazoratida ko'rinadi.

Rasm 5 MB dan katta bo'lsa — "Rasm juda katta."; yuklab bo'lmasa — "Rasmni olib bo'lmadi, qayta yuboring."

### 6.4.11 Yangi savdo nuqtasi

1. **➕ Yangi nuqta** → "Nuqta (do'kon) nomini yozing:" — 2–50 belgi.
2. Telefon: `90 123 45 67` yoki `+998901234567` ko'rinishida yozing yoki kontaktni yuboring.
3. "Nuqta oldida turib, joylashuvni yuboring:" — **📍 Joylashuvni yuborish**.
4. Mo'ljal yoki manzil (masalan "bozor yonida") yoki **⏭ O'tkazib yuborish**.
5. Nuqta fotosi (vivyeska, peshtaxta) yoki **⏭ O'tkazib yuborish**.
6. Bot hammasini ko'rsatadi ("Tekshiring: ... Saqlaysizmi?") → **✅ Saqlash**.
7. Javob: "✅ Nuqta qo'shildi. Ofis tasdiqlagach, unga buyurtmalar tasdiqlanadi." va yangi nuqtaning kartasi.

Nuqta agentning o'ziga biriktiriladi, holati — tasdiqlanmagan. Rahbarlarga: `🆕 Yangi savdo nuqtasi: ... agent Jasur. Distributsiya → Mijozlar sahifasida tasdiqlang.` Aloqa uzilib qayta yuborilsa, bir soat ichidagi bir xil nomli nuqta qayta ochiladi ("Bu nuqta yaqinda qo'shilgan — o'shasi ochildi.") — dublikat yaratilmaydi.

### 6.4.12 Jonli joylashuv

Rahbar agentning qayerdaligini Dala nazorati sahifasida ko'rishi uchun Telegram'ning **jonli joylashuvi** (live location) ishlatiladi.

1. **📡 Jonli joylashuv** — bot yo'riqnoma yuboradi: 📎 → Joylashuv → «Jonli joylashuvni ulashish» → 8 soat.
2. Ulashish boshlanganda bot: "📡 Jonli joylashuv qabul qilindi. Ish kuni davomida rahbaringiz joylashuvingizni ko'radi."
3. Kun boshlanmagan bo'lsa: "Avval ▶️ Kunni boshlang — jonli joylashuv faqat ish kuni ochiq paytda yoziladi."

> **Eslatma:** Joylashuv faqat ish kuni ochiq paytda va ko'pi bilan 30 soniyada bir nuqta yoziladi. Kunni yakunlagach ulashishni Telegram'da to'xtating — shaxsiy vaqt kuzatilmaydi.

### 6.4.13 Kassa: qo'ldagi naqd va topshirish

1. **💵 Kassa** → `💵 Qo'lingizdagi naqd: 2 300 000 so'm.\nBugun yig'ilgan: 3 800 000 so'm.`
2. Qo'lda naqd bo'lsa **📤 Kassaga topshirish** → "2 300 000 so'mni kassaga topshirasizmi? Kassir sanab qabul qiladi." → **✅ Tasdiqlash**.
3. Bot: `✅ Topshirish DK-2026-00021: 2 300 000 so'm. Kassir qabul qilishini kuting.` Rahbarlarga: "Jasur kassaga 2 300 000 so'm topshirdi — qabul qiling."

Kassir pulni sanab ERP'da qabul qiladi (To'lovlar bo'limi). Qabul qilingunga qadar summa agent hisobida turadi.

### 6.4.14 Kunni yakunlash

1. **🏁 Kunni yakunlash** → joylashuvni yuboring.
2. Bot kun yakunini beradi:

```
🏁 Kun yakuni
Tashriflar: 11/12 (buyurtmasiz: 3)
Buyurtmalar: 7 ta, 14 600 000 so'm
Yig'ilgan: 3 800 000 so'm
Qo'lingizda naqd: 0 so'm
```

Ochiq tashriflar shu vaqt bilan yopiladi.

### 6.4.15 Muammo yoki xato haqida xabar (/xato)

**Kim:** ulangan har qanday xodim (mijoz emas)

1. **⚠️ Muammo** tugmasi yoki `/xato` (`/problem`) buyrug'i.
2. Bot: "Muammoni yozing: nima qildingiz, nima bo'ldi (buyurtma/mijoz raqami bo'lsa — qo'shing). Skrinshotni izoh bilan yuborishingiz mumkin."
3. Kamida 5 belgili matn yoki skrinshot (izohli yoki izohsiz) yuboring.
4. Bot: `✅ Qayd qilindi: P-007. Rahbarga xabar berildi, rahmat!` Muammo Distributsiya → **Xatolar jurnali** ga tushadi, rahbarlarga "⚠️ Yangi muammo ..." xabari ketadi.

## 6.5 Ekspeditor

**Kim:** ekspeditor (masalan Sherzod)   **Qayerda:** bot + yetkazish Mini App

Ekspeditor menyusi:

```
[ 🚚 Reys           ] [ 💵 Kassa     ]
[ 📡 Jonli joylashuv ] [ ⚠️ Muammo   ]
[ 🏁 Reysni yakunlash ]
```

Ekspeditorda alohida "Kunni boshlash" yo'q: ish kuni reys boshlanganda (**▶️ Yo'lga chiqish**) avtomatik, selfisiz ochiladi va reys yakunlanganda yoki yopilganda (boshqa yo'ldagi reysi bo'lmasa) yopiladi.

Reysni ofis rejalaydi (7.5-bo'lim). Reys biriktirilganda ekspeditorga xabar keladi:

`🚚 Sizga reys R-2026-00007 (21.11.2026) biriktirildi: mashina 01 A 123 AA, 6 ta manzil, 2 450 kg. Botda: 🚚 Reys.`

### 6.5.1 Bugungi reys

**🚚 Reys** bosilganda joriy reys ko'rsatiladi: avval **Yo'lda** holatidagi yakunlanmagan reys; bo'lmasa **Rejalandi** reyslardan sanasi bugunga eng yaqini — avval bugungi, keyin o'tib ketgani (sana cheklovi yo'q), keyin kelajakdagi. Yakunlangan (lekin hali yopilmagan) reys keyingi rejalangan reysni to'smaydi. Reys bo'lmasa: "Sizga hozircha reys biriktirilmagan."

```
🚚 Reys R-2026-00007 (21.11.2026) — Rejalandi
Mashina: 01 A 123 AA · 2450 kg · 9 860 000 so'm
Manzillar: 0/6 hal qilindi
```

Tugmalar:

- **▶️ Yo'lga chiqish** — faqat reys "Rejalandi" bo'lsa.
- Har manzil: `⏳ 1. DEMO Anvar do'koni · 1 240 000` (⏳ kutmoqda, ✅ yetkazildi, 🟡 qisman, 🚫 rad etildi).
- **🗺 Marshrut (Yandex)** — barcha manzillar tartib bilan Yandex Xaritalarda (ombor koordinatasi sozlangan bo'lsa — ombordan boshlab).
- **⛽ Xarajat**, **🔄 Yangilash**.

### 6.5.2 Yo'lga chiqish

1. Yuk ortilgach **▶️ Yo'lga chiqish** ni bosing.
2. Bot: `✅ Yo'lga chiqdingiz. Mijozlarga "yo'lga chiqdi" xabari yuborildi.` Shu payt ish kuningiz ochiladi — jonli joylashuvni ulashing (📡), u reys davomida yoziladi.

Telegram'ga ulangan har mijoz oladi: `🚚 Buyurtmangiz yo'lga chiqdi: D-2026-00045 (1 240 000 so'm).\nEkspeditor: Sherzod, +998901112233\nMashina: 01 A 123 AA`. Ofis ham reys sahifasidagi **Yo'lga chiqarish** bilan shuni bajarishi mumkin.

> **Muhim:** Reys "Yo'lda" bo'lmaguncha yetkazish natijasini kiritib bo'lmaydi.

### 6.5.3 Manzil kartasi

Manzil tugmasi bosilganda:

```
📍 1. DEMO Anvar do'koni
Chilonzor 9-kvartal, bozor yonida
☎️ +998901234567
Buyurtma D-2026-00045 (DJ-2026-00031)
Summa: 1 240 000 so'm · qarz: 3 400 000 so'm
Holat: Kutmoqda

Tovarlar:
• Sement M400 — 20 qop
```

Telefon raqami Telegram'da bosiladi — qo'ng'iroq qilish mumkin. Tugmalar:

| Tugma | Vazifasi |
|---|---|
| 🧭 Navigatsiya | Yandex Xaritalarda shu manzilgacha yo'l (mijozda koordinata bo'lsa) |
| ✅ Yetkazildi (rasm) | Hammasi topshirildi — rasm bilan tasdiq |
| ✍️ Imzo / qisman | Yetkazish Mini App: mijoz imzosi, qisman miqdorlar, rad |
| 🚫 Rad etildi | Mijoz umuman qabul qilmadi — sabab bilan |
| 💵 To'lov | Mijozdan pul olish |
| ⬅️ Reys | Reys ro'yxatiga qaytish |

Natija kiritilgan manzilda faqat **💵 To'lov** qoladi.

### 6.5.4 To'liq yetkazildi (rasm bilan)

1. **✅ Yetkazildi (rasm)** → "📷 Topshirilgan tovarning yoki imzolangan nakladnoyning rasmini yuboring."
2. Rasmni yuboring.
3. Bot: `✅ Yetkazildi: 1 240 000 so'm. Mijozga nakladnoy yuboriladi.` va yangilangan reys.

Mijozga `✅ Buyurtma D-2026-00045 yetkazildi (DJ-2026-00031): 1 240 000 so'm. Nakladnoy ilova qilingan.` xabari **nakladnoy PDF** fayli bilan keladi.

### 6.5.5 Imzo yoki qisman yetkazish (Mini App)

1. **✍️ Imzo / qisman** — yetkazish oynasi ochiladi: tepada `1. DEMO Anvar do'koni`, `D-2026-00045 · 1 240 000 so'm`.
2. Har tovar qatorida `20 qop jo'natilgan · 1 240 000` va miqdor maydoni (standart — hammasi). Mijoz olmagan miqdorni kamaytiring, masalan `17`.
3. **Yetkaziladi** — yetkaziladigan summa avtomatik qayta hisoblanadi.
4. **Sabab (qisman yoki rad bo'lsa)** — qisman bo'lsa majburiy (qaytarish sabablari ro'yxati).
5. **Izoh** — ixtiyoriy.
6. **Mijoz imzosi** — mijoz barmog'i bilan imzo chizadi; **Tozalash** — qayta.
7. Pastdagi **Tasdiqlash** tugmasi. Oyna "Saqlandi" deb yopiladi, chatga: `🟡 Qisman yetkazildi: 1 054 000 so'm (1 240 000 so'mdan).`

Oynadagi ogohlantirishlar: "Mijoz imzosini oling." (imzosiz tasdiqlab bo'lmaydi), "Qisman yetkazish yoki rad uchun sababni tanlang.", "Bu manzil bo'yicha natija allaqachon kiritilgan."

Shu oynadagi **Mijoz butunlay rad etdi** tugmasi — "Rad etilgani qayd qilinsinmi?" tasdig'idan keyin radni qayd qiladi (sabab majburiy, imzo ixtiyoriy).

Qisman yetkazishda mijozga: `✅ Buyurtma D-2026-00045 qisman yetkazildi (DJ-2026-00031): 1 054 000 so'm (1 240 000 so'mdan). Sabab: .... Nakladnoy ilova qilingan.` (PDF faqat yetkazilgan miqdorlar bilan). Rahbarlarga: `⚠️ Reys R-2026-00007: DEMO Anvar do'koni, buyurtma D-2026-00045 — Qisman. Sabab: .... Ekspeditor: Sherzod.`

### 6.5.6 Rad etildi

1. **🚫 Rad etildi** → "Rad etish sababini tanlang:" (qaytarish sabablari).
2. Sababni tanlang → "🚫 Rad etildi qayd qilindi. Tovar reys yopilganda omborga qaytariladi." Rad etilgan tovarni mijozga qayta yetkazish — ofis yopilgan reysdan **Qayta yetkazish** bilan yangi buyurtma ochadi (7-bob).

Mijozga: `🚫 Buyurtma D-2026-00045 qabul qilinmadi. Sabab: ....`; rahbarlarga — muammo xabari.

> **Muhim:** Botdan to'liq yoki qisman yetkazishda **rasm yoki imzo majburiy** ("Yetkazishni tasdiqlang: rasm yuboring yoki mijoz imzosini oling."). Natijani kiritgandan keyin ekspeditor uni o'zgartira olmaydi — xato bo'lsa ofisga ayting: ofis reys yopilguncha natijani **↺** bilan qaytaradi (buyurtma holati, foto/imzo va mijoz xabari ham qaytadi, 7.7-bo'lim), keyin natijani qayta kiritasiz.

### 6.5.7 Manzilda to'lov olish

1. Manzil kartasida **💵 To'lov** → `💵 DEMO Anvar do'koni\nBuyurtma: 1 240 000 so'm, qarz: 3 400 000 so'm. Olingan summani yozing:`
2. Summa → usul (agent to'lovi bilan bir xil ro'yxat) → `✅ To'lov DP-... qabul qilindi: ... Qo'lingizda naqd: ...`

To'lov shu manzildagi mijoz va buyurtmaga, izohida reys raqami bilan yoziladi. Naqd ekspeditor qo'lida qoladi.

### 6.5.8 Reys xarajati

1. Reys xabarida **⛽ Xarajat** → "Xarajat turi:" — **Yoqilg'i**, **To'xtash joyi**, **Ta'mir**, **Ovqat**, **Boshqa**.
2. Summani yozing, izoh bilan bo'lishi mumkin: `150000 AI-92`.
3. Bot: `✅ Xarajat qayd qilindi: 150 000 so'm. Ofis moliyaga o'tkazadi.`

Xarajat reys sahifasida "Kutmoqda" holatida, **bot** belgisi bilan chiqadi; moliyaga ofis o'tkazadi (7.9-bo'lim).

### 6.5.9 Reysni yakunlash

1. Hamma manzil bo'yicha natija kiritilgach **🏁 Reysni yakunlash**.
   - Kutayotgan manzil bo'lsa: "Hali 2 ta manzil bo'yicha natija yo'q." va reys ro'yxati.
   - Yo'lda reys bo'lmasa: "Yo'ldagi reys yo'q."
2. "Reys R-2026-00007ni yakunlaysizmi? Yetkazilmagan tovarni omborga topshiring — ombor sanaydi, reysni ofis yopadi." → **✅ Tasdiqlash**.
3. Bot yakun beradi:

```
🏁 Reys R-2026-00007 yakunlandi.
Yetkazildi: 4, qisman: 1, rad: 1
Yetkazilgan: 8 380 000 so'm, omborga topshiriladi: 1 480 000 so'm
Reysda yig'ilgan: 4 200 000 so'm, qo'lingizda: 4 200 000 so'm
Ombor tovarni sanab, reysni ofis yopadi.
```

4. Qo'lda naqd bo'lsa darhol kassa ma'lumoti va **📤 Kassaga topshirish** tugmasi chiqadi (6.4.13 bilan bir xil).

Rahbar va omborchiga: `🏁 Reys R-2026-00007 yakunlandi (…). Qaytadigan tovar: 1 480 000 so'm — omborda sanab, Reyslar sahifasida yoping (qaytgan miqdor va kamomad).`

> **Muhim:** Yakunlash reysni **yopmaydi** — reys "Yo'lda" qoladi, ombor hali hech narsa qabul qilmaydi. Yetkazilmagan tovarni omborga topshiring: ombor uni sanaydi, ofis **Reyslar → reys → Yopish** da haqiqatda qaytgan miqdorni kiritadi, farq kamomad sifatida qayd qilinadi (7.8-bo'lim). Yakunlangan reys keyingi rejalangan reysni to'smaydi — botda keyingi reys ko'rinadi.

## 6.6 Omborchi

**Kim:** omborchi (`demo_ombor`); rahbar ham ombor ilovasini ocha oladi   **Qayerda:** bot + ombor Mini App

Omborchi menyusi: **📦 Ombor** · **⚠️ Muammo** ("📦 Ombor — inventarizatsiya va buyurtma yig'ish (skaner bilan).").

### 6.6.1 Omborchiga keladigan xabarlar

| Hodisa | Xabar |
|---|---|
| Ofis buyurtmani tasdiqladi | `📦 Yig'ish kerak: buyurtma D-2026-00045 tasdiqlandi.` + mijoz, ombor, qatorlar soni, kg, summa, yetkazish sanasi yoki "o'zi olib ketadi" |
| Tovar minimal qoldiqdan pastga tushdi | `⚠️ Minimal qoldiqdan past — ...` |

Xabar faqat ombor ko'rinishi (scope) shu omborni qamragan omborchilarga ketadi; ko'rinish cheklanmagan omborchi hamma omborlar xabarini oladi. Tasdiqlagan odamning o'ziga yuborilmaydi.

### 6.6.2 Ombor ilovasini ochish

1. **📦 Ombor** → "Inventarizatsiya va buyurtma yig'ish — shtrix-kod skaneri bilan:" va **📦 Ombor ilovasi** tugmasi.
2. Ilovada ikki yorliq: **Inventarizatsiya** va **Yig'ish**.

Ilova faqat foydalanuvchining ombor ko'rinishidagi omborlarni ko'rsatadi.

### 6.6.3 Inventarizatsiya sanog'i (skaner bilan)

Sanoq hujjatini ofis ERP'da yaratadi (Distributsiya → Inventarizatsiya → Yangi sanoq). Ilovada faqat **ochiq (qoralama)** sanoqlar chiqadi; bo'lmasa — "Ochiq sanoq yo'q (ERP'da yarating)."

1. **Inventarizatsiya** yorlig'ida sanoqni tanlang: `DI-2026-00003 — DEMO Asosiy ombor (Toshkent)`, `Sanaldi: 0 / 3 qator`.
2. Tepadagi maydonlar: **Shtrix-kod yoki artikul**, miqdor (standart `1`) va skaner tugmasi.
3. Tovarni skanerlang yoki kodni yozib Enter bosing. Har skanerlash sanalganga **miqdor × birlik koeffitsienti** qo'shadi: qopdagi kod → +1 qop; poddondagi (40 qop) kod → +40. Pastda: `+40 Sement M400`.
4. Ko'p bir xil quti bo'lsa: miqdor maydoniga `12` yozing va bir marta skanerlang → +12.
5. Oxirgi skanerlangan tovar ro'yxat tepasida. Qatorga bosib aniq miqdor yozish mumkin ("Miqdor (bo'sh — sanalmagan):") — xato sanalganni tuzatish uchun.
6. Sanoqda yo'q tovar skanerlansa, u sanoqqa avtomatik qo'shiladi. Noma'lum kod: `Kod topilmadi: ...`.
7. **← Ro'yxatga** — sanoqlar ro'yxatiga qaytish. Sanoqni yakunlash (farqni qoldiqqa yozish) — ERP'da.

> **Eslatma:** Sanoq **"ko'r"** — ilovada tizimdagi qoldiq ko'rsatilmaydi, omborchi faqat haqiqatda borini sanaydi. Miqdorlar har skanerlashda darhol ERP'ga yoziladi.

### 6.6.4 Buyurtmani yig'ish va jo'natish

1. **Yig'ish** yorlig'ida yig'iladigan buyurtmalar: `D-2026-00045 — DEMO Anvar do'koni`, `tasdiqlangan · 22.11.2026 · 1000 kg`. Holatlar: **tasdiqlangan**, **yig'ilmoqda**, **qisman jo'natilgan**. Bo'lmasa — "Yig'iladigan buyurtma yo'q."
2. Buyurtmani oching. Har qator: `DEMO-SEM-400 · kerak 20 qop` (sotish birligi boshqa bo'lsa qavsda, masalan `kerak 40 m² (27,78 quti)`).
3. Pastdagi **Yig'ishni boshlash** ni bosing — buyurtma holati "yig'ilmoqda" bo'ladi, tugma **Jo'natish** ga aylanadi.
4. Tovarlarni skanerlang: miqdor koeffitsient bilan mos qatorga qo'shiladi, to'lgan qator **✅** bilan belgilanadi; `Yig'ildi: 1 / 2` progressi.
   - Buyurtmada yo'q tovar: `Buyurtmada yo'q: ...`
   - Kerakdan ortiq: `Ortiqcha: ...` (qator kerakli miqdorgacha to'ldiriladi, ortig'i qo'shilmaydi).
   - Miqdorni **− / +** yoki qo'lda ham o'zgartirish mumkin.
5. **Jo'natish** → "Yig'ilgan miqdor jo'natiladi. Davom etasizmi?" → OK. Natija: `Jo'natildi: DJ-2026-00031`.

Jo'natish ERP'dagi ofis "Jo'natish" amali bilan bir xil: ombordan chiqim, mijoz qarzi, mijozga "Buyurtmangiz D-2026-00045 jo'natildi ..." xabari. Qisman yig'ilgan bo'lsa buyurtma "qisman jo'natilgan" bo'lib ro'yxatda qoladi. Jo'natish (DJ) yetkazish uchun bo'lsa, u reys rejalashda paydo bo'ladi (7.5-bo'lim).

> **Eslatma:** Yig'ilgan miqdorlar har o'zgarishda telefon xotirasida saqlanadi (**2 kun**) — ilova yopilsa yoki ro'yxatga qaytilsa ham yig'ish o'sha joydan davom etadi. Jo'natilgach saqlangan natija o'chadi. Hech narsa yig'ilmagan bo'lsa: "Hali hech narsa yig'ilmadi."

## 6.7 Rahbar

**Kim:** rahbar (`demo_rahbar`, botga **Rahbar** roli bilan ulangan)   **Qayerda:** bot

Rahbar menyusi: **📊 Bugungi yakun** · **📦 Ombor** / **⚠️ Muammo**.

### 6.7.1 Rahbarga keladigan xabarlar

| Hodisa | Xabar | Amal |
|---|---|---|
| Yirik buyurtma (Sozlamalar → "Yirik buyurtma, so'm" dan katta) | `Yirik buyurtma D-2026-00045: DEMO Bunyodkor qurilish, 48 000 000 so'm.` | — |
| Buyurtma kredit shartidan chiqdi (limit yoki muddati o'tgan qarz) — yaratishda, tahrirdan keyin yoki tasdiqlash qarz sabab rad etilganda; bir buyurtmaga kuniga bir marta | `Buyurtma D-2026-00046 (DEMO Anvar do'koni, 25 000 000 so'm) kredit shartidan chiqdi — tasdiqlash kerak:` | **✅ Tasdiqlash** |
| Hisobdan chiqarish tasdiqqa yuborildi | `🗑 Hisobdan chiqarish DW-2026-00004 tasdiqni kutmoqda` + ombor, sabab, tuzgan | **✅ Tasdiqlash** / **❌ Rad etish** |
| Tovar minimal qoldiqdan past | `⚠️ Minimal qoldiqdan past — ...` | — |
| Ko'chirma farq bilan qabul qilindi | `📦 Ko'chirma farqi DT-...: ombor → ombor` + kamomad, yo'qotish, ortiqcha (summalar — tannarx huquqi bilan, aks holda faqat qatorlar soni) | — |
| Agent yangi nuqta qo'shdi | `🆕 Yangi savdo nuqtasi: ...` | ERP'da tasdiqlash |
| Agent/ekspeditor naqdni topshirdi | `... kassaga ... so'm topshirdi — qabul qiling.` | Kassir qabul qiladi |
| Yangi muammo (⚠️ Muammo) | `⚠️ Yangi muammo P-...: ...` | Xatolar jurnali |
| Yetkazishda qisman yoki rad | `⚠️ Reys R-...: mijoz, buyurtma ... — Qisman. Sabab: .... Ekspeditor: ...` | — |
| Ekspeditor reysni yakunladi (omborchiga ham) | `🏁 Reys R-... yakunlandi (...). Qaytadigan tovar: ... so'm — omborda sanab, Reyslar sahifasida yoping (qaytgan miqdor va kamomad).` | Ombor sanaydi, ofis yopadi |
| Reys yopildi | `🏁 Reys R-2026-00007 yopildi: yetkazildi 5/6, ... so'm; omborga qaytdi ... so'm, kamomad ... so'm; xarajat ... so'm.` | — |

Amalni bajargan rahbarning o'ziga (masalan reysni o'zi yopsa) xabar yuborilmaydi.

### 6.7.2 Kredit limitidan oshgan buyurtmani tasdiqlash

1. Xabar tagidagi **✅ Tasdiqlash** ni bosing.
2. Bot: `✅ Buyurtma D-2026-00046 tasdiqlandi.` Tugma xabardan olib tashlanadi (ikkinchi marta bosilmaydi). Izohga "Telegram" yoziladi.

Buning uchun ERP'da foydalanuvchida kredit shartini chetlab tasdiqlash huquqi bo'lishi kerak; bo'lmasa — "Bu amal sizning rolingiz uchun emas." yoki ERP xatosi.

### 6.7.3 Hisobdan chiqarishni tasdiqlash yoki rad etish

1. **✅ Tasdiqlash** → `✅ Hisobdan chiqarish DW-2026-00004 tasdiqlandi (tannarx 120 000).` — tovar ombordan chiqariladi.
2. **❌ Rad etish** → `❌ Hisobdan chiqarish DW-2026-00004 rad etildi.` (sabab sifatida "Telegram" yoziladi).

Hujjatni tuzgan xodimga ham natija xabari boradi. Hisobdan chiqarishni tasdiqlash huquqi kerak; o'zi tuzgan hujjatni faqat Admin tasdiqlaydi.

### 6.7.4 Kunlik yakun

Bot har kuni kechqurun (standart — Toshkent vaqti bilan 20:00 dan keyin, kuniga bir marta) barcha rahbarlarga yuboradi; **📊 Bugungi yakun** tugmasi bilan istalgan payt so'rash mumkin:

```
Kun yakuni 21.11.2026
Buyurtmalar: 23 ta, 61 400 000 so'm
Jo'natildi: 48 900 000 so'm
Yig'ilgan pul (to'lovlar): 17 300 000 so'm
Umumiy qarz: 212 000 000 so'm (muddati o'tgan: 18 500 000)
Faol agentlar: 5/6
Kun yetakchisi: Jasur — 14 600 000 so'm
```

"Yig'ilgan pul (to'lovlar)" — shu kunda olingan mijoz to'lovlari (5.8). "Faol agentlar" — bugun kunini boshlagan agentlar / jami faol agentlar. Ma'lumot tayyor bo'lmasa: "Yakun hozircha tayyor emas."

## 6.8 Mijoz

**Kim:** botga ulangan mijoz (do'kon egasi)   **Qayerda:** bot

Mijoz menyusi: **💳 Qarzim** · **📄 Akt sverki** ("Buyurtmalaringiz holati va to'lovlar haqida xabarlar shu yerga keladi. Qarzingiz va akt sverki — pastdagi tugmalarda.").

### 6.8.1 Holat xabarlari

| Hodisa | Xabar mijozga |
|---|---|
| Buyurtma jo'natildi (ombordan chiqdi) | `Buyurtmangiz D-2026-00045 jo'natildi: 1 240 000 so'm. Joriy qarzingiz: 4 640 000 so'm.` (o'zi olib ketishda bu xabar o'rniga bitta "yetkazildi" xabari) |
| Reys yo'lga chiqdi | `🚚 Buyurtmangiz yo'lga chiqdi: D-2026-00045 (1 240 000 so'm).` + ekspeditor, telefoni, mashina |
| To'liq yetkazildi | `✅ Buyurtma ... yetkazildi (...): ... so'm. Nakladnoy ilova qilingan.` + **PDF** |
| Qisman yetkazildi | `✅ Buyurtma ... qisman yetkazildi (...): ... so'm (... so'mdan). Sabab: .... Nakladnoy ilova qilingan.` + **PDF** |
| Rad etildi | `🚫 Buyurtma ... qabul qilinmadi. Sabab: ....` |
| Ofis buyurtmani "Yetkazildi" deb belgiladi (reyssiz) | `Buyurtmangiz ... yetkazildi: ... so'm. Rahmat!` |
| Ofis yetkazish natijasini qaytardi (↺), xabar allaqachon ketgan bo'lsa | `↺ yetkazish natijasi bekor qilindi va qayta kiritiladi` |
| To'lov qabul qilindi | `To'lovingiz qabul qilindi: ... so'm (...). Qolgan qarz: ... so'm.` |

### 6.8.2 Nakladnoy PDF

Yetkazish xabariga `Nakladnoy_DJ-2026-00031.pdf` fayli biriktiriladi. Hujjatda: "NAKLADNOY № DJ-2026-00031" (qisman bo'lsa "(qisman yetkazilgan)"), buyurtma raqami va sana, yetkazib beruvchi va qabul qiluvchi (STIR bilan), tovarlar jadvali (№, Tovar, Miqdor, Narx, Summa) — **faqat yetkazilgan miqdor**, Jami, mijozning joriy qarzi, reys/ekspeditor/mashina/yetkazilgan vaqt va "Topshirdi" / "Qabul qildi" imzo joylari.

> **Eslatma:** PDF tayyorlab bo'lmasa (masalan serverda shrift muammosi), mijoz baribir yetkazish xabarini oddiy matn sifatida oladi.

### 6.8.3 Qarzim

**💳 Qarzim** bosilganda:

```
💳 DEMO Anvar do'koni
Qarz: 4 640 000 so'm
  muddati kelmagan: 3 440 000
  muddati o'tgan: 1 200 000 (eng ko'pi 12 kun)
Limit: 20 000 000 so'm, to'lov muddati 7 kun

So'nggi to'lovlar:
• 18.11.2026 — 1 500 000 so'm
```

Qarz bo'lmasa: "Qarzingiz yo'q. ✅"; oldindan to'lov bo'lsa — "Avans (oldindan to'langan): ... so'm".

### 6.8.4 Akt sverki

1. **📄 Akt sverki** → davr: **Shu oy**, **O'tgan oy**, **Oxirgi 3 oy**.
2. Bot aktni yuboradi: boshlang'ich qoldiq, har hujjat qatori (`18.11 To'lov DP-...: −1 500 000 → 3 400 000`), jami `+... / −...`, yakuniy qoldiq. "+" — qarz oshgan, "−" — kamaygan.
3. Yozuvlar Telegram xabariga sig'masa oxirida: "… yana 14 ta yozuv (to'liq akt — ofisdan)."

## 6.9 Telegram cheklovlari va maslahatlar

| Talab | Holat | Izoh va maslahat |
|---|---|---|
| Oflayn ishlash | Yo'q | Bot va Mini App internet talab qiladi. Aloqa yo'q joyda buyurtmani tuzib, aloqa chiqqanda yuboring; qayta yuborish ikkinchi buyurtma yaratmaydi. Mini App savati telefonda 3 kun saqlanadi — oyna yopilsa ham yo'qolmaydi |
| Soxta GPS'ni aniqlash | Yo'q | Telegram imkon bermaydi. Bilvosita nazorat: jonli/statik belgisi, GPS aniqligi, vaqt, selfi, javon fotosi (Dala nazorati) |
| "Keldim" radiusi | Bor | Radius sozlamada; GPS aniqligi hisobga olinadi. Bino ichida GPS yomon — eshik oldida yuboring |
| Selfi "hozir tushirilgan"mi | Qisman | Gallereyadan yuborilgan rasmni ajratib bo'lmaydi. Rahbar selfilarni Dala nazoratida ko'rib boradi |
| Kamera (skaner) | Bor | Telegram mobil ilovasida ishlaydi; Telegram Desktop/Web'da kamera ruxsati bo'lmasligi mumkin — kodni qo'lda yozing. Kamera ruxsatini telefon sozlamalarida Telegram uchun yoqing |
| Imzo | Bor | Mini App'da barmoq bilan. Telefonni gorizontal tutish qulayroq |
| Jonli joylashuv | Bor | Faqat ish kuni ochiq paytda yoziladi; 8 soatga ulashing. Batareya tejash rejimi Telegram'ni to'xtatsa, joylashuv uziladi |
| Rasm hajmi | 5 MB gacha | Telegram rasmni siqib yuboradi — odatda muammo bo'lmaydi; "fayl" sifatida yubormang |

Qo'shimcha maslahatlar:

- Bot javob bermasa, bir necha soniya kuting — xabarlar navbat bilan ketadi. Tugmani qayta-qayta bosmang.
- Chat tarixini tozalash bot ulanishini buzmaydi; menyu yo'qolsa `/menu` yuboring.
- Mini App sekin ochilsa, uni yopib qayta oching — ma'lumotlar ERP'dan qayta yuklanadi.

## 6.10 Muammolarni hal qilish

| Bot xabari / belgi | Sababi | Nima qilish kerak |
|---|---|---|
| Assalomu alaykum! Botdan foydalanish uchun ... ulanish kodini oling ... | Telegram hisobi ulanmagan yoki uzilgan | Rahbardan yangi kod oling (6.3) |
| ⚠️ Kod noto'g'ri yoki muddati o'tgan. | Kod ishlatilgan, 3 kun o'tgan yoki keyin yangisi berilgan | Yangi kod |
| ⛔ Tizim litsenziyasi faol emas. Kompaniya administratoriga murojaat qiling. | Kompaniya litsenziyasi tugagan yoki to'xtatilgan | Administrator / to'lov |
| ⚠️ ERP bilan aloqa yo'q, birozdan keyin qayta urinib ko'ring. | ERP serveri javob bermayapti | Biroz kutib qayta urining; amal ikki marta bajarilmaydi. Davom etsa — administrator |
| ⚠️ Xatolik yuz berdi, birozdan keyin qayta urinib ko'ring. | Bot ichki xatosi | Qayta urining; takrorlansa ⚠️ Muammo bilan yozing |
| Bu amal sizning rolingiz uchun emas. | Tugma boshqa rolga tegishli yoki ERP'da ruxsat yo'q | Rolni tekshiring (to'g'ri rol bilan qayta ulash) yoki ERP ruxsatini bering |
| Bu bo'lim mijozlar uchun. | Xodim 💳 Qarzim / 📄 Akt sverki ni bosgan | — |
| ⚠️ Siz mijozdan 350 m uzoqdasiz (ruxsat: 200 m). | "Keldim" radiusdan tashqarida | Mijozga yaqinlashing; mijoz koordinatasi noto'g'ri bo'lsa — ofis kartada tuzatadi |
| Avval ish kunini boshlang. | Selfi majburiy, kun boshlanmagan | ▶️ Kunni boshlash |
| Avval 📍 Keldim tugmasi bilan tashrifni boshlang. | Tashrifsiz amal | Kartada 📍 Keldim |
| Rasm juda katta. / Rasmni olib bo'lmadi, qayta yuboring. | Rasm 5 MB dan katta yoki yuklanmadi | Oddiy foto sifatida qayta yuboring |
| Sizga hozircha reys biriktirilmagan. | Ekspeditorga yo'ldagi yoki rejalangan reys yo'q | Ofis reys rejalashi kerak |
| "Keldim" uchun jonli joylashuv kerak: ... | Sozlamada "Keldim — faqat jonli joylashuv" yoqilgan, statik joylashuv yuborilgan | 📎 → Joylashuv → "Jonli joylashuvni ulashish" |
| 🎙 Ovozli buyurtma faqat agentlar uchun. | Ekspeditor ovozli xabar yuborgan | Buyurtmani agent beradi |
| Yetkazishni tasdiqlang: rasm yuboring yoki mijoz imzosini oling. | Isbotsiz yetkazish | Rasm yoki imzo |
| Reys yo'lda emas. | Yo'lga chiqilmagan yoki reys yopilgan | ▶️ Yo'lga chiqish |
| Mini App: "Bu sahifa Telegram ichida ochiladi." | Havola brauzerda ochilgan | Faqat bot tugmasidan oching |
| Mini App: "initData" / Xatolik 401 | Mini App juda uzoq ochiq turgan (sessiya eskirgan) | Oynani yopib, bot tugmasidan qayta oching |
| Mini App: "Bu amal sizning rolingiz uchun emas." | Masalan omborchi yoki rahbar buyurtma oynasini, yoki agent yetkazish oynasini ochgan | Rolga mos tugmadan foydalaning |
| Selfi/foto ERP'da ko'rinmaydi | Sozlamalarda "Bot ochiq manzili" bo'sh | 6.2-bo'lim |
| Mijozga xabar bormayapti | Mijoz botga ulanmagan yoki botni bloklagan | Mijozni qayta ulang |
