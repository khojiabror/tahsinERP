/* =====================================================================
   001 — Tizim spravochnik qatorlari uchun barqaror SystemCode.

   Muammo: kod "Naqd pul", "Ta'minotchilardan qarzlar", Name.Contains("Sotuv"), UnitID == 1 kabi
   NOM / QATTIQ ID bo'yicha qidirardi — foydalanuvchi nomni o'zgartirsa hisobot jimgina buzilardi.
   Yechim: kod faqat SystemCode bo'yicha qidiradi; Name — faqat ko'rsatish uchun, erkin o'zgartiriladi.
   SystemCode bor qatorni UI'da o'chirib bo'lmaydi. Kod ro'yxati: Services/Common/SystemCodes.cs.

   To'ldirish (mavjud bazalar uchun, bir martalik): avvalgi kod qaysi qatorni topgan bo'lsa, xuddi
   shu qatorga kod beriladi (nom/ID mantiqi shu yerda — runtime kodda emas). Yangi bazada jadvallar
   bo'sh — qatorlar Seed/100_Reference.sql dan SystemCode bilan keladi.
   ===================================================================== */

-- ---------- ustunlar ----------
IF COL_LENGTH('dbo.FIN_BALANCE_CATEGORIES', 'SystemCode') IS NULL
    ALTER TABLE dbo.FIN_BALANCE_CATEGORIES ADD SystemCode varchar(40) NULL;
IF COL_LENGTH('dbo.FIN_CATEGORIES', 'SystemCode') IS NULL
    ALTER TABLE dbo.FIN_CATEGORIES ADD SystemCode varchar(40) NULL;
IF COL_LENGTH('dbo.PRODUCT_CATEGORIES', 'SystemCode') IS NULL
    ALTER TABLE dbo.PRODUCT_CATEGORIES ADD SystemCode varchar(40) NULL;
IF COL_LENGTH('dbo.UNITS', 'SystemCode') IS NULL
    ALTER TABLE dbo.UNITS ADD SystemCode varchar(40) NULL;
GO

-- ---------- FIN_BALANCE_CATEGORIES ----------
-- Avvalgi kod: aniq nom (BalanceSheetService, BalanceInventoryValuation, FinLiabilities, BalanceCategoryDisplayHelper),
-- oylik — nomida "oylik" (FinQueries.IsSalaryLiabilityCategory), logistika — "logistika" + "qarz".
;WITH rules AS (
    SELECT v.Code, v.Prio, c.ID
    FROM dbo.FIN_BALANCE_CATEGORIES c
    CROSS APPLY (VALUES
        ('CASH',                  CASE WHEN LTRIM(RTRIM(c.Name)) = N'Naqd pul' THEN 1 END),
        ('BANK',                  CASE WHEN LTRIM(RTRIM(c.Name)) = N'Bank hisobi' THEN 1 END),
        ('PAYABLES',              CASE WHEN c.Name = N'Ta''minotchilardan qarzlar' THEN 1 END),
        ('RECEIVABLES',           CASE WHEN c.Name = N'Xaridorlardan haqlarimiz' THEN 1 END),
        ('RAW_MATERIALS',         CASE WHEN LTRIM(RTRIM(c.Name)) = N'Xomashyo' THEN 1 END),
        ('FINISHED_GOODS',        CASE WHEN LTRIM(RTRIM(c.Name)) = N'Tayyor maxsulot' THEN 1 END),
        ('EQUIPMENT',             CASE WHEN LTRIM(RTRIM(c.Name)) = N'Jihozlar' THEN 1 END),
        ('BUILDINGS',             CASE WHEN LTRIM(RTRIM(c.Name)) = N'Bino' THEN 1 END),
        ('CHARTER_CAPITAL',       CASE WHEN LTRIM(RTRIM(c.Name)) = N'Ustav kapitali' THEN 1 END),
        ('RECURRING_OBLIGATIONS', CASE WHEN c.Name = N'Doimiy harajatlar qarzi' THEN 1 END),
        ('PAYROLL_DEBT',          CASE WHEN c.CalculationType = 3 AND c.Name LIKE N'%oylik%' THEN 1 END),
        ('LOGISTICS_DEBT',        CASE WHEN c.CalculationType = 3 AND c.Name LIKE N'%logistika%' AND c.Name LIKE N'%qarz%' THEN 1 END)
    ) v(Code, Prio)
    WHERE v.Prio IS NOT NULL AND c.IsActive = 1 AND ISNULL(c.IsDeleted, 0) = 0
),
pick AS (
    -- Balans kategoriyalarni SortOrder, ID tartibida ko'rib chiqardi — birinchisi olinadi
    SELECT r.Code, r.ID, ROW_NUMBER() OVER (PARTITION BY r.Code ORDER BY c.SortOrder, c.ID) AS rn
    FROM rules r JOIN dbo.FIN_BALANCE_CATEGORIES c ON c.ID = r.ID
)
UPDATE c SET SystemCode = p.Code
FROM dbo.FIN_BALANCE_CATEGORIES c
JOIN pick p ON p.ID = c.ID AND p.rn = 1
WHERE c.SystemCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_BALANCE_CATEGORIES x WHERE x.SystemCode = p.Code)
  AND NOT EXISTS (SELECT 1 FROM pick p2 WHERE p2.ID = p.ID AND p2.rn = 1 AND p2.Code < p.Code); -- bitta qatorga bitta kod
