<#
.SYNOPSIS
    Yangi korxona bazasini 0 dan yaratadi: CREATE DATABASE -> baseline -> migratsiyalar -> seed (-> demo) -> admin.

.DESCRIPTION
    SSMS kerak emas. Panel agenti (yoki odam) shu skriptni chaqiradi; kontrakt: Docs/ControlPanel/03_DB_PROVISIONING.md.
    Skript tenants.json, DNS/IIS va litsenziyaga TEGMAYDI; LICENSE_STATE/InstallationId YOZILMAYDI.

    Natija stdout'ning oxirgi qatorida: "RESULT {json}" — ok, exitCode, database, adminLogin, tempPassword, backup.
    Xato bo'lsa yangi yaratilgan baza o'chiriladi (-KeepOnFailure bilan qoldiriladi).

.EXAMPLE
    .\New-TenantDb.ps1 -Server localhost -DbName DB_firma1 -CompanyName '"Firma 1" MChJ'
    .\New-TenantDb.ps1 -Server localhost -DbName DB_DEMO -Demo -BackupTo C:\Backups\DB_DEMO_TEMPLATE.bak
    .\New-TenantDb.ps1 -Server localhost -DbName DB_TEMPLATE -NoAdmin -BackupTo C:\Backups\DB_TEMPLATE.bak
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [Parameter(Mandatory = $true)][string]$DbName,
    [string]$SqlUser,
    [string]$SqlPassword,

    [string]$CompanyName,
    [string]$AdminLogin = 'admin',
    [string]$AdminFullName = 'Administrator',
    [string]$AdminEmail,
    # Berilmasa tasodifiy yaratiladi (stdout RESULT'da qaytadi). Har holda birinchi kirishda almashtirish majburiy.
    [string]$AdminPassword,
    [switch]$NoAdmin,

    [switch]$Demo,
    # Seed (+demo) dan keyin, admin yaratilishidan OLDIN COPY_ONLY backup (DB_TEMPLATE.bak / DB_DEMO_TEMPLATE.bak)
    [string]$BackupTo,

    [string]$Collation = 'Cyrillic_General_CI_AS',
    [ValidateSet('SIMPLE', 'FULL')][string]$RecoveryModel = 'FULL',
    [switch]$KeepOnFailure,
    [string]$LogFile
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
# Maxfiy qiymatlar buyruq satrida ko'rinmasligi uchun env orqali ham beriladi (provisioning agenti shunday chaqiradi).
if (-not $AdminPassword -and $env:TAHSINERP_ADMIN_PASSWORD) { $AdminPassword = $env:TAHSINERP_ADMIN_PASSWORD }
if ($SqlUser -and -not $SqlPassword -and $env:TAHSINERP_SQL_PASSWORD) { $SqlPassword = $env:TAHSINERP_SQL_PASSWORD }
$EXIT_OK = 0; $EXIT_BAD_ARGS = 2; $EXIT_CONNECT = 3; $EXIT_DB_EXISTS = 7; $EXIT_ADMIN = 8; $EXIT_BACKUP = 9

$root = Split-Path -Parent $PSScriptRoot
function Write-Log([string]$Level, [string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}' -f (Get-Date), $Level, $Message
    if ($Level -eq 'ERROR') { [Console]::Error.WriteLine($line) } else { Write-Host $line }
    if ($LogFile) { Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8 }
}
$result = [ordered]@{ ok = $false; exitCode = $EXIT_OK; database = $DbName; server = $Server; adminLogin = $null; tempPassword = $null; backup = $null; error = $null }
function Finish([int]$code, [string]$err) {
    $result.exitCode = $code; $result.ok = ($code -eq 0); $result.error = $err
    if ($err) { Write-Log 'ERROR' $err }
    Write-Log 'RESULT' ($result | ConvertTo-Json -Compress)
    exit $code
}

