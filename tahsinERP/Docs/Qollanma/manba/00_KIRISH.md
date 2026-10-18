# 0. Tizim bilan tanishuv

Bu bob "iPack Eco Solutions" MChJ xodimlari uchun tahsinERP'ga birinchi kirish qo'llanmasi. Unda korxonaning qaysi bo'limi qaysi modulda ishlashi, ma'lumot bir bo'limdan ikkinchisiga qanday o'tishi va qo'llanma bo'ylab ishlatiladigan namunaviy "Noyabr 2026 buyurtmasi" ssenariysi tushuntiriladi. Shuningdek, barcha sahifalarda bir xil ishlaydigan elementlar ham shu yerda yoritilgan: tizimga kirish, profil, menyu, ro'yxatlar, qidiruv, eksport, header'dagi belgilar va Bosh sahifadagi rahbariyat paneli.
Bobni barcha xodimlar o'qiydi. Rahbar va bo'lim boshliqlari uchun eng muhim qismlar 0.3, 0.4 va 0.10.

## 0.1 Asosiy tushunchalar

- **Modul** — tizimning katta bo'limi, masalan Ta'minot (SRM), Ombor, Ishlab chiqarish (MES), Sotuvlar (CRM), Logistika, Moliya, Kadrlar (HR). Chap menyuda har bir modul alohida sarlavha ostida turadi.
- **Ma'lumotnoma** — tez-tez o'zgarmaydigan asosiy ma'lumotlar: mahsulotlar, ta'minotchilar, mijozlar, omborlar, o'lchov birliklari, valyutalar. Hujjatlar ma'lumotnomadagi yozuvlarni tanlab ishlatadi.
- **Hujjat** — kundalik amal: xarid buyurtmasi, ombor kirimi, ishlab chiqarish yozuvi, sotuv invoysi, yo'l varaqasi, to'lov. Hujjatning holati (status) bo'ladi, masalan "Qoralama", "Tasdiqlangan", "Bekor qilingan".
- **Rol** — foydalanuvchilar guruhi, masalan Xaridchi, Savdogar, Texnolog. Kim qaysi sahifani ko'ra olishi va o'zgartira olishi rolga bog'liq. Bu 1-bobda batafsil yoritilgan.
- **Ko'rish (View) va o'zgartirish (Change) huquqi** — ko'rish huquqi bilan ro'yxat va batafsil sahifalarni ochasiz. Yaratish, tahrirlash, o'chirish, tasdiqlash va qabul qilish uchun o'zgartirish huquqi kerak.
- **Xomashyo ombori, Ishlab chiqarish ombori, Savdo ombori** — iPack'dagi uchta asosiy ombor. Kelgan narsa avval Xomashyo omboriga tushadi, sex Ishlab chiqarish omboridan oladi, tayyor mahsulot Savdo omboriga o'tadi va sotuv shu ombordan chiqadi.
- **FIFO** — ombordan chiqim har doim eng avval kelgan partiyadan (lotdan) olinadi. Lotni qo'lda tanlash shart emas. Chiqim tannarxi shu lot narxidan QQSsiz hisoblanadi.
- **CBU kursi** — O'zbekiston Markaziy banki kursi. Valyutadagi summalar hisobotlarda hujjat sanasidagi kurs bilan so'mga o'giriladi.
- **MRP (moddiy javobgar shaxs)** — omborga mas'ul xodim. Omborga kelgan kirimlarni u qabul qiladi.

## 0.2 iPack korxonasi va tahsinERP modullari xaritasi

iPack Andijonda yog'och va karton qadoq ishlab chiqaradi. Asosiy tayyor mahsulotlar: iPack P1 va P4 "Yog'och taglik" (paddon), iPack 5.1 / 3.4.1 / 3.2.1 "Yog'och yashik", iPack B6.1 "B6.1 quti", iPack B7 / B10 "Karton quti", iPack B5/B6 "Karton". Asosiy mijozlar avtokomponent zavodlari: AJ "UzAutoMotors Powertrain", MChJ "Avtokomponent", "Uz-Koram Kompani", AJ QK "UzSaemyung Co", MChJ "Avtooyna", MChJ "Autorubber", MChJ QK "UzDongyang Co".

Quyidagi jadvalda har bir bo'lim qaysi modul va menyu bandidan foydalanishi ko'rsatilgan.

