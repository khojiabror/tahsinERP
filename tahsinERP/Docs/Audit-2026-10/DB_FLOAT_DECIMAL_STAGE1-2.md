# float → decimal: 1- va 2-bosqich hisoboti (2026-10-19)

Reja: [DB_FLOAT_DECIMAL_PLAN.md](DB_FLOAT_DECIMAL_PLAN.md). Branch `claude/float-decimal-logistics` (master `1054300f` ustidan).
3–5-bosqichlar bu ishga kirmaydi.

## 1-bosqich: solishtiruv harness'i

`Database/tools/RegressionProbe/` — `RegressionProbe.cs` + `Run-RegressionProbe.ps1` (Roslyn csc bilan `bin/` ga qarshi kompilyatsiya).
Faqat o'qiydi. 193 bo'lim (IPACK):

| Guruh | Nima yoziladi |
|---|---|
| balance/, finance/ | avvalgi BalanceProbe: Balans (3 sana), passiv/aktiv tafsiloti, kategoriya harakatlari, ReportsHub, Liabilities, FinancialOverview, P&L, hisob qoldiqlari |
| tannarx/ | `TannarxAnalyticsService` (MonthSources, Dashboard, Report), fakt tannarx dvigateli `TannarxActualCostEngine.CalculateMonthAsync` (yozmaydi), `TannarxController.Index` — 2025-12, 2026-03, 2026-05 |
| stock/ | har ombor: `StockLedger.GetOnHand`, `WarehouseMonthlyReportService` (2 oy × miqdor/summa) |
| crm/, srm/, counterparty/ | `CRMDashboardController.Index`, `SRMDashboardController.Index` (standart va 2025- davr), `CounterpartyBalanceService.Suppliers/Customers` |
| logistics/ | LogisticsDashboard (Index, MonthlyReport, LanePerformance, Reconciliation, ForwarderComparison), Waybill/WaybillPayment/FContract/FContractTariff/Forwarder ro'yxat va tafsilotlari, `WaybillLedgerService` balans/paid lookup, `LogisticsAnalyticsService.GetTotals` |

