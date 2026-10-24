# tahsinERP baza qatlami

Yangi korxona bazasi SSMS'siz, bitta buyruq bilan yaratiladi; mavjud bazalar versiyalangan
migratsiyalar bilan yangilanadi. Panel (Control Panel) shu skriptlarni chaqiradi —
kontrakt: [`Docs/ControlPanel/03_DB_PROVISIONING.md`](../Docs/ControlPanel/03_DB_PROVISIONING.md).

```
Database/
  Schema/000_Baseline.sql      toza sxema (generatsiya qilingan, QO'LDA TAHRIR QILINMAYDI)
  Migrations/NNN_nomi.sql      har sxema o'zgarishi — yangi fayl
  Seed/100_Reference.sql       majburiy spravochniklar (generatsiya: Export-ReferenceSeed.ps1)
  Seed/105_PermissionModules_Extra.sql  eksportdan keyin qo'shilgan ruxsat modullari (mavjud bazalarga — NNN migratsiya)
  Seed/110_Defaults.sql        yangi korxona standart qatorlari (qo'lda)
  Demo/NNN_<Modul>_*.sql       namuna ma'lumot, modul bo'yicha (New-TenantDb.ps1 -Demo; pastda "Demo ma'lumot")
  Setup/300_ModuleProfile.sql  tanlangan modullar profili: rollar/ruxsatlar, omborlar, boshlang'ich kategoriyalar
  tools/Invoke-DbMigrations.ps1  runner: bitta baza yoki tenants.json dagi barchasi
  tools/New-TenantDb.ps1         yangi baza: CREATE -> baseline -> migratsiya -> seed -> SETUP_MODULES -> (demo) -> profil -> admin
  tools/Export-ReferenceSeed.ps1 seed'ni etalon bazadan qayta generatsiya
  tools/Export-Baseline.ps1      baseline'ni qayta generatsiya ("squash", kamdan-kam)
  tools/RegressionProbe/         hisobotlar oldin/keyin solishtiruvi (Run-RegressionProbe.ps1) va barcha view'lar
                                 kompilyatsiyasi (Run-RazorCheck.ps1) — ustun turi o'zgarganda majburiy
```

## Asosiy qoida: endi har sxema o'zgarishi = yangi `NNN` fayl

1. `Database/Migrations/` ga keyingi raqam bilan fayl qo'shing: `007_HR_AddBonusTable.sql`
   (`^\d{3}_[A-Za-z0-9_-]+\.sql$`, raqamlar takrorlanmaydi). Bir branch'da bir nechta bo'lsa — ketma-ket.
   Ikki branch bir raqamni olsa, merge qilayotgan kishi keyingisini qayta raqamlaydi (hali hech qayerda
   qo'llanmagan bo'lsa).
2. Skript **idempotent** bo'lsin (`IF COL_LENGTH(...) IS NULL`, `IF NOT EXISTS (SELECT 1 FROM sys.indexes ...)`):
   lokal baza ko'pincha qo'lda oldinroq o'zgartirilgan bo'ladi.
3. Runner har faylni **bitta tranzaksiyada** bajaradi, `GO` bilan batch'larga bo'ladi. Shuning uchun:
   `CREATE DATABASE`, `ALTER DATABASE`, `BACKUP`, full-text kabi tranzaksiyadan tashqari buyruqlar yozilmaydi.
4. **Qo'llangan migratsiyani tahrir qilmang** — checksum (SHA-256) farq qilsa runner to'xtaydi (chiqish kodi 4).
   Tuzatish kerak bo'lsa — yangi NNN fayl.
5. EDMX'ni o'sha commit'da yangilang (`Models/DBTHSNModel.edmx` + `Models/*.cs`). Xom SQL bilan
   ishlatiladigan jadval/ustun bo'lsa ham migratsiya shart.
6. Ma'lumot tuzatish (DataFix) migratsiya EMAS — u bitta bazaga tegishli; `Database_Migrations/DataFix_*`
   kabi alohida qoladi. Agar tuzatish har bir tenant uchun kerak bo'lsa (masalan yangi ruxsat moduli) —
   idempotent INSERT bilan NNN migratsiya.
