-- =====================================================================
-- Finance (FIN-B08) — TUZATISH. Avval ..._Check.sql natijasini ko'rib chiqing.
-- Mirror yozuvlari summasini yagona qoidaga keltiradi (faqat Amount o'zgaradi).
-- =====================================================================
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE t
SET t.Amount = CAST(COALESCE((SELECT SUM(inc.TotalPrice) FROM dbo.INCOMES inc
                              WHERE inc.InvoiceID = p.ID AND ISNULL(inc.IsDeleted, 0) = 0 AND inc.TotalPrice IS NOT NULL),
                             p.Amount, 0) AS DECIMAL(19, 2))
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.P_INVOICES p ON p.ID = t.SourceDocID
WHERE t.SourceDocType = 'PurchaseInvoice' AND ISNULL(t.IsDeleted, 0) = 0;
PRINT CONCAT(@@ROWCOUNT, ' ta xarid mirror yozuvi qayta hisoblandi');

UPDATE t
SET t.Amount = ISNULL(s.TotalWithVAT, s.TotalAmount)
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.SALES_INVOICES s ON s.ID = t.SourceDocID
WHERE t.SourceDocType = 'SalesInvoice' AND ISNULL(t.IsDeleted, 0) = 0
  AND t.Amount <> ISNULL(s.TotalWithVAT, s.TotalAmount);
PRINT CONCAT(@@ROWCOUNT, ' ta sotuv mirror yozuvi yangilandi');

COMMIT;
