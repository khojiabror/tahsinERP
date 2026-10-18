/* =====================================================================
   006 — SRM xarid moduli FK/filtr indekslari (SRM-B14, SRM-P03/P05).
   Manba: Database_Migrations/Proposal_SRM_Procurement_Indexes.sql (lokal DBTHSN-IPACK'da qo'llangan).
   Raqam ustunlaridagi global UNIQUE indekslar baseline'da bor — bu yerda faqat FK/filtr indekslari.
   Idempotent (IF NOT EXISTS): mavjud bazada no-op.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;  -- hisoblangan ustunli jadvallar (P_CONTRACT_PRODUCTS, P_INVOICE_PRODUCTS) indekslari uchun shart
SET ANSI_NULLS ON;

-- ---------- FK / issiq filtrlar ----------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_ORDER_PRODUCTS_OrderID' AND object_id = OBJECT_ID('dbo.P_ORDER_PRODUCTS'))
    CREATE INDEX IX_P_ORDER_PRODUCTS_OrderID       ON dbo.P_ORDER_PRODUCTS(OrderID) INCLUDE (ProductID, Amount, Price, UnitID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_ORDER_PRODUCTS_ProductID' AND object_id = OBJECT_ID('dbo.P_ORDER_PRODUCTS'))
    CREATE INDEX IX_P_ORDER_PRODUCTS_ProductID     ON dbo.P_ORDER_PRODUCTS(ProductID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_CONTRACT_PRODUCTS_ContractID' AND object_id = OBJECT_ID('dbo.P_CONTRACT_PRODUCTS'))
    CREATE INDEX IX_P_CONTRACT_PRODUCTS_ContractID ON dbo.P_CONTRACT_PRODUCTS(ContractID) INCLUDE (ProductID, Quantity, Price, VATAmount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_CONTRACT_PRODUCTS_ProductID' AND object_id = OBJECT_ID('dbo.P_CONTRACT_PRODUCTS'))
    CREATE INDEX IX_P_CONTRACT_PRODUCTS_ProductID  ON dbo.P_CONTRACT_PRODUCTS(ProductID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICE_PRODUCTS_InvoiceID' AND object_id = OBJECT_ID('dbo.P_INVOICE_PRODUCTS'))
    CREATE INDEX IX_P_INVOICE_PRODUCTS_InvoiceID   ON dbo.P_INVOICE_PRODUCTS(InvoiceID) INCLUDE (ProductID, Quantity, Price, UnitID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICE_PRODUCTS_ProductID' AND object_id = OBJECT_ID('dbo.P_INVOICE_PRODUCTS'))
    CREATE INDEX IX_P_INVOICE_PRODUCTS_ProductID   ON dbo.P_INVOICE_PRODUCTS(ProductID) INCLUDE (InvoiceID, Quantity, UnitID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICE_DOCS_InvoiceID' AND object_id = OBJECT_ID('dbo.P_INVOICE_DOCS'))
    CREATE INDEX IX_P_INVOICE_DOCS_InvoiceID       ON dbo.P_INVOICE_DOCS(InvoiceID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_CONTRACT_DOCS_ContractID' AND object_id = OBJECT_ID('dbo.P_CONTRACT_DOCS'))
    CREATE INDEX IX_P_CONTRACT_DOCS_ContractID     ON dbo.P_CONTRACT_DOCS(ContractID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_REQUISITION_PRODUCTS_ReqID' AND object_id = OBJECT_ID('dbo.P_REQUISITION_PRODUCTS'))
    CREATE INDEX IX_P_REQUISITION_PRODUCTS_ReqID   ON dbo.P_REQUISITION_PRODUCTS(RequisitionID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_REQUISITION_APPROVALS_ReqID' AND object_id = OBJECT_ID('dbo.P_REQUISITION_APPROVALS'))
    CREATE INDEX IX_P_REQUISITION_APPROVALS_ReqID  ON dbo.P_REQUISITION_APPROVALS(RequisitionID, SortOrder);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICES_Supplier_Date' AND object_id = OBJECT_ID('dbo.P_INVOICES'))
    CREATE INDEX IX_P_INVOICES_Supplier_Date ON dbo.P_INVOICES(SupplierID, InvoiceDate) INCLUDE (Amount, ContractID, OrderID, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICES_ContractID' AND object_id = OBJECT_ID('dbo.P_INVOICES'))
    CREATE INDEX IX_P_INVOICES_ContractID    ON dbo.P_INVOICES(ContractID) INCLUDE (Amount, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICES_OrderID' AND object_id = OBJECT_ID('dbo.P_INVOICES'))
    CREATE INDEX IX_P_INVOICES_OrderID       ON dbo.P_INVOICES(OrderID) INCLUDE (IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_INVOICES_InvoiceDate' AND object_id = OBJECT_ID('dbo.P_INVOICES'))
    CREATE INDEX IX_P_INVOICES_InvoiceDate   ON dbo.P_INVOICES(InvoiceDate) INCLUDE (SupplierID, Amount, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_ORDERS_ContractID' AND object_id = OBJECT_ID('dbo.P_ORDERS'))
    CREATE INDEX IX_P_ORDERS_ContractID      ON dbo.P_ORDERS(ContractID) INCLUDE (Amount, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_ORDERS_IssuedDate' AND object_id = OBJECT_ID('dbo.P_ORDERS'))
    CREATE INDEX IX_P_ORDERS_IssuedDate      ON dbo.P_ORDERS(IssuedDate) INCLUDE (PurchaseType, SupplierID, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_P_CONTRACTS_Supplier' AND object_id = OBJECT_ID('dbo.P_CONTRACTS'))
    CREATE INDEX IX_P_CONTRACTS_Supplier     ON dbo.P_CONTRACTS(SupplierID, IssuedDate) INCLUDE (IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INCOMES_InvoiceID' AND object_id = OBJECT_ID('dbo.INCOMES'))
    CREATE INDEX IX_INCOMES_InvoiceID        ON dbo.INCOMES(InvoiceID) INCLUDE (IssueDateTime, IsDeleted);
GO
