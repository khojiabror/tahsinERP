<#
.SYNOPSIS
    Foydalanuvchining barcha rollariga Distributsiya (Dst*) va Sayt (Site*) ruxsatlarini beradi va ERP keshini yangilaydi.

.DESCRIPTION
    Migratsiyalar Dst*/Site* ruxsatlarini faqat SystemCode='ADMIN' rolga beradi. Boshqa rolda (masalan "developer") ishlaydigan
    foydalanuvchi uchun shu skript. Bor ruxsat qayta qo'shilmaydi. Administrator PowerShell'da.

.EXAMPLE
    .\Grant-DstPermissions.ps1 -Login admin -DbName DB_ipack
#>
param(
    [Parameter(Mandatory)] [string] $Login,
    [Parameter(Mandatory)] [string] $DbName,
    [string] $SqlServer = '.',
    [string] $ErpPool = 'tahsinERP'
)
$ErrorActionPreference = 'Stop'
if ($Login -match "'" -or $DbName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Login yoki baza nomi noto''g''ri.' }

$sql = @"
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = '$Login' AND ISNULL(IsDeleted, 0) = 0);
IF @u IS NULL THROW 50001, 'Foydalanuvchi topilmadi', 1;
INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
SELECT 1, 1, 1, ur.RoleID, pm.ID
FROM dbo.USERROLES ur CROSS JOIN dbo.PERMISSIONMODULES pm
WHERE ur.UserID = @u AND (pm.Module LIKE 'Dst%' OR pm.Module LIKE 'Site%')
  AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = ur.RoleID AND p.PermissionModuleID = pm.ID);
SELECT @@ROWCOUNT;
"@
$cn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$DbName;Integrated Security=True;TrustServerCertificate=True"
try { $cn.Open(); $c = $cn.CreateCommand(); $c.CommandText = $sql; $n = $c.ExecuteScalar() }
finally { $cn.Dispose() }
Write-Host "[ok] qo'shilgan ruxsatlar: $n"

Import-Module WebAdministration
Restart-WebAppPool -Name $ErpPool
Write-Host '[ok] ERP qayta ishga tushirildi - brauzerda sahifani yangilang.'
