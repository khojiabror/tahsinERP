/* =====================================================================
   tahsinERP — BASELINE SXEMA (000)
   Yangi korxona bazasining toza sxemasi: jadvallar, PK/FK/UNIQUE/CHECK/DEFAULT,
   indekslar, view, trigger, saqlanadigan protseduralar. MA'LUMOT YO'Q.

   Manba: lokal DBTHSN-IPACK (master EDMX bilan solishtirilgan), SqlPackage (DacFx)
   orqali generatsiya qilingan — QO'LDA TAHRIR QILMANG. Keyingi har sxema o'zgarishi
   = Database/Migrations/NNN_nomi.sql (qarang: Database/README.md).
   Kirmaydi: sysdiagrams va diagram sp_*, *_BAK_* va DataFix_* jadvallari, foydalanuvchilar/huquqlar.

   Ishga tushirish: Database/tools/Invoke-DbMigrations.ps1 (bo'sh bazada avtomatik 000 ni qo'llaydi).
   Baza collation: Cyrillic_General_CI_AS (New-TenantDb.ps1 shunday yaratadi).
   ===================================================================== */
SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TYPE [dbo].[IntList] AS TABLE (
    [ID] INT NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC));
GO
CREATE TABLE [dbo].[ADM_COMPANY_ORDERS] (
    [DocumentID]    BIGINT         NOT NULL,
    [OrderNo]       NVARCHAR (50)  NOT NULL,
    [OrderDate]     DATE           NOT NULL,
    [OrderTypeCode] NVARCHAR (50)  NULL,
    [OrgUnitID]     INT            NULL,
    [Notes]         NVARCHAR (MAX) NULL,
    [IsDeleted]     BIT            NULL,
    CONSTRAINT [PK_ADM_COMPANY_ORDERS] PRIMARY KEY CLUSTERED ([DocumentID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ADM_COMPANY_ORDERS_No_Date]
    ON [dbo].[ADM_COMPANY_ORDERS]([OrderNo] ASC, [OrderDate] ASC);
GO
CREATE TABLE [dbo].[APP_TASK_PRIORITY_REF] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [SortOrder] INT            NOT NULL,
    [IsDeleted] BIT            NULL,
    [ColorHex]  NVARCHAR (7)   NULL,
    CONSTRAINT [PK_APP_TASK_PRIORITY_REF] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[APP_TASK_PROJECTS] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (200) NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [CreatedDT] DATETIME2 (0)  NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_APP_TASK_PROJECTS] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[APP_TASK_STATUS_REF] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [SortOrder] INT            NOT NULL,
    [IsDeleted] BIT            NULL,
    [IsFinal]   BIT            NOT NULL,
    [ColorHex]  NVARCHAR (7)   NULL,
    CONSTRAINT [PK_APP_TASK_STATUS_REF] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[APP_TASKS] (
    [ID]             BIGINT         IDENTITY (1, 1) NOT NULL,
    [ProjectID]      INT            NULL,
    [KeyCode]        NVARCHAR (50)  NULL,
    [Title]          NVARCHAR (500) NOT NULL,
    [Description]    NVARCHAR (MAX) NULL,
    [StatusID]       INT            NOT NULL,
    [PriorityID]     INT            NOT NULL,
    [AssigneeUserID] INT            NULL,
    [ReporterUserID] INT            NULL,
    [CreatedDT]      DATETIME2 (0)  NOT NULL,
    [UpdatedDT]      DATETIME2 (0)  NULL,
    [DueDate]        DATE           NULL,
    [IsDeleted]      BIT            NOT NULL,
    [UserComment]    NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_APP_TASKS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_APP_TASKS_Assignee_Status]
    ON [dbo].[APP_TASKS]([AssigneeUserID] ASC, [StatusID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_APP_TASKS_Project_Status_Due]
    ON [dbo].[APP_TASKS]([ProjectID] ASC, [StatusID] ASC, [DueDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_APP_TASKS_KeyCode]
    ON [dbo].[APP_TASKS]([KeyCode] ASC);
GO
CREATE TABLE [dbo].[BOM_HEADERS] (
    [ID]        INT           IDENTITY (1, 1) NOT NULL,
    [ProductID] INT           NULL,
    [VersionNo] NVARCHAR (50) NULL,
    [IsActive]  BIT           NULL,
    [CreatedAt] DATETIME      NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[BOM_LINES] (
    [ID]          INT             IDENTITY (1, 1) NOT NULL,
    [BOMID]       INT             NULL,
    [ComponentID] INT             NULL,
    [Qty]         DECIMAL (18, 4) NULL,
    [UnitID]      INT             NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[COM_OFFICIAL_LETTERS] (
    [DocumentID]              BIGINT         NOT NULL,
    [DirectionCode]           NVARCHAR (10)  NOT NULL,
    [LetterNo]                NVARCHAR (50)  NOT NULL,
    [LetterDate]              DATE           NOT NULL,
    [ExternalCompanyName]     NVARCHAR (250) NULL,
    [ExternalContactName]     NVARCHAR (200) NULL,
    [OurRefNo]                NVARCHAR (50)  NULL,
    [TheirRefNo]              NVARCHAR (50)  NULL,
    [ChannelCode]             NVARCHAR (20)  NULL,
    [Topic]                   NVARCHAR (500) NULL,
    [ExternalContactPosition] NVARCHAR (200) NULL,
    [IsDeleted]               BIT            NULL,
    CONSTRAINT [PK_COM_OFFICIAL_LETTERS] PRIMARY KEY CLUSTERED ([DocumentID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_COM_OFFICIAL_LETTERS_No_Date_Dir]
    ON [dbo].[COM_OFFICIAL_LETTERS]([LetterNo] ASC, [LetterDate] ASC, [DirectionCode] ASC);
GO
CREATE TABLE [dbo].[COMPANIES] (
    [ID]           INT            IDENTITY (1, 1) NOT NULL,
    [Name]         VARCHAR (50)   NOT NULL,
    [Country]      VARCHAR (50)   NULL,
    [City]         VARCHAR (50)   NULL,
    [Address]      TEXT           NULL,
    [DUNS]         VARCHAR (50)   NULL,
    [Telephone]    VARCHAR (25)   NULL,
    [Email]        VARCHAR (50)   NULL,
    [IsActive]     BIT            NULL,
    [DirectorName] NVARCHAR (200) NULL,
    CONSTRAINT [PK_COMPANIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[COMPANY_LICENSES] (
    [ID]         INT            IDENTITY (1, 1) NOT NULL,
    [CompanyID]  INT            NOT NULL,
    [LicenseKey] VARCHAR (MAX)  NULL,
    [MaxUsers]   INT            NOT NULL,
    [StartDate]  DATETIME       NULL,
    [ExpiryDate] DATETIME       NULL,
    [Edition]    NVARCHAR (50)  NULL,
    [IsActive]   BIT            NOT NULL,
    [CreatedAt]  DATETIME       NOT NULL,
    [CreatedBy]  NVARCHAR (100) NULL,
    CONSTRAINT [PK_COMPANY_LICENSES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[COOKIES_CONFIRMATION] (
    [ID]           INT          IDENTITY (1, 1) NOT NULL,
    [UserID]       INT          NOT NULL,
    [IssuedDT]     DATETIME     NOT NULL,
    [MacAddress]   VARCHAR (50) NOT NULL,
    [Confirmation] BIT          NOT NULL,
    [IPAddress]    VARCHAR (24) NULL,
    CONSTRAINT [PK_COOKIES_CONFIRMATION] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[CURRENCIES] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Longname]    VARCHAR (MAX) NOT NULL,
    [Shortname]   VARCHAR (25)  NOT NULL,
    [Description] TEXT          NOT NULL,
    [IsDeleted]   BIT           NULL,
    CONSTRAINT [PK_CURRENCIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[CURRENCY_RATES] (
    [ID]         INT             IDENTITY (1, 1) NOT NULL,
    [CurrencyID] INT             NOT NULL,
    [RateDate]   DATE            NOT NULL,
    [Rate]       DECIMAL (18, 6) NOT NULL,
    [Source]     NVARCHAR (20)   NOT NULL,
    [CreatedDT]  DATETIME        NOT NULL,
    [UpdatedDT]  DATETIME        NULL,
    CONSTRAINT [PK_CURRENCY_RATES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_CURRENCY_RATES_Currency_Date]
    ON [dbo].[CURRENCY_RATES]([CurrencyID] ASC, [RateDate] ASC)
    INCLUDE([Rate]);
GO
CREATE TABLE [dbo].[CUSTOMER_BASE] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [CompanyName]   NVARCHAR (255) NULL,
    [ProductType]   NVARCHAR (255) NULL,
    [Country]       NVARCHAR (100) NULL,
    [Province]      NVARCHAR (100) NULL,
    [Region]        NVARCHAR (100) NULL,
    [ContactPerson] NVARCHAR (255) NULL,
    [Position]      NVARCHAR (100) NULL,
    [Phone]         NVARCHAR (50)  NULL,
    [Comment]       NVARCHAR (MAX) NULL,
    [IsDeleted]     BIT            NULL,
    CONSTRAINT [PK_CUSTOMER_BASE] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_CUSTOMER_BASE_IsDeleted]
    ON [dbo].[CUSTOMER_BASE]([IsDeleted] ASC) WHERE ([IsDeleted]=(0));
GO
CREATE NONCLUSTERED INDEX [IX_CUSTOMER_BASE_ProductType]
    ON [dbo].[CUSTOMER_BASE]([ProductType] ASC) WHERE ([IsDeleted]=(0));
GO
CREATE TABLE [dbo].[CUSTOMERS] (
    [ID]                INT          IDENTITY (1, 1) NOT NULL,
    [Name]              VARCHAR (50) NOT NULL,
    [Address]           TEXT         NULL,
    [Country]           VARCHAR (30) NULL,
    [Telephone]         VARCHAR (25) NULL,
    [Email]             VARCHAR (50) NULL,
    [ContactPersonName] VARCHAR (30) NULL,
    [DirectorName]      VARCHAR (30) NULL,
    [IsDeleted]         BIT          NULL,
    [DUNS]              VARCHAR (50) NULL,
    [Type]              VARCHAR (10) NULL,
    [City]              VARCHAR (50) NULL,
    CONSTRAINT [PK_CUSTOMERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[DAMAGED_PRODUCTS] (
    [ID]            INT        IDENTITY (1, 1) NOT NULL,
    [ProductID]     INT        NOT NULL,
    [DefectTypeID]  INT        NOT NULL,
    [Quantity]      FLOAT (53) NOT NULL,
    [IssueDateTime] DATETIME   NOT NULL,
    [IsDeleted]     BIT        NOT NULL,
    CONSTRAINT [PK_DAMAGED_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_DAMAGED_PRODUCTS_Product_Date]
    ON [dbo].[DAMAGED_PRODUCTS]([ProductID] ASC, [IssueDateTime] ASC)
    INCLUDE([DefectTypeID], [IsDeleted]);
GO
CREATE TABLE [dbo].[DEFECT_TYPES] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [DefectType]  VARCHAR (30) NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NULL,
    CONSTRAINT [PK_DEFECT_TYPES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[DOC_DOCUMENT_STATUS_REF] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (200) NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [SortOrder] INT            NOT NULL,
    CONSTRAINT [PK_DOC_DOCUMENT_STATUS_REF] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[DOC_DOCUMENT_TYPES] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (200) NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [SortOrder] INT            NOT NULL,
    CONSTRAINT [PK_DOC_DOCUMENT_TYPES] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[DOC_DOCUMENTS] (
    [ID]                    BIGINT           IDENTITY (1, 1) NOT NULL,
    [DocTypeID]             INT              NOT NULL,
    [StatusID]              INT              NOT NULL,
    [Code]                  NVARCHAR (50)    NULL,
    [Title]                 NVARCHAR (500)   NOT NULL,
    [OrgUnitID]             INT              NULL,
    [PositionID]            INT              NULL,
    [EmployeeID]            INT              NULL,
    [TemplateID]            INT              NULL,
    [EffectiveFrom]         DATE             NULL,
    [EffectiveTo]           DATE             NULL,
    [CreatedByUserID]       INT              NULL,
    [ApprovedByUserID]      INT              NULL,
    [CreatedDT]             DATETIME2 (0)    NOT NULL,
    [UpdatedDT]             DATETIME2 (0)    NULL,
    [IntegrationUID]        UNIQUEIDENTIFIER NOT NULL,
    [ExternalSystemCode]    NVARCHAR (50)    NULL,
    [ExternalDocKey]        NVARCHAR (100)   NULL,
    [IntegrationStatusCode] NVARCHAR (50)    NULL,
    [LastSyncDT]            DATETIME2 (0)    NULL,
    [IsDeleted]             BIT              NULL,
    CONSTRAINT [PK_DOC_DOCUMENTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_DOC_DOCUMENTS_OrgUnit]
    ON [dbo].[DOC_DOCUMENTS]([OrgUnitID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_DOC_DOCUMENTS_Position]
    ON [dbo].[DOC_DOCUMENTS]([PositionID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_DOC_DOCUMENTS_Code]
    ON [dbo].[DOC_DOCUMENTS]([Code] ASC)
    INCLUDE([IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_DOC_DOCUMENTS_Type_Status]
    ON [dbo].[DOC_DOCUMENTS]([DocTypeID] ASC, [StatusID] ASC);
GO
CREATE TABLE [dbo].[DOC_FILES] (
    [ID]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [DocumentID]      BIGINT          NOT NULL,
    [VersionNo]       INT             NOT NULL,
    [FileName]        NVARCHAR (260)  NOT NULL,
    [FileExtension]   NVARCHAR (20)   NOT NULL,
    [MimeType]        NVARCHAR (100)  NULL,
    [FileSizeBytes]   BIGINT          NULL,
    [FileContent]     VARBINARY (MAX) NOT NULL,
    [IsMain]          BIT             NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME2 (0)   NOT NULL,
    [IsDeleted]       BIT             NULL,
    CONSTRAINT [PK_DOC_FILES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_DOC_FILES_Document]
    ON [dbo].[DOC_FILES]([DocumentID] ASC, [VersionNo] ASC);
GO
CREATE TABLE [dbo].[DOC_TEMPLATES] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [DocTypeID]       INT             NOT NULL,
    [TemplateCode]    NVARCHAR (50)   NOT NULL,
    [Name]            NVARCHAR (200)  NOT NULL,
    [FileName]        NVARCHAR (260)  NOT NULL,
    [FileExtension]   NVARCHAR (20)   NOT NULL,
    [MimeType]        NVARCHAR (100)  NULL,
    [FileContent]     VARBINARY (MAX) NULL,
    [IsActive]        BIT             NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME2 (0)   NOT NULL,
    CONSTRAINT [PK_DOC_TEMPLATES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DOC_TEMPLATES_Code]
    ON [dbo].[DOC_TEMPLATES]([DocTypeID] ASC, [TemplateCode] ASC);
GO
CREATE TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [ExpenseProductID] INT             NOT NULL,
    [LotID]            INT             NOT NULL,
    [FromLocationID]   INT             NOT NULL,
    [Qty]              DECIMAL (18, 4) NOT NULL,
    [CreatedAt]        DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_EPA_LotID]
    ON [dbo].[EXPENSE_PRODUCT_ALLOCATIONS]([LotID] ASC)
    INCLUDE([ExpenseProductID], [Qty]);
GO
CREATE TABLE [dbo].[EXPENSE_PRODUCTS] (
    [ID]                  INT           IDENTITY (1, 1) NOT NULL,
    [ExpenseID]           INT           NOT NULL,
    [ProductID]           INT           NOT NULL,
    [Amount]              FLOAT (53)    NOT NULL,
    [PiecePrice]          FLOAT (53)    NOT NULL,
    [TotalPrice]          AS            ([Amount] * [PiecePrice]),
    [Comment]             TEXT          NULL,
    [UnitID]              INT           NOT NULL,
    [LotID]               INT           NULL,
    [FromLocationID]      INT           NULL,
    [AllocationPolicy]    NVARCHAR (20) NULL,
    [ViolationReasonCode] NVARCHAR (50) NULL,
    CONSTRAINT [PK_PROD_WRHS_EXPENSE_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[EXPENSES] (
    [ID]            INT          IDENTITY (1, 1) NOT NULL,
    [DocNo]         VARCHAR (50) NOT NULL,
    [Amount]        FLOAT (53)   NULL,
    [TotalPrice]    FLOAT (53)   NULL,
    [ReceiverWhID]  INT          NULL,
    [SenderWHID]    INT          NOT NULL,
    [IsDeleted]     BIT          NOT NULL,
    [Description]   TEXT         NULL,
    [IssueDateTime] DATETIME     NOT NULL,
    [SendStatus]    BIT          NULL,
    [CurrencyID]    INT          NOT NULL,
    [ProductionID]  INT          NULL,
    CONSTRAINT [PK_PROD_WRHS_EXPENSES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_EXPENSES_DocNo]
    ON [dbo].[EXPENSES]([DocNo] ASC)
    INCLUDE([IsDeleted], [SenderWHID], [ReceiverWhID]);
GO
CREATE NONCLUSTERED INDEX [IX_EXPENSES_ProductionID]
    ON [dbo].[EXPENSES]([ProductionID] ASC)
    INCLUDE([IsDeleted], [ReceiverWhID]) WHERE ([ProductionID] IS NOT NULL);
GO
CREATE TABLE [dbo].[F_CONTRACT_DOCS] (
    [ID]         INT             IDENTITY (1, 1) NOT NULL,
    [ContractID] INT             NULL,
    [Doc]        VARBINARY (MAX) NULL,
    CONSTRAINT [PK_F_CONTRACT_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[F_CONTRACT_TARIFFS] (
    [ID]              INT            IDENTITY (1, 1) NOT NULL,
    [ContractID]      INT            NOT NULL,
    [FromRegion]      NVARCHAR (100) NULL,
    [FromCity]        NVARCHAR (100) NULL,
    [ToRegion]        NVARCHAR (100) NULL,
    [ToCity]          NVARCHAR (100) NULL,
    [TransportTypeID] INT            NULL,
    [FreightBasis]    TINYINT        NOT NULL,
    [Rate]            FLOAT (53)     NOT NULL,
    [MinimumCharge]   FLOAT (53)     NULL,
    [CurrencyID]      INT            NULL,
    [IsActive]        BIT            NOT NULL,
    [Description]     NVARCHAR (500) NULL,
    [CreatedDT]       DATETIME       NOT NULL,
    CONSTRAINT [PK_F_CONTRACT_TARIFFS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_F_CONTRACT_TARIFFS_ContractID]
    ON [dbo].[F_CONTRACT_TARIFFS]([ContractID] ASC, [IsActive] ASC);
GO
CREATE TABLE [dbo].[F_CONTRACTS] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [ContractNo]  VARCHAR (50) NOT NULL,
    [CompanyID]   INT          NOT NULL,
    [ForwarderID] INT          NOT NULL,
    [IssueDate]   DATE         NOT NULL,
    [DueDate]     DATE         NOT NULL,
    [Amount]      FLOAT (53)   NOT NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NOT NULL,
    [CurrencyID]  INT          NULL,
    CONSTRAINT [PK_F_CONTRACTS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_F_CONTRACTS] UNIQUE NONCLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[F_TRANSPORT_TYPES] (
    [ID]               INT          IDENTITY (1, 1) NOT NULL,
    [TransportType]    VARCHAR (50) NOT NULL,
    [ExtLgth]          FLOAT (53)   NOT NULL,
    [ExtWdth]          FLOAT (53)   NOT NULL,
    [ExtHght]          FLOAT (53)   NOT NULL,
    [IntLgth]          FLOAT (53)   NOT NULL,
    [IntWdth]          FLOAT (53)   NOT NULL,
    [IntHght]          FLOAT (53)   NOT NULL,
    [CapableOfLifting] FLOAT (53)   NULL,
    [TransportWeight]  FLOAT (53)   NULL,
    [UnitID]           INT          NOT NULL,
    [IsDeleted]        BIT          NULL,
    CONSTRAINT [PK_F_TRANSPORT_TYPES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_F_TRANSPORT_TYPES] UNIQUE NONCLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[F_WAYBILL_DOCS] (
    [ID]        INT             IDENTITY (1, 1) NOT NULL,
    [WaybillID] INT             NOT NULL,
    [Doc]       VARBINARY (MAX) NULL,
    CONSTRAINT [PK_F_WAYBILL_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[F_WAYBILL_PAYMENT_SPLITS] (
    [ID]         INT            IDENTITY (1, 1) NOT NULL,
    [WaybillID]  INT            NOT NULL,
    [PayerType]  TINYINT        NOT NULL,
    [PayerID]    INT            NULL,
    [Amount]     FLOAT (53)     NOT NULL,
    [CurrencyID] INT            NULL,
    [Comment]    NVARCHAR (500) NULL,
    CONSTRAINT [PK_F_WAYBILL_PAYMENT_SPLITS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_F_WPS_Payer]
    ON [dbo].[F_WAYBILL_PAYMENT_SPLITS]([PayerType] ASC, [PayerID] ASC)
    INCLUDE([WaybillID], [Amount]);
GO
CREATE TABLE [dbo].[F_WAYBILL_PAYMENTS] (
    [ID]             INT        IDENTITY (1, 1) NOT NULL,
    [ForwarderID]    INT        NOT NULL,
    [WaybillID]      INT        NOT NULL,
    [Payment_Amount] FLOAT (53) NOT NULL,
    [IsDeleted]      BIT        NOT NULL,
    [Comment]        TEXT       NOT NULL,
    [CurrencyID]     INT        NULL,
    [PaymentDate]    DATETIME   NULL,
    CONSTRAINT [PK_F_WAYBILL_PAYMENTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILL_PAYMENTS_Forwarder_Date]
    ON [dbo].[F_WAYBILL_PAYMENTS]([ForwarderID] ASC, [PaymentDate] ASC)
    INCLUDE([IsDeleted], [Payment_Amount], [WaybillID]);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILL_PAYMENTS_WaybillID]
    ON [dbo].[F_WAYBILL_PAYMENTS]([WaybillID] ASC, [IsDeleted] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILL_PAYMENTS_Waybill]
    ON [dbo].[F_WAYBILL_PAYMENTS]([WaybillID] ASC)
    INCLUDE([IsDeleted], [Payment_Amount]);
GO
CREATE TABLE [dbo].[F_WAYBILLS] (
    [ID]                     INT            IDENTITY (1, 1) NOT NULL,
    [WaybillNo]              VARCHAR (50)   NOT NULL,
    [ContractID]             INT            NULL,
    [TransportTypeID]        INT            NULL,
    [InvoiceID]              INT            NULL,
    [CBM]                    FLOAT (53)     NULL,
    [GrWeight]               FLOAT (53)     NULL,
    [Description]            TEXT           NULL,
    [IsDeleted]              BIT            NULL,
    [Amount]                 FLOAT (53)     NULL,
    [IssueDate]              DATETIME       NULL,
    [Direction]              TINYINT        NULL,
    [FromLocation]           NVARCHAR (200) NULL,
    [ToLocation]             NVARCHAR (200) NULL,
    [DistanceKm]             FLOAT (53)     NULL,
    [FreightBasis]           TINYINT        NULL,
    [Rate]                   FLOAT (53)     NULL,
    [CurrencyID]             INT            NULL,
    [PayerType]              TINYINT        NULL,
    [PayerID]                INT            NULL,
    [SalesInvoiceID]         BIGINT         NULL,
    [ForwarderID]            INT            NULL,
    [TruckNo]                NVARCHAR (50)  NULL,
    [TrailerNo]              NVARCHAR (50)  NULL,
    [TruckModel]             NVARCHAR (100) NULL,
    [TrailerModel]           NVARCHAR (100) NULL,
    [DriverName]             NVARCHAR (200) NULL,
    [DriverPINFL]            NVARCHAR (50)  NULL,
    [NetWeight]              FLOAT (53)     NULL,
    [PalletsCount]           INT            NULL,
    [ManualCorrectionReason] NVARCHAR (500) NULL,
    [PlannedDeliveryDate]    DATETIME       NULL,
    [ActualDeliveryDate]     DATETIME       NULL,
    [TTNKID]                 BIGINT         NULL,
    [SalesOrderID]           INT            NULL,
    [PurchaseOrderID]        INT            NULL,
    [TariffID]               INT            NULL,
    CONSTRAINT [PK_F_WAYBILLS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILLS_Forwarder_Date]
    ON [dbo].[F_WAYBILLS]([ForwarderID] ASC, [IssueDate] ASC)
    INCLUDE([IsDeleted], [Amount]);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILLS_Direction_IssueDate]
    ON [dbo].[F_WAYBILLS]([Direction] ASC, [IssueDate] ASC)
    INCLUDE([ForwarderID], [Amount], [CurrencyID]);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILLS_TariffID]
    ON [dbo].[F_WAYBILLS]([TariffID] ASC) WHERE ([TariffID] IS NOT NULL);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_F_WAYBILLS_WaybillNo_Active]
    ON [dbo].[F_WAYBILLS]([WaybillNo] ASC) WHERE ([IsDeleted]=(0));
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILLS_ForwarderID]
    ON [dbo].[F_WAYBILLS]([ForwarderID] ASC, [IsDeleted] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_F_WAYBILLS_IssueDate]
    ON [dbo].[F_WAYBILLS]([IssueDate] DESC)
    INCLUDE([IsDeleted], [ForwarderID], [Amount], [CurrencyID]);
GO
CREATE TABLE [dbo].[FIFO_POLICY] (
    [ID]                         INT           IDENTITY (1, 1) NOT NULL,
    [WarehouseID]                INT           NOT NULL,
    [DocumentType]               NVARCHAR (30) NOT NULL,
    [IsStrictFifo]               BIT           NOT NULL,
    [AllowOverride]              BIT           NOT NULL,
    [RequiresApprovalOnOverride] BIT           NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_FP_Warehouse_DocType]
    ON [dbo].[FIFO_POLICY]([WarehouseID] ASC, [DocumentType] ASC);
GO
CREATE TABLE [dbo].[FIFO_VIOLATIONS] (
    [ID]                 INT            IDENTITY (1, 1) NOT NULL,
    [ExpenseProductID]   INT            NOT NULL,
    [ProductID]          INT            NOT NULL,
    [WarehouseID]        INT            NOT NULL,
    [ExpectedLotID]      INT            NULL,
    [SelectedLotID]      INT            NULL,
    [ExpectedReceivedAt] DATETIME       NULL,
    [SelectedReceivedAt] DATETIME       NULL,
    [ReasonCode]         NVARCHAR (50)  NULL,
    [CreatedBy]          NVARCHAR (128) NULL,
    [ApprovedBy]         NVARCHAR (128) NULL,
    [CreatedAt]          DATETIME       NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[FIN_ACCOUNTS] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [Code]              NVARCHAR (20)  NOT NULL,
    [Name]              NVARCHAR (100) NOT NULL,
    [OwnerName]         NVARCHAR (100) NULL,
    [CurrencyID]        INT            NOT NULL,
    [IsCash]            BIT            NOT NULL,
    [IsBank]            BIT            NOT NULL,
    [IsActive]          BIT            NOT NULL,
    [CreatedByUserID]   INT            NOT NULL,
    [CreatedDT]         DATETIME       NOT NULL,
    [BalanceCategoryID] INT            NULL,
    [IsDeleted]         BIT            NULL,
    CONSTRAINT [PK_FIN_ACCOUNTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_FIN_ACCOUNTS_Code]
    ON [dbo].[FIN_ACCOUNTS]([Code] ASC);
GO
CREATE TABLE [dbo].[FIN_BALANCE_CATEGORIES] (
    [ID]              INT            IDENTITY (1, 1) NOT NULL,
    [Name]            NVARCHAR (100) NOT NULL,
    [Type]            TINYINT        NOT NULL,
    [ParentID]        INT            NULL,
    [SortOrder]       INT            NOT NULL,
    [IsActive]        BIT            NOT NULL,
    [CalculationType] TINYINT        NOT NULL,
    [IsDeleted]       BIT            NULL,
    CONSTRAINT [PK_FIN_BALANCE_CATEGORIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES] (
    [BalanceCategoryID] INT NOT NULL,
    [CategoryID]        INT NOT NULL,
    CONSTRAINT [PK_FIN_BALANCE_CATEGORY_CATEGORIES] PRIMARY KEY CLUSTERED ([BalanceCategoryID] ASC, [CategoryID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_BALANCE_CATEGORY_CATEGORIES_CategoryID]
    ON [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES]([CategoryID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_BALANCE_CATEGORY_CATEGORIES_BalanceCategoryID]
    ON [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES]([BalanceCategoryID] ASC);
GO
CREATE TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [SnapshotDate]    DATE            NOT NULL,
    [CategoryID]      INT             NOT NULL,
    [Amount]          DECIMAL (18, 2) NOT NULL,
    [CurrencyID]      INT             NOT NULL,
    [SourceType]      NVARCHAR (30)   NULL,
    [SourceID]        INT             NULL,
    [CreatedByUserID] INT             NOT NULL,
    [CreatedDT]       DATETIME        NOT NULL,
    CONSTRAINT [PK_FIN_BALANCE_SNAPSHOTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_BALANCE_SNAPSHOTS_DateCategory]
    ON [dbo].[FIN_BALANCE_SNAPSHOTS]([SnapshotDate] ASC, [CategoryID] ASC);
GO
CREATE TABLE [dbo].[FIN_CATEGORIES] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [Type]      TINYINT        NOT NULL,
    [ParentID]  INT            NULL,
    [SortOrder] INT            NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_FIN_CATEGORIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[FIN_OPENING_CONTRACT_BALANCES] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [SnapshotDate]    DATE            NOT NULL,
    [BalanceType]     TINYINT         NOT NULL,
    [CounterpartyID]  INT             NOT NULL,
    [ContractID]      INT             NULL,
    [ContractNo]      NVARCHAR (100)  NULL,
    [TotalAmount]     DECIMAL (19, 2) NOT NULL,
    [PaidAmount]      DECIMAL (19, 2) NOT NULL,
    [Amount]          DECIMAL (19, 2) NOT NULL,
    [CurrencyID]      INT             NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_OPENING_CONTRACT_BALANCES_SnapshotDate]
    ON [dbo].[FIN_OPENING_CONTRACT_BALANCES]([SnapshotDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_OPENING_CONTRACT_BALANCES_TypeCounterparty]
    ON [dbo].[FIN_OPENING_CONTRACT_BALANCES]([BalanceType] ASC, [CounterpartyID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_OPENING_CONTRACT_BALANCES_Type_Date]
    ON [dbo].[FIN_OPENING_CONTRACT_BALANCES]([BalanceType] ASC, [SnapshotDate] ASC)
    INCLUDE([CounterpartyID], [ContractNo], [TotalAmount], [PaidAmount], [Amount]);
GO
CREATE TABLE [dbo].[FIN_PL_ITEM_CATEGORIES] (
    [ItemID]     INT NOT NULL,
    [CategoryID] INT NOT NULL,
    CONSTRAINT [PK_FIN_PL_ITEM_CATEGORIES] PRIMARY KEY CLUSTERED ([ItemID] ASC, [CategoryID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_PL_ITEM_CATEGORIES]
    ON [dbo].[FIN_PL_ITEM_CATEGORIES]([CategoryID] ASC, [ItemID] ASC);
GO
CREATE TABLE [dbo].[FIN_PL_ITEMS] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (20)  NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [Sign]      TINYINT        NOT NULL,
    [ParentID]  INT            NULL,
    [SortOrder] INT            NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_FIN_PL_ITEMS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_FIN_PL_ITEMS_Code]
    ON [dbo].[FIN_PL_ITEMS]([Code] ASC);
GO
CREATE TABLE [dbo].[FIN_PL_PLANS] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [Year]            INT             NOT NULL,
    [Month]           TINYINT         NOT NULL,
    [ItemID]          INT             NOT NULL,
    [AmountPlan]      DECIMAL (18, 2) NOT NULL,
    [CreatedByUserID] INT             NOT NULL,
    [CreatedDT]       DATETIME        NOT NULL,
    [IsDeleted]       BIT             NULL,
    CONSTRAINT [PK_FIN_PL_PLANS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_FIN_PL_PLANS_YearMonthItem]
    ON [dbo].[FIN_PL_PLANS]([Year] ASC, [Month] ASC, [ItemID] ASC);
GO
CREATE TABLE [dbo].[FIN_TRANSACTIONS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [TxnDate]          DATE            NOT NULL,
    [AccountID]        INT             NOT NULL,
    [CategoryID]       INT             NOT NULL,
    [Direction]        TINYINT         NOT NULL,
    [Amount]           DECIMAL (18, 2) NOT NULL,
    [CurrencyID]       INT             NOT NULL,
    [Qty]              DECIMAL (18, 3) NULL,
    [PricePerUnit]     DECIMAL (18, 2) NULL,
    [CounterpartyType] TINYINT         NULL,
    [CounterpartyID]   INT             NULL,
    [CounterpartyName] NVARCHAR (200)  NULL,
    [Purpose]          NVARCHAR (200)  NULL,
    [SourceDocType]    NVARCHAR (50)   NULL,
    [SourceDocID]      INT             NULL,
    [ContractNo]       NVARCHAR (50)   NULL,
    [Comment]          NVARCHAR (500)  NULL,
    [CreatedByUserID]  INT             NOT NULL,
    [CreatedDT]        DATETIME        NOT NULL,
    [IsDeleted]        BIT             NULL,
    CONSTRAINT [PK_FIN_TRANSACTIONS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_Category]
    ON [dbo].[FIN_TRANSACTIONS]([CategoryID] ASC, [TxnDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_Date]
    ON [dbo].[FIN_TRANSACTIONS]([TxnDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_SourceDoc]
    ON [dbo].[FIN_TRANSACTIONS]([SourceDocType] ASC, [SourceDocID] ASC)
    INCLUDE([IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_PL]
    ON [dbo].[FIN_TRANSACTIONS]([TxnDate] ASC, [CategoryID] ASC, [Direction] ASC)
    INCLUDE([Amount]);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_Counterparty]
    ON [dbo].[FIN_TRANSACTIONS]([CounterpartyType] ASC, [CounterpartyID] ASC, [TxnDate] ASC)
    INCLUDE([Direction], [Amount], [IsDeleted], [SourceDocType]);
GO
CREATE NONCLUSTERED INDEX [IX_FIN_TRANSACTIONS_Account]
    ON [dbo].[FIN_TRANSACTIONS]([AccountID] ASC, [TxnDate] ASC);
GO
CREATE TABLE [dbo].[FORWARDERS] (
    [ID]            INT          IDENTITY (1, 1) NOT NULL,
    [ForwarderName] VARCHAR (50) NOT NULL,
    [Director]      VARCHAR (50) NULL,
    [ContactPerson] VARCHAR (50) NULL,
    [Description]   TEXT         NULL,
    [IsDeleted]     BIT          NULL,
    CONSTRAINT [PK_FORWARDERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES] (
    [EmployeeID]                    INT            NOT NULL,
    [MaterialResponsibilityPercent] DECIMAL (5, 2) NOT NULL,
    [SpecialAllowancePercent]       DECIMAL (5, 2) NOT NULL,
    [CreatedDT]                     DATETIME       NOT NULL,
    [UpdatedDT]                     DATETIME       NULL,
    PRIMARY KEY CLUSTERED ([EmployeeID] ASC)
);
GO
CREATE TABLE [dbo].[HR_EMPLOYEE_HISTORY] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [EmployeeID]        INT            NOT NULL,
    [ActionType]        NVARCHAR (50)  NOT NULL,
    [FromOrgUnitID]     INT            NULL,
    [FromPositionID]    INT            NULL,
    [ToOrgUnitID]       INT            NULL,
    [ToPositionID]      INT            NULL,
    [Reason]            NVARCHAR (MAX) NULL,
    [CreatedByUserID]   INT            NULL,
    [CreatedDT]         DATETIME       NOT NULL,
    [EffectiveDateFrom] DATE           NOT NULL,
    [EffectiveDateTo]   DATE           NULL,
    CONSTRAINT [PK_HR_EMPLOYEE_HISTORY] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_EMPLOYEE_HISTORY_EmployeeID]
    ON [dbo].[HR_EMPLOYEE_HISTORY]([EmployeeID] ASC);
GO
CREATE TABLE [dbo].[HR_EMPLOYEES] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [TabNo]           NVARCHAR (50)   NOT NULL,
    [FullName]        NVARCHAR (200)  NOT NULL,
    [OrgUnitID]       INT             NULL,
    [PositionID]      INT             NULL,
    [BaseSalary]      DECIMAL (18, 2) NULL,
    [SalaryGradeID]   INT             NULL,
    [EmploymentDate]  DATE            NULL,
    [DismissalDate]   DATE            NULL,
    [IsActive]        BIT             NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME2 (7)   NOT NULL,
    [UpdatedDT]       DATETIME2 (7)   NULL,
    [IsDeleted]       BIT             NULL,
    CONSTRAINT [PK_HR_EMPLOYEES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_HR_EMPLOYEES_TabNo]
    ON [dbo].[HR_EMPLOYEES]([TabNo] ASC) WHERE ([IsActive]=(1));
GO
CREATE NONCLUSTERED INDEX [IX_HR_EMPLOYEES_IsActive]
    ON [dbo].[HR_EMPLOYEES]([IsActive] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_HR_EMPLOYEES_PositionID]
    ON [dbo].[HR_EMPLOYEES]([PositionID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_HR_EMPLOYEES_OrgUnitID]
    ON [dbo].[HR_EMPLOYEES]([OrgUnitID] ASC);
GO
CREATE TABLE [dbo].[HR_JOB_DESCRIPTIONS] (
    [DocumentID]  BIGINT         NOT NULL,
    [PositionID]  INT            NOT NULL,
    [OrgUnitID]   INT            NULL,
    [IsMandatory] BIT            NOT NULL,
    [Notes]       NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_HR_JOB_DESCRIPTIONS] PRIMARY KEY CLUSTERED ([DocumentID] ASC)
);
GO
CREATE TABLE [dbo].[HR_ORG_UNITS] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [ParentID]  INT            NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (200) NOT NULL,
    [LevelNo]   INT            NOT NULL,
    [SortOrder] INT            NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [CreatedDT] DATETIME2 (0)  NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_HR_ORG_UNITS] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[HR_PAYROLL_HEADERS] (
    [ID]               INT           IDENTITY (1, 1) NOT NULL,
    [Year]             INT           NOT NULL,
    [Month]            INT           NOT NULL,
    [OrgUnitID]        INT           NULL,
    [Status]           NVARCHAR (20) NOT NULL,
    [CreatedByUserID]  INT           NULL,
    [CreatedDT]        DATETIME2 (7) NOT NULL,
    [ApprovedByUserID] INT           NULL,
    [ApprovedDT]       DATETIME2 (7) NULL,
    [TimesheetID]      INT           NULL,
    [PayrollType]      NVARCHAR (20) NULL,
    [IsDeleted]        BIT           NULL,
    CONSTRAINT [PK_HR_PAYROLL_HEADERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_PAYROLL_HEADERS_TimesheetID]
    ON [dbo].[HR_PAYROLL_HEADERS]([TimesheetID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_HR_PAYROLL_HEADERS_Status]
    ON [dbo].[HR_PAYROLL_HEADERS]([Status] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_HR_PAYROLL_HEADERS_YearMonthOrgUnit]
    ON [dbo].[HR_PAYROLL_HEADERS]([Year] ASC, [Month] ASC, [OrgUnitID] ASC) WHERE ([OrgUnitID] IS NOT NULL);
GO
CREATE TABLE [dbo].[HR_PAYROLL_LINES] (
    [ID]          INT             IDENTITY (1, 1) NOT NULL,
    [PayrollID]   INT             NOT NULL,
    [EmployeeID]  INT             NOT NULL,
    [BaseSalary]  DECIMAL (18, 2) NULL,
    [HoursWorked] DECIMAL (8, 2)  NULL,
    [PieceQty]    DECIMAL (10, 2) NULL,
    [HourlyRate]  DECIMAL (18, 2) NULL,
    [GrossSalary] DECIMAL (18, 2) NULL,
    [Bonuses]     DECIMAL (18, 2) NULL,
    [Penalties]   DECIMAL (18, 2) NULL,
    [NetSalary]   DECIMAL (18, 2) NULL,
    [CurrencyID]  INT             NULL,
    [Notes]       NVARCHAR (MAX)  NULL,
    [IsDeleted]   BIT             NULL,
    CONSTRAINT [PK_HR_PAYROLL_LINES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_PAYROLL_LINES_EmployeeID]
    ON [dbo].[HR_PAYROLL_LINES]([EmployeeID] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_HR_PAYROLL_LINES_PayrollEmployee]
    ON [dbo].[HR_PAYROLL_LINES]([PayrollID] ASC, [EmployeeID] ASC);
GO
CREATE TABLE [dbo].[HR_PIECE_RATE_OPERATIONS] (
    [ID]               INT            IDENTITY (1, 1) NOT NULL,
    [Code]             NVARCHAR (50)  NOT NULL,
    [Name]             NVARCHAR (200) NOT NULL,
    [Description]      NVARCHAR (500) NULL,
    [IsActive]         BIT            NOT NULL,
    [SortOrder]        INT            NOT NULL,
    [CreatedByUserID]  INT            NULL,
    [CreatedDT]        DATETIME       NOT NULL,
    [ModifiedByUserID] INT            NULL,
    [ModifiedDT]       DATETIME       NULL,
    CONSTRAINT [PK_HR_PIECE_RATE_OPERATIONS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[HR_PIECE_RATE_TARIFFS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [ProductID]        INT             NOT NULL,
    [PositionID]       INT             NULL,
    [RatePerUnit]      DECIMAL (18, 4) NOT NULL,
    [EffectiveFrom]    DATE            NOT NULL,
    [EffectiveTo]      DATE            NULL,
    [IsActive]         BIT             NOT NULL,
    [CreatedByUserID]  INT             NULL,
    [CreatedDT]        DATETIME        NOT NULL,
    [ModifiedByUserID] INT             NULL,
    [ModifiedDT]       DATETIME        NULL,
    [OperationID]      INT             NULL,
    [CurrencyID]       INT             NULL,
    [IsDeleted]        BIT             NULL,
    CONSTRAINT [PK_HR_PIECE_RATE_TARIFFS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_PRT_Product_Active]
    ON [dbo].[HR_PIECE_RATE_TARIFFS]([ProductID] ASC, [IsActive] ASC)
    INCLUDE([EffectiveFrom], [EffectiveTo], [RatePerUnit]);
GO
CREATE TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] (
    [ID]               INT           IDENTITY (1, 1) NOT NULL,
    [Year]             INT           NOT NULL,
    [Month]            INT           NOT NULL,
    [OrgUnitID]        INT           NULL,
    [Status]           NVARCHAR (50) NOT NULL,
    [ApprovedByUserID] INT           NULL,
    [ApprovedDT]       DATETIME      NULL,
    [CreatedByUserID]  INT           NULL,
    [CreatedDT]        DATETIME      NOT NULL,
    [ModifiedByUserID] INT           NULL,
    [ModifiedDT]       DATETIME      NULL,
    [IsDeleted]        BIT           NULL,
    CONSTRAINT [PK_HR_PIECE_RATE_TIMESHEET_APPROVALS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UQ_HR_PIECE_RATE_TIMESHEET_APPROVALS] UNIQUE NONCLUSTERED ([Year] ASC, [Month] ASC, [OrgUnitID] ASC)
);
GO
CREATE TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [EmployeeID]       INT             NOT NULL,
    [ProductID]        INT             NOT NULL,
    [OperationID]      INT             NOT NULL,
    [WorkDate]         DATE            NOT NULL,
    [Quantity]         DECIMAL (18, 4) NOT NULL,
    [Notes]            NVARCHAR (500)  NULL,
    [CreatedByUserID]  INT             NULL,
    [CreatedDT]        DATETIME        NOT NULL,
    [ModifiedByUserID] INT             NULL,
    [ModifiedDT]       DATETIME        NULL,
    [IsDeleted]        BIT             NULL,
    CONSTRAINT [PK_HR_PIECE_RATE_WORK_ENTRIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[HR_POSITIONS] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [Code]      NVARCHAR (50)  NOT NULL,
    [Name]      NVARCHAR (200) NOT NULL,
    [IsManager] BIT            NOT NULL,
    [IsActive]  BIT            NOT NULL,
    [CreatedDT] DATETIME2 (0)  NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_HR_POSITIONS] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE TABLE [dbo].[HR_SALARY_GRADES] (
    [ID]            INT             IDENTITY (1, 1) NOT NULL,
    [GradeCode]     NVARCHAR (50)   NOT NULL,
    [Name]          NVARCHAR (200)  NOT NULL,
    [MinSalary]     DECIMAL (18, 2) NOT NULL,
    [MaxSalary]     DECIMAL (18, 2) NOT NULL,
    [CurrencyCode]  CHAR (3)        NOT NULL,
    [EffectiveFrom] DATE            NOT NULL,
    [EffectiveTo]   DATE            NULL,
    [IsDeleted]     BIT             NULL,
    CONSTRAINT [PK_HR_SALARY_GRADES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_HR_SALARY_GRADES] UNIQUE NONCLUSTERED ([GradeCode] ASC, [EffectiveFrom] ASC)
);
GO
CREATE TABLE [dbo].[HR_STAFFING_PLAN] (
    [ID]               INT            IDENTITY (1, 1) NOT NULL,
    [OrgUnitID]        INT            NOT NULL,
    [PositionID]       INT            NOT NULL,
    [SalaryGradeID]    INT            NULL,
    [PlannedHeadcount] DECIMAL (9, 2) NOT NULL,
    [IsPieceRate]      BIT            NOT NULL,
    [IsHourly]         BIT            NOT NULL,
    [IsFixedSalary]    BIT            NOT NULL,
    [EffectiveFrom]    DATE           NOT NULL,
    [EffectiveTo]      DATE           NULL,
    [IsActive]         BIT            NOT NULL,
    [IsDeleted]        BIT            NULL,
    CONSTRAINT [PK_HR_STAFFING_PLAN] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_STAFFING_PLAN_OrgUnit]
    ON [dbo].[HR_STAFFING_PLAN]([OrgUnitID] ASC, [PositionID] ASC, [EffectiveFrom] ASC);
GO
CREATE TABLE [dbo].[HR_TIMESHEET_HEADERS] (
    [ID]               INT           IDENTITY (1, 1) NOT NULL,
    [Year]             INT           NOT NULL,
    [Month]            INT           NOT NULL,
    [OrgUnitID]        INT           NULL,
    [Status]           NVARCHAR (20) NOT NULL,
    [CreatedByUserID]  INT           NULL,
    [CreatedDT]        DATETIME2 (7) NOT NULL,
    [ApprovedByUserID] INT           NULL,
    [ApprovedDT]       DATETIME2 (7) NULL,
    [IsDeleted]        BIT           NULL,
    CONSTRAINT [PK_HR_TIMESHEET_HEADERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_HR_TIMESHEET_HEADERS_Status]
    ON [dbo].[HR_TIMESHEET_HEADERS]([Status] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_HR_TIMESHEET_HEADERS_YearMonthOrgUnit]
    ON [dbo].[HR_TIMESHEET_HEADERS]([Year] ASC, [Month] ASC, [OrgUnitID] ASC) WHERE ([OrgUnitID] IS NOT NULL);
GO
CREATE TABLE [dbo].[HR_TIMESHEET_LINES] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [TimesheetID]      INT             NOT NULL,
    [EmployeeID]       INT             NOT NULL,
    [Day01Hours]       DECIMAL (5, 2)  NULL,
    [Day02Hours]       DECIMAL (5, 2)  NULL,
    [Day03Hours]       DECIMAL (5, 2)  NULL,
    [Day04Hours]       DECIMAL (5, 2)  NULL,
    [Day05Hours]       DECIMAL (5, 2)  NULL,
    [Day06Hours]       DECIMAL (5, 2)  NULL,
    [Day07Hours]       DECIMAL (5, 2)  NULL,
    [Day08Hours]       DECIMAL (5, 2)  NULL,
    [Day09Hours]       DECIMAL (5, 2)  NULL,
    [Day10Hours]       DECIMAL (5, 2)  NULL,
    [Day11Hours]       DECIMAL (5, 2)  NULL,
    [Day12Hours]       DECIMAL (5, 2)  NULL,
    [Day13Hours]       DECIMAL (5, 2)  NULL,
    [Day14Hours]       DECIMAL (5, 2)  NULL,
    [Day15Hours]       DECIMAL (5, 2)  NULL,
    [Day16Hours]       DECIMAL (5, 2)  NULL,
    [Day17Hours]       DECIMAL (5, 2)  NULL,
    [Day18Hours]       DECIMAL (5, 2)  NULL,
    [Day19Hours]       DECIMAL (5, 2)  NULL,
    [Day20Hours]       DECIMAL (5, 2)  NULL,
    [Day21Hours]       DECIMAL (5, 2)  NULL,
    [Day22Hours]       DECIMAL (5, 2)  NULL,
    [Day23Hours]       DECIMAL (5, 2)  NULL,
    [Day24Hours]       DECIMAL (5, 2)  NULL,
    [Day25Hours]       DECIMAL (5, 2)  NULL,
    [Day26Hours]       DECIMAL (5, 2)  NULL,
    [Day27Hours]       DECIMAL (5, 2)  NULL,
    [Day28Hours]       DECIMAL (5, 2)  NULL,
    [Day29Hours]       DECIMAL (5, 2)  NULL,
    [Day30Hours]       DECIMAL (5, 2)  NULL,
    [Day31Hours]       DECIMAL (5, 2)  NULL,
    [TotalWorkedDays]  DECIMAL (5, 2)  NULL,
    [TotalWorkedHours] DECIMAL (8, 2)  NULL,
    [OvertimeHours]    DECIMAL (8, 2)  NULL,
    [AbsentDays]       DECIMAL (5, 2)  NULL,
    [Notes]            NVARCHAR (MAX)  NULL,
    [ProductID]        INT             NULL,
    [Day01Quantity]    DECIMAL (18, 4) NULL,
    [Day02Quantity]    DECIMAL (18, 4) NULL,
    [Day03Quantity]    DECIMAL (18, 4) NULL,
    [Day04Quantity]    DECIMAL (18, 4) NULL,
    [Day05Quantity]    DECIMAL (18, 4) NULL,
    [Day06Quantity]    DECIMAL (18, 4) NULL,
    [Day07Quantity]    DECIMAL (18, 4) NULL,
    [Day08Quantity]    DECIMAL (18, 4) NULL,
    [Day09Quantity]    DECIMAL (18, 4) NULL,
    [Day10Quantity]    DECIMAL (18, 4) NULL,
    [Day11Quantity]    DECIMAL (18, 4) NULL,
    [Day12Quantity]    DECIMAL (18, 4) NULL,
    [Day13Quantity]    DECIMAL (18, 4) NULL,
    [Day14Quantity]    DECIMAL (18, 4) NULL,
    [Day15Quantity]    DECIMAL (18, 4) NULL,
    [Day16Quantity]    DECIMAL (18, 4) NULL,
    [Day17Quantity]    DECIMAL (18, 4) NULL,
    [Day18Quantity]    DECIMAL (18, 4) NULL,
    [Day19Quantity]    DECIMAL (18, 4) NULL,
    [Day20Quantity]    DECIMAL (18, 4) NULL,
    [Day21Quantity]    DECIMAL (18, 4) NULL,
    [Day22Quantity]    DECIMAL (18, 4) NULL,
    [Day23Quantity]    DECIMAL (18, 4) NULL,
    [Day24Quantity]    DECIMAL (18, 4) NULL,
    [Day25Quantity]    DECIMAL (18, 4) NULL,
    [Day26Quantity]    DECIMAL (18, 4) NULL,
    [Day27Quantity]    DECIMAL (18, 4) NULL,
    [Day28Quantity]    DECIMAL (18, 4) NULL,
    [Day29Quantity]    DECIMAL (18, 4) NULL,
    [Day30Quantity]    DECIMAL (18, 4) NULL,
    [Day31Quantity]    DECIMAL (18, 4) NULL,
    [TotalQuantity]    DECIMAL (18, 4) NULL,
    [IsDeleted]        BIT             NULL,
    CONSTRAINT [PK_HR_TIMESHEET_LINES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_HR_TIMESHEET_LINES_TimesheetEmployee]
    ON [dbo].[HR_TIMESHEET_LINES]([TimesheetID] ASC, [EmployeeID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_HR_TIMESHEET_LINES_EmployeeID]
    ON [dbo].[HR_TIMESHEET_LINES]([EmployeeID] ASC);
GO
CREATE TABLE [dbo].[HR_TIMESHEET_SETTINGS] (
    [TimesheetID] INT             NOT NULL,
    [FundHours]   DECIMAL (10, 2) NOT NULL,
    [CreatedDT]   DATETIME        NOT NULL,
    [UpdatedDT]   DATETIME        NULL,
    PRIMARY KEY CLUSTERED ([TimesheetID] ASC)
);
GO
CREATE TABLE [dbo].[HSCODES] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [HSCODE]      NVARCHAR (MAX) NOT NULL,
    [Description] NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_HSCODES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[INCOME_LOTS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [IncomeID]         INT             NOT NULL,
    [IncomeProductID]  INT             NOT NULL,
    [ProductID]        INT             NOT NULL,
    [WarehouseID]      INT             NOT NULL,
    [LotNo]            NVARCHAR (60)   NOT NULL,
    [ReceivedDateTime] DATETIME        NOT NULL,
    [QtyReceived]      DECIMAL (18, 4) NOT NULL,
    [QtyConsumed]      DECIMAL (18, 4) NOT NULL,
    [QtyAvailable]     AS              ([QtyReceived] - [QtyConsumed]) PERSISTED,
    [ExpiryDate]       DATETIME        NULL,
    [IsDeleted]        BIT             NOT NULL,
    [CreatedAt]        DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_IL_IncomeID]
    ON [dbo].[INCOME_LOTS]([IncomeID] ASC)
    INCLUDE([ProductID], [QtyReceived], [QtyConsumed], [IsDeleted]);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_IL_IncomeProduct_LotNo]
    ON [dbo].[INCOME_LOTS]([IncomeProductID] ASC, [LotNo] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_IL_Product_Wh_Received]
    ON [dbo].[INCOME_LOTS]([ProductID] ASC, [WarehouseID] ASC, [ReceivedDateTime] ASC, [ID] ASC);
GO
CREATE TABLE [dbo].[INCOME_PRODUCTS] (
    [ID]         INT        IDENTITY (1, 1) NOT NULL,
    [IncomeID]   INT        NOT NULL,
    [ProductID]  INT        NOT NULL,
    [Amount]     FLOAT (53) NOT NULL,
    [PiecePrice] FLOAT (53) NULL,
    [TotalPrice] AS         ([Amount] * [PiecePrice]),
    [Comment]    TEXT       NULL,
    [UnitID]     INT        NOT NULL,
    [VATRate]    FLOAT (53) NULL,
    CONSTRAINT [PK_PROD_WRHS_INCOME_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[INCOMES] (
    [ID]            INT          IDENTITY (1, 1) NOT NULL,
    [DocNo]         VARCHAR (50) NOT NULL,
    [ReceivingWHID] INT          NOT NULL,
    [Amount]        FLOAT (53)   NULL,
    [TotalPrice]    FLOAT (53)   NULL,
    [IsDeleted]     BIT          NULL,
    [Description]   TEXT         NULL,
    [IssueDateTime] DATETIME     NOT NULL,
    [SenderWHID]    INT          NULL,
    [RecieveStatus] BIT          NULL,
    [CurrencyID]    INT          NOT NULL,
    [InvoiceID]     INT          NULL,
    [ProductionID]  INT          NULL,
    CONSTRAINT [PK_PROD_WRHS_INCOMES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_INCOMES_ProductionID]
    ON [dbo].[INCOMES]([ProductionID] ASC)
    INCLUDE([IsDeleted]) WHERE ([ProductionID] IS NOT NULL);
GO
CREATE NONCLUSTERED INDEX [IX_INCOMES_DocNo]
    ON [dbo].[INCOMES]([DocNo] ASC)
    INCLUDE([IsDeleted], [ReceivingWHID]);
GO
CREATE TABLE [dbo].[INVENTORY_ADJUSTMENTS] (
    [ID]          INT      IDENTITY (1, 1) NOT NULL,
    [WarehouseID] INT      NOT NULL,
    [Period]      DATE     NOT NULL,
    [UserID]      INT      NULL,
    [IncomeID]    INT      NULL,
    [ExpenseID]   INT      NULL,
    [CreatedAt]   DATETIME NOT NULL,
    CONSTRAINT [PK_INVENTORY_ADJUSTMENTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_INVENTORY_ADJUSTMENTS_WH_Period]
    ON [dbo].[INVENTORY_ADJUSTMENTS]([WarehouseID] ASC, [Period] ASC);
GO
CREATE TABLE [dbo].[JOB_DESC] (
    [DocumentID]         BIGINT         NOT NULL,
    [JobDescriptionCode] NVARCHAR (100) NULL,
    [JobTitle]           NVARCHAR (200) NULL,
    [Department]         NVARCHAR (200) NULL,
    [VersionNo]          DECIMAL (5, 2) NOT NULL,
    [Notes]              NVARCHAR (MAX) NULL,
    [HRJobDescriptionID] BIGINT         NULL,
    [IsDeleted]          BIT            NULL,
    CONSTRAINT [PK_JOB_DESC] PRIMARY KEY CLUSTERED ([DocumentID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_JOB_DESC_JobDescriptionCode]
    ON [dbo].[JOB_DESC]([JobDescriptionCode] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_JOB_DESC_JobTitle]
    ON [dbo].[JOB_DESC]([JobTitle] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_JOB_DESC_Department]
    ON [dbo].[JOB_DESC]([Department] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_JOB_DESC_HRJobDescriptionID]
    ON [dbo].[JOB_DESC]([HRJobDescriptionID] ASC);
GO
CREATE TABLE [dbo].[LICENSE_STATE] (
    [Name]      NVARCHAR (50)  NOT NULL,
    [Value]     NVARCHAR (400) NOT NULL,
    [UpdatedAt] DATETIME       NOT NULL,
    CONSTRAINT [PK_LICENSE_STATE] PRIMARY KEY CLUSTERED ([Name] ASC)
);
GO
CREATE TABLE [dbo].[LOT_LOCATIONS] (
    [ID]             INT             IDENTITY (1, 1) NOT NULL,
    [LotID]          INT             NOT NULL,
    [LocationID]     INT             NOT NULL,
    [ProductID]      INT             NOT NULL,
    [QtyAvailable]   DECIMAL (18, 4) NOT NULL,
    [LastMovementAt] DATETIME        NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_LL_Product_Location_Qty]
    ON [dbo].[LOT_LOCATIONS]([ProductID] ASC, [LocationID] ASC, [QtyAvailable] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_LL_Lot_Location]
    ON [dbo].[LOT_LOCATIONS]([LotID] ASC, [LocationID] ASC);
GO
CREATE TABLE [dbo].[NORM_INPUT_LINES] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [ProductID]        INT             NOT NULL,
    [MaterialID]       INT             NOT NULL,
    [CutLengthMm]      INT             NOT NULL,
    [PiecesPerProduct] INT             NOT NULL,
    [Month]            CHAR (7)        NULL,
    [CreatedByUserID]  INT             NULL,
    [CreatedDT]        DATETIME        NOT NULL,
    [Price]            DECIMAL (18, 4) NOT NULL,
    [TotalAmount]      AS              (((CONVERT (DECIMAL (18, 4), isnull([CutLengthMm], (0))) / (1000.0)) * CONVERT (DECIMAL (18, 4), isnull([PiecesPerProduct], (0)))) * isnull([Price], (0))) PERSISTED,
    [Consumption]      FLOAT (53)      NULL,
    [WasteAmount]      FLOAT (53)      NULL,
    [UnitID]           INT             NULL,
    [Comment]          TEXT            NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[NORMLINES] (
    [ID]           INT             IDENTITY (1, 1) NOT NULL,
    [NormID]       INT             NULL,
    [MaterialID]   INT             NULL,
    [QtyPerUnit]   DECIMAL (18, 4) NULL,
    [WastePercent] DECIMAL (5, 2)  NULL,
    [UnitID]       INT             NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_NORMLINES_NormID]
    ON [dbo].[NORMLINES]([NormID] ASC)
    INCLUDE([MaterialID], [QtyPerUnit]);
GO
CREATE TABLE [dbo].[NORMVERSIONS] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [ProductID]     INT            NULL,
    [EffectiveFrom] DATE           NULL,
    [EffectiveTo]   DATE           NULL,
    [Comment]       NVARCHAR (255) NULL,
    [IsDeleted]     BIT            NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_NORMVERSIONS_Product_From]
    ON [dbo].[NORMVERSIONS]([ProductID] ASC, [EffectiveFrom] ASC)
    INCLUDE([EffectiveTo], [IsDeleted]);
GO
CREATE TABLE [dbo].[P_CONTRACT_DOCS] (
    [ID]         INT             IDENTITY (1, 1) NOT NULL,
    [ContractID] INT             NOT NULL,
    [Doc]        VARBINARY (MAX) NULL,
    CONSTRAINT [PK_P_CONTRACT_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_CONTRACT_PRODUCTS] (
    [ID]          INT        IDENTITY (1, 1) NOT NULL,
    [ContractID]  INT        NOT NULL,
    [ProductID]   INT        NOT NULL,
    [Price]       FLOAT (53) NOT NULL,
    [Quantity]    FLOAT (53) NOT NULL,
    [Amount]      AS         ([Price] * [Quantity]),
    [MOQ]         FLOAT (53) NULL,
    [ActivePart]  BIT        NULL,
    [UnitID]      INT        NULL,
    [HSCodeID]    INT        NULL,
    [VATAmount]   FLOAT (53) NULL,
    [TotalAmount] AS         ([price] * [quantity] + [vatamount]),
    CONSTRAINT [PK_P_CONTRACT_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_CONTRACTS] (
    [ID]           INT          IDENTITY (1, 1) NOT NULL,
    [ContractNo]   VARCHAR (50) NOT NULL,
    [IssuedDate]   DATETIME     NOT NULL,
    [CompanyID]    INT          NOT NULL,
    [SupplierID]   INT          NOT NULL,
    [Amount]       FLOAT (53)   NULL,
    [Incoterms]    VARCHAR (15) NULL,
    [PaymentTerms] VARCHAR (50) NULL,
    [DueDate]      DATETIME     NOT NULL,
    [IsDeleted]    BIT          NULL,
    [IDN]          VARCHAR (25) NULL,
    [CurrencyID]   INT          NULL,
    [PurchaseType] TINYINT      NOT NULL,
    CONSTRAINT [PK_PCONTRACTS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_P_CONTRACTS] UNIQUE NONCLUSTERED ([ContractNo] ASC)
);
GO
CREATE TABLE [dbo].[P_INVOICE_DOCS] (
    [ID]        INT             IDENTITY (1, 1) NOT NULL,
    [InvoiceID] INT             NOT NULL,
    [Doc]       VARBINARY (MAX) NULL,
    CONSTRAINT [PK_P_INVOICE_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_INVOICE_PRODUCTS] (
    [ID]         INT        IDENTITY (1, 1) NOT NULL,
    [InvoiceID]  INT        NOT NULL,
    [ProductID]  INT        NOT NULL,
    [Quantity]   FLOAT (53) NOT NULL,
    [Price]      FLOAT (53) NOT NULL,
    [TotalPrice] AS         ([quantity] * [price]),
    [UnitID]     INT        NOT NULL,
    CONSTRAINT [PK_P_INVOICE_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_INVOICES] (
    [ID]           INT          IDENTITY (1, 1) NOT NULL,
    [InvoiceNo]    VARCHAR (50) NOT NULL,
    [OrderID]      INT          NULL,
    [SupplierID]   INT          NOT NULL,
    [Amount]       FLOAT (53)   NULL,
    [InvoiceDate]  DATE         NOT NULL,
    [CompanyID]    INT          NULL,
    [IsDeleted]    BIT          NULL,
    [CurrencyID]   INT          NULL,
    [ContractID]   INT          NOT NULL,
    [PurchaseType] TINYINT      NOT NULL,
    CONSTRAINT [PK_P_INVOICES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_P_INVOICES] UNIQUE NONCLUSTERED ([InvoiceNo] ASC)
);
GO
CREATE TABLE [dbo].[P_ORDER_PRODUCTS] (
    [ID]           INT           IDENTITY (1, 1) NOT NULL,
    [OrderID]      INT           NOT NULL,
    [ProductID]    INT           NOT NULL,
    [Price]        FLOAT (53)    NOT NULL,
    [Amount]       FLOAT (53)    NOT NULL,
    [TotalPrice]   AS            ([Price] * [Amount]),
    [MOQ]          FLOAT (53)    NULL,
    [UnitID]       INT           NULL,
    [Priority]     NVARCHAR (50) NULL,
    [RequiredDate] DATETIME      NULL,
    CONSTRAINT [PK_P_ORDER_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_ORDERS] (
    [ID]              INT            IDENTITY (1, 1) NOT NULL,
    [OrderNo]         VARCHAR (50)   NOT NULL,
    [IssuedDate]      DATETIME       NOT NULL,
    [CompanyID]       INT            NULL,
    [SupplierID]      INT            NULL,
    [ContractID]      INT            NULL,
    [Amount]          FLOAT (53)     NULL,
    [Description]     TEXT           NULL,
    [IsDeleted]       BIT            NULL,
    [CurrencyID]      INT            NULL,
    [PurchaseType]    TINYINT        NOT NULL,
    [RequisitionID]   INT            NULL,
    [Status]          NVARCHAR (50)  NULL,
    [ContractStatus]  NVARCHAR (50)  NULL,
    [InvoiceStatus]   NVARCHAR (50)  NULL,
    [WarehouseStatus] NVARCHAR (50)  NULL,
    [DepartmentID]    INT            NULL,
    [RequesterID]     INT            NULL,
    [ContactPhone]    NVARCHAR (50)  NULL,
    [Purpose]         NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_P_ORDERS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_P_ORDERS] UNIQUE NONCLUSTERED ([ID] ASC),
    CONSTRAINT [IX_P_ORDERS_1] UNIQUE NONCLUSTERED ([OrderNo] ASC)
);
GO
CREATE TABLE [dbo].[P_REQUISITION_APPROVALS] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [RequisitionID] INT            NOT NULL,
    [Role]          NVARCHAR (100) NOT NULL,
    [ApproverID]    INT            NULL,
    [ApproverName]  NVARCHAR (200) NULL,
    [Signature]     NVARCHAR (MAX) NULL,
    [ApprovalDate]  DATETIME       NULL,
    [Status]        NVARCHAR (50)  NULL,
    [Comments]      NVARCHAR (MAX) NULL,
    [SortOrder]     INT            NOT NULL,
    CONSTRAINT [PK_P_REQUISITION_APPROVALS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_REQUISITION_PRODUCTS] (
    [ID]             INT            IDENTITY (1, 1) NOT NULL,
    [RequisitionID]  INT            NOT NULL,
    [ProductID]      INT            NOT NULL,
    [Quantity]       FLOAT (53)     NOT NULL,
    [UnitID]         INT            NOT NULL,
    [EstimatedPrice] FLOAT (53)     NULL,
    [Amount]         FLOAT (53)     NULL,
    [Priority]       NVARCHAR (50)  NULL,
    [RequiredDate]   DATETIME       NULL,
    [Description]    NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_P_REQUISITION_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[P_REQUISITIONS] (
    [ID]                 INT            IDENTITY (1, 1) NOT NULL,
    [RequisitionNo]      NVARCHAR (50)  NOT NULL,
    [RequisitionDate]    DATETIME       NOT NULL,
    [DepartmentID]       INT            NULL,
    [RequesterID]        INT            NOT NULL,
    [ContactPhone]       NVARCHAR (50)  NULL,
    [Purpose]            NVARCHAR (MAX) NULL,
    [Status]             NVARCHAR (50)  NULL,
    [ApprovedByID]       INT            NULL,
    [ApprovedDate]       DATETIME       NULL,
    [RejectedByID]       INT            NULL,
    [RejectionReason]    NVARCHAR (MAX) NULL,
    [ConvertedToOrderID] INT            NULL,
    [IsDeleted]          BIT            NULL,
    [CreatedByUserID]    INT            NULL,
    [CreatedDT]          DATETIME       NOT NULL,
    [ModifiedByUserID]   INT            NULL,
    [ModifiedDT]         DATETIME       NULL,
    CONSTRAINT [PK_P_REQUISITIONS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_P_REQUISITIONS_RequesterID]
    ON [dbo].[P_REQUISITIONS]([RequesterID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_P_REQUISITIONS_Status]
    ON [dbo].[P_REQUISITIONS]([Status] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_P_REQUISITIONS_RequisitionNo]
    ON [dbo].[P_REQUISITIONS]([RequisitionNo] ASC);
GO
CREATE TABLE [dbo].[PERMISSIONMODULES] (
    [ID]         INT          IDENTITY (1, 1) NOT NULL,
    [Module]     VARCHAR (50) NULL,
    [Controller] VARCHAR (50) NULL,
    [Action]     VARCHAR (50) NULL,
    [Parameter]  VARCHAR (50) NULL,
    CONSTRAINT [PK_PERMISSIONMODULES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[PERMISSIONS] (
    [ID]                 INT IDENTITY (1, 1) NOT NULL,
    [ViewPermit]         BIT NOT NULL,
    [ChangePermit]       BIT NOT NULL,
    [RoleID]             INT NOT NULL,
    [PermissionModuleID] INT NOT NULL,
    [DeletePermit]       BIT NOT NULL,
    CONSTRAINT [PK_PERMISSIONS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[PRODUCT_CATEGORIES] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [Code]              NVARCHAR (50)  NULL,
    [Name]              NVARCHAR (200) NOT NULL,
    [IsRawMaterial]     BIT            NOT NULL,
    [IsFinishedGood]    BIT            NOT NULL,
    [IsComponent]       BIT            NOT NULL,
    [IsConsumable]      BIT            NOT NULL,
    [RequiresNORM]      BIT            NOT NULL,
    [RequiresCutting]   BIT            NOT NULL,
    [IncludeInMRP]      BIT            NOT NULL,
    [AllowPurchase]     BIT            NOT NULL,
    [AllowSales]        BIT            NOT NULL,
    [RawMaterialGroup]  NVARCHAR (100) NULL,
    [RawMaterialSubGrp] NVARCHAR (100) NULL,
    [SortOrder]         INT            NOT NULL,
    [IsActive]          BIT            NOT NULL,
    [CreatedDT]         DATETIME2 (0)  NOT NULL,
    [UpdatedDT]         DATETIME2 (0)  NULL,
    [CalculateByWeight] BIT            NULL,
    [IsDeleted]         BIT            NULL,
    CONSTRAINT [PK_PRODUCT_CATEGORIES] PRIMARY KEY CLUSTERED ([ID] ASC),
    UNIQUE NONCLUSTERED ([Code] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCT_CATEGORIES_TypeFlags]
    ON [dbo].[PRODUCT_CATEGORIES]([IsRawMaterial] ASC, [IsFinishedGood] ASC, [IsComponent] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCT_CATEGORIES_RawMaterialGroups]
    ON [dbo].[PRODUCT_CATEGORIES]([RawMaterialGroup] ASC, [RawMaterialSubGrp] ASC);
GO
CREATE TABLE [dbo].[PRODUCT_PRODUCTION] (
    [ID]         INT        IDENTITY (1, 1) NOT NULL,
    [ProductID]  INT        NOT NULL,
    [ShopID]     INT        NOT NULL,
    [Date]       DATE       NOT NULL,
    [DayShift]   FLOAT (53) NOT NULL,
    [NightShift] FLOAT (53) NOT NULL,
    [Total]      AS         ([DayShift] + [NightShift]),
    [Comment]    TEXT       NULL,
    [IsDeleted]  BIT        NULL,
    [PlanDate]   DATE       NULL,
    CONSTRAINT [PK_PRODUCT_PRODUCTION] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCT_PRODUCTION_PlanDate]
    ON [dbo].[PRODUCT_PRODUCTION]([PlanDate] ASC)
    INCLUDE([ProductID], [ShopID], [Date], [Total], [IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCT_PRODUCTION_Shop_Date]
    ON [dbo].[PRODUCT_PRODUCTION]([ShopID] ASC, [Date] ASC)
    INCLUDE([ProductID], [Total], [IsDeleted]);
GO
CREATE TABLE [dbo].[PRODUCT_STOCKS] (
    [ID]        INT        IDENTITY (1, 1) NOT NULL,
    [WHID]      INT        NOT NULL,
    [ProductID] INT        NOT NULL,
    [Amount]    FLOAT (53) NOT NULL,
    [UnitID]    INT        NOT NULL,
    CONSTRAINT [PK_PROD_STOCKS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_PRODUCT_STOCKS_WH_Product]
    ON [dbo].[PRODUCT_STOCKS]([WHID] ASC, [ProductID] ASC)
    INCLUDE([Amount], [UnitID]);
GO
CREATE TABLE [dbo].[PRODUCTIMAGES] (
    [ID]        INT   IDENTITY (1, 1) NOT NULL,
    [ProdID]    INT   NOT NULL,
    [Image]     IMAGE NULL,
    [IsDeleted] BIT   NULL,
    CONSTRAINT [PK_PRODUCTIMAGES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCTIMAGES_ProdID]
    ON [dbo].[PRODUCTIMAGES]([ProdID] ASC)
    INCLUDE([IsDeleted]);
GO
CREATE TABLE [dbo].[PRODUCTIONPROCESSES] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [ProcessName] VARCHAR (50) NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NULL,
    CONSTRAINT [PK_PRODUCTIONPROCESSES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[PRODUCTPLANS] (
    [ID]           INT        IDENTITY (1, 1) NOT NULL,
    [ProductID]    INT        NOT NULL,
    [Amount]       FLOAT (53) NOT NULL,
    [IsDeleted]    BIT        NOT NULL,
    [StartDate]    DATE       NOT NULL,
    [DueDate]      DATE       NOT NULL,
    [SalesOrderID] INT        NULL,
    CONSTRAINT [PK_PRODUCTPLANS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_PRODUCTPLAN_SalesOrderID]
    ON [dbo].[PRODUCTPLANS]([SalesOrderID] ASC);
GO
CREATE TABLE [dbo].[PRODUCTPLANS_DAILY] (
    [ID]         INT        IDENTITY (1, 1) NOT NULL,
    [PlanID]     INT        NOT NULL,
    [Day]        DATE       NOT NULL,
    [DayShift]   FLOAT (53) NOT NULL,
    [NightShift] FLOAT (53) NOT NULL,
    [Total]      AS         ([DayShift] + [NightShift]),
    CONSTRAINT [PK_PRODUCTPLANS_DAILY] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[PRODUCTS] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [PNo]               VARCHAR (15)   NOT NULL,
    [Name]              VARCHAR (50)   NULL,
    [Weight]            FLOAT (53)     NULL,
    [Length]            FLOAT (53)     NULL,
    [Width]             FLOAT (53)     NULL,
    [Height]            FLOAT (53)     NULL,
    [Type]              VARCHAR (50)   NULL,
    [Description]       TEXT           NULL,
    [IsDeleted]         BIT            NOT NULL,
    [PNo2]              VARCHAR (15)   NULL,
    [PNo3]              VARCHAR (15)   NULL,
    [PNo4]              VARCHAR (15)   NULL,
    [HSCodeID]          INT            NULL,
    [UnitID]            INT            NOT NULL,
    [IsRawMaterial]     BIT            NULL,
    [IsComponent]       BIT            NULL,
    [IsFinishedGood]    BIT            NULL,
    [CategoryID]        INT            NULL,
    [NX3DFilePath]      NVARCHAR (MAX) NULL,
    [ShopID]            INT            NOT NULL,
    [StandardLengthMm]  FLOAT (53)     NULL,
    [OptimizationType]  TINYINT        NULL,
    [CalculateByWeight] BIT            NULL,
    CONSTRAINT [PK_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_PRODUCTS] UNIQUE NONCLUSTERED ([ID] ASC),
    CONSTRAINT [UX_PRODUCTS_PNo] UNIQUE NONCLUSTERED ([PNo] ASC)
);
GO
CREATE TABLE [dbo].[QUALITY_INSPECTION] (
    [ID]                 BIGINT          IDENTITY (1, 1) NOT NULL,
    [SOrderID]           INT             NULL,
    [DeliveryScheduleID] BIGINT          NULL,
    [ProductPlanDailyID] INT             NULL,
    [ProductionID]       INT             NULL,
    [ProductID]          INT             NOT NULL,
    [QtyInspected]       DECIMAL (18, 3) NOT NULL,
    [QtyAccepted]        DECIMAL (18, 3) NOT NULL,
    [QtyRejected]        DECIMAL (18, 3) NOT NULL,
    [DefectTypeID]       INT             NULL,
    [StatusCode]         NVARCHAR (50)   NULL,
    [Notes]              NVARCHAR (MAX)  NULL,
    [InspectedByUserID]  INT             NULL,
    [ApprovedByUserID]   INT             NULL,
    [InspectionDate]     DATETIME        NOT NULL,
    [ApprovalDate]       DATETIME        NULL,
    [CreatedDT]          DATETIME        NOT NULL,
    [UpdatedDT]          DATETIME        NULL,
    [IsDeleted]          BIT             NOT NULL,
    CONSTRAINT [PK_QUALITY_INSPECTION] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_QI_InspectionDate]
    ON [dbo].[QUALITY_INSPECTION]([InspectionDate] ASC)
    INCLUDE([StatusCode], [QtyAccepted], [QtyRejected], [DefectTypeID], [IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_QUALITY_INSPECTION_InspectionDate]
    ON [dbo].[QUALITY_INSPECTION]([InspectionDate] DESC)
    INCLUDE([ProductID], [StatusCode], [QtyAccepted], [QtyRejected]);
GO
CREATE NONCLUSTERED INDEX [IX_QUALITY_INSPECTION_ProductID]
    ON [dbo].[QUALITY_INSPECTION]([ProductID] ASC)
    INCLUDE([StatusCode], [QtyAccepted], [QtyRejected]);
GO
CREATE NONCLUSTERED INDEX [IX_QI_PlanDaily]
    ON [dbo].[QUALITY_INSPECTION]([ProductPlanDailyID] ASC)
    INCLUDE([ProductionID], [QtyInspected], [IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_QUALITY_INSPECTION_ProductionID]
    ON [dbo].[QUALITY_INSPECTION]([ProductionID] ASC) WHERE ([ProductionID] IS NOT NULL);
GO
CREATE NONCLUSTERED INDEX [IX_QUALITY_INSPECTION_StatusCode]
    ON [dbo].[QUALITY_INSPECTION]([StatusCode] ASC, [IsDeleted] ASC)
    INCLUDE([ProductID], [QtyAccepted]);
GO
CREATE NONCLUSTERED INDEX [IX_QI_Product_Status]
    ON [dbo].[QUALITY_INSPECTION]([ProductID] ASC, [StatusCode] ASC)
    INCLUDE([QtyAccepted], [IsDeleted]);
GO
CREATE NONCLUSTERED INDEX [IX_QI_ProductionID]
    ON [dbo].[QUALITY_INSPECTION]([ProductionID] ASC)
    INCLUDE([QtyAccepted], [InspectionDate], [ProductPlanDailyID], [IsDeleted]);
GO
CREATE TABLE [dbo].[RECONCILIATION_ADJUSTMENTS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [CounterpartyType] TINYINT         NOT NULL,
    [CounterpartyID]   INT             NOT NULL,
    [ContractID]       INT             NOT NULL,
    [EntryYear]        INT             NOT NULL,
    [CutoffDate]       DATE            NOT NULL,
    [Party1Debit]      DECIMAL (18, 2) NOT NULL,
    [Party1Credit]     DECIMAL (18, 2) NOT NULL,
    [Note]             NVARCHAR (500)  NULL,
    [CreatedDT]        DATETIME2 (7)   NOT NULL,
    [ModifiedDT]       DATETIME2 (7)   NULL,
    [CreatedByUserID]  INT             NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UQ_RECON_ADJ_CONTRACT_YEAR] UNIQUE NONCLUSTERED ([CounterpartyType] ASC, [CounterpartyID] ASC, [ContractID] ASC, [EntryYear] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_RECON_ADJ_LOOKUP]
    ON [dbo].[RECONCILIATION_ADJUSTMENTS]([CounterpartyType] ASC, [CounterpartyID] ASC, [ContractID] ASC, [CutoffDate] ASC);
GO
CREATE TABLE [dbo].[ROLES] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [RName]       VARCHAR (25) NOT NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NULL,
    CONSTRAINT [PK_ROLES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[S_CONTRACT_PRODUCTS] (
    [ID]          INT        IDENTITY (1, 1) NOT NULL,
    [ContractID]  INT        NOT NULL,
    [ProductID]   INT        NOT NULL,
    [PiecePrice]  FLOAT (53) NOT NULL,
    [Amount]      FLOAT (53) NOT NULL,
    [UnitID]      INT        NOT NULL,
    [vatamount]   FLOAT (53) NULL,
    [totalamount] AS         ([pieceprice] * [amount] + [vatamount]),
    CONSTRAINT [PK_S_CONTRACT_PRODUCTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[S_CONTRACTS] (
    [ID]           INT          IDENTITY (1, 1) NOT NULL,
    [ContractNo]   VARCHAR (50) NOT NULL,
    [IssuedDate]   DATE         NOT NULL,
    [CompanyID]    INT          NOT NULL,
    [CustomerID]   INT          NOT NULL,
    [Amount]       FLOAT (53)   NULL,
    [Incoterms]    VARCHAR (15) NULL,
    [PaymentTerms] VARCHAR (50) NULL,
    [DueDate]      DATE         NOT NULL,
    [IsDeleted]    BIT          NULL,
    [CurrencyID]   INT          NOT NULL,
    CONSTRAINT [PK_SCONTRACTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[S_ORDER_PRODUCTS] (
    [ID]         INT        IDENTITY (1, 1) NOT NULL,
    [OrderID]    INT        NOT NULL,
    [ProductID]  INT        NOT NULL,
    [Price]      FLOAT (53) NOT NULL,
    [Amount]     FLOAT (53) NOT NULL,
    [TotalPrice] AS         ([Price] * [Amount]),
    [MOQ]        FLOAT (53) NULL,
    [UnitID]     INT        NOT NULL,
    CONSTRAINT [PK_S_ORDER_PARTS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[S_ORDERS] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [OrderNo]     VARCHAR (50) NOT NULL,
    [IssuedDate]  DATETIME     NOT NULL,
    [CompanyID]   INT          NOT NULL,
    [CustomerID]  INT          NOT NULL,
    [ContractID]  INT          NOT NULL,
    [Amount]      FLOAT (53)   NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NULL,
    [CurrencyID]  INT          NOT NULL,
    CONSTRAINT [PK_S_ORDERS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_S_ORDERS] UNIQUE NONCLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SALES_INVOICE_LINES] (
    [ID]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [InvoiceID]      BIGINT          NOT NULL,
    [LineNo]         INT             NOT NULL,
    [ProductID]      INT             NOT NULL,
    [Description]    NVARCHAR (500)  NULL,
    [Qty]            DECIMAL (18, 3) NOT NULL,
    [UnitPrice]      DECIMAL (18, 4) NOT NULL,
    [LineAmount]     AS              ([Qty] * [UnitPrice]) PERSISTED,
    [VATRate]        DECIMAL (5, 2)  NOT NULL,
    [VATAmount]      DECIMAL (18, 2) NOT NULL,
    [TotalWithVAT]   AS              ([Qty] * [UnitPrice] + [VATAmount]) PERSISTED,
    [WarehouseOutID] BIGINT          NULL,
    CONSTRAINT [PK_SALES_INVOICE_LINES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_SALES_INVOICE_LINES] UNIQUE NONCLUSTERED ([InvoiceID] ASC, [LineNo] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICE_LINES_InvoiceID]
    ON [dbo].[SALES_INVOICE_LINES]([InvoiceID] ASC)
    INCLUDE([ProductID], [Qty], [LineAmount]);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICE_LINES_Invoice]
    ON [dbo].[SALES_INVOICE_LINES]([InvoiceID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICE_LINES_ProductID]
    ON [dbo].[SALES_INVOICE_LINES]([ProductID] ASC)
    INCLUDE([InvoiceID], [Qty]);
GO
CREATE TABLE [dbo].[SALES_INVOICES] (
    [ID]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [InvoiceNo]       NVARCHAR (50)   NOT NULL,
    [InvoiceDate]     DATE            NOT NULL,
    [CustomerID]      INT             NOT NULL,
    [ContractID]      INT             NULL,
    [OrderID]         INT             NULL,
    [TotalAmount]     DECIMAL (18, 2) NOT NULL,
    [TotalVAT]        DECIMAL (18, 2) NOT NULL,
    [TotalWithVAT]    AS              ([TotalAmount] + [TotalVAT]) PERSISTED,
    [DueDate]         DATE            NULL,
    [StatusCode]      NVARCHAR (50)   NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME2 (0)   NOT NULL,
    [UpdatedDT]       DATETIME2 (0)   NULL,
    [CurrencyID]      INT             NULL,
    CONSTRAINT [PK_SALES_INVOICES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_SALES_INVOICES_InvoiceNo] UNIQUE NONCLUSTERED ([InvoiceNo] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICES_Customer_Date]
    ON [dbo].[SALES_INVOICES]([CustomerID] ASC, [InvoiceDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICES_Contract]
    ON [dbo].[SALES_INVOICES]([ContractID] ASC)
    INCLUDE([StatusCode]);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICES_InvoiceDate]
    ON [dbo].[SALES_INVOICES]([InvoiceDate] ASC)
    INCLUDE([CustomerID], [ContractID], [OrderID], [TotalAmount], [StatusCode]);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICES_Order]
    ON [dbo].[SALES_INVOICES]([OrderID] ASC)
    INCLUDE([StatusCode]);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_INVOICES_Customer]
    ON [dbo].[SALES_INVOICES]([CustomerID] ASC)
    INCLUDE([StatusCode]);
GO
CREATE TABLE [dbo].[SALES_SF_HEADERS] (
    [ID]                    BIGINT           IDENTITY (1, 1) NOT NULL,
    [SFNumber]              NVARCHAR (50)    NOT NULL,
    [SFDate]                DATE             NOT NULL,
    [InvoiceID]             BIGINT           NULL,
    [CustomerID]            INT              NOT NULL,
    [SupplierINN]           NVARCHAR (20)    NULL,
    [CustomerINN]           NVARCHAR (20)    NULL,
    [XmlPayload]            XML              NULL,
    [StatusCode]            NVARCHAR (50)    NOT NULL,
    [CreatedDT]             DATETIME2 (0)    NOT NULL,
    [CreatedByUserID]       INT              NULL,
    [IntegrationUID]        UNIQUEIDENTIFIER NOT NULL,
    [SourceSystem]          NVARCHAR (50)    NOT NULL,
    [ExternalSystemCode]    NVARCHAR (50)    NULL,
    [ExternalSFKey]         NVARCHAR (100)   NULL,
    [IntegrationStatusCode] NVARCHAR (50)    NOT NULL,
    [LastSyncDT]            DATETIME2 (0)    NULL,
    [XmlSchemaVersion]      NVARCHAR (20)    NULL,
    [TaxMessageID]          NVARCHAR (100)   NULL,
    CONSTRAINT [PK_SALES_SF_HEADERS] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_SALES_SF_HEADERS] UNIQUE NONCLUSTERED ([SFNumber] ASC, [SFDate] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_SF_HEADERS_Integration]
    ON [dbo].[SALES_SF_HEADERS]([IntegrationUID] ASC, [ExternalSystemCode] ASC, [IntegrationStatusCode] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_SF_HEADERS_Invoice]
    ON [dbo].[SALES_SF_HEADERS]([InvoiceID] ASC);
GO
CREATE TABLE [dbo].[SALES_SF_LINES] (
    [ID]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [SFHeaderID]      BIGINT          NOT NULL,
    [LineNo]          INT             NOT NULL,
    [InvoiceLineID]   BIGINT          NULL,
    [ProductID]       INT             NOT NULL,
    [Description]     NVARCHAR (500)  NULL,
    [Qty]             DECIMAL (18, 3) NOT NULL,
    [UnitPrice]       DECIMAL (18, 4) NOT NULL,
    [LineAmount]      DECIMAL (18, 2) NOT NULL,
    [VATRate]         DECIMAL (5, 2)  NOT NULL,
    [VATAmount]       DECIMAL (18, 2) NOT NULL,
    [TotalWithVAT]    DECIMAL (18, 2) NOT NULL,
    [ExternalLineKey] NVARCHAR (100)  NULL,
    CONSTRAINT [PK_SALES_SF_LINES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_SALES_SF_LINES] UNIQUE NONCLUSTERED ([SFHeaderID] ASC, [LineNo] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_SF_LINES_InvoiceLine]
    ON [dbo].[SALES_SF_LINES]([InvoiceLineID] ASC);
GO
CREATE TABLE [dbo].[SALES_TTNK_HEADERS] (
    [ID]                     BIGINT           IDENTITY (1, 1) NOT NULL,
    [TTNKNumber]             NVARCHAR (50)    NOT NULL,
    [TTNKDate]               DATE             NOT NULL,
    [InvoiceID]              BIGINT           NULL,
    [OrderID]                INT              NULL,
    [ContractID]             INT              NULL,
    [ShipperName]            NVARCHAR (250)   NOT NULL,
    [ShipperINN]             NVARCHAR (20)    NULL,
    [ConsigneeName]          NVARCHAR (250)   NOT NULL,
    [ConsigneeINN]           NVARCHAR (20)    NULL,
    [CarrierName]            NVARCHAR (250)   NULL,
    [CarrierINN]             NVARCHAR (20)    NULL,
    [ResponsiblePersonName]  NVARCHAR (200)   NULL,
    [ResponsiblePersonPINFL] NVARCHAR (20)    NULL,
    [DriverName]             NVARCHAR (200)   NULL,
    [DriverPINFL]            NVARCHAR (20)    NULL,
    [TruckNo]                NVARCHAR (50)    NULL,
    [TrailerNo]              NVARCHAR (50)    NULL,
    [TruckModel]             NVARCHAR (100)   NULL,
    [TrailerModel]           NVARCHAR (100)   NULL,
    [DepartureAddress]       NVARCHAR (500)   NULL,
    [DepartureRegion]        NVARCHAR (200)   NULL,
    [ArrivalAddress]         NVARCHAR (500)   NULL,
    [ArrivalRegion]          NVARCHAR (200)   NULL,
    [DistanceKm]             DECIMAL (10, 2)  NULL,
    [CargoWeightGrossT]      DECIMAL (18, 3)  NULL,
    [CargoWeightNetT]        DECIMAL (18, 3)  NULL,
    [CurrencyCode]           CHAR (3)         NOT NULL,
    [DeliveryCost]           DECIMAL (18, 2)  NOT NULL,
    [VATAmount]              DECIMAL (18, 2)  NOT NULL,
    [TotalDeliveryCost]      AS               (isnull([DeliveryCost], (0)) + isnull([VATAmount], (0))) PERSISTED,
    [XmlPayload]             XML              NULL,
    [StatusCode]             NVARCHAR (50)    NOT NULL,
    [CreatedByUserID]        INT              NULL,
    [CreatedDT]              DATETIME2 (0)    NOT NULL,
    [UpdatedDT]              DATETIME2 (0)    NULL,
    [IntegrationUID]         UNIQUEIDENTIFIER NOT NULL,
    [ExternalSystemCode]     NVARCHAR (50)    NULL,
    [ExternalDocKey]         NVARCHAR (100)   NULL,
    [IntegrationStatusCode]  NVARCHAR (50)    NOT NULL,
    [LastSyncDT]             DATETIME2 (0)    NULL,
    [XmlSchemaVersion]       NVARCHAR (20)    NULL,
    [TaxMessageID]           NVARCHAR (100)   NULL,
    [CurrencyID]             INT              NULL,
    CONSTRAINT [PK_SALES_TTNK_HEADERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SALES_TTNK_Number_Date]
    ON [dbo].[SALES_TTNK_HEADERS]([TTNKNumber] ASC, [TTNKDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_TTNK_Order]
    ON [dbo].[SALES_TTNK_HEADERS]([OrderID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_TTNK_Invoice]
    ON [dbo].[SALES_TTNK_HEADERS]([InvoiceID] ASC);
GO
CREATE TABLE [dbo].[SALES_TTNK_INVOICES] (
    [ID]           BIGINT          IDENTITY (1, 1) NOT NULL,
    [TTNKHeaderID] BIGINT          NOT NULL,
    [InvoiceID]    BIGINT          NOT NULL,
    [AmountShare]  DECIMAL (18, 2) NULL,
    CONSTRAINT [PK_SALES_TTNK_INVOICES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SALES_TTNK_INVOICES]
    ON [dbo].[SALES_TTNK_INVOICES]([TTNKHeaderID] ASC, [InvoiceID] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_TTNK_INVOICES_InvoiceID]
    ON [dbo].[SALES_TTNK_INVOICES]([InvoiceID] ASC)
    INCLUDE([TTNKHeaderID]);
GO
CREATE TABLE [dbo].[SALES_TTNK_LINES] (
    [ID]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [TTNKHeaderID]  BIGINT          NOT NULL,
    [LineNo]        INT             NOT NULL,
    [ProductID]     INT             NULL,
    [InvoiceLineID] BIGINT          NULL,
    [HSCode]        NVARCHAR (20)   NULL,
    [Description]   NVARCHAR (500)  NOT NULL,
    [UnitOfMeasure] NVARCHAR (50)   NULL,
    [Qty]           DECIMAL (18, 3) NOT NULL,
    [UnitPrice]     DECIMAL (18, 4) NULL,
    [LineAmount]    DECIMAL (18, 2) NULL,
    [WeightNetT]    DECIMAL (18, 3) NULL,
    [WeightGrossT]  DECIMAL (18, 3) NULL,
    [PlaceCount]    INT             NULL,
    [PackageType]   NVARCHAR (100)  NULL,
    [Remark]        NVARCHAR (500)  NULL,
    [HsCodeID]      INT             NULL,
    CONSTRAINT [PK_SALES_TTNK_LINES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UX_SALES_TTNK_LINES_Line] UNIQUE NONCLUSTERED ([TTNKHeaderID] ASC, [LineNo] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SALES_TTNK_LINES_InvoiceLine]
    ON [dbo].[SALES_TTNK_LINES]([InvoiceLineID] ASC);
GO
CREATE TABLE [dbo].[SAMPLE_FILES] (
    [ID]       INT             IDENTITY (1, 1) NOT NULL,
    [File]     VARBINARY (MAX) NOT NULL,
    [FileName] VARCHAR (MAX)   NOT NULL,
    CONSTRAINT [PK_SAMPLE_FILES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] (
    [ID]               INT             IDENTITY (1, 1) NOT NULL,
    [ShareholderID]    INT             NOT NULL,
    [ContributionDate] DATE            NOT NULL,
    [Amount]           DECIMAL (18, 2) NOT NULL,
    [CurrencyID]       INT             NOT NULL,
    [Description]      NVARCHAR (500)  NULL,
    [ContributionType] TINYINT         NOT NULL,
    [FinTransactionID] INT             NULL,
    CONSTRAINT [PK_SHAREHOLDER_CONTRIBUTIONS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SHAREHOLDER_SHARE_PERIODS] (
    [ID]              INT            IDENTITY (1, 1) NOT NULL,
    [ShareholderID]   INT            NOT NULL,
    [FromDate]        DATE           NOT NULL,
    [ToDate]          DATE           NULL,
    [SharePercent]    DECIMAL (5, 2) NOT NULL,
    [EffectiveToDate] AS             (isnull([ToDate], CONVERT (DATE, '99991231', (112)))),
    CONSTRAINT [PK_SHAREHOLDER_SHARE_PERIODS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SHAREHOLDER_SHARE_PERIODS_Range]
    ON [dbo].[SHAREHOLDER_SHARE_PERIODS]([ShareholderID] ASC, [FromDate] ASC, [EffectiveToDate] ASC);
GO
CREATE TABLE [dbo].[SHAREHOLDERS] (
    [ID]        INT            IDENTITY (1, 1) NOT NULL,
    [FullName]  NVARCHAR (100) NOT NULL,
    [Phone]     NVARCHAR (50)  NULL,
    [Notes]     NVARCHAR (500) NULL,
    [IsActive]  BIT            NOT NULL,
    [IsDeleted] BIT            NULL,
    CONSTRAINT [PK_SHAREHOLDERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SHOPS] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [CompanyID]   INT          NULL,
    [ShopName]    VARCHAR (50) NULL,
    [Description] TEXT         NULL,
    [IsDeleted]   BIT          NULL,
    CONSTRAINT [PK_PRODUCTION_SHOPS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] (
    [ID]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [SOrderProductID] INT             NOT NULL,
    [SOrderID]        INT             NULL,
    [ProductID]       INT             NULL,
    [DeliveryDate]    DATE            NOT NULL,
    [QtyPlanned]      DECIMAL (18, 3) NOT NULL,
    [QtyShipped]      DECIMAL (18, 3) NOT NULL,
    [StatusCode]      NVARCHAR (50)   NOT NULL,
    [CreatedByUserID] INT             NULL,
    [CreatedDT]       DATETIME2 (0)   NOT NULL,
    [UpdatedDT]       DATETIME2 (0)   NULL,
    CONSTRAINT [PK_SORDER_DELIVERY_SCHEDULE] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_SORDER_DELIVERY_SCHEDULE_Order]
    ON [dbo].[SORDER_DELIVERY_SCHEDULE]([SOrderID] ASC)
    INCLUDE([SOrderProductID], [QtyPlanned], [QtyShipped]);
GO
CREATE NONCLUSTERED INDEX [IX_SORDER_DELIVERY_SOrderProd_Date]
    ON [dbo].[SORDER_DELIVERY_SCHEDULE]([SOrderProductID] ASC, [DeliveryDate] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_SORDER_DELIVERY_SOrder_Date]
    ON [dbo].[SORDER_DELIVERY_SCHEDULE]([SOrderID] ASC, [DeliveryDate] ASC);
GO
CREATE TABLE [dbo].[STD_COMPANY_STANDARDS] (
    [DocumentID]   BIGINT         NOT NULL,
    [StandardCode] NVARCHAR (100) NULL,
    [Area]         NVARCHAR (200) NULL,
    [Notes]        NVARCHAR (MAX) NULL,
    [VersionNo]    DECIMAL (5, 2) NOT NULL,
    [IsDeleted]    BIT            NULL,
    CONSTRAINT [PK_STD_COMPANY_STANDARDS] PRIMARY KEY CLUSTERED ([DocumentID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_STD_COMPANY_STANDARDS_Area]
    ON [dbo].[STD_COMPANY_STANDARDS]([Area] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_STD_COMPANY_STANDARDS_StandardCode]
    ON [dbo].[STD_COMPANY_STANDARDS]([StandardCode] ASC);
GO
CREATE TABLE [dbo].[SUPPLIER_BASE] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [CompanyName]   NVARCHAR (255) NULL,
    [ProductType]   NVARCHAR (255) NULL,
    [Country]       NVARCHAR (100) NULL,
    [Province]      NVARCHAR (100) NULL,
    [Region]        NVARCHAR (100) NULL,
    [ContactPerson] NVARCHAR (255) NULL,
    [Position]      NVARCHAR (100) NULL,
    [Phone]         NVARCHAR (50)  NULL,
    [Comment]       NVARCHAR (MAX) NULL,
    [IsDeleted]     BIT            NULL,
    CONSTRAINT [PK_SUPPLIER_BASE] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[SUPPLIERS] (
    [ID]                INT          IDENTITY (1, 1) NOT NULL,
    [Name]              VARCHAR (50) NOT NULL,
    [Address]           TEXT         NULL,
    [Country]           VARCHAR (30) NULL,
    [Telephone]         VARCHAR (25) NULL,
    [Email]             VARCHAR (50) NULL,
    [ContactPersonName] VARCHAR (30) NULL,
    [DirectorName]      VARCHAR (30) NULL,
    [IsDeleted]         BIT          NULL,
    [DUNS]              VARCHAR (50) NULL,
    [Type]              VARCHAR (10) NULL,
    [City]              VARCHAR (50) NULL,
    CONSTRAINT [PK_SUPPLIERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[TANNARX] (
    [ID]                                 INT             IDENTITY (1, 1) NOT NULL,
    [ProductID]                          INT             NOT NULL,
    [Year]                               INT             NOT NULL,
    [Month]                              INT             NOT NULL,
    [MonthString]                        NVARCHAR (20)   NULL,
    [MaterialPrice]                      DECIMAL (18, 4) NOT NULL,
    [MaterialTransportCost]              DECIMAL (18, 4) NOT NULL,
    [LaborCost]                          DECIMAL (18, 4) NOT NULL,
    [TaxAndBank]                         DECIMAL (18, 4) NOT NULL,
    [AdministrativeExpenses]             DECIMAL (18, 4) NOT NULL,
    [OtherExpenses]                      DECIMAL (18, 4) NOT NULL,
    [Profit]                             DECIMAL (18, 4) NOT NULL,
    [TaxAndBankPercent]                  DECIMAL (5, 2)  NULL,
    [AdministrativeExpensesPercent]      DECIMAL (5, 2)  NULL,
    [OtherExpensesPercent]               DECIMAL (5, 2)  NULL,
    [ProfitPercent]                      DECIMAL (5, 2)  NULL,
    [SalesPriceEXWWithoutVAT]            DECIMAL (18, 4) NOT NULL,
    [SalesPriceEXWWithVAT]               DECIMAL (18, 4) NOT NULL,
    [SetPriceEXWWithVAT]                 DECIMAL (18, 4) NULL,
    [FinishedProductTransportCost]       DECIMAL (18, 4) NULL,
    [SalesPriceCIPWithoutVAT]            DECIMAL (18, 4) NOT NULL,
    [SalesPriceCIPWithVAT]               DECIMAL (18, 4) NOT NULL,
    [SetPriceCIPWithVAT]                 DECIMAL (18, 4) NULL,
    [CreatedByUserID]                    INT             NULL,
    [CreatedDT]                          DATETIME        NOT NULL,
    [ModifiedByUserID]                   INT             NULL,
    [ModifiedDT]                         DATETIME        NULL,
    [IsDeleted]                          BIT             NOT NULL,
    [ActualMaterialPrice]                DECIMAL (18, 4) NULL,
    [ActualMaterialTransportCost]        DECIMAL (18, 4) NULL,
    [ActualLaborCost]                    DECIMAL (18, 4) NULL,
    [ActualTaxAndBank]                   DECIMAL (18, 4) NULL,
    [ActualAdministrativeExpenses]       DECIMAL (18, 4) NULL,
    [ActualOtherExpenses]                DECIMAL (18, 4) NULL,
    [ActualProfit]                       DECIMAL (18, 4) NULL,
    [ActualFinishedProductTransportCost] DECIMAL (18, 4) NULL,
    [ActualSalesPriceEXWWithoutVAT]      DECIMAL (18, 4) NULL,
    [ActualSalesPriceEXWWithVAT]         DECIMAL (18, 4) NULL,
    [ActualSalesPriceCIPWithoutVAT]      DECIMAL (18, 4) NULL,
    [ActualSalesPriceCIPWithVAT]         DECIMAL (18, 4) NULL,
    CONSTRAINT [PK_TANNARX] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_TANNARX_Product_Year_Month]
    ON [dbo].[TANNARX]([ProductID] ASC, [Year] ASC, [Month] ASC)
    INCLUDE([IsDeleted]);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_TANNARX_Unique_Product_Year_Month]
    ON [dbo].[TANNARX]([ProductID] ASC, [Year] ASC, [Month] ASC) WHERE ([IsDeleted]=(0));
GO
CREATE TABLE [dbo].[TANNARX_ACTUAL_AUDIT] (
    [ID]              INT             IDENTITY (1, 1) NOT NULL,
    [TannarxID]       INT             NOT NULL,
    [SelectedPath]    NVARCHAR (20)   NULL,
    [MethodCode]      NVARCHAR (64)   NULL,
    [ConfidenceScore] NVARCHAR (16)   NULL,
    [CoveragePercent] DECIMAL (18, 6) NOT NULL,
    [ReconcileDelta]  DECIMAL (18, 6) NOT NULL,
    [SourceAmount]    DECIMAL (18, 6) NOT NULL,
    [DenominatorQty]  DECIMAL (18, 6) NOT NULL,
    [ReasonCode]      NVARCHAR (64)   NULL,
    [UpdatedAt]       DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [UQ_TANNARX_ACTUAL_AUDIT_TannarxID] UNIQUE NONCLUSTERED ([TannarxID] ASC)
);
GO
CREATE TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP] (
    [CategoryID]      INT      NOT NULL,
    [Bucket]          TINYINT  NOT NULL,
    [UpdatedAt]       DATETIME NOT NULL,
    [UpdatedByUserID] INT      NULL,
    CONSTRAINT [PK_TANNARX_FIN_CATEGORY_MAP] PRIMARY KEY CLUSTERED ([CategoryID] ASC)
);
GO
CREATE TABLE [dbo].[UNITS] (
    [ID]        INT          IDENTITY (1, 1) NOT NULL,
    [UnitName]  VARCHAR (50) NOT NULL,
    [ShortName] VARCHAR (20) NOT NULL,
    [Code]      INT          NOT NULL,
    [IsDeleted] BIT          NULL,
    CONSTRAINT [PK_UNITS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[USER_ENTRIES] (
    [ID]       INT          IDENTITY (1, 1) NOT NULL,
    [UserID]   INT          NOT NULL,
    [DateTime] DATETIME     NOT NULL,
    [IP]       VARCHAR (20) NULL,
    [MAC]      VARCHAR (50) NULL,
    CONSTRAINT [PK_USER_ENTRIES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[USER_LICENSES] (
    [ID]               INT            IDENTITY (1, 1) NOT NULL,
    [LicenseKey]       VARCHAR (MAX)  NULL,
    [CompanyLicenseID] INT            NULL,
    [UserID]           INT            NULL,
    [StartDate]        DATETIME       NULL,
    [ExpiryDate]       DATETIME       NULL,
    [IsActive]         BIT            NOT NULL,
    [AssignedBy]       NVARCHAR (100) NULL,
    [AssignedDate]     DATETIME       NOT NULL,
    CONSTRAINT [PK_USER_LICENSES] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [IX_USER_LICENSES] UNIQUE NONCLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UL_License_User]
    ON [dbo].[USER_LICENSES]([CompanyLicenseID] ASC, [UserID] ASC) WHERE ([CompanyLicenseID] IS NOT NULL AND [UserID] IS NOT NULL);
GO
CREATE TABLE [dbo].[USERIMAGES] (
    [ID]     INT             IDENTITY (1, 1) NOT NULL,
    [Image]  VARBINARY (MAX) NOT NULL,
    [UserID] INT             NOT NULL,
    CONSTRAINT [PK_USERIMAGES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[USERLOGS] (
    [ControllerName] NVARCHAR (50) NOT NULL,
    [ActionName]     TEXT          NOT NULL,
    [DateTime]       DATETIME      NOT NULL,
    [UserID]         INT           NOT NULL,
    [ID]             INT           IDENTITY (1, 1) NOT NULL,
    [IP]             VARCHAR (50)  NOT NULL,
    [MacAddr]        VARCHAR (50)  NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[USERROLES] (
    [UserID] INT NOT NULL,
    [RoleID] INT NOT NULL
);
GO
CREATE TABLE [dbo].[USERS] (
    [ID]        INT          IDENTITY (1, 1) NOT NULL,
    [Uname]     VARCHAR (30) NOT NULL,
    [FullName]  VARCHAR (50) NULL,
    [Password]  VARCHAR (50) NOT NULL,
    [Email]     VARCHAR (50) NULL,
    [IsDeleted] BIT          NULL,
    [IsActive]  BIT          NOT NULL,
    [HashCode]  VARCHAR (30) NULL,
    [CompanyID] INT          NULL,
    CONSTRAINT [PK_USERS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[WAREHOUSE_LOCATIONS] (
    [ID]                 INT             IDENTITY (1, 1) NOT NULL,
    [WarehouseID]        INT             NOT NULL,
    [LocationCode]       NVARCHAR (30)   NOT NULL,
    [LocationName]       NVARCHAR (120)  NULL,
    [CapacityUomID]      INT             NULL,
    [MinCapacity]        DECIMAL (18, 4) NOT NULL,
    [MaxCapacity]        DECIMAL (18, 4) NOT NULL,
    [AllowMixedLots]     BIT             NOT NULL,
    [AllowMixedProducts] BIT             NOT NULL,
    [IsActive]           BIT             NOT NULL,
    [IsDeleted]          BIT             NOT NULL,
    [CreatedAt]          DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_WHL_Warehouse_LocationCode]
    ON [dbo].[WAREHOUSE_LOCATIONS]([WarehouseID] ASC, [LocationCode] ASC) WHERE ([IsDeleted]=(0));
GO
CREATE TABLE [dbo].[WAREHOUSES] (
    [ID]            INT          IDENTITY (1, 1) NOT NULL,
    [WHName]        VARCHAR (50) NULL,
    [Description]   TEXT         NULL,
    [IsDeleted]     BIT          NULL,
    [MRP]           INT          NULL,
    [ShopID]        INT          NULL,
    [Account]       INT          NULL,
    [WarehouseType] VARCHAR (20) NULL,
    CONSTRAINT [PK_PROD_WRHS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[WASTE_STOCKS] (
    [ID]      INT        IDENTITY (1, 1) NOT NULL,
    [WHID]    INT        NOT NULL,
    [WasteID] INT        NOT NULL,
    [Amount]  FLOAT (53) NOT NULL,
    [UnitID]  INT        NOT NULL,
    CONSTRAINT [PK_WASTE_STOCKS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[WASTES] (
    [ID]          INT          IDENTITY (1, 1) NOT NULL,
    [WasteName]   VARCHAR (50) NOT NULL,
    [ProcessID]   INT          NOT NULL,
    [Description] TEXT         NOT NULL,
    [IsDeleted]   BIT          NOT NULL,
    CONSTRAINT [PK_WASTES] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[WRHS_EXPENSE_DOCS] (
    [ID]        INT             IDENTITY (1, 1) NOT NULL,
    [ExpenseID] INT             NOT NULL,
    [Doc]       VARBINARY (MAX) NULL,
    CONSTRAINT [PK_PROD_WRHS_EXPENSE_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
CREATE TABLE [dbo].[WRHS_INCOME_DOCS] (
    [ID]       INT             IDENTITY (1, 1) NOT NULL,
    [IncomeID] INT             NOT NULL,
    [Doc]      VARBINARY (MAX) NULL,
    CONSTRAINT [PK_PROD_WRHS_INCOME_DOCS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO
ALTER TABLE [dbo].[ADM_COMPANY_ORDERS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[APP_TASK_PRIORITY_REF]
    ADD CONSTRAINT [DF_APP_TASK_PRIORITY_Sort] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[APP_TASK_PRIORITY_REF]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[APP_TASK_PROJECTS]
    ADD CONSTRAINT [DF_APP_TASK_PROJECTS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[APP_TASK_PROJECTS]
    ADD CONSTRAINT [DF_APP_TASK_PROJECTS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[APP_TASK_PROJECTS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[APP_TASK_STATUS_REF]
    ADD CONSTRAINT [DF_APP_TASK_STATUS_REF_IsFinal] DEFAULT ((0)) FOR [IsFinal];
GO
ALTER TABLE [dbo].[APP_TASK_STATUS_REF]
    ADD CONSTRAINT [DF_APP_TASK_STATUS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[APP_TASK_STATUS_REF]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[APP_TASK_STATUS_REF]
    ADD CONSTRAINT [DF_APP_TASK_STATUS_Sort] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[APP_TASKS]
    ADD CONSTRAINT [DF_APP_TASKS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[APP_TASKS]
    ADD CONSTRAINT [DF_APP_TASKS_IsDeleted] DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[COM_OFFICIAL_LETTERS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[COMPANY_LICENSES]
    ADD CONSTRAINT [DF_CL_MaxUsers] DEFAULT ((0)) FOR [MaxUsers];
GO
ALTER TABLE [dbo].[COMPANY_LICENSES]
    ADD CONSTRAINT [DF_CL_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[COMPANY_LICENSES]
    ADD CONSTRAINT [DF_CL_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[CURRENCIES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[CURRENCY_RATES]
    ADD CONSTRAINT [DF_CURRENCY_RATES_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[CURRENCY_RATES]
    ADD CONSTRAINT [DF_CURRENCY_RATES_Source] DEFAULT (N'MANUAL') FOR [Source];
GO
ALTER TABLE [dbo].[DOC_DOCUMENT_STATUS_REF]
    ADD CONSTRAINT [DF_DOC_DOCUMENT_STATUS_SortOrder] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[DOC_DOCUMENT_STATUS_REF]
    ADD CONSTRAINT [DF_DOC_DOCUMENT_STATUS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[DOC_DOCUMENT_TYPES]
    ADD CONSTRAINT [DF_DOC_DOCUMENT_TYPES_SortOrder] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[DOC_DOCUMENT_TYPES]
    ADD CONSTRAINT [DF_DOC_DOCUMENT_TYPES_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS]
    ADD CONSTRAINT [DF_DOC_DOCUMENTS_IntegrationUID] DEFAULT (newsequentialid()) FOR [IntegrationUID];
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS]
    ADD CONSTRAINT [DF_DOC_DOCUMENTS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[DOC_FILES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[DOC_FILES]
    ADD CONSTRAINT [DF_DOC_FILES_VersionNo] DEFAULT ((1)) FOR [VersionNo];
GO
ALTER TABLE [dbo].[DOC_FILES]
    ADD CONSTRAINT [DF_DOC_FILES_IsMain] DEFAULT ((1)) FOR [IsMain];
GO
ALTER TABLE [dbo].[DOC_FILES]
    ADD CONSTRAINT [DF_DOC_FILES_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[DOC_TEMPLATES]
    ADD CONSTRAINT [DF_DOC_TEMPLATES_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[DOC_TEMPLATES]
    ADD CONSTRAINT [DF_DOC_TEMPLATES_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS]
    ADD CONSTRAINT [DF_EPA_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[F_CONTRACT_TARIFFS]
    ADD DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[F_CONTRACT_TARIFFS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[F_TRANSPORT_TYPES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIFO_POLICY]
    ADD CONSTRAINT [DF_FP_AllowOverride] DEFAULT ((0)) FOR [AllowOverride];
GO
ALTER TABLE [dbo].[FIFO_POLICY]
    ADD CONSTRAINT [DF_FP_IsStrict] DEFAULT ((1)) FOR [IsStrictFifo];
GO
ALTER TABLE [dbo].[FIFO_POLICY]
    ADD CONSTRAINT [DF_FP_ReqApproval] DEFAULT ((1)) FOR [RequiresApprovalOnOverride];
GO
ALTER TABLE [dbo].[FIFO_VIOLATIONS]
    ADD CONSTRAINT [DF_FV_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD CONSTRAINT [DF_FIN_ACCOUNTS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD CONSTRAINT [DF_FIN_ACCOUNTS_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD CONSTRAINT [DF_FIN_ACCOUNTS_CurrencyID] DEFAULT ((1)) FOR [CurrencyID];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD CONSTRAINT [DF_FIN_ACCOUNTS_IsBank] DEFAULT ((0)) FOR [IsBank];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS]
    ADD CONSTRAINT [DF_FIN_ACCOUNTS_IsCash] DEFAULT ((0)) FOR [IsCash];
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES]
    ADD DEFAULT ((1)) FOR [CalculationType];
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES]
    ADD CONSTRAINT [DF_FIN_BALANCE_CATEGORIES_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES]
    ADD CONSTRAINT [DF_FIN_BALANCE_CATEGORIES_SortOrder] DEFAULT ((0)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS]
    ADD CONSTRAINT [DF_FIN_BALANCE_SNAPSHOTS_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS]
    ADD CONSTRAINT [DF_FIN_BALANCE_SNAPSHOTS_CurrencyID] DEFAULT ((1)) FOR [CurrencyID];
GO
ALTER TABLE [dbo].[FIN_CATEGORIES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_CATEGORIES]
    ADD CONSTRAINT [DF_FIN_CATEGORIES_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[FIN_CATEGORIES]
    ADD CONSTRAINT [DF_FIN_CATEGORIES_SortOrder] DEFAULT ((0)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[FIN_OPENING_CONTRACT_BALANCES]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[FIN_OPENING_CONTRACT_BALANCES]
    ADD CONSTRAINT [DF_FIN_OPENING_CONTRACT_BALANCES_PaidAmount] DEFAULT ((0)) FOR [PaidAmount];
GO
ALTER TABLE [dbo].[FIN_OPENING_CONTRACT_BALANCES]
    ADD CONSTRAINT [DF_FIN_OPENING_CONTRACT_BALANCES_TotalAmount] DEFAULT ((0)) FOR [TotalAmount];
GO
ALTER TABLE [dbo].[FIN_PL_ITEMS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_PL_ITEMS]
    ADD CONSTRAINT [DF_FIN_PL_ITEMS_SortOrder] DEFAULT ((0)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[FIN_PL_ITEMS]
    ADD CONSTRAINT [DF_FIN_PL_ITEMS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[FIN_PL_PLANS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_PL_PLANS]
    ADD CONSTRAINT [DF_FIN_PL_PLANS_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS]
    ADD CONSTRAINT [DF_FIN_TRANSACTIONS_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS]
    ADD CONSTRAINT [DF_FIN_TRANSACTIONS_CurrencyID] DEFAULT ((1)) FOR [CurrencyID];
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES]
    ADD CONSTRAINT [DF_HR_EMP_ALLOW_Created] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES]
    ADD CONSTRAINT [DF_HR_EMP_ALLOW_Special] DEFAULT ((0)) FOR [SpecialAllowancePercent];
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES]
    ADD CONSTRAINT [DF_HR_EMP_ALLOW_Material] DEFAULT ((0)) FOR [MaterialResponsibilityPercent];
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_EMPLOYEES]
    ADD DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_EMPLOYEES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_EMPLOYEES]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS]
    ADD CONSTRAINT [DF_HR_JOB_DESC_IsMandatory] DEFAULT ((1)) FOR [IsMandatory];
GO
ALTER TABLE [dbo].[HR_ORG_UNITS]
    ADD CONSTRAINT [DF_HR_ORG_UNITS_SortOrder] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[HR_ORG_UNITS]
    ADD CONSTRAINT [DF_HR_ORG_UNITS_LevelNo] DEFAULT ((1)) FOR [LevelNo];
GO
ALTER TABLE [dbo].[HR_ORG_UNITS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_ORG_UNITS]
    ADD CONSTRAINT [DF_HR_ORG_UNITS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_ORG_UNITS]
    ADD CONSTRAINT [DF_HR_ORG_UNITS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS]
    ADD DEFAULT ('Draft') FOR [Status];
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS]
    ADD DEFAULT ('Hourly') FOR [PayrollType];
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [HoursWorked];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [PieceQty];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [BaseSalary];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [GrossSalary];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [HourlyRate];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [Penalties];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [Bonuses];
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES]
    ADD DEFAULT ((0)) FOR [NetSalary];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS]
    ADD DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS]
    ADD DEFAULT ((0)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS]
    ADD DEFAULT ('Draft') FOR [Status];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_POSITIONS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_POSITIONS]
    ADD CONSTRAINT [DF_HR_POSITIONS_IsManager] DEFAULT ((0)) FOR [IsManager];
GO
ALTER TABLE [dbo].[HR_POSITIONS]
    ADD CONSTRAINT [DF_HR_POSITIONS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_POSITIONS]
    ADD CONSTRAINT [DF_HR_POSITIONS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_SALARY_GRADES]
    ADD DEFAULT ('UZS') FOR [CurrencyCode];
GO
ALTER TABLE [dbo].[HR_SALARY_GRADES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN]
    ADD CONSTRAINT [DF_HR_STAFFING_PLAN_IsFixed] DEFAULT ((1)) FOR [IsFixedSalary];
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN]
    ADD CONSTRAINT [DF_HR_STAFFING_PLAN_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN]
    ADD CONSTRAINT [DF_HR_STAFFING_PLAN_IsPiece] DEFAULT ((0)) FOR [IsPieceRate];
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN]
    ADD CONSTRAINT [DF_HR_STAFFING_PLAN_IsHourly] DEFAULT ((0)) FOR [IsHourly];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS]
    ADD DEFAULT ('Draft') FOR [Status];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day14Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day04Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day05Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day13Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day29Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day20Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day15Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day22Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [TotalWorkedDays];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day12Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day31Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day06Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day09Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day17Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [OvertimeHours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day23Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day28Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day30Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day02Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day01Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day26Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day25Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day08Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day21Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [TotalWorkedHours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day03Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day10Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [AbsentDays];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day07Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day16Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day18Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day27Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day24Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day19Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD DEFAULT ((0)) FOR [Day11Hours];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_SETTINGS]
    ADD CONSTRAINT [DF_HR_TIMESHEET_SETTINGS_Created] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[HR_TIMESHEET_SETTINGS]
    ADD CONSTRAINT [DF_HR_TIMESHEET_SETTINGS_FundHours] DEFAULT ((160)) FOR [FundHours];
GO
ALTER TABLE [dbo].[INCOME_LOTS]
    ADD CONSTRAINT [DF_IL_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[INCOME_LOTS]
    ADD CONSTRAINT [DF_IL_QtyConsumed] DEFAULT ((0)) FOR [QtyConsumed];
GO
ALTER TABLE [dbo].[INCOME_LOTS]
    ADD CONSTRAINT [DF_IL_IsDeleted] DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[INVENTORY_ADJUSTMENTS]
    ADD CONSTRAINT [DF_INVENTORY_ADJUSTMENTS_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[JOB_DESC]
    ADD DEFAULT ((1.00)) FOR [VersionNo];
GO
ALTER TABLE [dbo].[JOB_DESC]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[LICENSE_STATE]
    ADD CONSTRAINT [DF_LS_UpdatedAt] DEFAULT (getdate()) FOR [UpdatedAt];
GO
ALTER TABLE [dbo].[LOT_LOCATIONS]
    ADD CONSTRAINT [DF_LL_QtyAvailable] DEFAULT ((0)) FOR [QtyAvailable];
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES]
    ADD CONSTRAINT [DF_NORM_INPUT_LINES_Price] DEFAULT ((0)) FOR [Price];
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[NORMVERSIONS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[P_CONTRACTS]
    ADD DEFAULT ((0)) FOR [PurchaseType];
GO
ALTER TABLE [dbo].[P_INVOICES]
    ADD DEFAULT ((0)) FOR [PurchaseType];
GO
ALTER TABLE [dbo].[P_ORDERS]
    ADD DEFAULT ('Created') FOR [Status];
GO
ALTER TABLE [dbo].[P_ORDERS]
    ADD DEFAULT ((0)) FOR [PurchaseType];
GO
ALTER TABLE [dbo].[P_REQUISITIONS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[P_REQUISITIONS]
    ADD DEFAULT ('Draft') FOR [Status];
GO
ALTER TABLE [dbo].[P_REQUISITIONS]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_RequiresCutting] DEFAULT ((0)) FOR [RequiresCutting];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_AllowPurchase] DEFAULT ((1)) FOR [AllowPurchase];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_RequiresNORM] DEFAULT ((0)) FOR [RequiresNORM];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IsComponent] DEFAULT ((0)) FOR [IsComponent];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_AllowSales] DEFAULT ((0)) FOR [AllowSales];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IsFinishedGood] DEFAULT ((0)) FOR [IsFinishedGood];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_SortOrder] DEFAULT ((10)) FOR [SortOrder];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IsConsumable] DEFAULT ((0)) FOR [IsConsumable];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IsRawMaterial] DEFAULT ((0)) FOR [IsRawMaterial];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_IncludeInMRP] DEFAULT ((1)) FOR [IncludeInMRP];
GO
ALTER TABLE [dbo].[PRODUCT_CATEGORIES]
    ADD CONSTRAINT [DF_PC_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[PRODUCT_PRODUCTION]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[PRODUCTS]
    ADD DEFAULT ((0)) FOR [IsComponent];
GO
ALTER TABLE [dbo].[PRODUCTS]
    ADD DEFAULT ((0)) FOR [IsRawMaterial];
GO
ALTER TABLE [dbo].[PRODUCTS]
    ADD DEFAULT ((0)) FOR [IsFinishedGood];
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION]
    ADD CONSTRAINT [DF_QUALITY_INSPECTION_StatusCode] DEFAULT ('DRAFT') FOR [StatusCode];
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION]
    ADD CONSTRAINT [DF_QUALITY_INSPECTION_CreatedDT] DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION]
    ADD CONSTRAINT [DF_QUALITY_INSPECTION_InspectionDate] DEFAULT (getdate()) FOR [InspectionDate];
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[RECONCILIATION_ADJUSTMENTS]
    ADD CONSTRAINT [DF_RECON_ADJ_CDT] DEFAULT (sysutcdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[RECONCILIATION_ADJUSTMENTS]
    ADD CONSTRAINT [DF_RECON_ADJ_P1C] DEFAULT ((0)) FOR [Party1Credit];
GO
ALTER TABLE [dbo].[RECONCILIATION_ADJUSTMENTS]
    ADD CONSTRAINT [DF_RECON_ADJ_P1D] DEFAULT ((0)) FOR [Party1Debit];
GO
ALTER TABLE [dbo].[SALES_INVOICE_LINES]
    ADD DEFAULT ((0)) FOR [VATAmount];
GO
ALTER TABLE [dbo].[SALES_INVOICE_LINES]
    ADD DEFAULT ((12.0)) FOR [VATRate];
GO
ALTER TABLE [dbo].[SALES_INVOICES]
    ADD CONSTRAINT [DF_SALES_INVOICES_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[SALES_INVOICES]
    ADD DEFAULT ('DRAFT') FOR [StatusCode];
GO
ALTER TABLE [dbo].[SALES_INVOICES]
    ADD DEFAULT ((0)) FOR [TotalVAT];
GO
ALTER TABLE [dbo].[SALES_INVOICES]
    ADD DEFAULT ((0)) FOR [TotalAmount];
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS]
    ADD CONSTRAINT [DF_SALES_SF_HEADERS_IntegrationStatusCode] DEFAULT (N'LOCAL') FOR [IntegrationStatusCode];
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS]
    ADD CONSTRAINT [DF_SALES_SF_HEADERS_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS]
    ADD DEFAULT ('DRAFT') FOR [StatusCode];
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS]
    ADD CONSTRAINT [DF_SALES_SF_HEADERS_IntegrationUID] DEFAULT (newsequentialid()) FOR [IntegrationUID];
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS]
    ADD CONSTRAINT [DF_SALES_SF_HEADERS_SourceSystem] DEFAULT (N'IPACK_ERP') FOR [SourceSystem];
GO
ALTER TABLE [dbo].[SALES_SF_LINES]
    ADD DEFAULT ((12.0)) FOR [VATRate];
GO
ALTER TABLE [dbo].[SALES_SF_LINES]
    ADD DEFAULT ((0)) FOR [VATAmount];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_CurrencyCode] DEFAULT ('UZS') FOR [CurrencyCode];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_IntegrationUID] DEFAULT (newsequentialid()) FOR [IntegrationUID];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_DeliveryCost] DEFAULT ((0)) FOR [DeliveryCost];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_StatusCode] DEFAULT (N'DRAFT') FOR [StatusCode];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_VATAmount] DEFAULT ((0)) FOR [VATAmount];
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS]
    ADD CONSTRAINT [DF_TTNK_IntegrationStatus] DEFAULT (N'LOCAL') FOR [IntegrationStatusCode];
GO
ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS]
    ADD CONSTRAINT [DF_SHAREHOLDER_CONTRIBUTIONS_CurrencyID] DEFAULT ((1)) FOR [CurrencyID];
GO
ALTER TABLE [dbo].[SHAREHOLDERS]
    ADD CONSTRAINT [DF_SHAREHOLDERS_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[SHAREHOLDERS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE]
    ADD CONSTRAINT [DF_SORDER_DELIVERY_StatusCode] DEFAULT (N'PLANNED') FOR [StatusCode];
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE]
    ADD CONSTRAINT [DF_SORDER_DELIVERY_CreatedDT] DEFAULT (sysdatetime()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE]
    ADD CONSTRAINT [DF_SORDER_DELIVERY_QtyShipped] DEFAULT ((0)) FOR [QtyShipped];
GO
ALTER TABLE [dbo].[STD_COMPANY_STANDARDS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[STD_COMPANY_STANDARDS]
    ADD DEFAULT ((1.00)) FOR [VersionNo];
GO
ALTER TABLE [dbo].[SUPPLIER_BASE]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((10)) FOR [ProfitPercent];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [SalesPriceCIPWithoutVAT];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [TaxAndBank];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [Profit];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((5)) FOR [AdministrativeExpensesPercent];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((5)) FOR [TaxAndBankPercent];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [OtherExpenses];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((5)) FOR [OtherExpensesPercent];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [SalesPriceEXWWithVAT];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [MaterialPrice];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [AdministrativeExpenses];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [LaborCost];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [MaterialTransportCost];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT (getdate()) FOR [CreatedDT];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [SalesPriceCIPWithVAT];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [SalesPriceEXWWithoutVAT];
GO
ALTER TABLE [dbo].[TANNARX]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT]
    ADD CONSTRAINT [DF_TANNARX_AUDIT_Reconcile] DEFAULT ((0)) FOR [ReconcileDelta];
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT]
    ADD CONSTRAINT [DF_TANNARX_AUDIT_Coverage] DEFAULT ((0)) FOR [CoveragePercent];
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT]
    ADD CONSTRAINT [DF_TANNARX_AUDIT_SourceAmount] DEFAULT ((0)) FOR [SourceAmount];
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT]
    ADD CONSTRAINT [DF_TANNARX_AUDIT_UpdatedAt] DEFAULT (getdate()) FOR [UpdatedAt];
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT]
    ADD CONSTRAINT [DF_TANNARX_AUDIT_DenQty] DEFAULT ((0)) FOR [DenominatorQty];
GO
ALTER TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP]
    ADD CONSTRAINT [DF_TANNARX_FIN_CATEGORY_MAP_UpdatedAt] DEFAULT (getdate()) FOR [UpdatedAt];
GO
ALTER TABLE [dbo].[UNITS]
    ADD DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[USER_LICENSES]
    ADD CONSTRAINT [DF_UL_AssignedDate] DEFAULT (getdate()) FOR [AssignedDate];
GO
ALTER TABLE [dbo].[USER_LICENSES]
    ADD CONSTRAINT [DF_UL_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_IsDeleted] DEFAULT ((0)) FOR [IsDeleted];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_AllowMixedProducts] DEFAULT ((0)) FOR [AllowMixedProducts];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_MaxCapacity] DEFAULT ((0)) FOR [MaxCapacity];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_CreatedAt] DEFAULT (getdate()) FOR [CreatedAt];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_MinCapacity] DEFAULT ((0)) FOR [MinCapacity];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_IsActive] DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS]
    ADD CONSTRAINT [DF_WHL_AllowMixedLots] DEFAULT ((0)) FOR [AllowMixedLots];
GO
ALTER TABLE [dbo].[ADM_COMPANY_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_ADM_COMPANY_ORDERS_Document] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]);
GO
ALTER TABLE [dbo].[APP_TASKS] WITH NOCHECK
    ADD CONSTRAINT [FK_APP_TASKS_PROJECT] FOREIGN KEY ([ProjectID]) REFERENCES [dbo].[APP_TASK_PROJECTS] ([ID]);
GO
ALTER TABLE [dbo].[APP_TASKS] WITH NOCHECK
    ADD CONSTRAINT [FK_APP_TASKS_STATUS] FOREIGN KEY ([StatusID]) REFERENCES [dbo].[APP_TASK_STATUS_REF] ([ID]);
GO
ALTER TABLE [dbo].[APP_TASKS] WITH NOCHECK
    ADD CONSTRAINT [FK_APP_TASKS_PRIORITY] FOREIGN KEY ([PriorityID]) REFERENCES [dbo].[APP_TASK_PRIORITY_REF] ([ID]);
GO
ALTER TABLE [dbo].[BOM_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_BOM_HEADER_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[BOM_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_BOM_LINES_BOMID_BOM_HEADERS] FOREIGN KEY ([BOMID]) REFERENCES [dbo].[BOM_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[BOM_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_BOM_LINES_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[BOM_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_BOM_LINES_ComponentID_PRODUCTS] FOREIGN KEY ([ComponentID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[COM_OFFICIAL_LETTERS] WITH NOCHECK
    ADD CONSTRAINT [FK_COM_OFFICIAL_LETTERS_Document] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]);
GO
ALTER TABLE [dbo].[COMPANY_LICENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_COMPANY_LICENSES_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[COOKIES_CONFIRMATION] WITH NOCHECK
    ADD CONSTRAINT [FK_COOKIES_CONFIRMATION_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[CURRENCY_RATES] WITH NOCHECK
    ADD CONSTRAINT [FK_CURRENCY_RATES_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[DAMAGED_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DAMAGED_PRODUCTS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[DAMAGED_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DAMAGED_PRODUCTS_DEFECT_TYPES] FOREIGN KEY ([DefectTypeID]) REFERENCES [dbo].[DEFECT_TYPES] ([ID]);
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_DOCUMENTS_Template] FOREIGN KEY ([TemplateID]) REFERENCES [dbo].[DOC_TEMPLATES] ([ID]);
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_DOCUMENTS_OrgUnit] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_DOCUMENTS_Status] FOREIGN KEY ([StatusID]) REFERENCES [dbo].[DOC_DOCUMENT_STATUS_REF] ([ID]);
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_DOCUMENTS_Position] FOREIGN KEY ([PositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_DOCUMENTS_DocType] FOREIGN KEY ([DocTypeID]) REFERENCES [dbo].[DOC_DOCUMENT_TYPES] ([ID]);
GO
ALTER TABLE [dbo].[DOC_FILES] WITH NOCHECK
    ADD CONSTRAINT [FK_DOC_FILES_Document] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_EPA_WHL] FOREIGN KEY ([FromLocationID]) REFERENCES [dbo].[WAREHOUSE_LOCATIONS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_EPA_IL] FOREIGN KEY ([LotID]) REFERENCES [dbo].[INCOME_LOTS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_EPA_EP] FOREIGN KEY ([ExpenseProductID]) REFERENCES [dbo].[EXPENSE_PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_PROD_WRHS_EXPENSES] FOREIGN KEY ([ExpenseID]) REFERENCES [dbo].[EXPENSES] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_EP_IL] FOREIGN KEY ([LotID]) REFERENCES [dbo].[INCOME_LOTS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_EP_WHL] FOREIGN KEY ([FromLocationID]) REFERENCES [dbo].[WAREHOUSE_LOCATIONS] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSES_PROD_WRHS] FOREIGN KEY ([SenderWHID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSES] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSES_PROD_WRHS2] FOREIGN KEY ([ReceiverWhID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[EXPENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_EXPENSES_PRODUCT_PRODUCTION] FOREIGN KEY ([ProductionID]) REFERENCES [dbo].[PRODUCT_PRODUCTION] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACT_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACT_DOCS_F_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[F_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACT_TARIFFS_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[F_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACT_TARIFFS_TRANSPORT_TYPES] FOREIGN KEY ([TransportTypeID]) REFERENCES [dbo].[F_TRANSPORT_TYPES] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACT_TARIFFS_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACTS_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACTS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[F_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_CONTRACTS_FORWARDERS] FOREIGN KEY ([ForwarderID]) REFERENCES [dbo].[FORWARDERS] ([ID]);
GO
ALTER TABLE [dbo].[F_TRANSPORT_TYPES] WITH NOCHECK
    ADD CONSTRAINT [FK_F_TRANSPORT_TYPES_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILL_DOCS_F_WAYBILLS] FOREIGN KEY ([WaybillID]) REFERENCES [dbo].[F_WAYBILLS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_PAYMENT_SPLITS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILL_PAYMENT_SPLITS_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_PAYMENT_SPLITS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILL_PAYMENT_SPLITS_WAYBILLS] FOREIGN KEY ([WaybillID]) REFERENCES [dbo].[F_WAYBILLS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_PAYMENTS] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_PAYMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILL_FORWARDERS] FOREIGN KEY ([ForwarderID]) REFERENCES [dbo].[FORWARDERS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILL_PAYMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILL_PAYMENTS_WAYBILLS] FOREIGN KEY ([WaybillID]) REFERENCES [dbo].[F_WAYBILLS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_P_INVOICES] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[P_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_P_ORDERS] FOREIGN KEY ([PurchaseOrderID]) REFERENCES [dbo].[P_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_TTNK] FOREIGN KEY ([TTNKID]) REFERENCES [dbo].[SALES_TTNK_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_S_ORDERS] FOREIGN KEY ([SalesOrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_SALES_INVOICES] FOREIGN KEY ([SalesInvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_F_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[F_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_F_TRANSPORT_TYPES] FOREIGN KEY ([TransportTypeID]) REFERENCES [dbo].[F_TRANSPORT_TYPES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_FORWARDERS] FOREIGN KEY ([ForwarderID]) REFERENCES [dbo].[FORWARDERS] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[F_WAYBILLS] WITH NOCHECK
    ADD CONSTRAINT [FK_F_WAYBILLS_F_CONTRACT_TARIFFS] FOREIGN KEY ([TariffID]) REFERENCES [dbo].[F_CONTRACT_TARIFFS] ([ID]);
GO
ALTER TABLE [dbo].[FIFO_POLICY] WITH NOCHECK
    ADD CONSTRAINT [FK_FP_WAREHOUSE] FOREIGN KEY ([WarehouseID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_ACCOUNTS_CREATEDBY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_ACCOUNTS_BalanceCategory] FOREIGN KEY ([BalanceCategoryID]) REFERENCES [dbo].[FIN_BALANCE_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_ACCOUNTS_CURRENCY] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_CATEGORIES_PARENT] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[FIN_BALANCE_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_CATEGORY_CATEGORIES_BalanceCategory] FOREIGN KEY ([BalanceCategoryID]) REFERENCES [dbo].[FIN_BALANCE_CATEGORIES] ([ID]) ON DELETE CASCADE ON UPDATE CASCADE;
GO
ALTER TABLE [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_CATEGORY_CATEGORIES_Category] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[FIN_CATEGORIES] ([ID]) ON DELETE CASCADE ON UPDATE CASCADE;
GO
ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CURRENCY] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CREATEDBY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CATEGORY] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[FIN_BALANCE_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_CATEGORIES_PARENT] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[FIN_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_PL_ITEM_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_PL_ITEM_CATEGORIES_CATEGORY] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[FIN_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_PL_ITEM_CATEGORIES] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_PL_ITEM_CATEGORIES_ITEM] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[FIN_PL_ITEMS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_PL_ITEMS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_PL_ITEMS_PARENT] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[FIN_PL_ITEMS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_PL_PLANS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_PL_PLANS_ITEM] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[FIN_PL_ITEMS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_PL_PLANS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_PL_PLANS_CREATEDBY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_TRANSACTIONS_CURRENCY] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_TRANSACTIONS_ACCOUNT] FOREIGN KEY ([AccountID]) REFERENCES [dbo].[FIN_ACCOUNTS] ([ID]);
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_TRANSACTIONS_CATEGORY] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[FIN_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_FIN_TRANSACTIONS_CREATEDBY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMP_ALLOW_EMP] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[HR_EMPLOYEES] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_FROM_POSITIONS] FOREIGN KEY ([FromPositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_CREATED_BY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_TO_ORG_UNITS] FOREIGN KEY ([ToOrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_EMPLOYEES] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[HR_EMPLOYEES] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_TO_POSITIONS] FOREIGN KEY ([ToPositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_FROM_ORG_UNITS] FOREIGN KEY ([FromOrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEES_HR_ORG_UNITS] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEES_HR_SALARY_GRADES] FOREIGN KEY ([SalaryGradeID]) REFERENCES [dbo].[HR_SALARY_GRADES] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEES_USERS] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_EMPLOYEES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_EMPLOYEES_HR_POSITIONS] FOREIGN KEY ([PositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_OrgUnit] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_Document] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]);
GO
ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_Position] FOREIGN KEY ([PositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_ORG_UNITS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_ORG_UNITS_Parent] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_HEADERS_USERS_Created] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_HEADERS_HR_TIMESHEET_HEADERS] FOREIGN KEY ([TimesheetID]) REFERENCES [dbo].[HR_TIMESHEET_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_HEADERS_USERS_Approved] FOREIGN KEY ([ApprovedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_HEADERS_HR_ORG_UNITS] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_LINES_HR_PAYROLL_HEADERS] FOREIGN KEY ([PayrollID]) REFERENCES [dbo].[HR_PAYROLL_HEADERS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_LINES_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PAYROLL_LINES_HR_EMPLOYEES] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[HR_EMPLOYEES] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_OPERATIONS_ModifiedByUserID_USERS] FOREIGN KEY ([ModifiedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_OPERATIONS_CreatedByUserID_USERS] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_ModifiedByUserID_USERS] FOREIGN KEY ([ModifiedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_CURRENCIES] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_POSITIONS] FOREIGN KEY ([PositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_CreatedByUserID_USERS] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_OPERATIONS] FOREIGN KEY ([OperationID]) REFERENCES [dbo].[HR_PIECE_RATE_OPERATIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_ORG_UNITS] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_APPROVED_BY] FOREIGN KEY ([ApprovedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_MODIFIED_BY] FOREIGN KEY ([ModifiedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_CREATED_BY] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_OPERATIONS] FOREIGN KEY ([OperationID]) REFERENCES [dbo].[HR_PIECE_RATE_OPERATIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_ModifiedByUserID_USERS] FOREIGN KEY ([ModifiedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_CreatedByUserID_USERS] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_EMPLOYEES] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[HR_EMPLOYEES] ([ID]);
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_STAFFING_PLAN_Position] FOREIGN KEY ([PositionID]) REFERENCES [dbo].[HR_POSITIONS] ([ID]);
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_STAFFING_PLAN_Grade] FOREIGN KEY ([SalaryGradeID]) REFERENCES [dbo].[HR_SALARY_GRADES] ([ID]);
GO
ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_STAFFING_PLAN_OrgUnit] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_HEADERS_USERS_Approved] FOREIGN KEY ([ApprovedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_HEADERS_USERS_Created] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_HEADERS_HR_ORG_UNITS] FOREIGN KEY ([OrgUnitID]) REFERENCES [dbo].[HR_ORG_UNITS] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_LINES_HR_EMPLOYEES] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[HR_EMPLOYEES] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_LINES_HR_TIMESHEET_HEADERS] FOREIGN KEY ([TimesheetID]) REFERENCES [dbo].[HR_TIMESHEET_HEADERS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_LINES_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[HR_TIMESHEET_SETTINGS] WITH NOCHECK
    ADD CONSTRAINT [FK_HR_TIMESHEET_SETTINGS_HEADER] FOREIGN KEY ([TimesheetID]) REFERENCES [dbo].[HR_TIMESHEET_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[INCOME_LOTS] WITH NOCHECK
    ADD CONSTRAINT [FK_IL_INCOME] FOREIGN KEY ([IncomeID]) REFERENCES [dbo].[INCOMES] ([ID]);
GO
ALTER TABLE [dbo].[INCOME_LOTS] WITH NOCHECK
    ADD CONSTRAINT [FK_IL_INCOME_PRODUCTS] FOREIGN KEY ([IncomeProductID]) REFERENCES [dbo].[INCOME_PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOME_PRODS_PROD_WRHS_INCOMES] FOREIGN KEY ([IncomeID]) REFERENCES [dbo].[INCOMES] ([ID]);
GO
ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOME_PRODUCTS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOME_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[INCOMES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOMES_PROD_WRHS] FOREIGN KEY ([ReceivingWHID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[INCOMES] WITH NOCHECK
    ADD CONSTRAINT [FK_INCOMES_InvoiceID_P_INVOICES] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[P_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[INCOMES] WITH NOCHECK
    ADD CONSTRAINT [FK_INCOMES_PRODUCT_PRODUCTION] FOREIGN KEY ([ProductionID]) REFERENCES [dbo].[PRODUCT_PRODUCTION] ([ID]);
GO
ALTER TABLE [dbo].[INCOMES] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[INCOMES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOMES_PROD_WRHS_SENDER] FOREIGN KEY ([SenderWHID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[INVENTORY_ADJUSTMENTS] WITH NOCHECK
    ADD CONSTRAINT [FK_INVENTORY_ADJUSTMENTS_WAREHOUSES] FOREIGN KEY ([WarehouseID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[JOB_DESC] WITH NOCHECK
    ADD CONSTRAINT [FK_JOB_DESC_DOC_DOCUMENTS] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[JOB_DESC] WITH NOCHECK
    ADD CONSTRAINT [FK_JOB_DESC_HR_JOB_DESCRIPTIONS] FOREIGN KEY ([HRJobDescriptionID]) REFERENCES [dbo].[HR_JOB_DESCRIPTIONS] ([DocumentID]) ON DELETE SET NULL;
GO
ALTER TABLE [dbo].[LOT_LOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_LL_IL] FOREIGN KEY ([LotID]) REFERENCES [dbo].[INCOME_LOTS] ([ID]);
GO
ALTER TABLE [dbo].[LOT_LOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_LL_WHL] FOREIGN KEY ([LocationID]) REFERENCES [dbo].[WAREHOUSE_LOCATIONS] ([ID]);
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORM_INPUT_LINES_CreatedByUserID_USERS] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORM_INPUT_LINES_MaterialID_PRODUCTS] FOREIGN KEY ([MaterialID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORM_INPUT_LINES_ProductID_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORM_INPUT_LINES_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[NORMLINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORMLINES_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[NORMLINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORMLINES_MaterialID_PRODUCTS] FOREIGN KEY ([MaterialID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[NORMLINES] WITH NOCHECK
    ADD CONSTRAINT [FK_NORMLINES_NormID_NORMVERSIONS] FOREIGN KEY ([NormID]) REFERENCES [dbo].[NORMVERSIONS] ([ID]);
GO
ALTER TABLE [dbo].[NORMVERSIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_NORMVERSION_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACT_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_CONTRACT_DOCS_P_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[P_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_CONTRACT_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_CONTRACT_PRODUCTS_P_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[P_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_CONTRACT_PRODUCTS_HSCODES] FOREIGN KEY ([HSCodeID]) REFERENCES [dbo].[HSCODES] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_CONTRACT_PRODUCTS_ID_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACTS] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PCONTRACTS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[P_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PCONTRACTS_SUPPLIERS] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[SUPPLIERS] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICE_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICE_DOCS_P_INVOICES] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[P_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICE_PRODUCTS_P_INVOICES] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[P_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICE_PRODUCTS_ID_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICE_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICES_P_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[P_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICES_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICES_SUPPLIERS] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[SUPPLIERS] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICES] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[P_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_P_INVOICES_P_ORDERS] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[P_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDER_PRODUCTS_ID_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDER_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDER_PRODUCTS_P_ORDERS] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[P_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDERS_REQUISITION] FOREIGN KEY ([RequisitionID]) REFERENCES [dbo].[P_REQUISITIONS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDERS_P_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[P_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDERS_SUPPLIERS] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[SUPPLIERS] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDERS] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[P_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_ORDERS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[P_REQUISITION_APPROVALS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_REQUISITION_APPROVALS_REQUISITION] FOREIGN KEY ([RequisitionID]) REFERENCES [dbo].[P_REQUISITIONS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_REQUISITION_PRODUCTS_REQUISITION] FOREIGN KEY ([RequisitionID]) REFERENCES [dbo].[P_REQUISITIONS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_REQUISITION_PRODUCTS_UNIT] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_P_REQUISITION_PRODUCTS_PRODUCT] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[PERMISSIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_PERMISSIONS_PERMISSIONMODULES] FOREIGN KEY ([PermissionModuleID]) REFERENCES [dbo].[PERMISSIONMODULES] ([ID]);
GO
ALTER TABLE [dbo].[PERMISSIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_PERMISSIONS_ROLES] FOREIGN KEY ([RoleID]) REFERENCES [dbo].[ROLES] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCT_PRODUCTION] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCT_PRODUCTION_SHOPS] FOREIGN KEY ([ShopID]) REFERENCES [dbo].[SHOPS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCT_PRODUCTION] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCT_PRODUCTION_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCT_STOCKS] WITH NOCHECK
    ADD FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCT_STOCKS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_STOCKS_PROD_WRHS] FOREIGN KEY ([WHID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCT_STOCKS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_STOCKS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTIMAGES] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTIMAGES_PARTS] FOREIGN KEY ([ProdID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTPLANS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTPLAN_S_ORDERS] FOREIGN KEY ([SalesOrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTPLANS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTPLANS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTPLANS_DAILY] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTPLANS_DAILY_PRODUCTPLANS] FOREIGN KEY ([PlanID]) REFERENCES [dbo].[PRODUCTPLANS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTS_SHOPS] FOREIGN KEY ([ShopID]) REFERENCES [dbo].[SHOPS] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTS_HSCODES] FOREIGN KEY ([HSCodeID]) REFERENCES [dbo].[HSCODES] ([ID]);
GO
ALTER TABLE [dbo].[PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTS_CATEGORY] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[PRODUCT_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCTPLANS_DAILY] FOREIGN KEY ([ProductPlanDailyID]) REFERENCES [dbo].[PRODUCTPLANS_DAILY] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_DEFECT_TYPES] FOREIGN KEY ([DefectTypeID]) REFERENCES [dbo].[DEFECT_TYPES] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCT_PRODUCTION] FOREIGN KEY ([ProductionID]) REFERENCES [dbo].[PRODUCT_PRODUCTION] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_S_ORDERS] FOREIGN KEY ([SOrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_SORDER_DELIVERY_SCHEDULE] FOREIGN KEY ([DeliveryScheduleID]) REFERENCES [dbo].[SORDER_DELIVERY_SCHEDULE] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_USERS_ApprovedBy] FOREIGN KEY ([ApprovedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [FK_QUALITY_INSPECTION_USERS_InspectedBy] FOREIGN KEY ([InspectedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_CONTRACT_PRODUCTS_S_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[S_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_CONTRACT_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_CONTRACT_PRODUCTS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACTS] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_SCONTRACTS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[S_CONTRACTS] WITH NOCHECK
    ADD CONSTRAINT [FK_PCONTRACTS_CUSTOMERS] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[CUSTOMERS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDER_PRODUCTS_S_ORDERS] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDER_PRODUCTS_UNITS] FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDER_PRODUCTS_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDERS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDERS_CUSTOMERS] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[CUSTOMERS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDERS] WITH NOCHECK
    ADD CONSTRAINT [FK_S_ORDERS_S_CONTRACTS] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[S_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[S_ORDERS] WITH NOCHECK
    ADD FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICE_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICE_LINES_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICE_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICE_LINES_Invoice] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICES_CONTRACT] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[S_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICES_ORDER] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICES_Customer] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[CUSTOMERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICES_Currency] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_INVOICES_CreatedBy] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_HEADERS_Invoice] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_HEADERS_Customer] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[CUSTOMERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SF_HEADERS_INVOICE] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SF_HEADERS_CUSTOMER] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[CUSTOMERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_HEADERS_CreatedBy] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_LINES_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_LINES_InvoiceLine] FOREIGN KEY ([InvoiceLineID]) REFERENCES [dbo].[SALES_INVOICE_LINES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_SF_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_SF_LINES_Header] FOREIGN KEY ([SFHeaderID]) REFERENCES [dbo].[SALES_SF_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_TTNK_HEADERS_ORDER] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_HEADERS_Invoice] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_TTNK_HEADERS_CONTRACT] FOREIGN KEY ([ContractID]) REFERENCES [dbo].[S_CONTRACTS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_TTNK_HEADERS_INVOICE] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_HEADERS_Currency] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_INVOICES_Invoice] FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[SALES_INVOICES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_INVOICES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_INVOICES_Header] FOREIGN KEY ([TTNKHeaderID]) REFERENCES [dbo].[SALES_TTNK_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_LINES_InvoiceLine] FOREIGN KEY ([InvoiceLineID]) REFERENCES [dbo].[SALES_INVOICE_LINES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_LINES_Header] FOREIGN KEY ([TTNKHeaderID]) REFERENCES [dbo].[SALES_TTNK_HEADERS] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_LINES_HsCode] FOREIGN KEY ([HsCodeID]) REFERENCES [dbo].[HSCODES] ([ID]);
GO
ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH NOCHECK
    ADD CONSTRAINT [FK_SALES_TTNK_LINES_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_FINTRAN] FOREIGN KEY ([FinTransactionID]) REFERENCES [dbo].[FIN_TRANSACTIONS] ([ID]);
GO
ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_CURRENCY] FOREIGN KEY ([CurrencyID]) REFERENCES [dbo].[CURRENCIES] ([ID]);
GO
ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_SH] FOREIGN KEY ([ShareholderID]) REFERENCES [dbo].[SHAREHOLDERS] ([ID]);
GO
ALTER TABLE [dbo].[SHAREHOLDER_SHARE_PERIODS] WITH NOCHECK
    ADD CONSTRAINT [FK_SHAREHOLDER_SHARE_PERIODS_SH] FOREIGN KEY ([ShareholderID]) REFERENCES [dbo].[SHAREHOLDERS] ([ID]);
GO
ALTER TABLE [dbo].[SHOPS] WITH NOCHECK
    ADD CONSTRAINT [FK_PRODUCTION_SHOPS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH NOCHECK
    ADD CONSTRAINT [FK_SORDER_DELIVERY_SCHEDULE_S_ORDERS] FOREIGN KEY ([SOrderID]) REFERENCES [dbo].[S_ORDERS] ([ID]);
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH NOCHECK
    ADD CONSTRAINT [FK_SORDER_DELIVERY_CreatedBy] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH NOCHECK
    ADD CONSTRAINT [FK_SORDER_DELIVERY_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[STD_COMPANY_STANDARDS] WITH NOCHECK
    ADD CONSTRAINT [FK_STD_COMPANY_STANDARDS_DOC_DOCUMENTS] FOREIGN KEY ([DocumentID]) REFERENCES [dbo].[DOC_DOCUMENTS] ([ID]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[TANNARX] WITH NOCHECK
    ADD CONSTRAINT [FK_TANNARX_USERS_CreatedBy] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[TANNARX] WITH NOCHECK
    ADD CONSTRAINT [FK_TANNARX_USERS_ModifiedBy] FOREIGN KEY ([ModifiedByUserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[TANNARX] WITH NOCHECK
    ADD CONSTRAINT [FK_TANNARX_PRODUCTS] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[PRODUCTS] ([ID]);
GO
ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT] WITH NOCHECK
    ADD CONSTRAINT [FK_TANNARX_ACTUAL_AUDIT_TANNARX] FOREIGN KEY ([TannarxID]) REFERENCES [dbo].[TANNARX] ([ID]);
GO
ALTER TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP] WITH NOCHECK
    ADD CONSTRAINT [FK_TANNARX_FIN_CATEGORY_MAP_CATEGORY] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[FIN_CATEGORIES] ([ID]);
GO
ALTER TABLE [dbo].[USER_ENTRIES] WITH NOCHECK
    ADD CONSTRAINT [FK_USER_ENTRIES_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[USER_LICENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_UL_COMPANY_LICENSES] FOREIGN KEY ([CompanyLicenseID]) REFERENCES [dbo].[COMPANY_LICENSES] ([ID]);
GO
ALTER TABLE [dbo].[USER_LICENSES] WITH NOCHECK
    ADD CONSTRAINT [FK_UL_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[USERIMAGES] WITH NOCHECK
    ADD CONSTRAINT [FK_USERIMAGES_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[USERLOGS] WITH NOCHECK
    ADD CONSTRAINT [FK_USERLOGS_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[USERROLES] WITH NOCHECK
    ADD CONSTRAINT [FK_USERROLES_ROLES] FOREIGN KEY ([RoleID]) REFERENCES [dbo].[ROLES] ([ID]);
GO
ALTER TABLE [dbo].[USERROLES] WITH NOCHECK
    ADD CONSTRAINT [FK_USERROLES_USERS] FOREIGN KEY ([UserID]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[USERS] WITH NOCHECK
    ADD CONSTRAINT [FK_USERS_COMPANIES] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[COMPANIES] ([ID]);
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [FK_WHL_WAREHOUSES] FOREIGN KEY ([WarehouseID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[WAREHOUSES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_USERS] FOREIGN KEY ([MRP]) REFERENCES [dbo].[USERS] ([ID]);
GO
ALTER TABLE [dbo].[WAREHOUSES] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_PROD_SHOPS] FOREIGN KEY ([ShopID]) REFERENCES [dbo].[SHOPS] ([ID]);
GO
ALTER TABLE [dbo].[WASTE_STOCKS] WITH NOCHECK
    ADD CONSTRAINT [FK_WASTE_STOCKS_WHID_WAREHOUSES] FOREIGN KEY ([WHID]) REFERENCES [dbo].[WAREHOUSES] ([ID]);
GO
ALTER TABLE [dbo].[WASTE_STOCKS] WITH NOCHECK
    ADD CONSTRAINT [FK_WASTE_STOCKS_WASTES] FOREIGN KEY ([WasteID]) REFERENCES [dbo].[WASTES] ([ID]);
GO
ALTER TABLE [dbo].[WASTE_STOCKS] WITH NOCHECK
    ADD FOREIGN KEY ([UnitID]) REFERENCES [dbo].[UNITS] ([ID]);
GO
ALTER TABLE [dbo].[WASTES] WITH NOCHECK
    ADD CONSTRAINT [FK_WASTES_PRODUCTIONPROCESSES] FOREIGN KEY ([ProcessID]) REFERENCES [dbo].[PRODUCTIONPROCESSES] ([ID]);
GO
ALTER TABLE [dbo].[WRHS_EXPENSE_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_EXPENSE_DOCS_PROD_WRHS_EXPENSES] FOREIGN KEY ([ExpenseID]) REFERENCES [dbo].[EXPENSES] ([ID]);
GO
ALTER TABLE [dbo].[WRHS_INCOME_DOCS] WITH NOCHECK
    ADD CONSTRAINT [FK_PROD_WRHS_INCOME_DOCS_PROD_WRHS_INCOMES] FOREIGN KEY ([IncomeID]) REFERENCES [dbo].[INCOMES] ([ID]);
GO
ALTER TABLE [dbo].[CURRENCY_RATES] WITH NOCHECK
    ADD CONSTRAINT [CK_CURRENCY_RATES_Rate] CHECK ([Rate]>(0));
GO
ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [CK_EPA_Qty_Positive] CHECK ([Qty]>(0));
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [CK_HR_PAYROLL_HEADERS_Month] CHECK ([Month]>=(1) AND [Month]<=(12));
GO
ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [CK_HR_PAYROLL_HEADERS_Year] CHECK ([Year]>=(2000) AND [Year]<=(2100));
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [CK_HR_TIMESHEET_HEADERS_Month] CHECK ([Month]>=(1) AND [Month]<=(12));
GO
ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH NOCHECK
    ADD CONSTRAINT [CK_HR_TIMESHEET_HEADERS_Year] CHECK ([Year]>=(2000) AND [Year]<=(2100));
GO
ALTER TABLE [dbo].[LOT_LOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [CK_LL_Qty_NonNegative] CHECK ([QtyAvailable]>=(0));
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [CK_QUALITY_INSPECTION_Quantities] CHECK (([QtyAccepted]+[QtyRejected])=[QtyInspected]);
GO
ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH NOCHECK
    ADD CONSTRAINT [CK_QUALITY_INSPECTION_NonNegative] CHECK ([QtyInspected]>=(0) AND [QtyAccepted]>=(0) AND [QtyRejected]>=(0));
GO
ALTER TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP] WITH NOCHECK
    ADD CONSTRAINT [CK_TANNARX_FIN_CATEGORY_MAP_Bucket] CHECK ([Bucket]=(3) OR [Bucket]=(2) OR [Bucket]=(1) OR [Bucket]=(0));
GO
ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS] WITH NOCHECK
    ADD CONSTRAINT [CK_WHL_MinLeMax] CHECK ([MinCapacity]<=[MaxCapacity]);
GO
ALTER TABLE [dbo].[WAREHOUSES] WITH NOCHECK
    ADD CONSTRAINT [CK_WAREHOUSES_WarehouseType] CHECK ([WarehouseType] IS NULL OR ([WarehouseType]='SALES' OR [WarehouseType]='PRODUCTION' OR [WarehouseType]='SUPPLY'));
GO
CREATE TRIGGER [dbo].[Update_Contract_Amount_After_Delete]
ON [dbo].[P_CONTRACT_PRODUCTS]
AFTER DELETE
AS
BEGIN
    UPDATE P_CONTRACTS
    SET Amount = (
        SELECT SUM(TotalAmount)
        FROM P_CONTRACT_PRODUCTS
        WHERE ContractID = deleted.ContractID
    )
    FROM P_CONTRACTS
    INNER JOIN deleted ON P_CONTRACTS.ID = deleted.ContractID;
END
GO
CREATE TRIGGER [dbo].[Update_Contract_Amount_After_Insert]
ON [dbo].[P_CONTRACT_PRODUCTS]
AFTER INSERT
AS
BEGIN
    UPDATE P_CONTRACTS
    SET Amount = (
        SELECT SUM(TotalAmount)
        FROM P_CONTRACT_PRODUCTS
        WHERE ContractID = inserted.ContractID
    )
    FROM P_CONTRACTS
    INNER JOIN inserted ON P_CONTRACTS.ID= inserted.ContractID
	WHERE P_CONTRACTS.IsDeleted = 0;
END;
GO
CREATE TRIGGER [dbo].[Update_Invoice_Amount_After_Insert] 
ON [dbo].[P_INVOICE_PRODUCTS]
AFTER INSERT
AS
BEGIN
    UPDATE P_INVOICES
    SET Amount = (
        SELECT SUM(TotalPrice)
        FROM P_INVOICE_PRODUCTS
        WHERE InvoiceID = inserted.InvoiceID
    )
    FROM P_INVOICES
    INNER JOIN inserted ON P_INVOICES.ID = inserted.InvoiceID;
END;
GO
CREATE TRIGGER [dbo].[Update_Invoice_Amount_After_Delete] 
ON [dbo].[P_INVOICE_PRODUCTS]
AFTER DELETE
AS
BEGIN
    UPDATE P_INVOICES
    SET Amount = (
        SELECT SUM(TotalPrice)
        FROM P_INVOICE_PRODUCTS
        WHERE InvoiceID = deleted.InvoiceID
    )
    FROM P_INVOICES
    INNER JOIN deleted ON P_INVOICES.ID = deleted.InvoiceID;
END;
GO
CREATE TRIGGER [dbo].[Update_Order_Amount_After_Insert] 
ON [dbo].[P_ORDER_PRODUCTS]
AFTER INSERT
AS
BEGIN
    UPDATE P_ORDERS
    SET Amount = (
        SELECT SUM(TotalPrice)
        FROM P_ORDER_PRODUCTS
        WHERE OrderID = inserted.OrderID
    )
    FROM P_ORDERS
    INNER JOIN inserted ON P_ORDERS.ID = inserted.OrderID;
END;
GO
CREATE TRIGGER [dbo].[Update_Order_Amount_After_Delete] 
ON [dbo].[P_ORDER_PRODUCTS]
AFTER DELETE
AS
BEGIN
    UPDATE P_ORDERS
    SET Amount = (
        SELECT SUM(TotalPrice)
        FROM P_ORDER_PRODUCTS
        WHERE OrderID = deleted.OrderID
    )
    FROM P_ORDERS
    INNER JOIN deleted ON P_ORDERS.ID = deleted.OrderID;
END;
GO
CREATE TRIGGER dbo.TR_ProductCategories_AutoCode
ON dbo.PRODUCT_CATEGORIES
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- SERIALIZABLE uchun lock
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    -- Insert rows WITH auto-generated Code
    INSERT INTO dbo.PRODUCT_CATEGORIES (
         Code, Name,
         IsRawMaterial, IsFinishedGood, IsComponent, IsConsumable,
         RequiresNORM, RequiresCutting, IncludeInMRP,
         AllowPurchase, AllowSales,
         RawMaterialGroup, RawMaterialSubGrp,
         SortOrder, IsActive, CreatedDT, UpdatedDT
    )
    SELECT
        -- Auto-generate Code (5 char padded)
        RIGHT('00000' + CAST(
            (
                SELECT ISNULL(MAX(CAST(Code AS INT)), 0)
                FROM dbo.PRODUCT_CATEGORIES WITH (TABLOCKX)
            ) 
            + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
        AS VARCHAR(5)), 5),

        i.Name,
        i.IsRawMaterial, i.IsFinishedGood, i.IsComponent, i.IsConsumable,
        i.RequiresNORM, i.RequiresCutting, i.IncludeInMRP,
        i.AllowPurchase, i.AllowSales,
        i.RawMaterialGroup, i.RawMaterialSubGrp,
        i.SortOrder, i.IsActive, SYSDATETIME(), NULL
    FROM inserted i;
END;
GO
DISABLE TRIGGER [dbo].[TR_ProductCategories_AutoCode]
    ON [dbo].[PRODUCT_CATEGORIES];
GO
CREATE VIEW dbo.V_FIN_PL_FACT_MONTHLY
AS
WITH TxnAgg AS
(
    SELECT
          YEAR(t.TxnDate)  AS YearNum
        , MONTH(t.TxnDate) AS MonthNum
        , DATEFROMPARTS(YEAR(t.TxnDate), MONTH(t.TxnDate), 1) AS PeriodStartDate
        , pic.ItemID                       -- mapping to FIN_PL_ITEMS.ID
        , SUM(
              CASE 
                  WHEN t.Direction = 1 THEN t.Amount          -- Income
                  WHEN t.Direction = 2 THEN -1 * t.Amount     -- Expense
                  ELSE 0
              END
          ) AS AmountFact
    FROM dbo.FIN_TRANSACTIONS             AS t
    INNER JOIN dbo.FIN_PL_ITEM_CATEGORIES AS pic 
        ON pic.CategoryID = t.CategoryID
    GROUP BY
          YEAR(t.TxnDate)
        , MONTH(t.TxnDate)
        , DATEFROMPARTS(YEAR(t.TxnDate), MONTH(t.TxnDate), 1)
        , pic.ItemID
)
SELECT
      a.YearNum            AS [Year]
    , a.MonthNum           AS [Month]
    , a.PeriodStartDate
    , a.ItemID
    , pli.Name             AS ItemName
    , pli.SortOrder
    , a.AmountFact
FROM TxnAgg a
INNER JOIN dbo.FIN_PL_ITEMS pli
    ON pli.ID = a.ItemID;           -- REAL FK mapping
GO
CREATE VIEW [dbo].[V_FIN_PL_PLAN_VS_FACT_MONTHLY]
AS
WITH TxnAgg AS (
    SELECT 
        YEAR(t.TxnDate) AS YearNum, 
        MONTH(t.TxnDate) AS MonthNum, 
        DATEFROMPARTS(YEAR(t.TxnDate), MONTH(t.TxnDate), 1) AS PeriodStartDate, 
        pic.ItemID, 
        SUM(CASE 
            WHEN t.Direction = 1 THEN t.Amount 
            WHEN t.Direction = 2 THEN -1 * t.Amount 
            ELSE 0 
        END) AS AmountFact
    FROM dbo.FIN_TRANSACTIONS AS t 
    INNER JOIN dbo.FIN_PL_ITEM_CATEGORIES AS pic ON pic.CategoryID = t.CategoryID
    WHERE 
        -- Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real transactions
        ((t.SourceDocType != 'PurchaseInvoice' AND t.SourceDocType != 'SalesInvoice') 
         OR t.SourceDocType IS NULL 
         OR t.SourceDocType = '')
    GROUP BY 
        YEAR(t.TxnDate), 
        MONTH(t.TxnDate), 
        DATEFROMPARTS(YEAR(t.TxnDate), MONTH(t.TxnDate), 1), 
        pic.ItemID
)
SELECT 
    a.YearNum AS Year, 
    a.MonthNum AS Month, 
    a.PeriodStartDate, 
    a.ItemID, 
    pli.Name AS ItemName, 
    pli.SortOrder, 
    a.AmountFact
FROM TxnAgg AS a 
INNER JOIN dbo.FIN_PL_ITEMS AS pli ON pli.ID = a.ItemID
GO
CREATE VIEW dbo.V_TANNARX_ACTUAL_ADMIN_EXPENSES
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
SalesAgg AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M,
           SUM(CAST(sil.LineAmount AS decimal(18,4))) AS SumLineAmt,
           SUM(CAST(sil.Qty AS decimal(18,4))) AS SumQty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.AdministrativeExpenses,
           t.SalesPriceEXWWithoutVAT
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
)
SELECT tb.ProductID, tb.Year, tb.Month,
       CAST(
         CASE
           WHEN sa.SumQty > 0 AND tb.SalesPriceEXWWithoutVAT <> 0
             THEN tb.AdministrativeExpenses * ((sa.SumLineAmt / sa.SumQty) / tb.SalesPriceEXWWithoutVAT)
           ELSE tb.AdministrativeExpenses
         END
       AS decimal(38,6)) AS ActualAdministrativeExpenses
FROM TannarxBase tb
LEFT JOIN SalesAgg sa ON sa.ProductID = tb.ProductID AND sa.Y = tb.Year AND sa.M = tb.Month;
GO
CREATE VIEW dbo.V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT
AS
WITH InvoiceQty AS (
    SELECT sil.InvoiceID, SUM(CAST(sil.Qty AS decimal(18,4))) AS TotQty
    FROM dbo.SALES_INVOICE_LINES sil
    GROUP BY sil.InvoiceID
),
/* Har bir sotuv chizig'i: barcha chiquvchi yo'l varaqalari bo'yicha jami tashuv / chiziq miqdori */
FinishedProductFreightLine AS (
    SELECT sil.ProductID,
           YEAR(COALESCE(wb.IssueDate, wb.ActualDeliveryDate, wb.PlannedDeliveryDate, si.InvoiceDate)) AS Y,
           MONTH(COALESCE(wb.IssueDate, wb.ActualDeliveryDate, wb.PlannedDeliveryDate, si.InvoiceDate)) AS M,
           sil.ID AS LineID,
           MAX(CAST(sil.Qty AS decimal(18,4))) AS LineQty,
           SUM(
               CAST(ISNULL(wb.Amount, 0) AS decimal(18,4)) *
               CASE
                 WHEN si.TotalAmount IS NOT NULL AND si.TotalAmount <> 0
                   THEN CAST(sil.LineAmount AS decimal(18,4)) / CAST(si.TotalAmount AS decimal(18,4))
                 ELSE CAST(sil.Qty AS decimal(18,4)) / NULLIF(iq.TotQty, 0)
               END
           ) AS LineFreightAlloc
    FROM dbo.F_WAYBILLS wb
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = wb.SalesInvoiceID
    INNER JOIN dbo.SALES_INVOICE_LINES sil ON sil.InvoiceID = si.ID
    LEFT JOIN InvoiceQty iq ON iq.InvoiceID = sil.InvoiceID
    WHERE ISNULL(wb.IsDeleted, 0) = 0
      AND wb.SalesInvoiceID IS NOT NULL
      AND (wb.Direction = 1 OR wb.Direction IS NULL)
      AND si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID,
             YEAR(COALESCE(wb.IssueDate, wb.ActualDeliveryDate, wb.PlannedDeliveryDate, si.InvoiceDate)),
             MONTH(COALESCE(wb.IssueDate, wb.ActualDeliveryDate, wb.PlannedDeliveryDate, si.InvoiceDate)),
             sil.ID
),
LineFreight AS (
    SELECT ProductID, Y AS Year, M AS Month,
           CAST(SUM(LineFreightAlloc) / NULLIF(SUM(LineQty), 0) AS float) AS ActualFinishedProductTransportCost
    FROM FinishedProductFreightLine
    GROUP BY ProductID, Y, M
)
SELECT ProductID, Year, Month, ActualFinishedProductTransportCost
FROM LineFreight;
GO
CREATE VIEW dbo.V_TANNARX_ACTUAL_LABOR_COSTS
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
PayrollPieceMonth AS (
    SELECT h.Year AS Y, h.Month AS M,
           SUM(CAST(COALESCE(l.GrossSalary, l.NetSalary, 0) AS decimal(18,4))) AS TotalGross
    FROM dbo.HR_PAYROLL_LINES l
    INNER JOIN dbo.HR_PAYROLL_HEADERS h ON h.ID = l.PayrollID
    WHERE ISNULL(l.IsDeleted, 0) = 0
      AND ISNULL(h.IsDeleted, 0) = 0
      AND h.PayrollType = N'PieceRate'
    GROUP BY h.Year, h.Month
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.LaborCost
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
W AS (
    SELECT tb.ProductID, tb.Year, tb.Month,
           tb.LaborCost * tb.DenomSafe AS WLabor,
           pm.TotalGross,
           SUM(tb.LaborCost * tb.DenomSafe) OVER (PARTITION BY tb.Year, tb.Month) AS S_WLabor
    FROM TannarxBase tb
    LEFT JOIN PayrollPieceMonth pm ON pm.Y = tb.Year AND pm.M = tb.Month
)
SELECT w.ProductID, w.Year, w.Month,
       CAST(CASE WHEN w.S_WLabor > 0 AND w.TotalGross IS NOT NULL
            THEN (w.TotalGross * w.WLabor / w.S_WLabor) / NULLIF(x.DenomSafe, 0)
            END AS decimal(38,8)) AS ActualLaborCost
FROM W w
     INNER JOIN TannarxBase x ON x.ProductID = w.ProductID AND x.Year = w.Year AND x.Month = w.Month;
GO
CREATE VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_COSTS
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(CAST(pp.Total AS decimal(18,4))) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
InvLineSum AS (
    SELECT pip.InvoiceID, SUM(CAST(pip.TotalPrice AS decimal(18,4))) AS LineTotal
    FROM dbo.P_INVOICE_PRODUCTS pip
    GROUP BY pip.InvoiceID
),
/* Xarid invoysiga bog'langan FIN — mahsulot qatorlariga narx ulushi bo'yicha */
MatPurchaseByProductMonth AS (
    SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
           pip.ProductID,
           SUM(CAST(t.Amount AS decimal(18,4)) * (CAST(pip.TotalPrice AS decimal(18,4)) / ls.LineTotal)) AS MatAmt
    FROM dbo.FIN_TRANSACTIONS t
    INNER JOIN dbo.P_INVOICES pi ON pi.ID = t.SourceDocID
    INNER JOIN InvLineSum ls ON ls.InvoiceID = pi.ID AND ls.LineTotal > 0
    INNER JOIN dbo.P_INVOICE_PRODUCTS pip ON pip.InvoiceID = pi.ID
    WHERE ISNULL(t.IsDeleted, 0) = 0
      AND t.Direction = 2
      AND t.SourceDocType = N'PurchaseInvoice'
      AND ISNULL(pi.IsDeleted, 0) = 0
    GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate), pip.ProductID
),
/* Qo'lda Material kategoriyasi (xarid invoysiga bog'lanmagan) */
FinMaterialManualMonth AS (
    SELECT YEAR(t.TxnDate) AS Y, MONTH(t.TxnDate) AS M,
           SUM(CAST(t.Amount AS decimal(18,4))) AS TotalAmt
    FROM dbo.FIN_TRANSACTIONS t
    INNER JOIN dbo.FIN_CATEGORIES c ON c.ID = t.CategoryID
    WHERE ISNULL(t.IsDeleted, 0) = 0
      AND t.Direction = 2
      AND ISNULL(c.IsDeleted, 0) = 0
      AND (t.SourceDocType IS NULL OR t.SourceDocType <> N'PurchaseInvoice')
      AND (
            LOWER(LTRIM(RTRIM(c.Name))) = N'material'
         OR LOWER(c.Name) LIKE N'%material%'
      )
    GROUP BY YEAR(t.TxnDate), MONTH(t.TxnDate)
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS ActQty,
           CAST(t.MaterialPrice AS decimal(38, 12)) * COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), 0) AS PlanMatWeight
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
MonthMatPlanWeightAgg AS (
    SELECT tb.Year, tb.Month,
           SUM(CASE WHEN tb.ActQty > 0 THEN tb.PlanMatWeight ELSE CAST(0 AS decimal(38, 12)) END) AS S_PlanMat,
           SUM(CASE WHEN tb.ActQty > 0 THEN tb.ActQty ELSE CAST(0 AS decimal(38, 12)) END) AS S_ActQty
    FROM TannarxBase tb
    GROUP BY tb.Year, tb.Month
),
MatRow AS (
    SELECT tb.ProductID, tb.Year, tb.Month, tb.ActQty,
           ISNULL(mp.MatAmt, 0) AS MatPurchaseAmt,
           CAST(
             CASE
               WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_PlanMat, 0) > 0
                 THEN fmm.TotalAmt * tb.PlanMatWeight / mma.S_PlanMat
               WHEN ISNULL(fmm.TotalAmt, 0) > 0 AND ISNULL(mma.S_ActQty, 0) > 0
                 THEN fmm.TotalAmt * tb.ActQty / mma.S_ActQty
               ELSE CAST(0 AS decimal(38, 12))
             END AS decimal(38, 12)) AS ManualMatAlloc
    FROM TannarxBase tb
    LEFT JOIN MatPurchaseByProductMonth mp ON mp.ProductID = tb.ProductID AND mp.Y = tb.Year AND mp.M = tb.Month
    LEFT JOIN FinMaterialManualMonth fmm ON fmm.Y = tb.Year AND fmm.M = tb.Month
    LEFT JOIN MonthMatPlanWeightAgg mma ON mma.Year = tb.Year AND mma.Month = tb.Month
)
SELECT mr.ProductID, mr.Year, mr.Month,
       CAST(
         CASE
           WHEN mr.ActQty > 0 AND (mr.MatPurchaseAmt + mr.ManualMatAlloc) > 0
             THEN (mr.MatPurchaseAmt + mr.ManualMatAlloc) / mr.ActQty
         END
       AS decimal(38, 6)) AS ActualMaterialPrice
FROM MatRow mr;
GO
CREATE VIEW dbo.V_TANNARX_ACTUAL_MATERIAL_TRANSPORT
AS
WITH ProdQty AS (
    SELECT pp.ProductID, YEAR(pp.[Date]) AS Y, MONTH(pp.[Date]) AS M, SUM(pp.Total) AS Qty
    FROM dbo.PRODUCT_PRODUCTION pp
    WHERE ISNULL(pp.IsDeleted, 0) = 0
    GROUP BY pp.ProductID, YEAR(pp.[Date]), MONTH(pp.[Date])
),
SalesQty AS (
    SELECT sil.ProductID, YEAR(si.InvoiceDate) AS Y, MONTH(si.InvoiceDate) AS M, SUM(CAST(sil.Qty AS decimal(18,4))) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    INNER JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.StatusCode NOT IN (N'CANCELED', N'DRAFT')
    GROUP BY sil.ProductID, YEAR(si.InvoiceDate), MONTH(si.InvoiceDate)
),
PurchFreightMonth AS (
    SELECT YEAR(pi.InvoiceDate) AS Y, MONTH(pi.InvoiceDate) AS M,
           SUM(CAST(ISNULL(wb.Amount, 0) AS float)) AS TotalFreight
    FROM dbo.F_WAYBILLS wb
    INNER JOIN dbo.P_INVOICES pi ON pi.ID = wb.InvoiceID
    WHERE ISNULL(wb.IsDeleted, 0) = 0
      AND wb.InvoiceID IS NOT NULL
      AND ISNULL(pi.IsDeleted, 0) = 0
    GROUP BY YEAR(pi.InvoiceDate), MONTH(pi.InvoiceDate)
),
TannarxBase AS (
    SELECT t.ProductID, t.Year, t.Month,
           COALESCE(NULLIF(pq.Qty, 0), NULLIF(sq.Qty, 0), CAST(1 AS decimal(18,4))) AS DenomSafe,
           t.MaterialTransportCost
    FROM dbo.TANNARX t
    LEFT JOIN ProdQty pq ON pq.ProductID = t.ProductID AND pq.Y = t.Year AND pq.M = t.Month
    LEFT JOIN SalesQty sq ON sq.ProductID = t.ProductID AND sq.Y = t.Year AND sq.M = t.Month
    WHERE t.IsDeleted = 0
),
W AS (
    SELECT tb.ProductID, tb.Year, tb.Month,
           tb.MaterialTransportCost * tb.DenomSafe AS WTr,
           pf.TotalFreight,
           SUM(tb.MaterialTransportCost * tb.DenomSafe) OVER (PARTITION BY tb.Year, tb.Month) AS S_WTr
    FROM TannarxBase tb
    LEFT JOIN PurchFreightMonth pf ON pf.Y = tb.Year AND pf.M = tb.Month
)
SELECT w.ProductID, w.Year, w.Month,
       CAST(CASE WHEN w.S_WTr > 0 AND w.TotalFreight IS NOT NULL
            THEN (w.TotalFreight * w.WTr / w.S_WTr) / NULLIF(x.DenomSafe, 0)
            END AS float) AS ActualMaterialTransportCost
FROM W w
     INNER JOIN TannarxBase x ON x.ProductID = w.ProductID AND x.Year = w.Year AND x.Month = w.Month;
GO
CREATE PROCEDURE dbo.sp_CheckDlocCapacity
    @LocationID INT,
    @IncomingQty DECIMAL(18,4)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaxCapacity DECIMAL(18,4), @CurrentQty DECIMAL(18,4);

    SELECT @MaxCapacity = MaxCapacity
    FROM dbo.WAREHOUSE_LOCATIONS
    WHERE ID = @LocationID AND IsDeleted = 0 AND IsActive = 1;

    IF @MaxCapacity IS NULL
    BEGIN
        RAISERROR('DLOC_REQUIRED_OR_INACTIVE', 16, 1);
        RETURN;
    END

    SELECT @CurrentQty = ISNULL(SUM(QtyAvailable), 0)
    FROM dbo.LOT_LOCATIONS
    WHERE LocationID = @LocationID;

    IF (@CurrentQty + @IncomingQty) > @MaxCapacity
    BEGIN
        RAISERROR('DLOC_CAPACITY_EXCEEDED', 16, 1);
        RETURN;
    END
END;
GO
CREATE PROCEDURE dbo.sp_Fin_AddTransaction
(
    @TxnDate          DATE,
    @AccountID        INT,
    @CategoryID       INT,
    @Direction        TINYINT,          -- 1=Kirim, 2=Chiqim
    @Amount           DECIMAL(18,2),
    @CurrencyID       INT = 1,

    @Qty              DECIMAL(18,3) = NULL,
    @PricePerUnit     DECIMAL(18,2) = NULL,

    @CounterpartyType TINYINT = NULL,   -- 1=Customer,2=Supplier,3=Employee,4=Other
    @CounterpartyID   INT = NULL,
    @CounterpartyName NVARCHAR(200) = NULL,

    @Purpose          NVARCHAR(200) = NULL,
    @SourceDocType    NVARCHAR(50)  = NULL,
    @SourceDocID      INT           = NULL,
    @ContractNo       NVARCHAR(50)  = NULL,
    @Comment          NVARCHAR(500) = NULL,

    @CreatedByUserID  INT,

    @NewID            INT OUTPUT        -- chiqishda Transaction ID
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Amount <= 0
    BEGIN
        RAISERROR('Amount must be > 0', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.FIN_ACCOUNTS WHERE ID = @AccountID AND IsActive = 1)
    BEGIN
        RAISERROR('Invalid AccountID', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.FIN_CATEGORIES WHERE ID = @CategoryID AND IsActive = 1)
    BEGIN
        RAISERROR('Invalid CategoryID', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE ID = @CreatedByUserID)
    BEGIN
        RAISERROR('Invalid CreatedByUserID', 16, 1);
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.FIN_TRANSACTIONS
        (
            TxnDate, AccountID, CategoryID, Direction, Amount, CurrencyID,
            Qty, PricePerUnit,
            CounterpartyType, CounterpartyID, CounterpartyName,
            Purpose, SourceDocType, SourceDocID, ContractNo, Comment,
            CreatedByUserID
        )
        VALUES
        (
            @TxnDate, @AccountID, @CategoryID, @Direction, @Amount, @CurrencyID,
            @Qty, @PricePerUnit,
            @CounterpartyType, @CounterpartyID, @CounterpartyName,
            @Purpose, @SourceDocType, @SourceDocID, @ContractNo, @Comment,
            @CreatedByUserID
        );

        SET @NewID = SCOPE_IDENTITY();

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END;
GO
CREATE PROCEDURE [dbo].[sp_Fin_CashSummary]
(
    @From DATE,
    @To   DATE
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @From IS NULL OR @To IS NULL OR @From > @To
    BEGIN
        RAISERROR('@From and @To must be valid and @From <= @To',16,1);
        RETURN;
    END;

    ;WITH TxnBefore AS
    (
        SELECT
            AccountID,
            SUM(CASE WHEN Direction = 1 THEN Amount ELSE -Amount END) AS BalanceBefore
        FROM dbo.FIN_TRANSACTIONS
        WHERE TxnDate < @From
            -- Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real transactions
            AND ((SourceDocType != 'PurchaseInvoice' AND SourceDocType != 'SalesInvoice') 
                 OR SourceDocType IS NULL 
                 OR SourceDocType = '')
        GROUP BY AccountID
    ),
    TxnPeriod AS
    (
        SELECT
            AccountID,
            SUM(CASE WHEN Direction = 1 THEN Amount ELSE 0 END) AS Income,
            SUM(CASE WHEN Direction = 2 THEN Amount ELSE 0 END) AS Expense,
            SUM(CASE WHEN Direction = 1 THEN Amount ELSE -Amount END) AS NetChange
        FROM dbo.FIN_TRANSACTIONS
        WHERE TxnDate BETWEEN @From AND @To
            -- Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real transactions
            AND ((SourceDocType != 'PurchaseInvoice' AND SourceDocType != 'SalesInvoice') 
                 OR SourceDocType IS NULL 
                 OR SourceDocType = '')
        GROUP BY AccountID
    )
    SELECT
        a.ID          AS AccountID,
        a.Code        AS AccountCode,
        a.Name        AS AccountName,
        a.OwnerName   AS OwnerName,
        ISNULL(b.BalanceBefore,0) AS OpeningBalance,
        ISNULL(p.Income,0)        AS Income,
        ISNULL(p.Expense,0)       AS Expense,
        ISNULL(b.BalanceBefore,0) + ISNULL(p.NetChange,0) AS ClosingBalance
    FROM dbo.FIN_ACCOUNTS a
    LEFT JOIN TxnBefore b ON b.AccountID = a.ID
    LEFT JOIN TxnPeriod p ON p.AccountID = a.ID
    WHERE a.IsActive = 1
    ORDER BY a.Code;
END;
GO
CREATE PROCEDURE dbo.sp_FIN_ShareholderValue_AsOf
(
      @AsOfDate date
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SnapshotDate date;

    SELECT @SnapshotDate = MAX(SnapshotDate)
    FROM dbo.FIN_BALANCE_SNAPSHOTS
    WHERE SnapshotDate <= @AsOfDate;

    IF @SnapshotDate IS NULL
    BEGIN
        DECLARE @Msg nvarchar(200);

        SET @Msg = N'No balance snapshot exists on or before the requested date ('
                 + CONVERT(varchar(10), @AsOfDate, 120)
                 + N').';

        RAISERROR(@Msg, 16, 1);
        RETURN;
    END;

    SELECT
          SnapshotDate
        , ShareholderID
        , ShareholderName
        , FromDate
        , ToDate
        , EffectiveToDate
        , SharePercent
        , TotalAssets
        , TotalLiabilities
        , EquityTotal
        , ShareValue
    FROM dbo.V_FIN_ShareholderValue
    WHERE SnapshotDate = @SnapshotDate;
END;
GO
CREATE PROCEDURE dbo.sp_GenerateMaterialRequirement_FromSalesOrders
    @SalesOrderIDs dbo.IntList READONLY   -- E'tibor: AS YO'Q!
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ReqID INT;

    -- 1. Header
    INSERT INTO MATERIAL_REQUIREMENTS (CreatedDT)
    VALUES (GETDATE());

    SET @ReqID = SCOPE_IDENTITY();

    -------------------------------------------------
    -- shu joyda shuni faraz qilaman:
    -- S_ORDERS da ID (int PK) va S_ORDER_PRODUCTS da OrderID (FK) bor.
    -- Agar senda OrderNo PK bo'lsa, keyin shu joyni moslab beraman.
    -------------------------------------------------

    ;WITH so AS (
        SELECT so.ID AS SalesOrderID, sop.ProductID, sop.Amount AS Qty
        FROM S_ORDERS so
        JOIN S_ORDER_PRODUCTS sop ON sop.OrderID = so.ID
        JOIN @SalesOrderIDs ids   ON ids.ID = so.ID
    ),
    nv AS (
        -- har bir Product uchun oxirgi NormID
        SELECT ProductID,
               MAX(ID) AS NormID
        FROM NORMVERSIONS
        GROUP BY ProductID
    ),
    nl AS (
        -- NormLines + SalesOrders
        SELECT
            so.SalesOrderID,
            so.ProductID,
            so.Qty,
            nl.MaterialID,
            nl.QtyPerUnit
        FROM so
        JOIN nv ON nv.ProductID = so.ProductID
        JOIN NORMLINES nl ON nl.NormID = nv.NormID
    ),
    req AS (
        -- umumlashtirilgan material talabi
        SELECT
            MaterialID,
            SUM(Qty * QtyPerUnit) AS RequiredQty
        FROM nl
        GROUP BY MaterialID
    )
    INSERT INTO MATERIAL_REQUIREMENT_LINES
        (RequirementID, MaterialID, RequiredQty, UnitID, OnHandQty, NetToBuyQty)
    SELECT
        @ReqID,
        r.MaterialID,
        r.RequiredQty,
        p.UnitID,
        ISNULL(s.StockQty, 0) AS OnHandQty,
        CASE 
            WHEN r.RequiredQty <= ISNULL(s.StockQty,0) THEN 0
            ELSE r.RequiredQty - ISNULL(s.StockQty,0)
        END AS NetToBuyQty
    FROM req r
    JOIN PRODUCTS p ON p.ID = r.MaterialID
    OUTER APPLY (
        SELECT SUM(Amount) AS StockQty
        FROM PRODUCT_STOCKS s
        WHERE s.ProductID = r.MaterialID
    ) s;
END;
GO
CREATE PROCEDURE sp_Norm_PreviewCutMaterial
    @ProductID INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH c AS (
        SELECT
            MaterialID,
            SUM(CutLengthMm * PiecesPerProduct) AS TotalCutMm
        FROM NORM_INPUT_LINES
        WHERE ProductID = @ProductID
        GROUP BY MaterialID
    )
    SELECT
        c.MaterialID,
        p.Name        AS MaterialName,
        p.StandardLengthMm     AS StockLenMm,
        c.TotalCutMm,
        CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm)        AS BoardsNeeded,
        CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm AS TotalRawMm,
        CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm - c.TotalCutMm AS WasteMm,
        CAST(
          (CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm - c.TotalCutMm)
          * 100.0 /
          NULLIF(CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm,0)
        AS DECIMAL(5,2)) AS WastePercent
    FROM c
    JOIN PRODUCTS p ON p.ID = c.MaterialID;
END;
GO
CREATE PROCEDURE sp_Norm_SaveCutMaterials
    @ProductID INT,
    @EffectiveFrom DATE,
    @Comment NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NormID INT;

    -- 1. NormVersion ochish
    INSERT INTO NORMVERSIONS (ProductID, EffectiveFrom, Comment)
    VALUES (@ProductID, @EffectiveFrom, @Comment);

    SET @NormID = SCOPE_IDENTITY();

    -- 2. Yog'och materiallar bo'yicha jamlash va NORMLINES ga yozish
    ;WITH c AS (
        SELECT
            n.MaterialID,
            SUM(n.CutLengthMm * n.PiecesPerProduct) AS TotalCutMm
        FROM NORM_INPUT_LINES n
        WHERE n.ProductID = @ProductID
        GROUP BY n.MaterialID
    )
    INSERT INTO NORMLINES (NormID, MaterialID, QtyPerUnit, WastePercent)
    SELECT
        @NormID,
        c.MaterialID,
        -- Norma: brutto sarf (metrda) – misol uchun:
        (CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm) / 1000.0,
        CAST(
          (CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm - c.TotalCutMm)
          * 100.0 /
          NULLIF(CEILING(1.0 * c.TotalCutMm / p.StandardLengthMm) * p.StandardLengthMm,0)
        AS DECIMAL(5,2)) AS WastePercent
    FROM c
    JOIN PRODUCTS p ON p.ID = c.MaterialID;

    -- 3. Xohlasang, draft inputlarni shu NormID bilan bog'lab qo'yish mumkin
    UPDATE NORM_INPUT_LINES
        SET Month = CONVERT(CHAR(7), @EffectiveFrom, 120) -- 'yyyy-MM'
    WHERE ProductID = @ProductID;
END;
GO
CREATE   PROCEDURE dbo.sp_SOrder_RecalcDeliveryShippedFromInvoices
    @SOrderID INT
AS
BEGIN
    SET NOCOUNT ON;

    -------------------------------------------------------------------
    -- 1. Bu order bo‘yicha mavjud productlar ro‘yxati
    -------------------------------------------------------------------
    DECLARE @ProductID INT;

    DECLARE curProducts CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT ProductID
        FROM dbo.SORDER_DELIVERY_SCHEDULE
        WHERE SOrderID = @SOrderID;

    OPEN curProducts;
    FETCH NEXT FROM curProducts INTO @ProductID;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        ----------------------------------------------------------------
        -- 2. Shu product uchun barcha schedule satrlaridagi QtyShipped ni 0 qilamiz
        ----------------------------------------------------------------
        UPDATE d
        SET d.QtyShipped = 0
        FROM dbo.SORDER_DELIVERY_SCHEDULE d
        WHERE d.SOrderID = @SOrderID
          AND d.ProductID = @ProductID;

        ----------------------------------------------------------------
        -- 3. Shu product bo‘yicha barcha invoice line’larni ketma-ket olaylik
        ----------------------------------------------------------------
        DECLARE @InvoiceLineID BIGINT;
        DECLARE @QtyRemaining  DECIMAL(18,3);

        DECLARE curInvoices CURSOR LOCAL FAST_FORWARD FOR
            SELECT l.ID,
                   l.Qty
            FROM dbo.SALES_INVOICE_LINES l
            INNER JOIN dbo.SALES_INVOICES i
                ON i.ID = l.InvoiceID
            WHERE i.OrderID   = @SOrderID
              AND l.ProductID = @ProductID
            ORDER BY i.InvoiceDate, i.ID, l.ID;   -- vaqt bo‘yicha ketma-ket

        OPEN curInvoices;
        FETCH NEXT FROM curInvoices INTO @InvoiceLineID, @QtyRemaining;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            WHILE @QtyRemaining > 0
            BEGIN
                ----------------------------------------------------------------
                -- 3.1. Keyingi "bo'sh joy" bo‘lgan schedule satrini topamiz
                ----------------------------------------------------------------
                ;WITH NextSched AS (
                    SELECT TOP (1) 
                           ID, 
                           QtyPlanned, 
                           QtyShipped
                    FROM dbo.SORDER_DELIVERY_SCHEDULE
                    WHERE SOrderID   = @SOrderID
                      AND ProductID  = @ProductID
                      AND QtyPlanned > QtyShipped
                    ORDER BY DeliveryDate, ID
                )
                UPDATE d
                SET d.QtyShipped = d.QtyShipped + 
                                   CASE 
                                       WHEN @QtyRemaining >= (ISNULL(n.QtyPlanned,0) - ISNULL(n.QtyShipped,0))
                                           THEN (ISNULL(n.QtyPlanned,0) - ISNULL(n.QtyShipped,0))
                                           ELSE @QtyRemaining
                                   END,
                    @QtyRemaining  = @QtyRemaining - 
                                   CASE 
                                       WHEN @QtyRemaining >= (ISNULL(n.QtyPlanned,0) - ISNULL(n.QtyShipped,0))
                                           THEN (ISNULL(n.QtyPlanned,0) - ISNULL(n.QtyShipped,0))
                                           ELSE @QtyRemaining
                                   END
                FROM dbo.SORDER_DELIVERY_SCHEDULE d
                JOIN NextSched n ON n.ID = d.ID;

                -- Agar endi bo'sh schedule qolmagan bo'lsa, chiqib ketamiz
                IF @@ROWCOUNT = 0 BREAK;
            END;

            -- Keyingi invoice line ga o'tamiz
            FETCH NEXT FROM curInvoices INTO @InvoiceLineID, @QtyRemaining;
        END;

        CLOSE curInvoices;
        DEALLOCATE curInvoices;

        -- Keyingi productga o‘tamiz
        FETCH NEXT FROM curProducts INTO @ProductID;
    END;

    CLOSE curProducts;
    DEALLOCATE curProducts;
END;
GO
ALTER TABLE [dbo].[ADM_COMPANY_ORDERS] WITH CHECK CHECK CONSTRAINT [FK_ADM_COMPANY_ORDERS_Document];

ALTER TABLE [dbo].[APP_TASKS] WITH CHECK CHECK CONSTRAINT [FK_APP_TASKS_PROJECT];

ALTER TABLE [dbo].[APP_TASKS] WITH CHECK CHECK CONSTRAINT [FK_APP_TASKS_STATUS];

ALTER TABLE [dbo].[APP_TASKS] WITH CHECK CHECK CONSTRAINT [FK_APP_TASKS_PRIORITY];

ALTER TABLE [dbo].[BOM_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_BOM_HEADER_PRODUCTS];

ALTER TABLE [dbo].[BOM_LINES] WITH CHECK CHECK CONSTRAINT [FK_BOM_LINES_BOMID_BOM_HEADERS];

ALTER TABLE [dbo].[BOM_LINES] WITH CHECK CHECK CONSTRAINT [FK_BOM_LINES_UNITS];

ALTER TABLE [dbo].[BOM_LINES] WITH CHECK CHECK CONSTRAINT [FK_BOM_LINES_ComponentID_PRODUCTS];

ALTER TABLE [dbo].[COM_OFFICIAL_LETTERS] WITH CHECK CHECK CONSTRAINT [FK_COM_OFFICIAL_LETTERS_Document];

ALTER TABLE [dbo].[COMPANY_LICENSES] WITH CHECK CHECK CONSTRAINT [FK_COMPANY_LICENSES_COMPANIES];

ALTER TABLE [dbo].[COOKIES_CONFIRMATION] WITH CHECK CHECK CONSTRAINT [FK_COOKIES_CONFIRMATION_USERS];

ALTER TABLE [dbo].[CURRENCY_RATES] WITH CHECK CHECK CONSTRAINT [FK_CURRENCY_RATES_CURRENCIES];

ALTER TABLE [dbo].[DAMAGED_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_DAMAGED_PRODUCTS_PRODUCTS];

ALTER TABLE [dbo].[DAMAGED_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_DAMAGED_PRODUCTS_DEFECT_TYPES];

ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH CHECK CHECK CONSTRAINT [FK_DOC_DOCUMENTS_Template];

ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH CHECK CHECK CONSTRAINT [FK_DOC_DOCUMENTS_OrgUnit];

ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH CHECK CHECK CONSTRAINT [FK_DOC_DOCUMENTS_Status];

ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH CHECK CHECK CONSTRAINT [FK_DOC_DOCUMENTS_Position];

ALTER TABLE [dbo].[DOC_DOCUMENTS] WITH CHECK CHECK CONSTRAINT [FK_DOC_DOCUMENTS_DocType];

ALTER TABLE [dbo].[DOC_FILES] WITH CHECK CHECK CONSTRAINT [FK_DOC_FILES_Document];

ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_EPA_WHL];

ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_EPA_IL];

ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_EPA_EP];

ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_UNITS];

ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_PROD_WRHS_EXPENSES];

ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_EP_IL];

ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_EXPENSE_PRODUCTS_PRODUCTS];

ALTER TABLE [dbo].[EXPENSE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_EP_WHL];

ALTER TABLE [dbo].[F_CONTRACT_DOCS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACT_DOCS_F_CONTRACTS];

ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACT_TARIFFS_CONTRACTS];

ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACT_TARIFFS_TRANSPORT_TYPES];

ALTER TABLE [dbo].[F_CONTRACT_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACT_TARIFFS_CURRENCIES];

ALTER TABLE [dbo].[F_CONTRACTS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACTS_CURRENCIES];

ALTER TABLE [dbo].[F_CONTRACTS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACTS_COMPANIES];

ALTER TABLE [dbo].[F_CONTRACTS] WITH CHECK CHECK CONSTRAINT [FK_F_CONTRACTS_FORWARDERS];

ALTER TABLE [dbo].[F_TRANSPORT_TYPES] WITH CHECK CHECK CONSTRAINT [FK_F_TRANSPORT_TYPES_UNITS];

ALTER TABLE [dbo].[F_WAYBILL_DOCS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILL_DOCS_F_WAYBILLS];

ALTER TABLE [dbo].[F_WAYBILL_PAYMENT_SPLITS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILL_PAYMENT_SPLITS_CURRENCIES];

ALTER TABLE [dbo].[F_WAYBILL_PAYMENT_SPLITS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILL_PAYMENT_SPLITS_WAYBILLS];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_P_INVOICES];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_P_ORDERS];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_TTNK];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_S_ORDERS];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_SALES_INVOICES];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_F_CONTRACTS];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_F_TRANSPORT_TYPES];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_FORWARDERS];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_CURRENCIES];

ALTER TABLE [dbo].[F_WAYBILLS] WITH CHECK CHECK CONSTRAINT [FK_F_WAYBILLS_F_CONTRACT_TARIFFS];

ALTER TABLE [dbo].[FIFO_POLICY] WITH CHECK CHECK CONSTRAINT [FK_FP_WAREHOUSE];

ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_ACCOUNTS_CREATEDBY];

ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_ACCOUNTS_BalanceCategory];

ALTER TABLE [dbo].[FIN_ACCOUNTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_ACCOUNTS_CURRENCY];

ALTER TABLE [dbo].[FIN_BALANCE_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_CATEGORIES_PARENT];

ALTER TABLE [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_CATEGORY_CATEGORIES_BalanceCategory];

ALTER TABLE [dbo].[FIN_BALANCE_CATEGORY_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_CATEGORY_CATEGORIES_Category];

ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CURRENCY];

ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CREATEDBY];

ALTER TABLE [dbo].[FIN_BALANCE_SNAPSHOTS] WITH CHECK CHECK CONSTRAINT [FK_FIN_BALANCE_SNAPSHOTS_CATEGORY];

ALTER TABLE [dbo].[FIN_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_CATEGORIES_PARENT];

ALTER TABLE [dbo].[FIN_PL_ITEM_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_PL_ITEM_CATEGORIES_CATEGORY];

ALTER TABLE [dbo].[FIN_PL_ITEM_CATEGORIES] WITH CHECK CHECK CONSTRAINT [FK_FIN_PL_ITEM_CATEGORIES_ITEM];

ALTER TABLE [dbo].[FIN_PL_ITEMS] WITH CHECK CHECK CONSTRAINT [FK_FIN_PL_ITEMS_PARENT];

ALTER TABLE [dbo].[FIN_PL_PLANS] WITH CHECK CHECK CONSTRAINT [FK_FIN_PL_PLANS_ITEM];

ALTER TABLE [dbo].[FIN_PL_PLANS] WITH CHECK CHECK CONSTRAINT [FK_FIN_PL_PLANS_CREATEDBY];

ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH CHECK CHECK CONSTRAINT [FK_FIN_TRANSACTIONS_CURRENCY];

ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH CHECK CHECK CONSTRAINT [FK_FIN_TRANSACTIONS_ACCOUNT];

ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH CHECK CHECK CONSTRAINT [FK_FIN_TRANSACTIONS_CATEGORY];

ALTER TABLE [dbo].[FIN_TRANSACTIONS] WITH CHECK CHECK CONSTRAINT [FK_FIN_TRANSACTIONS_CREATEDBY];

ALTER TABLE [dbo].[HR_EMPLOYEE_ALLOWANCES] WITH CHECK CHECK CONSTRAINT [FK_HR_EMP_ALLOW_EMP];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_FROM_POSITIONS];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_CREATED_BY];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_TO_ORG_UNITS];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_EMPLOYEES];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_TO_POSITIONS];

ALTER TABLE [dbo].[HR_EMPLOYEE_HISTORY] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEE_HISTORY_FROM_ORG_UNITS];

ALTER TABLE [dbo].[HR_EMPLOYEES] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEES_HR_ORG_UNITS];

ALTER TABLE [dbo].[HR_EMPLOYEES] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEES_HR_SALARY_GRADES];

ALTER TABLE [dbo].[HR_EMPLOYEES] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEES_USERS];

ALTER TABLE [dbo].[HR_EMPLOYEES] WITH CHECK CHECK CONSTRAINT [FK_HR_EMPLOYEES_HR_POSITIONS];

ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH CHECK CHECK CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_OrgUnit];

ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH CHECK CHECK CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_Document];

ALTER TABLE [dbo].[HR_JOB_DESCRIPTIONS] WITH CHECK CHECK CONSTRAINT [FK_HR_JOB_DESCRIPTIONS_Position];

ALTER TABLE [dbo].[HR_ORG_UNITS] WITH CHECK CHECK CONSTRAINT [FK_HR_ORG_UNITS_Parent];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_HEADERS_USERS_Created];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_HEADERS_HR_TIMESHEET_HEADERS];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_HEADERS_USERS_Approved];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_HEADERS_HR_ORG_UNITS];

ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_LINES_HR_PAYROLL_HEADERS];

ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_LINES_CURRENCIES];

ALTER TABLE [dbo].[HR_PAYROLL_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_PAYROLL_LINES_HR_EMPLOYEES];

ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_OPERATIONS_ModifiedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_OPERATIONS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_OPERATIONS_CreatedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_PRODUCTS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_ModifiedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_CURRENCIES];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_POSITIONS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_CreatedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_OPERATIONS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_ORG_UNITS];

ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_APPROVED_BY];

ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_MODIFIED_BY];

ALTER TABLE [dbo].[HR_PIECE_RATE_TIMESHEET_APPROVALS] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_TIMESHEET_APPROVALS_CREATED_BY];

ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_PRODUCTS];

ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_OPERATIONS];

ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_ModifiedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_CreatedByUserID_USERS];

ALTER TABLE [dbo].[HR_PIECE_RATE_WORK_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_HR_PIECE_RATE_WORK_ENTRIES_EMPLOYEES];

ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH CHECK CHECK CONSTRAINT [FK_HR_STAFFING_PLAN_Position];

ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH CHECK CHECK CONSTRAINT [FK_HR_STAFFING_PLAN_Grade];

ALTER TABLE [dbo].[HR_STAFFING_PLAN] WITH CHECK CHECK CONSTRAINT [FK_HR_STAFFING_PLAN_OrgUnit];

ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_HEADERS_USERS_Approved];

ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_HEADERS_USERS_Created];

ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_HEADERS_HR_ORG_UNITS];

ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_LINES_HR_EMPLOYEES];

ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_LINES_HR_TIMESHEET_HEADERS];

ALTER TABLE [dbo].[HR_TIMESHEET_LINES] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_LINES_PRODUCTS];

ALTER TABLE [dbo].[HR_TIMESHEET_SETTINGS] WITH CHECK CHECK CONSTRAINT [FK_HR_TIMESHEET_SETTINGS_HEADER];

ALTER TABLE [dbo].[INCOME_LOTS] WITH CHECK CHECK CONSTRAINT [FK_IL_INCOME];

ALTER TABLE [dbo].[INCOME_LOTS] WITH CHECK CHECK CONSTRAINT [FK_IL_INCOME_PRODUCTS];

ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_INCOME_PRODS_PROD_WRHS_INCOMES];

ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_INCOME_PRODUCTS_PRODUCTS];

ALTER TABLE [dbo].[INCOME_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_INCOME_PRODUCTS_UNITS];

ALTER TABLE [dbo].[INVENTORY_ADJUSTMENTS] WITH CHECK CHECK CONSTRAINT [FK_INVENTORY_ADJUSTMENTS_WAREHOUSES];

ALTER TABLE [dbo].[JOB_DESC] WITH CHECK CHECK CONSTRAINT [FK_JOB_DESC_DOC_DOCUMENTS];

ALTER TABLE [dbo].[JOB_DESC] WITH CHECK CHECK CONSTRAINT [FK_JOB_DESC_HR_JOB_DESCRIPTIONS];

ALTER TABLE [dbo].[LOT_LOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_LL_IL];

ALTER TABLE [dbo].[LOT_LOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_LL_WHL];

ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH CHECK CHECK CONSTRAINT [FK_NORM_INPUT_LINES_CreatedByUserID_USERS];

ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH CHECK CHECK CONSTRAINT [FK_NORM_INPUT_LINES_MaterialID_PRODUCTS];

ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH CHECK CHECK CONSTRAINT [FK_NORM_INPUT_LINES_ProductID_PRODUCTS];

ALTER TABLE [dbo].[NORM_INPUT_LINES] WITH CHECK CHECK CONSTRAINT [FK_NORM_INPUT_LINES_UNITS];

ALTER TABLE [dbo].[NORMLINES] WITH CHECK CHECK CONSTRAINT [FK_NORMLINES_UNITS];

ALTER TABLE [dbo].[NORMLINES] WITH CHECK CHECK CONSTRAINT [FK_NORMLINES_MaterialID_PRODUCTS];

ALTER TABLE [dbo].[NORMLINES] WITH CHECK CHECK CONSTRAINT [FK_NORMLINES_NormID_NORMVERSIONS];

ALTER TABLE [dbo].[NORMVERSIONS] WITH CHECK CHECK CONSTRAINT [FK_NORMVERSION_PRODUCTS];

ALTER TABLE [dbo].[P_CONTRACT_DOCS] WITH CHECK CHECK CONSTRAINT [FK_P_CONTRACT_DOCS_P_CONTRACTS];

ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_CONTRACT_PRODUCTS_UNITS];

ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_CONTRACT_PRODUCTS_P_CONTRACTS];

ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_CONTRACT_PRODUCTS_HSCODES];

ALTER TABLE [dbo].[P_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_CONTRACT_PRODUCTS_ID_PRODUCTS];

ALTER TABLE [dbo].[P_INVOICE_DOCS] WITH CHECK CHECK CONSTRAINT [FK_P_INVOICE_DOCS_P_INVOICES];

ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_INVOICE_PRODUCTS_P_INVOICES];

ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_INVOICE_PRODUCTS_ID_PRODUCTS];

ALTER TABLE [dbo].[P_INVOICE_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_INVOICE_PRODUCTS_UNITS];

ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_ORDER_PRODUCTS_ID_PRODUCTS];

ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_ORDER_PRODUCTS_UNITS];

ALTER TABLE [dbo].[P_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_ORDER_PRODUCTS_P_ORDERS];

ALTER TABLE [dbo].[P_REQUISITION_APPROVALS] WITH CHECK CHECK CONSTRAINT [FK_P_REQUISITION_APPROVALS_REQUISITION];

ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_REQUISITION_PRODUCTS_REQUISITION];

ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_REQUISITION_PRODUCTS_UNIT];

ALTER TABLE [dbo].[P_REQUISITION_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_P_REQUISITION_PRODUCTS_PRODUCT];

ALTER TABLE [dbo].[PERMISSIONS] WITH CHECK CHECK CONSTRAINT [FK_PERMISSIONS_PERMISSIONMODULES];

ALTER TABLE [dbo].[PERMISSIONS] WITH CHECK CHECK CONSTRAINT [FK_PERMISSIONS_ROLES];

ALTER TABLE [dbo].[PRODUCT_PRODUCTION] WITH CHECK CHECK CONSTRAINT [FK_PRODUCT_PRODUCTION_SHOPS];

ALTER TABLE [dbo].[PRODUCT_PRODUCTION] WITH CHECK CHECK CONSTRAINT [FK_PRODUCT_PRODUCTION_PRODUCTS];

ALTER TABLE [dbo].[PRODUCTIMAGES] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTIMAGES_PARTS];

ALTER TABLE [dbo].[PRODUCTPLANS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTPLAN_S_ORDERS];

ALTER TABLE [dbo].[PRODUCTPLANS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTPLANS_PRODUCTS];

ALTER TABLE [dbo].[PRODUCTPLANS_DAILY] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTPLANS_DAILY_PRODUCTPLANS];

ALTER TABLE [dbo].[PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTS_UNITS];

ALTER TABLE [dbo].[PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTS_SHOPS];

ALTER TABLE [dbo].[PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTS_HSCODES];

ALTER TABLE [dbo].[PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTS_CATEGORY];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCTPLANS_DAILY];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCTS];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_DEFECT_TYPES];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_PRODUCT_PRODUCTION];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_S_ORDERS];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_SORDER_DELIVERY_SCHEDULE];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_USERS_ApprovedBy];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [FK_QUALITY_INSPECTION_USERS_InspectedBy];

ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_CONTRACT_PRODUCTS_S_CONTRACTS];

ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_CONTRACT_PRODUCTS_UNITS];

