/*
  Tannarx: fakt (actual) qiymatlarni hisoblab TANNARX jadvalini yangilaydi.
  SSMS da barcha blokni bitta marta ishga tushiring (F5).

  Manbalar:
  - Sotuv: SALES_INVOICES / SALES_INVOICE_LINES (holat: CANCELED, DRAFT dan tashqari)
  - Material (fakt): (1) Xarid invoysi FIN (SourceDocType=PurchaseInvoice) — P_INVOICE_PRODUCTS qator
    narx ulushi bo'yicha mahsulotga; (2) qo'lda "Material" kategoriyasi — oy jami reja og'irligi
    (TANNARX.MaterialPrice × ishlab chiqarish yoki sotuv miqdori, ishlab chiqarish ustun) bo'linadi.
    Faqat shu oyda ishlab chiqarilgan yoki sotilgan mahsulot qatorlarida birlik narxi; aktiv bo'lmaganlar — NULL.
  - Ishlab chiqarish hajmi: PRODUCT_PRODUCTION
  - Xomashyo kirimi transporti: F_WAYBILLS + P_INVOICES; rejaviy vazn 0 bo'lsa ham oy jami tashuv / umumiy ishlab chiqarish.
  - Tayyor mahsulot yetkazib berish: F_WAYBILLS (SalesInvoiceID) Amount, sotuv invoysi oyiga (InvoiceDate) bog'langan;
    Direction 0 yoki 1 (ichki 2 emas); mahsulot bo'yicha birlik narxi.
  - Ish haqi: HR_PAYROLL_HEADERS / HR_PAYROLL_LINES (GrossSalary)
  - Admin (soatbay): rejaviy taqsimot; reja vazni 0 bo'lsa soatbay jami / umumiy ishlab chiqarish — reja bilan majburiy teng emas.

  Taqsimot: soliq+bank, boshqa FIN — oy jami / umumiy ishlab chiqarish; material — yuqoridagi mahsulot bo'yicha.

  Yangilanishlar:
  - Material fakt: xarid FIN + P_INVOICE_PRODUCTS; qo'lda Material — reja og'irligi; faqat ishlab chiqarish/sotuv miqdori > 0.
  - Soliq, bank xizmati: FIN alohida yig'indi (kategoriya nomi bo'yicha), keyin qo'shiladi; birlik = jami / oylik PRODUCT_PRODUCTION yig'indisi.
  - Boshqa xarajat: FIN boshqa kategoriya jami / shu yig'indi.
  - Ish haqi: HR PayrollType = PieceRate; admin: Hourly yoki NULL.
  - Foyda: SetPriceEXWWithVAT (yoki Sales) − barcha fakt xarajatlar (tayyor mahsulot tashuvi bilan).
  - Aktual EXW/CIP: sotuv o'rtachasi emas — barcha aktual o'lchov xarajatlari yig'indisi; QQS 12% (1.12);
    CIP = EXW (qqs siz) + tayyor mahsulot tashuvi, keyin yana 1.12.
*/

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS', N'V') IS NOT NULL
    DROP VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS;
GO

IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT', N'V') IS NOT NULL
    DROP VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT;
GO

IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_LABOR_COSTS', N'V') IS NOT NULL
    DROP VIEW dbo.V_TANNARX_ACTUAL_LABOR_COSTS;
GO

IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT', N'V') IS NOT NULL
    DROP VIEW dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT;
GO

IF OBJECT_ID(N'dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES', N'V') IS NOT NULL
    DROP VIEW dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES;
GO

