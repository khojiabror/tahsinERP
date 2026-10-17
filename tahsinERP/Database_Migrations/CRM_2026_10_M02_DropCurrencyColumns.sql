/*
 CRM-M02 (foydalanuvchi qarori 2026-10-18): valyutaning yagona manbai CurrencyID.
 SALES_INVOICES.CurrencyCode (char(3)) va S_ORDERS.Currency (varchar(15)) olib tashlanadi.
 Kod va EDMX shu commit bilan ularni ishlatmaydi. Oldin:
   - DataFix_CRM_M02_InvoiceCurrencyID.sql bajarilgan bo'lishi kerak (CurrencyID to'ldirilgan);
   - eski qiymatlar zaxira jadvallarga ko'chiriladi.
 Qayta ishga tushirsa bo'ladi (ustun yo'q bo'lsa hech narsa qilmaydi).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF COL_LENGTH('dbo.SALES_INVOICES', 'CurrencyCode') IS NOT NULL
BEGIN
    -- CurrencyID bo'sh qolgan invoys bo'lsa — koddan to'ldiriladi, topilmasa to'xtaladi
    EXEC('UPDATE si SET CurrencyID = c.ID
          FROM dbo.SALES_INVOICES si JOIN dbo.CURRENCIES c ON c.Shortname = si.CurrencyCode
          WHERE si.CurrencyID IS NULL');
    IF EXISTS (SELECT 1 FROM dbo.SALES_INVOICES WHERE CurrencyID IS NULL)
        THROW 50001, 'SALES_INVOICES: CurrencyID bo''sh qatorlar bor — avval DataFix_CRM_M02_InvoiceCurrencyID.sql', 1;

    IF OBJECT_ID('dbo.SALES_INVOICES_CurrencyCode_BAK_20261018') IS NULL
        EXEC('SELECT ID, CurrencyCode, CurrencyID INTO dbo.SALES_INVOICES_CurrencyCode_BAK_20261018 FROM dbo.SALES_INVOICES');

    DECLARE @df sysname, @sql nvarchar(400);
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.SALES_INVOICES') AND c.name = 'CurrencyCode';
    IF @df IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE dbo.SALES_INVOICES DROP CONSTRAINT ' + QUOTENAME(@df);
        EXEC(@sql);
    END
    ALTER TABLE dbo.SALES_INVOICES DROP COLUMN CurrencyCode;
    PRINT 'SALES_INVOICES.CurrencyCode olib tashlandi';
END

IF COL_LENGTH('dbo.S_ORDERS', 'Currency') IS NOT NULL
BEGIN
    IF OBJECT_ID('dbo.S_ORDERS_Currency_BAK_20261018') IS NULL
        EXEC('SELECT ID, Currency, CurrencyID INTO dbo.S_ORDERS_Currency_BAK_20261018 FROM dbo.S_ORDERS');
    ALTER TABLE dbo.S_ORDERS DROP COLUMN Currency;
    PRINT 'S_ORDERS.Currency olib tashlandi';
END

COMMIT;
GO
