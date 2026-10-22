<#
.SYNOPSIS
    Kunlik zaxira: SQL Server'dagi barcha foydalanuvchi bazalari + tahsinERP sozlamalari -> gzip -> S3 (Lightsail bucket).
    Eski nusxalar serverda va bucket'da tozalanadi (Docs/Deploy/04_BACKUP.md).

.DESCRIPTION
    Task Scheduler'dan SYSTEM sifatida ishlaydi (Install-Backup.ps1 ro'yxatdan o'tkazadi). Sozlama:
    C:\ProgramData\tahsinERP\Backup\config.json, kalitlar: s3.key (DPAPI LocalMachine, faqat SYSTEM va Administrators).
    Tashqi dastur kerak emas: S3 so'rovlari AWS Signature V4 bilan shu skriptning o'zida imzolanadi.

    Har ishga tushishda:
      1. har baza: BACKUP ... COPY_ONLY, CHECKSUM -> RESTORE VERIFYONLY -> .bak.gz
      2. config.zip: C:\ProgramData\tahsinERP (Updates, Backup, sayt keshisiz), applicationHost.config, saytlarning Web.config'lari
      3. S3: <prefix>/<yyyy-MM-dd>/<fayl>; yuklangan fayl ETag (MD5) bilan tekshiriladi
      4. tozalash: bucket'da KeepDays kundan eski kunlik nusxalar (oyning 1-kuni - KeepMonthlyDays), serverda LocalKeepDays
    Natija: Backup\last.json (Ok/xato, fayllar, hajm) va Backup\logs\*.log. Xato bo'lsa chiqish kodi 1.
    Faqat ASCII (PowerShell 5.1).

.EXAMPLE
    .\Backup-Databases.ps1                                   # zaxira (Task Scheduler shunday chaqiradi)
    .\Backup-Databases.ps1 -List                             # bucket'dagi nusxalar
    .\Backup-Databases.ps1 -Download 2026-10-23 -Database DB_ipack -OutDir C:\tahsinERP\restore
    .\Backup-Databases.ps1 -SelfTest                         # imzo funksiyasini AWS namunasi bilan tekshirish
#>
[CmdletBinding()]
param(
    [string] $ConfigDir = 'C:\ProgramData\tahsinERP\Backup',
    [switch] $List,
    [string] $Download,
    [string] $Database,
    [string] $OutDir = 'C:\tahsinERP\restore',
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.Security
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ------------------------------------------------------------------------------------------------ AWS SigV4
function Sort-Ordinal([string[]]$a) { if (-not $a) { return @() }; $c = [string[]]@($a); [Array]::Sort($c, [StringComparer]::Ordinal); $c }
function Get-Hex([byte[]]$b) { -join ($b | ForEach-Object { $_.ToString('x2') }) }
function Get-Sha256Hex([byte[]]$b) { $s = [Security.Cryptography.SHA256]::Create(); try { Get-Hex $s.ComputeHash($b) } finally { $s.Dispose() } }
function Get-Hmac([byte[]]$key, [string]$data) {
    $h = New-Object Security.Cryptography.HMACSHA256 (, $key)
    try { $h.ComputeHash([Text.Encoding]::UTF8.GetBytes($data)) } finally { $h.Dispose() }
}
function ConvertTo-UriPart([string]$s) {
    # RFC 3986: A-Z a-z 0-9 - _ . ~ dan boshqasi %XX
    $sb = New-Object Text.StringBuilder
    foreach ($b in [Text.Encoding]::UTF8.GetBytes($s)) {
        $c = [char]$b
        if (($c -ge 'A' -and $c -le 'Z') -or ($c -ge 'a' -and $c -le 'z') -or ($c -ge '0' -and $c -le '9') -or '-_.~'.Contains([string]$c)) { [void]$sb.Append($c) }
        else { [void]$sb.Append('%' + $b.ToString('X2')) }
    }
    $sb.ToString()
}

<# Authorization sarlavhasini qaytaradi. $Headers - imzolanadigan sarlavhalar (host, x-amz-date, x-amz-content-sha256 va boshqalar). #>
function Get-SigV4Authorization([string]$Method, [string]$CanonicalUri, [hashtable]$Query, [hashtable]$Headers,
                                [string]$PayloadHash, [string]$Region, [string]$AccessKey, [string]$SecretKey) {
    $amzDate = $Headers['x-amz-date']; $day = $amzDate.Substring(0, 8)
    $q = (Sort-Ordinal @($Query.Keys) | ForEach-Object { (ConvertTo-UriPart $_) + '=' + (ConvertTo-UriPart ([string]$Query[$_])) }) -join '&'
    $names = Sort-Ordinal @($Headers.Keys | ForEach-Object { $_.ToLowerInvariant() })
    $canonHeaders = ($names | ForEach-Object { $_ + ':' + ([string]$Headers[$_]).Trim() + "`n" }) -join ''
    $signed = $names -join ';'
    $canonical = "$Method`n$CanonicalUri`n$q`n$canonHeaders`n$signed`n$PayloadHash"
    $scope = "$day/$Region/s3/aws4_request"
    $toSign = "AWS4-HMAC-SHA256`n$amzDate`n$scope`n" + (Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($canonical)))
    $k = Get-Hmac ([Text.Encoding]::UTF8.GetBytes('AWS4' + $SecretKey)) $day
    $k = Get-Hmac $k $Region; $k = Get-Hmac $k 's3'; $k = Get-Hmac $k 'aws4_request'
    $sig = Get-Hex (Get-Hmac $k $toSign)
    "AWS4-HMAC-SHA256 Credential=$AccessKey/$scope, SignedHeaders=$signed, Signature=$sig"
}

