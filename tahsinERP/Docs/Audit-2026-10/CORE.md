# CORE module audit (cross-cutting layer)

Root: `C:\Work\SCHAIN\tahsinERP\tahsinERP\tahsinERP`. Paths below are relative to that root. Audit was read-only.

## Scope

Read in full (49 code/config files + 40 views):

- Pipeline/config: `Global.asax.cs`, `Web.config`, `Web.Release.config`, `App_Start/BundleConfig.cs`, `App_Start/BundleConfig2.cs`, `App_Start/FilterConfig.cs`, `App_Start/RouteConfig.cs`, `libman.json`, `packages.config`
- Filters: `Filters/LicenseCheckAttribute.cs`, `Filters/ValidateAntiForgeryHeaderAttribute.cs`
- Helpers: `Helpers/LicenseHelper.cs`, `Helpers/LoginThrottle.cs`, `Helpers/PasswordHasher.cs`. The other files in Helpers/ are module-specific and were not reviewed.
- `Configuration/DbConfig.cs`
- Controllers/AdminSetting: `AccountController`, `AdminPanelController`, `ComingSoonController`, `CookieHelper`, `DeletedRecordsController`, `ErrorController`, `Helper`, `HomeController`, `LicenseController`, `LogController`, `LogHelper`, `NetworkHelper`, `PModuleController`, `PermissionController`, `RoleController`, `UserController`, `UserEntriesController`, `UserHelper`, `UserManualController`, `ViewNotAuthorizedController`
- `Controllers/ReportsController.cs`
- Models: `UsersRoleProvider.cs`, `RoleHelper.cs`, `DBTHSNModel.Context.cs`, `DBTHSNModel.Context.partial.cs`, `DBTHSNModel.Context.HR.cs`, `DataTableModel.cs`
- Services: `UserActionLogger.cs`, `IUserActionLogger.cs`, `LicenseService.cs`, `SoftDeleteReferenceService.cs`. `UnitConversionService.ConvertToUnitAsync` was also read because ReportsController calls it.
- Views: `Shared/_Layout.cshtml`, `Shared/_AdminPanelLayout.cshtml`, `Shared/_UserProfile.cshtml`, `Shared/_LicenseBanner.cshtml`, `Shared/Error.cshtml`, `Home/*` (4), `AdminPanel/Index`, `Role/*` (6), `User/*` (5), `Log/Index`, `UserEntries/Index`, `DeletedRecords/Index`, `PModule/*` (6), `Permission/Edit`, `Reports/*` (3), `Account/Login`, `Account/Settings`, `License/Index`, `License/Blocked`, `Error/*`, `ViewNotAuthorized/Index`, `Scripts/csrf.js`
- `Database_Migrations/*` and `Scripts/*.sql`, checked for index definitions
- Repo-wide greps over all 107 controllers and 447 views for permission patterns, duplicate helpers and front-end includes

## Per-request cost (answers to the key questions)

**What the pipeline does for an authenticated GET that renders `_Layout`:**

| Stage | DB round-trips, warm cache | DB round-trips, cold cache | Cached? |
|---|---|---|---|
| FormsAuth + RoleManager (`UsersRoleProvider`) | 0. Nothing calls `IsInRole` or `[Authorize(Roles)]` (0 usages). | 0 | n/a |
| Global `AuthorizeAttribute` | 0 | 0 | n/a |
| `LicenseCheckAttribute` → `LicenseService.GetCachedStatus` | 0 | about 5–6, at most once per 5 min per app (INSTALLATION_ID read, COMPANY_LICENSES, LAST_SEEN read, possibly an UPDATE) | MemoryCache 5 min. Good. |
| Global log filter | none exists. `LogActionFilter` is mentioned in a Global.asax comment but no such class exists. | – | – |
| Action permission check (`RoleHelper.IsView/ChangePermitted`) | 0 | 3 per (user, module) key | HttpRuntime.Cache, 10 min **absolute** |
| `_Layout` "Layout" permission + 165 `IsViewPermitted` calls (69 distinct modules) | 0 | **~207** (69 × 3) | same cache |
| `_Layout` user block: FullName/Position/Roles | 0 | 1–2 | cached. Empty role lists are **not** cached. |
| `_Layout` badge block (lines 925–950) | **3–4**: `UserHelper.GetUserId`, `APP_TASKS` count joined to status, `WAREHOUSES` by MRP, `INCOMES` count | same | **not cached** |
| `_Layout` line 972: `UserHelper.GetUserId` again | **1** | 1 | **not cached** |
| `_LicenseBanner` | 0 | uses license cache | cached |
| **Total before/around the action** | **4–5 queries, 3 DbContexts** | **~215 queries, ~75 DbContexts/connections** | |

- **Permission data.** It is cached per user per key. The whole set expires together every 10 minutes because the expiry is absolute and all keys are written in the same first request. `RoleHelper.ClearAllCache()` runs after any role or permission edit, which makes every logged-in user go cold at once (thundering herd).
- **Logging.** Nothing logs on every request. `LogHelper.LogToDatabase` (282 calls in 75 files) runs synchronously on write actions. Each call costs a `GetUserId` query, a `Dns.GetHostEntry` lookup, enumeration of `ServerVariables.AllKeys`, and an INSERT through a new DbContext.
- **Session lock.** TempData (328 usages) is backed by session, and `HomeController.Index` writes `Session[...]`. As a result every MVC request of a user takes an exclusive session lock, so parallel AJAX calls from one browser are serialized.

**Permission-check implementation, repo-wide.** There is no BaseController and no permission attribute. Checks are copy-pasted:

- **586** `RoleHelper.Is*Permitted(...)` calls in **78** controller files, and **544** copies of `return RedirectToAction("Index","ViewNotAuthorized");`
- Variant A, `RouteData.Values["controller"]/["action"]`: 154
- Variant B, module string such as `"Supplier"`: 330
- Variant C, literal controller and action: 97
- Variant D, other: 5 (`SalesReportsController:18-20` OR-composite, `ReconciliationController:180,229` variable module)
- Views: **683** `RoleHelper.*` calls in **386** views. Controllers also push `ViewBag.*Change*` flags in 8 places.
- **25 controllers have no server-side permission check at all.** 20 of them are business controllers. See CORE-S02.

