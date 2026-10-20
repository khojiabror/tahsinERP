-- tahsin_bot sxemasi. Idempotent: har ishga tushishda qo'llanadi (faqat yo'q obyektlar yaratiladi).
-- Har buyruq bo'sh qator bilan ajratiladi (GO ishlatilmaydi).

IF OBJECT_ID('dbo.BotTenants', 'U') IS NULL
CREATE TABLE dbo.BotTenants (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BotTenants PRIMARY KEY,
    Slug            nvarchar(50)  NOT NULL,
    ErpBaseUrl      nvarchar(300) NOT NULL,
    ErpTenant       nvarchar(50)  NULL,
    TokenEnc        nvarchar(500) NOT NULL,
    WebhookSecret   nvarchar(100) NOT NULL,
    BotUsername     nvarchar(100) NULL,
    IsActive        bit NOT NULL CONSTRAINT DF_BotTenants_IsActive DEFAULT 1,
    CreatedAt       datetime2(0) NOT NULL CONSTRAINT DF_BotTenants_CreatedAt DEFAULT SYSUTCDATETIME(),
    LastSummaryDate date NULL
);

IF COL_LENGTH('dbo.BotTenants', 'ErpTenant') IS NULL
    ALTER TABLE dbo.BotTenants ADD ErpTenant nvarchar(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BotTenants_Slug')
    CREATE UNIQUE INDEX UX_BotTenants_Slug ON dbo.BotTenants (Slug);

IF OBJECT_ID('dbo.BotInbox', 'U') IS NULL
CREATE TABLE dbo.BotInbox (
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_BotInbox PRIMARY KEY,
    TenantId    int NOT NULL CONSTRAINT FK_BotInbox_Tenant REFERENCES dbo.BotTenants (Id),
    UpdateId    bigint NOT NULL,
    ChatId      bigint NULL,
    Payload     nvarchar(max) NOT NULL,
    ReceivedAt  datetime2(3) NOT NULL,
    LockedUntil datetime2(3) NULL,
    ProcessedAt datetime2(3) NULL,
    Attempts    int NOT NULL CONSTRAINT DF_BotInbox_Attempts DEFAULT 0,
    Error       nvarchar(1000) NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BotInbox_Update')
    CREATE UNIQUE INDEX UX_BotInbox_Update ON dbo.BotInbox (TenantId, UpdateId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BotInbox_Pending')
    CREATE INDEX IX_BotInbox_Pending ON dbo.BotInbox (Id) INCLUDE (LockedUntil, Attempts) WHERE ProcessedAt IS NULL;

IF OBJECT_ID('dbo.BotOutbox', 'U') IS NULL
CREATE TABLE dbo.BotOutbox (
    Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_BotOutbox PRIMARY KEY,
    TenantId          int NOT NULL CONSTRAINT FK_BotOutbox_Tenant REFERENCES dbo.BotTenants (Id),
    ChatId            bigint NOT NULL,
    Method            nvarchar(50) NOT NULL,
    Payload           nvarchar(max) NOT NULL,
    Priority          tinyint NOT NULL CONSTRAINT DF_BotOutbox_Priority DEFAULT 0,
    CreatedAt         datetime2(3) NOT NULL,
    NotBefore         datetime2(3) NOT NULL,
    SentAt            datetime2(3) NULL,
    FailedAt          datetime2(3) NULL,
    Attempts          int NOT NULL CONSTRAINT DF_BotOutbox_Attempts DEFAULT 0,
    Error             nvarchar(1000) NULL,
    ErpNotificationId bigint NULL,
    AckedAt           datetime2(3) NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BotOutbox_Pending')
    CREATE INDEX IX_BotOutbox_Pending ON dbo.BotOutbox (Priority, Id) INCLUDE (TenantId, ChatId, NotBefore) WHERE SentAt IS NULL AND FailedAt IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BotOutbox_ErpNotification')
    CREATE UNIQUE INDEX UX_BotOutbox_ErpNotification ON dbo.BotOutbox (TenantId, ErpNotificationId) WHERE ErpNotificationId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BotOutbox_Ack')
    CREATE INDEX IX_BotOutbox_Ack ON dbo.BotOutbox (TenantId) INCLUDE (ErpNotificationId, SentAt, FailedAt) WHERE ErpNotificationId IS NOT NULL AND AckedAt IS NULL;

IF OBJECT_ID('dbo.BotLinks', 'U') IS NULL
CREATE TABLE dbo.BotLinks (
    TenantId       int NOT NULL CONSTRAINT FK_BotLinks_Tenant REFERENCES dbo.BotTenants (Id),
    TelegramUserId bigint NOT NULL,
    ChatId         bigint NOT NULL,
    UserId         int NULL,
    CustomerId     int NULL,
    Role           nvarchar(20) NOT NULL,
    Name           nvarchar(200) NULL,
    Language       nvarchar(5) NOT NULL CONSTRAINT DF_BotLinks_Language DEFAULT 'uz',
    LinkedAt       datetime2(0) NOT NULL,
    CONSTRAINT PK_BotLinks PRIMARY KEY (TenantId, TelegramUserId)
);

IF OBJECT_ID('dbo.BotSessions', 'U') IS NULL
CREATE TABLE dbo.BotSessions (
    TenantId       int NOT NULL CONSTRAINT FK_BotSessions_Tenant REFERENCES dbo.BotTenants (Id),
    TelegramUserId bigint NOT NULL,
    State          nvarchar(50) NOT NULL,
    Data           nvarchar(max) NOT NULL,
    UpdatedAt      datetime2(0) NOT NULL,
    CONSTRAINT PK_BotSessions PRIMARY KEY (TenantId, TelegramUserId)
);
