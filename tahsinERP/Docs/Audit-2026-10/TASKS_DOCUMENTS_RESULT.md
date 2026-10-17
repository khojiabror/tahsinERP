# Tasks + Documents — bajarilgan ishlar (2026-10-17)

Branch: `claude/opt-tasks-docs-9aa641` (core `19c07bd7` ustida), 10 ta commit. Merge qilinmagan.

**Tekshiruv:** MSBuild (Debug) xatosiz. Modul view'lari aspnet_compiler bilan alohida nusxada kompilyatsiya qilindi (xatosiz).
HTML→DOCX konverter namunaviy HTML bilan sinaldi: OpenXmlValidator 0 xato, rasm joylashgan.
**Brauzerda va jonli bazada sinalmagan** — ayniqsa Dashboard GROUP BY, Kanban filtri, STD Edit muharriri, xat/buyruq yaratish va raqamlash.

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
    Migratsiya: `Database_Migrations/Alter_COMPANIES_AddDirectorName.sql` (ishga tushirilmagan; ustun bo'lmaguncha eski qiymat ishlatiladi).
    Tahrirlash UI'si yo'q (Company sahifasi core'da) — hozircha SQL bilan o'rnatiladi.
  - **MAN-B01:** dasturchilar uchun "CRUD arxitekturasi" bo'limi qo'llanmadan olib tashlandi.
  - `assets/` ildizidagi 6 ta Cursor skrinshoti o'chirildi (csproj yozuvlari bilan).
- **Sxema (taklif, `Database_Migrations/Proposal_Tasks_Docs_Indexes.sql`):**
  - DOC-P02 — table splitting.
  - DOC-P10 — xat JSON'i alohida ustunlarga.
  - MNT-01/TSK-B09 — ref jadvallarga `IsFinal` va `ColorHex`.
  - Unique indekslar.
- **Kod:**
  - DOC-P08: rasmlarni dedupe qilish yoki faqat joriy versiyani ko'rsatish.
  - TSK-P10: DataTables server-side.
  - MNT-02: Dashboard JS'ni alohida faylga chiqarish.
  - Create/Edit view juftlarini va Quill/mammoth skriptini `_Form` partial'larga birlashtirish (JobDesc bilan umumiy, HRM bilan kelishish kerak).
  - TaskPriority/TaskStatus view'larini umumlashtirish.
  - `OfficialLetterDataService.HtmlToPlainText` CompanyOrders'da ishlatiladi (egasi noto'g'ri).
- **Core uchun:**
  - `SoftDeleteReferenceService.AfterDocDocumentSoftDeletedAsync` (`:237`) barcha DOC_FILES'ni blob bilan yuklaydi. Bu modul uni endi chaqirmaydi; JobDesc chaqirsa, set-based UPDATE'ga o'tkazish kerak.
  - `RoleHelper.IsAdminOrDeveloper` umumiy qatlamda bo'lishi kerak (hozir modulda `TaskRules`).

## Data-fix skriptlari (ishga tushirilmagan)

- `Database_Migrations/DataFix_Docs_01_Find.sql` — faqat SELECT:
  - STD IsDeleted nomuvofiqligi;
  - o'chirilgan hujjatlarning faol fayllari;
  - yetim xat/buyruq sarlavhalari;
  - bir nechta IsMain;
  - takroriy raqamlar;
  - o'chirilgan ref'ga bog'langan topshiriqlar.
- `Database_Migrations/DataFix_Docs_02_Fix.sql` — 1–4-bo'limlarni tuzatadi. Standart holatda `ROLLBACK` bilan tugaydi. Takroriy raqamlarga tegmaydi.
- TSK-B01 tufayli yo'qolgan izohlarni tiklab bo'lmaydi — ular bazada qolmagan.
