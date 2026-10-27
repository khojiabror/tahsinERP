namespace tahsinERP.ControlPanel.Data;

// Iqtisodiyot (Docs/ControlPanel/10_ECONOMICS.md): reja tannarx/narx modeli va haqiqiy xarajatlar.
// Pul — tiyin (so'm × 100), boshqa jadvallar bilan bir xil. Dollar faqat kiritishda: kurs bo'yicha so'mga o'giriladi.

/// <summary>
/// Xarajat moddasi. Infra va Support — to'g'ridan-to'g'ri tannarx (COGS, mijozga xizmat): gross margin shundan o'lchanadi.
/// Development, Sales, Admin — operatsion xarajatlar (OPEX: R&amp;D, S&amp;M, G&amp;A), mijozlar soniga bog'liq emas.
/// </summary>
public enum CostCategory { Infra = 1, Support = 2, Development = 3, Sales = 4, Admin = 5 }

public enum PlannedCostKind { Staff = 1, Other = 2 }

/// <summary>Reja modeli farazlari — bitta qator (Id = 1).</summary>
public class EconomicsSettings
{
    public int Id { get; set; }
    /// <summary>So'm / $ — dollarda kiritilgan xarajat va ekrandagi $ ekvivalenti uchun.</summary>
    public decimal UsdRate { get; set; }
    /// <summary>Ish beruvchi to'laydigan ijtimoiy soliq, % (maosh ustiga).</summary>
    public decimal PayrollTaxPercent { get; set; }

    /// <summary>1 server oylik narxi (AWS Lightsail).</summary>
    public long ServerPriceTiyin { get; set; }
    /// <summary>1 serverga sig'adigan firmalar (har firma alohida ERP pool).</summary>
    public int FirmsPerServer { get; set; }
    /// <summary>Mijoz bo'lmasa ham ishlaydigan serverlar (panel, bot, demo).</summary>
    public int MinServers { get; set; }
    /// <summary>1 firma uchun o'zgaruvchan xarajat: zaxira hajmi, bot, email/SMS.</summary>
    public long PerFirmTiyin { get; set; }
    /// <summary>Platforma doimiy xarajati: domen, SSL, KMS.</summary>
    public long PlatformTiyin { get; set; }

    public long SupportSalaryTiyin { get; set; }
    /// <summary>1 support xodimi nechta firmaga xizmat qiladi — oshsa yangi xodim (pog'onali xarajat).</summary>
    public int FirmsPerSupport { get; set; }
    public int MinSupport { get; set; }

    /// <summary>Tushumdan: aylanma soliq / IT Park ajratmasi, %. QQS to'lovchisida 0.</summary>
    public decimal TurnoverTaxPercent { get; set; }
    /// <summary>Tushumdan: bank / to'lov komissiyasi, %.</summary>
    public decimal BankFeePercent { get; set; }
    /// <summary>Foyda solig'i, % (umumiy rejim 15, IT Park / aylanma rejimida 0).</summary>
    public decimal ProfitTaxPercent { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>Rejadagi doimiy xarajat: jamoa (Staff — soni × maosh, ustiga ijtimoiy soliq) yoki boshqa oylik xarajat.</summary>
public class PlannedCost
{
    public int Id { get; set; }
    public PlannedCostKind Kind { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Staff: xodimlar soni; Other: 1.</summary>
    public int Count { get; set; } = 1;
    /// <summary>Oylik summa (Staff — 1 kishining maoshi).</summary>
    public long AmountTiyin { get; set; }
    /// <summary>Faqat OPEX moddalari: Development / Sales / Admin.</summary>
    public CostCategory Category { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Modul og'irligi (ball): murakkablik va resurs talabi. Paket xarajati shu ballar × foydalanuvchilar bo'yicha taqsimlanadi.</summary>
public class ModuleWeight
{
    /// <summary>ProductModule nomi ("Core" — har doim bor asosiy qism).</summary>
    public string Module { get; set; } = "";
    public int Weight { get; set; }
}

/// <summary>Paketning reja ko'rsatkichlari.</summary>
public class PlanEconomics
{
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    /// <summary>12 oy ichida sotish rejasi (firmalar). Tannarx shu hajm bo'yicha taqsimlanadi.</summary>
    public int PlannedTenants { get; set; }
    /// <summary>Sof foyda ustamasi, to'liq tannarxga nisbatan %.</summary>
    public decimal MarkupPercent { get; set; }
    /// <summary>Maqsadli gross margin, % (SaaS 60–80, starter 40–55).</summary>
    public decimal TargetGrossMarginPercent { get; set; }
}

/// <summary>Raqobatchi tarifi — bozor narxi bilan solishtirish uchun.</summary>
public class Competitor
{
    public int Id { get; set; }
    public string Company { get; set; } = "";
    public string Tariff { get; set; } = "";
    /// <summary>Oylik narx so'mda (bir martalik litsenziya bo'lsa — oyga bo'lingani).</summary>
    public long? PriceMonthTiyin { get; set; }
    /// <summary>Oylik narx dollarda (so'm berilmagan bo'lsa ishlatiladi).</summary>
    public decimal? PriceMonthUsd { get; set; }
    /// <summary>Tarifga kirgan foydalanuvchilar. Null — noma'lum: 1 foydalanuvchi narxi hisoblanmaydi.</summary>
    public int? Users { get; set; }
    /// <summary>Qaysi paketimizga o'xshaydi (Plan.Code).</summary>
    public string? MatchPlanCode { get; set; }
    /// <summary>Bozor o'rtachasiga kiradimi (narxi noma'lum yoki xalqaro mo'ljal — yo'q).</summary>
    public bool Include { get; set; } = true;
    /// <summary>Imkoniyatlar: kalitlar vergul bilan, "~" — qisman (Warehouse,Sales,~Finance).</summary>
    public string Features { get; set; } = "";
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Haqiqiy xarajat (kirim qilingan). Faktni tizim shular va to'lovlardan hisoblaydi.</summary>
public class Expense
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public CostCategory Category { get; set; }
    public string Description { get; set; } = "";
    /// <summary>"UZS" yoki "USD".</summary>
    public string Currency { get; set; } = "UZS";
    /// <summary>Kiritilgan summa (o'z valyutasida).</summary>
    public decimal AmountOriginal { get; set; }
    /// <summary>So'm / valyuta birligi (UZS uchun 1).</summary>
    public decimal Rate { get; set; } = 1m;
    /// <summary>So'mdagi summa (tiyin) — hisob-kitob shu bo'yicha.</summary>
    public long AmountTiyin { get; set; }
    /// <summary>Hisob/chek raqami, kontragent.</summary>
    public string? Reference { get; set; }
    public int RecordedByStaffId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
