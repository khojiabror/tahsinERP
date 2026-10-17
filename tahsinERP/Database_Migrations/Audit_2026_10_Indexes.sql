-- =====================================================================
-- Audit 2026-10: issiq yo'llar uchun yetishmayotgan indekslar
-- (Docs/Audit-2026-10/CORE.md, "Indexes" bo'limi).
--
-- Xavfsiz, qayta ishga tushirish mumkin: har bir indeks faqat mavjud bo'lmasa
-- va kerakli ustunlar bo'lsa yaratiladi. Hech qanday ma'lumot o'zgarmaydi.
-- Lokal bazaga (DBTHSN-IPACK) solishtirib yozilgan; prod'da avval shu skriptni
-- test nusxada ishga tushiring. Katta jadvallarda (USERLOGS, EXPENSE_PRODUCTS)
-- indeks qurish bir necha soniya jadvalni qulflaydi — ish vaqtidan tashqarida.
-- =====================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('tempdb..#ix') IS NOT NULL DROP TABLE #ix;
CREATE TABLE #ix (TableName sysname, IndexName sysname, KeyCols nvarchar(400), IncludeCols nvarchar(400) NULL, Note nvarchar(200));

INSERT INTO #ix VALUES
-- Har so'rov: login, ruxsat snapshot'i, layout badge'lari
('USERS',              'IX_USERS_Uname',                    'Uname',                       'ID, IsActive, IsDeleted, FullName', 'login, RoleHelper snapshot'),
('USERROLES',          'IX_USERROLES_UserID',               'UserID, RoleID',              NULL,                                'rollar'),
('PERMISSIONS',        'IX_PERMISSIONS_Role_Module',        'RoleID, PermissionModuleID',  'ViewPermit, ChangePermit',          'ruxsat snapshot'),
('WAREHOUSES',         'IX_WAREHOUSES_MRP',                 'MRP',                         'IsDeleted',                         'layout badge'),
('INCOMES',            'IX_INCOMES_RecvWh_Status',          'ReceivingWHID, RecieveStatus','IsDeleted',                         'layout badge'),
('USERIMAGES',         'IX_USERIMAGES_UserID',              'UserID',                      NULL,                                'profil rasmi'),
-- Audit jurnallari
('USERLOGS',           'IX_USERLOGS_DateTime',              'DateTime',                    'UserID, ControllerName',            'Log sahifasi (ActionName text turida)'),
('USERLOGS',           'IX_USERLOGS_User_Date',             'UserID, DateTime',            NULL,                                'User Details'),
('USER_ENTRIES',       'IX_USER_ENTRIES_DateTime',          'DateTime',                    'UserID',                            'UserEntries'),
('USER_ENTRIES',       'IX_USER_ENTRIES_User_Date',         'UserID, DateTime',            NULL,                                'UserEntries'),
-- Ombor hujjatlari va qoldiq
('INCOME_PRODUCTS',    'IX_INCOME_PRODUCTS_IncomeID',       'IncomeID',                    'ProductID, Amount, UnitID',         'kirim qatorlari'),
('INCOME_PRODUCTS',    'IX_INCOME_PRODUCTS_ProductID',      'ProductID',                   'IncomeID, Amount',                  'mahsulot tarixi'),
('INCOMES',            'IX_INCOMES_IssueDate',              'IssueDateTime',               'ReceivingWHID, IsDeleted, RecieveStatus', 'hisobotlar'),
('EXPENSES',           'IX_EXPENSES_SenderWh_Date',         'SenderWHID, IssueDateTime',   'IsDeleted, ReceiverWhID',           'sarf hisobotlari'),
('EXPENSES',           'IX_EXPENSES_IssueDate',             'IssueDateTime',               'SenderWHID, IsDeleted',             'hisobotlar'),
('EXPENSE_PRODUCTS',   'IX_EXPENSE_PRODUCTS_ExpenseID',     'ExpenseID',                   'ProductID, Amount, UnitID',         'chiqim qatorlari'),
('EXPENSE_PRODUCTS',   'IX_EXPENSE_PRODUCTS_ProductID',     'ProductID',                   'ExpenseID, Amount',                 'talab statistikasi'),
('EXPENSE_PRODUCT_ALLOCATIONS', 'IX_EPA_ExpenseProductID',  'ExpenseProductID',            'LotID, FromLocationID, Qty',        'FIFO'),
('PRODUCT_STOCKS',     'IX_PRODUCT_STOCKS_Wh_Product',      'WHID, ProductID',             'Amount, UnitID',                    'qoldiq'),
('PRODUCT_STOCKS',     'IX_PRODUCT_STOCKS_Product',         'ProductID',                   'WHID, Amount',                      'mahsulot qoldig''i'),
-- Ishlab chiqarish
('PRODUCTPLANS',       'IX_PRODUCTPLANS_Product_Dates',     'ProductID, StartDate, DueDate','IsDeleted, Amount',                'reja kesishmasi'),
('PRODUCTPLANS_DAILY', 'IX_PRODUCTPLANS_DAILY_Day',         'Day',                         'PlanID, Total',                     'kunlik reja (to''liq yuklanardi)'),
('PRODUCTPLANS_DAILY', 'IX_PRODUCTPLANS_DAILY_PlanID',      'PlanID, Day',                 'Total',                             'reja tafsiloti'),
('PRODUCT_PRODUCTION', 'IX_PRODUCT_PRODUCTION_Date',        'Date',                        'ProductID, ShopID, Total, IsDeleted','MES hisobotlari'),
('PRODUCT_PRODUCTION', 'IX_PRODUCT_PRODUCTION_Product',     'ProductID, Date',             'Total, IsDeleted',                  'MES dashboard'),
('BOM_HEADERS',        'IX_BOM_HEADERS_Product',            'ProductID, IsActive',         'VersionNo',                         'BOM'),
('BOM_LINES',          'IX_BOM_LINES_BOMID',                'BOMID',                       'ComponentID, Qty, UnitID',          'BOM daraxti'),
('NORM_INPUT_LINES',   'IX_NORM_INPUT_LINES_Product_Month', 'ProductID, Month',            'MaterialID',                        'norma'),
('HR_PIECE_RATE_WORK_ENTRIES', 'IX_HR_PRWE_WorkDate',       'WorkDate',                    'EmployeeID, ProductID, OperationID, Quantity, IsDeleted', 'ishbay tabel'),
('HR_PIECE_RATE_WORK_ENTRIES', 'IX_HR_PRWE_Employee_Date',  'EmployeeID, WorkDate',        'ProductID, OperationID, Quantity',  'ishbay hisob'),
('HR_PIECE_RATE_TARIFFS', 'IX_HR_PRT_Product_Op',           'ProductID, OperationID, EffectiveFrom', 'PositionID, RatePerUnit, EffectiveTo, IsActive, IsDeleted', 'tarif qidiruvi (N+1)'),
-- Xarid/sotuv qatorlari (FK'lar SQL Server'da avtomatik indekslanmaydi)
('P_ORDER_PRODUCTS',   'IX_P_ORDER_PRODUCTS_OrderID',       'OrderID',                     'ProductID',                         'buyurtma qatorlari'),
('P_CONTRACT_PRODUCTS','IX_P_CONTRACT_PRODUCTS_ContractID', 'ContractID',                  'ProductID',                         'shartnoma qatorlari'),
('P_INVOICE_PRODUCTS', 'IX_P_INVOICE_PRODUCTS_InvoiceID',   'InvoiceID',                   'ProductID',                         'invoys qatorlari'),
('P_ORDERS',           'IX_P_ORDERS_Supplier_Date',         'SupplierID, IssuedDate',      'ContractID, IsDeleted, Amount',     'yetkazib beruvchi'),
('P_INVOICES',         'IX_P_INVOICES_Contract',            'ContractID',                  'SupplierID, Amount, IsDeleted',     'shartnoma balansi'),
('P_CONTRACTS',        'IX_P_CONTRACTS_Supplier',           'SupplierID',                  'IsDeleted, Amount',                 'yetkazib beruvchi'),
('S_ORDER_PRODUCTS',   'IX_S_ORDER_PRODUCTS_OrderID',       'OrderID',                     'ProductID',                         'buyurtma qatorlari'),
('S_CONTRACT_PRODUCTS','IX_S_CONTRACT_PRODUCTS_ContractID', 'ContractID',                  'ProductID',                         'shartnoma qatorlari'),
('S_ORDERS',           'IX_S_ORDERS_IssuedDate',            'IssuedDate',                  'Amount, ContractID, CustomerID, IsDeleted', 'CRM dashboard'),
('S_ORDERS',           'IX_S_ORDERS_Contract',              'ContractID',                  'IsDeleted',                         'shartnoma'),
('S_CONTRACTS',        'IX_S_CONTRACTS_IssuedDate',         'IssuedDate',                  'Amount, CustomerID, IsDeleted',     'CRM dashboard'),
('S_CONTRACTS',        'IX_S_CONTRACTS_Customer',           'CustomerID',                  'IsDeleted',                         'mijoz'),
-- Logistika va litsenziya
('F_WAYBILL_PAYMENT_SPLITS', 'IX_F_WPS_WaybillID',          'WaybillID',                   'Amount, PayerType',                 'to''lov taqsimoti'),
('COMPANY_LICENSES',   'IX_COMPANY_LICENSES_Company_Active','CompanyID, IsActive',         'ExpiryDate',                        'litsenziya'),
('APP_TASKS',          'IX_APP_TASKS_Reporter',             'ReporterUserID',              'StatusID, IsDeleted',               'topshiriqlar');

DECLARE @t sysname, @i sysname, @k nvarchar(400), @inc nvarchar(400), @sql nvarchar(max), @missing nvarchar(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, IndexName, KeyCols, IncludeCols FROM #ix;
OPEN c;
FETCH NEXT FROM c INTO @t, @i, @k, @inc;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t), N'U') IS NULL
        PRINT N'O''tkazildi (jadval yo''q): ' + @t + N'.' + @i;
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@t)) AND name = @i)
        PRINT N'Mavjud: ' + @t + N'.' + @i;
    ELSE
    BEGIN
        -- Barcha ustunlar mavjudligini tekshirish
        SELECT @missing = STRING_AGG(LTRIM(RTRIM(value)), N', ')
        FROM STRING_SPLIT(@k + ISNULL(N',' + @inc, N''), N',')
        WHERE COL_LENGTH(N'dbo.' + @t, LTRIM(RTRIM(value))) IS NULL;

        IF @missing IS NOT NULL
            PRINT N'O''tkazildi (ustun yo''q: ' + @missing + N'): ' + @t + N'.' + @i;
        ELSE
        BEGIN
            SET @sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@i) + N' ON dbo.' + QUOTENAME(@t)
                     + N' (' + (SELECT STRING_AGG(QUOTENAME(LTRIM(RTRIM(value))), N', ') FROM STRING_SPLIT(@k, N',')) + N')'
                     + ISNULL(N' INCLUDE (' + (SELECT STRING_AGG(QUOTENAME(LTRIM(RTRIM(value))), N', ') FROM STRING_SPLIT(@inc, N',')) + N')', N'')
                     + N';';
            BEGIN TRY
                EXEC sp_executesql @sql;
                PRINT N'Yaratildi: ' + @t + N'.' + @i;
            END TRY
            BEGIN CATCH
                PRINT N'XATO: ' + @t + N'.' + @i + N' — ' + ERROR_MESSAGE();
            END CATCH
        END
    END
    FETCH NEXT FROM c INTO @t, @i, @k, @inc;