**Front-end payload per `_Layout` page:**

- 22 local `<script>` tags + 6 CDN scripts, about **3.5 MB of JS** uncompressed:
  - jQuery is loaded **3 times**: the `~/bundles/jquery` bundle (3.7.1, unminified while debug=true), `vendors.min.js` (which embeds jQuery 3.3.1 and Bootstrap 4.3.1), and `assets/js/jquery.min.js` (3.6.0). It is then juggled with `jQuery.noConflict(true)`.
  - Bootstrap JS is loaded **twice**: `Scripts/bootstrap.bundle.js` v5.3.3 (208 KB, unminified) and BS4 inside vendors.
  - LivIcons is 261 KB and is unused; there are no `livicon-evo` elements in any view.
  - pdfmake + vfs_fonts + jszip (about 1.9 MB from CDN) load on every page, although only about 52 views use export buttons.
- 16 stylesheets, about **800 KB of CSS**, including `dark-layout.css` (66 KB), which the light theme does not use.
- Each HTML response also carries about **32 KB of inline CSS** (_Layout lines 34–873) and about **15 KB of inline JS**, which cannot be cached.
- Only the `~/bundles/jquery` bundle is used. `BundleConfig2.cs` is a verbatim copy of BundleConfig, is not in the csproj and is never registered. `EnableOptimizations` is never set, so minification depends on debug=false.
- 9 views (Currency, Unit, HSCode, FTransportType, Forwarder, FContract Index; InventoryReports/Dashboard; POrderPayment/Create; Koreshok) load a 4th jQuery copy from CDN.

**web.config:**

- `compilation debug="true"`. The Release transform removes it, which is fine if Release is always used.
- `customErrors mode="On"`, but `Views/Shared/Error.cshtml` prints `ex.Message` and `InnerException.Message`.
- No `<staticContent><clientCache>`, so about 35 assets are revalidated with 304s on every navigation.
- No dynamic compression for the about 100 KB HTML.
- No output caching anywhere (0 `[OutputCache]`).
- RoleManager points at a buggy, unused provider.

## Findings

