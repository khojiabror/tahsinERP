<#
.SYNOPSIS
    Mavjud tahsinERP bazasining .bak zaxirasini serverda firma (tenant) sifatida tiklaydi: RESTORE -> migratsiya/seed ->
    ERP login'iga huquq -> tenants.json ({slug}.<domen>).

.DESCRIPTION
    Masalan iPack sinov bazasi (demo uchun) yoki boshqa serverdan ko'chirilayotgan mijoz bazasi.
    Administrator PowerShell'da (SQL sysadmin). Baza nomi band bo'lsa - to'xtaydi (-Replace bilan ustiga yoziladi).
    tenants.json'ga qo'shilgach ERP 15 soniyada ko'radi: https://{slug}.<domen>.
    Panel bu firmani bilmaydi - litsenziya uchun panelda shu slug/DbName bilan tenant ochilsa, agent mavjud bazani
    "adopt" qiladi (yaratmaydi) va ro'yxat tokenini yozadi.

.EXAMPLE
    .\Restore-TenantBackup.ps1 -BakFile C:\tahsinERP\DBTHSN-IPACK_20261021.bak -Slug ipack -DbName DB_ipack
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BakFile,
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9-]{1,40}$')] [string] $Slug,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]{1,100}$')] [string] $DbName,
    [string] $SqlServer = '.',
    [string] $DatabaseRoot = 'C:\tahsinERP\Database',
    [string] $TenantsFile = 'C:\ProgramData\tahsinERP\tenants.json',
    [string] $AppLogin = 'IIS APPPOOL\tahsinERP',
    [int] $CompanyId = 0,
    [switch] $Replace
)

$ErrorActionPreference = 'Stop'
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }

$reserved = 'admin','my','pay','www','api','app','mail','smtp','static','cdn','assets','status','panel','erp','bot','sayt','sites'
if ($reserved -contains $Slug) { throw "'$Slug' - band nom (xizmat subdomeni)." }
$BakFile = (Resolve-Path $BakFile).Path

function Open-Sql([string]$db) {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True"
    $cn.Open(); return $cn
}
function Invoke-Sql($cn, [string]$sql, [hashtable]$p = @{}) {
    $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 0
    foreach ($k in $p.Keys) { [void]$cmd.Parameters.AddWithValue($k, $p[$k]) }
    $r = New-Object System.Data.DataTable
    $rd = $cmd.ExecuteReader(); $r.Load($rd); $rd.Dispose()
    return ,$r
}

# ---------------------------------------------------------------------------------------------------------------
Step '1/5 Zaxirani tekshirish'
$master = Open-Sql 'master'
try {
    # SQL xizmati faylni o'qiy olishi uchun (standart instance va Express)
    foreach ($sid in 'NT Service\MSSQLSERVER', 'NT Service\MSSQL$SQLEXPRESS') {
        # mavjud bo'lmagan SID yoki huquq yetmasa - jim o'tkazib yuboriladi (RESTORE o'zi aniq xato beradi)
        cmd /c "icacls `"$BakFile`" /grant `"${sid}:R`" >nul 2>&1"
    }
    [void](Invoke-Sql $master "RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM" @{ '@f' = $BakFile })
    Ok "zaxira yaroqli: $BakFile"
    $files = Invoke-Sql $master "RESTORE FILELISTONLY FROM DISK = @f" @{ '@f' = $BakFile }

    # ---------------------------------------------------------------------------------------------------------------
    Step "2/5 RESTORE -> $DbName"
    $exists = (Invoke-Sql $master "SELECT DB_ID(@d) AS id" @{ '@d' = $DbName }).Rows[0].id
    if ($exists -isnot [DBNull] -and -not $Replace) { throw "$DbName bazasi allaqachon bor. Ustiga yozish uchun -Replace." }
    $paths = (Invoke-Sql $master "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260)) AS d, CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(260)) AS l").Rows[0]
    $moves = @(); $p = @{ '@f' = $BakFile }; $i = 0
    foreach ($row in $files.Rows) {
        $ext = if ($row.Type -eq 'L') { '_log.ldf' } elseif ($i -eq 0) { '.mdf' } else { "_$i.ndf" }
        $dir = if ($row.Type -eq 'L') { $paths.l } else { $paths.d }
        $p["@ln$i"] = $row.LogicalName; $p["@pn$i"] = (Join-Path $dir ($DbName + $ext))
        $moves += "MOVE @ln$i TO @pn$i"; $i++
    }
    if ($exists -isnot [DBNull]) { [void](Invoke-Sql $master "ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE") }
    [void](Invoke-Sql $master "RESTORE DATABASE [$DbName] FROM DISK = @f WITH REPLACE, RECOVERY, $($moves -join ', ')" $p)
    [void](Invoke-Sql $master "ALTER DATABASE [$DbName] SET MULTI_USER")
    Ok "tiklandi: $DbName"
} finally { $master.Dispose() }

