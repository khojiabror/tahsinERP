# MES / Engineering module audit (tahsinERP)

Root: `C:\Work\SCHAIN\tahsinERP\tahsinERP\tahsinERP` (all paths below are relative to it). Read-only audit; nothing in the project was changed.

## Scope

**Controllers (18 files, 8,264 lines). All were read in full.**
- `Controllers/MES/`: `ProductPlanController.cs` (1074), `ProductProductionController.cs` (1034), `QualityInspectionController.cs` (681), `MESDashboardController.cs` (369), `MESReportsController.cs` (296), `DailyPlanUpdateItem.cs` (10)
- `Controllers/Engineering/`: `NormController.cs` (2041), `ProductController.cs` (616), `TannarxController.cs` (546), `ProductCategoryController.cs` (290), `TannarxAnalyticsController.cs` (213), `BOMController.cs` (198), `ShopController.cs` (176)
- `Controllers/`: `DamagedPartController.cs`, `DamagedProductController.cs`, `DefectTypeController.cs`, `ProductionProcessController.cs`

**Services and helpers.** All were read in full.
- `TannarxAnalyticsService.cs` (1529), `BomMaterialSummaryService.cs` (570), `TannarxService.cs` (544), `ProductionConsumptionService.cs` (411), `ProductionLotService.cs` (151)
- `MaterialConsumptionService.cs` and `MaterialConsumptionServiceDTOs.cs`: both files are empty (0 bytes)
- `Helpers/QualityInspectionBalanceHelper.cs`, `Helpers/ProductProductionPlanDateMeta.cs`
- Read as dependencies: `LogHelper`, `UserHelper.GetUserId`, `RoleHelper` (cache section), `Filters/ValidateAntiForgeryHeader`

**Views (about 75 files).**
- Read in full or near full: Norm Create and Edit (diffed against each other), ProductPlan Index, Details, Grid* and `_DailyGridPartial`, ProductProduction Index and Create (and Details compared to Create), QualityInspection Index and Create, BOM Details and `_BomTreeNode`, Tannarx Index and Edit.
- Skimmed with targeted greps for permission checks, scripts, `Html.Raw` and DataTables: MESDashboard, MESReports, TannarxAnalytics, Product, ProductCategory, Shop, DamagedProduct, DefectType, ProductionProcess.

**Models checked:** PRODUCT*, BOM_*, NORM*, TANNARX*, V_TANNARX_*, PRODUCTPLAN*, QUALITY_INSPECTION, SHOP*, DAMAGED_PRODUCTS, sp_Norm_PreviewCutMaterial_Result, USERLOG.

---

## Findings

Severity counts: **Critical 3 · High 14 · Medium 21 · Low 9** (47 findings: 26 Perf, 21 Bug/Sec). Plus 26 duplicate groups and 19 dead-code items listed separately below.

