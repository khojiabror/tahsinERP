-- =====================================================================
-- Finance (FIN-P11) — TEKSHIRUV (faqat SELECT, hech narsa o'zgarmaydi).
--
-- Eski kod har so'rovda quyidagini bajarardi:
--   UPDATE FIN_OPENING_CONTRACT_BALANCES SET TotalAmount = Amount, PaidAmount = 0
--   WHERE TotalAmount = 0 AND PaidAmount = 0
-- (TotalAmount/PaidAmount ustunlari qo'shilishidan oldingi qatorlar uchun).
-- Kod endi buni qilmaydi. Quyidagi so'rov hali to'ldirilmagan qatorlarni ko'rsatadi.
-- Natija bo'sh bo'lsa — DataFix_..._Fix.sql kerak emas.
-- =====================================================================
SELECT ID, SnapshotDate, BalanceType, CounterpartyID, ContractNo, TotalAmount, PaidAmount, Amount
FROM dbo.FIN_OPENING_CONTRACT_BALANCES
WHERE TotalAmount = 0 AND PaidAmount = 0 AND Amount <> 0
ORDER BY SnapshotDate, BalanceType, CounterpartyID;
