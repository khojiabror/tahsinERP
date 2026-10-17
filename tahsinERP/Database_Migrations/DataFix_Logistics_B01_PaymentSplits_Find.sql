/*
  LOG-B01 — noto'g'ri taqsimlangan yo'l varaqasi to'lovlarini TOPISH (faqat o'qiydi).

  Xato: WaybillPayment/Create bir nechta yo'l varaqasi tanlanganda yangi to'lovni birinchi
  yo'l varaqasiga "allaqachon to'langan" deb hisoblab, taqsimotni keyingilarga surib yuborardi.
  Misol: W1=100, W2=100, to'lov 100 -> hammasi W2 ga, W1 to'lanmagan bo'lib qolardi.

  Skript har bir ko'p-yo'l-varaqali to'lovni yaratilish tartibida (ID) tuzatilgan algoritm bilan
  (WaybillLedgerService.Allocate) qayta taqsimlaydi — faqat #temp jadvallarda — va joriy
  split'lardan farqini ko'rsatadi. Real jadvallarga HECH NARSA yozilmaydi.

  Natija:
    1-jadval — farqli qatorlar (to'lov, yo'l varaqasi, joriy / kutilgan summa).
    2-jadval — to'lovlar bo'yicha xulosa; LikelyB01 = 1 — asosiy (birinchi) yo'l varaqasiga
               kutilganidan kam ajratilgan, ya'ni aynan shu xato belgisi.
  Tuzatish: DataFix_Logistics_B01_PaymentSplits_Fix.sql
*/
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

-- Natija 1: farqli qatorlar
SELECT d.PaymentID, p.PaymentDate, p.Payment_Amount, d.WaybillID, w.WaybillNo, w.Amount AS WaybillAmount,
       d.CurrentAmount, d.ExpectedAmount, d.ExpectedAmount - d.CurrentAmount AS Delta
FROM #Diff d
JOIN #Pay p ON p.ID = d.PaymentID
JOIN dbo.F_WAYBILLS w ON w.ID = d.WaybillID
ORDER BY d.PaymentID, w.IssueDate DESC, w.WaybillNo;

-- Natija 2: to'lovlar bo'yicha xulosa
SELECT p.ID AS PaymentID, p.PaymentDate, p.Payment_Amount, p.WaybillID AS MainWaybillID,
       COUNT(*) AS DiffRows,
       CASE WHEN EXISTS (SELECT 1 FROM #Diff x WHERE x.PaymentID = p.ID AND x.WaybillID = p.WaybillID
                                                AND x.ExpectedAmount > x.CurrentAmount + @Eps)
            THEN 1 ELSE 0 END AS LikelyB01
FROM #Pay p
JOIN #Diff d ON d.PaymentID = p.ID
GROUP BY p.ID, p.PaymentDate, p.Payment_Amount, p.WaybillID
ORDER BY p.ID;
