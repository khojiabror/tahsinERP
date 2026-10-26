/* =====================================================================
   tahsinERP — DEMO: Sayt-katalog (Website, "+SITE") — sozlamalar, katalog, sahifalar, so'rovlar (290_Distribution_Orders.sql dan keyin).

   Areas/Site shaklida: site.SETTINGS (Enabled = 1, kompaniya rekvizitlari; Distributsiya narxlari bo'lsa PriceMode SHOW, aks holda
   ASK — "narxini so'rang"), tayyor mahsulot kategoriyalari va 6 mahsulot nashr qilingan (slug [a-z0-9-], tavsif oddiy matn),
   3 sahifa, 4 so'rov (Items — Json.NET PascalCase: ProductId, ProductUnitId, Qty, Sku, Name, Unit, Price).
   Faqat Website moduli tanlangan bo'lsa. Idempotent: site.PRODUCT_CONTENT bo'sh bo'lmasa o'tkazib yuboriladi. Bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF dbo.fn_SetupHasModule('Website') = 0 OR OBJECT_ID(N'demo_tmp.PROD') IS NULL OR EXISTS (SELECT 1 FROM site.PRODUCT_CONTENT)
BEGIN
    PRINT 'Demo sayt: modul tanlanmagan yoki ma''lumot bor — o''tkazib yuborildi';
    RETURN;
END

DECLARE @t date = CAST(GETDATE() AS date);
DECLARE @now datetime2(0) = DATEADD(hour, 9, CAST(@t AS datetime2(0)));
DECLARE @withPrices bit = CASE WHEN EXISTS (SELECT 1 FROM dst.PRICE_LIST_ITEMS) THEN 1 ELSE 0 END;

/* ---------------- sozlamalar (kalit/qiymat) ---------------- */
MERGE site.SETTINGS AS tg
USING (VALUES ('Enabled', N'1'), ('Name', N'"Demo Plast" MChJ'), ('Tagline', N'Uy-ro''zg''or va oziq-ovqat uchun plastik idishlar'),
              ('Phone', N'+998712000000'), ('Email', N'info@demo.tahsinerp.uz'), ('Address', N'Toshkent sh., Sergeli tumani, Sanoat ko''chasi 12'),
              ('WorkingHours', N'Du–Sha 9:00–18:00'), ('Telegram', N'demoplast_uz'),
              ('PriceMode', CASE WHEN @withPrices = 1 THEN N'SHOW' ELSE N'ASK' END), ('StockMode', N'STATUS'),
              ('SeoTitle', N'"Demo Plast" — plastik idishlar ishlab chiqaruvchi'),
              ('SeoDescription', N'Chelaklar, oziq-ovqat konteynerlari, plastik mebel — ulgurji va chakana.')) AS s ([Key], [Value])
ON tg.[Key] = s.[Key]
WHEN MATCHED THEN UPDATE SET [Value] = s.[Value], UpdatedAt = @now
WHEN NOT MATCHED THEN INSERT ([Key], [Value], UpdatedAt) VALUES (s.[Key], s.[Value], @now);

/* ---------------- katalog ---------------- */
INSERT INTO site.CATEGORY_CONTENT (CategoryID, IsPublished, Slug, NameUz, NameRu, DescriptionUz, DescriptionRu, MediaID, SortOrder, SeoTitle, SeoDescription, UpdatedAt)
SELECT c.ID, 1, v.Slug, c.Name, v.NameRu, v.DescrUz, NULL, NULL, v.Sort, NULL, NULL, @now
FROM (VALUES (N'Plastik idishlar', 'plastik-idishlar', N'Пластиковая посуда', N'Chelak, tog''ora, konteyner va savatlar.', 1),
             (N'Plastik mebel', 'plastik-mebel', N'Пластиковая мебель', N'Bog'' va kafe uchun stul va stollar.', 2)) v (CatName, Slug, NameRu, DescrUz, Sort)
JOIN dbo.PRODUCT_CATEGORIES c ON c.Name = v.CatName AND ISNULL(c.IsDeleted, 0) = 0;

INSERT INTO site.PRODUCT_CONTENT (ProductID, IsPublished, Slug, TitleUz, TitleRu, DescriptionUz, DescriptionRu, Badges, SortOrder, SeoTitle, SeoDescription, UpdatedAt)
SELECT p.ID, 1, v.Slug, NULL, v.TitleRu, v.DescrUz, NULL, v.Badges, v.Sort, NULL, NULL, @now
FROM (VALUES ('TM-001', 'chelak-5l-oq', N'Ведро 5 л белое', N'Oziq-ovqat uchun yaroqli polipropilen chelak.' + CHAR(10) + N'- hajmi: 5 litr' + CHAR(10) + N'- metall dasta', 'HIT', 1),
             ('TM-002', 'chelak-10l-oq', N'Ведро 10 л белое', N'Mustahkam **10 litrli** chelak qopqog''i bilan.', NULL, 2),
             ('TM-004', 'chelak-20l-uv', N'Ведро 20 л синее (UV)', N'Quyosh nuriga chidamli (UV stabilizator) qurilish chelagi.', 'NEW', 3),
             ('TM-007', 'konteyner-1l', N'Контейнер 1 л', N'Oziq-ovqat konteyneri, muzlatgich va mikroto''lqinli pech uchun.', NULL, 4),
             ('TM-011', 'plastik-stul', N'Пластиковый стул', N'Bog'' va kafe uchun yengil, mustahkam stul.' + CHAR(10) + N'- yuk ko''tarishi: 120 kg', 'SALE', 5),
             ('TM-012', 'plastik-stol', N'Пластиковый стол', N'Bog'' stoli, 4–6 kishilik.', NULL, 6)) v (PNo, Slug, TitleRu, DescrUz, Badges, Sort)
