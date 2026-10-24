-- tahsin_site sxemasi. Idempotent; har buyruq bo'sh qator bilan ajratiladi (GO ishlatilmaydi).

IF OBJECT_ID('dbo.SiteTenants', 'U') IS NULL
CREATE TABLE dbo.SiteTenants (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SiteTenants PRIMARY KEY,
    Slug        nvarchar(50)  NOT NULL,
    ErpBaseUrl  nvarchar(300) NOT NULL,
    ErpTenant   nvarchar(50)  NULL,
    IsActive    bit NOT NULL CONSTRAINT DF_SiteTenants_IsActive DEFAULT 1,
    CreatedAt   datetime2(0) NOT NULL CONSTRAINT DF_SiteTenants_CreatedAt DEFAULT SYSUTCDATETIME()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SiteTenants_Slug')
    CREATE UNIQUE INDEX UX_SiteTenants_Slug ON dbo.SiteTenants (Slug);

IF OBJECT_ID('dbo.SiteSnapshots', 'U') IS NULL
CREATE TABLE dbo.SiteSnapshots (
    TenantId  int NOT NULL CONSTRAINT PK_SiteSnapshots PRIMARY KEY CONSTRAINT FK_SiteSnapshots_Tenant REFERENCES dbo.SiteTenants (Id),
    ETag      nvarchar(100) NOT NULL,
    Json      nvarchar(max) NOT NULL,
    FetchedAt datetime2(0) NOT NULL
);

IF OBJECT_ID('dbo.LeadOutbox', 'U') IS NULL
CREATE TABLE dbo.LeadOutbox (
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeadOutbox PRIMARY KEY,
    TenantId        int NOT NULL CONSTRAINT FK_LeadOutbox_Tenant REFERENCES dbo.SiteTenants (Id),
    ClientRequestId uniqueidentifier NOT NULL,
    Payload         nvarchar(max) NOT NULL,
    CreatedAt       datetime2(3) NOT NULL,
    NextAt          datetime2(3) NOT NULL,
    SentAt          datetime2(3) NULL,
    FailedAt        datetime2(3) NULL,
    Attempts        int NOT NULL CONSTRAINT DF_LeadOutbox_Attempts DEFAULT 0,
    Error           nvarchar(1000) NULL,
    ErpLeadId       int NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_LeadOutbox_Request')
    CREATE UNIQUE INDEX UX_LeadOutbox_Request ON dbo.LeadOutbox (ClientRequestId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeadOutbox_Pending')
    CREATE INDEX IX_LeadOutbox_Pending ON dbo.LeadOutbox (NextAt) WHERE SentAt IS NULL AND FailedAt IS NULL;

-- O'z domeni (boshqaruv paneli ulaydi): firma.uz -> shu tenant; RedirectToDomain — standart subdomendan 301 (sertifikat tayyor bo'lgach)
IF COL_LENGTH('dbo.SiteTenants', 'CustomDomain') IS NULL
    ALTER TABLE dbo.SiteTenants ADD CustomDomain nvarchar(253) NULL;

IF COL_LENGTH('dbo.SiteTenants', 'RedirectToDomain') IS NULL
    ALTER TABLE dbo.SiteTenants ADD RedirectToDomain bit NOT NULL CONSTRAINT DF_SiteTenants_RedirectToDomain DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SiteTenants_CustomDomain')
    EXEC('CREATE UNIQUE INDEX UX_SiteTenants_CustomDomain ON dbo.SiteTenants (CustomDomain) WHERE CustomDomain IS NOT NULL');
