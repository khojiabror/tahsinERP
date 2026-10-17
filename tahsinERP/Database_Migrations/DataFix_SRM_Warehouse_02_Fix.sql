/* =====================================================================
   DataFix_SRM_Warehouse_02_Fix.sql  — MA'LUMOTNI O'ZGARTIRADI
   Audit 2026-10, SRM — Ombor/Inventar

   !!! Avval DataFix_SRM_Warehouse_01_Check.sql natijalarini ko'rib chiqing.
   !!! Avval TEST nusxada ishga tushiring, bazaning zaxira nusxasini oling.
   !!! Standart holatda @Commit = 0 — hamma o'zgarish ROLLBACK qilinadi (quruq yurish).
       Natija sonlari to'g'ri bo'lsa, @Commit = 1 qilib qayta ishga tushiring.

   Nimalarni tuzatadi (faqat aniq, xavfsiz holatlar):
     F1. PRODUCT_STOCKS dublikatlarini birlashtirish (yig'indi eng kichik ID'ga, qolganlari o'chiriladi).
         Shundan keyin Audit_2026_10_SRM_Warehouse_Indexes.sql dagi UNIQUE indeksni qo'yish mumkin.
     F2. Yetim EXPENSE_PRODUCT_ALLOCATIONS (chiqim qatori yo'q) — o'chiriladi.
     F3. O'CHIRILGAN chiqim qatorlaridagi ajratmalar: lot/joy qoldig'i tiklanadi
         (LOT_LOCATIONS += Qty, INCOME_LOTS.QtyConsumed -= Qty), ajratma o'chiriladi.
         PRODUCT_STOCKS ga tegilmaydi — eski Delete uni allaqachon qaytargan (faqat ledger qaytmagan edi).
     F4. O'chirilgan kirimning ishlatilmagan (QtyConsumed = 0) DLOC lotlari bekor qilinadi
         (LOT_LOCATIONS o'chiriladi, lot IsDeleted = 1). PRODUCT_STOCKS ga tegilmaydi — eski Delete
         uni allaqachon ayirgan.
     F5. O'chirilgan chiqimning QABUL QILINMAGAN bog'langan kirimi soft-delete qilinadi (SRM-B04).

   Nimalarni TUZATMAYDI (qo'lda ko'rib chiqish kerak):
     - 01_Check §2 dagi hujjat/qoldiq farqlari: tarixda qoldiq hujjatsiz ham yozilgan, shuning uchun
       avtomatik "to'g'rilash" xavfli. Tavsiya: har ombor uchun Inventory/Adjust (hujjatli inventarizatsiya).
     - O'chirilgan chiqimning QABUL QILINGAN bog'langan kirimi (01_Check §5, "QABUL QILINGAN"):
       tovar ikki omborda — biznes qarori bilan (kirimni o'chirish yoki chiqimni tiklash).
     - Ishlatilgan (QtyConsumed > 0) lotlar, takroriy DocNo (01_Check §7) va bo'lingan TRNSF (§8):
       tarixiy hujjat raqamlari o'zgartirilmaydi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Commit BIT = 0;   -- 1 = saqlash, 0 = faqat sinov (ROLLBACK)

BEGIN TRAN;

/* ---------------- F1. PRODUCT_STOCKS dublikatlari ---------------- */
;WITH Dups AS (
    SELECT WHID, ProductID, MIN(ID) AS KeepID, SUM(Amount) AS Total
    FROM dbo.PRODUCT_STOCKS WITH (UPDLOCK, HOLDLOCK)
    GROUP BY WHID, ProductID
    HAVING COUNT(*) > 1
)
UPDATE s SET s.Amount = d.Total
FROM dbo.PRODUCT_STOCKS s
JOIN Dups d ON d.KeepID = s.ID;
PRINT 'F1 birlashtirilgan (keeper yangilandi): ' + CAST(@@ROWCOUNT AS varchar(20));

DELETE s
FROM dbo.PRODUCT_STOCKS s
WHERE EXISTS (
    SELECT 1 FROM dbo.PRODUCT_STOCKS k
    WHERE k.WHID = s.WHID AND k.ProductID = s.ProductID AND k.ID < s.ID);
PRINT 'F1 o''chirilgan dublikat qatorlar: ' + CAST(@@ROWCOUNT AS varchar(20));

