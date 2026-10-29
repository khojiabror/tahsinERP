/* =====================================================================
   tahsinERP — yangi korxona: tanlangan modullar profili (New-TenantDb.ps1, seed va demo'dan keyin).

   dbo.SETUP_MODULES (032) dagi modullar bo'yicha boshlang'ich sozlama (jadval bo'sh — hamma modul):
     1. Rollar: "Rahbar" (har doim — tanlangan hamma bo'limni ko'radi, tasdiqlaydi, Distributsiya maxsus huquqlari) va har tanlangan
        modulga ish roli (masalan "Sotuv menejeri"); Savdoda alohida: operator, omborchi, kassir; dalada: savdo agenti, ekspeditor
        (qo'llanma 1.10.1) + chegirma limitlari. PERMISSIONS — #matrix bo'yicha aniq: o'z ishi C (ko'rish + yaratish/tahrirlash),
        qo'shni modullarning kerakli bo'limlari V (faqat ko'rish), asosiy spravochniklar V, topshiriqlar — ishlash C, sozlamalari V.
        O'chirish — hech bir rolga (faqat Admin). Admin/Developer o'zgarmaydi (to'liq huquq, 110_Defaults). Tanlanmagan modul
        bo'limlariga hech bir rol huquq olmaydi; menyuni litsenziya (Edition) ham yopadi. Foydalanuvchilar yaratilmaydi — admin
        xodimlarni qo'shib, shu rollardan birini beradi.
     2. Omborlar: tanlanmagan modul ombori (harakati yo'q bo'lsa) o'chirib qo'yiladi — PRODUCTION (Mes), SUPPLY (Srm/Mes),
        SALES (Crm/Mes/Commerce/Distribution/Website); Savdo — sotuv ombori dst.WAREHOUSE_SETTINGS da savdo nuqtasi.
     3. Mahsulot kategoriyalari: bazada mahsulot yo'q bo'lsa (namuna ma'lumotsiz) — modulga mos boshlang'ich kategoriyalar.
   Valyutalar, o'lchov birliklari, ТНВЭД, moliya kategoriyalari, hujjat/topshiriq spravochniklari — Core (100_Reference) — hammaga
   bir xil: keyin paket kengaytirilsa ham qayta seed kerak bo'lmaydi.

   Controller -> modul xaritasi tahsinERP.Licensing/Editions/EditionModules.cs bilan BIR XIL bo'lishi shart
   (tahsinERP.ControlPanel.Tests: ModuleProfileSqlTests tekshiradi). Idempotent; bitta batch.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @now datetime = GETDATE();
CREATE TABLE #sel (Module varchar(30) COLLATE DATABASE_DEFAULT PRIMARY KEY);
INSERT INTO #sel (Module)
SELECT v.M FROM (VALUES ('Srm'), ('Crm'), ('Tasks'), ('Mes'), ('Engineering'), ('Finance'), ('Hr'), ('Logistics'), ('Docs'), ('Commerce'), ('Distribution'), ('Website')) v (M)
WHERE dbo.fn_SetupHasModule(v.M) = 1;
-- dala Savdo'siz ishlamaydi: eski SETUP_MODULES yozuvi (Commerce'dan oldin) faqat 'Distribution' bo'lishi mumkin
IF EXISTS (SELECT 1 FROM #sel WHERE Module = 'Distribution') AND NOT EXISTS (SELECT 1 FROM #sel WHERE Module = 'Commerce')
    INSERT INTO #sel (Module) VALUES ('Commerce');

/* ---------------- controller -> modul (EditionModules.ControllerMap; Core yozilmaydi) ---------------- */
CREATE TABLE #cm (Module varchar(30) COLLATE DATABASE_DEFAULT, Controller varchar(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
INSERT INTO #cm (Module, Controller) VALUES
-- @module-map-begin
('Srm', 'SRMDashboard'), ('Srm', 'Supplier'), ('Srm', 'SupplierBase'), ('Srm', 'PRequisitions'), ('Srm', 'PContracts'), ('Srm', 'POrders'),
('Srm', 'PInvoices'), ('Srm', 'PurchaseOrderTracking'), ('Srm', 'Tender'), ('Srm', 'HSCode'), ('Srm', 'Reports'), ('Srm', 'Incomes'),
('Srm', 'Expenses'), ('Srm', 'Inventory'), ('Srm', 'InventoryReports'), ('Srm', 'ProductionToSalesTransfer'),
('Crm', 'CRMDashboard'), ('Crm', 'Customer'), ('Crm', 'CustomerBase'), ('Crm', 'SContract'), ('Crm', 'SOrder'), ('Crm', 'SalesInvoices'),
('Crm', 'SalesReports'), ('Crm', 'SalesSF'), ('Crm', 'SalesTTNK'),
('Tasks', 'Tasks'), ('Tasks', 'TaskDashboard'), ('Tasks', 'TaskKanban'), ('Tasks', 'TaskChangeStatus'), ('Tasks', 'TaskPriority'),
('Tasks', 'TaskProject'), ('Tasks', 'TaskStatus'),
('Mes', 'MESDashboard'), ('Mes', 'MESReports'), ('Mes', 'ProductPlan'), ('Mes', 'ProductProduction'), ('Mes', 'QualityInspection'),
('Mes', 'DefectType'), ('Mes', 'DamagedProduct'), ('Mes', 'ProductionProcess'),
('Engineering', 'Bom'), ('Engineering', 'Norm'), ('Engineering', 'Tannarx'), ('Engineering', 'TannarxAnalytics'),
('Finance', 'FinDashboard'), ('Finance', 'FinAccounts'), ('Finance', 'FinTransactions'), ('Finance', 'FinCategories'),
('Finance', 'FinBalanceCategories'), ('Finance', 'FinPlItems'), ('Finance', 'FinPlPlans'), ('Finance', 'FinRecurringObligations'),
('Finance', 'OpeningBalance'), ('Finance', 'Shareholder'),
('Hr', 'HrDashboard'), ('Hr', 'HrEmployees'), ('Hr', 'HrEmployeeHistory'), ('Hr', 'HrOrgUnits'), ('Hr', 'HrPositions'), ('Hr', 'HrSalaryGrades'),
('Hr', 'HrStaffingPlan'), ('Hr', 'HrTimesheets'), ('Hr', 'HrPieceRateTimesheets'), ('Hr', 'HrPieceRateTariffs'), ('Hr', 'HrPayroll'),
('Hr', 'HrSettings'), ('Hr', 'Koreshok'),
('Logistics', 'LogisticsDashboard'), ('Logistics', 'Forwarder'), ('Logistics', 'FContract'), ('Logistics', 'FContractTariff'),
('Logistics', 'FTransportType'), ('Logistics', 'Waybill'), ('Logistics', 'WaybillImport'), ('Logistics', 'WaybillLookup'),
('Logistics', 'WaybillPayment'), ('Logistics', 'TransportLoadSimulation'), ('Logistics', 'TransportLoadApi'),
('Docs', 'OfficialLetters'), ('Docs', 'CompanyOrders'), ('Docs', 'STDCompanyStandards'), ('Docs', 'JobDesc'), ('Docs', 'DocTemplates'),
('Commerce', 'DstHome'), ('Distribution', 'DstTerritories'), ('Commerce', 'DstCustomers'), ('Commerce', 'DstProducts'),
('Commerce', 'DstDictionaries'), ('Commerce', 'DstPrices'), ('Commerce', 'DstTeams'), ('Commerce', 'DstSettings'), ('Commerce', 'DstChannels'),
('Commerce', 'DstImport'), ('Commerce', 'DstOrders'), ('Commerce', 'DstStock'), ('Commerce', 'DstPayments'),
('Commerce', 'DstReturns'), ('Commerce', 'DstDebts'), ('Distribution', 'DstField'), ('Commerce', 'DstApi'),
('Commerce', 'DstDashboard'), ('Commerce', 'DstReports'), ('Commerce', 'DstInventory'), ('Commerce', 'DstIssues'),
('Commerce', 'DstTransfers'), ('Commerce', 'DstWriteOffs'), ('Commerce', 'DstRegrades'), ('Commerce', 'DstLandedCosts'),
('Commerce', 'DstReorder'), ('Commerce', 'DstPromotions'), ('Distribution', 'DstVehicles'), ('Distribution', 'DstTrips'),
('Distribution', 'DstRoutes'), ('Distribution', 'DstKpi'), ('Commerce', 'DstAudit'),
('Website', 'SiteHome'), ('Website', 'SiteSettings'), ('Website', 'SiteCatalog'), ('Website', 'SitePages'), ('Website', 'SiteLeads'), ('Website', 'SiteApi'),
-- @module-map-end
-- xaritadan tashqari ruxsat yozuvlari (controller emas): Savdo maxsus huquqlari (DstSeeCost, DstOverrideCredit...)
('Commerce', 'Dst');

