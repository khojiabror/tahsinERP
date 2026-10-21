<#
.SYNOPSIS
    Boshqaruv panelining birinchi Owner foydalanuvchisini yaratadi (Install-PanelIis.ps1 dan keyin, bir marta).

.DESCRIPTION
    create-owner buyrug'i MasterKey'ni talab qiladi - u panel app pool sozlamalaridan (Install-PanelIis.ps1 yozgan)
    o'qiladi va faqat shu jarayonga beriladi: qo'lda kiritish, ekranga chiqarish yoki diskka yozish yo'q.
    Owner paroli yashirin so'raladi - klaviaturada yozing (brauzer RDP'da joylash ishlamasligi mumkin).

.EXAMPLE
    .\New-PanelOwner.ps1 -Email siz@pochta.uz -Name "Ism Familiya"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Email,
    [string] $Name,
    [string] $PhysicalPath = 'C:\inetpub\tahsinERP.ControlPanel',
    [string] $Pool = 'tahsinERP.ControlPanel'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$dll = Join-Path $PhysicalPath 'tahsinERP.ControlPanel.dll'
if (-not (Test-Path $dll)) { throw "Topilmadi: $dll" }

$vars = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.applicationHost/applicationPools/add[@name='$Pool']/environmentVariables/add"
$set = @()
foreach ($v in $vars) {
    if ($v.name -like 'ControlPanel__*' -or $v.name -like 'ConnectionStrings__*') { Set-Item -Path "Env:\$($v.name)" -Value $v.value; $set += $v.name }
}
if ($set -notcontains 'ControlPanel__Security__MasterKey') { throw "Pool '$Pool' sozlamalarida MasterKey yo'q - avval Install-PanelIis.ps1." }

try {
    Push-Location $PhysicalPath   # appsettings.json shu papkadan o'qiladi
    $argsList = @($dll, 'create-owner', '--email', $Email)
    if ($Name) { $argsList += @('--name', $Name) }
    & dotnet @argsList
    if ($LASTEXITCODE -ne 0) { throw "create-owner xato bilan tugadi (exit $LASTEXITCODE)." }
} finally {
    Pop-Location
    foreach ($n in $set) { Remove-Item -Path "Env:\$n" -ErrorAction SilentlyContinue }
}
