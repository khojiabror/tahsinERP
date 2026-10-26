/* =====================================================================
   tahsinERP — DEMO: Rasmiy hujjatlar (Docs) — rasmiy xatlar va korxona buyruqlari (250_Hr_Payroll.sql dan keyin).

   DocumentService shaklida: avval DOC_DOCUMENTS sarlavhasi (DocTypeID — DOC_DOCUMENT_TYPES kodi, holat — DOC_DOCUMENT_STATUS_REF),
   so'ng COM_OFFICIAL_LETTERS / ADM_COMPANY_ORDERS (DocumentID = sarlavha ID). Chiquvchi xat va buyruq raqami 4 xonali ('0001'),
   kiruvchi xat sarlavhasi 'IN-' + jo'natuvchi raqami. HR demo bo'lsa buyruqlar bo'linmaga bog'lanadi.
   Fayl talab qiladigan standartlar / lavozim yo'riqnomalari / shablonlar kiritilmaydi.
   Faqat Docs moduli tanlangan bo'lsa. Idempotent: COM_OFFICIAL_LETTERS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Docs') = 0 OR EXISTS (SELECT 1 FROM dbo.COM_OFFICIAL_LETTERS) OR NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo hujjatlar: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @tLetter int = (SELECT ID FROM dbo.DOC_DOCUMENT_TYPES WHERE Code = 'OFFICIAL_LETTER');
DECLARE @tOrder int = (SELECT ID FROM dbo.DOC_DOCUMENT_TYPES WHERE Code = 'COMP_ORDER');
IF @tLetter IS NULL OR @tOrder IS NULL THROW 50281, 'Demo hujjatlar: DOC_DOCUMENT_TYPES seed topilmadi', 1;
DECLARE @ouRah int = (SELECT TOP 1 ID FROM dbo.HR_ORG_UNITS WHERE Code = N'RAH' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @ouIsh int = (SELECT TOP 1 ID FROM dbo.HR_ORG_UNITS WHERE Code = N'ISH' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @ouSav int = (SELECT TOP 1 ID FROM dbo.HR_ORG_UNITS WHERE Code = N'SAV' AND ISNULL(IsDeleted, 0) = 0);
CREATE TABLE #map (K int, ID bigint);

/* ---------------- rasmiy xatlar ---------------- */
CREATE TABLE #l (K int IDENTITY PRIMARY KEY, Dir varchar(3) COLLATE DATABASE_DEFAULT, LetterNo nvarchar(50) COLLATE DATABASE_DEFAULT, D date, Company nvarchar(250) COLLATE DATABASE_DEFAULT,
                 Contact nvarchar(200) COLLATE DATABASE_DEFAULT NULL, ContactPos nvarchar(200) COLLATE DATABASE_DEFAULT NULL, Channel varchar(20) COLLATE DATABASE_DEFAULT,
                 Topic nvarchar(500) COLLATE DATABASE_DEFAULT, Status varchar(20) COLLATE DATABASE_DEFAULT, Greeting nvarchar(500) COLLATE DATABASE_DEFAULT NULL,
                 Body nvarchar(max) COLLATE DATABASE_DEFAULT NULL, DocID bigint NULL);
INSERT INTO #l (Dir, LetterNo, D, Company, Contact, ContactPos, Channel, Topic, Status, Greeting, Body) VALUES
('OUT', N'0001', DATEADD(day, -40, @t), N'"Uy-Ro''zg''or Savdo" MChJ', N'Rahimov Anvar', N'Direktor', 'EMAIL', N'Yangi narxlar ro''yxati to''g''risida', 'APPROVED',
     N'Hurmatli Anvar aka!', N'<p>Sizga keyingi oydan amal qiladigan yangilangan narxlar ro''yxatini yuboramiz.</p><p>Hurmat bilan, "Demo Plast" MChJ.</p>'),
('OUT', N'0002', DATEADD(day, -21, @t), N'"Sho''rtan Polimer Savdo" MChJ', N'Normatov Akmal', N'Direktor', 'LETTER', N'Yetkazib berish jadvalini kelishish', 'APPROVED',
     N'Hurmatli Akmal Normatovich!', N'<p>Keyingi chorak uchun granula yetkazib berish jadvalini kelishib olishni so''raymiz.</p>'),
('OUT', N'0003', DATEADD(day, -3, @t), N'Hududiy elektr tarmoqlari', N'Abonent bo''limi', NULL, 'COURIER', N'Quvvatni oshirish bo''yicha ariza', 'DRAFT',
     N'Hurmatli abonent bo''limi!', N'<p>Ishlab chiqarish quvvatini 150 kVt ga oshirish uchun texnik shartlar berishingizni so''raymiz.</p>'),
