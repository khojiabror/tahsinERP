/*
 FIN-B27 (foydalanuvchi qarori 2026-10-18): Doimiy harajatlar (FinRecurringObligations) o'z ruxsat moduliga ega bo'ladi.
 Avval CRUD FinDashboard ruxsati bilan tekshirilardi — dashboardni o'zgartira oladigan har kim majburiyatlarni
 tahrirlay olardi. Kod: FinRecurringObligationsController [Permission("FinRecurringObligations")].

 Hozirgi kirishlar saqlanishi uchun FinDashboard moduli berilgan rollarga yangi modul AYNAN shu
 huquqlar (View/Change/Delete) bilan beriladi. Keyin Admin > Ruxsatlar sahifasida moslang.
 Qayta ishga tushirsa bo'ladi (bor yozuvlar takrorlanmaydi).
 MUHIM: skript kod bilan birga (yoki undan oldin) bajarilishi kerak — aks holda sahifaga hech kim kira olmaydi.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES WHERE Module = 'FinRecurringObligations')
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    VALUES ('FinRecurringObligations', 'FinRecurringObligations', 'Index', NULL);
    PRINT 'PERMISSIONMODULES: FinRecurringObligations qo''shildi';
END

DECLARE @moduleId INT = (SELECT MIN(ID) FROM dbo.PERMISSIONMODULES WHERE Module = 'FinRecurringObligations');

INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
SELECT p.ViewPermit, p.ChangePermit, p.DeletePermit, p.RoleID, @moduleId
FROM dbo.PERMISSIONS p
JOIN dbo.PERMISSIONMODULES m ON m.ID = p.PermissionModuleID
WHERE m.Module = 'FinDashboard'
  AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = p.RoleID AND x.PermissionModuleID = @moduleId);
PRINT CONCAT('PERMISSIONS: ', @@ROWCOUNT, ' ta rolga FinRecurringObligations huquqi berildi');

COMMIT TRAN;

-- Tekshirish
SELECT p.RoleID, p.ViewPermit, p.ChangePermit, p.DeletePermit
FROM dbo.PERMISSIONS p
JOIN dbo.PERMISSIONMODULES m ON m.ID = p.PermissionModuleID
WHERE m.Module = 'FinRecurringObligations'
ORDER BY p.RoleID;
