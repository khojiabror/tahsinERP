# float → decimal: ta'sir ro'yxati va xavfsiz reja

> Holat 2026-10-19: hamma 5 bosqich bajarildi — 1–2: migratsiya 011, [DB_FLOAT_DECIMAL_STAGE1-2.md](DB_FLOAT_DECIMAL_STAGE1-2.md);
> 3–5: migratsiyalar 014/015/017, [DB_FLOAT_DECIMAL_STAGE3-5.md](DB_FLOAT_DECIMAL_STAGE3-5.md). Sxemada float qolmadi;
> ichki hisoblarni (StockLedger/FIFO, UnitConversion, VatMath) to'liq decimal'ga o'tkazish — ochiq.

Holat (2026-10-18, master + db-foundation): sxemada **76 ta `float`** ustun (pul, narx, miqdor, o'lcham).
`float` ikkilik kasr: `0.1 + 0.2 ≠ 0.3`, SUM/GROUP BY'da tiyinlar "suzadi", tenglik solishtiruvi ishonchsiz.
Kod tomonda ular `double` — ko'p joyda `(decimal)` / `Convert.ToDecimal` bilan aralash hisoblanadi.

## Tavsiya etilgan turlar

| Guruh | Ustunlar (misol) | Tur |
|---|---|---|
| Pul summasi | `*.Amount` (hujjat summasi), `TotalPrice`, `P_INVOICES.Amount`, `F_WAYBILLS.Price` | `decimal(19,4)` |
| Birlik narxi | `*.Price`, `PiecePrice`, tarif | `decimal(19,6)` (USD birlik narxlari 4+ xona) |
| Miqdor | `INCOME_PRODUCTS.Amount`, `Quantity`, `PRODUCT_STOCKS.Amount`, `Qty*` | `decimal(18,6)` (2 qatorda 4+ xona bor) |
| O'lcham / og'irlik | `PRODUCTS.Length/Width/Height/Weight`, `F_TRANSPORT_TYPES.*` | `decimal(18,4)` |
| Foiz / stavka | `VATRate` | `decimal(9,4)` |

## Bosqichlar (har biri alohida PR, Balans/Tannarx/ombor hisobotlari oldin/keyin solishtiriladi)

1. **Tayyorlov**: `BalanceProbe` kabi solishtiruv harness'ini Tannarx, ombor qoldig'i (StockLedger), CRM/SRM
   dashboard'lariga kengaytirish; IPACK nusxasida "oldin" JSON.
2. **Moliya-logistika guruhi** (F_WAYBILLS, F_WAYBILL_PAYMENTS, F_CONTRACT_TARIFFS, F_CONTRACTS — kod kam, `LogisticsMoney` bor):
   migratsiya `ALTER COLUMN ... decimal` (+ indeks/default'larni qayta yaratish), EDMX `Type="decimal" Precision Scale`,
   `Models/*.cs` `double` → `decimal`, kompilyatsiya xatolari bo'yicha kodni tuzatish.
3. **Xarid/sotuv hujjatlari** (P_*/S_* *_PRODUCTS, P_INVOICES, S_ORDERS ...): triggerlar (`Update_*_Amount_After_*`) va
   `sp_*` protseduralar ichidagi `float` o'zgaruvchilar ham.
4. **Ombor va ishlab chiqarish** (INCOME_PRODUCTS, EXPENSE_PRODUCTS, PRODUCT_STOCKS, PRODUCT_PRODUCTION, NORM_INPUT_LINES, PRODUCTPLANS*):
   eng xavfli — FIFO lot (DlocFifo), UnitConversionService, kesish normalari. Avval `double`→`decimal` faqat
   saqlash chegarasida (`(decimal)` konvertatsiya), keyin ichki hisoblar.
5. **Spravochnik o'lchamlari** (PRODUCTS, F_TRANSPORT_TYPES) — oxirida, past xavf.

Har bosqichda: lokal zaxira → migratsiya (`Database/Migrations/NNN`) → EDMX → build → harness solishtiruvi (farq
≤ 0.01 so'm yaxlitlash bo'lishi mumkin — hujjatlanadi) → sahifa smoke testi.
Xavf: `ALTER COLUMN` katta jadvalda qulf; prod'da texnik oynada. JS tomonda (`Scripts/app/*`) raqam formatlash o'zgarmaydi.

## Ta'sir jadvali (ustun → fayllar, avtomatik grep: fayl ustun nomini va entity/jadval nomini o'z ichiga oladi)
| Jadval.Ustun | EF entity | Fayllar | Namuna |
|---|---|---|---|
| DAMAGED_PRODUCTS.Quantity | DAMAGED_PRODUCTS | 7 | Create.cshtml, DamagedProductController.cs, Delete.cshtml, Details.cshtml, Edit.cshtml … |
| EXPENSE_PRODUCTS.Amount | EXPENSE_PRODUCTS | 18 | Details.cshtml, Details.cshtml, ExpenseDocumentService.cs, ExpensesController.cs, HomeController.cs … |
| EXPENSE_PRODUCTS.PiecePrice | EXPENSE_PRODUCTS | 10 | Details.cshtml, ExpenseDocumentService.cs, ExpensesController.cs, IncomesController.cs, InventoryAdjustmentService.cs … |
| EXPENSE_PRODUCTS.TotalPrice | EXPENSE_PRODUCTS | 10 | Details.cshtml, ExpenseDocumentService.cs, ExpensesController.cs, IncomesController.cs, InventoryAdjustmentService.cs … |
| EXPENSES.Amount | EXPENS | 18 | DeletedRecordsController.cs, Details.cshtml, Details.cshtml, ExpenseDocumentService.cs, ExpensesController.cs … |
| EXPENSES.TotalPrice | EXPENS | 11 | Delete.cshtml, Details.cshtml, ExpenseDocumentService.cs, ExpensesController.cs, IncomesController.cs … |
| F_CONTRACT_TARIFFS.Rate | F_CONTRACT_TARIFFS | 8 | Delete.cshtml, Details.cshtml, Details.cshtml, FContractTariffController.cs, WaybillImportService.cs … |
| F_CONTRACT_TARIFFS.MinimumCharge | F_CONTRACT_TARIFFS | 6 | Details.cshtml, FContractTariffController.cs, WaybillImportService.cs, WaybillLookupController.cs, WaybillService.cs … |
| F_CONTRACTS.Amount | F_CONTRACTS | 15 | Delete.cshtml, DeletedRecordsController.cs, Details.cshtml, Details.cshtml, FContractController.cs … |
| F_TRANSPORT_TYPES.ExtLgth | F_TRANSPORT_TYPES | 4 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, TransportLoadCalculator.cs |
| F_TRANSPORT_TYPES.ExtWdth | F_TRANSPORT_TYPES | 4 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, TransportLoadCalculator.cs |
| F_TRANSPORT_TYPES.ExtHght | F_TRANSPORT_TYPES | 5 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, Index.cshtml, TransportLoadCalculator.cs |
| F_TRANSPORT_TYPES.IntLgth | F_TRANSPORT_TYPES | 5 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, TransportLoadCalculator.cs, TransportLoadSimulationController.cs |
| F_TRANSPORT_TYPES.IntWdth | F_TRANSPORT_TYPES | 5 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, TransportLoadCalculator.cs, TransportLoadSimulationController.cs |
| F_TRANSPORT_TYPES.IntHght | F_TRANSPORT_TYPES | 6 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, Index.cshtml, TransportLoadCalculator.cs … |
| F_TRANSPORT_TYPES.CapableOfLifting | F_TRANSPORT_TYPES | 5 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, Index.cshtml, WaybillService.cs |
| F_TRANSPORT_TYPES.TransportWeight | F_TRANSPORT_TYPES | 4 | Delete.cshtml, Details.cshtml, FTransportTypeController.cs, Index.cshtml |
| F_WAYBILL_PAYMENT_SPLITS.Amount | F_WAYBILL_PAYMENT_SPLITS | 4 | FinancePostingService.cs, WaybillController.cs, WaybillLedgerService.cs, WaybillPaymentController.cs |
| F_WAYBILL_PAYMENTS.Payment_Amount | F_WAYBILL_PAYMENTS | 12 | Delete.cshtml, DeletedRecordsController.cs, Details.cshtml, Details.cshtml, FinCounterpartyLookupService.cs … |
| F_WAYBILLS.CBM | F_WAYBILLS | 5 | Details.cshtml, LogisticsAnalyticsService.cs, WaybillController.cs, WaybillImportService.cs, WaybillService.cs |
| F_WAYBILLS.GrWeight | F_WAYBILLS | 5 | Details.cshtml, LogisticsAnalyticsService.cs, WaybillController.cs, WaybillImportService.cs, WaybillService.cs |
| F_WAYBILLS.Amount | F_WAYBILLS | 16 | DeletedRecordsController.cs, Details.cshtml, FinCounterpartyLookupService.cs, FinLiabilities.cs, FinancePostingService.cs … |
| F_WAYBILLS.DistanceKm | F_WAYBILLS | 5 | Details.cshtml, LogisticsAnalyticsService.cs, WaybillController.cs, WaybillImportService.cs, WaybillService.cs |
| F_WAYBILLS.Rate | F_WAYBILLS | 2 | WaybillImportService.cs, WaybillService.cs |
| F_WAYBILLS.NetWeight | F_WAYBILLS | 2 | WaybillImportService.cs, WaybillService.cs |
| INCOME_PRODUCTS.Amount | INCOME_PRODUCTS | 16 | BalanceInventoryValuation.cs, Details.cshtml, ExpensesController.cs, IncomeDocumentService.cs, IncomesController.cs … |
| INCOME_PRODUCTS.PiecePrice | INCOME_PRODUCTS | 14 | BalanceInventoryValuation.cs, Details.cshtml, ExpensesController.cs, IncomeDocumentService.cs, IncomesController.cs … |
| INCOME_PRODUCTS.TotalPrice | INCOME_PRODUCTS | 12 | BalanceInventoryValuation.cs, Details.cshtml, ExpensesController.cs, IncomeDocumentService.cs, IncomesController.cs … |
| INCOME_PRODUCTS.VATRate | INCOME_PRODUCTS | 4 | IncomeDocumentService.cs, IncomesController.cs, InventoryAdjustmentService.cs, StockCostService.cs |
| INCOMES.Amount | INCOME | 26 | BalanceInventoryValuation.cs, BalanceSheetService.cs, CounterpartyBalanceService.cs, DeletedRecordsController.cs, Details.cshtml … |
| INCOMES.TotalPrice | INCOME | 19 | BalanceInventoryValuation.cs, BalanceSheetService.cs, CounterpartyBalanceService.cs, Delete.cshtml, Details.cshtml … |
| NORM_INPUT_LINES.Consumption | NORM_INPUT_LINES | 4 | BomMaterialSummaryService.cs, NormController.cs, NormLineMath.cs, TannarxService.cs |
| NORM_INPUT_LINES.WasteAmount | NORM_INPUT_LINES | 4 | BomMaterialSummaryService.cs, NormController.cs, NormLineMath.cs, TannarxService.cs |
| P_CONTRACT_PRODUCTS.Price | P_CONTRACT_PRODUCTS | 5 | Details.cshtml, PContractService.cs, PContractVatHelper.cs, PContractsController.cs, PInvoiceService.cs |
| P_CONTRACT_PRODUCTS.Quantity | P_CONTRACT_PRODUCTS | 6 | ContractBalanceService.cs, Details.cshtml, PContractService.cs, PContractVatHelper.cs, PContractsController.cs … |
| P_CONTRACT_PRODUCTS.Amount | P_CONTRACT_PRODUCTS | 5 | Details.cshtml, PContractService.cs, PContractVatHelper.cs, PContractsController.cs, PInvoiceService.cs |
| P_CONTRACT_PRODUCTS.MOQ | P_CONTRACT_PRODUCTS | 3 | Details.cshtml, PContractService.cs, PContractsController.cs |
| P_CONTRACT_PRODUCTS.VATAmount | P_CONTRACT_PRODUCTS | 4 | Details.cshtml, PContractService.cs, PContractVatHelper.cs, PContractsController.cs |
| P_CONTRACT_PRODUCTS.TotalAmount | P_CONTRACT_PRODUCTS | 1 | Details.cshtml |
| P_CONTRACTS.Amount | P_CONTRACTS | 17 | CounterpartyBalanceService.cs, Delete.cshtml, DeletedRecordsController.cs, Details.cshtml, Details.cshtml … |
| P_INVOICE_PRODUCTS.Quantity | P_INVOICE_PRODUCTS | 6 | ContractBalanceService.cs, Details.cshtml, IncomesController.cs, PInvoiceService.cs, PInvoicesController.cs … |
| P_INVOICE_PRODUCTS.Price | P_INVOICE_PRODUCTS | 4 | Details.cshtml, IncomesController.cs, PInvoiceService.cs, PInvoicesController.cs |
| P_INVOICE_PRODUCTS.TotalPrice | P_INVOICE_PRODUCTS | 5 | Details.cshtml, IncomesController.cs, PInvoiceService.cs, PInvoicesController.cs, TannarxActualCostEngine.cs |
| P_INVOICES.Amount | P_INVOICES | 20 | BalanceSheetService.cs, CounterpartyBalanceService.cs, Delete.cshtml, DeletedRecordsController.cs, Details.cshtml … |
| P_ORDER_PRODUCTS.Price | P_ORDER_PRODUCTS | 5 | Details.cshtml, PInvoicesController.cs, POrderGenerationService.cs, POrderService.cs, POrdersController.cs |
| P_ORDER_PRODUCTS.Amount | P_ORDER_PRODUCTS | 7 | Delete.cshtml, Details.cshtml, PInvoicesController.cs, POrderGenerationService.cs, POrderService.cs … |
| P_ORDER_PRODUCTS.TotalPrice | P_ORDER_PRODUCTS | 6 | Details.cshtml, PInvoicesController.cs, POrderGenerationService.cs, POrderService.cs, POrdersController.cs … |
| P_ORDER_PRODUCTS.MOQ | P_ORDER_PRODUCTS | 3 | Details.cshtml, POrderService.cs, POrdersController.cs |
| P_ORDERS.Amount | P_ORDERS | 14 | Delete.cshtml, DeletedRecordsController.cs, Details.cshtml, Details.cshtml, Details.cshtml … |
| P_REQUISITION_PRODUCTS.Quantity | P_REQUISITION_PRODUCTS | 3 | Details.cshtml, POrdersController.cs, PRequisitionsController.cs |
| P_REQUISITION_PRODUCTS.EstimatedPrice | P_REQUISITION_PRODUCTS | 3 | Details.cshtml, POrdersController.cs, PRequisitionsController.cs |
| P_REQUISITION_PRODUCTS.Amount | P_REQUISITION_PRODUCTS | 4 | Details.cshtml, POrderService.cs, POrdersController.cs, PRequisitionsController.cs |
| PRODUCT_PRODUCTION.DayShift | PRODUCT_PRODUCTION | 3 | Index.cshtml, ProductPlanController.cs, ProductProductionController.cs |
| PRODUCT_PRODUCTION.NightShift | PRODUCT_PRODUCTION | 3 | Index.cshtml, ProductPlanController.cs, ProductProductionController.cs |
| PRODUCT_PRODUCTION.Total | PRODUCT_PRODUCTION | 13 | HomeController.cs, Index.cshtml, MESDashboardController.cs, MESReportsController.cs, ProductPlanController.cs … |
| PRODUCT_STOCKS.Amount | PRODUCT_STOCKS | 14 | ExpensesController.cs, HomeController.cs, Index.cshtml, InventoryController.cs, InventoryReportsController.cs … |
| PRODUCTPLANS.Amount | PRODUCTPLAN | 9 | Delete.cshtml, DeletedRecordsController.cs, HomeController.cs, InventoryReportsController.cs, MESDashboardController.cs … |
| PRODUCTPLANS_DAILY.DayShift | PRODUCTPLANS_DAILY | 2 | ProductPlanController.cs, ProductProductionController.cs |
| PRODUCTPLANS_DAILY.NightShift | PRODUCTPLANS_DAILY | 2 | ProductPlanController.cs, ProductProductionController.cs |
| PRODUCTPLANS_DAILY.Total | PRODUCTPLANS_DAILY | 8 | InventoryReportsController.cs, MESDashboardController.cs, MESReportsController.cs, ProductPlanController.cs, ProductProductionController.cs … |
| PRODUCTS.Weight | PRODUCT | 7 | Create.cshtml, Delete.cshtml, Details.cshtml, Edit.cshtml, Index.cshtml … |
| PRODUCTS.Length | PRODUCT | 25 | BomMaterialSummaryService.cs, Create.cshtml, Delete.cshtml, Details.cshtml, Details.cshtml … |
| PRODUCTS.Width | PRODUCT | 10 | Create.cshtml, Delete.cshtml, Details.cshtml, Edit.cshtml, Index.cshtml … |
| PRODUCTS.Height | PRODUCT | 10 | Create.cshtml, Delete.cshtml, Details.cshtml, Edit.cshtml, Index.cshtml … |
| PRODUCTS.StandardLengthMm | PRODUCT | 6 | BomMaterialSummaryService.cs, Create.cshtml, Edit.cshtml, NormController.cs, NormLineMath.cs … |
| S_CONTRACT_PRODUCTS.PiecePrice | S_CONTRACT_PRODUCTS | 8 | Delete.cshtml, SContractController.cs, SContractVatHelper.cs, SalesInvoiceService.cs, SalesInvoicesController.cs … |
| S_CONTRACT_PRODUCTS.Amount | S_CONTRACT_PRODUCTS | 7 | Delete.cshtml, SContractController.cs, SContractVatHelper.cs, SalesInvoicesController.cs, SalesLookups.cs … |
| S_CONTRACT_PRODUCTS.vatamount | S_CONTRACT_PRODUCTS | 5 | SContractController.cs, SContractVatHelper.cs, SalesInvoicesController.cs, SalesLookups.cs, SalesOrderService.cs |
| S_CONTRACT_PRODUCTS.totalamount | S_CONTRACT_PRODUCTS | 2 | SContractController.cs, SContractVatHelper.cs |
| S_CONTRACTS.Amount | S_CONTRACTS | 22 | CRMDashboardController.cs, CounterpartyBalanceService.cs, Delete.cshtml, Delete.cshtml, DeletedRecordsController.cs … |
| S_ORDER_PRODUCTS.Price | S_ORDER_PRODUCTS | 6 | Delete.cshtml, Details.cshtml, POrderGenerationService.cs, POrdersController.cs, SalesInvoicesController.cs … |
| S_ORDER_PRODUCTS.Amount | S_ORDER_PRODUCTS | 12 | Delete.cshtml, Details.cshtml, MESDashboardController.cs, POrderGenerationService.cs, POrdersController.cs … |
| S_ORDER_PRODUCTS.TotalPrice | S_ORDER_PRODUCTS | 7 | Delete.cshtml, Details.cshtml, POrderGenerationService.cs, POrdersController.cs, SalesOrderService.cs … |
| S_ORDER_PRODUCTS.MOQ | S_ORDER_PRODUCTS | 3 | Details.cshtml, POrdersController.cs, SalesOrderService.cs |
| S_ORDERS.Amount | S_ORDERS | 21 | CRMDashboardController.cs, Delete.cshtml, Delete.cshtml, DeletedRecordsController.cs, Details.cshtml … |
| WASTE_STOCKS.Amount | WASTE_STOCKS | 0 |  |

Jami ta'sirlangan fayllar (taxminiy, ustun nomi + entity nomi bir faylda): 132

Faol claude/opt-* branch'larda o'zgarayotgan ta'sirlangan fayllar: 0
