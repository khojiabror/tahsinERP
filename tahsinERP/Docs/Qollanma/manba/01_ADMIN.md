# 1. Tizim sozlamalari va boshqaruv

Bu bob iPack'da tahsinERP'ni ishga tayyorlash va keyin kundalik boshqarish haqida. Unda korxona rekvizitlari, foydalanuvchilar, rollar va ruxsatlar, valyuta va CBU kurslari, o'lchov birliklari, omborlar, sex, Admin panel, loglar, o'chirilgan yozuvlar va litsenziya yoritilgan.
Bobdan asosan tizim administratori (Admin roli) foydalanadi. Direktor esa xavfsizlik qoidalari va oylik nazorat ro'yxatini (1.15–1.16) bilishi kerak. Bu yerdagi sozlamalar barcha modullarga ta'sir qiladi. Masalan, ombor turi noto'g'ri belgilansa, sotuv invoysi Savdo omboridan chiqim qila olmaydi. Valyuta kodi noto'g'ri bo'lsa, CBU kursi olinmaydi va hisobotlarda summa so'mga o'girilmaydi.

## 1.1 Asosiy tushunchalar

- **Foydalanuvchi** — tizimga kirish uchun login va parol. Har bir xodimga alohida beriladi. Bitta loginni bir nechta xodim ishlatmaydi, aks holda loglarda kim nima qilgani ko'rinmaydi.
- **Rol** — huquqlar to'plami. Har bir foydalanuvchiga bitta rol biriktiriladi. iPack rollari: Admin, Developer, Texnolog, Xaridchi, Savdogar, Savdo+Ishlab chiqarish, Ta'sischi, Xaridchi+Savdogar, Texnolog+Ishlab chiqarish.
- **Ruxsat moduli** — himoyalangan sahifa yoki sahifalar guruhi (Modul nomi + Controller + Action). Masalan "SOrder" moduli Sotuv buyurtmalari sahifalarini qamraydi.
- **View / Change** — ko'rish va o'zgartirish huquqi. Change bo'lmasa, Create, Edit, Delete, Approve, Receive, Cancel kabi amallar yopiq bo'ladi. Ruxsatlar sahifasida Delete ustuni ham bor, lekin o'chirish amali Change huquqi bilan tekshiriladi.
- **Developer** — tizim egasi (yetkazib beruvchi) roli. Admin uni ro'yxatlarda ko'rmaydi, tahrirlay olmaydi va hech kimga bera olmaydi.
- **Seat (litsenziya o'rni)** — bitta foydalanuvchiga beriladigan litsenziya o'rni. Admin va Developer'dan boshqa xodimlar seatsiz tizimga kira olmaydi.
- **Ombor turi** — "Xomashyo (ta'minot)", "Ishlab chiqarish", "Savdo" yoki oddiy ombor. Tizim avtomatik harakatlarda (masalan, sotuv invoysi chiqimida) omborni shu tur bo'yicha topadi.
- **DLOC** — ombor ichidagi joy (stellaj, qator, yacheyka). Qoldiq joylar bo'yicha kuzatilganda ishlatiladi.
- **Soft-delete** — yozuv bazadan o'chmaydi, "o'chirilgan" deb belgilanadi. Uni Admin panel → O'chirilganlar sahifasida ko'rish va ba'zilarini tiklash mumkin.

## 1.2 Ishga tushirish tartibi (birinchi sozlash)

Yangi o'rnatilgan tizimni quyidagi tartibda sozlang. Har bir qadam keyingisi uchun kerak.

| # | Nima qilinadi | Bo'lim |
|---|---|---|
| 1 | Litsenziya kaliti faollashtiriladi | 1.14 |
| 2 | Korxona rekvizitlari to'ldiriladi | 1.3 |
| 3 | Valyutalar (UZS, USD, RUB, KRW) va kurslar tekshiriladi | 1.8, 1.9 |
| 4 | O'lchov birliklari (dona, kg, m², m, m³) kiritiladi | 1.10 |
| 5 | Sex ("Ishlab chiqarish") kiritiladi | 1.12 |
| 6 | Uchta ombor turi va MRP bilan yaratiladi, kerak bo'lsa DLOC joylari ham | 1.11 |
| 7 | Rollar va ularning ruxsatlari sozlanadi | 1.5, 1.6 |
| 8 | Foydalanuvchilar yaratiladi va ularga seat beriladi | 1.4, 1.14 |
| 9 | Har bir rol bilan bir marta kirib, menyu tekshiriladi | 1.7 |

## 1.3 Korxona ma'lumotlari

**Kim:** Admin   **Menyu:** Sozlamalar → Korxona

Sahifada bitta forma bor. U korxonaning asosiy rekvizitlarini saqlaydi. Bu ma'lumotlar hujjat shablonlarida va elektron hujjatlarda (Счёт-фактура, ТТНК) sotuvchi rekvizitlari sifatida ishlatiladi.

1. **Sozlamalar → Korxona** ni oching.
2. Maydonlarni to'ldiring va saqlash tugmasini bosing. Saqlangach Bosh sahifa ochiladi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Korxona nomi | Rasmiy nomi | "iPack Eco Solutions" MChJ |
| Davlati | Davlat | O'zbekiston |
| Shaxri | Shahar | Andijon |
| DUNS | Korxona STIR'i (9 raqam). Elektron hujjatlarda sotuvchi STIR'i boshqa joyda ko'rsatilmagan bo'lsa, shu yerdan olinadi | 3XXXXXXXX (korxona STIR'i) |
| Telefon | Asosiy telefon | +998 74 XXX XX XX |
| E-mail | Korxona pochtasi | info@ipack.uz |

> **Diqqat:** Direktor ismi (hujjat shablonlaridagi imzo) bu formada yo'q. U bazadagi alohida ustundan olinadi va administrator tomonidan bir marta sozlanadi. Hujjatlarda direktor ismi noto'g'ri chiqsa, tizim administratoriga murojaat qiling.

**Natija:** o'zgarish loglarga "korxona ma'lumotlarini tahrirladi" deb yoziladi.

## 1.4 Foydalanuvchilar

**Kim:** Admin   **Menyu:** Admin panel → Foydalanuvchilar

Ro'yxatda foydalanuvchi nomi, F.I.Sh, e-mail, roli va faolligi ("Faol"/"Nofaol") ko'rinadi. Tepada **Rol bo'yicha filtrlash** va faollik filtri bor.

### Yangi foydalanuvchi yaratish

1. **Admin panel → Foydalanuvchilar → Yangi** ni oching.
2. Maydonlarni to'ldiring (yulduzcha bilan belgilanganlari majburiy).
3. Saqlang. "Foydalanuvchi muvaffaqiyatli qo'shildi." xabari chiqadi.
4. Agar xodim Admin bo'lmasa, unga seat bering (1.14). Seatsiz u tizimga kira olmaydi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Foydalanuvchi nomi * | Login, lotin harflarida, bo'shliqsiz, takrorlanmas | `ombor_aliyev` |
| To'liq F.I.Sh | HR'dagi xodim kartochkasidagi bilan aynan bir xil | Aliyev Sherzod Rustamovich |
| E-mail | Ish pochtasi | sherzod.aliyev@ipack.uz |
| Roli * | Ro'yxatdan rol | Omborchi (1.6 dagi tavsiya bo'yicha yaratilgan rol) |
| Kalit so'zi * | Boshlang'ich parol. Bo'sh bo'lsa qabul qilinmaydi | Vaqtinchalik parol |
| Faolligi (Faol) | Belgilangan bo'lsa kirishi mumkin | Belgilangan |
| Foydalanuvchi sur'ati | Rasm, hajmi cheklangan (2 MB dan kichik) | Xodim fotosurati |

Xato xabarlari:

| Xabar | Sababi |
|---|---|
| "Bu foydalanuvchi nomi allaqachon mavjud." | Shu login bilan faol foydalanuvchi bor |
| "Kalit so'zni kiriting" | Parol bo'sh |
| "Birinchi martada 'Developer' role biriktirish mumkin emas." | Developer rolini tanlab bo'lmaydi |
| "Suratni yuklab bo'lmadi, u 2 MB dan ortiq..." | Rasm katta. Foydalanuvchi yaratilmaydi, kichikroq rasm bilan qayta saqlang |

> **Diqqat:** Foydalanuvchi, rol va rasm birga saqlanadi. Xato chiqsa, "yarim" foydalanuvchi qolmaydi va xuddi shu nom bilan qayta yaratish mumkin.

### Tahrirlash, parolni tiklash, faolsizlantirish

1. Ro'yxatdagi qatorda **Tahrirlash** belgisini bosing.
2. Kerakli maydonlarni o'zgartiring:
   - **Kalit so'zi** — faqat parolni almashtirish uchun. Bo'sh qoldirilsa, eski parol saqlanadi. Xodim parolni unutganida shu yerda yangisini o'rnatasiz.
   - **Roli** — boshqa rolga o'tkazish.
   - **Faol** belgisi — olib tashlansa, xodim tizimga kira olmaydi.
3. Saqlang. "Foydalanuvchi ma'lumotlari muvaffaqiyatli yangilandi."

**Natija:** rol, faollik va huquq o'zgarishlari darhol kuchga kiradi. Xodim tizimdan chiqib-kirishi shart emas, keyingi sahifadayoq yangi huquq ishlaydi. Faolsizlantirilgan xodim keyingi bosgan sahifasida "Sizga ruxsat berilmagan!" ko'radi, qayta kirishga urinsa "Foydalanuvchi faol emas. Administrator bilan bog'laning." xabari chiqadi.

> **Maslahat:** Ishdan ketgan xodimni o'chirmang, faolsizlantiring va seatini olib qo'ying (1.14). Shunda uning eski hujjatlardagi ismi va loglari saqlanib qoladi, seat esa yangi xodimga bo'shaydi.

### Batafsil sahifa

**Batafsil** ni bosganda foydalanuvchi ma'lumotlari, rasmi va oxirgi harakatlari (ko'pi bilan 100 ta) ko'rinadi. Jadvalda Sana, Vaqt, Controller, Amali, IP manzili va MAC manzili ustunlari bor. Xodim qaysi hujjat bilan ishlaganini tez tekshirish uchun qulay.

