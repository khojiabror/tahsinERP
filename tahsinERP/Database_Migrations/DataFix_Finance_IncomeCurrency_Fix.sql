/*
 FIN-B01: invoysga bog'langan kirimning valyutasini invoys valyutasiga tenglaydi (summa o'zgarmaydi).
 Avval DataFix_Finance_IncomeCurrency_Check.sql natijasini ko'ring: faqat summasi aslida invoys
 valyutasida kiritilgan (valyuta xato belgilangan) qatorlar uchun ishlating.
 Zaxira: eski qiymatlar dbo.INCOMES_CURRENCY_BAK_20261019 jadvaliga saqlanadi. Qayta ishga tushirsa bo'ladi.
 Keyin DataFix_Finance_InvoiceMirrorAmounts_Fix.sql (mirror summasi kirimlar yig'indisidan olinadi).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID('dbo.INCOMES_CURRENCY_BAK_20261019') IS NULL
    SELECT inc.ID, inc.CurrencyID INTO dbo.INCOMES_CURRENCY_BAK_20261019
    FROM dbo.INCOMES inc
    JOIN dbo.P_INVOICES p ON p.ID = inc.InvoiceID
    WHERE ISNULL(inc.IsDeleted, 0) = 0 AND ISNULL(p.IsDeleted, 0) = 0
      AND p.CurrencyID IS NOT NULL AND inc.CurrencyID <> p.CurrencyID;

UPDATE inc SET inc.CurrencyID = p.CurrencyID
FROM dbo.INCOMES inc
JOIN dbo.P_INVOICES p ON p.ID = inc.InvoiceID
WHERE ISNULL(inc.IsDeleted, 0) = 0 AND ISNULL(p.IsDeleted, 0) = 0
  AND p.CurrencyID IS NOT NULL AND inc.CurrencyID <> p.CurrencyID;
PRINT CONCAT('INCOMES: ', @@ROWCOUNT, ' qator valyutasi invoys valyutasiga tenglandi');

COMMIT TRAN;
