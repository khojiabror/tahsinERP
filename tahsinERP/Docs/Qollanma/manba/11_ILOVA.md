# 11. Ilovalar: oylik yopish, javobgarlik va lug'at

Bu bob oldingi boblarni bitta korxona kalendariga birlashtiradi: kim, qachon, qaysi modulda nima qiladi. Batafsil qadamlar har bir bobning o'zida, bu yerda esa havola beriladi.

## 11.1 Noyabr 2026 buyurtmasi: oy davomidagi kalendar

Bu namunaviy ssenariy butun qo'llanma bo'ylab ishlatilgan. UzAutoMotors Powertrain 1 200 dona iPack P1 Yog'och taglik va 3 000 dona iPack B6.1 quti buyurtma qiladi. Jami summa QQSsiz 265 500 000 so'm, QQS bilan 297 360 000 so'm, uchta partiyada yetkaziladi.

| Sana | Bo'lim | Nima qilinadi | Bob |
|---|---|---|---|
| 1–3 noyabr | Sotuv | Shartnoma va buyurtma (SO-2026-11-001), yetkazish jadvali: 09.11, 19.11, 29.11 | 6 |
| 2–4 noyabr | Ishlab chiqarish, Texnolog | Norma va BOM dolzarbligi, oylik reja jadvalda, xomashyo ehtiyoji | 2, 5 |
| 3–5 noyabr | Ta'minot | Talabnoma → tasdiq → buyurtma → shartnoma: Wood Nam Import (taxta), IMPORT EVRO GRAND (karton) | 3 |
| 5–8 noyabr | Ombor | Xarid invoysi bo'yicha kirim, sanoq, qabul; sexga o'tkazma | 3, 4 |
| Har kuni | Ishlab chiqarish | Kunlik fakt (kun va tun smenalari), sifat nazorati, sotuvga o'tkazma | 5 |
| Har kuni | Ombor | Kutilayotgan kirimlarni qabul qilish, sex talabnomalari | 4 |
| 9, 19, 29 noyabr | Sotuv, Logistika | Invoys (Savdo omboridan avtomatik chiqim), TTNK, hisob-faktura; Osiyo Trans Kargo yo'l varaqasi | 6, 7 |
| To'lov kelganda | Moliya | Mijoz to'lovi kontragent va shartnoma bilan; ekspeditor va ta'minotchiga to'lovlar | 7, 8 |
| Oy davomida | HR, bo'lim boshliqlari | Soatbay va ishbay tabellar | 9 |
| 30 noyabr | Ombor | Komissiya sanog'i va Excel inventarizatsiya | 4 |
| 1–5 dekabr | Hamma | Oylik yopish (11.2) | 11 |

## 11.2 Oylik yopish tartibi (butun korxona)

Tizimda davrni qulflash funksiyasi yo'q. Shuning uchun tartibni bo'limlar o'rtasida kelishib, quyidagi ketma-ketlikda bajaring. Har qadam oldingisiga tayanadi: masalan, buxgalter ombor va sotuv hujjatlari to'liq kiritilmaguncha balansni yopmaydi.

