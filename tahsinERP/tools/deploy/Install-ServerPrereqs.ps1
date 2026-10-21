<#
.SYNOPSIS
    tahsinERP serveri uchun tayyorgarlik (Docs/Deploy/01_DEPLOY_GUIDE.md, 1-bo'lim): IIS rollari, ASP.NET Core 10 Hosting Bundle,
    IIS URL Rewrite 2.1, win-acme (+ Cloudflare DNS plagini), papkalar va (ixtiyoriy) SQL xotira chegarasi.

.DESCRIPTION
    Windows Server 2022 (AWS Lightsail "SQL Server 2022 Express" blueprint) uchun. Administrator PowerShell'da ishga tushiring.
    Qayta ishga tushirish xavfsiz: o'rnatilganlari o'tkazib yuboriladi.

    Xavfsizlik: har yuklab olingan fayl tekshiriladi -
      * Hosting Bundle - Microsoft rasmiy release metadata'sidagi SHA-512 + Authenticode (Microsoft imzosi);
      * URL Rewrite - qayd qilingan SHA-256 + Authenticode;
      * win-acme - qayd qilingan versiya va SHA-256 (GitHub release).
    Mos kelmasa - o'rnatmaydi va to'xtaydi.

.EXAMPLE
    Set-ExecutionPolicy -Scope Process Bypass -Force
    .\Install-ServerPrereqs.ps1 -SqlMaxMemoryMB 1024

.PARAMETER SqlMaxMemoryMB
    Berilsa - SQL Server 'max server memory' shu qiymatga o'rnatiladi (4 GB RAM'li server uchun 1024 tavsiya). 0 - tegilmaydi.

.PARAMETER SqlInstance
    SQL instance ('.' yoki '.\SQLEXPRESS'). Bo'sh bo'lsa serverdagi yagona SQL xizmatidan avtomatik aniqlanadi. Faqat -SqlMaxMemoryMB uchun.
#>
[CmdletBinding()]
param(
    [int]$SqlMaxMemoryMB = 0,
    [string]$SqlInstance = '',
    [string]$DownloadDir = "$env:TEMP\tahsinERP-prereqs",
    [string]$WinAcmeDir = 'C:\tools\win-acme',
    [string]$DotnetChannel = '10.0'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest progress bari yuklashni juda sekinlashtiradi
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# --- Qayd qilingan versiyalar va xeshlar (yangilashda shu yerni o'zgartiring) ---
$UrlRewriteUrl    = 'https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi'
$UrlRewriteSha256 = '37342FF2F585F263F34F48E9DE59EB1051D61015A8E967DBDE4075716230A32A'
$WinAcmeVersion   = '2.2.9.1701'
$WinAcmeZipSha256 = 'A2C874E9893A1D91E0329887F72C067DCC49800A49963FA7D61C5A4E09058F0C'
$CfPluginSha256   = '3261C9334AF67AA380C0479A5BE43E60BE46B4282CDD51C81DB0CE4ACE838CA2'

function Step($text) { Write-Host "`n=== $text" -ForegroundColor Cyan }
function Ok($text)   { Write-Host "  [ok] $text" -ForegroundColor Green }
function Skip($text) { Write-Host "  [--] $text" -ForegroundColor DarkGray }

function Assert-Admin {
    $id = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $id.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Administrator sifatida ishga tushiring (PowerShell -> Run as administrator).'
    }
}

function Get-File([string]$Url, [string]$Name) {
    New-Item -ItemType Directory -Force $DownloadDir | Out-Null
    $path = Join-Path $DownloadDir $Name
    Write-Host "  yuklanmoqda: $Url"
    Invoke-WebRequest -Uri $Url -OutFile $path -UseBasicParsing
    return $path
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Algorithm) {
    $actual = (Get-FileHash -Path $Path -Algorithm $Algorithm).Hash
    if ($actual -ne $Expected.ToUpperInvariant()) {
        Remove-Item $Path -Force -ErrorAction SilentlyContinue
        throw "Xesh mos kelmadi: $Path`n  kutilgan: $Expected`n  haqiqiy:  $actual"
    }
    Ok "$Algorithm tekshirildi: $(Split-Path $Path -Leaf)"
}

