/* =====================================================================
   tahsinERP — yangi korxona: tanlangan modullar profili (New-TenantDb.ps1, seed va demo'dan keyin).

   dbo.SETUP_MODULES (032) dagi modullar bo'yicha boshlang'ich sozlama (jadval bo'sh — hamma modul):
     1. Rollar: har tanlangan modulga bitta rol (masalan "Sotuv menejeri"); PERMISSIONS — #matrix bo'yicha aniq: o'z moduli
        C (ko'rish + yaratish/tahrirlash), qo'shni modullarning kerakli bo'limlari V (faqat ko'rish), asosiy spravochniklar V
        (Ta'minot/Texnolog/Distributsiya — mahsulot C), topshiriqlar — ishlash C, sozlamalari V. O'chirish — hech bir modul roliga
        (faqat Admin). Admin/Developer o'zgarmaydi (to'liq huquq, 110_Defaults). Tanlanmagan modul bo'limlariga hech bir rol huquq
        olmaydi; menyuni litsenziya (Edition) ham yopadi.
     2. Omborlar: tanlanmagan modul ombori (harakati yo'q bo'lsa) o'chirib qo'yiladi — PRODUCTION (Mes), SUPPLY (Srm/Mes),
        SALES (Crm/Mes/Distribution/Website); Distributsiya — sotuv ombori dst.WAREHOUSE_SETTINGS da savdo nuqtasi.
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
SELECT v.M FROM (VALUES ('Srm'), ('Crm'), ('Tasks'), ('Mes'), ('Engineering'), ('Finance'), ('Hr'), ('Logistics'), ('Docs'), ('Distribution'), ('Website')) v (M)
WHERE dbo.fn_SetupHasModule(v.M) = 1;

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
('Distribution', 'DstHome'), ('Distribution', 'DstTerritories'), ('Distribution', 'DstCustomers'), ('Distribution', 'DstProducts'),
('Distribution', 'DstDictionaries'), ('Distribution', 'DstPrices'), ('Distribution', 'DstTeams'), ('Distribution', 'DstSettings'),
('Distribution', 'DstImport'), ('Distribution', 'DstOrders'), ('Distribution', 'DstStock'), ('Distribution', 'DstPayments'),
('Distribution', 'DstReturns'), ('Distribution', 'DstDebts'), ('Distribution', 'DstField'), ('Distribution', 'DstApi'),
('Distribution', 'DstDashboard'), ('Distribution', 'DstReports'), ('Distribution', 'DstInventory'), ('Distribution', 'DstIssues'),
('Distribution', 'DstTransfers'), ('Distribution', 'DstWriteOffs'), ('Distribution', 'DstRegrades'), ('Distribution', 'DstLandedCosts'),
('Distribution', 'DstReorder'), ('Distribution', 'DstPromotions'), ('Distribution', 'DstVehicles'), ('Distribution', 'DstTrips'),
('Distribution', 'DstRoutes'), ('Distribution', 'DstKpi'), ('Distribution', 'DstAudit'),
('Website', 'SiteHome'), ('Website', 'SiteSettings'), ('Website', 'SiteCatalog'), ('Website', 'SitePages'), ('Website', 'SiteLeads'), ('Website', 'SiteApi'),
-- @module-map-end
-- xaritadan tashqari ruxsat yozuvlari (controller emas): Distributsiya maxsus huquqlari (DstSeeCost, DstOverrideCredit...)
('Distribution', 'Dst');

/* ---------------- 1. modul rollari va ruxsat matritsasi ---------------- */
-- Har tanlangan modulga bitta rol. RName — ROLES.RName (varchar 25).
CREATE TABLE #roles (Module varchar(30) COLLATE DATABASE_DEFAULT PRIMARY KEY, RName varchar(25) COLLATE DATABASE_DEFAULT, Description varchar(200) COLLATE DATABASE_DEFAULT, ID int NULL);
INSERT INTO #roles (Module, RName, Description)
SELECT r.Module, r.RName, r.Descr
FROM (VALUES ('Srm', 'Ta''minot menejeri', 'Xarid, ta''minotchilar va omborlar'),
             ('Crm', 'Sotuv menejeri', 'Mijozlar, shartnomalar, buyurtmalar va sotuv invoyslari'),
             ('Mes', 'Ishlab chiqarish ustasi', 'Ishlab chiqarish rejasi, hisobi va sifat nazorati'),
             ('Engineering', 'Texnolog', 'BOM, normalar va tannarx'),
             ('Finance', 'Hisobchi', 'Pul hisoblari, to''lovlar, moliyaviy hisobotlar'),
             ('Hr', 'Kadrlar mutaxassisi', 'Xodimlar, tabel va oylik'),
             ('Logistics', 'Logist', 'Ekspeditorlar, tariflar va yo''l varaqalari'),
             ('Docs', 'Devonxona mudiri', 'Rasmiy xatlar, buyruqlar va standartlar'),
             ('Distribution', 'Distributsiya menejeri', 'Savdo nuqtalari, agentlar, buyurtmalar, qarzlar'),
             ('Website', 'Sayt menejeri', 'Sayt-katalog: mahsulotlar, sahifalar, so''rovlar')) r (Module, RName, Descr)
