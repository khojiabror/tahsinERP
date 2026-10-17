# FINANCE module audit

Root: `C:\Work\SCHAIN\tahsinERP\tahsinERP\tahsinERP` (all paths below are relative to it).
Read-only audit. No project files were changed.

## Scope

**Files read: 98**

Controllers (12, 10 978 lines incl. services/helper):
- Controllers/Finance/FinDashboardController.cs (3672), OpeningBalanceController.cs (1560), FinTransactionsController.cs (1379), FinBalanceCategoriesController.cs (552), ShareholderController.cs (465), FinPlPlansController.cs (434), FinPlItemsController.cs (428), FinCategoriesController.cs (374), FinAccountsController.cs (361), FinRecurringObligationsController.cs (230)
- Controllers/ReconciliationController.cs (339)

Services / helpers (5): Services/FinancePostingService.cs, ReconciliationService.cs, ReconciliationAdjustmentService.cs, ContractBalanceService.cs, Helpers/BalanceCategoryDisplayHelper.cs

Models (15): FIN_ACCOUNTS, FIN_BALANCE_CATEGORIES, FIN_BALANCE_SNAPSHOTS, FIN_CATEGORIES, FIN_OPENING_CONTRACT_BALANCES, FIN_PL_ITEMS, FIN_PL_PLANS, FIN_TRANSACTIONS, V_FIN_PL_FACT_MONTHLY, V_FIN_PL_PLAN_VS_FACT_MONTHLY, SHAREHOLDER, SHAREHOLDER_CONTRIBUTIONS, SHAREHOLDER_SHARE_PERIODS, RECONCILIATION_ADJUSTMENTS, sp_Fin_CashSummary_Result

Views (66): FinDashboard (15): AccountAnalytics, AccountBalances, BalanceSheet, BalanceSheetAssetDetails, BalanceSheetLiabilityDetails, Cashflow, CategoryAnalytics, EBITDA, ExpenseAnalysis, FinanceReportsHub, FinancialOverview, IncomeAnalysis, LiabilitiesDashboard, PL, ShareholderValue; FinAccounts (5); FinBalanceCategories (6); FinCategories (5); FinPlItems (6); FinPlPlans (5); FinRecurringObligations (3); FinTransactions (6); OpeningBalance (4, Edit.cshtml 956); Shareholder (7); Reconciliation (2).
I read the large views (OpeningBalance/Edit, FinTransactions Create/Edit/Index, BalanceSheet*, PL, Cashflow, EBITDA) in full. The other views were checked with pattern scans (permission blocks, scripts, CDN, DataTables, ViewBag usage) and diffs between sibling views.

Also used for cross-checks: PInvoicesController (lines 150-200: ContractBalanceService usage), RoleHelper (permission checks are cached), _Layout menu links, and the EDMX entries for FIN_BALANCE_CATEGORY_CATEGORIES and FIN_PL_ITEM_CATEGORIES.

---

## Key answer: how the dashboards compute totals

Almost every aggregate is computed in memory or with one query per row, not with a set-based SQL query. The same totals are recomputed several times within a single request and again on each drill-down page.

| Action | What happens | Rough query count / memory |
|---|---|---|
| Cashflow (FinDashboardController.cs:19) | Loads all period transactions as tracked entities with 3 Includes, then sums in memory. Runs one SUM per cash account and one per bank account (lines 63-79). | 3 + 2·N_accounts queries, full period rows in RAM |
| FinancialOverview (:2079) | Same pattern, but **no Include**, so `t.FIN_CATEGORIES` and `t.CURRENCy` lazy-load per entity (lines 2124-2158) | N+1 per row; per-account SUM loop (2107-2120) |
| CategoryAnalytics / AccountAnalytics / ExpenseAnalysis / IncomeAnalysis (:2177, :2270, :2358, :2444) | Load all period rows, then GroupBy in memory. `DailyTrend` and `DailyAccountActivity` are computed but no view uses them. | full period rows in RAM |
| AccountBalances (:2530) | Loads **every FIN_TRANSACTIONS row ≤ toDate** for all accounts as full entities (2571-2577). Loads all FIN_BALANCE_SNAPSHOTS before filtering (2580-2585). | whole ledger in RAM |
| PL (:142) | Loads the period's transactions. Runs one FIN_CATEGORIES query per PL item (181-184), then sums in memory. Ignores the existing SQL view `V_FIN_PL_FACT_MONTHLY`. | 3 + N_items queries |
| BalanceSheet (:245) | One SUM per account (296-302). Per category: account/category SUMs, full AP/AR FIFO (`CalculateSupplierOutstanding`/`CalculateCustomerOutstanding`), inventory valuation with an N+1 PRODUCTS lookup per stock line (3435), and one SUM per P_INVOICE (508-526) / per obligation (625). | 50-500+ queries |
| BalanceSheetAssetDetails / LiabilityDetails (:660, :682) | Recompute the whole balance through a **second, diverging engine** (`BuildBalanceCategoryDetails`, :1203) | same again |
| ShareholderValue DCF (:1047) | Calls `BuildBalanceCategoryDetails` **twice** (assets, then liabilities). Each call re-runs the per-account loop and AR/AP; the asset pass also runs inventory valuation. One share-period query per shareholder (1002). | 2× the balance sheet |
| LiabilitiesDashboard (:2915) | Runs `CalculateSupplierOutstanding` and `CalculateCustomerOutstanding` **once per month in range plus once more**, and again inside `BuildBalanceCategoryDetails` (3219). Each call materialises all supplier/customer payments and invoices ≤ date. Per-month payroll queries, per-account ToList loops (3114-3195). Unused: `payablesTransactions` and `receivablesTransactions` (loaded only to pick a currency), and the ViewBags TotalPayables/TotalReceivables/PayrollLiability, which the view never reads. | default 1 month ≈ 8 full AR/AP scans; 12 months ≈ 28 |
| FinanceReportsHub (:2675) | 12-month loop × 2 cumulative full-history aggregates (2828-2859). One SUM per recurring obligation (2878). One HR_EMPLOYEES lookup per opening row (2770). | ~30 + N queries |

