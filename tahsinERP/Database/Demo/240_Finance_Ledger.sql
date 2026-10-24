/* =====================================================================
   tahsinERP — DEMO: Moliya (Finance) — ustav kapitali, invoyslar ko'zgu yozuvlari, to'lovlar, doimiy xarajatlar
   (230_Crm_Sales.sql dan keyin).

   FIN_TRANSACTIONS servislar yozadigan shaklda:
     'PurchaseInvoice' / 'SalesInvoice' — FinancePostingService ko'zgu yozuvlari (PAYABLES_{cur} / RECEIVABLES_{cur},
                                          PURCHASE / SALES kategoriyalari; real pul oqimiga kirmaydi)
     'PurchaseInvoicePayment'          — ta'minotchiga to'lov (FinTransactionsController, invoysga bog'langan)
     'Manual'                          — mijoz to'lovi (ContractNo bilan), ijara/kommunal/bank xizmati, xomashyo tashuvi
     'ShareholderContribution'         — ustav kapitaliga hissa (ShareholderController, kassaga)
   Invoyslar faqat tegishli modul (Srm / Crm) demo hujjatlari bor bo'lsa yoziladi. Xomashyo tashuvi qo'lda yozuvi Logistika
   tanlanmaganda — aks holda tashuv to'lovi 270_Logistics_Waybills.sql yo'l varaqasi to'lovidan keladi (ikki marta hisoblanmasin).
   Oylik to'lovi (HR_PAYROLL) — 250_Hr_Payroll.sql.

   Faqat Finance moduli tanlangan bo'lsa. Idempotent: SHAREHOLDERS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Finance') = 0 OR EXISTS (SELECT 1 FROM dbo.SHAREHOLDERS) OR NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo moliya: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @kassa int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9000');
DECLARE @bank int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9001');
DECLARE @catSales int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'SALES');
DECLARE @catPurchase int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'PURCHASE');
DECLARE @catCapital int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'SHAREHOLDER_CONTRIBUTION');
-- tizim kodi yo'q kategoriyalar: seed ID'lari (P&L moddalariga bog'langan)
DECLARE @catMaterial int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE ID = 24 AND Type = 2);
DECLARE @catRent int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE ID = 19 AND Type = 2);
DECLARE @catUtil int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE ID = 21 AND Type = 2);
DECLARE @catBankFee int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE ID = 15 AND Type = 2);
DECLARE @catTransport int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'MATERIAL_TRANSPORT');
IF @kassa IS NULL OR @bank IS NULL OR @catSales IS NULL OR @catPurchase IS NULL OR @catCapital IS NULL
    THROW 50201, 'Demo moliya: 200_Core_Master.sql natijasi yoki seed kategoriyalari topilmadi', 1;
SET @catMaterial = ISNULL(@catMaterial, @catPurchase);
IF dbo.fn_SetupHasModule('Logistics') = 1 SET @catTransport = NULL;

/* ---------------- 1. kontragent hisoblari (FinancePostingService.EnsureCounterpartyAccount) ---------------- */
INSERT INTO dbo.FIN_ACCOUNTS (Code, Name, OwnerName, CurrencyID, IsCash, IsBank, IsActive, CreatedByUserID, CreatedDT, BalanceCategoryID, IsDeleted)
SELECT v.Code, v.Name, NULL, @uzs, 0, 0, 1, @u, DATEADD(day, -90, CAST(@t AS datetime)), NULL, NULL
FROM (VALUES ('PAYABLES_' + CAST(@uzs AS varchar(10)), N'Kreditorlar qarzi'),
             ('RECEIVABLES_' + CAST(@uzs AS varchar(10)), N'Debitorlar qarzi')) v (Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.FIN_ACCOUNTS a WHERE a.Code = v.Code);
DECLARE @accAP int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = 'PAYABLES_' + CAST(@uzs AS varchar(10)));
DECLARE @accAR int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = 'RECEIVABLES_' + CAST(@uzs AS varchar(10)));

/* ---------------- 2. ustav kapitali: aksiyadorlar va hissalar (naqd, kassaga) ---------------- */
INSERT INTO dbo.SHAREHOLDERS (FullName, Phone, Notes, IsActive, IsDeleted)
VALUES (N'Karimov Aziz Bahodirovich', N'+998 90 900-00-01', N'Ta''sischi, direktor', 1, 0),
       (N'Sobirova Gulnora Akmalovna', N'+998 90 900-00-02', N'Ta''sischi', 1, 0);
