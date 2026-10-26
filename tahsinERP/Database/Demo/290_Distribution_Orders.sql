/* =====================================================================
   tahsinERP — DEMO: Distributsiya — hududlar, savdo nuqtalari, agentlar, narxlar, buyurtmalar, jo'natish, to'lovlar
   (200_Core_Master.sql dan keyin; 300_ModuleProfile.sql savdo omborini dst.WAREHOUSE_SETTINGS ga yozadi).

   Areas/Distribution servislari yozadigan shaklda:
     mijoz = dbo.CUSTOMERS (Type 'DST') + dst.CUSTOMER_PROFILES (IsApproved = 1, kredit limiti, to'lov muddati 14 kun);
     mahsulot = demo tayyor mahsulotlari (TM-001..008) + dst.PRODUCT_PROFILES; narx — dst.PRICE_LIST_ITEMS (2 Ulgurji, 1 Chakana);
     agent = dbo.USERS + dst.SALES_AGENTS (paroli ishlamaydi — admin "parolni tiklash" bilan beradi);
     ombor = SALES ombori: distributsiya uchun kirim (INCOMES, FIFO tannarx manbai);
     buyurtma D-yyyy-NNNNN: Status/OrderStatus juftligi, D_ORDER_STATUS_LOG zanjiri, tasdiqlangan — STOCK_RESERVATIONS,
     jo'natish DJ-... = dst.D_SHIPMENTS + dbo.EXPENSES (DocNo bir xil) + CUSTOMER_LEDGER debet (muddat = sana + PaymentDays),
     to'lov DP-... = dst.D_PAYMENTS + ledger kredit (+ hisobga tushsa FIN_TRANSACTIONS 'DstPayment'); to'liq qoplangan — Closed (6).
   Invariantlar: Subtotal/Total = qatorlar yig'indisi, ShippedTotal = jo'natishlar, QtyShippedBase = jo'natilgan, rezerv faqat
   1/2/3 holatda, muddati o'tgan qarz yo'q (UI'da yangi buyurtmani tasdiqlash bloklanmasin). PRODUCT_STOCKS — 299.
   Faqat Distribution moduli tanlangan bo'lsa. Idempotent: dst.D_ORDERS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Distribution') = 0 OR OBJECT_ID(N'demo_tmp.PROD') IS NULL OR EXISTS (SELECT 1 FROM dst.D_ORDERS)
BEGIN
    PRINT 'Demo distributsiya: modul tanlanmagan yoki hujjatlar bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @t0 datetime2(0) = CAST(@t AS datetime2(0));
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @wh int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SALES' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @kassa int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9000');
DECLARE @bank int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9001');
DECLARE @catSales int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'SALES');
DECLARE @ptWholesale int = (SELECT ID FROM dst.PRICE_TYPES WHERE IsDefault = 1);
DECLARE @ptRetail int = (SELECT MIN(ID) FROM dst.PRICE_TYPES WHERE IsDefault = 0 AND IsActive = 1);
IF @u IS NULL OR @wh IS NULL OR @ptWholesale IS NULL OR @catSales IS NULL
    THROW 50291, 'Demo distributsiya: demo user / SALES ombori / narx turi / SALES kategoriyasi topilmadi', 1;
CREATE TABLE #map (K int, ID int);

/* ---------------- hududlar, jamoa, agentlar ---------------- */
INSERT INTO dst.TERRITORIES (ParentID, Level, Name, NameRu, IsActive) VALUES (NULL, 1, N'Toshkent shahri', N'г. Ташкент', 1);
DECLARE @trCity int = SCOPE_IDENTITY();
INSERT INTO dst.TERRITORIES (ParentID, Level, Name, NameRu, IsActive) VALUES (NULL, 1, N'Toshkent viloyati', N'Ташкентская обл.', 1);
DECLARE @trReg int = SCOPE_IDENTITY();
INSERT INTO dst.TERRITORIES (ParentID, Level, Name, NameRu, IsActive)
VALUES (@trCity, 2, N'Chilonzor tumani', N'Чиланзарский р-н', 1), (@trCity, 2, N'Yunusobod tumani', N'Юнусабадский р-н', 1),
       (@trReg, 2, N'Chirchiq shahri', N'г. Чирчик', 1);
