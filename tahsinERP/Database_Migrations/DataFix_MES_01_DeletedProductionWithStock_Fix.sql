-- =====================================================================
-- DataFix MES-01 (TUZATUVCHI) — audit MES-B01. Avval ..._Find.sql natijasini ko'rib chiqing!
--
-- Qoldiqni SQL'da qo'lda qaytarish xavfli (DLOC lotlari, FIFO taqsimoti, PRODUCT_STOCKS) — buni ilova qiladi.
-- Shu sabab bu skript faqat ko'rinmaydigan yozuvni QAYTA KO'RINADIGAN qiladi (IsDeleted = 0).
-- Keyin har birini ProductProduction sahifasidan tekshiring: kerak bo'lsa ilovadagi "O'chirish" tugmasi bilan
-- o'chiring — u kirim va xomashyo chiqimini to'g'ri bekor qiladi (endi bitta tranzaksiyada).
--
-- Tranzaksiya ichida; natijani ko'rib COMMIT yoki ROLLBACK qiling. HECH QAYERDA AVTOMATIK ISHGA TUSHIRILMAGAN.
-- =====================================================================
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#fix') IS NOT NULL DROP TABLE #fix;
SELECT p.ID
INTO #fix
FROM dbo.PRODUCT_PRODUCTION p
WHERE p.IsDeleted = 1
  AND (EXISTS (SELECT 1 FROM dbo.INCOMES i WHERE ISNULL(i.IsDeleted,0) = 0
            AND (i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR i.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20))))
    OR EXISTS (SELECT 1 FROM dbo.EXPENSES e WHERE ISNULL(e.IsDeleted,0) = 0 AND e.ReceiverWhID IS NULL
            AND (e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)) + '[^0-9]%' OR e.Description LIKE '%ProductionID:' + CAST(p.ID AS varchar(20)))));

UPDATE p
SET p.IsDeleted = 0
FROM dbo.PRODUCT_PRODUCTION p
JOIN #fix f ON f.ID = p.ID;

PRINT CONCAT(@@ROWCOUNT, ' ta ishlab chiqarish qayta ko''rinadigan qilindi.');
SELECT p.* FROM dbo.PRODUCT_PRODUCTION p JOIN #fix f ON f.ID = p.ID;

-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
