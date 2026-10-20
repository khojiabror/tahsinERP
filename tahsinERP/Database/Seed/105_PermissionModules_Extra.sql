/* =====================================================================
   105 — 100_Reference eksportidan keyin qo'shilgan ruxsat modullari (yangi tenant bazalari uchun).
   Mavjud bazalarga xuddi shu yozuvlarni NNN migratsiyalari qo'shadi (012_HSCode_PermissionModules, 013_DST_Foundation, 016_DST_Directories, 018_DST_Orders, 019_DST_Money).
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

-- Distributsiya ma'lumotnomalari (016_DST_Directories): har controller uchun bitta yozuv
INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
SELECT v.Controller, v.Controller, 'Index', NULL
FROM (VALUES ('DstTerritories'), ('DstCustomers'), ('DstProducts'), ('DstDictionaries'), ('DstPrices'),
             ('DstTeams'), ('DstSettings'), ('DstImport')) v (Controller)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Module = v.Controller);
GO

-- Distributsiya buyurtmalari va qoldiqlar (018_DST_Orders)
INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
SELECT v.Controller, v.Controller, 'Index', NULL
FROM (VALUES ('DstOrders'), ('DstStock')) v (Controller)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Module = v.Controller);
GO

-- Distributsiya to'lovlari, qaytarishlar, qarzlar (019_DST_Money)
INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
SELECT v.Controller, v.Controller, 'Index', NULL
FROM (VALUES ('DstPayments'), ('DstReturns'), ('DstDebts')) v (Controller)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Module = v.Controller);
GO

-- Sayt-katalog admin paneli (021_SITE)
INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
SELECT v.Controller, v.Controller, 'Index', NULL
FROM (VALUES ('SiteHome'), ('SiteSettings'), ('SiteCatalog'), ('SitePages'), ('SiteLeads')) v (Controller)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES m WHERE m.Module = v.Controller);
GO
