/* =====================================================================
   011 — Moliya-logistika guruhi: float → decimal (Docs/Audit-2026-10/DB_FLOAT_DECIMAL_PLAN.md, 2-bosqich).

   F_WAYBILLS, F_WAYBILL_PAYMENTS, F_WAYBILL_PAYMENT_SPLITS, F_CONTRACT_TARIFFS, F_CONTRACTS:
     pul summasi      decimal(19,4)  — Amount, Payment_Amount, MinimumCharge
     tarif (birlik)   decimal(19,6)  — Rate
     hajm (m³)        decimal(18,6)  — CBM
     og'irlik/masofa  decimal(18,4)  — GrWeight, NetWeight, DistanceKm
   Nullability o'zgarmaydi. Ustunni o'z ichiga olgan indekslar (INCLUDE ham) va default'lar sys.* dan
   skriptlanib, o'chirilib, ALTER'dan keyin aynan qayta yaratiladi; ustundagi avtomatik statistikalar
   o'chiriladi (SQL Server ularni o'zi qayta yaratadi). Ko'rinishlar (V_TANNARX_ACTUAL_*_TRANSPORT) yangilanadi.

   Xavfsizlik: qiymat yangi tur chegarasidan oshsa yoki kasr xonasi yo'qolsa (|x - ROUND(x, scale)| > 1e-9)
   migratsiya to'xtaydi (THROW) — ma'lumot jim yaxlitlanmaydi. Idempotent: allaqachon decimal bo'lgan ustun o'tkazib yuboriladi.
   Katta jadvalda ALTER COLUMN qulf oladi — prod'da texnik oynada.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

DECLARE @cols TABLE (Tbl sysname, Col sysname, Prec int, Scale int);
INSERT @cols VALUES
    ('F_WAYBILLS',               'Amount',         19, 4),
    ('F_WAYBILLS',               'Rate',           19, 6),
    ('F_WAYBILLS',               'CBM',            18, 6),
    ('F_WAYBILLS',               'GrWeight',       18, 4),
    ('F_WAYBILLS',               'NetWeight',      18, 4),
    ('F_WAYBILLS',               'DistanceKm',     18, 4),
    ('F_WAYBILL_PAYMENTS',       'Payment_Amount', 19, 4),
    ('F_WAYBILL_PAYMENT_SPLITS', 'Amount',         19, 4),
    ('F_CONTRACT_TARIFFS',       'Rate',           19, 6),
    ('F_CONTRACT_TARIFFS',       'MinimumCharge',  19, 4),
    ('F_CONTRACTS',              'Amount',         19, 4);

-- faqat hali float bo'lganlari
DECLARE @todo TABLE (Tbl sysname, Col sysname, Prec int, Scale int, ObjectId int, ColumnId int, IsNullable bit);
INSERT @todo
SELECT x.Tbl, x.Col, x.Prec, x.Scale, c.object_id, c.column_id, c.is_nullable
FROM @cols x
JOIN sys.columns c ON c.object_id = OBJECT_ID('dbo.' + x.Tbl) AND c.name = x.Col
WHERE TYPE_NAME(c.user_type_id) IN ('float', 'real');

IF NOT EXISTS (SELECT 1 FROM @todo)
BEGIN
    PRINT '011: hamma ustunlar allaqachon decimal — o''zgarish yo''q';
    RETURN;
END

DECLARE @sql nvarchar(max), @tbl sysname, @col sysname, @prec int, @scale int, @nullable bit, @bad int;

-- 1) Ma'lumot tekshiruvi: chegara va kasr xonalari
DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col, Prec, Scale FROM @todo;
OPEN c1;
FETCH NEXT FROM c1 INTO @tbl, @col, @prec, @scale;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @bad = COUNT(*) FROM dbo.' + QUOTENAME(@tbl) + N' WHERE ' + QUOTENAME(@col) + N' IS NOT NULL AND ('
             + N'ABS(' + QUOTENAME(@col) + N') >= POWER(CAST(10 AS float), ' + CAST(@prec - @scale AS nvarchar(5)) + N')'
             + N' OR ABS(' + QUOTENAME(@col) + N' - ROUND(' + QUOTENAME(@col) + N', ' + CAST(@scale AS nvarchar(5)) + N')) > 1e-9)';
    EXEC sp_executesql @sql, N'@bad int OUTPUT', @bad = @bad OUTPUT;
    IF @bad > 0
    BEGIN
        DECLARE @msg nvarchar(400) = N'011: ' + @tbl + N'.' + @col + N' — ' + CAST(@bad AS nvarchar(10))
            + N' ta qiymat decimal(' + CAST(@prec AS nvarchar(5)) + N',' + CAST(@scale AS nvarchar(5)) + N') ga sig''maydi yoki yaxlitlanadi';
        THROW 50011, @msg, 1;
    END
    FETCH NEXT FROM c1 INTO @tbl, @col, @prec, @scale;
END
CLOSE c1; DEALLOCATE c1;

-- 2) Bog'liq indekslarni skriptlash (PK/UNIQUE constraint indekslari bu ustunlarda yo'q — bo'lsa to'xtaymiz)
IF EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
           JOIN @todo t ON t.ObjectId = ic.object_id AND t.ColumnId = ic.column_id
           WHERE i.is_primary_key = 1 OR i.is_unique_constraint = 1 OR i.type NOT IN (1, 2))
    THROW 50012, N'011: ustun PK/UNIQUE constraint yoki rowstore bo''lmagan indeksda — qo''lda ko''rib chiqing', 1;

DECLARE @idx TABLE (ObjectId int, IndexName sysname, DropSql nvarchar(max), CreateSql nvarchar(max));
INSERT @idx
SELECT i.object_id, i.name,
       N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.' + QUOTENAME(OBJECT_NAME(i.object_id)),
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
  AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN @todo t ON t.ObjectId = ic.object_id AND t.ColumnId = ic.column_id
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id);

-- default'lar
DECLARE @df TABLE (DropSql nvarchar(max), CreateSql nvarchar(max));
INSERT @df
SELECT N'ALTER TABLE dbo.' + QUOTENAME(t.Tbl) + N' DROP CONSTRAINT ' + QUOTENAME(d.name),
       N'ALTER TABLE dbo.' + QUOTENAME(t.Tbl) + N' ADD CONSTRAINT ' + QUOTENAME(d.name) + N' DEFAULT ' + d.definition + N' FOR ' + QUOTENAME(t.Col)
FROM sys.default_constraints d JOIN @todo t ON t.ObjectId = d.parent_object_id AND t.ColumnId = d.parent_column_id;

-- indeks bo'lmagan statistikalar (asosan _WA_Sys_*)
DECLARE @st TABLE (DropSql nvarchar(max));
INSERT @st
SELECT DISTINCT N'DROP STATISTICS dbo.' + QUOTENAME(OBJECT_NAME(s.object_id)) + N'.' + QUOTENAME(s.name)
FROM sys.stats s JOIN sys.stats_columns sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id
JOIN @todo t ON t.ObjectId = sc.object_id AND t.ColumnId = sc.column_id
WHERE NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = s.object_id AND i.index_id = s.stats_id);

-- 3) O'chirish
DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR
    SELECT DropSql FROM @idx UNION ALL SELECT DropSql FROM @df UNION ALL SELECT DropSql FROM @st;
OPEN c2; FETCH NEXT FROM c2 INTO @sql;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM c2 INTO @sql; END
CLOSE c2; DEALLOCATE c2;

-- 4) ALTER COLUMN
DECLARE c3 CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col, Prec, Scale, IsNullable FROM @todo;
OPEN c3; FETCH NEXT FROM c3 INTO @tbl, @col, @prec, @scale, @nullable;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tbl) + N' ALTER COLUMN ' + QUOTENAME(@col)
             + N' decimal(' + CAST(@prec AS nvarchar(5)) + N', ' + CAST(@scale AS nvarchar(5)) + N')'
             + CASE WHEN @nullable = 1 THEN N' NULL' ELSE N' NOT NULL' END;
    EXEC (@sql);
    PRINT '011: ' + @sql;
    FETCH NEXT FROM c3 INTO @tbl, @col, @prec, @scale, @nullable;
END
CLOSE c3; DEALLOCATE c3;

-- 5) Qayta yaratish
DECLARE c4 CURSOR LOCAL FAST_FORWARD FOR SELECT CreateSql FROM @df UNION ALL SELECT CreateSql FROM @idx;
OPEN c4; FETCH NEXT FROM c4 INTO @sql;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM c4 INTO @sql; END
CLOSE c4; DEALLOCATE c4;
GO

-- 6) Jadvallarga tayangan ko'rinishlar metadata'sini yangilash
DECLARE @v sysname;
DECLARE cv CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT OBJECT_SCHEMA_NAME(d.referencing_id) + '.' + OBJECT_NAME(d.referencing_id)
    FROM sys.sql_expression_dependencies d JOIN sys.views v ON v.object_id = d.referencing_id
    WHERE d.referenced_id IN (OBJECT_ID('dbo.F_WAYBILLS'), OBJECT_ID('dbo.F_WAYBILL_PAYMENTS'), OBJECT_ID('dbo.F_WAYBILL_PAYMENT_SPLITS'),
                              OBJECT_ID('dbo.F_CONTRACT_TARIFFS'), OBJECT_ID('dbo.F_CONTRACTS'))
      AND OBJECTPROPERTY(d.referencing_id, 'IsSchemaBound') = 0;
OPEN cv; FETCH NEXT FROM cv INTO @v;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC sp_refreshview @v; FETCH NEXT FROM cv INTO @v; END
CLOSE cv; DEALLOCATE cv;
GO