ALTER TABLE [dbo].[S_CONTRACT_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_CONTRACT_PRODUCTS_PRODUCTS];

ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_ORDER_PRODUCTS_S_ORDERS];

ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_ORDER_PRODUCTS_UNITS];

ALTER TABLE [dbo].[S_ORDER_PRODUCTS] WITH CHECK CHECK CONSTRAINT [FK_S_ORDER_PRODUCTS_PRODUCTS];

ALTER TABLE [dbo].[SALES_INVOICE_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICE_LINES_Product];

ALTER TABLE [dbo].[SALES_INVOICE_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICE_LINES_Invoice];

ALTER TABLE [dbo].[SALES_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICES_CONTRACT];

ALTER TABLE [dbo].[SALES_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICES_ORDER];

ALTER TABLE [dbo].[SALES_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICES_Customer];

ALTER TABLE [dbo].[SALES_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICES_Currency];

ALTER TABLE [dbo].[SALES_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_INVOICES_CreatedBy];

ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_HEADERS_Invoice];

ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_HEADERS_Customer];

ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SF_HEADERS_INVOICE];

ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SF_HEADERS_CUSTOMER];

ALTER TABLE [dbo].[SALES_SF_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_HEADERS_CreatedBy];

ALTER TABLE [dbo].[SALES_SF_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_LINES_Product];

