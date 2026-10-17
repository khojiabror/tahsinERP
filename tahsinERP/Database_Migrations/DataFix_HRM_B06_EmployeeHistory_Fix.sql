-- HRM-B06: xodim tarixini tuzatish. AVVAL DataFix_HRM_B06_EmployeeHistory_Find.sql natijasini ko'rib chiqing va zaxira oling.
--  1) EmploymentDate'ni eng birinchi davr boshlanishiga qaytarish (faqat Transfer almashtirgan holatlar);
--  2) bo'shatilgan xodimlarning ochiq tarix qatorlarini DismissalDate bilan yopish.
-- 3-so'rovdagi ustma-ust "History" qatorlari avtomatik o'chirilmaydi (staj ma'lumoti) — qo'lda ko'rib chiqing.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

;WITH lastTransfer AS (
    SELECT EmployeeID, MAX(EffectiveDateFrom) AS TransferFrom
    FROM dbo.HR_EMPLOYEE_HISTORY
    WHERE ActionType = 'Transfer'
    GROUP BY EmployeeID
), earliest AS (
    SELECT EmployeeID, MIN(EffectiveDateFrom) AS EarliestFrom
    FROM dbo.HR_EMPLOYEE_HISTORY
    WHERE ActionType IN ('Initial Employment', 'Transfer', 'History')
    GROUP BY EmployeeID
)
UPDATE e
SET    e.EmploymentDate = er.EarliestFrom,
       e.UpdatedDT = GETDATE()
OUTPUT inserted.ID AS EmployeeID, deleted.EmploymentDate AS OldEmploymentDate, inserted.EmploymentDate AS NewEmploymentDate
FROM dbo.HR_EMPLOYEES e
JOIN lastTransfer lt ON lt.EmployeeID = e.ID
JOIN earliest er ON er.EmployeeID = e.ID
WHERE CAST(e.EmploymentDate AS date) = CAST(lt.TransferFrom AS date)
  AND er.EarliestFrom < lt.TransferFrom;

UPDATE h
SET    h.EffectiveDateTo = CASE WHEN CAST(e.DismissalDate AS date) >= CAST(h.EffectiveDateFrom AS date)
                                THEN CAST(e.DismissalDate AS date) ELSE CAST(h.EffectiveDateFrom AS date) END
OUTPUT inserted.ID AS HistoryID, inserted.EmployeeID, inserted.EffectiveDateFrom, inserted.EffectiveDateTo
FROM dbo.HR_EMPLOYEE_HISTORY h
JOIN dbo.HR_EMPLOYEES e ON e.ID = h.EmployeeID
WHERE h.EffectiveDateTo IS NULL
  AND e.DismissalDate IS NOT NULL;

-- Natija to'g'ri bo'lsa COMMIT qiling.
-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