INSERT INTO dbo.SHAREHOLDER_SHARE_PERIODS (ShareholderID, FromDate, ToDate, SharePercent)
SELECT ID, DATEADD(day, -100, @t), NULL, CASE WHEN FullName LIKE N'Karimov%' THEN 60 ELSE 40 END
FROM dbo.SHAREHOLDERS WHERE FullName IN (N'Karimov Aziz Bahodirovich', N'Sobirova Gulnora Akmalovna');

DECLARE @capDate date = DATEADD(day, -98, @t);
DECLARE @capital decimal(18,2) = 1500000000;
CREATE TABLE #sh (ShID int, Name nvarchar(200) COLLATE DATABASE_DEFAULT, Amount decimal(18,2), FinID int NULL, ContribID int NULL);
INSERT INTO #sh (ShID, Name, Amount)
SELECT s.ID, s.FullName, ROUND(@capital * sp.SharePercent / 100, 2)
FROM dbo.SHAREHOLDERS s JOIN dbo.SHAREHOLDER_SHARE_PERIODS sp ON sp.ShareholderID = s.ID AND sp.ToDate IS NULL
WHERE s.IsDeleted = 0;

INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT @capDate, @kassa, @catCapital, 1, Amount, @uzs, NULL, NULL, 3, ShID, Name, N'Aksiyador hissasi', 'ShareholderContribution', NULL, NULL,
       N'Ustav kapitaliga hissa', @u, DATEADD(hour, 10, CAST(@capDate AS datetime)), 0
FROM #sh;
UPDATE s SET FinID = f.ID FROM #sh s JOIN dbo.FIN_TRANSACTIONS f ON f.SourceDocType = 'ShareholderContribution' AND f.CounterpartyID = s.ShID;
INSERT INTO dbo.SHAREHOLDER_CONTRIBUTIONS (ShareholderID, ContributionDate, Amount, CurrencyID, Description, ContributionType, FinTransactionID)
SELECT ShID, @capDate, Amount, @uzs, N'Ustav kapitaliga hissa', 1, FinID FROM #sh;

/* ---------------- 3. xarid invoyslari (Srm demo bo'lsa): ko'zgu yozuv + to'lov (14 kundan keyin) ---------------- */
CREATE TABLE #pinv (ID int PRIMARY KEY, InvoiceNo varchar(50) COLLATE DATABASE_DEFAULT, D date, SupplierID int, SupName varchar(50) COLLATE DATABASE_DEFAULT, SupDuns varchar(50) COLLATE DATABASE_DEFAULT, ContractNo varchar(50) COLLATE DATABASE_DEFAULT, Amount decimal(18,2));
INSERT INTO #pinv
SELECT i.ID, i.InvoiceNo, i.InvoiceDate, i.SupplierID, s.Name, s.DUNS, c.ContractNo,
       ISNULL((SELECT SUM(CAST(x.TotalPrice AS decimal(18,2))) FROM dbo.INCOMES x WHERE x.InvoiceID = i.ID AND ISNULL(x.IsDeleted, 0) = 0), i.Amount)
FROM dbo.P_INVOICES i JOIN dbo.SUPPLIERS s ON s.ID = i.SupplierID JOIN dbo.P_CONTRACTS c ON c.ID = i.ContractID
WHERE ISNULL(i.IsDeleted, 0) = 0;

INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT D, @accAP, @catPurchase, 2, Amount, @uzs, NULL, NULL, 2, SupplierID, SupName, N'Xarid invoysi #' + InvoiceNo, 'PurchaseInvoice', ID, NULL, NULL,
       @u, DATEADD(minute, 600, CAST(D AS datetime)), 0
FROM #pinv;

-- to'lov: 3-ta'minotchiga oxirgi to'langan invoys bo'yicha 60% (qisman qarz qoladi); 14 kundan keyin
INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT pd.PayDate, CASE WHEN pd.PayDate < DATEADD(day, -10, @t) THEN @kassa ELSE @bank END, @catMaterial, 2,
       CASE WHEN p.SupDuns = '313333333' AND p.D > DATEADD(day, -30, @t) THEN ROUND(p.Amount * 0.6, 0) ELSE p.Amount END,
       @uzs, NULL, NULL, 2, p.SupplierID, p.SupName, N'To''lov: xarid invoysi ' + p.InvoiceNo, 'PurchaseInvoicePayment', p.ID, p.ContractNo, NULL,
       @u, DATEADD(hour, 12, CAST(pd.PayDate AS datetime)), 0
FROM #pinv p CROSS APPLY (SELECT DATEADD(day, 14, p.D) AS PayDate) pd
WHERE pd.PayDate < @t;

