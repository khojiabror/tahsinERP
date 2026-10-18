# 6. Sotuv (CRM)

Sotuv moduli iPack'ning avtokomponent zavodlari (UzAutoMotors Powertrain, Avtokomponent, UzSaemyung Co va boshqalar) bilan butun ishini bir joyda yuritadi: mijoz → shartnoma → buyurtma va yetkazish jadvali → sotuv invoysi → TTNK va hisob-faktura → hisobotlar. Modul bilan asosan **Savdogar**, **Xaridchi+Savdogar**, **Savdo+Ishlab chiqarish** rollari ishlaydi. Rahbar va **Ta'sischi** dashboard va hisobotlarni ko'radi.

Modul boshqa modullar bilan shunday bog'langan:
- **Ombor:** sotuv invoysi "Savdo ombori"dan avtomatik chiqim qiladi.
- **Moliya:** invoys mijoz qarzi sifatida avtomatik yoziladi.
- **Ishlab chiqarish:** tayyor mahsulot "Ishlab chiqarishdan sotuvga" o'tkazmasi bilan Savdo omboriga keladi.
- **Logistika:** yo'l varaqasi invoys yoki TTNK'ga bog'lanadi.

Butun bob davomida bitta misol ishlatiladi. **Namunaviy ssenariy (Noyabr 2026):** AJ "UzAutoMotors Powertrain" iPack'dan buyurtma qiladi:
- 1 200 dona **iPack P1 "Yog'och taglik"**, narxi 185 000 so'm/dona;
- 3 000 dona **iPack B6.1 "B6.1 quti"**, narxi 14 500 so'm/dona.

QQS 12%, mahsulot 3 partiyada yetkaziladi. Shartnoma va buyurtma raqamlari, sanalar — misol.

## 6.1 Asosiy tushunchalar

- **Xaridor (mijoz)** — iPack mahsulot sotadigan, shartnoma tuzilgan korxona (masalan AJ "UzAutoMotors Powertrain"). Shartnoma, buyurtma, invoys, qarz va Akt-sverki faqat xaridorga bog'lanadi.
- **Ma'lumotnoma (mijozlar bazasi)** — potentsial mijozlar ro'yxati: hali shartnoma tuzilmagan zavodlar va do'konlar, ularning aloqa shaxsi va qaysi mahsulotga qiziqishi. Bu shunchaki marketing bazasi, hujjatlarga bog'lanmaydi.
- **Sotuv shartnomasi** — mijoz bilan kelishilgan mahsulotlar, miqdor, narx (QQSsiz), QQS foizi, valyuta va muddat. Keyingi hujjatlardagi narx va QQS shu yerdan olinadi.
- **Sotuv buyurtmasi** — shartnoma doirasidagi aniq buyurtma (masalan noyabr partiyasi). Miqdori shartnomaning qolgan miqdoridan oshmaydi.
- **Yetkazib berish rejasi (jadvali)** — buyurtma qaysi sanada qancha miqdorda yetkazilishi, ya'ni partiyalar.
- **Sotuv invoysi** — haqiqiy jo'natma hujjati. Saqlanganda bir vaqtda uch narsa yoziladi: Savdo omboridan chiqim, mijoz qarzi (Moliya) va hisobotlardagi sotuv.
- **TTNK** — tovar-transport yuk xati. Invoys(lar) asosida tuziladi, unda mashina, haydovchi va yuk ma'lumotlari bo'ladi.
- **Hisob-faktura (SF, Счёт-фактура)** — soliq hujjati. Invoys qatorlaridan tuziladi.
- **Valyuta kursi** — 1 birlik valyutaning so'mdagi qiymati (CBU kursi yoki qo'lda kiritilgan kurs). CRM hisobotlari barcha summalarni hujjat sanasidagi kurs bilan so'mga o'giradi.
- **Bekor qilingan invoys (CANCELED)** — o'chirilmaydi, lekin ombor chiqimi qaytariladi, moliyaviy yozuvi olib tashlanadi. Bunday invoys qarzga ham, hisobotlarga ham kirmaydi.

## 6.2 Xaridorlar

**Kim:** Savdogar   **Menyu:** Sotuvlar (CRM) → Sotuvlar → Xaridorlar

Shartnoma tuzishdan oldin mijoz shu ro'yxatda bo'lishi kerak. Ro'yxat 25 qatordan sahifalanadi, ustida qidiruv maydoni bor va Excel'ga eksport qilinadi (eksportga filtrga mos barcha qatorlar tushadi).

1. "Yangi" tugmasini bosing.
2. Maydonlarni to'ldiring (jadvalga qarang) va saqlang.
3. Ro'yxatda qidiruvga `Powertrain` deb yozib, mijoz qo'shilganini tekshiring.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Nomi | Korxonaning to'liq nomi | AJ "UzAutoMotors Powertrain" |
| DUNS | INN (STIR) — SF va TTNK'da ishlatiladi | 200 000 001 (misol) |
| Turi | Erkin matn: mijoz turi | Avtokomponent zavodi |
| Mamlakat / Shahar / Manzil | Yuridik manzil | O'zbekiston / Toshkent / ... |
| Telefon raqami, E-mail | Aloqa ma'lumotlari | +998 ... |
| Aloqa uchun hodim | Ta'minot bo'limidagi mas'ul shaxs | Ta'minot bo'limi boshlig'i |
| Direktor ismi | Rahbar F.I.Sh. | — |