# ---------------------------------------------------------------------------------------------------------------
Step '3/5 Migratsiya va seed (ERP relizi bilan tenglashtirish)'
$mig = Join-Path $DatabaseRoot 'tools\Invoke-DbMigrations.ps1'
& powershell -NoProfile -ExecutionPolicy Bypass -File $mig -Server $SqlServer -Database $DbName -DatabaseRoot $DatabaseRoot -Seed
if ($LASTEXITCODE -ne 0) { throw "Invoke-DbMigrations xato (kod $LASTEXITCODE). Kod 5 - baza baseline'siz: -BaselineExisting bilan qo'lda (Database/README.md)." }
Ok 'migratsiya va seed bajarildi'

# ---------------------------------------------------------------------------------------------------------------
Step "4/5 ERP huquqi: [$AppLogin]"
$db = Open-Sql $DbName
try {
    [void](Invoke-Sql $db @"
IF SUSER_ID(@l) IS NULL THROW 50003, 'ERP SQL login serverda yo''q - avval Install-Erp.ps1', 1;
DECLARE @q sysname = QUOTENAME(@l);
IF USER_ID(@l) IS NULL EXEC(N'CREATE USER ' + @q + N' FOR LOGIN ' + @q); ELSE EXEC(N'ALTER USER ' + @q + N' WITH LOGIN = ' + @q);
EXEC(N'ALTER ROLE db_owner ADD MEMBER ' + @q);
SELECT 1 AS ok;
"@ @{ '@l' = $AppLogin })
    Ok "$DbName : [$AppLogin] db_owner"
    $users = (Invoke-Sql $db "SELECT Uname, IsActive FROM dbo.USERS WHERE ISNULL(IsDeleted,0)=0 ORDER BY Uname").Rows
    Write-Host "  foydalanuvchilar: $(($users | ForEach-Object { $_.Uname + $(if ($_.IsActive) { '' } else { '(nofaol)' }) }) -join ', ')"
} finally { $db.Dispose() }

# ---------------------------------------------------------------------------------------------------------------
Step "5/5 tenants.json: $Slug -> $DbName"
$t = Get-Content -Raw $TenantsFile | ConvertFrom-Json
$list = @($t.tenants | Where-Object { $_ -and $_.slug -ne $Slug })
$entry = [ordered]@{ slug = $Slug; database = $DbName; enabled = $true }
if ($CompanyId -gt 0) { $entry.companyId = $CompanyId }
$list += [pscustomobject]$entry
$out = [ordered]@{ baseDomain = $t.baseDomain; tenants = $list }
$json = $out | ConvertTo-Json -Depth 5
$tmp = "$TenantsFile.tmp"
[IO.File]::WriteAllText($tmp, $json, (New-Object Text.UTF8Encoding($false)))
Move-Item -Force $tmp $TenantsFile
Ok "https://$Slug.$($t.baseDomain)  (ERP 15 soniyada ko'radi)"

Write-Host @"

Eslatma:
  - Bazadagi foydalanuvchilar va parollar o'zgarmadi. Internetga ochiq bo'ladi - keraksizlarini ERP'da o'chiring
    yoki parollarini almashtiring.
  - Litsenziya: eski kalit boshqa o'rnatishga bog'langan bo'lishi mumkin - panelda shu slug ($Slug) va DbName ($DbName)
    bilan tenant ochib, ERP -> Litsenziya -> "Panelga ulash".
"@
