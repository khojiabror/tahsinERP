# 2. Ta'minot: xarid va ombor

Bu bo'lim xarid zanjirini (ta'minotchi, xarid arizasi, buyurtma, shartnoma, invoys) va omborxonani (kirim, chiqim, o'tkazma, inventarizatsiya, hisobotlar) tekshiradi. Testlarni tartib bilan bajaring: avval T-XAR-01…14, keyin T-OMB-01…18. Har bir test oldingi testda yaratilgan DEMO hujjatlardan foydalanadi.
Sanalar: testlarda 20.10.2026–24.10.2026 berilgan. Agar u sana hali kelmagan bo'lsa, bugungi sanani qo'ying va hisobotlarda shu oyni tanlang. Hujjat raqamlari tizimning joriy yil va oyidan olinadi (masalan PO-2026-10-0001). Shuning uchun quyida "NNNN" deb yozilgan joyda keyingi bo'sh tartib raqami chiqadi.
Ombor qoldig'ini **Omborxona → Ombor → Operatsiyalar → Qoldiqlar** sahifasida tekshiring (filtr: ombor). DEMO mahsulotlar yangi bo'lgani uchun boshlang'ich qoldiq 0.

---

### T-XAR-01. DEMO ta'minotchini yaratish
**Nima tekshiriladi:** Yangi ta'minotchi kiritiladi va keyingi barcha xarid hujjatlarida tanlanadi.
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. **Ta'minot (SRM) → Ta'minotchilar → Ta'minotchilar** ni oching va **Yangi ta'minotchi** tugmasini bosing.
2. Maydonlarni to'ldiring: Nomi = `DEMO Yog'och MChJ`, DUNS = `300000001` (formada alohida INN maydoni yo'q, INN shu yerga yoziladi), Turi = `Mahalliy`, Mamlakat = `O'zbekiston`, Shahar = `Toshkent`, Telefon raqami = `+998 90 000 00 01`.
3. Saqlang.
4. Ro'yxatning qidiruv maydoniga `DEMO` deb yozing.
**Kutilgan natija:**
- Ro'yxatda "DEMO Yog'och MChJ" bitta qator bo'lib chiqadi. Shahar ustunida Toshkent turadi.
- Ro'yxat jadvali bir marta yuklanadi va brauzerda "Cannot reinitialise DataTable" ogohlantirishi chiqmaydi (SRM-B21).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-02. Ta'minotchini o'chirish (ro'yxatdan va Details sahifasidan)
**Nima tekshiriladi:** Ta'minotchini o'chirish endi ikkala yo'l bilan ishlaydi. Oldin ikkalasi ham 500 xato berardi. (audit ID: SRM-B02)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-01
**Qadamlar:**
1. T-XAR-01 dagi kabi ikkita vaqtinchalik ta'minotchi yarating: `DEMO O'chiriladigan 1` va `DEMO O'chiriladigan 2` (Turi = `Mahalliy`).
2. Ro'yxatda "DEMO O'chiriladigan 1" qatoridagi qizil **O'chirish** belgisini bosing va tasdiqlang.
3. "DEMO O'chiriladigan 2" ning Details (ko'rish) sahifasini oching. **O'chirish** sahifasiga o'ting va tasdiqlang.
4. Ro'yxatda `DEMO` bo'yicha qidiring.
**Kutilgan natija:**
- 2-qadamda sahifa yangilanadi va qator yo'qoladi. "Server xatoligi yuz berdi" xabari chiqmaydi.
- 3-qadamdan keyin ro'yxat sahifasiga qaytadi. 500 xato sahifasi chiqmaydi.
- Ro'yxatda faqat "DEMO Yog'och MChJ" qoladi. Ikkala yozuv bazadan butunlay o'chmaydi, faqat yashiriladi (soft-delete).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-03. Xarid arizasini yaratish (avtomatik raqam, qoralama)
**Nima tekshiriladi:** Ariza raqami bo'sh qoldirilsa, tizim uni o'zi beradi. Ariza doim Draft holatida yaratiladi. Ro'yxatda bo'lim va so'rovchi ko'rinadi. (audit ID: SRM-B03, SRM-B12, SRM-B14, SRM-B19)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-01
**Qadamlar:**
1. **Ta'minot (SRM) → Xarid jarayoni → Xarid arizalari → Yangi ariza** ni oching.
2. Ariza raqami = **bo'sh qoldiring**, Ariza sanasi = `20.10.2026`, Bo'lim = mavjud ishlab chiqarish bo'limi, So'rovchi = `DEMO Aliyev Anvar` (agar u hali yo'q bo'lsa, istalgan xodim), Xarid qilish maqsadi = `DEMO: 100 ta ofis stoli uchun xomashyo`.
3. Mahsulotlar jadvaliga 2 ta qator qo'shing:
   - `DEMO-XOM-01` Qayin taxta 18 mm, Miqdor `500`, Birlik `m²`, Taxminiy narx `85000`, Muhimlik `High`, Kerakli sana `25.10.2026`.
   - `DEMO-XOM-02` Mebel shurupi 4x30, Miqdor `20000`, Birlik `dona`, Taxminiy narx `120`, Muhimlik `Medium`.
4. Saqlang va **Xarid arizalari** ro'yxatiga qayting.
**Kutilgan natija:**
- Saqlashda "Ariza raqami majburiy" xatosi chiqmaydi. Arizaga `PR-2026-NNNN` ko'rinishidagi raqam beriladi.
- Ro'yxatda ariza Draft holatida turadi. Bo'lim va So'rovchi ustunlari to'ldirilgan (bo'sh emas). Status filtri sahifani qayta yuklamasdan jadvalni yangilaydi.
- Details sahifasida 2 ta mahsulot qatori bor. "Tasdiqlash jarayoni" jadvalidagi bosqichlar Pending holatida.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-04. Arizani yuborish va bitta tasdiq bilan tasdiqlash
**Nima tekshiriladi:** Ariza Draft → Submitted → Approved oqimidan o'tadi. Biznes qarori bo'yicha bitta tasdiq yetarli. Buyurtmaga faqat tasdiqlangan ariza aylantiriladi. (audit ID: SRM-B04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-03
**Qadamlar:**
1. T-XAR-03 dagi arizaning Details sahifasini oching. **Buyurtmaga aylantirish** tugmasi yo'qligiga ishonch hosil qiling.
2. **Tasdiqlashga yuborish** ni bosing va tasdiqlang.
3. Shu sahifada **Tasdiqlash** ni bosing va tasdiqlang.
4. "Tasdiqlash jarayoni" jadvaliga qarang.
**Kutilgan natija:**
- 2-qadamdan keyin "Talabnoma tasdiqlashga yuborildi." xabari chiqadi va holat Submitted bo'ladi. Endi **Tasdiqlash** va **Rad etish** tugmalari ko'rinadi.
- 3-qadamdan keyin "Talabnoma tasdiqlandi." xabari chiqadi va holat Approved (yashil) bo'ladi.
- Barcha tasdiqlash bosqichlari bir vaqtda Approved bo'ladi. Tasdiqlovchi ustunida demo_admin ning F.I.Sh. turadi.
- **Buyurtmaga aylantirish** tugmasi paydo bo'ladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-05. Arizani sabab bilan rad etish
**Nima tekshiriladi:** Sababsiz rad etib bo'lmaydi. Rad etilgan arizani tasdiqlab yoki buyurtmaga aylantirib bo'lmaydi. (audit ID: SRM-B04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-03
**Qadamlar:**
1. Yangi ariza yarating: maqsad = `DEMO: rad etish sinovi`, bitta qator `DEMO-XOM-02` 1000 dona. Keyin **Tasdiqlashga yuborish** ni bosing.
2. "Rad etish sababi" maydonini bo'sh qoldirib **Rad etish** ni bosing.
3. Sabab kiriting: `DEMO: miqdor asoslanmagan` va **Rad etish** ni bosing.
4. Brauzer manzil satriga `/PRequisitions/ConvertToOrder/<shu ariza ID>` yozib oching.
**Kutilgan natija:**
- 2-qadamda forma yuborilmaydi: maydon majburiy deb ko'rsatiladi.
- 3-qadamdan keyin "Talabnoma rad etildi." xabari chiqadi. Holat Rejected (qizil), sahifada "Rad etish sababi: DEMO: miqdor asoslanmagan" yozuvi turadi.
- **Tasdiqlash** va **Buyurtmaga aylantirish** tugmalari ko'rinmaydi.
- 4-qadamda Details sahifasiga qaytadi va "Faqat tasdiqlangan talabnomani xarid buyurtmasiga aylantirish mumkin." xabari chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-06. Arizadan xarid buyurtmasi (QQS bilan summa)
**Nima tekshiriladi:** Buyurtma tasdiqlangan arizadan yaratiladi. Raqami PO-YYYY-MM-NNNN formatida bo'ladi. Summa QQS bilan saqlanadi. (audit ID: SRM-B14, SRM-B15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-04
**Qadamlar:**
1. T-XAR-04 dagi arizada **Buyurtmaga aylantirish** ni bosing.
2. Ochilgan buyurtma formasida "Indirect Purchase" ma'lumot bloki chiqqanini va mahsulotlar arizadan to'lganini tekshiring.
3. Buyurtma raqami = bo'sh, Sana = `20.10.2026`, Ta'minotchi = `DEMO Yog'och MChJ`, Valyuta = `UZS`, QQS % = `12`. Narxlar: DEMO-XOM-01 = `85000`, DEMO-XOM-02 = `120`.
4. Saqlang va buyurtma Details sahifasini oching.
**Kutilgan natija:**
- Raqam `PO-2026-10-NNNN` ko'rinishida beriladi (yil va oy joriy sana bo'yicha, tartib raqami 4 xonali).
- Qatorlar: 500 m² × 85 000 = 42 500 000; 20 000 dona × 120 = 2 400 000. QQS'siz jami 44 900 000.
- Jami QQS 12% = 5 388 000. Buyurtma summasi (QQS bilan) = **50 288 000 so'm**. **Xarid buyurtmalari** ro'yxatida ham shu summa turadi.
- Agar saqlashda xato bo'lsa (masalan ta'minotchi tanlanmagan), forma kiritilgan qatorlar bilan qayta ochiladi. Qatorlar yo'qolmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-07. Buyurtmada qator o'chirish va raqam takrorlanmasligi
**Nima tekshiriladi:** O'rtadagi qator o'chirilganda keyingi qatorlar yo'qolmaydi. O'chirilgan buyurtma raqami qayta berilmaydi. (audit ID: SRM-B05, SRM-B14)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-06
**Qadamlar:**
1. **Xarid buyurtmalari → Yangi buyurtma (Direct)** ni oching. Ta'minotchi = `DEMO Yog'och MChJ`, Valyuta = `UZS`, QQS % = `12`.
2. **Qo'shish** tugmasi bilan 3 ta qator kiriting:
   - (1) DEMO-XOM-01, 10 m², 85 000;
   - (2) DEMO-XOM-02, 100 dona, 120;
   - (3) DEMO-XOM-01, 5 m², 85 000.
3. **2-qatorni** qizil tugma bilan o'chiring va saqlang. Berilgan raqamni yozib oling (masalan PO-2026-10-0002).
4. Details sahifasini oching.
5. Shu buyurtmani o'chiring (**O'chirish** → tasdiqlash). Keyin yana bir **Yangi buyurtma (Direct)** yarating: bitta qator DEMO-XOM-02, 10 dona, 120.
**Kutilgan natija:**
- Details sahifasida 2 ta qator bor: 10 m² va 5 m² DEMO-XOM-01. 3-qator tushib qolmagan.
- Shu buyurtma summasi: (10 + 5) × 85 000 = 1 275 000; QQS bilan 1 428 000.
- 5-qadamdagi yangi buyurtma o'chirilgan buyurtma raqamini olmaydi, undan keyingi raqamni oladi (masalan PO-2026-10-0003). Ro'yxatda bir xil raqamli ikkita buyurtma yo'q.
- Oxirgi sinov buyurtmasini ham o'chiring. Zanjirda faqat T-XAR-06 buyurtmasi qolsin.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-08. HS (ТНВЭД) kod: yaratish, ishlatilganini o'chirish taqiqi, ruxsat
**Nima tekshiriladi:** HS kodlarni faqat ruxsati bor foydalanuvchi o'zgartira oladi. O'chirish haqiqatan ishlaydi, lekin ishlatilgan kodni o'chirib bo'lmaydi. (audit ID: SRM-B09, SRM-B10)
**Kim:** demo_admin, demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. demo_admin bilan manzil satrida `/HSCode/Create` ni oching (menyuda bu sahifaga havola yo'q). HsCODE = `9999000001`, ma'lumot = `DEMO Qayin fanera`. Saqlang.
2. Yana bitta vaqtinchalik kod yarating: `9999000002` (`DEMO o'chiriladigan`). Keyin uni ro'yxatda **O'chirish** orqali o'chiring.
3. Chiqing va demo_omborchi bilan kiring. Manzil satrida `/HSCode/Create` ni oching. Keyin `/HSCode/Edit/<9999000001 ning ID si>` ni oching.
**Kutilgan natija:**
- 1-qadamdan keyin `9999000001` ro'yxatda turadi.
- 2-qadamdan keyin `9999000002` ro'yxatdan haqiqatan yo'qoladi va sahifa yangilanganda qayta chiqmaydi.
- 3-qadamda ikkala sahifa o'rniga "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- Ishlatilgan kodni o'chirish sinovi T-XAR-09 ning oxirida.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-09. Xarid shartnomasi (QQS, HS kod, PDF tekshiruvi, tahrir)
**Nima tekshiriladi:** Shartnoma summasi qatorlardagi QQS bilan hisoblanadi. Tahrir sahifasida birlik va HS kod saqlanib qoladi. Hujjat sifatida faqat PDF qabul qilinadi. (audit ID: SRM-B05, SRM-B16, SRM-B20, SRM-B22, SRM-B23, SRM-P02)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-06, T-XAR-08
**Qadamlar:**
1. **Xarid jarayoni → Xarid shartnomalari → Yangi shartnoma (Indirect)** ni oching.
2. Shartnoma raqami = bo'sh, Sana = `20.10.2026`, Ta'minotchi = `DEMO Yog'och MChJ`, Valyuta = `UZS`, Muddat = `31.12.2026`, To'lov shartlari = `DEMO: 30 kun`.
3. Qatorlar:
   - DEMO-XOM-01: Miqdor `500`, Birlik `m²`, Narx `85000`, QQS (%) `12`, HS Code `9999000001`;
   - DEMO-XOM-02: Miqdor `20000`, Birlik `dona`, Narx `120`, QQS (%) `12`.
   Mahsulot tanlovida `DEMO` deb yozib qidiring.
4. "Shartnoma hujjati (PDF)" ga ataylab .xlsx yoki .jpg fayl tanlab saqlang.
5. Faylni haqiqiy PDF ga almashtirib (yoki fayl tanlamasdan) saqlang.
6. Shartnomani **Tahrirlash** da oching. Hech narsani o'zgartirmay saqlang va Details ni oching.
7. `/HSCode` ro'yxatini oching (ro'yxat serverda sahifalanadi — qidiruvga `9999000001` yozing) va shu kodni o'chirishga urining.
**Kutilgan natija:**
- Mahsulot tanlovi qidiruv bilan ishlaydi. Tahrir sahifasi 3 soniyadan kam vaqtda ochiladi.
- 4-qadamda "Faqat PDF fayl yuklash mumkin." yoki "Fayl PDF emas." xabari chiqadi. Kiritilgan qatorlar saqlanib qoladi.
- 5-qadamdan keyin raqam `PC-2026-NNNN` bo'ladi. Shartnoma summasi = 42 500 000 + 5 100 000 + 2 400 000 + 288 000 = **50 288 000 so'm**.
- Tahrir sahifasida birlik ustunlari "m²" va "dona" ni ko'rsatadi ("---Tanlang---" emas). DEMO-XOM-01 qatorida HS Code `9999000001` turadi. Saqlagandan keyin Details da HS kod va birliklar o'zgarmagan.
- 7-qadamda "Bu ТНВЭД kod mahsulot yoki hujjatlarda ishlatilgan, o'chirib bo'lmaydi." xabari chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-10. Xarid invoysi: summa QQS bilan va Moliyaga ko'zgu yozuvi
**Nima tekshiriladi:** Biznes qarori bo'yicha invoys summasi QQS bilan saqlanadi. Moliya bo'limida invoysga mos kreditor yozuvi avtomatik paydo bo'ladi. (audit ID: SRM-B25, SRM-B12, SRM-B13, SRM-B14, SRM-B18)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-06, T-XAR-09
**Qadamlar:**
1. **Xarid jarayoni → Xarid invoyslari → Yangi invoys** ni oching.
2. Invoys raqami = bo'sh, Sana = `20.10.2026`, Buyurtma = T-XAR-06 dagi `PO-2026-10-NNNN`, Ta'minotchi = `DEMO Yog'och MChJ`, Shartnoma = T-XAR-09 dagi `PC-2026-NNNN`, Valyuta = `UZS`.
3. Qatorlar: DEMO-XOM-01 `500` m² × `85000`; DEMO-XOM-02 `20000` dona × `120`. Saqlang.
4. **Xarid invoyslari** ro'yxatiga va invoysning Details sahifasiga qarang.
5. **Moliya → Boshqaruv → Tranzaksiyalar** ni oching va invoys raqami (`PI-2026-`) bo'yicha qidiring.
**Kutilgan natija:**
- Raqam `PI-2026-NNNN` bo'ladi. QQS % maydoni 12 ni ko'rsatadi (shartnomadan olinadi).
- Ro'yxatdagi "Invoys summasi (QQS bilan)" ustunida **50 288 000,00** turadi. Ostida "44 900 000,00 + QQS 12%" yozuvi bor.
- Details sahifasi: "Jami summa (QQSsiz)" 44 900 000; "QQS (%)" 12; "Jami QQS" 5 388 000; "Jami (QQS bilan)" 50 288 000. Shartnoma va shartnoma qoldig'i bloki ko'rinadi.
- Tranzaksiyalar ro'yxatida bitta yozuv bor: chiqim, hisob "Kreditorlar qarzi" (UZS), kategoriya nomida "Xarid" so'zi bor, kontragent DEMO Yog'och MChJ, maqsad "Xarid invoysi #PI-2026-NNNN", summa **50 288 000**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-11. Invoysni tahrirlash: summa qayta hisoblanadi, raqam takrori taqiqlanadi
**Nima tekshiriladi:** Qatorlar o'zgarganda invoys summasi va Moliyadagi ko'zgu yozuvi yangilanadi. Invoys raqami faol invoyslar orasida butun tizimda takrorlanmaydi; o'chirilgan invoys raqami qayta ishlatilishi mumkin (eski yozuv raqami avtomatik arxivlanadi). (audit ID: SRM-B01, SRM-B14, SRM-B20)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-10 (omborga kirim hali qilinmagan bo'lsin)
**Qadamlar:**
1. T-XAR-10 dagi invoysni **Tahrirlash** da oching. DEMO-XOM-02 miqdorini `18000` ga o'zgartirib saqlang.
2. Invoys ro'yxati va **Moliya → Tranzaksiyalar** dagi yozuvni qayta tekshiring.
3. Invoysni yana tahrirlab, miqdorni `20000` ga qaytaring va saqlang.
4. Yangi invoys yarating: Ta'minotchi = `DEMO Yog'och MChJ`, Invoys raqami = T-XAR-10 dagi raqamning o'zi (qo'lda yozing), bitta qator DEMO-XOM-02 1 dona × 120. Saqlang.
**Kutilgan natija:**
- 1-qadamdan keyin: QQS'siz 42 500 000 + 2 160 000 = 44 660 000; QQS 5 359 200; ro'yxatda **50 019 200**.
- Moliya tranzaksiyasi yangi yozuv qo'shmaydi. Mavjud yozuv summasi 50 019 200 ga o'zgaradi.
- 3-qadamdan keyin invoys ham, tranzaksiya ham yana **50 288 000** bo'ladi.
- 4-qadamda "Shu raqamli invoys allaqachon mavjud." xabari chiqadi va invoys yaratilmaydi. Boshqa ta'minotchi tanlansa ham xuddi shu xabar chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-12. SRM Dashboard va Buyurtma kuzatuv
**Nima tekshiriladi:** "Jami xarajat" barcha invoyslar bo'yicha hisoblanadi, faqat top-15 emas. "Gacha" sanasining oxirgi kuni hisobga kiradi. Kuzatuv sahifasi ochiladi va faqat o'z shartnomasini ko'rsatadi. (audit ID: SRM-B06, SRM-B07, SRM-B08, SRM-B24)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-10 (kuzatuvdagi ombor holati uchun T-OMB-03 dan keyin ham qayta ko'ring)
**Qadamlar:**
1. **Ta'minot (SRM) → Hisobotlar → SRM Dashboard** ni oching. Dan sana = `20.10.2026`, Gacha sana = `20.10.2026` qo'yib qo'llang.
2. Gacha sanani `19.10.2026` ga o'zgartiring.
3. **Kuzatuv va tender → Buyurtma kuzatuv** ni oching va T-XAR-06 buyurtmasining Details sahifasini oching.
**Kutilgan natija:**
- 1-qadamda "Jami xarajat" kartasi DEMO invoysni o'z ichiga oladi (kamida 50 288 000; shu kun boshqa invoys bo'lmasa aynan 50 288 000). Grafikda DEMO Yog'och MChJ ko'rinadi.
- 2-qadamda DEMO invoys hisobdan chiqadi ("Jami xarajat" 50 288 000 ga kamayadi).
- 3-qadamda Details sahifasi 500 xatosiz ochiladi. Buyurtma shartnomasiz bo'lgani uchun "bog'liq shartnomalar" ro'yxatida boshqa kompaniyalarning shartnomalari chiqmaydi. Invoys holati "bor" deb ko'rsatiladi. T-OMB-03 dan keyin ombor kirimi ham "bor" bo'ladi.
- "Kreditorlik" kartasi va ta'minotchilar bo'yicha qarz grafigi Moliya dashboard'idagi ta'minotchilar qarzi bilan bir xil (yagona hisob, SRM-P09). Faqat musbat qarzlar ko'rsatiladi.
- Buyurtma kuzatuv ro'yxatida Status filtri (masalan `Invoiced`) jadvalni sahifani qayta yuklamasdan filtrlaydi va T-XAR-06 buyurtmasi "Invoiced" holatida chiqadi.
- Har bir sahifa 3 soniyadan kam vaqtda ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-13. Xarid bo'limida ruxsatlar (cheklangan foydalanuvchi)
**Nima tekshiriladi:** Xarid ruxsati yo'q foydalanuvchi sahifa va hujjatlarni URL orqali ham ocha olmaydi. (audit ID: SRM-B10, SRM-B11)
**Kim:** demo_omborchi (faqat ombor ruxsati)   **Oldin bajarilgan bo'lishi kerak:** T-XAR-09, T-XAR-10
**Qadamlar:**
1. demo_omborchi bilan kiring. Chap menyuda "Ta'minot (SRM)" bo'limi yo'qligini tekshiring.
2. Manzil satrida ketma-ket oching: `/PRequisitions/Create`, `/PInvoices/Create`, `/PContracts/Edit/<T-XAR-09 shartnoma ID si>`, `/PContracts/DownloadDoc/<shartnoma hujjati ID si>`.
**Kutilgan natija:**
- Har bir manzilda "Sizga ruxsat berilmagan!" sahifasi chiqadi. Forma yoki fayl ochilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-XAR-14. Xarid sahifalari tezligi
**Nima tekshiriladi:** Katta katalogda ham forma tez ochiladi, chunki mahsulotlar har qatorga to'liq ro'yxat bo'lib yozilmaydi. Ro'yxatlar serverda sahifalanadi. (audit ID: SRM-P01, SRM-P02, SRM-P03, SRM-P04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR-06, T-XAR-09, T-XAR-10
**Qadamlar:**
1. Ketma-ket oching: Xarid buyurtmalari ro'yxati, T-XAR-06 buyurtmasining Tahrirlash sahifasi, T-XAR-09 shartnomasining Tahrirlash sahifasi, T-XAR-10 invoysining Details sahifasi.
2. Tahrir sahifasida yangi qator qo'shing va mahsulot tanlovida `Qayin` deb qidiring.
3. Xarid buyurtmalari, shartnomalar, invoyslar, arizalar ro'yxatlarida qidiruvga `DEMO` yozing, ustun sarlavhasi bo'yicha saralang, keyin **Excel** tugmasini bosing.
**Kutilgan natija:**
- Har bir sahifa sezilarli kechikishsiz (3 soniyadan kam) ochiladi.
- Qidiruv bir zumda DEMO-XOM-01 ni topadi. Ro'yxat pastga aylantirilganda keyingi mahsulotlar qo'shilib boradi.
- 3-qadamda qidiruv va saralash serverda bajariladi (jadval ostida "jami N ta yozuvdan filtrlangan"). Excel fayli joriy filtr va qidiruv bo'yicha **barcha** yozuvlarni oladi, faqat ko'rinib turgan sahifani emas. PDF tugmasi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

### T-OMB-01. Omborlarni tayyorlash (MRP va DEMO ombor)
**Nima tekshiriladi:** Omborlar sozlamasi ishlaydi. Ro'yxat tez ochiladi. Kirimni faqat omborning MRP (moddiy javobgar shaxs) qabul qiladi. Ombor roli (xomashyo / ishlab chiqarish / savdo) nomdan emas, "Ombor turi" maydonidan olinadi. (audit ID: SRM-P13, SRM-S01, SRM-B12, SRM-P14)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. **Omborxona → Ombor → Omborlar** ni oching.
2. "Xomashyo ombori" ni tahrirlang: MRP = `demo_omborchi`, Ombor turi = "Xomashyo (ta'minot)". Avvalgi MRP ni Izohga yozib qo'ying, test oxirida qaytarasiz.
3. Yangi ombor qo'shing: Ombor nomi = `DEMO Sex ombori`, Sex = istalgan, MRP = `demo_admin`, Ombor turi = **"--- Oddiy ombor ---"** (bo'sh).
4. Ro'yxatdagi "Turi" ustunida ishlab chiqarish ombori = "Ishlab chiqarish", Sotuv ombori (matnda "Tayyor mahsulot ombori") = "Savdo" ekanini tekshiring. Ularning MRP lari kim ekanini yozib oling.
5. Boshqa bir omborni tahrirlab, Ombor turi = "Savdo" qilib saqlashga urining (keyin bekor qiling).
**Kutilgan natija:**
- Omborlar ro'yxati 3 soniyadan kam vaqtda ochiladi. MRP, Sex va Turi ustunlari to'ldirilgan.
- 5-qadamda "Savdo ombori allaqachon belgilangan — faqat bitta savdo ombori bo'lishi mumkin." xabari chiqadi va saqlanmaydi.
- "Ombor muvaffaqiyatli qo'shildi" va "Ombor muvaffaqiyatli yangilandi" xabarlari chiqadi.
- Qoldiqlar sahifasida DEMO-XOM-01 va DEMO-XOM-02 hech bir omborda yo'q (0).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-02. Invoysdan kirim: qabul qilinmaguncha qoldiq o'zgarmaydi
**Nima tekshiriladi:** Qabul qilinmagan kirim (ham yaratish, ham tahrir) ombor qoldig'iga ta'sir qilmaydi. (audit ID: SRM-B02, SRM-B07, SRM-B24)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-XAR-11, T-OMB-01
**Qadamlar:**
1. **Omborxona → Ombor → Operatsiyalar → Kirimlar → Yangi kirim** ni oching.
2. Sana va vaqt = `21.10.2026 10:00`, Invoys = T-XAR-10 dagi `PI-2026-NNNN`. **Invoys mahsulotlarini yuklash** ni bosing. Qabul qiluvchi ombor = Xomashyo ombori, Valyuta = `UZS`. Omborda DLOC joylari bo'lsa, har qatorga DLOC tanlang.
3. Saqlang. Keyin **Qoldiqlar** sahifasini oching (ombor = Xomashyo ombori).
4. Kirimni **Tahrirlash** da oching. DEMO-XOM-02 miqdorini `19000` ga o'zgartirib saqlang. Keyin qayta `20000` ga qaytarib saqlang. Har safar Qoldiqlar sahifasini tekshiring.
**Kutilgan natija:**
- Ikkala qator (500 m² va 20 000 dona) narx bilan yuklanadi. Hujjat raqami `202610_N` ko'rinishida bo'ladi (yil+oy_tartib; takrorlanmaydi).
- Kirimning "Jami summa" si **50 288 000** (QQS bilan). Kirimlar ro'yxatida holati "Kutilmoqda".
- Qatorlarning "Izoh" maydoniga `QQS %: …%` matni qo'shilmaydi. Tahrirlashda QQS % ustuni saqlangan stavkani (12) ko'rsatadi (stavka alohida ustunda saqlanadi — SRM-M04).
- 3- va 4-qadamlarda Qoldiqlar sahifasida DEMO-XOM-01 va DEMO-XOM-02 **0** (yoki ko'rinmaydi). Tahrir qoldiqni oshirmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-03. Kirimni qabul qilish: qoldiq bir marta qo'shiladi
**Nima tekshiriladi:** "Qabul qilish" qoldiqni oshiradigan yagona joy. Ikki marta bosish yoki ikkinchi urinish qoldiqni ikki baravar qilmaydi. (audit ID: SRM-B01, SRM-B08, SRM-B02)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-02
**Qadamlar:**
1. T-OMB-02 kirimining Details sahifasida **Qabul qilish** tugmasini bosing (DLOC bo'lsa, har qatorga DLOC tanlang).
2. Brauzerning "Orqaga" tugmasi bilan oldingi sahifaga qayting va **Qabul qilish** ni yana bosing.
3. Qoldiqlar sahifasini oching (Xomashyo ombori).
**Kutilgan natija:**
- 1-qadamda "Kirim «202610_N» qabul qilindi. Ombor qoldig'i yangilandi." xabari chiqadi. Holat "Qabul qilingan" bo'ladi.
- 2-qadamda "Bu kirim hujjati allaqachon qabul qilingan." xabari chiqadi.
- Qoldiq: DEMO-XOM-01 = **500 m²**, DEMO-XOM-02 = **20 000 dona** (1000 yoki 40 000 emas).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-04. Qabul qilinmagan kirimni o'chirish qoldiqni buzmaydi
**Nima tekshiriladi:** Kutilayotgan kirim o'chirilganda ombor qoldig'i kamaymaydi. Kirimni faqat shu omborning MRP i yoki Admin o'chira oladi. (audit ID: SRM-B03, SRM-S05)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-03
**Qadamlar:**
1. **Yangi kirim**: Invoys bo'sh, Qabul qiluvchi ombor = Xomashyo ombori, qator DEMO-XOM-01 `10` m² × `85000`, QQS % `12`, Izoh = `DEMO o'chiriladigan kirim`. Saqlang (qabul qilmang).
2. Kirimni ro'yxatdan **O'chirish** orqali o'chiring.
3. Qoldiqlar sahifasini oching.
**Kutilgan natija:**
- O'chirish xatosiz bajariladi. Kirim ro'yxatdan yo'qoladi.
- DEMO-XOM-01 qoldig'i **500 m²** bo'lib qoladi (490 emas).
- (Ixtiyoriy) Xomashyo omboriga biriktirilmagan, Admin bo'lmagan foydalanuvchi shu kirimning `/Incomes/Delete/<ID>` sahifasini ochsa, "Sizga ruxsat berilmagan!" sahifasi chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-05. Ikkinchi partiya kirimi (FIFO sinovi uchun)
**Nima tekshiriladi:** Boshqa narxdagi ikkinchi partiya alohida lot bo'lib kiradi. (audit ID: SRM-B01)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-03
**Qadamlar:**
1. **Yangi kirim**: Sana = `22.10.2026 10:00`, Qabul qiluvchi ombor = Xomashyo ombori, qator DEMO-XOM-01 `100` m² × `90000`, QQS % `12`, Izoh = `DEMO 2-partiya`. Saqlang.
2. Details sahifasida **Qabul qilish** ni bosing.
3. Qoldiqlar sahifasini oching. Omborda DLOC bo'lsa, **Joylar bo'yicha qoldiq** sahifasini ham oching.
**Kutilgan natija:**
- Kirim "Jami summa" si 100 × 90 000 × 1,12 = **10 080 000**.
- DEMO-XOM-01 qoldig'i **600 m²**.
- DLOC sahifasida DEMO-XOM-01 uchun "Lotlar soni" = 2, "Eng erta kirim" = 21.10.2026.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-06. Ombor chiqimi har doim sof FIFO (lot tanlanmaydi)
**Nima tekshiriladi:** Biznes qarori: chiqimda lot tanlanmaydi, eng eski partiya birinchi sarflanadi. Chiqim narxi qo'lda kiritilmaydi — FIFO tannarxi (QQSsiz) avtomatik hisoblanadi. (audit ID: SRM-B13, SRM-B07, SRM-B19)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-05
**Qadamlar:**
1. **Kirimlar** yonidagi **Chiqimlar → Yangi chiqim** ni oching.
2. Formada "Lot" yoki "Lot (FIFO)" ustuni yo'qligini, "Tannarx (FIFO)" maydoni faqat o'qish uchun ekanini tekshiring.
3. Sana va vaqt = `23.10.2026 09:00`, Yuboruvchi ombor = Xomashyo ombori, Qabul qiluvchi ombor = **bo'sh**, qator DEMO-XOM-01 Chiqim miqdori `520`, Izoh = `DEMO FIFO sinovi`. Saqlang va chiqimning Details sahifasini oching.
4. Qoldiqlar sahifasini va (DLOC bo'lsa) **Joylar bo'yicha qoldiq** sahifasini oching.
**Kutilgan natija:**
- Qatorda "Mavjud qoldiq" 600 bo'lib ko'rinadi. Lot tanlash maydoni yo'q.
- Hujjat raqami `202610_N` ko'rinishida bo'ladi.
- Details da qator summasi = 500 × 85 000 + 20 × 90 000 = **44 300 000** (dona narxi ≈ 85 192,31), QQSsiz.
- DEMO-XOM-01 qoldig'i 600 − 520 = **80 m²**.
- DLOC sahifasida: 21.10 partiyasi (500 m²) to'liq sarflangan, 22.10 partiyasidan 20 m² olingan. "Lotlar soni" = 1, "Eng erta kirim" = 22.10.2026, qoldiq 80.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-07. Chiqimni o'chirish: qoldiq va lotlar qaytadi
**Nima tekshiriladi:** O'chirilgan chiqim qoldiqni ham, partiyalarni ham aynan qaytaradi. (audit ID: SRM-B04, SRM-B05)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-06
**Qadamlar:**
1. T-OMB-06 chiqimini ro'yxatda **O'chirish** orqali o'chiring.
2. Qoldiqlar va (DLOC bo'lsa) Joylar bo'yicha qoldiq sahifalarini oching.
**Kutilgan natija:**
- DEMO-XOM-01 qoldig'i yana **600 m²**.
- DLOC sahifasida "Lotlar soni" = 2, "Eng erta kirim" = 21.10.2026. Partiyalar ikki marta sarflanmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-08. Manfiy qoldiqqa yo'l qo'yilmaydi
**Nima tekshiriladi:** Ombordagidan ko'p chiqim qilib bo'lmaydi. Bir mahsulot bir necha qatorga bo'lib yozilsa ham jami miqdor tekshiriladi. (audit ID: SRM-B01, SRM-B08)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-07
**Qadamlar:**
1. **Yangi chiqim**: Xomashyo ombori, qator DEMO-XOM-01 `700` m². Saqlang.
2. Formani tuzating: ikki qator DEMO-XOM-01 `400` va `300` m². Saqlang.
3. Qoldiqlar sahifasini oching.
**Kutilgan natija:**
- Ikkala urinishda "Mahsulot DEMO-XOM-01 uchun yetarli qoldiq yo'q. Mavjud: 600, Talab: 700" xabari chiqadi. Hujjat yaratilmaydi.
- Xato bilan qaytgan formada kiritilgan qatorlar, birlik va ombor tanlovlari saqlanib qoladi (sahifa xato sahifasiga o'tmaydi).
- Qoldiq **600 m²** bo'lib qoladi (0 yoki manfiy emas).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-09. Ombordan omborga o'tkazma va qabul
**Nima tekshiriladi:** O'tkazmada yuboruvchidan darhol chiqim bo'ladi. Qabul qiluvchida qoldiq faqat qabul qilingandan keyin paydo bo'ladi. Qabul qilingan o'tkazmani o'chirib bo'lmaydi. (audit ID: SRM-B04, SRM-B06, SRM-B07, SRM-B08)
**Kim:** demo_omborchi (yuboradi), demo_admin (DEMO Sex ombori MRP i, qabul qiladi)   **Oldin bajarilgan bo'lishi kerak:** T-OMB-08
**Qadamlar:**
1. demo_omborchi: **Yangi chiqim**: Sana = `23.10.2026 11:00`, Yuboruvchi ombor = Xomashyo ombori, Qabul qiluvchi ombor = `DEMO Sex ombori`. Qatorlar: DEMO-XOM-01 `50` m², DEMO-XOM-02 `1000` dona (narx avtomatik). Saqlang.
2. Qoldiqlarni ikkala ombor bo'yicha tekshiring. **Kirimlar** ro'yxatida Status = "Kutilmoqda" filtrini qo'ying.
3. demo_admin bilan kiring. Yuqoridagi qo'ng'iroqcha belgisida kutilayotgan kirim sonini ko'ring. Shu kirimni oching va **Qabul qilish** ni bosing.
4. Qoldiqlarni qayta tekshiring.
5. demo_omborchi bilan 1-qadamdagi chiqimni o'chirishga urining.
**Kutilgan natija:**
- 2-qadamda Xomashyo ombori: DEMO-XOM-01 = **550**, DEMO-XOM-02 = **19 000**. DEMO Sex ombori: 0. Kirimlar ro'yxatida `INC-202610_N` raqamli bog'langan kirim "Kutilmoqda" holatida turadi.
- Chiqim narxi FIFO bo'yicha: DEMO-XOM-01 = **85 000** (eng eski 21.10 partiyasidan), DEMO-XOM-02 = **120**. Bog'langan kirim ham shu narxlarda.
- 3-qadamdan keyin DEMO Sex ombori: DEMO-XOM-01 = **50**, DEMO-XOM-02 = **1 000**. Xomashyo ombori o'zgarmaydi (550 / 19 000).
- 5-qadamda o'chirish rad etiladi. "…o'chirib bo'lmaydi" mazmunidagi xabar chiqadi (chiqim yuborilgan yoki bog'langan kirim «INC-…» qabul qilingan). Ikkala qoldiq o'zgarmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-10. O'tkazmani rad etish: faqat yuboruvchi qoldig'i qaytadi
**Nima tekshiriladi:** Qabul qiluvchi rad etganda yuboruvchiga mahsulot qaytadi. Qabul qiluvchi ombor qoldig'iga tegilmaydi. (audit ID: SRM-B03, SRM-B08)
**Kim:** demo_omborchi, demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-OMB-09
**Qadamlar:**
1. demo_omborchi: Xomashyo ombori → DEMO Sex ombori o'tkazmasi, DEMO-XOM-01 `20` m². Saqlang va Xomashyo qoldig'ini tekshiring.
2. demo_admin: bog'langan kirimni oching. **Rad qilish** ni bosing va ogohlantirishni tasdiqlang.
3. Ikkala ombor qoldig'ini, Kirimlar va Chiqimlar ro'yxatini tekshiring.
**Kutilgan natija:**
- 1-qadamdan keyin Xomashyo ombori DEMO-XOM-01 = 530.
- Rad etilgandan keyin Xomashyo ombori = **550**. DEMO Sex ombori = **50** (30 yoki 70 emas).
- Rad etilgan kirim ham, unga bog'langan chiqim ham ro'yxatlardan yo'qoladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-11. Qabul qilinmagan o'tkazmani o'chirish
**Nima tekshiriladi:** Yuboruvchi o'tkazmani o'chirsa, kutilayotgan bog'langan kirim ham o'chadi. Qabul qiluvchi "havodan" mahsulot ololmaydi. (audit ID: SRM-B04)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-OMB-10
**Qadamlar:**
1. Xomashyo ombori → DEMO Sex ombori o'tkazmasi, DEMO-XOM-01 `10` m². Saqlang va bog'langan `INC-…` raqamini yozib oling.
2. Chiqimni o'chiring.
3. Kirimlar ro'yxatida o'sha `INC-…` ni qidiring. Ikkala ombor qoldig'ini tekshiring.
**Kutilgan natija:**
- O'chirish xatosiz bajariladi.
- Bog'langan kirim ro'yxatda yo'q (uni endi qabul qilib bo'lmaydi).
- Xomashyo ombori DEMO-XOM-01 = **550**, DEMO Sex ombori = **50**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-12. Inventarizatsiya (Excel import)
**Nima tekshiriladi:** Excel fayldagi haqiqiy sanoq tizimdagi qoldiq bilan solishtiriladi. Farq bitta INV kirim va bitta INV chiqim bilan yoziladi. Har bir omborda oyiga bir marta ruxsat beriladi (qulf foydalanuvchi emas, ombor bo'yicha). (audit ID: SRM-B09, SRM-P06, SRM-P07)
**Kim:** demo_admin (DEMO Sex ombori MRP i)   **Oldin bajarilgan bo'lishi kerak:** T-OMB-11
**Qadamlar:**
1. **Qoldiqlar → Inventarizatsiya** ni oching. Shablon faylni yuklab oling: 1-ustun `ProductPNo`, 2-ustun `Quantity`.
2. Faylga yozing: `DEMO-XOM-01` | `48`, `DEMO-XOM-02` | `1005`.
3. Ombor = `DEMO Sex ombori`, faylni tanlang, inventarizatsiya qoidalari belgisini qo'ying va yuboring.
4. Qoldiqlar (DEMO Sex ombori), Kirimlar va Chiqimlar ro'yxatlarini tekshiring.
5. Xuddi shu ombor uchun inventarizatsiyani yana yubormoqchi bo'ling.
**Kutilgan natija:**
- "Inventarizatsiya muvaffaqiyatli yakunlandi." xabari chiqadi.
- DEMO Sex ombori: DEMO-XOM-01 = **48** (−2), DEMO-XOM-02 = **1 005** (+5).
- Kirimlarda bitta `INV_…IN…` (+5 dona), Chiqimlarda bitta `INV_…OUT…` (−2 m²) hujjati bor. Izohida "Inventarizatsiya" so'zi turadi. INV chiqimi FIFO tannarxida: 2 × 85 000 = **170 000**.
- 5-qadamda ombor bloklangan yoki "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi." xabari chiqadi. Shu omborga biriktirilgan boshqa foydalanuvchi uchun ham bloklangan bo'ladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-13. Ishlab chiqarishdan sotuv omboriga o'tkazma
**Nima tekshiriladi:** Ko'p qatorli o'tkazma bitta hujjat bo'lib, bitta tranzaksiyada yoziladi. Sotuv omboriga mahsulot faqat qabul qilingandan keyin qo'shiladi. (audit ID: SRM-B10, SRM-S03, SRM-P12)
**Kim:** ishlab chiqarish ombori MRP i, so'ng Tayyor mahsulot ombori MRP i   **Oldin bajarilgan bo'lishi kerak:** MES bo'limidagi 100 ta DEMO-TM-01 ni ishlab chiqarish va sifat nazorati testlari
**Qadamlar:**
1. Qoldiqlarda ishlab chiqarish omboridagi DEMO-TM-01 miqdorini yozib oling (kutilgan: 100 dona).
2. **Ombor → Operatsiyalar → Ishlab chiqarishdan sotuvga** ni oching va yangi o'tkazma yarating. O'tkazma sanasi = bugun, mahsulot DEMO-TM-01, "Lot (ishlab chiqarish)" dan sifat nazoratidan o'tgan lotni tanlang, O'tkazish miqdori `100`.
3. Avval miqdorni `150` qilib saqlab ko'ring, keyin `100` qilib saqlang.
4. Ikkala ombor qoldig'ini tekshiring. Tayyor mahsulot ombori MRP i bilan `TRNSF_…-IN` kirimini oching va **Qabul qilish** ni bosing.
**Kutilgan natija:**
- 150 da "…yetarli qoldiq yo'q. Mavjud: 100, Talab qilinmoqda: 150" (yoki lot/sifat bo'yicha) xatosi chiqadi va hech narsa yozilmaydi.
- 100 da "O'tkazma muvaffaqiyatli yaratildi" xabari chiqadi. Bitta chiqim `TRNSF_ddMMyy_N` va bitta kirim `TRNSF_ddMMyy_N-IN` yaratiladi.
- O'tkazma narxi = ishlab chiqarish kirimining FIFO tannarxi (sotuv shartnomasidagi sotish narxi emas — SRM-B19).
- Qabul qilishdan oldin: ishlab chiqarish ombori DEMO-TM-01 = 0, Tayyor mahsulot ombori = o'zgarmagan.
- Qabul qilingandan keyin Tayyor mahsulot ombori = **+100 dona** (CRM sotuvi 60 stol shu qoldiqdan olinadi).
- Sana maydoniga ertangi sanani qo'ysangiz, "O'tkazma sanasi bugungi kunidan oshmasligi kerak." xabari chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-14. Qoldig'i bor omborni o'chirib bo'lmaydi
**Nima tekshiriladi:** Ombor o'chirilganda tarix buzilmaydi. Qoldig'i yoki kutilayotgan hujjati bor ombor o'chirilmaydi. (audit ID: SRM-B11, SRM-B17)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-OMB-12
**Qadamlar:**
1. **Omborlar** ro'yxatida `DEMO Sex ombori` ni o'chirishga urining.
2. T-OMB-09 dagi o'tkazma chiqimining Details sahifasini oching.
**Kutilgan natija:**
- "Omborda qoldiq bor (1 053,00). Avval qoldiqni boshqa omborga o'tkazing yoki inventarizatsiya qiling." xabari chiqadi (48 + 1 005 = 1 053). Ombor o'chirilmaydi.
- O'tkazma chiqimida "Qabul qiluvchi ombor" hali ham DEMO Sex ombori (bo'sh emas).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-15. Ombor oylik hisoboti (kirim QQS'siz)
**Nima tekshiriladi:** Hisobotga faqat qabul qilingan va o'chirilmagan hujjatlar kiradi. "Summa" rejimida kirim QQS'siz, chiqim FIFO tannarxida ko'rsatiladi. Inventarizatsiya alohida ustunda chiqadi. (audit ID: SRM-B19, SRM-P09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-OMB-03 … T-OMB-12
**Qadamlar:**
1. **Ombor → Oylik hisobot** ni oching. Ko'rinishi = "Miqdor bo'yicha", Ombor = Xomashyo ombori, Oy = Oktyabr, Yil = 2026. **Hisobotni ko'rsatish** ni bosing.
2. Ko'rinishni "Summa bo'yicha" ga almashtiring.
3. Ombor = DEMO Sex ombori, "Miqdor bo'yicha".
**Kutilgan natija:**
- Xomashyo ombori, miqdor bo'yicha: DEMO-XOM-01: Oldingi qoldiq 0, Kirim **600**, Chiqim **50**, Oy oxiridagi qoldiq **550**. DEMO-XOM-02: Kirim **20 000**, Chiqim **1 000**, qoldiq **19 000**. O'chirilgan (T-OMB-04, -07, -11) va rad etilgan (T-OMB-10) hujjatlar hisobda yo'q.
- Agar MES testlari allaqachon bajarilgan bo'lsa, chiqimga ishlab chiqarish sarfi ham qo'shiladi: 90 m² va 2 400 dona.
- Summa bo'yicha: DEMO-XOM-01 kirimi = 500 × 85 000 + 100 × 90 000 = **51 500 000** (QQS bilan 57 680 000 emas). DEMO-XOM-02 kirimi = **2 400 000**. Chiqim (FIFO tannarx): DEMO-XOM-01 = 50 × 85 000 = **4 250 000**, DEMO-XOM-02 = 1 000 × 120 = **120 000**.
- DEMO Sex ombori: Kirim 50 / 1 000. "Inventarizatsiya orqali" ustunida DEMO-XOM-01 = −2, DEMO-XOM-02 = +5. Oy oxiri 48 / 1 005.
- Hisobot 3 soniyadan kam vaqtda ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-16. Ombor Dashboard (DOH)
**Nima tekshiriladi:** Dashboard tez ochiladi. Filtrlar trend grafiklariga ham qo'llanadi. Runner sarlavhasi tanlangan oynani ko'rsatadi. Katta ro'yxatlar cheklangan. (audit ID: SRM-B20, SRM-B21, SRM-P01, SRM-P08, SRM-P19)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-OMB-15
**Qadamlar:**
1. **Ombor → DOH Dashboard** ni oching (yoki **Dashboard → Ombor Dashboard**).
2. Ombor = Xomashyo ombori, Mahsulot turi = Xomashyo, Runner oynasi = `60` qo'yib qo'llang.
3. Ombor = DEMO Sex ombori ga almashtiring.
**Kutilgan natija:**
- Sahifa 3 soniyadan kam vaqtda ochiladi. Brauzer konsolida jQuery yoki DataTables xatosi yo'q.
- Sarlavhalar "High runner (60 kun)" va "Low runner (60 kun)" bo'ladi, doimiy "30 kun" emas.
- "Yuqori DOH" / "Past DOH" kartalarida ko'pi bilan 50 ta qator bor. Sarlavhada jami soni ko'rsatiladi. To'liq ro'yxat pastdagi "Barcha mahsulotlar DOH jadvali" da.
- "Kirim/Chiqim tendentsiyasi" grafigi ombor tanloviga qarab o'zgaradi. DEMO Sex omborida faqat shu ombor harakatlari (50 m², 1 000 dona va boshqalar) ko'rinadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-17. Kirim va chiqim ro'yxatlari: serverda sahifalash va tezlik
**Nima tekshiriladi:** Ro'yxatlar butun jadvalni yuklamaydi. Sahifalash, qidiruv va saralash serverda bajariladi. Standart saralash sana bo'yicha. Excel filtr bo'yicha barcha yozuvlarni beradi. (audit ID: SRM-P04, SRM-B25, SRM-P12)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-OMB-12
**Qadamlar:**
1. **Kirimlar** ni oching. Sanadan/Sanagacha maydonlariga qarang va 2-sahifaga o'ting.
2. Qidiruvga `INC-` deb yozing. So'ng ombor = DEMO Sex ombori filtrini qo'ying.
3. **Excel** tugmasini bosing.
4. Xuddi shuni **Chiqimlar** ro'yxatida qiling. T-OMB-09 chiqimining Details sahifasini oching.
**Kutilgan natija:**
- Ro'yxat 3 soniyadan kam vaqtda ochiladi. Standart sana oralig'i oxirgi 30 kun. Eng yangi hujjat tepada (sana bo'yicha kamayish, summa bo'yicha emas).
- Sahifa almashtirish va qidiruv sahifani to'liq qayta yuklamay, tez ishlaydi. Qidiruv hujjat raqami, invoys raqami va ombor nomi bo'yicha topadi.
- Excel faylida faqat filtrga mos yozuvlar bor, lekin ularning hammasi (faqat ekrandagi 10–25 tasi emas).
- Details sahifasi tez ochiladi. Ilova hujjatlari ro'yxati ko'rinadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-OMB-18. Ombor ruxsatlari (cheklangan foydalanuvchi)
**Nima tekshiriladi:** Ombor ruxsati yo'q foydalanuvchi URL orqali ham ombor amallarini bajara olmaydi va hujjatlarni yuklab ololmaydi. (audit ID: SRM-S01, SRM-S02, SRM-S03, SRM-S04)
**Kim:** demo_sotuv (faqat CRM)   **Oldin bajarilgan bo'lishi kerak:** T-OMB-09
**Qadamlar:**
1. demo_sotuv bilan kiring. Menyuda "Omborxona" bo'limi yo'qligini tekshiring.
2. Manzil satrida oching: `/Incomes/Create`, `/Expenses/Create`, `/Warehouse/Edit/<DEMO Sex ombori ID si>`, `/Unit/Create`, `/ProductionToSalesTransfer/Create`, `/Inventory/Adjust`, `/Incomes/DownloadDoc/1`.
**Kutilgan natija:**
- Har bir manzilda "Sizga ruxsat berilmagan!" sahifasi chiqadi.
- demo_sotuv o'zini ombor MRP iga aylantira olmaydi. Fayl yuklab olinmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

> Test oxirida: T-OMB-01 da o'zgartirilgan Xomashyo ombori MRP ini avvalgi foydalanuvchiga qaytaring.

---

## Ma'lum cheklovlar
- Ishlab chiqarish va savdo ombori "Ombor turi" bilan belgilanmagan bo'lsa, o'tkazma va ishlab chiqarish kirimi "…ombori topilmadi" xatosini beradi (nom bo'yicha taxmin yo'q — SRM-B12).
- Qator birligi qoldiq birligidan farq qilsa, faqat dona↔kg (mahsulot og'irligi bo'yicha) va metr↔dona (kesiladigan mahsulot uzunligi bo'yicha) o'giriladi; boshqa juftliklarda saqlash xato bilan rad etiladi (SRM-B14).
- Chiqim tannarxi DLOC omborda lot narxidan, oddiy omborda qabul qilingan kirimlar tarixidan (FIFO qatlamlari) hisoblanadi. Tarixi to'liq bo'lmagan eski qoldiqlar eng eski ma'lum narxda baholanadi.
- Oddiy (DLOC siz) omborda FIFO partiyalari alohida ko'rinmaydi. Lot izi faqat DLOC omborlarida "Joylar bo'yicha qoldiq" sahifasida kuzatiladi.
- Xarid arizasi faqat bitta tasdiq bilan ishlaydi. Ko'p bosqichli (rol bo'yicha) tasdiqlash qilinmagan (biznes qarori).
- HS (ТНВЭД) kodlar sahifasiga menyuda havola yo'q, faqat `/HSCode` manzili orqali ochiladi.
- Moliyadagi xarid ko'zgu yozuvi faqat invoys saqlanganda yangilanadi. Keyin yaratilgan ombor kirimi summasi u yerga avtomatik qayta yozilmaydi.
- Ta'minotchi formasida alohida INN maydoni yo'q, INN "DUNS" maydoniga yoziladi.
- Inventarizatsiya bitta ombor uchun oyiga bir marta ruxsat etiladi (ataylab qo'yilgan qoida). Xato kiritilsa, tuzatish keyingi oyda yoki administrator orqali qilinadi.
- Eski ma'lumotlar uchun data-fix skriptlari (`DataFix_SRM_Warehouse_*`, `DataFix_SRM_Procurement_*`) va indeks skriptlari alohida, qo'lda ishga tushiriladi. Bu UAT ularni tekshirmaydi.
