<#
.SYNOPSIS
  tahsinERP boshqaruv panelini Windows serverda IIS saytiga joylaydi (bir martalik o'rnatish va yangilash).

.DESCRIPTION
  Talab: Windows Server + IIS, ASP.NET Core 10 Hosting Bundle, SQL Server. Administrator sifatida ishga tushiring.
  1) Ilovani publish qiladi (yoki -SkipPublish bilan tayyor papkani ishlatadi).
  2) App pool (No Managed Code, ApplicationPoolIdentity) va sayt (https binding, host: admin.<domen>) yaratadi/yangilaydi.
  3) Sirlarni app pool'ning environmentVariables'iga yozadi (applicationHost.config - faqat administratorlar o'qiydi).
     Sirlar argument sifatida emas, yashirin so'rov orqali kiritiladi (shell tarixida qolmaydi).
  4) DataProtection kalitlari va loglar uchun papkalar, app pool identity'ga ruxsat.
  5) migrate buyrug'ini ishga tushiradi.
  Birinchi Owner: o'rnatishdan keyin qo'lda -  dotnet <PhysicalPath>\tahsinERP.ControlPanel.dll create-owner --email ...

.EXAMPLE
  .\Install-PanelIis.ps1 -HostName admin.example.uz -PortalHostName my.example.uz -CertThumbprint 0123ABCD...
  (-PortalHostName - mijoz portali, o'sha ilova va sayt; ikkinchi https binding. Sertifikat ikkala nomni qamrasin, masalan wildcard.)

.EXAMPLE
  .\Install-PanelIis.ps1 -HostName admin.tahsinerp.uz -PortalHostName my.tahsinerp.uz -PublicHostNames tahsinerp.uz,www.tahsinerp.uz -CertThumbprint ...
  (-PublicHostNames - ommaviy mahsulot sahifasi va demo so'rovi; https + http bindinglar, http -> https ilova o'zi yo'naltiradi.
   Sertifikat asosiy domenni ham qamrasin: "tahsinerp.uz,*.tahsinerp.uz".)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $HostName,
    [string] $PortalHostName,   # mijoz portali host'i (my.<domen>); bo'sh - portal ochilmaydi
    [string[]] $PublicHostNames,   # ommaviy sayt (<domen>, www.<domen>); bo'sh - qo'shilmaydi
    [string] $SiteName = 'tahsinERP.ControlPanel',
    [string] $PhysicalPath = 'C:\inetpub\tahsinERP.ControlPanel',
    [string] $DataDir = "$env:ProgramData\tahsinERP\ControlPanel",
    [string] $SigningKeyFile = "$env:ProgramData\tahsinERP\panel-signing-k2.json",
    [string] $ConnectionString = 'Server=.;Database=tahsin_control;Trusted_Connection=True;TrustServerCertificate=True;Application Name=tahsinERP.ControlPanel',
    [string] $CertThumbprint,
    [string] $CertStore = 'WebHosting',   # win-acme sertifikatni WebHosting'ga qo'yadi
    [switch] $SkipPublish,
    [switch] $KeepSecrets,   # yangilashda: mavjud env sirlariga tegmaslik
    # Sirlar fayli (brauzer RDP'da yashirin maydonga joylash ishlamaydi): 1-qator MasterKey, 2-qator imzo paroli,
    # 3-qator Telegram token (ixtiyoriy), 4-qator webhook secret (ixtiyoriy). O'qilgach fayl o'chiriladi.
    [string] $SecretsFile
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
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $pool -HostHeader $HostName -Port 443 -Ssl -SslFlags 1 | Out-Null   # SNI (ERP wildcard bilan bir portda)
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationDefaults.preloadEnabled -Value $true
if ($PortalHostName -and -not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $PortalHostName)) {
    New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $PortalHostName -SslFlags 1 | Out-Null   # SNI
}
foreach ($h in @($PublicHostNames) | Where-Object { $_ }) {
    if (-not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $h)) {
        New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $h -SslFlags 1 | Out-Null   # SNI
    }
    if (-not (Get-WebBinding -Name $SiteName -Protocol http -HostHeader $h)) {
        New-WebBinding -Name $SiteName -Protocol http -Port 80 -HostHeader $h | Out-Null   # http -> https (ilova)
    }
}
if ($CertThumbprint) {
    foreach ($h in @($HostName, $PortalHostName) + @($PublicHostNames) | Where-Object { $_ }) {
        $binding = Get-WebBinding -Name $SiteName -Protocol https -HostHeader $h
        $binding.AddSslCertificate($CertThumbprint, $CertStore)
    }
}

