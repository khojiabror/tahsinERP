# Kirish va tayyorgarlik

Bu hujjat audit va optimizatsiyadan keyin tahsinERP'ga kiritilgan o'zgarishlarni qo'lda, brauzerda tekshirish uchun tuzilgan. Har bir test bitta biznes amalini demo ma'lumot bilan bajaradi va natijani tekshiradi: masalan, kirim kiritilgach ombor qoldig'i ko'paydimi, to'lovdan keyin qarz kamaydimi.

Testlar bir-biriga zanjir bo'lib bog'langan: xariddan kelgan xomashyo ishlab chiqarishga, tayyor mahsulot sotuvga, sotuv esa moliyaga o'tadi. Shuning uchun ularni hujjatdagi tartibda bajaring.

## Qanday ishlatiladi

- Har bir testdagi qadamlarni ketma-ket bajaring va "Kutilgan natija" bilan solishtiring.
- Natija mos kelsa "☐ O'tdi", mos kelmasa "☐ O'tmadi" ni belgilang. Izohga nima ko'rganingizni yozing: raqam, xato matni, sahifa nomi.
- Xato topilsa, ekran rasmini oling va test kodini yozib qo'ying (masalan, T-OMB-04). Tuzatishda shu kod bo'yicha qidiriladi.
- "Oldin bajarilgan bo'lishi kerak" qatoridagi test hali o'tmagan bo'lsa, avval o'shani bajaring.
- Hamma narsa faqat lokal bazada (localhost, DBTHSN-IPACK) tekshiriladi.

## Tayyorgarlik (bir marta)

1. **Zaxira nusxa.** SQL Server Management Studio'da DBTHSN-IPACK bazasining zaxira nusxasini oling (Tasks → Back Up...), masalan `DBTHSN-IPACK_before_UAT.bak`. Testlardan keyin bazani shu holatga qaytarish mumkin bo'ladi.
2. **Oxirgi kod.** Visual Studio'da `master` branch'ini oling (git pull), loyihani Debug rejimida build qiling va ishga tushiring.
3. **Migratsiyalar.** `Docs/Audit-2026-10/INTEGRATION.md` dagi "Kod o'rnatilishidan OLDIN" ro'yxatidagi skriptlar lokal bazada bajarilganini tekshiring.
4. **Brauzer.** Testlarni Chrome yoki Edge'da bajaring. Xavfsizlik testlari uchun ikkinchi brauzer yoki "Inkognito" oynasi kerak bo'ladi.
5. **Demo foydalanuvchilar.** Admin bilan kirib, T-CORE testlarida ko'rsatilgandek `demo_admin`, `demo_omborchi` va `demo_sotuv` foydalanuvchilarini yarating. Parolni o'zingiz tanlang va faqat o'zingiz biling.

## Umumiy demo ma'lumotlar

Hamma bo'limlar bir xil demo nomlardan foydalanadi. Ular DEMO- prefiksi bilan boshlanadi, shuning uchun keyin ularni oson topib o'chirish mumkin.

| Turi | Nomi | Asosiy qiymatlar |
|---|---|---|
| Ta'minotchi | DEMO Yog'och MChJ | INN 300000001, Toshkent |
| Xomashyo | DEMO-XOM-01 Qayin taxta 18 mm | m², 85 000 so'm/m², QQS 12% |
| Xomashyo | DEMO-XOM-02 Mebel shurupi 4x30 | dona, 120 so'm |
| Tayyor mahsulot | DEMO-TM-01 Ofis stoli 1200x600 | dona; norma: 0,9 m² taxta + 24 shurup |
| Mijoz | DEMO Mebel Savdo MChJ | INN 300000002 |
| Ekspeditor | DEMO Trans Logistik | yuk mashinasi |
| Xodim (oylik) | DEMO Aliyev Anvar | mavjud lavozimlardan biri |
| Xodim (ishbay) | DEMO Karimov Botir | sex xodimi |
| Foydalanuvchilar | demo_admin, demo_omborchi, demo_sotuv | Admin; faqat ombor; faqat CRM |

Asosiy miqdorlar:
- **Xarid:** 500 m² taxta va 20 000 dona shurup.
- **Ishlab chiqarish:** 100 ta stol.
- **Sotuv:** 60 ta stol, har biri 1 450 000 so'm.

## Bajarish tartibi

| № | Bo'lim | Taxminiy vaqt |
|---|---|---|
| 1 | Umumiy tizim, xavfsizlik va ruxsatlar (T-CORE) | 1–1,5 soat |
| 2 | Ta'minot: xarid (T-XAR) va ombor (T-OMB) | 2–3 soat |
| 3 | Ishlab chiqarish (T-MES) va tannarx (T-TAN) | 2 soat |
| 4 | Sotuv (T-CRM) va moliya (T-FIN) | 2–3 soat |
| 5 | Logistika (T-LOG), kadrlar (T-HRM), hujjatlar va topshiriqlar (T-DOC) | 2–3 soat |

Tekshiruv tugagach, oxiridagi "Yakuniy natijalar" jadvalini to'ldiring.
