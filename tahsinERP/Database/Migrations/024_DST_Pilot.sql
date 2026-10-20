/* =====================================================================
   024 — Distributsiya: pilot uchun inventarizatsiya va xatolar jurnali (03_PILOT.md, S6).

     - dst.INVENTORY_COUNTS (+ _LINES): ombordagi sanoq — boshlang'ich (qoldiqni sanalganga tenglaydi) yoki nazorat
       (taqqoslash, ixtiyoriy tuzatish). Qoldiqqa yozish mavjud InventoryAdjustmentService orqali (dbo.INVENTORY_ADJUSTMENTS,
       INV kirim/chiqim, FIFO) — AdjustmentID bog'lanadi.
     - dst.ISSUES: pilot xatolar jurnali (web va bot orqali), daraja K1/K2/K3, holat.
     - ruxsat modullari DstInventory, DstIssues — Admin'ga (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   Idempotent.
   ===================================================================== */

IF OBJECT_ID('dst.INVENTORY_COUNTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.INVENTORY_COUNTS (
        ID           int IDENTITY(1, 1) NOT NULL,
        DocNo        nvarchar(30)   NOT NULL,
        DocDate      date           NOT NULL,
        Status       tinyint        NOT NULL,
        WarehouseID  int            NOT NULL,
        Kind         varchar(10)    NOT NULL,      -- OPENING | CONTROL
        Applied      bit            NOT NULL CONSTRAINT DF_DST_INVENTORY_COUNTS_Applied DEFAULT (0),
        AdjustmentID int            NULL,          -- dbo.INVENTORY_ADJUSTMENTS
        Note         nvarchar(300)  NULL,
        CreatedBy    int            NOT NULL,
        CreatedAt    datetime2(0)   NOT NULL,
        PostedBy     int            NULL,
        PostedAt     datetime2(0)   NULL,
        CancelledBy  int            NULL,
        CancelledAt  datetime2(0)   NULL,
        CancelReason nvarchar(500)  NULL,
        CONSTRAINT PK_DST_INVENTORY_COUNTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_INVENTORY_COUNTS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT CK_DST_INVENTORY_COUNTS_Kind CHECK (Kind IN ('OPENING', 'CONTROL'))
    );
    CREATE UNIQUE INDEX UX_DST_INVENTORY_COUNTS_DocNo ON dst.INVENTORY_COUNTS (DocNo);
    CREATE INDEX IX_DST_INVENTORY_COUNTS_Warehouse ON dst.INVENTORY_COUNTS (WarehouseID, DocDate);
END
GO

IF OBJECT_ID('dst.INVENTORY_COUNT_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.INVENTORY_COUNT_LINES (
        ID          int IDENTITY(1, 1) NOT NULL,
        CountID     int            NOT NULL,
        ProductID   int            NOT NULL,
        CountedQty  decimal(19, 4) NULL,           -- NULL — hali sanalmagan
        SystemQty   decimal(19, 4) NULL,           -- o'tkazilganda qoldiq (asosiy birlikda)
        UnitCost    decimal(19, 2) NULL,           -- boshlang'ich sanoqda ortiqcha uchun tannarx (bo'sh — oxirgi kirim narxi)
        Note        nvarchar(200)  NULL,
        CONSTRAINT PK_DST_INVENTORY_COUNT_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_INVENTORY_COUNT_LINES_Count FOREIGN KEY (CountID) REFERENCES dst.INVENTORY_COUNTS (ID),
        CONSTRAINT FK_DST_INVENTORY_COUNT_LINES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_INVENTORY_COUNT_LINES ON dst.INVENTORY_COUNT_LINES (CountID, ProductID);
END
GO

IF OBJECT_ID('dst.ISSUES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.ISSUES (
        ID          int IDENTITY(1, 1) NOT NULL,
        IssueNo     nvarchar(20)   NOT NULL,       -- P-001
        CreatedAt   datetime2(0)   NOT NULL,
        CreatedBy   int            NULL,           -- dbo.USERS
        Source      varchar(10)    NOT NULL CONSTRAINT DF_DST_ISSUES_Source DEFAULT ('web'),
        Place       nvarchar(150)  NULL,           -- bot / Mini App / sahifa
        DocRef      nvarchar(100)  NULL,           -- buyurtma, to'lov, mijoz raqami
        Description nvarchar(2000) NOT NULL,
        Expected    nvarchar(1000) NULL,
        Severity    tinyint        NULL,           -- 1 = K1, 2 = K2, 3 = K3; NULL — baholanmagan
        Status      tinyint        NOT NULL CONSTRAINT DF_DST_ISSUES_Status DEFAULT (0),   -- 0 yangi, 1 tuzatilmoqda, 2 tekshirish, 3 yopildi
        PhotoKey    varchar(200)   NULL,
        Resolution  nvarchar(1000) NULL,
        UpdatedAt   datetime2(0)   NULL,
        ClosedBy    int            NULL,
        ClosedAt    datetime2(0)   NULL,
        CONSTRAINT PK_DST_ISSUES PRIMARY KEY CLUSTERED (ID)
    );
    CREATE UNIQUE INDEX UX_DST_ISSUES_No ON dst.ISSUES (IssueNo);
    CREATE INDEX IX_DST_ISSUES_Status ON dst.ISSUES (Status, Severity);
END
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstInventory'), ('DstIssues')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstInventory', 'DstIssues')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
