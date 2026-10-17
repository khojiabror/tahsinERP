-- =====================================================================
-- CRM-B02: takroriy sotuv invoysi raqamlari.
-- Sabab: GetNextInvoiceNo oxirgi BEKOR QILINMAGAN invoys raqamiga +1 qilardi va qulf yo'q edi —
-- bekor qilingan invoys raqami qayta berilardi, parallel saqlashda ikki invoys bir raqam olardi.
--
-- 1-QISM (faqat SELECT) — xavfsiz, istalgan vaqtda ishga tushiring.
-- 2-QISM (tuzatish) — SIZ ko'rib chiqib ishga tushirasiz. Standart holatda ROLLBACK qiladi:
--         natijani tekshirib, oxiridagi ROLLBACK ni COMMIT ga almashtiring.
-- Tuzatish qoidasi: har bir takroriy guruhda eng birinchi (eng kichik ID) invoys raqamni saqlaydi,
-- qolganlariga "-D<ID>" qo'shimchasi qo'shiladi (masalan INV_0042 -> INV_0042-D1057).
-- Rasmiy hujjatlarda (SF/TTNK, chop etilgan invoys) ishlatilgan raqam bo'lsa — avval buxgalteriya
-- bilan kelishing.
-- =====================================================================
SET NOCOUNT ON;

-- 1-QISM: topish
SELECT si.InvoiceNo, si.ID, si.InvoiceDate, si.CustomerID, si.StatusCode, si.TotalAmount,
       ROW_NUMBER() OVER (PARTITION BY si.InvoiceNo ORDER BY si.ID) AS RN,
       (SELECT COUNT(*) FROM dbo.SALES_SF_HEADERS sf WHERE sf.InvoiceID = si.ID)        AS SFCount,
       (SELECT COUNT(*) FROM dbo.SALES_TTNK_INVOICES ti WHERE ti.InvoiceID = si.ID)     AS TTNKCount
FROM dbo.SALES_INVOICES si
WHERE si.InvoiceNo IN (
    SELECT InvoiceNo FROM dbo.SALES_INVOICES
    WHERE InvoiceNo IS NOT NULL
    GROUP BY InvoiceNo HAVING COUNT(*) > 1)
ORDER BY si.InvoiceNo, si.ID;
GO

-- 2-QISM: tuzatish (ko'rib chiqib ishga tushiring)
/*
BEGIN TRAN;

;WITH d AS (
    SELECT ID, InvoiceNo,
           ROW_NUMBER() OVER (PARTITION BY InvoiceNo ORDER BY ID) AS RN
    FROM dbo.SALES_INVOICES
    WHERE InvoiceNo IS NOT NULL
)
UPDATE si
SET si.InvoiceNo = LEFT(si.InvoiceNo + '-D' + CAST(si.ID AS varchar(20)), 50),
    si.UpdatedDT = GETDATE()
OUTPUT deleted.ID, deleted.InvoiceNo AS OldNo, inserted.InvoiceNo AS NewNo
FROM dbo.SALES_INVOICES si
JOIN d ON d.ID = si.ID
WHERE d.RN > 1;

-- Finance yozuvidagi maqsad matnini ham yangilash (Purpose = 'Sotuv invoysi #<raqam>')
UPDATE t
SET t.Purpose = N'Sotuv invoysi #' + si.InvoiceNo
FROM dbo.FIN_TRANSACTIONS t
JOIN dbo.SALES_INVOICES si ON si.ID = t.SourceDocID
WHERE t.SourceDocType = 'SalesInvoice' AND si.InvoiceNo LIKE '%-D%' AND t.Purpose <> N'Sotuv invoysi #' + si.InvoiceNo;

ROLLBACK;   -- tekshirgandan keyin COMMIT ga almashtiring
*/
