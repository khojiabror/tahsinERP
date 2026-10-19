/* =====================================================================
   tahsinERP — DEMO korxona: master ma'lumot (New-TenantDb.ps1 -Demo, seed'dan keyin).
   Uydirma "Demo Plast" MChJ — plastik idishlar ishlab chiqaruvchi.

   Bu faylda: demo foydalanuvchi, ombor turlari/joylari, mahsulot kategoriyalari, 24 mahsulot,
   BOM + norma, 5 mijoz, 5 ta'minotchi, valyuta kurslari, kassa/bank hisoblari, aksiyadorlar.

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
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @usd int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'USD');
DECLARE @rub int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'RUB');
DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE RName = N'Admin' AND ISNULL(IsDeleted, 0) = 0);
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
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SUPPLY'     WHERE ID = 3 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET WarehouseType = 'PRODUCTION' WHERE ID = 6 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET WarehouseType = 'SALES'      WHERE ID = 7 AND WarehouseType IS NULL;
UPDATE dbo.WAREHOUSES SET MRP = @u WHERE ID IN (3, 6, 7) AND MRP IS NULL;
DECLARE @whProd int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'PRODUCTION' AND ISNULL(IsDeleted, 0) = 0);
IF @whProd IS NULL THROW 50002, 'Demo: PRODUCTION ombori topilmadi', 1;

-- Ishlab chiqarish ombori DLOC (ProductionDlocGuard faol joy talab qiladi)
INSERT INTO dbo.WAREHOUSE_LOCATIONS (WarehouseID, LocationCode, LocationName, CapacityUomID, MinCapacity, MaxCapacity,
                                     AllowMixedLots, AllowMixedProducts, IsActive, IsDeleted, CreatedAt)
VALUES (@whProd, 'A-01', N'Sex ombori — granula va tayyor mahsulot', NULL, 0, 1000000, 1, 1, 1, 0, DATEADD(day, -100, @t)),
       (@whProd, 'A-02', N'Sex ombori — qadoq va butlovchilar',       NULL, 0, 1000000, 1, 1, 1, 0, DATEADD(day, -100, @t));

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

-- ---------------- BOM / norma (NormController: NORM_INPUT_LINES -> NORMVERSIONS/NORMLINES -> BOM) ----------------
-- 1 dona tayyor mahsulot uchun sarf; kg materiallarga 2% chiqindi.
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

DECLARE @normFrom date = DATEFROMPARTS(YEAR(DATEADD(day, -100, @t)), MONTH(DATEADD(day, -100, @t)), 1);
DECLARE @normMonth char(7) = CONVERT(char(7), @normFrom, 126);   -- 'yyyy-MM' = BOM_HEADERS.VersionNo

INSERT INTO dbo.NORM_INPUT_LINES (ProductID, MaterialID, CutLengthMm, PiecesPerProduct, Month, CreatedByUserID, CreatedDT, Price,
                                  Consumption, WasteAmount, UnitID, Comment)
SELECT f.ID, m.ID, 0, 1, @normMonth, @u, DATEADD(day, -100, @t), m.BasePrice, b.Qty,
       CASE WHEN m.Unit = @kg THEN ROUND(b.Qty * 0.02, 4) ELSE 0 END, m.Unit, NULL
FROM @bom b JOIN @p f ON f.PNo = b.FG JOIN @p m ON m.PNo = b.Mat;

DECLARE @nv TABLE (ProductID int, ID int);
INSERT INTO dbo.NORMVERSIONS (ProductID, EffectiveFrom, EffectiveTo, Comment, IsDeleted)
OUTPUT inserted.ProductID, inserted.ID INTO @nv
SELECT ID, @normFrom, NULL, N'Asosiy norma', 0 FROM @p WHERE IsRaw = 0;

INSERT INTO dbo.NORMLINES (NormID, MaterialID, QtyPerUnit, WastePercent, UnitID)
SELECT nv.ID, m.ID, b.Qty, CASE WHEN m.Unit = @kg THEN 2 ELSE 0 END, m.Unit
FROM @bom b JOIN @p f ON f.PNo = b.FG JOIN @p m ON m.PNo = b.Mat JOIN @nv nv ON nv.ProductID = f.ID;

DECLARE @bh TABLE (ProductID int, ID int);
INSERT INTO dbo.BOM_HEADERS (ProductID, VersionNo, IsActive, CreatedAt)
OUTPUT inserted.ProductID, inserted.ID INTO @bh
SELECT ID, @normMonth, 1, DATEADD(day, -100, @t) FROM @p WHERE IsRaw = 0;

INSERT INTO dbo.BOM_LINES (BOMID, ComponentID, Qty, UnitID)
SELECT bh.ID, m.ID, CASE WHEN m.Unit = @kg THEN ROUND(b.Qty * 1.02, 4) ELSE b.Qty END, m.Unit
FROM @bom b JOIN @p f ON f.PNo = b.FG JOIN @p m ON m.PNo = b.Mat JOIN @bh bh ON bh.ProductID = f.ID;

-- ---------------- mijozlar va ta'minotchilar (IsDeleted = 0 — ro'yxatlar IsDeleted == false bilan) ----------------
INSERT INTO dbo.CUSTOMERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Uy-Ro''zg''or Savdo" MChJ', 'Chilonzor tumani, Bunyodkor ko''chasi 45', 'O''zbekiston', '+998 90 111-22-33', 'savdo@uyrozgor.uz', 'Rahimov Bobur', 'Rahimov Anvar', 0, '301111111', 'MChJ', 'Toshkent'),
       ('"Bozor Plus" MChJ',          'Registon ko''chasi 8',                     'O''zbekiston', '+998 91 222-33-44', 'info@bozorplus.uz', 'Saidova Malika', 'Saidov Jamshid', 0, '302222222', 'MChJ', 'Samarqand'),
       ('"Orzu Market" XK',           'Mustaqillik ko''chasi 17',                 'O''zbekiston', '+998 93 333-44-55', 'orzu.market@mail.uz', 'Tursunov Ulug''bek', 'Tursunova Dilnoza', 0, '303333333', 'XK', 'Farg''ona'),
       ('"Mega Opt" MChJ',            'Bobur shoh ko''chasi 102',                 'O''zbekiston', '+998 94 444-55-66', 'opt@megaopt.uz', 'Qodirov Sherzod', 'Qodirov Farhod', 0, '304444444', 'MChJ', 'Andijon'),
       ('"Navro''z Trade" MChJ',      'B.Naqshband ko''chasi 21',                 'O''zbekiston', '+998 95 555-66-77', 'trade@navroz.uz', 'Ergasheva Nodira', 'Ergashev Rustam', 0, '305555555', 'MChJ', 'Buxoro');

INSERT INTO dbo.SUPPLIERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Sho''rtan Polimer Savdo" MChJ', 'Sanoat zonasi, 3-uchastka',  'O''zbekiston', '+998 75 521-10-10', 'sales@shortanpolimer.uz', 'Normatov Ilhom', 'Normatov Akmal', 0, '311111111', 'Granula', 'Qarshi'),
       ('"Polietilen Invest" MChJ',      'Olmazor tumani, Sanoat 5',   'O''zbekiston', '+998 71 245-20-20', 'info@pe-invest.uz', 'Yusupov Dilshod', 'Yusupov Bahrom', 0, '312222222', 'Granula', 'Toshkent'),
       ('"Color Master" MChJ',           'Yangiyo''l yo''li 14',       'O''zbekiston', '+998 71 233-30-30', 'order@colormaster.uz', 'Abdullayeva Zarina', 'Abdullayev Sanjar', 0, '313333333', 'Bo''yoq', 'Toshkent'),
       ('"Karton Pack" MChJ',            'Chirchiq sh., Zavod ko''chasi 2', 'O''zbekiston', '+998 70 715-40-40', 'pack@kartonpack.uz', 'Hasanov Otabek', 'Hasanov Murod', 0, '314444444', 'Qadoq', 'Chirchiq'),
       ('"Metall Detal Servis" XK',      'Yunusobod tumani, 19-mavze', 'O''zbekiston', '+998 99 850-50-50', 'mds@detal.uz', 'Mirzayev Sardor', 'Mirzayev Hamid', 0, '315555555', 'Butlovchi', 'Toshkent');

-- ---------------- valyuta kurslari (UZS uchun 1 so'm; CRM boshqa valyutani shu orqali so'mga o'giradi) ----------------
;WITH n AS (SELECT TOP (181) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects)
INSERT INTO dbo.CURRENCY_RATES (CurrencyID, RateDate, Rate, Source, CreatedDT)
SELECT c.CurrencyID, DATEADD(day, -n.k, @t),
       CAST(c.Base + c.Step * ((180 - n.k) / 7) + c.Wobble * ((n.k * 37) % 11 - 5) AS decimal(18, 6)), 'CBU', DATEADD(day, -n.k, @t)
FROM n
CROSS JOIN (VALUES (@usd, 12650.00, 12.0, 4.0), (@rub, 152.00, -0.20, 0.35)) c (CurrencyID, Base, Step, Wobble)
WHERE c.CurrencyID IS NOT NULL;

-- ---------------- pul hisoblari (New-TenantDb admin bosqichi FIN_ACCOUNTS bo'sh bo'lsagina yaratadi) ----------------
INSERT INTO dbo.FIN_ACCOUNTS (Code, Name, OwnerName, CurrencyID, IsCash, IsBank, IsActive, CreatedByUserID, CreatedDT, BalanceCategoryID, IsDeleted)
VALUES ('9000', N'Kassa', NULL, @uzs, 1, 0, 1, @u, DATEADD(day, -100, @t), (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'CASH'), 0),
       ('9001', N'Hisob raqam (Demo bank)', NULL, @uzs, 0, 1, 1, @u, DATEADD(day, -100, @t), (SELECT ID FROM dbo.FIN_BALANCE_CATEGORIES WHERE SystemCode = 'BANK'), 0);

-- Ustav kapitali: aksiyador hissalari kategoriyasi balansdagi CHARTER_CAPITAL qatoriga bog'lanadi
INSERT INTO dbo.FIN_BALANCE_CATEGORY_CATEGORIES (BalanceCategoryID, CategoryID)
SELECT b.ID, c.ID
FROM dbo.FIN_BALANCE_CATEGORIES b CROSS JOIN dbo.FIN_CATEGORIES c
WHERE b.SystemCode = 'CHARTER_CAPITAL' AND c.SystemCode = 'SHAREHOLDER_CONTRIBUTION'
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORY_CATEGORIES x WHERE x.BalanceCategoryID = b.ID AND x.CategoryID = c.ID);

INSERT INTO dbo.SHAREHOLDERS (FullName, Phone, Notes, IsActive, IsDeleted)
VALUES (N'Karimov Aziz Bahodirovich', N'+998 90 900-00-01', N'Ta''sischi, direktor', 1, 0),
       (N'Sobirova Gulnora Akmalovna', N'+998 90 900-00-02', N'Ta''sischi', 1, 0);
INSERT INTO dbo.SHAREHOLDER_SHARE_PERIODS (ShareholderID, FromDate, ToDate, SharePercent)
SELECT ID, DATEADD(day, -100, @t), NULL, CASE WHEN FullName LIKE N'Karimov%' THEN 60 ELSE 40 END
FROM dbo.SHAREHOLDERS WHERE FullName IN (N'Karimov Aziz Bahodirovich', N'Sobirova Gulnora Akmalovna');

PRINT 'Demo master: OK';
