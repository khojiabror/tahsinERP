-- =====================================================================
-- DataFix MES-03 (TUZATUVCHI) — audit MES-B09. Avval ..._Find.sql natijasini ko'rib chiqing!
-- Faqat header oralig'idan TASHQARIDAGI kunlik qatorlarni tozalaydi:
--  - sifat nazoratiga (QUALITY_INSPECTION.ProductPlanDailyID) bog'lanmaganlari o'chiriladi;
--  - bog'langanlari 0 qilinadi (eski attribution buzilmasin).
-- Oraliq ichidagi yetishmayotgan kunlar / yig'indi farqi uchun: rejani ProductPlan/Edit'da ochib saqlang —
-- yangi kod muddat/miqdor o'zgarganda kunlik rejani qayta taqsimlaydi (qo'lda kiritilganlar qayta yoziladi).
--
-- Tranzaksiya ichida; COMMIT yoki ROLLBACK ni o'zingiz tanlang. HECH QAYERDA AVTOMATIK ISHGA TUSHIRILMAGAN.
-- =====================================================================
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#outside') IS NOT NULL DROP TABLE #outside;
SELECT d.ID, d.PlanID, d.[Day], d.DayShift, d.NightShift, d.Total,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.QUALITY_INSPECTION q WHERE q.ProductPlanDailyID = d.ID) THEN 1 ELSE 0 END AS ReferencedByQI
INTO #outside
FROM dbo.PRODUCTPLANS p
JOIN dbo.PRODUCTPLANS_DAILY d ON d.PlanID = p.ID
WHERE p.IsDeleted = 0
  AND (CAST(d.[Day] AS date) < CAST(p.StartDate AS date) OR CAST(d.[Day] AS date) > CAST(p.DueDate AS date));

UPDATE d SET d.DayShift = 0, d.NightShift = 0, d.Total = 0
FROM dbo.PRODUCTPLANS_DAILY d JOIN #outside o ON o.ID = d.ID
WHERE o.ReferencedByQI = 1;
PRINT CONCAT(@@ROWCOUNT, ' ta QI''ga bog''langan kunlik qator 0 qilindi.');

DELETE d
FROM dbo.PRODUCTPLANS_DAILY d JOIN #outside o ON o.ID = d.ID
WHERE o.ReferencedByQI = 0;
PRINT CONCAT(@@ROWCOUNT, ' ta oraliqdan tashqaridagi kunlik qator o''chirildi.');

SELECT * FROM #outside ORDER BY PlanID, [Day];

-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