ALTER TABLE [dbo].[SALES_SF_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_LINES_InvoiceLine];

ALTER TABLE [dbo].[SALES_SF_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_SF_LINES_Header];

ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_TTNK_HEADERS_ORDER];

ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_HEADERS_Invoice];

ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_TTNK_HEADERS_CONTRACT];

ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_TTNK_HEADERS_INVOICE];

ALTER TABLE [dbo].[SALES_TTNK_HEADERS] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_HEADERS_Currency];

ALTER TABLE [dbo].[SALES_TTNK_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_INVOICES_Invoice];

ALTER TABLE [dbo].[SALES_TTNK_INVOICES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_INVOICES_Header];

ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_LINES_InvoiceLine];

ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_LINES_Header];

ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_LINES_HsCode];

ALTER TABLE [dbo].[SALES_TTNK_LINES] WITH CHECK CHECK CONSTRAINT [FK_SALES_TTNK_LINES_Product];

ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH CHECK CHECK CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_FINTRAN];

ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH CHECK CHECK CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_CURRENCY];

ALTER TABLE [dbo].[SHAREHOLDER_CONTRIBUTIONS] WITH CHECK CHECK CONSTRAINT [FK_SHAREHOLDER_CONTRIBUTIONS_SH];

ALTER TABLE [dbo].[SHAREHOLDER_SHARE_PERIODS] WITH CHECK CHECK CONSTRAINT [FK_SHAREHOLDER_SHARE_PERIODS_SH];

