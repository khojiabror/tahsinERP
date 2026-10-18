# 8. Moliya

Moliya moduli iPack'ning pul harakati va qarzlarini hisobga oladi. Unda quyidagilar yuritiladi:
- kassa, bank hisobi va kartalardagi pul;
- mijozlar, ta'minotchilar, ekspeditor va xodimlar bilan hisob-kitob;
- doimiy xarajatlar (ijara va h.k.);
- balans, foyda-zarar (P&L), kassa oqimi va kompaniya qiymati.

Modul bilan asosan **buxgalter** (Admin yoki moliya huquqi berilgan rol) ishlaydi. **Direktor** va **Ta'sischi** dashboard va hisobotlarni ko'radi.

Ko'p yozuvlar qo'lda emas, boshqa modullardan avtomatik keladi:
- **Sotuv (CRM):** sotuv invoysi;
- **Ta'minot (SRM):** xarid invoysi;
- **Logistika:** yo'l varaqasi to'lovi;
- **HR:** ish haqi.

Shuning uchun buxgalter asosan **to'lovlarni** kiritadi va **solishtiradi**.

**Namunaviy ssenariy (Noyabr 2026, raqamlar misol):**
- iPack AJ "UzAutoMotors Powertrain"ga 3 partiya yetkazadi. Har partiya invoysi 99 120 000 so'm, jami 297 360 000 so'm (6-bob).
- MChJ "Wood Nam Import"dan taxta oladi: 2 600 dona × 54 000 = 140 400 000 + QQS 12% = **157 248 000 so'm** (ikki partiya, 3-bob).
- "IMPORT EVRO GRAND"dan karton oladi: 2 000 m² × 6 500 = 13 000 000 + QQS = **14 560 000 so'm**.
- "Osiyo Trans Kargo" 3 reys tashiydi, har biri 2 500 000 so'm.
- Noyabr ish haqi 85 000 000 so'm.

## 8.1 Asosiy tushunchalar