if ($SelfTest) {
    # AWS hujjatidagi namuna (S3 SigV4, "GET Object" misoli)
    $h = @{ 'host' = 'examplebucket.s3.amazonaws.com'; 'range' = 'bytes=0-9'
            'x-amz-content-sha256' = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'; 'x-amz-date' = '20130524T000000Z' }
    $auth = Get-SigV4Authorization 'GET' '/test.txt' @{} $h $h['x-amz-content-sha256'] 'us-east-1' 'AKIAIOSFODNN7EXAMPLE' 'wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY'
    $want = 'f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41'
    if ($auth.EndsWith("Signature=$want")) { Write-Host '[ok] SigV4 imzo AWS namunasiga mos'; exit 0 }
    Write-Host "[!] SigV4 mos emas: $auth"; exit 1
}

# ------------------------------------------------------------------------------------------------ sozlama
$cfgPath = Join-Path $ConfigDir 'config.json'
if (-not (Test-Path $cfgPath)) { throw "Sozlama topilmadi: $cfgPath (avval Install-Backup.ps1)" }
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
# Bucket bo'sh - faqat lokal zaxira (tashqi saqlash yo'q)
$useS3 = -not [string]::IsNullOrWhiteSpace([string]$cfg.Bucket)
if (-not $useS3 -and ($List -or $Download)) { throw "Bucket sozlanmagan (faqat lokal zaxira): nusxalar $($cfg.LocalDir) da." }
if ($useS3) {
    $keyBytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $ConfigDir 's3.key')), $null, 'LocalMachine')
    $keyParts = [Text.Encoding]::UTF8.GetString($keyBytes).Split("`n")
    $accessKey = $keyParts[0].Trim(); $secretKey = $keyParts[1].Trim()
}
# Endpoint (ixtiyoriy, faqat sinov): S3 bilan mos lokal server, path-style (http://127.0.0.1:9000 -> /<bucket>/<kalit>)
$endpoint = [string]$cfg.Endpoint
if ($endpoint) { $endpointUri = [Uri]$endpoint; $s3Host = $endpointUri.Authority; $base = $endpoint.TrimEnd('/'); $pathPrefix = '/' + $cfg.Bucket }
else { $s3Host = "$($cfg.Bucket).s3.$($cfg.Region).amazonaws.com"; $base = "https://$s3Host"; $pathPrefix = '' }
$prefix = ([string]$cfg.Prefix).Trim('/')