**Ko'rish (Details)** sahifasida mijozning shartnomalari, buyurtmalari va invoyslari soni chiqadi. Har bir son tugma bo'lib, bosilsa shu mijozga filtrlangan ro'yxat ochiladi.

> **Diqqat:** Xaridor faqat "Xaridorlar" jadvaliga yoziladi. U Ta'minot (SRM) bo'limidagi ta'minotchilar ro'yxatida ko'rinmaydi. Bir korxona ham sotuvchi, ham xaridor bo'lsa, uni ikkala bo'limda alohida kiriting.

> **Diqqat:** O'chirish yumshoq (soft delete) bajariladi. O'chirilgan mijoz yangi hujjatlarda tanlanmaydi, lekin eski shartnoma va invoyslarda nomi bilan qoladi.

**Natija:** Mijoz shartnoma, buyurtma va invoys formalarida "Mijoz/Haridor" ro'yxatida chiqadi. Moliya'da esa "Kontragent turi = Mijoz" ro'yxatida chiqadi.

## 6.3 Ma'lumotnoma (mijozlar bazasi)

**Kim:** Savdogar   **Menyu:** Sotuvlar (CRM) → Sotuvlar → Ma'lumotnoma

Bu ro'yxatga iPack'ning yangi qadoq taklif qilmoqchi bo'lgan korxonalari yoziladi. Masalan, karton quti olishi mumkin bo'lgan yangi zavod. Sahifada "Maxsulot turi" bo'yicha filtr bor.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Korxona / Do'kon | Nomi | MChJ QK "UzDongyang Co" (agar hali shartnoma bo'lmasa) |
| Maxsulot turi | Qiziqqan mahsulot guruhi | Karton quti |
| Davlat / Viloyat / Region | Joylashuvi | O'zbekiston / Andijon / Asaka |
| Aloqa uchun / Lavozimi / Telefon | Muloqot qilinadigan shaxs | Ta'minot menejeri |
| Izoh | Muzokara holati | "B7 namunasi yuborildi" |

> **Maslahat:** Ma'lumotnomadagi korxona shartnoma imzolashga tayyor bo'lgach, uni **Xaridorlar** ro'yxatiga alohida qo'shing. Ikki ro'yxat avtomatik birlashtirilmaydi.

**Natija:** Faqat ma'lumot saqlanadi. Ombor, Moliya va hisobotlarga ta'siri yo'q.

## 6.4 Sotuv shartnomasi

**Kim:** Savdogar   **Menyu:** Sotuvlar (CRM) → Sotuvlar → Shartnomalar

Shartnoma narx va QQS uchun "manba hujjat" hisoblanadi. Buyurtma va invoysdagi narx hamda QQS % shu yerdan olinadi va u yerda qo'lda o'zgartirilmaydi.

1. "Yangi" tugmasini bosing va sarlavhani to'ldiring.
2. "Shartnoma mahsulotlari" bo'limiga har mahsulot uchun qator qo'shing. Mahsulot maydoniga kodning bir qismini yozing (masalan `P1`) va ro'yxatdan tanlang.
3. Miqdor, Narx (QQSsiz) va QQS (%) ni kiriting. "Jami narx" va sarlavhadagi "Jami summa" avtomatik hisoblanadi.
4. Saqlang va Details sahifasida summani tekshiring.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Shartnoma raqami | Qog'ozdagi raqam | S-2026/045 (misol) |
| Sana | Imzolangan sana | 01.11.2026 |
| Mijoz | Xaridor | AJ "UzAutoMotors Powertrain" |
| Valyuta | Shartnoma valyutasi | UZS |
| Muddat | Shartnoma amal qilish oxiri (majburiy) | 31.12.2026 |
| Incoterms | Yetkazish sharti (ixtiyoriy) | DAP Asaka |
| To'lov shartlari | Erkin matn | "Har partiyadan keyin 10 bank kuni ichida" |
| Mahsulot / Miqdor / Narx / QQS (%) | Qatorlar | P1: 1 200 × 185 000, 12%; B6.1: 3 000 × 14 500, 12% |

Ssenariy bo'yicha hisob:

| Mahsulot | Summa (QQSsiz) | QQS 12% | Jami |
|---|---|---|---|
| iPack P1 | 222 000 000 | 26 640 000 | 248 640 000 |
| iPack B6.1 | 43 500 000 | 5 220 000 | 48 720 000 |
| **Shartnoma jami** | **265 500 000** | **31 860 000** | **297 360 000 so'm** |

**Shartnoma ichidagi amallar:**
- **Tahrirlash (Edit):** qator qo'shish va olib tashlash. O'rtadagi qatorni olib tashlasangiz, qolganlari to'g'ri saqlanadi.
- **Mahsulot qatorini o'chirish:** savat belgisi va tasdiqlash orqali bajariladi. Mahsulot shu shartnoma bo'yicha buyurtma yoki invoysda ishlatilgan bo'lsa, o'chirilmaydi. Bunday holda "... mahsuloti shu shartnoma bo'yicha buyurtmada ishlatilgan — uni shartnomadan o'chirib bo'lmaydi" xabari chiqadi.
- **Buyurtmalarni boshqarish (ManageOrders):** buyurtmani shartnomaga bog'lash yoki uzish.
- **Details** sahifasida quyidagilar bor:
  - bog'langan buyurtmalar;
  - "Buyurtmalar" va "Invoyslar" tugmalari;
  - **"Akt-sverki (ushbu shartnoma)"** tugmasi — shartnoma bo'yicha mijoz bilan solishtirma (8-bobga qarang).
- **O'chirish:** yumshoq o'chirish. Eski hujjatlardagi bog'lanish saqlanadi.

> **Diqqat:** Narxni **QQSsiz** kiriting. QQS alohida "QQS (%)" ustunida beriladi. Narxni QQS bilan yozsangiz, QQS ikki marta hisoblanadi.

> **Maslahat:** Miqdor va narxni vergul bilan yozsangiz ham (`185000,00`) to'g'ri o'qiladi.

**Natija:** Shartnoma buyurtma va invoys formalarida tanlanadi. Uning qatorlari keyingi hujjatlarda narx va QQS manbai bo'ladi. CRM Dashboard'da "Jami shartnomalar" soni oshadi.

## 6.5 Sotuv buyurtmasi

**Kim:** Savdogar   **Menyu:** Sotuvlar (CRM) → Sotuvlar → Buyurtmalar

1. "Yangi" tugmasini bosing. "Haridor"ni, so'ng "Shartnoma raqami"ni tanlang. Shartnoma mahsulotlari jadvali avtomatik yuklanadi.
2. Har bir mahsulot qatoriga "Buyurtma miqdori"ni kiriting. Kerak bo'lmagan mahsulotni bo'sh qoldiring yoki "O'chirish" bilan olib tashlang.
3. Saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Buyurtma raqami | Mijoz buyurtma raqami | SO-2026-112 (misol) |
| Buyurtma sanasi | Buyurtma kelgan sana | 03.11.2026 |
| Haridor | Mijoz | AJ "UzAutoMotors Powertrain" |
| Shartnoma raqami | Shartnoma | S-2026/045 |
| Qisqacha tavsifi | Izoh | "Noyabr, 3 partiya" |
| Buyurtma miqdori | Har mahsulot | P1 = 1 200; B6.1 = 3 000 |

Jadvalda quyidagilar faqat ko'rish uchun chiqadi:
- **Shartnoma miqdori**;
- **Birlik narxi (QQS bilan)** — P1 uchun 185 000 × 1,12 = **207 200**, B6.1 uchun 14 500 × 1,12 = **16 240**;
- **Jami (QQS bilan)** — buyurtma jami **297 360 000 so'm**.

**Qoidalar (tizim tekshiradi):**
- Narx va birlik doim shartnoma qatoridan olinadi.
- Bir shartnoma bo'yicha barcha buyurtmalar miqdori shartnomaning qolgan miqdoridan oshmaydi. Masalan, P1 uchun 1 300 yozilsa, xato chiqadi. Kiritgan qiymatlaringiz formada saqlanib qoladi.
- Tahrirlashda miqdorni allaqachon invoys qilingan miqdordan kam qilib bo'lmaydi.
- Tahrirlashda miqdorni yetkazish rejasidagi miqdordan kam qilib bo'lmaydi ("... yetkazib berish rejasida 1 200,00 — miqdorni avval rejada kamaytiring").
- Rejasi bor qatorni olib tashlab bo'lmaydi.
- Faol (bekor qilinmagan) invoysi bor buyurtmani o'chirib bo'lmaydi: "Buyurtmaga faol invoys bog'langan — avval invoysni bekor qiling."

**Natija:** Buyurtma CRM Dashboard'dagi "Jami buyurtmalar"ga va "Buyurtmalar - oy bo'yicha" grafigiga kiradi. Invoys qilinmaguncha "Invoys yaratilmagan buyurtmalar" jadvalida turadi.

## 6.6 Yetkazib berish rejasi (partiyalar)

**Kim:** Savdogar, Savdo+Ishlab chiqarish   **Menyu:** Buyurtmalar → buyurtma Details → "Yetkazib berish rejasi"

Rejada buyurtma qaysi kuni qancha jo'natilishi belgilanadi. Ishlab chiqarish rejasini tuzishda shu sanalarga qarang.

1. Buyurtma Details sahifasida "Yetkazib berish rejasi" tugmasini bosing.
2. Pastdagi jadvalga har partiya va har mahsulot uchun qator qo'shing: Maxsulot, Yetkazib berish sanasi, Miqdor.
3. Saqlang. "Yetkazib berish rejasi muvaffaqiyatli saqlandi" xabari chiqadi.

Ssenariy (misol):

| Partiya | Yetkazib berish sanasi | iPack P1 | iPack B6.1 |
|---|---|---|---|
| 1 | 10.11.2026 | 400 | 1 000 |
| 2 | 20.11.2026 | 400 | 1 000 |
| 3 | 30.11.2026 | 400 | 1 000 |
| **Jami** | | **1 200** | **3 000** |

Yuqoridagi "Buyurtma maxsulotlari" jadvalida har mahsulot bo'yicha quyidagilar ko'rinadi: Buyurtma miqdori, Rejalashtirilgan, Yetkazilgan, Qolgan.

> **Diqqat:** Rejalashtirilgan jami buyurtma miqdoridan oshsa, saqlanmaydi. Masalan, P1 ga 4-qator qo'shib jami 1 300 bo'lsa: "iPack P1 - Rejalashtirilgan miqdor (1 300,00) buyurtma miqdoridan (1 200,00) oshib ketdi".

> **Diqqat:** Qaysi qatorlarni o'zgartirib yoki o'chirib bo'lmaydi:
> - yetkazilgan miqdori bor qatorni o'chirib bo'lmaydi;
> - sifat nazorati bog'langan qatorni o'chirib bo'lmaydi;
> - reja miqdorini yetkazilgan miqdordan kam qilib bo'lmaydi.

> **Diqqat:** "Yetkazilgan" (QtyShipped) ustunini hozircha hech qaysi hujjat avtomatik to'ldirmaydi: invoys yaratilganda u o'zgarmaydi. Haqiqiy jo'natmani **invoyslar** bo'yicha kuzating. Dashboard'dagi "Yetkazib berish kechikkan" ro'yxatiga rejadagi sanasi o'tgan barcha partiyalar tushadi.

**Natija:** CRM Dashboard'da "Yetkazib berish kechikkan" soni va jadvali (Buyurtma, Mahsulot, Reja sana, Yuborilgan, Kechikish) shu rejadan hisoblanadi.

## 6.7 Sotuv invoysi

**Kim:** Savdogar   **Menyu:** Sotuvlar (CRM) → Sotuvlar → Invoyslar → "Invoys yaratish"

Invoys — modulning eng muhim hujjati. U saqlanganda **bitta tranzaksiyada** to'rt narsa yoziladi:
1. invoys sarlavhasi va qatorlari;
2. har qator uchun **Savdo omboridan** chiqim (sof FIFO bo'yicha);
3. Moliya'da mijoz qarzi yozuvi;
4. invoys raqami.

Biror qadamda xato bo'lsa, hech narsa yozilmaydi: yarim invoys yoki qisman chiqim qolmaydi.

**Oldin bajarilgan bo'lishi kerak:** tayyor mahsulot Savdo omborida bo'lishi kerak. Mahsulot sexdan "Ombor → Operatsiyalar → Ishlab chiqarishdan sotuvga" o'tkazmasi bilan keladi.

1. "Invoys yaratish"ni bosing. Yana bir yo'l bor: CRM Dashboard'dagi "Invoys yaratilmagan buyurtmalar" jadvalida "Invoys yaratish" tugmasini bossangiz, forma buyurtma bilan ochiladi.
2. Mijoz → Shartnoma → (ixtiyoriy) Buyurtma tanlang. Valyuta shartnomadan olinadi.
3. Buyurtma tanlansa, uning mahsulotlari qatorlarga yuklanadi. Partiya miqdoriga moslab tuzating.
4. Mahsulot ro'yxatida faqat Savdo omborida qoldig'i bor tayyor mahsulotlar chiqadi.
5. "Birlik narxi" va "QQS %" shartnomadan avtomatik to'ladi va o'zgartirilmaydi.
6. "Invoys raqami"ni bo'sh qoldiring va saqlang.

| Maydon | Nima kiritiladi | iPack misoli (1-partiya) |
|---|---|---|
| Invoys raqami | Bo'sh — avtomatik beriladi | (avtomatik) |
| Invoys sanasi * | Jo'natish sanasi | 10.11.2026 |
| Mijoz * | Xaridor | AJ "UzAutoMotors Powertrain" |
| Shartnoma * | Shartnoma | S-2026/045 |
| Buyurtma | Buyurtma | SO-2026-112 |
| Valyuta * | Shartnomadan | UZS |
| Mahsulot / Miqdor | Partiya miqdori | P1 — 400; B6.1 — 1 000 |
| Birlik narxi / QQS % | Shartnomadan (faqat o'qish) | 185 000 / 12; 14 500 / 12 |
| Tavsif | Ixtiyoriy | "1-partiya" |

1-partiya invoysi bo'yicha hisob:

| Qator | Summa (QQSsiz) | QQS 12% | Jami |
|---|---|---|---|
| P1: 400 × 185 000 | 74 000 000 | 8 880 000 | 82 880 000 |
| B6.1: 1 000 × 14 500 | 14 500 000 | 1 740 000 | 16 240 000 |
| **Invoys jami** | **88 500 000** | **10 620 000** | **99 120 000 so'm** |

2- va 3-partiyalar xuddi shunday tuziladi: sanalari 20.11 va 30.11, summasi har birida 99 120 000 so'm. Uch invoysning yig'indisi 297 360 000 so'm, ya'ni shartnoma summasiga teng.

**Raqamlash:**
- Raqam oxirgi invoysning uslubi bo'yicha davom ettiriladi: prefiks va raqam uzunligi oxirgi invoysdan olinadi, raqam +1 bo'ladi.
- Bekor qilingan invoyslarning raqamlari ham hisobga olinadi, shuning uchun ular qayta berilmaydi.
- Ikki foydalanuvchi bir vaqtda saqlasa ham raqam takrorlanmaydi.
- Raqamni qo'lda yozsangiz va u band bo'lsa, xato chiqadi: "... raqamli invoys allaqachon mavjud. Raqamni bo'sh qoldiring — avtomatik beriladi."

**Qoldiq yetmasa:**
- Invoys umuman saqlanmaydi. Xato chiqadi: "Mahsulot iPack P1 uchun sotuv omborida yetarli qoldiq yo'q. Mavjud: 350,00, Talab: 400,00".
- Formadagi qatorlar yo'qolmaydi.
- Bir mahsulot bir necha qatorda bo'lsa, qoldiq ularning yig'indisi bo'yicha tekshiriladi.
- Ikki yo'l bor: miqdorni kamaytirish yoki ishlab chiqarishdan sotuvga o'tkazmani kutish.

> **Diqqat:** Invoysda faqat tanlangan shartnomadagi (buyurtma tanlansa — buyurtmadagi) mahsulotlarni sotish mumkin. Boshqa mahsulot qo'shilsa: "Mahsulot ... tanlangan shartnomada mavjud emas."

**Natija (1-partiya saqlangach):**
- **Ombor:** Savdo omboridagi P1 qoldig'i 400 ga, B6.1 qoldig'i 1 000 ga kamayadi. Chiqim hujjati tavsifida invoys raqami bo'ladi.
- **Moliya:** "Tranzaksiyalar"da yangi yozuv paydo bo'ladi:
  - Yo'nalish = Kirim, Summa = 99 120 000 (QQS bilan), Valyuta = UZS;
  - Hisob = "Debitorlar qarzi" (UZS), Kategoriya = "Sotuv..." (nomida "Sotuv" bor kirim kategoriyasi);
  - Kontragent = UzAutoMotors Powertrain;
  - Maqsad = "Sotuv invoysi #<raqam>".
- **Qarz:** mijoz qarzi 99 120 000 ga oshadi. U Balans, Majburiyatlar dashboardi, CRM Dashboard va Akt-sverkida ko'rinadi.
- **Hisobotlar:** sotuv hisobotlarida QQSsiz 88 500 000 bo'lib ko'rinadi.

> **Diqqat:** Moliya yozuvi **invoys valyutasida** yoziladi. UZS invoys so'mdagi "Debitorlar qarzi" hisobiga, USD invoys esa USD "Debitorlar qarzi" hisobiga tushadi. Bu yozuv pul harakati emas, faqat mijoz qarzining "ko'zgusi". U kassa oqimiga, kassa qoldig'iga va P&L'ga kirmaydi.

### 6.7.1 Invoysni ko'rish va chop etish

Details sahifasida quyidagilar bor:
- sarlavha: mijoz, buyurtma va shartnoma havolalari, holat, valyuta;
- qatorlar: birlik narxi, qator summa, QQS summa, jami;
- tugmalar: **"Chop etish"**, **"Bekor qilish"**, **"SF (soni)"**, **"TTNK (soni)"**, **"SF yaratish"**, **"TTNK yaratish"**.

"Chop etish" invoysni bosma shaklda yangi oynada ochadi.

### 6.7.2 Invoysni tahrirlash

1. Invoyslar ro'yxatida yoki Details'da "Tahrirlash"ni bosing. Har qatorda saqlangan mahsulot tanlangan bo'ladi.
2. Miqdorni o'zgartiring va saqlang.

**Natija:**
- Avvalgi ombor chiqimi qaytariladi va yangisi yoziladi.
- Moliyadagi mavjud yozuv **yangilanadi** (yangi yozuv qo'shilmaydi).
- Masalan, P1 400 → 390 bo'lsa: ombor qoldig'i 10 ga ortadi, moliya yozuvi 10 × 185 000 × 1,12 = 2 072 000 ga kamayadi.

> **Diqqat:** Quyidagi hollarda tahrirlash cheklanadi:
> - Invoys asosida SF yoki TTNK yaratilgan bo'lsa, qatorlarni o'zgartirib bo'lmaydi: "Bu invoys qatorlari asosida SF/TTNK yaratilgan — qatorlarni o'zgartirish mumkin emas...". Faqat sarlavhani tahrirlash mumkin.
> - To'langan (PAID), yakunlangan (FINALIZED) yoki bekor qilingan (CANCELED) invoysni umuman tahrirlab bo'lmaydi.

### 6.7.3 Invoysni bekor qilish

1. Details'da yoki ro'yxatda "Bekor qilish" tugmasini bosing.
2. "Invoysni bekor qilish" sahifasida "Ha, bekor qilish" tugmasini bosing.

**Natija (bitta tranzaksiyada):**
- holat CANCELED bo'ladi;
- Savdo omboriga mahsulot qaytadi;
- moliyadagi "Sotuv invoysi #..." yozuvi olib tashlanadi;
- invoys mijoz qarziga, Akt-sverkiga, dashboard va hisobotlarga kirmaydi;
- raqami keyingi invoysga berilmaydi.

> **Diqqat:** To'langan (PAID) invoysni bekor qilib bo'lmaydi. Agar invoysdan SF yoki TTNK yaratilgan bo'lsa, bekor qilishdan oldin ularni tekshiring: hujjatlar soliq tizimiga yuborilgan bo'lishi mumkin.

### 6.7.4 Holatlar

| Holat | Ma'nosi | Qanday paydo bo'ladi | Nima qilish mumkin |
|---|---|---|---|
| APPROVED (tasdiqlangan) | Faol invoys: ombor chiqimi va moliya yozuvi bor | Yaratish yoki tahrirlashdan keyin | Tahrirlash, bekor qilish, SF/TTNK yaratish |
| CANCELED (bekor qilingan) | Bekor qilingan, hisobga kirmaydi | "Bekor qilish" | Faqat ko'rish |
| PAID / FINALIZED | To'langan / yakunlangan | Eski yoki import ma'lumotlarda | Faqat ko'rish (PAID'ni bekor qilib bo'lmaydi) |
| DRAFT | Eski import qilingan qoralama (asosan 2025 yil) | Import | Ombor va moliyaga yozilmagan. Ular bo'yicha qaror buxgalteriya bilan alohida qabul qilinadi |

Invoyslar ro'yxati 25 qatordan server tomonda sahifalanadi. Filtrlar: Mijoz, Buyurtma, Shartnoma, sana, holat, qidiruv. Excel eksportga filtrga mos barcha qatorlar tushadi.

## 6.8 TTNK (tovar-transport yuk xati)

**Kim:** Savdogar, logist   **Menyu:** Invoys Details → "TTNK yaratish"; ro'yxat: Sotuvlar (CRM) → Sotuvlar → ТТНК

1. Invoys Details sahifasida "TTNK yaratish" tugmasini bosing. Qatorlar invoysdan yuklanadi.
2. "TTNK raqami"ni bo'sh qoldiring: **TTNK-2026-0001** ko'rinishida avtomatik beriladi.
3. Yuk tashuvchi, transport, marshrut va yuk ma'lumotlarini kiriting va saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| TTNK sanasi * | Jo'natish sanasi | 10.11.2026 |
| Yuk tashuvchi nomi / INN | Ekspeditor | "Osiyo Trans Kargo" MChJ |
| Yuk beruvchi nomi / INN | iPack | "iPack Eco Solutions" MChJ |
| Yuk oluvchi nomi / INN | Mijoz | AJ "UzAutoMotors Powertrain" |
| Haydovchi / Haydovchi PINFL | Haydovchi | — |
| Mashina raqami / modeli, Pritsep | Transport | 60 A 123 BC (misol) |
| Jo'nash manzili / viloyati | Ombor manzili | Andijon |
| Yetkazish manzili / viloyati, Masofa (km) | Mijoz zavodi | Asaka |
| Yuk og'irligi (brutto/netto, t) | Partiya og'irligi | Misol: 6,2 / 5,8 |
| Yetkazib berish narxi, QQS summa, Valyuta | Tashish narxi (ixtiyoriy) | — |
| Qatorlar: HS Kod, O'lchov birligi, Og'irlik, Joylar soni, Qadoq turi | Har mahsulot | P1: 400 dona, 40 joy (misol) |

> **Diqqat:** Bir TTNK bir nechta invoysdan tuzilishi mumkin, lekin barchasi **bitta mijozga** tegishli bo'lishi kerak. Bekor qilingan invoysdan TTNK tuzilmaydi.

**Natija:** TTNK "DRAFT" holatida saqlanadi va invoys Details'dagi "TTNK (soni)" tugmasida ko'rinadi. Bundan keyin invoys qatorlarini o'zgartirib bo'lmaydi. TTNK ombor yoki moliyaga yozilmaydi. Tashish xarajati Logistika moduli orqali, yo'l varaqasi to'lovi sifatida yoziladi.

## 6.9 Hisob-faktura (SF)

**Kim:** Savdogar, buxgalter   **Menyu:** Invoys Details → "SF yaratish"; ro'yxat: Sotuvlar (CRM) → Sotuvlar → Счёт-фактура

1. Invoys Details sahifasida "SF yaratish"ni bosing. "SF qatorlari (Invoys qatorlaridan)" avtomatik to'ladi.
2. "SF raqami"ni bo'sh qoldiring: **SF-2026-0001** ko'rinishida beriladi.
3. "SF sanasi", "Ta'minotchi INN" (iPack INN) va "Mijoz INN" (UzAutoMotors Powertrain INN) ni kiriting va saqlang.

| Maydon | iPack misoli |
|---|---|
| SF sanasi * | 10.11.2026 |
| Ta'minotchi INN * | iPack Eco Solutions INN |
| Mijoz INN * | Mijoz kartasidagi DUNS |
| Qatorlar | P1 400 × 185 000, 12%; B6.1 1 000 × 14 500, 12% |

Summalar brauzerda emas, serverda invoysdan hisoblanadi: 88 500 000 / QQS 10 620 000 / jami 99 120 000.

> **Diqqat:** Bir invoys uchun faqat bitta SF yaratiladi. Takrorlansa: "Bu invoys uchun SF allaqachon yaratilgan". SF qatorlari invoys qatorlariga mos bo'lishi shart.

### 6.9.1 Elektron hujjat (EDO)

SF va TTNK Details sahifalarida "Elektron hujjat (EDO)" paneli bor.
- Hujjat har doim tahsinERP ichida saqlanadi. Bu holat "Ichki (tizimda)" deb ko'rinadi.
- Administrator tashqi provayderni (Didox yoki Faktura.uz) sozlagan bo'lsa, panelda "... ga yuborish" tugmasi chiqadi.
- Yuborishdan oldin majburiy maydonlar to'ldirilmagan bo'lsa, panelda "Yuborishdan oldin to'ldirish kerak" ro'yxati chiqadi.
- "E-hujjat ma'lumotlari (JSON)" tugmasi yuboriladigan ma'lumotni ko'rsatadi.

| EDO holati | Ma'nosi |
|---|---|
| Ichki (tizimda) | Faqat tahsinERP'da |
| Yuborilgan | Provayderga yuborildi, javob kutilmoqda |
| Qabul qilingan | Kontragent yoki soliq tomonidan imzolangan |
| Rad etilgan / Xato | Qayta tekshirib yuborish kerak |

Holatni yangilash uchun panelda yangilash tugmasi bor (tashqi ID bo'lganda chiqadi).

## 6.10 Valyuta va CBU kurslari

**Kim:** Savdogar, buxgalter   **Kirish:** CRM Dashboard yuqorisidagi "valyuta kurslari" havolasi (yoki manzil satriga `/CurrencyRates`)

iPack'da asosiy valyuta — UZS. Mijoz bilan USD, RUB yoki KRW'da shartnoma tuzilishi mumkin. CRM dashboard va hisobotlari bunday summalarni **hujjat sanasidagi** kurs bilan so'mga o'giradi.

Kurs quyidagi tartibda qidiriladi:
1. kurslar jadvali (qo'lda kiritilgan yoki avval CBU'dan olingan);
2. cbu.uz arxivi (topilsa jadvalga saqlanadi);
3. internet bo'lmasa — jadvaldagi eng yaqin oldingi kurs.

Sahifadagi amallar:
1. **Filtr:** Valyuta, Dan, Gacha → "Ko'rsatish". Jadvalda Sana, Valyuta, Kurs (so'm), Manba ko'rinadi.
2. **Kursni qo'lda kiritish:** valyuta = USD, sana, kurs (masalan `12800`) → "Saqlash". "Kurs saqlandi." xabari chiqadi.
3. **CBU'dan davr bo'yicha yuklash:** "Yetishmayotganlarni yuklash (≤ 92 kun)". Davr 92 kundan oshmasligi kerak.

> **Diqqat:** Hujjat kursi topilmasa, bu summa hisobotdagi jamiga qo'shilmaydi va sahifada ogohlantirish chiqadi. Bunday holda kursni qo'lda kiriting.

> **Diqqat:** Moliya modulining dashboardlari (Kassa oqimi, Balans va boshqalar) hozircha valyutalarni so'mga o'girmaydi. USD invoys moliyada USD hisobida turadi. Valyutali mijozlar qarzini CRM Dashboard va Akt-sverki orqali kuzating.

## 6.11 Muddati o'tgan hujjatlar

CRM Dashboard'da uchta "muddati o'tgan" ro'yxat bor:

| Ro'yxat | Qanday hisoblanadi |
|---|---|
| Muddati o'tgan shartnomalar | Shartnomaning "Muddat" sanasi bugundan oldin. Ustunlar: Shartnoma №, Xaridor, Muddat, Kechikish (kun) |
| Yetkazib berish kechikkan | Reja sanasi o'tgan, yuborilgan miqdor rejadan kam |
| Muddati o'tgan invoyslar | Invoysning to'lov muddati o'tgan va u to'lanmagan |

"Muddati o'tgan invoyslar" jadvali haqida batafsil:
- ustunlar: Invoys №, Xaridor, To'lov muddati, Kechikish (kun), Summa (so'm), To'langan, Qoldiq;
- to'liq yopilgan invoys bu ro'yxatga tushmaydi;
- bekor qilingan va PAID invoyslar ham tushmaydi.

> **Diqqat:** Invoys formasida to'lov muddati maydoni yo'q. Shuning uchun bu ro'yxatga faqat muddati bazada bor invoyslar (asosan import qilinganlar) tushadi. Yangi invoyslar bo'yicha to'lov nazoratini shartnoma bo'yicha Akt-sverki va "Korxonalar bo'yicha haqqimiz (Debitorlik)" jadvali orqali yuriting.

## 6.12 Hisobotlar va dashboardlar

### CRM Dashboard

**Kim:** Savdogar, rahbar, Ta'sischi   **Menyu:** Sotuvlar (CRM) → Sotuvlar → CRM Dashboard (yoki Dashboard → CRM Dashboard)

Filtrda Dan/Gacha sanalarni tanlang va "Filtrlash"ni bosing. Barcha summalar so'mda.

| Blok | Nima ko'rsatadi |
|---|---|
| Kartochkalar | Jami shartnomalar, Shartnoma muddati o'tgan, Jami buyurtmalar, Yetkazib berish kechikkan, Invoyslar (muddati o'tgan), Xaridorlar |
| Grafiklar | Shartnomalar - oy bo'yicha; Buyurtmalar - oy bo'yicha; Buyurtma bajarilishi - invoys bilan / invoysiz; Shartnoma holati (muddati o'tgan / bajarilgan / davom etayotgan); Mijozlar bo'yicha buyurtma summasi (TOP 10) |
| Korxonalar bo'yicha haqqimiz (Debitorlik) | Har mijoz va shartnoma bo'yicha qarz: QQS bilan invoyslar minus to'lovlar. Bekor qilinganlar kirmaydi |
| Invoys yaratilmagan buyurtmalar | Har qatorda "Invoys yaratish" tugmasi |
| Muddati o'tgan ro'yxatlar | 6.11 ga qarang |

iPack misoli: uch partiya invoys qilingan, mijoz 25.11.2026 da 1-partiya uchun 99 120 000 so'm to'lagan. U holda "Debitorlik" jadvalida UzAutoMotors Powertrain / S-2026/045 = 297 360 000 − 99 120 000 = **198 240 000 so'm**. Xuddi shu raqam Moliya'dagi Balans, Majburiyatlar dashboardi va Akt-sverkida ham chiqadi.

### Sotuv hisobotlari

**Menyu:** Sotuvlar (CRM) → Sotuvlar → Hisobotlar → Barcha hisobotlar / Oylik savdo hisoboti / Mijoz bo'yicha sotuv / Mahsulot bo'yicha sotuv

| Hisobot | Filtr | Ustunlar |
|---|---|---|
| Oylik savdo hisoboti | Oy, Yil | Mahsulot kodi va nomi, O'lchov, Buyurtma miqdori, Buyurtma summa, Sotilgan miqdor, Sotuv summa, Konversiya % |
| Mijoz bo'yicha sotuv | Dan, Gacha | Xaridor, Buyurtmalar soni, Buyurtma jami, Invoyslar soni, Sotuv jami |
| Mahsulot bo'yicha sotuv | Dan, Gacha | Kodi, Mahsulot nomi, O'lchov, Buyurtma miqdori, Buyurtma summa, Sotilgan miqdor, Sotuv summa, Konversiya % |

Hisobotlarni o'qish qoidalari:
- "Sotuv" ustunlari **faol** invoyslardan olinadi va QQSsiz ko'rsatiladi.
- "Mijoz bo'yicha sotuv"dagi "Buyurtma jami" buyurtmalardan QQS bilan olinadi.
- Mahsulot va oylik hisobotlardagi "Buyurtma summa" esa QQSsiz (miqdor × shartnoma narxi).
- "Konversiya %" = Sotuv summa / Buyurtma summa.
- Bekor qilingan invoyslar kirmaydi.
- Davrning oxirgi kuni hisobotga kiradi.
- Har sahifada "Summalar so'mda — har hujjat o'z sanasidagi CBU kursi bilan." yozuvi bor.

iPack misoli, 01.11–30.11.2026, uch partiya invoys qilingan:

| Hisobot / qator | Buyurtma | Sotuv (QQSsiz) | Konversiya |
|---|---|---|---|
| Mahsulot bo'yicha: iPack P1 | 1 200 dona, 222 000 000 | 1 200 dona, 222 000 000 | 100 % |
| Mahsulot bo'yicha: iPack B6.1 | 3 000 dona, 43 500 000 | 3 000 dona, 43 500 000 | 100 % |
| Mijoz bo'yicha: UzAutoMotors Powertrain | 1 buyurtma, 297 360 000 (QQS bilan) | 3 invoys, 265 500 000 | — |

Faqat 1-partiya invoys qilingan bo'lsa, P1 bo'yicha konversiya 74 000 000 / 222 000 000 ≈ 33,3 % bo'ladi.

## 6.13 Ruxsatlar

Ruxsatlar serverda tekshiriladi. Ruxsati yo'q foydalanuvchi sahifa manzilini qo'lda yozib ochsa, "Ruxsat yo'q" sahifasiga yo'naltiriladi. Masalan, Savdogar rolida "Moliya" sahifalari (`/FinTransactions`, Balans, Opening) ochilmaydi. Omborchi esa sotuv invoysi yoki shartnoma yarata olmaydi.

Rollarni sozlash 2-bobda yoziladi. Tavsiya etilgan sozlama:
- **Savdogar:** Xaridorlar, Shartnomalar, Buyurtmalar, Invoyslar, SF, TTNK — ko'rish va o'zgartirish.
- **Ta'sischi:** faqat ko'rish va dashboardlar.

## 6.14 Tez-tez uchraydigan savollar va muammolar

**Invoys saqlanmayapti: "sotuv omborida yetarli qoldiq yo'q".**
Mahsulot hali sexdan Savdo omboriga o'tkazilmagan. "Ombor → Qoldiqlar"da Savdo omborini tekshiring va "Ishlab chiqarishdan sotuvga" o'tkazmasini bajaring.

**Invoysda narx yoki QQS ni o'zgartirib bo'lmayapti.**
Bu to'g'ri: ular shartnomadan olinadi. Narx o'zgargan bo'lsa, shartnomani tahrirlang yoki qo'shimcha kelishuv bo'yicha yangi shartnoma tuzing.

**Mahsulot ro'yxatida kerakli mahsulot yo'q.**
Ikki sabab bo'lishi mumkin: mahsulotning Savdo omborida qoldig'i yo'q, yoki u tanlangan shartnomada yo'q.

**Buyurtma miqdorini kamaytirib bo'lmayapti.**
Avval yetkazib berish rejasidagi miqdorni kamaytiring. Agar shu miqdor allaqachon invoys qilingan bo'lsa, ortiqcha invoysni bekor qiling.

**Invoysni tahrirlashda "SF/TTNK yaratilgan" xatosi.**
Qatorlarni o'zgartirib bo'lmaydi, faqat sarlavhani tahrirlash mumkin. Miqdor xato bo'lsa, buxgalter bilan kelishib invoysni bekor qiling va yangisini tuzing. Bunda SF va TTNK holatini ham hisobga oling.

**Bekor qilingan invoys raqami bo'sh qoldi — bu xatomi?**
Yo'q. Raqamlar qayta berilmaydi, bu hujjat izchilligi uchun shunday qilingan.

**Mijoz qarzi CRM Dashboard'da bir xil, Moliya'da boshqacha.**
Ikkala joy bitta qoidadan foydalanadi: invoyslar QQS bilan minus to'lovlar. Farq bo'lsa, odatda to'lov kiritilganda "Kontragent" yoki "Shartnoma raqami" ko'rsatilmagan bo'ladi (8-bob, tranzaksiyalar). Shartnomasiz to'lov faqat "Barcha shartnomalar" Akt-sverkisida ko'rinadi.

**USD shartnoma summasi hisobotda chiqmayapti.**
Hujjat sanasi uchun kurs topilmagan. Valyuta kurslari sahifasida kursni qo'lda kiriting yoki CBU'dan yuklang.

**"Yetkazilgan" ustuni doim 0.**
Hozircha bu ustun avtomatik to'ldirilmaydi. Jo'natilgan miqdorni invoyslar va "Mahsulot bo'yicha sotuv" hisobotidan ko'ring.
