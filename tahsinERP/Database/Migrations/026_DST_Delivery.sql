/* =====================================================================
   026 — Distributsiya 2-bosqich: yetkazish va nazorat (00_PLAN.md 5-bo'lim, 01_PHASE1_MVP.md 17-bo'lim).

     - dst.VEHICLES: mashina (raqam, yuk ko'tarish kg, hajm m³, haydovchi, odatiy ekspeditor va ombor).
     - dst.TRIPS: reys (Draft — rejalandi/yo'lda, Posted — yopildi, Cancelled); TripStatus 0 rejalandi, 1 yo'lda, 2 yopildi, 9 bekor.
     - dst.TRIP_STOPS (+ _LINES): reysdagi jo'natishlar (bitta jo'natish — bitta reysda), tartib, yetkazish natijasi
       (to'liq/qisman/rad, rasm yoki imzo, sabab), yopishda qaytarish hujjati (D_RETURNS) bilan.
     - dst.TRIP_EXPENSES: reys xarajatlari (yoqilg'i va h.k.) — o'tkazilganda moliyaga chiqim (FIN_TRANSACTIONS).
     - dst.VISIT_PLANS: agentning aniq kunga tashrif rejasi (tartib bilan). Yo'q bo'lsa — mijoz kartasidagi tashrif kunlari.
     - dst.KPI_SCHEMES / KPI_RESULTS: agent KPI ish haqi (% savdo, bonus, jarima) → HR ish haqi qatoriga (mukofot/jarima).
     - ruxsat modullari DstVehicles, DstTrips, DstRoutes, DstKpi — Admin'ga (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   Sozlamalar (dst.SETTINGS, kalit-qiymat, jadval o'zgarmaydi): YandexMapsKey, VoiceOrders, DepotGeo_<omborID>.
   Idempotent.
   ===================================================================== */

