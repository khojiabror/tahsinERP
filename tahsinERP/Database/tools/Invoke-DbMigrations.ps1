<#
.SYNOPSIS
    tahsinERP baza migratsiya runner'i (DbUp uslubida): baseline + Migrations/NNN_*.sql (+ Seed).

.DESCRIPTION
    Bitta bazaga (-Database) yoki tenants.json dagi barcha bazalarga (-AllTenants) hali qo'llanmagan
    skriptlarni tartib bilan, har birini ALOHIDA TRANZAKSIYADA qo'llaydi va dbo.SCHEMA_VERSIONS ga yozadi.

      1. Schema/000_Baseline.sql  — faqat bo'sh bazada (jadval yo'q) avtomatik.
                                     Mavjud (eski) bazada: -BaselineExisting bilan "qo'llangan" deb belgilanadi.
      2. Migrations/NNN_*.sql     — tartib raqami bo'yicha; qo'llanganlari checksum bilan tekshiriladi.
                                     Mazmunni o'zgartirmaydigan tuzatishdan keyin faylga "-- @supersedes-checksum: <eski sha256>"
                                     qatori yoziladi — eski checksum bilan qo'llangan bazalar xato bermaydi.
      3. Seed/*.sql               — faqat -Seed bilan (idempotent; checksum o'zgarsa qayta ishlaydi).
      4. -ExtraScripts            — qo'shimcha skriptlar (masalan Demo), Seed kabi kuzatiladi.

    Kontrakt va chiqish kodlari: Docs/ControlPanel/03_DB_PROVISIONING.md.

.EXAMPLE
    .\Invoke-DbMigrations.ps1 -Server localhost -Database DB_firma1 -DryRun
    .\Invoke-DbMigrations.ps1 -Server localhost -Database DBTHSN-IPACK -BaselineExisting
    .\Invoke-DbMigrations.ps1 -AllTenants -TenantsFile C:\ProgramData\tahsinERP\tenants.json -ConnectionString "Data Source=.;Integrated Security=True"
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [string]$Database,
    # Asosiy connection string (Initial Catalog almashtiriladi). Berilmasa: -Server + Windows auth (yoki -SqlUser/-SqlPassword).
    [string]$ConnectionString,
    [string]$SqlUser,
    [string]$SqlPassword,

    [switch]$AllTenants,
    [string]$TenantsFile,
    [switch]$SkipDisabled,

    [switch]$DryRun,
    [switch]$BaselineExisting,
    [switch]$Seed,
    [string[]]$ExtraScripts = @(),
    # Checksum farqini xato emas, ogohlantirish deb hisoblash (faqat tushunib ishlating).
    [switch]$AllowChecksumMismatch,

    [string]$DatabaseRoot,
    [string]$LogFile,
    [int]$CommandTimeout = 0
)
# Windows PowerShell 5.1: `powershell -File` bilan chaqirilganda param default'ida $PSScriptRoot bo'sh — shu yerda hisoblanadi.
if (-not $DatabaseRoot) { $DatabaseRoot = Split-Path -Parent $PSScriptRoot }
# Parol buyruq satrida ko'rinmasligi uchun env orqali ham beriladi (provisioning agenti shunday chaqiradi).
if ($SqlUser -and -not $SqlPassword -and $env:TAHSINERP_SQL_PASSWORD) { $SqlPassword = $env:TAHSINERP_SQL_PASSWORD }

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# ---- chiqish kodlari (03_DB_PROVISIONING.md bilan bir xil) ----
$EXIT_OK = 0; $EXIT_SCRIPT_FAILED = 1; $EXIT_BAD_ARGS = 2; $EXIT_CONNECT = 3; $EXIT_CHECKSUM = 4; $EXIT_NOT_BASELINED = 5; $EXIT_LOCKED = 6

function Write-Log([string]$Level, [string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}' -f (Get-Date), $Level, $Message
    if ($Level -eq 'ERROR') { [Console]::Error.WriteLine($line) } else { Write-Host $line }
    if ($script:LogFile) { Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8 }
}

function Get-ScriptText([string]$Path) {
    # BOM'siz, CRLF -> LF: checksum git autocrlf ga bog'liq bo'lmasin
    $t = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    return $t.TrimStart([char]0xFEFF).Replace("`r`n", "`n")
}

function Get-Checksum([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)) | ForEach-Object { $_.ToString('x2') }) }
    finally { $sha.Dispose() }
}

function Split-Batches([string]$Text) {
    # GO — alohida qatorda (katta-kichik harf farqsiz). Bo'sh batch'lar tashlanadi.
    return [regex]::Split($Text, '^\s*GO\s*(?:--[^\n]*)?$', [Text.RegularExpressions.RegexOptions]'Multiline,IgnoreCase') |
        Where-Object { $_.Trim().Length -gt 0 }
}

function New-ConnString([string]$Db, [string]$TenantConn) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    if ($TenantConn) { $b.ConnectionString = $TenantConn }
    elseif ($ConnectionString) { $b.ConnectionString = $ConnectionString }
    else {
        $b['Data Source'] = $Server
        if ($SqlUser) { $b['User ID'] = $SqlUser; $b['Password'] = $SqlPassword } else { $b['Integrated Security'] = $true }
    }
    if ($Db) { $b['Initial Catalog'] = $Db }
    $b['Application Name'] = 'tahsinERP.DbMigrations'
    if ([int]$b['Connect Timeout'] -eq 15) { $b['Connect Timeout'] = 30 }
    return $b.ConnectionString
}

function Invoke-NonQuery($Conn, $Tran, [string]$Sql) {
    $cmd = $Conn.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = $CommandTimeout
    if ($Tran) { $cmd.Transaction = $Tran }
    [void]$cmd.ExecuteNonQuery()
}
function Invoke-Scalar($Conn, [string]$Sql) {
    $cmd = $Conn.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = $CommandTimeout
    return $cmd.ExecuteScalar()
}

$BootstrapSql = @"
IF OBJECT_ID(N'dbo.SCHEMA_VERSIONS', N'U') IS NULL
CREATE TABLE dbo.SCHEMA_VERSIONS (
    ScriptName  nvarchar(260) NOT NULL CONSTRAINT PK_SCHEMA_VERSIONS PRIMARY KEY,
    Kind        varchar(16)   NOT NULL,          -- baseline | migration | seed | extra
    Version     int           NULL,              -- NNN (baseline = 0)
    Checksum    char(64)      NOT NULL,          -- SHA-256 (UTF-8, LF)
    AppliedAt   datetime2(0)  NOT NULL CONSTRAINT DF_SCHEMA_VERSIONS_AppliedAt DEFAULT (SYSUTCDATETIME()),
    AppliedBy   nvarchar(128) NOT NULL CONSTRAINT DF_SCHEMA_VERSIONS_AppliedBy DEFAULT (SUSER_SNAME()),
    DurationMs  int           NULL,
    Note        nvarchar(400) NULL               -- masalan 'marked: -BaselineExisting'
);
"@

function Get-Plan {
    $plan = New-Object System.Collections.Generic.List[object]
    $baseline = Join-Path $DatabaseRoot 'Schema\000_Baseline.sql'
    if (-not (Test-Path -LiteralPath $baseline)) { throw "Baseline topilmadi: $baseline" }
    $plan.Add([pscustomobject]@{ Name = 'Schema/000_Baseline.sql'; Path = $baseline; Kind = 'baseline'; Version = 0 })

    $migDir = Join-Path $DatabaseRoot 'Migrations'
    $seen = @{}
    if (Test-Path -LiteralPath $migDir) {
        foreach ($f in Get-ChildItem -LiteralPath $migDir -Filter '*.sql' | Sort-Object Name) {
            if ($f.Name -notmatch '^(\d{3})_[A-Za-z0-9_\-]+\.sql$') { throw "Migratsiya nomi noto'g'ri (NNN_nomi.sql bo'lishi kerak): $($f.Name)" }
            $v = [int]$Matches[1]
            if ($v -eq 0) { throw "000 raqami baseline uchun band: $($f.Name)" }
            if ($seen.ContainsKey($v)) { throw "Takroriy migratsiya raqami $($Matches[1]): $($seen[$v]) va $($f.Name)" }
            $seen[$v] = $f.Name
            $plan.Add([pscustomobject]@{ Name = "Migrations/$($f.Name)"; Path = $f.FullName; Kind = 'migration'; Version = $v })
        }
    }
    if ($Seed) {
        $seedDir = Join-Path $DatabaseRoot 'Seed'
        foreach ($f in Get-ChildItem -LiteralPath $seedDir -Filter '*.sql' | Sort-Object Name) {
            $plan.Add([pscustomobject]@{ Name = "Seed/$($f.Name)"; Path = $f.FullName; Kind = 'seed'; Version = $null })
        }
    }
    foreach ($x in $ExtraScripts) {
        $full = (Resolve-Path -LiteralPath $x).Path
        $rel = $full
        if ($full.StartsWith($DatabaseRoot, [StringComparison]::OrdinalIgnoreCase)) { $rel = $full.Substring($DatabaseRoot.Length).TrimStart('\', '/').Replace('\', '/') }
        $plan.Add([pscustomobject]@{ Name = $rel; Path = $full; Kind = 'extra'; Version = $null })
    }
    foreach ($p in $plan) {
        $text = Get-ScriptText $p.Path
        $p | Add-Member -NotePropertyName Text -NotePropertyValue $text
        $p | Add-Member -NotePropertyName Checksum -NotePropertyValue (Get-Checksum $text)
        $p | Add-Member -NotePropertyName Supersedes -NotePropertyValue @([regex]::Matches($text, '(?m)^--\s*@supersedes-checksum:\s*([0-9a-f]{64})\s*$') | ForEach-Object { $_.Groups[1].Value })
    }
    return $plan
}

function Invoke-OneDatabase([string]$Label, [string]$ConnStr, $Plan) {
    $result = [ordered]@{ target = $Label; applied = @(); pending = @(); status = 'ok'; exitCode = $EXIT_OK; error = $null }
    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnStr
    $conn.add_InfoMessage({ param($s, $e) foreach ($m in $e.Errors) { if ($m.Message -and $m.Message -notlike 'Checking constraint:*') { Write-Log 'SQL' ("  " + $m.Message) } } })
    try {
        try { $conn.Open() } catch { $result.status = 'connect-failed'; $result.exitCode = $EXIT_CONNECT; $result.error = $_.Exception.Message; Write-Log 'ERROR' "[$Label] ulanib bo'lmadi: $($_.Exception.Message)"; return $result }

        # Bir bazada bir vaqtda faqat bitta runner (sessiya darajasidagi qulf)
        $lock = Invoke-Scalar $conn "DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'tahsinERP.DbMigrations', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;"
        if ([int]$lock -lt 0) { $result.status = 'locked'; $result.exitCode = $EXIT_LOCKED; $result.error = 'boshqa runner ishlayapti'; Write-Log 'ERROR' "[$Label] bazada boshqa migratsiya ishlayapti (applock)."; return $result }

        $hasVersions = [int](Invoke-Scalar $conn "SELECT CASE WHEN OBJECT_ID(N'dbo.SCHEMA_VERSIONS', N'U') IS NULL THEN 0 ELSE 1 END")
        $userTables = [int](Invoke-Scalar $conn "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'SCHEMA_VERSIONS'")
        if (-not $hasVersions -and -not $DryRun) { Invoke-NonQuery $conn $null $BootstrapSql; $hasVersions = 1 }

        $applied = @{}
        if ($hasVersions) {
            $cmd = $conn.CreateCommand(); $cmd.CommandText = 'SELECT ScriptName, Checksum, Kind FROM dbo.SCHEMA_VERSIONS'
            $rd = $cmd.ExecuteReader()
            while ($rd.Read()) { $applied[$rd.GetString(0)] = @{ Checksum = $rd.GetString(1); Kind = $rd.GetString(2) } }
            $rd.Close()
        }

        $baselineName = 'Schema/000_Baseline.sql'
        $todo = New-Object System.Collections.Generic.List[object]
        foreach ($p in $Plan) {
            if ($applied.ContainsKey($p.Name)) {
                $old = $applied[$p.Name]
                if ($old.Checksum -ne $p.Checksum) {
                    if ($p.Kind -in @('seed', 'extra')) { $todo.Add($p); continue }   # idempotent — qayta ishlaydi
                    if ($p.Kind -eq 'baseline') { continue }                           # baseline faqat yangi bazalar uchun; qayta generatsiya normal
                    if ($p.Supersedes -contains $old.Checksum) { Write-Log 'INFO' "[$Label] $($p.Name): oldingi tahrir bilan qo'llangan (@supersedes-checksum) — o'tkazildi"; continue }
                    $msg = "[$Label] $($p.Name) qo'llanganidan keyin o'zgargan (checksum farq). Qo'llangan migratsiyani tahrir qilmang — yangi NNN fayl yozing."
                    if ($AllowChecksumMismatch) { Write-Log 'WARN' $msg } else { $result.status = 'checksum-mismatch'; $result.exitCode = $EXIT_CHECKSUM; $result.error = $msg; Write-Log 'ERROR' $msg; return $result }
                }
                continue
            }
            if ($p.Kind -eq 'baseline' -and $userTables -gt 0) {
                if ($BaselineExisting) { $p | Add-Member -Force -NotePropertyName MarkOnly -NotePropertyValue $true; $todo.Add($p); continue }
                $msg = "[$Label] bazada $userTables ta jadval bor, lekin baseline qayd etilmagan. Mavjud baza bo'lsa: -BaselineExisting (sxema baseline'ga mosligini avval tekshiring)."
                $result.status = 'not-baselined'; $result.exitCode = $EXIT_NOT_BASELINED; $result.error = $msg; Write-Log 'ERROR' $msg; return $result
            }
            $todo.Add($p)
        }

        if ($todo.Count -eq 0) { Write-Log 'INFO' "[$Label] yangilanish yo'q (hammasi qo'llangan)."; return $result }

        foreach ($p in $todo) {
            $mark = ($p.PSObject.Properties.Name -contains 'MarkOnly')
            $result.pending += $p.Name
            if ($DryRun) { Write-Log 'PLAN' ("[$Label] {0} {1}" -f ($(if ($mark) { 'MARK' } else { 'RUN ' })), $p.Name); continue }

            $sw = [Diagnostics.Stopwatch]::StartNew()
            $tran = $conn.BeginTransaction()
            try {
                if (-not $mark) {
                    Write-Log 'INFO' "[$Label] -> $($p.Name)"
                    $i = 0
                    foreach ($batch in (Split-Batches $p.Text)) {
                        $i++
                        try { Invoke-NonQuery $conn $tran $batch }
                        catch {
                            $ex = $_.Exception; while ($ex.InnerException) { $ex = $ex.InnerException }
                            $head = ($batch.Trim() -split "`n")[0]
                            throw ("batch #{0} ({1}): {2}" -f $i, $head.Substring(0, [Math]::Min(80, $head.Length)), $ex.Message)
                        }
                    }
                }
                $cmd = $conn.CreateCommand(); $cmd.Transaction = $tran
                $cmd.CommandText = @"
MERGE dbo.SCHEMA_VERSIONS AS t USING (SELECT @n AS ScriptName) AS s ON t.ScriptName = s.ScriptName
WHEN MATCHED THEN UPDATE SET Checksum = @c, AppliedAt = SYSUTCDATETIME(), AppliedBy = SUSER_SNAME(), DurationMs = @d, Note = @note
WHEN NOT MATCHED THEN INSERT (ScriptName, Kind, Version, Checksum, DurationMs, Note) VALUES (@n, @k, @v, @c, @d, @note);
"@
                [void]$cmd.Parameters.AddWithValue('@n', $p.Name)
                [void]$cmd.Parameters.AddWithValue('@k', $p.Kind)
                [void]$cmd.Parameters.AddWithValue('@v', $(if ($null -eq $p.Version) { [DBNull]::Value } else { $p.Version }))
                [void]$cmd.Parameters.AddWithValue('@c', $p.Checksum)
                [void]$cmd.Parameters.AddWithValue('@d', [int]$sw.ElapsedMilliseconds)
                [void]$cmd.Parameters.AddWithValue('@note', $(if ($mark) { 'marked: -BaselineExisting' } else { [DBNull]::Value }))
                [void]$cmd.ExecuteNonQuery()
                $tran.Commit()
                $result.applied += $p.Name
                Write-Log 'INFO' ("[$Label]    {0} {1} ({2} ms)" -f ($(if ($mark) { 'belgilandi' } else { 'OK' })), $p.Name, $sw.ElapsedMilliseconds)
            }
            catch {
                try { if ($tran.Connection) { $tran.Rollback() } } catch { }
                $result.status = 'failed'; $result.exitCode = $EXIT_SCRIPT_FAILED; $result.error = "$($p.Name): $($_.Exception.Message)"
                Write-Log 'ERROR' "[$Label] $($p.Name) XATO, tranzaksiya bekor qilindi: $($_.Exception.Message)"
                return $result
            }
        }
        return $result
    }
    finally { $conn.Dispose() }
}

# ------------------------------- main -------------------------------
$script:LogFile = $LogFile
$DatabaseRoot = (Resolve-Path -LiteralPath $DatabaseRoot).Path.TrimEnd('\')
$exit = $EXIT_OK
$results = @()
try {
    if ($AllTenants -and $Database) { Write-Log 'ERROR' '-Database va -AllTenants birga berilmaydi.'; exit $EXIT_BAD_ARGS }
    if (-not $AllTenants -and -not $Database) { Write-Log 'ERROR' '-Database yoki -AllTenants kerak.'; exit $EXIT_BAD_ARGS }

    $plan = @(Get-Plan)
    Write-Log 'INFO' ("Reja: {0} skript ({1}){2}" -f $plan.Count, $DatabaseRoot, $(if ($DryRun) { ' — DRY RUN' } else { '' }))

    $targets = @()
    if ($AllTenants) {
        if (-not $TenantsFile) {
            $TenantsFile = if ($env:TAHSINERP_TENANTS_FILE) { $env:TAHSINERP_TENANTS_FILE } else { Join-Path $env:ProgramData 'tahsinERP\tenants.json' }
        }
        if (-not (Test-Path -LiteralPath $TenantsFile)) { Write-Log 'ERROR' "tenants.json topilmadi: $TenantsFile"; exit $EXIT_BAD_ARGS }
        $reg = Get-Content -LiteralPath $TenantsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $seenDb = @{}
        foreach ($t in @($reg.tenants)) {
            if (-not $t.database) { Write-Log 'ERROR' "tenant '$($t.slug)': database berilmagan"; exit $EXIT_BAD_ARGS }
            if ($t.database -notmatch '^[A-Za-z0-9_\-\.]+$') { Write-Log 'ERROR' "tenant '$($t.slug)': database nomi yaroqsiz"; exit $EXIT_BAD_ARGS }
            $enabled = -not ($t.PSObject.Properties.Name -contains 'enabled' -and $t.enabled -eq $false)
            if (-not $enabled -and $SkipDisabled) { Write-Log 'INFO' "tenant '$($t.slug)' o'chirilgan — o'tkazib yuborildi"; continue }
            $tc = if ($t.PSObject.Properties.Name -contains 'connectionString') { $t.connectionString } else { $null }
            $cs = New-ConnString $t.database $tc
            $key = (New-Object System.Data.SqlClient.SqlConnectionStringBuilder $cs)
            $k = ('{0}|{1}' -f $key.DataSource, $key.InitialCatalog).ToLowerInvariant()
            if ($seenDb.ContainsKey($k)) { continue }   # bitta bazaga bir nechta slug bog'langan bo'lishi mumkin
            $seenDb[$k] = 1
            $targets += [pscustomobject]@{ Label = "$($t.slug)/$($t.database)"; Conn = $cs }
        }
        if ($targets.Count -eq 0) { Write-Log 'WARN' "tenants.json da baza yo'q." }
    }
    else {
        if ($Database -notmatch '^[A-Za-z0-9_\-\.]+$') { Write-Log 'ERROR' "Baza nomi yaroqsiz: $Database"; exit $EXIT_BAD_ARGS }
        $targets += [pscustomobject]@{ Label = $Database; Conn = (New-ConnString $Database $null) }
    }

    foreach ($t in $targets) {
        $r = Invoke-OneDatabase $t.Label $t.Conn $plan
        $results += $r
        # Birinchi xato kodi saqlanadi; qolgan tenantlar davom etadi (bittasi yiqilsa hammasi to'xtamasin)
        if ($r.exitCode -ne $EXIT_OK -and $exit -eq $EXIT_OK) { $exit = $r.exitCode }
    }
}
catch {
    Write-Log 'ERROR' $_.Exception.Message
    if ($exit -eq $EXIT_OK) { $exit = $EXIT_BAD_ARGS }
}

$summary = [ordered]@{ ok = ($exit -eq $EXIT_OK); exitCode = $exit; dryRun = [bool]$DryRun; targets = $results }
Write-Log 'RESULT' ($summary | ConvertTo-Json -Depth 5 -Compress)
exit $exit
