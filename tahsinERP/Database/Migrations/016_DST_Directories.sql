/* =====================================================================
   016 — Distributsiya ma'lumotnomalari (Docs/Distribution/01_PHASE1_MVP.md, S1).

   Mavjud dbo jadvallariga TEGILMAYDI — qo'shimcha maydonlar 1:1 profil jadvallarida:
     dst.TERRITORIES          viloyat -> tuman -> hudud (Level 1..3)
     dst.CUSTOMER_PROFILES    dbo.CUSTOMERS uchun: STIR, GPS, tur, A/B/C, agent, narx turi, limit, kechiktirish
     dst.BRANDS, MANUFACTURERS, SUB_CATEGORIES (dbo.PRODUCT_CATEGORIES ichida)
     dst.PRODUCT_PROFILES     dbo.PRODUCTS uchun: rus nomi, brend, subkategoriya, sotuvda/katta o'lchamli
     dst.PRODUCT_UNITS        qo'shimcha o'lchov birliklari: Factor = nechta ASOSIY birlik (PRODUCTS.UnitID)
     dst.PRODUCT_BARCODES     bir nechta shtrix-kod (birlikka bog'lanishi mumkin)
     dst.PRICE_TYPES, PRICE_LIST_ITEMS (tarix ValidFrom bo'yicha), CUSTOMER_PRICES (maxsus narx)
     dst.SALES_TEAMS, SALES_AGENTS  jamoa (supervayzer) va agentlar
     dst.DISCOUNT_LIMITS      rol bo'yicha maksimal chegirma %
     dst.REASONS              qaytarish / buyurtmasiz tashrif / bekor qilish / hisobdan chiqarish sabablari
     dst.WAREHOUSE_SETTINGS, PRODUCT_STOCK_LIMITS  ombor sozlamalari va minimal qoldiq
   Narx va miqdorlar ASOSIY birlikda saqlanadi; boshqa birlik narxi = narx * Factor.
   Ruxsat modullari — 013 dagi kabi faqat to'ldirilgan bazada (bo'sh baza: Seed/105).
   ===================================================================== */

IF OBJECT_ID('dst.TERRITORIES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TERRITORIES (
        ID        int IDENTITY(1, 1) NOT NULL,
        ParentID  int            NULL,
        Level     tinyint        NOT NULL,   -- 1 viloyat, 2 tuman, 3 hudud
        Name      nvarchar(100)  NOT NULL,
        NameRu    nvarchar(100)  NULL,
        IsActive  bit            NOT NULL CONSTRAINT DF_DST_TERRITORIES_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_TERRITORIES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TERRITORIES_Parent FOREIGN KEY (ParentID) REFERENCES dst.TERRITORIES (ID),
        CONSTRAINT CK_DST_TERRITORIES_Level CHECK (Level BETWEEN 1 AND 3)
    );
    CREATE INDEX IX_DST_TERRITORIES_Parent ON dst.TERRITORIES (ParentID);
END
GO

IF OBJECT_ID('dst.PRICE_TYPES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.PRICE_TYPES (
        ID        int IDENTITY(1, 1) NOT NULL,
        Name      nvarchar(50)  NOT NULL,
        NameRu    nvarchar(50)  NULL,
        IsDefault bit           NOT NULL CONSTRAINT DF_DST_PRICE_TYPES_IsDefault DEFAULT (0),
        IsActive  bit           NOT NULL CONSTRAINT DF_DST_PRICE_TYPES_IsActive DEFAULT (1),
        SortOrder int           NOT NULL CONSTRAINT DF_DST_PRICE_TYPES_SortOrder DEFAULT (0),
        CONSTRAINT PK_DST_PRICE_TYPES PRIMARY KEY CLUSTERED (ID)
    );
    CREATE UNIQUE INDEX UX_DST_PRICE_TYPES_Default ON dst.PRICE_TYPES (IsDefault) WHERE IsDefault = 1;
END
GO