---

## Findings

Severity: Critical/High/Med/Low. Effort and Risk: S/M/L, Low/Med/High.

### Performance

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| FIN-P01 | Critical | Perf | FinDashboardController.cs:3060-3102, 2946, 2954, 3219 | LiabilitiesDashboard calls `CalculateSupplierOutstanding` and `CalculateCustomerOutstanding` once per month in the loop, plus the headline call, plus once more inside `BuildBalanceCategoryDetails`. Each call loads all payment candidates, invoices, INCOMES totals and contracts ≤ date into memory. A 12-month range means ~28 full AR/AP rebuilds per page load. | Compute AR/AP once for the whole range: load invoices and payments once, sort by date, and do a running FIFO to get each month-end balance in a single pass. Or pre-aggregate monthly balances in SQL (a `sp_Fin_ArApMonthEnd(@from,@to)` stored procedure). Reuse the headline result for `PassiveBalanceStats` instead of calling `BuildBalanceCategoryDetails`. | M | Med |
| FIN-P02 | High | Perf | FinDashboardController.cs:63-79, 296-302, 1240-1246, 2107-2120, 3114-3195; also AccountBalances 2571 | Account balances are computed with **one SUM query per account** in six places. LiabilitiesDashboard even ToLists each account's full history. | One grouped query, e.g. `ActiveFinTransactions(db).Where(t=>t.TxnDate<=asOf && ids.Contains(t.AccountID)).GroupBy(t=>t.AccountID).Select(g=>new{g.Key, Bal=g.Sum(t=>t.Direction==1?t.Amount:-t.Amount), In=..., Out=...})`. Put it in `FinQueries.GetAccountBalances(db, ids, asOf, from?)`. The unused `sp_Fin_CashSummary` already does this in SQL. | S | Low |
| FIN-P03 | High | Perf | FinDashboardController.cs:3429-3450 (`GetBalanceStockLines`) | One `PRODUCTS` query with 2 Includes per positive stock line. It runs on BalanceSheet, AssetDetails, DCF and the popup, usually for hundreds of products. | Bulk-load: `var ids = qtyRows.Select(q=>q.ProductID).ToList(); var prods = db.PRODUCTS.AsNoTracking().Include(..).Where(p=>ids.Contains(p.ID)).ToDictionary(p=>p.ID);` | S | Low |
| FIN-P04 | High | Perf | FinDashboardController.cs:1050-1051, 245-657, 660-702 | The balance sheet is recomputed from scratch on every view: BalanceSheet, AssetDetails and LiabilityDetails, ×2 for DCF, and LiabilitiesDashboard. DCF runs the full engine twice. | One `BalanceSheetService.Compute(db, asOf)` that returns all category details once. DCF and the details pages filter that result. Optionally cache it per asOfDate for 1-5 minutes in `MemoryCache` (key includes the max FIN_TRANSACTIONS.ID / CreatedDT). | M | Med |
| FIN-P05 | High | Perf | FinTransactionsController.cs:30-122; Views/FinTransactions/Index.cshtml | Index has no default date filter, so it loads the **whole FIN_TRANSACTIONS table** with 4 Includes (tracked). It then runs 3 extra lookups to "fix" navigation properties that the Includes already loaded (100-122). Everything renders into one client-side DataTable. The sortOrder/page/pageSize params are ignored. | Default to the last 1-3 months. Use server-side DataTables (an `IndexData` JSON action with projection, `AsNoTracking`, Skip/Take). Delete the fix-up loop. | M | Low |
| FIN-P06 | High | Perf | FinDashboardController.cs:2092-2158 | FinancialOverview materialises period transactions **without Include**, then reads `t.FIN_CATEGORIES.Name` and `t.CURRENCy`, which lazy-loads per row. | Push GroupBy/Sum to SQL with projections (`GroupBy(t=>new{t.CategoryID,t.FIN_CATEGORIES.Name})` on IQueryable). Get the currency with one `GroupBy(CurrencyID).OrderByDescending(Count)` query. | S | Low |
| FIN-P07 | High | Perf | FinDashboardController.cs:2565-2585 | AccountBalances loads all ledger rows ≤ toDate for all accounts as full entities, and all snapshots ≤ date, before filtering by SourceType in memory. | SQL GroupBy per account: balance plus period in/out (see P02). Filter snapshots by SourceType in SQL. | S | Low |
| FIN-P08 | Med | Perf | FinDashboardController.cs:2189-2252, 2282-2341, 2370-2429, 2456-2515, 31-44 | The analytics actions (Category/Account/Expense/Income/Cashflow) ToList the whole period as tracked entities with Includes, then GroupBy in memory. They also compute unused series (`DailyTrend` 2233, `DailyAccountActivity` 2322). | Aggregate in SQL with anonymous projections and `AsNoTracking`. Remove the unused series. | M | Low |
| FIN-P09 | Med | Perf | FinDashboardController.cs:166-230 | PL runs one FIN_CATEGORIES query per PL item (N+1) and sums in memory over the period's rows. | Load the item→category map once (`db.FIN_PL_ITEMS.Select(i=>new{i.ID, Cats=i.FIN_CATEGORIES.Select(c=>c.ID)})`). Aggregate `GroupBy(CategoryID, Direction)` in SQL. Or read from `V_FIN_PL_FACT_MONTHLY`, which already exists. | S | Low |
| FIN-P10 | Med | Perf | FinDashboardController.cs:2826-2859, 2869-2897, 2766-2773 | FinanceReportsHub runs 12 iterations × 2 cumulative full-history SUMs, one SUM per obligation, and one employee-name query per opening row. | Group accruals and payments by month in a single query each, then compute the running total in memory. Batch obligation payments with `GroupBy(SourceDocID)`. Batch employee names with `Contains`. | S | Low |
| FIN-P11 | Med | Perf/Sec | OpeningBalanceController.cs:1412-1438 (called at :30, :121, :347, :1171, :1206, :1227) | `EnsureOpeningContractBalanceTable` runs 4 SQL commands on **every** Index/Edit/Details/Delete request, including GETs: an OBJECT_ID check with CREATE TABLE, 2× ALTER, and an `UPDATE` over the whole table. The table is already in the EDMX. This mutates data on GET and needs DDL rights for the app login. | Delete the method. Move the DDL into a one-time migration script. | S | Low |
| FIN-P12 | Med | Perf | Views/OpeningBalance/Edit.cshtml:946-949, 127-166 | On page load, every saved AR/AP/forwarder line fires its own `fetch` for contracts. After AutoFill this can be hundreds of requests. Each row also renders the full customer/supplier list as `<option>`s, and the same lists are serialized again as JSON (594-597). | Embed the contract options for the counterparties already used in the model (one query, grouped). Render the counterparty select once as a `<template>` and clone it. Load contracts lazily on focus. | M | Low |
| FIN-P13 | Med | Perf | FinDashboardController.cs:1868-1874 | `CalculateCustomerOutstanding` materialises **all** incoming non-invoice transactions ≤ date, including non-customer ones, and only filters `CounterpartyType==1` in memory (1893). | Add `t.CounterpartyType==1 && t.CounterpartyID!=null` to the SQL WHERE. | S | Low |
| FIN-P14 | Med | Perf | Services/ContractBalanceService.cs:30-143 (used at PInvoicesController.cs:160-172) | Per invoice product: one contract-product query, 2 conversion lookups, a full P_INVOICE_PRODUCTS load, and one `ConvertPieceToCBMAsync` per invoice line. Called in a loop per product, so the cost is N×M. | Load contract products and invoice quantities for all products in one grouped query. Cache the conversion factors per product. | M | Low |
| FIN-P15 | Low | Perf | FinPlPlansController.cs:344-368 | BulkEdit runs one `FirstOrDefault` per item. | Load existing plans for the year/month into a dictionary once. | S | Low |
| FIN-P16 | Low | Perf | FinCategoriesController.cs:356, FinBalanceCategoriesController.cs:534, FinPlItemsController.cs:410 | `GetDescendantIds` does one query per tree node (recursive). | Load (ID, ParentID) once and walk the tree in memory. | S | Low |
| FIN-P17 | Low | Perf | All FinDashboard and lookup actions | No `AsNoTracking` on read-only queries. The Cashflow/Analytics rows and the lookup lists are all tracked. | Add `AsNoTracking()` and project to DTOs. | S | Low |
| FIN-P18 | Low | Perf | 8 FinDashboard views (Cashflow:160, AccountAnalytics, CategoryAnalytics, ExpenseAnalysis, FinanceReportsHub, FinancialOverview, IncomeAnalysis, LiabilitiesDashboard) | Each view loads `https://cdn.jsdelivr.net/npm/chart.js` **unpinned** (a breaking major version can ship silently). The `.stat-card` CSS block is copy-pasted into 4 views. | Host one pinned `chart.umd.min.js` in a bundle. Move the CSS to `Content/fin-dashboard.css`. | S | Low |

