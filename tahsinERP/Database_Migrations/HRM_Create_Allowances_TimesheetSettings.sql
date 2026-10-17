-- HRM-P13: HrEmployeeAllowanceService / HrTimesheetSettingsService endi jadvalni runtime'da yaratmaydi
-- (avval har o'qish/yozishda IF OBJECT_ID ... CREATE TABLE DDL yuborilardi).
-- Jadvallar EDMX'da bor va prod'da allaqachon mavjud bo'lishi kerak. Yangi muhit uchun shu skriptni bir marta ishga tushiring.

IF OBJECT_ID(N'dbo.HR_EMPLOYEE_ALLOWANCES', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HR_EMPLOYEE_ALLOWANCES
    (
        EmployeeID INT NOT NULL PRIMARY KEY,
        MaterialResponsibilityPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Material DEFAULT(0),
        SpecialAllowancePercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Special DEFAULT(0),
        CreatedDT DATETIME NOT NULL CONSTRAINT DF_HR_EMP_ALLOW_Created DEFAULT(GETDATE()),
        UpdatedDT DATETIME NULL,
        CONSTRAINT FK_HR_EMP_ALLOW_EMP FOREIGN KEY (EmployeeID) REFERENCES dbo.HR_EMPLOYEES(ID)
    );
END
GO

IF OBJECT_ID(N'dbo.HR_TIMESHEET_SETTINGS', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HR_TIMESHEET_SETTINGS
    (
        TimesheetID INT NOT NULL PRIMARY KEY,
        FundHours DECIMAL(10,2) NOT NULL CONSTRAINT DF_HR_TIMESHEET_SETTINGS_FundHours DEFAULT(160),
        CreatedDT DATETIME NOT NULL CONSTRAINT DF_HR_TIMESHEET_SETTINGS_Created DEFAULT(GETDATE()),
        UpdatedDT DATETIME NULL,
        CONSTRAINT FK_HR_TIMESHEET_SETTINGS_HEADER FOREIGN KEY (TimesheetID) REFERENCES dbo.HR_TIMESHEET_HEADERS(ID)
    );
END
GO
