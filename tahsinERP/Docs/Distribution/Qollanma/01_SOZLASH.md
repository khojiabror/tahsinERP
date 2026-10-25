# 1. Boshlang'ich sozlash

Bu bob yangi kompaniya Distributsiya modulini ishga tushirishda nimani, qaysi tartibda sozlashini ko'rsatadi. Bo'limlar shu tartibda joylashgan — yuqoridan pastga bajarsangiz, keyingi qadamga kerakli ma'lumot oldin kiritilgan bo'ladi.

Bobni administrator va rahbar o'qiydi. Operator uchun 1.9 (mijozlar) va 1.11 (import) muhim.

Barcha misollar **DEMO Qurilish Savdo MChJ** ssenariysida (0-bob, 0.8).

## 1.1 Ishga tushirish tartibi

| # | Qadam | Qayerda | Bo'lim |
|---|---|---|---|
| 1 | Litsenziya faol (KORP, TRIAL yoki DIST paketi) | Admin panel → Litsenziyalar | 0.5 |
| 2 | ERP foydalanuvchilari va rollar, Distributsiya huquqlari | Admin panel → Foydalanuvchilar, Rollar | 1.10 |
| 3 | Omborlar, o'lchov birliklari, tovar kategoriyalari, kassa va bank hisoblari | Umumiy ma'lumotnomalar, Moliya | — |
| 4 | Distributsiya sozlamalari | Distributsiya → Sozlamalar | 1.2 |
| 5 | Hududlar | Distributsiya → Hududlar (yoki import) | 1.3 |
| 6 | Ma'lumotnomalar: narx turlari, brendlar, sabablar | Distributsiya → Ma'lumotnomalar | 1.4 |
| 7 | Tovarlar, birliklar, shtrix-kodlar | Mahsulotlar + Distributsiya → Tovarlar (yoki import) | 1.5 |
| 8 | Narxlar | Distributsiya → Narxlar (yoki import) | 1.6 |
| 9 | Jamoalar va agentlar | Distributsiya → Agentlar va jamoalar | 1.7 |
| 10 | Ko'rinish doirasi va botga ulash | Agentlar va jamoalar | 1.8 |
| 11 | Mijozlar | Distributsiya → Mijozlar (yoki import) | 1.9 |
| 12 | Kirish qarzlari | Distributsiya → Excel import | 1.11 |
| 13 | Boshlang'ich qoldiq | Distributsiya → Inventarizatsiya (boshlang'ich sanoq) | 4.3 |
| 14 | Aksiyalar (kerak bo'lsa) | Distributsiya → Aksiyalar | 2.17 |
| 15 | Sinov buyurtmasi → bekor qilish | Distributsiya → Buyurtmalar | 2-bob |

> **Muhim:** Distributsiya omborlar, o'lchov birliklari, tovar kategoriyalari va kassa/bank hisoblarini o'zi yaratmaydi — ular tahsinERP'ning umumiy qismida bo'lishi kerak. Masalan, "poddon" yoki "quti" birligi umumiy O'lchov birliklari ro'yxatida bo'lmasa, uni tovarga qo'shimcha birlik qilib bo'lmaydi.

## 1.2 Sozlamalar

**Kim:** Admin, Rahbar   **Menyu:** Distributsiya → Sozlamalar

Sahifada beshta blok bor: Umumiy sozlamalar, Agent to'lovlari, Chegirma limitlari, Omborlar, Minimal qoldiq. Har blok o'zining **Saqlash** tugmasi bilan saqlanadi. O'zgartirish huquqi bo'lmasa maydonlar faqat ko'rish uchun.

### 1.2.1 Umumiy sozlamalar

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Minimal buyurtma, so'm | Yetkazib beriladigan buyurtmaning eng kichik summasi. Undan kichik buyurtma tasdiqlanmaydi. "O'zi olib ketadi" buyurtmasiga qo'llanmaydi. 0 — cheklov yo'q | 500 000 |
| Yirik buyurtma, so'm | Shu summadan katta yoki teng yangi buyurtma haqida rahbarlarga Telegram xabari. 0 — xabar yuborilmaydi | 30 000 000 |
| Tashrif radiusi, m | Agent "📍 Keldim" bosganda mijozdan shu masofada bo'lishi kerak. 20 dan 5 000 gacha | 200 |
| Agent naqdi topshiriladigan kassa | Agent mijozdan olgan naqd pulni kassaga topshirganda qaysi kassaga kirim bo'ladi (Moliya'dagi faol kassa yoki bank hisobi) | DEMO Asosiy kassa |
| Telegram bot nomi | Kompaniya botining username'i (@ belgisiz). Ulanish havolasi shu nom bilan tuziladi | demo_qurilish_bot |
| Bot ochiq manzili (rasmlar uchun) | Bot serverining https manzili. To'ldirilsa ERP'da selfi, tashrif fotosi va nuqta fotolari ko'rinadi | https://bot.demo.uz |
| Kun boshida selfi majburiy | Belgilansa agent selfisiz ish kunini boshlay olmaydi. Belgilanmasa selfini o'tkazib yuborish mumkin | belgilanmagan |
| Keldim — faqat jonli joylashuv | Belgilansa agent "📍 Keldim" uchun faqat jonli joylashuv yuboradi, statik joylashuv rad etiladi. Belgilanmasa statik joylashuv ham qabul qilinadi va tashriflar hisobotida belgilanadi. Standart — o'chiq, chunki botning "joylashuvni yuborish" tugmasi faqat statik joylashuv yuboradi | belgilanmagan |
| Brak ombori | Belgilansa "brak" qaytarish faqat shu omborga qilinadi (qaytarish formasida brak belgilanganda ombor o'zi tanlanadi, boshqa ombor tanlansa xato), bu ombor buyurtmalarga taklif qilinmaydi. Bo'sh — cheklov yo'q | — |

> **Eslatma:** Bot nomi kiritilmasa ulanish kodi baribir beriladi, lekin havola chiqmaydi — xodim kodni botga `/start KOD` ko'rinishida qo'lda yuboradi.

### 1.2.2 Agent to'lovlari

Bu blok agent botda qaysi usulda to'lov qabul qila olishini belgilaydi.

| Usul | Agent qabul qiladi | Hisob |
|---|---|---|
| Naqd | belgi | Hisob tanlanmaydi — "agent qo'lida → kassaga topshiradi" |
| Karta | belgi | Kassa yoki bank hisobi — majburiy |
| Click | belgi | Hisob — majburiy |
| Payme | belgi | Hisob — majburiy |
| Uzum | belgi | Hisob — majburiy |
| Pul o'tkazmasi | belgi | Hisob — majburiy |

Qanday ishlaydi:

- **Naqd** pul agent qo'lida qoladi va kechqurun kassaga topshiriladi (kassir sanab qabul qiladi).
- **Naqdsiz** usullar (karta, Click va boshqalar) darhol tanlangan hisobga tushadi va Moliya'da tranzaksiya bo'lib ko'rinadi.
- Botda faqat belgilangan usullar tugma bo'lib chiqadi.
- Hech narsa saqlanmagan bo'lsa odatiy holat: naqd, karta, Click. Lekin karta va Click uchun hisob tanlanmaguncha agent ular bilan to'lov qila olmaydi.

Usul belgilanib hisob tanlanmasa, saqlashda "To'lov usuli uchun hisob tanlanmagan (Sozlamalar → Agent to'lovlari). (Karta)" xatosi chiqadi.

DEMO misoli: Naqd ✓, Karta ✓ → "DEMO Kapitalbank", Click ✓ → "DEMO Click hisobi". Payme, Uzum, o'tkazma — belgilanmagan (ular ofisda kassir orqali qabul qilinadi).

### 1.2.3 Chegirma limitlari

Har ERP roli qatorida **Max %** maydoni bor — shu rol foydalanuvchisi buyurtmaga bera oladigan eng katta chegirma.

| Rol | Max % | Ma'nosi |
|---|---|---|
| Agent | 3 | Agent qator va buyurtma chegirmasini birgalikda 3% gacha beradi |
| Operator | 5 | |
| Rahbar | 10 | |
| Kassir | (bo'sh) | Chegirma bera olmaydi |

Qoidalar:

- Maydon **bo'sh** — rol chegirma bera olmaydi. 0 yozish ham amalda shu.
- Foydalanuvchida bir nechta rol bo'lsa — eng katta limit olinadi.
- Qator va buyurtma chegirmasi ketma-ket qo'shiladi: qatorga 2% va buyurtmaga 1% — samarali 2,98%.
- "Cheksiz chegirma" huquqi (1.10) bor xodim limitdan va minimal narxdan ozod.
- Aksiya chegirmasi limitga kirmaydi (2.17).

### 1.2.4 Omborlar

Har ombor qatorida:

| Ustun | Ma'nosi |
|---|---|
| Savdoda | Belgilansa — buyurtmalar shu ombordan jo'natiladi; buyurtma formasida va ombor taklifida faqat savdo omborlari qatnashadi |
| Minimal qoldiq | Shu omborda nechta tovarga minimal qoldiq belgilangan; bosilsa pastda ro'yxat ochiladi |

> **Muhim:** Agar birorta ombor ham sozlanmagan bo'lsa (sahifada hech qaysi qatorda saqlash bosilmagan), **barcha omborlar savdo ombori** hisoblanadi. Bitta omborni saqlashingiz bilan faqat "Savdoda" belgilanganlari qoladi. Shuning uchun hamma savdo omborlarini birdaniga belgilab chiqing.

DEMO misoli: DEMO Asosiy ombor — Savdoda ✓; DEMO Filial ombor — Savdoda ✓.

### 1.2.5 Minimal qoldiq

**Omborlar** jadvalida ombor qatoridagi Minimal qoldiq sonini bosing — pastda shu ombor ro'yxati ochiladi.

1. Tovarni qidiring (nom yoki artikul).
2. **Minimal qoldiq** (asosiy birlikda) va ixtiyoriy **Maksimal (ixtiyoriy)** kiriting.
3. **+** tugmasini bosing.

| Maydon | Ma'nosi | DEMO misoli (Asosiy ombor) |
|---|---|---|
| Minimal qoldiq | Mavjud qoldiq shundan pastga tushganda rahbar va omborchilarga xabar, tovar "Buyurtma taklifi"ga tushadi | Sement — 200 qop |
| Maksimal | Buyurtma taklifi shu darajagacha to'ldiradi; bo'sh — minimal × 2 | Sement — 1 000 qop |

Maksimal minimaldan kam bo'lsa "Maksimal minimaldan kam bo'lmasin." xatosi chiqadi. Minimalni 0 qilsangiz yoki savat belgisini bossangiz chegara olib tashlanadi.

### 1.2.6 Yetkazish sozlamalari

Yetkazishga oid sozlamalar **Distributsiya → Mashinalar** sahifasidagi **Yetkazish sozlamalari** blokida:

| Maydon | Ma'nosi |
|---|---|
| Yandex Maps JavaScript API kaliti | Reys va tashrif rejasi sahifalarida xarita uchun. Bo'sh bo'lsa xarita o'rniga Yandex havolalari ko'rsatiladi |
| Botda ovozli buyurtma | Yoqilsa agent ovozli xabar bilan buyurtma qoralamasi yuboradi (bot serverida nutq kaliti ham sozlangan bo'lishi kerak) |
| Omborlar koordinatasi (marshrut boshi) | Har ombor uchun `kenglik,uzunlik`, masalan `41.311081,69.240562`. Reys tartibini hisoblash shu nuqtadan boshlanadi |

Batafsil — 7-bob, 7.4.

## 1.3 Hududlar

**Kim:** Admin, Rahbar, Operator   **Menyu:** Distributsiya → Hududlar

Hudud uch darajali daraxt: **Viloyat → Tuman → Hudud**. Mijozlar hududga biriktiriladi. Savdo hisobotlarida yuqori darajani tanlasangiz ichidagi hududlar ham kiradi. Ko'rinish doirasida ham hudud ichidagi quyi hududlar bilan birga olinadi (1.8.1).

1. **Viloyat qo'shish** tugmasini bosing, **Nomi** va **Nomi (rus)** ni yozing, saqlang.
2. Viloyat qatorida **Ichiga qo'shish** — tuman qo'shiladi.
3. Tuman qatorida **Ichiga qo'shish** — hudud qo'shiladi.

DEMO misoli:

```
Toshkent sh.            (Viloyat)
 ├─ Chilonzor           (Tuman)
 └─ Yunusobod           (Tuman)
```

Qoidalar va xatolar:

| Holat | Xabar |
|---|---|
| Hudud ostiga to'rtinchi daraja | "Hudud ostida yangi daraja ochib bo'lmaydi (maksimum 3)." |
| Hududni o'z ichidagi hudud ostiga ko'chirish | "Hududni o'z ichidagi hudud ostiga ko'chirib bo'lmaydi." |
| Ichida hududlar bor hududning darajasini o'zgartirish | "Ichida hududlar bor — darajasini o'zgartirib bo'lmaydi." |
| Ichida hudud yoki mijoz bor hududni o'chirish | "Yozuv ishlatilgan — o'chirib bo'lmaydi, nofaol qiling." |

Ishlatilgan hududni o'chirish o'rniga **Faol / nofaol** bilan nofaol qiling. Ko'p hududni Excel'dan yuklash mumkin (1.11).

## 1.4 Ma'lumotnomalar

**Kim:** Admin, Rahbar   **Menyu:** Distributsiya → Ma'lumotnomalar

Sahifada beshta yorliq bor. Har yozuvda **Nomi**, **Nomi (rus)**, **Tartib** va **Faol / nofaol** bor; ishlatilgan yozuv o'chirilmaydi — nofaol qilinadi.

| Yorliq | Nima | Qo'shimcha maydon |
|---|---|---|
| Brendlar | Tovar brendi (masalan "Qizilqumsement") | — |
| Ishlab chiqaruvchilar | Zavod | Davlat |
| Subkategoriyalar | Tovar kategoriyasi ichidagi guruh | Kategoriya (majburiy — umumiy tovar kategoriyalaridan) |
| Narx turlari | Chakana, Ulgurji, Diler ... | **Standart qilish** tugmasi |
| Sabablar | Qaytarish, buyurtmasiz tashrif, bekor qilish, hisobdan chiqarish sabablari | Turi |

### Narx turlari

Yangi bazada to'rtta narx turi tayyor turadi: Chakana, Ulgurji (standart), Yirik ulgurji, Diler. Ularni o'z siyosatingizga moslang.

> **Eslatma:** Standart narx turi ataylab "Ulgurji" — ulgurji distributor uchun narxi topilmagan mijozga ulgurji narx chiqadi. Boshqasi kerak bo'lsa, shu yerda **Standart qilish** bilan o'zgartiring; pilot import faylidagi "Standart" ustuni ham uni o'zi belgilaydi.

DEMO misoli:

1. **Chakana** qatorida **Standart qilish** ni bosing — endi standart narx turi Chakana.
2. "Yirik ulgurji" kerak emas — **Faol / nofaol** bilan nofaol qiling.

Qoidalar:

- Standart narx turi bittagina va u faol bo'lishi shart. Standartni nofaol qilmoqchi bo'lsangiz: "Standart narx turi faol bo'lishi kerak."
- Standart narx turi ikki holatda ishlaydi: mijozga narx turi berilmagan bo'lsa va mijoz narx turida tovarning narxi yo'q bo'lsa (0-bob, 0.4).

### Sabablar

Sabab turlari: **Qaytarish**, **Buyurtmasiz tashrif**, **Bekor qilish**, **Hisobdan chiqarish**. Yangi bazada tayyor ro'yxat bor:

| Turi | Tayyor sabablar |
|---|---|
| Buyurtmasiz tashrif | Do'kon yopiq; Pul yo'q; Tovar bor; Egasi yo'q |
| Qaytarish | Brak; Muddati o'tgan; Noto'g'ri tovar; Mijoz rad etdi |
| Bekor qilish | Mijoz bekor qildi; Qoldiq yetmadi; Xato kiritilgan |
| Hisobdan chiqarish | Sinish; Buzilish; Kamomad |

Agent botda "🚫 Buyurtmasiz" bosganda **Buyurtmasiz tashrif** sabablaridan birini tanlashi majburiy. Ekspeditor qisman yetkazish va radda **Qaytarish** sabablaridan tanlaydi.

## 1.5 Tovarlar

**Kim:** Admin, Rahbar, Operator   **Menyu:** Distributsiya → Tovarlar

Tovarning o'zi (artikul, nom, asosiy birlik, kategoriya) — tahsinERP'ning umumiy **Mahsulotlar** ma'lumotnomasida. Distributsiya unga **distributsiya kartasi**ni qo'shadi: rus nomi, brend, birliklar, shtrix-kodlar, sotuvdaligi.

Tovarni ikki yo'l bilan kiritasiz:

- bittalab: **Yangi tovar** tugmasi umumiy mahsulot formasini ochadi; saqlagach Distributsiya → Tovarlar'da qatorning tahrirlash tugmasi bilan distributsiya kartasini to'ldirasiz;
- ko'plab: **Excel import → Tovarlar** (1.11) — tovar, distributsiya kartasi, 2 tagacha qo'shimcha birlik va shtrix-kodlar bir yo'la.

Ro'yxatda kategoriya, brend va sotuvdaligi bo'yicha filtr, nom/artikul/shtrix-kod bo'yicha qidiruv bor. Distributsiya kartasi to'ldirilmagan tovarda "profil yo'q" belgisi turadi.

### 1.5.1 Distributsiya kartasi

| Maydon | Nima kiritiladi | DEMO misoli (Sement) |
|---|---|---|
| Nomi (rus) | Ruscha nom (ruscha interfeys, Mini App va hujjatlar uchun) | Цемент М400 |
| Brend | Ma'lumotnomadan | Qizilqumsement |
| Ishlab chiqaruvchi | Ma'lumotnomadan | — |
| Subkategoriya | Ma'lumotnomadan | Quruq aralashmalar |
| Og'irlik, kg (asosiy birlik) | Bitta asosiy birlik og'irligi — buyurtma va reys og'irligi shundan | 50 |
| Hajm, m³ (asosiy birlik) | Bitta asosiy birlik hajmi | 0,035 |
| Sotuvda | Belgi olib tashlansa tovar buyurtmaga qo'shilmaydi ("… sotuvdan olingan.") | ✓ |

Kartada narx turlari bo'yicha joriy narxlar ham ko'rinadi ("Narxlar 1 qop uchun.") — ular Narxlar sahifasida o'zgartiriladi (1.6).

### 1.5.2 O'lchov birliklari

> Agent qulay birlikda (quti, m², qop) sotadi; ombor va narx asosiy birlikda hisoblanadi.

1. Kartadagi **O'lchov birliklari** blokida **Qo'shish** ni bosing.
2. Maydonlarni to'ldiring va saqlang.

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Birlik | Umumiy birliklar ro'yxatidan | poddon |
| Nechta qop (asosiy birlik) | Koeffitsient | 40 |
| Og'irlik, kg | Bo'sh — avtomatik (asosiy og'irlik × koeffitsient) | (bo'sh → 2 000) |
| Hajm, m³ | Bo'sh — avtomatik | (bo'sh) |
| Sotishda odatiy birlik | Belgilansa Mini App va formada shu birlik birinchi taklif qilinadi | — |

DEMO tovarlari:

| Tovar | Asosiy birlik | Qo'shimcha birlik | Koeffitsient |
|---|---|---|---|
| Sement M400 | qop | poddon | 40 |
| Kafel 30×30 | m² | quti | 1,44 |
| Gipsokarton 12,5 mm | list | — | — |

Xatolar:

| Holat | Xabar |
|---|---|
| Asosiy birlikni qo'shimcha qilib qo'shish | "Bu tovarning asosiy birligi — qo'shimcha birlik sifatida qo'shilmaydi." |
| Bir birlikni ikki marta | "Bu birlik allaqachon qo'shilgan." |
| Koeffitsient 0 yoki manfiy | "Koeffitsient 0 dan katta bo'lishi kerak." |

Ishlatilgan birlik o'chirilmaydi — nofaol qilinadi (eski buyurtmalarda iz qoladi).

> **Muhim:** Tovarning asosiy birligini keyin o'zgartirmang — qoldiq, narx va minimal qoldiq shu birlikda yozilgan. Import ham mavjud tovarning asosiy birligini o'zgartirmaydi: "Mavjud tovarning asosiy birligini import orqali o'zgartirib bo'lmaydi."

### 1.5.3 Shtrix-kodlar

Bitta tovarga bir nechta shtrix-kod (har birligiga alohida ham) beriladi. Shtrix-kod buyurtma formasidagi skaner maydonida, Mini App kamerasida va ombor sanog'ida ishlatiladi. Qutidagi shtrix-kod skanerlansa miqdorga quti koeffitsienti bilan qo'shiladi.

1. **Shtrix-kodlar** blokida kodni yozing (yoki skaner bilan o'qing).
2. Kerak bo'lsa birlikni tanlang (bo'sh — asosiy birlik).
3. **Qo'shish**.

| Holat | Xabar |
|---|---|
| Bo'sh | "Shtrix-kodni kiriting." |
| 4 dan kam yoki 50 dan ortiq belgi | "Shtrix-kod 4–50 ta harf/raqamdan iborat bo'lishi kerak." |
| EAN/UPC nazorat raqami noto'g'ri | "Shtrix-kod nazorat raqami noto'g'ri (EAN/UPC)." |
| Boshqa tovarda bor | "Bu shtrix-kod boshqa tovarga biriktirilgan (ID …)." |

## 1.6 Narxlar

**Kim:** Rahbar (narx siyosati)   **Menyu:** Distributsiya → Narxlar

Sahifa — tovar × narx turi jadvali. Har katakda narx va (bo'lsa) minimal narx. "Narxlar asosiy birlik uchun, so'mda."

### 1.6.1 Narx kiritish

1. Kategoriya bo'yicha filtrlang yoki tovarni qidiring.
2. Kerakli katakni bosing — oyna ochiladi.
3. **Narx** va **Minimal narx** ni yozing, saqlang.

DEMO misoli (asosiy birlik narxlari):

| Tovar | Chakana | Ulgurji | Diler | Minimal (har turda) |
|---|---|---|---|---|
| Sement M400 (qop) | 62 000 | 58 000 | 55 000 | 52 000 |
| Kafel 30×30 (m²) | 95 000 | 88 000 | — | — |
| Gipsokarton (list) | 68 000 | — | — | — |

> **Muhim:** Minimal narx avval mijoz narx turi qatoridan olinadi. U bo'sh bo'lsa — standart narx turidagi minimum ishlaydi, lekin mijozning o'z narxidan yuqori emas. Shuning uchun umumiy minimumni standart narx turi katagiga yozish yetarli; narx turi uchun boshqa minimum kerak bo'lsa — o'sha katakka yozing.

Qoidalar:

- Narx manfiy bo'lmaydi: "Narx manfiy bo'lmasin."
- Minimal narx 0 va narx oralig'ida: "Minimal narx 0 va narx oralig'ida bo'lishi kerak."
- Narx o'zgarmagan bo'lsa tarixga yozilmaydi. O'zgargan har narx sana, vaqt va foydalanuvchi bilan **Narx tarixi**ga tushadi (tovar qatoridagi tarix belgisi).
- Yangi narx darhol amal qiladi: yangi saqlanadigan buyurtmalar yangi narxda. Oldin saqlangan buyurtma narxi o'zgarmaydi, toki u tahrirlanib qayta saqlanmaguncha.
- **Standart narxi yo'q** filtri — standart narx turida narxi yo'q tovarlarni ko'rsatadi. Bunday tovar narx turi bo'yicha ham narxi bo'lmagan mijozga sotilmaydi.

### 1.6.2 Ommaviy o'zgartirish

Narx turidagi narxlarni foiz bilan birdaniga oshirish yoki tushirish.

1. **Ommaviy o'zgartirish** tugmasini bosing.
2. **Narx turi**, kerak bo'lsa **Kategoriya**, foiz (oshirish — musbat, tushirish — manfiy) va **Yaxlitlash** (so'mgacha) ni kiriting.
3. **Qo'llash**.

"Tanlangan narx turida joriy narxi bor tovarlarga qo'llanadi; minimal narx ham shu foizda o'zgaradi." Hammasi bitta amalda yoziladi, natijada nechta narx o'zgargani chiqadi.

DEMO misoli: Ulgurji, +5%, yaxlitlash 1 000.

| Tovar | Eski | Hisob | Yangi |
|---|---|---|---|
| Sement (qop) | 58 000 | 58 000 × 1,05 = 60 900 | 61 000 |
| Kafel (m²) | 88 000 | 88 000 × 1,05 = 92 400 | 92 000 |

Agar ulgurji sement uchun minimal 52 000 bo'lsa, u ham 52 000 × 1,05 = 54 600 → 55 000 bo'ladi. Foiz 0 bo'lsa yoki −100% va undan past bo'lsa: "Foiz noto'g'ri."

### 1.6.3 Maxsus narx

Bitta mijozga alohida kelishilgan narx.

**Menyu:** Distributsiya → Mijozlar → mijoz kartasi → **Maxsus narxlar**

1. Tovarni tanlang, **Narx** (asosiy birlikda), **Boshlanish** va ixtiyoriy **Tugash** sanasini kiriting.
2. Saqlang.

| Qoidasi | Izoh |
|---|---|
| Ustunlik | Maxsus narx narx turidan ustun turadi |
| Minimal narx | Maxsus narxga ham qo'llanadi (mijoz narx turidagi, u bo'sh bo'lsa standart narx turidagi minimum). Ozod — faqat "Cheksiz chegirma" huquqi |
| Muddat | Faqat Boshlanish–Tugash oralig'ida amal qiladi; Tugash bo'sh — muddatsiz |
| To'xtatish | **To'xtatish**: kecha yoki undan oldin boshlangan narx kechagi sana bilan yopiladi va tarixda qoladi; bugun yoki keyin boshlangani o'chiriladi (audit jurnalida qoladi) |
| Sana xatosi | "Tugash sanasi boshlanishdan oldin bo'lmasin." |

DEMO misoli: DEMO Bunyodkor qurilish uchun Gipsokarton — 64 000 so'm/list, 01.11.2026 dan 31.12.2026 gacha. Gipsokartonning Diler narxi yo'q, shuning uchun maxsus narxsiz Bunyodkorga standart Chakana narxi (68 000) chiqardi; endi bu muddatda 64 000 dan chiqadi. Sement esa avvalgidek Diler narxida (55 000) qoladi.

## 1.7 Jamoalar va agentlar

**Kim:** Admin, Rahbar   **Menyu:** Distributsiya → Agentlar va jamoalar

> Agent — ERP foydalanuvchisi; u faqat o'ziga biriktirilgan mijozlarni ko'radi.

Shuning uchun avval Admin panelda har agent va ekspeditorga ERP foydalanuvchisi (login) yaratiladi, keyin u shu sahifada agent yoki ekspeditor qilib qo'shiladi.

### 1.7.1 Jamoa

1. **Jamoalar** blokida **Qo'shish**.
2. **Nomi**, **Supervayzer** (ERP foydalanuvchisi), **Faol**.
3. **Saqlash**.

"Supervayzer o'z jamoasi agentlari va ularning mijozlarini ko'radi." Supervayzer bir nechta jamoaga ega bo'lishi mumkin — hammasining agentlarini ko'radi.

DEMO misoli: jamoa "Toshkent savdo", supervayzer — demo_rahbar.

### 1.7.2 Agent va ekspeditor

1. **Agentlar** blokida **Qo'shish**.
2. Maydonlarni to'ldiring, **Saqlash**.

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Foydalanuvchi | ERP foydalanuvchisi | Jasur (demo_agent) |
| Jamoa | Qaysi jamoada | Toshkent savdo |
| Ombor | Agent botdan bergan buyurtmalar shu ombordan bo'ladi (bo'sh — tasdiqlashda avtomatik tanlanadi) | DEMO Asosiy ombor |
| Telefon | +998 formatida | +998 90 123 45 67 |
| Ekspeditor | Belgilansa — ekspeditor (reys oladi, mijoz biriktirilmaydi) | Jasur — yo'q; Sherzod — ✓ |
| Faol | Nofaol agent mijozlarga biriktirilmaydi | ✓ |

Jadvalda har agentga biriktirilgan mijozlar soni ko'rinadi.

> **Eslatma:** Agentni nofaol qilish uning mijozlarini boshqa agentga o'tkazmaydi. Mijozlarni avval boshqa agentga o'tkazing (mijoz kartasida yoki **Excel import → Marshrutlar** orqali).

## 1.8 Ko'rinish doirasi va Telegram'ga ulash

### 1.8.1 Ko'rinish doirasi (scope)

**Kim:** Admin   **Menyu:** Distributsiya → Agentlar va jamoalar → foydalanuvchi qatoridagi **Ko'rinish doirasi** yoki **Xodimning ko'rinish doirasi** kartasi (istalgan xodimni — agent bo'lmasa ham, masalan omborchi yoki operator — tanlab, uning cheklov sahifasiga o'tish)

Bu sahifada foydalanuvchiga uchta ro'yxat beriladi: **Omborlar**, **Hududlar**, **Mijozlar**.

> Ro'yxat bo'sh bo'lsa — cheklov yo'q. To'ldirilsa — foydalanuvchi faqat tanlanganlarni ko'radi. Admin har doim hammasini ko'radi.

1. Kerakli ro'yxatda omborlar/hududlar/mijozlarni tanlang.
2. Shu ro'yxatning **Saqlash** tugmasini bosing (har ro'yxat alohida saqlanadi).

