# DLOC va FIFO — foydalanuvchi qo'llanmasi

**Versiya:** 1.0  
**Sana:** 2026-05-20  
**Tizim:** tahsinERP / iPack ERP — SRM moduli

---

## 1. Kirish

- **Nima o'zgardi:** Omborda mahsulot endi faqat «umumiy qoldiq» emas — **joy (DLOC) + lot + kirim sanasi** bo'yicha saqlanadi.
- **FIFO:** Chiqimda avval **eng eski kirim sanasi** bo'lgan lot ishlatiladi (`Comment`da `IncomeID` yozish shart emas).
- **Min/Max:** Har bir joy (yacheyka) uchun minimal va maksimal sig'im; ortiqcha kirim **qabul qilinmaydi**.

---

## 2. Kim nima qiladi

| Rol | Vazifalar |
|-----|-----------|
| **Admin** | DB migration, omborlar, FIFO policy, foydalanuvchi huquqlari |
| **Ombor menejeri** | DLOC yaratish, Min/Max, zona qoidalari |
| **Ombor xodimi** | Kirim (DLOC tanlash), chiqim, transfer |
| **Menejer** | Dashboard, violation va past qoldiq hisobotlari |

---

## 3. O'rnatish (Admin)

1. SQL skriptni ishga tushiring: `Scripts/20260519_DLOC_FIFO_Core_Migration.sql`
2. Ilovani qayta ishga tushiring (IIS Express / IIS).
3. Onboarding checklist (ushbu hujjat oxirida) bo'yicha sinovlar o'tkazing.

---

## 4. DLOC va Min/Max sozlash

**Yo'l:** `Sozlamalar` → **Ombor joylashuvlari (DLOC)** (`/WarehouseLocations`)

**Yangi DLOC qo'shish:**

