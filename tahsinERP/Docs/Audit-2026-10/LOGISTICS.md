# LOGISTICS module audit

Root: `C:\Work\SCHAIN\tahsinERP\tahsinERP\tahsinERP`. The audit was read-only. Line numbers refer to the current `master` branch.

## Scope

I read 58 files in full.

**Controllers (9):**
- `Controllers/Logistic/WaybillController.cs` (1389 lines)
- `WaybillPaymentController.cs` (671)
- `LogisticsDashboardController.cs` (404)
- `FTransportTypeController.cs` (375)
- `ForwarderController.cs` (365)
- `FContractTariffController.cs` (302)
- `FContractController.cs` (289)
- `TransportLoadSimulationController.cs` (60)
- `Controllers/Api/TransportLoadApiController.cs` (44)

**Services (3):**
- `Services/LogisticsMonthlyReportService.cs`
- `Services/LogisticsTerms.cs`
- `Services/TransportLoadCalculator.cs`

**Models (9):**
- `FORWARDER`
- `F_CONTRACTS`
- `F_CONTRACT_DOCS`
- `F_CONTRACT_TARIFFS`
- `F_TRANSPORT_TYPES`
- `F_WAYBILLS`
- `F_WAYBILL_DOCS`
- `F_WAYBILL_PAYMENTS`
- `F_WAYBILL_PAYMENT_SPLITS`

**Views (37):**
- Waybill (6): Index, Create, Edit, Details, Delete, UploadExcel
- WaybillPayment (5): Index, Create, Edit, Details, Delete
- LogisticsDashboard (5): Index, MonthlyReport, LanePerformance, Reconciliation, ForwarderComparison
- `Logistic/TransportLoadSimulation/Index` (658 lines)
- FTransportType (5)
- Forwarder (5)
- FContract (5)
- FContractTariff (5)

**Related code checked:**
- `Services/SoftDeleteReferenceService.cs` (logistics hooks)
- `Services/FinancePostingService.PostWaybillPayment`
- `Filters/ValidateAntiForgeryHeaderAttribute`
- `Scripts/csrf.js`
- `Models/RoleHelper` (permission cache)
- Script ordering in `Views/Shared/_Layout.cshtml`

## Findings

Severity: Critical / High / Med / Low. Effort: S / M / L.

