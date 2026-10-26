/* =====================================================================
   034 — Ish haqi: bir bo'limga bir oyda soatbay VA ishbay hisob (HR_PAYROLL_HEADERS).
   Unikal indeks faqat (Year, Month, OrgUnitID) bo'yicha edi — bir oyning ichida soatbay hisobi bor bo'limga
   ishbay hisob yaratilganda (yoki aksincha) INSERT 2601 bilan yiqilardi -> 500. Kod turlarni allaqachon
   ajratadi (HrPayrollController.FindPayrollHeadersAsync, PayrollType); indeks endi turni ham o'z ichiga oladi.
   Eski qatorlarda PayrollType NULL = soatbay (kod ham shunday ko'rsatadi) — 'Hourly' bilan to'ldiriladi,
   aks holda NULL va 'Hourly' ikkita soatbay hisobga yo'l qo'yardi.
   Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE dbo.HR_PAYROLL_HEADERS SET PayrollType = N'Hourly' WHERE PayrollType IS NULL;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HR_PAYROLL_HEADERS_YearMonthOrgUnit' AND object_id = OBJECT_ID('dbo.HR_PAYROLL_HEADERS'))
    DROP INDEX IX_HR_PAYROLL_HEADERS_YearMonthOrgUnit ON dbo.HR_PAYROLL_HEADERS;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HR_PAYROLL_HEADERS_YearMonthOrgUnitType' AND object_id = OBJECT_ID('dbo.HR_PAYROLL_HEADERS'))
    CREATE UNIQUE NONCLUSTERED INDEX IX_HR_PAYROLL_HEADERS_YearMonthOrgUnitType
        ON dbo.HR_PAYROLL_HEADERS ([Year] ASC, [Month] ASC, [OrgUnitID] ASC, [PayrollType] ASC)
        WHERE ([OrgUnitID] IS NOT NULL);
