/* =====================================================================
   tahsinERP — ixcham namuna: Distributsiya — hudud (Toshkent shahri → Chilonzor), bitta savdo nuqtasi (standart narx turi,
   limit 10 mln, 7 kun), sotiladigan namuna mahsulotga distributsiya kartasi va standart narx turida narx.
   Agent, ombor qoldig'i va buyurtma yaratilmaydi — ular xodim va inventarizatsiyadan keyin.
   Idempotent: dst.TERRITORIES bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @admin int = (SELECT TOP 1 u.ID FROM dbo.USERS u JOIN dbo.USERROLES ur ON ur.UserID = u.ID
                      JOIN dbo.ROLES r ON r.ID = ur.RoleID AND r.SystemCode = 'ADMIN' WHERE ISNULL(u.IsDeleted, 0) = 0 ORDER BY u.ID);
IF dbo.fn_SetupHasModule('Commerce') = 0 OR @admin IS NULL OR EXISTS (SELECT 1 FROM dst.TERRITORIES)
BEGIN
    PRINT 'Namuna distributsiya: modul tanlanmagan, admin yo''q yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @now datetime2(0) = SYSDATETIME();
DECLARE @today date = CAST(@now AS date);
DECLARE @pt int = (SELECT TOP 1 ID FROM dst.PRICE_TYPES WHERE IsDefault = 1 AND IsActive = 1);

/* ---------------- hudud ---------------- */
INSERT INTO dst.TERRITORIES (ParentID, Level, Name, NameRu, IsActive) VALUES (NULL, 1, N'Toshkent shahri', N'г. Ташкент', 1);
DECLARE @city int = SCOPE_IDENTITY();
INSERT INTO dst.TERRITORIES (ParentID, Level, Name, NameRu, IsActive) VALUES (@city, 2, N'Chilonzor tumani', N'Чиланзарский р-н', 1);
DECLARE @district int = SCOPE_IDENTITY();

/* ---------------- savdo nuqtasi ---------------- */
INSERT INTO dbo.CUSTOMERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Namuna" do''koni', 'Chilonzor-9, bozor yonida', 'O''zbekiston', '+998900000003', NULL, 'Do''kon egasi', 'Do''kon egasi', 0, NULL, 'DST', 'Toshkent');
DECLARE @customer int = SCOPE_IDENTITY();
INSERT INTO dst.CUSTOMER_PROFILES (CustomerID, INN, Phone2, OwnerName, Landmark, Lat, Lng, PhotoKey, PointType, Category, TerritoryID, AgentUserID,
                                   PriceTypeID, VisitDays, CreditLimit, PaymentDays, IsApproved, ApprovedBy, ApprovedAt, CreatedVia, CreatedBy, CreatedAt)
VALUES (@customer, NULL, NULL, N'Do''kon egasi', N'Chilonzor-9, bozor yonida', NULL, NULL, NULL, 'SHOP', 'B', @district, NULL,
        @pt, 0, 10000000, 7, 1, @admin, @now, 'web', @admin, @now);

/* ---------------- sotiladigan namuna mahsulotlar: distributsiya kartasi va narx ---------------- */
INSERT INTO dst.PRODUCT_PROFILES (ProductID, NameRu, BrandID, ManufacturerID, SubCategoryID, IsSaleActive, IsBulky, IsWeighted, BaseWeightKg, BaseVolumeM3)
SELECT p.ID, NULL, NULL, NULL, NULL, 1, 0, 0, NULL, NULL
FROM dbo.PRODUCTS p
WHERE p.PNo IN ('NAM-001', 'NAM-003') AND p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dst.PRODUCT_PROFILES x WHERE x.ProductID = p.ID);
IF @pt IS NOT NULL
    INSERT INTO dst.PRICE_LIST_ITEMS (PriceTypeID, ProductID, Price, MinPrice, ValidFrom, CreatedBy, CreatedAt)
    SELECT @pt, p.ID, 100000, 90000, @today, @admin, @now
    FROM dbo.PRODUCTS p
    WHERE p.PNo IN ('NAM-001', 'NAM-003') AND p.IsDeleted = 0;
