/*
    SRM-B01 — eski noto'g'ri invoys summalarini TUZATISH.

    !!! Avval DataFix_SRM_Procurement_01_InvoiceAmount_Find.sql natijasini ko'rib chiqing.
    !!! Zaxira nusxa oling. Skript standart holatda ROLLBACK qiladi — natijani tekshirib,
        oxiridagi ROLLBACK ni COMMIT ga almashtiring.

    Nima qiladi:
      1) Qatorlari bor, o'chirilmagan invoyslarda P_INVOICES.Amount = SUM(Price * Quantity).
      2) Shu invoyslarning faol FIN_TRANSACTIONS (SourceDocType='PurchaseInvoice') yozuvi
         summasini yangi Amount ga tenglaydi (FinancePostingService.PostPurchaseInvoice qoidasi).
    Qatorsiz invoyslarga tegmaydi (Find skriptining ikkinchi natijasi — qo'lda hal qilinadi).
    To'langan summalar/akt-sverka bu skriptda o'zgartirilmaydi.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @fixed TABLE (InvoiceID int PRIMARY KEY, OldAmount float NULL, NewAmount float NOT NULL);

;WITH lines AS (
    SELECT ip.InvoiceID, SUM(ip.Price * ip.Quantity) AS LinesTotal
    FROM dbo.P_INVOICE_PRODUCTS ip
    GROUP BY ip.InvoiceID
)
UPDATE i
   SET i.Amount = l.LinesTotal
OUTPUT inserted.ID, deleted.Amount, inserted.Amount INTO @fixed (InvoiceID, OldAmount, NewAmount)
FROM dbo.P_INVOICES i
JOIN lines l ON l.InvoiceID = i.ID
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND ABS(l.LinesTotal - ISNULL(i.Amount, 0)) > 0.005;

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
