/*
    SRM-B01 + SRM-B25 — P_INVOICES.Amount ni QQS BILAN qayta hisoblash (TUZATISH).

    !!! Avval DataFix_SRM_Procurement_01_InvoiceAmount_Find.sql natijasini ko'rib chiqing.
    !!! Zaxira nusxa oling. Yangi kod (Amount QQS bilan) deploy qilinganda bir vaqtda ishga tushiring.
    !!! Skript standart holatda ROLLBACK qiladi — natijani tekshirib, oxirida COMMIT ga almashtiring.
    !!! Finance va Tannarx modullari Amount'ni QQSsiz deb o'qiydigan joylari bilan moslashtirilgan
        bo'lishi kerak (OpeningBalanceController QQS qo'shmasligi kerak) — aks holda QQS ikki marta qo'shiladi.

    Qoida (PInvoicesController bilan bir xil):
        Amount = ROUND(SUM(Price * Quantity) * (1 + Stavka / 100), 2)
        Stavka = shartnoma (ContractID yoki buyurtma orqali) qatorlari: ROUND(SUM(VATAmount)/SUM(Amount)*100, 2)
    Faqat qatorlari bor, o'chirilmagan invoyslar. Qayta ishga tushirish xavfsiz (natija qatorlardan hisoblanadi).
    Faol FIN_TRANSACTIONS (SourceDocType='PurchaseInvoice') summasi ham yangi Amount ga tenglanadi.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @fixed TABLE (InvoiceID int PRIMARY KEY, OldAmount float NULL, NewAmount float NOT NULL);

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
UPDATE i
   SET i.Amount = e.ExpectedAmount
OUTPUT inserted.ID, deleted.Amount, inserted.Amount INTO @fixed (InvoiceID, OldAmount, NewAmount)
FROM dbo.P_INVOICES i
JOIN expected e ON e.ID = i.ID
WHERE ABS(e.ExpectedAmount - ISNULL(i.Amount, 0)) > 0.005;

UPDATE ft
   SET ft.Amount = f.NewAmount  -- ustun turiga avtomatik o'giriladi (kod: (decimal)invoice.Amount)
FROM dbo.FIN_TRANSACTIONS ft
JOIN @fixed f ON f.InvoiceID = ft.SourceDocID
WHERE ft.SourceDocType = 'PurchaseInvoice'
  AND ISNULL(ft.IsDeleted, 0) = 0;

SELECT f.InvoiceID, f.OldAmount, f.NewAmount, f.NewAmount - ISNULL(f.OldAmount, 0) AS Difference
FROM @fixed f
ORDER BY f.InvoiceID;

-- Tekshirib bo'lgach: COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
