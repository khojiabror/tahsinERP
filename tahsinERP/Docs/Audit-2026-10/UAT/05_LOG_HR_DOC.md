# 5. Logistika, kadrlar, hujjatlar va topshiriqlar

Bu bo'limda uchta modul tekshiriladi: **Logistika** (yuk tashuvchi → shartnoma va tarif → yo'l varaqasi → to'lov → hisobotlar), **Kadrlar (HR)** (sozlamalar → bo'limlar → shtat → xodim → tabel → ish haqi → Finance) va **Vazifalar/Hujjatlar** (topshiriqlar, rasmiy xatlar, buyruqlar, standartlar, qo'llanma).
Testlarni shu tartibda bajaring: har bir test oldingisida kiritilgan DEMO yozuvlardan foydalanadi. Barcha testlar **demo_admin** bilan, boshqa foydalanuvchi ko'rsatilmagan bo'lsa.
Logistika testlari uchun SRM bo'limida **DEMO Yog'och MChJ** xarid buyurtmasi va xarid invoysi oldindan yaratilgan bo'lishi kerak. Moliya → Hisoblar'da kamida bitta faol UZS bank hisobi va bitta UZS kassa bo'lishi kerak.

---

## A. Logistika

### T-LOG-01. Yuk tashuvchi va transport turi (o'lchamlar mm'da)
**Nima tekshiriladi:** ekspeditor va yuk mashinasi ma'lumotnomaga kiritiladi. Transport o'lchamlari millimetrda saqlanadi, hajm va palletlar soni to'g'ri hisoblanadi, o'lchov birligi tahrirda saqlanadi; (audit ID: LOG-B10, LOG-B18, LOG-S03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Logistika → Yuk tashish → Ma'lumotnoma va shartnomalar → Tashuvchi kompaniyalar → yangi yozuv: Yuk tashuvchi nomi **DEMO Trans Logistik**, Direktor "DEMO Rahimov", Bog'lanish uchun shaxs "DEMO Dispetcher", Qisqacha bayoni "DEMO ekspeditor". Saqlang.
2. Transport turlari → yangi: Transport turi **DEMO Fura 20t**, O'lchov birligi — istalgan (masalan "dona"), Ko'tarish imkoniyati 20000, Og'irligi 8000.
   Tashqi: Uzunlik 13800, Kenglik 2550, Balandlik 4000 (mm). Ichki: Uzunlik 13600, Kenglik 2450, Balandlik 2700 (mm). Saqlang.
3. DEMO Fura 20t → Batafsil sahifasini oching.
4. Tahrirlash → O'lchov birligini boshqasiga almashtiring → Saqlang → Batafsil'ni qayta oching.
**Kutilgan natija:**
- DEMO Trans Logistik ro'yxatda bor.
- Batafsil: **Ichki hajm 89,96 m³** (13,6 × 2,45 × 2,7). **Tashqi hajm 140,76 m³**. **Palletlar (1200×1000, bir qatlam) = 26**.
- 4-qadamdan keyin yangi o'lchov birligi saqlangan (avvalgisiga qaytmagan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-02. Shartnoma va tarif
**Nima tekshiriladi:** ekspeditor bilan shartnoma va "ton uchun" tarif (eng kam to'lov bilan) kiritiladi. Sana maydonlari ikkalasi ham ishlaydi, valyutasiz shartnoma ro'yxatni buzmaydi; (audit ID: LOG-B17, LOG-S01, LOG-S03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-01
**Qadamlar:**
1. Ma'lumotnoma va shartnomalar → Shartnomalar → yangi: Shartnoma raqami **DEMO-TL-2026/01**, Yuk tashuvchi DEMO Trans Logistik.
   Tuzilgan sanasi 01.10.2026, Tugaydigan sana 31.12.2026 (ikkala sanani kalendardan tanlang), Miqdori 50 000 000, Valyuta UZS. Saqlang.
2. Shartnomalar ro'yxatini oching.
3. Tariflar → yangi: Shartnoma DEMO-TL-2026/01, Transport turi DEMO Fura 20t, Valyuta UZS.
   Qayerdan (Viloyat/Shahar) Toshkent / Toshkent, Qayerga Toshkent viloyati / Chirchiq.
   Tarif asosi **"Ton uchun"**, Tarif stavkasi **50 000**, Eng kam to'lov summasi **600 000**, Faol ✔. Saqlang.
**Kutilgan natija:**
- Shartnomada ikkala sana ham to'g'ri saqlangan (01.10.2026 va 31.12.2026), sanalar almashib ketmagan.
- Shartnomalar ro'yxati xatosiz ochiladi (sariq xato sahifasi yo'q).
- Tariflar ro'yxatida DEMO-TL-2026/01 bo'yicha "Ton uchun", 50 000, eng kam to'lov 600 000, faol tarif bor.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-03. Yo'l varaqasi: invoysga bog'lash, summa tarifdan, eng kam to'lov, raqam takrori
**Nima tekshiriladi:** xom ashyo tashigan yo'l varaqasi xarid invoysi va buyurtmasiga bog'lanadi. Summa tarifdan hisoblanadi (eng kam to'lov ham hisobga olinadi). Bir xil raqam ikki marta kiritilmaydi. Forma skriptlari ishlaydi; (audit ID: LOG-B03, LOG-B06, LOG-B07, LOG-P01)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-02; SRM bo'limida DEMO Yog'och MChJ xarid invoysi va buyurtmasi
**Qadamlar:**
1. Operatsiyalar → Yo'l varaqalari → "Yangi yo'l varaqasi".
2. Yo'l varaqasi raqami **DEMO-YV-001**, Sana 20.10.2026.
3. Shartnoma DEMO-TL-2026/01 tanlang. Tarif ro'yxati avtomatik to'lishini kuzating va DEMO tarifini tanlang.
4. Xarid invoysi maydoniga "DEMO" deb 1–2 harf yozing. Qidiruv natijasidan DEMO Yog'och MChJ invoysini tanlang. Xarid buyurtmasini ham shu usulda tanlang.
5. Brut vazn (ton) = **10** yozing → "Jami summa"ga qarang.
6. Brut vaznni **20** ga o'zgartiring → "Jami summa"ga qarang.
7. Mashina raqami 01A123BC, Haydovchi "DEMO Haydovchi". Saqlang.
8. Yana "Yangi yo'l varaqasi" oching va raqamga yana **DEMO-YV-001** yozing. Qolgan majburiy maydonlarni to'ldirib saqlashga urining. Keyin formani bekor qiling.
**Kutilgan natija:**
- 3-qadam: tarif ro'yxati sahifani yangilamasdan to'ladi. Tarif tanlanganda Tarif asosi "Ton uchun", stavka 50 000 bo'ladi. Brauzer konsolida "$ is not defined" xatosi yo'q.
- 4-qadam: invoys tanlanganda Yo'nalish "Kiruvchi", Qayerga "Bizning ombor" bo'ladi. Qayerdan maydoniga ta'minotchi manzili, valyutaga invoys valyutasi avtomatik yoziladi.
- 5-qadam: Jami summa **600 000** (10 × 50 000 = 500 000, bu eng kam to'lovdan kam, shuning uchun 600 000).
- 6-qadam: Jami summa **1 000 000** (20 × 50 000).
- 7-qadam: ro'yxatda DEMO-YV-001, Yuk tashuvchi DEMO Trans Logistik, Summa 1 000 000 UZS, To'lov holati **"To'lanmagan"**, Qoldiq 1 000 000.
- 8-qadam: "Bu raqamli yo'l varaqasi allaqachon mavjud." xabari, ikkinchi yozuv yaratilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-04. Tarif o'zgarsa eski yo'l varaqasi summasi o'zgarmaydi
**Nima tekshiriladi:** yo'l varaqasida tanlangan tarif saqlanadi (F_WAYBILLS.TariffID). Tarif keyinroq o'zgartirilsa yoki nofaol qilinsa ham, eski yo'l varaqasini tahrirlaganda o'sha tarif ko'rsatiladi, stavka va summa o'z-o'zidan qayta yozilmaydi; (audit ID: LOG-B05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-03
**Qadamlar:**
1. Tariflar → DEMO tarifini tahrirlang: Tarif stavkasi **55 000**. Saqlang.
2. Yo'l varaqalari → DEMO-YV-001 → Tahrirlash. Faqat Haydovchi maydonini "DEMO Haydovchi 2" ga o'zgartiring. Tarifga tegmang. Saqlang.
3. Tarifni yana tahrirlab stavkani **50 000** ga qaytaring.
4. DEMO tarifini tahrirlab **Faol** belgisini olib tashlang. Saqlang. DEMO-YV-001 → Tahrirlash ni oching, Tarif maydoniga qarang, saqlamasdan chiqing.
5. DEMO tarifini yana **Faol** qiling.
**Kutilgan natija:**
- 2-qadamdan keyin DEMO-YV-001 summasi **1 000 000** bo'lib qoladi (55 000 × 20 = 1 100 000 emas), Tarif stavkasi 50 000.
- Saqlash xatosiz o'tadi, "Tarif tanlash majburiy" xabari chiqmaydi.
- 4: tarif nofaol bo'lsa ham Tarif maydonida **DEMO tarifi tanlangan** holda turadi (bo'sh emas, boshqa tarif emas).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-05. Excel orqali yo'l varaqasini import qilish
**Nima tekshiriladi:** namuna fayl to'g'ri ustunlar va haqiqiy faol tarif qiymatlari bilan yuklab olinadi. Har qator shartnoma tarifiga solishtiriladi: bo'sh tarif asosi/stavka/valyuta tarifdan olinadi, tarifdan farq qilsa ManualCorrectionReason majburiy. Summa bo'sh bo'lsa tarif bo'yicha (eng kam to'lov bilan) hisoblanadi. Noto'g'ri qator butun faylni to'xtatmaydi, xato qator raqami bilan ko'rsatiladi; (audit ID: LOG-B06, LOG-B12, LOG-D05, LOG-M01)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-02, T-LOG-04 (DEMO tarifi faol, stavka 50 000)
**Qadamlar:**
1. Yo'l varaqalari → "Excel orqali yuklash" → "Namuna faylni yuklab olish". (Sahifa manzili `/WaybillImport/UploadExcel`.)
2. Faylni Excelda oching. IssueDate, PlannedDeliveryDate, ActualDeliveryDate ustunlari sana formatida ekanini, Description esa oddiy matn ekanini tekshiring. Namuna qatorida ContractNo, TransportType, FreightBasis ("Ton uchun" kabi matn), Rate va Currency haqiqiy faol tarifdan to'ldirilganini ko'ring.
3. 2-qatorni (namuna) quyidagicha to'ldiring:
   WaybillNo = **DEMO-YV-002**, IssueDate = 20.10.2026, Direction = Kiruvchi, ContractNo = DEMO-TL-2026/01, TransportType = DEMO Fura 20t, FromLocation = Toshkent, ToLocation = Chirchiq, GrWeight = 20, **FreightBasis, Rate va Amount — bo'sh qoldiring**, Currency = UZS.
4. 3-qatorga 2-qatorni to'liq nusxalang (WaybillNo yana DEMO-YV-002).
5. 4-qator: WaybillNo = DEMO-YV-099, IssueDate = 20.10.2026, ContractNo = "YOQ-123" (mavjud bo'lmagan shartnoma).
6. 5-qatorga 2-qatorni nusxalang, faqat WaybillNo = **DEMO-YV-098**, Rate = **45000** (ManualCorrectionReason bo'sh).
7. Faylni saqlab, "Yuklash" bilan yuklang.
**Kutilgan natija:**
- Natija: **1 ta muvaffaqiyatli** yuklangan, 3 ta xato. Sahifa sariq xato bilan yiqilmaydi.
- Xatolar: "Qator 3: Bu waybill raqami Excel faylida takrorlangan (DEMO-YV-002).", "Qator 4: Shartnoma topilmadi (ContractID/ContractNo)." va "Qator 5: tarif #… dan farq qiladi: stavka (45000 ≠ 50000) — ManualCorrectionReason kiriting."
- Yo'l varaqalari ro'yxatida DEMO-YV-002: tarif asosi "Ton uchun", stavka 50 000 (tarifdan olingan), summa **1 000 000** (20 × 50 000, Amount bo'sh bo'lgani uchun hisoblangan), To'lanmagan. DEMO-YV-098 yaratilmagan.
- DEMO-YV-002 → Tahrirlash: Tarif maydonida DEMO tarifi tanlangan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-06. Bitta to'lov bilan ikkita yo'l varaqasini to'lash va Finance'ga chiqim
**Nima tekshiriladi:** bitta to'lov bir nechta yo'l varaqasiga to'g'ri taqsimlanadi: yangi to'lovning o'zi "allaqachon to'langan" deb hisoblanmaydi. To'lov, taqsimot va moliya yozuvi birga saqlanadi. Noto'g'ri summa yoki boshqa ekspeditor rad etiladi; (audit ID: LOG-B01, LOG-B08, LOG-B20, LOG-M03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-03, T-LOG-05 (W1 = DEMO-YV-001 = 1 000 000; W2 = DEMO-YV-002 = 1 000 000)
**Qadamlar:**
1. Operatsiyalar → To'lovlar → "Yangi to'lov". Yo'l varaqalari: **DEMO-YV-001 va DEMO-YV-002** (ikkalasi). Yuk tashuvchi DEMO Trans Logistik. To'lov miqdori **0**. Saqlang.
2. Yuk tashuvchini boshqa (DEMO bo'lmagan) ekspeditorga almashtiring, summa 1 000 000. Saqlang.
3. Yuk tashuvchini DEMO Trans Logistik ga qaytaring.
   To'lov miqdori **1 000 000**, To'lov sanasi 25.10.2026, Valyuta UZS, To'lov hisobi — UZS bank hisobi, Izoh "DEMO bitta to'lov ikki varaqaga". Saqlang.
4. Yo'l varaqalari ro'yxatini oching.
5. Moliya → Boshqaruv → Tranzaksiyalar: sana oralig'ini 25.10.2026 qilib qidiring.
**Kutilgan natija:**
- 1-qadam: "To'lov miqdori noldan katta bo'lishi kerak." — to'lov saqlanmaydi.
- 2-qadam: "Quyidagi yo'l varaqalari boshqa yuk tashuvchiga tegishli: DEMO-YV-001, DEMO-YV-002." — to'lov saqlanmaydi.
- 4-qadam: **DEMO-YV-001 — "To'langan", To'langan 1 000 000, Qoldiq 0**. **DEMO-YV-002 — "To'lanmagan", Qoldiq 1 000 000**. To'lov ro'yxatda turgan birinchi varaqani to'liq yopadi. Ikkala varaqa sanasi bir xil bo'lgani uchun tartib raqam bo'yicha: avval 001.
- 5-qadam: bitta **Chiqim** tranzaksiyasi, summa 1 000 000 UZS, tanlangan bank hisobida.
  Kontragent DEMO Trans Logistik, maqsad "Yo'l varaqasi bo'yicha yuk tashish to'lovi: DEMO-YV-001, DEMO-YV-002". Toifa — material tashish ("M.Tash"), chunki W1 xarid invoysiga bog'langan.
- Ogohlantirish chiqmaydi (bank hisobi topildi, ortiqcha to'lov yo'q).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-07. Qarz barcha logistika ekranlarida bir xil
**Nima tekshiriladi:** dashboard, oylik hisobot, yuk tashuvchi kartasi, akt-sverki va solishtirish hisoboti bitta qarz qoidasini ishlatadi. Split to'lov hisobga olinadi; (audit ID: LOG-B04, LOG-D01, LOG-P05, LOG-P06, LOG-P07, LOG-P08, LOG-B13, LOG-B19)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-06
**Qadamlar:**
1. Dashboard → Logistika Dashboard: Boshlanish sanasi 01.10.2026, Tugash sanasi 31.10.2026, Yuk tashuvchi DEMO Trans Logistik → filtrlang.
2. Logistika → Yuk tashish → Oylik hisobot: Yil 2026, Oy oktyabr.
3. Tashuvchi kompaniyalar → DEMO Trans Logistik → Batafsil.
4. Shartnomalar → DEMO-TL-2026/01 → Batafsil → "Akt-sverki (ushbu shartnoma)". Sana 31.10.2026.
5. Logistika Dashboard → "Solishtirish hisoboti", sana 01.10.2026–31.10.2026.
**Kutilgan natija:**
- 1: Jami xarajat **2 000 000**, Jami reyslar **2**, 1 ton uchun **50 000** (2 000 000 / 40 t), **Qarz 1 000 000**. Reytingda DEMO Trans Logistik: To'langan 1 000 000, Qoldiq 1 000 000. "Bir nechta valyuta … so'mga o'girilgan" izohi va "valyuta kursi topilmadi" ogohlantirishi yo'q (hammasi UZS).
- 2: DEMO Trans Logistik qatori: Reyslar soni 2, Oldingi qarzdorlik 0, Oy xarajati 2 000 000, Oy to'lovi 1 000 000, **Oy oxiridagi qarzdorlik 1 000 000**.
- 3: Qarz **1 000 000**, Jami reyslar 2, Jami tonnaj 40. OTIF bloki to'ldirilgan (bo'sh/xato emas).
- 4: Yo'l varaqalari 2 × 1 000 000 va to'lov 1 000 000 qatorlari. Yakuniy saldo **1 000 000** (bizning qarzimiz).
- 5: "To'lovsiz yo'l varaqalari"da **faqat DEMO-YV-002** bor, DEMO-YV-001 yo'q. "Invoyssiz yo'l varaqalari"da DEMO-YV-002 bor (u invoysga bog'lanmagan).
- Barcha sahifalar 3 soniyadan tez ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-08. Bank hisobi topilmasa ogohlantirish; boshqa valyutadagi to'lov kurs bilan; to'lov o'chirilganda moliya yozuvi ham o'chadi
**Nima tekshiriladi:** mos bank hisobi yo'q bo'lsa ham to'lov saqlanadi, lekin foydalanuvchi ogohlantiriladi (foydalanuvchi qarori). Yo'l varaqasidan boshqa valyutadagi to'lov to'lov sanasidagi CBU kursi bilan yo'l varaqasi valyutasiga o'giriladi. To'lov o'chirilsa uning taqsimoti ham, moliya yozuvi ham bekor qilinadi; (audit ID: LOG-B08, LOG-B13)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-07
**Qadamlar:**
1. Moliya → Hisoblar: faol bank hisobi **yo'q** valyutani aniqlang (masalan USD). Agar har bir valyutada bank hisobi bo'lsa, bu testni "O'tkazilmadi" deb belgilang.
2. To'lovlar → "Yangi to'lov": Yo'l varaqalari DEMO-YV-002, Yuk tashuvchi DEMO Trans Logistik, To'lov miqdori **10**, To'lov sanasi — bugun yoki o'tgan sana, Valyuta — 1-qadamdagi valyuta, **To'lov hisobi — bo'sh**. Saqlang.
3. Moliya → Tranzaksiyalar'da shu to'lov uchun yozuv bor-yo'qligini tekshiring.
4. Yo'l varaqalari ro'yxatida DEMO-YV-002 ni tekshiring.
5. To'lovlar ro'yxatida shu (10 lik) to'lovni o'chiring.
6. Yo'l varaqalari ro'yxatida DEMO-YV-002 ni qayta tekshiring.
**Kutilgan natija:**
- 2: to'lov saqlanadi va sariq ogohlantirish chiqadi: "To'lov saqlandi, lekin moliyaga (Chiqim) o'tkazilmadi: mos bank hisob-raqami topilmadi. To'lovni tahrirlab, hisob-raqamni tanlang." Agar to'lov sanasi uchun kurs topilmasa (internet yo'q va "Valyuta kurslari"da kurs kiritilmagan), to'lov saqlanmaydi va "… sanasi uchun valyuta kursi topilmadi" xabari chiqadi — bu kutilgan xatti-harakat.
- 3: bu to'lov uchun moliya tranzaksiyasi yo'q.
- 4: DEMO-YV-002 — **"Qisman"**, To'langan ≈ 10 × to'lov sanasidagi CBU kursi (so'mda, masalan 10 USD ≈ 118 000), Qoldiq ≈ 1 000 000 − shu summa.
- 6: DEMO-YV-002 yana "To'lanmagan", Qoldiq 1 000 000. T-LOG-07 dagi qarz raqamlari o'zgarmagan.
- (Qo'shimcha) T-LOG-06 to'lovi o'chirilsa, uning Finance chiqimi ham o'chadi. Bu qadamni faqat test bazasida bajaring.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-09. Transport yuklash simulyatsiyasi
**Nima tekshiriladi:** mahsulot qidiruvi serverdan (butun katalog yuklanmaydi). Hisoblash bir bosishda bir marta ishlaydi. Natija mm o'lchamlarda to'g'ri; (audit ID: LOG-B10, LOG-B11, LOG-P12, LOG-P13, LOG-P15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-01; DEMO-TM-01 "Ofis stoli 1200x600" mahsulotida o'lcham kiritilgan: Uzunligi 1200, Eni 600, Balandligi 100 (mm, qadoqdagi stol) — Muhandislik → Maxsulotlar'da tahrirlang
**Qadamlar:**
1. Logistika → Operatsiyalar → Yuklash simulyatsiyasi.
2. Mahsulot maydoniga "DEMO-TM" yozing va DEMO-TM-01 ni tanlang. Transport turi: DEMO Fura 20t.
3. Brauzerda F12 → Network oynasini oching, hisoblash tugmasini **bir marta** bosing.
4. Tugmani yana 3–4 marta bosing va 3D ko'rinishni aylantirib ko'ring.
**Kutilgan natija:**
- 2: mahsulot ro'yxati yozilgandan keyin serverdan keladi (sahifa ochilganda minglab mahsulot yuklanmaydi).
- 3: Network'da **bitta** hisoblash so'rovi (ikkita emas).
- Natija: maksimal miqdor **1 188 dona** (11 × 4 × 27). Jami hajm **85,536 m³**. Transport hajmi 89,964 m³. To'ldirilganlik **≈95,08%**.
- 4: sahifa sekinlashmaydi va qotmaydi. Konsolda console.log chiqindilari yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-10. Logistikaga ruxsatsiz foydalanuvchi
**Nima tekshiriladi:** logistika sahifalari va JSON endpointlari controller darajasida ruxsat bilan himoyalangan (ajratilgan import va AJAX controller'lari ham "Waybill" ruxsati bilan); (audit ID: LOG-D07, LOG-S02, LOG-B19, LOG-M01)
**Kim:** demo_omborchi (faqat ombor)   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. demo_omborchi bilan kiring. Chap menyuda "Logistika" bo'limi bor-yo'qligini tekshiring.
2. Manzil satriga ketma-ket kiriting: `/Waybill/Index`, `/WaybillPayment/Create`, `/LogisticsDashboard/MonthlyReport`, `/TransportLoadSimulation`, `/Forwarder/Index`, `/WaybillImport/UploadExcel`, `/WaybillLookup/SearchDocuments?type=pinvoice&q=DEMO`.
**Kutilgan natija:**
- Menyuda "Logistika" yo'q.
- Har bir URL'da **"Sizga ruxsat berilmagan!"** sahifasi. Ma'lumotlar ko'rinmaydi, sariq xato sahifasi chiqmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-11. Yo'l varaqalari ro'yxati tezligi
**Nima tekshiriladi:** ro'yxat server tomonida sahifalanadi, filtrlar ishlaydi, hujjat tanlovlari AJAX bilan; (audit ID: LOG-P02, LOG-P03, LOG-P09, LOG-P10, LOG-B14, LOG-B15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-06
**Qadamlar:**
1. Yo'l varaqalari ro'yxatini oching, keyingi sahifaga o'ting, sahifa hajmini o'zgartiring.
2. Yuk tashuvchi filtri = DEMO Trans Logistik, qidiruvga "DEMO-YV" yozing.
3. To'lovlar ro'yxatini va DEMO-YV-001 Batafsil sahifasini oching.
**Kutilgan natija:**
- Har bir sahifa 3 soniyadan tez ochiladi. Sahifalash serverdan (URL'da page parametri).
- 2: faqat DEMO-YV-001 va DEMO-YV-002.
- 3: Batafsil'da to'lovlar ro'yxati va to'langan/qoldiq summalari T-LOG-06 bilan bir xil.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-LOG-12. Yuk tashuvchini o'chirish — eski yo'l varaqalarida nomi va qarzi qoladi
**Nima tekshiriladi:** ekspeditor o'chirilganda tarixiy yo'l varaqalaridan bog'lanish o'chirilmaydi. Qarz hisobotlardan yo'qolmaydi. O'chirilgan yozuvni Edit orqali tiklab bo'lmaydi; (audit ID: LOG-B02 / CORE-B01, LOG-B16, LOG-B09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-LOG-07 (Logistika bo'limidagi oxirgi test)
**Qadamlar:**
1. Tashuvchi kompaniyalar → DEMO Trans Logistik → Batafsil. Manzil satridagi ID'ni yozib oling.
2. O'chirish → tasdiqlang.
3. Yo'l varaqalari ro'yxatini oching.
4. Oylik hisobot: 2026, oktyabr.
5. "Yangi yo'l varaqasi" formasidagi Yuk tashuvchi ro'yxatini oching.
6. Manzil satriga `/Forwarder/Edit/<1-qadamdagi ID>` kiriting.
**Kutilgan natija:**
- 3: DEMO-YV-001 va DEMO-YV-002 da Yuk tashuvchi ustunida hali ham **DEMO Trans Logistik**, shartnoma DEMO-TL-2026/01.
- 4: DEMO Trans Logistik qatori qoladi, Oy oxiridagi qarzdorlik **1 000 000**.
- 5: DEMO Trans Logistik tanlov ro'yxatida yo'q. Tashuvchi kompaniyalar ro'yxatida ham yo'q.
- 6: "topilmadi" (404). Yozuv qayta faollashmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## B. Kadrlar (HR)

### T-HRM-01. HR sozlamalari: ish kalendari va soliq foizi
**Nima tekshiriladi:** /HrSettings sahifasida ish kunlari, bayramlar, kunlik me'yor, qo'shimcha soat koeffitsienti va standart soliq % saqlanadi. Tabel va ish haqi shu sozlamalardan foydalanadi; (audit ID: HRM-B15)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Kadrlar (HR) → Ish haqi → "Sozlamalar" tugmasi (yoki Tabel → Soatbay tabel → "Sozlamalar").
2. Ish kunlari: Dushanba–Juma ✔, Shanba va Yakshanba ✘.
3. Bayramlar maydoniga alohida qatorda **2026-10-01** yozing (O'qituvchilar va murabbiylar kuni).
4. Kunlik me'yor **8**, Qo'shimcha soat koeffitsienti **1,5**, Standart daromad solig'i **12** %. Saqlang.
5. Sahifani yangilang.
**Kutilgan natija:**
- Saqlangandan keyin barcha qiymatlar joyida, bayram ro'yxatida 2026-10-01.
- Hisob: 2026-yil oktyabrda 22 ta Du–Ju kun bor, 1-oktyabr bayram, demak **21 ish kuni** va **fond soati 168** (21 × 8). Bu T-HRM-05 da tekshiriladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-02. Bo'limlar daraxti va tashkiliy sxema
**Nima tekshiriladi:** bo'limlar ierarxiyasi tez quriladi. Tahrirda bo'lim kodi saqlanadi. O'chirilgan quyi bo'lim o'chirishni to'smaydi; (audit ID: HRM-P10, HRM-B19, HRM-D09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Kadrlar boshqaruvi → Tashkiliy struktura → "Bo'lim qo'shish":
   a) Kod DEMO-BSH, Nomi **DEMO Boshqaruv**.
   b) Kod DEMO-ICH, Nomi **DEMO Ishlab chiqarish**.
   c) Kod DEMO-SX1, Nomi **DEMO Sex-1**, ota bo'lim DEMO Ishlab chiqarish.
2. DEMO Sex-1 ni tahrirlang: Kodni **DEMO-SEX1** ga o'zgartiring. Saqlang va qayta oching.
3. Vaqtinchalik "DEMO O'chiriladigan" bo'lim yarating (ota bo'lim — DEMO Boshqaruv). Keyin uni o'chiring.
4. "Tashkiliy sxema"ni oching.
**Kutilgan natija:**
- Daraxtda DEMO Sex-1 DEMO Ishlab chiqarish ostida.
- 2: Kod **DEMO-SEX1** bo'lib saqlangan (eski qiymatga qaytmagan).
- 3: "DEMO O'chiriladigan" o'chiriladi. Shundan keyin DEMO Boshqaruv o'chirilmoqchi bo'lsa, o'chirilgan quyi bo'lim sababli bloklanmaydi (bu qadamni bajarmang, faqat ogohlantirish matnini ko'ring).
- 4: sxema 3 soniyadan tez ochiladi. Faqat faol xodimlar lavozim nomi bilan ko'rinadi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-03. Shtat jadvali va "Xodimlardan yaratish" dublikatsiz
**Nima tekshiriladi:** shtat rejasi pozitsiyalari kiritiladi. Xodimlardan generatsiya mavjud (bo'lim, lavozim) juftligini qayta yaratmaydi; (audit ID: HRM-B13, HRM-P14)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-02
**Qadamlar:**
1. Kadrlar boshqaruvi → Shtat jadvali → "Pozitsiya qo'shish" (amal qilish 01.01.2024 dan, Faol):
   a) DEMO Boshqaruv + mavjud lavozim **L1** (masalan "Menejer"), Soatbay to'lov, Rejalashtirilgan soni 1.
   b) DEMO Ishlab chiqarish + o'sha **L1**, Soatbay, soni 2.
   c) DEMO Sex-1 + mavjud lavozim **L2** (masalan "Sex ishchisi"), Ishbay to'lov, soni 1.
2. T-HRM-04 bajarilgandan keyin: "Xodimlardan yaratish" → DEMO juftliklarini belgilab "Tanlangan pozitsiyalarni yaratish" ni bosing.
**Kutilgan natija:**
- 1: uchta pozitsiya ro'yxatda.
- 2: DEMO bo'limlari uchun **yangi takroriy qator yaratilmaydi** (Shtat jadvalida DEMO qatorlari soni 3 ligicha qoladi). Har bir (bo'lim, lavozim) uchun bitta taklif, soni = xodimlar soni.
- "Faktik" ustuni: DEMO Boshqaruv/L1 = 1, DEMO Ishlab chiqarish/L1 = 1, DEMO Sex-1/L2 = 1 (T-HRM-04 dan keyin).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-04. Xodimlarni qabul qilish (tabel raqami avtomatik)
**Nima tekshiriladi:** tabel raqami qulf ostida avtomatik beriladi va takrorlanmaydi. Xodim, ustama va staj tarixi birga saqlanadi; (audit ID: HRM-B17, HRM-M02, HRM-P11, HRM-D13)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-03 (1-qadam)
**Qadamlar:**
1. Kadrlar boshqaruvi → Xodimlar → "Xodim qo'shish":
   a) **DEMO Aliyev Anvar**: bo'lim DEMO Boshqaruv, lavozim L1, Asosiy maosh **5 040 000**, ishga qabul sanasi **01.03.2024**.
   b) **DEMO Karimov Botir**: bo'lim DEMO Sex-1, lavozim L2, maosh bo'sh, ishga qabul sanasi 01.06.2025.
   c) **DEMO Toshmatov Sardor**: bo'lim DEMO Ishlab chiqarish, lavozim L1, Asosiy maosh 4 000 000, ishga qabul sanasi 01.09.2026.
