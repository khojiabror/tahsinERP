# 10. Iqtisodiyot: reja tannarx/narx va fakt

Panel → **Iqtisodiyot** (faqat Owner). `tahsinERP_Tannarx_Narx_v2.xlsx` modelining elektron varianti: reja shu yerda saqlanadi,
faktni tizim obunalar, to'lovlar va kiritilgan xarajatlardan o'zi hisoblaydi.

## Sahifalar

| Sahifa | Nima |
|---|---|
| `/Economics` — Reja va fakt | Tanlangan oy: to'layotgan firmalar, MRR, xarajatlar, foyda, 1 firma/1 user tannarxi; reja (moslashuvchan byudjet) va fakt farqi; zararsizlik; 6 oylik trend |
| `/Economics/Plan` — Tannarx va narx | 1 firma va 1 user tannarxi (rejadagi hajmda), simulyatsiya (1/5/10/25/50/100 firma), paketlar: tannarx → narx, bozor bilan solishtirish, «Tarifga qo'yish» |
| `/Economics/Expenses` | Haqiqiy xarajatlarni kiritish (so'm yoki $ + kurs), toifa bo'yicha reja/fakt, o'tgan oyni nusxalash |
| `/Economics/Competitors` | Raqobatchilar tariflari (Bito, Linko, Hippo, MoySklad, Smartup, Sales Doctor, 1C, Odoo, SAP B1), bozor xulosasi |
| `/Economics/Settings` | Model farazlari, doimiy xarajatlar rejasi, paketlar sotuv rejasi/ustama/maqsadli margin, modul og'irliklari |

## Hisob usuli

1. **COGS** (to'g'ridan-to'g'ri tannarx) — mijozga xizmat: server, zaxira/bot, platforma, support. Gross margin shundan (SaaS 60–80%, starter 40–55%).
2. **OPEX** — dasturlash (R&D), sotuv-marketing (S&M), boshqaruv (G&A); firmalar soniga bog'liq emas.
3. **Pog'onali xarajat**: serverlar = max(min, ⌈N ÷ sig'im⌉), support xodimlari ham shunday.
4. **Paketlarga taqsimlash (ABC)**: yuk = modul ulushi × foydalanuvchi; support va OPEX yuk bo'yicha, infratuzilma har firmaga teng.
5. **Narx** = to'liq tannarx + sof foyda (ustama) + foyda solig'i + tushumdan ajratmalar (aylanma soliq, bank), ustiga QQS (tarifdan). Tavsiya QQS bilan, 10 000 so'mga yuqoriga yaxlitlanadi.
6. **Zararsizlik**: reja aralashmasi bo'yicha o'rtacha narxda foyda ≥ 0 bo'ladigan eng kichik N (pog'onali xarajat bilan), 1 000 gacha.

## Fakt

- **Firmalar / MRR**: oy ichida amalda bo'lgan pullik paket obunalari (TRIAL va qo'shimcha xizmatlarsiz), tarifning joriy narxi bo'yicha (yillik ÷ 12), QQSsiz.
- **Kassa**: storno qilinmagan to'lovlar, hisob-faktura QQS ulushi chiqarilgan.
- **Xarajatlar**: `Expenses` jadvali; toifa Infra/Support — COGS, qolgani OPEX.
- **Reja ustuni** — moslashuvchan byudjet: model haqiqiy firmalar soniga qayta hisoblanadi.

## Ma'lumotlar

Jadvallar: `EconomicsSettings` (1 qator), `PlannedCosts`, `ModuleWeights`, `PlanEconomics`, `Competitors`, `Expenses` (migratsiya `Economics`).
Boshlang'ich qiymatlar Excel modelidan ($ × 12 700). Pul — tiyin. O'zgarishlar audit jurnaliga yoziladi.
Kod: `Services/EconomicsCalculator.cs` (sof hisob, testlar: `EconomicsTests.cs`), `Services/EconomicsService.cs`, `Pages/Economics/`.
