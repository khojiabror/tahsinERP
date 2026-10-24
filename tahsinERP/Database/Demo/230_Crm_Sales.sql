/* =====================================================================
   tahsinERP — DEMO: Sotuvlar (CRM) — mijozlar va sotuv zanjiri (220_Mes_Production.sql dan keyin).

   S_CONTRACTS ('DP-NN/yyyy') -> S_ORDERS ('ZK-yyyy-NNN') -> SALES_INVOICES ('INV_NNNN', QQS 12%, APPROVED)
   -> sotuv omboridan chiqim (WarehouseFlowService.DecreaseSalesStock); ochiq buyurtmalar + yetkazish jadvali.
   Haftalik sotuv = oldingi hafta sotuvga tayyor mahsulotining 85% (demo_tmp.FTL). Ishlab chiqarish (Mes) tanlanmagan bo'lsa
   sotuv omboriga tayyor mahsulot boshlang'ich qoldig'i kirimi yoziladi (tovar sotuvchi korxona ko'rinishi).

   Faqat Crm moduli tanlangan bo'lsa. Idempotent: SALES_INVOICES bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Crm') = 0 OR OBJECT_ID(N'demo_tmp.FTL') IS NULL OR EXISTS (SELECT 1 FROM dbo.SALES_INVOICES)
BEGIN
    PRINT 'Demo sotuv: modul tanlanmagan yoki hujjatlar bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @start date = DATEADD(day, -56, @t);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @whT int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SALES' AND ISNULL(IsDeleted, 0) = 0);
IF @u IS NULL OR @whT IS NULL THROW 50121, 'Demo sotuv: demo user yoki SALES ombori topilmadi', 1;

/* ---------------- mijozlar (IsDeleted = 0) ---------------- */
INSERT INTO dbo.CUSTOMERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Uy-Ro''zg''or Savdo" MChJ', 'Chilonzor tumani, Bunyodkor ko''chasi 45', 'O''zbekiston', '+998 90 111-22-33', 'savdo@uyrozgor.uz', 'Rahimov Bobur', 'Rahimov Anvar', 0, '301111111', 'MChJ', 'Toshkent'),
       ('"Bozor Plus" MChJ',          'Registon ko''chasi 8',                     'O''zbekiston', '+998 91 222-33-44', 'info@bozorplus.uz', 'Saidova Malika', 'Saidov Jamshid', 0, '302222222', 'MChJ', 'Samarqand'),
       ('"Orzu Market" XK',           'Mustaqillik ko''chasi 17',                 'O''zbekiston', '+998 93 333-44-55', 'orzu.market@mail.uz', 'Tursunov Ulug''bek', 'Tursunova Dilnoza', 0, '303333333', 'XK', 'Farg''ona'),
       ('"Mega Opt" MChJ',            'Bobur shoh ko''chasi 102',                 'O''zbekiston', '+998 94 444-55-66', 'opt@megaopt.uz', 'Qodirov Sherzod', 'Qodirov Farhod', 0, '304444444', 'MChJ', 'Andijon'),
       ('"Navro''z Trade" MChJ',      'B.Naqshband ko''chasi 21',                 'O''zbekiston', '+998 95 555-66-77', 'trade@navroz.uz', 'Ergasheva Nodira', 'Ergashev Rustam', 0, '305555555', 'MChJ', 'Buxoro');

CREATE TABLE #prod (ID int PRIMARY KEY, PNo varchar(15) COLLATE DATABASE_DEFAULT, Name varchar(50) COLLATE DATABASE_DEFAULT, UnitID int, IsRaw bit,
                    Price decimal(18,2), DailyQty int NULL);
INSERT INTO #prod SELECT ID, PNo, Name, UnitID, IsRaw, Price, DailyQty FROM demo_tmp.PROD;
CREATE TABLE #cust (No int PRIMARY KEY, ID int, Name varchar(50) COLLATE DATABASE_DEFAULT);
INSERT INTO #cust SELECT CAST(LEFT(DUNS, 3) AS int) - 300, ID, Name FROM dbo.CUSTOMERS WHERE DUNS IN ('301111111','302222222','303333333','304444444','305555555');
CREATE TABLE #ftl (Wk int, ProductID int, Qty decimal(18,4));
INSERT INTO #ftl SELECT Wk, ProductID, Qty FROM demo_tmp.FTL;
CREATE TABLE #map (K int, ID int);

