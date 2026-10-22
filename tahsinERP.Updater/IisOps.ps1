<#
.SYNOPSIS
    tahsinERP.Updater uchun IIS amallari (faqat ASCII - Windows PowerShell 5.1). Updater (LocalSystem) chaqiradi.

.DESCRIPTION
    EnsureErpPool  - faqat pool (env bilan). IIS APPPOOL\<pool> Windows hisobi shundan keyin paydo bo'ladi - SQL login
                     undan oldin yaratilmaydi, shuning uchun Updater avval shu buyruqni chaqiradi.
    EnsureErpSite  - firma ERP instance'i: pool (env umumiy ERP pool'idan nusxa), sayt (https <slug>.<domen>, SNI,
                     umumiy saytning sertifikati), papka huquqlari. Qayta chaqirish xavfsiz.
    RemoveErpSite  - sayt va pool'ni o'chiradi (papka va baza qoladi).
    HTTP (80) so'rovlarni umumiy wildcard sayt HTTPS'ga yo'naltiradi - firma saytiga faqat https binding kerak.
#>
param(
    [Parameter(Mandatory)] [ValidateSet('EnsureErpPool', 'EnsureErpSite', 'RemoveErpSite')] [string] $Command,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Name,
    [string] $PhysicalPath,
    [ValidatePattern('^[a-z0-9.-]+$')] [string] $HostName,
    [string] $SharedSite = 'tahsinERP',
    [int] $IdleMinutes = 20,
    [string] $DataDir = 'C:\ProgramData\tahsinERP'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$psp = 'MACHINE/WEBROOT/APPHOST'

if ($Command -eq 'RemoveErpSite') {
    if (Get-Website -Name $Name) { Remove-Website -Name $Name; Write-Host "sayt o'chirildi: $Name" }
    if (Test-Path "IIS:\AppPools\$Name") { Remove-WebAppPool -Name $Name; Write-Host "pool o'chirildi: $Name" }
    exit 0
}

if ($Command -eq 'EnsureErpSite') {
    if (-not $PhysicalPath -or -not $HostName) { throw 'PhysicalPath va HostName majburiy.' }
    if (-not (Test-Path $PhysicalPath)) { throw "Papka yo'q: $PhysicalPath" }
}
$shared = Get-Website -Name $SharedSite
if (-not $shared) { throw "Umumiy ERP sayti topilmadi: $SharedSite" }
$sharedPool = $shared.applicationPool

# --- Pool
if (-not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null; Write-Host "pool yaratildi: $Name" }
Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value 'v4.0'
Set-ItemProperty "IIS:\AppPools\$Name" -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.loadUserProfile -Value $true
if ($IdleMinutes -gt 0) {
    Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value 'OnDemand'
    Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.idleTimeout -Value ([TimeSpan]::FromMinutes($IdleMinutes))
} else {
    Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
}

# --- Muhit o'zgaruvchilari: umumiy pool'dan (sirlar shu yerda ko'chadi, hech qayerga yozilmaydi/chiqarilmaydi)
$src = Get-WebConfiguration -PSPath $psp -Filter "system.applicationHost/applicationPools/add[@name='$sharedPool']/environmentVariables/add"
$dstFilter = "system.applicationHost/applicationPools/add[@name='$Name']/environmentVariables"
$count = 0
foreach ($v in @($src)) {
    if (-not $v) { continue }
    $existing = Get-WebConfiguration -PSPath $psp -Filter "$dstFilter/add[@name='$($v.name)']"
    if ($existing) { Remove-WebConfigurationProperty -PSPath $psp -Filter $dstFilter -Name '.' -AtElement @{ name = $v.name } }
    Add-WebConfigurationProperty -PSPath $psp -Filter $dstFilter -Name '.' -Value @{ name = $v.name; value = $v.value }
    $count++
}
Write-Host "pool env: $count ta o'zgaruvchi $sharedPool dan"
if ($Command -eq 'EnsureErpPool') { exit 0 }

# --- Sayt va https binding (SNI) + umumiy saytning sertifikati
if (-not (Get-Website -Name $Name)) {
    New-Website -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $Name -HostHeader $HostName -Port 443 -Ssl -SslFlags 1 | Out-Null
    Write-Host "sayt yaratildi: https://$HostName"
} else {
    Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $Name
    if (-not (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName)) {
        New-WebBinding -Name $Name -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1 | Out-Null
    }
}
Set-ItemProperty "IIS:\Sites\$Name" -Name applicationDefaults.preloadEnabled -Value $true
$sharedHttps = @(Get-WebBinding -Name $SharedSite -Protocol https | Where-Object { $_.certificateHash })[0]
if ($sharedHttps) {
    (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName).AddSslCertificate($sharedHttps.certificateHash, $sharedHttps.certificateStoreName)
    Write-Host "sertifikat: $($sharedHttps.certificateHash) ($($sharedHttps.certificateStoreName))"
} else { Write-Warning "Umumiy saytda https sertifikati topilmadi - binding sertifikatsiz" }
if ((Get-Website -Name $Name).State -ne 'Started') { Start-Website -Name $Name -ErrorAction SilentlyContinue }

# --- Huquqlar: pool identity
$id = "IIS AppPool\$Name"
icacls $PhysicalPath /grant "${id}:(OI)(CI)RX" | Out-Null
$appData = Join-Path $PhysicalPath 'App_Data'
New-Item -ItemType Directory -Force $appData | Out-Null
icacls $appData /grant "${id}:(OI)(CI)M" | Out-Null
icacls $DataDir /grant "${id}:(OI)(CI)RX" | Out-Null
$conn = Join-Path $DataDir 'db.connection'
if (Test-Path $conn) { icacls $conn /grant "${id}:R" | Out-Null }
Write-Host "huquqlar: $id"
exit 0
