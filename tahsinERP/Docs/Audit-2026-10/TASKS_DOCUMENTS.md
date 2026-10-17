# Module audit: TASKS_DOCUMENTS

Root: `C:\Work\SCHAIN\tahsinERP\tahsinERP\tahsinERP` (all paths below are relative to it)

## Scope

I read 77 files (about 18,100 lines). All controllers and services were read in full. Views were read in full, or diffed line by line against their near-identical siblings.

**Controllers (12):** TasksController.cs (565), TaskDashboardController.cs (246), TaskKanbanController.cs (201), TaskChangeStatusController.cs (168), TaskSettings/TaskPriorityController.cs (203), TaskSettings/TaskStatusController.cs (203), TaskSettings/TaskProjectController.cs (247), OfficialLettersController.cs (910), STDCompanyStandardsController.cs (880), CompanyOrdersController.cs (651), HelpDocsController.cs (30), AdminSetting/UserManualController.cs (57)

**Services (9):** DocumentTemplateService, IDocumentTemplateService, HtmlToDocxConverter, WordDocumentConverterService, WordDocumentImageService, OfficialLetterDataService, TextSuggestionService, ITextSuggestionService, UserManualProvider (1398)

**Views (41):**
- Tasks/{Index, Create, Edit, Details}
- TaskDashboard/Index (747)
- TaskKanban/Index (485)
- TaskChangeStatus/ChangeStatus
- TaskPriority/*, TaskStatus/*, TaskProject/* (5 each)
- OfficialLetters/{Index, Create, Edit, Details, RegisterIncoming}
- STDCompanyStandards/{Index, Create, Edit, Details, Delete}
- CompanyOrders/{Index, Create, Edit, Details}
- Manual/Index (2875)
- UserManual/Index

**Models (11):** APP_TASKS, APP_TASK_PRIORITY_REF, APP_TASK_STATUS_REF, APP_TASK_PROJECTS, DOC_DOCUMENTS, DOC_FILES, DOC_TEMPLATES, COM_OFFICIAL_LETTERS, STD_COMPANY_STANDARDS, ADM_COMPANY_ORDERS, SAMPLE_FILES

**Supporting files read:** UserHelper.cs (GetUserId / GetFullName), SoftDeleteReferenceService (the methods this module uses), STDCompanyStandardsViewModel.cs, the `[AllowHtml]` view models, and the related menu lines in `_Layout.cshtml`.

---

## Findings

Severity counts: **Critical 2, High 17, Med 22, Low 11** (52 total).
By category: Perf 18, Bug 15, Sec 9, Dup 6, Dead 2, Maint 2.

### A. Blob (varbinary) handling: the biggest perf problem in DOCUMENTS

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| DOC-P01 | **Critical** | Perf | OfficialLettersController.cs:334-338, CompanyOrdersController.cs:320-325 | `Details` does `.Include(d => d.DOC_FILES)`. This loads `FileContent` for **every version** of the generated .docx plus the `_letterdata.json` row. The view (Details.cshtml:11) then uses only `IsMain` metadata. 10 generated versions × ~50-500 KB means MBs read from SQL on every Details open. | Remove the Include. Load only metadata: `db.DOC_FILES.Where(f=>f.DocumentID==id && f.IsDeleted!=true && f.FileExtension!=".json").Select(f=>new DocFileListItem{ID,VersionNo,FileName,CreatedDT,IsMain}).ToList()`. | S | Low |
| DOC-P02 | **Critical** | Perf | DOC_FILES / DOC_TEMPLATES / SAMPLE_FILES model design | `FileContent` (varbinary(max)) sits on the same EF entity as the metadata, so **any** materialisation of `DOC_FILES` pulls the blob. This affects every `FindAsync`, `FirstOrDefault`, `ToListAsync` and `Include` in this module and in JobDesc. | (1) Short term: always project metadata and never materialise DOC_FILES entities except in Download. (2) Medium term: use EF6 table splitting with a `DOC_FILES` entity (metadata) plus a `DOC_FILE_CONTENT` entity (same PK, `FileContent` only, 1:1 required), or move the content to a separate table. (3) Long term: move to FILESTREAM, disk or S3 and keep only a path in the DB (fits the AWS plan). | M/L | Med |
| DOC-P03 | High | Perf | STDCompanyStandardsController.cs:590-593 | `Details` loads all `.docx` rows **with blobs** just to build the version-history list (only number, date, name and ID are used). | Project into `VersionHistoryItem` inside SQL, the same way the images query at 605-623 already does. | S | Low |
| DOC-P04 | High | Perf | STDCompanyStandardsController.cs:384-397 | `Edit` GET loads the latest .docx blob into `ExistingDocumentContent`. The view never reads it. | Delete the query and the VM property. Set `ViewBag.DocumentFileId = latestFileId` (projection of ID only). This is also the fix for DOC-B03. | S | Low |
| DOC-P05 | High | Perf | OfficialLettersController.cs:471-485, CompanyOrdersController.cs:461-475 | `GenerateDoc` loads the newest DOC_FILES row (full blob) only to read `VersionNo`, then loads all `IsMain` rows (blobs) only to flip a bool. | Get the version with `db.DOC_FILES.Where(..).Max(f=>(int?)f.VersionNo)`. Flip the flag with `ExecuteSqlCommand("UPDATE DOC_FILES SET IsMain=0 WHERE DocumentID=@p0 AND IsMain=1", id)`. | S | Low |
| DOC-P06 | High | Perf | OfficialLettersController.cs:367-374, CompanyOrdersController.cs:348-355, SoftDeleteReferenceService.cs:313 | `Delete` Includes DOC_FILES (blobs), and then `AfterDocDocumentSoftDeletedAsync` re-queries every file with `ToListAsync()` (blobs again) only to set `IsDeleted`. | Use a single `UPDATE DOC_FILES SET IsDeleted=1 WHERE DocumentID=@id` and remove the Include. | S | Low |
| DOC-P07 | Med | Perf | CompanyOrdersController.cs:393, DocumentTemplateService.cs:24 | `GenerateDoc` does `.Include(d => d.DOC_TEMPLATES)` (loads the template blob, which is then unused). It then queries DOC_TEMPLATES again to find the template, and DocumentTemplateService loads it a **third** time by ID in a new context. | Remove the Include. Select only `template.ID`. In DocumentTemplateService, cache the template bytes in `MemoryCache` keyed by `templateId` (invalidate on template upload). | S | Low |
| DOC-P08 | Med | Perf | STDCompanyStandardsController.cs:312-334, 519-540, WordDocumentImageService | Every Create and Edit extracts **all** images from the .docx and stores them again as separate DOC_FILES rows. Each new version duplicates every image (the bytes are already inside the docx), so storage grows by about version count × image bytes. The Details images query (605-623) shows the images of **all** versions, and each one is a separate full-blob request. | Option 1: drop image extraction and render images from the current docx on demand. Option 2: dedupe by SHA-256 hash and filter Details to `VersionNo == current`. At minimum, add `Response.Cache.SetCacheability(Public)` and an ETag of the fileId to `GetImage`/`DownloadFile` (DOC_FILES rows are immutable). | M | Med |
| DOC-P09 | Med | Perf | HtmlToDocxConverter.cs:226-259 | `CreateImageRunFromBase64` adds an `ImagePart` to the package and then returns null, so the image never appears in the document. The docx still carries every image as an orphan part, which bloats it, and DOC-P08 then extracts those parts again. | Either implement the Drawing element (see the OpenXML SDK "insert picture" sample) or stop calling `AddImagePart`. | M | Low |
| DOC-P10 | Low | Perf | OfficialLetterDataService.cs:28-90 | Letter body, greeting and items are stored as a JSON **varbinary** row in DOC_FILES (schema workaround). That costs an extra blob read on Edit, Details and GenerateDoc, and pollutes the file list and versioning. | Add `BodyHtml NVARCHAR(MAX)`, `GreetingLine` and an items child table, or `DataJson NVARCHAR(MAX)`, to COM_OFFICIAL_LETTERS. Then delete this service's file-based storage. | M | Med |

### B. TASKS: performance

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| TSK-P01 | High | Perf | UserHelper.cs:50-70, called at TasksController.cs:140,142,232,234; TaskKanbanController.cs:80,107,140; TaskProjectController.cs:90,92 | `UserHelper.GetFullName(userID:)` opens a **new DbContext and runs one query per call**, with no cache. Tasks Index costs 25 rows × 2 = 50 extra round trips. Kanban costs 1 per task for **all** tasks (hundreds). Project Details costs 2 per task. | Fetch names once per request: `var names = db.USERS.Where(u=>ids.Contains(u.ID)).Select(u=>new{u.ID,Name=u.FullName??u.Uname}).ToDictionary(...)`. Alternatively, add a cached `UserHelper.GetFullNameMap()` (MemoryCache, 5 min). | S | Low |
| TSK-P02 | High | Perf | TaskKanbanController.cs:32-44 | Kanban loads **every** non-deleted task ever created, including DONE and CANCELLED from previous years, with three Includes and full `Description`. The board grows without limit. | Default filter: exclude final statuses older than N days (or add a `?showClosed` toggle). Project into `KanbanTaskViewModel` in SQL, truncating Description to `Substring(0,100)` in SQL. Add `AsNoTracking()`. | S | Low |
| TSK-P03 | High | Perf | TaskDashboardController.cs:42-53 | The dashboard materialises **all** tasks (full entities plus includes) and then does every count and group in memory. | Push to SQL: `GroupBy(t=>new{t.StatusID}).Select(g=>new{g.Key,Count=g.Count()})`, conditional counts with `Sum(t=> cond ? 1:0)`, overdue by project in SQL. One query or a SQL view `vw_TaskStats`. | M | Low |
| TSK-P04 | High | Perf | TaskDashboardController.cs:129 | `t.APP_TASK_PROJECTS?.Name` is not Included, so lazy loading fires (N+1 per distinct project). | Include it, or do it in SQL (TSK-P03). | S | Low |
| TSK-P05 | High | Perf | TaskDashboardController.cs:150-197 (line 185) | `await db.USERS.FirstOrDefaultAsync(u => u.ID == assigneeId)` runs **inside the loop**, so N assignees means N queries, each loading the full USER entity. | Load one dictionary before the loop (same as TSK-P01). | S | Low |
| TSK-P06 | Med | Perf | TasksController.cs:151, 274, 370, 429, 520; TaskDashboardController.cs:224; CompanyOrdersController.cs:112, 186, 237, 296 | Dropdowns load **full USER entities** (Password, HashCode, Email and so on, all tracked) just for ID and FullName. CompanyOrders `vm.Employees` is **never rendered** by any view. | Project `Select(u=>new{u.ID,u.FullName})` and use `AsNoTracking`. Delete `vm.Employees` from CompanyOrders entirely. Cache the active-user list (MemoryCache, invalidate on user change). | S | Low |
| TSK-P07 | Med | Perf | TasksController.cs:152-153, 272-275, 368-371, 427-430, 518-521; TaskChangeStatusController.cs:64-66, 160-162 | Status, Priority and Project reference lists are re-queried on every GET and every failed POST. | Write one `PopulateLookups(db, vm)` method and cache the ref data (it changes rarely). | S | Low |
| TSK-P08 | Med | Perf | TasksController.cs:322-334 | KeyCode generation loads **every** KeyCode of the prefix into memory and regex-parses each one. | Add `APP_TASK_PROJECTS.NextSeq` (or a SQL SEQUENCE per project). Increment it with `UPDATE ... OUTPUT inserted.NextSeq` in the same transaction. This also fixes TSK-B05. | M | Med |
| TSK-P09 | Med | Perf | Views/Tasks/Index.cshtml:254-259 | The view re-loads DataTables Buttons **2.4.1**, jszip and pdfmake (about 1 MB) from CDN, but `_Layout.cshtml:1759-1765` already loads Buttons **2.4.2**. The double load also overwrites the plugin with an older version. | Delete lines 254-259. | S | Low |
| TSK-P10 | Low | Perf | Views/Tasks/Index.cshtml:263-346 | Client-side DataTables runs over a server-paged list of 25 rows. Search, sort and the Excel/PDF export apply only to the current page, which misleads users ("export" exports 25 rows). | Either go fully server-side (DataTables `serverSide:true` with a JSON endpoint) or disable DataTables paging and search and keep the server pager. | M | Low |
| TSK-P11 | Low | Perf | All Task read actions | No `AsNoTracking()` on read-only lists (Index, Details, Kanban, Dashboard, Project Details). | Add `AsNoTracking()`. | S | Low |

### C. TASKS: bugs and security

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| TSK-B01 | **High** | Bug | TaskChangeStatusController.cs:114-142 | **User comments are silently lost.** The code `UPDATE`s `UserComment` with raw SQL, then sets `db.Entry(task).State = Modified` and calls `SaveChanges()`. `UserComment` **is mapped** in the EDMX (APP_TASKS.cs:27), so EF writes back the old value it loaded through `FindAsync`, overwriting the new comment. The comment in TasksController.cs:210 ("not in the EF model") is stale. | Delete both raw-SQL blocks and use `task.UserComment = newComment;` directly. Remove `db.Entry(task).State = Modified`; the entity is already tracked. Do the same in TasksController.cs:210-219 (use `task.UserComment`). | S | Low |
| TSK-B02 | High | Bug | Views/TaskPriority/Index.cshtml:91, Views/TaskStatus/Index.cshtml:102, Views/TaskProject/Index.cshtml (Delete link) | The Delete buttons are **GET links**, but each controller has only `[HttpPost] Delete`, so the link returns 404 and nothing can be deleted. The `Delete.cshtml` confirm views exist but have no GET action. | Replace each link with a POST form plus `@Html.AntiForgeryToken()` (as Tasks/Index does), or add a GET `Delete(id)` action that renders the existing Delete.cshtml. | S | Low |
| TSK-B03 | Med | Sec | TaskKanbanController.cs:158-199 | `UpdateStatus` has **no `RoleHelper` permission check**; only an ownership check. Any logged-in user can move their own tasks even without Tasks change permission. `newStatusId` is not validated (it could be inactive or deleted, or an FK error that leaks `ex.Message`). | Add `IsChangePermitted("Tasks","Edit")` and validate `newStatusId` against active, non-deleted statuses. | S | Low |
| TSK-B04 | Med | Bug | TasksController.cs:152-153, 272-275; TaskDashboard/Kanban lookups | Dropdowns filter only `IsActive`, never `IsDeleted`. Soft-deleted statuses, priorities and projects still appear (priority has **no** filter at all). TaskProjectController.Index:25 also lists deleted projects. | Add `&& (x.IsDeleted == false \|\| x.IsDeleted == null)` everywhere. Better: add a central `TaskLookups` cached provider. | S | Low |
| TSK-B05 | Med | Bug | TasksController.cs:318-345 | KeyCode generation is "read max, then insert" with no lock or transaction, so two concurrent creates produce the same `PROJ-12`. No unique index is visible. | Use a sequence or counter table (TSK-P08) plus a unique index on `KeyCode`. | M | Med |
| TSK-B06 | Med | Bug | TasksController.cs:545-555 | `Delete` sets the status to CANCELLED instead of `IsDeleted=true` whenever a CANCELLED status exists, so a "deleted" task stays visible everywhere, and Kanban and Dashboard count it. | Decide the semantics: either rename the action to Cancel, or always set `IsDeleted=true`. | S | Low |
| TSK-B07 | Med | Bug | Views/TaskDashboard/Index.cshtml:69 | `Html.Hidden("userId", ViewBag.CurrentUserID)` has no `@`, so it renders as literal text on the page for non-admin users. | Add `@`, or delete it (the select is disabled anyway). | S | Low |
| TSK-B08 | Low | Bug | Views/TaskDashboard/Index.cshtml:305, 394, 569, 634 | JS string literals are built as `'@x.Replace("'", "\\'")'`. Razor HTML-encodes the output, so `'` becomes `\&#39;` and names display wrongly. Values are also locale-formatted. | Use `@Html.Raw(Json.Encode(Model.StatusStatistics.Select(...)))` for each series. | S | Low |
| TSK-B09 | Low | Bug | Overdue logic: TasksController.cs:84-88; TaskDashboardController.cs:111-126, 155-168; Views/Tasks/Index.cshtml:182; Views/TaskKanban/Index.cshtml:109, 216; Views/TaskProject/Details.cshtml:301 | The rule "done = Code=='DONE' or Name contains 'bajarildi'" is applied inconsistently. The views check only `StatusName.ToLower() != "done"`, so completed tasks with an Uzbek status name show red as overdue. | Add `IsFinal BIT` to APP_TASK_STATUS_REF and use one helper `TaskRules.IsOverdue(task)`. | S | Low |
| TSK-B10 | Low | Bug | Views/Tasks/Index.cshtml:236 | Pagination links drop `showOverdue`, so page 2 of the "overdue" filter shows all tasks. | Add `showOverdue = ViewBag.ShowOverdue` to the links. | S | Low |
| TSK-B11 | Low | Sec | TaskPriority/Status/Project Edit POST (entity binding) | Binding the EF entity directly plus `State=Modified` allows over-posting (for example `IsDeleted`), and `IsDeleted` resets to null on every edit. | Bind a small VM, or `TryUpdateModel(entity, new[]{"Code","Name","SortOrder","IsActive"})`. | S | Low |
| TSK-D01 | Low | Dead | TasksController.cs:487-492 | The `doneStatus` query runs and its `if` body is empty, costing a wasted DB round trip per edit. | Delete it, or implement `CompletedDT`. | S | Low |

### D. DOCUMENTS: bugs and security

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| DOC-S01 | **High** | Sec | Views/OfficialLetters/Details.cshtml:159, 163; Views/CompanyOrders/Details.cshtml:134 | **Stored XSS.** The body is `[AllowHtml]` (OfficialLetterCreateVm.cs:46, CompanyOrderCreateVm.cs:28 and the Edit VMs) and rendered with `Html.Raw` with no server-side sanitising. Quill sanitises only in the browser, and a crafted POST bypasses it. `Html.Raw(Model.Title?.Replace(...))` also renders a plain-text title raw. | Sanitise on save with HtmlSanitizer (Ganss.Xss, whitelisting Quill tags). For the title use `@Html.Raw(Html.Encode(Model.Title).Replace("\n","<br/>"))`. | S | Low |
| DOC-S02 | **High** | Sec | OfficialLettersController.cs:567-584, CompanyOrdersController.cs:557-574, STDCompanyStandardsController.cs:729-812 (also JobDescController.cs:607) | **IDOR.** `DownloadFile(fileId)` and `GetImage` return **any** DOC_FILES row (any doc type: job descriptions, standards, orders, letters), checking only that the user has view permission on *this* controller. Soft-deleted files (`IsDeleted`) are also still served. | Write one `DocFilesController.Download(fileId)`. Join to DOC_DOCUMENTS, map `DocTypeID` to the owning controller, check `IsViewPermitted` for that controller, and require `f.IsDeleted != true && doc.IsDeleted != true`. | M | Low |
| DOC-S03 | High | Sec | STDCompanyStandardsController.cs:86-108 | `SaveDocument` (OnlyOffice callback stub) has **no permission check** and calls `WebClient.DownloadData(url)` on a **caller-supplied URL** (SSRF). It does nothing with the result. OnlyOffice is not used (no view references it). | Delete the action. | S | Low |
| DOC-S04 | Med | Sec | OfficialLettersController.cs:589, CompanyOrdersController.cs:579 | `SuggestText` has no permission check, returns `ex.Message`, and (with AI enabled) sends user data to Gemini with the API key in the **query string** (TextSuggestionService.cs:100, 149). | Add the permission check. Send the key in the `x-goog-api-key` header. Return a generic error. | S | Low |
| DOC-S05 | Med | Sec | STDCompanyStandardsController.cs:70 | `GetTemplate` has no permission check. Low impact, but inconsistent. | Add `IsViewPermitted`. | S | Low |
| DOC-B01 | **High** | Bug | STDCompanyStandardsController.cs:34-47 (Index), 579-582 (Details), 374-376 (Edit) | Soft delete sets **only `DOC_DOCUMENTS.IsDeleted`** (line 703). Index has **no IsDeleted filter at all**, and Details and Edit check only `STD_COMPANY_STANDARDS.IsDeleted`. Deleted standards stay listed and editable. | Filter `s.IsDeleted != true && s.DOC_DOCUMENTS.IsDeleted != true` everywhere, and set both flags on delete. | S | Low |
| DOC-B02 | **High** | Bug | Views/STDCompanyStandards/Edit.cshtml:405-409 | There is an **extra `});`** (4 closers versus 3 in Create.cshtml:376-379), so the whole `<script>` block throws a SyntaxError. Quill never initialises and the submit handler never attaches. Saving posts an empty `DocumentContent`, so metadata and version number change but **no new file** is created. | Delete line 408. Verify in the browser. | S | Low |
| DOC-B03 | High | Bug | Views/STDCompanyStandards/Edit.cshtml:236, 373 versus STDCompanyStandardsController.cs:360-401 | The view expects `ViewBag.DocumentFileId` (copied from JobDesc), but the STD controller never sets it. The editor opens **empty**, and once DOC-B02 is fixed, saving would create a new version containing only what the user typed: **data-loss risk**. | Set `ViewBag.DocumentFileId` to the latest main docx ID (projection) in Edit GET. | S | Low |
| DOC-B04 | High | Bug | OfficialLettersController.cs:836-862 | `RegisterIncoming` saves DOC_DOCUMENTS (line 837) **before** validating the attachment (PDF only, ≤10 MB). If validation fails, it returns the view and leaves an orphan document with no letter row (an invisible draft). | Validate the file first, then wrap all inserts in a transaction with a single SaveChanges (EF fixes up `DocumentID` through the navigation). | S | Low |
| DOC-B05 | Med | Bug | OfficialLettersController.cs:126-172, CompanyOrdersController.cs:144-170, STDCompanyStandardsController.cs:224-341 | Multi-table creates use 2-4 `SaveChanges` calls with no transaction. A failure midway leaves orphan DOC_DOCUMENTS. In STD, the image-extraction failure is swallowed. | Set `document.COM_OFFICIAL_LETTERS = letter` (navigation) and call one SaveChanges, or use `db.Database.BeginTransaction()`. | S | Low |
| DOC-B06 | Med | Bug | OfficialLettersController.cs:737-768, CompanyOrdersController.cs:620-648 | Number generators load **all** numbers into memory, have a race condition (duplicate numbers under concurrency), and count **incoming** letters (`DirectionCode=IN`, external numbers) towards our outgoing sequence. The `Substring(4)` heuristic turns e.g. "IN-12345" into a random max. Numbers are not per year. | Use a counter table `DOC_NUMBER_SEQ(DocType, Year, LastNo)` with `UPDLOCK` / `OUTPUT`, filtered to OUT letters. Generate at **POST** time, not on the GET form (the current form shows a number that may already be taken by save time). | M | Med |
| DOC-B07 | Med | Bug | STDCompanyStandardsController.cs:872-877 | `GenerateDocumentCode` uses the count of **all** DOC_DOCUMENTS of any type for the year, plus 1, so codes collide after deletes or concurrent creates and skip numbers across types. | Use the same counter-table service as DOC-B06. | S | Med |
| DOC-B08 | Med | Bug | OfficialLettersController.cs:516-564, CompanyOrdersController.cs:506-554 | `ChangeStatus` accepts any status from any state (no workflow guard, so a user can self-approve a CANCELLED doc). It does not check that the document belongs to this doc type, and it ignores `IsDeleted`. | Write a `DocumentWorkflowService` with an allowed-transition map plus a doc-type check. Use a separate permission for APPROVE. | M | Med |
| DOC-B09 | Med | Bug | TextSuggestionService.cs:22, 32, 54 | `new HttpClient()` per controller instance (OfficialLetters and CompanyOrders constructors run it on **every** request, even Index) causes socket exhaustion. `.Result` on an async method without `ConfigureAwait(false)` under the ASP.NET SynchronizationContext is a **classic deadlock** when `UseAITextSuggestion=true`. | Use a `static readonly HttpClient`, make `SuggestText` `async Task<JsonResult>` and await it, and create the service lazily. | S | Low |
| DOC-B10 | Med | Bug | WordDocumentConverterService.cs:191-201 (used by JobDesc) | `SanitizeText` manually turns `&`, `<`, `>` into `&amp;` etc. before `new Text(...)`. OpenXML escapes again, so Word shows literal `&amp;`. | Remove `SanitizeText`. | S | Low |
| DOC-B11 | Low | Bug | OfficialLettersController.cs:456, CompanyOrdersController.cs:424 | `DIRECTOR_NAME = "X.M. Maxmudov"` is hard-coded (and will be wrong for other tenants or licenses). | Read it from company settings / COMPANy. | S | Low |
| DOC-B12 | Low | Bug | OfficialLettersController.cs:80, CompanyOrdersController.cs:80 | The Years dropdown includes deleted letters and orders. | Add an `IsDeleted` filter. | S | Low |
| DOC-B13 | Low | Bug | STDCompanyStandardsController.cs:458-498 | `DocumentContent` is base64-decoded twice. If it is not base64, raw UTF-8 bytes are stored as a ".docx" (corrupt file). | Decode once, convert HTML to docx, and reject anything else. | S | Low |
| DOC-B14 | Low | Bug | STDCompanyStandardsController.cs:767, 808 | `Content-Disposition` is built with an unescaped `FileName` (quotes or non-ASCII break the header). | Use `File(bytes, mime, fileName)` or `ContentDisposition{FileName=..}.ToString()`. | S | Low |

### E. DOCUMENTS: performance (non-blob) and manual

| ID | Sev | Cat | Location | What and why | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| DOC-P11 | Med | Perf | OfficialLettersController.cs:37-75, CompanyOrdersController.cs:37-75 | Index materialises `{doc, letter, docType}` as full tracked entities, with **no paging**. It then reads `x.doc.DOC_DOCUMENT_STATUS_REF` and `x.doc.HR_ORG_UNITS` after `ToListAsync`, which lazy-loads per distinct key. | Project into `OfficialLetterIndexVm` inside the LINQ query (`StatusName = doc.DOC_DOCUMENT_STATUS_REF.Name`), use `AsNoTracking`, and add server-side paging or DataTables serverSide once volumes grow. | S | Low |
| DOC-P12 | Med | Perf | Views/OfficialLetters/Index.cshtml:127-139, Views/CompanyOrders/Index.cshtml:124-136 | Two `@Html.AntiForgeryToken()` calls per row (Delete and GenerateDoc forms), so each row serialises and encrypts two tokens. | Use one hidden token at the top of the page and JS-submitted forms, or a single form with `formaction`. | S | Low |
| MAN-P01 | High | Perf | UserManualProvider.cs:14-62, UserManualController.cs:30, 47 | The 1,398-line StringBuilder HTML for **13 chapters is rebuilt on every request**. The content is 100% static. The page embeds the whole manual **twice** (Index.cshtml:65 for the initial chapter and line 77 as the JSON of all chapters). `GetChapter` rebuilds the whole manual to return one chapter. | Cache with `private static readonly Lazy<UserManualPageViewModel> _manual = new Lazy<...>(Build);`. Better: move each chapter to a static `.html` or `.cshtml` partial under `~/Content/manual/` and serve it with `[OutputCache(Duration=86400, VaryByParam="none")]`. Load chapters lazily through `GetChapter` (which already exists) instead of the inline JSON. | S | Low |
| MAN-D01 | High | Dead | Views/Manual/Index.cshtml (2875 lines) + `assets/manual/` (style.css, bootstrap.min.css, popper.min.js, **images 27 MB**) | **Unreachable.** ManualController was removed in commit 912b1e0b and no route or controller serves this view. It was superseded by UserManual. It contains broken HTML (unbalanced `<ul>`/`<li>`, `<img src=".">`) and Cyrillic file names. It does not need to become static or cached; it should be deleted (or, if the screenshots are wanted, a few of them moved into UserManual chapters). | Delete the view, the `assets/manual/**` folder and the csproj entry (`tahsinERP.csproj:3106`). | S | Low |
| MAN-B01 | Low | Sec | UserManualProvider.cs:64-210 ("CRUD patterns" chapter) | It shows developer internals (code snippets, RoleHelper calls, architecture) to every end user. Duplicate `Order=3` values (CrudPatterns and Sales) make the chapter order unstable. | Move the developer chapter to internal docs and fix the Order values. | S | Low |

### F. Maintainability

| ID | Sev | Cat | Location | What | Fix | Effort | Risk |
|---|---|---|---|---|---|---|---|
| MNT-01 | Med | Maint | Status and priority colours in 7 view places (see Duplicates map #5) | Colours are decided by `Name.ToLower().Contains("muhim"/"bajarildi")`, which is fragile and duplicated. | Add `ColorHex` and `IsFinal` columns to APP_TASK_STATUS_REF and APP_TASK_PRIORITY_REF, plus one `@Html.TaskStatusBadge()` helper. | M | Low |
| MNT-02 | Low | Maint | TaskDashboard/Index.cshtml:279-744 | 15+ `console.log` debug calls and 470 lines of inline Razor-in-JS. | Serialise the VM once with `Json.Encode` and move the JS to `~/Scripts/app/task-dashboard.js` (bundled and cached). | M | Low |

---

## Duplicates map

1. **`isAdminOrDeveloper` role check.**
   - Locations: TasksController.cs:26-29, 184-187, 391-394, 447-450; TaskDashboardController.cs:23-26; TaskKanbanController.cs:23-26, 163-166; TaskChangeStatusController.cs:28-31, 83-86; TaskProjectController.cs:46-49; Views/Tasks/Index.cshtml:16-19; Views/Tasks/Edit.cshtml:9-12.
   - Shared home: `RoleHelper.IsAdminOrDeveloper(string username)`, using the cached roles RoleHelper already keeps.
2. **Task ownership guard** `!isAdmin && task.AssigneeUserID != currentUserId`.
   - Locations: TasksController.cs:204, 406, 464; TaskChangeStatusController.cs:48, 100; TaskKanbanController.cs:179.
   - Shared home: `TaskAccessService.CanAccess(APP_TASKS t, int userId, bool isAdmin)`.
3. **Task lookup dropdown filling** (Status, Priority, Assignee, Project).
   - Locations: TasksController.cs:151-153, 272-275, 368-371, 427-430, 518-521; TaskChangeStatusController.cs:64-66, 160-162.
   - Shared home: `private void PopulateTaskLookups(...)` backed by a cached `TaskLookupProvider`.
4. **APP_TASKS → view model mapping.**
   - Locations: TasksController.cs:129-148, 221-241; TaskProjectController.cs:79-98; TaskKanbanController.cs:69-83, 96-110, 129-145.
   - Shared home: one `static Expression<Func<APP_TASKS, TaskViewModel>> TaskProjection` (SQL-side) plus a user-name dictionary.
5. **Status and priority badge colour logic.**
   - Locations: Views/Tasks/Index.cshtml:128-176; Views/Tasks/Details.cshtml:54-102; Views/TaskProject/Details.cshtml:247-295; Views/TaskKanban/Index.cshtml:79-97, 141-163, 180-198; Views/TaskDashboard/Index.cshtml:319-336, 402-415.
   - Shared home: `Views/Shared/_TaskBadge.cshtml` or an `HtmlHelper` extension, with colour stored in the ref tables.
6. **Overdue / "done" predicate.** Locations are listed under TSK-B09. Shared home: `TaskRules.IsDone` / `IsOverdue`, backed by `APP_TASK_STATUS_REF.IsFinal`.
7. **TaskPriorityController and TaskStatusController.**
   - These are about 98% identical; only the variable names and the `AnyAsync()` guard differ. Their 5 views each are also near-identical (Index diff: only an IsActive column).
   - Shared home: `abstract RefDataController<TEntity>` with Code/Name/SortOrder/IsDeleted, or one shared view set with a model interface `IRefEntity`. TaskProject can join too.
8. **`ChangeStatus(id, statusCode)`.**
   - Locations: OfficialLettersController.cs:516-564 and CompanyOrdersController.cs:506-554 (identical apart from strings); also the status button blocks in Views/OfficialLetters/Details.cshtml:104-142 and Views/CompanyOrders/Details.cshtml:84-122.
   - Shared home: `DocumentWorkflowService.ChangeStatusAsync(db, docId, code, user)` plus partial `_DocStatusButtons.cshtml`.
9. **`DownloadFile(fileId)` / `GetImage`.**
   - Locations: OfficialLettersController.cs:567-584; CompanyOrdersController.cs:557-574; STDCompanyStandardsController.cs:729-771, 774-812; JobDescController.cs:607+.
   - Shared home: one `DocFilesController` with `Download` and `Image` actions (permission per doc type, `IsDeleted` checks, cache headers, `MimeMapping.GetMimeMapping`).
10. **New-main-version file save** (compute next VersionNo, clear IsMain, add DOC_FILES).
    - Locations: OfficialLettersController.cs:469-504; CompanyOrdersController.cs:459-494; STDCompanyStandardsController.cs:467-514 (also 290-307).
    - Shared home: `DocFileService.AddMainVersionAsync(db, docId, bytes, fileName, ext, mime, userId)` (SQL-only, no blob reads).
11. **Soft delete of a document plus its files.**
    - Locations: OfficialLettersController.cs:358-395; CompanyOrdersController.cs:339-376; STDCompanyStandardsController.cs:678-726.
    - Shared home: `DocumentService.SoftDeleteAsync(db, docId)` (set-based UPDATEs).
12. **Document numbering.**
    - Locations: OfficialLettersController.GenerateNextLetterNo:737-768; CompanyOrdersController.GenerateNextOrderNo:620-648; STDCompanyStandardsController.GenerateDocumentCode:872-877; TasksController KeyCode:318-337.
    - Shared home: `DocumentNumberService.Next(docType, year)` with a counter table and lock (shared with Sales/Purchase numbering, see cross-module notes).
13. **Create DOC_DOCUMENTS header plus subtype row** with separate SaveChanges.
    - Locations: OfficialLettersController.cs:137-167, 825-851; CompanyOrdersController.cs:144-170; STDCompanyStandardsController.cs:227-251.
    - Shared home: `DocumentService.CreateHeader(db, typeCode, code, title, user)` used inside one transaction.
14. **Static SelectLists.**
    - Directions and Channels: OfficialLettersController.cs:85, 106-107, 186-187, 242-243, 313-314.
    - OrderTypes: CompanyOrdersController.cs:85, 107, 181, 232, 291.
    - Shared home: `static readonly` lookup constants in a `DocLookups` class.
15. **Create/Edit views.**
    - OfficialLetters/Create vs Edit: 5-line diff (344 vs 345 lines).
    - CompanyOrders/Create vs Edit: 5-line diff.
    - STDCompanyStandards/Create vs Edit: about 85 diff lines, with a shared ~200-line Quill + mammoth script that is also copied in JobDesc/Create and Edit.
    - The Quill toolbar markup is repeated in 6+ views.
    - Shared home: `_OfficialLetterForm.cshtml`, `_CompanyOrderForm.cshtml`, `_QuillToolbar.cshtml`, `~/Scripts/app/docx-editor.js` (one `initDocxEditor({...})`).
16. **DataTables Excel/PDF button config with timestamp filename.** Found in **30 views** across the project (in this module: Tasks/Index, STDCompanyStandards/Index, TaskProject/Index, TaskPriority/Index, TaskStatus/Index, OfficialLetters/Index, CompanyOrders/Index). Shared home: `~/Scripts/app/erp-datatable.js` exposing `erpDataTable(selector, {fileBase, exportCols})`.
17. **HTML → DOCX conversion.**
    - Converters: `HtmlToDocxConverter.ConvertHtmlToDocx` (used by STD) and `WordDocumentConverterService.ConvertHtmlToDocx` (used by JobDesc).
    - `CreateEmptyDocx` copies: STDCompanyStandardsController.cs:169-192 and WordDocumentConverterService.cs:104-123.
    - `OfficialLetterDataService.HtmlToPlainText` is used by CompanyOrders (wrong owner).
    - Shared home: one `DocxConversionService` (HtmlToDocx with real images, DocxToHtml, Empty, HtmlToPlainText).
18. **Gemini call.** TextSuggestionService.cs:69-116 and 118-165 differ only in the prompt. Shared home: a private `CallGeminiAsync(prompt)`.
19. **MIME and extension maps.** STDCompanyStandardsController.cs:814-833 and WordDocumentImageService.cs:115-153; image-extension checks at STDCompanyStandardsController.cs:608-613, 758-763, 796-800. Shared home: `System.Web.MimeMapping.GetMimeMapping` plus `FileTypeHelper.IsImage(ext)`.
20. **STD module vs JobDesc module.** STDCompanyStandardsController follows JobDescController almost line for line (Create, Edit, Details, GetTemplate, DownloadFile, Quill/mammoth views); STD Edit.cshtml even keeps JobDesc's `ViewBag.DocumentFileId`. Shared home: a generic "versioned Word document" base (`VersionedDocController<TSubtype>`) or `VersionedDocumentService` plus shared views.

## Dead code

| Item | Location | Confidence |
|---|---|---|
| Manual view + 27 MB assets (no ManualController since 912b1e0b) | Views/Manual/Index.cshtml, assets/manual/**, tahsinERP.csproj:3106 | High |
| Delete confirm views with no GET action (and the index links 404) | Views/TaskPriority/Delete.cshtml, Views/TaskStatus/Delete.cshtml, Views/TaskProject/Delete.cshtml | High (either wire up a GET Delete or delete them) |
| `SaveDocument` (OnlyOffice stub, SSRF) | STDCompanyStandardsController.cs:86-108 | High |
| `UploadImage` (no caller in any view) | STDCompanyStandardsController.cs:111-167 | High |
| `ViewBag.HasTemplate` / `ViewBag.TemplatePath` (never read; leaks a server path) | STDCompanyStandardsController.cs:62-64, 216-218, 352-354 | High |
| `ExistingDocumentContent` VM property plus its blob query | STDCompanyStandardsViewModel / controller :384-397 | High |
| `CompanyOrder*Vm.Employees` plus 4 USERS queries | CompanyOrdersController.cs:112, 186, 237, 296 | High |
| `UserManualController.GetChapter` (no caller; keep it if lazy loading is adopted) | UserManualController.cs:36-55 | High |
| `HtmlToDocxConverter.CreateImageDrawing` and the image-part code that is never referenced | HtmlToDocxConverter.cs:226-282 | High |
| `WordDocumentConverterService.LoadTemplateDocx` | WordDocumentConverterService.cs:128-135 | High (no caller) |
| Empty `doneStatus` block | TasksController.cs:487-492 | High |
| Raw-SQL UserComment read/write (the column is mapped) | TasksController.cs:210-219, TaskChangeStatusController.cs:114-139 | High (replace) |
| `RunInfo.StartPos` (never read) | DocumentTemplateService.cs:256 | Medium |
| `_templateService` / `_textSuggestionService` built in the constructor even for Index/Details | OfficialLetters, CompanyOrders constructors | Medium (make them lazy) |

## Suggested split of god-controllers

No controller in this module is over 1,000 lines. OfficialLetters (910) and STDCompanyStandards (880) are close, and both are inflated by plumbing that is duplicated across controllers. Suggested extraction, which shrinks each controller by about 40%:

- **`Services/Documents/DocumentService`** gets header create, soft delete and ChangeStatus (workflow).
  - Moves from: OfficialLetters (Create header, Delete, ChangeStatus), CompanyOrders (same), STD (Create header, Delete).
- **`Services/Documents/DocFileService`** gets AddMainVersion, ListVersions (metadata projection) and GetContent.
  - Moves from: GenerateDoc version blocks and STD Edit/Create file saving.
- **`Controllers/DocFilesController`** gets `Download(fileId)` and `Image(fileId)`.
  - Moves from: the 4 Download/GetImage copies (+JobDesc).
- **`Services/Documents/DocumentNumberService`** gets `Next(type, year)`.
  - Moves from: GenerateNextLetterNo, GenerateNextOrderNo, GenerateDocumentCode.
- **`Services/Documents/DocxConversionService`** merges HtmlToDocxConverter, WordDocumentConverterService and HtmlToPlainText.
- **OfficialLettersController**: placeholder building (`BuildItemsTextBlock`, `AddItemPlaceholders`, `BuildLetterData`, `MapItems`, lines 622-735) moves to an `OfficialLetterDocBuilder`.
- **CompanyOrdersController**: placeholder and decree-line parsing (411-452) moves to a `CompanyOrderDocBuilder`.
- **TaskSettings**: TaskPriority and TaskStatus collapse into one generic `RefDataController<T>`.

## Cross-module notes (belongs to the shared/core layer)

1. **`UserHelper.GetFullName` / `GetUserId` / `GetUser`** (Controllers/AdminSetting/UserHelper.cs) each open a new DbContext and query per call with no cache. They are used project-wide inside loops (see TSK-P01). Add a MemoryCache-backed `UserDirectory` (ID↔Uname↔FullName map, 5-minute TTL, invalidated on user save). This likely removes thousands of queries across all modules.
2. **Blob columns**: DOC_FILES.FileContent, DOC_TEMPLATES.FileContent and SAMPLE_FILES.File (SAMPLE_FILES is used in CRM/CustomerController.cs:265, Engineering/ProductController.cs:408 and SRM/ProductStockController.cs:86). Apply a global rule: never materialise these entities in lists; use EF table splitting or a separate content table. SAMPLE_FILES (Excel import templates) should be static files under `~/Content/templates/` served by IIS with caching, not DB rows.
3. **Document numbering** (DOC-B06/B07, TSK-B05) shares a race-condition pattern that probably also exists in Sales/Purchase/Invoice numbering. Build one `DocumentNumberService` with a counter table plus `UPDLOCK, HOLDLOCK` (or SQL SEQUENCE objects).
4. **DataTables export JS** is copied in 30 views. Write one shared `erp-datatable.js` and stop per-view CDN loads of Buttons, jszip and pdfmake (the layout already loads Buttons 2.4.2).
5. **`RoleHelper.IsAdminOrDeveloper`** should live in RoleHelper (cached roles) instead of 13 inline copies.
6. **`[AllowHtml]` + `Html.Raw`**: add a project-wide `HtmlSanitizerService` and audit every `[AllowHtml]` property. JobDesc uses the same Quill pattern.
7. **JobDesc** (outside this scope) shares the STD document pipeline (WordDocumentConverterService, DownloadFile, the Quill/mammoth views). Fix STD and JobDesc together via `VersionedDocumentService`.
8. **TextSuggestionService / HttpClient**: any other `new HttpClient()` per request in the project should become a static or shared client.
9. **Soft-delete reference service** (`SoftDeleteReferenceService.After*`) loads whole rows with `ToListAsync` to null an FK. Replace it with set-based `ExecuteSqlCommand` UPDATEs across all modules (and avoid loading blobs, see DOC-P06).

## Recommended order of work (quick wins first)

1. **Same-day fixes (S, low risk):**
   - TSK-B01: lost user comments; delete the raw SQL.
   - DOC-B02: extra `});` in STD Edit.
   - DOC-B03: set `ViewBag.DocumentFileId`.
   - DOC-B01: IsDeleted filters on STD.
   - TSK-B02: Delete links in the 3 TaskSettings views.
   - TSK-B07: missing `@`.
   - TSK-P09: duplicate DataTables CDN scripts.
2. **Security quick wins:**
   - DOC-S03: delete SaveDocument.
   - DOC-S01: sanitise HTML and encode the Title.
   - DOC-S02: single DocFilesController with doc-type permission and IsDeleted checks.
   - TSK-B03: permission check on Kanban UpdateStatus.
   - DOC-S04: SuggestText permission and API key in a header.
3. **Blob perf quick wins:**
   - DOC-P01: remove `Include(DOC_FILES)` in Details.
   - DOC-P03: project version history.
   - DOC-P04: remove the unused blob in Edit.
   - DOC-P05: Max and UPDATE in GenerateDoc.
   - DOC-P06: set-based soft delete.
   - DOC-P07: template Include and caching.
4. **Task perf:**
   - TSK-P01: batch user names / cached UserDirectory.
   - TSK-P05: user query in the dashboard loop.
   - TSK-P04: Include projects.
   - TSK-P06: project USERS and drop Employees.
   - TSK-P02: Kanban filter and projection.
   - TSK-P11: AsNoTracking.
5. **Manual:**
   - MAN-D01: delete Views/Manual and the 27 MB assets.
   - MAN-P01: Lazy static cache of UserManualProvider, or static partials with OutputCache.
6. **Correctness with transactions:**
   - DOC-B04: RegisterIncoming validates before insert.
   - DOC-B05: single SaveChanges / transaction.
   - DOC-B09: static HttpClient, async SuggestText.
7. **Deduplication refactor:**
   - Write DocumentService, DocFileService, DocumentWorkflowService, DocumentNumberService (DOC-B06/B07/B08, TSK-B05/P08).
   - Partials for the Create/Edit forms and the Quill editor.
   - RefDataController for TaskPriority and TaskStatus.
   - TaskBadge helper with an `IsFinal` / `ColorHex` schema change (MNT-01, TSK-B09).
8. **Dashboard:** SQL-side aggregation (TSK-P03) and moving the JS to a static file (MNT-02).
9. **Longer term:**
   - DOC-P02: blob table splitting, then external storage (S3 for the AWS plan).
   - DOC-P08/P09: image dedupe or real image embedding.
   - DOC-P10: move the letter JSON from DOC_FILES into proper columns.