| Bo'lim / lavozim | iPack roli | Asosiy modullar (menyu) | Nima qiladi |
|---|---|---|---|
| Boshqaruv (direktor X.M. Maxmudov, o'rinbosarlar) | Ta'sischi, Admin | Bosh sahifa, Dashboard, Moliya → Hisobotlar | KPI, trend, qarz va balansni kuzatadi, talabnomalarni tasdiqlaydi |
| Xarid bo'limi (xarid mutaxassisi) | Xaridchi | Ta'minot (SRM) → Ta'minotchilar, Xarid jarayoni, Kuzatuv va tender | Wood Nam Import, WOODLAND BIZNES, IMPORT EVRO GRAND bilan shartnoma, buyurtma, invoys |
| Ombor (omborchi) | Omborchi uchun alohida rol (1-bobga qarang) | Omborxona → Ombor → Operatsiyalar | Kirimni qabul qiladi, chiqim va o'tkazma qiladi, qoldiqni kuzatadi |
| Texnologiya bo'limi (texnolog, konstruktor) | Texnolog | Engineering → Muhandislik (BOM va norma, Maxsulotlar, Tannarx) | Mahsulot kartochkasi, BOM, norma-sarf, tannarx |
| Ishlab chiqarish bo'limi (sex boshlig'i, qadoqlovchi) | Texnolog+Ishlab chiqarish, Savdo+Ishlab chiqarish | MES - Ishlab chiqarish → Ishlab chiqarish | Reja, kunlik fakt, sifat nazorati, nuqsonlar |
| Sotuv bo'limi (sotuv mutaxassisi) | Savdogar, Xaridchi+Savdogar | Sotuvlar (CRM) → Xaridorlar, Shartnomalar, Buyurtmalar, Invoyslar, Счёт-фактура, ТТНК | Mijoz bilan shartnoma, buyurtma, invoys va hujjatlar |
| Logist | Savdogar yoki alohida rol | Logistika → Yuk tashish | Osiyo Trans Kargo bilan shartnoma, tarif, yo'l varaqasi, to'lov |
| Buxgalteriya | Admin yoki alohida moliya roli | Moliya → Boshqaruv, Hisobotlar | Hisoblar (9000–9006), tranzaksiyalar, qarzlar, P&L, balans |
| Kadrlar (HR) | Alohida HR roli | Kadrlar (HR) → Kadrlar boshqaruvi, Tabel, Ish haqi | Xodim, lavozim, shtat, tabel, ish haqi |
| Barcha xodimlar | har qanday | Tasks / Vazifalar, Hujjatlar boshqaruvi | Topshiriqlar, buyruqlar, rasmiy maktublar, standartlar |
| Tizim administratori | Admin | Sozlamalar, Admin panel | Foydalanuvchi, rol, ruxsat, ombor, valyuta, litsenziya |

> **Diqqat:** Menyuda faqat sizga ruxsat berilgan bo'limlar ko'rinadi. Kerakli bo'lim yo'q bo'lsa, bu xato emas. Administratorga murojaat qiling, u rolingizga huquq qo'shadi.

## 0.3 Korxona bo'ylab ma'lumot oqimi

tahsinERP'da har bir bo'lim o'z hujjatini kiritadi. Keyingi bo'lim shu hujjatdan davom etadi, shuning uchun bir ma'lumot ikki marta qo'lda yozilmaydi. Asosiy zanjir quyidagicha:

```
 XARID (SRM)            OMBOR                    ISHLAB CHIQARISH (MES)
 Talabnoma              Xomashyo ombori          Reja (ProductPlan)
   -> Shartnoma           kirim qabul qilinadi     -> Kunlik fakt
   -> Buyurtma            (qoldiq +)                -> Sifat nazorati
   -> Xarid invoysi  -->  Chiqim / o'tkazma  -->  Ishlab chiqarish ombori
        |                 (FIFO, qoldiq -)          (xomashyo sarfi, tayyor mahsulot +)
        |                                               |
        v                                               v
 MOLIYA  <---------------------------------------  SAVDO OMBORI
 To'lov, qarz           "Ishlab chiqarishdan sotuvga" o'tkazmasi
 (ta'minotchiga)                                        |
        ^                                               v
        |                                        SOTUV (CRM)
        |                                        Shartnoma -> Buyurtma -> Invoys
        |                                        (Savdo omboridan avtomatik chiqim)
        |                                        Счёт-фактура, ТТНК
        |                                               |
        |                                               v
        +---------- mijoz to'lovi, qarz <----- LOGISTIKA
                    ekspeditor to'lovi          Yo'l varaqasi, tarif, to'lov

 PARALLEL:  HR (xodim, tabel, ish haqi -> Moliyaga avtomatik chiqim)
            Hujjatlar va Vazifalar (buyruq, maktub, topshiriq — barcha bo'limlar)
```

Har bir bosqichda tizimda nima o'zgarishi:

