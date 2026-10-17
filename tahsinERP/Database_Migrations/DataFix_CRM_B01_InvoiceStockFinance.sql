-- =====================================================================
-- CRM-B01: sotuv invoysi, ombor chiqimi va finance posting o'rtasidagi nomuvofiqliklar.
-- Sabab: invoys sarlavhasi va qatorlari alohida commit qilinardi, ombor chiqimi har qatorga alohida
-- tranzaksiyada, ombor va finance xatolari esa yutilardi. Natijada:
--   A) faol invoys, lekin ombor chiqimi (EXPENSES) yo'q yoki qisman;
--   B) faol invoys, lekin FIN_TRANSACTIONS da posting yo'q;
--   C) bekor qilingan invoys, lekin chiqim yoki posting qolib ketgan;
--   D) invoys qatori yo'q "yetim" sarlavhalar.
--   E) CRM-B03: tahrirda qatorlar almashib, chiqim eski miqdorda qolgan (A ichida ko'rinadi).
--
-- Bog'lanish matnli marker orqali: EXPENSES.Description / EXPENSE_PRODUCTS.Comment oxirida
-- "URL: /SalesInvoices/Details/<ID>". Quyida marker CHEGARASI bilan solishtiriladi
-- (LIKE '%/Details/12' — 120, 121 ... ga mos kelmaydi, chunki oxirida '%' yo'q).
--
-- 1-QISM (faqat SELECT) — xavfsiz.
-- 2-QISM (tuzatish) — faqat C) holati uchun avtomatik tuzatish beriladi; A), B), D) ni
--         qo'lda ko'rib chiqing (qoldiq yetmasligi, kurs, kontragent masalalari bor).
-- =====================================================================
SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#inv') IS NOT NULL DROP TABLE #inv;
SELECT si.ID, si.InvoiceNo, si.InvoiceDate, si.CustomerID, si.StatusCode, si.TotalAmount,
       '/SalesInvoices/Details/' + CAST(si.ID AS varchar(20)) AS Marker
INTO #inv
FROM dbo.SALES_INVOICES si;

-- Invoys qatorlari bo'yicha mahsulot miqdori
IF OBJECT_ID('tempdb..#lineQty') IS NOT NULL DROP TABLE #lineQty;
SELECT l.InvoiceID, l.ProductID, SUM(l.Qty) AS InvoiceQty
INTO #lineQty
FROM dbo.SALES_INVOICE_LINES l
GROUP BY l.InvoiceID, l.ProductID;

-- Avtomatik chiqimlar bo'yicha mahsulot miqdori (marker chegarasi bilan)
IF OBJECT_ID('tempdb..#expQty') IS NOT NULL DROP TABLE #expQty;
SELECT i.ID AS InvoiceID, ep.ProductID, SUM(ep.Amount) AS IssuedQty, COUNT(DISTINCT ep.ExpenseID) AS ExpenseCount
INTO #expQty
FROM #inv i
JOIN dbo.EXPENSE_PRODUCTS ep
  ON ep.Comment LIKE '%' + i.Marker
JOIN dbo.EXPENSES e ON e.ID = ep.ExpenseID
GROUP BY i.ID, ep.ProductID;

-- A) Faol invoys: qator miqdori != chiqim miqdori
SELECT 'A: ombor chiqimi mos emas' AS Issue, i.ID, i.InvoiceNo, i.InvoiceDate, i.StatusCode,
       COALESCE(lq.ProductID, eq.ProductID) AS ProductID,
       ISNULL(lq.InvoiceQty, 0) AS InvoiceQty,
       ISNULL(eq.IssuedQty, 0)  AS IssuedQty
FROM #inv i
JOIN (SELECT InvoiceID, ProductID FROM #lineQty UNION SELECT InvoiceID, ProductID FROM #expQty) k ON k.InvoiceID = i.ID
LEFT JOIN #lineQty lq ON lq.InvoiceID = i.ID AND lq.ProductID = k.ProductID
LEFT JOIN #expQty  eq ON eq.InvoiceID = i.ID AND eq.ProductID = k.ProductID
WHERE (i.StatusCode IS NULL OR i.StatusCode NOT IN ('CANCELED', 'CANCELLED'))
  AND ABS(ISNULL(lq.InvoiceQty, 0) - ISNULL(eq.IssuedQty, 0)) > 0.0001
ORDER BY i.ID, ProductID;

-- B) Faol invoys: finance posting yo'q yoki summa farq qiladi
SELECT 'B: finance posting yo''q/mos emas' AS Issue, i.ID, i.InvoiceNo, i.InvoiceDate, i.TotalAmount,
       t.ID AS FinTxnID, t.Amount AS PostedAmount
FROM #inv i
LEFT JOIN dbo.FIN_TRANSACTIONS t
  ON t.SourceDocType = 'SalesInvoice' AND t.SourceDocID = i.ID AND (t.IsDeleted IS NULL OR t.IsDeleted = 0)
WHERE (i.StatusCode IS NULL OR i.StatusCode NOT IN ('CANCELED', 'CANCELLED'))
  AND (t.ID IS NULL OR ABS(t.Amount - i.TotalAmount) > 0.01)
ORDER BY i.ID;

-- C) Bekor qilingan invoys: chiqim yoki posting qolgan
SELECT 'C: bekor qilingan, lekin chiqim qolgan' AS Issue, i.ID, i.InvoiceNo, eq.ProductID, eq.IssuedQty, eq.ExpenseCount
FROM #inv i
JOIN #expQty eq ON eq.InvoiceID = i.ID
WHERE i.StatusCode IN ('CANCELED', 'CANCELLED');

SELECT 'C: bekor qilingan, lekin posting faol' AS Issue, i.ID, i.InvoiceNo, t.ID AS FinTxnID, t.Amount
FROM #inv i
JOIN dbo.FIN_TRANSACTIONS t
  ON t.SourceDocType = 'SalesInvoice' AND t.SourceDocID = i.ID AND (t.IsDeleted IS NULL OR t.IsDeleted = 0)
WHERE i.StatusCode IN ('CANCELED', 'CANCELLED');

-- D) Qatorsiz invoyslar
SELECT 'D: qatorsiz invoys' AS Issue, i.ID, i.InvoiceNo, i.InvoiceDate, i.StatusCode, i.TotalAmount
FROM #inv i
WHERE NOT EXISTS (SELECT 1 FROM dbo.SALES_INVOICE_LINES l WHERE l.InvoiceID = i.ID);
GO

-- =====================================================================
-- 2-QISM: tuzatish — faqat C) bekor qilingan invoysning faol finance yozuvini o'chirilgan deb belgilash.
-- (Bekor qilingan invoysning qolib ketgan ombor chiqimini qaytarish qoldiqni o'zgartiradi —
--  uni ilovadagi "Bekor qilish" mantiqiga o'xshab qo'lda, ombor mas'uli bilan bajaring.)
-- Standart: ROLLBACK. Tekshirib, COMMIT ga almashtiring.
-- =====================================================================
/*
BEGIN TRAN;

UPDATE t
SET t.IsDeleted = 1
OUTPUT inserted.ID, inserted.SourceDocID, inserted.Amount
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.SALES_INVOICES si ON si.ID = t.SourceDocID
WHERE t.SourceDocType = 'SalesInvoice'
  AND (t.IsDeleted IS NULL OR t.IsDeleted = 0)
  AND si.StatusCode IN ('CANCELED', 'CANCELLED');

ROLLBACK;   -- tekshirgandan keyin COMMIT
*/
