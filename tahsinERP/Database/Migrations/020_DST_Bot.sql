/* =====================================================================
   020 — Distributsiya: Telegram bot va agent ish kuni (01_PHASE1_MVP.md, S4).

     dst.TELEGRAM_LINK_CODES  bir martalik bog'lash kodi (ERP foydalanuvchisi yoki mijoz) — t.me/<bot>?start=<kod>
     dst.TELEGRAM_ACCOUNTS    bog'langan Telegram hisoblari (bildirishnoma kimga yetkazilishi uchun)
     dst.NOTIFICATIONS        tranzaksion outbox: biznes amal bilan birga yoziladi, bot API orqali tortib oladi
     dst.AGENT_DAYS           agentning ish kuni: boshlanishi (joylashuv, selfi) va yakuni
     dst.VISITS               tashrif: "keldim" (joylashuv, masofa, live/statik), natija (buyurtma, to'lov, buyurtmasiz sabab)
     dst.D_ORDERS.SignatureKey  mijozning ekrandagi imzosi (fayl kaliti)
   Sozlamalar: VisitRadiusM (200), AgentCashAccountId, BotUsername.
   ===================================================================== */

IF OBJECT_ID('dst.TELEGRAM_LINK_CODES', 'U') IS NULL
    CREATE TABLE dst.TELEGRAM_LINK_CODES (
        Code           varchar(16)  NOT NULL,
        UserID         int          NULL,
        CustomerID     int          NULL,
        Role           varchar(20)  NOT NULL,   -- AGENT / EXPEDITOR / MANAGER / STOREKEEPER / CUSTOMER / USER
        ExpiresAt      datetime2(0) NOT NULL,
        UsedAt         datetime2(0) NULL,
        TelegramUserId bigint       NULL,
        CreatedBy      int          NOT NULL,
        CreatedAt      datetime2(0) NOT NULL,
        CONSTRAINT PK_DST_TELEGRAM_LINK_CODES PRIMARY KEY CLUSTERED (Code),
        CONSTRAINT FK_DST_TELEGRAM_LINK_CODES_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_TELEGRAM_LINK_CODES_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT CK_DST_TELEGRAM_LINK_CODES CHECK ((UserID IS NULL AND CustomerID IS NOT NULL) OR (UserID IS NOT NULL AND CustomerID IS NULL))
    );
GO

IF OBJECT_ID('dst.TELEGRAM_ACCOUNTS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.TELEGRAM_ACCOUNTS (
        ID             int IDENTITY(1, 1) NOT NULL,
        TelegramUserId bigint       NOT NULL,
        ChatId         bigint       NOT NULL,
        UserID         int          NULL,
        CustomerID     int          NULL,
        Role           varchar(20)  NOT NULL,
        IsActive       bit          NOT NULL CONSTRAINT DF_DST_TELEGRAM_ACCOUNTS_IsActive DEFAULT (1),
        LinkedAt       datetime2(0) NOT NULL,
        CONSTRAINT PK_DST_TELEGRAM_ACCOUNTS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_TELEGRAM_ACCOUNTS_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_TELEGRAM_ACCOUNTS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_TELEGRAM_ACCOUNTS_Tg ON dst.TELEGRAM_ACCOUNTS (TelegramUserId) WHERE IsActive = 1;
    CREATE INDEX IX_DST_TELEGRAM_ACCOUNTS_User ON dst.TELEGRAM_ACCOUNTS (UserID) WHERE UserID IS NOT NULL;
    CREATE INDEX IX_DST_TELEGRAM_ACCOUNTS_Customer ON dst.TELEGRAM_ACCOUNTS (CustomerID) WHERE CustomerID IS NOT NULL;
END
GO

IF OBJECT_ID('dst.NOTIFICATIONS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.NOTIFICATIONS (
        ID         bigint IDENTITY(1, 1) NOT NULL,
        Type       varchar(30)    NOT NULL,
        UserID     int            NULL,           -- ERP foydalanuvchisiga
        CustomerID int            NULL,           -- mijozga
        Text       nvarchar(2000) NOT NULL,       -- tayyor matn (qabul qiluvchi tilida)
        Payload    nvarchar(max)  NULL,           -- tugmalar va h.k. (JSON)
        CreatedAt  datetime2(0)   NOT NULL,
        SentAt     datetime2(0)   NULL,
        Attempts   int            NOT NULL CONSTRAINT DF_DST_NOTIFICATIONS_Attempts DEFAULT (0),
        CONSTRAINT PK_DST_NOTIFICATIONS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT CK_DST_NOTIFICATIONS CHECK (UserID IS NOT NULL OR CustomerID IS NOT NULL)
    );
    CREATE INDEX IX_DST_NOTIFICATIONS_Pending ON dst.NOTIFICATIONS (ID) WHERE SentAt IS NULL;
END
GO

IF OBJECT_ID('dst.AGENT_DAYS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.AGENT_DAYS (
        ID        int IDENTITY(1, 1) NOT NULL,
        UserID    int           NOT NULL,
        WorkDate  date          NOT NULL,
        StartAt   datetime2(0)  NOT NULL,
        StartLat  decimal(9, 6) NULL,
        StartLng  decimal(9, 6) NULL,
        SelfieKey varchar(200)  NULL,
        EndAt     datetime2(0)  NULL,
        EndLat    decimal(9, 6) NULL,
        EndLng    decimal(9, 6) NULL,
        CONSTRAINT PK_DST_AGENT_DAYS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_AGENT_DAYS_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID)
    );
    CREATE UNIQUE INDEX UX_DST_AGENT_DAYS ON dst.AGENT_DAYS (UserID, WorkDate);