Controller'lar soxta `HttpContext` (Developer foydalanuvchi) bilan chaqiriladi; `ViewData.Model` va `ViewBag` yoziladi.
`compare` rejimi raqamlarni decimal sifatida solishtiradi: `≤ tolerans` — yaxlitlash (ro'yxat), katta — xato (chiqish kodi 1).
Ikki marta "oldin" ishga tushirish: 0 farq (deterministik).

"Oldin" JSON (master build, IPACK, 010 holati): umumiy eslatmalar papkasida
`float-decimal/ipack_before_stage2_master-1054300f.json` (biznes ma'lumoti bo'lgani uchun repoga qo'shilmadi).

## 2-bosqich: moliya-logistika guruhi

### Migratsiya `011_Logistics_FloatToDecimal.sql`

| Ustun | Tur |
|---|---|
| F_WAYBILLS.Amount, F_WAYBILL_PAYMENTS.Payment_Amount, F_WAYBILL_PAYMENT_SPLITS.Amount, F_CONTRACTS.Amount, F_CONTRACT_TARIFFS.MinimumCharge | decimal(19,4) |
| F_WAYBILLS.Rate, F_CONTRACT_TARIFFS.Rate | decimal(19,6) |
| F_WAYBILLS.CBM | decimal(18,6) |
| F_WAYBILLS.GrWeight, NetWeight, DistanceKm | decimal(18,4) |

- Ustunni o'z ichiga olgan indekslar (6 ta, hammasi `INCLUDE`) `sys.*` dan skriptlanib qayta yaratiladi; default'lar ham
  (bu ustunlarda yo'q); `_WA_Sys_*` statistikalar o'chiriladi; `V_TANNARX_ACTUAL_*_TRANSPORT` — `sp_refreshview`
  (ular `CAST(... AS decimal/float)` qiladi, natija turi o'zgarmaydi).
- Qiymat sig'masa yoki kasr xonasi yo'qolsa `THROW 50011` — jim yaxlitlash yo'q. IPACK: barcha qiymatlar ≤ 2 xona, max 1e9.
- Idempotent: qayta ishga tushirsa "o'zgarish yo'q". Indekslar IPACK bilan aynan bir xil (tekshirildi).

### Kod

- EDMX (SSDL `decimal` + CSDL `Decimal`, Precision/Scale), `Models/F_*.cs` `double` → `decimal`.
- `LogisticsMoney` — to'liq decimal API (`UzsConverter` allaqachon decimal edi; double↔decimal aylanishi olib tashlandi).
- `WaybillLedgerService` (paid lookup, `StatusOf`, `Allocate`, `ForwarderBalance`), `LogisticsAnalyticsService`, `FreightCalculator`,
  `LogisticsMonthlyReportService`, logistika view model'lari — decimal. `OTIFPercentage` double qoldi (foiz, pul emas).
- Moliya chegarasi: `FinLiabilities.LogisticsResult.WaybillTotal` decimal (avval decimal→double→decimal edi),
  `FinCounterpartyLookupService`, `ReconciliationService`, `FinancePostingService`, `TannarxActualCostEngine` — ortiqcha cast'lar olib tashlandi.
- `ExcelCellReader.Decimal()` — Excel importi pul/miqdorni double "dumi"siz o'qiydi.
- Razor (runtime kompilyatsiya — build ko'rmaydi): `ViewBag.X as double?` / `as IDictionary<int,double>` → decimal
  (aks holda jim 0/null bo'lardi: Waybill/Details, WaybillPayment/Details, WaybillPayment/Index); tahrir formalarida
  `TextBoxFor(..., "{0:0.######}")` — `4500000.0000` emas `4500000`; FContract/Index `@item.Amount` formatlandi.

### Xatti-harakat o'zgarishlari (ataylab, hujjatlangan)

1. `Allocate`: ortiqcha summa teng bo'linganda ulush 4 xonaga yaxlitlanadi, qoldiq oxirgi yo'l varaqasiga — split'lar jami
   to'lovga aynan teng (avval double 1/3 lar saqlanardi; decimal(19,4) da EF kesib, jami 0.0001 ga kam chiqardi).
2. `FreightCalculator`: natija 4 xonaga yaxlitlanadi (ustun scale'i; EF6 ortiqcha xonani yaxlitlamay kesadi).
3. `ForwarderController.Details`: so'nggi yo'l varaqalari/to'lovlar tartibiga `ID` tie-breaker (bir sana+summa bo'lgan
   to'lovlar indeks qayta qurilgach o'rin almashdi — avval ham kafolatlanmagan edi).
4. `DisplayFor(Amount)` (FContract Details/Delete) MVC decimal shabloni bilan `0.00` formatda ko'rsatiladi.
5. Excel importida scale'dan ko'p kasr xonasi bo'lsa (masalan CBM 7 xona) EF ortiqchasini kesadi.

**Deploy:** yangi build eski (float) sxemada ishlamaydi va aksincha — migratsiya va build bir vaqtda.

## Tekshiruv

| Tekshiruv | Natija |
|---|---|
| Build (Debug) | xatosiz |
| IPACK: oldin (master) ↔ keyin (011 + branch) | **0 farq**; 6 yaxlitlash farqi, har biri 1·10⁻¹⁰ so'm — `AvgCostPerKm` (double vs decimal bo'lish) |
| Balans, Tannarx, ombor, CRM/SRM bo'limlari | 0 farq |
| Ko'p valyutali sinov nusxasi (F2CUR: 58 USD + 17 RUB yo'l varaqasi, 23 USD to'lov, split/tarif USD, kunlik kurslar) | **0 farq**; 136 yaxlitlash farqi, max 1·10⁻⁷ so'm (float shovqini yo'qoldi) |
| `New-TenantDb` toza baza / `-Demo` | ikkalasi chiqish kodi 0; ustun turlari EDMX bilan bir xil; demo bazada harness 0 xato |
| Smoke (IIS Express, F2CUR nusxasi) | 54 sahifa: logistika (dashboard 6, Waybill/Payment/FContract/Tariff/Forwarder ro'yxat/Details/Edit/Delete/Create), Balans, P&L, Cashflow, Akt-sverka, Tannarx — hammasi 200, `.0000`/NaN yo'q |
| Edit formalarini o'zgarishsiz qayta yuborish (Waybill ×2, FContract, Tariff, Payment) | saqlandi, summalar o'zgarmadi |
| `Allocate` 3 ga bo'linmaydigan summa | jami aynan teng, ≤ 4 xona |

IPACK'dagi logistika ma'lumoti hammasi UZS — valyuta yo'li shuning uchun alohida sinov nusxasida tekshirildi.
Lokal zaxira: `MSSQL\Backup\DBTHSN-IPACK_pre_011_float_decimal_20261019.bak` (RESTORE VERIFYONLY OK).

## Keyingi bosqichlar uchun

`Run-RegressionProbe.ps1 -Out before.json` (migratsiyadan oldin, joriy build) → migratsiya + build →
`-Out after.json` → `-Compare before.json, after.json`. Eski build'ni saqlash: probe ishchi papkasi
(`%TEMP%\tahsinERP-probe\<vaqt>`) ichidagi `RegressionProbe.exe`ni `TAHSINERP_DB` bilan boshqa nusxaga qarshi ishlatish mumkin.