| Bosqich | Kim kiritadi | Hujjat | Natija boshqa modullarda |
|---|---|---|---|
| 1. Xarid talabnomasi | Ishlab chiqarish yoki ombor | Xarid arizasi (Draft → Submitted → Approve/Reject) | Tasdiqlangach xaridchi buyurtma beradi |
| 2. Xarid shartnomasi va buyurtmasi | Xaridchi | Xarid shartnomasi, Xarid buyurtmasi | Buyurtma kuzatuvida ko'rinadi |
| 3. Xarid invoysi | Xaridchi | Xarid invoysi (summa QQS bilan) | Moliyada ta'minotchiga qarz paydo bo'ladi |
| 4. Omborga kirim | Omborchi (MRP) | Kirim → qabul qilish | Xomashyo ombori qoldig'i oshadi, FIFO lot yaratiladi |
| 5. Sexga o'tkazma | Omborchi | Chiqim (o'tkazma) | Xomashyo ombori kamayadi, Ishlab chiqarish ombori oshadi |
| 6. Ishlab chiqarish | Sex boshlig'i | Reja, kunlik ishlab chiqarish, sifat nazorati | Xomashyo norma bo'yicha sarflanadi, tayyor mahsulot paydo bo'ladi |
| 7. Savdo omboriga o'tkazish | Omborchi / sex | Ishlab chiqarishdan sotuvga | Savdo ombori qoldig'i oshadi |
| 8. Sotuv | Sotuvchi | Sotuv shartnomasi, buyurtma, invoys | Invoys Savdo omboridan avtomatik chiqim qiladi, mijoz qarzi paydo bo'ladi |
| 9. Hujjatlar | Sotuvchi | Счёт-фактура, ТТНК | Mijozga yuboriladigan rasmiy hujjatlar |
| 10. Yetkazish | Logist | Yo'l varaqasi, to'lov | Ekspeditor oldidagi qarz va logistika xarajati |
| 11. Moliya | Buxgalter | Tranzaksiya, to'lov | Hisoblar qoldig'i, qarzlar, P&L, balans |
| Parallel: HR | HR mutaxassisi | Tabel, ish haqi | Ish haqi tasdiqlanganda Moliyaga avtomatik chiqim yoziladi |
| Parallel: hujjat va vazifa | Barcha | Buyruq, maktub, topshiriq | Ijro nazorati, Bosh sahifadagi "Vazifalar bajarilishi" |

> **Diqqat:** Bekor qilingan sotuv invoysi qarzga ham, Bosh sahifadagi "Hisob-faktura hajmi"ga ham kirmaydi. Xato invoysni o'chirmang, uni bekor qiling.

> **Maslahat:** Valyutadagi hujjat (masalan, USD'dagi import yog'och yoki RUB'dagi xarid) hisobotlarda hujjat sanasidagi CBU kursi bilan so'mga o'giriladi. Kurslar ro'yxatini 1-bobdagi "Valyuta kurslari" sahifasida tekshirasiz.

## 0.4 Namunaviy ssenariy: "Noyabr 2026 buyurtmasi"

Qo'llanmaning barcha boblari bitta misol bilan bog'langan. Har bir bobda shu buyurtmaning o'sha bo'limga tegishli qismi bajariladi.

**Buyurtma:** AJ "UzAutoMotors Powertrain" noyabr 2026 da quyidagilarni buyurtma qiladi:

| Mahsulot | Miqdor | Narx (QQSsiz) | Summa (QQSsiz) | QQS 12% | Jami (QQS bilan) |
|---|---|---|---|---|---|
| iPack P1 "Yog'och taglik" | 1 200 dona | 185 000 so'm | 222 000 000 so'm | 26 640 000 so'm | 248 640 000 so'm |
| iPack B6.1 "B6.1 quti" | 3 000 dona | 14 500 so'm | 43 500 000 so'm | 5 220 000 so'm | 48 720 000 so'm |
| **Jami** | | | **265 500 000 so'm** | **31 860 000 so'm** | **297 360 000 so'm** |

Yetkazish 3 partiyada bo'ladi. Misol uchun har partiyada 400 dona P1 va 1 000 dona B6.1 jo'natiladi. Bitta partiyaning summasi 88 500 000 so'm, QQS bilan 99 120 000 so'm.

**Ssenariyning boblar bo'yicha rejasi:**

| Qadam | Nima qilinadi | Bob |
|---|---|---|
| 1 | Foydalanuvchilar va rollar sozlanadi, omborlarga MRP biriktiriladi, USD/RUB/KRW kurslari tekshiriladi | 1. Tizim sozlamalari va boshqaruv |
| 2 | P1 va B6.1 kartochkalari, BOM va norma-sarf (taxta "Yog'och 100*50*6000", "Karton_2090x1370", mix) tekshiriladi | 2. Mahsulot va texnologiya (Muhandislik) |
| 3 | Kerakli xomashyo uchun xarid talabnomasi, Wood Nam Import'dan taxta va IMPORT EVRO GRAND'dan karton bo'yicha shartnoma, buyurtma va invoys | 3. Ta'minot (xarid) |
| 4 | Taxta va karton Xomashyo omboriga kirim qilinadi va qabul qilinadi, keyin sexga o'tkaziladi | 4. Ombor |
| 5 | Noyabr uchun ishlab chiqarish rejasi, kunlik fakt, sifat nazorati, nuqsonlar | 5. Ishlab chiqarish (MES) |
| 6 | Tayyor P1 va B6.1 Savdo omboriga o'tkaziladi | 4. Ombor |
| 7 | UzAutoMotors Powertrain bilan shartnoma, buyurtma, 3 ta invoys, Счёт-фактура va ТТНК | 6. Sotuv (CRM) |
| 8 | Osiyo Trans Kargo bilan 3 ta yo'l varaqasi va to'lov | 7. Logistika |
| 9 | Ta'minotchilarga to'lov, mijozdan tushum, qarzlar, P&L va balans | 8. Moliya |
| 10 | Noyabr tabeli va ish haqi (sex xodimlari ishbay tabel bilan) | 9. Kadrlar va ish haqi (HR) |
| 11 | Buyurtma bo'yicha topshiriqlar, buyruq va rasmiy maktub | 10. Hujjatlar va topshiriqlar |
| 12 | Natijani Bosh sahifadagi rahbariyat panelida va bo'lim dashboardlarida ko'rish | 0.10 va har bobning hisobotlar qismi; oylik yopish — 11. Ilovalar |

> **Maslahat:** Ssenariyni o'rganish uchun avval test bazada bajaring. Ishchi bazada esa haqiqiy ma'lumotlar bilan xuddi shu tartibda ishlaysiz.

## 0.5 Tizimga kirish va chiqish

**Kim:** barcha xodimlar   **Menyu:** brauzerda tizim manzili (login sahifasi)

1. Brauzerda administrator bergan tizim manzilini oching. "tahsinERP tizimi — Xush kelibsiz!" sahifasi chiqadi.
2. **FOYDALANUVCHI NOMI** maydoniga loginingizni yozing, masalan `sotuv_karimov`.
3. **KALIT SO'ZI** maydoniga parolni yozing.
4. Kerak bo'lsa **Meni tizimda saqlab qolish (1 soat)** belgisini qo'ying.
5. Kirish tugmasini bosing. Bosh sahifa ("Rahbariyat nazorat paneli") ochiladi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| FOYDALANUVCHI NOMI | Administrator bergan login (katta-kichik harf farq qilmaydi) | `ombor_aliyev` |
| KALIT SO'ZI | Shaxsiy parol | (o'zingiz bilasiz) |
| Meni tizimda saqlab qolish (1 soat) | Belgilansa, sessiya 1 soat saqlanadi | Shaxsiy kompyuterda |

