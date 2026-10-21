/* =====================================================================
   025 — Distributsiya 3-bosqich: ombor kengaytmasi (00_PLAN.md, 5-bo'lim, 3-bosqich).

     - dst.LANDED_COSTS (+ _INCOMES, _ITEMS, _LINES): kirimga qo'shimcha xarajat (yo'l, bojxona, boshqa) —
       qabul qilingan kirim qatorlariga qiymat / miqdor / og'irlik bo'yicha taqsimlanadi va dbo.INCOME_PRODUCTS.PiecePrice
       oshiriladi (FIFO tannarx shu narxdan olinadi). Bekor qilinganda qaytariladi.
     - dst.WRITE_OFFS (+ _LINES): hisobdan chiqarish rahbar tasdig'i bilan (qoralama → tasdiqqa yuborildi → tasdiq =
       ombor chiqimi FIFO tannarxda; rad = bekor).
     - dst.REGRADES (+ _LINES): pereso'rtitsa — bir tovar chiqim, boshqasi kirim (tannarx saqlanadi).
     - dst.TRANSFER_ACTS (+ _LINES): ko'chirmani farq dalolatnomasi bilan qabul qilish (kamomad — yo'qotish yoki
       jo'natuvchiga qaytarish, ortiqcha — kirim).
     - dst.PROMOTIONS: aksiyalar (hajmga qarab chegirma, muddatli chegirma, N+M sovg'a); D_ORDER_LINES ga aksiya ustunlari.
     - dst.PRODUCT_STOCK_LIMITS.MaxQty: minimal qoldiqdan buyurtma taklifi qaysi darajagacha to'ldirilishi.
     - ruxsat modullari DstLandedCosts, DstWriteOffs, DstRegrades, DstTransfers, DstReorder, DstPromotions
       va DstWriteOffApprove (hisobdan chiqarishni tasdiqlash) — Admin'ga (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   Idempotent.
   ===================================================================== */

-- ---------- landed cost ----------
IF OBJECT_ID('dst.LANDED_COSTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.LANDED_COSTS (
        ID              int IDENTITY(1, 1) NOT NULL,
        DocNo           nvarchar(30)   NOT NULL,
        DocDate         date           NOT NULL,
        Status          tinyint        NOT NULL,
        Basis           tinyint        NOT NULL,      -- 1 qiymat, 2 miqdor, 3 og'irlik
        Note            nvarchar(300)  NULL,
        TotalAmount     decimal(19, 2) NOT NULL CONSTRAINT DF_DST_LANDED_COSTS_TotalAmount DEFAULT (0),
        ConsumedAmount  decimal(19, 2) NOT NULL CONSTRAINT DF_DST_LANDED_COSTS_ConsumedAmount DEFAULT (0),   -- allaqachon chiqarilgan tovarga tushgan qism
        CreatedBy       int            NOT NULL,
        CreatedAt       datetime2(0)   NOT NULL,
        PostedBy        int            NULL,
        PostedAt        datetime2(0)   NULL,
        CancelledBy     int            NULL,
        CancelledAt     datetime2(0)   NULL,
        CancelReason    nvarchar(500)  NULL,
        CONSTRAINT PK_DST_LANDED_COSTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT CK_DST_LANDED_COSTS_Basis CHECK (Basis IN (1, 2, 3))
    );
    CREATE UNIQUE INDEX UX_DST_LANDED_COSTS_DocNo ON dst.LANDED_COSTS (DocNo);
END
GO

IF OBJECT_ID('dst.LANDED_COST_INCOMES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.LANDED_COST_INCOMES (
        ID           int IDENTITY(1, 1) NOT NULL,
        LandedCostID int NOT NULL,
        IncomeID     int NOT NULL,
        CONSTRAINT PK_DST_LANDED_COST_INCOMES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_LANDED_COST_INCOMES_Doc FOREIGN KEY (LandedCostID) REFERENCES dst.LANDED_COSTS (ID),
        CONSTRAINT FK_DST_LANDED_COST_INCOMES_INCOMES FOREIGN KEY (IncomeID) REFERENCES dbo.INCOMES (ID)
    );
    CREATE UNIQUE INDEX UX_DST_LANDED_COST_INCOMES ON dst.LANDED_COST_INCOMES (LandedCostID, IncomeID);
    CREATE INDEX IX_DST_LANDED_COST_INCOMES_Income ON dst.LANDED_COST_INCOMES (IncomeID);
END
GO