### Performance

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| MES-P01 | Critical | Perf | MESDashboardController.cs:253-316 | **Order bottlenecks N+1.** Loads ALL non-deleted `S_ORDERS` (whole history) with lines. For each order line it runs 4 queries: produced sum, approved QI sum, invoiced sum and `PRODUCTS.FindAsync`. The produced and inspected sums depend only on ProductID, yet they are recomputed for every order. At 1,000 orders × 5 lines that is about 20k round-trips on every dashboard load. The dashboard also has no permission check. | Use 3 grouped queries: produced by ProductID, accepted by ProductID, invoiced by (OrderID, ProductID). Join S_ORDER_PRODUCTS to PRODUCTS for PNo in one projection, filter `remaining > 0` in SQL, then `Take(10)`. Better still, use a SQL view or SP. | M | Low |
| MES-P02 | Critical | Perf | ProductProductionController.cs:894-898 and 970-974 | `GetPlannedProducts` (AJAX, fires on every date/shop change) and `LoadPlannedProducts` load **the whole `PRODUCTPLANS_DAILY` table** with `Include(PRODUCTPLAN.PRODUCT)` and then filter one day in memory. The comment ("avoid LINQ to Entities Date issues") is wrong: `d.Day >= day && d.Day < day+1` translates fine. | `.Where(d => d.Day >= dateOnly && d.Day < next && d.PRODUCTPLAN.PRODUCT.ShopID == shopId && !d.PRODUCTPLAN.IsDeleted)`, then project to `ProductionEntryItem` with `AsNoTracking`. | S | Low |
| MES-P03 | High | Perf | ProductProductionController.cs:916-919, 992-995, 103-105 | All `PRODUCT_PRODUCTION` rows for the shop (full history, deleted rows included) are loaded in GetPlannedProducts, LoadPlannedProducts and Create POST, then regex-matched against `[RejaSana:]` in memory. | Filter in SQL: `Date` in [day-60, day+1] OR `Comment.Contains("[RejaSana:" + day:yyyy-MM-dd + "]")`, plus the IsDeleted filter. Longer term, add a `PlanDate` column instead of the Comment meta. | S | Low |
| MES-P04 | High | Perf | ProductPlanController.cs:108-113 and 805-810 | Details and GetDailyGridPartial load **every production of the product** (all history), then `AggregateActualProductionByPlanCalendarDay` loops days × rows with a regex per row. | Restrict to `Date` between plan.StartDate-N and DueDate+N plus the Comment LIKE. Extract one shared method (see Duplicates #13). | S | Low |
| MES-P05 | High | Perf | TannarxService.cs:312-336 (via TannarxController.cs:114, 188) | `GetFinishedGoodsWithNormsProductIdsAsync` calls `HasNormsForMonthAsync` **per finished good** (1-2 queries each, with `Include(NORMLINES)`). This runs on **every Tannarx Index load**: 500 FG means up to 1,000 queries. | One set-based query: active NORMVERSIONS for the month joined to NORMLINES or NORM_INPUT_LINES (by Month ∈ EffectiveFrom periods), then `Select(ProductID).Distinct()`. | M | Low |
| MES-P06 | High | Perf | TannarxService.cs:351-462 | `GenerateTannarxForMonthAsync` runs about 8 queries per product: existing check, HasNorms (2), CalculateMaterialPrice (3-4), Labor. | Preload existing TANNARX ids, the set of products with norms, NORM_INPUT_LINES for the month (grouped), material categories and active tariffs. Compute in memory, then one SaveChanges. Add a unique index (ProductID, Year, Month, IsDeleted=0). | M | Med |
| MES-P07 | High | Perf | TannarxAnalyticsService.cs:380-473, 708-883, 1032-1124, 1364-1396 | `UpdateActualCostsAsync` first runs the SP `SP_TANNARX_UPDATE_ACTUAL_COSTS` and then **recomputes everything in C#** (two engines). Per product: Path2 then Path1 (each rebuilds BOM ids (2-3 queries), SO ids, PO ids, P_INVOICES, lines), finished transport and material transport (BOM, SO and PO again), plus a raw `MERGE` per row. `GetPurchaseOrderIdsBySalesOrderTokensAsync` loads **all `P_ORDER_PRODUCTS` with "Buy#"/"B:"** on each call, up to 3 times per product. Roughly 30-40 queries per product. | Build a per-month context once: BOM components for all products in 1 query, SO→PO token map in 1 query, PI/INCOME/waybill sets in 1 query each. Pass dictionaries into the path evaluators. Write the audit through the EF entity `TANNARX_ACTUAL_AUDIT` (it already exists in the EDMX) in one SaveChanges. Decide which engine is authoritative (SP or C#) and drop the other. | L | Med |
| MES-P08 | Med | Perf | TannarxAnalyticsService.cs:146-214; 290-304 | Monthly trends run 12 sequential queries that load full TANNARX entities. Report and ProductDetails with a productID build 3 "calculation notes" per row (each about 10 queries, including the full P_ORDER_PRODUCTS load and the whole selector engine). Report with productID and no year/month means all months × 30 queries. | Trends: one `Where(date range).GroupBy(Year, Month).Select(sums)`. Notes: compute only in ProductDetails (single row), reusing the decision stored in TANNARX_ACTUAL_AUDIT. | S | Low |
| MES-P09 | High | Perf | BomMaterialSummaryService.cs:124-341, 205-236 | `BuildBomTree` and `BuildBomTreeChildren` run recursive **synchronous** per-node queries: image varbinary, child BOM headers, child lines. `LoadNormWoodDataByMaterial` runs twice (ApplyWoodQuantities + BuildMaterialSummaries). The method sits on the hot path through `ProductionConsumptionService.CalculateRequiredRawMaterialsAsync`: production Create calls it **2× per item** (validate + consume), Edit 3×, Details 1×, Reconcile once per production (all productions), and the InventoryReports / ProductionControl controllers too. There is no cycle guard, so a BOM loop ends in StackOverflow. | Load all active BOM_HEADERS and BOM_LINES (projection) for the product closure in 1-2 queries (or a recursive CTE SP), then build the tree in memory. Remove image loading from the service (BOM Details only). Cache the wood data once per call. Memoize `GetMaterialSummariesPerUnitAsync` per request so validate+consume share it. Add a visited-set guard. | M | Med |
| MES-P10 | Med | Perf | ProductionConsumptionService.cs:93-123, 153-163, 280; ProductProductionController.cs:533, 546 | `GetDisplayUnitFromIncome` runs one sync query per material. It is called again per material in Consume (280) and twice per material in ProductProduction Details. | `Dictionary<int,int?> GetDisplayUnitsFromIncome(IEnumerable<int>)` in 1 query (GroupBy ProductID → first by IssueDateTime). | S | Low |
| MES-P11 | Med | Perf | ProductionConsumptionService.cs:168-212, 277-320 | Per material: loads ALL income lines in the warehouse (history), plus `SaveChanges` per EXPENSE_PRODUCTS line and a stock issue per line. | Batch prices (see B11 for the correctness issue). Add all lines, then one SaveChanges before the issue helper if the helper allows it. | M | Med |
| MES-P12 | Med | Perf | ProductProductionController.cs:477-518 | Details runs 3 EXPENSES queries with `Description.Contains(...)` (LIKE '%x%' full scans). It also loads **all expenses ±30 days with their lines** as a legacy fallback. | Add a `ProductionID` column (FK) on EXPENSES/INCOMES (the marker currently lives in the Description text). Keep the legacy fallback behind a one-off data migration. | M | Med |
| MES-P13 | High | Perf | NormController.cs:89-156 | Details: per norm version it runs `NORM_INPUT_LINES` + `PRODUCTS` queries inside `Select` (N+1, 2 per version). | One query for all input lines of the product, grouped by Month, plus one material-category dictionary. | S | Low |
| MES-P14 | High | Perf | NormController.cs:1165-1170, 1231, 1266-1303, 1376-1384, plus about 10 `LogHelper` calls | Confirm (save) runs a PRODUCTS query per line, `UserHelper.GetUserId` **per line** (new DbContext each time), 3 queries per material group, then a PRODUCTS query per material again, about 12 `SaveChanges`, and about 8 `LogToDatabase` (each a new context + user lookup), all inside a `TransactionScope`. | Preload a materials dictionary once, get the user id once, build NORMLINES from the in-memory `NORM_INPUT_LINES` list, write one summary log, and use 2-3 SaveChanges total. | M | Med |
| MES-P15 | High | Perf | Views/Norm/Create.cshtml:158-160, 431, 496-520, 617-628 (same in Edit.cshtml) | Every existing row renders a full `<select>` of **all raw-material + component products**, and the list is also serialized as JSON. With 40 lines × 3,000 materials that is 120k `<option>` tags. Create also fires one `GetMaterialData` AJAX **per row on load**, each running the INCOME_PRODUCTS "latest price" query. | select2 with `ajax:` remote search (`/Norm/SearchMaterials?q=`). Render only the selected option per row. Add a batch endpoint `GetMaterialData(int[] ids)`. | M | Med |
| MES-P16 | Med | Perf | QualityInspectionController.cs:327-350, 191-237; QualityInspectionBalanceHelper.cs:19-49 | Building the daily list runs 2 queries per production (`GetAccepted` is executed twice via `GetRemainingQty`). Create POST per item: production query + 2 helper queries + `UserHelper.GetUserId`. | One grouped query: `QUALITY_INSPECTION.Where(ProductionID in ids).GroupBy(ProductionID).Sum(QtyAccepted)`. Get the user id once. | S | Low |
| MES-P17 | Med | Perf | ProductionLotService.cs:32-52, 59-86, 96-115 | `GetAvailableProductionLots` calls `GetTransferredFromProduction` **per lot**, and each call re-runs the identical query that loads all transfer comments of the product (N identical queries plus a regex each). `AllLotsPassedQuality` does the same per allocation. | Load the comments once, parse all `ProdID:x:q` into a `Dictionary<int,double>`, and reuse it. | S | Low |
| MES-P18 | Med | Perf | ProductProductionController.cs:24-53; QualityInspectionController.cs:27-71; ProductPlanController.cs:24-48; ProductController.cs:124-129; DamagedProductController.cs:207-211 | List pages ignore their own default date range. ViewBag shows "last 30 days" but the query is unfiltered when no parameter is given, so the whole table is materialized (tracked, with Includes) into a client-side DataTable. | Apply the default range in the query, add `AsNoTracking` + projection, and move to server-side DataTables for Product and Production. | S | Low |
| MES-P19 | Med | Perf | ProductProductionController.cs:55-56, 81-82, 242-243, 256-257, 308-309, 338-339, 355-356, 414-415, 429-430; ProductPlanController.cs:1002-1009; QualityInspectionController.cs:73-88, 644-654; TannarxController.cs:65-71; TannarxAnalyticsController.cs:63-69, 117-123; DamagedProduct/DamagedPart Create/Edit | Dropdowns load **full PRODUCT entities** (ProductProduction even loads ALL products, raw materials included) on every GET and error path. QI `PopulateViewBags` loads **all S_ORDERS and all SORDER_DELIVERY_SCHEDULE**. QI Index loads DEFECT_TYPES that the view never uses. | A shared `LookupService` with projected, `HttpRuntime.Cache`-d lists (finished goods, shops, units, defect types, categories) invalidated on save. | M | Low |
| MES-P20 | Med | Perf | MESDashboardController.cs:86-191; MESReportsController.cs:51-81, 161-191 | Loads all productions ±6 months around the period (year view = 2 years) with `Include(SHOP)` and attributes dates in memory. `linkedProductionIds.Contains` produces a large IN list. The attribution join relies on `QUALITY_INSPECTION.ProductPlanDailyID`, which new code always writes as null (see B20), so the path is legacy only. | Aggregate in SQL by date and shop (`GroupBy(DbFunctions.TruncateTime(Date))`). Drop the ±6-month window or move to one SQL view `V_MES_PRODUCTION_DAILY`. | M | Med |
| MES-P21 | Low | Perf | TannarxAnalyticsService.cs:500-510, 631-640, 663-706 | Materializes amount lists and sums them in memory. `FIN_CATEGORIES.Name.ToLower().Contains(...)` is non-sargable and fragile. | `SumAsync`; classify categories by a code or flag column, not by name text. | S | Low |
| MES-P22 | Med | Perf | BOMController.cs:27-116 | Index runs a `BOM_HEADERS` query per finished good (N+1) and loads full product and norm entities. | One query of active headers for all FGs + GroupBy. Reuse `ParseVersionNoDate` from the service. | S | Low |
| MES-P23 | Med | Perf | BOMController.cs:166-173 + BomMaterialSummaryService.cs:127-136 (root image loaded twice); ProductController.cs:153-160, 284-291, 368-374 | Product images (varbinary) are inlined as base64 data-URIs into HTML, once per BOM node. The pages become MBs and nothing is cached. | `ProductImage(int id, int? w)` action with `OutputCache`/ETag and a thumbnail. Views reference it by URL. | M | Low |
| MES-P24 | Low | Perf | ProductController.cs:528-561 | Excel import: per row `AnyAsync` + `SaveChanges` + `LogToDatabase`. | Preload existing PNo `HashSet`, `AddRange`, one SaveChanges, one summary log. | S | Low |
| MES-P25 | Med | Perf/Bug | ProductPlanController.cs:209-259, 332-411 | GridCreate/GridEdit: each row runs SO validation + overlap query + `SaveChanges` + daily generation `SaveChanges`. There is **no transaction**, so a mid-grid failure leaves a partial save. | Validate all rows first (overlaps among the posted rows too), then one transaction and one SaveChanges. | M | Med |
| MES-P26 | Low | Perf | ProductPlanController.cs:762-772 | `UpdateDailyPlan`: `Find` per item. | `Where(ID in ids && PlanID == planID)` once. | S | Low |

### Correctness, security and risk

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| MES-B01 | Critical | Bug | ProductProductionController.cs:103-105 → 121-124, 146-175; 916-919; 992-995 | **Soft-deleted productions are not filtered.** Create picks `existing` from all shop productions, deleted ones included, so it can reverse and re-post stock onto an **invisible deleted row** (IsDeleted stays true). Stock increases with no visible production. GetPlannedProducts and LoadPlannedProducts also show deleted rows as "existing". | Add `(p.IsDeleted == false \|\| p.IsDeleted == null)` in all three queries. Write a data-check script for deleted rows that have live EXPENSE/INCOME markers. | S | Low |
| MES-B02 | High | Bug | ProductProductionController.cs:631-671 | `DeleteConfirmed` runs ReverseProductionIncome + ReverseProductionConsumption (which call SaveChanges internally) + soft delete with **no transaction**. A failure midway leaves stock half-reversed. | Wrap in `db.Database.BeginTransaction()` exactly like Edit. | S | Low |
| MES-B03 | High | Bug | ProductProductionController.cs:285-286, 332-333, 615-616, 643-644 | The "linked to sales invoice" guard is `SALES_INVOICE_LINES.Any(l => PRODUCT_PRODUCTION.Any(p.ID==id && p.ProductID==l.ProductID))`, which is true if the **product was ever invoiced**. Any production of a sold product therefore becomes uneditable and undeletable. It is also a correlated subquery over all invoice lines. | Check real linkage: lot allocations (`ProdID:{id}:` in transfer EXPENSE_PRODUCTS, via ProductionLotService) and approved QI. Put it in one method. | S | Med |
| MES-B04 | High | Bug | Views/QualityInspection (only Create, Details, Index exist); Views/ProductProduction (no Delete.cshtml); ProductPlanController.cs:874-901 (no RawMaterialsPreview view); QualityInspectionController.cs:546 | Edit, Delete and Approve links in QI Index/Details and the Delete link in ProductProduction Index/Details lead to GET actions whose **views do not exist**, so they throw "view not found". `ApproveConfirmed` uses `d.IssueDateTime.Date` inside LINQ-to-Entities, which throws `NotSupportedException` whenever `QtyRejected > 0`. In addition, Create already writes APPROVED/REJECTED + DAMAGED_PRODUCTS (217-253), so a later Approve would duplicate damaged rows (the date match uses `DateTime.Now` vs InspectionDate). | Decide the workflow. Either add the missing views and use `DbFunctions.TruncateTime`, or remove Edit/Approve/Delete GETs and switch to POST-only buttons. Create DAMAGED_PRODUCTS only at approval. | M | Med |
| MES-B05 | High | Sec | ProductPlanController (all actions), ProductProductionController (all except Reconcile), ProductController, ProductCategoryController, ShopController, DefectTypeController, DamagedProductController, DamagedPartController, ProductionProcessController, MESDashboardController; NormController.GetMaterialData (731); QualityInspectionController.GetAvailableQtyForInvoicing (658) | **No server-side permission check.** Permissions are enforced only by `@if RoleHelper...` in views, so POST endpoints (create/edit/delete production with stock movements, plans, products) can be called directly by any authenticated user. `Views/ProductProduction/Details.cshtml:8` hard-codes `bool changePermit = true;` and has no view check at all. | Global `[ModulePermission]` filter (View for GET, Change for POST) keyed on controller/action, registered in FilterConfig and backed by the cached RoleHelper. Remove the per-action copies. | M | Med |
| MES-B06 | High | Bug | NormController.cs:974-1004, 1094-1159 | When **updating** a norm, the period can change but the duplicate-period check runs only for new norms. Old `NORM_INPUT_LINES` are deleted by (ProductID, **new** period), which **deletes another version's lines** if that month already has a norm. The old period's lines stay orphaned. | Capture `oldPeriod` before the update, delete by oldPeriod, run the duplicate check for updates (`n.ID != NormVersionID`). Long term: add a `NormVersionID` FK on NORM_INPUT_LINES instead of the Month string. | S | Med |
| MES-B07 | Med | Bug | NormController.cs:1334-1358, 1361-1432 | There is only one `BOM_HEADERS` per product (`FirstOrDefault(ProductID)`). Every norm save **overwrites it** with the VersionNo and lines of the month just saved, even an older month. `BomMaterialSummaryService.SelectActiveBomHeaderAsync(asOfDate)` (60-101) has nothing to choose between, so consumption uses the last-edited month's norm. | One BOM header per NormVersion (VersionNo = period), or update the header only when the saved period is the latest. | M | Med |
| MES-B08 | Med | Bug | NormController.cs:375-450 | Delete hard-deletes the NORMVERSION but leaves `BOMS_AND_NORMS` rows (FK violation or orphan) and BOM_LINES built from it. Production keeps consuming per the deleted norm. | Deactivate the link, rebuild the BOM from the previous version (or soft-delete), all in the same transaction. | S | Med |
| MES-B09 | Med | Bug | ProductPlanController.cs:638-646, 374-380 | Edit and GridEdit change Start/Due/Amount but **do not regenerate `PRODUCTPLANS_DAILY`**. The daily plan, dashboard and reports (which sum daily rows) diverge from the header. | Regenerate daily rows (or warn when manual edits exist) inside a transaction. | S | Med |
| MES-B10 | Med | Bug | ProductPlanController.cs:235-259, 454-481; ProductCategoryController.cs:93-131; TannarxService.cs:370-453 | Race conditions: the plan-overlap check runs outside a transaction; the category `Code` is generated with MAX+1 after insert (also loads all codes); Tannarx Generate has no unique constraint, so a double submit creates duplicates. | Use SERIALIZABLE/UPDLOCK or unique indexes (ProductID+Year+Month; Code). Use a SEQUENCE for the category code. | S | Low |
| MES-B11 | Med | Bug | ProductionConsumptionService.cs:168-212 | The "FIFO" price is not FIFO. It always takes the oldest receipts' `Amount`, ignoring quantity already consumed, so the expense cost is systematically wrong (oldest prices forever). | Price from DLOC lots / `InventoryValuationService` (already used for qty) or the remaining qty per receipt. | M | Med |
| MES-B12 | Med | Bug | DamagedProductController.cs:242, 295; DamagedPartController.cs:56, 106 | `model.PRODUCT.PNo` on a model-bound or new entity is null and throws NRE **after** SaveChanges. Create shows "unable to save" although the row was saved (duplicates on retry). Edit throws a YSOD. | Look up PNo via `db.PRODUCTS.Where(id).Select(PNo)`. | S | Low |
| MES-B13 | Med | Sec/Perf | ProductProductionController.cs:779-875; Views/ProductProduction/Index.cshtml:43 | `ReconcileProductionConsumption` is a **state-changing GET** (CSRF via link or img). It synchronously re-posts consumption for **all** productions inside one request and can time out. | `[HttpPost][ValidateAntiForgeryToken]`, batch with paging, or move to an admin job. | S | Low |
| MES-B14 | Med | Sec | Views/Norm/Create.cshtml:617-628 (and Edit); Views/ProductPlan/GridCreate.cshtml:82-83, GridEdit.cshtml:108-109 | Stored XSS. Product names and PNo are concatenated into HTML strings in JS, and `Html.Raw` builds `<option>` text with no encoding. | Use `$('<option>').val(id).text(name)` and `Json.Encode` data instead of Raw HTML. | S | Low |
| MES-B15 | Med | Bug | Views/ProductPlan/_DailyGridPartial.cshtml:93; Views/Tannarx/Edit.cshtml:252 | Inline `$(document).ready` is rendered inside the body, but `_Layout` loads jQuery after `RenderBody` (lines 1743/1779), so `$` is undefined and inline daily-plan editing likely never binds. Other views wrap with `DOMContentLoaded` + `jQuery`. | Move into `@section scripts` (the partial needs the script hosted by the parent view or a `.js` file). **Not yet confirmed in a browser.** | S | Low |
| MES-B16 | Low | Sec/Dead | NormController.cs:831-852, 954-959, 1007-1014 | `Calculate` re-reads `Request.InputStream` (the bound parameter is overwritten), echoes up to 200 characters of raw JSON in error messages and writes `Debug.WriteLine` lines. `IsOptimized` is parsed and then unused. | Bind with `[FromBody]`-style JSON model binding. Remove the echo and the unused flag. | S | Low |
| MES-B17 | Low | Sec | ShopController.cs:138; ProductController.cs:185-195; DamagedProduct Create | Over-posting: `TryUpdateModel(..., "CompanyID","IsDeleted")` takes CompanyID from the form; Product Create binds the whole `PRODUCT` entity (StandardLengthMm, CategoryID, etc.). | Bind whitelists / view models. | S | Low |
| MES-B18 | Low | Bug | ProductCategoryController.cs:278-288 | `Dispose` creates a new DbContext just to dispose it. | Delete the override. | S | Low |
| MES-B19 | Low | Bug | ProductionLotService.cs:26, 34, 128 | Lot quantities are written with the current culture (`$"{qty}"`) and parsed with `[0-9.]+` / `double.TryParse` (culture-dependent). On a comma-decimal culture the fraction is lost. | Use `ToString(CultureInfo.InvariantCulture)` and parse with InvariantCulture. | S | Low |
| MES-B20 | Low | Bug | QualityInspectionController.cs:225; MESDashboardController.cs:86-117; MESReportsController.cs:51-81; ProductPlanController.cs:980-1000 | Plan-vs-actual attribution is inconsistent. ProductPlan uses the `[RejaSana:]` Comment meta. Dashboard and Reports use the `QI.ProductPlanDailyID` join (always null for new QIs), then fall back to the fact date. The same day therefore shows different "actual" on different screens. | One attribution rule (store a `PlanDate` column) used by all three. | M | Med |
| MES-B21 | Low | Maint | NormController.cs:1236; whole module | `StandardLengthMm`, which drives all cutting math, is not editable anywhere in the UI (DB-only field). `material.UnitID` is accessed when `material` may be null (1236). | Add it to the Product Edit form; null-guard. | S | Low |

---

## Duplicates map

| # | Duplicated logic | Locations | Proposed single home |
|---|---|---|---|
| 1 | NORM_INPUT_LINES → consumption (m) / waste (m) / cost math, plus `NormInputLineConsumptionToDisplay` and `NormInputLineWasteToDisplay` | NormController.cs:108-139, 215-298, 1485-1503, 1703-1773; TannarxService.cs:120-179; BomMaterialSummaryService.cs:495-555 | `Services/Norm/NormLineMath` (static, pure, unit-tested) |
| 2 | "Is wood" category detection (4 string variants) | NormController.cs:309-320, 765-769, 1541-1552 (unused copy), 1797-1808; BomMaterialSummaryService.cs:557-568; POrderGenerationService.cs:688 | `PRODUCT_CATEGORIES.IsWood` bit column, or one `ProductCategoryRules.IsWood()` helper |
| 3 | Effective standard length (StandardLengthMm > 0 else Length) | NormController.cs:772-781, 1173-1184, 1510-1519, 1888-1895; BomMaterialSummaryService.cs:459-468, 500-507 | `PRODUCT` partial-class property `EffectiveStandardLengthMm` |
| 4 | Materials dropdown (raw + component + missing line materials → SelectList + JSON) | NormController.cs:523-563, 625-665, 687-723 | `NormViewModelBuilder.PopulateMaterialsAsync()` (or a remote select2 endpoint) |
| 5 | "Norm already exists for period" check | NormController.cs:476-500, 978-1003 | `NormService.ExistsForPeriodAsync(productId, period, excludeId)` |
| 6 | **Norm Create.cshtml (1327) vs Edit.cshtml (1388)**: same table, same about 900 lines of JS (optimize, apply, recalc); differences are header, form action, template URL, console.logs | Views/Norm/Create.cshtml, Edit.cshtml | `_NormForm.cshtml` partial + `Scripts/app/norm-form.js` (bundled) taking a mode flag |
| 7 | Planned-products loader (~60 lines) | ProductProductionController.cs:894-952 vs 970-1027 | One private method; the JSON action calls it |
| 8 | "Linked to QI / invoice" guard | ProductProductionController.cs:284-286, 331-333, 614-616, 642-644 | `ProductionGuard.IsLocked(db, id)` |
| 9 | Shops + Products ViewBag fill | ProductProductionController.cs: 9 places (55, 81, 242, 256, 308, 338, 355, 414, 429) | `PopulateLookups(selectedShop, selectedProduct)` + LookupService |
| 10 | Exception → "Xatolik: ... \| Ichki xatolik: ..." chain | ProductPlanController.cs:523-529, 655-661; ProductProductionController.cs:224-232, 404-412; QualityInspectionController.cs:280-286; NormController.cs:1557-1573 | `ExceptionHelper.ToUserMessage(ex)` (core) |
| 11 | Plan overlap query | ProductPlanController.cs:235-239, 362-367, 384-388, 454-459, 622-628 | `ProductPlanService.FindOverlapAsync(productId, start, end, excludeId)` |
| 12 | Daily plan generation | ProductPlanController.cs:486-505 vs 1028-1048 | `CreateDailyPlansAsync` (already exists; Create POST should call it) |
| 13 | Daily plan VM + actual aggregation | ProductPlanController.cs:99-127 vs 798-823 | `BuildDailyPlanViewModelsAsync(plan)` |
| 14 | Production candidates ±6 months + `BuildAttributionDateMapAsync` | MESDashboardController.cs:86-117, 335-366; MESReportsController.cs:51-81, 161-191, 263-294 | `MesProductionQueryService` (or SQL view) |
| 15 | Uzbek month names list or dictionary | TannarxController.cs:46-60; TannarxAnalyticsController.cs:44-58, 98-112; TannarxService.cs:531-542; TannarxAnalyticsService.cs:367-378 | `Helpers/UzMonths` (core) |
| 16 | Finished-goods query (IsFinishedGood or category IsFinishedGood) | TannarxController.cs:65-71; TannarxAnalyticsController.cs:63-69, 117-123; TannarxService.cs:316-322, 358-363 | `LookupService.FinishedGoodsAsync()` (cached) |
| 17 | Current user lookup `USERS.FirstOrDefault(Uname==...)` / `UserHelper.GetUserId` | TannarxController.cs:174, 306, 349, 516; NormController.cs:1231; QualityInspectionController.cs:236, 534 | `BaseController.CurrentUserId` (cached per request / claim) |
| 18 | BOM tree builder; VersionNo parsing; active-header selection | BomMaterialSummaryService.cs:124-203 vs 270-328; BOMController.cs:53-90 vs BomMaterialSummaryService.cs:60-101, 343-367 | One recursive builder over a preloaded line map; BOM Index uses `SelectActiveBomHeader` |
| 19 | DamagedPart vs DamagedProduct controllers (same table, ~95% identical) | Controllers/DamagedPartController.cs, DamagedProductController.cs | Keep DamagedProduct, delete DamagedPart |
| 20 | Simple lookup CRUD scaffolds | DefectTypeController, ProductionProcessController, ShopController (diff shows only names differ) | Generic `LookupCrudController<TEntity>` or shared partials |
| 21 | SO → PO (Buy# token) → PI → lines chain | TannarxAnalyticsService.cs:757-816, 818-883, 1032-1124, 1126-1215, 1269-1350 | `MonthCostContext` (preloaded maps) used by all paths and notes |
| 22 | TANNARX → VM mapping | TannarxController.cs:132-157, 243-272, 418-451, 478-487 | `TannarxMapper` |
| 23 | Raw-material stock validation | ProductPlanController.cs:903-974 (dead) vs ProductProductionController.cs:677-743 vs ProductionConsumptionService.cs:230-238 | Keep only the service version |
| 24 | Product image → base64 | ProductController.cs:153-160, 284-291, 368-374; BOMController.cs:166-173; BomMaterialSummaryService.cs:127-136, 165-173, 290-298 | `ProductImageController.Image(id)` URL |
| 25 | Product type flags | ProductController.cs:90-117 vs 590-614 | One `ProductTypeMapper` |
| 26 | Category list reloads | ProductController.cs:173-177, 209-210, 280-281, 306-307, 345-346 | `PopulateFormLookups(product)` |

---

## Dead code

| Item | Location | Confidence |
|---|---|---|
| Empty files still compiled | Services/MaterialConsumptionService.cs, MaterialConsumptionServiceDTOs.cs (0 bytes; csproj:1054, 1056) | High |
| `DailyPlanUpdateItem` class (no references) | Controllers/MES/DailyPlanUpdateItem.cs | High |
| `ValidateRawMaterialStockAsync` (never called) | ProductPlanController.cs:903-974 | High |
| `RawMaterialsPreview` (no view) and `CalculateRequiredRawMaterials` (no caller) | ProductPlanController.cs:829-901 | High |
| Unused `matchingSchedules` query | ProductPlanController.cs:509-516 | High |
| View with no action | Views/ProductPlan/UploadWithExcel.cshtml | High |
| `DamagedPartController` (no Views/DamagedPart folder) + `DamagedPartIndexViewModel` | Controllers/DamagedPartController.cs; ViewModels/DamagedPartIndexViewModel.cs | High |
| QI Edit/Approve/Delete GET (views missing), `GetAvailableQtyForInvoicing` (no caller), Index `ViewBag.DefectTypes` | QualityInspectionController.cs:361-409, 475-506, 572-604, 658-678, 88 | Medium (workflow decision needed, see B04) |
| `IsWoodProductCategory` (private, unused); `BomQtyFromNormAggregatedMeters(...,false,...)` always returns its input; `IsOptimized` flag; `ApplyBaseConsumptionWaste` comment on PiecesPerBar | NormController.cs:1541-1552, 1420/1525-1539, 1006-1014 | High |
| `GetFinishedGoodsWithNormsCountAsync` | TannarxService.cs:341-345 | High |
| `GetSalesOrderIdsByPurchaseOrderLinksAsync` (loads all S_ORDERS), `GetTotalSalesOrderProductQtyAsync`, `ParseProductionId` + `ProductionIdRegex`, `ExtractBuyTokensFromPriority` | TannarxAnalyticsService.cs:1398-1451, 475-483, 16 | High |
| `FormatLotAllocationComment` | ProductionLotService.cs:18-21 | High |
| `GetAttributionCalendarDate`, `BelongsToCalendarMonth`, `BelongsToInclusiveCalendarDateRange` (only self-referenced) | Helpers/ProductProductionPlanDateMeta.cs:81-109 | High |
| Waste branch: `WasteQty` is always 0 from `CalculateRequiredRawMaterialsAsync`, so `RecordWasteAsync` never runs; `RequiredQtyMeters` is always 0 | ProductionConsumptionService.cs:73-75, 113-118, 309-319 | High |
| EDMX entities never used from C#: V_TANNARX_ACTUAL_* (5 views), sp_Norm_PreviewCutMaterial_Result, SHOP_PLANNED_DTS | Models/*.cs + EDMX | Medium (may be used by SPs; check DB) |
| `console.log` debug noise (about 25 calls) | Views/Norm/Create.cshtml, Edit.cshtml | High |
| Tannarx Edit inline script (only logs) | Views/Tannarx/Edit.cshtml:252-264 | High |
| `using DocumentFormat.OpenXml.EMMA` / `...Office2010.Excel` unused usings | DamagedPart/DamagedProduct/ShopController line 1 | High |

---

## Suggested split of god-controllers

**NormController (2041 lines)**
- `NormController` (thin, about 400 lines): Index, Details, ViewDetails, Create/Edit GET, Edit/Confirm POST delegating to services, Delete.
- `NormMaterialApiController`: `GetMaterialData` (plus batch), `SearchMaterials` (select2 remote), `Calculate`.
- `Services/Norm/NormSaveService`: Confirm body (lines 1016-1466) covering input lines, NORMLINES aggregation, BOM rebuild and BOMS_AND_NORMS link, in one transaction with preloaded dictionaries.
- `Services/Norm/NormCuttingOptimizer`: `OptimizeMaterialCutting` + `ApplyBaseConsumptionWaste` (1838-2016). Pure functions with unit tests.
- `Services/Norm/NormLineMath`: conversions shared with Tannarx and BOM (Duplicates #1-#3).
- `Services/Norm/NormViewModelBuilder`: `PopulateCuttingLinesFromPeriodAsync`, `RecalculateTotalsForViewModelAsync`, materials/units ViewBag, `GetPreviousNormOptionsAsync`, `ApplyTemplateFromNormVersionAsync`.

**ProductPlanController (1074 lines)**
- `ProductPlanController`: CRUD + Details.
- `ProductPlanGridController`: GridCreate, GridEdit, DeleteGridRow.
- `ProductPlanDailyController`: UpdateDailyPlan, GetDailyGridPartial.
- `ProductPlanService`: overlap check, SO validation, daily generation and regeneration, actual aggregation.
- Delete the dead preview and validation methods.

**ProductProductionController (1034 lines)**
- `ProductionEntryService`: Create/Edit/Delete stock flows (reverse income/consumption + post) behind one transaction API. Computes requirements once and passes them to both validation and consumption.
- `ProductionDetailsQuery`: Details expense lookup.
- `ProductionPlanLookup`: planned products.
- Move `ReconcileProductionConsumption` to an admin maintenance controller (POST).

**TannarxAnalyticsService (1529 lines; a service, but god-sized)**
- `ActualCostEngine` (month loop).
- `MonthCostContext` (preloaded maps).
- `MaterialCostPathResolver` (Path1/2/3).
- `TransportCostAllocator` (finished and material).
- `CalculationNoteBuilder`.
- `TannarxDashboardQuery` (dashboard, trends, report).

---

## Cross-module notes (shared or core layer)

1. **`UserHelper.GetUserId`** (Controllers/AdminSetting/UserHelper.cs:12) opens a new DbContext and queries USERS on every call, with no caching. It is called in loops and inside every `LogHelper.LogToDatabase`. Cache it (like RoleHelper does) or put the user id in the auth ticket.
2. **`LogHelper.LogToDatabase`** opens its own context + SaveChanges per call.
   - Called inside `TransactionScope` and `BeginTransaction` blocks (Norm Confirm and Delete, production flows), so it enlists a second connection (risk of MSDTC promotion), and logs roll back with the business transaction.
   - Most callers pass the message as the `actionName` argument, so the free text lands in `USERLOGS.ActionName` (a `text` column) while `details` stays null.
   - Suggested fix: queue logs per request and flush them once after commit.
3. **Permissions:** move to a global filter (see B05). View-level `@if RoleHelper ... else Response.Redirect` is used in every view. It is redundant once a filter exists, and `Response.Redirect` from a view after rendering has started is fragile.
4. **Text-encoded foreign keys** are the root cause of many LIKE scans and fragile regexes. They should become real columns:
   - `ProductionIncomeMarker` in `EXPENSES.Description`
   - `[RejaSana:]` in `PRODUCT_PRODUCTION.Comment`
   - `ProdID:x:q` in `EXPENSE_PRODUCTS.Comment`
   - `Buy#`/`B:` tokens in `P_ORDER_PRODUCTS.Priority`
   - `NORM_INPUT_LINES.Month` string instead of NormVersionID
5. **LookupService with cache** for UNITS, SHOPS, DEFECT_TYPES, PRODUCT_CATEGORIES and finished goods / materials projections. These are needed across MES, SRM and Finance.
6. **`_Layout.cshtml`** loads jQuery twice (`@Scripts.Render("~/bundles/jquery")` at 1743 and `jquery.min.js` at 1779; the comments mention `noConflict`). Inline scripts inside the body break (B15). Chart.js is loaded unversioned from a CDN in MESDashboard (`cdn.jsdelivr.net/npm/chart.js`), 3.9.1 in ProductPlan, and local apexcharts in Tannarx. Standardize on one local bundle.
7. **Product images** are stored as DB varbinary and inlined as base64 everywhere. Use a shared image endpoint with caching.
8. **`InventoryValuationService`, `WarehouseFlowService` and `ExpenseStockIssueHelper`** are the stock core used by production. The pricing bug B11 and per-line SaveChanges likely exist in other callers too.
9. A shared `ExceptionHelper.ToUserMessage`, `UzMonths`, and a `BaseController` (CurrentUserId, PopulateLookups, Dispose of `db`) would remove dozens of copies across modules.

---

## Recommended order of work (quick wins first)

1. **B01** IsDeleted filters in ProductProduction (S, prevents silent stock corruption). Then **B02** transaction on production delete (S).
2. **P02/P03** SQL-side date filters in GetPlannedProducts / LoadPlannedProducts / Create (S, large win). **P18** default date filters on list pages (S).
3. **P01** MES Dashboard bottlenecks as 3 grouped queries (S-M; currently the worst page).
4. **B05** global permission filter (M; then delete the view-level redirects gradually). **B13** Reconcile to POST (S).
5. **B06** norm period-change data loss (S). **B03** wrong invoice-lock guard (S). **B12** NRE in DamagedProduct (S).
6. **P05** set-based "products with norms" (Tannarx Index); **P13** Norm Details N+1; **P16/P17** QI and lot helpers (all S).
7. **P14** Norm Confirm preload + single save, extracted into `NormSaveService` (M), together with **Duplicates #1-#5** (NormLineMath).
8. **P09** BOM tree preload + memoized requirements (M; speeds up every production entry, consumption and report).
9. **P15 + Duplicates #6**: Norm Create/Edit into one partial + JS file with remote select2 (M).
10. **B04** decide the QI workflow (add or remove views, fix Approve); **B07/B08** BOM-per-norm-version model (M, needs business sign-off).
11. **P07/P08** Tannarx actual-cost engine refactor (L); choose SP or C#.
12. Dead-code removal pass (DamagedPartController, empty services, unused methods and views): S, low risk, do it alongside the items above.
