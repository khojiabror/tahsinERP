/* =====================================================================
   008 — 000 baseline'dan keyin master'ga eski Database_Migrations/ orqali kirgan, lekin NNN migratsiyasi
   bo'lmagan sxema o'zgarishlari (yangi tenant bazalarida CRM shartnoma va rasmiy xat sahifalari SQL xato berardi):
     1) COM_OFFICIAL_LETTERS.BodyHtml / GreetingLine + COM_OFFICIAL_LETTER_ITEMS  (Alter_OfficialLetters_BodyItems.sql, DOC-P10)
     2) S_CONTRACTS.PaymentDays + CK_S_CONTRACTS_PaymentDays                    (CRM_2026_10_PaymentDays.sql)
   Lokal DBTHSN-IPACK'da qo'lda qo'llangan — bu yerda idempotent (mavjud bazada no-op, "po faktu" to'ldirish
   faqat bo'sh PaymentDays uchun).
   ===================================================================== */
SET NOCOUNT ON;

-- ---------- 1. Rasmiy xat matni va mahsulot/narx satrlari ----------
IF COL_LENGTH('dbo.COM_OFFICIAL_LETTERS', 'BodyHtml') IS NULL
    ALTER TABLE dbo.COM_OFFICIAL_LETTERS ADD BodyHtml NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.COM_OFFICIAL_LETTERS', 'GreetingLine') IS NULL
    ALTER TABLE dbo.COM_OFFICIAL_LETTERS ADD GreetingLine NVARCHAR(500) NULL;
GO

IF OBJECT_ID('dbo.COM_OFFICIAL_LETTER_ITEMS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.COM_OFFICIAL_LETTER_ITEMS
    (
        ID          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_COM_OFFICIAL_LETTER_ITEMS PRIMARY KEY,
        DocumentID  BIGINT NOT NULL,
        SortOrder   INT NOT NULL CONSTRAINT DF_COM_OFFICIAL_LETTER_ITEMS_SortOrder DEFAULT (0),
        ItemName    NVARCHAR(500) NULL,
        ItemSize    NVARCHAR(200) NULL,
        PriceExw    DECIMAL(18, 2) NULL,
        PriceVatExw DECIMAL(18, 2) NULL,
        PriceCip    DECIMAL(18, 2) NULL,
        PriceVatCip DECIMAL(18, 2) NULL,
        CONSTRAINT FK_COM_OFFICIAL_LETTER_ITEMS_LETTER FOREIGN KEY (DocumentID)
            REFERENCES dbo.COM_OFFICIAL_LETTERS (DocumentID)
    );
    CREATE INDEX IX_COM_OFFICIAL_LETTER_ITEMS_DocumentID ON dbo.COM_OFFICIAL_LETTER_ITEMS (DocumentID, SortOrder);
END
GO

-- ---------- 2. Sotuv shartnomasi bo'yicha to'lov muddati (kun) ----------
IF COL_LENGTH('dbo.S_CONTRACTS', 'PaymentDays') IS NULL
    ALTER TABLE dbo.S_CONTRACTS ADD PaymentDays INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_S_CONTRACTS_PaymentDays')
    ALTER TABLE dbo.S_CONTRACTS ADD CONSTRAINT CK_S_CONTRACTS_PaymentDays CHECK (PaymentDays IS NULL OR PaymentDays BETWEEN 0 AND 3650);
GO

-- "po faktu" (yetkazilganda) — 0 kun
UPDATE dbo.S_CONTRACTS SET PaymentDays = 0
WHERE PaymentDays IS NULL AND LTRIM(RTRIM(LOWER(PaymentTerms))) IN (N'po faktu', N'по факту', N'post');
GO