CREATE VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(CAST(pp.Total AS decimal(18,4))) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
InvLineSum AS (
    SELECT pip.InvoiceID, SUM(CAST(pip.TotalPrice AS decimal(18,4))) AS LineTotal
    FROM dbo.P_INVOICE_PRODUCTS pip
    GROUP BY pip.InvoiceID
),
/* Xarid invoysiga bog'langan FIN — mahsulot qatorlariga narx ulushi bo'yicha */
MatPurchaseByProductMonth AS (
    SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
           pip.ProductID,
           SUM(CAST(t.Amount AS decimal(18,4)) * (CAST(pip.TotalPrice AS decimal(18,4)) / ls.LineTotal)) AS MatAmt
    FROM dbo.FIN_TRANSACTIONS t
    INNER JOIN dbo.P_INVOICES pi ON pi.ID = t.SourceDocID
    INNER JOIN InvLineSum ls ON ls.InvoiceID = pi.ID AND ls.LineTotal > 0
    INNER JOIN dbo.P_INVOICE_PRODUCTS pip ON pip.InvoiceID = pi.ID
    WHERE ISNULL(t.IsDeleted, 0) = 0
      AND t.Direction = 2
      AND t.SourceDocType = N'PurchaseInvoice'
      AND ISNULL(pi.IsDeleted, 0) = 0
    GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate), pip.ProductID
),
/* Qo'lda Material kategoriyasi (xarid invoysiga bog'lanmagan) */
FinMaterialManualMonth AS (
    SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
           SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
    FROM dbo.FIN_TRANSACTIONS t
    INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
    WHERE ISNULL(t.IsDeleted, 0) = 0
      AND t.Direction = 2
      AND ISNULL(c.IsDeleted, 0) = 0
      AND (t.SourceDocType IS NULL OR t.SourceDocType <> N'PurchaseInvoice')
      AND (
            LOWER(LTRIM(RTRIM(c.Name))) = N'material'
         OR LOWER(c.Name) LIKE N'%material%'
      )
    GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS ActQty,
           CAST(t.MaterialPrice AS decimal(38, 12)) * COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS PlanMatWeight
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
MonthMatPlanWeightAgg AS (
    SELECT tb.Year, tb.Month,
           SUM(CASE WHEN tb.ActQty > 0 THEN tb.PlanMatWeight ELSE CAST(0 AS decimal(38, 12)) END) AS S_PlanMat,
           SUM(CASE WHEN tb.ActQty > 0 THEN tb.ActQty ELSE CAST(0 AS decimal(38, 12)) END) AS S_ActQty
    FROM TannarxBase tb
    GROUP BY tb.Year, tb.Month
),
MatRow AS (
    SELECT tb.ProductID, tb.Year, tb.Month, tb.ActQty,
           ISNULL(mp.MatAmt, 0) AS MatPurchaseAmt,
           CAST(
             CASE
               WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_PlanMat, 0) > 0
                 THEN fmm.TotalAmt * tb.PlanMatWeight / mma.S_PlanMat
               WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_ActQty, 0) > 0
                 THEN fmm.TotalAmt * tb.ActQty / mma.S_ActQty
               ELSE CAST(0 AS decimal(38, 12))
             END AS decimal(38, 12)) AS ManualMatAlloc
    FROM TannarxBase tb
    LEFT JOIN MatPurchaseByProductMonth mp ON mp.ProductID = tb.ProductID AND mp.Y = tb.Year AND mp.M = tb.Month
    LEFT JOIN FinMaterialManualMonth fmm ON fmm.Y = tb.Year AND fmm.M = tb.Month
    LEFT JOIN MonthMatPlanWeightAgg mma ON mma.Year = tb.Year AND mma.Month = tb.Month
)
SELECT mr.ProductID, mr.Year, mr.Month,
       CAST(
         CASE
           WHEN mr.ActQty > 0 AND (mr.MatPurchaseAmt + mr.ManualMatAlloc) > 0
             THEN (mr.MatPurchaseAmt + mr.ManualMatAlloc) / mr.ActQty
         END
       AS decimal(38, 6)) AS ActualMaterialPrice
FROM MatRow mr;
GO