/* ---------------- ishlab chiqarish bo'lmasa: sotuv omboriga boshlang'ich qoldiq (tannarx ~ sotuv narxining 70%) ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.INCOMES i JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID JOIN #prod p ON p.ID = ip.ProductID AND p.IsRaw = 0
               WHERE i.ReceivingWHID = @whT AND i.RecieveStatus = 1 AND ISNULL(i.IsDeleted, 0) = 0)
BEGIN
    DECLARE @openAt datetime = DATEADD(hour, 9, CAST(DATEADD(day, -1, @start) AS datetime));
    INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
    SELECT CONVERT(char(6), @openAt, 112) + '_BQ-T', @whT, SUM(f.Qty), SUM(f.Qty * ROUND(p.Price * 0.7, 0)), 0, 'Boshlang''ich qoldiq (tayyor mahsulot)',
           @openAt, NULL, 1, @uzs, NULL, NULL
    FROM #ftl f JOIN #prod p ON p.ID = f.ProductID;
    DECLARE @openInc int = SCOPE_IDENTITY();
    INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
    SELECT @openInc, f.ProductID, SUM(f.Qty), ROUND(p.Price * 0.7, 0), N'Boshlang''ich qoldiq', p.UnitID, NULL
    FROM #ftl f JOIN #prod p ON p.ID = f.ProductID GROUP BY f.ProductID, p.Price, p.UnitID;
END

/* ---------------- sotuv: shartnoma -> buyurtma -> invoys -> sotuv omboridan chiqim ---------------- */
-- mijoz -> mahsulotlar; har hafta har mijozga invoys; haftalik sotuv = oldingi hafta tayyor mahsulotining 85%
CREATE TABLE #cp (CustNo int, ProductID int, Price decimal(18,2));
INSERT INTO #cp (CustNo, ProductID, Price)
SELECT v.CustNo, p.ID, CASE WHEN v.CustNo = 4 THEN ROUND(p.Price * 0.95, 0) ELSE p.Price END
FROM (VALUES (1,'TM-001'),(1,'TM-002'),(1,'TM-003'),(1,'TM-006'),(1,'TM-007'),
             (2,'TM-002'),(2,'TM-004'),(2,'TM-005'),(2,'TM-009'),(2,'TM-010'),
             (3,'TM-006'),(3,'TM-007'),(3,'TM-008'),(3,'TM-010'),(3,'TM-011'),
             (4,'TM-001'),(4,'TM-003'),(4,'TM-004'),(4,'TM-008'),(4,'TM-012'),
             (5,'TM-005'),(5,'TM-009'),(5,'TM-011'),(5,'TM-012'),(5,'TM-002')) v (CustNo, PNo)
JOIN #prod p ON p.PNo = v.PNo;

CREATE TABLE #sl (S int, CustNo int, ProductID int, Qty decimal(18,3), Price decimal(18,2), UnitID int);
;WITH buy AS (
    SELECT s.S, cp.CustNo, cp.ProductID, cp.Price
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7)) s (S) CROSS JOIN #cp cp),
cnt AS (SELECT S, ProductID, COUNT(*) AS N FROM buy GROUP BY S, ProductID)
INSERT INTO #sl (S, CustNo, ProductID, Qty, Price, UnitID)
SELECT b.S, b.CustNo, b.ProductID, FLOOR(l.Qty * 0.85 / c.N / 10) * 10, b.Price, p.UnitID
FROM buy b JOIN cnt c ON c.S = b.S AND c.ProductID = b.ProductID
JOIN #ftl l ON l.Wk = b.S - 1 AND l.ProductID = b.ProductID
JOIN #prod p ON p.ID = b.ProductID
WHERE FLOOR(l.Qty * 0.85 / c.N / 10) * 10 > 0;

