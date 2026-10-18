<#
.SYNOPSIS
  tahsinERP boshqaruv panelini Windows serverda IIS saytiga joylaydi (bir martalik o'rnatish va yangilash).

.DESCRIPTION
  Talab: Windows Server + IIS, ASP.NET Core 10 Hosting Bundle, SQL Server. Administrator sifatida ishga tushiring.
  1) Ilovani publish qiladi (yoki -SkipPublish bilan tayyor papkani ishlatadi).
  2) App pool (No Managed Code, ApplicationPoolIdentity) va sayt (https binding, host: admin.<domen>) yaratadi/yangilaydi.
  3) Sirlarni app pool'ning environmentVariables'iga yozadi (applicationHost.config — faqat administratorlar o'qiydi).
     Sirlar argument sifatida emas, yashirin so'rov orqali kiritiladi (shell tarixida qolmaydi).
  4) DataProtection kalitlari va loglar uchun papkalar, app pool identity'ga ruxsat.
  5) migrate buyrug'ini ishga tushiradi.
  Birinchi Owner: o'rnatishdan keyin qo'lda —  dotnet <PhysicalPath>\tahsinERP.ControlPanel.dll create-owner --email ...

.EXAMPLE
  .\Install-PanelIis.ps1 -HostName admin.example.uz -CertThumbprint 0123ABCD...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $HostName,
    [string] $SiteName = 'tahsinERP.ControlPanel',
    [string] $PhysicalPath = 'C:\inetpub\tahsinERP.ControlPanel',
    [string] $DataDir = "$env:ProgramData\tahsinERP\ControlPanel",
    [string] $SigningKeyFile = "$env:ProgramData\tahsinERP\panel-signing-k2.json",
    [string] $ConnectionString = 'Server=.;Database=tahsin_control;Trusted_Connection=True;TrustServerCertificate=True;Application Name=tahsinERP.ControlPanel',
    [string] $CertThumbprint,
    [switch] $SkipPublish,
    [switch] $KeepSecrets   # yangilashda: mavjud env sirlariga tegmaslik
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
$pool = $SiteName

function Read-Secret([string] $prompt) {
    $s = Read-Host -AsSecureString $prompt
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}

function Set-PoolEnv([string] $name, [string] $value) {
    & $appcmd set config -section:system.applicationHost/applicationPools "/-[name='$pool'].environmentVariables.[name='$name']" /commit:apphost 2>$null | Out-Null
    & $appcmd set config -section:system.applicationHost/applicationPools "/+[name='$pool'].environmentVariables.[name='$name',value='$value']" /commit:apphost | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "environmentVariables yozilmadi: $name" }
}

# 1. Publish
if (-not $SkipPublish) {
    $project = Join-Path $PSScriptRoot '..\tahsinERP.ControlPanel.csproj'
    if (Test-Path "IIS:\AppPools\$pool") { Stop-WebAppPool -Name $pool -ErrorAction SilentlyContinue }
    dotnet publish $project -c Release -o $PhysicalPath
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish xato bilan tugadi.' }
}

# 2. App pool va sayt
if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.loadUserProfile -Value $true
Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'   # kunlik fon ishi to'xtamasin
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $pool -HostHeader $HostName -Port 443 -Ssl | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationDefaults.preloadEnabled -Value $true
if ($CertThumbprint) {
    $binding = Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName
    $binding.AddSslCertificate($CertThumbprint, 'My')
}

# 3. Sirlar
Set-PoolEnv 'ASPNETCORE_ENVIRONMENT' 'Production'
Set-PoolEnv 'ConnectionStrings__Control' $ConnectionString
Set-PoolEnv 'ControlPanel__Signing__KeyFile' $SigningKeyFile
Set-PoolEnv 'ControlPanel__DataProtection__KeysDir' (Join-Path $DataDir 'keys')
if (-not $KeepSecrets) {
    Write-Host 'MasterKey: `dotnet tahsinERP.ControlPanel.dll gen-master-key` bilan yaratilgan qiymat (zaxira nusxasi bazadan ALOHIDA saqlansin).'
    Set-PoolEnv 'ControlPanel__Security__MasterKey' (Read-Secret 'MasterKey')
    Set-PoolEnv 'ControlPanel__Signing__Passphrase' (Read-Secret 'Imzolash kaliti fayli paroli')
    $tg = Read-Secret 'Telegram bot token (bo''sh — faqat log)'
    if ($tg) { Set-PoolEnv 'ControlPanel__Telegram__BotToken' $tg }
}

# 4. Papkalar va ruxsatlar
New-Item -ItemType Directory -Force (Join-Path $DataDir 'keys') | Out-Null
$identity = "IIS AppPool\$pool"
icacls $DataDir /grant "${identity}:(OI)(CI)M" /T | Out-Null
if (Test-Path $SigningKeyFile) { icacls $SigningKeyFile /grant "${identity}:R" | Out-Null }
else { Write-Warning "Imzolash kaliti fayli yo'q: $SigningKeyFile — `keygen --out` bilan yarating (04_PANEL_MVP.md, 3-bo'lim)." }

# 5. Migratsiya (shu sirlar bilan)
$env:ConnectionStrings__Control = $ConnectionString
dotnet (Join-Path $PhysicalPath 'tahsinERP.ControlPanel.dll') migrate
if ($LASTEXITCODE -ne 0) { throw 'migrate xato bilan tugadi (SQL login/ruxsatni tekshiring).' }

Start-WebAppPool -Name $pool -ErrorAction SilentlyContinue
Write-Host "Tayyor: https://$HostName/health → ok bo'lishi kerak. App pool identity ($identity) SQL'da tahsin_control bazasiga db_owner (migratsiya) yoki db_datareader+db_datawriter bo'lsin."