- **Hisob** — pul turadigan joy: 9000 Naqd pul, 9001 Hisob raqam, 9002 Korporativ karta, xodimlar hisoblari (9003, 9004), 9005 Xarid agenti, 9006 Sotuv agenti. Har hisobning bitta valyutasi bor.
- **Tranzaksiya** — hisobdagi bitta harakat: **Kirim** (pul kirdi) yoki **Chiqim** (pul chiqdi). Unda sana, summa, kategoriya va (kerak bo'lsa) kontragent bo'ladi.
- **Kategoriya** — pul nima uchun kirgan yoki chiqqan (masalan "Sotuv daromadi", "Xomashyo", "Oylik", "Ijara"). Kategoriyalar daraxt ko'rinishida bo'ladi.
- **Balans kategoriyasi** — balans hisobotidagi qator. Masalan "Xaridorlardan haqlarimiz", "Ta'minotchilardan qarzlar", "Xomashyo", "Tayyor maxsulot".
- **P&L elementi** — foyda-zarar hisobotidagi band (daromad yoki xarajat). Unga moliya kategoriyalari bog'lanadi.
- **Ko'zgu (mirror) yozuv** — sotuv yoki xarid invoysi saqlanganda Moliya'da avtomatik paydo bo'ladigan yozuv. U pul harakati emas, faqat qarzning hisobi. Kassa oqimi, hisob qoldiqlari, P&L va EBITDA'ga kirmaydi.
- **Kontragent qarzi** — mijoz, ta'minotchi yoki ekspeditor bilan hisob-kitob qoldig'i. Hisoblash qoidasi: hujjatlar minus to'lovlar.
- **Opening balans** — tanlangan sanadagi boshlang'ich qoldiqlar. Undan keyingi hisob-kitoblar shu sanadan boshlanadi.
- **Akt-sverki** — kontragent bilan o'zaro hisob-kitob solishtirmasi (debet/kredit va yakuniy qoldiq).

## 8.2 Hisoblar

**Kim:** buxgalter   **Menyu:** Moliya → Boshqaruv → Hisoblar

Ro'yxatda barcha faol hisoblar chiqadi. Filtrlar: qidiruv, valyuta, hisob turi. Har hisobning **Details** va **Transactions** sahifasida o'sha hisob bo'yicha harakatlar ko'rinadi.

1. "Yangi" tugmasini bosing.
2. Maydonlarni to'ldiring va saqlang.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Kod * | Hisob kodi (Excel importda shu kod ishlatiladi) | 9001 |
| Nomi * | Hisob nomi | Hisob raqam |
| Egasi | Javobgar shaxs (xodim hisoblari uchun) | 9003 uchun — xodim F.I.Sh. |
| Valyuta * | Hisob valyutasi | UZS |
| Naqd pul | Kassa bo'lsa belgilang | 9000 — belgilangan |
| Bank | Bank hisobi yoki karta bo'lsa belgilang | 9001, 9002 — belgilangan |
| Balans kategoriyasi | Balansda qaysi qatorga qo'shilishi | "Pul mablag'lari" (sizdagi aktiv kategoriya) |

iPack hisoblari qanday sozlanadi:

| Kod | Nomi | Turi | Izoh |
|---|---|---|---|
| 9000 | Naqd pul | Naqd pul | Kassa |
| 9001 | Hisob raqam | Bank | Asosiy bank hisobi. Ish haqi va yo'l varaqasi to'lovlari shu yerdan |
| 9002 | Korporativ karta | Bank | Mayda xaridlar |
| 9003, 9004 | Xodim hisoblari | — | Hisobdor shaxslarga berilgan pul |
| 9005 | Xarid agenti | — | Xarid agentiga berilgan avans |
| 9006 | Sotuv agenti | — | Sotuv agenti orqali tushum |

> **Diqqat:** Tizim invoyslar uchun qo'shimcha hisoblarni **o'zi yaratadi**: "Debitorlar qarzi" (mijozlar) va "Kreditorlar qarzi" (ta'minotchilar), har valyuta uchun alohida. Bu hisoblarga qo'lda to'lov kiritmang va ularni o'chirmang. Ular pul emas, qarz hisobi.

> **Diqqat:** Yo'l varaqasi to'lovlari moliyaga yozilishi uchun shu valyutada kamida bitta faol **Bank** hisobi bo'lishi kerak. Aksiyadorning naqd hissasi yozilishi uchun esa shu valyutada faol **Naqd pul** hisobi bo'lishi kerak. Ish haqi uchun hisob ish haqi formasida tanlanadi.

**O'chirish:** hisob nofaol qilinadi (soft delete). Eski tranzaksiyalar saqlanadi.

**Natija:** Hisob tranzaksiya formasida, "Hisoblardagi qoldiq" hisobotida va (balans kategoriyasi berilgan bo'lsa) Balansda ko'rinadi.

## 8.3 Kategoriyalar daraxti

**Kim:** buxgalter   **Menyu:** Moliya → Boshqaruv → Kategoriyalar

Kategoriya pul nima uchun harakatlanganini bildiradi. Daraxt ko'rinishida tuziladi: "Ota kategoriya" ostida bolalar bo'ladi. Ro'yxatda qidiruv va tur bo'yicha filtr bor.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Nomi * | Kategoriya nomi | Xomashyo (yog'och) |
| Turi * | Daromad / Chiqim / Boshqa | Chiqim |
| Ota kategoriya | Yuqori guruh | Ishlab chiqarish xarajatlari |
| Tartib raqami | Ro'yxatdagi o'rni | 10 |

iPack uchun tavsiya etilgan daraxt (misol):
- **Daromad:** Sotuv daromadi; Boshqa daromadlar.
- **Chiqim:**
  - Ishlab chiqarish xarajatlari: Xomashyo (yog'och), Karton, Metiz, Selofan;
  - Oylik;
  - Ijara;
  - Kommunal;
  - M.Tash (material tashish);
  - T.Tash (tayyor mahsulot tashish);
  - Soliqlar;
  - Bank xizmati.
- **Boshqa:** Ichki o'tkazma; Qarz olish/qaytarish; Aksiyador hissasi.

> **Diqqat:** Ayrim kategoriya nomlarini tizim avtomatik yozuvlarda ishlatadi. Ularni o'zgartirmang:
> - nomida **"Sotuv"** bor daromad kategoriyasi — sotuv invoysi yozuvi uchun;
> - nomida **"Xarid"** bor chiqim kategoriyasi — xarid invoysi yozuvi uchun;
> - **"T.Tash"** va **"M.Tash"** — yo'l varaqasi to'lovlari uchun;
> - nomida **"oylik"** bor kategoriyalar — ish haqi qarzini hisoblashda.
>
> Kerakli kategoriya topilmasa, tizim uni o'zi yaratadi.

> **Diqqat:** EBITDA soliq, foiz va amortizatsiyani kategoriya **nomidan** taniydi:
> - "soliq", "QQS", "NDS" — soliq;
> - "foiz", "kredit", "ssuda" — foiz;
> - "amortizatsiya", "eskirish", "iznos" — amortizatsiya.
>
> Shu so'zlarni nomda ishlating.

**O'chirish:** kategoriya nofaol qilinadi. Eski tranzaksiyalarda nomi qoladi.

## 8.4 Balans kategoriyalari

**Kim:** buxgalter (bir marta sozlanadi)   **Menyu:** Moliya → Boshqaruv → Balans kategoriyalari

Balans kategoriyasi balans hisobotidagi qatorni bildiradi. Har qatorning **turi** va **hisoblash usuli** bo'ladi.

| Maydon | Qiymatlar | Izoh |
|---|---|---|
| Nomi * | — | Balansdagi qator nomi |
| Turi * | Aktiv / Passiv / Kapital | Passiv — majburiyatlar (qarzlar). Kapital — ustav kapitali, aksiyador hissalari |
| Ota kategoriya, Tartib raqami | — | Guruhlash va tartib |
| Hisoblash turi * | Hisob asosida / Tranzaksiya asosida / Hujjat asosida | Quyidagi jadvalga qarang |

| Hisoblash turi | Qanday hisoblanadi | iPack misoli |
|---|---|---|
| Hisob asosida (Account-Based) | Shu kategoriyaga bog'langan hisoblar qoldig'i (Hisoblar → "Balans kategoriyasi") | Pul mablag'lari: 9000, 9001, 9002 |
| Tranzaksiya asosida (Transaction-Based) | Bog'langan **moliya kategoriyalari** bo'yicha kirim − chiqim. Bog'lash "MapCategories" sahifasida | Asosiy vositalar: "Jihoz xaridi" kategoriyasi |
| Hujjat asosida (Document-Based) | Boshqa modullar hujjatlaridan | Oylik qarzi, Logistika qarzi, Doimiy harajatlar qarzi |

Tizim quyidagi kategoriyalarni **nomi** bo'yicha taniydi. Ular aynan shunday nomlanishi kerak:

| Nomi | Turi | Qayerdan hisoblanadi |
|---|---|---|
| Xaridorlardan haqlarimiz | Aktiv | Sotuv invoyslari (QQS bilan) − mijoz to'lovlari |
| Ta'minotchilardan qarzlar | Passiv | Xarid invoyslari − ta'minotchiga to'lovlar |
| Xomashyo | Aktiv | Xomashyo ombori qoldig'i × oxirgi kirim narxi ("Jihoz" kategoriyasidan tashqari) |
| Tayyor maxsulot | Aktiv | Tayyor mahsulot qoldig'i × oxirgi Tannarx (sotuv narxi QQS bilan) |
| Doimiy harajatlar qarzi | Passiv | 8.8 ga qarang |
| nomida "logistika" va "qarz" bor | Passiv | Yo'l varaqalari − ekspeditorga to'lovlar |
| nomida "oylik" bor | Passiv | Hisoblangan ish haqi − to'langan |

> **Diqqat:** Xomashyo, Tayyor maxsulot, Jihozlar va Bino kabi mulk kategoriyalarining turi **Aktiv** bo'lishi kerak, Kapital emas. Aks holda ular balansning noto'g'ri ustuniga tushadi va kompaniya qiymati (DCF) kamayib chiqadi.

## 8.5 Tranzaksiyalar

**Kim:** buxgalter   **Menyu:** Moliya → Boshqaruv → Tranzaksiyalar

**Ro'yxat:**
- qatorlar serverdan 25 tadan yuklanadi, pastda jami yozuvlar soni ko'rsatiladi;
- sana kiritilmasa, oxirgi 3 oy ko'rsatiladi;
- filtrlar: Dan/Gacha, Hisob, Kategoriya, Yo'nalish, Kontragent, Manba hujjat turi, qidiruv (maqsad, izoh, kontragent nomi, shartnoma raqami bo'yicha);
- **Excel eksport** barcha filtrlarni hisobga oladi.

### 8.5.1 Mijozdan tushum (kirim)

1. "Yangi" tugmasini bosing.
2. Quyidagicha to'ldiring. UzAutoMotors Powertrain 1-partiya uchun 25.11.2026 da bankka pul o'tkazdi:

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Sana * | Bank ko'chirmasidagi sana | 25.11.2026 |
| Hisob * | Pul tushgan hisob | 9001 Hisob raqam |
| Kategoriya * | Daromad kategoriyasi | Sotuv daromadi |
| Yo'nalish * | Kirim / Chiqim | Kirim |
| Summa * | To'lov summasi | 99 120 000 |
| Valyuta * | Hisob valyutasi bilan bir xil | UZS |
| Kontragent turi | Mijoz / Ta'minotchi / Aksiyador / Forwarder / Xodim / Boshqa | Mijoz |
| Kontragent | Ro'yxatdan (turga qarab yuklanadi) | AJ "UzAutoMotors Powertrain" |
| Shartnoma (sotuv) | Kontragent tanlangach chiqadi; "Shartnoma raqami"ni to'ldiradi | S-2026/045 |
| Maqsad | To'lov topshiriqnomasidagi maqsad | "1-partiya uchun to'lov" |
| Manba hujjat turi | Qo'lda | Qo'lda |
| Izoh | Ixtiyoriy | To'lov topshiriqnomasi № ... |

3. Saqlang.

**Natija:**
- 9001 qoldig'i 99 120 000 ga oshadi.
- Mijoz qarzi 297 360 000 dan 198 240 000 ga kamayadi. Bu Balans, Majburiyatlar dashboardi, CRM Dashboard va Akt-sverkida ko'rinadi.
- Kassa oqimida kirim 99 120 000 bo'ladi.

> **Diqqat:** "Kontragent turi", "Kontragent" va "Shartnoma"ni albatta to'ldiring.
> - Kontragentsiz to'lov hech qaysi qarzni kamaytirmaydi va hech qaysi Akt-sverkiga tushmaydi.
> - Shartnomasiz to'lov faqat "Barcha shartnomalar" Akt-sverkisida ko'rinadi. Shartnoma bo'yicha aktda u yo'q, aktda bu haqda ogohlantirish chiqadi.

> **Diqqat:** Hisob valyutasi tranzaksiya valyutasidan farq qilsa, saqlanmaydi: "Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi."

### 8.5.2 Ta'minotchiga to'lov (chiqim)

Wood Nam Import'ga 15.11.2026 da 60 000 000 so'm to'lanadi.
1. Yo'nalish = Chiqim, Hisob = 9001, Kategoriya = "Xomashyo (yog'och)", Summa = 60 000 000.
2. Kontragent turi = Ta'minotchi, Kontragent = MChJ "Wood Nam Import", "Shartnoma (xarid)" dan shartnomani tanlang.
3. Invoysga aniq bog'lash uchun: Manba hujjat turi = "Ta'minotchiga to'lov (xarid invoysiga bog'lash)", "Manba hujjat ID" = xarid invoysi ID (Ta'minot → Invoyslar sahifasida ko'rinadi).
4. Saqlang.

**Natija:** 9001 qoldig'i 60 000 000 ga kamayadi. Wood Nam Import'ga qarz 157 248 000 − 60 000 000 = **97 248 000 so'm** bo'ladi.

> **Diqqat:** "Ta'minotchiga to'lov" tanlansa, uchta shart majburiy: Kontragent turi = Ta'minotchi, Kontragent tanlangan va xarid invoysi ID kiritilgan. Bittasi bo'lmasa saqlanmaydi.

> **Diqqat:** Faqat **"Qo'lda"** turidagi tranzaksiyani keyin tahrirlash yoki o'chirish mumkin. "Ta'minotchiga to'lov", "Doimiy harajat to'lovi" va boshqa modullardan kelgan yozuvlar saqlangandan keyin qulflanadi: "Ushbu tranzaksiya '...' hujjatidan yaratilgan...". Bunday yozuvni saqlashdan oldin summani, sanani va invoys ID'ni yaxshilab tekshiring.

### 8.5.3 Boshqa holatlar

| Holat | Yo'nalish | Kontragent turi | Kategoriya / manba |
|---|---|---|---|
| Kassadan bankka pul topshirish | 9000 dan Chiqim + 9001 ga Kirim (ikki yozuv) | — | Ichki o'tkazma |
| Xodimga hisobdor pul berish | 9001 dan Chiqim + 9003 ga Kirim | Xodim | Ichki o'tkazma |
| Ekspeditorga to'lov | Logistika modulida "Yo'l varaqasi to'lovi" orqali kiriting (8.6) | — | — |
| Ijara to'lovi | Chiqim | Boshqa | Ijara; Manba = "Doimiy harajat to'lovi", Manba hujjat ID = majburiyat ID (8.8) |
| Oylik avansi | Chiqim | Xodim | Oylik |

### 8.5.4 Excel import

**Menyu:** Tranzaksiyalar → Import

1. Shablonni yuklab oling. Unda "Hisoblar", "Kategoriyalar" va "Valyutalar" varaqlari bor: to'g'ri kod va nomlarni shu yerdan oling.
2. Ustunlar:
   - Sana (DD.MM.YYYY), Vaqt (HH:mm), Hisob kodi, Kategoriya nomi;
   - Yo'nalish (1=Kirim, 2=Chiqim), Summa, Valyuta qisqa nomi;
   - Kontragent nomi, Maqsad, Shartnoma raqami, Izoh, Miqdor, Narx birligi (bular ixtiyoriy).
3. Faqat .xlsx fayl qabul qilinadi. Natijada "N ta tranzaksiya muvaffaqiyatli import qilindi!" xabari chiqadi. Xato va ogohlantirishlar qatorma-qator ko'rsatiladi.

> **Maslahat:** Bank ko'chirmasini shu shablonga keltirib, oyiga bir marta import qiling. Bazada aynan bir xil qator bo'lsa, u o'tkazib yuboriladi va ikki marta yozilmaydi. Importda kontragent faqat **nom** bilan yoziladi. Qarzga ta'sir qiladigan to'lovlarni (mijoz va ta'minotchi) qo'lda, kontragent va shartnomani tanlab kiriting.

## 8.6 Avtomatik yozuvlar: qaysi modul nimani yozadi

| Manba (modul) | Qachon | Moliya yozuvi | Pulga ta'sir |
|---|---|---|---|
| Sotuv invoysi (CRM) | Invoys saqlanganda yoki tahrirlanganda | Kirim, "Debitorlar qarzi" (invoys valyutasi), kategoriya "Sotuv...", summa QQS bilan, Maqsad "Sotuv invoysi #..." | Yo'q (ko'zgu). Bekor qilinsa yozuv olib tashlanadi |
| Xarid invoysi (SRM) | Invoys saqlanganda | Chiqim, "Kreditorlar qarzi", kategoriya "Xarid...". Summa: invoysga bog'langan ombor kirimi bo'lsa kirim summasi, aks holda invoys summasi (QQS bilan) | Yo'q (ko'zgu) |
| Yo'l varaqasi to'lovi (Logistika) | To'lov kiritilganda | Chiqim, Bank hisobidan, kategoriya "T.Tash" (tayyor mahsulot) yoki "M.Tash" (material), kontragent — ekspeditor | Ha |
| Ish haqi (HR) | Hisob tasdiqlanganda | Chiqim, ish haqi formasida tanlangan hisob va "Chiqim toifasi", Maqsad "Ish haqi hisobi YYYY-MM ..." | Ha |
| Aksiyador hissasi | "Hissa qo'shish", turi "Naqd pul" | Kirim, Naqd pul hisobiga, kontragent — aksiyador | Ha |