### Performance

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| LOG-P01 | High | Perf | WaybillController.cs:1020-1070 (`BuildWaybillViewModel`) | Loads 9 lookup tables in full on every Create GET, Edit GET and failed POST. That is all non-deleted P_INVOICES, **all** SALES_INVOICES (no IsDeleted filter), all S_ORDERS, P_ORDERS, SALES_TTNK_HEADERS, FORWARDERS, F_CONTRACTS, F_TRANSPORT_TYPES and CURRENCIES. Invoices, orders and TTNKs grow without limit, so the page gets heavier every month (thousands of `<option>`s plus select2 initialisation). | Make invoice, order and TTNK pickers select2 AJAX endpoints (`/Waybill/SearchInvoices?q=`, Take 20). Pre-render only the currently selected option. Keep the small tables (forwarders, contracts, currencies) and cache them (see D03). | M | Low |
| LOG-P02 | High | Perf | WaybillController.cs:23-157, 1235-1275 | Index loads **every** non-deleted waybill with 5 Includes and no paging. The `page`/`pageSize` parameters are never used. It then sends `IN (all ids)` twice to compute paid amounts, so EF6 builds a large inlined IN list and the query is slow to compile. DataTables then pages on the client, so the whole table goes to the browser. | Use server-side DataTables (JSON endpoint, Skip/Take, projection to `WaybillIndexItemViewModel` inside SQL). Compute paid amounts with one grouped subquery joined on the page's IDs. Add `AsNoTracking`. | M/L | Med |
| LOG-P03 | Med | Perf | WaybillController.cs:302-311, 327-331 | Details does `.Include(F_WAYBILL_DOCS)`, which **loads byte[] document blobs**. It also includes `F_WAYBILL_PAYMENTS` and `P_INVOICES`; the view uses none of the three. Lines 327-331 load **all split payer IDs in the whole table** into memory, only to use them in `!Contains`. | Drop the three Includes. Replace `allSplitPaymentIds` with the `NOT EXISTS` subquery already used in `BuildWaybillPaidLookup` (line 1244), or call the shared ledger service (D01). | S | Low |
| LOG-P04 | Med | Perf/Bug | WaybillController.cs:1162-1191 | `GenerateNextWaybillNo` pulls **every WaybillNo** into a HashSet on each Create, then runs 2 more queries. There is no lock and no unique index, so two users creating at the same time get the same `TB_xxxx`. | Use a SQL `SEQUENCE` or a numbering table updated with `UPDLOCK`. Add a unique filtered index on `WaybillNo WHERE IsDeleted=0`. Catch the duplicate-key error and retry. | M | Med |
| LOG-P05 | High | Perf | LogisticsMonthlyReportService.cs:23-57, 78-118 | The monthly report loads **all waybills, all payments and all forwarder FIN transactions ever recorded** (no upper date bound), then runs a per-forwarder `Where` loop in memory (O(F×N)). Cost grows with total history. | Aggregate in SQL. Group by ForwarderID and use conditional sums: `Sum(IssueDate < start ? Amount : 0)` and `Sum(IssueDate >= start && <= end ? Amount : 0)`. Do the same for payments and FIN. Apply `<= periodEnd` in SQL. This gives 3 small result sets. | M | Low |
| LOG-P06 | High | Perf | LogisticsDashboardController.cs:283-330 + Reconciliation.cshtml | Reconciliation has no date filter and no paging. It loads all waybills twice (with Includes), all paid IDs (`IN` over all waybills), and all SALES_INVOICES without a waybill (no IsDeleted or date filter). It renders everything into plain `<table>`s. On a mature database this page is the slowest in the module. | Add a date range (default: current month or quarter). Use `NOT EXISTS` subqueries in SQL. Project to small view models. Use Take(500) or server-side DataTables. Use the shared paid logic (D01). | M | Low |
| LOG-P07 | Med | Perf | LogisticsDashboardController.cs:34-165, 238-270, 350-392 | Dashboard Index, LanePerformance and ForwarderComparison materialise full F_WAYBILLS entities with Includes and do all grouping and sums in memory. `MonthlyTrend` (155-165) is computed but **never rendered**. | Project to `{ForwarderID, From, To, Amount, GrWeight, DistanceKm, ...}` with `AsNoTracking`, or GroupBy in SQL. Remove MonthlyTrend or render it. | M | Low |
| LOG-P08 | Med | Perf | ForwarderController.cs:99-179 | Forwarder Details loads all of the forwarder's waybills and payments as full entities to compute counts and sums, then takes 10 "recent" rows in memory. | SQL aggregates plus `OrderByDescending().Take(10)` queries. | S/M | Low |
| LOG-P09 | Med | Perf | WaybillPaymentController.cs:20-136 | Index loads all payments with 3 Includes and no paging. Line 51 runs `splitRows.Where(s => s.PayerID == item.ID)` inside a `foreach` over all payments (O(n×m) in memory). Lines 83-115 copy the paid computation again. | Server-side paging. `splitRows.ToLookup(s => s.PayerID)`. Shared ledger service (D01). | M | Low |
| LOG-P10 | Med | Perf | WaybillPaymentController.cs:503-537 | `PopulateFormBags` loads **all waybill entities (every column)** to fill one ListBox, plus forwarders, currencies and accounts. It runs on Create/Edit GET and on every failed POST. | Project to `{ID, WaybillNo}`. Restrict to waybills with outstanding > 0, or use select2 AJAX. | S | Low |
| LOG-P11 | Low | Perf | WaybillPaymentController.cs:253-273, 594-614 | `GetWaybillPaidTotal` (2 queries each) is called inside a per-waybill loop in Details and in `AllocateAcrossWaybills`: an N+1 pattern. | Use one batch lookup (`BuildWaybillPaidLookup`-style) moved into the shared service. | S | Low |
| LOG-P12 | Med | Perf | Views/Logistic/TransportLoadSimulation/Index.cshtml:493-516, 531-535 | Creates **one Mesh plus one cloned material per box**. Small products give thousands of meshes and the browser tab freezes. Each simulation also calls `window.animate()` again, so **extra requestAnimationFrame loops pile up** and CPU use grows with every click. | Use `THREE.InstancedMesh` with one shared material. Start `animate()` once; keep the rAF id and cancel it before starting again. | S | Low |
| LOG-P13 | Low | Perf | TransportLoadSimulationController.cs:25-37; view lines 48-59 | Every product is rendered into the dropdown with `data-length/width/height` attributes that nothing reads (the server recomputes). | select2 AJAX product search. Drop the data attributes. | S | Low |
| LOG-P14 | Low | Perf/Bug | Forwarder/Index.cshtml:102, FContract/Index.cshtml:111, FTransportType/Index.cshtml:106 | Each page loads **another jQuery 3.5.1 from googleapis** in the body. The layout already loads jQuery twice (`_Layout.cshtml:1743` bundle and `:1779` assets), so these pages end up with 3 copies. Plugins attach to whichever copy loads last. The `refreshDataTable()` function there reads `CustomerID`/`SourceID` elements that don't exist (dead). | Delete those `<script>` blocks. | S | Low |
| LOG-P15 | Low | Perf | TransportLoadSimulation/Index.cshtml:222, plus about 30 `console.log` calls | `OrbitControls.js` is downloaded but never used (the page writes its own controls). There is debug logging throughout. | Remove both. | S | Low |
| LOG-P16 | Low | Perf | All read actions in module | No `AsNoTracking()` anywhere in Index, Details, the dashboards, MonthlyReport or the dropdown loaders. | Add `AsNoTracking()` to read-only queries. | S | Low |

