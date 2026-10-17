# Finance moduli — optimizatsiya natijasi (2026-10-17)

Branch: `claude/opt-finance-22068b` (push/merge qilinmagan). Audit: `FINANCE.md`.
Build: MSBuild Debug — xatosiz. Finance view'lari `aspnet_compiler` bilan alohida kompilyatsiya qilib tekshirildi (xatosiz).

## Commitlar

| Commit | Mazmuni |
|---|---|
| e3ca0c29 | FinPlPlans Delete/BulkEdit view'larida Razor kompilyatsiya xatolari (ikkala sahifa runtime'da yiqilardi) |
| 41550497 | FinDashboard servislarga bo'lindi, yagona balans engine (P01–P04, P06–P10, P13, B02, B03) |
| b575dc93 | OpeningBalance: GET'dagi DDL olib tashlandi, saqlash tranzaksiyada (P11, B06, B26) |
| 97da4a9a | FinTransactions: server-side sahifalash + standart sana filtri (P05, B14, B22) |
| d979802a | FinAccounts: 20 tadan keyingi hisoblar ko'rinmasdi (B13) |
| b1fba8e9 | Stored XSS (B19) |
| 7bd31c87 | EBITDA/DCF formulalari (B04, B05) — sizning qaroringiz |
| 61fe60d3 | Kontragent qarzi uchun yagona summa qoidasi (B08) — sizning qaroringiz |
| 9219b27c | Akt-sverki: opening + keyingi harakatlar (B07, B09) — sizning qaroringiz |
| 5e21846a | Data-fix skriptlari (ishga tushirilmagan) |
| 4e754284 | Kichik buglar va o'lik kod (B10, B11, B12, B21, B25, B28, P15) |

## Yangi tuzilma

`FinDashboardController` 3672 → ~330 qator. URL'lar o'zgarmagan.
Ruxsat: `[Permission("FinDashboard")]`, `ShareholderValue` — `"Shareholder"`. Popup JSON qaytarishi uchun ichki tekshiruv saqlangan.

`Services/Finance/`:

| Fayl | Vazifasi |
|---|---|
| `FinQueries` | Real-cash filtri (D1), hisob qoldiqlari bitta GROUP BY bilan (D2), oylik kategoriyalari/payroll (D5, D6), asosiy valyuta (D12) |
| `CounterpartyBalanceService` | AR/AP (D9, D10). Ma'lumot bir marta yuklanib, sanalar bo'yicha xotirada kesiladi va keshlanadi. Yagona summa qoidasi (B08) |
| `BalanceSheetService` | Yagona balans engine (D3): BalanceSheet, details, DCF, Liabilities; popup harakatlari |
| `FinLiabilities` | Oylik, doimiy harajatlar va logistika qarzi (D6–D8) |
| `BalanceInventoryValuation` | Tayyor maxsulot va xomashyo bahosi (P03) |
| `FinAnalyticsService` | Cashflow, Overview, analitika, AccountBalances, P&L |
| `LiabilityReportsService` | ReportsHub, LiabilitiesDashboard |
| `ValuationService` | EBITDA, DCF, aksiyadorlar ulushi |

DTO'lar `ViewModels/Finance/FinDashboardModels.cs` ga ko'chdi (namespace `tahsinERP.ViewModels.Finance`).

## Raqamlar: refaktordan oldin va keyin (lokal DBTHSN-IPACK, faqat o'qish)

Usul: eski va yangi `tahsinERP.dll` ustida harness barcha FinDashboard action'larini chaqirdi. 44 stsenariy: 3 sana, 11 popup va 4 Akt-sverki. Har bir ViewBag/Model JSON'da maydonma-maydon solishtirildi. Harness yozish buyruqlarini bloklaydi; bironta ham yozish urinishi bo'lmadi.

1. **Sof refaktor (41550497 … 4e754284, qarorlardan tashqari):** barcha raqamlar bir xil (33/33 dashboard fayli).
   - Farq faqat view ishlatmaydigan va olib tashlangan ViewBag'larda: `DailyTrend`, `DailyAccountActivity`, CategoryAnalytics `CurrencyShortname`, Liabilities `TotalPayables`/`TotalReceivables`/`PayrollLiability`.
   - Balansda bir xil SortOrder'li kategoriyalar tartibi endi deterministik (SortOrder, keyin ID). Avval SQL tasodifiy tartibda qaytarardi.
2. **Qarorlar bo'yicha o'zgargan raqamlar (30.06.2026):**

