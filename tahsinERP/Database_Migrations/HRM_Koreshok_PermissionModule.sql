-- HRM-S01 davomi: Koreshok endi login + [Permission] talab qiladi (core, 0-bosqich).
-- [Permission] (Controller='Koreshok') yozuvi PERMISSIONMODULES'da bo'lmasa, sahifaga HECH KIM kira olmaydi.
-- Bu skript modul yozuvini qo'shadi va HrPayroll'ni ko'ra oladigan rollarga Koreshok'ni ko'rish huquqini beradi.
-- Ishga tushirishdan oldin tekshiring; keyin Admin > Ruxsatlar sahifasida kerakli rollarni moslang.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Mavjud yozuvlarni ko'rish
SELECT * FROM dbo.PERMISSIONMODULES WHERE Controller = 'Koreshok' OR Module = 'Koreshok';

-- 2) Modul yozuvi (yo'q bo'lsa)
IF NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES WHERE Controller = 'Koreshok')
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    VALUES ('Koreshok', 'Koreshok', 'Index', NULL);
END

DECLARE @koreshokId INT = (SELECT MIN(ID) FROM dbo.PERMISSIONMODULES WHERE Controller = 'Koreshok');

-- 3) HrPayroll'ni ko'ra oladigan rollarga Koreshok ko'rish huquqi (o'zgartirish huquqi kerak emas: Calculate — o'qish amali)
INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
SELECT DISTINCT 1, 0, 0, p.RoleID, @koreshokId
FROM dbo.PERMISSIONS p
JOIN dbo.PERMISSIONMODULES m ON m.ID = p.PermissionModuleID
WHERE (m.Module = 'HrPayroll' OR m.Controller = 'HrPayroll')
  AND p.ViewPermit = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = p.RoleID AND x.PermissionModuleID = @koreshokId);

SELECT p.*, r.* FROM dbo.PERMISSIONS p LEFT JOIN dbo.ROLES r ON r.ID = p.RoleID WHERE p.PermissionModuleID = @koreshokId;

-- Natija to'g'ri bo'lsa COMMIT qiling. Keyin ilovada ruxsat keshi 10 daqiqada (yoki Ruxsatlar sahifasida saqlanganda) yangilanadi.
-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