# 3. Sirlar
Set-PoolEnv 'ASPNETCORE_ENVIRONMENT' 'Production'
Set-PoolEnv 'ConnectionStrings__Control' $ConnectionString
Set-PoolEnv 'ControlPanel__Signing__KeyFile' $SigningKeyFile
Set-PoolEnv 'ControlPanel__DataProtection__KeysDir' (Join-Path $DataDir 'keys')
# Host zonalari (06_CUSTOMER_PORTAL.md): xodim sahifalari faqat $HostName, portal faqat $PortalHostName; boshqa host - 404.
Set-PoolEnv 'ControlPanel__Hosts__AdminHost' $HostName
if ($PortalHostName) { Set-PoolEnv 'ControlPanel__Hosts__PortalHost' $PortalHostName; Set-PoolEnv 'ControlPanel__Hosts__PortalPublicUrl' "https://$PortalHostName" }
Set-PoolEnv 'ControlPanel__Hosts__OtherHosts' 'None'
if ($PublicHostNames) { Set-PoolEnv 'ControlPanel__Hosts__PublicHosts' ($PublicHostNames -join ',') }
# Firma ERP manzili https://<slug>.<domen> (provisioning, bot/sayt ulash): admin.<domen> -> <domen>
$baseDomain = $HostName.Substring($HostName.IndexOf('.') + 1)
Set-PoolEnv 'ControlPanel__Hosts__Domain' $baseDomain
Set-PoolEnv 'ControlPanel__Provisioning__BaseDomain' $baseDomain
if (-not $KeepSecrets) {
    if ($SecretsFile) {
        $lines = @(Get-Content $SecretsFile | ForEach-Object { $_.Trim() }) + @('', '', '', '')
        Remove-Item $SecretsFile -Force
        $mk = $lines[0]; $pp = $lines[1]; $tg = $lines[2]; $hook = $lines[3]
    } else {
        Write-Host 'MasterKey: `dotnet tahsinERP.ControlPanel.dll gen-master-key` bilan yaratilgan qiymat (zaxira nusxasi bazadan ALOHIDA saqlansin).'
        $mk = (Read-Secret 'MasterKey').Trim()
        $pp = Read-Secret 'Imzolash kaliti fayli paroli'
        $tg = Read-Secret 'Telegram bot token (bo''sh - faqat log)'
        $hook = Read-Secret 'Telegram webhook secret_token (bo''sh - bot /start bog''lash o''chiq)'
    }
    # Yashirin maydonga joylash (paste) ishlamasa 1 belgi tushadi - shunday qiymatni yozmaymiz
    $mkBytes = $null; try { $mkBytes = [Convert]::FromBase64String($mk) } catch { }
    if (-not $mkBytes -or $mkBytes.Length -ne 32) { throw "MasterKey noto'g'ri (uzunligi $($mk.Length), 44 belgili Base64 kerak). Joylash ishlamagan bo'lsa: -SecretsFile." }
    if ($pp.Length -lt 16) { throw "Imzo paroli juda qisqa ($($pp.Length) belgi). Joylash ishlamagan bo'lsa: -SecretsFile." }
    if ($pp.Contains("'")) { throw "Imzo parolida apostrof (') bo'lmasin (appcmd sintaksisi)." }
    Set-PoolEnv 'ControlPanel__Security__MasterKey' $mk
    Set-PoolEnv 'ControlPanel__Signing__Passphrase' $pp
    if ($tg) { Set-PoolEnv 'ControlPanel__Telegram__BotToken' $tg }
    if ($hook) { Set-PoolEnv 'ControlPanel__Telegram__WebhookSecret' $hook }
}

# 4. Papkalar va ruxsatlar
New-Item -ItemType Directory -Force (Join-Path $DataDir 'keys') | Out-Null
$identity = "IIS AppPool\$pool"
icacls $DataDir /grant "${identity}:(OI)(CI)M" /T | Out-Null
if (Test-Path $SigningKeyFile) { icacls $SigningKeyFile /grant "${identity}:R" | Out-Null }
else { Write-Warning "Imzolash kaliti fayli yo'q: $SigningKeyFile - `keygen --out` bilan yarating (04_PANEL_MVP.md, 3-bo'lim)." }

# 5. Migratsiya (shu sirlar bilan)
$env:ConnectionStrings__Control = $ConnectionString
dotnet (Join-Path $PhysicalPath 'tahsinERP.ControlPanel.dll') migrate
if ($LASTEXITCODE -ne 0) { throw 'migrate xato bilan tugadi (SQL login/ruxsatni tekshiring).' }

# 6. SQL: app pool identity'ga tahsin_control bazasida huquq (skriptni ishga tushirgan administrator SQL sysadmin bo'lishi kerak)
$csb = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
# PowerShell builder'ni lug'at deb ko'radi - qiymatlar kalit nomi bilan ("Initial Catalog")
$dbName = $csb['Initial Catalog']
$csb['Initial Catalog'] = 'master'
$csb['Integrated Security'] = $true
$login = "IIS APPPOOL\$pool"
$sql = @"
IF SUSER_ID(N'$login') IS NULL CREATE LOGIN [$login] FROM WINDOWS;
EXEC(N'USE [$dbName]; IF USER_ID(N''$login'') IS NULL CREATE USER [$login] FOR LOGIN [$login]; ALTER ROLE db_owner ADD MEMBER [$login];');
"@
$cn = New-Object System.Data.SqlClient.SqlConnection $csb.ConnectionString
try {
    $cn.Open(); $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; [void]$cmd.ExecuteNonQuery()
    Write-Host "SQL: [$login] -> $dbName db_owner"
} catch {
    Write-Warning "SQL huquqi berilmadi ($login): $($_.Exception.Message). SSMS'da qo'lda bering."
} finally { $cn.Dispose() }

Start-WebAppPool -Name $pool -ErrorAction SilentlyContinue
Write-Host "Tayyor: https://$HostName/health -> ok bo'lishi kerak (sertifikat bog'langandan keyin)."
