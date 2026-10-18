/* =====================================================================
   Audit_2026_10_SRM_Warehouse_Schema.sql — SRM Ombor/Inventar sxema o'zgarishlari
   (foydalanuvchi ruxsati bilan, 2026-10-18). Idempotent: qayta ishga tushirish xavfsiz.
   Avval bazaning zaxira nusxasini oling.

     S1. WAREHOUSES.WarehouseType (SUPPLY | PRODUCTION | SALES)       — SRM-B12, P14
         Ombor roli nom bo'yicha taxmin qilinmaydi; backfill eski kalit so'zlar bilan bir martalik.
     S2. INVENTORY_ADJUSTMENTS (ombor + oy bo'yicha yagona)            — SRM-P07
         USERLOGS'dagi "InventoryAdjustment / Apply-WH-n" yozuvlaridan backfill.
     S3. INCOME_PRODUCTS.VATRate                                       — SRM-M04
         Izohdagi "QQS %: n%" dan (yo'q bo'lsa jami/asosiy summadan) backfill; izohdan matn olib tashlanadi.
     S4. INCOMES.Currency (ishlatilmaydi, hamma qiymat NULL) olib tashlanadi; valyuta — CurrencyID.
   ===================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

/* ---------------- S1. WAREHOUSES.WarehouseType ---------------- */
IF COL_LENGTH('dbo.WAREHOUSES', 'WarehouseType') IS NULL
BEGIN
    ALTER TABLE dbo.WAREHOUSES ADD WarehouseType varchar(20) NULL;
    PRINT 'S1: WAREHOUSES.WarehouseType qo''shildi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_WAREHOUSES_WarehouseType')
    ALTER TABLE dbo.WAREHOUSES ADD CONSTRAINT CK_WAREHOUSES_WarehouseType
        CHECK (WarehouseType IS NULL OR WarehouseType IN ('SUPPLY', 'PRODUCTION', 'SALES'));
GO
-- Backfill: eski WarehouseFlowService kalit so'zlari (faqat turi hali berilmagan omborlar).
UPDATE dbo.WAREHOUSES SET WarehouseType = 'PRODUCTION'
WHERE WarehouseType IS NULL AND ISNULL(IsDeleted, 0) = 0
  AND (UPPER(WHName) LIKE '%PRODUCTION%' OR UPPER(WHName) LIKE '%ISHLAB CHIQARISH%' OR UPPER(WHName) LIKE '%PROD%');
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SALES'
WHERE WarehouseType IS NULL AND ISNULL(IsDeleted, 0) = 0
  AND (UPPER(WHName) LIKE '%SALES%' OR UPPER(WHName) LIKE '%SOTUV%' OR UPPER(WHName) LIKE '%SALE%' OR UPPER(WHName) LIKE '%SAVDO%');
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SUPPLY'
WHERE WarehouseType IS NULL AND ISNULL(IsDeleted, 0) = 0
  AND (UPPER(WHName) LIKE '%SUPPLY%' OR UPPER(WHName) LIKE '%XOMASHYO%' OR UPPER(WHName) LIKE '%SUPPLIER%');
PRINT 'S1: ombor turlari:';
SELECT ID, WHName, WarehouseType FROM dbo.WAREHOUSES WHERE ISNULL(IsDeleted, 0) = 0 ORDER BY ID;
GO

/* ---------------- S2. INVENTORY_ADJUSTMENTS ---------------- */
IF OBJECT_ID('dbo.INVENTORY_ADJUSTMENTS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.INVENTORY_ADJUSTMENTS (
        ID          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_INVENTORY_ADJUSTMENTS PRIMARY KEY,
        WarehouseID int      NOT NULL CONSTRAINT FK_INVENTORY_ADJUSTMENTS_WAREHOUSES REFERENCES dbo.WAREHOUSES(ID),
        Period      date     NOT NULL,   -- oyning 1-kuni
        UserID      int      NULL,
        IncomeID    int      NULL,       -- INV kirim hujjati (ortiqcha)
        ExpenseID   int      NULL,       -- INV chiqim hujjati (kamomad)
        CreatedAt   datetime NOT NULL CONSTRAINT DF_INVENTORY_ADJUSTMENTS_CreatedAt DEFAULT (GETDATE())
    );
    CREATE UNIQUE INDEX UX_INVENTORY_ADJUSTMENTS_WH_Period ON dbo.INVENTORY_ADJUSTMENTS(WarehouseID, Period);
    PRINT 'S2: INVENTORY_ADJUSTMENTS yaratildi.';
END
GO
-- Backfill: USERLOGS (eski qulf) — har ombor+oy uchun birinchi yozuv.
INSERT INTO dbo.INVENTORY_ADJUSTMENTS (WarehouseID, Period, UserID, CreatedAt)
SELECT x.WarehouseID, x.Period, x.UserID, x.CreatedAt
FROM (
    SELECT TRY_CAST(SUBSTRING(CAST(l.ActionName AS nvarchar(100)), 10, 20) AS int) AS WarehouseID,
           DATEFROMPARTS(YEAR(l.DateTime), MONTH(l.DateTime), 1) AS Period,
           l.UserID, l.DateTime AS CreatedAt,
           ROW_NUMBER() OVER (PARTITION BY CAST(l.ActionName AS nvarchar(100)), YEAR(l.DateTime), MONTH(l.DateTime)
                              ORDER BY l.DateTime) AS rn
    FROM dbo.USERLOGS l
    WHERE CAST(l.ControllerName AS nvarchar(100)) = 'InventoryAdjustment'
      AND CAST(l.ActionName AS nvarchar(100)) LIKE 'Apply-WH-%'
) x
WHERE x.rn = 1 AND x.WarehouseID IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.WAREHOUSES w WHERE w.ID = x.WarehouseID)
  AND NOT EXISTS (SELECT 1 FROM dbo.INVENTORY_ADJUSTMENTS a WHERE a.WarehouseID = x.WarehouseID AND a.Period = x.Period);
