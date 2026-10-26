/* =====================================================================
   tahsinERP — DEMO: Logistika — ekspeditorlar, shartnoma va tariflar, yo'l varaqalari, to'lovlar (230_Crm_Sales.sql dan keyin).

   WaybillService shaklida: WaybillNo 'TB_NNNN', tarifdan TransportTypeID/FreightBasis/Rate/CurrencyID, summa = reys narxi
   (FreightBasis 0 — PerTruck). Kiruvchi reyslar xarid invoysiga (P_INVOICES, Srm demo bo'lsa), chiquvchilari sotuv invoysiga
   (SALES_INVOICES, Crm demo bo'lsa) bog'lanadi; ikkalasi ham bo'lmasa — ichki tashuvlar. 7 kundan eski reyslar to'langan:
   F_WAYBILL_PAYMENTS + FinancePostingService.PostWaybillPayment ko'zgusi (FIN_TRANSACTIONS 'WaybillPayment', bank hisobi,
   MATERIAL_TRANSPORT / FG_TRANSPORT kategoriyasi, CounterpartyType 4).
   Faqat Logistics moduli tanlangan bo'lsa. Idempotent: F_WAYBILLS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Logistics') = 0 OR EXISTS (SELECT 1 FROM dbo.F_WAYBILLS) OR NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo logistika: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uzs int = (SELECT ID FROM dbo.CURRENCIES WHERE Shortname = 'UZS');
DECLARE @bank int = (SELECT ID FROM dbo.FIN_ACCOUNTS WHERE Code = '9001' AND IsBank = 1 AND IsActive = 1);
DECLARE @catMat int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'MATERIAL_TRANSPORT');
DECLARE @catFg int = (SELECT ID FROM dbo.FIN_CATEGORIES WHERE SystemCode = 'FG_TRANSPORT');
DECLARE @isuzu int = (SELECT ID FROM dbo.F_TRANSPORT_TYPES WHERE ID = 7);
DECLARE @tent int = (SELECT ID FROM dbo.F_TRANSPORT_TYPES WHERE ID = 11);
DECLARE @labo int = (SELECT ID FROM dbo.F_TRANSPORT_TYPES WHERE ID = 5);

/* ---------------- ekspeditorlar va shartnomalar (IsDeleted = 0 — dropdownlar IsDeleted == false) ---------------- */
INSERT INTO dbo.FORWARDERS (ForwarderName, Director, ContactPerson, Description, IsDeleted)
VALUES ('"Tez Yetkaz Logistik" MChJ', 'Aliyev Rustam', 'Aliyev Sardor', 'Shahar ichi va viloyatlararo yuk tashish', 0),
       ('"Ipak Yo''li Trans" MChJ',  'Hamidov Bahodir', 'Hamidov Jasur', 'Og''ir yuklar, tentli fura', 0);
CREATE TABLE #fw (No int PRIMARY KEY, ID int, Name varchar(50) COLLATE DATABASE_DEFAULT, ContractID int NULL);
INSERT INTO #fw (No, ID, Name)
SELECT CASE WHEN ForwarderName LIKE '%Tez%' THEN 1 ELSE 2 END, ID, ForwarderName
FROM dbo.FORWARDERS WHERE ForwarderName IN ('"Tez Yetkaz Logistik" MChJ', '"Ipak Yo''li Trans" MChJ');

INSERT INTO dbo.F_CONTRACTS (ContractNo, CompanyID, ForwarderID, IssueDate, DueDate, Amount, Description, IsDeleted, CurrencyID)
SELECT 'FC-' + CAST(YEAR(DATEADD(day, -90, @t)) AS varchar(4)) + '-' + RIGHT('00' + CAST(f.No AS varchar(2)), 2), 1, f.ID,
       DATEADD(day, -90, @t), DATEADD(day, 275, @t), CASE f.No WHEN 1 THEN 60000000 ELSE 120000000 END,
       'Yuk tashish xizmatlari shartnomasi', 0, @uzs
FROM #fw f;
UPDATE f SET ContractID = c.ID FROM #fw f JOIN dbo.F_CONTRACTS c ON c.ForwarderID = f.ID;

