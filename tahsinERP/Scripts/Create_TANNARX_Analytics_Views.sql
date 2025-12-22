-- =============================================
-- TANNARX Analytics Views and Stored Procedures
-- =============================================
-- This script creates views and stored procedures for calculating
-- actual costs from various modules for planned vs actual comparison
-- =============================================

-- =============================================
-- View: V_TANNARX_ACTUAL_MATERIAL_COSTS
-- =============================================
-- Calculates actual material costs from NORM_INPUT_LINES
-- This is the actual material price used in production for finished goods
-- Grouped by ProductID (finished good), Year, Month
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_TANNARX_ACTUAL_MATERIAL_COSTS')
    DROP VIEW V_TANNARX_ACTUAL_MATERIAL_COSTS
GO

CREATE VIEW V_TANNARX_ACTUAL_MATERIAL_COSTS
AS
SELECT 
    nil.ProductID,
    CASE 
        WHEN LEN(nil.Month) = 7 AND nil.Month LIKE '____-__' THEN CAST(LEFT(nil.Month, 4) AS INT)
        ELSE YEAR(nil.CreatedDT)
    END AS Year,
    CASE 
        WHEN LEN(nil.Month) = 7 AND nil.Month LIKE '____-__' THEN CAST(RIGHT(nil.Month, 2) AS INT)
        ELSE MONTH(nil.CreatedDT)
    END AS Month,
    SUM(ISNULL(nil.TotalAmount, nil.Price * ISNULL(nil.Consumption, 0))) AS ActualMaterialPrice
FROM NORM_INPUT_LINES nil
INNER JOIN PRODUCTS p ON nil.ProductID = p.ID
INNER JOIN PRODUCT_CATEGORIES pc ON p.CategoryID = pc.ID
WHERE p.IsDeleted = 0
    AND pc.IsFinishedGood = 1
    AND nil.Month IS NOT NULL
GROUP BY 
    nil.ProductID,
    CASE 
        WHEN LEN(nil.Month) = 7 AND nil.Month LIKE '____-__' THEN CAST(LEFT(nil.Month, 4) AS INT)
        ELSE YEAR(nil.CreatedDT)
    END,
    CASE 
        WHEN LEN(nil.Month) = 7 AND nil.Month LIKE '____-__' THEN CAST(RIGHT(nil.Month, 2) AS INT)
        ELSE MONTH(nil.CreatedDT)
    END
GO

-- =============================================
-- View: V_TANNARX_ACTUAL_MATERIAL_TRANSPORT
-- =============================================
-- Calculates actual material transport costs from F_WAYBILLS
-- For material transport (Direction = 0 or based on P_ORDERS)
-- PurchaseOrderID ga tegishli waybill summasini shu PurchaseOrderID dagi materiallarga teng taqsimlash
-- Cost per product = Total waybill cost / Number of distinct materials in PurchaseOrder
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_TANNARX_ACTUAL_MATERIAL_TRANSPORT')
    DROP VIEW V_TANNARX_ACTUAL_MATERIAL_TRANSPORT
GO

CREATE VIEW V_TANNARX_ACTUAL_MATERIAL_TRANSPORT
AS
WITH WaybillMaterialCounts AS (
    SELECT 
        wb.ID AS WaybillID,
        wb.PurchaseOrderID,
        YEAR(wb.IssueDate) AS Year,
        MONTH(wb.IssueDate) AS Month,
        wb.Amount AS TotalWaybillAmount,
        COUNT(DISTINCT pop.ProductID) AS MaterialCount
    FROM F_WAYBILLS wb
    INNER JOIN P_ORDERS po ON wb.PurchaseOrderID = po.ID
    INNER JOIN P_ORDER_PRODUCTS pop ON po.ID = pop.OrderID
    WHERE wb.IsDeleted = 0
        AND (wb.Direction = 0 OR wb.Direction IS NULL)  -- Material transport (incoming)
        AND wb.IssueDate IS NOT NULL
        AND wb.Amount IS NOT NULL
        AND wb.Amount > 0
        AND wb.PurchaseOrderID IS NOT NULL
    GROUP BY wb.ID, wb.PurchaseOrderID, YEAR(wb.IssueDate), MONTH(wb.IssueDate), wb.Amount
)
SELECT 
    pop.ProductID,
    wmc.Year,
    wmc.Month,
    SUM(wmc.TotalWaybillAmount / NULLIF(wmc.MaterialCount, 0)) AS ActualMaterialTransportCost
