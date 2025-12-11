-- HR_PIECE_RATE_TARIFFS: Ishbay uchun tariff jadvali
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HR_PIECE_RATE_TARIFFS]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HR_PIECE_RATE_TARIFFS](
        [ID] [int] IDENTITY(1,1) NOT NULL,
        [ProductID] [int] NOT NULL,
        [PositionID] [int] NULL,
        [RatePerUnit] [decimal](18, 4) NOT NULL,
        [EffectiveFrom] [date] NOT NULL,
        [EffectiveTo] [date] NULL,
        [IsActive] [bit] NOT NULL DEFAULT(1),
        [CreatedByUserID] [int] NULL,
        [CreatedDT] [datetime] NOT NULL DEFAULT(GETDATE()),
        [ModifiedByUserID] [int] NULL,
        [ModifiedDT] [datetime] NULL,
        CONSTRAINT [PK_HR_PIECE_RATE_TARIFFS] PRIMARY KEY CLUSTERED ([ID] ASC)
    )
END
GO

-- Foreign Keys for HR_PIECE_RATE_TARIFFS
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HR_PIECE_RATE_TARIFFS_PRODUCTS')
BEGIN
    ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_PRODUCTS] FOREIGN KEY([ProductID]) 
    REFERENCES [dbo].[PRODUCTS] ([ID])
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HR_PIECE_RATE_TARIFFS_POSITIONS')
BEGIN
    ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_POSITIONS] FOREIGN KEY([PositionID]) 
    REFERENCES [dbo].[HR_POSITIONS] ([ID])
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HR_PIECE_RATE_TARIFFS_CreatedByUserID_USERS')
BEGIN
    ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_CreatedByUserID_USERS] FOREIGN KEY([CreatedByUserID]) 
    REFERENCES [dbo].[USERS] ([ID])
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HR_PIECE_RATE_TARIFFS_ModifiedByUserID_USERS')
BEGIN
    ALTER TABLE [dbo].[HR_PIECE_RATE_TARIFFS]
    ADD CONSTRAINT [FK_HR_PIECE_RATE_TARIFFS_ModifiedByUserID_USERS] FOREIGN KEY([ModifiedByUserID]) 
    REFERENCES [dbo].[USERS] ([ID])
END
GO

-- HR_TIMESHEET_LINES jadvaliga ishbay uchun ustunlar qo'shish
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[HR_TIMESHEET_LINES]') AND name = 'ProductID')
BEGIN
    ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD [ProductID] [int] NULL,
        [Day01Quantity] [decimal](18, 4) NULL,
        [Day02Quantity] [decimal](18, 4) NULL,
        [Day03Quantity] [decimal](18, 4) NULL,
        [Day04Quantity] [decimal](18, 4) NULL,
        [Day05Quantity] [decimal](18, 4) NULL,
        [Day06Quantity] [decimal](18, 4) NULL,
        [Day07Quantity] [decimal](18, 4) NULL,
        [Day08Quantity] [decimal](18, 4) NULL,
        [Day09Quantity] [decimal](18, 4) NULL,
        [Day10Quantity] [decimal](18, 4) NULL,
        [Day11Quantity] [decimal](18, 4) NULL,
        [Day12Quantity] [decimal](18, 4) NULL,
        [Day13Quantity] [decimal](18, 4) NULL,
        [Day14Quantity] [decimal](18, 4) NULL,
        [Day15Quantity] [decimal](18, 4) NULL,
        [Day16Quantity] [decimal](18, 4) NULL,
        [Day17Quantity] [decimal](18, 4) NULL,
        [Day18Quantity] [decimal](18, 4) NULL,
        [Day19Quantity] [decimal](18, 4) NULL,
        [Day20Quantity] [decimal](18, 4) NULL,
        [Day21Quantity] [decimal](18, 4) NULL,
        [Day22Quantity] [decimal](18, 4) NULL,
        [Day23Quantity] [decimal](18, 4) NULL,
        [Day24Quantity] [decimal](18, 4) NULL,
        [Day25Quantity] [decimal](18, 4) NULL,
        [Day26Quantity] [decimal](18, 4) NULL,
        [Day27Quantity] [decimal](18, 4) NULL,
        [Day28Quantity] [decimal](18, 4) NULL,
        [Day29Quantity] [decimal](18, 4) NULL,
        [Day30Quantity] [decimal](18, 4) NULL,
        [Day31Quantity] [decimal](18, 4) NULL,
        [TotalQuantity] [decimal](18, 4) NULL
END
GO

-- Foreign Key for HR_TIMESHEET_LINES.ProductID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HR_TIMESHEET_LINES_PRODUCTS')
BEGIN
    ALTER TABLE [dbo].[HR_TIMESHEET_LINES]
    ADD CONSTRAINT [FK_HR_TIMESHEET_LINES_PRODUCTS] FOREIGN KEY([ProductID]) 
    REFERENCES [dbo].[PRODUCTS] ([ID])
END
GO

