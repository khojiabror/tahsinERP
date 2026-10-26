<#
.SYNOPSIS
    Telegram bot (bot.<domen>) va sayt-katalog ({firma}.sayt.<domen>) ni IIS'ga o'rnatadi yoki yangilaydi (01_DEPLOY_GUIDE.md, 7-bo'lim).

.DESCRIPTION
    Paket: bot\ va site\ (ASP.NET Core 10 publish) shu skript yonida. Administrator PowerShell'da. Avval Install-Erp.ps1.
    * Bitta master kalit (>=32 belgi) ERP (TAHSINERP_BOT_KEY), bot (Bot__MasterKey) va sayt (Site__MasterKey) pool'lariga
      yoziladi. Birinchi o'rnatishda ERP'da bor bo'lsa o'sha olinadi, bo'lmasa tasodifiy yaratiladi - qo'lda tarqatish shart emas.
    * tahsin_bot va tahsin_site bazalari administrator sifatida yaratiladi; ilova pool'lariga faqat o'z bazasida db_owner.
    * Panel pool'iga ControlPanel__Apps__* yoziladi: firma botini/saytini panelning firma sahifasidan ulash.
    * Zaxira yo'l - bot/sayt buyruqlari (tenant add, webhook set): Invoke-AppCli.ps1 (kalitni pool'dan o'qiydi).
    Yangilash: -KeepSecrets.

.EXAMPLE
    .\Install-BotSite.ps1 -Domain tahsinerp.uz
    .\Install-BotSite.ps1 -Domain tahsinerp.uz -KeepSecrets -CertThumbprint <*.tahsinerp.uz> -SaytCertThumbprint <*.sayt.tahsinerp.uz>
    .\Install-BotSite.ps1 -Domain tahsinerp.uz -KeepSecrets -SiteServerIp 63.183.64.107   # firma o'z domeni uchun DNS tekshiruvi
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Domain,
    [string] $BotSource = (Join-Path $PSScriptRoot 'bot'),
    [string] $SiteSource = (Join-Path $PSScriptRoot 'site'),
    [string] $SqlServer = '.',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $ErpPool = 'tahsinERP',
    [string] $PanelPool = 'tahsinERP.ControlPanel',
    [string] $CertThumbprint,
    [string] $SaytCertThumbprint,
    # Sayt serverining ochiq IP manzili: panel firmaning o'z domeni A yozuvini shunga solishtiradi (09_SITE_CUSTOM_DOMAIN.md)
    [string] $SiteServerIp,
    [switch] $KeepSecrets
)

$ErrorActionPreference = 'Stop'
$Domain = $Domain.Trim().Trim('.').ToLowerInvariant()
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }
function Skip($t) { Write-Host "  [--] $t" -ForegroundColor DarkGray }

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator sifatida ishga tushiring.' }
if (-not (Test-Path (Join-Path $BotSource 'tahsinERP.Bot.dll'))) { throw "Bot paketi topilmadi: $BotSource" }
if (-not (Test-Path (Join-Path $SiteSource 'tahsinERP.Site.dll'))) { throw "Sayt paketi topilmadi: $SiteSource" }
Import-Module WebAdministration

function Get-PoolEnv([string]$pool, [string]$name) {
    $e = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.applicationHost/applicationPools/add[@name='$pool']/environmentVariables/add[@name='$name']"
    if ($e) { return $e.value } else { return $null }
}
function Set-PoolEnv([string]$pool, [string]$name, [string]$value) {
    & $appcmd set config -section:system.applicationHost/applicationPools "/-[name='$pool'].environmentVariables.[name='$name']" /commit:apphost 2>$null | Out-Null
    & $appcmd set config -section:system.applicationHost/applicationPools "/+[name='$pool'].environmentVariables.[name='$name',value='$value']" /commit:apphost | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "environmentVariables yozilmadi: $pool / $name" }
}
function Invoke-Sql([string]$db, [string]$sql) {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True"
    try { $cn.Open(); $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120; [void]$cmd.ExecuteNonQuery() }
    finally { $cn.Dispose() }
}