7. Yangi tizim spravochnik qatori (kod unga tayanadi) — `SystemCode` bilan (pastga qarang) va:
   migratsiya (mavjud bazalar uchun, idempotent) + `Seed/` (yangi bazalar uchun, seed qayta generatsiyasi).

Eski `Database_Migrations/` papkasidagi sxema skriptlari 000 baseline'ga singdirilgan (2026-10-18);
u yerga yangi sxema skripti qo'shilmaydi.

## Kod spravochnikni nom bo'yicha qidirmaydi — `SystemCode`

Kod tizim qatorini faqat barqaror kod bo'yicha topadi; `Name` — faqat ko'rsatish uchun, foydalanuvchi
uni erkin o'zgartiradi. `SystemCode` bor qatorni UI'da o'chirib bo'lmaydi.

| Jadval | Ustun | Kodlar (Services/Common/SystemCodes.cs) |
|---|---|---|
| FIN_BALANCE_CATEGORIES | SystemCode | CASH, BANK, PAYABLES, RECEIVABLES, CHARTER_CAPITAL, PAYROLL_DEBT, LOGISTICS_DEBT, RAW_MATERIALS, FINISHED_GOODS, EQUIPMENT, BUILDINGS, RECURRING_OBLIGATIONS |
| FIN_CATEGORIES | SystemCode | SALES, PURCHASE, PAYROLL, SHAREHOLDER_CONTRIBUTION, MATERIAL_TRANSPORT, FG_TRANSPORT |
| PRODUCT_CATEGORIES | SystemCode | EQUIPMENT |
| UNITS | SystemCode | PCS, KG, TONNE, GRAM, METER, M2, M3, LITER, MM, CM, PAIR |
| ROLES | SystemCode (007) | ADMIN, DEVELOPER — `RoleHelper.IsDeveloper/IsAdminOrDeveloper/HasRoleCode` |
| FIN_ACCOUNTS | Code (mavjud) | PAYABLES_{CurrencyID}, RECEIVABLES_{CurrencyID} |
| APP_TASK_STATUS_REF | Code + IsFinal (mavjud) | TODO, IN_PROGRESS, DONE, CANCELLED |
| WAREHOUSES | WarehouseType (mavjud) | SUPPLY, PRODUCTION, SALES (yangi bazada 3/6/7 — Seed/110) |
| FIN_BALANCE_CATEGORY_CATEGORIES | bog'lanish | CHARTER_CAPITAL ← SHAREHOLDER_CONTRIBUTION (migratsiya 009, Seed/110) |

Lookup: `SystemRefs.BalanceCategory(db, SystemCodes.BalanceCategory.Cash)`, `SystemRefs.UnitId(db, SystemCodes.Unit.Pieces)`.
Har ustunda `UNIQUE ... WHERE SystemCode IS NOT NULL` indeks bor.

### Guruh belgilari (UNIQUE emas, foydalanuvchi formada tanlaydi)

Bir nechta qatorga tegishli tushuncha (masalan "transport xarajatlari") — bitta tizim qatori emas, guruh. Kod uni nom
kalit so'zlari bilan emas, shu ustunlar bilan taniydi:

| Jadval.ustun | Qiymatlar | Kim ishlatadi |
|---|---|---|
| FIN_CATEGORIES.AnalyticsGroup (007) | MATERIAL, PAYROLL, PURCHASE, LOGISTICS, TAX, VAT, BANK, INTEREST, DEPRECIATION, CAPEX, DIVIDEND, FINANCING, OTHER | `ValuationService` (EBITDA: soliq/foiz/amortizatsiya, dividend chiqariladi), `TannarxFinanceCategoryClassifier` (fakt tannarx guruhi; `TANNARX_FIN_CATEGORY_MAP` undan ustun) |
| PRODUCT_CATEGORIES.MaterialKind (007) | WOOD | `NormLineMath.IsWoodCategory` (norma/BOM: metr narxi, umumiy norma/chiqindi) |
| PRODUCT_CATEGORIES.CalculateByWeight (mavjud) | 0/1 | `UnitConversionService.ShouldCalculateByWeightAsync` (metiz ham shu belgi bilan) |