ALTER TABLE [dbo].[SHOPS] WITH CHECK CHECK CONSTRAINT [FK_PRODUCTION_SHOPS_COMPANIES];

ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH CHECK CHECK CONSTRAINT [FK_SORDER_DELIVERY_SCHEDULE_S_ORDERS];

ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH CHECK CHECK CONSTRAINT [FK_SORDER_DELIVERY_CreatedBy];

ALTER TABLE [dbo].[SORDER_DELIVERY_SCHEDULE] WITH CHECK CHECK CONSTRAINT [FK_SORDER_DELIVERY_Product];

ALTER TABLE [dbo].[STD_COMPANY_STANDARDS] WITH CHECK CHECK CONSTRAINT [FK_STD_COMPANY_STANDARDS_DOC_DOCUMENTS];

ALTER TABLE [dbo].[TANNARX] WITH CHECK CHECK CONSTRAINT [FK_TANNARX_USERS_CreatedBy];

ALTER TABLE [dbo].[TANNARX] WITH CHECK CHECK CONSTRAINT [FK_TANNARX_USERS_ModifiedBy];

ALTER TABLE [dbo].[TANNARX] WITH CHECK CHECK CONSTRAINT [FK_TANNARX_PRODUCTS];

ALTER TABLE [dbo].[TANNARX_ACTUAL_AUDIT] WITH CHECK CHECK CONSTRAINT [FK_TANNARX_ACTUAL_AUDIT_TANNARX];

