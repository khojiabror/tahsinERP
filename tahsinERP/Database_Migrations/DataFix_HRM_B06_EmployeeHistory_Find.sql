-- HRM-B06: xodim tarixidagi eski noto'g'ri ma'lumotni topish. Faqat o'qiydi.

-- 1) Transfer EmploymentDate'ni o'tkazish sanasiga almashtirgan xodimlar:
--    EmploymentDate = oxirgi Transfer qatori boshlanishi, lekin undan oldingi davr qatori bor.
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
SELECT  e.ID, e.TabNo, e.FullName,
        e.EmploymentDate          AS CurrentEmploymentDate,
        er.EarliestFrom           AS SuggestedEmploymentDate,
        lt.TransferFrom           AS LastTransferFrom
FROM dbo.HR_EMPLOYEES e
JOIN lastTransfer lt ON lt.EmployeeID = e.ID
JOIN earliest er ON er.EmployeeID = e.ID
WHERE CAST(e.EmploymentDate AS date) = CAST(lt.TransferFrom AS date)
  AND er.EarliestFrom < lt.TransferFrom
ORDER BY e.TabNo;

-- 2) Bo'shatilgan/nofaol xodimlarning yopilmagan (EffectiveDateTo IS NULL) tarix qatorlari —
--    shu sabab ular yangi tabellarga tushib qolardi.
SELECT  h.ID AS HistoryID, h.EmployeeID, e.TabNo, e.FullName, h.ActionType,
        h.EffectiveDateFrom, e.DismissalDate, e.IsActive
FROM dbo.HR_EMPLOYEE_HISTORY h
JOIN dbo.HR_EMPLOYEES e ON e.ID = h.EmployeeID
WHERE h.EffectiveDateTo IS NULL
  AND e.DismissalDate IS NOT NULL
ORDER BY e.TabNo, h.EffectiveDateFrom;

-- 3) Dismiss yaratgan ustma-ust "History" qatorlari (EmploymentDate..DismissalDate) — staj ikki marta hisoblanishi mumkin
SELECT  h.ID AS HistoryID, h.EmployeeID, e.TabNo, e.FullName, h.EffectiveDateFrom, h.EffectiveDateTo,
        (SELECT COUNT(*) FROM dbo.HR_EMPLOYEE_HISTORY o
          WHERE o.EmployeeID = h.EmployeeID AND o.ID <> h.ID
            AND o.EffectiveDateFrom <= ISNULL(h.EffectiveDateTo, '9999-12-31')
            AND ISNULL(o.EffectiveDateTo, '9999-12-31') >= h.EffectiveDateFrom) AS OverlappingRows
FROM dbo.HR_EMPLOYEE_HISTORY h
JOIN dbo.HR_EMPLOYEES e ON e.ID = h.EmployeeID
WHERE h.ActionType = 'History'
ORDER BY e.TabNo;

-- 4) Bir xodimda bir nechta ochiq qator
SELECT EmployeeID, COUNT(*) AS OpenRows
FROM dbo.HR_EMPLOYEE_HISTORY
WHERE EffectiveDateTo IS NULL
GROUP BY EmployeeID
HAVING COUNT(*) > 1;
