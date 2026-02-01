param(
    [Parameter(Mandatory = $true)]
    [string]$TargetPath,
    [switch]$Use32Bit
)

if (-not (Test-Path -LiteralPath $TargetPath)) {
    throw "Target path not found: $TargetPath"
}

$frameworkRoot = if ($Use32Bit) { "Framework" } else { "Framework64" }
$aspnetRegiis = Join-Path $env:WINDIR "Microsoft.NET\$frameworkRoot\v4.0.30319\aspnet_regiis.exe"

if (-not (Test-Path -LiteralPath $aspnetRegiis)) {
    throw "aspnet_regiis.exe not found at: $aspnetRegiis"
}

Write-Host "Decrypting appSettings in $TargetPath (DPAPI)" -ForegroundColor Cyan
& $aspnetRegiis -pdf "appSettings" "$TargetPath"

Write-Host "Decrypting connectionStrings in $TargetPath (DPAPI)" -ForegroundColor Cyan
& $aspnetRegiis -pdf "connectionStrings" "$TargetPath"

Write-Host "Encrypting appSettings in $TargetPath (DPAPI)" -ForegroundColor Cyan
& $aspnetRegiis -pef "appSettings" "$TargetPath" -prov "DataProtectionConfigurationProvider"

Write-Host "Encrypting connectionStrings in $TargetPath (DPAPI)" -ForegroundColor Cyan
& $aspnetRegiis -pef "connectionStrings" "$TargetPath" -prov "DataProtectionConfigurationProvider"

Write-Host "Done." -ForegroundColor Green
