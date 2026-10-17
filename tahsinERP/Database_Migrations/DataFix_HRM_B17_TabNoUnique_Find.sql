-- HRM-B17 Find: HR_EMPLOYEES.TabNo takrorlari (unique index qo'shishdan oldin). Faqat o'qiydi.
-- Natija (lokal DBTHSN-IPACK, 2026-10-18): 5 juft takror, hammasi ishdan bo'shagan (IsActive=0) turli odamlar — raqam qayta berilgan.
-- Bazada IX_HR_EMPLOYEES_TabNo UNIQUE (filtr IsActive = 1) allaqachon bor, yangi indeks kerak emas; kod shu qoidaga moslandi.
SET NOCOUNT ON;

SELECT c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'HR_EMPLOYEES' AND c.COLUMN_NAME IN ('TabNo', 'IsDeleted');

-- 1) Barcha xodimlar (o'chirilganlar bilan) orasida takrorlar
SELECT 'ALL' AS Scope, LTRIM(RTRIM(TabNo)) AS TabNo, COUNT(*) AS Cnt,
       STRING_AGG(CAST(ID AS varchar(20)) + CASE WHEN IsDeleted = 1 THEN '(o''chirilgan)' ELSE '' END, ', ') AS IDs
FROM HR_EMPLOYEES
WHERE TabNo IS NOT NULL AND LTRIM(RTRIM(TabNo)) <> ''
GROUP BY LTRIM(RTRIM(TabNo))
HAVING COUNT(*) > 1;

-- 2) Faqat faol xodimlar orasida takrorlar
SELECT 'ACTIVE' AS Scope, LTRIM(RTRIM(TabNo)) AS TabNo, COUNT(*) AS Cnt
FROM HR_EMPLOYEES
WHERE TabNo IS NOT NULL AND LTRIM(RTRIM(TabNo)) <> '' AND ISNULL(IsDeleted, 0) = 0
GROUP BY LTRIM(RTRIM(TabNo))
HAVING COUNT(*) > 1;

-- 3) Bo'sh/NULL TabNo va bosh/oxiri bo'shliqli qiymatlar
SELECT SUM(CASE WHEN TabNo IS NULL THEN 1 ELSE 0 END) AS NullTabNo,
       SUM(CASE WHEN TabNo IS NOT NULL AND LTRIM(RTRIM(TabNo)) = '' THEN 1 ELSE 0 END) AS EmptyTabNo,
       SUM(CASE WHEN TabNo IS NOT NULL AND TabNo <> LTRIM(RTRIM(TabNo)) THEN 1 ELSE 0 END) AS PaddedTabNo,
       SUM(CASE WHEN IsDeleted IS NULL THEN 1 ELSE 0 END) AS NullIsDeleted,
       COUNT(*) AS Total
FROM HR_EMPLOYEES;

-- 4) Mavjud indekslar
SELECT i.name, i.is_unique, i.filter_definition
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('HR_EMPLOYEES') AND i.name IS NOT NULL;