function New-Conn([string]$db) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $Server
    if ($SqlUser) { $b['User ID'] = $SqlUser; $b['Password'] = $SqlPassword } else { $b['Integrated Security'] = $true }
    $b['Initial Catalog'] = $db
    $b['Application Name'] = 'tahsinERP.NewTenantDb'
    $b['Connect Timeout'] = 30
    $c = New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
    $c.Open()
    return $c
}
function Exec($conn, [string]$sql, [hashtable]$p = @{}, $tran = $null) {
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 0
    if ($tran) { $cmd.Transaction = $tran }
    foreach ($k in $p.Keys) { [void]$cmd.Parameters.AddWithValue($k, $(if ($null -eq $p[$k]) { [DBNull]::Value } else { $p[$k] })) }
    return $cmd.ExecuteScalar()
}

# PasswordHasher.cs bilan bir xil: PBKDF2-HMAC-SHA256, 100 000 iteratsiya, 16 bayt salt, "$p2$" + Base64(32 bayt) '=' siz
function New-PasswordHash([string]$password) {
    $salt = New-Object byte[] 16
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $rng.GetBytes($salt); $rng.Dispose()
    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $hash = '$p2$' + ([Convert]::ToBase64String($kdf.GetBytes(32))).TrimEnd('=')
    $kdf.Dispose()
    return @{ Hash = $hash; Salt = [Convert]::ToBase64String($salt) }
}
function New-TempPassword {
    $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $bytes = New-Object byte[] 12
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $rng.GetBytes($bytes); $rng.Dispose()
    return -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}

# ---------------- tekshiruvlar ----------------
if ($DbName -notmatch '^[A-Za-z0-9_\-\.]{1,100}$') { Finish $EXIT_BAD_ARGS "Baza nomi yaroqsiz: $DbName (A-Za-z0-9_-. )" }
if ($Collation -notmatch '^[A-Za-z0-9_]+$') { Finish $EXIT_BAD_ARGS "Collation yaroqsiz: $Collation" }
if (-not $NoAdmin -and ($AdminLogin -notmatch '^[A-Za-z0-9_\.\-]{3,30}$')) { Finish $EXIT_BAD_ARGS "AdminLogin yaroqsiz (3-30 belgi, A-Za-z0-9_.-)" }
if ($AdminPassword -and $AdminPassword.Length -lt 8) { Finish $EXIT_BAD_ARGS 'AdminPassword kamida 8 belgi bo''lishi kerak' }

try { $master = New-Conn 'master' } catch { Finish $EXIT_CONNECT "SQL Server'ga ulanib bo'lmadi ($Server): $($_.Exception.Message)" }
$exists = [int](Exec $master 'SELECT COUNT(*) FROM sys.databases WHERE name = @n' @{ '@n' = $DbName })
if ($exists -gt 0) { $master.Dispose(); Finish $EXIT_DB_EXISTS "Baza allaqachon mavjud: $DbName (skript mavjud bazani o'zgartirmaydi)" }

