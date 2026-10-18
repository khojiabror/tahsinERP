# 1. Umumiy tizim: kirish, foydalanuvchilar, ruxsatlar, xavfsizlik

Bu bo'lim tizimning umumiy qatlamini tekshiradi: tizimga kirish va chiqish, profil sozlamalari, foydalanuvchi va rollar, URL orqali ruxsatsiz kirishni bloklash, bosh sahifa dashboardi, header badge'lari, Admin panel, o'chirilgan yozuvlarni tiklash, xato sahifasi, eksport, sahifa tezligi va hujjatlar xavfsizligi.
Testlarni shu tartibda bajaring. T-CORE-02 da test foydalanuvchilari (demo_admin, demo_omborchi, demo_sotuv) yaratiladi va keyingi barcha bo'limlar ulardan foydalanadi.
Ruxsat testlari uchun ikkita brauzer qulay: birida Developer yoki demo_admin, ikkinchisida (yoki inkognito oynada) cheklangan foydalanuvchi.

---

### T-CORE-01. Tizimga kirish, noto'g'ri parol va chiqish
**Nima tekshiriladi:** Login sahifasi ochiq. Noto'g'ri parolda aniq xabar chiqadi, ko'p marta xato kiritilsa vaqtincha blok qo'yiladi. Chiqqandan keyin ichki sahifalar login so'raydi (audit ID: CORE-S01).
**Kim:** Developer (tizim egasi)   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Brauzerda tizim manzilini oching. Login sahifasi chiqadi.
2. O'z foydalanuvchi nomingizni va ataylab noto'g'ri parolni kiriting.
3. To'g'ri parol bilan kiring.
4. Yuqori o'ngdagi profil rasmini bosing va **Tizimdan chiqish** ni tanlang.
5. Brauzerning "Orqaga" tugmasini bosing, keyin manzil satriga `/SOrder/Index` ni yozib oching.
6. (Ixtiyoriy, test foydalanuvchisi bilan, T-CORE-02 dan keyin) demo_sotuv nomi bilan ketma-ket 5 marta noto'g'ri parol kiriting, keyin 6-marta to'g'ri parolni kiriting.
**Kutilgan natija:**
- 2-qadamda "Kalit so'zi noto'g'ri!" xabari chiqadi va login sahifasida qolasiz.
- 3-qadamda Bosh sahifa ochiladi.
- 5-qadamda hech bir ichki sahifa ochilmaydi, har safar login sahifasiga qaytariladi.
- 6-qadamda 5-xatodan keyin "... 5 marta xato — 15 daqiqaga bloklandi" xabari chiqadi. 15 daqiqa o'tmaguncha to'g'ri parol bilan ham kirib bo'lmaydi ("Juda ko'p noto'g'ri urinish. N daqiqadan keyin qayta urinib ko'ring.").
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-02. Test rollari va foydalanuvchilarini yaratish (bitta tranzaksiyada saqlash)
**Nima tekshiriladi:** Foydalanuvchi, uning roli va surati birga saqlanadi. Xato bo'lsa, "yarim" foydalanuvchi qolmaydi va shu nom bilan qayta yaratish mumkin. Bo'sh parol qabul qilinmaydi (audit ID: CORE-B03).
**Kim:** Developer   **Oldin bajarilgan bo'lishi kerak:** T-CORE-01
**Qadamlar:**
1. Chap menyu: **Admin panel → Rollar → Yangi rol** ni oching. Rol nomi: `DEMO-Omborchi`, Tavsif: `Faqat ombor`. Saqlang.
2. Ro'yxatda DEMO-Omborchi qatoridagi **Huquqlar** sahifasini oching. Sahifa hajmini **Barchasi** ga qo'ying, keyin **Barchasini bekor qilish** ni bosing.
3. Qidiruv maydoniga ketma-ket `Incomes`, `Expenses`, `Inventory` ni yozing. Topilgan qatorlarda **View** va **Change** ni belgilang. **Saqlash** ni bosing.
4. Xuddi shunday `DEMO-Sotuv` rolini yarating (Tavsif: `Faqat CRM`). Huquqlarida avval "Barchasini bekor qilish", keyin `SOrder`, `SalesInvoices`, `Customer`, `SContract` qatorlarida View va Change ni belgilang. Saqlang.
5. **Admin panel → Foydalanuvchilar → Yangi** ni oching. Foydalanuvchi nomi: `demo_omborchi`, To'liq F.I.Sh: `DEMO Omborchi`, Roli: `DEMO-Omborchi`, Kalit so'zi: o'zingiz tanlagan test paroli. Foydalanuvchi sur'ati maydoniga **2 MB dan katta** rasm tanlang. Saqlang.
6. Shu formani o'sha nom bilan, lekin kichik rasm bilan (yoki rasmsiz) qayta saqlang.
7. Xuddi shunday `demo_sotuv` (Roli: DEMO-Sotuv) va `demo_admin` (Roli: Admin) foydalanuvchilarini yarating.
8. Foydalanuvchilar ro'yxatida demo_omborchi qatoridagi **Batafsil** ni oching.
**Kutilgan natija:**
- 5-qadamda "Suratni yuklab bo'lmadi, u 2 MB dan ortiq..." xabari chiqadi va foydalanuvchi ro'yxatda **paydo bo'lmaydi**.
- 6-qadamda "Bu foydalanuvchi nomi allaqachon mavjud." xatosi chiqmaydi, aksincha "Foydalanuvchi muvaffaqiyatli qo'shildi." xabari chiqadi.
- Ro'yxatda 3 ta demo foydalanuvchi to'g'ri rol bilan, "Faol" holatda turadi.
- 8-qadamdagi Batafsil sahifasi 3 soniyadan tez ochiladi va oxirgi harakatlar (ko'pi bilan 100 ta) ko'rinadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-03. Profil sozlamalari: amaldagi parolsiz hech narsa o'zgarmaydi
**Nima tekshiriladi:** Foydalanuvchi faqat o'z profilini o'zgartira oladi. Har qanday o'zgarish uchun amaldagi parol so'raladi. Avval istalgan foydalanuvchi Admin parolini almashtira olardi (audit ID: CORE-S01).
**Kim:** demo_sotuv   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. demo_sotuv bilan kiring. Profil rasmini bosing va **Profil sozlamalari** ni oching.
2. "Foydalanuvchi nomi" maydonini o'zgartirishga urinib ko'ring.
3. Foydalanuvchi F.I.Sh: `DEMO Sotuvchi Test`. "Amaldagi kalit so'zi" ga ataylab noto'g'ri parol yozing va **Saqlash** ni bosing.
4. "Amaldagi kalit so'zi" ga to'g'ri parolni yozing. "Yangi kalit so'zi" va "Yangi kalit so'zini qayta kiriting" maydonlariga har xil qiymat yozing. Saqlang.
5. To'g'ri amaldagi parol bilan yangi parolni ikkala maydonga bir xil yozing. Saqlang.
6. Manzil satriga `/Account/Settings?userName=demo_admin` yozib oching.
7. Tizimdan chiqing, keyin eski parol bilan, so'ngra yangi parol bilan kirib ko'ring.
**Kutilgan natija:**
- 2-qadam: Foydalanuvchi nomi maydoni faqat o'qish uchun, uni o'zgartirib bo'lmaydi.
- 3-qadam: "Amaldagi kalit so'zi noto'g'ri." xabari chiqadi, F.I.Sh saqlanmaydi.
- 4-qadam: "Yangi kalit so'zi va takrori mos kelmadi." xabari chiqadi.
- 5-qadam: Bosh sahifaga o'tadi, yuqoridagi ism "DEMO Sotuvchi Test" ga o'zgaradi.
- 6-qadam: demo_admin ning emas, **o'zingizning** (demo_sotuv) profilingiz ochiladi.
- 7-qadam: eski parol bilan kirib bo'lmaydi, yangi parol bilan kiriladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-04. Developer himoyasi
**Nima tekshiriladi:** Admin roli Developer foydalanuvchisini ko'rmaydi, tahrirlay olmaydi, o'chira olmaydi va hech kimga Developer rolini bera olmaydi (audit ID: CORE-S03).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. Developer bilan **Admin panel → Foydalanuvchilar** ni oching. O'z (Developer) foydalanuvchingizning ID raqamini yozib oling: Tahrirlash havolasi manzilidagi oxirgi raqam, masalan `/User/Edit/1`.
2. demo_admin bilan kiring va **Admin panel → Foydalanuvchilar** ni oching.
3. Rol filtri ro'yxatini oching.
4. Manzil satriga `/User/Edit/<Developer ID>`, keyin `/User/Details/<Developer ID>` va `/User/Delete/<Developer ID>` ni yozib oching.
5. **Yangi** foydalanuvchi formasida Roli = `Developer` tanlab, `demo_test_dev` nomli foydalanuvchi yaratishga urinib ko'ring.
6. demo_sotuv ni tahrirlang, Roli = `Developer` qilib saqlang. Keyin uning rolini tekshiring.
**Kutilgan natija:**
- 2–3-qadam: ro'yxatda ham, rol filtrida ham Developer foydalanuvchi va Developer roli ko'rinmaydi.
- 4-qadam: uchala manzilda ham "404 — Sahifa topilmadi" sahifasi chiqadi.
- 5-qadam: "Birinchi martada 'Developer' role biriktirish mumkin emas." xabari chiqadi, foydalanuvchi yaratilmaydi.
- 6-qadam: demo_sotuv roli **DEMO-Sotuv** bo'lib qoladi, Developer berilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-05. Ruxsat, rol va faollik o'zgarishi darhol kuchga kiradi
**Nima tekshiriladi:** Admin rol huquqlarini yoki foydalanuvchi rolini o'zgartirsa, yoki foydalanuvchini faolsizlantirsa, bu foydalanuvchi qayta kirmasdan darhol ta'sir qiladi. Avval 10–60 daqiqa eski huquq bilan ishlash mumkin edi (audit ID: CORE-S08, CORE-P02).
**Kim:** demo_admin + demo_omborchi (ikki brauzerda)   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. 2-brauzerda demo_omborchi bilan kiring va `/Currency/Index` ni oching.
2. 1-brauzerda demo_admin bilan **Admin panel → Rollar → DEMO-Omborchi → Huquqlar** ni oching. Qidiruvga `Currency` yozing, Index qatorida **View** ni belgilang va saqlang.
3. 2-brauzerda chiqib-kirmasdan `/Currency/Index` ni qayta yuklang (F5).
4. 1-brauzerda Currency View belgisini olib tashlang va saqlang. 2-brauzerda F5 bosing.
5. 1-brauzerda **Foydalanuvchilar → demo_omborchi → Tahrirlash** ni oching, Roli = `DEMO-Sotuv` qilib saqlang. 2-brauzerda `/SOrder/Index` ni, keyin `/Incomes` ni oching.
6. Rolni qaytadan `DEMO-Omborchi` ga o'zgartiring.
7. 1-brauzerda demo_omborchi ni tahrirlang, **Faol** belgisini olib tashlang va saqlang. 2-brauzerda `/Incomes` ni yangilang.
8. 2-brauzerda chiqing va demo_omborchi bilan qayta kirishga urinib ko'ring. Keyin 1-brauzerda **Faol** ni qayta belgilang.
**Kutilgan natija:**
- 1-qadam: "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- 3-qadam: Valyutalar ro'yxati ochiladi (qayta login shart emas).
- 4-qadam: yana "Sizga ruxsat berilmagan!" chiqadi.
- 5-qadam: Sotuv buyurtmalari ochiladi, Kirimlar esa "Sizga ruxsat berilmagan!" ko'rsatadi.
- 7-qadam: Kirimlar sahifasi ham "Sizga ruxsat berilmagan!" ko'rsatadi (foydalanuvchi hali chiqmagan bo'lsa ham).
- 8-qadam: "Foydalanuvchi faol emas. Administrator bilan bog'laning." xabari chiqadi. Faol qilingandan keyin kirish yana ishlaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-06. Cheklangan omborchi URL orqali boshqa modullarga kira olmaydi
**Nima tekshiriladi:** Menyuda ko'rinmaydigan sahifani manzilni qo'lda yozib ochib bo'lmaydi. Avval 20 dan ortiq controller (sotuv buyurtmasi, invoys, valyuta, log va h.k.) har qanday foydalanuvchiga ochiq edi (audit ID: CORE-S02, CORE-P09, CRM-S01, SRM-S01/S02/S03).
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02, T-CORE-05
**Qadamlar:**
1. demo_omborchi bilan kiring. Chap menyuda faqat ombor bo'limlari (Ombor → Operatsiyalar → Qoldiqlar/Kirimlar/Chiqimlar) borligini ko'ring.
2. **Ombor → Operatsiyalar → Kirimlar** ni oching.
3. Manzil satriga birma-bir yozib oching: `/SOrder/Index`, `/SOrder/Create`, `/SalesInvoices/Index`, `/SalesInvoices/Create`, `/Customer/Index`, `/Currency/Index`, `/Log`, `/ProductPlan`, `/MESDashboard`, `/Company/Index`, `/HSCode`.
**Kutilgan natija:**
- 2-qadam: Kirimlar ro'yxati ochiladi.
- 3-qadam: har bir manzilda "Sizga ruxsat berilmagan!" sahifasi chiqadi. Hech bir ro'yxat, forma yoki ma'lumot ko'rinmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-07. Sotuvchi: faqat ko'rish huquqi bor joyda yaratish yopiq
**Nima tekshiriladi:** CRM foydalanuvchisi ombor va sozlamalarga kira olmaydi. "Faqat ko'rish" huquqi bilan yangi hujjat yaratish sahifasi ham yopiq (Create, Edit, Delete o'zgartirish huquqini talab qiladi) (audit ID: CORE-S02).
**Kim:** demo_sotuv (+ demo_admin huquqni o'zgartirish uchun)   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. demo_sotuv bilan **Sotuvlar → Buyurtmalar** ni oching.
2. Manzil satriga yozib oching: `/Warehouse/Index`, `/Unit/Index`, `/Incomes`, `/Expenses`, `/Log`, `/Currency/Index`.
3. demo_admin bilan **Rollar → DEMO-Sotuv → Huquqlar** da qidiruvga `SOrder` yozing. Barcha SOrder qatorlarida **Change** belgisini olib tashlang (View qolsin) va saqlang.
4. demo_sotuv bilan **Sotuvlar → Buyurtmalar** ni yangilang, keyin `/SOrder/Create` ni oching.
5. 3-qadamdagi Change belgilarini qayta qo'ying.
**Kutilgan natija:**
- 1-qadam: Buyurtmalar ro'yxati ochiladi.
- 2-qadam: barchasida "Sizga ruxsat berilmagan!" chiqadi.
- 4-qadam: ro'yxat ochiladi, `/SOrder/Create` esa "Sizga ruxsat berilmagan!" ko'rsatadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-08. Koreshok sahifasi login talab qiladi
**Nima tekshiriladi:** Koreshok (HR) sahifasi avval tizimga kirmagan odamga ham ochiq edi. Endi login va ruxsat talab qilinadi (audit ID: HRM-S01).
**Kim:** anonim (kirmagan) + demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. Inkognito oynada, tizimga kirmasdan, `/Koreshok` manzilini oching.
2. demo_omborchi bilan kirib, `/Koreshok` ni oching.
3. Developer bilan `/Koreshok` ni oching.
**Kutilgan natija:**
- 1-qadam: login sahifasiga yo'naltiriladi, hech qanday xodim ma'lumoti ko'rinmaydi.
- 2-qadam: "Sizga ruxsat berilmagan!" chiqadi.
- 3-qadam: sahifa odatdagidek ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-09. Yangi ruxsat moduli faqat Admin va Developer'ga ochiladi
**Nima tekshiriladi:** Yangi modul qo'shilganda u avtomatik ravishda barcha rollarga ochilmaydi. Faqat Admin/Developer ko'radi, boshqa rollarga admin o'zi beradi (audit ID: CORE-S04).
**Kim:** Developer   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. **Admin panel → Modullar → Yangi** ni oching. Modul nomi: `DEMO-Modul`, Controller: ro'yxatdan istalgan biri (masalan `Koreshok`), Action: `Index`. Saqlang.
2. **Rollar → Admin → Huquqlar** ni oching va qidiruvga `DEMO-Modul` yozing.
3. **Rollar → DEMO-Omborchi → Huquqlar** va **DEMO-Sotuv → Huquqlar** da ham `DEMO-Modul` ni qidiring.
4. Test oxirida **Modullar** ro'yxatidan `DEMO-Modul` ni o'chiring.
**Kutilgan natija:**
- 2-qadam: Admin rolida DEMO-Modul qatorida **View** belgilangan.
- 3-qadam: DEMO-Omborchi va DEMO-Sotuv rollarida DEMO-Modul uchun **View belgilanmagan**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-10. Bosh sahifa: 12 oylik trend to'ldirilgan
**Nima tekshiriladi:** "Savdo konversiyasi trendi" va "Ishlab chiqarish: reja va fakt trendi" grafiklari tanlangan davrdan qat'i nazar oxirgi 12 oyni ko'rsatadi. Avval 12 oydan 11 tasi doim 0 edi (audit ID: CORE-P07).
**Kim:** Developer yoki demo_admin   **Oldin bajarilgan bo'lishi kerak:** CRM bo'limidagi oktyabr oyidagi DEMO sotuv buyurtmasi (DEMO Mebel Savdo MChJ, 60 stol × 1 450 000 = 87 000 000 so'm); MES bo'limidagi 100 stol ishlab chiqarish
**Qadamlar:**
1. **Bosh sahifa** ni oching ("Rahbariyat nazorat paneli").
2. Sana filtrlarini 01.11.2026 – 30.11.2026 qiling (oktyabr davrga kirmaydi), "Barcha omborlar (xomashyo)" ni qoldiring va **Yangilash** ni bosing.
3. "Savdo konversiyasi trendi" grafigida 2026-10 ustuniga sichqonchani olib boring.
4. "Ishlab chiqarish: reja va fakt trendi" grafigida 2026-10 ni ko'ring.
**Kutilgan natija:**
- Grafiklarda 2025-12 dan 2026-11 gacha 12 ta oy bor.
- 3-qadam: 2026-10 dagi buyurtmalar summasi kamida 87 000 000 so'm (oktyabrdagi boshqa buyurtmalar ham qo'shiladi). Davr noyabr bo'lsa ham oktyabr 0 emas.
- 4-qadam: 2026-10 (yoki ishlab chiqarish kiritilgan oy) faktida kamida 100 dona.
- Ma'lumot kiritilgan boshqa oylar ham 0 emas. Sahifa 3 soniyadan tez yangilanadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-11. Bosh sahifa: davrning oxirgi kuni hisobga olinadi, bekor qilingan invoys kirmaydi
**Nima tekshiriladi:** "Hisob-faktura hajmi" va "Savdo buyurtmalari" KPI'lari tanlangan davrning oxirgi kunidagi hujjatlarni ham qo'shadi. Bekor qilingan invoyslar hisobga kirmaydi (audit ID: CORE-P07).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** CRM bo'limidagi DEMO sotuv invoysi (60 stol)
**Qadamlar:**
1. **Sotuvlar → Invoyslar** da DEMO Mebel Savdo MChJ invoysining sanasini va Jami summasini yozib oling (60 × 1 450 000 = 87 000 000 so'm, QQS 12% bilan 97 440 000 so'm).
2. Bosh sahifada davrni "invoys oyining 1-kuni" dan "**aynan invoys sanasi**" gacha qo'ying va **Yangilash** ni bosing. "Hisob-faktura hajmi" ni yozib oling.
3. Shu mijozga, o'sha sana bilan 1 stol (1 450 000 so'm) uchun qo'shimcha test invoysi yarating (CRM bo'limidagi tartibda). Bosh sahifani yangilang.
4. Test invoysining **Batafsil** sahifasida **Bekor qilish** ni bosing va tasdiqlang. Bosh sahifani yangilang.
**Kutilgan natija:**
- 2-qadam: "Hisob-faktura hajmi" 1-qadamdagi invoys summasini o'z ichiga oladi (oxirgi kun tushib qolmaydi).
- 3-qadam: qiymat test invoysining Jami summasiga oshadi (1 450 000, QQS bilan 1 624 000).
- 4-qadam: qiymat 2-qadamdagi songa qaytadi, bekor qilingan invoys hisobga kirmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-12. Header badge'lari: topshiriqlar va qabul qilinmagan kirimlar
**Nima tekshiriladi:** Profil rasmi yonidagi qizil nuqta va menyudagi sonlar to'g'ri. Ular endi har sahifada bazadan so'ralmaydi, 60 soniya keshlanadi (audit ID: CORE-P01).
**Kim:** demo_admin + demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02; Xomashyo omborida "MRP (Moddiy javobgar shaxs)" = demo_omborchi (Sozlamalar → Omborlar → Tahrirlash)
**Qadamlar:**
1. demo_admin bilan **Tasks / Vazifalar → Vazifalar → Yangi** ni oching. Title: `DEMO badge topshirig'i`, Assignee: `demo_omborchi`, Status: bajarilmagan holat (masalan "New"/"Yangi"), Due Date: 25.10.2026. Saqlang.
2. Xomashyo omboriga qabul qilinmagan kirim yarating: **Ombor → Operatsiyalar → Kirimlar → Yangi**, DEMO-XOM-02 "Mebel shurupi 4x30", 100 dona, sana 20.10.2026. Qabul qilmasdan saqlang (yoki ombor bo'limidagi qabul qilinmagan kirimdan foydalaning).
3. demo_omborchi bilan kiring (yoki 1 daqiqa kutib sahifani yangilang). Profil rasmini bosing.
4. Topshiriq holatini "Bajarildi" ga o'zgartiring, kirimni qabul qiling. 1 daqiqa kutib sahifani yangilang.
**Kutilgan natija:**
- 3-qadam: profil rasmi ustida qizil nuqta bor. Ochilgan menyuda "Topshiriqlar" yonida **1** (yoki avvalgi son + 1), "Omborga kirim" yonida qabul qilinmagan kirimlar soni (kamida 1) turadi.
- 4-qadam: ko'pi bilan 60 soniyadan keyin sonlar kamayadi. Hammasi 0 bo'lsa, qizil nuqta yo'qoladi.
- Badge'lar sahifalar orasida o'tishni sekinlashtirmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-13. Admin panel va tizim loglari
**Nima tekshiriladi:** Admin panel tez ochiladi, bugungi kirishlar va loglar soni to'g'ri. Loglar fonda yoziladi va harakatdan keyin bir necha soniyada ko'rinadi. Log sahifasi cheklangan foydalanuvchiga yopiq (audit ID: CORE-P11, CORE-P10, CORE-P09, CORE-B04).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. Chap menyu: **Admin panel** ni oching. "Foydalanuvchilar", "Rollar", "Bugungi kirishlar", "Bugungi loglar" kartochkalaridagi sonlarni yozib oling.
2. Boshqa brauzerda demo_sotuv bilan kiring va **Profil sozlamalari** ni to'g'ri amaldagi parol bilan saqlang.
3. 5 soniya kutib, Admin panelni yangilang. **Oxirgi tizimga kirishlar** va **Oxirgi harakatlar** jadvallarini ko'ring.
4. Chap menyudan **Tizim loglari** ni oching va demo_sotuv bo'yicha bugungi yozuvlarni toping.
5. Admin panel menyusidagi boshqa bo'limlarni (Foydalanuvchilar, Rollar, Modullar, Tizimga kirishlar, O'chirilganlar) birma-bir oching.
**Kutilgan natija:**
- Admin panel 3 soniyadan tez ochiladi.
- 3-qadam: "Bugungi kirishlar" 1 taga, "Bugungi loglar" kamida 1 taga oshgan. Jadvalda demo_sotuv ning kirishi va "demo_sotuv foydalanuvchi ma'lumotlarini saqladi" yozuvi bor, IP manzil ustunida demo_sotuv kompyuterining manzili turadi.
- 4-qadam: yozuv Tizim loglari ro'yxatida ham bor.
- 5-qadam: birorta bo'lim xato sahifasini ko'rsatmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-14. O'chirilgan ombor va moliya hujjatlari tiklanmaydi
**Nima tekshiriladi:** O'chirilgan ombor kirimi, chiqimi, ishlab chiqarish, moliya tranzaksiyasi, xarid invoysi va yo'l varaqasi to'lovini "O'chirilganlar" orqali tiklab bo'lmaydi. Avval ular qoldiq va moliyani qayta hisoblamasdan tiklanardi (audit ID: CORE-B02).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CORE-12 (yoki istalgan DEMO kirim)
**Qadamlar:**
1. **Ombor → Operatsiyalar → Qoldiqlar** da Xomashyo omboridagi DEMO-XOM-02 qoldig'ini yozib oling.
2. **Ombor → Operatsiyalar → Kirimlar** da T-CORE-12 dagi 100 dona DEMO-XOM-02 kirimini o'chiring. Qoldiqni qayta tekshiring.
3. **Admin panel → O'chirilganlar** ni oching. "Modulni tanlang" ro'yxatidan **Ombor kirimlari** ni tanlang va **Yuklash** ni bosing.
4. O'chirilgan kirim qatorida **Tiklash** ni bosing va tasdiqlang.
5. **Qoldiqlar** sahifasini qayta oching.
6. (Ixtiyoriy) Xuddi shunday **Tranzaksiyalar** va **Xarid invoyslari** dagi biror o'chirilgan DEMO yozuvni tiklashga urinib ko'ring.
**Kutilgan natija:**
- 2-qadam: kirim qabul qilingan bo'lsa, qoldiq 100 dona kamayadi. Qabul qilinmagan bo'lsa, o'zgarmaydi.
- 3-qadam: o'chirilgan kirim ro'yxatda ko'rinadi.
- 4-qadam: qizil xabar chiqadi: "Bu turdagi hujjatni tiklab bo'lmaydi: u ombor qoldig'i yoki moliyaviy yozuvlarga ta'sir qiladi. Hujjatni qaytadan kiriting." Kirim o'chirilganlar ro'yxatida qoladi.
- 5-qadam: qoldiq 2-qadamdagi bilan bir xil, o'zgarmagan.
- 6-qadam: xuddi shu xabar chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-15. Ma'lumotnoma o'chirilsa eski hujjatlarda nomi qoladi va tiklanadi (ekspeditor, valyuta)
**Nima tekshiriladi:** Ekspeditor yoki valyuta o'chirilganda eski yo'l varaqalari va to'lovlarda uning nomi va qarzi saqlanadi. U yangi hujjat tanlovida ko'rinmaydi, "O'chirilganlar" dan tiklansa, bog'lanishlar joyida qoladi. Avval eski hujjatlardagi bog'lanish butunlay o'chib ketardi (audit ID: CORE-B01, LOG-B02, SRM-B11).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** Logistika bo'limidagi DEMO Trans Logistik yo'l varaqasi va to'lovi testlari
**Qadamlar:**
1. **Yuk tashish → Operatsiyalar → Yo'l varaqalari** da DEMO Trans Logistik ning yo'l varaqasini toping. Ekspeditor nomi va summasini, shuningdek **Yuk tashish → Oylik hisobot** dagi DEMO Trans Logistik qarzini yozib oling.
2. **Yuk tashish → Ma'lumotnoma va shartnomalar → Tashuvchi kompaniyalar** da DEMO Trans Logistik ni o'chiring.
3. Yo'l varaqalari ro'yxatini, o'sha yo'l varaqasining Batafsil sahifasini va Oylik hisobotni qayta oching.
4. **Yo'l varaqalari → Yangi** formasidagi ekspeditor ro'yxatini oching (saqlamang).
5. **Admin panel → O'chirilganlar → Tashuvchi kompaniyalar → Yuklash** ni oching va DEMO Trans Logistik ni **Tiklash** qiling.
6. Tashuvchi kompaniyalar ro'yxatini va 1-qadamdagi yo'l varaqasini qayta oching.
7. Valyuta uchun: **Sozlamalar → Valyutalar** da Nomi `DEMO valyuta`, Qisqa nomi `DMV` bilan yangi valyuta yarating. DEMO Trans Logistik yo'l varaqasiga **Yuk tashish → To'lovlar → Yangi** orqali 1 000 DMV to'lov kiriting, sana 20.10.2026. Keyin DMV valyutasini o'chiring va To'lovlar ro'yxatini oching. So'ng uni O'chirilganlar → **Valyutalar** dan tiklang.
**Kutilgan natija:**
- 3-qadam: yo'l varaqasida ekspeditor nomi "DEMO Trans Logistik" bo'lib qoladi (bo'sh emas). Summa va Oylik hisobotdagi qarz 1-qadamdagi bilan bir xil.
- 4-qadam: yangi hujjat tanlovida DEMO Trans Logistik ko'rinmaydi.
- 5-qadam: "Ma'lumot muvaffaqiyatli tiklandi." xabari chiqadi.
- 6-qadam: DEMO Trans Logistik ro'yxatda, yo'l varaqasi unga bog'langan holda turibdi.
- 7-qadam: valyuta o'chirilgandan keyin ham to'lov qatorida valyuta "DMV" ko'rinadi. Tiklangach valyuta Valyutalar ro'yxatiga qaytadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-16. O'chirilgan foydalanuvchini tiklash: nom band bo'lsa tiklanmaydi
**Nima tekshiriladi:** O'chirilgan foydalanuvchi tiklanadi. Lekin shu nom bilan yangi faol foydalanuvchi bo'lsa, ikkita bir xil login paydo bo'lmasligi uchun tiklanmaydi (audit ID: CORE-B02).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. **Foydalanuvchilar → Yangi** dan `demo_tiklash` foydalanuvchisini yarating (Roli: DEMO-Sotuv). Keyin uni o'chiring.
2. **O'chirilganlar → Foydalanuvchilar → Yuklash** ni oching va demo_tiklash ni **Tiklash** qiling.
3. demo_tiklash ni yana o'chiring. Shu nom bilan yangi `demo_tiklash` foydalanuvchisini yarating.
4. O'chirilganlar → Foydalanuvchilar dagi eski demo_tiklash ni **Tiklash** qiling.
5. Test oxirida demo_tiklash ni o'chiring.
**Kutilgan natija:**
- 2-qadam: "Ma'lumot muvaffaqiyatli tiklandi.", foydalanuvchi ro'yxatga qaytadi.
- 4-qadam: "Tiklashda xatolik." xabari chiqadi. Foydalanuvchilar ro'yxatida faqat bitta demo_tiklash bor.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-17. Xato sahifasi ichki texnik matnni ko'rsatmaydi
**Nima tekshiriladi:** Kutilmagan xato bo'lganda oddiy foydalanuvchi faqat umumiy xabarni ko'radi. SQL va dastur ichidagi xato matni faqat Developer'ga ko'rinadi (audit ID: CORE-S06).
**Kim:** demo_admin, keyin Developer   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. demo_admin bilan kiring va manzil satriga `/OfficialLetters/DownloadFile?fileId=abc` ni yozib oching (ataylab noto'g'ri parametr).
2. Xuddi shu manzilni Developer bilan oching.
3. `/Bunday/Sahifa/Yoq` manzilini oching.
**Kutilgan natija:**
- 1-qadam: "500 — Kutilmagan xatolik yuz berdi" sahifasi chiqadi: "So'rovni qayta ishlashda server xatoligi yuz berdi...". Inglizcha texnik matn (masalan "The parameters dictionary contains a null entry...") **ko'rinmaydi**.
- 2-qadam: o'sha sahifada Developer uchun texnik xato matni qo'shimcha ko'rinadi.
- 3-qadam: "404 — Sahifa topilmadi" sahifasi chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-18. PDF va Excel eksport tugmalari
**Nima tekshiriladi:** PDF kutubxonasi (taxminan 1,9 MB) endi har sahifada yuklanmaydi, faqat PDF tugmasi birinchi bosilganda yuklanadi. Eksport avvalgidek ishlaydi (audit ID: CORE-P03).
**Kim:** Developer yoki demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** ombor qoldig'i bor (SRM bo'limi)
**Qadamlar:**
1. **Ombor → Operatsiyalar → Qoldiqlar** ni oching.
2. **Excel** tugmasini bosing va faylni oching.
3. **PDF** tugmasini bosing (birinchi bosishda 1–3 soniya kutish mumkin, internet kerak). Ikkinchi marta yana bosing.
4. **Admin panel → Tizim loglari** va **Hujjatlar boshqaruvi → Rasmiy maktublar** ro'yxatlarida ham PDF va Excel ni sinab ko'ring.
**Kutilgan natija:**
- 2-qadam: .xlsx fayl yuklanadi, ustunlar va qatorlar ekrandagi jadval bilan bir xil, DEMO-XOM-01/02 qoldiqlari bor.
- 3-qadam: PDF fayl yuklanadi va ochiladi. Ikkinchi bosish darhol ishlaydi.
- 4-qadam: barcha sahifalarda eksport ishlaydi, brauzerda xato oynasi chiqmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-19. Sahifa tezligi: birinchi ochilish va menyu bo'ylab o'tish
**Nima tekshiriladi:** Login'dan keyingi birinchi sahifa va keyingi sahifalar tez ochiladi. Avval birinchi sahifada ruxsatlar uchun ~210 ta so'rov bo'lardi, har sahifa ~3,5 MB skript yuklardi va jQuery uch marta yuklanardi. Statik fayllar endi keshlanadi va siqiladi (audit ID: CORE-P02, CORE-P03, CORE-P04, CORE-P06).
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CORE-02
**Qadamlar:**
1. Chiqing, keyin demo_admin bilan kiring va Bosh sahifa to'liq chiqquncha vaqtni o'lchang.
2. Menyu orqali birma-bir oching: Sotuvlar → Buyurtmalar, Ombor → Qoldiqlar, Ombor → Kirimlar, Yuk tashish → Yo'l varaqalari, Sozlamalar → Valyutalar, Admin panel.
3. 2-qadamdagi sahifalarni ikkinchi marta oching.
4. (Ixtiyoriy) F12 → Network yorlig'ini oching, "Disable cache" ni o'chiring va Qoldiqlar sahifasini yangilang. Filtrga `jquery`, keyin `pdfmake` yozing.
5. Tizimni 15 daqiqa ishlatmay turing, keyin istalgan sahifani oching.
**Kutilgan natija:**
- 1-qadam: Bosh sahifa 3 soniyadan tez ochiladi.
- 2–3-qadam: har bir sahifa sezilarli kechikishsiz (3 soniyadan kam) ochiladi. Ikkinchi ochilish birinchisidan tezroq yoki teng.
- 4-qadam: alohida jquery fayllari ro'yxatda yo'q yoki bittadan oshmaydi (jQuery `vendors.min.js` ichida). `pdfmake` PDF tugmasi bosilmaguncha yuklanmaydi. Ko'p statik fayllar keshdan olinadi ("disk cache"/"memory cache").
- 5-qadam: kutishdan keyingi sahifa ham 3 soniyadan tez ochiladi. Avvalgidek har 10 daqiqada "sovuq" sekin sahifa bo'lmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CORE-20. Xat matnidagi skript bajarilmaydi va hujjat faylini boshqa modul orqali yuklab bo'lmaydi
**Nima tekshiriladi:** Rasmiy maktub va kompaniya buyrug'i matniga yozilgan HTML yoki skript ko'rish sahifasida ishlamaydi (stored XSS yopilgan). Hujjat fayli faqat o'z moduli orqali va hujjat o'chirilmagan bo'lsa yuklanadi (audit ID: DOC-S01, DOC-S02, HRM-S05).
**Kim:** Developer (yoki maktublarga ruxsati bor demo_admin)   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. **Hujjatlar boshqaruvi → Rasmiy maktublar → Yangi** ni oching. Tashqi kompaniya nomi: `DEMO Mebel Savdo MChJ`, Mavzu: `DEMO XSS test`, Maktub sanasi: 20.10.2026. "Asosiy matn" ga oddiy qalin (bold) matn yozing, keyin shu qatorni qo'shing: `<script>alert('XSS')</script> <img src=x onerror=alert('XSS2')>`. Saqlang.
2. Maktubning **Batafsil** sahifasini oching.
3. Batafsil sahifasida shablon tanlab **Word hujjat yaratish** ni bosing. Fayllar ro'yxatidagi yuklab olish tugmasining havolasini nusxalang (masalan `/OfficialLetters/DownloadFile?fileId=123`) va uni yangi oynada ochib ko'ring.
4. Havoladagi `OfficialLetters` ni `CompanyOrders`, keyin `JobDesc` ga almashtirib oching (fileId o'zgarmasin).
5. Maktubni o'chiring va 3-qadamdagi asl havolani qayta oching.
6. (Ixtiyoriy) **Hujjatlar boshqaruvi → Kompaniya buyruqlari** da xuddi shu 1–2-qadamlarni takrorlang.
**Kutilgan natija:**
- 2-qadam: hech qanday "XSS" ogohlantirish oynasi chiqmaydi. Qalin matn formatlangan holda ko'rinadi, skript esa oddiy matn bo'lib ko'rinadi yoki umuman ko'rinmaydi.
- 3-qadam: Word fayl yuklanadi.
- 4-qadam: ikkala manzilda ham fayl yuklanmaydi, "404 — Sahifa topilmadi" chiqadi.
- 5-qadam: o'chirilgan maktub fayli ham yuklanmaydi (404).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

## Ma'lum cheklovlar
- Yangi **rol** yaratilganda u hamon barcha modullarga "View" bilan yaratiladi (CORE-S04 ning rol qismi). Rol yaratgandan keyin huquqlarni qo'lda tozalang (T-CORE-02 dagi kabi).
- Rol huquqlari sahifasidagi "Barchasini bekor qilish" faqat sahifada ko'rinib turgan qatorlarga ta'sir qiladi. Avval sahifa hajmini "Barchasi" ga qo'ying.
- Tizim loglari va Tizimga kirishlar sahifalarida server tomonidagi sahifalash yo'q: 30 kunlik log bir yo'la yuklanadi (CORE-P09 ning tezlik qismi).
- TempData/Session qulfi (bir foydalanuvchining parallel AJAX so'rovlari navbatda turishi, CORE-P05) o'zgartirilmagan.
- Profil sozlamalari va Foydalanuvchi tahririda kutilmagan xato bo'lsa, ichki xato matni hali ham forma ustida ko'rsatiladi ("Xatolik yuz berdi: ...").
- PDF eksport birinchi bosishda kutubxonani internetdan (cdnjs) yuklaydi. Internetsiz serverda PDF ishlamaydi.
- Header badge'lari keshlangani uchun 60 soniyagacha kechikib yangilanadi. Bu xato emas, shunday mo'ljallangan.
- Litsenziya o'rni (seat) bekor qilinganda foydalanuvchining ruxsat keshi darhol tozalanmaydi (CORE-S08 ning litsenziya qismi).
