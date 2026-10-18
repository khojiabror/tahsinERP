# 4. Sotuv va moliya

Bu bo'lim tayyor stolni sotishdan boshlanadi: mijoz → shartnoma → buyurtma → invoys → TTNK/SF → hisobotlar. Keyin sotuv va xarid natijalari Moliya'da tekshiriladi: tranzaksiyalar, qoldiqlar, qarzlar, Akt-sverki, balans va dashboardlar, eng oxirida opening balans.
Testlarni yozilgan tartibda bajaring, chunki keyingi testlar oldingilarda kiritilgan ma'lumotdan foydalanadi. Kerakli tayyorgarlik: Ishlab chiqarish (T-MES) testlaridan keyin sotuv omboridagi (nomida "Sotuv" yoki "Savdo" so'zi bor ombor) DEMO-TM-01 qoldig'i **100 dona** bo'lishi kerak. Xarid (T-XAR) testlaridagi DEMO Yog'och MChJ invoysi ham kiritilgan bo'lishi kerak.
Hisobot sanalari kelajakda bo'lishi mumkin, shuning uchun hisobot va dashboardlarda "Dan/Gacha" yoki "Sana" maydoniga testda yozilgan sanani qo'lda kiriting.

---

## A. Sotuv (CRM)

### T-CRM-01. Mijoz yaratish va ro'yxatda topish
**Nima tekshiriladi:** Yangi xaridor faqat xaridorlar jadvaliga yoziladi. Avvalgi buzuq "Excel orqali" import xaridorlarni ta'minotchilar ro'yxatiga yozib yuborardi, u endi olib tashlangan. Xaridorlar ro'yxati sahifalab ko'rsatiladi; (audit ID: CRM-B04, CRM-B05, CRM-P09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Xaridorlar" sahifasini oching va "Yangi" (yaratish) tugmasini bosing.
2. Formada "Excel orqali" tugmasi bor-yo'qligini tekshiring.
3. Maydonlarni to'ldiring: Nomi = `DEMO Mebel Savdo MChJ`, Turi = mavjud turlardan biri, DUNS = `300000002` (INN o'rnida), Mamlakat = O'zbekiston, Shahar = Toshkent. Saqlang.
4. Xaridorlar ro'yxatidagi qidiruv maydoniga `DEMO` deb yozib, qidiring.
5. Filtrni tozalang va ro'yxat ostidagi sahifalagichda 2-sahifaga o'ting.
6. "Ta'minot (SRM)" bo'limidagi ta'minotchilar ro'yxatida `DEMO Mebel Savdo` ni qidiring.
**Kutilgan natija:**
- Formada "Excel orqali" tugmasi yo'q.
- Qidiruv natijasida DEMO Mebel Savdo MChJ chiqadi.
- 25 tadan ko'p xaridor bo'lsa, 2-sahifa ochiladi va unda boshqa xaridorlar ko'rinadi.
- Ta'minotchilar ro'yxatida DEMO Mebel Savdo MChJ yo'q.
- Xaridor "Ko'rish" (Details) sahifasi xatosiz ochiladi. Shartnoma, buyurtma va invoyslar soni 0.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-02. Sotuv shartnomasi (QQS bilan summa)
**Nima tekshiriladi:** Shartnoma summasi QQS bilan to'g'ri hisoblanadi. Miqdor va narx vergul bilan yozilsa ham xatosiz saqlanadi; (audit ID: CRM-B19, CRM-S01)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-01
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Shartnomalar" sahifasida yangi shartnoma oching.
2. Maydonlarni to'ldiring: Shartnoma raqami = `DEMO-S-01`, Sana = 20.10.2026, Mijoz = DEMO Mebel Savdo MChJ, Valyuta = UZS, Muddat = 31.12.2026.
3. "Shartnoma mahsulotlari" bo'limiga bitta qator qo'shing: Mahsulot = DEMO-TM-01 Ofis stoli 1200x600 (mahsulot maydoniga `DEMO-TM` deb yozib qidiring), Miqdor = `80`, Narx = `1450000`, QQS (%) = `12`. Saqlang.
4. Shartnomaning Details sahifasini oching.
**Kutilgan natija:**
- Shartnoma saqlanadi va ro'yxatda ko'rinadi.
- Summa: 80 × 1 450 000 = 116 000 000, QQS 12% = 13 920 000, umumiy summa **129 920 000 so'm**.
- Details sahifasi xatosiz ochiladi, valyuta va mijoz nomi ko'rinadi.
- Mahsulot ro'yxati qidiruv bilan ochiladi (to'liq katalog har qatorga yuklanmaydi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-03. Shartnomada qator qo'shish, o'rtadagi qatorni olib tashlash va mahsulotni o'chirish (POST)
**Nima tekshiriladi:** Ikkita holat tekshiriladi.
- Formada o'rtadagi qator olib tashlansa, keyingi qatorlar jimgina yo'qolib qolmaydi.
- Saqlangan mahsulot faqat tasdiqlangan tugma orqali (POST) o'chadi. Brauzer manzil satriga havolani yozib ochish (GET) uni o'chirmaydi.

O'chirilgandan keyin summa qayta hisoblanadi; (audit ID: CRM-B08, CRM-S02)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-02
**Qadamlar:**
1. DEMO-S-01 shartnomasini tahrirlash (Edit) sahifasida yana 2 ta qator qo'shing:
   - DEMO-XOM-02, miqdor 1000, narx 150, QQS 12;
   - DEMO-XOM-01, miqdor `10`, narx `100000`, QQS 12.
