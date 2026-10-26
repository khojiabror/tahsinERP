/* =====================================================================
   tahsinERP — DEMO: Ishlab chiqarish (MES) — ombor o'tkazmalari, ishlab chiqarish, sifat nazorati, reja
   (200_Core_Master.sql va 210_Srm_Purchases.sql dan keyin).

   Hujjatlar servislar yozadigan shaklda (raqam formatlari, tavsiflar, statuslar):
     Ombor  SUPPLY -> PRODUCTION haftalik o'tkazma: EXPENSES + bog'langan INCOMES 'INC-...' (SUPPLY qatlamlaridan FIFO tannarx),
            PRODUCTION ombori DLOC: INCOME_LOTS 'LOT-{inc}-{ip}' + LOT_LOCATIONS
     MES  PRODUCT_PRODUCTION -> INCOMES 'PROD-...' (lot 'PROD-{inc}-{inc}') + BOM bo'yicha xomashyo chiqimi
          (EXPENSE_PRODUCT_ALLOCATIONS — eng eski lotdan, FIFO) -> QUALITY_INSPECTION -> sotuv omboriga 'TRNSF_' o'tkazma
     PRODUCTPLANS / _DAILY — oyma-oy reja.
   Ta'minot (Srm) tanlanmagan bo'lsa SUPPLY omboriga o'tkazmalar uchun yetarli boshlang'ich qoldiq kirimi yoziladi.
   PRODUCT_STOCKS — 299_Core_Finish.sql (barcha harakatlardan, manfiy qoldiq tekshiruvi bilan).

   Faqat Mes moduli tanlangan bo'lsa. Idempotent: PRODUCT_PRODUCTION bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Mes') = 0 OR OBJECT_ID(N'demo_tmp.PP') IS NULL OR EXISTS (SELECT 1 FROM dbo.PRODUCT_PRODUCTION)
BEGIN
    PRINT 'Demo ishlab chiqarish: modul tanlanmagan yoki hujjatlar bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @start date = DATEADD(day, -56, @t);          -- ishlab chiqarish davri: 8 hafta (200 bilan bir xil)
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @kg int = (SELECT ID FROM dbo.UNITS WHERE SystemCode = 'KG');
DECLARE @whS int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SUPPLY' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @whP int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'PRODUCTION' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @whT int = (SELECT MIN(ID) FROM dbo.WAREHOUSES WHERE WarehouseType = 'SALES' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @shop int = (SELECT ShopID FROM dbo.WAREHOUSES WHERE ID = @whP);
IF @u IS NULL OR @whS IS NULL OR @whP IS NULL OR @whT IS NULL OR @shop IS NULL
    THROW 50101, 'Demo ishlab chiqarish: 200_Core_Master.sql natijasi topilmadi (demo user / omborlar / sex)', 1;

-- Ishlab chiqarish ombori DLOC (ProductionDlocGuard faol joy talab qiladi)
INSERT INTO dbo.WAREHOUSE_LOCATIONS (WarehouseID, LocationCode, LocationName, CapacityUomID, MinCapacity, MaxCapacity,
                                     AllowMixedLots, AllowMixedProducts, IsActive, IsDeleted, CreatedAt)
SELECT @whP, v.Code, v.Name, NULL, 0, 1000000, 1, 1, 1, 0, DATEADD(day, -100, @t)
FROM (VALUES ('A-01', N'Sex ombori — granula va tayyor mahsulot'), ('A-02', N'Sex ombori — qadoq va butlovchilar')) v (Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.WAREHOUSE_LOCATIONS l WHERE l.WarehouseID = @whP AND l.LocationCode = v.Code);
DECLARE @locA int = (SELECT TOP 1 ID FROM dbo.WAREHOUSE_LOCATIONS WHERE WarehouseID = @whP AND IsActive = 1 AND IsDeleted = 0 ORDER BY LocationCode);
DECLARE @locB int = (SELECT TOP 1 ID FROM dbo.WAREHOUSE_LOCATIONS WHERE WarehouseID = @whP AND IsActive = 1 AND IsDeleted = 0 ORDER BY LocationCode DESC);

/* ---------------- ssenariy (demo_tmp) ---------------- */
CREATE TABLE #prod (ID int PRIMARY KEY, PNo varchar(15) COLLATE DATABASE_DEFAULT, Name varchar(50) COLLATE DATABASE_DEFAULT, UnitID int, IsRaw bit,
                    Price decimal(18,2), DailyQty int NULL, FgIdx int NULL);
INSERT INTO #prod SELECT ID, PNo, Name, UnitID, IsRaw, Price, DailyQty, FgIdx FROM demo_tmp.PROD;

CREATE TABLE #pp (K int PRIMARY KEY, ProductID int, D date, DayIdx int, Wk int, Qty int, DayShift int, NightShift int,
                  At datetime, Rejected int, PPID int NULL, IncID int NULL, IPID int NULL, LotID int NULL, ExpID int NULL);
INSERT INTO #pp (K, ProductID, D, DayIdx, Wk, Qty, DayShift, NightShift, At, Rejected)
SELECT K, ProductID, D, DayIdx, Wk, Qty, DayShift, NightShift, At, Rejected FROM demo_tmp.PP;

CREATE TABLE #cons (K int PRIMARY KEY, PPK int, Mat int, Qty decimal(18,4), UnitID int, At datetime, EPID int NULL, Cost decimal(18,4) NULL);
INSERT INTO #cons (K, PPK, Mat, Qty, UnitID, At) SELECT K, PPK, Mat, Qty, UnitID, At FROM demo_tmp.CONS;