| Ko'rsatkich | Oldin | Keyin |
|---|---|---|
| EBITDA, 12 oy | 140 611 503,58 | 154 592 282,10 |
| Operatsion daromad | 2 182 181 402,32 | 1 898 150 636,07 |
| Operatsion xarajat | 2 181 902 419,29 | 1 743 558 353,97 |
| DCF sof aktiv | 93 635 192,98 | 864 560 831,98 |
| DCF passivlar | 2 473 373 108,85 | 1 702 447 469,85 |
| Kompaniya qiymati (DCF) | 747 198 522,97 | 1 433 056 947,48 |
| Kompaniya qiymati (EBITDA × 4) | 562 446 014,32 | 618 369 128,40 |
| Akt-sverki: ta'minotchi 1 / shartnoma 107 | 925 975 586,39 (27 qator, to'liq tarix) | 937 091 601,47 (opening + keyingi; dashboard bilan bir xil) |
| Akt-sverki: mijozlar 4/374 va 5/01-08-25 | yakuniy qoldiq bir xil | yakuniy qoldiq bir xil; qatorlar faqat opening'dan keyin |

So'rovlar soni (bitta sahifa yuklanishi):

| Sahifa | Oldin | Keyin |
|---|---|---|
| LiabilitiesDashboard, 12 oy | 296 | 47 |
| LiabilitiesDashboard, 1 oy | 162 | 43 |
| BalanceSheet | 124–189 | 32–35 |
| Passivlar details | 121–188 | 25–29 |
| DCF | 151 | 36 |
| ReportsHub | 40–43 | 19 |
| FinancialOverview | 28 | 9 |
| PL | 14 | 4 |
| Cashflow | 11–14 | 8 |

