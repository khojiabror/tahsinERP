/*
 CRM yakuniy tur (19.10.2026): invoysda to'lov muddati.
 S_CONTRACTS.PaymentDays — shartnoma bo'yicha to'lov muddati (kun). Invoys yaratilganda/tahrirlanganda
 "To'lov muddati" bo'sh qoldirilsa DueDate = InvoiceDate + PaymentDays. PaymentTerms (erkin matn) o'zgarmaydi.
 Oldin: BACKUP DATABASE (DBTHSN-IPACK_CRM_FINAL_20261019.bak). Qayta ishga tushirsa bo'ladi.
*/
SET NOCOUNT ON;

IF COL_LENGTH('dbo.S_CONTRACTS', 'PaymentDays') IS NULL
BEGIN
    ALTER TABLE dbo.S_CONTRACTS ADD PaymentDays INT NULL;
    PRINT 'S_CONTRACTS.PaymentDays qo''shildi';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_S_CONTRACTS_PaymentDays')
    ALTER TABLE dbo.S_CONTRACTS ADD CONSTRAINT CK_S_CONTRACTS_PaymentDays CHECK (PaymentDays IS NULL OR PaymentDays BETWEEN 0 AND 3650);
GO

-- "po faktu" (yetkazilganda) — 0 kun
UPDATE dbo.S_CONTRACTS SET PaymentDays = 0
WHERE PaymentDays IS NULL AND LTRIM(RTRIM(LOWER(PaymentTerms))) IN (N'po faktu', N'по факту', N'post');
PRINT CONCAT('PaymentDays = 0 qo''yilgan shartnomalar: ', @@ROWCOUNT);
GO
