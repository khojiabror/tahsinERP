/* =====================================================================
   Topshiriq status/prioritet ranglari va "yopilgan" belgisi bazada (audit MNT-01, TSK-B09).
   Avval rang va "bajarildi" holati nomdan aniqlanardi (Name LIKE '%bajarildi%', '%muhim%') — nom
   o'zgarsa yoki boshqa tilda bo'lsa noto'g'ri ishlardi.
   - APP_TASK_STATUS_REF.IsFinal  — yopilgan holat (bajarilgan yoki bekor): muddati o'tgan hisoblanmaydi,
                                    Kanban'da 30 kundan keyin yashiriladi.
   - APP_TASK_STATUS_REF.ColorHex, APP_TASK_PRIORITY_REF.ColorHex — badge/grafik rangi (#RRGGBB, NULL — standart).
   Boshlang'ich qiymatlar avvalgi kod qoidalari bilan bir xil. Idempotent. EDMX ham yangilangan.
   2026-10-18: lokal DBTHSN-IPACK bazasida ishga tushirildi (zaxira:
   ...\MSSQL\Backup\DBTHSN-IPACK_before_task_refs_2026-10-18.bak).
   ===================================================================== */
IF COL_LENGTH('dbo.APP_TASK_STATUS_REF', 'IsFinal') IS NULL
    ALTER TABLE dbo.APP_TASK_STATUS_REF ADD IsFinal BIT NOT NULL CONSTRAINT DF_APP_TASK_STATUS_REF_IsFinal DEFAULT (0);
IF COL_LENGTH('dbo.APP_TASK_STATUS_REF', 'ColorHex') IS NULL
    ALTER TABLE dbo.APP_TASK_STATUS_REF ADD ColorHex NVARCHAR(7) NULL;
IF COL_LENGTH('dbo.APP_TASK_PRIORITY_REF', 'ColorHex') IS NULL
    ALTER TABLE dbo.APP_TASK_PRIORITY_REF ADD ColorHex NVARCHAR(7) NULL;
GO

-- Statuslar: avvalgi TaskBadges qoidalari bo'yicha
UPDATE dbo.APP_TASK_STATUS_REF SET IsFinal = 1, ColorHex = ISNULL(ColorHex, N'#28A745')
WHERE Code = 'DONE' OR Name LIKE N'%bajarildi%' OR Name LIKE N'%tugallangan%' OR Name LIKE N'%done%';

UPDATE dbo.APP_TASK_STATUS_REF SET IsFinal = 1, ColorHex = ISNULL(ColorHex, N'#DC3545')
WHERE Code = 'CANCELLED' OR Name LIKE N'%bekor%' OR Name LIKE N'%cancel%';

UPDATE dbo.APP_TASK_STATUS_REF SET ColorHex = N'#FFC107'
WHERE ColorHex IS NULL AND (Code = 'IN_PROGRESS' OR Name LIKE N'%jarayonda%');

UPDATE dbo.APP_TASK_STATUS_REF SET ColorHex = N'#6C757D'
WHERE ColorHex IS NULL AND (Code = 'TODO' OR Name LIKE N'%rejada%');

-- Prioritetlar
UPDATE dbo.APP_TASK_PRIORITY_REF SET ColorHex = N'#DC3545'
WHERE ColorHex IS NULL AND (Code = 'CRITICAL' OR Name LIKE N'%juda muhim%');

UPDATE dbo.APP_TASK_PRIORITY_REF SET ColorHex = N'#FF6666'
WHERE ColorHex IS NULL AND (Code = 'HIGH' OR Name LIKE N'%muhim%');

UPDATE dbo.APP_TASK_PRIORITY_REF SET ColorHex = N'#FFCCCC'
WHERE ColorHex IS NULL AND (Code IN ('NORMAL', 'MEDIUM') OR Name LIKE N'%normal%');
GO

SELECT ID, Code, Name, IsFinal, ColorHex FROM dbo.APP_TASK_STATUS_REF;
SELECT ID, Code, Name, ColorHex FROM dbo.APP_TASK_PRIORITY_REF;