### Bugs and correctness

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| LOG-B01 | High | Bug | WaybillPaymentController.cs:193 (vs. 452-459 in Edit) | **Create allocates split payments wrongly.** The payment is saved first, with `WaybillID = first selected`. Then `SavePaymentSplits` is called **without `excludePaymentId`**. `GetWaybillPaidTotal` counts the new payment as already paid on the first waybill, because no split row references it yet. Example: W1=100 and W2=100 selected, payment 100. W1 looks fully paid, so all 100 goes to W2 and W1 stays unpaid. Edit passes the ID correctly. | Pass `model.ID` as `excludePaymentIdForOutstanding` in Create. Then recompute the existing multi-waybill payments (a one-time data fix script). | S | Low |
| LOG-B02 | High | Bug/Data | SoftDeleteReferenceService.cs:143-159, 261-267; called at ForwarderController.cs:296, FContractController.cs:257, FTransportTypeController.cs:348 | **Soft delete wipes history.** Deleting a forwarder, contract or transport type sets `ForwarderID`, `ContractID` or `TransportTypeID` to **NULL on every historical waybill**. MonthlyReport (`w.ForwarderID.HasValue`) and the dashboards then drop that debt. Payments still carry `ForwarderID`, so balances end up skewed. Restoring the record from DeletedRecords cannot re-link the waybills. | For soft delete, keep the FKs and only hide the record in dropdowns. Either block the delete when active references exist, or move the nulling to the hard-delete path only. | S | Med |
| LOG-B03 | High | Bug | Waybill/Index.cshtml:195-227; Waybill/Create.cshtml:312-422; Waybill/Edit.cshtml (same block) | Inline `<script>` blocks call `$(document).ready(...)` when the body is parsed. The layout renders the body at `_Layout.cshtml:1729` but loads jQuery only at `:1743`, so they throw `ReferenceError: $ is not defined`. Expected effect: the Waybill Index DataTable never initialises, and in Create/Edit the contract→tariff loading, amount auto-calculation and invoice autofill never get bound. The simulation view has a "wait for jQuery, retry" workaround, which supports this. **Needs a browser check.** | Move these scripts into `@section scripts { }`, which renders after the libraries at `_Layout.cshtml:1925`. | S | Low |
| LOG-B04 | Med | Bug | LogisticsDashboardController.cs:67-74, 309-317; ForwarderController.cs:103-119; LogisticsMonthlyReportService.cs:64-68 | **"Paid" means something different on each screen.** The Dashboard, Reconciliation, Forwarder Details and MonthlyReport ignore `F_WAYBILL_PAYMENT_SPLITS`, so a multi-waybill payment is credited in full to its first waybill. Reconciliation lists split-paid waybills as "without payment". Forwarder Details debt excludes manual FIN forwarder payments, while the Dashboard and MonthlyReport include them. Users see a different debt for the same forwarder on each screen. | One `WaybillLedgerService` (D01) used by all screens, with one definition: direct (non-split) payments + split allocations + manual FIN payments by forwarder. | M | Med |
| LOG-B05 | Med | Bug | WaybillController.cs:389-393 (Edit GET), 452-473 (Edit POST) | The waybill has no TariffID column. Edit GET **guesses** the newest active tariff for contract + transport type, and Edit POST overwrites `Rate`, `FreightBasis` and `TransportTypeID` from it. Any edit after a tariff change silently rewrites the historical rate. If the old tariff was deactivated, the waybill cannot be saved at all ("Tarif tanlash majburiy"). | Add a `TariffID` column to F_WAYBILLS and persist it. Re-apply the tariff only when TariffID actually changed. Allow saving without an active tariff on Edit. | M | Med |
| LOG-B06 | Med | Bug | WaybillController.cs:859-880 vs. Create.cshtml:631-655; UploadExcel 759-796 | The server's `CalculateAmount` ignores `MinimumCharge`, but the client JS applies it, so totals can differ between the form and what is saved. Excel import never calculates Amount and does no tariff validation. | One `FreightCalculator.Calculate(basis, rate, weight, km, minCharge)` in Services, used by Create, Edit, Import and (via a JSON endpoint) the UI. | S | Low |
| LOG-B07 | Med | Bug | WaybillController.cs:217-220, 480-534 | Create and Edit never check for a duplicate WaybillNo (only the Excel import does). Together with the numbering race in P04, duplicates are possible. | Unique filtered index plus a ModelState check. | S | Low |
| LOG-B08 | Med | Bug | WaybillPaymentController.cs:179-205 (Create), 440-481 (Edit), 330-361 (Delete) | Multi-step writes without a transaction. Create does payment save → delete and save splits → FIN posting save (3-4 SaveChanges); Delete does 3. A failure partway leaves a payment without splits (counted as direct on the first waybill) or without a FIN entry. FIN posting exceptions are **only logged and swallowed** (201-205, 477-480), so the user sees success while finance is out of sync. | Wrap each action in `db.Database.BeginTransaction()`. Make `PostWaybillPayment` part of the same transaction and show an error to the user if it fails. | M | Med |
| LOG-B09 | Med | Bug | WaybillController.cs:853, FContractController.cs:286, ForwarderController.cs:324, FTransportTypeController.cs:371, WaybillPaymentController.cs:368 | On error, the Delete POST returns `View()` with a **null model**. The Delete views use `Model.WaybillNo`, `Model.ID` and so on, so the user gets a NullReferenceException (yellow screen) instead of the error message. | Reload the entity and return `View(entity)`, or redirect to Index with a TempData error. | S | Low |
| LOG-B10 | Med | Bug | TransportLoadCalculator.cs:34-41 vs. FTransportTypeController.cs:36-42, 172-194 | **Units conflict for `F_TRANSPORT_TYPES.Int*`.** The calculator and the simulation UI treat them as **mm**. FTransportType Index/Details divide by 100 (treating them as **cm**) and compute pallets as 120×100 cm. One of the two results is wrong by 10× (pallets) or 1000× (CBM). `UnitID` exists on the table but nobody uses it. | Pick one canonical unit (mm). Convert with `UNIT` on input. Put the CBM and pallet calculations in TransportLoadCalculator. | S | Med |
| LOG-B11 | Med | Bug | TransportLoadSimulation/Index.cshtml:579-600 and 603-624 | The jQuery click handler and `btn.onclick` are **both** attached to the same button. `stopPropagation` does not stop other handlers on the same element, so every click makes 2 AJAX calls and 2 renders. That doubles the leak in P12. | Keep one handler. | S | Low |
| LOG-B12 | Med | Bug | WaybillController.cs:595-813 | Import problems: (a) `SaveChangesAsync` (803) has no try/catch, so one bad row (string length, FK) fails the whole file with a yellow screen and no row number. (b) `GetCellDouble`/`GetCellInt` parse `cell.Text` (formatted and culture-dependent), so `1,875,000.00` silently becomes null. (c) The template formats columns 36-37 as dates, but the dates are columns 35-36, so Description gets a date format. (d) The sample row has no ContractNo or TransportType, so importing the downloaded template as-is fails. | Wrap the save and map entity errors back to rows. Parse `cell.Value` (double/DateTime) with InvariantCulture as a fallback. Fix the column indexes. Fill the sample row. | S | Low |
| LOG-B13 | Med | Bug | Dashboard Index 54-113, MonthlyReport service, ForwarderController 114-147, WaybillPayment outstanding calculations | Amounts and payments are **added up across currencies** (UZS + USD) without conversion. Waybill outstanding = waybill amount (currency A) − payments (currency B). | Group KPIs by CurrencyID or convert with a rate table. At minimum, validate that a payment's currency equals the waybill's currency. | M | Med |
| LOG-B14 | Low | Bug | e.g. WaybillController.cs:39, 141, 671, 1024; ForwarderController.cs:30; FContractController.cs:53; LogisticsMonthlyReportService.cs:24, 72 | `FORWARDER.IsDeleted` and `F_WAYBILLS.IsDeleted` are `bool?`. `== false` excludes NULL rows, so legacy or imported rows with NULL vanish from lists. F_TRANSPORT_TYPES correctly uses `== false \|\| == null`. | `IsDeleted != true` everywhere, or make the column NOT NULL DEFAULT 0. | S | Low |
| LOG-B15 | Low | Bug | WaybillController.cs:143, 1030; TransportLoadSimulationController.cs:42; TransportLoadCalculator.cs:28 | Soft-deleted transport types still appear in Waybill filters, Create/Edit and the simulation. | Add the IsDeleted filter (through the shared dropdown provider, D03). | S | Low |
| LOG-B16 | Low | Bug | WaybillController.cs:482, 530; FContractController.cs:170, 180 | Edit POST finds the row without an IsDeleted check and then sets `IsDeleted = false`. Posting to the Edit URL of a deleted record silently restores it. | Return 404 for deleted rows and don't touch IsDeleted in Edit. | S | Low |
| LOG-B17 | Low | Bug | FContract/Create.cshtml:62, 70; FContract/Edit.cshtml:60, 68; FContract/Index.cshtml:85 | Two inputs share `id="dateInput"`, so flatpickr binds only IssueDate. Create uses format `d.m.Y` and Edit uses `d-m-Y`, so binding depends on server culture. Index does `item.CURRENCy.Shortname` although `CurrencyID` is nullable, which throws a NullReferenceException. | Use separate ids or a class selector, one ISO format (`Y-m-d`), and `?.`. | S | Low |
| LOG-B18 | Low | Bug | FTransportTypeController.cs:271 | `UnitID` update is commented out, so the unit dropdown on Edit is silently ignored. | Restore the line, or make the dropdown read-only. | S | Low |
| LOG-B19 | Low | Bug | LogisticsDashboardController.cs:204 | MonthlyReport checks the `Waybill/Index` permission instead of its own route, unlike the other dashboard actions. | Use the controller/action route values, or add a dedicated permission. | S | Low |
| LOG-B20 | Low | Bug | WaybillPaymentController.cs:153-217, 412-501 | No validation that `Payment_Amount > 0`, that the payment's forwarder matches the selected waybills' forwarder, or that the payment doesn't exceed the outstanding amount. The surplus is silently spread "equally" (621-630). | Validate in ModelState. Show the allocation preview. | S | Low |