END
GO

IF OBJECT_ID('dst.VISITS', 'U') IS NULL
BEGIN
    CREATE TABLE dst.VISITS (
        ID             int IDENTITY(1, 1) NOT NULL,
        UserID         int           NOT NULL,
        CustomerID     int           NOT NULL,
        VisitDate      date          NOT NULL,
        CheckInAt      datetime2(0)  NOT NULL,
        Lat            decimal(9, 6) NULL,
        Lng            decimal(9, 6) NULL,
        AccuracyM      int           NULL,
        DistanceM      int           NULL,           -- nuqtagacha masofa (mijozda GPS bo'lsa)
        IsLive         bit           NOT NULL CONSTRAINT DF_DST_VISITS_IsLive DEFAULT (0),
        IsPlanned      bit           NOT NULL CONSTRAINT DF_DST_VISITS_IsPlanned DEFAULT (0),
        Result         varchar(10)   NULL,           -- ORDER / PAYMENT / NO_ORDER
        NoOrderReasonID int          NULL,
        OrderID        int           NULL,
        PaymentID      int           NULL,
        PhotoKey       varchar(200)  NULL,
        Note           nvarchar(300) NULL,
        CheckOutAt     datetime2(0)  NULL,
        CONSTRAINT PK_DST_VISITS PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_DST_VISITS_USERS FOREIGN KEY (UserID) REFERENCES dbo.USERS (ID),
        CONSTRAINT FK_DST_VISITS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES dbo.CUSTOMERS (ID),
        CONSTRAINT FK_DST_VISITS_Reason FOREIGN KEY (NoOrderReasonID) REFERENCES dst.REASONS (ID),
        CONSTRAINT CK_DST_VISITS_Result CHECK (Result IS NULL OR Result IN ('ORDER', 'PAYMENT', 'NO_ORDER'))
    );
    CREATE INDEX IX_DST_VISITS_User ON dst.VISITS (UserID, VisitDate);
    CREATE INDEX IX_DST_VISITS_Customer ON dst.VISITS (CustomerID, VisitDate);
END
GO

IF COL_LENGTH('dst.D_ORDERS', 'SignatureKey') IS NULL
    ALTER TABLE dst.D_ORDERS ADD SignatureKey varchar(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'VisitRadiusM')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('VisitRadiusM', '200');
IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'AgentCashAccountId')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('AgentCashAccountId', NULL);
IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'BotUsername')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('BotUsername', NULL);
IF NOT EXISTS (SELECT 1 FROM dst.SETTINGS WHERE [Key] = 'LargeOrderAmount')
    INSERT INTO dst.SETTINGS ([Key], Value) VALUES ('LargeOrderAmount', '0');
GO
