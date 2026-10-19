/* =====================================================================
   007 — Qolgan nom bo'yicha qidiruvlar o'rniga barqaror kodlar.

   Kod rol/kategoriya/birlikni NOMI bo'yicha tanirdi ("Developer", nomida "divid", "tashuv", "yog'och",
   "metiz"...) — foydalanuvchi nomni o'zgartirsa hisobot yoki huquq jimgina buzilardi. Endi:
     ROLES.SystemCode               ADMIN, DEVELOPER (UNIQUE) — RoleHelper.IsDeveloper/IsAdminOrDeveloper
     FIN_CATEGORIES.AnalyticsGroup  tahliliy guruh (UNIQUE EMAS, UI'da tanlanadi) — EBITDA (dividend, soliq,
                                    foiz, amortizatsiya) va fakt tannarx klassifikatori
     PRODUCT_CATEGORIES.MaterialKind  WOOD — norma/BOM'da yog'och (metr narxi, umumiy norma/chiqindi)
     PRODUCT_CATEGORIES.CalculateByWeight (mavjud) — metiz kategoriyalari uchun yoqiladi (avval nomidan
                                    "metiz/shurup/bolt..." deb aniqlanardi; flag faqat shu tekshiruvda ishlatiladi)
   Kod ro'yxati: Services/Common/SystemCodes.cs.

   To'ldirish (mavjud bazalar uchun, bir martalik): avvalgi runtime nom mantig'i xuddi shu tartibda shu yerda.
   Matn solishtiruvi LOWER(...) + Latin1_General_BIN2 — C# dagi ToLowerInvariant().Contains (ordinal) bilan bir xil.
   Yangi bazada qatorlar Seed/100_Reference.sql dan kodlari bilan keladi.
   ===================================================================== */

-- ---------- ustunlar ----------
IF COL_LENGTH('dbo.ROLES', 'SystemCode') IS NULL
    ALTER TABLE dbo.ROLES ADD SystemCode varchar(40) NULL;
IF COL_LENGTH('dbo.FIN_CATEGORIES', 'AnalyticsGroup') IS NULL
    ALTER TABLE dbo.FIN_CATEGORIES ADD AnalyticsGroup varchar(40) NULL;
IF COL_LENGTH('dbo.PRODUCT_CATEGORIES', 'MaterialKind') IS NULL
    ALTER TABLE dbo.PRODUCT_CATEGORIES ADD MaterialKind varchar(20) NULL;
GO

-- ---------- ROLES ----------
-- Avvalgi kod: string.Equals(RName, "Admin"/"Developer", OrdinalIgnoreCase). Bir nechta bo'lsa — eng kichik ID.
UPDATE r SET SystemCode = v.Code
FROM dbo.ROLES r
JOIN (VALUES ('ADMIN', N'admin'), ('DEVELOPER', N'developer')) v(Code, NameLower)
  ON r.ID = (SELECT MIN(x.ID) FROM dbo.ROLES x
             WHERE LOWER(x.RName) COLLATE Latin1_General_BIN2 = v.NameLower AND ISNULL(x.IsDeleted, 0) = 0)
WHERE r.SystemCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ROLES y WHERE y.SystemCode = v.Code);
GO

