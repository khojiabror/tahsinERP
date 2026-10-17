/*
  Tannarx fakt xarajatlari — eski (SP + C# ikki engine) noto'g'ri ma'lumotni TOPUVCHI skript.
  FAQAT SELECT. Hech narsa o'zgartirmaydi.

  Tuzatishning to'g'ri yo'li: har oy uchun "Tannarx tahlili → Fakt xarajatlarni yangilash" ni qayta bosish
  (yangi C# engine barcha fakt ustunlarni qayta yozadi). Ishlab chiqarilmagan oylar uchun alohida
  Tannarx_2026_10_ActualCosts_Fix.sql bor.
*/
SET NOCOUNT ON;

/* 1) Fakt foyda reja foydadan farq qiladi (eski SP: foyda = QQS bilan narx − fakt xarajat). */
SELECT t.ID, t.ProductID, p.PNo, t.Year, t.Month, t.Profit, t.ActualProfit,
       t.ActualSalesPriceEXWWithoutVAT, t.ActualSalesPriceEXWWithVAT
FROM dbo.TANNARX t
LEFT JOIN dbo.PRODUCTS p ON p.ID = t.ProductID
WHERE t.IsDeleted = 0
  AND t.ActualProfit IS NOT NULL
  AND ABS(t.ActualProfit - t.Profit) > 0.01
ORDER BY t.Year, t.Month, p.PNo;

/* 2) Fakt EXW (QQSsiz) ≠ fakt xarajatlar yig'indisi + fakt foyda (ikki engine aralashgan qatorlar). */
SELECT t.ID, t.ProductID, t.Year, t.Month,
       t.ActualSalesPriceEXWWithoutVAT,
       ISNULL(t.ActualMaterialPrice,0) + ISNULL(t.ActualMaterialTransportCost,0) + ISNULL(t.ActualLaborCost,0)
       + ISNULL(t.ActualTaxAndBank,0) + ISNULL(t.ActualAdministrativeExpenses,0) + ISNULL(t.ActualOtherExpenses,0)
       + ISNULL(t.ActualProfit,0) AS ExpectedExwWithoutVat
FROM dbo.TANNARX t
WHERE t.IsDeleted = 0
  AND t.ActualSalesPriceEXWWithoutVAT IS NOT NULL
  AND ABS(t.ActualSalesPriceEXWWithoutVAT
          - (ISNULL(t.ActualMaterialPrice,0) + ISNULL(t.ActualMaterialTransportCost,0) + ISNULL(t.ActualLaborCost,0)
             + ISNULL(t.ActualTaxAndBank,0) + ISNULL(t.ActualAdministrativeExpenses,0) + ISNULL(t.ActualOtherExpenses,0)
             + ISNULL(t.ActualProfit,0))) > 0.01
ORDER BY t.Year, t.Month;

/* 3) Shu oyda ishlab chiqarilmagan, lekin fakt qiymatlari bor qatorlar (eski SP transport/EXW/foydani qoldirgan). */
SELECT t.ID, t.ProductID, t.Year, t.Month,
       t.ActualMaterialTransportCost, t.ActualFinishedProductTransportCost,
       t.ActualSalesPriceEXWWithVAT, t.ActualProfit
FROM dbo.TANNARX t
WHERE t.IsDeleted = 0
  AND NOT EXISTS (
        SELECT 1 FROM dbo.PRODUCT_PRODUCTION pp
        WHERE pp.ProductID = t.ProductID
          AND ISNULL(pp.IsDeleted, 0) = 0
          AND pp.[Date] >= DATEFROMPARTS(t.Year, t.Month, 1)
          AND pp.[Date] <  DATEADD(MONTH, 1, DATEFROMPARTS(t.Year, t.Month, 1))
          AND pp.Total > 0)
  AND (t.ActualMaterialPrice IS NOT NULL OR t.ActualMaterialTransportCost IS NOT NULL
       OR t.ActualFinishedProductTransportCost IS NOT NULL OR t.ActualSalesPriceEXWWithVAT IS NOT NULL
       OR t.ActualProfit IS NOT NULL)
ORDER BY t.Year, t.Month;

/* 4) Finance xarajat kategoriyalari ro'yxati — tannarx klassifikatori nom bo'yicha ishlaydi; nomlarni ko'zdan kechiring. */
SELECT c.ID, c.Name, c.Type, c.ParentID, c.IsActive,
       (SELECT COUNT(*) FROM dbo.FIN_TRANSACTIONS t WHERE t.CategoryID = c.ID AND ISNULL(t.IsDeleted,0) = 0) AS TxnCount
FROM dbo.FIN_CATEGORIES c
WHERE c.Type = 2 AND ISNULL(c.IsDeleted, 0) = 0
ORDER BY c.ParentID, c.SortOrder, c.Name;

/* 5) Ishlab chiqarish uchun ombordan chiqimda narxsiz (0) qatorlar — PATH0 material narxini pasaytiradi. */
SELECT e.ID AS ExpenseID, e.DocNo, e.IssueDateTime, ep.ProductID AS MaterialID, ep.Amount, ep.PiecePrice, ep.TotalPrice
FROM dbo.EXPENSES e
INNER JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ExpenseID = e.ID
WHERE e.IsDeleted = 0
  AND e.Description LIKE N'%ProductionID:%'
  AND ep.TotalPrice <= 0
ORDER BY e.IssueDateTime DESC;
