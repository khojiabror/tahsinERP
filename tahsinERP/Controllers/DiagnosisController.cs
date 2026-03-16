using System;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Mijoz korxonasi uchun diagnosis (holatni baholash) sahifasi.
    /// Login bo'lgan foydalanuvchi mijoz ma'lumotlarini kiritib,
    /// moliya, supply chain va boshqa yo'nalishlar bo'yicha yulduzli bahoni ko'radi.
    /// </summary>
    [AllowAnonymous]
    public class DiagnosisController : Controller
    {
        // GET: /Diagnosis
        [HttpGet]
        public ActionResult Index()
        {
            var model = new DiagnosisInputViewModel();
            return View(model);
        }

        // POST: /Diagnosis
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(DiagnosisInputViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var result = CalculateDiagnosis(input);
            input.Result = result;
            return View(input);
        }

        /// <summary>
        /// Kiritilgan ma'lumotlar asosida ko'p-bosqichli baholashni amalga oshiradi.
        /// Bloklar: Moliya, Supply chain, Ombor, Shaffoflik, Avtomatizatsiya, Qulaylik.
        /// Natijada 1–5 yulduz oralig'ida ball, qisqa izoh va tahsinERP modullari bo'yicha tavsiyalar qaytaradi.
        /// </summary>
        private DiagnosisResultViewModel CalculateDiagnosis(DiagnosisInputViewModel input)
        {
            // 1. Moliya (Finance)
            // Indikatorlar:
            // - Sof foyda marjasi
            // - Naqd pul oqimi rejalashtirilishi
            // - Byudjetlash mavjudligi
            // - Debitor/kreditorlarni tizimda kuzatish
            double financeScore = 0;

            financeScore += Math.Max(0, Math.Min(40, input.ProfitMarginPercent)); // 0–40%
            if (input.HasCashflowPlanning) financeScore += 20;
            if (input.HasBudgeting) financeScore += 20;
            if (input.TracksReceivablesPayablesInSystem) financeScore += 20;

            int financeStars = ToStars(financeScore, out string financeLabel);

            // 2. Supply chain (ta'minot zanjiri)
            // Indikatorlar:
            // - Yetkazib beruvchilar soni/diversifikatsiya
            // - Lead time kuzatuvi
            // - Xarid jarayonining integratsiyasi
            // - Min/Max zaxira darajalari
            double supplyScore = 0;

            if (input.SuppliersCount >= 3 && input.SuppliersCount <= 10) supplyScore += 25;
            else if (input.SuppliersCount > 10) supplyScore += 20;
            else if (input.SuppliersCount >= 1) supplyScore += 10;

            if (input.HasLeadTimeTracking) supplyScore += 25;
            if (input.HasIntegratedPurchasing) supplyScore += 30;
            if (input.HasMinMaxLevels) supplyScore += 20;

            int supplyStars = ToStars(supplyScore, out string supplyLabel);

            // 3. Ombor boshqaruvi (Inventory)
            // Indikatorlar:
            // - BOM mavjudligi
            // - Inventarizatsiya chastotasi
            // - Onlayn qoldiqni ko'rish
            // - Ombor xatoliklarini tahlil qilish
            double inventoryScore = 0;

            if (input.HasBOM) inventoryScore += 30;
            if (input.InventoryCheckPerYear >= 4) inventoryScore += 30;
            else if (input.InventoryCheckPerYear >= 1) inventoryScore += 20;

            if (input.CanSeeStockOnline) inventoryScore += 20;
            if (input.AnalyzesWarehouseErrors) inventoryScore += 20;

            int inventoryStars = ToStars(inventoryScore, out string inventoryLabel);

            // 4. Shaffoflik (Transparency)
            // Indikatorlar:
            // - Avtomatik hisobotlar
            // - KPI dashboard
            // - Rahbariyat uchun vizual hisobotlar
            double transparencyScore = 0;

            if (input.HasAutomaticReports) transparencyScore += 35;
            if (input.CanSeeKPIsDashboard) transparencyScore += 35;
            if (input.HasManagementDashboards) transparencyScore += 30;

            int transparencyStars = ToStars(transparencyScore, out string transparencyLabel);

            // 5. Avtomatizatsiya (Automation)
            // Indikator: jarayonlar avtomatizatsiya darajasi
            double automationScore = 0;

            if (input.ProcessAutomationLevel == ProcessAutomationLevel.FullyManual) automationScore += 20;
            else if (input.ProcessAutomationLevel == ProcessAutomationLevel.PartiallyAutomated) automationScore += 60;
            else if (input.ProcessAutomationLevel == ProcessAutomationLevel.MostlyAutomated) automationScore += 90;

            int automationStars = ToStars(automationScore, out string automationLabel);

            // 6. Qulaylik (Usability)
            // Indikatorlar:
            // - Tizimdan foydalanayotgan userlar soni
            // - Trening/materiallar
            // - Tizimdan qoniqish
            // - Key user/super userlar mavjudligi
            double usabilityScore = 0;

            if (input.UsersCountUsingSystem >= 20) usabilityScore += 30;
            else if (input.UsersCountUsingSystem >= 5) usabilityScore += 20;

            if (input.HasTrainingMaterials) usabilityScore += 25;
            if (input.UsersSatisfiedWithSystem) usabilityScore += 25;
            if (input.HasKeyUsers) usabilityScore += 20;

            int usabilityStars = ToStars(usabilityScore, out string usabilityLabel);

            // Modul tavsiyalari (SRM, MES, CRM, Finance, HRM, Tasks, BOM va hokazo)
            var financeModules = RecommendModulesForFinance(financeStars);
            var supplyModules = RecommendModulesForSupplyChain(supplyStars);
            var inventoryModules = RecommendModulesForInventory(inventoryStars);
            var transparencyModules = RecommendModulesForTransparency(transparencyStars);
            var automationModules = RecommendModulesForAutomation(automationStars);
            var usabilityModules = RecommendModulesForUsability(usabilityStars);

            var combinedModules = string.Join(", ",
                string.Join(", ", financeModules),
                string.Join(", ", supplyModules),
                string.Join(", ", inventoryModules),
                string.Join(", ", transparencyModules),
                string.Join(", ", automationModules),
                string.Join(", ", usabilityModules));

            // Unikal qilish
            var uniqueCombined = string.Join(", ",
                combinedModules
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase));

            // Marketing-tildagi diagnoz matnlari
            var financeNarrative = BuildFinanceNarrative(financeStars, financeModules);
            var supplyNarrative = BuildSupplyChainNarrative(supplyStars, supplyModules);
            var inventoryNarrative = BuildInventoryNarrative(inventoryStars, inventoryModules);
            var transparencyNarrative = BuildTransparencyNarrative(transparencyStars, transparencyModules);
            var automationNarrative = BuildAutomationNarrative(automationStars, automationModules);
            var usabilityNarrative = BuildUsabilityNarrative(usabilityStars, usabilityModules);

            var overallNarrative =
                "Yuqoridagi baholar bo'yicha ko'rinib turibdiki, hozirgi uslubingiz (qog'oz/Excel/oddiy accounting) " +
                "kundalik boshqaruvda sizni ancha ushlab turibdi. tahsinERP modullarini bosqichma-bosqich joriy etish " +
                "orqali jarayonlarni raqamlashtirib, real vaqtda natijalarni ko'rish, buyurtmalarni nazorat qilish va " +
                "xodimlarning ishini yengillashtirish mumkin.";

            return new DiagnosisResultViewModel
            {
                FinanceStars = financeStars,
                FinanceLabel = financeLabel,
                SupplyChainStars = supplyStars,
                SupplyChainLabel = supplyLabel,
                InventoryStars = inventoryStars,
                InventoryLabel = inventoryLabel,
                TransparencyStars = transparencyStars,
                TransparencyLabel = transparencyLabel,
                AutomationStars = automationStars,
                AutomationLabel = automationLabel,
                UsabilityStars = usabilityStars,
                UsabilityLabel = usabilityLabel,
                FinanceRecommendedModules = string.Join(", ", financeModules),
                SupplyChainRecommendedModules = string.Join(", ", supplyModules),
                InventoryRecommendedModules = string.Join(", ", inventoryModules),
                TransparencyRecommendedModules = string.Join(", ", transparencyModules),
                AutomationRecommendedModules = string.Join(", ", automationModules),
                UsabilityRecommendedModules = string.Join(", ", usabilityModules),
                CombinedRecommendedModules = uniqueCombined,
                FinanceNarrative = financeNarrative,
                SupplyChainNarrative = supplyNarrative,
                InventoryNarrative = inventoryNarrative,
                TransparencyNarrative = transparencyNarrative,
                AutomationNarrative = automationNarrative,
                UsabilityNarrative = usabilityNarrative,
                OverallNarrative = overallNarrative
            };
        }

        private int ToStars(double score, out string label)
        {
            // 0–100 ball -> 1–5 yulduz
            int stars;
            if (score <= 20) stars = 1;
            else if (score <= 40) stars = 2;
            else if (score <= 60) stars = 3;
            else if (score <= 80) stars = 4;
            else stars = 5;

            if (stars <= 1) label = "Juda past daraja - tartibsizlik ko'p";
            else if (stars == 2) label = "Boshlang'ich daraja - asosiy jarayonlar boshqarilmoqda, lekin katta bo'shliqlar bor";
            else if (stars == 3) label = "O'rtacha daraja - ishlayapti, lekin tahsinERP bilan ancha optimizatsiya qilish mumkin";
            else if (stars == 4) label = "Yaxshi daraja - tizim bor, tahsinERP bilan yanada silliq va shaffof qilish mumkin";
            else label = "Yuqori daraja - kuchli jarayonlar, tahsinERP bilan integratsiya qilib skalalash mumkin";

            return stars;
        }

        // Quyidagi mappinglar soddalashtirilgan jahon tajribasi asosida:
        // yulduz past bo'lsa - ko'proq modul tavsiya qilinadi, yuqori bo'lsa - kamroq (optimallashtirish).

        private string[] RecommendModulesForFinance(int stars)
        {
            if (stars <= 2) return new[] { "Finance", "CRM", "Tasks" };
            if (stars == 3) return new[] { "Finance", "CRM" };
            if (stars == 4) return new[] { "Finance" };
            return new[] { "Finance (advanced reporting)" };
        }

        private string[] RecommendModulesForSupplyChain(int stars)
        {
            if (stars <= 2) return new[] { "SRM", "Inventory", "Tasks" };
            if (stars == 3) return new[] { "SRM", "Inventory" };
            if (stars == 4) return new[] { "SRM" };
            return new[] { "SRM (advanced planning)" };
        }

        private string[] RecommendModulesForInventory(int stars)
        {
            if (stars <= 2) return new[] { "Inventory", "BOM", "MES" };
            if (stars == 3) return new[] { "Inventory", "BOM" };
            if (stars == 4) return new[] { "Inventory" };
            return new[] { "Inventory (analytics)" };
        }

        private string[] RecommendModulesForTransparency(int stars)
        {
            if (stars <= 2) return new[] { "Finance", "CRM", "Reporting/Dashboard" };
            if (stars == 3) return new[] { "Reporting/Dashboard", "Finance" };
            if (stars == 4) return new[] { "Reporting/Dashboard" };
            return new[] { "Advanced analytics" };
        }

        private string[] RecommendModulesForAutomation(int stars)
        {
            if (stars <= 2) return new[] { "MES", "Tasks", "Workflow" };
            if (stars == 3) return new[] { "MES", "Tasks" };
            if (stars == 4) return new[] { "Tasks" };
            return new[] { "Optimization tools" };
        }

        private string[] RecommendModulesForUsability(int stars)
        {
            if (stars <= 2) return new[] { "HRM", "Tasks", "Training/Knowledge Base" };
            if (stars == 3) return new[] { "HRM", "Training/Knowledge Base" };
            if (stars == 4) return new[] { "HRM" };
            return new[] { "Talent & development" };
        }

        // Quyidagi metodlar har blok bo'yicha tadbirkor og'riqlari va tahsinERP modullari foydasini
        // marketing tilida izohlab beradi. Keyinchalik bu matnlar Gemini uchun kontekst sifatida ishlatilishi mumkin.

        private string BuildFinanceNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Hozir foyda-zararni faqat oy oxirida yoki Excel orqali taxmin qilasiz, real vaqtda qaysi mahsulot " +
                    "yoki yo'nalish qancha foyda berayotganini ko'rish qiyin. tahsinERP’dagi " + mods +
                    " modullarini to'g'ri yuritsangiz, har bir buyurtma, xarid va sotuv avtomatik bog'lanadi va " +
                    "bir-ikki klikda real-time foyda/zarar hisobotlarini ko'rasiz.";
            }

            if (stars == 3)
            {
                return
                    "Moliya hisobi ishlayapti, lekin ko'p narsa Excel va qo'lda hisob-kitoblarga tayanadi. " +
                    "tahsinERP’dagi " + mods +
                    " modullari bilan foyda-zarar, debitor/kreditor va kassa/bank harakatlari yagona tizimda " +
                    "ko'rinadi, hisobotlar avtomatik shakllanadi.";
            }

            return
                "Moliya jarayonlari nisbatan tartibga solingan, lekin tahsinERP’dagi " + mods +
                " modullari yordamida real-time analitika va chuqur tahlillarni tezroq va qulayroq olishingiz mumkin.";
        }

        private string BuildSupplyChainNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Buyurtmalar, schyotlar va yetkazib berish muddatlarini ko'p hollarda telefon/Telegram yoki Excel orqali " +
                    "boshqarasiz, kechikishlar va zaxira yetishmovchiligi tez-tez uchraydi. tahsinERP’dagi " + mods +
                    " modullari bilan buyurtma → qabul → to'lov zanjiri bitta tizimda yuradi, supplierlar bo'yicha tarix va " +
                    "narxlar to'liq ko'rinadi, lead time qisqaradi.";
            }

            if (stars == 3)
            {
                return
                    "Ta'minot zanjiri ishlayapti, lekin ma'lumotlar turli fayl va dasturlarga tarqalgan. " +
                    "tahsinERP’dagi " + mods +
                    " modullari orqali xaridlar, yetkazib berishlar va zaxira rejalashtirish yagona platformada boshqariladi.";
            }

            return
                "Supply chain jarayonlari nisbatan yaxshi, lekin tahsinERP’dagi " + mods +
                " modullari sizga yanada chuqur tahlil va rejalashtirish (planning) imkoniyatlarini beradi.";
        }

        private string BuildInventoryNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Ombor bo'yicha qog'oz va haqiqat o'rtasida katta farqlar bor, inventarizatsiyada kutilmagan " +
                    "kamomad va ortiqchalar chiqadi. tahsinERP’dagi " + mods +
                    " modullari bilan har bir mahsulot va xomashyo bo'yicha aniq kartochkalar yuritiladi, " +
                    "kirim-chiqim avtomatik yoziladi va BOM asosida ishlab chiqarish nazorat qilinadi.";
            }

            if (stars == 3)
            {
                return
                    "Zaxira hisobini yuritish bor, lekin ko'p qo'lda va Excel asosida. tahsinERP’dagi " + mods +
                    " modullari orqali real qoldiq, aylanish va xatoliklarni tahlil qilish osonlashadi.";
            }

            return
                "Ombor hisobingiz yomon emas, lekin tahsinERP’dagi " + mods +
                " modullari yordamida zaxira optimallashtirish va chuqur analitikani avtomatlashtirish mumkin.";
        }

        private string BuildTransparencyNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Rahbariyat yoki investor hisobot so'raganda, siz har safar Excel yig'ib, formulalarni tekshirib " +
                    "ko'p vaqt yo'qotasiz. tahsinERP’dagi " + mods +
                    " yordamida foyda-zarar, sotuv, zaxira va debitor/kreditor bo'yicha avtomatik dashboardlar " +
                    "shakllanadi, qaror qabul qilish tezlashadi.";
            }

            if (stars == 3)
            {
                return
                    "Asosiy hisobotlar bor, lekin ularni tayyorlash va yangilash ko'p qo'lda ish talab qiladi. " +
                    "tahsinERP’dagi " + mods +
                    " bilan siz ko'proq real vaqtdagi ko'rsatkichlarga tayana boshlaysiz.";
            }

            return
                "Shaffoflik darajangiz fena emas, lekin tahsinERP’dagi " + mods +
                " bilan ko'rsatkichlarni yanada chuqurroq, segmentlar bo'yicha tahlil qilish imkoniga ega bo'lasiz.";
        }

        private string BuildAutomationNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Ko'p jarayonlar (buyurtma, schyot, qabul, tasdiqlash) qo'lda va qog'oz/Excel asosida yuradi, " +
                    "xodimlar bir joydan ikkinchi joyga hujjat ko'tarib yurishga majbur. tahsinERP’dagi " + mods +
                    " bilan bu bosqichlar tizimda avtomatik status va eslatmalar bilan boshqariladi, chaos kamayadi.";
            }

            if (stars == 3)
            {
                return
                    "Jarayonlarning bir qismi avtomatlashtirilgan, biroq hali ham ko'p qo'lda ish bor. " +
                    "tahsinERP’dagi " + mods +
                    " bilan bu jarayonlarni yanada to'liqroq raqamlashtirish mumkin.";
            }

            return
                "Avtomatizatsiya darajangiz yaxshi, lekin tahsinERP’dagi " + mods +
                " yordamida mavjud jarayonlarni yanada optimallashtirish va 'bottleneck'larni kamaytirish mumkin.";
        }

        private string BuildUsabilityNarrative(int stars, string[] modules)
        {
            var mods = string.Join(", ", modules);
            if (stars <= 2)
            {
                return
                    "Xodimlar hozirgi uslubdan charchagan, ko'p xato va qayta ishlashlar bo'lmoqda, yangi xodimni " +
                    "o'rgatish uzoq vaqt oladi. tahsinERP’dagi " + mods +
                    " orqali rollar va vazifalar aniq bo'ladi, trening/materiallar bir joyda to'planadi va tizim " +
                    "odamlar uchun ancha qulaylashadi.";
            }

            if (stars == 3)
            {
                return
                    "Tizimdan foydalanish yomon emas, lekin ko'p narsa 'ustoz' xodimlarga bog'langan. " +
                    "tahsinERP’dagi " + mods +
                    " yordamida bilimni tizimlashtirish va yangi xodimlarni tezroq ishga moslashtirish mumkin.";
            }

            return
                "Qulaylik darajasi yaxshi, lekin tahsinERP’dagi " + mods +
                " bilan xodimlar uchun yanada intuitiv interfeys va strukturalangan bilim bazasini ta'minlash mumkin.";
        }
    }
}