JOIN #sel s ON s.Module = r.Module;

INSERT INTO dbo.ROLES (RName, Description, IsDeleted)
SELECT r.RName, r.Description, 0 FROM #roles r
WHERE NOT EXISTS (SELECT 1 FROM dbo.ROLES x WHERE x.RName = r.RName AND ISNULL(x.IsDeleted, 0) = 0);
UPDATE r SET ID = x.ID FROM #roles r JOIN dbo.ROLES x ON x.RName = r.RName AND ISNULL(x.IsDeleted, 0) = 0;

/* Ruxsat matritsasi: rol (moduli) -> qaysi bo'limni ko'radi (V) yoki o'zgartiradi (C = ko'rish + yaratish/tahrirlash).
   Target = modul nomi yoki 'Core' (asosiy spravochniklar); Controller = '*' — modulning barcha bo'limlari, aks holda aniq controller.
   Qator faqat rol moduli VA target moduli tanlangan bo'lsa qo'llanadi (Core — har doim). O'chirish (DeletePermit) — faqat Admin.
   Bir bo'limga ikki qator tushsa kuchlisi (C) olinadi. Jadval README "Rollar va ruxsatlar" bilan bir xil. */
CREATE TABLE #matrix (RoleModule varchar(30) COLLATE DATABASE_DEFAULT, Target varchar(30) COLLATE DATABASE_DEFAULT,
                      Controller varchar(50) COLLATE DATABASE_DEFAULT, Access char(1) COLLATE DATABASE_DEFAULT);
INSERT INTO #matrix (RoleModule, Target, Controller, Access) VALUES
-- har bir modul roli: asosiy spravochniklar — ko'rish (bosh sahifa, mahsulot, o'lchov birligi, valyuta, ombor, korxona)
('*', 'Core', 'Home', 'V'), ('*', 'Core', 'Account', 'V'), ('*', 'Core', 'Product', 'V'), ('*', 'Core', 'ProductCategory', 'V'),
('*', 'Core', 'Unit', 'V'), ('*', 'Core', 'Currency', 'V'), ('*', 'Core', 'Warehouse', 'V'), ('*', 'Core', 'Company', 'V'),
('*', 'Core', 'ProductStock', 'V'),
-- har bir modul roli: topshiriqlar — o'z topshiriqlari bilan ishlash (C), sozlamalar (holat, muhimlik, loyiha) — ko'rish
('*', 'Tasks', 'Tasks', 'C'), ('*', 'Tasks', 'TaskKanban', 'C'), ('*', 'Tasks', 'TaskChangeStatus', 'C'), ('*', 'Tasks', 'TaskDashboard', 'V'),
('*', 'Tasks', 'TaskProject', 'V'), ('*', 'Tasks', 'TaskStatus', 'V'), ('*', 'Tasks', 'TaskPriority', 'V'),
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
('Finance', 'Logistics', 'WaybillPayment', 'V'), ('Finance', 'Logistics', 'FContract', 'V'), ('Finance', 'Distribution', 'DstPayments', 'V'),
('Finance', 'Distribution', 'DstDebts', 'V'),
-- Kadrlar mutaxassisi
('Hr', 'Hr', '*', 'C'), ('Hr', 'Docs', 'CompanyOrders', 'V'), ('Hr', 'Docs', 'JobDesc', 'V'),
-- Logist
('Logistics', 'Logistics', '*', 'C'), ('Logistics', 'Srm', 'PInvoices', 'V'), ('Logistics', 'Srm', 'POrders', 'V'),
('Logistics', 'Crm', 'SalesInvoices', 'V'), ('Logistics', 'Crm', 'SOrder', 'V'), ('Logistics', 'Distribution', 'DstTrips', 'V'),
-- Devonxona mudiri
('Docs', 'Docs', '*', 'C'), ('Docs', 'Hr', 'HrEmployees', 'V'), ('Docs', 'Hr', 'HrOrgUnits', 'V'), ('Docs', 'Hr', 'HrPositions', 'V'),
-- Distributsiya menejeri (maxsus huquqlar 'Dst' — tannarxni ko'rish, kredit limitidan o'tish, cheksiz chegirma — faqat Admin)
('Distribution', 'Distribution', '*', 'C'), ('Distribution', 'Core', 'Product', 'C'),
('Distribution', 'Finance', 'FinTransactions', 'V'), ('Distribution', 'Website', 'SiteLeads', 'V'),
-- Sayt menejeri
('Website', 'Website', '*', 'C'), ('Website', 'Distribution', 'DstPrices', 'V'), ('Website', 'Distribution', 'DstStock', 'V'),
('Website', 'Crm', 'Customer', 'V');

