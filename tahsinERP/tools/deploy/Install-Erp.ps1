<#
.SYNOPSIS
    tahsinERP (ERP, .NET 4.8) ni serverga o'rnatadi yoki yangilaydi: {firma}.<domen> IIS sayti (Docs/Deploy/01_DEPLOY_GUIDE.md, 4-bo'lim).

.DESCRIPTION
    Paket tuzilishi (shu skript yonida):  app\  - ERP Release publish,  database\  - tahsinERP\Database (migratsiya/seed/skriptlar).
    Administrator PowerShell'da ishga tushiring. Qayta ishga tushirish xavfsiz (yangilash ham shu skript bilan):
      * machineKey birinchi o'rnatishda yangi yaratiladi, keyingi yangilashlarda serverdagi qiymat saqlanadi;
      * db.connection va tenants.json mavjud bo'lsa tegilmaydi;
      * sirlar (bot master kaliti) -KeepSecrets bilan so'ralmaydi.
    SQL'ga ERP Windows autentifikatsiyasi bilan ulanadi (IIS APPPOOL\tahsinERP) - parol saqlanmaydi.
    Har firma bazasiga huquqni provisioning agenti beradi (Sql:AppLogin); bu skript loginni yaratadi va mavjud bazalarga beradi.

.EXAMPLE
    .\Install-Erp.ps1 -Domain tahsinerp.uz
    .\Install-Erp.ps1 -Domain tahsinerp.uz -KeepSecrets            # yangilash
    .\Install-Erp.ps1 -Domain tahsinerp.uz -CertThumbprint ABC...   # sertifikat olingandan keyin
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Domain,
    [string] $SourceDir = (Join-Path $PSScriptRoot 'app'),
    [string] $DatabaseSource = (Join-Path $PSScriptRoot 'database'),
    [string] $PhysicalPath = 'C:\inetpub\tahsinERP',
    [string] $DatabaseRoot = 'C:\tahsinERP\Database',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $SqlServer = '.',
    [string] $FirstDatabase = 'DB_demo',
    [string] $SiteName = 'tahsinERP',
    [string] $CertThumbprint,
    [switch] $KeepSecrets
)

$ErrorActionPreference = 'Stop'
$Domain = $Domain.Trim().Trim('.').ToLowerInvariant()
$pool = $SiteName
$identity = "IIS AppPool\$pool"
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"

function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "  [ok] $t" -ForegroundColor Green }
function Skip($t) { Write-Host "  [--] $t" -ForegroundColor DarkGray }

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator sifatida ishga tushiring.' }
if (-not (Test-Path (Join-Path $SourceDir 'bin\tahsinERP.dll'))) { throw "ERP paketi topilmadi: $SourceDir\bin\tahsinERP.dll" }
if (-not (Test-Path (Join-Path $DatabaseSource 'tools\New-TenantDb.ps1'))) { throw "Database papkasi topilmadi: $DatabaseSource" }
Import-Module WebAdministration

function Read-Secret([string]$prompt) {
    $s = Read-Host -AsSecureString $prompt
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}
function New-Hex([int]$bytes) {
    $b = New-Object byte[] $bytes
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    -join ($b | ForEach-Object { $_.ToString('X2') })
}
function Set-PoolEnv([string]$name, [string]$value) {
    & $appcmd set config -section:system.applicationHost/applicationPools "/-[name='$pool'].environmentVariables.[name='$name']" /commit:apphost 2>$null | Out-Null
    & $appcmd set config -section:system.applicationHost/applicationPools "/+[name='$pool'].environmentVariables.[name='$name',value='$value']" /commit:apphost | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "environmentVariables yozilmadi: $name" }
}
function Invoke-Sql([string]$db, [string]$sql) {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True"
    try { $cn.Open(); $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120; [void]$cmd.ExecuteNonQuery() }
    finally { $cn.Dispose() }
}

# ---------------------------------------------------------------------------------------------------------------
Step '1/8 Fayllar'
$webConfig = Join-Path $PhysicalPath 'Web.config'
$oldMachineKey = $null
if (Test-Path $webConfig) {
    $mk = ([xml](Get-Content -Raw $webConfig)).configuration.'system.web'.machineKey
    if ($mk -and $mk.validationKey) { $oldMachineKey = @{ v = $mk.validationKey; d = $mk.decryptionKey } }
}
if (Test-Path "IIS:\AppPools\$pool") { Stop-WebAppPool -Name $pool -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }
robocopy $SourceDir $PhysicalPath /E /R:2 /W:2 /NFL /NDL /NJH /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "ERP fayllari ko'chirilmadi (robocopy $LASTEXITCODE)." }
Ok "ERP -> $PhysicalPath"
robocopy $DatabaseSource $DatabaseRoot /MIR /R:2 /W:2 /NFL /NDL /NJH /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Database papkasi ko'chirilmadi (robocopy $LASTEXITCODE)." }
Ok "Database -> $DatabaseRoot"