### Correctness, risk and security

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| FIN-B01 | High | Bug | All dashboards (e.g. FinDashboardController.cs:47-48, 1104-1118, 2098-2099) | `Amount` is summed **across currencies** with no FX conversion. FIN_TRANSACTIONS has no base-currency column. The page shows a "primary currency" label picked by most-used currency, so UZS and USD amounts are silently added together. | Add `AmountBase` (or a rate) at posting time, or group every KPI by CurrencyID. At minimum, filter by one currency and show the others separately. | L | Med |
| FIN-B02 | High | Bug | FinDashboardController.cs:1473-1576 vs 606-640 | `BuildBalanceCategoryDetails` (used by Liability details, DCF, LiabilitiesDashboard) has **no "Doimiy harajatlar qarzi" (recurring obligations) branch**, but BalanceSheet has one. The "Jami passivlar" total on BalanceSheet therefore disagrees with the details page it links to, and DCF understates liabilities. | Unify both engines (see split section). Until then, port the recurring-obligation branch. | S | Low |
| FIN-B03 | High | Bug | FinDashboardController.cs:465-528 vs 1474-1485 | For CalculationType=3 "Ta'minotchilardan qarzlar", BalanceSheet uses **account-based** PAYABLES balances (or a per-invoice fallback), while the details engine uses **document-based** FIFO. The two screens show different AP. | Same as above: one engine. | M | Med |
| FIN-B04 | High | Bug | FinDashboardController.cs:1050-1051 | DCF: `totalAssets` uses types `{1}` only, so it excludes Type 2 asset-column categories that AssetDetails includes (`{1,2}`). `totalLiabilities` uses `{2,3}`, so **equity (Type 3) is subtracted as a liability**. Net assets come out wrong. | Assets = `{1,2}` with includeAsset=true. Liabilities = `{2}` with includeAsset=false. Exclude Type 3. | S | Low |
| FIN-B05 | High | Bug | FinDashboardController.cs:1059-1064, 1104-1108 | EBITDA/DCF "operating income" = every Direction=1 transaction except invoice mirrors. It includes shareholder contributions (posted by ShareholderController.cs:392-411), loans and transfers, so valuations are inflated. | Drive income/expense from FIN_CATEGORIES.Type (1/2) or from PL items. Exclude `SourceDocType="ShareholderContribution"` and Type 3 categories. | S | Med |
| FIN-B06 | High | Bug | OpeningBalanceController.cs:117-313, 342-647 | Edit POST and AutoFill first **delete** all opening rows for the date (`DeleteOpeningByDate`), then insert through several `SaveChanges` and raw `ExecuteSqlCommand` calls **without a transaction**. Any exception midway loses that date's opening balances. | Wrap in `using (var tx = db.Database.BeginTransaction())`. Batch the inserts (one SaveChanges, or a TVP/bulk insert). | S | Low |
| FIN-B07 | High | Bug (verify intent) | Services/ReconciliationService.cs:71-75, 115-122, 170; 214-225, 249-256, 297 | Akt-sverki adds the opening contract balance **and** all invoices/payments since the beginning of time, with no `> openingDate` filter. Anything before the opening date is counted twice. The opening row is also dated `asOfDate` (it sorts last) instead of the snapshot date. | Return the snapshot date with the opening row. Filter invoices and payments to `> openingDate`. Date the row at the snapshot date. | S | Med |
| FIN-B08 | High | Bug | Supplier AP formula in 5 places: FinDashboardController.cs:1645-1674 (INCOMES.TotalPrice, else P_INVOICES.Amount); OpeningBalanceController.cs:420-444, 787-795, 1020-1030 (Amount × (1+contract VAT)); ReconciliationService.cs:77-90 (INCOMES else Amount); FinancePostingService.cs:195 (Amount, no VAT). Sales: FinancePostingService.cs:107 posts `TotalAmount` (no VAT) while AR uses `TotalWithVAT ?? TotalAmount` | The same supplier or customer debt is computed differently on the dashboard, in opening AutoFill, in Akt-sverki and in the FIN mirror rows. | One `CounterpartyBalanceService` with `GetInvoiceAmountWithVat(invoice)` and `IsPaymentFor(...)` rules, used everywhere. | M | Med |
| FIN-B09 | Med | Bug | ReconciliationService.cs:24-31 vs FinDashboardController.cs:2060-2076 | Akt-sverki finds the opening date from FIN_BALANCE_SNAPSHOTS (`OpeningBalance`/null). When only contract rows were saved (snapshots are written as `OpeningBalanceDetailed`, or not at all), it returns no date and the opening is ignored. The dashboard reads `MAX(SnapshotDate)` from FIN_OPENING_CONTRACT_BALANCES. | Use the same `GetLatestOpeningDateForBalanceType` from a shared service. | S | Low |
| FIN-B10 | Med | Bug | FinDashboardController.cs:2815 | `TotalPayrollDebt` is summed **after `.Take(30)`**, so the total understates the debt whenever more than 30 employees owe. | Sum before Take. | S | Low |
| FIN-B11 | Med | Bug | FinDashboardController.cs:173-175, FinPlPlansController.cs:106-107, 306-308, 348-349, 391-395 | Soft-deleted plans (`IsDeleted=true`) are still counted by PL, BulkEdit and ExportExcel. Create refuses to re-create a deleted plan ("already exists"). BulkEdit cannot set a plan back to 0 (`if (AmountPlan != 0)`). | Add `IsDeleted != true` everywhere. In Create/BulkEdit, revive or update the deleted row. Allow 0 to update an existing row. | S | Low |
| FIN-B12 | Med | Bug | ShareholderController.cs:24, FinDashboardController.cs:995-997 | Delete sets only `IsDeleted=true`, but Index and ShareholderValue filter only `IsActive`. Deleted shareholders stay listed and still receive a company-value share. | Filter `IsDeleted != true` (or also set IsActive=false on delete). | S | Low |
| FIN-B13 | Med | Bug | FinAccountsController.cs:70-81; Views/FinAccounts/Index.cshtml | Server-side Skip/Take (pageSize 20), but the view has no pager (CurrentPage/TotalRecords are unused) and DataTables paginates only those 20 rows. **Accounts beyond the first 20 can't be reached from the list.** | Remove the server paging (accounts are few), or render a pager. | S | Low |
| FIN-B14 | Med | Bug | Views/FinTransactions/Index.cshtml:102-142 | The "Amallar" `<th>` renders only when `changePermit`, but the `<td>` renders always. View-only users get a column-count mismatch and a DataTables warning. | Wrap the `<td>` in the same condition. | S | Low |
| FIN-B15 | Med | Bug/Sec | FinTransactionsController.cs:567-647 | Edit POST lacks the PurchaseInvoicePayment validation that Create has (309-325). It also lets a direct POST edit non-Manual (posted) transactions, including changing SourceDocType, even though the UI and Delete block that. | Shared `ValidateTransaction()`. Reject edits when the existing SourceDocType is not Manual. | S | Low |
| FIN-B16 | Med | Bug | FinDashboardController.cs:2604-2621 | AccountBalances splits a category snapshot **equally across all accounts** in that category (`Amount / cnt`), which produces made-up per-account balances. It uses `TxnDate >= snapDate` while every other report uses `> openingDate`, so opening-day transactions are counted twice relative to BalanceSheet. | Store opening balances per account (FIN_ACCOUNTS opening). Use `>` consistently. | M | Med |
| FIN-B17 | Med | Bug | FinRecurringObligationsController.cs + FinDashboardController.cs:611-613 vs 2864-2866 | An obligation can be saved with BalanceCategoryID NULL (the UI offers "Tanlamaslik"). BalanceSheet only counts obligations whose `BalanceCategoryID = category.ID`, so NULL ones vanish from the balance but still show in FinanceReportsHub. Currency is ignored. | Make the category required, or treat NULL as the "Doimiy harajatlar qarzi" category. | S | Low |
| FIN-B18 | Med | Bug | FinDashboardController.cs:529-563, 1486-1522 vs 2762-2796 | The balance sheet salary liability ignores the opening payroll rows (BalanceType 3). FinanceReportsHub adds them. Paid amounts also differ: Hub uses only CounterpartyType 6, while the liability uses the "oylik" category, CP 6 or HR_PAYROLL. | Use one payroll-liability function. | S | Low |
| FIN-B19 | Med | Sec | Views/FinDashboard/BalanceSheetLiabilityDetails.cshtml:186, 207-209 (and AssetDetails); Views/OpeningBalance/Edit.cshtml:687, 706; Views/Reconciliation/AktSverki.cshtml:197-244 | Stored XSS. User-entered Purpose/Comment/ContractNo/product names go into `innerHTML` / option HTML unescaped. AktSverki builds JS strings with `Html.Raw` after only escaping `'`, so a backslash or `</script>` in a supplier name breaks out. | Use `textContent` / `escapeHtml` (it already exists in Edit.cshtml:606). In Razor, `var x = @Html.Raw(Json.Encode(value));`. | S | Low |
| FIN-B20 | Med | Bug | Many `catch { }`: FinDashboardController.cs:425-447, 636-639, 2031-2034, 2071-2073, 2904-2908; FinRecurringObligationsController.cs:212; ReconciliationAdjustmentService.cs:55-58, 71-74; FinBalanceCategoriesController.cs:127-131, 436-440, 496-527 | Swallowed exceptions make a balance silently 0 or empty instead of failing. MapCategories even falls back to raw SQL on any exception. | Remove the "table might not exist" fallbacks (the tables are in the EDMX). Log and rethrow. | S | Low |
| FIN-B21 | Low | Bug | FinTransactionsController.cs:1358-1377 | `GetSourceDocLink` points at the non-existent controllers `Shareholders`, `WhIncome` and `WhExpense`, so the Details links 404. | Point to `Shareholder` and the real income/expense controllers. | S | Low |
| FIN-B22 | Low | Bug | Views/FinTransactions/Index.cshtml:41; FinTransactionsController.cs:160-163 | The Export link passes `ViewBag.AccountID`/`ViewBag.CategoryID`, which are never set, so the export ignores the account, category, direction and search filters. The Export button is shown only to change-permitted users although Export is a view action. | Set the ViewBags, or build the URL from `Request.QueryString`. | S | Low |
| FIN-B23 | Low | Bug | FinTransactionsController.cs:999-1001, 1197-1214 | Import: `ToDictionary` on Code/Name throws on duplicates (generic failure). There is no account-vs-currency check (Create enforces one at 302-306) and no duplicate-row detection. | Use `GroupBy().First()`, add the currency check, and optionally a duplicate hash. | S | Low |
| FIN-B24 | Low | Bug | ShareholderController.cs:298-308, 384-418 | AddSharePeriod adds a ModelState error and then redirects, so the error is lost. The 100% check excludes the shareholder's own overlapping periods. AddContribution does 3 SaveChanges with no transaction and creates a category inline. | Use TempData for the error, include own periods in the check, and wrap in a transaction. | S | Low |
| FIN-B25 | Low | Bug | FinCategoriesController.cs:40-47, FinBalanceCategoriesController.cs:36-43, FinPlItemsController.cs:32-39 | `else if (parentID == 0)` is unreachable (HasValue catches 0, so filtering `ParentID==0` returns nothing). The search/type filter plus `BuildTree(null)` drops matching children whose parent was filtered out, so searching for a child returns nothing. | Fix the branch order. When filtering, return a flat list. | S | Low |
| FIN-B26 | Low | Bug | OpeningBalanceController.cs:1486-1488 | `UpsertDetailedOpeningSnapshots` resolves the AP category by keyword `"qarz"` ordered by SortOrder, which can pick "Logistika qarzi" or "Doimiy harajatlar qarzi". The snapshots it writes (`OpeningBalanceDetailed`) are never read by any report (see Dead code). | Delete it, or resolve by exact name / config. | S | Low |
| FIN-B27 | Low | Sec | FinRecurringObligationsController.cs:21, 49, 75, 121, 154, 199 | Uses the `FinDashboard` permission for CRUD, so anyone with dashboard change rights can edit obligations. | Give it its own module key `FinRecurringObligations`. | S | Low |
| FIN-B28 | Low | Bug | Services/ContractBalanceService.cs:82-87 | Invoiced quantity includes **deleted** P_INVOICES. It goes through P_ORDERS.ContractID, so invoices with a direct ContractID and no order are missed. | Filter `IsDeleted != true` and use `P_INVOICES.ContractID`. | S | Low |
| FIN-B29 | Low | Bug | FinancePostingService.cs:143-164 | PostPurchaseInvoice finds the account by name (`Contains("Kreditorlar")`), not by code like PostSalesInvoice. With CurrencyID null and no account it **silently skips** posting. It auto-creates categories (race-prone) and calls SaveChanges several times. | Look up by code `PAYABLES_{cur}`, log on skip, wrap in the caller's transaction. | S | Low |

