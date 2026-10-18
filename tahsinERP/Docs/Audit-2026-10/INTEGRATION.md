# Audit 2026-10 — integratsiya (barcha modul branch'lari birlashmasi)

Branch: `optimization/integration` (asos: `optimization/core` 19c07bd7). Push qilinmagan.
Har modul branch'i `--no-ff` bilan merge qilingan; sessiya keyin yangi commit qo'shsa, branch qayta merge qilinadi.

## Birlashtirilgan branch'lar

| Modul | Branch |
|---|---|
| SRM — Ombor + StockLedger | `claude/opt-srm-warehouse-838141` |
| SRM — Xarid | `claude/opt-srm-procurement-760fa4` |
| Finance | `claude/opt-finance-22068b` |
| MES + Engineering | `claude/opt-mes-904a4d` |
| Tannarx (MES'dan ajralgan) | `claude/happy-keller-da8d6b` |
| CRM | `claude/opt-crm-ca1873` |
| Logistika | `claude/opt-logistics-fe4abd` |
| HRM | `claude/opt-hrm-17b1d3` |
| Tasks + Documents | `claude/opt-tasks-docs-9aa641` |

Aniq commit'lar: `git log --first-parent optimization/integration`.

## Hal qilingan ziddiyatlar

- `tahsinERP.csproj` (SRM xarid, MES, Logistika bilan): ikki tomon qo'shgan yozuvlar birlashtirildi,
  birortasi o'chirgan yozuvlar tushirildi (base bilan uch tomonlama).
- `Services/ContractBalanceService.cs` (Finance FIN-B28 va SRM-B17/P05): SRM xarid versiyasi olindi — u FIN-B28
  mazmunini to'liq qamraydi (o'chirilgan invoyslar chiqariladi, `P_INVOICES.ContractID` hisobga olinadi) va qo'shimcha
  ravishda o'chirilgan buyurtmalarni chiqaradi, to'plam so'rov qiladi, CBM'ga faqat dona qatorlarni aylantiradi.
- Integratsiya tuzatishi: tasks-docs o'chirgan `assets/manual/*` va `Task*/Delete` view'larining csproj havolalari
  (197 ta) olib tashlandi; `Views/Log/Index.cshtml` repoga qo'shildi (`.gitignore`dagi `[Ll]og/` uni yashirgan edi —
  yangi clone/worktree'larda LogController sahifasi yo'q edi).

## Modullararo kelishuvlar