CREATE TABLE #grant (RoleID int, PermissionModuleID int, Chg bit, PRIMARY KEY (RoleID, PermissionModuleID));
;WITH m AS (
    SELECT r.ID AS RoleID, x.Target, x.Controller, x.Access
    FROM #matrix x JOIN #roles r ON x.RoleModule = r.Module OR x.RoleModule = '*'
    WHERE x.Target = 'Core' OR EXISTS (SELECT 1 FROM #sel s WHERE s.Module = x.Target)),
c AS (   -- '*' -> modulning barcha controllerlari (Dst maxsus yozuvlari bundan mustasno)
    SELECT m.RoleID, cm.Controller, m.Access FROM m JOIN #cm cm ON cm.Module = m.Target WHERE m.Controller = '*' AND cm.Controller <> 'Dst'
    UNION ALL
    SELECT m.RoleID, m.Controller, m.Access FROM m WHERE m.Controller <> '*')
INSERT INTO #grant (RoleID, PermissionModuleID, Chg)
SELECT c.RoleID, pm.ID, MAX(CASE WHEN c.Access = 'C' THEN 1 ELSE 0 END)
FROM c JOIN dbo.PERMISSIONMODULES pm ON pm.Controller = c.Controller
GROUP BY c.RoleID, pm.ID;

-- mavjud yozuv bo'lsa ham matritsaga tenglanadi (profil qayta ishlasa ham natija bir xil)
UPDATE p SET ViewPermit = 1, ChangePermit = g.Chg, DeletePermit = 0
FROM dbo.PERMISSIONS p JOIN #grant g ON g.RoleID = p.RoleID AND g.PermissionModuleID = p.PermissionModuleID;
INSERT INTO dbo.PERMISSIONS (ViewPermit, ChangePermit, RoleID, PermissionModuleID, DeletePermit)
SELECT 1, g.Chg, g.RoleID, g.PermissionModuleID, 0 FROM #grant g
WHERE NOT EXISTS (SELECT 1 FROM dbo.PERMISSIONS p WHERE p.RoleID = g.RoleID AND p.PermissionModuleID = g.PermissionModuleID);

-- distributsiya menejeri: qo'lda chegirma 10% gacha
IF EXISTS (SELECT 1 FROM #roles WHERE Module = 'Distribution')
    INSERT INTO dst.DISCOUNT_LIMITS (RoleID, MaxPercent)
    SELECT ID, 10 FROM #roles r WHERE r.Module = 'Distribution' AND NOT EXISTS (SELECT 1 FROM dst.DISCOUNT_LIMITS d WHERE d.RoleID = r.ID);

/* ---------------- 2. omborlar ---------------- */
DECLARE @wh TABLE (ID int PRIMARY KEY, Keep bit);
INSERT INTO @wh (ID, Keep)
SELECT w.ID,
       CASE w.WarehouseType
            WHEN 'PRODUCTION' THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module = 'Mes') THEN 1 ELSE 0 END
            WHEN 'SUPPLY'     THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Mes')) THEN 1 ELSE 0 END
            WHEN 'SALES'      THEN CASE WHEN EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Crm', 'Mes', 'Distribution', 'Website')) THEN 1 ELSE 0 END
            ELSE 1 END
FROM dbo.WAREHOUSES w WHERE ISNULL(w.IsDeleted, 0) = 0;
UPDATE w SET IsDeleted = 1
FROM dbo.WAREHOUSES w JOIN @wh x ON x.ID = w.ID AND x.Keep = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.INCOMES i WHERE i.ReceivingWHID = w.ID OR i.SenderWHID = w.ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.EXPENSES e WHERE e.SenderWHID = w.ID OR e.ReceiverWhID = w.ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.PRODUCT_STOCKS s WHERE s.WHID = w.ID);

IF EXISTS (SELECT 1 FROM #sel WHERE Module = 'Distribution')
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
       OR (v.Ord = 3 AND @hasMes = 0 AND EXISTS (SELECT 1 FROM #sel WHERE Module IN ('Srm', 'Crm', 'Distribution', 'Website')));
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

DECLARE @list nvarchar(400) = (SELECT STRING_AGG(Module, ',') FROM #sel);
DECLARE @roleCount int = (SELECT COUNT(*) FROM #roles);
-- runner barcha fayllarni bitta sessiyada bajaradi: temp jadvallar keyingi faylga o'tmasin
DROP TABLE IF EXISTS #sel, #cm, #roles, #matrix, #grant;

PRINT 'Modul profili: ' + ISNULL(@list, '-') + ' (rollar: ' + CAST(@roleCount AS varchar(10)) + ')';