### Maintainability

| ID | Sev | Cat | Location | What | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| FIN-M01 | High | Maint | FinDashboardController.cs (3672 lines) | God controller. It holds 2 balance-sheet engines, AR/AP FIFO, payroll, valuation, inventory valuation and 13 DTO classes. | See the split section. | L | Med |
| FIN-M02 | Med | Maint | OpeningBalanceController.cs (1560) | Persistence, AutoFill calculations, Excel import/template, contract lookups and DDL are mixed in one class. | See the split section. | M | Low |
| FIN-M03 | Med | Maint | FinTransactionsController.cs (1379) | ~380 lines of Import parsing (the date-parse block is duplicated at 1026-1053 and 1058-1084), plus SelectList blocks repeated 4×. | See the split section. | M | Low |
| FIN-M04 | Low | Maint | Every Finance view (perm block + `Response.Redirect` in the else branch) | Views repeat the controller's permission check and call `Response.Redirect` from Razor, which can throw "headers already sent". | Rely on the controller check (or an `[RequirePermission("X")]` filter). Remove the view checks. | S | Low |

---

## Duplicates map

| # | Duplicated logic | Locations | Proposed single home |
|---|---|---|---|
| D1 | "Real cash" filter `(SourceDocType != "PurchaseInvoice" && != "SalesInvoice") \|\| null \|\| ""` | FinDashboardController.cs:37, 162, 1062, 1106, 1114, 2095, 2193, 2287, 2375, 2461, 1722, 814; ReconciliationService.cs:17-22; OpeningBalanceController.cs:455, 807 | `Services/Finance/FinQueries.RealCashTransactions(db)` (IQueryable extension) |
| D2 | Account balance `Sum(Direction==1?Amount:-Amount)` per account | FinDashboardController.cs:63-79, 296-302, 1240-1246, 2107-2120, 2594-2627, 3114-3195 (payables and receivables loops are copy-pasted) | `FinQueries.GetAccountBalances(db, ids, asOf, from?)` (one GroupBy), or the existing `sp_Fin_CashSummary` |
| D3 | Balance-sheet valuation engine | `BalanceSheet` 245-657 vs `BuildBalanceCategoryDetails` 1203-1583: opening-snapshot dict (264-289 / 1211-1236), name-matching account→category (306-318 / 1248-1260), case 1/2/3 switch, payroll, logistics | `Services/Finance/BalanceSheetService.Compute(db, asOf)` returning `List<BalanceCategoryDetailData>`; BalanceSheet = group by column |
| D4 | Mapped FIN_CATEGORIES for a balance category, with a try/catch raw-SQL fallback | FinDashboardController.cs:410-447, 1418-1452; FinBalanceCategoriesController.cs:113-131, 422-440, 468-528 | `BalanceCategoryRepository.GetMappedCategoryIds(id)` (EF navigation only) |
| D5 | Salary category ids `Name.ToLower().Contains("oylik")` | FinDashboardController.cs:542-548, 877-883, 1498-1504, 2818-2824, 2958-2964 | `FinQueries.SalaryCategoryIds(db)`; better a config flag on FIN_CATEGORIES |
| D6 | Approved payroll total (HR_PAYROLL_LINES with status/approved filters) | FinDashboardController.cs:532-540, 1488-1496, 2735-2750, 2833-2840, 2966-2974, 2999-3007; OpeningBalanceController.cs:496-506 | `PayrollLiabilityService.Accrued(db, from, to)` / `.ByEmployee(...)` |
| D7 | Recurring obligation accrual (months × amount − payments) | FinDashboardController.cs:611-634 vs 2864-2897 | `RecurringObligationService.GetDebts(db, asOf, categoryId?)`; also move the raw SQL CRUD into the EDMX |
| D8 | Logistics liability (opening + waybills − m.tash/t.tash payments) | FinDashboardController.cs:574-605 vs 1539-1575; OpeningBalanceController.cs:584-626 (per-contract version) | `LogisticsLiabilityService` |
| D9 | AR/AP outstanding (invoices − payments, contract key normalisation) | FinDashboardController.cs:1629-1819 (supplier), 1831-2011 (customer); OpeningBalanceController.cs:359-493 (AutoFill), 710-831 (per-contract totals), 1013-1030 (Excel); ReconciliationService.cs:71-112, 214-246 | `Services/Finance/CounterpartyBalanceService` with one VAT rule and one payment-matching rule (fixes B08) |
| D10 | Opening contract balance reads / latest opening date | FinDashboardController.cs:2022-2035, 2037-2044, 2060-2076; ReconciliationService.cs:24-53; OpeningBalanceController.cs:1318-1321; INSERT SQL duplicated at OpeningBalanceController.cs:297-311 and 630-644 | `OpeningBalanceRepository` (Get/Insert/LatestDate). Use the EDMX entity `FIN_OPENING_CONTRACT_BALANCES` instead of raw SQL |
| D11 | Subledger category name test | `IsDetailedSubledgerCategoryName` FinDashboardController.cs:2051 vs `IsDetailedSubledgerCategory` OpeningBalanceController.cs:1440 (they differ: "haq") | `BalanceCategoryDisplayHelper.IsSubledger(name)`; better a `IsSubledger` column |
| D12 | Primary-currency pick (most-used CurrencyID) | FinDashboardController.cs:85-108, 2153-2158, 2247-2252, 2336-2341, 2424-2429, 2510-2515, 3198-3204 | `FinQueries.PrimaryCurrency(db, query)`; obsolete once B01 is fixed |
| D13 | Year/Month SelectLists | FinDashboardController.cs:234-237; FinPlPlansController.cs:44-47, 71-74, 129-132, 162-165, 187-188, 320-323 | `Helpers/FinSelectLists.Years(sel)/Months(sel)` |
| D14 | CounterpartyType / Direction / SourceDocType SelectLists | FinTransactionsController.cs:133-158, 246-265, 354-373, 543-558, 628-643, 1331-1356 | `FinSelectLists` (static) |
| D15 | Category-tree CRUD (tree build, GetDescendantIds, circular-parent check, soft delete, SortOrder default) | FinCategoriesController (whole), FinBalanceCategoriesController (whole), FinPlItemsController (whole) | `TreeHelper.Build<T>(items, idSel, parentSel)` + `TreeHelper.Descendants`; shared `_TreeIndex` partial |
| D16 | MapCategories action and view | FinBalanceCategoriesController.cs:398-532 vs FinPlItemsController.cs:337-408; Views/FinBalanceCategories/MapCategories.cshtml ≈ Views/FinPlItems/MapCategories.cshtml (only model/controller names differ) | One `_MapCategories` partial with a model `{Title, PostAction, AllCategories, MappedIds}` |
| D17 | Counterparty / contract lookup JSON | FinTransactionsController.cs:381-499; OpeningBalanceController.cs:660-707, 834-856; also PInvoicesController.GetContractsBySupplier, POrdersController.GetContractsBySupplier | `LookupController` (shared, AsNoTracking projections) |
| D18 | FinTransactions Create/Edit views (~180 lines of identical counterparty/contract JS plus the form) | Views/FinTransactions/Create.cshtml vs Edit.cshtml | `_FinTransactionForm.cshtml` partial + `Scripts/fin-transaction-form.js` |
| D19 | Balance details pages + movement popup JS | Views/FinDashboard/BalanceSheetAssetDetails.cshtml vs BalanceSheetLiabilityDetails.cshtml | One `BalanceSheetDetails(side)` action/view + `fin-balance-movements.js` |
| D20 | Expense vs Income analysis (action and view) | FinDashboardController.cs:2358-2441 vs 2444-2527; ExpenseAnalysis.cshtml vs IncomeAnalysis.cshtml (310 lines each, ~80% the same) | `DirectionAnalysis(direction)` + one view; the two DTO pairs (ExpenseBy*/IncomeBy*) become one |
| D21 | FinRecurringObligations Create/Edit views; Shareholder Create/Edit views | Views/FinRecurringObligations/Create vs Edit; Views/Shareholder/Create vs Edit | `_Form` partials |
| D22 | Excel export boilerplate (`LicenseContext`, MemoryStream, File) | FinTransactionsController.cs:775-812, 846-941; FinPlPlansController.cs:397-422; OpeningBalanceController.cs:917-962 | `ExcelExportHelper.ToFile(name, Action<ExcelPackage>)` (cross-module) |
| D23 | Nav-property "fix-up" after Include | FinTransactionsController.cs:99-122, 196-212; FinAccountsController.cs:124-133; ShareholderController.cs:81-91 | Delete it (the Include already loads them) |

