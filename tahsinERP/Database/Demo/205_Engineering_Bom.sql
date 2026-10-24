/* =====================================================================
   tahsinERP — DEMO: Muhandislik (Engineering) — norma, BOM va tannarx (200_Core_Master.sql dan keyin).

   NormController: NORM_INPUT_LINES -> NORMVERSIONS/NORMLINES -> BOM_HEADERS/BOM_LINES (12 tayyor mahsulot, demo_tmp.BOM).
   TannarxService: oxirgi 3 oy, material + 2% tashuv + ish haqi; 5/5/5/10%; QQS 12%.
   Faqat Engineering moduli tanlangan bo'lsa (MES tanlansa Engineering ham avtomatik tanlanadi). Idempotent: BOM_HEADERS bo'sh
   bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Engineering') = 0 OR OBJECT_ID(N'demo_tmp.BOM') IS NULL OR EXISTS (SELECT 1 FROM dbo.BOM_HEADERS)
BEGIN
    PRINT 'Demo muhandislik: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');

DECLARE @normFrom date = DATEFROMPARTS(YEAR(DATEADD(day, -100, @t)), MONTH(DATEADD(day, -100, @t)), 1);
DECLARE @normMonth char(7) = CONVERT(char(7), @normFrom, 126);   -- 'yyyy-MM' = BOM_HEADERS.VersionNo

INSERT INTO dbo.NORM_INPUT_LINES (ProductID, MaterialID, CutLengthMm, PiecesPerProduct, Month, CreatedByUserID, CreatedDT, Price,
                                  Consumption, WasteAmount, UnitID, Comment)
SELECT b.FG, b.Mat, 0, 1, @normMonth, @u, DATEADD(day, -100, @t), m.Price, b.Qty,
       ROUND(b.Qty * b.WastePercent / 100.0, 4), b.UnitID, NULL
FROM demo_tmp.BOM b JOIN demo_tmp.PROD m ON m.ID = b.Mat;

DECLARE @nv TABLE (ProductID int, ID int);
INSERT INTO dbo.NORMVERSIONS (ProductID, EffectiveFrom, EffectiveTo, Comment, IsDeleted)
OUTPUT inserted.ProductID, inserted.ID INTO @nv
SELECT ID, @normFrom, NULL, N'Asosiy norma', 0 FROM demo_tmp.PROD WHERE IsRaw = 0;

INSERT INTO dbo.NORMLINES (NormID, MaterialID, QtyPerUnit, WastePercent, UnitID)
SELECT nv.ID, b.Mat, b.Qty, b.WastePercent, b.UnitID
FROM demo_tmp.BOM b JOIN @nv nv ON nv.ProductID = b.FG;

DECLARE @bh TABLE (ProductID int, ID int);
INSERT INTO dbo.BOM_HEADERS (ProductID, VersionNo, IsActive, CreatedAt)
OUTPUT inserted.ProductID, inserted.ID INTO @bh
SELECT ID, @normMonth, 1, DATEADD(day, -100, @t) FROM demo_tmp.PROD WHERE IsRaw = 0;

INSERT INTO dbo.BOM_LINES (BOMID, ComponentID, Qty, UnitID)
SELECT bh.ID, b.Mat, b.BomQty, b.UnitID
FROM demo_tmp.BOM b JOIN @bh bh ON bh.ProductID = b.FG;

/* ---------------- tannarx (TannarxService: material + 2% tashuv + ish haqi; 5/5/5/10%; QQS 12%) ---------------- */
;WITH mat AS (
    SELECT b.FG, SUM(b.BomQty * m.Price) AS MaterialPrice FROM demo_tmp.BOM b JOIN demo_tmp.PROD m ON m.ID = b.Mat GROUP BY b.FG),
mon AS (SELECT DATEADD(month, -k, @t) AS D FROM (VALUES (0), (1), (2)) v (k)),
c AS (
    SELECT p.ID, YEAR(mon.D) AS Y, MONTH(mon.D) AS M, ROUND(mat.MaterialPrice, 4) AS Mt, ROUND(mat.MaterialPrice * 0.02, 4) AS Tr, ROUND(p.Price * 0.06, 4) AS Lb
    FROM demo_tmp.PROD p JOIN mat ON mat.FG = p.ID CROSS JOIN mon)
INSERT INTO dbo.TANNARX (ProductID, Year, Month, MonthString, MaterialPrice, MaterialTransportCost, LaborCost, TaxAndBank, AdministrativeExpenses,
                         OtherExpenses, Profit, TaxAndBankPercent, AdministrativeExpensesPercent, OtherExpensesPercent, ProfitPercent,
                         SalesPriceEXWWithoutVAT, SalesPriceEXWWithVAT, SetPriceEXWWithVAT, FinishedProductTransportCost,
                         SalesPriceCIPWithoutVAT, SalesPriceCIPWithVAT, SetPriceCIPWithVAT, CreatedByUserID, CreatedDT, IsDeleted)
SELECT c.ID, c.Y, c.M,
       CHOOSE(c.M, 'Yanvar', 'Fevral', 'Mart', 'Aprel', 'May', 'Iyun', 'Iyul', 'Avgust', 'Sentabr', 'Oktabr', 'Noyabr', 'Dekabr') + '-' + RIGHT('0' + CAST(c.Y % 100 AS varchar(2)), 2),
       c.Mt, c.Tr, c.Lb, b.B * 0.05, b.B * 0.05, b.B * 0.05, b.B * 0.10, 5, 5, 5, 10,
       b.B * 1.25, b.B * 1.25 * 1.12, b.B * 1.25 * 1.12, NULL, b.B * 1.25, b.B * 1.25 * 1.12, b.B * 1.25 * 1.12, @u, DATEFROMPARTS(c.Y, c.M, 1), 0
FROM c CROSS APPLY (SELECT c.Mt + c.Tr + c.Lb AS B) b;

PRINT 'Demo muhandislik: OK';
