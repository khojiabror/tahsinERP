# CRM module audit

## Scope

Read in full: 9 CRM controllers, 2 SRM controllers used for comparison, 2 VAT helpers, 42 CRM views and 14 CRM models. Supporting code was spot-read where it affects CRM behaviour: `RoleHelper`, `FilterConfig`/`Global.asax`, `WarehouseFlowService.GetSalesWarehouse/DecreaseSalesStock`, `InventoryValuationService.GetQty/GetPositiveStocks`, `DlocFifoSchema.IsEnabled`, `SoftDeleteReferenceService`, `ValidateAntiForgeryHeaderAttribute`, `Views/SupplierBase/*` and `Views/Supplier/*` (diffed), and the `SUPPLIER`/`SUPPLIER_BASE` models.

**Controllers (11):** `Controllers/CRM/SalesInvoicesController.cs` (1309 lines), `SOrderController.cs` (1034), `SContractController.cs` (708), `SalesTTNKController.cs` (509), `SalesSFController.cs` (428), `CRMDashboardController.cs` (506), `CustomerController.cs` (407), `CustomerBaseController.cs` (297), `SalesReportsController.cs` (280); `Controllers/SRM/SupplierController.cs` (357), `SupplierBaseController.cs` (297).

**Services (2):** `Services/SContractVatHelper.cs`, `Services/PContractVatHelper.cs`.

**Views (42):**
- `SalesInvoices/`: Index, Create, CreateFromOrder, Edit, Details, Delete, Print
- `SOrder/`: Index, Create, Edit, Details, Delete, DeliverySchedule
- `SContract/`: Index, Create, Edit, Details, Delete, ManageOrders
- `SalesTTNK/`: Index, CreateFromInvoices, Edit, Details
- `SalesSF/`: Index, GenerateFromInvoice, Edit, Details
- `CRMDashboard/`: Index
- `Customer/`: Index, Create, Edit, Details, Delete
- `CustomerBase/`: Index, Create, Edit, Details, Delete
- `SalesReports/`: Index, MonthlyReport, SalesByCustomer, SalesByProduct

**Models (14):** `CUSTOMER`, `CUSTOMER_BASE`, `SALES_INVOICES`, `SALES_INVOICE_LINES`, `SALES_SF_HEADERS`, `SALES_SF_LINES`, `SALES_TTNK_HEADERS`, `SALES_TTNK_INVOICES`, `SALES_TTNK_LINES`, `SORDER_DELIVERY_SCHEDULE`, `S_CONTRACTS`, `S_CONTRACT_PRODUCTS`, `S_ORDERS`, `S_ORDER_PRODUCTS`.

Two facts change how every finding below should be read:

1. **Permissions are not enforced on POST actions.** The only global filters are `HandleError`, `Authorize` and `LicenseCheck`. Most CRM permission checks happen inside the *views* (`@if (RoleHelper.IsViewPermitted...) else Response.Redirect`). That means:
   - The action's queries run before the check.
   - POST/AJAX endpoints have no role check at all.
   - The only server-side checks are in CRMDashboard, SalesReports, CustomerBase, and SContract's Delete actions.
2. **Lazy loading is ON** (`LazyLoadingEnabled="true"` in the EDMX). Every `using (db)` block disposes the context before the view renders. Any navigation property the view touches that wasn't `Include`d will either:
   - throw `ObjectDisposedException`, or
   - silently work through relationship fix-up if the related entity happens to be tracked already.

## Findings

Format: ID | Severity | Category | Location | Problem and impact | Proposed fix | Effort | Risk of change

### Critical / High

**CRM-B01 | Critical | Bug | `SalesInvoicesController.cs:353-415` and `825-887`**
- **Problem:** Creating an invoice is not atomic.
  1. The header is committed with `SaveChanges` at 354/826 and the lines at 376/848.
  2. Then `DecreaseSalesStock` runs per line, each call in its own `TransactionScope`.
  3. If line N fails with "yetarli mahsulot yo'q", the exception goes to the outer catch and the user sees an error. But the invoice, its lines, and the stock decreases for lines 1..N-1 are already committed.
  4. Any other stock error is only logged and swallowed, so the invoice exists with no stock movement. Finance posting errors are swallowed the same way.
- **Impact:** Orphan invoices and stock/finance mismatches.
- **Fix:** Wrap header, lines, stock and posting in one `db.Database.BeginTransaction()` (or one outer `TransactionScope`) inside a new `SalesInvoiceService.Create(...)`. Stop matching on exception message text: rethrow every stock error and roll back.
- **Effort:** M | **Risk:** Med

**CRM-B02 | High | Bug | `SalesInvoicesController.cs:480-494`**
- **Problem:** `GetNextInvoiceNo` takes the last *non-cancelled* invoice by ID and adds 1.
  - If the newest invoice is cancelled (`DeleteConfirmed` sets CANCELED), its number is handed out again, producing duplicate invoice numbers.
  - There is no lock or unique index, so two concurrent creates get the same number.
- **Fix:** Use a SQL `SEQUENCE` or a counters table updated with `UPDLOCK`, include cancelled invoices when finding the maximum, and add a unique index on `InvoiceNo`.
- **Effort:** S | **Risk:** Low