---

## Dead code

| Item | Location | Confidence |
|---|---|---|
| `sp_Fin_CashSummary` function import + `sp_Fin_CashSummary_Result` | Models/DBTHSNModel.Context.cs:243-253 | High: never called. **Reuse it** for Cashflow/FinancialOverview balances instead of deleting it |
| `V_FIN_PL_PLAN_VS_FACT_MONTHLY` | Models | High: unused. `V_FIN_PL_FACT_MONTHLY` is used only by HomeController; the PL action should use it |
| `ContractBalanceService.GetAllContractBalancesAsync` | Services/ContractBalanceService.cs:150-165 | High |
| Empty `if (contractProduct.UnitID != 11) { }` | Services/ContractBalanceService.cs:74-78 | High |
| `FinTransactionsController.GetContractsBySupplier` ("backward compat") | FinTransactionsController.cs:504-508 | High: no caller in views/JS |
| `EnsureOpeningContractBalanceTable` (table is in the EDMX) | OpeningBalanceController.cs:1412-1438 | High |
| `UpsertDetailedOpeningSnapshots` writes `SourceType="OpeningBalanceDetailed"`; no report reads that SourceType (dashboard filters `"OpeningBalance"`, AccountBalances filters `"OpeningBalance"`/null) | OpeningBalanceController.cs:1455-1518 | High: write-only data |
| Raw-SQL fallbacks for `FIN_BALANCE_CATEGORY_CATEGORIES` (the relationship exists in the EDMX) | FinDashboardController.cs:425-447, 1433-1452; FinBalanceCategoriesController.cs:496-528 | High |
| ViewBag series not used by their views: `DailyTrend` (2233-2244), `DailyAccountActivity` (2322-2333), LiabilitiesDashboard `TotalPayables`/`TotalReceivables`/`PayrollLiability` and their queries (2949, 2955, 2966-2983), `payablesTransactions`/`receivablesTransactions` (2928-2944, used only for the currency) | FinDashboardController.cs | High |
| `accountBalances` dictionary in BalanceSheet (used only by the case-3 AP branch) | FinDashboardController.cs:294-302 | Med |
| `GetLatestOpeningDate` (only a fallback) and `CategorySnapshotInfo.IsOpening` (never read) | FinDashboardController.cs:2037, 1626 | Med |
| `OpeningContractBalanceLite.TotalAmount/PaidAmount` in ReconciliationService | ReconciliationService.cs:325-330 | Low |
| Unused action params `sortOrder, page, pageSize` | FinTransactionsController.cs:19-21 | High |
| `else if (parentID == 0)` unreachable | FinCategories:44, FinBalanceCategories:40, FinPlItems:36 | High |
| `loadAccountCurrency()` empty JS function | Views/FinTransactions/Create.cshtml (scripts) | High |
| `FinAccountsController.Transactions` (redirect-only action; the views link to it) | FinAccountsController.cs:346-359 | Low: keep or replace the links with a direct `FinTransactions/Index?accountID=` |