### O'chirish

**O'chirish** belgisi bilan foydalanuvchi o'chirilgan deb belgilanadi va O'chirilganlar ro'yxatiga o'tadi (1.13).

## 1.5 Rollar

**Kim:** Admin   **Menyu:** Admin panel → Rollar

Ro'yxatda Rol nomi, Tavsif, foydalanuvchilar soni va Huquqlar soni ko'rinadi.

### Yangi rol yaratish

1. **Rollar → Yangi rol** ni oching.
2. **Rol nomi** va **Tavsif** ni kiriting, masalan `Omborchi` va "Xomashyo, ishlab chiqarish va savdo omborlari".
3. Saqlang. "'Omborchi' roli muvaffaqiyatli yaratildi" xabari chiqadi.
4. **Darhol** shu rolning **Huquqlar** sahifasini oching va keraksiz huquqlarni olib tashlang (1.6).

> **Diqqat:** Yangi rol yaratilganda unga **barcha** modullarda View (ko'rish) huquqi avtomatik beriladi. Huquqlarni tozalamasangiz, yangi xodim moliya, ish haqi va boshqa maxfiy sahifalarni ko'radi.

Xato xabarlari: "Rol nomi kiritilishi shart", "Bu nomli rol allaqachon mavjud".

**Tahrirlash va o'chirish.** Rol nomi va tavsifi Tahrirlash orqali o'zgartiriladi. Rolni o'chirishdan oldin undagi foydalanuvchilarni boshqa rolga o'tkazing.

## 1.6 Ruxsatlar (huquqlar) va iPack uchun tavsiya

**Kim:** Admin   **Menyu:** Admin panel → Rollar → (rol qatori) → Huquqlar

Sahifada rolning barcha ruxsat modullari jadvali bor. Ustunlar: Modul, Controller, Action, View, Change, Delete. Qatorlar modul nomi bo'yicha guruhlangan.

Tepadagi tugmalar:

| Tugma | Nima qiladi |
|---|---|
| Barcha View | Ko'rinib turgan barcha qatorlarda View ni belgilaydi |
| Barcha Change | Barcha Change ni belgilaydi |
| Barcha Delete | Barcha Delete ni belgilaydi |
| Barchasini bekor qilish | Barcha belgilarni olib tashlaydi |
| Qidirish... | Modul yoki controller nomi bo'yicha qatorlarni filtrlaydi |
| Sahifa hajmi (10/25/50/100/Barchasi) | Bir sahifadagi qatorlar soni |

### Rolga huquq berish tartibi

1. Huquqlar sahifasida sahifa hajmini **Barchasi** ga qo'ying.
2. Yangi rol bo'lsa, **Barchasini bekor qilish** ni bosing.
3. Qidiruvga `Layout` yozing va View ni belgilang. Bu majburiy, aks holda xodim hech qaysi sahifani, hatto menyuni ham ko'ra olmaydi.
4. Qidiruvga kerakli modul nomini yozing (masalan `Incomes`). Topilgan qatorlarda View va kerak bo'lsa Change ni belgilang.
5. Har bir kerakli modul uchun 4-qadamni takrorlang.
6. **Saqlash** ni bosing.
7. Shu roldagi test foydalanuvchi bilan kirib, menyuni tekshiring.

> **Diqqat:** Tugmalar faqat sahifada ko'rinib turgan qatorlarga ta'sir qiladi. Avval sahifa hajmini "Barchasi" ga qo'ying.

> **Maslahat:** Faqat View berilgan modulda ro'yxat va Batafsil sahifalari ochiladi, Yangi/Tahrirlash/O'chirish esa "Sizga ruxsat berilmagan!" ko'rsatadi. Bo'lim boshlig'iga boshqa bo'lim hujjatlarini "faqat ko'rish" uchun shunday berish qulay.

### Asosiy ruxsat modullari (qidiruv uchun nomlar)

| Soha | Modul nomlari |
|---|---|
| Umumiy | Layout (majburiy), Tasks, TaskKanban, TaskDashboard, TaskStatus, TaskPriority, TaskProject |
| Xarid (SRM) | Supplier, PRequisitions, PContracts, POrders, PInvoices, PurchaseOrderTracking, Tender, PurchasingOrder |
| Ombor | Incomes, Expenses, Inventory, ProductionToSalesTransfer, InventoryReports, Warehouse |
| Muhandislik | Product, ProductCategory, BOM, Norm, Tannarx |
| Ishlab chiqarish | ProductPlan, ProductProduction, QualityInspection, DefectType, MESDashboard, Shop |
| Sotuv (CRM) | Customer, CustomerBase, SContract, SOrder, SalesInvoices, SalesSF, SalesTTNK, SalesReports, CRMDashboard |
| Logistika | Forwarder, FContract, Waybill, WaybillPayment, LogisticsDashboard |
| Moliya | FinAccounts, FinTransactions, FinCategories, FinBalanceCategories, OpeningBalance, FinPlItems, FinPlPlans, FinDashboard, Payment, Shareholder |
| HR | HrDashboard, HrOrgUnits, HrPositions, HrEmployees, HrSalaryGrades, HrStaffingPlan, HrTimesheets, HrPieceRateTimesheets, HrPieceRateTariffs, HrPayroll |
| Hujjatlar | CompanyOrders, OfficialLetters, STDCompanyStandards, JobDesc |
| Sozlamalar | Company, Currency (valyuta kurslari ham shu huquq bilan), Unit, Warehouse, WarehouseLocations |
| Boshqaruv | AdminPanel (Admin panel va O'chirilganlarni tiklash) |

### iPack rollari uchun tavsiya etilgan ruxsatlar matritsasi

Belgilar: **VC** — ko'rish va o'zgartirish, **V** — faqat ko'rish, **—** — yo'q. Barcha rollarga **Layout (V)** va **Tasks (VC)** beriladi. Omborchi, Buxgalter, HR va Logist rollari iPack'da hali yo'q. Ularni 1.5 bo'yicha yaratish tavsiya etiladi.

| Rol | Xarid (SRM) | Ombor | Muhandislik | Ishlab chiqarish | Sotuv (CRM) | Logistika | Moliya | HR | Hujjatlar | Sozlamalar | Admin panel |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Admin | VC | VC | VC | VC | VC | VC | VC | VC | VC | VC | VC |
| Ta'sischi | V | V | V | V | V | V | V | V | V | — | — |
| Xaridchi | VC | V (Kirimlar: VC) | V | V | — | — | — | — | V | — | — |
| Savdogar | — | V (Qoldiqlar) | V (Product) | V | VC | VC | V (to'lovlar) | — | V | — | — |
| Xaridchi+Savdogar | VC | V (Kirimlar: VC) | V | V | VC | VC | V | — | V | — | — |
| Texnolog | V | V | VC | V | — | — | — | — | V | — | — |
| Texnolog+Ishlab chiqarish | V (PRequisitions: VC) | V (Expenses: VC) | VC | VC | — | — | — | — | V | — | — |
| Savdo+Ishlab chiqarish | V (PRequisitions: VC) | V (ProductionToSalesTransfer: VC) | V | VC | VC | V | — | — | V | — | — |
| Omborchi (yangi) | V | VC | V (Product) | V | — | — | — | — | V | — | — |
| Buxgalter (yangi) | V | V | V (Tannarx) | — | V | V | VC | V (HrPayroll) | V | V (Currency: VC) | — |
| HR (yangi) | — | — | — | V | — | — | — | VC | VC | — | — |
| Logist (yangi) | — | V | — | — | V | VC | — | — | V | — | — |

> **Diqqat:** Ish haqi (HrPayroll), Moliya va Admin panel huquqlarini faqat zarur xodimlarga bering. Ish haqi tasdiqlanganda Moliyada avtomatik chiqim yoziladi, shuning uchun HrPayroll'ga Change huquqi faqat HR boshlig'i va buxgalterda bo'lsin.

> **Maslahat:** Kirimni faqat ombor egasi (MRP) yoki Admin o'chira oladi. Shuning uchun xaridchiga Kirimlar uchun Change berilganda ham, u qabul qilingan kirimni buzib qo'ya olmaydi.

## 1.7 Ruxsat modullari (Admin uchun texnik bo'lim)

**Kim:** Admin, Developer   **Menyu:** Admin panel → Modullar

Bu yerda tizimdagi himoyalangan sahifalar ro'yxati saqlanadi. Ustunlar: Modul nomi, Controller, Action, Parametr va shu modulga ruxsati bor rollar.

1. **Yangi modul qo'shish** — Modul nomi, Controller (ro'yxatdan), Action va ixtiyoriy Parametr kiritiladi. "Permission moduli muvaffaqiyatli yaratildi".
2. **Ruhsat yaratilmagan Controllerlar** — ruxsat moduli hali yo'q sahifalar ro'yxati. **Default modullarni yaratish** tugmasi tanlangan controller uchun standart modullarni qo'shadi.

> **Diqqat:** Yangi modul qo'shilganda u faqat **Admin** va **Developer** rollariga ochiq bo'ladi. Boshqa rollarga uni 1.6 bo'yicha o'zingiz berasiz. Bu yangi sahifa beixtiyor hammaga ochilib qolmasligi uchun shunday qilingan.

> **Maslahat:** Bu bo'lim yangi versiya o'rnatilgandan keyin kerak bo'ladi. Odatiy ishda modullarni o'zgartirmang. Mavjud modulni o'chirish shu sahifani barcha rollar uchun yopib qo'yishi mumkin.

Xato xabarlari: "Modul nomi kiritilishi shart", "Controller tanlanishi shart", "Bu modul, controller va action kombinatsiyasi allaqachon mavjud".

## 1.8 Valyutalar

**Kim:** Admin, Buxgalter   **Menyu:** Sozlamalar → Valyutalar

Ro'yxatda Valyuta nomi va Qisqa nomi bor. iPack'da UZS, USD, RUB, KRW ishlatiladi.

1. **Yangi** ni bosing.
2. Maydonlarni to'ldirib, **Saqlash** ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Nomi | To'liq nomi | AQSh dollari |
| Qisqa nomi | **Xalqaro 3 harfli kod, katta harflarda** | USD |
| Tavsif | Izoh (majburiy maydon) | Import yog'och to'lovlari uchun |

| Valyuta | Qisqa nomi | Izoh |
|---|---|---|
| O'zbek so'mi | UZS | Asos valyuta. Barcha hisobotlar shu valyutada |
| AQSh dollari | USD | Import xomashyo |
| Rossiya rubli | RUB | Rossiyadan xarid |
| Koreya voni | KRW | Koreya uskunalari va UzSaemyung, UzDongyang bilan hisob-kitob |

> **Diqqat:** Qisqa nomi CBU kodi bilan aynan bir xil bo'lishi kerak (USD, RUB, KRW). "$" yoki "dollar" deb yozilsa, kurs CBU'dan olinmaydi va bu valyutadagi hujjatlar hisobotlarda so'mga o'girilmaydi. So'm uchun UZS yozing.

> **Diqqat:** Valyutani o'chirsangiz, eski hujjatlarda (to'lovlar, ish haqi) valyuta nomi saqlanadi, lekin u yangi hujjat tanlovida ko'rinmaydi. Xato o'chirilgan valyutani O'chirilganlar → Valyutalar dan tiklash mumkin.

## 1.9 Valyuta kurslari (CBU)

**Kim:** Admin, Buxgalter   **Menyu:** menyuda alohida band yo'q. Sotuvlar → CRM Dashboard dagi "valyuta kurslari" havolasi orqali yoki manzil satriga `/CurrencyRates` yozib ochiladi. Huquq Valyutalar (Currency) bilan bir xil.

Kurs bu 1 birlik valyutaning necha so'm turishi. CRM, Moliya va Logistika hisobotlari valyutadagi summani hujjat sanasidagi kurs bilan so'mga o'giradi.

Tizim kursni quyidagi tartibda qidiradi:
1. Kurslar jadvalida (qo'lda kiritilgan yoki avval CBU'dan saqlangan) shu sana uchun kurs bormi.
2. Yo'q bo'lsa, cbu.uz arxividan olinadi va jadvalga saqlanadi.
3. Internet yoki CBU javob bermasa, jadvaldagi eng yaqin oldingi sana kursi olinadi.
4. Umuman topilmasa, hujjat summasi hisobotga qo'shilmaydi va sahifada ogohlantirish chiqadi.

### Kurslarni ko'rish

1. **Valyuta** (yoki "Barchasi"), **Dan** va **Gacha** sanalarini tanlang. Standart davr oxirgi 30 kun.
2. **Ko'rsatish** ni bosing. Jadvalda Sana, Valyuta, Kurs (so'm) va Manba (CBU yoki MANUAL) ko'rinadi.

