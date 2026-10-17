-- =====================================================================
-- MES sxema o'zgarishi (audit MES-B20, MES-P12) — foydalanuvchi tasdiqlagan (2026-10-17).
--
--  1) PRODUCT_PRODUCTION.PlanDate (date NULL) — ishlab chiqarish qaysi REJA kuniga tegishli.
--     Avval Comment ichidagi "[RejaSana:yyyy-MM-dd]" matnida va eski QUALITY_INSPECTION.ProductPlanDailyID
--     bog'lanishida saqlanardi. Har ekran (ProductPlan, Dashboard, Hisobot) har xil qoida ishlatardi.
--     Endi bitta qoida: reja kuni = ISNULL(PlanDate, Date).
--  2) EXPENSES.ProductionID, INCOMES.ProductionID (int NULL, FK -> PRODUCT_PRODUCTION) — avtomatik xomashyo
--     chiqimi / tayyor mahsulot kirimi qaysi ishlab chiqarishniki. Avval faqat Description'dagi
--     "ProductionID:x" markeri bor edi (LIKE '%...%' — indekssiz to'liq skan). Marker Description'da qoladi.
--
-- MUHIM: ushbu skript ilovaning yangi versiyasini (branch claude/opt-mes-904a4d) o'rnatishdan OLDIN
-- ishga tushirilishi kerak — EF modeli bu ustunlarni kutadi, ustun bo'lmasa so'rovlar xato beradi.
-- Qayta ishga tushirish xavfsiz (har qadam mavjudligini tekshiradi). Avval test nusxada.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------------
-- 1. Ustunlar
-- ---------------------------------------------------------------------
IF COL_LENGTH('dbo.PRODUCT_PRODUCTION', 'PlanDate') IS NULL
    ALTER TABLE dbo.PRODUCT_PRODUCTION ADD PlanDate date NULL;
IF COL_LENGTH('dbo.EXPENSES', 'ProductionID') IS NULL
    ALTER TABLE dbo.EXPENSES ADD ProductionID int NULL;
IF COL_LENGTH('dbo.INCOMES', 'ProductionID') IS NULL
    ALTER TABLE dbo.INCOMES ADD ProductionID int NULL;
GO

-- ---------------------------------------------------------------------
-- 2. PlanDate backfill. Ustunlik tartibi:
--    a) Comment'dagi [RejaSana:yyyy-MM-dd]
--    b) eski sifat nazorati bog'lanishi (QI.ProductPlanDailyID -> PRODUCTPLANS_DAILY.Day), eng ko'p tekshirilgan kun
--    c) fakt sanasi (Date)
-- ---------------------------------------------------------------------
BEGIN TRANSACTION;

UPDATE p
SET p.PlanDate = TRY_CONVERT(date, SUBSTRING(c.CommentText, c.MetaPos + 10, 10), 23)
FROM dbo.PRODUCT_PRODUCTION p
CROSS APPLY (SELECT CAST(p.Comment AS varchar(max)) AS CommentText) t
CROSS APPLY (SELECT t.CommentText, CHARINDEX('[RejaSana:', t.CommentText) AS MetaPos) c
WHERE p.PlanDate IS NULL
  AND c.MetaPos > 0
  AND TRY_CONVERT(date, SUBSTRING(c.CommentText, c.MetaPos + 10, 10), 23) IS NOT NULL;
PRINT CONCAT('PlanDate (Comment meta): ', @@ROWCOUNT);

;WITH Links AS (
    SELECT qi.ProductionID, CAST(pd.[Day] AS date) AS PlanDay, SUM(qi.QtyInspected) AS Qty
    FROM dbo.QUALITY_INSPECTION qi
    JOIN dbo.PRODUCTPLANS_DAILY pd ON pd.ID = qi.ProductPlanDailyID
    WHERE qi.IsDeleted = 0 AND qi.ProductionID IS NOT NULL
    GROUP BY qi.ProductionID, CAST(pd.[Day] AS date)
), Ranked AS (
    SELECT ProductionID, PlanDay,
           ROW_NUMBER() OVER (PARTITION BY ProductionID ORDER BY Qty DESC, PlanDay DESC) AS rn
    FROM Links
)
UPDATE p
SET p.PlanDate = r.PlanDay
FROM dbo.PRODUCT_PRODUCTION p
JOIN Ranked r ON r.ProductionID = p.ID AND r.rn = 1
WHERE p.PlanDate IS NULL;
PRINT CONCAT('PlanDate (eski QI bog''lanishi): ', @@ROWCOUNT);

UPDATE dbo.PRODUCT_PRODUCTION SET PlanDate = [Date] WHERE PlanDate IS NULL;
PRINT CONCAT('PlanDate (fakt sanasi): ', @@ROWCOUNT);