ALTER TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP] WITH CHECK CHECK CONSTRAINT [FK_TANNARX_FIN_CATEGORY_MAP_CATEGORY];

ALTER TABLE [dbo].[USER_ENTRIES] WITH CHECK CHECK CONSTRAINT [FK_USER_ENTRIES_USERS];

ALTER TABLE [dbo].[USER_LICENSES] WITH CHECK CHECK CONSTRAINT [FK_UL_COMPANY_LICENSES];

ALTER TABLE [dbo].[USER_LICENSES] WITH CHECK CHECK CONSTRAINT [FK_UL_USERS];

ALTER TABLE [dbo].[USERIMAGES] WITH CHECK CHECK CONSTRAINT [FK_USERIMAGES_USERS];

ALTER TABLE [dbo].[USERLOGS] WITH CHECK CHECK CONSTRAINT [FK_USERLOGS_USERS];

ALTER TABLE [dbo].[USERROLES] WITH CHECK CHECK CONSTRAINT [FK_USERROLES_ROLES];

ALTER TABLE [dbo].[USERROLES] WITH CHECK CHECK CONSTRAINT [FK_USERROLES_USERS];

ALTER TABLE [dbo].[USERS] WITH CHECK CHECK CONSTRAINT [FK_USERS_COMPANIES];

ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS] WITH CHECK CHECK CONSTRAINT [FK_WHL_WAREHOUSES];