Kirishda chiqishi mumkin bo'lgan xabarlar:

| Xabar | Sababi | Nima qilish kerak |
|---|---|---|
| "Bunday foydalanuvchi mavjud emas!" | Login noto'g'ri yozilgan yoki foydalanuvchi o'chirilgan | Loginni tekshiring, administratorga murojaat qiling |
| "Kalit so'zi noto'g'ri!" | Parol xato | Qayta urinib ko'ring |
| "Kalit so'zi noto'g'ri! 5 marta xato — 15 daqiqaga bloklandi." | Ketma-ket 5 marta xato parol | 15 daqiqa kuting yoki administratordan parolni yangilashni so'rang |
| "Juda ko'p noto'g'ri urinish. N daqiqadan keyin qayta urinib ko'ring." | Blok muddati tugamagan | Ko'rsatilgan vaqtni kuting. Bu vaqtda to'g'ri parol ham qabul qilinmaydi |
| "Foydalanuvchi faol emas. Administrator bilan bog'laning." | Administrator sizni faolsizlantirgan | Administratorga murojaat qiling |
| Litsenziya haqidagi xabar | Korxona litsenziyasi yoki sizning o'rningiz (seat) muddati tugagan | Administratorga murojaat qiling (1-bob, Litsenziya) |

**Chiqish:** yuqori o'ng burchakdagi profil rasmini bosing va **Tizimdan chiqish** ni tanlang. Chiqqandan keyin brauzerning "Orqaga" tugmasi bilan ham ichki sahifalarni ochib bo'lmaydi, tizim login sahifasiga qaytaradi.

> **Diqqat:** Sessiya 60 daqiqa harakatsizlikdan keyin tugaydi. Uzoq forma to'ldirayotgan bo'lsangiz, ma'lumotni vaqti-vaqti bilan saqlab boring.

> **Maslahat:** Umumiy kompyuterda "Meni tizimda saqlab qolish" ni belgilamang va ishni tugatgach albatta **Tizimdan chiqish** ni bosing.

## 0.6 Parol va profil sozlamalari

**Kim:** barcha xodimlar   **Menyu:** profil rasmi → Profil sozlamalari (yoki Sozlamalar → Mening Profilim)

Profilda faqat o'zingizning ma'lumotlaringizni o'zgartirasiz. Boshqa xodimning profilini bu yerdan ochib bo'lmaydi.

1. Yuqori o'ngdagi profil rasmini bosing va **Profil sozlamalari** ni tanlang.
2. Kerakli maydonlarni o'zgartiring (F.I.Sh, E-mail, rasm).
3. Parolni almashtirish uchun **Yangi kalit so'zi** va **Yangi kalit so'zini qayta kiriting** maydonlariga bir xil yangi parolni yozing. Parolni o'zgartirmasangiz, bu maydonlarni bo'sh qoldiring.
4. **Amaldagi kalit so'zi** maydoniga hozirgi parolingizni yozing. Busiz hech qanday o'zgarish saqlanmaydi.
5. Saqlash tugmasini bosing. Muvaffaqiyatli bo'lsa, Bosh sahifa ochiladi va yuqoridagi ism yangilanadi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Yangi rasm yuklash | Profil surati (2 MB dan kichik) | Xodim fotosurati |
| Foydalanuvchi nomi | Faqat o'qish uchun, o'zgartirib bo'lmaydi | `sotuv_karimov` |
| Foydalanuvchi F.I.Sh | To'liq ism-sharif | Karimov Jasur Anvarovich |
| E-mail | Ish pochtasi | jasur.karimov@ipack.uz |
| Amaldagi kalit so'zi | Hozirgi parol (majburiy) | |
| Yangi kalit so'zi | Yangi parol yoki bo'sh | |
| Yangi kalit so'zini qayta kiriting | Yangi parolning takrori | |