$created = $false
$code = $EXIT_OK; $err = $null
try {
    # ---------------- 1. baza ----------------
    Write-Log 'INFO' "CREATE DATABASE [$DbName] COLLATE $Collation"
    Exec $master "CREATE DATABASE [$DbName] COLLATE $Collation" | Out-Null
    $created = $true
    Exec $master "ALTER DATABASE [$DbName] SET RECOVERY $RecoveryModel" | Out-Null

    # ---------------- 2. baseline + migratsiya + seed (+ demo) ----------------
    $runner = Join-Path $PSScriptRoot 'Invoke-DbMigrations.ps1'
    $extra = @()
    if ($Demo) {
        $demoDir = Join-Path $root 'Demo'
        if (Test-Path -LiteralPath $demoDir) { $extra = @(Get-ChildItem -LiteralPath $demoDir -Filter '*.sql' | Sort-Object Name | ForEach-Object { $_.FullName }) }
        if ($extra.Count -eq 0) { $code = $EXIT_BAD_ARGS; throw [System.Exception]::new("Demo skriptlari topilmadi: $demoDir") }
    }
    $runArgs = @{ Server = $Server; Database = $DbName; Seed = $true; DatabaseRoot = $root }
    if ($SqlUser) { $runArgs.SqlUser = $SqlUser; $runArgs.SqlPassword = $SqlPassword }
    if ($extra.Count -gt 0) { $runArgs.ExtraScripts = $extra }
    if ($LogFile) { $runArgs.LogFile = $LogFile }
    & $runner @runArgs
    if ($LASTEXITCODE -ne 0) { throw [System.Exception]::new("Migratsiya runner xato kodi: $LASTEXITCODE") }

    $db = New-Conn $DbName
    try {
        if ($CompanyName) {
            Exec $db 'UPDATE dbo.COMPANIES SET Name = @n WHERE ID = 1' @{ '@n' = $CompanyName } | Out-Null
        }

        # ---------------- 3. shablon backup (admin'siz) ----------------
        if ($BackupTo) {
            Write-Log 'INFO' "BACKUP -> $BackupTo"
            try {
                Exec $master "BACKUP DATABASE [$DbName] TO DISK = @f WITH COPY_ONLY, INIT, FORMAT, CHECKSUM" @{ '@f' = $BackupTo } | Out-Null
                $result.backup = $BackupTo
            }
            catch { $code = $EXIT_BACKUP; throw }
        }

        # ---------------- 4. admin ----------------
        if (-not $NoAdmin) {
            $tempPwd = if ($AdminPassword) { $AdminPassword } else { New-TempPassword }
            $h = New-PasswordHash $tempPwd
            $tran = $db.BeginTransaction()
            try {
                $adminRole = Exec $db "SELECT ID FROM dbo.ROLES WHERE RName = N'Admin' AND ISNULL(IsDeleted, 0) = 0" @{} $tran
                if ($null -eq $adminRole) { throw 'Seed''da Admin roli topilmadi' }
                $uid = Exec $db @"
INSERT INTO dbo.USERS (Uname, FullName, Password, Email, IsDeleted, IsActive, HashCode, CompanyID, MustChangePassword)
VALUES (@u, @f, @p, @e, 0, 1, @s, 1, 1);
SELECT CAST(SCOPE_IDENTITY() AS int);
"@ @{ '@u' = $AdminLogin; '@f' = $AdminFullName; '@p' = $h.Hash; '@e' = $AdminEmail; '@s' = $h.Salt } $tran
                Exec $db 'INSERT INTO dbo.USERROLES (UserID, RoleID) VALUES (@u, @r)' @{ '@u' = $uid; '@r' = $adminRole } $tran | Out-Null
                # Standart kassa va bank hisobi (FIN_ACCOUNTS.CreatedByUserID — USERS ga FK, shuning uchun seed'da emas)
                Exec $db @"
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @cash int = (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'CASH');
DECLARE @bank int = (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'BANK');
IF @uzs IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.FIN_ACCOUNTS)
    INSERT INTO dbo.FIN_ACCOUNTS (Code, Name, OwnerName, CurrencyID, IsCash, IsBank, IsActive, CreatedByUserID, CreatedDT, BalanceCategoryID, IsDeleted)
    VALUES ('9000', N'Kassa', NULL, @uzs, 1, 0, 1, @uid, GETDATE(), @cash, 0),
           ('9001', N'Hisob raqam', NULL, @uzs, 0, 1, 1, @uid, GETDATE(), @bank, 0);
"@ @{ '@uid' = $uid } $tran | Out-Null
                $tran.Commit()
            }
            catch { try { $tran.Rollback() } catch { }; $code = $EXIT_ADMIN; throw }
            $result.adminLogin = $AdminLogin
            $result.tempPassword = $tempPwd
            Write-Log 'INFO' "Admin yaratildi: $AdminLogin (birinchi kirishda parol almashtiriladi)"
        }
    }
    finally { $db.Dispose() }
}
catch {
    if ($code -eq $EXIT_OK) { $code = 1 }
    $err = $_.Exception.Message
}

if ($code -ne $EXIT_OK -and $created -and -not $KeepOnFailure) {
    try {
        Write-Log 'WARN' "Xato — yangi baza o'chirilmoqda: $DbName"
        [System.Data.SqlClient.SqlConnection]::ClearAllPools()
        Exec $master "ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DbName];" | Out-Null
    } catch { Write-Log 'ERROR' "Bazani o'chirib bo'lmadi: $($_.Exception.Message)" }
}
$master.Dispose()
Finish $code $err