### Security

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| LOG-S01 | Low | Sec | FContractController.cs:73 | `contract.CompanyID = 1` is hard-coded. It is harmless with DB-per-tenant but wrong for any multi-company setup. | Take it from the current company context. | S | Low |
| LOG-S02 | Low | Sec | Api/TransportLoadApiController.cs:19-41; WaybillController.cs:887, 938, 973 | The JSON endpoints have no RoleHelper check (only the global `[Authorize]`), so any logged-in user can read supplier and customer addresses and tariffs. TransportLoadApi returns `ex.Message` to the client. | Add a permission attribute (see cross-module notes) and return a generic error. | S | Low |
| LOG-S03 | Low | Sec | WaybillPaymentController.cs:153, 412; ForwarderController.cs:48, 207; FContractController.cs:60, 160; FContractTariffController.cs:72, 171 | POST actions bind EF entities directly, which allows over-posting (ID, IsDeleted, CreatedDT and navigation properties). | Use view models or `[Bind(Include=...)]`. | M | Low |
| LOG-S04 | Low | Sec | FContractController.cs:1-2, 13; ForwarderController.cs:1, 6; FTransportTypeController.cs:9 | Leftover usings: `Microsoft.AspNetCore.Mvc` in an MVC5 controller, `DocumentFormat.OpenXml.EMMA`, `System.Web.UI.WebControls`, `DocumentFormat.OpenXml.Drawing.Charts`. Any one of them can cause type-name clashes (`Controller`, `HttpPost`). | Remove them. | S | Low |