### Kursni qo'lda kiritish yoki tuzatish

1. "Kursni qo'lda kiritish" blokida valyuta, sana va kursni kiriting, masalan USD, 03.11.2026, 12 850.
2. **Saqlash** ni bosing. "Kurs saqlandi." Shu sana uchun kurs bor bo'lsa, u almashtiriladi.

### CBU'dan davr bo'yicha yuklash

1. "CBU'dan davr bo'yicha yuklash" blokida valyuta va davrni tanlang, masalan RUB, 01.11.2026 – 30.11.2026.
2. **Yetishmayotganlarni yuklash (≤ 92 kun)** ni bosing.
3. Natija: "CBU'dan 30 ta kurs yuklandi." Ba'zi kunlar uchun javob kelmasa, "..., N ta sana uchun javob olinmadi." deb yoziladi.

> **Diqqat:** Davr 92 kundan uzun bo'lmasligi kerak, kelajak sanalar esa bugungi kungacha qisqartiriladi. Jadvalda allaqachon bor sanalar qayta yuklanmaydi.

> **Maslahat:** Har oy oxirida 1.16 dagi ro'yxat bo'yicha o'tgan oyning USD, RUB, KRW kurslarini yuklab qo'ying. Shunda oylik hisobotlar internetga bog'liq bo'lmaydi va tez ochiladi.