| ID | Sev | Cat | file:line | What & why | Proposed fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| CORE-S01 | **Critical** | Sec | Controllers/AdminSetting/AccountController.cs:18, 293-328, 330-413; Views/Account/Settings.cshtml:62 | The whole `AccountController` is `[AllowAnonymous]`. `SaveSettings` loads the user by the **posted** `uvm.UName` (the readonly attribute is client-side only), then overwrites Password, Email, FullName and the photo. Any logged-in user, and possibly an anonymous one with a token from the login page, can reset the **Admin/Developer password**. `Settings(userName)` shows any user's profile. `GetUserImage(id)` is an anonymous IDOR. | Move `[AllowAnonymous]` to `Login` (GET/POST) and `SetDeviceId` only. In `Settings`/`SaveSettings`, ignore the `userName`/`UName` input and use `User.Identity.Name`. Require the current password before setting a new one. Call `RoleHelper.ClearUserCache`. | S | Low |
| CORE-S02 | **Critical** | Sec | Controllers without any check: CRM/SOrderController (1034 lines), CRM/SalesInvoicesController (1309), CRM/SalesSFController, CRM/SalesTTNKController, CRM/CustomerController, MES/ProductPlanController (1074), MES/MESDashboardController, SRM/ProductionToSalesTransferController, SRM/HSCodeController, Settings/Currency, Unit, Company, Engineering/ShopController, DamagedPart, DamagedProduct, DefectType, ProductionProcess, Api/TransportLoadApi, HomeController (all JSON KPI endpoints incl. finance), AdminSetting/LogController, AdminSetting/PermissionController | The menu hides links, but the URLs work for **any authenticated user**: create/edit/delete sales orders, invoices, plans and currencies, read all finance KPIs, and read the full audit log. | Add a global `PermissionFilter` (IAuthorizationFilter) registered in FilterConfig. It resolves the module from `[Permission("Module", Change=true)]` on the controller or action. When there is no attribute, it falls back to the `controller/action` row and treats GET as View and POST as Change. Opt out with `[SkipPermission]` (Account, Error, License, ViewNotAuthorized, ComingSoon, UserManual). This also lets the 586 copy-pasted checks be removed incrementally. | M | Med |
| CORE-S03 | High | Sec | UserController.cs:133, 288; 188-377, 421-502 | The Developer guard `(RName != "Developer" \|\| ID != 1)` only blocks when **both** the name is "Developer" and ID == 1. A non-Developer admin can assign the Developer role if its ID is not 1. `Edit`, `Delete` and password reset of an existing Developer user are not guarded, because Index merely hides those users, so the target can be reached by URL. | Make `IsDeveloperUser(target)` and `IsDeveloperRole(role)` checks mandatory in Edit GET/POST, Delete and Details. Compare by name only, case-insensitively. | S | Low |
| CORE-S04 | Med | Sec | PModuleController.cs:166, 274, 466; RoleController.cs:113-126 | Default-open permissions. Every new permission module gives `ViewPermit=true` to **all** roles, and every new role gets View on **every** module. | Default to `ViewPermit=false` and let the admin grant explicitly. | S | Med |
| CORE-S05 | Med | Sec | NetworkHelper.cs:35-330 (GetClientIpAddress, GetIPv4FromRequest) | It trusts client-supplied `X-Forwarded-For`, `X-Real-IP`, `CF-Connecting-IP`, `True-Client-IP` and `X-Client-IP`. It also scans **all** request headers and server variables for anything that looks like an IPv4 address. The IPs in USERLOGS and USER_ENTRIES are therefore forgeable. LoginThrottle correctly uses `UserHostAddress`. | Use `Request.UserHostAddress`. Honour XFF only when REMOTE_ADDR is in a configured `TrustedProxies` appSetting. Delete `GetIPv4FromRequest`. | S | Low |
| CORE-S06 | Med | Sec | Views/Shared/Error.cshtml:5-63; 30 occurrences of `Json(new{error/message = ex.Message})` (e.g. HomeController:156-722, DeletedRecords:110, PModule) | Internal exception and SQL messages are shown to users. | Log the exception (Trace or a log table) and show a generic message plus a correlation id. | S | Low |
| CORE-S07 | Med | Bug/Sec | RoleHelper.cs:492 | `user.ROLES.First().ID`: only the **first** role is used, so users with more than one role get permissions in arbitrary order. | Union over all role IDs (MAX of View/Change). This is covered by P02's snapshot. | S | Low |
| CORE-S08 | Med | Bug/Sec | UserController.cs:231-359, 463-491; LicenseController RevokeSeat | Role change, deactivation, deletion and seat revocation do not invalidate the user's permission cache (`ClearUserCache` is never called). The auth cookie stays valid (sliding 60 min), so a disabled user keeps working for 10–60+ minutes. | Call `RoleHelper.ClearUserCache(uname)`. In PostAuthenticate (or the new PermissionFilter), check a cached "user active + seat" flag with a 1–2 min TTL. | S | Low |
| CORE-P01 | **High** | Perf | Views/Shared/_Layout.cshtml:925-950, 972 | Every page runs **4–5 uncached queries** with a new DbContext: GetUserId ×2, a COUNT on APP_TASKS joined to the status ref, WAREHOUSES where MRP=user, and a COUNT on INCOMES. This happens on every full page view, including every Index/Create/Edit. | Add `RoleHelper.GetUserId(uname)` (cached). Move the badge counts to `/Home/HeaderBadges` (JSON, called once by JS) or a `[ChildActionOnly][OutputCache(Duration=60, VaryByCustom="user")]` partial. | S | Low |
| CORE-P02 | **High** | Perf | Models/RoleHelper.cs:308-539 + _Layout.cshtml:877-1720 (165 calls / 69 modules) | Permissions are cached per (user, module) key. Each miss costs 3 queries (USERS+ROLES join, PERMISSIONMODULES, PERMISSIONS). The first page after login, and **every 10 minutes** after that (absolute expiry, all keys expire together), costs **~210 queries / ~70 connections per user**. `ClearAllCache()` after any permission edit makes all users cold at once. | Build a **per-user permission snapshot** with one query: `PERMISSIONS ⋈ PERMISSIONMODULES WHERE RoleID IN (user roles)` → `Dictionary<string,(bool v,bool c)>` keyed by Module and by `ctrl/action[/param]`. Cache it with sliding 20 min plus a global "permissions version" counter (incremented on edit) instead of enumerating the cache. Also render the menu once per role set (`VaryByCustom="roles"`). | M | Med |
| CORE-P03 | **High** | Perf | _Layout.cshtml:10-31, 1743-1783; _AdminPanelLayout.cshtml:160-195 | About 3.5 MB JS and 0.8 MB CSS per page, triple jQuery, double Bootstrap JS (v5 + v4), unused LivIcons, PDF libraries on every page, 47 KB of inline CSS/JS per HTML response. Load-order hacks (`noConflict(true)`) make it fragile. | (1) Remove `~/bundles/jquery`, `Scripts/bootstrap.bundle.js`, `assets/js/jquery.min.js` and the noConflict block, and load the repeater after vendors (vendors.min.js already has jQuery 3.3.1 + BS4). (2) Remove LivIcons (3 files) and dark-layout.css. (3) Move jszip/pdfmake/vfs_fonts/buttons into a `@section exportScripts` rendered only by views with export buttons, or lazy-load them on click. (4) Move the inline `<style>` and print/menu JS to `assets/css/ipack.css` and `assets/js/layout.js`. (5) Create 2 bundles (`~/bundles/vendor`, `~/Content/theme`) with `EnableOptimizations=true` to get versioned, minified URLs. (6) Remove the 4th jQuery from the 9 views listed above. | M | Med |
| CORE-P04 | Med | Perf | Web.config:25-47, 169-185 | No `clientCache`, so about 35 conditional requests per navigation. No dynamic compression for the about 100 KB HTML. `debug=true` in the base config: if anyone publishes Debug, bundles are unminified and the local DB is used (DbConfig `#if DEBUG`). | Add `<staticContent><clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="30.00:00:00"/></staticContent>` together with versioned bundle URLs, and `<urlCompression doStaticCompression="true" doDynamicCompression="true"/>`. Add a build check that rejects a Debug publish. | S | Low |
| CORE-P05 | Med | Perf | TempData (328 uses), HomeController.cs:18, HrTimesheetsController.cs:722-742 | Session-backed TempData plus a session write means the default `SessionStateBehavior` takes an exclusive lock per user. Concurrent AJAX calls (dashboards, Select2 searches) are serialized. HrTimesheets also puts Excel byte[] into InProc session memory. | Switch TempData to a cookie provider (`CookieTempDataProvider`, about 40 lines) or mark AJAX/read controllers `[SessionState(SessionStateBehavior.ReadOnly)]`. Delete `Session["HasSeenCookieConsent"]`. Store temporary Excel files in MemoryCache or a temp file keyed by a GUID. | M | Med |
| CORE-P06 | High | Perf/Dead | HomeController.cs:13-46 | The landing page (the first hit after login) runs `CookieHelper.IsConfirmed`: GetUserId, IP resolution with DNS, and a COOKIES_CONFIRMATION query. It also runs a **GROUP BY over the whole FIN_TRANSACTIONS** to find a "primary currency". Neither `ViewBag.ShowCookieConsent` nor `ViewBag.CurrencyShortname` is used in `Views/Home/Index.cshtml`. | Delete both blocks. `Index()` becomes `return View();`. | S | Low |
| CORE-P07 | High | Perf/Bug | HomeController.cs:508-724 (`GetExecutiveDashboard`, the only Home endpoint used, Views/Home/Index.cshtml:324) | Loads **all** APP_TASKS rows (to count them), a dictionary of **all** product names (to label 12 rows), and all consumption lines in memory. The trend makes 5 × 12 passes with `ToString("yyyy-MM")` per element. **Bug:** the trend covers 12 months from `trendFrom`, but contracts, orders, invoices, plans and production are loaded only for `[from,to]` (default 1 month), so 11 of the 12 trend months are always 0. | Do the KPI counts as SQL `Count()`. Add a separate trend query grouped by `Year,Month` from `trendFrom`. Look up product names only for the top 12 IDs. Cache the result for 60 s per (from, to, wh). | M | Low |
| CORE-P08 | High | Perf | UserController.cs:391-409 | `Include(u => u.USERLOGS)` loads the user's **entire** log history (unbounded; every write action logs) and then does `Take(100)` in memory. | `db.USERLOGS.AsNoTracking().Where(l=>l.UserID==id).OrderByDescending(l=>l.DateTime).Take(100)`. | S | Low |
| CORE-P09 | High | Perf/Sec | AdminSetting/LogController.cs:287-338; UserEntriesController.cs:103-145 | No paging: 30 days of logs plus `Include(USER)` are sent to a client-side DataTable. `SELECT DISTINCT ControllerName FROM USERLOGS` scans the whole table on every request. **LogController has no permission check.** | Add a permission check, server-side DataTables (`Skip/Take` + count), a static module list (or one cached for 1 h) and AsNoTracking projection. | M | Low |
| CORE-P10 | Med | Perf | LogHelper.cs:102-225; NetworkHelper.cs:338-365; UserActionLogger.cs:223-251 | Synchronous audit write per call: `GetUserId` query, `Dns.GetHostEntry(Dns.GetHostName())` (blocking DNS) on each call, up to 6× `ServerVariables.AllKeys` enumeration, a new DbContext and an INSERT. UserActionLogger (used by 11 HR controllers) also calls `GetIpAddress()` (DNS) twice and logs the **server** IP instead of the client IP. | Cache server IPs in a static Lazy. Take the user ID from the RoleHelper cache. Merge the two loggers into one `IAuditLogger` that enqueues to a `BlockingCollection` drained by a background worker (`HostingEnvironment.QueueBackgroundWorkItem`) with batched inserts. | M | Low |
| CORE-P11 | Med | Perf | AdminPanelController.cs:26-70; Views/AdminPanel/Index.cshtml:259-271 | 11 sequential COUNTs, of which 2 are unused by the view (`TotalLogs`, `TotalPermissions`, both full counts on large tables). `DbFunctions.TruncateTime(DateTime)==` is non-sargable and scans USERLOGS and USER_ENTRIES. All users and roles are loaded, and the view lazy-loads `role.PERMISSIONS` per role (N+1, full rows only to `.Count`). | Use range predicates `DateTime >= today && < tomorrow`, drop the unused counts, project `Roles.Select(r=>new{r.ID,r.RName,Cnt=r.PERMISSIONS.Count()})`, and AsNoTracking. | S | Low |
| CORE-P12 | Med | Perf | LicenseController.cs:193-218 | N+1: one USER_LICENSES query per user plus `RoleHelper.IsDeveloper(u.Uname)`, which on a cold cache is one more query per user. | One left-join query of USERS × USER_LICENSES (active license) plus a USERROLES join on the Developer role. | S | Low |
| CORE-P13 | Med | Perf | ReportsController.cs:454-793 (`Coverage`) | Loads full PRODUCT_STOCKS with 3 includes, all active BOMs with lines (then `component.PRODUCT_CATEGORIES` lazy-loads per line, N+1), and full PRODUCT_PRODUCTION rows for a 12-month window. `unitConversionService.ConvertToUnitAsync` runs inside the days × FG × component loop and does a PRODUCTS query per call when units differ (up to thousands of queries). | Precompute a `factor[(rawId, fromUnit, toUnit)]` once before the loop, include `BOM_LINES.Select(l=>l.PRODUCT.PRODUCT_CATEGORIES)` or project, project production to `{ID, ProductID, Date, Total}`, and add AsNoTracking. | M | Low |
| CORE-P14 | Med | Perf/Bug | ReportsController.cs:58-63, 230-235, 499-504 | ABC, SafetyStock and Coverage each load EXPENSES `Include(EXPENSE_PRODUCTS)` as full tracked entities. **Bug:** `IssueDateTime <= to`, where `to` is the date at 00:00, excludes almost all of the last day. | One shared SQL projection grouped by `(ProductID, DbFunctions.TruncateTime(IssueDateTime))` with `< to.AddDays(1)`. | S | Low |
| CORE-P15 | Med | Perf | Services/SoftDeleteReferenceService.cs (all 30 methods) | Every referencing row is materialized and tracked just to null one FK (e.g. deleting a supplier loads all of its P_ORDERS). | Set-based `ExecuteSqlCommand("UPDATE … SET X=NULL WHERE X=@p0")`, but see B01 first. | S | Med |
| CORE-P16 | Low | Perf | PModuleController.cs:84-88, 238-246, 320-321, 666-675 | `Assembly.GetTypes()` reflection on every call, and `Create` loads the whole PERMISSIONMODULES table to detect duplicates. | Static `Lazy<List<string>>` of controller names and a SQL `Any()` for the duplicate check. | S | Low |
| CORE-P17 | Low | Perf | Configuration/DbConfig.cs:61-70 | `GetEntityConnectionString()` rebuilds `EntityConnectionStringBuilder` for each of the 677 `new DBTHSNEntities()` call sites. | Cache it in a `Lazy<string>`. | S | Low |
| CORE-P18 | Low | Perf | Global.asax.cs:22-27; Web.config:30 | A `new CultureInfo("en-US")` is created per request and contradicts `globalization culture="uz-UZ"`. | Set `culture="en-US" uiCulture="en-US"` in web.config, or use `CultureInfo.GetCultureInfo` (cached). | S | Low |
| CORE-P19 | Low | Perf | RoleHelper.cs:250-280 | Empty role arrays are not cached, so a user without roles hits the DB on every layout render. | Cache `new string[0]` too. | S | Low |
| CORE-P20 | Low | Perf | AccountController.cs:74-76 | `Uname.ToLower() == x.ToLower()` is non-sargable, so the USERS table is scanned per login. | Rely on the CI collation: `u.Uname == user.UName`. | S | Low |
| CORE-B01 | **High** | Bug | Services/SoftDeleteReferenceService.cs:14-365 (29 call sites) | "Soft" delete **destroys history** by nulling FKs on historical documents. Deleting a Supplier sets `P_ORDERS.SupplierID=NULL`. Deleting a Currency nulls `HR_PAYROLL_LINES.CurrencyID` and `F_WAYBILL_PAYMENTS.CurrencyID` (amounts lose their currency). Deleting an S_ORDER nulls the OrderID on invoices, TTNK and waybills. Deleting a User nulls task assignee/reporter and DOC creator/approver. Deleting a Warehouse nulls `EXPENSES.ReceiverWhID` and `INCOMES.SenderWHID`. Deleting a Product removes BOM components. `DeletedRecords/Recover` cannot restore any of these links. | Stop nulling FKs on transactional and historical tables; keep the reference and filter `IsDeleted` in dropdowns and new-document validation. Keep the nulling only for pure configuration links if really needed. | M | Med |
| CORE-B02 | High | Bug | DeletedRecordsController.cs:775-826 | `Recover` just sets `IsDeleted=false` through `dynamic` and marks the whole entity Modified. For EXPENSES, INCOMES, PRODUCT_PRODUCTION and FIN_TRANSACTIONS this restores a document **without re-posting stock or finance**. For USER there is no seat or username-uniqueness check. For ROLE the user links were already removed. | Allow Recover only for master/reference data. For documents, call the module's own restore service (re-post stock/finance in one transaction) or forbid it. | M | Med |
| CORE-B03 | Med | Bug | UserController.cs:113-174 | No transaction around: USER insert → SaveChanges → raw SQL USERROLES insert → image SaveChanges. A failure midway leaves a user without a role, and a retry fails with "username exists". | Wrap it in `db.Database.BeginTransaction()`. Validate that the password is non-null before hashing. | S | Low |
| CORE-B04 | Med | Bug/Dead | AdminPanelController.cs:76-259 | `Users`, `Roles`, `Permissions`, `SavePermissions`, `Logs` and `UserEntries` have **no views** (only `AdminPanel/Index.cshtml` exists), so they throw if called. `SavePermissions` also does one query per module row. | Delete them. Role, PModule, Log and UserEntries controllers already cover these features. | S | Low |
| CORE-B05 | Med | Bug/Dead | PermissionController.cs (whole file); Views/Permission/Edit.cshtml | The POST writes back the same values it read (a no-op), with `SaveChanges` and `LogToDatabase` per row. There is no permission check. There are no views for `Index` or `Permissions`, and `Edit.cshtml` has no action. | Delete the controller and the view. | S | Low |
| CORE-B06 | Low | Bug/Dead | Models/UsersRoleProvider.cs (whole file); Web.config:37-42 | It queries by `Email`, but `Identity.Name` is `Uname`, so `IsUserInRole` is always false. `FindUsersInRole` always returns empty. `Get*PermissionInRole` returns `Any()` instead of the permit value. It is registered but nothing calls it. | Remove the `<roleManager>` element and the file, or rewrite it to delegate to `RoleHelper.GetUserRoles`. | S | Low |
| CORE-B07 | Low | Bug | PModuleController.cs:79, 115, 326-328 | Operator precedence bug in `GetActions` (`!NonAction && ActionResult \|\| Task<ActionResult>`). It also misses JsonResult and FileResult actions. `MissingControllers` and `CreateDefaultModules` need PERMISSIONMODULE rows for those exact actions, otherwise even admins are locked out. | Use `typeof(ActionResult).IsAssignableFrom(unwrapTask(m.ReturnType))` and check the "PModule" module-level permission. | S | Low |
| CORE-B08 | Low | Dead/Bug | HomeController.cs:725-738; CookieHelper.cs | The cookie-consent flow is dead: `ShowCookieConsent` is never rendered and `CookieConfirm` is never called. `CookieConfirm` is GET-able, writes a row and then throws (Json without AllowGet). | Delete CookieHelper, CookieConfirm and the COOKIES_CONFIRMATION usage. | S | Low |
| CORE-M01 | Low | Maint | Global.asax.cs:16; FilterConfig.cs | The comment says "LogActionFilter" is added, but the class does not exist. Global filters are split between FilterConfig and Global.asax. | Register all global filters (HandleError, Authorize, LicenseCheck, the new PermissionFilter) in `FilterConfig`. | S | Low |
| CORE-M02 | Low | Maint | DbConfig.cs:34-46 | The AES key and IV are in source next to the ciphertext (documented as obfuscation only). | Keep env/file override as the primary path in production (already supported). | – | – |