DECLARE @trChil int = (SELECT ID FROM dst.TERRITORIES WHERE ParentID = @trCity AND Name = N'Chilonzor tumani');
DECLARE @trYun int = (SELECT ID FROM dst.TERRITORIES WHERE ParentID = @trCity AND Name = N'Yunusobod tumani');
DECLARE @trChir int = (SELECT ID FROM dst.TERRITORIES WHERE ParentID = @trReg AND Name = N'Chirchiq shahri');

INSERT INTO dst.SALES_TEAMS (Name, SupervisorUserID, IsActive) VALUES (N'Toshkent savdo jamoasi', @u, 1);
DECLARE @team int = SCOPE_IDENTITY();

-- agentlar: kirish uchun parol yo'q (xesh mos kelmaydi) — admin Foydalanuvchilar sahifasida parol beradi
INSERT INTO dbo.USERS (Uname, FullName, Password, Email, IsDeleted, IsActive, HashCode, CompanyID, MustChangePassword)
SELECT v.Uname, v.FullName, '!' + CONVERT(varchar(40), CRYPT_GEN_RANDOM(16), 2), NULL, 0, 1, CONVERT(varchar(30), CRYPT_GEN_RANDOM(12), 2), 1, 1
FROM (VALUES ('agent1', 'Usmonov Bekzod (agent)'), ('agent2', 'Rahimova Nilufar (agent)')) v (Uname, FullName)
WHERE NOT EXISTS (SELECT 1 FROM dbo.USERS x WHERE x.Uname = v.Uname);
CREATE TABLE #ag (No int PRIMARY KEY, UserID int);
INSERT INTO #ag SELECT CASE Uname WHEN 'agent1' THEN 1 ELSE 2 END, ID FROM dbo.USERS WHERE Uname IN ('agent1', 'agent2');
INSERT INTO dst.SALES_AGENTS (UserID, TeamID, WarehouseID, Phone, IsExpeditor, IsActive)
SELECT a.UserID, @team, @wh, CASE a.No WHEN 1 THEN '+998901234501' ELSE '+998901234502' END, 0, 1 FROM #ag a;

/* ---------------- savdo nuqtalari (dbo.CUSTOMERS Type 'DST' + profil) ---------------- */
CREATE TABLE #c (No int PRIMARY KEY, Name varchar(50) COLLATE DATABASE_DEFAULT, Owner nvarchar(100) COLLATE DATABASE_DEFAULT, Phone varchar(25) COLLATE DATABASE_DEFAULT,
                 PointType varchar(20) COLLATE DATABASE_DEFAULT, Cat char(1) COLLATE DATABASE_DEFAULT, Territory int, AgentNo int, Lat decimal(9,6), Lng decimal(9,6), Landmark nvarchar(200) COLLATE DATABASE_DEFAULT,
                 VisitDays tinyint, ID int NULL);
INSERT INTO #c VALUES
(1, '"Oila" do''koni',        N'Nazarov Akbar',    '+998901112201', 'SHOP',   'A', @trChil, 1, 41.285600, 69.203400, N'Chilonzor-9, bozor yonida', 1 + 8, NULL),
(2, '"Baraka" minimarket',    N'Qosimova Lola',    '+998901112202', 'SHOP',   'B', @trChil, 1, 41.278100, 69.214500, N'Qatortol ko''chasi 12', 2 + 16, NULL),
(3, 'Yunusobod bozori, 14-do''kon', N'Ismoilov Rustam', '+998901112203', 'MARKET', 'A', @trYun, 2, 41.366200, 69.288700, N'Bozor ichi, 2-qator', 1 + 4, NULL),
(4, '"Ideal Uy" XK',          N'Mirzayev Doston',  '+998901112204', 'SHOP',   'B', @trYun, 2, 41.355400, 69.295100, N'Amir Temur shoh ko''chasi 107', 4 + 32, NULL),
(5, 'Chirchiq "Universal"',   N'Hasanova Gulchehra', '+998901112205', 'SHOP', 'C', @trChir, 1, 41.468900, 69.582100, N'Markaziy ko''cha 3', 8, NULL);
INSERT INTO dbo.CUSTOMERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
SELECT Name, CAST(Landmark AS varchar(200)), 'O''zbekiston', Phone, NULL, LEFT(CAST(Owner AS varchar(100)), 30), LEFT(CAST(Owner AS varchar(100)), 30), 0,
       '40' + RIGHT('0000000' + CAST(No AS varchar(7)), 7), 'DST', CASE WHEN No = 5 THEN 'Chirchiq' ELSE 'Toshkent' END