GO

-- ---------- FIN_CATEGORIES ----------
-- SALES: FinancePostingService.PostSalesInvoice — Type=1, nomida "Sotuv" (aniq "Sotuv" ustun), eng kichik ID
-- PURCHASE: PostPurchaseInvoice — Type=2, nomida "Xarid"
-- PAYROLL: FinQueries.SalaryCategoryIds / HrPayroll — nomida "oylik" yoki "ish haqi" (chiqim ustun)
-- SHAREHOLDER_CONTRIBUTION: ShareholderController — Type=1, nomida "Aksiyador"
;WITH rules AS (
    SELECT v.Code, v.Prio, c.ID, c.SortOrder
    FROM dbo.FIN_CATEGORIES c
    CROSS APPLY (VALUES
        ('SALES',                    CASE WHEN c.Type = 1 AND LTRIM(RTRIM(c.Name)) = N'Sotuv' THEN 1
                                          WHEN c.Type = 1 AND c.Name LIKE N'%Sotuv%' THEN 2 END),
        ('PURCHASE',                 CASE WHEN c.Type = 2 AND c.Name LIKE N'%Xarid%' THEN 1 END),
        ('PAYROLL',                  CASE WHEN c.Type = 2 AND (c.Name LIKE N'%oylik%' OR c.Name LIKE N'%ish haqi%') THEN 1
                                          WHEN c.Name LIKE N'%oylik%' THEN 2 END),
        ('SHAREHOLDER_CONTRIBUTION', CASE WHEN c.Type = 1 AND c.Name LIKE N'%Aksiyador%' THEN 1 END)
    ) v(Code, Prio)
    WHERE v.Prio IS NOT NULL AND c.IsActive = 1 AND ISNULL(c.IsDeleted, 0) = 0
),
pick AS (
    SELECT Code, ID, ROW_NUMBER() OVER (PARTITION BY Code ORDER BY Prio, ID) AS rn FROM rules
)
UPDATE c SET SystemCode = p.Code
FROM dbo.FIN_CATEGORIES c
JOIN pick p ON p.ID = c.ID AND p.rn = 1
WHERE c.SystemCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.FIN_CATEGORIES x WHERE x.SystemCode = p.Code)
  AND NOT EXISTS (SELECT 1 FROM pick p2 WHERE p2.ID = p.ID AND p2.rn = 1 AND p2.Code < p.Code);
GO