## Duplicates map

| Duplicated thing | Locations | Proposed single home |
|---|---|---|
| Permission check + redirect boilerplate | 586 `RoleHelper.Is*Permitted` calls in 78 controllers; 544 `RedirectToAction("Index","ViewNotAuthorized")`; 4 call variants (A/B/C/D above) | `Filters/PermissionAttribute` + global `PermissionFilter`. Keep `RoleHelper` as the data source only. |
| Role/permission lookup | `Models/RoleHelper.cs` (used) vs `Models/UsersRoleProvider.cs` (dead, buggy) | RoleHelper → `Services/PermissionService` with a per-user snapshot |
| Current user ID lookup (DB per call) | `UserHelper.GetUserId` 87 calls (HrPieceRateTimesheets ×9, HrPayroll ×8, Tasks ×6, HrEmployees ×6, Inventory, Incomes, OfficialLetters, SalesInvoices ×4 …; _Layout ×2); `SalesInvoicesController.GetCurrentUserID` (line 1232); `UserHelper.GetUser` / `GetFullName` (12) | `RoleHelper.GetUserId(uname)` (cached), exposed as `BaseController.CurrentUserId` |
| Audit logging | `LogHelper.LogToDatabase` (282 calls / 75 files); `Services/UserActionLogger` (11 HR controllers); `AccountController.SetUserEntry`; `CookieHelper.Confirm` | One `IAuditLogger` with an async queue |
| Client IP resolution | `NetworkHelper.GetClientIpAddress` used in LogHelper:119-123, CookieHelper:28,64, AccountController:212; `NetworkHelper.GetIpAddress` (server IP) in UserActionLogger:32-33; `SendARP`/`inet_addr` P/Invoke duplicated in AccountController:21-25 (unused) and NetworkHelper:11-15 | `NetworkHelper.GetClientIp(HttpRequestBase)` (simple, trusted-proxy aware) |
| Password hashing | `Helpers/PasswordHasher` (live, incl. `LegacyHash`); `Controllers/AdminSetting/Helper.EncodePassword`, `EncodePasswordMd5`, `GeneratePassword` (`System.Random`), `base64Encode/Decode`, `ToAbsoluteUrl`: 0 usages | Keep PasswordHasher, delete Helper.cs |
| Photo upload → byte[] + size check | AccountController.SaveSettings:368-392; UserController.Create:149-173; UserController.Edit:315-351 (and the `photoMaxSize` appSetting read in 3 files) | `Helpers/UploadHelper.ReadImage(HttpPostedFileBase, maxBytes, out error)` |
| User image → base64 data-URI | UserController:222, 258, 348, 374, 415, 455; AccountController:323 | `UserImageHelper.ToDataUri(userId)` or just `<img src="/Account/GetUserImage/{id}">` (secured) |
| Roles SelectList refill | UserController:89, 109, 143, 171, 184, 225, 254, 344, 370 (9×); PModuleController:366-376, 505-512 | `private SelectList RolesSelectList(int? selected)` + cached roles |
| Assign default permissions to roles for a new module/role | PModuleController.CreateDefaultModules:154-176; PModuleController.Create:265-283; RoleController.Create:114-128 | `PermissionService.SeedForModule(moduleId)` / `SeedForRole(roleId)` |
| PermissionModuleViewModel build | PModuleController.Delete:539-558 and Details:638-657 | private method |
| Controller-name reflection | PModuleController:84-88, 320-321, 668-674 | static Lazy cache |
| "Deleted records" query per table | DeletedRecordsController.cs:272-760 (**54 near-identical** `GetDeletedX` methods) + 54-case `switch` (155-267) + 54-case Recover switch (775-826) | A registry `Dictionary<string, DeletedModuleDef>` with `Func<DBTHSNEntities, string, IQueryable<DeletedRecordItem>>` projection: one generic method, about 150 lines instead of 830 |
| Soft-delete reference sync/async twins | SoftDeleteReferenceService: AfterShopSoftDeleted(Async), AfterDefectTypeSoftDeleted(Async), AfterProductCategorySoftDeleted(Async), AfterProductPlanSoftDeleted(Async) | Keep sync set-based SQL versions only (fast, no deadlock concern) |
| Soft-delete flag assignment | 70 `.IsDeleted = true;` in 51 controllers | `SoftDeleteService.Delete<T>(db, id, user)` that also logs |
| Demand-statistics pipeline (load EXPENSES lines → daily dict → avg/std/CoV → ABC/XYZ cut 80/95, 0.1/0.25 → z-table) | ReportsController.ABCSegmentation:58-170; SafetyStock:230-349; Coverage:499-591; HomeController.GetExecutiveDashboard:585-659 (coverage days) | `Services/DemandStatisticsService` (one SQL projection + pure calculator) |
| Warehouse SelectList building in reports | ReportsController:34-37 / 75-84 / 175-180, 204-207 / 359-364, 400-403 / 869-874 | `ReferenceDataCache.Warehouses()` + helper |
| Excel (EPPlus) setup | `ExcelPackage.LicenseContext = NonCommercial` set in **20** places in 12 files (Customer, Product, FinPlPlans, FinTransactions ×3, OpeningBalance ×2, HrPayroll, HrStaffingPlan, HrTimesheets ×3, Waybill ×2, Inventory ×4, ProductStock); 18 Export/Template/Upload actions with hand-rolled header/column code | Set once in `Application_Start`; `Services/ExcelExportService.Export<T>(IEnumerable<T>, columns)` + `ExcelImportService.Read(file, map)` |
| Reference-data dropdowns (no caching) | `new SelectList(db.CURRENCIES…)` 40× in 8 files; CUSTOMERS 20× in 5; UNITS 13× in 2; FORWARDERS 8× in 4; WAREHOUSES 8×; 608 `new SelectList(` total | `ReferenceDataCache` (MemoryCache 10 min, invalidated on Create/Edit/Delete of those tables) + `SelectListFactory` |
| Exception → JSON | 30× `Json(new { error = ex.Message })` / `success=false, message=ex.Message` | `BaseController.JsonError(Exception)` that logs and hides details |
| Layout header/user block + script list | `_Layout.cshtml:877-1000` vs `_AdminPanelLayout.cshtml:28-110`; script lists `_Layout:1743-1783` vs `_AdminPanelLayout:160-195` (same files, different order) | `_HeaderUser.cshtml` partial + shared `_Scripts.cshtml` / bundles |
| Bundle config | `App_Start/BundleConfig.cs` = `App_Start/BundleConfig2.cs` (verbatim) | delete BundleConfig2 |