IF OBJECT_ID('dst.LANDED_COST_ITEMS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.LANDED_COST_ITEMS (
        ID           int IDENTITY(1, 1) NOT NULL,
        LandedCostID int            NOT NULL,
        Kind         varchar(10)    NOT NULL,         -- FREIGHT | CUSTOMS | OTHER
        Description  nvarchar(200)  NULL,
        Amount       decimal(19, 2) NOT NULL,         -- asosiy valyutada, QQSsiz
        CONSTRAINT PK_DST_LANDED_COST_ITEMS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_LANDED_COST_ITEMS_Doc FOREIGN KEY (LandedCostID) REFERENCES dst.LANDED_COSTS (ID),
        CONSTRAINT CK_DST_LANDED_COST_ITEMS_Kind CHECK (Kind IN ('FREIGHT', 'CUSTOMS', 'OTHER')),
        CONSTRAINT CK_DST_LANDED_COST_ITEMS_Amount CHECK (Amount > 0)
    );
END
GO

IF OBJECT_ID('dst.LANDED_COST_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.LANDED_COST_LINES (
        ID              int IDENTITY(1, 1) NOT NULL,
        LandedCostID    int            NOT NULL,
        IncomeID        int            NOT NULL,
        IncomeProductID int            NOT NULL,
        ProductID       int            NOT NULL,
        Qty             decimal(18, 6) NOT NULL,      -- kirim qatori miqdori (o'z birligida)
        BaseValue       decimal(19, 2) NOT NULL,      -- taqsimlashdan oldingi qiymat (Qty × PiecePrice)
        WeightKg        decimal(18, 4) NULL,
        Allocated       decimal(19, 2) NOT NULL,      -- shu qatorga tushgan xarajat
        UnitAdd         decimal(19, 6) NOT NULL,      -- PiecePrice ga qo'shilgan
        ConsumedQty     decimal(18, 6) NOT NULL CONSTRAINT DF_DST_LANDED_COST_LINES_ConsumedQty DEFAULT (0),
        CONSTRAINT PK_DST_LANDED_COST_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_LANDED_COST_LINES_Doc FOREIGN KEY (LandedCostID) REFERENCES dst.LANDED_COSTS (ID),
        CONSTRAINT FK_DST_LANDED_COST_LINES_IP FOREIGN KEY (IncomeProductID) REFERENCES dbo.INCOME_PRODUCTS (ID)
    );
    CREATE INDEX IX_DST_LANDED_COST_LINES_Doc ON dst.LANDED_COST_LINES (LandedCostID);
END
GO

-- ---------- hisobdan chiqarish ----------
IF OBJECT_ID('dst.WRITE_OFFS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.WRITE_OFFS (
        ID           int IDENTITY(1, 1) NOT NULL,
        DocNo        nvarchar(30)   NOT NULL,
        DocDate      date           NOT NULL,
        Status       tinyint        NOT NULL,
        WarehouseID  int            NOT NULL,
        ReasonID     int            NULL,
        Note         nvarchar(300)  NULL,
        PhotoKey     varchar(200)   NULL,
        SubmittedBy  int            NULL,
        SubmittedAt  datetime2(0)   NULL,
        ExpenseID    int            NULL,             -- dbo.EXPENSES (tasdiqlanganda)
        CostTotal    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_WRITE_OFFS_CostTotal DEFAULT (0),
        CreatedBy    int            NOT NULL,
        CreatedAt    datetime2(0)   NOT NULL,
        PostedBy     int            NULL,             -- tasdiqlagan
        PostedAt     datetime2(0)   NULL,
        CancelledBy  int            NULL,
        CancelledAt  datetime2(0)   NULL,
        CancelReason nvarchar(500)  NULL,
        CONSTRAINT PK_DST_WRITE_OFFS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_WRITE_OFFS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT FK_DST_WRITE_OFFS_REASONS FOREIGN KEY (ReasonID) REFERENCES dst.REASONS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_WRITE_OFFS_DocNo ON dst.WRITE_OFFS (DocNo);
    CREATE INDEX IX_DST_WRITE_OFFS_Status ON dst.WRITE_OFFS (Status, SubmittedAt);
END
GO

IF OBJECT_ID('dst.WRITE_OFF_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.WRITE_OFF_LINES (
        ID          int IDENTITY(1, 1) NOT NULL,
        WriteOffID  int            NOT NULL,
        ProductID   int            NOT NULL,
        QtyBase     decimal(18, 4) NOT NULL,
        UnitCost    decimal(19, 6) NULL,
        CostAmount  decimal(19, 2) NULL,
        Note        nvarchar(200)  NULL,
        CONSTRAINT PK_DST_WRITE_OFF_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_WRITE_OFF_LINES_Doc FOREIGN KEY (WriteOffID) REFERENCES dst.WRITE_OFFS (ID),
        CONSTRAINT FK_DST_WRITE_OFF_LINES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT CK_DST_WRITE_OFF_LINES_Qty CHECK (QtyBase > 0)
    );
    CREATE UNIQUE INDEX UX_DST_WRITE_OFF_LINES ON dst.WRITE_OFF_LINES (WriteOffID, ProductID);
END
GO

-- ---------- pereso'rtitsa ----------
IF OBJECT_ID('dst.REGRADES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.REGRADES (
        ID           int IDENTITY(1, 1) NOT NULL,
        DocNo        nvarchar(30)   NOT NULL,
        DocDate      date           NOT NULL,
        Status       tinyint        NOT NULL,
        WarehouseID  int            NOT NULL,
        ReasonID     int            NULL,
        Note         nvarchar(300)  NULL,
        ExpenseID    int            NULL,
        IncomeID     int            NULL,
        CostTotal    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_REGRADES_CostTotal DEFAULT (0),
        CreatedBy    int            NOT NULL,
        CreatedAt    datetime2(0)   NOT NULL,
        PostedBy     int            NULL,
        PostedAt     datetime2(0)   NULL,
        CancelledBy  int            NULL,
        CancelledAt  datetime2(0)   NULL,
        CancelReason nvarchar(500)  NULL,
        CONSTRAINT PK_DST_REGRADES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_REGRADES_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT FK_DST_REGRADES_REASONS FOREIGN KEY (ReasonID) REFERENCES dst.REASONS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_REGRADES_DocNo ON dst.REGRADES (DocNo);
END
GO

IF OBJECT_ID('dst.REGRADE_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.REGRADE_LINES (
        ID            int IDENTITY(1, 1) NOT NULL,
        RegradeID     int            NOT NULL,
        FromProductID int            NOT NULL,
        ToProductID   int            NOT NULL,
        FromQtyBase   decimal(18, 4) NOT NULL,
        ToQtyBase     decimal(18, 4) NOT NULL,
        CostAmount    decimal(19, 2) NULL,
        CONSTRAINT PK_DST_REGRADE_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_REGRADE_LINES_Doc FOREIGN KEY (RegradeID) REFERENCES dst.REGRADES (ID),
        CONSTRAINT FK_DST_REGRADE_LINES_From FOREIGN KEY (FromProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_REGRADE_LINES_To FOREIGN KEY (ToProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT CK_DST_REGRADE_LINES_Qty CHECK (FromQtyBase > 0 AND ToQtyBase > 0 AND FromProductID <> ToProductID)
    );
    CREATE INDEX IX_DST_REGRADE_LINES_Doc ON dst.REGRADE_LINES (RegradeID);
END
GO

-- ---------- ko'chirma farq dalolatnomasi ----------
IF OBJECT_ID('dst.TRANSFER_ACTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRANSFER_ACTS (
        ID              int IDENTITY(1, 1) NOT NULL,
        DocNo           nvarchar(30)   NOT NULL,
        DocDate         date           NOT NULL,
        Status          tinyint        NOT NULL,
        IncomeID        int            NOT NULL,      -- qabul qilingan ko'chirma kirimi
        ExpenseID       int            NULL,          -- jo'natuvchi chiqimi
        FromWarehouseID int            NOT NULL,
        ToWarehouseID   int            NOT NULL,
        Note            nvarchar(300)  NULL,
        ShortageCost    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRANSFER_ACTS_Shortage DEFAULT (0),
        LossCost        decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRANSFER_ACTS_Loss DEFAULT (0),
        SurplusCost     decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRANSFER_ACTS_Surplus DEFAULT (0),
        ReturnIncomeID  int            NULL,          -- kamomad jo'natuvchiga qaytarilsa
        CreatedBy       int            NOT NULL,
        CreatedAt       datetime2(0)   NOT NULL,
        PostedBy        int            NULL,
        PostedAt        datetime2(0)   NULL,
        CancelledBy     int            NULL,
        CancelledAt     datetime2(0)   NULL,
        CancelReason    nvarchar(500)  NULL,
        CONSTRAINT PK_DST_TRANSFER_ACTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRANSFER_ACTS_INCOMES FOREIGN KEY (IncomeID) REFERENCES dbo.INCOMES (ID)
    );
    CREATE UNIQUE INDEX UX_DST_TRANSFER_ACTS_DocNo ON dst.TRANSFER_ACTS (DocNo);
    CREATE UNIQUE INDEX UX_DST_TRANSFER_ACTS_Income ON dst.TRANSFER_ACTS (IncomeID);
END
GO

IF OBJECT_ID('dst.TRANSFER_ACT_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRANSFER_ACT_LINES (
        ID              int IDENTITY(1, 1) NOT NULL,
        ActID           int            NOT NULL,
        IncomeProductID int            NOT NULL,
        ProductID       int            NOT NULL,
        UnitID          int            NOT NULL,
        SentQty         decimal(18, 6) NOT NULL,
        ReceivedQty     decimal(18, 6) NOT NULL,
        UnitCost        decimal(19, 6) NOT NULL,
        Resolution      varchar(10)    NULL,          -- kamomadda: LOSS | RETURN
        Note            nvarchar(200)  NULL,
        CONSTRAINT PK_DST_TRANSFER_ACT_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRANSFER_ACT_LINES_Doc FOREIGN KEY (ActID) REFERENCES dst.TRANSFER_ACTS (ID),
        CONSTRAINT CK_DST_TRANSFER_ACT_LINES_Qty CHECK (SentQty >= 0 AND ReceivedQty >= 0),
        CONSTRAINT CK_DST_TRANSFER_ACT_LINES_Res CHECK (Resolution IS NULL OR Resolution IN ('LOSS', 'RETURN'))
    );
    CREATE INDEX IX_DST_TRANSFER_ACT_LINES_Doc ON dst.TRANSFER_ACT_LINES (ActID);
END
GO

-- ---------- aksiyalar ----------
IF OBJECT_ID('dst.PROMOTIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.PROMOTIONS (
        ID            int IDENTITY(1, 1) NOT NULL,
        Name          nvarchar(150)  NOT NULL,
        Kind          varchar(10)    NOT NULL,        -- DISCOUNT | FREE
        DateFrom      date           NOT NULL,
        DateTo        date           NULL,
        IsActive      bit            NOT NULL CONSTRAINT DF_DST_PROMOTIONS_IsActive DEFAULT (1),
        ProductID     int            NULL,            -- tovar (FREE — majburiy)
        CategoryID    int            NULL,            -- yoki kategoriya (DISCOUNT); ikkalasi bo'sh — hamma tovar
        PriceTypeID   int            NULL,            -- faqat shu narx turidagi mijozlarga
        MinQtyBase    decimal(18, 4) NOT NULL CONSTRAINT DF_DST_PROMOTIONS_MinQty DEFAULT (0),   -- DISCOUNT: qatordagi miqdor shundan kam bo'lmasa
        DiscountPct   decimal(5, 2)  NULL,
        BuyQtyBase    decimal(18, 4) NULL,            -- FREE: har N ...
        FreeQtyBase   decimal(18, 4) NULL,            -- ... uchun M sovg'a
        FreeProductID int            NULL,            -- sovg'a tovar (bo'sh — o'sha tovar)
        Repeatable    bit            NOT NULL CONSTRAINT DF_DST_PROMOTIONS_Repeatable DEFAULT (1),
        Note          nvarchar(300)  NULL,
        CreatedBy     int            NULL,
        CreatedAt     datetime2(0)   NOT NULL,
        UpdatedAt     datetime2(0)   NULL,
        CONSTRAINT PK_DST_PROMOTIONS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_PROMOTIONS_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_PROMOTIONS_FREE FOREIGN KEY (FreeProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_PROMOTIONS_PRICE_TYPES FOREIGN KEY (PriceTypeID) REFERENCES dst.PRICE_TYPES (ID),
        CONSTRAINT CK_DST_PROMOTIONS_Kind CHECK (Kind IN ('DISCOUNT', 'FREE')),
        CONSTRAINT CK_DST_PROMOTIONS_Discount CHECK (Kind <> 'DISCOUNT' OR (DiscountPct > 0 AND DiscountPct <= 100)),
        CONSTRAINT CK_DST_PROMOTIONS_Free CHECK (Kind <> 'FREE' OR (ProductID IS NOT NULL AND BuyQtyBase > 0 AND FreeQtyBase > 0)),
        CONSTRAINT CK_DST_PROMOTIONS_Dates CHECK (DateTo IS NULL OR DateTo >= DateFrom)
    );
    CREATE INDEX IX_DST_PROMOTIONS_Active ON dst.PROMOTIONS (IsActive, DateFrom, DateTo);
END
GO

IF COL_LENGTH('dst.D_ORDER_LINES', 'PromotionID') IS NULL
    ALTER TABLE dst.D_ORDER_LINES ADD
        PromotionID int           NULL,
        PromoPct    decimal(5, 2) NOT NULL CONSTRAINT DF_DST_D_ORDER_LINES_PromoPct DEFAULT (0),
        IsFree      bit           NOT NULL CONSTRAINT DF_DST_D_ORDER_LINES_IsFree DEFAULT (0);
GO

IF COL_LENGTH('dst.PRODUCT_STOCK_LIMITS', 'MaxQty') IS NULL
    ALTER TABLE dst.PRODUCT_STOCK_LIMITS ADD MaxQty decimal(18, 4) NULL;
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstLandedCosts'), ('DstWriteOffs'), ('DstRegrades'), ('DstTransfers'), ('DstReorder'), ('DstPromotions')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT 'DstWriteOffApprove', 'Dst', 'WriteOffApprove', NULL
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = 'DstWriteOffApprove');

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstLandedCosts', 'DstWriteOffs', 'DstRegrades', 'DstTransfers', 'DstReorder', 'DstPromotions', 'DstWriteOffApprove')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
