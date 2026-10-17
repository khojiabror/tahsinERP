-- =====================================================================
-- DataFix MES-03 (TOPUVCHI, faqat SELECT) — audit MES-B09.
-- Eski kod reja (PRODUCTPLANS) muddati/miqdori tahrirlanganda PRODUCTPLANS_DAILY ni qayta yaratmasdi.
-- Dashboard va hisobotlar kunlik qatorlarni yig'adi — header bilan farq qiladi.
--  A) Header oralig'idan tashqaridagi kunlik qatorlar
--  B) Header oralig'ida kunlik qatori yo'q kunlar soni va kunlik yig'indi ≠ header miqdori
-- Eslatma: kunlik qatorlar qo'lda tahrirlangan bo'lishi mumkin — yig'indi farqi har doim ham xato emas.
-- Ma'lumot o'zgarmaydi.
-- =====================================================================
SET NOCOUNT ON;

-- A) Oraliqdan tashqaridagi kunlik qatorlar
SELECT p.ID AS PlanID, pr.PNo, p.StartDate, p.DueDate, d.ID AS DailyID, d.[Day], d.Total,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.QUALITY_INSPECTION q WHERE q.ProductPlanDailyID = d.ID) THEN 1 ELSE 0 END AS ReferencedByQI
FROM dbo.PRODUCTPLANS p
JOIN dbo.PRODUCTS pr ON pr.ID = p.ProductID
JOIN dbo.PRODUCTPLANS_DAILY d ON d.PlanID = p.ID
WHERE p.IsDeleted = 0
  AND (CAST(d.[Day] AS date) < CAST(p.StartDate AS date) OR CAST(d.[Day] AS date) > CAST(p.DueDate AS date))
ORDER BY p.ID, d.[Day];

-- B) Header va kunlik reja farqi
SELECT p.ID AS PlanID, pr.PNo, p.StartDate, p.DueDate, p.Amount,
       DATEDIFF(day, p.StartDate, p.DueDate) + 1 AS PlanDays,
       COUNT(DISTINCT CASE WHEN CAST(d.[Day] AS date) BETWEEN CAST(p.StartDate AS date) AND CAST(p.DueDate AS date) THEN CAST(d.[Day] AS date) END) AS DaysWithRows,
       SUM(CASE WHEN CAST(d.[Day] AS date) BETWEEN CAST(p.StartDate AS date) AND CAST(p.DueDate AS date) THEN d.Total ELSE 0 END) AS DailyTotalInRange
FROM dbo.PRODUCTPLANS p
JOIN dbo.PRODUCTS pr ON pr.ID = p.ProductID
LEFT JOIN dbo.PRODUCTPLANS_DAILY d ON d.PlanID = p.ID
WHERE p.IsDeleted = 0
GROUP BY p.ID, pr.PNo, p.StartDate, p.DueDate, p.Amount
HAVING COUNT(DISTINCT CASE WHEN CAST(d.[Day] AS date) BETWEEN CAST(p.StartDate AS date) AND CAST(p.DueDate AS date) THEN CAST(d.[Day] AS date) END)
           <> DATEDIFF(day, p.StartDate, p.DueDate) + 1
    OR SUM(CASE WHEN CAST(d.[Day] AS date) BETWEEN CAST(p.StartDate AS date) AND CAST(p.DueDate AS date) THEN d.Total ELSE 0 END) < p.Amount
ORDER BY p.StartDate DESC;
