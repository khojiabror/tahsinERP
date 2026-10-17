-- =====================================================================
-- CRM-B06 / CRM-B10: buyurtma yetkazib berish rejasi (SORDER_DELIVERY_SCHEDULE) buzilishlari.
-- Sabab:
--   B10 — buyurtma tahririda S_ORDER_PRODUCTS hammasi o'chirilib qayta qo'shilardi, ID'lar o'zgarardi;
--         SORDER_DELIVERY_SCHEDULE.SOrderProductID da FK yo'q, shuning uchun reja "yetim" qolardi.
--   B06 — rejani saqlash hamma qatorlarni o'chirib QtyShipped = 0 bilan qayta yozardi: QUALITY_INSPECTION
--         dagi DeliveryScheduleID yo'q qatorga ishora qilib qolardi, yetkazilgan miqdor yo'qolardi
--         (yo'qolgan QtyShipped ni bazadan tiklab bo'lmaydi — faqat sifat nazorati/chiqim hujjatlaridan qo'lda).
--
-- 1-QISM (SELECT) — xavfsiz.
-- 2-QISM (tuzatish) — yetim reja qatorini shu buyurtmadagi SHU MAHSULOTning yagona qatoriga qayta bog'laydi.
--         Mahsulot buyurtmada bir necha qatorda bo'lsa — avtomatik bog'lanmaydi (qo'lda).
--         Standart: ROLLBACK.
-- =====================================================================
SET NOCOUNT ON;

-- A) Yetim reja qatorlari (SOrderProductID mavjud emas yoki boshqa buyurtmaga tegishli)
SELECT 'A: yetim reja qatori' AS Issue, s.ID, s.SOrderID, s.SOrderProductID, s.ProductID,
       s.DeliveryDate, s.QtyPlanned, s.QtyShipped,
       (SELECT COUNT(*) FROM dbo.S_ORDER_PRODUCTS op
         WHERE op.OrderID = s.SOrderID AND op.ProductID = s.ProductID) AS CandidateLines
FROM dbo.SORDER_DELIVERY_SCHEDULE s
LEFT JOIN dbo.S_ORDER_PRODUCTS op ON op.ID = s.SOrderProductID AND op.OrderID = s.SOrderID
WHERE op.ID IS NULL
ORDER BY s.SOrderID, s.DeliveryDate;

-- B) Sifat nazorati yo'q reja qatoriga ishora qiladi
SELECT 'B: QI yo''q rejaga ishora qiladi' AS Issue, q.ID AS QualityInspectionID, q.DeliveryScheduleID
FROM dbo.QUALITY_INSPECTION q
WHERE q.DeliveryScheduleID IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.SORDER_DELIVERY_SCHEDULE s WHERE s.ID = q.DeliveryScheduleID);

-- C) Reja miqdori buyurtma miqdoridan oshgan qatorlar
SELECT 'C: reja > buyurtma' AS Issue, op.OrderID, op.ID AS SOrderProductID, op.ProductID, op.Amount AS OrderQty,
       SUM(s.QtyPlanned) AS PlannedQty
FROM dbo.S_ORDER_PRODUCTS op
JOIN dbo.SORDER_DELIVERY_SCHEDULE s ON s.SOrderProductID = op.ID AND s.SOrderID = op.OrderID
GROUP BY op.OrderID, op.ID, op.ProductID, op.Amount
HAVING SUM(s.QtyPlanned) > op.Amount + 0.0001;
GO

-- 2-QISM: yetim reja qatorlarini qayta bog'lash (ko'rib chiqib ishga tushiring)
/*
BEGIN TRAN;

;WITH cand AS (
    SELECT s.ID AS ScheduleID, MIN(op.ID) AS NewLineID, COUNT(*) AS Cnt
    FROM dbo.SORDER_DELIVERY_SCHEDULE s
    JOIN dbo.S_ORDER_PRODUCTS op ON op.OrderID = s.SOrderID AND op.ProductID = s.ProductID
    WHERE NOT EXISTS (SELECT 1 FROM dbo.S_ORDER_PRODUCTS x WHERE x.ID = s.SOrderProductID AND x.OrderID = s.SOrderID)
    GROUP BY s.ID
)
UPDATE s
SET s.SOrderProductID = c.NewLineID,
    s.UpdatedDT = GETDATE()
OUTPUT inserted.ID, deleted.SOrderProductID AS OldLineID, inserted.SOrderProductID AS NewLineID
FROM dbo.SORDER_DELIVERY_SCHEDULE s
JOIN cand c ON c.ScheduleID = s.ID
WHERE c.Cnt = 1;

ROLLBACK;   -- tekshirgandan keyin COMMIT
*/
