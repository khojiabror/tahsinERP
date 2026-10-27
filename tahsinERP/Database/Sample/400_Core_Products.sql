/* =====================================================================
   tahsinERP — sinov korxonasi uchun ixcham namuna (New-TenantDb.ps1 -Sample; modul profili va admin'dan keyin).

   Katta "Demo Plast" ssenariysi (Demo/) o'rniga: har tanlangan bo'limda bittadan yozuv — mijoz qanday ishlashini ko'radi,
   lekin keraksiz ma'lumotni o'chirib o'tirmaydi. Har modul fayli dbo.fn_SetupHasModule bilan o'zini tekshiradi; yaratuvchi —
   birinchi Admin foydalanuvchi. Bu fayl (Core) — profil yaratgan har kategoriyaga bitta mahsulot.
   Idempotent: PRODUCTS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.PRODUCTS)
BEGIN
    PRINT 'Namuna mahsulotlar: mahsulot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @pcs int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'PCS');
DECLARE @kg int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'KG');
DECLARE @shop int = (SELECT MIN(ID) FROM dbo.SHOPS WHERE ISNULL(IsDeleted, 0) = 0);

INSERT INTO dbo.PRODUCTS (PNo, Name, Weight, Type, Description, IsDeleted, UnitID, IsRawMaterial, IsComponent, IsFinishedGood,
                          CategoryID, ShopID, CalculateByWeight)
SELECT v.PNo, v.Name, v.Weight, v.Type, N'Namuna — o''z mahsulotingiz bilan almashtiring yoki o''chiring', 0,
       CASE WHEN v.IsRaw = 1 THEN @kg ELSE @pcs END, v.IsRaw, 0, 1 - v.IsRaw, c.ID, @shop, 0
FROM (VALUES ('NAM-001', N'Namuna tovar', N'Tovarlar', 0, 1.0, 'MAXSULOT'),
             ('NAM-002', N'Namuna xomashyo', N'Xomashyo va materiallar', 1, 1.0, 'XOMASHYO'),
             ('NAM-003', N'Namuna tayyor mahsulot', N'Tayyor mahsulot', 0, 1.0, 'MAXSULOT')) v (PNo, Name, Cat, IsRaw, Weight, Type)
JOIN dbo.PRODUCT_CATEGORIES c ON c.Name = v.Cat AND ISNULL(c.IsDeleted, 0) = 0;

PRINT 'Namuna mahsulotlar: ' + CAST(@@ROWCOUNT AS varchar(10));