FROM WaybillMaterialCounts wmc
INNER JOIN P_ORDER_PRODUCTS pop ON wmc.PurchaseOrderID = pop.OrderID
GROUP BY pop.ProductID, wmc.Year, wmc.Month
GO

-- =============================================
-- View: V_TANNARX_ACTUAL_LABOR_COSTS
-- =============================================
-- Calculates actual labor costs from HR_PIECE_RATE_WORK_ENTRIES
-- Sum of (Quantity * RatePerUnit) for each product
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_TANNARX_ACTUAL_LABOR_COSTS')
    DROP VIEW V_TANNARX_ACTUAL_LABOR_COSTS
GO

CREATE VIEW V_TANNARX_ACTUAL_LABOR_COSTS
AS
SELECT 
    we.ProductID,
    YEAR(we.WorkDate) AS Year,
    MONTH(we.WorkDate) AS Month,
    SUM(we.Quantity * ISNULL(t.RatePerUnit, 0)) AS ActualLaborCost
FROM HR_PIECE_RATE_WORK_ENTRIES we
LEFT JOIN HR_PIECE_RATE_TARIFFS t ON we.ProductID = t.ProductID 
    AND we.OperationID = t.OperationID
    AND we.WorkDate >= t.EffectiveFrom
    AND (t.EffectiveTo IS NULL OR we.WorkDate <= t.EffectiveTo)
    AND t.IsActive = 1
WHERE we.ProductID IN (SELECT ID FROM PRODUCTS WHERE IsDeleted = 0)
GROUP BY we.ProductID, YEAR(we.WorkDate), MONTH(we.WorkDate)
GO

-- =============================================
-- View: V_TANNARX_ACTUAL_ADMIN_EXPENSES
-- =============================================
-- Calculates actual administrative expenses from HR_PAYROLL_LINES
-- For hourly employees (administrative staff) - soatbaya harajatlar
-- Cost per product = Total admin payroll / Total products produced in that month
-- Distributed equally among all products produced
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_TANNARX_ACTUAL_ADMIN_EXPENSES')
    DROP VIEW V_TANNARX_ACTUAL_ADMIN_EXPENSES
GO

CREATE VIEW V_TANNARX_ACTUAL_ADMIN_EXPENSES
AS
WITH AdminPayrollTotal AS (
    SELECT 
        ph.Year,
        ph.Month,
        SUM(pl.GrossSalary) AS TotalAdminPayroll
    FROM HR_PAYROLL_HEADERS ph
    INNER JOIN HR_PAYROLL_LINES pl ON ph.ID = pl.PayrollID
    INNER JOIN HR_EMPLOYEES e ON pl.EmployeeID = e.ID
    INNER JOIN HR_STAFFING_PLAN sp ON e.OrgUnitID = sp.OrgUnitID 
        AND e.PositionID = sp.PositionID
    WHERE ph.Status = 'Approved'
        AND sp.IsHourly = 1  -- Administrative (hourly) employees
    GROUP BY ph.Year, ph.Month
),
ProductsProduced AS (
    SELECT DISTINCT
        we.ProductID,
        YEAR(we.WorkDate) AS Year,
        MONTH(we.WorkDate) AS Month
    FROM HR_PIECE_RATE_WORK_ENTRIES we
    WHERE we.ProductID IN (
        SELECT ID FROM PRODUCTS p
        INNER JOIN PRODUCT_CATEGORIES pc ON p.CategoryID = pc.ID
        WHERE p.IsDeleted = 0 AND pc.IsFinishedGood = 1
    )
)
SELECT 
    pp.ProductID,
    pp.Year,
    pp.Month,
    apt.TotalAdminPayroll / NULLIF((
        SELECT COUNT(DISTINCT pp2.ProductID)
        FROM ProductsProduced pp2
        WHERE pp2.Year = pp.Year AND pp2.Month = pp.Month
    ), 0) AS ActualAdministrativeExpenses
