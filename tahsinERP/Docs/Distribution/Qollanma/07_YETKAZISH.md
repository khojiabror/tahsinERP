# 7. Yetkazish va nazorat

Bu bob ofis tomonidan yetkazishni boshqarishni tushuntiradi: mashinalar, reys rejalash (sig'im tekshiruvi va marshrut), yo'lga chiqarish, yetkazish natijalarini kuzatish, reysni yakunlash va yopish, farq dalolatnomasi, qayta yetkazish, reys xarajatlarini moliyaga o'tkazish, shuningdek agentlarning kunlik tashrif rejasi (reja/fakt), ovozli buyurtmani yoqish va mijozga yuboriladigan xabarlar. Ekspeditorning botdagi ishi — 6.5-bo'limda.

Asosiy foydalanuvchilar — **logist / yetkazish bo'limi** (reys rejalaydi va yopadi), **omborchi** (yuk ortadi, qaytgan tovarni qabul qiladi), **buxgalter / kassir** (reys xarajatlari), **rahbar va supervayzer** (tashrif rejasi, nazorat). Misollar demo-ssenariy bo'yicha: mashina **01 A 123 AA** (Isuzu, 5 000 kg, 20 m³), ekspeditor **Sherzod**, agent **Jasur**, ombor **DEMO Asosiy ombor (Toshkent)**, mijoz **DEMO Anvar do'koni**.

## 7.1 Asosiy tushunchalar va oqim

- **Jo'natish (DJ-...)** — buyurtma bo'yicha ombordan chiqim hujjati. Reysga buyurtma emas, **o'tkazilgan jo'natish** qo'shiladi. Bitta buyurtma bir nechta jo'natishga bo'linishi mumkin.
- **Reys (R-YYYY-NNNNN)** — bir mashina, bir ekspeditor va bir ombordan bir kunlik yetkazish safari. Reysdagi har jo'natish — **manzil**.
- **Manzil natijasi** — Kutmoqda, Yetkazildi, Qisman, Rad etildi.
- **Farq dalolatnomasi** — reys yopilganda yetkazilmagan tovar: omborga qaytgani va **kamomad** (yo'qolgani).
- **Kamomad** — yetkazilmagan, lekin omborga ham qaytmagan tovar. Mijozdan undirilmaydi; tannarxi reysda qayd qilinadi (ekspeditor KPI va nazorat uchun).
- **Tashrif rejasi** — agentning aniq bir kunga qaysi mijozlarga, qaysi tartibda borishi. Bo'lmasa — mijoz kartasidagi tashrif kunlari (andoza).

```
Buyurtma (D) → ofis tasdig'i → yig'ish → Jo'natish (DJ, o'tkazilgan)
        │                                       │ mijozga "jo'natildi"
        ▼                                       ▼
                      Reys rejalash (R): mashina + ekspeditor + jo'natishlar
                      sig'im (kg, m³) tekshiruvi, marshrut tartibi, xarita
                                       │ ekspeditorga "reys biriktirildi"
                                       ▼
                      Yo'lga chiqarish → mijozlarga "yo'lga chiqdi"
                                       │
                                       ▼
          Yetkazish: Yetkazildi / Qisman / Rad etildi (bot yoki ofis)
          mijozga xabar + nakladnoy PDF; buyurtma → "Yetkazildi"
                                       │
                                       ▼
          Reysni yakunlash (ekspeditor, bot): reys "Yo'lda" qoladi,
                         rahbar va omborchiga "omborda sanab, yoping" xabari
                                       │
                                       ▼
          Reysni yopish (ofis): ombor sanagan qaytgan tovar → qaytarish
                         hujjati (DQ) → ombor; qaytmagani → kamomad;
                         rahbarga yakun xabari
                                       │
                                       ▼
          Reys xarajatlari → Moliyaga (kassa/hisobdan chiqim)
```

## 7.2 Tayyorgarlik: ruxsatlar va ekspeditor

**Kim:** Admin

1. **Ruxsatlar.** 2-bosqich sahifalari alohida ruxsat modullari: **DstVehicles** (Mashinalar), **DstTrips** (Reyslar), **DstRoutes** (Tashrif rejasi), **DstKpi** (Agent KPI). Admin roliga avtomatik beriladi; logist, omborchi va boshqalarga rollar orqali bering (ko'rish — sahifani ochish, o'zgartirish — saqlash, yopish va h.k.).
2. **Ekspeditor.** Distributsiya → **Agentlar va jamoalar** → **Qo'shish** (yoki mavjud xodimni tahrirlash): foydalanuvchi (masalan `demo_eksp`), **Ekspeditor** belgisi, **telefon** (mijozga "yo'lga chiqdi" xabarida ko'rsatiladi), ombor. Faqat shunday belgilangan faol xodim reysga ekspeditor bo'la oladi.
3. **Botga ulash.** Ekspeditor qatoridagi Telegram tugmasi → kod → Sherzod botga ulanadi (6.3-bo'lim). Ulanmagan ekspeditorga reys berish mumkin, lekin u botdan foydalana olmaydi va natijani ofis kiritadi.
4. **Tovar og'irligi va hajmi.** Reys sig'imi tovar kartasidagi asosiy birlik og'irligi/hajmidan (bo'lmasa — sotish birligi og'irligidan) hisoblanadi. Og'irligi kiritilmagan tovar reysda **0 kg** bo'lib ko'rinadi — Tovarlar kartasida to'ldiring (masalan Sement M400: qop = 50 kg).
5. **Mijoz koordinatalari.** Xarita, marshrut tartibi va navigatsiya mijoz kartasidagi koordinatadan ishlaydi. Koordinatasiz mijozlar ro'yxatlarda **⚠** bilan belgilanadi.

## 7.3 Mashinalar

**Kim:** logist / Admin (DstVehicles)   **Qayerda:** web menyu Distributsiya → Mashinalar

Ro'yxat ustunlari: Davlat raqami, Rusumi, Yuk ko'tarish (kg), Hajm (m³), Haydovchi (telefoni bosiladigan), Ekspeditor, Ombor, Reyslar (bekor qilinmagan reyslar soni). Nofaol mashina kulrang, **Nofaol** belgisi bilan. Sahifa tepasida **Reyslar** sahifasiga havola.

1. **Qo'shish** ni bosing.
2. To'ldiring va **Saqlash**:

| Maydon | Nima kiritiladi | Demo misol |
|---|---|---|
| Davlat raqami * | 4–20 belgi; katta harflarga o'giriladi; takrorlanmaydi | 01 A 123 AA |
| Rusumi | Ixtiyoriy | Isuzu |
| Yuk ko'tarish, kg * | Musbat son | 5000 |
| Hajm, m³ | Ixtiyoriy; kiritilsa — musbat | 20 |
| Haydovchi, Telefon | Ixtiyoriy (haydovchi ekspeditordan boshqa odam bo'lsa) | Sherzod, +998901112233 |
| Ekspeditor | Odatiy ekspeditor — reysda mashina tanlansa avtomatik qo'yiladi | Sherzod |
| Ombor | Odatiy ombor — reysda avtomatik tanlanadi | DEMO Asosiy ombor (Toshkent) |
| Izoh | 300 belgigacha | |
| Faol | O'chirilsa — yangi reysga tanlanmaydi | yoqiq |

Xatolar: "Davlat raqamini kiriting (4–20 belgi).", "Yuk ko'tarish va hajm musbat bo'lsin.", takror raqam — dublikat xatosi, ekspeditor emas xodim tanlansa — "Ekspeditorni tanlang (Jamoalar sahifasida ekspeditor sifatida qo'shilgan xodim)."

> **Eslatma:** Mashina o'chirilmaydi — ishlatilmay qolsa **Faol** belgisini olib tashlang; eski reyslar tarixi saqlanadi.

## 7.4 Yetkazish sozlamalari

**Kim:** Admin   **Qayerda:** Distributsiya → Mashinalar → pastdagi **Yetkazish sozlamalari** kartasi

| Sozlama | Nima qiladi |
|---|---|
| **Yandex Maps JavaScript API kaliti** | Reys va tashrif rejasi sahifalarida jonli xarita. developer.tech.yandex.ru da olinadi. Bo'sh bo'lsa xarita o'rniga ro'yxat va Yandex havolalari ko'rsatiladi |
| **Botda ovozli buyurtma** | Agent ovozli xabar bilan buyurtma bera oladi (7.12) |
| **Omborlar koordinatasi (marshrut boshi)** | Har savdo ombori uchun `kenglik,uzunlik`, masalan `41.311081,69.240562` (Yandex xaritadan nusxa). Marshrut va "Tartiblash" shu nuqtadan boshlanadi |

**Saqlash** ni bosing. Noto'g'ri kalit — "Kalit formati noto'g'ri."; noto'g'ri koordinata — "Koordinatalar noto'g'ri."

Boshqa sozlamalar Distributsiya → **Sozlamalar** sahifasida: Telegram bot nomi, **Bot ochiq manzili** (yetkazish rasmi va imzo ERP'da ko'rinishi uchun shart), Tashrif radiusi, Kun boshida selfi majburiy, Keldim — faqat jonli joylashuv, Agent to'lovlari (ekspeditor to'lovlari ham shu qoidaga bo'ysunadi) — 6.2-bo'lim.

## 7.5 Reys rejalash

**Kim:** logist (DstTrips, o'zgartirish)   **Qayerda:** Distributsiya → Reyslar → **Yangi reys**

### 7.5.1 Reyslar ro'yxati

Filtr: **Dan**, **Gacha**, Holat, Ekspeditor → **Ko'rsatish**. Ustunlar: Raqam, Sana, Holat, Mashina, Ekspeditor, Ombor, Manzillar (`hal qilingan/jami`), kg, Summa, Yetkazildi. Rejalangan reys raqami bosilsa — tahrirlash, boshqalari — reys sahifasi.

### 7.5.2 Yangi reys yaratish

1. **Yangi reys** ni bosing.
2. Sarlavha: **Sana** (standart — bugun), **Mashina \*** (ro'yxatda `01 A 123 AA Isuzu — 5 000 kg, 20 m³`), **Ekspeditor \***, **Ombor \***, **Izoh**. Mashina tanlansa, uning odatiy ekspeditori (bo'sh bo'lsa) va ombori avtomatik qo'yiladi.
3. Chap kartada **Jo'natishlar (reyssiz)** — shu ombordan:
   - o'tkazilgan (ombordan chiqqan) jo'natishlar;
   - "o'zi olib ketadi" bo'lmaganlar;
   - buyurtmasi yetkazilmagan va bekor qilinmaganlar;
   - boshqa reysga qo'shilmaganlar.
   Har qatorda: mijoz (koordinatasi yo'q bo'lsa **⚠**), manzil va hudud, buyurtma va jo'natish raqami, yetkazish sanasi, kg, summa. Yetkazish sanasi bo'yicha tartiblangan; qidiruv maydoni mijoz, buyurtma, manzil va hudud bo'yicha filtrlaydi.
4. Jo'natishni reysga qo'shish uchun **+** ni bosing — u o'ng kartadagi **Marshrut (n)** ro'yxatiga o'tadi.
5. Marshrut ro'yxatida: **↑ / ↓** — tartibni o'zgartirish, **×** — reysdan olib tashlash.
6. Yuqoridagi ikki chiziq yuklamani ko'rsatadi: **Og'irlik, kg** `2 450 / 5 000 kg (49%)` va **Hajm, m³**. 100% dan oshsa chiziq qizil bo'ladi.
7. **Tartiblash** — ombordan boshlab har safar eng yaqin keyingi manzil (koordinatasiz manzillar oxirida). Taxminiy masofa `≈ 18,4 km` ko'rinishida chiqadi.
8. Yandex kaliti sozlangan bo'lsa, xaritada ombor (⌂), tanlangan manzillar (raqamli ko'k belgilar) va tanlanmaganlar (kulrang nuqta) hamda yo'nalish chizig'i ko'rinadi.
9. **Saqlash** — reys raqami beriladi (`R-2026-00007`), reys sahifasi ochiladi.

Saqlashda tekshiruvlar:

| Xabar | Sababi |
|---|---|
| Faol mashinani tanlang. | Mashina tanlanmagan yoki nofaol |
| Ekspeditorni tanlang (Jamoalar sahifasida ekspeditor sifatida qo'shilgan xodim). | Ekspeditor yo'q yoki faol ekspeditor emas |
| Og'irlik 5 320 kg mashina sig'imidan (5 000 kg) oshdi. | Yuk ko'tarishdan oshgan — **reys saqlanmaydi** |
| Hajm 22 m³ mashina sig'imidan (20 m³) oshdi. | Hajmdan oshgan (mashinada hajm kiritilgan bo'lsa) |
| DJ-2026-00031: jo'natish boshqa reysda. | Shu orada boshqa logist qo'shib yuborgan |
| DJ-...: jo'natish boshqa ombordan. | Reys ombori boshqa |
| DJ-...: jo'natish yetkazishga yaroqsiz (o'zi olib ketish, bekor qilingan yoki allaqachon yetkazilgan). | Jo'natish holati o'zgargan |

Reys yaratilganda (yoki ekspeditor almashtirilganda) ekspeditorga xabar ketadi: `🚚 Sizga reys R-2026-00007 (21.11.2026) biriktirildi: mashina 01 A 123 AA, 6 ta manzil, 2450 kg. Botda: 🚚 Reys.`

> **Muhim:** Sig'imdan oshgan reys saqlanmaydi. Yukni ikki reysga bo'ling yoki kattaroq mashina tanlang.
> **Eslatma:** Bitta jo'natish faqat bitta reysda bo'ladi. Ombor almashtirilsa tanlangan ro'yxat tozalanadi.

### 7.5.3 Rejalangan reysni o'zgartirish va bekor qilish

- **Tahrirlash** — faqat **Rejalandi** holatida (jo'natish qo'shish/olib tashlash, mashina, ekspeditor, sana). Boshqa holatda — "Reysni bu holatda o'zgartirib bo'lmaydi."
- **Bekor** — faqat rejalangan reys; sabab so'raladi. Jo'natishlar bo'shaydi va boshqa reysga qo'shilishi mumkin; kutayotgan xarajatlar bekor bo'ladi.

## 7.6 Reys holatlari va reys sahifasi

| Reys holati | Ma'nosi | Mumkin amallar |
|---|---|---|
| **Rejalandi** | Yaratilgan, yuk ortilmoqda | Tahrirlash, Yo'lga chiqarish, Bekor, Tartiblash |
| **Yo'lda** | Mashina chiqdi (ekspeditor botda yakunlagan bo'lsa ham — yopilguncha shu holatda) | Natija kiritish/qaytarish (↺), Tartiblash, xarajat, Reysni yopish |
| **Yopildi** | Hammasi hal qilingan, farqlar qayd etilgan | Farq dalolatnomasi, xarajatlarni moliyaga o'tkazish, Qayta yetkazish |
| **Bekor qilingan** | Rejalangan reys bekor qilindi | — |

| Manzil holati | Rang (xaritada) |
|---|---|
| Kutmoqda | ko'k |
| Yetkazildi | yashil |
| Qisman | to'q sariq |
| Rad etildi | qizil |

**Reys sahifasi** (Reyslar → reys raqami):

- **Sarlavha:** raqam va holat, sana, mashina, ekspeditor va telefoni, ombor, haydovchi; "Yo'lga chiqdi" va "Yopildi" vaqtlari; bekor qilingan bo'lsa sababi.
- **Tugmalar:** **Marshrut varag'i** (chop etish), **Farq dalolatnomasi** (yopilgan reysda), **Tahrirlash** / **Yo'lga chiqarish** / **Bekor** (rejalangan), **Tartiblash** (rejalangan va yo'ldagi — yo'lda bo'lsa faqat kutayotgan manzillar qayta tartiblanadi).
- **Ko'rsatkichlar:** Manzillar (hal qilingan/jami), Og'irlik (sig'im %), Hajm, Summa, Yetkazildi, Reys xarajatlari; yopilgan reysda **Kamomad tannarxi** (0 dan katta bo'lsa qizil).
- **Xarita** (7.6.1).
- **Marshrut jadvali:** #, ↑↓ (yopilmagan reysda), Mijoz (manzil, mo'ljal, telefon; ⚠ Mijoz koordinatasi yo'q), Buyurtma va jo'natish raqami, kg, Summa, Holat (sabab, izoh, vaqt), Yetkazildi (summa; qaytarish hujjati raqami; kamomad), **Tasdiq** (yetkazish rasmi va imzo kichik rasmlari — bosilsa kattalashadi), **Tovarlar** (qatorlar: Jo'natilgan, Yetkazildi, Omborga qaytdi, Summa), **Natija** / **↺**.
- **Reysni yopish** kartasi (yo'ldagi reysda) va **Reys xarajatlari** jadvali.

### 7.6.1 Marshrut xaritada (Yandex)

Kalit sozlangan bo'lsa xaritada: ombor (⌂, koordinatasi kiritilgan bo'lsa), raqamlangan manzillar holat rangida, avtomobil yo'li bo'ylab marshrut (50 nuqtagacha). Belgiga bosilsa — mijoz, buyurtma, summa, holat.

Kalit bo'lmasa: "Xarita uchun Yandex kaliti sozlanmagan (Mashinalar → Yetkazish sozlamalari)." va **Yandex xaritada ochish** havolasi — xuddi shu marshrut Yandex Xaritalarda (kalitsiz) ochiladi. Ekspeditor botda xuddi shu havolani **🗺 Marshrut (Yandex)** tugmasi sifatida oladi. Koordinatali nuqta umuman bo'lmasa — "Xaritada ko'rsatish uchun koordinatali nuqta yo'q."

### 7.6.2 Marshrut varag'i

**Marshrut varag'i** — chop etiladigan sahifa: mashina va haydovchi, ekspeditor va telefoni, ombor, jami kg va summa; jadvalda manzillar tartibda — #, Mijoz (manzil, mo'ljal, telefon), Buyurtma va jo'natish raqami, tovarlar va miqdorlar, Summa, mijozning Qarzi va bo'sh **Qabul qildi** ustuni (mijoz imzosi uchun). Pastda imzo joylari: Topshirdi va Ekspeditor. Haydovchiga qog'ozda beriladi (telefoni yo'q yoki bot ishlamaydigan holat uchun).

## 7.7 Yo'lga chiqarish va yetkazishni kuzatish

**Kim:** logist   **Qayerda:** reys sahifasi

### 7.7.1 Yo'lga chiqarish

Yuk ortilgach reys sahifasida **Yo'lga chiqarish** → "Ishonchingiz komilmi?" → OK. Ekspeditor buni botda **▶️ Yo'lga chiqish** bilan o'zi ham qiladi — natija bir xil:

- reys holati **Yo'lda**, "Yo'lga chiqdi" vaqti yoziladi;
- ekspeditorning ish kuni avtomatik (selfisiz) ochiladi — jonli joylashuvi Dala nazoratida yoziladi; reys yakunlanganda yoki yopilganda (boshqa yo'ldagi reysi bo'lmasa) ish kuni yopiladi;
- Telegram'ga ulangan har mijozga (bir nechta buyurtmasi bo'lsa — bitta xabar): `🚚 Buyurtmangiz yo'lga chiqdi: D-2026-00045 (1 240 000 so'm).\nEkspeditor: Sherzod, +998901112233\nMashina: 01 A 123 AA`.

Reysda manzil bo'lmasa — "Reysda jo'natish yo'q."

> **Eslatma:** Buyurtmaning o'z holati "Yo'lda"ga o'zgarmaydi — "yo'lda" reys holati; mijoz buni xabardan biladi.

### 7.7.2 Ekspeditor natijalari

Ekspeditor botda har manzilga natija kiritadi (6.5-bo'lim). Ofis reys sahifasini yangilab kuzatadi: holat, yetkazilgan summa, vaqt, sabab, **Tasdiq** ustunida rasm yoki imzo. Har natijada:

- **Yetkazildi / Qisman** — mijozga xabar va nakladnoy PDF (7.13);
- **Qisman / Rad etildi** — rahbarlarga `⚠️ Reys R-2026-00007: DEMO Anvar do'koni, buyurtma D-2026-00045 — Qisman. Sabab: Shikastlangan qop. Ekspeditor: Sherzod.`;
- buyurtmaning barcha (o'zi olib ketishdan tashqari) jo'natishlari hal qilinsa va oxirgi natija rad bo'lmasa, buyurtma **Yetkazildi** holatiga o'tadi (bu holda mijozga alohida "yetkazildi" xabari ketmaydi — u manzil xabarini allaqachon olgan).

### 7.7.3 Natijani ofisdan kiritish

Ekspeditor botdan foydalana olmasa (telefoni o'chgan, internet yo'q) natijani ofis telefon orqali tasdiqlab kiritadi.

1. Manzil qatorida **Natija** ni bosing.
2. Tanlang: **Yetkazildi** (hammasi), **Qisman** (har tovar uchun yetkazilgan miqdorni kiriting), **Rad etildi**.
3. Qisman va radda **Sabab** majburiy (qaytarish sabablari — Ma'lumotnomalar → Sabablar, turi "Qaytarish"). **Izoh** ixtiyoriy.
4. **Saqlash**.

> **Eslatma:** Ofisdan kiritilganda rasm/imzo talab qilinmaydi (masalan telefon orqali tasdiq). Botdan kiritilganda esa rasm yoki imzo majburiy.

Tekshiruvlar: "Yetkazilgan miqdor jo'natilgandan (20) ko'p bo'lmasin.", "Qisman yetkazish yoki rad uchun sababni tanlang.", "Bu manzil bo'yicha natija allaqachon kiritilgan.", "Reys yo'lda emas."

### 7.7.4 Natijani bekor qilish

Xato kiritilgan natijani reys **yopilguncha** bekor qilish mumkin: qatordagi **↺** (Natijani bekor qilish) → tasdiq. Manzil yana **Kutmoqda** bo'ladi, keyin natijani qayta kiriting (ofisdan yoki ekspeditor botdan).

↺ bilan birga qaytariladi:

- buyurtma natija sabab **Yetkazildi** bo'lgan bo'lsa — yana **Jo'natildi**, yetkazish sanasi olib tashlanadi (buyurtma allaqachon Yopildi bo'lsa — faqat sana);
- yetkazish fotosi/imzosi, izoh va koordinata tozalanadi;
- mijozga hali yuborilmagan natija xabari o'chiriladi; yuborilgan bo'lsa mijozga `↺ yetkazish natijasi bekor qilindi va qayta kiritiladi` xabari ketadi.

## 7.8 Reysni yopish va farq dalolatnomasi

**Kim:** logist + omborchi (qaytgan tovarni sanaydi)   **Qayerda:** reys sahifasi → **Reysni yopish** kartasi

Reys yopilishi uchun **hamma manzil bo'yicha natija** bo'lishi kerak; aks holda kartada "Avval barcha manzillar bo'yicha natija kiriting (ekspeditor botda yoki shu yerda)." yozuvi turadi.

Odatiy tartib: ekspeditor hamma manzilni hal qilgach botda **🏁 Reysni yakunlash** ni bosadi (6.5.9). Bu reysni yopmaydi — reys "Yo'lda" qoladi, rahbar va shu ombor omborchisiga xabar keladi: `🏁 Reys R-2026-00007 yakunlandi (…). Qaytadigan tovar: 1 480 000 so'm — omborda sanab, Reyslar sahifasida yoping (qaytgan miqdor va kamomad).` Reysni har doim ofis shu sahifada yopadi.

1. Mashina qaytgach omborchi ortib qolgan tovarni sanaydi.
2. Kartadagi jadvalda yetkazilmagan har qator: Mijoz, Tovar, **Yetkazilmagan** miqdor va **Omborga qaytdi** maydoni (standart — yetkazilmaganning hammasi). Haqiqatda qaytgan miqdorni yozing.
3. Ombor joylashuvli (DLOC) bo'lsa — **Joylashuv** ni tanlang (qaytgan tovar qayerga qo'yiladi).
4. **Izoh** (ixtiyoriy) → **Reysni yopish** → tasdiq.

Yopishda tizim har manzil bo'yicha:

| Natija | Nima bo'ladi |
|---|---|
| Omborga qaytgan miqdor | Mijoz uchun **qaytarish hujjati (DQ)** avtomatik yaratiladi: ombor kirimi (jo'natishdagi FIFO tannarxda), mijoz qarzi qaytgan tovar summasiga kamayadi. Sabab — manzil sababi, izohda reys raqami |
| Qaytmagan miqdor (yetkazilmagan − qaytgan) | **Kamomad**: mijozga tuzatish krediti (sotuv narxida — mijoz olmagan tovarga to'lamaydi); tovar tannarxi reysning **Kamomad tannarxi** ga yoziladi |
| Hammasi yetkazilgan manzil | O'zgarishsiz |

So'ng reys **Yopildi**, mijozlar qarzi buyurtmalar bo'yicha qayta taqsimlanadi va rahbarlarga xabar ketadi: `🏁 Reys R-2026-00007 yopildi: yetkazildi 5/6, 8 380 000 so'm; omborga qaytdi 1 418 000 so'm, kamomad 62 000 so'm; xarajat 300 000 so'm.`

Tekshiruv: "Qaytgan miqdor yetkazilmagandan (3) ko'p bo'lmasin."; kutayotgan manzil bo'lsa — "2 ta manzil bo'yicha natija yo'q — avval yetkazildi/rad belgilang."

> **Eslatma:** Botdagi yakunlash hech narsani omborga yozmaydi — qaytgan miqdor va kamomad faqat shu yerda, ombor sanog'i bo'yicha kiritiladi. Yakunlangan reys ekspeditorning keyingi rejalangan reysini to'smaydi.

### 7.8.1 Ishlangan misol

Reys **R-2026-00007**, manzil **DEMO Anvar do'koni**: jo'natish DJ-2026-00031 — **20 qop Sement M400 × 62 000 = 1 240 000 so'm** (tannarx 40 000 so'm/qop).

1. Sherzod botda **✍️ Imzo / qisman**: 17 qop yetkazildi, sabab "Shikastlangan qop", mijoz imzosi. Natija — **Qisman, 1 054 000 so'm**. Mijozga qisman nakladnoy (17 qop), rahbarga ogohlantirish.
2. Mashina qaytdi. Omborchi sanadi: 3 qopdan 2 tasi butun qaytdi, 1 tasi yo'l-yo'lakay yorilib tashlangan.
3. Logist **Reysni yopish** jadvalida Anvar do'koni qatoriga **Omborga qaytdi = 2** yozib yopadi.

| Ko'rsatkich | Hisob | Summa |
|---|---|---|
| Jo'natish (mijoz qarziga yozilgan) | 20 × 62 000 | 1 240 000 |
| Qaytarish hujjati DQ (ombor kirimi 2 qop) | 2 × 62 000 | − 124 000 |
| Kamomad — mijozga tuzatish krediti | 1 × 62 000 | − 62 000 |
| **Mijoz to'laydi** | 17 × 62 000 | **1 054 000** |
| Reysning kamomad tannarxi | 1 × 40 000 | 40 000 |

Ombor qoldig'i 2 qopga oshadi; 1 qop kamomad farq dalolatnomasida ko'rinadi va ekspeditor KPI'siga (sxemada "Reys kamomadidan, %") ta'sir qiladi.

### 7.8.3 Qayta yetkazish

Rad etilgan yoki qisman qaytgan tovarni mijozga qayta yetkazish kerak bo'lsa:

1. Yopilgan reys sahifasida qaytarish hujjati bor manzil qatorida **Qayta yetkazish** ni bosing.
2. Yangi buyurtma **qoralamasi** ochiladi: qaytgan miqdorlar (asosiy birlikda), joriy narx, izoh `Qayta yetkazish: D-2026-00045 (R-2026-00007)`.
3. Kerak bo'lsa tahrirlang, oddiy tartibda tasdiqlang, jo'nating va keyingi reysga qo'shing.

Tugma ikkinchi marta bosilsa — o'sha qoralama ochiladi (yangisi yaratilmaydi). Asl jo'natish va buyurtma o'zgarmaydi.

### 7.8.2 Farq dalolatnomasi (chop etish)

Yopilgan reys sahifasida **Farq dalolatnomasi** — chop etiladigan hujjat: kompaniya, reys, sana, yopilgan vaqt, mashina, ekspeditor, ombor; manzillar soni (Yetkazildi / Qisman / Rad etildi). Jadval faqat farqli qatorlar bilan: #, Mijoz (buyurtma → qaytarish hujjati), Tovar, Sabab, Jo'natilgan, Yetkazildi, Omborga qaytdi, Reys kamomadi, Summa. Pastda jamlar: Yetkazilmagan, Omborga qaytgan (qaytarish hujjati), Reys kamomadi (kamomad tannarxi bilan), Reys xarajatlari. Imzo joylari: Ekspeditor va Qabul qildi (Ombor). Farq bo'lmasa: "Farq yo'q — hammasi yetkazildi."

## 7.9 Reys xarajatlari

**Kim:** logist, buxgalter/kassir; ekspeditor — botdan   **Qayerda:** reys sahifasi → **Reys xarajatlari**

Turlar: **Yoqilg'i**, **To'xtash joyi**, **Ta'mir**, **Ovqat**, **Boshqa**.

**Ofisdan qo'shish:**

1. **Qo'shish** → **Turi**, **Summa**, **Chiqim moddasi** (moliyadagi faol chiqim moddasi), **Kassa / hisob**, **Izoh**.
2. Kassa tanlansa — darhol moliyaga chiqim; bo'sh qoldirilsa — xarajat **Kutmoqda** holatida qoladi ("Kassa tanlansa — darhol moliyaga chiqim; bo'sh — keyin o'tkaziladi.").
3. **Saqlash**.

**Ekspeditordan:** botdagi **⛽ Xarajat** (6.5.8) — jadvalda **bot** belgisi bilan, Kutmoqda holatida.

**Moliyaga o'tkazish:** Kutmoqda qatoridagi **Moliyaga** → chiqim moddasi va kassa/hisobni tanlang → **Saqlash**. Moliyada chiqim yozuvi paydo bo'ladi:

| Maydon | Qiymat |
|---|---|
| Hisob | Tanlangan kassa / hisob |
| Modda | Tanlangan chiqim moddasi; tanlanmagan bo'lsa — "Reys xarajatlari" tizim moddasi (avtomatik yaratiladi) |
| Kontragent | Mashina raqami (01 A 123 AA) |
| Maqsad | `Reys R-2026-00007: Yoqilg'i — AI-92` |

**Bekor qilish:** qatordagi **×** — o'tkazilgan xarajatning moliya yozuvi ham o'chadi. Bekor qilingan qator kulrang.

Xatolar: "Chiqim moddasini tanlang." (modda faol chiqim moddasi emas), "Xarajat allaqachon o'tkazilgan yoki bekor qilingan."

**Misol:** Sherzod botda `⛽ Xarajat → Yoqilg'i → 300000 AI-92` yozdi. Logist reys sahifasida qatorni **Moliyaga** bilan "Asosiy kassa"dan o'tkazdi — kassadan 300 000 so'm chiqim, reys yakuni xabarida "xarajat 300 000 so'm".

## 7.10 Tashrif rejasi

**Kim:** supervayzer / rahbar (DstRoutes)   **Qayerda:** Distributsiya → Tashrif rejasi

Mijoz kartasidagi **tashrif kunlari** (Du, Se, ...) — haftalik **andoza**. Tashrif rejasi sahifasida aniq kun uchun andozani o'zgartirish mumkin: mijoz qo'shish (boshqa hududdan ham), olib tashlash va tartiblash.

1. Tepada **Agent** (ko'rinishingizdagi agentlar) va **Sana**ni tanlang; **« Oldingi kun · Bugun · Keyingi kun »** havolalari va **Davr bo'yicha hisobot** (Hisobotlar → Tashriflar).
2. Chap karta **Kun rejasi** — belgisi **Tashrif kunlaridan** (andoza) yoki **Saqlangan reja**.
3. **Mijoz qo'shish…** maydonida mijozni qidirib tanlang — ro'yxat oxiriga qo'shiladi.
4. **↑ / ↓** — tartib, **×** — olib tashlash. Koordinatasiz mijoz **⚠** bilan.
5. **Saqlash** — kun rejasi saqlanadi (belgi "Saqlangan reja").
6. Tartiblash tugmasi (shoxlanish belgisi) — "Saqlash va eng yaqin manzil bo'yicha tartiblash (kun boshi joyidan)": agent shu kuni kunini boshlagan joydan eng yaqin qo'shni tartibida (kun boshlanmagan bo'lsa — birinchi mijozdan).
7. **Andozaga qaytish** — saqlangan reja o'chadi, yana tashrif kunlari ishlaydi.

Saqlangan reja ta'siri:

- agent botdagi **🗺 Marshrut** ni shu tartibda ko'radi;
- "Keldim"da tashrif "rejadagi" yoki "rejadan tashqari" deb shu reja bo'yicha belgilanadi;
- Tashriflar hisobotida reja soni andoza o'rniga shu rejadan olinadi.

Bir kunlik rejada ko'pi bilan 200 mijoz ("Bir kunlik rejada ko'pi bilan 200 ta mijoz.").

### 7.10.1 Reja / fakt

O'ng karta **Reja / fakt** tanlangan kun bo'yicha:

| Ko'rsatkich | Ma'nosi |
|---|---|
| Rejada | Rejadagi mijozlar soni |
| Borildi | Rejadagilardan borilgani |
| Rejadan tashqari | Rejada yo'q, lekin agent "Keldim" qilgan mijozlar |
| % | Reja bajarilishi |

Xaritada: rejadagi mijozlar tartib raqami bilan — **ko'k** (kutilmoqda), **yashil** (borildi), **qizil** (o'tgan kunda borilmagan); rejadan tashqari — **binafsha** "+" belgisi; agentning shu kungi joylashuv izi chiziq bilan. Kalit bo'lmasa — Yandex havolasi.

Jadval: Mijoz (rejadan tashqari bo'lsa belgi bilan), **Keldi** (vaqt), Holat (**Buyurtma**, **To'lov**, **Buyurtmasiz**, **Tashrif** — natija hali yo'q, **Borilmagan**), m (mijozgacha masofa "Keldim" paytida), Buyurtmalar (tashrif buyurtmasi summasi).

**Misol:** Jasurning seshanba andozasida 12 mijoz. Supervayzer 21.11 uchun rejaga Yunusobodagi **DEMO Bunyodkor qurilish** ni qo'shib, tartiblash tugmasini bosdi. Jasur botda marshrutni yangi tartibda oldi; kechqurun Reja / fakt: Rejada 13, Borildi 12, Rejadan tashqari 1, 92,3%.

### 7.10.2 Tashriflar hisoboti

**Qayerda:** Distributsiya → Hisobotlar → Tashriflar

Davr bo'yicha agentlar kesimida Reja, Fakt, Rejadagilar, Rejadan tashqari, Reja bajarilishi, Buyurtma bilan, Samaradorlik, To'lov bilan, Buyurtmasiz, Tashrif buyurtmalari, O'rtacha buyurtma. Kun rejasi saqlangan kunlarda reja soni shu rejadan, qolgan kunlarda — tashrif kunlari andozasidan.

## 7.11 Ofis va ekspeditor: kun tartibi (namuna)

| Vaqt | Ofis (web) | Ekspeditor (bot) |
|---|---|---|
| Kechqurun / ertalab | Buyurtmalar tasdiqlanadi, omborchi yig'ib jo'natadi | — |
| 08:00 | Reyslar → Yangi reys: 01 A 123 AA, Sherzod, 6 jo'natish, Tartiblash, Saqlash | "🚚 Sizga reys ... biriktirildi" |
| 08:30 | Marshrut varag'i chop etiladi, yuk ortiladi | 🚚 Reys — manzillarni ko'radi |
| 09:00 | (yoki ofisdan Yo'lga chiqarish) | ▶️ Yo'lga chiqish — mijozlarga xabar |
| Kun davomida | Reys sahifasida natijalar, rasm/imzo; qisman/rad xabarlari | Manzil → Yetkazildi (rasm) / Imzo / qisman / Rad, 💵 To'lov, ⛽ Xarajat |
| Qaytgach | Omborchi qaytgan tovarni sanaydi, logist Reysni yopish (qaytgan miqdor bilan) | 🏁 Reysni yakunlash, qaytgan tovarni omborga topshiradi; 💵 Kassa → 📤 Kassaga topshirish |
| Keyin | Xarajatlar → Moliyaga; kassir naqdni qabul qiladi; Farq dalolatnomasi imzolanadi | — |

## 7.12 Ovozli buyurtmani yoqish

**Kim:** Admin   **Qayerda:** server (bot sozlamasi) + Distributsiya → Mashinalar → Yetkazish sozlamalari

1. **Bot serverida** nutqni aniqlash kaliti (Groq) sozlanadi — `Bot__Voice__GroqApiKey` (tizim administratori). Kalit ERP bazasida va sahifalarda saqlanmaydi.
2. **ERP'da** **Botda ovozli buyurtma** belgisini yoqing va **Saqlash**.
3. Agentga tushuntiring (6.4.7): ovozli xabar 90 soniyagacha, mijoz nomi + tovar + miqdor + birlik aytiladi.

Qanday ishlaydi: ovoz matnga o'giriladi (qurilish mollari lug'ati bilan), matndan mijoz va qatorlar ajratiladi, ERP katalogdan tovarni topadi (kirill/lotin va o'/g' farqiga chidamli; `M400`, `60x60` kabi belgilar muhim), birlikni (qop, quti, dona, kg, m², мешок, коробка ...), mijoz narxini qo'yadi va qoralamani ko'rsatadi. Mijoz faqat agent ko'radigan mijozlar orasidan qidiriladi.

| Holat | Bot javobi |
|---|---|
| ERP'da o'chiq | 🎤 Ovozli buyurtma o'chirilgan. |
| Serverda kalit yo'q | 🎤 Ovozli buyurtma sozlanmagan (nutqni aniqlash kaliti yo'q). |
| Ovozli xabarni agent emas (masalan ekspeditor) yuborgan | 🎙 Ovozli buyurtma faqat agentlar uchun. |

> **Maslahat:** Tovar nomlarini katalogdagidek, artikul belgilari bilan ayting ("sement M400", "kafel 30 ga 30"). Ovozli buyurtma oddiy buyurtma kabi ofis tasdig'iga tushadi — xato tovar chiqsa, ofis yoki agent Mini App'da tuzatadi.

## 7.13 Mijozga xabarlar va nakladnoy PDF

Mijoz Telegram'ga ulangan bo'lsa (6.3.2), yetkazish jarayonida avtomatik xabar oladi:

| Hodisa | Qachon | Xabar |
|---|---|---|
| Jo'natildi | Jo'natish o'tkazilganda (ofis yoki omborchi Mini App); o'zi olib ketishda — o'rniga bitta "yetkazildi" xabari | `Buyurtmangiz D-... jo'natildi: ... so'm. Joriy qarzingiz: ... so'm.` |
| Yo'lga chiqdi | Reys yo'lga chiqqanda | `🚚 Buyurtmangiz yo'lga chiqdi: ...` + ekspeditor, telefon, mashina |
| Yetkazildi | Manzil "Yetkazildi" | `✅ Buyurtma ... yetkazildi (DJ-...): ... so'm. Nakladnoy ilova qilingan.` + PDF |
| Qisman | Manzil "Qisman" | `✅ Buyurtma ... qisman yetkazildi (...): ... so'm (... so'mdan). Sabab: .... Nakladnoy ilova qilingan.` + PDF |
| Rad etildi | Manzil "Rad etildi" | `🚫 Buyurtma ... qabul qilinmadi. Sabab: ....` |
| To'lov | Agent yoki ekspeditor to'lov olganda | `To'lovingiz qabul qilindi: ... so'm (...). Qolgan qarz: ... so'm.` |
| Natija qaytarildi (↺) | Ofis natijani bekor qilganda, natija xabari allaqachon ketgan bo'lsa | `↺ yetkazish natijasi bekor qilindi va qayta kiritiladi` |

Xabarlar mijozning Telegram tilida (o'zbek yoki rus) yuboriladi.

**Nakladnoy PDF** (`Nakladnoy_DJ-2026-00031.pdf`) — 6.8.2-bo'limda tasvirlangan; qisman yetkazishda faqat yetkazilgan miqdorlar bilan va "(qisman yetkazilgan)" belgisi. PDF'ni bot serveri tayyorlaydi; tayyorlab bo'lmasa xabar matn sifatida ketadi.

> **Eslatma:** Mijoz ulanmagan bo'lsa xabar yuborilmaydi va keyin ham yuborilmaydi — ulangandan keyingi hodisalar haqida xabar oladi.

## 7.14 Sozlamalar jamlanmasi

| Sozlama | Qayerda | Ta'siri |
|---|---|---|
| Mashina: yuk ko'tarish, hajm | Mashinalar | Reys sig'imi tekshiruvi |
| Mashina: odatiy ekspeditor, ombor | Mashinalar | Reysda avtomatik to'ldirish |
| Yandex Maps kaliti | Mashinalar → Yetkazish sozlamalari | Jonli xarita (bo'lmasa — havolalar) |
| Botda ovozli buyurtma | Mashinalar → Yetkazish sozlamalari | Ovozli buyurtma ruxsati (+ bot serveri kaliti) |
| Omborlar koordinatasi | Mashinalar → Yetkazish sozlamalari | Marshrut boshi, Tartiblash, masofa |
| Bot ochiq manzili | Sozlamalar | Yetkazish rasmi, imzo, selfi ERP'da ko'rinadi |
| Telegram bot nomi | Sozlamalar | Ulash havolalari |
| Agent to'lovlari | Sozlamalar | Agent va ekspeditor qabul qiladigan to'lov usullari va hisoblar |
| Tashrif radiusi, m | Sozlamalar | "Keldim" ruxsat masofasi |
| Qaytarish sabablari | Ma'lumotnomalar → Sabablar (Qaytarish) | Qisman/rad sabablari ro'yxati |
| Tovar og'irligi/hajmi | Tovarlar | Reys yuklamasi hisobi |
| Mijoz koordinatasi | Mijozlar | Xarita, tartib, navigatsiya, "Keldim" |

## 7.15 Tez-tez uchraydigan holatlar

| Holat | Sababi va yechim |
|---|---|
| Jo'natish "Jo'natishlar (reyssiz)" ro'yxatida yo'q | Jo'natish o'tkazilmagan, "o'zi olib ketadi", boshqa ombordan, buyurtma yetkazilgan/bekor yoki allaqachon boshqa (shu jumladan yopilgan) reysda. Reys omborini tekshiring |
| Reysda og'irlik 0 kg | Tovar kartasida og'irlik kiritilmagan |
| "Og'irlik ... mashina sig'imidan oshdi" | Jo'natishni olib tashlang yoki boshqa mashina |
| Ekspeditor ro'yxatda yo'q | Jamoalar sahifasida "Ekspeditor" belgisi va faollikni tekshiring |
| Ekspeditor "Sizga hozircha reys biriktirilmagan." deydi | Unga yo'ldagi yoki rejalangan reys yo'q (reys boshqa ekspeditorga berilgan yoki bekor qilingan). Rejalangan reys sanasi o'tib ketgan bo'lsa ham ko'rinadi |
| Ekspeditor reysni yakunladi, lekin reys "Yo'lda" | Shunday bo'lishi kerak — ombor qaytgan tovarni sanagach, reysni ofis **Reysni yopish** bilan yopadi (7.8) |
| Ekspeditor natija kirita olmaydi ("Reys yo'lda emas.") | Reys yo'lga chiqarilmagan |
| Tasdiq ustunida rasm o'rniga 📷 / ✍️ belgisi | Sozlamalarda "Bot ochiq manzili" bo'sh |
| Reysni yopib bo'lmaydi | Kutayotgan manzil bor — natija kiriting |
| Buyurtma kartasida "Yetkazildi": "Buyurtma reysda (R-…) — yetkazish natijasi reys orqali kiritiladi" | Buyurtma ochiq reysda — natijani reys sahifasida (7.7.3) yoki ekspeditor botda kiriting |
| Xaritada nuqtalar yo'q | Mijozlarda koordinata yo'q yoki Yandex kaliti noto'g'ri |
| Rad etilgan buyurtmani qayta yetkazish kerak | Yopilgan reysdagi jo'natish boshqa reysga qayta qo'shilmaydi — tovar qaytarish hujjati bilan omborga qaytgan. Reys sahifasida **Qayta yetkazish** — qaytgan miqdorlar bilan yangi buyurtma qoralamasi (7.8.3) |
