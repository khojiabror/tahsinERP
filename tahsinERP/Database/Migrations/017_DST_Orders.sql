/* =====================================================================
   017 — Distributsiya buyurtmalari, rezerv, jo'natish va mijoz ledgeri (01_PHASE1_MVP.md, S2).

     dst.D_ORDERS              buyurtma (holat: 0 yangi .. 6 yopildi, 9 bekor)
     dst.D_ORDER_LINES         qatorlar: sotish birligi + asosiy birlikdagi miqdor, narx/min narx surati
     dst.D_ORDER_STATUS_LOG    kim, qachon, qaysi holatdan qaysiga
     dst.STOCK_RESERVATIONS    rezerv (asosiy birlikda); mavjud = PRODUCT_STOCKS.Amount - SUM(rezerv)
     dst.D_SHIPMENTS (+LINES)  jo'natish: ombor chiqimi (dbo.EXPENSES) bilan bog'langan, FIFO tannarx
     dst.CUSTOMER_LEDGER       mijoz qarzi: debet (jo'natish), kredit (to'lov, qaytarish) — S3 to'ldiradi
     dst.SETTINGS              kalit/qiymat sozlamalari (minimal buyurtma summasi va h.k.)
   Ruxsat modullari: DstOrders, DstStock (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   ===================================================================== */

IF OBJECT_ID('dst.D_ORDERS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_ORDERS (
        ID                 int IDENTITY(1, 1) NOT NULL,
        DocNo              nvarchar(30)   NOT NULL,
        DocDate            date           NOT NULL,
        Status             tinyint        NOT NULL,   -- DstDocument: 0 qoralama (yangi), 1 o'tkazilgan (tasdiqlangan+), 2 bekor
        OrderStatus        tinyint        NOT NULL,   -- OrderStatus enum
        CustomerID         int            NOT NULL,
        AgentUserID        int            NULL,
        WarehouseID        int            NULL,
        PriceTypeID        int            NULL,
        DeliveryDate       date           NULL,
        IsPickup           bit            NOT NULL CONSTRAINT DF_DST_D_ORDERS_IsPickup DEFAULT (0),
        Comment            nvarchar(500)  NULL,
        Subtotal           decimal(19, 2) NOT NULL CONSTRAINT DF_DST_D_ORDERS_Subtotal DEFAULT (0),
        DiscountPct        decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_D_ORDERS_DiscountPct DEFAULT (0),
        DiscountSum        decimal(19, 2) NOT NULL CONSTRAINT DF_DST_D_ORDERS_DiscountSum DEFAULT (0),
        Total              decimal(19, 2) NOT NULL CONSTRAINT DF_DST_D_ORDERS_Total DEFAULT (0),
        ShippedTotal       decimal(19, 2) NOT NULL CONSTRAINT DF_DST_D_ORDERS_ShippedTotal DEFAULT (0),
        WeightKg           decimal(18, 3) NOT NULL CONSTRAINT DF_DST_D_ORDERS_WeightKg DEFAULT (0),
        VolumeM3           decimal(18, 4) NOT NULL CONSTRAINT DF_DST_D_ORDERS_VolumeM3 DEFAULT (0),
        Source             varchar(10)    NOT NULL CONSTRAINT DF_DST_D_ORDERS_Source DEFAULT ('web'),
        ClientRequestId    uniqueidentifier NULL,     -- idempotentlik (bot qayta yuborsa ikki buyurtma bo'lmasin)
        CreditOverrideBy   int            NULL,
        CreditOverrideAt   datetime2(0)   NULL,
        CreditOverrideNote nvarchar(300)  NULL,
        ConfirmedBy        int            NULL,
        ConfirmedAt        datetime2(0)   NULL,
        DeliveredAt        datetime2(0)   NULL,
        CreatedBy          int            NOT NULL,
        CreatedAt          datetime2(0)   NOT NULL,
        PostedBy           int            NULL,
        PostedAt           datetime2(0)   NULL,
        CancelledBy        int            NULL,
        CancelledAt        datetime2(0)   NULL,
        CancelReason       nvarchar(500)  NULL,
        RowVer             rowversion     NOT NULL,
        CONSTRAINT PK_DST_D_ORDERS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_ORDERS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_D_ORDERS_Agent FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_D_ORDERS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT FK_DST_D_ORDERS_PriceType FOREIGN KEY (PriceTypeID) REFERENCES dst.PRICE_TYPES (ID),
        CONSTRAINT CK_DST_D_ORDERS_Discount CHECK (DiscountPct BETWEEN 0 AND 100)
    );
    CREATE UNIQUE INDEX UX_DST_D_ORDERS_DocNo ON dst.D_ORDERS (DocNo);
    CREATE UNIQUE INDEX UX_DST_D_ORDERS_ClientRequest ON dst.D_ORDERS (ClientRequestId) WHERE ClientRequestId IS NOT NULL;
    CREATE INDEX IX_DST_D_ORDERS_Customer ON dst.D_ORDERS (CustomerID, DocDate);
    CREATE INDEX IX_DST_D_ORDERS_Status ON dst.D_ORDERS (OrderStatus, DocDate) INCLUDE (AgentUserID, WarehouseID, Total);
    CREATE INDEX IX_DST_D_ORDERS_Agent ON dst.D_ORDERS (AgentUserID, DocDate);
END
GO

IF OBJECT_ID('dst.D_ORDER_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_ORDER_LINES (
        ID             int IDENTITY(1, 1) NOT NULL,
        OrderID        int            NOT NULL,
        LineNo         int            NOT NULL,
        ProductID      int            NOT NULL,
        ProductUnitID  int            NULL,          -- NULL = asosiy birlik
        UnitID         int            NOT NULL,
        Factor         decimal(18, 6) NOT NULL,
        Qty            decimal(18, 4) NOT NULL,      -- sotish birligida
        QtyBase        decimal(18, 4) NOT NULL,      -- asosiy birlikda
        UnitPrice      decimal(19, 2) NOT NULL,      -- sotish birligi narxi (chegirmasiz)
        MinUnitPrice   decimal(19, 2) NULL,          -- surat: shu paytdagi min narx
        PriceSource    tinyint        NOT NULL,      -- PriceSource enum (0 — qo'lda)
        DiscountPct    decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_D_ORDER_LINES_DiscountPct DEFAULT (0),
        DiscountSum    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_D_ORDER_LINES_DiscountSum DEFAULT (0),
        Amount         decimal(19, 2) NOT NULL,
        QtyShippedBase decimal(18, 4) NOT NULL CONSTRAINT DF_DST_D_ORDER_LINES_Shipped DEFAULT (0),
        CONSTRAINT PK_DST_D_ORDER_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_ORDER_LINES_Order FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID),
        CONSTRAINT FK_DST_D_ORDER_LINES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_D_ORDER_LINES_Unit FOREIGN KEY (ProductUnitID) REFERENCES dst.PRODUCT_UNITS (ID),
        CONSTRAINT CK_DST_D_ORDER_LINES CHECK (Qty > 0 AND QtyBase > 0 AND Factor > 0 AND UnitPrice >= 0
            AND DiscountPct BETWEEN 0 AND 100 AND QtyShippedBase >= 0 AND QtyShippedBase <= QtyBase)
    );
    CREATE INDEX IX_DST_D_ORDER_LINES_Order ON dst.D_ORDER_LINES (OrderID);
    CREATE INDEX IX_DST_D_ORDER_LINES_Product ON dst.D_ORDER_LINES (ProductID) INCLUDE (QtyBase, Amount);
END
GO

IF OBJECT_ID('dst.D_ORDER_STATUS_LOG', 'U') IS NULL
    CREATE TABLE dst.D_ORDER_STATUS_LOG (
        ID         bigint IDENTITY(1, 1) NOT NULL,
        OrderID    int           NOT NULL,
        FromStatus tinyint       NULL,
        ToStatus   tinyint       NOT NULL,
        UserID     int           NULL,
        At         datetime2(0)  NOT NULL,
        Note       nvarchar(500) NULL,
        CONSTRAINT PK_DST_D_ORDER_STATUS_LOG PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_ORDER_STATUS_LOG_Order FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID)
    );
