/* =====================================================================
   105 — 100_Reference eksportidan keyin qo'shilgan ruxsat modullari (yangi tenant bazalari uchun).
   Mavjud bazalarga xuddi shu yozuvlarni NNN migratsiyalari qo'shadi (012_HSCode_PermissionModules, 013_DST_Foundation).
   ID qat'iy emas (IDENTITY) — modul Controller+Action bo'yicha topiladi. Idempotent.
   Admin roliga to'liq huquqni keyingi 110_Defaults beradi.
   ===================================================================== */
SET NOCOUNT ON;

-- HSCode (ТНВЭД kodlari) — huquqlar Unit (O'lchov birliklari) bilan bir xil
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
GO

-- Distributsiya (013_DST_Foundation): modul sahifasi va maxsus huquqlar (Controller = 'Dst')
INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
SELECT v.Module, v.Controller, v.Action, NULL
FROM (VALUES ('DstHome', 'DstHome', 'Index'), ('DstSeeCost', 'Dst', 'SeeCost'),
             ('DstOverrideCredit', 'Dst', 'OverrideCredit'), ('DstDiscountUnlimited', 'Dst', 'DiscountUnlimited')) v (Module, Controller, Action)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Module = v.Module);
GO
