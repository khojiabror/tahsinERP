/* =====================================================================
   023 — Distributsiya: rahbar paneli va savdo hisobotlari (01_PHASE1_MVP.md, S5).

   Yangi jadval yo'q — hisobotlar mavjud hujjatlardan (D_SHIPMENTS/D_RETURNS qatorlari, CUSTOMER_LEDGER,
   D_PAYMENTS, CASH_HANDOVERS, VISITS, PRODUCT_STOCKS + kirim qatlamlari) o'qiydi. Bu skript:
     - davr bo'yicha hisobot so'rovlari uchun indekslar (jo'natish/qaytarish sanasi, qatorlar hujjat bo'yicha,
       tashriflar sanasi);
     - ruxsat modullari DstDashboard (rahbar paneli), DstReports (savdo hisobotlari) — Admin'ga beriladi
       (faqat to'ldirilgan bazada; bo'sh baza — Seed/105).
   Idempotent.
   ===================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.D_SHIPMENTS') AND name = 'IX_DST_D_SHIPMENTS_Date')
    CREATE INDEX IX_DST_D_SHIPMENTS_Date ON dst.D_SHIPMENTS (DocDate, Status) INCLUDE (OrderID, WarehouseID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.D_SHIPMENT_LINES') AND name = 'IX_DST_D_SHIPMENT_LINES_Shipment')
    CREATE INDEX IX_DST_D_SHIPMENT_LINES_Shipment ON dst.D_SHIPMENT_LINES (ShipmentID) INCLUDE (ProductID, QtyBase, Amount, CostAmount);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.D_RETURNS') AND name = 'IX_DST_D_RETURNS_Date')
    CREATE INDEX IX_DST_D_RETURNS_Date ON dst.D_RETURNS (DocDate, Status) INCLUDE (CustomerID, OrderID, WarehouseID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.D_RETURN_LINES') AND name = 'IX_DST_D_RETURN_LINES_Return')
    CREATE INDEX IX_DST_D_RETURN_LINES_Return ON dst.D_RETURN_LINES (ReturnID) INCLUDE (ProductID, QtyBase, Amount, UnitCost);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.VISITS') AND name = 'IX_DST_VISITS_Date')
    CREATE INDEX IX_DST_VISITS_Date ON dst.VISITS (VisitDate) INCLUDE (UserID, CustomerID, IsPlanned, Result, OrderID, PaymentID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dst.CASH_HANDOVERS') AND name = 'IX_DST_CASH_HANDOVERS_Date')
    CREATE INDEX IX_DST_CASH_HANDOVERS_Date ON dst.CASH_HANDOVERS (DocDate) INCLUDE (AgentUserID, Status, AmountDeclared, AmountReceived);
GO

-- ---------- ruxsat modullari ----------
IF EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES)
BEGIN
    INSERT INTO dbo.PERMISSIONMODULES (Module, Controller, Action, Parameter)
    SELECT v.Controller, v.Controller, 'Index', NULL
    FROM (VALUES ('DstDashboard'), ('DstReports')) v (Controller)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONMODULES pm WHERE pm.Module = v.Controller);

    DECLARE @admin int = (SELECT ID FROM dbo.ROLES WHERE SystemCode = 'ADMIN');
    IF @admin IS NOT NULL
        INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
        SELECT 1, 1, @admin, pm.ID, 1
        FROM dbo.PERMISSIONMODULES pm
        WHERE pm.Module IN ('DstDashboard', 'DstReports')
          AND NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = @admin AND p.PermissionModuleID = pm.ID);
END
GO
