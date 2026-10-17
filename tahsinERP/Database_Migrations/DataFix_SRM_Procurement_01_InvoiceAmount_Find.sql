/*
    SRM-B01 — eski noto'g'ri ma'lumotni TOPISH (faqat SELECT, hech narsani o'zgartirmaydi).

    Muammo: PInvoices/Edit qatorlarni almashtirganda P_INVOICES.Amount qayta hisoblanmas edi.
    Natijada sarlavha summasi qatorlar yig'indisidan farq qiladi va FinancePostingService
    shu eskirgan summani FIN_TRANSACTIONS (SourceDocType='PurchaseInvoice') ga yozgan.

    Qoida (Create/Edit kodi bilan bir xil): P_INVOICES.Amount = SUM(Price * Quantity), QQSsiz.
    Kod tuzatilgan (commit "SRM xarid: invoys/shartnoma/buyurtma bug'lari..."), bu skript
    faqat tuzatishdan OLDIN tahrirlangan invoyslarni topadi.

    Tuzatish: DataFix_SRM_Procurement_01_InvoiceAmount_Fix.sql (alohida, qo'lda ishga tushiriladi).
*/
SET NOCOUNT ON;

;WITH lines AS (
    SELECT ip.InvoiceID,
           COUNT(*)                              AS LineCount,
           SUM(ip.Price * ip.Quantity)           AS LinesTotal
    FROM dbo.P_INVOICE_PRODUCTS ip
    GROUP BY ip.InvoiceID
)
SELECT  i.ID                    AS InvoiceID,
        i.InvoiceNo,
        i.InvoiceDate,
        i.SupplierID,
        s.Name                  AS SupplierName,
        i.CurrencyID,
        i.Amount                AS HeaderAmount,
        ISNULL(l.LinesTotal, 0) AS LinesTotal,
        ISNULL(l.LinesTotal, 0) - ISNULL(i.Amount, 0) AS Difference,
        ISNULL(l.LineCount, 0)  AS LineCount,
        ft.ID                   AS FinTransactionID,
        ft.Amount               AS FinTransactionAmount
FROM dbo.P_INVOICES i
LEFT JOIN lines l              ON l.InvoiceID = i.ID
LEFT JOIN dbo.SUPPLIERS s      ON s.ID = i.SupplierID
LEFT JOIN dbo.FIN_TRANSACTIONS ft
       ON ft.SourceDocType = 'PurchaseInvoice'
      AND ft.SourceDocID = i.ID
      AND ISNULL(ft.IsDeleted, 0) = 0
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ABS(ISNULL(l.LinesTotal, 0) - ISNULL(i.Amount, 0)) > 0.005
ORDER BY i.InvoiceDate DESC, i.ID DESC;

-- Qatorsiz, lekin summasi bor invoyslar alohida ko'rib chiqilsin (qo'lda/eski import bo'lishi mumkin):
SELECT i.ID AS InvoiceID, i.InvoiceNo, i.InvoiceDate, i.Amount
FROM dbo.P_INVOICES i
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ISNULL(i.Amount, 0) <> 0
  AND NOT EXISTS (SELECT 1 FROM dbo.P_INVOICE_PRODUCTS ip WHERE ip.InvoiceID = i.ID);
