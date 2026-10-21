<#
.SYNOPSIS
    Boshqaruv panelining birinchi Owner foydalanuvchisini yaratadi (Install-PanelIis.ps1 dan keyin, bir marta).

.DESCRIPTION
    create-owner buyrug'i MasterKey'ni talab qiladi - u shu yerda yashirin so'raladi va faqat shu jarayon uchun beriladi
    (shell tarixida va diskda qolmaydi). Parol ham yashirin so'raladi.

.EXAMPLE
    .\New-PanelOwner.ps1 -Email siz@pochta.uz -Name "Ism Familiya"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Email,
    [string] $Name,
    [string] $PhysicalPath = 'C:\inetpub\tahsinERP.ControlPanel'
)

$ErrorActionPreference = 'Stop'

function Read-Secret([string] $prompt) {
    $s = Read-Host -AsSecureString $prompt
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}

$dll = Join-Path $PhysicalPath 'tahsinERP.ControlPanel.dll'
if (-not (Test-Path $dll)) { throw "Topilmadi: $dll" }

$env:ControlPanel__Security__MasterKey = Read-Secret 'MasterKey (gen-master-key chiqargan qiymat)'
try {
    Push-Location $PhysicalPath   # appsettings.json shu papkadan o'qiladi
    $argsList = @($dll, 'create-owner', '--email', $Email)
    if ($Name) { $argsList += @('--name', $Name) }
    & dotnet @argsList
    if ($LASTEXITCODE -ne 0) { throw "create-owner xato bilan tugadi (exit $LASTEXITCODE)." }
} finally {
    Pop-Location
    Remove-Item Env:\ControlPanel__Security__MasterKey -ErrorAction SilentlyContinue
}
