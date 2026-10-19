-- Demo tenant ma'lumotlari (New-TenantDb.ps1 -Demo). Faqat DB_DEMO_TEMPLATE shabloni uchun: haqiqiy korxona bazasiga QO'LLANMAYDI.
-- Idempotent; spravochnik qatorlari faqat SystemCode bo'yicha topiladi (Database/README.md).
-- Kechasi 03:00 da demo tenant shu shablondan qayta tiklanadi (05_PROVISIONING.md, 6-bo'lim).

UPDATE dbo.COMPANIES SET Name = N'"Demo Ishlab chiqarish" MChJ' WHERE ID = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SUPPLIERS WHERE Name = 'Demo Metall Savdo')
    INSERT INTO dbo.SUPPLIERS (Name, Country, City, Telephone, Email, ContactPersonName, IsDeleted, Type)
    VALUES ('Demo Metall Savdo', 'Uzbekistan', 'Toshkent', '+998 71 200 00 01', 'metall@demo.uz', 'Aliyev A.', 0, 'Local'),
           ('Demo Plastik Group', 'Uzbekistan', 'Samarqand', '+998 66 200 00 02', 'plastik@demo.uz', 'Karimova D.', 0, 'Local'),
           ('Demo Import Co', 'China', 'Guangzhou', '+86 20 0000 0003', 'import@demo.cn', 'Li Wei', 0, 'Foreign');

IF NOT EXISTS (SELECT 1 FROM dbo.CUSTOMERS WHERE Name = 'Demo Qurilish MChJ')
    INSERT INTO dbo.CUSTOMERS (Name, Country, City, Telephone, Email, ContactPersonName, IsDeleted, Type)
    VALUES ('Demo Qurilish MChJ', 'Uzbekistan', 'Toshkent', '+998 71 300 00 01', 'qurilish@demo.uz', 'Rahimov B.', 0, 'Local'),
           ('Demo Avto Servis', 'Uzbekistan', 'Andijon', '+998 74 300 00 02', 'avto@demo.uz', 'Yusupov S.', 0, 'Local'),
           ('Demo Market', 'Uzbekistan', 'Buxoro', '+998 65 300 00 03', 'market@demo.uz', 'Nazarova M.', 0, 'Local');

DECLARE @pcs int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'PCS');
DECLARE @kg int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'KG');
DECLARE @shop int = (SELECT TOP 1 ID FROM dbo.SHOPS WHERE ISNULL(IsDeleted, 0) = 0 ORDER BY ID);
IF @pcs IS NULL OR @kg IS NULL OR @shop IS NULL
    THROW 50001, 'Demo: UNITS (PCS/KG) yoki SHOPS seed''da topilmadi', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.PRODUCTS WHERE PNo = 'DEMO-RM-001')
    INSERT INTO dbo.PRODUCTS (PNo, Name, Type, IsDeleted, UnitID, IsRawMaterial, IsComponent, IsFinishedGood, ShopID, Weight)
    VALUES ('DEMO-RM-001', 'Po''lat list 2mm', 'Raw', 0, @kg, 1, 0, 0, @shop, 15.7),
           ('DEMO-RM-002', 'Polipropilen granula', 'Raw', 0, @kg, 1, 0, 0, @shop, 1),
           ('DEMO-CM-001', 'Bolt M8x30', 'Component', 0, @pcs, 0, 1, 0, @shop, 0.02),
           ('DEMO-CM-002', 'Plastik qopqoq', 'Component', 0, @pcs, 0, 1, 0, @shop, 0.05),
           ('DEMO-FG-001', 'Metall shkaf ShM-1', 'Finished', 0, @pcs, 0, 0, 1, @shop, 32),
           ('DEMO-FG-002', 'Instrument quti IQ-2', 'Finished', 0, @pcs, 0, 0, 1, @shop, 4.5);

IF NOT EXISTS (SELECT 1 FROM dbo.APP_TASK_PROJECTS WHERE Code = 'DEMO')
    INSERT INTO dbo.APP_TASK_PROJECTS (Code, Name, IsActive, CreatedDT, IsDeleted)
    VALUES ('DEMO', N'Demo loyiha: yangi mahsulotni ishlab chiqarishga joriy etish', 1, SYSUTCDATETIME(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.HR_EMPLOYEES WHERE TabNo = 'D-001')
    INSERT INTO dbo.HR_EMPLOYEES (TabNo, FullName, BaseSalary, EmploymentDate, IsActive, CreatedDT, IsDeleted)
    VALUES ('D-001', N'Toshmatov Jasur', 6000000, '2025-01-15', 1, SYSUTCDATETIME(), 0),
           ('D-002', N'Ergasheva Nilufar', 5500000, '2025-03-01', 1, SYSUTCDATETIME(), 0),
           ('D-003', N'Qodirov Sardor', 4800000, '2025-06-10', 1, SYSUTCDATETIME(), 0);
