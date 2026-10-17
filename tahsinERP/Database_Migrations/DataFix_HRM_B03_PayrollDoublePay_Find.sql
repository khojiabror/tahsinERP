-- HRM-B03 / HRM-B04 / HRM-B02: ish haqi hisoblaridagi eski noto'g'ri ma'lumotni topish. Faqat o'qiydi.

-- 1) Bir xodim bir oy va bir turda (Hourly / PieceRate) bir nechta faol hisobda (ikki marta to'lash xavfi, HRM-B03)
SELECT  h.[Year], h.[Month], h.PayrollType, l.EmployeeID, e.TabNo, e.FullName,
        COUNT(DISTINCT h.ID)                         AS PayrollCount,
        MIN(h.ID) AS FirstPayrollID, MAX(h.ID) AS LastPayrollID,
        SUM(CASE WHEN h.Status IN ('Approved', 'Posted') THEN 1 ELSE 0 END) AS ApprovedOrPostedLines,
        SUM(l.NetSalary)                             AS TotalNet
FROM dbo.HR_PAYROLL_LINES l
JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
JOIN dbo.HR_EMPLOYEES e ON e.ID = l.EmployeeID
WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(h.IsDeleted, 0) = 0
GROUP BY h.[Year], h.[Month], h.PayrollType, l.EmployeeID, e.TabNo, e.FullName
HAVING COUNT(DISTINCT h.ID) > 1
ORDER BY h.[Year], h.[Month], h.PayrollType, e.TabNo;

-- 2) Soatbay hisoblar ichida soliq foizi har xil qatorlar (HRM-B04: foiz o'zgarganda faqat birinchi qator qayta hisoblanardi)
;WITH x AS (
    SELECT  l.PayrollID, l.ID AS LineID,
            CAST(ROUND((ISNULL(l.GrossSalary,0) + ISNULL(l.Bonuses,0) - ISNULL(l.Penalties,0) - ISNULL(l.NetSalary,0))
                       / NULLIF(l.GrossSalary, 0) * 100, 2) AS DECIMAL(9,2)) AS TaxPercent
    FROM dbo.HR_PAYROLL_LINES l
    JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
    WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(h.IsDeleted, 0) = 0
      AND h.PayrollType = 'Hourly' AND ISNULL(l.GrossSalary, 0) > 0
)
SELECT  h.ID AS PayrollID, h.[Year], h.[Month], h.OrgUnitID, h.Status,
        MIN(x.TaxPercent) AS MinTaxPercent, MAX(x.TaxPercent) AS MaxTaxPercent, COUNT(*) AS LineCount
FROM x JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = x.PayrollID
GROUP BY h.ID, h.[Year], h.[Month], h.OrgUnitID, h.Status
HAVING MAX(x.TaxPercent) - MIN(x.TaxPercent) > 0.05
ORDER BY h.[Year], h.[Month], h.ID;

-- 3) Ishbay hisobga tasdiqlanmagan bo'lim yozuvlari kirgan bo'lishi mumkin bo'lgan hisoblar (HRM-B02)
SELECT  h.ID AS PayrollID, h.[Year], h.[Month], h.OrgUnitID AS PayrollOrgUnitID, h.Status,
        e.OrgUnitID AS EmployeeOrgUnitID, COUNT(*) AS EntryCount, SUM(w.Quantity) AS Qty
FROM dbo.HR_PAYROLL_HEADERS h
JOIN dbo.HR_PAYROLL_LINES l ON l.PayrollID = h.ID AND ISNULL(l.IsDeleted, 0) = 0
JOIN dbo.HR_EMPLOYEES e ON e.ID = l.EmployeeID
JOIN dbo.HR_PIECE_RATE_WORK_ENTRIES w ON w.EmployeeID = l.EmployeeID AND ISNULL(w.IsDeleted, 0) = 0
     AND YEAR(w.WorkDate) = h.[Year] AND MONTH(w.WorkDate) = h.[Month]
WHERE h.PayrollType = 'PieceRate' AND ISNULL(h.IsDeleted, 0) = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.HR_PIECE_RATE_TIMESHEET_APPROVALS a
                  WHERE a.[Year] = h.[Year] AND a.[Month] = h.[Month] AND a.Status = 'Approved' AND ISNULL(a.IsDeleted, 0) = 0
                    AND (a.OrgUnitID IS NULL OR a.OrgUnitID = e.OrgUnitID))
GROUP BY h.ID, h.[Year], h.[Month], h.OrgUnitID, h.Status, e.OrgUnitID
ORDER BY h.[Year], h.[Month], h.ID;
