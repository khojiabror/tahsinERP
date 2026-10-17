-- =====================================================================
-- DataFix MES-02 (TUZATUVCHI) — audit MES-B06, B08. Avval ..._Find.sql natijasini ko'rib chiqing!
--  A) Yetim NORM_INPUT_LINES (aktiv versiyasi yo'q davr) o'chiriladi. Ehtiyot nusxa #orphan_backup ga olinadi —
--     COMMIT'dan oldin kerak bo'lsa doimiy jadvalga ko'chiring.
--  B) Mavjud bo'lmagan normaga bog'langan BOMS_AND_NORMS qatorlari o'chiriladi.
--  C) BOM eski oy normasida bo'lsa (Find C/D) — SQL'da qayta qurilmaydi: mahsulotning eng so'nggi normasini
--     Norm sahifasida ochib "Saqlash" bosing — yangi kod BOM'ni eng so'nggi norma bo'yicha quradi
--     (normasi qolmagan mahsulotda BOM faolsizlantiriladi).
--
-- Tranzaksiya ichida; COMMIT yoki ROLLBACK ni o'zingiz tanlang. HECH QAYERDA AVTOMATIK ISHGA TUSHIRILMAGAN.
-- =====================================================================
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#orphan_backup') IS NOT NULL DROP TABLE #orphan_backup;
SELECT il.*
INTO #orphan_backup
FROM dbo.NORM_INPUT_LINES il
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.NORMVERSIONS nv
    WHERE nv.ProductID = il.ProductID
      AND ISNULL(nv.IsDeleted, 0) = 0
      AND nv.EffectiveFrom IS NOT NULL
      AND CONVERT(char(7), nv.EffectiveFrom, 126) = il.Month);

DELETE il
FROM dbo.NORM_INPUT_LINES il
JOIN #orphan_backup b ON b.ID = il.ID;
PRINT CONCAT(@@ROWCOUNT, ' ta yetim NORM_INPUT_LINES o''chirildi.');

DELETE bn
FROM dbo.BOMS_AND_NORMS bn
WHERE NOT EXISTS (SELECT 1 FROM dbo.NORMVERSIONS nv WHERE nv.ID = bn.NormID);
PRINT CONCAT(@@ROWCOUNT, ' ta yetim BOMS_AND_NORMS o''chirildi.');

SELECT * FROM #orphan_backup ORDER BY ProductID, Month;

-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
