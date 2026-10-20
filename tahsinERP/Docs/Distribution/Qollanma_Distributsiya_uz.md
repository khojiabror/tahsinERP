# tahsinERP Distributsiya — qisqa qo'llanma

Versiya: pilot (1-bosqich), 2026-10. Web: `/Distribution` bo'limi. Bot: kompaniyangizning Telegram boti.

## Boshlash (hamma uchun)

1. Rahbardan **ulanish kodini** oling (**Agentlar va jamoalar** sahifasida, mijozlar uchun — mijoz kartasida).
2. Havolani oching yoki botga `/start KOD` yuboring. Bot ismingiz va rolingizni yozadi va menyu chiqaradi.
3. Til: ERP profilingizdagi til (o'zbek / rus) — bot ham shu tilda.
4. Bot "⛔ Tizim litsenziyasi faol emas" desa — administratorga ayting (sizda xato yo'q). "⚠️ ERP bilan aloqa yo'q" — internetni tekshirib, 1–2 daqiqadan keyin qayta urining.

---

## Agent (bot + Mini App)

| Qachon | Nima qilasiz |
|---|---|
| Ertalab | **▶️ Kunni boshlash** → **📍 Joylashuvni yuborish** |
| | **🗺 Marshrut** — bugungi mijozlar: ✅ borildi, ⏳ kutmoqda, 🚫 buyurtmasiz; qarzi bo'lsa ko'rinadi |
| Do'konda | Mijozni tanlang (yoki **🔎 Mijoz** — nomi bilan qidirish) → **📍 Keldim** → Telegram'da **jonli joylashuv** yuboring. 200 m dan uzoqda bo'lsangiz qabul qilinmaydi |
| Buyurtma | **🛒 Buyurtma** — Mini App ochiladi: qidiruv yoki kategoriya, tavsiya (mijoz odatda oladigan tovarlar), **birlik** (dona / quti / poddon), miqdor → savat → yetkazish sanasi, izoh → mijoz **imzosi** → Yuborish. Buyurtma "Yangi" holatda ofisga tushadi |
| To'lov | **💵 To'lov** → summani yozing (masalan `250000`) → usul. Naqd — **sizning qo'lingizda** turadi, kassaga topshirguningizcha. Tugmani ikki marta bossangiz ham to'lov bitta |
| Buyurtma yo'q | **🚫 Buyurtmasiz** → sababni tanlang (majburiy) |
| Rasm | **📷 Foto** — tokcha / do'kon rasmi |
| Kechqurun | **💵 Kassa** → qo'lingizdagi naqd → **📤 Kassaga topshirish** → pulni kassirga bering. Kassir sanab tasdiqlaydi |
| Kun oxiri | **🏁 Kunni yakunlash** → joylashuv. Bot yakunni yozadi: tashriflar, buyurtmalar, yig'ilgan pul, qo'lingizdagi naqd |

Eslatmalar:
- Narx avtomatik (mijozning narx turi yoki maxsus narxi). Chegirma — faqat rolingizga ruxsat etilgan foizgacha va minimal narxdan past emas.
- Limitdan oshgan yoki muddati o'tgan qarzli mijoz buyurtmasi rahbar ruxsatisiz tasdiqlanmaydi — rahbarga botda xabar boradi.
- Siz faqat o'z mijozlaringizni ko'rasiz.
- Keyin qo'shiladi: kun boshida selfi, botdan yangi savdo nuqtasi qo'shish, Mini App'da kamera bilan shtrix-kod.

## Ekspeditor (1-bosqich)

Reys va marshrut 2-bosqichda. Hozir: botda **🔎 Mijoz** → **📍 Keldim** → yetkazishda **💵 To'lov** (naqd sizda qoladi) → kechqurun **📤 Kassaga topshirish**. "Yetkazildi"ni operator belgilaydi — unga ayting.

## Omborchi (web)

| Qadam | Qayerda |
|---|---|
| Tasdiqlangan buyurtmalar | **Buyurtmalar** → filtr: holat "Tasdiqlangan", omboringiz |
| Yig'ish | Buyurtma kartasi → **Yig'ishni boshlash** → **Yig'ish varaqasi** (chop) |
| Jo'natish | **Jo'natish** → har qator miqdori (hammasi yoki qisman) → saqlash. Ombordan FIFO bo'yicha chiqim, mijoz qarziga yoziladi. **Nakladnoy** chop |
| Qisman | Qolganini keyin jo'nating yoki operator **Qolganidan voz kechish** qiladi (sabab bilan) |
| Qoldiqlar | **Qoldiqlar** — haqiqiy / rezerv / mavjud; minimaldan pastlari |
| Qaytgan tovar | Qaytarish hujjatini operator kiritadi; yaroqli yoki brak omboriga kirim bo'ladi |

Xato jo'natishni bekor qilish — buyurtma kartasida (yetkazilmagan va qaytarishi yo'q bo'lsa). Keyin qo'shiladi: yangi tasdiqlangan buyurtma haqida botda xabar.

## Kassir (web)

| Qadam | Qayerda |
|---|---|
| Ofisdagi to'lov | **To'lovlar** → **To'lov** → mijoz, summa, usul (naqd / karta / Click / Payme / Uzum / o'tkazma), hisob (kassa yoki bank) |
| Agent naqdi | **To'lovlar → Agentlar naqdi va kassa** → agent topshirgan summa → pulni sanang → **Sanalgan** summani yozing → **Qabul qilish**. Farq (kamomad/ortiqcha) moliyaga alohida yoziladi, kassa qoldig'i haqiqiy pulga teng bo'ladi. Noto'g'ri bo'lsa — **Rad etish** |
| Xato to'lov | To'lov kartasida **Bekor qilish** (sabab). Kassaga topshirilgan agent puli bekor qilinmaydi |

## Rahbar (bot + web)

| Qayerda | Nima |
|---|---|
| Bot | Yirik buyurtma va limit oshishi haqida xabar → **✅ Tasdiqlash** (rahbar ruxsati) |
| Bot | Kassaga topshirish haqida xabar; **📊 Bugungi yakun**; har kuni 20:00 da avtomatik kunlik yakun |
| Web: Qarzlar | Mijozlar qarzi muddatlar bo'yicha (muddati kelmagan, 1–7, 8–30, 31–60, 60+), **akt sverki** (chop) |
| Web: Buyurtmalar | Holat, agent, sana bo'yicha; tannarx va foyda — faqat "tannarxni ko'rish" huquqi bilan |
| Web: Sozlamalar | Chegirma limiti (rol bo'yicha), minimal va yirik buyurtma, tashrif radiusi, omborlar |
| Web: Import | Boshlang'ich ma'lumot (tartib sahifada yozilgan): avval **ko'rib chiqish**, keyin **Import qilish** |
| Keyin qo'shiladi | Rahbar paneli va hisobotlar (savdo kesimlari, reja/fakt, Excel) |

## Operator (web, qisqa)

Buyurtmalar → **Tasdiqlash** (limit, rezerv, ombor avtomatik). Limit bloki bo'lsa — rahbar ruxsat beradi. Yetkazilgach — **Yetkazildi**. Bekor qilish — sabab bilan (faqat jo'natilmagan). Qaytarish — **Qaytarishlar → Qaytarish** (buyurtma bo'yicha yoki buyurtmasiz, yaroqli/brak, sabab).
