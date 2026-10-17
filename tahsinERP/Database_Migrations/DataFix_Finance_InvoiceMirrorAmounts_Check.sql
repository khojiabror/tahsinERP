-- =====================================================================
-- Finance (FIN-B08) — TEKSHIRUV (faqat SELECT).
-- FIN "mirror" yozuvlari (SourceDocType = PurchaseInvoice / SalesInvoice) endi
-- kontragent qarzi bilan bir xil qoida bo'yicha yoziladi:
--   xarid  = invoysga bog'langan kirimlar SUM(INCOMES.TotalPrice), bo'lmasa P_INVOICES.Amount;
--   sotuv  = TotalWithVAT, bo'lmasa TotalAmount.
-- Eski yozuvlar eski qoida bilan (QQSsiz / kirimsiz) qolgan. Ular faqat
-- kreditor/debitor HISOB qoldig'iga (LiabilitiesDashboard jadvali) ta'sir qiladi;
-- dashboard/balans AR/AP hujjatlardan hisoblanadi.
-- Lokal baza (2026-10-17): xarid 39 qator (1 542,1 mln -> 1 705,0 mln), sotuv 0 qator.
-- =====================================================================
SELECT 'PurchaseInvoice' AS Tur, t.ID AS FinTxnID, p.ID AS InvoiceID, p.InvoiceNo, t.Amount AS Hozirgi,
       CAST(COALESCE((SELECT SUM(inc.TotalPrice) FROM dbo.INCOMES inc
                      WHERE inc.InvoiceID = p.ID AND ISNULL(inc.IsDeleted, 0) = 0 AND inc.TotalPrice IS NOT NULL),
                     p.Amount, 0) AS DECIMAL(19, 2)) AS Kerakli
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.P_INVOICES p ON p.ID = t.SourceDocID
WHERE t.SourceDocType = 'PurchaseInvoice' AND ISNULL(t.IsDeleted, 0) = 0
  AND t.Amount <> CAST(COALESCE((SELECT SUM(inc.TotalPrice) FROM dbo.INCOMES inc
                                 WHERE inc.InvoiceID = p.ID AND ISNULL(inc.IsDeleted, 0) = 0 AND inc.TotalPrice IS NOT NULL),
                                p.Amount, 0) AS DECIMAL(19, 2))
UNION ALL
SELECT 'SalesInvoice', t.ID, CAST(s.ID AS INT), s.InvoiceNo, t.Amount, ISNULL(s.TotalWithVAT, s.TotalAmount)
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.SALES_INVOICES s ON s.ID = t.SourceDocID
WHERE t.SourceDocType = 'SalesInvoice' AND ISNULL(t.IsDeleted, 0) = 0
  AND t.Amount <> ISNULL(s.TotalWithVAT, s.TotalAmount)
ORDER BY 1, 2;
