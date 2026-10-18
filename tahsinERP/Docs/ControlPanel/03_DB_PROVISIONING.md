# Baza provisioning kontrakti (panel agenti → ERP baza qatlami)

Panel agenti (2-bosqich, 00_PLAN.md 7-bo'lim) yangi korxona bazasini yaratish va bazalarni yangilash uchun
**faqat** shu ikki skriptni chaqiradi. Skriptlar `tahsinERP/Database/` papkasi bilan birga serverga
ko'chiriladi (ERP release paketining bir qismi; versiyasi ERP bilan bir xil bo'lishi shart).

| Skript | Vazifa |
|---|---|
| `Database/tools/New-TenantDb.ps1` | yangi baza: CREATE DATABASE → baseline → migratsiyalar → seed (→ demo) → [shablon backup] → admin |
| `Database/tools/Invoke-DbMigrations.ps1` | mavjud baza(lar)ni yangilash: bitta baza yoki `tenants.json` dagi barchasi |

Skriptlar **qilmaydi**: `tenants.json` ga yozish, DNS/TLS/IIS binding, `ProvisioningJobs`, litsenziya
(`LICENSE_STATE.InstallationId` / `RegToken` yozilmaydi — LicenseService o'zi yaratadi/bloklaydi),
demo bazani kechasi reset qilish jadvali. Bular panel/agent ishi.

Talablar: Windows PowerShell 5.1+, .NET Framework 4.7.2+, SQL Server 2016+ (collation `Cyrillic_General_CI_AS`),
ulanuvchi login'da `dbcreator` (New-TenantDb) yoki bazada `db_owner` (Invoke-DbMigrations). `sqlcmd`/SSMS kerak emas.

## 1. New-TenantDb.ps1

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\tools\New-TenantDb.ps1 `
    -Server <sql> -DbName DB_<slug> [-SqlUser u -SqlPassword p] `
    [-CompanyName '"Firma" MChJ'] [-AdminLogin admin] [-AdminFullName ...] [-AdminEmail ...] [-AdminPassword ...] `
    [-NoAdmin] [-Demo] [-BackupTo <fayl.bak>] [-RecoveryModel FULL|SIMPLE] [-KeepOnFailure] [-LogFile <fayl>]
```

| Parametr | Ma'nosi |
|---|---|
| `-DbName` | `^[A-Za-z0-9_\-\.]{1,100}$`. Baza **mavjud bo'lmasligi** shart (aks holda kod 7, hech narsa o'zgarmaydi). |
| `-CompanyName` | `COMPANIES.ID=1` nomi (ERP footer'i va hujjatlarda). Bo'sh — "Yangi korxona". |
| `-AdminLogin` | 3–30 belgi `A-Za-z0-9_.-`, standart `admin`. Admin roli bilan yaratiladi, `MustChangePassword=1`. |
| `-AdminPassword` | berilmasa 12 belgili tasodifiy parol. Har holda birinchi kirishda almashtirish majburiy. |
| `-NoAdmin` | admin yaratilmaydi (shablon baza uchun). |
| `-Demo` | `Database/Demo/*.sql` ham qo'llanadi (demo tenant). Demo skriptlari hali yo'q bo'lsa — kod 2. |
| `-BackupTo` | seed (+demo) dan keyin, **admin yaratilishidan oldin** `COPY_ONLY` backup — `DB_TEMPLATE.bak` / `DB_DEMO_TEMPLATE.bak`. Yo'l SQL Server xizmati yoza oladigan joy bo'lishi kerak. |
| (env) `TAHSINERP_ADMIN_PASSWORD`, `TAHSINERP_SQL_PASSWORD` | `-AdminPassword` / `-SqlPassword` berilmasa shu env'dan olinadi — parol buyruq satrida (jarayonlar ro'yxatida) ko'rinmasin. Provisioning agenti shunday chaqiradi (05_PROVISIONING.md). |
| `-KeepOnFailure` | xato bo'lsa yarim yaratilgan bazani o'chirmaslik (tahlil uchun). Standart: o'chiriladi. |

Shablon oqimi (00_PLAN.md 7.2): bir marta `New-TenantDb.ps1 -DbName DB_TEMPLATE -NoAdmin -BackupTo ...\DB_TEMPLATE.bak`,
keyin har yangi tenant uchun `RESTORE ... WITH MOVE` + `Invoke-DbMigrations.ps1 -Database DB_<slug>` (shablondan keyingi
migratsiyalar) + admin yaratish. Hozircha oddiyroq yo'l: har tenant uchun to'g'ridan-to'g'ri `New-TenantDb.ps1`
(bo'sh instance'da ~10 soniya).

## 2. Invoke-DbMigrations.ps1

```powershell
Database\tools\Invoke-DbMigrations.ps1 -Server <sql> -Database DB_<slug> [-DryRun]
Database\tools\Invoke-DbMigrations.ps1 -AllTenants [-TenantsFile <yo'l>] [-ConnectionString <asos>] [-SkipDisabled] [-DryRun]
```