## 1.10 O'lchov birliklari

**Kim:** Admin, Texnolog   **Menyu:** Sozlamalar → O'lchov birliklari

Ro'yxatda Birlik nomi, Qisqa nomi va Kodi bor.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Birlik nomi | To'liq nomi | Kvadrat metr |
| Qisqa nomi | Hujjatlarda ko'rinadigan qisqa yozuv | m² |
| Kodi | Ichki yoki rasmiy kod | 055 |

iPack uchun kerakli birliklar: **dona** (taxta "Yog'och 100*50*6000", tayyor P1, B6.1), **m²** (karton "Karton_2090x1370"), **kg** (mix, shurup), **m** (selofan, lenta), **m³** (yog'och hajmi, logistika).

> **Diqqat:** Mahsulotning asosiy birligini keyin o'zgartirmang. Ombor qoldig'i shu birlikda yuritiladi. Birliklar orasidagi o'tish (masalan, karton list → m²) mahsulot kartochkasida sozlanadi (Muhandislik bobi).

> **Diqqat:** O'chirilgan birlik eski hujjatlarda saqlanadi, lekin yangi tanlovda ko'rinmaydi. Ishlatilayotgan birlikni o'chirmang.

## 1.11 Omborlar va ombor joylari (DLOC)

**Kim:** Admin   **Menyu:** Sozlamalar → Omborlar (yoki Omborxona → Ombor → Omborlar)

Ro'yxatda Ombor nomi, Tavsif, Turi, Sex, MRP (Mas'ul shaxs), DLOC va Amallar ustunlari bor.

