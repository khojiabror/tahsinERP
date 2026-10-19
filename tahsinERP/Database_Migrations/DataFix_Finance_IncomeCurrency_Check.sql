-- =====================================================================
-- Finance (FIN-B01) — TEKSHIRUV (faqat SELECT).
-- Invoysga bog'langan kirim (INCOMES) valyutasi invoys valyutasidan farq qiladigan qatorlar.
-- B01'dan keyin hisobotlar summani hujjat valyutasidan so'mga o'giradi: so'mda kiritilgan, lekin USD deb
-- belgilangan kirim kurs bo'lmasa 0 (ogohlantirish bilan), kurs bo'lsa ~12 000 marta katta bo'lib chiqadi.
-- Lokal baza (2026-10-19): 1 qator — INCOMES 699 (78-faktura, 300-invoys), 19 550 000 "USD", invoys va
-- boshqa kirimlari UZS.
-- =====================================================================
SELECT inc.ID AS IncomeID, inc.DocNo, inc.IssueDateTime, inc.TotalPrice,
       ci.Shortname AS KirimValyutasi, p.ID AS InvoiceID, p.InvoiceNo, cp.Shortname AS InvoysValyutasi
FROM dbo.INCOMES inc
JOIN dbo.P_INVOICES p ON p.ID = inc.InvoiceID
LEFT JOIN dbo.CURRENCIES ci ON ci.ID = inc.CurrencyID
LEFT JOIN dbo.CURRENCIES cp ON cp.ID = p.CurrencyID
WHERE ISNULL(inc.IsDeleted, 0) = 0
  AND ISNULL(p.IsDeleted, 0) = 0
  AND p.CurrencyID IS NOT NULL
  AND inc.CurrencyID <> p.CurrencyID
ORDER BY inc.IssueDateTime;
