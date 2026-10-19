/* =====================================================================
   tahsinERP — yangi korxona uchun standart qatorlar (qo'lda yoziladi; 100_Reference.sql dan keyin).
   Idempotent. Faqat New-TenantDb.ps1 orqali (Invoke-DbMigrations.ps1 -Seed) yangi bazada ishlaydi.
   ===================================================================== */
SET NOCOUNT ON;
GO

-- Korxona (Web.config companyID = 1). Nomi/rekvizitlarini New-TenantDb.ps1 -CompanyName yozadi.
SET IDENTITY_INSERT dbo.COMPANIES ON;
INSERT INTO dbo.COMPANIES (ID, Name, Country, City, Address, DUNS, Telephone, Email, IsActive, DirectorName)
SELECT 1, N'Yangi korxona', N'O''zbekiston', NULL, NULL, NULL, NULL, NULL, 1, NULL
WHERE NOT EXISTS (SELECT 1 FROM dbo.COMPANIES WHERE ID = 1);
SET IDENTITY_INSERT dbo.COMPANIES OFF;
GO

-- Ishlab chiqarish sexi va uchta asosiy ombor. Kod omborni WarehouseType bo'yicha topadi (WarehouseRoles):
-- SUPPLY — xarid kirimi, PRODUCTION — ishlab chiqarish, SALES — sotuv invoysi chiqimi, balans ombor bahosi.
SET IDENTITY_INSERT dbo.SHOPS ON;
INSERT INTO dbo.SHOPS (ID, CompanyID, ShopName, Description, IsDeleted)
SELECT 1, 1, N'Ishlab chiqarish', N'Asosiy ishlab chiqarish sexi', 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.SHOPS WHERE ID = 1);
SET IDENTITY_INSERT dbo.SHOPS OFF;
GO
SET IDENTITY_INSERT dbo.WAREHOUSES ON;
INSERT INTO dbo.WAREHOUSES (ID, WHName, Description, IsDeleted, MRP, ShopID, Account)
SELECT v.ID, v.WHName, v.Description, 0, NULL, v.ShopID, NULL
FROM (VALUES
    (3, N'Xomashyo ombori',         N'Xarid qilingan xomashyo va materiallar birinchi shu omborga kiradi', CAST(NULL AS int)),
    (6, N'Ishlab chiqarish ombori', N'Ishlab chiqarish sexiga tegishli ombor',                               1),
    (7, N'Savdo ombori',            N'Tayyor mahsulot; sotuv invoysi shu ombordan chiqim qiladi',            1)
) v (ID, WHName, Description, ShopID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.WAREHOUSES w WHERE w.ID = v.ID);
SET IDENTITY_INSERT dbo.WAREHOUSES OFF;
GO
UPDATE w SET WarehouseType = v.WarehouseType
FROM dbo.WAREHOUSES w
JOIN (VALUES (3, 'SUPPLY'), (6, 'PRODUCTION'), (7, 'SALES')) v (ID, WarehouseType) ON v.ID = w.ID
WHERE w.WarehouseType IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.WAREHOUSES x WHERE x.WarehouseType = v.WarehouseType AND ISNULL(x.IsDeleted, 0) = 0);
GO

-- Doimiy harajatlar qarzi (FIN_RECURRING_OBLIGATIONS, migratsiya 003) balans kategoriyasi
SET IDENTITY_INSERT dbo.FIN_BALANCE_CATEGORIES ON;
INSERT INTO dbo.FIN_BALANCE_CATEGORIES (ID, Name, Type, ParentID, SortOrder, IsActive, CalculationType, IsDeleted, SystemCode)
SELECT 12, N'Doimiy harajatlar qarzi', 2, NULL, 12, 1, 3, 0, 'RECURRING_OBLIGATIONS'
WHERE NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORIES WHERE ID = 12 OR SystemCode = 'RECURRING_OBLIGATIONS');
SET IDENTITY_INSERT dbo.FIN_BALANCE_CATEGORIES OFF;
GO

-- Aksiyador hissalari (ShareholderController) — tizim kategoriyasi
SET IDENTITY_INSERT dbo.FIN_CATEGORIES ON;
INSERT INTO dbo.FIN_CATEGORIES (ID, Name, Type, ParentID, SortOrder, IsActive, IsDeleted, SystemCode)
SELECT 32, N'Aksiyador hissalari', 1, NULL, 999, 1, 0, 'SHAREHOLDER_CONTRIBUTION'
WHERE NOT EXISTS (SELECT 1 FROM dbo.FIN_CATEGORIES WHERE ID = 32 OR SystemCode = 'SHAREHOLDER_CONTRIBUTION');
SET IDENTITY_INSERT dbo.FIN_CATEGORIES OFF;
GO

-- Etalon bazadagi qisqartma nomlarni tushunarli qilish (faqat hali o'zgartirilmagan bo'lsa)
UPDATE c SET Name = v.NewName
FROM dbo.FIN_CATEGORIES c
JOIN (VALUES
    (12, N'Sotuv otx', N'Chiqindi sotuvi'),
    (15, N'Bank xiz',  N'Bank xizmatlari'),
    (23, N'M.Tash',    N'Material tashuvi'),
    (29, N'T.Tash',    N'Tayyor mahsulot tashuvi'),
    (17, N'Divident',  N'Dividend')
) v (ID, OldName, NewName) ON v.ID = c.ID AND c.Name = v.OldName;
GO

-- Tashuv kategoriyalari tizim kodi (FinancePostingService yo'l varaqasi to'lovi; migratsiya 009 bilan bir xil)
UPDATE c SET SystemCode = v.Code
FROM dbo.FIN_CATEGORIES c
JOIN (VALUES (23, 'MATERIAL_TRANSPORT'), (29, 'FG_TRANSPORT')) v (ID, Code) ON v.ID = c.ID
WHERE c.SystemCode IS NULL AND c.Type = 2
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_CATEGORIES x WHERE x.SystemCode = v.Code);
GO

-- Ustav kapitali <- aksiyador hissalari (balans CHARTER_CAPITAL qatori; migratsiya 009 bilan bir xil)
INSERT INTO dbo.FIN_BALANCE_CATEGORY_CATEGORIES (BalanceCategoryID, CategoryID)
SELECT b.ID, c.ID
FROM dbo.FIN_BALANCE_CATEGORIES b CROSS JOIN dbo.FIN_CATEGORIES c
WHERE b.SystemCode = 'CHARTER_CAPITAL' AND c.SystemCode = 'SHAREHOLDER_CONTRIBUTION'
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORY_CATEGORIES x WHERE x.BalanceCategoryID = b.ID AND x.CategoryID = c.ID);
GO

-- Yangi korxona admini — barcha modullarga to'liq huquq (etalon bazadagi Admin roli korxonaga moslab
-- cheklangan bo'lishi mumkin). Yangi ruxsat modullari keyin qo'shilsa — Role -> Huquqlar sahifasida.
DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE RName = N'Admin');
IF @admin IS NOT NULL
BEGIN
    UPDATE dbo.PERMISSIONS SET ViewPermit = 1, ChangePermit = 1, DeletePermit = 1 WHERE RoleID = @admin;
    INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
    SELECT 1, 1, @admin, pm.ID, 1
    FROM dbo.PERMISSIONMODULES pm
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
