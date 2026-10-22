<#
.SYNOPSIS
    tahsinERP relizini yig'adi: har ilova alohida ZIP + manifest.json (+ imzo, kalit berilsa). CI (.github/workflows/release.yml)
    va lokal tekshiruv uchun bitta skript. Docs/Deploy/03_AUTO_UPDATE.md.

.DESCRIPTION
    Natija (-OutDir):
      erp.zip      app\ (MSBuild WebPublish) + database\ (tahsinERP\Database)
      panel.zip, bot.zip, site.zip, agent.zip, updater.zip  - dotnet publish (win-x64, framework-dependent)
      manifest.json, manifest.sig (RELEASE_SIGNING_KEY env bo'lsa), notes.md, changed.txt
    O'zgargan ilovalar -PrevTag dan beri git diff bo'yicha aniqlanadi (tag yo'q - hammasi o'zgargan).

.EXAMPLE
    .\tools\release\Build-Release.ps1 -Version 2026.10.22.1 -OutDir C:\temp\rel
    .\tools\release\Build-Release.ps1 -Version 2026.10.22.1 -OutDir C:\temp\rel -SkipErp     # tezkor (ERP'siz)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $OutDir,
    [string] $PrevTag,
    [string] $MsBuild,
    [switch] $SkipErp,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$commit = (git -C $root rev-parse HEAD).Trim()
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Check($what) { if ($LASTEXITCODE -ne 0) { throw "$what xato bilan tugadi ($LASTEXITCODE)." } }

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
$stage = Join-Path $OutDir '_stage'
New-Item -ItemType Directory -Force $stage | Out-Null

# --------------------------------------------------------------------------------------------- O'zgargan ilovalar
$paths = [ordered]@{
    erp     = @('tahsinERP/', 'tahsinERP.Licensing/')
    panel   = @('tahsinERP.ControlPanel/', 'tahsinERP.Provisioning.Contracts/', 'tahsinERP.Licensing/')
    bot     = @('tahsinERP.Bot/')
    site    = @('tahsinERP.Site/')
    agent   = @('tahsinERP.ProvisioningAgent/', 'tahsinERP.Provisioning.Contracts/', 'tahsinERP.Licensing/', 'tahsinERP/Helpers/PasswordHasher.cs', 'tahsinERP/Database/')
    updater = @('tahsinERP.Updater/', 'tahsinERP.Provisioning.Contracts/')
}
$ignore = '^(tahsinERP/Docs/|.*\.md$)'
$changed = @()
if ($PrevTag) {
    $files = @(git -C $root diff --name-only "$PrevTag" HEAD) | Where-Object { $_ -and $_ -notmatch $ignore }
    foreach ($app in $paths.Keys) {
        foreach ($p in $paths[$app]) { if ($files | Where-Object { $_.StartsWith($p) }) { $changed += $app; break } }
    }
} else { $changed = @($paths.Keys) }
Write-Host "O'zgargan: $($changed -join ', ') (oldingi teg: $(if ($PrevTag) { $PrevTag } else { 'yo''q' }))"

# --------------------------------------------------------------------------------------------- Testlar (SQL'siz)
if (-not $SkipTests) {
    Step 'Testlar'
    dotnet test (Join-Path $root 'tahsinERP.ControlPanel.Tests') -c Release --filter 'FullyQualifiedName~UpdaterTests|FullyQualifiedName~ReleaseTests' --nologo -v q
    Check 'ControlPanel.Tests (updater)'
    dotnet test (Join-Path $root 'tahsinERP.Bot.Tests') -c Release --nologo -v q; Check 'Bot.Tests'
    dotnet test (Join-Path $root 'tahsinERP.Site.Tests') -c Release --nologo -v q; Check 'Site.Tests'
}

# --------------------------------------------------------------------------------------------- .NET ilovalar
$dotnetApps = [ordered]@{
    panel   = 'tahsinERP.ControlPanel\tahsinERP.ControlPanel.csproj'
    bot     = 'tahsinERP.Bot\tahsinERP.Bot.csproj'
    site    = 'tahsinERP.Site\tahsinERP.Site.csproj'
    agent   = 'tahsinERP.ProvisioningAgent\tahsinERP.ProvisioningAgent.csproj'
    updater = 'tahsinERP.Updater\tahsinERP.Updater.csproj'
}
foreach ($app in $dotnetApps.Keys) {
    Step "dotnet publish: $app"
    $o = Join-Path $stage $app
    dotnet publish (Join-Path $root $dotnetApps[$app]) -c Release -r win-x64 --self-contained false -o $o --nologo -v q `
        "-p:Version=$Version" "-p:InformationalVersion=$Version+$($commit.Substring(0, 8))"
    Check "publish $app"
    Get-ChildItem $o -Filter 'appsettings.Development.json' -Recurse | Remove-Item -Force
}

# --------------------------------------------------------------------------------------------- ERP (.NET 4.8)
if (-not $SkipErp) {
    Step 'ERP: MSBuild WebPublish'
    if (-not $MsBuild) {
        $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        $MsBuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
    if (-not $MsBuild -or -not (Test-Path $MsBuild)) { throw 'MSBuild topilmadi (-MsBuild).' }
    $erpProj = Join-Path $root 'tahsinERP\tahsinERP.csproj'
    $app = Join-Path $stage 'erp\app'
    & $MsBuild $erpProj -t:restore -p:RestorePackagesConfig=true -p:SolutionDir="$root\" -v:m -nologo
    Check 'ERP restore'
    & $MsBuild $erpProj -p:Configuration=Release -p:DeployOnBuild=true -p:DeployDefaultTarget=WebPublish -p:WebPublishMethod=FileSystem `
        -p:PublishUrl="$app" -p:DeleteExistingFiles=true -p:SolutionDir="$root\" -v:m -nologo
    Check 'ERP build'
    if (-not (Test-Path (Join-Path $app 'bin\tahsinERP.dll'))) { throw "ERP publish natijasi topilmadi: $app" }
    robocopy (Join-Path $root 'tahsinERP\Database') (Join-Path $stage 'erp\database') /E /XD RegressionProbe /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Database ko'chirilmadi ($LASTEXITCODE)." }
}

# --------------------------------------------------------------------------------------------- ZIP
Step 'ZIP'
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($dir in Get-ChildItem $stage -Directory) {
    $zip = Join-Path $OutDir "$($dir.Name).zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($dir.FullName, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host ("  {0,-12} {1,10:N0} KB" -f "$($dir.Name).zip", ((Get-Item $zip).Length / 1KB))
}
Remove-Item $stage -Recurse -Force

# --------------------------------------------------------------------------------------------- Manifest, imzo, izoh
Step 'Manifest'
$tool = Join-Path $root 'tahsinERP.ReleaseTool\tahsinERP.ReleaseTool.csproj'
$prevArg = @(); if ($PrevTag) { $prevArg = @('--prev-tag', $PrevTag) }
dotnet run --project $tool -c Release -- manifest --dir $OutDir --version $Version --commit $commit --changed ($changed -join ',') @prevArg
Check 'manifest'
if ($env:RELEASE_SIGNING_KEY) {
    dotnet run --project $tool -c Release --no-build -- sign --dir $OutDir --key-env RELEASE_SIGNING_KEY --key-id r1
    Check 'sign'
    dotnet run --project $tool -c Release --no-build -- verify --dir $OutDir
    Check 'verify'
} else { Write-Warning 'RELEASE_SIGNING_KEY yo''q - manifest imzolanmadi (lokal sinov).' }

$notes = New-Object System.Text.StringBuilder
[void]$notes.AppendLine("tahsinERP $Version (commit $($commit.Substring(0, 8)))")
[void]$notes.AppendLine('')
foreach ($app in $changed) {
    [void]$notes.AppendLine("### $app")
    $range = if ($PrevTag) { "$PrevTag..HEAD" } else { 'HEAD~30..HEAD' }
    $log = @(git -C $root log --no-merges --format='- %s (%h)' $range -- $paths[$app] 2>$null) | Select-Object -First 60
    if ($log.Count -eq 0) { $log = @('- (merge orqali)') }
    foreach ($l in $log) { [void]$notes.AppendLine($l) }
    [void]$notes.AppendLine('')
}
[IO.File]::WriteAllText((Join-Path $OutDir 'notes.md'), $notes.ToString(), (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $OutDir 'changed.txt'), ($changed -join ','), (New-Object Text.UTF8Encoding($false)))
Write-Host "`nTayyor: $OutDir" -ForegroundColor Green