FinTransactions ro'yxati endi butun jadval o'rniga sahifama-sahifa yuklanadi (5 so'rov, 25 qator).

## Sizning qarorlaringiz (2026-10-17)

| ID | Qaror |
|---|---|
| B04 | DCF: aktiv ustuni − Type 2 majburiyatlar; kapital (Type 3) ayirilmaydi |
| B05 | EBITDA/DCF daromad va xarajati P&L bandlari bo'yicha (dividend bandisiz) |
| B08 | Dashboard qoidasi hamma joyda: xarid = INCOMES.TotalPrice, bo'lmasa P_INVOICES.Amount; sotuv = TotalWithVAT ?? TotalAmount |
| B07/B09 | Akt-sverki: opening sanasi shartnoma opening jadvalidan, so'ng faqat keyingi harakatlar |

## Data-fix skriptlari (`Database_Migrations/`, hech biri ishga tushirilmagan)

| Skript | Lokal natija |
|---|---|
| `Finance_OpeningContractBalances_Schema.sql` — avval har GET'da bajariladigan DDL + (BalanceType, SnapshotDate) indeksi | — |
| `DataFix_Finance_OpeningContractTotals_Check/Fix.sql` — eski har-so'rov UPDATE'i | 0 qator |
| `DataFix_Finance_InvoiceMirrorAmounts_Check/Fix.sql` — eski mirror yozuvlarini B08 qoidasiga keltirish | 39 xarid yozuvi, 1 542,1 → 1 705,0 mln |
| `DataFix_Finance_BalanceCategoryTypes_Check/Fix.sql` — ombor va asosiy vosita kategoriyalari | Xomashyo, Tayyor maxsulot, Jihozlar, Bino Type=3 (kapital) bo'lib qolgan. Shu sabab balansda passiv ustunida, DCF'da aktivdan tashqarida |

Kategoriya turlari tuzatilsa, DCF sof aktivi lokal bazada taxminan 1 635,5 mln bo'ladi.

**Ishga tushirish tartibi (SRM xarid bilan bog'liq):** SRM-B25 (`claude/opt-srm-procurement-760fa4`, 391b2cd6) dan keyin `P_INVOICES.Amount` QQS bilan (brutto) saqlanadi. Shu sabab:
1. Avval `DataFix_SRM_Procurement_01_InvoiceAmount_Fix.sql` ishga tushiriladi (eski invoyslar bruttoga keltiriladi).
2. Keyin `DataFix_Finance_InvoiceMirrorAmounts_Fix.sql`.

Finance kodi Amount'ga QQS qo'shmaydi (61fe60d3), shuning uchun ikki marta QQS bo'lmaydi.

SRM xarid branch'i `FinancePostingService.cs` ni o'zgartirmagan, shuning uchun merge ziddiyati bo'lmaydi (SRM sessiyasi tasdiqladi).

## Tuzatilgan ID'lar

- **Performance:** P01, P02, P03, P04, P05, P06, P07, P08 (DailyTrend/DailyAccountActivity olib tashlandi), P09, P10, P11, P13, P15, P17 (yangi kodda AsNoTracking).
- **Bug va xavfsizlik:** B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13, B14, B19, B21, B22, B25, B26, B28. B20 qisman: dashboarddagi `catch {}` fallback'lari olib tashlandi, FIN_RECURRING_OBLIGATIONS uchun faqat SqlException ushlanadi va loglanadi.
- **Qo'shimcha:** FinPlPlans Delete va BulkEdit view'lari kompilyatsiya bo'lmasdi (auditda yo'q edi).
- **Dublikatlar:** D1–D12 (servislarga yig'ildi), D23 (FinTransactions Index fix-up).
- **O'lik kod:** EnsureOpeningContractBalanceTable, UpsertDetailedOpeningSnapshots, ishlatilmagan ViewBag hisoblari, GetLatestOpeningDate/CategorySnapshotInfo, raw-SQL fallback'lar, FinTransactions.GetContractsBySupplier, sortOrder/page/pageSize, GetAllContractBalancesAsync va bo'sh if, `parentID == 0` tarmog'i, loadAccountCurrency, OpeningContractBalanceLite.TotalAmount/PaidAmount (Reconciliation).

## Qolganlar (bajarilmadi)

- **B01 (valyuta):** ko'rsatmaga ko'ra konvertatsiya qo'shilmadi. Dashboardlar UZS va USD summalarni hali ham qo'shib yuboradi.
- **B15:** FinTransactions Edit POST'da Create'dagi validatsiya yo'q; posted (Manual emas) yozuvni POST orqali tahrirlash mumkin.
- **B16:** AccountBalances opening snapshot'ni hisoblar soniga teng bo'ladi va `>=` ishlatadi. Bu hisob kesimidagi opening (sxema) talab qiladi.
- **B17:** BalanceCategoryID NULL bo'lgan doimiy majburiyat balansga tushmaydi.
- **B18:** balansdagi oylik qarzi opening payroll qatorlarini (BalanceType 3) hisobga olmaydi.
- **B20 qoldig'i:** FinBalanceCategories/MapCategories va ReconciliationAdjustmentService'dagi `catch {}`.
- **B23:** Import'da dublikat kod/nom; **B24:** Shareholder AddSharePeriod/AddContribution; **B27:** FinRecurringObligations o'z ruxsat moduli.
- **B29:** PostPurchaseInvoice hisobni nom bo'yicha qidiradi va jim o'tkazib yuboradi. Mirror invoys yaratilganda yoziladi; keyin kelgan kirimlar (INCOMES) uni avtomatik yangilamaydi.
- **P12, P14, P16, P18:** OpeningBalance sahifasidagi fetch'lar, ContractBalanceService N×M, daraxt N+1, Chart.js CDN.
- **M02/M03:** OpeningBalance va FinTransactions controllerlarini servislarga bo'lish.
- **D13–D22:** SelectList, daraxt CRUD, MapCategories va Create/Edit view'larini birlashtirish.
- **Ruxsat:** boshqa Finance controllerlarini `[Permission]` ga o'tkazish (hozir inline tekshiruvlar ishlayapti). M04 — view'lardagi `Response.Redirect`.
- **Ekspeditor akt-sverkisi:** opening (BalanceType 4) ishlatmaydi; ta'minotchi/mijoz bilan bir xil qilish uchun alohida qaror kerak.
- **Brauzerda tekshirilmadi:** FinTransactions ro'yxatining server-side DataTables UI'si, OpeningBalance saqlash/AutoFill tranzaksiyasi va XSS tuzatishlari. Faqat build, view kompilyatsiyasi va harness orqali tekshirildi.

## Umumiy qatlam uchun takliflar (o'zgartirilmadi)

1. `EDMX`: `FIN_RECURRING_OBLIGATIONS` jadvali modelda yo'q, xom SQL ishlatiladi. `sp_Fin_CashSummary` va `V_FIN_PL_PLAN_VS_FACT_MONTHLY` ishlatilmaydi.
2. `PERMISSIONMODULES` ga `FinRecurringObligations` moduli qo'shilishi kerak (B27).
3. FinancePosting mirror yozuvlari chaqiruvchining tranzaksiyasida bo'lishi kerak (CRM, SRM, Logistika chaqiradi).
4. FIN_TRANSACTIONS'ga asosiy valyutadagi summa (`AmountBase`/`Rate`) — B01 qarori.
5. CRM/SRM/HR dashboardlari FIN_OPENING_CONTRACT_BALANCES'ni xom SQL bilan o'qiydi; ular `CounterpartyBalanceService` dan foydalanishi mumkin.