ALTER TABLE [dbo].[WAREHOUSES] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_USERS];

ALTER TABLE [dbo].[WAREHOUSES] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_PROD_SHOPS];

ALTER TABLE [dbo].[WASTES] WITH CHECK CHECK CONSTRAINT [FK_WASTES_PRODUCTIONPROCESSES];

ALTER TABLE [dbo].[WRHS_EXPENSE_DOCS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_EXPENSE_DOCS_PROD_WRHS_EXPENSES];

ALTER TABLE [dbo].[WRHS_INCOME_DOCS] WITH CHECK CHECK CONSTRAINT [FK_PROD_WRHS_INCOME_DOCS_PROD_WRHS_INCOMES];

ALTER TABLE [dbo].[CURRENCY_RATES] WITH CHECK CHECK CONSTRAINT [CK_CURRENCY_RATES_Rate];

ALTER TABLE [dbo].[EXPENSE_PRODUCT_ALLOCATIONS] WITH CHECK CHECK CONSTRAINT [CK_EPA_Qty_Positive];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [CK_HR_PAYROLL_HEADERS_Month];

ALTER TABLE [dbo].[HR_PAYROLL_HEADERS] WITH CHECK CHECK CONSTRAINT [CK_HR_PAYROLL_HEADERS_Year];

ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH CHECK CHECK CONSTRAINT [CK_HR_TIMESHEET_HEADERS_Month];