## Dead code

| Item | Confidence |
|---|---|
| `App_Start/BundleConfig2.cs` (not in csproj, never called) | High |
| `Controllers/AdminSetting/Helper.cs`: all 6 methods have 0 usages (MD5/SHA1 password helpers, `System.Random` password generator) | High |
| `Models/UsersRoleProvider.cs` + `<roleManager>` in Web.config (0 `IsInRole`/`Roles.` usages) | High |
| `Models/DataTableModel.cs`: check usage before deleting (no usage seen in core) | Med |
| `AccountController.SendARP`/`inet_addr` P/Invoke (lines 21-25), `Index()` (no view use), `SetDeviceId` (stub) | High / Med |
| `NetworkHelper.GetMacAddressLegacy` (`[Obsolete]`), `GetMacAddress` (constant), `GetIpAddress` (server IP, only used by UserActionLogger, wrongly), `IsPrivateNetworkIp`/`IsInRange` (unused) | High |
| `HomeController`: `GetFinancePlanVsFact`, `GetFinanceFactMonthly`, `GetFinanceCashSummary`, `GetSRMMaterialRequirements`, `GetCRMSalesOrdersStats`, `GetCRMCustomersStats`, `GetMESProductionStats`, `GetHRStats`, `GetTasksStats`, `GetDocumentsStats` (10 endpoints, about 450 lines, none referenced by any view or JS; they also contain N+1 and full-table loads), `CookieConfirm`, `About`, `Contact`, `Views/Home/About`, `Contact`, `StatusView` | High (grep for routes in JS/cshtml found only `GetExecutiveDashboard`) |
| `CookieHelper.cs` + COOKIES_CONFIRMATION flow | High |
| `AdminPanelController`: `Users`, `Roles`, `Permissions`, `SavePermissions`, `Logs`, `UserEntries` (no views) | High |
| `PermissionController` + `Views/Permission/Edit.cshtml` | High |
| `ReportsController.Index` (no view) and unused usings (`System.Data.SqlClient`, `System.Configuration`) | High |
| `AdminDashboardViewModel.TotalLogs` / `TotalPermissions` (computed, not displayed) | High |
| `LicenseHelper.GenerateKey/GenerateLegacyV1Key/GenerateKeyPair/Sign` inside the web DLL: used only by LicenseGen and Tests. Consider moving them to a shared signing library so the web app ships verify-only code. | Med |
| LivIcons JS (3 files, 261 KB) and `dark-layout.css` loaded by both layouts | High |
| `ViewBag.Parametr` from `AppSettings["Parametr"]`: the key does not exist in Web.config, so the list is always empty | High |
| `DBTHSNModel.Context.HR.cs` / `.partial.cs`: comments say "temporary workaround until EDMX updated". Verify whether the EDMX now generates these DbSets; if it does, this is a duplicate-definition risk. | Low |