### Maintainability

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| LOG-M01 | Med | Maint | WaybillController.cs (1389 lines) | God controller: CRUD, Excel import and template, numbering, tariff matching, invoice autofill, paid ledger, enum parsing. | See the split below. | L | Med |
| LOG-M02 | Low | Maint | Throughout | Magic numbers: Direction 0/1/2, FreightBasis 0-3, PayerType 0-3, split PayerType **99** (repeated in WaybillController:21, WaybillPaymentController:18, FinancePostingService:14), FIN `CounterpartyType == 4` and `Direction == 2`. FreightBasis shows as a raw byte in FContract/Details.cshtml:104, FContractTariff/Index.cshtml:90 and FContractTariff/Details.cshtml:40. | Add a `LogisticsEnums` class (enums plus `ToDisplay()` plus a `SelectList` factory) next to `LogisticsTerms`. | S | Low |
| LOG-M03 | Low | Maint | WaybillController.cs:112 vs. 348-350; Waybill/Index.cshtml:141 vs. Details.cshtml:27 | The payment status string is `"To'landi"` in one place and `"To'langan"` in another. The badge colour logic depends on the exact string, so a mismatch means a wrong colour. | Use an enum and render the text in one helper. | S | Low |

### Duplication (also listed in the duplicates map)

| ID | Sev | Cat | Location | What and why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| LOG-D01 | Med | Dup | 8+ places (see map) | The waybill "paid / outstanding" computation is copy-pasted with diverging semantics. This is the root cause of B04. | `Services/WaybillLedgerService` | M | Med |
| LOG-D02 | Med | Dup | Waybill Create/Edit views (98% identical, about 420 lines each); WaybillPayment Create/Edit; FContract, FContractTariff, FTransportType and Forwarder Create/Edit | The form markup and JS are duplicated, and fixes have to be made twice. B03 exists in both Waybill copies. | Use `_Form.cshtml` partials; put JS in `Scripts/app/waybill-form.js`. | M | Low |
| LOG-D03 | Low | Dup | Many (see map) | Dropdown fillers for forwarders (9×), contracts (6×), transport types (8×, with inconsistent deleted filters), currencies (9×). | `LookupService` with cached reference data | S | Low |
| LOG-D04 | Low | Dup | LogisticsDashboardController.cs:134-152, 247-270; ForwarderController.cs:160-179 | Lane aggregation is implemented 3 times with 3 near-identical view models. Forwarder stats and OTIF are calculated twice (Dashboard 116-131 / 368-392, Forwarder 151-157). | `LogisticsAnalyticsService` with one `LaneStat` and one `ForwarderStat` view model | S | Low |
| LOG-D05 | Low | Dup | WaybillController.cs:1214-1326 vs. InventoryController.cs:917-950+ | Excel helpers (`BuildHeaderMap`, `GetCell*`) are duplicated. | `Helpers/ExcelImportHelper` | S | Low |
| LOG-D06 | Low | Dup | 11 views | The `var T = new { Waybill = LogisticsTerms.Waybill, ... }` block is copy-pasted. | `@using static tahsinERP.Services.LogisticsTerms` in `Views/web.config`, or a global `@helper` | S | Low |
| LOG-D07 | Low | Dup | Every action and every view | The permission-check boilerplate (`RoleHelper.IsViewPermitted(User.Identity.Name, RouteData...)`) is repeated in each action **and** again in each view. | A `[ModulePermission(View/Change)]` filter; drop the checks from the views | M | Low |