ALTER TABLE [dbo].[HR_TIMESHEET_HEADERS] WITH CHECK CHECK CONSTRAINT [CK_HR_TIMESHEET_HEADERS_Year];

ALTER TABLE [dbo].[LOT_LOCATIONS] WITH CHECK CHECK CONSTRAINT [CK_LL_Qty_NonNegative];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [CK_QUALITY_INSPECTION_Quantities];

ALTER TABLE [dbo].[QUALITY_INSPECTION] WITH CHECK CHECK CONSTRAINT [CK_QUALITY_INSPECTION_NonNegative];

ALTER TABLE [dbo].[TANNARX_FIN_CATEGORY_MAP] WITH CHECK CHECK CONSTRAINT [CK_TANNARX_FIN_CATEGORY_MAP_Bucket];

ALTER TABLE [dbo].[WAREHOUSE_LOCATIONS] WITH CHECK CHECK CONSTRAINT [CK_WHL_MinLeMax];

ALTER TABLE [dbo].[WAREHOUSES] WITH CHECK CHECK CONSTRAINT [CK_WAREHOUSES_WarehouseType];
GO
CREATE TABLE [#__checkStatus] (
    id           INT            IDENTITY (1, 1) PRIMARY KEY CLUSTERED,
    [Schema]     NVARCHAR (256),
    [Table]      NVARCHAR (256),
    [Constraint] NVARCHAR (256)
);