function Invoke-S3([string]$Method, [string]$Key, [hashtable]$Query = @{}, [string]$InFile, [string]$OutFile) {
    $uri = $pathPrefix + '/' + (($Key.Split('/') | ForEach-Object { ConvertTo-UriPart $_ }) -join '/')
    if (-not $Key) { $uri = $pathPrefix + '/' }
    $amzDate = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $h = @{ 'host' = $s3Host; 'x-amz-date' = $amzDate; 'x-amz-content-sha256' = 'UNSIGNED-PAYLOAD' }
    $auth = Get-SigV4Authorization $Method $uri $Query $h 'UNSIGNED-PAYLOAD' $cfg.Region $accessKey $secretKey
    $qs = (Sort-Ordinal @($Query.Keys) | ForEach-Object { (ConvertTo-UriPart $_) + '=' + (ConvertTo-UriPart ([string]$Query[$_])) }) -join '&'
    $url = "$base$uri" + $(if ($qs) { "?$qs" } else { '' })
    $hdr = @{ 'Authorization' = $auth; 'x-amz-date' = $amzDate; 'x-amz-content-sha256' = 'UNSIGNED-PAYLOAD' }
    $p = @{ Uri = $url; Method = $Method; Headers = $hdr; UseBasicParsing = $true; TimeoutSec = 1800 }
    if ($InFile) { $p.InFile = $InFile; $p.ContentType = 'application/octet-stream' }
    if ($OutFile) { $p.OutFile = $OutFile }
    Invoke-WebRequest @p
}

function Get-S3Keys([string]$Pfx) {
    $keys = @(); $token = $null
    do {
        $q = @{ 'list-type' = '2'; 'prefix' = $Pfx }
        if ($token) { $q['continuation-token'] = $token }
        [xml]$x = (Invoke-S3 'GET' '' $q).Content
        foreach ($c in $x.ListBucketResult.Contents) { if ($c.Key) { $keys += [pscustomobject]@{ Key = $c.Key; Size = [long]$c.Size } } }
        $token = if ($x.ListBucketResult.IsTruncated -eq 'true') { $x.ListBucketResult.NextContinuationToken } else { $null }
    } while ($token)
    $keys
}

# ------------------------------------------------------------------------------------------------ -List / -Download
if ($List) {
    Get-S3Keys "$prefix/" | Sort-Object Key | ForEach-Object { '{0,-70} {1,10:N0} KB' -f $_.Key, ($_.Size / 1KB) }
    exit 0
}
if ($Download) {
    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $want = if ($Database) { "$Database.bak.gz" } else { $null }
    $items = Get-S3Keys "$prefix/$Download/" | Where-Object { -not $want -or $_.Key.EndsWith("/$want") }
    if (-not $items) { throw "Topilmadi: $prefix/$Download/$want" }
    foreach ($i in $items) {
        $name = $i.Key.Substring($i.Key.LastIndexOf('/') + 1)
        $gz = Join-Path $OutDir $name
        Invoke-S3 'GET' $i.Key -OutFile $gz | Out-Null
        if ($name.EndsWith('.gz')) {
            $target = $gz.Substring(0, $gz.Length - 3)
            $in = [IO.File]::OpenRead($gz); $out = [IO.File]::Create($target)
            try { $z = New-Object IO.Compression.GZipStream($in, [IO.Compression.CompressionMode]::Decompress); $z.CopyTo($out); $z.Dispose() }
            finally { $out.Dispose(); $in.Dispose() }
            Remove-Item $gz
            Write-Host "[ok] $target"
        } else { Write-Host "[ok] $gz" }
    }
    exit 0
}

# ------------------------------------------------------------------------------------------------ zaxira
$started = Get-Date
$stamp = $started.ToString('yyyy-MM-dd')
$localRoot = [string]$cfg.LocalDir
$work = Join-Path $localRoot $started.ToString('yyyy-MM-dd_HHmm')
$logDir = Join-Path $ConfigDir 'logs'
New-Item -ItemType Directory -Force $work, $logDir | Out-Null
$log = Join-Path $logDir ("backup-" + $started.ToString('yyyy-MM-dd_HHmm') + '.log')
function Log([string]$t) { $line = (Get-Date).ToString('HH:mm:ss') + ' ' + $t; Add-Content -Path $log -Value $line -Encoding ASCII; Write-Host $line }