# ---------------------------------------------------------------------------------------------------------------
Step '2/8 Web.config (machineKey, sayt domeni)'
[xml]$cfg = Get-Content -Raw $webConfig
$sw = $cfg.configuration.'system.web'
$mkNode = $sw.SelectSingleNode('machineKey')
if (-not $mkNode) { $mkNode = $cfg.CreateElement('machineKey'); [void]$sw.AppendChild($mkNode) }
if ($oldMachineKey) {
    $mkNode.SetAttribute('validationKey', $oldMachineKey.v); $mkNode.SetAttribute('decryptionKey', $oldMachineKey.d)
    Skip 'machineKey - serverdagi oldingi qiymat saqlandi'
} else {
    $mkNode.SetAttribute('validationKey', (New-Hex 64)); $mkNode.SetAttribute('decryptionKey', (New-Hex 32))
    Ok 'machineKey - yangi yaratildi (repodagi qiymat ishlatilmaydi)'
}
$mkNode.SetAttribute('validation', 'HMACSHA256'); $mkNode.SetAttribute('decryption', 'AES')
foreach ($kv in @(@('SiteBaseDomain', "sayt.$Domain"), @('SiteCnameTarget', "sites.$Domain"))) {
    $n = $cfg.configuration.appSettings.SelectSingleNode("add[@key='$($kv[0])']")
    if (-not $n) { $n = $cfg.CreateElement('add'); $n.SetAttribute('key', $kv[0]); [void]$cfg.configuration.appSettings.AppendChild($n) }
    $n.SetAttribute('value', $kv[1])
}
$cfg.Save($webConfig)
Ok "SiteBaseDomain=sayt.$Domain, SiteCnameTarget=sites.$Domain"

