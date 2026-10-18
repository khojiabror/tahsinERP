# 9. Kadrlar va ish haqi (HR)

Kadrlar moduli iPack'ning tashkiliy tuzilmasini, xodimlarini, ish vaqtini va ish haqini bitta joyda yuritadi. Bo'limlar: Boshqaruv, Ishlab chiqarish bo'limi, Sotuv, Texnologiya, Xarid.
Ish haqi ikki xil hisoblanadi:
- **soatbay** — direktor, omborchi, texnolog, elektrik kabi oylik maoshli xodimlar uchun, oylik tabel asosida;
- **ishbay** — qadoqlovchi va sex ishchilari uchun, nechta P1 taglik yoki B6.1 quti yig'ilganiga qarab.

Modul bilan HR mutaxassisi va buxgalter ishlaydi. Tabelni bo'lim boshlig'i to'ldirishi mumkin, rahbar HR dashboardni ko'radi.
Bog'liq modullar:
- **Moliya** — ish haqi tasdiqlanganda kassa yoki bankdan avtomatik **Chiqim** yoziladi;
- **Hujjatlar** — lavozim yo'riqnomalari, korxona buyruqlari;
- **Muhandislik** — ishbay tariflari mahsulotga bog'lanadi.

> **Diqqat:** HR sahifalari ruxsat bilan himoyalangan. Ruxsati yo'q foydalanuvchi menyuda "Kadrlar (HR)" bo'limini ko'rmaydi, manzilni qo'lda yozsa "Sizga ruxsat berilmagan!" xabarini oladi. Holatni o'zgartiradigan amallar (tasdiqlash, yopish, o'chirish) faqat tugma orqali bajariladi, havola orqali emas.

## 9.1 Asosiy tushunchalar

- **Bo'lim (tashkiliy birlik)** — daraxt ko'rinishidagi tuzilma.
  Misol: Boshqaruv → Ishlab chiqarish bo'limi → "Ishlab chiqarish" sexi.
- **Lavozim** — Direktor, Texnolog, Konstruktor, Omborchi, Qadoqlovchi, Elektrik va boshqalar. "Rahbariyat lavozimi" belgisi bilan rahbarlar ajratiladi.
- **Tarif tarmog'i (razryad)** — lavozim yoki xodim uchun maoshning eng kam va eng ko'p chegarasi. Misol: "R-3 Ishchi": 3 500 000 – 5 000 000 so'm.
- **Shtat jadvali** — har bo'limda qaysi lavozimdan nechta bo'lishi rejalashtirilgani va to'lov turi: belgilangan maosh, soatbay yoki ishbay.
- **Tabel raqami (Tab.№)** — xodimning ichki raqami. Tizim uni avtomatik beradi, ishlayotgan xodimlar orasida takrorlanmaydi.
- **Staj (xodim tarixi)** — xodim qaysi davrda qaysi bo'lim va lavozimda ishlagani.
- **Fond soati** — oyda ishlanishi kerak bo'lgan me'yoriy soat: ish kunlari × kunlik me'yor.
  Noyabr 2026: 21 ish kuni × 8 = **168 soat**.
- **Qo'shimcha soat** — ish kunida kunlik me'yordan ortiq ishlangan soat hamda dam olish yoki bayram kunida ishlangan soat. U koeffitsient bilan (odatda 1,5) to'lanadi.
- **Soatbay tabel** — oyning har kuni uchun xodim ishlagan soatlar jadvali.
- **Ishbay tabel** — xodim qaysi kuni qaysi mahsulot bo'yicha qaysi operatsiyadan nechta bajargani.
- **Ishbay tarifi** — bitta mahsulot birligi uchun to'lanadigan summa. U mahsulot, lavozim va operatsiyaga bog'lanadi.
- **Ustama** — xodimga belgilangan qo'shimcha foizlar: "Moddiy javobgarlik, %" va "Maxsus ustama, %". Ular maoshga qo'shiladi.
- **Koreshok** — bitta xodim bo'yicha davr kesimida hisoblangan, to'langan va qoldiq ish haqi varaqasi.

## 9.2 HR sozlamalari (ish kalendari va soliq)