- `-AllTenants`: `-TenantsFile` → `TAHSINERP_TENANTS_FILE` env → `%ProgramData%\tahsinERP\tenants.json`
  (format: 02_MULTI_TENANT.md). Har tenant: `connectionString` bo'lsa u, aks holda asosiy ulanishda `Initial Catalog=database`.
  Bir xil bazaga bog'langan bir nechta slug — bir marta. Bitta tenant yiqilsa qolganlari davom etadi; chiqish kodi — birinchi xato.
- `-DatabaseRoot` berilmasa skript papkasining ota papkasi (2026-10-19 tuzatildi: Windows PowerShell 5.1 da `powershell -File` bilan chaqirilganda param default'ida `$PSScriptRoot` bo'sh bo'lib, skript ishga tushmasdi).
- `-DryRun`: hech narsa yozmaydi, rejani (`[PLAN] RUN|MARK ...`) chiqaradi; checksum tekshiruvi ishlaydi.
- `-BaselineExisting`: jadvallari bor, lekin `SCHEMA_VERSIONS` siz eski baza — baseline "qo'llangan" deb belgilanadi.
  Panel bu bayroqni avtomatik ishlatmasin (faqat odam, moslik tekshirilgach).
- `-Seed`, `-ExtraScripts` — New-TenantDb ichki foydalanadi.
- Har skript alohida tranzaksiyada; xato bo'lsa o'sha skript to'liq bekor qilinadi, oldingilari qoladi
  (qayta ishga tushirish xavfsiz — xato bergan skriptdan davom etadi).

## 3. Chiqish kodlari (ikkala skript)

| Kod | Ma'no | Agent nima qiladi |
|---|---|---|
| 0 | muvaffaqiyat | — |
| 1 | migratsiya/seed skripti xatosi (tranzaksiya bekor qilindi) | job = failed, log'ni operatorga |
| 2 | noto'g'ri parametr / tenants.json topilmadi yoki buzilgan / demo skriptlari yo'q | qayta urinmaslik |
| 3 | SQL Server'ga ulanib bo'lmadi | keyinroq qayta urinish |
| 4 | qo'llangan migratsiya fayli o'zgargan (checksum) | release xatosi — to'xtatish |
| 5 | eski baza baseline'siz (jadvallar bor, SCHEMA_VERSIONS yo'q) | operator `-BaselineExisting` qaror qiladi |
| 6 | bazada boshqa runner ishlayapti (applock) | keyinroq qayta urinish |
| 7 | (New-TenantDb) baza allaqachon mavjud | job = failed yoki idempotent "done" (agent tekshiradi) |
| 8 | (New-TenantDb) admin yaratishda xato | baza o'chirilgan; qayta urinish mumkin |
| 9 | (New-TenantDb) shablon backup xatosi | baza o'chirilgan; backup yo'lini tekshirish |

## 4. Chiqish va log

- Har qator: `yyyy-MM-dd HH:mm:ss [LEVEL] matn`; LEVEL: INFO, PLAN, SQL (PRINT xabarlari), WARN, ERROR (stderr), RESULT.
- **Oxirgi qator** doimo `... [RESULT] {json}` — agent faqat shuni parse qiladi:
  - Invoke-DbMigrations: `{"ok":true,"exitCode":0,"dryRun":false,"targets":[{"target":"slug/DB","applied":[...],"pending":[...],"status":"ok|failed|connect-failed|checksum-mismatch|not-baselined|locked","exitCode":0,"error":null}]}`
  - New-TenantDb: `{"ok":true,"exitCode":0,"database":"DB_x","server":"...","adminLogin":"admin","tempPassword":"...","backup":null,"error":""}`
- `-LogFile` — shu qatorlar faylga ham (UTF-8, qo'shib yoziladi).
- **`tempPassword` maxfiy**: agent uni faqat xush kelibsiz xabariga qo'yadi, job log'iga/DB'ga ochiq yozmaydi
  (RESULT qatorini saqlashdan oldin maydonni o'chiring). Foydalanuvchi birinchi kirishda almashtiradi.

## 5. Yaratiladigan baza tarkibi

- Sxema: baseline (133 jadval, 269 FK, 7 trigger, 7 view, 8 protsedura) + `Database/Migrations/*`.
- Seed: valyutalar (UZS, USD, RUB, KRW), o'lchov birliklari (SystemCode bilan), HS kodlar (11 294),
  ruxsat modullari + Admin/Developer rollari va to'liq huquqlari, balans va moliya kategoriyalari (SystemCode),
  P&L moddalari, hujjat turlari/holatlari, topshiriq holatlari/muhimliklari, transport turlari, "Jihoz" mahsulot kategoriyasi,
  `COMPANIES` (ID=1), sex va 3 ombor (Xomashyo / Ishlab chiqarish / Savdo), "Doimiy harajatlar qarzi", "Aksiyador hissalari".
- Admin bilan birga: `Kassa` (9000) va `Hisob raqam` (9001) UZS hisoblari.
- `dbo.SCHEMA_VERSIONS` — qaysi baseline/migratsiya/seed qachon qo'llangani.
- Yangi bazada litsenziya yo'q: admin kiradi, modullar `License` sahifasiga yo'naltiriladi — panel oqimi (01_LICENSE_API.md).
