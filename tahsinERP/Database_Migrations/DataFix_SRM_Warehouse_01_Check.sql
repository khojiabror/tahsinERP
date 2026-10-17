/* =====================================================================
   DataFix_SRM_Warehouse_01_Check.sql  — FAQAT O'QISH (SELECT)
   Audit 2026-10, SRM — Ombor/Inventar (SRM-B01..B10)

   Maqsad: kod tuzatilgunga qadar yuzaga kelgan noto'g'ri ombor ma'lumotlarini TOPISH.
   Hech narsa o'zgartirmaydi. Natijalarni ko'rib chiqing, so'ng kerak bo'lsa
   DataFix_SRM_Warehouse_02_Fix.sql ni (avval test nusxada) ishga tushiring.

   Bo'limlar:
     1. PRODUCT_STOCKS dublikat (WHID, ProductID) qatorlari          — SRM-B01
     2. Hujjatlardan hisoblangan qoldiq va PRODUCT_STOCKS farqi        — SRM-B01/B02/B03/B04 (clamp, ikki marta kirim)
     3. DLOC ledger > PRODUCT_STOCKS (mumkin bo'lmagan holat)          — SRM-B03/B05/B09
     4. Ajratmalar (EXPENSE_PRODUCT_ALLOCATIONS) muammolari            — SRM-B05
     5. O'chirilgan chiqimning "tirik" bog'langan kirimi              — SRM-B04
     6. Bog'langan kirim qatorlari chiqimnikidan farq qiladi          — SRM-B06
     7. Takroriy hujjat raqamlari (DocNo)                             — SRM-B07/B10
     8. Bir DocNo'li bir nechta TRNSF chiqimi                         — SRM-B10
   ===================================================================== */
SET NOCOUNT ON;

/* ---------------------------------------------------------------------
   1. PRODUCT_STOCKS dublikatlari: bitta (ombor, mahsulot) uchun bir nechta qator.
   --------------------------------------------------------------------- */
SELECT s.WHID, w.WHName, s.ProductID, p.PNo,
       COUNT(*)        AS RowCnt,
       SUM(s.Amount)   AS TotalAmount,
       MIN(s.ID)       AS KeepID
FROM dbo.PRODUCT_STOCKS s
LEFT JOIN dbo.WAREHOUSES w ON w.ID = s.WHID
LEFT JOIN dbo.PRODUCTS p   ON p.ID = s.ProductID
GROUP BY s.WHID, w.WHName, s.ProductID, p.PNo
HAVING COUNT(*) > 1
ORDER BY RowCnt DESC;

/* ---------------------------------------------------------------------
   2. Hujjatlar bo'yicha kutilgan qoldiq va PRODUCT_STOCKS farqi.
      Kutilgan = qabul qilingan kirimlar (RecieveStatus=1, o'chirilmagan)
               - o'chirilmagan chiqimlar.
      Eslatma: tarixda qoldiq hujjatsiz ham yozilgan (ProductStock/Save Excel yuklash,
      eski inventarizatsiya "stock.Amount = qty"), shuning uchun har bir farq xato emas —
      ko'rib chiqish ro'yxati. Clamp (0 ga tushirish) va ikki marta kirim (B02) ham shu yerda
      ko'rinadi.
   --------------------------------------------------------------------- */
