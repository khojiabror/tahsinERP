/*
  2025 yildagi barcha yo'l varaqalari uchun qoldiq summada to'lov yozuvi (F_WAYBILL_PAYMENTS).
  UI da "To'langan" ko'rinishi uchun (logistika hisobi bilan bir xil hisoblash).

  DIQQAT: Avval BEGIN TRANSACTION; qiling, tekshirib ko'ring, keyin COMMIT.
  ForwarderID bo'lmasa shartnoma yoki birinchi forwarder olinadi.
*/

SET NOCOUNT ON;

;WITH DirectPaid AS (
    SELECT p.WaybillID,
           SUM(CAST(p.Payment_Amount AS float)) AS Amt
    FROM dbo.F_WAYBILL_PAYMENTS p
    WHERE p.IsDeleted = 0
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
          WHERE s.PayerType = 99
            AND s.PayerID = p.ID
      )
    GROUP BY p.WaybillID
),
SplitPaid AS (
    SELECT s.WaybillID,
           SUM(CAST(s.Amount AS float)) AS Amt
    FROM dbo.F_WAYBILL_PAYMENT_SPLITS s
    INNER JOIN dbo.F_WAYBILL_PAYMENTS p ON p.ID = s.PayerID AND s.PayerType = 99
    WHERE p.IsDeleted = 0
    GROUP BY s.WaybillID
),
TotPaid AS (
    SELECT u.WaybillID, SUM(u.Amt) AS TotalPaid
    FROM (
        SELECT WaybillID, Amt FROM DirectPaid
        UNION ALL
        SELECT WaybillID, Amt FROM SplitPaid
    ) u
    GROUP BY u.WaybillID
),
DefaultFwd AS (
    SELECT TOP 1 ID AS ForwarderID FROM dbo.FORWARDERS ORDER BY ID
)
INSERT INTO dbo.F_WAYBILL_PAYMENTS (ForwarderID, WaybillID, Payment_Amount, IsDeleted, Comment, CurrencyID, PaymentDate)
SELECT
    COALESCE(wb.ForwarderID, fc.ForwarderID, df.ForwarderID) AS ForwarderID,
    wb.ID,
    CAST(wb.Amount - ISNULL(tp.TotalPaid, 0) AS float) AS Payment_Amount,
    0,
    N'2025 avto to''lov (SQL)',
    wb.CurrencyID,
    COALESCE(CAST(wb.IssueDate AS date), CAST(GETDATE() AS date))
FROM dbo.F_WAYBILLS wb
LEFT JOIN dbo.F_CONTRACTS fc ON fc.ID = wb.ContractID
CROSS JOIN DefaultFwd df
LEFT JOIN TotPaid tp ON tp.WaybillID = wb.ID
WHERE YEAR(wb.IssueDate) = 2025
  AND ISNULL(wb.IsDeleted, 0) = 0
  AND wb.Amount IS NOT NULL
  AND wb.Amount > 0
  AND (wb.Amount - ISNULL(tp.TotalPaid, 0)) > 0.01
  AND COALESCE(wb.ForwarderID, fc.ForwarderID, df.ForwarderID) IS NOT NULL;

SELECT @@ROWCOUNT AS PaymentsInserted;
