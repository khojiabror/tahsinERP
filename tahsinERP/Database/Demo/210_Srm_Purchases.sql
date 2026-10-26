/* =====================================================================
   tahsinERP — DEMO: Ta'minot (SRM) — ta'minotchilar va xarid zanjiri (200_Core_Master.sql dan keyin).

   Hujjatlar servislar yozadigan shaklda (raqam formatlari, tavsiflar, statuslar):
     P_CONTRACTS (PC-{yyyy}-NNNN) -> P_ORDERS (PO-yyyy-MM-NNNN) -> P_INVOICES (PI-yyyy-NNNN) -> INCOMES ('{yyyyMM}_{n}',
     SUPPLY ombori, IncomesController). Miqdorlar demo_tmp.PR (ishlab chiqarish ehtiyojining ~130%) dan.
   Faqat Srm moduli tanlangan bo'lsa. Idempotent: P_INVOICES bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Srm') = 0 OR OBJECT_ID(N'demo_tmp.PR') IS NULL OR EXISTS (SELECT 1 FROM dbo.P_INVOICES)
BEGIN
    PRINT 'Demo ta''minot: modul tanlanmagan yoki hujjatlar bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @whS int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SUPPLY' AND ISNULL(IsDeleted, 0) = 0);
IF @whS IS NULL THROW 50111, 'Demo ta''minot: SUPPLY ombori topilmadi', 1;

/* ---------------- ta'minotchilar (IsDeleted = 0 — ro'yxatlar IsDeleted == false bilan) ---------------- */
INSERT INTO dbo.SUPPLIERS (Name, Address, Country, Telephone, Email, ContactPersonName, DirectorName, IsDeleted, DUNS, Type, City)
VALUES ('"Sho''rtan Polimer Savdo" MChJ', 'Sanoat zonasi, 3-uchastka',  'O''zbekiston', '+998 75 521-10-10', 'sales@shortanpolimer.uz', 'Normatov Ilhom', 'Normatov Akmal', 0, '311111111', 'Granula', 'Qarshi'),
       ('"Polietilen Invest" MChJ',      'Olmazor tumani, Sanoat 5',   'O''zbekiston', '+998 71 245-20-20', 'info@pe-invest.uz', 'Yusupov Dilshod', 'Yusupov Bahrom', 0, '312222222', 'Granula', 'Toshkent'),
       ('"Color Master" MChJ',           'Yangiyo''l yo''li 14',       'O''zbekiston', '+998 71 233-30-30', 'order@colormaster.uz', 'Abdullayeva Zarina', 'Abdullayev Sanjar', 0, '313333333', 'Bo''yoq', 'Toshkent'),
       ('"Karton Pack" MChJ',            'Chirchiq sh., Zavod ko''chasi 2', 'O''zbekiston', '+998 70 715-40-40', 'pack@kartonpack.uz', 'Hasanov Otabek', 'Hasanov Murod', 0, '314444444', 'Qadoq', 'Chirchiq'),
       ('"Metall Detal Servis" XK',      'Yunusobod tumani, 19-mavze', 'O''zbekiston', '+998 99 850-50-50', 'mds@detal.uz', 'Mirzayev Sardor', 'Mirzayev Hamid', 0, '315555555', 'Butlovchi', 'Toshkent');

CREATE TABLE #sup (No int PRIMARY KEY, ID int, Name varchar(50) COLLATE DATABASE_DEFAULT);
INSERT INTO #sup SELECT CAST(RIGHT(DUNS, 1) AS int), ID, Name FROM dbo.SUPPLIERS WHERE DUNS IN ('311111111','312222222','313333333','314444444','315555555');
CREATE TABLE #pr (R int, Mat int, SupNo int, Qty decimal(18,4), Price decimal(18,2), UnitID int, D date, IPID int NULL);
INSERT INTO #pr (R, Mat, SupNo, Qty, Price, UnitID, D) SELECT R, Mat, SupNo, Qty, Price, UnitID, D FROM demo_tmp.PR;

