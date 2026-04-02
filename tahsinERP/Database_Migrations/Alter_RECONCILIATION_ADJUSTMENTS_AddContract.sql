-- Akt-sverki endi faqat bitta shartnoma bo'yicha — ContractID qo'shiladi.
-- Eski (shartnomasiz) yozuvlar o'chiriladi; kerak bo'lsa oldin eksport qiling.

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RECONCILIATION_ADJUSTMENTS')
AND NOT EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = 'RECONCILIATION_ADJUSTMENTS' AND c.name = 'ContractID'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RECON_ADJ_LOOKUP' AND object_id = OBJECT_ID('dbo.RECONCILIATION_ADJUSTMENTS'))
        DROP INDEX IX_RECON_ADJ_LOOKUP ON dbo.RECONCILIATION_ADJUSTMENTS;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_RECON_ADJ_PARTY_YEAR' AND parent_object_id = OBJECT_ID('dbo.RECONCILIATION_ADJUSTMENTS'))
        ALTER TABLE dbo.RECONCILIATION_ADJUSTMENTS DROP CONSTRAINT UQ_RECON_ADJ_PARTY_YEAR;

    DELETE FROM dbo.RECONCILIATION_ADJUSTMENTS;

    ALTER TABLE dbo.RECONCILIATION_ADJUSTMENTS ADD ContractID INT NOT NULL;

    ALTER TABLE dbo.RECONCILIATION_ADJUSTMENTS
        ADD CONSTRAINT UQ_RECON_ADJ_CONTRACT_YEAR UNIQUE (CounterpartyType, CounterpartyID, ContractID, EntryYear);

    CREATE INDEX IX_RECON_ADJ_LOOKUP ON dbo.RECONCILIATION_ADJUSTMENTS (CounterpartyType, CounterpartyID, ContractID, CutoffDate);
END
GO