-- xomashyo tashuvi (har kirimga), boshqa kontragent — Logistika tanlanmaganda
INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT D, @kassa, @catTransport, 2, 1200000, @uzs, NULL, NULL, 5, NULL, N'Yuk tashish xizmati', N'Xomashyo tashuvi: ' + InvoiceNo, 'Manual', NULL, NULL, NULL,
       @u, DATEADD(hour, 15, CAST(D AS datetime)), 0
FROM #pinv WHERE @catTransport IS NOT NULL;

/* ---------------- 4. sotuv invoyslari (Crm demo bo'lsa): ko'zgu yozuv + mijoz to'lovi (bank, ContractNo bilan) ---------------- */
CREATE TABLE #sinv (ID bigint PRIMARY KEY, InvoiceNo nvarchar(100) COLLATE DATABASE_DEFAULT, D date, CustomerID int, CustName varchar(50) COLLATE DATABASE_DEFAULT, CustNo int, ContractNo varchar(50) COLLATE DATABASE_DEFAULT, Amount decimal(19,2));
INSERT INTO #sinv
SELECT i.ID, i.InvoiceNo, i.InvoiceDate, i.CustomerID, c.Name, TRY_CAST(LEFT(c.DUNS, 3) AS int) - 300, sc.ContractNo, i.TotalWithVAT
FROM dbo.SALES_INVOICES i JOIN dbo.CUSTOMERS c ON c.ID = i.CustomerID LEFT JOIN dbo.S_CONTRACTS sc ON sc.ID = i.ContractID
WHERE i.StatusCode NOT IN ('CANCELED', 'CANCELLED');

INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT D, @accAR, @catSales, 1, Amount, @uzs, NULL, NULL, 1, CustomerID, CustName, N'Sotuv invoysi #' + InvoiceNo, 'SalesInvoice', CAST(ID AS int), NULL, NULL,
       @u, DATEADD(minute, 665, CAST(D AS datetime)), 0
FROM #sinv;

-- to'lov: (10 + 2 x mijoz №) kundan keyin; 5-mijoz yarmini to'laydi (muddati o'tgan qarz)
INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT pd.PayDate, @bank, @catSales, 1, CASE WHEN s.CustNo = 5 THEN ROUND(s.Amount * 0.5, 0) ELSE s.Amount END, @uzs, NULL, NULL, 1, s.CustomerID, s.CustName,
       N'To''lov: ' + s.InvoiceNo, 'Manual', NULL, s.ContractNo, NULL, @u, DATEADD(hour, 14, CAST(pd.PayDate AS datetime)), 0
FROM #sinv s CROSS APPLY (SELECT DATEADD(day, 10 + 2 * ISNULL(s.CustNo, 3), s.D) AS PayDate) pd
WHERE pd.PayDate < @t;

/* ---------------- 5. doimiy xarajatlar (bank orqali) ---------------- */
;WITH m AS (SELECT DATEFROMPARTS(YEAR(DATEADD(month, -k, @t)), MONTH(DATEADD(month, -k, @t)), 1) AS M1 FROM (VALUES (0), (1), (2), (3)) v (k)),
x AS (
    SELECT DATEADD(day, 4, m.M1) AS D, @catRent AS Cat, CAST(18000000 AS decimal(18,2)) AS Amt, N'"Sergeli Sanoat Park" MChJ' AS Cp, N'Ishlab chiqarish binosi ijarasi' AS Purpose FROM m
    UNION ALL SELECT DATEADD(day, 9, m.M1), @catUtil, 9500000 + 1500000 * (MONTH(m.M1) % 3), N'Hududiy elektr tarmoqlari', N'Elektr energiyasi' FROM m
    UNION ALL SELECT DATEADD(day, 9, m.M1), @catUtil, 1800000, N'Suvoqova', N'Suv ta''minoti' FROM m
    UNION ALL SELECT DATEADD(day, 26, m.M1), @catBankFee, 650000, N'Demo bank', N'Hisob-kassa xizmati' FROM m)
INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                  CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
SELECT D, CASE WHEN D < DATEADD(day, -30, @t) THEN @kassa ELSE @bank END, Cat, 2, Amt, @uzs, NULL, NULL, 5, NULL, Cp, Purpose, 'Manual', NULL, NULL, NULL,
       @u, DATEADD(hour, 11, CAST(D AS datetime)), 0
FROM x WHERE Cat IS NOT NULL AND D > @capDate AND D < @t;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #sh, #pinv, #sinv;

PRINT 'Demo moliya: OK';
