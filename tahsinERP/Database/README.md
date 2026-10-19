# tahsinERP baza qatlami

Yangi korxona bazasi SSMS'siz, bitta buyruq bilan yaratiladi; mavjud bazalar versiyalangan
migratsiyalar bilan yangilanadi. Panel (Control Panel) shu skriptlarni chaqiradi —
kontrakt: [`Docs/ControlPanel/03_DB_PROVISIONING.md`](../Docs/ControlPanel/03_DB_PROVISIONING.md).

```
Database/
  Schema/000_Baseline.sql      toza sxema (generatsiya qilingan, QO'LDA TAHRIR QILINMAYDI)
  Migrations/NNN_nomi.sql      har sxema o'zgarishi — yangi fayl
  Seed/100_Reference.sql       majburiy spravochniklar (generatsiya: Export-ReferenceSeed.ps1)
  Seed/110_Defaults.sql        yangi korxona standart qatorlari (qo'lda)
  Demo/*.sql                   demo tenant ma'lumoti (New-TenantDb.ps1 -Demo -> DB_DEMO_TEMPLATE.bak; 05_PROVISIONING.md 6)
  tools/Invoke-DbMigrations.ps1  runner: bitta baza yoki tenants.json dagi barchasi
  tools/New-TenantDb.ps1         yangi baza: CREATE -> baseline -> migratsiya -> seed -> admin
  tools/Export-ReferenceSeed.ps1 seed'ni etalon bazadan qayta generatsiya
  tools/Export-Baseline.ps1      baseline'ni qayta generatsiya ("squash", kamdan-kam)
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
| FIN_CATEGORIES | SystemCode | SALES, PURCHASE, PAYROLL, SHAREHOLDER_CONTRIBUTION |
| PRODUCT_CATEGORIES | SystemCode | EQUIPMENT |
| UNITS | SystemCode | PCS, KG, TONNE, GRAM, METER, M2, M3, LITER, MM, CM, PAIR |
| ROLES | SystemCode (007) | ADMIN, DEVELOPER — `RoleHelper.IsDeveloper/IsAdminOrDeveloper/HasRoleCode` |
| FIN_ACCOUNTS | Code (mavjud) | PAYABLES_{CurrencyID}, RECEIVABLES_{CurrencyID} |
| APP_TASK_STATUS_REF | Code + IsFinal (mavjud) | TODO, IN_PROGRESS, DONE, CANCELLED |
| WAREHOUSES | WarehouseType (mavjud) | SUPPLY, PRODUCTION, SALES |

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

Uydirma "Demo Plast" MChJ (plastik idishlar): 24 mahsulot (12 xomashyo + 12 tayyor, BOM/norma), 5 mijoz,
5 ta'minotchi, ~3 oylik xarid -> ombor -> ishlab chiqarish (DLOC/FIFO lotlar) -> sotuv (QQS 12%) -> to'lovlar,
ustav kapitali, 9 xodim va o'tgan oyning tasdiqlangan oyligi. Kirish: `demo` / `demo2026` (Admin roli).

- `200_Demo_Master.sql` — foydalanuvchi, ombor turlari/joylari, mahsulot, BOM/norma, kontragentlar, kurslar, hisoblar.
- `210_Demo_Operations.sql` — SRM, ombor, MES, CRM hujjatlari; `PRODUCT_STOCKS` harakatlardan, manfiy qoldiq bo'lsa THROW.
- `220_Demo_Finance_HR.sql` — FIN_TRANSACTIONS (FinancePostingService ko'zgu yozuvlari + to'lovlar), HR.

Hujjatlar ilova servislari yozadigan shaklda (raqam formatlari, statuslar, tavsiflar) — servis o'zgarsa demo ham
yangilanadi. Sanalar `GETDATE()` ga nisbatan; har fayl bitta batch (GO yo'q) va ma'lumot bor bo'lsa o'zini
o'tkazib yuboradi. Faqat yangi (bo'sh) bazaga mo'ljallangan.

## Baseline va seed'ni qayta generatsiya qilish

- **Seed**: `Export-ReferenceSeed.ps1 -Database DBTHSN-IPACK` (barcha migratsiyalar qo'llangan etalon baza).
  Qaysi jadval/qator kirishi skript boshidagi ro'yxatda. Tranzaksiya va kontragent ma'lumoti kirmaydi.
- **Baseline**: odatda kerak emas. Faqat hali hech bir tenant eski baseline'dan yaratilmagan bo'lsa yoki
  barcha tenantlar to'liq mos bo'lsa: `Export-Baseline.ps1 -SourceDatabase <etalon> [-ExtraDrop ...] [-FixupSql ...]`.
  Keyin natijani EDMX bilan solishtiring (jadval/ustun/tur/nullable) — master'da yo'q ustun baseline'ga
  kirmasligi kerak (boshqa branch'ning hali merge bo'lmagan o'zgarishi bo'lishi mumkin).
