-- ============================================================================
-- PERMISSIONMODULES INSERT QUERY
-- Barcha Controller va Action'lar uchun PERMISSIONMODULES jadvaliga insert
-- Module = Controller nomi
-- Controller = Controller nomi (Controller qismi olib tashlangan)
-- Action = Action nomi
-- Parameter = NULL
-- ============================================================================

-- AdminSetting Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Account', 'Account', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Account', 'Account', 'Settings', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Account', 'Account', 'Login', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Account', 'Account', 'Logout', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('AdminPanel', 'AdminPanel', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('AdminPanel', 'AdminPanel', 'Permissions', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('AdminPanel', 'AdminPanel', 'SavePermissions', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('User', 'User', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('User', 'User', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('User', 'User', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('User', 'User', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('User', 'User', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Details', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Role', 'Role', 'Permissions', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PModule', 'PModule', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PModule', 'PModule', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PModule', 'PModule', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PModule', 'PModule', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PModule', 'PModule', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Permission', 'Permission', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Permission', 'Permission', 'Edit', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Log', 'Log', 'Index', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Home', 'Home', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Home', 'Home', 'About', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Home', 'Home', 'Contact', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('UserManual', 'UserManual', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('UserManual', 'UserManual', 'GetChapter', NULL);

-- CRM Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Customer', 'Customer', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Customer', 'Customer', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Customer', 'Customer', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Customer', 'Customer', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Customer', 'Customer', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'Details', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SOrder', 'SOrder', 'DeliverySchedule', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SContract', 'SContract', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SContract', 'SContract', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SContract', 'SContract', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SContract', 'SContract', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SContract', 'SContract', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Details', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'CreateFromOrder', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesInvoices', 'SalesInvoices', 'Print', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesSF', 'SalesSF', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesSF', 'SalesSF', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesSF', 'SalesSF', 'Details', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesSF', 'SalesSF', 'GenerateFromInvoice', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesTTNK', 'SalesTTNK', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesTTNK', 'SalesTTNK', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesTTNK', 'SalesTTNK', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('SalesTTNK', 'SalesTTNK', 'Details', NULL);

-- SRM / Purchase Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Supplier', 'Supplier', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Supplier', 'Supplier', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Supplier', 'Supplier', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Supplier', 'Supplier', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Supplier', 'Supplier', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('POrders', 'POrders', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('POrders', 'POrders', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('POrders', 'POrders', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('POrders', 'POrders', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('POrders', 'POrders', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PContracts', 'PContracts', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PContracts', 'PContracts', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PContracts', 'PContracts', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PContracts', 'PContracts', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PContracts', 'PContracts', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PInvoices', 'PInvoices', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PInvoices', 'PInvoices', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PInvoices', 'PInvoices', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PInvoices', 'PInvoices', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PInvoices', 'PInvoices', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tender', 'Tender', 'Index', NULL);

-- Warehouse Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Warehouse', 'Warehouse', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Warehouse', 'Warehouse', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Warehouse', 'Warehouse', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Warehouse', 'Warehouse', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Warehouse', 'Warehouse', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Incomes', 'Incomes', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Incomes', 'Incomes', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Incomes', 'Incomes', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Incomes', 'Incomes', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Incomes', 'Incomes', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Expenses', 'Expenses', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Expenses', 'Expenses', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Expenses', 'Expenses', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Expenses', 'Expenses', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Expenses', 'Expenses', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Inventory', 'Inventory', 'Index', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('InventoryReports', 'InventoryReports', 'Dashboard', NULL);

-- MES / Production Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductPlan', 'ProductPlan', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductPlan', 'ProductPlan', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductPlan', 'ProductPlan', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductPlan', 'ProductPlan', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductPlan', 'ProductPlan', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductProduction', 'ProductProduction', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductProduction', 'ProductProduction', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductProduction', 'ProductProduction', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductProduction', 'ProductProduction', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductProduction', 'ProductProduction', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('QualityInspection', 'QualityInspection', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('QualityInspection', 'QualityInspection', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('QualityInspection', 'QualityInspection', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('QualityInspection', 'QualityInspection', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('MESDashboard', 'MESDashboard', 'Dashboard', NULL);

-- Finance Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinAccounts', 'FinAccounts', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinAccounts', 'FinAccounts', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinAccounts', 'FinAccounts', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinAccounts', 'FinAccounts', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinAccounts', 'FinAccounts', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinTransactions', 'FinTransactions', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinTransactions', 'FinTransactions', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinTransactions', 'FinTransactions', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinTransactions', 'FinTransactions', 'Delete', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinCategories', 'FinCategories', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinCategories', 'FinCategories', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinCategories', 'FinCategories', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinCategories', 'FinCategories', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinCategories', 'FinCategories', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinBalanceCategories', 'FinBalanceCategories', 'Index', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlItems', 'FinPlItems', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlItems', 'FinPlItems', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlItems', 'FinPlItems', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlItems', 'FinPlItems', 'Delete', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlPlans', 'FinPlPlans', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlPlans', 'FinPlPlans', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlPlans', 'FinPlPlans', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinPlPlans', 'FinPlPlans', 'Delete', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinDashboard', 'FinDashboard', 'Cashflow', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinDashboard', 'FinDashboard', 'PL', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinDashboard', 'FinDashboard', 'BalanceSheet', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FinDashboard', 'FinDashboard', 'ShareholderValue', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shareholder', 'Shareholder', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shareholder', 'Shareholder', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shareholder', 'Shareholder', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shareholder', 'Shareholder', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shareholder', 'Shareholder', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Payment', 'Payment', 'Index', NULL);

-- HR Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrDashboard', 'HrDashboard', 'Index', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrOrgUnits', 'HrOrgUnits', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrOrgUnits', 'HrOrgUnits', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrOrgUnits', 'HrOrgUnits', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrOrgUnits', 'HrOrgUnits', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrOrgUnits', 'HrOrgUnits', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrEmployees', 'HrEmployees', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrEmployees', 'HrEmployees', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrEmployees', 'HrEmployees', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrEmployees', 'HrEmployees', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrEmployees', 'HrEmployees', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrSalaryGrades', 'HrSalaryGrades', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrSalaryGrades', 'HrSalaryGrades', 'Create', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrStaffingPlan', 'HrStaffingPlan', 'Index', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrTimesheets', 'HrTimesheets', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrTimesheets', 'HrTimesheets', 'Import', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HrPayroll', 'HrPayroll', 'Index', NULL);

-- Tasks Controller
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tasks', 'Tasks', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tasks', 'Tasks', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tasks', 'Tasks', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tasks', 'Tasks', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Tasks', 'Tasks', 'Details', NULL);

-- Engineering Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Product', 'Product', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Product', 'Product', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Product', 'Product', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Product', 'Product', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Product', 'Product', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('BOM', 'Bom', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('BOM', 'Bom', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('BOM', 'Bom', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('BOM', 'Bom', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('BOM', 'Bom', 'Details', NULL);
-- Note: Controller class is "BomController" but Module name is "BOM" as used in permission checks

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Norm', 'Norm', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Norm', 'Norm', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Norm', 'Norm', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Norm', 'Norm', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shop', 'Shop', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shop', 'Shop', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shop', 'Shop', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shop', 'Shop', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Shop', 'Shop', 'Details', NULL);

-- Logistics Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Forwarder', 'Forwarder', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Forwarder', 'Forwarder', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Forwarder', 'Forwarder', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Forwarder', 'Forwarder', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Forwarder', 'Forwarder', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FContract', 'FContract', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FContract', 'FContract', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FContract', 'FContract', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FContract', 'FContract', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FContract', 'FContract', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Waybill', 'Waybill', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Waybill', 'Waybill', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Waybill', 'Waybill', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Waybill', 'Waybill', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Waybill', 'Waybill', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('WaybillPayment', 'WaybillPayment', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('WaybillPayment', 'WaybillPayment', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('WaybillPayment', 'WaybillPayment', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('WaybillPayment', 'WaybillPayment', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('WaybillPayment', 'WaybillPayment', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FTransportType', 'FTransportType', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FTransportType', 'FTransportType', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FTransportType', 'FTransportType', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FTransportType', 'FTransportType', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('FTransportType', 'FTransportType', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('LogisticsDashboard', 'LogisticsDashboard', 'Index', NULL);

-- Settings Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Company', 'Company', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Company', 'Company', 'Edit', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Currency', 'Currency', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Currency', 'Currency', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Currency', 'Currency', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Currency', 'Currency', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Currency', 'Currency', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Unit', 'Unit', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Unit', 'Unit', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Unit', 'Unit', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Unit', 'Unit', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Unit', 'Unit', 'Details', NULL);

-- Other Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('CompanyOrders', 'CompanyOrders', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('CompanyOrders', 'CompanyOrders', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('CompanyOrders', 'CompanyOrders', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('CompanyOrders', 'CompanyOrders', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('CompanyOrders', 'CompanyOrders', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('OfficialLetters', 'OfficialLetters', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('OfficialLetters', 'OfficialLetters', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('OfficialLetters', 'OfficialLetters', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('OfficialLetters', 'OfficialLetters', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('OfficialLetters', 'OfficialLetters', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Reports', 'Reports', 'Index', NULL);

-- Additional Controllers
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HSCode', 'HSCode', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HSCode', 'HSCode', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HSCode', 'HSCode', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HSCode', 'HSCode', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('HSCode', 'HSCode', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductStock', 'ProductStock', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductStock', 'ProductStock', 'Download', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductStock', 'ProductStock', 'UploadWithExcel', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DefectType', 'DefectType', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DefectType', 'DefectType', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DefectType', 'DefectType', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DefectType', 'DefectType', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DefectType', 'DefectType', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedPart', 'DamagedPart', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedPart', 'DamagedPart', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedPart', 'DamagedPart', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedPart', 'DamagedPart', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedPart', 'DamagedPart', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedProduct', 'DamagedProduct', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedProduct', 'DamagedProduct', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedProduct', 'DamagedProduct', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedProduct', 'DamagedProduct', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('DamagedProduct', 'DamagedProduct', 'Details', NULL);

INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductionProcess', 'ProductionProcess', 'Index', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductionProcess', 'ProductionProcess', 'Create', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductionProcess', 'ProductionProcess', 'Edit', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductionProcess', 'ProductionProcess', 'Delete', NULL);
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('ProductionProcess', 'ProductionProcess', 'Details', NULL);

-- Layout permission (for general system access)
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('Layout', 'Layout', 'Index', NULL);

-- PurchasingOrder (for menu check)
INSERT INTO PERMISSIONMODULES (Module, Controller, Action, Parameter) VALUES ('PurchasingOrder', 'PurchasingOrder', 'Index', NULL);