$apps = @(
    @{ Name = 'tahsinERP.Bot';  Source = $BotSource;  Path = 'C:\inetpub\tahsinERP.Bot';  Host = "bot.$Domain";    Db = 'tahsin_bot';  Cert = $CertThumbprint;     Data = (Join-Path $DataDir 'Bot') },
    @{ Name = 'tahsinERP.Site'; Source = $SiteSource; Path = 'C:\inetpub\tahsinERP.Site'; Host = "*.sayt.$Domain"; Db = 'tahsin_site'; Cert = $SaytCertThumbprint; Data = (Join-Path $DataDir 'Site') }
)

# ---------------------------------------------------------------------------------------------------------------
Step '1/5 Fayllar'
foreach ($a in $apps) {
    if (Test-Path "IIS:\AppPools\$($a.Name)") { Stop-WebAppPool -Name $a.Name -ErrorAction SilentlyContinue }
}
Start-Sleep -Seconds 2
foreach ($a in $apps) {
    robocopy $a.Source $a.Path /E /R:2 /W:2 /NFL /NDL /NJH /NP /XF appsettings.Development.json | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "$($a.Name) ko'chirilmadi (robocopy $LASTEXITCODE)." }
    Ok "$($a.Name) -> $($a.Path)"
}

# ---------------------------------------------------------------------------------------------------------------
Step '2/5 Bazalar va SQL huquqlari'
foreach ($a in $apps) {
    # IIS APPPOOL\<nom> Windows hisobi pool yaratilgandan keyingina mavjud bo'ladi
    if (-not (Test-Path "IIS:\AppPools\$($a.Name)")) { New-WebAppPool -Name $a.Name | Out-Null }
    $login = "IIS APPPOOL\$($a.Name)"
    Invoke-Sql 'master' "IF DB_ID(N'$($a.Db)') IS NULL CREATE DATABASE [$($a.Db)];
IF SUSER_ID(N'$login') IS NULL CREATE LOGIN [$login] FROM WINDOWS;"
    Invoke-Sql $a.Db "IF USER_ID(N'$login') IS NULL CREATE USER [$login] FOR LOGIN [$login]; ALTER ROLE db_owner ADD MEMBER [$login];"
    Ok "$($a.Db): [$login] db_owner"
}