function Assert-MicrosoftSigned([string]$Path) {
    $sig = Get-AuthenticodeSignature -FilePath $Path
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "Microsoft imzosi yaroqsiz: $Path ($($sig.Status); $($sig.SignerCertificate.Subject))"
    }
    Ok "Microsoft imzosi yaroqli: $(Split-Path $Path -Leaf)"
}

Assert-Admin

# ---------------------------------------------------------------------------------------------------------------
Step '1/6 IIS rollari va .NET Framework 4.8 (ERP uchun)'
$features = 'Web-Server', 'Web-Asp-Net45', 'Web-WebSockets', 'Web-Http-Redirect', 'Web-Dyn-Compression',
            'Web-Mgmt-Console', 'Web-Scripting-Tools', 'NET-Framework-45-ASPNET'
$missing = $features | Where-Object { -not (Get-WindowsFeature $_).Installed }
if ($missing) {
    $r = Install-WindowsFeature -Name $missing
    if (-not $r.Success) { throw "IIS rollari o'rnatilmadi: $($missing -join ', ')" }
    Ok "o'rnatildi: $($missing -join ', ')"
    if ($r.RestartNeeded -eq 'Yes') { Write-Warning 'Server qayta yuklanishi kerak - skript oxirida qayta yuklang va yana ishga tushiring.' }
} else { Skip 'IIS rollari allaqachon bor' }

$net = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
if ($net -ge 528040) { Ok ".NET Framework 4.8+ bor (release $net)" } else { throw ".NET Framework 4.8 topilmadi (release $net) - Windows Update'ni o'rnating." }

# ---------------------------------------------------------------------------------------------------------------
Step "2/6 ASP.NET Core $DotnetChannel Hosting Bundle (panel, bot, sayt, agent)"
$hasRuntime = $false
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (Test-Path $dotnet) {
    $hasRuntime = [bool](& $dotnet --list-runtimes | Select-String -SimpleMatch "Microsoft.AspNetCore.App $DotnetChannel.")
}
$hasModule = Test-Path "$env:windir\System32\inetsrv\aspnetcorev2.dll"
if ($hasRuntime -and $hasModule) {
    Skip "ASP.NET Core $DotnetChannel runtime va IIS moduli allaqachon bor"
} else {
    $meta = Invoke-RestMethod -UseBasicParsing "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$DotnetChannel/releases.json"
    $rel  = $meta.releases | Where-Object { $_.'release-version' -eq $meta.'latest-release' } | Select-Object -First 1
    $file = $rel.'aspnetcore-runtime'.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' } | Select-Object -First 1
    if (-not $file) { throw "Hosting Bundle metadata'da topilmadi ($DotnetChannel)." }
    $exe = Get-File $file.url "dotnet-hosting-$($meta.'latest-release')-win.exe"
    Assert-Hash $exe $file.hash 'SHA512'
    Assert-MicrosoftSigned $exe
    $p = Start-Process $exe -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
    if ($p.ExitCode -notin 0, 3010) { throw "Hosting Bundle o'rnatilmadi (exit $($p.ExitCode))." }
    Ok "Hosting Bundle $($meta.'latest-release') o'rnatildi"
}

