/* =====================================================================
   013 — Distributsiya moduli poydevori (Docs/Distribution/01_PHASE1_MVP.md, S0).

   Distributsiya jadvallari alohida `dst` sxemasida va EF6 Code-First kontekst
   (Areas/Distribution/Data/DistributionDbContext) orqali ishlatiladi — EDMX'ga kirmaydi.
     dst.AUDIT_TRAIL    — maydon darajasidagi jurnal: kim, qachon, eski -> yangi qiymat
     dst.USER_SCOPES    — qator darajasidagi ko'rinish doirasi (ombor / hudud / jamoa / mijoz)
     dst.USER_SETTINGS  — foydalanuvchi tili (uz / ru) va boshqa shaxsiy sozlamalar
   Ruxsat modullari: DstHome (modul sahifasi) va maxsus huquqlar (Controller = 'Dst'):
     DstSeeCost           — xarid narxi, tannarx va foydani ko'rish
     DstOverrideCredit    — qarz limitidan oshgan buyurtmani tasdiqlash
     DstDiscountUnlimited — rol chegirma limitidan yuqori chegirma
   Admin roliga yangi modullarga to'liq huquq beriladi (Seed/110 bilan bir xil qoida).
   ===================================================================== */

IF SCHEMA_ID('dst') IS NULL
    EXEC('CREATE SCHEMA dst AUTHORIZATION dbo');
GO

IF OBJECT_ID('dst.AUDIT_TRAIL', 'U') IS NULL
BEGIN
    CREATE TABLE dst.AUDIT_TRAIL (
        ID            bigint IDENTITY(1, 1) NOT NULL,
        CorrelationId uniqueidentifier      NOT NULL,   -- bitta SaveChanges'dagi yozuvlar guruhi
        EntityName    varchar(100)          NOT NULL,
        EntityKey     varchar(100)          NOT NULL,
        Action        char(1)               NOT NULL,   -- I / U / D
        FieldName     varchar(100)          NULL,       -- I/D da NULL (butun qator JSON)
        OldValue      nvarchar(max)         NULL,
        NewValue      nvarchar(max)         NULL,
        UserID        int                   NULL,
        Source        varchar(10)           NOT NULL,   -- web / bot / api / system
        ChangedAt     datetime2(3)          NOT NULL,
        CONSTRAINT PK_DST_AUDIT_TRAIL PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT CK_DST_AUDIT_TRAIL_Action CHECK (Action IN ('I', 'U', 'D'))
    );
    CREATE INDEX IX_DST_AUDIT_TRAIL_Entity ON dst.AUDIT_TRAIL (EntityName, EntityKey, ChangedAt);
    CREATE INDEX IX_DST_AUDIT_TRAIL_User ON dst.AUDIT_TRAIL (UserID, ChangedAt);
END
GO

IF OBJECT_ID('dst.USER_SCOPES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.USER_SCOPES (
        ID        int IDENTITY(1, 1) NOT NULL,
        UserID    int                NOT NULL,
        ScopeType varchar(20)        NOT NULL,   -- WAREHOUSE / TERRITORY / TEAM / CUSTOMER
        ScopeID   int                NOT NULL,
        CONSTRAINT PK_DST_USER_SCOPES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_USER_SCOPES_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT CK_DST_USER_SCOPES_Type CHECK (ScopeType IN ('WAREHOUSE', 'TERRITORY', 'TEAM', 'CUSTOMER'))
    );
    CREATE UNIQUE INDEX UX_DST_USER_SCOPES ON dst.USER_SCOPES (UserID, ScopeType, ScopeID);
END
GO

IF OBJECT_ID('dst.USER_SETTINGS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.USER_SETTINGS (
        UserID   int        NOT NULL,
        Language varchar(5) NOT NULL CONSTRAINT DF_DST_USER_SETTINGS_Language DEFAULT ('uz'),
        CONSTRAINT PK_DST_USER_SETTINGS PRIMARY KEY CLUSTERED (UserID),
        CONSTRAINT FK_DST_USER_SETTINGS_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT CK_DST_USER_SETTINGS_Language CHECK (Language IN ('uz', 'ru'))
    );
END
GO

-- ---------- ruxsat modullari ----------
-- Bo'sh bazada (yangi tenant, seed hali ishlamagan) qo'shilmaydi — u yerda Seed/105_PermissionModules_Extra.sql
-- qo'shadi (aks holda Seed/100 ning qat'iy ID'lari bilan to'qnashadi); Admin huquqini Seed/110 beradi.
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Module, v.Controller, v.Action, NULL
    FROM (VALUES
        ('DstHome',              'DstHome', 'Index'),
        ('DstSeeCost',           'Dst',     'SeeCost'),
        ('DstOverrideCredit',    'Dst',     'OverrideCredit'),
        ('DstDiscountUnlimited', 'Dst',     'DiscountUnlimited')
    ) v (Module, Controller, Action)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Module);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstHome', 'DstSeeCost', 'DstOverrideCredit', 'DstDiscountUnlimited')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
