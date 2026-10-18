# 10. Hujjatlar va topshiriqlar

Bu bob iPack'ning ichki va tashqi hujjat aylanishi hamda topshiriqlarni nazorat qilish haqida.
**Hujjatlar** bo'limida yuritiladi:
- rasmiy xatlar: UzAutoMotors Powertrain'ga narx taklifi, Wood Nam Import'dan kelgan xat;
- korxona buyruqlari;
- korxona standartlari (STD);
- lavozim yo'riqnomalari.

Hujjatlar raqamlanadi, Word shablon orqali rasmiy blankaga chiqariladi va versiyalari saqlanadi.
**Tasks / Vazifalar** bo'limida rahbar xodimlarga topshiriq beradi, muddat va bajarilishini Kanban va dashboard orqali kuzatadi.

Kimlar foydalanadi:
- direktor va o'rinbosarlar — buyruq, tasdiqlash, topshiriq berish;
- devonxona yoki kotib — xatlarni ro'yxatga olish;
- sotuv bo'limi — narx taklifi xatlari;
- texnolog — standartlar;
- barcha xodimlar — o'z topshiriqlari.

Bog'liq modullar: Kadrlar (buyruqdagi bo'lim va javobgar xodimlar), Sozlamalar (direktor ismi Word hujjatga shu yerdan olinadi).

> **Diqqat:** Hujjatlar va Vazifalar sahifalari ruxsat bilan himoyalangan. Ruxsati yo'q foydalanuvchi menyuda "Hujjatlar" yoki "Tasks / Vazifalar" bo'limini ko'rmaydi, manzilni qo'lda yozsa "Sizga ruxsat berilmagan!" xabarini oladi.

## 10.1 Asosiy tushunchalar

- **Rasmiy maktub (xat)** — tashqi tashkilot bilan yozishma. Ikki yo'nalishda bo'ladi:
  - "Chiqish" (chiquvchi) — biz yuboramiz;
  - "Kirish" (kiruvchi) — bizga keladi.
- **Raqamlash** — chiquvchi xat va buyruqlar tizim tomonidan ketma-ket raqamlanadi (masalan 0057, 0058). Kiruvchi xat esa jo'natuvchining raqami bilan ro'yxatga olinadi va bizning ketma-ketlikka qo'shilmaydi.
- **Korxona buyrug'i** — direktor buyrug'i. Masalan: "Noyabr buyurtmasi bo'yicha ishlab chiqarishni boshlash to'g'risida".
- **Korxona standarti (STD)** — ichki texnologik yoki tashkiliy qoida. Masalan: "P1 yog'och taglik yig'ish tartibi".
- **Lavozim yo'riqnomasi** — lavozim majburiyatlari hujjati (9-bobga qarang).
- **Hujjat kodi** — tizim beradigan noyob kod:
  - STD uchun STD-2026-000012;
  - yo'riqnoma uchun JD-2026-000005.
- **Word shablon** — iPack blankasidagi .docx fayl. Unda `{{LETTER_NO}}`, `{{LETTER_BODY}}` kabi maydonlar bor, tizim ularni hujjat ma'lumotlari bilan to'ldiradi.
- **Fayl versiyasi** — har safar Word hujjat yaratilganda yoki matn o'zgarganda yangi fayl saqlanadi. Eski versiyalar o'chmaydi, oxirgisi asosiy hisoblanadi.
- **Hujjat holati** — xat va buyruqning tasdiqlash bosqichi: Qoralama → Tasdiqlashda → Tasdiqlangan yoki Rad etilgan.
- **Topshiriq (vazifa, task)** — xodimga berilgan ish. Uning kodi (TASK-NN), muddati, ustuvorligi, statusi, loyihasi va ijrochisi bo'ladi.
- **Loyiha** — topshiriqlar guruhi. Masalan: "UzAuto noyabr buyurtmasi".

## 10.2 Hujjat holatlari (xat va buyruq uchun)

| Holat | Ma'nosi | Mumkin o'tish (Batafsil sahifasidagi tugmalar) |
|---|---|---|
| Qoralama | Yaratilgan, tahrirlanmoqda | "Tasdiqlash uchun yuborish", "Tasdiqlash", "Rad etish" |
| Tasdiqlashda | Rahbar ko'rib chiqmoqda | "Tasdiqlash", "Rad etish" |
| Tasdiqlangan | Yakuniy holat | Tugma yo'q |
| Rad etilgan (bekor qilingan) | Yakuniy holat | Tugma yo'q |

