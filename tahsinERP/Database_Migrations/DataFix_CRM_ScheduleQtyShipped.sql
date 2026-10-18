/*
 SORDER_DELIVERY_SCHEDULE.QtyShipped — buyurtmaning faol invoyslari bo'yicha bir martalik to'ldirish.
 Ilovadagi SalesOrderService.RecalculateShipped bilan bir xil qoida: har (buyurtma, mahsulot) bo'yicha
 invoys qilingan miqdor reja qatorlariga DeliveryDate, ID tartibida (FIFO) taqsimlanadi.
 Holat: 0 -> PLANNED, qisman -> PARTIAL, to'liq -> SHIPPED. Qayta ishga tushirsa bo'ladi.
*/
SET NOCOUNT ON;

;WITH invoiced AS (
    SELECT si.OrderID, sil.ProductID, SUM(sil.Qty) AS Qty
    FROM dbo.SALES_INVOICE_LINES sil
    JOIN dbo.SALES_INVOICES si ON si.ID = sil.InvoiceID
    WHERE si.OrderID IS NOT NULL
      AND (si.StatusCode IS NULL OR si.StatusCode NOT IN ('CANCELED', 'CANCELLED'))
    GROUP BY si.OrderID, sil.ProductID
),
planned AS (
    SELECT s.ID, s.SOrderID, op.ProductID, s.QtyPlanned,
           SUM(s.QtyPlanned) OVER (PARTITION BY s.SOrderID, op.ProductID ORDER BY s.DeliveryDate, s.ID
                                   ROWS UNBOUNDED PRECEDING) - s.QtyPlanned AS PlannedBefore
    FROM dbo.SORDER_DELIVERY_SCHEDULE s
    JOIN dbo.S_ORDER_PRODUCTS op ON op.ID = s.SOrderProductID
),
calc AS (
    SELECT p.ID,
           CASE WHEN ISNULL(i.Qty, 0) - p.PlannedBefore <= 0 THEN 0
                WHEN ISNULL(i.Qty, 0) - p.PlannedBefore >= p.QtyPlanned THEN p.QtyPlanned
                ELSE ISNULL(i.Qty, 0) - p.PlannedBefore END AS Shipped,
           p.QtyPlanned
    FROM planned p
    LEFT JOIN invoiced i ON i.OrderID = p.SOrderID AND i.ProductID = p.ProductID
)
UPDATE s
SET QtyShipped = c.Shipped,
    StatusCode = CASE WHEN c.Shipped <= 0 THEN 'PLANNED' WHEN c.Shipped < c.QtyPlanned THEN 'PARTIAL' ELSE 'SHIPPED' END,
    UpdatedDT = SYSDATETIME()
FROM dbo.SORDER_DELIVERY_SCHEDULE s
JOIN calc c ON c.ID = s.ID
WHERE s.QtyShipped <> c.Shipped
   OR s.StatusCode <> CASE WHEN c.Shipped <= 0 THEN 'PLANNED' WHEN c.Shipped < c.QtyPlanned THEN 'PARTIAL' ELSE 'SHIPPED' END;

PRINT CONCAT('Yangilangan reja qatorlari: ', @@ROWCOUNT);

SELECT StatusCode, COUNT(*) AS Rows, SUM(QtyPlanned) AS Planned, SUM(QtyShipped) AS Shipped
FROM dbo.SORDER_DELIVERY_SCHEDULE GROUP BY StatusCode;
GO