| № | Kim | Nima qiladi | Tekshiruv mezoni | Bob |
|---|---|---|---|---|
| 1 | Sotuv | Barcha jo'natmalar invoys qilinganini, xato invoyslar bekor qilinganini tekshiradi | CRM Dashboard'da "Invoys yaratilmagan buyurtmalar" bo'sh | 6 |
| 2 | Ta'minot | Barcha xarid invoyslari kiritilgan va kirim qilinganini tekshiradi | Buyurtma kuzatuvida ochiq "Invoys/Ombor" holatlari asosli | 3 |
| 3 | Ombor | Inventarizatsiya, oylik ombor hisobotini imzolaydi | Kutilayotgan kirim va o'tkazmalar yo'q (yoki sababi yozilgan) | 4 |
| 4 | Ishlab chiqarish | Oylik ishlab chiqarish va sifat hisobotlari | Reja/fakt va sex ombori qoldig'i omborchi bilan teng | 5 |
| 5 | Logistika | Oy yo'l varaqalari va to'lovlari, ekspeditor akt-sverkisi | Logistika qarzi = ekspeditor akt-sverkisi | 7 |
| 6 | HR | Tabellarni tasdiqlaydi, ish haqini hisoblaydi va tasdiqlaydi | Moliyada "Ish haqi hisobi 2026-11" chiqimi bor | 9 |
| 7 | Moliya | Bank va kassa yozuvlari, hisob qoldiqlari | 9000/9001/9002 qoldig'i ko'chirma va kassa daftari bilan teng | 8 |
| 8 | Moliya | Asosiy kontragentlar bilan akt-sverki | Farq 0 yoki sababi aniqlangan | 8 |
| 9 | Moliya, Texnolog | Tannarx faktini yangilash (oy xarajatlari manbalari) | Taqsimlanmagan qoldiq tushunarli | 2 |
| 10 | Moliya | Balans, P&L, Majburiyatlar dashboardi; keyingi oy P&L rejasi | Balans tafsilotlari jamisi asosiy sahifa bilan teng | 8 |
| 11 | Direktor | Yadro moliyaviy hisobot, Kassa oqimi, EBITDA ko'rib chiqadi | Oyning asosiy qarorlari topshiriq sifatida kiritilgan | 8, 10 |
| 12 | Administrator | Zaxira nusxa, foydalanuvchilar va ruxsatlar tekshiruvi | Zaxira nusxa olingan, ishdan ketganlar o'chirilgan | 1 |

> **Diqqat:** Yil oxirida (31.12) Moliya bo'limi opening balansni "Auto-fill" bilan saqlaydi (8-bob). Shundan keyin o'sha sanagacha bo'lgan hujjatlarni o'zgartirmang: ular qarzlarda qayta hisoblanmaydi.

> **Maslahat:** Har oy shu jadvalni bitta umumiy topshiriq sifatida Topshiriqlar modulida yarating va har qadamni ijrochiga biriktiring (10-bob). Shunda kim nima qilganini Kanban'da ko'rasiz.

## 11.3 Javobgarlik jadvali: kim qaysi modulda ishlaydi

Belgilar: **A** — asosiy foydalanuvchi (hujjat yaratadi), **T** — tasdiqlaydi, **K** — faqat ko'radi yoki hisobotdan foydalanadi.

| Modul | Direktor | Xarid | Ombor | Texnolog | Ishlab chiqarish | Sotuv | Logistika | Moliya | HR |
|---|---|---|---|---|---|---|---|---|---|
| Sozlamalar va ruxsatlar (1) | T | | | | | | | | |
| Mahsulot, norma, BOM (2) | K | K | K | A | K | K | | K | |
| Ta'minot (3) | T | A | K | K | K | | K | K | |
| Ombor (4) | K | K | A | | A (sex ombori) | K | | K | |
| Ishlab chiqarish (5) | K | | K | K | A | K | | | K |
| Sotuv (6) | T | | K | | K | A | K | K | |
| Logistika (7) | K | K | | | | K | A | K | |
| Moliya (8) | T | K | | | | K | K | A | K |
| Kadrlar va ish haqi (9) | T | | | | K | | | A (ish haqi) | A |
| Hujjatlar va topshiriqlar (10) | T | A | A | A | A | A | A | A | A |

Bu jadval ruxsatlarni sozlash uchun boshlang'ich nuqta. Aniq ruxsat modullari va rollar 1-bobdagi ruxsatlar matritsasida berilgan.

## 11.4 Tizimda hozircha yo'q yoki cheklangan imkoniyatlar

Quyidagilar kodni tekshirish paytida aniqlangan. Ish jarayonida ularni hisobga oling, keyingi rivojlantirish rejasiga kiritish tavsiya etiladi.