IF OBJECT_ID('dst.CUSTOMER_PROFILES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.CUSTOMER_PROFILES (
        CustomerID   int            NOT NULL,
        INN          varchar(14)    NULL,
        Phone2       varchar(25)    NULL,
        OwnerName    nvarchar(100)  NULL,
        Landmark     nvarchar(200)  NULL,
        Lat          decimal(9, 6)  NULL,
        Lng          decimal(9, 6)  NULL,
        PhotoKey     varchar(200)   NULL,
        PointType    varchar(20)    NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_PointType DEFAULT ('SHOP'),
        Category     char(1)        NULL,
        TerritoryID  int            NULL,
        AgentUserID  int            NULL,
        PriceTypeID  int            NULL,
        VisitDays    tinyint        NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_VisitDays DEFAULT (0), -- bit: Du=1 .. Ya=64
        CreditLimit  decimal(19, 2) NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_CreditLimit DEFAULT (0),
        PaymentDays  int            NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_PaymentDays DEFAULT (0),
        IsApproved   bit            NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_IsApproved DEFAULT (1),
        ApprovedBy   int            NULL,
        ApprovedAt   datetime2(0)   NULL,
        CreatedVia   varchar(10)    NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_CreatedVia DEFAULT ('web'),
        CreatedBy    int            NULL,
        CreatedAt    datetime2(0)   NOT NULL CONSTRAINT DF_DST_CUSTOMER_PROFILES_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_DST_CUSTOMER_PROFILES PRIMARY KEY CLUSTERED (CustomerID),
        CONSTRAINT FK_DST_CUSTOMER_PROFILES_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_CUSTOMER_PROFILES_Territory FOREIGN KEY (TerritoryID) REFERENCES dst.TERRITORIES (ID),
        CONSTRAINT FK_DST_CUSTOMER_PROFILES_Agent FOREIGN KEY (AgentUserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_CUSTOMER_PROFILES_PriceType FOREIGN KEY (PriceTypeID) REFERENCES dst.PRICE_TYPES (ID),
        CONSTRAINT CK_DST_CUSTOMER_PROFILES_Category CHECK (Category IS NULL OR Category IN ('A', 'B', 'C')),
        CONSTRAINT CK_DST_CUSTOMER_PROFILES_Limit CHECK (CreditLimit >= 0 AND PaymentDays >= 0),
        CONSTRAINT CK_DST_CUSTOMER_PROFILES_Geo CHECK ((Lat IS NULL AND Lng IS NULL) OR
            (Lat BETWEEN -90 AND 90 AND Lng BETWEEN -180 AND 180))
    );
    CREATE INDEX IX_DST_CUSTOMER_PROFILES_Agent ON dst.CUSTOMER_PROFILES (AgentUserID);
    CREATE INDEX IX_DST_CUSTOMER_PROFILES_Territory ON dst.CUSTOMER_PROFILES (TerritoryID);
    CREATE INDEX IX_DST_CUSTOMER_PROFILES_INN ON dst.CUSTOMER_PROFILES (INN) WHERE INN IS NOT NULL;
END
GO

IF OBJECT_ID('dst.BRANDS', 'U') IS NULL
    CREATE TABLE dst.BRANDS (
        ID       int IDENTITY(1, 1) NOT NULL,
        Name     nvarchar(100) NOT NULL,
        IsActive bit           NOT NULL CONSTRAINT DF_DST_BRANDS_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_BRANDS PRIMARY KEY CLUSTERED (ID)
    );
GO

IF OBJECT_ID('dst.MANUFACTURERS', 'U') IS NULL
    CREATE TABLE dst.MANUFACTURERS (
        ID       int IDENTITY(1, 1) NOT NULL,
        Name     nvarchar(100) NOT NULL,
        Country  nvarchar(50)  NULL,
        IsActive bit           NOT NULL CONSTRAINT DF_DST_MANUFACTURERS_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_MANUFACTURERS PRIMARY KEY CLUSTERED (ID)
    );
GO

IF OBJECT_ID('dst.SUB_CATEGORIES', 'U') IS NULL
    CREATE TABLE dst.SUB_CATEGORIES (
        ID         int IDENTITY(1, 1) NOT NULL,
        CategoryID int           NOT NULL,
        Name       nvarchar(100) NOT NULL,
        NameRu     nvarchar(100) NULL,
        IsActive   bit           NOT NULL CONSTRAINT DF_DST_SUB_CATEGORIES_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_SUB_CATEGORIES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_SUB_CATEGORIES_Category FOREIGN KEY (CategoryID) REFERENCES dbo.PRODUCT_CATEGORIES (ID)
    );
GO

IF OBJECT_ID('dst.PRODUCT_PROFILES', 'U') IS NULL
    CREATE TABLE dst.PRODUCT_PROFILES (
        ProductID      int            NOT NULL,
        NameRu         nvarchar(150)  NULL,
        BrandID        int            NULL,
        ManufacturerID int            NULL,
        SubCategoryID  int            NULL,
        IsSaleActive   bit            NOT NULL CONSTRAINT DF_DST_PRODUCT_PROFILES_IsSaleActive DEFAULT (1),
        IsBulky        bit            NOT NULL CONSTRAINT DF_DST_PRODUCT_PROFILES_IsBulky DEFAULT (0),
        IsWeighted     bit            NOT NULL CONSTRAINT DF_DST_PRODUCT_PROFILES_IsWeighted DEFAULT (0),
        BaseWeightKg   decimal(18, 4) NULL,   -- asosiy birlik og'irligi
        BaseVolumeM3   decimal(18, 6) NULL,   -- asosiy birlik hajmi
        CONSTRAINT PK_DST_PRODUCT_PROFILES PRIMARY KEY CLUSTERED (ProductID),
        CONSTRAINT FK_DST_PRODUCT_PROFILES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_PRODUCT_PROFILES_Brand FOREIGN KEY (BrandID) REFERENCES dst.BRANDS (ID),
        CONSTRAINT FK_DST_PRODUCT_PROFILES_Manufacturer FOREIGN KEY (ManufacturerID) REFERENCES dst.MANUFACTURERS (ID),
        CONSTRAINT FK_DST_PRODUCT_PROFILES_SubCategory FOREIGN KEY (SubCategoryID) REFERENCES dst.SUB_CATEGORIES (ID)
    );
GO

IF OBJECT_ID('dst.PRODUCT_UNITS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.PRODUCT_UNITS (
        ID            int IDENTITY(1, 1) NOT NULL,
        ProductID     int            NOT NULL,
        UnitID        int            NOT NULL,
        Factor        decimal(18, 6) NOT NULL,   -- 1 shu birlik = Factor asosiy birlik
        WeightKg      decimal(18, 4) NULL,
        VolumeM3      decimal(18, 6) NULL,
        IsDefaultSale bit            NOT NULL CONSTRAINT DF_DST_PRODUCT_UNITS_IsDefaultSale DEFAULT (0),
        IsActive      bit            NOT NULL CONSTRAINT DF_DST_PRODUCT_UNITS_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_PRODUCT_UNITS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_PRODUCT_UNITS_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_PRODUCT_UNITS_UNITS FOREIGN KEY (UnitID) REFERENCES dbo.UNITS (ID),
        CONSTRAINT CK_DST_PRODUCT_UNITS_Factor CHECK (Factor > 0)
    );
    CREATE UNIQUE INDEX UX_DST_PRODUCT_UNITS ON dst.PRODUCT_UNITS (ProductID, UnitID);
END
GO

IF OBJECT_ID('dst.PRODUCT_BARCODES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.PRODUCT_BARCODES (
        ID            int IDENTITY(1, 1) NOT NULL,
        Barcode       varchar(50) NOT NULL,
        ProductID     int         NOT NULL,
        ProductUnitID int         NULL,        -- NULL = asosiy birlik
        CONSTRAINT PK_DST_PRODUCT_BARCODES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_PRODUCT_BARCODES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT FK_DST_PRODUCT_BARCODES_Unit FOREIGN KEY (ProductUnitID) REFERENCES dst.PRODUCT_UNITS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_PRODUCT_BARCODES ON dst.PRODUCT_BARCODES (Barcode);
    CREATE INDEX IX_DST_PRODUCT_BARCODES_Product ON dst.PRODUCT_BARCODES (ProductID);
END
GO

IF OBJECT_ID('dst.PRICE_LIST_ITEMS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.PRICE_LIST_ITEMS (
        ID          int IDENTITY(1, 1) NOT NULL,
        PriceTypeID int            NOT NULL,
        ProductID   int            NOT NULL,
        Price       decimal(19, 2) NOT NULL,   -- asosiy birlik uchun, so'm
        MinPrice    decimal(19, 2) NULL,
        ValidFrom   datetime2(0)   NOT NULL,
        CreatedBy   int            NULL,
        CreatedAt   datetime2(0)   NOT NULL CONSTRAINT DF_DST_PRICE_LIST_ITEMS_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_DST_PRICE_LIST_ITEMS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_PRICE_LIST_ITEMS_Type FOREIGN KEY (PriceTypeID) REFERENCES dst.PRICE_TYPES (ID),
        CONSTRAINT FK_DST_PRICE_LIST_ITEMS_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT CK_DST_PRICE_LIST_ITEMS_Price CHECK (Price >= 0 AND (MinPrice IS NULL OR (MinPrice >= 0 AND MinPrice <= Price)))
    );
    CREATE INDEX IX_DST_PRICE_LIST_ITEMS_Lookup ON dst.PRICE_LIST_ITEMS (PriceTypeID, ProductID, ValidFrom DESC) INCLUDE (Price, MinPrice);
END
GO

IF OBJECT_ID('dst.CUSTOMER_PRICES', 'U') IS NULL
BEGIN
    CREATE TABLE dst.CUSTOMER_PRICES (
        ID         int IDENTITY(1, 1) NOT NULL,
        CustomerID int            NOT NULL,
        ProductID  int            NOT NULL,
        Price      decimal(19, 2) NOT NULL,
        ValidFrom  date           NOT NULL,
        ValidTo    date           NULL,
        CreatedBy  int            NULL,
        CreatedAt  datetime2(0)   NOT NULL CONSTRAINT DF_DST_CUSTOMER_PRICES_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_DST_CUSTOMER_PRICES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_CUSTOMER_PRICES_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_CUSTOMER_PRICES_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT CK_DST_CUSTOMER_PRICES CHECK (Price >= 0 AND (ValidTo IS NULL OR ValidTo >= ValidFrom))
    );
    CREATE INDEX IX_DST_CUSTOMER_PRICES_Lookup ON dst.CUSTOMER_PRICES (CustomerID, ProductID, ValidFrom DESC);
END
GO

IF OBJECT_ID('dst.SALES_TEAMS', 'U') IS NULL
    CREATE TABLE dst.SALES_TEAMS (
        ID               int IDENTITY(1, 1) NOT NULL,
        Name             nvarchar(100) NOT NULL,
        SupervisorUserID int           NULL,
        IsActive         bit           NOT NULL CONSTRAINT DF_DST_SALES_TEAMS_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_SALES_TEAMS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_SALES_TEAMS_Supervisor FOREIGN KEY (SupervisorUserID) REFERENCES dbo.USERS (ID)
    );
GO

IF OBJECT_ID('dst.SALES_AGENTS', 'U') IS NULL
    CREATE TABLE dst.SALES_AGENTS (
        UserID       int          NOT NULL,
        TeamID       int          NULL,
        WarehouseID  int          NULL,     -- odatiy jo'natish ombori
        Phone        varchar(25)  NULL,
        IsExpeditor  bit          NOT NULL CONSTRAINT DF_DST_SALES_AGENTS_IsExpeditor DEFAULT (0),
        IsActive     bit          NOT NULL CONSTRAINT DF_DST_SALES_AGENTS_IsActive DEFAULT (1),
        CONSTRAINT PK_DST_SALES_AGENTS PRIMARY KEY CLUSTERED (UserID),
        CONSTRAINT FK_DST_SALES_AGENTS_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_SALES_AGENTS_Team FOREIGN KEY (TeamID) REFERENCES dst.SALES_TEAMS (ID),
        CONSTRAINT FK_DST_SALES_AGENTS_Warehouse FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID)
    );
GO

IF OBJECT_ID('dst.DISCOUNT_LIMITS', 'U') IS NULL
    CREATE TABLE dst.DISCOUNT_LIMITS (
        RoleID     int          NOT NULL,
        MaxPercent decimal(5, 2) NOT NULL,
        CONSTRAINT PK_DST_DISCOUNT_LIMITS PRIMARY KEY CLUSTERED (RoleID),
        CONSTRAINT FK_DST_DISCOUNT_LIMITS_ROLES FOREIGN KEY (RoleID) REFERENCES dbo.ROLES (ID),
        CONSTRAINT CK_DST_DISCOUNT_LIMITS CHECK (MaxPercent BETWEEN 0 AND 100)
    );
GO

IF OBJECT_ID('dst.REASONS', 'U') IS NULL
    CREATE TABLE dst.REASONS (
        ID        int IDENTITY(1, 1) NOT NULL,
        Type      varchar(20)   NOT NULL,   -- RETURN / NO_ORDER / CANCEL / WRITE_OFF
        Name      nvarchar(100) NOT NULL,
        NameRu    nvarchar(100) NULL,
        IsActive  bit           NOT NULL CONSTRAINT DF_DST_REASONS_IsActive DEFAULT (1),
        SortOrder int           NOT NULL CONSTRAINT DF_DST_REASONS_SortOrder DEFAULT (0),
        CONSTRAINT PK_DST_REASONS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT CK_DST_REASONS_Type CHECK (Type IN ('RETURN', 'NO_ORDER', 'CANCEL', 'WRITE_OFF'))
    );
GO

IF OBJECT_ID('dst.WAREHOUSE_SETTINGS', 'U') IS NULL
    CREATE TABLE dst.WAREHOUSE_SETTINGS (
        WarehouseID    int           NOT NULL,
        IsSalesPoint   bit           NOT NULL CONSTRAINT DF_DST_WAREHOUSE_SETTINGS_IsSalesPoint DEFAULT (1), -- distributsiya savdosida ishlatiladi
        AllowNegative  bit           NOT NULL CONSTRAINT DF_DST_WAREHOUSE_SETTINGS_AllowNegative DEFAULT (0),
        DeliveryZone   nvarchar(200) NULL,
        CONSTRAINT PK_DST_WAREHOUSE_SETTINGS PRIMARY KEY CLUSTERED (WarehouseID),
        CONSTRAINT FK_DST_WAREHOUSE_SETTINGS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID)
    );