/* ---------------- 1. standart rollar va ruxsat matritsasi ---------------- */
-- Rahbar (Director) — har doim: tanlangan hamma bo'limni ko'radi va tasdiqlaydi. Qolganlari — modul tanlansa: har modulga ish roli,
-- Distributsiyada qo'llanma 1.10.1 jadvali bo'yicha alohida rollar (operator, omborchi, kassir, agent, ekspeditor).
-- Code — skript ichidagi kalit (#matrix.Role); Module — rol qaysi modul bilan yaratiladi (NULL — har doim); RName — ROLES.RName (varchar 25).
CREATE TABLE #roles (Code varchar(30) COLLATE DATABASE_DEFAULT PRIMARY KEY, Module varchar(30) COLLATE DATABASE_DEFAULT NULL,
                     RName varchar(25) COLLATE DATABASE_DEFAULT, Description varchar(200) COLLATE DATABASE_DEFAULT, ID int NULL);
INSERT INTO #roles (Code, Module, RName, Description)
SELECT r.Code, r.Module, r.RName, r.Descr
FROM (VALUES ('Director', NULL, 'Rahbar', 'Barcha bo''limlarni ko''radi; buyurtma, hisobdan chiqarish va limitdan oshishni tasdiqlaydi'),
             ('Srm', 'Srm', 'Ta''minot menejeri', 'Xarid, ta''minotchilar va omborlar'),
             ('Crm', 'Crm', 'Sotuv menejeri', 'Mijozlar, shartnomalar, buyurtmalar va sotuv invoyslari'),
             ('Mes', 'Mes', 'Ishlab chiqarish ustasi', 'Ishlab chiqarish rejasi, hisobi va sifat nazorati'),
             ('Engineering', 'Engineering', 'Texnolog', 'BOM, normalar va tannarx'),
             ('Finance', 'Finance', 'Hisobchi', 'Pul hisoblari, to''lovlar, moliyaviy hisobotlar'),
             ('Hr', 'Hr', 'Kadrlar mutaxassisi', 'Xodimlar, tabel va oylik'),
             ('Logistics', 'Logistics', 'Logist', 'Ekspeditorlar, tariflar va yo''l varaqalari'),
             ('Docs', 'Docs', 'Devonxona mudiri', 'Rasmiy xatlar, buyruqlar va standartlar'),
             ('DstOperator', 'Commerce', 'Savdo operatori', 'Buyurtmalarni tasdiqlash, mijozlar, qaytarish, import'),
             ('DstStorekeeper', 'Commerce', 'Omborchi', 'Yig''ish, jo''natish, inventarizatsiya, ko''chirma, hisobdan chiqarish'),
             ('DstCashier', 'Commerce', 'Kassir', 'Mijoz to''lovlari va agentlar naqdini qabul qilish'),
             ('DstAgent', 'Distribution', 'Savdo agenti', 'Telegram bot va Mini App: tashrif, buyurtma, to''lov'),
             ('DstCourier', 'Distribution', 'Ekspeditor', 'Telegram bot: reys, yetkazish, manzilda to''lov'),
             ('Website', 'Website', 'Sayt menejeri', 'Sayt-katalog: mahsulotlar, sahifalar, so''rovlar')) r (Code, Module, RName, Descr)