2. Har birida "Tabel raqami avtomatik generatsiya qilinadi" maydoniga qarang.
3. DEMO Toshmatov ni tahrirlab, tabel raqamini DEMO Aliyev raqamiga o'zgartirib saqlashga urining.
**Kutilgan natija:**
- Uchala xodimga ketma-ket, takrorlanmaydigan tabel raqamlari berilgan.
- 3: dublikat haqida xato. Saqlanmaydi.
- Xodimlar ro'yxati 3 soniyadan tez ochiladi, "Qabul qilingan sana" to'g'ri (01.03.2024, 01.06.2025, 01.09.2026).
- Xodimlar staji sahifasida har biri uchun bitta ochiq davr (qabul sanasidan).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-05. Soatbay tabel: fond soati, jamilar, qoralama himoyasi, POST tasdiq
**Nima tekshiriladi:** yangi tabelning fond soati HR sozlamalaridan olinadi. Bayram va dam olish kunidagi soat qo'shimcha soat hisoblanadi. Tasdiq faqat tugma (POST) orqali ishlaydi. Tasdiqlangan tabelni o'zgartirib bo'lmaydi; (audit ID: HRM-B15, HRM-B05, HRM-S03, HRM-P04, HRM-P05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-01, T-HRM-04
**Qadamlar:**
1. Tabel → Soatbay tabel → "Tabel yaratish": Yil 2026, Oy 10, Bo'lim DEMO Boshqaruv. "Oylik fond soati"ga qarang. Yarating.
2. DEMO Aliyev qatorida har bir ish kuniga **8** soat kiriting: 2, 5–9, 12–16, 19–23, 26–30-oktyabr. **1-oktyabr (bayram) bo'sh qolsin.** Shanba **3-oktyabrga 2** soat kiriting. "Saqlash".
3. Jamilarni tekshiring.
4. Tabellar ro'yxatida tabel ID'sini oling. Yangi tabda `/HrTimesheets/Approve/<ID>` manzilini oching. Keyin ro'yxatni yangilang.
5. Ro'yxatdagi "Tasdiqlash" tugmasini bosing va tasdiqlang.
6. Tabelni qayta oching, biror katakni o'zgartirib "Saqlash"ni bosing.
**Kutilgan natija:**
- 1: Oylik fond soati **168**.
- 3: DEMO Aliyev: Ish soatlari **170**, Ish kunlari **22**, Qo'shimcha **2** (shanba), Qatnashmagan **0** (1-oktyabr bayram, shanba/yakshanba kelmagan kun hisoblanmaydi).
- 4: "topilmadi"/404 sahifasi, tabel holati **Qoralama**ligicha qoladi (GET orqali tasdiqlab bo'lmaydi).
- 5: holat **Tasdiqlangan**.
- 6: "Faqat qoralama holatidagi tabelni saqlash mumkin" (yoki "...tahrirlash mumkin"). Qiymatlar o'zgarmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-06. Soatbay ish haqi: faqat tasdiqlangan tabel, formula va soliqni qayta hisoblash
**Nima tekshiriladi:** hisob faqat tasdiqlangan tabeldan yaratiladi. Soatlik stavka = oylik / fond soati, qo'shimcha soat × 1,5. Soliq foizi butun hisob bo'yicha serverda qayta hisoblanadi. Qoralama hisobni qayta yaratish satrlarni tabeldan qayta quradi, lekin qo'lda kiritilgan mukofot/jarima va soliq foizini saqlaydi; (audit ID: HRM-B09, HRM-B04, HRM-D02, HRM-P05)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-05
**Qadamlar:**
1. Kadrlar (HR) → Ish haqi → "Soatbay hisob yaratish": Yil 2026, Oy 10, Bo'lim DEMO Boshqaruv. Tabel ro'yxatidan T-HRM-05 tabelini tanlab "Yaratish".
2. DEMO Aliyev qatorini tekshiring.
3. Xulosada "Daromad solig'i (%)" ni **10** ga o'zgartiring (maydondan chiqing).
4. Yana **12** ga qaytaring.
5. DEMO Aliyev qatorida "Mukofotlar" maydoniga **100 000** kiriting (maydondan chiqing).
6. Ish haqi → "Soatbay hisob yaratish" ni xuddi 1-qadamdagidek (2026, 10, DEMO Boshqaruv, o'sha tabel) qayta bosing.
7. DEMO Aliyev qatorida "Mukofotlar"ni yana **0** ga qaytaring (keyingi testlar uchun).
8. Ish haqi ro'yxatida bo'sh yoki takroriy "Qoralama" hisob paydo bo'lmaganini tekshiring.
**Kutilgan natija:**
- 1: tanlov ro'yxatida faqat tasdiqlangan tabellar. Qoralama tabel bilan hisob yaratilmaydi ("Tabel topilmadi yoki tasdiqlanmagan").
- 2: Asosiy maosh 5 040 000, Ishlangan soatlar 170. Soatlik stavka 30 000 (5 040 000 / 168).
  Hisoblangan = 168 × 30 000 + 2 × 30 000 × 1,5 = **5 130 000**. Daromad solig'i 12% = **615 600**. To'lovga **4 514 400**.
- 3: sahifa qayta yuklanmasdan: soliq **513 000**, To'lovga **4 617 000**.
- 4: yana 615 600 / 4 514 400. Sahifani yangilasangiz ham shu qiymatlar.
- 5: To'lovga **4 614 400** (5 130 000 + 100 000 − 615 600).
- 6: o'sha hisob (o'sha ID) ochiladi, satrlar tabeldan qayta qurilgan, lekin Mukofot **100 000** va soliq 12% saqlangan; To'lovga hamon **4 614 400**.
- 7: To'lovga yana **4 514 400**.
- 8: 2026/10/DEMO Boshqaruv uchun bitta soatbay hisob, ortiqcha bo'sh hisob yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-07. Ish haqini tasdiqlash — Finance'ga avtomatik chiqim, ikki marta yozilmaydi
**Nima tekshiriladi:** tasdiqlash bilan bir vaqtda Finance'ga **chiqim** (kirim emas) tranzaksiyasi yoziladi (foydalanuvchi qarori). Qayta bosish ikkinchi yozuv yaratmaydi. Tasdiqlangan hisobni qayta hisoblash yoki o'chirish mumkin emas; (audit ID: HRM-B01, HRM-B10, HRM-S03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-06
**Qadamlar:**
1. Hisobning tahrir sahifasida: "Moliya hisobi (kassa/bank)" — UZS kassa, "Chiqim toifasi" — "Avtomatik (oylik)". "Tasdiqlash va moliyaga yozish" → tasdiqlang.
2. Brauzerda "Orqaga" bosib, eski sahifadagi tugmani yana bosing.
3. Moliya → Boshqaruv → Tranzaksiyalar: bugungi sana bo'yicha.
4. Ish haqi → "Soatbay hisob yaratish" bilan 2026/10/DEMO Boshqaruv ni qayta yaratishga urining.
5. Ish haqi ro'yxatida shu hisobni o'chirishga urining.
**Kutilgan natija:**
- 1: "Hisob tasdiqlandi va moliya moduliga chiqim sifatida yozildi". Holat: **"Tasdiqlangan, moliyaga yozilgan"**. Soliq maydoni faqat o'qish uchun.
- 2: "Faqat 'Qoralama' holatidagi hisoblarni tasdiqlash mumkin" yoki "Hisob holati o'zgargan...". Yangi yozuv yo'q.
- 3: faqat **bitta** **Chiqim** tranzaksiyasi: 4 514 400 UZS, kontragent "Xodimlar", maqsad "Ish haqi hisobi 2026-10 (...)".
  Agar nomida "oylik"/"ish haqi" bo'lgan chiqim toifasi bo'lmasa, 1-qadamda "Ish haqi uchun moliya toifasi topilmadi..." xabari chiqadi va hech narsa yozilmaydi.
- 4: "Mavjud soatbay hisob tasdiqlangan yoki o'tkazilgan..." — qayta hisoblanmaydi.
- 5: "Tasdiqlangan yoki o'tkazilgan hisobni o'chirib bo'lmaydi".
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-08. Ishbay tarif va ishbay tabel (standart yil, tarif avtomatik, dublikat)
**Nima tekshiriladi:** ishbay tabel ro'yxati standart holatda joriy yilni ko'rsatadi. Jadvalda tarif sana bo'yicha avtomatik topiladi. Bir partiyadagi takroriy qator saqlanmaydi; (audit ID: HRM-P01, HRM-P02, HRM-D10, HRM-B13, HRM-B14, HRM-P06, HRM-S04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-04
**Qadamlar:**
1. Tabel → Ishbay tabel → Ishbay tariflari → yangi: Maxsulot DEMO-TM-01, Lavozim L2, Operatsiya — mavjudlardan biri (masalan "Yig'ish"), Valyuta UZS, Tarif (birlik uchun) **15 000**, Amal qilishdan 01.10.2026, Faol ✔.
2. Ishbay tabel → "Jadval orqali kiritish" (oy: oktyabr 2026). Qatorlar: DEMO Karimov Botir / DEMO-TM-01 / o'sha operatsiya:
   05.10.2026 — **40**; 12.10.2026 — **30**; 19.10.2026 — **30**.
   4-qator: 05.10.2026 — 40 (1-qatorning aynan takrori).
   O'rtadagi qatorlardan birini qo'shib, keyin o'chirib ko'ring. Saqlang.
3. Ishbay tabel ro'yxatini filtrsiz oching.
**Kutilgan natija:**
- 2: Tarif ustunida har qatorda **15 000** avtomatik. Summa: 600 000; 450 000; 450 000.
  Natija xabari "3 ta yozuv saqlandi" va "1 ta qator o'tkazildi: ... dublikat". O'rtadan qator o'chirilgani keyingi qatorlarning yo'qolishiga olib kelmaydi.
- 3: standart holatda **2026-yil** yozuvlari. Oktyabr / DEMO Sex-1 guruhi: jami miqdor 100, jami **1 500 000**, holat **"Tasdiqlanmagan"**.
- Xodim/mahsulot nomlaridagi maxsus belgilar (masalan qo'shtirnoq) jadvalni buzmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-09. Ishbay tabelni tasdiqlash qulfi
**Nima tekshiriladi:** tasdiqlanmagan ishbay tabel bilan hisob yaratilmaydi. Tasdiqlangandan keyin shu oy/bo'lim yozuvlarini qo'shish, tahrirlash yoki o'chirish bloklanadi; (audit ID: HRM-B02, HRM-B07, HRM-B08, HRM-B20)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-08
**Qadamlar:**
1. Ish haqi → "Ishbay hisob yaratish": 2026, 10, DEMO Sex-1 → yaratishga urining.
2. Ishbay tabel → oktyabr/DEMO Sex-1 guruhida "Tasdiqlash" → tasdiqlang.
3. "Grid tahrirlash" (oktyabr) orqali DEMO Karimov uchun 26.10.2026 — 10 dona qo'shib saqlashga urining.
4. 05.10 yozuvini tahrirlab miqdorni 45 ga o'zgartirishga urining.
**Kutilgan natija:**
- 1: "Ushbu oy (bo'lim) uchun tasdiqlangan ishbay tabel topilmadi". Hisob yaratilmaydi.
- 2: "Tabel muvaffaqiyatli tasdiqlandi". Guruhda tasdiq sanasi ko'rinadi.
- 3 va 4: "2026-10 uchun tegishli bo'lim tabeli allaqachon tasdiqlangan. O'zgartirish mumkin emas." Jami 100 dona / 1 500 000 o'zgarmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-10. Ishbay ish haqi: soliq, Finance va ikki marta to'lash himoyasi
**Nima tekshiriladi:** ishbay hisob faqat tasdiqlangan yozuvlardan tuziladi. Ishbayga ham daromad solig'i qo'llanadi (foydalanuvchi qarori). Bir xodim bir oyda ikki xil (bo'lim va butun kompaniya) ishbay hisobga tushmaydi; (audit ID: HRM-B18, HRM-B02, HRM-B03, HRM-B04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-09
**Qadamlar:**
1. Ish haqi → "Ishbay hisob yaratish": 2026, 10, Bo'lim DEMO Sex-1, tabel — DEMO Sex-1 tabeli → "Yaratish".
2. DEMO Karimov qatorini tekshiring.
3. "Tasdiqlash va moliyaga yozish" (UZS kassa, Avtomatik toifa).
4. Yana "Ishbay hisob yaratish": 2026, 10, Bo'lim — bo'sh (butun kompaniya), tabel "Barcha tabellar" → "Yaratish".
5. 4-qadamda yaratilgan qoralama hisobni o'chiring (tozalash).
**Kutilgan natija:**
- 2: Miqdor 100, Hisoblangan **1 500 000**, Daromad solig'i 12% = **180 000**, To'lovga **1 320 000**. Soliq ustunlari ishbay hisobda ham ko'rinadi.
- 3: Finance'da yangi **Chiqim** 1 320 000 UZS.
- 4: ogohlantirish: "1 ta xodim shu oy uchun boshqa (bo'lim yoki butun kompaniya) hisobida allaqachon bor va qo'shilmadi: DEMO Karimov Botir (...)". Kompaniya hisobida DEMO Karimov yo'q.
- 5: qoralama hisob o'chiriladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-11. Xodimni boshqa bo'limga o'tkazish — ishga kirgan sana saqlanadi
**Nima tekshiriladi:** transfer ishga qabul sanasini o'zgartirmaydi. Eski davr yopiladi, yangisi ochiladi, staj ikki marta sanalmaydi. Staj yozuvini tahrirlashda harakat turi saqlanadi (avval tahrirda e'tiborsiz qolardi); (audit ID: HRM-B06, HRM-B16, HRM-P03, HRM-M01)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-07 (ish haqi hisoblangandan keyin)
**Qadamlar:**
1. Xodimlar → DEMO Aliyev Anvar → Transfer: yangi bo'lim DEMO Ishlab chiqarish, lavozim L1, kuchga kirish sanasi **01.11.2026**, sabab "DEMO o'tkazish". Saqlang.
2. Transferni L2 lavozimga DEMO Boshqaruv bo'limiga qilishga urining (bu juftlik shtatda yo'q).
3. Xodimlar ro'yxatida "Qabul qilingan sana"ga qarang.
4. Kadrlar boshqaruvi → Xodimlar staji → DEMO Aliyev.
5. Shu sahifada DEMO Aliyev uchun "Tahrirlash" (01.11.2026 dan ochiq davr): "Sabab"ni **"DEMO o'tkazish (tahrir)"** ga o'zgartiring, harakat turi "Boshqa lavozimga o'tkazish" qolsin. Saqlang va qayta oching.
**Kutilgan natija:**
- 1: "Xodim muvaffaqiyatli transfer qilindi". Xodimning bo'limi DEMO Ishlab chiqarish.
- 2: "Tanlangan lavozim ushbu bo'lim uchun reja qilinmagan..." — o'tkazilmaydi.
- 3: qabul sanasi **01.03.2024** bo'lib qoladi (01.11.2026 emas).
- 4: ikki davr: 01.03.2024 – **31.10.2026** (DEMO Boshqaruv, yopilgan) va **01.11.2026** – hozir (DEMO Ishlab chiqarish, ochiq). Davrlar ustma-ust tushmaydi. Umumiy staj 01.03.2024 dan uzluksiz, ikki marta sanalmaydi. Tarixsiz xodimlarda "Tahrirlash" havolasi /Edit/0 ga olib bormaydi.
- 5: "Xodim tarixi muvaffaqiyatli yangilandi". Qayta ochilganda sabab yangilangan, harakat turi **"Boshqa lavozimga o'tkazish"**, sanalar (01.11.2026 dan, ochiq) o'zgarmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-12. Xodimni ishdan bo'shatish — tarix yopiladi
**Nima tekshiriladi:** bo'shatishda joriy ochiq davr yopiladi, ustma-ust yangi qator qo'shilmaydi. Bo'shatilgan xodim bilan transfer va qayta bo'shatish bloklanadi. Tabel raqami faqat ishlayotgan xodimlar orasida yagona (bazadagi `IX_HR_EMPLOYEES_TabNo` indeksi bilan bir xil): raqami qayta berilgan bo'shagan xodimni tahrirlash mumkin; (audit ID: HRM-B06, HRM-P04, HRM-B17)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-04
**Qadamlar:**
1. Xodimlar → DEMO Toshmatov Sardor → Bo'shatish: sana **31.10.2026**, sabab "DEMO o'z xohishi". Saqlang.
2. Qayta "Bo'shatish" va "Transfer"ni sinab ko'ring.
3. Xodimlar staji → DEMO Toshmatov.
4. (Faqat noyabr oyida bajarilsa) Soatbay tabel → 2026/11, DEMO Ishlab chiqarish tabelini yarating.
5. Xodimlar → DEMO Toshmatov (nofaol) → Tahrirlash: tabel raqamini **DEMO Aliyev raqamiga** o'zgartiring, "Faol" belgilanmagan qolsin. Saqlang.
6. Yana tahrirlab "Faol" ni belgilab saqlashga urining.
7. Tabel raqamini asl qiymatiga qaytaring ("Faol" belgilanmagan) va saqlang.
**Kutilgan natija:**
- 1: "Xodim muvaffaqiyatli ishdan bo'shatildi". Ro'yxatda nofaol.
- 2: "Bu xodim allaqachon ishdan bo'shatilgan" / "Ishdan bo'shatilgan xodimni transfer qilib bo'lmaydi".
- 3: bitta davr **01.09.2026 – 31.10.2026** (yopilgan), staj 2 oy. Ochiq yoki takroriy qator yo'q.
- 4: tabelga DEMO Aliyev (transferdan keyin) qo'shiladi, DEMO Toshmatov **qo'shilmaydi**.
- 5: saqlanadi (bo'shagan xodim raqami boshqa xodimga qayta berilgan holat; avval bunday xodimni umuman tahrirlab bo'lmasdi).
- 6: "Табельный номер уже существует" — saqlanmaydi, sariq xato sahifasi chiqmaydi.
- 7: saqlanadi, raqam asl holiga qaytgan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-13. Koreshok: faqat login va ruxsat bilan, ish haqi bilan bir xil formula
**Nima tekshiriladi:** Koreshok sahifasi endi anonim ochilmaydi va ruxsat talab qiladi. Hisoblangan summa ish haqi hisobi bilan bir xil. To'langan qism Finance'dan olinadi; (audit ID: HRM-S01, HRM-B12, HRM-D02, HRM-P12, HRM-D15/D16)
**Kim:** demo_admin; tizimdan chiqqan foydalanuvchi; demo_sotuv   **Oldin bajarilgan bo'lishi kerak:** T-HRM-07, T-HRM-10; bazada `HRM_Koreshok_PermissionModule.sql` qo'llangan
**Qadamlar:**
1. Tizimdan chiqing va manzil satriga `/Koreshok` kiriting.
2. demo_sotuv bilan kiring va `/Koreshok` ni oching.
3. demo_admin bilan `/Koreshok`: Xodim DEMO Aliyev Anvar, Dan 01.10.2026, Gacha 30.11.2026 → "Oylikni hisobla".
4. Xuddi shunday DEMO Karimov Botir uchun.
**Kutilgan natija:**
- 1: login sahifasiga yo'naltiriladi.
- 2: "Sizga ruxsat berilmagan!".
- 3: Ishlagan soat 170. Soatbay maoshi — Hisoblangan **4 514 400**, To'langan **4 514 400**, Qoldiq **0**.
- 4: Ishbay maoshi — Hisoblangan **1 320 000**, To'langan **1 320 000**, Qoldiq **0**.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-14. Lavozim yo'riqnomasi (JobDesc): hujjat kodi va fayl faqat o'z hujjatidan
**Nima tekshiriladi:** yo'riqnoma kodi JD-YYYY-NNNNNN takrorlanmaydi. Hujjat bitta tranzaksiyada yaratiladi. Word fayl formatlash bilan chiqadi. Fayl yuklab olish boshqa modul hujjatini bermaydi; (audit ID: HRM-B17, HRM-M02, HRM-S05, HRM-S06, HRM-D13, DOC-B10, DOC-P09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-02; T-DOC-06 (rasmiy xat fayli bo'lishi uchun)
**Qadamlar:**
1. Hujjatlar → Hujjatlar boshqaruvi → Lavozim yo'riqnomalari → "Yangi lavozim yo'riqnomasi qo'shish".
   Lavozim kodi DEMO-JD-01, Lavozim nomi "DEMO Sex ishchisi", Bo'lim DEMO Sex-1, HR lavozimga bog'lash L2.
   Hujjat kontenti: **qalin** so'z, raqamli ro'yxat, "Tom & Jerry" matni, bitta kichik rasm. Saqlang.
2. Yana bitta (DEMO-JD-02) yarating.
3. DEMO-JD-01 → Batafsil → "Word hujjatini yuklash (.docx)" → faylni oching.
4. Rasmiy maktublar → istalgan xat Batafsil → fayl "Yuklab olish" havolasidagi fileId raqamini oling.
   Manzil satriga `/JobDesc/DownloadFile?fileId=<shu raqam>` kiriting.
**Kutilgan natija:**
- 1–2: kodlar **JD-2026-NNNNNN** ko'rinishida, ketma-ket va takrorlanmaydi.
- 3: Word'da qalin matn faqat o'sha so'zda (butun paragraf emas), ro'yxat raqamli, rasm ko'rinadi, "Tom & Jerry" (`&amp;` emas).
- 4: "topilmadi" (404) — boshqa hujjat turidagi fayl berilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-HRM-15. HR sahifalariga ruxsatsiz kirish
**Nima tekshiriladi:** HR sahifalarida ruxsat controller'da tekshiriladi (view'dagi redirect o'ramalari olib tashlangan, "headers already sent" xatosi yo'q). Holat o'zgartiruvchi amallar faqat POST; (audit ID: HRM-M04, HRM-S03, HRM-S06)
**Kim:** demo_omborchi   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. demo_omborchi bilan kiring. URL'larni ketma-ket oching: `/HrPayroll`, `/HrTimesheets`, `/HrEmployees`, `/HrSettings`, `/HrPieceRateTimesheets`, `/JobDesc`.
**Kutilgan natija:**
- Har birida "Sizga ruxsat berilmagan!" sahifasi. Sariq xato sahifasi yoki bo'sh oq sahifa yo'q. Menyuda "Kadrlar (HR)" bo'limi yo'q.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## C. Vazifalar va Hujjatlar

