/* =====================================================================
   tahsinERP — ixcham namuna: Sayt-katalog — sayt nomi (korxona nomi), "Biz haqimizda" sahifasi, namuna mahsulot katalogda.
   Sayt o'chiq holda qoladi (Enabled = 0) — mijoz logotip va kontaktlarni kiritib o'zi yoqadi.
   Idempotent: site.PAGES bo'sh bo'lmasa o'tkazib yuboriladi.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Website') = 0 OR EXISTS (SELECT 1 FROM site.PAGES)
BEGIN
    PRINT 'Namuna sayt: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @now datetime2(0) = SYSDATETIME();
DECLARE @company nvarchar(200) = ISNULL((SELECT TOP 1 Name FROM dbo.COMPANIES ORDER BY ID), N'Korxona');

MERGE site.SETTINGS AS tg
USING (VALUES ('Name', @company)) AS s ([Key], [Value])
ON tg.[Key] = s.[Key]
WHEN MATCHED AND ISNULL(tg.[Value], N'') = N'' THEN UPDATE SET [Value] = s.[Value], UpdatedAt = @now
WHEN NOT MATCHED THEN INSERT ([Key], [Value], UpdatedAt) VALUES (s.[Key], s.[Value], @now);

INSERT INTO site.PAGES (Slug, TitleUz, TitleRu, BodyUz, BodyRu, IsPublished, ShowInMenu, SortOrder, UpdatedAt)
VALUES ('biz-haqimizda', N'Biz haqimizda', N'О нас',
        N'Bu namuna sahifa — kompaniyangiz haqida yozing: nima bilan shug''ullanasiz, qayerda joylashgansiz, qanday yetkazib berasiz.',
        N'Это пример страницы — расскажите о своей компании.', 1, 1, 1, @now);

INSERT INTO site.PRODUCT_CONTENT (ProductID, IsPublished, Slug, TitleUz, TitleRu, DescriptionUz, DescriptionRu, Badges, SortOrder, SeoTitle, SeoDescription, UpdatedAt)
SELECT p.ID, 1, 'namuna-tovar', NULL, NULL, N'Namuna tovar — tavsif, rasm va xususiyatlarni Sayt → Katalog bo''limida kiriting.', NULL, NULL, 1, NULL, NULL, @now
FROM dbo.PRODUCTS p WHERE p.PNo = 'NAM-001' AND p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM site.PRODUCT_CONTENT x WHERE x.ProductID = p.ID);