IF OBJECT_ID('dbo.EXPENSE_PRODUCT_ALLOCATIONS', 'U') IS NOT NULL
BEGIN
    /* ---------------- F2. Yetim ajratmalar ---------------- */
    DELETE a
    FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
    WHERE NOT EXISTS (SELECT 1 FROM dbo.EXPENSE_PRODUCTS ep WHERE ep.ID = a.ExpenseProductID);
    PRINT 'F2 yetim ajratmalar o''chirildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    /* ---------------- F3. O'chirilgan chiqimlar ajratmalari: ledger'ni tiklash ---------------- */
    IF OBJECT_ID('tempdb..#DeadAlloc') IS NOT NULL DROP TABLE #DeadAlloc;
    SELECT a.ID, a.LotID, a.FromLocationID, a.Qty, il.ProductID
    INTO #DeadAlloc
    FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
    JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ID = a.ExpenseProductID
    JOIN dbo.EXPENSES e ON e.ID = ep.ExpenseID AND e.IsDeleted = 1
    JOIN dbo.INCOME_LOTS il ON il.ID = a.LotID;

    -- joy qoldig'i (lot+joy juftligi mavjud bo'lsa — oshiriladi, bo'lmasa yaratiladi)
    UPDATE ll SET ll.QtyAvailable = ll.QtyAvailable + x.Qty, ll.LastMovementAt = GETDATE()
    FROM dbo.LOT_LOCATIONS ll
    JOIN (SELECT LotID, FromLocationID, SUM(Qty) AS Qty FROM #DeadAlloc GROUP BY LotID, FromLocationID) x
      ON x.LotID = ll.LotID AND x.FromLocationID = ll.LocationID;
    PRINT 'F3 LOT_LOCATIONS oshirildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    INSERT INTO dbo.LOT_LOCATIONS (LotID, LocationID, ProductID, QtyAvailable, LastMovementAt)
    SELECT x.LotID, x.FromLocationID, x.ProductID, x.Qty, GETDATE()
    FROM (SELECT LotID, FromLocationID, MIN(ProductID) AS ProductID, SUM(Qty) AS Qty
          FROM #DeadAlloc GROUP BY LotID, FromLocationID) x
    WHERE NOT EXISTS (SELECT 1 FROM dbo.LOT_LOCATIONS ll WHERE ll.LotID = x.LotID AND ll.LocationID = x.FromLocationID);
    PRINT 'F3 LOT_LOCATIONS yaratildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    UPDATE il SET il.QtyConsumed = CASE WHEN il.QtyConsumed - x.Qty < 0 THEN 0 ELSE il.QtyConsumed - x.Qty END
    FROM dbo.INCOME_LOTS il
    JOIN (SELECT LotID, SUM(Qty) AS Qty FROM #DeadAlloc GROUP BY LotID) x ON x.LotID = il.ID;
    PRINT 'F3 INCOME_LOTS.QtyConsumed qaytarildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    DELETE a FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a JOIN #DeadAlloc d ON d.ID = a.ID;
    PRINT 'F3 ajratmalar o''chirildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    /* ---------------- F4. O'chirilgan kirimning ishlatilmagan lotlari ---------------- */
    IF OBJECT_ID('tempdb..#DeadLots') IS NOT NULL DROP TABLE #DeadLots;
    SELECT il.ID
    INTO #DeadLots
    FROM dbo.INCOME_LOTS il
    JOIN dbo.INCOMES i ON i.ID = il.IncomeID
    WHERE il.IsDeleted = 0 AND ISNULL(i.IsDeleted, 0) = 1 AND il.QtyConsumed = 0;

    DELETE ll FROM dbo.LOT_LOCATIONS ll JOIN #DeadLots d ON d.ID = ll.LotID;
    PRINT 'F4 LOT_LOCATIONS o''chirildi: ' + CAST(@@ROWCOUNT AS varchar(20));

    UPDATE il SET il.IsDeleted = 1, il.QtyConsumed = il.QtyReceived
    FROM dbo.INCOME_LOTS il JOIN #DeadLots d ON d.ID = il.ID;
    PRINT 'F4 lotlar bekor qilindi: ' + CAST(@@ROWCOUNT AS varchar(20));
END

/* ---------------- F5. O'chirilgan chiqimning kutilayotgan bog'langan kirimi ---------------- */
UPDATE i SET i.IsDeleted = 1
FROM dbo.INCOMES i
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ISNULL(i.RecieveStatus, 0) = 0
  AND EXISTS (
      SELECT 1 FROM dbo.EXPENSES e
      WHERE e.IsDeleted = 1
        AND (   i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20))
             OR i.Description LIKE '%EXPENSE_ID:' + CAST(e.ID AS varchar(20)) + '[^0-9]%'
             OR i.Description LIKE '%EXPENS ID: ' + CAST(e.ID AS varchar(20))
             OR i.Description LIKE '%EXPENS ID: ' + CAST(e.ID AS varchar(20)) + '[^0-9]%'));
PRINT 'F5 kutilayotgan bog''langan kirimlar bekor qilindi: ' + CAST(@@ROWCOUNT AS varchar(20));

IF @Commit = 1
BEGIN
    COMMIT;
    PRINT 'SAQLANDI (COMMIT).';
END
ELSE
BEGIN
    ROLLBACK;
    PRINT 'SINOV: hech narsa saqlanmadi (ROLLBACK). Saqlash uchun @Commit = 1.';
END