GO

IF OBJECT_ID('dst.PRODUCT_STOCK_LIMITS', 'U') IS NULL
    CREATE TABLE dst.PRODUCT_STOCK_LIMITS (
        WarehouseID int            NOT NULL,
        ProductID   int            NOT NULL,
        MinQty      decimal(18, 4) NOT NULL,   -- asosiy birlikda
        CONSTRAINT PK_DST_PRODUCT_STOCK_LIMITS PRIMARY KEY CLUSTERED (WarehouseID, ProductID),
        CONSTRAINT FK_DST_PRODUCT_STOCK_LIMITS_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID),
        CONSTRAINT FK_DST_PRODUCT_STOCK_LIMITS_PRODUCTS FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT CK_DST_PRODUCT_STOCK_LIMITS CHECK (MinQty >= 0)
    );
GO

-- ---------- boshlang'ich ma'lumot (har bir bazada, idempotent) ----------
IF NOT EXISTS (SELECT 1 FROM dst.PRICE_TYPES)
    INSERT INTO dst.PRICE_TYPES (Name, NameRu, IsDefault, SortOrder) VALUES
        (N'Chakana', N'Розница', 0, 1),
        (N'Ulgurji', N'Опт', 1, 2),
        (N'Yirik ulgurji', N'Крупный опт', 0, 3),
        (N'Diler', N'Дилер', 0, 4);

