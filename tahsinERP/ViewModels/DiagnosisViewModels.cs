using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Diagnosis sahifasida foydalanuvchi kiritadigan ma'lumotlar modeli.
    /// Bloklar: Moliya, Supply chain, Ombor, Shaffoflik, Avtomatizatsiya, Qulaylik.
    /// Har blok uchun 3–5 ta indikator asosida baholash qilinadi.
    /// </summary>
    public class DiagnosisInputViewModel
    {
        // Moliya (Finance) - tadbirkor og'riqlari
        [Display(Name = "Foyda-zararni real vaqtda ko'ra olasizmi? (taxminiy sof foyda marjasi, %)")]
        [Range(0, 100)]
        public double ProfitMarginPercent { get; set; }

        [Display(Name = "Naqd pul oqimini (kassa/bank) rejalashtirasizmi yoki faqat oy oxirida hisob-kitob qilasizmi?")]
        public bool HasCashflowPlanning { get; set; }

        [Display(Name = "Yillik byudjet va reja-fakt tahlili yuritasizmi?")]
        public bool HasBudgeting { get; set; }

        [Display(Name = "Debitor/kreditor qarzdorlikni tizimda doimiy kuzatib borasizmi?")]
        public bool TracksReceivablesPayablesInSystem { get; set; }

        // Supply chain - ta'minot va sotib olishdagi og'riqlar
        [Display(Name = "Faol yetkazib beruvchilar soni (asosiy)")]
        [Range(0, 1000)]
        public int SuppliersCount { get; set; }

        [Display(Name = "Buyurtma berganingizdan yetkazib berilgunga qadar bo'lgan muddati (lead time)ni aniq bilasizmi?")]
        public bool HasLeadTimeTracking { get; set; }

        [Display(Name = "Xarid jarayonlari qog'oz/Excelda emas, tizimda (buyurtma → qabul → to'lov) boshqariladimi?")]
        public bool HasIntegratedPurchasing { get; set; }

        [Display(Name = "Asosiy materiallar uchun minimal/maximal zaxira darajalari belgilangan va amalda ishlaydimi?")]
        public bool HasMinMaxLevels { get; set; }

        // Ombor - qoldiq va inventarizatsiya og'riqlari
        [Display(Name = "Murakkab mahsulotlar uchun BOM/resept asosida xomashyo hisobini yuritasizmi?")]
        public bool HasBOM { get; set; }

        [Display(Name = "Yiliga necha marta to'liq inventarizatsiya o'tkazasiz?")]
        [Range(0, 365)]
        public int InventoryCheckPerYear { get; set; }

        [Display(Name = "Har bir mahsulot bo'yicha real qoldiqni tizimdan real vaqtda ko'ra olasizmi?")]
        public bool CanSeeStockOnline { get; set; }

        [Display(Name = "Ombor xatoliklari (ortiqcha/yetishmovchilik)ni tahlil qilib, rasmiy hisobot qilasizmi?")]
        public bool AnalyzesWarehouseErrors { get; set; }

        // Shaffoflik (Transparency) - ko'rinuvchanlik og'riqlari
        [Display(Name = "Asosiy hisobotlar (foyda-zarar, zaxira, sotuv) avtomatik shakllanadimi?")]
        public bool HasAutomaticReports { get; set; }

        [Display(Name = "Asosiy KPI va ko'rsatkichlar uchun vizual dashboard/monitor bor mi?")]
        public bool CanSeeKPIsDashboard { get; set; }

        [Display(Name = "Rahbariyat uchun tezkor qaror qabul qilishga yordam beradigan tayyor vizual hisobotlar bormi?")]
        public bool HasManagementDashboards { get; set; }

        // Avtomatizatsiya (Automation) - qo'lda ish hajmi
        [Display(Name = "Jarayonlar avtomatizatsiya darajasi (qanchalik qo'lda/Excel, qanchalik tizimda)?")]
        public ProcessAutomationLevel ProcessAutomationLevel { get; set; }

        // Qulaylik (Usability) - xodimlar uchun qulaylik
        [Display(Name = "Tizim/Excel/qog'ozdan faol foydalanayotgan xodimlar soni")]
        [Range(0, 10000)]
        public int UsersCountUsingSystem { get; set; }

        [Display(Name = "Tizim bo'yicha trening/materiallar mavjudmi?")]
        public bool HasTrainingMaterials { get; set; }

        [Display(Name = "Xodimlar hozirgi tizim/uslubdan qanchalik qoniqadi? (subyektiv baho)")]
        public bool UsersSatisfiedWithSystem { get; set; }

        [Display(Name = "Tizim/Excel bo'yicha ichki 'ustoz' (key user/super user)larga haddan tashqari bog'liqlik bormi?")]
        public bool HasKeyUsers { get; set; }

        // Natija
        public DiagnosisResultViewModel Result { get; set; }
    }

    /// <summary>
    /// Jarayonlar avtomatizatsiya darajasi uchun enum.
    /// </summary>
    public enum ProcessAutomationLevel
    {
        FullyManual = 0,
        PartiallyAutomated = 1,
        MostlyAutomated = 2
    }

    /// <summary>
    /// Diagnosis natijalarini (yulduzlar va izohlar) ko'rsatish modeli.
    /// </summary>
    public class DiagnosisResultViewModel
    {
        public int FinanceStars { get; set; }
        public string FinanceLabel { get; set; }

        public int SupplyChainStars { get; set; }
        public string SupplyChainLabel { get; set; }

        public int InventoryStars { get; set; }
        public string InventoryLabel { get; set; }

        public int TransparencyStars { get; set; }
        public string TransparencyLabel { get; set; }

        public int AutomationStars { get; set; }
        public string AutomationLabel { get; set; }

        public int UsabilityStars { get; set; }
        public string UsabilityLabel { get; set; }

        // Bloklar bo'yicha modul tavsiyalari (vergul bilan ajratilgan ro'yxatlar)
        public string FinanceRecommendedModules { get; set; }
        public string SupplyChainRecommendedModules { get; set; }
        public string InventoryRecommendedModules { get; set; }
        public string TransparencyRecommendedModules { get; set; }
        public string AutomationRecommendedModules { get; set; }
        public string UsabilityRecommendedModules { get; set; }

        // Umumiy, birlashtirilgan modul tavsiyalari (unikal ro'yxat)
        public string CombinedRecommendedModules { get; set; }

        // Har blok bo'yicha marketing-tildagi diagnoz matnlari
        public string FinanceNarrative { get; set; }
        public string SupplyChainNarrative { get; set; }
        public string InventoryNarrative { get; set; }
        public string TransparencyNarrative { get; set; }
        public string AutomationNarrative { get; set; }
        public string UsabilityNarrative { get; set; }

        // Umumiy qisqa xulosa
        public string OverallNarrative { get; set; }
    }
}

