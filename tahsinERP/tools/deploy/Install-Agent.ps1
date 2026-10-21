<#
.SYNOPSIS
    Provisioning agentini (Windows Service) o'rnatadi yoki yangilaydi (Docs/Deploy/01_DEPLOY_GUIDE.md, 6-bo'lim).

.DESCRIPTION
    Oldin: panelda Serverlar -> + Server -> "Agent kalitini chiqarish" (Owner) - Server ID va kalit bir marta ko'rsatiladi.
    Paket: app\ (agent publish) shu skript yonida. Administrator PowerShell'da.
    Sirlar (agent kaliti, demo paroli) xizmat muhitiga (registr, faqat administratorlar) yoziladi - fayllarga emas.
    Yangilash: -KeepSecrets (appsettings.json va sirlar saqlanadi, faqat fayllar yangilanadi).

.EXAMPLE
    .\Install-Agent.ps1 -Domain tahsinerp.uz -ServerId 1
    .\Install-Agent.ps1 -Domain tahsinerp.uz -ServerId 1 -KeepSecrets     # yangilash
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Domain,
    [Parameter(Mandatory)] [int] $ServerId,
    [string] $SourceDir = (Join-Path $PSScriptRoot 'app'),
    [string] $InstallDir = 'C:\tahsinERP\Agent',
    [string] $SqlServer = '.',
    [string] $DatabaseRoot = 'C:\tahsinERP\Database',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $ServiceName = 'tahsinERP.ProvisioningAgent',
    [switch] $KeepSecrets
)

$ErrorActionPreference = 'Stop'
$Domain = $Domain.Trim().Trim('.').ToLowerInvariant()
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }
function Skip($t) { Write-Host "  [--] $t" -ForegroundColor DarkGray }
function Read-Secret([string]$prompt) {
    $s = Read-Host -AsSecureString $prompt
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator sifatida ishga tushiring.' }
$exe = 'tahsinERP.ProvisioningAgent.exe'
if (-not (Test-Path (Join-Path $SourceDir $exe))) { throw "Agent paketi topilmadi: $SourceDir\$exe" }
if (-not (Test-Path (Join-Path $DatabaseRoot 'tools\New-TenantDb.ps1'))) { throw "Database papkasi topilmadi: $DatabaseRoot (avval Install-Erp.ps1)" }

# ---------------------------------------------------------------------------------------------------------------
Step '1/5 Fayllar'
$svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; Start-Sleep -Seconds 2 }
$settingsPath = Join-Path $InstallDir 'appsettings.json'
$hadSettings = Test-Path $settingsPath
$xf = @(); if ($hadSettings -and $KeepSecrets) { $xf = @('/XF', 'appsettings.json') }
robocopy $SourceDir $InstallDir /E /R:2 /W:2 /NFL /NDL /NJH /NP @xf | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Agent fayllari ko'chirilmadi (robocopy $LASTEXITCODE)." }
Ok "agent -> $InstallDir"

# ---------------------------------------------------------------------------------------------------------------
Step '2/5 appsettings.json'
if ($hadSettings -and $KeepSecrets) {
    Skip 'appsettings.json saqlandi (-KeepSecrets)'
} else {
    $cfg = Get-Content -Raw $settingsPath | ConvertFrom-Json
    $cfg.Panel.Url = "https://admin.$Domain"
    $cfg.Panel.ServerId = $ServerId
    $cfg.Panel.Secret = ''                       # faqat xizmat muhitida
    $cfg.Sql.Server = $SqlServer
    $cfg.Sql.AppLogin = 'IIS APPPOOL\tahsinERP'
    $cfg.Paths.DatabaseRoot = $DatabaseRoot
    $cfg.Paths.BaseDomain = $Domain
    $cfg.Paths.BackupDir = Join-Path $DataDir 'Backups'
    $cfg.Paths.ArchiveDir = Join-Path $DataDir 'Archive'
    $cfg.Paths.StateDir = Join-Path $DataDir 'Agent'
    $cfg.Demo.UserPassword = ''                  # faqat xizmat muhitida
    [IO.File]::WriteAllText($settingsPath, ($cfg | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
    Ok "Panel=https://admin.$Domain, ServerId=$ServerId, Sql=$SqlServer, DatabaseRoot=$DatabaseRoot, BaseDomain=$Domain"
}

# ---------------------------------------------------------------------------------------------------------------
Step '3/5 Papkalar va huquqlar'
foreach ($d in 'Backups', 'Archive', 'Agent') { New-Item -ItemType Directory -Force (Join-Path $DataDir $d) | Out-Null }
icacls $InstallDir /grant 'NT AUTHORITY\NETWORK SERVICE:(OI)(CI)RX' | Out-Null
icacls $DataDir /grant 'NT AUTHORITY\NETWORK SERVICE:(OI)(CI)M' | Out-Null
icacls $DatabaseRoot /grant 'NT AUTHORITY\NETWORK SERVICE:(OI)(CI)RX' | Out-Null
foreach ($sid in 'NT Service\MSSQLSERVER', 'NT Service\MSSQL$SQLEXPRESS') {
    cmd /c "icacls `"$DataDir\Backups`" /grant `"${sid}:(OI)(CI)M`" >nul 2>&1"
    cmd /c "icacls `"$DataDir\Archive`" /grant `"${sid}:(OI)(CI)M`" >nul 2>&1"
}
Ok 'NETWORK SERVICE: agent - o''qish, ProgramData\tahsinERP - yozish; SQL xizmati: Backups/Archive'

# ---------------------------------------------------------------------------------------------------------------
Step '4/5 Windows Service'
$bin = Join-Path $InstallDir $exe
if (-not $svc) {
    sc.exe create $ServiceName binPath= "`"$bin`"" start= delayed-auto obj= 'NT AUTHORITY\NETWORK SERVICE' DisplayName= 'tahsinERP Provisioning Agent' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Xizmat yaratilmadi (sc.exe $LASTEXITCODE)." }
    Ok "xizmat yaratildi: $ServiceName"
} else { Skip "xizmat bor: $ServiceName" }
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
sc.exe description $ServiceName 'tahsinERP: firma bazalarini yaratish, demo tiklash, faollik (panel admin.<domen> bilan)' | Out-Null

if (-not $KeepSecrets) {
    $secret = Read-Secret 'Agent kaliti (panel: Serverlar -> Agent kalitini chiqarish)'
    if (-not $secret) { throw 'Agent kaliti bo''sh.' }
    $demo = Read-Secret 'Demo foydalanuvchi paroli (demo firma uchun; bo''sh - Enter)'
    $envList = @("TAHSIN_AGENT_Panel__Secret=$secret")
    if ($demo) { $envList += "TAHSIN_AGENT_Demo__UserPassword=$demo" }
    New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -PropertyType MultiString -Value $envList -Force | Out-Null
    Ok 'sirlar xizmat muhitiga yozildi'
} else { Skip 'sirlar saqlandi (-KeepSecrets)' }

# ---------------------------------------------------------------------------------------------------------------
Step '5/5 Ishga tushirish'
Start-Service $ServiceName
Start-Sleep -Seconds 5
$st = (Get-Service $ServiceName).Status
Write-Host "  xizmat: $st"
Write-Host "  Loglar: Event Viewer -> Windows Logs -> Application (manba: $ServiceName)"
Write-Host "  Panelda: Serverlar -> shu server 'oxirgi aloqa' yangilanishi kerak (DNS va sertifikat tayyor bo'lsa)."