-- shartnoma: ta'minotchiga bitta, PC-{yyyy}-NNNN (PContractService), QQS 12% faqat shartnoma qatorida
DECLARE @pcDate date = DATEADD(day, -95, @t);
CREATE TABLE #pc (SupNo int PRIMARY KEY, ContractNo varchar(50) COLLATE DATABASE_DEFAULT, ID int NULL);
INSERT INTO #pc (SupNo, ContractNo)
SELECT No, 'PC-' + CAST(YEAR(@pcDate) AS varchar(4)) + '-' + RIGHT('0000' + CAST(ROW_NUMBER() OVER (ORDER BY No) AS varchar(10)), 4)
FROM #sup;

INSERT INTO dbo.P_CONTRACTS (ContractNo, IssuedDate, CompanyID, SupplierID, Amount, Incoterms, PaymentTerms, DueDate, IsDeleted, IDN, CurrencyID, PurchaseType)
SELECT pc.ContractNo, @pcDate, 1, s.ID, 0, 'DAP', 'Yetkazilgandan keyin 14 kun', DATEADD(day, 270, @t), 0, NULL, @uzs, 0
FROM #pc pc JOIN #sup s ON s.No = pc.SupNo;
UPDATE pc SET ID = c.ID FROM #pc pc JOIN dbo.P_CONTRACTS c ON c.ContractNo = pc.ContractNo;