WHERE r.Module IS NULL OR EXISTS (SELECT 1 FROM #sel s WHERE s.Module = r.Module);

INSERT INTO dbo.ROLES (RName, Description, IsDeleted)
SELECT r.RName, r.Description, 0 FROM #roles r
WHERE NOT EXISTS (SELECT 1 FROM dbo.ROLES x WHERE x.RName = r.RName AND ISNULL(x.IsDeleted, 0) = 0);
UPDATE r SET ID = x.ID FROM #roles r JOIN dbo.ROLES x ON x.RName = r.RName AND ISNULL(x.IsDeleted, 0) = 0;

/* Ruxsat matritsasi: rol (#roles.Code) -> qaysi bo'limni ko'radi (V) yoki o'zgartiradi (C = ko'rish + yaratish/tahrirlash).
   Target = modul nomi yoki 'Core' (asosiy spravochniklar); Controller = '*' — modulning barcha bo'limlari, aks holda aniq controller.
   Qator faqat rol yaratilgan VA target moduli tanlangan bo'lsa qo'llanadi (Core — har doim). O'chirish (DeletePermit) — faqat Admin.
   Bir bo'limga ikki qator tushsa kuchlisi (C) olinadi. Jadval README "Rollar va ruxsatlar" bilan bir xil. */
CREATE TABLE #matrix (Role varchar(30) COLLATE DATABASE_DEFAULT, Target varchar(30) COLLATE DATABASE_DEFAULT,
                      Controller varchar(50) COLLATE DATABASE_DEFAULT, Access char(1) COLLATE DATABASE_DEFAULT);