DEMO misollari:

| Foydalanuvchi | Doira | Natija |
|---|---|---|
| demo_ombor (omborchi) | Omborlar: DEMO Asosiy ombor | Faqat Asosiy ombor buyurtmalari, qoldiqlari; yig'ish xabari faqat shu ombordan |
| Chilonzor operatori | Hududlar: Toshkent sh. / Chilonzor | Faqat Chilonzor (va uning ichidagi hududlar) mijozlari va ularning buyurtmalari |
| demo_kassir | (bo'sh) | Cheklovsiz |

> **Eslatma:** Hudud doirasi tanlangan hududni ichidagi quyi hududlari bilan qamraydi: viloyat tanlansa — uning barcha tumanlari va hududlari kiradi. Buyurtmalar ro'yxati, buyurtma kartasi va chop etish ham mijoz hududi bo'yicha cheklanadi.

> **Eslatma:** Agent va supervayzer uchun mijoz cheklovi alohida sozlanmaydi — u agent ierarxiyasidan avtomatik (0-bob, 0.4); supervayzer agenti biriktirilmagan mijozlarni ham ko'radi. Ko'rinish doirasi bunga qo'shimcha qo'llanadi. Ombor doirasi bor xodim ombori hali tanlanmagan buyurtmalarni ham ko'radi.

### 1.8.2 Xodimni botga ulash

**Kim:** Admin, Rahbar   **Menyu:** Distributsiya → Agentlar va jamoalar → **Xodimni botga ulash**

1. Foydalanuvchini va botdagi rolini tanlang: **Rahbar**, **Omborchi**, **Agent** yoki **Ekspeditor**.
2. **Kod olish** ni bosing.
3. Oynada kod, havola (`https://t.me/<bot>?start=<kod>`) va amal qilish muddati chiqadi.
4. Havolani xodimga yuboring. Xodim uni Telegram'da ochadi (yoki botga `/start KOD` yozadi) — bot uning ismi va rolini yozib, menyuni chiqaradi.

Agentlar jadvalidagi Telegram belgisi shu amalni agent (yoki ekspeditor) roli bilan darhol bajaradi.

Qoidalar:

- Kod **bir martalik**, **3 kun** amal qiladi. Muddati o'tgan yoki ishlatilgan kodga bot "Kod noto'g'ri yoki muddati o'tgan." deydi.
- Xodim boshqa telefonga o'tsa — yangi kod oling; qayta ulanganda eski Telegram hisobi uziladi.
- Rahbar roli — limit oshishi va yirik buyurtma xabarlari va **✅ Tasdiqlash** tugmasi uchun. Omborchi roli — yig'ish xabarlari va **📦 Ombor** Mini App uchun.

Mijozni botga ulash — mijoz kartasida (1.9.4).

## 1.9 Mijozlar

**Kim:** Operator, Rahbar   **Menyu:** Distributsiya → Mijozlar

Ro'yxat sahifalanadi; filtrlar: hudud, agent, toifa (A/B/C), tasdiq holati ("Tasdiq kutilmoqda"). Sarlavhada tasdiq kutayotgan nuqtalar soni ko'rinadi.

### 1.9.1 Yangi mijoz

1. **Yangi mijoz** tugmasini bosing.
2. Uch blokni to'ldiring va **Saqlash**.

**Asosiy ma'lumot**

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Nomi * | Do'kon yoki tashkilot nomi (majburiy) | DEMO Anvar do'koni |
| STIR | 9 raqam (yuridik) yoki JShShIR 14 raqam; takrorlanmaydi | 301234567 |
| Nuqta turi | Xo'jalik do'koni, Qurilish bozori, Qurilish brigadasi, Prorab, Xususiy mijoz | Xo'jalik do'koni |
| Egasi | | Anvar Karimov |
| Aloqa shaxsi | | — |
| Telefon, Telefon 2 | +998 formatida | +998 90 111 22 33 |
| Shahar, Manzil | | Toshkent, Chilonzor 9-kvartal |
| Mo'ljal | Topish uchun belgi | Bozor yonida |
| Kenglik, Uzunlik | GPS koordinata (Yandex xaritadan nusxa); ikkalasi birga | 41.2856, 69.2034 |

Kenglik va uzunlik yonidagi xarita tugmasi koordinata yozilishi bilan (saqlashdan oldin ham) faollashadi va joyni Yandex xaritada ochadi — tekshiring. Yandex'dan `41.2856, 69.2034` ko'rinishida nusxalangan qatorni Kenglik maydoniga qo'ysangiz, ikkala maydonga o'zi bo'linadi.

**Savdo shartlari**

| Maydon | Nima kiritiladi | DEMO misoli |
|---|---|---|
| Hudud | Daraxtdan | Toshkent sh. / Chilonzor |
| Kategoriya | A, B yoki C (mijoz ahamiyati) | B |
| Agent | Faol agent | Jasur |
| Narx turi | Bo'sh — "Standart narx turi" | Chakana |
| Tashrif kunlari | Du, Se, Ch, Pa, Ju, Sh, Ya — agent marshruti shu kunlardan | Du, Pa |
| Kredit limiti (so'm) | Qarz + buyurtma chegarasi | 20 000 000 |
| To'lov muddati (kun) | 0–365 | 7 |

> Qarz + tasdiqlangan, lekin hali jo'natilmagan buyurtmalar + yangi buyurtma limitdan oshsa yoki muddati o'tgan qarz bo'lsa, buyurtma tasdiqlanmaydi (rahbar ruxsatisiz). 0 — faqat oldindan to'lov.

DEMO Bunyodkor qurilish: hudud Toshkent sh. / Yunusobod, narx turi Diler, limit 50 000 000, muddat 14 kun.

Xatolar:

| Holat | Xabar |
|---|---|
| STIR formati | "STIR 9 raqam (yuridik) yoki JShShIR 14 raqam bo'lishi kerak." |
| STIR boshqa mijozda | "Bu STIR boshqa mijozda bor." |
| Telefon | "Telefon raqami noto'g'ri." |
| Faqat kenglik yoki faqat uzunlik | "Kenglik va uzunlik birga kiritiladi." |
| Limit manfiy yoki muddat 365 dan ko'p | "Kredit limiti 0 dan, to'lov muddati 0–365 kun." |
| Nofaol agent | "Agent topilmadi yoki faol emas." |

Saqlangan kartada qo'shimcha tugmalar: **Maxsus narxlar** (1.6.3), **Akt sverki**, **Telegram** (1.9.4).

### 1.9.2 Agent qo'shgan nuqtani tasdiqlash

Agent botda **➕ Yangi nuqta** orqali (nom, telefon, joylashuv, mo'ljal, foto) yangi do'kon qo'shadi. Bunday nuqta **tasdiqlanmagan** holda yaratiladi va rahbarlarga "🆕 Yangi savdo nuqtasi …" xabari keladi.

1. **Mijozlar** ro'yxatida filtrni "Tasdiq kutilmoqda" ga qo'ying.
2. Kartani oching — tepada "Bu nuqtani agent qo'shgan — ma'lumotni tekshirib, ro'yxatda tasdiqlang." va nuqta fotosi.
3. Narx turi, limit, muddat, hududni to'ldiring, **Saqlash**.
4. Ro'yxatda **Tasdiqlash** ni bosing.

> **Muhim:** Tasdiqlanmagan mijozga buyurtma yozish mumkin, lekin u **tasdiqlanmaydi**: "Mijoz hali tasdiqlanmagan — avval mijoz kartasini tasdiqlang." Web'da qo'lda yaratilgan mijoz darhol tasdiqlangan bo'ladi.

### 1.9.3 Mavjud mijozni qo'shish

Sotuvlar (CRM) modulidagi mijoz Distributsiyada ham savdo qilishi kerak bo'lsa, uni qaytadan yaratmang:

1. **Mavjud mijozni qo'shish** ni bosing.
2. CRM mijozini qidirib tanlang.
3. Kartani oching va savdo shartlarini to'ldiring.

Mijoz yozuvi bitta qoladi, unga distributsiya kartasi qo'shiladi. Distributsiya kartasi yo'q mijozga buyurtma yozib bo'lmaydi: "Mijoz distributsiyaga qo'shilmagan."

### 1.9.4 Mijozni Telegram'ga ulash

1. Mijoz kartasida **Telegram** tugmasini bosing.
2. Kod va havola chiqadi (bir martalik, 3 kun).
3. Havolani mijozga yuboring.

Ulangan mijoz botda buyurtmasi tasdiqlangani, jo'natilgani, yo'lga chiqqani, yetkazilgani (nakladnoy PDF bilan) va to'lovi qabul qilingani haqida xabar oladi, **💳 Qarzim** va **📄 Akt sverki** tugmalaridan foydalanadi. Xabarlar mijozning Telegram tilida keladi.

## 1.10 Rollar va huquqlar

**Kim:** Admin   **Menyu:** Admin panel → Rollar → (rol qatori) → Huquqlar

Distributsiyaning har sahifasi alohida ruxsat moduli. Qidiruvga `Dst` yozsangiz hammasi chiqadi.

- **View** — sahifani ochish va ko'rish (menyuda ko'rinadi).
- **Change** — yaratish, tahrirlash, tasdiqlash, jo'natish, bekor qilish va boshqa barcha amallar. Mijoz va tovar kartasi View bilan ham ochiladi — o'qish rejimida; saqlash va yangi mijoz yaratish Change talab qiladi.
- **Delete** ustuni Distributsiyada ishlatilmaydi (o'chirish va bekor qilish Change bilan).

### 1.10.1 Sahifa modullari

| Modul | Sahifa | Rahbar | Operator | Omborchi | Kassir | Agent* |
|---|---|---|---|---|---|---|
| DstHome | Distributsiya menyusi va bosh sahifasi (busiz menyu umuman chiqmaydi) | V | V | V | V | V |
| DstDashboard | Rahbar paneli | V | — | — | — | — |
| DstReports | Hisobotlar | V | V | — | — | — |
| DstOrders | Buyurtmalar | V C | V C | V C | V | V C |
| DstStock | Qoldiqlar | V | V | V | — | — |
| DstPayments | To'lovlar, agentlar naqdi | V C | V | — | V C | — |
| DstDebts | Qarzlar, akt sverki | V | V | — | V | — |
| DstReturns | Qaytarishlar | V C | V C | V | — | — |
| DstCustomers | Mijozlar | V C | V C | — | V | — |
| DstProducts | Tovarlar | V C | V C | V | — | — |
| DstPrices | Narxlar | V C | V | — | — | — |
| DstPromotions | Aksiyalar | V C | V | — | — | — |
| DstTerritories | Hududlar | V C | V C | — | — | — |
| DstTeams | Agentlar va jamoalar | V C | — | — | — | — |
| DstDictionaries | Ma'lumotnomalar | V C | V | — | — | — |
| DstSettings | Sozlamalar | V C | — | — | — | — |
| DstImport | Excel import | V C | V C | — | — | — |
| DstInventory | Inventarizatsiya | V C | — | V C | — | — |
| DstIssues | Xatolar jurnali | V C | V | V | V | V |
| DstTransfers | Ko'chirmalarni qabul qilish | V | — | V C | — | — |
| DstWriteOffs | Hisobdan chiqarish | V C | — | V C | — | — |
| DstRegrades | Pereso'rtitsa | V C | — | V C | — | — |
| DstLandedCosts | Qo'shimcha xarajatlar | V C | — | — | — | — |
| DstReorder | Buyurtma taklifi | V C | — | V | — | — |
| DstField | Dala nazorati | V | — | — | — | — |
| DstRoutes | Tashrif rejasi | V C | — | — | — | — |
| DstTrips | Reyslar | V C | V C | V | — | — |
| DstVehicles | Mashinalar, yetkazish sozlamalari | V C | — | — | — | — |
| DstKpi | Agent KPI | V C | — | — | — | — |
| DstAudit | Audit jurnali (faqat o'qish) | — | — | — | — | — |

V — View, C — Change. Jadval tavsiya; o'z tuzilmangizga moslang. **DstAudit** odatda faqat Admin'da (Admin uni doim ko'radi).

\* Agent asosan botda ishlaydi. Bot va Mini App ham agentning ERP foydalanuvchisi nomidan, shu huquqlar va ko'rinish doirasi bilan ishlaydi — botdan buyurtma yuborish uchun agentda **DstOrders** bo'yicha Change bo'lishi kerak.

### 1.10.2 Maxsus huquqlar

Bular sahifa emas, balki ruxsat bayrog'i. Ular huquqlar jadvalida modul sifatida turadi va **Change** belgisi bilan beriladi — faqat View yetmaydi.

| Modul | Nima ochadi | Kimga |
|---|---|---|
| **DstSeeCost** | Tannarx, xarid narxi, yalpi foyda, marja, qoldiq qiymati (ekranda va Excel'da); qo'shimcha xarajatlar (landed cost) bilan ishlash | Rahbar |
| **DstOverrideCredit** | Qarz limitidan oshgan yoki muddati o'tgan qarzli mijoz buyurtmasini izoh bilan tasdiqlash (web va botdagi **✅ Tasdiqlash**) | Faqat rahbar |
| **DstDiscountUnlimited** | Rol chegirma limitidan yuqori chegirma; minimal narx tekshiruvidan ozod | Faqat rahbar (ehtiyotkorlik bilan) |
| **DstWriteOffApprove** | Hisobdan chiqarishni tasdiqlash yoki rad etish (o'zi tuzgan hujjatni faqat Admin tasdiqlaydi) | Rahbar |

Admin va Developer bu huquqlarning hammasiga avtomatik ega.

> **Eslatma:** Admin panelda yangi rol yaratilganda unga barcha modullarda View avtomatik beriladi, lekin maxsus huquqlar bu bilan berilmaydi — ular uchun Change ni alohida belgilang.

> **Muhim:** Yangilanishdan keyin mavjud bazada avval View belgilangan maxsus huquq qatorlariga Change ham avtomatik qo'yilgan (egalari saqlangan). Har rolda shu to'rt qatorni ko'rib chiqing va keraksiz joyda Change ni olib tashlang.

## 1.11 Excel import

**Kim:** Admin, Operator   **Menyu:** Distributsiya → Excel import

Boshlang'ich ma'lumotni qo'lda emas, Excel'dan yuklash uchun. Sahifada yorliqlar yuklash tartibida joylashgan:

> Yuklash tartibi: hududlar → narx turlari → (agentlar — Jamoalar sahifasida) → mijozlar → marshrutlar → tovarlar → birliklar → narxlar → kirish qarzlari. Shablonning "Namuna" varag'ida misol bor.

### 1.11.1 Umumiy oqim

1. Kerakli yorliqni tanlang va **Shablonni yuklab olish** ni bosing. Shablon interfeys tilida; "Namuna" varag'ida to'ldirilgan misol bor.
2. Shablonni to'ldiring (birinchi varaq, sarlavha qatorini o'zgartirmang).
3. **Avval ko'rib chiqish (tavsiya)** belgisini qoldirib faylni tanlang va **Yuklash** ni bosing.
4. Natija:
   - xato bo'lsa — har xato qator raqami bilan; "N ta xato — hech narsa yozilmadi.";
   - xato bo'lmasa — "Xato yo'q, hali hech narsa yozilmadi. Quyidagi ro'yxatni tekshiring va "Import qilish" ni bosing." va har qator uchun amal: **Yangi**, **Yangilanadi** yoki **O'zgarishsiz**.
5. Ro'yxatni tekshirib **Import qilish** ni bosing. Fayl qayta tekshiriladi va bitta amalda yoziladi.

Qoidalar:

- **Hammasi yoki hech narsa:** bitta qatorda xato bo'lsa ham hech narsa yozilmaydi — xatolarni tuzatib qayta yuklang.
- Faqat `.xlsx`, 10 MB gacha.
- Ko'rib chiqilgan fayl 2 soat saqlanadi. Kechiksangiz: "Ko'rib chiqilgan fayl topilmadi yoki eskirgan — faylni qayta yuklang".
- Bir xil faylni qayta yuklash xavfsiz: bor yozuvlar yangilanadi, o'zgarmaganlari "O'zgarishsiz".
- Faylda bir yozuv ikki marta bo'lsa: "Faylda takrorlangan: …".
- Ma'lumotnomada yo'q qiymat (hudud, agent, kategoriya, birlik, narx turi): "Topilmadi: …".

### 1.11.2 Import turlari

| # | Yorliq | Kalit (qanday topiladi) | Ustunlar | Eslatma |
|---|---|---|---|---|
| 1 | Hududlar | Nomi (uz yoki ru) | Viloyat*, Viloyat (rus), Tuman, Tuman (rus), Hudud, Hudud (rus) | Bor nom topiladi, yo'g'i yaratiladi. Hudud tumansiz bo'lmaydi |
| 2 | Narx turlari | Nomi | Nomi*, Nomi (rus), Standart (ha/yo'q), Tartib | "ha" faqat bitta qatorda |
| 3 | Mijozlar | STIR, bo'lmasa nomi | Nomi*, STIR, Telefon, Telefon 2, Shahar, Manzil, Mo'ljal, Egasi, Aloqa shaxsi, Nuqta turi, Toifa (A/B/C), Hudud, Agent (login), Narx turi, Tashrif kunlari (1-7), Kredit limiti, Kechiktirish (kun), Kenglik, Uzunlik | Hudud — nomi yoki yo'li "Toshkent sh. / Chilonzor"; tashrif kunlari "1,4" (Du=1 … Ya=7); nuqta turi — SHOP, MARKET, BRIGADE, FOREMAN, PRIVATE |
| 4 | Marshrutlar | Mijoz (STIR yoki nomi) | Mijoz*, Agent (login), Tashrif kunlari (1-7) | Faqat mavjud mijozlar; bo'sh katak — o'zgarishsiz, "-" — tozalash. Agentlar o'rtasida qayta taqsimlash uchun qulay |
| 5 | Tovarlar | Artikul | Artikul*, Nomi*, Nomi (rus), Kategoriya, Asosiy birlik, Brend, Ishlab chiqaruvchi, Og'irlik (kg), Hajm (m3), Shtrix-kod, Birlik 2, Koeffitsient 2, Shtrix-kod 2, Birlik 3, Koeffitsient 3, Shtrix-kod 3 | Yangi tovar uchun asosiy birlik majburiy. Kategoriya va birlik bazada bo'lishi kerak; brend yo'q bo'lsa yaratiladi. Bir nechta shtrix-kod — ";" bilan |
| 6 | Tovar birliklari | Artikul + birlik | Artikul*, Birlik*, Koeffitsient*, Og'irlik (kg), Hajm (m3), Shtrix-kod, Odatiy sotish (ha/yo'q) | Mavjud tovarlarga qo'shimcha birlik (1 poddon = 40 qop) |
| 7 | Narxlar | Artikul | Artikul* + har faol narx turi ustuni + "<narx turi> min" | Asosiy birlik narxi. Bo'sh katak — o'zgarishsiz. Shablon ustunlari faol narx turlaridan tuziladi |
| 8 | Kirish qarzlari | Mijoz (STIR yoki nomi) | Mijoz*, Qarz (+) / avans (−)*, To'lov muddati | Har mijozga bir marta. Muddat bo'sh — bugun + mijoz kechiktirish kunlari |

\* — majburiy ustun. Majburiy ustun faylda bo'lmasa: "Majburiy ustun yo'q: …".

Turga xos xatolar:

| Xabar | Sabab |
|---|---|
| "Shu nomli bir nechta mijoz bor — STIR bilan aniqlashtiring." | Mijoz nomi takrorlanadi |
| "Hudud aniq emas: … — "Viloyat / Tuman / Hudud" yo'lini yozing." | Bir xil nomli hududlar bor |
| "Yangi tovar uchun asosiy birlik majburiy." | Yangi artikulda asosiy birlik bo'sh |
| "Birlik 2 va koeffitsient 2 birga kiritiladi." | Juftlikning biri bo'sh |
| "Bazada sex (SHOPS) yo'q — yangi tovar yaratib bo'lmaydi." | Umumiy ma'lumotnomada bitta ham sex yo'q — avval yarating |
| "Narx turi nomli ustun topilmadi (shablonni yuklab oling)." | Narxlar faylida narx turi ustunlari yo'q |
| "Bu mijozga kirish qoldig'i allaqachon kiritilgan." | Kirish qarzi ikkinchi marta |
| ""Standart" faqat bitta qatorda bo'lishi mumkin" | Narx turlarida ikkita "ha" |

### 1.11.3 DEMO misoli: kirish qarzi

DEMO Bunyodkor qurilish tizimga o'tishdan oldin 3 000 000 so'm qarzdor, muddati 20.11.2026:

| Mijoz (STIR yoki nomi) | Qarz (+) / avans (−) | To'lov muddati |
|---|---|---|
| DEMO Bunyodkor qurilish | 3000000 | 20.11.2026 |

Import qilingach mijoz qarzi 3 000 000 so'm, akt sverkida "Kirish qoldig'i" qatori. Avans bo'lsa summani minus bilan yozing (`-500000`).

Summa xato kiritilgan bo'lsa: **Qarzlar → mijoz akti** sahifasida **Kirish qoldig'ini bekor qilish** (sabab bilan) — teskari yozuv qilinadi, keyin to'g'ri summani shu import orqali qayta kiriting (3-bob, 3.10).

> **Muhim:** Boshlang'ich ombor qoldig'i import orqali kiritilmaydi. U **Distributsiya → Inventarizatsiya** dagi **Boshlang'ich** sanoq bilan kiritiladi (omborchi sanaydi, tizim qoldiqni sanalganga tenglaydi). Batafsil — 4-bob, 4.3.

## 1.12 Tayyorlik tekshiruvi

Sozlash tugagach, quyidagilarni tekshiring:

| # | Tekshiruv | Kutilgan natija |
|---|---|---|
| 1 | Import natijasidagi soni | Excel'dagi qatorlar soniga teng |
| 2 | 3–5 ta tasodifiy mijoz kartasi | Hudud, agent, narx turi, limit, muddat to'g'ri |
| 3 | Narxlar → "Standart narxi yo'q" filtri | Bo'sh (yoki ataylab narxsiz tovarlar) |
| 4 | Qoldiqlar | Boshlang'ich sanoqdan keyin haqiqiy = sanalgan |
| 5 | Agent botida 🗺 Marshrut | Bugungi tashrif kunidagi mijozlar chiqadi |
| 6 | Agent roli huquqlari | DstSeeCost, DstOverrideCredit, DstDiscountUnlimited, DstWriteOffApprove — Change belgilanmagan |
| 7 | Sinov buyurtmasi (2-bob) | Yaratildi → tasdiqlandi → bekor qilindi, rezerv qaytdi |