### Ombor yaratish

1. **Yangi ombor qo'shish** ni oching.
2. Maydonlarni to'ldirib saqlang. "Ombor muvaffaqiyatli qo'shildi".

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ombor nomi * | Nomi | Xomashyo ombori |
| Tavsif | Izoh | Kelgan barcha xomashyo shu yerga tushadi |
| Sex | Bog'langan sex (ixtiyoriy) | Ishlab chiqarish (ishlab chiqarish ombori uchun) |
| Ombor turi | "--- Oddiy ombor ---", "Xomashyo (ta'minot)", "Ishlab chiqarish", "Savdo" | Xomashyo (ta'minot) |
| MRP (Mas'ul shaxs) | Omborga mas'ul foydalanuvchi | ombor_aliyev |

iPack uchun to'g'ri sozlama:

| Ombor | Ombor turi | Sex | MRP | Vazifasi |
|---|---|---|---|---|
| Xomashyo ombori | Xomashyo (ta'minot) | — | Xomashyo omborchisi | Xariddan kelgan barcha kirimlar |
| Ishlab chiqarish ombori | Ishlab chiqarish | Ishlab chiqarish | Sex omborchisi yoki sex boshlig'i | Sexga berilgan xomashyo, ishlab chiqarish sarfi |
| Savdo ombori | Savdo | — | Tayyor mahsulot omborchisi | Tayyor mahsulot. Sotuv invoysi shu ombordan avtomatik chiqim qiladi |

> **Diqqat:** Faqat **bitta** Savdo ombori bo'lishi mumkin. Ikkinchisini belgilasangiz, "Savdo ombori allaqachon belgilangan — faqat bitta savdo ombori bo'lishi mumkin." xatosi chiqadi. Ombor turi belgilanmasa, avtomatik amallar (invoys chiqimi, ishlab chiqarish sarfi) omborni topa olmaydi va xato beradi.

> **Maslahat:** MRP maydoni ikki narsaga ta'sir qiladi: shu xodim header'da "Omborga kirim" belgisini ko'radi va omborga kelgan kirimlarni qabul qiladi. Har bir omborga albatta MRP biriktiring.

### Omborni o'chirish

Quyidagi hollarda omborni o'chirib bo'lmaydi va sabab ko'rsatiladi:

| Xabar | Nima qilish kerak |
|---|---|
| "Omborda qoldiq bor (N). Avval qoldiqni boshqa omborga o'tkazing yoki inventarizatsiya qiling." | Qoldiqni o'tkazing |
| "Ombor DLOC joylarida lot qoldig'i bor (N)." | DLOC'dagi lotlarni bo'shating |
| "Omborga N ta qabul qilinmagan kirim bor. Avval ularni qabul qiling yoki rad eting." | Kirimlarni yakunlang |
| "Ombor ishtirokidagi N ta yakunlanmagan o'tkazma bor." | O'tkazmalarni yakunlang |

### Ombor joylari (DLOC)

**Menyu:** Omborxona → Ombor → DLOC (joylashuvlar)

Ombor ichida joylar bo'yicha qoldiq yuritilsa ishlatiladi. Masalan Xomashyo omborida taxta uchun "A-01" stellaji, karton uchun "B-01" joyi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ombor * | Qaysi omborga tegishli | Xomashyo ombori |
| Joy kodi (DLOC) * | Qisqa kod | A-01 |
| Nomi | Tushunarli nom | Taxta stellaji 1 |
| Min sig'im / Max sig'im | Joy sig'imi (asosiy birlikda) | 0 / 2 000 |
| Lot aralashtirishga ruxsat | Bir joyda turli partiyalar tursa bo'ladimi | Belgilangan |
| Mahsulot aralashtirishga ruxsat | Bir joyda turli mahsulotlar tursa bo'ladimi | Belgilanmagan |
| Faol | Joy ishlatiladimi | Belgilangan |

Qoidalar: Max sig'im Min'dan kichik bo'lmasligi kerak. Joyda qoldiq bo'lsa, Max'ni qoldiqdan kam qilib bo'lmaydi va joyni o'chirib bo'lmaydi ("DLOC da N qoldiq bor — o'chirib bo'lmaydi. Avval qoldiqni boshqa joyga o'tkazing.").

## 1.12 Sex

**Kim:** Admin, Texnolog   **Menyu:** menyuda alohida band yo'q, manzil satriga `/Shop` yozib ochiladi ("TSexlar" sahifasi). "Shop" ruxsat moduli kerak.

1. **TSex qo'shish** ni bosing.
2. **TSex nomi** (masalan "Ishlab chiqarish") va **Batafsil** (izoh) ni kiriting, saqlang.

Sex ishlab chiqarish omboriga (1.11) va ishlab chiqarish rejalariga bog'lanadi. iPack'da bitta sex bor: "Ishlab chiqarish".

## 1.13 Admin panel, loglar va o'chirilgan yozuvlar

**Kim:** Admin   **Menyu:** Admin panel

Admin panelning chap menyusida quyidagi bandlar bor: Bosh sahifa, Foydalanuvchilar, Rollar, Modullar, Tizim loglari, Tizimga kirishlar, O'chirilganlar, Litsenziyalar.

### Admin panel bosh sahifasi

| Element | Nima ko'rsatadi |
|---|---|
| Foydalanuvchilar, Rollar | Jami soni |
| Bugungi kirishlar | Bugun tizimga necha marta kirilgan |
| Bugungi loglar | Bugun nechta amal yozilgan |
| Oxirgi tizimga kirishlar | Foydalanuvchi, sana, IP |
| Oxirgi harakatlar | Kim, qachon, nima qildi |
| Foydalanuvchilar jadvali | F.I.Sh, Login, Rol, Holat (Faol/Nofaol) |
| Rollar jadvali | Rol nomi, Huquqlar soni, **Boshqarish** tugmasi |

### Tizim loglari

**Menyu:** Admin panel → Tizim loglari

Har bir yozish amali (yaratish, tahrirlash, o'chirish, tasdiqlash) loglanadi. Ustunlar: Sana, Vaqt, Foydalanuvchi, Modul, Controller, Action, IP manzili, MAC manzili.

1. **Dan** va **Gacha** sanalarini tanlang. Standart holatda oxirgi 30 kun ko'rsatiladi.
2. Qidiruv maydoniga foydalanuvchi yoki modul nomini yozing, masalan "SalesInvoices".
3. Kerak bo'lsa **Excel** yoki **PDF** ga eksport qiling.

> **Maslahat:** Uzoq davr tanlamang. Log sahifasi tanlangan davrning barcha yozuvlarini birdaniga yuklaydi, shuning uchun 1–3 oylik davr sekin ochilishi mumkin. Aniq hodisani qidirganda davrni bir necha kun bilan cheklang.

> **Diqqat:** Loglar amaldan keyin bir necha soniyada paydo bo'ladi. Darhol ko'rinmasa, 5 soniyadan keyin yangilang.

### Tizimga kirishlar

**Menyu:** Admin panel → Tizimga kirishlar

Kim, qachon va qaysi kompyuterdan (IP, MAC) kirganini ko'rsatadi. Filtrlari: Dan, Gacha (standart 30 kun). Begona IP yoki ish vaqtidan tashqari kirishlarni shu yerda tekshiring.

### O'chirilganlar

**Menyu:** Admin panel → O'chirilganlar

1. **Modulni tanlang** ro'yxatidan modulni tanlang, masalan "Tashuvchi kompaniyalar".
2. **Yuklash** ni bosing. O'chirilgan yozuvlar ID, asosiy nom va qo'shimcha ustunlar bilan chiqadi. Qidiruv maydonidan foydalanish mumkin.
3. Kerakli qatorda **Tiklash** ni bosing va tasdiqlang. "Ma'lumot muvaffaqiyatli tiklandi." Tiklash ham logga yoziladi.

Nima tiklanadi, nima tiklanmaydi:

| Tiklanadi (ma'lumotnoma va boshqaruv yozuvlari) | Tiklanmaydi (qoldiq yoki moliyaga ta'sir qiladi) |
|---|---|
| Xaridorlar, Xaridor ma'lumotnoma, Ta'minotchilar, Ta'minotchi ma'lumotnoma | Ombor kirimlari |
| Sotuv shartnomalari va buyurtmalari, Xarid arizalari, shartnomalari va buyurtmalari | Ombor chiqimlari |
| Yo'l varaqalari, Logistika shartnomalari, Tashuvchi kompaniyalar, Transport turlari | Ishlab chiqarish (kunlik ishlab chiqarish yozuvlari) |
| Maxsulotlar, Maxsulot kategoriyalari, Ishlab chiqarish rejalari va jarayonlari, Norm versiyalari, Nukson turlari va mahsulotlari, Chiqindilar, Sexlar, Tannarx | Tranzaksiyalar (moliya) |
| Moliya hisoblari, kategoriyalari, Balans kategoriyalari, P&L rejalar va elementlar, Aksiyadorlar | Xarid invoyslari |
| O'lchov birliklari, Valyutalar, Omborlar | Yo'l varaqasi to'lovlari |
| Foydalanuvchilar, Rollar | |
| Vazifalar, Task loyihalari, statuslari, prioritetlari | |
| Kompaniya buyruqlari, Rasmiy maktublar, Korxona standartlari, Lavozim yo'riqnomalari | |
| Xodimlar, Lavozimlar, Tashkiliy birliklar, Ish haqi tariflari, Shtat jadvali, Ishbay tariflari | |

Tiklanmaydigan hujjatda shu xabar chiqadi: "Bu turdagi hujjatni tiklab bo'lmaydi: u ombor qoldig'i yoki moliyaviy yozuvlarga ta'sir qiladi. Hujjatni qaytadan kiriting." Yozuv O'chirilganlar ro'yxatida qoladi, qoldiq va moliya o'zgarmaydi.

> **Diqqat:** Foydalanuvchi tiklanmaydi, agar shu login bilan yangi faol foydalanuvchi allaqachon yaratilgan bo'lsa ("Tiklashda xatolik."). Tizimda ikkita bir xil login bo'lishiga yo'l qo'yilmaydi.

> **Maslahat:** Ma'lumotnoma yozuvi (masalan, Osiyo Trans Kargo) o'chirilganda eski yo'l varaqalarida nomi va qarzi saqlanadi. Tiklangandan keyin u yangi hujjatlarda yana tanlanadi va eski bog'lanishlar joyida qoladi.

## 1.14 Litsenziya

**Kim:** faqat Admin (va Developer)   **Menyu:** profil rasmi → Litsenziya (yoki Admin panel → Litsenziyalar)

Litsenziya ikki darajada ishlaydi. **Kompaniya litsenziyasi** tizimning muddati, tarifi va maksimal foydalanuvchilar sonini belgilaydi. **Seat** esa har bir xodimga beriladigan o'rin.

Sahifa bo'limlari:

| Bo'lim | Nima bor |
|---|---|
| Kompaniya litsenziyasi holati | Holat (Faol / Muddati tugagan / Boshlanmagan / Faqat o'qish rejimi), Tarif, Qolgan kun, Boshlanish, Tugash, Seat (band / jami; cheksiz bo'lsa "∞"), Kalit formati, O'rnatma ID |
| Kalitni faollashtirish | Yetkazib beruvchidan olingan kalitni joylashtirish maydoni va **Faollashtirish** tugmasi |
| Markaziy panel | **Panelga ulash**, **Hozir tekshirish** (holat: Ulangan / Ulanmagan) |
| Xodimlar seat boshqaruvi | Har xodim uchun: **Seat berish**, **Uzaytirish**, **Olib qo'yish** |
| Litsenziyalar tarixi | Avvalgi kalitlar, muddatlari, kim faollashtirgan |

### Kalitni faollashtirish

1. "O'rnatma ID" qiymatini **Nusxa olish** bilan oling va yetkazib beruvchiga yuboring.
2. Olingan kalitni "Litsenziya kalitini shu yerga joylashtiring" maydoniga qo'ying.
3. **Faollashtirish** ni bosing. Holat "Faol" ga o'zgaradi.

### Xodimga seat berish

1. "Xodimlar seat boshqaruvi" jadvalida xodimni toping.
2. Kerak bo'lsa muddatni tanlang va **Seat berish** ni bosing. Band seatlar soni oshadi.
3. Xodim ketganda **Olib qo'yish** ni bosing, muddatni cho'zish uchun **Uzaytirish** ni bosing.

Muddat tugashi bilan bog'liq holatlar:

| Holat | Nima bo'ladi |
|---|---|
| Tugashiga 14 kun yoki kamroq qolgan | Admin'ga sahifalarda ogohlantirish banneri chiqadi |
| Muddat tugagan, 3 kunlik imtiyoz davri | Faqat o'qish rejimi: sahifalar ochiladi, lekin hech narsani saqlab bo'lmaydi |
| Imtiyoz davri ham tugagan | "Tizim vaqtincha bloklangan" sahifasi chiqadi. Admin u yerdan yangi kalit kiritadi |
| Xodimning seati yo'q yoki muddati tugagan | Xodim tizimga kira olmaydi, login sahifasida sabab yoziladi |
| Modul tarifga kirmaydi | "Paketingizda yo'q — tarifni oshiring" sahifasi chiqadi |

> **Diqqat:** Kompyuter soatini orqaga surish litsenziyani uzaytirmaydi. Tizim buni aniqlaydi va bloklaydi. Server sanasi va vaqti har doim to'g'ri bo'lsin.

> **Diqqat:** Seat olib qo'yilganda xodimning ochiq sessiyasi darhol yopilmasligi mumkin. Xodimning ishini darhol to'xtatish kerak bo'lsa, foydalanuvchi kartochkasida **Faol** belgisini ham olib tashlang (1.4).

## 1.15 Xavfsizlik qoidalari

Tizimda o'rnatilgan himoya:

| Qoida | Qanday ishlaydi |
|---|---|
| Login bloklash | Bir login va kompyuterdan ketma-ket 5 marta xato parol kiritilsa, 15 daqiqaga blok qo'yiladi. To'g'ri kirish hisoblagichni nolga tushiradi |
| Parollar | Bazada shifrlangan (xeshlangan) ko'rinishda saqlanadi. Administrator ham parolni ko'ra olmaydi, faqat yangisini o'rnatadi |
| Profil | Har bir xodim faqat o'z profilini o'zgartiradi. Har qanday o'zgarish uchun amaldagi parol so'raladi |
| Sessiya | 60 daqiqa harakatsizlikdan keyin tugaydi |
| Ruxsat | Har bir sahifa serverda tekshiriladi. Manzilni qo'lda yozish yordam bermaydi. Huquq, rol va faollik o'zgarishlari darhol kuchga kiradi |
| Developer | Admin uni ko'rmaydi va o'zgartira olmaydi. Uning sahifalari manzil orqali ochilganda "404" chiqadi |
| Xato sahifasi | Oddiy foydalanuvchiga texnik matn ko'rsatilmaydi |
| Loglar | Barcha yozish amallari va kirishlar foydalanuvchi va IP bilan saqlanadi |

Tashkiliy qoidalar (administrator va rahbariyat uchun):

1. Har bir xodimga alohida login bering. Umumiy "ombor", "sotuv" kabi loglar ochmang.
2. Parol kamida 8 belgidan iborat bo'lib, harf va raqam aralash bo'lsin. Tizim parol murakkabligini o'zi tekshirmaydi, shuning uchun bu qoidani xodimlarga yetkazing.
3. Yangi xodimga vaqtinchalik parol bering va birinchi kirishdayoq uni Profil sozlamalarida almashtirishni talab qiling.
4. Admin rolini 1–2 kishiga bering (direktor tayinlagan administrator va zaxira shaxs).
5. Ishdan ketgan xodimni o'sha kuniyoq faolsizlantiring va seatini olib qo'ying.
6. Yangi rolni yaratgach darhol huquqlarini tozalang (1.5).
7. Server kompyuterida soat va sana avtomatik sinxronlansin.

## 1.16 Zaxira nusxa va oylik nazorat: administrator ro'yxati

tahsinERP ma'lumotlari SQL Server bazasida (iPack'da `DBTHSN-IPACK`, lokal server) saqlanadi. Tizimning o'zida zaxira nusxa olish yoki eski loglarni tozalash tugmasi yo'q. Bu ishlar SQL Server vositalari bilan bajariladi.

**Har kuni:**

| # | Ish | Qanday |
|---|---|---|
| 1 | Bazaning to'liq zaxira nusxasi | SQL Server Management Studio → baza ustida Tasks → Back Up..., yoki SQL Server Agent'da kunlik reja. Fayl nomida sana bo'lsin: `DBTHSN-IPACK_2026-11-15.bak` |
| 2 | Nusxani boshqa joyga ko'chirish | Tashqi disk yoki boshqa kompyuter. Server bilan bir diskda saqlamang |
| 3 | Admin panelni ko'rib chiqish | Bugungi kirishlar va oxirgi harakatlarda g'ayrioddiy narsa yo'qligini tekshirish |

**Har hafta:**

| # | Ish |
|---|---|
| 1 | Tizimga kirishlar sahifasida begona IP va ish vaqtidan tashqari kirishlarni tekshirish |
| 2 | Qabul qilinmagan kirimlarni ko'rish (Kirimlar, "qabul qilinmagan" filtri) va omborchilarga eslatish |
| 3 | Bosh sahifadagi signal panelini va muddati o'tgan topshiriqlarni ko'rish |

**Har oy (oy yopilgandan keyin, masalan 1–3 dekabr kunlari noyabr uchun):**

| # | Ish | Qayerda |
|---|---|---|
| 1 | O'tgan oy uchun USD, RUB, KRW kurslarini CBU'dan yuklash va bo'shliq yo'qligini tekshirish | Valyuta kurslari (1.9) |
| 2 | Ishdan ketgan va uzoq ta'tildagi xodimlarni faolsizlantirish, seatlarni qayta taqsimlash | Foydalanuvchilar, Litsenziya |
| 3 | Rollar huquqlarini ko'rib chiqish: lavozimi o'zgargan xodimning roli to'g'rimi | Rollar → Huquqlar |
| 4 | O'chirilganlar ro'yxatini ko'rib chiqish: xato o'chirilgan ma'lumotnoma yozuvlarini tiklash, tiklanmaydigan hujjatlar o'rniga to'g'ri hujjat kiritilganini tekshirish | O'chirilganlar |
| 5 | Litsenziya muddati va band seatlar sonini tekshirish (14 kundan kam qolgan bo'lsa, yetkazib beruvchiga murojaat qilish) | Litsenziya |
| 6 | Oy oxiridagi zaxira nusxani alohida, uzoq muddatga saqlash (masalan `DBTHSN-IPACK_2026-11_oy.bak`) | SQL Server |
| 7 | Zaxira nusxadan test bazaga tiklab ko'rish (har chorakda kamida bir marta) | SQL Server |
| 8 | Server diskida bo'sh joy va baza hajmini tekshirish. Loglar jadvali o'sib boradi. Eski loglarni arxivlash faqat zaxira nusxadan keyin va mutaxassis tomonidan bajariladi | SQL Server |
| 9 | Ombor qoldig'i va moliya hisoblari qoldig'ini tegishli bo'lim boshliqlari bilan solishtirish | Ombor → Oylik hisobot, Moliya → Hisoblardagi qoldiq |

> **Diqqat:** Yangi versiya o'rnatishdan yoki bazaga har qanday skript ishga tushirishdan oldin alohida zaxira nusxa oling. Skriptlar avtomatik ishga tushmaydi, ular yetkazib beruvchi ko'rsatmasi bilan bajariladi.

> **Maslahat:** Server qayta ishga tushirilganda barcha foydalanuvchilar tizimdan chiqib ketishi mumkin. Yangilash va qayta ishga tushirishni ish vaqtidan tashqari rejalashtiring va xodimlarni oldindan ogohlantiring.

## 1.17 Hisobotlar va nazorat sahifalari

| Sahifa | Menyu | Nima uchun |
|---|---|---|
| Admin panel bosh sahifasi | Admin panel | Kunlik umumiy holat: foydalanuvchilar, kirishlar, oxirgi harakatlar |
| Tizim loglari | Admin panel → Tizim loglari | Kim qachon qaysi hujjatni o'zgartirgan. Excel/PDF eksport |
| Tizimga kirishlar | Admin panel → Tizimga kirishlar | Kirishlar tarixi, IP |
| Foydalanuvchi batafsil | Foydalanuvchilar → Batafsil | Bitta xodimning oxirgi 100 ta harakati |
| Rollar → Huquqlar | Admin panel → Rollar | Rolning to'liq huquqlar xaritasi |
| Litsenziya | Profil → Litsenziya | Muddat, seatlar, tarix |
| Valyuta kurslari | `/CurrencyRates` | Hisobotlarda ishlatilgan kurslar va ularning manbasi |

## 1.18 Tez-tez uchraydigan savollar va muammolar

**Yangi xodim kirdi, lekin menyu bo'sh yoki darhol "Sizga ruxsat berilmagan!" chiqadi.**
Rolida `Layout` moduliga View huquqi yo'q. Rollar → Huquqlar da `Layout` ni qidirib View ni belgilang.

**Yangi xodim moliya va ish haqi sahifalarini ko'ryapti.**
Rol yaratilganda barcha modullarga View berilgan va tozalanmagan. Huquqlar sahifasida "Barchasi" → "Barchasini bekor qilish" → keraklilarini qayta belgilang.

**Xodim "Litsenziya" xabari bilan kira olmayapti.**
Unga seat berilmagan yoki seat muddati tugagan. Litsenziya → Xodimlar seat boshqaruvi da seat bering yoki uzaytiring. Barcha seatlar band bo'lsa, ketgan xodimlarning seatini olib qo'ying.

**Omborchi "Omborga kirim" belgisini ko'rmayapti.**
U hech qaysi omborning MRP'si emas. Sozlamalar → Omborlar → Tahrirlash da MRP maydoniga uni tanlang. Belgi bir daqiqa ichida paydo bo'ladi.

**Sotuv invoysi saqlanmayapti, ombor topilmadi degan xato chiqadi.**
Savdo omborining "Ombor turi" maydoni "Savdo" qilib belgilanmagan. Omborni tahrirlab turini to'g'rilang.

**Hisobotlarda USD'dagi hujjatlar summaga qo'shilmayapti va ogohlantirish chiqyapti.**
Kurs topilmagan. Valyuta qisqa nomi "USD" ekanini tekshiring, keyin Valyuta kurslari sahifasida shu davr uchun "Yetishmayotganlarni yuklash" ni bosing yoki kursni qo'lda kiriting.

**Xato o'chirilgan ombor kirimini tiklab bo'lmayapti.**
Bu ataylab shunday qilingan: tiklash qoldiqni qayta hisoblamaydi. Kirimni qaytadan kiriting va qabul qiling.

**Admin parolini unutdi.**
Ikkinchi Admin foydalanuvchi Foydalanuvchilar → Tahrirlash orqali yangi parol o'rnatadi. Ikkinchi Admin bo'lmasa, yetkazib beruvchiga (Developer) murojaat qiling. Shuning uchun har doim kamida ikkita Admin foydalanuvchi bo'lsin.

**Foydalanuvchini tiklashda "Tiklashda xatolik." chiqdi.**
Shu login bilan yangi foydalanuvchi allaqachon bor. Yangi foydalanuvchidan foydalaning yoki uni o'chirib, eskisini tiklang.

**Huquqni o'zgartirdim, xodimda o'zgarmadi.**
Huquqlar sahifasida **Saqlash** bosilganini tekshiring. Xodim sahifani yangilashi (F5) yetarli, qayta kirish shart emas.
