# 4. Ombor

Ombor moduli iPack'dagi har bir taxta, karton varag'i va tayyor paddonning qayerda, qancha va qaysi tannarxda turganini ko'rsatadi. Asosiy foydalanuvchi — **Omborchi** (har ombor uchun biriktirilgan moddiy javobgar shaxs, MRP); qo'shimcha: Xaridchi (kirim holatini kuzatadi), Ishlab chiqarish boshlig'i (sexga material oladi, tayyor mahsulotni topshiradi), Sotuvchi (Savdo omboridagi qoldiqni ko'radi), buxgalter (oylik hisobot, tannarx). Modul **Ta'minot** (invoysdan kirim), **Ishlab chiqarish/MES** (norma bo'yicha sarf, tayyor mahsulot kirimi, sifat nazorati), **CRM** (sotuv invoysi Savdo omboridan avtomatik chiqim qiladi) va **Moliya/Tannarx** (FIFO tannarx) bilan bog'langan.

## 4.1 Asosiy tushunchalar

- **Ombor turi** — omborning roli: "Xomashyo (ta'minot)", "Ishlab chiqarish", "Savdo" yoki oddiy ombor. Tizim oqimlarni ombor nomidan emas, shu maydondan aniqlaydi.
- **MRP (Mas'ul shaxs)** — omborga biriktirilgan foydalanuvchi (omborchi). Kirim yaratish, qabul qilish, chiqim va inventarizatsiyani faqat o'z omborida bajaradi. Admin hammasini qila oladi.
- **Kirim** — omborga mahsulot kelishi hujjati. Holati: **Kutilmoqda** yoki **Qabul qilingan**. Qoldiq faqat **Qabul qilish** bosilganda oshadi.
- **Chiqim** — ombordan mahsulot ketishi hujjati. Qabul qiluvchi ombor ko'rsatilsa — bu **o'tkazma** (ichki ko'chirish); ko'rsatilmasa — hisobdan chiqarish.
- **Lot (partiya)** — bir kirim bilan kelgan, bir xil narxdagi miqdor. Masalan 06.11 da 54 000 so'mdan kelgan 1 600 dona taxta — bitta lot.
- **Sof FIFO** — "birinchi kelgan — birinchi ketadi". Chiqimda lot tanlanmaydi: tizim avtomatik eng eski lotdan sarflaydi.
- **FIFO tannarx** — chiqim narxi qo'lda yozilmaydi; sarflangan lotlar narxidan **QQSsiz** hisoblanadi.
- **DLOC** — ombor ichidagi aniq joy (stellaj, qator, katak), masalan `XA-01-A`. DLOC yoqilgan omborda lotlar joy bo'yicha kuzatiladi.
- **Inventarizatsiya** — komissiya bilan haqiqiy sanoq va tizim qoldig'ini tenglash. Har ombor uchun oyiga bir marta.
- **DOH (Days on Hand)** — joriy qoldiq necha kunlik sarfga yetishi.

### iPack omborlar oqimi

```
 Ta'minotchi (Wood Nam Import, IMPORT EVRO GRAND)
        | xarid invoysi -> kirim -> Qabul qilish
        v
 [Xomashyo ombori]  (turi: Xomashyo)  taxta, karton, selofan, metiz
        | chiqim-o'tkazma -> sex omborchisi Qabul qiladi
        v
 [Ishlab chiqarish ombori] (turi: Ishlab chiqarish)
        | MES: norma bo'yicha xomashyo avtomatik chiqim, tayyor mahsulot avtomatik kirim
        | sifat nazorati (QC) -> tasdiqlangan miqdor
        | "Ishlab chiqarishdan sotuvga" o'tkazma -> savdo omborchisi Qabul qiladi
        v
 [Savdo ombori] (turi: Savdo)  iPack P1, B6.1 ...
        | sotuv invoysi -> avtomatik chiqim
        v
 Mijoz (UzAutoMotors Powertrain)
```

## 4.2 Omborlarni sozlash

**Kim:** Admin   **Menyu:** Omborxona → Ombor → Omborlar

Ro'yxatda Ombor nomi, Tavsif, Sex, MRP (Mas'ul shaxs), Turi va DLOC ustunlari bor (DLOC ustunidan omborning joylar ro'yxati va "DLOC qo'shish" ochiladi). Excel/PDF eksport mavjud.

1. **Yangi ombor qo'shish** ni bosing.
2. Ombor nomi = `Xomashyo ombori`, Sex = `Ishlab chiqarish`, MRP = xomashyo omborchisi, Ombor turi = `Xomashyo (ta'minot)`, Tavsif. Saqlang → "Ombor muvaffaqiyatli qo'shildi".
3. `Ishlab chiqarish ombori` — Ombor turi = `Ishlab chiqarish`, MRP = sex omborchisi.
4. `Savdo ombori` — Ombor turi = `Savdo`, MRP = tayyor mahsulot omborchisi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ombor nomi | Nom (majburiy) | Xomashyo ombori |
| Sex | Qaysi sexga tegishli | Ishlab chiqarish |
| MRP (Mas'ul shaxs) | Omborchi foydalanuvchisi | xomashyo omborchisi |
| Ombor turi | Xomashyo (ta'minot) / Ishlab chiqarish / Savdo / Oddiy | Xomashyo (ta'minot) |
| Tavsif | Izoh | "Barcha kelgan xomashyo avval shu yerga" |

> **Diqqat:** Savdo ombori faqat bitta bo'ladi. Ikkinchisini belgilasangiz "Savdo ombori allaqachon belgilangan — faqat bitta savdo ombori bo'lishi mumkin." xabari chiqadi.
> **Diqqat:** Ishlab chiqarish va Savdo omborlari "Ombor turi" bilan belgilanmagan bo'lsa, ishlab chiqarish kirimi va o'tkazmalar "...ombori topilmadi" xatosini beradi. Nomga "Ishlab chiqarish" deb yozish yetarli emas.
> **Diqqat:** Ombor o'chirilmaydi, agar unda qoldiq ("Omborda qoldiq bor (...). Avval qoldiqni boshqa omborga o'tkazing yoki inventarizatsiya qiling."), DLOC lot qoldig'i, qabul qilinmagan kirim yoki yakunlanmagan o'tkazma bo'lsa.

**Natija:** Omborlar kirim/chiqim formalarida chiqadi; har omborchi menyuda faqat o'z omborida amal bajaradi.

## 4.3 Ombor joylari (DLOC)

**Kim:** Admin, omborchi   **Menyu:** Omborxona → Ombor → DLOC (joylashuvlar)

DLOC — ombor ichidagi aniq joy. Omborda kamida bitta DLOC bo'lsa, u "DLOC ombori" bo'ladi: har kirim qatoriga joy tanlanadi va lotlar joy bo'yicha kuzatiladi.

1. **Yangi joylashuv (DLOC)** ni bosing.
2. Ombor = `Xomashyo ombori`, Joy kodi = `XA-01-A`, Nomi = `Taxta stellaji 1`, Min sig'im = `200`, Max sig'im = `2500`.
3. "Mahsulot aralashtirishga ruxsat" — o'chiq (bu stellajda faqat bir xil taxta), "Lot aralashtirishga ruxsat" — yoqiq (har xil partiyalar bir joyda turishi mumkin), "Faol" — yoqiq. Saqlang.
4. Karton uchun `XA-02-A` (Max 5 000 m²) yarating.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ombor | Qaysi omborda | Xomashyo ombori |
| Joy kodi (DLOC) | Qisqa kod | XA-01-A |
| Nomi | Tushunarli nom | Taxta stellaji 1 |
| Min / Max sig'im | Joyga sig'adigan miqdor chegarasi | 200 / 2 500 |
| Mahsulot aralashtirishga ruxsat | Bir joyda turli mahsulot bo'lishi mumkinmi | Yo'q |
| Lot aralashtirishga ruxsat | Bir joyda turli partiyalar bo'lishi mumkinmi | Ha |
| Faol | Joy ishlatiladimi | Ha |

> **Diqqat:** Kirimda joyning Max sig'imi oshib ketsa, "DLOC XA-01-A: sig'im oshib ketadi (Max ..., jami kirimdan keyin ...)" xabari chiqadi. Boshqa joyni tanlang yoki kirimni ikki qatorga bo'ling.
> **Maslahat:** DLOC yoqilishidan oldin omborda turgan qoldiq "joylashtirilmagan" bo'ladi. Uni **Qoldiqlar → DLOC ga joylashtirish** sahifasida joylarga taqsimlang (ombor tanlanadi, "Joylashtirilmagan qoldiqlar" ro'yxatidan mahsulot, miqdor va DLOC belgilanadi, **Tanlanganlarni DLOC ga joylashtirish**). Joylashtirilmagan qoldiq bo'lsa chiqim va inventarizatsiya to'xtatiladi.

**Natija:** **Qoldiqlar → Joylar bo'yicha qoldiq** sahifasida har mahsulot uchun DLOC, Qoldiq, Lotlar soni, Eng erta kirim, Min/Max va Sig'im % ko'rinadi.

## 4.4 Kirim: xarid invoysidan

**Kim:** Xomashyo ombori omborchisi (MRP)   **Menyu:** Omborxona → Ombor → Operatsiyalar → Kirimlar

Ro'yxat ustunlari: Hujjat raqami, Invoys raqami, Sana, Ombor, Jami summa, Status. Filtrlar: Status (Barchasi / Kutilmoqda / Qabul qilingan), Ombor, Sanadan / Sanagacha (standart — oxirgi 30 kun). Eng yangi hujjat tepada; qidiruv hujjat raqami, invoys raqami va ombor nomi bo'yicha ishlaydi; **Excel** filtrga mos barcha yozuvlarni beradi.

### Kirim holatlari

| Holat | Ma'nosi | Qoldiqqa ta'siri | Mumkin amallar |
|---|---|---|---|
| Kutilmoqda | Hujjat yaratilgan, yuk sanalmagan | Yo'q | Tahrirlash, O'chirish, Qabul qilish; o'tkazma kirimida — Rad qilish |
| Qabul qilingan | Omborchi yukni sanab qabul qildi | Qoldiq va lot qo'shildi | Faqat ko'rish |

### Qadamlar (Wood Nam Import, 1-partiya)

1. **Yangi kirim** ni bosing.
2. Hujjat raqami = bo'sh ("Bo'sh qoldirilsa, avtomatik generatsiya qilinadi", ko'rinishi `202611_N`), Sana va vaqt = `07.11.2026 09:30`.
3. Invoys = `PI-2026-0031` va **Invoys mahsulotlarini yuklash** ni bosing — qatorlar narxi bilan tushadi: `Yog'och 100*50*6000`, 1 600 dona × 54 000, QQS % 12.
4. Qabul qiluvchi ombor = `Xomashyo ombori`, Valyuta = `UZS`. DLOC ombori bo'lsa, har qatorga joy tanlang (`XA-01-A`); "Joy sig'imi" ustuni bo'sh joyni ko'rsatadi.
5. "Kirim hujjati (PDF)" ga yuk xati yoki dalolatnomani biriktiring (ixtiyoriy). Saqlang.
6. Yuk haqiqatda sanalgandan keyin kirimning **Details** sahifasini oching va **Qabul qilish** ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Hujjat raqami | Bo'sh — avtomatik | 202611_14 |
| Sana va vaqt | Yuk kelgan vaqt | 07.11.2026 09:30 |
| Invoys | Xarid invoysi | PI-2026-0031 |
| Qabul qiluvchi ombor | Qaysi omborga | Xomashyo ombori |
| Valyuta | Hujjat valyutasi | UZS |
| Mahsulot, Miqdor, Birlik | Kelgan miqdor | Yog'och 100\*50\*6000, 2 000 dona |
| Dona narxi | Birlik narxi (QQSsiz) | 54 000 |
| QQS % | Stavka alohida saqlanadi | 12 |
| DLOC | Joy (DLOC omborda majburiy) | XA-01-A |
| Izoh | Erkin izoh | "1-partiya" |

Kirimning "Jami summa"si QQS bilan: 1 600 × 54 000 × 1,12 = **96 768 000 so'm**.

> **Diqqat:** Qabul qilinmagan kirim (yaratish ham, tahrir ham) qoldiqqa ta'sir qilmaydi. Qoldiq faqat **Qabul qilish** da bir marta qo'shiladi. Qayta bosilsa "Bu kirim hujjati allaqachon qabul qilingan." xabari chiqadi, qoldiq ikki baravar bo'lmaydi.
> **Diqqat:** Qabul qilingan kirimni tahrirlab yoki rad etib bo'lmaydi.
> **Diqqat:** Kirimni faqat shu omborning MRP'i yoki Admin o'chiradi. Kutilayotgan kirim o'chirilsa qoldiq o'zgarmaydi.
> **Diqqat:** Qo'lda yangi kirim faqat Xomashyo turidagi omborga yaratiladi. Ishlab chiqarish va Savdo omborlariga "qo'lda kirim yaratish mumkin emas" — ular faqat chiqimdan (o'tkazmadan) avtomatik yaratilgan kirimni qabul qiladi.
> **Maslahat:** Yuk kam kelgan bo'lsa (masalan 1 980 dona), qabul qilishdan oldin kirimni tahrirlab miqdorni haqiqiy songa tushiring, so'ng ta'minotchi bilan farqni hal qiling.

**Natija:** Qabul qilishdan keyin "Kirim «202611_14» qabul qilindi. Ombor qoldig'i yangilandi." xabari chiqadi. Xomashyo omborida +1 600 dona taxta, yangi lot (54 000 so'm, 07.11.2026). Buyurtma kuzatuvida buyurtma "Received" bo'ladi. 2-partiya (1 000 dona) va IMPORT EVRO GRAND'dan karton (2 000 m² × 6 500) xuddi shunday kirim qilinadi.

Kirim **Details** sahifasida: Hujjat raqami, Invoys, Manbasi (o'tkazmadan avtomatik yaratilgan bo'lsa shunday yoziladi), Qabul qiluvchi va Yuboruvchi ombor, Status, Jami miqdor, Jami summa, qatorlar (Lot, DLOC bilan) va "Kirim hujjatlari" (yuklangan fayllar).

## 4.5 Chiqim va o'tkazma (sof FIFO)

**Kim:** yuboruvchi ombor omborchisi (MRP)   **Menyu:** Omborxona → Ombor → Operatsiyalar → Chiqimlar

Ro'yxat ustunlari: Hujjat raqami, Sana, Yuboruvchi ombor, Qabul qiluvchi ombor, Jami summa, Status (**Kutilmoqda** / **Yuborilgan**). Filtrlar va Excel kirimlardagi kabi.

### Chiqim holatlari

| Holat | Ma'nosi |
|---|---|
| Kutilmoqda | Chiqim yozilgan (yuboruvchidan qoldiq darhol kamaygan); o'tkazma bo'lsa, qabul qiluvchi hali qabul qilmagan |
| Yuborilgan | Bog'langan kirim qabul qilingan — o'tkazma yakunlangan |

### Qadamlar: taxtani sexga o'tkazish

Xomashyo omborida oldindan qolgan 300 dona taxta (eski lot, 52 000 so'm) va 07.11 da kelgan 1 600 dona (54 000 so'm) bor. Sex P1 paddon uchun 1 000 dona so'raydi.

1. **Yangi chiqim** ni bosing.
2. Hujjat raqami = bo'sh (avtomatik `202611_N`), Sana va vaqt = `10.11.2026 08:00`.
3. Yuboruvchi ombor = `Xomashyo ombori`, Qabul qiluvchi ombor = `Ishlab chiqarish ombori`.
4. **Mavjud mahsulotlarni yuklash** yoki **Qo'shish** bilan qator: `Yog'och 100*50*6000`, Chiqim miqdori `1000`. "Mavjud qoldiq" ustuni 1 900 ni ko'rsatadi; "Tannarx (FIFO)" faqat o'qish uchun.
5. "Chiqim hujjati (PDF)" — talabnoma yoki yuk xati (ixtiyoriy). Saqlang.

FIFO hisobi: avval eski lotdan 300 × 52 000 = 15 600 000, keyin yangi lotdan 700 × 54 000 = 37 800 000. **Chiqim summasi 53 400 000 so'm** (dona narxi 53 400, QQSsiz).

6. Ishlab chiqarish ombori omborchisi tizimga kiradi: yuqoridagi qo'ng'iroqcha belgisida kutilayotgan kirimlar soni ko'rinadi. **Kirimlar** da `INC-202611_N` raqamli bog'langan kirimni ochadi, taxtani sanaydi va **Qabul qilish** ni bosadi (yoki mos kelmasa **Rad qilish**).

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Hujjat raqami | Bo'sh — avtomatik | 202611_22 |
| Sana va vaqt | Chiqim vaqti | 10.11.2026 08:00 |
| Yuboruvchi ombor | Faqat o'zingizga biriktirilgan | Xomashyo ombori |
| Qabul qiluvchi ombor | O'tkazmada — qayerga; hisobdan chiqarishda bo'sh | Ishlab chiqarish ombori |
| Mahsulot, Birlik | Nima | Yog'och 100\*50\*6000, dona |
| Mavjud qoldiq | Avtomatik, faqat ko'rish | 1 900 |
| Chiqim miqdori | Qancha | 1 000 |
| Tannarx (FIFO) | Avtomatik, QQSsiz | 53 400 |
| Izoh | Sabab | "P1 paddon, noyabr buyurtmasi" |

> **Diqqat:** Lot tanlanmaydi va narx qo'lda yozilmaydi — chiqim har doim sof FIFO (biznes qarori).
> **Diqqat:** Manfiy qoldiqqa yo'l qo'yilmaydi. Ombordagidan ko'p yozilsa "Mahsulot ... uchun yetarli qoldiq yo'q. Mavjud: 2300, Talab: 2500" xabari chiqadi va hujjat yaratilmaydi. Bir mahsulot bir necha qatorga bo'lib yozilsa ham jami tekshiriladi. Xato bilan qaytgan formada kiritilganlar saqlanib qoladi.
> **Diqqat:** O'tkazmada yuboruvchidan qoldiq **darhol** kamayadi, qabul qiluvchida esa faqat **Qabul qilish** dan keyin paydo bo'ladi. Oraliqda mahsulot "yo'lda" hisoblanadi.
> **Diqqat:** Bog'langan kirim qabul qilingandan keyin chiqimni tahrirlab ham, o'chirib ham bo'lmaydi ("Bog'langan kirim «INC-...» allaqachon qabul qilingan — chiqimni o'chirib bo'lmaydi.").
> **Diqqat:** Qator birligi qoldiq birligidan farq qilsa, faqat dona↔kg (mahsulot og'irligi bo'yicha) va metr↔dona (kesiladigan mahsulot uzunligi bo'yicha) o'giriladi; boshqa juftliklarda saqlash rad etiladi.

### O'tkazmani rad etish va o'chirish

- **Rad qilish** (qabul qiluvchi): yuboruvchiga mahsulot qaytadi, qabul qiluvchi qoldig'iga tegilmaydi; rad etilgan kirim ham, unga bog'langan chiqim ham ro'yxatlardan yo'qoladi.
- **O'chirish** (yuboruvchi, qabul qilinmaguncha): qoldiq va lotlar aynan qaytadi, kutilayotgan bog'langan kirim ham o'chadi.

**Natija:** Xomashyo omborida taxta 1 900 → 900 dona (eski 52 000 lot to'liq tugadi, 54 000 lotdan 900 qoldi). Qabul qilingandan keyin Ishlab chiqarish omborida +1 000 dona, narxi 53 400 000. Chiqim holati "Yuborilgan".

> **Maslahat:** Chiqimlar ro'yxatidagi **Tuzatish** va **Status tuzatish** tugmalari (o'zgartirish huquqi borlarga) eski ma'lumotlardagi nomuvofiqliklarni tiklash uchun: kutilayotgan chiqimlarga yetishmayotgan bog'langan kirimni yaratadi va qabul qilingan o'tkazmalar chiqimi statusini "Yuborilgan" ga moslaydi. Kundalik ishda kerak emas.

## 4.6 Ishlab chiqarish bilan bog'liq avtomatik harakatlar

Bu harakatlarni omborchi qo'lda yozmaydi — ular MES (Ishlab chiqarish) bobidagi amallardan avtomatik paydo bo'ladi, lekin omborchi ularni bilishi shart:

| Amal (MES'da) | Ombordagi natija |
|---|---|
| Kunlik ishlab chiqarish fakti kiritildi (masalan 400 dona P1) | Ishlab chiqarish omboridan norma bo'yicha xomashyo chiqimi (400 × 2 = 800 dona taxta, misol), FIFO tannarxda, QQSsiz |
| Shu fakt | Ishlab chiqarish omboriga 400 dona iPack P1 kirimi (ishlab chiqarishga bog'langan) |
| Xomashyo yetmasa | Fakt saqlanmaydi: "...xomashyo yetarli emas. Mahsulot: ..., omborda ..., kerak ..." |
| Fakt tahrirlandi yoki o'chirildi | Eski avtomatik kirim/chiqim bekor qilinib, qayta yoziladi |

> **Diqqat:** Ishlab chiqarishdan oldin xomashyo **Ishlab chiqarish omborida** turishi kerak. Xomashyo omborida turgan bo'lsa, avval 4.5 dagi o'tkazmani qiling.

## 4.7 Ishlab chiqarishdan sotuvga o'tkazma

**Kim:** Ishlab chiqarish ombori omborchisi; qabul — Savdo ombori omborchisi   **Menyu:** Omborxona → Ombor → Operatsiyalar → Ishlab chiqarishdan sotuvga

Tayyor mahsulot sifat nazoratidan o'tgandan keyin Savdo omboriga shu sahifa orqali o'tkaziladi. Ro'yxat ("O'tkazmalar ro'yxati") ustunlari: Hujjat raqami, Sana, Mahsulot kodi, Mahsulot, Jami miqdor, Ishlab chiqarish ombori, Sotuv ombori, Holati; filtr: Sana (dan/gacha), mahsulot.

1. Yangi o'tkazma formasini oching ("Ishlab chiqarishdan sotuv omboriga o'tkazma").
2. O'tkazma sanasi = bugun (`14.11.2026`), Hujjat raqami = bo'sh (avtomatik `TRNSF_ddMMyy_N`).
3. **Mahsulot qo'shish**: Mahsulot = `iPack P1 Yog'och taglik`, "Lot (ishlab chiqarish)" dan sifat nazoratidan o'tgan lotni tanlang; "Tanlangan lotlar qoldig'i" ko'rinadi. O'tkazish miqdori = `400`.
4. Kerak bo'lsa B6.1 quti uchun ham qator qo'shing (bitta hujjatda bir necha mahsulot).
5. Hujjat fayli (PDF, Word yoki rasm) — topshirish dalolatnomasi (ixtiyoriy). **Saqlash**.
6. Savdo ombori omborchisi **Kirimlar** da `TRNSF_141126_N-IN` kirimini ochib **Qabul qilish** ni bosadi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| O'tkazma sanasi | Bugungi yoki o'tgan sana | 14.11.2026 |
| Hujjat raqami | Bo'sh — avtomatik | TRNSF_141126_1 |
| Mahsulot | Tayyor mahsulot | iPack P1 Yog'och taglik |
| Lot (ishlab chiqarish) | QC'dan o'tgan lot | 13.11 smenasi loti |
| O'tkazish miqdori | Qancha | 400 |
| Izoh, Hujjat fayli | Ixtiyoriy | dalolatnoma |

> **Diqqat:** O'tkaziladigan miqdor sifat nazoratida tasdiqlangan (qabul qilingan) miqdordan oshmaydi. Ko'p yozilsa "...yetarli qoldiq yo'q. Mavjud: 400, Talab qilinmoqda: 450" kabi xato chiqadi va hech narsa yozilmaydi.
> **Diqqat:** Kelajak sanani qo'yib bo'lmaydi: "O'tkazma sanasi bugungi kunidan oshmasligi kerak."
> **Diqqat:** O'tkazma narxi — ishlab chiqarish kirimining FIFO tannarxi, sotuv narxi (185 000) emas.

**Natija:** Bitta chiqim `TRNSF_...` va bitta kirim `TRNSF_...-IN` yaratiladi. Ishlab chiqarish omboridan 400 dona darhol kamayadi; Savdo omboriga faqat qabul qilingandan keyin +400 dona qo'shiladi. Shundan keyin CRM'da sotuv invoysi (1-partiya, 400 dona) Savdo omboridan avtomatik chiqim qiladi.

## 4.8 Chiqindi, brak va hisobdan chiqarish

Tizimda alohida "chiqindi" hujjati yo'q. iPack'da quyidagicha yuritiladi:

- **Kesish chiqindisi** (taxta qoldiq bo'laklari) — norma ichida hisobga olingan: Muhandislik bo'limidagi normada "Chiqindi" ustuni bor. Ishlab chiqarish fakti kiritilganda xomashyo norma bo'yicha (chiqindisi bilan birga) ombordan avtomatik chiqim qilinadi. Omborchi uni alohida yozmaydi.
- **Yaroqsiz xomashyo** (masalan nam bo'lib buzilgan 50 m² karton) — **Chiqimlar → Yangi chiqim**: Yuboruvchi ombor = `Xomashyo ombori`, Qabul qiluvchi ombor = **bo'sh**, miqdor `50`, Izoh = `Hisobdan chiqarish: namlik, dalolatnoma №...`. Dalolatnoma skanini PDF sifatida biriktiring. Tannarx FIFO bo'yicha avtomatik.
- **Tayyor mahsulot braki** — MES'dagi sifat nazoratida rad etilgan miqdor Savdo omboriga o'tkazilmaydi (4.7 dagi cheklov).
- **Sanoqda aniqlangan kamomad** — inventarizatsiya (4.9) orqali yoziladi.

> **Maslahat:** Hisobdan chiqarishlarni izoh boshida bir xil so'z bilan yozing (masalan `Hisobdan chiqarish:`) — keyin Chiqimlar ro'yxatida qidiruv orqali oylik yo'qotishlarni tez yig'asiz.

## 4.9 Inventarizatsiya

**Kim:** ombor MRP'i (o'z ombori bo'yicha), Admin   **Menyu:** Omborxona → Ombor → Operatsiyalar → Qoldiqlar → **Inventarizatsiya**

Inventarizatsiya Excel fayl orqali kiritiladi: komissiya omborni sanaydi, natija shablonga yoziladi va tizimga yuklanadi. Tizim sanoqni joriy qoldiq bilan solishtirib, farqni yozadi.

1. **Excel shablonini yuklab oling**: 1-ustun `ProductPNo` (mahsulot kodi), 2-ustun `Quantity` (sanalgan miqdor).
2. Komissiya (omborchi, sex boshlig'i, buxgalter) omborni sanaydi. Natijani faylga yozing, masalan Xomashyo ombori: `Yog'och 100*50*6000` kodi | `1295`; `Karton_2090x1370` kodi | `3790`.
3. Inventarizatsiya sahifasida Ombor = `Xomashyo ombori`, "Excel fayl (.xlsx)" ni tanlang.
4. Ogohlantirishni o'qing ("Inventarizatsiya faqat komissiya bilan sanoq o'tkazilgandan so'ng amalga oshiriladi...") va **Qoidalarni qabul qilaman** belgisini qo'ying.
5. **Inventarizatsiyani tasdiqlash** ni bosing.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Ombor | Faqat o'zingizga biriktirilgan | Xomashyo ombori |
| Excel fayl (.xlsx) | ProductPNo, Quantity | noyabr_sanoq.xlsx |
| Qoidalarni qabul qilaman | Majburiy belgi | belgilandi |

Natija misoli (tizimda taxta 900, karton 1 200 bo'lsa): taxta −5 dona va karton −10 m² kamomad bitta `INV_...OUT...` chiqimida FIFO tannarxda yoziladi. Ortiqcha topilsa — bitta `INV_...IN...` kirimida oxirgi kirim narxida (QQSsiz). Izohda "Inventarizatsiya" so'zi turadi.

> **Diqqat:** Har ombor uchun oyiga **bir marta**. Ikkinchi urinishda "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi." xabari chiqadi (qulf foydalanuvchi emas, ombor bo'yicha). Xato kiritilsa, tuzatish keyingi oyda yoki administrator orqali.
> **Diqqat:** Faylda faqat sanalgan mahsulotlar solishtiriladi; faylda yo'q mahsulot qoldig'i o'zgarmaydi. Haqiqatda tugagan mahsulotni `0` bilan yozing.
> **Diqqat:** Fayl formati xato bo'lsa "Excel format xato. Majburiy ustunlar: ProductPNo va Quantity." Qator xatolari ro'yxat bilan ko'rsatiladi — tuzatib qayta yuklang.
> **Diqqat:** DLOC omborda joylashtirilmagan qoldiq bo'lsa inventarizatsiya qabul qilinmaydi — avval DLOC ga joylashtiring.

**Natija:** Qoldiq sanoqqa tenglashadi; oylik hisobotda farq "Inventarizatsiya orqali" ustunida alohida chiqadi.

## 4.10 Qoldiqlar sahifasi

**Kim:** omborchi, xaridchi, sotuvchi (ko'rish), rahbar   **Menyu:** Omborxona → Ombor → Operatsiyalar → Qoldiqlar

Ustunlar: Mahsulot kodi, Mahsulot nomi, Kategoriya, Ombor, Miqdor, Birlik. Filtrlar: Ombor, Kategoriya, Mahsulot turi (Barchasi / Tayyor mahsulot / Xom ashyo), "Faqat xom ashyo". Excel/PDF eksport.

Sahifadan tezkor o'tishlar: **Kirimga o'tish**, **Chiqimga o'tish**, **Inventarizatsiya**, **Joylar bo'yicha qoldiq** (DLOC), **DLOC ga joylashtirish**.

**Joylar bo'yicha qoldiq** (DLOC omborlar uchun): Umumiy qoldiq, har DLOC bo'yicha Qoldiq, **Lotlar soni**, **Eng erta kirim** sanasi, Min/Max, Sig'im %. FIFO ishlayotganini shu yerda ko'rasiz: chiqimdan keyin eng eski lot birinchi tugaydi va "Eng erta kirim" sanasi oldinga suriladi.

> **Maslahat:** Sotuvchi UzAutoMotors'ga yetkazish sanasini va'da qilishdan oldin Savdo ombori + Mahsulot turi = "Tayyor mahsulot" filtri bilan iPack P1 qoldig'ini tekshiradi.
> **Diqqat:** Oddiy (DLOC siz) omborda FIFO partiyalari alohida ko'rinmaydi, lekin chiqim baribir FIFO tannarxda hisoblanadi.

## 4.11 Hisobotlar va dashboardlar

### Ombor (DOH) Dashboard

**Kim:** omborchi, xaridchi, rahbar   **Menyu:** Omborxona → Ombor → DOH Dashboard (yoki Dashboard → Ombor Dashboard)

Filtrlar: Ombor, Kategoriya, Mahsulot turi, sana Dan/Gacha (standart oxirgi 30 kun), **Runner oynasi (kun)** = 30 / 60 / 90, **Top-N** (eng ko'pi 50), FIFO scope.

| Blok | Nimani ko'rsatadi |
|---|---|
| Planned DOH / Actual DOH | Qoldiq ÷ rejadagi (ishlab chiqarish rejasi bo'yicha) va haqiqiy (chiqimlar bo'yicha) o'rtacha kunlik sarf |
| Yuqori DOH / Past DOH | Planned DOH 60 kundan ko'p (ortiqcha zahira) va 30 kundan kam (tez tugaydi) mahsulotlar; har kartada eng muhim 50 tasi |
| High runner / Low runner (N kun) | Tanlangan oynada eng tez va eng sekin sarflanayotgan mahsulotlar |
| Kategoriya bo'yicha zaxira taqsimoti | Yog'och, Karton, Metiz... ulushi |
| Kirim/Chiqim tendentsiyasi | Tanlangan ombor bo'yicha kunlik harakat grafigi |
| FIFO jurnal / FIFO taxminiy ogohlantirishlar | DLOC lot chiqimlari va yangi partiya eskisidan oldin ishlatilgandek ko'ringan holatlar |
| Barcha mahsulotlar DOH jadvali | To'liq ro'yxat |

iPack misoli: noyabrda taxta kuniga ~120 dona sarflanmoqda, qoldiq 900 → Actual DOH ≈ 7,5 kun. Bu "Past DOH" ga tushadi — xaridchi Wood Nam Import'ga ikkinchi partiyani tezlashtirish signali.

### Oylik ombor hisoboti

**Kim:** omborchi (MRP topshirig'i), buxgalter   **Menyu:** Omborxona → Ombor → Oylik hisobot

1. Ko'rinishi = `Miqdor bo'yicha`, Ombor = `Xomashyo ombori`, Oy = `Noyabr`, Yil = `2026`. **Hisobotni ko'rsatish**.
2. Ko'rinishni `Summa bo'yicha` ga almashtiring.
3. **Chop etish** — imzo uchun bosma nusxa ("OMBOR OYLIK HISOBOTI").

Ustunlar: №, Kod, Mahsulot, Birlik, **Oldingi qoldiq**, **Kirim**, **Chiqim**, **Inventarizatsiya orqali**, **Oy oxiridagi qoldiq**.

- Faqat qabul qilingan va o'chirilmagan hujjatlar kiradi; rad etilgan va o'chirilganlar hisobga olinmaydi.
- "Summa bo'yicha" rejimida kirim **QQSsiz**, chiqim FIFO tannarxida. Misol: taxta kirimi 2 600 × 54 000 = 140 400 000 (QQS bilan 157 248 000 emas).

> **Maslahat:** Oy yopilishida buxgalter oylik hisobot summalarini Moliyadagi xarid va tannarx raqamlari bilan solishtiradi.

## 4.12 Omborchi uchun ish tartibi

### Har kuni

1. Tizimga kirgach, qo'ng'iroqcha belgisida kutilayotgan kirimlar sonini ko'ring.
2. **Kirimlar** → Status = `Kutilmoqda`: kelgan yuklarni sanang va **Qabul qilish** ni bosing. Mos kelmasa — qabul qilishdan oldin miqdorni tuzating (xarid kirimi) yoki **Rad qilish** (o'tkazma).
3. Ta'minotchidan yuk kelsa: xarid invoysi bo'yicha **Yangi kirim** → **Invoys mahsulotlarini yuklash** → DLOC → saqlash → sanoqdan keyin qabul.
4. Sex talabnomalari bo'yicha Xomashyo → Ishlab chiqarish omboriga chiqim-o'tkazma yozing; sex omborchisi qabul qilganini kuzating.
5. (Sex omborchisi) QC'dan o'tgan tayyor mahsulotni **Ishlab chiqarishdan sotuvga** o'tkazing.
6. (Savdo omborchisi) `TRNSF_...-IN` kirimlarini qabul qiling; sotuv invoyslari avtomatik chiqim qilganini Qoldiqlar sahifasida tekshiring.
7. Har hujjatga qog'oz nusxa (yuk xati, dalolatnoma) skanini biriktiring.

### Har hafta

1. **DOH Dashboard**: "Past DOH" va "High runner" ro'yxatini xaridchiga yuboring.
2. **Chiqimlar** → Status = `Kutilmoqda`: uzoq turgan o'tkazmalarni qabul qiluvchi bilan aniqlang.
3. DLOC omborlarda **Joylar bo'yicha qoldiq** → Sig'im % 100 ga yaqin joylarni bo'shating.

### Har oy

1. Oy oxirida komissiya bilan sanoq → **Inventarizatsiya** (Excel) — oyiga bir marta, shuning uchun faylni ikki marta tekshiring.
2. **Oylik hisobot** (Miqdor va Summa) ni chop eting, imzolang va buxgalteriyaga topshiring.
3. Hisobdan chiqarishlarni (izoh bo'yicha qidiruv) yig'ib rahbarga hisobot bering.

## 4.13 Ruxsatlar

- "Omborxona" menyu bo'limi faqat ombor ruxsati bor foydalanuvchilarga ko'rinadi.
- Omborchi faqat **o'ziga biriktirilgan** omborga kirim qiladi va undan chiqim qiladi ("Siz faqat biriktirilgan ombordan chiqim qilishingiz mumkin.").
- MES (ishlab chiqarish) va Sotuv/CRM rollari ombor hujjatlarini faqat ko'radi — yaratish va tahrirlash yo'q.
- Ruxsatsiz foydalanuvchi URL orqali (`/Incomes/Create`, `/Expenses/Create`, `/Inventory/Adjust` va h.k.) kirsa ham "Sizga ruxsat berilmagan!" sahifasi chiqadi; hujjat fayllarini yuklab ololmaydi.

## 4.14 Tez-tez uchraydigan savollar va muammolar

**Kirim yaratdim, lekin qoldiq o'zgarmadi.** Kirim "Kutilmoqda" holatida. Details sahifasida **Qabul qilish** ni bosing.

**"Yetarli qoldiq yo'q" chiqyapti, lekin omborda mahsulot bor.** Ehtimol u hali qabul qilinmagan kirimda yoki boshqa omborda turibdi. Qoldiqlar sahifasini ombor filtri bilan tekshiring. DLOC omborda "joylashtirilmagan" qoldiq bo'lsa — avval **DLOC ga joylashtirish**.

**Chiqimda boshqa (arzonroq) partiyani tanlamoqchiman.** Mumkin emas: chiqim sof FIFO, eng eski partiya birinchi ketadi.

**Ishlab chiqarish omboriga qo'lda kirim qila olmayapman.** To'g'ri — Ishlab chiqarish va Savdo omborlariga mahsulot faqat o'tkazma (chiqimdan avtomatik kirim) yoki MES orqali keladi.

**O'tkazmani noto'g'ri omborga yubordim.** Qabul qilinmagan bo'lsa — chiqimni o'chiring (qoldiq qaytadi) yoki qabul qiluvchi **Rad qilsin**. Qabul qilingan bo'lsa — teskari o'tkazma yozing.

**Inventarizatsiyada xato qildim.** Joriy oyda shu omborga qayta inventarizatsiya qilib bo'lmaydi. Administratorga murojaat qiling yoki keyingi oy sanog'ida tuzating.

**"Ishlab chiqarish yoki sotuv ombori topilmadi".** Omborlar sozlamasida "Ombor turi" belgilanmagan (4.2).

**Kirimda DLOC "sig'im oshib ketadi" deyapti.** Boshqa DLOC tanlang yoki qatorni ikki joyga bo'ling; zarurat bo'lsa Admin Max sig'imni oshiradi.

**Omborni o'chira olmayapman.** Unda qoldiq, qabul qilinmagan kirim yoki yakunlanmagan o'tkazma bor. Avval qoldiqni o'tkazing yoki inventarizatsiya qiling.

**Kirim summasi QQS bilan, oylik hisobotda esa kamroq.** To'g'ri: kirim hujjati summasi QQS bilan saqlanadi, oylik hisobot va tannarx esa QQSsiz.
