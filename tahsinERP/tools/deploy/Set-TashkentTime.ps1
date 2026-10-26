<#
.SYNOPSIS
    Server vaqt mintaqasini Toshkentga (UTC+5) o'tkazadi va (ixtiyoriy) firma bazalaridagi eski UTC vaqt belgilarini +5 soatga suradi.

.DESCRIPTION
    ERP vaqtni server soatidan oladi (DateTime.Now, SQL GETDATE/SYSDATETIME). Server UTC'da bo'lsa hamma yozuv 5 soat orqada.
    Panel, bot va sayt vaqtni UTC'da saqlab o'zi Asia/Tashkent'ga aylantiradi - ularga ta'sir qilmaydi.

    Ishlatish tartibi (Administrator PowerShell):
      1) .\Set-TashkentTime.ps1                         # faqat hisobot: joriy mintaqa + qaysi ustunlarda nechta qator suriladi
      2) .\Set-TashkentTime.ps1 -SetTimeZone            # mintaqa -> Toshkent, SQL va IIS qayta ishga tushadi (1-2 daqiqa)
      3) .\Set-TashkentTime.ps1 -ShiftData              # DRY RUN: suriladigan qatorlar soni (hech narsa yozilmaydi)
      4) .\Set-TashkentTime.ps1 -ShiftData -Apply       # haqiqiy surish (har baza bitta tranzaksiyada, qayta ishlamaydi)

    Suriladigan ustunlar: faqat tizim avtomatik yozadigan vaqt belgilari (Created/Updated/Modified/Approved/Posted/... + At/DT/Date,
    USERLOGS/USER_ENTRIES.DateTime, GPS va tashrif vaqtlari). Hujjat sanalari (IssuedDate, DueDate, ExpiryDate, IssueDateTime...),
    sysutcdatetime() standartli va nomida Utc bor ustunlar, LICENSE_STATE/SCHEMA_VERSIONS/RECONCILIATION_ADJUSTMENTS tegilmaydi.
    Vaqt qismi 00:00:00 bo'lgan qiymatlar (sana sifatida saqlangan) ham tegilmaydi.
    Faqat mintaqa almashgan paytdan (-SetTimeZone yozgan belgi) OLDINGI qatorlar suriladi; bazada dbo.TIMEZONE_FIX belgisi qoladi -
    ikkinchi marta ishga tushirilsa o'sha baza o'tkazib yuboriladi.
    Oldin zaxira oling: .\Backup-Databases.ps1

.EXAMPLE
    .\Set-TashkentTime.ps1 -SetTimeZone
    .\Set-TashkentTime.ps1 -ShiftData -Database DB_demo -Apply
#>
param(
    [switch] $SetTimeZone,
    [switch] $ShiftData,
    [switch] $Apply,
    [string] $SqlServer = '.',
    [string[]] $Database,
    [int] $ShiftHours = 5
)
$ErrorActionPreference = 'Stop'
$TargetZone = 'West Asia Standard Time'   # Toshkent, UTC+5, yozgi vaqt yo'q
$MarkerFile = 'C:\ProgramData\tahsinERP\timezone-change.txt'

function Info([string]$t) { Write-Host $t }
function Ok([string]$t) { Write-Host ('[ok] ' + $t) -ForegroundColor Green }
function Warn([string]$t) { Write-Host ('[!] ' + $t) -ForegroundColor Yellow }

function Invoke-Sql([string]$db, [string]$sql, [switch]$Table) {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True"
    try {
        $cn.Open()
        $c = $cn.CreateCommand(); $c.CommandText = $sql; $c.CommandTimeout = 600
        if ($Table) {
            $dt = New-Object System.Data.DataTable
            $dt.Load($c.ExecuteReader())
            return ,$dt
        }
        return $c.ExecuteScalar()
    } finally { $cn.Dispose() }
}

$tz = Get-TimeZone
Info ("Joriy vaqt mintaqasi: {0} ({1}), UTC offset {2}" -f $tz.Id, $tz.DisplayName, $tz.BaseUtcOffset)