CREATE VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
PurchFreightMonth AS (
    SELECT YEAR(pi.InvoiceDate) AS Y, MONTH(pi.InvoiceDate) AS M,
           SUM(CAST(ISNULL(wb.Amount, 0) AS float)) AS TotalFreight
    FROM dbo.F_WAYBILLS wb
    INNER JOIN dbo.P_INVOICES pi ON pi.ID = wb.InvoiceID
    WHERE ISNULL(wb.IsDeleted, 0) = 0
      AND wb.InvoiceID IS NOT NULL
      AND ISNULL(pi.IsDeleted, 0) = 0
    GROUP BY YEAR(pi.InvoiceDate), MONTH(pi.InvoiceDate)
),
ProdQtyMonthTotal AS (
    SELECT YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M,
           SUM(CAST(pp.Total AS decimal(18,4))) AS TotalQty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY YEAR(pp.[Date]), MONTH(pp.[Date])
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.MaterialTransportCost
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
W AS (
    SELECT tb.ProductID, tb.Year, tb.Month,
           tb.MaterialTransportCost * tb.DenomSafe AS WTr,
           pf.TotalFreight,
           SUM(tb.MaterialTransportCost * tb.DenomSafe) OVER (PARTITION BY tb.Year, tb.Month) AS S_WTr,
           pqm.TotalQty AS MonthProdQty
    FROM TannarxBase tb
    LEFT JOIN PurchFreightMonth pf ON pf.Y = tb.Year AND pf.M = tb.Month
    LEFT JOIN ProdQtyMonthTotal pqm ON pqm.Y = tb.Year AND pqm.M = tb.Month
)
SELECT w.ProductID, w.Year, w.Month,
       CAST(CASE WHEN w.S_WTr > 0 AND w.TotalFreight IS NOT NULL
            THEN (w.TotalFreight * w.WTr / w.S_WTr) / NULLIF(x.DenomSafe, 0)
            WHEN ISNULL(w.S_WTr, 0) = 0 AND w.TotalFreight IS NOT NULL AND ISNULL(w.MonthProdQty, 0) > 0
            THEN w.TotalFreight / w.MonthProdQty
            END AS float) AS ActualMaterialTransportCost
FROM W w
     INNER JOIN TannarxBase x ON x.ProductID = w.ProductID AND x.Year = w.Year AND x.Month = w.Month;
GO

CREATE VIEW dbo.V_TANNARX_ACTUAL_LABOR_COSTS
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
PayrollPieceMonth AS (
    SELECT h.Year AS Y, h.Month AS M,
           SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
    FROM dbo.HR_PAYROLL_LINES l
    INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
    WHERE ISNULL(l.IsDeleted, 0) = 0
      AND ISNULL(h.IsDeleted, 0) = 0
      AND h.PayrollType = N'PieceRate'
    GROUP BY h.Year, h.Month
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.LaborCost
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
W AS (
    SELECT tb.ProductID, tb.Year, tb.Month,
           tb.LaborCost * tb.DenomSafe AS WLabor,
           pm.TotalGross,
           SUM(tb.LaborCost * tb.DenomSafe) OVER (PARTITION BY tb.Year, tb.Month) AS S_WLabor
    FROM TannarxBase tb
    LEFT JOIN PayrollPieceMonth pm ON pm.Y = tb.Year AND pm.M = tb.Month
)
SELECT w.ProductID, w.Year, w.Month,
       CAST(CASE WHEN w.S_WLabor > 0 AND w.TotalGross IS NOT NULL
            THEN (w.TotalGross * w.WLabor / w.S_WLabor) / NULLIF(x.DenomSafe, 0)
            END AS decimal(38,8)) AS ActualLaborCost
FROM W w
     INNER JOIN TannarxBase x ON x.ProductID = w.ProductID AND x.Year = w.Year AND x.Month = w.Month;
GO

CREATE VIEW dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT
AS
WITH InvoiceQty AS (
    SELECT sil.InvoiceID, SUM(CAST(sil.Qty AS decimal(18,4))) AS TotQty
    FROM dbo.SALES_INVOICE_LINES sil
    GROUP BY sil.InvoiceID
),
/* Sotuv invoysi oyi (Tannarx bilan mos); yo'l varaqasi summasi qatorlarga bo'linadi. Direction 0/1/null (ichki 2 emas). */
FinishedProductFreightLine AS (
    SELECT sil.ProductID,
           YEAR(si.InvoiceDate) AS Y,
           MONTH(si.InvoiceDate) AS M,
           sil.ID AS LineID,
           MAX(CAST(sil.Qty AS decimal(18,4))) AS LineQty,
           SUM(
               CAST(ISNULL(wb.Amount, 0) AS decimal(18,4)) *
               CASE
                 WHEN si.TotalAmount IS NOT NULL AND si.TotalAmount <> 0
                   THEN CAST(sil.LineAmount AS decimal(18,4)) / CAST(si.TotalAmount AS decimal(18,4))
                 ELSE CAST(sil.Qty AS decimal(18,4)) / NULLIF(iq.TotQty, 0)
               END
           ) AS LineFreightAlloc
    FROM dbo.F_WAYBILLS wb
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = wb.SalesInvoiceID
    INNER JOIN dbo.SALES_INVOICE_LINES sil ON sil.InvoiceID = si.ID
    LEFT JOIN InvoiceQty iq ON iq.InvoiceID = sil.InvoiceID
    WHERE ISNULL(wb.IsDeleted, 0) = 0
      AND wb.SalesInvoiceID IS NOT NULL
      AND (wb.Direction IS NULL OR wb.Direction IN (0, 1))
      AND si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID,
             YEAR(si.InvoiceDate),
             MONTH(si.InvoiceDate),
             sil.ID
),
LineFreight AS (
    SELECT ProductID, Y AS Year, M AS Month,
           CAST(SUM(LineFreightAlloc) / NULLIF(SUM(LineQty), 0) AS float) AS ActualFinishedProductTransportCost
    FROM FinishedProductFreightLine
    GROUP BY ProductID, Y, M
)
SELECT ProductID, Year, Month, ActualFinishedProductTransportCost
FROM LineFreight;
GO

CREATE VIEW dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
SalesAgg AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M,
           SUM(CAST(sil.LineAmount AS decimal(18,4))) AS SumLineAmt,
           SUM(CAST(sil.Qty AS decimal(18,4))) AS SumQty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
PayrollHourlyMonth AS (
    SELECT h.Year AS Y, h.Month AS M,
           SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
    FROM dbo.HR_PAYROLL_LINES l
    INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
    WHERE ISNULL(l.IsDeleted, 0) = 0
      AND ISNULL(h.IsDeleted, 0) = 0
      AND (h.PayrollType IS NULL OR h.PayrollType = N'Hourly')
    GROUP BY h.Year, h.Month
),
ProdQtyMonthTotal AS (
    SELECT YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M,
           SUM(CAST(pp.Total AS decimal(18,4))) AS TotalQty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY YEAR(pp.[Date]), MONTH(pp.[Date])
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.AdministrativeExpenses,
           t.SalesPriceEXWWithoutVAT
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
MonthAgg AS (
    SELECT tb.Year, tb.Month,
           SUM(tb.AdministrativeExpenses * tb.DenomSafe) AS S_Admin
    FROM TannarxBase tb
    GROUP BY tb.Year, tb.Month
)
SELECT tb.ProductID, tb.Year, tb.Month,
       CAST(
         CASE
           WHEN ma.S_Admin > 0 AND pmh.TotalGross IS NOT NULL
             THEN (pmh.TotalGross * (tb.AdministrativeExpenses * tb.DenomSafe) / ma.S_Admin) / NULLIF(tb.DenomSafe, 0)
           WHEN sa.SumQty > 0 AND tb.SalesPriceEXWWithoutVAT <> 0
             THEN tb.AdministrativeExpenses * ((sa.SumLineAmt / sa.SumQty) / tb.SalesPriceEXWWithoutVAT)
           WHEN pqm.TotalQty > 0 AND pmh.TotalGross IS NOT NULL
             THEN pmh.TotalGross / pqm.TotalQty
         END
       AS decimal(38,6)) AS ActualAdministrativeExpenses
FROM TannarxBase tb
LEFT JOIN MonthAgg ma ON ma.Year = tb.Year AND ma.Month = tb.Month
LEFT JOIN PayrollHourlyMonth pmh ON pmh.Y = tb.Year AND pmh.M = tb.Month
LEFT JOIN ProdQtyMonthTotal pqm ON pqm.Y = tb.Year AND pqm.M = tb.Month
LEFT JOIN SalesAgg sa ON sa.ProductID = tb.ProductID AND sa.Y = tb.Year AND sa.M = tb.Month;
GO

IF OBJECT_ID(N'dbo.SP_TANNARX_UPDATE_ACTUAL_COSTS', N'P') IS NOT NULL
    DROP PROCEDURE dbo.SP_TANNARX_UPDATE_ACTUAL_COSTS;
GO

CREATE PROCEDURE dbo.SP_TANNARX_UPDATE_ACTUAL_COSTS
    @Year  INT = NULL,
    @Month INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    /* Aktual EXW/CIP: QQS 12% = * 1.12 (Calc2) */

    ;WITH ProdQty AS (
        SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
        FROM dbo.PRODUCT_PRODUCTION pp
        WHERE ISNULL(pp.IsDeleted, 0) = 0
        GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
    ),
    SalesQty AS (
        SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
        FROM dbo.SALES_INVOICE_LINES sil
        INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
        WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
        GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
    ),
    SalesAgg AS (
        SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M,
               SUM(CAST(sil.LineAmount AS decimal(18,4))) AS SumLineAmt,
               SUM(CAST(sil.TotalWithVAT AS decimal(18,4))) AS SumWithVAT,
               SUM(CAST(sil.Qty AS decimal(18,4))) AS SumQty
        FROM dbo.SALES_INVOICE_LINES sil
        INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
        WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
        GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
    ),
    InvLineSum AS (
        SELECT pip.InvoiceID, SUM(CAST(pip.TotalPrice AS decimal(18,4))) AS LineTotal
        FROM dbo.P_INVOICE_PRODUCTS pip
        GROUP BY pip.InvoiceID
    ),
    MatPurchaseByProductMonth AS (
        SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
               pip.ProductID,
               SUM(CAST(t.Amount AS decimal(18,4)) * (CAST(pip.TotalPrice AS decimal(18,4)) / ls.LineTotal)) AS MatAmt
        FROM dbo.FIN_TRANSACTIONS t
        INNER JOIN dbo.P_INVOICES pi ON pi.ID = t.SourceDocID
        INNER JOIN InvLineSum ls ON ls.InvoiceID = pi.ID AND ls.LineTotal > 0
        INNER JOIN dbo.P_INVOICE_PRODUCTS pip ON pip.InvoiceID = pi.ID
        WHERE ISNULL(t.IsDeleted, 0) = 0
          AND t.Direction = 2
          AND t.SourceDocType = N'PurchaseInvoice'
          AND ISNULL(pi.IsDeleted, 0) = 0
        GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate), pip.ProductID
    ),
    FinMaterialManualMonth AS (
        SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
               SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
        FROM dbo.FIN_TRANSACTIONS t
        INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
        WHERE ISNULL(t.IsDeleted, 0) = 0
          AND t.Direction = 2
          AND ISNULL(c.IsDeleted, 0) = 0
          AND (t.SourceDocType IS NULL OR t.SourceDocType <> N'PurchaseInvoice')
          AND (
                LOWER(LTRIM(RTRIM(c.Name))) = N'material'
             OR LOWER(c.Name) LIKE N'%material%'
          )
        GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
    ),
    ProdQtyMonthTotal AS (
        SELECT YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M,
               SUM(CAST(pp.Total AS decimal(18,4))) AS TotalQty
        FROM dbo.PRODUCT_PRODUCTION pp
        WHERE ISNULL(pp.IsDeleted, 0) = 0
        GROUP BY YEAR(pp.[Date]), MONTH(pp.[Date])
    ),
    PurchFreightMonth AS (
        SELECT YEAR(pi.InvoiceDate) AS Y, MONTH(pi.InvoiceDate) AS M,
               SUM(CAST(ISNULL(wb.Amount, 0) AS decimal(18,4))) AS TotalFreight
        FROM dbo.F_WAYBILLS wb
        INNER JOIN dbo.P_INVOICES pi ON pi.ID = wb.InvoiceID
        WHERE ISNULL(wb.IsDeleted, 0) = 0
          AND wb.InvoiceID IS NOT NULL
          AND ISNULL(pi.IsDeleted, 0) = 0
        GROUP BY YEAR(pi.InvoiceDate), MONTH(pi.InvoiceDate)
    ),
    /* Ishbay (PieceRate) — ish haqi; Soatbay (Hourly / NULL) — admin xarajat */
    PayrollAllMonth AS (
        SELECT h.Year AS Y, h.Month AS M,
               SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
        FROM dbo.HR_PAYROLL_LINES l
        INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
        WHERE ISNULL(l.IsDeleted, 0) = 0
          AND ISNULL(h.IsDeleted, 0) = 0
        GROUP BY h.Year, h.Month
    ),
    PayrollPieceMonth AS (
        SELECT h.Year AS Y, h.Month AS M,
               SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
        FROM dbo.HR_PAYROLL_LINES l
        INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
        WHERE ISNULL(l.IsDeleted, 0) = 0
          AND ISNULL(h.IsDeleted, 0) = 0
          AND h.PayrollType = N'PieceRate'
        GROUP BY h.Year, h.Month
    ),
    PayrollHourlyMonth AS (
        SELECT h.Year AS Y, h.Month AS M,
               SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
        FROM dbo.HR_PAYROLL_LINES l
        INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
        WHERE ISNULL(l.IsDeleted, 0) = 0
          AND ISNULL(h.IsDeleted, 0) = 0
          AND (h.PayrollType IS NULL OR h.PayrollType = N'Hourly')
        GROUP BY h.Year, h.Month
    ),
    /* Soliq (bank nomi tushib qolmasin — bank alohida) */
    FinSoliqMonth AS (
        SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
               SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
        FROM dbo.FIN_TRANSACTIONS t
        INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
        WHERE ISNULL(t.IsDeleted, 0) = 0
          AND t.Direction = 2
          AND ISNULL(c.IsDeleted, 0) = 0
          AND LOWER(c.Name) LIKE N'%soliq%'
        GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
    ),
    FinBankServiceMonth AS (
        SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
               SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
        FROM dbo.FIN_TRANSACTIONS t
        INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
        WHERE ISNULL(t.IsDeleted, 0) = 0
          AND t.Direction = 2
          AND ISNULL(c.IsDeleted, 0) = 0
          AND (
                LOWER(c.Name) LIKE N'%bank xizmati%'
             OR (LOWER(c.Name) LIKE N'%bank%' AND LOWER(c.Name) NOT LIKE N'%soliq%')
          )
        GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
    ),
    FinOtherMonth AS (
        SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
               SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
        FROM dbo.FIN_TRANSACTIONS t
        INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
        WHERE ISNULL(t.IsDeleted, 0) = 0
          AND t.Direction = 2
          AND ISNULL(c.IsDeleted, 0) = 0
          AND (
                LOWER(c.Name) LIKE N'%boshqa%'
             OR LOWER(c.Name) LIKE N'%boshqa xarajat%'
          )
        GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
    ),
    InvoiceQty AS (
        SELECT sil.InvoiceID, SUM(CAST(sil.Qty AS decimal(18,4))) AS TotQty
        FROM dbo.SALES_INVOICE_LINES sil
        GROUP BY sil.InvoiceID
    ),
    FinishedProductFreightLine AS (
        SELECT sil.ProductID,
               YEAR(si.InvoiceDate) AS Y,
               MONTH(si.InvoiceDate) AS M,
               sil.ID AS LineID,
               MAX(CAST(sil.Qty AS decimal(18,4))) AS LineQty,
               SUM(
                   CAST(ISNULL(wb.Amount, 0) AS decimal(18,4)) *
                   CASE
                     WHEN si.TotalAmount IS NOT NULL AND si.TotalAmount <> 0
                       THEN CAST(sil.LineAmount AS decimal(18,4)) / CAST(si.TotalAmount AS decimal(18,4))
                     ELSE CAST(sil.Qty AS decimal(18,4)) / NULLIF(iq.TotQty, 0)
                   END
               ) AS LineFreightAlloc
        FROM dbo.F_WAYBILLS wb
        INNER JOIN dbo.SALES_INVOICES si ON si.ID = wb.SalesInvoiceID
        INNER JOIN dbo.SALES_INVOICE_LINES sil ON sil.InvoiceID = si.ID
        LEFT JOIN InvoiceQty iq ON iq.InvoiceID = sil.InvoiceID
        WHERE ISNULL(wb.IsDeleted, 0) = 0
          AND wb.SalesInvoiceID IS NOT NULL
          AND (wb.Direction IS NULL OR wb.Direction IN (0, 1))
          AND si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
        GROUP BY sil.ProductID,
                 YEAR(si.InvoiceDate),
                 MONTH(si.InvoiceDate),
                 sil.ID
    ),
    LineFreightFix AS (
        SELECT ProductID, Y, M,
               CAST(SUM(LineFreightAlloc) / NULLIF(SUM(LineQty), 0) AS decimal(18,4)) AS FreightPerUnit
        FROM FinishedProductFreightLine
        GROUP BY ProductID, Y, M
    ),
    TannarxBase AS (
        SELECT t.ID, t.ProductID, t.Year, t.Month,
               COALESCE(pq.Qty, sq.Qty, 0) AS DenomQty,
               COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
               COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS ActQty,
               CAST(t.MaterialPrice AS decimal(18, 4)) * COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS PlanMatWeight,
               t.MaterialPrice, t.MaterialTransportCost, t.LaborCost, t.TaxAndBank,
               t.AdministrativeExpenses, t.OtherExpenses,
               t.SalesPriceEXWWithoutVAT, t.SalesPriceEXWWithVAT,
               t.SalesPriceCIPWithoutVAT, t.SalesPriceCIPWithVAT,
               COALESCE(t.SetPriceEXWWithVAT, t.SalesPriceEXWWithVAT) AS PlannedUnitRev
        FROM dbo.TANNARX t
        LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
        LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
        WHERE t.IsDeleted = 0
          AND (@Year IS NULL OR t.Year = @Year)
          AND (@Month IS NULL OR t.Month = @Month)
    ),
    MonthMatPlanWeightAgg AS (
        SELECT tb.Year, tb.Month,
               SUM(CASE WHEN tb.ActQty > 0 THEN tb.PlanMatWeight ELSE CAST(0 AS decimal(18, 4)) END) AS S_PlanMat,
               SUM(CASE WHEN tb.ActQty > 0 THEN tb.ActQty ELSE CAST(0 AS decimal(18, 4)) END) AS S_ActQty
        FROM TannarxBase tb
        GROUP BY tb.Year, tb.Month
    ),
    MatRow AS (
        SELECT tb.ID,
               tb.ActQty,
               ISNULL(mp.MatAmt, 0) AS MatPurchaseAmt,
               CAST(
                 CASE
                   WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_PlanMat, 0) > 0
                     THEN fmm.TotalAmt * tb.PlanMatWeight / mma.S_PlanMat
                   WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_ActQty, 0) > 0
                     THEN fmm.TotalAmt * tb.ActQty / mma.S_ActQty
                   ELSE CAST(0 AS decimal(18, 4))
                 END AS decimal(18, 4)) AS ManualMatAlloc
        FROM TannarxBase tb
        LEFT JOIN MatPurchaseByProductMonth mp ON mp.ProductID = tb.ProductID AND mp.Y = tb.Year AND mp.M = tb.Month
        LEFT JOIN FinMaterialManualMonth fmm ON fmm.Y = tb.Year AND fmm.M = tb.Month
        LEFT JOIN MonthMatPlanWeightAgg mma ON mma.Year = tb.Year AND mma.Month = tb.Month
    ),
    MonthAgg AS (
        SELECT tb.Year, tb.Month,
               SUM(tb.MaterialTransportCost * tb.DenomSafe) AS S_MatTr,
               SUM(tb.LaborCost * tb.DenomSafe) AS S_Labor,
               SUM(tb.AdministrativeExpenses * tb.DenomSafe) AS S_Admin
        FROM TannarxBase tb
        GROUP BY tb.Year, tb.Month
    ),
    Calc AS (
        SELECT tb.ID,
               tb.PlannedUnitRev,
               CAST(CASE WHEN mr.ActQty > 0 AND (mr.MatPurchaseAmt + mr.ManualMatAlloc) > 0
                    THEN (mr.MatPurchaseAmt + mr.ManualMatAlloc) / mr.ActQty
                    END AS decimal(18,4)) AS ActualMaterialPrice,
               CAST(CASE WHEN ISNULL(ma.S_MatTr, 0) > 0 AND pf.TotalFreight IS NOT NULL
                    THEN (pf.TotalFreight * (tb.MaterialTransportCost * tb.DenomSafe) / ma.S_MatTr) / NULLIF(tb.DenomSafe, 0)
                    WHEN ISNULL(ma.S_MatTr, 0) = 0 AND pf.TotalFreight IS NOT NULL AND pqm.TotalQty > 0
                    THEN pf.TotalFreight / pqm.TotalQty
                    END AS decimal(18,4)) AS ActualMaterialTransportCost,
               CAST(CASE WHEN ma.S_Labor > 0 AND COALESCE(pmp.TotalGross, pma.TotalGross) IS NOT NULL
                    THEN (COALESCE(pmp.TotalGross, pma.TotalGross) * (tb.LaborCost * tb.DenomSafe) / ma.S_Labor) / NULLIF(tb.DenomSafe, 0)
                    END AS decimal(18,4)) AS ActualLaborCost,
               CAST(CASE WHEN sa.SumQty > 0 THEN sa.SumLineAmt / sa.SumQty END AS decimal(18,4)) AS AvgExWNoVat,
               CAST(CASE WHEN sa.SumQty > 0 THEN sa.SumWithVAT / sa.SumQty END AS decimal(18,4)) AS AvgExWWithVat,
               CAST(CASE WHEN pqm.TotalQty > 0
                         AND (fs.TotalAmt IS NOT NULL OR fbk.TotalAmt IS NOT NULL)
                    THEN (COALESCE(fs.TotalAmt, 0) + COALESCE(fbk.TotalAmt, 0)) / pqm.TotalQty
                    END AS decimal(18,4)) AS ActualTaxAndBank,
               CAST(CASE WHEN ma.S_Admin > 0 AND pmh.TotalGross IS NOT NULL
                    THEN (pmh.TotalGross * (tb.AdministrativeExpenses * tb.DenomSafe) / ma.S_Admin) / NULLIF(tb.DenomSafe, 0)
                    WHEN sa.SumQty > 0 AND tb.SalesPriceEXWWithoutVAT <> 0
                    THEN tb.AdministrativeExpenses * ((sa.SumLineAmt / sa.SumQty) / tb.SalesPriceEXWWithoutVAT)
                    WHEN pqm.TotalQty > 0 AND pmh.TotalGross IS NOT NULL
                    THEN pmh.TotalGross / pqm.TotalQty
                    END AS decimal(18,4)) AS ActualAdministrativeExpenses,
               CAST(CASE WHEN pqm.TotalQty > 0 AND fo.TotalAmt IS NOT NULL
                    THEN fo.TotalAmt / pqm.TotalQty
                    END AS decimal(18,4)) AS ActualOtherExpenses,
               lf.FreightPerUnit AS FreightPerUnit
        FROM TannarxBase tb
        INNER JOIN MatRow mr ON mr.ID = tb.ID
        LEFT JOIN MonthAgg ma ON ma.Year = tb.Year AND ma.Month = tb.Month
        LEFT JOIN ProdQtyMonthTotal pqm ON pqm.Y = tb.Year AND pqm.M = tb.Month
        LEFT JOIN PurchFreightMonth pf ON pf.Y = tb.Year AND pf.M = tb.Month
        LEFT JOIN PayrollPieceMonth pmp ON pmp.Y = tb.Year AND pmp.M = tb.Month
        LEFT JOIN PayrollAllMonth pma ON pma.Y = tb.Year AND pma.M = tb.Month
        LEFT JOIN PayrollHourlyMonth pmh ON pmh.Y = tb.Year AND pmh.M = tb.Month
        LEFT JOIN FinSoliqMonth fs ON fs.Y = tb.Year AND fs.M = tb.Month
        LEFT JOIN FinBankServiceMonth fbk ON fbk.Y = tb.Year AND fbk.M = tb.Month
        LEFT JOIN FinOtherMonth fo ON fo.Y = tb.Year AND fo.M = tb.Month
        LEFT JOIN SalesAgg sa ON sa.ProductID = tb.ProductID AND sa.Y = tb.Year AND sa.M = tb.Month
        LEFT JOIN LineFreightFix lf ON lf.ProductID = tb.ProductID AND lf.Y = tb.Year AND lf.M = tb.Month
    ),
    Calc2a AS (
        SELECT c.*,
               ISNULL(c.ActualMaterialPrice, 0) + ISNULL(c.ActualMaterialTransportCost, 0) + ISNULL(c.ActualLaborCost, 0)
               + ISNULL(c.ActualTaxAndBank, 0) + ISNULL(c.ActualAdministrativeExpenses, 0) + ISNULL(c.ActualOtherExpenses, 0) AS SumExwCostNoVat
        FROM Calc c
    ),
    Calc2 AS (
        SELECT ca.*,
               CAST(CASE WHEN ca.SumExwCostNoVat > 0 THEN ca.SumExwCostNoVat END AS decimal(18, 4)) AS ActualExwNoVat,
               CAST(CASE WHEN ca.SumExwCostNoVat > 0 THEN ca.SumExwCostNoVat * CAST(1.12 AS DECIMAL(18, 6)) END AS decimal(18, 4)) AS ActualExwWithVat,
               CAST(
                 CASE WHEN ca.SumExwCostNoVat + ISNULL(ca.FreightPerUnit, 0) > 0
                      THEN ca.SumExwCostNoVat + ISNULL(ca.FreightPerUnit, 0)
                 END AS decimal(18, 4)) AS ActualCipNoVat,
               CAST(
                 CASE WHEN ca.SumExwCostNoVat + ISNULL(ca.FreightPerUnit, 0) > 0
                      THEN (ca.SumExwCostNoVat + ISNULL(ca.FreightPerUnit, 0)) * CAST(1.12 AS DECIMAL(18, 6))
                 END AS decimal(18, 4)) AS ActualCipWithVat,
               CAST(
                 CASE WHEN ca.PlannedUnitRev IS NOT NULL
                      THEN ca.PlannedUnitRev
                          - ISNULL(ca.ActualMaterialPrice, 0)
                          - ISNULL(ca.ActualMaterialTransportCost, 0)
                          - ISNULL(ca.ActualLaborCost, 0)
                          - ISNULL(ca.ActualTaxAndBank, 0)
                          - ISNULL(ca.ActualAdministrativeExpenses, 0)
                          - ISNULL(ca.ActualOtherExpenses, 0)
                          - ISNULL(ca.FreightPerUnit, 0)
                 END AS decimal(18, 4)) AS ActualProfit
        FROM Calc2a ca
    )
    UPDATE t
    SET ActualMaterialPrice = c.ActualMaterialPrice,
        ActualMaterialTransportCost = c.ActualMaterialTransportCost,
        ActualLaborCost = c.ActualLaborCost,
        ActualTaxAndBank = c.ActualTaxAndBank,
        ActualAdministrativeExpenses = c.ActualAdministrativeExpenses,
        ActualOtherExpenses = c.ActualOtherExpenses,
        ActualFinishedProductTransportCost = c.FreightPerUnit,
        ActualSalesPriceEXWWithoutVAT = c.ActualExwNoVat,
        ActualSalesPriceEXWWithVAT = c.ActualExwWithVat,
        ActualSalesPriceCIPWithoutVAT = c.ActualCipNoVat,
        ActualSalesPriceCIPWithVAT = c.ActualCipWithVat,
        ActualProfit = c.ActualProfit,
        ModifiedDT = GETDATE()
    FROM dbo.TANNARX t
    INNER JOIN Calc2 c ON c.ID = t.ID;

    DECLARE @RowCount INT = @@ROWCOUNT;
    SELECT @RowCount AS RowsUpdated;
END;
GO