FROM #c ORDER BY No;
UPDATE c SET ID = x.ID FROM #c c JOIN dbo.CUSTOMERS x ON x.DUNS = '40' + RIGHT('0000000' + CAST(c.No AS varchar(7)), 7) AND x.Type = 'DST';
INSERT INTO dst.CUSTOMER_PROFILES (CustomerID, INN, Phone2, OwnerName, Landmark, Lat, Lng, PhotoKey, PointType, Category, TerritoryID, AgentUserID,
                                   PriceTypeID, VisitDays, CreditLimit, PaymentDays, IsApproved, ApprovedBy, ApprovedAt, CreatedVia, CreatedBy, CreatedAt)
SELECT c.ID, NULL, NULL, c.Owner, c.Landmark, c.Lat, c.Lng, NULL, c.PointType, c.Cat, c.Territory, a.UserID,
       @ptWholesale, c.VisitDays, 50000000, 14, 1, @u, DATEADD(day, -40, @t0), 'web', @u, DATEADD(day, -40, @t0)
FROM #c c JOIN #ag a ON a.No = c.AgentNo;

/* ---------------- mahsulotlar, narxlar, distributsiya uchun kirim ---------------- */
CREATE TABLE #p (ID int PRIMARY KEY, PNo varchar(15) COLLATE DATABASE_DEFAULT, UnitID int, Price decimal(19,2), Cost decimal(19,6), Weight decimal(18,4), Qty decimal(18,4));
INSERT INTO #p (ID, PNo, UnitID, Price, Cost, Weight, Qty)
SELECT p.ID, p.PNo, p.UnitID, p.Price, ROUND(p.Price * 0.7, 0), x.Weight, CASE WHEN p.DailyQty >= 600 THEN 3000 ELSE 1200 END
FROM demo_tmp.PROD p JOIN dbo.PRODUCTS x ON x.ID = p.ID
WHERE p.PNo IN ('TM-001','TM-002','TM-003','TM-004','TM-005','TM-006','TM-007','TM-008');

INSERT INTO dst.PRODUCT_PROFILES (ProductID, NameRu, BrandID, ManufacturerID, SubCategoryID, IsSaleActive, IsBulky, IsWeighted, BaseWeightKg, BaseVolumeM3)
SELECT p.ID, NULL, NULL, NULL, NULL, 1, 0, 0, p.Weight, NULL FROM #p p
WHERE NOT EXISTS (SELECT 1 FROM dst.PRODUCT_PROFILES x WHERE x.ProductID = p.ID);
INSERT INTO dst.PRICE_LIST_ITEMS (PriceTypeID, ProductID, Price, MinPrice, ValidFrom, CreatedBy, CreatedAt)
SELECT @ptWholesale, ID, Price, ROUND(Price * 0.95, 0), DATEADD(day, -45, @t0), @u, DATEADD(day, -45, @t0) FROM #p
UNION ALL
SELECT @ptRetail, ID, ROUND(Price * 1.2, 0), NULL, DATEADD(day, -45, @t0), @u, DATEADD(day, -45, @t0) FROM #p WHERE @ptRetail IS NOT NULL;

DECLARE @recAt datetime = DATEADD(hour, 9, CAST(DATEADD(day, -35, @t) AS datetime));
INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
SELECT CONVERT(char(6), @recAt, 112) + '_DST', @wh, SUM(Qty), SUM(Qty * Cost), 0, 'Distributsiya uchun tovar kirimi', @recAt, NULL, 1, @uzs, NULL, NULL FROM #p;
DECLARE @recInc int = SCOPE_IDENTITY();
INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
SELECT @recInc, ID, Qty, Cost, NULL, UnitID, NULL FROM #p;

