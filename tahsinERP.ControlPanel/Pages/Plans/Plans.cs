using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Plans;

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    public List<Plan> Plans { get; private set; } = [];
    public async Task OnGetAsync() => Plans = await db.Plans.OrderBy(p => p.SortOrder).ToListAsync();
}

/// <summary>Tarif tahrirlash (faqat Owner). Kod (= Edition) o'zgarmaydi — ERP modul xaritasi shunga bog'langan.</summary>
[Authorize(Policy = Policies.Owner)]
public class EditModel(ControlDbContext db, AuditLogger audit) : PanelPageModel
{
    public Plan Plan { get; private set; } = null!;
    [BindProperty, Required(ErrorMessage = "Nomi majburiy"), StringLength(100)] public string Name { get; set; } = "";
    [BindProperty] public string? PriceMonth { get; set; }
    [BindProperty] public string? PriceYear { get; set; }
    [BindProperty, Range(1, 10000, ErrorMessage = "1–10000")] public int MaxUsers { get; set; }
    [BindProperty, StringLength(500)] public string? Modules { get; set; }
    [BindProperty, Range(0, 100, ErrorMessage = "0–100")] public decimal VatRatePercent { get; set; }
    [BindProperty, Range(1, 90)] public int? TrialDays { get; set; }
    [BindProperty] public bool IsActive { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return NotFound();
        Plan = plan;
        Name = plan.Name; MaxUsers = plan.MaxUsers; Modules = plan.Modules; VatRatePercent = plan.VatRatePercent;
        TrialDays = plan.TrialDays; IsActive = plan.IsActive;
        PriceMonth = Money.Format(plan.PriceMonthTiyin); PriceYear = Money.Format(plan.PriceYearTiyin);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return NotFound();
        Plan = plan;
        long? month = ParsePrice(PriceMonth, nameof(PriceMonth));
        long? year = ParsePrice(PriceYear, nameof(PriceYear));
        if (plan.IsAddOn)
        {
            // Qo'shimcha xizmat foydalanuvchiga bog'liq emas — chegara 0 bo'lib qoladi.
            ModelState.Remove(nameof(MaxUsers));
            MaxUsers = 0;
        }
        if (!ModelState.IsValid) return Page();
        var before = new { plan.Name, plan.PriceMonthTiyin, plan.PriceYearTiyin, plan.MaxUsers, plan.Modules, plan.VatRatePercent, plan.TrialDays, plan.IsActive };
        plan.Name = Name.Trim(); plan.PriceMonthTiyin = month; plan.PriceYearTiyin = year; plan.MaxUsers = MaxUsers;
        plan.Modules = (Modules ?? "").Replace(" ", ""); plan.VatRatePercent = VatRatePercent; plan.IsActive = IsActive;
        if (plan.Code == PlanCodes.Trial) plan.TrialDays = TrialDays ?? 14;
        audit.Add("plan.update", "Plan", plan.Id, before,
            new { plan.Name, plan.PriceMonthTiyin, plan.PriceYearTiyin, plan.MaxUsers, plan.Modules, plan.VatRatePercent, plan.TrialDays, plan.IsActive });
        await db.SaveChangesAsync();
        FlashOk = $"«{plan.Name}» saqlandi.";
        return RedirectToPage("Index");
    }

    private long? ParsePrice(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (Money.TryParse(text, out long t)) return t;
        ModelState.AddModelError(field, "Summa noto'g'ri (masalan 1 500 000).");
        return null;
    }
}