iPack, noyabr misoli:
- uch sotuv invoysi → 3 ta ko'zgu yozuv, jami 297 360 000;
- xarid invoyslari → 157 248 000 (Wood Nam Import) va 14 560 000 (IMPORT EVRO GRAND);
- Osiyo Trans Kargo'ga 2 reys uchun 5 000 000 → bankdan chiqim, kategoriya T.Tash;
- noyabr ish haqi 85 000 000 → tasdiqlanganda 9001 dan chiqim.

> **Diqqat:** Avtomatik yozuvlarni Tranzaksiyalar sahifasida tahrirlab yoki o'chirib bo'lmaydi. O'zgartirish faqat **manba hujjatda** qilinadi: invoysni tahrirlash yoki bekor qilish, ish haqini qayta hisoblash va h.k.

> **Diqqat:** Xarid invoysining moliyaviy yozuvi invoys saqlangan paytdagi summa bilan yoziladi. Ombor kirimi keyinroq kiritilsa, qarz hisobotlari (Balans, Akt-sverki) kirim summasidan foydalanadi. Ko'zgu yozuvning o'zi esa o'zgarmasligi mumkin. Qarzni Balans va Akt-sverkidan kuzating.

> **Diqqat:** Ish haqi tasdiqlanishi uchun ish haqi hisobida "Moliya hisobi (kassa/bank)" va "Chiqim toifasi" tanlangan bo'lishi kerak. Tasdiqlash tugmasini ikki marta bossangiz ham, yozuv faqat bir marta paydo bo'ladi.