**Kim:** HR mutaxassisi (o'zgartirish huquqi bilan)   **Menyu:** Kadrlar (HR) → Ish haqi → "Sozlamalar" (yoki Tabel → Soatbay tabel → "Sozlamalar")

Bu sozlamalar butun tizim uchun bitta. Yil boshida bir marta to'ldiriladi, yil davomida bayramlar qo'shib boriladi.

1. **Ish kunlari** — Dushanba–Juma belgilang, Shanba va Yakshanba belgilanmasin. Agar sex 6 kunlik ishlasa, Shanbani ham belgilang.
2. **Bayram va qo'shimcha dam olish kunlari** — har qatorga bitta sana yozing (2026-12-08 yoki 08.12.2026 ko'rinishida).
3. **Kunlik me'yor (soat)** — 8.
4. **Qo'shimcha soat koeffitsienti** — 1,5.
5. **Standart daromad solig'i (%)** — 12.
6. "Saqlash"ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ish kunlari | Haftaning ish kunlari | Du–Ju |
| Bayram va qo'shimcha dam olish kunlari | Har qatorda bitta sana | 2026-01-01, 2026-03-08, 2026-03-21, 2026-09-01, 2026-10-01, 2026-12-08 |
| Kunlik me'yor (soat) | Bir kunlik me'yor | 8 |
| Qo'shimcha soat koeffitsienti | Ortiqcha soat ko'paytmasi | 1,5 |
| Standart daromad solig'i (%) | Soatbay va ishbay hisoblarga qo'llanadi | 12 |

> **Diqqat:** Sozlamalar faqat shundan keyin **yangi yaratilgan** tabellarning fond soatiga ta'sir qiladi. Avval yaratilgan tabelning fond soati o'zgarmaydi. Shuning uchun bayramni oy tabelini yaratishdan oldin kiriting.

## 9.3 Tashkiliy tuzilma va tashkiliy sxema

**Kim:** HR mutaxassisi   **Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Tashkiliy struktura

Sahifaning chap qismida **bo'limlar daraxti**, o'ng qismida tanlangan bo'limning tafsilotlari ko'rinadi.

1. "Bo'lim qo'shish"ni bosing.
2. Kod, Nomi va ota bo'limni (yuqori bo'lim) tanlang, "Faol" belgisini qo'ying va saqlang.
3. iPack tuzilmasini shu tartibda kiriting:
   - BSH "Boshqaruv" — ota bo'limsiz;
   - ICH "Ishlab chiqarish bo'limi" — otasi Boshqaruv;
   - SOT "Sotuv bo'limi", TEX "Texnologiya bo'limi", XAR "Xarid bo'limi" — otasi Boshqaruv.
4. "Tashkiliy sxema" tugmasi org-chartni ochadi. Unda faol xodimlar lavozim nomi bilan bo'limlar ostida ko'rinadi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Kod | Qisqa kod (takrorlanmasin) | ICH |
| Nomi | Bo'lim nomi | Ishlab chiqarish bo'limi |
| Ota bo'lim | Yuqori bo'lim | Boshqaruv |
| Faol | Ishlayotgan bo'lim | ✔ |

- **Batafsil** sahifasida bo'limdagi shtatlar (lavozim va soni) hamda xodimlar ro'yxati ko'rinadi.
- **O'chirish**: bo'limda faol quyi bo'lim yoki xodim bo'lsa, tizim ogohlantiradi. O'chirilgan quyi bo'limlar o'chirishga to'sqinlik qilmaydi.

## 9.4 Lavozimlar

**Kim:** HR mutaxassisi   **Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Lavozimlar

1. "Lavozim qo'shish"ni bosing.
2. Kod va Nomini kiriting. Rahbar lavozimlari uchun "Rahbariyat lavozimi"ni belgilang.
3. "Faol"ni belgilab saqlang.

| Maydon | iPack misoli |
|---|---|
| Kod | DIR, TEX, KON, OMB, QAD, SOTM, XARM, ELK |
| Nomi | Direktor, Texnolog, Konstruktor, Omborchi, Qadoqlovchi, Sotuv mutaxassisi, Xarid mutaxassisi, Elektrik |
| Rahbariyat lavozimi | Direktor, o'rinbosarlar, bo'lim boshliqlari uchun ✔ |

Ro'yxatda filtr bor: Hammasi / Faol bo'lgan / Inactive; Rahbariyat / Oddiy.

## 9.5 Tarif tarmog'i (razryadlar)

**Kim:** HR mutaxassisi, buxgalter   **Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Tariflar

