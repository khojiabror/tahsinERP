/* =====================================================================
   015 — Ombor va ishlab chiqarish: float → decimal (4-bosqich) (Docs/Audit-2026-10/DB_FLOAT_DECIMAL_PLAN.md).
   Ombor va ishlab chiqarish: INCOMES/INCOME_PRODUCTS, EXPENSES/EXPENSE_PRODUCTS, PRODUCT_STOCKS, WASTE_STOCKS,
   DAMAGED_PRODUCTS, PRODUCT_PRODUCTION, PRODUCTPLANS(_DAILY), NORM_INPUT_LINES.
   Miqdor decimal(18,6), birlik narxi (FIFO tannarxi ham) decimal(19,6), hujjat summasi decimal(19,4), QQS stavkasi decimal(9,4).
   Hisoblangan: INCOME_PRODUCTS/EXPENSE_PRODUCTS.TotalPrice, PRODUCT_PRODUCTION/PRODUCTPLANS_DAILY.Total.
   Katta jadvalda ALTER COLUMN qulf oladi — prod'da texnik oynada.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO
-- ---------------------------------------------------------------------------------------------
-- Umumiy yordamchi (014/015/016 da bir xil): #F2D dagi float ustunlarni decimal'ga o'tkazadi.
--  * qiymat tekshiruvi: chegaradan oshsa yoki yaxlitlash yo'qotishi > 0.0001 bo'lsa THROW;
--    kichikroq farq (float "dumi", masalan 9053571.6000000015) yaxlitlanadi va soni PRINT qilinadi;
--  * bog'liq indekslar (ustun yoki unga tayangan hisoblangan ustun bo'yicha, INCLUDE ham), default'lar,
--    indeks bo'lmagan statistikalar va hisoblangan ustunlar sys.* dan skriptlanib o'chiriladi va
--    ALTER'dan keyin aynan qayta yaratiladi (hisoblangan ustun ta'rifi o'zgarmaydi — natija turi decimal bo'ladi);
--  * allaqachon decimal bo'lgan ustun o'tkazib yuboriladi (idempotent).
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#FloatToDecimal') IS NOT NULL DROP PROCEDURE #FloatToDecimal;
GO
CREATE PROCEDURE #FloatToDecimal @Tag nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    DECLARE @todo TABLE (Tbl sysname, Col sysname, Prec int, Scale int, ObjectId int, ColumnId int, IsNullable bit);
    INSERT @todo
    SELECT x.Tbl, x.Col, x.Prec, x.Scale, c.object_id, c.column_id, c.is_nullable
    FROM #F2D x
    JOIN sys.columns c ON c.object_id = OBJECT_ID('dbo.' + QUOTENAME(x.Tbl)) AND c.name = x.Col
    WHERE TYPE_NAME(c.user_type_id) IN ('float', 'real') AND c.is_computed = 0;

    IF NOT EXISTS (SELECT 1 FROM @todo)
    BEGIN
        PRINT @Tag + ': hamma ustunlar allaqachon decimal — o''zgarish yo''q';
        RETURN;
    END

    DECLARE @sql nvarchar(max), @tbl sysname, @col sysname, @prec int, @scale int, @nullable bit,
            @bad int, @rounded int, @msg nvarchar(400);

    -- 1) Ma'lumot tekshiruvi
    DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col, Prec, Scale FROM @todo;
    OPEN c1; FETCH NEXT FROM c1 INTO @tbl, @col, @prec, @scale;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'SELECT @bad = SUM(CASE WHEN ABS(' + QUOTENAME(@col) + N') >= POWER(CAST(10 AS float), ' + CAST(@prec - @scale AS nvarchar(5)) + N')'
                 + N' OR ABS(' + QUOTENAME(@col) + N' - ROUND(' + QUOTENAME(@col) + N', ' + CAST(@scale AS nvarchar(5)) + N')) > 0.0001 THEN 1 ELSE 0 END),'
                 + N' @rounded = SUM(CASE WHEN ' + QUOTENAME(@col) + N' <> ROUND(' + QUOTENAME(@col) + N', ' + CAST(@scale AS nvarchar(5)) + N') THEN 1 ELSE 0 END)'
                 + N' FROM dbo.' + QUOTENAME(@tbl) + N' WHERE ' + QUOTENAME(@col) + N' IS NOT NULL';
        EXEC sp_executesql @sql, N'@bad int OUTPUT, @rounded int OUTPUT', @bad = @bad OUTPUT, @rounded = @rounded OUTPUT;
        IF ISNULL(@bad, 0) > 0
        BEGIN
            SET @msg = @Tag + N': ' + @tbl + N'.' + @col + N' — ' + CAST(@bad AS nvarchar(10))
                + N' ta qiymat decimal(' + CAST(@prec AS nvarchar(5)) + N',' + CAST(@scale AS nvarchar(5)) + N') ga sig''maydi yoki 0.0001 dan ko''p yaxlitlanadi';
            THROW 50013, @msg, 1;
        END
        IF ISNULL(@rounded, 0) > 0
            PRINT @Tag + ': ' + @tbl + '.' + @col + ' — ' + CAST(@rounded AS varchar(10)) + ' ta qiymat ' + CAST(@scale AS varchar(5)) + ' xonaga yaxlitlanadi (float dumi)';
        FETCH NEXT FROM c1 INTO @tbl, @col, @prec, @scale;
    END
    CLOSE c1; DEALLOCATE c1;

    -- 2) Hisoblangan ustunlar (target ustunlarga tayanadi)
    DECLARE @comp TABLE (ObjectId int, ColumnId int, DropSql nvarchar(max), CreateSql nvarchar(max));
    INSERT @comp
    SELECT DISTINCT cc.object_id, cc.column_id,
           N'ALTER TABLE dbo.' + QUOTENAME(OBJECT_NAME(cc.object_id)) + N' DROP COLUMN ' + QUOTENAME(cc.name),
           N'ALTER TABLE dbo.' + QUOTENAME(OBJECT_NAME(cc.object_id)) + N' ADD ' + QUOTENAME(cc.name) + N' AS ' + cc.definition
           + CASE WHEN cc.is_persisted = 1 THEN N' PERSISTED' ELSE N'' END
    FROM sys.computed_columns cc
    JOIN sys.sql_expression_dependencies d ON d.referencing_id = cc.object_id AND d.referencing_minor_id = cc.column_id
    JOIN @todo t ON t.ObjectId = d.referenced_id AND t.ColumnId = d.referenced_minor_id;

    IF EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE (EXISTS (SELECT 1 FROM @todo t WHERE t.ObjectId = ic.object_id AND t.ColumnId = ic.column_id)
                   OR EXISTS (SELECT 1 FROM @comp k WHERE k.ObjectId = ic.object_id AND k.ColumnId = ic.column_id))
                 AND (i.is_primary_key = 1 OR i.is_unique_constraint = 1 OR i.type NOT IN (1, 2)))
        THROW 50014, N'ustun PK/UNIQUE constraint yoki rowstore bo''lmagan indeksda — qo''lda ko''rib chiqing', 1;

    -- 3) Indekslar
    DECLARE @idx TABLE (DropSql nvarchar(max), CreateSql nvarchar(max));
    INSERT @idx
    SELECT N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.' + QUOTENAME(OBJECT_NAME(i.object_id)),
           N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END
           + CASE WHEN i.type = 1 THEN N'CLUSTERED' ELSE N'NONCLUSTERED' END
           + N' INDEX ' + QUOTENAME(i.name) + N' ON dbo.' + QUOTENAME(OBJECT_NAME(i.object_id)) + N' ('
           + STUFF((SELECT N', ' + QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N' ASC' END
                    FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                    ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'') + N')'
           + ISNULL(N' INCLUDE (' + STUFF((SELECT N', ' + QUOTENAME(c.name)
                    FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                    ORDER BY ic.index_column_id FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'') + N')', N'')
           + CASE WHEN i.has_filter = 1 THEN N' WHERE ' + i.filter_definition ELSE N'' END
           + CASE WHEN i.fill_factor > 0 THEN N' WITH (FILLFACTOR = ' + CAST(i.fill_factor AS nvarchar(5)) + N')' ELSE N'' END
    FROM sys.indexes i
    WHERE i.index_id > 0
      AND EXISTS (SELECT 1 FROM sys.index_columns ic
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND (EXISTS (SELECT 1 FROM @todo t WHERE t.ObjectId = ic.object_id AND t.ColumnId = ic.column_id)
                      OR EXISTS (SELECT 1 FROM @comp k WHERE k.ObjectId = ic.object_id AND k.ColumnId = ic.column_id)));

    -- 4) Default'lar va statistikalar
    DECLARE @df TABLE (DropSql nvarchar(max), CreateSql nvarchar(max));
    INSERT @df
    SELECT N'ALTER TABLE dbo.' + QUOTENAME(t.Tbl) + N' DROP CONSTRAINT ' + QUOTENAME(d.name),
           N'ALTER TABLE dbo.' + QUOTENAME(t.Tbl) + N' ADD CONSTRAINT ' + QUOTENAME(d.name) + N' DEFAULT ' + d.definition + N' FOR ' + QUOTENAME(t.Col)
    FROM sys.default_constraints d JOIN @todo t ON t.ObjectId = d.parent_object_id AND t.ColumnId = d.parent_column_id;

    DECLARE @st TABLE (DropSql nvarchar(max));
    INSERT @st
    SELECT DISTINCT N'DROP STATISTICS dbo.' + QUOTENAME(OBJECT_NAME(s.object_id)) + N'.' + QUOTENAME(s.name)
    FROM sys.stats s JOIN sys.stats_columns sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id
    WHERE NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = s.object_id AND i.index_id = s.stats_id)
      AND (EXISTS (SELECT 1 FROM @todo t WHERE t.ObjectId = sc.object_id AND t.ColumnId = sc.column_id)
        OR EXISTS (SELECT 1 FROM @comp k WHERE k.ObjectId = sc.object_id AND k.ColumnId = sc.column_id));

    -- 5) O'chirish: indeks -> statistika -> default -> hisoblangan ustun
    DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT DropSql FROM @idx UNION ALL SELECT DropSql FROM @st UNION ALL SELECT DropSql FROM @df UNION ALL SELECT DropSql FROM @comp;
    OPEN c2; FETCH NEXT FROM c2 INTO @sql;
    WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM c2 INTO @sql; END
    CLOSE c2; DEALLOCATE c2;

    -- 6) ALTER COLUMN
    DECLARE c3 CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col, Prec, Scale, IsNullable FROM @todo;
    OPEN c3; FETCH NEXT FROM c3 INTO @tbl, @col, @prec, @scale, @nullable;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tbl) + N' ALTER COLUMN ' + QUOTENAME(@col)
                 + N' decimal(' + CAST(@prec AS nvarchar(5)) + N', ' + CAST(@scale AS nvarchar(5)) + N')'
                 + CASE WHEN @nullable = 1 THEN N' NULL' ELSE N' NOT NULL' END;
        EXEC (@sql);
        PRINT @Tag + ': ' + @sql;
        FETCH NEXT FROM c3 INTO @tbl, @col, @prec, @scale, @nullable;
    END
    CLOSE c3; DEALLOCATE c3;

    -- 7) Qayta yaratish: hisoblangan ustun -> default -> indeks
    DECLARE c4 CURSOR LOCAL FAST_FORWARD FOR
        SELECT CreateSql FROM @comp UNION ALL SELECT CreateSql FROM @df UNION ALL SELECT CreateSql FROM @idx;
    OPEN c4; FETCH NEXT FROM c4 INTO @sql;
    WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM c4 INTO @sql; END
    CLOSE c4; DEALLOCATE c4;

    -- 8) Jadvallarga tayangan ko'rinishlar metadata'si
    DECLARE @v sysname;
    DECLARE cv CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT OBJECT_SCHEMA_NAME(d.referencing_id) + '.' + OBJECT_NAME(d.referencing_id)
        FROM sys.sql_expression_dependencies d JOIN sys.views v ON v.object_id = d.referencing_id
        WHERE d.referenced_id IN (SELECT ObjectId FROM @todo) AND OBJECTPROPERTY(d.referencing_id, 'IsSchemaBound') = 0;
    OPEN cv; FETCH NEXT FROM cv INTO @v;
    WHILE @@FETCH_STATUS = 0 BEGIN EXEC sp_refreshview @v; FETCH NEXT FROM cv INTO @v; END
    CLOSE cv; DEALLOCATE cv;
END
GO

IF OBJECT_ID('tempdb..#F2D') IS NOT NULL DROP TABLE #F2D;
CREATE TABLE #F2D (Tbl sysname, Col sysname, Prec int, Scale int);
INSERT #F2D VALUES
    ('INCOMES',            'Amount',            18, 6),
    ('INCOMES',            'TotalPrice',        19, 4),
    ('INCOME_PRODUCTS',    'Amount',            18, 6),
    ('INCOME_PRODUCTS',    'PiecePrice',        19, 6),
    ('INCOME_PRODUCTS',    'VATRate',           9, 4),
    ('EXPENSES',           'Amount',            18, 6),
    ('EXPENSES',           'TotalPrice',        19, 4),
    ('EXPENSE_PRODUCTS',   'Amount',            18, 6),
    ('EXPENSE_PRODUCTS',   'PiecePrice',        19, 6),
    ('PRODUCT_STOCKS',     'Amount',            18, 6),
    ('WASTE_STOCKS',       'Amount',            18, 6),
    ('DAMAGED_PRODUCTS',   'Quantity',          18, 6),
    ('PRODUCT_PRODUCTION', 'DayShift',          18, 6),
    ('PRODUCT_PRODUCTION', 'NightShift',        18, 6),
    ('PRODUCTPLANS',       'Amount',            18, 6),
    ('PRODUCTPLANS_DAILY', 'DayShift',          18, 6),
    ('PRODUCTPLANS_DAILY', 'NightShift',        18, 6),
    ('NORM_INPUT_LINES',   'Consumption',       18, 6),
    ('NORM_INPUT_LINES',   'WasteAmount',       18, 6);

EXEC #FloatToDecimal @Tag = N'015';

DROP TABLE #F2D;
DROP PROCEDURE #FloatToDecimal;
GO
