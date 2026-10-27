/* =====================================================================
   tahsinERP — ixcham namuna: Kadrlar (Hr) — korxona bo'linmasi va "Direktor" lavozimi (xodim kiritilmaydi).
   Idempotent: HR_ORG_UNITS bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Hr') = 0 OR EXISTS (SELECT 1 FROM dbo.HR_ORG_UNITS)
BEGIN
    PRINT 'Namuna tuzilma: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @now datetime = GETDATE();
DECLARE @company nvarchar(200) = ISNULL((SELECT TOP 1 Name FROM dbo.COMPANIES ORDER BY ID), N'Korxona');

INSERT INTO dbo.HR_ORG_UNITS (ParentID, Code, Name, LevelNo, SortOrder, IsActive, CreatedDT, IsDeleted)
VALUES (NULL, N'KORX', LEFT(@company, 200), 1, 1, 1, @now, 0);
IF NOT EXISTS (SELECT 1 FROM dbo.HR_POSITIONS)
    INSERT INTO dbo.HR_POSITIONS (Code, Name, IsManager, IsActive, CreatedDT, IsDeleted)
    VALUES (N'DIR', N'Direktor', 1, 1, @now, 0);