**CRM-B03 | High | Bug | `Views/SalesInvoices/Edit.cshtml:105-110` and `SalesInvoicesController.cs:1037,1046-1122`**
- **Problem:** The Edit form shows the wrong product and can silently change it.
  - `Html.DropDownList("lines[i].ProductID", ViewBag.Products, new { @Value = line.ProductID })` does **not** select the saved product. The `Value` attribute is ignored on `<select>`, so every row shows the first option.
  - `ViewBag.Products` only contains products with stock > 0 in the sales warehouse, and the invoice itself just used that stock, so the original product is often missing from the list entirely.
  - Saving the form replaces the line products with whatever is selected.
  - Edit also deletes and re-inserts lines without touching stock (no rollback or re-issue), so stock drifts from what invoices say.
- **Fix:**
  - Build a per-row `SelectList(items, "Value", "Text", line.ProductID)`.
  - Always include the products already on the invoice in the list.
  - In POST, call `RollbackSalesInvoiceStockAndDeleteAutoExpenses` and then re-issue stock inside one transaction, or forbid editing lines after stock has been issued.
- **Effort:** M | **Risk:** Med

**CRM-S01 | High | Sec | All CRM POST actions**
- **Problem:** There are no server-side permission checks on: SalesInvoices Create/CreateFromOrder/Edit/Delete, SOrder Create/Edit/Delete/SaveDeliverySchedule, SContract Create/Edit/LinkOrder/UnlinkOrder/DeleteProduct, SalesTTNK, SalesSF, and all Customer actions. Any authenticated user can post directly.
- **Fix:** Add a `[Permission("SalesInvoices", Change=true)]` action filter, applied per controller (or globally via controller/action route data), that calls `RoleHelper`. Then remove the in-view `Response.Redirect` checks.
- **Effort:** M | **Risk:** Med

**CRM-S02 | High | Sec/Bug | `SContractController.cs:596-636` and `Views/SContract/Edit.cshtml:141`**
- **Problem:** `DeleteProduct` is a **GET** that deletes a contract line and recalculates the amount, with no CSRF protection and no permission check. Any link or image tag can trigger it.
- **Fix:** Make it `[HttpPost][ValidateAntiForgeryToken]`, add a permission check, and refuse to delete if the product is already ordered or invoiced.
- **Effort:** S | **Risk:** Low

**CRM-S03 | High | Sec/Bug | `SalesInvoicesController.cs:706-848`, `SOrderController.cs:164-283`, `SalesSFController.cs:237-254`**
- **Problem:** Server code trusts prices and amounts from the browser.
  - Invoice `UnitPrice` is `readonly` only in HTML. The server never re-reads `S_CONTRACT_PRODUCTS.PiecePrice`.
  - `VATRate` is freely editable.
  - SOrder takes `PiecePrice` from a hidden input.
  - SF lines store client-sent `LineAmount/VATAmount/TotalWithVAT` with no recalculation.
- **Fix:** On POST, load the contract product price and VAT server-side and recompute every amount. Use the client values only for qty, product and description.
- **Effort:** M | **Risk:** Med

**CRM-B04 | High | Bug | `CustomerController.cs:271-405`**
- **Problem:** The Excel import in `CustomerController` is a copy of the Supplier one.
  - `UploadWithExcel`/`Save` read and write **`db.SUPPLIERS`**, so customers imported here become suppliers.
  - `Download` serves `taminotchilar.xlsx` (the suppliers template).
  - `Views/Customer/UploadWithExcel.cshtml` does not exist, so the "Excel" button on `Customer/Create.cshtml:39` throws "view not found".
  - The POST `Save` endpoint is still reachable.
  - `supplierName` is a controller field.
- **Fix:** Delete the block, or rewrite it against `CUSTOMERS` using a shared `CounterpartyExcelImporter`.
- **Effort:** S | **Risk:** Low

**CRM-B05 | High | Bug | `Views/SalesInvoices/Index.cshtml` (no pager) with `SalesInvoicesController.cs:89-98`; `Views/Customer/Index.cshtml` with `CustomerController.cs:61-69`**
- **Problem:** The controller pages server-side (`Take(25)`), but the view has no pager and wraps the rows in a client DataTable.
  - Users only ever see the 25 newest invoices / first 25 customers.
  - Search and Excel/PDF export only cover those 25 rows.
- **Fix:** Either use DataTables server-side processing (`/SalesInvoices/List` JSON endpoint with paging, sorting and search) or render the `_Pager` partial (see CRM-D17).
- **Effort:** M | **Risk:** Low

**CRM-P01 | High | Perf | `CRMDashboardController.cs:52`**
- **Problem:** `contracts.Count(c => db.SALES_INVOICES.Any(inv => inv.ContractID == c.ID))` runs **one SQL query per contract** on every dashboard load.
- **Fix:** Use `var ids = db.SALES_INVOICES.Where(i=>i.ContractID!=null).Select(i=>i.ContractID.Value).Distinct().ToHashSet();` and count in memory, or do the whole count in one SQL statement.
- **Effort:** S | **Risk:** Low