/* ---------------- buyurtmalar ---------------- */
-- St: 0 yangi, 1 tasdiqlangan, 2 yig'ilmoqda, 3 qisman jo'natilgan, 4 jo'natilgan, 5 yetkazilgan, 6 yopilgan, 9 bekor
-- Pay: to'lov ulushi (%), Method/Acc: to'lov usuli va hisob (NULL — agent qo'lidagi naqd)
CREATE TABLE #o (N int PRIMARY KEY, CustNo int, Ago int, St tinyint, PayPct int NULL, Method varchar(10) COLLATE DATABASE_DEFAULT NULL, Acc int NULL,
                 DocNo nvarchar(30) COLLATE DATABASE_DEFAULT NULL, ID int NULL, CustID int NULL, AgentID int NULL, PaymentDays int NULL,
                 At datetime2(0) NULL, ShipAt datetime2(0) NULL, PayAt datetime2(0) NULL, Subtotal decimal(19,2) NULL, Weight decimal(18,3) NULL);
INSERT INTO #o (N, CustNo, Ago, St, PayPct, Method, Acc) VALUES
(1, 1, 25, 6, 100, 'TRANSFER', @bank),
(2, 2, 22, 6, 100, 'CASH', @kassa),
(3, 3, 18, 6, 100, 'CASH', NULL),
(4, 1, 10, 5, 50, 'CARD', @bank),
(5, 4, 12, 5, NULL, NULL, NULL),
(6, 4, 8, 9, NULL, NULL, NULL),
(7, 2, 5, 4, NULL, NULL, NULL),
(8, 5, 6, 4, NULL, NULL, NULL),
(9, 3, 4, 3, NULL, NULL, NULL),
(10, 4, 2, 1, NULL, NULL, NULL),
(11, 5, 1, 1, NULL, NULL, NULL),
(12, 1, 2, 2, NULL, NULL, NULL),
(13, 2, 1, 0, NULL, NULL, NULL),
(14, 3, 0, 0, NULL, NULL, NULL);
UPDATE o SET CustID = c.ID, AgentID = cp.AgentUserID, PaymentDays = cp.PaymentDays,
             At = DATEADD(hour, 10, DATEADD(day, -o.Ago, @t0)),
             ShipAt = CASE WHEN o.St IN (3, 4, 5, 6) THEN DATEADD(hour, 10, DATEADD(day, 1 - o.Ago, @t0)) END
FROM #o o JOIN #c c ON c.No = o.CustNo JOIN dst.CUSTOMER_PROFILES cp ON cp.CustomerID = c.ID;
UPDATE #o SET ShipAt = DATEADD(hour, 3, At) WHERE ShipAt > SYSDATETIME();       -- bugungi buyurtma bugun jo'natiladi
UPDATE #o SET PayAt = DATEADD(day, CASE WHEN St = 6 THEN 5 ELSE 3 END, ShipAt) WHERE PayPct IS NOT NULL;
;WITH x AS (SELECT DocNo, At, ROW_NUMBER() OVER (PARTITION BY YEAR(At) ORDER BY At, N) AS R FROM #o)
UPDATE x SET DocNo = N'D-' + CAST(YEAR(At) AS nvarchar(4)) + N'-' + RIGHT(N'00000' + CAST(R AS nvarchar(10)), 5);

CREATE TABLE #l (K int IDENTITY PRIMARY KEY, N int, Ln int, ProductID int, UnitID int, Qty decimal(18,4), Price decimal(19,2), MinPrice decimal(19,2),
                 Amount decimal(19,2), Cost decimal(19,6), ShipQty decimal(18,4), ID int NULL);