-- ---------- PRODUCT_CATEGORIES ----------
-- EQUIPMENT: BalanceInventoryValuation xomashyo bahosidan chiqaradigan "Jihoz" kategoriyasi
UPDATE c SET SystemCode = 'EQUIPMENT'
FROM dbo.PRODUCT_CATEGORIES c
WHERE c.ID = (SELECT MIN(ID) FROM dbo.PRODUCT_CATEGORIES WHERE LTRIM(RTRIM(Name)) = N'Jihoz')
  AND c.SystemCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PRODUCT_CATEGORIES x WHERE x.SystemCode = 'EQUIPMENT');
GO

-- ---------- UNITS ----------
-- Kod UnitID == 1 ni "dona", 11 ni "m3" deb hisoblardi. OKEI kodi (UNITS.Code) yoki qisqa nom bo'yicha.
;WITH rules AS (
    SELECT v.SysCode, v.Prio, u.ID
    FROM dbo.UNITS u
    CROSS APPLY (VALUES
        ('PCS',   CASE WHEN u.Code = '796' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'dona' THEN 2 END),
        ('KG',    CASE WHEN u.Code = '166' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'kg' THEN 2 END),
        ('TONNE', CASE WHEN u.Code = '168' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) IN (N'tn', N't') THEN 2 END),
        ('GRAM',  CASE WHEN u.Code = '163' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) IN (N'gr', N'g') THEN 2 END),
        ('METER', CASE WHEN u.Code = '006' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'm' THEN 2 END),
        ('M2',    CASE WHEN u.Code = '055' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'm2' THEN 2 END),
        ('M3',    CASE WHEN u.Code = '113' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'm3' THEN 2 END),
        ('LITER', CASE WHEN u.Code = '112' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'l' THEN 2 END),
        ('MM',    CASE WHEN u.Code = '003' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'mm' THEN 2 END),
        ('CM',    CASE WHEN u.Code = '004' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'cm' THEN 2 END),
        ('PAIR',  CASE WHEN u.Code = '715' THEN 1 WHEN LTRIM(RTRIM(u.ShortName)) = N'juft' THEN 2 END)
    ) v(SysCode, Prio)
    WHERE v.Prio IS NOT NULL AND ISNULL(u.IsDeleted, 0) = 0
),
pick AS (
    SELECT SysCode, ID, ROW_NUMBER() OVER (PARTITION BY SysCode ORDER BY Prio, ID) AS rn FROM rules
)
UPDATE u SET SystemCode = p.SysCode
FROM dbo.UNITS u
JOIN pick p ON p.ID = u.ID AND p.rn = 1
WHERE u.SystemCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.UNITS x WHERE x.SystemCode = p.SysCode)
  AND NOT EXISTS (SELECT 1 FROM pick p2 WHERE p2.ID = p.ID AND p2.rn = 1 AND p2.SysCode < p.SysCode);
GO

-- ---------- noyoblik ----------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FIN_BALANCE_CATEGORIES_SystemCode' AND object_id = OBJECT_ID('dbo.FIN_BALANCE_CATEGORIES'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_FIN_BALANCE_CATEGORIES_SystemCode ON dbo.FIN_BALANCE_CATEGORIES (SystemCode) WHERE SystemCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FIN_CATEGORIES_SystemCode' AND object_id = OBJECT_ID('dbo.FIN_CATEGORIES'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_FIN_CATEGORIES_SystemCode ON dbo.FIN_CATEGORIES (SystemCode) WHERE SystemCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PRODUCT_CATEGORIES_SystemCode' AND object_id = OBJECT_ID('dbo.PRODUCT_CATEGORIES'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_PRODUCT_CATEGORIES_SystemCode ON dbo.PRODUCT_CATEGORIES (SystemCode) WHERE SystemCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_UNITS_SystemCode' AND object_id = OBJECT_ID('dbo.UNITS'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_UNITS_SystemCode ON dbo.UNITS (SystemCode) WHERE SystemCode IS NOT NULL;
GO