-- tariflar: reys narxi (FreightBasis 0 = PerTruck)
CREATE TABLE #tf (K int IDENTITY PRIMARY KEY, FwNo int, FromCity nvarchar(100) COLLATE DATABASE_DEFAULT, ToCity nvarchar(100) COLLATE DATABASE_DEFAULT,
                  TypeID int, Rate decimal(19,6), Direction tinyint, ID int NULL);
INSERT INTO #tf (FwNo, FromCity, ToCity, TypeID, Rate, Direction) VALUES
(1, N'Toshkent', N'Toshkent',  @labo,  350000, 2),
(1, N'Qarshi',   N'Toshkent',  @isuzu, 3200000, 0),
(2, N'Toshkent', N'Samarqand', @tent,  4800000, 1),
(2, N'Toshkent', N'Andijon',   @tent,  5200000, 1);
INSERT INTO dbo.F_CONTRACT_TARIFFS (ContractID, FromRegion, FromCity, ToRegion, ToCity, TransportTypeID, FreightBasis, Rate, MinimumCharge, CurrencyID, IsActive, Description, CreatedDT)
SELECT f.ContractID, NULL, x.FromCity, NULL, x.ToCity, x.TypeID, 0, x.Rate, NULL, @uzs, 1, x.FromCity + N' → ' + x.ToCity, DATEADD(day, -90, CAST(@t AS datetime))
FROM #tf x JOIN #fw f ON f.No = x.FwNo
ORDER BY x.K;
UPDATE x SET ID = tt.ID
FROM #tf x JOIN #fw f ON f.No = x.FwNo
JOIN dbo.F_CONTRACT_TARIFFS tt ON tt.ContractID = f.ContractID AND tt.FromCity = x.FromCity AND tt.ToCity = x.ToCity;

/* ---------------- yo'l varaqalari ---------------- */
CREATE TABLE #wb (K int IDENTITY PRIMARY KEY, TariffK int, At datetime, InvoiceID int NULL, SalesInvoiceID bigint NULL, Weight decimal(18,4),
                  Descr varchar(200) COLLATE DATABASE_DEFAULT, No varchar(50) COLLATE DATABASE_DEFAULT NULL, ID int NULL);
-- kiruvchi: so'nggi 5 xarid invoysi (Qarshi → Toshkent)
INSERT INTO #wb (TariffK, At, InvoiceID, SalesInvoiceID, Weight, Descr)
SELECT TOP (5) 2, DATEADD(hour, 8, CAST(i.InvoiceDate AS datetime)), i.ID, NULL, 2800, 'Xomashyo: ' + i.InvoiceNo
FROM dbo.P_INVOICES i WHERE ISNULL(i.IsDeleted, 0) = 0 ORDER BY i.InvoiceDate DESC, i.ID DESC;
-- chiquvchi: so'nggi 6 sotuv invoysi (navbat bilan Samarqand / Andijon)
INSERT INTO #wb (TariffK, At, InvoiceID, SalesInvoiceID, Weight, Descr)
SELECT 3 + (ROW_NUMBER() OVER (ORDER BY x.InvoiceDate DESC, x.ID DESC) % 2), DATEADD(hour, 13, CAST(x.InvoiceDate AS datetime)), NULL, x.ID, 6500,
       'Tayyor mahsulot: ' + CAST(x.InvoiceNo AS varchar(100))