;WITH Ins AS (
    SELECT i.ReceivingWHID AS WHID, ip.ProductID, SUM(ip.Amount) AS Qty
    FROM dbo.INCOME_PRODUCTS ip
    JOIN dbo.INCOMES i ON i.ID = ip.IncomeID
    WHERE ISNULL(i.IsDeleted, 0) = 0 AND ISNULL(i.RecieveStatus, 0) = 1
    GROUP BY i.ReceivingWHID, ip.ProductID
), Outs AS (
    SELECT e.SenderWHID AS WHID, ep.ProductID, SUM(ep.Amount) AS Qty
    FROM dbo.EXPENSE_PRODUCTS ep
    JOIN dbo.EXPENSES e ON e.ID = ep.ExpenseID
    WHERE e.IsDeleted = 0
    GROUP BY e.SenderWHID, ep.ProductID
), Stk AS (
    SELECT WHID, ProductID, SUM(Amount) AS Qty
    FROM dbo.PRODUCT_STOCKS
    GROUP BY WHID, ProductID
), Keys AS (
    SELECT WHID, ProductID FROM Ins
    UNION SELECT WHID, ProductID FROM Outs
    UNION SELECT WHID, ProductID FROM Stk
)
SELECT k.WHID, w.WHName, k.ProductID, p.PNo,
       ISNULL(ins.Qty, 0)                          AS ReceivedQty,
       ISNULL(outs.Qty, 0)                         AS IssuedQty,
       ISNULL(ins.Qty, 0) - ISNULL(outs.Qty, 0)    AS ExpectedQty,
       ISNULL(stk.Qty, 0)                          AS StockQty,
       ISNULL(stk.Qty, 0) - (ISNULL(ins.Qty, 0) - ISNULL(outs.Qty, 0)) AS Diff,
       CASE WHEN ISNULL(ins.Qty, 0) - ISNULL(outs.Qty, 0) < 0 THEN 'MANFIY (oversell / clamp)' ELSE '' END AS Note
FROM Keys k
LEFT JOIN Ins  ins  ON ins.WHID  = k.WHID AND ins.ProductID  = k.ProductID
LEFT JOIN Outs outs ON outs.WHID = k.WHID AND outs.ProductID = k.ProductID
LEFT JOIN Stk  stk  ON stk.WHID  = k.WHID AND stk.ProductID  = k.ProductID
LEFT JOIN dbo.WAREHOUSES w ON w.ID = k.WHID
LEFT JOIN dbo.PRODUCTS p   ON p.ID = k.ProductID
WHERE ABS(ISNULL(stk.Qty, 0) - (ISNULL(ins.Qty, 0) - ISNULL(outs.Qty, 0))) > 0.0001
ORDER BY ABS(ISNULL(stk.Qty, 0) - (ISNULL(ins.Qty, 0) - ISNULL(outs.Qty, 0))) DESC;

/* ---------------------------------------------------------------------
   3. DLOC ledger (LOT_LOCATIONS) yig'indisi PRODUCT_STOCKS dan katta — bo'lishi mumkin emas
      (PRODUCT_STOCKS = ledger + joylashtirilmagan qoldiq).
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.LOT_LOCATIONS', 'U') IS NOT NULL
BEGIN
    ;WITH Ledger AS (
        SELECT loc.WarehouseID AS WHID, ll.ProductID, SUM(ll.QtyAvailable) AS Qty
        FROM dbo.LOT_LOCATIONS ll
        JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
        GROUP BY loc.WarehouseID, ll.ProductID
    ), Stk AS (
        SELECT WHID, ProductID, SUM(Amount) AS Qty FROM dbo.PRODUCT_STOCKS GROUP BY WHID, ProductID
    )
    SELECT l.WHID, w.WHName, l.ProductID, p.PNo,
           l.Qty AS LedgerQty, ISNULL(s.Qty, 0) AS StockQty, l.Qty - ISNULL(s.Qty, 0) AS LedgerExcess
    FROM Ledger l
    LEFT JOIN Stk s ON s.WHID = l.WHID AND s.ProductID = l.ProductID
    LEFT JOIN dbo.WAREHOUSES w ON w.ID = l.WHID
    LEFT JOIN dbo.PRODUCTS p   ON p.ID = l.ProductID
    WHERE l.Qty - ISNULL(s.Qty, 0) > 0.0001
    ORDER BY LedgerExcess DESC;

    /* 3b. Lot holati: QtyReceived - QtyConsumed joylardagi qoldiqqa teng emas */
    SELECT il.ID AS LotID, il.LotNo, il.IncomeID, il.ProductID, il.WarehouseID,
           il.QtyReceived, il.QtyConsumed,
           il.QtyReceived - il.QtyConsumed AS ExpectedAvailable,
           ISNULL(x.AtLocations, 0)         AS AtLocations
    FROM dbo.INCOME_LOTS il
    OUTER APPLY (SELECT SUM(ll.QtyAvailable) AS AtLocations FROM dbo.LOT_LOCATIONS ll WHERE ll.LotID = il.ID) x
    WHERE il.IsDeleted = 0
      AND ABS((il.QtyReceived - il.QtyConsumed) - ISNULL(x.AtLocations, 0)) > 0.0001
    ORDER BY il.ID;

    /* 3c. O'chirilgan kirimning hali "tirik" DLOC lotlari (B03: o'chirishda lot bekor qilinmasdi) */
    SELECT il.ID AS LotID, il.LotNo, il.IncomeID, i.DocNo, il.ProductID, il.WarehouseID,
           il.QtyReceived, il.QtyConsumed, ISNULL(x.AtLocations, 0) AS AtLocations
    FROM dbo.INCOME_LOTS il
    JOIN dbo.INCOMES i ON i.ID = il.IncomeID
    OUTER APPLY (SELECT SUM(ll.QtyAvailable) AS AtLocations FROM dbo.LOT_LOCATIONS ll WHERE ll.LotID = il.ID) x
    WHERE il.IsDeleted = 0 AND ISNULL(i.IsDeleted, 0) = 1
    ORDER BY il.ID;