> **Diqqat:** Tasdiqlangan yoki rad etilgan hujjatning holatini qaytarib bo'lmaydi. Xatoni tuzatish uchun yangi hujjat yarating.

## 10.3 Rasmiy xatlar: chiquvchi xat

**Kim:** sotuv bo'limi, devonxona   **Menyu:** Hujjatlar → Hujjatlar boshqaruvi → Rasmiy maktublar → "Yangi Maktub"

Ssenariy: iPack UzAutoMotors Powertrain'ga 2027 yil uchun P1 taglik va B6.1 quti narx taklifini yuboradi.

1. "Yangi Maktub"ni oching. "Maktub raqami" avtomatik ko'rinadi (masalan 0057).
2. Asosiy maydonlarni to'ldiring (jadvalga qarang).
3. **Salomlashish** qatorini yozing: "Hurmatli Sardor Akmalovich!" (misol).
4. **Asosiy matn**ni yozing. Muharrirda qalin, kursiv, ro'yxat va rasm ishlatish mumkin.
   Yoki **"Matn taklif qilish"** tugmasini bosing. Tizim yo'nalish, kompaniya, kontakt shaxs va mavzu asosida xat matnini taklif qiladi. Taklifni tekshirib, o'zgartiring.
5. **Mahsulot va narx** jadvaliga "Mahsulot qo'shish" bilan qatorlar qo'shing (jadvalga qarang).
6. "Saqlash"ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Maktub raqami | Avtomatik (o'zgartirish mumkin) | 0057 |
| Maktub sanasi | Xat sanasi | 02.11.2026 |
| Yo'nalish | Chiqish / Kirish | Chiqish |
| Kanal | EMAIL, LETTER, COURIER, FAX | EMAIL |
| Tashqi kompaniya nomi | Qabul qiluvchi | AJ "UzAutoMotors Powertrain" |
| Tashqi kontakt shaxs | Kimga | (ta'minot bo'limi boshlig'i) |
| Tashqi kompaniya rahbari lavozimi | Lavozimi | Ta'minot bo'limi boshlig'i |
| Bizning havola raqami | Ichki raqam | iP-2026/057 |
| Ularning havola raqami | Ularning so'rov xati raqami (bo'lsa) | 12-345 |
| Mavzu | Qisqa mavzu | "2027 yil uchun qadoq narxlari" |

| Mahsulot nomi | O'lchami | Narx EXW | Narx QQS bilan EXW | Narx CIP | Narx QQS bilan CIP |
|---|---|---|---|---|---|
| iPack P1 Yog'och taglik | 1200×1000 | 185 000 | 207 200 | 195 000 | 218 400 |
| iPack B6.1 quti | — | 14 500 | 16 240 | 15 200 | 17 024 |

(Narxlar misol: QQS 12%.)

**Natija:**
- Xabar: "Maktub muvaffaqiyatli yaratildi". Xat **Qoralama** holatida ro'yxatda turadi.
- Agar shu orada boshqa xodim 0057 raqamni band qilgan bo'lsa, xat keyingi raqamni oladi. Bunda xabar chiqadi: "Maktub yaratildi. 0057 raqami band bo'lgani uchun 0058 raqami berildi".

> **Diqqat:** O'chirilgan xatning raqami qayta berilmaydi. Raqamlar ketma-ketligida "teshik" qolishi normal holat.

> **Maslahat:** "Matn taklif qilish" sun'iy intellekt sozlamasi yoqilgan bo'lsa AI orqali, aks holda tayyor qolip asosida ishlaydi. Har ikki holatda ham matnni albatta o'qib, tahrirlang.

## 10.4 Kiruvchi xatni ro'yxatga olish

**Kim:** devonxona   **Menyu:** Rasmiy maktublar → "Kiruvchi xatni ro'yxatga olish"

Ssenariy: Wood Nam Import narx o'zgarishi haqida xat yubordi.

1. To'ldiring:
   - Kompaniya/Shaxs nomi: MChJ "Wood Nam Import";
   - Xat raqami: 214 (ularning raqami);
   - Xat sanasi: 04.11.2026.
2. "Xat ilovasi (PDF)" maydonida skanerlangan xatni tanlang. Faqat PDF qabul qilinadi, hajmi 10 MB gacha.
3. "Saqlash"ni bosing.

**Natija:**
- Xabar: "Kiruvchi xat muvaffaqiyatli ro'yxatga olindi".
- Ro'yxatda Yo'nalish "Kirish", raqam 214 ko'rinadi. "Batafsil"dan PDF yuklab olinadi.
- Chiquvchi xatlar ketma-ketligi o'zgarmaydi: keyingi "Yangi Maktub" yana 0058 ni taklif qiladi.

> **Diqqat:**
> - .docx yoki rasm yuklansa, xato chiqadi ("Faqat PDF fayllarni yuklash mumkin") va yozuv yaratilmaydi.
> - Kirill harflari va qo'shtirnoqli fayl nomlari (masalan `Акт "сверки".pdf`) to'g'ri saqlanadi.

## 10.5 Xatlar ro'yxati, ko'rish, holat, o'chirish

- **Ro'yxat**:
  - ustunlar: Raqam, Sana, Yo'nalish, Kompaniya, Kontakt, Mavzu, Holat;
  - filtr — **Yil** (faqat tanlangan yil xatlari ko'rinadi) va Qidirish;
  - Excel va PDF'ga eksport qilish mumkin;
  - yuqorida "Yangi Maktub", "Kiruvchi xatni ro'yxatga olish" va "Word shablonlar" tugmalari bor.
- **Batafsil**:
  - barcha maydonlar, matn va "Mahsulot va narxlar" jadvali;
  - holat tugmalari (10.2);
  - Word shablon tanlash va **"Word hujjat yaratish"**;
  - **Yaratilgan hujjatlar** ro'yxati: Fayl nomi, Versiya, Yaratilgan sana, "Yuklab olish".
- **Tahrirlash**: maydonlarni o'zgartirish.
- **O'chirish**: yumshoq o'chirish. Xat ro'yxatdan yo'qoladi, raqami qayta berilmaydi.
- Xat mavzusi va kompaniya nomiga yozilgan HTML teglar oddiy matn sifatida ko'rinadi. Asosiy matndagi formatlash (qalin, ro'yxat) esa saqlanadi.

**Tasdiqlash oqimi (misol):**
1. Sotuv mutaxassisi 0057-xatni yaratadi va "Tasdiqlash uchun yuborish"ni bosadi. Holat **Tasdiqlashda** bo'ladi.
2. Direktor X.M. Maxmudov xatni ochib, "Tasdiqlash"ni bosadi. Holat **Tasdiqlangan** bo'ladi.
3. Sotuv mutaxassisi "Word hujjat yaratish" bilan blankani chiqaradi, imzolatadi va yuboradi.

## 10.6 Word shablonlar va hujjat generatsiyasi

**Kim:** devonxona (shablonni bir marta yuklaydi), har bir hujjat egasi (generatsiya)   **Menyu:** Rasmiy maktublar (yoki Kompaniya buyruqlari, Korxona standartlari) → "Word shablonlar"

Shablonlar har hujjat turi uchun alohida saqlanadi: Rasmiy maktub, Korxona buyrug'i, Korxona standarti.

### Shablonni tayyorlash
1. "Namunaviy shablonni yuklab olish"ni bosing. Bu tayyor tuzilma va barcha maydonlar ro'yxati bilan .docx fayl.
2. Uni Word'da iPack blankasiga moslang: logotip, rekvizitlar, "iPack Eco Solutions" MChJ, Andijon. Maydonlarni `{{...}}` ko'rinishida qoldiring.
3. "Yangi shablon yuklash" qismida nom ("iPack xat blankasi") va Word fayl (.docx) tanlab, "Yuklash"ni bosing.
4. Tizim shablonda tanilgan va tanilmagan maydonlar ro'yxatini ko'rsatadi. Shablon **Faol** holatda bo'ladi.

**Asosiy maydonlar:**

| Maydon | Ma'nosi |
|---|---|
| `{{LETTER_NO}}`, `{{LETTER_DATE}}` | Maktub raqami va sanasi |
| `{{COMPANY_NAME}}`, `{{CONTACT_NAME}}`, `{{CONTACT_POSITION}}`, `{{RECIPIENT_BLOCK}}` | Qabul qiluvchi |
| `{{GREETING_LINE}}`, `{{TOPIC}}` | Murojaat va mavzu |
| `{{LETTER_BODY}}` | Xat matni (formatlash bilan, alohida qatorda) |
| `{{ITEMS_TABLE}}` | Mahsulot va narxlar jadvali |
| `{{OUR_REF_NO}}`, `{{THEIR_REF_NO}}`, `{{CHANNEL}}` | Havolalar va yuborish usuli |
| `{{ORDER_NO}}`, `{{ORDER_DATE}}`, `{{ORDER_TOPIC}}`, `{{ORDER_TYPE}}`, `{{ORG_UNIT_NAME}}`, `{{ORDER_BODY}}` | Buyruq maydonlari |
| `{{DIRECTOR_NAME}}` | Direktor F.I.Sh. (korxona sozlamasidan: "X.M. Maxmudov") |
| `{{TODAY}}` | Bugungi sana |

### Hujjat yaratish
1. Xatning "Batafsil" sahifasida shablonni ("iPack xat blankasi") tanlang.
2. "Word hujjat yaratish"ni bosing.
3. Xabar chiqadi: "Hujjat yaratildi (versiya 1) va yuklab olinmoqda." Fayl avtomatik yuklanadi.
4. Yana bossangiz, **versiya 2** yaratiladi. "Yaratilgan hujjatlar" ro'yxatida ikkala fayl turadi, oxirgisi asosiy.

**Shablonlarni boshqarish:**
- shablonni yuklab olish mumkin;
- "Boshqa faol shablonlarni arxivlash" mumkin;
- shablonni faollashtirish yoki arxivlash mumkin;
- o'chirish faqat u bilan hujjat yaratilmagan bo'lsa mumkin.

> **Maslahat:** Eski blankani o'chirmang, arxivlang. Shunda u bilan yaratilgan hujjatlar tarixi saqlanadi.

> **Diqqat:**
> - Faol shablon bo'lmasa, Batafsil'da "Avval shablon yuklang" yozuvi chiqadi.
> - Shablonda noma'lum maydon bo'lsa, u hujjatda o'zgarishsiz qoladi va ogohlantirish chiqadi ("Shablonda tanilmagan maydonlar o'zgarishsiz qoldi: …"). Bunday maydonni shablonda tuzating.
> - Direktor ismi korxona sozlamasidan olinadi. U noto'g'ri bo'lsa, administratorga murojaat qiling.

## 10.7 Korxona buyruqlari

**Kim:** direktor, devonxona, HR   **Menyu:** Hujjatlar → Hujjatlar boshqaruvi → Kompaniya buyruqlari → "Yangi Buyruq"

Ssenariy: noyabr buyurtmasi bo'yicha ishlab chiqarishni boshlash to'g'risida buyruq.

1. "Yangi Buyruq"ni oching. "Buyruq raqami" avtomatik ko'rinadi.
2. Maydonlarni to'ldiring (jadvalga qarang). "Matn taklif qilish" tugmasi buyruq matnini taklif qiladi.
3. "Saqlash"ni bosing.
4. "Batafsil"da holatni o'zgartiring: "Tasdiqlash uchun yuborish", keyin "Tasdiqlash".
5. "Word hujjat yaratish" bilan imzo uchun blankani chiqaring.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Buyruq raqami | Avtomatik | 0031 |
| Buyruq sanasi | Sana | 03.11.2026 |
| Buyruq turi | GENERAL / URGENT / SPECIAL | URGENT |
| Tashkilot bo'limi | Tegishli bo'lim (HR bo'limlaridan) | Ishlab chiqarish bo'limi |
| Sarlavha | Buyruq mavzusi | "UzAutoMotors Powertrain buyurtmasi bo'yicha 1 200 dona P1 taglik va 3 000 dona B6.1 quti ishlab chiqarishni boshlash to'g'risida" |
| Matn | Buyruq bandlari (formatlash bilan) | 1. Ishlab chiqarish rejasi tuzilsin… 2. Xomashyo xaridi ta'minlansin… 3. Nazorat … zimmasiga yuklansin. |
| Javobgar xodimlar | Kadrlar ro'yxatidan | Ishlab chiqarish bo'limi boshlig'i, Xarid bo'limi boshlig'i |

**Natija:**
- Buyruq Qoralama holatida ro'yxatga tushadi.
- Raqam band bo'lsa, keyingisi beriladi va xabar chiqadi ("Buyruq yaratildi. 0031 raqami band bo'lgani uchun 0032 raqami berildi").

**Ro'yxat**:
- ustunlar: Raqam, Sana, Turi, Bo'lim, Sarlavha, Holat;
- filtrlar: Yil, Buyruq turi, Qidirish;
- Excel/PDF eksport.

Batafsil sahifasi xatnikiga o'xshaydi: holat tugmalari, shablon tanlash, yaratilgan hujjatlar va ularning versiyalari.

## 10.8 Korxona standartlari (STD)

**Kim:** texnolog, sifat bo'yicha mas'ul   **Menyu:** Hujjatlar → Hujjatlar boshqaruvi → Korxona standartlari → "Yangi standart qo'shish"

1. **Standart kodi**ni kiriting: "STD-P1-01" (takrorlanmasligi kerak).
2. **Soha**: "Ishlab chiqarish".
3. **Shablon**: ro'yxatdan tayyor shablonni tanlang. U Hujjat kontentiga yuklanadi. Yoki matnni muharrirda o'zingiz yozing, yoki "Word yuklash" bilan tayyor .docx'ni yuklang.
4. **Hujjat kontenti** misoli: "P1 yog'och taglik yig'ish tartibi":
   1. taxta o'lchami 1200 mm;
   2. mix turi va soni;
   3. namlik nazorati;
   4. sifat nazorati.
5. **Eslatma** yozing va saqlang.

**Natija:**
- Standartga **STD-2026-NNNNNN** hujjat kodi va **v1.00** versiya beriladi.
- Matndan Word fayl yaratilib saqlanadi.

**Batafsil**:
- standart ma'lumotlari;
- **Versiya tarixi** (har versiya fayli, Fayl nomi, Yaratilgan sana, yuklab olish);
- "Word hujjatini yuklash (.docx)" tugmasi;
- **Hujjatdagi rasmlar** galereyasi (joriy versiya rasmlari).

**Tahrirlash**: tugma "Saqlash (Yangi versiya yaratish)" deb nomlanadi.
- Versiya raqami har tahrirda 0,01 ga oshadi (v1.00 → v1.01).
- **Yangi fayl versiyasi faqat matn o'zgarsa** (yoki yangi Word yuklansa) qo'shiladi. Faqat Eslatma yoki Soha o'zgarsa, fayllar soni o'zgarmaydi.

**O'chirish**: "Standartni o'chirish" sahifasida "Ha, o'chirish"ni bosing. Standart ro'yxatdan va havolalardan yo'qoladi.

> **Maslahat:** Texnologik jarayon o'zgarganda yangi standart yaratmang, mavjudini tahrirlang. Versiya tarixida eski tartib ham saqlanib qoladi va tekshiruvchilarga qachon nima o'zgarganini ko'rsatish oson bo'ladi.

## 10.9 Fayl versiyalari (umumiy qoida)

| Hujjat | Yangi fayl versiyasi qachon paydo bo'ladi | Raqamlash |
|---|---|---|
| Rasmiy xat | Har "Word hujjat yaratish"da | versiya 1, 2, 3…; oxirgisi asosiy |
| Buyruq | Har "Word hujjat yaratish"da | versiya 1, 2, 3… |
| Korxona standarti | Matn o'zgarganda yoki Word yuklanganda | v1.00, v1.01…; har tahrirda +0,01 |
| Lavozim yo'riqnomasi | Har tahrirda (yangi versiya) | v1.00, v1.01… |

Har bo'lim faqat o'z hujjat turining faylini beradi. Masalan, STD sahifasi orqali xat faylini yuklab bo'lmaydi. Rasm fayllar brauzerda ochiladi, qolganlari yuklab olinadi.

## 10.10 Topshiriqlar: sozlamalar

**Kim:** administrator yoki rahbar   **Menyu:** Tasks / Vazifalar → Task Sozlamalari → Statuslar / Prioritetlar / Loyihalar

Topshiriq yaratishdan oldin ma'lumotnomalar tayyor bo'lishi kerak.

**Statuslar** ("Status qo'shish"):

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Kod * | Lotin kodi | TODO, IN_PROGRESS, DONE, CANCELLED |
| Nomi * | Ko'rinadigan nom | Bajarilishi kerak, Jarayonda, Bajarildi, Bekor qilindi |
| SortOrder | Kanban ustunlari tartibi; 0 bo'lsa avtomatik | 10, 20, 30, 40 |
| Rang | Badge va grafiklardagi rang | yashil — Bajarildi |
| Yopilgan holat | Bu status topshiriqni yopadimi | DONE va CANCELLED uchun ✔ |
| Faol | Tanlovlarda ko'rinsinmi | ✔ |

**Prioritetlar** ham shu tartibda kiritiladi: Kod, Nomi, tartib, rang. Masalan: LOW — Past, MEDIUM — O'rta, HIGH — Yuqori, CRITICAL — Shoshilinch.

**Loyihalar** ("Loyiha qo'shish"): Kod * "UZA-2611", Nomi * "UzAuto noyabr buyurtmasi", Faol.
Loyiha "Batafsil" sahifasida unga biriktirilgan topshiriqlar ro'yxati va "Yangi vazifa qo'shish" tugmasi bor.

> **Diqqat:**
> - Topshiriqlarda ishlatilayotgan status, prioritet yoki loyihani o'chirib bo'lmaydi. Kerak bo'lmasa, uni nofaol qiling.
> - O'chirilganlar tanlov ro'yxatlarida ko'rinmaydi.
> - "Yopilgan holat" belgisi muhim: belgilangan statusdagi topshiriq muddati o'tgan deb hisoblanmaydi va Kanbanda 30 kundan keyin yashiriladi.

## 10.11 Topshiriq yaratish

**Kim:** rahbar, bo'lim boshlig'i (o'ziga ham yaratishi mumkin)   **Menyu:** Tasks / Vazifalar → Vazifalar → "Yangi Task qo'shish"

Ssenariy: Noyabr buyurtmasi bo'yicha topshiriqlar.

1. "Yangi Task qo'shish"ni bosing.
2. Maydonlarni to'ldiring:

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Title * | Qisqa sarlavha | "Wood Nam Import'dan 100*50*6000 taxta yetkazilishini nazorat qilish" |
| Description | Batafsil vazifa | "P1 taglik uchun taxta, 3 partiya, 1-partiya 05.11 gacha" |
| Status * | Boshlang'ich status | TODO |
| Priority * | Ustuvorlik | Yuqori |
| Project | Loyiha | UzAuto noyabr buyurtmasi |
| Assignee | Ijrochi (foydalanuvchi) | Xarid mutaxassisi |
| Due Date | Muddat | 05.11.2026 |

3. "Saqlash"ni bosing. Tizim **TASK-NN** ko'rinishidagi takrorlanmaydigan kod beradi. Yaratuvchi (Reporter) avtomatik yoziladi.

Shu loyihaga yana topshiriqlar qo'shing:
- "1 200 dona P1 ishlab chiqarish rejasini tuzish" — texnolog, 04.11;
- "1-partiya uchun Osiyo Trans Kargo bilan fura kelishish" — logist, 05.11;
- "Sotuv invoysi va hisob-faktura chiqarish" — sotuv mutaxassisi.

**Natija:**
- Ijrochining yuqori o'ng menyusidagi **"Topshiriqlar"** bandida qizil raqam (unga biriktirilgan ochiq topshiriqlar soni) paydo bo'ladi.
- Topshiriq ro'yxat, Kanban va Dashboardda ko'rinadi.

## 10.12 Topshiriqlar ro'yxati, status va izoh

**Menyu:** Tasks / Vazifalar → Vazifalar

**Ro'yxat**:
- ustunlar: Kod, Sarlavha, Status, Prioritet, Mas'ul, Muddati, Yaratilgan;
- **filtrlar**: Assignee (Mas'ul), Status, Prioritet, Muddati (dan) / Muddati (gacha) ("gacha" sanasi ham kiradi), "Muddatidan o'tgan", Qidirish;
- "Filtrlash" va "Tozalash" tugmalari;
- qidiruv, saralash va Excel eksport butun natija bo'yicha ishlaydi (faqat ko'rinib turgan sahifa emas).

**Statusni o'zgartirish (ijrochi uchun):**
1. Ro'yxatdan yoki "Batafsil"dan "Status o'zgartirish"ni bosing.
2. Yangi Status *: "IN_PROGRESS" (Jarayonda).
3. Izoh: "Ta'minotchi bilan 05.11 ga kelishildi, mashina 04.11 da yuklanadi".
4. Saqlang. Xabar chiqadi: "Status muvaffaqiyatli o'zgartirildi".
5. Ish tugagach, statusni "DONE" ga o'zgartiring. Izoh: "1-partiya 18 t qabul qilindi, kirim hujjati №…".

"Batafsil" ("Vazifa Ma'lumotlari") sahifasida joriy status va **ijrochi izohi** ko'rinadi. Yangi izoh eskisining o'rniga yoziladi.

**Tahrirlash:** sarlavha, tavsif, muddat, ijrochi va boshqa maydonlarni o'zgartirish. Kod o'zgarmaydi.

**O'chirish = bekor qilish:**
- "O'chirish" tugmasi topshiriqni bazadan o'chirmaydi, uni **CANCELLED** (bekor qilingan) statusiga o'tkazadi. Kodi va tarixi saqlanadi.
- Oddiy foydalanuvchi faqat o'ziga tegishli topshiriqni bekor qila oladi.
- Bekor qilingan topshiriqlarni ro'yxatda Status filtri "CANCELLED" bilan ko'rish mumkin.

| Status (misol) | Ma'nosi | Yopilganmi |
|---|---|---|
| TODO | Bajarilishi kerak | Yo'q |
| IN_PROGRESS | Jarayonda | Yo'q |
| DONE | Bajarildi | Ha |
| CANCELLED | Bekor qilindi ("O'chirish" tugmasi) | Ha |

## 10.13 Kanban

**Kim:** hamma ijrochilar va rahbar   **Menyu:** Tasks / Vazifalar → Kanban

- Ikki ko'rinish bor:
  - **"Status bo'yicha"** — ustunlar statuslar (TODO → IN_PROGRESS → DONE);
  - **"Loyiha bo'yicha"** — topshiriqlar loyihalar bo'yicha guruhlanadi.
- Har kartochkada sarlavha, ijrochi, muddat va loyiha ko'rinadi.
- Kartochkani boshqa status ustuniga **sudrab o'tkazsangiz**, status saqlanadi va sahifa yangilansa ham o'sha joyda qoladi. Buning uchun topshiriqni tahrirlash yoki status o'zgartirish huquqi kerak.
- Standart holatda 30 kundan eski yopilgan topshiriqlar (DONE/CANCELLED) yashirin. **"Barcha yopilganlar"** tugmasi ularni ko'rsatadi, **"Faqat oxirgi 30 kun"** yana yashiradi.

> **Maslahat:** Har kuni ertalabki 10 daqiqalik yig'ilishni Kanban "Loyiha bo'yicha" ko'rinishida o'tkazing. "UzAuto noyabr buyurtmasi" ustunida qaysi ish qayerda turgani bir qarashda ko'rinadi.

## 10.14 Vazifalar dashboardi

**Menyu:** Dashboard → Vazifalar Dashboard (yoki Tasks / Vazifalar → Dashboard)

- **Filtr:** "Foydalanuvchi" (Barcha foydalanuvchilar yoki bitta xodim).
- **Kartalar:**
  - foydalanuvchining jami topshiriqlari;
  - bajarilishi kerak, jarayonda va bajarilgan topshiriqlar soni;
  - **muddati o'tganlar** (yopilgan statusdagilar kirmaydi);
  - oxirgi topshiriqlar.
- **Grafiklar:** "Status bo'yicha taqsimot", "Prioritet bo'yicha taqsimot", "Prioritet bo'yicha batafsil statistika".
- **"Muddatidan o'tgan vazifalar (Loyiha bo'yicha)"** — qaysi loyiha kechikayotgani.
- **"Xodimlar reytingi"** — kim qancha topshiriqni bajargan.

## 10.15 Foydalanuvchi qo'llanmasi sahifasi

**Menyu:** Hujjatlar → Hujjatlar boshqaruvi → Qo'llanma (manzil `/UserManual`)

- Chap tomonda bo'limlar ro'yxati, tepada "Bo'limlar bo'yicha qidirish..." maydoni bor.
- Bo'limlar:
  - Kirish;
  - Foydalanuvchilar, rollar va ruxsatlar;
  - Sotuvlar moduli (CRM);
  - Xarid moduli;
  - Omborxona boshqaruvi;
  - Ishlab chiqarish rejalashtirish;
  - Moliya moduli;
  - Kadrlar boshqaruvi (HR);
  - Sozlamalar;
  - Engineering / Muhandislik;
  - Logistika;
  - Vazifalar boshqaruvi (Tasks).
- Sahifa tez ochiladi va bo'limlar tartibi o'zgarmaydi.

Bu qisqa ichki ma'lumotnoma. Batafsil ish tartibi uchun ushbu qo'llanmadan foydalaning.

## 10.16 Hisobotlar va nazorat

| Nima | Qayerda | Kim uchun |
|---|---|---|
| Yil bo'yicha chiquvchi va kiruvchi xatlar | Rasmiy maktublar → Yil filtri → Excel/PDF | Devonxona, rahbar |
| Buyruqlar reyestri | Kompaniya buyruqlari → Yil va tur filtri → Excel/PDF | Devonxona, HR |
| Standartlar ro'yxati va versiyalari | Korxona standartlari → Excel/PDF; Batafsil → Versiya tarixi | Texnolog |
| Topshiriqlar ijrosi | Vazifalar Dashboard, Kanban | Rahbar |
| Xodim bo'yicha topshiriqlar | Vazifalar → Assignee filtri → Excel | Bo'lim boshlig'i |

## 10.17 Tez-tez uchraydigan savollar va muammolar

**Formada 0057 ko'rindi, saqlangach 0058 bo'ldi.**
Shu orada boshqa foydalanuvchi 0057 ni band qilgan. Tizim keyingi raqamni berib, bu haqda xabar chiqargan. Bu to'g'ri xatti-harakat.

**Kiruvchi xat raqami bizning ketma-ketlikni buzadimi?**
Yo'q. Faqat chiquvchi xatlar hisoblanadi.

**"Word hujjat yaratish" tugmasi yo'q, "Avval shablon yuklang" yozilgan.**
Bu hujjat turi uchun faol shablon yo'q. "Word shablonlar" sahifasida shablon yuklang.

**Word hujjatda `{{...}}` qolib ketdi.**
Shablondagi maydon nomi noto'g'ri yozilgan. Ogohlantirishda uning nomi ko'rsatiladi. Namunaviy shablondagi maydonlar ro'yxati bilan solishtiring.

**Direktor ismi Word'da noto'g'ri.**
U korxona sozlamasidan olinadi. Administratorga murojaat qiling.

**Tasdiqlangan xatda holat tugmalari yo'q.**
Tasdiqlangan va rad etilgan — yakuniy holatlar.

**STD'ni tahrirladim, lekin versiya tarixida yangi fayl yo'q.**
Faqat eslatma yoki soha o'zgargan. Yangi fayl faqat matn o'zgarganda qo'shiladi, versiya raqami esa baribir oshadi.

**Topshiriqni o'chirdim, lekin u ro'yxatda CANCELLED bo'lib turibdi.**
Bu qasddan shunday: o'chirish topshiriqni bekor qiladi, tarix saqlanadi.

**Boshqa xodimning topshirig'ini o'chira olmayapman.**
Oddiy foydalanuvchi faqat o'z topshirig'ini bekor qila oladi. Rahbar yoki administratorga murojaat qiling.

**Statusni o'chirib bo'lmayapti.**
U topshiriqlarda ishlatilmoqda. Uni nofaol qiling.

**Kanbanda eski bajarilgan topshiriqlar ko'rinmayapti.**
30 kundan eski yopilganlar yashirin. "Barcha yopilganlar" tugmasini bosing.
