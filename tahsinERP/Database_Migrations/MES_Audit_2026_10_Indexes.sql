-- =====================================================================
-- Audit 2026-10 (MES/Engineering): Audit_2026_10_Indexes.sql (core) ga qo'shimcha indekslar.
-- MES kodidagi yangi so'rovlar shu ustunlar bo'yicha filtrlaydi/guruhlaydi.
--
-- Xavfsiz, qayta ishga tushirish mumkin: indeks faqat mavjud bo'lmasa va ustunlar bo'lsa yaratiladi.
-- Ma'lumot o'zgarmaydi. Avval test nusxada; katta jadvallarda ish vaqtidan tashqarida.
-- HECH QAYERDA AVTOMATIK ISHGA TUSHIRILMAGAN.
-- =====================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('tempdb..#ix') IS NOT NULL DROP TABLE #ix;
CREATE TABLE #ix (TableName sysname, IndexName sysname, KeyCols nvarchar(400), IncludeCols nvarchar(400) NULL, Note nvarchar(200));

INSERT INTO #ix VALUES
-- Sifat nazorati: qabul qilingan miqdor GROUP BY ProductionID, lock guard, dashboard
('QUALITY_INSPECTION', 'IX_QI_ProductionID',               'ProductionID',                'QtyAccepted, InspectionDate, ProductPlanDailyID, IsDeleted', 'QI balans, ProductionLockGuard'),
('QUALITY_INSPECTION', 'IX_QI_Product_Status',             'ProductID, StatusCode',       'QtyAccepted, IsDeleted',                'dashboard bottleneck'),
('QUALITY_INSPECTION', 'IX_QI_InspectionDate',             'InspectionDate',              'StatusCode, QtyAccepted, QtyRejected, DefectTypeID, IsDeleted', 'dashboard/hisobot'),
('QUALITY_INSPECTION', 'IX_QI_PlanDaily',                  'ProductPlanDailyID',          'ProductionID, QtyInspected, IsDeleted', 'eski attribution'),
-- Sotuv invoysi qatorlari: dashboard (OrderID, ProductID) bo'yicha yig'indi
('SALES_INVOICE_LINES','IX_SALES_INVOICE_LINES_Invoice',   'InvoiceID',                   'ProductID, Qty',                        'bottleneck'),
-- Normalar
('NORMVERSIONS',       'IX_NORMVERSIONS_Product_From',     'ProductID, EffectiveFrom',    'EffectiveTo, IsDeleted',                'Tannarx/Norm/BOM'),
('NORMLINES',          'IX_NORMLINES_NormID',              'NormID',                      'MaterialID, QtyPerUnit',                'norma qatorlari'),
-- Ishlab chiqarish: sex + sana (kunlik kiritish, planned products)
('PRODUCT_PRODUCTION', 'IX_PRODUCT_PRODUCTION_Shop_Date',  'ShopID, Date',                'ProductID, Total, IsDeleted',           'kunlik kiritish'),
-- Tannarx ish haqi (set-based GROUP BY)
('HR_PIECE_RATE_TARIFFS','IX_HR_PRT_Product_Active',       'ProductID, IsActive',         'EffectiveFrom, EffectiveTo, RatePerUnit','tannarx ish haqi'),
-- Brak
('DAMAGED_PRODUCTS',   'IX_DAMAGED_PRODUCTS_Product_Date', 'ProductID, IssueDateTime',    'DefectTypeID, IsDeleted',               'QI tasdiqlash'),
-- Mahsulot rasmlari (BOM daraxti bitta so'rovda)
('PRODUCTIMAGES',      'IX_PRODUCTIMAGES_ProdID',          'ProdID',                      'IsDeleted',                             'rasm');

DECLARE @t sysname, @i sysname, @k nvarchar(400), @inc nvarchar(400), @sql nvarchar(max), @missing nvarchar(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, IndexName, KeyCols, IncludeCols FROM #ix;
OPEN c;
FETCH NEXT FROM c INTO @t, @i, @k, @inc;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t), N'U') IS NULL
        PRINT N'O''tkazildi (jadval yo''q): ' + @t + N'.' + @i;
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@t)) AND name = @i)
        PRINT N'Mavjud: ' + @t + N'.' + @i;
    ELSE
    BEGIN
        SET @missing = NULL;
        SELECT @missing = STRING_AGG(LTRIM(RTRIM(value)), N', ')
        FROM STRING_SPLIT(@k + ISNULL(N',' + @inc, N''), N',')
        WHERE COL_LENGTH(N'dbo.' + @t, LTRIM(RTRIM(value))) IS NULL;

        IF @missing IS NOT NULL
            PRINT N'O''tkazildi (ustun yo''q: ' + @missing + N'): ' + @t + N'.' + @i;
        ELSE
        BEGIN
            SET @sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@i) + N' ON dbo.' + QUOTENAME(@t)
                     + N' (' + (SELECT STRING_AGG(QUOTENAME(LTRIM(RTRIM(value))), N', ') FROM STRING_SPLIT(@k, N',')) + N')'
                     + ISNULL(N' INCLUDE (' + (SELECT STRING_AGG(QUOTENAME(LTRIM(RTRIM(value))), N', ') FROM STRING_SPLIT(@inc, N',')) + N')', N'')
                     + N';';
            BEGIN TRY
                EXEC sp_executesql @sql;
                PRINT N'Yaratildi: ' + @t + N'.' + @i;
            END TRY
            BEGIN CATCH
                PRINT N'XATO: ' + @t + N'.' + @i + N' — ' + ERROR_MESSAGE();
            END CATCH
        END
    END
    FETCH NEXT FROM c INTO @t, @i, @k, @inc;
