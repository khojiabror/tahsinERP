/* =====================================================================
   027 — Distributsiya: qo'llanma yozilganda topilgan kamchiliklar tuzatishi (Docs/Distribution/05_FINDINGS.md).

     - K1-1: maxsus huquqlar (DstSeeCost, DstOverrideCredit, DstDiscountUnlimited, DstWriteOffApprove) endi
       Change ustuni bilan tekshiriladi (yangi rolga hamma modulda View avtomatik beriladi). Mavjud egalar saqlanadi:
       View bor qatorlarga Change ham qo'yiladi.
   Idempotent.
   ===================================================================== */

-- ---------- K1-1: maxsus huquqlar — View egalari Change oladi ----------
UPDATE p SET p.ChangePermit = 1
FROM dbo.PERMISSIONS p
JOIN dbo.PERMISSIONMODULES pm ON pm.ID = p.PermissionModuleID
WHERE pm.Module IN ('DstSeeCost', 'DstOverrideCredit', 'DstDiscountUnlimited', 'DstWriteOffApprove')
  AND p.ViewPermit = 1 AND p.ChangePermit = 0;
GO

-- ---------- K2-21: ko'chirma kamomadini bo'lish (yo'qotish + qaytarish) ----------
IF COL_LENGTH('dst.TRANSFER_ACT_LINES', 'LossQty') IS NULL
    ALTER TABLE dst.TRANSFER_ACT_LINES ADD LossQty decimal(18, 6) NULL;   -- kamomadning yo'qotilgan qismi
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DST_TRANSFER_ACT_LINES_Res'
                                                AND definition NOT LIKE '%SPLIT%')
    ALTER TABLE dst.TRANSFER_ACT_LINES DROP CONSTRAINT CK_DST_TRANSFER_ACT_LINES_Res;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DST_TRANSFER_ACT_LINES_Res')
    ALTER TABLE dst.TRANSFER_ACT_LINES ADD CONSTRAINT CK_DST_TRANSFER_ACT_LINES_Res
        CHECK (Resolution IS NULL OR Resolution IN ('LOSS', 'RETURN', 'SPLIT'));
GO
-- mavjud qatorlar: LOSS — butun kamomad, RETURN — 0
UPDATE dst.TRANSFER_ACT_LINES SET LossQty = CASE WHEN Resolution = 'LOSS' THEN SentQty - ReceivedQty ELSE 0 END
WHERE LossQty IS NULL AND Resolution IN ('LOSS', 'RETURN');
GO

-- ---------- K2-24: mijoz bildirishnomalari tili ----------
IF COL_LENGTH('dst.TELEGRAM_ACCOUNTS', 'Language') IS NULL
    ALTER TABLE dst.TELEGRAM_ACCOUNTS ADD Language varchar(5) NULL;   -- mijoz: Telegram tili (uz/ru); xodim — USER_SETTINGS
GO

-- ---------- K3-33: audit jurnali ekrani (DstAudit) — Admin'ga; bo'sh baza — Seed/105 ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT 'DstAudit', 'DstAudit', 'Index', NULL
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = 'DstAudit');

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module = 'DstAudit'
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO

-- ---------- K2-21: dalolatnoma bekor qilinsa, ko'chirma qayta qabul qilinadi — bitta kirimga bitta AMALDAGI dalolatnoma ----------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DST_TRANSFER_ACTS_Income' AND object_id = OBJECT_ID('dst.TRANSFER_ACTS') AND has_filter = 0)
    DROP INDEX UX_DST_TRANSFER_ACTS_Income ON dst.TRANSFER_ACTS;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DST_TRANSFER_ACTS_Income' AND object_id = OBJECT_ID('dst.TRANSFER_ACTS'))
    CREATE UNIQUE INDEX UX_DST_TRANSFER_ACTS_Income ON dst.TRANSFER_ACTS (IncomeID) WHERE Status <> 2;   -- 2 = bekor qilingan
GO
