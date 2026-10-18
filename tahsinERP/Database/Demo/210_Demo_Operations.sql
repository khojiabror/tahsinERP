/* =====================================================================
   tahsinERP — DEMO korxona: xarid, ombor, ishlab chiqarish, sotuv (200_Demo_Master.sql dan keyin).

   Hujjatlar servislar yozadigan shaklda (raqam formatlari, tavsiflar, statuslar):
     SRM  P_CONTRACTS -> P_ORDERS (to'g'ridan-to'g'ri) -> P_INVOICES -> INCOMES (SUPPLY ombori, IncomesController)
     Ombor  SUPPLY -> PRODUCTION haftalik o'tkazma: EXPENSES + bog'langan INCOMES 'INC-...' (FIFO tannarx bilan),
            PRODUCTION ombori DLOC: INCOME_LOTS 'LOT-{inc}-{ip}' + LOT_LOCATIONS
     MES  PRODUCT_PRODUCTION -> INCOMES 'PROD-...' (lot 'PROD-{inc}-{inc}') + BOM bo'yicha xomashyo chiqimi
          (EXPENSE_PRODUCT_ALLOCATIONS — eng eski lotdan, FIFO) -> QUALITY_INSPECTION -> sotuv omboriga 'TRNSF_' o'tkazma
     CRM  S_CONTRACTS -> S_ORDERS -> SALES_INVOICES (QQS 12%) -> sotuv omboridan chiqim (WarehouseFlowService.DecreaseSalesStock)
   PRODUCT_STOCKS harakatlardan hisoblanadi; oxirida qoldiq manfiy emasligi tekshiriladi (aks holda THROW).
   Moliya yozuvlari (FIN_TRANSACTIONS) — 220_Demo_Finance_HR.sql.

   Idempotent: INCOMES bo'sh bo'lmasa fayl hech narsa qilmaydi. Bitta batch (GO yo'q).
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.INCOMES) OR EXISTS (SELECT 1 FROM dbo.SALES_INVOICES) OR EXISTS (SELECT 1 FROM dbo.P_INVOICES)
BEGIN
    PRINT 'Demo operatsiyalar: bazada hujjatlar bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @start date = DATEADD(day, -56, @t);          -- ishlab chiqarish davri: 8 hafta
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @kg int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'KG');
DECLARE @whS int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SUPPLY' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @whP int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'PRODUCTION' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @whT int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SALES' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @shop int = (SELECT ShopID FROM dbo.WAREHOUSES WHERE ID = @whP);
DECLARE @locA int = (SELECT TOP 1 ID FROM dbo.WAREHOUSE_LOCATIONS WHERE WarehouseID = @whP AND IsActive = 1 AND IsDeleted = 0 ORDER BY LocationCode);
DECLARE @locB int = (SELECT TOP 1 ID FROM dbo.WAREHOUSE_LOCATIONS WHERE WarehouseID = @whP AND IsActive = 1 AND IsDeleted = 0 ORDER BY LocationCode DESC);
IF @u IS NULL OR @whS IS NULL OR @whP IS NULL OR @whT IS NULL OR @locA IS NULL OR @shop IS NULL
    THROW 50101, 'Demo operatsiyalar: 200_Demo_Master.sql natijasi topilmadi (demo user / omborlar / joylar)', 1;

/* ---------------- mahsulotlar va narxlar ---------------- */
CREATE TABLE #prod (ID int PRIMARY KEY, PNo varchar(15), Name varchar(50), UnitID int, IsRaw bit,
                    Price decimal(18,2), DailyQty int NULL, SupplierNo int NULL, FgIdx int NULL);
