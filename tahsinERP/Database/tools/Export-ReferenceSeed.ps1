<#
.SYNOPSIS
    Majburiy spravochnik seed'ini (Database/Seed/100_Reference.sql) etalon bazadan generatsiya qiladi.

.DESCRIPTION
    Faqat spravochniklar (valyuta, birlik, HS kodlar, ruxsat modullari, Admin roli va huquqlari,
    moliya/balans kategoriyalari, hujjat/topshiriq holatlari, transport turlari...). Tranzaksiya va
    kontragent ma'lumotlari KIRMAYDI. ID'lar saqlanadi (IDENTITY_INSERT), skript idempotent:
    har qator faqat shu kalit bazada bo'lmasa qo'shiladi.

    Qo'lda tuzatiladigan / qo'shimcha qatorlar: Seed/110_Defaults.sql (bu skript unga tegmaydi).

.EXAMPLE
    .\Export-ReferenceSeed.ps1 -Server localhost -Database DBTHSN-IPACK
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [Parameter(Mandatory = $true)][string]$Database,
    [string]$OutFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Seed\100_Reference.sql')
)
$ErrorActionPreference = 'Stop'

# Jadval, kalit ustun(lar), filtr. Tartib FK bo'yicha (ota jadval avval).
$specs = @(
    @{ T = 'CURRENCIES';              Key = 'ID' }
    @{ T = 'UNITS';                   Key = 'ID' }
    @{ T = 'HSCODES';                 Key = 'ID' }
    @{ T = 'PERMISSIONMODULES';       Key = 'ID' }
    @{ T = 'ROLES';                   Key = 'ID'; Where = "SystemCode IS NOT NULL" }
    @{ T = 'PERMISSIONS';             Key = 'ID'; Where = "RoleID IN (SELECT ID FROM dbo.ROLES WHERE SystemCode IS NOT NULL)" }
    @{ T = 'FIN_BALANCE_CATEGORIES';  Key = 'ID'; Where = "SystemCode IS NOT NULL"; Order = 'CASE WHEN ParentID IS NULL THEN 0 ELSE 1 END, ID' }
    # Umumiy moliya kategoriyalari (shaxsga/iPack'ka xos nomlar — G'ayratdan, Birja, Naqdlash... — kirmaydi)
    @{ T = 'FIN_CATEGORIES';          Key = 'ID'; Where = "ID IN (4, 8, 10, 11, 12, 15, 17, 19, 21, 23, 24, 25, 27, 29, 31)"; Order = 'CASE WHEN ParentID IS NULL THEN 0 ELSE 1 END, ID' }
    @{ T = 'FIN_PL_ITEMS';            Key = 'ID'; Order = 'CASE WHEN ParentID IS NULL THEN 0 ELSE 1 END, ID' }
    @{ T = 'FIN_PL_ITEM_CATEGORIES';  Key = 'ItemID,CategoryID'; Where = "CategoryID IN (4, 8, 10, 11, 12, 15, 17, 19, 21, 23, 24, 25, 27, 29, 31)" }
    @{ T = 'DOC_DOCUMENT_TYPES';      Key = 'ID' }
    @{ T = 'DOC_DOCUMENT_STATUS_REF'; Key = 'ID' }
    @{ T = 'APP_TASK_STATUS_REF';     Key = 'ID' }
    @{ T = 'APP_TASK_PRIORITY_REF';   Key = 'ID' }
    @{ T = 'F_TRANSPORT_TYPES';       Key = 'ID' }
    @{ T = 'PRODUCT_CATEGORIES';      Key = 'ID'; Where = "SystemCode IS NOT NULL" }
)

