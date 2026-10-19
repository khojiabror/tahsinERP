using System;
using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Services.Editions
{
    /// <summary>Mahsulot modullari (paketlar shular bo'yicha ochiladi).</summary>
    public enum ProductModule
    {
        /// <summary>Har doim ochiq: bosh sahifa, sozlamalar, admin, spravochniklar (mahsulot, ombor, valyuta...).</summary>
        Core,
        Srm,            // Ta'minot: xarid + ombor
        Crm,            // Sotuvlar
        Tasks,          // Topshiriqlar
        Mes,            // Ishlab chiqarish
        Engineering,    // BOM / Norma / Tannarx
        Finance,        // Moliya
        Hr,             // Kadrlar: oylik, tabel
        Logistics,      // Logistika
        Docs,           // Rasmiy hujjatlar
        Distribution    // Distributsiya: agent, buyurtma, rezerv, mijoz qarzi (Areas/Distribution)
    }

    /// <summary>
    /// Paket (litsenziya kalitidagi Edition) → modullar → controllerlar xaritasi. YAGONA joy — umumiy kutubxonada
    /// (tahsinERP.Licensing, netstandard2.0): ERP (EditionCheck, menyu), provisioning agenti (USERLOGS → modul) va panel
    /// (Traction: paketda bor, lekin ishlatilmayotgan modullar) bir xil xaritadan foydalanadi. Namespace ERP'dagi eski joyi bilan bir xil.
    /// Kalit = controller nomi ("Controller" qo'shimchasisiz) = PERMISSIONMODULE.Controller ustuni.
    ///
    /// Paketlar (00_PLAN.md, 11-bo'lim):
    ///   START  = SRM, CRM, Topshiriqlar                          (5 foydalanuvchi)
    ///   BIZNES = START + MES, Muhandislik, Moliya                (20)
    ///   KORP   = BIZNES + HR, Logistika, Rasmiy hujjatlar, Distributsiya (50)
    ///   TRIAL  = KORP                                            (5, 14 kun)
    ///
    /// Orqaga moslik: Edition bo'sh, eski v1 kalit yoki noma'lum nom (masalan "Standard", "Pro") — HAMMA modul ochiq.
    /// Xaritada yo'q controller ham ochiq (Core) — yangi controller qo'shilsa testi
    /// (EditionModulesTests.EveryController_IsMapped) eslatadi.
    /// </summary>
    public static class EditionModules
    {
        public const string Start = "START";
        public const string Biznes = "BIZNES";
        public const string Korp = "KORP";
        public const string Trial = "TRIAL";

        private static readonly ProductModule[] StartModules = { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks };
        private static readonly ProductModule[] BiznesModules = StartModules
            .Concat(new[] { ProductModule.Mes, ProductModule.Engineering, ProductModule.Finance }).ToArray();
        private static readonly ProductModule[] KorpModules = BiznesModules
            .Concat(new[] { ProductModule.Hr, ProductModule.Logistics, ProductModule.Docs, ProductModule.Distribution }).ToArray();

        private static readonly Dictionary<string, HashSet<ProductModule>> EditionMap =
            new Dictionary<string, HashSet<ProductModule>>(StringComparer.OrdinalIgnoreCase)
            {
                { Start, new HashSet<ProductModule>(StartModules) },
                { Biznes, new HashSet<ProductModule>(BiznesModules) },
                { Korp, new HashSet<ProductModule>(KorpModules) },
                { Trial, new HashSet<ProductModule>(KorpModules) },
            };

        /// <summary>Paketlar tartibi (tarifni oshirish sahifasida eng arzon mos paketni ko'rsatish uchun).</summary>
        public static readonly IReadOnlyList<string> UpgradeOrder = new[] { Start, Biznes, Korp };

        private static readonly Dictionary<string, ProductModule> ControllerMap = Build(new Dictionary<ProductModule, string[]>
        {
            { ProductModule.Core, new[]
                {
                    "Home", "Account", "License", "Error", "ViewNotAuthorized", "ComingSoon", "HelpDocs", "UserManual",
                    "AdminPanel", "DeletedRecords", "Log", "PModule", "Role", "User", "UserEntries",
                    "Company", "Currency", "CurrencyRates", "Unit", "Warehouse", "WarehouseLocations",
                    "Product", "ProductCategory", "ProductImage", "ProductLookup", "Shop", "Branding",
                    "Reconciliation"    // akt-sverka: xarid, sotuv va yuk tashish shartnomalari uchun umumiy
                } },
            { ProductModule.Srm, new[]
                {
                    "SRMDashboard", "Supplier", "SupplierBase", "PRequisitions", "PContracts", "POrders", "PInvoices",
                    "PurchaseOrderTracking", "Tender", "HSCode", "Reports",
                    "Incomes", "Expenses", "Inventory", "InventoryReports", "ProductionToSalesTransfer"
                } },
            { ProductModule.Crm, new[]
                {
                    "CRMDashboard", "Customer", "CustomerBase", "SContract", "SOrder", "SalesInvoices", "SalesReports",
                    "SalesSF", "SalesTTNK"
                } },
            { ProductModule.Tasks, new[]
                {
                    "Tasks", "TaskDashboard", "TaskKanban", "TaskChangeStatus", "TaskPriority", "TaskProject", "TaskStatus"
                } },
            { ProductModule.Mes, new[]
                {
                    "MESDashboard", "MESReports", "ProductPlan", "ProductProduction", "QualityInspection", "DefectType",
                    "DamagedProduct", "ProductionProcess"
                } },
            { ProductModule.Engineering, new[] { "Bom", "Norm", "Tannarx", "TannarxAnalytics" } },
            { ProductModule.Finance, new[]
                {
                    "FinDashboard", "FinAccounts", "FinTransactions", "FinCategories", "FinBalanceCategories", "FinPlItems",
                    "FinPlPlans", "FinRecurringObligations", "OpeningBalance", "Shareholder"
                } },
            { ProductModule.Hr, new[]
                {
                    "HrDashboard", "HrEmployees", "HrEmployeeHistory", "HrOrgUnits", "HrPositions", "HrSalaryGrades",
                    "HrStaffingPlan", "HrTimesheets", "HrPieceRateTimesheets", "HrPieceRateTariffs", "HrPayroll", "HrSettings", "Koreshok"
                } },
            { ProductModule.Logistics, new[]
                {
                    "LogisticsDashboard", "Forwarder", "FContract", "FContractTariff", "FTransportType", "Waybill", "WaybillImport", "WaybillLookup",
                    "WaybillPayment", "TransportLoadSimulation", "TransportLoadApi"
                } },
            { ProductModule.Docs, new[] { "OfficialLetters", "CompanyOrders", "STDCompanyStandards", "JobDesc", "DocTemplates" } },
            { ProductModule.Distribution, new[] { "DstHome" } },
        });

        private static Dictionary<string, ProductModule> Build(Dictionary<ProductModule, string[]> byModule)
        {
            var map = new Dictionary<string, ProductModule>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in byModule)
                foreach (var controller in pair.Value)
                    map.Add(controller, pair.Key); // takror bo'lsa — statik konstruktor yiqiladi (test ushlaydi)
            return map;
        }

        /// <summary>Xaritadagi barcha controller nomlari (test uchun).</summary>
        public static IEnumerable<string> MappedControllers { get { return ControllerMap.Keys; } }

        /// <summary>Controller qaysi modulga tegishli; xaritada bo'lmasa Core.</summary>
        public static ProductModule ModuleOf(string controller)
        {
            ProductModule m;
            return controller != null && ControllerMap.TryGetValue(controller.Trim(), out m) ? m : ProductModule.Core;
        }

        /// <summary>Barcha modullar (Core'dan tashqari) — paket tartibida.</summary>
        public static IReadOnlyList<ProductModule> PaidModules { get { return KorpModules; } }

        /// <summary>Paketga kirgan modullar (Core'siz). Cheklovsiz/noma'lum paket — hammasi.</summary>
        public static IReadOnlyList<ProductModule> ModulesOf(string edition)
        {
            return IsRestrictedEdition(edition) ? KorpModules.Where(m => EditionMap[edition.Trim()].Contains(m)).ToArray() : KorpModules;
        }

        // USERLOGS.ControllerName ba'zan controller nomi emas (LogHelper chaqiruvlarida o'zbekcha bo'lim nomi) — shu nomlar uchun.
        private static readonly Dictionary<string, string> LogAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Foydalanuvchilar", "User" },
            { "Omborlar", "Warehouse" },
            { "Korxona ma'lumotlari", "Company" },
            { "ТНВЭД kodlar", "HSCode" },
        };

        /// <summary>
        /// USERLOGS.ControllerName → xaritadagi controller nomi: "ProductProductionController.Details" → "ProductProduction",
        /// "SOrderController" → "SOrder", o'zbekcha bo'lim nomlari — LogAliases. Bo'sh — null.
        /// </summary>
        public static string NormalizeLogController(string logName)
        {
            if (string.IsNullOrWhiteSpace(logName)) return null;
            string s = logName.Trim();
            string alias;
            if (LogAliases.TryGetValue(s, out alias)) return alias;
            int dot = s.IndexOf('.');
            if (dot > 0) s = s.Substring(0, dot);
            if (s.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) && s.Length > "Controller".Length)
                s = s.Substring(0, s.Length - "Controller".Length);
            return s;
        }

        /// <summary>Log nomi xaritada bormi (Traction: «xaritada yo'q log nomlari» — log gigiyenasi uchun).</summary>
        public static bool IsMapped(string controller)
        {
            return controller != null && ControllerMap.ContainsKey(controller.Trim());
        }

        /// <summary>Edition modul cheklovi qo'yadimi (START/BIZNES/KORP/TRIAL). Bo'sh/v1/noma'lum — yo'q.</summary>
        public static bool IsRestrictedEdition(string edition)
        {
            return !string.IsNullOrWhiteSpace(edition) && EditionMap.ContainsKey(edition.Trim());
        }

        public static bool IsModuleAllowed(string edition, ProductModule module)
        {
            if (module == ProductModule.Core || !IsRestrictedEdition(edition))
                return true;
            return EditionMap[edition.Trim()].Contains(module);
        }

        public static bool IsControllerAllowed(string edition, string controller)
        {
            return IsModuleAllowed(edition, ModuleOf(controller));
        }

        /// <summary>Modulni o'z ichiga olgan eng arzon paket (START → BIZNES → KORP).</summary>
        public static string CheapestEditionWith(ProductModule module)
        {
            return UpgradeOrder.FirstOrDefault(e => EditionMap[e].Contains(module)) ?? Korp;
        }

        /// <summary>Modulning foydalanuvchiga ko'rinadigan nomi.</summary>
        public static string DisplayName(ProductModule module)
        {
            switch (module)
            {
                case ProductModule.Srm: return "Ta'minot (xarid va ombor)";
                case ProductModule.Crm: return "Sotuvlar (CRM)";
                case ProductModule.Tasks: return "Topshiriqlar";
                case ProductModule.Mes: return "Ishlab chiqarish (MES)";
                case ProductModule.Engineering: return "Muhandislik (BOM, norma, tannarx)";
                case ProductModule.Finance: return "Moliya";
                case ProductModule.Hr: return "Kadrlar (HR)";
                case ProductModule.Logistics: return "Logistika";
                case ProductModule.Docs: return "Rasmiy hujjatlar";
                case ProductModule.Distribution: return "Distributsiya";
                default: return "Asosiy";
            }
        }

        /// <summary>Paketning foydalanuvchiga ko'rinadigan nomi.</summary>
        public static string EditionDisplayName(string edition)
        {
            switch ((edition ?? "").Trim().ToUpperInvariant())
            {
                case Start: return "Start";
                case Biznes: return "Biznes";
                case Korp: return "Korporativ";
                case Trial: return "Sinov (Korporativ)";
                default: return edition;
            }
        }
    }
}
