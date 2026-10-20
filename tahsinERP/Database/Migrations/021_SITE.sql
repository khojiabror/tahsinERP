/* =====================================================================
   021 — Sayt-katalog (Docs/Distribution/02_WEBSITE_CATALOG.md): `site` sxemasi.

     site.SETTINGS            sayt sozlamalari (Key/Value): nom, rang, kontaktlar, narx/qoldiq ko'rinishi, arizalar...
     site.DOMAINS             saytning domenlari (standart subdomen + kompaniyaning o'z domeni, tasdiq holati)
     site.CATEGORY_CONTENT    kategoriya nashri: slug, nom/tavsif (uz/ru), rasm, tartib, SEO
     site.PRODUCT_CONTENT     tovar nashri: slug, nom/tavsif (uz/ru), belgilar (yangi/aksiya/xit), tartib, SEO
     site.PRODUCT_ATTRIBUTES  tovar xususiyatlari ("Marka: M400")
     site.MEDIA               rasmlar (bazada; ochiq sayt o'zi kichraytirib keshlaydi)
     site.PRODUCT_MEDIA       tovar galereyasi (tartib bilan)
     site.PAGES               matnli sahifalar (Biz haqimizda, Yetkazib berish...)
     site.BANNERS             bosh sahifa bannerlari (muddat bilan)
     site.LEADS               saytdan kelgan arizalar → mijoz va buyurtma
   Ochiq sayt (tahsinERP.Site) faqat imzoli SiteApi orqali o'qiydi; ERP internetga ochilmaydi.
   ===================================================================== */

IF SCHEMA_ID('site') IS NULL
    EXEC('CREATE SCHEMA site');
GO

IF OBJECT_ID('site.SETTINGS', 'U') IS NULL
    CREATE TABLE site.SETTINGS (
        [Key]     varchar(50)   NOT NULL,
        [Value]   nvarchar(max) NULL,
        UpdatedAt datetime2(0)  NOT NULL CONSTRAINT DF_SITE_SETTINGS_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_SITE_SETTINGS PRIMARY KEY CLUSTERED ([Key])
    );
GO

IF OBJECT_ID('site.DOMAINS', 'U') IS NULL
    CREATE TABLE site.DOMAINS (
        ID         int IDENTITY(1, 1) NOT NULL,
        Host       varchar(253) NOT NULL,
        IsPrimary  bit          NOT NULL CONSTRAINT DF_SITE_DOMAINS_IsPrimary DEFAULT (0),
        Status     tinyint      NOT NULL CONSTRAINT DF_SITE_DOMAINS_Status DEFAULT (0),   -- 0 kutilmoqda, 1 faol, 2 rad
        CreatedAt  datetime2(0) NOT NULL,
        VerifiedAt datetime2(0) NULL,
        CONSTRAINT PK_SITE_DOMAINS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT UQ_SITE_DOMAINS_Host UNIQUE (Host)
    );
GO

IF OBJECT_ID('site.MEDIA', 'U') IS NULL
    CREATE TABLE site.MEDIA (
        ID          int IDENTITY(1, 1) NOT NULL,
        Content     varbinary(max) NOT NULL,
        ContentType varchar(50)    NOT NULL,
        Width       int            NOT NULL,
        Height      int            NOT NULL,
        Bytes       int            NOT NULL,
        Hash        char(64)       NOT NULL,   -- SHA-256 (hex) — sayt keshi va ETag uchun
        AltUz       nvarchar(200)  NULL,
        AltRu       nvarchar(200)  NULL,
        CreatedBy   int            NULL,
        CreatedAt   datetime2(0)   NOT NULL,
        CONSTRAINT PK_SITE_MEDIA PRIMARY KEY CLUSTERED (ID)
    );
GO

IF OBJECT_ID('site.CATEGORY_CONTENT', 'U') IS NULL
    CREATE TABLE site.CATEGORY_CONTENT (
        CategoryID     int           NOT NULL,
        IsPublished    bit           NOT NULL CONSTRAINT DF_SITE_CATEGORY_CONTENT_IsPublished DEFAULT (0),
        Slug           varchar(120)  NOT NULL,
        NameUz         nvarchar(150) NULL,
        NameRu         nvarchar(150) NULL,
        DescriptionUz  nvarchar(2000) NULL,
        DescriptionRu  nvarchar(2000) NULL,
        MediaID        int           NULL,
        SortOrder      int           NOT NULL CONSTRAINT DF_SITE_CATEGORY_CONTENT_SortOrder DEFAULT (0),
        SeoTitle       nvarchar(150) NULL,
        SeoDescription nvarchar(300) NULL,
        UpdatedAt      datetime2(0)  NOT NULL,
        CONSTRAINT PK_SITE_CATEGORY_CONTENT PRIMARY KEY CLUSTERED (CategoryID),
        CONSTRAINT FK_SITE_CATEGORY_CONTENT_CATEGORY FOREIGN KEY (CategoryID) REFERENCES dbo.PRODUCT_CATEGORIES (ID),
        CONSTRAINT FK_SITE_CATEGORY_CONTENT_MEDIA FOREIGN KEY (MediaID) REFERENCES site.MEDIA (ID),
        CONSTRAINT UQ_SITE_CATEGORY_CONTENT_Slug UNIQUE (Slug)
    );
