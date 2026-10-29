/* =====================================================================
   035 — «Sotuv kanallari» ekrani (DstChannels): Telegram bot / Mini App va sayt-katalog sozlamalari bitta joyda.
   Distributsiya Savdo (Commerce) va dala (Distribution) modullariga bo'lindi — sayt va bot Savdo moduliga tayanadi,
   SAVDO paketida (agentlarsiz) ham ishlaydi (tahsinERP.Licensing/Editions/EditionModules.cs).
   Ruxsat: DstSettings'ga huquqi bor rollarga xuddi shunday; bo'sh baza — Seed/105. Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT 'DstChannels', 'DstChannels', 'Index', NULL
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = 'DstChannels');

    INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
    SELECT p.ViewPermit, p.ChangePermit, p.RoleID, ch.ID, p.DeletePermit
    FROM dbo.PERMISSIONS p
    JOIN dbo.PERMISSIONMODULES st ON st.ID = p.PermissionModuleID AND st.Module = 'DstSettings'
    CROSS JOIN (SELECT ID FROM dbo.PERMISSIONMODULES WHERE Module = 'DstChannels') ch
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = p.RoleID AND x.PermissionModuleID = ch.ID);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module = 'DstChannels'
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
