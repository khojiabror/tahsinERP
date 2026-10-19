/* =====================================================================
   010 — P_INVOICES / P_ORDERS summasini qatorlardan qayta yozuvchi triggerlarni olib tashlash (SRM-B25).

   PInvoiceService / POrderService Amount'ni QQS BILAN yozadi (net + shartnoma QQS stavkasi) va hamma o'quvchilar
   (moliya, tannarx, SRM dashboard) shunga moslangan. Lekin Update_Invoice_Amount_After_Insert/Delete va
   Update_Order_Amount_After_Insert/Delete triggerlari qatorlar yozilishi bilan Amount'ni QQSsiz SUM(TotalPrice)
   bilan almashtirardi — natijada bazada doim netto qolardi.

   P_CONTRACT_PRODUCTS triggerlari QOLADI: ular SUM(TotalAmount) = narx x miqdor + QQS yozadi (servis bilan bir xil).

   Mavjud invoyslar: Amount = ROUND(SUM(Price * Quantity) * (1 + stavka / 100), 2), stavka — shartnoma (yoki
   buyurtma shartnomasi) qatorlaridan ROUND(SUM(VATAmount) / SUM(Amount) * 100, 2)
   (Database_Migrations/DataFix_SRM_Procurement_01_InvoiceAmount_Fix.sql qoidasi). Moliya ko'zgu yozuvi
   ('PurchaseInvoice') FinancePostingService qoidasida: invoysga kirimlar bo'lsa SUM(INCOMES.TotalPrice), aks holda Amount.
   Mavjud buyurtmalar o'zgarmaydi (to'g'ridan-to'g'ri buyurtmada QQS stavkasi saqlanmagan).
   Idempotent: qayta ishga tushirsa natija qatorlardan qayta hisoblanadi.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID('dbo.Update_Invoice_Amount_After_Insert', 'TR') IS NOT NULL DROP TRIGGER dbo.Update_Invoice_Amount_After_Insert;
IF OBJECT_ID('dbo.Update_Invoice_Amount_After_Delete', 'TR') IS NOT NULL DROP TRIGGER dbo.Update_Invoice_Amount_After_Delete;
IF OBJECT_ID('dbo.Update_Order_Amount_After_Insert', 'TR') IS NOT NULL DROP TRIGGER dbo.Update_Order_Amount_After_Insert;
IF OBJECT_ID('dbo.Update_Order_Amount_After_Delete', 'TR') IS NOT NULL DROP TRIGGER dbo.Update_Order_Amount_After_Delete;
GO

;WITH lines AS (
    SELECT InvoiceID, SUM(Price * Quantity) AS Net
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
    SELECT i.ID, ROUND(l.Net * (1 + ISNULL(cr.VatRate, 0) / 100.0), 2) AS ExpectedAmount
    FROM dbo.P_INVOICES i
    JOIN lines l ON l.InvoiceID = i.ID
    LEFT JOIN dbo.P_ORDERS o ON o.ID = i.OrderID
    LEFT JOIN contractRate cr ON cr.ContractID = CASE WHEN i.ContractID > 0 THEN i.ContractID ELSE o.ContractID END
    WHERE ISNULL(i.IsDeleted, 0) = 0
)
UPDATE i SET Amount = e.ExpectedAmount
FROM dbo.P_INVOICES i
JOIN expected e ON e.ID = i.ID
WHERE ABS(e.ExpectedAmount - ISNULL(i.Amount, 0)) > 0.005;
GO

UPDATE ft SET Amount = x.Amount
FROM dbo.FIN_TRANSACTIONS ft
JOIN (
    SELECT i.ID,
           CAST(ISNULL((SELECT SUM(inc.TotalPrice) FROM dbo.INCOMES inc
                        WHERE inc.InvoiceID = i.ID AND ISNULL(inc.IsDeleted, 0) = 0 AND inc.TotalPrice IS NOT NULL),
                       i.Amount) AS decimal(18, 2)) AS Amount
    FROM dbo.P_INVOICES i
    WHERE ISNULL(i.IsDeleted, 0) = 0 AND i.Amount IS NOT NULL
) x ON x.ID = ft.SourceDocID
WHERE ft.SourceDocType = 'PurchaseInvoice'
  AND ISNULL(ft.IsDeleted, 0) = 0
  AND ABS(ft.Amount - x.Amount) > 0.005;
GO
