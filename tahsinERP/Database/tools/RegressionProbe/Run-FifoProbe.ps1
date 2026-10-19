<#
.SYNOPSIS
  Ombor/FIFO va QQS ichki hisoblarini ikki build o'rtasida solishtirish uchun natijalarni yozadi (FifoProbe.cs).
  A) chiqim qatorlari FIFO tannarxi va birlik konversiyasi, C) BOM sarfi, E) QQS — faqat o'qish;
  -Write bilan B/D) oxirgi 60 chiqim hujjatini bekor qilib qayta chiqarish, lot ajratmalari, kasr Adjust —
  bitta tranzaksiyada, oxirida ROLLBACK. -Write faqat sinov nusxa bazasida (nomi F<raqam>... yoki ...TEST...) ruxsat etiladi.

.EXAMPLE
  # eski build (double API) — -Old; yangi build (decimal API) — belgisiz
  .\Run-FifoProbe.ps1 -BinDir C:\old\bin -Old -Database F6TEST_IPACK -Write -Out C:\tmp\old.txt
  .\Run-FifoProbe.ps1 -Database F6TEST_IPACK -Write -Out C:\tmp\new.txt
  # natija: "kalit<TAB>qiymat" qatorlari (raqamlar 10 xonagacha) — ikki faylni qator bo'yicha solishtiring
#>
param([string]$BinDir = (Join-Path $PSScriptRoot '..\..\..\bin'), [Parameter(Mandatory)][string]$Out, [switch]$Old, [switch]$Write,
      [Parameter(Mandatory)][string]$Database, [string]$Server = 'localhost', [string]$WebConfig)
$ErrorActionPreference = 'Stop'
if ($Write -and $Database -notmatch '^F\d|TEST') { throw "-Write faqat sinov nusxa bazasi uchun (berilgan: $Database)" }
$BinDir = (Resolve-Path $BinDir).Path
$csc = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe' | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.exe topilmadi.' }
$work = Join-Path $env:TEMP ('tahsinERP-fifo\' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Force $work | Out-Null
Copy-Item (Join-Path $BinDir '*.dll') $work
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll', 'System.Core.dll', 'System.Data.dll', 'System.Web.dll', 'System.ComponentModel.DataAnnotations.dll', 'Microsoft.CSharp.dll', 'System.Runtime.dll', 'netstandard.dll') |
    ForEach-Object { if (Test-Path "$fw\$_") { "/r:$fw\$_" } elseif (Test-Path "$fw\Facades\$_") { "/r:$fw\Facades\$_" } }
$refs += @('tahsinERP.dll', 'EntityFramework.dll') | ForEach-Object { "/r:$work\$_" }
$exe = Join-Path $work 'FifoProbe.exe'
$def = if ($Old) { '/define:OLD' } else { '/define:NEW' }
& $csc.FullName /nologo /langversion:latest /target:exe $def /out:$exe @refs (Join-Path $PSScriptRoot 'FifoProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Kompilyatsiya xatosi' }
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="entityFramework" type="System.Data.Entity.Internal.ConfigFile.EntityFrameworkSection, EntityFramework, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" requirePermission="false" />
  </configSections>
  <startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup>
  <entityFramework><providers><provider invariantName="System.Data.SqlClient" type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer" /></providers></entityFramework>
</configuration>
'@ | Set-Content -Encoding UTF8 "$exe.config"
# Binding redirect'lar web.config'dan
if (-not $WebConfig) { $WebConfig = Join-Path $BinDir '..\Web.config' }
$web = [xml](Get-Content $WebConfig -Raw); $cfg = [xml](Get-Content "$exe.config" -Raw)
if ($web.configuration.runtime) { $cfg.configuration.AppendChild($cfg.ImportNode($web.configuration.runtime, $true)) | Out-Null; $cfg.Save("$exe.config") }
$env:TAHSINERP_DB = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
$a = @([IO.Path]::GetFullPath($Out)); if ($Write) { $a += 'write' }
& $exe @a
exit $LASTEXITCODE