/* ---------------------------------------------------------------------
   4. EXPENSE_PRODUCT_ALLOCATIONS muammolari (B05: tahrirda ajratmalar qaytarilmasdi)
   --------------------------------------------------------------------- */
    /* 4a. Yetim ajratmalar: chiqim qatori yo'q */
    SELECT a.*
    FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
    LEFT JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ID = a.ExpenseProductID
    WHERE ep.ID IS NULL;

    /* 4b. O'chirilgan chiqim qatorlaridagi ajratmalar (lot qaytarilmagan) */
    SELECT a.ID AS AllocationID, a.ExpenseProductID, ep.ExpenseID, e.DocNo, a.LotID, a.FromLocationID, a.Qty
    FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
    JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ID = a.ExpenseProductID
    JOIN dbo.EXPENSES e ON e.ID = ep.ExpenseID
    WHERE e.IsDeleted = 1;

    /* 4c. Lot QtyConsumed va ajratmalar yig'indisi mos emas */
    SELECT il.ID AS LotID, il.LotNo, il.QtyConsumed, ISNULL(x.Allocated, 0) AS AllocatedQty,
           il.QtyConsumed - ISNULL(x.Allocated, 0) AS Diff
    FROM dbo.INCOME_LOTS il
    OUTER APPLY (
        SELECT SUM(a.Qty) AS Allocated
        FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
        JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ID = a.ExpenseProductID
        JOIN dbo.EXPENSES e ON e.ID = ep.ExpenseID AND e.IsDeleted = 0
        WHERE a.LotID = il.ID
    ) x
    WHERE il.IsDeleted = 0
      AND ABS(il.QtyConsumed - ISNULL(x.Allocated, 0)) > 0.0001
    ORDER BY ABS(il.QtyConsumed - ISNULL(x.Allocated, 0)) DESC;
END

/* ---------------------------------------------------------------------
   5. O'chirilgan chiqimning o'chirilmagan bog'langan kirimi (B04).
      Marker aniq moslik bilan: "EXPENSE_ID:12" dan keyin raqam kelmasligi kerak.
   --------------------------------------------------------------------- */
SELECT e.ID AS ExpenseID, e.DocNo AS ExpenseDocNo, i.ID AS IncomeID, i.DocNo AS IncomeDocNo,
       i.RecieveStatus, i.ReceivingWHID,
       CASE WHEN ISNULL(i.RecieveStatus, 0) = 1
            THEN 'QABUL QILINGAN: tovar ikki omborda (yuboruvchiga qaytarilgan + qabul qiluvchida)'
            ELSE 'Kutilmoqda: "Qabul qilish" yo''qdan qoldiq yaratadi' END AS Risk
FROM dbo.EXPENSES e
JOIN dbo.INCOMES i
  ON ISNULL(i.IsDeleted, 0) = 0
 AND (   i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20))
      OR i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20)) + '[^0-9]%'
      OR i.Description LIKE '%EXPENS ID: ' + CAST(e.ID AS varchar(20))
      OR i.Description LIKE '%EXPENS ID: ' + CAST(e.ID AS varchar(20)) + '[^0-9]%')
WHERE e.IsDeleted = 1
ORDER BY e.ID;

