# float → decimal: ichki hisoblar (ombor/FIFO, QQS) — hisobot (2026-10-19)

Oldingi bosqichlar: [DB_FLOAT_DECIMAL_STAGE1-2.md](DB_FLOAT_DECIMAL_STAGE1-2.md), [DB_FLOAT_DECIMAL_STAGE3-5.md](DB_FLOAT_DECIMAL_STAGE3-5.md).
3–5-bosqichdan keyin sxemada float qolmagan edi, lekin ombor/FIFO va QQS quvurlari `double` bo'lib, decimal faqat entity
chegarasida (`(double)`/`(decimal)` konversiyalari) edi. Bu ishda o'sha ichki hisoblar decimal'ga o'tkazildi.

Branch: `claude/float-decimal-internal` (master `791226cb` dan; `claude/float-decimal-logistics` allaqachon master'da edi),
keyin master `de797c0a` (Distributsiya S2) merge qilingan. **Sxema o'zgarmadi** — migratsiya kerak bo'lmadi (018 raqami
distributsiyada band).

## O'zgarishlar (modul bo'yicha, har biri alohida commit)

### 1. Ombor / FIFO / DLOC (`280cacf1`)
- `StockLedger`: `Adjust`, `Increase`, `Decrease`, `GetOnHand` (ikkala variant), `GetOnHandForUpdate`, `ToStockUnit`,
  `ReceiveLine`, `IssueLine(Async)`, `ReverseIssueLine` — `decimal`. `Epsilon = 0.000001m`. `InsufficientStockException`
  `Requested`/`Available` decimal.
  - `AdjustSql`: `@total/@new FLOAT` → `DECIMAL(38,6)` (qoldiq ustuni `decimal(18,6)`; avval float→decimal(18,6) yozishda
    yaxlitlanardi — natija bir xil).
  - raw SQL'dagi `CAST(... AS FLOAT)` olib tashlandi (`GetOnHand` → `DECIMAL(38,6)`, `ReverseIncomeReceipt`, `ToStockUnit`).
- `StockCostService`: `IssuedLineUnitCost`, `FifoLayerCost`, `StoredUnitCost(decimal)` — decimal. Lot tannarxi va FIFO qatlam
  narxi endi SQL'da `CAST AS FLOAT` bilan emas, C# decimal'da hisoblanadi (`Amount × PiecePrice / QtyReceived`,
  `PiecePrice` yoki `TotalPrice / Amount`) — SQL decimal bo'linmasi kasrni 6 xonaga kesgani uchun.
- `ExpenseStockIssueHelper`, `DlocFifo` (`InventoryValuationService.GetQty/GetPositiveStocks`, `LegacyDlocAssignmentService`,
  `UnassignedDlocStockRow`, `LocationLotStockLedger` dagi kastlar), `ProductionConsumptionService` (`producedQty`, `credit`,
  `ProductionRawMaterialRequirement`), `ProductionLotService` (lot tokenlari `decimal.TryParse`), `WarehouseFlowService`
  (`IncreaseProductionStock`, `ProductionTransferLine`, `DecreaseSalesStock`, `GetProductionStock`), `ExpenseDocumentService`,
  `InventoryAdjustmentService`, `SalesInvoiceService.GetSalesStockMap`, `BalanceInventoryValuation`.
- View model'lar: `ExpenseProductViewModel`, `ExpenseEditViewModel`, `ProductProductionViewModel`(+ bulk qatori),
  `BomConsumptionLineViewModel`/`ProductionExpenseLinkViewModel`, `TransferLineViewModel`, `AssignDlocPostLine`,
  `BalanceSheetInventoryBreakdownRow.Qty`.

### 2. QQS va xarid/sotuv hujjatlari (`0bb4e0a8`)
- `VatMath` (barcha funksiyalar, `VatLine`), `PContractVatHelper`, `SContractVatHelper`, `IncomeDocumentService.GrossTotal` — decimal.
  Yaxlitlash rejimlari o'zgarmagan (`Math.Round(x, 2)` — ToEven, `ApplyVat`/`RateFromGrossNet` — AwayFromZero).
- Servislar: `PContractService`, `POrderService`, `PInvoiceService`, `ProcurementLookups`, `SalesOrderService`, `SalesLookups`,
  `SalesInvoiceService`, `CrmNumber.Decimal` (double variantlari olib tashlandi).
- View model'lar: `PContract*`, `POrder*`, `PInvoice*`, `IncomeEditViewModel/IncomeProductViewModel`, `SContractProductViewModel`,
  `SOrder*` (`ContractProductQuantity`, jadval qatori).
- View'lar: `ViewBag.X as double?` / `ViewBag.ProductVatRates as Dictionary<int, double>` (decimal bo'lgach **jim 0/null** bo'lardi)
  → decimal: `PContracts/Details`, `PInvoices/Details|Edit`, `POrders/Details|Edit`, `SContract/Details`, `SOrder/Details|Delete`,
  `SOrder/Create|Edit` (`100d` → `100m`).

### 3. Kastlar, chegara konversiyalari
- Roslyn semantik model bilan topilgan **88 ta ortiqcha kast** (operand allaqachon shu turda: `(decimal)line.Amount` va h.k.)
  butun loyihada olib tashlandi; `QualityInspectionController` dagi `(decimal)((double)x)` aylanma konversiyasi ham.
- Matnga chiqadigan miqdorlar avvalgi double ko'rinishida: `QtyText.Plain(decimal)` (hujjat raqami `20261019_PNo_5`,
  izoh/log va xato xabarlari — `5.000000` emas), `TextBoxFor(..., "{0:0.######}")` (Expenses/Incomes/PContracts/POrders/
  PInvoices/ProductProduction tahrir formalari), `AssignDloc` `0.######`.
