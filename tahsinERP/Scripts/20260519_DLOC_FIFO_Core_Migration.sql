USE [DBTHSN-IPACK];
GO

/* DLOC + FIFO core schema (idempotent) */

IF OBJECT_ID('dbo.WAREHOUSE_LOCATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WAREHOUSE_LOCATIONS
    (
        ID                 INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WarehouseID        INT NOT NULL,
        LocationCode       NVARCHAR(30) NOT NULL,
        LocationName       NVARCHAR(120) NULL,
        CapacityUomID      INT NULL,
        MinCapacity        DECIMAL(18,4) NOT NULL CONSTRAINT DF_WHL_MinCapacity DEFAULT (0),
        MaxCapacity        DECIMAL(18,4) NOT NULL CONSTRAINT DF_WHL_MaxCapacity DEFAULT (0),
        AllowMixedLots     BIT NOT NULL CONSTRAINT DF_WHL_AllowMixedLots DEFAULT (0),
        AllowMixedProducts BIT NOT NULL CONSTRAINT DF_WHL_AllowMixedProducts DEFAULT (0),
        IsActive           BIT NOT NULL CONSTRAINT DF_WHL_IsActive DEFAULT (1),
        IsDeleted          BIT NOT NULL CONSTRAINT DF_WHL_IsDeleted DEFAULT (0),
        CreatedAt          DATETIME NOT NULL CONSTRAINT DF_WHL_CreatedAt DEFAULT (GETDATE())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_WHL_Warehouse_LocationCode' AND object_id = OBJECT_ID('dbo.WAREHOUSE_LOCATIONS'))
BEGIN
    CREATE UNIQUE INDEX UQ_WHL_Warehouse_LocationCode
        ON dbo.WAREHOUSE_LOCATIONS(WarehouseID, LocationCode)
        WHERE IsDeleted = 0;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_WHL_MinLeMax')
BEGIN
    ALTER TABLE dbo.WAREHOUSE_LOCATIONS
    ADD CONSTRAINT CK_WHL_MinLeMax CHECK (MinCapacity <= MaxCapacity);
END;
GO

IF OBJECT_ID('dbo.INCOME_LOTS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.INCOME_LOTS
    (
        ID               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IncomeID         INT NOT NULL,
        IncomeProductID  INT NOT NULL,
        ProductID        INT NOT NULL,
        WarehouseID      INT NOT NULL,
        LotNo            NVARCHAR(60) NOT NULL,
        ReceivedDateTime DATETIME NOT NULL,
        QtyReceived      DECIMAL(18,4) NOT NULL,
        QtyConsumed      DECIMAL(18,4) NOT NULL CONSTRAINT DF_IL_QtyConsumed DEFAULT (0),
        QtyAvailable     AS (QtyReceived - QtyConsumed) PERSISTED,
        ExpiryDate       DATETIME NULL,
        IsDeleted        BIT NOT NULL CONSTRAINT DF_IL_IsDeleted DEFAULT (0),
        CreatedAt        DATETIME NOT NULL CONSTRAINT DF_IL_CreatedAt DEFAULT (GETDATE())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IL_Product_Wh_Received' AND object_id = OBJECT_ID('dbo.INCOME_LOTS'))
BEGIN
    CREATE INDEX IX_IL_Product_Wh_Received ON dbo.INCOME_LOTS(ProductID, WarehouseID, ReceivedDateTime, ID);
END;
GO

IF OBJECT_ID('dbo.LOT_LOCATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LOT_LOCATIONS
    (
        ID             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        LotID          INT NOT NULL,
        LocationID     INT NOT NULL,
        ProductID      INT NOT NULL,
        QtyAvailable   DECIMAL(18,4) NOT NULL CONSTRAINT DF_LL_QtyAvailable DEFAULT (0),
        LastMovementAt DATETIME NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_LL_Lot_Location' AND object_id = OBJECT_ID('dbo.LOT_LOCATIONS'))
BEGIN
    CREATE UNIQUE INDEX UQ_LL_Lot_Location ON dbo.LOT_LOCATIONS(LotID, LocationID);
END;
GO

IF COL_LENGTH('dbo.EXPENSE_PRODUCTS', 'LotID') IS NULL
    ALTER TABLE dbo.EXPENSE_PRODUCTS ADD LotID INT NULL;
GO
IF COL_LENGTH('dbo.EXPENSE_PRODUCTS', 'FromLocationID') IS NULL
    ALTER TABLE dbo.EXPENSE_PRODUCTS ADD FromLocationID INT NULL;
GO
IF COL_LENGTH('dbo.EXPENSE_PRODUCTS', 'AllocationPolicy') IS NULL
    ALTER TABLE dbo.EXPENSE_PRODUCTS ADD AllocationPolicy NVARCHAR(20) NULL;
GO
IF COL_LENGTH('dbo.EXPENSE_PRODUCTS', 'ViolationReasonCode') IS NULL
    ALTER TABLE dbo.EXPENSE_PRODUCTS ADD ViolationReasonCode NVARCHAR(50) NULL;
GO

IF OBJECT_ID('dbo.EXPENSE_PRODUCT_ALLOCATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EXPENSE_PRODUCT_ALLOCATIONS
    (
        ID               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ExpenseProductID INT NOT NULL,
        LotID            INT NOT NULL,
        FromLocationID   INT NOT NULL,
        ToLocationID     INT NULL,
        Qty              DECIMAL(18,4) NOT NULL,
        CreatedAt        DATETIME NOT NULL CONSTRAINT DF_EPA_CreatedAt DEFAULT (GETDATE())
    );
END;
GO

IF OBJECT_ID('dbo.FIFO_POLICY', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FIFO_POLICY
    (
        ID                         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WarehouseID                INT NOT NULL,
        DocumentType               NVARCHAR(30) NOT NULL,
        IsStrictFifo               BIT NOT NULL CONSTRAINT DF_FP_IsStrict DEFAULT (1),
        AllowOverride              BIT NOT NULL CONSTRAINT DF_FP_AllowOverride DEFAULT (0),
        RequiresApprovalOnOverride BIT NOT NULL CONSTRAINT DF_FP_ReqApproval DEFAULT (1)
    );
END;
GO

IF OBJECT_ID('dbo.FIFO_VIOLATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FIFO_VIOLATIONS
    (
        ID                 INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ExpenseProductID   INT NOT NULL,
        ProductID          INT NOT NULL,
        WarehouseID        INT NOT NULL,
        ExpectedLotID      INT NULL,
        SelectedLotID      INT NULL,
        ExpectedReceivedAt DATETIME NULL,
        SelectedReceivedAt DATETIME NULL,
        ReasonCode         NVARCHAR(50) NULL,
        CreatedBy          NVARCHAR(128) NULL,
        ApprovedBy         NVARCHAR(128) NULL,
        CreatedAt          DATETIME NOT NULL CONSTRAINT DF_FV_CreatedAt DEFAULT (GETDATE())
    );
END;
GO

IF OBJECT_ID('dbo.V_INVENTORY_QTY_BY_WAREHOUSE_PRODUCT', 'V') IS NULL
    EXEC('CREATE VIEW dbo.V_INVENTORY_QTY_BY_WAREHOUSE_PRODUCT AS
        SELECT loc.WarehouseID AS WHID, ll.ProductID, SUM(ll.QtyAvailable) AS QtyFromLedger
        FROM dbo.LOT_LOCATIONS ll
        INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID
        WHERE ll.QtyAvailable > 0 AND loc.IsDeleted = 0
        GROUP BY loc.WarehouseID, ll.ProductID');
GO

IF OBJECT_ID('dbo.sp_CheckDlocCapacity', 'P') IS NULL
    EXEC('CREATE PROCEDURE dbo.sp_CheckDlocCapacity AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE dbo.sp_CheckDlocCapacity
    @LocationID INT,
    @IncomingQty DECIMAL(18,4)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @MaxCapacity DECIMAL(18,4), @CurrentQty DECIMAL(18,4);
    SELECT @MaxCapacity = MaxCapacity FROM dbo.WAREHOUSE_LOCATIONS
    WHERE ID = @LocationID AND IsDeleted = 0 AND IsActive = 1;
    IF @MaxCapacity IS NULL BEGIN RAISERROR('DLOC_REQUIRED_OR_INACTIVE', 16, 1); RETURN; END
    SELECT @CurrentQty = ISNULL(SUM(QtyAvailable), 0) FROM dbo.LOT_LOCATIONS WHERE LocationID = @LocationID;
    IF (@CurrentQty + @IncomingQty) > @MaxCapacity BEGIN RAISERROR('DLOC_CAPACITY_EXCEEDED', 16, 1); RETURN; END
END;
GO
