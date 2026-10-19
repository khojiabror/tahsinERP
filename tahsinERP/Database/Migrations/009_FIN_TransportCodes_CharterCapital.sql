/* =====================================================================
   009 — moliya tizim bog'lanishlari (nom bo'yicha qidiruvni yo'qotish):
     1. FIN_CATEGORIES.SystemCode: MATERIAL_TRANSPORT (xomashyo tashuvi), FG_TRANSPORT (tayyor mahsulot tashuvi).
        FinancePostingService yo'l varaqasi to'lovini shu kategoriyaga yozadi. Avval "M.Tash"/"T.Tash" nomi bilan
        qidirardi — Seed/110_Defaults nomni o'zgartirgani (yoki foydalanuvchi qayta nomlagani) uchun har to'lovda
        yangi dublikat kategoriya yaratilardi.
     2. FIN_BALANCE_CATEGORY_CATEGORIES: CHARTER_CAPITAL <- SHAREHOLDER_CONTRIBUTION. Bog'lanishsiz aksiyador
        hissasi kassani oshirib, ustav kapitaliga tushmasdi (farq taqsimlanmagan foydaga ketardi).
   Idempotent; mavjud kod yoki bog'lanish bo'lsa tegmaydi.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1. Tashuv kategoriyalari: avval eski/yangi seed nomi, bo'lmasa seed ID'si (23 / 29), faqat xarajat turi
;WITH pick AS (
    SELECT v.Code,
           (SELECT TOP 1 c.ID FROM dbo.FIN_CATEGORIES c
            WHERE c.Type = 2 AND c.SystemCode IS NULL AND ISNULL(c.IsDeleted, 0) = 0
              AND (c.Name IN (v.Name1, v.Name2) OR c.ID = v.SeedID)
            ORDER BY CASE WHEN c.Name IN (v.Name1, v.Name2) THEN 0 ELSE 1 END, c.ID) AS ID
    FROM (VALUES ('MATERIAL_TRANSPORT', N'M.Tash', N'Material tashuvi', 23),
                 ('FG_TRANSPORT',       N'T.Tash', N'Tayyor mahsulot tashuvi', 29)) v (Code, Name1, Name2, SeedID)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.FIN_CATEGORIES x WHERE x.SystemCode = v.Code))
UPDATE c SET SystemCode = p.Code
FROM dbo.FIN_CATEGORIES c JOIN pick p ON p.ID = c.ID;
GO

-- 2. Ustav kapitali <- aksiyador hissalari
INSERT INTO dbo.FIN_BALANCE_CATEGORY_CATEGORIES (BalanceCategoryID, CategoryID)
SELECT b.ID, c.ID
FROM dbo.FIN_BALANCE_CATEGORIES b CROSS JOIN dbo.FIN_CATEGORIES c
WHERE b.SystemCode = 'CHARTER_CAPITAL' AND c.SystemCode = 'SHAREHOLDER_CONTRIBUTION'
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORY_CATEGORIES x WHERE x.BalanceCategoryID = b.ID AND x.CategoryID = c.ID);
GO