# ---------------------------------------------------------------------------------------------------------------
Step '3/6 IIS URL Rewrite 2.1 (HTTP -> HTTPS)'
if (Test-Path "$env:windir\System32\inetsrv\rewrite.dll") {
    Skip 'URL Rewrite allaqachon bor'
} else {
    $msi = Get-File $UrlRewriteUrl 'rewrite_amd64_en-US.msi'
    Assert-Hash $msi $UrlRewriteSha256 'SHA256'
    Assert-MicrosoftSigned $msi
    $p = Start-Process msiexec.exe -ArgumentList '/i', "`"$msi`"", '/quiet', '/norestart' -Wait -PassThru
    if ($p.ExitCode -notin 0, 3010) { throw "URL Rewrite o'rnatilmadi (exit $($p.ExitCode))." }
    Ok 'URL Rewrite 2.1 o''rnatildi'
}

# ---------------------------------------------------------------------------------------------------------------
Step "4/6 win-acme $WinAcmeVersion + Cloudflare DNS plagini -> $WinAcmeDir"
if (Test-Path (Join-Path $WinAcmeDir 'wacs.exe')) {
    Skip "win-acme allaqachon bor ($WinAcmeDir)"
} else {
    $base = "https://github.com/win-acme/win-acme/releases/download/v$WinAcmeVersion"
    $zip  = Get-File "$base/win-acme.v$WinAcmeVersion.x64.pluggable.zip" "win-acme.v$WinAcmeVersion.x64.pluggable.zip"
    Assert-Hash $zip $WinAcmeZipSha256 'SHA256'
    $cf   = Get-File "$base/plugin.validation.dns.cloudflare.v$WinAcmeVersion.zip" "plugin.validation.dns.cloudflare.v$WinAcmeVersion.zip"
    Assert-Hash $cf $CfPluginSha256 'SHA256'
    New-Item -ItemType Directory -Force $WinAcmeDir | Out-Null
    Expand-Archive -Path $zip -DestinationPath $WinAcmeDir -Force
    Expand-Archive -Path $cf  -DestinationPath $WinAcmeDir -Force
    # Faqat administratorlar va SYSTEM (yangilash vazifasi) - sertifikat kalitlari va Cloudflare tokeni shu yerda saqlanadi
    icacls $WinAcmeDir /inheritance:r /grant:r 'Administrators:(OI)(CI)F' 'SYSTEM:(OI)(CI)F' | Out-Null
    Ok "win-acme o'rnatildi: $WinAcmeDir\wacs.exe"
}

# ---------------------------------------------------------------------------------------------------------------
Step '5/6 Papkalar'
$dirs = 'C:\ProgramData\tahsinERP', 'C:\ProgramData\tahsinERP\Backups', 'C:\inetpub\tahsinERP', 'C:\inetpub\tahsinERP.ControlPanel',
        'C:\inetpub\tahsinERP.Bot', 'C:\inetpub\tahsinERP.Site', 'C:\tahsinERP\Agent'
foreach ($d in $dirs) {
    if (Test-Path $d) { Skip $d } else { New-Item -ItemType Directory -Force $d | Out-Null; Ok $d }
}

# ---------------------------------------------------------------------------------------------------------------
Step '6/6 SQL Server'
$svc = Get-Service 'MSSQL*' -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'MSSQL$*' -or $_.Name -eq 'MSSQLSERVER' }
if (-not $svc) {
    Write-Warning 'SQL Server xizmati topilmadi - "SQL Server 2022 Express" blueprint tanlanganini tekshiring yoki SQL Express''ni o''rnating.'
} else {
    $found = @()
    foreach ($s in $svc) {
        $inst = if ($s.Name -eq 'MSSQLSERVER') { '.' } else { '.\' + $s.Name.Substring(6) }
        $found += $inst
        Ok "SQL xizmati: $($s.Name) ($($s.Status)) -> qo'llanmadagi '.\SQLEXPRESS' o'rniga: $inst"
    }
    if (-not $SqlInstance) {
        if ($found.Count -eq 1) { $SqlInstance = $found[0] }
        else { $SqlInstance = '.\SQLEXPRESS'; Write-Warning "Bir nechta SQL xizmati topildi - -SqlInstance bilan aniq ko'rsating." }
    }
    if ($SqlMaxMemoryMB -gt 0) {
        $cs = "Server=$SqlInstance;Database=master;Integrated Security=True;TrustServerCertificate=True"
        $cn = New-Object System.Data.SqlClient.SqlConnection $cs
        try {
            $cn.Open()
            $cmd = $cn.CreateCommand()
            $cmd.CommandText = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'max server memory (MB)', $SqlMaxMemoryMB; RECONFIGURE;"
            [void]$cmd.ExecuteNonQuery()
            Ok "SQL 'max server memory' = $SqlMaxMemoryMB MB ($SqlInstance)"
        } catch {
            Write-Warning "SQL xotira chegarasi qo'yilmadi ($SqlInstance): $($_.Exception.Message). -SqlInstance ni tekshiring yoki SSMS'da qo'lda bajaring."
        } finally { $cn.Dispose() }
    } else { Skip 'SQL xotira chegarasi (-SqlMaxMemoryMB berilmagan)' }
}

Step 'Yakun'
& "$env:windir\System32\iisreset.exe" /noforce | Out-Null
Ok 'IIS qayta ishga tushirildi'
Write-Host @"

Keyingi qadamlar (01_DEPLOY_GUIDE.md):
  2-bo'lim - Cloudflare DNS va win-acme sertifikatlari (ERP IIS sayti yaratilgandan keyin)
  3-bo'lim - SQL login va huquqlar
  4-bo'lim - ERP publish va IIS sayti
"@