GO

IF OBJECT_ID('site.PRODUCT_CONTENT', 'U') IS NULL
    CREATE TABLE site.PRODUCT_CONTENT (
        ProductID      int            NOT NULL,
        IsPublished    bit            NOT NULL CONSTRAINT DF_SITE_PRODUCT_CONTENT_IsPublished DEFAULT (0),
        Slug           varchar(160)   NOT NULL,
        TitleUz        nvarchar(200)  NULL,     -- bo'sh bo'lsa — tovar nomi
        TitleRu        nvarchar(200)  NULL,     -- bo'sh bo'lsa — PRODUCT_PROFILES.NameRu
        DescriptionUz  nvarchar(max)  NULL,     -- oddiy matn: paragraf, "- " ro'yxat, **qalin** (HTML emas)
        DescriptionRu  nvarchar(max)  NULL,
        Badges         varchar(50)    NULL,     -- NEW,SALE,HIT
        SortOrder      int            NOT NULL CONSTRAINT DF_SITE_PRODUCT_CONTENT_SortOrder DEFAULT (0),
        SeoTitle       nvarchar(150)  NULL,
        SeoDescription nvarchar(300)  NULL,
        UpdatedAt      datetime2(0)   NOT NULL,
        CONSTRAINT PK_SITE_PRODUCT_CONTENT PRIMARY KEY CLUSTERED (ProductID),
        CONSTRAINT FK_SITE_PRODUCT_CONTENT_PRODUCT FOREIGN KEY (ProductID) REFERENCES dbo.PRODUCTS (ID),
        CONSTRAINT UQ_SITE_PRODUCT_CONTENT_Slug UNIQUE (Slug)
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SITE_PRODUCT_CONTENT_Published' AND object_id = OBJECT_ID('site.PRODUCT_CONTENT'))
    CREATE INDEX IX_SITE_PRODUCT_CONTENT_Published ON site.PRODUCT_CONTENT (IsPublished) INCLUDE (Slug, SortOrder);
GO

IF OBJECT_ID('site.PRODUCT_ATTRIBUTES', 'U') IS NULL
    CREATE TABLE site.PRODUCT_ATTRIBUTES (
        ID        int IDENTITY(1, 1) NOT NULL,
        ProductID int           NOT NULL,
        NameUz    nvarchar(100) NOT NULL,
        NameRu    nvarchar(100) NULL,
        ValueUz   nvarchar(200) NOT NULL,
        ValueRu   nvarchar(200) NULL,
        SortOrder int           NOT NULL CONSTRAINT DF_SITE_PRODUCT_ATTRIBUTES_SortOrder DEFAULT (0),
        CONSTRAINT PK_SITE_PRODUCT_ATTRIBUTES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_SITE_PRODUCT_ATTRIBUTES_CONTENT FOREIGN KEY (ProductID) REFERENCES site.PRODUCT_CONTENT (ProductID) ON DELETE CASCADE
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SITE_PRODUCT_ATTRIBUTES_Product' AND object_id = OBJECT_ID('site.PRODUCT_ATTRIBUTES'))
    CREATE INDEX IX_SITE_PRODUCT_ATTRIBUTES_Product ON site.PRODUCT_ATTRIBUTES (ProductID, SortOrder);
GO

IF OBJECT_ID('site.PRODUCT_MEDIA', 'U') IS NULL
    CREATE TABLE site.PRODUCT_MEDIA (
        ProductID int NOT NULL,
        MediaID   int NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_SITE_PRODUCT_MEDIA_SortOrder DEFAULT (0),
        CONSTRAINT PK_SITE_PRODUCT_MEDIA PRIMARY KEY CLUSTERED (ProductID, MediaID),
        CONSTRAINT FK_SITE_PRODUCT_MEDIA_CONTENT FOREIGN KEY (ProductID) REFERENCES site.PRODUCT_CONTENT (ProductID) ON DELETE CASCADE,
        CONSTRAINT FK_SITE_PRODUCT_MEDIA_MEDIA FOREIGN KEY (MediaID) REFERENCES site.MEDIA (ID)
    );
GO

IF OBJECT_ID('site.PAGES', 'U') IS NULL
    CREATE TABLE site.PAGES (
        ID          int IDENTITY(1, 1) NOT NULL,
        Slug        varchar(120)  NOT NULL,
        TitleUz     nvarchar(150) NOT NULL,
        TitleRu     nvarchar(150) NULL,
        BodyUz      nvarchar(max) NULL,
        BodyRu      nvarchar(max) NULL,
        IsPublished bit           NOT NULL CONSTRAINT DF_SITE_PAGES_IsPublished DEFAULT (0),
        ShowInMenu  bit           NOT NULL CONSTRAINT DF_SITE_PAGES_ShowInMenu DEFAULT (1),
        SortOrder   int           NOT NULL CONSTRAINT DF_SITE_PAGES_SortOrder DEFAULT (0),
        UpdatedAt   datetime2(0)  NOT NULL,
        CONSTRAINT PK_SITE_PAGES PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT UQ_SITE_PAGES_Slug UNIQUE (Slug)
    );
GO

IF OBJECT_ID('site.BANNERS', 'U') IS NULL
    CREATE TABLE site.BANNERS (
        ID         int IDENTITY(1, 1) NOT NULL,
        MediaID    int           NOT NULL,
        TitleUz    nvarchar(150) NULL,
        TitleRu    nvarchar(150) NULL,
        SubtitleUz nvarchar(300) NULL,
        SubtitleRu nvarchar(300) NULL,
        Link       nvarchar(300) NULL,
        ActiveFrom date          NULL,
        ActiveTo   date          NULL,
        IsActive   bit           NOT NULL CONSTRAINT DF_SITE_BANNERS_IsActive DEFAULT (1),
        SortOrder  int           NOT NULL CONSTRAINT DF_SITE_BANNERS_SortOrder DEFAULT (0),
        UpdatedAt  datetime2(0)  NOT NULL,
        CONSTRAINT PK_SITE_BANNERS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_SITE_BANNERS_MEDIA FOREIGN KEY (MediaID) REFERENCES site.MEDIA (ID)
    );
GO

IF OBJECT_ID('site.LEADS', 'U') IS NULL
    CREATE TABLE site.LEADS (
        ID              int IDENTITY(1, 1) NOT NULL,
        ClientRequestId uniqueidentifier NOT NULL,
        CreatedAt       datetime2(0)   NOT NULL,
        Name            nvarchar(100)  NOT NULL,
        Phone           varchar(20)    NOT NULL,
        CompanyName     nvarchar(200)  NULL,
        INN             varchar(20)    NULL,
        Message         nvarchar(1000) NULL,
        Items           nvarchar(max)  NULL,     -- JSON: [{productId, productUnitId, qty, name, unit, price}]
        Language        varchar(2)     NOT NULL,
        Host            varchar(253)   NULL,
        Status          tinyint        NOT NULL CONSTRAINT DF_SITE_LEADS_Status DEFAULT (0),   -- 0 yangi, 1 ko'rildi, 2 buyurtma, 3 rad
        CustomerID      int            NULL,
        OrderID         int            NULL,
        ProcessedBy     int            NULL,
        ProcessedAt     datetime2(0)   NULL,
        RejectReason    nvarchar(300)  NULL,
        CONSTRAINT PK_SITE_LEADS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT UQ_SITE_LEADS_ClientRequestId UNIQUE (ClientRequestId),
        CONSTRAINT FK_SITE_LEADS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_SITE_LEADS_ORDERS FOREIGN KEY (OrderID) REFERENCES dst.D_ORDERS (ID)
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SITE_LEADS_Status' AND object_id = OBJECT_ID('site.LEADS'))
    CREATE INDEX IX_SITE_LEADS_Status ON site.LEADS (Status, CreatedAt DESC);
GO

-- ---------- standart sozlamalar ----------
INSERT INTO site.SETTINGS ([Key], [Value])
SELECT v.k, v.v
FROM (VALUES
        ('Enabled', '0'),            -- sayt yoqilgan
        ('Languages', 'uz,ru'),
        ('PriceMode', 'ASK'),        -- SHOW (narx turi bo'yicha) / ASK ("narxni so'rang") / HIDDEN
        ('StockMode', 'STATUS'),     -- EXACT (aniq son) / STATUS (bor/kam/buyurtmaga) / HIDDEN
        ('LowStockQty', '10'),
        ('LeadsEnabled', '1'),
        ('PrimaryColor', '#111827'),
        ('Font', 'Inter'),
        ('Radius', '10')
     ) v (k, v)
WHERE NOT EXISTS (SELECT 1 FROM site.SETTINGS s WHERE s.[Key] = v.k);
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('SiteHome'), ('SiteSettings'), ('SiteCatalog'), ('SitePages'), ('SiteLeads')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('SiteHome', 'SiteSettings', 'SiteCatalog', 'SitePages', 'SiteLeads')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
