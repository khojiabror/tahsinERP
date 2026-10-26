/* =====================================================================
   tahsinERP — DEMO: Kadrlar (HR) — tuzilma, 9 xodim, o'tgan oy tabeli va oyligi (240_Finance_Ledger.sql dan keyin).

   Bo'linmalar, lavozimlar, razryadlar, shtat jadvali, xodimlar; o'tgan oy tabeli (tasdiqlangan) va soatbay oylik
   (Posted, daromad solig'i 12%). Moliya tanlangan bo'lsa — oylik to'lovi FIN_TRANSACTIONS 'HR_PAYROLL'
   (HrPayrollController.PostToFinance).
   Faqat Hr moduli tanlangan bo'lsa. Idempotent: HR_EMPLOYEES bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Hr') = 0 OR EXISTS (SELECT 1 FROM dbo.HR_EMPLOYEES) OR NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo HR: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @kassa int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9000');
DECLARE @catPayroll int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'PAYROLL');

/* ---------------- 1. tuzilma ---------------- */
DECLARE @hrFrom date = DATEADD(day, -120, @t);
INSERT INTO dbo.HR_ORG_UNITS (ParentID, Code, Name, LevelNo, SortOrder, IsActive, CreatedDT, IsDeleted)
VALUES (NULL, N'KORX', N'"Demo Plast" MChJ', 1, 1, 1, @hrFrom, 0);
DECLARE @root int = SCOPE_IDENTITY();
INSERT INTO dbo.HR_ORG_UNITS (ParentID, Code, Name, LevelNo, SortOrder, IsActive, CreatedDT, IsDeleted)
VALUES (@root, N'RAH', N'Rahbariyat', 2, 1, 1, @hrFrom, 0),
       (@root, N'BUX', N'Buxgalteriya', 2, 2, 1, @hrFrom, 0),
       (@root, N'ISH', N'Ishlab chiqarish sexi', 2, 3, 1, @hrFrom, 0),
       (@root, N'OMB', N'Ombor xo''jaligi', 2, 4, 1, @hrFrom, 0),
       (@root, N'SAV', N'Savdo bo''limi', 2, 5, 1, @hrFrom, 0);

INSERT INTO dbo.HR_POSITIONS (Code, Name, IsManager, IsActive, CreatedDT, IsDeleted)
VALUES (N'DIR', N'Direktor', 1, 1, @hrFrom, 0),
       (N'BHS', N'Bosh hisobchi', 1, 1, @hrFrom, 0),
       (N'SXB', N'Sex boshlig''i', 1, 1, @hrFrom, 0),
       (N'OPR', N'Quyish mashinasi operatori', 0, 1, @hrFrom, 0),
       (N'SFN', N'Sifat nazoratchisi', 0, 1, @hrFrom, 0),
       (N'OMC', N'Omborchi', 0, 1, @hrFrom, 0),
       (N'SVM', N'Savdo menejeri', 0, 1, @hrFrom, 0),
       (N'HAY', N'Haydovchi-ekspeditor', 0, 1, @hrFrom, 0);

INSERT INTO dbo.HR_SALARY_GRADES (GradeCode, Name, MinSalary, MaxSalary, CurrencyCode, EffectiveFrom, EffectiveTo, IsDeleted)
VALUES (N'R1', N'Rahbar', 10000000, 18000000, 'UZS', @hrFrom, NULL, 0),
       (N'M1', N'Mutaxassis', 6500000, 10000000, 'UZS', @hrFrom, NULL, 0),
       (N'I1', N'Ishchi', 4500000, 7000000, 'UZS', @hrFrom, NULL, 0);

CREATE TABLE #emp (TabNo nvarchar(50) COLLATE DATABASE_DEFAULT, FullName nvarchar(200) COLLATE DATABASE_DEFAULT, Org nvarchar(50) COLLATE DATABASE_DEFAULT, Pos nvarchar(50) COLLATE DATABASE_DEFAULT, Grade nvarchar(50) COLLATE DATABASE_DEFAULT, Salary decimal(18,2),
                   Hired date, MatPct decimal(5,2), SpecPct decimal(5,2), AbsentDays int, OtDays int, ID int NULL);