PRINT 'S2: USERLOGS dan ko''chirildi: ' + CAST(@@ROWCOUNT AS varchar(20));
GO

/* ---------------- S3. INCOME_PRODUCTS.VATRate ---------------- */
IF COL_LENGTH('dbo.INCOME_PRODUCTS', 'VATRate') IS NULL
BEGIN
    ALTER TABLE dbo.INCOME_PRODUCTS ADD VATRate float NULL;
    PRINT 'S3: INCOME_PRODUCTS.VATRate qo''shildi.';
END
GO
BEGIN TRAN;
-- 3a. izohdagi "QQS %: n%" (IncomesController.MergeIncomeCommentWithVat formati: "<izoh> | QQS %: n%" yoki "QQS %: n%")
;WITH c AS (
    SELECT ip.ID, CAST(ip.Comment AS nvarchar(max)) AS Txt,
           PATINDEX('%QQS [%]:%', CAST(ip.Comment AS nvarchar(max))) AS Pos
    FROM dbo.INCOME_PRODUCTS ip
    WHERE ip.VATRate IS NULL AND ip.Comment IS NOT NULL
), p AS (
    SELECT ID, Txt, Pos, LTRIM(SUBSTRING(Txt, Pos + 6, 30)) AS Rest FROM c WHERE Pos > 0
)
UPDATE ip SET ip.VATRate = CASE WHEN v.Rate < 0 THEN 0 WHEN v.Rate > 100 THEN 100 ELSE v.Rate END
FROM dbo.INCOME_PRODUCTS ip
JOIN p ON p.ID = ip.ID
CROSS APPLY (SELECT TRY_CAST(REPLACE(LEFT(p.Rest, NULLIF(CHARINDEX('%', p.Rest), 0) - 1), ',', '.') AS float) AS Rate) v
WHERE v.Rate IS NOT NULL;
PRINT 'S3a: izohdan olingan QQS: ' + CAST(@@ROWCOUNT AS varchar(20));

-- 3b. izohda yo'q — jami va asosiy summadan (IncomesController.GetVatRateForIncomeLine bilan bir xil qoida)
UPDATE ip SET ip.VATRate =
    CASE
        WHEN ip.Amount * ISNULL(ip.PiecePrice, 0) <= 0.0000001 THEN 0
        WHEN ip.TotalPrice IS NULL OR ABS(ip.TotalPrice - ip.Amount * ip.PiecePrice) < 0.02 THEN 0
        WHEN (ip.TotalPrice / (ip.Amount * ip.PiecePrice) - 1.0) * 100.0 BETWEEN -0.5 AND 100.5
            THEN CASE WHEN ROUND((ip.TotalPrice / (ip.Amount * ip.PiecePrice) - 1.0) * 100.0, 4) < 0 THEN 0
                      WHEN ROUND((ip.TotalPrice / (ip.Amount * ip.PiecePrice) - 1.0) * 100.0, 4) > 100 THEN 100
                      ELSE ROUND((ip.TotalPrice / (ip.Amount * ip.PiecePrice) - 1.0) * 100.0, 4) END
        ELSE 0
    END
FROM dbo.INCOME_PRODUCTS ip
WHERE ip.VATRate IS NULL;
PRINT 'S3b: summadan hisoblangan QQS: ' + CAST(@@ROWCOUNT AS varchar(20));

-- 3c. izohdan "QQS %: n%" qismini olib tashlash (endi ustunda)
;WITH c AS (
    SELECT ip.ID, CAST(ip.Comment AS nvarchar(max)) AS Txt
    FROM dbo.INCOME_PRODUCTS ip
    WHERE CAST(ip.Comment AS nvarchar(max)) LIKE '%QQS [%]:%[%]'
)
UPDATE ip SET ip.Comment =
    CASE
        WHEN CHARINDEX(' | QQS %:', c.Txt) > 0 THEN NULLIF(RTRIM(LEFT(c.Txt, CHARINDEX(' | QQS %:', c.Txt) - 1)), '')
        WHEN c.Txt LIKE 'QQS [%]:%' THEN NULL
        ELSE ip.Comment
    END
FROM dbo.INCOME_PRODUCTS ip JOIN c ON c.ID = ip.ID;
PRINT 'S3c: izohdan QQS matni olib tashlandi: ' + CAST(@@ROWCOUNT AS varchar(20));
COMMIT;
GO

/* ---------------- S4. INCOMES.Currency ---------------- */
IF COL_LENGTH('dbo.INCOMES', 'Currency') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.INCOMES WHERE Currency IS NOT NULL)
        PRINT 'S4: INCOMES.Currency da qiymatlar bor — olib tashlanmadi (avval CurrencyID ga ko''chiring).';
    ELSE
    BEGIN
        ALTER TABLE dbo.INCOMES DROP COLUMN Currency;
        PRINT 'S4: INCOMES.Currency olib tashlandi.';
    END
END
GO
PRINT 'SRM ombor sxema migratsiyasi tugadi.';
