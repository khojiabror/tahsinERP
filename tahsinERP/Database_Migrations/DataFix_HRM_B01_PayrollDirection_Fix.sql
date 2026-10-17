-- HRM-B01: Ish haqi tranzaksiyalarining yo'nalishini tuzatish (1 = kirim -> 2 = chiqim).
-- AVVAL DataFix_HRM_B01_PayrollDirection_Find.sql natijasini tekshiring va bazaning zaxira nusxasini oling.
-- Hisob qoldiqlari FIN_TRANSACTIONS'dan hisoblanadi (alohida saqlanmaydi), shuning uchun boshqa jadval o'zgarmaydi.
-- Agar shu davr uchun yopilgan davr / akt-sverka / ochilish qoldig'i qo'lda kiritilgan bo'lsa, ularni alohida ko'rib chiqing.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @fixed TABLE (ID INT PRIMARY KEY);

UPDATE t
SET    t.Direction = 2
OUTPUT inserted.ID INTO @fixed(ID)
FROM dbo.FIN_TRANSACTIONS t
WHERE t.SourceDocType = 'HR_PAYROLL'
  AND t.Direction = 1;

SELECT COUNT(*) AS FixedCount FROM @fixed;
SELECT ID FROM @fixed ORDER BY ID;  -- tuzatilgan ID'larni saqlab qo'ying (qaytarish uchun)

-- Natija to'g'ri bo'lsa COMMIT, aks holda ROLLBACK qiling.
-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