# ---------------------------------------------------------------------------------------------------------------
Step '3/5 App pool, sayt, papkalar'
foreach ($a in $apps) {
    $n = $a.Name
    if (-not (Test-Path "IIS:\AppPools\$n")) { New-WebAppPool -Name $n | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$n" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$n" -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$n" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty "IIS:\AppPools\$n" -Name processModel.loadUserProfile -Value $true
    if (-not (Get-Website -Name $n)) {
        New-Website -Name $n -PhysicalPath $a.Path -ApplicationPool $n -HostHeader $a.Host -Port 443 -Ssl -SslFlags 1 | Out-Null
        Ok "sayt: https://$($a.Host) (SNI)"
    } else {
        Set-ItemProperty "IIS:\Sites\$n" -Name physicalPath -Value $a.Path
        Skip "sayt bor: $n"
    }
    Set-ItemProperty "IIS:\Sites\$n" -Name applicationDefaults.preloadEnabled -Value $true
    if ($a.Cert) {
        (Get-WebBinding -Name $n -Protocol https -HostHeader $a.Host).AddSslCertificate($a.Cert, 'WebHosting')
        Ok "sertifikat bog'landi: $($a.Cert)"
    }
    New-Item -ItemType Directory -Force $a.Data | Out-Null
    icacls $a.Data /grant "IIS AppPool\${n}:(OI)(CI)M" | Out-Null
}
Ok "HTTP so'rovlar ERP saytining *.$Domain qoidasi orqali HTTPS'ga yo'naltiriladi"

# ---------------------------------------------------------------------------------------------------------------
Step '4/5 Sozlamalar va master kalit'
$bot = 'tahsinERP.Bot'; $site = 'tahsinERP.Site'
foreach ($n in $bot, $site) { Set-PoolEnv $n 'ASPNETCORE_ENVIRONMENT' 'Production' }
Set-PoolEnv $bot 'Bot__PublicBaseUrl' "https://bot.$Domain"
Set-PoolEnv $bot 'ConnectionStrings__Bot' "Server=$SqlServer;Database=tahsin_bot;Trusted_Connection=True;TrustServerCertificate=True"
Set-PoolEnv $bot 'Bot__FilesRoot' (Join-Path $DataDir 'Bot\files')
Set-PoolEnv $site 'Site__BaseDomain' "sayt.$Domain"
Set-PoolEnv $site 'ConnectionStrings__Site' "Server=$SqlServer;Database=tahsin_site;Trusted_Connection=True;TrustServerCertificate=True"
Set-PoolEnv $site 'Site__DataRoot' (Join-Path $DataDir 'Site\cache')
Ok "bot: https://bot.$Domain; sayt: *.sayt.$Domain"
if (-not $KeepSecrets) {
    $key = Get-PoolEnv $ErpPool 'TAHSINERP_BOT_KEY'
    if ($key -and $key.Length -ge 32) { Skip 'master kalit ERP pool''idan olindi' }
    else {
        $b = New-Object byte[] 36; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
        $key = [Convert]::ToBase64String($b).Replace('+', 'A').Replace('/', 'B').Replace('=', '')
        Set-PoolEnv $ErpPool 'TAHSINERP_BOT_KEY' $key
        & $appcmd recycle apppool /apppool.name:$ErpPool | Out-Null
        Ok 'yangi master kalit yaratildi va ERP pool''iga yozildi (ERP qayta ishga tushirildi)'
    }
    Set-PoolEnv $bot 'Bot__MasterKey' $key
    Set-PoolEnv $site 'Site__MasterKey' $key
    Ok 'master kalit bot va sayt pool''lariga yozildi (ERP bilan bir xil)'
} else { Skip 'master kalit saqlandi (-KeepSecrets)' }

# Panel firma sahifasidan bot/saytni ulashi uchun: master kalitning HMAC hosilasi (u bilan bot tokenlarini ochib bo'lmaydi)
if (Test-Path "IIS:\AppPools\$PanelPool") {
    $mk = Get-PoolEnv $bot 'Bot__MasterKey'
    $h = New-Object System.Security.Cryptography.HMACSHA256 (,[Text.Encoding]::UTF8.GetBytes($mk))
    $adminKey = [Convert]::ToBase64String($h.ComputeHash([Text.Encoding]::UTF8.GetBytes('tahsin-admin-api'))).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    Set-PoolEnv $PanelPool 'ControlPanel__Apps__BotUrl' "https://bot.$Domain"
    Set-PoolEnv $PanelPool 'ControlPanel__Apps__SiteUrl' "https://api.sayt.$Domain"
    Set-PoolEnv $PanelPool 'ControlPanel__Apps__AdminKey' $adminKey
    if ($SiteServerIp) { Set-PoolEnv $PanelPool 'ControlPanel__Apps__SiteServerIp' $SiteServerIp.Trim() }
    & $appcmd recycle apppool /apppool.name:$PanelPool | Out-Null
    Ok 'panel: firma sahifasidan bot/sayt ulash yoqildi (ControlPanel__Apps__*)'
} else { Skip "panel pool'i yo'q ($PanelPool) - panel ulanishi keyin shu skriptni -KeepSecrets bilan qayta ishga tushirib yoziladi" }

# ---------------------------------------------------------------------------------------------------------------
Step '5/5 Ishga tushirish'
foreach ($a in $apps) {
    Start-WebAppPool -Name $a.Name -ErrorAction SilentlyContinue
    if ((Get-Website -Name $a.Name).State -ne 'Started') { Start-Website -Name $a.Name -ErrorAction SilentlyContinue }
    Write-Host "  $($a.Name): pool $((Get-WebAppPoolState -Name $a.Name).Value), sayt $((Get-Website -Name $a.Name).State)"
}
Write-Host @"

Keyingi qadamlar (DNS va sertifikat tayyor bo'lgach):
  - Sertifikat: .\Install-BotSite.ps1 -Domain $Domain -KeepSecrets -CertThumbprint <*.$Domain> -SaytCertThumbprint <*.sayt.$Domain>
  - Tekshiruv: https://bot.$Domain/health  va  https://demo.sayt.$Domain/health
  - Har firma boti va sayti: panel -> Tashkilotlar -> firma -> "Telegram bot va sayt-katalog"
    (zaxira yo'l: .\Invoke-AppCli.ps1 -App Bot tenant add / webhook set, -App Site tenant add)
"@
