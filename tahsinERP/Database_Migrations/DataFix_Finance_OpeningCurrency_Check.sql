/*
 FIN-B01 tayyorgarligi: opening qatorlari va snapshot'lari valyutasi.
 Opening sahifasi "standart valyuta = ID 5" deb yozilgan edi; ba'zi bazalarda ID 5 = UZS, boshqalarida
 USD bo'lishi mumkin. Lokal bazada (2026-10-19) 112 ta FIN_OPENING_CONTRACT_BALANCES va 6 ta
 OpeningBalanceDetailed snapshot USD (ID 2) deb belgilangan, lekin summalar so'mda (akt-sverki va AutoFill
 so'mdagi invoyslardan aynan shu summalarni beradi). Konvertatsiya yoqilganda ular ~12 700 marta oshib ketadi.
 Faqat SELECT.
*/
DECLARE @uzs INT = (SELECT TOP 1 ID FROM dbo.CURRENCIES WHERE Shortname IN ('UZS','SUM','SO''M') ORDER BY ID);
SELECT @uzs AS UzsCurrencyID;

SELECT 'FIN_OPENING_CONTRACT_BALANCES' AS Tbl, o.SnapshotDate, o.BalanceType, c.Shortname, COUNT(*) AS Rows_, SUM(o.Amount) AS Amount
FROM dbo.FIN_OPENING_CONTRACT_BALANCES o JOIN dbo.CURRENCIES c ON c.ID = o.CurrencyID
WHERE o.CurrencyID <> @uzs
GROUP BY o.SnapshotDate, o.BalanceType, c.Shortname
ORDER BY o.SnapshotDate, o.BalanceType;

SELECT 'FIN_BALANCE_SNAPSHOTS' AS Tbl, s.SnapshotDate, s.SourceType, c.Shortname, COUNT(*) AS Rows_, SUM(s.Amount) AS Amount
FROM dbo.FIN_BALANCE_SNAPSHOTS s JOIN dbo.CURRENCIES c ON c.ID = s.CurrencyID
WHERE s.CurrencyID <> @uzs AND (s.SourceType LIKE 'OpeningBalance%' OR s.SourceType IS NULL)
GROUP BY s.SnapshotDate, s.SourceType, c.Shortname
ORDER BY s.SnapshotDate;

-- Shu kontragentning hujjatlari qaysi valyutada (opening shu valyutada bo'lishi kerak)
SELECT 'P_INVOICES' AS Doc, ISNULL(c.Shortname, 'NULL') AS Currency, COUNT(*) AS Rows_ FROM dbo.P_INVOICES i LEFT JOIN dbo.CURRENCIES c ON c.ID = i.CurrencyID WHERE ISNULL(i.IsDeleted,0)=0 GROUP BY c.Shortname
UNION ALL
SELECT 'SALES_INVOICES', ISNULL(c.Shortname, 'NULL'), COUNT(*) FROM dbo.SALES_INVOICES i LEFT JOIN dbo.CURRENCIES c ON c.ID = i.CurrencyID GROUP BY c.Shortname;