## Suggested split of god-controllers

None of the controllers in scope exceeds 1000 lines, but three are oversized.

- **ReportsController (916)** → keep it as a thin controller (about 120 lines) and add `Services/SupplyPlanning/DemandStatisticsService` (expense projection, daily series, ABC/XYZ, safety stock; shared with HomeController's executive KPI) and `CoverageService` (BOM explosion, plan/fact attribution, unit-factor precompute). Give it its own permission module "SupplyReports" instead of borrowing "Supplier".
- **DeletedRecordsController (829)** → `DeletedRecordsController` (about 80 lines) plus `Services/DeletedRecordsRegistry` (data-driven list of 54 entries: DbSet accessor, search fields, columns, and a `CanRecover` flag that is false for stock and finance documents).
- **HomeController (754)** → delete the 10 dead endpoints and the cookie code (about 480 lines). Move `GetExecutiveDashboard` to `Services/ExecutiveDashboardService` (SQL aggregates, 60 s cache). The controller drops to about 40 lines.
- **PModuleController (677) / RoleController (564) / AdminPanelController (270) / PermissionController** → one `PermissionService` (seed defaults, snapshot, cache version) plus RoleController and PModuleController as thin CRUD. Delete PermissionController and the dead AdminPanel actions.
- **_Layout.cshtml (2039 lines)** → `_Layout` (about 150 lines) + `_HeaderUser` partial + `_MainMenu` partial (driven by a `MenuDefinition` list in code, rendered per role set with output cache) + external `ipack.css` and `layout.js`.

## Cross-module notes

1. **Permission enforcement is the most important cross-cutting gap.** 20 business controllers have no checks (CORE-S02); every other module agent should flag its controllers. Recommended design:

   ```csharp
   public sealed class PermissionAttribute : Attribute { public string Module; public bool Change; }
   public sealed class PermissionFilter : IAuthorizationFilter { /* resolve module: attribute ?? "{controller}/{action}" ; GET→View, else Change ; uses PermissionService.Snapshot(user) */ }
   ```

   Then delete the per-action `if (!RoleHelper...) return RedirectToAction(...)` blocks module by module.

2. **BaseController.** Add `CurrentUserName`, `CurrentUserId` (cached), `Db` (one context per request, disposed), `JsonError(ex)`, `Forbidden()`, `CanChange(module)` for views, and `ReferenceData` (cached SelectLists). Many modules re-implement these.
3. **Logging.** Replace `LogHelper.LogToDatabase` (282 calls) with an async queued `IAuditLogger`. The signature stays the same, so a find-replace is enough. Remove the DNS lookup per call immediately (static cache), which is a one-line quick win.
4. **Reference-data cache.** CURRENCIES, UNITS, WAREHOUSES, CUSTOMERS, FORWARDERS, PRODUCT_CATEGORIES and ROLES dropdowns are queried on every Create/Edit GET and on every validation-failure re-render. A shared `ReferenceDataCache` with invalidation on CUD saves 2–6 queries per form view across all modules.
5. **Soft-delete policy (CORE-B01).** Each module agent should stop calling `SoftDeleteReferenceService.After*` for historical references. Decide on one global policy.
6. **EPPlus setup.** Set `ExcelPackage.LicenseContext` once in `Application_Start`, and build a shared Excel export/import service (12 files).
7. **TempData/Session (CORE-P05).** It affects every module's AJAX responsiveness.
8. **Front-end.** Views must stop including their own jQuery (9 views). Module agents should check view-local `<style>` blocks (51 views) and duplicated JS.
9. **Indexes.** The repo only defines 7 indexes (RECONCILIATION_ADJUSTMENTS, USER_LICENSES, PRODUCTPLAN.SalesOrderID, WAREHOUSE_LOCATIONS, INCOME_LOTS, LOT_LOCATIONS). The base schema is not in the repo, so verify with `sys.indexes` on the server. SQL Server does **not** index FKs automatically. Proposed indexes for the hot core paths:

   ```sql
   -- auth / permissions / layout (every request)
   CREATE UNIQUE INDEX UX_USERS_Uname ON dbo.USERS(Uname) INCLUDE (ID, IsActive, IsDeleted, FullName);
   CREATE INDEX IX_USERROLES_RoleID ON dbo.USERROLES(RoleID, UserID);
   CREATE UNIQUE INDEX UX_PERMISSIONS_Role_Module ON dbo.PERMISSIONS(RoleID, PermissionModuleID) INCLUDE (ViewPermit, ChangePermit);
   CREATE INDEX IX_PERMISSIONMODULES_Module ON dbo.PERMISSIONMODULES(Module);
   CREATE INDEX IX_PERMISSIONMODULES_CtrlAction ON dbo.PERMISSIONMODULES(Controller, Action, Parameter);
   CREATE INDEX IX_APP_TASKS_Assignee ON dbo.APP_TASKS(AssigneeUserID, IsDeleted) INCLUDE (StatusID, DueDate);
   CREATE INDEX IX_WAREHOUSES_MRP ON dbo.WAREHOUSES(MRP) WHERE IsDeleted = 0 OR IsDeleted IS NULL;  -- adjust to column nullability
   CREATE INDEX IX_INCOMES_RecvWh_Status ON dbo.INCOMES(ReceivingWHID, RecieveStatus) INCLUDE (IsDeleted);
   CREATE INDEX IX_USERIMAGES_UserID ON dbo.USERIMAGES(UserID);
   -- audit
   CREATE INDEX IX_USERLOGS_DateTime ON dbo.USERLOGS([DateTime] DESC) INCLUDE (UserID, ControllerName, ActionName);
   CREATE INDEX IX_USERLOGS_User_Date ON dbo.USERLOGS(UserID, [DateTime] DESC);
   CREATE INDEX IX_USER_ENTRIES_DateTime ON dbo.USER_ENTRIES([DateTime] DESC) INCLUDE (UserID);
   CREATE INDEX IX_USER_ENTRIES_User_Date ON dbo.USER_ENTRIES(UserID, [DateTime] DESC);
   -- license
   CREATE INDEX IX_COMPANY_LICENSES_Company_Active ON dbo.COMPANY_LICENSES(CompanyID, IsActive, ID DESC);
   -- reports / dashboards
   CREATE INDEX IX_EXPENSES_SenderWh_Date ON dbo.EXPENSES(SenderWHID, IssueDateTime) INCLUDE (IsDeleted);
   CREATE INDEX IX_EXPENSE_PRODUCTS_ExpenseID ON dbo.EXPENSE_PRODUCTS(ExpenseID) INCLUDE (ProductID, Amount);
   CREATE INDEX IX_PRODUCT_STOCKS_Wh_Product ON dbo.PRODUCT_STOCKS(WHID, ProductID) INCLUDE (Amount, UnitID);
   CREATE INDEX IX_PRODUCTPLANS_DAILY_Day ON dbo.PRODUCTPLANS_DAILY([Day]) INCLUDE (PlanID, Total);
   CREATE INDEX IX_QI_ProductionID ON dbo.QUALITY_INSPECTION(ProductionID) INCLUDE (ProductPlanDailyID, QtyInspected, IsDeleted);
   CREATE INDEX IX_QI_PlanDaily ON dbo.QUALITY_INSPECTION(ProductPlanDailyID) INCLUDE (ProductionID, IsDeleted);
   CREATE INDEX IX_PRODUCT_PRODUCTION_Date ON dbo.PRODUCT_PRODUCTION([Date]) INCLUDE (ProductID, Total, IsDeleted);
   CREATE INDEX IX_FIN_TRANSACTIONS_Account_Date ON dbo.FIN_TRANSACTIONS(AccountID, TxnDate) INCLUDE (Direction, Amount, SourceDocType, IsDeleted, CategoryID);
   CREATE INDEX IX_S_ORDERS_IssuedDate ON dbo.S_ORDERS(IssuedDate) INCLUDE (Amount, ContractID, IsDeleted);
   CREATE INDEX IX_S_CONTRACTS_IssuedDate ON dbo.S_CONTRACTS(IssuedDate) INCLUDE (Amount, IsDeleted);
   CREATE INDEX IX_SALES_INVOICES_InvoiceDate ON dbo.SALES_INVOICES(InvoiceDate) INCLUDE (TotalAmount, OrderID);
   ```

   Every module agent should add the FK indexes for its own `*_PRODUCTS` / `*_LINES` child tables (HeaderID and ProductID).

## Recommended order of work

1. **Security, same day (S effort):** CORE-S01 (Account), CORE-S03 (Developer guard), CORE-S08 (`ClearUserCache` on user edit), and add a permission check to LogController (CORE-P09). Delete PermissionController and the dead AdminPanel actions (B04, B05).
2. **Quick performance wins (S):**
   - P06: delete the Home Index dead queries.
   - P01: cache UserId and move the layout badges to AJAX or a cached child action.
   - P08: User Details logs.
   - P19, P17, P18.
   - P10: static cache of server IPs (removes the DNS lookup per log).
   - P11: AdminPanel counts.
   - P04: web.config clientCache and compression.
3. **Front-end diet (M):** P03. Remove 2 jQuery copies, Bootstrap 5 JS, LivIcons and dark CSS. Lazy-load the PDF and Excel libraries. Externalize the inline CSS/JS. Expected result: about 3.5 MB → about 1 MB of JS per first load, and far fewer 304s.
4. **Permission architecture (M):** P02 per-user snapshot plus version stamp, then S02 global `PermissionFilter`, then strip the per-action boilerplate gradually.
5. **Data-integrity fixes (M):** B01 soft-delete FK nulling policy and B02 Recover restrictions. Coordinate with module owners.
6. **Logging and session (M):** P10 async audit logger (merge LogHelper and UserActionLogger), P05 TempData cookie provider or read-only session.
7. **Reports and dashboard (M):** P07, P13 and P14 via the shared `DemandStatisticsService`; fix the trend and last-day bugs.
8. **Cleanup (S–M):** dead-code list, DeletedRecords registry refactor, BundleConfig2, Helper.cs, UsersRoleProvider, indexes script.
