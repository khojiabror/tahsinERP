<#
.SYNOPSIS
    tahsinERP.Updater (LocalSystem Windows Service) ni o'rnatadi yoki yangilaydi (Docs/Deploy/03_AUTO_UPDATE.md).

.DESCRIPTION
    Updater paneldan bosilgan yangilashlarni bajaradi: agent (NETWORK SERVICE) imzolangan paketni Updates\Inbox ga qo'yadi,
    Updater imzo va SHA-256 ni QAYTA tekshiradi, zaxira oladi, pool/xizmatni to'xtatib fayllarni almashtiradi, migratsiya,
    health va xato bo'lsa rollback. Updater tarmoqqa chiqmaydi va sir saqlamaydi.

    Huquqlar (minimal):
      * Updater papkasi, Releases, _backup, Updater\state.json - faqat SYSTEM va Administrators yozadi
        (LocalSystem ishga tushiradigan fayllarni boshqa hisob o'zgartira olmasin);
      * Updates\Inbox va Updates\Status - agent (NETWORK SERVICE) yozadi; Updater\state.json - agent o'qiydi;
      * SQL: NT AUTHORITY\SYSTEM - sysadmin (baza zaxirasi/tiklash, firma pool login'i).
    Paket: app\ (Updater publish) shu skript yonida. Administrator PowerShell'da. Faqat ASCII (PowerShell 5.1).
    Updater o'zini yangilamaydi - yangi versiya chiqsa shu skript bilan qayta (sozlama saqlanadi).

.EXAMPLE
    .\Install-Updater.ps1 -Domain tahsinerp.uz
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Domain,
    [string] $SourceDir = (Join-Path $PSScriptRoot 'app'),
    [string] $InstallDir = 'C:\tahsinERP\Updater',
    [string] $SqlServer = '.',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $ReleasesDir = 'C:\tahsinERP\releases',
    [string] $BackupRoot = 'C:\inetpub\_backup',
    [string] $ServiceName = 'tahsinERP.Updater',
    [switch] $Reconfigure
)

$ErrorActionPreference = 'Stop'
$Domain = $Domain.Trim().Trim('.').ToLowerInvariant()
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }
function Skip($t) { Write-Host "  [--] $t" -ForegroundColor DarkGray }
function Lock-Dir([string]$path, [string[]]$extra) {
    New-Item -ItemType Directory -Force $path | Out-Null
    $args2 = @($path, '/inheritance:r', '/grant:r', '*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F')
    foreach ($e in $extra) { $args2 += '/grant:r'; $args2 += $e }
    & icacls @args2 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls: $path" }
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator sifatida ishga tushiring.' }
$exe = 'tahsinERP.Updater.exe'
if (-not (Test-Path (Join-Path $SourceDir $exe))) { throw "Updater paketi topilmadi: $SourceDir\$exe" }

# ---------------------------------------------------------------------------------------------------------------
Step '1/5 Fayllar'
$svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; Start-Sleep -Seconds 2 }
Lock-Dir $InstallDir @()
$settingsPath = Join-Path $InstallDir 'appsettings.json'
$hadSettings = Test-Path $settingsPath
$xf = @(); if ($hadSettings -and -not $Reconfigure) { $xf = @('/XF', 'appsettings.json') }
robocopy $SourceDir $InstallDir /E /R:2 /W:2 /NFL /NDL /NJH /NP @xf | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Updater fayllari ko'chirilmadi (robocopy $LASTEXITCODE)." }
Ok "updater -> $InstallDir (faqat SYSTEM/Administrators yozadi)"

# ---------------------------------------------------------------------------------------------------------------
Step '2/5 appsettings.json'
if ($hadSettings -and -not $Reconfigure) { Skip 'appsettings.json saqlandi (-Reconfigure bilan qayta yoziladi)' }
else {
    $cfg = Get-Content -Raw $settingsPath | ConvertFrom-Json
    $cfg.Domain = $Domain
    $cfg.SqlServer = $SqlServer
    [IO.File]::WriteAllText($settingsPath, ($cfg | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
    Ok "Domain=$Domain, SqlServer=$SqlServer"
}

# ---------------------------------------------------------------------------------------------------------------
Step '3/5 Papkalar va huquqlar'
$ns = '*S-1-5-20'   # NT AUTHORITY\NETWORK SERVICE (agent)
$updates = Join-Path $DataDir 'Updates'
Lock-Dir $updates @("${ns}:RX")
Lock-Dir (Join-Path $updates 'Inbox') @("${ns}:(OI)(CI)M")
Lock-Dir (Join-Path $updates 'Status') @("${ns}:(OI)(CI)M")
Lock-Dir (Join-Path $updates 'Done') @()
Lock-Dir (Join-Path $DataDir 'Updater') @("${ns}:(OI)(CI)RX")
Lock-Dir $ReleasesDir @()
Lock-Dir $BackupRoot @()
New-Item -ItemType Directory -Force 'C:\inetpub\erp' | Out-Null
Ok 'Inbox/Status - agent yozadi; Updater holati - agent o''qiydi; Releases/_backup - faqat SYSTEM/Administrators'

# ---------------------------------------------------------------------------------------------------------------
Step '4/5 SQL: NT AUTHORITY\SYSTEM'
try {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=master;Integrated Security=True;TrustServerCertificate=True"
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = "IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];"
    [void]$cmd.ExecuteNonQuery()
    $cn.Dispose()
    Ok 'NT AUTHORITY\SYSTEM - sysadmin (baza zaxirasi/tiklash, firma pool login''lari)'
} catch { Write-Warning "SQL huquqi berilmadi: $($_.Exception.Message)" }

# ---------------------------------------------------------------------------------------------------------------
Step '5/5 Windows Service'
$bin = Join-Path $InstallDir $exe
if (-not $svc) {
    sc.exe create $ServiceName binPath= "`"$bin`"" start= delayed-auto obj= LocalSystem DisplayName= 'tahsinERP Updater' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Xizmat yaratilmadi (sc.exe $LASTEXITCODE)." }
    Ok "xizmat yaratildi: $ServiceName (LocalSystem)"
} else { Skip "xizmat bor: $ServiceName" }
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
sc.exe description $ServiceName 'tahsinERP: paneldan yangilashlarni o''rnatadi (imzolangan reliz, zaxira, rollback)' | Out-Null
Start-Service $ServiceName
Start-Sleep -Seconds 3
Write-Host "  xizmat: $((Get-Service $ServiceName).Status)"
Write-Host @"

Keyingi qadamlar:
  - Panel -> Yangilanishlar: 'Updater' yonida versiya chiqishi kerak (agent 5 daqiqagacha inventar yuboradi).
  - Mavjud firmalarni alohida instance'larga ajratish (bir martalik): & '$bin' split-erp
    (yoki panelda har firma uchun 'Instance yaratish' - relizdan).
  - Holat: & '$bin' status ;  loglar: Event Viewer -> Application (manba: $ServiceName)
"@
