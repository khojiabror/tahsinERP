/*
    SRM-B01 + SRM-B25 — P_INVOICES.Amount noto'g'ri bo'lgan invoyslarni TOPISH (faqat SELECT).

    Yangi qoida (biznes qarori 2026-10-17, SRM-B25): P_INVOICES.Amount QQS BILAN saqlanadi:
        Amount = ROUND(SUM(Price * Quantity) * (1 + Stavka / 100), 2)
        Stavka = invoysning shartnomasi (P_INVOICES.ContractID, bo'lmasa P_ORDERS.ContractID)
                 qatorlari bo'yicha SUM(VATAmount) / SUM(Amount) * 100 (2 xona); shartnoma yo'q — 0.
    Avval Amount QQSsiz saqlanardi va Edit'da umuman qayta hisoblanmasdi (SRM-B01).

    Bu skript yangi koddan oldingi barcha invoyslarni (deyarli hammasi QQSsiz) ko'rsatadi.
    Tuzatish: DataFix_SRM_Procurement_01_InvoiceAmount_Fix.sql — YANGI KOD BILAN BIR VAQTDA
    ishga tushirilishi kerak (aks holda eski invoyslar ro'yxatda QQSsiz ko'rinadi).
*/
SET NOCOUNT ON;

;WITH lines AS (
    SELECT InvoiceID, COUNT(*) AS LineCount, SUM(Price * Quantity) AS Net
    FROM dbo.P_INVOICE_PRODUCTS
    GROUP BY InvoiceID
),
contractRate AS (
    SELECT ContractID,
           CASE WHEN SUM(Amount) > 0 AND SUM(ISNULL(VATAmount, 0)) > 0
                THEN ROUND(SUM(ISNULL(VATAmount, 0)) / SUM(Amount) * 100.0, 2)
                ELSE 0 END AS VatRate
    FROM dbo.P_CONTRACT_PRODUCTS
    GROUP BY ContractID
),
expected AS (
    SELECT i.ID,
           l.Net,
           l.LineCount,
           ISNULL(cr.VatRate, 0) AS VatRate,
           ROUND(l.Net * (1 + ISNULL(cr.VatRate, 0) / 100.0), 2) AS ExpectedAmount
    FROM dbo.P_INVOICES i
    JOIN lines l ON l.InvoiceID = i.ID
    LEFT JOIN dbo.P_ORDERS o ON o.ID = i.OrderID
    LEFT JOIN contractRate cr ON cr.ContractID = CASE WHEN i.ContractID > 0 THEN i.ContractID ELSE o.ContractID END
)
SELECT  i.ID              AS InvoiceID,
        i.InvoiceNo,
        i.InvoiceDate,
        s.Name            AS SupplierName,
        i.CurrencyID,
        i.Amount          AS CurrentAmount,
        e.Net,
        e.VatRate,
        e.ExpectedAmount,
        e.ExpectedAmount - ISNULL(i.Amount, 0) AS Difference,
        ft.ID             AS FinTransactionID,
        ft.Amount         AS FinTransactionAmount
FROM dbo.P_INVOICES i
JOIN expected e              ON e.ID = i.ID
LEFT JOIN dbo.SUPPLIERS s    ON s.ID = i.SupplierID
LEFT JOIN dbo.FIN_TRANSACTIONS ft
       ON ft.SourceDocType = 'PurchaseInvoice'
      AND ft.SourceDocID = i.ID
      AND ISNULL(ft.IsDeleted, 0) = 0
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ABS(e.ExpectedAmount - ISNULL(i.Amount, 0)) > 0.005
ORDER BY i.InvoiceDate DESC, i.ID DESC;

-- Qatorsiz, lekin summasi bor invoyslar (qo'lda yoki eski import) — qo'lda ko'rib chiqing:
SELECT i.ID AS InvoiceID, i.InvoiceNo, i.InvoiceDate, i.Amount
FROM dbo.P_INVOICES i
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ISNULL(i.Amount, 0) <> 0
  AND NOT EXISTS (SELECT 1 FROM dbo.P_INVOICE_PRODUCTS ip WHERE ip.InvoiceID = i.ID);