INSERT INTO #prod (ID, PNo, Name, UnitID, IsRaw, Price, DailyQty, SupplierNo, FgIdx)
SELECT p.ID, p.PNo, p.Name, p.UnitID, ISNULL(p.IsRawMaterial, 0), v.Price, v.DailyQty, v.SupplierNo, v.FgIdx
FROM dbo.PRODUCTS p
JOIN (VALUES
 ('XM-001', 16500, NULL, 1, NULL), ('XM-002', 17800, NULL, 2, NULL), ('XM-003', 18200, NULL, 2, NULL),
 ('XM-004', 38000, NULL, 3, NULL), ('XM-005', 42000, NULL, 3, NULL), ('XM-006', 44000, NULL, 3, NULL),
 ('XM-007', 65000, NULL, 3, NULL), ('XM-008',  6500, NULL, 4, NULL), ('XM-009',  4200, NULL, 4, NULL),
 ('XM-010',   120, NULL, 4, NULL), ('XM-011', 24000, NULL, 5, NULL), ('XM-012',   900, NULL, 5, NULL),
 ('TM-001', 14000,  400, NULL, 1), ('TM-002', 22000,  300, NULL, 2), ('TM-003', 23000,  150, NULL, 3),
 ('TM-004', 38000,  120, NULL, 4), ('TM-005', 32000,  100, NULL, 5), ('TM-006',  3200, 1200, NULL, 6),
 ('TM-007',  4800,  900, NULL, 7), ('TM-008',  7500,  600, NULL, 8), ('TM-009', 12000,  200, NULL, 9),
 ('TM-010',  9000,  150, NULL, 10), ('TM-011', 85000,  60, NULL, 11), ('TM-012', 240000, 15, NULL, 12)
) v (PNo, Price, DailyQty, SupplierNo, FgIdx) ON v.PNo = p.PNo
WHERE p.IsDeleted = 0;
IF (SELECT COUNT(*) FROM #prod) <> 24 THROW 50102, 'Demo operatsiyalar: 24 demo mahsulot topilmadi', 1;

CREATE TABLE #sup (No int PRIMARY KEY, ID int, Name varchar(50));
INSERT INTO #sup SELECT CAST(RIGHT(DUNS, 1) AS int), ID, Name FROM dbo.SUPPLIERS WHERE DUNS IN ('311111111','312222222','313333333','314444444','315555555');
CREATE TABLE #cust (No int PRIMARY KEY, ID int, Name varchar(50));
INSERT INTO #cust SELECT CAST(LEFT(DUNS, 3) AS int) - 300, ID, Name FROM dbo.CUSTOMERS WHERE DUNS IN ('301111111','302222222','303333333','304444444','305555555');

-- Faol BOM (eng oxirgi VersionNo) — ProductionConsumptionService shu bo'yicha sarflaydi
CREATE TABLE #bom (FG int, Mat int, Qty decimal(18,4), UnitID int);
INSERT INTO #bom
SELECT h.ProductID, l.ComponentID, l.Qty, l.UnitID
FROM dbo.BOM_HEADERS h JOIN dbo.BOM_LINES l ON l.BOMID = h.ID
WHERE h.IsActive = 1 AND h.ProductID IN (SELECT ID FROM #prod WHERE IsRaw = 0);

/* ---------------- 1. ishlab chiqarish rejasi (yakshanbasiz, har mahsulot kun ora) ---------------- */
CREATE TABLE #pp (K int IDENTITY PRIMARY KEY, ProductID int, D date, DayIdx int, Wk int, Qty int, DayShift int, NightShift int,
                  At datetime, Rejected int, PPID int NULL, IncID int NULL, IPID int NULL, LotID int NULL, ExpID int NULL);
;WITH n AS (SELECT TOP (56) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects),
d AS (SELECT DATEADD(day, k, @start) AS D, k AS DayIdx FROM n WHERE DATEDIFF(day, '19000101', DATEADD(day, k, @start)) % 7 <> 6)
INSERT INTO #pp (ProductID, D, DayIdx, Wk, Qty, DayShift, NightShift, At, Rejected)
SELECT p.ID, d.D, d.DayIdx, d.DayIdx / 7, q.Qty, CEILING(q.Qty * 0.6), q.Qty - CEILING(q.Qty * 0.6),
       DATEADD(minute, 17 * 60 + p.FgIdx * 3, CAST(d.D AS datetime)),
       CASE WHEN (d.DayIdx + p.FgIdx) % 3 = 0 THEN FLOOR(q.Qty * 0.01) ELSE 0 END
FROM d CROSS JOIN #prod p
CROSS APPLY (SELECT CAST(ROUND(p.DailyQty * 2 * (0.85 + 0.05 * ((d.DayIdx * 3 + p.FgIdx) % 7)), 0) AS int) AS Qty) q
WHERE p.IsRaw = 0 AND (d.DayIdx + p.FgIdx) % 2 = 0
ORDER BY d.D, p.FgIdx;

-- har ishlab chiqarishning xomashyo sarfi (BOM x miqdor)
CREATE TABLE #cons (K int IDENTITY PRIMARY KEY, PPK int, Mat int, Qty decimal(18,4), UnitID int, At datetime, EPID int NULL, Cost decimal(18,4) NULL);
INSERT INTO #cons (PPK, Mat, Qty, UnitID, At)
SELECT pp.K, b.Mat, ROUND(b.Qty * pp.Qty, 4), b.UnitID, pp.At
FROM #pp pp JOIN #bom b ON b.FG = pp.ProductID
ORDER BY pp.At, pp.K, b.Mat;

/* ---------------- 2. haftalik o'tkazma SUPPLY -> PRODUCTION (hafta boshida 08:00, ehtiyoj + 5%) ---------------- */
CREATE TABLE #tr (K int IDENTITY PRIMARY KEY, Wk int, Mat int, Qty decimal(18,4), UnitID int, At datetime, Cost decimal(18,4) NULL,
                  ExpID int NULL, EPID int NULL, IncID int NULL, IPID int NULL, LotID int NULL);
INSERT INTO #tr (Wk, Mat, Qty, UnitID, At)
SELECT pp.Wk, c.Mat,
       CASE WHEN c.UnitID = @kg THEN CEILING(SUM(c.Qty) * 1.05) ELSE CEILING(SUM(c.Qty) * 1.05 / 10) * 10 END,
       c.UnitID, DATEADD(hour, 8, CAST(DATEADD(day, 7 * pp.Wk, @start) AS datetime))
FROM #cons c JOIN #pp pp ON pp.K = c.PPK
GROUP BY pp.Wk, c.Mat, c.UnitID
ORDER BY pp.Wk, c.Mat;