INSERT INTO dbo.P_CONTRACT_PRODUCTS (ContractID, ProductID, Price, Quantity, MOQ, ActivePart, UnitID, HSCodeID, VATAmount)
SELECT pc.ID, p.ID, p.Price, q.Qty, 0, 1, p.UnitID, NULL, p.Price * q.Qty * 0.12
FROM demo_tmp.PROD p JOIN #pc pc ON pc.SupNo = p.SupplierNo
CROSS APPLY (SELECT CEILING(SUM(x.Qty) * 1.25 / 100) * 100 AS Qty FROM #pr x WHERE x.Mat = p.ID) q
WHERE p.IsRaw = 1;
UPDATE c SET Amount = x.Amt
FROM dbo.P_CONTRACTS c
JOIN (SELECT ContractID, SUM(Price * Quantity + VATAmount) AS Amt FROM dbo.P_CONTRACT_PRODUCTS GROUP BY ContractID) x ON x.ContractID = c.ID
WHERE c.ID IN (SELECT ID FROM #pc);

-- tur x ta'minotchi = bitta buyurtma + invoys + kirim
CREATE TABLE #pi (K int IDENTITY PRIMARY KEY, R int, SupNo int, D date, OrderNo varchar(50) COLLATE DATABASE_DEFAULT, InvoiceNo varchar(50) COLLATE DATABASE_DEFAULT, DocNo varchar(50) COLLATE DATABASE_DEFAULT,
                  Net decimal(18,2), Gross decimal(18,2), Qty decimal(18,4), OrderID int NULL, InvID int NULL, IncID int NULL);
INSERT INTO #pi (R, SupNo, D, Net, Gross, Qty)
SELECT R, SupNo, D, SUM(Qty * Price), SUM(ROUND(Qty * Price * 1.12, 2)), SUM(Qty)
FROM #pr GROUP BY R, SupNo, D ORDER BY D, SupNo;
;WITH x AS (
    SELECT OrderNo, InvoiceNo, DATEADD(day, -5, D) AS OD, D,
           ROW_NUMBER() OVER (PARTITION BY YEAR(DATEADD(day, -5, D)), MONTH(DATEADD(day, -5, D)) ORDER BY K) AS OrdN,
           ROW_NUMBER() OVER (PARTITION BY YEAR(D) ORDER BY K) AS InvN
    FROM #pi)
UPDATE x SET
    OrderNo = 'PO-' + CONVERT(char(4), YEAR(OD)) + '-' + RIGHT('0' + CAST(MONTH(OD) AS varchar(2)), 2) + '-' + RIGHT('0000' + CAST(OrdN AS varchar(10)), 4),
    InvoiceNo = 'PI-' + CONVERT(char(4), YEAR(D)) + '-' + RIGHT('0000' + CAST(InvN AS varchar(10)), 4);

INSERT INTO dbo.P_ORDERS (OrderNo, IssuedDate, CompanyID, SupplierID, ContractID, Amount, Description, IsDeleted, CurrencyID, PurchaseType,
                          Status, ContractStatus, InvoiceStatus, WarehouseStatus)
SELECT OrderNo, DATEADD(day, -5, CAST(D AS datetime)), 1, NULL, NULL, ROUND(Net * 1.12, 2), N'Xomashyo xaridi: ' + s.Name, 0, @uzs, 0,
       'Created', 'NotCreated', 'NotCreated', 'NotReceived'
FROM #pi x JOIN #sup s ON s.No = x.SupNo;
UPDATE x SET OrderID = o.ID FROM #pi x JOIN dbo.P_ORDERS o ON o.OrderNo = x.OrderNo;

INSERT INTO dbo.P_ORDER_PRODUCTS (OrderID, ProductID, Price, Amount, MOQ, UnitID, Priority, RequiredDate)
SELECT x.OrderID, r.Mat, r.Price, r.Qty, 0, r.UnitID, 'Medium', CAST(r.D AS datetime)
FROM #pr r JOIN #pi x ON x.R = r.R AND x.SupNo = r.SupNo;

INSERT INTO dbo.P_INVOICES (InvoiceNo, OrderID, SupplierID, Amount, InvoiceDate, CompanyID, IsDeleted, CurrencyID, ContractID, PurchaseType)
SELECT x.InvoiceNo, x.OrderID, s.ID, ROUND(x.Net * 1.12, 2), x.D, 1, 0, @uzs, pc.ID, 0
FROM #pi x JOIN #sup s ON s.No = x.SupNo JOIN #pc pc ON pc.SupNo = x.SupNo;
UPDATE x SET InvID = i.ID FROM #pi x JOIN dbo.P_INVOICES i ON i.InvoiceNo = x.InvoiceNo;

INSERT INTO dbo.P_INVOICE_PRODUCTS (InvoiceID, ProductID, Quantity, Price, UnitID)
SELECT x.InvID, r.Mat, r.Qty, r.Price, r.UnitID
FROM #pr r JOIN #pi x ON x.R = r.R AND x.SupNo = r.SupNo;

-- kirim: DocNo '{yyyyMM}_{n}' (IncomesController), SUPPLY ombori, RecieveStatus = 1 (invoysga bog'langan)
;WITH x AS (SELECT DocNo, D, ROW_NUMBER() OVER (PARTITION BY CONVERT(char(6), D, 112) ORDER BY K) AS N FROM #pi)
UPDATE x SET DocNo = CONVERT(char(6), D, 112) + '_' + CAST(N AS varchar(10));
INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
SELECT x.DocNo, @whS, x.Qty, x.Gross, 0, 'Xarid invoysi ' + x.InvoiceNo + ' bo''yicha kirim', DATEADD(hour, 10, CAST(x.D AS datetime)), NULL, 1, @uzs, x.InvID, NULL
FROM #pi x;
UPDATE x SET IncID = i.ID FROM #pi x JOIN dbo.INCOMES i ON i.DocNo = x.DocNo AND i.InvoiceID = x.InvID;

INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
SELECT x.IncID, r.Mat, r.Qty, r.Price, NULL, r.UnitID, 12
FROM #pr r JOIN #pi x ON x.R = r.R AND x.SupNo = r.SupNo;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #sup, #pr, #pc, #pi;

PRINT 'Demo ta''minot: OK';