"Tarif tarmog'ini qo'shish" orqali maosh oraliqlari kiritiladi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Kod | Razryad kodi | R-3 |
| Nomi | Razryad nomi | Ishchi 3-razryad |
| Min. maosh | Pastki chegara | 3 500 000 |
| Maks. maosh | Yuqori chegara | 5 000 000 |
| Valyuta | Maosh valyutasi | UZS |
| Amal qiladi / Amal qiladi (to) | Amal qilish davri | 01.01.2026 – (bo'sh) |

> **Maslahat:** Xodimning "Asosiy maosh" maydoni bo'sh bo'lsa, soatbay hisobda oylik baza quyidagi tartibda olinadi:
> 1. shtat jadvalidagi belgilangan maosh;
> 2. u ham bo'lmasa, xodimning tarif tarmog'idagi o'rtacha qiymat: (min + maks) / 2. R-3 uchun bu 4 250 000.

## 9.6 Shtat jadvali

**Kim:** HR mutaxassisi, rahbar   **Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Shtat jadvali

Shtat jadvali har bir bo'lim va lavozim juftligi uchun rejadagi soni va to'lov turini belgilaydi. Xodimni bo'limga o'tkazishda faqat shu yerda rejalashtirilgan lavozimni tanlash mumkin.

1. "Pozitsiya qo'shish"ni bosing.
2. Bo'lim, Lavozim, Tarif tarmog'i va Rejalashtirilgan sonini kiriting.
3. To'lov turini belgilang: "Belgilangan maosh", "Soatbay to'lov" yoki "Ishbay to'lov".
4. "Amal qiladi" sanalarini kiriting va "Faol"ni belgilang.

| Bo'lim | Lavozim | To'lov turi | Rejalashtirilgan soni |
|---|---|---|---|
| Boshqaruv | Direktor | Soatbay | 1 |
| Ishlab chiqarish bo'limi | Omborchi | Soatbay | 2 |
| Ishlab chiqarish bo'limi | Qadoqlovchi | Ishbay | 12 |
| Texnologiya bo'limi | Texnolog | Soatbay | 2 |
| Sotuv bo'limi | Sotuv mutaxassisi | Soatbay | 2 |

(Sonlar misol.)

- Ro'yxatda **"Faktik"** ustuni bor: shu juftlikda hozir nechta faol xodim ishlayotgani. Rejadan og'ish HR dashboardda ham ko'rinadi.
- **"Xodimlardan yaratish"** — mavjud xodimlar asosida har (bo'lim, lavozim) uchun bitta taklif tuziladi, soni xodimlar soniga teng. Kerakli qatorlarni belgilab, "Tanlangan pozitsiyalarni yaratish"ni bosing. Shtatda allaqachon bor juftlik qayta yaratilmaydi.
- **"Exceldan import"** — shtat Excel faylini (Tab.№, F.I.O., Lavozim, Maosh ustunlari bilan) yuklang. Oldindan ko'rish jadvalini tekshirib, "Tasdiqlash va saqlash"ni bosing.

## 9.7 Xodimlar

**Kim:** HR mutaxassisi   **Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Xodimlar

### 9.7.1 Ishga qabul qilish

1. "Xodim qo'shish"ni bosing.
2. Maydonlarni to'ldiring. Tabel raqami avtomatik beriladi ("Tabel raqami avtomatik generatsiya qilinadi").
3. "Faol"ni belgilab saqlang.

| Maydon (formadagi nomi) | Nima kiritiladi | iPack misoli |
|---|---|---|
| Табельный номер | Avtomatik | 0045 (tizim beradi) |
| Ф.И.О. | To'liq ism | Aliyev Anvar (misol) |
| Подразделение (Bo'lim) | Bo'lim | Ishlab chiqarish bo'limi |
| Должность (Lavozim) | Lavozim (shtatda rejalashtirilganlari) | Omborchi |
| Базовая зарплата (Asosiy maosh) | Oylik maosh; ishbay xodimda bo'sh qolishi mumkin | 5 040 000 |
| Moddiy javobgarlik, % | Moddiy javobgarlik ustamasi | 0 (omborchi uchun, masalan, 10) |
| Maxsus ustama, % | Qo'shimcha ustama | 0 |
| Тарифная сетка (Tarif tarmog'i) | Razryad | R-3 |
| Дата приема (Qabul sanasi) | Ishga kirgan sana | 01.03.2024 |
| Дата увольнения | Faqat bo'shatishda | — |
| Faol | Ishlayaptimi | ✔ |

Qadoqlovchi (ishbay) uchun misol:
- Ф.И.О.: Karimov Botir (misol);
- Bo'lim: Ishlab chiqarish bo'limi;
- Lavozim: Qadoqlovchi;
- Asosiy maosh: bo'sh;
- Qabul sanasi: 01.06.2025.

**Natija:**
- Xodim ro'yxatda paydo bo'ladi.
- Xodimlar staji sahifasida qabul sanasidan boshlangan bitta ochiq davr ochiladi.
- Shtat jadvalidagi "Faktik" soni bittaga oshadi.

> **Diqqat:** Tabel raqamini qo'lda boshqa ishlayotgan xodim raqamiga o'zgartirib bo'lmaydi — dublikat xatosi chiqadi. Bo'shatilgan xodimning raqami esa yangi xodimga berilishi mumkin.

### 9.7.2 Ro'yxat va xodim kartasi

- **Ro'yxat**:
  - ustunlar: Tab.№, F.I.O., Bo'lim, Lavozim, Qabul qilingan sana, Holat;
  - filtr: Hammasi / Faol / Nofaol;
  - Excel va PDF'ga yuklash tugmalari.
- **Batafsil** (xodim kartasi):
  - ma'lumotlar va **Xodim tarixi** (bo'lim, lavozim, boshlanish va tugash sanasi, sabab);
  - "Staj qo'shish" tugmasi — tizimda ko'rinmayotgan oldingi davrni qo'lda qo'shadi. Misol: xodim 2020–2023 yillarda ham iPack'da ishlagan bo'lsa.

### 9.7.3 Boshqa bo'limga o'tkazish (Transfer)

1. Xodim qatoridagi "Transfer" tugmasini bosing.
2. Maydonlarni to'ldiring:
   - Yangi bo'lim — avval bo'limni tanlang;
   - Yangi lavozim — ro'yxatda faqat shu bo'lim uchun shtatda rejalashtirilgan lavozimlar chiqadi;
   - O'tkazish sanasi: 01.12.2026;
   - Sabab: "Buyruq №… asosida".
3. "Tasdiqlash"ni bosing.

**Natija:**
- Eski staj davri o'tkazish sanasidan bir kun oldin yopiladi (30.11.2026), yangisi 01.12.2026 dan ochiladi.
- **Ishga qabul sanasi o'zgarmaydi**, umumiy staj uzluksiz hisoblanadi.
- Lavozim shtatda bo'lmasa, xabar chiqadi: "Tanlangan lavozim ushbu bo'lim uchun reja qilinmagan…".

### 9.7.4 Ishdan bo'shatish

1. "Ishdan bo'shatish" tugmasini bosing.
2. Ishdan bo'shatish sanasi va Sababni kiritib, "Tasdiqlash"ni bosing.

**Natija:**
- Xodim nofaol bo'ladi va joriy staj davri bo'shatish sanasida yopiladi.
- Keyingi oylarning tabeliga u qo'shilmaydi.
- Bo'shatilgan xodimni qayta bo'shatib yoki transfer qilib bo'lmaydi.
- Bo'shatish sanasi qabul sanasidan oldin bo'lishi mumkin emas.

### 9.7.5 Xodimlar staji

**Menyu:** Kadrlar (HR) → Kadrlar boshqaruvi → Xodimlar staji

- Har xodim bo'yicha davrlar ko'rinadi: Ishga kirgan sana, Ishdan chiqqan sana, Bo'lim, Lavozim, Sabab, Staj.
- Filtrlar: Barcha xodimlar / Faol xodimlar / Chiqib ketganlar, lavozim bo'yicha.
- "Qo'shish" va "Tahrirlash" orqali davrni tuzatish mumkin. Tugash sanasi boshlanishdan oldin bo'lmasligi kerak.

## 9.8 Soatbay tabel

**Kim:** HR mutaxassisi yoki bo'lim boshlig'i   **Menyu:** Kadrlar (HR) → Tabel → Soatbay tabel

### Holatlar

| Holat | Nima qilish mumkin | Keyingi qadam |
|---|---|---|
| Qoralama | Soat kiritish, xodim qo'shish yoki olib tashlash, o'chirish | "Tasdiqlash" |
| Tasdiqlangan | Faqat ko'rish va eksport; ish haqi shu tabeldan yaratiladi | "Yopish" |
| Yopilgan | Faqat ko'rish (oy yakunlangan) | — |

### Tabel yaratish va to'ldirish (Noyabr 2026, Ishlab chiqarish bo'limi)

1. "Tabel yaratish"ni bosing.
2. Yil 2026, Oy noyabr, Bo'lim "Ishlab chiqarish bo'limi"ni tanlang. "Oylik fond soati" maydonida **168** ko'rinadi.
3. Kerak bo'lsa, "Qo'lda xodim qo'shish (ixtiyoriy)" orqali boshqa bo'lim xodimini ham qo'shing. Yarating.
4. Tabelga bo'limning faol xodimlari qatorma-qator qo'shiladi. Har bir ish kuni katagiga ishlangan soatni yozing:
   - Aliyev Anvar (omborchi): har ish kuniga 8 soat;
   - 21.11.2026 shanba kuni inventarizatsiya uchun 4 soat.
5. "Saqlash"ni bosing.
6. Har qatorning jamilarini tekshiring:
   - Ish soatlari 172;
   - Ish kunlari 22;
   - Qo'shimcha 4 (shanba kuni ishlangani uchun);
   - Qatnashmagan 0.
7. Hammasi to'g'ri bo'lsa, ro'yxatdagi "Tasdiqlash" tugmasini bosing va tasdiqlang.
8. Ish haqi tasdiqlangandan va to'langandan keyin tabelni "Yopish" bilan yakunlang.

- **Xodim qo'shish**: tahrir sahifasidagi "Tabelga xodim qo'shish" — bir nechta xodimni birdan tanlash mumkin.
- **Ko'rib chiqish / Chop etish / PDF olish / Excelga eksport** — rasmiy tabel shakli ("ISH VAQTI HISOBOTI").
- **Exceldan import**:
  1. Yil va Bo'limni (ixtiyoriy) tanlang, Excel faylini yuklang.
  2. Oy varag'ini tanlab, "Import qilish"ni bosing.
  - Import faqat **qoralama** tabelga qilinadi.

> **Diqqat:**
> - Kelajak oyiga tabel yaratib bo'lmaydi.
> - Bitta oy va bo'lim uchun ikkinchi tabel yaratilmaydi ("Bu oy uchun tabel allaqachon mavjud").
> - Tasdiqlangan tabelni saqlab ham, o'chirib ham bo'lmaydi. Bunda "Faqat qoralama holatidagi tabelni saqlash mumkin" xabari chiqadi.
> - Tabelga ish haqi hisobi bog'langan bo'lsa, uni o'chirib bo'lmaydi.

> **Maslahat:** Bayram kunini (masalan 08.12) bo'sh qoldiring. U ish kuni hisoblanmaydi va "Qatnashmagan"ga tushmaydi. Bayramda ishlangan soat qo'shimcha soat hisoblanadi.

## 9.9 Ishbay ish: operatsiyalar, tariflar, ishbay tabel

### 9.9.1 Ishbay tariflari

**Kim:** texnolog yoki HR   **Menyu:** Kadrlar (HR) → Tabel → Ishbay tabel → Ishbay tariflari → "Tarif qo'shish"

**Operatsiyalar** — "Taglik yig'ish", "Quti yig'ish", "Qadoqlash" kabi ish turlari. Ular ma'lumotnoma sifatida tizimda oldindan kiritilgan bo'ladi. Yangi operatsiya kerak bo'lsa, administratorga murojaat qiling.

| Maydon | Nima kiritiladi | iPack misoli 1 | iPack misoli 2 |
|---|---|---|---|
| Maxsulot | Tayyor mahsulot | iPack P1 Yog'och taglik | iPack B6.1 quti |
| Lavozim | Kim bajaradi | Qadoqlovchi | Qadoqlovchi |
| Operatsiya | Ish turi | Taglik yig'ish | Quti yig'ish |
| Valyuta | | UZS | UZS |
| Tarif (birlik uchun) | 1 dona uchun to'lov | 3 000 | 400 |
| Amal qilishdan / Amal qilishgacha | Davr | 01.11.2026 / bo'sh | 01.11.2026 / bo'sh |
| Faol | | ✔ | ✔ |

(Tariflar misol.)

> **Maslahat:** Tarif o'zgarganda eski tarifga "Amal qilishgacha" sanasini qo'ying va yangi tarif yarating. Ishbay tabelda tarif har yozuvning **sanasi bo'yicha** avtomatik topiladi.

### 9.9.2 Ishbay tabelni to'ldirish

**Menyu:** Kadrlar (HR) → Tabel → Ishbay tabel

Kiritishning ikki usuli bor:

**A) "Ishbay ma'lumot kiritish"** (bitta yozuv uchun). Maydonlar: Xodim, Maxsulot, Ishbay operatsiyasi, Sana, Miqdor (operatsiya miqdori). "Tarif (birlik uchun)" avtomatik to'ladi.

**B) "Jadval orqali kiritish"** (ko'p yozuv uchun):
1. Yil va Oyni tanlab, "Oyni qo'llash"ni bosing.
2. "Yangi qator qo'shish" bilan qatorlar qo'shing: Xodim / Mahsulot / Operatsiya / Sana / Miqdor. Tarif va Summa avtomatik hisoblanadi.
3. Ssenariy misoli — Karimov Botir, P1 taglik yig'ish:
   - 03.11 — 120 dona;
   - 10.11 — 140 dona;
   - 17.11 — 140 dona;
   - jami 400 dona × 3 000 = **1 200 000**.
4. "Saqlash"ni bosing.

**Natija:**
- Xabar chiqadi: "N ta yozuv saqlandi".
- Aynan takroriy qator (xodim, mahsulot, operatsiya va sana bir xil) saqlanmaydi, bu haqda "dublikat" deb ko'rsatiladi.
- Ro'yxatda oy va bo'lim bo'yicha guruh chiqadi: jami miqdor, jami summa, holat **"Tasdiqlanmagan"**.

### 9.9.3 Ishbay tabelni tasdiqlash

1. Ishbay tabel ro'yxatida noyabr / Ishlab chiqarish bo'limi guruhini toping.
2. "Batafsil"da yozuvlarni tekshiring.
3. "Tasdiqlash"ni bosing. Xabar: "Tabel muvaffaqiyatli tasdiqlandi". Guruhda tasdiq sanasi ko'rinadi.

| Holat | Ma'nosi |
|---|---|
| Tasdiqlanmagan | Yozuv qo'shish, tahrirlash ("Grid tahrirlash"), o'chirish mumkin |
| Tasdiqlangan | Shu oy va bo'lim yozuvlari qulflanadi; ishbay hisob yaratish mumkin |

> **Diqqat:**
> - Tasdiqlangandan keyin shu oy va bo'limga yozuv qo'shib, tahrirlab yoki o'chirib bo'lmaydi: "… tabeli allaqachon tasdiqlangan. O'zgartirish mumkin emas."
> - Kelajak sanaga yozuv kiritilmaydi ("Kelajakka tabel kirita olmaysiz").
> - Amaldagi tarif topilmagan yozuv 0 summa bilan hisoblanadi va ish haqi yaratishda ogohlantirish chiqadi.

## 9.10 Ish haqi hisobi

**Kim:** buxgalter (tasdiqlash huquqi bilan)   **Menyu:** Kadrlar (HR) → Ish haqi

### Holatlar

| Holat | Ma'nosi | Mumkin amallar |
|---|---|---|
| Qoralama | Hisob yaratilgan, tekshirilmoqda | Mukofot, jarima va soliq %ni o'zgartirish; qayta yaratish; o'chirish |
| Tasdiqlangan, moliyaga yozilgan (O'tkazilgan) | Finance'ga chiqim yozilgan | Faqat ko'rish, chop etish, eksport |
| Tasdiqlangan (moliyaga yozilmagan) | Eski hisoblarda uchraydi | "Moliyaga yozish" tugmasi |

### 9.10.1 Soatbay hisob yaratish

1. "Soatbay hisob yaratish"ni bosing.
2. Yil 2026, Oy noyabr, Bo'lim "Ishlab chiqarish bo'limi"ni tanlang. "Tabel (ma'lumot manbai)" ro'yxatidan tasdiqlangan noyabr tabelini tanlang.
3. "Hisob yaratish"ni bosing.
4. Aliyev Anvar qatorini tekshiring:
   - Asosiy maosh 5 040 000, Ishlangan soatlar 172;
   - soat stavkasi = 5 040 000 / 168 = 30 000;
   - Hisoblangan = 168 × 30 000 + 4 × 30 000 × 1,5 = **5 220 000**;
   - Daromad solig'i 12% = **626 400**;
   - **To'lovga 4 593 600**.

**Formula:**
- Soat stavkasi = (asosiy maosh + ustama) / fond soati.
- Hisoblangan = oddiy soat × stavka + qo'shimcha soat × stavka × koeffitsient.
- Soliq faqat hisoblangan summadan olinadi.
- **To'lovga = hisoblangan + mukofot − jarima − soliq.**

### 9.10.2 Ishbay hisob yaratish

1. "Ishbay hisob yaratish"ni bosing.
2. 2026, noyabr, Bo'lim "Ishlab chiqarish bo'limi" va shu bo'limning tasdiqlangan ishbay tabelini tanlang. "Hisob yaratish"ni bosing.
3. Karimov Botir qatori:
   - Miqdor 400;
   - Hisoblangan **1 200 000** (ustama bo'lsa, foizi qo'shiladi);
   - Daromad solig'i 12% = 144 000;
   - **To'lovga 1 056 000**.

### 9.10.3 Tekshirish va tuzatish (Qoralama)

Tahrir sahifasida:
- har qatorda **Mukofotlar** va **Jarimalar** maydonlari bor. Qiymat kiritib maydondan chiqsangiz, jamilar darhol qayta hisoblanadi;
- **Xulosa** blokida "Daromad solig'i (%)" bor. Uni o'zgartirsangiz, soliq butun hisob bo'yicha serverda qayta hisoblanadi.

Misol: Aliyev'ga mukofot 300 000 → To'lovga 4 893 600.

**Qayta yaratish:**
- Shu oy, bo'lim va tabel uchun "Soatbay hisob yaratish"ni yana bossangiz, **o'sha qoralama hisob** qayta quriladi.
- Qatorlar tabeldan yangilanadi, qo'lda kiritilgan mukofot, jarima va soliq foizi saqlanadi.
- Takroriy bo'sh hisob paydo bo'lmaydi.

> **Diqqat:**
> - Hisob faqat **tasdiqlangan** tabeldan yaratiladi. Aks holda xabar chiqadi: "Tabel topilmadi yoki tasdiqlanmagan" yoki "Ushbu oy (bo'lim) uchun tasdiqlangan ishbay tabel topilmadi".
> - Bir xodim bir oyda ikki hisobga tushmaydi (masalan bo'lim hisobi va butun kompaniya hisobi). Ikkinchisiga u qo'shilmaydi va "… boshqa (bo'lim yoki butun kompaniya) hisobida allaqachon bor va qo'shilmadi" ogohlantirishi chiqadi.
> - Ishbay xodim soatbay tabelga ham kiritilgan bo'lsa, u soatbay hisobga ham tushadi. Ishbay ishchilarni soatbay tabelga kiritmang (yoki soatlarini bo'sh qoldiring).

### 9.10.4 Tasdiqlash va Finance'ga yozish

1. Qoralama hisobning tahrir sahifasida tanlang:
   - **Moliya hisobi (kassa/bank)**: 9001 Hisob raqam (plastik kartaga o'tkazilsa) yoki 9000 Naqd pul;
   - **Chiqim toifasi**: "Avtomatik (oylik)" yoki aniq toifa.
2. "Tasdiqlash va moliyaga yozish"ni bosing va tasdiqlang.

**Natija:**
- Xabar: "Hisob tasdiqlandi va moliya moduliga chiqim sifatida yozildi".
- Moliya → Tranzaksiyalar'da **Chiqim** paydo bo'ladi:
  - summa — hisobning "To'lovga" jami (masalan 4 593 600 + boshqalar);
  - kontragent "Xodimlar";
  - maqsad "Ish haqi hisobi 2026-11 (…)".
- Tanlangan kassa yoki bank hisobining qoldig'i kamayadi. P&L va kassa oqimida ish haqi xarajati ko'rinadi.
- Tugmani qayta bossangiz ham ikkinchi yozuv yaratilmaydi.
- Tasdiqlangan hisobni qayta hisoblab ham, o'chirib ham bo'lmaydi.

> **Diqqat:** "Avtomatik (oylik)" tanlansa, tizim nomida "oylik" yoki "ish haqi" so'zi bo'lgan chiqim toifasini qidiradi. Topilmasa, hech narsa yozilmaydi va xabar chiqadi: "Ish haqi uchun moliya toifasi topilmadi…". Bunday holda Moliya → Kategoriyalar'da "Oylik ish haqi" toifasini yarating yoki uni qo'lda tanlang.

**Boshqa sahifalar:**
- **Ish haqi ro'yxati** — filtrlar: Yil, Oy, Bo'lim, Tur (Soatbay/Ishbay), Holat. Ustunlarda To'lov summasi ko'rinadi.
- **Batafsil / Chop etish / Excelga eksport** — "ISH HAQI HISOBOTI" shakli. Ustunlar: Tab.№, F.I.O., Lavozim, Asosiy maosh, Ishlangan soatlar, Hisoblangan, Mukofotlar, Jarimalar, Daromad solig'i, To'lovga, Valyuta.

## 9.11 Koreshok (xodim hisob-kitobi)

**Kim:** buxgalter, HR   **Sahifa:** `/Koreshok` (menyuda yo'q, manzil satri orqali ochiladi; tizimga kirish va ruxsat talab qilinadi)

Koreshok xodim so'raganda "menga qancha hisoblandi va qancha to'landi" degan savolga javob beradi.

1. Xodimni tanlang (masalan Aliyev Anvar).
2. "Dan" 01.11.2026, "Gacha" 30.11.2026 sanalarini kiriting.
3. "Oylikni hisobla"ni bosing.

Natijada quyidagilar ko'rinadi:
- Bo'lim, Ishlagan kun, Ishlagan soat;
- soatbay va ishbay bo'yicha alohida: **Hisoblangan**, **To'langan**, **Qoldiq**.

Ish haqi bilan bir xil formula ishlatiladi. To'langan qism Moliya tranzaksiyalaridan (tranzaksiya sanasi bo'yicha) olinadi. Misol: Hisoblangan 4 593 600, To'langan 4 593 600, Qoldiq 0. "Chop etish" bilan varaqa chiqariladi.

## 9.12 Lavozim yo'riqnomalari

**Kim:** HR mutaxassisi   **Menyu:** Hujjatlar → Hujjatlar boshqaruvi → Lavozim yo'riqnomalari

1. "Yangi lavozim yo'riqnomasi qo'shish"ni bosing.
2. Maydonlarni to'ldiring:
   - Lavozim kodi: OMB-01 (takrorlanmasligi kerak);
   - Lavozim nomi: "Omborchi";
   - Bo'lim: Ishlab chiqarish bo'limi;
   - HR Lavozimga bog'lash: Omborchi;
   - Eslatma.
3. **Hujjat kontenti**ni yozing. Matnni muharrirda yozish (qalin, ro'yxat, rasm), shablondan olish yoki "Word yuklash" bilan tayyor .docx'ni yuklash mumkin.
4. Saqlang. Hujjatga **JD-2026-NNNNNN** ko'rinishida kod beriladi, versiya v1.00.

- **Batafsil**: ma'lumotlar, **Versiya tarixi** (har versiya fayli) va "Word hujjatini yuklash (.docx)" tugmasi. Word faylida formatlash va rasmlar saqlanadi.
- **Tahrirlash**: "Saqlash (Yangi versiya yaratish)" — har tahrirda versiya 0,01 ga oshadi (v1.00 → v1.01).

## 9.13 HR Dashboard

**Menyu:** Dashboard → HR Dashboard (yoki Kadrlar (HR) → HR Dashboard). Sahifa sarlavhasi "HR Boshqaruv paneli".

- **Filtr:** Yil, Oy → "Qo'llash".
- **Kartalar:**
  - xodimlar soni (faktik) va rejalashtirilgan soni;
  - oylik ish haqi fondi;
  - ishlangan soatlar va qo'shimcha soatlar;
  - ishbay miqdor va summa;
  - soatbay hisoblangan summa;
  - Finance'dan to'langan ish haqi va qoldiq.
- **Bo'limlar bo'yicha shtat soni:** Bo'lim / Rejalashtirilgan / Faktik / Og'ishish. Kamchilik yoki ortiqcha rang bilan ajratiladi.
- **Ish haqi fondi dinamikasi:** oylar bo'yicha grafik.

## 9.14 HR uchun oylik ish tartibi

| Qachon | Kim | Nima qilinadi |
|---|---|---|
| Yil boshida | HR | Sozlamalar: ish kunlari, yil bayramlari, soliq % |
| Ishga qabul / o'tkazma / bo'shatishda | HR | Xodim kartasi, Transfer, Ishdan bo'shatish (shtatga mos lavozim) |
| Oy boshida (1–2-kun) | HR | Har bo'lim uchun soatbay tabel yaratish |
| Har kuni yoki har hafta | Bo'lim boshlig'i | Soatbay tabelga soat, ishbay tabelga bajarilgan ish (masalan, kunlik ishlab chiqarish faktiga mos P1/B6.1 miqdori) |
| Oy oxirida | Bo'lim boshlig'i | Tabellarni tekshirish va tasdiqlash (soatbay va ishbay) |
| Oy oxirida | Buxgalter | Soatbay va ishbay hisob yaratish, mukofot va jarimalarni kiritish |
| To'lov kuni | Buxgalter / rahbar | "Tasdiqlash va moliyaga yozish" (kassa yoki bank), Finance'da chiqimni tekshirish |
| To'lovdan keyin | HR | Soatbay tabelni "Yopish"; HR dashboardda fond va og'ishlarni ko'rish |

> **Maslahat:** Ishbay tabeldagi P1 taglik miqdorini MES'dagi "Kunlik ishlab chiqarish" fakti bilan solishtiring. Noyabrda 1 200 dona P1 ishlab chiqarilgan bo'lsa, qadoqlovchilar ishbay tabelidagi jami ham shunga yaqin bo'lishi kerak.

## 9.15 Tez-tez uchraydigan savollar va muammolar

**Tabel yaratishda fond soati noto'g'ri chiqdi.**
Sozlamalardagi ish kunlari va bayramlarni tekshiring. Tabel hali qoralama va bo'sh bo'lsa, uni o'chirib, sozlamani tuzatgandan keyin qayta yarating.

**Tabelni saqlab bo'lmayapti.**
Tabel allaqachon tasdiqlangan. Tasdiqlangan tabel o'zgartirilmaydi.

**Ish haqi yaratishda tabel ro'yxati bo'sh.**
Ro'yxatda faqat tasdiqlangan tabellar chiqadi. Avval tabelni tasdiqlang.

**Ishbay hisobda xodim summasi 0.**
Yozuv sanasida amal qiladigan faol ishbay tarifi yo'q. Tarifni (mahsulot, lavozim, operatsiya va sana oralig'i) tekshiring.

**Xodim hisobga qo'shilmadi.**
U shu oy uchun boshqa (bo'lim yoki butun kompaniya) hisobida allaqachon bor. Ogohlantirish matnida ismi yoziladi.

**Tasdiqlashda "moliya toifasi topilmadi".**
Moliya'da nomida "oylik" yoki "ish haqi" bo'lgan chiqim toifasi yarating yoki chiqim toifasini qo'lda tanlang.

**Tasdiqlangan hisobni tuzatish kerak.**
Tasdiqlangan hisob o'zgartirilmaydi va o'chirilmaydi. Farqni keyingi oy hisobida mukofot yoki jarima sifatida kiriting, yoki Moliya'da tuzatuvchi yozuv qiling.

**Transferda lavozim ro'yxati bo'sh.**
Yangi bo'lim uchun shtat jadvalida faol pozitsiya yo'q. Avval Shtat jadvaliga pozitsiya qo'shing.

**Koreshok "Sizga ruxsat berilmagan!" deyapti.**
Rolingizga Koreshok ruxsati berilmagan. Administratorga murojaat qiling.