function Format-Value($v, [string]$type) {
    if ($v -is [DBNull] -or $null -eq $v) { return 'NULL' }
    switch -regex ($type) {
        '^(n?varchar|n?char|n?text|sysname|xml)$' { return "N'" + ([string]$v).Replace("'", "''") + "'" }
        '^(bit)$' { return $(if ($v) { '1' } else { '0' }) }
        '^(tinyint|smallint|int|bigint)$' { return ([string]$v) }
        '^(decimal|numeric|money|smallmoney)$' { return ([decimal]$v).ToString([Globalization.CultureInfo]::InvariantCulture) }
        '^(float|real)$' { return ([double]$v).ToString('R', [Globalization.CultureInfo]::InvariantCulture) }
        '^(date)$' { return "'" + ([datetime]$v).ToString('yyyy-MM-dd') + "'" }
        '^(datetime|datetime2|smalldatetime)$' { return "'" + ([datetime]$v).ToString('yyyy-MM-ddTHH:mm:ss.fff') + "'" }
        '^(uniqueidentifier)$' { return "'" + $v + "'" }
        '^(varbinary|binary|image)$' { return '0x' + (-join ($v | ForEach-Object { $_.ToString('X2') })) }
        default { throw "Qo'llab-quvvatlanmagan tur: $type" }
    }
}

$cs = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;Application Name=tahsinERP.SeedExport"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine(@"
/* =====================================================================
   tahsinERP — MAJBURIY SPRAVOCHNIK SEED (generatsiya qilingan, QO'LDA TAHRIR QILMANG)
   Manba: $Database, $(Get-Date -Format 'yyyy-MM-dd'), Database/tools/Export-ReferenceSeed.ps1
   Idempotent: har qator kaliti bazada bo'lmasa qo'shiladi; ID'lar saqlanadi (IDENTITY_INSERT).
   Tranzaksiya va kontragent ma'lumotlari kirmaydi. Qo'shimcha qatorlar: Seed/110_Defaults.sql.
   ===================================================================== */
SET NOCOUNT ON;
GO
"@)

foreach ($s in $specs) {
    $t = $s.T
    $colCmd = $conn.CreateCommand()
    $colCmd.CommandText = @"
SELECT c.name, ty.name, c.is_identity
FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(@t) AND c.is_computed = 0
ORDER BY c.column_id
"@
    [void]$colCmd.Parameters.AddWithValue('@t', "dbo.$t")
    $rd = $colCmd.ExecuteReader()
    $cols = @(); $types = @{}; $hasIdentity = $false
    while ($rd.Read()) { $cols += $rd.GetString(0); $types[$rd.GetString(0)] = $rd.GetString(1); if ($rd.GetBoolean(2)) { $hasIdentity = $true } }
    $rd.Close()

    $keys = $s.Key -split ','
    $order = if ($s.Order) { $s.Order } else { $s.Key }
    $where = if ($s.Where) { "WHERE $($s.Where)" } else { '' }
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT $(($cols | ForEach-Object { "[$_]" }) -join ', ') FROM dbo.[$t] $where ORDER BY $order"
    $rd = $cmd.ExecuteReader()
    $rows = New-Object System.Collections.Generic.List[string]
    while ($rd.Read()) {
        $vals = for ($i = 0; $i -lt $cols.Count; $i++) { Format-Value $rd.GetValue($i) $types[$cols[$i]] }
        $rows.Add('(' + ($vals -join ', ') + ')')
    }
    $rd.Close()
    Write-Host ("{0,-28} {1,6} qator" -f $t, $rows.Count)
    if ($rows.Count -eq 0) { continue }

    $colList = ($cols | ForEach-Object { "[$_]" }) -join ', '
    $join = ($keys | ForEach-Object { "t.[$_] = v.[$_]" }) -join ' AND '
    [void]$sb.AppendLine("-- ---------- $t ($($rows.Count)) ----------")
    for ($start = 0; $start -lt $rows.Count; $start += 500) {
        $chunk = $rows.GetRange($start, [Math]::Min(500, $rows.Count - $start))
        if ($hasIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT dbo.[$t] ON;") }
        [void]$sb.AppendLine("INSERT INTO dbo.[$t] ($colList)")
        [void]$sb.AppendLine("SELECT $(($cols | ForEach-Object { "v.[$_]" }) -join ', ') FROM (VALUES")
        [void]$sb.AppendLine(($chunk -join ",`r`n"))
        [void]$sb.AppendLine(") v ($colList)")
        [void]$sb.AppendLine("WHERE NOT EXISTS (SELECT 1 FROM dbo.[$t] t WHERE $join);")
        if ($hasIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT dbo.[$t] OFF;") }
        [void]$sb.AppendLine('GO')
    }
}
$conn.Close()
[IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object Text.UTF8Encoding $false))
Write-Host "Yozildi: $OutFile"