1. **Joylashuv kodi** — masalan `A-01-01` (noaniq bo'lmasin).
2. **Nomi** — masalan «Xomashyo zona 1».
3. **Min sig'im** — pastga tushganda ogohlantirish.
4. **Max sig'im** — shu joyga maksimal nechta birlik sig'adi.
5. **Aralashtirish** — boshqa lot / boshqa mahsulot ruxsat yoki yo'q.
6. **Faol** — faqat faol DLOC kirimda tanlanadi.

**Muhim:** Min va Max o'zlari kirim/chiqim bilan o'zgarmaydi. O'zgaradigan — shu joydagi **qoldiq**.

**Misol (Max = 500):**

- Qoldiq 200, +300 kirim → 500 → **OK**
- Qoldiq 200, +301 kirim → **Rad etiladi** («Sig'im oshdi»)

---

## 5. Birinchi marta ishlatish ketma-ketligi

```
1) Admin: migration + deploy
2) Menejer: Production va Sales omborlari uchun DLOC + Min/Max
3) Xodim: Kirim → har mahsulot qatori uchun DLOC → saqlash
4) Xodim: Chiqim → tizim eng eski lotni FIFO bo'yicha ajratadi
5) Xodim: Production → Sales transfer → Sales omborida qabul (har qator DLOC)
6) Menejer: Inventory hisobotlari → FIFO / MIN ogohlantirishlari
```

---

## 6. Kirim (Income) — qadam-baqadam

1. `SRM` → `Kirimlar` → `Yangi kirim`.
2. Ombor, invoys, sana — odatdagidek.
3. **Kirim mahsulotlari** jadvalida har qator uchun **DLOC** tanlang (ombor DLOC ga ega bo'lsa majburiy).
4. DLOC tanlangach: **Joriy qoldiq | Min | Max | Bo'sh joy** ko'rinadi.
5. Miqdor kiriting → **Saqlash**.
6. Agar `joriy + kirim > Max` → xato, saqlanmaydi.
7. Muvaffaqiyatli kirimda **lot** yaratiladi; FIFO shu lotning kirim sanasidan hisoblanadi.

**Eslatma:** Agar omborda hali DLOC yo'q bo'lsa, avval `WarehouseLocations` dan joy yarating va sahifani **Ctrl+F5** bilan yangilang.

---

## 7. Kirim qabul qilish (transfer va kutayotgan kirimlar)

1. `Kirimlar` → hujjat **Tafsilotlari**.
2. MRP foydalanuvchi **Qabul qilish** tugmasini ko'radi.
3. Har mahsulot qatori uchun **DLOC** tanlang (jadval ichida).
4. **Qabul qilish** — sotuv ombori qoldig'i yangilanadi.

---

## 8. Chiqim (Expense) — qadam-baqadam

1. `SRM` → `Chiqimlar` → `Yangi chiqim`.
2. Manba ombor va mahsulotni tanlang.
3. DLOC yoqilgan omborda tizim **eng eski lot** bo'yicha FIFO ajratadi.
4. Miqdorni kiriting → saqlang.
5. DLOC qoldig'i **Min**dan past bo'lsa — dashboardda ogohlantirish (chiqim odatda to'xtatilmaydi).

---

## 9. Production → Sales transfer

1. `Ishlab chiqarishdan sotuvga` menyusi.
2. Mahsulot, miqdor va **sifat nazoratidan o'tgan lot** tanlang.
3. Ishlab chiqarish omborida DLOC bo'lsa chiqim **FIFO** orqali lotlardan ayiriladi.
4. Sotuv omborida kirim hujjati yaratiladi; MRP **Qabul qilish**da har qator uchun **DLOC** tanlaydi.

---

## 10. Ombor qoldig'ini ko'rish

- `SRM` → `Ombor qoldig'i` — umumiy qoldiq (`PRODUCT_STOCKS`).
- **Joylar bo'yicha qoldiq** tugmasi — DLOC, lot, Min/Max bilan (`/Inventory/IndexDloc`).
- `Inventory hisobotlari` → `Dashboard` — FIFO buzilishlari, DLOC MIN/MAX ogohlantirishlari.

---

## 11. Tez-tez uchraydigan xatolar

| Xabar | Sabab | Nima qilish |
|-------|--------|-------------|
| DLOC tanlanmagan | Joy tanlanmagan | Har qator uchun DLOC tanlang |
| Sig'im oshib ketdi | joriy + kirim > Max | Boshqa DLOC yoki kamroq miqdor |
| FIFO qoidasiga zid | Yangi lotdan chiqish | Avval eski lotdan chiqim |
| Boshqa mahsulot aralashmaydi | AllowMixedProducts = yo'q | Boshqa DLOC ishlating |

---

## 12. Eski ma'lumotlar

- `PRODUCT_STOCKS` umumiy qoldiq sifatida saqlanadi (rollup).
- Yangi kirim/chiqimlar DLOC+lot orqali; `Comment`da `IncomeID:123` yozish **talab qilinmaydi**.

---

## 13. Qo'llab-quvvatlash

- Savollar: ombor menejeri / IT admin.
- Hujjat: `Docs/DLOC_FIFO_User_Guide.pdf` (versiya sarlavhada).

---

## Onboarding checklist

**Kim:** Admin (IT) + Ombor menejeri  
**Qachon:** DB migration va deploy dan keyin, birinchi kirim/chiqimga o'tishdan oldin.

| # | Vazifa | Kim | Tayyor? |
|---|--------|-----|---------|
| 1 | DB migration ishga tushirildi | Admin | ☐ |
| 2 | Production ombori aniqlangan | Admin | ☐ |
| 3 | Sales ombori aniqlangan | Admin | ☐ |
| 4 | ProductionWH uchun DLOC yaratildi | Ombor menejeri | ☐ |
| 5 | SalesWH uchun DLOC yaratildi | Ombor menejeri | ☐ |
| 6 | Har DLOC uchun Min/Max to'g'ri kiritildi | Ombor menejeri | ☐ |
| 7 | AllowMixedLots / AllowMixedProducts qaror qilindi | Ombor menejeri | ☐ |
| 8 | FIFO policy (strict) yoqildi | Admin | ☐ |
| 9 | Sinov kirim: 1 mahsulot, 1 DLOC, capacity OK | Ombor xodimi | ☐ |
| 10 | Sinov chiqim: FIFO eng eski lot | Ombor xodimi | ☐ |
| 11 | Sinov: Max oshiradigan kirim bloklandi | Ombor xodimi | ☐ |
| 12 | Production → Sales transfer sinovi | Ombor xodimi | ☐ |
| 13 | Inventory Dashboard: violation / below MIN | Menejer | ☐ |
| 14 | Qo'llanma (PDF) xodimlarga tarqatildi | Admin | ☐ |

---

*© tahsinERP — SRM DLOC/FIFO moduli*