COMMIT TRANSACTION;
GO

-- ---------------------------------------------------------------------
-- 3. ProductionID backfill (Description'dagi "ProductionID:<raqam>" markeridan)
-- ---------------------------------------------------------------------
BEGIN TRANSACTION;

UPDATE e
SET e.ProductionID = x.ProdID
FROM dbo.EXPENSES e
CROSS APPLY (SELECT CAST(e.Description AS varchar(max)) AS D) t
CROSS APPLY (SELECT PATINDEX('%ProductionID:[0-9]%', t.D) AS Pos) a
CROSS APPLY (SELECT SUBSTRING(t.D, a.Pos + 13, 12) AS Tail) b
CROSS APPLY (SELECT TRY_CONVERT(int, LEFT(b.Tail, PATINDEX('%[^0-9]%', b.Tail + 'x') - 1)) AS ProdID) x
WHERE e.ProductionID IS NULL
  AND a.Pos > 0
  AND x.ProdID IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.PRODUCT_PRODUCTION p WHERE p.ID = x.ProdID);
PRINT CONCAT('EXPENSES.ProductionID: ', @@ROWCOUNT);

UPDATE i
SET i.ProductionID = x.ProdID
FROM dbo.INCOMES i
CROSS APPLY (SELECT CAST(i.Description AS varchar(max)) AS D) t
CROSS APPLY (SELECT PATINDEX('%ProductionID:[0-9]%', t.D) AS Pos) a
CROSS APPLY (SELECT SUBSTRING(t.D, a.Pos + 13, 12) AS Tail) b
CROSS APPLY (SELECT TRY_CONVERT(int, LEFT(b.Tail, PATINDEX('%[^0-9]%', b.Tail + 'x') - 1)) AS ProdID) x
WHERE i.ProductionID IS NULL
  AND a.Pos > 0
  AND x.ProdID IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.PRODUCT_PRODUCTION p WHERE p.ID = x.ProdID);
PRINT CONCAT('INCOMES.ProductionID: ', @@ROWCOUNT);

COMMIT TRANSACTION;
GO

-- ---------------------------------------------------------------------
-- 4. FK va indekslar
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.FK_EXPENSES_PRODUCT_PRODUCTION', 'F') IS NULL
    ALTER TABLE dbo.EXPENSES WITH CHECK ADD CONSTRAINT FK_EXPENSES_PRODUCT_PRODUCTION
        FOREIGN KEY (ProductionID) REFERENCES dbo.PRODUCT_PRODUCTION(ID);
IF OBJECT_ID('dbo.FK_INCOMES_PRODUCT_PRODUCTION', 'F') IS NULL
    ALTER TABLE dbo.INCOMES WITH CHECK ADD CONSTRAINT FK_INCOMES_PRODUCT_PRODUCTION
        FOREIGN KEY (ProductionID) REFERENCES dbo.PRODUCT_PRODUCTION(ID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EXPENSES') AND name = 'IX_EXPENSES_ProductionID')
    CREATE NONCLUSTERED INDEX IX_EXPENSES_ProductionID ON dbo.EXPENSES(ProductionID) INCLUDE (IsDeleted, ReceiverWhID) WHERE ProductionID IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.INCOMES') AND name = 'IX_INCOMES_ProductionID')
    CREATE NONCLUSTERED INDEX IX_INCOMES_ProductionID ON dbo.INCOMES(ProductionID) INCLUDE (IsDeleted) WHERE ProductionID IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PRODUCT_PRODUCTION') AND name = 'IX_PRODUCT_PRODUCTION_PlanDate')
    CREATE NONCLUSTERED INDEX IX_PRODUCT_PRODUCTION_PlanDate ON dbo.PRODUCT_PRODUCTION(PlanDate) INCLUDE (ProductID, ShopID, [Date], Total, IsDeleted);
GO

-- ---------------------------------------------------------------------
-- 5. Tekshiruv
-- ---------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM dbo.PRODUCT_PRODUCTION WHERE PlanDate IS NULL) AS ProductionsWithoutPlanDate,
    (SELECT COUNT(*) FROM dbo.PRODUCT_PRODUCTION WHERE PlanDate <> [Date]) AS ProductionsPlanDiffersFromFact,
    (SELECT COUNT(*) FROM dbo.EXPENSES WHERE ProductionID IS NULL AND CAST(Description AS varchar(max)) LIKE '%ProductionID:[0-9]%') AS ExpensesMarkerNotLinked,
    (SELECT COUNT(*) FROM dbo.INCOMES WHERE ProductionID IS NULL AND CAST(Description AS varchar(max)) LIKE '%ProductionID:[0-9]%') AS IncomesMarkerNotLinked;
GO