INSERT INTO #l (N, Ln, ProductID, UnitID, Qty, Price, MinPrice, Amount, Cost, ShipQty)
SELECT o.N, ROW_NUMBER() OVER (PARTITION BY o.N ORDER BY p.PNo), p.ID, p.UnitID, q.Qty, p.Price, ROUND(p.Price * 0.95, 0), ROUND(q.Qty * p.Price, 2), p.Cost, 0
FROM #o o
JOIN (VALUES (1,'TM-001',120),(1,'TM-006',400),(2,'TM-002',80),(2,'TM-007',300),(2,'TM-008',150),(3,'TM-004',40),(3,'TM-005',30),
             (4,'TM-001',100),(4,'TM-003',60),(4,'TM-006',500),(5,'TM-002',90),(5,'TM-008',200),(6,'TM-005',50),
             (7,'TM-006',350),(7,'TM-007',250),(8,'TM-001',150),(8,'TM-004',30),(9,'TM-002',70),(9,'TM-003',50),(9,'TM-007',200),
             (10,'TM-006',600),(10,'TM-008',120),(11,'TM-001',80),(11,'TM-005',25),(12,'TM-003',40),(12,'TM-004',35),
             (13,'TM-007',180),(13,'TM-002',60),(14,'TM-006',300)) v (N, PNo, Qty) ON v.N = o.N
JOIN #p p ON p.PNo = v.PNo
CROSS APPLY (SELECT CAST(v.Qty AS decimal(18,4)) AS Qty) q;
-- jo'natilgan miqdor: 4/5/6 — hammasi, 3 — faqat 1-qator
UPDATE l SET ShipQty = CASE WHEN o.St IN (4, 5, 6) THEN l.Qty WHEN o.St = 3 AND l.Ln = 1 THEN l.Qty ELSE 0 END
FROM #l l JOIN #o o ON o.N = l.N;
UPDATE o SET Subtotal = x.S, Weight = x.W
FROM #o o JOIN (SELECT l.N, SUM(l.Amount) AS S, SUM(l.Qty * ISNULL(p.Weight, 0)) AS W FROM #l l JOIN #p p ON p.ID = l.ProductID GROUP BY l.N) x ON x.N = o.N;

MERGE dst.D_ORDERS AS tg
USING #o AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocNo, DocDate, Status, OrderStatus, CustomerID, AgentUserID, WarehouseID, PriceTypeID, DeliveryDate, IsPickup, Comment,
                              Subtotal, DiscountPct, DiscountSum, Total, ShippedTotal, WeightKg, VolumeM3, Source, ClientRequestId,
                              ConfirmedBy, ConfirmedAt, DeliveredAt, CreatedBy, CreatedAt, PostedBy, PostedAt, CancelledBy, CancelledAt, CancelReason)
     VALUES (s.DocNo, CAST(s.At AS date), CASE WHEN s.St = 0 THEN 0 WHEN s.St = 9 THEN 2 ELSE 1 END, s.St, s.CustID, s.AgentID,
             CASE WHEN s.St IN (0, 9) THEN NULL ELSE @wh END, @ptWholesale, CAST(DATEADD(day, 1, s.At) AS date), 0, NULL,
             s.Subtotal, 0, 0, s.Subtotal, 0, ISNULL(s.Weight, 0), 0, 'web', NULL,
             CASE WHEN s.St IN (0, 9) THEN NULL ELSE @u END, CASE WHEN s.St IN (0, 9) THEN NULL ELSE DATEADD(hour, 1, s.At) END,
             CASE WHEN s.St IN (5, 6) THEN DATEADD(hour, 6, s.ShipAt) END,
             ISNULL(s.AgentID, @u), s.At,
             CASE WHEN s.St IN (0, 9) THEN NULL ELSE @u END, CASE WHEN s.St IN (0, 9) THEN NULL ELSE DATEADD(hour, 1, s.At) END,
             CASE WHEN s.St = 9 THEN @u END, CASE WHEN s.St = 9 THEN DATEADD(hour, 2, s.At) END,
             CASE WHEN s.St = 9 THEN N'Mijoz buyurtmani bekor qildi (qayta hisob-kitob)' END)
OUTPUT s.N, inserted.ID INTO #map (K, ID);
UPDATE o SET ID = m.ID FROM #o o JOIN #map m ON m.K = o.N;
TRUNCATE TABLE #map;

