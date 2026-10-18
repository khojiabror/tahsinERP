<#
.SYNOPSIS
    Etalon bazadan Database/Schema/000_Baseline.sql ni qayta generatsiya qiladi (SqlPackage / DacFx).

.DESCRIPTION
    Odatda KERAK EMAS: sxema o'zgarishi = yangi Migrations/NNN fayl. Bu skript faqat "squash" uchun —
    ko'p migratsiya to'planib, hali hech bir tenant bazasi eski baseline'dan yaratilmagan bo'lsa,
    yoki barcha tenantlar baseline+migratsiyalarga to'liq mos bo'lsa (keyin ular uchun -BaselineExisting).

    Qadamlar: manba -> dacpac -> vaqtinchalik bazaga publish -> chiqariladigan obyektlarni o'chirish
    (sysdiagrams, diagram sp_*/fn_*, *_BAK_*, DataFix_*, SCHEMA_VERSIONS, -ExtraDrop) -> dacpac -> bo'sh
    bazaga nisbatan CREATE skripti -> sqlcmd o'zgaruvchilaridan tozalash -> 000_Baseline.sql.
    -FixupSql: chiqarishdan oldin vaqtinchalik bazada bajariladigan SQL (masalan master'ga hali merge
    bo'lmagan branch o'zgarishlarini qaytarish). Natijani EDMX bilan solishtirish: README "Baseline".

.EXAMPLE
    .\Export-Baseline.ps1 -SourceDatabase DBTHSN-IPACK -ExtraDrop SHOP_PLANNED_DTS
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [Parameter(Mandatory = $true)][string]$SourceDatabase,
    [string[]]$ExtraDrop = @(),
    [string]$FixupSql,
    [string]$SqlPackage,
    [string]$OutFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Schema\000_Baseline.sql'),
    [string]$WorkDir = (Join-Path $env:TEMP 'tahsinERP.baseline')
)
$ErrorActionPreference = 'Stop'

if (-not $SqlPackage) {
    $SqlPackage = @(
        (Get-Command sqlpackage -ErrorAction SilentlyContinue | ForEach-Object Source)
        Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\Common7\IDE\Extensions\Microsoft\SQLDB\DAC\SqlPackage.exe' -ErrorAction SilentlyContinue | ForEach-Object FullName
        Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\DAC\bin\SqlPackage.exe' -ErrorAction SilentlyContinue | ForEach-Object FullName
    ) | Where-Object { $_ } | Select-Object -First 1
}
if (-not $SqlPackage) { throw 'SqlPackage.exe topilmadi (-SqlPackage bilan bering yoki "dotnet tool install -g microsoft.sqlpackage").' }
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$tmpSrc = 'TMP_BASELINE_SRC_' + (Get-Random)
$tmpEmpty = 'TMP_BASELINE_EMPTY_' + (Get-Random)

function Sql([string]$db, [string]$q) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Data Source=$Server;Initial Catalog=$db;Integrated Security=True"
    $c.Open(); try { $cmd = $c.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $q; [void]$cmd.ExecuteNonQuery() } finally { $c.Dispose() }
}
function Run-SqlPackage([string[]]$a) {
    & $SqlPackage @a | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "SqlPackage xato: $($a[0])" }
}

