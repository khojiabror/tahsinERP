/* =====================================================================
   029 — Admin/Developer to'liq huquqi va omborning sexga bog'liqligi.
   1) ADMIN va DEVELOPER rollari HAMMA ruxsat modullarida ko'rish/o'zgartirish/o'chirish huquqiga ega bo'ladi
      (PModule orqali keyin qo'shilgan modullar Admin'ga faqat "ko'rish" bilan yozilardi — Create/Edit yopiq edi).
      Kod ham Admin/Developer'ni har qanday tekshiruvdan o'tkazadi (RoleHelper.IsSuper); bu qatorlar Rollar
      sahifasida to'g'ri ko'rinishi uchun.
   2) Sex (SHOPS) faqat ishlab chiqarish omboriga tegishli — boshqa turdagi omborlardan ShopID olib tashlanadi.
   Idempotent. Bo'sh bazada (seed hali ishlamagan) hech narsa qilmaydi — Seed/110 xuddi shuni qiladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE p SET ViewPermit = 1, ChangePermit = 1, DeletePermit = 1
FROM dbo.PERMISSIONS p
JOIN dbo.ROLES r ON r.ID = p.RoleID
WHERE r.SystemCode IN ('ADMIN', 'DEVELOPER')
  AND (p.ViewPermit = 0 OR p.ChangePermit = 0 OR ISNULL(p.DeletePermit, 0) = 0);

INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, DeletePermit, RoleID, PermissionModuleID)
SELECT 1, 1, 1, r.ID, m.ID
FROM dbo.ROLES r
CROSS JOIN dbo.PERMISSIONMODULES m
WHERE r.SystemCode IN ('ADMIN', 'DEVELOPER')
  AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS x WHERE x.RoleID = r.ID AND x.PermissionModuleID = m.ID);

UPDATE dbo.WAREHOUSES
SET ShopID = NULL
WHERE ShopID IS NOT NULL
  AND ISNULL(WarehouseType, '') <> 'PRODUCTION';
