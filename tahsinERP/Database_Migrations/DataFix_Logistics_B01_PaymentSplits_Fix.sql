/*
  LOG-B01 — noto'g'ri taqsimlangan yo'l varaqasi to'lovlarini TUZATISH.

  AVVAL DataFix_Logistics_B01_PaymentSplits_Find.sql ni ishga tushirib natijani ko'rib chiqing
  va bazaning zaxira nusxasini oling.

  Nima qiladi: Find skripti bilan bir xil hisob (to'lovlar yaratilish tartibida, tuzatilgan
  algoritm bilan qayta taqsimlanadi), so'ng tanlangan to'lovlarning F_WAYBILL_PAYMENT_SPLITS
  qatorlarini o'chirib, kutilgan qatorlarni yozadi. To'lov summasi va FIN_TRANSACTIONS
  o'zgarmaydi (moliya yozuvi to'lovning umumiy summasi bo'yicha, taqsimotga bog'liq emas).

  Parametrlar:
    @Apply         = 0 — faqat ko'rsatadi va ROLLBACK qiladi (standart); 1 — COMMIT.
    @OnlyLikelyB01 = 1 — faqat xato belgisi aniq to'lovlar (asosiy yo'l varaqasiga kam ajratilgan);
                     0 — barcha farqli to'lovlar (Edit orqali saqlanganlar ham yaratilish tartibida
                     qayta hisoblanadi).
    Eslatma: kutilgan taqsimot oldingi barcha to'lovlar tuzatilgan deb hisoblanadi; @OnlyLikelyB01 = 1
    bo'lsa, tuzatilmay qolgan oldingi to'lovlar tufayli keyingi summalar biroz farq qilishi mumkin.
*/
DECLARE @Apply BIT = 0;
DECLARE @OnlyLikelyB01 BIT = 1;

SET NOCOUNT ON;

DECLARE @Eps FLOAT = 0.01;
DECLARE @SplitPayerType TINYINT = 99;

IF OBJECT_ID('tempdb..#Pay') IS NOT NULL DROP TABLE #Pay;
IF OBJECT_ID('tempdb..#Sim') IS NOT NULL DROP TABLE #Sim;
IF OBJECT_ID('tempdb..#Direct') IS NOT NULL DROP TABLE #Direct;
IF OBJECT_ID('tempdb..#W') IS NOT NULL DROP TABLE #W;
IF OBJECT_ID('tempdb..#Expected') IS NOT NULL DROP TABLE #Expected;
IF OBJECT_ID('tempdb..#Diff') IS NOT NULL DROP TABLE #Diff;

-- 1) Bir nechta yo'l varaqasiga bo'lingan, o'chirilmagan to'lovlar
SELECT p.ID, p.WaybillID, p.Payment_Amount, p.CurrencyID, p.PaymentDate
INTO #Pay
FROM dbo.F_WAYBILL_PAYMENTS p
WHERE p.IsDeleted = 0
  AND p.Payment_Amount > 0
  AND (SELECT COUNT(DISTINCT s.WaybillID)
         FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
        WHERE s.PayerType = @SplitPayerType AND s.PayerID = p.ID) >= 2;

-- 2) Simulyatsiya holati: barcha o'chirilmagan to'lovlarning joriy split'lari
SELECT s.PayerID AS PaymentID, s.WaybillID, s.Amount
INTO #Sim
FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
JOIN dbo.F_WAYBILL_PAYMENTS p ON p.ID = s.PayerID AND p.IsDeleted = 0
WHERE s.PayerType = @SplitPayerType;

-- 3) Split'siz (to'g'ridan-to'g'ri) to'lovlar
SELECT p.ID, p.WaybillID, p.Payment_Amount
INTO #Direct
FROM dbo.F_WAYBILL_PAYMENTS p
WHERE p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
                   WHERE s.PayerType = @SplitPayerType AND s.PayerID = p.ID);

CREATE TABLE #W (Seq INT IDENTITY(1,1) PRIMARY KEY, WaybillID INT NOT NULL, Outstanding FLOAT NOT NULL, Alloc FLOAT NOT NULL);
CREATE TABLE #Expected (PaymentID INT NOT NULL, WaybillID INT NOT NULL, Amount FLOAT NOT NULL);

