# 4. Kunlik zaxira (SQL + sozlamalar → Lightsail bucket)

Lightsail'da Windows instance uchun **avtomatik snapshot yo'q**. Shuning uchun har kecha serverning o'zi zaxira oladi va
uni serverdan **tashqariga** — Lightsail Object Storage bucket'iga (S3) yuboradi. Server butunlay yo'qolsa ham ma'lumot qoladi.

| Nima | Qayerga | Qancha saqlanadi |
|---|---|---|
| Har bir SQL baza (`DB_*`, `tahsin_control`, `tahsin_bot`, `tahsin_site`, ...) — `.bak.gz` | `s3://<bucket>/sql/<yyyy-MM-dd>/` | 14 kun; oyning 1-kunidagi nusxa — 180 kun |
| `config.zip`: `C:\ProgramData\tahsinERP` (tenants.json, panel kalitlari, imzo kaliti fayli, bot rasmlari), IIS `applicationHost.config` (pool sozlamalari va sirlari), har saytning `Web.config` (machineKey) | shu papkada | shunday |
| Serverdagi nusxa | `C:\tahsinERP\Backups\` | 2 kun |

Skriptlar: `tahsinERP/tools/deploy/Backup-Databases.ps1` (zaxira, ro'yxat, yuklab olish) va `Install-Backup.ps1` (o'rnatish).
Tashqi dastur (AWS CLI) kerak emas — S3 so'rovlari skript ichida AWS Signature V4 bilan imzolanadi.

> ⚠️ `config.zip` ichida sirlar bor (pool sozlamalari, bot kalitlari). Bucket **faqat private** bo'lsin, kalit faqat shu bucket'ga.

**Bepul variant — faqat lokal** (`-Bucket` berilmasa): xuddi shu zaxira har kecha `C:\tahsinERP\Backups\` da, 7 kun.
Xato yangilanish, o'chirilgan ma'lumot yoki buzilgan bazadan himoya qiladi, lekin **server yo'qolsa nusxalar ham yo'qoladi** —
vaqti-vaqti bilan (masalan haftada bir) eng oxirgi papkani o'z kompyuteringizga ko'chiring. Keyin bucket qo'shish:
`Install-Backup.ps1` ni `-Bucket ... -SecretsFile ...` bilan qayta ishga tushirish kifoya.

```powershell
.\Install-Backup.ps1 -RunNow                 # faqat lokal, 7 kun
```

## 4.1 Bucket va kalit (Lightsail konsoli, bir marta)

Lightsail chap menyusida **Storage** → **Create bucket** (instance sahifasidagi "Storage" yorlig'i emas — u disklar uchun).

1. **Create bucket**: hudud — serverniki (Frankfurt, `eu-central-1`), reja — eng kichigi
   (5 GB, ~$1/oy), nom — masalan `tahsinerp-backup-<tasodifiy>` (nom butun dunyoda yagona bo'lishi kerak).
2. Bucket → **Permissions**: "Bucket access" — **All objects are private**.
3. Bucket → **Permissions → Access keys → Create access key**. Ikki qiymat chiqadi (secret faqat bir marta ko'rinadi).
   Serverda Notepad'da `C:\tahsinERP\s3.txt` ga **2 qator** qilib yozing: 1-qator Access key ID, 2-qator Secret access key.

## 4.2 O'rnatish (serverda, Administrator PowerShell)

`Backup-Databases.ps1` va `Install-Backup.ps1` ni bitta papkaga qo'ying:

```powershell
cd C:\tahsinERP\backup-pkg
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Install-Backup.ps1 -Bucket <bucket-nomi> -SecretsFile C:\tahsinERP\s3.txt -RunNow
```

Skript: papkalar va huquqlar (SQL xizmati `C:\tahsinERP\Backups` ga yozadi) → `config.json` → kalit DPAPI bilan shifrlanadi
(`C:\ProgramData\tahsinERP\Backup\s3.key`, faqat SYSTEM va Administrators), `s3.txt` o'chiriladi → imzo o'z-o'zini tekshirish
va bucket'ga ulanish → Task Scheduler: **"tahsinERP Backup"**, har kuni **01:30** (SYSTEM; demo qayta tiklanishi 03:00 da).
`-RunNow` darhol sinov zaxirasini oladi va natijani ko'rsatadi.

Parametrlar: `-At 02:00`, `-KeepDays 30`, `-KeepMonthlyDays 365`, `-LocalKeepDays 3`, `-Region`, `-Prefix`.
Qayta ishga tushirish xavfsiz; kalitni almashtirish — yangi `-SecretsFile` bilan.

## 4.3 Kuzatish

- **Panel → Serverlar → "Oxirgi zaxira"**: bajarildi / eskirgan / xato, vaqt, fayllar soni, hajm, xato matnlari.
  Skript natijani `C:\ProgramData\tahsinERP\Updates\Status\backup.json` ga ham yozadi, agent uni inventar bilan
  (5 daqiqada bir) panelga yuboradi. Xato bo'lsa yoki 26 soatdan beri yangi zaxira yo'q bo'lsa — **Bosh sahifada** qizil ogohlantirish.
- Oxirgi natija: `Get-Content C:\ProgramData\tahsinERP\Backup\last.json` (`Ok: true`, yuklangan fayllar).
- Batafsil log: `C:\ProgramData\tahsinERP\Backup\logs\backup-<sana>.log` (60 kun).
- Xato bo'lsa: Windows **Event Viewer → Application**, manba `tahsinERP.Backup`; task "Last Run Result" ≠ 0.
- Biror baza xato bersa ham qolganlari yuklanadi; o'sha kuni bucket'dagi eski nusxalar **o'chirilmaydi**.

## 4.4 Tiklash

```powershell
$s = 'C:\tahsinERP\Backup\Backup-Databases.ps1'
& $s -List                                                   # bucket'dagi sanalar va fayllar
& $s -Download 2026-10-23 -Database DB_ipack                 # -> C:\tahsinERP\restore\DB_ipack.bak (gzip ochiladi)
& $s -Download 2026-10-23                                    # shu kunning hammasi (config.zip ham)
.\Restore-TenantBackup.ps1 -BakFile C:\tahsinERP\restore\DB_ipack.bak -Slug ipack -DbName DB_ipack -Replace
```

Panel/bot/sayt bazasi: `RESTORE DATABASE [tahsin_control] FROM DISK = N'...' WITH REPLACE` (avval pool'ni to'xtating).
Yangi serverga to'liq tiklash: o'rnatish skriptlari (01_DEPLOY_GUIDE.md) → bazalarni tiklash → `config.zip` dan
`ProgramData\tahsinERP` ni qaytarish, pool sozlamalari va `Web.config` machineKey'ni arxivdagi `iis\applicationHost.config`
va `sites\<sayt>\Web.config` dan olish.

Sinov (lokal): skript `config.json` dagi ixtiyoriy `Endpoint` (S3 bilan mos lokal server, path-style) va `Databases`
(faqat shu bazalar) bilan sinalgan; `-SelfTest` imzoni AWS hujjatidagi namuna bilan tekshiradi.
