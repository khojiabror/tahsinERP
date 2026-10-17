/*
    TAKLIF — SRM xarid moduli indekslari (SRM-B14, SRM-P03/P05). ISHGA TUSHIRILMAGAN.

    - Avval jonli bazada mavjud indekslarni tekshiring:
        SELECT t.name, i.name, i.is_unique, i.filter_definition
        FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
        WHERE t.name IN ('P_ORDERS','P_ORDER_PRODUCTS','P_CONTRACTS','P_CONTRACT_PRODUCTS',
                         'P_INVOICES','P_INVOICE_PRODUCTS','P_INVOICE_DOCS','P_CONTRACT_DOCS',
                         'P_REQUISITIONS','P_REQUISITION_PRODUCTS','P_REQUISITION_APPROVALS');
    - Unique indekslardan oldin DataFix_SRM_Procurement_02_DuplicateNumbers_Find.sql bo'sh
      natija berishi kerak.
    - Filtrlangan indeksda OR ishlatib bo'lmaydi, shuning uchun filtr "IsDeleted = 0".
      Faol yozuvlarda IsDeleted NULL bo'lsa, ular indeksdan tashqarida qoladi — avval
      NULL'larni 0 ga keltiring: UPDATE dbo.P_INVOICES SET IsDeleted = 0 WHERE IsDeleted IS NULL;
      (P_ORDERS, P_CONTRACTS, P_REQUISITIONS uchun ham).
*/

-- ---------- Unique (faol yozuvlar orasida) ----------
CREATE UNIQUE INDEX UX_P_ORDERS_OrderNo_Active
    ON dbo.P_ORDERS(OrderNo) WHERE IsDeleted = 0 AND OrderNo IS NOT NULL;

CREATE UNIQUE INDEX UX_P_REQUISITIONS_No_Active
    ON dbo.P_REQUISITIONS(RequisitionNo) WHERE IsDeleted = 0 AND RequisitionNo IS NOT NULL;

CREATE UNIQUE INDEX UX_P_CONTRACTS_Supplier_No_Active
    ON dbo.P_CONTRACTS(SupplierID, ContractNo) WHERE IsDeleted = 0 AND ContractNo IS NOT NULL;

CREATE UNIQUE INDEX UX_P_INVOICES_Supplier_No_Active
    ON dbo.P_INVOICES(SupplierID, InvoiceNo) WHERE IsDeleted = 0 AND InvoiceNo IS NOT NULL;

-- ---------- Raqam generatori (DocumentNumberService: WHERE col LIKE 'PREFIX%') ----------
-- Yuqoridagi unique indekslar faqat faol yozuvlarni qamraydi; MAX hisobi o'chirilganlarni ham oladi.
CREATE INDEX IX_P_INVOICES_InvoiceNo   ON dbo.P_INVOICES(InvoiceNo);
CREATE INDEX IX_P_CONTRACTS_ContractNo ON dbo.P_CONTRACTS(ContractNo);

-- ---------- FK / issiq filtrlar ----------
CREATE INDEX IX_P_ORDER_PRODUCTS_OrderID       ON dbo.P_ORDER_PRODUCTS(OrderID) INCLUDE (ProductID, Amount, Price, UnitID);
CREATE INDEX IX_P_ORDER_PRODUCTS_ProductID     ON dbo.P_ORDER_PRODUCTS(ProductID);
CREATE INDEX IX_P_CONTRACT_PRODUCTS_ContractID ON dbo.P_CONTRACT_PRODUCTS(ContractID) INCLUDE (ProductID, Quantity, Price, VATAmount);
CREATE INDEX IX_P_CONTRACT_PRODUCTS_ProductID  ON dbo.P_CONTRACT_PRODUCTS(ProductID);
CREATE INDEX IX_P_INVOICE_PRODUCTS_InvoiceID   ON dbo.P_INVOICE_PRODUCTS(InvoiceID) INCLUDE (ProductID, Quantity, Price, UnitID);
CREATE INDEX IX_P_INVOICE_PRODUCTS_ProductID   ON dbo.P_INVOICE_PRODUCTS(ProductID) INCLUDE (InvoiceID, Quantity, UnitID);
CREATE INDEX IX_P_INVOICE_DOCS_InvoiceID       ON dbo.P_INVOICE_DOCS(InvoiceID);
CREATE INDEX IX_P_CONTRACT_DOCS_ContractID     ON dbo.P_CONTRACT_DOCS(ContractID);
CREATE INDEX IX_P_REQUISITION_PRODUCTS_ReqID   ON dbo.P_REQUISITION_PRODUCTS(RequisitionID);
CREATE INDEX IX_P_REQUISITION_APPROVALS_ReqID  ON dbo.P_REQUISITION_APPROVALS(RequisitionID, SortOrder);

CREATE INDEX IX_P_INVOICES_Supplier_Date ON dbo.P_INVOICES(SupplierID, InvoiceDate) INCLUDE (Amount, ContractID, OrderID, IsDeleted);
CREATE INDEX IX_P_INVOICES_ContractID    ON dbo.P_INVOICES(ContractID) INCLUDE (Amount, IsDeleted);
CREATE INDEX IX_P_INVOICES_OrderID       ON dbo.P_INVOICES(OrderID) INCLUDE (IsDeleted);
CREATE INDEX IX_P_INVOICES_InvoiceDate   ON dbo.P_INVOICES(InvoiceDate) INCLUDE (SupplierID, Amount, IsDeleted);
CREATE INDEX IX_P_ORDERS_ContractID      ON dbo.P_ORDERS(ContractID) INCLUDE (Amount, IsDeleted);
CREATE INDEX IX_P_ORDERS_IssuedDate      ON dbo.P_ORDERS(IssuedDate) INCLUDE (PurchaseType, SupplierID, IsDeleted);
CREATE INDEX IX_P_CONTRACTS_Supplier     ON dbo.P_CONTRACTS(SupplierID, IssuedDate) INCLUDE (IsDeleted);
CREATE INDEX IX_INCOMES_InvoiceID        ON dbo.INCOMES(InvoiceID) INCLUDE (IssueDateTime, IsDeleted);