INSERT INTO #matrix (Role, Target, Controller, Access) VALUES
-- har bir rol: asosiy spravochniklar — ko'rish (bosh sahifa, mahsulot, o'lchov birligi, valyuta, ombor, korxona)
('*', 'Core', 'Home', 'V'), ('*', 'Core', 'Account', 'V'), ('*', 'Core', 'Product', 'V'), ('*', 'Core', 'ProductCategory', 'V'),
('*', 'Core', 'Unit', 'V'), ('*', 'Core', 'Currency', 'V'), ('*', 'Core', 'Warehouse', 'V'), ('*', 'Core', 'Company', 'V'),
('*', 'Core', 'ProductStock', 'V'),
-- har bir rol: topshiriqlar — o'z topshiriqlari bilan ishlash (C), sozlamalar (holat, muhimlik, loyiha) — ko'rish
('*', 'Tasks', 'Tasks', 'C'), ('*', 'Tasks', 'TaskKanban', 'C'), ('*', 'Tasks', 'TaskChangeStatus', 'C'), ('*', 'Tasks', 'TaskDashboard', 'V'),
('*', 'Tasks', 'TaskProject', 'V'), ('*', 'Tasks', 'TaskStatus', 'V'), ('*', 'Tasks', 'TaskPriority', 'V'),
-- Rahbar: tanlangan hamma modulni ko'radi, topshiriqlarni boshqaradi, Distributsiyada qo'llanmadagi "Rahbar" ustuni (audit jurnali — faqat
-- Admin), sayt so'rovlari bilan ishlaydi. Maxsus huquqlar (tannarx, limitdan oshish, hisobdan chiqarish tasdig'i) — pastda, #grant'da.
('Director', 'Srm', '*', 'V'), ('Director', 'Crm', '*', 'V'), ('Director', 'Mes', '*', 'V'), ('Director', 'Engineering', '*', 'V'),
('Director', 'Finance', '*', 'V'), ('Director', 'Hr', '*', 'V'), ('Director', 'Logistics', '*', 'V'), ('Director', 'Docs', '*', 'V'),
('Director', 'Website', '*', 'V'), ('Director', 'Website', 'SiteLeads', 'C'), ('Director', 'Tasks', '*', 'C'),
('Director', 'Commerce', 'DstHome', 'V'), ('Director', 'Commerce', 'DstDashboard', 'V'), ('Director', 'Commerce', 'DstReports', 'V'),
('Director', 'Commerce', 'DstStock', 'V'), ('Director', 'Commerce', 'DstDebts', 'V'), ('Director', 'Commerce', 'DstTransfers', 'V'),
('Director', 'Distribution', 'DstField', 'V'),
('Director', 'Commerce', 'DstOrders', 'C'), ('Director', 'Commerce', 'DstPayments', 'C'), ('Director', 'Commerce', 'DstReturns', 'C'),
('Director', 'Commerce', 'DstCustomers', 'C'), ('Director', 'Commerce', 'DstProducts', 'C'), ('Director', 'Commerce', 'DstPrices', 'C'),
('Director', 'Commerce', 'DstPromotions', 'C'), ('Director', 'Distribution', 'DstTerritories', 'C'), ('Director', 'Commerce', 'DstTeams', 'C'),
('Director', 'Commerce', 'DstDictionaries', 'C'), ('Director', 'Commerce', 'DstSettings', 'C'), ('Director', 'Commerce', 'DstImport', 'C'),
('Director', 'Commerce', 'DstInventory', 'C'), ('Director', 'Commerce', 'DstIssues', 'C'), ('Director', 'Commerce', 'DstWriteOffs', 'C'),
('Director', 'Commerce', 'DstRegrades', 'C'), ('Director', 'Commerce', 'DstLandedCosts', 'C'), ('Director', 'Commerce', 'DstReorder', 'C'),
('Director', 'Distribution', 'DstRoutes', 'C'), ('Director', 'Distribution', 'DstTrips', 'C'), ('Director', 'Distribution', 'DstVehicles', 'C'),
('Director', 'Distribution', 'DstKpi', 'C'),
-- Ta'minot menejeri
('Srm', 'Srm', '*', 'C'), ('Srm', 'Core', 'Product', 'C'), ('Srm', 'Core', 'ProductCategory', 'C'),
('Srm', 'Mes', 'ProductPlan', 'V'), ('Srm', 'Logistics', 'Waybill', 'V'), ('Srm', 'Finance', 'FinTransactions', 'V'),
-- Sotuv menejeri
('Crm', 'Crm', '*', 'C'), ('Crm', 'Srm', 'InventoryReports', 'V'),
('Crm', 'Mes', 'ProductPlan', 'V'), ('Crm', 'Logistics', 'Waybill', 'V'), ('Crm', 'Finance', 'FinTransactions', 'V'),
-- Ishlab chiqarish ustasi
('Mes', 'Mes', '*', 'C'), ('Mes', 'Engineering', '*', 'V'), ('Mes', 'Srm', 'Incomes', 'V'), ('Mes', 'Srm', 'Expenses', 'V'),
('Mes', 'Srm', 'InventoryReports', 'V'), ('Mes', 'Srm', 'ProductionToSalesTransfer', 'C'),
-- Texnolog
('Engineering', 'Engineering', '*', 'C'), ('Engineering', 'Mes', '*', 'V'), ('Engineering', 'Core', 'Product', 'C'),
('Engineering', 'Core', 'ProductCategory', 'C'),
-- Hisobchi
('Finance', 'Finance', '*', 'C'), ('Finance', 'Core', 'Currency', 'C'),
('Finance', 'Srm', 'PInvoices', 'V'), ('Finance', 'Srm', 'PContracts', 'V'),
('Finance', 'Crm', 'SalesInvoices', 'V'), ('Finance', 'Crm', 'SContract', 'V'), ('Finance', 'Hr', 'HrPayroll', 'V'),
('Finance', 'Logistics', 'WaybillPayment', 'V'), ('Finance', 'Logistics', 'FContract', 'V'), ('Finance', 'Commerce', 'DstPayments', 'V'),
('Finance', 'Commerce', 'DstDebts', 'V'),
-- Kadrlar mutaxassisi
('Hr', 'Hr', '*', 'C'), ('Hr', 'Docs', 'CompanyOrders', 'V'), ('Hr', 'Docs', 'JobDesc', 'V'),
-- Logist
('Logistics', 'Logistics', '*', 'C'), ('Logistics', 'Srm', 'PInvoices', 'V'), ('Logistics', 'Srm', 'POrders', 'V'),
('Logistics', 'Crm', 'SalesInvoices', 'V'), ('Logistics', 'Crm', 'SOrder', 'V'), ('Logistics', 'Distribution', 'DstTrips', 'V'),
-- Devonxona mudiri
('Docs', 'Docs', '*', 'C'), ('Docs', 'Hr', 'HrEmployees', 'V'), ('Docs', 'Hr', 'HrOrgUnits', 'V'), ('Docs', 'Hr', 'HrPositions', 'V'),
-- Distributsiya operatori (qo'llanma 1.10.1 "Operator" ustuni) — telefon buyurtmasi, tasdiqlash, mijozlar, import, sayt so'rovlari
('DstOperator', 'Commerce', 'DstHome', 'V'), ('DstOperator', 'Commerce', 'DstReports', 'V'), ('DstOperator', 'Commerce', 'DstStock', 'V'),
('DstOperator', 'Commerce', 'DstPayments', 'V'), ('DstOperator', 'Commerce', 'DstDebts', 'V'), ('DstOperator', 'Commerce', 'DstPrices', 'V'),
('DstOperator', 'Commerce', 'DstPromotions', 'V'), ('DstOperator', 'Commerce', 'DstDictionaries', 'V'), ('DstOperator', 'Commerce', 'DstIssues', 'V'),
('DstOperator', 'Commerce', 'DstOrders', 'C'), ('DstOperator', 'Commerce', 'DstReturns', 'C'), ('DstOperator', 'Commerce', 'DstCustomers', 'C'),
('DstOperator', 'Commerce', 'DstProducts', 'C'), ('DstOperator', 'Distribution', 'DstTerritories', 'C'), ('DstOperator', 'Commerce', 'DstImport', 'C'),
('DstOperator', 'Distribution', 'DstTrips', 'C'), ('DstOperator', 'Core', 'Product', 'C'), ('DstOperator', 'Website', 'SiteLeads', 'C'),
-- Omborchi — yig'ish, jo'natish, sanoq, ko'chirma qabuli, hisobdan chiqarish (tasdiq — Rahbar)
('DstStorekeeper', 'Commerce', 'DstHome', 'V'), ('DstStorekeeper', 'Commerce', 'DstStock', 'V'), ('DstStorekeeper', 'Commerce', 'DstReturns', 'V'),
('DstStorekeeper', 'Commerce', 'DstProducts', 'V'), ('DstStorekeeper', 'Commerce', 'DstIssues', 'V'), ('DstStorekeeper', 'Commerce', 'DstReorder', 'V'),
('DstStorekeeper', 'Distribution', 'DstTrips', 'V'),
('DstStorekeeper', 'Commerce', 'DstOrders', 'C'), ('DstStorekeeper', 'Commerce', 'DstInventory', 'C'), ('DstStorekeeper', 'Commerce', 'DstTransfers', 'C'),
('DstStorekeeper', 'Commerce', 'DstWriteOffs', 'C'), ('DstStorekeeper', 'Commerce', 'DstRegrades', 'C'),
-- Kassir — ofis to'lovlari, agentlar naqdini qabul qilish
('DstCashier', 'Commerce', 'DstHome', 'V'), ('DstCashier', 'Commerce', 'DstOrders', 'V'), ('DstCashier', 'Commerce', 'DstDebts', 'V'),
('DstCashier', 'Commerce', 'DstCustomers', 'V'), ('DstCashier', 'Commerce', 'DstIssues', 'V'), ('DstCashier', 'Commerce', 'DstPayments', 'C'),
('DstCashier', 'Finance', 'FinTransactions', 'V'),
-- Savdo agenti — asosan bot, botdan buyurtma yuborish uchun DstOrders C shart
('DstAgent', 'Commerce', 'DstHome', 'V'), ('DstAgent', 'Commerce', 'DstIssues', 'V'), ('DstAgent', 'Commerce', 'DstOrders', 'C'),
('DstAgent', 'Distribution', 'DstField', 'C'),
-- Ekspeditor — bot: reys natijasi
('DstCourier', 'Commerce', 'DstHome', 'V'), ('DstCourier', 'Commerce', 'DstOrders', 'V'), ('DstCourier', 'Commerce', 'DstIssues', 'V'),
('DstCourier', 'Distribution', 'DstTrips', 'C'),
-- Sayt menejeri
('Website', 'Website', '*', 'C'), ('Website', 'Commerce', 'DstPrices', 'V'), ('Website', 'Commerce', 'DstStock', 'V'),
('Website', 'Crm', 'Customer', 'V');

CREATE TABLE #grant (RoleID int, PermissionModuleID int, Chg bit, PRIMARY KEY (RoleID, PermissionModuleID));
;WITH m AS (
    SELECT r.ID AS RoleID, x.Target, x.Controller, x.Access
    FROM #matrix x JOIN #roles r ON x.Role = r.Code OR x.Role = '*'
    WHERE x.Target = 'Core' OR EXISTS (SELECT 1 FROM #sel s WHERE s.Module = x.Target)),
c AS (   -- '*' -> modulning barcha controllerlari (Dst maxsus yozuvlari bundan mustasno)
    SELECT m.RoleID, cm.Controller, m.Access FROM m JOIN #cm cm ON cm.Module = m.Target WHERE m.Controller = '*' AND cm.Controller <> 'Dst'
    UNION ALL
    SELECT m.RoleID, m.Controller, m.Access FROM m WHERE m.Controller <> '*')
INSERT INTO #grant (RoleID, PermissionModuleID, Chg)
SELECT c.RoleID, pm.ID, MAX(CASE WHEN c.Access = 'C' THEN 1 ELSE 0 END)
FROM c JOIN dbo.PERMISSIONMODULES pm ON pm.Controller = c.Controller
GROUP BY c.RoleID, pm.ID;

-- Savdo maxsus huquqlari (PERMISSIONMODULES.Controller = 'Dst', Module bo'yicha; DstPermissions — Change bilan): Rahbar —
-- tannarxni ko'rish, kredit limitidan oshishga ruxsat, hisobdan chiqarishni tasdiqlash. Cheksiz chegirma — faqat Admin.
IF EXISTS (SELECT 1 FROM #sel WHERE Module = 'Commerce')
    INSERT INTO #grant (RoleID, PermissionModuleID, Chg)
    SELECT r.ID, pm.ID, 1 FROM #roles r
    JOIN dbo.PERMISSIONMODULES pm ON pm.Controller = 'Dst' AND pm.Module IN ('DstSeeCost', 'DstOverrideCredit', 'DstWriteOffApprove')
    WHERE r.Code = 'Director' AND NOT EXISTS (SELECT 1 FROM #grant g WHERE g.RoleID = r.ID AND g.PermissionModuleID = pm.ID);

-- Asosiy menyu (sidebar, navbar) — PERMISSIONMODULES.Module = 'Layout', Controller'i yo'q, shuning uchun yuqoridagi controller
-- bo'yicha bog'lanishga tushmaydi. Usiz rol egasi tizimga kirganda bo'sh sahifani ko'radi (_Layout.cshtml) — har rolga ko'rish.
INSERT INTO #grant (RoleID, PermissionModuleID, Chg)
SELECT r.ID, pm.ID, 0 FROM #roles r
JOIN dbo.PERMISSIONMODULES pm ON pm.Module = 'Layout'
WHERE r.ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM #grant g WHERE g.RoleID = r.ID AND g.PermissionModuleID = pm.ID);

-- mavjud yozuv bo'lsa ham matritsaga tenglanadi (profil qayta ishlasa ham natija bir xil)
UPDATE p SET ViewPermit = 1, ChangePermit = g.Chg, DeletePermit = 0
FROM dbo.PERMISSIONS p JOIN #grant g ON g.RoleID = p.RoleID AND g.PermissionModuleID = p.PermissionModuleID;
INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
SELECT 1, g.Chg, g.RoleID, g.PermissionModuleID, 0 FROM #grant g
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = g.RoleID AND p.PermissionModuleID = g.PermissionModuleID);

-- distributsiya: qo'lda chegirma limiti (qo'llanma 1.2.3) — agent 3%, operator 5%, rahbar 10%; qolgan rollar chegirma bermaydi
IF EXISTS (SELECT 1 FROM #sel WHERE Module = 'Commerce')
    INSERT INTO dst.DISCOUNT_LIMITS (RoleID, MaxPercent)
    SELECT r.ID, v.Pct FROM #roles r JOIN (VALUES ('DstAgent', 3), ('DstOperator', 5), ('Director', 10)) v (Code, Pct) ON v.Code = r.Code
    WHERE NOT EXISTS (SELECT 1 FROM dst.DISCOUNT_LIMITS d WHERE d.RoleID = r.ID);

/* ---------------- 2. omborlar ---------------- */
DECLARE @wh TABLE (ID int PRIMARY KEY, Keep bit);
INSERT INTO @wh (ID, Keep)
SELECT w.ID,
       CASE w.WarehouseType
            WHEN 'PRODUCTION' THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module = 'Mes') THEN 1 ELSE 0 END
            WHEN 'SUPPLY'     THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Mes')) THEN 1 ELSE 0 END
            WHEN 'SALES'      THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Crm', 'Mes', 'Commerce', 'Distribution', 'Website')) THEN 1 ELSE 0 END
            ELSE 1 END
FROM dbo.WAREHOUSES w WHERE ISNULL(w.IsDeleted, 0) = 0;
UPDATE w SET IsDeleted = 1
FROM dbo.WAREHOUSES w JOIN @wh x ON x.ID = w.ID AND x.Keep = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.INCOMES i WHERE i.ReceivingWHID = w.ID OR i.SenderWHID = w.ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.EXPENSES e WHERE e.SenderWHID = w.ID OR e.ReceiverWhID = w.ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.PRODUCT_STOCKS s WHERE s.WHID = w.ID);

IF EXISTS (SELECT 1 FROM #sel WHERE Module = 'Commerce')
    INSERT INTO dst.WAREHOUSE_SETTINGS (WarehouseID, IsSalesPoint, AllowNegative, DeliveryZone)
    SELECT w.ID, 1, 0, NULL FROM dbo.WAREHOUSES w
    WHERE w.WarehouseType = 'SALES' AND ISNULL(w.IsDeleted, 0) = 0
      AND NOT EXISTS (SELECT 1 FROM dst.WAREHOUSE_SETTINGS s WHERE s.WarehouseID = w.ID);

/* ---------------- 3. boshlang'ich mahsulot kategoriyalari (namuna ma'lumotsiz bazada) ---------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.PRODUCTS)
BEGIN
    DECLARE @hasMes bit = CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module = 'Mes') THEN 1 ELSE 0 END;
    DECLARE @cat TABLE (Ord int, Name nvarchar(200), IsRaw bit, IsFg bit, RequiresNorm bit, AllowPurchase bit, AllowSales bit);
    INSERT INTO @cat
    SELECT * FROM (VALUES
        (1, N'Xomashyo va materiallar', 1, 0, 0, 1, 0),
        (2, N'Tayyor mahsulot',         0, 1, 1, 0, 1),
        (3, N'Tovarlar',                0, 1, 0, 1, 1)) v (Ord, Name, IsRaw, IsFg, RequiresNorm, AllowPurchase, AllowSales)
    WHERE (v.Ord = 1 AND EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Mes')))
       OR (v.Ord = 2 AND @hasMes = 1)
       OR (v.Ord = 3 AND @hasMes = 0 AND EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Crm', 'Commerce', 'Distribution', 'Website')));
    DELETE c FROM @cat c WHERE EXISTS (SELECT 1 FROM dbo.PRODUCT_CATEGORIES x WHERE x.Name = c.Name);

    DECLARE @catBase int = ISNULL((SELECT MAX(TRY_CAST(Code AS int)) FROM dbo.PRODUCT_CATEGORIES), 0);
    DECLARE @sortBase int = ISNULL((SELECT MAX(SortOrder) FROM dbo.PRODUCT_CATEGORIES), 0);
    INSERT INTO dbo.PRODUCT_CATEGORIES (Code, Name, IsRawMaterial, IsFinishedGood, IsComponent, IsConsumable, RequiresNORM, RequiresCutting,
                                        IncludeInMRP, AllowPurchase, AllowSales, RawMaterialGroup, RawMaterialSubGrp, SortOrder, IsActive,
                                        CreatedDT, CalculateByWeight, IsDeleted)
    SELECT RIGHT('00000' + CAST(@catBase + ROW_NUMBER() OVER (ORDER BY Ord) AS varchar(10)), 5), Name, IsRaw, IsFg, 0, 0, RequiresNorm, 0,
           CASE WHEN IsRaw = 1 OR RequiresNorm = 1 THEN 1 ELSE 0 END, AllowPurchase, AllowSales, NULL, NULL,
           @sortBase + ROW_NUMBER() OVER (ORDER BY Ord), 1, @now, 0, 0
    FROM @cat;
END

/* ---------------- 4. qadoq birliklari (savdo va ombor: quti, pachka, poddon ...) ---------------- */
-- Tovar qo'shimcha birligi (dst.PRODUCT_UNITS, import "Birlik 2") faqat UNITS ro'yxatidagi birlik bo'lishi mumkin; seed'da faqat
-- o'lchov birliklari bor. Kod bu birliklarni nom bo'yicha qidirmaydi — SystemCode faqat takror qo'shilmasligi uchun.
IF EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Crm', 'Mes', 'Commerce', 'Distribution', 'Website'))
    INSERT INTO dbo.UNITS (UnitName, ShortName, Code, IsDeleted, SystemCode)
    SELECT v.Name, v.Short, 0, 0, v.Sys
    FROM (VALUES ('quti', 'quti', 'BOX'), ('pachka', 'pachka', 'PACK'), ('blok', 'blok', 'BLOCK'),
                 ('poddon', 'poddon', 'PALLET'), ('qop', 'qop', 'BAG'), ('rulon', 'rulon', 'ROLL')) v (Name, Short, Sys)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.UNITS u WHERE u.SystemCode = v.Sys OR u.UnitName = v.Name);

DECLARE @list nvarchar(400) = (SELECT STRING_AGG(Module, ',') FROM #sel);
DECLARE @roleCount int = (SELECT COUNT(*) FROM #roles);
-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #sel, #cm, #roles, #matrix, #grant;

PRINT 'Modul profili: ' + ISNULL(@list, '-') + ' (rollar: ' + CAST(@roleCount AS varchar(10)) + ')';
