<#
.SYNOPSIS
    Panel bazasidan (tahsin_control) sinov paytida kiritilgan hisob-faktura va to'lovlarni tozalaydi; bitta firmani
    to'lovsiz sana gacha ishlatadi, sinov firmalariga tegmaydi, qolgan firmalarni yopadi (obuna bekor, holat To'xtatilgan).

.DESCRIPTION
    Standart - faqat KO'RSATADI (hech narsa o'zgarmaydi). -Apply bilan: avval tahsin_control zaxirasi, keyin bitta
    tranzaksiyada:
      * barcha to'lovlar, hisob-faktura qatorlari va hisob-fakturalar o'chiriladi, raqamlash qaytadan boshlanadi;
        bank importi va portal so'rovlaridagi havolalar bo'shatiladi;
      * -KeepSlug firma: eski obunalari bekor, yangi KORP (+ ilgari bo'lsa Sayt-katalog) -KeepUntil gacha, holat Faol;
      * -TrialSlugs firmalari (sinov/demo): obunasiga tegilmaydi;
      * qolgan firmalar: faol/kutilayotgan obunalari bekor, holati To'xtatilgan (arxivlangan - o'zgarmaydi);
      * auditga "ops.clear-test-billing" yoziladi.
    Keyin panelda -KeepSlug firma sahifasida "Kalitni obunadan yangilash", ERP'da Litsenziya -> "Hozir tekshirish".
    Administrator PowerShell'da. Faqat ASCII (PowerShell 5.1).

.EXAMPLE
    .\Clear-TestBilling.ps1                                    # nima o'zgarishini ko'rsatadi
    .\Clear-TestBilling.ps1 -TrialSlugs smarttech -Apply       # bajaradi