/* ---------------------------------------------------------------------
   6. Bog'langan kirim qatorlari (mahsulot bo'yicha jami) chiqim qatorlaridan farq qiladi (B06:
      LIKE '%EXPENSE_ID:12%' boshqa chiqimning kirimini qayta yozgan bo'lishi mumkin).
   --------------------------------------------------------------------- */
;WITH Link AS (
    SELECT e.ID AS ExpenseID, i.ID AS IncomeID
    FROM dbo.EXPENSES e
    JOIN dbo.INCOMES i
      ON ISNULL(i.IsDeleted, 0) = 0
     AND (   i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20))
          OR i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20)) + '[^0-9]%')
    WHERE e.IsDeleted = 0
), ExpQ AS (
    SELECT l.ExpenseID, l.IncomeID, ep.ProductID, SUM(ep.Amount) AS Qty
    FROM Link l JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ExpenseID = l.ExpenseID
    GROUP BY l.ExpenseID, l.IncomeID, ep.ProductID
), IncQ AS (
    SELECT l.ExpenseID, l.IncomeID, ip.ProductID, SUM(ip.Amount) AS Qty
    FROM Link l JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = l.IncomeID
    GROUP BY l.ExpenseID, l.IncomeID, ip.ProductID
)
SELECT COALESCE(x.ExpenseID, n.ExpenseID) AS ExpenseID, COALESCE(x.IncomeID, n.IncomeID) AS IncomeID,
       COALESCE(x.ProductID, n.ProductID) AS ProductID,
       ISNULL(x.Qty, 0) AS ExpenseQty, ISNULL(n.Qty, 0) AS IncomeQty
FROM ExpQ x
FULL JOIN IncQ n ON n.ExpenseID = x.ExpenseID AND n.IncomeID = x.IncomeID AND n.ProductID = x.ProductID
WHERE ABS(ISNULL(x.Qty, 0) - ISNULL(n.Qty, 0)) > 0.0001
ORDER BY 1, 3;

/* 6b. Bitta chiqimga bir nechta tirik bog'langan kirim */
SELECT e.ID AS ExpenseID, e.DocNo, COUNT(*) AS LinkedIncomeCount
FROM dbo.EXPENSES e
JOIN dbo.INCOMES i
  ON ISNULL(i.IsDeleted, 0) = 0
 AND (   i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20))
      OR i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20)) + '[^0-9]%')
WHERE e.IsDeleted = 0
GROUP BY e.ID, e.DocNo
HAVING COUNT(*) > 1;

/* ---------------------------------------------------------------------
   7. Takroriy DocNo (o'chirilmaganlar orasida) — B07.
   --------------------------------------------------------------------- */
SELECT 'INCOMES' AS Tbl, DocNo, COUNT(*) AS Cnt, MIN(ID) AS MinID, MAX(ID) AS MaxID
FROM dbo.INCOMES
WHERE ISNULL(IsDeleted, 0) = 0 AND DocNo IS NOT NULL AND DocNo <> ''
GROUP BY DocNo HAVING COUNT(*) > 1
UNION ALL
SELECT 'EXPENSES', DocNo, COUNT(*), MIN(ID), MAX(ID)
FROM dbo.EXPENSES
WHERE IsDeleted = 0 AND DocNo IS NOT NULL AND DocNo <> ''
GROUP BY DocNo HAVING COUNT(*) > 1
ORDER BY Cnt DESC;

/* ---------------------------------------------------------------------
   8. Ko'p qatorli PTST o'tkazmasi bir nechta chiqim/kirim juftiga bo'lingan (B10):
      bir xil TRNSF_ DocNo'li chiqimlar soni va har birining kirimi.
   --------------------------------------------------------------------- */
SELECT e.DocNo, COUNT(*) AS ExpenseCount, SUM(CASE WHEN e.SendStatus = 1 THEN 1 ELSE 0 END) AS SentCount,
       MIN(e.IssueDateTime) AS FirstAt, MAX(e.IssueDateTime) AS LastAt
FROM dbo.EXPENSES e
WHERE e.IsDeleted = 0 AND e.DocNo LIKE 'TRNSF[_]%'
GROUP BY e.DocNo
HAVING COUNT(*) > 1
ORDER BY FirstAt DESC;