FROM ProductsProduced pp
INNER JOIN AdminPayrollTotal apt ON pp.Year = apt.Year AND pp.Month = apt.Month
GO

-- =============================================
-- View: V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT
-- =============================================
-- Calculates actual finished product transport costs from F_WAYBILLS
-- For finished product transport (Direction = 1 or based on SalesInvoiceID)
-- SalesInvoiceID ga tegishli waybill summasini shu SalesInvoiceID ga tegishli productlar soniga teng taqsimlash
-- Cost per product = Total waybill cost / Number of distinct products in SalesInvoice
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT')
    DROP VIEW V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT
GO

CREATE VIEW V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT
AS
WITH WaybillProductCounts AS (
    SELECT 
        wb.ID AS WaybillID,
        wb.SalesInvoiceID,
        YEAR(wb.IssueDate) AS Year,
        MONTH(wb.IssueDate) AS Month,
        wb.Amount AS TotalWaybillAmount,
        COUNT(DISTINCT sil.ProductID) AS ProductCount
    FROM F_WAYBILLS wb
    INNER JOIN SALES_INVOICES si ON wb.SalesInvoiceID = si.ID
    INNER JOIN SALES_INVOICE_LINES sil ON si.ID = sil.InvoiceID
    WHERE wb.IsDeleted = 0
        AND wb.Direction = 1  -- Finished product transport (outgoing)
        AND wb.IssueDate IS NOT NULL
        AND wb.Amount IS NOT NULL
        AND wb.Amount > 0
        AND wb.SalesInvoiceID IS NOT NULL
    GROUP BY wb.ID, wb.SalesInvoiceID, YEAR(wb.IssueDate), MONTH(wb.IssueDate), wb.Amount
)
SELECT 
    sil.ProductID,
    wpc.Year,
    wpc.Month,
    SUM(wpc.TotalWaybillAmount / NULLIF(wpc.ProductCount, 0)) AS ActualFinishedProductTransportCost
FROM WaybillProductCounts wpc
INNER JOIN SALES_INVOICE_LINES sil ON wpc.SalesInvoiceID = sil.InvoiceID
GROUP BY sil.ProductID, wpc.Year, wpc.Month
GO

-- =============================================
-- Stored Procedure: SP_TANNARX_UPDATE_ACTUAL_COSTS
-- =============================================
-- Updates TANNARX table with actual costs from various views
-- This should be run periodically (e.g., daily or after data entry)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'SP_TANNARX_UPDATE_ACTUAL_COSTS')
    DROP PROCEDURE SP_TANNARX_UPDATE_ACTUAL_COSTS
GO