2. O'rtadagi qatorni (DEMO-XOM-02) qizil "olib tashlash" tugmasi bilan olib tashlang va "Saqlash"ni bosing.
3. Shartnomani qayta oching va qatorlarni sanang.
4. DEMO-XOM-01 qatoridagi o'chirish (savat) belgisini bosmang. Uning o'rniga belgining ustida sichqonchaning o'ng tugmasini bosib, havola manzilini nusxalang (masalan `/SContract/DeleteProduct/123`). Shu manzilni yangi oynada oching.
5. Shartnoma Edit sahifasiga qayting, DEMO-XOM-01 qatoridagi o'chirish belgisini bosing va tasdiqlang.
**Kutilgan natija:**
- 3-qadamdan keyin 2 ta qator bor: DEMO-TM-01 (80) va DEMO-XOM-01 (10). DEMO-TM-01 o'chib ketmagan.
- O'sha paytdagi umumiy summa: 129 920 000 + 10 × 100 000 × 1,12 = **131 040 000 so'm**.
- 4-qadamda (GET) qator o'chmaydi: "topilmadi" yoki xato sahifasi chiqadi, shartnomada 2 ta qator qoladi.
- 5-qadamdan keyin faqat DEMO-TM-01 qoladi, umumiy summa yana **129 920 000 so'm**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-04. Sotuv buyurtmasi (narx shartnomadan, xatoda miqdorlar saqlanadi)
**Nima tekshiriladi:** Buyurtma narxi brauzerdagi yashirin maydondan emas, shartnoma qatoridan olinadi. Validatsiya xatosida kiritilgan miqdorlar yo'qolmaydi; (audit ID: CRM-S03, CRM-B11, CRM-B13)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-03
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Buyurtmalar" sahifasida yangi buyurtma oching. Maydonlar: Buyurtma raqami = `DEMO-SO-01`, Buyurtma sanasi = 21.10.2026, Haridor = DEMO Mebel Savdo MChJ, Shartnoma = DEMO-S-01.
2. DEMO-TM-01 qatoriga Buyurtma miqdori = `90` yozing (shartnomada 80 ta bor) va saqlang.
3. Xato chiqqanda miqdorni `60` ga o'zgartirib, qayta saqlang.
4. Buyurtma Details sahifasini oching.
**Kutilgan natija:**
- 2-qadamda shartnoma miqdoridan oshgani haqida xato chiqadi. Sahifada siz yozgan 90 qiymati saqlanib qoladi (0 ga tushmaydi).
- 3-qadamdan keyin buyurtma saqlanadi.
- Birlik narxi (QQS bilan) = 1 450 000 × 1,12 = **1 624 000**. Jami (QQS bilan) = 60 × 1 624 000 = **97 440 000 so'm**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-05. Yetkazib berish rejasi (upsert, xabarlar, buyurtma tahriri rejani buzmaydi)
**Nima tekshiriladi:** Rejani saqlash qatorlarni o'chirib qayta yozmaydi. Reja buyurtmadan oshsa, xom JSON sahifa emas, tushunarli xabar chiqadi. Details'da "Rejalashtirilgan/Yetkazilgan" ko'rinadi. Buyurtmani tahrirlash rejani yo'qotmaydi va miqdorni rejadan kam qilish taqiqlanadi; (audit ID: CRM-B06, CRM-B09, CRM-B10, CRM-P07)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-04
**Qadamlar:**
1. DEMO-SO-01 buyurtmasi Details sahifasidan "Yetkazib berish rejasi" sahifasini oching.
2. DEMO-TM-01 uchun 2 qator kiriting: 25.10.2026 — 30 dona, 30.10.2026 — 30 dona. Saqlang.
3. Rejani qayta oching. Uchinchi qator qo'shing: 31.10.2026 — 10 dona (jami 70). Saqlang.
4. Uchinchi qatorni olib tashlab, qayta saqlang.
5. Buyurtmani tahrirlash (Edit) sahifasida DEMO-TM-01 miqdorini `50` qilib saqlashga urinib ko'ring.
6. Miqdorni 60 da qoldirib, faqat "Qisqacha tavsifi" maydonini o'zgartiring va saqlang. Keyin Details'ni oching.
**Kutilgan natija:**
- 2-qadamdan keyin "Yetkazib berish rejasi muvaffaqiyatli saqlandi" xabari chiqadi. Details'da: Rejalashtirilgan: 60,00, Yetkazilgan: 0,00.
- 3-qadamda reja sahifasida qizil xabar chiqadi: "DEMO-TM-01 - Rejalashtirilgan miqdor (70,00) buyurtma miqdoridan (60,00) oshib ketdi". Brauzerda xom JSON matni chiqmaydi.
- 5-qadamda xato chiqadi: "...yetkazib berish rejasida 60,00 — miqdorni avval rejada kamaytiring."
- 6-qadamdan keyin ham rejada 25.10 va 30.10 qatorlari (30 + 30) bor, Details'da "Rejalashtirilgan: 60,00" ko'rinadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-06. Buyurtmada ishlatilgan mahsulotni shartnomadan o'chirib bo'lmaydi
**Nima tekshiriladi:** Buyurtma yoki invoysda ishlatilgan mahsulot shartnomadan o'chirilmaydi, aks holda hujjatlar "yetim" qolardi; (audit ID: CRM-S02, CRM-B12)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-05
**Qadamlar:**
1. DEMO-S-01 shartnomasining Edit sahifasida DEMO-TM-01 qatoridagi o'chirish belgisini bosing va tasdiqlang.
2. Shartnoma Details sahifasida bog'langan buyurtmalar ro'yxatini ko'ring.
**Kutilgan natija:**
- Xabar chiqadi: "DEMO-TM-01 mahsuloti shu shartnoma bo'yicha buyurtmada ishlatilgan — uni shartnomadan o'chirib bo'lmaydi." Qator joyida qoladi.
- Details'da DEMO-SO-01 buyurtmasi ko'rinadi, sahifa xatosiz ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-07. Qoldiq yetmasa invoys umuman saqlanmaydi (bitta tranzaksiya)
**Nima tekshiriladi:** Invoys sarlavhasi, qatorlari, ombor chiqimi va moliya yozuvi bitta tranzaksiyada saqlanadi. Biror qator uchun qoldiq yetmasa, hech narsa yozilmaydi: yarim invoys yoki qisman chiqim bo'lmaydi; (audit ID: CRM-B01, CRM-P03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-06; sotuv omborida DEMO-TM-01 = 100 dona
**Qadamlar:**
1. "Ombor → Qoldiqlar" sahifasida sotuv omboridagi DEMO-TM-01 qoldig'ini yozib oling (100).
2. "Sotuvlar (CRM) → Sotuvlar → Invoyslar" sahifasida "Invoys yaratish"ni bosing. Maydonlar: Mijoz = DEMO Mebel Savdo MChJ, Shartnoma = DEMO-S-01, Valyuta = UZS, Invoys sanasi = 31.10.2026. Invoys raqamini bo'sh qoldiring.
3. 2 ta qator qo'shing: DEMO-TM-01 miqdor 30 va yana DEMO-TM-01 miqdor 80. Narx va QQS % shartnomadan avtomatik to'ladi (1 450 000 va 12), ularni o'zgartirib bo'lmaydi. Saqlang.
4. Invoyslar ro'yxatida `DEMO` bo'yicha qidiring. Ombor qoldig'ini va "Moliya → Boshqaruv → Tranzaksiyalar" sahifasini tekshiring.
**Kutilgan natija:**
- Xato chiqadi: "Mahsulot DEMO-TM-01 ... uchun sotuv omborida yetarli qoldiq yo'q. Mavjud: 100,00, Talab: 110,00". Formadagi qatorlar yo'qolmaydi.
- Yangi invoys yaratilmagan, DEMO-TM-01 qoldig'i 100 da qolgan, moliyada yangi "Sotuv invoysi" yozuvi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-08. Bekor qilingan invoys: ombor va moliya qaytadi, raqam qayta berilmaydi
**Nima tekshiriladi:** Invoysni bekor qilish uchta ishni bitta tranzaksiyada bajaradi: holat "bekor qilingan" bo'ladi, ombor chiqimi qaytariladi, moliya yozuvi olib tashlanadi. Bekor qilingan invoys raqami keyingi invoysga takror berilmaydi; (audit ID: CRM-B01, CRM-B02)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-07
**Qadamlar:**
1. T-CRM-07 dagidek yangi invoys yarating, lekin faqat bitta qator kiriting: DEMO-TM-01, miqdor `2` (QQS 12 shartnomadan avtomatik). Sana = 31.10.2026, raqam bo'sh.
2. Berilgan invoys raqamini yozib oling (N).
3. Ombor qoldig'ini va "Moliya → Boshqaruv → Tranzaksiyalar" sahifasini tekshiring. Tranzaksiyalarda Gacha = 31.10.2026 qilib, `DEMO Mebel` bo'yicha qidiring.
4. Invoys Details sahifasida "Bekor qilish" (O'chirish) → "Ha, bekor qilish" tugmasini bosing.
5. Qoldiqni va tranzaksiyalarni qayta tekshiring.
**Kutilgan natija:**
- Invoysga avtomatik raqam beriladi (N).
- Bekor qilishdan oldin: DEMO-TM-01 qoldig'i 98. Moliyada kirim yozuvi bor: "Sotuv invoysi #N", summa 2 × 1 450 000 × 1,12 = **3 248 000 so'm**.
- Bekor qilgandan keyin: invoys holati bekor qilingan (CANCELED), qoldiq yana **100**, "Sotuv invoysi #N" yozuvi tranzaksiyalar ro'yxatida yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-09. Asosiy sotuv invoysi: 60 stol, ombordan chiqim, Finance'ga UZS'da posting
**Nima tekshiriladi:** Asosiy sotuv invoysi bitta tranzaksiyada saqlanadi va takrorlanmas raqam oladi. Ombordan chiqim yoziladi. Moliyaga QQS bilan summa UZS hisobiga yoziladi: avval posting jimgina tushib qolardi, endi to'g'ri valyutada yoziladi. Narx va QQS % shartnomadan olinadi, formada o'zgartirib bo'lmaydi. Valyuta faqat valyuta ID'si sifatida saqlanadi (eski matnli ustun olib tashlangan); (audit ID: CRM-B01, CRM-B02, CRM-M02, CRM-S03, FIN-B08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-08
**Qadamlar:**
1. Yangi invoys yarating: Mijoz = DEMO Mebel Savdo MChJ, Shartnoma = DEMO-S-01, Buyurtma = DEMO-SO-01, Valyuta = UZS, Invoys sanasi = 31.10.2026, raqam bo'sh.
2. Qator kiriting: DEMO-TM-01, miqdor `60`. Birlik narxi (1 450 000) va QQS % (12) shartnomadan avtomatik to'ladi. Ikkala maydon faqat o'qish uchun ekanini tekshiring (qiymat yozib bo'lmaydi). Saqlang.
3. Invoys Details sahifasini, "Ombor → Qoldiqlar" sahifasini va "Moliya → Boshqaruv → Tranzaksiyalar" sahifasini tekshiring (Gacha = 31.10.2026).
**Kutilgan natija:**
- Invoys raqami **N+1**: bekor qilingan N raqami qayta berilmaydi.
- Narx va QQS % maydonlari faqat o'qish uchun.
- Details: birlik narxi 1 450 000, Qator summa **87 000 000**, QQS summa **10 440 000**, Jami **97 440 000 so'm**; summalar yonida valyuta **UZS** ko'rinadi. Holat — tasdiqlangan (APPROVED).
- Sotuv omboridagi DEMO-TM-01 qoldig'i: 100 − 60 = **40**.
- Moliyada bitta yangi yozuv bor:
  - Yo'nalish: Kirim; Summa: **97 440 000**; Valyuta: UZS;
  - Hisob: "Debitorlar qarzi" (UZS); Kategoriya: "Sotuv..."; Kontragent: DEMO Mebel Savdo MChJ;
  - Maqsad: "Sotuv invoysi #N+1".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-10. Invoysni tahrirlash: saqlangan mahsulot tanlangan, miqdor o'zgarsa ombor va moliya yangilanadi
**Nima tekshiriladi:** Tahrirlash formasida har bir qatorda saqlangan mahsulot tanlangan bo'ladi. Avval ro'yxatdagi birinchi mahsulot ko'rinardi va saqlanganda mahsulot almashib ketardi. Miqdor o'zgarsa, ombor chiqimi va moliya yozuvi ham yangilanadi; (audit ID: CRM-B03, CRM-B08, CRM-B20)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-09
**Qadamlar:**
1. Invoys N+1 ni tahrirlash (Edit) sahifasida oching. Qatorda qaysi mahsulot tanlanganini ko'ring.
2. Miqdorni `59` ga o'zgartirib saqlang.
3. Ombor qoldig'ini va moliyadagi "Sotuv invoysi #N+1" yozuvini tekshiring.
4. Yana tahrirlab, miqdorni `60` ga qaytaring va saqlang.
**Kutilgan natija:**
- Edit'da qatorda **DEMO-TM-01 Ofis stoli** tanlangan.
- 59 dan keyin: qoldiq **41**. Moliya yozuvi summasi 59 × 1 450 000 × 1,12 = **95 816 000** bo'ladi (yangi yozuv qo'shilmaydi, mavjudi yangilanadi).
- 60 ga qaytgandan keyin: qoldiq **40**, moliya yozuvi **97 440 000**.
- Qatorni o'chirishda tasdiq oynasi faqat bir marta chiqadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-11. TTNK va Счёт-фактура invoysdan yaratiladi (avtomatik raqam, server summalari)
**Nima tekshiriladi:** Invoys Details sahifasida "TTNK yaratish" tugmasi bor (avval TTNK yaratib bo'lmasdi). Raqam bo'sh qoldirilsa, TTNK-yyyy-0001 / SF-yyyy-0001 ko'rinishida avtomatik beriladi. SF summalari invoysdan serverda hisoblanadi. SF/TTNK bog'langan invoys qatorlarini o'zgartirib bo'lmaydi; (audit ID: CRM-B17, CRM-S03, CRM-B14, CRM-B15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-10
**Qadamlar:**
1. Invoys N+1 Details sahifasida "TTNK yaratish"ni bosing. TTNK raqamini bo'sh qoldiring, TTNK sanasi = 31.10.2026.
2. Yuk tashuvchi nomi = `DEMO Trans Logistik`, Mashina raqami = `01A777AA`, Haydovchi = `DEMO Haydovchi`. Saqlang.
3. "Sotuvlar (CRM) → Sotuvlar → ТТНК" ro'yxatini oching.
4. Invoys Details'ga qayting va "SF yaratish"ni bosing. SF raqamini bo'sh qoldiring, SF sanasi = 31.10.2026, Mijoz INN = 300000002, Ta'minotchi INN = korxona INN. Saqlang.
5. "Sotuvlar (CRM) → Sotuvlar → Счёт-фактура" ro'yxatida yangi SF'ni ochib ko'ring.
6. Invoys N+1 ni Edit'da ochib, miqdorni 59 qilib saqlashga urinib ko'ring.
**Kutilgan natija:**
- TTNK saqlanadi va unga **TTNK-2026-NNNN** ko'rinishidagi raqam beriladi. ТТНК ro'yxati xatosiz ochiladi, Invoys ustunida N+1 ko'rinadi.
- SF raqami **SF-2026-NNNN**. SF qatori: DEMO-TM-01, 60 dona, narx 1 450 000, QQS 12%. Summalar: 87 000 000 / QQS 10 440 000 / jami **97 440 000**.
- 6-qadamda xato chiqadi: "Bu invoys qatorlari asosida SF/TTNK yaratilgan — qatorlarni o'zgartirish mumkin emas...". Qoldiq 40 da qoladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-12. Valyuta kurslari sahifasi va USD buyurtma
**Nima tekshiriladi:** Valyuta kurslari sahifasi kurslarni ko'rsatadi va qo'lda kurs kiritishga imkon beradi. USD hujjatlar shu kurslar bilan so'mga o'giriladi. Biznes qarori: asos valyuta UZS, kurs — hujjat sanasidagi CBU kursi; (audit ID: CRM-B18)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-11
**Qadamlar:**
1. "Sotuvlar (CRM) → CRM Dashboard" sahifasini oching. Yuqoridagi "Barcha summalar so'mda..." yozuvidagi "valyuta kurslari" havolasini bosing (yoki manzil satriga `/CurrencyRates` yozing).
2. "Kursni qo'lda kiritish" bo'limida: valyuta = USD, sana = 21.10.2026, kurs = `12800`. "Saqlash"ni bosing.
3. Filtrda Valyuta = USD, Dan = 01.10.2026, Gacha = 31.10.2026 qilib, "Ko'rsatish"ni bosing.
4. Yangi sotuv shartnomasi yarating: raqam `DEMO-S-02`, sana 21.10.2026, mijoz DEMO Mebel Savdo MChJ, **Valyuta = USD**. Qator: DEMO-TM-01, 5 dona, narx 120, QQS 12.
5. Yangi buyurtma yarating: `DEMO-SO-02`, sana 21.10.2026, shartnoma DEMO-S-02, DEMO-TM-01 = 5 dona.
**Kutilgan natija:**
- "Kurs saqlandi." xabari chiqadi. Jadvalda 21.10.2026 — USD — 12 800,00 qatori bor.
- DEMO-S-02 shartnoma summasi 5 × 120 × 1,12 = **672 USD**. DEMO-SO-02 buyurtma jami **672 USD**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-13. Sotuv hisobotlari: bekor qilinganlar kirmaydi, oxirgi kun kiradi, USD so'mga o'giriladi
**Nima tekshiriladi:** Hisobotlarga bekor qilingan invoyslar kirmaydi. Davrning oxirgi kunidagi (31.10) invoyslar hisobotga kiradi. USD summalar hujjat sanasidagi kurs bilan so'mga o'giriladi; (audit ID: CRM-B07, CRM-B18, CRM-P08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-12
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Hisobotlar → Mijoz bo'yicha sotuv" sahifasini oching. Dan = 01.10.2026, Gacha = 31.10.2026, "Ko'rsatish"ni bosing. DEMO Mebel Savdo MChJ qatorini toping.
2. "Mahsulot bo'yicha sotuv" sahifasini xuddi shu davr bilan oching va DEMO-TM-01 qatorini toping.
3. "Oylik savdo hisoboti" sahifasida Oy = Oktyabr, Yil = 2026 tanlab, DEMO-TM-01 qatorini toping.
**Kutilgan natija:**
- **Mijoz bo'yicha sotuv**, DEMO Mebel Savdo MChJ qatori:
  - Buyurtmalar soni 2; Buyurtma jami = 97 440 000 + 672 × 12 800 = **106 041 600**;
  - Invoyslar soni **1** (bekor qilingan N kirmaydi);
  - Sotuv jami **87 000 000** (QQSsiz; 31.10 dagi invoys kirgan).
- **Mahsulot bo'yicha sotuv**, DEMO-TM-01 qatori:
  - Buyurtma miqdori 65; Buyurtma summa = 87 000 000 + 600 × 12 800 = **94 680 000**;
  - Sotilgan miqdor **60**; Sotuv summa **87 000 000**; Konversiya ≈ **91,89 %**.
- **Oylik hisobot**, DEMO-TM-01 qatori: sotilgan miqdor 60, buyurtma va sotuv summalari yuqoridagi bilan bir xil.
- Har uch sahifada "Summalar so'mda — har hujjat o'z sanasidagi CBU kursi bilan." yozuvi bor. Kurs topilmagani haqida ogohlantirish yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-14. CRM Dashboard: tezlik, so'mdagi jamilar, debitorlik
**Nima tekshiriladi:** Dashboard ko'rsatkichlari SQL'da hisoblanadi, shuning uchun sahifa tez ochiladi. Bekor qilingan invoyslar jamilarga kirmaydi. Grafik yorliqlari mijoz nomidagi maxsus belgilar tufayli buzilmaydi. Grafiklar skripti alohida faylda (Scripts/crm/crm-dashboard.js); (audit ID: CRM-P01, CRM-P02, CRM-B07, CRM-B18, CRM-S05, CRM-M03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-13
**Qadamlar:**
1. "Sotuvlar (CRM) → CRM Dashboard" sahifasini oching va ochilish vaqtini kuzating.
2. Filtrda Dan = 01.10.2026, Gacha = 31.10.2026 qilib, "Filtrlash"ni bosing.
3. "Korxonalar bo'yicha haqqimiz (Debitorlik)" jadvalida DEMO Mebel Savdo MChJ qatorini toping.
4. Grafiklar ("Buyurtmalar - oy bo'yicha", "Mijozlar bo'yicha buyurtma summasi (TOP 10)") chizilganini ko'ring.
**Kutilgan natija:**
- Sahifa 3 soniyadan kam vaqtda ochiladi.
- Debitorlik jadvalida DEMO Mebel Savdo MChJ / DEMO-S-01 = **97 440 000** (moliyaga to'lov hali kiritilmagan; bekor qilingan 3 248 000 kirmaydi).
- Barcha summalar so'mda. USD buyurtma 672 × 12 800 = 8 601 600 so'm bo'lib qo'shilgan.
- Grafiklar chiziladi. Brauzer konsolida JavaScript xatosi yo'q (F12 → Console).
- "Muddati o'tgan invoyslar" jadvalida Summa (so'm) / To'langan / Qoldiq ustunlari bor; to'liq to'langan invoys bu ro'yxatda va muddati o'tganlar sonida chiqmaydi (CRM-B18).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-15. Ro'yxatlar server tomonda sahifalanadi
**Nima tekshiriladi:** Invoyslar, buyurtmalar, shartnomalar va xaridorlar ro'yxatlari endi faqat oxirgi 25 ta yozuvni emas, barcha yozuvlarni sahifalab ko'rsatadi. Excel eksportga filtrlangan barcha yozuvlar tushadi; (audit ID: CRM-B05, CRM-P04, CRM-P05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-09
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Invoyslar" sahifasini oching. Pastdagi sahifalagichda oxirgi sahifaga o'ting.
2. Filtrda Mijoz = DEMO Mebel Savdo MChJ qilib, qidiring. Keyin Excel eksport tugmasini bosing.
3. "Buyurtmalar" va "Shartnomalar" ro'yxatlarida ham sahifalagich bilan 2-sahifaga o'ting. `DEMO` bo'yicha qidiring.
**Kutilgan natija:**
- Invoyslar sahifasida sahifalagich bor. Oxirgi sahifada eng eski invoyslar (2025-yil importi) ko'rinadi. Bazada 200 dan ortiq invoys bor.
- Filtrda DEMO invoyslari chiqadi: N+1 faol, N bekor qilingan holatda. Excel faylida filtrga mos barcha qatorlar bor (faqat 25 tasi emas).
- Buyurtmalar va shartnomalar ro'yxatlari tez ochiladi, 2-sahifa ishlaydi. DEMO-SO-01/02 va DEMO-S-01/02 qidiruvda topiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-CRM-16. Ruxsatlar: demo_sotuv faqat CRM'ni ko'radi
**Nima tekshiriladi:** Ruxsat endi serverda tekshiriladi. Avval faqat view'da tekshirilardi va POST orqali har kim yoza olardi. Cheklangan foydalanuvchi URL'ni qo'lda yozib ochsa, "Ruxsat yo'q" sahifasi chiqadi; (audit ID: CRM-S01, CRM-S04)
**Kim:** demo_sotuv, demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** T-CRM-09; T-CORE'da demo_sotuv va demo_omborchi yaratilgan
**Qadamlar:**
1. Inkognito oynada demo_sotuv bilan kiring. Menyuda "Sotuvlar (CRM)" bo'limi borligini, "Moliya" bo'limi yo'qligini tekshiring.
2. CRM sahifalarini oching: Invoyslar, invoys N+1 Details, Buyurtmalar, CRM Dashboard, Hisobotlar.
3. Manzil satriga qo'lda yozib oching: `/FinTransactions`, `/FinDashboard/BalanceSheet`, `/OpeningBalance`.
4. Chiqing va demo_omborchi bilan kiring. `/SalesInvoices/Create` va `/SContract/Edit/<DEMO-S-01 ID>` manzillarini oching.
**Kutilgan natija:**
- demo_sotuv uchun CRM sahifalari xatosiz ochiladi.
- 3-qadamdagi har bir manzil "Ruxsat yo'q" (ViewNotAuthorized) sahifasiga yo'naltiradi. Ma'lumotlar ko'rinmaydi.
- demo_omborchi uchun ikkala CRM manzili ham "Ruxsat yo'q" sahifasini ochadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## B. Moliya (Finance)

### T-FIN-01. Tranzaksiyalar ro'yxati: server sahifalash va standart sana filtri
**Nima tekshiriladi:** Tranzaksiyalar ro'yxati butun jadvalni yuklamaydi: qatorlar serverdan 25 tadan keladi. Sana kiritilmasa, oxirgi 3 oy ko'rsatiladi. Faqat ko'rish huquqi borlar uchun ham jadval buzilmaydi. Excel eksport barcha filtrlarni hisobga oladi; (audit ID: FIN-P05, FIN-B14, FIN-B22)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-09
**Qadamlar:**
1. "Moliya → Boshqaruv → Tranzaksiyalar" sahifasini oching va ochilish vaqtini kuzating.
2. Dan/Gacha maydonlaridagi sanalarni ko'ring.
3. Gacha = 31.10.2026 qilib, Yo'nalish = Kirim tanlang va qidiruvga `DEMO` yozing. Keyin 2-sahifaga o'ting (agar bo'lsa).
4. Shu filtr bilan Excel eksport qiling.
**Kutilgan natija:**
- Sahifa 3 soniyadan kam vaqtda ochiladi.
- Dan = bugundan 3 oy oldin, Gacha = bugun.
- Har sahifada 25 ta qator. Pastda jami yozuvlar soni ko'rsatiladi.
- Filtrda "Sotuv invoysi #N+1" (97 440 000) chiqadi. Bekor qilingan "#N" chiqmaydi.
- Excel faylida faqat filtrga mos qatorlar bor: yo'nalish, sana va qidiruv hisobga olingan.
- "Amallar" ustuni sarlavhasi va qatorlari mos. DataTables ogohlantirish oynasi chiqmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-02. Hisoblar ro'yxati to'liq
**Nima tekshiriladi:** Avval 20 tadan keyingi moliya hisoblari ro'yxatda ko'rinmasdi. Endi barcha hisoblar ko'rinadi; (audit ID: FIN-B13)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. "Moliya → Boshqaruv → Hisoblar" sahifasini oching.
2. Jadval ostidagi yozuvlar sonini ko'ring va oxirgi sahifaga o'ting.
3. Qidiruvga `Debitorlar` deb yozing.
**Kutilgan natija:**
- Jadvalda bazadagi barcha faol hisoblar bor (soni 20 dan ko'p bo'lishi mumkin). Oxirgi sahifa ham ochiladi.
- "Debitorlar qarzi" (UZS) hisobi topiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-03. Mijozdan to'lov (kirim) va hisob qoldig'i
**Nima tekshiriladi:** Mijozdan tushgan pul kassa qoldig'ini oshiradi. Kassa oqimiga faqat haqiqiy pul harakati kiradi, invoysning moliyadagi ko'zgu yozuvi (97 440 000) kirmaydi; (audit ID: FIN-P02, FIN-P08, FIN-D1)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-CRM-09
**Qadamlar:**
1. "Moliya → Hisobotlar → Hisoblardagi qoldiq" sahifasida Gacha = 31.10.2026 qiling. Ishlatiladigan UZS kassa hisobining qoldig'ini yozib oling (K).
2. "Moliya → Boshqaruv → Tranzaksiyalar → Yangi" sahifasini oching. Maydonlar:
   - Sana = 31.10.2026; Hisob = UZS kassa; Kategoriya = mijozdan tushum uchun mavjud kirim kategoriyasi;
   - Yo'nalish = Kirim; Summa = `50000000`; Valyuta = UZS;
   - Kontragent turi = Mijoz; Kontragent = DEMO Mebel Savdo MChJ; Shartnoma raqami = `DEMO-S-01`;
   - Maqsad = `DEMO to'lov`.

   Saqlang.
3. "Hisoblardagi qoldiq" sahifasini qayta oching (Gacha = 31.10.2026).
4. "Moliya → Hisobotlar → Kassa oqimi" sahifasida Dan = 01.10.2026, Gacha = 31.10.2026 qiling.
**Kutilgan natija:**
- Tranzaksiya saqlanadi.
- Kassa qoldig'i = K + **50 000 000**. Davrdagi kirim ustuni ham 50 000 000 ga oshgan.
- Kassa oqimining kirim jamisida 50 000 000 bor. "Sotuv invoysi #N+1" (97 440 000) kassa oqimiga kirmaydi.
- "Debitorlar qarzi" hisobi kassa/bank qoldiqlari qatoriga qo'shilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-04. Ta'minotchiga to'lov (chiqim)
**Nima tekshiriladi:** Xarid invoysiga bog'langan to'lov kassa qoldig'ini kamaytiradi va ta'minotchi qarzini kamaytiradi (qarz T-FIN-05 da tekshiriladi); (audit ID: FIN-B08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-XAR testlari (DEMO Yog'och MChJ xarid invoysi)
**Qadamlar:**
1. Avval T-FIN-05 dagi "ta'minotchi qarzi" qiymatini 25.10.2026 sanasi bilan yozib oling (A).
2. "Moliya → Boshqaruv → Tranzaksiyalar → Yangi" sahifasini oching. Maydonlar:
   - Sana = 25.10.2026; Hisob = UZS kassa; Kategoriya = ta'minotchiga to'lov uchun mavjud chiqim kategoriyasi;
   - Yo'nalish = Chiqim; Summa = `20000000`; Valyuta = UZS;
   - Kontragent turi = Ta'minotchi; Kontragent = DEMO Yog'och MChJ;
   - Manba hujjat turi = "Ta'minotchiga to'lov (xarid invoysiga bog'lash)"; Shartnoma (xarid) = DEMO Yog'och shartnomasi.

   Saqlang.
3. "Hisoblardagi qoldiq" sahifasini oching (Gacha = 31.10.2026).
**Kutilgan natija:**
- Tranzaksiya saqlanadi. Ro'yxatda yo'nalish "Chiqim" bo'lib ko'rinadi.
- Kassa qoldig'i T-FIN-03 dagi qiymatdan **20 000 000** ga kam.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-05. Kontragent qarzi hamma sahifada bir xil (yagona qoida)
**Nima tekshiriladi:** Biznes qarori: bitta qarz har sahifada bitta qoida bilan hisoblanadi.
- **Xarid invoysi summasi:** unga bog'langan ombor kirimi summasi (INCOMES); kirim bo'lmasa invoys summasi (QQS bilan).
- **Sotuv invoysi summasi:** QQS bilan jami.

Avval dashboard, opening va Akt-sverki har xil summa ko'rsatardi; (audit ID: FIN-B08, FIN-B03, FIN-P01, FIN-P13)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-03, T-FIN-04
**Qadamlar:**
1. "Moliya → Hisobotlar → Balans" sahifasida Sana = 31.10.2026 qilib, "Ko'rsatish"ni bosing.
2. Aktivlardagi mijozlar qarzi (debitorlik) kategoriyasining tafsilotini (Aktiv details) oching va DEMO Mebel Savdo MChJ / DEMO-S-01 qatorini toping.
3. Passivlardagi "Ta'minotchilardan qarzlar" kategoriyasi tafsilotida DEMO Yog'och MChJ qatorini toping.
4. "Dashboard → Moliya Dashboard → Majburiyatlar dashboard" sahifasida Gacha sana = 31.10.2026 qiling. "Xaridorlardan haqlarimiz" va "Ta'minotchilardan qarzlar" bo'limlarida o'sha kontragentlarni toping.
5. "CRM Dashboard → Korxonalar bo'yicha haqqimiz (Debitorlik)" jadvalini ko'ring.
**Kutilgan natija:**
- **DEMO Mebel Savdo MChJ** qarzi hamma joyda 97 440 000 − 50 000 000 = **47 440 000 so'm**: Balans tafsilotida, Majburiyatlar dashboardida va CRM Dashboardda.
- **DEMO Yog'och MChJ** qarzi hamma joyda **A − 20 000 000**. A — xarid invoysi summasi QQS bilan (yoki kirim summasi), masalan faqat taxta bo'lsa 500 × 85 000 × 1,12 = 47 600 000 → 27 600 000.
- Raqamlar sahifalar o'rtasida tiyinigacha bir xil.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-06. Akt-sverki (mijoz): invoys va to'lov, yakuniy qoldiq
**Nima tekshiriladi:** Mijoz Akt-sverkisi opening sanasidan keyingi invoys va to'lovlarni ko'rsatadi. Yakuniy qoldiq dashboarddagi qarz bilan bir xil. Kontragent nomi va shartnoma raqami JavaScript'ni buzmaydi; (audit ID: FIN-B07, FIN-B09, FIN-B19)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-05
**Qadamlar:**
1. "Sotuvlar (CRM) → Sotuvlar → Shartnomalar" sahifasida DEMO-S-01 Details'ni oching va "Akt-sverki (ushbu shartnoma)" tugmasini bosing.
2. Qoldiq sanasi = 31.10.2026 qiling va "Yangilash"ni bosing.
**Kutilgan natija:**
- Qatorlar:
  - "Sotuv invoysi №N+1 sanasi 31.10.2026" — debet **97 440 000**;
  - "FIN to'lovi №... — DEMO to'lov sanasi 31.10.2026" — kredit **50 000 000**.
- Bekor qilingan invoys N ko'rinmaydi.
- "Yakuniy qoldiq 31.10.2026" = **47 440 000** (T-FIN-05 bilan bir xil).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-07. Akt-sverki opening qoldig'idan boshlanadi (ikki marta hisoblash yo'q)
**Nima tekshiriladi:** Biznes qarori: Akt-sverki opening sanasidagi shartnoma qoldig'idan boshlanadi va faqat undan keyingi harakatlarni qo'shadi. Avval opening ustiga butun tarix ham qo'shilib, eski qarz ikki marta hisoblanardi; (audit ID: FIN-B07, FIN-B09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. "Moliya → Boshqaruv → Opening balans" sahifasida mavjud oxirgi opening sanasini (S) yozib oling.
2. O'sha sanani ochib, "Ta'minotchilardan qarzimiz (AP) — shartnoma kesimida" bo'limidan qoldig'i bor bitta haqiqiy ta'minotchi va shartnomani tanlang. Qoldig'ini yozib oling (O).
3. "Ta'minot (SRM)" bo'limida o'sha shartnomaning Details sahifasini oching va "Akt-sverki"ni bosing (Qoldiq sanasi = bugun).
4. "Moliya → Hisobotlar → Balans → Passivlar" tafsilotida o'sha ta'minotchi va shartnoma qatorini toping (Sana = bugun).
**Kutilgan natija:**
- Akt-sverkining birinchi qatori: "Opening balansdan boshlangan qarzdorlik", sanasi **S**, summasi **O**.
- S sanasidan oldingi invoys yoki to'lovlar ro'yxatda yo'q. Keyingi qatorlar faqat S dan keyingi sanalar bilan.
- Yakuniy qoldiq Balans tafsilotidagi shu shartnoma qarzi bilan bir xil.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-08. Balans: asosiy sahifa va tafsilotlar bir xil jami, tezlik
**Nima tekshiriladi:** Balans va uning tafsilot sahifalari bitta hisob-kitob mexanizmidan foydalanadi. Avval ikkita mexanizm bor edi va "Jami passivlar" tafsilot sahifasidagi jami bilan mos kelmasdi. "Doimiy harajatlar qarzi" endi tafsilotlarda ham bor. Sahifalar tez ochiladi; (audit ID: FIN-B02, FIN-B03, FIN-P03, FIN-P04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-05
**Qadamlar:**
1. "Moliya → Hisobotlar → Balans" sahifasida Sana = 31.10.2026 qilib, "Ko'rsatish"ni bosing. Ochilish vaqtini kuzating.
2. "Jami aktivlar" va "Jami passivlar va kapital" qiymatlarini yozib oling.
3. Aktivlar tafsiloti sahifasini oching va kategoriyalar summalarini qo'shing (yoki sahifadagi jamini ko'ring).
4. Passivlar tafsiloti sahifasida ham shunday qiling. "Doimiy harajatlar qarzi" kategoriyasini toping.
5. Biror passiv kategoriyani bosib, "Kategoriya harakatlari" oynasini oching.
**Kutilgan natija:**
- Balans va tafsilot sahifalari har biri 3 soniyadan kam vaqtda ochiladi.
- Aktivlar tafsilotidagi jami = "Jami aktivlar".
- Passivlar tafsilotidagi jami = "Jami passivlar va kapital".
- "Doimiy harajatlar qarzi" balansda ham, passivlar tafsilotida ham bir xil summa bilan bor.
- Kategoriyalar har safar bir xil tartibda chiqadi. Harakatlar oynasi xatosiz ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-09. Majburiyatlar dashboardi 12 oylik davrda tez ochiladi
**Nima tekshiriladi:** Majburiyatlar dashboardi qarzlarni har oy uchun qayta hisoblamaydi. Avval 12 oyda ~300 so'rov bajarilardi, endi ~47 ta. Raqamlar balans bilan mos; (audit ID: FIN-P01, FIN-P02)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-05
**Qadamlar:**
1. "Dashboard → Moliya Dashboard → Majburiyatlar dashboard" sahifasini oching. Dan sana = 01.11.2025, Gacha sana = 31.10.2026 qilib, "Ko'rsatish"ni bosing.
2. Ochilish vaqtini kuzating.
3. "Oyliklardan qarzimiz (Trend)" grafigini va "Ta'minotchilar bo'yicha (Pie Chart)" grafigini ko'ring.
**Kutilgan natija:**
- Sahifa 12 oylik davrda ham 3–5 soniyadan kam vaqtda ochiladi.
- Trend grafigida 12 ta oy nuqtasi bor.
- Ta'minotchilar bo'yicha qarz jamisi Balansdagi (31.10.2026) "Ta'minotchilardan qarzlar" summasiga teng.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-10. EBITDA: operatsion daromad faqat P&L bandlaridan
**Nima tekshiriladi:** Biznes qarori: EBITDA'dagi daromad va xarajat P&L bandlariga bog'langan kategoriyalardan olinadi (dividend bandisiz). Qarz olish, ichki o'tkazma va aksiyador hissasi kabi P&L'ga bog'lanmagan kirimlar endi daromad hisoblanmaydi; (audit ID: FIN-B05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. "Moliya → Hisobotlar → EBITDA" sahifasida Boshlanish = 01.10.2026, Tugash = 31.10.2026 qilib, "Ko'rsatish"ni bosing. "Operatsion daromad" qiymatini yozib oling (D).
2. "Moliya → Boshqaruv → P&L elementlar" sahifasida hech bir P&L bandiga bog'lanmagan kirim kategoriyasini toping (masalan, qarz olish yoki ichki o'tkazma).
3. Shu kategoriya bilan yangi tranzaksiya kiriting: Sana = 30.10.2026, UZS kassa, Kirim, `5000000`, Maqsad = `DEMO P&L tashqari`.
4. EBITDA'ni qayta oching. Keyin "Moliya → Hisobotlar → Kassa oqimi" sahifasini (01.10–31.10.2026) oching.
5. "Moliya → Hisobotlar → Foyda va zarar" sahifasida oktyabr 2026 uchun daromad bandlarining "Haqiqiy" jamisini EBITDA'dagi "Operatsion daromad" bilan solishtiring.
6. Test tranzaksiyani o'chiring.
**Kutilgan natija:**
- 4-qadamda EBITDA "Operatsion daromad" o'zgarmaydi (D). Kassa oqimining kirimi esa 5 000 000 ga oshadi.
- "Operatsion daromad" = P&L sahifasidagi daromad (Sign=1) bandlarining "Haqiqiy" jamisi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-11. Kompaniya qiymati (DCF): sof aktiv to'g'ri, o'chirilgan aksiyador yo'q
**Nima tekshiriladi:** Biznes qarori: DCF sof aktivi = balansning aktiv ustuni − majburiyatlar (Type 2). Kapital (Type 3) passiv sifatida ayirilmaydi. O'chirilgan aksiyador ro'yxatda va ulushda qatnashmaydi; (audit ID: FIN-B04, FIN-B12, FIN-P04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-08
**Qadamlar:**
1. "Moliya → Aksiyadorlar → Kompaniya qiymati" sahifasini oching. Sana = 31.10.2026, Hisoblash uslubi = DCF. "Ko'rsatish"ni bosing.
2. "Jami aktivlar", "Jami passivlar (majburiyatlar)" va "Sof aktivlar" qiymatlarini yozib oling.
3. Shu sanadagi Balans bilan solishtiring: "Jami aktivlar" hamda passivlar tafsilotidagi faqat majburiyat kategoriyalari jamisi. Kapital kategoriyalarini qo'shmang.
4. "Moliya → Aksiyadorlar → Ro'yxat" sahifasida `DEMO Aksiyador` yarating va keyin o'chiring. Kompaniya qiymati sahifasini qayta oching.
**Kutilgan natija:**
- DCF "Jami aktivlar" = Balansdagi "Jami aktivlar".
- "Jami passivlar (majburiyatlar)" = majburiyat kategoriyalari jamisi. Kapital ayirilmaydi.
- Sof aktivlar = aktivlar − majburiyatlar.
- Sahifa 3 soniyadan kam vaqtda ochiladi.
- O'chirilgan DEMO Aksiyador ro'yxatda ham, qiymat jadvalida ham yo'q. Ulushlar jamisi o'zgarmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-12. P&L rejalari: o'chirilgan reja hisoblanmaydi, qayta yaratiladi, 0 ga o'zgaradi
**Nima tekshiriladi:** O'chirilgan reja P&L, BulkEdit va Excel eksportda hisoblanmaydi. Avval o'chirilgan rejani qayta yaratib bo'lmasdi ("allaqachon mavjud"). BulkEdit'da rejani 0 ga o'zgartirib bo'lmasdi; (audit ID: FIN-B11, FIN-P09, FIN-P15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. "Moliya → Boshqaruv → P&L rejalar" sahifasida yangi reja yarating: 2026-yil, noyabr, istalgan daromad bandi, summa `1000000`.
2. "Moliya → Hisobotlar → Foyda va zarar" sahifasida noyabr 2026 ni ochib, shu bandning "Reja" ustunini ko'ring.
3. Rejani o'chiring va P&L'ni qayta oching.
4. Xuddi shu yil, oy va band uchun rejani yana yarating (`1000000`).
5. BulkEdit (ommaviy tahrir) sahifasida shu reja summasini `0` qilib saqlang.
**Kutilgan natija:**
- 2-qadamda Reja = 1 000 000. 3-qadamdan keyin Reja = 0.
- 4-qadamda "allaqachon mavjud" xatosi chiqmaydi, reja tiklanadi.
- 5-qadamdan keyin reja 0 ga o'zgaradi va P&L'da Reja = 0.
- P&L sahifasi 3 soniyadan kam vaqtda ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-13. Ish haqi tasdiqlanganda Finance'ga chiqim yoziladi
**Nima tekshiriladi:** Biznes qarori: ish haqi hisobi tasdiqlanganda moliyaga avtomatik **chiqim** (Direction = 2) yoziladi. Avval kirim (1) bo'lib yozilardi. Ikki marta bosish ikki marta yozmaydi; (audit ID: HRM-B01, FIN-B10)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM ish haqi testlari (DEMO Aliyev Anvar uchun "Qoralama" holatidagi oktyabr hisobi)
**Qadamlar:**
1. "Moliya → Hisobotlar → Hisoblardagi qoldiq" sahifasida UZS kassa qoldig'ini yozib oling (K2).
2. "Kadrlar (HR) → Ish haqi" sahifasida oktyabr 2026 hisobini Edit'da oching. "Moliya hisobi (kassa/bank)" = UZS kassa, "Chiqim toifasi" = oylik toifasi. Hisob jamisini yozib oling (P).
3. Hisobni tasdiqlang. Tasdiqlash tugmasini ketma-ket ikki marta bosishga harakat qiling.
4. "Moliya → Boshqaruv → Tranzaksiyalar" sahifasida oxirgi yozuvlarni ko'ring. "Hisoblardagi qoldiq"ni qayta oching.
**Kutilgan natija:**
- Hisob holati "Posted" (moliyaga yozilgan) bo'ladi.
- Tranzaksiyalarda bitta yangi yozuv bor: Yo'nalish = **Chiqim**, Summa = **P**, hisob UZS kassa, oylik kategoriyasi. Ikkinchi takroriy yozuv yo'q.
- Kassa qoldig'i = K2 − P.
- "Yadro moliyaviy hisobot"dagi oylik qarzi barcha xodimlar bo'yicha hisoblanadi (30 ta bilan cheklanmaydi).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-14. XSS: foydalanuvchi matni sahifada skript bo'lib ishlamaydi
**Nima tekshiriladi:** Foydalanuvchi kiritgan matnlar (maqsad, izoh, shartnoma raqami, kontragent nomi) ro'yxat, popup va Akt-sverki sahifalarida oddiy matn sifatida chiqadi va brauzerda kod sifatida bajarilmaydi; (audit ID: FIN-B19, CRM-S05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Yangi tranzaksiya kiriting: Sana = 30.10.2026, UZS kassa, Chiqim, `1000`, istalgan xarajat kategoriyasi. Maqsad = `DEMO <img src=x onerror=alert('XSS')>`, Izoh = `</script><b>DEMO</b>`.
2. Tranzaksiyalar ro'yxatini oching (Gacha = 31.10.2026, qidiruv `DEMO`).
3. "Balans" → shu kategoriya joylashgan tafsilot sahifasini (31.10.2026) oching va "Kategoriya harakatlari" oynasini oching.
4. Test tranzaksiyani o'chiring.
**Kutilgan natija:**
- Hech qayerda `XSS` degan alert oynasi chiqmaydi.
- Maqsad va izoh aynan yozilganidek matn bo'lib ko'rinadi (`<img ...>` va `<b>` belgilari bilan). Qalin yozuv yoki rasm belgisi chiqmaydi.
- Sahifa tuzilishi buzilmaydi, konsolda xato yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-FIN-15. Opening balans: Auto-fill, saqlash va keyingi Akt-sverki (oxirida o'chiriladi)
**Nima tekshiriladi:** Opening balansni avtomatik to'ldirish (Auto-fill) va saqlash bitta tranzaksiyada bajariladi, shuning uchun o'rtada xato bo'lsa sananing qoldiqlari yo'qolmaydi. Opening qiymatlari yagona qarz qoidasi bilan hisoblanadi. Akt-sverki yangi opening'dan boshlanadi; (audit ID: FIN-B06, FIN-B08, FIN-B07, FIN-P11, FIN-B19)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-FIN-05, T-FIN-06 (bu test ENG OXIRIDA bajariladi)
**Qadamlar:**
1. "Moliya → Boshqaruv → Opening balans" sahifasini oching. Opening sana = 31.10.2026 tanlab, "Load qilish"ni bosing.
2. "Auto-fill from current balances" tugmasini bosing va tasdiqlang.
3. "Mijozlardan haqqimiz (AR) — shartnoma kesimida" bo'limida DEMO Mebel Savdo MChJ / DEMO-S-01 qatorini toping. "Ta'minotchilardan qarzimiz (AP)" bo'limida DEMO Yog'och MChJ qatorini toping.
4. "Boshqa balans kategoriyalari (manual opening)" bo'limiga bitta qator qo'shing: istalgan kategoriya, `1000000`. "Saqlash"ni bosing.
5. Opening balans ro'yxatida 31.10.2026 sanasi va "Qatorlar soni"ni ko'ring. Sanani qayta ochib, qatorlar joyidaligini tekshiring.
6. DEMO-S-01 Akt-sverkisini oching (Qoldiq sanasi = 31.10.2026).
7. Tozalash: Opening balans ro'yxatida 31.10.2026 qatoridagi "O'chirish" → "Ha, o'chirish" tugmasini bosing. DEMO-S-01 Akt-sverkisini qayta oching.
**Kutilgan natija:**
- Auto-fill'dan keyin "Auto-fill bajarilgan..." xabari chiqadi.
- DEMO-S-01 qatori = **47 440 000**. DEMO Yog'och qatori = **A − 20 000 000** (T-FIN-05 bilan bir xil).
- Shartnoma tanlash ro'yxatlari xatosiz ochiladi.
- Saqlagandan keyin "Opening balans muvaffaqiyatli saqlandi." xabari chiqadi. Ro'yxatda 31.10.2026 sanasi bor, qayta ochilganda barcha qatorlar (qo'lda qo'shilgan 1 000 000 ham) joyida.
- 6-qadamda Akt-sverki: birinchi qator "Opening balansdan boshlangan haqqimiz", 31.10.2026, **47 440 000**. 31.10 dagi invoys va to'lov qayta qo'shilmaydi, yakuniy qoldiq yana 47 440 000.
- 7-qadamda "Opening balans (2026-10-31) o'chirildi." xabari chiqadi. Akt-sverki T-FIN-06 dagi ko'rinishga qaytadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ma'lum cheklovlar

- **Kod o'qishda topilgan xavf.** Moliya'dagi mijoz qarzi (Balans, Majburiyatlar dashboard, opening Auto-fill) va mijoz Akt-sverkisi bekor qilingan (CANCELED) sotuv invoyslarini chiqarib tashlamaydi. Shuning uchun T-FIN-05, T-FIN-06 va T-FIN-15 da DEMO Mebel Savdo qarzi 3 248 000 ga ko'p chiqishi mumkin. CRM Dashboard bunday invoyslarni to'g'ri chiqaradi.
- Buyurtmaning yetkazish rejasidagi "Yetkazilgan" (QtyShipped) miqdorini hech qaysi oqim avtomatik to'ldirmaydi. U 0 bo'lib qoladi.
- Moliya dashboardlari (FIN-B01) UZS va USD summalarini so'mga o'girmasdan qo'shadi. Konvertatsiya faqat CRM dashboard va hisobotlarda bor.
- FinTransactions Edit'da Create'dagi tekshiruvlar yo'q. Avtomatik (Manual emas) yozuvni POST orqali tahrirlash mumkin (FIN-B15).
- "Hisoblardagi qoldiq" sahifasi opening'ni hisoblar soniga teng bo'ladi va `>=` sana sharti bilan ishlaydi (FIN-B16).
- Balans toifasi tanlanmagan doimiy majburiyat balansga tushmaydi (FIN-B17).
- Balansdagi oylik qarzi opening payroll qatorlarini hisobga olmaydi (FIN-B18).
- Tranzaksiyalar importida takroriy kod/nom tekshiruvi yo'q (FIN-B23). Aksiyador ulush davri va hissa qo'shish kamchiliklari tuzatilmagan (FIN-B24).
- Doimiy harajatlar sahifasi alohida ruxsat moduliga ega emas (FIN-B27).
- Xarid invoysining moliyadagi ko'zgu yozuvi faqat invoys yaratilganda yoziladi. Keyin kiritilgan ombor kirimi uni yangilamaydi (FIN-B29).
- Ekspeditor Akt-sverkisi opening qoldig'idan foydalanmaydi.
- Opening balans sahifasida qatorlar ko'p bo'lsa, shartnomalar alohida so'rovlar bilan yuklanadi va sahifa sekin ochilishi mumkin (FIN-P12).
- Boshqa Finance sahifalari (Hisoblar, Kategoriyalar, Opening) hali eski, view ichidagi ruxsat tekshiruvida ishlaydi.
- Sotuv invoysida QQS % brauzerdan olinadi. Narx shartnoma tanlangandagina serverda shartnomadan olinadi (CRM-S03 qisman).
- CustomerBase/SupplierBase va Customer/Supplier birlashtirilmagan. Inline JS bundle'larga ko'chirilmagan (CRM-M03, D1, D2).
- 187 ta eski DRAFT sotuv invoysi (asosan 2025-yil importi) ombor chiqimi va moliya postingisiz qolgan. Ularni post qilish alohida buxgalteriya qarori bilan hal qilinadi.