-- 4) Yaratilish tartibida (ID) qayta taqsimlash: ro'yxat tartibida qoldiqlar yopiladi, ortgan summa
--    teng bo'linadi. "Allaqachon to'langan" faqat shu to'lovdan OLDIN kiritilgan to'lovlardan olinadi.
--    Ro'yxat tartibi = formadagi ListBox tartibi (IssueDate DESC, WaybillNo).
DECLARE @pid INT, @amt FLOAT, @n INT, @rest FLOAT;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT ID, Payment_Amount FROM #Pay ORDER BY ID;
OPEN cur;
FETCH NEXT FROM cur INTO @pid, @amt;
WHILE @@FETCH_STATUS = 0
BEGIN
    TRUNCATE TABLE #W;

    INSERT INTO #W (WaybillID, Outstanding, Alloc)
    SELECT w.ID,
           CASE WHEN x.Outstanding > 0 THEN x.Outstanding ELSE 0 END,
           0
    FROM (SELECT DISTINCT s.WaybillID FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
           WHERE s.PayerType = @SplitPayerType AND s.PayerID = @pid) d
    JOIN dbo.F_WAYBILLS w ON w.ID = d.WaybillID
    CROSS APPLY (SELECT ISNULL(w.Amount, 0)
                        - ISNULL((SELECT SUM(dp.Payment_Amount) FROM #Direct dp WHERE dp.WaybillID = w.ID AND dp.ID < @pid), 0)
                        - ISNULL((SELECT SUM(sm.Amount) FROM #Sim sm WHERE sm.WaybillID = w.ID AND sm.PaymentID < @pid), 0)
                        AS Outstanding) x
    ORDER BY w.IssueDate DESC, w.WaybillNo;

    ;WITH c AS (
        SELECT Seq, Outstanding,
               SUM(Outstanding) OVER (ORDER BY Seq ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS Before
        FROM #W)
    UPDATE w SET Alloc = CASE
            WHEN @amt - ISNULL(c.Before, 0) <= 0 THEN 0
            WHEN @amt - ISNULL(c.Before, 0) < c.Outstanding THEN @amt - ISNULL(c.Before, 0)
            ELSE c.Outstanding END
    FROM #W w JOIN c ON c.Seq = w.Seq;

    SELECT @n = COUNT(*), @rest = @amt - ISNULL(SUM(Alloc), 0) FROM #W;
    IF @n > 0 AND @rest > 0.005
        UPDATE #W SET Alloc = Alloc + @rest / @n;

    DELETE FROM #Sim WHERE PaymentID = @pid;
    INSERT INTO #Sim (PaymentID, WaybillID, Amount) SELECT @pid, WaybillID, Alloc FROM #W WHERE Alloc > 0;
    INSERT INTO #Expected (PaymentID, WaybillID, Amount) SELECT @pid, WaybillID, Alloc FROM #W WHERE Alloc > 0;

    FETCH NEXT FROM cur INTO @pid, @amt;
END
CLOSE cur;
DEALLOCATE cur;

-- 5) Joriy va kutilgan taqsimot farqi
SELECT COALESCE(cs.PaymentID, e.PaymentID) AS PaymentID,
       COALESCE(cs.WaybillID, e.WaybillID) AS WaybillID,
       ISNULL(cs.Amount, 0) AS CurrentAmount,
       ISNULL(e.Amount, 0) AS ExpectedAmount
INTO #Diff
FROM (SELECT s.PayerID AS PaymentID, s.WaybillID, SUM(s.Amount) AS Amount
        FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
        JOIN #Pay p ON p.ID = s.PayerID
       WHERE s.PayerType = @SplitPayerType
       GROUP BY s.PayerID, s.WaybillID) cs
FULL OUTER JOIN #Expected e ON e.PaymentID = cs.PaymentID AND e.WaybillID = cs.WaybillID
WHERE ABS(ISNULL(cs.Amount, 0) - ISNULL(e.Amount, 0)) > @Eps;

IF OBJECT_ID('tempdb..#Target') IS NOT NULL DROP TABLE #Target;
SELECT DISTINCT d.PaymentID
INTO #Target
FROM #Diff d
JOIN #Pay p ON p.ID = d.PaymentID
WHERE @OnlyLikelyB01 = 0
   OR EXISTS (SELECT 1 FROM #Diff x WHERE x.PaymentID = p.ID AND x.WaybillID = p.WaybillID
                                      AND x.ExpectedAmount > x.CurrentAmount + @Eps);

BEGIN TRANSACTION;

DECLARE @Comments TABLE (PaymentID INT PRIMARY KEY, Comment NVARCHAR(MAX));
INSERT INTO @Comments (PaymentID, Comment)
SELECT s.PayerID, MAX(s.Comment)
FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
JOIN #Target t ON t.PaymentID = s.PayerID
WHERE s.PayerType = @SplitPayerType
GROUP BY s.PayerID;

DELETE s
FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
JOIN #Target t ON t.PaymentID = s.PayerID
WHERE s.PayerType = @SplitPayerType;

INSERT INTO dbo.F_WAYBILL_PAYMENT_SPLITS (WaybillID, PayerType, PayerID, Amount, CurrencyID, Comment)
SELECT e.WaybillID, @SplitPayerType, e.PaymentID, e.Amount, p.CurrencyID,
       ISNULL(c.Comment, N'WaybillPaymentLink')
FROM #Expected e
JOIN #Target t ON t.PaymentID = e.PaymentID
JOIN #Pay p ON p.ID = e.PaymentID
LEFT JOIN @Comments c ON c.PaymentID = e.PaymentID;

SELECT t.PaymentID, e.WaybillID, e.Amount AS NewAmount
FROM #Target t
JOIN #Expected e ON e.PaymentID = t.PaymentID
ORDER BY t.PaymentID, e.WaybillID;

IF @Apply = 1
BEGIN
    COMMIT TRANSACTION;
    PRINT 'LOG-B01: split taqsimoti tuzatildi.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'LOG-B01: sinov rejimi (@Apply = 0), o''zgarishlar bekor qilindi.';
END
