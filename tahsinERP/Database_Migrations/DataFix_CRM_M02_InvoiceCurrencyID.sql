-- =====================================================================
-- CRM-M02 / B18: SALES_INVOICES.CurrencyID hech qachon to'ldirilmagan (faqat CurrencyCode matni).
-- Oqibat: FinancePostingService.PostSalesInvoice "CurrencyID ?? 1" bilan HAMMA invoysni 1-valyuta
-- debitorlik hisobiga (RECEIVABLES_1) va 1-valyutada yozgan — USD invoys so'm hisobida turibdi.
-- Kod endi CurrencyID'ni CurrencyCode bo'yicha yozadi (yangi invoyslar to'g'ri).
--
-- 1-QISM (SELECT) — xavfsiz.
-- 2-QISM — eski invoyslarda CurrencyID ni to'ldirish (ROLLBACK standart).
-- 3-QISM — noto'g'ri valyutada post qilingan FIN_TRANSACTIONS: faqat ro'yxat. Tuzatish (hisob va
--          valyutani almashtirish) buxgalteriya bilan kelishib, Finance sessiyasi/buxgalter tomonidan.
-- =====================================================================
SET NOCOUNT ON;

-- 1) CurrencyID bo'sh yoki kodga mos kelmaydigan invoyslar
SELECT si.ID, si.InvoiceNo, si.InvoiceDate, si.CurrencyCode, si.CurrencyID,
       c.ID AS ExpectedCurrencyID
FROM dbo.SALES_INVOICES si
LEFT JOIN dbo.CURRENCIES c ON c.Shortname = si.CurrencyCode AND (c.IsDeleted IS NULL OR c.IsDeleted = 0)
WHERE si.CurrencyID IS NULL OR si.CurrencyID <> c.ID
ORDER BY si.ID;

-- Kodi CURRENCIES'da topilmaydigan invoyslar (qo'lda ko'rib chiqing)
SELECT si.CurrencyCode, COUNT(*) AS Cnt
FROM dbo.SALES_INVOICES si
WHERE NOT EXISTS (SELECT 1 FROM dbo.CURRENCIES c WHERE c.Shortname = si.CurrencyCode)
GROUP BY si.CurrencyCode;
GO

-- 2) To'ldirish
/*
BEGIN TRAN;

UPDATE si
SET si.CurrencyID = c.ID
OUTPUT inserted.ID, deleted.CurrencyID AS OldCurrencyID, inserted.CurrencyID AS NewCurrencyID
FROM dbo.SALES_INVOICES si
JOIN dbo.CURRENCIES c ON c.Shortname = si.CurrencyCode AND (c.IsDeleted IS NULL OR c.IsDeleted = 0)
WHERE si.CurrencyID IS NULL OR si.CurrencyID <> c.ID;

ROLLBACK;   -- tekshirgandan keyin COMMIT
*/
GO

-- 3) Invoys valyutasidan farqli valyutada post qilingan finance yozuvlari (faqat ro'yxat)
SELECT t.ID AS FinTxnID, t.AccountID, t.CurrencyID AS PostedCurrencyID, t.Amount, t.TxnDate,
       si.ID AS InvoiceID, si.InvoiceNo, si.CurrencyCode, c.ID AS InvoiceCurrencyID
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.SALES_INVOICES si ON si.ID = t.SourceDocID
JOIN dbo.CURRENCIES c ON c.Shortname = si.CurrencyCode
WHERE t.SourceDocType = 'SalesInvoice'
  AND (t.IsDeleted IS NULL OR t.IsDeleted = 0)
  AND t.CurrencyID <> c.ID
ORDER BY t.TxnDate;