END
CLOSE c; DEALLOCATE c;
DROP TABLE #ix;
GO

-- ---------------------------------------------------------------------
-- Unique cheklovlar (MES-B10). Avval dublikat bormi tekshiring — bo'lsa yaratish xato beradi.
-- ---------------------------------------------------------------------
-- Tannarx: lokal bazada IX_TANNARX_Unique_Product_Year_Month allaqachon bor (core skriptdagi izohga qarang).
-- Prod'da tekshirish:
--   SELECT name, is_unique, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TANNARX');
--   SELECT ProductID, [Year], [Month], COUNT(*) FROM dbo.TANNARX WHERE IsDeleted = 0 GROUP BY ProductID, [Year], [Month] HAVING COUNT(*) > 1;

-- Mahsulot kategoriyasi kodi:
--   SELECT Code, COUNT(*) FROM dbo.PRODUCT_CATEGORIES WHERE Code IS NOT NULL GROUP BY Code HAVING COUNT(*) > 1;
--   CREATE UNIQUE INDEX UX_PRODUCT_CATEGORIES_Code ON dbo.PRODUCT_CATEGORIES(Code) WHERE Code IS NOT NULL;

-- Norma: bir mahsulot + oy uchun bitta aktiv versiya (kod tekshiradi; DB darajasida kafolat uchun hisoblanadigan ustun kerak):
--   SELECT ProductID, CONVERT(char(7), EffectiveFrom, 126) AS Period, COUNT(*)
--   FROM dbo.NORMVERSIONS WHERE ISNULL(IsDeleted, 0) = 0 AND EffectiveFrom IS NOT NULL
--   GROUP BY ProductID, CONVERT(char(7), EffectiveFrom, 126) HAVING COUNT(*) > 1;

-- ---------------------------------------------------------------------
-- Sxema takliflari (EDMX yangilashni talab qiladi — foydalanuvchi qarori, hozircha QO'LLANMAGAN):
--   1) PRODUCT_PRODUCTION.PlanDate (date NULL) — Comment'dagi "[RejaSana:yyyy-MM-dd]" o'rniga (MES-B20, P03/P04).
--   2) EXPENSES.ProductionID / INCOMES.ProductionID (int NULL, FK) — Description'dagi "ProductionID:x" o'rniga (P12).
--   3) EXPENSE_PRODUCTS.SourceProductionID yoki alohida LOT_ALLOCATIONS jadvali — "ProdID:x:q" izohi o'rniga (P17, B03).
--   4) NORM_INPUT_LINES.NormVersionID (int NULL, FK) — Month satri o'rniga (B06).
--   5) BOM_HEADERS: har NORMVERSION uchun alohida header (VersionNo = davr) — oyga mos BOM tanlash uchun (B07).
-- ---------------------------------------------------------------------
