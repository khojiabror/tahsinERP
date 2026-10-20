/* =====================================================================
   022 — Distributsiya: S4 qoldiqlari (01_PHASE1_MVP.md, 14-bo'lim).

     dst.AGENT_TRACK   agent joylashuvi nuqtalari: Telegram jonli joylashuvi (LIVE), kun boshi/oxiri (DAY), "keldim" (VISIT)
     Sozlamalar:       RequireSelfie (kun boshida selfi majburiy, 0/1), BotPublicUrl (bot servisining ochiq manzili —
                       ERP sahifalarida selfi/foto imzolangan havola orqali ko'rsatiladi)
     Ruxsat moduli:    DstField (dala nazorati: ish kunlari, selfi, tashriflar, jonli joylashuv)
   ===================================================================== */

IF OBJECT_ID('dst.AGENT_TRACK', 'U') IS NULL
BEGIN
    CREATE TABLE dst.AGENT_TRACK (
        ID        bigint IDENTITY(1, 1) NOT NULL,
        UserID    int           NOT NULL,
        At        datetime2(0)  NOT NULL,
        Lat       decimal(9, 6) NOT NULL,
        Lng       decimal(9, 6) NOT NULL,
        AccuracyM int           NULL,
        Source    varchar(10)   NOT NULL,   -- LIVE / DAY / VISIT
        CONSTRAINT PK_DST_AGENT_TRACK PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_AGENT_TRACK_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT CK_DST_AGENT_TRACK_Source CHECK (Source IN ('LIVE', 'DAY', 'VISIT'))
    );
    CREATE INDEX IX_DST_AGENT_TRACK_User ON dst.AGENT_TRACK (UserID, At) INCLUDE (Lat, Lng, AccuracyM, Source);
END
GO

IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'RequireSelfie')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('RequireSelfie', '0');
IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'BotPublicUrl')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('BotPublicUrl', NULL);
GO

-- ---------- ruxsat moduli ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstField')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstField')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