GO

IF OBJECT_ID('dst.STOCK_RESERVATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.STOCK_RESERVATIONS (
        ID          int IDENTITY(1, 1) NOT NULL,
        WarehouseID int            NOT NULL,
        ProductID   int            NOT NULL,
        OrderLineID int            NOT NULL,
        QtyBase     decimal(18, 4) NOT NULL,
        CreatedAt   datetime2(0)   NOT NULL,
        CONSTRAINT PK_DST_STOCK_RESERVATIONS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_STOCK_RESERVATIONS_Line FOREIGN KEY (OrderLineID) REFERENCES dst.D_ORDER_LINES (ID),
        CONSTRAINT FK_DST_STOCK_RESERVATIONS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT CK_DST_STOCK_RESERVATIONS CHECK (QtyBase > 0)
    );
    CREATE UNIQUE INDEX UX_DST_STOCK_RESERVATIONS_Line ON dst.STOCK_RESERVATIONS (OrderLineID);
    CREATE INDEX IX_DST_STOCK_RESERVATIONS_Stock ON dst.STOCK_RESERVATIONS (WarehouseID, ProductID) INCLUDE (QtyBase);
END
GO

IF OBJECT_ID('dst.D_SHIPMENTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_SHIPMENTS (
        ID           int IDENTITY(1, 1) NOT NULL,
        DocNo        nvarchar(30)   NOT NULL,
        DocDate      date           NOT NULL,
        Status       tinyint        NOT NULL,
        OrderID      int            NOT NULL,
        WarehouseID  int            NOT NULL,
        ExpenseID    int            NULL,           -- dbo.EXPENSES (ombor chiqimi)
        Total        decimal(19, 2) NOT NULL,
        CostTotal    decimal(19, 2) NULL,
        IsPickup     bit            NOT NULL CONSTRAINT DF_DST_D_SHIPMENTS_IsPickup DEFAULT (0),
        CreatedBy    int            NOT NULL,
        CreatedAt    datetime2(0)   NOT NULL,
        PostedBy     int            NULL,
        PostedAt     datetime2(0)   NULL,
        CancelledBy  int            NULL,
        CancelledAt  datetime2(0)   NULL,
        CancelReason nvarchar(500)  NULL,
        CONSTRAINT PK_DST_D_SHIPMENTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_SHIPMENTS_Order FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID),
        CONSTRAINT FK_DST_D_SHIPMENTS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID)
    );
    CREATE UNIQUE INDEX UX_DST_D_SHIPMENTS_DocNo ON dst.D_SHIPMENTS (DocNo);
    CREATE INDEX IX_DST_D_SHIPMENTS_Order ON dst.D_SHIPMENTS (OrderID);