JOIN demo_tmp.PROD p ON p.PNo = v.PNo;

/* ---------------- sahifalar ---------------- */
INSERT INTO site.PAGES (Slug, TitleUz, TitleRu, BodyUz, BodyRu, IsPublished, ShowInMenu, SortOrder, UpdatedAt)
VALUES ('biz-haqimizda', N'Biz haqimizda', N'О нас',
        N'"Demo Plast" 2015-yildan buyon plastik idishlar ishlab chiqaradi.' + CHAR(10) + CHAR(10) + N'- 12 turdagi mahsulot' + CHAR(10) + N'- butun O''zbekiston bo''ylab yetkazib berish',
        N'«Demo Plast» производит пластиковую посуду с 2015 года.', 1, 1, 1, @now),
       ('yetkazib-berish', N'Yetkazib berish va to''lov', N'Доставка и оплата',
        N'Toshkent bo''ylab yetkazib berish — 1 kun ichida. Viloyatlarga — 2–3 kun.' + CHAR(10) + CHAR(10) + N'To''lov: naqd, karta yoki bank o''tkazmasi.',
        N'Доставка по Ташкенту — в течение дня, по областям — 2–3 дня.', 1, 1, 2, @now),
       ('aloqa', N'Aloqa', N'Контакты', N'Telefon: +998 71 200-00-00' + CHAR(10) + N'Manzil: Toshkent sh., Sergeli tumani, Sanoat ko''chasi 12', NULL, 1, 1, 3, @now);

/* ---------------- so'rovlar (2 yangi, 1 ko'rilgan, 1 rad etilgan) ---------------- */
DECLARE @p1 int = (SELECT ID FROM demo_tmp.PROD WHERE PNo = 'TM-001'), @p2 int = (SELECT ID FROM demo_tmp.PROD WHERE PNo = 'TM-011'),
        @p3 int = (SELECT ID FROM demo_tmp.PROD WHERE PNo = 'TM-007');
DECLARE @u int = (SELECT ID FROM dbo.USERS WHERE Uname = 'demo');
INSERT INTO site.LEADS (ClientRequestId, CreatedAt, Name, Phone, CompanyName, INN, Message, Items, Language, Host, Status, CustomerID, OrderID, ProcessedBy, ProcessedAt, RejectReason)
VALUES (NEWID(), DATEADD(hour, -3, @now), N'Abdullayev Sherzod', '+998901230011', N'"Sherzod Savdo" XK', NULL, N'Ulgurji narxlar kerak, oyiga 500 dona.',
        N'[{"ProductId":' + CAST(@p1 AS nvarchar(12)) + N',"ProductUnitId":null,"Qty":500,"Sku":"TM-001","Name":"Chelak 5 L oq","Unit":"dona","Price":null}]', 'uz', NULL, 0, NULL, NULL, NULL, NULL, NULL),
       (NEWID(), DATEADD(day, -1, @now), N'Ковалева Ирина', '+998935550022', N'Кафе «Лагман-хаус»', NULL, N'Нужны стулья для летней площадки.',
        N'[{"ProductId":' + CAST(@p2 AS nvarchar(12)) + N',"ProductUnitId":null,"Qty":40,"Sku":"TM-011","Name":"Plastik stul","Unit":"dona","Price":null}]', 'ru', NULL, 0, NULL, NULL, NULL, NULL, NULL),
       (NEWID(), DATEADD(day, -3, @now), N'Tursunova Dilfuza', '+998977770033', NULL, NULL, NULL,
        N'[{"ProductId":' + CAST(@p3 AS nvarchar(12)) + N',"ProductUnitId":null,"Qty":100,"Sku":"TM-007","Name":"Oziq-ovqat konteyneri 1 L","Unit":"dona","Price":null}]', 'uz', NULL, 1, NULL, NULL, @u, DATEADD(day, -2, @now), NULL),
       (NEWID(), DATEADD(day, -6, @now), N'Test', '+998900000044', NULL, NULL, N'test', NULL, 'uz', NULL, 3, NULL, NULL, @u, DATEADD(day, -6, @now), N'Sinov so''rovi');

PRINT 'Demo sayt: OK';
