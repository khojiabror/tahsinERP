/* =====================================================================
   036 — Rollarga asosiy menyu (Layout) ko'rish huquqi.
   Sinov/demo ochishda Setup/300_ModuleProfile.sql rollar ruxsatini PERMISSIONMODULES.Controller bo'yicha berardi;
   'Layout' yozuvining Controller'i yo'q — huquq tushmasdi va rol egasi kirganda menyusiz bo'sh sahifa ko'rardi
   (_Layout.cshtml: IsViewPermitted(user, "Layout")). Profil skripti tuzatildi; bu migratsiya allaqachon ochilgan
   bazalarni tuzatadi: biror bo'limni ko'ra oladigan har bir faol rolga Layout ko'rish huquqi.
   Bo'sh bazada (seed hali ishlamagan) hech narsa qilmaydi. Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @layout int = (SELECT MIN(ID) FROM dbo.PERMISSIONMODULES WHERE Module = 'Layout');
IF @layout IS NOT NULL
BEGIN
    UPDATE p SET ViewPermit = 1
    FROM dbo.PERMISSIONS p
    JOIN dbo.ROLES r ON r.ID = p.RoleID AND ISNULL(r.IsDeleted, 0) = 0
    WHERE p.PermissionModuleID = @layout AND p.ViewPermit = 0
      AND EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = p.RoleID AND x.ViewPermit = 1 AND x.PermissionModuleID <> @layout);

    INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
    SELECT 1, 0, 0, r.ID, @layout
    FROM dbo.ROLES r
    WHERE ISNULL(r.IsDeleted, 0) = 0
      AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = r.ID AND x.PermissionModuleID = @layout)
      AND EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = r.ID AND x.ViewPermit = 1);
END
