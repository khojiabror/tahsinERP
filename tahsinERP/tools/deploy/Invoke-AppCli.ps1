<#
.SYNOPSIS
    Bot yoki sayt buyruqlarini (tenant add, webhook set, snapshot sync ...) server sozlamalari bilan ishga tushiradi.

.DESCRIPTION
    Master kalit va ulanish satri IIS pool muhitidan o'qiladi va faqat shu jarayonga beriladi - qo'lda kiritish,
    ekranga chiqarish yoki diskka yozish shart emas. Administrator PowerShell'da.

.EXAMPLE
    .\Invoke-AppCli.ps1 -App Bot tenant add firma1 https://firma1.tahsinerp.uz/ <TOKEN> --erp-tenant firma1
    .\Invoke-AppCli.ps1 -App Bot webhook set firma1
    .\Invoke-AppCli.ps1 -App Site tenant add firma1 https://firma1.tahsinerp.uz/ --erp-tenant firma1
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Bot', 'Site')] [string] $App,
    [Parameter(ValueFromRemainingArguments = $true)] [string[]] $CliArgs
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$pool = "tahsinERP.$App"
$dir = "C:\inetpub\tahsinERP.$App"
$dll = Join-Path $dir "tahsinERP.$App.dll"
if (-not (Test-Path $dll)) { throw "Topilmadi: $dll (avval Install-BotSite.ps1)" }
if (-not $CliArgs) { $CliArgs = @('help') }

$vars = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.applicationHost/applicationPools/add[@name='$pool']/environmentVariables/add"
$set = @()
foreach ($v in $vars) { Set-Item -Path "Env:\$($v.name)" -Value $v.value; $set += $v.name }
try {
    Push-Location $dir
    & dotnet $dll @CliArgs
    $code = $LASTEXITCODE
} finally {
    Pop-Location
    foreach ($n in $set) { Remove-Item -Path "Env:\$n" -ErrorAction SilentlyContinue }
}
exit $code
