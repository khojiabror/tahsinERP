/* =====================================================================
   tahsinERP — DEMO korxona: asosiy (Core) master ma'lumot va ssenariy (New-TenantDb.ps1 -Demo, seed'dan keyin).
   Uydirma "Demo Plast" MChJ — plastik idishlar ishlab chiqaruvchi.

   Demo modul bo'yicha bo'lingan (README "Demo ma'lumot"): har fayl dbo.fn_SetupHasModule('<Modul>') bilan o'zi
   qo'llanadi yoki o'tkazib yuboriladi (SETUP_MODULES bo'sh — hammasi). Bu fayl har doim ishlaydi (Core):
   demo foydalanuvchi, ombor turlari, mahsulot kategoriyalari, 24 mahsulot, valyuta kurslari, kassa/bank hisoblari.

   Ssenariy: modullar bir-biriga bog'liq hujjat zanjiri (xarid -> ishlab chiqarish -> sotuv) bo'lagini o'zi yozadi, lekin
   miqdorlar bitta rejadan olinadi — shu fayl uni demo_tmp sxemasiga yozadi (mahsulot narxlari, BOM, ishlab chiqarish rejasi,
   sarf, haftalik o'tkazma, xarid qatorlari, sotuvga tayyor miqdor). 299_Core_Finish.sql demo_tmp'ni o'chiradi.

   Sanalar GETDATE() ga nisbatan (shablon qachon yaratilsa, shu kungacha bo'lgan ~3 oy tarix).
   Idempotent: USERS'da 'demo' bo'lsa fayl hech narsa qilmaydi. Bitta batch (GO yo'q) — RETURN butun
   faylni to'xtatadi; runner faylni bitta tranzaksiyada bajaradi.

   Demo kirish: demo / demo2026 (Admin roli, parol almashtirish talab qilinmaydi).
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo master allaqachon bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @start date = DATEADD(day, -56, @t);          -- ishlab chiqarish davri: 8 hafta
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @usd int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'USD');
DECLARE @rub int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'RUB');
DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @pcs int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'PCS');
DECLARE @kg int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'KG');
IF @uzs IS NULL OR @admin IS NULL OR @pcs IS NULL OR @kg IS NULL
    THROW 50001, 'Demo: seed topilmadi (UZS / Admin roli / UNITS PCS, KG)', 1;

-- ---------------- korxona ----------------
UPDATE dbo.COMPANIES
SET Name = '"Demo Plast" MChJ', City = 'Toshkent', Address = 'Toshkent sh., Sergeli tumani, Sanoat ko''chasi 12',
    Telephone = '+998 71 200-00-00', Email = 'info@demo.tahsinerp.uz', DUNS = '300000001', DirectorName = N'Karimov Aziz'
WHERE ID = 1 AND Name = 'Yangi korxona';

-- ---------------- demo foydalanuvchi (PasswordHasher: PBKDF2-SHA256, parol demo2026) ----------------
INSERT INTO dbo.USERS (Uname, FullName, Password, Email, IsDeleted, IsActive, HashCode, CompanyID, MustChangePassword)
VALUES ('demo', 'Karimov Aziz Bahodirovich', '$p2$ujX+vF/aImlOpyzu0SKlPaRrt7gwLY0ulVPF/t9bqbI', 'demo@demo.tahsinerp.uz', 0, 1, '8WCZFoZKnj2cfxviGpVjoQ==', 1, 0);
DECLARE @u int = SCOPE_IDENTITY();
INSERT INTO dbo.USERROLES (UserID, RoleID) VALUES (@u, @admin);

-- ---------------- omborlar: tur (WarehouseRoles) va mas'ul (MRP) ----------------
-- Tanlanmagan modul omborlarini Setup/300_ModuleProfile.sql (demodan keyin) o'chiradi.
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SUPPLY'     WHERE ID = 3 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET WarehouseType = 'PRODUCTION' WHERE ID = 6 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SALES'      WHERE ID = 7 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET MRP = @u WHERE ID IN (3, 6, 7) AND MRP IS NULL;

-- ---------------- mahsulot kategoriyalari (Code = 5 xonali MAX+1, ProductCategoryController) ----------------
DECLARE @catBase int = ISNULL((SELECT MAX(TRY_CAST(Code AS int)) FROM dbo.PRODUCT_CATEGORIES), 0);
DECLARE @sortBase int = ISNULL((SELECT MAX(SortOrder) FROM dbo.PRODUCT_CATEGORIES), 0);
DECLARE @cats TABLE (K int, ID int);
INSERT INTO dbo.PRODUCT_CATEGORIES (Code, Name, IsRawMaterial, IsFinishedGood, IsComponent, IsConsumable, RequiresNORM, RequiresCutting,
                                    IncludeInMRP, AllowPurchase, AllowSales, RawMaterialGroup, RawMaterialSubGrp, SortOrder, IsActive,
                                    CreatedDT, CalculateByWeight, IsDeleted)
OUTPUT inserted.SortOrder - @sortBase, inserted.ID INTO @cats (K, ID)
VALUES (RIGHT('00000' + CAST(@catBase + 1 AS varchar(10)), 5), N'Polimer granulalar',  1, 0, 0, 0, 0, 0, 1, 1, 0, N'Polimer', NULL, @sortBase + 1, 1, DATEADD(day, -100, @t), 0, 0),
       (RIGHT('00000' + CAST(@catBase + 2 AS varchar(10)), 5), N'Bo''yoq va qo''shimchalar', 1, 0, 0, 0, 0, 0, 1, 1, 0, N'Qo''shimcha', NULL, @sortBase + 2, 1, DATEADD(day, -100, @t), 0, 0),
       (RIGHT('00000' + CAST(@catBase + 3 AS varchar(10)), 5), N'Qadoq va butlovchilar', 1, 0, 0, 1, 0, 0, 1, 1, 0, N'Qadoq', NULL, @sortBase + 3, 1, DATEADD(day, -100, @t), 0, 0),
       (RIGHT('00000' + CAST(@catBase + 4 AS varchar(10)), 5), N'Plastik idishlar',    0, 1, 0, 0, 1, 0, 1, 0, 1, NULL, NULL, @sortBase + 4, 1, DATEADD(day, -100, @t), 0, 0),
       (RIGHT('00000' + CAST(@catBase + 5 AS varchar(10)), 5), N'Plastik mebel',       0, 1, 0, 0, 1, 0, 1, 0, 1, NULL, NULL, @sortBase + 5, 1, DATEADD(day, -100, @t), 0, 0);

-- ---------------- mahsulotlar (ProductController: Type XOMASHYO / MAXSULOT) ----------------
-- BasePrice: xomashyo — xarid narxi (QQSsiz), tayyor — sotuv narxi (QQSsiz), so'm. DailyQty — kunlik ishlab chiqarish.
DECLARE @p TABLE (PNo varchar(15) PRIMARY KEY, Name varchar(50), Cat int, IsRaw bit, Unit int, BasePrice decimal(18,2),
                  Weight float NULL, DailyQty int NULL, SupplierNo int NULL, Ord int, ID int NULL);
INSERT INTO @p (PNo, Name, Cat, IsRaw, Unit, BasePrice, Weight, DailyQty, SupplierNo, Ord) VALUES
('XM-001', 'Polipropilen granula (PP)',          1, 1, @kg,  16500, 1, NULL, 1, 1),
('XM-002', 'Polietilen granula HDPE',            1, 1, @kg,  17800, 1, NULL, 2, 2),
('XM-003', 'Polietilen granula LDPE',            1, 1, @kg,  18200, 1, NULL, 2, 3),
('XM-004', 'Masterbatch oq',                     2, 1, @kg,  38000, 1, NULL, 3, 4),
('XM-005', 'Masterbatch ko''k',                  2, 1, @kg,  42000, 1, NULL, 3, 5),
('XM-006', 'Masterbatch qizil',                  2, 1, @kg,  44000, 1, NULL, 3, 6),
('XM-007', 'UV stabilizator',                    2, 1, @kg,  65000, 1, NULL, 3, 7),
('XM-008', 'Karton quti katta (60x40x40)',       3, 1, @pcs,  6500, NULL, NULL, 4, 8),
('XM-009', 'Karton quti kichik (40x30x30)',      3, 1, @pcs,  4200, NULL, NULL, 4, 9),
('XM-010', 'Etiketka (o''z-o''zidan yopishuvchi)', 3, 1, @pcs,   120, NULL, NULL, 4, 10),
('XM-011', 'Stretch plyonka',                    3, 1, @kg,  24000, 1, NULL, 5, 11),
('XM-012', 'Metall dasta (chelak uchun)',        3, 1, @pcs,   900, NULL, NULL, 5, 12),
('TM-001', 'Chelak 5 L oq',                      4, 0, @pcs, 14000, 0.30, 400, NULL, 13),
('TM-002', 'Chelak 10 L oq',                     4, 0, @pcs, 22000, 0.47, 300, NULL, 14),
('TM-003', 'Chelak 10 L ko''k',                  4, 0, @pcs, 23000, 0.47, 150, NULL, 15),
('TM-004', 'Chelak 20 L ko''k (UV)',             4, 0, @pcs, 38000, 0.88, 120, NULL, 16),
('TM-005', 'Tog''ora 30 L',                      4, 0, @pcs, 32000, 0.77, 100, NULL, 17),
('TM-006', 'Oziq-ovqat konteyneri 0,5 L',        4, 0, @pcs,  3200, 0.04, 1200, NULL, 18),
('TM-007', 'Oziq-ovqat konteyneri 1 L',          4, 0, @pcs,  4800, 0.06, 900, NULL, 19),
('TM-008', 'Oziq-ovqat konteyneri 2 L',          4, 0, @pcs,  7500, 0.09, 600, NULL, 20),
('TM-009', 'Savat (kir uchun)',                  4, 0, @pcs, 12000, 0.31, 200, NULL, 21),
('TM-010', 'Gul tuvak 3 L',                      4, 0, @pcs,  9000, 0.23, 150, NULL, 22),
('TM-011', 'Plastik stul',                       5, 0, @pcs, 85000, 2.50, 60, NULL, 23),
('TM-012', 'Plastik stol (bog'' uchun)',         5, 0, @pcs, 240000, 7.80, 15, NULL, 24);

DECLARE @pm TABLE (PNo varchar(15), ID int);
INSERT INTO dbo.PRODUCTS (PNo, Name, Weight, Type, Description, IsDeleted, UnitID, IsRawMaterial, IsComponent, IsFinishedGood,
                          CategoryID, ShopID, CalculateByWeight)
OUTPUT inserted.PNo, inserted.ID INTO @pm (PNo, ID)
SELECT p.PNo, p.Name, p.Weight, CASE WHEN p.IsRaw = 1 THEN 'XOMASHYO' ELSE 'MAXSULOT' END, NULL, 0, p.Unit,
       p.IsRaw, 0, 1 - p.IsRaw, c.ID, 1, 0
FROM @p p JOIN @cats c ON c.K = p.Cat
ORDER BY p.Ord;
UPDATE p SET ID = m.ID FROM @p p JOIN @pm m ON m.PNo = p.PNo;

-- ---------------- valyuta kurslari (UZS uchun 1 so'm; CRM boshqa valyutani shu orqali so'mga o'giradi) ----------------
;WITH n AS (SELECT TOP (181) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects)
INSERT INTO dbo.CURRENCY_RATES (CurrencyID, RateDate, Rate, Source, CreatedDT)
SELECT c.CurrencyID, DATEADD(day, -n.k, @t),
       CAST(c.Base + c.Step * ((180 - n.k) / 7) + c.Wobble * ((n.k * 37) % 11 - 5) AS decimal(18, 6)), 'CBU', DATEADD(day, -n.k, @t)
FROM n
CROSS JOIN (VALUES (@usd, 12650.00, 12.0, 4.0), (@rub, 152.00, -0.20, 0.35)) c (CurrencyID, Base, Step, Wobble)
WHERE c.CurrencyID IS NOT NULL;

-- ---------------- pul hisoblari (New-TenantDb admin bosqichi FIN_ACCOUNTS bo'sh bo'lsagina yaratadi) ----------------
-- Core'da: moliya, logistika (yo'l varaqasi to'lovi) va distributsiya (mijoz to'lovi) shu hisoblarga yozadi.
INSERT INTO dbo.FIN_ACCOUNTS (Code, Name, OwnerName, CurrencyID, IsCash, IsBank, IsActive, CreatedByUserID, CreatedDT, BalanceCategoryID, IsDeleted)
VALUES ('9000', N'Kassa', NULL, @uzs, 1, 0, 1, @u, DATEADD(day, -100, @t), (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'CASH'), 0),
       ('9001', N'Hisob raqam (Demo bank)', NULL, @uzs, 0, 1, 1, @u, DATEADD(day, -100, @t), (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'BANK'), 0);

/* =====================================================================
   Ssenariy (demo_tmp): modul fayllari shu jadvallardan o'qiydi — har biri o'z bo'lagini yozadi.
   ===================================================================== */