('IN',  N'312/K', DATEADD(day, -15, @t), N'"Bozor Plus" MChJ', N'Saidov Jamshid', N'Direktor', 'EMAIL', N'Kiruvchi xat: "Bozor Plus" MChJ', 'APPROVED', NULL, NULL),
('IN',  N'05-118', DATEADD(day, -6, @t), N'Davlat soliq qo''mitasi', NULL, NULL, 'LETTER', N'Kiruvchi xat: Davlat soliq qo''mitasi', 'FOR_APPROVAL', NULL, NULL);

MERGE dbo.DOC_DOCUMENTS AS tg
USING (SELECT l.*, s.ID AS StatusID FROM #l l JOIN dbo.DOC_DOCUMENT_STATUS_REF s ON s.Code = l.Status) AS x ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocTypeID, StatusID, Code, Title, CreatedByUserID, ApprovedByUserID, CreatedDT, UpdatedDT, IntegrationUID, IsDeleted)
     VALUES (@tLetter, x.StatusID, CASE WHEN x.Dir = 'IN' THEN N'IN-' + x.LetterNo ELSE x.LetterNo END, x.Topic,
             @u, CASE WHEN x.Status = 'APPROVED' THEN @u END, DATEADD(hour, 10, CAST(x.D AS datetime2(0))), NULL, NEWID(), 0)
OUTPUT x.K, inserted.ID INTO #map (K, ID);
UPDATE l SET DocID = m.ID FROM #l l JOIN #map m ON m.K = l.K;
TRUNCATE TABLE #map;

INSERT INTO dbo.COM_OFFICIAL_LETTERS (DocumentID, DirectionCode, LetterNo, LetterDate, ExternalCompanyName, ExternalContactName, OurRefNo, TheirRefNo,
                                      ChannelCode, Topic, ExternalContactPosition, IsDeleted, BodyHtml, GreetingLine)
SELECT DocID, Dir, LetterNo, D, Company, Contact, NULL, CASE WHEN Dir = 'IN' THEN LetterNo END, Channel, Topic, ContactPos, 0, Body, Greeting
FROM #l;

/* ---------------- korxona buyruqlari ---------------- */
CREATE TABLE #o (K int IDENTITY PRIMARY KEY, OrderNo nvarchar(50) COLLATE DATABASE_DEFAULT, D date, TypeCode varchar(50) COLLATE DATABASE_DEFAULT, OrgUnitID int NULL,
                 Title nvarchar(500) COLLATE DATABASE_DEFAULT, Notes nvarchar(max) COLLATE DATABASE_DEFAULT, Status varchar(20) COLLATE DATABASE_DEFAULT, DocID bigint NULL);
INSERT INTO #o (OrderNo, D, TypeCode, OrgUnitID, Title, Notes, Status) VALUES
(N'0001', DATEADD(day, -60, @t), 'GENERAL', @ouRah, N'ERP tizimini joriy etish to''g''risida',
     N'1. Korxonada tahsinERP tizimi joriy etilsin.' + CHAR(10) + N'2. Bo''lim boshliqlari o''z bo''limi ma''lumotlarini kiritishni ta''minlasin.' + CHAR(10) + N'3. Nazorat direktor zimmasida qolsin.', 'APPROVED'),
(N'0002', DATEADD(day, -30, @t), 'URGENT', @ouIsh, N'Inventarizatsiya o''tkazish to''g''risida',
     N'Barcha omborlarda oy yakuni bo''yicha inventarizatsiya o''tkazilsin. Natijalar 3 kun ichida taqdim etilsin.', 'APPROVED'),
(N'0003', DATEADD(day, -2, @t), 'GENERAL', @ouSav, N'Savdo bo''limi xodimlarini rag''batlantirish to''g''risida',
     N'Choraklik reja bajarilgani uchun savdo bo''limi xodimlariga mukofot berilsin.', 'DRAFT');

MERGE dbo.DOC_DOCUMENTS AS tg
USING (SELECT o.*, s.ID AS StatusID FROM #o o JOIN dbo.DOC_DOCUMENT_STATUS_REF s ON s.Code = o.Status) AS x ON 1 = 0
WHEN NOT MATCHED THEN INSERT (DocTypeID, StatusID, Code, Title, OrgUnitID, CreatedByUserID, ApprovedByUserID, CreatedDT, UpdatedDT, IntegrationUID, IsDeleted)
     VALUES (@tOrder, x.StatusID, x.OrderNo, x.Title, x.OrgUnitID, @u, CASE WHEN x.Status = 'APPROVED' THEN @u END,
             DATEADD(hour, 9, CAST(x.D AS datetime2(0))), NULL, NEWID(), 0)
OUTPUT x.K, inserted.ID INTO #map (K, ID);
UPDATE o SET DocID = m.ID FROM #o o JOIN #map m ON m.K = o.K;

INSERT INTO dbo.ADM_COMPANY_ORDERS (DocumentID, OrderNo, OrderDate, OrderTypeCode, OrgUnitID, Notes, IsDeleted)
SELECT DocID, OrderNo, D, TypeCode, OrgUnitID, Notes, 0 FROM #o;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #map, #l, #o;

PRINT 'Demo hujjatlar: OK';