# ---------------------------------------------------------------------------------------------------------------
Step '3/8 App pool va sayt'
if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value 'v4.0'
Set-ItemProperty "IIS:\AppPools\$pool" -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.loadUserProfile -Value $true
$wild = "*.$Domain"
if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $pool -HostHeader $wild -Port 80 | Out-Null
    Ok "sayt yaratildi: http://$wild"
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $pool
    Skip "sayt bor: $SiteName"
}
if (-not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $wild)) {
    New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $wild -SslFlags 1 | Out-Null
    Ok "https binding: $wild (SNI)"
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationDefaults.preloadEnabled -Value $true
if ($CertThumbprint) {
    (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $wild).AddSslCertificate($CertThumbprint, 'WebHosting')
    Ok "sertifikat bog'landi: $CertThumbprint"
}
# HTTP -> HTTPS (sayt darajasida, applicationHost.config - Web.config yangilanganda ham saqlanadi)
$psp = 'MACHINE/WEBROOT/APPHOST'
$rule = Get-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "system.webServer/rewrite/rules/rule[@name='HttpsRedirect']" -Name name -ErrorAction SilentlyContinue
if (-not $rule) {
    Add-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter 'system.webServer/rewrite/rules' -Name '.' -Value @{ name = 'HttpsRedirect'; stopProcessing = 'True' }
    $f = "system.webServer/rewrite/rules/rule[@name='HttpsRedirect']"
    Set-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "$f/match" -Name url -Value '(.*)'
    Add-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "$f/conditions" -Name '.' -Value @{ input = '{HTTPS}'; pattern = '^OFF$' }
    Set-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "$f/action" -Name type -Value 'Redirect'
    Set-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "$f/action" -Name url -Value 'https://{HTTP_HOST}/{R:1}'
    Set-WebConfigurationProperty -PSPath $psp -Location $SiteName -Filter "$f/action" -Name redirectType -Value 'Permanent'
    Ok 'HTTP -> HTTPS yo''naltirish'
} else { Skip 'HTTP -> HTTPS qoidasi bor' }

# ---------------------------------------------------------------------------------------------------------------
Step '4/8 Muhit o''zgaruvchilari'
Set-PoolEnv 'TAHSINERP_BASE_DOMAIN' $Domain
Set-PoolEnv 'TAHSINERP_LICENSE_PANEL_URL' "https://admin.$Domain"
Ok "TAHSINERP_BASE_DOMAIN=$Domain, TAHSINERP_LICENSE_PANEL_URL=https://admin.$Domain"
if (-not $KeepSecrets) {
    $botKey = Read-Secret 'Bot/sayt master kaliti (>=32 belgi; hozir bo''sh qoldirsangiz - Enter, keyin -KeepSecrets siz qayta)'
    if ($botKey) {
        if ($botKey.Length -lt 32) { throw 'Master kalit kamida 32 belgi bo''lishi kerak.' }
        Set-PoolEnv 'TAHSINERP_BOT_KEY' $botKey; Ok 'TAHSINERP_BOT_KEY yozildi'
    } else { Skip 'TAHSINERP_BOT_KEY berilmadi (bot/sayt API 503 qaytaradi)' }
}

# ---------------------------------------------------------------------------------------------------------------
Step '5/8 Ulanish satri va tenants.json'
New-Item -ItemType Directory -Force $DataDir | Out-Null
icacls $DataDir /grant "${identity}:(OI)(CI)RX" | Out-Null
$connFile = Join-Path $DataDir 'db.connection'
if (Test-Path $connFile) { Skip "db.connection bor - tegilmadi" }
else {
    Set-Content -Encoding UTF8 -Path $connFile -Value "data source=$SqlServer;initial catalog=$FirstDatabase;Integrated Security=True;MultipleActiveResultSets=True;App=EntityFramework"
    icacls $connFile /inheritance:r /grant:r 'Administrators:F' "${identity}:R" | Out-Null
    Ok "db.connection (Windows auth, parolsiz; firma bazasi tenants.json bo'yicha almashtiriladi)"
}
$tenantsFile = Join-Path $DataDir 'tenants.json'
if (Test-Path $tenantsFile) { Skip 'tenants.json bor - tegilmadi (agent yuritadi)' }
else {
    $json = '{ "baseDomain": "' + $Domain + '", "tenants": [] }'
    [IO.File]::WriteAllText($tenantsFile, $json, (New-Object Text.UTF8Encoding($false)))
    Ok 'tenants.json (bo''sh - firmalarni panel/agent qo''shadi)'
}
icacls $DataDir /grant 'NT AUTHORITY\NETWORK SERVICE:(OI)(CI)M' | Out-Null
icacls $DatabaseRoot /grant 'NT AUTHORITY\NETWORK SERVICE:(OI)(CI)RX' | Out-Null
Ok 'huquqlar: ERP pool - o''qish; agent (NETWORK SERVICE) - tenants.json/zaxira yozish, Database o''qish'

# ---------------------------------------------------------------------------------------------------------------
Step '6/8 SQL login va huquqlar'
$login = "IIS APPPOOL\$pool"
try {
    Invoke-Sql 'master' "IF SUSER_ID(N'$login') IS NULL CREATE LOGIN [$login] FROM WINDOWS;
IF SUSER_ID(N'NT AUTHORITY\NETWORK SERVICE') IS NULL CREATE LOGIN [NT AUTHORITY\NETWORK SERVICE] FROM WINDOWS;
ALTER SERVER ROLE dbcreator ADD MEMBER [NT AUTHORITY\NETWORK SERVICE];"
    Ok "login: [$login]; agent [NETWORK SERVICE] - dbcreator"
    $t = Get-Content -Raw $tenantsFile | ConvertFrom-Json
    foreach ($ten in @($t.tenants)) {
        if (-not $ten.database) { continue }
        Invoke-Sql $ten.database "IF USER_ID(N'$login') IS NULL CREATE USER [$login] FOR LOGIN [$login] ELSE ALTER USER [$login] WITH LOGIN = [$login]; ALTER ROLE db_owner ADD MEMBER [$login];"
        Ok "$($ten.database): [$login] db_owner"
    }
} catch { Write-Warning "SQL huquqlari berilmadi: $($_.Exception.Message)" }

# ---------------------------------------------------------------------------------------------------------------
Step '7/8 Ishga tushirish'
Start-WebAppPool -Name $pool -ErrorAction SilentlyContinue
if ((Get-Website -Name $SiteName).State -ne 'Started') { Start-Website -Name $SiteName -ErrorAction SilentlyContinue }
Ok "pool va sayt ishga tushirildi"

# ---------------------------------------------------------------------------------------------------------------
Step '8/8 Holat'
Get-WebBinding -Name $SiteName | ForEach-Object { Write-Host "  binding: $($_.protocol) $($_.bindingInformation) sslFlags=$($_.sslFlags)" }
Write-Host "  pool: $((Get-WebAppPoolState -Name $pool).Value), sayt: $((Get-Website -Name $SiteName).State)"

Write-Host @"

Keyingi qadamlar:
  - Agent: C:\tahsinERP\Agent\appsettings.json -> Sql:AppLogin = "$($login.Replace('\','\\'))", Paths:DatabaseRoot = "$($DatabaseRoot.Replace('\','\\'))"
  - Sertifikat (DNS faol bo'lgach): win-acme -> keyin .\Install-Erp.ps1 -Domain $Domain -KeepSecrets -CertThumbprint <...>
  - Birinchi firma (demo) - panelda "Bazani yaratish" (agent o'rnatilgandan keyin)
"@