END
GO

IF OBJECT_ID('dst.D_SHIPMENT_LINES', 'U') IS NULL
    CREATE TABLE dst.D_SHIPMENT_LINES (
        ID          int IDENTITY(1, 1) NOT NULL,
        ShipmentID  int            NOT NULL,
        OrderLineID int            NOT NULL,
        ProductID   int            NOT NULL,
        QtyBase     decimal(18, 4) NOT NULL,
        Amount      decimal(19, 2) NOT NULL,         -- sotuv summasi (buyurtma chegirmasi ulushi bilan)
        CostAmount  decimal(19, 2) NULL,             -- FIFO tannarx
        CONSTRAINT PK_DST_D_SHIPMENT_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_SHIPMENT_LINES_Shipment FOREIGN KEY (ShipmentID) REFERENCES dst.D_SHIPMENTS (ID),
        CONSTRAINT FK_DST_D_SHIPMENT_LINES_Line FOREIGN KEY (OrderLineID) REFERENCES dst.D_ORDER_LINES (ID),
        CONSTRAINT CK_DST_D_SHIPMENT_LINES CHECK (QtyBase > 0)
    );
GO

IF OBJECT_ID('dst.CUSTOMER_LEDGER', 'U') IS NULL
BEGIN
    CREATE TABLE dst.CUSTOMER_LEDGER (
        ID          bigint IDENTITY(1, 1) NOT NULL,
        CustomerID  int            NOT NULL,
        EntryDate   datetime2(0)   NOT NULL,
        DocType     varchar(20)    NOT NULL,   -- SHIPMENT / PAYMENT / RETURN / ADJUSTMENT / OPENING / REVERSAL
        DocID       int            NULL,
        OrderID     int            NULL,
        Debit       decimal(19, 2) NOT NULL CONSTRAINT DF_DST_CUSTOMER_LEDGER_Debit DEFAULT (0),   -- mijoz qarzi oshadi
        Credit      decimal(19, 2) NOT NULL CONSTRAINT DF_DST_CUSTOMER_LEDGER_Credit DEFAULT (0),  -- qarz kamayadi
        DueDate     date           NULL,
        AgentUserID int            NULL,
        Note        nvarchar(300)  NULL,
        CreatedBy   int            NULL,
        CONSTRAINT PK_DST_CUSTOMER_LEDGER PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_CUSTOMER_LEDGER_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT CK_DST_CUSTOMER_LEDGER CHECK (Debit >= 0 AND Credit >= 0 AND (Debit = 0 OR Credit = 0))
    );
    CREATE INDEX IX_DST_CUSTOMER_LEDGER_Customer ON dst.CUSTOMER_LEDGER (CustomerID, EntryDate) INCLUDE (Debit, Credit, DueDate);
    CREATE INDEX IX_DST_CUSTOMER_LEDGER_Order ON dst.CUSTOMER_LEDGER (OrderID) WHERE OrderID IS NOT NULL;
END
GO

IF OBJECT_ID('dst.SETTINGS', 'U') IS NULL
    CREATE TABLE dst.SETTINGS (
        [Key]  varchar(50)   NOT NULL,
        Value  nvarchar(400) NULL,
        CONSTRAINT PK_DST_SETTINGS PRIMARY KEY CLUSTERED ([Key])
    );
GO

IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'MinOrderAmount')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('MinOrderAmount', '0');
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstOrders'), ('DstStock')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstOrders', 'DstStock')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
