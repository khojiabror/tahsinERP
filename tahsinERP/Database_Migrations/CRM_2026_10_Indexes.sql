-- =====================================================================
-- Audit 2026-10 / CRM: sotuv hujjatlari uchun indekslar (Docs/Audit-2026-10/CRM.md).
--
-- 1) UX_SALES_INVOICES_InvoiceNo — invoys raqami takrorlanmasligi (CRM-B02).
--    Kod endi raqamni qulf ostida beradi va band raqamni rad etadi; unique indeks
--    oxirgi himoya. Takroriy raqamlar bo'lsa indeks YARATILMAYDI — avval
--    DataFix_CRM_B02_DuplicateInvoiceNo.sql bilan tuzating.
-- 2) Issiq FK/filtr ustunlari (CORE.md "Indexes": har modul o'z *_LINES jadvallari uchun).
--
-- Xavfsiz, qayta ishga tushirish mumkin: faqat mavjud bo'lmagan indeks yaratiladi.
-- Ma'lumot o'zgarmaydi. Avval test nusxada, ish vaqtidan tashqarida ishga tushiring.
-- =====================================================================
SET NOCOUNT ON;
GO

-- 1) Unique invoys raqami
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SALES_INVOICES_InvoiceNo' AND object_id = OBJECT_ID('dbo.SALES_INVOICES'))
BEGIN
    IF EXISTS (SELECT InvoiceNo FROM dbo.SALES_INVOICES WHERE InvoiceNo IS NOT NULL GROUP BY InvoiceNo HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'DIQQAT: takroriy InvoiceNo bor — UX_SALES_INVOICES_InvoiceNo yaratilmadi. DataFix_CRM_B02_DuplicateInvoiceNo.sql ni ko''ring.';
        SELECT InvoiceNo, COUNT(*) AS Cnt FROM dbo.SALES_INVOICES WHERE InvoiceNo IS NOT NULL GROUP BY InvoiceNo HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        -- Filtrlangan: NULL raqamlar (eski yozuvlar) cheklanmaydi
        CREATE UNIQUE NONCLUSTERED INDEX UX_SALES_INVOICES_InvoiceNo
            ON dbo.SALES_INVOICES(InvoiceNo) WHERE InvoiceNo IS NOT NULL;
        PRINT 'UX_SALES_INVOICES_InvoiceNo yaratildi.';
    END
END
ELSE
    PRINT 'UX_SALES_INVOICES_InvoiceNo allaqachon mavjud.';
GO

-- 2) FK / filtr indekslari
-- (SF/TTNK sarlavha va qator bo'yicha indekslar bazada UX_* va IX_SALES_SF_HEADERS_Invoice bilan qoplangan — qo'shilmaydi)
IF OBJECT_ID('tempdb..#ix') IS NOT NULL DROP TABLE #ix;
CREATE TABLE #ix (TableName sysname, IndexName sysname, KeyCols nvarchar(400), IncludeCols nvarchar(400) NULL);

INSERT INTO #ix VALUES
('SALES_INVOICE_LINES',      'IX_SALES_INVOICE_LINES_InvoiceID',     'InvoiceID',                 'ProductID, Qty, LineAmount'),
('SALES_INVOICE_LINES',      'IX_SALES_INVOICE_LINES_ProductID',     'ProductID',                 'InvoiceID, Qty'),
('SALES_INVOICES',           'IX_SALES_INVOICES_InvoiceDate',        'InvoiceDate',               'CustomerID, ContractID, OrderID, TotalAmount, StatusCode'),
('SALES_INVOICES',           'IX_SALES_INVOICES_Customer',           'CustomerID',                'StatusCode'),
('SALES_INVOICES',           'IX_SALES_INVOICES_Contract',           'ContractID',                'StatusCode'),
('SALES_INVOICES',           'IX_SALES_INVOICES_Order',              'OrderID',                   'StatusCode'),
('SORDER_DELIVERY_SCHEDULE', 'IX_SORDER_DELIVERY_SCHEDULE_Order',    'SOrderID',                  'SOrderProductID, QtyPlanned, QtyShipped'),
('SALES_SF_LINES',           'IX_SALES_SF_LINES_InvoiceLine',        'InvoiceLineID',             NULL),
('SALES_TTNK_INVOICES',      'IX_SALES_TTNK_INVOICES_InvoiceID',     'InvoiceID',                 'TTNKHeaderID'),
('SALES_TTNK_LINES',         'IX_SALES_TTNK_LINES_InvoiceLine',      'InvoiceLineID',             NULL);

DECLARE @t sysname, @i sysname, @k nvarchar(400), @inc nvarchar(400), @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, IndexName, KeyCols, IncludeCols FROM #ix;
OPEN c;
FETCH NEXT FROM c INTO @t, @i, @k, @inc;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('dbo.' + @t, 'U') IS NULL
        PRINT 'O''tkazildi (jadval yo''q): ' + @t;
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = @i AND object_id = OBJECT_ID('dbo.' + @t))
        PRINT 'Mavjud: ' + @i;
    ELSE
    BEGIN
        SET @sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@i) + N' ON dbo.' + QUOTENAME(@t) + N'(' + @k + N')'
                 + CASE WHEN @inc IS NULL THEN N'' ELSE N' INCLUDE (' + @inc + N')' END + N';';
        BEGIN TRY
            EXEC sp_executesql @sql;
            PRINT 'Yaratildi: ' + @i;
        END TRY
        BEGIN CATCH
            PRINT 'XATO (' + @i + '): ' + ERROR_MESSAGE();
        END CATCH
    END
    FETCH NEXT FROM c INTO @t, @i, @k, @inc;
END
CLOSE c;
DEALLOCATE c;
DROP TABLE #ix;
GO
