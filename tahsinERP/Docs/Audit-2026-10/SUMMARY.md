# Audit va optimizatsiya 2026-10 — yakuniy xulosa

Holat: 19.10.2026, `origin/master` 30640438 (main ham shu kodda; farq faqat LICENSE va README).
Baza: faqat lokal (localhost, DBTHSN-IPACK). Kod va izohlardagi 192.168.100.200 eskirgan — ishlatilmaydi.

## 1. Nima qilindi (qisqacha)

| Bosqich | Nima | Natija |
|---|---|---|
| Audit | 9 modul bo'yicha kod to'liq o'qildi | 429 topilma, hisobotlar shu papkada (`CORE.md`, `SRM_*.md`, `MES.md`, `FINANCE.md`, `CRM.md`, `LOGISTICS.md`, `HRM.md`, `TASKS_DOCUMENTS.md`), reja `00_PLAN.md` |
| 0. Xavfsizlik | Admin parolini almashtirish teshigi, anonim Koreshok, open redirect, GET bilan o'chirish, stored XSS, boshqa modul fayllarini yuklab olish (IDOR), SSRF stub, ruxsatsiz 22 controller | Yopildi (`[Permission]` filtri, `DocFileAccess`, `_SafeHtml`) |
| 1. Core | Ruxsat snapshot keshi (login'dan keyin ~210 so'rov → 2–3), `BaseController`, `ReferenceDataCache`, `DocumentNumberService`, `ExcelExport`, `ExcelCellReader`, `CurrencyRateService`, navbatli audit log, layout dietasi (3 ta jQuery → 1, pdfmake faqat bosilganda), statik kesh va gzip, soft-delete siyosati, o'lik kod (Home 10 endpoint, PermissionController va b.) | master'da |
| 2–5. Modullar | 8 ta parallel sessiya: bug'lar, tezlik, dublikatlar, o'lik kod, controller'larni servislarga bo'lish | master'da; har modul natijasi pastda |
| UAT | 147 ta test, demo ma'lumotlar bilan zanjirli ssenariy | `UAT/` (manba), `tahsinERP_UAT_2026-10-18_v2.pdf` (lokal) |
| Qo'llanma | iPack misolida 12 bobli foydalanuvchi qo'llanmasi | `../Qollanma/manba/` (manba), `../Qollanma/tahsinERP_Qollanma_iPack_2026-10.pdf` (lokal) |

