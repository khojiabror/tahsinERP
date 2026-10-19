/* =====================================================================
   012 — HSCode (ТНВЭД kodlari) sahifasining ruxsat modullari.
   HSCodeController [Permission] + [ActionViewPermission] bilan himoyalangan, lekin PERMISSIONMODULES'da
   'HSCode' yozuvi yo'q edi — hech kim (Admin ham) kira olmasdi: ViewNotAuthorized.
   Unit (O'lchov birliklari) kabi 5 ta yozuv qo'shiladi; rollarga Unit'ning AYNAN shu amalidagi huquqlari
   ko'chiriladi (spravochnik — kim birliklarni ko'ra/o'zgartira olsa, ТНВЭД kodlarini ham).
   Idempotent. Bo'sh bazada (yangi tenant, seed hali ishlamagan) hech narsa qilmaydi — u yerda
   Seed/105_PermissionModules_Extra.sql qo'shadi (aks holda seed'ning qat'iy ID'lari bilan to'qnashadi).
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    BEGIN TRAN;

    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Module, 'HSCode', v.Action, NULL
    FROM (VALUES ('HSCode', 'Index'), ('HSCodeCreate', 'Create'), ('HSCodeEdit', 'Edit'),
                 ('HSCodeDetails', 'Details'), ('HSCodeDelete', 'Delete')) v (Module, Action)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Controller = 'HSCode' AND m.Action = v.Action);

    INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
    SELECT p.ViewPermit, p.ChangePermit, p.DeletePermit, p.RoleID, h.ID
    FROM dbo.PERMISSIONMODULES h
    JOIN dbo.PERMISSIONMODULES u ON u.Controller = 'Unit' AND u.Action = h.Action AND u.Parameter IS NULL
    JOIN dbo.PERMISSIONS p ON p.PermissionModuleID = u.ID
    WHERE h.Controller = 'HSCode'
      AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = p.RoleID AND x.PermissionModuleID = h.ID);

    COMMIT TRAN;
END
