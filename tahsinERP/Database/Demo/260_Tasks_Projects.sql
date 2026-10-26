/* =====================================================================
   tahsinERP — DEMO: Topshiriqlar (Tasks) — 3 loyiha va 12 topshiriq (200_Core_Master.sql dan keyin).

   TasksController shaklida: KeyCode '{LoyihaKodi}-{n}', holat/ustuvorlik — APP_TASK_STATUS_REF / APP_TASK_PRIORITY_REF kodlari
   bo'yicha; izohlar UserComment'da "[yyyy-MM-dd HH:mm - FullName]:\n matn" (TaskChangeStatus). Yopilganlar UpdatedDT so'nggi
   30 kun ichida (kanban ko'rsatadi), muddati o'tganlari ham bor.
   Faqat Tasks moduli tanlangan bo'lsa. Idempotent: APP_TASKS bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Tasks') = 0 OR EXISTS (SELECT 1 FROM dbo.APP_TASKS) OR NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = 'demo')
BEGIN
    PRINT 'Demo topshiriqlar: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @now datetime2(0) = DATEADD(hour, 9, CAST(@t AS datetime2(0)));
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
DECLARE @uName nvarchar(100) = (SELECT FullName FROM dbo.USERS WHERE ID = @u);

INSERT INTO dbo.APP_TASK_PROJECTS (Code, Name, IsActive, CreatedDT, IsDeleted)
SELECT v.Code, v.Name, 1, DATEADD(day, -40, @now), 0
FROM (VALUES (N'ERP', N'ERP tizimini joriy etish'), (N'SAV', N'Savdo va mijozlar'), (N'ISH', N'Ishlab chiqarish va ombor')) v (Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.APP_TASK_PROJECTS p WHERE p.Code = v.Code);

CREATE TABLE #tk (N int IDENTITY PRIMARY KEY, Project nvarchar(50) COLLATE DATABASE_DEFAULT, Title nvarchar(500) COLLATE DATABASE_DEFAULT,
                  Description nvarchar(1000) COLLATE DATABASE_DEFAULT, Status varchar(20) COLLATE DATABASE_DEFAULT, Priority varchar(20) COLLATE DATABASE_DEFAULT,
                  CreatedAgo int, UpdatedAgo int NULL, DueIn int NULL, Comment nvarchar(1000) COLLATE DATABASE_DEFAULT NULL);
INSERT INTO #tk (Project, Title, Description, Status, Priority, CreatedAgo, UpdatedAgo, DueIn, Comment) VALUES
(N'ERP', N'Foydalanuvchilarni qo''shish va rollarni biriktirish', N'Har bir bo''lim xodimi uchun login yarating, tegishli rolni bering.', 'DONE', 'HIGH', 30, 25, -24, N'Barcha bo''lim boshliqlari qo''shildi.'),
(N'ERP', N'Mahsulot nomenklaturasini tekshirish', N'Kategoriyalar, o''lchov birliklari va narxlarni haqiqiy ma''lumot bilan solishtiring.', 'DONE', 'NORMAL', 28, 12, -10, NULL),
(N'ERP', N'Ombor boshlang''ich qoldiqlarini kiritish', N'Inventarizatsiya natijasini omborlarga kirim qiling.', 'IN_PROGRESS', 'CRITICAL', 14, 2, 3, N'Xomashyo ombori tayyor, savdo ombori qoldi.'),
(N'ERP', N'Kontragentlar bazasini import qilish', N'Mijoz va ta''minotchilar ro''yxatini Excel''dan yuklang.', 'TODO', 'NORMAL', 7, NULL, 7, NULL),
(N'ERP', N'Xodimlar uchun qisqa o''quv mashg''uloti', N'Har bir modul bo''yicha 30 daqiqalik ko''rsatma.', 'TODO', 'NORMAL', 3, NULL, 12, NULL),
(N'SAV', N'Yirik mijozlar bilan yillik shartnomalarni yangilash', N'Narx va to''lov muddatlarini kelishib oling.', 'IN_PROGRESS', 'HIGH', 20, 4, -2, N'Ikki mijoz imzoladi, qolganlari ko''rib chiqmoqda.'),
(N'SAV', N'Muddati o''tgan debitorlik qarzlari bo''yicha qo''ng''iroqlar', N'15 kundan oshgan qarzlar ro''yxati bo''yicha.', 'TODO', 'CRITICAL', 6, NULL, -1, NULL),
(N'SAV', N'Yangi narxlar ro''yxatini tasdiqlash', N'Xomashyo qimmatlashgani sababli narxlarni qayta ko''rib chiqish.', 'DONE', 'HIGH', 18, 9, -8, NULL),
(N'SAV', N'Ko''rgazma uchun namunalar tayyorlash', NULL, 'CANCELLED', 'NORMAL', 25, 15, -12, N'Ko''rgazma keyingi yilga ko''chirildi.'),
(N'ISH', N'Quyish mashinasi profilaktikasi', N'Rejali texnik xizmat: filtr va moy almashtirish.', 'TODO', 'HIGH', 5, NULL, 4, NULL),
(N'ISH', N'Chiqindi foizini kamaytirish rejasi', N'Oxirgi oy sifat nazorati natijalarini tahlil qiling.', 'IN_PROGRESS', 'NORMAL', 10, 1, 10, NULL),
(N'ISH', N'Granula yetkazib beruvchisi bilan zaxira kelishuvi', NULL, 'DONE', 'NORMAL', 22, 6, -5, NULL);

INSERT INTO dbo.APP_TASKS (ProjectID, KeyCode, Title, Description, StatusID, PriorityID, AssigneeUserID, ReporterUserID, CreatedDT, UpdatedDT, DueDate, IsDeleted, UserComment)
SELECT p.ID, k.Project + N'-' + CAST(ROW_NUMBER() OVER (PARTITION BY k.Project ORDER BY k.N) AS nvarchar(10)), k.Title, k.Description,
       s.ID, pr.ID, @u, @u, DATEADD(day, -k.CreatedAgo, @now),
       CASE WHEN k.UpdatedAgo IS NULL THEN NULL ELSE DATEADD(hour, 6, DATEADD(day, -k.UpdatedAgo, @now)) END,
       CASE WHEN k.DueIn IS NULL THEN NULL ELSE DATEADD(day, k.DueIn, @t) END, 0,
       CASE WHEN k.Comment IS NULL THEN NULL
            ELSE N'[' + CONVERT(nvarchar(16), DATEADD(hour, 6, DATEADD(day, -ISNULL(k.UpdatedAgo, 0), @now)), 120) + N' - ' + ISNULL(@uName, N'demo') + N']:' + CHAR(10) + k.Comment END
FROM #tk k
JOIN dbo.APP_TASK_PROJECTS p ON p.Code = k.Project
JOIN dbo.APP_TASK_STATUS_REF s ON s.Code = k.Status
JOIN dbo.APP_TASK_PRIORITY_REF pr ON pr.Code = k.Priority
ORDER BY k.N;

-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #tk;

PRINT 'Demo topshiriqlar: OK';