## Duplicates map

| Duplicated logic | Locations | Proposed shared home |
|---|---|---|
| Waybill paid/outstanding (direct + split + FIN) | WaybillController.cs:1235-1280 (`BuildWaybillPaidLookup`, unused `GetWaybillPaidTotal`); WaybillController.cs:319-350 (Details inline copy); WaybillPaymentController.cs:75-130 (Index inline copy); WaybillPaymentController.cs:565-584 (`GetWaybillPaidTotal` with exclude); LogisticsDashboardController.cs:67-100 (no splits); LogisticsDashboardController.cs:309-317 (Reconciliation, no splits); ForwarderController.cs:103-119 (by forwarder, no FIN); LogisticsMonthlyReportService.cs:34-106. Outside the module: OpeningBalanceController.cs:590-600, 878-890; FinDashboardController.cs:580, 1545 | `Services/Logistics/WaybillLedgerService`: `GetPaidLookup(db, IQueryable<int> waybillIds, int? excludePaymentId)`, `GetForwarderBalance(db, fid, from, to)`, plus split allocation (`AllocateAcrossWaybills`, `SavePaymentSplits` moved out of WaybillPaymentController). The const `SplitPayerType = 99` is defined only there. |
| Waybill form (Create/Edit) | Views/Waybill/Create.cshtml and Edit.cshtml (diff is 8 lines) | `Views/Waybill/_WaybillForm.cshtml` plus `Scripts/app/waybill-form.js` |
| Waybill entity ⇄ view model mapping | WaybillController.cs:233-270 (Create), 395-433 (Edit GET), 492-530 (Edit POST), 759-796 (Import) | `WaybillMapper.ToEntity / ApplyTo / FromEntity` (or AutoMapper profile) |
| Tariff validation in Create/Edit POST | WaybillController.cs:187-213 and 452-478 (identical) | `WaybillService.ApplyTariff(db, vm, ModelState)` |
| Payment form | WaybillPayment Create/Edit views; `PopulateFormBags` call repeated 5× | `_PaymentForm.cshtml` |
| Forwarder dropdown | WaybillController.cs:141, 1024; WaybillPaymentController.cs:531; LogisticsDashboardController.cs:169; LogisticsMonthlyReportService.cs:120; FContractController.cs:53, 86, 96, 152, 202, 209 | `LookupService.Forwarders(selected)`, cached in MemoryCache for 5 minutes and invalidated on Forwarder CRUD |
| Contract dropdown | WaybillController.cs:142, 1027; FContractTariffController.cs:34, 63, 93, 106, 133, 225 | `LookupService.FContracts()` |
| Transport type dropdown | WaybillController.cs:143, 1030; FContractTariffController.cs:64, 94, 107, 134, 226; TransportLoadSimulationController.cs:42 | `LookupService.TransportTypes()` with one deleted filter |
| Currency dropdown | WaybillController.cs:1048; WaybillPaymentController.cs:532; FContractController.cs (6×); FContractTariffController.cs (5×) | `LookupService.Currencies()` (global, cross-module) |
| Direction / FreightBasis / PayerType lists and parsing | WaybillController.cs:144-149, 1328-1387; LogisticsDashboardController.cs:170-175; Waybill Create/Edit views (3 inline SelectLists each); FContractTariff Create/Edit; Waybill/Index.cshtml:139-140 | `LogisticsEnums` (enum + display + parse + SelectList) |
| Lane statistics | LogisticsDashboardController.cs:134-152, 247-270; ForwarderController.cs:160-179 (3 view models: `LaneDashboardStatViewModel`, `LanePerformanceViewModel`, `LanePerformance`) | `LogisticsAnalyticsService.GetLaneStats(query, top)` with one view model |
| Forwarder KPI and OTIF | LogisticsDashboardController.cs:116-131, 368-392; ForwarderController.cs:107-157 | `LogisticsAnalyticsService.GetForwarderStats` |
| CBM and pallet calculation | FTransportTypeController.cs:34-43, 170-194; TransportLoadCalculator | `TransportLoadCalculator.Cbm(type)` / `.Pallets(type)` (after fixing units, B10) |
| Excel header map and cell parsing | WaybillController.cs:1214-1326; InventoryController.cs:917-950+ | `Helpers/ExcelImportHelper` (cross-module) |
| `var T = new {...}` terms block | 11 Waybill and WaybillPayment views | Static import of `LogisticsTerms` |
| Soft-delete POST boilerplate (Find → IsDeleted → hook → Save → log → catch RetryLimit/Exception) | FContractController.cs:240-287; ForwarderController.cs:278-326; FTransportTypeController.cs:329-373; WaybillController.cs:817-854 | `BaseController.SoftDelete<T>(id, hook, logText)` (cross-module) |