Kod hajmi (9fca261a → master, Docs va eski qo'llanma rasmlarisiz): 794 fayl, +57 183 / −66 728 qator — tizim kichraydi va tezlashdi.

## 2. Modullar bo'yicha yakuniy holat

Raqamlar 18.10.2026 dagi sessiya hisobotlaridan (qilindi/jami); undan keyin yopilgan bandlar "Keyin yopildi" ustunida.

| Modul | Bug | Xavfs. | Perf | Keyin yopildi | Brauzerda |
|---|---|---|---|---|---|
| Core | 7/8 | 8/8 | 12/20 | csrf.js (Pace.js o'rami — 28 ta AJAX POST 500 berardi) | qisman |
| SRM Ombor | 24/27 | 5/6 | 13/20 | B12, B14, B19, S05, P06, P07, P11, P14, M01, M03, M04, M05, o'lik kod | yo'q |
| SRM Xarid | 23/23 | 2/2 | 8/13 | P03, P08, P09, P12, D7, D13, D16, M01; o'chirilgan PO raqami qayta berilishi (UAT'da topildi) | qisman |
| MES + Tannarx | 14/15 | 5/5 | 22/26 | B11 (StockCostService orqali FIFO), P15, P23, D#17 | qisman |
| Finance | 18/26 | 1/3 | 13/18 | B15–B29, B16 (hisob kesimidagi opening), B27, M02–M04, P12, P16, D11–D22; bekor qilingan invoyslar qarzga kirmaydi; akt-sverki, qarz, balans va opening 5 sanada 0,00 farq bilan mos | yo'q |
| CRM | 20/21 | 4/5 | 9/9 | M01, M02, B18, S03, D14/D16/D23; UAT'da 4 xato (TTNK saqlanmasdi va b.) | ha (T-CRM-01..15 o'tdi) |
| Logistika | 19/20 | 4/4 | 16/16 | B05 (TariffID), B06, B13 (so'mga), D02, M01, M02, view ruxsat wrapper'lari | ha (40 sahifa) |
| HRM | 21/21 | 6/6 | 17/17 | B09, B15, B17, B18, P15, D13, D15/D16 | yo'q |
| Tasks + Docs | 21/23 | 8/8 | 20/24 | IsFinal/ColorHex, Word shablonlar bazada, yagona HTML→DOCX | qisman |

## 3. Qabul qilingan biznes qarorlari

- Xarid talabnomasi: bitta tasdiq (Draft → Submitted → Approve/Reject sabab bilan).
- Ombor chiqimi har doim sof FIFO, lot tanlanmaydi; chiqim tannarxi FIFO lot narxi, QQSsiz; qo'lda narx yo'q.
- Boshqa birlikdagi qator qoldiq birligiga o'giriladi, konversiya bo'lmasa rad etiladi.
- Kirimni faqat ombor egasi yoki Admin o'chiradi.
- P_INVOICES summasi QQS bilan saqlanadi.
- Valyuta so'mga hujjat sanasidagi CBU kursi bilan (CURRENCY_RATES, qo'lda tuzatish mumkin).
- Ish haqi tasdiqlanganda Finance'ga avtomatik chiqim yoziladi.
- Bekor qilingan sotuv invoysi qarz va akt-sverkiga kirmaydi.
- CustomerBase ≠ Customer, SupplierBase ≠ Supplier — birlashtirilmaydi.
- O'chirilgan ma'lumotnoma (ekspeditor, valyuta, ta'minotchi) eski hujjatlarda bog'lanish bilan qoladi; ombor/moliya hujjatlari DeletedRecords orqali tiklanmaydi.
- Topshiriqni "Delete" qilish uni CANCELLED holatiga o'tkazadi.

## 4. Lokal bazaga qo'llangan sxema o'zgarishlari

Hammasi zaxira nusxadan keyin, EDMX yangilangan. Skriptlar `Database_Migrations/` da.

| Modul | O'zgarish |
|---|---|
| MES | `PRODUCT_PRODUCTION.PlanDate`, `EXPENSES.ProductionID`, `INCOMES.ProductionID` |
| SRM Ombor | `WAREHOUSES.WarehouseType`, `INVENTORY_ADJUSTMENTS` jadvali, `INCOME_PRODUCTS.VATRate`, `INCOMES.Currency` olib tashlandi, `*_PRODUCTS.TotalPrice` hisoblanadigan ustun |
| CRM | `CURRENCY_RATES` jadvali; `SALES_INVOICES.CurrencyCode`, `S_ORDERS.Currency` olib tashlandi |
| Logistika | `F_WAYBILLS.TariffID` |
| Finance | `FIN_OPENING_CONTRACT_BALANCES` sxemasi, hisob kesimidagi opening, FinRecurringObligations ruxsat moduli |
| HRM | Koreshok ruxsat moduli (ID 477), HR sozlamalari jadvallari |
| Tasks + Docs | `COMPANIES.DirectorName`, task ref `IsFinal`/`ColorHex`, STD shabloni `DOC_TEMPLATES` da |
| Tannarx | `TANNARX_FIN_CATEGORY_MAP` |
| Hammasi | Audit indekslari (Core 46 ta, SRM, MES, CRM, Logistika, Tasks) |

Data-fix'lar: har modulda `*_Find`/`*_Check` → `*_Fix`; lokal bazada bajarilganlari sessiya hisobotlarida (`CRM_2026_10_APPLIED.md`, `INTEGRATION.md`).

## 5. Ochiq qolgan ishlar

1. **Brauzerda to'liq tekshiruv** — UAT hujjati bo'yicha (CRM va Logistika qisman bajardi). Demo foydalanuvchilar (demo_admin, demo_omborchi, demo_sotuv) hali yaratilmagan; DEMO ma'lumotlar lokal bazada qoldirilgan.
2. **`web.config`'da `machineKey` yo'q** — bir vaqtda ikki IIS Express ishlasa yoki qayta ishga tushirilsa, login 500 va to'satdan logout.
3. **Core qoldiqlari:** TempData cookie provider (P05), Log sahifasi paging (P09), Reports/License N+1 (P12–P16), umumiy DataTables/JS va Create/Edit/Delete partial'lari, yangi rol uchun standart ruxsatlar, umumiy mahsulot qidiruvi (SRM-P05), `SoftDeleteReferenceService` set-based (SRM-P17), layout ombor badge'i (SRM-P20).
4. **Moliya:** dashboardlar valyutani so'mga o'girmaydi; P&L kassa usulida; ta'minotchi va doimiy xarajat to'lovi saqlangach tahrirlanmaydi.
5. **Sotuv:** buyurtmadagi "Yetkazilgan" ustuni avtomatik to'lmaydi; invoysda to'lov muddati maydoni yo'q. SF/TTNK EDO integratsiyasi (Didox/Faktura.uz) hozircha ichki.
6. **Davrni yopish (qulflash)** funksiyasi yo'q — qo'llanmaning 11-bobidagi oylik yopish tartibi bo'yicha ishlang.
7. **Menyuda yo'q sahifalar:** `/CurrencyRates`, `/Shop`, `/HSCode`.
8. **Zaxira nusxa va log tozalash** tizimda yo'q — SQL Server vositalari bilan.

## 6. Hujjatlar xaritasi

| Fayl | Nima uchun |
|---|---|
| `00_PLAN.md` | Tasdiqlangan optimizatsiya rejasi |
| `CORE.md`, `SRM_PROCUREMENT.md`, `SRM_WAREHOUSE.md`, `MES.md`, `FINANCE.md`, `CRM.md`, `LOGISTICS.md`, `HRM.md`, `TASKS_DOCUMENTS.md` | Dastlabki audit topilmalari (ID'lar bilan) |
| `FINANCE_NATIJA.md`, `TASKS_DOCUMENTS_RESULT.md` | Modul natija hisobotlari |
| `INTEGRATION.md` | Branch'lar birlashmasi, deploy va migratsiya tartibi |
| `UAT/*.md` | 147 ta qabul testi (manba) |
| `../Qollanma/manba/*.md` | iPack qo'llanmasi (manba) |
| `SUMMARY.md` | Shu fayl |