---

## Suggested split of god-controllers

### FinDashboardController (3672 lines)
Keep the controller name `FinDashboard`, because the _Layout menu hardcodes `/FinDashboard/...` (lines 1037-1048, 1334-1349). Turn it into thin actions over services, and optionally split it into partial-class files. Moving the logic out removes ~2600 lines.

| New unit | Moves from FinDashboardController | Notes |
|---|---|---|
| `Services/Finance/FinQueries.cs` (static) | `ActiveFinTransactions` 3225, the real-cash filter (D1), account balances (D2), `SalaryCategoryIds` (D5), `GetPayrollPaymentQuery` 1585, primary currency (D12), `IsSalaryLiabilityCategory` 3230 | AsNoTracking IQueryables |
| `Services/Finance/CounterpartyBalanceService.cs` | `CalculateSupplierOutstanding` 1629-1819, `CalculateCustomerOutstanding` 1831-2011, `GetOpeningContractBalances` 2022, `GetLatestOpeningDate*` 2037-2076, the related private DTOs 1603-1627, 1821-2020 | Also used by OpeningBalance AutoFill/totals and ReconciliationService (D9/D10). Add a `MonthEndSeries(from,to)` for P01 |
| `Services/Finance/LiabilityServices.cs` | payroll liability (D6), recurring obligations (D7), logistics (D8) | |
| `Services/Finance/InventoryValuationForBalance.cs` | 3241-3520 (Tayyor maxsulot / Xomashyo lines, warehouse resolution, unit price) | Fix the N+1 (P03) |
| `Services/Finance/BalanceSheetService.cs` | `BuildBalanceCategoryDetails` 1203-1583 **as the single engine**, plus the movements query from `BalanceCategoryMovements` 705-955 | `BalanceSheet` (245-657) becomes ~15 lines: `Compute()` + group by `IsAssetColumn`. Fixes B02/B03 |
| `Services/Finance/ValuationService.cs` | `CalculateEbitda` 1101, `CalculateEbitdaValuation` 1033, `CalculateDcfValuation` 1047, `GetAverageTannarxMarginPercent` 1086 | Fix B04/B05 |
| `ViewModels/Finance/FinDashboardModels.cs` | DTOs 3523-3671 (AccountBalanceData, …, RecurringObligationRow) | Views reference `tahsinERP.Controllers.Finance.*`; update the namespaces |
| Controller partials (optional): `FinDashboardController.Cash.cs` (Cashflow, FinancialOverview, Category/AccountAnalytics, DirectionAnalysis, AccountBalances), `.Statements.cs` (PL, BalanceSheet, details, movements, EBITDA), `.Liabilities.cs` (LiabilitiesDashboard, FinanceReportsHub), `.Valuation.cs` (ShareholderValue) | | If separate controllers are preferred, add `[RoutePrefix("FinDashboard")]` attribute routes so the URLs stay the same |