- Master merge'dan keyin: `Areas/Distribution/.../OrderService.cs` (FIFO jo'natish) `(double)qty` kastlari olib tashlandi.

## Tekshiruv

| Tekshiruv | Natija |
|---|---|
| Build (tahsinERP + tahsinERP.Tests) | xatosiz, har moduldan keyin |
| `Run-RazorCheck.ps1` | modul 1: 478/0; modul 2: 3 xato topdi (SOrder `100d`) → tuzatildi, 478/0; merge'dan keyin **485 view, 0 xato** |
| `Run-RegressionProbe.ps1` IPACK, oldin (master `791226cb`) ↔ keyin | har moduldan keyin va merge'dan keyin: **391 bo'lim, 0 farq, 0 yaxlitlash farqi** |
| `Run-FifoProbe.ps1` (yangi) eski DLL (double API) ↔ yangi DLL, F6TEST nusxada | **11 142 natija, 0 farq**; 3 533 yaxlitlash farqi, eng kattasi **3.3·10⁻⁷** (dona tannarxining 7-xonasi) |
| Sahifa matni + input qiymatlari: master sayt ↔ yangi sayt (IIS Express, bir xil ma'lumot) | **116 sahifa**: matn bir xil; faqat 2 JSON endpoint'da son ko'rinishi `111.5` → `111.500000` (pastga qarang) |
| Yozish yo'li (veb): ikkala saytda bir xil 10 tahrir formasi + MES "xomashyo chiqimini qayta hisoblash", F6A ↔ F6B | **9 569 qator, 0 farq** (P/S shartnoma, buyurtma, invoys, sotuv invoysi, ishlab chiqarish, ombor jadvallari) |
| Unit testlar | 116/116 |
| Integratsion testlar (F6C — IPACK'ning yangi nusxasi, migratsiya 018 bilan) | 12/12, jumladan distributsiya FIFO jo'natish + tannarx |
| Smoke (yangi sayt) | 111 sahifa 200; 4 ta 404 (mavjud bo'lmagan ID/route — master'da ham); 1 ta 500 — master'da ham (pastga qarang) |

FifoProbe bo'limlari: A) 2 164 chiqim qatorining FIFO tannarxi va qoldiq birligiga konversiyasi (faqat o'qish); C) 240 ishlab
chiqarishning BOM/norma sarfi; E) P/S shartnoma QQS stavkalari, qator jamlari, P-buyurtma `ApplyVat`/`RateFromGrossNet`,
1 432 kirim qatorining brutto summasi, `ParsePercent`; B/D) bitta tranzaksiyada oxirgi 60 chiqim hujjatini bekor qilib qayta
chiqarish (DLOC lot ajratmalari, qoldiq, tannarx), kasr deltali `Increase/Decrease`, yetarli bo'lmagan qoldiq — oxirida ROLLBACK.
Xatolar (123 ta, ma'lumotdagi m2→dona konversiyasi yo'qligi) ikkala build'da aynan bir xil.

Veb-yozish sinovida kirim/chiqim tahriri bu foydalanuvchi uchun ruxsat/holat qoidasi bilan to'xtadi (ikkala saytda bir xil) —
bu yo'l FifoProbe'ning B bo'limida servis darajasida sinalgan.

## Bilib qo'yish kerak

- **JSON'da decimal ko'rinishi**: `ProductionToSalesTransfer/GetProductionStock`, `GetAvailableProductionLots` endi `111.500000`
  qaytaradi (avval `111.5`). JS uchun bu bir xil son (`JSON.parse`), sahifada ko'rinadigan matn o'zgarmagan.
- **Xavfli naqshlar** (build ko'rmaydi, RazorCheck qisman ko'radi): `ViewBag.X as double?`, `ViewBag` dinamik qiymatni `var` ga
  olish (natija `dynamic`), decimal `ToString()`/`TextBoxFor`/`Html.TextBox` dagi `.000000` dumi. Yangi kodda miqdor/narx
  uchun `decimal` ishlating; matnga `QtyText.Plain` yoki aniq format bilan chiqaring.
- `SOrder/DeliverySchedule/97` — 500, **master'da ham** (bu ishga aloqasiz, alohida ko'rish kerak).

## Qamrovdan tashqarida qolgan double'lar

Hisobot/dashboard va muhandislik hisoblari (ombor/FIFO/QQS quvuriga kirmaydi, faqat ko'rsatish): `MESDashboardController` +
`MESDashboardViewModel`, `MESReportsController` + `MESReportViewModels`, `ProductPlanController` + `ProductPlan*ViewModel`,
`NormController`/`NormLineMath`, `InventoryReportsController` (raw SQL'da `CAST(... AS FLOAT)`), `WarehouseMonthlyReportService`,
`TransportLoadCalculator`/`FTransportTypeController`, `ReportsController`, `AdminSetting/HomeController`, `TannarxActualCostEngine`,
`BomMaterialSummaryService`. Ularni o'tkazish — keyingi alohida qadam (ko'rinadigan matn formati bilan ehtiyotkor).

## Lokal bazalar

- `DBTHSN-IPACK` — o'zgartirilmadi (faqat o'qildi; zaxiralar: `MSSQL\Backup\DBTHSN-IPACK_pre_float_internal_20261019.bak`,
  `..._f6c_copy_20261019.bak`). 192.168.100.200 ishlatilmadi.
- Sinov nusxalari (o'chirsa bo'ladi): `F6TEST_IPACK`, `F6A_IPACK`, `F6B_IPACK`, `F6C_IPACK` — ularda `khojiabror` foydalanuvchisiga
  vaqtinchalik sinov paroli o'rnatilgan.
