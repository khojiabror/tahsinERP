<#
.SYNOPSIS
    Kunlik zaxirani o'rnatadi: Backup-Databases.ps1 + sozlama + bucket kaliti (DPAPI) + Task Scheduler (SYSTEM) (Docs/Deploy/04_BACKUP.md).

.DESCRIPTION
    Bucket kaliti fayldan o'qiladi (brauzer-RDP'da paste ishlamaydi): Notepad'da 2 qator - Access key ID, Secret access key.
    Kalit DPAPI (LocalMachine) bilan shifrlanib C:\ProgramData\tahsinERP\Backup\s3.key ga yoziladi (faqat SYSTEM va
    Administrators o'qiydi), kalit fayli o'chiriladi. Zaxira papkasiga SQL Server xizmati yozadi. Administrator PowerShell'da.
    Qayta ishga tushirish xavfsiz: -SecretsFile berilmasa mavjud kalit saqlanadi. Faqat ASCII (PowerShell 5.1).

.EXAMPLE
    .\Install-Backup.ps1 -Bucket tahsinerp-backup -SecretsFile C:\tahsinERP\s3.txt -RunNow
    .\Install-Backup.ps1 -Bucket tahsinerp-backup -At 02:00 -KeepDays 30
#>
[CmdletBinding()]
param(
    [string] $Bucket,            # bo'sh - faqat lokal zaxira (bepul, lekin server yo'qolsa nusxa ham yo'qoladi)
    [string] $Region = 'eu-central-1',
    [string] $Prefix = 'sql',
    [string] $SecretsFile,
    [string] $At = '01:30',
    [int] $KeepDays = 14,
    [int] $KeepMonthlyDays = 180,
    [int] $LocalKeepDays = 0,    # 0 - avtomatik: bucket bo'lsa 2 kun, faqat lokal bo'lsa 7 kun
    [string] $SqlServer = '.',
    [string] $InstallDir = 'C:\tahsinERP\Backup',
    [string] $LocalDir = 'C:\tahsinERP\Backups',
    [string] $ConfigDir = 'C:\ProgramData\tahsinERP\Backup',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $TaskName = 'tahsinERP Backup',
    [switch] $RunNow
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }
function Skip($t) { Write-Host "  [--] $t" -ForegroundColor DarkGray }
function Lock-Dir([string]$path, [string[]]$extra) {
    New-Item -ItemType Directory -Force $path | Out-Null
    $a = @($path, '/inheritance:r', '/grant:r', '*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F')
    foreach ($e in $extra) { $a += '/grant:r'; $a += $e }
    & icacls @a | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls: $path" }
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator sifatida ishga tushiring.' }
$Bucket = ([string]$Bucket).Trim().ToLowerInvariant()
$useS3 = [bool]$Bucket
if ($useS3 -and $Bucket -notmatch '^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$') { throw "Bucket nomi noto'g'ri: $Bucket" }
if ($LocalKeepDays -le 0) { $LocalKeepDays = if ($useS3) { 2 } else { 7 } }
if ($At -notmatch '^\d{1,2}:\d{2}$') { throw "-At HH:mm ko'rinishida bo'lsin" }
$src = Join-Path $PSScriptRoot 'Backup-Databases.ps1'
if (-not (Test-Path $src)) { throw "Topilmadi: $src" }

# ---------------------------------------------------------------------------------------------------------------
Step '1/5 Papkalar va huquqlar'
Lock-Dir $InstallDir @()
Lock-Dir $ConfigDir @()
# SQL Server xizmati .bak ni shu papkaga yozadi
$sqlSvc = (Get-Service | Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' } | Select-Object -First 1).Name
if (-not $sqlSvc) { throw 'SQL Server xizmati topilmadi.' }
$sqlAccount = if ($sqlSvc -eq 'MSSQLSERVER') { 'NT SERVICE\MSSQLSERVER' } else { "NT SERVICE\$sqlSvc" }
Lock-Dir $LocalDir @("${sqlAccount}:(OI)(CI)M")
Copy-Item $src (Join-Path $InstallDir 'Backup-Databases.ps1') -Force
Ok "skript: $InstallDir; nusxalar: $LocalDir (SQL: $sqlAccount); sozlama: $ConfigDir"

# ---------------------------------------------------------------------------------------------------------------
Step '2/5 Sozlama'
$cfg = [ordered]@{
    Bucket = $Bucket; Region = $Region; Prefix = $Prefix.Trim('/'); SqlServer = $SqlServer
    LocalDir = $LocalDir; DataDir = $DataDir; KeepDays = $KeepDays; KeepMonthlyDays = $KeepMonthlyDays; LocalKeepDays = $LocalKeepDays
}
$cfg | ConvertTo-Json | Set-Content -Path (Join-Path $ConfigDir 'config.json') -Encoding ASCII
if ($useS3) { Ok "s3://$Bucket/$($cfg.Prefix)/ ($Region); bucket'da $KeepDays kun (oyning 1-kuni $KeepMonthlyDays kun), serverda $LocalKeepDays kun" }
else { Ok "faqat lokal: $LocalDir, $LocalKeepDays kun (server yo'qolsa nusxa ham yo'qoladi - vaqti-vaqti bilan o'z kompyuteringizga ko'chiring)" }

# ---------------------------------------------------------------------------------------------------------------
Step '3/5 Bucket kaliti'
$keyFile = Join-Path $ConfigDir 's3.key'
if ($SecretsFile) {
    if (-not (Test-Path $SecretsFile)) { throw "Topilmadi: $SecretsFile" }
    $lines = @(Get-Content $SecretsFile | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    Remove-Item $SecretsFile -Force
    if ($lines.Count -lt 2) { throw 'Faylda 2 qator bo''lsin: Access key ID va Secret access key.' }
    if ($lines[0] -notmatch '^[A-Z0-9]{16,128}$') { throw "Access key ID noto'g'ri (uzunligi $($lines[0].Length))." }
    if ($lines[1].Length -lt 30) { throw "Secret access key juda qisqa (uzunligi $($lines[1].Length)) - nusxa to'liq tushmagan." }
    $plain = [Text.Encoding]::UTF8.GetBytes($lines[0] + "`n" + $lines[1])
    [IO.File]::WriteAllBytes($keyFile, [Security.Cryptography.ProtectedData]::Protect($plain, $null, 'LocalMachine'))
    Ok "kalit shifrlab yozildi (ID uzunligi $($lines[0].Length), secret $($lines[1].Length)); $SecretsFile o'chirildi"
} elseif (-not $useS3) { Skip 'bucket yo''q - kalit kerak emas' }
elseif (Test-Path $keyFile) { Skip 'mavjud kalit saqlandi' }
else { throw '-SecretsFile kerak (birinchi o''rnatish).' }

# ---------------------------------------------------------------------------------------------------------------
Step '4/5 Imzo va bucket tekshiruvi'
$script = Join-Path $InstallDir 'Backup-Databases.ps1'
& powershell -NoProfile -ExecutionPolicy Bypass -File $script -SelfTest
if ($LASTEXITCODE -ne 0) { throw 'SigV4 o''z-o''zini tekshirish o''tmadi.' }
if ($useS3) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $script -List -ConfigDir $ConfigDir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Bucket'ga ulanib bo'lmadi: nom, hudud ($Region) va kalitni tekshiring." }
    Ok "bucket'ga ulanildi"
} else { Skip 'bucket tekshiruvi yo''q (faqat lokal)' }

# ---------------------------------------------------------------------------------------------------------------
Step '5/5 Task Scheduler'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 4) -MultipleInstances IgnoreNew
$sys = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $sys -Force | Out-Null
Ok "'$TaskName' har kuni $At da (SYSTEM)"

if ($RunNow) {
    Write-Host "`nHozir sinov zaxirasi ishga tushirilmoqda..."
    Start-ScheduledTask -TaskName $TaskName
    do { Start-Sleep -Seconds 5 } while ((Get-ScheduledTask -TaskName $TaskName).State -eq 'Running')
    $last = Get-Content (Join-Path $ConfigDir 'last.json') -Raw | ConvertFrom-Json
    if ($last.Ok) { Ok ("zaxira tayyor: " + $last.Folder + " (" + @($last.Files).Count + " fayl)") }
    else { Write-Host '  [!] xatolar:' -ForegroundColor Red; $last.Errors | ForEach-Object { Write-Host "      $_" } }
}
Write-Host @"

Tekshirish:  Get-Content $ConfigDir\last.json
Ro'yxat:     & '$script' -List
Tiklash:     & '$script' -Download <yyyy-MM-dd> -Database DB_firma   -> C:\tahsinERP\restore\DB_firma.bak
             keyin: .\Restore-TenantBackup.ps1 -BakFile C:\tahsinERP\restore\DB_firma.bak -Slug firma -DbName DB_firma -Replace
"@
