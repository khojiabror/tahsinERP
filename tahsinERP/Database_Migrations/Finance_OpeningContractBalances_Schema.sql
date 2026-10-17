-- =====================================================================
-- Finance (FIN-P11): FIN_OPENING_CONTRACT_BALANCES sxemasi.
--
-- Avval OpeningBalanceController.EnsureOpeningContractBalanceTable har
-- Index/Edit/Details/Delete so'rovida (GET'da ham) shu DDL'ni va butun jadval
-- bo'yicha UPDATE'ni ishga tushirardi. Kod endi sxemani o'zgartirmaydi;
-- jadval bo'lmagan bazada shu skriptni bir marta bajaring.
--
-- Qayta ishga tushirish xavfsiz: faqat yo'q obyektlar yaratiladi,
-- ma'lumot o'zgarmaydi (eski TotalAmount/PaidAmount to'ldirish uchun
-- DataFix_Finance_OpeningContractTotals_*.sql ga qarang).
-- =====================================================================
SET NOCOUNT ON;

IF OBJECT_ID('dbo.FIN_OPENING_CONTRACT_BALANCES', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FIN_OPENING_CONTRACT_BALANCES(
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SnapshotDate DATE NOT NULL,
        BalanceType TINYINT NOT NULL, -- 1: mijoz (AR), 2: ta'minotchi (AP), 3: xodim (oylik), 4: ekspeditor
        CounterpartyID INT NOT NULL,
        ContractID INT NULL,
        ContractNo NVARCHAR(100) NULL,
        TotalAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_TotalAmount DEFAULT(0),
        PaidAmount DECIMAL(19,2) NOT NULL CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_PaidAmount DEFAULT(0),
        Amount DECIMAL(19,2) NOT NULL,
        CurrencyID INT NOT NULL,
        CreatedByUserID INT NULL,
        CreatedDT DATETIME NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_FIN_OPENING_CONTRACT_BALANCES_SnapshotDate ON dbo.FIN_OPENING_CONTRACT_BALANCES(SnapshotDate);
    CREATE INDEX IX_FIN_OPENING_CONTRACT_BALANCES_TypeCounterparty ON dbo.FIN_OPENING_CONTRACT_BALANCES(BalanceType, CounterpartyID);
    PRINT 'FIN_OPENING_CONTRACT_BALANCES yaratildi';
END

IF COL_LENGTH('dbo.FIN_OPENING_CONTRACT_BALANCES', 'TotalAmount') IS NULL
    ALTER TABLE dbo.FIN_OPENING_CONTRACT_BALANCES ADD TotalAmount DECIMAL(19,2) NOT NULL
        CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_TotalAmount_2 DEFAULT(0);

IF COL_LENGTH('dbo.FIN_OPENING_CONTRACT_BALANCES', 'PaidAmount') IS NULL
    ALTER TABLE dbo.FIN_OPENING_CONTRACT_BALANCES ADD PaidAmount DECIMAL(19,2) NOT NULL
        CONSTRAINT DF_FIN_OPENING_CONTRACT_BALANCES_PaidAmount_2 DEFAULT(0);

-- Hisobotlar (BalanceType, SnapshotDate) bo'yicha MAX(SnapshotDate) va qatorlarni o'qiydi
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FIN_OPENING_CONTRACT_BALANCES_Type_Date'
               AND object_id = OBJECT_ID('dbo.FIN_OPENING_CONTRACT_BALANCES'))
    CREATE INDEX IX_FIN_OPENING_CONTRACT_BALANCES_Type_Date
        ON dbo.FIN_OPENING_CONTRACT_BALANCES(BalanceType, SnapshotDate)
        INCLUDE (CounterpartyID, ContractNo, TotalAmount, PaidAmount, Amount);