/* ---------------- 3. xaridlar: 5 tur x 5 ta'minotchi (jami o'tkazmaning ~130%) ---------------- */
CREATE TABLE #pr (R int, Mat int, SupNo int, Qty decimal(18,4), Price decimal(18,2), UnitID int, D date, IPID int NULL);
INSERT INTO #pr (R, Mat, SupNo, Qty, Price, UnitID, D)
SELECT r.R, p.ID, p.SupplierNo,
       CASE WHEN p.UnitID = @kg THEN CEILING(tt.Qty * 0.26 / 25) * 25 ELSE CEILING(tt.Qty * 0.26 / 100) * 100 END,
       ROUND(p.Price * (1 + 0.015 * r.R), 2), p.UnitID, DATEADD(day, r.Ofs, @t)
FROM #prod p
JOIN (SELECT Mat, SUM(Qty) AS Qty FROM #tr GROUP BY Mat) tt ON tt.Mat = p.ID
CROSS JOIN (VALUES (0, -80), (1, -62), (2, -44), (3, -26), (4, -9)) r (R, Ofs)
WHERE p.IsRaw = 1;

-- shartnoma: ta'minotchiga bitta, PC-{yyyy}-NNNN (PContractService), QQS 12% faqat shartnoma qatorida
DECLARE @pcDate date = DATEADD(day, -95, @t);
CREATE TABLE #pc (SupNo int PRIMARY KEY, ContractNo varchar(50), ID int NULL);
INSERT INTO #pc (SupNo, ContractNo)
SELECT No, 'PC-' + CAST(YEAR(@pcDate) AS varchar(4)) + '-' + RIGHT('0000' + CAST(ROW_NUMBER() OVER (ORDER BY No) AS varchar(10)), 4)
FROM #sup;

INSERT INTO dbo.P_CONTRACTS (ContractNo, IssuedDate, CompanyID, SupplierID, Amount, Incoterms, PaymentTerms, DueDate, IsDeleted, IDN, CurrencyID, PurchaseType)
SELECT pc.ContractNo, @pcDate, 1, s.ID, 0, 'DAP', 'Yetkazilgandan keyin 14 kun', DATEADD(day, 270, @t), 0, NULL, @uzs, 0
FROM #pc pc JOIN #sup s ON s.No = pc.SupNo;
UPDATE pc SET ID = c.ID FROM #pc pc JOIN dbo.P_CONTRACTS c ON c.ContractNo = pc.ContractNo;

INSERT INTO dbo.P_CONTRACT_PRODUCTS (ContractID, ProductID, Price, Quantity, MOQ, ActivePart, UnitID, HSCodeID, VATAmount)
SELECT pc.ID, p.ID, p.Price, q.Qty, 0, 1, p.UnitID, NULL, p.Price * q.Qty * 0.12
FROM #prod p JOIN #pc pc ON pc.SupNo = p.SupplierNo
CROSS APPLY (SELECT CEILING(SUM(x.Qty) * 1.25 / 100) * 100 AS Qty FROM #pr x WHERE x.Mat = p.ID) q
WHERE p.IsRaw = 1;
UPDATE c SET Amount = x.Amt
FROM dbo.P_CONTRACTS c
JOIN (SELECT ContractID, SUM(Price * Quantity + VATAmount) AS Amt FROM dbo.P_CONTRACT_PRODUCTS GROUP BY ContractID) x ON x.ContractID = c.ID
WHERE c.ID IN (SELECT ID FROM #pc);

-- tur x ta'minotchi = bitta buyurtma + invoys + kirim
CREATE TABLE #pi (K int IDENTITY PRIMARY KEY, R int, SupNo int, D date, OrderNo varchar(50), InvoiceNo varchar(50), DocNo varchar(50),
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

CREATE TABLE #map (K int, ID int);
MERGE dbo.INCOME_PRODUCTS AS tg
USING (SELECT r.*, x.IncID FROM #pr r JOIN #pi x ON x.R = r.R AND x.SupNo = r.SupNo) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
     VALUES (s.IncID, s.Mat, s.Qty, s.Price, NULL, s.UnitID, 12)
OUTPUT s.R * 100000 + s.Mat, inserted.ID INTO #map (K, ID);
UPDATE r SET IPID = m.ID FROM #pr r JOIN #map m ON m.K = r.R * 100000 + r.Mat;
TRUNCATE TABLE #map;

/* ---------------- 4. o'tkazma tannarxi: SUPPLY ombori qatlamlaridan FIFO ---------------- */
;WITH L AS (
    SELECT r.Mat, r.Price, SUM(r.Qty) OVER (PARTITION BY r.Mat ORDER BY r.D, r.IPID ROWS UNBOUNDED PRECEDING) AS L1, r.Qty
    FROM #pr r),
C AS (
    SELECT t.K, t.Mat, t.Qty, SUM(t.Qty) OVER (PARTITION BY t.Mat ORDER BY t.At, t.K ROWS UNBOUNDED PRECEDING) AS C1 FROM #tr t)
UPDATE t SET Cost = x.Cost
FROM #tr t
JOIN (SELECT c.K, SUM(ov.Q * l.Price) / MAX(c.Qty) AS Cost
      FROM C c JOIN L l ON l.Mat = c.Mat
      CROSS APPLY (SELECT CASE WHEN l.L1 < c.C1 THEN l.L1 ELSE c.C1 END
                        - CASE WHEN l.L1 - l.Qty > c.C1 - c.Qty THEN l.L1 - l.Qty ELSE c.C1 - c.Qty END AS Q) ov
      WHERE ov.Q > 0
      GROUP BY c.K) x ON x.K = t.K;
IF EXISTS (SELECT 1 FROM #tr WHERE Cost IS NULL) THROW 50103, 'Demo: o''tkazma uchun xarid qoldig''i yetmadi', 1;

-- chiqim hujjati: hafta bo'yicha bitta, DocNo '{yyyyMM}_{n}'
CREATE TABLE #trd (Wk int PRIMARY KEY, At datetime, DocNo varchar(50), ExpID int NULL, IncID int NULL);
INSERT INTO #trd (Wk, At) SELECT Wk, MIN(At) FROM #tr GROUP BY Wk;
;WITH x AS (SELECT DocNo, At, ROW_NUMBER() OVER (PARTITION BY CONVERT(char(6), At, 112) ORDER BY Wk) AS N FROM #trd)
UPDATE x SET DocNo = CONVERT(char(6), At, 112) + '_' + CAST(N AS varchar(10));

INSERT INTO dbo.EXPENSES (DocNo, Amount, TotalPrice, ReceiverWhID, SenderWHID, IsDeleted, Description, IssueDateTime, SendStatus, CurrencyID, ProductionID)
SELECT d.DocNo, SUM(t.Qty), SUM(t.Qty * t.Cost), @whP, @whS, 0, 'Ishlab chiqarish uchun haftalik xomashyo', d.At, 1, @uzs, NULL
FROM #trd d JOIN #tr t ON t.Wk = d.Wk GROUP BY d.Wk, d.DocNo, d.At;
UPDATE d SET ExpID = e.ID FROM #trd d JOIN dbo.EXPENSES e ON e.DocNo = d.DocNo AND e.SenderWHID = @whS AND e.ReceiverWhID = @whP;

MERGE dbo.EXPENSE_PRODUCTS AS tg
USING (SELECT t.*, d.ExpID AS EID FROM #tr t JOIN #trd d ON d.Wk = t.Wk) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (ExpenseID, ProductID, Amount, PiecePrice, Comment, UnitID, LotID, FromLocationID, AllocationPolicy, ViolationReasonCode)
     VALUES (s.EID, s.Mat, s.Qty, s.Cost, '', s.UnitID, NULL, NULL, NULL, NULL)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE t SET EPID = m.ID FROM #tr t JOIN #map m ON m.K = t.K;
TRUNCATE TABLE #map;

-- bog'langan kirim (IncomesController.Receive: ishlab chiqarish mas'uli qabul qilgan)
INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
SELECT 'INC-' + d.DocNo, @whP, e.Amount, e.TotalPrice, 0,
       'Chiqimdan avtomatik bog''langan: ' + d.DocNo + ' - Ishlab chiqarish uchun haftalik xomashyo | EXPENSE_ID:' + CAST(d.ExpID AS varchar(12)),
       d.At, @whS, 1, @uzs, NULL, NULL
FROM #trd d JOIN dbo.EXPENSES e ON e.ID = d.ExpID;
UPDATE d SET IncID = i.ID FROM #trd d JOIN dbo.INCOMES i ON i.DocNo = 'INC-' + d.DocNo AND i.ReceivingWHID = @whP;

MERGE dbo.INCOME_PRODUCTS AS tg
USING (SELECT t.*, d.IncID AS IID, d.DocNo FROM #tr t JOIN #trd d ON d.Wk = t.Wk) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
     VALUES (s.IID, s.Mat, s.Qty, s.Cost, 'Chiqimdan bog''langan: ' + s.DocNo, s.UnitID, NULL)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE t SET IPID = m.ID, IncID = d.IncID FROM #tr t JOIN #map m ON m.K = t.K JOIN #trd d ON d.Wk = t.Wk;
TRUNCATE TABLE #map;

-- DLOC lot: 'LOT-{IncomeID}-{IncomeProductID}' (LocationLotStockLedger)
INSERT INTO dbo.INCOME_LOTS (IncomeID, IncomeProductID, ProductID, WarehouseID, LotNo, ReceivedDateTime, QtyReceived, QtyConsumed, ExpiryDate, IsDeleted, CreatedAt)
SELECT IncID, IPID, Mat, @whP, 'LOT-' + CAST(IncID AS varchar(12)) + '-' + CAST(IPID AS varchar(12)), At, Qty, 0, NULL, 0, At FROM #tr;
UPDATE t SET LotID = l.ID FROM #tr t JOIN dbo.INCOME_LOTS l ON l.IncomeProductID = t.IPID;
INSERT INTO dbo.LOT_LOCATIONS (LotID, LocationID, ProductID, QtyAvailable, LastMovementAt)
SELECT LotID, CASE WHEN UnitID = @kg THEN @locA ELSE ISNULL(@locB, @locA) END, Mat, Qty, At FROM #tr;

/* ---------------- 5. ishlab chiqarish: PRODUCT_PRODUCTION + tayyor mahsulot kirimi + xomashyo chiqimi ---------------- */
INSERT INTO dbo.PRODUCT_PRODUCTION (ProductID, ShopID, Date, DayShift, NightShift, Comment, IsDeleted, PlanDate)
SELECT ProductID, @shop, D, DayShift, NightShift, NULL, 0, D FROM #pp ORDER BY K;
UPDATE pp SET PPID = x.ID FROM #pp pp JOIN dbo.PRODUCT_PRODUCTION x ON x.ProductID = pp.ProductID AND x.Date = pp.D;

-- WarehouseFlowService.IncreaseProductionStock
INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
SELECT 'PROD-' + CONVERT(char(8), pp.At, 112) + REPLACE(CONVERT(char(8), pp.At, 108), ':', '') + '-' + CAST(pp.ProductID AS varchar(12)),
       @whP, NULL, NULL, 0, 'Ishlab chiqarishdan kirim: ' + p.PNo + ' - ' + p.Name + '. ProductionID:' + CAST(pp.PPID AS varchar(12)),
       pp.At, NULL, 1, @uzs, NULL, pp.PPID
FROM #pp pp JOIN #prod p ON p.ID = pp.ProductID;
UPDATE pp SET IncID = i.ID FROM #pp pp JOIN dbo.INCOMES i ON i.ProductionID = pp.PPID;

INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
SELECT pp.IncID, pp.ProductID, pp.Qty, 0, NULL, p.UnitID, NULL FROM #pp pp JOIN #prod p ON p.ID = pp.ProductID;
UPDATE pp SET IPID = ip.ID FROM #pp pp JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = pp.IncID;

INSERT INTO dbo.INCOME_LOTS (IncomeID, IncomeProductID, ProductID, WarehouseID, LotNo, ReceivedDateTime, QtyReceived, QtyConsumed, ExpiryDate, IsDeleted, CreatedAt)
SELECT IncID, IPID, ProductID, @whP, 'PROD-' + CAST(IncID AS varchar(12)) + '-' + CAST(IncID AS varchar(12)), At, Qty, 0, NULL, 0, At FROM #pp;
UPDATE pp SET LotID = l.ID FROM #pp pp JOIN dbo.INCOME_LOTS l ON l.IncomeProductID = pp.IPID;
INSERT INTO dbo.LOT_LOCATIONS (LotID, LocationID, ProductID, QtyAvailable, LastMovementAt)
SELECT LotID, @locA, ProductID, Qty, At FROM #pp;

-- xomashyo chiqimi: ProductionConsumptionService (DocNo '{yyyyMMdd}_{PNo}_{qty}', ishlab chiqarish omboridan)
INSERT INTO dbo.EXPENSES (DocNo, Amount, TotalPrice, ReceiverWhID, SenderWHID, IsDeleted, Description, IssueDateTime, SendStatus, CurrencyID, ProductionID)
SELECT CONVERT(char(8), pp.D, 112) + '_' + p.PNo + '_' + CAST(pp.Qty AS varchar(12)), NULL, NULL, NULL, @whP, 0,
       CONVERT(char(10), pp.D, 104) + 'y. kunlik reja asosida Ishlab chiqarish uchun xomashyo chiqimi: ' + p.PNo + ', Miqdor ' + CAST(pp.Qty AS varchar(12))
         + ' | ProductionID:' + CAST(pp.PPID AS varchar(12)),
       pp.At, 1, @uzs, pp.PPID
FROM #pp pp JOIN #prod p ON p.ID = pp.ProductID;
UPDATE pp SET ExpID = e.ID FROM #pp pp JOIN dbo.EXPENSES e ON e.ProductionID = pp.PPID;

-- FIFO: sarf ketma-ketligi x o'tkazma lotlari (ReceivedDateTime, ID) kesishmasi
CREATE TABLE #alloc (ConsK int, LotID int, LocID int, Qty decimal(18,4), Price decimal(18,4), LotOrd int);
;WITH L AS (
    SELECT t.Mat, t.LotID, t.Cost, t.Qty, CASE WHEN t.UnitID = @kg THEN @locA ELSE ISNULL(@locB, @locA) END AS LocID,
           SUM(t.Qty) OVER (PARTITION BY t.Mat ORDER BY t.At, t.LotID ROWS UNBOUNDED PRECEDING) AS L1,
           ROW_NUMBER() OVER (PARTITION BY t.Mat ORDER BY t.At, t.LotID) AS LotOrd
    FROM #tr t),
C AS (
    SELECT c.K, c.Mat, c.Qty, SUM(c.Qty) OVER (PARTITION BY c.Mat ORDER BY c.At, c.K ROWS UNBOUNDED PRECEDING) AS C1 FROM #cons c)
INSERT INTO #alloc (ConsK, LotID, LocID, Qty, Price, LotOrd)
SELECT c.K, l.LotID, l.LocID, ov.Q, l.Cost, l.LotOrd
FROM C c JOIN L l ON l.Mat = c.Mat
CROSS APPLY (SELECT CASE WHEN l.L1 < c.C1 THEN l.L1 ELSE c.C1 END
                  - CASE WHEN l.L1 - l.Qty > c.C1 - c.Qty THEN l.L1 - l.Qty ELSE c.C1 - c.Qty END AS Q) ov
WHERE ov.Q > 0;
IF EXISTS (SELECT 1 FROM #cons c WHERE c.Qty > ISNULL((SELECT SUM(a.Qty) FROM #alloc a WHERE a.ConsK = c.K), 0) + 0.00005)
    THROW 50104, 'Demo: ishlab chiqarish omborida xomashyo yetmadi', 1;
UPDATE c SET Cost = x.Cost FROM #cons c JOIN (SELECT ConsK, SUM(Qty * Price) / SUM(Qty) AS Cost FROM #alloc GROUP BY ConsK) x ON x.ConsK = c.K;

MERGE dbo.EXPENSE_PRODUCTS AS tg
USING (SELECT c.*, pp.ExpID AS EID, f.LotID AS FLot, f.LocID AS FLoc
       FROM #cons c JOIN #pp pp ON pp.K = c.PPK
       CROSS APPLY (SELECT TOP 1 a.LotID, a.LocID FROM #alloc a WHERE a.ConsK = c.K ORDER BY a.LotOrd) f) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (ExpenseID, ProductID, Amount, PiecePrice, Comment, UnitID, LotID, FromLocationID, AllocationPolicy, ViolationReasonCode)
     VALUES (s.EID, s.Mat, s.Qty, s.Cost, 'BOM/NORM sarfi', s.UnitID, s.FLot, s.FLoc, 'FIFO', NULL)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE c SET EPID = m.ID FROM #cons c JOIN #map m ON m.K = c.K;
TRUNCATE TABLE #map;

INSERT INTO dbo.EXPENSE_PRODUCT_ALLOCATIONS (ExpenseProductID, LotID, FromLocationID, Qty, CreatedAt)
SELECT c.EPID, a.LotID, a.LocID, a.Qty, c.At FROM #alloc a JOIN #cons c ON c.K = a.ConsK;

UPDATE e SET Amount = x.Q, TotalPrice = x.V
FROM dbo.EXPENSES e
JOIN (SELECT pp.ExpID, SUM(CAST(c.Qty AS float)) AS Q, SUM(CAST(c.Qty AS float) * CAST(c.Cost AS float)) AS V
      FROM #cons c JOIN #pp pp ON pp.K = c.PPK GROUP BY pp.ExpID) x ON x.ExpID = e.ID;

-- sifat nazorati (QualityInspectionController): sotuv omboriga o'tkazish faqat qabul qilingan miqdor bo'yicha
INSERT INTO dbo.QUALITY_INSPECTION (SOrderID, DeliveryScheduleID, ProductPlanDailyID, ProductionID, ProductID, QtyInspected, QtyAccepted, QtyRejected,
                                    DefectTypeID, StatusCode, Notes, InspectedByUserID, ApprovedByUserID, InspectionDate, ApprovalDate, CreatedDT, IsDeleted)
SELECT NULL, NULL, NULL, PPID, ProductID, Qty, Qty - Rejected, Rejected, NULL, 'APPROVED',
       CASE WHEN Rejected > 0 THEN N'Rang notekisligi' END, @u, @u, DATEADD(minute, 30, At), DATEADD(minute, 40, At), DATEADD(minute, 30, At), 0
FROM #pp;

/* ---------------- 6. PRODUCTION -> SALES o'tkazma (TransferProductionToSales): har haftaning qabul qilingani ---------------- */
CREATE TABLE #ft (Wk int PRIMARY KEY, At datetime, DocNo varchar(50), Pnos varchar(400), ExpID int NULL, IncID int NULL);
INSERT INTO #ft (Wk, At)
SELECT DISTINCT Wk, DATEADD(hour, 7, CAST(DATEADD(day, 7 * (Wk + 1), @start) AS datetime)) FROM #pp WHERE Wk <= 6;
UPDATE f SET DocNo = 'TRNSF_' + REPLACE(CONVERT(char(8), f.At, 3), '/', '') + '_1',
             Pnos = STUFF((SELECT ', ' + p.PNo FROM #prod p WHERE p.ID IN (SELECT ProductID FROM #pp WHERE Wk = f.Wk) ORDER BY p.PNo FOR XML PATH('')), 1, 2, '')
FROM #ft f;

CREATE TABLE #ftl (K int IDENTITY PRIMARY KEY, Wk int, ProductID int, UnitID int, Qty decimal(18,4), Comment varchar(4000), FirstLot int, EPID int NULL);
INSERT INTO #ftl (Wk, ProductID, UnitID, Qty, Comment, FirstLot)
SELECT pp.Wk, pp.ProductID, p.UnitID, SUM(pp.Qty - pp.Rejected),
       STUFF((SELECT '|ProdID:' + CAST(x.PPID AS varchar(12)) + ':' + CAST(x.Qty - x.Rejected AS varchar(12))
              FROM #pp x WHERE x.Wk = pp.Wk AND x.ProductID = pp.ProductID ORDER BY x.At FOR XML PATH('')), 1, 1, ''),
       MIN(pp.LotID)
FROM #pp pp JOIN #prod p ON p.ID = pp.ProductID
WHERE pp.Wk <= 6
GROUP BY pp.Wk, pp.ProductID, p.UnitID;

INSERT INTO dbo.EXPENSES (DocNo, Amount, TotalPrice, ReceiverWhID, SenderWHID, IsDeleted, Description, IssueDateTime, SendStatus, CurrencyID, ProductionID)
SELECT f.DocNo, SUM(l.Qty), 0, @whT, @whP, 0, 'Ishlab chiqarishdan sotuv omboriga o''tkazma: ' + f.Pnos, f.At, 1, @uzs, NULL
FROM #ft f JOIN #ftl l ON l.Wk = f.Wk GROUP BY f.Wk, f.DocNo, f.Pnos, f.At;
UPDATE f SET ExpID = e.ID FROM #ft f JOIN dbo.EXPENSES e ON e.DocNo = f.DocNo AND e.SenderWHID = @whP AND e.ReceiverWhID = @whT;

MERGE dbo.EXPENSE_PRODUCTS AS tg
USING (SELECT l.*, f.ExpID AS EID FROM #ftl l JOIN #ft f ON f.Wk = l.Wk) AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (ExpenseID, ProductID, Amount, PiecePrice, Comment, UnitID, LotID, FromLocationID, AllocationPolicy, ViolationReasonCode)
     VALUES (s.EID, s.ProductID, s.Qty, 0, s.Comment, s.UnitID, s.FirstLot, @locA, 'FIFO', NULL)
OUTPUT s.K, inserted.ID INTO #map (K, ID);
UPDATE l SET EPID = m.ID FROM #ftl l JOIN #map m ON m.K = l.K;
TRUNCATE TABLE #map;

INSERT INTO dbo.EXPENSE_PRODUCT_ALLOCATIONS (ExpenseProductID, LotID, FromLocationID, Qty, CreatedAt)
SELECT l.EPID, pp.LotID, @locA, pp.Qty - pp.Rejected, f.At
FROM #pp pp JOIN #ftl l ON l.Wk = pp.Wk AND l.ProductID = pp.ProductID JOIN #ft f ON f.Wk = pp.Wk
WHERE pp.Qty - pp.Rejected > 0;

INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
SELECT f.DocNo + '-IN', @whT, e.Amount, 0, 0,
       'Chiqimdan avtomatik bog''langan: ' + f.DocNo + ' - Ishlab chiqarishdan sotuv omboriga o''tkazma: ' + f.Pnos + '. EXPENS ID: ' + CAST(f.ExpID AS varchar(12)),
       f.At, @whP, 1, @uzs, NULL, NULL
FROM #ft f JOIN dbo.EXPENSES e ON e.ID = f.ExpID;
UPDATE f SET IncID = i.ID FROM #ft f JOIN dbo.INCOMES i ON i.DocNo = f.DocNo + '-IN';
INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
SELECT f.IncID, l.ProductID, l.Qty, 0, NULL, l.UnitID, NULL FROM #ftl l JOIN #ft f ON f.Wk = l.Wk;

-- lot qoldiqlari (ishlab chiqarish ombori)
UPDATE l SET QtyConsumed = x.Q
FROM dbo.INCOME_LOTS l JOIN (SELECT LotID, SUM(Qty) AS Q FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS GROUP BY LotID) x ON x.LotID = l.ID;
UPDATE ll SET QtyAvailable = l.QtyReceived - l.QtyConsumed,
              LastMovementAt = ISNULL((SELECT MAX(a.CreatedAt) FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a WHERE a.LotID = l.ID), l.ReceivedDateTime)
FROM dbo.LOT_LOCATIONS ll JOIN dbo.INCOME_LOTS l ON l.ID = ll.LotID;

/* ---------------- 7. sotuv: shartnoma -> buyurtma -> invoys -> sotuv omboridan chiqim ---------------- */
-- mijoz -> mahsulotlar; har hafta har mijozga invoys; haftalik sotuv = oldingi hafta o'tkazmasining 85%
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

CREATE TABLE #si (K int IDENTITY PRIMARY KEY, S int, CustNo int, D date, InvoiceNo nvarchar(100), OrderNo varchar(50), OrderID int NULL, InvID bigint NULL);
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
CREATE TABLE #sc (CustNo int PRIMARY KEY, ContractNo varchar(50), Issued date, Due date, ID int NULL);
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
CREATE TABLE #se (K int IDENTITY PRIMARY KEY, InvID bigint, InvoiceNo nvarchar(100), D date, ProductID int, PNo varchar(15), Qty decimal(18,3), Price decimal(18,2), UnitID int, ExpID int NULL);
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

/* ---------------- 8. ishlab chiqarish rejasi (PRODUCTPLANS / _DAILY, oyma-oy) ---------------- */
CREATE TABLE #plan (ProductID int, S date, E date, Amount float, ID int NULL);
;WITH m AS (
    SELECT @start AS S, EOMONTH(@start) AS E
    UNION ALL
    SELECT DATEADD(day, 1, E), EOMONTH(DATEADD(day, 1, E)) FROM m WHERE E < EOMONTH(@t))
INSERT INTO #plan (ProductID, S, E, Amount)
SELECT p.ID, m.S, m.E, ROUND(p.DailyQty * (DATEDIFF(day, m.S, m.E) + 1) * 6.0 / 7, -1)
FROM m CROSS JOIN #prod p WHERE p.IsRaw = 0;
MERGE dbo.PRODUCTPLANS AS tg
USING #plan AS s ON 1 = 0
WHEN NOT MATCHED THEN INSERT (ProductID, Amount, IsDeleted, StartDate, DueDate, SalesOrderID) VALUES (s.ProductID, s.Amount, 0, s.S, s.E, NULL)
OUTPUT inserted.ProductID * 100000 + DATEDIFF(day, '20000101', inserted.StartDate), inserted.ID INTO #map (K, ID);
UPDATE pl SET ID = m.ID FROM #plan pl JOIN #map m ON m.K = pl.ProductID * 100000 + DATEDIFF(day, '20000101', pl.S);
TRUNCATE TABLE #map;
;WITH n AS (SELECT TOP (40) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k FROM sys.all_objects)
INSERT INTO dbo.PRODUCTPLANS_DAILY (PlanID, Day, DayShift, NightShift)
SELECT pl.ID, DATEADD(day, n.k, pl.S), CEILING(pl.Amount / (DATEDIFF(day, pl.S, pl.E) + 1)), 0
FROM #plan pl JOIN n ON n.k <= DATEDIFF(day, pl.S, pl.E);

/* ---------------- 9. tannarx (TannarxService: material + 2% tashuv + ish haqi; 5/5/5/10%; QQS 12%) ---------------- */
;WITH mat AS (
    SELECT b.FG, SUM(b.Qty * m.Price) AS MaterialPrice FROM #bom b JOIN #prod m ON m.ID = b.Mat GROUP BY b.FG),
mon AS (SELECT DATEADD(month, -k, @t) AS D FROM (VALUES (0), (1), (2)) v (k)),
c AS (
    SELECT p.ID, YEAR(mon.D) AS Y, MONTH(mon.D) AS M, ROUND(mat.MaterialPrice, 4) AS Mt, ROUND(mat.MaterialPrice * 0.02, 4) AS Tr, ROUND(p.Price * 0.06, 4) AS Lb
    FROM #prod p JOIN mat ON mat.FG = p.ID CROSS JOIN mon)
INSERT INTO dbo.TANNARX (ProductID, Year, Month, MonthString, MaterialPrice, MaterialTransportCost, LaborCost, TaxAndBank, AdministrativeExpenses,
                         OtherExpenses, Profit, TaxAndBankPercent, AdministrativeExpensesPercent, OtherExpensesPercent, ProfitPercent,
                         SalesPriceEXWWithoutVAT, SalesPriceEXWWithVAT, SetPriceEXWWithVAT, FinishedProductTransportCost,
                         SalesPriceCIPWithoutVAT, SalesPriceCIPWithVAT, SetPriceCIPWithVAT, CreatedByUserID, CreatedDT, IsDeleted)
SELECT c.ID, c.Y, c.M,
       CHOOSE(c.M, 'Yanvar', 'Fevral', 'Mart', 'Aprel', 'May', 'Iyun', 'Iyul', 'Avgust', 'Sentabr', 'Oktabr', 'Noyabr', 'Dekabr') + '-' + RIGHT('0' + CAST(c.Y % 100 AS varchar(2)), 2),
       c.Mt, c.Tr, c.Lb, b.B * 0.05, b.B * 0.05, b.B * 0.05, b.B * 0.10, 5, 5, 5, 10,
       b.B * 1.25, b.B * 1.25 * 1.12, b.B * 1.25 * 1.12, NULL, b.B * 1.25, b.B * 1.25 * 1.12, b.B * 1.25 * 1.12, @u, DATEFROMPARTS(c.Y, c.M, 1), 0
FROM c CROSS APPLY (SELECT c.Mt + c.Tr + c.Lb AS B) b;

/* ---------------- 10. ombor qoldiqlari: harakatlardan; manfiy qoldiq bo'lmasligi tekshiriladi ---------------- */
CREATE TABLE #mv (WH int, ProductID int, At datetime, Ord int, Qty float, UnitID int);
INSERT INTO #mv SELECT i.ReceivingWHID, ip.ProductID, i.IssueDateTime, 0, ip.Amount, ip.UnitID
FROM dbo.INCOMES i JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID WHERE i.RecieveStatus = 1 AND ISNULL(i.IsDeleted, 0) = 0;
INSERT INTO #mv SELECT e.SenderWHID, ep.ProductID, e.IssueDateTime, 1, -ep.Amount, ep.UnitID
FROM dbo.EXPENSES e JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ExpenseID = e.ID WHERE e.IsDeleted = 0;
IF EXISTS (SELECT 1 FROM (SELECT SUM(Qty) OVER (PARTITION BY WH, ProductID ORDER BY At, Ord ROWS UNBOUNDED PRECEDING) AS Bal FROM #mv) x WHERE x.Bal < -0.0001)
    THROW 50105, 'Demo: biror vaqtda ombor qoldig''i manfiy bo''lib qoldi', 1;
INSERT INTO dbo.PRODUCT_STOCKS (WHID, ProductID, Amount, UnitID)
SELECT WH, ProductID, ROUND(SUM(Qty), 4), MIN(UnitID) FROM #mv GROUP BY WH, ProductID HAVING ROUND(SUM(Qty), 4) > 0;

PRINT 'Demo operatsiyalar: OK';