function Invoke-Sql([string]$sql, [switch]$Scalar) {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$($cfg.SqlServer);Database=master;Integrated Security=True;TrustServerCertificate=True"
    try {
        $cn.Open(); $c = $cn.CreateCommand(); $c.CommandText = $sql; $c.CommandTimeout = 3600
        if ($Scalar) { return $c.ExecuteScalar() }
        $r = $c.ExecuteReader(); $rows = @(); while ($r.Read()) { $rows += $r.GetString(0) }; $r.Close(); return $rows
    } finally { $cn.Dispose() }
}

function Compress-Gzip([string]$src, [string]$dst) {
    $in = [IO.File]::OpenRead($src); $out = [IO.File]::Create($dst)
    try { $z = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Optimal); $in.CopyTo($z); $z.Dispose() }
    finally { $out.Dispose(); $in.Dispose() }
}

function Get-Md5Hex([string]$path) {
    $m = [Security.Cryptography.MD5]::Create(); $f = [IO.File]::OpenRead($path)
    try { Get-Hex $m.ComputeHash($f) } finally { $f.Dispose(); $m.Dispose() }
}

$errors = @(); $uploaded = @(); $files = @()
try {
    Log ("=== zaxira boshlandi -> " + $(if ($useS3) { "s3://$($cfg.Bucket)/$prefix/$stamp/" } else { "$work (faqat lokal)" }))
    $dbs = Invoke-Sql "SELECT name FROM sys.databases WHERE database_id > 4 AND state_desc = 'ONLINE' AND source_database_id IS NULL ORDER BY name"
    if ($cfg.Databases) { $only = @($cfg.Databases); $dbs = @($dbs | Where-Object { $only -contains $_ }) }   # ixtiyoriy: faqat shular
    Log ("bazalar: " + ($dbs -join ', '))
    foreach ($db in $dbs) {
        try {
            $bak = Join-Path $work "$db.bak"
            $q = $db.Replace(']', ']]'); $p = $bak.Replace("'", "''")
            Invoke-Sql "BACKUP DATABASE [$q] TO DISK = N'$p' WITH COPY_ONLY, CHECKSUM, INIT, FORMAT" -Scalar | Out-Null
            Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$p' WITH CHECKSUM" -Scalar | Out-Null
            Compress-Gzip $bak "$bak.gz"; Remove-Item $bak
            $files += "$bak.gz"
            Log ("[ok] {0} ({1:N0} KB)" -f $db, ((Get-Item "$bak.gz").Length / 1KB))
        } catch { $errors += "$db : $($_.Exception.Message)"; Log "[!] $db : $($_.Exception.Message)" }
    }

    # Sozlamalar: tenants.json, kalitlar papkalari, bot fayllari; IIS konfiguratsiyasi va saytlarning Web.config'lari
    try {
        $cfgZip = Join-Path $work 'config.zip'
        $zip = [IO.Compression.ZipFile]::Open($cfgZip, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $data = [string]$cfg.DataDir
            $skip = @('Updates', 'Backup', 'Site\cache') | ForEach-Object { (Join-Path $data $_).TrimEnd('\') + '\' }
            foreach ($f in Get-ChildItem $data -Recurse -File -Force -ErrorAction SilentlyContinue) {
                if ($skip | Where-Object { $f.FullName.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }) { continue }
                try { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, 'ProgramData\' + $f.FullName.Substring($data.Length).TrimStart('\')) } catch { }
            }
            # IIS: pool sozlamalari (env sirlari) va har saytning Web.config'i (machineKey)
            $appHost = "$env:windir\System32\inetsrv\config\applicationHost.config"
            if (Test-Path $appHost) {
                [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $appHost, 'iis\applicationHost.config')
                Import-Module WebAdministration
                foreach ($site in Get-Website) {
                    $wc = Join-Path ([Environment]::ExpandEnvironmentVariables($site.physicalPath)) 'Web.config'
                    if (Test-Path $wc) { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $wc, 'sites\' + $site.Name + '\Web.config') }
                }
            } else { Log '[--] IIS topilmadi - faqat ProgramData' }
        } finally { $zip.Dispose() }
        $files += $cfgZip
        Log ("[ok] config.zip ({0:N0} KB)" -f ((Get-Item $cfgZip).Length / 1KB))
    } catch { $errors += "config : $($_.Exception.Message)"; Log "[!] config : $($_.Exception.Message)" }

    # Bucket'ga yuklash (bitta PUT, ETag = MD5 bilan tekshiriladi)
    foreach ($f in $(if ($useS3) { $files } else { @() })) {
        $key = "$prefix/$stamp/" + [IO.Path]::GetFileName($f)
        try {
            $r = Invoke-S3 'PUT' $key -InFile $f
            $etag = ([string]$r.Headers['ETag']).Trim('"')
            if ($etag -ne (Get-Md5Hex $f)) { throw "ETag mos emas ($etag)" }
            $uploaded += $key
            Log "[ok] yuklandi: $key"
        } catch { $errors += "upload $key : $($_.Exception.Message)"; Log "[!] upload $key : $($_.Exception.Message)" }
    }

    # Bucket'da eski nusxalar: kunlik > KeepDays, oyning 1-kuni > KeepMonthlyDays
    if (-not $useS3) { }
    elseif ($uploaded.Count -gt 0 -and $errors.Count -eq 0) {
        try {
            $today = $started.Date; $removed = 0
            foreach ($o in Get-S3Keys "$prefix/") {
                $m = [regex]::Match($o.Key, '/(\d{4}-\d{2}-\d{2})/')
                if (-not $m.Success) { continue }
                $d = [DateTime]::ParseExact($m.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                $keep = if ($d.Day -eq 1) { [int]$cfg.KeepMonthlyDays } else { [int]$cfg.KeepDays }
                if (($today - $d).TotalDays -gt $keep) { Invoke-S3 'DELETE' $o.Key | Out-Null; $removed++ }
            }
            Log "bucket tozalash: $removed ta eski fayl o'chirildi"
        } catch { $errors += "bucket tozalash : $($_.Exception.Message)"; Log "[!] bucket tozalash : $($_.Exception.Message)" }
    } else { Log 'bucket tozalash o''tkazib yuborildi (bugungi zaxirada xato bor)' }

    # Serverdagi eski papkalar
    foreach ($d in Get-ChildItem $localRoot -Directory -ErrorAction SilentlyContinue) {
        if ($d.FullName -ne $work -and $d.CreationTime -lt $started.AddDays(-[int]$cfg.LocalKeepDays)) { Remove-Item $d.FullName -Recurse -Force }
    }
    foreach ($l in Get-ChildItem $logDir -Filter '*.log' -ErrorAction SilentlyContinue) {
        if ($l.LastWriteTime -lt $started.AddDays(-60)) { Remove-Item $l.FullName -Force }
    }
} catch {
    $errors += $_.Exception.Message; Log "[!] $($_.Exception.Message)"
}

$status = [ordered]@{
    Ok = ($errors.Count -eq 0); StartedAt = $started.ToString('s'); FinishedAt = (Get-Date).ToString('s')
    Bucket = $cfg.Bucket; Folder = $(if ($useS3) { "$prefix/$stamp" } else { $work }); Files = @($files | ForEach-Object { [IO.Path]::GetFileName($_) }); Uploaded = $uploaded; Errors = $errors
}
$status | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $ConfigDir 'last.json') -Encoding ASCII
if ($errors.Count -gt 0) {
    try {
        if (-not [Diagnostics.EventLog]::SourceExists('tahsinERP.Backup')) { New-EventLog -LogName Application -Source 'tahsinERP.Backup' }
        Write-EventLog -LogName Application -Source 'tahsinERP.Backup' -EntryType Error -EventId 1 -Message ($errors -join "`n")
    } catch { }
    Log "=== tugadi: $($errors.Count) ta xato"
    exit 1
}
Log ("=== tugadi: " + $(if ($useS3) { "$($uploaded.Count) ta fayl yuklandi" } else { "$($files.Count) ta fayl $work da" }))
exit 0
