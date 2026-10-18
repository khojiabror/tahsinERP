# Tasks + Documents — bajarilgan ishlar (2026-10-17)

Branch: `claude/opt-tasks-docs-9aa641`, 24+ commit (2-tur — pastda), `origin` (khojiabror/tahsinERP) ga push va master'ga merge qilingan.

**SQL:** barcha skriptlar 2026-10-17 da lokal `DBTHSN-IPACK` bazasida zaxiradan keyin ishga tushirildi — tafsilotlar oxirgi bo'limda.

**Tekshiruv:** MSBuild (Debug) xatosiz. Modul view'lari aspnet_compiler bilan alohida nusxada kompilyatsiya qilindi (xatosiz).
HTML→DOCX konverter namunaviy HTML bilan sinaldi: OpenXmlValidator 0 xato, rasm joylashgan.
**Brauzerda sinalmagan** (SQL va shablon generatsiyasi lokal bazada sinalgan) — ayniqsa Dashboard GROUP BY, Kanban filtri, STD Edit muharriri, xat/buyruq yaratish va raqamlash.

## Commitlar

| # | Mazmuni |
|---|---|
| 1 | DOC_FILES blob'lari faqat yuklab olishda (`Services/Documents/DocFileService`, `DocumentService.SoftDeleteAsync`), shablon keshi |
| 2 | TSK-B01 (izoh yo'qolishi), STD Edit JS/DocumentFileId, STD IsDeleted filtrlari, tranzaksiya, raqam |
| 3 | Tasks: ismlar/dropdownlar (`Services/Tasks/TaskLookups`), Dashboard SQL GROUP BY, Kanban proyeksiya, `TaskBadges` |
| 4 | `TaskRefDataController<T>` (Priority/Status/Project), GET→POST o'chirish, oq ro'yxat binding |
| 5 | Xat/buyruq: `[Permission]`, workflow (`ChangeStatusAsync`), `DocNumbering`, RegisterIncoming, async TextSuggestion |
| 6 | STD `[Permission]`, Content-Disposition RFC 5987, fayl keshi, qator formalari → data-post-url |
| 7 | Yagona HTML→DOCX konverter (run formatlash, sarlavha, ro'yxat, rasm), JobDesc facade |
| 8 | O'lik `Views/Manual` + `assets/manual` (194 fayl, ~28 MB), `UserManualProvider` Lazy kesh |
| 9 | Data-fix skriptlari, indeks taklifi, shu hisobot |
| 10 | Biznes qarorlari: COMPANIES.DirectorName, qo'llanmadan CRUD bo'limi, assets skrinshotlari |

## Tuzatilgan ID'lar

- **Critical:** DOC-P01, DOC-P02 (qisqa muddatli qism: hamma joyda proyeksiya; table splitting — taklif).
- **High:**
  - Hujjatlar: DOC-P03, P04, P05, P06, DOC-B01, B02, B03, B04, MAN-P01, MAN-D01.
  - Tasks: TSK-P01, P02, P03, P04, P05, TSK-B01, B02.
  - DOC-S01/S02/S03 core'da hal qilingan edi — qayta qilinmadi.
- **Med:**
  - Hujjatlar: DOC-P07, P09, P11, P12, DOC-S04, S05, DOC-B05, B06, B07, B08, B09, B10, MNT-01 (qisman).
  - Tasks: TSK-P06, P07, P08, TSK-B03, B04, B05, B07.
- **Low:** TSK-P09, P11, TSK-B08, B09, B10, B11, TSK-D01, DOC-B12, B13, B14, MAN-B01 (faqat tartib), MNT-02 (qisman: JSON + console.log).
- **Dublikatlar:**
  - isAdminOrDeveloper → `TaskRules`.
  - Badge'lar → `TaskBadges`.
  - TaskPriority/Status/Project → generic controller.
  - ChangeStatus, yangi fayl versiyasi, soft delete, raqamlash → `Services/Documents`.
  - Ikki HTML→DOCX konverter → bitta.
  - DownloadFile/GetImage (STD) → bitta.
  - Statik SelectList'lar va MIME lug'ati.
- **O'lik kod:** Manual, Delete.cshtml (3 ta), UploadImage, ViewBag.HasTemplate/TemplatePath, ExistingDocumentContent, CompanyOrders Employees, LoadTemplateDocx, SanitizeText.

## Xulq-atvor o'zgarishlari (foydalanuvchiga ko'rinadi)

- **Kanban:** 30 kundan eski yopilgan (DONE/CANCELLED/"bajarildi") topshiriqlar standart holatda yashirin. Ularni "Barcha yopilganlar" tugmasi ko'rsatadi.
- **Tasks Delete:** endi egalik tekshiriladi — oddiy foydalanuvchi faqat o'z topshirig'ini o'chira oladi.
- **Kanban drag-drop ruxsati:** Tasks/Edit o'zgartirish huquqi YOKI TaskChangeStatus ko'rish huquqi kerak (avval hech qanday tekshiruv yo'q edi).
- **Xat/buyruq raqami:**
  - Forma ko'rsatgan raqam saqlashda band bo'lsa, keyingi raqam beriladi va bu haqda xabar chiqadi.
  - Faqat chiquvchi (OUT) xatlar hisoblanadi.
  - O'chirilgan hujjatlarning raqamlari qayta berilmaydi.
- **Status o'tishlari:** DRAFT → FOR_APPROVAL/APPROVED/CANCELLED, FOR_APPROVAL → APPROVED/CANCELLED. APPROVED/CANCELLED — yakuniy holatlar. Bu Details sahifasidagi tugmalarga mos keladi.
- **STD Edit:** matn o'zgartirilmasa (va Word yuklanmasa) yangi fayl versiyasi yaratilmaydi, faqat metadata saqlanadi. Versiya raqami avvalgidek har tahrirda oshadi.
- **JobDesc (HRM) .docx:** endi formatlash va rasmlar saqlanadi. `&` endi `&amp;` bo'lib ko'rinmaydi.
- **Permission filtri:** OfficialLetters, CompanyOrders, STD, Tasks, TaskDashboard, TaskSettings `[Permission]` filtriga o'tdi. Action uchun PERMISSIONMODULES yozuvi bo'lmasa, controller darajasidagi ruxsat ishlatiladi (core dizayni).

## Qolganlar (qilinmagan)

- **Biznes qarorlari (2026-10-17, foydalanuvchi):**
  - **TSK-B06:** "Delete" topshiriqni o'chirmaydi, CANCELLED statusiga o'tkazadi — shunday qoladi (qasddan).
  - **DOC-B11:** direktor ismi `COMPANIES.DirectorName` ustunidan olinadi (`Services/Documents/CompanySettings`).
    Migratsiya `Database_Migrations/Alter_COMPANIES_AddDirectorName.sql` lokal bazada bajarilgan ("X.M. Maxmudov" yozilgan).
    Tahrirlash UI'si yo'q (Company sahifasi core'da) — hozircha SQL bilan o'rnatiladi.
  - **MAN-B01:** dasturchilar uchun "CRUD arxitekturasi" bo'limi qo'llanmadan olib tashlandi.
  - `assets/` ildizidagi 6 ta Cursor skrinshoti o'chirildi (csproj yozuvlari bilan).
- **Sxema (foydalanuvchi qarori, 2026-10-18):**
  - MNT-01/TSK-B09 — BAJARILDI (19-commit): `IsFinal`/`ColorHex` lokal bazada qo'shildi, EDMX yangilandi.
  - DOC-P10 — BAJARILDI (2026-10-19, foydalanuvchi ruxsati bilan, zaxiradan keyin): `COM_OFFICIAL_LETTERS.BodyHtml/GreetingLine`, `COM_OFFICIAL_LETTER_ITEMS`; eski JSON qatorlarini ilova o'zi ko'chiradi (`Database_Migrations/Alter_OfficialLetters_BodyItems.sql`).
  - DOC-P02 — BAJARILDI: EF6 table splitting — `DOC_FILES` (metadata) + `DOC_FILE_CONTENTS` (blob) bitta jadvalga; bazada o'zgarish yo'q. Blob faqat `DocFileService.ContentAsync` orqali o'qiladi.
- **Kod:**
  - JobDesc umumiy qismlarga ulandi (21-commit). To'liq `VersionedDocController` bazasi (#20) qilinmadi — STD va JobDesc endi umumiy servislar orqali ishlaydi, qolgan farq faqat o'z maydonlari.
  - TaskPriority/TaskStatus view'larini umumlashtirish (kichik, qilinmadi).
- **Core uchun:**
  - `SoftDeleteReferenceService.AfterDocDocumentSoftDeletedAsync` — core blob'siz set-based UPDATE'ga o'tkazdi.
  - `RoleHelper.IsAdminOrDeveloper` — core qo'shdi, modul nusxasi olib tashlandi (20-commit).
  - #16 DataTables eksport JS: Tasks hozircha `Scripts/srm/srm-server-table.js` dan foydalanadi (yangi nusxa yaratilmadi).

## 2-tur (2026-10-18): 13–19-commitlar

| # | Mazmuni |
|---|---|
| 13 | TSK-P10: Tasks ro'yxati DataTables server-side (`IndexData`), `ExportExcel` — filtr bo'yicha barcha qatorlar; `TaskQueries` proyeksiyasi (#4) |
| 14 | 6-commit'da tasodifan qo'shilgan `_program files_git` (aspnet_compiler artefakti) olib tashlandi |
| 15 | MNT-02: Dashboard grafiklari `Scripts/app/task-dashboard.js` ga |
| 16 | DOC-P08: STD rasmlari alohida saqlanmaydi, galereya joriy .docx'dan; versiya raqami teskariligi tuzatildi |
| 17 | #6 yagona "bajarilgan/yopilgan" qoidasi, #9 `DocFileResult`, #13 `DocumentService.NewHeader`, STD turi kod bo'yicha, `HtmlToPlainText` → `HtmlToDocxConverter` |
| 18 | #15: letters/orders/STD Create+Edit → `_Form`/`_FormScripts`; `Scripts/app/docx-editor.js`, `Views/DocTemplates/_QuillToolbar.cshtml` |
| 19 | MNT-01/TSK-B09: ref jadvallarda `IsFinal`, `ColorHex` (sxema + EDMX + sozlamalar formasi) |
| 20 | `TaskRules.IsAdminOrDeveloper` olib tashlandi — core `RoleHelper.IsAdminOrDeveloper` |
| 21 | JobDesc (HRM roziligi bilan): `docx-editor.js`, `_QuillToolbar`, `NewHeader`, `AddMainVersionAsync`, `DocFileResult`, tur/holat kod bo'yicha |
| 22 | Status/prioritet badge'lari faqat ID/Code/IsFinal/ColorHex bo'yicha — nom bo'yicha qidiruv qolmadi (db-foundation so'rovi) |
| 23 | Brauzer tekshiruvi tuzatishlari: inline skriptlar jQuery'dan keyin, Quill `clipboard.convert` (Word/shablon muharrirga tushmasdi) |
| 24 | DOC-P10 sxema skripti; DOC-P10 kodi va DOC-P02 table splitting (EDMX + kod) |

**Yakuniy holat (2026-10-19):** Bug 22/23 (TSK-B06 — foydalanuvchi qarori) · Xavfsizlik 8/8 · Perf 24/24 · Dublikat 19/20 (#16 — Core) · O'lik kod 2/2 · Maint 2/2.
Brauzerda tekshirildi: Tasks, Kanban, Dashboard, TaskSettings, xat/buyruq/STD/JobDesc ro'yxat, Details, Create, Edit, fayl/rasm yuklab olish.

Xulq-atvor o'zgarishlari:
- Tasks ro'yxatida qidiruv/saralash/eksport endi butun natijaga ishlaydi; "Muddati (gacha)" shu kunni ham oladi.
- Bekor qilingan topshiriq muddati o'tgan hisoblanmaydi (lokal bazada 24 → 23).
- STD Details galereyasi faqat joriy versiya rasmlari; Create validatsiyadan keyin matnni yo'qotmaydi.
- Xat/buyruqda rasm fayllar brauzerda ochiladi, qolganlari yuklab olinadi.

Tekshiruv: MSBuild xatosiz; modul view'lari aspnet_compiler bilan xatosiz; `TaskQueries`, status ID'lari, rasm o'qish,
shablon generatsiyasi lokal bazada sinaldi; `docx-editor.js`/`task-dashboard.js` node'da stub bilan sinaldi. **Brauzerda sinalmagan.**

## Data-fix skriptlari (lokal bazada bajarilgan)

- `Database_Migrations/DataFix_Docs_01_Find.sql` — faqat SELECT:
  - STD IsDeleted nomuvofiqligi;
  - o'chirilgan hujjatlarning faol fayllari;
  - yetim xat/buyruq sarlavhalari;
  - bir nechta IsMain;
  - takroriy raqamlar;
  - o'chirilgan ref'ga bog'langan topshiriqlar.
- `Database_Migrations/DataFix_Docs_02_Fix.sql` — 1–5-bo'limlarni tuzatadi; 5-bo'lim takroriy topshiriq kodlarini qayta raqamlaydi. Xat/buyruq raqamlariga tegmaydi.
  - Faylda oxirgi qator `ROLLBACK` — bu boshqa bazada qayta ishlatishda xavfsizlik uchun. Lokal bazada u COMMIT bilan bajarilgan.
- TSK-B01 tufayli yo'qolgan izohlarni tiklab bo'lmaydi — ular bazada qolmagan.

## 11-commit: Word shablonlar va generatsiya qayta ishlandi (foydalanuvchi so'rovi)

- **Shablonlar bazada** (`DOC_TEMPLATES`), papkada emas. Yangi sahifa: `DocTemplates` (har bo'limda "Word shablonlar" tugmasi):
  - yuklash (.docx tekshiruvi, tanilgan/tanilmagan maydonlar hisoboti);
  - namunaviy shablon (tayyor tuzilma + barcha maydonlar ro'yxati);
  - yuklab olish, arxivlash/faollashtirish, o'chirish (faqat ishlatilmagan bo'lsa);
  - STD'ning eski papka faylini bir bosishda bazaga ko'chirish.
- **Ruxsat:** hujjat turining o'z bo'limidan olinadi — ko'rish uchun bo'lim Index, boshqarish uchun bo'lim Create huquqi. Yangi PERMISSIONMODULES yozuvi kerak emas.
- **Generatsiya:** Details'da shablon tanlanadi, "Word hujjat yaratish" bosiladi, fayl avtomatik yuklab olinadi. Xato va ogohlantirishlar endi ko'rinadi (avval Details xabar ko'rsatmasdi).
  - Index'dagi tugma eng yangi faol shablonni ishlatadi.
  - Ishlatilgan shablon `DOC_DOCUMENTS.TemplateID` ga yoziladi.
- **Dvigatel** (`DocumentTemplateService.Fill`):
  - maydon Word tomonidan bir necha run'ga bo'linsa ham topiladi; qaysi run'dan boshlansa, o'sha format saqlanadi;
  - alohida qatordagi `{{LETTER_BODY}}`/`{{ORDER_BODY}}` formatlangan matn (qalin, ro'yxat, rasm) bilan, `{{ITEMS_TABLE}}` haqiqiy jadval bilan almashtiriladi;
  - katalogdagi, lekin bo'sh maydonlar tozalanadi (masalan, ortiqcha `{{ITEM_NAME_7}}`); noma'lumlari hujjatda qoladi va ogohlantirishda ko'rsatiladi;
  - eski nomlar (`{{Letter.No}}`, `{{Content}}`, `{ORDER NO}`...) ham ishlaydi;
  - header/footer ham to'ldiriladi.
- **Maydonlar katalogi:** `Services/Documents/DocPlaceholders` (yagona manba). Ma'lumot yig'ish `DocTemplateDataBuilder`'da (controllerlardan ko'chirildi).
- **Sinov:** namunaviy shablonga xat ma'lumoti qo'yildi — OpenXmlValidator 0 xato, jadval va ro'yxat joyida, bo'lingan run va alias to'ldirilgan. **Brauzerda va jonli bazada sinalmagan.**

## Lokal bazada bajarilgan SQL (2026-10-17, KHOJIABROR / DBTHSN-IPACK)

Foydalanuvchi ko'rsatmasi bo'yicha barcha skriptlar lokal bazada ishga tushirildi. Server (192.168.100.200) ishlatilmadi.

- **Zaxira (COPY_ONLY):** `C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup\DBTHSN-IPACK_before_tasks_docs_2026-10-17.bak` (47 MB).
- **`Alter_COMPANIES_AddDirectorName.sql`:** `COMPANIES.DirectorName` qo'shildi; "iPack Eco Solutions" MChJ = "X.M. Maxmudov".
- **`DataFix_Docs_01_Find.sql`:** quyidagilar topildi:
  - 1 ta yetim kiruvchi xat sarlavhasi (ID 181, "IN-0125", fayl ham, xat qatori ham yo'q — DOC-B04 natijasi);
  - **14 ta topshiriq bir xil `TASK-10` kodida** (TSK-B05 natijasi).
  - Qolgan tekshiruvlar 0 qator qaytardi.
- **`DataFix_Docs_02_Fix.sql`:** avval ROLLBACK bilan quruq sinov, keyin COMMIT.
  - ID 181 soft-delete qilindi.
  - TASK-10 kodida birinchisi (ID 25) qoldi, qolgan 13 tasi TASK-26…TASK-38 bo'ldi.
  - Qayta tekshiruvda 6 ta so'rovning barchasi 0 qator.
- **`Tasks_Docs_Indexes.sql`:** `IX_APP_TASKS_KeyCode`, `IX_DOC_DOCUMENTS_Code` qo'shildi.
  - Mavjud indekslar yetarli deb topildi. Bazada allaqachon `UX_COM_OFFICIAL_LETTERS_No_Date_Dir` va `UX_ADM_COMPANY_ORDERS_No_Date` bor — ular o'chirilgan yozuvlarni ham qamraydi, shuning uchun `DocNumbering` o'chirilgan raqamni ham band deb hisoblaydigan qilib moslandi.
- **STD shabloni:** `App_Data/Templates/CompanyStandardTemplate.docx` bazaga ko'chirildi (DOC_TEMPLATES ID 3, `COMPANY_STANDARD-01`) va papkadan o'chirildi.
- **Mavjud shablonlar yangi dvigatelda sinaldi:**
  - `COMP_ORDER_DEFAULT` va `OFFICIAL_LETTER_DEFAULT` — Word validatsiyasi 0 xato, to'ldirilmay qolgan maydon yo'q.
  - Eski nomlar aniq maydonlarga bog'landi: `Receipent.Receiver.Name` → CONTACT_NAME. Avval "Xurmatli Hurmatli ...!" takrori chiqardi.
- **Topilgan kod xatosi:** STD hujjat turi kodi bazada `COMPANY_STANDARD`, kodda esa `COMPANYSTANDARD` edi — tuzatildi.
