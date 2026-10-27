/* =====================================================================
   tahsinERP — ixcham namuna: Topshiriqlar (Tasks) — "ERP" loyihasi va admin'ga bitta boshlang'ich topshiriq.
   Idempotent: APP_TASKS bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @admin int = (SELECT TOP 1 u.ID FROM dbo.USERS u JOIN dbo.USERROLES ur ON ur.UserID = u.ID
                      JOIN dbo.ROLES r ON r.ID = ur.RoleID AND r.SystemCode = 'ADMIN' WHERE ISNULL(u.IsDeleted, 0) = 0 ORDER BY u.ID);
IF dbo.fn_SetupHasModule('Tasks') = 0 OR @admin IS NULL OR EXISTS (SELECT 1 FROM dbo.APP_TASKS)
BEGIN
    PRINT 'Namuna topshiriq: modul tanlanmagan, admin yo''q yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @now datetime2(0) = SYSDATETIME();
DECLARE @status int = (SELECT ID FROM dbo.APP_TASK_STATUS_REF WHERE Code = 'TODO');
DECLARE @priority int = (SELECT ID FROM dbo.APP_TASK_PRIORITY_REF WHERE Code = 'NORMAL');
IF @status IS NULL OR @priority IS NULL
BEGIN
    PRINT 'Namuna topshiriq: holat/muhimlik spravochnigi yo''q — o''tkazib yuborildi';
    RETURN;
END

INSERT INTO dbo.APP_TASK_PROJECTS (Code, Name, IsActive, CreatedDT, IsDeleted) VALUES (N'ERP', N'ERP tizimini joriy etish', 1, @now, 0);
DECLARE @project int = SCOPE_IDENTITY();
INSERT INTO dbo.APP_TASKS (ProjectID, KeyCode, Title, Description, StatusID, PriorityID, AssigneeUserID, ReporterUserID, CreatedDT, UpdatedDT,
                           DueDate, IsDeleted, UserComment)
VALUES (@project, N'ERP-1', N'Xodimlarni qo''shing va rollarni biriktiring',
        N'Admin panel → Foydalanuvchilar: har xodimga login yarating va tayyor rollardan birini bering.',
        @status, @priority, @admin, @admin, @now, NULL, DATEADD(day, 7, CAST(@now AS date)), 0, NULL);
