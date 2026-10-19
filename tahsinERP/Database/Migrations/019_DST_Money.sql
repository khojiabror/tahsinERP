/* =====================================================================
   019 — Distributsiya: to'lovlar, agent pulini kassaga topshirish, qaytarishlar (01_PHASE1_MVP.md, S3).

     dst.D_PAYMENTS       mijoz to'lovi: kassada (FinAccountID darhol) yoki agent qo'lida (topshirilguncha FinAccountID NULL);
                          CUSTOMER_LEDGER'ga kredit; pul kassaga tushganda dbo.FIN_TRANSACTIONS (SourceDocType = 'DstPayment')
     dst.CASH_HANDOVERS   agent (ekspeditor) naqd pulini kassaga topshirishi: e'lon qilingan / qabul qilingan summa, farq
     dst.D_RETURNS (+LINES)  mijozdan qaytarish: ombor kirimi (dbo.INCOMES + StockLedger.ReceiveLine, tannarx — jo'natishdagi),
                          yaroqli yoki brak, sabab, CUSTOMER_LEDGER'ga kredit
   Ruxsat modullari: DstPayments, DstReturns, DstDebts (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   ===================================================================== */

IF OBJECT_ID('dst.CASH_HANDOVERS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.CASH_HANDOVERS (
        ID             int IDENTITY(1, 1) NOT NULL,
        DocNo          nvarchar(30)   NOT NULL,
        DocDate        date           NOT NULL,
        Status         tinyint        NOT NULL,   -- 0 topshirildi (kutmoqda), 1 qabul qilindi, 2 rad etildi
        AgentUserID    int            NOT NULL,
        FinAccountID   int            NOT NULL,   -- qaysi kassaga
        AmountDeclared decimal(19, 2) NOT NULL,   -- agent qo'lidagi to'lovlar summasi
        AmountReceived decimal(19, 2) NULL,       -- kassir sanagan
        Note           nvarchar(300)  NULL,
        CreatedBy      int            NOT NULL,
        CreatedAt      datetime2(0)   NOT NULL,
        PostedBy       int            NULL,
        PostedAt       datetime2(0)   NULL,
        CancelledBy    int            NULL,
        CancelledAt    datetime2(0)   NULL,
        CancelReason   nvarchar(500)  NULL,
        CONSTRAINT PK_DST_CASH_HANDOVERS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_CASH_HANDOVERS_Agent FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_CASH_HANDOVERS_Account FOREIGN KEY (FinAccountID) REFERENCES dbo.FIN_ACCOUNTS (ID),
        CONSTRAINT CK_DST_CASH_HANDOVERS CHECK (AmountDeclared >= 0 AND (AmountReceived IS NULL OR AmountReceived >= 0))
    );
    CREATE UNIQUE INDEX UX_DST_CASH_HANDOVERS_DocNo ON dst.CASH_HANDOVERS (DocNo);
    CREATE INDEX IX_DST_CASH_HANDOVERS_Agent ON dst.CASH_HANDOVERS (AgentUserID, DocDate);
END
GO

IF OBJECT_ID('dst.D_PAYMENTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_PAYMENTS (
        ID               int IDENTITY(1, 1) NOT NULL,
        DocNo            nvarchar(30)   NOT NULL,
        DocDate          date           NOT NULL,
        Status           tinyint        NOT NULL,
        CustomerID       int            NOT NULL,
        OrderID          int            NULL,
        Amount           decimal(19, 2) NOT NULL,
        Method           varchar(10)    NOT NULL,   -- CASH / CARD / CLICK / PAYME / UZUM / TRANSFER
        ReceivedByUserID int            NOT NULL,   -- agent / ekspeditor / kassir
        FinAccountID     int            NULL,       -- pul tushgan hisob (agent naqdi — topshirilganda)
        HandoverID       int            NULL,
        FinTransactionID int            NULL,
        Note             nvarchar(300)  NULL,
        Source           varchar(10)    NOT NULL CONSTRAINT DF_DST_D_PAYMENTS_Source DEFAULT ('web'),
        ClientRequestId  uniqueidentifier NULL,
        CreatedBy        int            NOT NULL,
        CreatedAt        datetime2(0)   NOT NULL,
        PostedBy         int            NULL,
        PostedAt         datetime2(0)   NULL,
        CancelledBy      int            NULL,
        CancelledAt      datetime2(0)   NULL,
        CancelReason     nvarchar(500)  NULL,
        CONSTRAINT PK_DST_D_PAYMENTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_PAYMENTS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_D_PAYMENTS_Order FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID),
        CONSTRAINT FK_DST_D_PAYMENTS_Receiver FOREIGN KEY (ReceivedByUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_D_PAYMENTS_Account FOREIGN KEY (FinAccountID) REFERENCES dbo.FIN_ACCOUNTS (ID),
        CONSTRAINT FK_DST_D_PAYMENTS_Handover FOREIGN KEY (HandoverID) REFERENCES dst.CASH_HANDOVERS (ID),
        CONSTRAINT CK_DST_D_PAYMENTS CHECK (Amount > 0 AND Method IN ('CASH', 'CARD', 'CLICK', 'PAYME', 'UZUM', 'TRANSFER'))
    );
    CREATE UNIQUE INDEX UX_DST_D_PAYMENTS_DocNo ON dst.D_PAYMENTS (DocNo);
    CREATE UNIQUE INDEX UX_DST_D_PAYMENTS_ClientRequest ON dst.D_PAYMENTS (ClientRequestId) WHERE ClientRequestId IS NOT NULL;
    CREATE INDEX IX_DST_D_PAYMENTS_Customer ON dst.D_PAYMENTS (CustomerID, DocDate);
    CREATE INDEX IX_DST_D_PAYMENTS_OnHand ON dst.D_PAYMENTS (ReceivedByUserID) WHERE FinAccountID IS NULL AND Status = 1;
