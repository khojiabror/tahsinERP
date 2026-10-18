/*
 FIN-B01 tayyorgarligi: so'mda kiritilgan, lekin boshqa valyuta (USD) deb belgilangan opening qatorlari
 va snapshot'larini so'mga (UZS) o'tkazadi. Faqat kontragent hujjatlari so'mda bo'lgan bazada ishlating —
 avval DataFix_Finance_OpeningCurrency_Check.sql natijasini ko'ring.
 Zaxira: eski qiymatlar *_CURRENCY_BAK_20261019 jadvallariga saqlanadi. Qayta ishga tushirsa bo'ladi.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @uzs INT = (SELECT TOP 1 ID FROM dbo.CURRENCIES WHERE Shortname IN ('UZS','SUM','SO''M') ORDER BY ID);
IF @uzs IS NULL THROW 50001, 'UZS valyutasi topilmadi', 1;

-- Hujjatlarning hammasi so'mda ekanini tekshiramiz (aks holda opening chindan valyutada bo'lishi mumkin)
IF EXISTS (SELECT 1 FROM dbo.P_INVOICES WHERE ISNULL(IsDeleted,0)=0 AND CurrencyID IS NOT NULL AND CurrencyID <> @uzs)
   OR EXISTS (SELECT 1 FROM dbo.SALES_INVOICES WHERE CurrencyID IS NOT NULL AND CurrencyID <> @uzs)
    THROW 50002, 'So''mdan boshqa valyutadagi invoyslar bor — opening valyutasini qo''lda tekshiring', 1;

IF OBJECT_ID('dbo.FIN_OPENING_CONTRACT_BALANCES_CURRENCY_BAK_20261019') IS NULL
    SELECT ID, CurrencyID INTO dbo.FIN_OPENING_CONTRACT_BALANCES_CURRENCY_BAK_20261019
    FROM dbo.FIN_OPENING_CONTRACT_BALANCES WHERE CurrencyID <> @uzs;
IF OBJECT_ID('dbo.FIN_BALANCE_SNAPSHOTS_CURRENCY_BAK_20261019') IS NULL
    SELECT ID, CurrencyID INTO dbo.FIN_BALANCE_SNAPSHOTS_CURRENCY_BAK_20261019
    FROM dbo.FIN_BALANCE_SNAPSHOTS WHERE CurrencyID <> @uzs AND (SourceType LIKE 'OpeningBalance%' OR SourceType IS NULL);

UPDATE dbo.FIN_OPENING_CONTRACT_BALANCES SET CurrencyID = @uzs WHERE CurrencyID <> @uzs;
PRINT CONCAT('FIN_OPENING_CONTRACT_BALANCES: ', @@ROWCOUNT, ' qator so''mga o''tkazildi');

UPDATE dbo.FIN_BALANCE_SNAPSHOTS SET CurrencyID = @uzs
WHERE CurrencyID <> @uzs AND (SourceType LIKE 'OpeningBalance%' OR SourceType IS NULL)
  -- Hisob kesimidagi opening (SourceID = hisob) hisob valyutasida qoladi
  AND NOT (SourceType = 'OpeningBalanceAccount' AND SourceID IS NOT NULL);
PRINT CONCAT('FIN_BALANCE_SNAPSHOTS: ', @@ROWCOUNT, ' qator so''mga o''tkazildi');

COMMIT TRAN;
