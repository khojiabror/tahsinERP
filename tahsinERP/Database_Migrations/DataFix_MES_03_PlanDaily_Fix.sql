-- =====================================================================
-- DataFix MES-03 (TUZATUVCHI) — audit MES-B09. Avval ..._Find.sql natijasini ko'rib chiqing!
-- Header (StartDate..DueDate) bilan mos kelmaydigan kunlik rejalar:
--  1) Oraliqdan TASHQARIDAGI kunlik qatorlar: sifat nazoratiga bog'lanmaganlari o'chiriladi,
--     bog'langanlari 0 qilinadi (eski attribution buzilmasin).
--  2) Oraliq ICHIDA qatori yo'q kunlar qo'shiladi: miqdor = CEILING(Amount / kunlar soni)
--     (ilovadagi AddDailyPlans bilan bir xil). Mavjud (qo'lda tahrirlangan) kun qatorlari o'zgartirilmaydi.
-- Lokal bazada ko'rilgan holat: bir kunlik rejalarning kunlik qatori bir kun keyinga surilgan
-- (masalan reja 09.04, kunlik qator 10.04) — 1+2 qadam uni to'g'ri kunga ko'chiradi.
--
-- Tranzaksiya ichida. Prod'da: natijani ko'rib COMMIT yoki ROLLBACK.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#outside') IS NOT NULL DROP TABLE #outside;
SELECT d.ID, d.PlanID, d.[Day], d.DayShift, d.NightShift, d.Total,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.QUALITY_INSPECTION q WHERE q.ProductPlanDailyID = d.ID) THEN 1 ELSE 0 END AS ReferencedByQI
INTO #outside
FROM dbo.PRODUCTPLANS p
JOIN dbo.PRODUCTPLANS_DAILY d ON d.PlanID = p.ID
WHERE p.IsDeleted = 0
  AND (CAST(d.[Day] AS date) < CAST(p.StartDate AS date) OR CAST(d.[Day] AS date) > CAST(p.DueDate AS date));

UPDATE d SET d.DayShift = 0, d.NightShift = 0  -- Total hisoblanadigan ustun
FROM dbo.PRODUCTPLANS_DAILY d JOIN #outside o ON o.ID = d.ID
WHERE o.ReferencedByQI = 1;
PRINT CONCAT(@@ROWCOUNT, ' ta QI''ga bog''langan oraliqdan tashqari kunlik qator 0 qilindi.');

DELETE d
FROM dbo.PRODUCTPLANS_DAILY d JOIN #outside o ON o.ID = d.ID
WHERE o.ReferencedByQI = 0;
PRINT CONCAT(@@ROWCOUNT, ' ta oraliqdan tashqaridagi kunlik qator o''chirildi.');

-- Oraliq ichida yetishmayotgan kunlar
;WITH Days AS (
    SELECT p.ID AS PlanID, CAST(p.StartDate AS date) AS [Day], CAST(p.DueDate AS date) AS LastDay,
           p.Amount, DATEDIFF(day, p.StartDate, p.DueDate) + 1 AS DayCount
    FROM dbo.PRODUCTPLANS p
    WHERE p.IsDeleted = 0 AND p.DueDate >= p.StartDate
    UNION ALL
    SELECT PlanID, DATEADD(day, 1, [Day]), LastDay, Amount, DayCount
    FROM Days
    WHERE [Day] < LastDay
)
INSERT INTO dbo.PRODUCTPLANS_DAILY (PlanID, [Day], DayShift, NightShift)
SELECT x.PlanID, x.[Day], CEILING(x.Amount / x.DayCount), 0
FROM Days x
WHERE NOT EXISTS (SELECT 1 FROM dbo.PRODUCTPLANS_DAILY d WHERE d.PlanID = x.PlanID AND CAST(d.[Day] AS date) = x.[Day])
OPTION (MAXRECURSION 3660);
PRINT CONCAT(@@ROWCOUNT, ' ta yetishmayotgan kunlik qator qo''shildi.');

SELECT * FROM #outside ORDER BY PlanID, [Day];

-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