CREATE TABLE #si (K int IDENTITY PRIMARY KEY, S int, CustNo int, D date, InvoiceNo nvarchar(100) COLLATE DATABASE_DEFAULT, OrderNo varchar(50) COLLATE DATABASE_DEFAULT, OrderID int NULL, InvID bigint NULL);
INSERT INTO #si (S, CustNo, D)
SELECT S, CustNo, DATEADD(day, 7 * S + (CustNo % 5) + 1, @start) FROM #sl GROUP BY S, CustNo ORDER BY DATEADD(day, 7 * S + (CustNo % 5) + 1, @start), CustNo;
;WITH o AS (SELECT K, ROW_NUMBER() OVER (ORDER BY D, CustNo) AS N FROM #si)
UPDATE si SET InvoiceNo = 'INV_' + RIGHT('0000' + CAST(o.N AS varchar(10)), 4) FROM #si si JOIN o ON o.K = si.K;

-- ochiq buyurtmalar (invoys hali yo'q) + yetkazish jadvali
CREATE TABLE #oo (CustNo int, D date, ProductID int, Qty decimal(18,3), Price decimal(18,2), UnitID int, OrderID int NULL, SOPID int NULL);
INSERT INTO #oo (CustNo, D, ProductID, Qty, Price, UnitID)
SELECT cp.CustNo, DATEADD(day, -2 * cp.CustNo, @t), cp.ProductID, CEILING(p.DailyQty * 0.6 / 10) * 10, cp.Price, p.UnitID
FROM #cp cp JOIN #prod p ON p.ID = cp.ProductID
WHERE cp.CustNo <= 3 AND cp.ProductID IN (SELECT TOP 3 x.ProductID FROM #cp x WHERE x.CustNo = cp.CustNo ORDER BY x.ProductID);

-- shartnomalar: mijozga bitta, miqdor = buyurtmalar x 1.3; 5-mijoz shartnomasi muddati o'tgan
CREATE TABLE #sc (CustNo int PRIMARY KEY, ContractNo varchar(50) COLLATE DATABASE_DEFAULT, Issued date, Due date, ID int NULL);
INSERT INTO #sc (CustNo, ContractNo, Issued, Due)
SELECT No, 'DP-' + RIGHT('00' + CAST(No AS varchar(2)), 2) + '/' + CAST(YEAR(DATEADD(day, CASE WHEN No = 5 THEN -70 ELSE -110 END, @t)) AS varchar(4)),
       DATEADD(day, CASE WHEN No = 5 THEN -70 ELSE -110 END, @t),
       DATEADD(day, CASE WHEN No = 5 THEN -1 ELSE 255 END, @t)
FROM #cust;
INSERT INTO dbo.S_CONTRACTS (ContractNo, IssuedDate, CompanyID, CustomerID, Amount, Incoterms, PaymentTerms, DueDate, IsDeleted, CurrencyID)
SELECT sc.ContractNo, sc.Issued, 1, c.ID, 0, 'EXW', CASE WHEN sc.CustNo >= 4 THEN '15 kun ichida' ELSE '30 kun ichida' END, sc.Due, 0, @uzs
FROM #sc sc JOIN #cust c ON c.No = sc.CustNo;
UPDATE sc SET ID = s.ID FROM #sc sc JOIN dbo.S_CONTRACTS s ON s.ContractNo = sc.ContractNo;

INSERT INTO dbo.S_CONTRACT_PRODUCTS (ContractID, ProductID, PiecePrice, Amount, UnitID, vatamount)
SELECT sc.ID, cp.ProductID, cp.Price, q.Qty, p.UnitID, cp.Price * q.Qty * 0.12
FROM #cp cp JOIN #sc sc ON sc.CustNo = cp.CustNo JOIN #prod p ON p.ID = cp.ProductID
CROSS APPLY (SELECT CEILING((ISNULL((SELECT SUM(Qty) FROM #sl x WHERE x.CustNo = cp.CustNo AND x.ProductID = cp.ProductID), 0)
                            + ISNULL((SELECT SUM(Qty) FROM #oo x WHERE x.CustNo = cp.CustNo AND x.ProductID = cp.ProductID), 0)) * 1.3 / 100 + 1) * 100 AS Qty) q;
UPDATE c SET Amount = ROUND(x.Amt, 0)
FROM dbo.S_CONTRACTS c
JOIN (SELECT ContractID, SUM(PiecePrice * Amount + vatamount) AS Amt FROM dbo.S_CONTRACT_PRODUCTS GROUP BY ContractID) x ON x.ContractID = c.ID
WHERE c.ID IN (SELECT ID FROM #sc);

-- buyurtmalar: invoysdan 2 kun oldin; ochiq buyurtmalar — so'nggi kunlarda
UPDATE #si SET OrderNo = 'ZK-' + CAST(YEAR(DATEADD(day, -2, D)) AS varchar(4)) + '-' + RIGHT('000' + CAST(K AS varchar(10)), 3);
INSERT INTO dbo.S_ORDERS (OrderNo, IssuedDate, CompanyID, CustomerID, ContractID, Amount, Description, IsDeleted, CurrencyID)
SELECT si.OrderNo, DATEADD(day, -2, CAST(si.D AS datetime)), 1, c.ID, sc.ID, x.Amt, N'Haftalik buyurtma', 0, @uzs
FROM #si si JOIN #cust c ON c.No = si.CustNo JOIN #sc sc ON sc.CustNo = si.CustNo
CROSS APPLY (SELECT SUM(l.Price * l.Qty * 1.12) AS Amt FROM #sl l WHERE l.S = si.S AND l.CustNo = si.CustNo) x
ORDER BY si.K;
UPDATE si SET OrderID = o.ID FROM #si si JOIN dbo.S_ORDERS o ON o.OrderNo = si.OrderNo;
INSERT INTO dbo.S_ORDER_PRODUCTS (OrderID, ProductID, Price, Amount, MOQ, UnitID)
SELECT si.OrderID, l.ProductID, l.Price, l.Qty, 0, l.UnitID FROM #sl l JOIN #si si ON si.S = l.S AND si.CustNo = l.CustNo;

DECLARE @ooBase int = (SELECT COUNT(*) FROM #si);
INSERT INTO dbo.S_ORDERS (OrderNo, IssuedDate, CompanyID, CustomerID, ContractID, Amount, Description, IsDeleted, CurrencyID)
SELECT 'ZK-' + CAST(YEAR(o.D) AS varchar(4)) + '-' + RIGHT('000' + CAST(@ooBase + ROW_NUMBER() OVER (ORDER BY o.D DESC) AS varchar(10)), 3),
       CAST(o.D AS datetime), 1, c.ID, sc.ID, SUM(o.Price * o.Qty * 1.12), N'Yetkazish jadvali bo''yicha buyurtma', 0, @uzs
FROM #oo o JOIN #cust c ON c.No = o.CustNo JOIN #sc sc ON sc.CustNo = o.CustNo
GROUP BY o.CustNo, o.D, c.ID, sc.ID;
UPDATE o SET OrderID = so.ID
FROM #oo o JOIN #cust c ON c.No = o.CustNo
JOIN dbo.S_ORDERS so ON so.CustomerID = c.ID AND CAST(so.IssuedDate AS date) = o.D AND so.ID NOT IN (SELECT OrderID FROM #si);
INSERT INTO dbo.S_ORDER_PRODUCTS (OrderID, ProductID, Price, Amount, MOQ, UnitID)
SELECT OrderID, ProductID, Price, Qty, 0, UnitID FROM #oo;
UPDATE o SET SOPID = sp.ID FROM #oo o JOIN dbo.S_ORDER_PRODUCTS sp ON sp.OrderID = o.OrderID AND sp.ProductID = o.ProductID;
-- 3-mijoz jadvali muddati o'tgan (CRM: yetkazish kechikkan)
INSERT INTO dbo.SORDER_DELIVERY_SCHEDULE (SOrderProductID, SOrderID, ProductID, DeliveryDate, QtyPlanned, QtyShipped, StatusCode, CreatedByUserID, CreatedDT)
SELECT SOPID, OrderID, ProductID, DATEADD(day, 4, D), Qty, 0, 'PLANNED', @u, CAST(D AS datetime2) FROM #oo;

-- invoyslar (SalesInvoiceService.Create: StatusCode APPROVED, narx/QQS shartnomadan)
INSERT INTO dbo.SALES_INVOICES (InvoiceNo, InvoiceDate, CustomerID, ContractID, OrderID, TotalAmount, TotalVAT, DueDate, StatusCode, CreatedByUserID, CreatedDT, UpdatedDT, CurrencyID)
SELECT si.InvoiceNo, si.D, c.ID, sc.ID, si.OrderID, x.Net, x.Vat, DATEADD(day, CASE WHEN si.CustNo >= 4 THEN 15 ELSE 30 END, si.D), 'APPROVED', @u,
       DATEADD(hour, 11, CAST(si.D AS datetime2)), NULL, @uzs
FROM #si si JOIN #cust c ON c.No = si.CustNo JOIN #sc sc ON sc.CustNo = si.CustNo
CROSS APPLY (SELECT SUM(l.Qty * l.Price) AS Net, SUM(ROUND(l.Qty * l.Price * 0.12, 2)) AS Vat FROM #sl l WHERE l.S = si.S AND l.CustNo = si.CustNo) x
ORDER BY si.K;
UPDATE si SET InvID = i.ID FROM #si si JOIN dbo.SALES_INVOICES i ON i.InvoiceNo = si.InvoiceNo;

INSERT INTO dbo.SALES_INVOICE_LINES (InvoiceID, [LineNo], ProductID, Description, Qty, UnitPrice, VATRate, VATAmount, WarehouseOutID)
SELECT si.InvID, ROW_NUMBER() OVER (PARTITION BY si.InvID ORDER BY l.ProductID), l.ProductID, NULL, l.Qty, l.Price, 12, ROUND(l.Qty * l.Price * 0.12, 2), NULL
FROM #sl l JOIN #si si ON si.S = l.S AND si.CustNo = l.CustNo;

-- WarehouseFlowService.DecreaseSalesStock: har qatorga bitta EXPENSES (narx — shartnoma narxi)
CREATE TABLE #se (K int IDENTITY PRIMARY KEY, InvID bigint, InvoiceNo nvarchar(100) COLLATE DATABASE_DEFAULT, D date, ProductID int, PNo varchar(15) COLLATE DATABASE_DEFAULT, Qty decimal(18,3), Price decimal(18,2), UnitID int, ExpID int NULL);
INSERT INTO #se (InvID, InvoiceNo, D, ProductID, PNo, Qty, Price, UnitID)
SELECT si.InvID, si.InvoiceNo, si.D, l.ProductID, p.PNo, l.Qty, l.Price, l.UnitID
FROM #sl l JOIN #si si ON si.S = l.S AND si.CustNo = l.CustNo JOIN #prod p ON p.ID = l.ProductID
ORDER BY si.D, si.InvID, l.ProductID;
MERGE dbo.EXPENSES AS tg
USING #se AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocNo, Amount, TotalPrice, ReceiverWhID, SenderWHID, IsDeleted, Description, IssueDateTime, SendStatus, CurrencyID, ProductionID)
     VALUES (CONVERT(char(8), s.D, 112) + '_' + s.PNo + '_' + FORMAT(s.Qty, '0.##'), s.Qty, s.Qty * s.Price, NULL, @whT, 0,
             CONVERT(char(10), s.D, 104) + 'y. sotuv invoysi asosida tayyor mahsulot chiqimi: ' + s.PNo + ', Miqdor ' + FORMAT(s.Qty, '0.##')
               + '. Invoys raqami: ' + s.InvoiceNo + '. URL: /SalesInvoices/Details/' + CAST(s.InvID AS varchar(20)),
             CAST(s.D AS datetime), 1, @uzs, NULL)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE s SET ExpID = m.ID FROM #se s JOIN #map m ON m.K = s.K;
TRUNCATE TABLE #map;
INSERT INTO dbo.EXPENSE_PRODUCTS (ExpenseID, ProductID, Amount, PiecePrice, Comment, UnitID, LotID, FromLocationID, AllocationPolicy, ViolationReasonCode)
SELECT ExpID, ProductID, Qty, Price, 'Invoys raqami: ' + InvoiceNo + '. URL: /SalesInvoices/Details/' + CAST(InvID AS varchar(20)), UnitID, NULL, NULL, NULL, NULL
FROM #se;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #prod, #cust, #ftl, #map, #cp, #sl, #si, #oo, #sc, #se;

PRINT 'Demo sotuv: OK';