| Mavzu | Hozirgi holat | Nima qilish kerak | Bob |
|---|---|---|---|
| Davrni yopish (qulflash) | Yo'q | 11.2 dagi tartib bo'yicha kelishib ishlang | 8, 11 |
| Valyutani so'mga o'girish | Sotuv va logistikada bor; moliya dashboardlari summani o'girmasdan qo'shadi | Moliya hisobotlarini valyuta bo'yicha alohida ko'ring | 6, 8 |
| P&L | Kassa usulida hisoblanadi (to'lov bo'yicha) | Hisoblash usulini rahbar bilan kelishing | 8 |
| Sotuv buyurtmasidagi "Yetkazilgan" ustuni | Avtomatik to'lmaydi | Yetkazishdan keyin qo'lda yangilang | 6 |
| Invoysda to'lov muddati | Maydon yo'q | Muddatni shartnoma shartlarida yozing | 6 |
| Ta'minotchi to'lovi, doimiy xarajat to'lovi | Saqlangach tahrirlanmaydi | Xato bo'lsa o'chirib, qayta kiriting | 8 |
| Kunlik ishlab chiqarishni qayta kiritish | Shu reja kuni uchun avvalgi yozuvni almashtiradi (qo'shmaydi) | To'liq kunlik miqdorni kiriting | 5 |
| Sifat nazorati va brak | Ombor qoldig'ini o'zgartirmaydi | Brakni alohida ombor hujjati bilan rasmiylashtiring | 5 |
| Inventarizatsiya | Faqat Excel orqali | Shablonni ombor bobidagi namunadan oling | 4 |
| CBU kurslari va Sex sahifalari | Menyuda havola yo'q | Manzil orqali oching: `/CurrencyRates`, `/Shop` | 1 |
| Zaxira nusxa va log tozalash | Tizimda yo'q | SQL Server vositalari bilan (1-bobdagi ro'yxat) | 1 |

## 11.5 Atamalar lug'ati

| Atama | Ma'nosi |
|---|---|
| Akt-sverki | Kontragent bilan o'zaro hisob-kitobni solishtirish dalolatnomasi: hujjatlar, to'lovlar, qoldiq |
| BOM | Mahsulot tarkibi: bitta tayyor mahsulotga qaysi xomashyodan qancha ketishi (normadan quriladi) |
| CBU kursi | O'zbekiston Markaziy banki rasmiy kursi; hujjat sanasidagi kurs bilan so'mga o'giriladi |
| DLOC | Ombor ichidagi joy (lokatsiya) bo'yicha lot hisobi |
| DOH | Days on hand: joriy qoldiq necha kunlik sarfga yetishi |
| FIFO | Birinchi kelgan birinchi chiqadi: chiqimda eng eski lot avval ishlatiladi, tannarx ham shu lot narxida (QQSsiz) |
| Ishbay | Bajarilgan ish miqdoriga qarab hisoblanadigan ish haqi (dona × tarif) |
| Ko'zgu yozuvi | Xarid yoki sotuv invoysi saqlanganda Moliyada avtomatik yaratiladigan qarz yozuvi |
| Kontragent | Hisob-kitob qilinadigan tomon: mijoz, ta'minotchi, ekspeditor, xodim |
| Lot | Bir kirim bilan kelgan bir xil mahsulot partiyasi: o'z sanasi va narxi bilan |
| Norma | Bitta mahsulotga xomashyo sarfi va kesish rejasi (taxta uzunligi, chiqindi) |
| Opening balans | Hisob-kitob boshlanadigan sanadagi boshlang'ich qoldiqlar |
| QQS | Qo'shilgan qiymat solig'i (namunada 12%) |
| SF | Hisob-faktura (soliq hujjati) |
| Shtat jadvali | Bo'limlar bo'yicha tasdiqlangan lavozimlar va ularning soni |
| Tabel | Xodimning oy davomida ishlagan kun va soatlari |
| Tannarx | Mahsulotning o'zini o'zi qoplash narxi: material, mehnat, transport va boshqa xarajatlar |
| TTNK | Tovar-transport yuk xati (jo'natish hujjati) |
| Yo'l varaqasi | Logistikada bitta reys hujjati: ekspeditor, yo'nalish, tarif, summa |
