-- HRM-B01: Ish haqi Finance'ga noto'g'ri yo'nalish bilan yozilgan tranzaksiyalarni topish.
-- Tizimda FIN_TRANSACTIONS.Direction: 1 = kirim, 2 = chiqim.
-- HrPayrollController.Post avval direction = 1 (kirim) yuborardi; ish haqi chiqim (2) bo'lishi kerak.
-- Faqat o'qiydi. Natijani ko'rib chiqing, keyin DataFix_HRM_B01_PayrollDirection_Fix.sql ni ishga tushiring.

SELECT  t.ID,
        t.TxnDate,
        t.AccountID,
        t.CategoryID,
        t.Direction,
        t.Amount,
        t.CurrencyID,
        t.SourceDocType,
        t.SourceDocID,
        h.[Year],
        h.[Month],
        h.OrgUnitID,
        h.Status        AS PayrollStatus,
        t.Purpose,
        t.CreatedDT,
        t.IsDeleted
FROM dbo.FIN_TRANSACTIONS t
LEFT JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = t.SourceDocID
WHERE t.SourceDocType = 'HR_PAYROLL'
  AND t.Direction = 1
ORDER BY t.TxnDate, t.ID;

-- Hisoblar bo'yicha ta'sir (tuzatilgandan keyin har hisob qoldig'i 2 * Amount ga kamayadi)
SELECT  t.AccountID,
        t.CurrencyID,
        COUNT(*)            AS TxnCount,
        SUM(t.Amount)       AS WrongInflowTotal,
        SUM(t.Amount) * 2   AS BalanceShiftAfterFix
FROM dbo.FIN_TRANSACTIONS t
WHERE t.SourceDocType = 'HR_PAYROLL'
  AND t.Direction = 1
  AND (t.IsDeleted IS NULL OR t.IsDeleted = 0)
GROUP BY t.AccountID, t.CurrencyID
ORDER BY t.AccountID, t.CurrencyID;
