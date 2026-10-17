/*
  LOG-B05: yo'l varaqasida tanlangan tarifni saqlash (F_WAYBILLS.TariffID).
  Avval Edit shartnoma + transport turi bo'yicha eng so'nggi faol tarifni "taxmin qilardi"; endi saqlangan tarif ishlatiladi.
  Takroran ishga tushirsa bo'ladi (idempotent). EDMX'da F_WAYBILLS.TariffID skalyar xossasi qo'shilgan.

  Eski yozuvlarni to'ldirish: shu shartnomaning, shu transport turidagi tarifi; asos va stavka mos kelgani ustun,
  so'ng faol, so'ng eng yangisi. Mos tarif bo'lmasa NULL qoladi (Edit'da foydalanuvchi tanlaydi).
*/
SET NOCOUNT ON;

IF COL_LENGTH('dbo.F_WAYBILLS', 'TariffID') IS NULL
BEGIN
    ALTER TABLE dbo.F_WAYBILLS ADD TariffID INT NULL;
    PRINT 'F_WAYBILLS.TariffID — qo''shildi';
END
ELSE
    PRINT 'F_WAYBILLS.TariffID — allaqachon bor';

IF OBJECT_ID('dbo.FK_F_WAYBILLS_F_CONTRACT_TARIFFS', 'F') IS NULL
BEGIN
    EXEC('ALTER TABLE dbo.F_WAYBILLS ADD CONSTRAINT FK_F_WAYBILLS_F_CONTRACT_TARIFFS
          FOREIGN KEY (TariffID) REFERENCES dbo.F_CONTRACT_TARIFFS(ID)');
    PRINT 'FK_F_WAYBILLS_F_CONTRACT_TARIFFS — yaratildi';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.F_WAYBILLS') AND name = 'IX_F_WAYBILLS_TariffID')
BEGIN
    EXEC('CREATE INDEX IX_F_WAYBILLS_TariffID ON dbo.F_WAYBILLS(TariffID) WHERE TariffID IS NOT NULL');
    PRINT 'IX_F_WAYBILLS_TariffID — yaratildi';
END

EXEC('
UPDATE w
SET w.TariffID = t.ID
FROM dbo.F_WAYBILLS w
CROSS APPLY (
    SELECT TOP (1) ct.ID
    FROM dbo.F_CONTRACT_TARIFFS ct
    WHERE ct.ContractID = w.ContractID
      AND (ct.TransportTypeID = w.TransportTypeID OR ct.TransportTypeID IS NULL)
    ORDER BY
      CASE WHEN ct.FreightBasis = w.FreightBasis AND ABS(ct.Rate - ISNULL(w.Rate, -1)) < 0.005 THEN 0 ELSE 1 END,
      CASE WHEN ct.TransportTypeID = w.TransportTypeID THEN 0 ELSE 1 END,
      CASE WHEN ct.IsActive = 1 THEN 0 ELSE 1 END,
      ct.CreatedDT DESC
) t
WHERE w.TariffID IS NULL AND w.ContractID IS NOT NULL;
PRINT CONCAT(''Backfill: '', @@ROWCOUNT, '' ta yo''''l varaqasiga tarif bog''''landi'');
');

EXEC('
SELECT COUNT(*) AS Jami,
       SUM(CASE WHEN TariffID IS NOT NULL THEN 1 ELSE 0 END) AS TarifBilan,
       SUM(CASE WHEN TariffID IS NULL AND ContractID IS NOT NULL THEN 1 ELSE 0 END) AS ShartnomaBorTarifYoq
FROM dbo.F_WAYBILLS
WHERE ISNULL(IsDeleted, 0) = 0;
');