Xato xabarlari: "Amaldagi kalit so'zi noto'g'ri." (hozirgi parol xato), "Yangi kalit so'zi va takrori mos kelmadi.", "Suratni yuklab bo'lmadi, u 2 MB dan ortiq."

**Natija:** yangi ism header'da va hujjatlardagi "kim yaratgan" ustunlarida ko'rinadi. Parol almashgach eski parol bilan kirib bo'lmaydi.

> **Maslahat:** Header'da ismingiz ostida lavozimingiz ko'rinadi. U HR modulidagi xodim kartochkasidan olinadi va F.I.Sh aynan bir xil yozilgandagina chiqadi. Lavozim chiqmasa, rolingiz nomi ko'rsatiladi.

> **Diqqat:** Parolni unutsangiz, uni o'zingiz tiklay olmaysiz. Administrator foydalanuvchi kartochkasida yangi parol o'rnatadi (1-bob).

## 0.7 Ekran tuzilishi va menyu

Ekran uch qismdan iborat:

- **Header (yuqori qator).** Til tanlash (O'zbekcha / Русский), to'liq ekran tugmasi, ismingiz va lavozimingiz, profil rasmi. Profil rasmi bosilganda menyu ochiladi: **Profil sozlamalari**, **Litsenziya** (faqat Admin), **Topshiriqlar**, **Omborga kirim** (faqat ombor mas'uliga), **Tizimdan chiqish**.
- **Chap menyu.** Modullar sarlavhalar bo'yicha guruhlangan. Kichik ekranda menyu yashiriladi va chap yuqoridagi uch chiziqli tugma bilan ochiladi.
- **Ish maydoni.** Tepada "non bo'laklari" (breadcrumb) yo'li bor, masalan "Uy belgisi → Valyutalar → Kurslar". Uy belgisi Bosh sahifaga qaytaradi.

Chap menyuning to'liq tuzilishi (ruxsatingizga qarab bir qismi ko'rinmasligi mumkin):

| Sarlavha | Menyu bandi | Ichidagi sahifalar |
|---|---|---|
| — | Bosh sahifa | Rahbariyat nazorat paneli |
| — | Dashboard | Umumiy Dashboard, Moliya Dashboard (Umumiy ko'rinish, Kassa oqimi, Kategoriyalar tahlili, Hisoblar tahlili, Chiqimlar/Kirimlar tahlili, Hisoblardagi qoldiq, Foyda va zarar, EBITDA, Balans, Majburiyatlar dashboard, Yadro moliyaviy hisobot), Tannarx, CRM, Logistika, HR, MES, SRM, Ombor va Vazifalar dashboardlari |
| Ta'minot (SRM) | Hisobotlar | SRM Dashboard, Coverage, ABC / XYZ, Xavfsiz zahira |
| | Ta'minotchilar | Ta'minotchilar, Ma'lumotnoma |
| | Xarid jarayoni | Xarid arizalari, Xarid shartnomalari, Xarid buyurtmalari, Xarid invoyslari |
| | Kuzatuv va tender | Buyurtma kuzatuv, Tender taqqoslash |
| Logistika | Yuk tashish | Ma'lumotnoma va shartnomalar (Tashuvchi kompaniyalar, Shartnomalar, Tariflar, Transport turlari), Operatsiyalar (Yo'l varaqalari, To'lovlar, Yuklash simulyatsiyasi), Oylik hisobot |
| Omborxona | Ombor | Operatsiyalar (Qoldiqlar, Kirimlar, Chiqimlar, Ishlab chiqarishdan sotuvga), DOH Dashboard, Oylik hisobot, Omborlar, DLOC (joylashuvlar) |
| Xarid | Xaridlar | Buyurtmalar, Maxsulotlar bazasi, Shartnomalar |
| Moliya | Moliya | Boshqaruv (Hisoblar, Tranzaksiyalar, Kategoriyalar, Balans kategoriyalari, Opening balans, P&L elementlar, P&L rejalar), Hisobotlar (Kassa oqimi, Hisoblardagi qoldiq, Foyda va zarar, EBITDA, Balans, Yadro moliyaviy hisobot, Doimiy harajatlar), Aksiyadorlar (Ro'yxat, Kompaniya qiymati) |
| Engineering | Muhandislik | BOM va norma (BOM, Norma-sarf), Maxsulotlar (Barcha, Tayyor maxsulot, Yarim tayyor, Xomashyo, Maxsulot turlari), Tannarx (Tannarx, Dashboard, Hisobot) |
| MES - Ishlab chiqarish | Ishlab chiqarish | Dashboard, Ishlab chiqarish rejasi, Kunlik ishlab chiqarish, Sifat nazorati, Nuqson turlari, Oylik hisobotlar (Ishlab chiqarish oylik, Sifat oylik) |
| Sotuvlar (CRM) | Sotuvlar | CRM Dashboard, Xaridorlar, Ma'lumotnoma, Shartnomalar, Buyurtmalar, Invoyslar, Hisobotlar (Barcha hisobotlar, Oylik savdo hisoboti, Mijoz bo'yicha sotuv, Mahsulot bo'yicha sotuv), Счёт-фактура, ТТНК |
| Kadrlar (HR) | HR Dashboard, Kadrlar boshqaruvi, Tabel, Ish haqi | Tashkiliy struktura, Lavozimlar, Xodimlar, Xodimlar staji, Tariflar, Shtat jadvali; Soatbay tabel, Ishbay tabel, Ishbay tariflari |
| Tasks / Vazifalar | Tasks / Vazifalar | Dashboard, Vazifalar, Kanban, Task Sozlamalari (Statuslar, Prioritetlar, Loyihalar) |
| Hujjatlar | Hujjatlar boshqaruvi | Kompaniya buyruqlari, Rasmiy maktublar, Korxona standartlari, Lavozim yo'riqnomalari |
| — | Qo'llanma | Tizim ichidagi qisqa qo'llanma |
| Sozlamalar | Sozlamalar | Korxona, Omborlar, O'lchov birliklari, Valyutalar, Mening Profilim |
| — | Admin panel | Faqat administrator (1-bob) |

> **Diqqat:** Menyuda ko'rinmaydigan sahifani manzilni qo'lda yozib ham ochib bo'lmaydi. Bunday holda "Sizga ruxsat berilmagan!" sahifasi chiqadi. Undagi **Asosiy sahifa** tugmasi bilan qaytasiz.

> **Maslahat:** "Xarid → Xaridlar" bo'limi alohida (eski) xarid ro'yxatlari uchun. iPack'ning asosiy xarid jarayoni "Ta'minot (SRM) → Xarid jarayoni" orqali yuritiladi.

## 0.8 Umumiy elementlar: ro'yxatlar, qidiruv, filtrlar, sahifalash, eksport

Deyarli barcha ro'yxat sahifalari (Kirimlar, Buyurtmalar, Invoyslar, Xodimlar va h.k.) bir xil tuzilgan.

| Element | Qayerda | Qanday ishlaydi |
|---|---|---|
| **Yangi / ... qo'shish** tugmasi | Ro'yxat tepasida | Yaratish formasini ochadi. O'zgartirish huquqi bo'lmasa, tugma ko'rinmaydi |
| Qidiruv maydoni ("Qidirish...") | Jadval tepasida o'ngda | Yozgan matningiz jadvalning barcha ustunlaridan qidiriladi, natija darhol filtrlanadi |
| Sana filtrlari (Dan / Gacha) | Hujjat ro'yxatlarida | Davrni tanlab **Ko'rsatish** yoki **Yangilash** ni bosasiz. Ko'p ro'yxatlar standart holatda oxirgi 30 kun yoki joriy oyni ko'rsatadi |
| Boshqa filtrlar | Holat, ombor, rol, mijoz va h.k. | Ro'yxatni tanlangan qiymat bo'yicha toraytiradi |
| Ustun sarlavhasi | Jadvalda | Bosilsa, shu ustun bo'yicha saralanadi. Qayta bosilsa, teskari tartib |
| Sahifa hajmi (10 / 25 / 50 / 100 / Barchasi) | Jadval tepasida chapda | Bir sahifada nechta qator ko'rinishini belgilaydi |
| Sahifalash | Jadval ostida | Sahifalar orasida o'tish |
| **Excel** | Jadval tepasida | Ekrandagi jadvalni .xlsx faylga yuklaydi |
| **PDF** | Jadval tepasida | Ekrandagi jadvalni PDF faylga yuklaydi |
| Amallar ustuni | Har qatorda | Batafsil (ko'rish), Tahrirlash, O'chirish belgilari |

Eksport bo'yicha:

1. Kerakli filtrlarni qo'ying (masalan, Qoldiqlar sahifasida "Xomashyo ombori").
2. Barcha qatorlar kerak bo'lsa, sahifa hajmini **Barchasi** ga qo'ying.
3. **Excel** yoki **PDF** ni bosing. Fayl brauzerning yuklab olishlar papkasiga tushadi.

> **Diqqat:** PDF tugmasi birinchi bosilganda kerakli kutubxona internetdan yuklanadi, shuning uchun 1–3 soniya kutish mumkin. Internet bo'lmasa, PDF ishlamaydi, Excel esa ishlaydi.

> **Maslahat:** Excel faylga jadvalda ko'rinib turgan ustunlar va qatorlar tushadi. Hisobot uchun avval filtr va saralashni sozlang, keyin eksport qiling.

**O'chirish.** Ko'pchilik yozuvlar butunlay o'chmaydi, "o'chirilgan" deb belgilanadi va keyin Admin panelda ko'rinadi. Ma'lumotnomadagi yozuv (ekspeditor, valyuta, mijoz) o'chirilsa, eski hujjatlarda uning nomi saqlanib qoladi, lekin yangi hujjat tanlovida ko'rinmaydi. Ombor va moliya hujjatlarini o'chirish va tiklash qoidalari 1-bobda.

**Xato sahifalari.** "404 — Sahifa topilmadi" noto'g'ri manzilda chiqadi. "500 — Kutilmagan xatolik yuz berdi" kutilmagan xatoda chiqadi. Ikkinchi holatda nima qilganingizni (sahifa, tugma, kiritilgan qiymat) yozib, administratorga yuboring.

## 0.9 Header'dagi belgilar: topshiriqlar va qabul qilinmagan kirimlar

Profil rasmi ustidagi **qizil nuqta** sizni kutayotgan ish borligini bildiradi. Profil rasmini bosganda menyuda sonlar ko'rinadi:

| Band | Kimga ko'rinadi | Son nimani bildiradi | Bosilsa |
|---|---|---|---|
| **Topshiriqlar** | Barcha | Sizga biriktirilgan, hali bajarilmagan topshiriqlar soni | Faqat sizning topshiriqlaringiz ro'yxati ochiladi |
| **Omborga kirim** | Faqat biror omborning MRP (moddiy javobgar shaxs) i | Siz mas'ul bo'lgan omborlarga kelgan, hali qabul qilinmagan kirimlar soni | Kirimlar ro'yxati "qabul qilinmagan" filtri bilan ochiladi |

iPack misoli: Wood Nam Import'dan taxta kelib, xaridchi Xomashyo omboriga kirim yaratdi. Xomashyo omborining MRP'si bo'lgan omborchining profil rasmida qizil nuqta paydo bo'ladi, "Omborga kirim" yonida **1** turadi. Omborchi kirimni ochib qabul qiladi va qoldiq oshadi.

> **Diqqat:** Sonlar har sahifada qayta hisoblanmaydi, ular 60 soniyagacha kechikib yangilanadi. Topshiriqni bajarib yoki kirimni qabul qilib bo'lganingizdan keyin son darhol kamaymasa, bir daqiqadan keyin sahifani yangilang. Bu xato emas.

> **Maslahat:** Omborchi uchun "Omborga kirim" bandi chiqishi uchun administrator Sozlamalar → Omborlar sahifasida omborning "MRP (Mas'ul shaxs)" maydoniga shu xodimni tanlagan bo'lishi kerak.

## 0.10 Bosh sahifa: Rahbariyat nazorat paneli

**Kim:** direktor, o'rinbosarlar, bo'lim boshliqlari   **Menyu:** Bosh sahifa (yoki Dashboard → Umumiy Dashboard)

Panel savdo, ishlab chiqarish, zaxira va vazifalar ijrosini bitta ekranda ko'rsatadi.

1. **Bosh sahifa** ni oching.
2. Yuqori o'ngda ombor filtrini tanlang. Standart qiymat "Barcha omborlar (xomashyo)". Bu filtr faqat zaxira ko'rsatkichlariga (DOH, qamrov) ta'sir qiladi.
3. Davr boshi va oxiri sanalarini tanlang. Standart holatda oxirgi 1 oy olinadi. Davrning oxirgi kuni ham hisobga kiradi.
4. **Yangilash** ni bosing.

KPI kartochkalari va ularning hisoblanishi:

| Kartochka | Formula | iPack ma'nosi |
|---|---|---|
| Oylik savdo konversiyasi | Davrdagi sotuv buyurtmalari summasi ÷ davrdagi sotuv shartnomalari summasi × 100% | Shartnomalarning qanchasi buyurtmaga aylandi |
| Savdo buyurtmasi bajarilishi | Davrdagi invoyslar summasi ÷ davrdagi buyurtmalar summasi × 100% | Buyurtmalarning qanchasi jo'natilib, invoys qilindi |
| Ishlab chiqarish bajarilishi | Davrdagi fakt ishlab chiqarilgan miqdor ÷ davrda boshlangan rejalar miqdori × 100% | Sex rejani necha foiz bajardi |
| Vazifalar bajarilishi | "Bajarildi" holatidagi topshiriqlar ÷ barcha topshiriqlar × 100% (bajarildi / jami soni bilan) | Ijro intizomi |
| DOH (zaxira kunlari) | Xomashyo qoldig'i ÷ oxirgi 30 kundagi o'rtacha kunlik sarf | Xomashyo necha kunga yetadi |
| Qamrov (kun) | Xuddi shu ko'rsatkich (zaxira qamrovi) | |
| Savdo buyurtmalari | Davrdagi sotuv buyurtmalari summasi | |
| Hisob-faktura hajmi | Davrdagi sotuv invoyslari summasi (bekor qilinganlar kirmaydi) | |

Grafiklar:

| Grafik | Nima ko'rsatadi |
|---|---|
| Savdo konversiyasi trendi | Oxirgi 12 oy bo'yicha buyurtma va hisob-faktura summalari (ustunlar). Tanlangan davrdan qat'i nazar, davr oxiri oyidan orqaga 12 oy olinadi |
| Ishlab chiqarish: reja va fakt trendi | Oxirgi 12 oy bo'yicha reja miqdori va amaldagi miqdor (chiziqlar) |
| DOH / qamrov riski (SKU bo'yicha) | Qamrovi eng kam 12 ta xomashyo va ular necha kunga yetishi |
| Buyurtma va ishlab chiqarish bajarilishi KPI | Buyurtma, ishlab chiqarish va vazifa bajarilishi foizlari doiraviy ko'rsatkichda |
| Boshqaruv signal paneli | Avtomatik ogohlantirishlar (pastdagi jadval) |

Signal panelidagi ogohlantirishlar:

| Shart | Ogohlantirish |
|---|---|
| Savdo konversiyasi 65% dan past | "Savdo konversiyasi past. Shartnoma -> buyurtma jarayonini kuchaytirish kerak." |
| Buyurtma bajarilishi 75% dan past | "Buyurtma bajarilishi sekin. Invoicing va logistika bottleneck bor." |
| Ishlab chiqarish bajarilishi 80% dan past | "Ishlab chiqarish reja bajarilishi me'yordan past." |
| DOH 20 kundan kam | "DOH juda past. Stockout riski bor." |
| DOH 90 kundan ko'p | "DOH yuqori. Zaxira aylanishi sust." |
| Muddati o'tgan topshiriq bor | "Muddati o'tgan vazifalar mavjud, ijro nazoratini kuchaytiring." |
| Hammasi me'yorda | "Asosiy KPIlar nazorat ostida. Hozirgi trend barqaror." |

**Ssenariy misoli.** Davr 01.11.2026 – 30.11.2026. UzAutoMotors Powertrain bilan shartnoma va buyurtma noyabrda kiritilgan, uchala partiya invoys qilingan bo'lsa, "Savdo buyurtmalari" va "Hisob-faktura hajmi" kartochkalarida buyurtma summalari ko'rinadi (boshqa mijozlarning noyabr hujjatlari ham qo'shiladi). Faqat 1-partiya invoys qilingan bo'lsa, "Savdo buyurtmasi bajarilishi" taxminan uchdan biriga teng bo'ladi va 75% dan past bo'lgani uchun signal panelida "Buyurtma bajarilishi sekin" ogohlantirishi chiqadi.

> **Diqqat:** Summalar hujjatlarda qanday kiritilgan bo'lsa, shunday qo'shiladi. Invoys summasi QQS bilan, buyurtma summasi esa buyurtmada qanday yozilgan bo'lsa (odatda QQSsiz) shunday olinadi. Shuning uchun bajarilish foizi 100% dan oshib ketishi mumkin. Aniq taqqoslash uchun CRM hisobotlaridan foydalaning.

> **Maslahat:** Har kuni ertalab davrni "oy boshidan bugungacha" qo'yib Yangilash bosing. Signal panelini ko'rib, tegishli bo'lim boshlig'iga topshiriq bering (Tasks / Vazifalar).

Batafsil bo'lim dashboardlari (Moliya, Tannarx, CRM, Logistika, HR, MES, SRM, Ombor, Vazifalar) **Dashboard** menyusida joylashgan va har bir modul bobida yoritilgan.

## 0.11 Tez-tez uchraydigan savollar va muammolar

**Menyuda kerakli bo'lim yo'q.**
Rolingizda bu bo'limga ko'rish huquqi yo'q. Administratorga qaysi sahifa kerakligini ayting.

**"Sizga ruxsat berilmagan!" sahifasi chiqdi.**
Sahifani ko'rishga yoki undagi amalni (yaratish, tahrirlash, o'chirish) bajarishga huquqingiz yo'q. Masalan, faqat ko'rish huquqi bilan ro'yxat ochiladi, lekin "Yangi" formasi ochilmaydi. Administrator huquq bergach qayta kirish shart emas, sahifani yangilash yetarli.

**Parolni unutdim yoki tizim 15 daqiqaga blokladi.**
15 daqiqa kuting yoki administratorga murojaat qiling. U Admin panel → Foydalanuvchilar → Tahrirlash orqali yangi parol o'rnatadi.

**Ish vaqtida tizim meni login sahifasiga chiqarib yubordi.**
60 daqiqa harakatsizlikdan keyin sessiya tugaydi. Server qayta ishga tushirilganda ham barcha foydalanuvchilar tizimdan chiqib ketishi mumkin. Qayta kiring. Ochiq forma saqlanmagan bo'lsa, ma'lumotni qayta kiritasiz.

**Qizil nuqta yo'qolmayapti.**
Sonlar 60 soniyagacha kechikadi. Bir daqiqadan keyin sahifani yangilang. Shundan keyin ham qolsa, Topshiriqlar ro'yxatida bajarilmagan topshiriq yoki qabul qilinmagan kirim borligini tekshiring.

**Bosh sahifadagi ko'rsatkichlar 0.**
Tanlangan davrda hujjat yo'q yoki sanalar noto'g'ri tanlangan. Davrni kengaytirib **Yangilash** ni bosing. DOH 0 bo'lsa, oxirgi 30 kunda xomashyo chiqimi bo'lmagan.

**PDF yuklanmayapti.**
Internet aloqasini tekshiring. PDF kutubxonasi birinchi bosishda internetdan olinadi. Vaqtincha Excel'dan foydalaning.

**Header'da lavozimim o'rniga rol nomi chiqyapti.**
Profildagi F.I.Sh HR'dagi xodim kartochkasidagi F.I.Sh bilan harfma-harf bir xil emas. Ikkalasini bir xil qiling.