### OpeningBalanceController (1560)
- `Services/Finance/OpeningBalanceService.cs`: `Save(model)` (117-313 with a transaction, B06), `AutoFill(date)` (342-647, computing through `CounterpartyBalanceService` instead of its own formulas), `Delete(date)`, `BuildViewModel` (1225-1410), `UpsertDetailedOpeningSnapshots` (delete or keep).
- `Services/Finance/OpeningBalanceExcelService.cs`: template 910-963 and import 967-1152.
- Lookup endpoints 660-907 move to the shared `LookupController` (D17). Contract totals use `CounterpartyBalanceService.ContractTotals(type, cp, contract, date)`.
- Delete `EnsureOpeningContractBalanceTable`.
- The controller ends up around 250 lines.

### FinTransactionsController (1379)
- `Services/Finance/FinTransactionImportService.cs`: 948-1329 (with one `ParseExcelDate(cell)` helper replacing the duplicated block).
- `Services/Finance/FinTransactionExcelService.cs`: Export 748-814 and Template 837-943.
- `Helpers/FinSelectLists.cs`: all SelectList builders (D13/D14) and `PopulateFormLists(transaction)`, which removes the 4× copies.
- `GetCounterparties` and `GetContractsByCounterparty` move to `LookupController`.
- Index switches to server-side DataTables (P05).
- The controller ends up around 350 lines.