- **P_INVOICES.Amount endi QQS bilan** (SRM-B25, foydalanuvchi qarori). Finance (OpeningBalance,
  CounterpartyBalanceService) va Tannarx (PI reconcile qatorlar yig'indisi bilan) shunga moslashtirilgan.
- `FinancePostingService.PostPurchaseInvoice` — Finance versiyasi (SRM branch bu faylga tegmagan).

## Deploy tartibi (DB)

Baza: faqat lokal (localhost, DBTHSN-IPACK). Kod/izohlardagi 192.168.100.200 eskirgan — ishlatilmaydi.

Skriptlar `tahsinERP/Database_Migrations/`da. Hech biri avtomatik ishga tushirilmaydi. Har biridan oldin zaxira nusxa.

### 1. Kod o'rnatilishidan OLDIN (majburiy)
1. `MES_2026_10_PlanDate_ProductionID.sql` — EDMX'ga `PRODUCT_PRODUCTION.PlanDate`, `EXPENSES.ProductionID`,
   `INCOMES.ProductionID` qo'shilgan; ustunlarsiz bu jadvallarga har qanday EF so'rovi xato beradi.
2. `HRM_Koreshok_PermissionModule.sql` — aks holda Koreshok sahifasiga hech kim kira olmaydi.
3. `Finance_OpeningContractBalances_Schema.sql` — jadval yo'q bazada (kod endi uni o'zi yaratmaydi).
4. `HRM_Create_Allowances_TimesheetSettings.sql` — faqat jadvallar hali yo'q bazada (lokal DBTHSN-IPACK'da bor bo'lsa — kerak emas).
5. `Alter_COMPANIES_AddDirectorName.sql` — ixtiyoriy (ustun bo'lmasa eski qiymat ishlatiladi).

### 2. Indekslar (tekshirib, past yuklama vaqtida)
`Audit_2026_10_Indexes.sql`, `Audit_2026_10_SRM_Warehouse_Indexes.sql`, `MES_Audit_2026_10_Indexes.sql`,
`CRM_2026_10_Indexes.sql`, `Proposal_SRM_Procurement_Indexes.sql`, `Proposal_Tasks_Docs_Indexes.sql`.
Unique indekslardan oldin tegishli `*_Find` / `*DuplicateNumbers*` skriptlari bo'sh natija berishi kerak.

### 3. Kod o'rnatilgandan KEYIN — eski ma'lumotni tuzatish
Har birida avval `*_Find`/`*_Check`, keyin `*_Fix` (Fix'lar tranzaksiyada; ko'pchiligi standart holatda ROLLBACK).
Bog'liq tartib:
1. `DataFix_SRM_Warehouse_01_Check.sql` → `DataFix_SRM_Warehouse_02_Fix.sql` (ombor qoldig'i).
2. `DataFix_SRM_Procurement_01_InvoiceAmount_*` → **keyin** `DataFix_Finance_InvoiceMirrorAmounts_*`
   (Finance skripti invoysning yangi, QQS bilan Amount'ini oladi).
3. `DataFix_MES_01..03_*` → keyin `Tannarx_2026_10_ActualCosts_*` va ilovada "Fakt xarajatlarni yangilash".
4. Mustaqil: `DataFix_Finance_BalanceCategoryTypes_*`, `DataFix_Finance_OpeningContractTotals_*`,
   `DataFix_HRM_B01/B03/B06_*`, `DataFix_Logistics_B01/B02_*`, `DataFix_CRM_B01/B02/B04/B06_B10_*`,
   `DataFix_Docs_01/02_*`, `DataFix_SRM_Procurement_02_DuplicateNumbers_Find.sql`.

`Proposal_Logistics_Schema.sql` — faqat taklif (izohdagi buyruqlar); EDMX yangilashsiz qo'llanmasin.

## Tekshiruv holati
- MSBuild (Debug) — xatosiz. csproj ↔ fayllar mosligi asos bilan bir xil.
- Razor view'lar build vaqtida kompilyatsiya qilinmaydi (`MvcBuildViews=false`); modul sessiyalari o'z sahifalarini
  alohida tekshirgan. Integratsiyadan keyin asosiy sahifalarni brauzerda bir marta ochib chiqish tavsiya etiladi.

## Integratsiya sinovida topilgan muammo: `machineKey` yo'q
- `web.config`da `<machineKey>` yo'q (AutoGenerate). Ilova qayta ishga tushganda (rebuild, `web.config` o'zgarishi,
  ko'p view'ning birinchi kompilyatsiyasi, app pool recycle) kalitlar almashadi. Natijada ochiq turgan formalar
  `HttpAntiForgeryException` (login'da 500) beradi va barcha foydalanuvchilar login'dan chiqib ketadi.
- Lokal sinovda vaqtinchalik `machineKey` bilan menyudagi 109 sahifa xatosiz ochildi (2026-10-17).
- Tavsiya: server `web.config`iga (repoga emas) aniq `validationKey`/`decryptionKey` qo'yish yoki IIS app pool'da
  "Load User Profile = True" — kalitlar recycle'da saqlanadi.

## Lokal bazaga (DBTHSN-IPACK) allaqachon qo'llanganlar (sessiyalar xabari, 2026-10-17)
- CRM: `CURRENCY_RATES` jadvali, CRM indekslari; 22 ta sotuv invoysi finance'ga post qilindi —
  tafsilot `Database_Migrations/CRM_2026_10_APPLIED.md`.
- Tannarx: `TANNARX_FIN_CATEGORY_MAP` yaratildi, `SP_TANNARX_UPDATE_ACTUAL_COSTS` o'chirildi (kod chaqirmaydi).
- SRM Xarid (2026-10-18, zaxira `DBTHSN-IPACK_pre_SRM_Procurement_20261018.bak`): `Proposal_SRM_Procurement_Indexes.sql` —
  18 ta FK/filtr indeksi yaratildi (`sqlcmd -I` yoki skriptdagi `SET QUOTED_IDENTIFIER ON` shart). Raqam ustunlarida
  global UNIQUE indekslar oldindan bor edi. `DataFix_SRM_Procurement_01/02_*_Find` — bo'sh natija, Fix kerak emas.
- Qolgan skriptlarni ishga tushirishdan oldin har birining "allaqachon qo'llanganmi" tekshiruvini ko'ring.

## Core takliflari
- `optimization/core-logistics` (d4042860) merge qilindi: ReferenceDataCache'ga FContracts/TransportTypes,
  `Services/Common/ExcelCellReader`, `PostWaybillPayment` bool natija, `BaseController.SoftDeleteAsync<T>`.