try {
    Run-SqlPackage @('/Action:Extract', "/SourceServerName:$Server", "/SourceDatabaseName:$SourceDatabase", "/TargetFile:$WorkDir\src.dacpac", '/OverwriteFiles:True', '/p:VerifyExtraction=false', '/p:ExtractAllTableData=false', '/SourceTrustServerCertificate:True')
    Run-SqlPackage @('/Action:Publish', "/SourceFile:$WorkDir\src.dacpac", "/TargetServerName:$Server", "/TargetDatabaseName:$tmpSrc", '/TargetTrustServerCertificate:True', '/p:ExcludeObjectTypes=Users;Logins;RoleMembership;Permissions;Credentials;DatabaseScopedCredentials')

    $extra = ($ExtraDrop | ForEach-Object { "N'" + $_.Replace("'", "''") + "'" }) -join ','
    if (-not $extra) { $extra = "N''" }
    Sql $tmpSrc @"
DECLARE @s nvarchar(max) = N'';
SELECT @s += N'DROP TABLE dbo.' + QUOTENAME(name) + N';' FROM sys.tables
 WHERE name = N'sysdiagrams' OR name = N'SCHEMA_VERSIONS' OR name LIKE N'%[_]BAK[_]%' OR name LIKE N'DataFix[_]%' OR name IN ($extra);
SELECT @s += N'DROP PROCEDURE dbo.' + QUOTENAME(name) + N';' FROM sys.procedures
 WHERE name IN (N'sp_alterdiagram', N'sp_creatediagram', N'sp_dropdiagram', N'sp_helpdiagramdefinition', N'sp_helpdiagrams', N'sp_renamediagram', N'sp_upgraddiagrams');
IF OBJECT_ID(N'dbo.fn_diagramobjects') IS NOT NULL SET @s += N'DROP FUNCTION dbo.fn_diagramobjects;';
EXEC (@s);
"@
    if ($FixupSql) { Sql $tmpSrc ([IO.File]::ReadAllText($FixupSql)) }

    Run-SqlPackage @('/Action:Extract', "/SourceServerName:$Server", "/SourceDatabaseName:$tmpSrc", "/TargetFile:$WorkDir\baseline.dacpac", '/OverwriteFiles:True', '/p:VerifyExtraction=false', '/SourceTrustServerCertificate:True')
    Sql 'master' "CREATE DATABASE [$tmpEmpty] COLLATE Cyrillic_General_CI_AS"
    Run-SqlPackage @('/Action:Script', "/SourceFile:$WorkDir\baseline.dacpac", "/TargetServerName:$Server", "/TargetDatabaseName:$tmpEmpty", '/TargetTrustServerCertificate:True', "/OutputPath:$WorkDir\baseline_raw.sql", '/OverwriteFiles:True', '/p:IncludeTransactionalScripts=false', '/p:ScriptDatabaseOptions=false', '/p:DropObjectsNotInSource=false', '/p:ExcludeObjectTypes=Users;Logins;RoleMembership;Permissions;Credentials;DatabaseScopedCredentials')

    # sqlcmd sarlavhasi, USE [$(DatabaseName)] va PRINT batch'larini olib tashlash
    $raw = [IO.File]::ReadAllText("$WorkDir\baseline_raw.sql").TrimStart([char]0xFEFF).Replace("`r`n", "`n")
    $batches = [regex]::Split("`n" + $raw + "`n", '\n\s*GO\s*\n')
    $out = New-Object System.Collections.Generic.List[string]
    $started = $false
    foreach ($b in $batches) {
        $s = $b.Trim()
        if (-not $s) { continue }
        if (-not $started) { if ($s.StartsWith('USE [$(DatabaseName)]')) { $started = $true }; continue }
        if ($s.StartsWith('USE [$(DatabaseName)]')) { continue }
        if ($s -match "^PRINT N'[^']*';$") { continue }
        if ($s.Contains('$(')) { throw "sqlcmd o'zgaruvchisi qoldi: $($s.Substring(0, [Math]::Min(200, $s.Length)))" }
        $out.Add($s)
    }
    $header = @"
/* =====================================================================
   tahsinERP — BASELINE SXEMA (000)
   Yangi korxona bazasining toza sxemasi: jadvallar, PK/FK/UNIQUE/CHECK/DEFAULT,
   indekslar, view, trigger, saqlanadigan protseduralar. MA'LUMOT YO'Q.

   Manba: $SourceDatabase (master EDMX bilan solishtirilgan), SqlPackage (DacFx) orqali generatsiya
   qilingan (Database/tools/Export-Baseline.ps1) — QO'LDA TAHRIR QILMANG. Keyingi har sxema o'zgarishi
   = Database/Migrations/NNN_nomi.sql (qarang: Database/README.md).
   Kirmaydi: sysdiagrams va diagram sp_*, *_BAK_* va DataFix_* jadvallari, foydalanuvchilar/huquqlar.

   Ishga tushirish: Database/tools/Invoke-DbMigrations.ps1 (bo'sh bazada avtomatik 000 ni qo'llaydi).
   Baza collation: Cyrillic_General_CI_AS (New-TenantDb.ps1 shunday yaratadi).
   ===================================================================== */
SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO
"@
    $text = ($header.Replace("`r`n", "`n") + "`n" + ($out -join "`nGO`n") + "`nGO`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($OutFile, $text, (New-Object Text.UTF8Encoding $true))
    Write-Host "Yozildi: $OutFile ($($out.Count) batch)"
}
finally {
    foreach ($d in @($tmpSrc, $tmpEmpty)) {
        try { Sql 'master' "IF DB_ID(N'$d') IS NOT NULL BEGIN ALTER DATABASE [$d] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$d]; END" } catch { Write-Warning $_ }
    }
}