---

## Cross-module notes (shared / core layer)

1. **No base-currency amount on FIN_TRANSACTIONS**, so every module that reports money (CRM/SRM dashboards too) faces the same FX issue as B01. Decide on an `AmountBase` + `Rate` column at the core level.
2. **FIN "mirror" postings** from SalesInvoicesController (421, 893, 1128, 1223), PInvoicesController (443, 891, 1056) and WaybillPaymentController (199) go through FinancePostingService with multiple internal SaveChanges and no transaction shared with the caller. The SalesInvoice mirror uses TotalAmount without VAT. These postings should take part in the caller's transaction (`db.Database.CurrentTransaction`).
3. `GetContractsBySupplier` exists in PInvoicesController:1090, POrdersController:1054 and FinTransactionsController:504. Contract/counterparty lookups belong in one shared `LookupController`.
4. Excel export/import boilerplate (EPPlus LicenseContext, MemoryStream→File, multi-format date parsing) is repeated across modules and should live in a shared `ExcelHelper`.
5. View-level permission checks plus `Response.Redirect` in Razor appear in every Finance view. RoleHelper is cached, so the cost is small, but a shared `[ModulePermission("X", Change=true)]` action filter would remove ~130 duplicated blocks.
6. Chart.js is loaded unpinned from a CDN per view. Add one pinned bundle in `_Layout` or BundleConfig for all dashboards (CRM/SRM/HR dashboards probably do the same).
7. Runtime DDL (`EnsureOpeningContractBalanceTable`) and "table may not exist" try/catch fallbacks mean schema is being managed from controllers. Introduce migration scripts under `/Scripts/migrations` and remove these.
8. Soft delete is inconsistent: some code sets IsActive=false, some IsDeleted=true, and readers filter one or the other (FIN_CATEGORIES, FIN_ACCOUNTS, SHAREHOLDERS, FIN_PL_PLANS). Adopt one convention (SoftDeleteReferenceService exists in Services). The same applies to the stored-XSS pattern of `innerHTML` with server JSON, which appears in other modules' popups as well.

---

## Recommended order of work (quick wins first)

1. **Data-safety and visible errors (S, low risk):** B06 transaction around OpeningBalance save/AutoFill; B10 TotalPayrollDebt sum; B11 deleted P&L plans; B12 deleted shareholders; B13 FinAccounts paging; B14 Index column mismatch; B21 broken source links; B19 XSS escaping.
2. **Cheap performance wins (S):** P11 delete EnsureOpeningContractBalanceTable; P03 bulk product load; P02 grouped account balances (6 places, or `sp_Fin_CashSummary`); P06 FinancialOverview lazy load; P13 SQL filter in CalculateCustomerOutstanding; P09 PL N+1; P10 ReportsHub loops; remove the unused computations (Dead code list); add AsNoTracking.
3. **P05 FinTransactions server-side DataTables** with a default date range (M).
4. **Unify the balance-sheet engine** into `BalanceSheetService` (fixes B02/B03, P04; then B04/B05 in ValuationService) (M).
5. **CounterpartyBalanceService**: one AR/AP formula (B08, B09, B07) shared by the dashboard, OpeningBalance and Akt-sverki, plus the month-end series for **P01 LiabilitiesDashboard** (M/L). Agree the VAT rule with the business first.
6. Split OpeningBalance and FinTransactions into services (M02/M03); shared LookupController and FinSelectLists (D13, D14, D17); merge the duplicated views (D16, D18-D21) and the tree controllers (D15).
7. Strategic: multi-currency base amounts (B01); store per-account opening balances (B16); move the dashboard aggregates to SQL views/SPs (monthly fact tables) with short-lived caching.