-- ---------- FIN_CATEGORIES.AnalyticsGroup ----------
-- 1) Fakt tannarx klassifikatori (TannarxFinanceCategoryClassifier.ClassifyOwn) — aynan o'sha tartib:
--    material -> chiqariladiganlar (moliyaviy, dividend, QQS, kapital, transport, ish haqi, xarid) -> soliq/bank -> boshqa.
-- 2) Qolganlari — EBITDA (ValuationService) nom qoidasi: foiz/ssuda -> INTEREST, amortizatsiya -> DEPRECIATION.
;WITH n AS (
    SELECT c.ID,
           LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(c.Name, N''))),
                 NCHAR(699), N''''), NCHAR(700), N''''), NCHAR(8216), N''''), NCHAR(8217), N''''), N'`', N''''))
               COLLATE Latin1_General_BIN2 AS t
    FROM dbo.FIN_CATEGORIES c
    WHERE c.AnalyticsGroup IS NULL
),
g AS (
    SELECT n.ID,
        CASE
            WHEN t LIKE N'%material%' AND t NOT LIKE N'%tash%' THEN 'MATERIAL'
            WHEN t LIKE N'%kredit%' OR t LIKE N'%o''tkazma%' OR t LIKE N'%otkazma%' OR t LIKE N'%naqd%' OR t LIKE N'%transfer%'
              OR t LIKE N'%qarz%' OR t LIKE N'%hisob raqam%' OR t LIKE N'%hisobraqam%' THEN 'FINANCING'
            WHEN t LIKE N'%divid%' OR t LIKE N'%aksiyador%' THEN 'DIVIDEND'
            WHEN t LIKE N'%qqs%' OR t LIKE N'%nds%' OR t LIKE N'%vat%' THEN 'VAT'
            WHEN t LIKE N'%asosiy vosita%' OR t LIKE N'%uskuna%' OR t LIKE N'%jihoz%' OR t LIKE N'%jixoz%' OR t LIKE N'%qurilish%'
              OR t LIKE N'%investitsiya%' OR t LIKE N'%kapital%' THEN 'CAPEX'
            WHEN t LIKE N'%tash%' OR t LIKE N'%transport%' OR t LIKE N'%logist%' OR t LIKE N'%yuk%' THEN 'LOGISTICS'
            WHEN t LIKE N'%oylik%' OR t LIKE N'%ish haqi%' OR t LIKE N'%ish xaqi%' OR t LIKE N'%maosh%' OR t LIKE N'%salary%'
              OR t LIKE N'%avans%' THEN 'PAYROLL'
            WHEN t LIKE N'%xarid%' OR t LIKE N'%ta''minotchi%' OR t LIKE N'%taminotchi%' OR t LIKE N'%supplier%' THEN 'PURCHASE'
            WHEN t LIKE N'%soliq%' OR t LIKE N'%solig%' THEN 'TAX'
            WHEN t LIKE N'%bank%' THEN 'BANK'
            WHEN t LIKE N'%boshqa%' THEN 'OTHER'
            WHEN t LIKE N'%foiz%' OR t LIKE N'%ssuda%' THEN 'INTEREST'
            WHEN t LIKE N'%amortizatsiya%' OR t LIKE N'%eskirish%' OR t LIKE N'%iznos%' THEN 'DEPRECIATION'
        END AS Grp
    FROM n
)
UPDATE c SET AnalyticsGroup = g.Grp
FROM dbo.FIN_CATEGORIES c
JOIN g ON g.ID = c.ID
WHERE g.Grp IS NOT NULL AND c.AnalyticsGroup IS NULL;
GO

-- 3) Dividend: EBITDA avval nomida "divid" bor P&L bandining kategoriyalarini operatsion xarajatdan chiqarardi.
--    Shunday bandga bog'langan va boshqa (dividend bo'lmagan) xarajat bandida yo'q kategoriya — DIVIDEND
--    (agar nomi tannarxda chiqariladigan guruhga tushgan yoki hech qayerga tushmagan bo'lsa).
;WITH items AS (
    SELECT i.ID, i.Sign,
           CASE WHEN LOWER(ISNULL(i.Name, N'')) COLLATE Latin1_General_BIN2 LIKE N'%divid%' THEN 1 ELSE 0 END AS IsDiv
    FROM dbo.FIN_PL_ITEMS i
    WHERE i.IsActive = 1 AND ISNULL(i.IsDeleted, 0) = 0
)
UPDATE c SET AnalyticsGroup = 'DIVIDEND'
FROM dbo.FIN_CATEGORIES c
WHERE ISNULL(c.AnalyticsGroup, '') <> 'DIVIDEND'
  AND (c.AnalyticsGroup IS NULL OR c.AnalyticsGroup IN ('FINANCING', 'VAT', 'CAPEX', 'LOGISTICS', 'PAYROLL', 'PURCHASE', 'INTEREST', 'DEPRECIATION'))
  AND EXISTS (SELECT 1 FROM dbo.FIN_PL_ITEM_CATEGORIES ic JOIN items it ON it.ID = ic.ItemID
              WHERE ic.CategoryID = c.ID AND it.Sign <> 1 AND it.IsDiv = 1)
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_PL_ITEM_CATEGORIES ic JOIN items it ON it.ID = ic.ItemID
                  WHERE ic.CategoryID = c.ID AND it.Sign <> 1 AND it.IsDiv = 0);
GO

-- ---------- PRODUCT_CATEGORIES ----------
-- Yog'och: avval NormLineMath.IsWoodCategory — RawMaterialGroup yoki Name da "yog'och"/"yo'goch".
UPDATE c SET MaterialKind = 'WOOD'
FROM dbo.PRODUCT_CATEGORIES c
WHERE c.MaterialKind IS NULL
  AND (LOWER(ISNULL(c.RawMaterialGroup, N'')) COLLATE Latin1_General_BIN2 LIKE N'%yog''och%'
    OR LOWER(ISNULL(c.RawMaterialGroup, N'')) COLLATE Latin1_General_BIN2 LIKE N'%yo''goch%'
    OR LOWER(ISNULL(c.Name, N'')) COLLATE Latin1_General_BIN2 LIKE N'%yog''och%'
    OR LOWER(ISNULL(c.Name, N'')) COLLATE Latin1_General_BIN2 LIKE N'%yo''goch%');

-- Metiz: avval UnitConversionService.IsMetizHardwareCategory — Name/RawMaterialGroup/RawMaterialSubGrp/Code da
-- "metiz/shurup/murvat/bolt/gaika/крепеж" bo'lsa CalculateByWeight yoqilgandek ishlardi.
UPDATE c SET CalculateByWeight = 1
FROM dbo.PRODUCT_CATEGORIES c
CROSS APPLY (SELECT LOWER(ISNULL(c.Name, N'') + N'|' + ISNULL(c.RawMaterialGroup, N'') + N'|'
                        + ISNULL(c.RawMaterialSubGrp, N'') + N'|' + ISNULL(c.Code, N'')) COLLATE Latin1_General_BIN2 AS t) x
WHERE ISNULL(c.CalculateByWeight, 0) = 0
  AND (x.t LIKE N'%metiz%' OR x.t LIKE N'%shurup%' OR x.t LIKE N'%murvat%' OR x.t LIKE N'%bolt%'
    OR x.t LIKE N'%gaika%' OR x.t LIKE N'%' + NCHAR(1082) + NCHAR(1088) + NCHAR(1077) + NCHAR(1087) + NCHAR(1077) + NCHAR(1078) + N'%');
GO

-- ---------- indekslar ----------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ROLES_SystemCode' AND object_id = OBJECT_ID('dbo.ROLES'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_ROLES_SystemCode ON dbo.ROLES (SystemCode) WHERE SystemCode IS NOT NULL;
GO