IF OBJECT_ID('dst.VEHICLES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.VEHICLES (
        ID              int IDENTITY(1, 1) NOT NULL,
        PlateNo         nvarchar(20)   NOT NULL,
        Model           nvarchar(100)  NULL,
        CapacityKg      decimal(18, 2) NOT NULL,
        CapacityM3      decimal(18, 3) NULL,       -- NULL — hajm tekshirilmaydi
        DriverName      nvarchar(100)  NULL,
        DriverPhone     varchar(25)    NULL,
        ExpeditorUserID int            NULL,       -- odatiy ekspeditor (dbo.USERS)
        WarehouseID     int            NULL,       -- odatiy ombor
        IsActive        bit            NOT NULL CONSTRAINT DF_DST_VEHICLES_IsActive DEFAULT (1),
        Note            nvarchar(300)  NULL,
        CONSTRAINT PK_DST_VEHICLES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_VEHICLES_USERS FOREIGN KEY (ExpeditorUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_VEHICLES_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT CK_DST_VEHICLES_Capacity CHECK (CapacityKg > 0 AND (CapacityM3 IS NULL OR CapacityM3 > 0))
    );
    CREATE UNIQUE INDEX UX_DST_VEHICLES_Plate ON dst.VEHICLES (PlateNo);
END
GO

IF OBJECT_ID('dst.TRIPS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRIPS (
        ID              int IDENTITY(1, 1) NOT NULL,
        DocNo           nvarchar(30)   NOT NULL,
        DocDate         date           NOT NULL,
        Status          tinyint        NOT NULL,
        TripStatus      tinyint        NOT NULL CONSTRAINT DF_DST_TRIPS_TripStatus DEFAULT (0),
        VehicleID       int            NOT NULL,
        ExpeditorUserID int            NOT NULL,
        WarehouseID     int            NOT NULL,
        WeightKg        decimal(18, 3) NOT NULL CONSTRAINT DF_DST_TRIPS_WeightKg DEFAULT (0),
        VolumeM3        decimal(18, 4) NOT NULL CONSTRAINT DF_DST_TRIPS_VolumeM3 DEFAULT (0),
        Total           decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRIPS_Total DEFAULT (0),
        RouteOptimized  bit            NOT NULL CONSTRAINT DF_DST_TRIPS_RouteOptimized DEFAULT (0),
        StartedAt       datetime2(0)   NULL,
        FinishedAt      datetime2(0)   NULL,       -- ekspeditor botda yakunladi
        ShortageCost    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRIPS_ShortageCost DEFAULT (0),
        Note            nvarchar(500)  NULL,
        CreatedBy       int            NOT NULL,
        CreatedAt       datetime2(0)   NOT NULL,
        PostedBy        int            NULL,
        PostedAt        datetime2(0)   NULL,
        CancelledBy     int            NULL,
        CancelledAt     datetime2(0)   NULL,
        CancelReason    nvarchar(500)  NULL,
        RowVer          rowversion     NOT NULL,
        CONSTRAINT PK_DST_TRIPS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRIPS_VEHICLES FOREIGN KEY (VehicleID) REFERENCES dst.VEHICLES (ID),
        CONSTRAINT FK_DST_TRIPS_USERS FOREIGN KEY (ExpeditorUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_TRIPS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT CK_DST_TRIPS_TripStatus CHECK (TripStatus IN (0, 1, 2, 9))
    );
    CREATE UNIQUE INDEX UX_DST_TRIPS_DocNo ON dst.TRIPS (DocNo);
    CREATE INDEX IX_DST_TRIPS_Expeditor ON dst.TRIPS (ExpeditorUserID, DocDate) INCLUDE (TripStatus);
    CREATE INDEX IX_DST_TRIPS_Date ON dst.TRIPS (DocDate, TripStatus);
END
GO

IF OBJECT_ID('dst.TRIP_STOPS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRIP_STOPS (
        ID              int IDENTITY(1, 1) NOT NULL,
        TripID          int            NOT NULL,
        ShipmentID      int            NOT NULL,
        OrderID         int            NOT NULL,
        CustomerID      int            NOT NULL,
        Seq             int            NOT NULL,
        StopStatus      tinyint        NOT NULL CONSTRAINT DF_DST_TRIP_STOPS_Status DEFAULT (0),   -- 0 kutmoqda, 1 yetkazildi, 2 qisman, 3 rad
        Amount          decimal(19, 2) NOT NULL,
        DeliveredAmount decimal(19, 2) NULL,
        WeightKg        decimal(18, 3) NOT NULL,
        VolumeM3        decimal(18, 4) NOT NULL,
        DeliveredAt     datetime2(0)   NULL,
        DeliveredBy     int            NULL,
        PhotoKey        varchar(200)   NULL,
        SignatureKey    varchar(200)   NULL,
        ReasonID        int            NULL,       -- dst.REASONS (RETURN turi)
        Note            nvarchar(300)  NULL,
        Lat             decimal(9, 6)  NULL,
        Lng             decimal(9, 6)  NULL,
        ReturnID        int            NULL,       -- yopishda yaratilgan qaytarish (dst.D_RETURNS)
        ShortageAmount  decimal(19, 2) NOT NULL CONSTRAINT DF_DST_TRIP_STOPS_Shortage DEFAULT (0),   -- mijozga kreditlangan, omborga qaytmagan (sotuv narxida)
        CONSTRAINT PK_DST_TRIP_STOPS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRIP_STOPS_TRIPS FOREIGN KEY (TripID) REFERENCES dst.TRIPS (ID),
        CONSTRAINT FK_DST_TRIP_STOPS_SHIPMENTS FOREIGN KEY (ShipmentID) REFERENCES dst.D_SHIPMENTS (ID),
        CONSTRAINT CK_DST_TRIP_STOPS_Status CHECK (StopStatus IN (0, 1, 2, 3))
    );
    CREATE UNIQUE INDEX UX_DST_TRIP_STOPS_Shipment ON dst.TRIP_STOPS (ShipmentID);
    CREATE INDEX IX_DST_TRIP_STOPS_Trip ON dst.TRIP_STOPS (TripID, Seq);
    CREATE INDEX IX_DST_TRIP_STOPS_Order ON dst.TRIP_STOPS (OrderID);
END
GO

IF OBJECT_ID('dst.TRIP_STOP_LINES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRIP_STOP_LINES (
        ID             int IDENTITY(1, 1) NOT NULL,
        StopID         int            NOT NULL,
        ShipmentLineID int            NOT NULL,
        ProductID      int            NOT NULL,
        QtyBase        decimal(18, 4) NOT NULL,     -- jo'natilgan
        Amount         decimal(19, 2) NOT NULL,
        QtyDelivered   decimal(18, 4) NULL,         -- NULL — hali yetkazilmagan
        QtyReturned    decimal(18, 4) NULL,         -- yopishda omborga qaytgani (yetkazilmagandan ko'p emas)
        CONSTRAINT PK_DST_TRIP_STOP_LINES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRIP_STOP_LINES_STOPS FOREIGN KEY (StopID) REFERENCES dst.TRIP_STOPS (ID),
        CONSTRAINT FK_DST_TRIP_STOP_LINES_SL FOREIGN KEY (ShipmentLineID) REFERENCES dst.D_SHIPMENT_LINES (ID),
        CONSTRAINT CK_DST_TRIP_STOP_LINES CHECK (QtyBase > 0 AND (QtyDelivered IS NULL OR (QtyDelivered >= 0 AND QtyDelivered <= QtyBase))
                                                 AND (QtyReturned IS NULL OR QtyReturned >= 0))
    );
    CREATE UNIQUE INDEX UX_DST_TRIP_STOP_LINES ON dst.TRIP_STOP_LINES (StopID, ShipmentLineID);
END
GO

IF OBJECT_ID('dst.TRIP_EXPENSES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TRIP_EXPENSES (
        ID               int IDENTITY(1, 1) NOT NULL,
        TripID           int            NOT NULL,
        CategoryID       int            NULL,       -- dbo.FIN_CATEGORIES (chiqim); bot yozganda bo'sh bo'lishi mumkin
        Kind             varchar(10)    NOT NULL,   -- FUEL | PARKING | REPAIR | MEAL | OTHER
        Amount           decimal(19, 2) NOT NULL,
        FinAccountID     int            NULL,       -- o'tkazilganda: qaysi kassadan
        Status           tinyint        NOT NULL CONSTRAINT DF_DST_TRIP_EXPENSES_Status DEFAULT (0),   -- 0 kutmoqda, 1 o'tkazildi, 2 bekor
        FinTransactionID int            NULL,
        Note             nvarchar(300)  NULL,
        PhotoKey         varchar(200)   NULL,
        Source           varchar(10)    NOT NULL CONSTRAINT DF_DST_TRIP_EXPENSES_Source DEFAULT ('web'),
        CreatedBy        int            NOT NULL,
        CreatedAt        datetime2(0)   NOT NULL,
        PostedBy         int            NULL,
        PostedAt         datetime2(0)   NULL,
        CONSTRAINT PK_DST_TRIP_EXPENSES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TRIP_EXPENSES_TRIPS FOREIGN KEY (TripID) REFERENCES dst.TRIPS (ID),
        CONSTRAINT CK_DST_TRIP_EXPENSES CHECK (Amount > 0 AND Status IN (0, 1, 2)
                                               AND Kind IN ('FUEL', 'PARKING', 'REPAIR', 'MEAL', 'OTHER'))
    );
    CREATE INDEX IX_DST_TRIP_EXPENSES_Trip ON dst.TRIP_EXPENSES (TripID);
END
GO

IF OBJECT_ID('dst.VISIT_PLANS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.VISIT_PLANS (
        ID          int IDENTITY(1, 1) NOT NULL,
        AgentUserID int            NOT NULL,
        PlanDate    date           NOT NULL,
        CustomerID  int            NOT NULL,
        Seq         int            NOT NULL,
        Note        nvarchar(200)  NULL,
        CreatedBy   int            NOT NULL,
        CreatedAt   datetime2(0)   NOT NULL,
        CONSTRAINT PK_DST_VISIT_PLANS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_VISIT_PLANS_USERS FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_VISIT_PLANS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_VISIT_PLANS ON dst.VISIT_PLANS (AgentUserID, PlanDate, CustomerID);
    CREATE INDEX IX_DST_VISIT_PLANS_Date ON dst.VISIT_PLANS (PlanDate) INCLUDE (AgentUserID, CustomerID);
END
GO

IF OBJECT_ID('dst.KPI_SCHEMES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.KPI_SCHEMES (
        AgentUserID        int            NOT NULL,
        EmployeeID         int            NULL,       -- dbo.HR_EMPLOYEES (ish haqi qatori shu xodimniki)
        SalesPct           decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_SalesPct DEFAULT (0),
        MonthlyPlan        decimal(19, 2) NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_Plan DEFAULT (0),
        PlanBonus          decimal(19, 2) NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_PlanBonus DEFAULT (0),
        OverPlanPct        decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_OverPlan DEFAULT (0),
        MinCoveragePct     decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_MinCov DEFAULT (0),
        CoveragePenalty    decimal(19, 2) NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_CovPenalty DEFAULT (0),
        OverduePenaltyPct  decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_Overdue DEFAULT (0),
        ShortagePenaltyPct decimal(5, 2)  NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_Shortage DEFAULT (0),
        IsActive           bit            NOT NULL CONSTRAINT DF_DST_KPI_SCHEMES_IsActive DEFAULT (1),
        UpdatedBy          int            NULL,
        UpdatedAt          datetime2(0)   NULL,
        CONSTRAINT PK_DST_KPI_SCHEMES PRIMARY KEY CLUSTERED (AgentUserID),
        CONSTRAINT FK_DST_KPI_SCHEMES_USERS FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_KPI_SCHEMES_EMPLOYEES FOREIGN KEY (EmployeeID) REFERENCES dbo.HR_EMPLOYEES (ID),
        CONSTRAINT CK_DST_KPI_SCHEMES CHECK (SalesPct BETWEEN 0 AND 100 AND OverPlanPct BETWEEN 0 AND 100 AND MinCoveragePct BETWEEN 0 AND 100
                                             AND OverduePenaltyPct BETWEEN 0 AND 100 AND ShortagePenaltyPct BETWEEN 0 AND 100
                                             AND MonthlyPlan >= 0 AND PlanBonus >= 0 AND CoveragePenalty >= 0)
    );
END
GO

IF OBJECT_ID('dst.KPI_RESULTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.KPI_RESULTS (
        ID              int IDENTITY(1, 1) NOT NULL,
        [Year]          int            NOT NULL,
        [Month]         int            NOT NULL,
        AgentUserID     int            NOT NULL,
        EmployeeID      int            NULL,
        NetSales        decimal(19, 2) NOT NULL,
        PlanAmount      decimal(19, 2) NOT NULL,
        PlanPct         decimal(7, 2)  NULL,
        CoveragePct     decimal(7, 2)  NULL,
        OverdueDebt     decimal(19, 2) NOT NULL,
        ShortageCost    decimal(19, 2) NOT NULL,
        Commission      decimal(19, 2) NOT NULL,
        Bonus           decimal(19, 2) NOT NULL,
        Penalty         decimal(19, 2) NOT NULL,
        ManualBonus     decimal(19, 2) NOT NULL CONSTRAINT DF_DST_KPI_RESULTS_MB DEFAULT (0),
        ManualPenalty   decimal(19, 2) NOT NULL CONSTRAINT DF_DST_KPI_RESULTS_MP DEFAULT (0),
        Note            nvarchar(300)  NULL,
        Status          tinyint        NOT NULL CONSTRAINT DF_DST_KPI_RESULTS_Status DEFAULT (0),   -- 0 hisoblangan, 1 ish haqiga o'tkazilgan
        PayrollLineID   int            NULL,       -- dbo.HR_PAYROLL_LINES
        AppliedBonus    decimal(19, 2) NULL,
        AppliedPenalty  decimal(19, 2) NULL,
        CalculatedBy    int            NOT NULL,
        CalculatedAt    datetime2(0)   NOT NULL,
        AppliedBy       int            NULL,
        AppliedAt       datetime2(0)   NULL,
        CONSTRAINT PK_DST_KPI_RESULTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_KPI_RESULTS_USERS FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT CK_DST_KPI_RESULTS CHECK ([Month] BETWEEN 1 AND 12 AND Status IN (0, 1)
                                             AND ManualBonus >= 0 AND ManualPenalty >= 0)
    );
    CREATE UNIQUE INDEX UX_DST_KPI_RESULTS ON dst.KPI_RESULTS ([Year], [Month], AgentUserID);
END
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstVehicles'), ('DstTrips'), ('DstRoutes'), ('DstKpi')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstVehicles', 'DstTrips', 'DstRoutes', 'DstKpi')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