CREATE PROCEDURE SP_TANNARX_UPDATE_ACTUAL_COSTS
    @Year INT = NULL,
    @Month INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- If Year and Month are provided, update only those records
    -- Otherwise, update all records
    
    -- Calculate base cost for percentage calculations
    DECLARE @BaseCost DECIMAL(18, 4);
    
    UPDATE t
    SET 
        t.ActualMaterialPrice = ISNULL(amc.ActualMaterialPrice, 0),
        t.ActualMaterialTransportCost = ISNULL(amt.ActualMaterialTransportCost, 0),
        t.ActualLaborCost = ISNULL(alc.ActualLaborCost, 0),
        t.ActualAdministrativeExpenses = ISNULL(aae.ActualAdministrativeExpenses, 0),
        t.ActualFinishedProductTransportCost = ISNULL(afpt.ActualFinishedProductTransportCost, 0),
        -- Calculate actual Tax and Bank, Other Expenses, Profit based on actual base cost
        -- Base cost = Material + Material Transport + Labor + Admin
        t.ActualTaxAndBank = (ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.TaxAndBankPercent, 5) / 100.0,
        t.ActualOtherExpenses = (ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.OtherExpensesPercent, 5) / 100.0,
        t.ActualProfit = (ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.ProfitPercent, 10) / 100.0,
        -- Calculate actual EXW without VAT: SUM(a to e + material tashuvi narx + material narxi)
        t.ActualSalesPriceEXWWithoutVAT = ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.TaxAndBankPercent, 5) / 100.0
            + ISNULL(aae.ActualAdministrativeExpenses, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.OtherExpensesPercent, 5) / 100.0
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.ProfitPercent, 10) / 100.0,
        -- Calculate actual EXW with VAT: EXWWithoutVAT * 1.12
        t.ActualSalesPriceEXWWithVAT = (
            ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.TaxAndBankPercent, 5) / 100.0
            + ISNULL(aae.ActualAdministrativeExpenses, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.OtherExpensesPercent, 5) / 100.0
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.ProfitPercent, 10) / 100.0
        ) * 1.12,
        -- Calculate actual CIP without VAT: EXWWithoutVAT + FinishedProductTransportCost
        t.ActualSalesPriceCIPWithoutVAT = (
            ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.TaxAndBankPercent, 5) / 100.0
            + ISNULL(aae.ActualAdministrativeExpenses, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.OtherExpensesPercent, 5) / 100.0
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.ProfitPercent, 10) / 100.0
        ) + ISNULL(afpt.ActualFinishedProductTransportCost, 0),
        -- Calculate actual CIP with VAT: CIPWithoutVAT * 1.12
        t.ActualSalesPriceCIPWithVAT = ((
            ISNULL(amc.ActualMaterialPrice, 0) 
            + ISNULL(amt.ActualMaterialTransportCost, 0)
            + ISNULL(alc.ActualLaborCost, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.TaxAndBankPercent, 5) / 100.0
            + ISNULL(aae.ActualAdministrativeExpenses, 0)
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.OtherExpensesPercent, 5) / 100.0
            + (ISNULL(amc.ActualMaterialPrice, 0) 
                + ISNULL(amt.ActualMaterialTransportCost, 0)
                + ISNULL(alc.ActualLaborCost, 0)
                + ISNULL(aae.ActualAdministrativeExpenses, 0)) * ISNULL(t.ProfitPercent, 10) / 100.0
        ) + ISNULL(afpt.ActualFinishedProductTransportCost, 0)) * 1.12
    FROM TANNARX t
    LEFT JOIN V_TANNARX_ACTUAL_MATERIAL_COSTS amc 
        ON t.ProductID = amc.ProductID 
        AND t.Year = amc.Year 
        AND t.Month = amc.Month
    LEFT JOIN V_TANNARX_ACTUAL_MATERIAL_TRANSPORT amt 
        ON t.ProductID = amt.ProductID 
        AND t.Year = amt.Year 
        AND t.Month = amt.Month
    LEFT JOIN V_TANNARX_ACTUAL_LABOR_COSTS alc 
        ON t.ProductID = alc.ProductID 
        AND t.Year = alc.Year 
        AND t.Month = alc.Month
    LEFT JOIN V_TANNARX_ACTUAL_ADMIN_EXPENSES aae 
        ON t.ProductID = aae.ProductID 
        AND t.Year = aae.Year 
        AND t.Month = aae.Month
    LEFT JOIN V_TANNARX_ACTUAL_FINISHED_PRODUCT_TRANSPORT afpt 
        ON t.ProductID = afpt.ProductID 
        AND t.Year = afpt.Year 
        AND t.Month = afpt.Month
    WHERE t.IsDeleted = 0
        AND (@Year IS NULL OR t.Year = @Year)
        AND (@Month IS NULL OR t.Month = @Month);
    
    SELECT @@ROWCOUNT AS RecordsUpdated;
END
GO

PRINT 'TANNARX Analytics Views and Stored Procedures created successfully!'
GO