MERGE dst.D_ORDER_LINES AS tg
USING (SELECT l.*, o.ID AS OrderID FROM #l l JOIN #o o ON o.N = l.N) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (OrderID, [LineNo], ProductID, ProductUnitID, UnitID, Factor, Qty, QtyBase, UnitPrice, MinUnitPrice, PriceSource,
                              DiscountPct, DiscountSum, Amount, QtyShippedBase)
     VALUES (s.OrderID, s.Ln, s.ProductID, NULL, s.UnitID, 1, s.Qty, s.Qty, s.Price, s.MinPrice, 2, 0, 0, s.Amount, s.ShipQty)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE l SET ID = m.ID FROM #l l JOIN #map m ON m.K = l.K;
TRUNCATE TABLE #map;

-- holatlar jurnali: har buyurtma o'z zanjiri bo'yicha
INSERT INTO dst.D_ORDER_STATUS_LOG (OrderID, FromStatus, ToStatus, UserID, At, Note)
SELECT o.ID, s.F, s.T, CASE WHEN s.F IS NULL THEN ISNULL(o.AgentID, @u) ELSE @u END,
       CASE s.Step WHEN 0 THEN o.At WHEN 1 THEN DATEADD(hour, 1, o.At) WHEN 2 THEN DATEADD(hour, 2, o.At) WHEN 3 THEN o.ShipAt
                   WHEN 4 THEN DATEADD(hour, 6, o.ShipAt) ELSE o.PayAt END, NULL
FROM #o o
CROSS APPLY (
    SELECT * FROM (VALUES (0, CAST(NULL AS tinyint), CAST(0 AS tinyint)), (1, 0, 1), (2, 1, 2), (3, 1, 3), (3, 1, 4), (4, 4, 5), (5, 5, 6), (2, 0, 9)) v (Step, F, T)
    WHERE v.Step = 0
       OR (v.T = 1 AND o.St IN (1, 2, 3, 4, 5, 6))
       OR (v.T = 2 AND o.St = 2)
       OR (v.T = 3 AND o.St = 3)
       OR (v.T = 4 AND o.St IN (4, 5, 6))
       OR (v.T = 5 AND o.St IN (5, 6))
       OR (v.T = 6 AND o.St = 6)
       OR (v.T = 9 AND o.St = 9)) s
ORDER BY o.N, s.Step, s.T;

-- rezerv: tasdiqlangan / yig'ilmoqda / qisman jo'natilgan — jo'natilmagan qoldiq
INSERT INTO dst.STOCK_RESERVATIONS (WarehouseID, ProductID, OrderLineID, QtyBase, CreatedAt)
SELECT @wh, l.ProductID, l.ID, l.Qty - l.ShipQty, DATEADD(hour, 1, o.At)
FROM #l l JOIN #o o ON o.N = l.N
WHERE o.St IN (1, 2, 3) AND l.Qty - l.ShipQty > 0;

