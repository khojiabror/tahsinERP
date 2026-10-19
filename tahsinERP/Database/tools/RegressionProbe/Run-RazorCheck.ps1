<#
.SYNOPSIS
  Barcha Razor view'larni kompilyatsiya qilib xatolarni chiqaradi (build view'larni tekshirmaydi).
  Model/ViewBag turi o'zgarganda (masalan float -> decimal) ishlating. Avval loyihani MSBuild bilan build qiling.

.EXAMPLE
  .\Run-RazorCheck.ps1            # chiqish kodi 0 — xato yo'q
#>
param([string]$WebDir = (Join-Path $PSScriptRoot '..\..\..'))
$ErrorActionPreference = 'Stop'
$WebDir = (Resolve-Path $WebDir).Path
$csc = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.exe topilmadi.' }
$work = Join-Path $env:TEMP ('tahsinERP-razorcheck\' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Force $work | Out-Null
$bin = Join-Path $WebDir 'bin'
$exe = Join-Path $work 'RazorCheck.exe'
& $csc.FullName /nologo /target:exe /out:$exe "/r:$bin\System.Web.Razor.dll" "/r:$bin\System.Web.Mvc.dll" "/r:$bin\System.Web.WebPages.Razor.dll" `
    "/r:$bin\System.Web.WebPages.dll" /r:System.Xml.Linq.dll /r:System.Web.dll (Join-Path $PSScriptRoot 'RazorCheck.cs')
if ($LASTEXITCODE -ne 0) { throw 'RazorCheck kompilyatsiya xatosi' }
Copy-Item "$bin\System.Web.Razor.dll", "$bin\System.Web.Mvc.dll", "$bin\System.Web.WebPages.Razor.dll", "$bin\System.Web.WebPages.dll" $work
& $exe $WebDir (Join-Path $work 'gen') $csc.FullName
exit $LASTEXITCODE