Ro'yxatlar va UI nomlari: `SystemCodes.FinAnalyticsGroup.All`, `SystemCodes.MaterialKind.All`. Yangi guruh qo'shilsa —
shu ro'yxatga + uni ishlatadigan servisga; mavjud bazalarni to'ldirish kerak bo'lsa — NNN migratsiya.

## Ishga tushirish

```powershell
# yangi korxona (bo'sh SQL instance yetarli)
.\Database\tools\New-TenantDb.ps1 -Server localhost -DbName DB_firma1 -CompanyName '"Firma 1" MChJ'

# faqat tanlangan modullar uchun (sinov): sozlamalar + shu modullarning namuna ma'lumoti
.\Database\tools\New-TenantDb.ps1 -Server localhost -DbName DB_firma2 -Modules Srm,Crm,Tasks,Finance -Demo

# bitta bazani yangilash (avval reja)
.\Database\tools\Invoke-DbMigrations.ps1 -Server localhost -Database DB_firma1 -DryRun
.\Database\tools\Invoke-DbMigrations.ps1 -Server localhost -Database DB_firma1

# tenants.json dagi barcha bazalar
.\Database\tools\Invoke-DbMigrations.ps1 -AllTenants -TenantsFile C:\ProgramData\tahsinERP\tenants.json

# baseline'dan oldin yaratilgan (eski) baza: sxema baseline'ga mosligini tekshirib, so'ng
.\Database\tools\Invoke-DbMigrations.ps1 -Database DBTHSN-IPACK -BaselineExisting
```

Mavjud bazaning baseline'ga mosligini tekshirish: SqlPackage `DeployReport` (baseline dacpac → baza);
farq faqat BAK/DataFix jadvallari bo'lishi kerak. Lokal `DBTHSN-IPACK` 2026-10-18 da shunday belgilangan
(zaxira: `Backup\DBTHSN-IPACK_pre_migrations_20261018.bak`).

## `dbo.SCHEMA_VERSIONS`

Runner o'zi yaratadi. `ScriptName` (PK, masalan `Migrations/001_SystemCodes.sql`), `Kind`
(baseline / migration / seed / extra), `Version`, `Checksum`, `AppliedAt` (UTC), `AppliedBy`, `DurationMs`, `Note`.
Bir bazada bir vaqtda faqat bitta runner (`sp_getapplock`).

## Demo ma'lumot (`Demo/`)

Uydirma "Demo Plast" MChJ (plastik idishlar): 24 mahsulot (12 xomashyo + 12 tayyor), ~3 oylik bitta hujjat ssenariysi.
Kirish: `demo` / `demo2026` (Admin roli; sinov tenantida agent uni o'chirib qo'yadi).

**Modul bo'yicha.** Fayl nomi `NNN_<Modul>_*.sql` (`Modul` = `EditionModules.ProductModule` nomi yoki `Core`). Har modul fayli
boshida `dbo.fn_SetupHasModule('<Modul>') = 0` bo'lsa o'zini o'tkazib yuboradi — `dbo.SETUP_MODULES` (032) ni
`New-TenantDb.ps1 -Modules` to'ldiradi, bo'sh jadval = hamma modul (demo shabloni, eski bazalar). Shuning uchun New-TenantDb
hamma faylni beradi, qaysi biri ishlashini baza hal qiladi (test: `TrialModulesUnitTests.ScriptModuleLists_MatchProductModule`).

