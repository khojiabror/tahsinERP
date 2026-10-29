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
        Distribution,   // Distributsiya (dala): agent, hudud, marshrut, reys, mashina, KPI (Areas/Distribution) — Commerce ustida
        Website,        // Sayt-katalog (Areas/Site) — paketga kirmaydi, alohida xizmat: Edition'da "+SITE"
        Commerce        // Savdo yadrosi: mijoz, narx, buyurtma, rezerv, jo'natish, to'lov, qarz, bot/Mini App (Areas/Distribution, Dst*)
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
    ///   TRIAL  = KORP                                            (5, 14 kun) — yoki xodim tanlagan modullar (pastga qarang)
    ///   DIST   = SRM, CRM, Topshiriqlar, Moliya, Savdo, Distributsiya (20) — distribyutorlar uchun yon tarmoq: MES/BOM/HR/Logistika/
    ///            Rasmiy hujjatlar yo'q (Distribution/00_PLAN.md 1, 4-faza). START→BIZNES→KORP zanjirida emas: DIST'dan
    ///            BIZNES'ga o'tish Distributsiyani yo'qotadi — tavsiyalar <see cref="SuggestEditionsFor"/> bilan.
    ///   SAVDO  = SRM, CRM, Topshiriqlar, Moliya, Savdo           (20) — online do'kon / ulgurji savdo agentlarsiz: DIST'dan dala
    ///            (agent, marshrut, reys, KPI) olib tashlangan; "+SITE" bilan sayt-katalog, bot va Mini App mijoz do'koni.
    ///
    /// Savdo (Commerce) va Distributsiya: Dst* controllerlarining buyurtma/ombor/pul qismi Commerce, dala qismi Distribution.
    /// Distribution Commerce'siz ishlamaydi (<see cref="RequiredBy"/>); eski "TRIAL:...,Distribution" kalitlarida Commerce
    /// ham ochiladi (oldin bitta modul edi). Commerce to'lovlari moliya hisoblariga yoziladi — Moliya ham kerak.
    ///
    /// Qo'shimcha xizmatlar (paketdan tashqari, alohida sotiladi) Edition'ga "+" bilan yoziladi: "KORP+SITE", "START+SITE".
    /// Kalit formati o'zgarmaydi (v2 payload'dagi Edition qatori). Hozircha: SITE → Website.
    ///
    /// Tanlangan modullar (sinov): paket kodidan keyin ":" va modul nomlari vergul bilan — "TRIAL:Srm,Crm,Tasks",
    /// "TRIAL:Mes,Engineering+SITE" (<see cref="ComposeModules"/>). Ruxsat = paket modullari ∩ ro'yxat — paketdan kengaytirib
    /// bo'lmaydi. Panel sinov formasida xodim belgilagan modullar shu yo'l bilan kalitga yetadi (alohida paket kodi kerak emas,
    /// imzolangan payload formati o'zgarmaydi). ":" ni bilmaydigan eski ERP bunday qatorni noma'lum paket deb hamma modulni ochadi.
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
        public const string Dist = "DIST";
        public const string Savdo = "SAVDO";

        private static readonly ProductModule[] StartModules = { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks };
        private static readonly ProductModule[] BiznesModules = StartModules
            .Concat(new[] { ProductModule.Mes, ProductModule.Engineering, ProductModule.Finance }).ToArray();
        private static readonly ProductModule[] KorpModules = BiznesModules
            .Concat(new[] { ProductModule.Hr, ProductModule.Logistics, ProductModule.Docs, ProductModule.Commerce, ProductModule.Distribution }).ToArray();
        private static readonly ProductModule[] SavdoModules =
            { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks, ProductModule.Finance, ProductModule.Commerce };
        private static readonly ProductModule[] DistModules = SavdoModules.Concat(new[] { ProductModule.Distribution }).ToArray();

        private static readonly Dictionary<string, HashSet<ProductModule>> EditionMap =
            new Dictionary<string, HashSet<ProductModule>>(StringComparer.OrdinalIgnoreCase)
            {
                { Start, new HashSet<ProductModule>(StartModules) },
                { Biznes, new HashSet<ProductModule>(BiznesModules) },
                { Korp, new HashSet<ProductModule>(KorpModules) },
                { Trial, new HashSet<ProductModule>(KorpModules) },
                { Dist, new HashSet<ProductModule>(DistModules) },
                { Savdo, new HashSet<ProductModule>(SavdoModules) },
            };

        /// <summary>Asosiy zanjir tartibi START → BIZNES → KORP (har biri oldingisini to'liq o'z ichiga oladi). DIST bu zanjirda emas.</summary>
        public static readonly IReadOnlyList<string> UpgradeOrder = new[] { Start, Biznes, Korp };

        /// <summary>Sotiladigan barcha paketlar, taxminiy narx tartibida (SAVDO, DIST — START va BIZNES orasida) — tavsiyalar uchun.</summary>
        public static readonly IReadOnlyList<string> SellableEditions = new[] { Start, Savdo, Dist, Biznes, Korp };

        private static readonly Dictionary<string, ProductModule> ControllerMap = Build(new Dictionary<ProductModule, string[]>
        {
            { ProductModule.Core, new[]
                {
                    "Home", "Account", "Language", "License", "Error", "ViewNotAuthorized", "ComingSoon", "HelpDocs", "UserManual",
                    "AdminPanel", "DeletedRecords", "Log", "PModule", "Role", "User", "UserEntries",
                    "Company", "Currency", "CurrencyRates", "Unit", "Warehouse", "WarehouseLocations",
                    "Product", "ProductCategory", "ProductImage", "ProductLookup", "Shop", "Branding", "Health",
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
            { ProductModule.Commerce, new[]
                {
                    "DstHome", "DstCustomers", "DstProducts", "DstDictionaries", "DstPrices",
                    "DstTeams", "DstSettings", "DstChannels", "DstImport", "DstOrders", "DstStock", "DstPayments", "DstReturns", "DstDebts",
                    "DstApi",   // bot/Mini App API: dala amallari action darajasida Distribution talab qiladi ([DstApiModule])
                    "DstDashboard", "DstReports", "DstInventory", "DstIssues",
                    "DstTransfers", "DstWriteOffs", "DstRegrades", "DstLandedCosts", "DstReorder", "DstPromotions", "DstAudit"
                } },
            { ProductModule.Distribution, new[] { "DstTerritories", "DstField", "DstVehicles", "DstTrips", "DstRoutes", "DstKpi" } },
            { ProductModule.Website, new[] { "SiteHome", "SiteSettings", "SiteCatalog", "SitePages", "SiteLeads", "SiteApi" } },
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

        /// <summary>Qo'shimcha xizmat kodi → modul.</summary>
        private static readonly Dictionary<string, ProductModule> AddOnMap = new Dictionary<string, ProductModule>(StringComparer.OrdinalIgnoreCase)
        {
            { "SITE", ProductModule.Website },
        };

        /// <summary>Paketdan tashqari sotiladigan modullar.</summary>
        public static readonly IReadOnlyList<ProductModule> AddOnModules = new[] { ProductModule.Website };

        public static bool IsAddOn(ProductModule module)
        {
            return AddOnModules.Contains(module);
        }

        /// <summary>"KORP+SITE" → "KORP", "TRIAL:Srm,Crm+SITE" → "TRIAL" (bo'sh/null — o'zi).</summary>
        public static string BaseEdition(string edition)
        {
            if (string.IsNullOrWhiteSpace(edition)) return edition;
            string head = PackagePart(edition);
            int colon = head.IndexOf(':');
            return (colon < 0 ? head : head.Substring(0, colon)).Trim();
        }

        // "+" gacha bo'lgan qism: "TRIAL:Srm,Crm+SITE" → "TRIAL:Srm,Crm"
        private static string PackagePart(string edition)
        {
            int plus = edition.IndexOf('+');
            return (plus < 0 ? edition : edition.Substring(0, plus)).Trim();
        }

        /// <summary>
        /// Edition'dagi tanlangan modullar ("TRIAL:Srm,Crm" → Srm, Crm), paket tartibida. Ro'yxat yo'q — null (paketning hamma
        /// modullari). Noma'lum nom, Core va qo'shimcha xizmat (Website — "+SITE" bilan yoziladi) e'tiborga olinmaydi.
        /// </summary>
        public static IReadOnlyList<ProductModule> SelectedModulesOf(string edition)
        {
            if (string.IsNullOrWhiteSpace(edition)) return null;
            string head = PackagePart(edition);
            int colon = head.IndexOf(':');
            if (colon < 0) return null;
            var picked = new HashSet<ProductModule>();
            foreach (var name in head.Substring(colon + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ProductModule m;
                if (TryParseModule(name, out m) && m != ProductModule.Core && !IsAddOn(m)) picked.Add(m);
            }
            if (picked.Contains(ProductModule.Distribution)) picked.Add(ProductModule.Commerce);   // eski kalit: Distribution = Savdo + dala
            return KorpModules.Where(picked.Contains).ToList();
        }

        /// <summary>Modul nomi (ProductModule, registrsiz): "srm" → Srm. Raqamli qiymat qabul qilinmaydi.</summary>
        public static bool TryParseModule(string name, out ProductModule module)
        {
            module = ProductModule.Core;
            string n = (name ?? "").Trim();
            if (n.Length == 0 || !char.IsLetter(n[0])) return false;
            return Enum.TryParse(n, true, out module) && Enum.IsDefined(typeof(ProductModule), module);
        }

        /// <summary>
        /// Modul bog'liqligi: ishlab chiqarish sarfni BOM/norma bo'yicha hisoblaydi — MES tanlansa Muhandislik ham kerak;
        /// dala (Distribution) buyurtma/ombor/qarzni Savdo'dan oladi; Savdo to'lovlari moliya hisoblariga yoziladi.
        /// </summary>
        public static IReadOnlyList<ProductModule> RequiredBy(ProductModule module)
        {
            switch (module)
            {
                case ProductModule.Mes: return new[] { ProductModule.Engineering };
                case ProductModule.Distribution: return new[] { ProductModule.Commerce, ProductModule.Finance };
                case ProductModule.Commerce: return new[] { ProductModule.Finance };
                default: return new ProductModule[0];
            }
        }

        /// <summary>
        /// Tanlangan modullarni tartibga soladi: Core olib tashlanadi, bog'liqliklar qo'shiladi, takror yo'q; paket modullari
        /// <see cref="PaidModules"/> tartibida, qo'shimcha xizmatlar oxirida.
        /// </summary>
        public static IReadOnlyList<ProductModule> NormalizeModules(IEnumerable<ProductModule> modules)
        {
            var set = new HashSet<ProductModule>((modules ?? new ProductModule[0]).Where(m => m != ProductModule.Core));
            foreach (var m in set.ToList())
                foreach (var r in RequiredBy(m)) set.Add(r);
            return KorpModules.Where(set.Contains).Concat(AddOnModules.Where(set.Contains)).ToList();
        }

        /// <summary>
        /// Paket kodi + tanlangan modullar → kalitdagi Edition qatori: ("TRIAL", [Srm, Crm, Website]) → "TRIAL:Srm,Crm+SITE".
        /// Paketning hamma modullari tanlansa ro'yxat yozilmaydi ("TRIAL+SITE"); faqat xizmat tanlansa — bo'sh ro'yxat ("TRIAL:+SITE");
        /// paketda yo'q modul tashlab yuboriladi.
        /// Natija barqaror — bir xil to'plam doim bir xil qator.
        /// </summary>
        public static string ComposeModules(string baseCode, IEnumerable<ProductModule> modules)
        {
            if (string.IsNullOrWhiteSpace(baseCode) || baseCode.IndexOf('+') >= 0 || baseCode.IndexOf(':') >= 0)
                throw new ArgumentException("Paket kodi noto'g'ri: " + baseCode);
            string code = baseCode.Trim();
            var list = NormalizeModules(modules);
            var all = EditionMap.ContainsKey(code) ? EditionMap[code] : new HashSet<ProductModule>(KorpModules);
            var package = list.Where(m => !IsAddOn(m) && all.Contains(m)).ToList();
            string head = package.Count > 0 && all.All(package.Contains) ? code : code + ":" + string.Join(",", package);
            return head + string.Concat(list.Where(IsAddOn).Select(m => "+" + AddOnMap.First(p => p.Value == m).Key));
        }

        // Paket ruxsat bergan modullar: kod bo'yicha; tanlangan ro'yxat bo'lsa — kesishmasi (paketdan kengaymaydi).
        private static HashSet<ProductModule> PackageModules(string edition)
        {
            var byCode = EditionMap[BaseEdition(edition)];
            var selected = SelectedModulesOf(edition);
            return selected == null ? byCode : new HashSet<ProductModule>(selected.Where(byCode.Contains));
        }

        /// <summary>Edition'dagi qo'shimcha xizmat modullari ("KORP+SITE" → Website).</summary>
        public static IReadOnlyList<ProductModule> AddOnsOf(string edition)
        {
            if (string.IsNullOrWhiteSpace(edition) || edition.IndexOf('+') < 0) return new ProductModule[0];
            var result = new List<ProductModule>();
            foreach (var code in edition.Substring(edition.IndexOf('+') + 1).Split(new[] { '+', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ProductModule m;
                if (AddOnMap.TryGetValue(code.Trim(), out m) && !result.Contains(m)) result.Add(m);
            }
            return result;
        }

        /// <summary>Paketga kirgan modullar (Core'siz) + qo'shimcha xizmatlar. Cheklovsiz/noma'lum paket — paket modullari hammasi.</summary>
        public static IReadOnlyList<ProductModule> ModulesOf(string edition)
        {
            var modules = IsRestrictedEdition(edition) ? KorpModules.Where(PackageModules(edition).Contains).ToList() : KorpModules.ToList();
            modules.AddRange(AddOnsOf(edition).Where(m => !modules.Contains(m)));
            return modules;
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
            return !string.IsNullOrWhiteSpace(edition) && EditionMap.ContainsKey(BaseEdition(edition));
        }

        public static bool IsModuleAllowed(string edition, ProductModule module)
        {
            if (module == ProductModule.Core || !IsRestrictedEdition(edition))
                return true;   // eski/cheklovsiz kalit — hammasi, qo'shimcha xizmatlar ham
            if (IsAddOn(module))
                return AddOnsOf(edition).Contains(module);
            return PackageModules(edition).Contains(module);
        }

        public static bool IsControllerAllowed(string edition, string controller)
        {
            return IsModuleAllowed(edition, ModuleOf(controller));
        }

        /// <summary>Modulni o'z ichiga olgan eng arzon paket asosiy zanjirda (START → BIZNES → KORP). Joriy paketni hisobga olish — <see cref="SuggestEditionsFor"/>.</summary>
        public static string CheapestEditionWith(ProductModule module)
        {
            return UpgradeOrder.FirstOrDefault(e => EditionMap[e].Contains(module)) ?? Korp;
        }

        /// <summary>
        /// Paketning boshqasini to'liq qamrab olishi: <paramref name="target"/> da <paramref name="current"/> ning barcha modullari bor.
        /// Cheklovsiz/noma'lum <paramref name="current"/> — faqat KORP/TRIAL qamraydi (hamma modul).
        /// </summary>
        public static bool Covers(string target, string current)
        {
            if (!IsRestrictedEdition(target)) return true;
            var t = PackageModules(target);
            var cur = IsRestrictedEdition(current) ? (IEnumerable<ProductModule>)PackageModules(current) : KorpModules;
            return cur.All(t.Contains);
        }

        /// <summary>
        /// "Paketingizda yo'q" sahifasi uchun tavsiya: modulni o'z ichiga olgan paketlar, avval joriy paketning hamma modullarini
        /// saqlaydiganlari (narx tartibida), keyin qolganlari. Masalan: START + Distributsiya → DIST, KORP; DIST + MES → KORP, BIZNES
        /// (BIZNES'da Distributsiya yo'q); BIZNES + Distributsiya → KORP, DIST. Qo'shimcha xizmat (Website) — bo'sh ro'yxat.
        /// </summary>
        public static IReadOnlyList<string> SuggestEditionsFor(string currentEdition, ProductModule module)
        {
            if (module == ProductModule.Core || IsAddOn(module)) return new string[0];
            var with = SellableEditions.Where(e => EditionMap[e].Contains(module)).ToList();
            string current = currentEdition;
            var keeping = with.Where(e => Covers(e, current)).ToList();
            return keeping.Concat(with.Where(e => !keeping.Contains(e))).ToList();
        }

        /// <summary>Tavsiya matni: "Distributsiya yoki Korporativ" (birinchi ikkitasi), bo'sh bo'lsa — eng arzon zanjir paketi.</summary>
        public static string SuggestionText(string currentEdition, ProductModule module)
        {
            var list = SuggestEditionsFor(currentEdition, module).Take(2).Select(EditionDisplayName).ToList();
            return list.Count == 0 ? EditionDisplayName(CheapestEditionWith(module)) : string.Join(" yoki ", list);
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
                case ProductModule.Commerce: return "Savdo (buyurtma, ombor, mijoz qarzi)";
                case ProductModule.Distribution: return "Distributsiya (agent, marshrut, yetkazish)";
                case ProductModule.Website: return "Sayt-katalog";
                default: return "Asosiy";
            }
        }

        /// <summary>Paketning foydalanuvchiga ko'rinadigan nomi.</summary>
        public static string EditionDisplayName(string edition)
        {
            var addOns = AddOnsOf(edition);
            if (addOns.Count > 0)
                return EditionDisplayName(PackagePart(edition)) + " + " + string.Join(", ", addOns.Select(DisplayName));
            var selected = SelectedModulesOf(edition);
            if (selected != null)
            {
                string code = BaseEdition(edition);
                string name = string.Equals(code, Trial, StringComparison.OrdinalIgnoreCase) ? "Sinov" : EditionDisplayName(code);
                return name + ": " + (selected.Count == 0 ? "—" : string.Join(", ", selected.Select(DisplayName)));
            }
            switch ((edition ?? "").Trim().ToUpperInvariant())
            {
                case Start: return "Start";
                case Biznes: return "Biznes";
                case Korp: return "Korporativ";
                case Trial: return "Sinov (Korporativ)";
                case Dist: return "Distributsiya";
                case Savdo: return "Savdo";
                default: return edition;
            }
        }
    }
}
