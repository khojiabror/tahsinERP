/* =====================================================================
   030 — Sotuv invoysi qaysi savdo omboridan chiqim qilgani (SALES_INVOICES.WarehouseID).
   Avval faqat bitta savdo ombori (WarehouseType = SALES) bo'lishi mumkin edi; endi bir nechta — invoysda
   ombor tanlanadi. NULL = eski invoys: standart savdo ombori (eng kichik ID) — avvalgi xatti-harakat.
   Idempotent.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.SALES_INVOICES', 'WarehouseID') IS NULL
BEGIN
    ALTER TABLE dbo.SALES_INVOICES ADD WarehouseID INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SALES_INVOICES_WAREHOUSES')
BEGIN
    ALTER TABLE dbo.SALES_INVOICES WITH CHECK
        ADD CONSTRAINT FK_SALES_INVOICES_WAREHOUSES FOREIGN KEY (WarehouseID) REFERENCES dbo.WAREHOUSES (ID);
END
GO