/* ---------------- jo'natishlar: dst.D_SHIPMENTS + ombor chiqimi + mijoz qarzi ---------------- */
CREATE TABLE #s (N int PRIMARY KEY, DocNo nvarchar(30) COLLATE DATABASE_DEFAULT, ShipAt datetime2(0), Total decimal(19,2), Cost decimal(19,2), ExpID int NULL, ID int NULL);
INSERT INTO #s (N, ShipAt, Total, Cost)
SELECT o.N, o.ShipAt, SUM(ROUND(l.Amount * l.ShipQty / l.Qty, 2)), SUM(ROUND(l.ShipQty * l.Cost, 2))
FROM #o o JOIN #l l ON l.N = o.N WHERE l.ShipQty > 0 GROUP BY o.N, o.ShipAt;
;WITH x AS (SELECT DocNo, ShipAt, ROW_NUMBER() OVER (PARTITION BY YEAR(ShipAt) ORDER BY ShipAt, N) AS R FROM #s)
UPDATE x SET DocNo = N'DJ-' + CAST(YEAR(ShipAt) AS nvarchar(4)) + N'-' + RIGHT(N'00000' + CAST(R AS nvarchar(10)), 5);

MERGE dbo.EXPENSES AS tg
USING (SELECT s.*, o.DocNo AS OrderNo, o.ID AS OrderID, x.Q FROM #s s JOIN #o o ON o.N = s.N
       CROSS APPLY (SELECT SUM(ShipQty) AS Q FROM #l WHERE N = s.N) x) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocNo, Amount, TotalPrice, ReceiverWhID, SenderWHID, IsDeleted, Description, IssueDateTime, SendStatus, CurrencyID, ProductionID)
     VALUES (CAST(s.DocNo AS varchar(50)), s.Q, s.Cost, NULL, @wh, 0,
             'Distributsiya: ' + CAST(s.OrderNo AS varchar(30)) + ' → ' + CAST(s.DocNo AS varchar(30)) + ' URL: /Distribution/DstOrders/Details/' + CAST(s.OrderID AS varchar(12)),
             CAST(s.ShipAt AS datetime), 1, @uzs, NULL)
OUTPUT s.N, inserted.ID INTO #map (K, ID);
UPDATE s SET ExpID = m.ID FROM #s s JOIN #map m ON m.K = s.N;
TRUNCATE TABLE #map;
INSERT INTO dbo.EXPENSE_PRODUCTS (ExpenseID, ProductID, Amount, PiecePrice, Comment, UnitID, LotID, FromLocationID, AllocationPolicy, ViolationReasonCode)
SELECT s.ExpID, l.ProductID, l.ShipQty, l.Cost, CAST(o.DocNo AS varchar(30)) + ' #' + CAST(l.Ln AS varchar(10)), l.UnitID, NULL, NULL, NULL, NULL
FROM #s s JOIN #l l ON l.N = s.N AND l.ShipQty > 0 JOIN #o o ON o.N = s.N;

MERGE dst.D_SHIPMENTS AS tg
USING (SELECT s.*, o.ID AS OrderID FROM #s s JOIN #o o ON o.N = s.N) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocNo, DocDate, Status, OrderID, WarehouseID, ExpenseID, Total, CostTotal, IsPickup, CreatedBy, CreatedAt, PostedBy, PostedAt)
     VALUES (s.DocNo, CAST(s.ShipAt AS date), 1, s.OrderID, @wh, s.ExpID, s.Total, s.Cost, 0, @u, s.ShipAt, @u, s.ShipAt)
OUTPUT s.N, inserted.ID INTO #map (K, ID);
UPDATE s SET ID = m.ID FROM #s s JOIN #map m ON m.K = s.N;
TRUNCATE TABLE #map;
INSERT INTO dst.D_SHIPMENT_LINES (ShipmentID, OrderLineID, ProductID, QtyBase, Amount, CostAmount)
SELECT s.ID, l.ID, l.ProductID, l.ShipQty, ROUND(l.Amount * l.ShipQty / l.Qty, 2), ROUND(l.ShipQty * l.Cost, 2)
FROM #s s JOIN #l l ON l.N = s.N AND l.ShipQty > 0;
UPDATE d SET ShippedTotal = s.Total FROM dst.D_ORDERS d JOIN #o o ON o.ID = d.ID JOIN #s s ON s.N = o.N;

INSERT INTO dst.CUSTOMER_LEDGER (CustomerID, EntryDate, DocType, DocID, OrderID, Debit, Credit, DueDate, AgentUserID, Note, CreatedBy)
SELECT o.CustID, s.ShipAt, 'SHIPMENT', s.ID, o.ID, s.Total, 0, CAST(DATEADD(day, o.PaymentDays, s.ShipAt) AS date), o.AgentID, s.DocNo, @u
FROM #s s JOIN #o o ON o.N = s.N;

/* ---------------- to'lovlar ---------------- */
CREATE TABLE #pay (N int PRIMARY KEY, DocNo nvarchar(30) COLLATE DATABASE_DEFAULT, Amount decimal(19,2), ID int NULL, FinID int NULL);
INSERT INTO #pay (N, Amount)
SELECT o.N, ROUND(s.Total * o.PayPct / 100.0, 0) FROM #o o JOIN #s s ON s.N = o.N WHERE o.PayPct IS NOT NULL;
UPDATE p SET Amount = s.Total FROM #pay p JOIN #s s ON s.N = p.N JOIN #o o ON o.N = p.N WHERE o.PayPct = 100;
;WITH x AS (SELECT p.DocNo, o.PayAt, ROW_NUMBER() OVER (PARTITION BY YEAR(o.PayAt) ORDER BY o.PayAt, p.N) AS R FROM #pay p JOIN #o o ON o.N = p.N)
UPDATE x SET DocNo = N'DP-' + CAST(YEAR(PayAt) AS nvarchar(4)) + N'-' + RIGHT(N'00000' + CAST(R AS nvarchar(10)), 5);

MERGE dst.D_PAYMENTS AS tg
USING (SELECT p.*, o.ID AS OrderID, o.CustID, o.AgentID, o.PayAt, o.Method, o.Acc FROM #pay p JOIN #o o ON o.N = p.N) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocNo, DocDate, Status, CustomerID, OrderID, Amount, Method, ReceivedByUserID, FinAccountID, HandoverID, FinTransactionID,
                              Note, Source, ClientRequestId, CreatedBy, CreatedAt, PostedBy, PostedAt)
     VALUES (s.DocNo, CAST(s.PayAt AS date), 1, s.CustID, s.OrderID, s.Amount, s.Method, CASE WHEN s.Acc IS NULL THEN ISNULL(s.AgentID, @u) ELSE @u END,
             s.Acc, NULL, NULL, NULL, 'web', NULL, @u, s.PayAt, @u, s.PayAt)
OUTPUT s.N, inserted.ID INTO #map (K, ID);
UPDATE p SET ID = m.ID FROM #pay p JOIN #map m ON m.K = p.N;
TRUNCATE TABLE #map;

-- hisobga tushgan to'lov — moliya yozuvi (PaymentService.Receive)
MERGE dbo.FIN_TRANSACTIONS AS tg
USING (SELECT p.*, o.Acc, o.PayAt, o.Method, o.CustID, c.Name AS CustName FROM #pay p JOIN #o o ON o.N = p.N JOIN dbo.CUSTOMERS c ON c.ID = o.CustID
       WHERE o.Acc IS NOT NULL) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                              CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
     VALUES (CAST(s.PayAt AS date), s.Acc, @catSales, 1, s.Amount, @uzs, NULL, NULL, 1, s.CustID, s.CustName,
             N'Distributsiya to''lovi ' + s.DocNo, 'DstPayment', s.ID, NULL, s.Method, @u, CAST(s.PayAt AS datetime), 0)
OUTPUT s.N, inserted.ID INTO #map (K, ID);
UPDATE dp SET FinTransactionID = m.ID FROM dst.D_PAYMENTS dp JOIN #pay p ON p.ID = dp.ID JOIN #map m ON m.K = p.N;
TRUNCATE TABLE #map;

INSERT INTO dst.CUSTOMER_LEDGER (CustomerID, EntryDate, DocType, DocID, OrderID, Debit, Credit, DueDate, AgentUserID, Note, CreatedBy)
SELECT o.CustID, o.PayAt, 'PAYMENT', p.ID, o.ID, 0, p.Amount, NULL, o.AgentID, p.DocNo, @u
FROM #pay p JOIN #o o ON o.N = p.N;

-- tekshiruv: muddati o'tgan qarz bo'lmasin, rezerv qoldiqdan oshmasin
IF EXISTS (SELECT 1 FROM dst.CUSTOMER_LEDGER WHERE DocType = 'SHIPMENT' AND DueDate < @t AND OrderID IN (SELECT ID FROM #o WHERE St IN (4, 5)))
    THROW 50292, 'Demo distributsiya: ochiq jo''natish qarzi muddati o''tib ketdi', 1;
IF EXISTS (SELECT 1 FROM #p p
           WHERE p.Qty - ISNULL((SELECT SUM(ShipQty) FROM #l WHERE ProductID = p.ID), 0)
                 < ISNULL((SELECT SUM(r.QtyBase) FROM dst.STOCK_RESERVATIONS r WHERE r.ProductID = p.ID AND r.WarehouseID = @wh), 0))
    THROW 50293, 'Demo distributsiya: rezerv qoldiqdan oshdi', 1;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #map, #ag, #c, #p, #o, #l, #s, #pay;

PRINT 'Demo distributsiya: OK';