# ---------------------------------------------------------------- 1. Mintaqa
if ($SetTimeZone) {
    if ($tz.Id -eq $TargetZone) {
        Ok 'Server allaqachon Toshkent vaqtida - mintaqa o''zgartirilmadi.'
    } else {
        $changeUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')
        Set-TimeZone -Id $TargetZone
        New-Item -ItemType Directory -Force -Path (Split-Path $MarkerFile) | Out-Null
        Set-Content -Path $MarkerFile -Value $changeUtc -Encoding ASCII
        Ok ("Mintaqa -> $TargetZone. O'zgarish vaqti (UTC) $changeUtc -> $MarkerFile")

        # SQL Server mintaqani ishga tushganda oladi - GETDATE() to'g'ri bo'lishi uchun qayta ishga tushiriladi
        $sqlSvc = Get-Service | Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' } | Where-Object { $_.Status -eq 'Running' }
        foreach ($s in $sqlSvc) {
            Info ("SQL xizmati qayta ishga tushirilmoqda: " + $s.Name)
            Restart-Service -Name $s.Name -Force
        }
        Start-Sleep -Seconds 5
        Info 'IIS qayta ishga tushirilmoqda (barcha pool: ERP instance''lar, panel, bot, sayt)...'
        & "$env:windir\System32\iisreset.exe" /restart | Out-Host
        Ok ('Tayyor. Hozirgi vaqt: ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + ' (Toshkent)')
    }
}

if (-not $ShiftData) {
    if (-not $SetTimeZone) {
        Info ''
        Info 'Hech narsa o''zgartirilmadi. Keyingi qadam: -SetTimeZone, so''ng -ShiftData (dry run) va -ShiftData -Apply.'
    }
    return
}

# ---------------------------------------------------------------- 2. Eski yozuvlarni surish
if ($ShiftHours -le 0 -or $ShiftHours -gt 14) { throw 'ShiftHours 1..14 bo''lishi kerak.' }
if ((Get-TimeZone).Id -ne $TargetZone) { Warn 'Server hali Toshkent vaqtida emas - avval -SetTimeZone ishlating (aks holda yangi yozuvlar ham UTC bo''lib qoladi).' }

if (Test-Path $MarkerFile) {
    $cutoff = (Get-Content $MarkerFile -Raw).Trim()
    Info "Chegara (mintaqa almashgan payt, UTC): $cutoff - undan oldingi qatorlar suriladi."
} else {
    $cutoff = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')
    Warn "Mintaqa belgisi fayli yo'q - chegara hozirgi UTC vaqt: $cutoff"
}
if ($cutoff -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$') { throw "Chegara vaqti noto'g'ri: $cutoff" }

if (-not $Database) {
    # ERP firma bazalari: dbo.USERS va dbo.SCHEMA_VERSIONS bor bazalar (panel/bot bazalari UTC - tegilmaydi)
    $list = Invoke-Sql 'master' "SELECT name FROM sys.databases WHERE database_id > 4 AND state_desc = 'ONLINE' ORDER BY name" -Table
    $Database = @()
    foreach ($r in $list.Rows) {
        $n = [string]$r.name
        $isErp = Invoke-Sql $n "SELECT CASE WHEN OBJECT_ID('dbo.USERS') IS NOT NULL AND OBJECT_ID('dbo.SCHEMA_VERSIONS') IS NOT NULL THEN 1 ELSE 0 END"
        if ($isErp -eq 1) { $Database += $n }
    }
}
if (-not $Database -or $Database.Count -eq 0) { throw 'ERP bazasi topilmadi.' }
Info ('Bazalar: ' + ($Database -join ', '))

# Tizim vaqt belgilari: nom qoidasi + aniq ro'yxat; istisnolar
$colSql = @"
SELECT s.name AS sch, t.name AS tbl, c.name AS col
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
WHERE ty.name IN ('datetime', 'datetime2', 'smalldatetime')
  AND s.name IN ('dbo', 'dst', 'site')
  AND t.name NOT LIKE '%[_]BAK[_]%' AND t.name NOT LIKE '%[_]BAK'
  AND t.name NOT IN ('LICENSE_STATE', 'SCHEMA_VERSIONS', 'RECONCILIATION_ADJUSTMENTS', 'TIMEZONE_FIX', 'COMPANY_LICENSES', 'USER_LICENSES', 'TELEGRAM_LINK_CODES')
  AND c.name NOT LIKE '%Utc%'
  AND ISNULL(dc.definition, '') NOT LIKE '%utc%'
  AND c.is_computed = 0
  AND (
        c.name LIKE '%Created%' OR c.name LIKE '%Updated%' OR c.name LIKE '%Modified%' OR c.name LIKE '%Approved%'
     OR c.name LIKE '%Assigned%' OR c.name LIKE '%Changed%' OR c.name LIKE '%Cancelled%' OR c.name LIKE '%Posted%'
     OR c.name LIKE '%Confirmed%' OR c.name LIKE '%Delivered%' OR c.name LIKE '%Closed%' OR c.name LIKE '%Sent%'
     OR c.name LIKE '%Linked%' OR c.name LIKE '%Submitted%' OR c.name LIKE '%Processed%' OR c.name LIKE '%Verified%'
     OR c.name LIKE '%Calculated%' OR c.name LIKE 'Applied%' OR c.name LIKE 'LastSync%' OR c.name LIKE 'LastMovement%'
     OR c.name IN ('IssuedDT', 'At', 'StartAt', 'EndAt', 'StartedAt', 'FinishedAt', 'CheckInAt', 'CheckOutAt', 'CreditOverrideAt', 'ApprovalDate')
     OR (t.name IN ('USERLOGS', 'USER_ENTRIES') AND c.name = 'DateTime')
  )
  AND c.name NOT IN ('ActualDeliveryDate', 'PlannedDeliveryDate')
ORDER BY s.name, t.name, c.name
"@

$grand = 0
foreach ($db in $Database) {
    if ($db -notmatch '^[A-Za-z0-9_-]+$') { Warn "Baza nomi o'tkazib yuborildi: $db"; continue }
    Info ''
    Info "=== $db ==="
    $done = Invoke-Sql $db "IF OBJECT_ID('dbo.TIMEZONE_FIX') IS NULL SELECT CAST(NULL AS varchar(19)) ELSE EXEC('SELECT TOP 1 CONVERT(varchar(19), AppliedAt, 126) FROM dbo.TIMEZONE_FIX')"
    if ($done -isnot [DBNull] -and $done) { Ok "allaqachon surilgan ($done) - o'tkazib yuborildi"; continue }

    $cols = Invoke-Sql $db $colSql -Table
    $stmts = New-Object System.Collections.Generic.List[string]
    $dbTotal = 0
    foreach ($r in $cols.Rows) {
        $full = '[' + $r.sch + '].[' + $r.tbl + ']'
        $col = '[' + $r.col + ']'
        $where = "$col IS NOT NULL AND $col < '$cutoff' AND CAST($col AS time) <> '00:00:00'"
        $cnt = [int](Invoke-Sql $db "SELECT COUNT_BIG(*) FROM $full WHERE $where")
        if ($cnt -gt 0) {
            Info ("  {0}.{1}.{2}: {3}" -f $r.sch, $r.tbl, $r.col, $cnt)
            $stmts.Add("UPDATE $full SET $col = DATEADD(hour, $ShiftHours, $col) WHERE $where;")
            $dbTotal += $cnt
        }
    }
    Info ("  jami: $dbTotal qiymat")
    $grand += $dbTotal

    if ($Apply) {
        # Triggerlar (masalan summa qayta hisobi) vaqt surishda ishlamasin
        $body = "SET XACT_ABORT ON; BEGIN TRAN;`n"
        $body += "CREATE TABLE dbo.TIMEZONE_FIX (AppliedAt datetime2 NOT NULL, CutoffUtc datetime2 NOT NULL, ShiftHours int NOT NULL, Rows bigint NOT NULL);`n"
        foreach ($s in $stmts) {
            $tbl = ($s -split ' SET ')[0].Replace('UPDATE ', '')
            $body += "ALTER TABLE $tbl DISABLE TRIGGER ALL; $s ALTER TABLE $tbl ENABLE TRIGGER ALL;`n"
        }
        $body += "INSERT INTO dbo.TIMEZONE_FIX VALUES (SYSDATETIME(), '$cutoff', $ShiftHours, $dbTotal);`n"
        $body += "COMMIT;"
        Invoke-Sql $db $body | Out-Null
        Ok "$db - surildi (+$ShiftHours soat), belgi dbo.TIMEZONE_FIX"
    }
}

Info ''
if ($Apply) {
    Ok "Hammasi: $grand qiymat surildi."
    Info 'ERP keshlari uchun pool''larni yangilang: iisreset /restart'
} else {
    Info "DRY RUN: $grand qiymat suriladi. Yozish uchun: .\Set-TashkentTime.ps1 -ShiftData -Apply"
}