### T-DOC-01. Task sozlamalari: status/prioritet/loyiha, POST orqali o'chirish
**Nima tekshiriladi:** ma'lumotnomalar umumiy controller orqali ishlaydi. O'chirish tugmasi POST qiladi (avval 404 berardi). O'chirilganlar ko'rinmaydi. Ishlatilayotgan qiymatni o'chirib bo'lmaydi; (audit ID: TSK-B02, TSK-B04, TSK-B11)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Tasks / Vazifalar → Task Sozlamalari → Loyihalar → yangi: Kod DEMO-PRJ, Nomi **DEMO Ofis stoli buyurtmasi**.
2. Statuslar → yangi: Kod DEMO_TEST, Nomi "DEMO vaqtinchalik", SortOrder 0. Saqlang.
3. Statuslar ro'yxatida DEMO_TEST qatorida "O'chirish" → tasdiqlang. ID'ni oldin yozib oling.
4. `/TaskStatus/Edit/<ID>` manzilini oching. Vazifa yaratish formasidagi Status ro'yxatini oching.
5. Statuslar ro'yxatida vazifalarda ishlatilayotgan biror statusni (masalan "IN_PROGRESS") o'chirishga urining.
**Kutilgan natija:**
- 2: SortOrder 0 bo'lsa avtomatik qiymat beriladi.
- 3: 404 emas, yozuv o'chadi va ro'yxatdan yo'qoladi.
- 4: Edit → "topilmadi" (404). Vazifa formasida DEMO_TEST yo'q.
- 5: "Bu status vazifalarda ishlatilmoqda. O'chirish mumkin emas." (yoki shunga o'xshash xabar). Status o'chmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-02. Vazifa yaratish — kod takrorlanmaydi
**Nima tekshiriladi:** vazifa kodi (TASK-NN) qulf ostida beriladi va takrorlanmaydi. Dropdownlarda faqat faol qiymatlar va foydalanuvchi ismlari; (audit ID: TSK-B05, TSK-P06, TSK-P07, TSK-P08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-01
**Qadamlar:**
1. Tasks / Vazifalar → Vazifalar → yangi:
   Title "DEMO: DEMO Yog'och MChJ dan qayin taxta yetkazilishini nazorat qilish", Description "500 m² DEMO-XOM-01".
   Status — boshlang'ich (masalan TODO), Priority — yuqori, Project DEMO Ofis stoli buyurtmasi, Assignee demo_admin, Due Date 31.10.2026. Saqlang.
2. Darhol ikkinchi vazifa: Title "DEMO: 100 ta ofis stoli ishlab chiqarish", shu loyiha.
**Kutilgan natija:**
- Ikkala vazifaga ketma-ket, takrorlanmaydigan **TASK-NN** kodlari (bazadagi oxirgi koddan keyingi).
- Formalar 3 soniyadan tez ochiladi. Assignee ro'yxatida foydalanuvchi ismlari.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-03. Statusni o'zgartirishda izoh saqlanadi
**Nima tekshiriladi:** status o'zgartirish sahifasidagi izoh bazaga yoziladi va yo'qolmaydi (avval eski qiymat bilan qayta yozilardi); (audit ID: TSK-B01, TSK-B03)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-02
**Qadamlar:**
1. Vazifalar ro'yxatida birinchi DEMO vazifa → "Status o'zgartirish".
2. Yangi Status "IN_PROGRESS" (Jarayonda). Izoh: "DEMO: ta'minotchi bilan 22.10.2026 ga kelishildi". Saqlang.
3. Vazifa → Batafsil.
4. Yana status o'zgartirish: "DONE", izoh "DEMO: 500 m² qabul qilindi".
**Kutilgan natija:**
- 2: "Status muvaffaqiyatli o'zgartirildi".
- 3: Status "IN_PROGRESS". **Bajaruvchi izohi: "DEMO: ta'minotchi bilan 22.10.2026 ga kelishildi"**.
- 4: izoh yangi matnga almashgan, status DONE.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-04. Kanban va Dashboard tezligi
**Nima tekshiriladi:** Kanban va Vazifalar Dashboard SQL'da hisoblanadi va tez ochiladi. 30 kundan eski yopilganlar standart holatda yashirin. Drag-drop status o'zgartirishi saqlanadi; (audit ID: TSK-P02, TSK-P03, TSK-P04, TSK-P05, TSK-B03, TSK-B08, TSK-B09)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-03
**Qadamlar:**
1. Tasks / Vazifalar → Kanban. Ochilish vaqtini kuzating.
2. Ikkinchi DEMO vazifani "IN_PROGRESS" ustuniga sudrab o'tkazing. Sahifani yangilang.
3. "Barcha yopilganlar" tugmasini bosing, keyin "Faqat oxirgi 30 kun".
4. Dashboard → Vazifalar Dashboard.
**Kutilgan natija:**
- 1 va 4: sahifalar **3 soniyadan tez** ochiladi.
- 2: yangilangandan keyin ham vazifa IN_PROGRESS ustunida.
- 3: "Barcha yopilganlar"da 30 kundan eski DONE/CANCELLED vazifalar ham chiqadi. Standart ko'rinishda ular yo'q, bugun yopilgan DEMO vazifa esa ko'rinadi.
- 4: status/prioritet bo'yicha sonlarda DEMO vazifalar hisobga olingan, grafiklar chiziladi. Muddati o'tganlar belgisi "bajarildi" statusidagilarni o'z ichiga olmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-05. Vazifani "o'chirish" = bekor qilish
**Nima tekshiriladi:** biznes qarori: o'chirish tugmasi vazifani o'chirmaydi, CANCELLED holatiga o'tkazadi (tarix saqlanadi). Oddiy foydalanuvchi faqat o'ziga tegishli vazifani o'chiradi; (audit ID: TSK-B06)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-02
**Qadamlar:**
1. Uchinchi DEMO vazifa yarating: "DEMO: bekor qilinadigan vazifa".
2. Vazifalar ro'yxatida uning "O'chirish" tugmasi → tasdiqlang.
3. Ro'yxatni status filtri "CANCELLED" bilan oching.
**Kutilgan natija:**
- Vazifa yo'qolmaydi, statusi **CANCELLED**. Kodi va tarixi saqlangan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-06. Rasmiy xat (chiquvchi): raqamlash, band raqam, o'chirilgan raqam qayta berilmaydi
**Nima tekshiriladi:** chiquvchi xat raqami qulf ostida beriladi. Forma ko'rsatgan raqam band bo'lsa keyingisi beriladi va xabar chiqadi. Xat va hujjat bitta tranzaksiyada yaratiladi; (audit ID: DOC-B05, DOC-B06, DOC-P11, DOC-B12)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Hujjatlar → Rasmiy maktublar → "Yangi Maktub". "Maktub raqami"ni yozib oling (masalan 0057).
2. Shu "Yangi Maktub" sahifasini **ikkinchi tabda** ham oching (u ham 0057 ko'rsatadi).
3. 1-tab: Yo'nalish "Chiqish", Maktub sanasi 20.10.2026, Tashqi kompaniya nomi **DEMO Mebel Savdo MChJ**, Tashqi kontakt shaxs "DEMO Valiyev", lavozimi "Direktor", Kanal EMAIL.
   Mavzu "DEMO: Ofis stoli narxlari". Asosiy matn: oddiy matn + **qalin** so'z + 2 bandli ro'yxat.
   Mahsulot va narx: "Ofis stoli 1200x600", O'lchami "1200x600", Narx EXW 1 450 000. Saqlang.
4. 2-tab: boshqa mavzu ("DEMO: ikkinchi xat") bilan saqlang.
5. Ikkinchi xatni o'chiring. Yangi (uchinchi) "DEMO: uchinchi xat" yarating.
**Kutilgan natija:**
- 3: "Maktub muvaffaqiyatli yaratildi", raqam 0057.
- 4: "Maktub yaratildi. 0057 raqami band bo'lgani uchun 0058 raqami berildi".
- 5: uchinchi xat **0059** oladi (o'chirilgan 0058 qayta berilmaydi).
- Index faqat tanlangan yil xatlarini ko'rsatadi, o'chirilganlar ko'rinmaydi, tez ochiladi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-07. Kiruvchi xat alohida ketma-ketlikda
**Nima tekshiriladi:** kiruvchi xatning tashqi raqami chiquvchi ketma-ketlikka qo'shilmaydi. Fayl avval tekshiriladi, hujjat, xat va fayl birga saqlanadi; (audit ID: DOC-B06, DOC-B04)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-06
**Qadamlar:**
1. Rasmiy maktublar → "Kiruvchi xatni ro'yxatga olish": Kompaniya/Shaxs nomi DEMO Yog'och MChJ, Xat raqami **5001**, Xat sanasi 21.10.2026.
   Xat ilovasi — 10 MB dan kichik PDF fayl. Saqlang.
2. Xuddi shu formani .docx fayl bilan yuborishga urining.
3. "Yangi Maktub" (chiquvchi) oching va raqamga qarang.
**Kutilgan natija:**
- 1: "Kiruvchi xat muvaffaqiyatli ro'yxatga olindi". Ro'yxatda Yo'nalish "Kirish", raqam 5001. Batafsil'da PDF yuklab olinadi.
- 2: faqat PDF qabul qilinishi haqida xato. Ro'yxatda yarim (faylsiz) yozuv paydo bo'lmaydi.
- 3: chiquvchi raqam **0060** (T-DOC-06 dan keyingi), 5002 emas.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-08. Xat matni xavfsiz ko'rsatiladi va status o'tishlari
**Nima tekshiriladi:** xat mavzusi, kompaniya nomi va matnidagi HTML/skript bajarilmaydi (XSS yo'q). Formatlash saqlanadi. Status faqat ruxsat etilgan yo'nalishda o'zgaradi; (audit ID: DOC-S01, DOC-B08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-06
**Qadamlar:**
1. "Yangi Maktub": Mavzu `DEMO <img src=x onerror=alert('XSS')>`, Tashqi kompaniya nomi `DEMO <b>Test</b>`. Asosiy matnda bitta so'z qalin. Saqlang.
2. Ro'yxat va Batafsil sahifasini oching.
3. T-DOC-06 dagi 0057-xat → Batafsil → "Tasdiqlash uchun yuborish" → "Tasdiqlash".
4. Tasdiqlangan xatda status tugmalarini qidiring.
**Kutilgan natija:**
- 2: hech qanday "XSS" oynasi chiqmaydi. Mavzu va kompaniya nomi teglari bilan oddiy matn bo'lib ko'rinadi. Matndagi qalin so'z qalin ko'rinadi.
- 3: holat Qoralama → Tasdiqlashda → **Tasdiqlangan**.
- 4: tasdiqlangan (va rad etilgan) xatda status o'zgartirish tugmalari yo'q (yakuniy holat).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-09. Word shablon yuklash va xatdan Word hujjat yaratish
**Nima tekshiriladi:** Word shablonlar bazada saqlanadi. Xat ma'lumotlari, formatlangan matn, jadval va direktor ismi shablonga to'ldiriladi. Fayl avtomatik yuklanadi. Yangi generatsiya yangi versiya bo'ladi; (audit ID: DOC-P05, DOC-P07, DOC-P09, DOC-B11, Tasks/Docs 11–12)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-06
**Qadamlar:**
1. Rasmiy maktublar → "Word shablonlar" → "namunaviy shablonni" yuklab oling. Faylni o'zgartirmasdan "Yangi shablon yuklash": nom "DEMO xat shabloni" → "Yuklash".
2. 0057-xat → Batafsil → shablon "DEMO xat shabloni" → "Word hujjat yaratish".
3. Yuklangan .docx ni oching.
4. Yana "Word hujjat yaratish" bosing va "Yaratilgan hujjatlar" ro'yxatiga qarang.
**Kutilgan natija:**
- 1: shablon "Faol" holatda. Yuklashda tanilgan maydonlar ro'yxati ko'rsatiladi.
- 2: "Hujjat yaratildi (versiya 1) va yuklab olinmoqda." — fayl o'zi yuklanadi.
- 3: hujjatda raqam 0057, sana 20.10.2026, DEMO Mebel Savdo MChJ, kontakt "DEMO Valiyev". Matn: qalin so'z va ro'yxat formatlangan.
  Mahsulot jadvali: "Ofis stoli 1200x600", 1 450 000. Direktor ismi korxona sozlamasidan (masalan "X.M. Maxmudov"). `{{...}}` ko'rinishidagi to'ldirilmagan maydon qolmagan, "Xurmatli Hurmatli" takrori yo'q.
- 4: "versiya 2" — ro'yxatda 2 ta fayl, oxirgisi asosiy.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-10. Korxona buyrug'i: raqam, matn, Word
**Nima tekshiriladi:** buyruq raqami ketma-ket va qulf ostida beriladi. Buyruq bitta tranzaksiyada yaratiladi. Matn xavfsiz ko'rinadi, Word generatsiya qilinadi; (audit ID: DOC-B05, DOC-B06, DOC-S01, TSK-P06)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-HRM-02, T-HRM-04
**Qadamlar:**
1. Hujjatlar → Kompaniya buyruqlari → yangi: Buyruq raqamini yozib oling. Buyruq sanasi 20.10.2026, Buyruq turi — mavjud, Tashkilot bo'limi DEMO Ishlab chiqarish.
   Sarlavha "DEMO: 100 ta ofis stolini ishlab chiqarishni boshlash to'g'risida". Matn — bir qalin so'z bilan. Javobgar xodimlar DEMO Aliyev Anvar. "Saqlash".
2. Batafsil → standart shablon bilan "Word hujjat yaratish".
**Kutilgan natija:**
- 1: buyruq formadagi raqam bilan yaratiladi. Raqam band bo'lsa keyingisi beriladi va xabar chiqadi. Forma tez ochiladi.
- 2: Word'da buyruq raqami, sanasi, sarlavhasi, formatlangan matn. To'ldirilmagan `{{...}}` qolmagan.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-11. Korxona standarti (STD): Edit ishlaydi, fayl versiyalari, o'chirilganlar ko'rinmaydi
**Nima tekshiriladi:** STD tahrirlash sahifasi skript xatosiz ishlaydi. Matn o'zgarmasa yangi fayl versiyasi yaratilmaydi. O'zgarsa yangi versiya qo'shiladi. O'chirilgan standart ro'yxatda va havola orqali ochilmaydi; (audit ID: DOC-B01, DOC-B02, DOC-B03, DOC-B07, DOC-B13, DOC-P03, DOC-P04, DOC-P06)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Hujjatlar → Korxona standartlari → yangi: Standart kodi **DEMO-STD-01**, Soha "Ishlab chiqarish".
   Hujjat kontenti — shablondan yoki qo'lda: "Ofis stoli yig'ish tartibi" + 3 band. Eslatma "DEMO". Saqlang.
2. Batafsil → "Versiya tarixi"dagi fayllar sonini yozing.
3. Tahrirlash: muharrir to'liq yuklanishini kuzating (F12 konsolida SyntaxError yo'q). **Faqat Eslatma**ni "DEMO 2" ga o'zgartirib saqlang.
4. Batafsil → Versiya tarixi.
5. Tahrirlash: matnga "4. Sifat nazorati" bandini qo'shib saqlang. Versiya tarixini qarang. Ikkala versiyani yuklab oling.
6. DEMO-STD-01 ni o'chiring. ID'ni oldin yozib oling. `/STDCompanyStandards/Details/<ID>` ni oching.
**Kutilgan natija:**
- 1: standart yaratiladi, hujjat kodi STD-2026-NNNNNN.
- 3: tahrir sahifasi ochiladi va saqlaydi (avval skript xatosi tufayli ishlamasdi). Mavjud matn muharrirda ko'rinadi.
- 4: fayl versiyalari soni **o'zgarmagan** (faqat metadata saqlangan).
- 5: versiya tarixida **yangi fayl** qo'shilgan. Eski va yangi fayl to'g'ri nomlar bilan yuklanadi, ikkinchisida 4-band bor.
- 6: standart ro'yxatda yo'q. Details → "topilmadi" (404).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-12. Hujjat fayllarini yuklab olish: nom va boshqa hujjat fayli
**Nima tekshiriladi:** kirill/qo'shtirnoqli fayl nomlari yuklashda buzilmaydi. Har bir bo'lim faqat o'z hujjat turining faylini beradi; (audit ID: DOC-B14, DOC-S02, DOC-P01, DOC-P08)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** T-DOC-07, T-DOC-11 (5-qadam)
**Qadamlar:**
1. Kiruvchi xatga nomi `Акт "DEMO" сверки.pdf` bo'lgan PDF bilan yana bitta kiruvchi xat yarating (DEMO Yog'och MChJ, raqam 5002). Batafsil'dan faylni yuklab oling.
2. Rasmiy xat faylining fileId raqamini oling. `/STDCompanyStandards/DownloadFile?fileId=<shu raqam>` va `/CompanyOrders/DownloadFile?fileId=<shu raqam>` ni oching.
**Kutilgan natija:**
- 1: fayl aynan `Акт "DEMO" сверки.pdf` nomi bilan saqlanadi, xato yo'q. Batafsil sahifasi tez ochiladi.
- 2: ikkala holatda "topilmadi" (404) — boshqa bo'lim fayli berilmaydi.
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-13. Qo'llanma sahifasi
**Nima tekshiriladi:** foydalanuvchi qo'llanmasi bir marta quriladi va tez ochiladi. Dasturchilar uchun "CRUD arxitekturasi" bo'limi olib tashlangan. Eski /Manual sahifasi yo'q; (audit ID: MAN-P01, MAN-B01, MAN-D01)
**Kim:** demo_admin   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. Chap menyu → "Qo'llanma". Bo'limlar ro'yxatini ko'ring va 3–4 bo'lim orasida o'ting (masalan "Logistika", "Kadrlar boshqaruvi (HR)", "Vazifalar boshqaruvi (Tasks)").
2. Manzil satriga `/Manual` kiriting.
**Kutilgan natija:**
- 1: sahifa va har bir bo'lim 3 soniyadan tez ochiladi. Bo'limlar tartibi barqaror. "CRUD arxitekturasi" bo'limi yo'q.
- 2: "topilmadi" (404).
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

### T-DOC-14. Vazifa va hujjatlarga ruxsatsiz kirish
**Nima tekshiriladi:** Tasks, TaskSettings, xat, buyruq, STD sahifalari [Permission] filtri bilan himoyalangan; (audit ID: DOC-S05, TSK-B03, Tasks/Docs 5–6)
**Kim:** demo_sotuv (faqat CRM)   **Oldin bajarilgan bo'lishi kerak:** —
**Qadamlar:**
1. demo_sotuv bilan kiring. URL'larni oching: `/OfficialLetters`, `/OfficialLetters/Create`, `/CompanyOrders`, `/STDCompanyStandards`, `/TaskStatus`, `/TaskKanban`, `/DocTemplates?docType=OFFICIAL_LETTER`.
**Kutilgan natija:**
- Har birida "Sizga ruxsat berilmagan!". Menyuda "Hujjatlar" va "Tasks / Vazifalar" bo'limlari yo'q.
  (Agar demo_sotuv roliga Tasks ruxsati atayin berilgan bo'lsa, faqat Tasks URL'lari ochiladi — izohga yozing.)
**Natija:** ☐ O'tdi ☐ O'tmadi — Izoh: ____________

---

## Ma'lum cheklovlar
- Logistika (LOG-B13): hisobot va balanslar so'mda — har hujjat o'z sanasidagi CBU kursi bilan; yo'l varaqasi bo'yicha to'langan — yo'l varaqasi valyutasida. Kursi topilmagan hujjat jamiga qo'shilmaydi va ogohlantirish chiqadi. Bitta to'lovga turli valyutadagi yo'l varaqalarini bog'lab bo'lmaydi.
- Logistika (LOG-B05): TariffID `Logistics_2026_10_B05_WaybillTariffID.sql` bilan qo'shilgan bo'lishi kerak. Tarif faqat tahrirda foydalanuvchi uni almashtirsa qayta qo'llanadi.
- Logistika: to'lov taqsimoti ro'yxatdagi tartib bo'yicha (sana bo'yicha yangisi birinchi), tanlash tartibi bo'yicha emas. Ortiqcha summa tanlangan varaqalarga teng bo'linadi.
- Logistika: Excel import sahifasi `/WaybillImport/UploadExcel` ga ko'chgan (eski `/Waybill/UploadExcel` manzili ishlamaydi; ro'yxatdagi tugma yangi manzilga olib boradi).
- HR sozlamalari App_Data/HrSettings.json faylida saqlanadi (butun tizim uchun bitta, bazada emas). Faqat yangi yaratilgan tabellarning fond soatiga ta'sir qiladi.
- Ishbay xodim soatbay tabelga ham tushsa, soatbay hisobga ham kiradi (qasddan saqlangan).
- Koreshok menyuda yo'q (faqat /Koreshok URL). Ishlashi uchun `HRM_Koreshok_PermissionModule.sql` qo'llangan bo'lishi kerak. "To'langan" moliya tranzaksiyasi sanasi bo'yicha olinadi.
- Ishbay tabelga kelajak oy uchun yozuv kiritib bo'lmaydi ("Kelajakka tabel kirita olmaysiz").
- Direktor ismi (COMPANIES.DirectorName) uchun tahrirlash sahifasi yo'q — hozircha SQL bilan o'rnatiladi (DOC-B11).
- STD: versiya raqami har tahrirda oshadi, lekin fayl versiyasi faqat matn o'zgarganda qo'shiladi.
- Qilinmagan: Tasks DataTables server sahifalash (TSK-P10), STD rasmlarini dedupe qilish (DOC-P08), ref jadvallarga IsFinal/ColorHex (MNT-01), DOC_FILES table splitting (DOC-P02), xat JSON'ini alohida ustunlarga ajratish (DOC-P10).
- Brauzerda avval sinalmagan joylar (Tasks Dashboard GROUP BY, Kanban filtri, STD Edit muharriri, xat yaratish va raqamlash) — shu testlar birinchi jonli tekshiruv.
- machineKey web.config'da yo'q: ilova qayta ishga tushsa ochiq formalar login'da 500 xato beradi va foydalanuvchilar tizimdan chiqib ketadi. Test paytida bu kod xatosi deb belgilanmasin.
