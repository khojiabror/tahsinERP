-- =====================================================================
-- DataFix MES-01 (TOPUVCHI, faqat SELECT) — audit MES-B01.
-- Eski kod kunlik kiritishda O'CHIRILGAN (IsDeleted = 1) ishlab chiqarish yozuvini ham "mavjud" deb olib,
-- unga kirim/xomashyo chiqimini qayta yozishi mumkin edi. Natija: ko'rinmaydigan (o'chirilgan) production'ga
-- bog'langan, o'chirilmagan kirim/chiqim hujjatlari — omborda izohsiz qoldiq.
-- Marker: Description'da "ProductionID:{id}" (keyin raqam kelmaydi).
-- Ma'lumot o'zgarmaydi.
-- =====================================================================
SET NOCOUNT ON;

;WITH DeletedProd AS (
    SELECT p.ID, p.ProductID, p.ShopID, p.[Date], p.Total
    FROM dbo.PRODUCT_PRODUCTION p
    WHERE p.IsDeleted = 1
)
SELECT 'INCOME' AS DocType, d.ID AS ProductionID, d.ProductID, d.[Date], d.Total,
       i.ID AS DocID, i.DocNo, i.IssueDateTime, i.Description
FROM DeletedProd d
JOIN dbo.INCOMES i
  ON ISNULL(i.IsDeleted, 0) = 0
 AND (i.Description LIKE '%ProductionID:' + CAST(d.ID AS varchar(20)) + '[^0-9]%'
      OR i.Description LIKE '%ProductionID:' + CAST(d.ID AS varchar(20)))
UNION ALL
SELECT 'EXPENSE', d.ID, d.ProductID, d.[Date], d.Total,
       e.ID, e.DocNo, e.IssueDateTime, e.Description
FROM DeletedProd d
JOIN dbo.EXPENSES e
  ON ISNULL(e.IsDeleted, 0) = 0
 AND e.ReceiverWhID IS NULL
 AND (e.Description LIKE '%ProductionID:' + CAST(d.ID AS varchar(20)) + '[^0-9]%'
      OR e.Description LIKE '%ProductionID:' + CAST(d.ID AS varchar(20)))
ORDER BY ProductionID, DocType;

-- Qisqa xulosa: har o'chirilgan production uchun nechta jonli hujjat
SELECT p.ID AS ProductionID, p.ProductID, pr.PNo, p.[Date], p.Total,
       (SELECT COUNT(*) FROM dbo.INCOMES i WHERE ISNULL(i.IsDeleted,0) = 0
            AND (i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)))) AS LiveIncomes,
       (SELECT COUNT(*) FROM dbo.EXPENSES e WHERE ISNULL(e.IsDeleted,0) = 0 AND e.ReceiverWhID IS NULL
            AND (e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)))) AS LiveExpenses
FROM dbo.PRODUCT_PRODUCTION p
JOIN dbo.PRODUCTS pr ON pr.ID = p.ProductID
WHERE p.IsDeleted = 1
  AND (EXISTS (SELECT 1 FROM dbo.INCOMES i WHERE ISNULL(i.IsDeleted,0) = 0
            AND (i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20))))
    OR EXISTS (SELECT 1 FROM dbo.EXPENSES e WHERE ISNULL(e.IsDeleted,0) = 0 AND e.ReceiverWhID IS NULL
            AND (e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)))))
ORDER BY p.[Date];