#>
[CmdletBinding()]
param(
    [string] $KeepSlug = 'ipack',
    [string] $KeepUntil = '2026-12-31',
    [string[]] $TrialSlugs,
    [string] $SqlServer = '.',
    [string] $Database = 'tahsin_control',
    [string] $BackupDir = 'C:\tahsinERP\Backups',
    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
$until = [DateTime]::ParseExact($KeepUntil, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$cs = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True"

function Query([string]$sql) {
    $cn = New-Object System.Data.SqlClient.SqlConnection $cs
    try {
        $cn.Open(); $c = $cn.CreateCommand(); $c.CommandText = $sql
        $t = New-Object System.Data.DataTable; $t.Load($c.ExecuteReader()); return ,$t
    } finally { $cn.Dispose() }
}

$tenants = Query "SELECT Id, Slug, LegalName, Status FROM Tenants ORDER BY Id"
$keep = $tenants | Where-Object { $_.Slug -eq $KeepSlug }
if (-not $keep) { throw "Firma topilmadi: $KeepSlug" }
if (-not $TrialSlugs) { $TrialSlugs = @($tenants | Where-Object { $_.Slug -like '*smart*' } | ForEach-Object { $_.Slug }) }
$TrialSlugs = @($TrialSlugs | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
foreach ($s in $TrialSlugs) { if (-not ($tenants | Where-Object { $_.Slug -eq $s })) { throw "Sinov firmasi topilmadi: $s" } }

$statusName = @{ 1 = 'Sinov'; 2 = 'Faol'; 3 = 'Imtiyozli'; 4 = "To'xtatilgan"; 5 = 'Arxiv' }
Write-Host "`n=== Firmalar" -ForegroundColor Cyan
foreach ($t in $tenants) {
    $plan = if ($t.Slug -eq $KeepSlug) { "QOLADI: KORP $KeepUntil gacha, to'lovsiz" }
            elseif ($TrialSlugs -contains $t.Slug) { 'QOLADI: sinov obunasiga tegilmaydi' }
            elseif ($t.Status -eq 5) { "arxivda - o'zgarmaydi" }
            else { "YOPILADI: obunalar bekor, To'xtatilgan" }
    '{0,4} {1,-20} {2,-12} {3}' -f $t.Id, $t.Slug, $statusName[[int]$t.Status], $plan
}

$inv = Query "SELECT i.Id, ISNULL(i.Number, 'qoralama') AS Number, t.Slug, i.Status, i.TotalTiyin, (SELECT COUNT(*) FROM Payments p WHERE p.InvoiceId = i.Id) AS Payments FROM Invoices i JOIN Tenants t ON t.Id = i.TenantId ORDER BY i.Id"
Write-Host "`n=== O'chiriladigan hisob-fakturalar: $($inv.Rows.Count)" -ForegroundColor Cyan
foreach ($i in $inv) { '{0,4} {1,-14} {2,-20} {3,14:N0} so''m, to''lovlar: {4}' -f $i.Id, $i.Number, $i.Slug, ([long]$i.TotalTiyin / 100), $i.Payments }
$subs = Query "SELECT s.Id, t.Slug, p.Code, s.Period, s.StartDate, s.EndDate, s.Status FROM Subscriptions s JOIN Tenants t ON t.Id = s.TenantId JOIN Plans p ON p.Id = s.PlanId WHERE s.Status IN (1, 2) ORDER BY t.Slug, s.Id"
Write-Host "`n=== Hozirgi faol/kutilayotgan obunalar" -ForegroundColor Cyan
foreach ($s in $subs) { '{0,4} {1,-20} {2,-6} {3:yyyy-MM-dd} - {4:yyyy-MM-dd}' -f $s.Id, $s.Slug, $s.Code, $s.StartDate, $s.EndDate }

if (-not $Apply) {
    Write-Host "`nHech narsa o'zgarmadi. Bajarish: .\Clear-TestBilling.ps1 -TrialSlugs $($TrialSlugs -join ',') -Apply" -ForegroundColor Yellow
    exit 0
}

# ---------------------------------------------------------------------------------------------------------------
New-Item -ItemType Directory -Force $BackupDir | Out-Null
$bak = Join-Path $BackupDir ("tahsin_control_before_cleanup_" + (Get-Date -Format 'yyyyMMdd_HHmm') + '.bak')
$cnm = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=master;Integrated Security=True;TrustServerCertificate=True"
try { $cnm.Open(); $c = $cnm.CreateCommand(); $c.CommandTimeout = 600; $c.CommandText = "BACKUP DATABASE [$Database] TO DISK = N'$($bak.Replace("'", "''"))' WITH COPY_ONLY, CHECKSUM, INIT"; [void]$c.ExecuteNonQuery() }
finally { $cnm.Dispose() }
Write-Host "[ok] zaxira: $bak"

$keepId = [int]$keep.Id
$trialIds = @($tenants | Where-Object { $TrialSlugs -contains $_.Slug } | ForEach-Object { [int]$_.Id })
$protected = (@($keepId) + $trialIds) -join ','
$untilSql = $until.ToString('yyyy-MM-dd')
$sql = @"
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @today date = CAST(SYSUTCDATETIME() AS date), @now datetime2 = SYSUTCDATETIME();
DECLARE @hadSite bit = CASE WHEN EXISTS (SELECT 1 FROM Subscriptions s JOIN Plans p ON p.Id = s.PlanId WHERE s.TenantId = $keepId AND p.Code = 'SITE') THEN 1 ELSE 0 END;

UPDATE BankImportLines SET InvoiceId = NULL, PaymentId = NULL WHERE InvoiceId IS NOT NULL OR PaymentId IS NOT NULL;
UPDATE PortalRequests SET InvoiceId = NULL WHERE InvoiceId IS NOT NULL;
DELETE FROM Payments;
DELETE FROM InvoiceLines;
DELETE FROM Invoices;
DELETE FROM InvoiceSequences;

-- saqlanadigan firma: eski obunalar bekor, yangi KORP (+SITE) to'lovsiz
UPDATE Subscriptions SET Status = 4 WHERE TenantId = $keepId AND Status IN (1, 2, 3);
INSERT INTO Subscriptions (TenantId, PlanId, Users, Period, StartDate, EndDate, AutoRenew, Status, CreatedAtUtc)
SELECT $keepId, p.Id, 50, 2, @today, '$untilSql', 0, 2, @now FROM Plans p WHERE p.Code = 'KORP';
INSERT INTO Subscriptions (TenantId, PlanId, Users, Period, StartDate, EndDate, AutoRenew, Status, CreatedAtUtc)
SELECT $keepId, p.Id, 0, 2, @today, '$untilSql', 0, 2, @now FROM Plans p WHERE p.Code = 'SITE' AND @hadSite = 1;
UPDATE Tenants SET Status = 2 WHERE Id = $keepId;

-- qolganlar (sinov firmalari va arxivdan tashqari): yopiladi
UPDATE Subscriptions SET Status = 4 WHERE TenantId NOT IN ($protected) AND Status IN (1, 2);
UPDATE Tenants SET Status = 4 WHERE Id NOT IN ($protected) AND Status <> 5;

INSERT INTO AuditLog (AtUtc, Action, Entity, EntityId, [After])
VALUES (@now, 'ops.clear-test-billing', 'Tenant', '$keepId', N'{"keep":"$KeepSlug","until":"$untilSql","trial":"$($TrialSlugs -join ',')"}');
COMMIT;
"@
$cn = New-Object System.Data.SqlClient.SqlConnection $cs
try { $cn.Open(); $c = $cn.CreateCommand(); $c.CommandTimeout = 300; $c.CommandText = $sql; [void]$c.ExecuteNonQuery() }
finally { $cn.Dispose() }
Write-Host "[ok] tozalandi: hisob-faktura va to'lovlar o'chirildi; $KeepSlug - KORP $KeepUntil gacha; boshqa firmalar yopildi" -ForegroundColor Green
Write-Host @"

Keyingi qadamlar:
  1. Panel -> Tashkilotlar -> $KeepSlug -> "Kalitni obunadan yangilash"
  2. ERP ($KeepSlug.tahsinerp.uz) -> Litsenziya -> "Hozir tekshirish"
  Qaytarish kerak bo'lsa: $bak dan tahsin_control ni tiklang (panel pool'ini to'xtatib).
"@