**CRM-P02 | High | Perf | `CRMDashboardController.cs:45-101,418-420`**
- **Problem:** The dashboard loads every row of `S_CONTRACTS`+CUSTOMER, `S_ORDERS`+CUSTOMER+CURRENCY, `SORDER_DELIVERY_SCHEDULE`+ORDER+CUSTOMER+PRODUCT, `SALES_INVOICES`+CUSTOMER and `CUSTOMERS` as tracked entities, then counts, sums and groups in memory. Cost grows linearly with history on every visit.
- **Fix:**
  - Push the KPIs into SQL: `Count`, `Sum` and `GroupBy(Year, Month)` on `IQueryable`.
  - Fetch only the top-N lists with `Take` plus projection.
  - Add `AsNoTracking`.
  - Better still, move to a `vw_CRM_Kpis` view or stored procedure and cache the result for 5 minutes.
- **Effort:** M | **Risk:** Low

**CRM-P03 | High | Perf | `SalesInvoicesController.cs:958-997` (`BuildSalesWarehouseProductSelectList`)**
- **Problem:** For each finished-good product the builder runs `db.PRODUCTS.Find(pid)` (a DB hit, because the entity isn't tracked) plus `InventoryValuationService.GetQty` (1–2 queries). That is roughly 2–3N queries.
- **Where it runs:** every Create GET, Edit GET, and every validation-error re-render. Create POST error paths call it up to 4 times.
- **Fix:** Use the existing bulk `InventoryValuationService.GetPositiveStocks(db, salesWH.ID)` and join to one projected `PRODUCTS` query (`ID, PNo, Name`).
- **Effort:** S | **Risk:** Low

**CRM-B06 | High | Bug | `SOrderController.cs:509-536`**
- **Problem:** `SaveDeliverySchedule` deletes every schedule row for the order and re-inserts them with `QtyShipped = 0` and `StatusCode = "PLANNED"`.
  - Shipped quantities are lost.
  - `QUALITY_INSPECTION` rows that reference the schedule are orphaned, or the save fails on the foreign key.
  - On error the action returns **JSON** to a normal (non-AJAX) form post, so the user sees a raw JSON page.
- **Fix:** Upsert by schedule `ID` (the view already has the IDs), keep `QtyShipped`, refuse to drop rows that have shipments or inspections, and return a View with ModelState errors.
- **Effort:** M | **Risk:** Med

**CRM-B07 | High | Bug | `SalesReportsController.cs:43,86-88,154-175,233-239`; `CRMDashboardController.cs:96-101`**
- **Problem:** Reports and dashboard totals are wrong in two ways.
  - The sales reports and dashboard invoice totals include **CANCELED** invoices.
  - The date upper bound is a midnight value (`periodEnd = first.AddMonths(1).AddDays(-1)`, `to = DateTime.Now.Date`) compared with `InvoiceDate <= periodEnd`. Invoices carry a time of day (`DateTime.Now`), so sales on the last day of the period are excluded.
- **Fix:**
  - Use `InvoiceDate < periodEnd.AddDays(1)`, i.e. a half-open range.
  - Add the same `StatusCode NOT IN ('CANCELED','CANCELLED')` filter used in `SalesInvoices.Index`.
  - Extract both into a shared `SalesInvoiceQueries.Active(db)`.
- **Effort:** S | **Risk:** Low

### Medium

**CRM-B08 | Med | Bug | `Views/SalesInvoices/Create.cshtml:418-425` and `Edit.cshtml:361-368`**
- **Problem:** Removing a middle line leaves a gap in the `lines[i]` indices (no re-indexing). The MVC binder stops at the first gap, so lines after it are **silently dropped** when the invoice is saved.
- **Same pattern elsewhere:** `SContract/Edit.cshtml:282-285` (`removeProductRow` has no reindex). There, `SContractController.Edit` POST also **deletes** existing products that aren't in the bound list (`:456-464`).
- **Fix:** Re-index on removal (as `SOrder/Create.cshtml:336` does), or post `lines.Index` hidden fields.
- **Effort:** S | **Risk:** Low

**CRM-B09 | Med | Bug | `Views/SOrder/Details.cshtml:129-131` and `SOrderController.cs:592-602`**
- **Problem:** `ViewBag.ScheduleSummary` is a `Dictionary<int, anonymous>`. Casting it with `as Dictionary<int, dynamic>` always gives `null`, so the planned/shipped summary column never shows.
- **Fix:** Create a typed `ScheduleSummaryDto` and use a typed dictionary.
- **Effort:** S | **Risk:** Low

**CRM-B10 | Med | Bug | `SOrderController.cs:750-787`**
- **Problem:** Order Edit deletes all `S_ORDER_PRODUCTS` rows and re-inserts them, so their IDs change. `SORDER_DELIVERY_SCHEDULE.SOrderProductID` has no foreign key, so existing schedules end up pointing at deleted IDs and disappear from Details/DeliverySchedule. Edit and Delete also don't check for existing invoices.
- **Fix:** Update lines in place by ProductID, and block the edit or delete when invoices exist.
- **Effort:** M | **Risk:** Med

**CRM-B11 | Med | Bug | `SOrderController.cs:997-1016`**
- **Problem:** `SetupViewBagForError` calls `LoadContractProducts`, which **overwrites** `model.ProductQuantities` with `OrderQuantity = 0`. On any validation error, everything the user typed is lost.
- **Fix:** Merge posted quantities into the reloaded list.
- **Effort:** S | **Risk:** Low

**CRM-B12 | Med | Bug | `SContractController.cs:693-705` and `657-662`**
- **Problem:**
  - `UnlinkOrder` sets `order.ContractID = 0`, but `ContractID` is a non-null foreign key to `S_CONTRACTS`. That gives either an FK violation or a dangling reference.
  - The `ManageOrders` filter `o.ContractID == 0 || o.ContractID != contractID` lists orders that belong to *other* contracts, so the user can "steal" them.
- **Fix:** Make `ContractID` nullable (or forbid unlinking), and filter to unlinked orders only.
- **Effort:** S | **Risk:** Med

**CRM-B13 | Med | Bug | Several `ToDictionary(p => p.ProductID, ...)` calls on `S_CONTRACT_PRODUCTS`**
- **Locations:** `SalesInvoicesController.cs:232-238,640-646`, `SOrderController.cs:237-243,758-765`, `SContractVatHelper.cs:120`.
- **Problem:** These throw `ArgumentException` if a contract has the same product twice. Nothing in the Create/Edit UI prevents that.
- **Fix:** Use `GroupBy(ProductID)` (as `SOrderController.BuildProductVatRates:884-892` already does) in a single helper.
- **Effort:** S | **Risk:** Low

**CRM-B14 | Med | Bug (verify) | Views touching non-included navigation properties after the context is disposed**
- **Locations:**
  - `Views/SalesTTNK/Index.cshtml:100-102` (`item.SALES_TTNK_INVOICES` → `.SALES_INVOICES`; only `SALES_INVOICES` is Included).
  - `Views/SContract/Details.cshtml:224` (`order.CURRENCy1`).
  - `Views/SContract/ManageOrders.cshtml:77,132` (`order.CURRENCy1`).
- **Problem:** With lazy loading on and the context disposed, these throw `ObjectDisposedException` unless relationship fix-up happens to have loaded the entity. TTNK Index will fail as soon as any TTNK exists.
- **Fix:** Add `Include(...)` or projections, and consider `LazyLoadingEnabled=false` per context in read actions.
- **Effort:** S | **Risk:** Low

**CRM-B15 | Med | Bug | `SalesInvoicesController.cs:1059-1063`, `SalesTTNKController.cs:226-241,392-396`, `SalesSFController.cs:210-223,344-348`**
- **Problem:** Several error paths `return View(model)` without rebuilding ViewBag. The view then throws a NullReference on `ViewBag.Customers as SelectList` / `ViewBag.Invoices`, turning a validation error into a 500.
- **Fix:** Use a single `PopulateFormLookups()` called on every return path.
- **Effort:** S | **Risk:** Low

**CRM-B16 | Med | Bug | `SalesInvoicesController.cs:192-478` vs `706-956`**
- **Problem:** The two invoice-create paths enforce different business rules.
  - `CreateFromOrder` checks order quantity, quality-inspection-accepted quantity and produced quantity.
  - Standalone `Create` (the only one linked from the UI) checks sales-warehouse stock only, with no order-quantity cap and no QI gating.
  - `CreateFromOrder` itself has no UI entry point (dead, see the Dead code section).
- **Fix:** Put one validation pipeline in `SalesInvoiceService.Validate(...)`.
- **Effort:** M | **Risk:** Med

**CRM-B17 | Med | Bug | `SalesTTNKController.cs:151-343`**
- **Problem:** The TTNK create flow (`CreateFromInvoices`) isn't linked from anywhere: Index has no Create button and invoice Details links only to the TTNK Index. The flow also:
  - `long.Parse`s raw query input (500 on bad input);
  - throws in `invoices.First(i => i.ID == invoiceID)` if an ID is missing;
  - never generates `TTNKNumber`.
- **Fix:** Add an entry point from invoice Details, validate the IDs, and generate the number.
- **Effort:** S | **Risk:** Low

**CRM-B18 | Med | Bug | `CRMDashboardController.cs:53,66,101,207`; `SalesReportsController.cs:191-192`**
- **Problem:** Amounts in different currencies (orders/contracts/invoices with `CurrencyID`/`CurrencyCode`) are summed into one "so'm" figure, and "InvoicesOverdue" ignores whether the invoice was paid.
- **Fix:** Group by currency, or convert via the rates table. Compute overdue from the receivable balance (which the same action already computes at 233-498).
- **Effort:** M | **Risk:** Low

**CRM-P04 | Med | Perf | Index dropdowns in `SalesInvoicesController.cs:101-105`, `SalesTTNKController.cs:88-90`, `SalesSFController.cs:81-82`, `SOrderController.cs:94-95`, `SContractController.cs:82`**
- **Problem:** Filter dropdowns load full entities: all CUSTOMERS, S_ORDERS, S_CONTRACTS, and **all SALES_INVOICES rows, unfiltered** in TTNK and SF. `ViewBag.Contracts` in SalesInvoices.Index isn't even used by the view.
- **Fix:**
  - Project `Select(x => new { x.ID, x.Name })` with `AsNoTracking`.
  - Replace the invoice/order dropdowns with select2 AJAX search.
  - Cache the customer list for 1–5 minutes in a `LookupCache`.
- **Effort:** S | **Risk:** Low

**CRM-P05 | Med | Perf | `SOrderController.cs:91` and `SContractController.cs:79`**
- **Problem:** Index loads **all** orders/contracts with Includes (full tracked entities) for client-side DataTables.
- **Fix:** Use server-side DataTables with projection and `AsNoTracking`, sharing one `DataTableRequest` helper.
- **Effort:** M | **Risk:** Low

**CRM-P06 | Med | Perf | `SalesInvoicesController.cs:273-329` (CreateFromOrder)**
- **Problem:** Each line runs 4–6 queries (already invoiced per order, QI sum, total invoiced per product, produced sum, plus `PRODUCTS.Find` on error).
- **Fix:** Pre-aggregate with `GroupBy(ProductID)` for all line product IDs in 3 queries.
- **Effort:** S | **Risk:** Low

**CRM-P07 | Med | Perf | `SOrderController.cs:419-442`**
- **Problem:** In `DeliverySchedule`, `s.PRODUCT` is lazy-loaded once per schedule row (N+1). `Details` already does `Include(d => d.PRODUCT)`.
- **Fix:** Add `.Include(s => s.PRODUCT)`, or project.
- **Effort:** S | **Risk:** Low

**CRM-P08 | Med | Perf | `SalesReportsController.cs:60-89,168-175,224-239`**
- **Problem:** Reports materialise full tracked line entities with 2–3 Includes, then group in memory.
- **Fix:** Do `GroupBy(ProductID).Select(new { Qty = Sum, Amt = Sum })` in SQL with `AsNoTracking`, then look up product names in one projected query.
- **Effort:** S | **Risk:** Low

**CRM-P09 | Med | Perf | `CustomerController.cs:120-131`**
- **Problem:** Details `Include`s the full `S_ORDERS` and `S_CONTRACTS` collections only to count them. The invoice count includes cancelled invoices.
- **Fix:** Use three `CountAsync` queries (as `SupplierController.Details:149-162` does).
- **Effort:** S | **Risk:** Low

**CRM-S04 | Med | Sec | Missing ownership and status checks**
- **Problem:**
  - `SalesInvoices.Print` has no permission check in either controller or view.
  - `Customer.Delete` POST throws NRE when the ID is not found (`:254`).
  - `Customer.Delete` doesn't run soft-delete reference cleanup (Supplier has `AfterSupplierSoftDeletedAsync`; there is no Customer equivalent).
- **Fix:** Add the permission filter and an `AfterCustomerSoftDeletedAsync`.
- **Effort:** S | **Risk:** Low

**CRM-M01 | Med | Maint | `SalesInvoicesController.cs` (1309 lines) and `SOrderController.cs` (1034)**
- **Problem:** Both are god-controllers with copy-pasted blocks.
- **Fix:** Split them (see the "Suggested split" section).
- **Effort:** L | **Risk:** Med

### Low

**CRM-S05 | Low | Sec | `Views/CRMDashboard/Index.cshtml:498-556`**
- **Problem:** Chart labels are built by string concatenation inside `Html.Raw`. Only quotes are escaped, so a customer name containing `\` or `</script>` breaks the JS (stored XSS).
- **Fix:** Use `@Html.Raw(Json.Encode(Model.TopCustomers.Select(x=>x.CustomerName)))`.
- **Effort:** S | **Risk:** Low

**CRM-B19 | Low | Bug | `SContractController.cs:133-135,394-396`**
- **Problem:** `int.Parse` on Amount means fractional quantities crash. `double.Parse` uses the current culture, so a comma vs dot decimal separator breaks it. The same culture issue affects `double.TryParse` in `SOrderController.cs:179,188` and `SContractVatHelper:168-169,190`.
- **Fix:** Use `CultureInfo.InvariantCulture` and model binding instead of FormCollection.
- **Effort:** S | **Risk:** Low

**CRM-B20 | Low | Bug | `Views/SalesInvoices/Edit.cshtml:347-349` and `361-368`**
- **Problem:** Newly added rows get both a per-button handler and a document-level delegate, so the user is asked to confirm twice.
- **Fix:** Keep only the delegate.
- **Effort:** S | **Risk:** Low

**CRM-B21 | Low | Bug | `SalesInvoicesController.cs:229` vs `CreateFromOrder.cshtml:7`**
- **Problem:** The view reads `ViewBag.Products`, which the controller never sets, so the product PNo is always blank.
- **Fix:** Use `op.PRODUCT` (already Included).
- **Effort:** S | **Risk:** Low

**CRM-M02 | Low | Maint | Models**
- **Problem:** Redundant currency fields: `S_ORDERS.Currency` (string) alongside `CurrencyID`, and `SALES_INVOICES.CurrencyCode` alongside `CurrencyID`. `CurrencyID` is never set by any CRM code.
- **Fix:** Pick one source of truth.
- **Effort:** M | **Risk:** Med

**CRM-M03 | Low | Maint | Inline JS/CSS**
- **Problem:** About 3.5k lines of view-specific JS are inline: invoice Create/Edit ≈ 300 lines each, SOrder Create/Edit ≈ 190 each, SContract Create/Edit, Dashboard ≈ 130. They can't be cached or minified.
- **Fix:** Move them to `Scripts/app/*.js` and add them to bundles.
- **Effort:** M | **Risk:** Low

## Duplicates map

| # | What is duplicated | Locations | Proposed single home |
|---|---|---|---|
| D1 | **CustomerBase ≡ SupplierBase** (whole CRUD; identical after renaming) | `Controllers/CRM/CustomerBaseController.cs` (297) ≡ `Controllers/SRM/SupplierBaseController.cs` (297); `Views/CustomerBase/{Create,Edit,Details,Delete}` ≡ `Views/SupplierBase/*` (0 diff lines after rename), Index differs only in filter/delete UX; `Models/CUSTOMER_BASE.cs` ≡ `SUPPLIER_BASE.cs` | Either one `PROSPECT_BASE` table with `Kind` (C/S) plus one `ProspectBaseController`, or a generic `BaseProspectController<TEntity>` with two 10-line subclasses and shared views under `Views/Shared/ProspectBase/` |
| D2 | Customer vs Supplier CRUD and Excel import | `CustomerController` ↔ `SupplierController` (same scalar schema in `CUSTOMER`/`SUPPLIER`); Excel import `CustomerController:271-405` is a copy of Supplier's (and still writes `SUPPLIERS`); views Create/Edit/Delete differ by 25–56 lines | `CounterpartyService` (duplicate-name check, soft delete, Excel import) plus `_CounterpartyForm.cshtml` partial |
| D3 | VAT math | `SContractVatHelper` ↔ `PContractVatHelper`: `GetVatPercentFromLine`, `CalculateVatAmount`, `CalculateContractVatRate(FromLines)`, `BuildContractVatRates`, `ContractProductVatLine`, `ParseVatPercentFromForm`, `GetProductVatRatesByContractAsync` are line-for-line the same; the only difference is the base amount (S: `PiecePrice*Amount`, P: `Amount`) | `Services/VatMath.cs` (pure math) plus two thin adapters exposing `GetLineBase(entity)`; also line totals in `SalesInvoicesController:336-338,810-812,1092-1094` |
| D4 | "ProductID → VAT %" dictionary for a contract | `SalesInvoicesController:232-238, 640-646`; `SOrderController:237-243, 758-765, 884-892`; the unused helper `SContractVatHelper.GetProductVatRatesByContractAsync:113` | `SContractVatHelper.GetProductVatRates(db, contractId)` (with GroupBy) |
| D5 | `GetContractsByCustomer` AJAX (identical) | `SalesInvoicesController:580-613` ≡ `SOrderController:310-343`; SRM `POrdersController:1054` / `PInvoicesController:1090` are the same pattern | `SalesLookupController` (`/SalesLookup/ContractsByCustomer`) |
| D6 | Invoice totals loop | `SalesInvoicesController:332-345, 806-819, 1086-1116` | `SalesInvoiceService.ComputeTotals(header, lines)` |
| D7 | Invoice line entity creation | `SalesInvoicesController:357-374, 829-846, 1098-1111` | `SalesInvoiceService.BuildLines` |
| D8 | Stock decrease plus finance posting block | `SalesInvoicesController:379-427` ≡ `851-899` | `SalesInvoiceService.Post(invoice)` (transactional) |
| D9 | Inner-exception message unwrap loop | SalesInvoices 450-457, 922-929, 1142-1149; SOrder 295-301, 547-553, 799-805; SContract 226-232, 519-525; TTNK 314-320, 475-481; SF 264-270, 398-404 (plus 20 controllers project-wide) | `ExceptionHelper.FullMessage(ex)` in Core |
| D10 | `DbEntityValidationException` formatting | `SalesInvoicesController:433-447, 905-919` | same `ExceptionHelper` |
| D11 | Customer → Contract → Order cascade SelectLists | SalesInvoices 543-569, 942-950, 1025-1035, 1157-1165; SOrder 121-143, 997-1016, 1018-1032 (`SetupViewBagForError` ≡ `SetupViewBagForEdit`) | `SalesSelectListBuilder.Customers/ContractsFor/OrdersFor` |
| D12 | Error-path ViewBag reload repeated four times in one action | `SalesInvoicesController:752-756, 765-769, 779-783, 796-800` | `PopulateCreateLookups(db, invoice)` |
| D13 | Contract-form lookups (Customers/Products/Units/currencies), 8 copies | `SContractController:106-109, 155-158, 165-168, 222-225, 365-368, 418-421, 428-431, 515-518` | `PopulateContractLookups(db, model)` |
| D14 | FormCollection repeater parsing | SOrder 169-202 ≡ 669-702; SContract 123-150 ≡ 382-413 | Delete it and rely on model binding (`List<ContractProductQuantity>` with reindexed names) |
| D15 | Details vs Print data loading | `SalesInvoicesController:143-187` ≡ `1252-1291` | `LoadInvoiceViewModel(db, id)` |
| D16 | Index filter/sort/paging boilerplate | SalesInvoices, SOrder, SContract, SalesTTNK, SalesSF, Customer `Index` | `PagedQuery<T>` helper plus server-side DataTables endpoint |
| D17 | Pager markup | `Views/SalesTTNK/Index.cshtml:131-165` ≡ `Views/SalesSF/Index.cshtml:134-168` | `Views/Shared/_Pager.cshtml` |
| D18 | DataTable language and Excel/PDF export config (about 80 lines each) | `SalesInvoices/Index:228-305`, `Customer/Index:128-205`, `SOrder/Index`, `SContract/Index`, `CustomerBase/Index` (49 views project-wide) | `Scripts/app/datatable-defaults.js` (`$.extend($.fn.dataTable.defaults, …)`) |
| D19 | Invoice form JS (loadContracts/loadOrders/loadPriceForProduct/calculateTotals/addLine) | `Views/SalesInvoices/Create.cshtml:194-490` ↔ `Edit.cshtml:194-410` | `Scripts/app/sales-invoice-form.js` plus `_InvoiceLines.cshtml` partial |
| D20 | Order form JS (loadContracts/loadProducts/reindex/calculateTotal) re-implemented in two styles | `SOrder/Create.cshtml:180-370` ↔ `SOrder/Edit.cshtml:178-345` | `Scripts/app/sales-order-form.js` |
| D21 | Contract product repeater JS | `SContract/Create.cshtml:165-290` ↔ `SContract/Edit.cshtml:204-285` (Edit lacks reindex) | `Scripts/app/contract-products.js` (shared with PContracts) |
| D22 | Report print popup script | `SalesReports/MonthlyReport.cshtml:154-170` ≡ `SalesByCustomer:108-124` ≡ `SalesByProduct:118-134` | `Scripts/app/print-area.js` |
| D23 | Report preamble (permission, date normalise/swap, company name) | `SalesReportsController:35-57, 151-166, 207-222`; `CRMDashboardController:34-36` | `ReportContext` helper / base `ReportController` |
| D24 | Line view models | `SalesInvoiceLineViewModel` ≈ `SalesSFLineViewModel` (`SalesInvoicesController:1297`, `SalesSFController:415`), declared inside controller files | `ViewModels/Sales/*.cs` |

## Dead code

| Item | Location | Confidence |
|---|---|---|
| `SalesInvoices.CreateFromOrder` GET/POST plus `CreateFromOrder.cshtml`: no link anywhere | `SalesInvoicesController:192-478` | High (no reference found) |
| `SalesTTNK.CreateFromInvoices` GET/POST plus view: no entry point (TTNK create unreachable from the UI) | `SalesTTNKController:151-343` | High; this is more a missing link than code to delete |
| `Customer.ViewOrders/ViewContracts/ViewInvoices` redirect actions: never referenced (Details links directly) | `CustomerController:83-108` | High |
| `Customer.Download/UploadWithExcel/ClearDataTable/Save`: views missing, and the code targets SUPPLIERS | `CustomerController:261-405` | High |
| `CustomerController.supplierName` field | `:22` | High |
| `SContractVatHelper.DefaultVatPercent` (JS hard-codes 12 instead), `GetProductVatRatesByContractAsync` | `SContractVatHelper.cs:13,113` | High (no callers) |
| `ViewBag.Contracts` in `SalesInvoices.Index` (the view has no contract filter); `ViewBag.Products` read in `CreateFromOrder.cshtml` but never set | `SalesInvoicesController:105` | High |
| `ViewBag.ScheduleSummary` rendering (the cast always gives null, so it is effectively dead) | `SOrder/Details.cshtml:129` | High |
| `CustomerBase.DeleteConfirmed` returns JSON on failure from a non-AJAX POST; Index uses `DeleteAjax`, so the Delete view is probably unused | `CustomerBaseController:229-260` | Medium |
| Unused `using` directives (`System.Configuration`, `System.Threading.Tasks`, `tahsinERP.Controllers`, `System.Web.Security`, `tahsinERP.Filters`) | several CRM controllers | High |
| Commented `//CustomerID = …` | `SalesTTNKController:187` | High |
| Unused `page`/`pageSize` params on `SOrder.Index` | `SOrderController:18` | High |

## Suggested split of god-controllers

**SalesInvoicesController (1309 lines)** becomes:
- `SalesInvoicesController` (≈250 lines): Index, Details, Print, Delete, plus thin Create/Edit actions that call the service.
- `Services/Sales/SalesInvoiceService.cs`:
  - `Validate(header, lines, mode)`, which merges the Create and CreateFromOrder rules (CRM-B16);
  - `ComputeTotals`;
  - `Create(header, lines, user)`: one transaction covering header, lines, `DecreaseSalesStock` and `FinancePostingService` (CRM-B01);
  - `Update(...)` with stock rollback and re-issue (CRM-B03);
  - `Cancel(id)`;
  - `GetNextInvoiceNo` (sequence based, CRM-B02).
- `Services/Sales/SalesSelectListBuilder.cs`: customer/contract/order cascades (D11, D12) and the bulk `SalesWarehouseProducts` list (CRM-P03).
- `SalesLookupController` (JSON only): `GetContractsByCustomer`, `GetOrdersByContract`, `GetOrderProductsForInvoice`, `GetContractPriceForProduct`, `GetContractCurrency`, plus SOrder's `GetContractsByCustomer` and `GetContractProducts` (D5).
- `ViewModels/Sales/SalesInvoiceLineViewModel.cs` and `SalesInvoiceFormViewModel` (replacing about 10 ViewBag keys).

**SOrderController (1034 lines)** becomes:
- `SOrderController`: Index, Details, Create, Edit, Delete (≈450 lines once the parsing and lookups are gone).
- `SOrderDeliveryScheduleController`: `DeliverySchedule` and `SaveDeliverySchedule` with upsert (CRM-B06).
- `Services/Sales/SalesOrderService.cs`: `ValidateOrderQuantities`, `BuildOrderLines` (VAT and total), in-place line update (CRM-B10).
- Merge `SetupViewBagForError` and `SetupViewBagForEdit` into one method (D11), and delete the FormCollection parsing (D14).

**SContractController (708 lines)**, optional: extract `PopulateContractLookups` (D13) and a `SalesContractService` (Create/Edit lines and amount recalculation, shared with `DeleteProduct`).

## Cross-module notes

These belong in the shared/core layer:
- **Permission enforcement** should be an action filter (`[Permission(module, change)]`) instead of in-view `Response.Redirect`. There are 387 in-view redirects across Views. This affects every module.
- **`ExceptionHelper.FullMessage`** (D9): 20+ controllers.
- **`datatable-defaults.js`** (D18): 49 views. Also a shared **`_Pager.cshtml`** and a server-side DataTables helper (`DataTableRequest`/`PagedResult`).
- **VAT math** (D3) is shared by CRM, SRM (`PContracts`, `POrders`, `PInvoices`, `Incomes`) and Finance (`OpeningBalanceController`).
- **Counterparty abstraction:** `CUSTOMER`/`SUPPLIER` and `CUSTOMER_BASE`/`SUPPLIER_BASE` are schema-identical pairs (D1, D2).
- **Document numbering:** replace max+1 with a `DocNumberService` backed by SQL SEQUENCE / UPDLOCK. `WarehouseFlowService.GetNextTransferDocNo` has the same risk pattern.
- **`WarehouseFlowService.DecreaseSalesStock`** opens its own `TransactionScope` per call. Callers can't make multi-line issues atomic without an ambient scope, so the service API should accept the caller's transaction.
- **`WarehouseFlowService.GetSalesWarehouse`:** `.AsEnumerable()` over all warehouses on every call, plus a hard-coded fallback to ID 7. Cache it.
- **Lazy loading:** consider `Configuration.LazyLoadingEnabled = false` (or projections everywhere) for read actions to make disposed-context crashes and N+1 visible (CRM-B14).
- **Active-invoice filter:** "not CANCELED/CANCELLED" appears in more than 6 places with two spellings. Use one status constant class plus a `SalesInvoiceQueries.Active()` extension.

## Recommended order of work

1. **Quick wins: low risk, under a day each.**
   - CRM-P01: dashboard N+1.
   - CRM-P03: bulk stock list via `GetPositiveStocks`.
   - CRM-B07: report date bound and cancelled filter.
   - CRM-S02: `DeleteProduct` to POST plus CSRF.
   - CRM-B04: remove the broken Customer Excel import.
   - CRM-B09: typed schedule summary.
   - CRM-B13: GroupBy in VAT dictionaries (D4).
   - CRM-B08: reindex JS.
   - CRM-P07: Include PRODUCT.
   - CRM-B14: missing Includes.
   - CRM-P04: projected dropdowns.
2. **Data integrity.**
   - CRM-B02: invoice numbering (sequence plus unique index).
   - CRM-B01: transactional invoice create.
   - CRM-B03: invoice edit (product selection and stock).
   - CRM-B06: delivery schedule upsert.
   - CRM-B10: order edit in place.
   - CRM-B12: unlink order.
3. **Security.**
   - CRM-S01: server-side permission filter (core work, then apply to CRM).
   - CRM-S03: recompute prices/VAT server-side.
   - CRM-S04 and CRM-S05.
4. **UX correctness.** CRM-B05: server-side DataTables for SalesInvoices and Customer (also covers CRM-P05 for SOrder/SContract). Then CRM-B11, CRM-B15 and CRM-B17.
5. **Refactor.**
   - Extract `SalesInvoiceService` and `SalesLookupController` (D5–D8, D11, D12, CRM-B16).
   - Split SOrder (delivery schedule controller).
   - Merge the VAT helpers (D3).
   - Merge CustomerBase/SupplierBase (D1), then Customer/Supplier (D2).
6. **Front-end cleanup.** Move inline JS to bundles (D18–D22, CRM-M03), add a `_Pager` partial, and add Create/Edit form partials.
7. **Dashboard and reports.** Move KPIs to SQL views/SP with short caching (CRM-P02, CRM-P08), and make them currency-aware (CRM-B18).