IF SCHEMA_ID(N'demo_tmp') IS NULL EXEC (N'CREATE SCHEMA demo_tmp');

-- mahsulotlar va narxlar
SELECT ID, PNo, Name, Unit AS UnitID, IsRaw, BasePrice AS Price, DailyQty, SupplierNo,
       CASE WHEN IsRaw = 0 THEN Ord - 12 END AS FgIdx
INTO demo_tmp.PROD
FROM @p;

-- BOM: 1 dona tayyor mahsulot uchun sarf (Qty — norma), kg materiallarga 2% chiqindi (BomQty — BOM_LINES, sarf shu bo'yicha)
DECLARE @bom TABLE (FG varchar(15), Mat varchar(15), Qty decimal(18,4));
INSERT INTO @bom (FG, Mat, Qty) VALUES
('TM-001','XM-001',0.28),  ('TM-001','XM-004',0.006), ('TM-001','XM-012',1), ('TM-001','XM-010',1), ('TM-001','XM-008',0.05),
('TM-002','XM-001',0.45),  ('TM-002','XM-004',0.009), ('TM-002','XM-012',1), ('TM-002','XM-010',1), ('TM-002','XM-008',0.1),
('TM-003','XM-001',0.45),  ('TM-003','XM-005',0.009), ('TM-003','XM-012',1), ('TM-003','XM-010',1), ('TM-003','XM-008',0.1),
('TM-004','XM-001',0.85),  ('TM-004','XM-005',0.017), ('TM-004','XM-007',0.004), ('TM-004','XM-012',1), ('TM-004','XM-010',1), ('TM-004','XM-011',0.01),
('TM-005','XM-002',0.75),  ('TM-005','XM-006',0.015), ('TM-005','XM-010',1), ('TM-005','XM-011',0.01),
('TM-006','XM-001',0.035), ('TM-006','XM-010',1), ('TM-006','XM-009',0.02),
('TM-007','XM-001',0.055), ('TM-007','XM-010',1), ('TM-007','XM-009',0.025),
('TM-008','XM-001',0.09),  ('TM-008','XM-010',1), ('TM-008','XM-009',0.04),
('TM-009','XM-003',0.30),  ('TM-009','XM-005',0.006), ('TM-009','XM-010',1), ('TM-009','XM-008',0.05),
('TM-010','XM-003',0.22),  ('TM-010','XM-006',0.005), ('TM-010','XM-010',1), ('TM-010','XM-009',0.05),
('TM-011','XM-001',2.40),  ('TM-011','XM-004',0.05),  ('TM-011','XM-007',0.012), ('TM-011','XM-010',1), ('TM-011','XM-011',0.03),
('TM-012','XM-001',6.50),  ('TM-012','XM-002',1.00),  ('TM-012','XM-004',0.14), ('TM-012','XM-007',0.03), ('TM-012','XM-010',1), ('TM-012','XM-011',0.08);
SELECT f.ID AS FG, m.ID AS Mat, b.Qty, CAST(CASE WHEN m.Unit = @kg THEN ROUND(b.Qty * 1.02, 4) ELSE b.Qty END AS decimal(18,4)) AS BomQty,
       m.Unit AS UnitID, CASE WHEN m.Unit = @kg THEN 2 ELSE 0 END AS WastePercent
INTO demo_tmp.BOM
FROM @bom b JOIN @p f ON f.PNo = b.FG JOIN @p m ON m.PNo = b.Mat;

-- 1. ishlab chiqarish rejasi (yakshanbasiz, har mahsulot kun ora)
CREATE TABLE demo_tmp.PP (K int IDENTITY PRIMARY KEY, ProductID int, D date, DayIdx int, Wk int, Qty int, DayShift int, NightShift int,
                          At datetime, Rejected int);
;WITH n AS (SELECT TOP (56) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects),
d AS (SELECT DATEADD(day, k, @start) AS D, k AS DayIdx FROM n WHERE DATEDIFF(day, '19000101', DATEADD(day, k, @start)) % 7 <> 6)
INSERT INTO demo_tmp.PP (ProductID, D, DayIdx, Wk, Qty, DayShift, NightShift, At, Rejected)
SELECT p.ID, d.D, d.DayIdx, d.DayIdx / 7, q.Qty, CEILING(q.Qty * 0.6), q.Qty - CEILING(q.Qty * 0.6),
       DATEADD(minute, 17 * 60 + p.FgIdx * 3, CAST(d.D AS datetime)),
       CASE WHEN (d.DayIdx + p.FgIdx) % 3 = 0 THEN FLOOR(q.Qty * 0.01) ELSE 0 END
FROM d CROSS JOIN demo_tmp.PROD p
CROSS APPLY (SELECT CAST(ROUND(p.DailyQty * 2 * (0.85 + 0.05 * ((d.DayIdx * 3 + p.FgIdx) % 7)), 0) AS int) AS Qty) q
WHERE p.IsRaw = 0 AND (d.DayIdx + p.FgIdx) % 2 = 0
ORDER BY d.D, p.FgIdx;

-- har ishlab chiqarishning xomashyo sarfi (BOM x miqdor)
CREATE TABLE demo_tmp.CONS (K int IDENTITY PRIMARY KEY, PPK int, Mat int, Qty decimal(18,4), UnitID int, At datetime);
INSERT INTO demo_tmp.CONS (PPK, Mat, Qty, UnitID, At)
SELECT pp.K, b.Mat, ROUND(b.BomQty * pp.Qty, 4), b.UnitID, pp.At
FROM demo_tmp.PP pp JOIN demo_tmp.BOM b ON b.FG = pp.ProductID
ORDER BY pp.At, pp.K, b.Mat;

-- 2. haftalik o'tkazma SUPPLY -> PRODUCTION (hafta boshida 08:00, ehtiyoj + 5%)
CREATE TABLE demo_tmp.TR (K int IDENTITY PRIMARY KEY, Wk int, Mat int, Qty decimal(18,4), UnitID int, At datetime);
INSERT INTO demo_tmp.TR (Wk, Mat, Qty, UnitID, At)
SELECT pp.Wk, c.Mat,
       CASE WHEN c.UnitID = @kg THEN CEILING(SUM(c.Qty) * 1.05) ELSE CEILING(SUM(c.Qty) * 1.05 / 10) * 10 END,
       c.UnitID, DATEADD(hour, 8, CAST(DATEADD(day, 7 * pp.Wk, @start) AS datetime))
FROM demo_tmp.CONS c JOIN demo_tmp.PP pp ON pp.K = c.PPK
GROUP BY pp.Wk, c.Mat, c.UnitID
ORDER BY pp.Wk, c.Mat;

-- 3. xaridlar: 5 tur x 5 ta'minotchi (jami o'tkazmaning ~130%)
CREATE TABLE demo_tmp.PR (R int, Mat int, SupNo int, Qty decimal(18,4), Price decimal(18,2), UnitID int, D date);
INSERT INTO demo_tmp.PR (R, Mat, SupNo, Qty, Price, UnitID, D)
SELECT r.R, p.ID, p.SupplierNo,
       CASE WHEN p.UnitID = @kg THEN CEILING(tt.Qty * 0.26 / 25) * 25 ELSE CEILING(tt.Qty * 0.26 / 100) * 100 END,
       ROUND(p.Price * (1 + 0.015 * r.R), 2), p.UnitID, DATEADD(day, r.Ofs, @t)
FROM demo_tmp.PROD p
JOIN (SELECT Mat, SUM(Qty) AS Qty FROM demo_tmp.TR GROUP BY Mat) tt ON tt.Mat = p.ID
CROSS JOIN (VALUES (0, -80), (1, -62), (2, -44), (3, -26), (4, -9)) r (R, Ofs)
WHERE p.IsRaw = 1;

-- 4. haftalik sotuvga tayyor tayyor mahsulot (sifat nazoratidan o'tgani; MES uni sotuv omboriga o'tkazadi, CRM shu bo'yicha sotadi)
SELECT Wk, ProductID, SUM(Qty - Rejected) AS Qty
INTO demo_tmp.FTL
FROM demo_tmp.PP WHERE Wk <= 6
GROUP BY Wk, ProductID;

PRINT 'Demo master: OK';