END
CLOSE c; DEALLOCATE c;
DROP TABLE #ix;
GO

-- ---------------------------------------------------------------------
-- Ixtiyoriy: ortiqcha (dublikat) indekslar. Har biri yozishni sekinlashtiradi,
-- o'qishga foyda bermaydi (PK allaqachon ID bo'yicha). Tekshirib, qo'lda ishga tushiring.
-- ---------------------------------------------------------------------
-- DROP INDEX IX_F_CONTRACTS        ON dbo.F_CONTRACTS;        -- (ID) = PK
-- DROP INDEX IX_F_TRANSPORT_TYPES  ON dbo.F_TRANSPORT_TYPES;  -- (ID) = PK
-- DROP INDEX IX_P_ORDERS           ON dbo.P_ORDERS;           -- (ID) = PK
-- DROP INDEX IX_PRODUCTS           ON dbo.PRODUCTS;           -- (ID) = PK
-- DROP INDEX IX_S_ORDERS           ON dbo.S_ORDERS;           -- (ID) = PK
-- DROP INDEX IX_USER_LICENSES      ON dbo.USER_LICENSES;      -- (ID) = PK
-- DROP INDEX IX_TANNARX_Product_Year_Month ON dbo.TANNARX;    -- IX_TANNARX_Unique_Product_Year_Month bilan bir xil