## Dead code

| Item | Location | Confidence |
|---|---|---|
| `GetSuggestedRate` action and `MatchTariffScore` | WaybillController.cs:885-934, 1072-1111. No caller in views or JS. | High |
| `GetWaybillPaidTotal` (private, unused) | WaybillController.cs:1277-1280 | High |
| `ForwarderController.GetWaybills` (AJAX) | ForwarderController.cs:331-362. No caller. | High |
| `page`, `pageSize` parameters | WaybillController.cs:24 | High |
| `MonthlyTrend` computation and ViewBag | LogisticsDashboardController.cs:155-165, 193. Not rendered. | High |
| `ViewData["CBM_{id}"]` and `.Include(UNIT)` in Index | FTransportTypeController.cs:30-43. The Index view uses neither. | High |
| Commented-out old `Create(F_TRANSPORT_TYPES)` | FTransportTypeController.cs:121-149 | High |
| Commented-out `UnitID` line | FTransportTypeController.cs:271 (see B18) | High |
| `refreshDataTable()` plus a third jQuery copy | Forwarder/Index.cshtml:102-109, FContract/Index.cshtml:111-118, FTransportType/Index.cshtml:106-113 | High |
| OrbitControls.js script | TransportLoadSimulation/Index.cshtml:222 | High |
| Duplicate `btn.onclick` handler | TransportLoadSimulation/Index.cshtml:603-624 | High |
| Unused Includes (`F_WAYBILL_DOCS`, `F_WAYBILL_PAYMENTS`, `P_INVOICES`) | WaybillController.cs:305, 309, 310, 572 | High |
| `F_WAYBILL_DOCS` and `F_CONTRACT_DOCS` tables | No UI reads or writes them; only the Include above and SoftDeleteReferenceService.cs:265. Feature never finished. | Med |
| `var type = ViewBag.Type` (never set by the controller) | FContract/Create.cshtml:6, FContract/Index.cshtml:5 | High |
| `ControllerName` field (only a log string) | WaybillPaymentController.cs:17 (keep or make const) | Low |
| Unused usings | FContractController.cs:1-2, 7, 13; ForwarderController.cs:1, 6; FTransportTypeController.cs:9 | High |
| `FormCollection gfs` / `nimadir` dummy parameters | Delete POSTs (used only to make the signatures differ). Use `[ActionName("Delete")] DeleteConfirmed`. | Med |

## Suggested split of god-controllers

**WaybillController (1389 lines)** splits into:

1. **`WaybillController`** (about 350 lines): Index (server-side DataTables JSON), Details, Create, Edit, Delete. Thin actions that call services.
2. **`WaybillImportController`**: `UploadExcel` GET/POST and `DownloadTemplate`. The logic moves to **`Services/Logistics/WaybillImportService`**, built on the shared `Helpers/ExcelImportHelper`, which takes `BuildHeaderMap`, `GetCell*` and the `Parse*` enum parsers (the parsers go to `LogisticsEnums`).
3. **`WaybillLookupController`** (JSON, `[ValidateAntiForgeryHeader]`, permission attribute): `GetTariffsByContract`, `GetDataFromInvoice`, plus new select2 search endpoints for invoices, orders and TTNKs (fixes P01). Delete `GetSuggestedRate` unless it gets wired up.
4. **`Services/Logistics/WaybillService`**: `ApplyTariff` (validation and copy), `FreightCalculator.Calculate` (with MinimumCharge), `WaybillNumberGenerator` (SQL sequence), and the mapping between entity and view model.
5. **`Services/Logistics/WaybillLedgerService`**: `BuildWaybillPaidLookup`, plus `GetWaybillPaidTotal`, `AllocateAcrossWaybills` and `SavePaymentSplits` from **WaybillPaymentController (671 lines)**. That controller then shrinks to about 300 lines of CRUD inside a transaction.

**LogisticsDashboardController** has 404 lines. Its aggregation should move to `LogisticsAnalyticsService`, next to `LogisticsMonthlyReportService`, so the controller only shapes view models.

## Cross-module notes

- **jQuery is loaded after `RenderBody`** (`_Layout.cshtml:1729` vs `:1743`), **and loaded twice** (bundle at 1743, `assets/js/jquery.min.js` at 1779). Any view with inline `$()` outside `@section scripts` is broken (B03). A project-wide grep for inline `<script>` that uses `$` outside sections is recommended. Removing the second copy saves about 90 KB per page.
- **Permissions:** `RoleHelper` results are cached, so the cost is small, but the check is written twice per request (once in the action, once in the view). A `[ModulePermission]` action filter would remove about 2 checks × 40 actions in this module alone.
- **Soft-delete hooks** in `SoftDeleteReferenceService` null out FKs on historical documents. This is a design bug and likely affects other modules too (S_CONTRACTS → SALES_INVOICES, P_ORDERS → P_INVOICES, and so on). It needs a core decision: soft delete should hide, not unlink.
- **Nullable `IsDeleted` with `== false`** is a project-wide pattern (B14). It can be fixed with a DB default and a NOT NULL migration.
- **Multi-currency aggregation** without conversion (B13) is probably also present in FinDashboard and OpeningBalance, which read F_WAYBILLS and F_WAYBILL_PAYMENTS.
- **FIN posting coupling:** `FinancePostingService.PostWaybillPayment` saves inside its own `SaveChanges`. A shared unit-of-work or transaction pattern is needed for every document that posts to finance (B08).
- **Reference data caching** (`LookupService` for currencies, units, forwarders, contracts, transport types) applies to all modules.
- **`CompanyID = 1` hard-coded** (S01). Check the other modules for the same pattern before the DB-per-tenant AWS move.
- **ExcelImportHelper** should be shared with InventoryController and FinTransactions import.

## Recommended order of work

1. **Quick wins (each S, low risk):**
   - B01: pass `model.ID` in Create, then a one-time data fix.
   - B03: move inline scripts into `@section scripts`, then check in the browser.
   - B09: null-model Delete views.
   - B11 and P12: simulation double handler, InstancedMesh, single animation loop.
   - P03: remove the blob Include and the table-wide split ID load.
   - P14 and P15: remove the extra jQuery and OrbitControls.
   - B17: dateInput ids and the CURRENCy null check.
   - B18, B19.
   - The dead-code removals listed above.
   - P16: AsNoTracking.
2. **Data-integrity fixes:**
   - B02: stop nulling FKs on soft delete. Coordinate with the core team.
   - B08: transactions around payment, splits and FIN posting.
   - B07 and P04: unique WaybillNo plus a sequence.
   - B10: decide on the transport dimension unit.
3. **Single source of truth for debt** (D01, B04, B06, B13): introduce `WaybillLedgerService` and `FreightCalculator` and switch the Dashboard, Reconciliation, MonthlyReport, Forwarder Details, Waybill Index/Details and WaybillPayment to them. Add a decision on currency handling.
4. **Heavy pages:**
   - P01: AJAX pickers in the Waybill form.
   - P02 and P09: server-side DataTables for Waybill Index and WaybillPayment Index.
   - P05: SQL aggregation in MonthlyReport.
   - P06: Reconciliation date filter and paging.
   - P07 and P08: dashboard projections.
5. **Edit semantics:** B05, persist TariffID (schema change).
6. **Refactor:** M01 split of WaybillController and WaybillPaymentController, D02 form partials, D03 LookupService, M02 LogisticsEnums, D04 analytics service, S03 view models instead of entity binding.