IF NOT EXISTS (SELECT 1 FROM dst.REASONS)
    INSERT INTO dst.REASONS (Type, Name, NameRu, SortOrder) VALUES
        ('NO_ORDER', N'Do''kon yopiq', N'Магазин закрыт', 1),
        ('NO_ORDER', N'Pul yo''q', N'Нет денег', 2),
        ('NO_ORDER', N'Tovar bor', N'Товар есть', 3),
        ('NO_ORDER', N'Egasi yo''q', N'Нет владельца', 4),
        ('RETURN', N'Brak', N'Брак', 1),
        ('RETURN', N'Muddati o''tgan', N'Просрочен', 2),
        ('RETURN', N'Noto''g''ri tovar', N'Не тот товар', 3),
        ('RETURN', N'Mijoz rad etdi', N'Отказ клиента', 4),
        ('CANCEL', N'Mijoz bekor qildi', N'Клиент отменил', 1),
        ('CANCEL', N'Qoldiq yetmadi', N'Нет остатка', 2),
        ('CANCEL', N'Xato kiritilgan', N'Ошибка ввода', 3),
        ('WRITE_OFF', N'Sinish', N'Бой', 1),
        ('WRITE_OFF', N'Buzilish', N'Порча', 2),
        ('WRITE_OFF', N'Kamomad', N'Недостача', 3);
GO

-- ---------- ruxsat modullari (har controller uchun bitta yozuv; Create/Edit — Change huquqi) ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstTerritories'), ('DstCustomers'), ('DstProducts'), ('DstDictionaries'), ('DstPrices'),
                 ('DstTeams'), ('DstSettings'), ('DstImport')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstTerritories', 'DstCustomers', 'DstProducts', 'DstDictionaries', 'DstPrices',
                            'DstTeams', 'DstSettings', 'DstImport')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