INSERT INTO #emp (TabNo, FullName, Org, Pos, Grade, Salary, Hired, MatPct, SpecPct, AbsentDays, OtDays) VALUES
(N'0001', N'Karimov Aziz Bahodirovich',   N'RAH', N'DIR', N'R1', 16000000, DATEADD(day, -110, @t), 0,  0, 0, 0),
(N'0002', N'Sobirova Gulnora Akmalovna',  N'BUX', N'BHS', N'R1', 11000000, DATEADD(day, -110, @t), 0, 10, 0, 0),
(N'0003', N'Toshmatov Jasur Rustamovich', N'ISH', N'SXB', N'R1', 10000000, DATEADD(day, -105, @t), 10, 0, 0, 3),
(N'0004', N'Ergashev Sanjar Olimovich',   N'ISH', N'OPR', N'I1',  6500000, DATEADD(day, -100, @t), 0,  0, 0, 5),
(N'0005', N'Xolmatov Bekzod Anvarovich',  N'ISH', N'OPR', N'I1',  6500000, DATEADD(day, -100, @t), 0,  0, 2, 0),
(N'0006', N'Yo''ldosheva Madina Karimovna', N'ISH', N'SFN', N'M1', 6800000, DATEADD(day, -95, @t), 0,  0, 0, 0),
(N'0007', N'Rasulov Otabek Shuhratovich', N'OMB', N'OMC', N'I1',  5800000, DATEADD(day, -100, @t), 15, 0, 1, 0),
(N'0008', N'Nazarova Kamola Baxtiyorovna', N'SAV', N'SVM', N'M1', 8000000, DATEADD(day, -90, @t), 0,  0, 0, 0),
(N'0009', N'Qosimov Ilyos Tohirovich',    N'SAV', N'HAY', N'I1',  5500000, DATEADD(day, -90, @t), 0,  0, 0, 2);

