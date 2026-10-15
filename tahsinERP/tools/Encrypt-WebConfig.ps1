param(
    [Parameter(Mandatory = $true)]
    [string]$TargetPath,
    [switch]$AllowUnc
)

$isUncPath = $TargetPath.StartsWith("\\")
if ($isUncPath -and -not $AllowUnc) {
    Write-Warning "UNC path detected. Skipping encryption. Run this script on the server or pass -AllowUnc."
    exit 0
}

if (-not (Test-Path -LiteralPath $TargetPath)) {
    throw "Target path not found: $TargetPath"
}

$framework64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\aspnet_regiis.exe"
$framework32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\aspnet_regiis.exe"

if (Test-Path -LiteralPath $framework64) {
    $aspnetRegiis = $framework64
} elseif (Test-Path -LiteralPath $framework32) {
    $aspnetRegiis = $framework32
} else {
    throw "aspnet_regiis.exe not found under $env:WINDIR\Microsoft.NET\Framework*."
}

Write-Host "Encrypting appSettings in $TargetPath (DPAPI)"
& $aspnetRegiis -pef "appSettings" "$TargetPath" -prov "DataProtectionConfigurationProvider"

Write-Host "Encrypting connectionStrings in $TargetPath (DPAPI)"
& $aspnetRegiis -pef "connectionStrings" "$TargetPath" -prov "DataProtectionConfigurationProvider"