## 8.7 Boshlang'ich qoldiqlar (Opening balans)

**Kim:** buxgalter (Admin)   **Menyu:** Moliya → Boshqaruv → Opening balans

Opening balans tizimga kirishdan oldingi qoldiqlarni kiritish uchun kerak. U keyinchalik "nazorat nuqtasi" sifatida ham ishlatiladi (masalan yil boshi). Undan keyin barcha qarzlar va Akt-sverkilar **shu sanadagi qoldiq + keyingi harakatlar** bo'yicha hisoblanadi.

Ro'yxat (Index) sahifasida har opening sanasi va "Qatorlar soni" ko'rinadi. Har sana uchun Ko'rish, Tahrirlash va O'chirish amallari bor.

**Tahrirlash sahifasining bo'limlari:**

| Bo'lim | Nima kiritiladi | iPack misoli (31.12.2026) |
|---|---|---|
| Hisoblar bo'yicha opening (kassa, bank) | Har hisobning qoldig'i | 9000 — 4 500 000; 9001 — 312 000 000 |
| Mijozlardan haqqimiz (AR) — shartnoma kesimida | Mijoz, shartnoma, qoldiq | UzAutoMotors Powertrain / S-2026/045 |
| Ta'minotchilardan qarzimiz (AP) — shartnoma kesimida | Ta'minotchi, shartnoma, qoldiq | Wood Nam Import / shartnoma |
| Logistikadan qarzimiz — ekspeditor shartnomasi kesimida | Ekspeditor, shartnoma, qoldiq | Osiyo Trans Kargo |
| Oylikdan qarzimiz (HRM) — xodim kesimida | Xodim, qarz | Har xodim bo'yicha |
| Boshqa balans kategoriyalari (manual opening) | Kategoriya, summa | Asosiy vositalar, Xomashyo, Tayyor maxsulot |

**Tugmalar:**
- **Load qilish** — tanlangan sananing saqlangan qatorlarini ochadi.
- **Auto-fill from current balances** — sanadagi joriy qoldiqlarni avtomatik hisoblab yozadi:
  - har mijoz, ta'minotchi va ekspeditor bo'yicha shartnoma kesimida, shartnomasiz qoldiq va avans alohida qatorda;
  - har xodim bo'yicha oylik qarzi.
  
  Hisob Akt-sverki bilan bir xil qoidada bajariladi. Tasdiq so'raladi, so'ng "Auto-fill bajarildi..." xabari chiqadi.