END
GO

IF OBJECT_ID('dst.D_RETURNS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_RETURNS (
        ID           int IDENTITY(1, 1) NOT NULL,
        DocNo        nvarchar(30)   NOT NULL,
        DocDate      date           NOT NULL,
        Status       tinyint        NOT NULL,
        CustomerID   int            NOT NULL,
        OrderID      int            NULL,
        WarehouseID  int            NOT NULL,
        ReasonID     int            NULL,
        IsDefect     bit            NOT NULL CONSTRAINT DF_DST_D_RETURNS_IsDefect DEFAULT (0),
        IncomeID     int            NULL,          -- dbo.INCOMES (ombor kirimi)
        Total        decimal(19, 2) NOT NULL,
        CostTotal    decimal(19, 2) NULL,
        PhotoKey     varchar(200)   NULL,
        Note         nvarchar(300)  NULL,
        Source       varchar(10)    NOT NULL CONSTRAINT DF_DST_D_RETURNS_Source DEFAULT ('web'),
        CreatedBy    int            NOT NULL,
        CreatedAt    datetime2(0)   NOT NULL,
        PostedBy     int            NULL,
        PostedAt     datetime2(0)   NULL,
        CancelledBy  int            NULL,
        CancelledAt  datetime2(0)   NULL,
        CancelReason nvarchar(500)  NULL,
        CONSTRAINT PK_DST_D_RETURNS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_RETURNS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_D_RETURNS_Order FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID),
        CONSTRAINT FK_DST_D_RETURNS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT FK_DST_D_RETURNS_Reason FOREIGN KEY (ReasonID) REFERENCES dst.REASONS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_D_RETURNS_DocNo ON dst.D_RETURNS (DocNo);
    CREATE INDEX IX_DST_D_RETURNS_Customer ON dst.D_RETURNS (CustomerID, DocDate);
END
GO

IF OBJECT_ID('dst.D_RETURN_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.D_RETURN_LINES (
        ID             int IDENTITY(1, 1) NOT NULL,
        ReturnID       int            NOT NULL,
        ProductID      int            NOT NULL,
        ShipmentLineID int            NULL,
        QtyBase        decimal(18, 4) NOT NULL,
        UnitPrice      decimal(19, 4) NOT NULL,     -- asosiy birlik narxi (jo'natishdagi)
        Amount         decimal(19, 2) NOT NULL,
        UnitCost       decimal(19, 6) NOT NULL,     -- asosiy birlik tannarxi (kirim narxi)
        CONSTRAINT PK_DST_D_RETURN_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_D_RETURN_LINES_Return FOREIGN KEY (ReturnID) REFERENCES dst.D_RETURNS (ID),
        CONSTRAINT FK_DST_D_RETURN_LINES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_D_RETURN_LINES_Shipment FOREIGN KEY (ShipmentLineID) REFERENCES dst.D_SHIPMENT_LINES (ID),
        CONSTRAINT CK_DST_D_RETURN_LINES CHECK (QtyBase > 0 AND UnitPrice >= 0 AND UnitCost >= 0)
    );
    CREATE INDEX IX_DST_D_RETURN_LINES_Shipment ON dst.D_RETURN_LINES (ShipmentLineID) WHERE ShipmentLineID IS NOT NULL;
END
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstPayments'), ('DstReturns'), ('DstDebts')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstPayments', 'DstReturns', 'DstDebts')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
