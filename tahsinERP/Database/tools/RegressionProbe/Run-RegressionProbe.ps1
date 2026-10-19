<#
.SYNOPSIS
  Regressiya harness'ini kompilyatsiya qilib ishga tushiradi (float → decimal bosqichlari uchun "oldin/keyin").

.EXAMPLE
  # 1) o'zgarishdan oldin (joriy build, bazada eski sxema)
  .\Run-RegressionProbe.ps1 -Out C:\tmp\before.json
  # 2) migratsiya + rebuild'dan keyin
  .\Run-RegressionProbe.ps1 -Out C:\tmp\after.json
  # 3) solishtirish (farq <= 0.01 — yaxlitlash, undan katta — xato, chiqish kodi 1)
  .\Run-RegressionProbe.ps1 -Compare C:\tmp\before.json, C:\tmp\after.json

.NOTES
  Faqat o'qiydi. -BinDir — tahsinERP\bin (avval MSBuild bilan build qiling). Ishchi papka: %TEMP%\tahsinERP-probe\<vaqt>.
#>
param(
    [string]$Out,
    [string[]]$Compare,
    [decimal]$Tolerance = 0.01,
    [string]$Server = 'localhost',
    [string]$Database = 'DBTHSN-IPACK',
    [string]$User = 'khojiabror',
    [string]$BinDir = (Join-Path $PSScriptRoot '..\..\..\bin')
)
$ErrorActionPreference = 'Stop'

$csc = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.exe topilmadi (Visual Studio / Build Tools kerak).' }
$BinDir = (Resolve-Path $BinDir).Path
if (-not (Test-Path (Join-Path $BinDir 'tahsinERP.dll'))) { throw "tahsinERP.dll topilmadi: $BinDir" }

$work = Join-Path $env:TEMP ('tahsinERP-probe\' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Force $work | Out-Null
Copy-Item (Join-Path $BinDir '*.dll') $work

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll', 'System.Core.dll', 'System.Data.dll', 'System.Web.dll', 'System.ComponentModel.DataAnnotations.dll',
          'Microsoft.CSharp.dll', 'System.Runtime.dll', 'netstandard.dll') |
    ForEach-Object { if (Test-Path "$fw\$_") { "/r:$fw\$_" } elseif (Test-Path "$fw\Facades\$_") { "/r:$fw\Facades\$_" } }
$refs += @('tahsinERP.dll', 'EntityFramework.dll', 'Newtonsoft.Json.dll', 'System.Web.Mvc.dll') | ForEach-Object { "/r:$work\$_" }
$exe = Join-Path $work 'RegressionProbe.exe'
& $csc.FullName /nologo /langversion:latest /target:exe /platform:anycpu /out:$exe @refs (Join-Path $PSScriptRoot 'RegressionProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Kompilyatsiya xatosi' }

@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="entityFramework" type="System.Data.Entity.Internal.ConfigFile.EntityFrameworkSection, EntityFramework, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" requirePermission="false" />
  </configSections>
  <startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup>
  <entityFramework>
    <providers>
      <provider invariantName="System.Data.SqlClient" type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer" />
    </providers>
  </entityFramework>
</configuration>
'@ | Set-Content -Encoding UTF8 "$exe.config"
# Binding redirect'lar web.config'dan
$web = [xml](Get-Content (Join-Path $BinDir '..\Web.config') -Raw)
$cfg = [xml](Get-Content "$exe.config" -Raw)
$rt = $web.configuration.runtime
if ($rt) { $cfg.configuration.AppendChild($cfg.ImportNode($rt, $true)) | Out-Null; $cfg.Save("$exe.config") }

if ($Compare) {
    if ($Compare.Count -ne 2) { throw '-Compare before.json, after.json' }
    & $exe compare (Resolve-Path $Compare[0]).Path (Resolve-Path $Compare[1]).Path $Tolerance.ToString([Globalization.CultureInfo]::InvariantCulture)
    exit $LASTEXITCODE
}
if (-not $Out) { throw '-Out yoki -Compare kerak' }
$env:TAHSINERP_DB = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
$OutFull = [IO.Path]::GetFullPath($Out)
& $exe run $OutFull $User
exit $LASTEXITCODE