- **Excel template / Excel import (AR/AP)** — mijoz va ta'minotchi qoldiqlarini Excel'dan yuklaydi. Import qatorlari formaga qo'shiladi, keyin **"Saqlash"**ni bosish shart.
- **+ Qator qo'shish / O'chir** — qo'lda tahrirlash.
- **Saqlash** — hammasi bitta tranzaksiyada yoziladi. Natijada "Opening balans muvaffaqiyatli saqlandi." xabari chiqadi.

**Birinchi marta kiritish tartibi (iPack):**
1. Opening sanani tanlang (masalan tizimga o'tishdan oldingi oyning oxirgi kuni) va "Load qilish"ni bosing.
2. Hisoblar qoldig'ini bank ko'chirmasi va kassa daftari bo'yicha kiriting.
3. Mijoz va ta'minotchi qoldiqlarini ular bilan imzolangan Akt-sverki bo'yicha, shartnoma kesimida kiriting (yoki Excel import qiling).
4. Ekspeditor, oylik qarzi va boshqa kategoriyalar (Xomashyo, Tayyor maxsulot, asosiy vositalar) qoldiqlarini kiriting.
5. "Saqlash"ni bosing va sanani qayta ochib qatorlarni tekshiring.

> **Diqqat:** Opening sanasigacha bo'lgan invoys va to'lovlar Akt-sverki va qarzlarda **qayta qo'shilmaydi**: ular opening qoldig'i ichida deb hisoblanadi. Shuning uchun opening'ni kiritgandan keyin o'sha sanadan oldingi hujjatlarni o'zgartirmang.

> **Diqqat:** Xomashyo yoki Tayyor maxsulot uchun opening kiritilsa, balansda shu qiymat ombor hisobining **o'rniga** ishlatiladi.

> **Maslahat:** Auto-fill sananing mavjud qatorlarini o'chirib, qaytadan yozadi. Qo'lda kiritgan "Boshqa balans kategoriyalari" qatorlaringizni Auto-fill'dan **keyin** tekshiring.

**O'chirish:** sana bo'yicha o'chiriladi ("Ha, o'chirish"). Natijada "Opening balans (YYYY-MM-DD) o'chirildi." xabari chiqadi va Akt-sverkilar oldingi opening'dan hisoblanadi.

## 8.8 Doimiy majburiyatlar (Doimiy harajatlar)

**Kim:** buxgalter   **Menyu:** Moliya → Hisobotlar → Doimiy harajatlar

Bu bo'lim har oy takrorlanadigan majburiyatlar uchun: ijara, buxgalteriya xizmati, internet va h.k. Har oy majburiyat summasi qarz sifatida yig'iladi va to'lov kiritilganda kamayadi.

| Maydon | Nima kiritiladi | iPack misoli |
|---|---|---|
| Nomi * | Majburiyat nomi | Sex ijarasi |
| Oylik summa * | Har oy | 6 000 000 |
| Valyuta | | UZS |
| Boshlash yili / oyi | Qachondan hisoblanadi | 2026 / 1 |
| Balans kategoriyasi (Passivlar) | Odatda "Doimiy harajatlar qarzi" | Doimiy harajatlar qarzi |
| Faol | Tugagan shartnomani nofaol qiling | belgilangan |

**To'lash tartibi:**
1. Ro'yxatda majburiyat **ID**sini ko'ring.
2. Tranzaksiya kiriting:
   - Chiqim, Hisob = 9001, Kategoriya = "Ijara", Summa = 6 000 000;
   - Manba hujjat turi = "Doimiy harajat to'lovi (ijara, buxgalteriya va h.k.)";
   - Manba hujjat ID = majburiyat ID.

**Natija:** Balansdagi "Doimiy harajatlar qarzi" = hisoblangan oylar × oylik summa − to'lovlar. Misol: 11 oy × 6 000 000 = 66 000 000; 10 oy to'langan bo'lsa, qarz = 6 000 000. "Yadro moliyaviy hisobot"da "Doimiy harajatlar: qaysilari to'lanmadi" jadvali chiqadi: Harajat nomi, Oyiga, Yig'ilgan oy, Yig'ilgan, Qoldiq qarz.

> **Diqqat:** Balans kategoriyasi tanlanmagan majburiyat ham "Doimiy harajatlar qarzi"ga qo'shiladi.

## 8.9 P&L: elementlar, reja va fakt

### P&L elementlari

**Menyu:** Moliya → Boshqaruv → P&L elementlar

| Maydon | Qiymatlar | iPack misoli |
|---|---|---|
| Kod | — | 4000 |
| Nomi * | — | Sotuv tushumi |
| Belgi * | + (Musbat) — daromad; - (Manfiy) — xarajat | + |
| Ota element, Tartib raqami | — | — |

Element Details sahifasida **"Kategoriyalarni bog'lash" (MapCategories)** orqali moliya kategoriyalari belgilanadi. Masalan:
- "Sotuv tushumi" ← "Sotuv daromadi";
- "Xomashyo xarajati" ← Xomashyo (yog'och), Karton, Metiz, Selofan;
- "Mehnat haqi" ← Oylik.

> **Diqqat:** Qaysi kategoriyalar P&L'ga **bog'lanmasligi** kerak: qarz olish/qaytarish, ichki o'tkazma, aksiyador hissasi. Ular foyda emas. EBITDA va kompaniya qiymati ham faqat P&L'ga bog'langan kategoriyalardan hisoblanadi. Nomida "divid" bor band EBITDA xarajatiga kirmaydi.

### P&L rejalari

**Menyu:** Moliya → Boshqaruv → P&L rejalar

| Maydon | iPack misoli |
|---|---|
| Yil * / Oy * | 2026 / 11 |
| P&L Element * | Sotuv tushumi |
| Reja summasi * | 297 360 000 |

- **BulkEdit** — bir oyning barcha bandlari rejasini bitta jadvalda kiritish yoki o'zgartirish (0 ham kiritish mumkin).
- **Excel eksport** — yil va oy bo'yicha.
- O'chirilgan reja hisobotlarga kirmaydi. Shu yil, oy va band uchun rejani qayta yaratish mumkin.

### Foyda va zarar (reja va fakt)

**Menyu:** Moliya → Hisobotlar → Foyda va zarar

Yil va oyni tanlang. Ustunlar: Element, Haqiqiy, Reja, Farq, Farq %.

> **Diqqat:** "Haqiqiy" ustuni **haqiqiy pul harakati** bo'yicha hisoblanadi (kassa usuli), invoyslar bo'yicha emas. Masalan, noyabrda uch invoys (297 360 000) chiqarilgan, lekin mijoz faqat 99 120 000 to'lagan. U holda "Sotuv tushumi" bandi Haqiqiy = 99 120 000 bo'ladi. Invoys bo'yicha sotuvni CRM hisobotlaridan ko'ring (6-bob).

## 8.10 Kontragent qarzlari va Akt-sverki

Hamma sahifada qarz **bitta qoida** bilan hisoblanadi. Shuning uchun Balans, Majburiyatlar dashboardi, CRM Dashboard, Opening Auto-fill va Akt-sverki tiyinigacha bir xil raqam beradi.

| Kontragent | Hujjatlar (qarzni oshiradi) | To'lovlar (kamaytiradi) |
|---|---|---|
| Mijoz | Faol sotuv invoyslari, QQS bilan (bekor qilinganlar kirmaydi) | Kirim, Kontragent turi = Mijoz |
| Ta'minotchi | Xarid invoysi: bog'langan ombor kirimi summasi, bo'lmasa invoys summasi (QQS bilan) | Chiqim, Kontragent turi = Ta'minotchi |
| Ekspeditor | Yo'l varaqalari | Yo'l varaqasi to'lovlari |
| Xodim (oylik) | Tasdiqlangan ish haqi | Oylik to'lovlari |

Hisob har doim **oxirgi opening sanasidagi qoldiqdan** boshlanadi va unga keyingi harakatlar qo'shiladi.

### Akt-sverki

**Qayerdan ochiladi:**
- mijoz: Sotuvlar (CRM) → Shartnomalar → shartnoma Details → **"Akt-sverki (ushbu shartnoma)"**;
- ta'minotchi: Ta'minot → shartnoma Details → "Akt-sverki";
- ekspeditor: Logistika → shartnoma Details yoki Logistika dashboardi.

1. Yuqoridagi ro'yxatdan shartnomani yoki **"— Barcha shartnomalar —"**ni tanlang.
2. "Qoldiq sanasi"ni kiriting va "Yangilash"ni bosing.
3. Jadvalni o'qing (ustunlar: Mazmuni, Debet, Kredit):
   - birinchi qator — "Opening balansdan boshlangan..." qoldiq;
   - keyin invoyslar va to'lovlar;
   - oxirgi qator — "Yakuniy qoldiq".
4. "Excel" tugmasi bilan yuklab oling va kontragentga imzolashga yuboring.

iPack misoli, UzAutoMotors Powertrain / S-2026/045, qoldiq sanasi 30.11.2026:

| Mazmuni | Debet | Kredit |
|---|---|---|
| Sotuv invoysi № ... 10.11.2026 | 99 120 000 | |
| Sotuv invoysi № ... 20.11.2026 | 99 120 000 | |
| FIN to'lovi № ... 25.11.2026 | | 99 120 000 |
| Sotuv invoysi № ... 30.11.2026 | 99 120 000 | |
| **Yakuniy qoldiq 30.11.2026** | **198 240 000** | |

**Tuzatish:** Akt-sverki sahifasidagi "Tuzatish" tugmasi "Akt-sverki tuzatishi" formasini ochadi. U kontragent imzolagan akt bilan farq bo'lsa ishlatiladi. Shartnoma bo'yicha yiliga bitta tuzatish kiritiladi. Maydonlari: sana chegarasi, Party1 debet, Party1 kredit, izoh. Akt jadvalida tuzatish qatori alohida rang bilan ko'rinadi.

> **Diqqat:** Bekor qilingan sotuv invoyslari aktga kirmaydi. Shartnomasiz to'lovni ko'rish uchun "Barcha shartnomalar" rejimini tanlang.

## 8.11 Balans va tafsilotlar

**Kim:** buxgalter, direktor   **Menyu:** Moliya → Hisobotlar → Balans

1. "Sana"ni tanlang va "Ko'rsatish"ni bosing.
2. Ikki ustun chiqadi: **Aktivlar** va **Passivlar va Kapital**. Har biri "Jami ..." qatori bilan tugaydi.
3. Aktivlar yoki passivlar tafsiloti sahifasini oching. Ustunlar: Kategoriya, Hisoblash usuli, Izoh, Transaction soni, Qo'shilgan, Ayrilgan, Summa.
4. Kategoriyani bossangiz, **"Kategoriya harakatlari"** oynasi chiqadi: oxirgi 20 ta qo'shilgan va ayrilgan harakat. Ombor kategoriyalari uchun ombor × narx hisobi ko'rinadi.

**Balans tengligi:** Aktiv = Majburiyatlar + Kapital + **"Taqsimlanmagan foyda (hisoblangan)"**. Oxirgi qator balanslovchi qator: aktivdan majburiyat va kiritilgan kapital ayirib topiladi. Majburiyatlar musbat son bilan ko'rsatiladi.

iPack misoli, 30.11.2026, faqat noyabr harakatlari:

| Qator | Summa |
|---|---|
| Xaridorlardan haqlarimiz (aktiv) | 198 240 000 |
| Ta'minotchilardan qarzlar (passiv) | 97 248 000 (Wood Nam Import). IMPORT EVRO GRAND to'liq to'langan |
| Logistika qarzi (passiv) | 2 500 000 (Osiyo Trans Kargo, 3-reys) |
| Oylik qarzi (passiv) | 0. Noyabr ish haqi dekabrda tasdiqlanadi va tasdiqlash paytida chiqim ham yoziladi. Qarz faqat opening'dagi yoki qo'lda kam to'langan summalardan paydo bo'ladi |

> **Maslahat:** Aktivlar tafsilotidagi jami "Jami aktivlar"ga, passivlar tafsilotidagi jami esa "Jami passivlar va kapital"ga teng bo'lishi kerak. Teng bo'lmasa, administratorga murojaat qiling.

## 8.12 Majburiyatlar dashboardi va Yadro moliyaviy hisobot

**Menyu:** Dashboard → Moliya Dashboard → Majburiyatlar dashboard

Dan sana va Gacha sanani tanlang (12 oygacha). Sahifa bo'limlari:
- **Ta'minotchilardan qarzlar (Payables)** — Pie Chart, Oylik trend, Payables account qoldiqlari;
- **Oyliklardan qarzimiz (Trend)**;
- **Xaridorlardan haqlarimiz (Receivables)** — Pie Chart, Receivables account qoldiqlari.

Raqamlar Balans bilan bir xil.

**Yadro moliyaviy hisobot** (Moliya → Hisobotlar) uchun "Holat sanasi"ni tanlang. Unda quyidagilar bor:
- TOP mijozlar (haqqimiz) va TOP ta'minotchilar (qarzimiz);
- Mijozlar bo'yicha haqqimiz va Ta'minotchilar bo'yicha qarzimiz — to'liq ro'yxat;
- oylik qarzi xodim kesimida: Hisoblangan, To'langan, Qarz;
- to'lanmagan doimiy harajatlar.

Direktor uchun haftalik "kimdan qancha olamiz, kimga qancha beramiz" hisoboti shu sahifa.

## 8.13 Kassa oqimi, hisob qoldiqlari va tahlillar

| Sahifa | Menyu | Nima ko'rsatadi |
|---|---|---|
| Kassa oqimi | Moliya → Hisobotlar → Kassa oqimi | Davrdagi kirim/chiqim. Filtr: Dan sana, Gacha sana, Hisob. Faqat haqiqiy pul harakati, invoys ko'zgulari kirmaydi |
| Hisoblardagi qoldiq | Moliya → Hisobotlar → Hisoblardagi qoldiq | Har hisob: Hisob raqam, Egasi, Kirim, Chiqim, Qoldiq. Filtr: Hisob turi (Barchasi / Aktiv / Majburiyat) |
| Umumiy ko'rinish | Dashboard → Moliya Dashboard | Oylik trend, eng ko'p kirim/chiqim kategoriyalari |
| Kategoriyalar / Hisoblar / Chiqimlar / Kirimlar tahlili | Dashboard → Moliya Dashboard | Kategoriya, hisob va oy kesimida taqsimot |

iPack, noyabr 2026 Kassa oqimi (misol):
- **kirim:** 99 120 000 (UzAutoMotors to'lovi);
- **chiqim:** 60 000 000 (Wood Nam) + 14 560 000 (IMPORT EVRO GRAND) + 5 000 000 (Osiyo Trans Kargo) + 6 000 000 (ijara) = 85 560 000;
- **sof oqim:** +13 560 000.

Invoys ko'zgulari (297 360 000 va 171 808 000) kassa oqimida yo'q.

> **Diqqat:** Moliya dashboardlari hozircha turli valyutadagi summalarni so'mga o'girmasdan qo'shadi. USD hisob va invoyslarni alohida filtr bilan ko'ring. So'mdagi konvertatsiya faqat CRM dashboard va sotuv hisobotlarida bor.

## 8.14 EBITDA va kompaniya qiymati (DCF)

**EBITDA** (Moliya → Hisobotlar → EBITDA) uchun Boshlanish va Tugash sanalarini tanlang. Standart davr — oxirgi 12 oy.
- Operatsion daromad = P&L daromad bandlariga bog'langan kategoriyalar bo'yicha haqiqiy pul tushumi.
- Operatsion xarajat = P&L xarajat bandlari (dividenddan tashqari).
- EBITDA = operatsion foyda + foiz + soliq + amortizatsiya. Bu uchtasi kategoriya nomidan aniqlanadi (8.3).

**Kompaniya qiymati** (Moliya → Aksiyadorlar → Kompaniya qiymati) uchun Sana va Hisoblash uslubini tanlang:

| Uslub | Formula | Parametr |
|---|---|---|
| EBITDA bilan | Oxirgi 12 oy EBITDA × multiplikator | EBITDA multiplikatori, standart 3 (1–10) |
| DCF bilan | Sof aktivlar (balans aktivlari − majburiyatlar; kapital ayirilmaydi) + yillik daromad × o'rtacha Tannarx marjasi × N yil | Loyihalash yili, standart 3. Tannarx ma'lumoti bo'lmasa marja 10% |

Pastda aksiyadorlar jadvali chiqadi: Aksiyador, Ulush %, Qiymati. Qiymat = kompaniya qiymati × sanadagi ulush.

## 8.15 Aksiyadorlar va ulushlar

**Kim:** Admin, Ta'sischi   **Menyu:** Moliya → Aksiyadorlar → Ro'yxat

1. "Yangi": To'liq ism *, Telefon, Izoh, Faol.
2. **Ulush davrlari (ManageSharePeriods):** Dan sana *, Gacha sana (bo'sh bo'lsa — hozirgacha), Ulush % *.
   - Bir davrda barcha aksiyadorlar ulushi 100% dan oshmasligi kerak, aks holda xato chiqadi.
   - Ulush 0% dan katta va 100% dan oshmasligi kerak.
3. **Hissa qo'shish (AddContribution):** Sana *, Summa *, Valyuta *, Hissa turi * (Naqd pul / Tovarlar / Xomashyo / Tayyor mahsulot / Boshqa), Tavsif.
   - "Naqd pul" turida shu valyutadagi faol kassa hisobiga avtomatik kirim yoziladi.
   - Kassa hisobi bo'lmasa, hissa saqlanadi, lekin ogohlantirish chiqadi.

iPack misoli: X.M. Maxmudov — 100%, 01.01.2026 dan.

> **Diqqat:** O'chirilgan aksiyador ro'yxatda ham, kompaniya qiymati jadvalida ham ko'rinmaydi.

## 8.16 Buxgalter uchun oylik yopish tartibi

Tizimda davrni qulflash (yopish) funksiyasi yo'q. Shuning uchun quyidagi tartibni har oy boshida, o'tgan oy uchun bajaring. iPack misolida noyabr uchun 1–5 dekabr kunlari.

1. **Sotuv:** noyabrdagi barcha jo'natmalar invoys qilinganini tekshiring.
   - CRM Dashboard → "Invoys yaratilmagan buyurtmalar".
   - Xato invoyslarni bekor qiling (6.7.3).
2. **Xarid:** Ta'minot bo'limi bilan barcha xarid invoyslari va ombor kirimlari kiritilganini solishtiring (Wood Nam Import, IMPORT EVRO GRAND).
3. **Bank va kassa:** bank ko'chirmasidagi barcha to'lovlarni kiriting (qo'lda yoki Excel import).
   - Mijoz va ta'minotchi to'lovlarida **Kontragent va Shartnoma** to'ldirilgan bo'lsin.
   - Tranzaksiyalar ro'yxatida Kontragent turi = Mijoz/Ta'minotchi filtri bilan bo'sh shartnomali yozuvlarni qidiring.
4. **Hisob qoldiqlari:** "Hisoblardagi qoldiq" (Gacha = 30.11.2026) dagi 9000, 9001, 9002 qoldig'i kassa daftari va bank ko'chirmasi bilan teng bo'lishi kerak.
5. **Logistika:** Osiyo Trans Kargo yo'l varaqalari va to'lovlari kiritilganini tekshiring. Logistika qarzi ekspeditor Akt-sverkisi bilan teng bo'lishi kerak.
6. **Ish haqi:** HR noyabr ish haqini tasdiqlaganini tekshiring. Tranzaksiyalarda "Ish haqi hisobi 2026-11" chiqimi bo'lishi kerak.
7. **Doimiy harajatlar:** ijara va boshqa majburiyatlar to'lovlari "Doimiy harajat to'lovi" turi bilan kiritilganini tekshiring.
   - "Yadro moliyaviy hisobot"dagi "qaysilari to'lanmadi" jadvalini ko'ring.
8. **Akt-sverki:** asosiy kontragentlar bilan aktni 30.11.2026 holatiga chiqaring va Excel'da yuboring: UzAutoMotors Powertrain, Wood Nam Import, IMPORT EVRO GRAND, Osiyo Trans Kargo.
   - Farq bo'lsa, sababini toping: kiritilmagan to'lov yoki noto'g'ri shartnoma.
   - Kerak bo'lsa "Tuzatish" kiriting.
9. **Balans:** 30.11.2026 holatiga Balansni oching.
   - Tafsilot sahifalari jamilari asosiy sahifa bilan teng bo'lishi kerak.
   - "Taqsimlanmagan foyda (hisoblangan)" qatoridagi kutilmagan o'zgarishga e'tibor bering.
10. **P&L:** "Foyda va zarar" (2026, noyabr) da Haqiqiy va Reja ustunlarini solishtiring. Keyingi oy rejasini "P&L rejalar → BulkEdit"da kiriting.
11. **Direktorga hisobot:** Yadro moliyaviy hisobot, Majburiyatlar dashboardi, Kassa oqimi va EBITDA.
12. **Yil oxirida (31.12):** barcha tekshiruvlardan keyin Opening balansda "Auto-fill from current balances"ni bajaring va saqlang. Shu bilan keyingi yil hisob-kitoblari shu nuqtadan boshlanadi.

> **Diqqat:** Opening saqlangandan keyin o'sha sanagacha bo'lgan hujjatlarni o'zgartirmang. Ular qarzlarda qayta hisoblanmaydi va farq paydo bo'ladi. O'zgartirish zarur bo'lsa, opening'ni o'chirib, qayta Auto-fill qiling.

## 8.17 Tez-tez uchraydigan savollar va muammolar

**Mijoz to'ladi, lekin qarzi kamaymadi.**
Tranzaksiyada "Kontragent turi = Mijoz" va "Kontragent" tanlanmagan, yoki yo'nalish Kirim emas. Qo'lda kiritilgan yozuvni tahrirlab, kontragent va shartnomani qo'shing.

**Akt-sverki shartnoma bo'yicha bitta, "Barcha shartnomalar" bo'yicha boshqa summa.**
To'lov shartnoma raqamisiz kiritilgan. Aktda bu haqda ogohlantirish chiqadi. To'lovga to'g'ri shartnoma raqamini qo'ying.

**Tranzaksiyani tahrirlab yoki o'chirib bo'lmayapti.**
Yozuv boshqa moduldan (invoys, ish haqi, yo'l varaqasi) kelgan yoki "Ta'minotchiga to'lov" / "Doimiy harajat to'lovi" turida. Manba hujjatni o'zgartiring. Xato to'lov bo'lsa, administratorga murojaat qiling.

**"Hisob valyutasi bilan tranzaksiya valyutasi mos kelmaydi".**
USD to'lovni USD hisobga kiriting. Kerak bo'lsa avval USD hisob yarating.

**Ish haqi tasdiqlandi, lekin Moliya'da chiqim yo'q.**
Ish haqi hisobida "Moliya hisobi" yoki "Chiqim toifasi" tanlanmagan, yoki shu valyutada faol hisob yo'q.

**Yo'l varaqasi to'lovi moliyaga tushmadi.**
To'lov valyutasida faol **Bank** hisobi yo'q. Hisoblar sahifasida "Bank" belgisini tekshiring.

**Balansda ombor qiymati g'alati.**
Opening'da "Xomashyo" yoki "Tayyor maxsulot" kiritilgan bo'lsa, ombor hisobi o'rniga shu qiymat ishlatiladi. Opening yo'q bo'lsa, qiymat ombor qoldig'i × oxirgi narxdan hisoblanadi. Kategoriya turi Aktiv ekanini tekshiring.

**P&L'dagi sotuv CRM hisobotidagi sotuvdan kam.**
P&L kassa usulida (tushgan pul), CRM esa invoys usulida (QQSsiz) hisoblaydi. Farq — to'lanmagan qarz va QQS.

**EBITDA'ga qarz olish ham daromad bo'lib kirib qoldi.**
"Qarz olish" kategoriyasi P&L daromad bandiga bog'langan. P&L elementlar → MapCategories'da bog'lanishni olib tashlang.
