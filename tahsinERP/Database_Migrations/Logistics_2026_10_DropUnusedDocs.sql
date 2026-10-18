/*
  LOG o'lik kod: F_WAYBILL_DOCS va F_CONTRACT_DOCS — tugallanmagan fayl biriktirish funksiyasi,
  hech bir UI o'qimaydi/yozmaydi (audit LOGISTICS.md "Dead code"). Foydalanuvchi ruxsati bilan (2026-10-19).
  Oldin: BACKUP DATABASE [DBTHSN-IPACK] ... DBTHSN-IPACK_pre_LOG_DOCS_20261019.bak (COPY_ONLY).
  Xavfsizlik: jadvalda qator bo'lsa o'chirilmaydi (xato bilan to'xtaydi). Takroran ishga tushirsa bo'ladi.
  EDMX va Models/F_*_DOCS.cs shu bilan birga olib tashlangan.
*/
SET NOCOUNT ON;

IF OBJECT_ID('dbo.F_WAYBILL_DOCS', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.F_WAYBILL_DOCS)
        THROW 50001, 'F_WAYBILL_DOCS bo''sh emas — o''chirilmadi.', 1;
    IF OBJECT_ID('dbo.FK_F_WAYBILL_DOCS_F_WAYBILLS', 'F') IS NOT NULL
        ALTER TABLE dbo.F_WAYBILL_DOCS DROP CONSTRAINT FK_F_WAYBILL_DOCS_F_WAYBILLS;
    DROP TABLE dbo.F_WAYBILL_DOCS;
    PRINT 'F_WAYBILL_DOCS — o''chirildi';
END
ELSE
    PRINT 'F_WAYBILL_DOCS — allaqachon yo''q';

IF OBJECT_ID('dbo.F_CONTRACT_DOCS', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.F_CONTRACT_DOCS)
        THROW 50002, 'F_CONTRACT_DOCS bo''sh emas — o''chirilmadi.', 1;
    IF OBJECT_ID('dbo.FK_F_CONTRACT_DOCS_F_CONTRACTS', 'F') IS NOT NULL
        ALTER TABLE dbo.F_CONTRACT_DOCS DROP CONSTRAINT FK_F_CONTRACT_DOCS_F_CONTRACTS;
    DROP TABLE dbo.F_CONTRACT_DOCS;
    PRINT 'F_CONTRACT_DOCS — o''chirildi';
END
ELSE
    PRINT 'F_CONTRACT_DOCS — allaqachon yo''q';

SELECT name FROM sys.tables WHERE name IN ('F_WAYBILL_DOCS', 'F_CONTRACT_DOCS');