SET NOCOUNT ON;

DECLARE tableconstraintnames CURSOR LOCAL FORWARD_ONLY
    FOR SELECT SCHEMA_NAME([schema_id]),
               OBJECT_NAME([parent_object_id]),
               [name],
               0
        FROM   [sys].[objects]
        WHERE  [parent_object_id] IN (OBJECT_ID(N'dbo.EXPENSES'), OBJECT_ID(N'dbo.F_WAYBILL_PAYMENTS'), OBJECT_ID(N'dbo.INCOMES'), OBJECT_ID(N'dbo.P_CONTRACTS'), OBJECT_ID(N'dbo.P_INVOICES'), OBJECT_ID(N'dbo.P_ORDERS'), OBJECT_ID(N'dbo.PRODUCT_STOCKS'), OBJECT_ID(N'dbo.S_CONTRACTS'), OBJECT_ID(N'dbo.S_ORDERS'), OBJECT_ID(N'dbo.WASTE_STOCKS'))
               AND [type] IN (N'F', N'C')
                   AND [object_id] IN (SELECT [object_id]
                                       FROM   [sys].[check_constraints]
                                       WHERE  [is_not_trusted] <> 0
                                              AND [is_disabled] = 0
                                       UNION
                                       SELECT [object_id]
                                       FROM   [sys].[foreign_keys]
                                       WHERE  [is_not_trusted] <> 0
                                              AND [is_disabled] = 0);

DECLARE @schemaname AS NVARCHAR (256);

DECLARE @tablename AS NVARCHAR (256);

DECLARE @checkname AS NVARCHAR (256);

DECLARE @is_not_trusted AS INT;

DECLARE @statement AS NVARCHAR (1024);

BEGIN TRY
    OPEN tableconstraintnames;
    FETCH tableconstraintnames INTO @schemaname, @tablename, @checkname, @is_not_trusted;
    WHILE @@fetch_status = 0
        BEGIN
            PRINT N'Checking constraint: ' + @checkname + N' [' + @schemaname + N'].[' + @tablename + N']';
            SET @statement = N'ALTER TABLE [' + @schemaname + N'].[' + @tablename + N'] WITH ' + CASE @is_not_trusted WHEN 0 THEN N'CHECK' ELSE N'NOCHECK' END + N' CHECK CONSTRAINT [' + @checkname + N']';
            BEGIN TRY
                EXECUTE [sp_executesql] @statement;
            END TRY
            BEGIN CATCH
                INSERT  [#__checkStatus] ([Schema], [Table], [Constraint])
                VALUES                  (@schemaname, @tablename, @checkname);
            END CATCH
            FETCH tableconstraintnames INTO @schemaname, @tablename, @checkname, @is_not_trusted;
        END
END TRY
BEGIN CATCH
    PRINT ERROR_MESSAGE();
END CATCH

IF CURSOR_STATUS(N'LOCAL', N'tableconstraintnames') >= 0
    CLOSE tableconstraintnames;

IF CURSOR_STATUS(N'LOCAL', N'tableconstraintnames') = -1
    DEALLOCATE tableconstraintnames;

SELECT N'Constraint verification failed:' + [Schema] + N'.' + [Table] + N',' + [Constraint]
FROM   [#__checkStatus];

IF @@ROWCOUNT > 0
    BEGIN
        DROP TABLE [#__checkStatus];
        RAISERROR (N'An error occurred while verifying constraints', 16, 127);
    END

SET NOCOUNT OFF;

DROP TABLE [#__checkStatus];
GO
