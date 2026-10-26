/* =====================================================================
   tahsinERP — DEMO: yakun (Core, har doim) — ombor qoldiqlari va ssenariy jadvallarini tozalash.

   PRODUCT_STOCKS barcha kirim/chiqim harakatlaridan hisoblanadi (qaysi modullar qo'llangan bo'lsa ham); biror vaqtda
   qoldiq manfiy bo'lsa THROW (demo hujjatlari bir-biriga mos emas). So'ng demo_tmp sxemasi o'chiriladi.
   Idempotent: PRODUCT_STOCKS bo'sh bo'lmasa qoldiq hisoblanmaydi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.PRODUCT_STOCKS)
BEGIN
    CREATE TABLE #mv (WH int, ProductID int, At datetime, Ord int, Qty float, UnitID int);
    INSERT INTO #mv SELECT i.ReceivingWHID, ip.ProductID, i.IssueDateTime, 0, ip.Amount, ip.UnitID
    FROM dbo.INCOMES i JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID WHERE i.RecieveStatus = 1 AND ISNULL(i.IsDeleted, 0) = 0;
    INSERT INTO #mv SELECT e.SenderWHID, ep.ProductID, e.IssueDateTime, 1, -ep.Amount, ep.UnitID
    FROM dbo.EXPENSES e JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ExpenseID = e.ID WHERE e.IsDeleted = 0;
    IF EXISTS (SELECT 1 FROM (SELECT SUM(Qty) OVER (PARTITION BY WH, ProductID ORDER BY At, Ord ROWS UNBOUNDED PRECEDING) AS Bal FROM #mv) x WHERE x.Bal < -0.0001)
        THROW 50105, 'Demo: biror vaqtda ombor qoldig''i manfiy bo''lib qoldi', 1;
    INSERT INTO dbo.PRODUCT_STOCKS (WHID, ProductID, Amount, UnitID)
    SELECT WH, ProductID, ROUND(SUM(Qty), 4), MIN(UnitID) FROM #mv GROUP BY WH, ProductID HAVING ROUND(SUM(Qty), 4) > 0;
END

IF SCHEMA_ID(N'demo_tmp') IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'DROP TABLE demo_tmp.' + QUOTENAME(name) + N';' FROM sys.tables WHERE schema_id = SCHEMA_ID(N'demo_tmp');
    EXEC (@sql);
    EXEC (N'DROP SCHEMA demo_tmp');
END

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #mv;

PRINT 'Demo yakun: OK';
