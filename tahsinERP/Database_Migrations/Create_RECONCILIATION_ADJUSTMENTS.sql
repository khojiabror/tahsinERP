-- Akt-sverki qo'lda tuzatishlari (har bir shartnoma bo'yicha yilda bir marta)
-- CounterpartyType: 1 = Mijoz, 2 = Ta'minotchi, 4 = Yuk tashuvchi
-- ContractID: P_CONTRACTS.ID / S_CONTRACTS.ID / F_CONTRACTS.ID (tur bo'yicha)

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RECONCILIATION_ADJUSTMENTS')
BEGIN
    CREATE TABLE dbo.RECONCILIATION_ADJUSTMENTS (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CounterpartyType TINYINT NOT NULL,
        CounterpartyID INT NOT NULL,
        ContractID INT NOT NULL,
        EntryYear INT NOT NULL,
        CutoffDate DATE NOT NULL,
        Party1Debit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_RECON_ADJ_P1D DEFAULT (0),
        Party1Credit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_RECON_ADJ_P1C DEFAULT (0),
        Note NVARCHAR(500) NULL,
        CreatedDT DATETIME2 NOT NULL CONSTRAINT DF_RECON_ADJ_CDT DEFAULT (SYSUTCDATETIME()),
        ModifiedDT DATETIME2 NULL,
        CreatedByUserID INT NOT NULL,
        CONSTRAINT UQ_RECON_ADJ_CONTRACT_YEAR UNIQUE (CounterpartyType, CounterpartyID, ContractID, EntryYear)
    );

    CREATE INDEX IX_RECON_ADJ_LOOKUP ON dbo.RECONCILIATION_ADJUSTMENTS (CounterpartyType, CounterpartyID, ContractID, CutoffDate);
END
GO
