-- =====================================================================
-- DataFix MES-02 (TOPUVCHI, faqat SELECT) — audit MES-B06, B07, B08.
--  A) Yetim NORM_INPUT_LINES: (ProductID, Month) uchun aktiv NORMVERSION yo'q. Eski kod norma davri
--     o'zgarganda eski davr qatorlarini qoldirib ketardi (va boshqa versiya qatorlarini o'chirardi).
--     Yetim qatorlar NORMLINES yig'indisi va BOM narx/yog'och hisobiga aralashadi.
--  B) BOMS_AND_NORMS: mavjud bo'lmagan NORMVERSION'ga bog'langan qatorlar (norma o'chirilganda qolgan).
--  C) BOM_HEADERS.VersionNo mahsulotning eng so'nggi normasi davriga teng emas — BOM eski oy normasi bilan
--     ustidan yozilgan (sarf noto'g'ri normadan hisoblanmoqda).
-- Ma'lumot o'zgarmaydi.
-- =====================================================================
SET NOCOUNT ON;

-- A) Yetim norma qatorlari
SELECT il.ProductID, pr.PNo, il.Month, COUNT(*) AS Lines, MIN(il.CreatedDT) AS FirstCreated, MAX(il.CreatedDT) AS LastCreated
FROM dbo.NORM_INPUT_LINES il
JOIN dbo.PRODUCTS pr ON pr.ID = il.ProductID
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.NORMVERSIONS nv
    WHERE nv.ProductID = il.ProductID
      AND ISNULL(nv.IsDeleted, 0) = 0
      AND nv.EffectiveFrom IS NOT NULL
      AND CONVERT(char(7), nv.EffectiveFrom, 126) = il.Month)
GROUP BY il.ProductID, pr.PNo, il.Month
ORDER BY pr.PNo, il.Month;

-- B) Yetim BOM ↔ norma bog'lanishlari (jadval hamma bazada yo'q)
IF OBJECT_ID('dbo.BOMS_AND_NORMS', 'U') IS NOT NULL
    EXEC('SELECT bn.* FROM dbo.BOMS_AND_NORMS bn WHERE NOT EXISTS (SELECT 1 FROM dbo.NORMVERSIONS nv WHERE nv.ID = bn.NormID)');

-- C) BOM eng so'nggi normada emas
;WITH LatestNorm AS (
    SELECT nv.ProductID, nv.ID AS NormID, CONVERT(char(7), nv.EffectiveFrom, 126) AS Period,
           ROW_NUMBER() OVER (PARTITION BY nv.ProductID ORDER BY nv.EffectiveFrom DESC, nv.ID DESC) AS rn
    FROM dbo.NORMVERSIONS nv
    WHERE ISNULL(nv.IsDeleted, 0) = 0 AND nv.EffectiveFrom IS NOT NULL
)
SELECT bh.ID AS BomID, bh.ProductID, pr.PNo, bh.VersionNo AS BomVersion, ln.Period AS LatestNormPeriod, ln.NormID AS LatestNormID, bh.IsActive
FROM dbo.BOM_HEADERS bh
JOIN dbo.PRODUCTS pr ON pr.ID = bh.ProductID
JOIN LatestNorm ln ON ln.ProductID = bh.ProductID AND ln.rn = 1
WHERE ISNULL(bh.VersionNo, '') <> ln.Period
ORDER BY pr.PNo;

-- D) Normasi umuman qolmagan, lekin faol BOM'i bor mahsulotlar
SELECT bh.ID AS BomID, bh.ProductID, pr.PNo, bh.VersionNo
FROM dbo.BOM_HEADERS bh
JOIN dbo.PRODUCTS pr ON pr.ID = bh.ProductID
WHERE bh.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.NORMVERSIONS nv WHERE nv.ProductID = bh.ProductID AND ISNULL(nv.IsDeleted, 0) = 0);