INSERT INTO dbo.HR_STAFFING_PLAN (OrgUnitID, PositionID, SalaryGradeID, PlannedHeadcount, IsPieceRate, IsHourly, IsFixedSalary, EffectiveFrom, EffectiveTo, IsActive, IsDeleted)
SELECT o.ID, p.ID, g.ID, x.Planned, 0, 1, 0, @hrFrom, NULL, 1, 0
FROM (SELECT Org, Pos, Grade, COUNT(*) + CASE WHEN Pos IN (N'OPR', N'SVM') THEN 1 ELSE 0 END AS Planned FROM #emp GROUP BY Org, Pos, Grade) x
JOIN dbo.HR_ORG_UNITS o ON o.Code = x.Org JOIN dbo.HR_POSITIONS p ON p.Code = x.Pos JOIN dbo.HR_SALARY_GRADES g ON g.GradeCode = x.Grade;

INSERT INTO dbo.HR_EMPLOYEES (TabNo, FullName, OrgUnitID, PositionID, BaseSalary, SalaryGradeID, EmploymentDate, DismissalDate, IsActive, CreatedByUserID, CreatedDT, UpdatedDT, IsDeleted)
SELECT e.TabNo, e.FullName, o.ID, p.ID, e.Salary, g.ID, e.Hired, NULL, 1, @u, CAST(e.Hired AS datetime2), NULL, 0
FROM #emp e JOIN dbo.HR_ORG_UNITS o ON o.Code = e.Org JOIN dbo.HR_POSITIONS p ON p.Code = e.Pos JOIN dbo.HR_SALARY_GRADES g ON g.GradeCode = e.Grade
ORDER BY e.TabNo;
UPDATE e SET ID = h.ID FROM #emp e JOIN dbo.HR_EMPLOYEES h ON h.TabNo = e.TabNo AND h.IsActive = 1;

INSERT INTO dbo.HR_EMPLOYEE_ALLOWANCES (EmployeeID, MaterialResponsibilityPercent, SpecialAllowancePercent, CreatedDT, UpdatedDT)
SELECT ID, MatPct, SpecPct, CAST(Hired AS datetime), CAST(Hired AS datetime) FROM #emp;
INSERT INTO dbo.HR_EMPLOYEE_HISTORY (EmployeeID, ActionType, FromOrgUnitID, FromPositionID, ToOrgUnitID, ToPositionID, Reason, CreatedByUserID, CreatedDT, EffectiveDateFrom, EffectiveDateTo)
SELECT h.ID, 'Initial Employment', h.OrgUnitID, h.PositionID, NULL, NULL, N'Ishga qabul qilingan', @u, CAST(h.EmploymentDate AS datetime), h.EmploymentDate, NULL
FROM dbo.HR_EMPLOYEES h WHERE h.ID IN (SELECT ID FROM #emp);

/* ---------------- 2. o'tgan oy tabeli (korxona bo'yicha, OrgUnitID NULL), Du–Ju 8 soat ---------------- */
DECLARE @pm date = DATEADD(month, -1, DATEFROMPARTS(YEAR(@t), MONTH(@t), 1));      -- o'tgan oyning 1-kuni
DECLARE @pmEnd date = EOMONTH(@pm);
DECLARE @postAt datetime = DATEADD(hour, 10, CAST(DATEFROMPARTS(YEAR(@t), MONTH(@t), 1) AS datetime));
CREATE TABLE #days (D date PRIMARY KEY, DayNo int, WorkNo int NULL);
;WITH n AS (SELECT TOP (31) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects)
INSERT INTO #days (D, DayNo) SELECT DATEADD(day, k, @pm), k + 1 FROM n WHERE DATEADD(day, k, @pm) <= @pmEnd;
;WITH w AS (SELECT D, ROW_NUMBER() OVER (ORDER BY D) AS N FROM #days WHERE DATEDIFF(day, '19000101', D) % 7 < 5)
UPDATE d SET WorkNo = w.N FROM #days d JOIN w ON w.D = d.D;
DECLARE @fund decimal(10,2) = 8 * (SELECT COUNT(*) FROM #days WHERE WorkNo IS NOT NULL);

INSERT INTO dbo.HR_TIMESHEET_HEADERS (Year, Month, OrgUnitID, Status, CreatedByUserID, CreatedDT, ApprovedByUserID, ApprovedDT, IsDeleted)
VALUES (YEAR(@pm), MONTH(@pm), NULL, 'Approved', @u, CAST(@pmEnd AS datetime2), @u, DATEADD(hour, -1, @postAt), 0);
DECLARE @ts int = SCOPE_IDENTITY();
INSERT INTO dbo.HR_TIMESHEET_SETTINGS (TimesheetID, FundHours, CreatedDT, UpdatedDT) VALUES (@ts, @fund, CAST(@pmEnd AS datetime), CAST(@pmEnd AS datetime));

-- har xodim/kun soati: ish kunida 8 (birinchi AbsentDays ish kuni — 0, birinchi OtDays ish kuni — 10)
CREATE TABLE #h (EmpID int, DayNo int, Hours decimal(5,2), IsWork bit);
INSERT INTO #h
SELECT e.ID, d.DayNo,
       CASE WHEN d.WorkNo IS NULL THEN 0
            WHEN d.WorkNo BETWEEN 3 AND 2 + e.AbsentDays THEN 0
            WHEN d.WorkNo BETWEEN 6 AND 5 + e.OtDays THEN 10
            ELSE 8 END,
       CASE WHEN d.WorkNo IS NULL THEN 0 ELSE 1 END
FROM #emp e CROSS JOIN #days d;

INSERT INTO dbo.HR_TIMESHEET_LINES (TimesheetID, EmployeeID, TotalWorkedDays, TotalWorkedHours, OvertimeHours, AbsentDays, Notes, ProductID, TotalQuantity, IsDeleted)
SELECT @ts, EmpID, SUM(CASE WHEN Hours > 0 THEN 1 ELSE 0 END), SUM(Hours),
       SUM(CASE WHEN IsWork = 1 AND Hours > 8 THEN Hours - 8 WHEN IsWork = 0 THEN Hours ELSE 0 END),
       SUM(CASE WHEN IsWork = 1 AND Hours = 0 THEN 1 ELSE 0 END), NULL, NULL, 0, 0
FROM #h GROUP BY EmpID;

DECLARE @dn int = 1, @sql nvarchar(max);
WHILE @dn <= 31
BEGIN
    SET @sql = N'UPDATE l SET Day' + RIGHT('0' + CAST(@dn AS nvarchar(2)), 2) + N'Hours = h.Hours
FROM dbo.HR_TIMESHEET_LINES l JOIN #h h ON h.EmpID = l.EmployeeID AND h.DayNo = @d WHERE l.TimesheetID = @ts;';
    EXEC sp_executesql @sql, N'@d int, @ts int', @d = @dn, @ts = @ts;
    SET @dn += 1;
END

/* ---------------- 3. soatbay oylik (HrPayrollCalculator), tasdiqlangan = Posted (+ moliyaga to'lov) ---------------- */
INSERT INTO dbo.HR_PAYROLL_HEADERS (Year, Month, OrgUnitID, Status, CreatedByUserID, CreatedDT, ApprovedByUserID, ApprovedDT, TimesheetID, PayrollType, IsDeleted)
VALUES (YEAR(@pm), MONTH(@pm), NULL, 'Posted', @u, DATEADD(minute, -30, @postAt), @u, @postAt, @ts, 'Hourly', 0);
DECLARE @pr int = SCOPE_IDENTITY();

INSERT INTO dbo.HR_PAYROLL_LINES (PayrollID, EmployeeID, BaseSalary, HoursWorked, PieceQty, HourlyRate, GrossSalary, Bonuses, Penalties, NetSalary, CurrencyID, Notes, IsDeleted)
SELECT @pr, e.ID, e.Salary, l.TotalWorkedHours, 0, ROUND(r.Rate, 2), g.Gross, 0, 0, g.Gross - ROUND(g.Gross * 12 / 100, 2), @uzs,
       CASE WHEN e.MatPct + e.SpecPct > 0 THEN N'Ustama: ' + CAST(CAST(e.MatPct + e.SpecPct AS int) AS nvarchar(10)) + N'% (moddiy+maxsus)' END, 0
FROM #emp e JOIN dbo.HR_TIMESHEET_LINES l ON l.TimesheetID = @ts AND l.EmployeeID = e.ID
CROSS APPLY (SELECT (e.Salary + e.Salary * (e.MatPct + e.SpecPct) / 100) / @fund AS Rate) r
CROSS APPLY (SELECT ROUND((l.TotalWorkedHours - l.OvertimeHours) * r.Rate + l.OvertimeHours * r.Rate * 1.5, 2) AS Gross) g
WHERE l.TotalWorkedHours > 0;

IF dbo.fn_SetupHasModule('Finance') = 1 AND @kassa IS NOT NULL AND @catPayroll IS NOT NULL
    INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                      CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
    SELECT CAST(@postAt AS date), @kassa, @catPayroll, 2, SUM(NetSalary), @uzs, NULL, NULL, NULL, NULL, N'Xodimlar',
           N'Ish haqi hisobi ' + CAST(YEAR(@pm) AS nvarchar(4)) + N'-' + RIGHT('0' + CAST(MONTH(@pm) AS nvarchar(2)), 2) + N' (Soatbay)',
           'HR_PAYROLL', @pr, NULL, N'Ish haqi hisobi tasdiqlandi, ID: ' + CAST(@pr AS nvarchar(12)), @u, @postAt, 0
    FROM dbo.HR_PAYROLL_LINES WHERE PayrollID = @pr
    HAVING SUM(NetSalary) > 0;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #emp, #days, #h;

PRINT 'Demo HR: OK';
