/* =====================================================================
   Rasmiy xat matni, salomlashish va mahsulot/narx satrlari — DOC_FILES JSON o'rniga ustunlarda (audit DOC-P10).
   Avval ular DOC_FILES'da "_letterdata.json" varbinary qatori sifatida saqlanardi: Edit/Details/GenerateDoc'da
   qo'shimcha blob o'qilardi, fayl ro'yxati va versiyalashga aralashardi.
   - COM_OFFICIAL_LETTERS.BodyHtml NVARCHAR(MAX), GreetingLine NVARCHAR(500)
   - COM_OFFICIAL_LETTER_ITEMS — mahsulot/narx satrlari (DocumentID -> COM_OFFICIAL_LETTERS)
   - Mavjud "_letterdata.json" qatorlarini ilova o'zi ko'chiradi (OfficialLetterDataService): ustunlar bo'sh
     bo'lsa JSON o'qiladi, keyingi saqlashda ustunlarga yoziladi va JSON qatori IsDeleted = 1 bo'ladi.
     (T-SQL'da varbinary UTF-8'ni Cyrillic collation'li bazada to'g'ri dekodlab bo'lmaydi.)
   Idempotent.
   2026-10-19: foydalanuvchi ruxsati bilan lokal DBTHSN-IPACK bazasida ishga tushirildi (zaxira:
   ...\MSSQL\Backup\DBTHSN-IPACK_before_doc_p10_2026-10-19.bak). Lokal bazada JSON qatorlari yo'q edi.
   ===================================================================== */
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