| Fayl | Modul | Nima |
|---|---|---|
| `200_Core_Master.sql` | Core (har doim) | demo foydalanuvchi, ombor turlari, kategoriyalar, 24 mahsulot, kurslar, kassa/bank; ssenariy `demo_tmp` sxemasiga (narxlar, BOM, ishlab chiqarish rejasi, sarf, o'tkazma, xarid, sotuvga tayyor miqdor) |
| `205_Engineering_Bom.sql` | Engineering | norma, BOM, tannarx |
| `210_Srm_Purchases.sql` | Srm | ta'minotchilar, shartnoma -> buyurtma -> invoys -> SUPPLY kirimi |
| `220_Mes_Production.sql` | Mes | SUPPLY -> PRODUCTION o'tkazma (FIFO), DLOC lotlar, ishlab chiqarish, sifat, sotuv omboriga o'tkazma, reja. Srm bo'lmasa — xomashyo boshlang'ich qoldig'i |
| `230_Crm_Sales.sql` | Crm | mijozlar, shartnoma -> buyurtma -> invoys -> sotuv chiqimi, ochiq buyurtmalar. Mes bo'lmasa — tayyor mahsulot boshlang'ich qoldig'i |
| `240_Finance_Ledger.sql` | Finance | ustav kapitali, mavjud invoyslar ko'zgusi va to'lovlari, doimiy xarajatlar (tashuv — Logistika bo'lmasa) |
| `250_Hr_Payroll.sql` | Hr | tuzilma, 9 xodim, tabel, oylik (to'lovi — Finance bo'lsa) |
| `260_Tasks_Projects.sql` | Tasks | 3 loyiha, 12 topshiriq |
| `270_Logistics_Waybills.sql` | Logistics | ekspeditor, shartnoma/tarif, yo'l varaqalari (mavjud xarid/sotuv invoyslariga), to'lovlar |
| `280_Docs_Letters.sql` | Docs | rasmiy xatlar (kiruvchi/chiquvchi), buyruqlar |
| `290_Distribution_Orders.sql` | Distribution | hududlar, savdo nuqtalari, agentlar (parolsiz), narxlar, 14 buyurtma (barcha holatlar), jo'natish, qarz daftari, to'lovlar |
| `295_Website_Catalog.sql` | Website | sayt sozlamalari, 6 mahsulot, 3 sahifa, 4 so'rov |
| `299_Core_Finish.sql` | Core (har doim) | `PRODUCT_STOCKS` barcha harakatlardan (manfiy qoldiq — THROW), `demo_tmp` o'chiriladi |

Hujjatlar ilova servislari yozadigan shaklda (raqam formatlari, statuslar, tavsiflar) — servis o'zgarsa demo ham
yangilanadi. Sanalar `GETDATE()` ga nisbatan; har fayl bitta batch (GO yo'q) va ma'lumot bor bo'lsa o'zini
o'tkazib yuboradi. Runner hamma fayllarni bitta sessiyada bajaradi — har fayl oxirida o'z `#temp` jadvallarini o'chiradi.
Faqat yangi (bo'sh) bazaga mo'ljallangan. Yangi modul demo fayli: `NNN_<Modul>_*.sql`, boshida `fn_SetupHasModule` tekshiruvi,
miqdorlar kerak bo'lsa `demo_tmp` dan.

## Modul profili (`Setup/300_ModuleProfile.sql`)

Demo'dan keyin, har yangi bazada (namuna ma'lumotsiz ham) ishlaydi. Tanlanmagan modul ombori (harakatsiz bo'lsa) o'chirib qo'yiladi;
Distributsiya — sotuv ombori savdo nuqtasi; mahsulot yo'q bazada modulga mos boshlang'ich kategoriyalar. Valyuta, o'lchov birligi,
ТНВЭД, moliya kategoriyalari va boshqa spravochniklar Core — hammaga to'liq (keyin paket kengaysa qayta seed kerak emas).
Controller -> modul xaritasi `EditionModules` bilan bir xil bo'lishi shart (`TrialModulesUnitTests.ModuleProfileSql_ControllerMap_MatchesEditionModules`).

### Rollar va ruxsatlar

Har tanlangan modulga bitta rol; ruxsatlar skriptdagi `#matrix` jadvalidan (shu jadval bilan bir xil). **C** — ko'rish + yaratish/
tahrirlash (`ViewPermit = 1, ChangePermit = 1`), **V** — faqat ko'rish (`ViewPermit = 1, ChangePermit = 0`). O'chirish
(`DeletePermit`) hech bir modul roliga berilmaydi — faqat Admin. Qator faqat rol moduli **va** maqsad moduli tanlangan bo'lsa
qo'llanadi (masalan Logistika tanlanmagan bo'lsa Sotuv menejeri yo'l varaqalarini ko'rmaydi). Admin/Developer — to'liq (110_Defaults).
Matritsadagi controller ko'rsatilgan modulga tegishliligi testda tekshiriladi (`ModuleProfileSql_PermissionMatrix_ControllersBelongToTargetModule`).

| Rol (modul) | C — o'z ishi | V — faqat ko'rish |
|---|---|---|
| hamma modul rollari | Topshiriqlar: `Tasks`, `TaskKanban`, `TaskChangeStatus` | bosh sahifa, mahsulot, kategoriya, o'lchov birligi, valyuta, ombor, korxona, qoldiqlar; `TaskDashboard`, topshiriq loyihasi/holati/muhimligi |
| Ta'minot menejeri (Srm) | butun SRM (xarid, ta'minotchi, kirim/chiqim, inventarizatsiya, ombor hisobotlari); mahsulot, kategoriya | ishlab chiqarish rejasi, yo'l varaqalari, moliya tranzaksiyalari |
| Sotuv menejeri (Crm) | butun CRM (mijoz, shartnoma, buyurtma, invoys, SF, TTN) | ombor hisobotlari, ishlab chiqarish rejasi, yo'l varaqalari, moliya tranzaksiyalari |
| Ishlab chiqarish ustasi (Mes) | butun MES; ishlab chiqarishdan sotuvga o'tkazma | BOM/norma/tannarx, kirim/chiqim, ombor hisobotlari |
| Texnolog (Engineering) | BOM, norma, tannarx; mahsulot, kategoriya | butun MES |
| Hisobchi (Finance) | butun Moliya; valyuta | xarid invoys/shartnoma, sotuv invoys/shartnoma, oylik, yo'l varaqasi to'lovi va shartnomasi, distributsiya to'lovlari va qarzlari |
| Kadrlar mutaxassisi (Hr) | butun HR | buyruqlar, lavozim yo'riqnomalari |
| Logist (Logistics) | butun Logistika | xarid buyurtma/invoys, sotuv buyurtma/invoys, distributsiya reyslari |
| Devonxona mudiri (Docs) | rasmiy xat, buyruq, standart, yo'riqnoma | xodimlar, bo'linmalar, lavozimlar |
| Distributsiya menejeri | butun Distributsiya (maxsus `Dst` huquqlari — tannarx, kredit limitidan o'tish, cheksiz chegirma — Admin'da); mahsulot | moliya tranzaksiyalari, sayt so'rovlari |
| Sayt menejeri (Website) | butun Sayt-katalog | distributsiya narx va qoldiqlari, mijozlar |

## Bosh sahifa va dashboardlar (ERP)

`HomeController` / `Views/Home/Index.cshtml` bo'limlari kalitdagi paket (sinovda tanlangan modullar) **va** foydalanuvchi roli
bo'yicha (`HomeSections`, `RoleHelper.IsViewPermitted` — menyu bilan bir xil qoida): sotuv KPI/grafik — CRM, ishlab chiqarish — MES,
topshiriqlar — Tasks, xomashyo qoplash kunlari — ombor hisobotlari yoki MES. Server ham faqat shu bo'limlarni hisoblaydi va
signallar (insights) faqat ko'rsatilgan bo'limlar bo'yicha chiqadi. Yuqorida ochiq modul dashboardlariga tezkor plitkalar
(SRM, CRM, Topshiriqlar, MES, Tannarx, Moliya, HR, Logistika, Hujjatlar, Distributsiya, Sayt); umumiy KPI bo'limi bo'lmasa
(masalan faqat HR + Hujjatlar) — faqat plitkalar va izoh.

## Baseline va seed'ni qayta generatsiya qilish

- **Seed**: `Export-ReferenceSeed.ps1 -Database DBTHSN-IPACK` (barcha migratsiyalar qo'llangan etalon baza).
  Qaysi jadval/qator kirishi skript boshidagi ro'yxatda. Tranzaksiya va kontragent ma'lumoti kirmaydi.
- **Baseline**: odatda kerak emas. Faqat hali hech bir tenant eski baseline'dan yaratilmagan bo'lsa yoki
  barcha tenantlar to'liq mos bo'lsa: `Export-Baseline.ps1 -SourceDatabase <etalon> [-ExtraDrop ...] [-FixupSql ...]`.
  Keyin natijani EDMX bilan solishtiring (jadval/ustun/tur/nullable) — master'da yo'q ustun baseline'ga
  kirmasligi kerak (boshqa branch'ning hali merge bo'lmagan o'zgarishi bo'lishi mumkin).
