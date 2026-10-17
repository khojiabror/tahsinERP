-- HRM-B03: ikki marta hisoblangan xodim qatorlarini tuzatish.
-- AVVAL DataFix_HRM_B03_PayrollDoublePay_Find.sql (1-so'rov) natijasini ko'rib chiqing va zaxira nusxa oling.
--
-- Qoida (faqat QORALAMA hisoblarga tegadi): bir xodim bir oy/turda bir nechta faol hisobda bo'lsa,
-- tasdiqlangan/o'tkazilgan hisobdagi qator saqlanadi; ular bo'lmasa — eng birinchi (kichik ID) hisobdagi.
-- Qolgan QORALAMA hisoblardagi qatorlar soft-delete qilinadi. Tasdiqlangan/o'tkazilgan ikkala hisobda ham
-- bo'lgan holatlar avtomatik tuzatilmaydi — oxirgi SELECT ularni ko'rsatadi (qo'lda hal qilish kerak).

SET XACT_ABORT ON;
BEGIN TRANSACTION;

;WITH dup AS (
    SELECT  l.ID AS LineID, h.ID AS PayrollID, h.Status, l.EmployeeID, h.[Year], h.[Month], h.PayrollType,
            ROW_NUMBER() OVER (PARTITION BY h.[Year], h.[Month], h.PayrollType, l.EmployeeID
                               ORDER BY CASE WHEN h.Status IN ('Approved', 'Posted') THEN 0 ELSE 1 END, h.ID) AS rn
    FROM dbo.HR_PAYROLL_LINES l
    JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
    WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(h.IsDeleted, 0) = 0
)
UPDATE l
SET    l.IsDeleted = 1
OUTPUT inserted.ID AS SoftDeletedLineID, inserted.PayrollID, inserted.EmployeeID, inserted.NetSalary
FROM dbo.HR_PAYROLL_LINES l
JOIN dup d ON d.LineID = l.ID
WHERE d.rn > 1 AND d.Status = 'Draft';

-- Qo'lda hal qilinadigan holatlar: bir xodim bir nechta tasdiqlangan/o'tkazilgan hisobda
SELECT  h.[Year], h.[Month], h.PayrollType, l.EmployeeID, COUNT(*) AS ApprovedOrPostedCount
FROM dbo.HR_PAYROLL_LINES l
JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(h.IsDeleted, 0) = 0 AND h.Status IN ('Approved', 'Posted')
GROUP BY h.[Year], h.[Month], h.PayrollType, l.EmployeeID
HAVING COUNT(*) > 1;

-- Natija to'g'ri bo'lsa COMMIT qiling.
-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
