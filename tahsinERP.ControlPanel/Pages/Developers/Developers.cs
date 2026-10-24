using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Pages.Developers;

// ERP super-foydalanuvchilari (dasturchilar). Butun papka faqat Owner (Program.cs: AuthorizeFolder("/Developers", Owner)).
// Login/parol panelda; firma ERP'si kirishni License API (POST /api/license/{id}/dev-login) orqali tekshiradi.

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    public List<ErpDeveloper> Rows { get; private set; } = [];
    public async Task OnGetAsync() => Rows = await db.ErpDevelopers.OrderBy(d => d.Login).ToListAsync();
}

public class CreateModel(ControlDbContext db, AuditLogger audit, Clock clock) : PanelPageModel
{
    [BindProperty, Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FieldRequired))]
    [RegularExpression(@"^[A-Za-z0-9._-]{3,30}$", ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.DevLoginFormat))]
    public string Login { get; set; } = "";
    [BindProperty, Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FullNameRequired)), StringLength(50)]
    public string FullName { get; set; } = "";
    [BindProperty, Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FieldRequired)), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PasswordHasher.ValidatePolicy(Password) is string err) ModelState.AddModelError(nameof(Password), err);
        if (!ModelState.IsValid) return Page();
        string login = Login.Trim();
        if (await db.ErpDevelopers.AnyAsync(d => d.Login == login))
        {
            ModelState.AddModelError(nameof(Login), L.T("Pnl_Dev_LoginTaken"));
            return Page();
        }
        var dev = new ErpDeveloper
        {
            Login = login,
            FullName = FullName.Trim(),
            PasswordHash = PasswordHasher.Hash(Password),
            CreatedAtUtc = clock.UtcNow,
        };
        db.ErpDevelopers.Add(dev);
        await db.SaveChangesAsync();
        audit.Add("erpdev.create", "ErpDeveloper", dev.Id, after: new { dev.Login, dev.FullName });
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Dev_Added", dev.Login);
        return RedirectToPage("Index");
    }
}

public class EditModel(ControlDbContext db, AuditLogger audit) : PanelPageModel
{
    public ErpDeveloper Dev { get; private set; } = null!;
    [BindProperty, Required(ErrorMessageResourceType = typeof(PnlVal), ErrorMessageResourceName = nameof(PnlVal.FullNameRequired)), StringLength(50)]
    public string FullName { get; set; } = "";
    [BindProperty] public bool IsActive { get; set; }
    [BindProperty, DataType(DataType.Password)] public string? NewPassword { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var d = await db.ErpDevelopers.FindAsync(id);
        if (d is null) return false;
        Dev = d;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        FullName = Dev.FullName; IsActive = Dev.IsActive;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        ModelState.Remove(nameof(NewPassword));
        if (!ModelState.IsValid) return Page();
        var before = new { Dev.FullName, Dev.IsActive };
        Dev.FullName = FullName.Trim();
        Dev.IsActive = IsActive;
        audit.Add("erpdev.update", "ErpDeveloper", Dev.Id, before, new { Dev.FullName, Dev.IsActive });
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_SavedDot");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostPasswordAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (PasswordHasher.ValidatePolicy(NewPassword) is string err) { ModelState.AddModelError(nameof(NewPassword), err); FullName = Dev.FullName; IsActive = Dev.IsActive; return Page(); }
        Dev.PasswordHash = PasswordHasher.Hash(NewPassword!);
        Dev.FailedLogins = 0; Dev.LockedUntilUtc = null;
        audit.Add("erpdev.password-reset", "ErpDeveloper", Dev.Id);
        await db.SaveChangesAsync();
        FlashOk = L.T("Pnl_Staff_PasswordReset");
        return RedirectToPage(new { id });
    }
}