FROM (SELECT TOP (6) ID, InvoiceNo, InvoiceDate FROM dbo.SALES_INVOICES WHERE StatusCode = 'APPROVED' ORDER BY InvoiceDate DESC, ID DESC) x;
-- hujjatsiz ichki tashuvlar (har doim; boshqa modul bo'lmasa ham ro'yxat bo'sh qolmasin)
INSERT INTO #wb (TariffK, At, InvoiceID, SalesInvoiceID, Weight, Descr)
SELECT 1, DATEADD(hour, 11, CAST(DATEADD(day, -v.D, @t) AS datetime)), NULL, NULL, 400, v.Descr
FROM (VALUES (33, 'Omborlar orasida ko''chirish'), (19, 'Ofis jihozlari'), (4, 'Qadoq materiallari namunasi')) v (D, Descr);

;WITH n AS (SELECT No, ROW_NUMBER() OVER (ORDER BY At, K) AS R FROM #wb)
UPDATE n SET No = 'TB_' + RIGHT('0000' + CAST(R AS varchar(10)), 4);

INSERT INTO dbo.F_WAYBILLS (WaybillNo, ContractID, TransportTypeID, InvoiceID, CBM, GrWeight, Description, IsDeleted, Amount, IssueDate, Direction,
                            FromLocation, ToLocation, DistanceKm, FreightBasis, Rate, CurrencyID, PayerType, PayerID, SalesInvoiceID, ForwarderID,
                            TruckNo, TrailerNo, TruckModel, TrailerModel, DriverName, DriverPINFL, NetWeight, PalletsCount, ManualCorrectionReason,
                            PlannedDeliveryDate, ActualDeliveryDate, TTNKID, SalesOrderID, PurchaseOrderID, TariffID)
SELECT w.No, f.ContractID, x.TypeID, w.InvoiceID, NULL, w.Weight,
       w.Descr, 0, x.Rate, w.At, x.Direction, x.FromCity, x.ToCity, NULL, 0, x.Rate, @uzs, 0, NULL, w.SalesInvoiceID, f.ID,
       CASE f.No WHEN 1 THEN '01 A ' ELSE '10 B ' END + RIGHT('000' + CAST(100 + w.K * 7 AS varchar(10)), 3) + CASE f.No WHEN 1 THEN ' AA' ELSE ' BB' END,
       NULL, NULL, NULL, CASE f.No WHEN 1 THEN N'Aliyev Sardor' ELSE N'Hamidov Jasur' END, NULL, NULL, NULL, NULL,
       DATEADD(day, 1, w.At), CASE WHEN w.At < DATEADD(day, -1, CAST(@t AS datetime)) THEN DATEADD(day, 1, w.At) END, NULL, NULL, NULL, x.ID
FROM #wb w JOIN #tf x ON x.K = w.TariffK JOIN #fw f ON f.No = x.FwNo;
UPDATE w SET ID = b.ID FROM #wb w JOIN dbo.F_WAYBILLS b ON b.WaybillNo = w.No;

/* ---------------- to'lovlar: 7 kundan eski reyslar (bitta reys — bitta to'lov, split shart emas) ---------------- */
CREATE TABLE #pay (WbID int PRIMARY KEY, FwID int, FwName varchar(50) COLLATE DATABASE_DEFAULT, WaybillNo varchar(50) COLLATE DATABASE_DEFAULT,
                   Amount decimal(19,4), PayAt datetime, IsFg bit, PayID int NULL);
INSERT INTO #pay (WbID, FwID, FwName, WaybillNo, Amount, PayAt, IsFg)
SELECT b.ID, b.ForwarderID, f.ForwarderName, b.WaybillNo, b.Amount, DATEADD(day, 5, b.IssueDate), CASE WHEN b.SalesInvoiceID IS NOT NULL THEN 1 ELSE 0 END
FROM dbo.F_WAYBILLS b JOIN dbo.FORWARDERS f ON f.ID = b.ForwarderID
WHERE b.IssueDate < DATEADD(day, -7, CAST(@t AS datetime));

INSERT INTO dbo.F_WAYBILL_PAYMENTS (ForwarderID, WaybillID, Payment_Amount, IsDeleted, Comment, CurrencyID, PaymentDate)
SELECT FwID, WbID, Amount, 0, 'Yo''l varaqasi ' + WaybillNo + ' bo''yicha to''lov', @uzs, PayAt FROM #pay ORDER BY PayAt;
UPDATE p SET PayID = x.ID FROM #pay p JOIN dbo.F_WAYBILL_PAYMENTS x ON x.WaybillID = p.WbID;

IF @bank IS NOT NULL AND @catMat IS NOT NULL AND @catFg IS NOT NULL
    INSERT INTO dbo.FIN_TRANSACTIONS (TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID, Qty, PricePerUnit, CounterpartyType, CounterpartyID,
                                      CounterpartyName, Purpose, SourceDocType, SourceDocID, ContractNo, Comment, CreatedByUserID, CreatedDT, IsDeleted)
    SELECT CAST(PayAt AS date), @bank, CASE WHEN IsFg = 1 THEN @catFg ELSE @catMat END, 2, Amount, @uzs, NULL, NULL, 4, FwID, FwName,
           N'Yo''l varaqasi bo''yicha yuk tashish to''lovi: ' + WaybillNo, 'WaybillPayment', PayID, NULL, NULL, @u, PayAt, 0
    FROM #pay;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #fw, #tf, #wb, #pay;

PRINT 'Demo logistika: OK';