CREATE TABLE #tr (K int PRIMARY KEY, Wk int, Mat int, Qty decimal(18,4), UnitID int, At datetime, Cost decimal(18,4) NULL,
                  ExpID int NULL, EPID int NULL, IncID int NULL, IPID int NULL, LotID int NULL);
INSERT INTO #tr (K, Wk, Mat, Qty, UnitID, At) SELECT K, Wk, Mat, Qty, UnitID, At FROM demo_tmp.TR;

CREATE TABLE #map (K int, ID int);

/* ---------------- 1. SUPPLY qoldig'i: ta'minot tanlanmagan bo'lsa — boshlang'ich qoldiq kirimi ---------------- */
CREATE TABLE #open (Mat int PRIMARY KEY, Qty decimal(18,4), Price decimal(18,2), UnitID int);
INSERT INTO #open (Mat, Qty, Price, UnitID)
SELECT t.Mat, t.Need - ISNULL(h.Have, 0), p.Price, t.UnitID
FROM (SELECT Mat, UnitID, SUM(Qty) AS Need FROM #tr GROUP BY Mat, UnitID) t
JOIN #prod p ON p.ID = t.Mat
LEFT JOIN (SELECT ip.ProductID, SUM(ip.Amount) AS Have
           FROM dbo.INCOMES i JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID
           WHERE i.ReceivingWHID = @whS AND i.RecieveStatus = 1 AND ISNULL(i.IsDeleted, 0) = 0 GROUP BY ip.ProductID) h ON h.ProductID = t.Mat
WHERE t.Need > ISNULL(h.Have, 0);
IF EXISTS (SELECT 1 FROM #open)
BEGIN
    DECLARE @openAt datetime = DATEADD(hour, 9, CAST(DATEADD(day, -1, @start) AS datetime));
    DECLARE @openDoc varchar(50) = CONVERT(char(6), @openAt, 112) + '_BQ';
    INSERT INTO dbo.INCOMES (DocNo, ReceivingWHID, Amount, TotalPrice, IsDeleted, Description, IssueDateTime, SenderWHID, RecieveStatus, CurrencyID, InvoiceID, ProductionID)
    SELECT @openDoc, @whS, SUM(Qty), SUM(Qty * Price), 0, 'Boshlang''ich qoldiq (xomashyo)', @openAt, NULL, 1, @uzs, NULL, NULL FROM #open;
    DECLARE @openInc int = SCOPE_IDENTITY();
    INSERT INTO dbo.INCOME_PRODUCTS (IncomeID, ProductID, Amount, PiecePrice, Comment, UnitID, VATRate)
    SELECT @openInc, Mat, Qty, Price, N'Boshlang''ich qoldiq', UnitID, NULL FROM #open;
END

/* ---------------- 2. o'tkazma tannarxi: SUPPLY ombori qatlamlaridan FIFO ---------------- */
;WITH L AS (
    SELECT ip.ProductID AS Mat, ip.PiecePrice AS Price, ip.Amount AS Qty,
           SUM(ip.Amount) OVER (PARTITION BY ip.ProductID ORDER BY i.IssueDateTime, ip.ID ROWS UNBOUNDED PRECEDING) AS L1
    FROM dbo.INCOMES i JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID
    WHERE i.ReceivingWHID = @whS AND i.RecieveStatus = 1 AND ISNULL(i.IsDeleted, 0) = 0),
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

-- chiqim hujjati: hafta bo'yicha bitta, DocNo '{yyyyMM}_{n}' (ta'minot kirimlari raqamlaridan keyin)
CREATE TABLE #trd (Wk int PRIMARY KEY, At datetime, DocNo varchar(50) COLLATE DATABASE_DEFAULT, ExpID int NULL, IncID int NULL);
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

/* ---------------- 3. ishlab chiqarish: PRODUCT_PRODUCTION + tayyor mahsulot kirimi + xomashyo chiqimi ---------------- */
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

/* ---------------- 4. PRODUCTION -> SALES o'tkazma (TransferProductionToSales): har haftaning qabul qilingani ---------------- */
CREATE TABLE #ft (Wk int PRIMARY KEY, At datetime, DocNo varchar(50) COLLATE DATABASE_DEFAULT, Pnos varchar(400) COLLATE DATABASE_DEFAULT, ExpID int NULL, IncID int NULL);
INSERT INTO #ft (Wk, At)
SELECT DISTINCT Wk, DATEADD(hour, 7, CAST(DATEADD(day, 7 * (Wk + 1), @start) AS datetime)) FROM #pp WHERE Wk <= 6;
UPDATE f SET DocNo = 'TRNSF_' + REPLACE(CONVERT(char(8), f.At, 3), '/', '') + '_1',
             Pnos = STUFF((SELECT ', ' + p.PNo FROM #prod p WHERE p.ID IN (SELECT ProductID FROM #pp WHERE Wk = f.Wk) ORDER BY p.PNo FOR XML PATH('')), 1, 2, '')
FROM #ft f;

CREATE TABLE #ftl (K int IDENTITY PRIMARY KEY, Wk int, ProductID int, UnitID int, Qty decimal(18,4), Comment varchar(4000) COLLATE DATABASE_DEFAULT, FirstLot int, EPID int NULL);
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

/* ---------------- 5. ishlab chiqarish rejasi (PRODUCTPLANS / _DAILY, oyma-oy) ---------------- */
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

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #prod, #pp, #cons, #tr, #map, #open, #trd, #alloc, #ft, #ftl, #plan;

PRINT 'Demo ishlab chiqarish: OK';
