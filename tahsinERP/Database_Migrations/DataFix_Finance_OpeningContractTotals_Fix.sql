-- =====================================================================
-- Finance (FIN-P11) — TUZATISH. Avval ..._Check.sql natijasini ko'rib chiqing.
-- TotalAmount/PaidAmount bo'sh (0/0) qatorlarda TotalAmount = Amount qilinadi
-- (eski kodning har so'rovdagi UPDATE'i bilan aynan bir xil, faqat bir marta).
-- =====================================================================
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE dbo.FIN_OPENING_CONTRACT_BALANCES
SET TotalAmount = Amount, PaidAmount = 0
WHERE TotalAmount = 0 AND PaidAmount = 0 AND Amount <> 0;

PRINT CONCAT(@@ROWCOUNT, ' ta qator yangilandi');

COMMIT;
